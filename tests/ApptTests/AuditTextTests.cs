// Unit tests for the audit text generators (backlog item v25_3-appts-audit-detail): one "Field: old → new" line per change in Edit Appointment,
// the move detail for drags and pinboard drops, the unschedule detail, and the 4,000-character cut. No database: names come from stubs.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ApptTests {
	[TestClass]
	public class AuditTextTests {
		private const string ARROW=" → ";
		private CultureInfo _cultureInfoOld;

		[TestInitialize]
		public void TestInitialize() {
			//Audit text uses the current culture for dates and English text when the culture is en-US (no translation lookup).
			_cultureInfoOld=Thread.CurrentThread.CurrentCulture;
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		[TestCleanup]
		public void TestCleanup() {
			Thread.CurrentThread.CurrentCulture=_cultureInfoOld;
		}

		private static ApptAuditNames Names() {
			return new ApptAuditNames {
				FuncGetProvAbbr=x => "Prov"+x,
				FuncGetOpAbbr=x => "Op"+x,
				FuncGetEmployeeName=x => "Emp"+x,
				FuncGetApptTypeName=x => "Type"+x,
				FuncGetClinicAbbr=x => "Clinic"+x,
				FuncGetConfirmedName=x => "Conf"+x,
			};
		}

		private static Appointment GetAppt() {
			return new Appointment { AptNum=7,PatNum=3,AptDateTime=new DateTime(2026,10,20,9,0,0),AptStatus=ApptStatus.Scheduled,Op=10,ProvNum=1,
				ProvHyg=2,Assistant=4,Pattern="XXXX",Confirmed=21,Priority=ApptPriority.Normal,AppointmentTypeNum=0,ClinicNum=0,Note="private text",
				ColorOverride=Color.Empty };
		}

		private static string[] Lines(string text) {
			return text.Split(new[] { "\r\n" },StringSplitOptions.None);
		}

		#region GetChangeSummary
		[TestMethod]
		public void Change_NothingChanged_Empty() {
			Assert.AreEqual("",Appointments.GetChangeSummary(GetAppt(),GetAppt(),null,null,Names()));
			Assert.AreEqual("",Appointments.GetChangeSummary(GetAppt(),GetAppt(),new List<string> { "D0120" },new List<string> { "D0120" },Names()));
			Assert.AreEqual("",Appointments.GetChangeSummary(null,GetAppt(),null,null,Names()));
		}

		[TestMethod]
		public void Change_ProvidersAndAssistant() {
			Appointment appointmentNew=GetAppt();
			appointmentNew.ProvNum=5;
			appointmentNew.ProvHyg=0;
			appointmentNew.Assistant=6;
			CollectionAssert.AreEqual(new[] { "Provider: Prov1"+ARROW+"Prov5","Hygienist: Prov2"+ARROW+"none","Assistant: Emp4"+ARROW+"Emp6" },
				Lines(Appointments.GetChangeSummary(GetAppt(),appointmentNew,null,null,Names())));
		}

		[TestMethod]
		public void Change_PlacementAndTiming() {
			Appointment appointmentNew=GetAppt();
			appointmentNew.Op=11;
			appointmentNew.AptDateTime=new DateTime(2026,10,21,14,30,0);
			appointmentNew.Pattern="XXXXXX";
			CollectionAssert.AreEqual(new[] { "Operatory: Op10"+ARROW+"Op11","Date/Time: 10/20/2026 9:00 AM"+ARROW+"10/21/2026 2:30 PM",
				"Length: 20 min"+ARROW+"30 min" },
				Lines(Appointments.GetChangeSummary(GetAppt(),appointmentNew,null,null,Names())));
		}

		[TestMethod]
		public void Change_StatusAndFlags() {
			Appointment appointmentOld=GetAppt();
			Appointment appointmentNew=GetAppt();
			appointmentNew.AptStatus=ApptStatus.UnschedList;
			appointmentNew.Confirmed=22;
			appointmentNew.Priority=ApptPriority.ASAP;
			appointmentNew.IsHygiene=true;
			appointmentNew.IsNewPatient=true;
			appointmentNew.IsMirrored=true;
			CollectionAssert.AreEqual(new[] { "Status: Scheduled"+ARROW+"Unscheduled List","Confirmed: Conf21"+ARROW+"Conf22","Priority: Normal"+ARROW+"ASAP",
				"Hygiene: off"+ARROW+"on","New Patient: off"+ARROW+"on","Mirror: off"+ARROW+"on" },
				Lines(Appointments.GetChangeSummary(appointmentOld,appointmentNew,null,null,Names())));
		}

		[TestMethod]
		public void Change_MirrorOnToOff() {
			Appointment appointmentOld=GetAppt();
			appointmentOld.IsMirrored=true;
			Assert.AreEqual("Mirror: on"+ARROW+"off",Appointments.GetChangeSummary(appointmentOld,GetAppt(),null,null,Names()));
		}

		[TestMethod]
		public void Change_TypeAndClinic() {
			Appointment appointmentNew=GetAppt();
			appointmentNew.AppointmentTypeNum=3;
			appointmentNew.ClinicNum=2;
			CollectionAssert.AreEqual(new[] { "Appointment Type: none"+ARROW+"Type3","Clinic: none"+ARROW+"Clinic2" },
				Lines(Appointments.GetChangeSummary(GetAppt(),appointmentNew,null,null,Names())));
		}

		[TestMethod]
		public void Change_ProcedureCodesAddedAndRemoved_KeepsDuplicates() {
			List<string> listOld=new List<string> { "D0120","D1110","D1110" };
			List<string> listNew=new List<string> { "D1110","D0274" };
			CollectionAssert.AreEqual(new[] { "Procedures added: D0274","Procedures removed: D0120, D1110" },
				Lines(Appointments.GetChangeSummary(GetAppt(),GetAppt(),listOld,listNew,Names())));
		}

		[TestMethod]
		public void Change_ProcedureCodesSkippedWhenEitherListIsNull() {
			Assert.AreEqual("",Appointments.GetChangeSummary(GetAppt(),GetAppt(),new List<string> { "D0120" },null,Names()));
		}

		[TestMethod]
		public void Change_NoteSaysOnlyThatItChanged() {
			Appointment appointmentNew=GetAppt();
			appointmentNew.Note="new private text";
			string summary=Appointments.GetChangeSummary(GetAppt(),appointmentNew,null,null,Names());
			Assert.AreEqual("Note changed",summary);
			Assert.IsFalse(summary.Contains("private"));
			appointmentNew.Note=null;
			Appointment appointmentOld=GetAppt();
			appointmentOld.Note="";
			Assert.AreEqual("",Appointments.GetChangeSummary(appointmentOld,appointmentNew,null,null,Names()),"Null and empty notes are the same.");
		}

		[TestMethod]
		public void Change_MissingOrFailingNamesFallBackToNumber() {
			ApptAuditNames names=Names();
			names.FuncGetProvAbbr=x => null;
			names.FuncGetOpAbbr=x => throw new Exception("cache miss");
			Appointment appointmentNew=GetAppt();
			appointmentNew.ProvNum=5;
			appointmentNew.Op=11;
			CollectionAssert.AreEqual(new[] { "Provider: #1"+ARROW+"#5","Operatory: #10"+ARROW+"#11" },
				Lines(Appointments.GetChangeSummary(GetAppt(),appointmentNew,null,null,names)));
		}

		[TestMethod]
		public void Change_DefaultNamesDoNotNeedADatabaseForZero() {
			//With no names passed, the cache lookups are used, but zero keys never reach them.
			Appointment appointmentOld=GetAppt();
			appointmentOld.Assistant=0;
			appointmentOld.IsMirrored=true;
			Appointment appointmentNew=GetAppt();
			appointmentNew.Assistant=0;
			Assert.AreEqual("Mirror: on"+ARROW+"off",Appointments.GetChangeSummary(appointmentOld,appointmentNew));
		}
		#endregion GetChangeSummary

		#region GetMoveSummary and GetUnscheduleSummary
		[TestMethod]
		public void Move_DateOpAndProviders() {
			Appointment appointmentNew=GetAppt();
			appointmentNew.AptDateTime=new DateTime(2026,10,20,10,0,0);
			appointmentNew.Op=12;
			appointmentNew.ProvNum=8;
			appointmentNew.ProvHyg=9;
			appointmentNew.Note="changed, but not part of a move";
			CollectionAssert.AreEqual(new[] { "Date/Time: 10/20/2026 9:00 AM"+ARROW+"10/20/2026 10:00 AM","Operatory: Op10"+ARROW+"Op12",
				"Provider: Prov1"+ARROW+"Prov8","Hygienist: Prov2"+ARROW+"Prov9" },
				Lines(Appointments.GetMoveSummary(GetAppt(),appointmentNew,Names())));
		}

		[TestMethod]
		public void Move_OnlyTimeChanged() {
			Appointment appointmentNew=GetAppt();
			appointmentNew.AptDateTime=new DateTime(2026,10,20,9,30,0);
			Assert.AreEqual("Date/Time: 10/20/2026 9:00 AM"+ARROW+"10/20/2026 9:30 AM",Appointments.GetMoveSummary(GetAppt(),appointmentNew,Names()));
		}

		[TestMethod]
		public void Move_FromUnscheduledList_NoOldDate() {
			Appointment appointmentOld=GetAppt();
			appointmentOld.AptDateTime=DateTime.MinValue;
			appointmentOld.Op=0;
			CollectionAssert.AreEqual(new[] { "Date/Time: none"+ARROW+"10/20/2026 9:00 AM","Operatory: none"+ARROW+"Op10" },
				Lines(Appointments.GetMoveSummary(appointmentOld,GetAppt(),Names())));
		}

		[TestMethod]
		public void Move_NothingChanged_Empty() {
			Assert.AreEqual("",Appointments.GetMoveSummary(GetAppt(),GetAppt(),Names()));
		}

		[TestMethod]
		public void Unschedule_OldStatusAndSlot() {
			CollectionAssert.AreEqual(new[] { "Status: Scheduled"+ARROW+"Unscheduled List","Previous slot: 10/20/2026 9:00 AM, Op10" },
				Lines(Appointments.GetUnscheduleSummary(GetAppt(),Names())));
			Appointment appointmentComplete=GetAppt();
			appointmentComplete.AptStatus=ApptStatus.Complete;
			appointmentComplete.Op=0;
			CollectionAssert.AreEqual(new[] { "Status: Complete"+ARROW+"Unscheduled List","Previous slot: 10/20/2026 9:00 AM" },
				Lines(Appointments.GetUnscheduleSummary(appointmentComplete,Names())));
		}

		[TestMethod]
		public void Create_PlaceProvidersLengthAndMirror() {
			Appointment appointment=GetAppt();
			appointment.IsMirrored=true;
			CollectionAssert.AreEqual(new[] { "Operatory: Op10","Provider: Prov1","Hygienist: Prov2","Length: 20 min","Mirror: on" },
				Lines(Appointments.GetCreateSummary(appointment,Names())));
			appointment=new Appointment { Pattern="" };
			Assert.AreEqual("",Appointments.GetCreateSummary(appointment,Names()),"Empty fields are left out.");
			Assert.AreEqual("",Appointments.GetCreateSummary(null,Names()));
		}

		[TestMethod]
		public void StatusText_ReadableNames() {
			Assert.AreEqual("Unscheduled List",Appointments.GetStatusText(ApptStatus.UnschedList));
			Assert.AreEqual("Patient Note",Appointments.GetStatusText(ApptStatus.PtNote));
			Assert.AreEqual("Patient Note Completed",Appointments.GetStatusText(ApptStatus.PtNoteCompleted));
			Assert.AreEqual("Broken",Appointments.GetStatusText(ApptStatus.Broken));
		}
		#endregion GetMoveSummary and GetUnscheduleSummary

		#region Length limit
		[TestMethod]
		public void Truncate_KeepsShortTextAndCutsLongText() {
			Assert.IsNull(Appointments.TruncateAuditText(null));
			Assert.AreEqual("abc",Appointments.TruncateAuditText("abc"));
			string text4000=new string('a',4000);
			Assert.AreEqual(text4000,Appointments.TruncateAuditText(text4000));
			string cut=Appointments.TruncateAuditText(new string('b',5000));
			Assert.AreEqual(Appointments.MAX_AUDIT_TEXT_LENGTH,cut.Length);
			Assert.IsTrue(cut.EndsWith("…"));
			Assert.AreEqual(new string('b',3999),cut.Substring(0,3999));
		}

		[TestMethod]
		public void Append_DetailOnANewLine_WithinTheLimit() {
			Assert.AreEqual("base",Appointments.AppendAuditDetail("base",""));
			Assert.AreEqual("base",Appointments.AppendAuditDetail("base",null));
			Assert.AreEqual("base\r\nMirror: off"+ARROW+"on",Appointments.AppendAuditDetail("base","Mirror: off"+ARROW+"on"));
			Assert.AreEqual("detail",Appointments.AppendAuditDetail(null,"detail").Trim());
			string result=Appointments.AppendAuditDetail(new string('x',3990),new string('y',100));
			Assert.AreEqual(4000,result.Length);
			Assert.IsTrue(result.EndsWith("…"));
		}

		[TestMethod]
		public void Change_ManyProceduresStayWithinTheLimitOnceAppended() {
			List<string> listNew=Enumerable.Range(0,2000).Select(x => "D"+x.ToString("0000")).ToList();
			string summary=Appointments.GetChangeSummary(GetAppt(),GetAppt(),new List<string>(),listNew,Names());
			Assert.IsTrue(summary.Length>4000);
			Assert.AreEqual(4000,Appointments.AppendAuditDetail("10/20/2026, D0120",summary).Length);
		}
		#endregion Length limit
	}
}
