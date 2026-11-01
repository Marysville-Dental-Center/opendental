using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CodeBase;
using OpenPmsDentalBusiness;
using WpfControls.UI;

namespace OpenPmsDental {
	/// <summary></summary>
	public partial class FrmPrintPreview : FrmODBase {
		//Yes, this was a quick and dirty window. Works fine, but we really need to build a custom one.
		//Buttons should have labels.
		public FixedDocument FixedDocumentCur;
		///<summary>B-PRINT (25.3 Print Options): the print situation whose printer the Print button starts with, and the print job's name.</summary>
		public PrintSituation PrintSituation_=PrintSituation.Default;
		public string Description="";

		///<summary></summary>
		public FrmPrintPreview(){
			InitializeComponent();
			Load+=FrmPrintPreview_Load;
			//The viewer's Print button (and Ctrl+P) opens the Print Options window instead of the Windows print dialog.
			CommandManager.AddPreviewExecutedHandler(documentViewer,documentViewer_PreviewExecuted);
		}

		private void FrmPrintPreview_Load(object sender, EventArgs e) {
			documentViewer.Document=FixedDocumentCur;
			StartMaximized=true;
		}

		private void documentViewer_PreviewExecuted(object sender,ExecutedRoutedEventArgs e) {
			if(e.Command!=ApplicationCommands.Print) {
				return;
			}
			e.Handled=true;
			if(WpfControls.PrintOptionsWpf.TryPrintFixedDocument(FixedDocumentCur,Description,PrintSituation_,isPromptForced:true)) {
				IsDialogOK=true;
			}
		}
	}
}