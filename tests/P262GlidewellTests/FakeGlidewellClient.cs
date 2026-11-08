// A stand-in for Glidewell in the P262-GLIDEWELL tests: records every call and answers with what the test set. Never opens a connection.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect.Glidewell;

namespace P262GlidewellTests {
	public sealed class FakeGlidewellClient:IGlidewellClient {
		///<summary>One recorded call.</summary>
		public sealed class Call {
			public string Method;
			public string AccountNumber;
			public string ApiKey;
			public string RequestJson;
			public long GlidewellMsgNum;
			public long LabCaseNum;
			public string VendorRxId;
		}

		public readonly List<Call> ListCalls=new List<Call>();
		///<summary>What Register answers; Success with a small body by default.</summary>
		public Func<GlidewellCallResult> FuncRegister=() => GlidewellCallResult.Success("{\"VendorRxId\":\"RX-SYN-1\",\"VendorName\":\"iTero\"}");
		///<summary>What GetStatus answers.</summary>
		public Func<GlidewellCallResult> FuncGetStatus=() => GlidewellCallResult.Success("{\"OrderStatus\":\"InProgress\"}");

		public bool IsAvailable { get; set; }=true;

		public string UnavailableReason {
			get {
				return IsAvailable ? "" : "Fake client switched off.";
			}
		}

		public GlidewellCallResult Register(GlidewellCredentials credentials,string requestJson) {
			ListCalls.Add(new Call { Method="Register",AccountNumber=credentials.AccountNumber,ApiKey=credentials.ApiKey,RequestJson=requestJson });
			return FuncRegister();
		}

		public GlidewellCallResult GetStatus(GlidewellCredentials credentials,GlidewellMsg glidewellMsg) {
			ListCalls.Add(new Call { Method="GetStatus",AccountNumber=credentials.AccountNumber,ApiKey=credentials.ApiKey,
				GlidewellMsgNum=glidewellMsg.GlidewellMsgNum,LabCaseNum=glidewellMsg.LabCaseNum,VendorRxId=glidewellMsg.VendorRxId });
			return FuncGetStatus();
		}
	}
}
