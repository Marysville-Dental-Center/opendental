// The windows of package M2-CAN, built in memory and never shown: Procedure Code Tools gets an Other Codes button in Canada only; Other
// Procedure Codes lists the code systems and the four Advanced Options; Enter Payment has a hidden Pay Labs button. Click handlers are checked in
// the source, since a window that is never shown never raises them.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDentalBusiness;

namespace M2CANTests {
	[TestClass]
	public class WindowTests {
		private static T GetField<T>(object form,string name) where T:class {
			FieldInfo fieldInfo=form.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
			Assert.IsNotNull(fieldInfo,"No field "+name);
			return fieldInfo.GetValue(form) as T;
		}

		private static List<Control> GetAllControls(Control control) {
			List<Control> listControls=new List<Control>();
			foreach(Control controlChild in control.Controls) {
				listControls.Add(controlChild);
				listControls.AddRange(GetAllControls(controlChild));
			}
			return listControls;
		}

		[TestMethod]
		public void ProcTools_OtherCodesOnlyInCanada() {
			Sta.Run("en-US",() => {
				Assert.IsFalse(FormProcTools.IsOtherCodesAvailable());
				using FormProcTools formProcTools=new FormProcTools();
				Assert.IsNull(GetField<OpenPmsDental.UI.Button>(formProcTools,"_butOtherCodes"));
				Assert.IsFalse(GetAllControls(formProcTools).Any(x => x.Name=="butOtherCodes"));
			});
			foreach(string cultureName in new[] { "en-CA","fr-CA" }) {
				Sta.Run(cultureName,() => {
					Assert.IsTrue(FormProcTools.IsOtherCodesAvailable(),cultureName);
					using FormProcTools formProcTools=new FormProcTools();
					OpenPmsDental.UI.Button butOtherCodes=GetField<OpenPmsDental.UI.Button>(formProcTools,"_butOtherCodes");
					Assert.IsNotNull(butOtherCodes,cultureName);
					Assert.IsTrue(GetAllControls(formProcTools).Contains(butOtherCodes),"The button is on the window.");
					Control butRun=GetField<Control>(formProcTools,"butRun");
					//The window is never shown, so the designer controls keep their 96 dpi bounds while the added button has screen bounds.
					object layoutManager=typeof(FormODBase).GetField("LayoutManager",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(formProcTools);
					MethodInfo methodUnscale=layoutManager.GetType().GetMethod("Unscale",new[] { typeof(float) });
					int top96=(int)methodUnscale.Invoke(layoutManager,new object[] { (float)butOtherCodes.Top });
					int right96=(int)methodUnscale.Invoke(layoutManager,new object[] { (float)butOtherCodes.Right });
					Assert.IsTrue(Math.Abs(butRun.Top-top96)<=1,"Level with Run Now: "+butRun.Top+" and "+top96);
					Assert.IsTrue(right96<butRun.Left,"Left of Run Now.");
					//Below every option, so it covers none of them.
					foreach(Control control in GetAllControls(formProcTools).Where(x => x.Name.StartsWith("check"))) {
						Assert.IsTrue(control.Bottom<=top96,control.Name+" is above the button.");
					}
				});
			}
			string code=TestPaths.ReadRepoFile("OpenPmsDental","Forms","FormProcTools.cs");
			StringAssert.Contains(code,"AddOtherCodesButton();");
			string codeOther=TestPaths.ReadRepoFile("OpenPmsDental","Forms","FormProcTools.OtherCodes.cs");
			StringAssert.Contains(codeOther,"_butOtherCodes.Click+=butOtherCodes_Click;");
			StringAssert.Contains(codeOther,"_butOtherCodes.Anchor=AnchorStyles.Bottom|AnchorStyles.Left;");
			StringAssert.Contains(codeOther,"using FormProcCodesOther formProcCodesOther=new FormProcCodesOther();");
			Assert.IsFalse(codeOther.Contains("http") || codeOther.Contains("WebClient"),"Codes come from a file the office supplies, never a download.");
		}

		[TestMethod]
		public void ProcCodesOther_SystemsAndAdvancedOptions() {
			Sta.Run("en-US",() => {
				using FormProcCodesOther formProcCodesOther=new FormProcCodesOther();
				Assert.AreEqual("Other Procedure Codes",formProcCodesOther.Text);
				OpenPmsDental.UI.GroupBox groupAdvanced=GetField<OpenPmsDental.UI.GroupBox>(formProcCodesOther,"groupAdvanced");
				Assert.AreEqual("Advanced Options",groupAdvanced.Text);
				Dictionary<string,EnumOtherCodeUpdates> dictChecks=new Dictionary<string,EnumOtherCodeUpdates> {
					{ "checkUpdateDescriptions",EnumOtherCodeUpdates.Descriptions },
					{ "checkUpdateAbbreviations",EnumOtherCodeUpdates.Abbreviations },
					{ "checkUpdateTreatArea",EnumOtherCodeUpdates.TreatArea },
					{ "checkUpdatePaintType",EnumOtherCodeUpdates.PaintType },
				};
				MethodInfo methodGetUpdates=typeof(FormProcCodesOther).GetMethod("GetUpdates",BindingFlags.Instance|BindingFlags.NonPublic);
				Assert.AreEqual(EnumOtherCodeUpdates.None,(EnumOtherCodeUpdates)methodGetUpdates.Invoke(formProcCodesOther,null));
				EnumOtherCodeUpdates updatesAll=EnumOtherCodeUpdates.None;
				foreach(KeyValuePair<string,EnumOtherCodeUpdates> pair in dictChecks) {
					OpenPmsDental.UI.CheckBox checkBox=GetField<OpenPmsDental.UI.CheckBox>(formProcCodesOther,pair.Key);
					Assert.AreSame(groupAdvanced,checkBox.Parent,pair.Key+" is an Advanced Option.");
					checkBox.Checked=true;
					updatesAll|=pair.Value;
					Assert.AreEqual(updatesAll,(EnumOtherCodeUpdates)methodGetUpdates.Invoke(formProcCodesOther,null),pair.Key);
				}
			});
			string designer=TestPaths.ReadRepoFile("OpenPmsDental","Forms","FormProcCodesOther.Designer.cs");
			StringAssert.Contains(designer,"this.butOK.Click += new System.EventHandler(this.butOK_Click);");
			StringAssert.Contains(designer,"this.butBrowse.Click += new System.EventHandler(this.butBrowse_Click);");
			StringAssert.Contains(designer,"this.Load += new System.EventHandler(this.FormProcCodesOther_Load);");
			string code=TestPaths.ReadRepoFile("OpenPmsDental","Forms","FormProcCodesOther.cs");
			//The list holds every code system, in the enum's order.
			StringAssert.Contains(code,"foreach(EnumOtherCodeSystem codeSystem in Enum.GetValues(typeof(EnumOtherCodeSystem)))");
			StringAssert.Contains(code,"DataValid.SetInvalid(InvalidType.Defs,InvalidType.ProcCodes);");
			Assert.IsFalse(code.Contains("http") || code.Contains("WebClient"),"Codes come from a file the office supplies, never a download.");
		}

		[TestMethod]
		public void ClaimPayTotal_PayLabsButtonHiddenUntilCanadianLabs() {
			Sta.Run("en-US",() => {
				using FormClaimPayTotal formClaimPayTotal=new FormClaimPayTotal(new Patient(),new Family(),new List<InsPlan>(),new List<PatPlan>(),
					new List<InsSub>(),null);
				OpenPmsDental.UI.Button butPayLabs=GetField<OpenPmsDental.UI.Button>(formClaimPayTotal,"butPayLabs");
				Assert.IsNotNull(butPayLabs);
				Assert.AreEqual("Pay &Labs",butPayLabs.Text);
				Assert.IsTrue(GetAllControls(formClaimPayTotal).Contains(butPayLabs));
				Control butWriteOff=GetField<Control>(formClaimPayTotal,"butWriteOff");
				Assert.AreEqual(butWriteOff.Top,butPayLabs.Top,"Next to Write-off.");
				Assert.IsTrue(butPayLabs.Left>=butWriteOff.Right);
			});
			string designer=TestPaths.ReadRepoFile("OpenPmsDental","Forms","FormClaimPayTotal.designer.cs");
			StringAssert.Contains(designer,"this.butPayLabs.Visible = false;");
			StringAssert.Contains(designer,"this.butPayLabs.Click += new System.EventHandler(this.butPayLabs_Click);");
			string code=TestPaths.ReadRepoFile("OpenPmsDental","Forms","FormClaimPayTotal.cs");
			StringAssert.Contains(code,"butPayLabs.Visible=CultureInfo.CurrentCulture.Name.EndsWith(\"CA\") && ClaimProcs.HasPayLabsGroups(ClaimProcArray,_listProcedures);");
			StringAssert.Contains(code,"ClaimProcs.PayLabs(ClaimProcArray,_listProcedures)");
			//The typed amounts are read from the grid first, and the grid is filled again after.
			int indexHandler=code.IndexOf("private void butPayLabs_Click(");
			Assert.IsTrue(indexHandler>0);
			int indexSave=code.IndexOf("SaveGridChanges(skipChecks:true)",indexHandler);
			int indexPay=code.IndexOf("ClaimProcs.PayLabs(",indexHandler);
			int indexFill=code.IndexOf("FillGrid();",indexHandler);
			Assert.IsTrue(indexSave>indexHandler && indexPay>indexSave && indexFill>indexPay);
		}
	}
}
