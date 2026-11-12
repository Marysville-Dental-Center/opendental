// EFORM-PRINT: printing an eForm from the Fill eForm window. Print (and Save to Images) in FrmEFormFillEdit build a FixedDocument through
// PrinterL.CreateFixedDocument with CtrlEFormFill.Pd_PrintPage as the page function. Before the fix, a row of the fill control whose fields are
// all hidden by conditional logic has a height of 0, and Graphics.DrawFrameworkElement gave RenderTargetBitmap a pixel height of 0, which throws
// ArgumentOutOfRangeException (pixelHeight). These tests build the fill control on an STA thread the way the window does, lay it out without a
// window, and print through the same path. No window is shown and the program is not launched. Synthetic data only.
// The same tests found a second fault on the way: a label with an empty document is written back by FillFieldsFromControls as an unclosed
// <FlowDocument>, and the fill window's next RefreshLayout threw XamlParseException (EFormFields.SerializeFlowDocument).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDental.Drawing;
using OpenPmsDentalBusiness;

namespace EFormPrintTests {
	[TestClass]
	public class EFormPrintTests {
		#region Internal eForms
		///<summary>The D5 steps of the stage 1 window checks: a new eForm from the internal Patient Registration, filled for a patient, printed. Its
		///responsible party and insurance questions are unanswered, so their conditional fields are hidden and their rows have no height.</summary>
		[TestMethod]
		public void PatientRegistration_Prints() {
			EFormPrintDb.Begin();
			PrintResult printResult=StaRunner.Run(() => PrintThroughFill(MakeEForm(EFormInternal.GetEFormDef(EnumEFormInternalType.PatientRegistration))));
			Assert.IsNull(printResult.Exception,"Printing the Patient Registration eForm threw: "+printResult.Exception);
			Assert.IsTrue(printResult.CountRowsZeroHeight>0,"The check needs rows of height 0 (conditionally hidden fields) to mean anything.");
			Assert.IsTrue(printResult.CountPages>=2,"Patient Registration fills more than one page; pages: "+printResult.CountPages);
			Assert.AreEqual(printResult.CountRowsDrawable,printResult.CountImages,"Every visible row is drawn once, and nothing else.");
		}

		///<summary>Every internal eForm (Patient Registration, Medical History, Consent, Dental History, HIPAA) prints.</summary>
		[TestMethod]
		public void EveryInternalEForm_Prints() {
			EFormPrintDb.Begin();
			List<string> listFailures=new List<string>();
			foreach(EnumEFormInternalType eFormInternalType in new[] { EnumEFormInternalType.PatientRegistration,EnumEFormInternalType.MedicalHist,
				EnumEFormInternalType.Consent,EnumEFormInternalType.DentalHist,EnumEFormInternalType.HIPPA })
			{
				PrintResult printResult=StaRunner.Run(() => PrintThroughFill(MakeEForm(EFormInternal.GetEFormDef(eFormInternalType))));
				if(printResult.Exception!=null) {
					listFailures.Add(eFormInternalType+": "+printResult.Exception.GetType().Name+": "+printResult.Exception.Message);
					continue;
				}
				if(printResult.CountPages<1 || printResult.CountImages<1) {
					listFailures.Add(eFormInternalType+": nothing printed ("+printResult.CountPages+" pages, "+printResult.CountImages+" images)");
				}
			}
			Assert.AreEqual(0,listFailures.Count,string.Join("\r\n",listFailures));
		}

		///<summary>Settles the cause: after the print layout (every page shown), a row of the fill control has a height of 0 exactly when all of its
		///fields are hidden by conditional logic, and every row with a field to show has a height. So the zero height comes from the eForm's
		///conditional logic, not from how the control's markup or styles are compiled.</summary>
		[TestMethod]
		public void PatientRegistration_ZeroHeightRowsAreExactlyTheConditionallyHiddenOnes() {
			EFormPrintDb.Begin();
			List<string> listMismatches=StaRunner.Run(() => {
				EForm eForm=MakeEForm(EFormInternal.GetEFormDef(EnumEFormInternalType.PatientRegistration));
				CtrlEFormFill ctrlEFormFill=MakeFill(eForm);
				ctrlEFormFill.FillFieldsFromControls();
				ctrlEFormFill.SetVisibilities(pgRequested:-1,forceRefresh:true);
				StackPanel stackPanel=GetStackPanel(ctrlEFormFill);
				stackPanel.UpdateLayout();
				List<string> listRet=new List<string>();
				int countHidden=0;
				foreach(Grid gridWrap in stackPanel.Children.OfType<Grid>()) {
					List<EFormField> listEFormFields=GetFieldsOfRow(ctrlEFormFill,gridWrap);
					bool isAllHidden=listEFormFields.All(x => x.IsHiddenCondit);
					bool isPageBreak=listEFormFields.All(x => x.FieldType==EnumEFormFieldType.PageBreak);
					if(isAllHidden) {
						countHidden++;
					}
					string description=string.Join(", ",listEFormFields.Select(x => x.FieldType+" '"+x.ValueLabel+"'"));
					if(isAllHidden && gridWrap.ActualHeight!=0) {
						listRet.Add("Hidden row has height "+gridWrap.ActualHeight+": "+description);
					}
					if(!isAllHidden && !isPageBreak && gridWrap.ActualHeight<=0) {
						listRet.Add("Visible row has no height: "+description);
					}
				}
				if(countHidden==0) {
					listRet.Add("No conditionally hidden row: the check needs some.");
				}
				return listRet;
			});
			Assert.AreEqual(0,listMismatches.Count,string.Join("\r\n",listMismatches));
		}
		#endregion Internal eForms

		#region Hidden and empty fields
		///<summary>A synthetic eForm with a conditionally hidden text field, a conditionally hidden row of two fields, an empty label without a border,
		///a page break and fields after it prints, and only its visible rows are drawn.</summary>
		[TestMethod]
		public void HiddenAndEmptyFields_Print() {
			EFormPrintDb.Begin();
			PrintResult printResult=StaRunner.Run(() => PrintThroughFill(MakeEForm(MakeDefHiddenAndEmpty(withSigBoxes:false))));
			Assert.IsNull(printResult.Exception,"Printing the eForm with hidden and empty fields threw: "+printResult.Exception);
			Assert.IsTrue(printResult.CountRowsZeroHeight>=2,"The hidden rows have no height; rows of height 0: "+printResult.CountRowsZeroHeight);
			Assert.AreEqual(1,printResult.CountPages);
			Assert.AreEqual(printResult.CountRowsDrawable,printResult.CountImages,"Every visible row is drawn once, and nothing else.");
		}

		///<summary>The same with signature boxes: one hidden by conditional logic and one with an empty label. A signature box prints its label and its
		///signature image.</summary>
		[TestMethod]
		public void HiddenAndEmptySignatureBoxes_Print() {
			EFormPrintDb.Begin();
			PrintResult printResult=StaRunner.Run(() => PrintThroughFill(MakeEForm(MakeDefHiddenAndEmpty(withSigBoxes:true))));
			Assert.IsNull(printResult.Exception,"Printing the eForm with hidden and empty signature boxes threw: "+printResult.Exception);
			Assert.IsTrue(printResult.CountPages>=1);
			Assert.IsTrue(printResult.CountImages>=printResult.CountRowsDrawable,"Every visible row is drawn.");
		}

		///<summary>An eForm whose fields are all hidden on the second page prints that page empty instead of throwing.</summary>
		[TestMethod]
		public void PageOfHiddenFieldsOnly_Prints() {
			EFormPrintDb.Begin();
			EFormDef eFormDef=MakeDef("Synthetic hidden page");
			AddField(eFormDef,EnumEFormFieldType.RadioButtons,"Do you have a pet?",pickListVis:"Yes|No",pickListDb:"|");
			AddField(eFormDef,EnumEFormFieldType.PageBreak,"");
			AddField(eFormDef,EnumEFormFieldType.TextField,"Pet name",conditionalParent:"Do you have a pet?",conditionalValue:"Yes");
			AddField(eFormDef,EnumEFormFieldType.TextField,"Pet kind",conditionalParent:"Do you have a pet?",conditionalValue:"Yes");
			PrintResult printResult=StaRunner.Run(() => PrintThroughFill(MakeEForm(eFormDef)));
			Assert.IsNull(printResult.Exception,"Printing an eForm with a page of hidden fields threw: "+printResult.Exception);
			Assert.AreEqual(printResult.CountRowsDrawable,printResult.CountImages);
		}
		#endregion Hidden and empty fields

		#region Graphics.DrawFrameworkElement
		///<summary>The other callers of Graphics.DrawFrameworkElement are covered by the method itself: an element of width or height 0 draws nothing
		///instead of throwing, and a sized one still draws.</summary>
		[TestMethod]
		public void DrawFrameworkElement_ZeroSize_DrawsNothing() {
			StaRunner.Run(() => {
				Canvas canvas=new Canvas { Width=700,Height=900 };
				Graphics g=Graphics.PrinterInit(canvas);
				foreach(Size size in new[] { new Size(0,0),new Size(100,0),new Size(0,40) }) {
					Border border=new Border { Width=size.Width,Height=size.Height };
					border.Measure(size);
					border.Arrange(new Rect(size));
					g.DrawFrameworkElement(border,0,0);
				}
				Border borderCollapsed=new Border { Width=100,Height=40,Visibility=Visibility.Collapsed };
				borderCollapsed.Measure(new Size(100,40));
				borderCollapsed.Arrange(new Rect(new Size(100,40)));
				g.DrawFrameworkElement(borderCollapsed,0,0);
				Assert.AreEqual(0,canvas.Children.Count,"Nothing is drawn for an element without width or height.");
				Border borderSized=new Border { Width=100,Height=40,Background=System.Windows.Media.Brushes.Black };
				borderSized.Measure(new Size(100,40));
				borderSized.Arrange(new Rect(new Size(100,40)));
				g.DrawFrameworkElement(borderSized,0,0);
				Assert.AreEqual(1,canvas.Children.Count,"A sized element is drawn.");
			});
		}
		#endregion Graphics.DrawFrameworkElement

		#region Empty label
		///<summary>A label whose document is empty (a label field with no text yet, ValueLabel "") is written back by FillFieldsFromControls through
		///SerializeFlowDocument. The empty document must come back as a document that loads again: before the fix, the self-closing
		///&lt;FlowDocument ... /&gt; lost its slash and the fill window's next RefreshLayout threw XamlParseException.</summary>
		[TestMethod]
		public void SerializeFlowDocument_EmptyDocument_RoundTrips() {
			StaRunner.Run(() => {
				string xmlEmpty=EFormFields.SerializeFlowDocument(new FlowDocument());
				FlowDocument flowDocument=EFormFields.DeserializeFlowDocument(xmlEmpty);
				Assert.AreEqual(0,flowDocument.Blocks.Count,"Serialized: "+xmlEmpty);
				FlowDocument flowDocumentText=new FlowDocument(new Paragraph(new Run("Synthetic text")));
				string xmlText=EFormFields.SerializeFlowDocument(flowDocumentText);
				StringAssert.StartsWith(xmlText,"<FlowDocument>");
				FlowDocument flowDocumentBack=EFormFields.DeserializeFlowDocument(xmlText);
				Assert.AreEqual("Synthetic text",new TextRange(flowDocumentBack.ContentStart,flowDocumentBack.ContentEnd).Text.Trim());
			});
		}
		#endregion Empty label

		#region Helpers
		///<summary>What printing an eForm gave. Exception is null when printing worked.</summary>
		public class PrintResult {
			public Exception Exception;
			public int CountPages;
			///<summary>Images on all pages: one per drawn row, two (label and signature) per signature box.</summary>
			public int CountImages;
			///<summary>Rows of the fill control (field rows, not page breaks) that have a size to draw, after the print layout.</summary>
			public int CountRowsDrawable;
			public int CountRowsZeroHeight;
		}

		///<summary>A new eForm from the def for the synthetic patient, filled from the database, as Forms > Add eForm does (FormPatientForms).</summary>
		private static EForm MakeEForm(EFormDef eFormDef) {
			EForm eForm=EForms.CreateEFormFromEFormDef(eFormDef,EFormPrintDb.PatNum,EnumEFormStatus.None);
			eForm.DateTimeShown=DateTime.Now;
			EFormFiller.FillFields(eForm);
			return eForm;
		}

		///<summary>The fill control as FrmEFormFillEdit sets it up on Load and sizes it (SetCtrlWidth), laid out without a window.</summary>
		private static CtrlEFormFill MakeFill(EForm eForm) {
			CtrlEFormFill ctrlEFormFill=new CtrlEFormFill();
			ctrlEFormFill.ListEFormFields=eForm.ListEFormFields;
			ctrlEFormFill.PatNum=eForm.PatNum;
			ctrlEFormFill.ShowLabelsBold=eForm.ShowLabelsBold;
			ctrlEFormFill.SpaceBelowEachField=eForm.SpaceBelowEachField;
			ctrlEFormFill.SpaceToRightEachField=eForm.SpaceToRightEachField;
			ctrlEFormFill.Zoom=1;
			double width=eForm.MaxWidth+17+2;//scrollbar plus border width, as SetCtrlWidth
			double height=650;
			ctrlEFormFill.Width=width;
			ctrlEFormFill.Height=height;
			//The root of a tree without a window: one measure and arrange gives the layout manager the size for later UpdateLayout calls.
			ctrlEFormFill.Measure(new Size(width,height));
			ctrlEFormFill.Arrange(new Rect(new Size(width,height)));
			ctrlEFormFill.UpdateLayout();
			ctrlEFormFill.RefreshLayout();//Load
			ctrlEFormFill.UpdateLayout();//SetCtrlWidth
			ctrlEFormFill.FillFieldsFromControls();
			ctrlEFormFill.RefreshLayout();
			return ctrlEFormFill;
		}

		///<summary>Builds the fill control and prints it the way FrmEFormFillEdit's Print does, into a FixedDocument (PrinterL.CreateFixedDocument, the
		///document of the print preview). Runs on an STA thread.</summary>
		private static PrintResult PrintThroughFill(EForm eForm) {
			PrintResult printResult=new PrintResult();
			CtrlEFormFill ctrlEFormFill=MakeFill(eForm);
			//butPrint_Click
			ctrlEFormFill.FillFieldsFromControls();
			Printout printout=new Printout();
			printout.FuncPrintPage=ctrlEFormFill.Pd_PrintPage;
			printout.thicknessMarginInches=new Thickness(0.5);
			ctrlEFormFill.PagesPrinted=0;
			try {
				WpfControls.PrinterL.CreateFixedDocument(printout);
			}
			catch(Exception ex) {
				printResult.Exception=ex;
				return printResult;
			}
			//Counted in the print layout (every page shown, conditional fields hidden), before RefreshLayout puts back the page showing.
			StackPanel stackPanel=GetStackPanel(ctrlEFormFill);
			foreach(Grid gridWrap in stackPanel.Children.OfType<Grid>()) {
				List<EFormField> listEFormFields=GetFieldsOfRow(ctrlEFormFill,gridWrap);
				if(listEFormFields.All(x => x.FieldType==EnumEFormFieldType.PageBreak)) {
					continue;
				}
				if(gridWrap.ActualHeight<=0 || gridWrap.ActualWidth<=0) {
					printResult.CountRowsZeroHeight++;
					continue;
				}
				printResult.CountRowsDrawable++;
			}
			FixedDocument fixedDocument=printout.FixedDocument_;
			printResult.CountPages=fixedDocument.Pages.Count;
			foreach(PageContent pageContent in fixedDocument.Pages) {
				foreach(Canvas canvas in pageContent.Child.Children.OfType<Canvas>()) {
					printResult.CountImages+=canvas.Children.OfType<System.Windows.Controls.Image>().Count();
				}
			}
			ctrlEFormFill.RefreshLayout();//as butPrint_Click does after the preview
			return printResult;
		}

		private static StackPanel GetStackPanel(CtrlEFormFill ctrlEFormFill) {
			return (StackPanel)ctrlEFormFill.FindName("stackPanel");
		}

		///<summary>The fields of one row (gridWrap) of the fill control: its wrap panel's field grids, each holding the field's border box.</summary>
		private static List<EFormField> GetFieldsOfRow(CtrlEFormFill ctrlEFormFill,Grid gridWrap) {
			WrapPanel wrapPanel=(WrapPanel)gridWrap.Children[1];
			List<EFormField> listEFormFields=new List<EFormField>();
			foreach(Grid gridForField in wrapPanel.Children.OfType<Grid>()) {
				Border borderBox=(Border)gridForField.Children[1];
				listEFormFields.Add(ctrlEFormFill.ListEFormFields.Find(x => x.TagOD==borderBox));
			}
			return listEFormFields;
		}

		private static EFormDef MakeDef(string description) {
			EFormDef eFormDef=new EFormDef();
			eFormDef.FormType=EnumEFormType.PatientForm;
			eFormDef.Description=description;
			eFormDef.MaxWidth=600;
			eFormDef.ShowLabelsBold=true;
			eFormDef.SpaceBelowEachField=20;
			eFormDef.SpaceToRightEachField=20;
			eFormDef.ListEFormFieldDefs=new List<EFormFieldDef>();
			return eFormDef;
		}

		private static EFormFieldDef AddField(EFormDef eFormDef,EnumEFormFieldType fieldType,string valueLabel,string pickListVis="",string pickListDb="",
			string conditionalParent="",string conditionalValue="",bool isHorizStacking=false,EnumEFormBorder border=EnumEFormBorder.ThreeD,int spaceBelow=-1)
		{
			EFormFieldDef eFormFieldDef=new EFormFieldDef();
			eFormFieldDef.FieldType=fieldType;
			eFormFieldDef.ValueLabel=valueLabel;
			eFormFieldDef.ItemOrder=eFormDef.ListEFormFieldDefs.Count;
			eFormFieldDef.PickListVis=pickListVis;
			eFormFieldDef.PickListDb=pickListDb;
			eFormFieldDef.IsHorizStacking=isHorizStacking;
			eFormFieldDef.FontScale=100;
			eFormFieldDef.ConditionalParent=conditionalParent;
			eFormFieldDef.ConditionalValue=conditionalValue;
			eFormFieldDef.Border=border;
			eFormFieldDef.SpaceBelow=spaceBelow;
			eFormDef.ListEFormFieldDefs.Add(eFormFieldDef);
			return eFormFieldDef;
		}

		///<summary>A label, a question whose children are hidden while it is unanswered (a text field, and a row of a text field and a date field),
		///an empty label without a border, and a text field; with signature boxes, also a hidden signature box and one with an empty label.</summary>
		private static EFormDef MakeDefHiddenAndEmpty(bool withSigBoxes) {
			EFormDef eFormDef=MakeDef("Synthetic hidden and empty fields");
			AddField(eFormDef,EnumEFormFieldType.Label,
				"<FlowDocument><Paragraph><Run FontWeight=\"Bold\">Synthetic print check</Run></Paragraph></FlowDocument>",border:EnumEFormBorder.None);
			AddField(eFormDef,EnumEFormFieldType.RadioButtons,"Any allergies?",pickListVis:"Yes|No",pickListDb:"|");
			AddField(eFormDef,EnumEFormFieldType.TextField,"Allergy details",conditionalParent:"Any allergies?",conditionalValue:"Yes");
			AddField(eFormDef,EnumEFormFieldType.TextField,"Reaction",conditionalParent:"Any allergies?",conditionalValue:"Yes");
			AddField(eFormDef,EnumEFormFieldType.DateField,"Since",conditionalParent:"Any allergies?",conditionalValue:"Yes",isHorizStacking:true);
			AddField(eFormDef,EnumEFormFieldType.Label,"",border:EnumEFormBorder.None,spaceBelow:0);
			AddField(eFormDef,EnumEFormFieldType.TextField,"Notes");
			if(withSigBoxes) {
				AddField(eFormDef,EnumEFormFieldType.SigBox,"Allergy signature",conditionalParent:"Any allergies?",conditionalValue:"Yes");
				AddField(eFormDef,EnumEFormFieldType.SigBox,"");
			}
			return eFormDef;
		}
		#endregion Helpers
	}
}
