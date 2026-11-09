using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace SparksToothChart.Wpf3D {
	///<summary>The winding rule of the WPF chart's meshes (docs/specs/wpf-tooth-chart-plan.md, section 5.2), with no WPF object. WPF draws only the
	///front faces of a mesh: triangles that run counter-clockwise as seen from the camera. The models' triangles are the DirectX chart's (the fan of
	///ToothGroup.GetTriangleIndices), and teeth 9-24 are the models of 1-8 and 25-32 mirrored in X when they are loaded, with their normals mirrored
	///but their winding not reversed (ToothGraphic.ImportObj). So each triangle is turned to agree with its vertex normals: when its geometric normal
	///((b-a) x (c-a)) points away from the sum of its three vertex normals, b and c swap. That reverses the mirrored teeth, and a triangle whose
	///normals face the camera then runs counter-clockwise and is kept. A mirroring model transform (negative determinant: the lingual perio rows)
	///reverses the winding on screen again, so such an instance draws the reversed list.</summary>
	public static class ToothMeshWinding {
		///<summary>The group's triangles as three indices into the tooth's VertexNormals each, every triangle turned to agree with its vertex normals.
		///A triangle with no area keeps its order.</summary>
		public static int[] GetWoundTriangles(ToothGraphic toothGraphic,int indexGroup) {
			List<int> listTriangles=toothGraphic.Groups[indexGroup].GetTriangleIndices();
			int[] arrayTriangles=listTriangles.ToArray();
			for(int i=0;i+2<arrayTriangles.Length;i+=3) {
				if(IsAgainstNormals(toothGraphic,arrayTriangles[i],arrayTriangles[i+1],arrayTriangles[i+2])) {
					int b=arrayTriangles[i+1];
					arrayTriangles[i+1]=arrayTriangles[i+2];
					arrayTriangles[i+2]=b;
				}
			}
			return arrayTriangles;
		}

		///<summary>Every triangle with its last two corners swapped: the same triangles, wound the other way.</summary>
		public static int[] GetReversedTriangles(int[] arrayTriangles) {
			int[] arrayReversed=(int[])arrayTriangles.Clone();
			for(int i=0;i+2<arrayReversed.Length;i+=3) {
				arrayReversed[i+1]=arrayTriangles[i+2];
				arrayReversed[i+2]=arrayTriangles[i+1];
			}
			return arrayReversed;
		}

		///<summary>True when the triangle's geometric normal points away from the sum of its vertex normals (negative dot product).</summary>
		public static bool IsAgainstNormals(ToothGraphic toothGraphic,int a,int b,int c) {
			double dot=GetNormalAgreement(toothGraphic,a,b,c);
			return dot<0;
		}

		///<summary>The dot product of the triangle's geometric normal ((b-a) x (c-a)) and the sum of its three vertex normals: positive when they
		///agree, zero for a triangle with no area.</summary>
		public static double GetNormalAgreement(ToothGraphic toothGraphic,int a,int b,int c) {
			Vertex3f pa=toothGraphic.VertexNormals[a].Vertex;
			Vertex3f pb=toothGraphic.VertexNormals[b].Vertex;
			Vertex3f pc=toothGraphic.VertexNormals[c].Vertex;
			double abx=(double)pb.X-pa.X;
			double aby=(double)pb.Y-pa.Y;
			double abz=(double)pb.Z-pa.Z;
			double acx=(double)pc.X-pa.X;
			double acy=(double)pc.Y-pa.Y;
			double acz=(double)pc.Z-pa.Z;
			double nx=aby*acz-abz*acy;
			double ny=abz*acx-abx*acz;
			double nz=abx*acy-aby*acx;
			Vertex3f na=toothGraphic.VertexNormals[a].Normal;
			Vertex3f nb=toothGraphic.VertexNormals[b].Normal;
			Vertex3f nc=toothGraphic.VertexNormals[c].Normal;
			double sx=(double)na.X+nb.X+nc.X;
			double sy=(double)na.Y+nb.Y+nc.Y;
			double sz=(double)na.Z+nb.Z+nc.Z;
			return nx*sx+ny*sy+nz*sz;
		}
	}

	///<summary>One group of a model as frozen WPF meshes: the triangles of ToothMeshWinding, and the same triangles reversed for mirroring
	///transforms. Each mesh holds only the vertices its triangles use (position and normal of VertexNormals, unchanged), because WPF lights every
	///vertex of a mesh for every material pass; ArraySourceIndices maps a mesh vertex back to its VertexNormals index.</summary>
	public sealed class ToothGroupMesh {
		public readonly ToothGroupType GroupType;
		///<summary>For each mesh vertex, its index in the tooth's VertexNormals.</summary>
		public readonly int[] ArraySourceIndices;
		///<summary>The triangles wound to agree with the vertex normals.</summary>
		public readonly MeshGeometry3D Mesh;
		///<summary>The same positions and normals with every triangle reversed.</summary>
		public readonly MeshGeometry3D MeshReversed;

		public ToothGroupMesh(ToothGroupType groupType,int[] arraySourceIndices,MeshGeometry3D mesh,MeshGeometry3D meshReversed) {
			GroupType=groupType;
			ArraySourceIndices=arraySourceIndices;
			Mesh=mesh;
			MeshReversed=meshReversed;
		}
	}

	///<summary>The frozen meshes of one model (a tooth ID or "implant").</summary>
	public sealed class ToothMeshSet {
		public readonly string ToothId;
		///<summary>The number of the model's VertexNormals: the positions the meshes take their vertices from, one for one.</summary>
		public readonly int CountVertexNormals;
		///<summary>One per group, in the order of ToothGraphic.Groups.</summary>
		public readonly ToothGroupMesh[] ArrayGroups;

		public ToothMeshSet(string toothId,int countVertexNormals,ToothGroupMesh[] arrayGroups) {
			ToothId=toothId;
			CountVertexNormals=countVertexNormals;
			ArrayGroups=arrayGroups;
		}
	}

	///<summary>The WPF chart's meshes, built from the embedded .obj models once per process on the render thread and frozen, so they can be used
	///from any thread (docs/specs/wpf-tooth-chart-plan.md, sections 5.2 and 5.4). Each model is loaded by its own ToothGraphic, made here, so the
	///render thread never reads a chart's ToothChartData; the scene's 3D items name a model by tooth ID and group index, and the groups come in the
	///same order from the same file. Not thread-safe: one cache belongs to one thread (the renderer's).</summary>
	public sealed class ToothMeshCache {
		private readonly Dictionary<string,ToothMeshSet> _dictMeshSets=new Dictionary<string,ToothMeshSet>();

		///<summary>The meshes of the model of toothId (1-32, A-T or "implant"), loaded and frozen the first time.</summary>
		public ToothMeshSet GetMeshSet(string toothId) {
			ToothMeshSet toothMeshSet;
			if(_dictMeshSets.TryGetValue(toothId,out toothMeshSet)) {
				return toothMeshSet;
			}
			toothMeshSet=BuildMeshSet(new ToothGraphic(toothId));
			_dictMeshSets[toothId]=toothMeshSet;
			return toothMeshSet;
		}

		///<summary>The mesh of one group, reversed or not. Null when the model has no such group.</summary>
		public MeshGeometry3D GetMesh(string toothId,int indexGroup,bool isReversed) {
			ToothMeshSet toothMeshSet=GetMeshSet(toothId);
			if(indexGroup<0 || indexGroup>=toothMeshSet.ArrayGroups.Length) {
				return null;
			}
			ToothGroupMesh toothGroupMesh=toothMeshSet.ArrayGroups[indexGroup];
			return isReversed?toothGroupMesh.MeshReversed:toothGroupMesh.Mesh;
		}

		///<summary>The number of models loaded so far.</summary>
		public int CountMeshSets {
			get {
				return _dictMeshSets.Count;
			}
		}

		///<summary>The frozen meshes of every group of the model.</summary>
		public static ToothMeshSet BuildMeshSet(ToothGraphic toothGraphic) {
			ToothGroupMesh[] arrayGroups=new ToothGroupMesh[toothGraphic.Groups.Count];
			for(int g=0;g<toothGraphic.Groups.Count;g++) {
				arrayGroups[g]=BuildGroupMesh(toothGraphic,g);
			}
			return new ToothMeshSet(toothGraphic.ToothID,toothGraphic.VertexNormals.Count,arrayGroups);
		}

		private static ToothGroupMesh BuildGroupMesh(ToothGraphic toothGraphic,int indexGroup) {
			int[] arrayWound=ToothMeshWinding.GetWoundTriangles(toothGraphic,indexGroup);
			//The vertices the triangles use, in the order of first use.
			Dictionary<int,int> dictLocal=new Dictionary<int,int>();
			List<int> listSource=new List<int>();
			int[] arrayLocal=new int[arrayWound.Length];
			for(int i=0;i<arrayWound.Length;i++) {
				int indexLocal;
				if(!dictLocal.TryGetValue(arrayWound[i],out indexLocal)) {
					indexLocal=listSource.Count;
					dictLocal[arrayWound[i]]=indexLocal;
					listSource.Add(arrayWound[i]);
				}
				arrayLocal[i]=indexLocal;
			}
			Point3DCollection point3DCollection=new Point3DCollection(listSource.Count);
			Vector3DCollection vector3DCollection=new Vector3DCollection(listSource.Count);
			for(int i=0;i<listSource.Count;i++) {
				VertexNormal vertexNormal=toothGraphic.VertexNormals[listSource[i]];
				point3DCollection.Add(new Point3D(vertexNormal.Vertex.X,vertexNormal.Vertex.Y,vertexNormal.Vertex.Z));
				vector3DCollection.Add(new Vector3D(vertexNormal.Normal.X,vertexNormal.Normal.Y,vertexNormal.Normal.Z));
			}
			point3DCollection.Freeze();
			vector3DCollection.Freeze();
			MeshGeometry3D mesh=CreateMesh(point3DCollection,vector3DCollection,arrayLocal);
			MeshGeometry3D meshReversed=CreateMesh(point3DCollection,vector3DCollection,ToothMeshWinding.GetReversedTriangles(arrayLocal));
			return new ToothGroupMesh(toothGraphic.Groups[indexGroup].GroupType,listSource.ToArray(),mesh,meshReversed);
		}

		private static MeshGeometry3D CreateMesh(Point3DCollection point3DCollection,Vector3DCollection vector3DCollection,int[] arrayTriangles) {
			MeshGeometry3D mesh=new MeshGeometry3D();
			mesh.Positions=point3DCollection;
			mesh.Normals=vector3DCollection;
			Int32Collection int32Collection=new Int32Collection(arrayTriangles);
			int32Collection.Freeze();
			mesh.TriangleIndices=int32Collection;
			mesh.Freeze();
			return mesh;
		}
	}
}
