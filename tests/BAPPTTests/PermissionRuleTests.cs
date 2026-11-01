// Unit tests for the Appointment Create and Appointment Move scope of Open Dental 25.3 (items v25_3-security-apptcreate-pinboard and
// v25_3-security-apptmove-scope): which permission each pinboard and unschedule action needs. No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BAPPTTests {
	[TestClass]
	public class PermissionRuleTests {
		private static IEnumerable<ApptStatus> AllStatuses() {
			return Enum.GetValues(typeof(ApptStatus)).Cast<ApptStatus>();
		}

		[TestMethod]
		public void PermNumbers_MatchOpenDental() {
			Assert.AreEqual(25,(int)EnumPermType.AppointmentCreate);
			Assert.AreEqual(26,(int)EnumPermType.AppointmentMove);
		}

		[TestMethod]
		public void Unschedule_CompleteNeedsMoveAndCompleteEdit() {
			CollectionAssert.AreEquivalent(new[] { EnumPermType.AppointmentCompleteEdit,EnumPermType.AppointmentMove },
				ApptPermissionRules.GetPermsToUnschedule(ApptStatus.Complete));
		}

		[TestMethod]
		public void Unschedule_EveryStatusNeedsMove() {
			foreach(ApptStatus aptStatus in AllStatuses()) {
				List<EnumPermType> listPermTypes=ApptPermissionRules.GetPermsToUnschedule(aptStatus);
				Assert.IsTrue(listPermTypes.Contains(EnumPermType.AppointmentMove),aptStatus+" needs Appointment Move.");
				if(aptStatus!=ApptStatus.Complete) {
					Assert.AreEqual(1,listPermTypes.Count,aptStatus+" needs only Appointment Move.");
				}
			}
		}

		[TestMethod]
		public void Pin_PlannedIsACopyAndNeedsNoMove() {
			Assert.IsFalse(ApptPermissionRules.IsMoveNeededToPin(ApptStatus.Planned));
			foreach(ApptStatus aptStatus in AllStatuses().Where(x => x!=ApptStatus.Planned)) {
				Assert.IsTrue(ApptPermissionRules.IsMoveNeededToPin(aptStatus),aptStatus+" is moved, so it needs Appointment Move.");
			}
		}

		[TestMethod]
		public void PlannedGrid_OnlyRowsWithAScheduledAppointmentAreMoves() {
			//The Chart's Planned Appointments grid sends the scheduled appointment for these, and copies the planned appointment otherwise.
			Assert.IsTrue(ApptPermissionRules.IsPlannedRowPinAMove(ApptStatus.Complete));
			Assert.IsTrue(ApptPermissionRules.IsPlannedRowPinAMove(ApptStatus.Scheduled));
			Assert.IsTrue(ApptPermissionRules.IsPlannedRowPinAMove(ApptStatus.UnschedList));
			Assert.IsTrue(ApptPermissionRules.IsPlannedRowPinAMove(ApptStatus.Broken));
			Assert.IsFalse(ApptPermissionRules.IsPlannedRowPinAMove(ApptStatus.None),"No scheduled appointment: the planned one is copied.");
			Assert.IsFalse(ApptPermissionRules.IsPlannedRowPinAMove(ApptStatus.Planned));
		}

		[TestMethod]
		public void DropFromPinboard_PlannedNeedsCreateAndNothingNeedsMove() {
			Assert.AreEqual(EnumPermType.AppointmentCreate,ApptPermissionRules.GetPermToDropFromPinboard(ApptStatus.Planned));
			foreach(ApptStatus aptStatus in AllStatuses()) {
				Assert.AreNotEqual(EnumPermType.AppointmentMove,ApptPermissionRules.GetPermToDropFromPinboard(aptStatus),
					"Scheduling from the pinboard never needs Appointment Move ("+aptStatus+").");
			}
			Assert.AreEqual(EnumPermType.None,ApptPermissionRules.GetPermToDropFromPinboard(ApptStatus.UnschedList),
				"Moving off the Unscheduled List stays unblocked.");
		}

		[TestMethod]
		public void DisplayText_DescribesTheScope() {
			//The wording is package M1-02's; this only checks that it still describes what B-APPT enforces.
			string create=GetDescription(EnumPermType.AppointmentCreate);
			string move=GetDescription(EnumPermType.AppointmentMove);
			StringAssert.Contains(create.ToLower(),"planned appointment");
			StringAssert.Contains(create.ToLower(),"pinboard");
			StringAssert.Contains(move.ToLower(),"any appointment status");
		}

		private static string GetDescription(EnumPermType permType) {
			FieldInfo fieldInfo=typeof(EnumPermType).GetField(permType.ToString());
			System.ComponentModel.DescriptionAttribute descriptionAttribute=(System.ComponentModel.DescriptionAttribute)Attribute.GetCustomAttribute(fieldInfo,
				typeof(System.ComponentModel.DescriptionAttribute));
			return descriptionAttribute?.Description??"";
		}
	}
}
