// Open Dental 26.2 (P262-GLIDEWELL): the lab cases sent to Glidewell (glidewellmsg), one row per lab case.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Reflection;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>One row per lab case sent to Glidewell (Open Dental's 26.2 database documents: "We insert the row when we send to GW the first time.
	///After that, when the user checks status, we just update the existing with new values for RawJsonResponse and OrderStatus. We don't keep a
	///history of glidewell messages."). GlidewellLabCases writes them; nothing here calls Glidewell.</summary>
	public class GlidewellMsgs {
		///<summary>The lab case's row, or null when it was never sent. If several rows exist (written elsewhere), the first one.</summary>
		public static GlidewellMsg GetForLabCase(long labCaseNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<GlidewellMsg>(MethodBase.GetCurrentMethod(),labCaseNum);
			}
			if(labCaseNum==0) {
				return null;
			}
			string command="SELECT * FROM glidewellmsg WHERE LabCaseNum="+POut.Long(labCaseNum)+" ORDER BY GlidewellMsgNum LIMIT 1";
			return Crud.GlidewellMsgCrud.SelectOne(command);
		}

		///<summary>Null if there is no such row.</summary>
		public static GlidewellMsg GetOne(long glidewellMsgNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<GlidewellMsg>(MethodBase.GetCurrentMethod(),glidewellMsgNum);
			}
			return Crud.GlidewellMsgCrud.SelectOne(glidewellMsgNum);
		}

		public static long Insert(GlidewellMsg glidewellMsg) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				glidewellMsg.GlidewellMsgNum=Meth.GetLong(MethodBase.GetCurrentMethod(),glidewellMsg);
				return glidewellMsg.GlidewellMsgNum;
			}
			return Crud.GlidewellMsgCrud.Insert(glidewellMsg);
		}

		public static void Update(GlidewellMsg glidewellMsg) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),glidewellMsg);
				return;
			}
			Crud.GlidewellMsgCrud.Update(glidewellMsg);
		}
	}
}
