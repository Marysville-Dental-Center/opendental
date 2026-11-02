using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using OpenPmsDentalBusiness;
using OpenPmsDental.UI;

namespace OpenPmsDental {
	public partial class FormWikiIncomingLinks:FormODBase {
		public string PageTitle;
		public WikiPage WikiPageJumpTo;
		private List<WikiPage> _listWikiPages;

		public FormWikiIncomingLinks() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormWiki_Load(object sender,EventArgs e) {
			Text="Incoming links to "+PageTitle;
			FillGrid();
			if(_listWikiPages.Count==0) {
				MsgBox.Show(this,"This page has no incoming links.");
				Close();
			}
		}

		private void LoadWikiPage(WikiPage wikiPage) {
			try {
				webViewWiki.NavigateToHtml(MarkupEdit.TranslateToXhtml(wikiPage.PageContent,false),isRemoteContentAllowed:true,
					listFoldersAllowed:MarkupEdit.GetImageFolders(isEmail:false));
			}
			catch(Exception ex) {
				webViewWiki.NavigateToHtml("");
				MessageBox.Show(this,Lan.g(this,"This page is broken and cannot be viewed.  Error message:")+" "+ex.Message);
			}
		}

		/// <summary></summary>
		private void FillGrid() {
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			GridColumn col=new GridColumn(Lan.g(this,"Page Title"),70);
			gridMain.Columns.Add(col);
			//col=new ODGridColumn(Lan.g(this,"Saved"),42);
			//gridMain.Columns.Add(col);
			gridMain.ListGridRows.Clear();
			_listWikiPages=WikiPages.GetIncomingLinks(PageTitle);
			for(int i=0;i<_listWikiPages.Count;i++) {
				GridRow row=new GridRow();
				row.Cells.Add(_listWikiPages[i].PageTitle);
				//row.Cells.Add(page.DateTimeSaved.ToString());
				gridMain.ListGridRows.Add(row);
			}
			gridMain.EndUpdate();
		}

		private void gridMain_Click(object sender,EventArgs e) {
			if(gridMain.SelectedIndices.Length<1) {
				return;
			}
			LoadWikiPage(_listWikiPages[gridMain.SelectedIndices[0]]);
			gridMain.Focus();
		}

		private void gridMain_CellDoubleClick(object sender,UI.ODGridClickEventArgs e) {
			WikiPageJumpTo=_listWikiPages[e.Row];
			DialogResult=DialogResult.OK;
		}

	}
}