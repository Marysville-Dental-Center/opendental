// Tests of the synthetic chart scenes: names, groups, and that every scene applies through the relay with no database (package TC-0).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDentalBusiness;
using SparksToothChart;

namespace ToothChartImageTests {
	[TestClass]
	public class SceneTests {
		[TestMethod]
		public void Scenes_AreAboutOneHundredAndTwenty_WithUniqueFileNames() {
			List<ChartScene> listScenes=ChartScenes.GetAll();
			Assert.IsTrue(listScenes.Count>=120,"The plan asks for about 120 scenes; there are "+listScenes.Count+".");
			List<string> listDuplicates=listScenes.GroupBy(x => x.Name,StringComparer.OrdinalIgnoreCase).Where(x => x.Count()>1).Select(x => x.Key).ToList();
			Assert.AreEqual(0,listDuplicates.Count,"Scene names must be unique, ignoring case (they are file names): "+string.Join(", ",listDuplicates));
			Regex regexName=new Regex(@"^[a-z0-9]+(-[a-z0-9]+)*$");
			List<string> listBadNames=listScenes.Select(x => x.Name).Where(x => !regexName.IsMatch(x)).ToList();
			Assert.AreEqual(0,listBadNames.Count,"Scene names are lower case words joined by '-': "+string.Join(", ",listBadNames));
		}

		[TestMethod]
		public void Scenes_HaveAGroupAndADescription_AndCoverTheGalleryGroups() {
			List<ChartScene> listScenes=ChartScenes.GetAll();
			foreach(ChartScene scene in listScenes) {
				Assert.IsFalse(string.IsNullOrWhiteSpace(scene.Group),scene.Name+" has no group.");
				Assert.IsTrue(scene.Description!=null && scene.Description.Length>=20,scene.Name+" needs a description of its data in words.");
				Assert.IsTrue(scene.Description.EndsWith("."),scene.Name+": a description is a sentence.");
			}
			//The groups of the owner gallery (plan section 8) that today's chart can show.
			string[] arrayGroups={ "empty","states","paint","known-difference","colours","interaction","free-text","ortho","sizes","perio" };
			CollectionAssert.AreEquivalent(arrayGroups,listScenes.Select(x => x.Group).Distinct().ToArray());
			Assert.IsTrue(listScenes.Count(x => x.Kind==ChartSceneKind.Perio)>=5);
			Assert.IsTrue(listScenes.Where(x => x.Kind==ChartSceneKind.Perio).All(x => x.Group=="perio" && x.ListSizesExtra.Count==0));
		}

		[TestMethod]
		public void Scenes_NameEveryNumberingAndPaintType() {
			List<ChartScene> listScenes=ChartScenes.GetAll();
			foreach(ToothNumberingNomenclature nomenclature in Enum.GetValues(typeof(ToothNumberingNomenclature))) {
				Assert.IsTrue(listScenes.Any(x => x.Nomenclature==nomenclature),"No scene in "+nomenclature+" numbering.");
			}
			//Each paint type has a scene, the ones today's chart draws nothing for (Text other than "W", TextBelow, retained root, space maintainer)
			//included, so the galleries have their "before".
			string[] arrayPrefixes={ "extraction","implant","rct","postbu","filling-dark","filling-light","crown-dark","crown-light","bridge-dark",
				"bridge-light","denture","sealant","veneer","text-","text-below","retained-root","space-maintainer" };
			foreach(string prefix in arrayPrefixes) {
				Assert.IsTrue(listScenes.Any(x => x.Name.StartsWith(prefix)),"No scene for "+prefix);
			}
			List<string> listSizes=listScenes.SelectMany(x => ChartScenes.GetSizes(x)).Select(x => x.FolderName).Distinct().ToList();
			CollectionAssert.AreEquivalent(new[] { "410x307","500x370","649x696","926x858","640x320","360x480" },listSizes);
		}

		[TestMethod]
		public void EveryPaintType_IsPaintedInEveryStatusColour() {
			//Plan section 8: every paint type from 1 to 18 in every status colour (TP, C, EC, EO, R, Cn). Paint logs each procedure it is given,
			//including the ones today's chart draws nothing for and the completed extractions it skips.
			List<string> listLog=StaRunner.Run(() => {
				ChartScenes.ListPaintLog=new List<string>();
				try {
					using ToothChartWrapper wrapper=new ToothChartWrapper();
					wrapper.Size=ChartScenes.SizeModule.Size;
					ToothChartRelay relay=new ToothChartRelay();
					relay.SetToothChartWrapper(wrapper);
					foreach(ChartScene scene in ChartScenes.GetAll().Where(x => x.Kind==ChartSceneKind.Chart)) {
						ChartScenes.Prepare(relay,scene,ChartColorSet.Module);
					}
					return ChartScenes.ListPaintLog;
				}
				finally {
					ChartScenes.ListPaintLog=null;
				}
			});
			List<string> listMissing=new List<string>();
			foreach(ToothPaintingType paintType in Enum.GetValues(typeof(ToothPaintingType))) {
				if(paintType==ToothPaintingType.None) {
					continue;
				}
				foreach(StatusColors statusColors in ChartScenes.ArrayStatuses) {
					if(!listLog.Contains(paintType+" "+statusColors.Status)) {
						listMissing.Add(paintType+" "+statusColors.Status);
					}
				}
			}
			Assert.AreEqual(18,Enum.GetValues(typeof(ToothPaintingType)).Length-1,"The paint types are 1 to 18.");
			Assert.AreEqual(0,listMissing.Count,"No scene paints: "+string.Join(", ",listMissing));
		}

		[TestMethod]
		public void CompletedExtractions_DrawNoX_AsInTheChartModule() {
			//ControlChart.DrawProcGraphics skips an extraction in C, EC or EO: the tooth is missing instead. The other statuses draw the X.
			StaRunner.Run(() => {
				using ToothChartWrapper wrapper=new ToothChartWrapper();
				ToothChartRelay relay=new ToothChartRelay();
				relay.SetToothChartWrapper(wrapper);
				foreach(StatusColors statusColors in ChartScenes.ArrayStatuses) {
					relay.ResetTeeth();
					ChartScenes.Paint(relay,ToothPaintingType.Extraction,"3","",statusColors);
					bool isCompleted=statusColors.Status=="C" || statusColors.Status=="EC" || statusColors.Status=="EO";
					Assert.AreEqual(isCompleted,ChartScenes.IsCompletedStatus(statusColors),statusColors.Status);
					Assert.AreEqual(!isCompleted,wrapper.TcData.ListToothGraphics["3"].DrawBigX,statusColors.Status);
				}
			});
		}

		[TestMethod]
		public void EveryScene_AppliesThroughTheRelay_WithNoDatabase() {
			StaRunner.Run(() => {
				using ToothChartWrapper wrapper=new ToothChartWrapper();
				wrapper.Size=ChartScenes.SizeModule.Size;
				ToothChartRelay relay=new ToothChartRelay();
				relay.SetToothChartWrapper(wrapper);
				foreach(ChartScene scene in ChartScenes.GetAll().Where(x => x.Kind==ChartSceneKind.Chart)) {
					ChartScenes.Prepare(relay,scene,ChartColorSet.Module);
				}
				//The Simple 2D chart cannot be put in perio mode, so the perio scenes' data is applied without it, on a new wrapper each.
				foreach(ChartScene scene in ChartScenes.GetAll().Where(x => x.Kind==ChartSceneKind.Perio)) {
					using ToothChartWrapper wrapperPerio=new ToothChartWrapper();
					ToothChartRelay relayPerio=new ToothChartRelay();
					relayPerio.SetToothChartWrapper(wrapperPerio);
					scene.Apply(relayPerio);
					if(scene.Name!="perio-empty") {
						Assert.IsTrue(wrapperPerio.TcData.ListPerioMeasure.Count>0,scene.Name+" adds no perio measures.");
					}
				}
			});
		}

		[TestMethod]
		public void Prepare_ResetsWhatTheLastSceneChanged() {
			//The recorder draws scene after scene on one wrapper per size, as the Chart module refills its one chart. So a scene must look the same
			//after any other scene. Checked on the Simple 2D chart, which needs no graphics device.
			StaRunner.Run(() => {
				string[] arrayNames={ "empty-universal","select-several","drawing-segments","restored-adult","movements-combined","primary-dentition",
					"colour-custom-chart-colours","ortho-mode" };
				foreach(ChartColorSet chartColorSet in new[] { ChartColorSet.Module,ChartColorSet.Printout }) {
					Size size=chartColorSet==ChartColorSet.Module?ChartScenes.SizeModule.Size:ChartScenes.SizePrintout.Size;
					Dictionary<string,string> dictFresh=new Dictionary<string,string>();
					foreach(string name in arrayNames) {
						dictFresh[name]=Render2D(size,chartColorSet,ChartScenes.Get(name));
					}
					using ToothChartWrapper wrapper=new ToothChartWrapper();
					wrapper.Size=size;
					ToothChartRelay relay=new ToothChartRelay();
					relay.SetToothChartWrapper(wrapper);
					foreach(string name in arrayNames.Reverse()) {
						ChartScenes.Prepare(relay,ChartScenes.Get("colour-custom-chart-colours"),chartColorSet);
						ChartScenes.Prepare(relay,ChartScenes.Get("restored-adult"),chartColorSet);
						ChartScenes.Prepare(relay,ChartScenes.Get(name),chartColorSet);
						using Bitmap bitmap=relay.GetBitmap();
						Assert.AreEqual(dictFresh[name],ChartImageCompare.DescribeBitmap(bitmap).PixelsSha256,
							name+" ("+chartColorSet+") looks different after other scenes than on a new chart.");
					}
				}
			});
		}

		[TestMethod]
		public void Scenes_DrawDifferentPictures_InSimple2D() {
			//A scene whose state the 2D chart shows must change the picture; the ones it cannot show (3D only, or drawn by nothing today) are listed.
			StaRunner.Run(() => {
				string hashEmpty=Render2D(ChartScenes.SizeModule.Size,ChartColorSet.Module,ChartScenes.Get("empty-universal"));
				foreach(string name in new[] { "empty-fdi","primary-dentition","missing-posterior","hidden-premolars","extraction-statuses","select-one",
					"drawing-segments","text-watch","colour-custom-chart-colours","extraction-completed","bridge-dark-cn","denture-light-tp-ec-r-cn" })
				{
					Assert.AreNotEqual(hashEmpty,Render2D(ChartScenes.SizeModule.Size,ChartColorSet.Module,ChartScenes.Get(name)),name);
				}
				//Today no engine draws free text, orthodontic hardware, retained roots or space maintainers, paint text other than "W" and TextBelow.
				foreach(string name in new[] { "text-label-long","ortho-brackets","retained-root","space-maintainer-tooth","text-other","text-other-eo-r",
					"text-below-statuses","retained-root-statuses","space-maintainer-statuses" })
				{
					Assert.AreEqual(hashEmpty,Render2D(ChartScenes.SizeModule.Size,ChartColorSet.Module,ChartScenes.Get(name)),name);
				}
			});
		}

		///<summary>The SHA-256 of the scene's pixels on a new Simple 2D chart.</summary>
		private static string Render2D(Size size,ChartColorSet chartColorSet,ChartScene scene) {
			using ToothChartWrapper wrapper=new ToothChartWrapper();
			wrapper.Size=size;
			ToothChartRelay relay=new ToothChartRelay();
			relay.SetToothChartWrapper(wrapper);
			ChartScenes.Prepare(relay,scene,chartColorSet);
			Assert.AreEqual(DrawingMode.Simple2D,wrapper.DrawMode);
			using Bitmap bitmap=relay.GetBitmap();
			Assert.AreEqual(size,bitmap.Size);
			return ChartImageCompare.DescribeBitmap(bitmap).PixelsSha256;
		}
	}
}
