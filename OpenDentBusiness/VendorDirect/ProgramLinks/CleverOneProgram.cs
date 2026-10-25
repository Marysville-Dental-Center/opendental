// V-BRIDGE2 (Open Dental 25.4, v25_4-bridges-cleverone): the Clever One by VATECH program link, a local launch of VATECH's newer imaging program.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using CodeBase;

namespace OpenDentBusiness.VendorDirect.ProgramLinks {
	///<summary>The CleverOne program link. The row is inserted at runtime with Enabled=0 (no ConvertDatabases or fork migration step), with the setup
	///the public setup page lists: the program path, the "Enter 0 to use PatientNum, or 1 to use ChartNum" property (0) and a Chart module button
	///"Clever One". Its button shows only after the office enables it.
	///How Clever One takes the patient is not public (VERIFY with VATECH). The fork reuses the EzDent-i hand-off, the closest VATECH precedent: a
	///linkage.xml file next to the program, then the program is started. Unlike EzDent-i, no Social Security number is written.</summary>
	public static class CleverOneProgram {
		///<summary>The program row's description, shown in Setup > Program Links.</summary>
		public const string ProgDesc="Clever One by VATECH";
		///<summary>The default "Path of file to open", from the public setup page.</summary>
		public const string DefaultPath=@"C:\Program Files\VATECH\CleverOne\Bin\CleverOne.exe";
		///<summary>The default text on the toolbar button.</summary>
		public const string DefaultButtonText="Clever One";
		///<summary>The toolbar the button is added to when the row is first inserted.</summary>
		public const EnumToolBar DefaultToolBar=EnumToolBar.ChartModule;
		///<summary>0 (default) sends the PatNum as the chart number, 1 sends the patient's ChartNumber.</summary>
		public const string PropPatOrChartNum=ProgramProperties.PropertyDescs.PatOrChartNum;
		///<summary>The hand-off file, written into the folder of the program.</summary>
		public const string LinkageFileName="linkage.xml";

		///<summary>Inserts the program row (Enabled=0) if it is missing, with the default path and the Chart module button, and the patient id property
		///if it is missing. An existing row's path and buttons are never changed. Returns true if anything was inserted. The caller refreshes the
		///caches and signals InvalidType.Programs and InvalidType.ToolButsAndMounts.</summary>
		public static bool EnsureProgram() {
			Meth.NoCheckMiddleTierRole();
			bool isInsertedProgram=Programs.InsertIfMissing(ProgramName.CleverOne,ProgDesc);
			bool isInserted=isInsertedProgram;
			Program program=Programs.GetCur(ProgramName.CleverOne);
			if(program==null || isInsertedProgram) {
				Programs.RefreshCache();
				program=Programs.GetCur(ProgramName.CleverOne);
			}
			if(program==null) {
				return isInserted;
			}
			if(isInsertedProgram) {
				Program programOld=program.Copy();
				program.Path=DefaultPath;
				Programs.Update(program,programOld);
				ToolButItem toolButItem=new ToolButItem();
				toolButItem.ProgramNum=program.ProgramNum;
				toolButItem.ToolBar=DefaultToolBar;
				toolButItem.ButtonText=DefaultButtonText;
				ToolButItems.Insert(toolButItem);
			}
			long programNum=program.ProgramNum;
			if(!ProgramProperties.GetWhere(x => x.ProgramNum==programNum && x.PropertyDesc==PropPatOrChartNum && x.ClinicNum==0).Any()) {
				ProgramProperties.InsertIfMissing(programNum,PropPatOrChartNum,0,"0",false,false);
				isInserted=true;
			}
			return isInserted;
		}

		///<summary>True if the property asks for the ChartNumber ("1"). Anything else, blank included, sends the PatNum.</summary>
		public static bool UsesChartNumber(string propertyValue) {
			Meth.NoCheckMiddleTierRole();
			return (propertyValue??"").Trim()=="1";
		}

		///<summary>The full path of linkage.xml for the program path: the program's folder. Blank for a blank path.</summary>
		public static string GetLinkageFilePath(string programPath) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrWhiteSpace(programPath)) {
				return "";
			}
			string dir=Path.GetDirectoryName(programPath);
			return ODFileUtils.CombinePaths(dir,LinkageFileName);
		}

		///<summary>The linkage.xml text for the patient, in the EzDent-i layout: a LinkageParameter element holding a Patient element with LastName,
		///FirstName and ChartNumber attributes (PatNum, or ChartNumber when useChartNumber), and Birthday (dd/MM/yyyy whatever the regional settings),
		///Address, ZipCode, Phone, Mobile and Gender elements. No SocialID element. No XML declaration, three-space indent, CRLF line ends.</summary>
		public static string BuildLinkageXml(Patient patient,bool useChartNumber) {
			Meth.NoCheckMiddleTierRole();
			if(patient==null) {
				throw new ArgumentNullException(nameof(patient));
			}
			StringBuilder stringBuilder=new StringBuilder();
			XmlWriterSettings xmlWriterSettings=new XmlWriterSettings();
			xmlWriterSettings.Indent=true;
			xmlWriterSettings.IndentChars="   ";
			xmlWriterSettings.NewLineChars="\r\n";
			xmlWriterSettings.OmitXmlDeclaration=true;
			using(XmlWriter xmlWriter=XmlWriter.Create(stringBuilder,xmlWriterSettings)) {
				xmlWriter.WriteStartElement("LinkageParameter");
				xmlWriter.WriteStartElement("Patient");
				xmlWriter.WriteAttributeString("LastName",patient.LName??"");
				xmlWriter.WriteAttributeString("FirstName",patient.FName??"");
				if(useChartNumber) {
					xmlWriter.WriteAttributeString("ChartNumber",patient.ChartNumber??"");
				}
				else {
					xmlWriter.WriteAttributeString("ChartNumber",patient.PatNum.ToString());
				}
				xmlWriter.WriteElementString("Birthday",patient.Birthdate.ToString("dd'/'MM'/'yyyy",System.Globalization.CultureInfo.InvariantCulture));
				xmlWriter.WriteElementString("Address",GetAddressLine(patient));
				xmlWriter.WriteElementString("ZipCode",patient.Zip??"");
				xmlWriter.WriteElementString("Phone",patient.HmPhone??"");
				xmlWriter.WriteElementString("Mobile",patient.WirelessPhone??"");
				xmlWriter.WriteElementString("Gender",patient.Gender==PatientGender.Female ? "Female" : "Male");
				xmlWriter.WriteEndElement();//Patient
				xmlWriter.WriteEndElement();//LinkageParameter
			}
			return stringBuilder.ToString();
		}

		///<summary>"Address, Address2, City, State", as the EzDent-i bridge writes it (Address2 only when not blank).</summary>
		public static string GetAddressLine(Patient patient) {
			Meth.NoCheckMiddleTierRole();
			string address=patient.Address??"";
			if(!string.IsNullOrEmpty(patient.Address2)) {
				address+=", "+patient.Address2;
			}
			address+=", "+(patient.City??"")+", "+(patient.State??"");
			return address;
		}
	}
}
