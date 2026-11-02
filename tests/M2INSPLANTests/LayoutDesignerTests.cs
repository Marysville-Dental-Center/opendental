// The 26.1 layout of Edit Insurance Plan (backlog item v26_1-family-insplan-layout), checked on the designer file: every control of the 24.3
// window is still there and on screen without tab pages or scrolling, every old event handler is still wired, the window fits the smallest
// supported screen, nothing sticks out of its container and no two controls overlap. No database, no window.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace M2INSPLANTests {
	[TestClass]
	public class LayoutDesignerTests {
		///<summary>The smallest supported workstation (our choice, see the port note): 1366x768 at 100%, with a 48 pixel taskbar (Windows 11).</summary>
		public static readonly Size SizeScreenSmallest=new Size(1366,768);
		public const int HEIGHT_TASKBAR=48;
		///<summary>FormODBase adds 8 pixels of border on the left, right and bottom and a 26 pixel title bar at 96 dpi.</summary>
		public const int WIDTH_BORDER=8;
		public const int HEIGHT_TITLE=26;
		///<summary>Containers of the 24.3 tab layout, gone in 26.1.</summary>
		private static readonly string[] _arrayRemoved={ "tabControlInsPlan","tabPageInsPlanInfo","tabPageOtherInsInfo","tabPageCanadian","panelCanadian","tabPageOrtho" };
		///<summary>Pairs that share a spot on purpose: the code shows one or the other (FormInsPlan.FillOtherSubscribers).</summary>
		private static readonly string[][] _arrayOverlapAllowed={ new[]{ "butOtherSubscribers","comboLinked" } };

		private static DesignerModel _designerModel;

		private static DesignerModel Model {
			get {
				if(_designerModel==null) {
					_designerModel=DesignerModel.LoadFormInsPlan();
				}
				return _designerModel;
			}
		}

		///<summary>name, type, keep or removed, from Data\FormInsPlan24_3.controls.txt.</summary>
		public static List<string[]> GetOldControls() {
			return ReadData("FormInsPlan24_3.controls.txt");
		}

		public static List<string[]> ReadData(string fileName) {
			string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Data",fileName);
			return File.ReadAllLines(path).Where(x => x.Trim()!="" && !x.StartsWith("#")).Select(x => x.Split('\t')).ToList();
		}

		[TestMethod]
		public void OldInventory_IsComplete() {
			//Guards the data file itself: the 24.3 designer declared 183 fields, 6 of them the tab layout's containers.
			List<string[]> listOld=GetOldControls();
			Assert.AreEqual(183,listOld.Count);
			CollectionAssert.AreEquivalent(_arrayRemoved,listOld.Where(x => x[2]=="removed").Select(x => x[0]).ToArray());
			Assert.AreEqual(44,ReadData("FormInsPlan24_3.events.txt").Count);
		}

		[TestMethod]
		public void EveryControlOfThe24_3Window_IsStillDeclaredWithItsType() {
			List<string> listProblems=new List<string>();
			foreach(string[] arrayOld in GetOldControls().Where(x => x[2]=="keep")) {
				if(!Model.DictControls.TryGetValue(arrayOld[0],out DesignerControl designerControl)) {
					listProblems.Add(arrayOld[0]+" is gone");
					continue;
				}
				if(designerControl.TypeName!=arrayOld[1]) {
					listProblems.Add(arrayOld[0]+" is now "+designerControl.TypeName+", was "+arrayOld[1]);
				}
			}
			Assert.AreEqual("",string.Join("; ",listProblems));
		}

		[TestMethod]
		public void TabLayout_IsGone() {
			foreach(string name in _arrayRemoved) {
				Assert.IsFalse(Model.DictControls.ContainsKey(name),name+" is still declared.");
			}
			Assert.IsFalse(Model.DictControls.Values.Any(x => x.TypeName.Contains("TabControl") || x.TypeName.Contains("TabPage")),
				"The window has a tab control again.");
		}

		[TestMethod]
		public void EveryControl_IsOnTheForm_WithNoTabPageOrScrollingPanelAbove() {
			List<string> listProblems=new List<string>();
			foreach(DesignerControl designerControl in Model.DictControls.Values) {
				if(designerControl.TypeName=="System.Windows.Forms.ToolTip") {
					continue;//a component, not a control
				}
				try {
					List<DesignerControl> listAncestors=Model.GetAncestors(designerControl.Name);
					DesignerControl designerControlScroll=listAncestors.FirstOrDefault(x => x.IsAutoScroll);
					if(designerControlScroll!=null) {
						listProblems.Add(designerControl.Name+" is inside the scrolling "+designerControlScroll.Name);
					}
				}
				catch(Exception ex) {
					listProblems.Add(ex.Message);
				}
			}
			Assert.AreEqual("",string.Join("; ",listProblems));
		}

		[TestMethod]
		public void NoContainer_Scrolls() {
			Assert.IsFalse(Model.DictControls.Values.Any(x => x.IsAutoScroll),"A panel still scrolls: "
				+string.Join(", ",Model.DictControls.Values.Where(x => x.IsAutoScroll).Select(x => x.Name)));
			StringAssert.DoesNotMatch(Model.Source,new System.Text.RegularExpressions.Regex(@"AutoScroll\s*=\s*true"));
		}

		[TestMethod]
		public void EveryOldEventHandler_IsStillWired() {
			List<string> listMissing=new List<string>();
			foreach(string[] arrayEvent in ReadData("FormInsPlan24_3.events.txt")) {
				string wiring=arrayEvent[0]+"."+arrayEvent[1]+":"+arrayEvent[2];
				if(!Model.ListEvents.Contains(wiring)) {
					listMissing.Add(wiring);
				}
			}
			Assert.AreEqual("",string.Join("; ",listMissing));
			CollectionAssert.Contains(Model.ListEvents,"butBenefits.Click:butBenefits_Click");
		}

		[TestMethod]
		public void Window_FitsTheSmallestSupportedScreen() {
			int widthWindow=Model.ClientSize.Width+2*WIDTH_BORDER;
			int heightWindow=Model.ClientSize.Height+HEIGHT_TITLE+WIDTH_BORDER;
			Assert.IsTrue(widthWindow<=SizeScreenSmallest.Width,"Window width "+widthWindow);
			Assert.IsTrue(heightWindow<=SizeScreenSmallest.Height-HEIGHT_TASKBAR,"Window height "+heightWindow);
			//And it really is the larger window the fixed panels need, not the old 984x696.
			Assert.AreEqual(new Size(1340,686),Model.ClientSize);
		}

		[TestMethod]
		public void EveryControl_LiesInsideItsContainer() {
			List<string> listProblems=new List<string>();
			foreach(DesignerControl designerControl in Model.DictControls.Values.Where(x => x.HasBounds && x.ParentName!=null)) {
				Size sizeParent=Model.GetClientSizeOf(designerControl.ParentName);
				Rectangle rectangle=designerControl.Bounds;
				if(rectangle.Left<0 || rectangle.Top<0 || rectangle.Right>sizeParent.Width || rectangle.Bottom>sizeParent.Height) {
					listProblems.Add(designerControl.Name+" "+rectangle+" in "+designerControl.ParentName+" "+sizeParent);
				}
			}
			Assert.AreEqual("",string.Join("; ",listProblems));
		}

		[TestMethod]
		public void NoTwoControls_Overlap() {
			List<string> listProblems=new List<string>();
			foreach(IGrouping<string,DesignerControl> grouping in Model.DictControls.Values.Where(x => x.HasBounds && x.ParentName!=null)
				.GroupBy(x => x.ParentName))
			{
				List<DesignerControl> listSiblings=grouping.ToList();
				for(int i=0;i<listSiblings.Count;i++) {
					for(int j=i+1;j<listSiblings.Count;j++) {
						if(!listSiblings[i].Bounds.IntersectsWith(listSiblings[j].Bounds)) {
							continue;
						}
						if(_arrayOverlapAllowed.Any(x => x.Contains(listSiblings[i].Name) && x.Contains(listSiblings[j].Name))) {
							continue;
						}
						listProblems.Add(listSiblings[i].Name+" "+listSiblings[i].Bounds+" and "+listSiblings[j].Name+" "+listSiblings[j].Bounds);
					}
				}
			}
			Assert.AreEqual("",string.Join("; ",listProblems));
		}

		[TestMethod]
		public void Panels_AreWhereTheSpecPutsThem() {
			//Patient plan strip across the top, then the plan (two columns) and the subscriber with the benefits, then the bottom bar.
			Assert.AreEqual(DesignerModel.FORM,Model.Get("panelPat").ParentName);
			Assert.AreEqual(0,Model.Get("panelPat").Bounds.Top);
			int topColumns=Model.Get("panelPlan").Bounds.Top;
			Assert.IsTrue(topColumns>=Model.Get("panelPat").Bounds.Bottom);
			Assert.AreEqual(topColumns,Model.Get("panelOrthInfo").Bounds.Top);
			Assert.AreEqual(topColumns,Model.Get("groupSubscriber").Bounds.Top);
			Assert.IsTrue(Model.Get("panelPlan").Bounds.Right<=Model.Get("panelOrthInfo").Bounds.Left);
			Assert.IsTrue(Model.Get("panelOrthInfo").Bounds.Right<=Model.Get("groupSubscriber").Bounds.Left);
			//Plan panel: carrier and Pick From List, employer, group, plan type, fee schedules, plan-level audit trail.
			foreach(string name in new[] { "butPick","butAudit","textEmployer","textCarrier","textGroupName","textGroupNum","comboPlanType","comboFeeSched",
				"comboOutOfNetwork","comboManualBlueBook","comboCopay","textInsPlanNum" })
			{
				Assert.IsTrue(Model.GetAncestors(name).Any(x => x.Name=="panelPlan"),name+" is not in the plan panel.");
			}
			//Claim form, filing code, write-off overrides, per visit amounts and ortho in the second plan column.
			foreach(string name in new[] { "comboClaimForm","comboFilingCode","comboFilingCodeSubtype","comboCobRule","comboBillType","comboExclusionFeeRule",
				"textPerVisitPatAmount","comboOrthoClaimType","checkOrthoWaitDays" })
			{
				Assert.IsTrue(Model.GetAncestors(name).Any(x => x.Name=="panelOrthInfo"),name+" is not in the second plan column.");
			}
			//The ortho fields stay in panelOrtho (FormInsPlan.SetEnabledOrtho disables its children), now inside groupOrtho.
			Assert.AreEqual("groupOrtho",Model.Get("panelOrtho").ParentName);
			//Patient level: relationship, patient ID, order, pending, Hist, patient-level audit trail, drop.
			foreach(string name in new[] { "comboRelationship","textPatID","textOrdinal","checkIsPending","butHistory","butAuditPat","butDrop","listAdj" }) {
				Assert.AreEqual("panelPat",Model.Get(name).ParentName,name);
			}
			//Bottom bar.
			foreach(string name in new[] { "butDelete","butLabel","butSave","groupChanges" }) {
				Assert.AreEqual(DesignerModel.FORM,Model.Get(name).ParentName,name);
				Assert.IsTrue(Model.Get(name).Bounds.Top>=Model.Get("panelPlan").Bounds.Bottom,name+" is not below the panels.");
			}
		}

		[TestMethod]
		public void TabOrder_IsUniqueInEveryContainer_AndFollowsThePanels() {
			string[] arrayFocusable={ "TextBox","Valid","ODtextBox","ComboBox","CheckBox","Button","RadioButton","ListBox","GridOD","Panel","GroupBox" };
			List<string> listProblems=new List<string>();
			foreach(IGrouping<string,DesignerControl> grouping in Model.DictControls.Values
				.Where(x => x.ParentName!=null && arrayFocusable.Any(y => x.TypeName.Contains(y)))
				.GroupBy(x => x.ParentName))
			{
				foreach(IGrouping<int,DesignerControl> groupingIdx in grouping.GroupBy(x => x.TabIndex).Where(x => x.Count()>1)) {
					listProblems.Add(grouping.Key+": TabIndex "+groupingIdx.Key+" on "+string.Join(", ",groupingIdx.Select(x => x.Name)));
				}
			}
			Assert.AreEqual("",string.Join("; ",listProblems));
			string[] arrayOrder={ "panelPat","groupSubscriber","panelPlan","panelOrthInfo","gridBenefits","butBenefits","textPlanNote","groupCanadian",
				"groupRequestBen","groupBox1","textDateLastVerifiedBenefits","butVerifyBenefits","checkDontVerify","groupChanges","butSave" };
			for(int i=1;i<arrayOrder.Length;i++) {
				Assert.IsTrue(Model.Get(arrayOrder[i-1]).TabIndex<Model.Get(arrayOrder[i]).TabIndex,arrayOrder[i-1]+" should come before "+arrayOrder[i]);
			}
			//Inside the plan panel: the plan fields, then plan type and fee schedules, then the buttons.
			Assert.IsTrue(Model.Get("groupPlan").TabIndex<Model.Get("comboPlanType").TabIndex);
			Assert.IsTrue(Model.Get("textEmployer").TabIndex<Model.Get("groupCarrier").TabIndex);
			Assert.IsTrue(Model.Get("groupCarrier").TabIndex<Model.Get("textGroupName").TabIndex);
			Assert.IsTrue(Model.Get("comboOrthoClaimType").TabIndex<Model.Get("checkOrthoWaitDays").TabIndex);
		}

		///<summary>Text the code puts on a control at run time, besides the designer's own: Canadian and British labels (FormInsPlan constructor), the
		///fixed benefit label (GetFilteredCopayFeeSched), and the longest item of some combo boxes.</summary>
		private static readonly string[][] _arrayRuntimeTexts={
			new[]{ "labelPatID","Dependant Code" },
			new[]{ "labelCitySTZip","City,Prov,Post" },
			new[]{ "labelCitySTZip","City,Postcode" },
			new[]{ "labelElectronicID","EDI Code" },
			new[]{ "labelGroupNum","Plan Number" },
			new[]{ "labelCopayFeeSched","Fixed Benefit Amounts" },
			new[]{ "comboExclusionFeeRule","Use Standard Provider Fee" },
			new[]{ "comboPlanType","Medicaid or Flat Co-pay" },
			new[]{ "comboOrthoClaimType","Initial Plus Periodic" },
			new[]{ "comboOrthoAutoProcPeriod","SemiAnnual" },
			new[]{ "comboBoxZeroWriteOffsOnAnnualMaxLimitation","Default" },
		};

		[TestMethod]
		public void EveryText_FitsItsControl_At96Dpi() {
			//Labels draw with GDI (TextRenderer); our check boxes, buttons, group box titles and combo boxes with GDI+ (Graphics.DrawString). A label
			//that is one line high must hold its text on one line; a taller one must hold it wrapped.
			List<string[]> listTexts=Model.DictControls.Values.Where(x => !string.IsNullOrEmpty(x.Text) && x.Text.IndexOf('\n')<0)
				.Select(x => new[] { x.Name,x.Text }).ToList();
			listTexts.AddRange(_arrayRuntimeTexts);
			List<string> listProblems=new List<string>();
			using Bitmap bitmap=new Bitmap(1,1);//96 dpi
			using Graphics g=Graphics.FromImage(bitmap);
			foreach(string[] arrayText in listTexts) {
				DesignerControl designerControl=Model.Get(arrayText[0]);
				string text=arrayText[1].Replace("&","");
				if(!designerControl.HasBounds) {
					continue;
				}
				using Font font=new Font("Microsoft Sans Serif",designerControl.FontSize,designerControl.IsFontBold ? FontStyle.Bold : FontStyle.Regular);
				Rectangle bounds=designerControl.Bounds;
				string typeName=designerControl.TypeName;
				if(typeName=="System.Windows.Forms.Label") {
					if(bounds.Height>=2*font.Height) {
						Size sizeWrapped=TextRenderer.MeasureText(g,text,font,new Size(bounds.Width,int.MaxValue),TextFormatFlags.WordBreak);
						if(sizeWrapped.Height>bounds.Height) {
							listProblems.Add(arrayText[0]+" '"+text+"' needs "+sizeWrapped.Height+" of "+bounds.Height+" high");
						}
						continue;
					}
					int widthText=TextRenderer.MeasureText(g,text,font).Width;
					if(widthText>bounds.Width) {
						listProblems.Add(arrayText[0]+" '"+text+"' needs "+widthText+" of "+bounds.Width);
					}
					continue;
				}
				float widthAvailable;
				if(typeName=="OpenPmsDental.UI.CheckBox") {
					widthAvailable=bounds.Width-12-4;//the 12 pixel box and its gap (UI.CheckBox.OnPaint)
				}
				else if(typeName=="OpenPmsDental.UI.Button") {
					if(designerControl.HasImage) {
						continue;
					}
					widthAvailable=bounds.Width;
				}
				else if(typeName=="OpenPmsDental.UI.GroupBox") {
					widthAvailable=bounds.Width-5;
				}
				else if(typeName=="OpenPmsDental.UI.ComboBox") {
					widthAvailable=bounds.Width-20;//the drop-down arrow
				}
				else {
					continue;//text boxes hold data, radio buttons are wide
				}
				float widthNeeded=g.MeasureString(text,font).Width;
				if(widthNeeded>widthAvailable) {
					listProblems.Add(arrayText[0]+" '"+text+"' needs "+widthNeeded.ToString("0")+" of "+widthAvailable.ToString("0"));
				}
			}
			Assert.AreEqual("",string.Join("; ",listProblems));
		}

		[TestMethod]
		public void WriteOffOverrides_Use25_4Wording() {
			//v25_4-family-writeoff-override-wording: the group keeps its title; the labels read Exclusions, Annual Max, Age or Frequency Limit.
			Assert.AreEqual("Write-Offs for Non-Covered Services Overrides",Model.Get("groupBoxOD1").Text);
			Assert.AreEqual("Exclusions",Model.Get("label11").Text);
			Assert.AreEqual("Annual Max",Model.Get("labelZeroWriteoffAnnualMax").Text);
			Assert.AreEqual("Age or Frequency Limit",Model.Get("label39").Text);
			Assert.AreEqual("groupBoxOD1",Model.Get("comboExclusionFeeRule").ParentName);
			Assert.AreEqual("groupBoxOD1",Model.Get("comboBoxZeroWriteOffsOnAnnualMaxLimitation").ParentName);
			Assert.AreEqual("groupBoxOD1",Model.Get("comboZeroWriteOffsOnLimitationOrAging").ParentName);
		}

		[TestMethod]
		public void NoTextInTheWindow_SaysUcr() {
			//v25_4-misc-ucr-terminology: the window's own text says "standard fee". The member and column names (ClaimsUseUCR) stay.
			List<string> listUcr=Model.DictControls.Values.Where(x => x.Text!=null && x.Text.Contains("UCR")).Select(x => x.Name+": "+x.Text).ToList();
			Assert.AreEqual("",string.Join("; ",listUcr));
			Assert.AreEqual("Claims show standard fee, not billed fee",Model.Get("checkClaimsUseUCR").Text);
		}
	}
}
