// Group C of the checklist: forms whose initializer order changed, each opened through the handler its click runs, in one in-process session
// (a new session after a check that hung).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WindowChecksTests {
	[TestClass]
	public class CTests {
		public static readonly string[][] ArrayChecks=new[] {
			new[] { "C1","Edit Patient Information (FormPatientEdit)" },
			new[] { "C2","Edit Procedure with signature box (FormProcEdit)" },
			new[] { "C3","Payment (FormPayment)" },
			new[] { "C4","Sheet fill (FormSheetFillEdit)" },
			new[] { "C5","Insurance benefits (FormInsBenefits)" },
			new[] { "C6","Query result window (FormQuery)" },
			new[] { "C7","Edit Deposit (FormDepositEdit)" },
			new[] { "C8","Time card (FormTimeCard)" },
			new[] { "C9","Edit Appointment (FormApptEdit)" },
			new[] { "C10","Database Maintenance list" },
			new[] { "C11","Claim forms" },
			new[] { "C12","Internal sheets and eForms" },
			new[] { "C13","CC Recurring Charges" },
			new[] { "C14","Billing options (statements)" },
			new[] { "C15","WebView2 window (Wiki)" },
		};

		///<summary>Runs the in-process checks in one session, starting a new session after a check that did not end; records every result and
		///fails the test when one failed.</summary>
		public static void RunInProcess(string[][] arrayChecks,string module="",Action<CheckResult> actionAfterEach=null) {
			List<string> listFailed=new List<string>();
			InProcHost inProcHost=InProcHost.Start(module);
			try {
				foreach(string[] arrayCheck in arrayChecks) {
					CheckResult checkResult=inProcHost.Run(arrayCheck[0],arrayCheck[1]);
					actionAfterEach?.Invoke(checkResult);
					Results.Write(checkResult);
					Console.WriteLine(checkResult.Id+" "+checkResult.Status+": "+checkResult.Summary);
					if(checkResult.Status==CheckStatus.Failed) {
						listFailed.Add(checkResult.Id+": "+checkResult.Summary);
					}
					if(checkResult.ListFailures.Exists(x => x.Contains("did not end within") || x.Contains("did not answer"))) {
						inProcHost.Dispose();
						inProcHost=InProcHost.Start(module);
					}
				}
			}
			finally {
				inProcHost.Dispose();
			}
			Assert.AreEqual(0,listFailed.Count,string.Join(" || ",listFailed));
		}

		[TestMethod]
		public void C1toC15_FormsWhoseInitializerOrderChanged() {
			WinChkFixture.Begin("C");
			RunInProcess(ArrayChecks,"chart",checkResult => {
				if(checkResult.Id=="C15") {
					checkResult.Note("Exit after the Wiki: the in-process session does not exit the program; A4 times every out-of-process exit");
					checkResult.ForPerson("the exit time after a session that opened the Wiki (stage 0 finding 2) needs File > Exit in a real run");
				}
			});
		}
	}
}
