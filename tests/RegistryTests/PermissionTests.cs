using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;

namespace RegistryTests {
	///<summary>Permission wording, reserved (HQ-only) permissions, the audit trail list, and where the new permissions sit in the security tree.</summary>
	[TestClass]
	public class PermissionTests {
		private static readonly EnumPermType[] _arrayReserved=new[] { EnumPermType.SendAlertsFromHQ,EnumPermType.TextAllEmployees,EnumPermType.PhoneExtension };
		private static readonly EnumPermType[] _arrayNewInTree=new[] { EnumPermType.ImageSignatureCreate,EnumPermType.SignedImageEdit,
			EnumPermType.BlockoutsFlagged,EnumPermType.PayPlanUnlock,EnumPermType.ProcTPEditFee };

		[TestMethod]
		public void ReservedPermissions_AreHqOnlyAndHaveNoAuditTrail() {
			foreach(EnumPermType permType in _arrayReserved) {
				Assert.IsTrue(GroupPermissions.IsReserved(permType),permType.ToString());
				Assert.IsFalse(GroupPermissions.HasAuditTrail(permType),permType+" must not show in the audit trail filter.");
			}
			foreach(EnumPermType permType in _arrayNewInTree.Concat(new[] { EnumPermType.EFormImport })) {
				Assert.IsFalse(GroupPermissions.IsReserved(permType),permType.ToString());
				Assert.IsTrue(GroupPermissions.HasAuditTrail(permType),permType+" writes audit entries, so it belongs in the audit trail filter.");
			}
			//Older HQ-only permissions keep working the same way.
			Assert.IsTrue(GroupPermissions.IsReserved(EnumPermType.FeatureRequestEdit));
			Assert.IsFalse(GroupPermissions.IsReserved(EnumPermType.SecurityAdmin));
		}

		[TestMethod]
		public void Wording_25_2_And_25_3() {
			//v25_3-security-claimproc-billedtoins-desc: fee or procedure code.
			string desc252=EnumPermType.ClaimProcFieldsBilledToInsEdit.GetDescription();
			StringAssert.Contains(desc252,"fee");
			StringAssert.Contains(desc252,"procedure code");
			//v25_2-security-permission-renames
			StringAssert.Contains(EnumPermType.AppointmentCreate.GetDescription(),"Pinboard");
			StringAssert.Contains(EnumPermType.AppointmentMove.GetDescription(),"any appointment status");
			StringAssert.Contains(EnumPermType.InsWriteOffEdit.GetDescription(),"create or delete");
			//25.1: Blockouts now only covers unflagged blockout types.
			StringAssert.Contains(EnumPermType.Blockouts.GetDescription(),"without");
			Assert.AreEqual("Treatment Planned Procedure Edit Fee",EnumPermType.ProcTPEditFee.GetDescription());
			Assert.AreEqual("Pay Plan Unlock",EnumPermType.PayPlanUnlock.GetDescription());
		}

		[TestMethod]
		public void EveryPermission_HasADescription() {
			foreach(EnumPermType permType in Enum.GetValues(typeof(EnumPermType))) {
				if(permType==EnumPermType.None) {
					continue;
				}
				if((int)permType<264) {
					continue;//Older values: unchanged from 24.3.
				}
				Assert.AreNotEqual(permType.ToString(),permType.GetDescription(),permType+" needs a [Description].");
			}
		}

		[TestMethod]
		public void SeedGrants_NeverGrantReservedPermissions() {
			foreach((EnumPermType permTypeNew,EnumPermType permTypeFrom) in Seed25_3.ListGrants) {
				Assert.IsFalse(GroupPermissions.IsReserved(permTypeNew),permTypeNew.ToString());
			}
			CollectionAssert.AreEquivalent(_arrayNewInTree,Seed25_3.ListGrants.Select(x => x.PermTypeNew).ToArray(),
				"Every new permission in the security tree gets an upgrade grant, so nobody loses access.");
		}

		#region Security tree (source checks; the tree is a WinForms control)
		private static string GetTreeSource() {
			return File.ReadAllText(TestPaths.GetRepoFile("OpenDental","User Controls","UserControlSecurityTree.cs"));
		}

		///<summary>The body of FillTreePermissionsInitial.</summary>
		private static string GetFillTreeSource() {
			string source=GetTreeSource();
			int start=source.IndexOf("public void FillTreePermissionsInitial()");
			int end=source.IndexOf("public void FillTreePerm()");
			Assert.IsTrue(start>0 && end>start,"FillTreePermissionsInitial not found.");
			return source.Substring(start,end-start);
		}

		private static List<string> GetSetNodeOrder(string source) {
			return Regex.Matches(source,@"SetNode\(EnumPermType\.(\w+)\)").Cast<Match>().Select(x => x.Groups[1].Value).ToList();
		}

		///<summary>The text of one #region in FillTreePermissionsInitial, up to the next #region or #endregion at the same depth.</summary>
		private static string GetRegion(string source,string regionName) {
			int start=source.IndexOf("#region "+regionName);
			Assert.IsTrue(start>=0,"Region '"+regionName+"' not found.");
			int end=source.IndexOf("#endregion",start);
			return source.Substring(start,end-start);
		}

		[TestMethod]
		public void SecurityTree_NewPermissionsSitNextToTheirSiblings() {
			string source=GetFillTreeSource();
			List<string> listOrder=GetSetNodeOrder(source);
			foreach(EnumPermType permType in _arrayNewInTree) {
				Assert.AreEqual(1,listOrder.Count(x => x==permType.ToString()),permType+" must appear exactly once in the tree.");
			}
			//Appointments module: flagged blockouts right after Blockouts.
			AssertFollows(listOrder,EnumPermType.Blockouts,EnumPermType.BlockoutsFlagged);
			StringAssert.Contains(GetRegion(source,"Appts Module"),"EnumPermType.BlockoutsFlagged");
			//Account module, Payment Plan node: unlock right after edit.
			AssertFollows(listOrder,EnumPermType.PayPlanEdit,EnumPermType.PayPlanUnlock);
			StringAssert.Contains(GetRegion(source,"Account Module"),"EnumPermType.PayPlanUnlock");
			//Chart module, Procedure node: TP fee right after Show Procedure Fee.
			AssertFollows(listOrder,EnumPermType.ProcEditShowFee,EnumPermType.ProcTPEditFee);
			StringAssert.Contains(GetRegion(source,"Chart Module"),"EnumPermType.ProcTPEditFee");
			//Imaging module: both signature permissions after Image Edit.
			AssertFollows(listOrder,EnumPermType.ImageEdit,EnumPermType.ImageSignatureCreate);
			AssertFollows(listOrder,EnumPermType.ImageSignatureCreate,EnumPermType.SignedImageEdit);
			StringAssert.Contains(GetRegion(source,"Imaging Module"),"EnumPermType.SignedImageEdit");
		}

		private static void AssertFollows(List<string> listOrder,EnumPermType permTypeBefore,EnumPermType permTypeAfter) {
			int idx=listOrder.IndexOf(permTypeBefore.ToString());
			Assert.IsTrue(idx>=0 && idx+1<listOrder.Count,permTypeBefore+" not found in the tree.");
			Assert.AreEqual(permTypeAfter.ToString(),listOrder[idx+1],permTypeAfter+" should come right after "+permTypeBefore+".");
		}

		[TestMethod]
		public void SecurityTree_HidesReservedAndLogOnlyPermissions() {
			List<string> listOrder=GetSetNodeOrder(GetFillTreeSource());
			foreach(EnumPermType permType in _arrayReserved.Concat(new[] { EnumPermType.EFormImport })) {
				Assert.IsFalse(listOrder.Contains(permType.ToString()),permType+" must not be in the security tree.");
			}
			//No reserved permission at all is placed in the tree.
			foreach(string name in listOrder) {
				EnumPermType permType=(EnumPermType)Enum.Parse(typeof(EnumPermType),name);
				if(permType==EnumPermType.FeatureRequestEdit) {
					continue;
				}
				Assert.IsFalse(GroupPermissions.IsReserved(permType),permType+" is reserved but shows in the tree.");
			}
		}

		[TestMethod]
		public void SecurityTree_SetAllSkipsReservedPermissions() {
			string source=GetTreeSource();
			int start=source.IndexOf("public void SetAll()");
			int end=source.IndexOf("public void SetNone()");
			string setAll=source.Substring(start,end-start);
			StringAssert.Contains(setAll,"GroupPermissions.IsReserved(permType)");
		}
		#endregion

		[TestMethod]
		public void OldPermissionName_IsGoneFromTheCode() {
			string root=TestPaths.GetRepositoryRoot();
			List<string> listHits=new List<string>();
			foreach(string folder in new[] { "OpenDental","OpenDentBusiness","WpfControlsOD" }) {
				foreach(string path in Directory.EnumerateFiles(Path.Combine(root,folder),"*.cs",SearchOption.AllDirectories)) {
					if(path.Contains(Path.DirectorySeparatorChar+"obj"+Path.DirectorySeparatorChar)) {
						continue;
					}
					if(Path.GetFileName(path).StartsWith("ConvertDatabases")) {
						continue;//The frozen legacy conversion chain keeps its old comment; it stores the number 252, not the name.
					}
					if(File.ReadAllText(path).Contains("EnumPermType.ClaimProcFeeBilledToInsEdit")) {
						listHits.Add(path);
					}
				}
			}
			Assert.AreEqual(0,listHits.Count,string.Join(", ",listHits));
		}
	}
}
