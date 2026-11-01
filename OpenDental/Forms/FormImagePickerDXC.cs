using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using OpenDentBusiness;
using OpenDental.UI;
using System.IO;
using CodeBase;
using OpenDentBusiness.Eclaims;
using OpenDentBusiness.VendorDirect.DentalXChange;
using System.Text;

namespace OpenDental {
	///<summary>This image picker shows all images and mounts for a specific patient and lets you send one to DXC. The code in this form was basically just copied in chunks from FormImagePickerPatient.</summary>
	public partial class FormImagePickerDXC:FormODBase {
		public Claim ClaimCur;
		public Patient PatientCur;
		private ClaimConnect.ImageAttachment _claimConnectImageAttachment;
		private long _docNumSelected;
		private WpfControls.UI.ImageSelector _imageSelector;
		///<summary>Stores a list of the image Ids from DXC to be saved locally.</summary>
		private List<int> _listImageReferenceIds;
		private long _mountNumSelected;
		private string _patFolder;
		///<summary>Open Dental 26.1.64: the file chosen with Send File (a PDF or an image from disk), while it is being sent. Null when the attachment
		///comes from the patient's images.</summary>
		private string _pathFileFromDisk;

		///<summary>Check that the imageFolder exists and is accessible before calling this form.</summary>
		///<summary>Open Dental 24.4: true once the user agreed to add images to an attachment that was already submitted to DentalXChange, or once
		///this window has sent images itself, so the question is asked at most once per window.</summary>
		private bool _isAddToSubmittedConfirmed;

		public FormImagePickerDXC() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_imageSelector=new WpfControls.UI.ImageSelector();
			elementHostImageSelector.Child=_imageSelector;
			_imageSelector.IsContextVisible=false;
			_imageSelector.ItemDoubleClick+=imageSelector_ItemDoubleClick;
			_imageSelector.SelectionChangeCommitted+=imageSelector_SelectionChangeCommitted;
			float scaleZoom=LayoutManager.ScaleMyFont();
			_imageSelector.LayoutTransform=new System.Windows.Media.ScaleTransform(scaleZoom,scaleZoom);
			listBoxImageType.Items.Clear();
			listBoxImageType.Items.AddEnums<ClaimConnect.ImageTypeCode>();
			listBoxImageType.SelectedIndex=-1;//No default, force user to select a type
			textDateCreated.Text=DateTime.Today.ToShortDateString();
		}

		private void FormImagePickerPatient_Load(object sender,EventArgs e) {
			//Jordan wants not visible for now
			checkIsXrayMirrored.Visible=false;
			if(ClaimAttaches.IsDxcAttachmentSubmitted(ClaimCur.AttachedFlags)) {
				Text+=" - "+Lan.g(this,"attachments already submitted");
			}
			FillTree();
			ValidateClaimDXC();
			WindowState=FormWindowState.Maximized;
			textNarrative.Text=ClaimCur.Narrative;
		}

		///<summary>Refreshes list from db, then fills the treeview.  Set keepSelection to true in order to keep the current selection active.</summary>
		public void FillTree() {
			List<Def> listDefsImageCats=Defs.GetDefsForCategory(DefCat.ImageCats,true);
			_imageSelector.SetCategories(listDefsImageCats);
			DataSet dataSet=Documents.RefreshForPatient(PatientCur.PatNum);
			_patFolder=ImageStore.GetPatientFolder(PatientCur,ImageStore.GetPreferredAtoZpath());
			_imageSelector.SetData(PatientCur,dataSet.Tables["DocumentList"],keepSelection:false,_patFolder);
			//Expand categories for BWs, FMX, and Panos.
			List<long> listDefNumsToExpand=new List<long>();
			List<long> listDefNums=listDefsImageCats.FindAll(x=>
				x.ItemName.Contains("BW")
				|| x.ItemName.Contains("Pano")
				|| x.ItemName.Contains("FMX")
				|| x.ItemValue.Contains("X")//User controlled setting to show image type in chart module
				|| x.ItemValue.Contains("M")//User controlled definition to show image type thumbnail
				).Select(x=>x.DefNum).ToList();
			listDefNumsToExpand.AddRange(listDefNums);
			_imageSelector.SetExpandedCategories(listDefNumsToExpand);
		}

		private void imageSelector_SelectionChangeCommitted(object sender,EventArgs e) {
			EnumImageNodeType nodeType=_imageSelector.GetSelectedType();
			long priKey=_imageSelector.GetSelectedKey();
			if(nodeType==EnumImageNodeType.Document){
				long docNum=priKey;
				Document documentSelected=Documents.GetByNum(docNum);
				Bitmap bitmap;
				if(DxcPdfAttachment.IsPdfFileName(documentSelected.FileName)) {//Open Dental 26.1.64: PDFs can be sent; they show as a PDF icon.
					bitmap=DxcPdfAttachment.CreatePreviewBitmap(400,520,documentSelected.Description);
				}
				else {
					bitmap=ImageHelper.GetBitmapOfDocumentFromDb(docNum);
				}
				pictureBox.Image?.Dispose();
				pictureBox.Image=bitmap;
				textFileName.Text=Documents.GetByNum(docNum).Description;
				if(textFileName.Text.IsNullOrEmpty()) {
					textFileName.Text="Attachment";
				}
			}
			if(nodeType==EnumImageNodeType.Mount) {
				long mountNum=priKey;
				Bitmap bitmap=MountHelper.GetBitmapOfMountFromDb(mountNum);
				pictureBox.Image?.Dispose();
				pictureBox.Image=bitmap;
				Mount mount=Mounts.GetByNum(mountNum);
				textFileName.Text=mount.Description;
				if(textFileName.Text.IsNullOrEmpty()) {
					textFileName.Text="Mount";
				}
			}
		}

		private bool ValidateClaimDXC() {
			Clearinghouse clearingHouse=ClaimConnect.GetClearingHouseForClaim(ClaimCur);
			if(XConnect.IsEnabled(clearingHouse)) {
				return ValidateXConnect();
			}
			else {
				return ValidateClaimConnect();
			}
		}

		private bool ValidateClaimConnect() {
			ClaimConnect.ValidateClaimResponse validateClaimResponse=null;
			//Usually super fast, but with a web call, they need a way to cancel if locked up.
			ProgressWin progressOD=new ProgressWin();
			progressOD.ActionMain=() => validateClaimResponse=ClaimConnect.ValidateClaim(ClaimCur,true);
			progressOD.StartingMessage="Communicating with DentalXChange...";
			try{
				progressOD.ShowDialog();
			}
			catch(ODException ex) {
				textClaimStatus.Text=ex.Message;
				return false;
			}
			catch(Exception ex) {
				textClaimStatus.Text=ex.Message;
				return false;
			}
			if(progressOD.IsCancelled){
				return false;
			}
			if(validateClaimResponse._isValidClaim) {
				textClaimStatus.Text="The claim is valid.";
				return true;
			}
			//Otherwise the claim must have errors, display them to the user.
			StringBuilder stringBuilder=new StringBuilder();
			for(int i=0;i<validateClaimResponse.ValidationErrors.Length;i++) {
				stringBuilder.AppendLine(validateClaimResponse.ValidationErrors[i]);
			}
			textClaimStatus.Text=stringBuilder.ToString();
			return false;
		}

		private bool ValidateXConnect() {
			XConnectWebResponse xConnectWebResponse=null;
			//Usually super fast, but with a web call, they need a way to cancel if locked up.
			ProgressWin progressWin=new ProgressWin();
			progressWin.ActionMain=() => xConnectWebResponse=XConnect.ValidateClaim(ClaimCur);
			progressWin.StartingMessage="Communicating with DentalXChange...";
			try {
				progressWin.ShowDialog();
			}
			catch(Exception ex) {
				textClaimStatus.Text=ex.Message;
				return false;
			}
			if(progressWin.IsCancelled) {
				return false;
			}
			if(xConnectWebResponse.response.claimStatus.message.Length>0) {
				textClaimStatus.Text=xConnectWebResponse.response.claimStatus.message;//Missing information errors
				return false;
			}
			for(int i=0;i<xConnectWebResponse.response.claimItems.Count();i++) {
				XConnectClaimItemResponse xConnectClaimItemResponse=xConnectWebResponse.response.claimItems[i];
				if(!string.IsNullOrEmpty(xConnectClaimItemResponse.itemStatus.message)) {
					//This message contains the attachment requirements
					textClaimStatus.Text=xConnectClaimItemResponse.itemStatus.message;
					return false;
				}
			}
			textClaimStatus.Text="The claim is valid";
			return true;
		}

		///<summary>Sends the passed-in attachments to ClaimConnect.  Will set the attachment id to the claim if needed.</summary>
		private void AddAttachments() {
			List<ClaimConnect.ImageAttachment> listClaimConnectImageAttachments=new List<ClaimConnect.ImageAttachment>();
			listClaimConnectImageAttachments.Add(_claimConnectImageAttachment);
			//Opens, fills and submits the attachment, or adds to the existing one, and sets AttachedFlags (Misc and, once sent, Submitted).
			_listImageReferenceIds=ClaimConnect.SendAttachmentImages(ClaimCur,listClaimConnectImageAttachments,textNarrative.Text);
			Claims.Update(ClaimCur);
		}

		///<summary>Sends attachments to DXC and saves locally. Mostly copied from FormClaimAttachSnipDXC.</summary>
		private bool SendSelectedToDXCAndSaveLocally() {
			if(!ClaimL.ConfirmAddToSubmittedDxcAttachment(ClaimCur,ref _isAddToSubmittedConfirmed)) {
				return false;
			}
			string fileName="";
			//Disposed at end of method.
			Bitmap bitmap=null;
			//Open Dental 26.1.64: a PDF's own bytes, from the patient's images or from disk, sent as they are. Null for images and mounts.
			byte[] arrayBytesPdf=null;
			if(_pathFileFromDisk!=null) {//Send File: a PDF or an image from disk.
				fileName=Path.GetFileName(_pathFileFromDisk);
				if(DxcPdfAttachment.IsPdfFileName(fileName)) {
					try {
						arrayBytesPdf=DxcPdfAttachment.ReadPdfFile(_pathFileFromDisk);
					}
					catch(ODException ex) {
						MessageBox.Show(this,ex.Message);
						return false;
					}
				}
				else {
					try {
						using Bitmap bitmapFile=new Bitmap(_pathFileFromDisk);
						bitmap=new Bitmap(bitmapFile);//A copy, so the file is not left locked.
					}
					catch(Exception ex) {
						ex.DoNothing();
						MsgBox.Show(this,"The selected file type is invalid. Valid file types include PDF, BMP, GIF, JPEG, PNG, TIFF, TIF, and JPG.");
						return false;
					}
				}
			}
			//A document or mount has been selected by this point.
			else if(_docNumSelected>0) {
				Document document=Documents.GetByNum(_docNumSelected);
				fileName=document.FileName;
				if(DxcPdfAttachment.IsPdfFileName(fileName)) {//Open Dental 26.1.64: any case of .pdf; the PDF's file is checked and sent as it is.
					try {
						arrayBytesPdf=DxcPdfAttachment.ReadPdfDocument(document,_patFolder);
					}
					catch(ODException ex) {
						MessageBox.Show(this,ex.Message);
						return false;
					}
				}
				else if(!fileName.ToLower().EndsWith(".bmp")
					&& !fileName.ToLower().EndsWith(".gif")
					&& !fileName.ToLower().EndsWith(".jpeg")
					&& !fileName.ToLower().EndsWith(".png")
					&& !fileName.ToLower().EndsWith(".tiff")
					&& !fileName.ToLower().EndsWith(".jpg")
					&& !fileName.ToLower().EndsWith(".tif"))
				{
					MsgBox.Show(this,"The selected file type is invalid. Valid file types include PDF, BMP, GIF, JPEG, PNG, TIFF, TIF, and JPG.");
					return false;
				}
				if(arrayBytesPdf==null) {
					bitmap=ImageHelper.GetBitmapOfDocumentFromDb(_docNumSelected);
				}
			}
			else {
				bitmap=MountHelper.GetBitmapOfMountFromDb(_mountNumSelected);
			}
			if(string.IsNullOrWhiteSpace(textFileName.Text)) {
				MsgBox.Show(this,"Enter the filename for this attachment.");
				return false;
			}
			if(textFileName.Text.IndexOfAny(Path.GetInvalidFileNameChars())>=0) {//returns -1 if nothing found
				MsgBox.Show(this,"Invalid characters detected in the filename. Please remove them and try again.");
				return false;
			}
			if(!textDateCreated.IsValid()) {
				MsgBox.Show(this,"Enter a valid date.");
				return false;
			}
			if(listBoxImageType.SelectedIndex==-1) {
				MsgBox.Show(this,"Select an image type.");
				return false;
			}
			try {
				if(arrayBytesPdf!=null) {//Open Dental 26.1.64: the PDF goes as it is.
					_claimConnectImageAttachment=DxcPdfAttachment.CreateImageAttachment(arrayBytesPdf,textFileName.Text,PIn.Date(textDateCreated.Text),
						listBoxImageType.GetSelected<ClaimConnect.ImageTypeCode>());
				}
				else {
					//Create an ImageAttachment object to send to ClaimConnect.
					_claimConnectImageAttachment=ClaimConnect.ImageAttachment.Create(
						fileName:textFileName.Text,
						createdDate:PIn.Date(textDateCreated.Text),
						typeCodeImage:listBoxImageType.GetSelected<ClaimConnect.ImageTypeCode>(),
						imageClaim:bitmap,
						rightOrientation:!checkIsXrayMirrored.Checked);
				}
			}
			catch(Exception ex) {
				FriendlyException.Show(Lan.g(this,"An error occurred. Please try again or call support.")+"\r\n"+ex.Message,ex);
				bitmap?.Dispose();
				return false;//Nothing to send.
			}
			try {
				AddAttachments();
			}
			catch(ODException ex) {
				//ODExceptions should already be Lans.g when throwing meaningful messages.
				//If they weren't translated, the message was from a third party and shouldn't be translated anyway.
				MessageBox.Show(ex.Message);
				return false;
			}
			catch(Exception ex) {
				//a catch-all for any exceptions that could be thrown from this method that aren't from a timeout and aren't handled as ODExceptions
				FriendlyException.Show("An error has occurred while trying to add attachments. If the problem persists please contact your clearinghouse's support.",ex);
				return false;
			}
			//Validate the claim, if it isn't valid let the user decide if they want to continue
			if(!ValidateClaimDXC()) {
				if(!MsgBox.Show(this,MsgBoxButtons.YesNo,"There were errors validating the claim, would you like to continue?")) {
					return false;
				}
			}
			//Used for determining which category to save the image attachments to. 0 will save the image to the first category in the Images module.
			long defNumImageType=0;
			Def defClaimAttachCat=Defs.GetCatList((int)DefCat.ImageCats).ToList().FindAll(x => x.ItemValue.Contains("C") && !x.IsHidden).FirstOrDefault();
			if(defClaimAttachCat==null) {//User does not have a Claim Attachment image category, just use the first image category available.
				defNumImageType=Defs.GetCatList((int)DefCat.ImageCats).FirstOrDefault(x => !x.IsHidden).DefNum;
			}
			else {
				defNumImageType=defClaimAttachCat.DefNum;
			}
			//Documents are already saved in A-Z folder.
			if(_docNumSelected>0) {
				_claimConnectImageAttachment.ImageFileNameActual=Documents.GetByNum(_docNumSelected).FileName;
			}
			//Saves mount bitmap (or a file sent from disk) to A-Z folder.
			else if(PrefC.GetBool(PrefName.SaveDXCAttachments)) {
				//Mounts get saved to the Claim Attachment image category. This is set by the user in Setup->Definitions->Image Categories.
				Document documentCur;
				if(_claimConnectImageAttachment.IsPdf) {//Open Dental 26.1.64: a PDF from disk is saved as the PDF it is.
					documentCur=ImageStore.Import(_claimConnectImageAttachment.ImageFileAsBase64,defNumImageType,ImageType.Document,PatientCur,
						fileExtension:DxcPdfAttachment.PdfExtension);
				}
				else {
					documentCur=ImageStore.Import(_claimConnectImageAttachment.ImageFileAsBase64,defNumImageType,ImageType.Attachment,PatientCur);
				}
				_claimConnectImageAttachment.ImageFileNameActual=documentCur.FileName;
				//Set description of newly created document
				Document documentOld=documentCur.Copy();
				documentCur.Description=textFileName.Text;
				Documents.Update(documentCur,documentOld);
			}
			//Create attachment objects
			List<ClaimAttach> listClaimAttaches=new List<ClaimAttach>();
			ClaimAttach claimAttach=new ClaimAttach();
			claimAttach.DisplayedFileName=_claimConnectImageAttachment.ImageFileNameDisplay;
			claimAttach.ActualFileName=_claimConnectImageAttachment.ImageFileNameActual;
			claimAttach.ClaimNum=ClaimCur.ClaimNum;
			claimAttach.ImageReferenceId=_listImageReferenceIds[0];
			listClaimAttaches.Add(claimAttach);
			//Keep a running list of attachments sent to DXC for the claim. This will show in the attachments listbox.
			ClaimCur.Attachments.AddRange(listClaimAttaches);
			Claims.Update(ClaimCur);
			MsgBox.Show(this,"Attachment sent successfully!");
			bitmap?.Dispose();//Null for a PDF.
			return true;
		}

		///<summary>Open Dental 26.1.64: sends a PDF or an image chosen from disk, with the image type, date and narrative entered in this window. The
		///file name box takes the file's name. Returns true once it was sent.</summary>
		private bool SendFileFromDisk() {
			if(listBoxImageType.SelectedIndex==-1) {//Asked before the file is chosen, so the user does not have to choose it twice.
				MsgBox.Show(this,"Select an image type.");
				return false;
			}
			using OpenFileDialog openFileDialog=new OpenFileDialog();
			openFileDialog.Multiselect=false;
			openFileDialog.Filter=Lan.g(this,"PDF and image files")+"|*.pdf;*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff|"+Lan.g(this,"All files")+"|*.*";
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ && !string.IsNullOrEmpty(_patFolder) && Directory.Exists(_patFolder)) {
				openFileDialog.InitialDirectory=_patFolder;
			}
			if(openFileDialog.ShowDialog()!=DialogResult.OK) {
				return false;
			}
			_pathFileFromDisk=openFileDialog.FileName;
			_docNumSelected=0;
			_mountNumSelected=0;
			textFileName.Text=Path.GetFileNameWithoutExtension(_pathFileFromDisk);
			try {
				return SendSelectedToDXCAndSaveLocally();
			}
			finally {
				_pathFileFromDisk=null;
			}
		}

		private void butSendFile_Click(object sender,EventArgs e) {
			if(SendFileFromDisk()) {
				FillTree();//A file saved to the patient's images shows in the tree.
			}
		}

		private bool SendAttachment() {
			_pathFileFromDisk=null;
			EnumImageNodeType nodeType=_imageSelector.GetSelectedType();
			long priKey=_imageSelector.GetSelectedKey();
			if(nodeType==EnumImageNodeType.Document){
				_docNumSelected=priKey;
				_mountNumSelected=0;
			}
			else if(nodeType==EnumImageNodeType.Mount) {
				_mountNumSelected=priKey;
				_docNumSelected=0;
			}
			else {
				MsgBox.Show(this,"An image must be specified before continuing.");
				return false;
			}
			return SendSelectedToDXCAndSaveLocally();
		}

		private void textNarrative_TextChanged(object sender,EventArgs e) {
			labelCharCount.Text=textNarrative.Text.Length+"/2000";//2000 char limit set by DXC
		}

		private void imageSelector_ItemDoubleClick(object sender,EventArgs e) {
			bool attachmentSentAndSaved=SendAttachment();
			if(attachmentSentAndSaved) {
				DialogResult=DialogResult.OK;
			}
		}

		private void butSend_Click(object sender,EventArgs e) {
			bool attachmentSentAndSaved=SendAttachment();
			if(attachmentSentAndSaved) {
				DialogResult=DialogResult.OK;
			}
		}

		private void butSendAndAgain_Click(object sender,EventArgs e) {
			bool attachmentSentAndSaved=SendAttachment();
			if(!attachmentSentAndSaved) {
				return;
			}
			//Refresh tree to see mounts saved locally after sending.
			FillTree();
		}
	}
}