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
		///<summary>3. The WPF 3D chart (namespace SparksToothChart.Wpf3D): a software render of the scene on the chart's own render thread, shown
		///on screen by a plain WinForms control.</summary>
		Wpf3D,
	}
}
