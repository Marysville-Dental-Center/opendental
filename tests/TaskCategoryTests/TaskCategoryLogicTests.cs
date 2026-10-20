// Unit tests for the task category helpers in OpenDentBusiness/Data Interface/Tasks.cs (feature v25_2-manage-task-category). No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace TaskCategoryTests {
	[TestClass]
	public class TaskCategoryLogicTests {
		///<summary>Synthetic Task Categories definitions, in item order: Billing (red), Clinical (hidden, blue), Front Desk (no color),
		///Insurance (hidden, no color).</summary>
		private static List<Def> GetDefs() {
			return new List<Def> {
				new Def { DefNum=11,Category=DefCat.TaskCategories,ItemOrder=0,ItemName="Billing",ItemColor=Color.FromArgb(255,200,0,0) },
				new Def { DefNum=12,Category=DefCat.TaskCategories,ItemOrder=1,ItemName="Clinical",ItemColor=Color.FromArgb(255,0,0,200),IsHidden=true },
				new Def { DefNum=13,Category=DefCat.TaskCategories,ItemOrder=2,ItemName="Front Desk",ItemColor=Color.Empty },
				new Def { DefNum=14,Category=DefCat.TaskCategories,ItemOrder=3,ItemName="Insurance",ItemColor=Color.Empty,IsHidden=true },
			};
		}

		[TestMethod]
		public void Choices_NoCategory_AreTheCategoriesThatAreNotHidden() {
			List<long> listDefNums=Tasks.GetCategoryChoices(0,GetDefs()).Select(x => x.DefNum).ToList();
			CollectionAssert.AreEqual(new List<long> { 11,13 },listDefNums);
		}

		[TestMethod]
		public void Choices_HiddenCategoryAlreadyAssigned_IsKeptInDefinitionOrder() {
			List<long> listDefNums=Tasks.GetCategoryChoices(12,GetDefs()).Select(x => x.DefNum).ToList();
			CollectionAssert.AreEqual(new List<long> { 11,12,13 },listDefNums);
		}

		[TestMethod]
		public void Choices_VisibleCategoryAssigned_DoesNotBringBackOtherHiddenCategories() {
			List<long> listDefNums=Tasks.GetCategoryChoices(11,GetDefs()).Select(x => x.DefNum).ToList();
			CollectionAssert.AreEqual(new List<long> { 11,13 },listDefNums);
		}

		[TestMethod]
		public void Choices_CategoryWhoseDefinitionIsGone_OffersTheVisibleCategories() {
			List<long> listDefNums=Tasks.GetCategoryChoices(99,GetDefs()).Select(x => x.DefNum).ToList();
			CollectionAssert.AreEqual(new List<long> { 11,13 },listDefNums);
		}

		[TestMethod]
		public void Choices_NoDefinitions_IsEmpty() {
			Assert.AreEqual(0,Tasks.GetCategoryChoices(0,new List<Def>()).Count);
		}

		[TestMethod]
		public void Text_IsTheCategoryName_OrBlankForNoneOrMissing() {
			List<Def> listDefs=GetDefs();
			Assert.AreEqual("",Tasks.GetCategoryText(0,listDefs),"0 means no category.");
			Assert.AreEqual("Billing",Tasks.GetCategoryText(11,listDefs));
			Assert.AreEqual("Clinical",Tasks.GetCategoryText(12,listDefs),"A hidden category that a task still has is shown.");
			Assert.AreEqual("",Tasks.GetCategoryText(99,listDefs),"A category whose definition is gone shows blank.");
		}

		[TestMethod]
		public void Color_IsTheCategoryColor_OrEmptySoTheRowColorShows() {
			List<Def> listDefs=GetDefs();
			Assert.AreEqual(Color.Empty,Tasks.GetCategoryColor(0,listDefs),"No category.");
			Assert.AreEqual(Color.FromArgb(255,200,0,0).ToArgb(),Tasks.GetCategoryColor(11,listDefs).ToArgb());
			Assert.AreEqual(Color.FromArgb(255,0,0,200).ToArgb(),Tasks.GetCategoryColor(12,listDefs).ToArgb());
			Assert.AreEqual(Color.Empty,Tasks.GetCategoryColor(13,listDefs),"A category saved with No Color.");
			Assert.AreEqual(Color.Empty,Tasks.GetCategoryColor(99,listDefs),"A category whose definition is gone.");
		}

		[TestMethod]
		public void DefCat_TaskCategories_IsNumber54AndNoLongerHqOnly() {
			//Open Dental's published database documentation numbers DefCat.TaskCategories 54. The Definitions window hides categories whose
			//description says HqOnly, so the 25.2 rename is what makes the category editable by every office.
			Assert.AreEqual(54,(int)DefCat.TaskCategories);
			Assert.AreEqual("Task Categories",DefCat.TaskCategories.GetDescription());
			Assert.IsFalse(DefCat.TaskCategories.GetDescription().Contains("HqOnly"));
		}

		[TestMethod]
		public void TaskCategoryShow_IsABooleanPreference() {
			PrefNameAttribute attribute=typeof(PrefName).GetField(nameof(PrefName.TaskCategoryShow))
				.GetCustomAttributes(typeof(PrefNameAttribute),false).Cast<PrefNameAttribute>().FirstOrDefault();
			Assert.IsNotNull(attribute);
			Assert.AreEqual(PrefValueType.BOOL,attribute.ValueType);
		}

		[TestMethod]
		public void TaskHist_CopiesTheCategory() {
			Task task=new Task { TaskNum=5,Category=12,TriagePosition=3,Descript="Synthetic" };
			TaskHist taskHist=new TaskHist(task);
			Assert.AreEqual(12,taskHist.Category);
			Assert.AreEqual(3,taskHist.TriagePosition,"Kept for data compatibility, never shown.");
		}

		[TestMethod]
		public void Task_Equals_SeesACategoryChange() {
			//FormTaskEdit decides whether there is anything to save by comparing with the task as loaded.
			Task task=new Task { TaskNum=5,Category=11,Descript="Synthetic" };
			Task taskChanged=task.Copy();
			taskChanged.Category=0;
			Assert.IsFalse(task.Equals(taskChanged));
		}
	}
}
