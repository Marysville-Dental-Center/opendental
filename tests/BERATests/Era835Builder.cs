// Builds a small synthetic ERA (X12 835, 5010 layout) for the database tests: one claim with the given procedure lines. Made-up payer, payee,
// patient and Z codes only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace BERATests {
	public class Era835Line {
		public string Code;
		public decimal Fee;
		public decimal InsPaid;
		///<summary>Sent back in REF*6R as pProcNum when not 0, the way our 837s identify procedures.</summary>
		public long ProcNum;
		public List<(string GroupCode,string ReasonCode,decimal Amount)> ListAdjs=new List<(string,string,decimal)>();

		public Era835Line(string code,decimal fee,decimal insPaid,long procNum,params (string,string,decimal)[] arrayAdjs) {
			Code=code;
			Fee=fee;
			InsPaid=insPaid;
			ProcNum=procNum;
			ListAdjs=arrayAdjs.ToList();
		}
	}

	public static class Era835Builder {
		private static string Amt(decimal amount) {
			return amount.ToString("0.##",CultureInfo.InvariantCulture);
		}

		///<summary>An 835 with one CLP for claimIdentifier. The claim totals are the sums of the lines; BPR02 is the claim's payment.</summary>
		public static string Build(string claimIdentifier,string lName,string fName,DateTime dateService,List<Era835Line> listLines,
			List<(string GroupCode,string ReasonCode,decimal Amount)> listClaimAdjs=null)
		{
			listClaimAdjs=listClaimAdjs??new List<(string,string,decimal)>();
			decimal fee=listLines.Sum(x => x.Fee);
			decimal insPaid=listLines.Sum(x => x.InsPaid);
			decimal patResp=listLines.SelectMany(x => x.ListAdjs).Where(x => x.GroupCode=="PR").Sum(x => x.Amount)
				+listClaimAdjs.Where(x => x.GroupCode=="PR").Sum(x => x.Amount);
			string isa="ISA*00*"+"".PadRight(10)+"*00*"+"".PadRight(10)+"*ZZ*"+"SYNTHPAYER".PadRight(15)+"*ZZ*"+"SYNTHOFFICE".PadRight(15)
				+"*261020*1200*U*00501*000000001*0*T*:";
			if(isa.Length!=105) {
				throw new ApplicationException("ISA must be 105 characters before the segment terminator, was "+isa.Length);
			}
			List<string> listSegments=new List<string>();
			listSegments.Add("ST*835*0001");
			listSegments.Add("BPR*I*"+Amt(insPaid)+"*C*CHK************20261020");
			listSegments.Add("TRN*1*SYNCHK"+claimIdentifier+"*1999999999");
			listSegments.Add("DTM*405*20261020");
			listSegments.Add("N1*PR*SYNTHETIC TEST PAYER");
			listSegments.Add("N3*1 TEST WAY");
			listSegments.Add("N4*TESTVILLE*OR*97000");
			listSegments.Add("N1*PE*SYNTHETIC DENTAL OFFICE*XX*1999999999");
			listSegments.Add("LX*1");
			listSegments.Add("CLP*"+claimIdentifier+"*1*"+Amt(fee)+"*"+Amt(insPaid)+"*"+Amt(patResp)+"*12*SYNPCN"+claimIdentifier+"*11");
			foreach((string GroupCode,string ReasonCode,decimal Amount) adj in listClaimAdjs) {
				listSegments.Add("CAS*"+adj.GroupCode+"*"+adj.ReasonCode+"*"+Amt(adj.Amount));
			}
			listSegments.Add("NM1*QC*1*"+lName.ToUpperInvariant()+"*"+fName.ToUpperInvariant()+"****MI*SYNSUB1");
			listSegments.Add("DTM*050*20261015");
			foreach(Era835Line line in listLines) {
				listSegments.Add("SVC*AD:"+line.Code+"*"+Amt(line.Fee)+"*"+Amt(line.InsPaid)+"**1");
				listSegments.Add("DTM*472*"+dateService.ToString("yyyyMMdd",CultureInfo.InvariantCulture));
				foreach((string GroupCode,string ReasonCode,decimal Amount) adj in line.ListAdjs) {
					listSegments.Add("CAS*"+adj.GroupCode+"*"+adj.ReasonCode+"*"+Amt(adj.Amount));
				}
				if(line.ProcNum!=0) {
					listSegments.Add("REF*6R*p"+line.ProcNum);
				}
			}
			listSegments.Add("SE*"+(listSegments.Count+1)+"*0001");
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append(isa+"~");
			stringBuilder.Append("GS*HP*SYNTHPAYER*SYNTHOFFICE*20261020*1200*1*X*005010X221A1~");
			foreach(string segment in listSegments) {
				stringBuilder.Append(segment+"~");
			}
			stringBuilder.Append("GE*1*1~");
			stringBuilder.Append("IEA*1*000000001~");
			return stringBuilder.ToString();
		}
	}
}
