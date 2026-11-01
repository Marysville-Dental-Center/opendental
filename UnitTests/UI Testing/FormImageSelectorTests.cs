using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace UnitTests{
	public partial class FormImageSelectorTests : OpenPmsDental.FormODBase{

		public FormImageSelectorTests(){
			InitializeComponent();
			InitializeLayoutManager();
			//LayoutManager.ZoomTest=45;
			//DoubleBuffered=true;
		}

		private void FormImageSelectorTests_Load(object sender,EventArgs e) {
			//imageSelector1.SetData(null);
		}
	}

}
