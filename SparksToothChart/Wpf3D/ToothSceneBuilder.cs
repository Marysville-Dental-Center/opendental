using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using OpenPmsDentalBusiness;

namespace SparksToothChart.Wpf3D {
	///<summary>Builds the immutable scene of the WPF chart from ToothChartData: a pure function of the data, run on the caller's thread, that
	///makes no WPF object (docs/specs/wpf-tooth-chart-plan.md, section 5). It follows the DirectX chart's DrawScene step by step:
	///- 3D layer: the tooth groups, implants and perio teeth that DirectX draws with depth testing on, each with DirectX's world matrix (without the
	///  handedness flip), its colour (opaque; unerupted teeth at half RGB), the specular value of DirectX's GetGroupMaterial, and the reversed
	///  winding when the transform mirrors (5.2).
	///- 2D layer: everything DirectX draws with depth testing off, and its D3DX text, in DirectX's order: per tooth the big X, the root canal lines
	///  and the build-up/post silhouette (facial view), and the sealant "S" (occlusal view); then the watches, the drawings, the centre line and the
	///  numbers (5.1). Points are projected with DirectX's own projection (ToothScene.ProjectToImage), the mapping the camera uses.
	///The perio chart gets its four rows of teeth with the perio light; its 2D decorations come with TC-7.</summary>
	public static class ToothSceneBuilder {
		///<summary>The specular values of DirectX's GetGroupMaterial: cementum 0.1, near-white enamel colours 0.4, every other colour 1.0.</summary>
		public const float SpecularCementum=0.1f;
		public const float SpecularEnamel=0.4f;
		public const float SpecularNormal=1f;

		///<summary>The scene of the chart as tcData holds it, at tcData's control size (ToothChartData.SizeControl, which the wrapper sets on every
		///resize). An empty size gives an empty scene.</summary>
		public static ToothScene Build(ToothChartData tcData) {
			Size size=tcData.SizeControl;
			if(size.Width<1 || size.Height<1 || tcData.ScaleMmToPix<=0 || float.IsNaN(tcData.ScaleMmToPix) || float.IsInfinity(tcData.ScaleMmToPix)) {
				return new ToothScene(new Size(Math.Max(size.Width,0),Math.Max(size.Height,0)),tcData.ColorBackground,tcData.PerioMode,
					tcData.SizeOriginalProjection.Width,tcData.SizeOriginalProjection.Height,new List<ToothSceneItem3D>(),new List<ToothSceneItem2D>());
			}
			//DirectX's Render: the projection keeps 130 x 97.34 mm and widens for a wide or tall control.
			float heightProj=tcData.SizeOriginalProjection.Height;
			float widthProj=tcData.SizeOriginalProjection.Width;
			if(tcData.IsWide) {
				widthProj=heightProj*size.Width/size.Height;
			}
			else {
				heightProj=widthProj*size.Height/size.Width;
			}
			SceneBuilding building=new SceneBuilding(tcData,size,widthProj,heightProj);
			if(tcData.PerioMode) {
				AddPerioScene(building);
			}
			else {
				AddChartScene(building);
			}
			return new ToothScene(size,tcData.ColorBackground,tcData.PerioMode,widthProj,heightProj,building.ListItems3D,building.ListItems2D);
		}

		#region Chart scene
		///<summary>DirectX's DrawScene: for each tooth the facial view, then the occlusal view; then the watches, the drawings, the centre line and the
		///numbers.</summary>
		private static void AddChartScene(SceneBuilding building) {
			ToothChartData tcData=building.TcData;
			for(int t=0;t<tcData.ListToothGraphics.Count;t++) {
				if(tcData.ListToothGraphics[t].ToothID=="implant") {//this is not an actual tooth.
					continue;
				}
				AddFacialView(building,tcData.ListToothGraphics[t]);
				AddOcclusalView(building,tcData.ListToothGraphics[t]);
			}
			AddWatches(building);
			AddDrawingSegments(building);
			AddNumbersAndLines(building);
		}

		///<summary>DirectX's DrawFacialView: the tooth, then (depth off) the big X, the root canal lines and the build-up/post, then the implant.</summary>
		private static void AddFacialView(SceneBuilding building,ToothGraphic toothGraphic) {
			ToothChartData tcData=building.TcData;
			string toothId=toothGraphic.ToothID;
			ChartMatrix toothTrans=ChartMatrix.Translation(tcData.GetTransX(toothId),tcData.GetTransYfacial(toothId),0);
			ChartMatrix world=GetUserRotationAndTranslation(toothGraphic)*toothTrans;
			if(toothGraphic.Visible || (toothGraphic.IsCrown && toothGraphic.IsImplant) || toothGraphic.IsPontic) {
				AddTooth(building,toothGraphic,world,toothId+" facial");
			}
			if(toothGraphic.DrawBigX) {//Extraction
				string source="big X of "+toothId;
				if(ToothGraphic.IsMaxillary(toothId)) {
					AddLineList(building,world,toothGraphic.colorX,0.71f,source,new Vertex3f(-2f,12f,0f),new Vertex3f(2f,-6f,0f));
					AddLineList(building,world,toothGraphic.colorX,0.71f,source,new Vertex3f(2f,12f,0f),new Vertex3f(-2f,-6f,0f));
				}
				else {
					AddLineList(building,world,toothGraphic.colorX,0.71f,source,new Vertex3f(-2f,6f,0f),new Vertex3f(2f,-12f,0f));
					AddLineList(building,world,toothGraphic.colorX,0.71f,source,new Vertex3f(2f,6f,0f),new Vertex3f(-2f,-12f,0f));
				}
			}
			if(toothGraphic.Visible && toothGraphic.IsRCT) {//Root Canal
				foreach(LineSimple line in toothGraphic.GetRctLines()) {
					AddExtendedLineStrip(building,world,0.08f,false,toothGraphic.colorRCT,0.8f,"root canal of "+toothId,line.Vertices.ToArray());
				}
			}
			ToothGroup groupBU=toothGraphic.GetGroup(ToothGroupType.Buildup);//during debugging, not all teeth have a BU group yet.
			if(toothGraphic.Visible && groupBU!=null && groupBU.Visible) {//BU or Post
				AddSilhouette(building,toothGraphic,toothGraphic.Groups.IndexOf(groupBU),world,groupBU.PaintColor,"build-up of "+toothId);
			}
			if(toothGraphic.IsImplant) {
				AddImplant(building,toothGraphic,world,"implant of "+toothId);
			}
		}

		///<summary>DirectX's DrawImplant: the implant model in the implant colour with the normal specular value, upside down for a maxillary tooth.</summary>
		private static void AddImplant(SceneBuilding building,ToothGraphic toothGraphic,ChartMatrix world,string source) {
			ToothChartData tcData=building.TcData;
			if(ToothGraphic.IsMaxillary(toothGraphic.ToothID)) {
				//flip the implant upside down
				world=ChartMatrix.RotationZ((float)Math.PI)*world;
			}
			ToothGraphic implantGraphic=tcData.ListToothGraphics["implant"];
			if(implantGraphic==null) {
				return;
			}
			for(int g=0;g<implantGraphic.Groups.Count;g++) {
				ToothGroup group=implantGraphic.Groups[g];
				if(!group.Visible || group.GroupType==ToothGroupType.Buildup || group.Faces.Count==0) {
					continue;
				}
				building.ListItems3D.Add(new ToothSceneItem3D("implant",g,group.GroupType,toothGraphic.colorImplant,SpecularNormal,world,source));
			}
		}

		///<summary>DirectX's DrawOcclusalView: the tooth seen from occlusal, unless the primary tooth over it shows, then (depth on) the sealant "S".</summary>
		private static void AddOcclusalView(SceneBuilding building,ToothGraphic toothGraphic) {
			ToothChartData tcData=building.TcData;
			string toothId=toothGraphic.ToothID;
			ChartMatrix toothTrans=ChartMatrix.Translation(tcData.GetTransX(toothId),tcData.GetTransYocclusal(toothId),0);
			ChartMatrix toothRot=GetOcclusalRotation(toothId);
			ChartMatrix world=GetUserRotationAndTranslation(toothGraphic)*toothRot*toothTrans;
			if(!Tooth.IsPrimary(toothId)//if perm tooth
				&& Tooth.IsValidDB(Tooth.PermToPri(toothId))
				&& tcData.ListToothGraphics[Tooth.PermToPri(toothId)].Visible)//and the primary tooth is visible
			{
				//do not paint
			}
			else if(toothGraphic.Visible//might not be visible if an implant
				|| (toothGraphic.IsCrown && toothGraphic.IsImplant))//a crown on an implant will paint
			//pontics won't paint, because tooth is invisible
			{
				AddTooth(building,toothGraphic,world,toothId+" occlusal");
			}
			if(toothGraphic.Visible && toothGraphic.IsSealant) {//draw sealant
				float toMm=1f/tcData.ScaleMmToPix;
				ChartMatrix worldSealant=GetUserTranslation(toothGraphic)*toothRot*toothTrans;
				ChartFont font=building.FontSealant;
				if(ToothGraphic.IsMaxillary(toothId)) {
					AddText(building,"S",worldSealant,tcData.PixelScaleRatio*(-6f*toMm),tcData.PixelScaleRatio*(-100f*toMm),-6f,toothGraphic.colorSealant,font,
						"sealant of "+toothId);
				}
				else {
					AddText(building,"S",worldSealant,tcData.PixelScaleRatio*(-6f*toMm),tcData.PixelScaleRatio*(22f*toMm),-6f,toothGraphic.colorSealant,font,
						"sealant of "+toothId);
				}
			}
		}

		///<summary>The view rotation of the occlusal surface (DrawOcclusalView): -110 degrees about X for the maxilla, 110 for mandibular anterior
		///teeth and 120 for mandibular posterior teeth.</summary>
		public static ChartMatrix GetOcclusalRotation(string toothId) {
			ChartMatrix toothRot=ChartMatrix.Identity;
			if(ToothGraphic.IsMaxillary(toothId)) {
				toothRot=toothRot*ChartMatrix.RotationX((float)((-110f*Math.PI)/180f));//rotate angle about line from origin to x,y,z
			}
			else {//mandibular
				if(ToothGraphic.IsAnterior(toothId)) {
					toothRot=toothRot*ChartMatrix.RotationX((float)((110f*Math.PI)/180f));
				}
				else {
					toothRot=toothRot*ChartMatrix.RotationX((float)((120f*Math.PI)/180f));
				}
			}
			return toothRot;
		}

		///<summary>DirectX's DrawTooth with GetGroupMaterial: every visible group except the build-up and the unnamed groups, in its paint colour
		///(half RGB when the tooth is unerupted, ShiftO below -10), opaque.</summary>
		private static void AddTooth(SceneBuilding building,ToothGraphic toothGraphic,ChartMatrix world,string source) {
			for(int g=0;g<toothGraphic.Groups.Count;g++) {
				ToothGroup group=toothGraphic.Groups[g];
				if(!group.Visible || group.Faces.Count==0 || group.GroupType==ToothGroupType.Buildup || group.GroupType==ToothGroupType.None) {
					continue;
				}
				building.ListItems3D.Add(new ToothSceneItem3D(toothGraphic.ToothID,g,group.GroupType,GetGroupColor(toothGraphic,group),
					GetGroupSpecular(group),world,source));
			}
		}

		///<summary>The colour of a tooth group: its paint colour, at half RGB when the tooth is unerupted (ShiftO below -10). DirectX halves the alpha
		///too but draws with blending off, so the colour is opaque.</summary>
		public static Color GetGroupColor(ToothGraphic toothGraphic,ToothGroup group) {
			Color colorPaint=group.PaintColor;
			if(toothGraphic.ShiftO<-10) {//if unerupted
				return Color.FromArgb(255,colorPaint.R/2,colorPaint.G/2,colorPaint.B/2);
			}
			return Color.FromArgb(255,colorPaint);
		}

		///<summary>DirectX's GetGroupMaterial: cementum 0.1; a near-white paint colour (R over 245, G over 245, B over 235; the enamel colour is
		///250,250,240) 0.4, because DirectX washes out the specular on enamel; every other colour 1.0.</summary>
		public static float GetGroupSpecular(ToothGroup group) {
			if(group.GroupType==ToothGroupType.Cementum) {
				return SpecularCementum;
			}
			if(group.PaintColor.R>245 && group.PaintColor.G>245 && group.PaintColor.B>235) {
				return SpecularEnamel;
			}
			return SpecularNormal;
		}
		#endregion Chart scene

		#region Tooth movement
		///<summary>DirectX's ToothTranslationMatrix: ShiftM, ShiftO and ShiftB with the signs of the tooth's quadrant.</summary>
		public static ChartMatrix GetUserTranslation(ToothGraphic toothGraphic) {
			string toothId=toothGraphic.ToothID;
			if(ToothGraphic.IsRight(toothId)) {
				if(ToothGraphic.IsMaxillary(toothId)) {//UR
					return ChartMatrix.Translation(toothGraphic.ShiftM,-toothGraphic.ShiftO,toothGraphic.ShiftB);
				}
				return ChartMatrix.Translation(toothGraphic.ShiftM,toothGraphic.ShiftO,toothGraphic.ShiftB);//LR
			}
			if(ToothGraphic.IsMaxillary(toothId)) {//UL
				return ChartMatrix.Translation(-toothGraphic.ShiftM,-toothGraphic.ShiftO,toothGraphic.ShiftB);
			}
			return ChartMatrix.Translation(-toothGraphic.ShiftM,toothGraphic.ShiftO,toothGraphic.ShiftB);//LL
		}

		///<summary>DirectX's ToothRotationMatrix: Rotate (about Y) first, then TipB (about X), then TipM (about Z), with the signs of the tooth's
		///quadrant.</summary>
		public static ChartMatrix GetUserRotation(ToothGraphic toothGraphic) {
			string toothId=toothGraphic.ToothID;
			ChartMatrix rotM;
			ChartMatrix rotB;
			ChartMatrix rot;
			if(ToothGraphic.IsRight(toothId)) {
				if(ToothGraphic.IsMaxillary(toothId)) {//UR
					rotM=ChartMatrix.RotationZ(((float)(toothGraphic.TipM*Math.PI)/180));
					rotB=ChartMatrix.RotationX(((float)(-toothGraphic.TipB*Math.PI)/180));
					rot=ChartMatrix.RotationY(((float)(toothGraphic.Rotate*Math.PI)/180));
				}
				else {//LR
					rotM=ChartMatrix.RotationZ(((float)(-toothGraphic.TipM*Math.PI)/180));
					rotB=ChartMatrix.RotationX(((float)(toothGraphic.TipB*Math.PI)/180));
					rot=ChartMatrix.RotationY(((float)(-toothGraphic.Rotate*Math.PI)/180));
				}
			}
			else {
				if(ToothGraphic.IsMaxillary(toothId)) {//UL
					rotM=ChartMatrix.RotationZ(((float)(-toothGraphic.TipM*Math.PI)/180));
					rotB=ChartMatrix.RotationX(((float)(-toothGraphic.TipB*Math.PI)/180));
					rot=ChartMatrix.RotationY(((float)(toothGraphic.Rotate*Math.PI)/180));
				}
				else {//LL
					rotM=ChartMatrix.RotationZ(((float)(toothGraphic.TipM*Math.PI)/180));
					rotB=ChartMatrix.RotationX(((float)(toothGraphic.TipB*Math.PI)/180));
					rot=ChartMatrix.RotationY(((float)(-toothGraphic.Rotate*Math.PI)/180));
				}
			}
			return rot*rotB*rotM;
		}

		///<summary>DirectX's ToothRotationAndTranslationMatrix: the user's rotations, then the user's translation.</summary>
		public static ChartMatrix GetUserRotationAndTranslation(ToothGraphic toothGraphic) {
			return GetUserRotation(toothGraphic)*GetUserTranslation(toothGraphic);
		}
		#endregion Tooth movement

		#region Perio scene
		///<summary>The four rows of the graphical perio chart, from the top: maxillary facial, maxillary lingual, mandibular lingual, mandibular
		///facial.</summary>
		public enum PerioRow {
			MaxillaryFacial,
			MaxillaryLingual,
			MandibularLingual,
			MandibularFacial,
		}

		///<summary>DirectX's DrawScenePerio: baseY moves the chart as near to the top of the control as possible, so a taller control gives the
		///legend room at the bottom.</summary>
		public static float GetPerioBaseY(ToothChartData tcData) {
			//The distance from y=0 to the tip of the highest root in the upper-most maxillary row.
			const float maxYmm=63f;
			return tcData.SizeControl.Height/(2f*tcData.ScaleMmToPix)-maxYmm;
		}

		///<summary>A perio row's orientation: DirectX's row matrix (DrawScenePerio) without the handedness flip and 400 mm shift of its defOrient.
		///The two lingual rows add a Z flip, which shows the teeth from behind; their transforms mirror, so their teeth use the reversed winding.</summary>
		public static ChartMatrix GetPerioRowOrientation(PerioRow perioRow,float baseY) {
			ChartMatrix defOrient=ChartMatrix.Translation(0,baseY,0);
			switch(perioRow) {
				case PerioRow.MaxillaryFacial:
					return ChartMatrix.Translation(0,45f,0)*defOrient;
				case PerioRow.MaxillaryLingual:
					return ChartMatrix.Scaling(1f,1f,-1f)*ChartMatrix.Translation(0,12f,0)*defOrient;
				case PerioRow.MandibularLingual:
					return ChartMatrix.Scaling(1f,1f,-1f)*ChartMatrix.Translation(0,-12f,0)*defOrient;
				default:
					return ChartMatrix.Translation(0,-45f,0)*defOrient;
			}
		}

		///<summary>DirectX's DrawScenePerio and DrawPerioRow, 3D layer only: the four rows of teeth and their implants. The rows' decorations, the
		///numbers and the legend are 2D-layer items of TC-7.</summary>
		private static void AddPerioScene(SceneBuilding building) {
			ToothChartData tcData=building.TcData;
			float baseY=GetPerioBaseY(tcData);
			List<ToothGraphic> listMaxillary=new List<ToothGraphic>();
			List<ToothGraphic> listMandible=new List<ToothGraphic>();
			for(int t=0;t<tcData.ListToothGraphics.Count;t++) {
				if(tcData.ListToothGraphics[t].ToothID=="implant") {
					continue;
				}
				if(ToothGraphic.IsMaxillary(tcData.ListToothGraphics[t].ToothID)) {
					listMaxillary.Add(tcData.ListToothGraphics[t]);
				}
				else {
					listMandible.Add(tcData.ListToothGraphics[t]);
				}
			}
			AddPerioRow(building,listMaxillary,GetPerioRowOrientation(PerioRow.MaxillaryFacial,baseY),"maxillary facial row");
			AddPerioRow(building,listMaxillary,GetPerioRowOrientation(PerioRow.MaxillaryLingual,baseY),"maxillary lingual row");
			AddPerioRow(building,listMandible,GetPerioRowOrientation(PerioRow.MandibularLingual,baseY),"mandibular lingual row");
			AddPerioRow(building,listMandible,GetPerioRowOrientation(PerioRow.MandibularFacial,baseY),"mandibular facial row");
		}

		private static void AddPerioRow(SceneBuilding building,List<ToothGraphic> listToothGraphics,ChartMatrix orientation,string rowName) {
			ToothChartData tcData=building.TcData;
			for(int t=0;t<listToothGraphics.Count;t++) {
				ToothGraphic toothGraphic=listToothGraphics[t];
				ChartMatrix world=ChartMatrix.Translation(tcData.GetTransX(toothGraphic.ToothID),0,0)*orientation;
				if(toothGraphic.Visible || (toothGraphic.IsCrown && toothGraphic.IsImplant) || toothGraphic.IsPontic) {
					AddTooth(building,toothGraphic,world,toothGraphic.ToothID+" "+rowName);
				}
				if(toothGraphic.IsImplant) {
					AddImplant(building,toothGraphic,world,"implant of "+toothGraphic.ToothID+" "+rowName);
				}
			}
		}
		#endregion Perio scene

		#region 2D layer
		///<summary>DirectX's DrawWatches and RenderToothWatch: a "W" for each watched tooth, a bigger white one first as its outline. A permanent tooth's
		///watch shows even when the tooth is missing; a primary tooth's only when it is visible, and it takes its permanent tooth's place.</summary>
		private static void AddWatches(SceneBuilding building) {
			ToothChartData tcData=building.TcData;
			//A Hashtable keyed by the permanent tooth, as DirectX keeps it: its order is the order of the watches.
			Hashtable hashtableWatchTeeth=new Hashtable(tcData.ListToothGraphics.Count);
			for(int t=0;t<tcData.ListToothGraphics.Count;t++) {//loop through each adult tooth
				ToothGraphic toothGraphic=tcData.ListToothGraphics[t];
				if(toothGraphic.ToothID=="implant" || !toothGraphic.Watch || Tooth.IsPrimary(toothGraphic.ToothID)) {
					continue;
				}
				hashtableWatchTeeth[toothGraphic.ToothID]=toothGraphic;
			}
			for(int t=0;t<tcData.ListToothGraphics.Count;t++) {//loop through each primary tooth
				ToothGraphic toothGraphic=tcData.ListToothGraphics[t];
				if(toothGraphic.ToothID=="implant" || !toothGraphic.Watch || !Tooth.IsPrimary(toothGraphic.ToothID) || !toothGraphic.Visible) {
					continue;
				}
				hashtableWatchTeeth[Tooth.PriToPerm(toothGraphic.ToothID)]=toothGraphic;
			}
			foreach(DictionaryEntry dictionaryEntry in hashtableWatchTeeth) {
				ToothGraphic toothGraphic=(ToothGraphic)dictionaryEntry.Value;
				ChartMatrix toothTrans;
				if(ToothGraphic.IsRight(toothGraphic.ToothID)) {
					toothTrans=ChartMatrix.Translation(tcData.GetTransX(toothGraphic.ToothID)+toothGraphic.ShiftM,0,0);
				}
				else {
					toothTrans=ChartMatrix.Translation(tcData.GetTransX(toothGraphic.ToothID)-toothGraphic.ShiftM,0,0);
				}
				float toMm=1f/tcData.ScaleMmToPix;
				string source="watch of "+toothGraphic.ToothID;
				if(ToothGraphic.IsMaxillary(toothGraphic.ToothID)) {
					AddText(building,"W",toothTrans,tcData.PixelScaleRatio*(-8f*toMm),tcData.PixelScaleRatio*(155f*toMm),-6f,Color.White,building.FontWatchBig,
						source+" (outline)");
					AddText(building,"W",toothTrans,tcData.PixelScaleRatio*(-6f*toMm),tcData.PixelScaleRatio*(153f*toMm),-6f,toothGraphic.colorWatch,
						building.FontWatch,source);
				}
				else {
					AddText(building,"W",toothTrans,tcData.PixelScaleRatio*(-8f*toMm),tcData.PixelScaleRatio*(-136f*toMm),-6f,Color.White,building.FontWatchBig,
						source+" (outline)");
					AddText(building,"W",toothTrans,tcData.PixelScaleRatio*(-6f*toMm),tcData.PixelScaleRatio*(-138f*toMm),-6f,toothGraphic.colorWatch,
						building.FontWatch,source);
				}
			}
		}

		///<summary>DirectX's DrawDrawingSegments: every saved drawing in its colour, then the points of the drawing in progress in the pen colour.</summary>
		private static void AddDrawingSegments(SceneBuilding building) {
			ToothChartData tcData=building.TcData;
			for(int s=0;s<tcData.DrawingSegmentList.Count;s++) {
				string[] arrayPointStr=tcData.DrawingSegmentList[s].DrawingSegment.Split(';');
				List<Vertex3f> listPoints=new List<Vertex3f>();
				for(int p=0;p<arrayPointStr.Length;p++) {
					string[] arrayXy=arrayPointStr[p].Split(',');
					float x;
					float y;
					if(IsValidCoordinate(arrayXy,out x,out y)) {
						PointF pointMm=tcData.PointDrawingPixToMm(new PointF(x,y));
						listPoints.Add(new Vertex3f(pointMm.X,pointMm.Y,0f));
					}
				}
				AddExtendedLineStrip(building,ChartMatrix.Identity,0.08f,true,tcData.DrawingSegmentList[s].ColorDraw,0.71f,"drawing "+s,listPoints.ToArray());
			}
			List<Vertex3f> listCurrent=new List<Vertex3f>();
			for(int i=0;i<tcData.PointList.Count;i++) {
				PointF pointMm=tcData.PointPixToMm(tcData.PointList[i]);
				listCurrent.Add(new Vertex3f(pointMm.X,pointMm.Y,0));
			}
			AddExtendedLineStrip(building,ChartMatrix.Identity,0.08f,true,tcData.ColorDrawing,0.71f,"drawing in progress",listCurrent.ToArray());
		}

		///<summary>DirectX's IsValidCoordinate: "x,y" with both parsed by float.TryParse in the current culture.</summary>
		private static bool IsValidCoordinate(string[] arrayCoordinate,out float x,out float y) {
			x=0;
			y=0;
			return arrayCoordinate.Length==2 && float.TryParse(arrayCoordinate[0],out x) && float.TryParse(arrayCoordinate[1],out y);
		}

		///<summary>DirectX's DrawNumbersAndLines: the white centre line, then the number of each tooth 1-32 and A-T in order, selected ones on their
		///highlight rectangle.</summary>
		private static void AddNumbersAndLines(SceneBuilding building) {
			ToothChartData tcData=building.TcData;
			AddLineList(building,ChartMatrix.Identity,Color.White,0.32f,"centre line",new Vertex3f(-65f,0,0),new Vertex3f(65f,0,0));
			for(int i=1;i<=52;i++) {
				string toothId=Tooth.FromOrdinal(i);
				AddNumber(building,toothId,tcData.SelectedTeeth.Contains(toothId),0);
			}
		}

		///<summary>DirectX's DrawNumber: nothing for a hidden number or a primary tooth whose letter is not shown; otherwise the highlight rectangle
		///when selected, then the number, a tenth of the box's width in from its left.</summary>
		private static void AddNumber(SceneBuilding building,string toothId,bool isSelected,float offsetY) {
			ToothChartData tcData=building.TcData;
			if(!Tooth.IsValidDB(toothId)) {
				return;
			}
			if(tcData.ListToothGraphics[toothId].HideNumber) {//if this is a "hidden" number
				return;//skip
			}
			//primary, but not set to show primary letters
			if(Tooth.IsPrimary(toothId) && !tcData.ListToothGraphics[Tooth.PriToPerm(toothId)].ShowPrimaryLetter) {
				return;
			}
			string displayNum=Tooth.DisplayGraphic(toothId,tcData.ToothNumberingNomenclature);
			SizeF labelSize=MeasureStringMm(building,displayNum);
			RectangleF recMm=tcData.GetNumberRecMm(toothId,labelSize);
			recMm.Y+=offsetY;
			Color foreColor=tcData.ColorText;
			if(isSelected) {
				foreColor=tcData.ColorTextHighlight;
				//Draw the background rectangle only if the text is selected.
				AddRectangle(building,ChartMatrix.Identity,recMm,tcData.ColorBackHighlight,"highlight of "+toothId);
			}
			//Offsets the text by 10% of the rectangle width to ensure that there is at least on pixel of space on the left for the background rectangle.
			AddText(building,displayNum,ChartMatrix.Identity,recMm.X+recMm.Width*0.1f,recMm.Y+recMm.Height,0,foreColor,building.FontNumbers,"number "+toothId);
		}

		///<summary>DirectX's MeasureStringMm: the text's size in the numbers' font, the width times 1.25 and the height times 0.8 (D3DX adds more
		///vertical and less horizontal room than GDI+), in mm.</summary>
		public static SizeF MeasureStringMm(SceneBuilding building,string text) {
			if(string.IsNullOrEmpty(text)) {
				return new SizeF(0,0);
			}
			Size sizeText=ChartFontMeasure.MeasureText(building.FontNumbers,text);
			float width=sizeText.Width;
			float height=sizeText.Height;
			return new SizeF((width*1.25f)/building.TcData.ScaleMmToPix,(height*0.8f)/building.TcData.ScaleMmToPix);
		}

		///<summary>DirectX's PrintString: the D3DX text's top left goes to the ceiling of x and the floor of y of the point's projection.</summary>
		private static void AddText(SceneBuilding building,string text,ChartMatrix world,float x,float y,float z,Color color,ChartFont font,string source) {
			if(string.IsNullOrEmpty(text)) {
				return;
			}
			float xScreen;
			float yScreen;
			building.Scene.ProjectDirectX(world,x,y,z,out xScreen,out yScreen);
			Point origin=new Point((int)Math.Ceiling(xScreen),(int)Math.Floor(yScreen));
			int widthPixels=ChartFontMeasure.MeasureText(font,text).Width;
			building.ListItems2D.Add(ToothSceneItem2D.CreateText(text,color,font,origin,widthPixels,source));
		}

		///<summary>DirectX's DrawColoredRectangle: the rectangle in mm (origin at the lower left) as one polygon.</summary>
		private static void AddRectangle(SceneBuilding building,ChartMatrix world,RectangleF rect,Color color,string source) {
			Vertex3f[] arrayVerts=new Vertex3f[] {
				new Vertex3f(rect.Left,rect.Bottom,0),
				new Vertex3f(rect.Left,rect.Top,0),
				new Vertex3f(rect.Right,rect.Top,0),
				new Vertex3f(rect.Right,rect.Bottom,0),
			};
			List<PointF[]> listPolygons=new List<PointF[]>();
			listPolygons.Add(ProjectPolygon(building,world,arrayVerts));
			building.ListItems2D.Add(ToothSceneItem2D.CreatePolygons(color,listPolygons,source));
		}

		///<summary>DirectX's DrawExtendedLineStrip: the strip broken into two-point lines, each line lengthened by extendDist at the joints (and at the
		///ends when extendEndPoints), then drawn as DrawLineList draws them.</summary>
		private static void AddExtendedLineStrip(SceneBuilding building,ChartMatrix world,float extendDist,bool extendEndPoints,Color color,float lineWidthMm,
			string source,params Vertex3f[] arrayPoints)
		{
			if(arrayPoints.Length<2) {
				return;//Can happen with drawings somehow.
			}
			List<Vertex3f> listLines=new List<Vertex3f>();
			for(int p=0;p<arrayPoints.Length-1;p++) {
				Vertex3f p1=arrayPoints[p];
				Vertex3f p2=arrayPoints[p+1];
				bool p1IsEndPoint=(p==0);
				bool p2IsEndPoint=(p==arrayPoints.Length-2);
				float dx=p2.X-p1.X;
				float dy=p2.Y-p1.Y;
				float dz=p2.Z-p1.Z;
				float length=(float)Math.Sqrt((dx*dx)+(dy*dy)+(dz*dz));
				if(length==0) {
					continue;
				}
				//SharpDX's Vector3.Normalize: multiply by 1/length.
				float inverse=1f/length;
				dx*=inverse;
				dy*=inverse;
				dz*=inverse;
				float x1=p1.X;
				float y1=p1.Y;
				float z1=p1.Z;
				float x2=p2.X;
				float y2=p2.Y;
				float z2=p2.Z;
				//Do not extend the endpoints for the ends of the line strips unless extendEndPoints=true.
				if(!p1IsEndPoint || extendEndPoints) {
					x1=x1-extendDist*dx;
					y1=y1-extendDist*dy;
					z1=z1-extendDist*dz;
				}
				if(!p2IsEndPoint || extendEndPoints) {
					x2=x2+extendDist*dx;
					y2=y2+extendDist*dy;
					z2=z2+extendDist*dz;
				}
				listLines.Add(new Vertex3f(x1,y1,z1));
				listLines.Add(new Vertex3f(x2,y2,z2));
			}
			AddLineList(building,world,color,lineWidthMm,source,listLines.ToArray());
		}

		///<summary>DirectX's DrawLineList: each pair of points becomes a rectangle lineWidthMm wide (two triangles in DirectX), in the plane of the
		///points, transformed by world and projected. One 2D item for the list.</summary>
		private static void AddLineList(SceneBuilding building,ChartMatrix world,Color color,float lineWidthMm,string source,params Vertex3f[] arrayPoints) {
			List<PointF[]> listPolygons=new List<PointF[]>();
			for(int i=0;i<arrayPoints.Length-1;i+=2) {
				Vertex3f p1=arrayPoints[i];
				Vertex3f p2=arrayPoints[i+1];
				float vx=p2.X-p1.X;
				float vy=p2.Y-p1.Y;
				float vz=p2.Z-p1.Z;
				if((float)Math.Sqrt((vx*vx)+(vy*vy)+(vz*vz))==0) {
					continue;
				}
				//Our "up" vector. Z component does not matter because of orthographic projection.
				float ux=-vy;
				float uy=vx;
				float lengthUp=(float)Math.Sqrt((ux*ux)+(uy*uy));
				if(lengthUp==0) {
					continue;//a line straight along Z has no width on screen
				}
				//SharpDX's Normalize (times 1/length), then times half the width, as DirectX computes it.
				float inverse=1f/lengthUp;
				ux=ux*inverse*(lineWidthMm/2);
				uy=uy*inverse*(lineWidthMm/2);
				Vertex3f[] arrayQuad=new Vertex3f[] {
					new Vertex3f(p1.X+ux,p1.Y+uy,p1.Z),
					new Vertex3f(p2.X+ux,p2.Y+uy,p2.Z),
					new Vertex3f(p2.X-ux,p2.Y-uy,p2.Z),
					new Vertex3f(p1.X-ux,p1.Y-uy,p1.Z),
				};
				listPolygons.Add(ProjectPolygon(building,world,arrayQuad));
			}
			if(listPolygons.Count==0) {
				return;
			}
			building.ListItems2D.Add(ToothSceneItem2D.CreatePolygons(color,listPolygons,source));
		}

		///<summary>The silhouette of a 3D group as DirectX shows a group drawn with depth testing off and only an emissive colour: the union of its
		///projected triangles, each turned the same way and all filled with the Nonzero rule. Flat colour, no depth, no culling.</summary>
		private static void AddSilhouette(SceneBuilding building,ToothGraphic toothGraphic,int indexGroup,ChartMatrix world,Color color,string source) {
			if(indexGroup<0) {
				return;
			}
			List<int> listTriangles=toothGraphic.Groups[indexGroup].GetTriangleIndices();
			if(listTriangles.Count<3) {
				return;
			}
			PointF[] arrayProjected=new PointF[toothGraphic.VertexNormals.Count];
			bool[] arrayIsProjected=new bool[toothGraphic.VertexNormals.Count];
			List<PointF[]> listPolygons=new List<PointF[]>();
			for(int i=0;i+2<listTriangles.Count;i+=3) {
				PointF[] arrayTriangle=new PointF[3];
				for(int v=0;v<3;v++) {
					int index=listTriangles[i+v];
					if(!arrayIsProjected[index]) {
						Vertex3f vertex=toothGraphic.VertexNormals[index].Vertex;
						arrayProjected[index]=building.Scene.ProjectToImage(world,vertex.X,vertex.Y,vertex.Z);
						arrayIsProjected[index]=true;
					}
					arrayTriangle[v]=arrayProjected[index];
				}
				listPolygons.Add(arrayTriangle);
			}
			building.ListItems2D.Add(ToothSceneItem2D.CreatePolygons(color,listPolygons,source));
		}

		private static PointF[] ProjectPolygon(SceneBuilding building,ChartMatrix world,Vertex3f[] arrayVerts) {
			PointF[] arrayPoints=new PointF[arrayVerts.Length];
			for(int i=0;i<arrayVerts.Length;i++) {
				arrayPoints[i]=building.Scene.ProjectToImage(world,arrayVerts[i].X,arrayVerts[i].Y,arrayVerts[i].Z);
			}
			return arrayPoints;
		}
		#endregion 2D layer

		#region Fonts
		///<summary>DirectX's PrepareForDirectX: the numbers' font, Arial with a cell height of 14 and an average width of 5 pixels at the original
		///size, both times the square root of the pixel scale ratio.</summary>
		public static ChartFont GetFontNumbers(float pixelScaleRatio) {
			return ChartFontMeasure.GetFont("Arial",(int)Math.Round((float)(14*Math.Sqrt(pixelScaleRatio))),(int)Math.Round((float)(5*Math.Sqrt(pixelScaleRatio))),400);
		}

		///<summary>The sealant's "S": Arial 25/9 times the ratio.</summary>
		public static ChartFont GetFontSealant(float pixelScaleRatio) {
			return ChartFontMeasure.GetFont("Arial",(int)Math.Round(25*pixelScaleRatio),(int)Math.Round(9*pixelScaleRatio),400);
		}

		///<summary>The watch's "W": Arial 15/6 times the ratio.</summary>
		public static ChartFont GetFontWatch(float pixelScaleRatio) {
			return ChartFontMeasure.GetFont("Arial",(int)Math.Round(15*pixelScaleRatio),(int)Math.Round(6*pixelScaleRatio),400);
		}

		///<summary>The white outline behind the watch's "W": Times New Roman 19/7.4 times the ratio, ultra bold.</summary>
		public static ChartFont GetFontWatchBig(float pixelScaleRatio) {
			return ChartFontMeasure.GetFont("Times New Roman",(int)Math.Round(19*pixelScaleRatio),(int)Math.Round(7.4*pixelScaleRatio),800);
		}
		#endregion Fonts

		///<summary>The state of one Build: the data, an empty scene of the right size and projection for the projections, the fonts and the item
		///lists in the order they are added.</summary>
		public class SceneBuilding {
			public readonly ToothChartData TcData;
			///<summary>The scene's size and projection, with no items: ProjectDirectX and ProjectToImage need only those.</summary>
			public readonly ToothScene Scene;
			public readonly List<ToothSceneItem3D> ListItems3D=new List<ToothSceneItem3D>();
			public readonly List<ToothSceneItem2D> ListItems2D=new List<ToothSceneItem2D>();
			private ChartFont _fontNumbers;
			private ChartFont _fontSealant;
			private ChartFont _fontWatch;
			private ChartFont _fontWatchBig;

			public SceneBuilding(ToothChartData tcData,Size size,float widthProj,float heightProj) {
				TcData=tcData;
				Scene=new ToothScene(size,tcData.ColorBackground,tcData.PerioMode,widthProj,heightProj,new List<ToothSceneItem3D>(),new List<ToothSceneItem2D>());
			}

			public ChartFont FontNumbers {
				get {
					if(_fontNumbers==null) {
						_fontNumbers=GetFontNumbers(TcData.PixelScaleRatio);
					}
					return _fontNumbers;
				}
			}

			public ChartFont FontSealant {
				get {
					if(_fontSealant==null) {
						_fontSealant=GetFontSealant(TcData.PixelScaleRatio);
					}
					return _fontSealant;
				}
			}

			public ChartFont FontWatch {
				get {
					if(_fontWatch==null) {
						_fontWatch=GetFontWatch(TcData.PixelScaleRatio);
					}
					return _fontWatch;
				}
			}

			public ChartFont FontWatchBig {
				get {
					if(_fontWatchBig==null) {
						_fontWatchBig=GetFontWatchBig(TcData.PixelScaleRatio);
					}
					return _fontWatchBig;
				}
			}
		}
	}

	///<summary>GDI fonts and text widths for the 2D layer. The DirectX chart's D3DX fonts are GDI fonts (D3DXCreateFont passes its height, width
	///and weight to CreateFont), so GDI gives their exact metrics and widths, with no WPF object, on any thread. Fonts and widths are cached; a lock
	///keeps the one memory DC to one thread at a time.</summary>
	public static class ChartFontMeasure {
		private static readonly object _lock=new object();
		private static IntPtr _hdc=IntPtr.Zero;
		private static Dictionary<string,FontEntry> _dictFonts=new Dictionary<string,FontEntry>();
		private static Dictionary<string,Size> _dictSizes=new Dictionary<string,Size>();
		private const int MaxFonts=64;
		private const int MaxSizes=4096;

		private class FontEntry {
			public IntPtr HFont;
			public ChartFont ChartFont;
		}

		///<summary>The font as D3DX makes it: CreateFont(height, width, 0, 0, weight, no italic, ANSI_CHARSET, OUT_DEVICE_PRECIS, CLIP_DEFAULT_PRECIS,
		///CLEARTYPE_QUALITY, DEFAULT_PITCH, face), with GDI's ascent and em height.</summary>
		public static ChartFont GetFont(string faceName,int height,int width,int weight) {
			lock(_lock) {
				return GetFontEntry(faceName,height,width,weight).ChartFont;
			}
		}

		///<summary>The size of one line of text in the font, as GDI's GetTextExtentPoint32 gives it: the advance width and the cell height.</summary>
		public static Size MeasureText(ChartFont chartFont,string text) {
			if(string.IsNullOrEmpty(text)) {
				return new Size(0,chartFont.Height);
			}
			string key=GetKey(chartFont.FaceName,chartFont.Height,chartFont.Width,chartFont.Weight)+"\n"+text;
			lock(_lock) {
				Size size;
				if(_dictSizes.TryGetValue(key,out size)) {
					return size;
				}
				FontEntry fontEntry=GetFontEntry(chartFont.FaceName,chartFont.Height,chartFont.Width,chartFont.Weight);
				IntPtr hdc=GetDC();
				IntPtr hFontOld=SelectObject(hdc,fontEntry.HFont);
				try {
					SIZE sizeNative;
					if(!GetTextExtentPoint32(hdc,text,text.Length,out sizeNative)) {
						throw new InvalidOperationException("GDI could not measure the text \""+text+"\" in "+chartFont+".");
					}
					size=new Size(sizeNative.cx,sizeNative.cy);
				}
				finally {
					SelectObject(hdc,hFontOld);
				}
				if(_dictSizes.Count>=MaxSizes) {
					_dictSizes.Clear();
				}
				_dictSizes[key]=size;
				return size;
			}
		}

		private static string GetKey(string faceName,int height,int width,int weight) {
			return faceName+"|"+height+"|"+width+"|"+weight;
		}

		private static FontEntry GetFontEntry(string faceName,int height,int width,int weight) {
			string key=GetKey(faceName,height,width,weight);
			FontEntry fontEntry;
			if(_dictFonts.TryGetValue(key,out fontEntry)) {
				return fontEntry;
			}
			if(_dictFonts.Count>=MaxFonts) {
				foreach(FontEntry fontEntryOld in _dictFonts.Values) {
					DeleteObject(fontEntryOld.HFont);
				}
				_dictFonts.Clear();
				_dictSizes.Clear();
			}
			const int ANSI_CHARSET=0;
			const int OUT_DEVICE_PRECIS=5;
			const int CLIP_DEFAULT_PRECIS=0;
			const int CLEARTYPE_QUALITY=5;
			const int DEFAULT_PITCH=0;
			IntPtr hFont=CreateFont(height,width,0,0,weight,0,0,0,ANSI_CHARSET,OUT_DEVICE_PRECIS,CLIP_DEFAULT_PRECIS,CLEARTYPE_QUALITY,DEFAULT_PITCH,faceName);
			if(hFont==IntPtr.Zero) {
				throw new InvalidOperationException("GDI could not make the font "+key+".");
			}
			IntPtr hdc=GetDC();
			IntPtr hFontOld=SelectObject(hdc,hFont);
			TEXTMETRIC textMetric;
			try {
				if(!GetTextMetrics(hdc,out textMetric)) {
					throw new InvalidOperationException("GDI gave no metrics for the font "+key+".");
				}
			}
			finally {
				SelectObject(hdc,hFontOld);
			}
			fontEntry=new FontEntry();
			fontEntry.HFont=hFont;
			fontEntry.ChartFont=new ChartFont(faceName,height,width,weight,textMetric.tmAscent,textMetric.tmHeight-textMetric.tmInternalLeading);
			_dictFonts[key]=fontEntry;
			return fontEntry;
		}

		private static IntPtr GetDC() {
			if(_hdc==IntPtr.Zero) {
				_hdc=CreateCompatibleDC(IntPtr.Zero);
				if(_hdc==IntPtr.Zero) {
					throw new InvalidOperationException("GDI could not make a memory device context.");
				}
			}
			return _hdc;
		}

		#region Native
		[StructLayout(LayoutKind.Sequential)]
		private struct SIZE {
			public int cx;
			public int cy;
		}

		[StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
		private struct TEXTMETRIC {
			public int tmHeight;
			public int tmAscent;
			public int tmDescent;
			public int tmInternalLeading;
			public int tmExternalLeading;
			public int tmAveCharWidth;
			public int tmMaxCharWidth;
			public int tmWeight;
			public int tmOverhang;
			public int tmDigitizedAspectX;
			public int tmDigitizedAspectY;
			public char tmFirstChar;
			public char tmLastChar;
			public char tmDefaultChar;
			public char tmBreakChar;
			public byte tmItalic;
			public byte tmUnderlined;
			public byte tmStruckOut;
			public byte tmPitchAndFamily;
			public byte tmCharSet;
		}

		[DllImport("gdi32.dll",CharSet=CharSet.Unicode,EntryPoint="CreateFontW")]
		private static extern IntPtr CreateFont(int nHeight,int nWidth,int nEscapement,int nOrientation,int fnWeight,uint fdwItalic,uint fdwUnderline,
			uint fdwStrikeOut,uint fdwCharSet,uint fdwOutputPrecision,uint fdwClipPrecision,uint fdwQuality,uint fdwPitchAndFamily,string lpszFace);

		[DllImport("gdi32.dll")]
		private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

		[DllImport("gdi32.dll")]
		private static extern IntPtr SelectObject(IntPtr hdc,IntPtr hgdiobj);

		[DllImport("gdi32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool DeleteObject(IntPtr hObject);

		[DllImport("gdi32.dll",CharSet=CharSet.Unicode,EntryPoint="GetTextMetricsW")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GetTextMetrics(IntPtr hdc,out TEXTMETRIC lptm);

		[DllImport("gdi32.dll",CharSet=CharSet.Unicode,EntryPoint="GetTextExtentPoint32W")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GetTextExtentPoint32(IntPtr hdc,string lpString,int c,out SIZE lpSize);
		#endregion Native
	}
}
