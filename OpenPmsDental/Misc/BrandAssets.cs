// The Open PMS - Dental logo and program icon, embedded once in this exe from the repository's branding folder (branding/README.md).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.IO;

namespace OpenPmsDental {
	///<summary>The program icon and logo. OpenPmsDental.csproj embeds branding/app.ico and branding/logo-256.png once, as manifest resources
	///with the names below; no form carries its own copy (FormODBase sets the icon of every window).</summary>
	public static class BrandAssets {
		///<summary>Manifest resource name of branding/app.ico: 16, 20, 24, 32, 40, 48, 64, 128 and 256 pixels.</summary>
		public const string AppIconResourceName="OpenPmsDental.Branding.app.ico";
		///<summary>Manifest resource name of branding/logo-256.png.</summary>
		public const string LogoResourceName="OpenPmsDental.Branding.logo-256.png";
		private static readonly Lazy<Icon> _lazyIconApp=new Lazy<Icon>(() => {
			using Stream stream=OpenResource(AppIconResourceName);
			//At the system icon size (SM_CXICON, 32 at 96 dpi), the size Windows asks a window's big icon to have; WinForms picks the small icon
			//(SM_CXSMICON) from the same images, and both scale with the dpi because every size up to 64 pixels is there.
			return new Icon(stream);
		});

		///<summary>The program icon (branding/app.ico), one instance shared by every window: the window icon, the taskbar and Alt+Tab. Never
		///dispose it.</summary>
		public static Icon AppIcon {
			get {
				return _lazyIconApp.Value;
			}
		}

		///<summary>A new copy of the logo (branding/logo-256.png, 256x256 with transparent corners). The caller owns it.</summary>
		public static Bitmap GetLogo() {
			using Stream stream=OpenResource(LogoResourceName);
			using Image image=Image.FromStream(stream);
			return new Bitmap(image);
		}

		///<summary>The bytes of one of the embedded branding files, exactly as they are in the branding folder.</summary>
		public static byte[] GetResourceBytes(string resourceName) {
			using Stream stream=OpenResource(resourceName);
			using MemoryStream memoryStream=new MemoryStream();
			stream.CopyTo(memoryStream);
			return memoryStream.ToArray();
		}

		private static Stream OpenResource(string resourceName) {
			Stream stream=typeof(BrandAssets).Assembly.GetManifestResourceStream(resourceName);
			if(stream==null) {
				throw new InvalidOperationException("The resource "+resourceName+" is missing from "+typeof(BrandAssets).Assembly.GetName().Name+".");
			}
			return stream;
		}
	}
}
