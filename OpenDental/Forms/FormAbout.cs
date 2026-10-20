using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using CodeBase;
using DataConnectionBase;
using OpenDentBusiness;
//using mshtml;

namespace OpenDental{
	///<summary>Form can be found at Help Tab->About</summary>
	public partial class FormAbout : FormODBase{

		///<summary></summary>
		public FormAbout(){
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormAbout_Load(object sender, System.EventArgs e) {
			string softwareName=PrefC.GetString(PrefName.SoftwareName);
			//Fork identity (decoupling Phase 4). The product name and the attribution are not translated: they are a name and a legal notice.
			//The Open Dental and reseller logos are no longer shown; this program is not Open Dental's product.
			labelProductName.Text=AboutBoxInfo.ProductName;
			labelAttribution.Text=AboutBoxInfo.Attribution;
			Assembly assemblyProgram=typeof(FormAbout).Assembly;
			labelVersion.Text=Lan.g(this,"Version:")+" "+AboutBoxInfo.GetReleaseVersion(assemblyProgram);
			//UpdateHistory rows are keyed by the assembly version (see PrefL), not by the release version.
			UpdateHistory updateHistory=UpdateHistories.GetForVersion(AboutBoxInfo.GetAssemblyVersion(assemblyProgram));
			if(updateHistory!=null) {
				labelVersion.Text+="  "+Lan.g(this,"Since:")+" "+updateHistory.DateTimeUpdated.ToShortDateString();
			}
			//keeps the trailing year up to date
			labelCopyright.Text=softwareName+" "+Lan.g(this,"Copyright 2003-")+DateTime.Now.ToString("yyyy")+", Jordan Sparks, D.M.D.";
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
			string diagnostics = BugSubmissions.GetDiagnostics(FormOpenDental.PatNumCur);
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
	///so it can be tested without a database or a window.</summary>
	public static class AboutBoxInfo {
		///<summary>The product name. The window titles, assemblies and the SoftwareName preference keep their Open Dental names until the final
		///cosmetic rename step.</summary>
		public const string ProductName=CodeBase.VendorDirect.ProductIdentity.ProductName;
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
	}
}
