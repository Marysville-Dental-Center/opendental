using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using CodeBase;
using OpenDentBusiness;
using WpfControls.UI;

namespace OpenDental {
	///<summary>Chart Letter Properties (24.4): date, time, description and provider of the letter; who last changed its content; the file's hash
	///against the stored hash (Match or No Match); the provider's signature; and a PDF copy for the Imaging module. Changes here never write a
	///new version and never change the user who last changed the content. Old versions and deleted letters open read-only.</summary>
	public partial class FrmChartLetterProperties:FrmODBase {
		private Document _document;
		private Document _documentOld;
		private Patient _patient;
		private byte[] _arrayFileBytes=new byte[0];
		private EnumChartLetterHashMatch _hashMatch;
		private bool _isSignatureChanged;
		private bool _isReadOnly;
		///<summary>Stores a PDF copy of the letter in the given image category and returns the new Imaging document, or null. Set by the caller,
		///because converting and importing live in the main program.</summary>
		public Func<Document,long,Document> FuncSavePdfCopy;

		public FrmChartLetterProperties(Document document,Patient patient) {
			InitializeComponent();
			_document=document;
			_documentOld=document.Copy();
			_patient=patient;
			Load+=FrmChartLetterProperties_Load;
			signatureBoxWrapper.SignatureChanged+=signatureBoxWrapper_SignatureChanged;
			PreviewKeyDown+=FrmChartLetterProperties_PreviewKeyDown;
		}

		private void FrmChartLetterProperties_Load(object sender,EventArgs e) {
			Lang.F(this);
			if(ODBuild.IsDebug()) {
				labelDocNum.Text="DocNum:"+_document.DocNum.ToString();
			}
			else {
				labelDocNum.Visible=false;
			}
			string patFolder="";
			try {
				patFolder=ImageStore.GetPatientFolder(_patient,ImageStore.GetPreferredAtoZpath());
				_arrayFileBytes=ChartLetters.GetFileBytes(_document,patFolder);
			}
			catch(Exception ex) {
				FriendlyException.Show(ex.Message,ex);
			}
			_hashMatch=ChartLetters.GetHashMatch(_document,_arrayFileBytes);
			textStatus.Text=ChartLetters.GetStatusText(_document);
			textDate.Text=_document.DateCreated.ToShortDateString();
			textTime.Text=_document.DateCreated.ToLongTimeString();
			textDescript.Text=_document.Description;
			FillComboProv();
			textUser.Text=_document.UserNum==0 ? "" : Userods.GetName(_document.UserNum);
			textHashFile.Text=_arrayFileBytes.Length==0 ? "" : ChartLetters.ComputeHash(_arrayFileBytes);
			textHashStored.Text=_document.ChartLetterHash;
			switch(_hashMatch) {
				case EnumChartLetterHashMatch.Match:
					labelMatch.Text=Lang.g(this,"Match");
					labelMatch.ColorText=System.Windows.Media.Colors.DarkGreen;
					break;
				case EnumChartLetterHashMatch.NoMatch:
					labelMatch.Text=Lang.g(this,"No Match");
					labelMatch.ColorText=System.Windows.Media.Colors.Firebrick;
					ChartLetters.LogHashMismatch(_document,Security.CurUser.UserNum);//The file was changed outside the program.
					break;
				default:
					labelMatch.Text=Lang.g(this,"File missing");
					labelMatch.ColorText=System.Windows.Media.Colors.Firebrick;
					break;
			}
			signatureBoxWrapper.SignatureMode=OpenDental.UI.SignatureBoxWrapper.SigMode.Document;
			FillSignature();
			comboImageCat.Items.AddDefNone();
			comboImageCat.Items.AddDefs(Defs.GetDefsForCategory(DefCat.ImageCats,true));
			comboImageCat.SetSelectedDefNum(0);
			_isReadOnly=ChartLetters.GetStatus(_document)!=EnumChartLetterStatus.Current
				|| !Security.IsAuthorized(ChartLetters.PERM_EDIT,_document.DateCreated,suppressMessage:true);
			if(_isReadOnly) {
				butSave.IsEnabled=false;
				signatureBoxWrapper.Enabled=false;
			}
			if(FuncSavePdfCopy==null) {
				groupPdf.Visible=false;
			}
		}

		///<summary>Shows the stored signature, trying every key a chart letter signature may have been made with.</summary>
		private void FillSignature() {
			List<string> listKeys=ChartLetters.GetSignatureKeyDataCandidates(_arrayFileBytes,_document.Note);
			for(int i=0;i<listKeys.Count;i++) {
				signatureBoxWrapper.FillSignature(_document.SigIsTopaz,listKeys[i],_document.Signature);
				if(_document.Signature=="" || signatureBoxWrapper.GetNumberOfTabletPoints(_document.SigIsTopaz)>0) {
					return;
				}
			}
			//None of the keys gives a valid signature: the box shows it as invalid.
			signatureBoxWrapper.FillSignature(_document.SigIsTopaz,listKeys[0],_document.Signature);
		}

		private void FillComboProv() {
			List<Provider> listProviders=Providers.GetProvsForClinic(Clinics.ClinicNum);
			comboProv.Items.Clear();
			comboProv.Items.AddProvNone();
			if(PrefC.GetBool(PrefName.EasyHideDentalSchools)) {
				comboProv.Items.AddProvsAbbr(listProviders);
			}
			else {
				comboProv.Items.AddProvsFull(listProviders);
			}
			comboProv.SetSelectedProvNum(_document.ProvNum);
		}

		private void signatureBoxWrapper_SignatureChanged(object sender,EventArgs e) {
			_isSignatureChanged=true;
		}

		private void butSavePdf_Click(object sender,EventArgs e) {
			if(FuncSavePdfCopy==null) {
				return;
			}
			Document documentCopy=FuncSavePdfCopy(_document,comboImageCat.GetSelectedDefNum());
			if(documentCopy!=null) {
				MsgBox.Show(this,"The copy was stored in the Imaging module.");
			}
		}

		private void butAudit_Click(object sender,EventArgs e) {
			List<EnumPermType> listPermissions=new List<EnumPermType>();
			listPermissions.Add(EnumPermType.ImageCreate);
			listPermissions.Add(EnumPermType.ImageEdit);
			listPermissions.Add(EnumPermType.ImageDelete);
			FrmAuditOneType frmAuditOneType=new FrmAuditOneType(_patient.PatNum,listPermissions,Lang.g(this,"Audit Trail for Chart Letter"),
				_document.DocNum);
			frmAuditOneType.ShowDialog();
		}

		private void FrmChartLetterProperties_PreviewKeyDown(object sender,KeyEventArgs e) {
			if(butSave.IsAltKey(Key.S,e)) {
				butSave_Click(this,new EventArgs());
			}
		}

		private void butSave_Click(object sender,EventArgs e) {
			if(_isReadOnly) {
				return;
			}
			if(!textDate.IsValid()) {
				MsgBox.Show(this,"Please fix data entry errors first.");
				return;
			}
			if(textDate.Text=="" || textTime.Text=="") {
				MsgBox.Show(this,"Please enter a date and time.");
				return;
			}
			DateTime dateTimeTest;
			if(!DateTime.TryParse(textTime.Text,out dateTimeTest)) {
				MsgBox.Show(this,"Please enter a valid time.");
				return;
			}
			DateTime dateTimeEntered=PIn.DateT(textDate.Text+" "+textTime.Text);
			if(dateTimeEntered>DateTime.Now) {
				MsgBox.Show(this,"A chart letter cannot be dated in the future.");
				return;
			}
			if(dateTimeEntered.Date!=_documentOld.DateCreated.Date
				&& !Security.IsAuthorized(ChartLetters.PERM_EDIT,dateTimeEntered,suppressMessage:false))
			{
				return;//The new date must also be within the user's date limit.
			}
			_document.DateCreated=dateTimeEntered;
			_document.Description=textDescript.Text;
			_document.ProvNum=comboProv.GetSelectedProvNum();
			if(_isSignatureChanged) {
				if(signatureBoxWrapper.SigIsBlank) {
					_document.Signature="";
					_document.SigIsTopaz=false;
				}
				else {
					if(_hashMatch!=EnumChartLetterHashMatch.Match) {
						MsgBox.Show(this,"The letter's file does not match its stored hash, so it cannot be signed. Open it with Edit Document and save it "
							+"to store a new version first.");
						return;
					}
					_document.Signature=signatureBoxWrapper.GetSignature(ChartLetters.GetSignatureKeyData(_arrayFileBytes));
					_document.SigIsTopaz=signatureBoxWrapper.GetSigIsTopaz();
				}
			}
			try {
				ChartLetters.UpdateProperties(_document,_documentOld,Security.CurUser.UserNum);
			}
			catch(Exception ex) {
				MsgBox.Show(ex.Message);
				return;
			}
			IsDialogOK=true;
		}
	}
}
