// Tests of the tooth chart's model (package TC-0): the .obj import, the mirrored teeth, the fan triangulation, the mouse hit test, the drag fill-in
// and the perio geometry, pinned to today's values so the WPF chart can reuse them unchanged.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using SparksToothChart;

namespace ToothChartImageTests {
	[TestClass]
	public class ModelTests {
		#region Recorded values
		///<summary>Today's import (ToothGraphic.ImportObj) of every tooth ID: VertexNormals, groups, faces, triangles, the group types in file order
		///and the first 16 hex digits of the SHA-256 of the model (GetModelHash). Recorded on 2026-11-08 from the build of TC-0 commit (5).</summary>
		private static readonly string[] _arrayModels={
			"1\t572\t10\t486\t824\tB,Buildup,Cementum,D,Enamel,L,M,O,V,None\tcdb4057c84746324",
			"2\t565\t10\t479\t822\tB,Buildup,Cementum,D,Enamel,L,M,O,V,None\tbb5299da745de824",
			"3\t602\t10\t492\t852\tB,Buildup,Cementum,D,Enamel,L,M,O,V,None\t9326f3ececc9f5ee",
			"4\t329\t12\t248\t484\tB,Buildup,Canals,Cementum,D,Enamel,EnamelF,L,M,O,V,None\t54df2e9ccffa8535",
			"5\t363\t11\t289\t512\tB,Buildup,Cementum,D,Enamel,EnamelF,L,M,O,V,None\t9be8aecfb98cf2f1",
			"6\t272\t14\t214\t394\tBuildup,Cementum,D,DF,Enamel,EnamelF,F,I,IF,L,M,MF,V,None\t3f294a980e8dfb99",
			"7\t250\t14\t210\t388\tBuildup,Cementum,D,DF,Enamel,EnamelF,F,I,IF,L,M,MF,V,None\t180af9c0f08bfb3e",
			"8\t265\t14\t196\t360\tBuildup,Cementum,D,DF,Enamel,EnamelF,F,I,IF,L,M,MF,V,None\t8c082b678452f518",
			"9\t265\t14\t196\t360\tBuildup,Cementum,D,DF,Enamel,EnamelF,F,I,IF,L,M,MF,V,None\tb16a39f16f4f3d03",
			"10\t250\t14\t210\t388\tBuildup,Cementum,D,DF,Enamel,EnamelF,F,I,IF,L,M,MF,V,None\t451a7c78c4da82ef",
			"11\t272\t14\t214\t394\tBuildup,Cementum,D,DF,Enamel,EnamelF,F,I,IF,L,M,MF,V,None\t01f4efb10dcdce57",
			"12\t363\t11\t289\t512\tB,Buildup,Cementum,D,Enamel,EnamelF,L,M,O,V,None\t0d7bdfa9009edeac",
			"13\t329\t12\t248\t484\tB,Buildup,Canals,Cementum,D,Enamel,EnamelF,L,M,O,V,None\tb560238ece9bac9d",
			"14\t602\t10\t492\t852\tB,Buildup,Cementum,D,Enamel,L,M,O,V,None\te5ea968599e2bcd7",
			"15\t565\t10\t479\t822\tB,Buildup,Cementum,D,Enamel,L,M,O,V,None\ta1ae5663a9590b42",
			"16\t572\t10\t486\t824\tB,Buildup,Cementum,D,Enamel,L,M,O,V,None\tdc67a262a435c33b",
			"17\t557\t10\t427\t742\tB,Buildup,Cementum,D,Enamel,L,M,O,V,None\taaeaec866801873c",
			"18\t564\t10\t432\t748\tB,Buildup,Cementum,D,Enamel,L,M,O,V,None\t79b03ccae22d1e40",
			"19\t581\t10\t448\t762\tB,Buildup,Cementum,D,Enamel,L,M,O,V,None\tc5e73a969469b351",
			"20\t347\t10\t255\t468\tB,Buildup,Cementum,D,Enamel,EnamelF,L,M,O,V\tcd5c343c280b6099",
			"21\t315\t11\t222\t414\tB,Buildup,Cementum,D,Enamel,EnamelF,L,M,O,V,None\t7bb32a0c18beb66f",
			"22\t273\t13\t221\t396\tBuildup,Cementum,D,DF,Enamel,EnamelF,F,I,IF,L,M,MF,V\t41356130728497c2",
			"23\t233\t14\t170\t312\tBuildup,Cementum,D,DF,Enamel,EnamelF,F,I,IF,L,M,MF,V,None\t665cc9b106378a25",
			"24\t233\t14\t170\t312\tBuildup,Cementum,D,DF,Enamel,EnamelF,F,I,IF,L,M,MF,V,None\t665cc9b106378a25",
			"25\t233\t14\t170\t312\tBuildup,Cementum,D,DF,Enamel,EnamelF,F,I,IF,L,M,MF,V,None\t3e3f34f47d4c9c7e",
			"26\t233\t14\t170\t312\tBuildup,Cementum,D,DF,Enamel,EnamelF,F,I,IF,L,M,MF,V,None\t3e3f34f47d4c9c7e",
			"27\t273\t13\t221\t396\tBuildup,Cementum,D,DF,Enamel,EnamelF,F,I,IF,L,M,MF,V\t2ec85a641b249f19",
			"28\t315\t11\t222\t414\tB,Buildup,Cementum,D,Enamel,EnamelF,L,M,O,V,None\t0769707aedea81ee",
			"29\t347\t10\t255\t468\tB,Buildup,Cementum,D,Enamel,EnamelF,L,M,O,V\t70b53d8c68a8422a",
			"30\t581\t10\t448\t762\tB,Buildup,Cementum,D,Enamel,L,M,O,V,None\t6497435640de190a",
			"31\t564\t10\t432\t748\tB,Buildup,Cementum,D,Enamel,L,M,O,V,None\t2e3fc66f3b57446f",
			"32\t557\t10\t427\t742\tB,Buildup,Cementum,D,Enamel,L,M,O,V,None\t6a7213811e89c15b",
			"A\t582\t10\t482\t832\tB,Buildup,Cementum,D,Enamel,L,M,O,V,None\t58ae18c737998c4a",
			"B\t537\t10\t468\t810\tB,Buildup,Cementum,D,Enamel,L,M,O,V,None\t1a58e044430147ab",
			"C\t273\t10\t214\t396\tBuildup,Cementum,D,Enamel,F,I,L,M,V,None\t3d2742c9bf149994",
			"D\t251\t10\t210\t390\tBuildup,Cementum,D,Enamel,F,I,L,M,V,None\t0654cededce37e36",
			"E\t265\t10\t196\t360\tBuildup,Cementum,D,Enamel,F,I,L,M,V,None\t843bf018a83a0d14",
			"F\t265\t10\t196\t360\tBuildup,Cementum,D,Enamel,F,I,L,M,V,None\t1afb294e98ad01cb",
			"G\t251\t10\t210\t390\tBuildup,Cementum,D,Enamel,F,I,L,M,V,None\t5418963c77ad2059",
			"H\t273\t10\t214\t396\tBuildup,Cementum,D,Enamel,F,I,L,M,V,None\t156ca873d2bf329a",
			"I\t537\t10\t468\t810\tB,Buildup,Cementum,D,Enamel,L,M,O,V,None\t101c1b5c001ee2b4",
			"J\t582\t10\t482\t832\tB,Buildup,Cementum,D,Enamel,L,M,O,V,None\t240bbb08400f0532",
			"K\t560\t9\t432\t734\tB,Buildup,Cementum,D,Enamel,L,M,O,V\t9a1595eecca866f7",
			"L\t493\t10\t399\t684\tB,Buildup,Cementum,D,Enamel,L,M,O,V,None\t67182f876e5ab3d4",
			"M\t273\t10\t221\t396\tBuildup,Cementum,D,Enamel,F,I,L,M,V,None\t4960546f48def8da",
			"N\t234\t10\t170\t314\tBuildup,Cementum,D,Enamel,F,I,L,M,V,None\tea0c6e73688ba8c9",
			"O\t234\t10\t170\t314\tBuildup,Cementum,D,Enamel,F,I,L,M,V,None\tea0c6e73688ba8c9",
			"P\t234\t10\t170\t314\tBuildup,Cementum,D,Enamel,F,I,L,M,V,None\t6f834a35b3669c08",
			"Q\t234\t10\t170\t314\tBuildup,Cementum,D,Enamel,F,I,L,M,V,None\t6f834a35b3669c08",
			"R\t273\t10\t221\t396\tBuildup,Cementum,D,Enamel,F,I,L,M,V,None\t32c0e210275ed06a",
			"S\t493\t10\t399\t684\tB,Buildup,Cementum,D,Enamel,L,M,O,V,None\te414bd984f343b80",
			"T\t560\t9\t432\t734\tB,Buildup,Cementum,D,Enamel,L,M,O,V\t2dbb39f941dbd171",
			"implant\t1874\t1\t496\t960\tImplant\t6e50d548a2bb9e79",
		};

		///<summary>The 25 model files and the 53 tooth IDs each is loaded for (ToothGraphic.ImportObj).</summary>
		private static readonly Dictionary<string,string[]> _dictFiles=new Dictionary<string,string[]> {
			{ "tooth1",new[] { "1","16" } },{ "tooth2",new[] { "2","15" } },{ "tooth3",new[] { "3","14" } },{ "tooth4",new[] { "4","13" } },
			{ "tooth5",new[] { "5","12" } },{ "tooth6",new[] { "6","11" } },{ "tooth7",new[] { "7","10" } },{ "tooth8",new[] { "8","9" } },
			{ "tooth32",new[] { "17","32" } },{ "tooth31",new[] { "18","31" } },{ "tooth30",new[] { "19","30" } },{ "tooth29",new[] { "20","29" } },
			{ "tooth28",new[] { "21","28" } },{ "tooth27",new[] { "22","27" } },{ "tooth25",new[] { "23","24","25","26" } },
			{ "toothA",new[] { "A","J" } },{ "toothB",new[] { "B","I" } },{ "toothC",new[] { "C","H" } },{ "toothD",new[] { "D","G" } },
			{ "toothE",new[] { "E","F" } },{ "toothP",new[] { "N","O","P","Q" } },{ "toothR",new[] { "M","R" } },{ "toothS",new[] { "L","S" } },
			{ "toothT",new[] { "K","T" } },{ "implant",new[] { "implant" } },
		};
		#endregion Recorded values

		#region Helpers
		private static Dictionary<string,ToothGraphic> _dictModels;

		///<summary>Every tooth ID's model, loaded once (53 models; the import is slow).</summary>
		private static ToothGraphic GetModel(string toothID) {
			if(_dictModels==null) {
				Dictionary<string,ToothGraphic> dict=new Dictionary<string,ToothGraphic>();
				foreach(string id in _dictFiles.Values.SelectMany(x => x)) {
					dict[id]=new ToothGraphic(id);
				}
				_dictModels=dict;
			}
			return _dictModels[toothID];
		}

		///<summary>The SHA-256 of the model: each VertexNormal's vertex and normal as six floats, then per group its type, face count and each face's
		///index count and indices, all little-endian.</summary>
		private static string GetModelHash(ToothGraphic toothGraphic) {
			using MemoryStream memoryStream=new MemoryStream();
			using BinaryWriter binaryWriter=new BinaryWriter(memoryStream);
			foreach(VertexNormal vertexNormal in toothGraphic.VertexNormals) {
				binaryWriter.Write(vertexNormal.Vertex.X);
				binaryWriter.Write(vertexNormal.Vertex.Y);
				binaryWriter.Write(vertexNormal.Vertex.Z);
				binaryWriter.Write(vertexNormal.Normal.X);
				binaryWriter.Write(vertexNormal.Normal.Y);
				binaryWriter.Write(vertexNormal.Normal.Z);
			}
			foreach(ToothGroup toothGroup in toothGraphic.Groups) {
				binaryWriter.Write((int)toothGroup.GroupType);
				binaryWriter.Write(toothGroup.Faces.Count);
				foreach(Face face in toothGroup.Faces) {
					binaryWriter.Write(face.IndexList.Count);
					foreach(int index in face.IndexList) {
						binaryWriter.Write(index);
					}
				}
			}
			binaryWriter.Flush();
			using SHA256 sha256=SHA256.Create();
			return string.Concat(sha256.ComputeHash(memoryStream.ToArray()).Select(x => x.ToString("x2")));
		}

		///<summary>True for the teeth ImportObj mirrors in X: 9-24 and the primary teeth over them (F-J, K-O).</summary>
		private static bool IsMirrored(string toothID) {
			return toothID!="implant" && ToothGraphic.IdToInt(toothID)>=9 && ToothGraphic.IdToInt(toothID)<=24;
		}

		///<summary>A chart's data with all 52 teeth and the implant, sized 410x307, as the Chart module has it.</summary>
		private static ToothChartData NewChartData() {
			ToothChartData toothChartData=null;
			StaRunner.Run(() => {
				using ToothChartWrapper wrapper=new ToothChartWrapper();
				toothChartData=wrapper.TcData;
			});
			toothChartData.SizeControl=new Size(410,307);
			return toothChartData;
		}

		///<summary>The control pixel at a point in scene millimetres: the inverse of ToothChartData.PointPixToMm, rounded to whole pixels as a mouse
		///event gives them.</summary>
		private static Point PointFromMm(ToothChartData toothChartData,float xMm,float yMm) {
			float x=(xMm+toothChartData.SizeOriginalProjection.Width/2f)*toothChartData.ScaleMmToPix+toothChartData.RectTarget.X;
			float y=(toothChartData.SizeOriginalProjection.Height/2f-yMm)*toothChartData.ScaleMmToPix+toothChartData.RectTarget.Y;
			return new Point((int)Math.Round(x),(int)Math.Round(y));
		}

		private static void AddMeasure(ToothChartData toothChartData,int intTooth,PerioSequenceType perioSequenceType,int mb,int b,int db,int ml,int l,
			int dl)
		{
			PerioMeasure perioMeasure=new PerioMeasure();
			perioMeasure.IntTooth=intTooth;
			perioMeasure.SequenceType=perioSequenceType;
			perioMeasure.MBvalue=mb;
			perioMeasure.Bvalue=b;
			perioMeasure.DBvalue=db;
			perioMeasure.MLvalue=ml;
			perioMeasure.Lvalue=l;
			perioMeasure.DLvalue=dl;
			toothChartData.ListPerioMeasure.Add(perioMeasure);
		}

		private static void AssertPoint(float xExpected,float yExpected,PointF pointActual,string message) {
			Assert.AreEqual(xExpected,pointActual.X,0.0001,message+" x");
			Assert.AreEqual(yExpected,pointActual.Y,0.0001,message+" y");
		}
		#endregion Helpers

		#region Import
		[TestMethod]
		public void Import_FiftyThreeToothIdsOnTwentyFiveModelFiles() {
			List<string> listFiles=Directory.GetFiles(TestPaths.ModelsFolder,"*.obj").Select(x => Path.GetFileNameWithoutExtension(x)).ToList();
			CollectionAssert.AreEquivalent(_dictFiles.Keys.ToList(),listFiles,"SparksToothChart\\Resources holds the 25 models.");
			List<string> listIds=_dictFiles.Values.SelectMany(x => x).ToList();
			Assert.AreEqual(53,listIds.Count);
			Assert.AreEqual(53,listIds.Distinct().Count());
			CollectionAssert.AreEquivalent(listIds,_arrayModels.Select(x => x.Split('\t')[0]).ToList());
		}

		[TestMethod]
		public void Import_GivesTodaysCountsAndModelPerToothId() {
			List<string> listDifferent=new List<string>();
			foreach(string line in _arrayModels) {
				string[] arrayFields=line.Split('\t');
				ToothGraphic toothGraphic=GetModel(arrayFields[0]);
				string actual=arrayFields[0]+"\t"+toothGraphic.VertexNormals.Count+"\t"+toothGraphic.Groups.Count+"\t"
					+toothGraphic.Groups.Sum(x => x.Faces.Count)+"\t"+toothGraphic.Groups.Sum(x => x.Faces.Sum(y => y.IndexList.Count-2))+"\t"
					+string.Join(",",toothGraphic.Groups.Select(x => x.GroupType.ToString()))+"\t"+GetModelHash(toothGraphic).Substring(0,16);
				if(actual!=line) {
					listDifferent.Add("expected "+line+"\r\n  actual "+actual);
				}
			}
			Assert.AreEqual(0,listDifferent.Count,string.Join("\r\n",listDifferent));
		}

		[TestMethod]
		public void Import_MatchesTheObjFile_GroupsFacesAndVertexNormalPairs() {
			//Every "g" line starts a group, every "f" line is a face, and each distinct vertex/normal index pair of the faces is one VertexNormal.
			foreach(KeyValuePair<string,string[]> pair in _dictFiles) {
				string[] arrayLines=File.ReadAllLines(Path.Combine(TestPaths.ModelsFolder,pair.Key+".obj"));
				int countGroups=arrayLines.Count(x => x.StartsWith("g"));
				int countFaces=arrayLines.Count(x => x.StartsWith("f"));
				HashSet<string> hashSetPairs=new HashSet<string>();
				foreach(string line in arrayLines.Where(x => x.StartsWith("f"))) {
					foreach(string item in line.Split(' ').Skip(1)) {
						string[] arrayParts=item.Split('/');
						hashSetPairs.Add(arrayParts[0]+"/"+arrayParts[2]);
					}
				}
				foreach(string toothID in pair.Value) {
					ToothGraphic toothGraphic=GetModel(toothID);
					Assert.AreEqual(countGroups,toothGraphic.Groups.Count,toothID+" groups");
					Assert.AreEqual(countFaces,toothGraphic.Groups.Sum(x => x.Faces.Count),toothID+" faces");
					Assert.AreEqual(hashSetPairs.Count,toothGraphic.VertexNormals.Count,toothID+" vertex/normal pairs");
				}
			}
		}

		[TestMethod]
		public void Import_MirrorsTeethNineToTwentyFourInX_WithoutReversingTheWinding() {
			int countMirroredPairs=0;
			foreach(string[] arrayIds in _dictFiles.Values) {
				ToothGraphic toothGraphicFirst=GetModel(arrayIds[0]);
				foreach(string toothID in arrayIds.Skip(1)) {
					ToothGraphic toothGraphic=GetModel(toothID);
					bool isMirroredPair=IsMirrored(toothID)!=IsMirrored(arrayIds[0]);
					float sign=isMirroredPair?-1:1;
					if(isMirroredPair) {
						countMirroredPairs++;
					}
					Assert.AreEqual(toothGraphicFirst.VertexNormals.Count,toothGraphic.VertexNormals.Count,toothID);
					for(int i=0;i<toothGraphic.VertexNormals.Count;i++) {
						VertexNormal vertexNormalFirst=toothGraphicFirst.VertexNormals[i];
						VertexNormal vertexNormal=toothGraphic.VertexNormals[i];
						Assert.AreEqual(sign*vertexNormalFirst.Vertex.X,vertexNormal.Vertex.X,toothID+" vertex "+i);
						Assert.AreEqual(vertexNormalFirst.Vertex.Y,vertexNormal.Vertex.Y,toothID+" vertex "+i);
						Assert.AreEqual(vertexNormalFirst.Vertex.Z,vertexNormal.Vertex.Z,toothID+" vertex "+i);
						Assert.AreEqual(sign*vertexNormalFirst.Normal.X,vertexNormal.Normal.X,toothID+" normal "+i);
						Assert.AreEqual(vertexNormalFirst.Normal.Y,vertexNormal.Normal.Y,toothID+" normal "+i);
						Assert.AreEqual(vertexNormalFirst.Normal.Z,vertexNormal.Normal.Z,toothID+" normal "+i);
					}
					//The faces keep the file's vertex order, so a mirrored tooth's triangles wind the other way round (ToothGraphic.cs, ImportObj).
					for(int g=0;g<toothGraphic.Groups.Count;g++) {
						CollectionAssert.AreEqual(toothGraphicFirst.Groups[g].GetTriangleIndices(),toothGraphic.Groups[g].GetTriangleIndices(),toothID+" group "+g);
					}
				}
			}
			Assert.AreEqual(14+2+5+5,countMirroredPairs,"Pairs of a mirrored and an unmirrored tooth: 14 permanent, 23/24 against 25/26 (2), 5 upper "
				+"primary (F-J against E-A) and 5 lower primary (K-O against T-P).");
			Assert.IsTrue(IsMirrored("9") && IsMirrored("24") && IsMirrored("F") && IsMirrored("O"));
			Assert.IsFalse(IsMirrored("8") || IsMirrored("25") || IsMirrored("E") || IsMirrored("P") || IsMirrored("implant"));
		}
		#endregion Import

		#region Triangulation
		[TestMethod]
		public void GetTriangleIndices_MakesAFanFromTheFirstVertexOfEachFace() {
			ToothGroup toothGroup=new ToothGroup();
			Face faceQuad=new Face();
			faceQuad.IndexList.AddRange(new[] { 10,11,12,13 });
			Face faceTriangle=new Face();
			faceTriangle.IndexList.AddRange(new[] { 20,21,22 });
			Face facePentagon=new Face();
			facePentagon.IndexList.AddRange(new[] { 1,2,3,4,5 });
			toothGroup.Faces.Add(faceQuad);
			toothGroup.Faces.Add(faceTriangle);
			toothGroup.Faces.Add(facePentagon);
			CollectionAssert.AreEqual(new[] { 10,11,12, 10,12,13, 20,21,22, 1,2,3, 1,3,4, 1,4,5 },toothGroup.GetTriangleIndices());
			Assert.AreEqual(0,new ToothGroup().GetTriangleIndices().Count);
		}

		[TestMethod]
		public void GetTriangleIndices_CountsMatchTheRecordedTriangles_AndIndexTheVertexNormals() {
			foreach(string line in _arrayModels) {
				string[] arrayFields=line.Split('\t');
				ToothGraphic toothGraphic=GetModel(arrayFields[0]);
				List<int> listAll=toothGraphic.Groups.SelectMany(x => x.GetTriangleIndices()).ToList();
				Assert.AreEqual(int.Parse(arrayFields[4])*3,listAll.Count,arrayFields[0]);
				Assert.IsTrue(listAll.All(x => x>=0 && x<toothGraphic.VertexNormals.Count),arrayFields[0]);
			}
		}
		#endregion Triangulation

		#region Mouse hit test
		[TestMethod]
		public void GetToothAtPoint_FindsEveryToothAtItsCentre_InTheFacialAndOcclusalViewsAndTheNumbers() {
			ToothChartData toothChartData=NewChartData();
			Assert.AreEqual(410f/130f,toothChartData.ScaleMmToPix,0.0001,"410x307 is (barely) not wide: the scale comes from the width.");
			for(int i=1;i<=32;i++) {
				string toothID=i.ToString();
				float x=ToothGraphic.GetDefaultOrthographicXpos(i);
				float sign=ToothGraphic.IsMaxillary(toothID)?1:-1;
				foreach(float y in new[] { toothChartData.GetTransYfacial(toothID),toothChartData.GetTransYocclusal(toothID),sign*2f }) {
					Assert.AreEqual(toothID,toothChartData.GetToothAtPoint(PointFromMm(toothChartData,x,y)),"tooth "+toothID+" at y "+y);
				}
			}
		}

		[TestMethod]
		public void GetToothAtPoint_GivesThePrimaryLetterOutsideThePermanentNumberBand() {
			ToothChartData toothChartData=NewChartData();
			foreach(int i in Enumerable.Range(4,10).Concat(Enumerable.Range(20,10))) {
				toothChartData.ListToothGraphics[i.ToString()].ShowPrimaryLetter=true;//as SetPrimary sets it
			}
			Assert.IsTrue(toothChartData.IsPermArea(5.1f) && toothChartData.IsPermArea(-4.4f));
			Assert.IsFalse(toothChartData.IsPermArea(5.2f) || toothChartData.IsPermArea(-4.5f));
			for(int i=1;i<=32;i++) {
				string toothID=i.ToString();
				float x=ToothGraphic.GetDefaultOrthographicXpos(i);
				float sign=ToothGraphic.IsMaxillary(toothID)?1:-1;
				string expectedOutside=Tooth.PermToPri(toothID)==""?toothID:Tooth.PermToPri(toothID);
				Assert.AreEqual(expectedOutside,toothChartData.GetToothAtPoint(PointFromMm(toothChartData,x,sign*8f)),"primary band at "+toothID);
				Assert.AreEqual(expectedOutside,toothChartData.GetToothAtPoint(PointFromMm(toothChartData,x,toothChartData.GetTransYfacial(toothID))),
					"facial view at "+toothID);
				Assert.AreEqual(toothID,toothChartData.GetToothAtPoint(PointFromMm(toothChartData,x,sign*2f)),"permanent number band at "+toothID);
			}
			//A hidden permanent number leaves only the letter, even in the permanent band; a hidden letter only the number.
			toothChartData.ListToothGraphics["6"].HideNumber=true;
			toothChartData.ListToothGraphics["L"].HideNumber=true;
			Assert.AreEqual("C",toothChartData.GetToothAtPoint(PointFromMm(toothChartData,ToothGraphic.GetDefaultOrthographicXpos(6),2f)));
			Assert.AreEqual("21",toothChartData.GetToothAtPoint(PointFromMm(toothChartData,ToothGraphic.GetDefaultOrthographicXpos(21),-8f)));
		}

		[TestMethod]
		public void GetToothAtPoint_TruncatesShiftM_PinnedAsToday() {
			//GetToothAtPoint moves each tooth's position by (int)ShiftM (ToothChartData.cs, the two "(int)ListToothGraphics[tooth_id].ShiftM"), not by
			//ShiftM as the drawing does. Pinned, not fixed: the WPF chart's mouse class must give the same teeth as today (TC-3).
			ToothChartData toothChartData=NewChartData();
			float x3=ToothGraphic.GetDefaultOrthographicXpos(3);
			Assert.AreEqual(8.5f,ToothGraphic.GetDefaultOrthographicXpos(4)-x3,0.0001);
			toothChartData.ListToothGraphics["3"].ShiftM=2.9f;
			//Truncated, tooth 3 sits at x3+2 and the point at x3+5.5 is 3.5 mm from it but 3.0 mm from tooth 4. With 2.9 mm it would be tooth 3.
			Assert.AreEqual("4",toothChartData.GetToothAtPoint(PointFromMm(toothChartData,x3+5.5f,toothChartData.GetTransYfacial("3"))));
			toothChartData.ListToothGraphics["3"].ShiftM=3f;
			Assert.AreEqual("3",toothChartData.GetToothAtPoint(PointFromMm(toothChartData,x3+5.5f,toothChartData.GetTransYfacial("3"))));
			//The same on the mandibular right, where mesial is toward the midline too.
			float x30=ToothGraphic.GetDefaultOrthographicXpos(30);
			Assert.AreEqual(9f,ToothGraphic.GetDefaultOrthographicXpos(29)-x30,0.0001);
			toothChartData.ListToothGraphics["30"].ShiftM=2.9f;
			Assert.AreEqual("29",toothChartData.GetToothAtPoint(PointFromMm(toothChartData,x30+5.75f,toothChartData.GetTransYfacial("30"))));
			//On the left side a mesial shift is toward the midline as well, so it is subtracted, and a distal -1.9 truncates to -1.
			float x14=ToothGraphic.GetDefaultOrthographicXpos(14);
			toothChartData.ListToothGraphics["14"].ShiftM=-1.9f;
			Assert.AreEqual(8.5f,ToothGraphic.GetDefaultOrthographicXpos(14)-ToothGraphic.GetDefaultOrthographicXpos(13),0.0001);
			//Tooth 14 truncated sits at x14+1: the point x14-3.5 is 4.5 mm from it and 5.0 mm from tooth 13. At x14+1.9 it would be tooth 13.
			Assert.AreEqual("14",toothChartData.GetToothAtPoint(PointFromMm(toothChartData,x14-3.5f,toothChartData.GetTransYfacial("14"))));
		}
		#endregion Mouse hit test

		#region Drag fill-in
		[TestMethod]
		public void GetAffectedTeeth_FillsInTheTeethADragSkipped() {
			ToothChartData toothChartData=NewChartData();
			CollectionAssert.AreEqual(new[] { "7","4","5","6" },toothChartData.GetAffectedTeeth("3","7",2f),"The end, then the teeth between in order.");
			CollectionAssert.AreEqual(new[] { "3","4","5","6" },toothChartData.GetAffectedTeeth("7","3",2f),"Backwards: the end, then the same order.");
			CollectionAssert.AreEqual(new[] { "4" },toothChartData.GetAffectedTeeth("3","4",2f),"Neighbours: only the end.");
			CollectionAssert.AreEqual(new[] { "30" },toothChartData.GetAffectedTeeth("3","30",0f),"Another arch: only the end.");
			CollectionAssert.AreEqual(new[] { "26","27","28","29" },toothChartData.GetAffectedTeeth("30","26",-2f));
			CollectionAssert.AreEqual(new[] { "10","8","9" },toothChartData.GetAffectedTeeth("7","10",2f),"Across the midline.");
			toothChartData.ListToothGraphics["5"].HideNumber=true;
			CollectionAssert.AreEqual(new[] { "7","4","6" },toothChartData.GetAffectedTeeth("3","7",2f),"A hidden number is skipped.");
		}

		[TestMethod]
		public void GetAffectedTeeth_TakesPrimaryLettersInThePrimaryBand() {
			ToothChartData toothChartData=NewChartData();
			foreach(string toothID in new[] { "4","5","6","7" }) {
				toothChartData.ListToothGraphics[toothID].ShowPrimaryLetter=true;
			}
			CollectionAssert.AreEqual(new[] { "D","A","B","C" },toothChartData.GetAffectedTeeth("3","D",8f),"Primary band: letters.");
			CollectionAssert.AreEqual(new[] { "7","4","5","6" },toothChartData.GetAffectedTeeth("3","7",2f),"Permanent band: numbers.");
			CollectionAssert.AreEqual(new[] { "3","4","5","6" },toothChartData.GetAffectedTeeth("D","3",2f),"A letter as the start counts as its tooth.");
			toothChartData.ListToothGraphics["B"].HideNumber=true;
			CollectionAssert.AreEqual(new[] { "D","A","5","C" },toothChartData.GetAffectedTeeth("3","D",8f),"A hidden letter gives its number.");
		}
		#endregion Drag fill-in

		#region Perio geometry
		[TestMethod]
		public void Perio_FurcationPositions_AreRecorded() {
			ToothChartData toothChartData=NewChartData();
			AssertPoint(3.8f,9.5f,toothChartData.GetFurcationPos(3,PerioSurf.MB),"3 MB: 0.38 of 10 mm, mesial is toward the midline");
			AssertPoint(-3.8f,9.5f,toothChartData.GetFurcationPos(3,PerioSurf.DB),"3 DB");
			AssertPoint(0,9.5f,toothChartData.GetFurcationPos(3,PerioSurf.B),"3 B");
			AssertPoint(-3.8f,9.5f,toothChartData.GetFurcationPos(14,PerioSurf.MB),"14 MB, mirrored");
			AssertPoint(-2.88f,9.5f,toothChartData.GetFurcationPos(2,PerioSurf.DL),"2 DL: 0.32 of 9 mm");
			AssertPoint(-4.07f,-9.5f,toothChartData.GetFurcationPos(30,PerioSurf.DB),"30 DB: 0.37 of 11 mm, mandibular");
			AssertPoint(-4.07f,-9.5f,toothChartData.GetFurcationPos(19,PerioSurf.ML),"19 ML");
			AssertPoint(3.675f,-9.5f,toothChartData.GetFurcationPos(31,PerioSurf.MB),"31 MB: 0.35 of 10.5 mm");
			AssertPoint(1.96f,9.5f,toothChartData.GetFurcationPos(4,PerioSurf.MB),"4 MB: 0.28 of 7 mm");
			toothChartData.ListToothGraphics["19"].IsImplant=true;
			AssertPoint(-2f,-9.5f,toothChartData.GetFurcationPos(19,PerioSurf.ML),"An implant's sites are 2 mm from its centre.");
			AddMeasure(toothChartData,3,PerioSequenceType.Furcation,2,-1,3,0,1,-1);
			Assert.AreEqual(2,toothChartData.GetFurcationValue(3,PerioSurf.MB));
			Assert.AreEqual(0,toothChartData.GetFurcationValue(3,PerioSurf.B),"-1 (no measurement) reads as 0.");
			Assert.AreEqual(3,toothChartData.GetFurcationValue(3,PerioSurf.DB));
			Assert.AreEqual(1,toothChartData.GetFurcationValue(3,PerioSurf.L));
			Assert.AreEqual(0,toothChartData.GetFurcationValue(14,PerioSurf.MB),"No measure for the tooth.");
		}

		[TestMethod]
		public void Perio_ProbingBars_AreRecorded() {
			ToothChartData toothChartData=NewChartData();
			toothChartData.ColorProbing=Color.Green;
			toothChartData.ColorProbingRed=Color.Red;
			toothChartData.RedLimitProbing=4;
			AddMeasure(toothChartData,3,PerioSequenceType.Probing,5,3,0,-1,2,4);
			AddMeasure(toothChartData,3,PerioSequenceType.GingMargin,2,103,0,0,-1,1);
			AddMeasure(toothChartData,30,PerioSequenceType.Probing,4,2,6,1,1,1);
			AddMeasure(toothChartData,30,PerioSequenceType.GingMargin,1,0,2,0,0,0);
			LineSimple line=toothChartData.GetProbingLine(3,PerioSurf.MB,out Color color);
			Assert.AreEqual(2,line.Vertices.Count);
			AssertPoint(3.8f,2f,new PointF(line.Vertices[0].X,line.Vertices[0].Y),"3 MB from the gingival margin");
			AssertPoint(3.8f,7f,new PointF(line.Vertices[1].X,line.Vertices[1].Y),"3 MB down the pocket");
			Assert.AreEqual(Color.Red,color,"5 mm is at or past the red limit of 4.");
			line=toothChartData.GetProbingLine(3,PerioSurf.B,out color);
			AssertPoint(0,-3f,new PointF(line.Vertices[0].X,line.Vertices[0].Y),"103 is 3 mm of hyperplasia");
			AssertPoint(0,0,new PointF(line.Vertices[1].X,line.Vertices[1].Y),"3 B");
			Assert.AreEqual(Color.Green,color);
			Assert.IsNull(toothChartData.GetProbingLine(3,PerioSurf.DB,out color),"A depth of 0 draws nothing.");
			Assert.IsNull(toothChartData.GetProbingLine(3,PerioSurf.ML,out color),"-1 draws nothing.");
			line=toothChartData.GetProbingLine(3,PerioSurf.L,out color);
			AssertPoint(0,0,new PointF(line.Vertices[0].X,line.Vertices[0].Y),"-1 gingival margin reads as 0");
			AssertPoint(0,2,new PointF(line.Vertices[1].X,line.Vertices[1].Y),"3 L");
			line=toothChartData.GetProbingLine(30,PerioSurf.DB,out color);
			AssertPoint(-4.07f,-2f,new PointF(line.Vertices[0].X,line.Vertices[0].Y),"mandibular bars run down");
			AssertPoint(-4.07f,-8f,new PointF(line.Vertices[1].X,line.Vertices[1].Y),"30 DB");
			Assert.AreEqual(Color.Red,color);
			toothChartData.ListToothGraphics["30"].Visible=false;
			Assert.IsNull(toothChartData.GetProbingLine(30,PerioSurf.DB,out color),"A missing tooth draws no bars.");
			toothChartData.ListToothGraphics["30"].IsImplant=true;
			line=toothChartData.GetProbingLine(30,PerioSurf.DB,out color);
			AssertPoint(-2f,-2f,new PointF(line.Vertices[0].X,line.Vertices[0].Y),"An implant's bars are 2 mm from its centre.");
		}

		[TestMethod]
		public void Perio_BleedingAndSuppurationDroplets_AreRecorded() {
			ToothChartData toothChartData=NewChartData();
			AddMeasure(toothChartData,3,PerioSequenceType.BleedSupPlaqCalc,(int)BleedingFlags.Blood,(int)BleedingFlags.Suppuration,
				(int)(BleedingFlags.Blood|BleedingFlags.Suppuration|BleedingFlags.Plaque),0,-1,(int)BleedingFlags.Calculus);
			AddMeasure(toothChartData,30,PerioSequenceType.BleedSupPlaqCalc,(int)BleedingFlags.Blood,0,0,0,0,0);
			AssertPoint(3.5f,-1.5f,toothChartData.GetBleedingOrSuppuration(3,PerioSurf.MB,true),"bleeding: 0.3 mm left of the site");
			AssertPoint(0,0,toothChartData.GetBleedingOrSuppuration(3,PerioSurf.MB,false),"no suppuration: 0,0");
			AssertPoint(0.3f,-1.5f,toothChartData.GetBleedingOrSuppuration(3,PerioSurf.B,false),"suppuration: 0.3 mm right of the site");
			AssertPoint(-4.1f,-1.5f,toothChartData.GetBleedingOrSuppuration(3,PerioSurf.DB,true),"3 DB both");
			AssertPoint(-3.5f,-1.5f,toothChartData.GetBleedingOrSuppuration(3,PerioSurf.DB,false),"3 DB both");
			AssertPoint(0,0,toothChartData.GetBleedingOrSuppuration(3,PerioSurf.DL,true),"calculus alone draws nothing");
			AssertPoint(3.77f,1.5f,toothChartData.GetBleedingOrSuppuration(30,PerioSurf.MB,true),"mandibular droplets sit 1.5 mm up");
			Vertex3f[] arrayDroplet=toothChartData.GetDropletVertices();
			Assert.AreEqual(5,arrayDroplet.Length,"A pentagon; with 0,0 it makes five triangles.");
			float[][] arrayExpected={ new[] { 0f,.89f },new[] { .34f,.049f },new[] { .21f,-.35f },new[] { -.21f,-.35f },new[] { -.34f,.049f } };
			for(int i=0;i<5;i++) {
				Assert.AreEqual(arrayExpected[i][0],arrayDroplet[i].X);
				Assert.AreEqual(arrayExpected[i][1],arrayDroplet[i].Y);
				Assert.AreEqual(0f,arrayDroplet[i].Z);
			}
		}

		[TestMethod]
		public void Perio_HorizontalLines_BreakAtMissingValuesAndTeeth() {
			ToothChartData toothChartData=NewChartData();
			AddMeasure(toothChartData,1,PerioSequenceType.GingMargin,1,1,1,-1,-1,-1);
			AddMeasure(toothChartData,2,PerioSequenceType.GingMargin,2,-1,2,-1,-1,-1);
			AddMeasure(toothChartData,3,PerioSequenceType.GingMargin,0,102,0,-1,-1,-1);
			AddMeasure(toothChartData,3,PerioSequenceType.MGJ,3,3,3,-1,-1,-1);
			List<LineSimple> listLines=toothChartData.GetHorizontalLines(PerioSequenceType.GingMargin,true,true);
			//Maxillary buccal runs from tooth 1 to 16, distal to mesial on the right: tooth 1 DB, B, MB, then tooth 2 DB; tooth 2's B has no value, so
			//a new line starts at its MB and runs through tooth 3; tooth 4 has none, which ends it.
			Assert.AreEqual(2,listLines.Count);
			Assert.AreEqual(4,listLines[0].Vertices.Count);
			Assert.AreEqual(4,listLines[1].Vertices.Count);
			float x1=ToothGraphic.GetDefaultOrthographicXpos(1);
			float x2=ToothGraphic.GetDefaultOrthographicXpos(2);
			float x3=ToothGraphic.GetDefaultOrthographicXpos(3);
			AssertPoint(x1-0.32f*8.5f,1,new PointF(listLines[0].Vertices[0].X,listLines[0].Vertices[0].Y),"1 DB");
			AssertPoint(x2-0.32f*9f,2,new PointF(listLines[0].Vertices[3].X,listLines[0].Vertices[3].Y),"2 DB");
			AssertPoint(x2+0.32f*9f,2,new PointF(listLines[1].Vertices[0].X,listLines[1].Vertices[0].Y),"2 MB");
			AssertPoint(x3,-2,new PointF(listLines[1].Vertices[2].X,listLines[1].Vertices[2].Y),"3 B: 102 is 2 mm of hyperplasia");
			//The mucogingival junction is measured from the gingival margin: 3 + (-2) at 3 B, 3 + 0 at the others.
			List<LineSimple> listMgj=toothChartData.GetHorizontalLines(PerioSequenceType.MGJ,true,true);
			Assert.AreEqual(1,listMgj.Count);
			CollectionAssert.AreEqual(new[] { 3f,1f,3f },listMgj[0].Vertices.Select(x => x.Y).ToList());
			//A missing tooth breaks the line, and a line needs two points.
			toothChartData.ListToothGraphics["2"].Visible=false;
			listLines=toothChartData.GetHorizontalLines(PerioSequenceType.GingMargin,true,true);
			Assert.AreEqual(2,listLines.Count);
			Assert.AreEqual(3,listLines[0].Vertices.Count,"tooth 1 alone");
			Assert.AreEqual(3,listLines[1].Vertices.Count,"tooth 3 alone");
			Assert.AreEqual(0,toothChartData.GetHorizontalLines(PerioSequenceType.GingMargin,true,false).Count,"No lingual values.");
		}
		#endregion Perio geometry

		[TestMethod]
		public void ModelFiles_DoNotNameSharpDX() {
			//TC-0's cut: the model holds no device objects, so the WPF chart can use it and TC-9a can delete the DirectX engine without touching it.
			foreach(string fileName in new[] { "ToothChartData.cs","ToothGraphic.cs","ToothGroup.cs","ToothGraphicCollection.cs","Face.cs","Line.cs",
				"Vertex3f.cs","VertexNormal.cs" })
			{
				string source=TestPaths.ReadSource("SparksToothChart/"+fileName);
				Assert.IsFalse(Regex.IsMatch(source,@"\bSharpDX\b"),fileName+" names SharpDX.");
			}
		}
	}
}
