// A connection as SSH.NET makes it, without a server, for the host key check's tests.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using OpenPmsDentalStorage;
using Renci.SshNet.Common;
using Renci.SshNet.Messages.Transport;

namespace SshHostKeyTests {
	///<summary>Each key exchange raises HostKeyReceived on the check and stops the connection when the key is not trusted, as SSH.NET does; the
	///password is sent after the first key exchange, then any re-key follows. The check is attached through its test constructor.</summary>
	public class SimulatedConnection {
		public SshHostKeyCheck Check;
		///<summary>The host key of each key exchange: the first, then re-keys.</summary>
		public List<HostKeyEventArgs> ListKeyExchanges=new List<HostKeyEventArgs>();
		public bool IsPasswordAccepted=true;
		public bool WasPasswordSent;
		public int CountConnects;
		public int CountDisconnects;

		public SimulatedConnection(string host,int port,ISshHostKeyStore store,params HostKeyEventArgs[] arrayKeyExchanges) {
			Check=new SshHostKeyCheck(host,port,store,Connect,() => CountDisconnects++);
			ListKeyExchanges.AddRange(arrayKeyExchanges);
		}

		private void Connect() {
			CountConnects++;
			WasPasswordSent=false;
			for(int i=0;i<ListKeyExchanges.Count;i++) {
				if(i==1) {
					SendPassword();
				}
				Check.OnHostKeyReceived(ListKeyExchanges[i]);
				if(!ListKeyExchanges[i].CanTrust) {
					throw new SshConnectionException("Key exchange negotiation failed.",DisconnectReason.KeyExchangeFailed);
				}
			}
			if(ListKeyExchanges.Count<=1) {
				SendPassword();
			}
		}

		private void SendPassword() {
			WasPasswordSent=true;
			if(!IsPasswordAccepted) {
				throw new SshAuthenticationException("Permission denied (password).");
			}
		}
	}
}
