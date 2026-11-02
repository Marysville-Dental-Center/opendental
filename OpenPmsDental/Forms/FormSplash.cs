using System;
using OpenPmsDentalBusiness;
using CodeBase;
using System.IO;
using System.Drawing;
using System.Windows.Forms;

namespace OpenPmsDental {
	///<summary>A splash screen to show when the program launches. The background is branding/splash.png (in FormSplash.resx), with a
	///copyright line that points to Help > About and the GPL notice. An office's own Splash.jpg in the program folder takes its place.</summary>
	public partial class FormSplash : Form {

		///<summary>Launches a splash screen.</summary>
		public FormSplash() {
			InitializeComponent();
			Lan.F(this);
		}

		private void FormSplash_Load(object sender,EventArgs e) {
			//Open PMS - Dental: no reseller (Benco) or country (Canada) splash; vendor branding is not shipped.
			if(File.Exists(Directory.GetCurrentDirectory()+@"\Splash.jpg")) {
				BackgroundImage=new Bitmap(Directory.GetCurrentDirectory()+@"\Splash.jpg");
			}
			if(Plugins.PluginsAreLoaded) {
				Plugins.HookAddCode(this,"FormSplash.FormSplash_Load_end");
			}
		}
  }
}
