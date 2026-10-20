// Tests for the PostcardMania (PCM Integrations) DirectMail API v3 integration: shared fixtures.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using OpenDentBusiness.PostcardMania;

namespace VendorDirectTests.PostcardMania {
	///<summary>A client over a FakeHttpHandler with a settable clock and recorded (not real) waits.</summary>
	public sealed class PostcardManiaFixture {
		public const string BaseUrl="https://v3.pcmintegrations.com/";
		public readonly FakeHttpHandler Handler=new FakeHttpHandler();
		public DateTime UtcNow=new DateTime(2026,10,19,12,0,0,DateTimeKind.Utc);
		public readonly List<TimeSpan> ListDelays=new List<TimeSpan>();
		public readonly PostcardManiaApiClient Client;

		public PostcardManiaFixture() {
			Client=new PostcardManiaApiClient(Handler,BaseUrl,() => UtcNow,
				(timeSpan,cancellationToken) => {
					lock(ListDelays) {
						ListDelays.Add(timeSpan);
					}
					return System.Threading.Tasks.Task.CompletedTask;
				},
				options => options.RetryBaseDelay=TimeSpan.FromMilliseconds(1));
		}

		public static PostcardManiaAccount MakeAccount(string apiKey="key-1",string apiSecret="secret-1",string childRefNbr="") {
			return new PostcardManiaAccount {
				ApiKey=apiKey,
				ApiSecret=apiSecret,
				ChildRefNbr=childRefNbr,
				Environment=PostcardManiaProgram.EnvironmentSandbox,
			};
		}

		///<summary>A login answer whose token expires 15 minutes after the fixture's clock.</summary>
		public string LoginJson(string token="tok-1",int minutes=15) {
			return "{\"token\":\""+token+"\",\"expires\":\""+UtcNow.AddMinutes(minutes).ToString("yyyy-MM-ddTHH:mm:ssZ")+"\"}";
		}

		public FakeHttpHandler EnqueueLogin(string token="tok-1",int minutes=15) {
			return Handler.Enqueue(HttpStatusCode.OK,LoginJson(token,minutes));
		}

		public List<RecordedRequest> RequestsTo(string pathAndMethod) {
			string[] arrayParts=pathAndMethod.Split(' ');
			HttpMethod method=new HttpMethod(arrayParts[0]);
			return Handler.Requests.Where(x => x.Method==method && x.RequestUri.AbsolutePath.TrimEnd('/')=="/"+arrayParts[1].Trim('/')).ToList();
		}

		public static PcmRecipient MakeRecipient(int number) {
			return new PcmRecipient {
				FirstName="First"+number,
				LastName="Last",
				Address=number+" Main St",
				City="Marysville",
				State="WA",
				ZipCode="98270",
			};
		}

		public static List<PcmRecipient> MakeRecipients(int count) {
			List<PcmRecipient> listRecipients=new List<PcmRecipient>(count);
			for(int i=0;i<count;i++) {
				listRecipients.Add(MakeRecipient(i));
			}
			return listRecipients;
		}

		public static PcmPostcardOrderRequest MakeOrder(string extRefNbr,int countRecipients=2) {
			return new PcmPostcardOrderRequest {
				DesignID=77,
				MailClass=PcmMailClass.FirstClass,
				ExtRefNbr=extRefNbr,
				MailDate=new DateTime(2026,11,2),
				Recipients=MakeRecipients(countRecipients),
			};
		}
	}
}
