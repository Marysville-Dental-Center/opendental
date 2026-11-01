using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenDentBusiness;
using WpfControls.UI;

namespace OpenDental {
	public partial class FrmImageDrawEdit:FrmODBase {
		public ImageDraw ImageDrawCur;
		/// <summary>Or, for a mount, the size of the mount.</summary>
		public Size SizeBitmap;
		///<summary>Same as zoom slider from parent.  Typical value is 100.</summary>
		public float ZoomVal;
		///<summary>Keeps Actual Size and Apparent Size in step as the user types in either box. Made on load, once ZoomVal is set.</summary>
		private ImageDrawFontSizeSync _imageDrawFontSizeSync;

		public FrmImageDrawEdit() {
			InitializeComponent();
			Load+=FrmImageDrawEdit_Load;
			textFontSize.TextChanged+=textFontSize_TextChanged;
			textFontApparent.TextChanged+=textFontApparent_TextChanged;
			PreviewKeyDown+=FrmImageDrawEdit_PreviewKeyDown;
		}

		private void FrmImageDrawEdit_Load(object sender, EventArgs e){
			Lang.F(this);
			textDrawText.Text=ImageDrawCur.GetTextString();
			Point pointLoc=new Point();
			pointLoc.X=ImageDrawCur.GetTextPoint().X;
			pointLoc.Y=ImageDrawCur.GetTextPoint().Y;
			textLocX.Text=pointLoc.X.ToString();
			textLocY.Text=pointLoc.Y.ToString();
			if(Programs.IsEnabled(ProgramName.Pearl)){
				groupBoxFont.IsEnabled=false;//show them the global pearl value, but don't let them change it.
			}
			_imageDrawFontSizeSync=new ImageDrawFontSizeSync(ZoomVal,ScaleF(1),
				actionSetActualText:text => textFontSize.Text=text,
				actionSetApparentText:text => textFontApparent.Text=text);
			textFontSize.Text=ImageDrawCur.GetFontSize().ToString("#.##");//fills Apparent Size through the text changed event
			//ImageDrawCur.ColorBack can be transparent (0,255,255,255), which is transparent white.
			panelColor.ColorBack=ColorOD.ToWpf(ImageDrawCur.ColorDraw);
			if(ImageDrawCur.ColorBack.A==0){//transparent
				checkTransparent.Checked=true;
				panelColorBack.ColorBack=Colors.White;
			}
			else{
				panelColorBack.ColorBack=ColorOD.ToWpf(ImageDrawCur.ColorBack);
			}
		}

		private void textFontSize_TextChanged(object sender,EventArgs e) {
			//Also fires when the other box sets this one; the sync object ignores that, so the boxes never update each other in a loop.
			_imageDrawFontSizeSync?.OnActualTextChanged(textFontSize.Text);
		}

		private void textFontApparent_TextChanged(object sender,EventArgs e) {
			_imageDrawFontSizeSync?.OnApparentTextChanged(textFontApparent.Text);
		}

		private void butColor_Click(object sender, EventArgs e){
			FrmColorDialog frmColorDialog=new FrmColorDialog();
			frmColorDialog.Color=panelColor.ColorBack;
			frmColorDialog.ShowDialog();
			panelColor.ColorBack=frmColorDialog.Color;
		}

		private void butColorBack_Click(object sender,EventArgs e) {
			FrmColorDialog frmColorDialog=new FrmColorDialog();
			frmColorDialog.Color=panelColorBack.ColorBack;
			frmColorDialog.ShowDialog();
			if(frmColorDialog.IsDialogCancel){
				//if Transparent was checked, it can stay checked.
				return;
			}
			checkTransparent.Checked=false;
			panelColorBack.ColorBack=frmColorDialog.Color;
		}

		private void checkTransparent_Click(object sender,EventArgs e) {
			if(checkTransparent.Checked==true){
				panelColorBack.ColorBack=Colors.White;//interpreted by user as transparent
			}
			else{
				//they can also do the same thing by editing the color, and the box will automatically uncheck.
				//It would just remain white like it was before.
			}
		}

		private void butDelete_Click(object sender, EventArgs e){
			if(ImageDrawCur.IsNew){
				IsDialogCancel=true;
				return;
			}
			//Not necessary:
			//if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"Delete?")){
			//	return;
			//}
			ImageDraws.Delete(ImageDrawCur.ImageDrawNum);
			//WasDeleted=true;
			IsDialogOK=true;
		}

		private void FrmImageDrawEdit_PreviewKeyDown(object sender,KeyEventArgs e) {
			if(butSave.IsAltKey(Key.S,e)) {
				butSave_Click(this,new EventArgs());
			}
		}

		private void butSave_Click(object sender,EventArgs e) {
			if(textDrawText.Text==""){
				MsgBox.Show(this,"Text is required.");
				return;
			}
			if(!textLocX.IsValid() 
				|| !textLocY.IsValid() 
				|| !textFontSize.IsValid())
			{
				MsgBox.Show(this,"Please fix data entry errors first.");
				return;
			}
//todo: make sure x and y are within image bounds
//todo: test empty strings in x and y
			Point pointLoc=new Point(textLocX.Value,textLocY.Value);
			System.Drawing.Point point=new System.Drawing.Point();
			point.X=(int)pointLoc.X;
			point.Y=(int)pointLoc.Y;
			ImageDrawCur.SetLocAndText(point,textDrawText.Text);
			if(!Programs.IsEnabled(ProgramName.Pearl)){
				ImageDrawCur.FontSize=(float)textFontSize.Value;
			}
			ImageDrawCur.ColorDraw=ColorOD.FromWpf(panelColor.ColorBack);
			if(checkTransparent.Checked==true){
				ImageDrawCur.ColorBack=System.Drawing.Color.Transparent;
			}
			else{
				ImageDrawCur.ColorBack=ColorOD.FromWpf(panelColorBack.ColorBack);
			}
			if (ImageDrawCur.IsNew){
				ImageDraws.Insert(ImageDrawCur);
			}
			else{
				ImageDraws.Update(ImageDrawCur);
			}
			IsDialogOK=true;
		}

	}
}