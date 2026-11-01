// V-BRIDGE1: the program link rows of the Open Dental 25.1 and 25.2 bridges (Overjet, DTX Studio, DTX Studio Acquire) and the Insurance Answers
// Plus row. Like the other vendor-direct program links, the rows are inserted at runtime, disabled, when Setup > Program Links opens.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Linq;

namespace OpenDentBusiness.VendorDirect.ProgramLinks {
	///<summary>What a program link row looks like when this program inserts it. Everything here is a default the office can change in
	///Program Links; nothing is re-applied to a row that already exists, except property rows that are missing.</summary>
	public class BridgeLinkDefinition {
		public ProgramName ProgName;
		public string ProgDesc="";
		///<summary>"Path of file to open".</summary>
		public string Path="";
		///<summary>"Command line" (for DTX Studio: the additional properties appended to the arguments).</summary>
		public string CommandLine="";
		///<summary>The Notes text in Program Links: what the link does and how to finish its setup. Describes, never promotes.</summary>
		public string Note="";
		///<summary>"Text on button" for the toolbar rows.</summary>
		public string ButtonText="";
		///<summary>Toolbars that get a button when the row is inserted. The button shows only once the office enables the link.</summary>
		public List<EnumToolBar> ListToolBars=new List<EnumToolBar>();
		///<summary>ClinicNum 0 property rows (PropertyDesc, default value), inserted when missing.</summary>
		public List<KeyValuePair<string,string>> ListProperties=new List<KeyValuePair<string,string>>();
	}

	///<summary>Inserts the rows of a BridgeLinkDefinition. Called by the program link setups each time Program Links opens. Never calls a vendor.
	///</summary>
	public static class BridgeLinkRows {
		///<summary>Inserts the program row (Enabled=0) with the definition's path, command line, note and toolbar buttons if there is no row with
		///this ProgName, then any missing ClinicNum 0 property rows. A row that already exists (for example one Open Dental created in the office
		///database) keeps its settings and toolbars. Returns true if anything was inserted; the caller refreshes the caches and signals
		///InvalidType.Programs.</summary>
		public static bool EnsureProgram(BridgeLinkDefinition definition) {
			Meth.NoCheckMiddleTierRole();
			bool isInserted=Programs.InsertIfMissing(definition.ProgName,definition.ProgDesc);
			if(isInserted || Programs.GetCur(definition.ProgName)==null) {
				Programs.RefreshCache();
			}
			Program program=Programs.GetCur(definition.ProgName);
			if(program==null) {
				return isInserted;
			}
			if(isInserted) {
				//Only the call whose row survived a race gets here, so the defaults and buttons are written once.
				Program programOld=program.Copy();
				program.Path=definition.Path??"";
				program.CommandLine=definition.CommandLine??"";
				program.Note=definition.Note??"";
				Programs.Update(program,programOld);
				for(int i=0;i<definition.ListToolBars.Count;i++) {
					ToolButItem toolButItem=new ToolButItem();
					toolButItem.ProgramNum=program.ProgramNum;
					toolButItem.ToolBar=definition.ListToolBars[i];
					toolButItem.ButtonText=definition.ButtonText??"";
					ToolButItems.Insert(toolButItem);
				}
				Programs.RefreshCache();
			}
			List<ProgramProperty> listProgramProperties=ProgramProperties.GetListForProgramAndClinic(program.ProgramNum,0);
			for(int i=0;i<definition.ListProperties.Count;i++) {
				string desc=definition.ListProperties[i].Key;
				if(listProgramProperties.Any(x => x.PropertyDesc==desc)) {
					continue;
				}
				ProgramProperties.InsertIfMissing(program.ProgramNum,desc,0,definition.ListProperties[i].Value,false,false);
				isInserted=true;
			}
			return isInserted;
		}

		///<summary>For the Program Links edit window: why "Path of file to open" can't be saved for this program link, or "" if it can. The web
		///links (Overjet, Insurance Answers Plus) take a full https:// address on an allowed host, or blank; every other link is not checked here.
		///</summary>
		public static string GetPathSaveProblem(string progName,string path) {
			Meth.NoCheckMiddleTierRole();
			bool isOverjet=progName==ProgramName.Overjet.ToString();
			bool isIap=progName==ProgramName.IAP.ToString();
			if((!isOverjet && !isIap) || string.IsNullOrWhiteSpace(path)) {
				return "";
			}
			BridgeLaunchProblem problem=BridgeLaunch.CheckWebAddress(path);
			if(problem==BridgeLaunchProblem.None) {
				return "";
			}
			if(problem==BridgeLaunchProblem.PathBlocked) {
				return BridgeLaunch.Describe(BridgeLaunchProblem.PathBlocked);
			}
			return BridgeLaunch.Describe(BridgeLaunchProblem.PathNotHttps);
		}
	}
}
