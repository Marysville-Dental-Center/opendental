using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using OpenPmsDentalBusiness;
using OpenPmsDental.UI;
using CodeBase;

namespace OpenPmsDental {
	public partial class FormWikiDrafts:FormODBase {
		private List<WikiPage> _listWikiPages;
		public FormWiki FormWiki_;
		///<summary>True when the page is locked and this user lacks Wiki Admin: drafts can be viewed and compared, not edited or deleted.</summary>
		private bool _isReadOnly;

		public FormWikiDrafts() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormWikiDrafts_Load(object sender,EventArgs e) {
			ResizeControls();
			FillGrid();
			gridMain.SetSelected(gridMain.ListGridRows.Count-1,true);//select most recent draft
			LoadWikiPage(_listWikiPages[gridMain.SelectedIndices[0]]);
			Text=Lan.g(this,"Wiki Drafts")+" - "+FormWiki_.WikiPageCur.PageTitle;
			//Viewing and comparing need only wiki access. The Wiki Admin rule for locked pages applies to editing (v25_1-misc-wiki-draft-compare).
			_isReadOnly=FormWiki_.WikiPageCur.IsLocked && !Security.IsAuthorized(EnumPermType.WikiAdmin,true);
			butEdit.Enabled=!_isReadOnly;
			butDelete.Enabled=!_isReadOnly;
			labelNotAuthorized.Visible=_isReadOnly;
		}

		private void butCompare_Click(object sender,EventArgs e) {
			if(gridMain.SelectedIndices.Length!=1) {
				MsgBox.Show(this,"Select one draft to compare with the published page.");
				return;
			}
			WikiPage wikiPageDraft=_listWikiPages[gridMain.SelectedIndices[0]];
			bool isFromHistory;
			WikiPageHist wikiPageHistPublished=WikiPages.GetPublishedForDraftCompare(wikiPageDraft.PageTitle,out isFromHistory);
			string captionLeft;
			if(wikiPageHistPublished==null) {
				captionLeft=Lan.g(this,"No published version of this page");
				wikiPageHistPublished=new WikiPageHist();
				wikiPageHistPublished.PageTitle=wikiPageDraft.PageTitle;
				wikiPageHistPublished.PageContent="";
			}
			else if(isFromHistory) {
				captionLeft=Lan.g(this,"Last history version (the page is archived)")+": "+wikiPageHistPublished.DateTimeSaved.ToString();
			}
			else {
				captionLeft=Lan.g(this,"Published page")+": "+wikiPageHistPublished.DateTimeSaved.ToString();
			}
			string captionRight=Lan.g(this,"Draft by")+" "+Userods.GetName(wikiPageDraft.UserNum)+": "+wikiPageDraft.DateTimeSaved.ToString();
			using FormWikiCompare formWikiCompare=new FormWikiCompare();
			formWikiCompare.InitializeRevisionsSelected(wikiPageHistPublished,WikiPages.PageToHist(wikiPageDraft),captionLeft,captionRight,
				Lan.g(this,"Published (removed or moved lines)."),Lan.g(this,"Draft (added or moved lines)."));
			formWikiCompare.ShowDialog();
			gridMain.Focus();
		}

		///<summary>Resize text boxes to each occupy ~1/2 of screen from top to bottom.</summary>
		private void ResizeControls() {
			//assuming gridMain, textNumbers do not change width or location.
			Rectangle rectangleWorkingArea=new Rectangle(294,12,ClientSize.Width-397,ClientSize.Height-24);
			//textNumbers resize
			LayoutManager.MoveHeight(textNumbers,rectangleWorkingArea.Height);
			//text resize
			LayoutManager.MoveLocation(textContent,new Point(textContent.Location.X,rectangleWorkingArea.Top));
			LayoutManager.MoveHeight(textContent,rectangleWorkingArea.Height);
			LayoutManager.MoveLocation(textContent,new Point(rectangleWorkingArea.Left,textContent.Location.Y));
			LayoutManager.MoveWidth(textContent,rectangleWorkingArea.Width/2-2);
			//Browser resize
			LayoutManager.MoveLocation(webViewWiki,new Point(webViewWiki.Location.X,rectangleWorkingArea.Top));
			LayoutManager.MoveHeight(webViewWiki,rectangleWorkingArea.Height);
			LayoutManager.MoveLocation(webViewWiki,new Point(rectangleWorkingArea.Left+rectangleWorkingArea.Width/2+2,webViewWiki.Location.Y));
			LayoutManager.MoveWidth(webViewWiki,rectangleWorkingArea.Width/2-2);
		}

		private void LoadWikiPage(WikiPage wikiPage) {
			try {
				textContent.Text=WikiPages.GetWikiPageContentWithWikiPageTitles(wikiPage.PageContent);
				webViewWiki.NavigateToHtml(MarkupEdit.TranslateToXhtml(textContent.Text,false,hasWikiPageTitles: true),isRemoteContentAllowed:true,
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
			GridColumn col=new GridColumn(Lan.g(this,"User"),70);
			gridMain.Columns.Add(col);
			col=new GridColumn(Lan.g(this,"Last Saved"),80);
			gridMain.Columns.Add(col);
			gridMain.ListGridRows.Clear();
			_listWikiPages=WikiPages.GetDraftsByTitle(FormWiki_.WikiPageCur.PageTitle);
			for(int i=0;i<_listWikiPages.Count;i++) {
				GridRow row=new GridRow();
				row.Cells.Add(Userods.GetName(_listWikiPages[i].UserNum));
				row.Cells.Add(_listWikiPages[i].DateTimeSaved.ToString());
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
			EditWikiDraft();
		}

		private void butEdit_Click(object sender,EventArgs e) {
			EditWikiDraft();
		}

		private void EditWikiDraft() {
			if(_isReadOnly) {
				MsgBox.Show(this,"This wiki page is locked and cannot be edited without the Wiki Admin security permission.");
				return;
			}
			int idxSelected=gridMain.GetSelectedIndex();
			using FormWikiEdit formWikiEdit=new FormWikiEdit();
			formWikiEdit.WikiPageCur=_listWikiPages[idxSelected];
			formWikiEdit.FormWiki_=this.FormWiki_;
			formWikiEdit.ShowDialog();
			if(formWikiEdit.HasSaved) {
				Close();
				return;
			}
			FillGrid();
			gridMain.SetSelected(idxSelected,true);
			LoadWikiPage(_listWikiPages[idxSelected]);
			gridMain.Focus();
		}

		private void butDelete_Click(object sender,EventArgs e) {
			if(gridMain.GetSelectedIndex()<0) {
				return;
			}
			int idxSelected=gridMain.GetSelectedIndex();
			if(!MsgBox.Show(this,MsgBoxButtons.YesNo,"Delete this draft?")) {
				return;
			}
			try {
				WikiPages.DeleteDraft(_listWikiPages[idxSelected]);
			}
			catch (Exception ex){
				//should never happen because we are only ever editing drafts here.
				MessageBox.Show(ex.Message);
				return;
			}
			//deleting Edge cases.
			FillGrid();
			if(idxSelected>=_listWikiPages.Count) {
				idxSelected--;
			}
			if(_listWikiPages.Count<1) {
				//Nothing else a user could possibly do when there are no drafts. Exit.
				Close();
				return;
			}
			gridMain.SetSelected(idxSelected,true);
			LoadWikiPage(_listWikiPages[idxSelected]);
			gridMain.Focus();
		}

	}
}