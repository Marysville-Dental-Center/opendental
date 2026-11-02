using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using CodeBase;
using OpenPmsDentalBusiness;
using WpfControls.UI;

namespace OpenPmsDental {
	///<summary></summary>
	public partial class FrmTestSplitters:FrmODBase {
		private int _countLoop=60;
		private DataTable _table;

		///<summary></summary>
		public FrmTestSplitters(){
			InitializeComponent();
			Load+=FrmFrmTestSplitters_Load;
		}

		///<summary>A local test page in the splitter's top panel: this window never goes online.</summary>
		private async void FrmFrmTestSplitters_Load(object sender,EventArgs e) {
			try {
				await webView2.Init();
			}
			catch(Exception ex) {
				ex.DoNothing();//No WebView2 Runtime (or not x86 in debug): the panel stays empty.
				return;
			}
			webView2.NavigateToString(CodeBase.HtmlPage.Build("<p>Splitter test page</p>","Test page","font-family:sans-serif"));
			FormClosing+=(sender2,e2) => webView2.Dispose();
		}

	}
}
