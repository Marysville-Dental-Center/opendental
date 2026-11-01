using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.Eclaims;
using OpenPmsDentalBusiness.VendorDirect.DentalXChange;

namespace OpenPmsDental {
	///<summary>This form takes the image the user wants to send through their clearinghouse and gathers the additional information (specified by the user)
	///that is needed to create the attachment.</summary>
	public partial class FormClaimAttachmentItemEdit:FormODBase {
		public bool DoNewSnip=false;
		public ClaimConnect.ImageAttachment ImageAttachmentDXC;
		public EDS.ImageAttachment ImageAttachmentEDS;
		public bool IsSnip=false;
		///<summary>This is another reference to an image in parent form.  Disposed by that form.</summary>
		private Image _imageForClaim;
		private EclaimsCommBridge _eclaimsCommBridge;
		private string _narrative;
		///<summary>Open Dental 26.1.64: the PDF's own bytes when this attachment is a PDF (DentalXChange only), otherwise null. The image passed to
		///the constructor is then only a preview.</summary>
		private byte[] _arrayBytesPdf;

		///<summary>Used for opening and editing an existing image attachment row in FormClaimAttachment.</summary>
		public FormClaimAttachmentItemEdit(Image image,string fileName,DateTime dateTime,Enum enumImageType,bool isRightOrientation ,EclaimsCommBridge eclaimsCommBridge,string narrative="") : this(image,eclaimsCommBridge) {
			textFileName.Text=fileName;
			textDateCreated.Text=dateTime.ToShortDateString();//Override today's date with the passed in date.
			listBoxImageType.SetSelectedEnum(enumImageType);
			checkIsXrayMirrored.Checked=!isRightOrientation;
			_narrative=narrative;
		}

		///<summary>Takes an image the user has chosen to send with their claim.</summary>
		public FormClaimAttachmentItemEdit(Image image,EclaimsCommBridge eclaimsCommBridge) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_eclaimsCommBridge=eclaimsCommBridge;
			listBoxImageType.Items.Clear();
			if(_eclaimsCommBridge==EclaimsCommBridge.ClaimConnect) {
				listBoxImageType.Items.AddEnums<ClaimConnect.ImageTypeCode>();
			}
			else if(_eclaimsCommBridge==EclaimsCommBridge.EDS) {
				listBoxImageType.Items.AddEnums<EDS.EnumDocumentTypeCode>();
			}
			listBoxImageType.SelectedIndex=0;//Default to the first item in the list
			_imageForClaim=image;
			textDateCreated.Text=DateTime.Today.ToShortDateString();
			ODImaging.ImageApplyOrientation(image);
			pictureBoxImagePreview.Image=ODImaging.ImageScaleMaxHeightAndWidth(image,pictureBoxImagePreview.Height,pictureBoxImagePreview.Width);
			pictureBoxImagePreview.Invalidate();
		}

		///<summary>Open Dental 26.1.64: makes this attachment a PDF that is sent as it is (DentalXChange only; the caller has checked the bytes). Call
		///before ShowDialog. A non-blank fileNameDisplay replaces the file name. The mirror-image box is hidden: orientation only applies to X-ray
		///images.</summary>
		public void SetPdf(byte[] arrayBytesPdf,string fileNameDisplay=null) {
			_arrayBytesPdf=arrayBytesPdf;
			if(!string.IsNullOrWhiteSpace(fileNameDisplay)) {
				textFileName.Text=fileNameDisplay;
			}
			checkIsXrayMirrored.Checked=false;
			checkIsXrayMirrored.Visible=false;
		}

		private void FormClaimAttachmentItemEdit_Load(object sender,EventArgs e) {
			if(!IsSnip) {
				butNewSnip.Visible=false;
				labelNewSnip.Visible=false;
			}
		}

		private void butNewSnip_Click(object sender,EventArgs e) {
			if(!ValidateAndCreateAttachment()) {
				return;
			}
			DoNewSnip=true;
			DialogResult=DialogResult.OK;
		}

		///<summary>Called on ValidateAndCreateAttachment(). This method takes the user entered data and creates the ClaimConnect.ImageAttachment object
		///used by FormClaimAttach. This object is eventually used in the ClaimConnect.CreateAttachment() API call to DentalXChange.</summary>
		private void CreateImageAttachment() {
			if(_eclaimsCommBridge==EclaimsCommBridge.ClaimConnect && _arrayBytesPdf!=null) {//Open Dental 26.1.64: the PDF goes as it is.
				ImageAttachmentDXC=DxcPdfAttachment.CreateImageAttachment(_arrayBytesPdf,textFileName.Text,PIn.Date(textDateCreated.Text),
					listBoxImageType.GetSelected<ClaimConnect.ImageTypeCode>());
			}
			else if(_eclaimsCommBridge==EclaimsCommBridge.ClaimConnect) {
				ImageAttachmentDXC=ClaimConnect.ImageAttachment.Create(
					fileName:textFileName.Text,
					createdDate:PIn.Date(textDateCreated.Text),
					typeCodeImage:listBoxImageType.GetSelected<ClaimConnect.ImageTypeCode>(),
					imageClaim:_imageForClaim,
					rightOrientation:!checkIsXrayMirrored.Checked);
			}
			else if(_eclaimsCommBridge==EclaimsCommBridge.EDS) {
				ImageAttachmentEDS=EDS.ImageAttachment.Create(
					fileName:textFileName.Text,
					dateTimeCreated:PIn.Date(textDateCreated.Text),
					documentTypeCode:listBoxImageType.GetSelected<EDS.EnumDocumentTypeCode>(),
					imageClaim:_imageForClaim,
					isRightOriented:!checkIsXrayMirrored.Checked,
					narrative:_narrative);
			}
		}

		private bool ValidateAndCreateAttachment() {
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
				CreateImageAttachment();
			}
			catch(ODException ex) {//A PDF that can't be sent; the message is ready to show.
				MessageBox.Show(this,ex.Message);
				return false;
			}
			return true;
		}

		///<summary></summary>
		private void butSave_Click(object sender,EventArgs e) {
			if(!ValidateAndCreateAttachment()) {
				return;
			}
			DialogResult=DialogResult.OK;
		}

	}
}