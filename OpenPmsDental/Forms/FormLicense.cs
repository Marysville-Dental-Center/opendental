using System;
using System.Collections.Generic;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using CodeBase;

namespace OpenPmsDental{
	/// <summary></summary>
	public partial class FormLicense : FormODBase {

		private string _selectedLicense;

		///<summary>selectedLicense is the text of the license to select. If it is null or not in the list, the program's own license (the GNU GPL,
		///LicenseList.GetProgramLicenseText) is selected.</summary>
		public FormLicense(string selectedLicense=null) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_selectedLicense=LicenseList.GetProgramLicenseText();
			if(!String.IsNullOrWhiteSpace(selectedLicense)){
				_selectedLicense=selectedLicense;
			}
		}

		private void FormLicense_Load(object sender,EventArgs e) {
			FillListBoxLicense();
			for(int i = 0; i < listBoxLicense.Items.Count; i++) {
				if((string)listBoxLicense.Items.GetObjectAt(i)==_selectedLicense) {
					listBoxLicense.SetSelected(i,true);
					return;
				}
			}
			listBoxLicense.SetSelected(0,true);//The program's own license.
		}

		///<summary>Fills the listbox with licenses and the license text as a tag, from LicenseList (shared with FormRegistrationKey).</summary>
		private void FillListBoxLicense() {
			List<LicenseEntry> listLicenseEntries=LicenseList.GetEntries();
			for(int i=0;i<listLicenseEntries.Count;i++) {
				listBoxLicense.Items.Add(listLicenseEntries[i].Name,listLicenseEntries[i].Text);
			}
		}

		private void listLicense_SelectedIndexChanged(object sender,EventArgs e) {
			textLicense.Text=listBoxLicense.GetSelected<string>();
		}

	}

	///<summary>One entry of the program's license list: the name shown in the list and the text shown for it.</summary>
	public class LicenseEntry {
		public string Name;
		public string Text;

		public LicenseEntry(string name,string text) {
			Name=name;
			Text=text;
		}
	}

	///<summary>One section of THIRD-PARTY-NOTICES.txt: a component ("--- Component: name") or a license text ("--- License: title").</summary>
	public class NoticeSection {
		///<summary>True for a component, false for a license text.</summary>
		public bool IsComponent;
		///<summary>The component name or the license title, as written after the colon of the header line.</summary>
		public string Title;
		///<summary>The lines after the header up to the next header, trimmed of blank lines at both ends, joined with CRLF.</summary>
		public string Body;

		///<summary>The value of a "Field:" line of a component, including its continuation lines (indented lines that follow it), joined with
		///spaces. "" when the section has no such field.</summary>
		public string GetField(string field) {
			string[] arrayLines=Body.Split(new[] { "\r\n" },StringSplitOptions.None);
			List<string> listParts=new List<string>();
			bool isInField=false;
			for(int i=0;i<arrayLines.Length;i++) {
				string line=arrayLines[i];
				if(isInField) {
					if(line.Length>0 && char.IsWhiteSpace(line[0]) && line.Trim()!="") {
						listParts.Add(line.Trim());
						continue;
					}
					break;
				}
				if(line.StartsWith(field+":")) {
					isInField=true;
					listParts.Add(line.Substring(field.Length+1).Trim());
				}
			}
			return string.Join(" ",listParts);
		}
	}

	///<summary>The license list of the program, shown by FormLicense (Help, About, Licenses) and FormRegistrationKey (package REL-FILES). The program's
	///own license comes first: the GNU GPL, version 2 or (at the user's option) any later version, as the source headers say. Then the third-party
	///notices (THIRD-PARTY-NOTICES.txt in the repository root, embedded as the ThirdPartyNotices resource), and one entry for each component listed
	///there, in the file's order: the component's notice, then its license text (the project's own text kept from Open Dental's list when there
	///is one, otherwise the standard text in the notices file that its License field names). Open Dental's commercial registration-key terms,
	///the end-user licenses of removed features and the texts of components that do not ship are not in the list.</summary>
	public static class LicenseList {
		///<summary>The name of the first entry: the license of this program.</summary>
		public const string NameProgram="Open PMS - Dental (GNU GPL v2 or later)";
		///<summary>The name of the entry that shows THIRD-PARTY-NOTICES.txt whole.</summary>
		public const string NameThirdPartyNotices="Third-party notices";
		private const string HEADER_COMPONENT="--- Component: ";
		private const string HEADER_LICENSE="--- License: ";

		///<summary>The text of the first entry: the attribution, the GPL notice of the source headers ("version 2 of the License, or (at your
		///option) any later version"), how the program is distributed with PDFsharp and MigraDoc (GNU GPL version 3 with their GNU AGPL version 3,
		///whose network clause then applies to the combination as a whole), the GPL version 2 text (Resources\GPL.txt; the same license as LICENSE in the repository root) and the GPL version 3 text
		///(LICENSE-GPL-3.0.txt).</summary>
		public static string GetProgramLicenseText() {
			return AboutBoxInfo.ProductName+"\r\n\r\n"
				+AboutBoxInfo.Attribution+"\r\n\r\n"
				+"This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License as "
				+"published by the Free Software Foundation; either version 2 of the License, or (at your option) any later version.\r\n\r\n"
				+"This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of "
				+"MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.\r\n\r\n"
				+"As distributed, the program includes PDFsharp and MigraDoc, which are licensed under the GNU Affero General Public License, "
				+"version 3. The program as a whole is therefore distributed under the terms of the GNU General Public License, version 3, which "
				+"\"any later version\" above allows, and PDFsharp and MigraDoc keep the terms of the GNU Affero General Public License, version 3 "
				+"(section 13 of each license). By the same section 13, the GNU Affero General Public License's requirements on interaction "
				+"through a network apply to the combination as a whole; NOTICE in the program folder says what that means for a modified version "
				+"that users reach over a network. The text of version 3 follows the text of version 2 below; both are in the program folder, as "
				+"LICENSE-GPL-3.0.txt and LICENSE-AGPL-3.0.txt.\r\n\r\n"
				+"Third-party components keep their own licenses: see \""+NameThirdPartyNotices+"\" and the entries after it.\r\n\r\n"
				+NormalizeNewLines(Properties.Resources.GPL)+"\r\n\r\n"
				+NormalizeNewLines(Properties.Resources.LicenseGpl3);
		}

		///<summary>The project's own license texts kept from Open Dental's list (OpenPmsDental\Resources), by the component name used in
		///THIRD-PARTY-NOTICES.txt. PDFsharp and MigraDoc show empira's notice for version 6.5 and the GNU AGPL version 3 in full.</summary>
		public static Dictionary<string,string> GetVendorTexts() {
			Dictionary<string,string> dictVendorTexts=new Dictionary<string,string>();
			dictVendorTexts.Add("Newtonsoft.Json",Properties.Resources.NewtonSoft_Json);
			dictVendorTexts.Add("SSH.NET",Properties.Resources.SshNet);
			dictVendorTexts.Add("PDFsharp",Properties.Resources.PdfSharp.TrimEnd()+"\r\n\r\n"+Properties.Resources.LicenseAgpl3);
			dictVendorTexts.Add("MigraDoc",Properties.Resources.MigraDoc.TrimEnd()+"\r\n\r\n"+Properties.Resources.LicenseAgpl3);
			dictVendorTexts.Add("SharpDX",Properties.Resources.SharpDX);
			dictVendorTexts.Add("Tao Framework",Properties.Resources.Tao_OpenGL);
			dictVendorTexts.Add("SharpSSH",Properties.Resources.Tamir);
			dictVendorTexts.Add("Mentalis.org Security Library",Properties.Resources.Mentalis);
			dictVendorTexts.Add("NHunspell and Hunspell",Properties.Resources.NHunSpell);
			dictVendorTexts.Add("NDde",Properties.Resources.NDde);
			dictVendorTexts.Add("TWAIN",Properties.Resources.Twain);
			dictVendorTexts.Add("Pearl API notice",Properties.Resources.Pearl);
			return dictVendorTexts;
		}

		///<summary>The entries of the license list in display order: the program's license, the third-party notices, then one entry per component of
		///THIRD-PARTY-NOTICES.txt.</summary>
		public static List<LicenseEntry> GetEntries() {
			List<LicenseEntry> listLicenseEntries=new List<LicenseEntry>();
			listLicenseEntries.Add(new LicenseEntry(NameProgram,GetProgramLicenseText()));
			string notices=NormalizeNewLines(Properties.Resources.ThirdPartyNotices);
			listLicenseEntries.Add(new LicenseEntry(NameThirdPartyNotices,notices));
			List<NoticeSection> listNoticeSections=ParseNotices(notices);
			List<NoticeSection> listLicenseSections=listNoticeSections.FindAll(x => !x.IsComponent);
			Dictionary<string,string> dictVendorTexts=GetVendorTexts();
			foreach(NoticeSection noticeSectionComponent in listNoticeSections.FindAll(x => x.IsComponent)) {
				string text=noticeSectionComponent.Title+"\r\n\r\n"+noticeSectionComponent.Body;
				string licenseText=GetLicenseText(noticeSectionComponent,listLicenseSections,dictVendorTexts);
				if(licenseText!="") {
					text+="\r\n\r\n"+licenseText;
				}
				listLicenseEntries.Add(new LicenseEntry(noticeSectionComponent.Title,text));
			}
			return listLicenseEntries;
		}

		///<summary>The license text shown under a component: the project's own text when there is one, otherwise the standard text that its License
		///field starts with ("MIT License", "Apache License 2.0", ...), with its title. "" when there is neither (the notice names the license's
		///address instead).</summary>
		public static string GetLicenseText(NoticeSection noticeSectionComponent,List<NoticeSection> listLicenseSections,
			Dictionary<string,string> dictVendorTexts)
		{
			if(dictVendorTexts.TryGetValue(noticeSectionComponent.Title,out string vendorText)) {
				return NormalizeNewLines(vendorText).Trim();
			}
			string license=noticeSectionComponent.GetField("License");
			NoticeSection noticeSectionLicense=listLicenseSections
				.Where(x => license.StartsWith(x.Title,StringComparison.Ordinal))
				.OrderByDescending(x => x.Title.Length)
				.FirstOrDefault();
			if(noticeSectionLicense==null) {
				return "";
			}
			return noticeSectionLicense.Title+"\r\n\r\n"+noticeSectionLicense.Body;
		}

		///<summary>Splits THIRD-PARTY-NOTICES.txt into its component and license sections. Text before the first section (the introduction) is not
		///a section.</summary>
		public static List<NoticeSection> ParseNotices(string notices) {
			List<NoticeSection> listNoticeSections=new List<NoticeSection>();
			string[] arrayLines=NormalizeNewLines(notices).Split(new[] { "\r\n" },StringSplitOptions.None);
			NoticeSection noticeSectionCur=null;
			List<string> listBodyLines=new List<string>();
			for(int i=0;i<=arrayLines.Length;i++) {
				bool isEnd=(i==arrayLines.Length);
				string line=isEnd ? "" : arrayLines[i];
				bool isHeader=!isEnd && (line.StartsWith(HEADER_COMPONENT) || line.StartsWith(HEADER_LICENSE));
				if(isEnd || isHeader) {
					if(noticeSectionCur!=null) {
						noticeSectionCur.Body=TrimBlankLines(listBodyLines);
						listNoticeSections.Add(noticeSectionCur);
					}
					if(isEnd) {
						break;
					}
					noticeSectionCur=new NoticeSection();
					noticeSectionCur.IsComponent=line.StartsWith(HEADER_COMPONENT);
					noticeSectionCur.Title=line.Substring(noticeSectionCur.IsComponent ? HEADER_COMPONENT.Length : HEADER_LICENSE.Length).Trim();
					listBodyLines=new List<string>();
					continue;
				}
				listBodyLines.Add(line.TrimEnd());
			}
			return listNoticeSections;
		}

		private static string TrimBlankLines(List<string> listLines) {
			int indexFirst=listLines.FindIndex(x => x.Trim()!="");
			if(indexFirst<0) {
				return "";
			}
			int indexLast=listLines.FindLastIndex(x => x.Trim()!="");
			return string.Join("\r\n",listLines.Skip(indexFirst).Take(indexLast-indexFirst+1));
		}

		///<summary>CRLF line breaks, which the RichTextBox shows as single line breaks. Null gives "".</summary>
		private static string NormalizeNewLines(string text) {
			if(text==null) {
				return "";
			}
			return text.Replace("\r\n","\n").Replace("\r","\n").Replace("\n","\r\n");
		}
	}
}
