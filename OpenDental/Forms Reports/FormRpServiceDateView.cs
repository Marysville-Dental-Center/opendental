using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using CodeBase;
using OpenDental.UI;
using OpenDentBusiness;
using PdfSharp.Pdf;

namespace OpenDental {
	///<summary>Service Date View: each charge with the credits applied to it beneath, grouped by date of service. Modeless. Since Open Dental 25.4
	///(package M2-SDV) the window follows the Account module's selected patient and Family/Patient choice and refreshes in place (SetPatient),
	///remembers "Show detailed view" per user, and takes its columns from Display Fields, Account Module Service Date View.</summary>
	public partial class FormRpServiceDateView:FormODBase {
		#region Public Variables
		///<summary>This will be the PatNum or the Guarantor's PatNum.</summary>
		public long PatNum { get; private set; }
		///<summary>Whether or not the window is displaying results for the entire family.</summary>
		public bool IsFamily { get; private set; }
		#endregion
		#region Private Variables
		private bool _headingPrinted;
		private int _pagesPrinted;
		private int _headingPrintH;
		private Family _fam;
		#endregion

		public FormRpServiceDateView(long patNum,bool isFamily) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			PatNum=patNum;
			IsFamily=isFamily;
			_fam=Patients.GetFamily(patNum);
		}

		private void FormRpServiceDate_Load(object sender,EventArgs e) {
			checkDetailedView.Checked=ServiceDateViewUserPrefs.GetBool(Security.CurUser.UserNum,ServiceDateViewUserPrefs.FKEY_SHOW_DETAILS,true);
			checkDetailedView.Click+=checkDetailedView_Click;
			FillGrid();
			SetTitle();
		}

		///<summary>Shows another patient or family, or the same one again with fresh data, without closing the window.</summary>
		public void SetPatient(long patNum,bool isFamily) {
			if(IsDisposed) {
				return;
			}
			PatNum=patNum;
			IsFamily=isFamily;
			_fam=Patients.GetFamily(patNum);
			SetTitle();
			FillGrid();
		}

		private void SetTitle() {
			Patient patient=_fam.GetPatient(PatNum);
			string name=patient==null ? "" : patient.GetNameFL();
			Text=Lans.g(this,"Service Date View -")+" "+name+(IsFamily ? " "+Lans.g(this,"(Family)") : "");
		}

		private void FillGrid() {
			ServiceDateViewOptions options=new ServiceDateViewOptions();
			options.IsDetailed=checkDetailedView.Checked;
			List<ServiceDateViewRow> listRows=RpServiceDateView.GetRows(PatNum,IsFamily,options);
			List<DisplayField> listDisplayFields=ServiceDateViewGrid.GetDisplayFields();
			gridMain.BeginUpdate();
			ServiceDateViewGrid.FillColumns(gridMain,listDisplayFields);
			gridMain.ListGridRows.Clear();
			for(int i=0;i<listRows.Count;i++) {
				gridMain.ListGridRows.Add(ServiceDateViewGrid.MakeGridRow(listRows[i],listDisplayFields));
			}
			gridMain.EndUpdate();
		}

		private void checkDetailedView_Click(object sender,EventArgs e) {
			ServiceDateViewUserPrefs.SetBool(Security.CurUser.UserNum,ServiceDateViewUserPrefs.FKEY_SHOW_DETAILS,checkDetailedView.Checked);
			DataValid.SetInvalid(InvalidType.UserOdPrefs);
			FillGrid();
		}

		private void butRefresh_Click(object sender,EventArgs e) {
			FillGrid();
		}

		private void butSetupColumns_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.Setup)) {
				return;
			}
			using FormDisplayFields formDisplayFields=new FormDisplayFields();
			formDisplayFields.DisplayFieldCategoryCur=DisplayFieldCategory.AccountModuleServiceDateView;
			formDisplayFields.ShowDialog();
			if(formDisplayFields.DialogResult!=DialogResult.OK) {
				return;
			}
			SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,"Display Fields");
			FillGrid();
		}

		private void butSavePDFToImages_Click(object sender,EventArgs e) {
			if(gridMain.ListGridRows.Count==0) {
				MsgBox.Show(this,"Grid is empty.");
				return;
			}
			//Get image category to save to. First image "Statement(S)" category.
			List<Def> listImageCatDefs=Defs.GetDefsForCategory(DefCat.ImageCats,true).Where(x => x.ItemValue.Contains("S")).ToList();
			if(listImageCatDefs.IsNullOrEmpty()) {
				MsgBox.Show(this,"No image category set for Statements.");
				return;
			}
			string tempFile=PrefC.GetRandomTempFile(".pdf");
			CreatePDF(tempFile);
			Patient patCur=_fam.GetPatient(PatNum);
			string rawBase64="";
			if(PrefC.AtoZfolderUsed==DataStorageType.InDatabase) {
				rawBase64=Convert.ToBase64String(File.ReadAllBytes(tempFile));
			}
			Document docSave=new Document();
			docSave.DocNum=Documents.Insert(docSave);
			docSave.ImgType=ImageType.Document;
			docSave.DateCreated=DateTime.Now;
			docSave.PatNum=PatNum;
			docSave.DocCategory=listImageCatDefs.FirstOrDefault().DefNum;
			docSave.Description=$"ServiceDateView"+docSave.DocNum+$"{docSave.DateCreated.Year}_{docSave.DateCreated.Month}_{docSave.DateCreated.Day}";
			docSave.RawBase64=rawBase64;//blank if using AtoZfolder
			string fileName=ODFileUtils.CleanFileName(docSave.Description);
			string filePath=ImageStore.GetPatientFolder(patCur,ImageStore.GetPreferredAtoZpath());
			while(FileAtoZ.Exists(FileAtoZ.CombinePaths(filePath,fileName+".pdf"))) {
				fileName+="x";
			}
			FileAtoZ.Copy(tempFile,ODFileUtils.CombinePaths(filePath,fileName+".pdf"),FileAtoZSourceDestination.LocalToAtoZ);
			docSave.FileName=fileName+".pdf";//file extension used for both DB images and AtoZ images
			Documents.Update(docSave);
			try {
				File.Delete(tempFile); //cleanup the temp file.
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			MsgBox.Show(this,"PDF saved successfully.");
		}

		private void CreatePDF(string tempFile) {
			MigraDoc.Rendering.PdfDocumentRenderer pdfRenderer=new MigraDoc.Rendering.PdfDocumentRenderer(true,PdfFontEmbedding.Always);
			pdfRenderer.Document=CreateDocument();
			pdfRenderer.RenderDocument();
			pdfRenderer.PdfDocument.Save(tempFile);
		}

		private MigraDoc.DocumentObjectModel.Document CreateDocument() {
			MigraDoc.DocumentObjectModel.Document doc= new MigraDoc.DocumentObjectModel.Document();
			doc.DefaultPageSetup.PageWidth=MigraDoc.DocumentObjectModel.Unit.FromInch(8.5);
			doc.DefaultPageSetup.PageHeight=MigraDoc.DocumentObjectModel.Unit.FromInch(11);
			doc.DefaultPageSetup.TopMargin=MigraDoc.DocumentObjectModel.Unit.FromInch(.5);
			doc.DefaultPageSetup.LeftMargin=MigraDoc.DocumentObjectModel.Unit.FromInch(.5);
			doc.DefaultPageSetup.RightMargin=MigraDoc.DocumentObjectModel.Unit.FromInch(.5);
			MigraDoc.DocumentObjectModel.Section section=doc.AddSection();
			MigraDoc.DocumentObjectModel.Font headingFont=MigraDocHelper.CreateFont(13,true);
			MigraDoc.DocumentObjectModel.Font subHeadingFont=MigraDocHelper.CreateFont(10,true);
			#region printHeading
			//Heading---------------------------------------------------------------------------------------------------------------
			MigraDoc.DocumentObjectModel.Paragraph par=section.AddParagraph();
			MigraDoc.DocumentObjectModel.ParagraphFormat parformat=new MigraDoc.DocumentObjectModel.ParagraphFormat();
			parformat.Alignment=MigraDoc.DocumentObjectModel.ParagraphAlignment.Center;
			par.Format=parformat;
			string text=Lans.g(this,"Service Date View");
			par.AddFormattedText(text,headingFont);
			par.AddLineBreak();
			//SubHeading---------------------------------------------------------------------------------------------------------------
			text=(IsFamily ? Lans.g(this,"Entire Family:")+" " : "")+$"{_fam.GetNameInFamFL(PatNum)}";
			par.AddFormattedText(text,subHeadingFont);
			par.AddLineBreak();
			text=Lans.g(this,"Date")+" "+DateTime.Now.ToShortDateString();
			par.AddFormattedText(text,subHeadingFont);
			#endregion
			MigraDocHelper.InsertSpacer(section,10);
			section.PageSetup.Orientation=MigraDoc.DocumentObjectModel.Orientation.Landscape;
			MigraDocHelper.DrawGrid(section,gridMain);
			return doc;
		}

		private void butPrint_Click(object sender,EventArgs e) {
			if(gridMain.ListGridRows.Count==0) {
				MsgBox.Show(this,"Grid is empty.");
				return;
			}
			_pagesPrinted=0;
			_headingPrinted=false;
			PrinterL.TryPrintOrDebugRpPreview(pd_PrintPage,Lan.g(this,"Service date view printed"),PrintoutOrientation.Landscape);
		}

		private void pd_PrintPage(object sender,PrintPageEventArgs e) {
			Rectangle bounds=e.MarginBounds;
			Graphics g=e.Graphics;
			string text;
			System.Drawing.Font headingFont=new System.Drawing.Font("Arial",13,FontStyle.Bold);
			System.Drawing.Font subHeadingFont=new System.Drawing.Font("Arial",10,FontStyle.Bold);
			int yPos=bounds.Top;
			int center=bounds.X+bounds.Width/2;
			#region printHeading
			if(!_headingPrinted) {
				text=Lan.g(this,"Service Date View");
				g.DrawString(text,headingFont,Brushes.Black,center-g.MeasureString(text,headingFont).Width/2,yPos);
				yPos+=(int)g.MeasureString(text,headingFont).Height;
				text=(IsFamily ? Lans.g(this,"Entire Family:")+" " : "")+$"{_fam.GetNameInFamFL(PatNum)}";
				g.DrawString(text,subHeadingFont,Brushes.Black,center-g.MeasureString(text,subHeadingFont).Width/2,yPos);
				yPos+=(int)g.MeasureString(text,subHeadingFont).Height;
				text=DateTime.Now.ToShortDateString();
				g.DrawString(text,subHeadingFont,Brushes.Black,center-g.MeasureString(text,subHeadingFont).Width/2,yPos);
				yPos+=20;
				_headingPrinted=true;
				_headingPrintH=yPos;
			}
			#endregion
			yPos=gridMain.PrintPage(g,_pagesPrinted,bounds,_headingPrintH);
			_pagesPrinted++;
			if(yPos==-1) {
				e.HasMorePages=true;
			}
			else {
				e.HasMorePages=false;
			}
			g.Dispose();
		}

	}

	///<summary>Columns and cells of a service-date grid: the Service Date View window and the Account module's main grid in service-date view. The
	///columns come from Display Fields, Account Module Service Date View (category 22); the internal names are the 24.3 window's column titles.</summary>
	public static class ServiceDateViewGrid {
		///<summary>Display fields of category 22, without Clinic when clinics are off.</summary>
		public static List<DisplayField> GetDisplayFields() {
			List<DisplayField> listDisplayFields=DisplayFields.GetForCategory(DisplayFieldCategory.AccountModuleServiceDateView);
			if(!PrefC.HasClinicsEnabled) {
				listDisplayFields.RemoveAll(x => x.InternalName=="Clinic");
			}
			return listDisplayFields;
		}

		///<summary>Replaces the grid's columns. Call between BeginUpdate and EndUpdate.</summary>
		public static void FillColumns(GridOD grid,List<DisplayField> listDisplayFields) {
			grid.Columns.Clear();
			for(int i=0;i<listDisplayFields.Count;i++) {
				string text=listDisplayFields[i].Description;
				if(string.IsNullOrEmpty(text)) {
					text=Lan.g("TableServiceView",listDisplayFields[i].InternalName);
				}
				HorizontalAlignment horizontalAlignment=HorizontalAlignment.Left;
				if(listDisplayFields[i].InternalName.In("Charge","Production","InsBal","AcctBal")) {
					horizontalAlignment=HorizontalAlignment.Right;
				}
				grid.Columns.Add(new GridColumn(text,listDisplayFields[i].ColumnWidth,horizontalAlignment));
			}
		}

		///<summary>A grid row for the display fields; Tag is the ServiceDateViewRow. Totals are bold.</summary>
		public static GridRow MakeGridRow(ServiceDateViewRow row,List<DisplayField> listDisplayFields) {
			GridRow gridRow=new GridRow();
			for(int i=0;i<listDisplayFields.Count;i++) {
				gridRow.Cells.Add(GetCellText(row,listDisplayFields[i].InternalName));
			}
			gridRow.Tag=row;
			if(row.IsTotal) {
				gridRow.Bold=true;
			}
			return gridRow;
		}

		///<summary>The text of one cell. Tooth numbers are converted to the office's nomenclature here, not in the query.</summary>
		public static string GetCellText(ServiceDateViewRow row,string internalName) {
			bool isTransaction=row.RowKind==ServiceDateViewRowKind.Transaction;
			bool hasAmounts=row.RowKind!=ServiceDateViewRowKind.UnallocatedHeading && row.RowKind!=ServiceDateViewRowKind.BalanceForward;
			switch(internalName) {
				case "Service Date":
					if(row.IsProcedure || row.RowKind==ServiceDateViewRowKind.UnallocatedHeading || row.RowKind==ServiceDateViewRowKind.DayTotal) {
						return row.DateService.Year<1880 ? "" : row.DateService.ToShortDateString();
					}
					return "";
				case "Trans Date":
					if(isTransaction && !row.IsProcedure && row.DateTrans.Year>=1880) {
						return row.DateTrans.ToShortDateString();
					}
					return "";
				case "Patient":
					return isTransaction ? row.PatientName : "";
				case "Reference":
					return GetReferenceText(row);
				case "Charge":
					return hasAmounts ? row.Charge.ToString("f") : "";
				case "Production":
					return hasAmounts ? row.Credit.ToString("f") : "";
				case "Prov":
					return isTransaction ? row.ProvAbbr : "";
				case "InsBal":
					return row.ShowsInsAndAcctBal ? row.InsBal.ToString("f") : "";
				case "AcctBal":
					return row.ShowsInsAndAcctBal ? row.AcctBal.ToString("f") : "";
				case "Clinic":
					return isTransaction ? Clinics.GetAbbr(row.ClinicNum) : "";
			}
			return "";
		}

		private static string GetReferenceText(ServiceDateViewRow row) {
			switch(row.RowKind) {
				case ServiceDateViewRowKind.UnallocatedHeading:
				case ServiceDateViewRowKind.DayTotal:
				case ServiceDateViewRowKind.OverallTotal:
					return Lan.g("TableServiceView",row.Reference);
				case ServiceDateViewRowKind.BalanceForward:
					return Lan.g("AccountModule","Balance Forward");
			}
			string reference=row.Reference;
			int toothNumPref=PrefC.GetInt(PrefName.UseInternationalToothNumbers);
			//Replace toothNum with correct nomenclature when not using American system.
			if(row.IsProcedure && toothNumPref!=0 && reference.Contains('#')) {
				int idxStart=reference.IndexOf('#')+1;
				int idxEnd=reference.IndexOf('-',idxStart);
				if(idxEnd>idxStart) {
					string toothNum=reference.Substring(idxStart,idxEnd-idxStart);
					string toothNumNom=Tooth.Display(toothNum,(ToothNumberingNomenclature)toothNumPref);
					reference=reference.Replace("#"+toothNum+"-","#"+toothNumNom+"-");
				}
			}
			if(reference=="PayPlan Credit") {//Changed here and not in the query, as in 24.3.
				reference="PayPlan Production";
			}
			return reference;
		}
	}
}
