using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenDentBusiness;
using WpfControls.UI;
using CodeBase;

namespace OpenDental {
	/// <summary>Lists the conditional children of a checkbox or radio button field in eForm setup (Open Dental 25.1): every field whose conditional
	/// parent is this field, with the value that makes it show. Double clicking a child opens its editor. Opened from the parent field's editor.</summary>
	public partial class FrmEFormFieldChildren : FrmODBase {
		///<summary>The field whose children are listed. Set before opening. Children are matched on its saved label.</summary>
		public EFormField EFormFieldParent;
		///<summary>Every field of the form in the designer: the same list the parent's editor uses. Set before opening.</summary>
		public List<EFormField> ListEFormFields;
		///<summary>Opens a field's editor the way double clicking it in the form designer does (FrmEFormDefEdit). Returns false if the field is no
		///longer in the form. When null, the list can only be viewed.</summary>
		public Func<EFormField,bool> FuncEditField;
		///<summary>After closing: true if a child's editor was opened, so the parent's editor recounts its children.</summary>
		public bool IsChildEdited;
		///<summary>The children showing in the grid, in form order.</summary>
		private List<EFormField> _listEFormFieldsChildren;

		///<summary></summary>
		public FrmEFormFieldChildren() {
			InitializeComponent();
			Load+=FrmEFormFieldChildren_Load;
			PreviewKeyDown+=FrmEFormFieldChildren_PreviewKeyDown;
			gridMain.CellDoubleClick+=GridMain_CellDoubleClick;
		}

		private void FrmEFormFieldChildren_Load(object sender, EventArgs e) {
			Lang.F(this);
			textParent.Text=EFormFieldChildren.GetDisplayLabel(EFormFieldParent);
			if(FuncEditField==null){
				labelExplain.Text=Lang.g(this,"These fields show only when the parent field has the value in the Shows When column.");
			}
			FillGrid();
		}

		private void FillGrid(){
			_listEFormFieldsChildren=EFormFieldChildren.GetChildren(EFormFieldParent,ListEFormFields);
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			GridColumn gridColumn=new GridColumn(Lang.g("TableEFormFieldChildren","Field"),250);
			gridColumn.IsWidthDynamic=true;
			gridMain.Columns.Add(gridColumn);
			gridColumn=new GridColumn(Lang.g("TableEFormFieldChildren","Type"),110);
			gridMain.Columns.Add(gridColumn);
			gridColumn=new GridColumn(Lang.g("TableEFormFieldChildren","Shows When"),150);
			gridMain.Columns.Add(gridColumn);
			gridMain.ListGridRows.Clear();
			for(int i=0;i<_listEFormFieldsChildren.Count;i++){
				GridRow gridRow=new GridRow();
				gridRow.Cells.Add(EFormFieldChildren.GetDisplayLabel(_listEFormFieldsChildren[i]));
				gridRow.Cells.Add(Lang.g("enumEFormFieldType",EFormFieldChildren.GetTypeText(_listEFormFieldsChildren[i].FieldType)));
				gridRow.Cells.Add(EFormFieldChildren.GetTriggerText(EFormFieldParent,_listEFormFieldsChildren[i]));
				//gridRow.Tag=;//not needed because the grid always matches the list.
				gridMain.ListGridRows.Add(gridRow);
			}
			gridMain.EndUpdate();
			if(_listEFormFieldsChildren.Count==0){
				gridMain.Title=Lang.g(this,"Child Fields")+" ("+Lang.g(this,"none")+")";
			}
			else{
				gridMain.Title=Lang.g(this,"Child Fields");
			}
		}

		private void GridMain_CellDoubleClick(object sender,GridClickEventArgs e) {
			if(e.Row<0 || e.Row>=_listEFormFieldsChildren.Count){
				return;
			}
			if(FuncEditField==null){
				MsgBox.Show(this,"Open this field from the eForm designer to edit its children.");
				return;
			}
			EFormField eFormFieldChild=_listEFormFieldsChildren[e.Row];
			IsChildEdited=true;
			if(!FuncEditField(eFormFieldChild)){
				MsgBox.Show(this,"That field is no longer in the form.");
			}
			//The child may have been changed, given another parent or deleted.
			FillGrid();
		}

		private void FrmEFormFieldChildren_PreviewKeyDown(object sender,KeyEventArgs e) {
			if(butClose.IsAltKey(Key.C,e)) {
				butClose_Click(this,new EventArgs());
			}
		}

		private void butClose_Click(object sender, EventArgs e) {
			//Nothing to save here. Child edits were applied by their own editors, and the form is saved from the designer.
			IsDialogOK=true;
		}
	}
}
