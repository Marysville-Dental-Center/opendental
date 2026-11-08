namespace OpenPmsDental{
	partial class FormTestLatency {
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing) {
			if(disposing && (components != null)) {
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() {
			this.label1 = new System.Windows.Forms.Label();
			this.textLatency = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.butLatency = new OpenPmsDental.UI.Button();
			this.groupBox1 = new OpenPmsDental.UI.GroupBox();
			this.groupBox2 = new OpenPmsDental.UI.GroupBox();
			this.label3 = new System.Windows.Forms.Label();
			this.butSpeed = new OpenPmsDental.UI.Button();
			this.label4 = new System.Windows.Forms.Label();
			this.textSpeed = new System.Windows.Forms.TextBox();
			this.groupBoxAverage = new OpenPmsDental.UI.GroupBox();
			this.labelAverageExplain = new System.Windows.Forms.Label();
			this.labelAverageCount = new System.Windows.Forms.Label();
			this.textAverageCount = new OpenPmsDental.ValidNum();
			this.labelAverage = new System.Windows.Forms.Label();
			this.textAverage = new System.Windows.Forms.TextBox();
			this.labelAverageMs = new System.Windows.Forms.Label();
			this.labelAverageMin = new System.Windows.Forms.Label();
			this.textAverageMin = new System.Windows.Forms.TextBox();
			this.labelAverageMinMs = new System.Windows.Forms.Label();
			this.labelAverageMax = new System.Windows.Forms.Label();
			this.textAverageMax = new System.Windows.Forms.TextBox();
			this.labelAverageMaxMs = new System.Windows.Forms.Label();
			this.butAverage = new OpenPmsDental.UI.Button();
			this.groupBox1.SuspendLayout();
			this.groupBox2.SuspendLayout();
			this.groupBoxAverage.SuspendLayout();
			this.SuspendLayout();
			// 
			// label1
			// 
			this.label1.Location = new System.Drawing.Point(6, 14);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(132, 47);
			this.label1.TabIndex = 5;
			this.label1.Text = "Roundtrip time for a very small query to the database.";
			this.label1.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
			// 
			// textLatency
			// 
			this.textLatency.Location = new System.Drawing.Point(9, 71);
			this.textLatency.Name = "textLatency";
			this.textLatency.ReadOnly = true;
			this.textLatency.Size = new System.Drawing.Size(55, 20);
			this.textLatency.TabIndex = 6;
			this.textLatency.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
			// 
			// label2
			// 
			this.label2.Location = new System.Drawing.Point(66, 72);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(53, 17);
			this.label2.TabIndex = 7;
			this.label2.Text = "ms.";
			this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// butLatency
			// 
			this.butLatency.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butLatency.Location = new System.Drawing.Point(8, 100);
			this.butLatency.Name = "butLatency";
			this.butLatency.Size = new System.Drawing.Size(70, 24);
			this.butLatency.TabIndex = 4;
			this.butLatency.Text = "Test";
			this.butLatency.Click += new System.EventHandler(this.butLatency_Click);
			// 
			// groupBox1
			// 
			this.groupBox1.Controls.Add(this.label1);
			this.groupBox1.Controls.Add(this.butLatency);
			this.groupBox1.Controls.Add(this.label2);
			this.groupBox1.Controls.Add(this.textLatency);
			this.groupBox1.Location = new System.Drawing.Point(12, 12);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(157, 132);
			this.groupBox1.TabIndex = 8;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Latency";
			// 
			// groupBox2
			// 
			this.groupBox2.Controls.Add(this.label3);
			this.groupBox2.Controls.Add(this.butSpeed);
			this.groupBox2.Controls.Add(this.label4);
			this.groupBox2.Controls.Add(this.textSpeed);
			this.groupBox2.Location = new System.Drawing.Point(193, 12);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(157, 132);
			this.groupBox2.TabIndex = 9;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "Speed";
			// 
			// label3
			// 
			this.label3.Location = new System.Drawing.Point(6, 19);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(132, 42);
			this.label3.TabIndex = 5;
			this.label3.Text = "For a larger database query.  The latency time is already subtracted.";
			this.label3.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
			// 
			// butSpeed
			// 
			this.butSpeed.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butSpeed.Location = new System.Drawing.Point(8, 100);
			this.butSpeed.Name = "butSpeed";
			this.butSpeed.Size = new System.Drawing.Size(70, 24);
			this.butSpeed.TabIndex = 4;
			this.butSpeed.Text = "Test";
			this.butSpeed.Click += new System.EventHandler(this.butSpeed_Click);
			// 
			// label4
			// 
			this.label4.Location = new System.Drawing.Point(66, 72);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(53, 17);
			this.label4.TabIndex = 7;
			this.label4.Text = "Mbps";
			this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// textSpeed
			// 
			this.textSpeed.Location = new System.Drawing.Point(9, 71);
			this.textSpeed.Name = "textSpeed";
			this.textSpeed.ReadOnly = true;
			this.textSpeed.Size = new System.Drawing.Size(55, 20);
			this.textSpeed.TabIndex = 6;
			this.textSpeed.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
			// 
			// labelAverageExplain
			// 
			this.labelAverageExplain.Location = new System.Drawing.Point(6, 16);
			this.labelAverageExplain.Name = "labelAverageExplain";
			this.labelAverageExplain.Size = new System.Drawing.Size(194, 30);
			this.labelAverageExplain.TabIndex = 0;
			this.labelAverageExplain.Text = "Average of several round trips of the same small query.";
			// 
			// labelAverageCount
			// 
			this.labelAverageCount.Location = new System.Drawing.Point(6, 50);
			this.labelAverageCount.Name = "labelAverageCount";
			this.labelAverageCount.Size = new System.Drawing.Size(96, 17);
			this.labelAverageCount.TabIndex = 1;
			this.labelAverageCount.Text = "Round trips";
			this.labelAverageCount.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// labelAverage
			// 
			this.labelAverage.Location = new System.Drawing.Point(6, 74);
			this.labelAverage.Name = "labelAverage";
			this.labelAverage.Size = new System.Drawing.Size(96, 17);
			this.labelAverage.TabIndex = 2;
			this.labelAverage.Text = "Average";
			this.labelAverage.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// labelAverageMs
			// 
			this.labelAverageMs.Location = new System.Drawing.Point(163, 74);
			this.labelAverageMs.Name = "labelAverageMs";
			this.labelAverageMs.Size = new System.Drawing.Size(30, 17);
			this.labelAverageMs.TabIndex = 3;
			this.labelAverageMs.Text = "ms.";
			this.labelAverageMs.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// labelAverageMin
			// 
			this.labelAverageMin.Location = new System.Drawing.Point(6, 98);
			this.labelAverageMin.Name = "labelAverageMin";
			this.labelAverageMin.Size = new System.Drawing.Size(96, 17);
			this.labelAverageMin.TabIndex = 4;
			this.labelAverageMin.Text = "Fastest";
			this.labelAverageMin.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// labelAverageMinMs
			// 
			this.labelAverageMinMs.Location = new System.Drawing.Point(163, 98);
			this.labelAverageMinMs.Name = "labelAverageMinMs";
			this.labelAverageMinMs.Size = new System.Drawing.Size(30, 17);
			this.labelAverageMinMs.TabIndex = 5;
			this.labelAverageMinMs.Text = "ms.";
			this.labelAverageMinMs.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// labelAverageMax
			// 
			this.labelAverageMax.Location = new System.Drawing.Point(6, 122);
			this.labelAverageMax.Name = "labelAverageMax";
			this.labelAverageMax.Size = new System.Drawing.Size(96, 17);
			this.labelAverageMax.TabIndex = 6;
			this.labelAverageMax.Text = "Slowest";
			this.labelAverageMax.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// labelAverageMaxMs
			// 
			this.labelAverageMaxMs.Location = new System.Drawing.Point(163, 122);
			this.labelAverageMaxMs.Name = "labelAverageMaxMs";
			this.labelAverageMaxMs.Size = new System.Drawing.Size(30, 17);
			this.labelAverageMaxMs.TabIndex = 7;
			this.labelAverageMaxMs.Text = "ms.";
			this.labelAverageMaxMs.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// textAverageCount
			// 
			this.textAverageCount.Location = new System.Drawing.Point(106, 48);
			this.textAverageCount.MaxVal = 1000;
			this.textAverageCount.MinVal = 1;
			this.textAverageCount.Name = "textAverageCount";
			this.textAverageCount.Size = new System.Drawing.Size(45, 20);
			this.textAverageCount.TabIndex = 20;
			this.textAverageCount.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
			// 
			// textAverage
			// 
			this.textAverage.Location = new System.Drawing.Point(106, 72);
			this.textAverage.Name = "textAverage";
			this.textAverage.ReadOnly = true;
			this.textAverage.Size = new System.Drawing.Size(55, 20);
			this.textAverage.TabIndex = 8;
			this.textAverage.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
			// 
			// textAverageMin
			// 
			this.textAverageMin.Location = new System.Drawing.Point(106, 96);
			this.textAverageMin.Name = "textAverageMin";
			this.textAverageMin.ReadOnly = true;
			this.textAverageMin.Size = new System.Drawing.Size(55, 20);
			this.textAverageMin.TabIndex = 9;
			this.textAverageMin.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
			// 
			// textAverageMax
			// 
			this.textAverageMax.Location = new System.Drawing.Point(106, 120);
			this.textAverageMax.Name = "textAverageMax";
			this.textAverageMax.ReadOnly = true;
			this.textAverageMax.Size = new System.Drawing.Size(55, 20);
			this.textAverageMax.TabIndex = 10;
			this.textAverageMax.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
			// 
			// butAverage
			// 
			this.butAverage.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butAverage.Location = new System.Drawing.Point(8, 140);
			this.butAverage.Name = "butAverage";
			this.butAverage.Size = new System.Drawing.Size(70, 24);
			this.butAverage.TabIndex = 21;
			this.butAverage.Text = "Test";
			this.butAverage.Click += new System.EventHandler(this.butAverage_Click);
			// 
			// groupBoxAverage
			// 
			this.groupBoxAverage.Controls.Add(this.labelAverageExplain);
			this.groupBoxAverage.Controls.Add(this.labelAverageCount);
			this.groupBoxAverage.Controls.Add(this.textAverageCount);
			this.groupBoxAverage.Controls.Add(this.labelAverage);
			this.groupBoxAverage.Controls.Add(this.textAverage);
			this.groupBoxAverage.Controls.Add(this.labelAverageMs);
			this.groupBoxAverage.Controls.Add(this.labelAverageMin);
			this.groupBoxAverage.Controls.Add(this.textAverageMin);
			this.groupBoxAverage.Controls.Add(this.labelAverageMinMs);
			this.groupBoxAverage.Controls.Add(this.labelAverageMax);
			this.groupBoxAverage.Controls.Add(this.textAverageMax);
			this.groupBoxAverage.Controls.Add(this.labelAverageMaxMs);
			this.groupBoxAverage.Controls.Add(this.butAverage);
			this.groupBoxAverage.Location = new System.Drawing.Point(374, 12);
			this.groupBoxAverage.Name = "groupBoxAverage";
			this.groupBoxAverage.Size = new System.Drawing.Size(206, 172);
			this.groupBoxAverage.TabIndex = 10;
			this.groupBoxAverage.TabStop = false;
			this.groupBoxAverage.Text = "Average latency";
			// 
			// FormTestLatency
			// 
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.ClientSize = new System.Drawing.Size(592, 196);
			this.Controls.Add(this.groupBoxAverage);
			this.Controls.Add(this.groupBox2);
			this.Controls.Add(this.groupBox1);
			this.Name = "FormTestLatency";
			this.Text = "Roundtrip latency";
			this.Load += new System.EventHandler(this.FormTestLatency_Load);
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.groupBox2.ResumeLayout(false);
			this.groupBox2.PerformLayout();
			this.groupBoxAverage.ResumeLayout(false);
			this.groupBoxAverage.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion
		private UI.Button butLatency;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.TextBox textLatency;
		private System.Windows.Forms.Label label2;
		private OpenPmsDental.UI.GroupBox groupBox1;
		private OpenPmsDental.UI.GroupBox groupBox2;
		private System.Windows.Forms.Label label3;
		private UI.Button butSpeed;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.TextBox textSpeed;
		private OpenPmsDental.UI.GroupBox groupBoxAverage;
		private System.Windows.Forms.Label labelAverageExplain;
		private System.Windows.Forms.Label labelAverageCount;
		private OpenPmsDental.ValidNum textAverageCount;
		private System.Windows.Forms.Label labelAverage;
		private System.Windows.Forms.TextBox textAverage;
		private System.Windows.Forms.Label labelAverageMs;
		private System.Windows.Forms.Label labelAverageMin;
		private System.Windows.Forms.TextBox textAverageMin;
		private System.Windows.Forms.Label labelAverageMinMs;
		private System.Windows.Forms.Label labelAverageMax;
		private System.Windows.Forms.TextBox textAverageMax;
		private System.Windows.Forms.Label labelAverageMaxMs;
		private UI.Button butAverage;
	}
}