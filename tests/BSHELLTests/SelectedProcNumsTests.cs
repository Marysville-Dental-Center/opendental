// v25_1-setup-programlink-selectedprocnums: the [SelectedProcNums] program-link field.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDental;

namespace BSHELLTests {
	[TestClass]
	public class SelectedProcNumsTests {
		[TestMethod]
		public void Format_CommaSeparatedNoSpacesInOrder() {
			Assert.AreEqual("12,5,40",ProgramLinkSelectedProcs.Format(new long[] { 12,5,40 }));
			Assert.AreEqual("",ProgramLinkSelectedProcs.Format(new long[0]));
			Assert.AreEqual("",ProgramLinkSelectedProcs.Format(null));
			Assert.AreEqual("7,3",ProgramLinkSelectedProcs.Format(new long[] { 0,7,3,7,-1 }),"Zeros, negatives and repeats are left out.");
		}

		[TestMethod]
		public void Replace_EveryOccurrence_AndEmptyWhenNothingSelected() {
			string commandLine="/procs=[SelectedProcNums] /again=[SelectedProcNums] /pat=[PatNum]";
			Assert.AreEqual("/procs=1,2 /again=1,2 /pat=[PatNum]",ProgramLinkSelectedProcs.Replace(commandLine,new long[] { 1,2 }));
			Assert.AreEqual("/procs= /again= /pat=[PatNum]",ProgramLinkSelectedProcs.Replace(commandLine,null),"The literal tag never reaches the program.");
			Assert.AreEqual("/procs= /again= /pat=[PatNum]",ProgramLinkSelectedProcs.Replace(commandLine,new List<long>()));
			Assert.AreEqual("no tag",ProgramLinkSelectedProcs.Replace("no tag",new long[] { 1 }));
			Assert.AreEqual("",ProgramLinkSelectedProcs.Replace("",new long[] { 1 }));
			Assert.IsNull(ProgramLinkSelectedProcs.Replace(null,new long[] { 1 }));
		}

		[TestMethod]
		public void Chart_SkipsGroupNotesAndNonProcedureRows() {
			DataTable table=new DataTable();
			table.Columns.Add("ProcNum");
			table.Columns.Add("ProcCode");
			table.Columns.Add("AptNum");
			table.Rows.Add("101","X0001","0");//Synthetic codes; the helper only reads ProcNum and the group note marker.
			table.Rows.Add("0","","55");//an appointment row
			table.Rows.Add("102","~GRP~","0");//a group note
			table.Rows.Add("103","X1","0");
			List<DataRow> listSelected=table.Rows.Cast<DataRow>().ToList();
			CollectionAssert.AreEqual(new List<long> { 101,103 },ProgramLinkSelectedProcs.FromProgNotesRows(listSelected));
			CollectionAssert.AreEqual(new List<long>(),ProgramLinkSelectedProcs.FromProgNotesRows(new DataRow[] { null }));
			CollectionAssert.AreEqual(new List<long>(),ProgramLinkSelectedProcs.FromProgNotesRows(null));
		}

		[TestMethod]
		public void TreatPlan_ProceduresAndSavedPlanRows() {
			//Procedure's constructor reads preferences, so the test builds one without it.
			Procedure procedure=(Procedure)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Procedure));
			procedure.ProcNum=7;
			List<object> listTags=new List<object> {
				procedure,
				null,//subtotal row
				new ProcTP { ProcTPNum=900,ProcNumOrig=8 },
				"total",
				new ProcTP { ProcTPNum=901,ProcNumOrig=0 },//a saved plan row whose procedure is gone
			};
			CollectionAssert.AreEqual(new List<long> { 7,8 },ProgramLinkSelectedProcs.FromTreatPlanTags(listTags));
		}

		[TestMethod]
		public void Account_OnlyProcedureLedgerRows_InGridOrder() {
			DataTable table=new DataTable("account");
			table.Columns.Add("ProcNum");
			table.Columns.Add("PayNum");
			table.Rows.Add("21","0");
			table.Rows.Add("0","5");//payment
			table.Rows.Add("22","0");
			CollectionAssert.AreEqual(new List<long> { 21,22 },ProgramLinkSelectedProcs.FromLedgerRows(table,new[] { 2,1,0,9,-1 }),
				"Sorted to grid order; out of range indexes are skipped.");
			CollectionAssert.AreEqual(new List<long>(),ProgramLinkSelectedProcs.FromLedgerRows(null,new[] { 0 }));
		}

		[TestMethod]
		public void ReplacementPicker_ListsTheField_OnlyForProgramLinks() {
			List<FrmMessageReplacements.ReplacementField> listForLinks=FrmMessageReplacements.GetReplacementFieldList(true,
				new List<MessageReplaceType> { MessageReplaceType.Patient,MessageReplaceType.Referral,MessageReplaceType.Misc,MessageReplaceType.ProgramLink });
			FrmMessageReplacements.ReplacementField replacementField=listForLinks.Single(x => x.FieldName=="[SelectedProcNums]");
			Assert.IsTrue(replacementField.IsSupported);
			Assert.AreEqual(MessageReplaceType.ProgramLink,replacementField.MessageReplaceTypeCur);
			List<FrmMessageReplacements.ReplacementField> listForEmail=FrmMessageReplacements.GetReplacementFieldList(true,
				new List<MessageReplaceType> { MessageReplaceType.Patient,MessageReplaceType.Misc });
			Assert.IsFalse(listForEmail.Any(x => x.FieldName=="[SelectedProcNums]"),"Email and other templates never replace it, so they don't list it.");
			string sourceOutputFile=TestPaths.ReadRepoFile("OpenDental","Forms","FormProgramLinkOutputFile .cs");
			StringAssert.Contains(sourceOutputFile,"listMessageReplaceTypes.Add(MessageReplaceType.ProgramLink)");
		}

		[TestMethod]
		public void ProgramL_ReplacesWithAndWithoutPatient() {
			string source=TestPaths.ReadRepoFile("WpfControlsOD","Logic","ProgramL.cs");
			StringAssert.Contains(source,"public static void Execute(long programNum,Patient patient,List<long> listSelectedProcNums)");
			StringAssert.Contains(source,"Execute(programNum,patient,null);");
			string body=TestPaths.GetMethodBody(source,"public static void Execute(long programNum,Patient patient,List<long> listSelectedProcNums)");
			int indexPatientBlock=body.IndexOf("if(patient!=null) {\r\n\t\t\t\tcmdline=ReplaceHelper",StringComparison.Ordinal);
			if(indexPatientBlock<0) {
				indexPatientBlock=body.IndexOf("if(patient!=null) {\n\t\t\t\tcmdline=ReplaceHelper",StringComparison.Ordinal);
			}
			Assert.IsTrue(indexPatientBlock>0);
			foreach(string replace in new[] { "cmdline=ProgramLinkSelectedProcs.Replace(cmdline,listSelectedProcNums);",
				"path=ProgramLinkSelectedProcs.Replace(path,listSelectedProcNums);",
				"fileTemplate=ProgramLinkSelectedProcs.Replace(fileTemplate,listSelectedProcNums);" })
			{
				int index=body.IndexOf(replace,StringComparison.Ordinal);
				Assert.IsTrue(index>0 && index<indexPatientBlock,replace+" runs before, and outside, the patient-only block.");
			}
		}

		[TestMethod]
		public void Modules_PassTheirSelection() {
			string chart=TestPaths.ReadRepoFile("OpenDental","Main Modules","ControlChart.cs");
			StringAssert.Contains(chart,"ProgramLinkSelectedProcs.FromProgNotesRows(gridProg.SelectedIndices.Select(x => gridProg.ListGridRows[x].Tag as DataRow))");
			string treat=TestPaths.ReadRepoFile("OpenDental","Main Modules","ControlTreat.cs");
			StringAssert.Contains(treat,"ProgramLinkSelectedProcs.FromTreatPlanTags(gridMain.SelectedIndices.Select(x => gridMain.ListGridRows[x].Tag))");
			string account=TestPaths.ReadRepoFile("OpenDental","Main Modules","ControlAccount.cs");
			StringAssert.Contains(account,"ProgramLinkSelectedProcs.FromLedgerRows(_dataSetMain?.Tables[\"account\"],gridAccount.SelectedIndices)");
			//Other modules and the main toolbar keep the two-argument call, which replaces the field with an empty string.
			foreach(string module in new[] { "ControlAppt.cs","ControlFamily.cs","ControlImages.cs","FormOpenDental.cs" }) {
				string source=TestPaths.ReadRepoFile("OpenDental","Main Modules",module);
				Assert.IsFalse(source.Contains("ProgramLinkSelectedProcs."),module);
			}
		}
	}
}
