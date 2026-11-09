// DirectX charts for the TC-3 tests that compare the WPF chart with today's DirectX chart live (the mouse and the render times), on a shown window.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Drawing;
using System.Windows.Forms;
using OpenPmsDentalBusiness;
using SparksToothChart;

namespace ToothChartImageTests {
	public static class DirectXCharts {
		///<summary>The first standard DirectX device format that starts on a chart on the form and gives a picture, without multisampling as TC-0
		///recorded (a multisampled draw is not repeatable on every driver); the multisampled one when only that starts. Null when none starts or the
		///DirectX runtime (d3dx9_43.dll) is missing.</summary>
		public static string FindFormat(Form form) {
			if(!ToothChartWrapper.IsDirectXRuntimeAvailable()) {
				return null;
			}
			foreach(ToothChartDirectX.DirectXDeviceFormat directXDeviceFormat in ToothChartDirectX.GetStandardDeviceFormats()) {
				string formatSearched=directXDeviceFormat.ToString();
				if(!ToothChartWrapper.TestDirectXFormat(form,formatSearched)) {
					continue;
				}
				directXDeviceFormat.MultiSampleCount=0;
				string formatPlain=directXDeviceFormat.ToString();
				if(formatPlain!=formatSearched && ToothChartWrapper.TestDirectXFormat(form,formatPlain)) {
					return formatPlain;
				}
				return formatSearched;
			}
			return null;
		}

		///<summary>The DirectX device format the program itself picks on a computer with none saved (ToothChartRelay.ChooseStartupDirectXFormat calls
		///ToothChartWrapper.GetPreferredDirectXFormat: the first standard format that works on a chart on the form, multisampled where the driver allows),
		///which is what offices draw with unless Setup > Graphics chose another; null when none starts or the DirectX runtime is missing. For the render
		///times (TC-3 fix round 3); the recordings and the mouse tests keep FindFormat's unsmoothed format.</summary>
		public static string FindProgramFormat(Form form) {
			if(!ToothChartWrapper.IsDirectXRuntimeAvailable()) {
				return null;
			}
			string directXFormat=ToothChartWrapper.GetPreferredDirectXFormat(form);
			return directXFormat=="invalid"?null:directXFormat;
		}

		///<summary>A DirectX wrapper of the size on the shown form, made as TC-0's recorder makes one: the size, the device format, the form, then the
		///mode. Read Engine to learn whether DirectX started.</summary>
		public static ToothChartWrapper CreateWrapper(Form form,Size size,string directXFormat) {
			form.ClientSize=size;
			ToothChartWrapper wrapper=new ToothChartWrapper();
			wrapper.Location=new Point(0,0);
			wrapper.Size=size;
			wrapper.UseHardware=false;
			wrapper.PreferredPixelFormatNumber=0;
			wrapper.DeviceFormat=new ToothChartDirectX.DirectXDeviceFormat(directXFormat);
			form.Controls.Add(wrapper);
			wrapper.DrawMode=DrawingMode.DirectX;
			Application.DoEvents();
			return wrapper;
		}
	}
}
