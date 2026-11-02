// The inventory windows built in memory and never shown: each one builds, and every control lies inside it. And the source rule that makes the
// Materials used panel reach every completion path: the program's windows change procedures only through Procedures.Insert and Procedures.Update.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDentalBusiness;

namespace InventoryTests {
	[TestClass]
	public class WindowTests {
		///<summary>WinForms controls are created on a single-threaded apartment thread, as the program does.</summary>
		private static void RunSta(Action action) {
			Exception exception=null;
			Thread thread=new Thread(() => {
				try {
					Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
					action();
				}
				catch(Exception ex) {
					exception=ex;
				}
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			thread.Join();
			if(exception!=null) {
				throw new AssertFailedException("Failed on the STA thread: "+exception,exception);
			}
		}

		[TestMethod]
		public void InventoryWindows_BuildWithEveryControlInside() {
			List<Func<Form>> listFuncs=new List<Func<Form>> {
				() => new FormMaterialsUsed(),() => new FormInventorySetup(),() => new FormSupplyAdjust(),() => new FormSupplyCount(),
				() => new FormSupplyProcCodes(),() => new FormSupplyProcPendings(),() => new FormSupplyReceive(),() => new FormSupplyStock(),
				() => new FormSupplyUsageReport(),() => new FormSupplyEdit(),
			};
			RunSta(() => {
				foreach(Func<Form> func in listFuncs) {
					using Form form=func();
					foreach(Control control in form.Controls) {
						if(control is MenuStrip) {
							continue;
						}
						Assert.IsTrue(form.ClientRectangle.Contains(control.Bounds),form.Name+"."+control.Name+" at "+control.Bounds+" is outside "+form.ClientSize);
					}
					List<Control> listButtons=form.Controls.Cast<Control>().Where(x => x is OpenPmsDental.UI.Button).ToList();
					for(int i=0;i<listButtons.Count;i++) {
						for(int j=i+1;j<listButtons.Count;j++) {
							Assert.IsFalse(listButtons[i].Bounds.IntersectsWith(listButtons[j].Bounds),form.Name+": "+listButtons[i].Name+" overlaps "+listButtons[j].Name);
						}
					}
				}
			});
		}

		private static T GetPrivate<T>(object obj,string fieldName) {
			return (T)obj.GetType().GetField(fieldName,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(obj);
		}

		private static void InvokePrivate(object obj,string methodName,params object[] arrayArgs) {
			obj.GetType().GetMethod(methodName,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(obj,arrayArgs);
		}

		///<summary>The Materials used panel built and loaded in memory, with a window handle but never shown. A keyboard-wedge scanner types into the
		///control that has focus, and an edit box in the Qty column would take its digits: no edit box opens on load or after a scan, rows are
		///selected in the Supply column, and the Scan box is first in the tab order. A scan adds one to the row of lot -1, the grid shows the lots
		///that would be taken now (split over lots), and OK deducts those.</summary>
		[TestMethod]
		public void MaterialsUsedPanel_ScansWithoutAnEditBox_AndDeductsTheLotsShown() {
			InvDb.Begin();
			string barcode="WEDGE"+InvData.Next();
			Supply supply=InvData.Supply(isLotTracked:true,barcode:barcode);
			int n=InvData.Next();
			SupplyLot supplyLotSoon=InvData.Lot(supply,"SOON"+n,DateTime.Today.AddMonths(1),1);
			SupplyLot supplyLotLate=InvData.Lot(supply,"LATE"+n,DateTime.Today.AddMonths(8),10);
			Procedure procedure=InvData.SetStatus(InvData.Proc(InvData.Patient(),InvData.Code((supply,1))),ProcStat.C);
			DialogResult dialogResult=DialogResult.None;
			RunSta(() => {
				using FormMaterialsUsed form=new FormMaterialsUsed();
				form.ListProcNums=new List<long> { procedure.ProcNum };
				Assert.AreNotEqual(IntPtr.Zero,form.Handle);
				InvokePrivate(form,"FormMaterialsUsed_Load",form,EventArgs.Empty);
				OpenPmsDental.UI.GridOD grid=GetPrivate<OpenPmsDental.UI.GridOD>(form,"gridMain");
				TextBox textScan=GetPrivate<TextBox>(form,"textScan");
				Assert.IsTrue(form.Controls.Cast<Control>().Where(x => !(x is Label) && x!=textScan).All(x => x.TabIndex>textScan.TabIndex),"Scan is first.");
				Assert.AreEqual(1,grid.ListGridRows.Count);
				Assert.AreEqual(0,grid.Controls.OfType<TextBoxBase>().Count(),"No edit box after loading.");
				Assert.AreEqual(1,grid.SelectedCell.X,"Selected in the Supply column.");
				Assert.AreEqual("SOON"+n,grid.ListGridRows[0].Cells[3].Text);
				textScan.Text=barcode;
				InvokePrivate(form,"textScan_KeyDown",textScan,new KeyEventArgs(Keys.Enter));
				Assert.AreEqual("",textScan.Text);
				Assert.AreEqual(1,grid.ListGridRows.Count,"Added to the row of lot -1.");
				Assert.AreEqual("2",grid.ListGridRows[0].Cells[2].Text);
				Assert.AreEqual("SOON"+n+" (1), LATE"+n+" (1)",grid.ListGridRows[0].Cells[3].Text,"Split over the lots, soonest-expiring first.");
				Assert.AreEqual(0,grid.Controls.OfType<TextBoxBase>().Count(),"No edit box after a scan.");
				Assert.AreEqual(1,grid.SelectedCell.X);
				InvokePrivate(form,"butOK_Click",form,EventArgs.Empty);
				dialogResult=form.DialogResult;
			});
			Assert.AreEqual(DialogResult.OK,dialogResult);
			Assert.AreEqual(0,InvData.LotQty(supplyLotSoon.SupplyLotNum));
			Assert.AreEqual(9,InvData.LotQty(supplyLotLate.SupplyLotNum));
			Assert.AreEqual(9,InvData.OnHand(supply.SupplyNum));
		}

		///<summary>A UPC scanned into a quantity cell instead of the Scan box is refused; a large quantity is asked first.</summary>
		[TestMethod]
		public void QuantityCells_RefuseAScannedBarcode_AndAskAboveTheLimit() {
			Assert.AreEqual(MaterialsUsedL.QtyCheck.Refused,MaterialsUsedL.CheckQty(36000291452,MaterialsUsedL.QTY_ASK_MATERIALS));
			Assert.AreEqual(MaterialsUsedL.QtyCheck.Refused,MaterialsUsedL.CheckQty(10000,MaterialsUsedL.QTY_ASK_STOCK));
			Assert.AreEqual(MaterialsUsedL.QtyCheck.Ask,MaterialsUsedL.CheckQty(100,MaterialsUsedL.QTY_ASK_MATERIALS));
			Assert.AreEqual(MaterialsUsedL.QtyCheck.Ok,MaterialsUsedL.CheckQty(100,MaterialsUsedL.QTY_ASK_STOCK));
			Assert.AreEqual(MaterialsUsedL.QtyCheck.Ok,MaterialsUsedL.CheckQty(2,MaterialsUsedL.QTY_ASK_MATERIALS));
			//Each window's CellLeave goes through the check.
			foreach(string fileName in new[] { "FormMaterialsUsed.cs","FormSupplyReceive.cs","FormSupplyCount.cs" }) {
				string source=File.ReadAllText(InvDb.FindRepoFile(Path.Combine("OpenPmsDental","Forms",fileName)));
				StringAssert.Contains(source,"MaterialsUsedL.IsQtyAccepted(this,qty,",fileName);
				Assert.IsFalse(Regex.IsMatch(source,@"SetSelected\(new System\.Drawing\.Point\([0-9]"),fileName+" selects rows in a column given by name, not an editable one.");
			}
		}

		///<summary>MaterialsUsedL relies on InventoryTracking.ProcSetComplete, which Procedures.Insert and Procedures.Update raise. A window that
		///wrote procedurelog itself would complete procedures without the panel.</summary>
		[TestMethod]
		public void ProgramWindows_ChangeProceduresOnlyThroughTheBusinessLayer() {
			string dirProgram=Path.GetDirectoryName(InvDb.FindRepoFile(Path.Combine("OpenPmsDental","OpenPmsDental.csproj")));
			Regex regex=new Regex(@"ProcedureCrud\.(Insert|Update|Sync)\(|UPDATE procedurelog SET[^""]*ProcStatus",RegexOptions.IgnoreCase);
			List<string> listProblems=new List<string>();
			foreach(string path in Directory.GetFiles(dirProgram,"*.cs",SearchOption.AllDirectories)) {
				if(path.Contains(Path.DirectorySeparatorChar+"obj"+Path.DirectorySeparatorChar) || path.Contains(Path.DirectorySeparatorChar+"bin"+Path.DirectorySeparatorChar)) {
					continue;
				}
				string text=File.ReadAllText(path);
				if(regex.IsMatch(text)) {
					listProblems.Add(path.Substring(dirProgram.Length));
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
			string sourceMain=File.ReadAllText(Path.Combine(dirProgram,"Main Modules","FormOpenDental.cs"));
			StringAssert.Contains(sourceMain,"MaterialsUsedL.Start(this);");
			string sourceProcedures=File.ReadAllText(InvDb.FindRepoFile(Path.Combine("OpenPmsDentalBusiness","Data Interface","Procedures.cs")));
			Assert.AreEqual(4,Regex.Matches(sourceProcedures,@"InventoryTracking\.NotifyProcSetComplete\(").Count,"Insert and Update raise it, on both sides of the Middle Tier check.");
		}

		///<summary>Business methods that complete procedures entirely on the Middle Tier server raise the event in the program after the remote call,
		///in their ClientMT branch: Procedures.SetCompleteInApptInList (Chart Set Complete of an appointment, the appointment edit window, per-visit
		///codes) and Appointments.CompleteClick (the Appointments module's Set Complete).</summary>
		[TestMethod]
		public void MiddleTierCompletions_RaiseTheEventInTheProgram() {
			string sourceProcedures=File.ReadAllText(InvDb.FindRepoFile(Path.Combine("OpenPmsDentalBusiness","Data Interface","Procedures.cs")));
			string sourceAppointments=File.ReadAllText(InvDb.FindRepoFile(Path.Combine("OpenPmsDentalBusiness","Data Interface","Appointments.cs")));
			string branchSetComplete=GetClientMtBranch(sourceProcedures,"public static List<Procedure> SetCompleteInApptInList(");
			StringAssert.Contains(branchSetComplete,"InventoryTracking.NotifyProcsNowComplete(listProcsForAppt,listProcsSetComplete);");
			string branchCompleteClick=GetClientMtBranch(sourceAppointments,"public static ODTuple<Appointment,List<Procedure>> CompleteClick(");
			StringAssert.Contains(branchCompleteClick,"InventoryTracking.NotifyProcsNowComplete(listProceduresForAppt,Procedures.GetProcsForSingle(");
		}

		///<summary>The text of the method's "if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {" block, up to its closing brace.</summary>
		private static string GetClientMtBranch(string source,string signature) {
			int indexMethod=source.IndexOf(signature);
			Assert.IsTrue(indexMethod>=0,signature);
			int indexBranch=source.IndexOf("if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {",indexMethod);
			int depth=0;
			for(int i=source.IndexOf('{',indexBranch);i<source.Length;i++) {
				if(source[i]=='{') {
					depth++;
				}
				else if(source[i]=='}') {
					depth--;
					if(depth==0) {
						return source.Substring(indexBranch,i-indexBranch+1);
					}
				}
			}
			Assert.Fail("No end of the ClientMT branch after "+signature);
			return "";
		}

		///<summary>Procedures.Delete puts back the materials after it writes the deleted status, as Update does, so a failure in between leaves a
		///deleted procedure holding materials, which the repair finds, never a complete procedure whose materials are back in stock.</summary>
		[TestMethod]
		public void ProcedureDelete_RestoresAfterTheStatusWrite() {
			string source=File.ReadAllText(InvDb.FindRepoFile(Path.Combine("OpenPmsDentalBusiness","Data Interface","Procedures.cs")));
			int indexDelete=source.IndexOf("public static void Delete(long procNum,bool forceDelete=false,bool hideGraphics=false) {");
			int indexNext=source.IndexOf("public static void TryDeleteLab(",indexDelete);
			string textDelete=source.Substring(indexDelete,indexNext-indexDelete);
			int indexStatus=textDelete.IndexOf("command=\"UPDATE procedurelog SET ProcStatus = \"+POut.Int((int)ProcStat.D)");
			int indexRestore=textDelete.IndexOf("InventoryTracking.OnProcNoLongerComplete(procNum,isDeleted:true);");
			Assert.IsTrue(indexStatus>0 && indexRestore>0);
			Assert.IsTrue(indexRestore>textDelete.IndexOf("Db.NonQ(command);",indexStatus),"The restore comes after the status is written.");
			Assert.AreEqual(1,Regex.Matches(textDelete,@"OnProcNoLongerComplete\(").Count);
		}
	}
}
