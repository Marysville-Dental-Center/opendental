// B-SHELL (v25_1-setup-programlink-selectedprocnums): the [SelectedProcNums] program-link replacement field.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace OpenDentBusiness {
	///<summary>The [SelectedProcNums] field of program links (Open Dental 25.1, backported to 24.4.34). In the command line, the path or URL and the
	///output-file template it becomes the ProcNums of the procedures selected in the module whose toolbar button was clicked, comma separated with
	///no spaces, in grid order. Chart: selected procedure rows of the progress notes (group notes and non-procedure rows are skipped). Treatment
	///Plan: selected procedures. Account: selected ledger rows that are procedures. Every other module and the main toolbar: empty.
	///No selection gives an empty string. The rules are our design; Open Dental's program link pages do not describe the field.</summary>
	public static class ProgramLinkSelectedProcs {
		public const string FIELD="[SelectedProcNums]";
		///<summary>procedurelog.ProcCode of a group note row in the progress notes table.</summary>
		private const string PROC_CODE_GROUP_NOTE="~GRP~";

		///<summary>Comma separated, no spaces, in the given order. Zeros and repeats are left out.</summary>
		public static string Format(IEnumerable<long> listProcNums) {
			if(listProcNums==null) {
				return "";
			}
			List<long> listDistinct=new List<long>();
			foreach(long procNum in listProcNums) {
				if(procNum<=0 || listDistinct.Contains(procNum)) {
					continue;
				}
				listDistinct.Add(procNum);
			}
			return string.Join(",",listDistinct);
		}

		///<summary>Replaces every [SelectedProcNums] in text. A null list replaces it with an empty string, so the literal tag never reaches the other
		///program.</summary>
		public static string Replace(string text,IEnumerable<long> listProcNums) {
			if(string.IsNullOrEmpty(text) || !text.Contains(FIELD)) {
				return text;
			}
			return text.Replace(FIELD,Format(listProcNums));
		}

		///<summary>Chart: rows of the progress notes table (Tag of each selected gridProg row), in grid order. Rows without a ProcNum (appointments,
		///commlogs, tasks, sheets...) and group notes are skipped.</summary>
		public static List<long> FromProgNotesRows(IEnumerable<DataRow> listDataRows) {
			List<long> listProcNums=new List<long>();
			if(listDataRows==null) {
				return listProcNums;
			}
			foreach(DataRow dataRow in listDataRows) {
				if(dataRow==null || !dataRow.Table.Columns.Contains("ProcNum")) {
					continue;
				}
				long procNum=PIn.Long(dataRow["ProcNum"].ToString());
				if(procNum<=0) {
					continue;
				}
				if(dataRow.Table.Columns.Contains("ProcCode") && dataRow["ProcCode"].ToString()==PROC_CODE_GROUP_NOTE) {
					continue;
				}
				listProcNums.Add(procNum);
			}
			return listProcNums;
		}

		///<summary>Treatment Plan: the Tag of each selected row, in grid order. A Procedure (active or inactive plan) gives its ProcNum, a ProcTP (saved
		///plan) the procedure it was copied from. Subtotal and total rows (no tag) are skipped.</summary>
		public static List<long> FromTreatPlanTags(IEnumerable<object> listTags) {
			List<long> listProcNums=new List<long>();
			if(listTags==null) {
				return listProcNums;
			}
			foreach(object tag in listTags) {
				if(tag is Procedure procedure) {
					listProcNums.Add(procedure.ProcNum);
				}
				else if(tag is ProcTP procTP) {
					listProcNums.Add(procTP.ProcNumOrig);
				}
			}
			return listProcNums.FindAll(x => x>0);
		}

		///<summary>Account: the selected rows of the account ledger table, by index in grid order. Indexes past the table and rows that are not
		///procedures (ProcNum 0: payments, adjustments, claims...) are skipped.</summary>
		public static List<long> FromLedgerRows(DataTable tableAccount,IEnumerable<int> listSelectedIndices) {
			List<long> listProcNums=new List<long>();
			if(tableAccount==null || listSelectedIndices==null || !tableAccount.Columns.Contains("ProcNum")) {
				return listProcNums;
			}
			foreach(int index in listSelectedIndices.OrderBy(x => x)) {
				if(index<0 || index>=tableAccount.Rows.Count) {
					continue;
				}
				long procNum=PIn.Long(tableAccount.Rows[index]["ProcNum"].ToString());
				if(procNum>0) {
					listProcNums.Add(procNum);
				}
			}
			return listProcNums;
		}
	}
}
