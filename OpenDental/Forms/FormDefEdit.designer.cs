using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenDental {
	public partial class FormDefEdit {
		private System.ComponentModel.IContainer components = null;// Required designer variable.

		///<summary></summary>
		protected override void Dispose( bool disposing ){
			if( disposing ){
				if(components != null){
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormDefEdit));
			this.labelName = new System.Windows.Forms.Label();
			this.labelValue = new System.Windows.Forms.Label();
			this.textName = new System.Windows.Forms.TextBox();
			this.textValue = new System.Windows.Forms.TextBox();
			this.butColor = new System.Windows.Forms.Button();
			this.colorDialog1 = new System.Windows.Forms.ColorDialog();
			this.labelColor = new System.Windows.Forms.Label();
			this.butSave = new OpenDental.UI.Button();
			this.checkHidden = new OpenDental.UI.CheckBox();
			this.butDelete = new OpenDental.UI.Button();
			this.butSelect = new OpenDental.UI.Button();
			this.butClearValue = new OpenDental.UI.Button();
			this.checkNoColor = new OpenDental.UI.CheckBox();
			this.SuspendLayout();
			// 
			// labelName
			// 
			this.labelName.Location = new System.Drawing.Point(12, 45);
			this.labelName.Name = "labelName";
			this.labelName.Size = new System.Drawing.Size(178, 17);
			this.labelName.TabIndex = 0;
			this.labelName.Text = "Name";
			this.labelName.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
			// 
			// labelValue
			// 
			this.labelValue.Location = new System.Drawing.Point(190, 34);
			this.labelValue.Name = "labelValue";
			this.labelValue.Size = new System.Drawing.Size(178, 28);
			this.labelValue.TabIndex = 1;
			this.labelValue.Text = "Value";
			this.labelValue.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
			// 
			// textName
			// 
			this.textName.Location = new System.Drawing.Point(12, 64);
			this.textName.Multiline = true;
			this.textName.Name = "textName";
			this.textName.Size = new System.Drawing.Size(178, 64);
			this.textName.TabIndex = 0;
			// 
			// textValue
			// 
			this.textValue.Location = new System.Drawing.Point(190, 64);
			this.textValue.MaxLength = 256;
			this.textValue.Multiline = true;
			this.textValue.Name = "textValue";
			this.textValue.Size = new System.Drawing.Size(178, 64);
			this.textValue.TabIndex = 1;
			// 
			// butColor
			// 
			this.butColor.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
			this.butColor.Location = new System.Drawing.Point(371, 64);
			this.butColor.Name = "butColor";
			this.butColor.Size = new System.Drawing.Size(30, 20);
			this.butColor.TabIndex = 2;
			this.butColor.Click += new System.EventHandler(this.butColor_Click);
			// 
			// colorDialog1
			// 
			this.colorDialog1.FullOpen = true;
			// 
			// labelColor
			// 
			this.labelColor.Location = new System.Drawing.Point(371, 46);
			this.labelColor.Name = "labelColor";
			this.labelColor.Size = new System.Drawing.Size(74, 16);
			this.labelColor.TabIndex = 5;
			this.labelColor.Text = "Color";
			this.labelColor.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// butSave
			// 
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.Location = new System.Drawing.Point(341, 319);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 25);
			this.butSave.TabIndex = 4;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			// 
			// checkHidden
			// 
			this.checkHidden.Location = new System.Drawing.Point(12, 12);
			this.checkHidden.Name = "checkHidden";
			this.checkHidden.Size = new System.Drawing.Size(157, 18);
			this.checkHidden.TabIndex = 3;
			this.checkHidden.Text = "Hidden";
			// 
			// butDelete
			// 
			this.butDelete.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butDelete.Icon = OpenDental.UI.EnumIcons.DeleteX;
			this.butDelete.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butDelete.Location = new System.Drawing.Point(12, 319);
			this.butDelete.Name = "butDelete";
			this.butDelete.Size = new System.Drawing.Size(79, 25);
			this.butDelete.TabIndex = 6;
			this.butDelete.Text = "Delete";
			this.butDelete.Click += new System.EventHandler(this.butDelete_Click);
			// 
			// butSelect
			// 
			this.butSelect.Location = new System.Drawing.Point(371, 106);
			this.butSelect.Name = "butSelect";
			this.butSelect.Size = new System.Drawing.Size(21, 22);
			this.butSelect.TabIndex = 200;
			this.butSelect.Text = "...";
			this.butSelect.Click += new System.EventHandler(this.butSelect_Click);
			// 
			// butClearValue
			// 
			this.butClearValue.AdjustImageLocation = new System.Drawing.Point(1, 0);
			this.butClearValue.Image = global::OpenDental.Properties.Resources.deleteX18;
			this.butClearValue.Location = new System.Drawing.Point(395, 106);
			this.butClearValue.Name = "butClearValue";
			this.butClearValue.Size = new System.Drawing.Size(21, 22);
			this.butClearValue.TabIndex = 201;
			this.butClearValue.Click += new System.EventHandler(this.butClearValue_Click);
			// 
			// checkNoColor
			// 
			this.checkNoColor.Location = new System.Drawing.Point(260, 16);
			this.checkNoColor.Name = "checkNoColor";
			this.checkNoColor.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
			this.checkNoColor.Size = new System.Drawing.Size(141, 18);
			this.checkNoColor.TabIndex = 203;
			this.checkNoColor.Text = "No Color";
			this.checkNoColor.Visible = false;
			this.checkNoColor.CheckedChanged += new System.EventHandler(this.checkNoColor_CheckedChanged);
			// 
			// FormDefEdit
			// 
			this.AcceptButton = this.butSave;
			this.ClientSize = new System.Drawing.Size(423, 352);
			this.Controls.Add(this.checkNoColor);
			this.Controls.Add(this.butClearValue);
			this.Controls.Add(this.butSelect);
			this.Controls.Add(this.butDelete);
			this.Controls.Add(this.checkHidden);
			this.Controls.Add(this.butSave);
			this.Controls.Add(this.butColor);
			this.Controls.Add(this.textValue);
			this.Controls.Add(this.textName);
			this.Controls.Add(this.labelValue);
			this.Controls.Add(this.labelName);
			this.Controls.Add(this.labelColor);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "FormDefEdit";
			this.ShowInTaskbar = false;
			this.Text = "Edit Definition";
			this.Load += new System.EventHandler(this.FormDefEdit_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}
		#endregion

		private System.Windows.Forms.Label labelName;
		private System.Windows.Forms.Label labelValue;
		private System.Windows.Forms.TextBox textName;
		private System.Windows.Forms.TextBox textValue;
		private System.Windows.Forms.Button butColor;
		private System.Windows.Forms.ColorDialog colorDialog1;
		private OpenDental.UI.Button butSave;
		private UI.Button butSelect;
		private System.Windows.Forms.Label labelColor;
		private OpenDental.UI.CheckBox checkHidden;
		private OpenDental.UI.Button butDelete;
		private UI.Button butClearValue;
		private OpenDental.UI.CheckBox checkNoColor;
	}
}
