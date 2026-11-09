using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace SparksToothChart.Wpf3D {
	///<summary>What the renderer did with one text item, for the DPI tests (docs/specs/wpf-tooth-chart-plan.md, section 5.6).</summary>
	public sealed class ChartTextReport {
		public string Text;
		public double PixelsPerDip;
		public TextFormattingMode TextFormattingMode;
		///<summary>The horizontal scale that fits WPF's text to GDI's width of the D3DX font.</summary>
		public double ScaleX;
	}

	///<summary>A DrawingVisual whose text is antialiased in grayscale, never ClearType, which depends on per-user settings (section 7).</summary>
	internal sealed class ChartDrawingVisual:DrawingVisual {
		public ChartDrawingVisual() {
			VisualTextRenderingMode=TextRenderingMode.Grayscale;
			VisualTextHintingMode=TextHintingMode.Fixed;
			//The 3D layer image is drawn at its own pixel size, so no scaling happens; nearest neighbour keeps it so.
			VisualBitmapScalingMode=BitmapScalingMode.NearestNeighbor;
		}
	}

	///<summary>Draws a scene's 2D layer over its 3D layer image, on the render thread (docs/specs/wpf-tooth-chart-plan.md, section 5.1): the items
	///in their order, polygons filled with the Nonzero rule in flat, opaque colour, and text in the D3DX font's face and weight at GDI's em height,
	///its baseline at GDI's ascent below the cell's top, scaled across to GDI's width, formatted in Display mode with pixelsPerDip 1.0.</summary>
	internal static class ChartLayer2D {
		public static List<ChartTextReport> Draw(DrawingContext drawingContext,ToothScene scene) {
			List<ChartTextReport> listReports=new List<ChartTextReport>();
			Dictionary<int,SolidColorBrush> dictBrushes=new Dictionary<int,SolidColorBrush>();
			Dictionary<string,Typeface> dictTypefaces=new Dictionary<string,Typeface>();
			foreach(ToothSceneItem2D item in scene.Items2D) {
				SolidColorBrush brush=GetBrush(dictBrushes,item.Color);
				if(item.Kind==ChartItem2DKind.Polygons) {
					DrawPolygons(drawingContext,item,brush);
					continue;
				}
				listReports.Add(DrawText(drawingContext,item,brush,dictTypefaces));
			}
			return listReports;
		}

		private static void DrawPolygons(DrawingContext drawingContext,ToothSceneItem2D item,SolidColorBrush brush) {
			if(item.Polygons.Count==0) {
				return;
			}
			StreamGeometry streamGeometry=new StreamGeometry();
			streamGeometry.FillRule=FillRule.Nonzero;
			using(StreamGeometryContext streamGeometryContext=streamGeometry.Open()) {
				foreach(System.Drawing.PointF[] arrayPoints in item.Polygons) {
					streamGeometryContext.BeginFigure(new Point(arrayPoints[0].X,arrayPoints[0].Y),true,true);
					List<Point> listPoints=new List<Point>(arrayPoints.Length-1);
					for(int i=1;i<arrayPoints.Length;i++) {
						listPoints.Add(new Point(arrayPoints[i].X,arrayPoints[i].Y));
					}
					streamGeometryContext.PolyLineTo(listPoints,false,false);
				}
			}
			streamGeometry.Freeze();
			drawingContext.DrawGeometry(brush,null,streamGeometry);
		}

		private static ChartTextReport DrawText(DrawingContext drawingContext,ToothSceneItem2D item,SolidColorBrush brush,Dictionary<string,Typeface> dictTypefaces) {
			ChartFont chartFont=item.Font;
			string keyTypeface=chartFont.FaceName+"|"+chartFont.Weight;
			Typeface typeface;
			if(!dictTypefaces.TryGetValue(keyTypeface,out typeface)) {
				typeface=new Typeface(new FontFamily(chartFont.FaceName),FontStyles.Normal,FontWeight.FromOpenTypeWeight(Math.Max(1,Math.Min(999,chartFont.Weight))),
					FontStretches.Normal);
				dictTypefaces[keyTypeface]=typeface;
			}
			FormattedText formattedText=new FormattedText(item.Text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,typeface,Math.Max(1,chartFont.EmHeight),
				brush,null,TextFormattingMode.Display,1.0);
			double widthNatural=formattedText.WidthIncludingTrailingWhitespace;
			double scaleX=1;
			if(widthNatural>0 && item.WidthPixels>0) {
				scaleX=item.WidthPixels/widthNatural;
			}
			double x=item.Origin.X;
			double yBaseline=item.Origin.Y+chartFont.Ascent;
			drawingContext.PushTransform(new ScaleTransform(scaleX,1,x,0));
			drawingContext.DrawText(formattedText,new Point(x,yBaseline-formattedText.Baseline));
			drawingContext.Pop();
			ChartTextReport chartTextReport=new ChartTextReport();
			chartTextReport.Text=item.Text;
			chartTextReport.PixelsPerDip=formattedText.PixelsPerDip;
			chartTextReport.TextFormattingMode=TextFormattingMode.Display;
			chartTextReport.ScaleX=scaleX;
			return chartTextReport;
		}

		public static SolidColorBrush GetBrush(Dictionary<int,SolidColorBrush> dictBrushes,System.Drawing.Color color) {
			int argb=color.ToArgb();
			SolidColorBrush brush;
			if(!dictBrushes.TryGetValue(argb,out brush)) {
				brush=new SolidColorBrush(Color.FromArgb(color.A,color.R,color.G,color.B));
				brush.Freeze();
				dictBrushes[argb]=brush;
			}
			return brush;
		}
	}
}
