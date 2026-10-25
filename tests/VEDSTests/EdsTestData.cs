// Synthetic test data for the V-EDS tests: claim snapshots, made-up NPIs and EDS answers. No office data.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect.Eds;

namespace VEDSTests {
	public static class EdsTestData {
		public static readonly DateTime DateToday=new DateTime(2026,10,20);

		///<summary>A made-up NPI: nine digits plus the check digit, computed the way CMS states the rule (double every other digit from the right of the
		///nine, add 24, the check digit takes the sum to a multiple of 10). Independent of the code under test, which uses the 80840-prefix Luhn form.
		///</summary>
		public static string MakeNpi(string nineDigits) {
			int sum=24;
			for(int i=0;i<9;i++) {
				int digit=nineDigits[8-i]-'0';
				if(i%2==0) {
					digit*=2;
					if(digit>9) {
						digit-=9;
					}
				}
				sum+=digit;
			}
			int check=(10-sum%10)%10;
			return nineDigits+check;
		}

		///<summary>An NPI with the wrong check digit.</summary>
		public static string MakeBadNpi(string nineDigits) {
			string npi=MakeNpi(nineDigits);
			int check=npi[9]-'0';
			return nineDigits+((check+1)%10);
		}

		///<summary>A snapshot that passes every local check: two providers with valid NPIs and taxonomy codes, a payer on a loaded EDS payer list as an
		///electronic dental payer, a subscriber ID, and service dates in the past.</summary>
		public static EdsClaimSnapshot MakeValidSnapshot() {
			EdsClaimSnapshot snapshot=new EdsClaimSnapshot();
			snapshot.ClaimNum=11;
			snapshot.PatNum=22;
			snapshot.ClaimType="P";
			snapshot.MedType=EnumClaimMedType.Dental;
			snapshot.DateService=DateToday.AddDays(-3);
			snapshot.DateToday=DateToday;
			snapshot.ProvBill=new EdsProviderInfo { ProvNum=1,IsFound=true,Abbr="BILL",Npi=MakeNpi("100000001"),Taxonomy="1223G0001X" };
			snapshot.ProvTreat=new EdsProviderInfo { ProvNum=2,IsFound=true,Abbr="TREAT",Npi=MakeNpi("100000002"),Taxonomy="1223P0221X" };
			snapshot.PayerId="ZZ123";
			snapshot.CarrierName="Synthetic Carrier";
			snapshot.SubscriberId="SUB0001";
			snapshot.ListProcLines.Add(new EdsProcLine { LineNumber=1,ProcNum=101,ProcCode="Z0001",ProcDate=DateToday.AddDays(-3) });
			snapshot.ListProcLines.Add(new EdsProcLine { LineNumber=2,ProcNum=102,ProcCode="Z0002",ProcDate=DateToday.AddDays(-3) });
			snapshot.IsEdsPayerListLoaded=true;
			snapshot.ListEdsPayers.Add(EdsClaimSnapshot.MakePayerListEntry("ZZ123","Synthetic Carrier",
				(int)EnumEDSPayerAttributes.DefaultClaimTP+","+(int)EnumEDSPayerAttributes.ERATP+","+(int)EnumEDSPayerAttributes.PayerType));
			snapshot.IsX12Validated=true;
			return snapshot;
		}

		public static List<string> CheckIds(List<EdsValidationIssue> listIssues) {
			return listIssues.Select(x => x.CheckId).ToList();
		}

		///<summary>An EDS attachment validation answer, in the JSON shape EDS.ListPayerResponses reads (camel case).</summary>
		public static string MakeAttachmentAnswer(bool isClaimLevel,params (string procId,bool response,string[] documentTypes,string comments)[] arrayProcedures) {
			List<string> listProcedureJson=new List<string>();
			for(int i=0;i<arrayProcedures.Length;i++) {
				string documents=string.Join(",",(arrayProcedures[i].documentTypes??new string[0])
					.Select(x => "{\"documentTypeCode\":\""+x+"\",\"attachmentReason\":\"Synthetic reason\"}"));
				listProcedureJson.Add("{\"procID\":\""+arrayProcedures[i].procId+"\",\"response\":"+(arrayProcedures[i].response ? "true" : "false")
					+",\"comments\":"+(arrayProcedures[i].comments==null ? "null" : "\""+arrayProcedures[i].comments+"\"")
					+",\"documents\":["+documents+"]}");
			}
			return "{\"payers\":[{\"id\":\"1\",\"claimLevelResponse\":"+(isClaimLevel ? "true" : "false")
				+",\"procedureResponses\":["+string.Join(",",listProcedureJson)+"]}]}";
		}
	}
}
