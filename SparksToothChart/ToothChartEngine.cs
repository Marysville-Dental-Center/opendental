using System;

namespace SparksToothChart {
	///<summary>The engine a ToothChartWrapper draws with (ToothChartWrapper.Engine). In memory only: it is never stored, and it is outside the
	///business layer, so docs/port-notes/FORK-ENUMS.md does not apply to it. The first three have the values of the stored DrawingMode
	///(computerpref.GraphicsSimple: 0 DirectX, 1 Simple 2D, 2 OpenGL), so a stored value converts with a cast. Wpf3D has no stored form: the stored
	///values stay 0, 1 and 2 (docs/specs/wpf-tooth-chart-plan.md, section 1 item 5).</summary>
	public enum ToothChartEngine {
		///<summary>0. DirectX 9 through SharpDX.</summary>
		DirectX,
		///<summary>1. The flat 2D chart, drawn with GDI+.</summary>
		Simple2D,
		///<summary>2. OpenGL through Tao.</summary>
		OpenGL,
		///<summary>3. The WPF 3D chart (namespace SparksToothChart.Wpf3D): on screen a live WPF view (O-19 (b), TC-3), drawn by WPF's render thread
		///as every WPF window of the process is (in OpenPmsDental.exe in software at 1x, because the program sets RenderOptions.ProcessRenderMode to
		///SoftwareOnly, O-34), or with ToothChartWrapper.WpfDrawInSoftware the renderer's supersampled software image of the scene, made on the chart's
		///own render thread and shown by a plain WinForms control; every image (GetBitmap, prints, sheets, the dashboard) is that renderer's image.</summary>
		Wpf3D,
	}

	///<summary>The kinds of fallback message of the tooth chart. ToothChartWrapper.LogFallbackOnce writes the first message of each kind in a process
	///to the ToothChart log and keeps the rest in memory only, so a computer that has written one kind still writes the first of another: a
	///computer that once logged a DirectX fallback (the computers the WPF chart replaces DirectX on) still logs the WPF chart's first failure. In
	///memory only.</summary>
	public enum ToothChartFallbackKind {
		///<summary>A DrawingMode engine could not start: DirectX, then OpenGL, fall back to the next mode.</summary>
		DrawMode,
		///<summary>The WPF chart could not start and the chart uses Simple 2D.</summary>
		Wpf3DStart,
		///<summary>A WPF image could not be drawn (GetBitmap or the screen) and the Simple 2D image of the same data took its place.</summary>
		Wpf3DRender,
		///<summary>The WPF chart's render thread: its last guard caught an error, a result could not be handed on, or failed images in a row
		///replaced the thread.</summary>
		Wpf3DRenderThread,
		///<summary>The WPF chart's live view could not start on a shown wrapper, or could not show a scene, and the wrapper shows the software render on
		///screen instead.</summary>
		Wpf3DLiveView,
		///<summary>A tool's cursor (the pen's, the eraser's or the colour changer's) could not be made from its embedded file, and the chart shows the
		///arrow for that tool until a later choice of the tool makes the cursor.</summary>
		Cursor,
	}
}
