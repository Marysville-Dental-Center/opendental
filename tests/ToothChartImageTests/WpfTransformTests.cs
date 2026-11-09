// Transform and layer tests of the WPF chart (package TC-1, docs/specs/wpf-tooth-chart-plan.md sections 5.1 to 5.3): sample vertices through WPF's
// camera chain and through the DirectX chain computed with SharpDX.Mathematics land within 0.001 mm of each other, and a marker vertex lands on the
// same pixel in the 3D layer and in the 2D layer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Media.Media3D;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDentalBusiness;
using SparksToothChart;
using SparksToothChart.Wpf3D;

namespace ToothChartImageTests {
	[TestClass]
	public class WpfTransformTests {
		///<summary>The scenes of the transform test: the normal chart with movements in all four quadrants and implants, at the module size and at a
		///wide size, and the perio chart with implants. Each with its data, which outlives its wrapper (disposing a wrapper leaves its data alone).</summary>
		private static List<KeyValuePair<ToothChartData,ToothScene>> BuildTransformScenes() {
			List<KeyValuePair<ToothChartData,ToothScene>> listScenes=new List<KeyValuePair<ToothChartData,ToothScene>>();
			foreach(Size size in new[] { ChartScenes.SizeModule.Size,ChartScenes.SizeSheetWide.Size,ChartScenes.SizeSheetTall.Size }) {
				using(ToothChartWrapper wrapper=new ToothChartWrapper()) {
					wrapper.Size=size;
					WpfMeshTests.ApplyMovementsAndImplants(WpfCharts.CreateRelay(wrapper));
					listScenes.Add(new KeyValuePair<ToothChartData,ToothScene>(wrapper.TcData,ToothSceneBuilder.Build(wrapper.TcData)));
				}
			}
			using(ToothChartWrapper wrapperPerio=new ToothChartWrapper()) {
				wrapperPerio.Size=ChartScenes.SizePerio.Size;
				ToothChartRelay relayPerio=WpfCharts.CreateRelay(wrapperPerio);
				relayPerio.SetMissing("3");
				relayPerio.SetImplant("3",Color.Gainsboro);
				relayPerio.SetMissing("30");
				relayPerio.SetImplant("30",Color.Gainsboro);
				wrapperPerio.TcData.PerioMode=true;
				listScenes.Add(new KeyValuePair<ToothChartData,ToothScene>(wrapperPerio.TcData,ToothSceneBuilder.Build(wrapperPerio.TcData)));
			}
			return listScenes;
		}

		[TestMethod]
		public void SampleVertices_LandWithinAThousandthOfAMm_InWpfAndInDirectX() {
			StaRunner.Run(() => {
				int countPoints=0;
				double maxDifferenceMm=0;
				string worst="";
				foreach(KeyValuePair<ToothChartData,ToothScene> pair in BuildTransformScenes()) {
					ToothChartData tcData=pair.Key;
					ToothScene toothScene=pair.Value;
					Viewport3DVisual viewport3DVisual=new Viewport3DVisual();
					viewport3DVisual.Camera=ToothChartRenderer.CreateCamera(toothScene);
					viewport3DVisual.Viewport=new System.Windows.Rect(0,0,toothScene.Size.Width,toothScene.Size.Height);
					SharpDX.Matrix matrixProjection=GetDirectXProjection(tcData);
					Dictionary<string,ToothGraphic> dictModels=new Dictionary<string,ToothGraphic>();
					foreach(ToothSceneItem3D item in toothScene.Items3D) {
						if(!dictModels.TryGetValue(item.ToothId,out ToothGraphic toothGraphicModel)) {
							toothGraphicModel=tcData.ListToothGraphics[item.ToothId];
							dictModels[item.ToothId]=toothGraphicModel;
						}
						SharpDX.Matrix matrixWorld=GetDirectXWorld(tcData,item.Source);
						SharpDX.Matrix matrixScreen=matrixWorld*SharpDX.Matrix.Identity*matrixProjection;
						ModelVisual3D modelVisual3D=new ModelVisual3D();
						modelVisual3D.Transform=new MatrixTransform3D(ToothChartRenderer.ToMatrix3D(item.Transform));
						viewport3DVisual.Children.Add(modelVisual3D);
						GeneralTransform3DTo2D generalTransform3DTo2D=modelVisual3D.TransformToAncestor(viewport3DVisual);
						//Sample vertices: every 37th of the model.
						for(int v=item.GroupIndex%37;v<toothGraphicModel.VertexNormals.Count;v+=37) {
							Vertex3f vertex=toothGraphicModel.VertexNormals[v].Vertex;
							SharpDX.Vector3 vectorScreen=SharpDX.Vector3.Project(new SharpDX.Vector3(vertex.X,vertex.Y,vertex.Z),0,0,toothScene.Size.Width,
								toothScene.Size.Height,0,1,matrixScreen);
							Assert.IsTrue(generalTransform3DTo2D.TryTransform(new Point3D(vertex.X,vertex.Y,vertex.Z),out System.Windows.Point pointWpf));
							//WPF's pixel centres are on halves, DirectX 9's on whole numbers (ToothScene.CameraCenterX).
							double dxPixels=(pointWpf.X-0.5)-vectorScreen.X;
							double dyPixels=(pointWpf.Y-0.5)-vectorScreen.Y;
							double differenceMm=Math.Sqrt(dxPixels*dxPixels+dyPixels*dyPixels)/tcData.ScaleMmToPix;
							if(differenceMm>maxDifferenceMm) {
								maxDifferenceMm=differenceMm;
								worst=item.Source+" "+item.ToothId+" vertex "+v+" at "+toothScene.Size.Width+"x"+toothScene.Size.Height+": WPF "+pointWpf
									+", DirectX "+vectorScreen.X+","+vectorScreen.Y;
							}
							countPoints++;
						}
						viewport3DVisual.Children.Remove(modelVisual3D);
					}
				}
				Console.WriteLine(countPoints+" vertices compared; the largest difference is "+maxDifferenceMm.ToString("0.000000")+" mm ("+worst+").");
				Assert.IsTrue(countPoints>5000,"Compared "+countPoints);
				Assert.IsTrue(maxDifferenceMm<0.001,"WPF and DirectX differ by "+maxDifferenceMm+" mm: "+worst);
			});
		}

		[TestMethod]
		public void MarkerVertex_LandsOnTheSamePixel_InThe3DLayerAndThe2DLayer() {
			StaRunner.Run(() => {
				using ToothChartWrapper wrapper=new ToothChartWrapper();
				wrapper.Size=ChartScenes.SizeModule.Size;
				ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
				WpfMeshTests.ApplyMovementsAndImplants(relay);
				ToothScene toothScene=ToothSceneBuilder.Build(wrapper.TcData);
				Viewport3DVisual viewport3DVisual=new Viewport3DVisual();
				viewport3DVisual.Camera=ToothChartRenderer.CreateCamera(toothScene);
				viewport3DVisual.Viewport=new System.Windows.Rect(0,0,toothScene.Size.Width,toothScene.Size.Height);
				int countPoints=0;
				foreach(ToothSceneItem3D item in toothScene.Items3D.Where((x,i) => i%5==0)) {
					ToothGraphic toothGraphicModel=wrapper.TcData.ListToothGraphics[item.ToothId];
					ModelVisual3D modelVisual3D=new ModelVisual3D();
					modelVisual3D.Transform=new MatrixTransform3D(ToothChartRenderer.ToMatrix3D(item.Transform));
					viewport3DVisual.Children.Add(modelVisual3D);
					GeneralTransform3DTo2D generalTransform3DTo2D=modelVisual3D.TransformToAncestor(viewport3DVisual);
					for(int v=0;v<toothGraphicModel.VertexNormals.Count;v+=11) {
						Vertex3f vertex=toothGraphicModel.VertexNormals[v].Vertex;
						System.Windows.Point point3D=generalTransform3DTo2D.Transform(new Point3D(vertex.X,vertex.Y,vertex.Z));
						PointF point2D=toothScene.ProjectToImage(item.Transform,vertex.X,vertex.Y,vertex.Z);
						Assert.AreEqual(point3D.X,point2D.X,0.002,item.Source+" vertex "+v);
						Assert.AreEqual(point3D.Y,point2D.Y,0.002,item.Source+" vertex "+v);
						countPoints++;
					}
					viewport3DVisual.Children.Remove(modelVisual3D);
				}
				Assert.IsTrue(countPoints>1000,"Compared "+countPoints);
				//And in rendered pixels: tooth 8's facial view in the 3D layer alone, and its projected triangles as one 2D silhouette alone, both white
				//on black, cover the same box to the pixel.
				List<ToothSceneItem3D> listItems8=toothScene.Items3D.Where(x => x.Source=="8 facial").Select(x => new ToothSceneItem3D(x.ToothId,
					x.GroupIndex,x.GroupType,Color.White,x.Specular,x.Transform,x.Source)).ToList();
				Assert.IsTrue(listItems8.Count>3);
				List<PointF[]> listTriangles=new List<PointF[]>();
				foreach(ToothSceneItem3D item in listItems8) {
					ToothGraphic toothGraphicModel=wrapper.TcData.ListToothGraphics[item.ToothId];
					int[] arrayTriangles=ToothMeshWinding.GetWoundTriangles(toothGraphicModel,item.GroupIndex);
					for(int i=0;i+2<arrayTriangles.Length;i+=3) {
						listTriangles.Add(new[] { arrayTriangles[i],arrayTriangles[i+1],arrayTriangles[i+2] }.Select(x => {
							Vertex3f vertex=toothGraphicModel.VertexNormals[x].Vertex;
							return toothScene.ProjectToImage(item.Transform,vertex.X,vertex.Y,vertex.Z);
						}).ToArray());
					}
				}
				ToothScene toothScene3D=new ToothScene(toothScene.Size,Color.Black,false,toothScene.WidthProjection,toothScene.HeightProjection,listItems8,
					new List<ToothSceneItem2D>());
				ToothScene toothScene2D=new ToothScene(toothScene.Size,Color.Black,false,toothScene.WidthProjection,toothScene.HeightProjection,
					new List<ToothSceneItem3D>(),new List<ToothSceneItem2D> { ToothSceneItem2D.CreatePolygons(Color.White,listTriangles,"silhouette of 8") });
				Rectangle rectangle3D=GetLitBounds(ToothChartRenderer.Instance.Render(toothScene3D,null));
				Rectangle rectangle2D=GetLitBounds(ToothChartRenderer.Instance.Render(toothScene2D,null));
				Console.WriteLine("Tooth 8 facial: 3D layer "+rectangle3D+", 2D layer "+rectangle2D);
				Assert.IsTrue(rectangle3D.Width>10 && rectangle3D.Height>20,"The tooth was drawn: "+rectangle3D);
				Assert.IsTrue(Math.Abs(rectangle3D.Left-rectangle2D.Left)<=1 && Math.Abs(rectangle3D.Right-rectangle2D.Right)<=1
					&& Math.Abs(rectangle3D.Top-rectangle2D.Top)<=1 && Math.Abs(rectangle3D.Bottom-rectangle2D.Bottom)<=1,
					"The layers are aligned: 3D "+rectangle3D+", 2D "+rectangle2D);
			});
		}

		///<summary>The box of the pixels brighter than a fifth of white.</summary>
		private static Rectangle GetLitBounds(ToothChartRenderResult result) {
			int left=int.MaxValue;
			int top=int.MaxValue;
			int right=-1;
			int bottom=-1;
			for(int y=0;y<result.Size.Height;y++) {
				for(int x=0;x<result.Size.Width;x++) {
					int i=(y*result.Size.Width+x)*4;
					int max=Math.Max(result.PixelsBgra[i],Math.Max(result.PixelsBgra[i+1],result.PixelsBgra[i+2]));
					if(max<=51) {
						continue;
					}
					left=Math.Min(left,x);
					top=Math.Min(top,y);
					right=Math.Max(right,x);
					bottom=Math.Max(bottom,y);
				}
			}
			if(right<0) {
				return Rectangle.Empty;
			}
			return Rectangle.FromLTRB(left,top,right+1,bottom+1);
		}

		#region The DirectX chain, as ToothChartDirectX computes it
		//Copies of the DirectX chart's private matrix code (ToothChartDirectX: Render, DrawScene, DrawFacialView, DrawOcclusalView, DrawImplant,
		//DrawScenePerio, DrawPerioRow, ToothTranslationMatrix, ToothRotationMatrix), with SharpDX.Mathematics, so the scene builder's ChartMatrix
		//code is checked against the library DirectX used.

		private static SharpDX.Matrix GetDirectXProjection(ToothChartData tcData) {
			float heightProj=tcData.SizeOriginalProjection.Height;
			float widthProj=tcData.SizeOriginalProjection.Width;
			int width=tcData.SizeControl.Width;
			int height=tcData.SizeControl.Height;
			if(tcData.IsWide) {
				widthProj=heightProj*width/height;
			}
			else {
				heightProj=widthProj*height/width;
			}
			return SharpDX.Matrix.OrthoLH(widthProj,heightProj,0,1000.0f);
		}

		///<summary>DirectX's world matrix of the item, from its source: "8 facial", "8 occlusal", "implant of 3", or in perio "8 maxillary facial row"
		///and "implant of 3 maxillary lingual row".</summary>
		private static SharpDX.Matrix GetDirectXWorld(ToothChartData tcData,string source) {
			bool isImplant=source.StartsWith("implant of ");
			string rest=isImplant?source.Substring("implant of ".Length):source;
			string toothId=rest.Split(' ')[0];
			string view=rest.Substring(toothId.Length).Trim();
			ToothGraphic toothGraphic=tcData.ListToothGraphics[toothId];
			SharpDX.Matrix world;
			if(tcData.PerioMode) {
				world=SharpDX.Matrix.Translation(tcData.GetTransX(toothId),0,0)*GetDirectXPerioRow(tcData,view);
			}
			else {
				SharpDX.Matrix defOrient=SharpDX.Matrix.Identity;
				defOrient.ScaleVector=new SharpDX.Vector3(1f,1f,-1f);
				SharpDX.Matrix trans=SharpDX.Matrix.Identity;
				trans.TranslationVector=new SharpDX.Vector3(0f,0f,400f);
				defOrient=defOrient*trans;
				if(view=="occlusal") {
					SharpDX.Matrix toothTrans=SharpDX.Matrix.Translation(tcData.GetTransX(toothId),tcData.GetTransYocclusal(toothId),0);
					SharpDX.Matrix toothRot=SharpDX.Matrix.Identity;
					if(ToothGraphic.IsMaxillary(toothId)) {
						toothRot=toothRot*SharpDX.Matrix.RotationX((float)((-110f*Math.PI)/180f));
					}
					else if(ToothGraphic.IsAnterior(toothId)) {
						toothRot=toothRot*SharpDX.Matrix.RotationX((float)((110f*Math.PI)/180f));
					}
					else {
						toothRot=toothRot*SharpDX.Matrix.RotationX((float)((120f*Math.PI)/180f));
					}
					world=GetDirectXRotation(toothGraphic)*GetDirectXTranslation(toothGraphic)*toothRot*toothTrans*defOrient;
				}
				else {//facial, and the implant, which is drawn after the facial view
					SharpDX.Matrix toothTrans=SharpDX.Matrix.Translation(tcData.GetTransX(toothId),tcData.GetTransYfacial(toothId),0);
					world=GetDirectXRotation(toothGraphic)*GetDirectXTranslation(toothGraphic)*toothTrans*defOrient;
				}
			}
			if(isImplant && ToothGraphic.IsMaxillary(toothId)) {
				world=SharpDX.Matrix.RotationZ((float)Math.PI)*world;//DrawImplant: flip the implant upside down
			}
			return world;
		}

		private static SharpDX.Matrix GetDirectXPerioRow(ToothChartData tcData,string rowName) {
			const float maxYmm=63f;
			float baseY=tcData.SizeControl.Height/(2f*tcData.ScaleMmToPix)-maxYmm;
			SharpDX.Matrix defOrient=SharpDX.Matrix.Scaling(1f,1f,-1f)*SharpDX.Matrix.Translation(0,baseY,400f);
			switch(rowName) {
				case "maxillary facial row":
					return SharpDX.Matrix.Translation(0,45f,0)*defOrient;
				case "maxillary lingual row":
					return SharpDX.Matrix.Scaling(1f,1f,-1f)*SharpDX.Matrix.Translation(0,12f,0)*defOrient;
				case "mandibular lingual row":
					return SharpDX.Matrix.Scaling(1f,1f,-1f)*SharpDX.Matrix.Translation(0,-12f,0)*defOrient;
				case "mandibular facial row":
					return SharpDX.Matrix.Translation(0,-45f,0)*defOrient;
				default:
					throw new ArgumentException("Unknown perio row: "+rowName);
			}
		}

		private static SharpDX.Matrix GetDirectXTranslation(ToothGraphic toothGraphic) {
			if(ToothGraphic.IsRight(toothGraphic.ToothID)) {
				if(ToothGraphic.IsMaxillary(toothGraphic.ToothID)) {//UR
					return SharpDX.Matrix.Translation(toothGraphic.ShiftM,-toothGraphic.ShiftO,toothGraphic.ShiftB);
				}
				return SharpDX.Matrix.Translation(toothGraphic.ShiftM,toothGraphic.ShiftO,toothGraphic.ShiftB);//LR
			}
			if(ToothGraphic.IsMaxillary(toothGraphic.ToothID)) {//UL
				return SharpDX.Matrix.Translation(-toothGraphic.ShiftM,-toothGraphic.ShiftO,toothGraphic.ShiftB);
			}
			return SharpDX.Matrix.Translation(-toothGraphic.ShiftM,toothGraphic.ShiftO,toothGraphic.ShiftB);//LL
		}

		private static SharpDX.Matrix GetDirectXRotation(ToothGraphic toothGraphic) {
			SharpDX.Matrix rotM;
			SharpDX.Matrix rotB;
			SharpDX.Matrix rot;
			if(ToothGraphic.IsRight(toothGraphic.ToothID)) {
				if(ToothGraphic.IsMaxillary(toothGraphic.ToothID)) {//UR
					rotM=SharpDX.Matrix.RotationZ(((float)(toothGraphic.TipM*Math.PI)/180));
					rotB=SharpDX.Matrix.RotationX(((float)(-toothGraphic.TipB*Math.PI)/180));
					rot=SharpDX.Matrix.RotationY(((float)(toothGraphic.Rotate*Math.PI)/180));
				}
				else {//LR
					rotM=SharpDX.Matrix.RotationZ(((float)(-toothGraphic.TipM*Math.PI)/180));
					rotB=SharpDX.Matrix.RotationX(((float)(toothGraphic.TipB*Math.PI)/180));
					rot=SharpDX.Matrix.RotationY(((float)(-toothGraphic.Rotate*Math.PI)/180));
				}
			}
			else {
				if(ToothGraphic.IsMaxillary(toothGraphic.ToothID)) {//UL
					rotM=SharpDX.Matrix.RotationZ(((float)(-toothGraphic.TipM*Math.PI)/180));
					rotB=SharpDX.Matrix.RotationX(((float)(-toothGraphic.TipB*Math.PI)/180));
					rot=SharpDX.Matrix.RotationY(((float)(toothGraphic.Rotate*Math.PI)/180));
				}
				else {//LL
					rotM=SharpDX.Matrix.RotationZ(((float)(toothGraphic.TipM*Math.PI)/180));
					rotB=SharpDX.Matrix.RotationX(((float)(toothGraphic.TipB*Math.PI)/180));
					rot=SharpDX.Matrix.RotationY(((float)(-toothGraphic.Rotate*Math.PI)/180));
				}
			}
			return rot*rotB*rotM;
		}
		#endregion The DirectX chain, as ToothChartDirectX computes it
	}
}
