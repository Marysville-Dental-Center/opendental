using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using CodeBase;

namespace OpenDentBusiness{

	///<summary>Import functions in this class should typically be called from a worker thread. Only CPT codes are imported, from the CPT data file that the
	///office licenses from the AMA. The other code systems used to be downloaded from Open Dental in its own format; that data is loaded into the
	///database directly.</summary>
	public class CodeSystems{

		public delegate void ProgressArgs(int numTotal,int numDone);

		///<summary>Returns a list of code systems in the code system table.  This query will change from version to version depending on what code systems we have available.</summary>
		public static List<CodeSystem> GetForCurrentVersion(bool isMemberNation) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<CodeSystem>>(MethodBase.GetCurrentMethod(),isMemberNation);
			}
			string command="";
			if(ODBuild.IsDebug()) {
				command="SELECT * FROM codesystem";// WHERE CodeSystemName IN ('ICD9CM','RXNORM','SNOMEDCT','CPT')";
			}
			else {
				command="SELECT * FROM codesystem WHERE CodeSystemName NOT IN ('AdministrativeSex','CDT')";
			}
			return Crud.CodeSystemCrud.SelectMany(command);
		}

		/////<summary>Returns a list of code systems in the code system table.  This query will change from version to version depending on what code systems we have available.</summary>
		//public static List<CodeSystem> GetForCurrentVersionNoSnomed() {
		//	if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
		//		return Meth.GetObject<List<CodeSystem>>(MethodBase.GetCurrentMethod());
		//	}
		//	//string command="SELECT * FROM codesystem WHERE CodeSystemName!='AdministrativeSex' AND CodeSystemName!='CDT'";
		//	string command="SELECT * FROM codesystem WHERE CodeSystemName IN ('ICD9CM','RXNORM','CPT')";
		//	return Crud.CodeSystemCrud.SelectMany(command);
		//}

		///<summary></summary>
		public static void Update(CodeSystem codeSystem){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT){
				Meth.GetVoid(MethodBase.GetCurrentMethod(),codeSystem);
				return;
			}
			Crud.CodeSystemCrud.Update(codeSystem);
		}

		///<summary>Updates VersionCurrent to the versionID passed in. Used by code system importer after successful import.  Only used for CPT.</summary>
		public static void UpdateCurrentVersion(CodeSystem codeSystem, string versionID) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),codeSystem,versionID);
				return;
			}
			if(string.Compare(codeSystem.VersionCur,versionID)>0) {  //If versionCur is newer than the version you just imported, don't update it.
				return;
			}
			codeSystem.VersionCur=versionID;
			Crud.CodeSystemCrud.Update(codeSystem);
		}

		///<summary>Called after user provides resource file.  Throws exceptions.  It is assumed that this is called from a worker thread.  Progress delegate will be called every 100th iteration to inform thread of current progress. Quit flag can be set at any time in order to quit importing prematurely.
		///No UpdateExisting parameter because we force users to accept new descriptions.</summary>
		public static void ImportCpt(string tempFileName,ProgressArgs progressArgs,ref bool quit,ref int numCodesImported,ref int numCodesUpdated,
			string versionID)
		{
			if(tempFileName==null) {
				return;
			}
			Dictionary<string,string> dictionaryCodes=Cpts.GetAll().ToDictionary(x=> x.CptCode,x => x.Description);
			Regex regex=new Regex(@"^([\d]{4}[\d\w])\s+(.+?)$");//Regex = "At the beginning of the string, find five numbers, followed by a white space (tab or space) followed by one or more characters (but as few as possible) to the end of the line."
			string[] stringArrayLines=File.ReadAllLines(tempFileName);
			string[] stringArrayCpts;
			bool isHeader=true;
			Cpt cpt=new Cpt();
			for(int i=0;i<stringArrayLines.Length;i++) {//each loop should read exactly one line of code. and each line of code should be a unique code
				if(quit) {
					return;
				}
				if(i%100==0) {
					progressArgs(i+1,stringArrayLines.Length);
				}
				if(isHeader) {
					if(!regex.IsMatch(stringArrayLines[i])) {  					//if(!lines[i].Contains("\t")) {	
						continue;//Copyright info is present at the head of the file.
					}
					isHeader=false;
				}
				stringArrayCpts=new string[2];
				stringArrayCpts[0]=regex.Match(stringArrayLines[i]).Groups[1].Value;//First five alphanumeric characters
				stringArrayCpts[1]=regex.Match(stringArrayLines[i]).Groups[2].Value;//Everything after the 6th character
				if(dictionaryCodes.Keys.Contains(stringArrayCpts[0])) {//code already exists
					Cpts.UpdateDescription(stringArrayCpts[0],stringArrayCpts[1],versionID);
					if(dictionaryCodes[stringArrayCpts[0]]!=stringArrayCpts[1]) {//The description is different
						numCodesUpdated++;
					}
				}
				else {
					cpt.CptCode			=stringArrayCpts[0];
					cpt.Description	=stringArrayCpts[1];
					cpt.VersionIDs	=versionID;
					Cpts.Insert(cpt);
					numCodesImported++;
				}
			}
		}

		/*
		Only pull out the methods below as you need them.  Otherwise, leave them commented out.

		///<summary>Gets one CodeSystem from the db.</summary>
		public static CodeSystem GetOne(long codeSystemNum){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT){
				return Meth.GetObject<CodeSystem>(MethodBase.GetCurrentMethod(),codeSystemNum);
			}
			return Crud.CodeSystemCrud.SelectOne(codeSystemNum);
		}

		///<summary></summary>
		public static long Insert(CodeSystem codeSystem){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT){
				codeSystem.CodeSystemNum=Meth.GetLong(MethodBase.GetCurrentMethod(),codeSystem);
				return codeSystem.CodeSystemNum;
			}
			return Crud.CodeSystemCrud.Insert(codeSystem);
		}

		///<summary></summary>
		public static void Delete(long codeSystemNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),codeSystemNum);
				return;
			}
			string command= "DELETE FROM codesystem WHERE CodeSystemNum = "+POut.Long(codeSystemNum);
			Db.NonQ(command);
		}
		*/



	}
}