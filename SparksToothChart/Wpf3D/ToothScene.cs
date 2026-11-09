using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Globalization;
using System.Text;

namespace SparksToothChart.Wpf3D {
	///<summary>A 4x4 matrix of floats for row vectors (a point is [x y z 1]*M), with the formulas of the DirectX chart's math library
	///(SharpDX.Matrix and D3DX): Translation, Scaling and RotationX/Y/Z build the same matrices, and Multiply adds the four products of each
	///element in the same order, so the scene's model transforms are the DirectX chart's world matrices without its handedness flip, float for
	///float. A plain value with no WPF type, so a scene holds no WPF object; the renderer converts it to a Matrix3D, whose convention is the same.</summary>
	public struct ChartMatrix {
		public float M11,M12,M13,M14;
		public float M21,M22,M23,M24;
		public float M31,M32,M33,M34;
		public float M41,M42,M43,M44;

		public static ChartMatrix Identity {
			get {
				ChartMatrix matrix=new ChartMatrix();
				matrix.M11=1;
				matrix.M22=1;
				matrix.M33=1;
				matrix.M44=1;
				return matrix;
			}
		}

		public static ChartMatrix Translation(float x,float y,float z) {
			ChartMatrix matrix=Identity;
			matrix.M41=x;
			matrix.M42=y;
			matrix.M43=z;
			return matrix;
		}

		public static ChartMatrix Scaling(float x,float y,float z) {
			ChartMatrix matrix=Identity;
			matrix.M11=x;
			matrix.M22=y;
			matrix.M33=z;
			return matrix;
		}

		///<summary>SharpDX.Matrix.RotationX: M22=cos, M23=sin, M32=-sin, M33=cos, with cos and sin as floats.</summary>
		public static ChartMatrix RotationX(float angle) {
			float cos=(float)Math.Cos(angle);
			float sin=(float)Math.Sin(angle);
			ChartMatrix matrix=Identity;
			matrix.M22=cos;
			matrix.M23=sin;
			matrix.M32=-sin;
			matrix.M33=cos;
			return matrix;
		}

		///<summary>SharpDX.Matrix.RotationY: M11=cos, M13=-sin, M31=sin, M33=cos.</summary>
		public static ChartMatrix RotationY(float angle) {
			float cos=(float)Math.Cos(angle);
			float sin=(float)Math.Sin(angle);
			ChartMatrix matrix=Identity;
			matrix.M11=cos;
			matrix.M13=-sin;
			matrix.M31=sin;
			matrix.M33=cos;
			return matrix;
		}

		///<summary>SharpDX.Matrix.RotationZ: M11=cos, M12=sin, M21=-sin, M22=cos.</summary>
		public static ChartMatrix RotationZ(float angle) {
			float cos=(float)Math.Cos(angle);
			float sin=(float)Math.Sin(angle);
			ChartMatrix matrix=Identity;
			matrix.M11=cos;
			matrix.M12=sin;
			matrix.M21=-sin;
			matrix.M22=cos;
			return matrix;
		}

		///<summary>left*right: left's transform first, then right's (row vectors).</summary>
		public static ChartMatrix operator *(ChartMatrix left,ChartMatrix right) {
			ChartMatrix m=new ChartMatrix();
			m.M11=(left.M11*right.M11)+(left.M12*right.M21)+(left.M13*right.M31)+(left.M14*right.M41);
			m.M12=(left.M11*right.M12)+(left.M12*right.M22)+(left.M13*right.M32)+(left.M14*right.M42);
			m.M13=(left.M11*right.M13)+(left.M12*right.M23)+(left.M13*right.M33)+(left.M14*right.M43);
			m.M14=(left.M11*right.M14)+(left.M12*right.M24)+(left.M13*right.M34)+(left.M14*right.M44);
			m.M21=(left.M21*right.M11)+(left.M22*right.M21)+(left.M23*right.M31)+(left.M24*right.M41);
			m.M22=(left.M21*right.M12)+(left.M22*right.M22)+(left.M23*right.M32)+(left.M24*right.M42);
			m.M23=(left.M21*right.M13)+(left.M22*right.M23)+(left.M23*right.M33)+(left.M24*right.M43);
			m.M24=(left.M21*right.M14)+(left.M22*right.M24)+(left.M23*right.M34)+(left.M24*right.M44);
			m.M31=(left.M31*right.M11)+(left.M32*right.M21)+(left.M33*right.M31)+(left.M34*right.M41);
			m.M32=(left.M31*right.M12)+(left.M32*right.M22)+(left.M33*right.M32)+(left.M34*right.M42);
			m.M33=(left.M31*right.M13)+(left.M32*right.M23)+(left.M33*right.M33)+(left.M34*right.M43);
			m.M34=(left.M31*right.M14)+(left.M32*right.M24)+(left.M33*right.M34)+(left.M34*right.M44);
			m.M41=(left.M41*right.M11)+(left.M42*right.M21)+(left.M43*right.M31)+(left.M44*right.M41);
			m.M42=(left.M41*right.M12)+(left.M42*right.M22)+(left.M43*right.M32)+(left.M44*right.M42);
			m.M43=(left.M41*right.M13)+(left.M42*right.M23)+(left.M43*right.M33)+(left.M44*right.M43);
			m.M44=(left.M41*right.M14)+(left.M42*right.M24)+(left.M43*right.M34)+(left.M44*right.M44);
			return m;
		}

		///<summary>The point [x y z 1]*M, divided by w as SharpDX's Vector3.TransformCoordinate divides (1/w, then each product).</summary>
		public void TransformCoordinate(float x,float y,float z,out float xOut,out float yOut,out float zOut) {
			float xw=(x*M11)+(y*M21)+(z*M31)+M41;
			float yw=(x*M12)+(y*M22)+(z*M32)+M42;
			float zw=(x*M13)+(y*M23)+(z*M33)+M43;
			float w=1f/((x*M14)+(y*M24)+(z*M34)+M44);
			xOut=xw*w;
			yOut=yw*w;
			zOut=zw*w;
		}

		///<summary>The direction [x y z 0]*M: no translation.</summary>
		public void TransformNormalVector(float x,float y,float z,out float xOut,out float yOut,out float zOut) {
			xOut=(x*M11)+(y*M21)+(z*M31);
			yOut=(x*M12)+(y*M22)+(z*M32);
			zOut=(x*M13)+(y*M23)+(z*M33);
		}

		///<summary>The determinant of the upper left 3x3 part. Negative when the transform mirrors, which reverses the winding of every triangle.</summary>
		public double Determinant3x3 {
			get {
				return (double)M11*((double)M22*M33-(double)M23*M32)
					-(double)M12*((double)M21*M33-(double)M23*M31)
					+(double)M13*((double)M21*M32-(double)M22*M31);
			}
		}

		public bool IsEqualTo(ChartMatrix other) {
			return M11==other.M11 && M12==other.M12 && M13==other.M13 && M14==other.M14
				&& M21==other.M21 && M22==other.M22 && M23==other.M23 && M24==other.M24
				&& M31==other.M31 && M32==other.M32 && M33==other.M33 && M34==other.M34
				&& M41==other.M41 && M42==other.M42 && M43==other.M43 && M44==other.M44;
		}

		public override string ToString() {
			CultureInfo culture=CultureInfo.InvariantCulture;
			return string.Format(culture,"[{0} {1} {2} {3}; {4} {5} {6} {7}; {8} {9} {10} {11}; {12} {13} {14} {15}]",
				M11,M12,M13,M14,M21,M22,M23,M24,M31,M32,M33,M34,M41,M42,M43,M44);
		}
	}

	///<summary>One tooth group drawn in the 3D layer: which mesh (the model of a tooth ID and the index of the group in its ToothGraphic.Groups,
	///the key of ToothMeshCache), with which colour, specular value and model transform, and with which winding. Immutable.</summary>
	public sealed class ToothSceneItem3D {
		///<summary>The model's tooth ID: 1-32, A-T or "implant".</summary>
		public readonly string ToothId;
		///<summary>The index of the group in the model's ToothGraphic.Groups.</summary>
		public readonly int GroupIndex;
		public readonly ToothGroupType GroupType;
		///<summary>As the builder gives it, which is always opaque (alpha 255): the DirectX chart draws with blending off, and WPF does not sort
		///transparent models. The item keeps the colour as given, so the scene tests guard the builder's colour paths (ToothSceneBuilder.GetGroupColor
		///and GetImplantColor) instead of a clamp here.</summary>
		public readonly Color Color;
		///<summary>The specular value of the group's material, 0 to 1 (DirectX's GetGroupMaterial rule).</summary>
		public readonly float Specular;
		///<summary>The model transform: the DirectX world matrix without its handedness flip (Scaling(1,1,-1)) and its shift of 400 mm, which the
		///camera replaces.</summary>
		public readonly ChartMatrix Transform;
		///<summary>True when Transform mirrors (negative determinant): the mesh's reversed triangle list is drawn, so its front faces still face the
		///camera.</summary>
		public readonly bool IsReversed;
		///<summary>What the item is, for tests and messages: for example "8 facial" or "implant of 3".</summary>
		public readonly string Source;

		public ToothSceneItem3D(string toothId,int groupIndex,ToothGroupType groupType,Color color,float specular,ChartMatrix transform,string source) {
			ToothId=toothId;
			GroupIndex=groupIndex;
			GroupType=groupType;
			Color=color;
			Specular=specular;
			Transform=transform;
			IsReversed=transform.Determinant3x3<0;
			Source=source;
		}

		public bool IsEqualTo(ToothSceneItem3D other) {
			return ToothId==other.ToothId && GroupIndex==other.GroupIndex && GroupType==other.GroupType && Color.ToArgb()==other.Color.ToArgb()
				&& Specular==other.Specular && Transform.IsEqualTo(other.Transform) && IsReversed==other.IsReversed;
		}

		public override string ToString() {
			return Source+": "+ToothId+" group "+GroupIndex+" ("+GroupType+"), #"+Color.ToArgb().ToString("X8")+(IsReversed?", reversed":"");
		}
	}

	///<summary>What a 2D layer item draws.</summary>
	public enum ChartItem2DKind {
		///<summary>Filled polygons in one colour, with the Nonzero rule: lines, the X, the build-up silhouette, rectangles.</summary>
		Polygons,
		///<summary>One line of text.</summary>
		Text,
	}

	///<summary>One item of the 2D layer, in pixels of the image (96 dpi, origin at the top left): what the DirectX chart draws with depth testing
	///off, and its D3DX text. Immutable.</summary>
	public sealed class ToothSceneItem2D {
		public readonly ChartItem2DKind Kind;
		///<summary>Polygons: opaque. Text: the colour as given, alpha included, because D3DX text is blended.</summary>
		public readonly Color Color;
		///<summary>Polygons only: each polygon's points, all running the same way (clockwise on screen, GetSignedArea positive), so overlapping
		///polygons of one item add up under the Nonzero rule instead of cutting holes.</summary>
		public readonly ReadOnlyCollection<PointF[]> Polygons;
		///<summary>Text only.</summary>
		public readonly string Text;
		///<summary>Text only: the font of the DirectX chart's D3DX font, as GDI made it (D3DX fonts are GDI fonts).</summary>
		public readonly ChartFont Font;
		///<summary>Text only: the top left of the text cell, in whole pixels, as D3DX draws it (ceiling of x, floor of y of the projected point).</summary>
		public readonly Point Origin;
		///<summary>Text only: the width of the text as GDI measures it with the font, in pixels. The renderer scales its text to this width.</summary>
		public readonly int WidthPixels;
		///<summary>What the item is, for tests and messages: for example "big X of 8", "number 8".</summary>
		public readonly string Source;

		private ToothSceneItem2D(ChartItem2DKind kind,Color color,List<PointF[]> listPolygons,string text,ChartFont font,Point origin,int widthPixels,
			string source)
		{
			Kind=kind;
			Color=color;
			Polygons=(listPolygons??new List<PointF[]>()).AsReadOnly();
			Text=text;
			Font=font;
			Origin=origin;
			WidthPixels=widthPixels;
			Source=source;
		}

		public static ToothSceneItem2D CreatePolygons(Color color,List<PointF[]> listPolygons,string source) {
			List<PointF[]> listOriented=new List<PointF[]>();
			for(int i=0;i<listPolygons.Count;i++) {
				if(listPolygons[i]==null || listPolygons[i].Length<3) {
					continue;
				}
				PointF[] arrayPoints=(PointF[])listPolygons[i].Clone();
				if(GetSignedArea(arrayPoints)<0) {
					Array.Reverse(arrayPoints);
				}
				listOriented.Add(arrayPoints);
			}
			return new ToothSceneItem2D(ChartItem2DKind.Polygons,Color.FromArgb(255,color),listOriented,null,null,Point.Empty,0,source);
		}

		public static ToothSceneItem2D CreateText(string text,Color color,ChartFont font,Point origin,int widthPixels,string source) {
			return new ToothSceneItem2D(ChartItem2DKind.Text,color,null,text,font,origin,widthPixels,source);
		}

		///<summary>Twice the signed area of the polygon by the shoelace formula, in image coordinates (x right, y down): positive when the polygon
		///runs clockwise as seen on screen. Only the sign matters: CreatePolygons turns every polygon of an item the same way.</summary>
		public static double GetSignedArea(PointF[] arrayPoints) {
			double sum=0;
			for(int i=0;i<arrayPoints.Length;i++) {
				PointF p1=arrayPoints[i];
				PointF p2=arrayPoints[(i+1)%arrayPoints.Length];
				sum+=(double)p1.X*p2.Y-(double)p2.X*p1.Y;
			}
			return sum;
		}

		public override string ToString() {
			if(Kind==ChartItem2DKind.Text) {
				return Source+": text \""+Text+"\" at "+Origin.X+","+Origin.Y+" in "+Font+", #"+Color.ToArgb().ToString("X8");
			}
			return Source+": "+Polygons.Count+" polygons, #"+Color.ToArgb().ToString("X8");
		}
	}

	///<summary>A GDI font as the DirectX chart's D3DX fonts are made (D3DXCreateFont passes height, width and weight to GDI's CreateFont), with
	///the metrics GDI gives it. Height is GDI's cell height, Width its average character width (0: the font's own). Immutable.</summary>
	public sealed class ChartFont {
		public readonly string FaceName;
		public readonly int Height;
		public readonly int Width;
		///<summary>GDI weight: 400 normal, 700 bold, 800 ultra bold.</summary>
		public readonly int Weight;
		///<summary>GDI's tmAscent: from the top of the cell to the baseline, in pixels.</summary>
		public readonly int Ascent;
		///<summary>The em height in pixels: GDI's tmHeight minus tmInternalLeading. The renderer's WPF font size.</summary>
		public readonly int EmHeight;

		public ChartFont(string faceName,int height,int width,int weight,int ascent,int emHeight) {
			FaceName=faceName;
			Height=height;
			Width=width;
			Weight=weight;
			Ascent=ascent;
			EmHeight=emHeight;
		}

		public bool IsEqualTo(ChartFont other) {
			return other!=null && FaceName==other.FaceName && Height==other.Height && Width==other.Width && Weight==other.Weight;
		}

		public override string ToString() {
			return FaceName+" "+Height+"/"+Width+" w"+Weight;
		}
	}

	///<summary>Everything the renderer needs to make one chart image, built from ToothChartData by ToothSceneBuilder on the caller's thread and
	///never changed afterwards. It holds no WPF object and no reference to ToothChartData, so the render thread never reads the chart's data, and
	///tests of the scene need no thread (docs/specs/wpf-tooth-chart-plan.md, sections 5.1 to 5.4).</summary>
	public sealed class ToothScene {
		///<summary>The DirectX chart's distance from the camera: it moves every tooth 400 mm forward (DrawScene), and the WPF camera sits that far
		///in front of the teeth, so the specular highlights get the same view direction.</summary>
		public const float CameraDistance=400f;
		///<summary>DirectX's light: one directional light whose specular part is 0.8. The WPF chart's one light has that colour.</summary>
		public const float LightIntensity=0.8f;
		///<summary>DirectX's specular power (specularSharpness).</summary>
		public const float SpecularPower=70f;
		///<summary>The size of the image in pixels.</summary>
		public readonly Size Size;
		public readonly Color ColorBackground;
		public readonly bool IsPerio;
		///<summary>The width and height of the scene in mm that the image shows: DirectX's orthographic projection (OrthoLH), widened for wide
		///controls (Render).</summary>
		public readonly float WidthProjection;
		public readonly float HeightProjection;
		///<summary>DirectX's ambient and diffuse light: 0.4 and 0.6, or 0.6 and 0.4 in perio mode.</summary>
		public readonly float LightAmbient;
		public readonly float LightDiffuse;
		public readonly ReadOnlyCollection<ToothSceneItem3D> Items3D;
		public readonly ReadOnlyCollection<ToothSceneItem2D> Items2D;

		public ToothScene(Size size,Color colorBackground,bool isPerio,float widthProjection,float heightProjection,List<ToothSceneItem3D> listItems3D,
			List<ToothSceneItem2D> listItems2D)
		{
			Size=size;
			ColorBackground=Color.FromArgb(255,colorBackground);
			IsPerio=isPerio;
			WidthProjection=widthProjection;
			HeightProjection=heightProjection;
			LightAmbient=isPerio?0.6f:0.4f;
			LightDiffuse=isPerio?0.4f:0.6f;
			Items3D=new List<ToothSceneItem3D>(listItems3D).AsReadOnly();
			Items2D=new List<ToothSceneItem2D>(listItems2D).AsReadOnly();
		}

		///<summary>The diffuse part of a material: the colour times DirectX's diffuse light over the one WPF light (0.6/0.8, perio 0.4/0.8).</summary>
		public float DiffuseFactor {
			get {
				return LightDiffuse/LightIntensity;
			}
		}

		///<summary>The emissive part of a material: the colour times DirectX's ambient light (0.4, perio 0.6). Ambient light times the material's
		///colour is a constant, so an emissive colour gives DirectX's ambient term without a WPF ambient light.</summary>
		public float EmissiveFactor {
			get {
				return LightAmbient;
			}
		}

		///<summary>The camera's centre in scene mm, half a pixel up and left of the scene's origin: DirectX 9 puts pixel centres on whole
		///coordinates and WPF on halves, so a point lands on the same pixel in both when WPF's image is shifted by half a pixel.</summary>
		public float CameraCenterX {
			get {
				return -0.5f*WidthProjection/Size.Width;
			}
		}

		public float CameraCenterY {
			get {
				return 0.5f*HeightProjection/Size.Height;
			}
		}

		///<summary>DirectX's orthographic projection: SharpDX's Matrix.OrthoLH(WidthProjection,HeightProjection,0,1000), element by element as
		///SharpDX builds it (OrthoOffCenterLH).</summary>
		public ChartMatrix GetProjectionDirectX() {
			float left=-WidthProjection*0.5f;
			float right=WidthProjection*0.5f;
			float bottom=-HeightProjection*0.5f;
			float top=HeightProjection*0.5f;
			float zRange=1f/(1000f-0f);
			ChartMatrix matrix=ChartMatrix.Identity;
			matrix.M11=2f/(right-left);
			matrix.M22=2f/(top-bottom);
			matrix.M33=zRange;
			matrix.M41=(left+right)/(left-right);
			matrix.M42=(top+bottom)/(bottom-top);
			matrix.M43=-0f*zRange;
			return matrix;
		}

		///<summary>DirectX's world-view-projection matrix for a model transform: the transform, then DirectX's handedness flip and 400 mm shift
		///(DrawScene's defOrient: Scaling(1,1,-1) then Translation(0,0,400)), the identity view and the projection, multiplied in DirectX's order.</summary>
		public ChartMatrix GetScreenMatrixDirectX(ChartMatrix transform) {
			ChartMatrix defOrient=ChartMatrix.Scaling(1f,1f,-1f)*ChartMatrix.Translation(0f,0f,CameraDistance);
			return ((transform*defOrient)*ChartMatrix.Identity)*GetProjectionDirectX();
		}

		///<summary>DirectX's screen position of a scene point after the model transform: SharpDX's Vector3.Project with the viewport of the whole
		///image, in float as DirectX computes it. Whole numbers are pixel centres (DirectX 9's rule).</summary>
		public void ProjectDirectX(ChartMatrix transform,float x,float y,float z,out float xScreen,out float yScreen) {
			ChartMatrix matrixScreen=GetScreenMatrixDirectX(transform);
			float xClip;
			float yClip;
			float zClip;
			matrixScreen.TransformCoordinate(x,y,z,out xClip,out yClip,out zClip);
			xScreen=((1f+xClip)*0.5f*Size.Width)+0;
			yScreen=((1f-yClip)*0.5f*Size.Height)+0;
		}

		///<summary>The image position of a scene point for the 2D layer, in WPF's coordinates (pixel centres on halves): DirectX's position plus
		///half a pixel. The same mapping as the camera's (CameraCenterX/Y), so both layers put a point on the same pixel.</summary>
		public PointF ProjectToImage(ChartMatrix transform,float x,float y,float z) {
			float xScreen;
			float yScreen;
			ProjectDirectX(transform,x,y,z,out xScreen,out yScreen);
			return new PointF(xScreen+0.5f,yScreen+0.5f);
		}

		///<summary>True when other's 3D layer would give the same pixels: same size, background, light and the same 3D items in the same order. A
		///change that touches only the 2D layer (selection, drawings, text) keeps the 3D layer, and the renderer reuses its last image.</summary>
		public bool IsSame3DLayer(ToothScene other) {
			if(other==null || other.Size!=Size || other.ColorBackground.ToArgb()!=ColorBackground.ToArgb() || other.IsPerio!=IsPerio
				|| other.WidthProjection!=WidthProjection || other.HeightProjection!=HeightProjection || other.Items3D.Count!=Items3D.Count)
			{
				return false;
			}
			for(int i=0;i<Items3D.Count;i++) {
				if(!Items3D[i].IsEqualTo(other.Items3D[i])) {
					return false;
				}
			}
			return true;
		}

		///<summary>A text dump of the scene, one line per item, for test messages.</summary>
		public string Describe() {
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.AppendLine("Scene "+Size.Width+"x"+Size.Height+(IsPerio?" perio":"")+", background #"+ColorBackground.ToArgb().ToString("X8")
				+", projection "+WidthProjection.ToString(CultureInfo.InvariantCulture)+"x"+HeightProjection.ToString(CultureInfo.InvariantCulture)+" mm");
			foreach(ToothSceneItem3D item in Items3D) {
				stringBuilder.AppendLine("3D "+item);
			}
			foreach(ToothSceneItem2D item in Items2D) {
				stringBuilder.AppendLine("2D "+item);
			}
			return stringBuilder.ToString();
		}
	}
}
