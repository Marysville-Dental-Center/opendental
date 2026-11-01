// Synthetic sample text in several scripts for the M2-SHEETS tests. Written as escapes so the test sources stay plain ASCII.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2SHEETSTests {
	public static class Samples {
		///<summary>"Privet" (hello) in Russian.</summary>
		public const string Russian="Привет";
		///<summary>"Geia sou" (hello) in Greek.</summary>
		public const string Greek="Γειά σου";
		///<summary>"Ni hao shijie" (hello world) in Chinese.</summary>
		public const string Chinese="你好世界";
		///<summary>"Konnichiwa" (hello) in Japanese hiragana, then the kanji for "world".</summary>
		public const string Japanese="こんにちは世界";
		///<summary>"Annyeonghaseyo" (hello) in Korean Hangul syllables.</summary>
		public const string Korean="안녕하세요";
		///<summary>"Shalom" in Hebrew.</summary>
		public const string Hebrew="שלום";
		///<summary>"Marhaba" (hello) in Arabic.</summary>
		public const string Arabic="مرحبا";
		///<summary>"Namaste" in Hindi (Devanagari, with a conjunct).</summary>
		public const string Hindi="नमस्ते";
		///<summary>"Sawasdee" (hello) in Thai.</summary>
		public const string Thai="สวัสดี";
		///<summary>"Gamarjoba" (hello) in Georgian.</summary>
		public const string Georgian="გამარჯობა";
		///<summary>Latin text with Latin-1 and Windows-1252 characters, as Spanish and French sheets have.</summary>
		public const string LatinWinAnsi="Señor Café €5 “ok” — done";

		public static void UseUsEnglish() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
		}

		///<summary>Makes the test inconclusive when one of the fonts is not installed on this computer.</summary>
		public static void RequireFonts(params string[] arrayFontNames) {
			foreach(string fontName in arrayFontNames) {
				if(!SheetFonts.IsFontInstalled(fontName)) {
					Assert.Inconclusive("The font "+fontName+" is not installed on this computer.");
				}
			}
		}

		///<summary>A text field like the sheet filler makes.</summary>
		public static SheetField TextField(string text,string fontName="Arial",HorizontalAlignment textAlign=HorizontalAlignment.Left,
			SheetFieldType sheetFieldType=SheetFieldType.StaticText,int xPos=50,int yPos=50,int width=400,int height=40)
		{
			return new SheetField {
				FieldType=sheetFieldType,
				FieldName=(sheetFieldType==SheetFieldType.InputField ? "misc" : ""),
				FieldValue=text,
				FontName=fontName,
				FontSize=11f,
				XPos=xPos,
				YPos=yPos,
				Width=width,
				Height=height,
				TextAlign=textAlign,
				ItemColor=Color.Black,
				RadioButtonValue="",
				RadioButtonGroup="",
				ReportableName="",
				UiLabelMobile="",
				UiLabelMobileRadioButton="",
				GrowthBehavior=GrowthBehaviorEnum.None,
			};
		}
	}
}
