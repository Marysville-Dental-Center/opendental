// Sets up the PDF library (PDFsharp 6.5) once per program: selects its AGPL licence and lets fonts that are not installed fall back to a font
// that is, as they did with PDFsharp 1.2.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using PdfSharp;
using PdfSharp.Fonts;

namespace OpenPmsDentalBusiness {
	///<summary>PDFsharp 6.5 needs two things before it is used. It refuses to create or read a PDF until the program has said which licence it
	///uses it under: this program uses it under the GNU AGPL (see THIRD-PARTY-NOTICES.txt), selected with PdfSharpLicense.SetAgplLicense. That
	///needs no key, draws nothing on the pages and opens no network connection (tests/PdfLibraryTests); the library only writes "(AGPL license)"
	///after its name in each PDF's Producer and Creator properties. And it throws when a font family is not installed, where PDFsharp 1.2 drew the
	///text in the font GDI+ substitutes; FontResolverWithFallback restores that. Every program calls Init before its first PDF; calling it again is
	///harmless. The font part must be set before the library draws any text, so Init belongs at program start.</summary>
	public class PdfSharpSetup {
		///<summary>The family drawn for one that is not installed: the one GDI+ substitutes for an unknown family, so text looks as it did with
		///PDFsharp 1.2.</summary>
		public const string FALLBACK_FONT_FAMILY="Microsoft Sans Serif";
		///<summary>Used when even FALLBACK_FONT_FAMILY is missing.</summary>
		public const string FALLBACK_FONT_FAMILY_LAST="Arial";
		private static readonly object _lock=new object();
		private static bool _isInitialized;

		///<summary>Selects the AGPL licence and installs the font fallback. Safe to call from any thread and more than once.</summary>
		public static void Init() {
			if(_isInitialized) {
				return;
			}
			lock(_lock) {
				if(_isInitialized) {
					return;
				}
				PdfSharpLicense.SetAgplLicense();
				if(!(GlobalFontSettings.FontResolver is FontResolverWithFallback)) {
					try {
						GlobalFontSettings.FontResolver=new FontResolverWithFallback();
					}
					catch(InvalidOperationException) {
						//The library already drew text before Init (it does not allow a new resolver then). Installed fonts still work; a missing one
						//throws, as it would without the fallback.
					}
				}
				_isInitialized=true;
			}
		}
	}

	///<summary>Resolves fonts as PDFsharp does by itself (GDI+, from the fonts installed in Windows), except that a family that is not installed
	///gets PdfSharpSetup.FALLBACK_FONT_FAMILY in the same style instead of an exception. Every face comes from Windows, so GetFont is never asked
	///for font data.</summary>
	public class FontResolverWithFallback:IFontResolver {
		public FontResolverInfo ResolveTypeface(string familyName,bool isBold,bool isItalic) {
			FontResolverInfo fontResolverInfo=null;
			if(!string.IsNullOrWhiteSpace(familyName)) {
				fontResolverInfo=PlatformFontResolver.ResolveTypeface(familyName,isBold,isItalic);
			}
			if(fontResolverInfo==null) {
				fontResolverInfo=PlatformFontResolver.ResolveTypeface(PdfSharpSetup.FALLBACK_FONT_FAMILY,isBold,isItalic);
			}
			if(fontResolverInfo==null) {
				fontResolverInfo=PlatformFontResolver.ResolveTypeface(PdfSharpSetup.FALLBACK_FONT_FAMILY_LAST,isBold,isItalic);
			}
			return fontResolverInfo;
		}

		public byte[] GetFont(string faceName) {
			//Only called for faces this resolver made up itself, and it makes up none.
			return null;
		}
	}
}
