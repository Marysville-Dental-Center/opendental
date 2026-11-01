// Permission wording, reserved permissions, the audit trail list and the security tree for the Open Dental 25.4 and 26.1 permissions.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations.Seeds26_1;

namespace M202Tests {
	[TestClass]
	public class Permission26_1Tests {
		private static readonly EnumPermType[] _arrayReserved26_1=new[] { EnumPermType.PhoneExtension,EnumPermType.LogScraperHQ };
		private static readonly EnumPermType[] _arrayNewInTree=new[] { EnumPermType.BlockoutAdd,EnumPermType.BlockoutDelete };

		[TestMethod]
		public void ReservedPermissions_AreHqOnlyWithoutAuditTrail() {
			foreach(EnumPermType permType in _arrayReserved26_1) {
				Assert.IsTrue(GroupPermissions.IsReserved(permType),permType.ToString());
				Assert.IsFalse(GroupPermissions.HasAuditTrail(permType),permType+" must not show in the audit trail filter.");
			}
		}

		[TestMethod]
		public void NewPermissions_AreUsableAndAudited() {
			foreach(EnumPermType permType in _arrayNewInTree.Concat(new[] { EnumPermType.PatientSelected })) {
				Assert.IsFalse(GroupPermissions.IsReserved(permType),permType.ToString());
				Assert.IsTrue(GroupPermissions.HasAuditTrail(permType),permType+" writes audit entries, so it belongs in the audit trail filter.");
				Assert.IsFalse(GroupPermissions.PermTakesDates(permType),permType+": blockout and audit permissions take no date limits, like Blockouts (13).");
			}
		}

		[TestMethod]
		public void Wording_25_4() {
			Assert.AreEqual("Blockout Add",EnumPermType.BlockoutAdd.GetDescription());
			Assert.AreEqual("Blockout Delete",EnumPermType.BlockoutDelete.GetDescription());
			Assert.AreEqual("Patient Selected",EnumPermType.PatientSelected.GetDescription());
			StringAssert.StartsWith(EnumPermType.Blockouts.GetDescription(),"Blockout Edit");
			StringAssert.Contains(EnumPermType.Blockouts.GetDescription(),"without","13 still covers only unflagged blockouts.");
			foreach(EnumPermType permType in Enum.GetValues(typeof(EnumPermType)).Cast<EnumPermType>().Where(x => (int)x>=272)) {
				Assert.AreNotEqual(permType.ToString(),permType.GetDescription(),permType+" needs a [Description].");
			}
		}

		[TestMethod]
		public void SeedGrants_SplitBlockoutsAndNeverGrantReserved() {
			foreach((EnumPermType permTypeNew,EnumPermType permTypeFrom) in Seed26_1.ListGrants) {
				Assert.IsFalse(GroupPermissions.IsReserved(permTypeNew),permTypeNew.ToString());
				Assert.AreEqual(EnumPermType.Blockouts,permTypeFrom);
			}
			CollectionAssert.AreEquivalent(_arrayNewInTree,Seed26_1.ListGrants.Select(x => x.PermTypeNew).ToArray(),
				"Every new permission in the security tree gets an upgrade grant, so nobody loses access.");
		}

		#region Security tree (source checks; the tree is a WinForms control)
		private static string GetFillTreeSource() {
			string source=File.ReadAllText(TestPaths.GetRepoFile("OpenDental","User Controls","UserControlSecurityTree.cs"));
			int start=source.IndexOf("public void FillTreePermissionsInitial()");
			int end=source.IndexOf("public void FillTreePerm()");
			Assert.IsTrue(start>0 && end>start,"FillTreePermissionsInitial not found.");
			return source.Substring(start,end-start);
		}

		private static List<string> GetSetNodeOrder(string source) {
			return Regex.Matches(source,@"SetNode\(EnumPermType\.(\w+)\)").Cast<Match>().Select(x => x.Groups[1].Value).ToList();
		}

		[TestMethod]
		public void SecurityTree_BlockoutPermissionsSitTogetherInTheAppointmentsModule() {
			string source=GetFillTreeSource();
			List<string> listOrder=GetSetNodeOrder(source);
			foreach(EnumPermType permType in _arrayNewInTree) {
				Assert.AreEqual(1,listOrder.Count(x => x==permType.ToString()),permType+" must appear exactly once in the tree.");
			}
			int idx=listOrder.IndexOf(nameof(EnumPermType.BlockoutAdd));
			//Ordered by their text: Blockout Add, Blockout Delete, Blockout Edit (13), Blockouts flagged (266).
			CollectionAssert.AreEqual(new[] { "BlockoutAdd","BlockoutDelete","Blockouts","BlockoutsFlagged" },listOrder.Skip(idx).Take(4).ToList());
			int start=source.IndexOf("#region Appts Module");
			int end=source.IndexOf("#endregion",start);
			Assert.IsTrue(start>=0 && end>start);
			string region=source.Substring(start,end-start);
			StringAssert.Contains(region,"EnumPermType.BlockoutAdd");
			StringAssert.Contains(region,"EnumPermType.BlockoutDelete");
		}

		[TestMethod]
		public void SecurityTree_HidesReservedAndAuditOnlyPermissions() {
			List<string> listOrder=GetSetNodeOrder(GetFillTreeSource());
			foreach(EnumPermType permType in _arrayReserved26_1.Concat(new[] { EnumPermType.PatientSelected })) {
				Assert.IsFalse(listOrder.Contains(permType.ToString()),permType+" must not be in the security tree.");
			}
		}
		#endregion
	}
}
