// The fork-only enum values of routine maintenance (docs/port-notes/FORK-ENUMS.md) and where the feature is wired into the program.
// Source checks read the repository's files; the windows themselves are WinForms and not run here.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using System.Text.RegularExpressions;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace MaintenanceTests {
	[TestClass]
	public class EnumAndWiringTests {
		[TestInitialize]
		public void TestInitialize() {
			TestDb.SetCultureUs();
		}

		[TestMethod]
		public void Permissions_AreForkOnlyValuesInTheMaintenanceRange() {
			Assert.AreEqual(10000,(int)EnumPermType.MaintenanceSetup);
			Assert.AreEqual(10001,(int)EnumPermType.MaintenanceRecord);
			foreach(EnumPermType permType in new[] { EnumPermType.MaintenanceSetup,EnumPermType.MaintenanceRecord }) {
				Assert.IsTrue(ForkEnums.IsForkOnly(permType));
				Assert.IsTrue((int)permType>=10000 && (int)permType<=10049,"Routine maintenance owns 10000 to 10049.");
				Assert.AreNotEqual(permType.ToString(),permType.GetDescription(),permType+" needs a [Description].");
				Assert.IsFalse(GroupPermissions.IsReserved(permType));
				Assert.IsTrue(GroupPermissions.HasAuditTrail(permType),permType+" writes audit entries, so it belongs in the audit trail filter.");
				Assert.IsTrue((int)permType<=short.MaxValue,"grouppermission.PermType and securitylog.PermType are smallint(6).");
			}
			Assert.AreEqual("Maintenance Setup",EnumPermType.MaintenanceSetup.GetDescription());
			Assert.AreEqual("Maintenance Record Completion",EnumPermType.MaintenanceRecord.GetDescription());
		}

		[TestMethod]
		public void AlertAndFormTypes_CountDownFrom127BecauseTheirColumnsAreTinyint() {
			Assert.AreEqual(127,(int)AlertType.MaintenanceDueSoon);
			Assert.AreEqual(126,(int)AlertType.MaintenanceOverdue);
			Assert.AreEqual(127,(int)FormType.FormMaintenanceBoard);
			foreach(Enum value in new Enum[] { AlertType.MaintenanceDueSoon,AlertType.MaintenanceOverdue,FormType.FormMaintenanceBoard }) {
				int number=Convert.ToInt32(value);
				Assert.IsTrue(number>=120 && number<=127,value+": routine maintenance owns 127 down to 120 of the tinyint enums.");
				Assert.IsTrue(ForkEnums.IsForkOnly(value));
				Assert.AreNotEqual(value.ToString(),value.GetDescription(),value+" needs a [Description].");
			}
			Assert.IsFalse(GenericTools.IsODHQ(AlertType.MaintenanceDueSoon),"Alert category setup lists it.");
			Assert.IsFalse(GenericTools.IsODHQ(AlertType.MaintenanceOverdue));
			//Open Dental's own values stay below the fork-only region.
			Assert.IsTrue(Enum.GetValues(typeof(AlertType)).Cast<int>().Where(x => x<ForkEnums.FIRST_VALUE_TINYINT).Max()<ForkEnums.FIRST_VALUE_TINYINT);
			Assert.AreEqual(ForkEnums.FIRST_VALUE_TINYINT,ForkEnums.GetFirstValue(typeof(AlertType)));
			Assert.AreEqual(ForkEnums.FIRST_VALUE_TINYINT,ForkEnums.GetFirstValue(typeof(FormType)));
			Assert.AreEqual(ForkEnums.FIRST_VALUE,ForkEnums.GetFirstValue(typeof(EnumPermType)));
			Assert.IsFalse(ForkEnums.IsForkOnly(AlertType.EServiceListenerError));
			Assert.IsFalse(ForkEnums.IsForkOnly(EnumPermType.LogScraperHQ));
		}

		[TestMethod]
		public void AlertTypes_FitTheirColumns() {
			//alertitem.Type, alertcategorylink.AlertType and alertsub.Type are tinyint(4); alertitem.FormToOpen too.
			OpenPmsDentalBusiness.ForkMigrations.SchemaSnapshot reference=OpenPmsDentalBusiness.ForkMigrations.SchemaVerifier.GetReference();
			Assert.AreEqual("tinyint(4) NOT NULL",reference.GetTable("alertitem").GetColumn("Type").Definition);
			Assert.AreEqual("tinyint(4) NOT NULL",reference.GetTable("alertitem").GetColumn("FormToOpen").Definition);
			Assert.AreEqual("tinyint(4) NOT NULL",reference.GetTable("alertcategorylink").GetColumn("AlertType").Definition);
			Assert.IsTrue(Enum.GetValues(typeof(AlertType)).Cast<int>().All(x => x>=sbyte.MinValue && x<=sbyte.MaxValue));
			Assert.IsTrue(Enum.GetValues(typeof(FormType)).Cast<int>().All(x => x>=sbyte.MinValue && x<=sbyte.MaxValue));
			Assert.AreEqual("smallint(6) NOT NULL",reference.GetTable("grouppermission").GetColumn("PermType").Definition);
		}

		#region Source checks
		private static string Source(params string[] arrayParts) {
			return TestDb.ReadRepoFile(arrayParts);
		}

		private static string Between(string source,string start,string end) {
			int indexStart=source.IndexOf(start);
			Assert.IsTrue(indexStart>=0,start+" not found.");
			int indexEnd=source.IndexOf(end,indexStart+start.Length);
			Assert.IsTrue(indexEnd>indexStart,end+" not found after "+start);
			return source.Substring(indexStart,indexEnd-indexStart);
		}

		[TestMethod]
		public void SecurityTree_HasBothPermissions_AndSetAllSetNoneLoopOverEveryValue() {
			string source=Source("OpenPmsDental","User Controls","UserControlSecurityTree.cs");
			string fill=Between(source,"public void FillTreePermissionsInitial()","public void FillTreePerm()");
			Assert.AreEqual(1,Regex.Matches(fill,@"SetNode\(EnumPermType\.MaintenanceSetup\)").Count);
			Assert.AreEqual(1,Regex.Matches(fill,@"SetNode\(EnumPermType\.MaintenanceRecord\)").Count);
			StringAssert.Contains(Between(fill,"#region Manage Module","#endregion"),"Routine Maintenance");
			foreach(string method in new[] { "public void SetAll()","public void SetNone()" }) {
				string body=Between(source,method,method=="public void SetAll()" ? "public void SetNone()" : "///<summary>Sets the current usergroup");
				Assert.IsFalse(body.Contains("(EnumPermType)i"),method+" must not cast a loop index: fork-only values start at 10000.");
				StringAssert.Contains(body,"Enum.GetValues(typeof(EnumPermType))");
			}
		}

		[TestMethod]
		public void CentralManager_AlertListUsesEveryValue() {
			string source=Source("CentralManager","FormCentralUserEdit.cs");
			Assert.IsFalse(source.Contains("(AlertType)i"),"Fork-only alert types are 126 and 127, not the next index.");
			Assert.IsFalse(source.Contains("(AlertType)index"));
		}

		[TestMethod]
		public void ManageModule_HasTheMaintenanceButton() {
			string designer=Source("OpenPmsDental","Main Modules","ControlManage.designer.cs");
			StringAssert.Contains(designer,"this.groupBox3.Controls.Add(this.butMaintenance);");
			StringAssert.Contains(designer,"this.butMaintenance.Text = \"Maintenance\";");
			string source=Source("OpenPmsDental","Main Modules","ControlManage.cs");
			StringAssert.Contains(Between(source,"private void butMaintenance_Click","}"),"new FormMaintBoard()");
		}

		[TestMethod]
		public void MainWindow_OpensTheBoardFromAnAlertAndRunsTheReminders() {
			string source=Source("OpenPmsDental","Main Modules","FormOpenDental.cs");
			StringAssert.Contains(Between(source,"case FormType.FormMaintenanceBoard:","break;"),"new FormMaintBoard()");
			string threads=Source("OpenPmsDental","Main Modules","FormOpenDentalThreads.cs");
			StringAssert.Contains(Between(threads,"private void SetThreads(bool doStart)","Logger.LogToPath(\"Started threads\""),"BeginMaintenanceRemindersThread()");
			StringAssert.Contains(Between(threads,"private void BeginMaintenanceRemindersThread()","#endregion"),"MaintAlerts.Sync(MaintAlerts.GetToday())");
		}

		[TestMethod]
		public void NewItemWindow_DefaultsTheFirstDueDateToToday_AndOffersYearsAfterTheLastCompletion() {
			string board=Source("OpenPmsDental","Forms","FormMaintBoard.cs");
			StringAssert.Contains(Between(board,"private void butAdd_Click","using FormMaintItemEdit"),"MaintItems.CreateNew(MaintAlerts.GetToday())");
			string edit=Source("OpenPmsDental","Forms","FormMaintItemEdit.cs");
			string load=Between(edit,"private void FormMaintItemEdit_Load","#region Combos");
			StringAssert.Contains(load,"if(_isNew && _maintItem.DateAnchor.Year<1880) {");
			StringAssert.Contains(load,"_maintItem.DateAnchor=_dateToday;");
			Assert.IsTrue(load.IndexOf("_maintItem.DateAnchor=_dateToday;")<load.IndexOf("textAnchor.Text="),"Today is filled in before the anchor is shown.");
			string fillUnit=Between(edit,"private void FillComboUnit(","private void SetUnassignedLabel()");
			StringAssert.Contains(fillUnit,"EnumMaintIntervalUnit.Years");
			Assert.IsFalse(fillUnit.Contains("AfterCompletion"),"Years are offered for every schedule that has a unit.");
			Assert.IsFalse(edit.Contains("no reminder alert will be sent"),"An unassigned item reminds the Maintenance Setup users now.");
			StringAssert.Contains(Source("OpenPmsDental","Forms","FormMaintItemEdit.Designer.cs"),"the reminders go to every user with Maintenance Setup");
		}

		[TestMethod]
		public void HistoryWindow_ShowsTheCorrection_AndCorrectsThroughTheBusinessLayer() {
			string history=Source("OpenPmsDental","Forms","FormMaintHistory.cs");
			string fill=Between(history,"private void FillGrid()","private void FillGridValues()");
			//The columns, Corrected among them, are MaintHistoryGrid's (HistoryGridTests lays them out at the window's size).
			StringAssert.Contains(fill,"MaintHistoryGrid.GetColumns(gridMain.TranslationName,isAllItems:_maintItemNum==0)");
			Assert.IsFalse(fill.Contains("new GridColumn("),"No column is added beside MaintHistoryGrid's.");
			StringAssert.Contains(fill,"MaintHistoryGrid.GetRecordedText(maintRecord.SecDateTEntry)");
			StringAssert.Contains(fill,"MaintCorrections.GetCorrectedText(maintRecord,");
			string correct=Between(history,"private void butCorrect_Click","private void butExport_Click");
			StringAssert.Contains(correct,"MaintRecords.CanCorrect(Security.CurUser,maintRecord)");
			StringAssert.Contains(correct,"new FormMaintRecordEdit(maintItem,maintRecord)");
			StringAssert.Contains(Source("OpenPmsDental","Forms","FormMaintHistory.Designer.cs"),
				"this.butCorrect.Click += new System.EventHandler(this.butCorrect_Click);");
			string record=Source("OpenPmsDental","Forms","FormMaintRecordEdit.cs");
			StringAssert.Contains(Between(record,"private void SaveCorrection()","private bool CopyAttachments()"),"MaintRecords.Correct(");
			Assert.IsFalse(Regex.IsMatch(history+record,@"MaintRecords\.Delete|MaintRecordCrud\.Delete|MaintRecordValueCrud\.Delete"),"A completion is never deleted.");
		}

		[TestMethod]
		public void ForkEnumsDocument_HasTheTinyintRule() {
			string text=Source("docs","port-notes","FORK-ENUMS.md");
			StringAssert.Contains(text,"tinyint");
			StringAssert.Contains(text,"127-120");
			StringAssert.Contains(text,"10000-10049");
		}
		#endregion Source checks
	}
}
