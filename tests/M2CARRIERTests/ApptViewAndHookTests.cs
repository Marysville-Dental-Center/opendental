// The PreAuthFlag appointment view element, and source checks that the shared files only gained the one-line hooks that call this package's
// own files (the UI itself is not run in tests: OpenPmsDental.exe is never launched). No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2CARRIERTests {
	[TestClass]
	public class ApptViewAndHookTests {
		[TestMethod]
		public void PreAuthFlag_IsTheLastElementAndKeepsEarlierNumbers() {
			Array arrayValues=Enum.GetValues(typeof(EnumApptViewElement));
			Assert.AreEqual(EnumApptViewElement.PreAuthFlag,arrayValues.GetValue(arrayValues.Length-1));
			Assert.AreEqual(46,(int)EnumApptViewElement.PreAuthFlag);
			Assert.AreEqual(45,(int)EnumApptViewElement.WkPhone,"Members before it keep their numbers.");
			Assert.AreEqual(0,(int)EnumApptViewElement.None);
		}

		[TestMethod]
		public void PreAuthFlag_IsStoredByName() {
			Assert.AreEqual("PreAuthFlag",EnumApptViewElement.PreAuthFlag.ToString());
			ApptViewItem apptViewItem=new ApptViewItem("PreAuthFlag",0,System.Drawing.Color.Red);
			Assert.AreEqual(EnumApptViewElement.PreAuthFlag,apptViewItem.GetElement());
			//The appointment bubble uses M2-02's display field with the same name.
			Assert.AreEqual("PreAuthFlag",DisplayFields26_1.ApptBubblePreAuthFlag);
			Assert.IsTrue(DisplayFields.GetAllAvailableList(DisplayFieldCategory.AppointmentBubble).Any(x => x.InternalName==DisplayFields26_1.ApptBubblePreAuthFlag));
		}

		[TestMethod]
		public void AppointmentTableColumns_AreNamed() {
			Assert.AreEqual("preAuthFlag",Appointments.PreAuthFlagColumn);
			Assert.AreEqual("preAuthCodes",Appointments.PreAuthCodesColumn);
			Assert.AreEqual("preAuthColor",Appointments.PreAuthColorColumn);
		}

		[TestMethod]
		public void CarrierColor_BlackOrEmptyMeansNone() {
			Assert.IsFalse(Appointments.IsCarrierColorSet(System.Drawing.Color.Black));
			Assert.IsFalse(Appointments.IsCarrierColorSet(System.Drawing.Color.FromArgb(255,0,0,0)));
			Assert.IsFalse(Appointments.IsCarrierColorSet(System.Drawing.Color.Empty));
			Assert.IsFalse(Appointments.IsCarrierColorSet(System.Drawing.Color.FromArgb(0,255,0,0)));
			Assert.IsTrue(Appointments.IsCarrierColorSet(System.Drawing.Color.FromArgb(255,200,230,255)));
		}

		#region Hooks in shared files
		private static string Source(params string[] arrayParts) {
			return ScratchDb.ReadRepoFile(Path.Combine(arrayParts));
		}

		private static int Count(string text,string value) {
			return Regex.Matches(text,Regex.Escape(value)).Count;
		}

		[TestMethod]
		public void Hooks_ChartQueuesTheReminderAfterAddProcedureAndAddQuick() {
			string source=Source("OpenPmsDental","Main Modules","ControlChart.cs");
			Assert.AreEqual(2,Count(source,"PreAuthReminder_Queue(procedure);"));
			//Both calls come right after logComplCreate, at the end of the two methods that add a procedure.
			Assert.AreEqual(2,Regex.Matches(source,@"logComplCreate\(procedure\);\s*PreAuthReminder_Queue\(procedure\);").Count);
			StringAssert.Contains(Source("OpenPmsDental","Main Modules","ControlChart.PreAuth.cs"),"private void PreAuthReminder_Queue(Procedure procedure)");
		}

		[TestMethod]
		public void Hooks_TreatmentPlanMarksRowsBeforeEndUpdate() {
			string source=Source("OpenPmsDental","Main Modules","ControlTreat.cs");
			Assert.AreEqual(1,Count(source,"FillMainDisplayPreAuthFlags();"));
			Assert.IsTrue(Regex.IsMatch(source,@"FillMainDisplayPreAuthFlags\(\);[^\r\n]*\r?\n\s*gridMain\.EndUpdate\(\);"));
		}

		[TestMethod]
		public void Hooks_AppointmentPanelDrawsTheElementAndTheBubbleLine() {
			string source=Source("OpenPmsDental","UI","Appt","ControlApptPanel.cs");
			Assert.AreEqual(1,Count(source,"case EnumApptViewElement.PreAuthFlag:"));
			Assert.AreEqual(1,Count(source,"return DrawElementPreAuthFlag("));
			Assert.AreEqual(1,Count(source,"case DisplayFields26_1.ApptBubblePreAuthFlag:"));
			Assert.AreEqual(1,Count(source,"y=DrawBubblePreAuthFlag("));
		}

		[TestMethod]
		public void Hooks_BusinessLayer() {
			Assert.AreEqual(1,Count(Source("OpenPmsDentalBusiness","Data Interface","Appointments.cs"),"FillPreAuthFlags(table,isPlanned);"));
			Assert.AreEqual(1,Count(Source("OpenPmsDentalBusiness","Data Interface","Carriers.cs"),"CarrierPreAuths.CombineCodes(pickedCarrierNum,listCarrierNumsToCombine"));
			Assert.AreEqual(2,Count(Source("OpenPmsDentalBusiness","Data Interface","InsEditLogs.cs"),"InsEditLogBigValues.MoveLongValues("));
			Assert.AreEqual(1,Count(Source("OpenPmsDentalBusiness","Data Interface","InsEditPatLogs.cs"),"InsEditLogBigValues.MoveLongValues("));
		}

		[TestMethod]
		public void Hooks_LogWindowsShowWhicheverColumnIsFilled() {
			foreach(string fileName in new[] { "FormInsEditLogs.cs","FormInsEditPatLog.cs" }) {
				string source=Source("OpenPmsDental","Forms",fileName);
				Assert.AreEqual(1,Count(source,"InsEditLogBigValues.GetOldValue("),fileName);
				Assert.AreEqual(1,Count(source,"InsEditLogBigValues.GetNewValue("),fileName);
				Assert.IsFalse(Regex.IsMatch(source,@"row\.Cells\.Add\(_listInsEdit\w*\[i\]\.(Old|New)Value\)"),fileName+" still shows only the short column.");
			}
		}

		[TestMethod]
		public void Hooks_EditCarrierReadsChecksAndLogsTheList() {
			string source=Source("OpenPmsDental","Forms","FormCarrierEdit.cs");
			StringAssert.Contains(source,"textPreAuthCodes.Text=CarrierPreAuths.ToDisplay(CarrierCur.PreAuthCodes);");
			StringAssert.Contains(source,"if(!TryGetPreAuthCodes(out string preAuthCodes)) {");
			StringAssert.Contains(source,"CarrierCur.PreAuthCodes=preAuthCodes;");
			StringAssert.Contains(source,"textPreAuthCodes.ReadOnly=true;");
			StringAssert.Contains(source,"\"preauth codes\"");
			string sourceDesigner=Source("OpenPmsDental","Forms","FormCarrierEdit.designer.cs");
			StringAssert.Contains(sourceDesigner,"this.Controls.Add(this.textPreAuthCodes);");
			StringAssert.Contains(sourceDesigner,"this.butPreAuthCodes.Click += new System.EventHandler(this.butPreAuthCodes_Click);");
			string sourcePartial=Source("OpenPmsDental","Forms","FormCarrierEdit.PreAuth.cs");
			StringAssert.Contains(sourcePartial,"CarrierPreAuths.Check(textPreAuthCodes.Text)");
			StringAssert.Contains(sourcePartial,"formProcCodes.CanAllowMultipleSelections=true;");
		}

		[TestMethod]
		public void Project_CompilesTheNewFiles() {
			string csprojOd=Source("OpenPmsDental","OpenPmsDental.csproj");
			foreach(string file in new[] { @"Main Modules\ControlChart.PreAuth.cs",@"Main Modules\ControlTreat.PreAuth.cs",@"UI\Appt\ControlApptPanel.PreAuth.cs",
				@"Forms\FormCarrierEdit.PreAuth.cs",@"Forms\FormPreAuthReminder.cs",@"Forms\FormPreAuthReminder.Designer.cs",@"Logic\PreAuthL.cs" })
			{
				StringAssert.Contains(csprojOd,"<Compile Include=\""+file+"\"",file);
			}
			string csprojOdb=Source("OpenPmsDentalBusiness","OpenPmsDentalBusiness.csproj");
			foreach(string file in new[] { @"Data Interface\CarrierPreAuths.cs",@"Data Interface\InsEditLogBigValues.cs",@"Data Interface\Appointments.PreAuth.cs" }) {
				StringAssert.Contains(csprojOdb,"<Compile Include=\""+file+"\"",file);
			}
		}
		#endregion Hooks in shared files
	}
}
