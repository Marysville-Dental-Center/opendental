using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using OpenPmsDentalBusiness;
using CodeBase;

namespace OpenPmsDental.UI {
/*
Jordan is the only one allowed to edit this file.

How signature encryption works:
	A signature starts as a collection of straight line segments that look like curved lines when chained together.
	The OD signature format looks like this: 45,68;48,70;49,72;0,0;55,88;etc.  It's simply a sequence of points, separated by semicolons.  0,0 represents pen up.
	We then need to encrypt this string with symmetric encryption using a key of some sort.
	The key that we use for this symmetric encryption is a 16 char hash of the data that the signature is tied to.
	For example, we might want to tie the signature to "This is a note", PriKeyNum=123, and ProvNum=7. 
	Our key data then looks like this: "This is a note1237". 
	Once we pick a signature data strategy, we cannot ever change it for that particular table, although we can for other tables.
	The hash might then look like {4A 6F 68 6E 44 6F 65 44 6B 9A A6 27 D6 9E 61 6C}. We never see this. It's just stored in memory as 16 bytes while we manipulate the sig.
	The hash is our symmetric key. The signature string gets encrypted and stored in the db.
	To get the signature back out of the database as vectors, we must have the same hash.
	We must recreate the hash at that time from the original key data.
	If we can't unencrypt the signature, we can't draw it and must show "invalid sig" or something like that.
	This strategy hides the sig from prying eyes in the db, and it also proves that the data has not changed since signing.

Topaz hardware vs our SignatureBox:
	Topaz is the 'tablet' hardware that a patient signs on with a stylus.
	They also provide a SigPlusNET control that we can place on the screen to show a signature that was signed on their hardware.
	We use their dll to perform the encryption and we don't get to see what format they use. Their .sig format is proprietary.
	https://www.topazsystems.com/software/download/sigplusnet.pdf
	Topaz signature pads are not supported since stage 0 of the .NET 10 move (Open PMS - Dental): the SigPlusNET control is never created, a
	stored Topaz signature shows TOPAZ_NOT_SUPPORTED_MESSAGE and is not valid, and every signature is made in our own SignatureBox. The SigIsTopaz
	columns stay, so the public members below that name Topaz stay too (GetSigIsTopaz always returns false).
	We also have our own SignatureBox control built by us. Our strategy is the one discussed above.
	KeyString and AutoKeyData are terms that originated with Topaz and which we have adopted for use in our SignatureBox.
	KeyString: This is the hash of the key data. We use 16 bytes. Topaz likes to create their own, so we don't generally use this with Topaz.
	AutoKeyData: This is the unhashed key data and can be quite long.
	The idea is that we should use one or the other, depending on if we had already hashed the key data.
	The way we know if a signature in the db is ours or Topaz, we typically have another db column along with Signature called SigIsTopaz.
	Sometimes, like in Sheets, the first char is 1 or 0 to indicate Topaz or not. The remainder of the signature is the actual signature.
	The OD sigs are not compressed, and the Topaz sigs are.  But there is very little difference in their sizes.  It would be very rare for a signature to be larger than 1000 bytes.

Control nesting
	This control is SignatureBoxWrapper.cs. It is a wrapper around the SignatureBox control, our own internal control. It held Topaz's
	SigPlusNET control too until Topaz was removed.
	For WPF, this control is wrapped in with a WindowsFormsHost container because Topaz did not have a WPF version, and wrapping is what they recommended.
	This SignatureBoxWrapper also contains two buttons that we need. The buttons are ugly and need an overhaul.

Size
	The control was designed originally to always be exactly 362x79
	It can be set larger if proportional and if scale and zoom match.
	In other words, it's not yet designed to be set to arbitrary sizes other than as described above.

Historical changes:
	There have been a series of changes and bugs over the years, both from us and from Topaz. Examples:
		We had a bug for a while where we were "encrypting" sigs with 16 zeros.
		There have been newline bugs due to RichTextBox and MiddleTier issues.
		Around 2010, Topaz added an enumeration value in the middle of an enumeration, breaking our code.
		Our internal SignatureBox sometimes uses the entire key string as the symmetric key instead of a hashed 16 byte version of the key string.
	Each time we have a bug or a variation on the original strategy, we must forever be able to handle that unique situation without breaking others.
	This leads to many "if" statements. We end up trying a chain of decryptions until we get one that works.
	At some point in the future, we may decide to re-encrypt all of the signatures with one single known good strategy.
	But because Topaz is still a black box, we can't ever really completely fix the complexity.

Guideline for usage:

In the load event handler or similar:
	string keyData=str1+str2+long1.ToString(), etc.
	signatureBoxWrapper.FillSignature(someTable.SigIsTopaz,keyData,someTable.Signature);

In the save click:
	if(sigBoxWrapper.GetSigChanged()){
		string keyData=Procedures.GetSignatureKeyData(_procedure, textNotes.Text);
		someTable.Signature=signatureBoxWrapper.GetSignature(keyData);
		someTable.SigIsTopaz=signatureBoxWrapper.GetSigIsTopaz();
	}

In FormClosing:
	signatureBoxWrapper?.SetTabletState(0);
	//signatureBoxWrapper?.Dispose(); is not needed here but is needed wherever you do not have it placed on a form.
	//    Example: when temporarily instantiating it for placing an image on a sheet.
*/
	///<summary>See the notes at the top of this file. Must always be exactly 362x79.</summary>
	public partial class SignatureBoxWrapper:UserControl {
		#region Fields
		///<summary>Shown in place of a stored Topaz signature, which can be neither shown nor checked: Topaz signature pads were removed in stage 0 of
		///the .NET 10 move.</summary>
		public const string TOPAZ_NOT_SUPPORTED_MESSAGE="Topaz signature pads are not supported in this version of Open PMS - Dental.";
		///<summary>Default 1. Typical value 1.5</summary>
		private float _scaleMS = 1;
		///<summary>Default 1. Typical value 1.2</summary>
		private float _zoomLocal=1;
		///<summary>This is set when someone has started drawing a signature. When true, this will be used to stop people from accidentally
		///drawing on the screen and invalidating their signature on FormSheetFillEdit.</summary>
		public bool IsSigStarted;
		public bool SigChanged;
		///<summary>Used for special cases where signature logic varies from our default.</summary>
		private SigMode _signatureMode=SigMode.Default;
		///<summary>The label's own text (LabelText) while the label says that Topaz signature pads are not supported, else null.</summary>
		private string _labelTextBeforeTopazMessage;
		///<summary>The Topaz-shaped signature (IsTopazShaped) that the last fill could not check because Topaz signature pads are not supported, else
		///null. A signature from the program's own box is never kept here: as a Topaz one it fails with the label's own text, as before. Treatment plans
		///keep two signatures under one SigIsTopaz column, so FormTPsign and SheetDrawingJob fill a signature that does not show once more as the other
		///kind. When the program's own check then does not accept that same signature either, the not-supported message stays: Topaz could not check
		///it, so "Invalid Signature" would be a tamper warning the program cannot back up.</summary>
		private string _signatureTopazNotInstalled;
		#endregion Fields

		#region Constructor
		public SignatureBoxWrapper() {
			InitializeComponent();
			signatureBox.SetTabletState(1);
			butClearSig.BringToFront();
		}
		#endregion Constructor

		#region Events
		///<summary>The reason for this event is so that if a different user is signing, that it properly records the change in users.  See the example pattern in FormProcGroup.</summary>
		[Category("OD")]
		[Description("Event raised when signature is cleared or altered.")]
		public event EventHandler SignatureChanged;
		///<summary>Event raised when the X button is clicked.</summary>
		[Category("OD")]
		[Description("Event raised when the X button is clicked.")]
		public event EventHandler ClearSignatureClicked;
		///<summary>Never raised: the Sign Topaz button was removed with Topaz signature pads. Kept so that its subscribers still compile.</summary>
		[Category("OD")]
		[Description("Never raised: Topaz signature pads are not supported.")]
		public event EventHandler SignTopazClicked {
			add { }
			remove { }
		}
		#endregion Events

		#region Enums
		///<summary>Do not add to this enum. Use Default.</summary>
		public enum SigMode {
			///<summary>0: Default case</summary>
			Default,
			///<summary>Signatures used for documents in the Images module.</summary>
			Document,
			///<summary>Signatures used for treatment plans.</summary>
			TreatPlan,
			///<summary>Signatures used for ortho charts.</summary>
			OrthoChart
		}
		#endregion Enums

		#region Properties
		public bool IsValid{
			get { return (!labelInvalidSig.Visible); }
		}

		[Category("OD")]
		[Description("Usually 'Invalid Signature', but this can be changed for different situations.")]
		[DefaultValue("Invalid Signature")]
		///<summary>Usually 'Invalid Signature', but this can be changed for different situations.</summary>
		public string LabelText {
			get {
				return _labelTextBeforeTopazMessage??labelInvalidSig.Text;
			}
			set {
				if(_labelTextBeforeTopazMessage!=null) {
					_labelTextBeforeTopazMessage=value;
					return;
				}
				labelInvalidSig.Text=value;
			}
		}

		public bool SigIsBlank{
			get{
				return(signatureBox.NumberOfTabletPoints()==0);
			}
		}

		///<summary>Used for special cases where signature logic varies from our default.</summary>
		[Category("OD")]
		[Description("Used for special cases where signature logic varies from our default.")]
		[DefaultValue(SigMode.Default)]
		public SigMode SignatureMode {
			get {
				return _signatureMode;
			}
			set {
				_signatureMode=value;
			}
		}
		#endregion Properties

		protected override void OnLayout(LayoutEventArgs e){
			base.OnLayout(e);
			butESign.Size=new Size(ScaleI(20),ScaleI(20));
			butClearSig.Size=new Size(ScaleI(20),ScaleI(20));
			butClearSig.Location=new Point(Width-ScaleI(20),0);
			signatureBox.Width=Width-2;
			signatureBox.Height=Height-2;
		}

		///<summary>Converts from 96dpi to current total scale.</summary>
		private float ScaleF(float val96){
			float scaleTotal=_scaleMS*_zoomLocal;//example 1.5*1.2
			return val96*scaleTotal;
		}

		///<summary>Converts from 96dpi to current total scale.</summary>
		private int ScaleI(float val96){
			float scaleTotal=_scaleMS*_zoomLocal;//example 1.5*1.2
			return (int)(Math.Round(val96*scaleTotal));
		}

		///<summary>Shows, in place of the signature, that Topaz signature pads are not supported, so a Topaz signature can be neither shown nor
		///checked. The signature then counts as not valid (IsValid), as it did when Topaz's software was not installed.</summary>
		private void ShowTopazNotInstalled(string signature) {
			if(_labelTextBeforeTopazMessage==null) {
				_labelTextBeforeTopazMessage=labelInvalidSig.Text;
			}
			_signatureTopazNotInstalled=signature;
			labelInvalidSig.Text=Lans.g(this,TOPAZ_NOT_SUPPORTED_MESSAGE);
			labelInvalidSig.Visible=true;
		}

		///<summary>True if the signature could be a Topaz SigPlus string, which is hex digits. The program's own box stores base 64 of AES ciphertext
		///(SigBox.EncryptSigString), which has letters past F, '+', '/' or '=' in effectively every real signature, so it is never taken for Topaz's.
		///Whitespace is ignored in case a stored string picked some up.</summary>
		public static bool IsTopazShaped(string signature) {
			if(string.IsNullOrEmpty(signature)) {
				return false;
			}
			bool hasHexDigit=false;
			foreach(char c in signature) {
				if(char.IsWhiteSpace(c)) {
					continue;
				}
				if(!Uri.IsHexDigit(c)) {
					return false;
				}
				hasHexDigit=true;
			}
			return hasHexDigit;
		}

		///<summary>Puts the label's own text back after ShowTopazNotInstalled, and forgets the signature Topaz could not check.</summary>
		private void RestoreLabelText() {
			_signatureTopazNotInstalled=null;
			if(_labelTextBeforeTopazMessage==null) {
				return;
			}
			labelInvalidSig.Text=_labelTextBeforeTopazMessage;
			_labelTextBeforeTopazMessage=null;
		}

		///<summary>Always false: Topaz signature pads are not supported (TOPAZ_NOT_SUPPORTED_MESSAGE). It returned true when Topaz's control was
		///initialized.</summary>
		public bool CheckTopaz() {
			return false;
		}

		public void SetAllowDigitalSig(bool allowDigitalSig,bool doSkipProcSignaturePref=false) {
			if(doSkipProcSignaturePref) {
				butESign.Visible=allowDigitalSig;
			}
			else {
				butESign.Visible=allowDigitalSig && PrefC.GetBool(PrefName.SignatureAllowDigital);
			}
		}

		///<summary>Defaults are 1,1.</summary>
		public void SetScaleAndZoom(float scaleMS,float zoomLocal){
			if(scaleMS==_scaleMS && zoomLocal==_zoomLocal){
				return;
			}
			_scaleMS = scaleMS;
			_zoomLocal=zoomLocal;
			signatureBox.SetScaleAndZoom(scaleMS,zoomLocal);
		}
		
		protected void OnSignatureChanged() {
			SigChanged=true;
			if(SignatureChanged!=null){
				SignatureChanged(this,new EventArgs());
			}
		}

		/// <summary>KeyData should not be hashed.</summary>
		public void FillSignature(bool sigIsTopaz,string keyData,string signature) {
			if(signature==null || signature=="") {
				return;
			}
			//True when the fill just before this one could not check this same signature because Topaz signature pads are not supported.
			bool isTopazNotInstalledForSignature=(_signatureTopazNotInstalled!=null && _signatureTopazNotInstalled==signature);
			RestoreLabelText();
			//A Topaz signature is never shown or checked: Topaz signature pads are not supported (TOPAZ_NOT_SUPPORTED_MESSAGE).
			if(sigIsTopaz) {
				if(IsTopazShaped(signature)) {
					ShowTopazNotInstalled(signature);
				}
				else {
					//Not a Topaz string (the program's own box stores base 64), so Topaz could not have checked it either: the label's own text, as before.
					labelInvalidSig.Visible=true;
				}
				return;
			}
			//This does 3 checks for normal signatures.  These keyData replacements are due to MiddleTier & RichTextBox newline issues.
			//Most cases will not get past check 1.  Some of the following checks are taken care of by things like POut.StringNote().
			//Check 1:  keyData.Replace("\r\n","\n").  This reverts any changes middle tier made to the keydata.  
			//					Middle tier converts "\r\n" -> "\n" -> "\r\n" so we change it back to "normal".
			//Check 2:  Normal keydata.  This is for cases that had "\r\n" as the original note.  These are keydata that were captured with a textbox.
			//Check 3:  keyData.Replace("\r\n","\n").Replace("\n","\r\n").  This is for cases where the original note was captured with a textbox, and then
			//					was filled into an ODTextBox(richtextbox) and the "\r\n" was changed to "\n" then the user clicked ok changing the note.
			//Note that if the keydata was hashed before this point, these replacements have to be dealt with outside of this function.
			FillSignatureHelper(keyData.Replace("\r\n","\n"),signature);
			if(signatureBox.NumberOfTabletPoints()==0) {
				FillSignatureHelper(keyData,signature);
			}
			if(signatureBox.NumberOfTabletPoints()==0) {
				FillSignatureHelper(keyData.Replace("\r\n","\n").Replace("\n","\r\n"),signature);
			}
			if(isTopazNotInstalledForSignature && !IsValid) {
				//A Topaz-shaped signature the program's own check does not accept either, so it may be a Topaz one that cannot be checked here: keep saying so.
				ShowTopazNotInstalled(signature);
			}
		}

		private void FillSignatureHelper(string keyData,string signature) {
			signatureBox.Visible=true;
			labelInvalidSig.Visible=false;
			signatureBox.ClearTablet();
			//sigBox.SetSigCompressionMode(0);
			//sigBox.SetEncryptionMode(0);
			//Some places used different logic for keystring and autokeydata in the past.  This must be maintained to keep signatures valid.
			switch(_signatureMode) {
				case SigMode.Document:
					//No auto key data set, only key string.
					signatureBox.SetKeyString(keyData);
					break;
				case SigMode.TreatPlan:
					string hashedKeyData=OpenPmsDentalBusiness.TreatPlans.GetHashStringForSignature(keyData);//Passed in KeyData still needs to be hashed.
					//No auto key data set, only key string.
					signatureBox.SetKeyString(hashedKeyData);
					break;
				case SigMode.OrthoChart:
					hashedKeyData=OpenPmsDentalBusiness.OrthoCharts.GetHashStringForSignature(keyData);//Passed in KeyData still needs to be hashed.
					//No auto key data set, only key string.
					signatureBox.SetKeyString(hashedKeyData);
					break;
				case SigMode.Default:
					signatureBox.SetKeyString("0000000000000000");
					signatureBox.SetAutoKeyData(keyData);
					break;
			}
			//sigBox.SetEncryptionMode(2);//high encryption
			//sigBox.SetSigCompressionMode(2);//high compression
			signatureBox.SetSigString(signature);
			if(signatureBox.IsDigitalSignature()) {
				labelInvalidSig.Visible=false;
			}
			else if(signatureBox.NumberOfTabletPoints()==0) {  //Both signature checks were invalid.
				labelInvalidSig.Visible=true;
			}
			signatureBox.SetTabletState(0);//not accepting input.  To accept input, change the note, or clear the sig.
		}

		///<summary>Helper method to manipulate the visibility of all buttons on the signature wrapper.</summary>
		public void SetButtonVisibility(bool isVisible) {
			butClearSig.Visible=isVisible;
			butESign.Visible=isVisible;
		}

		///<summary>0 for a Topaz signature, which is never shown (Topaz signature pads are not supported).</summary>
		public int GetNumberOfTabletPoints(bool sigIsTopaz) {
			if(sigIsTopaz) {
				return 0;
			}
			return signatureBox.NumberOfTabletPoints();
		}

		///<summary>This can be used to determine whether the signature has changed since the control was created.  Or you can use the SignatureChanged event to track changes.</summary>
		public bool GetSigChanged(){
			return SigChanged;
		}

		///<summary>This should NOT be used unless GetSigChanged returns true. Always false: every signature is made in the program's own box, because
		///Topaz signature pads are not supported. The SigIsTopaz columns it fills stay.</summary>
		public bool GetSigIsTopaz(){
			return false;
		}

		public bool GetIsTypedFromWebForms() {
			string pointString=signatureBox.GetPointStringForWebTest();
			return pointString.Equals("{X=1,Y=1};{X=15,Y=15};{X=0,Y=0};{X=1,Y=15};{X=15,Y=1}"); //This is the exact point string we expect to get from a digital signature in webforms
		}

		/*
		///<summary></summary>
		public bool GetSigIsValid(){
			if(labelInvalidSig.Visible){
				return false;
			}
			return true;
		}*/

		///<summary>This should happen a lot before the box is signed.  Once it's signed, if this happens, then the signature will be invalidated.  The user would have to clear the invalidation manually.  This "invalidation" is just a visual cue; nothing actually happens to the data.</summary>
		public void SetInvalid(){
			if(signatureBox.NumberOfTabletPoints()==0) {
				return;//no need to do anything because no signature
			}
			labelInvalidSig.Visible=true;
			labelInvalidSig.BringToFront();
		}

		///<summary>This should NOT be used unless GetSigChanged returns true. keyData should not be hashed.</summary>
		public string GetSignature(string keyData){
			if(signatureBox.NumberOfTabletPoints()==0) {
				return "";
			}
			//sigBox.SetSigCompressionMode(0);
			//sigBox.SetEncryptionMode(0);
			//Some places used different logic for keystring and autokeydata in the past.  This must be maintained to keep signatures valid.
			switch(_signatureMode) {
				case SigMode.Document:
				case SigMode.TreatPlan:
				case SigMode.OrthoChart:
					//No auto key data set, only key string.
					signatureBox.SetKeyString(keyData);
					break;
				case SigMode.Default:
					signatureBox.SetKeyString("0000000000000000");
					signatureBox.SetAutoKeyData(keyData);
					break;
			}
			//sigBox.SetEncryptionMode(2);
			//sigBox.SetSigCompressionMode(2);
			return signatureBox.GetSigString();
		}

		private void butClearSig_Click(object sender,EventArgs e) {
			if(ClearSignatureClicked!=null) {
				ClearSignatureClicked(this,new EventArgs());
			}
			ClearSignature();
			OnSignatureChanged();
		}

		///<summary>Explicitly set focus on this control.</summary>
		private void sigBox_MouseDown(object sender,MouseEventArgs e) {
			Focus();
			IsSigStarted=true;
		}

		private void sigBox_MouseUp(object sender,MouseEventArgs e) {
			//this is done on mouse up so that the initial pen capture won't be delayed.
			if(signatureBox.GetTabletState()==1//if accepting input.
				&& !SigChanged)//and sig not changed yet
			{
				//sigBox handles its own pen input.
				OnSignatureChanged();
			}
		}

		///<summary>Must set width and height of control and run FillSignature first.</summary>
		public Bitmap GetSigImage(){
			//no outline
			return (Bitmap)signatureBox.GetSigImage(false);
		}

		///<summary>If this is called externally, then the event SignatureChanged will also fire. clearTopazTablet is not used since Topaz signature
		///pads are not supported.</summary>
		public void ClearSignature(bool clearTopazTablet=true){
			signatureBox.Enabled=true;
			signatureBox.ClearTablet();
			signatureBox.Visible=true;
			signatureBox.SetTabletState(1);//on-screen box is now accepting input.
			SigChanged=true;
			RestoreLabelText();
			labelInvalidSig.Visible=false;
			OnSignatureChanged();
		}

		///<summary>Does nothing: Topaz signature pads are not supported. Kept for its callers.</summary>
		public void SetControlSigBoxTopaz(Control sigBoxTopaz) {
		}

		///<summary>Set to 1 to activate it to start accepting signatures.  Set to 0 to no longer accept input.  Should be called with a state of '0' in FormClosing events.</summary>
		public void SetTabletState(int state) {
			signatureBox?.SetTabletState(state);
		}

		private Userod _userSig;
		///<summary>The user who signs in this box when it is not Security.CurUser: set by a Change User button (procedure notes, group notes, ortho
		///chart, and since 25.1 sheet and treatment plan signature boxes). The E button stamps this user's name. Null means Security.CurUser.</summary>
		public Userod UserSig {
			get {
				return _userSig;
			}
			set {
				_userSig=value;
			}
		}

		private void butESign_Click(object sender,EventArgs e) {
			Userod curUser=_userSig??Security.CurUser;
			Provider provCur=null;
			if(curUser!=null) {
				provCur=Providers.GetProv(curUser.ProvNum);
			}
			string digitalSignature=Lans.g(this,"Digitally Signed by ");
			if(provCur!=null) {
				digitalSignature+=provCur.GetLongDesc();
			}
			else if(curUser!=null) {
				digitalSignature+=curUser.UserName+" (UserNum:"+curUser.UserNum+")";
			}
			else {
				//should never happen
				digitalSignature= Lans.g(this,"Digitally signed by unknown user.");
			}
			digitalSignature+="\r\n"+Lans.g(this,"Date Signed")+": "+MiscData.GetNowDateTime().ToString();
			signatureBox.SetDigitalSig(digitalSignature);
			OnSignatureChanged();
			signatureBox.Enabled=false;
		}
	}
}
