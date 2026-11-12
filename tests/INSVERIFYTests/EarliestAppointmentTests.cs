// The Insurance Verification List's choice of the earliest appointment of each patient plan, verify type and key, made in C# since INSV3
// (InsVerifies.RemoveLaterAppointmentRows, a private method, docs/port-notes/INSVERIFY.md): INSV2 made it in SQL with ROW_NUMBER() OVER, which
// needs MariaDB 10.2 or MySQL 8.0. The query orders its rows by AptDateTime, AptNum, PatPlanNum, VerifyType; the choice keeps the first row of
// each key and the order of the rows it keeps. These tests build that table by hand, so they need no database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace INSVERIFYTests {
	[TestClass]
	public class EarliestAppointmentTests {
		private static readonly DateTime _date=new DateTime(2026,3,2);

		[TestMethod]
		public void RemoveLaterAppointmentRows_KeepsTheFirstRowOfEachKeyInTheQuerysOrder() {
			//Rows in the query's order. Patient plan 10 has a plan whose PlanNum is also 10 (the keys are equal), patient plans 12 and 13 share plan 5.
			DataTable table=NewTable();
			AddRow(table,1,_date.AddHours(9),100,10,VerifyTypes.InsuranceBenefit,10);//kept: the first of 10, benefits, 10
			AddRow(table,2,_date.AddHours(9),100,10,VerifyTypes.PatientEnrollment,10);//kept: the first of 10, eligibility, 10 (the same FKey)
			AddRow(table,3,_date.AddHours(9),101,12,VerifyTypes.InsuranceBenefit,5);//kept: same time, the next AptNum
			AddRow(table,4,_date.AddHours(10),102,10,VerifyTypes.InsuranceBenefit,10);//a later appointment of 10, benefits, 10
			AddRow(table,5,_date.AddDays(1).AddHours(8),103,11,VerifyTypes.PatientEnrollment,11);//kept
			AddRow(table,6,_date.AddDays(1).AddHours(8),104,12,VerifyTypes.InsuranceBenefit,5);//a later appointment of 12, benefits, 5
			AddRow(table,7,_date.AddDays(2).AddHours(8),105,10,VerifyTypes.PatientEnrollment,10);//a later appointment of 10, eligibility, 10
			AddRow(table,8,_date.AddDays(2).AddHours(8),106,13,VerifyTypes.InsuranceBenefit,5);//kept: plan 5 again, but patient plan 13
			RemoveLaterAppointmentRows(table);
			CollectionAssert.AreEqual(new List<long> { 1,2,3,5,8 },GetRowIds(table),"the first row of each key, in the query's order");
			Assert.AreEqual(5,table.Rows.Count);
			Assert.AreEqual(0,table.Rows.Cast<DataRow>().Count(x => x.RowState==DataRowState.Deleted),"no row left marked deleted");
		}

		[TestMethod]
		public void RemoveLaterAppointmentRows_RowsReadFromTheDatabase() {
			//The table as Db.GetTable fills it: its rows are unchanged, not added. The same choice.
			DataTable table=NewTable();
			AddRow(table,1,_date.AddHours(9),100,20,VerifyTypes.PatientEnrollment,20);
			AddRow(table,2,_date.AddHours(9),101,20,VerifyTypes.PatientEnrollment,20);
			AddRow(table,3,_date.AddHours(11),102,21,VerifyTypes.PatientEnrollment,21);
			AddRow(table,4,_date.AddHours(12),103,21,VerifyTypes.PatientEnrollment,21);
			table.AcceptChanges();
			RemoveLaterAppointmentRows(table);
			CollectionAssert.AreEqual(new List<long> { 1,3 },GetRowIds(table));
		}

		[TestMethod]
		public void RemoveLaterAppointmentRows_NoRows() {
			DataTable table=NewTable();
			RemoveLaterAppointmentRows(table);
			Assert.AreEqual(0,table.Rows.Count);
		}

		///<summary>The columns the choice reads, typed as MySqlConnector fills them (bigint as long, tinyint as sbyte, datetime as DateTime), and
		///InsVerifyNum, which names the rows here.</summary>
		private static DataTable NewTable() {
			DataTable table=new DataTable();
			table.Columns.Add("InsVerifyNum",typeof(long));
			table.Columns.Add("AptDateTime",typeof(DateTime));
			table.Columns.Add("AptNum",typeof(long));
			table.Columns.Add("PatPlanNum",typeof(long));
			table.Columns.Add("VerifyType",typeof(sbyte));
			table.Columns.Add("FKey",typeof(long));
			return table;
		}

		private static void AddRow(DataTable table,long id,DateTime dateTimeApt,long aptNum,long patPlanNum,VerifyTypes verifyType,long fKey) {
			table.Rows.Add(id,dateTimeApt,aptNum,patPlanNum,(sbyte)verifyType,fKey);
		}

		private static List<long> GetRowIds(DataTable table) {
			return table.Rows.Cast<DataRow>().Select(x => (long)x["InsVerifyNum"]).ToList();
		}

		private static void RemoveLaterAppointmentRows(DataTable table) {
			MethodInfo methodInfo=typeof(InsVerifies).GetMethod("RemoveLaterAppointmentRows",BindingFlags.NonPublic|BindingFlags.Static);
			Assert.IsNotNull(methodInfo,"InsVerifies.RemoveLaterAppointmentRows");
			methodInfo.Invoke(null,new object[] { table });
		}
	}
}
