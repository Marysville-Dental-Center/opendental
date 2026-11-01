// The real Edit Insurance Plan window on a scratch database: built and loaded the way ShowDialog loads it (FormODBase lays it out, then the
// Load event fills it), but never shown. Checks what only the running code decides: which panels the visibility rules hide, the space the
// hidden Canadian fields leave to the benefits grid, the write-off wording and help text, where the employer and carrier drop-downs open, and
// the audit entries of a dropped plan (v26_1-tools-audit-insplan-drop). Synthetic rows only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDentalBusiness;

namespace M2INSPLANTests {
	[TestClass]
	public class FormInsPlanWindowTests {
		///<summary>Control.STATE_VISIBLE. A control's own visible flag: Control.Visible also needs every parent shown, and this window never is.</summary>
		private const int STATE_VISIBLE=0x2;
		private static readonly MethodInfo _methodGetState=typeof(Control).GetMethod("GetState",BindingFlags.Instance|BindingFlags.NonPublic,null,
			new[] { typeof(int) },null);
		private static int _countFixtures;
		///<summary>PrintWindow flag: render the whole window, child windows included, even where it is off screen.</summary>
		private const uint PW_RENDERFULLCONTENT=2;

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		private static extern bool PrintWindow(IntPtr hwnd,IntPtr hdc,uint flags);

		[TestInitialize]
		public void TestInitialize() {
			InsPlanDb.Begin();
		}

		#region Helpers
		private class Fixture {
			public Patient Patient;
			public Carrier Carrier;
			public InsPlan InsPlan;
			public InsSub InsSub;
			public PatPlan PatPlan;
		}

		///<summary>A patient with one plan of a new carrier, as the subscriber.</summary>
		private static Fixture InsertFixture(string carrierName) {
			_countFixtures++;
			Fixture fixture=new Fixture();
			Patient patient=new Patient();
			patient.FName="Plan"+_countFixtures;
			patient.LName="Synthetic";
			patient.PatStatus=PatientStatus.Patient;
			patient.Birthdate=new DateTime(1980,1,1);
			patient.Address="";
			patient.City="";
			patient.State="";
			patient.Zip="";
			patient.WirelessPhone="";
			Patients.Insert(patient,false);
			Patient patientOld=patient.Copy();
			patient.Guarantor=patient.PatNum;
			Patients.Update(patient,patientOld);
			fixture.Patient=patient;
			Carrier carrier=new Carrier() { CarrierName=carrierName,Address="",Address2="",City="",State="",Zip="",Phone="",ElectID="",CDAnetVersion="",TIN="" };
			Carriers.Insert(carrier);
			Carriers.RefreshCache();
			fixture.Carrier=carrier;
			InsPlan insPlan=new InsPlan() { CarrierNum=carrier.CarrierNum,GroupName="Synthetic Group",GroupNum="SG"+_countFixtures,PlanNote="",PlanType="",
				TrojanID="",DivisionNo="",CanadianPlanFlag="",CanadianDiagnosticCode="",CanadianInstitutionCode="",RxBIN="",SopCode="",
				ExclusionFeeRule=ExclusionRule.UseUcrFee };
			InsPlans.Insert(insPlan);
			fixture.InsPlan=insPlan;
			InsSub insSub=new InsSub() { PlanNum=insPlan.PlanNum,Subscriber=patient.PatNum,SubscriberID="SUB"+_countFixtures,BenefitNotes="",SubscNote="",
				ReleaseInfo=true,AssignBen=true };
			InsSubs.Insert(insSub);
			fixture.InsSub=insSub;
			PatPlan patPlan=new PatPlan() { PatNum=patient.PatNum,InsSubNum=insSub.InsSubNum,PatID="",Relationship=Relat.Self,Ordinal=1 };
			PatPlans.Insert(patPlan);
			fixture.PatPlan=patPlan;
			return fixture;
		}

		///<summary>Runs the action on an STA thread with the given culture, as the window runs in the program. Rethrows what it throws.</summary>
		private static void RunSta(string cultureName,Action action) {
			Exception exception=null;
			Thread thread=new Thread(() => {
				try {
					Thread.CurrentThread.CurrentCulture=new CultureInfo(cultureName);
					action();
				}
				catch(Exception ex) {
					exception=ex;
				}
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			thread.Join();
			if(exception is UnitTestAssertException) {
				throw exception;
			}
			if(exception!=null) {
				throw new Exception("The window failed: "+exception.GetBaseException().Message,exception);
			}
		}

		///<summary>Builds the window and loads it the way ShowDialog does: FormODBase.OnLoad lays the form out, then raises Load. Not shown.</summary>
		private static FormInsPlan OpenWindow(Fixture fixture) {
			FormInsPlan formInsPlan=new FormInsPlan(fixture.InsPlan,fixture.PatPlan,fixture.InsSub);
			typeof(Form).GetMethod("OnLoad",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(formInsPlan,new object[] { EventArgs.Empty });
			return formInsPlan;
		}

		private static T GetField<T>(FormInsPlan formInsPlan,string name) {
			FieldInfo fieldInfo=typeof(FormInsPlan).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);
			Assert.IsNotNull(fieldInfo,"No field "+name);
			return (T)fieldInfo.GetValue(formInsPlan);
		}

		private static Control GetControl(FormInsPlan formInsPlan,string name) {
			return GetField<Control>(formInsPlan,name);
		}

		private static bool IsSetVisible(Control control) {
			return (bool)_methodGetState.Invoke(control,new object[] { STATE_VISIBLE });
		}

		///<summary>The control's containers up to the form (not included); fails when the chain does not reach it.</summary>
		private static List<Control> GetAncestors(FormInsPlan formInsPlan,Control control) {
			List<Control> listAncestors=new List<Control>();
			for(Control controlParent=control.Parent;controlParent!=formInsPlan;controlParent=controlParent.Parent) {
				Assert.IsNotNull(controlParent,control.Name+" is not on the window.");
				listAncestors.Add(controlParent);
			}
			return listAncestors;
		}

		private static DataTable GetSecurityLogs(EnumPermType enumPermType,long planNum) {
			return DataCore.GetTable("SELECT LogText FROM securitylog WHERE PermType="+POut.Int((int)enumPermType)+" AND FKey="+POut.Long(planNum));
		}
		#endregion Helpers

		[TestMethod]
		public void UsOffice_EveryOldControlIsOnTheWindow_OnlyCanadianAndOrthoHidden() {
			Prefs.UpdateBool(PrefName.OrthoEnabled,false);
			Fixture fixture=InsertFixture("Window Dental A");
			RunSta("en-US",() => {
				using FormInsPlan formInsPlan=OpenWindow(fixture);
				List<string> listProblems=new List<string>();
				foreach(string[] arrayOld in LayoutDesignerTests.GetOldControls().Where(x => x[2]=="keep" && x[0]!="toolTip1")) {
					Control control=GetControl(formInsPlan,arrayOld[0]);
					if(control==null) {
						listProblems.Add(arrayOld[0]+" is null");
						continue;
					}
					List<Control> listAncestors=GetAncestors(formInsPlan,control);
					if(listAncestors.Any(x => x is OpenPmsDental.UI.TabPage || x is OpenPmsDental.UI.TabControl || x is System.Windows.Forms.TabPage)) {
						listProblems.Add(arrayOld[0]+" is on a tab page");
					}
					if(listAncestors.OfType<ScrollableControl>().Any(x => x.AutoScroll)) {
						listProblems.Add(arrayOld[0]+" is in a scrolling panel");
					}
					//Only the panels the 24.3 rules hide in this office may hide a control: Canadian fields outside Canada, ortho when it is off, and
					//Import Benefits when Trojan and IAP are off (all its controls are hidden then; ground rule 7).
					foreach(Control controlHidden in listAncestors.Where(x => !IsSetVisible(x))) {
						if(controlHidden.Name!="groupCanadian" && controlHidden.Name!="groupOrtho" && controlHidden.Name!="groupBox1") {
							listProblems.Add(arrayOld[0]+" is hidden by "+controlHidden.Name);
						}
					}
				}
				Assert.AreEqual("",string.Join("; ",listProblems));
				Assert.IsFalse(IsSetVisible(GetControl(formInsPlan,"groupCanadian")));
				Assert.IsFalse(IsSetVisible(GetControl(formInsPlan,"groupOrtho")));
				Assert.IsFalse(IsSetVisible(GetControl(formInsPlan,"butPatOrtho")));
				Assert.IsFalse(IsSetVisible(GetControl(formInsPlan,"groupBox1")),"The empty Import Benefits group shows.");
				foreach(string name in new[] { "panelPat","groupSubscriber","panelPlan","panelOrthInfo","gridBenefits","butBenefits","textPlanNote",
					"groupRequestBen","butSave","butDrop","butPick","butAudit","butHistory" })
				{
					Assert.IsTrue(IsSetVisible(GetControl(formInsPlan,name)),name+" is hidden.");
				}
				//The plan's data is in the moved controls.
				Assert.AreEqual(fixture.InsPlan.PlanNum.ToString(),GetControl(formInsPlan,"textInsPlanNum").Text);
				Assert.AreEqual("Window Dental A",GetControl(formInsPlan,"textCarrier").Text);
				Assert.AreEqual(fixture.InsSub.SubscriberID,GetControl(formInsPlan,"textSubscriberID").Text);
			});
		}

		[TestMethod]
		public void PlanFromTheInsurancePlansList_OpensWithoutPatientAndSubscriber() {
			//Lists > Insurance Plans opens the window with no patient plan and no subscription: the 24.3 rules hide those panels.
			Fixture fixture=InsertFixture("Window Dental G");
			RunSta("en-US",() => {
				using FormInsPlan formInsPlan=new FormInsPlan(fixture.InsPlan,null,null);
				typeof(Form).GetMethod("OnLoad",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(formInsPlan,new object[] { EventArgs.Empty });
				Assert.IsFalse(IsSetVisible(GetControl(formInsPlan,"panelPat")));
				Assert.IsFalse(IsSetVisible(GetControl(formInsPlan,"groupSubscriber")));
				Assert.IsFalse(IsSetVisible(GetControl(formInsPlan,"butPick")));
				Assert.IsFalse(IsSetVisible(GetControl(formInsPlan,"butDelete")));
				Assert.IsTrue(IsSetVisible(GetControl(formInsPlan,"panelPlan")));
				Assert.IsTrue(IsSetVisible(GetControl(formInsPlan,"panelOrthInfo")));
				Assert.IsTrue(IsSetVisible(GetControl(formInsPlan,"gridBenefits")));
				Assert.AreEqual("Window Dental G",GetControl(formInsPlan,"textCarrier").Text);
			});
		}

		[TestMethod]
		public void ImportBenefits_ShowsWhenTrojanOrIapIsEnabled() {
			Fixture fixture=InsertFixture("Window Dental H");
			foreach(ProgramName programName in new[] { ProgramName.Trojan,ProgramName.IAP }) {
				Program program=Programs.GetCur(programName);
				Assert.IsNotNull(program,programName+" has no program row.");
				SetProgramEnabled(program,true);
				try {
					RunSta("en-US",() => {
						using FormInsPlan formInsPlan=OpenWindow(fixture);
						Assert.IsTrue(IsSetVisible(GetControl(formInsPlan,"groupBox1")),programName+": Import Benefits is hidden.");
						Assert.AreEqual(programName==ProgramName.Trojan,IsSetVisible(GetControl(formInsPlan,"textTrojanID")),programName.ToString());
					});
					//From Lists > Insurance Plans (no subscriber) the IAP button is hidden, so only Trojan's read-only ID keeps the group.
					RunSta("en-US",() => {
						using FormInsPlan formInsPlan=new FormInsPlan(fixture.InsPlan,null,null);
						typeof(Form).GetMethod("OnLoad",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(formInsPlan,new object[] { EventArgs.Empty });
						Assert.AreEqual(programName==ProgramName.Trojan,IsSetVisible(GetControl(formInsPlan,"groupBox1")),programName+" without subscriber");
					});
				}
				finally {
					SetProgramEnabled(program,false);
				}
			}
		}

		private static void SetProgramEnabled(Program program,bool isEnabled) {
			Program programOld=program.Copy();
			program.Enabled=isEnabled;
			Programs.Update(program,programOld);
			Cache.Refresh(InvalidType.Programs);
		}

		[TestMethod]
		public void UsOffice_PlanNoteMovesDownAndTheGridGrows() {
			Fixture fixture=InsertFixture("Window Dental B");
			RunSta("en-US",() => {
				using FormInsPlan formInsPlan=OpenWindow(fixture);
				Control panelPlan=GetControl(formInsPlan,"panelPlan");
				Control gridBenefits=GetControl(formInsPlan,"gridBenefits");
				Control butBenefits=GetControl(formInsPlan,"butBenefits");
				Control label18=GetControl(formInsPlan,"label18");
				Control textPlanNote=GetControl(formInsPlan,"textPlanNote");
				float scale=panelPlan.Height/554f;//the designer height; works at any dpi or zoom
				Assert.IsTrue(Math.Abs(textPlanNote.Bottom-panelPlan.Bottom)<=2,"The plan note should end with the columns: "+textPlanNote.Bottom+" "+panelPlan.Bottom);
				Assert.IsTrue(Math.Abs(gridBenefits.Height-(233+67)*scale)<=3,"Grid height "+gridBenefits.Height);
				Assert.IsTrue(gridBenefits.Bottom<=butBenefits.Top);
				Assert.IsTrue(gridBenefits.Bottom<=label18.Top);
				Assert.IsTrue(butBenefits.Bottom<=textPlanNote.Top);
				Assert.IsTrue(label18.Bottom<=textPlanNote.Top);
				Assert.AreEqual(gridBenefits.Left,textPlanNote.Left);
			});
		}

		[TestMethod]
		public void CanadianOffice_ShowsTheCanadianFields_WithoutMovingThePlanNote() {
			Fixture fixture=InsertFixture("Window Dental C");
			RunSta("en-CA",() => {
				using FormInsPlan formInsPlan=OpenWindow(fixture);
				Control groupCanadian=GetControl(formInsPlan,"groupCanadian");
				Control textPlanNote=GetControl(formInsPlan,"textPlanNote");
				Control gridBenefits=GetControl(formInsPlan,"gridBenefits");
				Assert.IsTrue(IsSetVisible(groupCanadian));
				Assert.IsTrue(textPlanNote.Bottom<=groupCanadian.Top,"The plan note overlaps the Canadian fields.");
				Assert.IsTrue(gridBenefits.Bottom<=textPlanNote.Top);
				foreach(string name in new[] { "textPlanFlag","checkIsPMP","textCanadianDiagCode","textCanadianInstCode" }) {
					Control control=GetControl(formInsPlan,name);
					Assert.AreEqual("groupCanadian",control.Parent.Name,name);
					Assert.IsTrue(IsSetVisible(control),name);
				}
				Assert.AreEqual("Dependant Code",GetControl(formInsPlan,"labelPatID").Text);
				Assert.IsTrue(IsSetVisible(GetControl(formInsPlan,"textDivisionNo")));
			});
		}

		[TestMethod]
		public void OrthoOn_ShowsTheOrthoGroup_AndItsRulesStillApply() {
			Prefs.UpdateBool(PrefName.OrthoEnabled,true);
			try {
				Fixture fixture=InsertFixture("Window Dental D");
				RunSta("en-US",() => {
					using FormInsPlan formInsPlan=OpenWindow(fixture);
					Assert.IsTrue(IsSetVisible(GetControl(formInsPlan,"groupOrtho")));
					Assert.IsTrue(IsSetVisible(GetControl(formInsPlan,"butPatOrtho")));
					Assert.AreEqual("panelOrtho",GetControl(formInsPlan,"comboOrthoClaimType").Parent.Name);
					//SetEnabledOrtho still runs over panelOrtho: with Initial Claim Only, the periodic fields are off.
					Assert.AreEqual(0,GetField<OpenPmsDental.UI.ComboBox>(formInsPlan,"comboOrthoClaimType").SelectedIndex);
					Assert.IsFalse(GetControl(formInsPlan,"comboOrthoAutoProcPeriod").Enabled);
					Assert.IsFalse(GetControl(formInsPlan,"butPickOrthoProc").Enabled);
				});
			}
			finally {
				Prefs.UpdateBool(PrefName.OrthoEnabled,false);
			}
		}

		[TestMethod]
		public void Window_PaintsCompletely_UsAndCanadaWithOrtho() {
			//Paints the whole window into a bitmap (every control's own painting runs) and keeps the pictures in the test folder, Snapshots\, for
			//a person to look at. The Canadian office with ortho on is the fullest the window gets.
			string folder=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Snapshots");
			System.IO.Directory.CreateDirectory(folder);
			Fixture fixture=InsertFixture("Snapshot Dental");
			foreach(bool isCanadaWithOrtho in new[] { false,true }) {
				Prefs.UpdateBool(PrefName.OrthoEnabled,isCanadaWithOrtho);
				try {
					RunSta(isCanadaWithOrtho ? "en-CA" : "en-US",() => {
						//Printing a window reaches only child windows that are shown, so this one is shown, far off any screen, for a moment.
						using FormInsPlan formInsPlan=new FormInsPlan(fixture.InsPlan,fixture.PatPlan,fixture.InsSub);
						formInsPlan.StartPosition=FormStartPosition.Manual;
						formInsPlan.Location=new Point(-20000,-20000);
						formInsPlan.Show();//raises Load once, as ShowDialog would
						Application.DoEvents();
						using Bitmap bitmap=new Bitmap(formInsPlan.Width,formInsPlan.Height);
						using(Graphics g=Graphics.FromImage(bitmap)) {
							IntPtr hdc=g.GetHdc();
							try {
								PrintWindow(formInsPlan.Handle,hdc,PW_RENDERFULLCONTENT);
							}
							finally {
								g.ReleaseHdc(hdc);
							}
						}
						formInsPlan.Hide();
						bitmap.Save(System.IO.Path.Combine(folder,isCanadaWithOrtho ? "FormInsPlan-Canada-Ortho.png" : "FormInsPlan-US.png"),
							System.Drawing.Imaging.ImageFormat.Png);
						//Something was painted in every column: the bitmap is not one flat color.
						HashSet<int> hashSetColors=new HashSet<int>();
						for(int x=0;x<bitmap.Width;x+=7) {
							for(int y=0;y<bitmap.Height;y+=7) {
								hashSetColors.Add(bitmap.GetPixel(x,y).ToArgb());
							}
						}
						Assert.IsTrue(hashSetColors.Count>5,"The window painted almost nothing.");
					});
				}
				finally {
					Prefs.UpdateBool(PrefName.OrthoEnabled,false);
				}
			}
		}

		[TestMethod]
		public void WriteOffOverrides_ShowTheNewWording_AndHelp() {
			Fixture fixture=InsertFixture("Window Dental E");
			RunSta("en-US",() => {
				using FormInsPlan formInsPlan=OpenWindow(fixture);
				OpenPmsDental.UI.ComboBox comboExclusionFeeRule=GetField<OpenPmsDental.UI.ComboBox>(formInsPlan,"comboExclusionFeeRule");
				List<string> listItems=new List<string>();
				for(int i=0;i<comboExclusionFeeRule.Items.Count;i++) {
					listItems.Add(comboExclusionFeeRule.Items.GetTextShowingAt(i));
				}
				CollectionAssert.AreEqual(new[] { "Practice Default","Do Nothing","Use Standard Provider Fee" },listItems);
				//In enum order, so the handler's cast of the index still gives the stored value.
				Assert.AreEqual((int)ExclusionRule.UseUcrFee,comboExclusionFeeRule.SelectedIndex);
				Assert.AreEqual(ExclusionRule.UseUcrFee,comboExclusionFeeRule.GetSelected<ExclusionRule>());
				ToolTip toolTip=GetField<ToolTip>(formInsPlan,"toolTip1");
				Assert.AreEqual(InsPlanWriteOffL.GetExclusionsHelp(),toolTip.GetToolTip(comboExclusionFeeRule));
				Assert.AreEqual(InsPlanWriteOffL.GetExclusionsHelp(),toolTip.GetToolTip(GetControl(formInsPlan,"label11")));
				Assert.AreEqual(InsPlanWriteOffL.GetAnnualMaxHelp(),toolTip.GetToolTip(GetControl(formInsPlan,"comboBoxZeroWriteOffsOnAnnualMaxLimitation")));
				Assert.AreEqual(InsPlanWriteOffL.GetFreqOrAgingHelp(),toolTip.GetToolTip(GetControl(formInsPlan,"comboZeroWriteOffsOnLimitationOrAging")));
				Assert.AreEqual(InsPlanWriteOffL.GetClaimsUseStandardFeeHelp(),toolTip.GetToolTip(GetControl(formInsPlan,"checkClaimsUseUCR")));
				Assert.AreEqual("Claims show standard fee, not billed fee",GetControl(formInsPlan,"checkClaimsUseUCR").Text);
			});
		}

		[TestMethod]
		public void EmployerAndCarrierDropDowns_OpenRightBelowTheirTextBoxes() {
			Fixture fixture=InsertFixture("Window Dental F");
			RunSta("en-US",() => {
				using FormInsPlan formInsPlan=OpenWindow(fixture);
				foreach(string[] arrayPair in new[] { new[] { "_listBoxEmps","textEmployer" },new[] { "_listBoxCarriers","textCarrier" } }) {
					Control listBox=GetControl(formInsPlan,arrayPair[0]);
					Control textBox=GetControl(formInsPlan,arrayPair[1]);
					//The text box's bottom left corner in the drop-down's container (the window's client panel), summed up the container chain.
					Point pointBelow=new Point(textBox.Left,textBox.Bottom);
					for(Control controlParent=textBox.Parent;controlParent!=listBox.Parent;controlParent=controlParent.Parent) {
						Assert.IsNotNull(controlParent,arrayPair[1]+" is not inside the drop-down's container.");
						pointBelow.Offset(controlParent.Left,controlParent.Top);
					}
					Assert.IsTrue(Math.Abs(listBox.Left-pointBelow.X)<=2 && Math.Abs(listBox.Top-pointBelow.Y)<=2,
						arrayPair[0]+" opens at "+listBox.Location+", the text box ends at "+pointBelow);
				}
			});
		}

		[TestMethod]
		public void Drop_LogsThePlanNumInTheAuditText() {
			Fixture fixture=InsertFixture("Same Name Dental");
			Fixture fixtureOther=InsertFixture("Same Name Dental");//a second plan with the same carrier name
			bool isDropped=false;
			RunSta("en-US",() => {
				using FormInsPlan formInsPlan=OpenWindow(fixture);
				MethodInfo methodDrop=typeof(FormInsPlan).GetMethod("DropClickHelper",BindingFlags.Instance|BindingFlags.NonPublic);
				isDropped=(bool)methodDrop.Invoke(formInsPlan,null);
			});
			Assert.IsTrue(isDropped);
			Assert.IsNull(PatPlans.GetByPatPlanNum(fixture.PatPlan.PatPlanNum));
			DataTable table=GetSecurityLogs(EnumPermType.InsPlanEdit,fixture.InsPlan.PlanNum);
			Assert.AreEqual(1,table.Rows.Count);
			Assert.AreEqual("The insurance plan for the carrier Same Name Dental (PlanNum "+fixture.InsPlan.PlanNum+") was dropped.",
				table.Rows[0]["LogText"].ToString());
			Assert.AreNotEqual(fixture.InsPlan.PlanNum,fixtureOther.InsPlan.PlanNum);
			Assert.IsNotNull(PatPlans.GetByPatPlanNum(fixtureOther.PatPlan.PatPlanNum),"The other plan was dropped too.");
		}

		[TestMethod]
		public void Drop834Import_LogsThePlanNumAndTheCarrierName() {
			Fixture fixture=InsertFixture("Import Dental");
			MethodInfo methodDropPlan=typeof(EtransL).GetMethod("DropPlan",BindingFlags.Static|BindingFlags.NonPublic);
			Assert.IsNotNull(methodDropPlan,"EtransL.DropPlan is gone.");
			methodDropPlan.Invoke(null,new object[] { fixture.PatPlan,fixture.InsPlan,fixture.InsSub,fixture.Carrier });
			Assert.IsNull(PatPlans.GetByPatPlanNum(fixture.PatPlan.PatPlanNum));
			DataTable table=GetSecurityLogs(EnumPermType.InsPlanDropPat,fixture.InsPlan.PlanNum);
			Assert.AreEqual(1,table.Rows.Count);
			string logText=table.Rows[0]["LogText"].ToString();
			StringAssert.Contains(logText,"carrier 'Import Dental' (PlanNum "+fixture.InsPlan.PlanNum+")");
			StringAssert.Contains(logText,"from Import Ins Plans 834.");
		}
	}
}
