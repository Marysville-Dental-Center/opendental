// Vendor follow-up from public documentation (next/vendorpub-1): the Partner ID of the Medit Link program link. Medit's public article on its Open
// Dental integration adds a PartnerID key as the last key of the JSON object in the link's arguments, which assigns that partner (a lab) to every
// case created through the integration. The arguments with and without it, the refusals, the property row and the wiring. Nothing is launched.
// Review follow-up (next/vendorfix-1): patient values that would break the quoted JSON, checked on the whole arguments as Windows splits them.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect.ProgramLinks;

namespace P262LinksTests {
	[TestClass]
	public class MeditLinkPartnerTests {
		///<summary>The Command line Open Dental's 21.1 conversion gave the Medit Link row (ConvertDatabases7, the same in db/init/new-database.sql).
		///</summary>
		private const string COMMAND_LINE=@"{\""NameFL\"": \""[NameFL]\"", \""PatNum\"": \""[PatNum]\"", \""PatientGenderMF\"": \""[PatientGenderMF]\"", \""Birthdate_yyyyMMdd\"": \""[Birthdate_yyyyMMdd]\""}";
		///<summary>Medit's article, the Optional command line arguments for Open Dental 22.1 or later with a partner; "xxxxx" is the partner's ID.</summary>
		private const string ARTICLE_WITH_PARTNER=@"{\""NameFL\"": \""[NameFL]\"", \""PatNum\"": \""[PatNum]\"", \""PatientGenderMF\"": \""[PatientGenderMF]\"", \""Birthdate_yyyyMMdd\"": \""[Birthdate_yyyyMMdd]\"", \""PartnerID\"": \""xxxxx\""}";
		private const string UUID_PART="\"--commandUuid\" \"E02698AC-C1AE-4699-9307-F4A58DED47FE\"";
		///<summary>Glidewell Dental's PartnerID in Medit's table of supported partners.</summary>
		private const string GLIDEWELL="7307395";

		///<summary>A stand-in for Patients.ReplacePatient: only the four tokens of the Command line, no database.</summary>
		private static string Replace(string text,Patient patient) {
			return text.Replace("[NameFL]","Zoe Nunez").Replace("[PatNum]",patient.PatNum.ToString()).Replace("[PatientGenderMF]","F")
				.Replace("[Birthdate_yyyyMMdd]","19800309");
		}

		private static string Arguments(string commandLine,string partnerId,Patient patient,out string problem) {
			return MeditLinkPartner.GetArguments(commandLine,partnerId,patient,Replace,out problem);
		}

		///<summary>A stand-in for Patients.ReplacePatient on the four tags of the Command line with the patient's own values ([NameFL] from
		///Patients.GetNameFL, as ReplacePatient fills it), nothing removed. No database.</summary>
		private static string ReplaceNames(string text,Patient patient) {
			return text.Replace("[NameFL]",Patients.GetNameFL(patient.LName,patient.FName,patient.Preferred,patient.MiddleI))
				.Replace("[PatNum]",patient.PatNum.ToString()).Replace("[PatientGenderMF]","F").Replace("[Birthdate_yyyyMMdd]",patient.Birthdate.ToString("yyyyMMdd"));
		}

		private static Patient CreatePatient(string fName,string lName) {
			Patient patient=new Patient();
			patient.PatNum=1042;
			patient.FName=fName;
			patient.LName=lName;
			patient.MiddleI="";
			patient.Preferred="";
			patient.Gender=PatientGender.Female;
			patient.Birthdate=new DateTime(1980,3,9);
			return patient;
		}

		///<summary>The whole arguments Medit Link must get for the Command line Open Dental's conversion wrote: the command id, then its JSON object
		///with this NameFL and the patient's three other values, and PartnerID last when there is a Partner ID. Written out here, not built by the
		///code under test.</summary>
		private static string Expected(string nameFL,string partnerId) {
			string json="{\\\"NameFL\\\": \\\""+nameFL+"\\\", \\\"PatNum\\\": \\\"1042\\\", \\\"PatientGenderMF\\\": \\\"F\\\", "
				+"\\\"Birthdate_yyyyMMdd\\\": \\\"19800309\\\"";
			if(partnerId!="") {
				json+=", \\\"PartnerID\\\": \\\""+partnerId+"\\\"";
			}
			return UUID_PART+" \"--commandParam\" \""+json+"}\"";
		}

		///<summary>The keys and values ("key=value", in order) of the JSON object Medit Link reads, after Windows splits the arguments as for the
		///program it starts. Fails unless there are exactly the four arguments (--commandUuid, the command id, --commandParam, one JSON object) and
		///the object has no repeated key and only string values.</summary>
		private static List<string> ReadCommandParam(string arguments) {
			string[] arrayArgs=SplitAsWindows(arguments);
			Assert.AreEqual(4,arrayArgs.Length,string.Join(" | ",arrayArgs));
			Assert.AreEqual("--commandUuid",arrayArgs[0]);
			Assert.AreEqual(MeditLinkPartner.CommandUuid,arrayArgs[1]);
			Assert.AreEqual("--commandParam",arrayArgs[2]);
			JObject jObject=JObject.Parse(arrayArgs[3],new JsonLoadSettings { DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error });
			List<string> listKeyValues=new List<string>();
			foreach(JProperty jProperty in jObject.Properties()) {
				Assert.AreEqual(JTokenType.String,jProperty.Value.Type,jProperty.Name);
				listKeyValues.Add(jProperty.Name+"="+jProperty.Value.Value<string>());
			}
			return listKeyValues;
		}

		[DllImport("shell32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
		private static extern IntPtr CommandLineToArgvW(string lpCmdLine,out int pNumArgs);

		[DllImport("kernel32.dll")]
		private static extern IntPtr LocalFree(IntPtr hMem);

		///<summary>The arguments as Windows splits a command line for the program it starts (CommandLineToArgvW), without the program name.</summary>
		private static string[] SplitAsWindows(string arguments) {
			IntPtr intPtrArgv=CommandLineToArgvW("MeditLink.exe "+arguments,out int count);
			Assert.AreNotEqual(IntPtr.Zero,intPtrArgv);
			try {
				string[] arrayArgs=new string[count-1];
				for(int i=1;i<count;i++) {
					arrayArgs[i-1]=Marshal.PtrToStringUni(Marshal.ReadIntPtr(intPtrArgv,i*IntPtr.Size));
				}
				return arrayArgs;
			}
			finally {
				LocalFree(intPtrArgv);
			}
		}

		[TestMethod]
		public void WithoutPartnerId_TheArgumentsAreAsBefore() {
			Patient patient=SyntheticPatients.Create();
			foreach(string partnerId in new[] { "",null,"   " }) {
				string arguments=Arguments(COMMAND_LINE,partnerId,patient,out string problem);
				Assert.AreEqual("",problem);
				Assert.AreEqual(UUID_PART+" \"--commandParam\" \"{\\\"NameFL\\\": \\\"Zoe Nunez\\\", \\\"PatNum\\\": \\\"1042\\\", \\\"PatientGenderMF\\\": \\\"F\\\", "
					+"\\\"Birthdate_yyyyMMdd\\\": \\\"19800309\\\"}\"",arguments,"What the link sent before next/vendorpub-1.");
			}
			//An office's own Command line is not checked when there is no Partner ID.
			Assert.AreEqual(UUID_PART+" \"--commandParam\" \"anything 1042\"",Arguments("anything [PatNum]","",patient,out string problemOwn));
			Assert.AreEqual("",problemOwn);
			Assert.AreEqual("E02698AC-C1AE-4699-9307-F4A58DED47FE",MeditLinkPartner.CommandUuid);
		}

		[TestMethod]
		public void WithPartnerId_TheKeyIsAddedExactlyAsMeditsArticleShows() {
			Assert.AreEqual(ARTICLE_WITH_PARTNER.Replace("xxxxx",GLIDEWELL),MeditLinkPartner.AddPartnerId(COMMAND_LINE,GLIDEWELL));
			Assert.AreEqual(ARTICLE_WITH_PARTNER.Replace("xxxxx",GLIDEWELL),MeditLinkPartner.AddPartnerId(COMMAND_LINE+"  ",GLIDEWELL+" "),
				"Trailing spaces of the Command line and the Partner ID are not kept.");
			Assert.AreEqual(COMMAND_LINE,MeditLinkPartner.AddPartnerId(COMMAND_LINE,""),"Blank: unchanged.");
			Assert.AreEqual(@"{\""PartnerID\"": \""42\""}",MeditLinkPartner.AddPartnerId("{ }","42"),"An empty object gets the key alone.");
			string arguments=Arguments(COMMAND_LINE,GLIDEWELL,SyntheticPatients.Create(),out string problem);
			Assert.AreEqual("",problem);
			Assert.AreEqual(UUID_PART+" \"--commandParam\" \"{\\\"NameFL\\\": \\\"Zoe Nunez\\\", \\\"PatNum\\\": \\\"1042\\\", \\\"PatientGenderMF\\\": \\\"F\\\", "
				+"\\\"Birthdate_yyyyMMdd\\\": \\\"19800309\\\", \\\"PartnerID\\\": \\\"7307395\\\"}\"",arguments);
			Assert.AreEqual("PartnerID",MeditLinkPartner.PartnerIdKey,"Spelled as the article spells it.");
		}

		[TestMethod]
		public void WithoutAPatient_OnlyTheCommandId_ThePartnerIdIsNotUsed() {
			foreach(string partnerId in new[] { "",GLIDEWELL,"not valid;" }) {
				Assert.AreEqual(UUID_PART,Arguments(COMMAND_LINE,partnerId,null,out string problem));
				Assert.AreEqual("",problem,"Nothing to refuse without a command parameter.");
			}
			Assert.AreEqual(UUID_PART,Arguments("no json",GLIDEWELL,null,out string problemNoJson));
		}

		[TestMethod]
		public void PartnerIdThatCantBeAdded_IsRefused_NothingStarts() {
			Patient patient=SyntheticPatients.Create();
			Dictionary<string,string> dictCases=new Dictionary<string,string> {
				//Partner ID, Command line => what the message says
				{ "73 07|"+COMMAND_LINE,"letters, digits, hyphens and underscores" },
				{ "7307395\"|"+COMMAND_LINE,"letters, digits, hyphens and underscores" },
				{ "a;b|"+COMMAND_LINE,"letters, digits, hyphens and underscores" },
				{ GLIDEWELL+"|"+ARTICLE_WITH_PARTNER,"already holds a PartnerID" },
				{ GLIDEWELL+"|"+COMMAND_LINE.Replace("PatNum","partnerid"),"already holds a PartnerID" },
				{ GLIDEWELL+"|/patient [PatNum]","ends with }" },
				{ GLIDEWELL+"|","ends with }" },
			};
			foreach(KeyValuePair<string,string> pair in dictCases) {
				string partnerId=pair.Key.Substring(0,pair.Key.IndexOf('|'));
				string commandLine=pair.Key.Substring(pair.Key.IndexOf('|')+1);
				Assert.IsNull(Arguments(commandLine,partnerId,patient,out string problem),pair.Key);
				StringAssert.Contains(problem,pair.Value,pair.Key);
				Assert.AreEqual(problem,MeditLinkPartner.GetPartnerIdProblem(commandLine,partnerId));
				Assert.AreEqual(commandLine,MeditLinkPartner.AddPartnerId(commandLine,partnerId),"A refused Command line is returned unchanged.");
			}
			foreach(string partnerId in new[] { GLIDEWELL,"Lab-1","lab_2","X" }) {
				Assert.AreEqual("",MeditLinkPartner.GetPartnerIdProblem(COMMAND_LINE,partnerId),partnerId);
			}
		}

		///<summary>Review follow-up (next/vendorfix-1): a double quote, a backslash or a control character in a name used to reach the quoted JSON
		///as typed, so a name could end the argument or its JSON string, or add a key such as PartnerID. The values lose those characters; the
		///whole arguments are checked, and so is the JSON object Medit Link reads from them.</summary>
		[TestMethod]
		public void PatientValues_QuotesBackslashesAndControlCharactersRemoved_TheWholeArgumentsChecked() {
			string[][] arrayCases={
				//FName, LName, the NameFL Medit Link must get
				new[] { "Nico","Nu\"nez","Nico Nunez" },//A double quote.
				new[] { "Nico","Nu\\nez","Nico Nunez" },//A backslash.
				new[] { "Ni\r\nco","Nu\tnez","Nico Nunez" },//CR, LF and tab.
				new[] { "Nico","Nunez\\","Nico Nunez" },//A trailing backslash: it would escape the closing \".
				new[] { "Nico","Nunez\\\\","Nico Nunez" },
				new[] { "Nico\", \"PartnerID\": \"1","Nunez","Nico, PartnerID: 1 Nunez" },//A key, with plain quotes.
				new[] { "Nico\\\", \\\"PartnerID\\\": \\\"1","Nunez","Nico, PartnerID: 1 Nunez" },//A key, quoted as the Command line quotes.
				new[] { "Nico","Nunez\\\"}\" \"--commandUuid\" \"x","Nico Nunez} --commandUuid x" },//Another argument.
			};
			foreach(string partnerId in new[] { "",GLIDEWELL }) {
				foreach(string[] arrayCase in arrayCases) {
					string label=arrayCase[0]+" | "+arrayCase[1]+" | "+partnerId;
					string arguments=MeditLinkPartner.GetArguments(COMMAND_LINE,partnerId,CreatePatient(arrayCase[0],arrayCase[1]),ReplaceNames,
						out string problem);
					Assert.AreEqual("",problem,label);
					Assert.AreEqual(Expected(arrayCase[2],partnerId),arguments,label);
					List<string> listExpected=new List<string> { "NameFL="+arrayCase[2],"PatNum=1042","PatientGenderMF=F","Birthdate_yyyyMMdd=19800309" };
					if(partnerId!="") {
						listExpected.Add("PartnerID="+partnerId);
					}
					CollectionAssert.AreEqual(listExpected,ReadCommandParam(arguments),label);
				}
			}
			Assert.AreEqual(" a b",MeditLinkPartner.EncodeValue(" a\"\\\r\n\t b"),"Only those characters; nothing is trimmed.");
			Assert.AreEqual("",MeditLinkPartner.EncodeValue(null));
		}

		///<summary>Names with letters, spaces, apostrophes, hyphens and accented letters give byte for byte the arguments the link built before
		///(the whole Command line given to the replacement, nothing removed), with and without a Partner ID.</summary>
		[TestMethod]
		public void PatientValues_OrdinaryNames_SentExactlyAsBefore() {
			string[][] arrayNames={
				//FName, LName, Preferred (GetNameFL puts it in apostrophes), the NameFL
				new[] { "Zoé","Núñez","","Zoé Núñez" },
				new[] { "Mary Ann","O'Brien","Mo","Mary Ann 'Mo' O'Brien" },
				new[] { "Jean-Luc","Smith-Jones","","Jean-Luc Smith-Jones" },
				new[] { "Ana","de la Cruz","","Ana de la Cruz" },
				new[] { "Björn","Åström","Émile","Björn 'Émile' Åström" },
				new[] { "Zoé","","","Zoé " },//No last name: GetNameFL's trailing space is kept.
			};
			foreach(string partnerId in new[] { "",GLIDEWELL }) {
				foreach(string[] arrayName in arrayNames) {
					Patient patient=CreatePatient(arrayName[0],arrayName[1]);
					patient.Preferred=arrayName[2];
					string label=arrayName[3]+" | "+partnerId;
					string before=UUID_PART+" \"--commandParam\" \""+ReplaceNames(MeditLinkPartner.AddPartnerId(COMMAND_LINE,partnerId),patient)+"\"";
					string arguments=MeditLinkPartner.GetArguments(COMMAND_LINE,partnerId,patient,ReplaceNames,out string problem);
					Assert.AreEqual("",problem,label);
					Assert.AreEqual(before,arguments,label);
					Assert.AreEqual(Expected(arrayName[3],partnerId),arguments,label);
					Assert.AreEqual("NameFL="+arrayName[3],ReadCommandParam(arguments)[0],label);
				}
			}
			//A tag the replacement leaves alone, other bracketed text and the rest of an office's own Command line are kept as typed.
			Patient patientOwn=CreatePatient("Nico","Nunez");
			string commandLineOwn="{\\\"a\\\": \\\"[NameFL]\\\", \\\"b\\\": \\\"[Unknown]\\\", \\\"c\\\": [1, 2]}";
			Assert.AreEqual(UUID_PART+" \"--commandParam\" \"{\\\"a\\\": \\\"Nico Nunez\\\", \\\"b\\\": \\\"[Unknown]\\\", \\\"c\\\": [1, 2]}\"",
				MeditLinkPartner.GetArguments(commandLineOwn,"",patientOwn,ReplaceNames,out string problemOwn));
			Assert.AreEqual("",problemOwn);
			Assert.AreEqual("x [PatNum]",MeditLinkPartner.FillPatient("x [PatNum]",patientOwn,null),"No replacement: unchanged.");
			Assert.AreEqual("x [PatNum]",MeditLinkPartner.FillPatient("x [PatNum]",null,ReplaceNames),"No patient: unchanged.");
		}

		[TestMethod]
		public void Property_IsAddedBlankToTheExistingRowOnly_TheRowIsNotChanged() {
			ScratchDb.RequireDb();
			Assert.IsFalse(MeditLinkPartner.EnsureProperty(),"No Medit Link row: nothing is inserted (Open Dental's conversion creates the row).");
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM program"));
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM programproperty"));
			//The row as db/init/new-database.sql has it (ProgramNum 127, from Open Dental's 21.1 conversion).
			DataCore.NonQ("INSERT INTO program (ProgramNum,ProgName,ProgDesc,Enabled,Path,CommandLine,Note,PluginDllName,ButtonImage,FileTemplate,FilePath,IsDisabledByHq,CustErr) "
				+"VALUES (127,'MeditLink','Medit Link from www.meditlink.com',0,'"+POut.String(@"C:\Program Files\Medit\Medit Link\Medit Link\Medit_Link.exe")+"','"
				+POut.String(COMMAND_LINE)+"','','','','','',0,'')");
			ScratchDb.RefreshCaches();
			DataTable tableBefore=DataCore.GetTable("SELECT * FROM program WHERE ProgramNum=127");
			Assert.IsTrue(MeditLinkPartner.EnsureProperty());
			ScratchDb.RefreshCaches();
			Assert.IsFalse(MeditLinkPartner.EnsureProperty(),"Once.");
			DataTable tableProps=DataCore.GetTable("SELECT PropertyDesc,PropertyValue,ClinicNum,IsMasked,IsHighSecurity FROM programproperty WHERE ProgramNum=127");
			Assert.AreEqual(1,tableProps.Rows.Count);
			Assert.AreEqual("Partner ID",tableProps.Rows[0]["PropertyDesc"].ToString());
			Assert.AreEqual("",tableProps.Rows[0]["PropertyValue"].ToString(),"Blank: nothing is added to the arguments.");
			Assert.AreEqual("0",tableProps.Rows[0]["ClinicNum"].ToString());
			Assert.AreEqual("0",tableProps.Rows[0]["IsMasked"].ToString());
			DataTable tableAfter=DataCore.GetTable("SELECT * FROM program WHERE ProgramNum=127");
			foreach(DataColumn column in tableBefore.Columns) {
				Assert.AreEqual(tableBefore.Rows[0][column.ColumnName].ToString(),tableAfter.Rows[0][column.ColumnName].ToString(),"program."+column.ColumnName);
			}
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM toolbutitem"),"No button is added.");
			//The office's value is kept and read by the button, with the real patient token replacement.
			DataCore.NonQ("UPDATE programproperty SET PropertyValue=' "+GLIDEWELL+" ' WHERE ProgramNum=127");
			ScratchDb.RefreshCaches();
			Assert.IsFalse(MeditLinkPartner.EnsureProperty());
			Program program=Programs.GetCur(ProgramName.MeditLink);
			Assert.AreEqual(GLIDEWELL,MeditLinkPartner.GetPartnerId(program));
			Patient patient=SyntheticPatients.Create();
			patient.Gender=PatientGender.Female;
			patient.Birthdate=new DateTime(1980,3,9);
			string arguments=MeditLinkPartner.GetArguments(program,patient,out string problem);
			Assert.AreEqual("",problem);
			//The whole arguments: the synthetic last name holds double quotes, which are removed from the NameFL value (Zoé Q Núñez "Nico").
			string nameFL="Zoé Q Núñez Nico";
			Assert.AreEqual(Expected(nameFL,GLIDEWELL),arguments);
			Assert.AreEqual(UUID_PART+" \"--commandParam\" \"{\\\"NameFL\\\": \\\"Zoé Q Núñez Nico\\\", \\\"PatNum\\\": \\\"1042\\\", "
				+"\\\"PatientGenderMF\\\": \\\"F\\\", \\\"Birthdate_yyyyMMdd\\\": \\\"19800309\\\", \\\"PartnerID\\\": \\\"7307395\\\"}\"",arguments);
			CollectionAssert.AreEqual(new[] { "NameFL="+nameFL,"PatNum=1042","PatientGenderMF=F","Birthdate_yyyyMMdd=19800309","PartnerID=7307395" },
				ReadCommandParam(arguments));
			//Patients.ReplacePatient ignores the letter case of a tag; each tag is still filled in and made safe.
			Assert.AreEqual("\\\"1042\\\" \\\"Núñez Nico\\\" [Unknown]",
				MeditLinkPartner.FillPatient("\\\"[patnum]\\\" \\\"[LNAME]\\\" [Unknown]",patient,(x,y) => Patients.ReplacePatient(x,y)));
			//The real Patients.ReplacePatient fills tags with Regex.Replace, which reads a value as a replacement pattern ($_ is the whole input, $& the
			//match, $` and $' the text around it). Each tag is filled in on its own, so such a name can only bring back the tag itself: the JSON keeps
			//exactly its keys and every other value is unchanged.
			foreach(string lName in new[] { "Nu$_nez","Nu$&nez","Nu$`nez","Nu$'nez" }) {
				Patient patientPattern=SyntheticPatients.Create();
				patientPattern.LName=lName;
				string argumentsPattern=MeditLinkPartner.GetArguments(program,patientPattern,out string problemPattern);
				Assert.AreEqual("",problemPattern,lName);
				List<string> listKeyValues=ReadCommandParam(argumentsPattern);
				CollectionAssert.AreEqual(new List<string> { "NameFL","PatNum","PatientGenderMF","Birthdate_yyyyMMdd","PartnerID" },
					listKeyValues.Select(x => x.Substring(0,x.IndexOf('='))).ToList(),lName);
				CollectionAssert.AreEqual(new List<string> { "PatNum=1042","PatientGenderMF=F","Birthdate_yyyyMMdd=19800309","PartnerID="+GLIDEWELL },
					listKeyValues.Skip(1).ToList(),lName);
			}
			//An ordinary name gives byte for byte what the link sent before: the real Patients.ReplacePatient on the whole Command line.
			Patient patientPlain=SyntheticPatients.Create();
			patientPlain.LName="Núñez-O'Brien";
			Assert.AreEqual(UUID_PART+" \"--commandParam\" \""+Patients.ReplacePatient(MeditLinkPartner.AddPartnerId(program.CommandLine,GLIDEWELL),patientPlain)+"\"",
				MeditLinkPartner.GetArguments(program,patientPlain,out string problemPlain));
			Assert.AreEqual("",problemPlain);
			Assert.AreEqual("",MeditLinkPartner.GetPartnerId(null));
		}

		[TestMethod]
		public void Wiring_TheButtonUsesTheRules_ProgramLinksAddsTheProperty() {
			string bridge=TestPaths.ReadRepoFile("WpfControlsOD","Bridges","MeditLink.cs");
			StringAssert.Contains(bridge,"MeditLinkPartner.GetArguments(program,patient,out string problem)");
			int indexRefused=bridge.IndexOf("if(commandArgs==null) {",StringComparison.Ordinal);
			int indexStart=bridge.IndexOf("Process.Start(path,commandArgs);",StringComparison.Ordinal);
			Assert.IsTrue(indexRefused>0 && indexStart>indexRefused,"A refused Partner ID stops the launch.");
			StringAssert.Contains(bridge.Substring(indexRefused,indexStart-indexRefused),"return;");
			Assert.IsFalse(bridge.Contains("E02698AC"),"The command id lives in MeditLinkPartner only.");
			StringAssert.Contains(TestPaths.ReadRepoFile("WpfControlsOD","Logic","ProgramL.cs"),"MeditLink.SendData(");
			string setup=TestPaths.ReadRepoFile("OpenPmsDental","Logic","VendorDirect","MeditLinkProgramLinkSetup.cs");
			StringAssert.Contains(setup,"public class MeditLinkProgramLinkSetup:StandardProgramLinkSetup");
			StringAssert.Contains(setup,"return ProgramName.MeditLink;");
			StringAssert.Contains(setup,"return MeditLinkPartner.EnsureProperty();");
			StringAssert.Contains(TestPaths.ReadRepoFile("OpenPmsDental","OpenPmsDental.csproj"),"<Compile Include=\"Logic\\VendorDirect\\MeditLinkProgramLinkSetup.cs\" />");
			Regex regexUrl=new Regex(@"https?://|www\.",RegexOptions.IgnoreCase);
			foreach(string[] arrayPath in new[] {
				new[] { "OpenPmsDentalBusiness","VendorDirect","ProgramLinks","MeditLinkPartner.cs" },
				new[] { "OpenPmsDental","Logic","VendorDirect","MeditLinkProgramLinkSetup.cs" },
				new[] { "WpfControlsOD","Bridges","MeditLink.cs" },
				new[] { "OpenPmsDentalBusiness","Misc","ForkMigrations","OrthoCompositePatientArgumentsStep.cs" } })
			{
				string text=TestPaths.ReadRepoFile(arrayPath);
				Assert.IsFalse(regexUrl.IsMatch(text),string.Join("\\",arrayPath)+" names a web address.");
				Assert.IsTrue(text.All(x => x<128),string.Join("\\",arrayPath)+" should be plain ASCII.");
			}
		}
	}
}
