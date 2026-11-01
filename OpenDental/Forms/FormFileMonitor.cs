// B-SHELL (v25_3-tools-file-monitor): Help, File Monitor.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CodeBase;
using OpenDental.UI;
using OpenDentBusiness;

namespace OpenDental {
	///<summary>Open Dental 25.3, Help, File Monitor: reads and writes to the image (A to Z) folder made by this program instance, as they happen. For
	///finding slow or failing image access. Single instance and non-modal, so it can stay open in the background. Capture is off when the window
	///opens; Start and Stop toggle it. Keeps the newest 5,000 rows. Storage that skips the A to Z existence checks (cloud storage, a local path
	///override) shows fewer rows. Nothing is written to disk: paths contain patient folder names.</summary>
	public partial class FormFileMonitor:FormODBase {
		private static FormFileMonitor _formFileMonitorOpen;
		///<summary>Filled on whatever thread did the file operation, drained on the UI thread by the timer.</summary>
		private ConcurrentQueue<FileMonitorEntry> _concurrentQueueEntries=new ConcurrentQueue<FileMonitorEntry>();
		private FileMonitorFeed _fileMonitorFeed=new FileMonitorFeed();
		private bool _isCapturing;
		private bool _isImagesInDatabase;

		public FormFileMonitor() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		///<summary>Opens the window, or brings the open one to the front.</summary>
		public static void ShowSingle(Form formOwner) {
			if(_formFileMonitorOpen!=null && !_formFileMonitorOpen.IsDisposed) {
				if(_formFileMonitorOpen.WindowState==FormWindowState.Minimized) {
					_formFileMonitorOpen.WindowState=FormWindowState.Normal;
				}
				_formFileMonitorOpen.BringToFront();
				_formFileMonitorOpen.Activate();
				return;
			}
			_formFileMonitorOpen=new FormFileMonitor();
			_formFileMonitorOpen.FormClosed+=(sender,e) => {
				if(_formFileMonitorOpen==sender) {
					_formFileMonitorOpen=null;
				}
			};
			_formFileMonitorOpen.Show(formOwner);
		}

		private void FormFileMonitor_Load(object sender,EventArgs e) {
			FillGridColumns();
			_isImagesInDatabase=(PrefC.AtoZfolderUsed==DataStorageType.InDatabase);
			if(_isImagesInDatabase) {
				butStart.Enabled=false;
				butClear.Enabled=false;
				labelStatus.Text=Lan.g(this,"Images are stored in the database, so there is no A to Z folder to monitor.");
				return;
			}
			SetStatusText();
		}

		private void FillGridColumns() {
			gridFeed.BeginUpdate();
			gridFeed.Columns.Clear();
			gridFeed.Columns.Add(new GridColumn(Lan.g(gridFeed.TranslationName,"Time"),90,HorizontalAlignment.Center));
			gridFeed.Columns.Add(new GridColumn(Lan.g(gridFeed.TranslationName,"Action"),100));
			gridFeed.Columns.Add(new GridColumn(Lan.g(gridFeed.TranslationName,"Path"),100){ IsWidthDynamic=true });
			gridFeed.Columns.Add(new GridColumn(Lan.g(gridFeed.TranslationName,"Bytes"),90,HorizontalAlignment.Right));
			gridFeed.Columns.Add(new GridColumn(Lan.g(gridFeed.TranslationName,"Duration (ms)"),90,HorizontalAlignment.Right));
			gridFeed.Columns.Add(new GridColumn(Lan.g(gridFeed.TranslationName,"Result"),160));
			gridFeed.ListGridRows.Clear();
			gridFeed.EndUpdate();
		}

		private void butStart_Click(object sender,EventArgs e) {
			if(_isCapturing) {
				StopCapture();
			}
			else {
				StartCapture();
			}
			SetStatusText();
		}

		private void StartCapture() {
			string atoZRoot="";
			try {
				atoZRoot=OpenDentBusiness.FileIO.FileAtoZ.GetPreferredAtoZpath()??"";
			}
			catch(Exception ex) {
				ex.DoNothing();//Paths are then shown in full.
			}
			FileMonitor.EntryCaptured+=FileMonitor_EntryCaptured;
			FileMonitor.StartCapture(atoZRoot,doReadFileSizes:!CloudStorage.IsCloudStorage);
			timerProcessQueue.Start();
			_isCapturing=true;
			butStart.Text=Lan.g(this,"Stop");
		}

		private void StopCapture() {
			FileMonitor.EntryCaptured-=FileMonitor_EntryCaptured;
			FileMonitor.StopCapture();
			timerProcessQueue.Stop();
			_isCapturing=false;
			butStart.Text=Lan.g(this,"Start");
			DrainQueue();
		}

		private void SetStatusText() {
			string status=_isCapturing ? Lan.g(this,"Capturing") : Lan.g(this,"Stopped");
			status+="\r\n"+_fileMonitorFeed.Count.ToString("N0")+" "+Lan.g(this,"rows");
			if(CloudStorage.IsCloudStorage) {
				status+="\r\n"+Lan.g(this,"Cloud storage skips some folder checks, so fewer actions are listed.");
			}
			else if(!string.IsNullOrEmpty(OpenDentBusiness.FileIO.FileAtoZ.LocalAtoZpath)) {
				status+="\r\n"+Lan.g(this,"A local path override is in use, so fewer actions are listed.");
			}
			labelStatus.Text=status;
		}

		///<summary>Any thread.</summary>
		private void FileMonitor_EntryCaptured(FileMonitorEntry entry) {
			_concurrentQueueEntries.Enqueue(entry);
		}

		private void timerProcessQueue_Tick(object sender,EventArgs e) {
			DrainQueue();
		}

		private void DrainQueue() {
			bool hasNew=false;
			while(_concurrentQueueEntries.TryDequeue(out FileMonitorEntry entry)) {
				_fileMonitorFeed.Add(entry);
				hasNew=true;
			}
			if(!hasNew) {
				return;
			}
			FillGrid();
			SetStatusText();
		}

		private void FillGrid() {
			List<FileMonitorEntry> listEntries=_fileMonitorFeed.GetEntries();
			gridFeed.BeginUpdate();
			gridFeed.ListGridRows.Clear();
			for(int i=0;i<listEntries.Count;i++) {
				GridRow row=new GridRow();
				row.Cells.Add(FileMonitorFeed.FormatTime(listEntries[i].DateTimeStart));
				row.Cells.Add(listEntries[i].Action.ToString());
				row.Cells.Add(listEntries[i].Path);
				row.Cells.Add(FileMonitorFeed.FormatBytes(listEntries[i].Bytes));
				row.Cells.Add(FileMonitorFeed.FormatDuration(listEntries[i].DurationMs));
				row.Cells.Add(listEntries[i].Result);
				if(listEntries[i].IsError) {
					row.ColorText=Color.Red;
				}
				row.Tag=listEntries[i];
				gridFeed.ListGridRows.Add(row);
			}
			gridFeed.EndUpdate();
			gridFeed.ScrollToIndex(gridFeed.ListGridRows.Count-1);
		}

		private void butClear_Click(object sender,EventArgs e) {
			while(_concurrentQueueEntries.TryDequeue(out FileMonitorEntry _)) {
			}
			_fileMonitorFeed.Clear();
			gridFeed.BeginUpdate();
			gridFeed.ListGridRows.Clear();
			gridFeed.EndUpdate();
			SetStatusText();
		}

		private void FormFileMonitor_FormClosing(object sender,FormClosingEventArgs e) {
			ODException.SwallowAnyException(() => {
				if(_isCapturing) {
					FileMonitor.EntryCaptured-=FileMonitor_EntryCaptured;
					FileMonitor.StopCapture();
					timerProcessQueue.Stop();
					_isCapturing=false;
				}
			});
		}
	}
}
