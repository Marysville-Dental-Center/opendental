// The parity probes of the WPF chart against today's DirectX pictures (package TC-3, docs/specs/wpf-tooth-chart-plan.md section 7): the colour at
// each tooth's facial and occlusal surface centre and on its X, build-up, implant and root canal, and the allowance list of reviewed exceptions.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using OpenPmsDentalBusiness;
using SparksToothChart;
using SparksToothChart.Wpf3D;

namespace ToothChartImageTests {
	///<summary>What a probe samples.</summary>
	public enum ProbeKind {
		///<summary>The centre of the facial surface (B, or F on an anterior tooth) in the facial view.</summary>
		Facial,
		///<summary>The centre of the occlusal surface (O, or I on an anterior tooth) in the occlusal view.</summary>
		Occlusal,
		///<summary>Where the two strokes of the big X cross.</summary>
		BigX,
		///<summary>The centre of the build-up or post silhouette.</summary>
		BuildUp,
		///<summary>The centre of the implant, in the facial view.</summary>
		Implant,
		///<summary>The middle of the first root canal line.</summary>
		RootCanal,
	}

	///<summary>One probe point of a chart.</summary>
	public class ParityProbePoint {
		public string ToothId;
		public ProbeKind Kind;
		///<summary>In image coordinates with pixel centres on halves (ToothScene.ProjectToImage), so the pixel is (floor x, floor y), the same pixel in
		///the DirectX picture and in the WPF picture.</summary>
		public PointF Point;
		///<summary>False for the surfaces of a missing or hidden tooth: the view does not draw it, and both pictures must agree on tooth versus
		///background there.</summary>
		public bool IsToothDrawn;
		///<summary>1: the mean of the 3x3 pixels around the point (a surface). 0: the one pixel (a line or the crossing of the X, a few pixels wide).</summary>
		public int SampleRadius;

		public string Name {
			get {
				return ToothId+" "+Kind.ToString().ToLowerInvariant();
			}
		}
	}

	///<summary>One probe of one picture against the DirectX recording.</summary>
	public class ParityProbeOutcome {
		public string SceneName;
		public string SceneGroup;
		public string SizeName;
		public ParityProbePoint Probe;
		public Color ColorDirectX;
		public Color ColorWpf;
		///<summary>Null when the probe passes; otherwise what differs.</summary>
		public string Failure;
		///<summary>The reviewed exception that covers a failure, or null.</summary>
		public ParityAllowance Allowance;

		public override string ToString() {
			return SizeName+"/"+SceneName+" "+Probe.Name+" at "+Probe.Point.X.ToString("0.0",CultureInfo.InvariantCulture)+","
				+Probe.Point.Y.ToString("0.0",CultureInfo.InvariantCulture)+": DirectX "+ParityProbes.Describe(ColorDirectX)+", WPF "+ParityProbes.Describe(ColorWpf)
				+(Failure==null?"":" ("+Failure+")")+(Allowance==null?"":" [allowed: "+Allowance.Reason+"]");
		}
	}

	///<summary>A reviewed exception of the parity probes, with its reason. SceneName, SizeName and ToothId may be "*"; Kind null means every kind.</summary>
	public class ParityAllowance {
		public string SceneName;
		public string SizeName;
		public string ToothId;
		public ProbeKind? Kind;
		public string Reason;
		///<summary>True for a case of section 5.1's known difference, which the list keeps even when no probe lands on the covered part.</summary>
		public bool IsKnownDifference;

		public ParityAllowance(string sceneName,string sizeName,string toothId,ProbeKind? kind,string reason,bool isKnownDifference=false) {
			SceneName=sceneName;
			SizeName=sizeName;
			ToothId=toothId;
			Kind=kind;
			Reason=reason;
			IsKnownDifference=isKnownDifference;
		}

		public bool Covers(ParityProbeOutcome outcome) {
			return (SceneName=="*" || SceneName==outcome.SceneName) && (SizeName=="*" || SizeName==outcome.SizeName)
				&& (ToothId=="*" || ToothId==outcome.Probe.ToothId) && (Kind==null || Kind==outcome.Probe.Kind);
		}

		public override string ToString() {
			return SceneName+" "+SizeName+" "+ToothId+" "+(Kind?.ToString()??"every kind")+": "+Reason;
		}
	}

	///<summary>Section 7's parity probes. The colour is sampled at each tooth's facial and occlusal surface centre, in the WPF picture and in the
	///DirectX recording, and on the items DirectX draws over the teeth: the crossing of the big X, the centre of the build-up silhouette, the implant
	///and a root canal line. Every point is computed from the layout tables as the chart draws the tooth: the tooth's world transform
	///(ToothSceneBuilder, the DirectX chart's matrices) and its model, projected with the chart's own projection. A surface centre is the
	///area-weighted centre of the front-facing triangles of the surface's group, and its sample the mean of the 3x3 pixels there; a line or the X is
	///sampled at its one pixel. A coloured sample passes when its hue is within 12 degrees and its lightness within 20 points; any other when its
	///lightness is within 20 points. At a missing or hidden tooth both pictures must agree on tooth versus background. Reviewed exceptions are in
	///Allowances.</summary>
	public static class ParityProbes {
		public const double MaxHueDegrees=12;
		///<summary>HSL lightness in points of 100.</summary>
		public const double MaxLightnessPoints=20;
		///<summary>A sample with an HSL saturation at least this, and a lightness between 10 and 90, is coloured: its hue means something.</summary>
		public const double MinSaturationColoured=0.25;
		///<summary>A sample within this of the background colour in every channel is background.</summary>
		public const int BackgroundTolerance=8;

		///<summary>The reviewed exceptions, each with its reason. The cases of section 5.1's known difference: DirectX draws its overlays with no
		///depth, so a later depth-tested draw covers them, while the WPF chart's 2D layer is always on top.</summary>
		public static readonly List<ParityAllowance> Allowances=new List<ParityAllowance> {
			new ParityAllowance("x-under-moved-neighbour","*","*",ProbeKind.BigX,"Known difference (plan 5.1): DirectX draws the moved neighbour after "
				+"the X with depth testing on, so the neighbour covers the X; the WPF chart's X is in the 2D layer, always on top.",true),
			new ParityAllowance("buildup-under-moved-neighbour","*","*",ProbeKind.BuildUp,"Known difference (plan 5.1): the moved neighbour covers the "
				+"build-up in DirectX; the WPF chart's build-up silhouette is in the 2D layer, always on top.",true),
			new ParityAllowance("x-on-implant-tooth","*","*",ProbeKind.BigX,"Known difference (plan 5.1): DirectX draws the implant after the X with "
				+"depth testing on, so the implant covers the X; the WPF chart's X is in the 2D layer, always on top.",true),
		};

		///<summary>The probe points of the chart as tcData holds it, at its control size: the surface centres of every visible tooth in both views
		///(the occlusal view of a permanent tooth only when its primary tooth is not shown over it, as DirectX's DrawOcclusalView) and of every missing
		///or hidden permanent tooth; and, where DirectX draws them, the X, the build-up, the implant and the first root canal line. Primary teeth that
		///are not shown have no probes.</summary>
		public static List<ParityProbePoint> GetPoints(ToothChartData tcData) {
			ToothScene scene=ToothSceneBuilder.Build(tcData);
			List<ParityProbePoint> listPoints=new List<ParityProbePoint>();
			for(int t=0;t<tcData.ListToothGraphics.Count;t++) {
				ToothGraphic toothGraphic=tcData.ListToothGraphics[t];
				string toothId=toothGraphic.ToothID;
				if(toothId=="implant") {
					continue;
				}
				bool isPrimary=Tooth.IsPrimary(toothId);
				if(isPrimary && !toothGraphic.Visible) {
					continue;
				}
				bool isMaxillary=ToothGraphic.IsMaxillary(toothId);
				ChartMatrix worldFacial=ToothSceneBuilder.GetUserRotationAndTranslation(toothGraphic)
					*ChartMatrix.Translation(tcData.GetTransX(toothId),tcData.GetTransYfacial(toothId),0);
				bool isDrawnFacial=toothGraphic.Visible || (toothGraphic.IsCrown && toothGraphic.IsImplant) || toothGraphic.IsPontic;
				AddSurfacePoint(listPoints,scene,toothGraphic,ProbeKind.Facial,ToothGraphic.IsAnterior(toothId)?ToothGroupType.F:ToothGroupType.B,worldFacial,
					isDrawnFacial);
				if(toothGraphic.DrawBigX) {
					//DrawFacialView's two strokes, (-2,12)-(2,-6) and (2,12)-(-2,-6) on a maxillary tooth, cross at (0,3); on a mandibular one at (0,-3).
					AddPoint(listPoints,toothId,ProbeKind.BigX,scene.ProjectToImage(worldFacial,0,isMaxillary?3f:-3f,0),0);
				}
				ToothGroup groupBuildUp=toothGraphic.GetGroup(ToothGroupType.Buildup);
				if(toothGraphic.Visible && groupBuildUp!=null && groupBuildUp.Visible) {
					PointF? pointBuildUp=GetCentre(scene,toothGraphic,toothGraphic.Groups.IndexOf(groupBuildUp),worldFacial,false);
					if(pointBuildUp!=null) {
						AddPoint(listPoints,toothId,ProbeKind.BuildUp,pointBuildUp.Value,0);
					}
				}
				if(toothGraphic.IsImplant) {
					AddImplantPoint(listPoints,scene,tcData,toothGraphic,worldFacial);
				}
				if(toothGraphic.Visible && toothGraphic.IsRCT) {
					List<LineSimple> listLines=toothGraphic.GetRctLines();
					if(listLines.Count>0 && listLines[0].Vertices.Count>=3) {
						Vertex3f vertex1=listLines[0].Vertices[1];
						Vertex3f vertex2=listLines[0].Vertices[2];
						AddPoint(listPoints,toothId,ProbeKind.RootCanal,scene.ProjectToImage(worldFacial,(vertex1.X+vertex2.X)/2,(vertex1.Y+vertex2.Y)/2,
							(vertex1.Z+vertex2.Z)/2),0);
					}
				}
				if(!isPrimary && Tooth.IsValidDB(Tooth.PermToPri(toothId)) && tcData.ListToothGraphics[Tooth.PermToPri(toothId)].Visible) {
					continue;//the primary tooth's occlusal view is drawn there instead
				}
				ChartMatrix worldOcclusal=ToothSceneBuilder.GetUserRotationAndTranslation(toothGraphic)*ToothSceneBuilder.GetOcclusalRotation(toothId)
					*ChartMatrix.Translation(tcData.GetTransX(toothId),tcData.GetTransYocclusal(toothId),0);
				bool isDrawnOcclusal=toothGraphic.Visible || (toothGraphic.IsCrown && toothGraphic.IsImplant);
				AddSurfacePoint(listPoints,scene,toothGraphic,ProbeKind.Occlusal,ToothGraphic.IsAnterior(toothId)?ToothGroupType.I:ToothGroupType.O,worldOcclusal,
					isDrawnOcclusal);
			}
			return listPoints;
		}

		private static void AddSurfacePoint(List<ParityProbePoint> listPoints,ToothScene scene,ToothGraphic toothGraphic,ProbeKind kind,ToothGroupType groupType,
			ChartMatrix world,bool isToothDrawn)
		{
			ToothGroup group=toothGraphic.GetGroup(groupType) ?? toothGraphic.GetGroup(ToothGroupType.Enamel);
			if(group==null) {
				return;
			}
			PointF? pointCentre=GetCentre(scene,toothGraphic,toothGraphic.Groups.IndexOf(group),world,true);
			if(pointCentre==null) {
				return;
			}
			ParityProbePoint probe=AddPoint(listPoints,toothGraphic.ToothID,kind,pointCentre.Value,1);
			probe.IsToothDrawn=isToothDrawn;
		}

		///<summary>DrawImplant's model and transform: the implant upside down for a maxillary tooth. The centre of its front-facing triangles.</summary>
		private static void AddImplantPoint(List<ParityProbePoint> listPoints,ToothScene scene,ToothChartData tcData,ToothGraphic toothGraphic,ChartMatrix worldFacial) {
			ToothGraphic implantGraphic=tcData.ListToothGraphics["implant"];
			if(implantGraphic==null) {
				return;
			}
			ChartMatrix world=worldFacial;
			if(ToothGraphic.IsMaxillary(toothGraphic.ToothID)) {
				world=ChartMatrix.RotationZ((float)Math.PI)*world;
			}
			for(int g=0;g<implantGraphic.Groups.Count;g++) {
				if(!implantGraphic.Groups[g].Visible || implantGraphic.Groups[g].GroupType==ToothGroupType.Buildup || implantGraphic.Groups[g].Faces.Count==0) {
					continue;
				}
				PointF? pointCentre=GetCentre(scene,implantGraphic,g,world,true);
				if(pointCentre!=null) {
					AddPoint(listPoints,toothGraphic.ToothID,ProbeKind.Implant,pointCentre.Value,1);
					return;//the first group with front faces: the implant's body
				}
			}
		}

		private static ParityProbePoint AddPoint(List<ParityProbePoint> listPoints,string toothId,ProbeKind kind,PointF point,int sampleRadius) {
			ParityProbePoint probe=new ParityProbePoint();
			probe.ToothId=toothId;
			probe.Kind=kind;
			probe.Point=point;
			probe.IsToothDrawn=true;
			probe.SampleRadius=sampleRadius;
			listPoints.Add(probe);
			return probe;
		}

		///<summary>The area-weighted centre, in the image, of the group's triangles: those that face the camera after the world transform (their
		///outward normal points to +Z, where the camera is) when isFrontOnly, else all of them (a silhouette drawn with no culling). Null when none
		///counts.</summary>
		public static PointF? GetCentre(ToothScene scene,ToothGraphic toothGraphic,int indexGroup,ChartMatrix world,bool isFrontOnly) {
			int[] arrayTriangles=ToothMeshWinding.GetWoundTriangles(toothGraphic,indexGroup);
			double sumArea=0;
			double sumX=0;
			double sumY=0;
			for(int i=0;i+2<arrayTriangles.Length;i+=3) {
				float[] arrayX=new float[3];
				float[] arrayY=new float[3];
				float[] arrayZ=new float[3];
				PointF[] arrayProjected=new PointF[3];
				for(int v=0;v<3;v++) {
					Vertex3f vertex=toothGraphic.VertexNormals[arrayTriangles[i+v]].Vertex;
					world.TransformCoordinate(vertex.X,vertex.Y,vertex.Z,out arrayX[v],out arrayY[v],out arrayZ[v]);
					arrayProjected[v]=scene.ProjectToImage(world,vertex.X,vertex.Y,vertex.Z);
				}
				//The z of the geometric normal of the wound triangle in the world: positive when it faces the camera.
				double nz=((double)arrayX[1]-arrayX[0])*((double)arrayY[2]-arrayY[0])-((double)arrayY[1]-arrayY[0])*((double)arrayX[2]-arrayX[0]);
				if(isFrontOnly && nz<=0) {
					continue;
				}
				double area=Math.Abs(((double)arrayProjected[1].X-arrayProjected[0].X)*((double)arrayProjected[2].Y-arrayProjected[0].Y)
					-((double)arrayProjected[1].Y-arrayProjected[0].Y)*((double)arrayProjected[2].X-arrayProjected[0].X))/2;
				sumArea+=area;
				sumX+=area*(arrayProjected[0].X+arrayProjected[1].X+arrayProjected[2].X)/3;
				sumY+=area*(arrayProjected[0].Y+arrayProjected[1].Y+arrayProjected[2].Y)/3;
			}
			if(sumArea<=0) {
				return null;
			}
			return new PointF((float)(sumX/sumArea),(float)(sumY/sumArea));
		}

		///<summary>The mean colour of the pixels within radius of the probe's pixel (those inside the image), from pixels as GetPixelsArgb gives them.</summary>
		public static Color Sample(byte[] arrayPixelsArgb,Size size,PointF point,int radius) {
			int xCentre=(int)Math.Floor(point.X);
			int yCentre=(int)Math.Floor(point.Y);
			int sumB=0;
			int sumG=0;
			int sumR=0;
			int count=0;
			for(int y=yCentre-radius;y<=yCentre+radius;y++) {
				for(int x=xCentre-radius;x<=xCentre+radius;x++) {
					if(x<0 || y<0 || x>=size.Width || y>=size.Height) {
						continue;
					}
					int index=(y*size.Width+x)*4;
					sumB+=arrayPixelsArgb[index];
					sumG+=arrayPixelsArgb[index+1];
					sumR+=arrayPixelsArgb[index+2];
					count++;
				}
			}
			if(count==0) {
				return Color.Empty;
			}
			return Color.FromArgb(255,(int)Math.Round(sumR/(double)count),(int)Math.Round(sumG/(double)count),(int)Math.Round(sumB/(double)count));
		}

		///<summary>Probes both pictures (pixels as GetPixelsArgb gives them) at every point inside them. The outcomes say which failed and whether an
		///allowance covers the failure.</summary>
		public static List<ParityProbeOutcome> Probe(ChartScene scene,ChartSize chartSize,List<ParityProbePoint> listPoints,byte[] arrayDirectX,byte[] arrayWpf,
			Color colorBackground)
		{
			List<ParityProbeOutcome> listOutcomes=new List<ParityProbeOutcome>();
			foreach(ParityProbePoint probe in listPoints) {
				if(probe.Point.X<0 || probe.Point.Y<0 || probe.Point.X>=chartSize.Size.Width || probe.Point.Y>=chartSize.Size.Height) {
					continue;//outside the picture (a tooth moved past the edge)
				}
				ParityProbeOutcome outcome=new ParityProbeOutcome();
				outcome.SceneName=scene.Name;
				outcome.SceneGroup=scene.Group;
				outcome.SizeName=chartSize.FolderName;
				outcome.Probe=probe;
				outcome.ColorDirectX=Sample(arrayDirectX,chartSize.Size,probe.Point,probe.SampleRadius);
				outcome.ColorWpf=Sample(arrayWpf,chartSize.Size,probe.Point,probe.SampleRadius);
				outcome.Failure=GetFailure(outcome.ColorDirectX,outcome.ColorWpf,colorBackground,probe.IsToothDrawn);
				if(outcome.Failure!=null) {
					outcome.Allowance=Allowances.FirstOrDefault(x => x.Covers(outcome));
				}
				listOutcomes.Add(outcome);
			}
			return listOutcomes;
		}

		///<summary>Null when the two samples agree by section 7's rules, otherwise what differs. At a tooth that is not drawn (isToothDrawn false) the
		///two must agree on tooth versus background. Elsewhere: the lightness within 20 points, and for a coloured sample the hue within 12 degrees.</summary>
		public static string GetFailure(Color colorDirectX,Color colorWpf,Color colorBackground,bool isToothDrawn) {
			bool isBackgroundDirectX=IsBackground(colorDirectX,colorBackground);
			bool isBackgroundWpf=IsBackground(colorWpf,colorBackground);
			if(isBackgroundDirectX && isBackgroundWpf) {
				return null;
			}
			if(!isToothDrawn && isBackgroundDirectX!=isBackgroundWpf) {
				return "tooth versus background: DirectX shows "+(isBackgroundDirectX?"the background":"something")+", WPF "+(isBackgroundWpf?"the background":"something");
			}
			double lightnessDirectX=GetLightness(colorDirectX)*100;
			double lightnessWpf=GetLightness(colorWpf)*100;
			if(Math.Abs(lightnessDirectX-lightnessWpf)>MaxLightnessPoints) {
				return "lightness "+lightnessDirectX.ToString("0",CultureInfo.InvariantCulture)+" against "+lightnessWpf.ToString("0",CultureInfo.InvariantCulture);
			}
			if(IsColoured(colorDirectX) || IsColoured(colorWpf)) {
				double hueDifference=GetHueDifference(colorDirectX.GetHue(),colorWpf.GetHue());
				if(hueDifference>MaxHueDegrees) {
					return "hue "+colorDirectX.GetHue().ToString("0",CultureInfo.InvariantCulture)+" against "+colorWpf.GetHue().ToString("0",CultureInfo.InvariantCulture)
						+" degrees";
				}
			}
			return null;
		}

		public static bool IsBackground(Color color,Color colorBackground) {
			return Math.Abs(color.R-colorBackground.R)<=BackgroundTolerance && Math.Abs(color.G-colorBackground.G)<=BackgroundTolerance
				&& Math.Abs(color.B-colorBackground.B)<=BackgroundTolerance;
		}

		///<summary>HSL lightness, 0 to 1 (System.Drawing.Color.GetBrightness is HSL lightness).</summary>
		public static double GetLightness(Color color) {
			return color.GetBrightness();
		}

		public static bool IsColoured(Color color) {
			double lightness=color.GetBrightness();
			return color.GetSaturation()>=MinSaturationColoured && lightness>=0.1 && lightness<=0.9;
		}

		///<summary>The difference of two hues in degrees, 0 to 180.</summary>
		public static double GetHueDifference(double hue1,double hue2) {
			double difference=Math.Abs(hue1-hue2)%360;
			return difference>180?360-difference:difference;
		}

		public static string Describe(Color color) {
			return "#"+color.R.ToString("X2")+color.G.ToString("X2")+color.B.ToString("X2")+" (h"+color.GetHue().ToString("0",CultureInfo.InvariantCulture)+" s"
				+(color.GetSaturation()*100).ToString("0",CultureInfo.InvariantCulture)+" l"+(color.GetBrightness()*100).ToString("0",CultureInfo.InvariantCulture)+")";
		}
	}
}
