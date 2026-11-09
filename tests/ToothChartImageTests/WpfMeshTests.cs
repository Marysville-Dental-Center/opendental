// Mesh tests of the WPF chart (package TC-1, docs/specs/wpf-tooth-chart-plan.md section 5.2): the winding rule, the mirrored teeth, the meshes'
// vertices, and the front faces under every transform the chart uses.
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
	public class WpfMeshTests {
		///<summary>Every model of the chart: teeth 1-32, A-T and the implant.</summary>
		public static List<string> GetModelIds() {
			List<string> listIds=Enumerable.Range(1,32).Select(x => x.ToString()).ToList();
			listIds.AddRange("ABCDEFGHIJKLMNOPQRST".Select(x => x.ToString()));
			listIds.Add("implant");
			return listIds;
		}

		///<summary>The pairs that share an .obj file (ToothGraphic.ImportObj), the first loaded as it is and the second mirrored in X (teeth 9-24).</summary>
		public static readonly string[][] ArrayMirrorPairs={
			new[] { "1","16" },new[] { "2","15" },new[] { "3","14" },new[] { "4","13" },new[] { "5","12" },new[] { "6","11" },new[] { "7","10" },
			new[] { "8","9" },new[] { "32","17" },new[] { "31","18" },new[] { "30","19" },new[] { "29","20" },new[] { "28","21" },new[] { "27","22" },
			new[] { "25","24" },new[] { "26","23" },
		};

		[TestMethod]
		public void Winding_EveryTriangleAgreesWithItsVertexNormals() {
			int countTriangles=0;
			int countTurned=0;
			foreach(string toothId in GetModelIds()) {
				ToothGraphic toothGraphic=new ToothGraphic(toothId);
				for(int g=0;g<toothGraphic.Groups.Count;g++) {
					int[] arrayWound=ToothMeshWinding.GetWoundTriangles(toothGraphic,g);
					List<int> listFan=toothGraphic.Groups[g].GetTriangleIndices();
					Assert.AreEqual(listFan.Count,arrayWound.Length,toothId+" group "+g+": the same triangles.");
					for(int i=0;i+2<arrayWound.Length;i+=3) {
						countTriangles++;
						double agreement=ToothMeshWinding.GetNormalAgreement(toothGraphic,arrayWound[i],arrayWound[i+1],arrayWound[i+2]);
						Assert.IsTrue(agreement>=0,toothId+" group "+g+" triangle "+(i/3)+" points away from its normals.");
						//Same corners, and the first corner kept: a turned triangle swaps only the last two.
						Assert.AreEqual(listFan[i],arrayWound[i]);
						CollectionAssert.AreEquivalent(new[] { listFan[i+1],listFan[i+2] },new[] { arrayWound[i+1],arrayWound[i+2] });
						if(listFan[i+1]!=arrayWound[i+1]) {
							countTurned++;
						}
					}
				}
			}
			Console.WriteLine(countTriangles+" triangles in 53 models, "+countTurned+" turned to agree with their normals.");
			Assert.IsTrue(countTurned>0,"The mirrored teeth must have been turned.");
		}

		[TestMethod]
		public void Winding_TurnsTheMirroredTeeth_TheExactReverseOfTheirModels() {
			//Mirroring X negates every geometric normal and mirrors every vertex normal, so each triangle's agreement changes sign, and the mirrored
			//tooth's triangles are its model's reversed. This is the reversal of teeth 9-24 that ImportObj does not do (ToothGraphic.cs, its comment).
			foreach(string[] arrayPair in ArrayMirrorPairs) {
				ToothGraphic toothGraphic=new ToothGraphic(arrayPair[0]);
				ToothGraphic toothGraphicMirrored=new ToothGraphic(arrayPair[1]);
				Assert.AreEqual(toothGraphic.VertexNormals.Count,toothGraphicMirrored.VertexNormals.Count,arrayPair[1]);
				for(int v=0;v<toothGraphic.VertexNormals.Count;v++) {
					Assert.AreEqual(-toothGraphic.VertexNormals[v].Vertex.X,toothGraphicMirrored.VertexNormals[v].Vertex.X,arrayPair[1]+" vertex "+v);
					Assert.AreEqual(toothGraphic.VertexNormals[v].Vertex.Y,toothGraphicMirrored.VertexNormals[v].Vertex.Y);
					Assert.AreEqual(-toothGraphic.VertexNormals[v].Normal.X,toothGraphicMirrored.VertexNormals[v].Normal.X);
				}
				int countCompared=0;
				for(int g=0;g<toothGraphic.Groups.Count;g++) {
					int[] arrayWound=ToothMeshWinding.GetWoundTriangles(toothGraphic,g);
					int[] arrayWoundMirrored=ToothMeshWinding.GetWoundTriangles(toothGraphicMirrored,g);
					for(int i=0;i+2<arrayWound.Length;i+=3) {
						double agreement=ToothMeshWinding.GetNormalAgreement(toothGraphic,arrayWound[i],arrayWound[i+1],arrayWound[i+2]);
						if(agreement==0) {
							continue;//no area, or exactly edge-on to its normals: either order
						}
						Assert.AreEqual(arrayWound[i],arrayWoundMirrored[i]);
						Assert.AreEqual(arrayWound[i+1],arrayWoundMirrored[i+2],arrayPair[1]+" group "+g+" triangle "+(i/3));
						Assert.AreEqual(arrayWound[i+2],arrayWoundMirrored[i+1]);
						countCompared++;
					}
				}
				Assert.IsTrue(countCompared>100,arrayPair[1]+": "+countCompared+" triangles compared.");
			}
		}

		[TestMethod]
		public void Reversed_IsEveryTriangleWithItsLastTwoCornersSwapped() {
			int[] arrayTriangles={ 0,1,2,3,4,5,6,7,8 };
			CollectionAssert.AreEqual(new[] { 0,2,1,3,5,4,6,8,7 },ToothMeshWinding.GetReversedTriangles(arrayTriangles));
			CollectionAssert.AreEqual(new[] { 0,1,2,3,4,5,6,7,8 },arrayTriangles,"The list given is not changed.");
			ToothGraphic toothGraphic=new ToothGraphic("3");
			for(int g=0;g<toothGraphic.Groups.Count;g++) {
				int[] arrayWound=ToothMeshWinding.GetWoundTriangles(toothGraphic,g);
				CollectionAssert.AreEqual(arrayWound,ToothMeshWinding.GetReversedTriangles(ToothMeshWinding.GetReversedTriangles(arrayWound)));
			}
		}

		[TestMethod]
		public void Meshes_TakeTheirVerticesFromVertexNormals_OneForOne() {
			StaRunner.Run(() => {
				foreach(string toothId in GetModelIds()) {
					ToothGraphic toothGraphic=new ToothGraphic(toothId);
					ToothMeshSet toothMeshSet=ToothMeshCache.BuildMeshSet(toothGraphic);
					Assert.AreEqual(toothGraphic.Groups.Count,toothMeshSet.ArrayGroups.Length);
					HashSet<int> hashSetUsed=new HashSet<int>();
					for(int g=0;g<toothGraphic.Groups.Count;g++) {
						ToothGroupMesh toothGroupMesh=toothMeshSet.ArrayGroups[g];
						Assert.AreEqual(toothGraphic.Groups[g].GroupType,toothGroupMesh.GroupType);
						//Each group's mesh holds the vertices its triangles use, each once: counted here from the DirectX chart's fan triangles,
						//independently of the mesh cache.
						int countPositionsExpected=toothGraphic.Groups[g].GetTriangleIndices().Distinct().Count();
						Assert.AreEqual(countPositionsExpected,toothGroupMesh.Mesh.Positions.Count,toothId+" group "+g+": one position per vertex its triangles use.");
						Assert.IsTrue(toothGroupMesh.Mesh.IsFrozen && toothGroupMesh.MeshReversed.IsFrozen,toothId+": frozen, usable from any thread.");
						Assert.AreSame(toothGroupMesh.Mesh.Positions,toothGroupMesh.MeshReversed.Positions,"Both windings share the positions.");
						Assert.AreEqual(toothGroupMesh.ArraySourceIndices.Length,toothGroupMesh.Mesh.Positions.Count);
						Assert.AreEqual(toothGroupMesh.ArraySourceIndices.Length,toothGroupMesh.Mesh.Normals.Count);
						Assert.AreEqual(toothGroupMesh.ArraySourceIndices.Length,toothGroupMesh.ArraySourceIndices.Distinct().Count(),"No vertex twice.");
						for(int i=0;i<toothGroupMesh.ArraySourceIndices.Length;i++) {
							VertexNormal vertexNormal=toothGraphic.VertexNormals[toothGroupMesh.ArraySourceIndices[i]];
							Assert.AreEqual(new Point3D(vertexNormal.Vertex.X,vertexNormal.Vertex.Y,vertexNormal.Vertex.Z),toothGroupMesh.Mesh.Positions[i]);
							Assert.AreEqual(new Vector3D(vertexNormal.Normal.X,vertexNormal.Normal.Y,vertexNormal.Normal.Z),toothGroupMesh.Mesh.Normals[i]);
							hashSetUsed.Add(toothGroupMesh.ArraySourceIndices[i]);
						}
						//The mesh's triangles, mapped back, are the wound triangles; the reversed mesh's are their reverse.
						int[] arrayWound=ToothMeshWinding.GetWoundTriangles(toothGraphic,g);
						int[] arrayMapped=toothGroupMesh.Mesh.TriangleIndices.Select(x => toothGroupMesh.ArraySourceIndices[x]).ToArray();
						CollectionAssert.AreEqual(arrayWound,arrayMapped,toothId+" group "+g);
						int[] arrayMappedReversed=toothGroupMesh.MeshReversed.TriangleIndices.Select(x => toothGroupMesh.ArraySourceIndices[x]).ToArray();
						CollectionAssert.AreEqual(ToothMeshWinding.GetReversedTriangles(arrayWound),arrayMappedReversed);
					}
					//Every VertexNormal belongs to a face, so the groups' meshes together hold every one of them: the union, counted from the fan
					//triangles of every group, is all of VertexNormals.
					Assert.AreEqual(toothGraphic.VertexNormals.Count,hashSetUsed.Count,toothId+": every VertexNormal is in a mesh.");
					Assert.AreEqual(toothGraphic.VertexNormals.Count,Enumerable.Range(0,toothGraphic.Groups.Count).SelectMany(x => toothGraphic.Groups[x].GetTriangleIndices())
						.Distinct().Count(),toothId+": the triangles use every VertexNormal.");
				}
			});
		}

		[TestMethod]
		public void FrontFaces_AreThoseFacingTheCamera_ForEveryTransformTheChartUses() {
			//Both views in all four quadrants with movements, the implants (the maxillary flip included), and the four perio rows with their implants.
			List<ToothSceneItem3D> listItems=StaRunner.Run(() => {
				List<ToothSceneItem3D> listItemsAll=new List<ToothSceneItem3D>();
				using(ToothChartWrapper wrapper=new ToothChartWrapper()) {
					wrapper.Size=ChartScenes.SizeModule.Size;
					ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
					ApplyMovementsAndImplants(relay);
					ToothScene toothScene=ToothSceneBuilder.Build(wrapper.TcData);
					listItemsAll.AddRange(toothScene.Items3D);
					Assert.IsTrue(toothScene.Items3D.Any(x => x.Source=="implant of 3"),"A maxillary implant is in the scene.");
					Assert.IsFalse(toothScene.Items3D.Any(x => x.IsReversed),"The normal chart has only rotations and translations: no mirrored instance.");
				}
				using(ToothChartWrapper wrapper=new ToothChartWrapper()) {
					wrapper.Size=ChartScenes.SizePerio.Size;
					ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
					relay.SetImplant("3",Color.Gainsboro);
					relay.SetImplant("30",Color.Gainsboro);
					wrapper.TcData.PerioMode=true;//as a DirectX chart in perio mode has it; the wrapper allows Wpf3D perio from TC-7
					ToothScene toothScene=ToothSceneBuilder.Build(wrapper.TcData);
					listItemsAll.AddRange(toothScene.Items3D);
					Assert.IsTrue(toothScene.Items3D.Any(x => x.IsReversed && x.Source.Contains("lingual")),"The lingual rows mirror.");
					Assert.IsTrue(toothScene.Items3D.Where(x => x.Source.Contains("lingual")).All(x => x.IsReversed));
					Assert.IsTrue(toothScene.Items3D.Where(x => x.Source.Contains("facial")).All(x => !x.IsReversed));
				}
				return listItemsAll;
			});
			Dictionary<string,ToothGraphic> dictModels=new Dictionary<string,ToothGraphic>();
			int countChecked=0;
			int countEdgeOn=0;
			int countVertexNormalsDiffer=0;
			List<string> listWrong=new List<string>();
			foreach(ToothSceneItem3D item in listItems) {
				if(!dictModels.TryGetValue(item.ToothId,out ToothGraphic toothGraphic)) {
					toothGraphic=new ToothGraphic(item.ToothId);
					dictModels[item.ToothId]=toothGraphic;
				}
				int[] arrayWound=ToothMeshWinding.GetWoundTriangles(toothGraphic,item.GroupIndex);
				int[] arrayDrawn=item.IsReversed?ToothMeshWinding.GetReversedTriangles(arrayWound):arrayWound;
				double[,] arrayNormalMatrix=GetNormalMatrix(item.Transform);
				for(int i=0;i+2<arrayDrawn.Length;i+=3) {
					//WPF keeps a triangle that runs counter-clockwise as seen from the camera, which looks down -Z with +Y up.
					double crossZ=GetScreenCrossZ(item.Transform,toothGraphic,arrayDrawn[i],arrayDrawn[i+1],arrayDrawn[i+2]);
					//The triangle's outward normal (its geometric normal as wound, which agrees with its vertex normals), through the inverse transpose.
					double[] arrayOutward=GetGeometricNormal(toothGraphic,arrayWound[i],arrayWound[i+1],arrayWound[i+2]);
					double areaTwice=Math.Sqrt(arrayOutward[0]*arrayOutward[0]+arrayOutward[1]*arrayOutward[1]+arrayOutward[2]*arrayOutward[2]);
					double[] arrayTransformed=TransformNormal(arrayNormalMatrix,arrayOutward[0],arrayOutward[1],arrayOutward[2]);
					double length=Math.Sqrt(arrayTransformed[0]*arrayTransformed[0]+arrayTransformed[1]*arrayTransformed[1]+arrayTransformed[2]*arrayTransformed[2]);
					//Edge-on triangles may go either way: normals within about 3 degrees of the screen plane, or slivers with no area to speak of
					//(under a millionth of a square mm), whose winding the rasterizer's own rounding decides.
					if(areaTwice<2e-6 || length<1e-12 || Math.Abs(arrayTransformed[2])<0.05*length) {
						countEdgeOn++;
						continue;
					}
					countChecked++;
					bool isKept=crossZ>0;
					bool isFacing=arrayTransformed[2]>0;
					if(isKept!=isFacing && listWrong.Count<20) {
						listWrong.Add(item.Source+" "+item.ToothId+" group "+item.GroupIndex+" triangle "+(i/3)+(isKept?" kept but facing away":" culled but facing the camera"));
					}
					//The smooth vertex normals (the sum of the three) agree with the faces for nearly every triangle; they part only at sharp edges.
					Vertex3f na=toothGraphic.VertexNormals[arrayWound[i]].Normal;
					Vertex3f nb=toothGraphic.VertexNormals[arrayWound[i+1]].Normal;
					Vertex3f nc=toothGraphic.VertexNormals[arrayWound[i+2]].Normal;
					double[] arrayVertexNormals=TransformNormal(arrayNormalMatrix,(double)na.X+nb.X+nc.X,(double)na.Y+nb.Y+nc.Y,(double)na.Z+nb.Z+nc.Z);
					if((arrayVertexNormals[2]>0)!=isFacing) {
						countVertexNormalsDiffer++;
					}
				}
			}
			Console.WriteLine(listItems.Count+" instances, "+countChecked+" triangles checked, "+countEdgeOn+" edge-on left out; for "
				+countVertexNormalsDiffer+" the smooth vertex normals face the other way (silhouette edges).");
			Assert.AreEqual(0,listWrong.Count,"Front faces must be exactly the triangles facing the camera:\r\n"+string.Join("\r\n",listWrong));
			Assert.IsTrue(countChecked>50000,"Checked "+countChecked);
			//A wrong winding rule would put about half the triangles here; the silhouette edges put about 3%.
			Assert.IsTrue(countVertexNormalsDiffer<countChecked/20,"The vertex normals agree with the faces for all but "+countVertexNormalsDiffer+" of "+countChecked);
		}

		///<summary>The z of (b-a) x (c-a) after the transform, in double as WPF's Matrix3D transforms: positive when the triangle runs
		///counter-clockwise seen from +Z.</summary>
		private static double GetScreenCrossZ(ChartMatrix transform,ToothGraphic toothGraphic,int a,int b,int c) {
			double[] arrayA=TransformPoint(transform,toothGraphic,a);
			double[] arrayB=TransformPoint(transform,toothGraphic,b);
			double[] arrayC=TransformPoint(transform,toothGraphic,c);
			return (arrayB[0]-arrayA[0])*(arrayC[1]-arrayA[1])-(arrayB[1]-arrayA[1])*(arrayC[0]-arrayA[0]);
		}

		private static double[] TransformPoint(ChartMatrix m,ToothGraphic toothGraphic,int index) {
			double x=Vx(toothGraphic,index);
			double y=Vy(toothGraphic,index);
			double z=Vz(toothGraphic,index);
			return new[] {
				x*m.M11+y*m.M21+z*m.M31+m.M41,
				x*m.M12+y*m.M22+z*m.M32+m.M42,
				x*m.M13+y*m.M23+z*m.M33+m.M43,
			};
		}

		///<summary>(b-a) x (c-a) in the model's space.</summary>
		private static double[] GetGeometricNormal(ToothGraphic toothGraphic,int a,int b,int c) {
			double abx=(double)Vx(toothGraphic,b)-Vx(toothGraphic,a);
			double aby=(double)Vy(toothGraphic,b)-Vy(toothGraphic,a);
			double abz=(double)Vz(toothGraphic,b)-Vz(toothGraphic,a);
			double acx=(double)Vx(toothGraphic,c)-Vx(toothGraphic,a);
			double acy=(double)Vy(toothGraphic,c)-Vy(toothGraphic,a);
			double acz=(double)Vz(toothGraphic,c)-Vz(toothGraphic,a);
			return new[] { aby*acz-abz*acy,abz*acx-abx*acz,abx*acy-aby*acx };
		}

		///<summary>A normal (row vector) times the normal matrix.</summary>
		private static double[] TransformNormal(double[,] arrayNormalMatrix,double x,double y,double z) {
			return new[] {
				arrayNormalMatrix[0,0]*x+arrayNormalMatrix[1,0]*y+arrayNormalMatrix[2,0]*z,
				arrayNormalMatrix[0,1]*x+arrayNormalMatrix[1,1]*y+arrayNormalMatrix[2,1]*z,
				arrayNormalMatrix[0,2]*x+arrayNormalMatrix[1,2]*y+arrayNormalMatrix[2,2]*z,
			};
		}

		///<summary>Movements in all four quadrants (rotation, both tips and the three shifts), a primary tooth, and implants on 3, 14, 19 and 30.</summary>
		public static void ApplyMovementsAndImplants(ToothChartRelay relay) {
			relay.ResetTeeth();
			relay.SetPrimary("12");
			relay.MoveTooth("4",30,0,0,0,0,0);
			relay.MoveTooth("13",-25,10,0,0.5f,0,0);
			relay.MoveTooth("20",0,15,-10,0,1,0);
			relay.MoveTooth("29",45,-12,8,-0.5f,-1,0.5f);
			relay.MoveTooth("8",0,0,20,0,0,0);
			relay.MoveTooth("24",-15,0,0,0,0,0);
			foreach(string toothId in new[] { "3","14","19","30" }) {
				relay.SetMissing(toothId);
				relay.SetImplant(toothId,Color.Gainsboro);
			}
			relay.SetCrown("14",Color.WhiteSmoke);
		}

		private static float Vx(ToothGraphic toothGraphic,int index) {
			return toothGraphic.VertexNormals[index].Vertex.X;
		}

		private static float Vy(ToothGraphic toothGraphic,int index) {
			return toothGraphic.VertexNormals[index].Vertex.Y;
		}

		private static float Vz(ToothGraphic toothGraphic,int index) {
			return toothGraphic.VertexNormals[index].Vertex.Z;
		}

		///<summary>The inverse transpose of the transform's upper 3x3 (for row vectors: a normal n goes to n*M^-T), from its cofactors.</summary>
		public static double[,] GetNormalMatrix(ChartMatrix m) {
			double[,] a={ { m.M11,m.M12,m.M13 },{ m.M21,m.M22,m.M23 },{ m.M31,m.M32,m.M33 } };
			double det=m.Determinant3x3;
			double[,] inverse=new double[3,3];
			inverse[0,0]=(a[1,1]*a[2,2]-a[1,2]*a[2,1])/det;
			inverse[0,1]=(a[0,2]*a[2,1]-a[0,1]*a[2,2])/det;
			inverse[0,2]=(a[0,1]*a[1,2]-a[0,2]*a[1,1])/det;
			inverse[1,0]=(a[1,2]*a[2,0]-a[1,0]*a[2,2])/det;
			inverse[1,1]=(a[0,0]*a[2,2]-a[0,2]*a[2,0])/det;
			inverse[1,2]=(a[0,2]*a[1,0]-a[0,0]*a[1,2])/det;
			inverse[2,0]=(a[1,0]*a[2,1]-a[1,1]*a[2,0])/det;
			inverse[2,1]=(a[0,1]*a[2,0]-a[0,0]*a[2,1])/det;
			inverse[2,2]=(a[0,0]*a[1,1]-a[0,1]*a[1,0])/det;
			double[,] transposed=new double[3,3];
			for(int r=0;r<3;r++) {
				for(int c=0;c<3;c++) {
					transposed[r,c]=inverse[c,r];
				}
			}
			return transposed;
		}
	}
}
