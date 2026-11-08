// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ImagingCapture.Dicom;
using ImagingCapture.Save;
using ImagingCapture.Templates;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace ImagingCapture.Tests {
	///<summary>Mount definitions and the security log for "Add standard templates"; ThrowOn names calls that fail.</summary>
	internal sealed class FakeStandardTemplateStore:IStandardTemplateStore {
		public bool IsAuthorizedResult=true;
		public HashSet<long> HashSetCategories=new HashSet<long> { 20 };
		public readonly List<MountDef> ListMountDefs=new List<MountDef>();
		public readonly List<MountItemDef> ListMountItemDefs=new List<MountItemDef>();
		public readonly List<SecurityLog> ListLogs=new List<SecurityLog>();
		public int CountSignals;
		///<summary>"InsertMountDef", "InsertMountItemDef", "MakeLogEntry", "GetMountDefs"; separated by '|'.</summary>
		public string ThrowOn="";
		///<summary>InsertMountItemDef throws from this call on (1-based), 0 for never.</summary>
		public int ThrowOnItemInsertNumber;
		private int _countItemInserts;
		///<summary>The MountDefNum of the next insert.</summary>
		private long _mountDefNumNext=101;

		public bool IsAuthorized(EnumPermType permType) {
			return IsAuthorizedResult;
		}

		public List<MountDef> GetMountDefs() {
			Call("GetMountDefs");
			return ListMountDefs.Select(x => x.Copy()).ToList();
		}

		public bool IsCategoryValid(long defNum) {
			return HashSetCategories.Contains(defNum);
		}

		public long InsertMountDef(MountDef mountDef) {
			Call("InsertMountDef");
			MountDef mountDefCopy=mountDef.Copy();
			mountDefCopy.MountDefNum=_mountDefNumNext++;
			ListMountDefs.Add(mountDefCopy);
			return mountDefCopy.MountDefNum;
		}

		public long InsertMountItemDef(MountItemDef mountItemDef) {
			Call("InsertMountItemDef");
			_countItemInserts++;
			if(ThrowOnItemInsertNumber>0 && _countItemInserts>=ThrowOnItemInsertNumber) {
				throw new Exception("The database is not reachable.");
			}
			MountItemDef mountItemDefCopy=mountItemDef.Copy();
			mountItemDefCopy.MountItemDefNum=ListMountItemDefs.Count+1;
			ListMountItemDefs.Add(mountItemDefCopy);
			return mountItemDefCopy.MountItemDefNum;
		}

		public void DeleteMountDef(long mountDefNum) {
			ListMountDefs.RemoveAll(x => x.MountDefNum==mountDefNum);
			ListMountItemDefs.RemoveAll(x => x.MountDefNum==mountDefNum);
		}

		public void MakeLogEntry(EnumPermType permType,long patNum,string logText,long fKey) {
			Call("MakeLogEntry");
			ListLogs.Add(new SecurityLog { PermType=permType,PatNum=patNum,LogText=logText,FKey=fKey });
		}

		public void SignalMountDefsChanged() {
			CountSignals++;
		}

		private void Call(string name) {
			if(ThrowOn.Split('|').Contains(name)) {
				throw new Exception(name+" failed");
			}
		}
	}

	///<summary>The standard mount templates (design 9.4 option a, 10.2, 9.1): the slots of Open Dental's generator, grouped by sensor size, the
	///scale only for one sensor size 1:1, and "Add standard templates" (planner and installer): idempotent by Description, Setup, logged.</summary>
	[TestClass]
	public class StandardMountTemplatesTests {
		private static readonly Dictionary<StandardTemplateKind,string> _dictAnatomyLabels=new Dictionary<StandardTemplateKind,string> {
			{ StandardTemplateKind.FmxHorizontalBitewings,"FMX horiz" },
			{ StandardTemplateKind.FmxVerticalBitewings,"FMX vert" },
			{ StandardTemplateKind.FourBitewingsHorizontal,"4BW horiz" },
			{ StandardTemplateKind.FourBitewingsVertical,"4BW vert" },
		};

		#region Templates
		[TestMethod]
		public void Slots_AreExactlyTheOnesIntraOralAnatomyTestsPins() {
			List<string> listFromTemplates=StandardMountTemplates.ListTemplates
				.SelectMany(x => x.ListSlots.Select(y => _dictAnatomyLabels[x.Kind]+"|"+y.ItemOrder+"|"+y.ToothNumbers+"|"+y.RotateOnAcquire)).ToList();
			List<string> listPinned=IntraOralAnatomyTests._arrayTemplateSlots.Select(x => x[0]+"|"+x[1]+"|"+x[2]+"|"+x[3]).ToList();
			CollectionAssert.AreEqual(listPinned,listFromTemplates,"ItemOrder, ToothNumbers and RotateOnAcquire of every slot, in order.");
		}

		[TestMethod]
		public void Texts_TheWindowShows_NameNoOtherProgram() {
			List<string> listTexts=new List<string> { StandardTemplateTexts.Intro };
			listTexts.AddRange(StandardMountTemplates.ListTemplates.Select(x => StandardTemplateTexts.GetTemplateText(x)));
			foreach(string text in listTexts) {
				StringAssert.DoesNotMatch(text,new System.Text.RegularExpressions.Regex("Open Dental",System.Text.RegularExpressions.RegexOptions.IgnoreCase),
					"The Add standard templates window names the program's own mount generator, not another program.");
			}
		}

		[TestMethod]
		public void Layout_IsOpenDentalsGenerator_WithItsDefaultSlotSize() {
			int w=1700;
			int h=1300;
			Assert.AreEqual(w,StandardMountTemplates.SlotWidth);
			Assert.AreEqual(h,StandardMountTemplates.SlotHeight);
			StandardTemplate fmx=StandardMountTemplates.Get(StandardTemplateKind.FmxHorizontalBitewings);
			Assert.AreEqual(4*w+3*h,fmx.Width);
			Assert.AreEqual(3*h,fmx.Height);
			AssertSlot(fmx,1,0,h,w,h);
			AssertSlot(fmx,3,3*w+3*h,h,w,h);
			AssertSlot(fmx,7,3*w+3*h,2*h,w,h);
			AssertSlot(fmx,13,2*w,0,h,w);
			AssertSlot(fmx,18,2*w+2*h,3*h-w,h,w);
			Assert.AreEqual(2*w+h,fmx.Text.Xpos);
			Assert.AreEqual(w,fmx.Text.Ypos);
			Assert.AreEqual(w,fmx.Text.Width);
			Assert.AreEqual(3*h-2*w,fmx.Text.Height);
			StandardTemplate fmxVertical=StandardMountTemplates.Get(StandardTemplateKind.FmxVerticalBitewings);
			Assert.AreEqual(4*w+3*h,fmxVertical.Width);
			Assert.AreEqual(2*h+w,fmxVertical.Height);
			AssertSlot(fmxVertical,1,(w-h)/2,h,h,w);
			AssertSlot(fmxVertical,4,2*w+3*h+(w-h)/2,h,h,w);
			AssertSlot(fmxVertical,9,0,h+w,w,h);
			AssertSlot(fmxVertical,16,2*w,2*h,h,w);
			Assert.AreEqual(2*h-w,fmxVertical.Text.Height);
			StandardTemplate bw=StandardMountTemplates.Get(StandardTemplateKind.FourBitewingsHorizontal);
			Assert.AreEqual(4*w,bw.Width);
			Assert.AreEqual(h,bw.Height);
			AssertSlot(bw,3,3*w,0,w,h);
			Assert.IsNull(bw.Text);
			StandardTemplate bwVertical=StandardMountTemplates.Get(StandardTemplateKind.FourBitewingsVertical);
			Assert.AreEqual(5*h,bwVertical.Width);
			Assert.AreEqual(w,bwVertical.Height);
			AssertSlot(bwVertical,3,4*h,0,h,w);
			Assert.AreEqual("Patient: [NameLF]\r\nPatNum: [PatNum]\r\nDate: [MountDate]\r\nProvider: [MountProv]\r\n",fmx.Text.TextShowing);
			Assert.AreEqual(80f,fmx.Text.FontSize);
		}

		private static void AssertSlot(StandardTemplate template,int itemOrder,int x,int y,int width,int height) {
			StandardTemplateSlot slot=template.ListSlots.Single(s => s.ItemOrder==itemOrder);
			Assert.AreEqual(new Rectangle(x,y,width,height),new Rectangle(slot.Xpos,slot.Ypos,slot.Width,slot.Height),template.Description+" slot "+itemOrder);
		}

		[TestMethod]
		public void Slots_InsideTheMount_NoOverlap_ItemOrderOneToN_UniqueDescriptions() {
			foreach(StandardTemplate template in StandardMountTemplates.ListTemplates) {
				Rectangle rectangleMount=new Rectangle(0,0,template.Width,template.Height);
				List<Rectangle> listRectangles=template.ListSlots.Select(x => new Rectangle(x.Xpos,x.Ypos,x.Width,x.Height)).ToList();
				if(template.Text!=null) {
					listRectangles.Add(new Rectangle(template.Text.Xpos,template.Text.Ypos,template.Text.Width,template.Text.Height));
				}
				for(int i=0;i<listRectangles.Count;i++) {
					Assert.IsTrue(rectangleMount.Contains(listRectangles[i]),template.Description+" item "+i);
					for(int j=i+1;j<listRectangles.Count;j++) {
						Assert.IsFalse(listRectangles[i].IntersectsWith(listRectangles[j]),template.Description+" items "+i+" and "+j+" overlap");
					}
				}
				CollectionAssert.AreEqual(Enumerable.Range(1,template.ListSlots.Count).ToList(),template.ListSlots.Select(x => x.ItemOrder).ToList());
				foreach(StandardTemplateSlot slot in template.ListSlots) {
					string error;
					Assert.IsNotNull(IntraOralAnatomy.FromTarget(new IntraOralTarget { HasMountSlot=true,ToothNumbers=slot.ToothNumbers,
						DegreesRotated=slot.RotateOnAcquire },out error),template.Description+" slot "+slot.ItemOrder+": "+error);
				}
			}
			Assert.AreEqual(4,StandardMountTemplates.ListTemplates.Select(x => x.Description.ToLowerInvariant()).Distinct().Count());
		}

		[TestMethod]
		public void ItemOrder_GroupedBySensorSize_TheSensorIsChangedAtMostTwice() {
			foreach(StandardTemplate template in StandardMountTemplates.ListTemplates) {
				Assert.IsTrue(template.CountSensorSwaps<=2,template.Description);
				//Each size in one run.
				List<SensorSize> listSizes=template.GetSensorSequence();
				foreach(SensorSize size in listSizes.Distinct()) {
					int idxFirst=listSizes.IndexOf(size);
					int idxLast=listSizes.LastIndexOf(size);
					Assert.IsTrue(listSizes.Skip(idxFirst).Take(idxLast-idxFirst+1).All(x => x==size),template.Description+" "+size);
				}
				foreach(StandardTemplateSlot slot in template.ListSlots) {
					List<ToothInfo> listTeeth;
					string error;
					Assert.IsTrue(ToothNumbers.TryParseList(slot.ToothNumbers,out listTeeth,out error));
					Assert.AreEqual(listTeeth.All(x => x.IsAnterior) ? SensorSize.Size1 : SensorSize.Size2,slot.SensorSize,"Anterior periapicals take size 1.");
				}
			}
			StandardTemplate fmx=StandardMountTemplates.Get(StandardTemplateKind.FmxHorizontalBitewings);
			Assert.AreEqual(1,fmx.CountSensorSwaps);
			Assert.AreEqual("size 2 for slots 1-12, then size 1 for slots 13-18",fmx.GetSensorOrderText());
			Assert.AreEqual(1,StandardMountTemplates.Get(StandardTemplateKind.FmxVerticalBitewings).CountSensorSwaps);
			Assert.AreEqual(0,StandardMountTemplates.Get(StandardTemplateKind.FourBitewingsHorizontal).CountSensorSwaps);
			Assert.AreEqual("size 2 for slots 1-4",StandardMountTemplates.Get(StandardTemplateKind.FourBitewingsVertical).GetSensorOrderText());
		}

		[TestMethod]
		public void ScaleValue_NoneInAnyTemplate_TheSlotsAreTheGeneratorsDefaultNotAMeasuredSensorImage() {
			Assert.AreEqual("",StandardMountTemplates.Get(StandardTemplateKind.FmxHorizontalBitewings).ScaleValue,"Size 1 and size 2 images in slots of one size.");
			Assert.AreEqual("",StandardMountTemplates.Get(StandardTemplateKind.FmxVerticalBitewings).ScaleValue);
			StringAssert.Contains(StandardMountTemplates.Get(StandardTemplateKind.FmxVerticalBitewings).ScaleNote,"size 1 sensor");
			//Design 9.1: "50 1 mm" only for slots that match one sensor size 1:1; 1700 x 1300 is no sensor's measured image, and the vertical 4BW's
			//portrait slots would show a landscape image at 0.765.
			Assert.AreEqual("",StandardMountTemplates.Get(StandardTemplateKind.FourBitewingsHorizontal).ScaleValue);
			Assert.AreEqual("",StandardMountTemplates.Get(StandardTemplateKind.FourBitewingsVertical).ScaleValue);
			StringAssert.Contains(StandardMountTemplates.Get(StandardTemplateKind.FourBitewingsVertical).ScaleNote,"not a measured sensor image");
			Assert.IsTrue(StandardMountTemplates.ListTemplates.All(x => x.ScaleNote.Contains("runbook")),"Each note says who sets the scale.");
			Assert.AreEqual(50f,MountDefs.GetScale("50 1 mm"),"Read the way Open Dental reads a mount scale.");
			Assert.AreEqual(1,MountDefs.GetDecimals("50 1 mm"));
			Assert.AreEqual("mm",MountDefs.GetScaleUnits("50 1 mm"));
		}
		#endregion Templates

		#region Plan
		[TestMethod]
		public void Plan_IdempotentByDescription_TrimmedIgnoringCase() {
			List<MountDef> listMountDefs=new List<MountDef> {
				new MountDef { MountDefNum=3,Description=" fmx HORIZONTAL bw " },
				new MountDef { MountDefNum=4,Description="FMX" },//Open Dental's generator's name: not a standard template's
			};
			StandardTemplatePlan plan=StandardTemplatePlanner.Plan(listMountDefs,20);
			Assert.AreEqual(4,plan.ListItems.Count);
			Assert.AreEqual(3,plan.ListLeftAlone.Single().MountDefExisting.MountDefNum);
			CollectionAssert.AreEqual(new[] { "FMX vertical BW","4BW horizontal","4BW vertical" },plan.ListToAdd.Select(x => x.Template.Description).ToList());
			string text=StandardTemplateTexts.GetPlanText(plan);
			StringAssert.Contains(text,"Already exists (mount 3, \"fmx HORIZONTAL bw\"): left alone.");
			StringAssert.Contains(text,"4BW vertical: 4 slots; sensor: size 2 for slots 1-4 (no change).");
			StringAssert.Contains(text,"FMX vertical BW: 18 slots; sensor: size 2 for slots 1-12, then size 1 for slots 13-18 (1 change).");
			Assert.AreEqual(4,StandardTemplatePlanner.Plan(null,20).ListToAdd.Count);
		}

		[TestMethod]
		public void BuildMountDef_AsTheGeneratorSetsItUp_WithThePickedCategory() {
			StandardTemplate template=StandardMountTemplates.Get(StandardTemplateKind.FourBitewingsVertical);
			MountDef mountDef=StandardTemplatePlanner.BuildMountDef(template,20,7);
			Assert.AreEqual("4BW vertical",mountDef.Description);
			Assert.AreEqual(7,mountDef.ItemOrder);
			Assert.AreEqual(20,mountDef.DefaultCat);
			Assert.AreEqual(template.Width,mountDef.Width);
			Assert.AreEqual(template.Height,mountDef.Height);
			Assert.AreEqual(Color.Black.ToArgb(),mountDef.ColorBack.ToArgb());
			Assert.AreEqual(Color.White.ToArgb(),mountDef.ColorFore.ToArgb());
			Assert.AreEqual(Color.Black.ToArgb(),mountDef.ColorTextBack.ToArgb());
			Assert.AreEqual("",mountDef.ScaleValue,"Design 9.1: the clinic sets it once the slots are sized to the sensor.");
			Assert.IsFalse(mountDef.FlipOnAcquire);
			Assert.IsFalse(mountDef.AdjModeAfterSeries,"The generator leaves it as a new mount definition has it.");
			Assert.AreEqual("",StandardTemplatePlanner.BuildMountDef(StandardMountTemplates.Get(StandardTemplateKind.FmxHorizontalBitewings),20,0).ScaleValue);
		}

		[TestMethod]
		public void BuildMountItemDefs_SlotsThenTheText_TheSessionSeesTheSlotsInOrder() {
			StandardTemplate template=StandardMountTemplates.Get(StandardTemplateKind.FmxHorizontalBitewings);
			List<MountItemDef> listMountItemDefs=StandardTemplatePlanner.BuildMountItemDefs(template,55);
			Assert.AreEqual(19,listMountItemDefs.Count);
			Assert.IsTrue(listMountItemDefs.All(x => x.MountDefNum==55));
			MountItemDef mountItemDefText=listMountItemDefs.Last();
			Assert.AreEqual(0,mountItemDefText.ItemOrder);
			Assert.AreEqual(StandardTemplateText.TextShowingDefault,mountItemDefText.TextShowing);
			Assert.AreEqual(80f,mountItemDefText.FontSize);
			//A mount made from the definition (Mounts.CreateMountFromDef copies these fields): the session's slots are the 18, in ItemOrder.
			List<MountItem> listMountItems=listMountItemDefs.Select((x,i) => new MountItem { MountItemNum=1000+i,ItemOrder=x.ItemOrder,
				ToothNumbers=x.ToothNumbers,TextShowing=x.TextShowing,Width=x.Width,Height=x.Height }).OrderBy(x => x.ItemOrder).ToList();
			List<MountItem> listSlots=listMountItems.Where(x => MountPlacement.IsSlot(x)).ToList();
			Assert.AreEqual(18,listSlots.Count);
			CollectionAssert.AreEqual(template.ListSlots.Select(x => x.ToothNumbers).ToList(),listSlots.Select(x => x.ToothNumbers).ToList());
			Assert.AreEqual(4,StandardTemplatePlanner.BuildMountItemDefs(StandardMountTemplates.Get(StandardTemplateKind.FourBitewingsHorizontal),1).Count);
		}
		#endregion Plan

		#region Install
		[TestMethod]
		public void Install_AddsEachTemplateOnce_LogsEach_TellsTheOtherComputers() {
			FakeStandardTemplateStore store=new FakeStandardTemplateStore();
			store.ListMountDefs.Add(new MountDef { MountDefNum=5,Description="Photos",ItemOrder=0 });
			StandardTemplateInstaller installer=new StandardTemplateInstaller(store);
			StandardTemplateResult result=installer.Install(20,"X-rays");
			Assert.IsTrue(result.IsDone,result.Error);
			Assert.AreEqual(4,result.ListAdded.Count);
			Assert.AreEqual(5,store.ListMountDefs.Count);
			CollectionAssert.AreEqual(new[] { 1,2,3,4 },result.ListAdded.Select(x => x.ItemOrder).ToList(),"After the existing mount definitions.");
			Assert.IsTrue(result.ListAdded.All(x => x.DefaultCat==20));
			Assert.AreEqual(19+19+4+4,store.ListMountItemDefs.Count);
			Assert.AreEqual(4,store.ListLogs.Count);
			Assert.IsTrue(store.ListLogs.All(x => x.PermType==EnumPermType.Setup && x.PatNum==0));
			CollectionAssert.AreEqual(result.ListAdded.Select(x => x.MountDefNum).ToList(),store.ListLogs.Select(x => x.FKey).ToList());
			StringAssert.StartsWith(store.ListLogs[0].LogText,StandardTemplatePlanner.LogPrefix);
			StringAssert.Contains(store.ListLogs[0].LogText,"Description=FMX horizontal BW; MountDefNum=101; DefaultCat=20 (X-rays); Size=10700x3900; Slots=18");
			StringAssert.Contains(store.ListLogs[0].LogText,"ScaleValue=(none)");
			StringAssert.Contains(store.ListLogs[3].LogText,"ScaleValue=(none)");
			StringAssert.Contains(store.ListLogs[0].LogText,"SlotList=1:2/3/30/31:0|2:4/5/28/29:0|3:14/15/18/19:180");
			Assert.AreEqual(1,store.CountSignals);
			StringAssert.Contains(StandardTemplateTexts.GetResultText(result),"Added: FMX horizontal BW (mount 101)");
			//Again: nothing is added, nothing logged.
			result=installer.Install(20,"X-rays");
			Assert.IsTrue(result.IsDone);
			Assert.AreEqual(0,result.ListAdded.Count);
			Assert.AreEqual(4,result.Plan.ListLeftAlone.Count);
			Assert.AreEqual(5,store.ListMountDefs.Count);
			Assert.AreEqual(4,store.ListLogs.Count);
			Assert.AreEqual(1,store.CountSignals);
			StringAssert.Contains(StandardTemplateTexts.GetResultText(result),"Nothing was added.");
		}

		[TestMethod]
		public void Install_NeedsSetupAndACategory() {
			FakeStandardTemplateStore store=new FakeStandardTemplateStore();
			store.IsAuthorizedResult=false;
			StandardTemplateResult result=new StandardTemplateInstaller(store).Install(20,"X-rays");
			Assert.IsFalse(result.IsDone);
			StringAssert.Contains(result.Error,"Not authorized for Setup");
			store.IsAuthorizedResult=true;
			Assert.IsFalse(new StandardTemplateInstaller(store).Install(0,"").IsDone);
			StringAssert.Contains(new StandardTemplateInstaller(store).Install(21,"Gone").Error,"Choose the X-ray category");
			Assert.AreEqual(0,store.ListMountDefs.Count);
			Assert.AreEqual(0,store.ListLogs.Count);
			Assert.AreEqual(0,store.CountSignals);
		}

		[TestMethod]
		public void Install_AnItemFails_TheHalfTemplateIsDeleted_NothingAfterIt() {
			FakeStandardTemplateStore store=new FakeStandardTemplateStore();
			store.ThrowOnItemInsertNumber=25;//inside the second template
			StandardTemplateResult result=new StandardTemplateInstaller(store).Install(20,"X-rays");
			Assert.IsFalse(result.IsDone);
			StringAssert.Contains(result.Error,"\"FMX vertical BW\" could not be added: The database is not reachable.");
			Assert.AreEqual("FMX horizontal BW",result.ListAdded.Single().Description);
			Assert.AreEqual("FMX horizontal BW",store.ListMountDefs.Single().Description,"No half template stays.");
			Assert.AreEqual(19,store.ListMountItemDefs.Count);
			Assert.AreEqual(1,store.ListLogs.Count);
			Assert.AreEqual(1,store.CountSignals);
		}

		[TestMethod]
		public void Install_LogFails_TheTemplateStays_Warned() {
			FakeStandardTemplateStore store=new FakeStandardTemplateStore();
			store.ThrowOn="MakeLogEntry";
			StandardTemplateResult result=new StandardTemplateInstaller(store).Install(20,"X-rays");
			Assert.IsTrue(result.IsDone);
			Assert.AreEqual(4,result.ListAdded.Count);
			Assert.AreEqual(4,result.ListWarnings.Count);
			StringAssert.Contains(result.ListWarnings[0],"security-log entry");
			store.ThrowOn="GetMountDefs";
			result=new StandardTemplateInstaller(store).Install(20,"X-rays");
			Assert.IsFalse(result.IsDone);
			StringAssert.Contains(result.Error,"GetMountDefs failed");
		}
		#endregion Install
	}
}
