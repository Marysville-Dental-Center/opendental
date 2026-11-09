using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace SparksToothChart {
	///<summary>For 3D tooth graphics, this is a group of faces within a single tooth.  Different groups can be assigned different colors and visibility.  Groups might include enamel, the filling surfaces, pulp, canals, and cementum.  If a group does not apply, such as a F on a posterior tooth, then that tooth will not have that group.  The code must be resiliant enough to handle missing groups.  We might add more groups later and subdivide existing groups.  For instance pits, grooves, cusps, cervical areas, etc.  Over the years, this could get quite extensive and complex.  We would have to add a table to the database to handle additional groups painted by user.</summary>
	public class ToothGroup {
		///<summary></summary>
		public bool Visible;
		///<summary></summary>
		public Color PaintColor;
		///<summary></summary>
		public ToothGroupType GroupType;
		///<summary>dim 1=the face. dim 2=the vertex. dim 3 always has length=2, with 1st vertex, and 2nd normal.</summary>
		public List<Face> Faces;

		public ToothGroup() {
			Faces=new List<Face>();
		}

		///<summary>A copy of the group and its faces. A graphics engine keeps its own buffers for the group (the DirectX chart keys them by tooth and
		///group), so nothing else needs to be made for the copy.</summary>
		public ToothGroup Copy() {
			ToothGroup tg=new ToothGroup();
			tg.Visible=this.Visible;
			tg.PaintColor=this.PaintColor;
			tg.GroupType=this.GroupType;
			tg.Faces=new List<Face>();
			for(int i=0;i<this.Faces.Count;i++) {
				tg.Faces.Add(this.Faces[i].Copy());
			}
			return tg;
		}

		///<summary>The group's faces as a triangle list: three indices into the tooth's VertexNormals per triangle. Graphics engines draw triangles, not
		///polygons, so every face becomes a fan from its first vertex, which keeps the winding (the normal's direction) of the polygon: a quad
		///0,1,2,3 gives 0,1,2 and 0,2,3. The DirectX chart draws these, and the WPF chart's mesh cache builds its meshes from them.</summary>
		public List<int> GetTriangleIndices() {
			List<int> listTriangles=new List<int>();
			for(int i=0;i<Faces.Count;i++) {
				for(int j=1;j<Faces[i].IndexList.Count-1;j++) {
					listTriangles.Add(Faces[i].IndexList[0]);
					listTriangles.Add(Faces[i].IndexList[j]);
					listTriangles.Add(Faces[i].IndexList[j+1]);
				}
			}
			return listTriangles;
		}

		public override string ToString() {
			return GroupType.ToString()+". Faces:"+Faces.Count.ToString();
		}


	}

	///<summary></summary>
	public enum ToothGroupType{
		///<summary>0- This is areas of the tooth that are not included in any fillings at all.</summary>
		Enamel,
		///<summary>1</summary>
		Cementum,
		///<summary>2</summary>
		M,
		///<summary>3</summary>
		O,
		///<summary>4</summary>
		D,
		///<summary>5</summary>
		B,
		///<summary>6</summary>
		L,
		///<summary>7</summary>
		F,
		///<summary>8</summary>
		I,
		///<summary>9. class V. In addition to B or F</summary>
		V,
		///<summary>10. The pulp chamber and post or buildup.  Because our teeth are not yet transparent, these faces need to be translated forward.</summary>
		Buildup,
		///<summary>11. Only defined on some anterior teeth.  The small bit of enamel on the F that is not included in any filling.</summary>
		EnamelF,
		/// <summary>12. Only defined on some anterior teeth.  The F portion of the D filling.</summary>
		DF,
		/// <summary>13. Only defined on some anterior teeth.  The F portion of the M filling.</summary>
		MF,
		/// <summary>14. Only defined on some anterior teeth.  The F portion of the I filling.</summary>
		IF,
		///<summary>Only present in the special implant tooth object.</summary>
		Implant,
		///<summary>Not used. Just a placeholder</summary>
		Canals,
		///<summary>Used where there are unknown materials present such as 'default'.  Always ignore these groups.</summary>
		None
	}
}
