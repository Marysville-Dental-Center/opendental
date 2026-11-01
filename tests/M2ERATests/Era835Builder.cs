// Builds small synthetic ERAs (X12 835, 5010 layout) for the M2-ERA tests: one or more claims (CLP loops), each with procedure lines, in one or
// more EOBs (transaction sets). Made-up payer, payee, patients, trace numbers and Z codes only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace M2ERATests {
	///<summary>One service line (SVC) of a synthetic ERA claim.</summary>
	public class EraLine {
		public string Code;
		public decimal Fee;
		public decimal InsPaid;
		///<summary>Sent back in REF*6R as pProcNum when not 0, the way our 837s identify procedures.</summary>
		public long ProcNum;
		///<summary>A CO-45 adjustment (contractual write-off) of this amount when not 0.</summary>
		public decimal WriteOff;

		public EraLine(string code,decimal fee,decimal insPaid,long procNum=0,decimal writeOff=0) {
			Code=code;
			Fee=fee;
			InsPaid=insPaid;
			ProcNum=procNum;
			WriteOff=writeOff;
		}
	}

	///<summary>One claim (CLP loop) of a synthetic ERA.</summary>
	public class EraClp {
		public string ClaimIdentifier;
		///<summary>CLP02: 1 processed as primary, 22 reversal of a previous payment, 25 predetermination.</summary>
		public string Clp02="1";
		public string LName="Synth";
		public string FName="Patient";
		///<summary>CLP07.</summary>
		public string PayerControlNumber="";
		public DateTime DateService=new DateTime(2026,9,1);
		public List<EraLine> ListLines=new List<EraLine>();

		public decimal ClaimFee {
			get { return ListLines.Sum(x => x.Fee); }
		}

		public decimal InsPaid {
			get { return ListLines.Sum(x => x.InsPaid); }
		}
	}

	///<summary>One EOB (transaction set, ST/SE) of a synthetic ERA.</summary>
	public class EraEob {
		///<summary>ST02, which is etrans.TranSetId835 for an ERA imported since 14.3.</summary>
		public string TranSetId="0001";
		///<summary>TRN02: the check or EFT trace number.</summary>
		public string TraceNumber="SYNTRACE1";
		public List<EraClp> ListClps=new List<EraClp>();

		///<summary>BPR02: the total payment, the sum of the claims' payments.</summary>
		public decimal TotalPaid {
			get { return ListClps.Sum(x => x.InsPaid); }
		}
	}

	public static class Era835Builder {
		private static string Amt(decimal amount) {
			return amount.ToString("0.##",CultureInfo.InvariantCulture);
		}

		///<summary>An 835 with one EOB holding the given claims.</summary>
		public static string Build(string traceNumber,params EraClp[] arrayClps) {
			EraEob eraEob=new EraEob() { TraceNumber=traceNumber,ListClps=arrayClps.ToList() };
			return Build(eraEob);
		}

		///<summary>An 835 file with the given EOBs, one functional group.</summary>
		public static string Build(params EraEob[] arrayEobs) {
			string isa="ISA*00*"+"".PadRight(10)+"*00*"+"".PadRight(10)+"*ZZ*"+"SYNTHPAYER".PadRight(15)+"*ZZ*"+"SYNTHOFFICE".PadRight(15)
				+"*261020*1200*U*00501*000000001*0*T*:";
			if(isa.Length!=105) {
				throw new ApplicationException("ISA must be 105 characters before the segment terminator, was "+isa.Length);
			}
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append(isa+"~");
			stringBuilder.Append("GS*HP*SYNTHPAYER*SYNTHOFFICE*20261020*1200*1*X*005010X221A1~");
			foreach(EraEob eraEob in arrayEobs) {
				foreach(string segment in BuildTransaction(eraEob)) {
					stringBuilder.Append(segment+"~");
				}
			}
			stringBuilder.Append("GE*"+arrayEobs.Length+"*1~");
			stringBuilder.Append("IEA*1*000000001~");
			return stringBuilder.ToString();
		}

		private static List<string> BuildTransaction(EraEob eraEob) {
			List<string> listSegments=new List<string>();
			listSegments.Add("ST*835*"+eraEob.TranSetId);
			listSegments.Add("BPR*I*"+Amt(eraEob.TotalPaid)+"*C*CHK************20261020");
			listSegments.Add("TRN*1*"+eraEob.TraceNumber+"*1999999999");
			listSegments.Add("DTM*405*20261020");
			listSegments.Add("N1*PR*SYNTHETIC TEST PAYER");
			listSegments.Add("N3*1 TEST WAY");
			listSegments.Add("N4*TESTVILLE*OR*97000");
			listSegments.Add("N1*PE*SYNTHETIC DENTAL OFFICE*XX*1999999999");
			listSegments.Add("LX*1");
			foreach(EraClp eraClp in eraEob.ListClps) {
				decimal patResp=0;
				listSegments.Add("CLP*"+eraClp.ClaimIdentifier+"*"+eraClp.Clp02+"*"+Amt(eraClp.ClaimFee)+"*"+Amt(eraClp.InsPaid)+"*"+Amt(patResp)+"*12*"
					+eraClp.PayerControlNumber+"*11");
				listSegments.Add("NM1*QC*1*"+eraClp.LName.ToUpperInvariant()+"*"+eraClp.FName.ToUpperInvariant()+"****MI*SYNSUB1");
				listSegments.Add("DTM*050*20261015");
				foreach(EraLine line in eraClp.ListLines) {
					listSegments.Add("SVC*AD:"+line.Code+"*"+Amt(line.Fee)+"*"+Amt(line.InsPaid)+"**1");
					if(eraClp.Clp02!="25") {//Predeterminations have no dates of service.
						listSegments.Add("DTM*472*"+eraClp.DateService.ToString("yyyyMMdd",CultureInfo.InvariantCulture));
					}
					if(line.WriteOff!=0) {
						listSegments.Add("CAS*CO*45*"+Amt(line.WriteOff));
					}
					if(line.ProcNum!=0) {
						listSegments.Add("REF*6R*p"+line.ProcNum);
					}
				}
			}
			listSegments.Add("SE*"+(listSegments.Count+1)+"*"+eraEob.TranSetId);
			return listSegments;
		}
	}
}
