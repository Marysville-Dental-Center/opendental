// Scene-builder tests of the WPF chart (package TC-1, docs/specs/wpf-tooth-chart-plan.md sections 5.1 to 5.3 and 7): opaque 3D colours, DirectX's
// order of the 2D items, the material rules, and that the builder makes no WPF object and needs no thread.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDentalBusiness;
using SparksToothChart;
using SparksToothChart.Wpf3D;

namespace ToothChartImageTests {
	[TestClass]
	public class WpfSceneTests {
		///<summary>A chart's data made without a wrapper or a control, as ToothChartWrapper.ResetTeeth fills it the first time, at the given size.</summary>
		public static ToothChartData CreateData(Size size) {
			ToothChartData tcData=new ToothChartData();
			for(int i=1;i<=32;i++) {
				ToothGraphic toothGraphic=new ToothGraphic(i.ToString());
				toothGraphic.Visible=true;
				tcData.ListToothGraphics.Add(toothGraphic);
				if(Tooth.PermToPri(i.ToString())!="") {
					toothGraphic=new ToothGraphic(Tooth.PermToPri(i.ToString()));
					toothGraphic.Visible=false;
					tcData.ListToothGraphics.Add(toothGraphic);
				}
			}
			tcData.ListToothGraphics.Add(new ToothGraphic("implant"));
			tcData.SizeControl=size;
			return tcData;
		}

		///<summary>Prepares each chart scene through the relay on a 2D wrapper (the data is the same in every engine) and returns its scene.</summary>
		private static List<KeyValuePair<string,ToothScene>> BuildEveryChartScene() {
			return StaRunner.Run(() => {
				List<KeyValuePair<string,ToothScene>> listScenes=new List<KeyValuePair<string,ToothScene>>();
				foreach(ChartSize chartSize in WpfCharts.GetChartSizes()) {
					using(ToothChartWrapper wrapper=new ToothChartWrapper()) {
						wrapper.Size=chartSize.Size;
						ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
						foreach(ChartScene scene in WpfCharts.GetChartScenes().Where(x => ChartScenes.GetSizes(x).Contains(chartSize))) {
							ChartScenes.Prepare(relay,scene,chartSize.ColorSet);
							listScenes.Add(new KeyValuePair<string,ToothScene>(chartSize.FolderName+"/"+scene.Name,ToothSceneBuilder.Build(wrapper.TcData)));
						}
					}
				}
				return listScenes;
			});
		}

		[TestMethod]
		public void Builder_NeedsNoThread_AndMakesNoWpfObject() {
			StaRunner.RunMta(() => CheckBuilderOnMta());
		}

		///<summary>On a new MTA thread: a WPF object made here would give the thread a Dispatcher (and a WPF UI object would throw on MTA).</summary>
		private static void CheckBuilderOnMta() {
			Assert.AreEqual(ApartmentState.MTA,Thread.CurrentThread.GetApartmentState());
			Assert.IsNull(System.Windows.Threading.Dispatcher.FromThread(Thread.CurrentThread),"No Dispatcher before.");
			ToothChartData tcData=CreateData(ChartScenes.SizeModule.Size);
			tcData.ListToothGraphics["8"].DrawBigX=true;
			tcData.ListToothGraphics["8"].colorX=Color.Red;
			tcData.SetSelected("8",true);
			ToothScene toothScene=ToothSceneBuilder.Build(tcData);
			Assert.IsTrue(toothScene.Items3D.Count>500,"Both views of 32 teeth: "+toothScene.Items3D.Count);
			Assert.IsTrue(toothScene.Items2D.Any(x => x.Source=="big X of 8"));
			Assert.IsNull(System.Windows.Threading.Dispatcher.FromThread(Thread.CurrentThread),"The builder made no WPF object on the caller's thread.");
			//The scene's types hold no WPF type at all, so a scene can cross threads freely.
			Type[] arrayTypes={ typeof(ToothScene),typeof(ToothSceneItem3D),typeof(ToothSceneItem2D),typeof(ChartFont),typeof(ChartMatrix) };
			string[] arrayWpfAssemblies={ "PresentationCore","PresentationFramework","WindowsBase","System.Xaml" };
			foreach(Type type in arrayTypes) {
				foreach(FieldInfo fieldInfo in type.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)) {
					List<Type> listTypes=new List<Type> { fieldInfo.FieldType };
					listTypes.AddRange(fieldInfo.FieldType.GetGenericArguments());
					foreach(Type typeField in listTypes) {
						Assert.IsFalse(arrayWpfAssemblies.Contains(typeField.Assembly.GetName().Name),type.Name+"."+fieldInfo.Name+" is a WPF type.");
					}
				}
			}
		}

		[TestMethod]
		public void EveryChartScene_HasOnlyOpaque3DColoursAndPolygons() {
			int countItems=0;
			foreach(KeyValuePair<string,ToothScene> pair in BuildEveryChartScene()) {
				foreach(ToothSceneItem3D item in pair.Value.Items3D) {
					Assert.AreEqual(255,item.Color.A,pair.Key+": "+item);
					countItems++;
				}
				foreach(ToothSceneItem2D item in pair.Value.Items2D.Where(x => x.Kind==ChartItem2DKind.Polygons)) {
					Assert.AreEqual(255,item.Color.A,pair.Key+": "+item);
				}
				Assert.AreEqual(255,pair.Value.ColorBackground.A);
			}
			Assert.IsTrue(countItems>100000,"3D items checked: "+countItems);
		}

		[TestMethod]
		public void UneruptedTeeth_AreHalfRgb_AndOpaque() {
			ToothChartData tcData=CreateData(ChartScenes.SizeModule.Size);
			tcData.ListToothGraphics["1"].ShiftO=-12;//as MoveTooth with shiftO -12 sets it
			ToothScene toothScene=ToothSceneBuilder.Build(tcData);
			List<ToothSceneItem3D> listItems=toothScene.Items3D.Where(x => x.ToothId=="1").ToList();
			Assert.IsTrue(listItems.Count>0);
			foreach(ToothSceneItem3D item in listItems) {
				Color colorPaint=tcData.ListToothGraphics["1"].Groups[item.GroupIndex].PaintColor;
				Assert.AreEqual(Color.FromArgb(255,colorPaint.R/2,colorPaint.G/2,colorPaint.B/2).ToArgb(),item.Color.ToArgb(),item.ToString());
			}
			ToothSceneItem3D itemEnamel=toothScene.Items3D.First(x => x.ToothId=="2" && x.GroupType==ToothGroupType.Enamel);
			Assert.AreEqual(Color.FromArgb(255,250,250,240).ToArgb(),itemEnamel.Color.ToArgb(),"An erupted tooth keeps its colour.");
		}

		[TestMethod]
		public void Specular_FollowsDirectXGetGroupMaterial() {
			ToothChartData tcData=CreateData(ChartScenes.SizeModule.Size);
			tcData.ListToothGraphics["3"].SetSurfaceColors("O",Color.FromArgb(255,0,0,200));
			tcData.ListToothGraphics["5"].IsImplant=true;
			tcData.ListToothGraphics["5"].colorImplant=Color.Gainsboro;
			ToothScene toothScene=ToothSceneBuilder.Build(tcData);
			Assert.IsTrue(toothScene.Items3D.Where(x => x.GroupType==ToothGroupType.Cementum).All(x => x.Specular==ToothSceneBuilder.SpecularCementum));
			Assert.IsTrue(toothScene.Items3D.Where(x => x.ToothId=="2" && x.GroupType==ToothGroupType.Enamel).All(x => x.Specular==ToothSceneBuilder.SpecularEnamel),
				"The enamel colour 250,250,240 is near white.");
			Assert.IsTrue(toothScene.Items3D.Where(x => x.ToothId=="3" && x.GroupType==ToothGroupType.O).All(x => x.Specular==ToothSceneBuilder.SpecularNormal));
			List<ToothSceneItem3D> listImplant=toothScene.Items3D.Where(x => x.Source=="implant of 5").ToList();
			Assert.IsTrue(listImplant.Count>0);
			Assert.IsTrue(listImplant.All(x => x.ToothId=="implant" && x.Specular==ToothSceneBuilder.SpecularNormal),"An implant is always 1.0, as DirectX's.");
			Assert.IsFalse(toothScene.Items3D.Any(x => x.GroupType==ToothGroupType.Buildup || x.GroupType==ToothGroupType.None),
				"The build-up is a 2D silhouette and unnamed groups are never drawn.");
		}

		[TestMethod]
		public void MaxillaryImplant_IsTurnedUpsideDown() {
			ToothChartData tcData=CreateData(ChartScenes.SizeModule.Size);
			foreach(string toothId in new[] { "3","30" }) {
				tcData.ListToothGraphics[toothId].Visible=false;
				tcData.ListToothGraphics[toothId].IsImplant=true;
				tcData.ListToothGraphics[toothId].colorImplant=Color.Gainsboro;
			}
			ToothScene toothScene=ToothSceneBuilder.Build(tcData);
			ChartMatrix matrixFacial3=ChartMatrix.Translation(tcData.GetTransX("3"),tcData.GetTransYfacial("3"),0);
			ChartMatrix matrixFacial30=ChartMatrix.Translation(tcData.GetTransX("30"),tcData.GetTransYfacial("30"),0);
			ToothSceneItem3D itemImplant3=toothScene.Items3D.First(x => x.Source=="implant of 3");
			ToothSceneItem3D itemImplant30=toothScene.Items3D.First(x => x.Source=="implant of 30");
			Assert.IsTrue((ChartMatrix.RotationZ((float)Math.PI)*matrixFacial3).IsEqualTo(itemImplant3.Transform),itemImplant3.Transform.ToString());
			Assert.IsTrue(matrixFacial30.IsEqualTo(itemImplant30.Transform),itemImplant30.Transform.ToString());
			Assert.IsFalse(itemImplant3.IsReversed,"The flip is a rotation, not a mirror.");
			Assert.IsFalse(toothScene.Items3D.Any(x => x.ToothId=="3"),"Tooth 3 is missing: only its implant.");
		}

		[TestMethod]
		public void Items2D_ComeInDirectXOrder() {
			List<ToothSceneItem2D> listItems=StaRunner.Run(() => {
				using(ToothChartWrapper wrapper=new ToothChartWrapper()) {
					wrapper.Size=ChartScenes.SizeModule.Size;
					ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
					relay.ResetTeeth();
					relay.SetBigX("8",Color.Red);
					relay.SetRCT("8",Color.Blue);
					relay.SetBU("8",Color.Green);
					relay.SetSealant("8",Color.Purple);
					relay.SetBigX("3",Color.Red);
					relay.SetSealant("19",Color.Purple);
					relay.SetRCT("24",Color.Blue);
					relay.SetText("12",Color.Red,"W");
					relay.SetText("30",Color.Red,"W");
					relay.SetSelected("8",true);
					relay.SetSelected("25",true);
					ToothInitial toothInitial=new ToothInitial();
					toothInitial.InitialType=ToothInitialType.Drawing;
					toothInitial.ColorDraw=Color.Black;
					toothInitial.DrawingSegment="100,100;120,110;140,100";
					relay.AddDrawingSegment(toothInitial);
					return ToothSceneBuilder.Build(wrapper.TcData).Items2D.ToList();
				}
			});
			List<string> listSources=listItems.Select(x => x.Source).ToList();
			//Per tooth in the chart's order (ListToothGraphics: 1, 2, 3, ... with the primary teeth after their permanent ones): the facial view's
			//X, canal lines and build-up, then the occlusal view's "S". Tooth 8 comes before 19 and 24.
			int indexX8=listSources.IndexOf("big X of 8");
			Assert.IsTrue(listSources.IndexOf("big X of 3")<indexX8);
			Assert.IsTrue(indexX8<listSources.IndexOf("root canal of 8"));
			Assert.IsTrue(listSources.LastIndexOf("root canal of 8")<listSources.IndexOf("build-up of 8"));
			Assert.IsTrue(listSources.IndexOf("build-up of 8")<listSources.IndexOf("sealant of 8"));
			Assert.IsTrue(listSources.IndexOf("sealant of 8")<listSources.IndexOf("sealant of 19"));
			Assert.IsTrue(listSources.IndexOf("sealant of 19")<listSources.IndexOf("root canal of 24"));
			//After every tooth: the watches (a white outline, then the colour), the drawings, the centre line, then the numbers.
			int indexLastTooth=listSources.LastIndexOf("root canal of 24");
			int indexFirstWatch=listSources.FindIndex(x => x.StartsWith("watch of "));
			int indexDrawing=listSources.IndexOf("drawing 0");
			int indexCentre=listSources.IndexOf("centre line");
			int indexFirstNumber=listSources.FindIndex(x => x.StartsWith("number ") || x.StartsWith("highlight of "));
			Assert.IsTrue(indexLastTooth<indexFirstWatch && indexFirstWatch<indexDrawing && indexDrawing<indexCentre && indexCentre<indexFirstNumber,
				string.Join(", ",listSources));
			foreach(string toothId in new[] { "12","30" }) {
				int indexOutline=listSources.IndexOf("watch of "+toothId+" (outline)");
				Assert.IsTrue(indexOutline>=0 && listSources[indexOutline+1]=="watch of "+toothId,"The white W, then the coloured one.");
				Assert.AreEqual(Color.White.ToArgb(),listItems[indexOutline].Color.ToArgb());
				Assert.AreEqual("Times New Roman",listItems[indexOutline].Font.FaceName);
			}
			//The numbers in tooth order, 1-32 (A-T are not shown here), each highlight right before its number.
			List<string> listNumbers=listSources.Skip(indexFirstNumber).ToList();
			Assert.AreEqual(32+2,listNumbers.Count,string.Join(", ",listNumbers));
			CollectionAssert.AreEqual(Enumerable.Range(1,32).Select(x => "number "+x).ToList(),listNumbers.Where(x => x.StartsWith("number ")).ToList());
			Assert.AreEqual("number 8",listNumbers[listNumbers.IndexOf("highlight of 8")+1]);
			Assert.AreEqual("number 25",listNumbers[listNumbers.IndexOf("highlight of 25")+1]);
			//Text keeps its colour's alpha; polygons are opaque.
			Assert.IsTrue(listItems.Where(x => x.Kind==ChartItem2DKind.Text).All(x => x.Font!=null && x.Text!=null));
		}

		[TestMethod]
		public void NumberBoxes_UseDirectXMeasureRule() {
			ToothChartData tcData=CreateData(ChartScenes.SizeModule.Size);
			tcData.SetSelected("8",true);
			ToothScene toothScene=ToothSceneBuilder.Build(tcData);
			ChartFont chartFont=ToothSceneBuilder.GetFontNumbers(tcData.PixelScaleRatio);
			Assert.AreEqual("Arial",chartFont.FaceName);
			Assert.AreEqual(14,chartFont.Height,"14 pixels times the square root of the ratio, which is 1 at 410x307.");
			Assert.AreEqual(5,chartFont.Width);
			Size sizeText=ChartFontMeasure.MeasureText(chartFont,"8");
			Assert.AreEqual(14,sizeText.Height,"GDI's cell height.");
			//The highlight box: GDI's width times 1.25 and height times 0.8, in mm, at the number's place (ToothChartData.GetNumberRecMm).
			SizeF sizeLabel=new SizeF(sizeText.Width*1.25f/tcData.ScaleMmToPix,sizeText.Height*0.8f/tcData.ScaleMmToPix);
			RectangleF rectMm=tcData.GetNumberRecMm("8",sizeLabel);
			ToothSceneItem2D itemHighlight=toothScene.Items2D.First(x => x.Source=="highlight of 8");
			PointF pointCorner=toothScene.ProjectToImage(ChartMatrix.Identity,rectMm.Left,rectMm.Top,0);
			Assert.IsTrue(itemHighlight.Polygons[0].Any(x => Math.Abs(x.X-pointCorner.X)<0.001 && Math.Abs(x.Y-pointCorner.Y)<0.001),
				"The box's corner is the projected corner of DirectX's box.");
			ToothSceneItem2D itemNumber=toothScene.Items2D.First(x => x.Source=="number 8");
			Assert.AreEqual(sizeText.Width,itemNumber.WidthPixels);
			Assert.AreEqual(Color.Red.ToArgb(),itemNumber.Color.ToArgb(),"The selected number has the highlight colour.");
			//At the printout size the font grows with the square root of the ratio.
			ToothChartData tcDataPrintout=CreateData(ChartScenes.SizePrintout.Size);
			Assert.AreEqual((int)Math.Round((float)(14*Math.Sqrt(tcDataPrintout.PixelScaleRatio))),ToothSceneBuilder.GetFontNumbers(tcDataPrintout.PixelScaleRatio).Height);
		}

		[TestMethod]
		public void IsSame3DLayer_ForASelectionOrDrawingChange_NotForAColourOrMovement() {
			ToothChartData tcData=CreateData(ChartScenes.SizeModule.Size);
			ToothScene toothScene1=ToothSceneBuilder.Build(tcData);
			tcData.SetSelected("8",true);
			ToothInitial toothInitial=new ToothInitial();
			toothInitial.ColorDraw=Color.Black;
			toothInitial.DrawingSegment="10,10;30,30";
			tcData.DrawingSegmentList.Add(toothInitial);
			ToothScene toothScene2=ToothSceneBuilder.Build(tcData);
			Assert.IsTrue(toothScene2.IsSame3DLayer(toothScene1),"Selection and drawings are 2D only.");
			Assert.AreNotEqual(toothScene1.Items2D.Count,toothScene2.Items2D.Count);
			tcData.ListToothGraphics["8"].SetSurfaceColors("M",Color.Blue);
			Assert.IsFalse(ToothSceneBuilder.Build(tcData).IsSame3DLayer(toothScene2),"A colour is 3D.");
			ToothScene toothScene3=ToothSceneBuilder.Build(tcData);
			tcData.ListToothGraphics["9"].Rotate=10;
			Assert.IsFalse(ToothSceneBuilder.Build(tcData).IsSame3DLayer(toothScene3),"A movement is 3D.");
			tcData.ColorBackground=Color.White;
			Assert.IsFalse(ToothSceneBuilder.Build(tcData).IsSame3DLayer(toothScene3),"The background is drawn in the 3D layer.");
		}

		[TestMethod]
		public void Projection_IsDirectXsWithHalfAPixel_AndTheCameraUsesTheSameMapping() {
			ToothChartData tcData=CreateData(new Size(500,300));//wide: the projection widens
			Assert.IsTrue(tcData.IsWide);
			ToothScene toothScene=ToothSceneBuilder.Build(tcData);
			Assert.AreEqual(tcData.SizeOriginalProjection.Height,toothScene.HeightProjection);
			Assert.AreEqual(tcData.SizeOriginalProjection.Height*500f/300f,toothScene.WidthProjection,0.0001);
			//The scene's origin is the image's centre in DirectX, and half a pixel further right and down in WPF's coordinates.
			toothScene.ProjectDirectX(ChartMatrix.Identity,0,0,0,out float xScreen,out float yScreen);
			Assert.AreEqual(250f,xScreen,0.0001);
			Assert.AreEqual(150f,yScreen,0.0001);
			PointF point=toothScene.ProjectToImage(ChartMatrix.Identity,10,-5,3);
			Assert.AreEqual(250+10*tcData.ScaleMmToPix+0.5,point.X,0.001);
			Assert.AreEqual(150+5*tcData.ScaleMmToPix+0.5,point.Y,0.001);
			//The camera's centre: half a pixel left and up in mm, so the 3D layer is shifted the same half pixel.
			Assert.AreEqual(-0.5/tcData.ScaleMmToPix,toothScene.CameraCenterX,0.00001);
			Assert.AreEqual(0.5/tcData.ScaleMmToPix,toothScene.CameraCenterY,0.00001);
		}
	}
}
