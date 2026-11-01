// V-BRIDGE2 (Open Dental 25.4, v25_4-family-clerri): the Clerri program link. Launch only: a Family module button opens the office's Clerri
// dashboard in the default browser. Open Dental's data sync with Clerri runs through its eConnector and API, which this fork removed; none is built.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CodeBase;

namespace OpenDentBusiness.VendorDirect.ProgramLinks {
	///<summary>The Clerri program link: a program row inserted at runtime with Enabled=0 (no ConvertDatabases or fork migration step), a Family module
	///button, the office's Client Key per clinic (osd1$, masked, high security) and the dashboard address.
	///Partner-gated: the address of Clerri's dashboard and how it takes the key and the patient are not public. The office enters the address Clerri
	///gives it; the fork replaces only [ClientKey] and [PatNum] in it, so no other patient data can be sent, and opens it only if it is an https
	///address. Every launch is written to the local program-link log (ProgramLinkLaunchLog).</summary>
	public static class ClerriProgram {
		///<summary>The program row's description, shown in Setup > Program Links. Read-only in the setup window.</summary>
		public const string ProgDesc="Clerri";
		///<summary>The text on the Family module button.</summary>
		public const string ButtonText="Clerri";
		///<summary>The only toolbar the button goes on.</summary>
		public const EnumToolBar ToolBar=EnumToolBar.FamilyModule;
		///<summary>The key Clerri issues to the office, per clinic (ClinicNum 0 is the default for clinics without their own). osd1$, masked, high
		///security.</summary>
		public const string PropClientKey="Client Key";
		///<summary>The address of the office's Clerri dashboard, from Clerri, practice-wide (ClinicNum 0). Plain text. May hold [ClientKey] and
		///[PatNum].</summary>
		public const string PropDashboardAddress="Dashboard Address";
		///<summary>Replaced with the clinic's Client Key, URL-encoded.</summary>
		public const string TagClientKey="[ClientKey]";
		///<summary>Replaced with the selected patient's PatNum, or blank when no patient is selected.</summary>
		public const string TagPatNum="[PatNum]";

		///<summary>The row this program inserts: no path (the dashboard address is a property) and the Family module button "Clerri". The two property
		///rows are not in the definition: they need the credential flags, so EnsureProgram adds them through VendorCredentials.</summary>
		public static BridgeLinkDefinition GetDefinition() {
			Meth.NoCheckMiddleTierRole();
			BridgeLinkDefinition definition=new BridgeLinkDefinition();
			definition.ProgName=ProgramName.Clerri;
			definition.ProgDesc=ProgDesc;
			definition.ButtonText=ButtonText;
			definition.ListToolBars=new List<EnumToolBar> { ToolBar };
			return definition;
		}

		///<summary>Inserts the program row (Enabled=0) with its Family module button if it is missing (BridgeLinkRows, shared with the other program
		///links), and the default Dashboard Address and Client Key rows if they are missing. An existing row's buttons are never changed. Never calls
		///Clerri. Returns true if anything was inserted. The caller refreshes the caches and signals InvalidType.Programs and
		///InvalidType.ToolButsAndMounts.</summary>
		public static bool EnsureProgram() {
			Meth.NoCheckMiddleTierRole();
			bool isInserted=BridgeLinkRows.EnsureProgram(GetDefinition());
			Program program=Programs.GetCur(ProgramName.Clerri);
			if(program==null) {
				return isInserted;
			}
			long programNum=program.ProgramNum;
			List<ProgramProperty> listProgramProperties=ProgramProperties.GetWhere(x => x.ProgramNum==programNum && x.ClinicNum==0);
			if(!listProgramProperties.Any(x => x.PropertyDesc==PropDashboardAddress)) {
				VendorCredentials.EnsureSetting(programNum,PropDashboardAddress);
				isInserted=true;
			}
			ProgramProperty programPropertyKey=listProgramProperties.FirstOrDefault(x => x.PropertyDesc==PropClientKey);
			if(programPropertyKey==null || !programPropertyKey.IsMasked || !programPropertyKey.IsHighSecurity) {
				VendorCredentials.EnsureSecret(programNum,PropClientKey);
				isInserted=true;
			}
			return isInserted;
		}

		///<summary>True if the program has its Family module button (Show Toolbar Button). Reads the cache.</summary>
		public static bool IsToolbarButtonShown(long programNum) {
			Meth.NoCheckMiddleTierRole();
			return ToolButItems.GetForProgram(programNum).Any(x => x.ToolBar==ToolBar);
		}

		///<summary>Adds or removes the Family module button. Only the Family module button is touched. The caller refreshes the cache and signals
		///InvalidType.ToolButsAndMounts.</summary>
		public static void SetToolbarButtonShown(long programNum,bool isShown) {
			Meth.NoCheckMiddleTierRole();
			List<ToolButItem> listToolButItems=ToolButItems.GetForProgram(programNum).FindAll(x => x.ToolBar==ToolBar);
			if(isShown) {
				if(listToolButItems.Count==0) {
					InsertToolbarButton(programNum);
				}
				return;
			}
			for(int i=0;i<listToolButItems.Count;i++) {
				ToolButItems.Delete(listToolButItems[i]);
			}
		}

		private static void InsertToolbarButton(long programNum) {
			ToolButItem toolButItem=new ToolButItem();
			toolButItem.ProgramNum=programNum;
			toolButItem.ToolBar=ToolBar;
			toolButItem.ButtonText=ButtonText;
			ToolButItems.Insert(toolButItem);
		}

		///<summary>Rule from the public setup page: Show Toolbar Button can't be changed while the link is enabled, so it is editable only when the link
		///is saved as disabled and the Enabled box is not checked.</summary>
		public static bool CanChangeToolbarButton(bool isEnabledSaved,bool isEnabledChecked) {
			Meth.NoCheckMiddleTierRole();
			return !isEnabledSaved && !isEnabledChecked;
		}

		///<summary>True if saving the setup window must add or remove the Family module button: the link is saved as disabled and Show Toolbar Button
		///differs from the saved button. Decided from the saved state, never from whether the box can be edited at the moment of saving: checking
		///Enabled locks the box (CanChangeToolbarButton) but keeps the value the user picked while the link was disabled, and that value is saved.</summary>
		public static bool ShouldSaveToolbarButton(bool isEnabledSaved,bool isShownSaved,bool isShownChecked) {
			Meth.NoCheckMiddleTierRole();
			return !isEnabledSaved && isShownSaved!=isShownChecked;
		}

		///<summary>The Show Toolbar Button and Enabled part of saving the setup window. The Family module button is added or removed first, while the
		///link is still saved as disabled (ShouldSaveToolbarButton), then Enabled is saved. So a button picked and Enabled checked in the same session
		///are both saved. Reads the ToolButItems cache. The caller refreshes the caches and signals InvalidType.Programs and
		///InvalidType.ToolButsAndMounts.</summary>
		public static void SaveToolbarButtonAndEnabled(Program program,bool isShownChecked,bool isEnabledChecked) {
			Meth.NoCheckMiddleTierRole();
			if(ShouldSaveToolbarButton(program.Enabled,IsToolbarButtonShown(program.ProgramNum),isShownChecked)) {
				SetToolbarButtonShown(program.ProgramNum,isShownChecked);
			}
			if(program.Enabled!=isEnabledChecked) {
				Program programOld=program.Copy();
				program.Enabled=isEnabledChecked;
				Programs.Update(program,programOld);
			}
		}

		///<summary>Null if the dashboard address can be opened: an absolute https address with a host, no user name and not on a host this program
		///never contacts (BridgeLaunch.CheckWebAddress), once the tags are filled in. Otherwise why not.</summary>
		public static string ValidateDashboardAddress(string dashboardAddress) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrWhiteSpace(dashboardAddress)) {
				return "Enter the dashboard address from Clerri.";
			}
			//Check the address as it will be opened, with sample values in the tags.
			string sample=ReplaceTag(ReplaceTag(dashboardAddress.Trim(),TagClientKey,"key"),TagPatNum,"1");
			return GetAddressProblem(sample);
		}

		///<summary>True if address is an absolute https URL with a host, no user name or password in it, and not on a blocked host.</summary>
		public static bool IsHttpsAddress(string address) {
			Meth.NoCheckMiddleTierRole();
			return GetAddressProblem(address)==null;
		}

		///<summary>Null if the finished address can be opened, else why not. The https and blocked-host rules are the ones every web program link uses
		///(BridgeLaunch.CheckWebAddress); a user name or password in the address is refused on top of them.</summary>
		private static string GetAddressProblem(string address) {
			const string NOT_HTTPS="The dashboard address must be a complete address that starts with https://.";
			BridgeLaunchProblem problem=BridgeLaunch.CheckWebAddress(address);
			if(problem==BridgeLaunchProblem.PathBlocked) {
				return BridgeLaunch.Describe(BridgeLaunchProblem.PathBlocked);
			}
			if(problem!=BridgeLaunchProblem.None) {
				return NOT_HTTPS;
			}
			Uri uri;
			if(!Uri.TryCreate(address.Trim(),UriKind.Absolute,out uri) || uri.UserInfo!="") {
				return NOT_HTTPS;
			}
			return null;
		}

		///<summary>The address to open: dashboardAddress with [ClientKey] replaced by the URL-encoded clientKey and [PatNum] by patNum (blank when 0).
		///Tags match in any case. Nothing else is replaced, so no other patient data can reach the address. Returns null with error set if the address
		///is not valid, or if it uses [ClientKey] and there is no key.</summary>
		public static string BuildDashboardUrl(string dashboardAddress,string clientKey,long patNum,out string error) {
			Meth.NoCheckMiddleTierRole();
			error=ValidateDashboardAddress(dashboardAddress);
			if(error!=null) {
				return null;
			}
			string address=dashboardAddress.Trim();
			if(ContainsTag(address,TagClientKey) && string.IsNullOrWhiteSpace(clientKey)) {
				error="No Client Key is entered for this clinic or the default.";
				return null;
			}
			address=ReplaceTag(address,TagClientKey,Uri.EscapeDataString((clientKey??"").Trim()));
			address=ReplaceTag(address,TagPatNum,patNum>0 ? POut.Long(patNum) : "");
			error=GetAddressProblem(address);
			if(error!=null) {
				return null;
			}
			return address;
		}

		///<summary>The address to open for the clinic and patient, from the program link's settings (the clinic's Client Key, else the default one).
		///Returns null with error set when it can't be opened. Reads the cache; never calls Clerri.</summary>
		public static string GetDashboardUrl(long programNum,long clinicNum,long patNum,out string error) {
			Meth.NoCheckMiddleTierRole();
			string dashboardAddress=ProgramProperties.GetValOrDefault(programNum,PropDashboardAddress,0);
			SecretReadResult secretReadResult=ReadClientKey(programNum,clinicNum);
			if(secretReadResult.NeedsReentry && ContainsTag(dashboardAddress,TagClientKey)) {
				error="The saved Client Key can't be read. Enter it again in Setup, Program Links, Clerri.";
				return null;
			}
			return BuildDashboardUrl(dashboardAddress,secretReadResult.PlainText,patNum,out error);
		}

		///<summary>The Client Key the clinic uses: its own row, or the default (ClinicNum 0) row when the clinic has none or a blank one. Reads the
		///cache; never throws.</summary>
		public static SecretReadResult ReadClientKey(long programNum,long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			SecretReadResult secretReadResult=ReadClientKeyExact(programNum,clinicNum);
			if(clinicNum!=0 && secretReadResult.Status==SecretReadStatus.Blank) {
				secretReadResult=ReadClientKeyExact(programNum,0);
			}
			return secretReadResult;
		}

		///<summary>The Client Key saved for exactly this clinic (ClinicNum 0 is the default), without falling back. Blank if there is no row.</summary>
		public static SecretReadResult ReadClientKeyExact(long programNum,long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			string stored="";
			try {
				ProgramProperty programProperty=ProgramProperties.GetWhere(x => x.ProgramNum==programNum && x.PropertyDesc==PropClientKey
					&& x.ClinicNum==clinicNum).OrderBy(x => x.ProgramPropertyNum).FirstOrDefault();
				stored=programProperty?.PropertyValue??"";
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			return SecretValue.Read(stored,LegacyPolicy.AcceptPlaintext);
		}

		///<summary>True if a readable Client Key is saved for any clinic or the default. Reads the cache.</summary>
		public static bool HasAnyClientKey(long programNum) {
			Meth.NoCheckMiddleTierRole();
			List<ProgramProperty> listProgramProperties=ProgramProperties.GetWhere(x => x.ProgramNum==programNum && x.PropertyDesc==PropClientKey);
			return listProgramProperties.Any(x => SecretValue.Read(x.PropertyValue,LegacyPolicy.AcceptPlaintext).HasValue);
		}

		///<summary>True if text holds the tag, in any case.</summary>
		public static bool ContainsTag(string text,string tag) {
			Meth.NoCheckMiddleTierRole();
			return (text??"").IndexOf(tag,StringComparison.OrdinalIgnoreCase)>=0;
		}

		private static string ReplaceTag(string text,string tag,string value) {
			return Regex.Replace(text??"",Regex.Escape(tag),(value??"").Replace("$","$$"),RegexOptions.IgnoreCase);
		}
	}
}
