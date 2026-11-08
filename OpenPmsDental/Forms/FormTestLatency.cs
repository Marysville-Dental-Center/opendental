using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using OpenPmsDentalBusiness;
using System.Diagnostics;

namespace OpenPmsDental {
	public partial class FormTestLatency:FormODBase {
		public FormTestLatency() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormTestLatency_Load(object sender,EventArgs e) {
			//Average latency (Open Dental 26.2, P262-TOOLS): the number of round trips starts at the default each time the window opens.
			textAverageCount.MinVal=LatencyMeasure.COUNT_MIN;
			textAverageCount.MaxVal=LatencyMeasure.COUNT_MAX;
			textAverageCount.Value=LatencyMeasure.COUNT_DEFAULT;
		}

		///<summary>Runs the small latency query the chosen number of times and shows the average, fastest and slowest round trip.</summary>
		private void butAverage_Click(object sender,EventArgs e) {
			if(!textAverageCount.IsValid()) {
				MsgBox.Show(this,"Enter a number of round trips from 1 to 1000.");
				return;
			}
			LatencyResult latencyResult;
			Cursor=Cursors.WaitCursor;
			try {
				latencyResult=LatencyMeasure.MeasureDatabase(textAverageCount.Value);
			}
			finally {
				Cursor=Cursors.Default;
			}
			textAverage.Text=LatencyResult.Format(latencyResult.AverageMs);
			textAverageMin.Text=LatencyResult.Format(latencyResult.MinMs);
			textAverageMax.Text=LatencyResult.Format(latencyResult.MaxMs);
		}

		private void butLatency_Click(object sender,EventArgs e) {
			Stopwatch stopwatch = new Stopwatch();
			Cursor=Cursors.WaitCursor;
			stopwatch.Start();
			MiscData.GetMySqlVersion();//a nice short query and small dataset.
			stopwatch.Stop();
			textLatency.Text=stopwatch.ElapsedMilliseconds.ToString();
			Cursor=Cursors.Default;
		}

		private void butSpeed_Click(object sender, EventArgs e){
			Stopwatch stopwatch = new Stopwatch();
			Cursor=Cursors.WaitCursor;
			stopwatch.Start();
			MiscData.GetMySqlVersion();//a nice short query and small dataset.
			stopwatch.Stop();
			long latency=stopwatch.ElapsedMilliseconds;
			stopwatch.Restart();
			Prefs.RefreshCache();
			stopwatch.Stop();
			long speed=stopwatch.ElapsedMilliseconds-latency;
			textSpeed.Text=speed.ToString();
			Cursor=Cursors.Default;
		}
	
	}
}