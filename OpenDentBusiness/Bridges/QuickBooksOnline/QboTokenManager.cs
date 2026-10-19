// QuickBooks Online (direct): access tokens in memory, the rotating refresh token in the database with compare-and-swap, and one retry on 401.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBase;
using CodeBase.VendorDirect;
using OpenDentBusiness.VendorDirect;

namespace OpenDentBusiness.Qbo {
	///<summary>Where the shared connection lives. ReadCurrent always reads the database (another workstation may have rotated the token); TryReplace is a
	///compare-and-swap.</summary>
	public interface IQboConnectionStore : IRefreshTokenStore {
		///<summary>The stored "Refresh Token Expires", or null.</summary>
		DateTime? ReadRefreshTokenExpiresUtc();
		void WriteRefreshTokenExpiresUtc(DateTime utc);
		///<summary>Tells other workstations, and this one's cache, that a stored value changed.</summary>
		void SignalChanged();
	}

	///<summary>The QuickBooksOnline program's rows (ClinicNum 0). Reads and writes go straight to the database and are safe under the middle tier.</summary>
	public sealed class QboProgramPropertyStore : IQboConnectionStore {
		private readonly long _programNum;
		private readonly ProgramPropertyRefreshTokenStore _refreshTokenStore;

		public QboProgramPropertyStore(long programNum) {
			_programNum=programNum;
			//Refresh tokens issued under Open Dental's app (plain or another installation's ciphertext) are never sent.
			_refreshTokenStore=new ProgramPropertyRefreshTokenStore(programNum,QboSettings.PropRefreshToken,0,LegacyPolicy.RejectPlaintext);
		}

		public string ReadCurrent() {
			return _refreshTokenStore.ReadCurrent();
		}

		public bool TryReplace(string expectedCurrent,string newValue) {
			return _refreshTokenStore.TryReplace(expectedCurrent,newValue);
		}

		public DateTime? ReadRefreshTokenExpiresUtc() {
			ProgramProperty programProperty=ProgramProperties.GetOneFromDb(_programNum,QboSettings.PropRefreshTokenExpires,0);
			return QboSettings.ParseUtc(programProperty?.PropertyValue);
		}

		public void WriteRefreshTokenExpiresUtc(DateTime utc) {
			VendorCredentials.WriteSetting(_programNum,QboSettings.PropRefreshTokenExpires,0,QboSettings.FormatUtc(utc));
		}

		public void SignalChanged() {
			Signalods.SetInvalid(InvalidType.Programs);
			Cache.Refresh(InvalidType.Programs);
		}
	}

	///<summary>One per process and configuration. Hands out access tokens (memory only, refreshed 5 minutes before they expire, single-flight), and keeps
	///the shared refresh token current: each refresh reads the value from the database first, and a rotated value is written back with
	///compare-and-swap, so workstations never strand each other. After Intuit refuses the refresh token, every call fails at once with "needs to be
	///reconnected" until the stored token changes (someone reconnected).</summary>
	public sealed class QboTokenManager {
		///<summary>"Refresh Token Expires" is rewritten only when it moves by more than this, to avoid a write per refresh.</summary>
		public static readonly TimeSpan ExpiryWriteThreshold=TimeSpan.FromDays(1);
		private static readonly ConcurrentDictionary<string,QboTokenManager> _dictManagers=new ConcurrentDictionary<string,QboTokenManager>();

		private readonly TokenCache _tokenCache;
		private readonly IQboConnectionStore _store;
		private readonly Func<string,CancellationToken,Task<QboTokenResult>> _funcRedeem;
		///<summary>Fingerprint of the refresh token Intuit refused, so a reconnect by another workstation clears the "needs reconnect" state.</summary>
		private string _fingerprintRejected;

		///<summary>funcRedeem exchanges a refresh token at Intuit (QboOAuth.RefreshAsync).</summary>
		public QboTokenManager(TokenCache tokenCache,IQboConnectionStore store,Func<string,CancellationToken,Task<QboTokenResult>> funcRedeem) {
			_tokenCache=tokenCache ?? throw new ArgumentNullException(nameof(tokenCache));
			_store=store ?? throw new ArgumentNullException(nameof(store));
			_funcRedeem=funcRedeem ?? throw new ArgumentNullException(nameof(funcRedeem));
		}

		///<summary>The process-wide manager for the settings' configuration, created on first use.</summary>
		public static QboTokenManager For(QboSettings settings,Func<QboTokenManager> funcCreate) {
			return _dictManagers.GetOrAdd(settings.GetFingerprint(),x => funcCreate());
		}

		///<summary>Forgets every manager, for tests and after a disconnect.</summary>
		public static void ClearAll() {
			_dictManagers.Clear();
		}

		///<summary>True after Intuit refused the stored refresh token in this session.</summary>
		public bool NeedsReconnect {
			get {
				return _tokenCache.NeedsReauthorization;
			}
		}

		///<summary>A valid access token. Throws QboAuthException when the connection has to be made again or the keys are wrong, and
		///HttpRequestException for network failures (which never mark the connection as refused).</summary>
		public async Task<OAuthAccessToken> GetAccessTokenAsync(CancellationToken cancellationToken=default(CancellationToken)) {
			ClearRejectionIfReconnected();
			try {
				return await _tokenCache.GetAsync(FetchAsync,cancellationToken).ConfigureAwait(false);
			}
			catch(TokenRejectedException ex) {
				throw ToAuthException(ex);
			}
			catch(ReauthorizationRequiredException ex) {
				throw new QboAuthException(QboAuthException.MessageReconnect,innerException:ex);
			}
		}

		///<summary>Sends the request built by createRequest with the access token. On 401 it refreshes once and sends once more; a second 401 comes
		///back to the caller.</summary>
		public async Task<HttpResponseMessage> SendAsync(HttpClient httpClient,Func<HttpRequestMessage> createRequest,
			CancellationToken cancellationToken=default(CancellationToken))
		{
			ClearRejectionIfReconnected();
			try {
				return await _tokenCache.SendWithAuthRetryAsync(httpClient,createRequest,FetchAsync,cancellationToken).ConfigureAwait(false);
			}
			catch(TokenRejectedException ex) {
				throw ToAuthException(ex);
			}
			catch(ReauthorizationRequiredException ex) {
				throw new QboAuthException(QboAuthException.MessageReconnect,innerException:ex);
			}
		}

		///<summary>Drops the cached access token, so the next call refreshes.</summary>
		public void Invalidate() {
			_tokenCache.Invalidate();
		}

		///<summary>After a successful connect: clears any "needs reconnect" state and caches the new access token.</summary>
		public void Prime(QboTokenResult tokenResult) {
			_fingerprintRejected=null;
			_tokenCache.ClearReauthorization();
			_tokenCache.Invalidate();
			OAuthAccessToken token=tokenResult.ToAccessToken();
			_tokenCache.GetAsync(x => System.Threading.Tasks.Task.FromResult(token)).GetAwaiter().GetResult();
		}

		private void ClearRejectionIfReconnected() {
			if(!_tokenCache.NeedsReauthorization || _fingerprintRejected==null) {
				return;
			}
			string refreshTokenCurrent=_store.ReadCurrent();
			if(refreshTokenCurrent!="" && TokenCache.Fingerprint(refreshTokenCurrent)!=_fingerprintRejected) {
				_fingerprintRejected=null;
				_tokenCache.ClearReauthorization();
			}
		}

		private async Task<OAuthAccessToken> FetchAsync(CancellationToken cancellationToken) {
			TrackingStore trackingStore=new TrackingStore(_store);
			QboTokenResult tokenResultLast=null;
			OAuthAccessToken token;
			try {
				token=await RefreshTokenStrategy.RefreshRotatingAsync(trackingStore,async (refreshToken,ct) => {
					tokenResultLast=await _funcRedeem(refreshToken,ct).ConfigureAwait(false);
					return tokenResultLast.ToAccessToken();
				},cancellationToken).ConfigureAwait(false);
			}
			catch(TokenRejectedException) {
				_fingerprintRejected=TokenCache.Fingerprint(trackingStore.LastRead??"");
				throw;
			}
			bool isChanged=trackingStore.IsReplaced;
			if(tokenResultLast!=null && tokenResultLast.RefreshTokenExpiresUtc.HasValue) {
				DateTime? dateTimeStored=_store.ReadRefreshTokenExpiresUtc();
				DateTime dateTimeNew=tokenResultLast.RefreshTokenExpiresUtc.Value;
				if(dateTimeStored==null || (dateTimeNew-dateTimeStored.Value).Duration()>ExpiryWriteThreshold) {
					_store.WriteRefreshTokenExpiresUtc(dateTimeNew);
					isChanged=true;
				}
			}
			if(isChanged) {
				try {
					_store.SignalChanged();
				}
				catch(Exception ex) {
					//The database already holds the new values; other workstations read them on their next refresh anyway.
					ex.DoNothing();
				}
			}
			return token;
		}

		private static QboAuthException ToAuthException(TokenRejectedException ex) {
			if(ex.Error=="invalid_client") {
				return new QboAuthException(QboAuthException.MessageInvalidClient,needsReconnect:false,oAuthError:ex.Error,innerException:ex);
			}
			return new QboAuthException(QboAuthException.MessageReconnect,needsReconnect:true,oAuthError:ex.Error,innerException:ex);
		}

		///<summary>Remembers what the rotating strategy read and whether its compare-and-swap won.</summary>
		private sealed class TrackingStore : IRefreshTokenStore {
			private readonly IRefreshTokenStore _storeInner;
			public string LastRead;
			public bool IsReplaced;

			public TrackingStore(IRefreshTokenStore storeInner) {
				_storeInner=storeInner;
			}

			public string ReadCurrent() {
				LastRead=_storeInner.ReadCurrent();
				return LastRead;
			}

			public bool TryReplace(string expectedCurrent,string newValue) {
				bool isReplaced=_storeInner.TryReplace(expectedCurrent,newValue);
				IsReplaced|=isReplaced;
				return isReplaced;
			}
		}
	}
}
