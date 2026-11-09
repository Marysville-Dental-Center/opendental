// The parity probes of the WPF chart against today's DirectX pictures (package TC-3, docs/specs/wpf-tooth-chart-plan.md section 7): surface colours
// at every tooth's surface centres, and the boxes of the numbers, the sealant "S" and the watch "W" within one pixel.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using SparksToothChart;
using SparksToothChart.Wpf3D;

namespace ToothChartImageTests {
	///<summary>Every chart scene that TC-0 recorded in DirectX, at each recorded size, probed in the WPF picture and in the DirectX picture. The WPF
	///pictures are the committed baselines (Baselines\wpf-net48), which ChartScenes_MatchTheWpfBaselines checks against fresh renders; with
	///OPMS_PARITY_FRESH=1 the probes render every scene again instead (for tuning, before the baselines are recorded). Each test writes its results
	///per scene group to %TEMP%\ToothChartImageTests (parity-probes.txt, parity-text.txt) and fails on any difference the allowance list does not
	///cover, or on an allowance that covers nothing.</summary>
	[TestClass]
	public class ParityProbeTests {
		public const string EnvFresh="OPMS_PARITY_FRESH";
		///<summary>A text box of the WPF chart is where DirectX's is when every edge of its ink is within this many pixels.</summary>
		public const int MaxTextEdgePixels=1;

		[TestMethod]
		public void ParityProbes_SurfaceCentres_MatchTheDirectXRecordings() {
			List<ParityProbeOutcome> listOutcomes=new List<ParityProbeOutcome>();
			List<string> listMissing=new List<string>();
			ForEachRecordedPicture((scene,chartSize,wrapper,arrayDirectX,arrayWpf) => {
				List<ParityProbePoint> listPoints=ParityProbes.GetPoints(wrapper.TcData);
				listOutcomes.AddRange(ParityProbes.Probe(scene,chartSize,listPoints,arrayDirectX,arrayWpf,wrapper.TcData.ColorBackground));
			},listMissing);
			List<ParityProbeOutcome> listFailed=listOutcomes.Where(x => x.Failure!=null && x.Allowance==null).ToList();
			List<ParityProbeOutcome> listAllowed=listOutcomes.Where(x => x.Failure!=null && x.Allowance!=null).ToList();
			StringBuilder sb=new StringBuilder();
			sb.AppendLine("Parity probes of the WPF chart against the DirectX recordings (plan section 7): "+listOutcomes.Count+" probes, "
				+listOutcomes.Count(x => x.Failure==null)+" pass, "+listAllowed.Count+" differ and are allowed, "+listFailed.Count+" fail.");
			sb.AppendLine("Rules: hue within "+ParityProbes.MaxHueDegrees+" degrees for a coloured sample, lightness within "+ParityProbes.MaxLightnessPoints
				+" points, tooth versus background agreed at missing and hidden teeth; a surface sample is the mean of 3x3 pixels at the surface centre, "
				+"a line or X sample one pixel.");
			sb.AppendLine("WPF pictures: "+(IsFresh()?"rendered again ("+EnvFresh+"=1)":"the committed baselines"));
			sb.AppendLine("By scene group:");
			AppendPerGroup(sb,listOutcomes,x => x.SceneGroup,x => x.Failure==null,x => x.Failure!=null && x.Allowance!=null);
			sb.AppendLine("By kind of probe:");
			AppendPerGroup(sb,listOutcomes,x => x.Probe.Kind.ToString(),x => x.Failure==null,x => x.Failure!=null && x.Allowance!=null);
			sb.AppendLine("By size:");
			AppendPerGroup(sb,listOutcomes,x => x.SizeName,x => x.Failure==null,x => x.Failure!=null && x.Allowance!=null);
			foreach(var group in listOutcomes.Where(x => x.Probe.IsToothDrawn).GroupBy(x => x.Probe.Kind)) {
				sb.AppendLine("Lightness of WPF minus DirectX, "+group.Key+": mean "+group.Select(x => GetLightnessDelta(x)).Average().ToString("0.0",CultureInfo.InvariantCulture)
					+" points, largest "+group.Select(x => Math.Abs(GetLightnessDelta(x))).Max().ToString("0.0",CultureInfo.InvariantCulture)+".");
			}
			foreach(ParityAllowance parityAllowance in ParityProbes.Allowances) {
				sb.AppendLine("Allowance ("+listAllowed.Count(x => x.Allowance==parityAllowance)+" probes): "+parityAllowance);
			}
			foreach(ParityProbeOutcome outcome in listAllowed) {
				sb.AppendLine("Allowed: "+outcome);
			}
			foreach(ParityProbeOutcome outcome in listFailed.Take(400)) {
				sb.AppendLine("Failed: "+outcome);
			}
			foreach(string missing in listMissing) {
				sb.AppendLine("Missing: "+missing);
			}
			WpfCharts.WriteEvidence("parity-probes.txt",sb.ToString());
			Assert.AreEqual(0,listMissing.Count,"Pictures missing:\r\n"+string.Join("\r\n",listMissing));
			Assert.IsTrue(listOutcomes.Count>15000,"Probes: "+listOutcomes.Count);
			foreach(ProbeKind probeKind in Enum.GetValues(typeof(ProbeKind))) {
				Assert.IsTrue(listOutcomes.Count(x => x.Probe.Kind==probeKind)>=20,"Probes of "+probeKind+": "+listOutcomes.Count(x => x.Probe.Kind==probeKind));
			}
			//The known-difference allowances stay listed when their scenes' probes happen to agree (the covered part may miss the probe point);
			//every other allowance must still cover a probe, or it goes.
			List<ParityAllowance> listUnused=ParityProbes.Allowances.Where(x => !x.IsKnownDifference && !listAllowed.Any(y => y.Allowance==x)).ToList();
			Assert.AreEqual(0,listUnused.Count,"Allowances that cover nothing (remove them): "+string.Join("; ",listUnused.Select(x => x.SceneName)));
			Assert.AreEqual(0,listFailed.Count,listFailed.Count+" probes differ from DirectX:\r\n"+string.Join("\r\n",listFailed.Take(60)));
		}

		[TestMethod]
		public void ParityProbes_NumbersAndMarks_SitWhereDirectXDrawsThem_WithinOnePixel() {
			List<TextBoxOutcome> listOutcomes=new List<TextBoxOutcome>();
			List<string> listMissing=new List<string>();
			ForEachRecordedPicture((scene,chartSize,wrapper,arrayDirectX,arrayWpf) => {
				ToothScene toothScene=ToothSceneBuilder.Build(wrapper.TcData);
				foreach(ToothSceneItem2D item in toothScene.Items2D.Where(x => x.Kind==ChartItem2DKind.Text)) {
					if(item.Source.EndsWith("(outline)")) {
						continue;//the white W behind the coloured one: its colour is the colour of the enamel and the numbers' highlight
					}
					TextBoxOutcome outcome=ProbeText(scene,chartSize,wrapper.TcData,item,arrayDirectX,arrayWpf);
					if(outcome!=null) {
						listOutcomes.Add(outcome);
					}
				}
			},listMissing);
			foreach(TextBoxOutcome outcome in listOutcomes.Where(x => x.Failure!=null)) {
				outcome.Allowance=TextAllowances.FirstOrDefault(x => x.Covers(outcome));
			}
			List<TextBoxOutcome> listFailed=listOutcomes.Where(x => x.Failure!=null && x.Allowance==null).ToList();
			List<TextBoxOutcome> listAllowed=listOutcomes.Where(x => x.Failure!=null && x.Allowance!=null).ToList();
			StringBuilder sb=new StringBuilder();
			sb.AppendLine("Text boxes of the WPF chart against the DirectX recordings: "+listOutcomes.Count+" numbers and marks, "+listAllowed.Count+" off and allowed, "
				+listFailed.Count+" with an edge of their ink more than "+MaxTextEdgePixels+" pixel from DirectX's.");
			AppendPerGroup(sb,listOutcomes,x => x.Kind,x => x.Failure==null,x => x.Failure!=null && x.Allowance!=null);
			sb.AppendLine("By size:");
			AppendPerGroup(sb,listOutcomes,x => x.SizeName,x => x.Failure==null,x => x.Failure!=null && x.Allowance!=null);
			foreach(TextAllowance textAllowance in TextAllowances) {
				sb.AppendLine("Allowance ("+listAllowed.Count(x => x.Allowance==textAllowance)+" boxes): "+textAllowance);
			}
			foreach(TextBoxOutcome outcome in listAllowed) {
				sb.AppendLine("Allowed: "+outcome);
			}
			foreach(var group in listOutcomes.GroupBy(x => x.Kind)) {
				sb.AppendLine(group.Key+": mean edge offsets WPF minus DirectX left "+Mean(group,x => x.DeltaLeft)+", top "+Mean(group,x => x.DeltaTop)+", right "
					+Mean(group,x => x.DeltaRight)+", bottom "+Mean(group,x => x.DeltaBottom));
			}
			foreach(TextBoxOutcome outcome in listFailed.Take(300)) {
				sb.AppendLine("Failed: "+outcome);
			}
			WpfCharts.WriteEvidence("parity-text.txt",sb.ToString());
			Assert.AreEqual(0,listMissing.Count,"Pictures missing:\r\n"+string.Join("\r\n",listMissing));
			Assert.IsTrue(listOutcomes.Count(x => x.Kind=="number")>5000,"Numbers: "+listOutcomes.Count(x => x.Kind=="number"));
			Assert.IsTrue(listOutcomes.Count(x => x.Kind=="sealant")>50,"Sealants: "+listOutcomes.Count(x => x.Kind=="sealant"));
			Assert.IsTrue(listOutcomes.Count(x => x.Kind=="watch")>20,"Watches: "+listOutcomes.Count(x => x.Kind=="watch"));
			List<TextAllowance> listUnused=TextAllowances.Where(x => !listAllowed.Any(y => y.Allowance==x)).ToList();
			Assert.AreEqual(0,listUnused.Count,"Text allowances that cover nothing (remove them): "+string.Join("; ",listUnused));
			Assert.AreEqual(0,listFailed.Count,listFailed.Count+" text boxes are off:\r\n"+string.Join("\r\n",listFailed.Take(60)));
		}

		///<summary>A reviewed exception of the text boxes, with its reason. ItemSource "*" covers every item of its kind in the scene and size.</summary>
		public class TextAllowance {
			public string SceneName;
			public string SizeName;
			public string Kind;
			public string ItemSource;
			public string Reason;

			public TextAllowance(string sceneName,string sizeName,string kind,string itemSource,string reason) {
				SceneName=sceneName;
				SizeName=sizeName;
				Kind=kind;
				ItemSource=itemSource;
				Reason=reason;
			}

			public bool Covers(TextBoxOutcome outcome) {
				return outcome.SceneName==SceneName && outcome.SizeName==SizeName && outcome.Kind==Kind && (ItemSource=="*" || outcome.ItemSource==ItemSource);
			}

			public override string ToString() {
				return SizeName+"/"+SceneName+" "+(ItemSource=="*"?"every "+Kind:ItemSource)+": "+Reason;
			}
		}

		///<summary>The reviewed exceptions of the text boxes. Each was looked at pixel by pixel in both pictures.</summary>
		public static readonly List<TextAllowance> TextAllowances=new List<TextAllowance> {
			new TextAllowance("size-sheet-tall","360x480","number","*","The tall sheet's numbers use the D3DX font 13/5, which GDI condenses; the WPF chart "
				+"scales Arial across to GDI's width of each number (about 0.86), which puts the 1-pixel stems of these small digits between two pixels, so "
				+"their ink is lighter and the ink box shrinks. The glyphs sit in the same cells (checked pixel by pixel; the gallery G1 shows the scene)."),
		};

		[TestMethod]
		public void CentreLineAndHighlightBoxes_FillThePixelsDirectXFills() {
			//TC-3 snaps the 2D layer's axis-aligned rectangles to the pixels DirectX fills (ToothSceneItem2D.SnapToDirectXPixels). Checked on every
			//recorded picture without drawings (a stroke may cross the line) where the item's colour is not the background's: the centre line fills the
			//same rows as DirectX's at five columns, exactly; each highlight box's top row and its left and right ends on the row below it (above the number's
			//glyphs) are within one pixel of DirectX's (D3D's own subpixel snapping can put an edge a pixel over).
			List<string> listFailures=new List<string>();
			List<string> listOffByOne=new List<string>();
			int countLines=0;
			int countBoxes=0;
			List<string> listMissing=new List<string>();
			ForEachRecordedPicture((scene,chartSize,wrapper,arrayDirectX,arrayWpf) => {
				if(wrapper.TcData.DrawingSegmentList.Count>0) {
					return;
				}
				ToothScene toothScene=ToothSceneBuilder.Build(wrapper.TcData);
				foreach(ToothSceneItem2D item in toothScene.Items2D.Where(x => x.Source=="centre line" || x.Source.StartsWith("highlight of "))) {
					if(item.Color.ToArgb()==toothScene.ColorBackground.ToArgb()) {
						continue;//white on the printouts' white: nothing to see
					}
					string name=chartSize.FolderName+"/"+scene.Name+" "+item.Source;
					foreach(PointF[] arrayPoints in item.Polygons) {
						Rectangle rectangle=Rectangle.FromLTRB((int)arrayPoints.Min(x => x.X),(int)arrayPoints.Min(x => x.Y),(int)arrayPoints.Max(x => x.X),
							(int)arrayPoints.Max(x => x.Y));
						if(item.Source=="centre line") {
							countLines++;
							foreach(int x in new[] { rectangle.Left+3,rectangle.Left+rectangle.Width/4,rectangle.Left+rectangle.Width/2,rectangle.Left+3*rectangle.Width/4,
								rectangle.Right-4 })
							{
								int[] arrayDirectXRows=GetRun(arrayDirectX,chartSize.Size,item.Color,x,(rectangle.Top+rectangle.Bottom-1)/2,false);
								int[] arrayWpfRows=GetRun(arrayWpf,chartSize.Size,item.Color,x,(rectangle.Top+rectangle.Bottom-1)/2,false);
								if(!arrayDirectXRows.SequenceEqual(arrayWpfRows)) {
									listFailures.Add(name+" at column "+x+": DirectX fills rows "+Describe(arrayDirectXRows)+", WPF "+Describe(arrayWpfRows));
								}
							}
							continue;
						}
						countBoxes++;
						int yTop=rectangle.Top;
						int xMiddle=(rectangle.Left+rectangle.Right)/2;
						int[] arrayDirectXColumns=GetRun(arrayDirectX,chartSize.Size,item.Color,xMiddle,yTop+1,true);
						int[] arrayWpfColumns=GetRun(arrayWpf,chartSize.Size,item.Color,xMiddle,yTop+1,true);
						int[] arrayDirectXTop=GetRun(arrayDirectX,chartSize.Size,item.Color,xMiddle,yTop+1,false);
						int[] arrayWpfTop=GetRun(arrayWpf,chartSize.Size,item.Color,xMiddle,yTop+1,false);
						int worst=Math.Max(Math.Max(Math.Abs(arrayDirectXColumns[0]-arrayWpfColumns[0]),Math.Abs(arrayDirectXColumns[1]-arrayWpfColumns[1])),
							Math.Abs(arrayDirectXTop[0]-arrayWpfTop[0]));
						string text=name+": DirectX columns "+Describe(arrayDirectXColumns)+" from row "+arrayDirectXTop[0]+", WPF "+Describe(arrayWpfColumns)+" from row "
							+arrayWpfTop[0];
						if(worst>1) {
							listFailures.Add(text);
						}
						else if(worst==1) {
							listOffByOne.Add(text);
						}
					}
				}
			},listMissing);
			WpfCharts.WriteEvidence("parity-crisp.txt","Centre lines and highlight boxes against DirectX: "+countLines+" centre lines (rows exactly as DirectX's at five "
				+"columns), "+countBoxes+" highlight boxes ("+listOffByOne.Count+" with an edge one pixel over, "+listFailures.Count+" off by more).\r\n"
				+string.Join("\r\n",listFailures.Take(100).Select(x => "Failed: "+x).Concat(listOffByOne.Select(x => "One pixel: "+x))));
			Assert.AreEqual(0,listMissing.Count,"Pictures missing:\r\n"+string.Join("\r\n",listMissing));
			Assert.IsTrue(countLines>100 && countBoxes>40,"Lines "+countLines+", boxes "+countBoxes);
			Assert.IsTrue(listOffByOne.Count*10<=countBoxes,"More than a tenth of the boxes have an edge a pixel over: "+listOffByOne.Count+" of "+countBoxes);
			Assert.AreEqual(0,listFailures.Count,listFailures.Count+" are off:\r\n"+string.Join("\r\n",listFailures.Take(40)));
		}

		///<summary>The first and last row (or column, isHorizontal) of the run of pixels of exactly the colour through (x,y); {y,y-1} (an empty run)
		///when that pixel is not of the colour.</summary>
		private static int[] GetRun(byte[] arrayPixelsArgb,Size size,Color color,int x,int y,bool isHorizontal) {
			Func<int,int,bool> funcIs=(xPixel,yPixel) => {
				if(xPixel<0 || yPixel<0 || xPixel>=size.Width || yPixel>=size.Height) {
					return false;
				}
				int index=(yPixel*size.Width+xPixel)*4;
				return arrayPixelsArgb[index+2]==color.R && arrayPixelsArgb[index+1]==color.G && arrayPixelsArgb[index]==color.B;
			};
			int start=isHorizontal?x:y;
			if(!(isHorizontal?funcIs(x,y):funcIs(x,y))) {
				return new[] { start,start-1 };
			}
			int first=start;
			int last=start;
			while(isHorizontal?funcIs(first-1,y):funcIs(x,first-1)) {
				first--;
			}
			while(isHorizontal?funcIs(last+1,y):funcIs(x,last+1)) {
				last++;
			}
			return new[] { first,last };
		}

		private static string Describe(int[] arrayRun) {
			return arrayRun[1]<arrayRun[0]?"none":arrayRun[0]+"-"+arrayRun[1];
		}

		private static double GetLightnessDelta(ParityProbeOutcome outcome) {
			return (ParityProbes.GetLightness(outcome.ColorWpf)-ParityProbes.GetLightness(outcome.ColorDirectX))*100;
		}

		private static string Mean<T>(IEnumerable<T> list,Func<T,int> func) {
			return list.Select(x => (double)func(x)).DefaultIfEmpty(0).Average().ToString("0.00",CultureInfo.InvariantCulture);
		}

		private static void AppendPerGroup<T>(StringBuilder sb,List<T> listOutcomes,Func<T,string> funcGroup,Func<T,bool> funcPass,Func<T,bool> funcAllowed) {
			foreach(var group in listOutcomes.GroupBy(funcGroup)) {
				sb.AppendLine("  "+group.Key+": "+group.Count()+" probes, "+group.Count(funcPass)+" pass, "+group.Count(funcAllowed)+" allowed, "
					+group.Count(x => !funcPass(x) && !funcAllowed(x))+" fail");
			}
		}

		private static bool IsFresh() {
			return Environment.GetEnvironmentVariable(EnvFresh)=="1";
		}

		///<summary>Runs action for every chart scene at every size that has a DirectX recording, on an STA thread, with a Wpf3D wrapper of that size
		///holding the scene's data, the DirectX picture's pixels and the WPF picture's pixels (the committed baseline, or a fresh render with
		///OPMS_PARITY_FRESH=1). A picture that is missing is added to listMissing.</summary>
		private static void ForEachRecordedPicture(Action<ChartScene,ChartSize,ToothChartWrapper,byte[],byte[]> action,List<string> listMissing) {
			bool isFresh=IsFresh();
			StaRunner.Run(() => {
				foreach(ChartSize chartSize in WpfCharts.GetChartSizes()) {
					using ToothChartWrapper wrapper=WpfCharts.CreateWrapper(chartSize.Size);
					ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
					foreach(ChartScene scene in WpfCharts.GetChartScenes().Where(x => ChartScenes.GetSizes(x).Contains(chartSize))) {
						string name=chartSize.FolderName+"/"+scene.Name;
						string pathDirectX=Path.Combine(TestPaths.BaselinesLegacy,"DirectX",chartSize.FolderName,scene.Name+".png");
						if(!File.Exists(pathDirectX)) {
							listMissing.Add(name+": no DirectX recording");
							continue;
						}
						byte[] arrayWpf;
						if(isFresh) {
							using Bitmap bitmapWpf=WpfCharts.Draw(relay,scene,chartSize.ColorSet);
							arrayWpf=ChartImageCompare.GetPixelsArgb(bitmapWpf);
						}
						else {
							string pathWpf=WpfCharts.GetPicturePath(WpfCharts.BaselinesWpf,chartSize,scene);
							if(!File.Exists(pathWpf)) {
								listMissing.Add(name+": no WPF baseline");
								continue;
							}
							ChartScenes.Prepare(relay,scene,chartSize.ColorSet);
							using Bitmap bitmapWpf=WpfImageTests.LoadBitmap(pathWpf);
							arrayWpf=ChartImageCompare.GetPixelsArgb(bitmapWpf);
						}
						using Bitmap bitmapDirectX=WpfImageTests.LoadBitmap(pathDirectX);
						action(scene,chartSize,wrapper,ChartImageCompare.GetPixelsArgb(bitmapDirectX),arrayWpf);
					}
				}
			});
		}

		#region Text boxes
		///<summary>Where one text item's ink is in the two pictures.</summary>
		public class TextBoxOutcome {
			public string Name;
			public string SceneName;
			public string SizeName;
			///<summary>The scene item's Source, for example "number 8".</summary>
			public string ItemSource;
			public TextAllowance Allowance;
			///<summary>"number", "sealant" or "watch".</summary>
			public string Kind;
			public Rectangle BoundsDirectX;
			public Rectangle BoundsWpf;
			public string Failure;

			public int DeltaLeft { get { return BoundsWpf.Left-BoundsDirectX.Left; } }
			public int DeltaTop { get { return BoundsWpf.Top-BoundsDirectX.Top; } }
			public int DeltaRight { get { return BoundsWpf.Right-BoundsDirectX.Right; } }
			public int DeltaBottom { get { return BoundsWpf.Bottom-BoundsDirectX.Bottom; } }

			public override string ToString() {
				return Name+": DirectX "+Describe(BoundsDirectX)+", WPF "+Describe(BoundsWpf)+(Failure==null?"":" ("+Failure+")")
					+(Allowance==null?"":" [allowed]");
			}

			private static string Describe(Rectangle rectangle) {
				return rectangle.Left+","+rectangle.Top+" to "+rectangle.Right+","+rectangle.Bottom;
			}
		}

		///<summary>The ink of one text item in both pictures, searched in its text cell (the D3DX cell: the origin, GDI's width and the font's cell
		///height) and one pixel to each side; not above or below the cell, where the teeth come close. A number's ink is a pixel at least 0.4 of the way from its background to the item's
		///colour than its background (the highlight colour when the tooth is selected); a coloured mark's ink is a coloured pixel within 30 degrees of
		///the mark's hue. Null when DirectX shows no ink there (a mark the scene covers).</summary>
		private static TextBoxOutcome ProbeText(ChartScene scene,ChartSize chartSize,ToothChartData tcData,ToothSceneItem2D item,byte[] arrayDirectX,byte[] arrayWpf) {
			Rectangle rectangleSearch=new Rectangle(item.Origin.X-1,item.Origin.Y,item.WidthPixels+2,item.Font.Height);
			string kind;
			Func<Color,bool> funcIsInk;
			if(item.Source.StartsWith("number ")) {
				kind="number";
				string toothId=item.Source.Substring("number ".Length);
				bool isSelected=tcData.SelectedTeeth.Contains(toothId);
				Color colorText=Color.FromArgb(255,item.Color);
				Color colorBack=isSelected?tcData.ColorBackHighlight:tcData.ColorBackground;
				//0.4, not 0.5: D3DX's edge pixels are a little darker than WPF's grayscale ones (its glyph coverage has another gamma), so a pixel
				//half covered in both lies on either side of 0.5. And near the line between the two colours: the chart's background around a
				//highlight box lies 0.4 of the way from white to red, but far from that line.
				funcIsInk=color => GetProjection(color,colorBack,colorText)>=0.4 && GetDistanceFromLine(color,colorBack,colorText)<=48;
			}
			else if(item.Source.StartsWith("sealant ")) {
				kind="sealant";
				funcIsInk=GetIsHueInk(item.Color);
			}
			else if(item.Source.StartsWith("watch ")) {
				kind="watch";
				funcIsInk=GetIsHueInk(item.Color);
			}
			else {
				return null;
			}
			if(funcIsInk==null) {
				return null;//a grey mark: no hue to find it by
			}
			Rectangle? rectangleDirectX=GetInkBounds(arrayDirectX,chartSize.Size,rectangleSearch,funcIsInk);
			if(rectangleDirectX==null) {
				return null;
			}
			TextBoxOutcome outcome=new TextBoxOutcome();
			outcome.Name=chartSize.FolderName+"/"+scene.Name+" "+item.Source+" \""+item.Text+"\"";
			outcome.SceneName=scene.Name;
			outcome.SizeName=chartSize.FolderName;
			outcome.ItemSource=item.Source;
			outcome.Kind=kind;
			outcome.BoundsDirectX=rectangleDirectX.Value;
			Rectangle? rectangleWpf=GetInkBounds(arrayWpf,chartSize.Size,rectangleSearch,funcIsInk);
			if(rectangleWpf==null) {
				outcome.Failure="no ink in the WPF picture";
				return outcome;
			}
			outcome.BoundsWpf=rectangleWpf.Value;
			int worst=new[] { outcome.DeltaLeft,outcome.DeltaTop,outcome.DeltaRight,outcome.DeltaBottom }.Max(x => Math.Abs(x));
			if(worst>MaxTextEdgePixels) {
				outcome.Failure="an edge "+worst+" pixels off";
			}
			return outcome;
		}

		///<summary>How far color lies from colorFrom toward colorTo, 0 at colorFrom and 1 at colorTo (the projection on the line between them).</summary>
		public static double GetProjection(Color color,Color colorFrom,Color colorTo) {
			double dr=colorTo.R-colorFrom.R;
			double dg=colorTo.G-colorFrom.G;
			double db=colorTo.B-colorFrom.B;
			double length2=dr*dr+dg*dg+db*db;
			if(length2==0) {
				return 0;
			}
			return ((color.R-colorFrom.R)*dr+(color.G-colorFrom.G)*dg+(color.B-colorFrom.B)*db)/length2;
		}

		///<summary>How far color lies from the line through colorFrom and colorTo, in RGB units.</summary>
		public static double GetDistanceFromLine(Color color,Color colorFrom,Color colorTo) {
			double t=GetProjection(color,colorFrom,colorTo);
			double r=colorFrom.R+t*(colorTo.R-colorFrom.R)-color.R;
			double g=colorFrom.G+t*(colorTo.G-colorFrom.G)-color.G;
			double b=colorFrom.B+t*(colorTo.B-colorFrom.B)-color.B;
			return Math.Sqrt(r*r+g*g+b*b);
		}

		private static Func<Color,bool> GetIsHueInk(Color colorMark) {
			if(!ParityProbes.IsColoured(colorMark)) {
				return null;
			}
			float hueMark=colorMark.GetHue();
			return color => color.GetSaturation()>=0.3 && color.GetBrightness()>0.08 && color.GetBrightness()<0.92
				&& ParityProbes.GetHueDifference(color.GetHue(),hueMark)<=30;
		}

		///<summary>The bounds of the ink pixels inside rectangleSearch (cut to the picture), or null when there are none. Right and Bottom are one past
		///the last ink pixel.</summary>
		public static Rectangle? GetInkBounds(byte[] arrayPixelsArgb,Size size,Rectangle rectangleSearch,Func<Color,bool> funcIsInk) {
			Rectangle rectangle=Rectangle.Intersect(rectangleSearch,new Rectangle(Point.Empty,size));
			int left=int.MaxValue;
			int top=int.MaxValue;
			int right=int.MinValue;
			int bottom=int.MinValue;
			for(int y=rectangle.Top;y<rectangle.Bottom;y++) {
				for(int x=rectangle.Left;x<rectangle.Right;x++) {
					int index=(y*size.Width+x)*4;
					Color color=Color.FromArgb(255,arrayPixelsArgb[index+2],arrayPixelsArgb[index+1],arrayPixelsArgb[index]);
					if(!funcIsInk(color)) {
						continue;
					}
					left=Math.Min(left,x);
					top=Math.Min(top,y);
					right=Math.Max(right,x+1);
					bottom=Math.Max(bottom,y+1);
				}
			}
			if(left==int.MaxValue) {
				return null;
			}
			return Rectangle.FromLTRB(left,top,right,bottom);
		}
		#endregion Text boxes
	}
}
