// The windows this package changed, built in memory and never shown: Fee Tools puts each tool on its own tab and keeps every 24.3 control and
// the 24.4 date boxes (Open Dental 25.1); Procedure Code Tools has the D Codes and Code Groups options; More Fees has the Provider and Clinic
// filters. The click handlers are checked in the source, since a form that is never shown never raises them.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDental;
using OpenDentBusiness;

namespace BPROCTests {
	[TestClass]
	public class FormLayoutTests {
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

		///<summary>The names of every control under the control, at any depth.</summary>
		private static HashSet<string> GetNamesBelow(Control control) {
			HashSet<string> hashSetNames=new HashSet<string>();
			foreach(Control controlChild in control.Controls) {
				hashSetNames.Add(controlChild.Name);
				hashSetNames.UnionWith(GetNamesBelow(controlChild));
			}
			return hashSetNames;
		}

		private static string ReadSource(string fileName) {
			return File.ReadAllText(TestDb.FindRepoFile(Path.Combine("OpenDental","Forms",fileName)));
		}

		[TestMethod]
		public void FeeTools_EachToolOnItsOwnTab() {
			RunSta(() => {
				using FormFeeSchedTools formFeeSchedTools=new FormFeeSchedTools(0,new List<FeeSched>(),new List<Provider>(),new List<Clinic>());
				OpenDental.UI.TabControl tabControl=GetField<OpenDental.UI.TabControl>(formFeeSchedTools,"tabControlTools");
				CollectionAssert.AreEqual(new[] { "Copy","Increase","Clear","Export / Import","Global Updates" },
					tabControl.TabPages.Select(x => x.Text).ToArray());
				Dictionary<string,string[]> dictControlsByTab=new Dictionary<string,string[]> {
					{ "tabPageCopy",new[] { "groupBox1","comboFeeSchedTo","butPickSchedTo","comboClinicTo","comboGroupTo","butPickGroupTo","comboProviderTo",
						"butPickProvTo","textDateEffectiveCopy","butCopy" } },
					{ "tabPageIncrease",new[] { "groupBox3","textPercent","groupBox4","radioDollar","radioDime","radioPenny","textDateAsOf",
						"textDateEffectiveNew","butIncrease" } },
					{ "tabPageClear",new[] { "groupBox2","butClear" } },
					{ "tabPageExportImport",new[] { "groupBox5","butExport","butImport","checkExportFuture","textDateEffectiveImport" } },
					{ "tabPageGlobalUpdates",new[] { "groupGlobalUpdateFees","comboGlobalUpdateClinics","butUpdate","butUpdateWriteoffs" } },
				};
				List<HashSet<string>> listNamesPerTab=new List<HashSet<string>>();
				foreach(KeyValuePair<string,string[]> pair in dictControlsByTab) {
					OpenDental.UI.TabPage tabPage=GetField<OpenDental.UI.TabPage>(formFeeSchedTools,pair.Key);
					Assert.IsTrue(tabControl.TabPages.Contains(tabPage),pair.Key);
					HashSet<string> hashSetNames=GetNamesBelow(tabPage);
					foreach(string name in pair.Value) {
						Assert.IsTrue(hashSetNames.Contains(name),name+" should be on "+pair.Key);
					}
					listNamesPerTab.Add(hashSetNames);
				}
				//The rounding options are only on Increase, the target only on Copy.
				Assert.AreEqual(1,listNamesPerTab.Count(x => x.Contains("radioPenny")));
				Assert.AreEqual(1,listNamesPerTab.Count(x => x.Contains("comboFeeSchedTo")));
				//The shared Select Fees panel stays above the tabs, without the Increase-only date.
				HashSet<string> hashSetSelectFees=GetNamesBelow(GetField<Control>(formFeeSchedTools,"groupBox7"));
				foreach(string name in new[] { "comboFeeSched","butPickSched","comboClinic","comboGroup","butPickGroup","comboProvider","butPickProv" }) {
					Assert.IsTrue(hashSetSelectFees.Contains(name),name+" should be in Select Fees");
				}
				Assert.IsFalse(hashSetSelectFees.Contains("textDateAsOf"));
				Assert.IsFalse(GetNamesBelow(tabControl).Contains("groupBox7"));
				Control checkShowGroups=GetField<Control>(formFeeSchedTools,"checkShowGroups");
				Assert.AreSame(formFeeSchedTools,checkShowGroups.Parent,"Show Fee Schedule Groups stays with the shared selection.");
			});
		}

		[TestMethod]
		public void FeeTools_EveryToolIsStillWired() {
			string designer=ReadSource("FormFeeSchedTools.designer.cs");
			foreach(string wiring in new[] {
				"this.butClear.Click += new System.EventHandler(this.butClear_Click);",
				"this.butCopy.Click += new System.EventHandler(this.butCopy_Click);",
				"this.butIncrease.Click += new System.EventHandler(this.butIncrease_Click);",
				"this.butExport.Click += new System.EventHandler(this.butExport_Click);",
				"this.butImport.Click += new System.EventHandler(this.butImport_Click);",
				"this.butUpdate.Click += new System.EventHandler(this.butUpdateFees_Click);",
				"this.butUpdateWriteoffs.Click += new System.EventHandler(this.butUpdateWriteoffs_Click);",
				"this.checkShowGroups.CheckedChanged += new System.EventHandler(this.checkShowGroups_CheckedChanged);",
				"this.comboFeeSched.SelectionChangeCommitted += new System.EventHandler(this.comboFeeCombos_SelectionChangeCommitted);",
				"this.comboFeeSchedTo.SelectionChangeCommitted += new System.EventHandler(this.comboFeeCombos_SelectionChangeCommitted);",
			}) {
				StringAssert.Contains(designer,wiring);
			}
			string code=ReadSource("FormFeeSchedTools.cs");
			StringAssert.Contains(code,"Security.IsAuthorized(EnumPermType.FeeSchedEdit)");
			StringAssert.Contains(code,"Fees.SaveIncrease(listFees,percent,round,dateEffectiveNew)");
			StringAssert.Contains(code,"FeeScheds.CopyFeeSchedule(feeSched,fromClinicNum,fromProvNum,feeSchedTo,listClinicNumsTo,toProvNum,dateEffectiveCopy)");
		}

		[TestMethod]
		public void ProcTools_HasCodeImportAndCodeGroups() {
			RunSta(() => {
				using FormProcTools formProcTools=new FormProcTools();
				Control checkCodeImport=GetField<Control>(formProcTools,"checkCodeImport");
				Control checkCodeGroups=GetField<Control>(formProcTools,"checkCodeGroups");
				Assert.AreSame(formProcTools,checkCodeImport.Parent);
				Assert.AreSame(formProcTools,checkCodeGroups.Parent);
				StringAssert.StartsWith(checkCodeImport.Text,"D Codes");
				StringAssert.StartsWith(checkCodeGroups.Text,"Code Groups");
				//Every option has its own row.
				List<Control> listChecks=formProcTools.Controls.Cast<Control>().Where(x => x.Name.StartsWith("check")).ToList();
				Assert.AreEqual(8,listChecks.Count);
				Assert.AreEqual(listChecks.Count,listChecks.Select(x => x.Top).Distinct().Count(),"No two options overlap.");
			});
			string code=ReadSource("FormProcTools.cs");
			StringAssert.Contains(code,"Security.IsAuthorized(EnumPermType.ProcCodeEdit)");
			StringAssert.Contains(code,"ProcCodeImports.IsYearEndWindow(DateTime.Today)");
			StringAssert.Contains(code,"CodeGroupResets.Reset(codeSystem,doKeepUnmatched:hasUnmatched)");
			StringAssert.Contains(code,"DataValid.SetInvalid(InvalidType.CodeGroups)");
			Assert.IsFalse(code.Contains("WebClient") || code.Contains("http"),"Codes come from a file the office supplies, never a download.");
		}

		[TestMethod]
		public void MoreFees_HasProviderAndClinicFilters() {
			RunSta(() => {
				using FormProcCodeEditMore formProcCodeEditMore=new FormProcCodeEditMore(new ProcedureCode { ProcCode="Z9999" });
				Control comboProvider=GetField<Control>(formProcCodeEditMore,"comboProvider");
				Control comboClinic=GetField<Control>(formProcCodeEditMore,"comboClinic");
				Control gridMain=GetField<Control>(formProcCodeEditMore,"gridMain");
				Assert.IsInstanceOfType(comboClinic,typeof(OpenDental.UI.ComboBoxClinicPicker));
				Assert.IsTrue(comboProvider.Bottom<=gridMain.Top && comboClinic.Bottom<=gridMain.Top,"Filters sit above the grid.");
				Assert.IsTrue(((OpenDental.UI.ComboBoxClinicPicker)comboClinic).IncludeAll);
				Assert.IsTrue(((OpenDental.UI.ComboBoxClinicPicker)comboClinic).IncludeUnassigned);
			});
			string code=ReadSource("FormProcCodeEditMore.cs");
			StringAssert.Contains(code,"MoreFeesFilters.Filter(_listFees,provNumFilter,clinicNumFilter)");
			StringAssert.Contains(code,"Clinics.GetForUserod(Security.CurUser,true)");
		}
	}
}
