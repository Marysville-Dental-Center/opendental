// The Insurance History window (FormInsHistSetup) built in memory and never shown, with 0 to 13 rows from code groups marked Show In History: every
// row has its label, two date boxes and their Edit and Remove buttons, and Save sits below the last row, inside the window, whatever the row
// count. Before the fix Save kept a bottom anchor while the rows were added, so WinForms moved it a second time when the window was sized to fit
// the rows: with one to three rows it ended above the window and dates could not be saved. Synthetic data only (made-up Z codes).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace M2HISTTests {
	[TestClass]
	public class InsHistWindowLayoutTests {
		private const string GROUP_PREFIX="M2HIST layout ";

		[TestInitialize]
		public void TestInitialize() {
			M2HistDb.Begin();
			ProcedureCodeT.CreateProcCode("Z1110");
			ProcedureCodes.RefreshCache();
		}

		[TestCleanup]
		public void TestCleanup() {
			if(M2HistDb.SkipReason!=null) {
				return;
			}
			DataCore.NonQ("DELETE FROM codegroup WHERE GroupName LIKE '"+GROUP_PREFIX+"%'");
			CodeGroups.RefreshCache();
		}

		///<summary>Only this test's history rows: countGroups groups marked Show In History, the last one with four quadrant rows when withQuadrants.
		///</summary>
		private static void SetUpRows(int countGroups,bool withQuadrants=false) {
			DataCore.NonQ("UPDATE codegroup SET ShowInHistory=0,HistProcCode='',IsPerioFourQuads=0");
			DataCore.NonQ("DELETE FROM codegroup WHERE GroupName LIKE '"+GROUP_PREFIX+"%'");
			for(int i=0;i<countGroups;i++) {
				CodeGroups.Insert(new CodeGroup { GroupName=GROUP_PREFIX+i,ProcCodes="Z1110",ItemOrder=900+i,ShowInHistory=true,HistProcCode="Z1110",
					IsPerioFourQuads=withQuadrants && i==countGroups-1 });
			}
			CodeGroups.RefreshCache();
		}

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

		private static T GetField<T>(object form,string name) where T:class {
			FieldInfo fieldInfo=form.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
			Assert.IsNotNull(fieldInfo,"No field "+name);
			T value=fieldInfo.GetValue(form) as T;
			Assert.IsNotNull(value,"Field "+name+" is not a "+typeof(T).Name);
			return value;
		}

		///<summary>The window as the Family module and Edit Insurance Plan open it, after the layout manager, never shown.</summary>
		private static void CheckLayout(int countRowsExpected,string message) {
			Assert.AreEqual(countRowsExpected,InsHistCategories.GetAll().Count,message);
			RunSta(() => {
				using FormInsHistSetup formInsHistSetup=new FormInsHistSetup(0,new InsSub());
				Control butSave=GetField<Control>(formInsHistSetup,"butSave");
				Control labelHeading=GetField<Control>(formInsHistSetup,"labelHeading");
				List<Control> listControls=formInsHistSetup.Controls.Cast<Control>().ToList();
				List<Control> listRowControls=listControls.FindAll(x => x.Name.StartsWith("labelInsHist") || x.Name.StartsWith("textDate")
					|| x.Name.StartsWith("butEdit") || x.Name.StartsWith("butRemove") || x.Name=="labelNoCategories");
				Assert.AreEqual(countRowsExpected,listControls.Count(x => x.Name.StartsWith("labelInsHist")),message);
				Assert.AreEqual(countRowsExpected*InsHistDates.DATES_PER_CATEGORY,listControls.Count(x => x.Name.StartsWith("textDate")),message);
				Assert.AreEqual(countRowsExpected*InsHistDates.DATES_PER_CATEGORY,listControls.Count(x => x.Name.StartsWith("butEdit")),message);
				Assert.AreEqual(countRowsExpected*InsHistDates.DATES_PER_CATEGORY,listControls.Count(x => x.Name.StartsWith("butRemove")),message);
				Assert.AreEqual(countRowsExpected==0 ? 1 : 0,listControls.Count(x => x.Name=="labelNoCategories"),message+": the note without rows.");
				int bottomContent=Math.Max(labelHeading.Bottom,listRowControls.Count==0 ? 0 : listRowControls.Max(x => x.Bottom));
				string where=message+": Save at "+butSave.Bounds+", content to "+bottomContent+", client "+formInsHistSetup.ClientSize;
				Assert.IsTrue(butSave.Top>=bottomContent,"Save is below the last row. "+where);
				Assert.IsTrue(formInsHistSetup.ClientRectangle.Contains(butSave.Bounds),"Save is inside the window. "+where);
				Assert.IsTrue(formInsHistSetup.ClientSize.Height-butSave.Bottom<=butSave.Height,"Save is at the bottom, not floating above it. "+where);
				Assert.IsTrue(butSave.Enabled,where);//Visible is false for every control of a window that is never shown.
			});
		}

		[TestMethod]
		public void SaveButton_BelowTheLastRowInsideTheWindow_ForAnyNumberOfRows() {
			foreach(int countGroups in new[] { 0,1,2,3,4,7,10,13 }) {
				SetUpRows(countGroups);
				CheckLayout(countGroups,countGroups+" row(s)");
			}
			//A Perio Four Quadrants group: four rows.
			SetUpRows(2,withQuadrants:true);
			CheckLayout(5,"one group and a four-quadrant group");
		}
	}
}
