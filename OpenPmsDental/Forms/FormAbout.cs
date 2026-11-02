using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using CodeBase;
using DataConnectionBase;
using OpenPmsDentalBusiness;

namespace OpenPmsDental{
	///<summary>Form can be found at Help Tab->About</summary>
	public partial class FormAbout : FormODBase{

		///<summary></summary>
		public FormAbout(){
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormAbout_Load(object sender, System.EventArgs e) {
			//Fork identity (decoupling Phase 4). The product name and the attribution are not translated: they are a name and a legal notice.
			//The Open Dental and reseller logos are no longer shown; this program is not Open Dental's product. Its own logo (branding/logo-256.png)
			//sits at the upper right, in the space beside the version and above the connection box.
			pictureLogo.Image=BrandAssets.GetLogo();
			labelProductName.Text=AboutBoxInfo.ProductName;
			labelAttribution.Text=AboutBoxInfo.Attribution;
			Assembly assemblyProgram=typeof(FormAbout).Assembly;
			labelVersion.Text=Lan.g(this,"Version:")+" "+AboutBoxInfo.GetReleaseVersion(assemblyProgram);
			UpdateHistory updateHistory=AboutBoxInfo.GetUpdateHistory(assemblyProgram);
			if(updateHistory!=null) {
				labelVersion.Text+="  "+Lan.g(this,"Since:")+" "+updateHistory.DateTimeUpdated.ToShortDateString();
			}
			//Open Dental's copyright line, written out: it is never built from the SoftwareName preference, which names this program (and which an
			//office may change), so the line always credits Open Dental. Keeps the trailing year up to date.
			labelCopyright.Text="Open Dental Software "+Lan.g(this,"Copyright 2003-")+DateTime.Now.ToString("yyyy")+", Jordan Sparks, D.M.D.";
			labelMySQLCopyright.Text=Lan.g(this,"MySQL - Copyright 1995-")+DateTime.Now.ToString("yyyy")+Lan.g(this,", www.mysql.com");
			labelMariaDBCopyright.Text=Lan.g(this,"MariaDB - Copyright 2009-")+DateTime.Now.ToString("yyyy")+Lan.g(this,", www.mariadb.com");
			//Database Server----------------------------------------------------------		
			List<string> listServiceInfo=Computers.GetServiceInfo();
			labelName.Text+=listServiceInfo[2].ToString();//MiscData.GetODServer();//server name
			labelService.Text+=listServiceInfo[0].ToString();//service name
			labelMySqlVersion.Text+=listServiceInfo[3].ToString();//service version
			labelServComment.Text+=listServiceInfo[1].ToString();//service comment
			labelMachineName.Text+=Environment.MachineName.ToUpper();//current client or remote application machine name
			labelDatabase.Text+=listServiceInfo[4].ToString();//database name
			Plugins.HookAddCode(this,"FormAbout.Load_end");
		}

		private void butDiagnostics_Click(object sender,EventArgs e) {
			//25.4: more details (date and time, hardware). Gathered on this computer and from this office's database server only (LocalDiagnostics).
			string diagnostics=LocalDiagnostics.GetDiagnostics(FormOpenDental.PatNumCur);
			using MsgBoxCopyPaste msgBoxCopyPaste=new MsgBoxCopyPaste(diagnostics);
			msgBoxCopyPaste.Text=Lans.g(this,"Diagnostics");
			msgBoxCopyPaste.ShowDialog();
		}

		private void butLicense_Click(object sender,EventArgs e) {
			using FormLicense formLicense=new FormLicense();
			formLicense.ShowDialog();
		}

	}

	///<summary>The fork identity the About box shows: product name, attribution to Open Dental, and release version. Kept apart from the form
	///so it can be tested without a database or a window (except the methods that say they read the preferences or the database).</summary>
	public static class AboutBoxInfo {
		///<summary>The product name. The assemblies carry its code name, OpenPmsDental. The MainWindowTitle and SoftwareName preferences keep
		///their stored values; GetDisplayName shows this name in place of Open Dental's defaults.</summary>
		public const string ProductName=CodeBase.VendorDirect.ProductIdentity.ProductName;
		///<summary>The values of the MainWindowTitle and SoftwareName preferences that are shown as ProductName: Open Dental's defaults ("Open
		///Dental", "Open Dental Software"; an adopted database keeps them) and the two values the Benco branding switch, since removed from
		///FrmBencoSetup, wrote. The preference rows are never changed (adoption may change no other row, AdoptionRowCheck).</summary>
		private static readonly string[] _arrayDefaultProductNames=new string[] {
			"Open Dental",
			"Open Dental Software",
			"Benco Practice Management powered by Open Dental",
			"Benco Practice Management",
		};
		///<summary>The attribution required by the owner, also in README.md and NOTICE. Never translated or shortened.</summary>
		public const string Attribution="Open PMS - Dental is a fork of Open Dental (c) Open Dental Software, Inc. and contributors, licensed under "
			+"the GNU GPL v2. It is not affiliated with or endorsed by Open Dental Software, Inc.";

		///<summary>The release version of the program: the assembly's AssemblyInformationalVersion when it is set, otherwise its assembly version.
		///Returns "0.0" if neither is available.</summary>
		public static string GetReleaseVersion(Assembly assembly) {
			if(assembly==null) {
				return "0.0";
			}
			AssemblyInformationalVersionAttribute attribute=null;
			try {
				attribute=(AssemblyInformationalVersionAttribute)System.Attribute.GetCustomAttribute(assembly,typeof(AssemblyInformationalVersionAttribute));
			}
			catch(Exception) {
				//Fall back to the assembly version.
			}
			if(attribute!=null && !string.IsNullOrWhiteSpace(attribute.InformationalVersion)) {
				return attribute.InformationalVersion.Trim();
			}
			return GetAssemblyVersion(assembly);
		}

		///<summary>The assembly version, for example "24.3.63.0", or "0.0" if it can't be read.</summary>
		public static string GetAssemblyVersion(Assembly assembly) {
			System.Version version=assembly?.GetName().Version;
			return (version==null) ? "0.0" : version.ToString();
		}

		///<summary>The program versions an updatehistory row can be stored under for this build, in the order they are looked up: the release
		///version (PrefL writes it when the update server publishes a release), then the compatibility version, the assembly version (the legacy
		///conversion chain and the development database tool write it). Without duplicates.</summary>
		public static List<string> GetUpdateHistoryVersions(Assembly assembly) {
			List<string> listVersions=new List<string>();
			listVersions.Add(GetReleaseVersion(assembly));
			string versionAssembly=GetAssemblyVersion(assembly);
			if(!listVersions.Contains(versionAssembly)) {
				listVersions.Add(versionAssembly);
			}
			return listVersions;
		}

		///<summary>The updatehistory row that says since when this build's version is installed, or null if there is none (see
		///GetUpdateHistoryVersions). Reads the database.</summary>
		public static UpdateHistory GetUpdateHistory(Assembly assembly) {
			List<string> listVersions=GetUpdateHistoryVersions(assembly);
			for(int i=0;i<listVersions.Count;i++) {
				UpdateHistory updateHistory=UpdateHistories.GetForVersion(listVersions[i]);
				if(updateHistory!=null) {
					return updateHistory;
				}
			}
			return null;
		}

		///<summary>True if the value is one of the stored defaults that are shown as the product name (Open Dental's defaults of MainWindowTitle
		///and SoftwareName, and the removed Benco branding values). Ignores case and surrounding spaces.</summary>
		public static bool IsDefaultProductName(string prefValue) {
			if(prefValue==null) {
				return false;
			}
			string trimmed=prefValue.Trim();
			return _arrayDefaultProductNames.Any(x => string.Equals(x,trimmed,StringComparison.OrdinalIgnoreCase));
		}

		///<summary>The name to show for a stored MainWindowTitle or SoftwareName value (decision D2): ProductName in place of a default
		///(IsDefaultProductName), otherwise the value as stored, so an office's own title is kept. Never changes the preference. Null gives "".</summary>
		public static string GetDisplayName(string prefValue) {
			if(IsDefaultProductName(prefValue)) {
				return ProductName;
			}
			return prefValue??"";
		}

		///<summary>The main window title to show: the MainWindowTitle preference through GetDisplayName. Reads the preference cache.</summary>
		public static string GetMainWindowTitle() {
			return GetDisplayName(PrefC.GetString(PrefName.MainWindowTitle));
		}

		///<summary>The program name to show in messages: the SoftwareName preference through GetDisplayName. Reads the preference cache.</summary>
		public static string GetSoftwareName() {
			return GetDisplayName(PrefC.GetString(PrefName.SoftwareName));
		}
	}
}
