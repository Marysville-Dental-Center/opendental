using System;
using System.Windows.Input;
using CodeBase;
using OpenDentBusiness;

namespace OpenDental {
	///<summary>Procedure Note Merge (Open Dental 25.4, package M2-NOTES). Shown by Procedure Info and the Group Note window when another user saved a
	///newer note on the procedure while the window was open. Shows their note (read-only, with who saved it and when), my note (read-only) and a
	///merged note to edit, pre-filled with their note, a separator line, then the text I added (ProcNoteMerges.GetMergedDefault). The caller acts on
	///MergeChoice and, for Save Merged, NoteMerged.</summary>
	public partial class FrmProcNoteMerge:FrmODBase {
		///<summary>Cancel unless the user picked Keep Mine, Keep Theirs or Save Merged.</summary>
		public ProcNoteMergeChoice MergeChoice=ProcNoteMergeChoice.Cancel;
		///<summary>The merged note as edited, when MergeChoice is SaveMerged.</summary>
		public string NoteMerged="";
		private readonly string _noteTheirs;
		private readonly string _noteMine;
		private readonly string _noteMergedDefault;
		private readonly string _textTheirsHeading;
		private readonly bool _hasAnySignature;

		///<param name="noteTheirs">The newer note saved by the other user.</param>
		///<param name="userNameTheirs">Who saved it.</param>
		///<param name="dateTimeTheirs">When it was saved.</param>
		///<param name="isTheirsSigned">True when their note is signed (either box).</param>
		///<param name="noteMine">My note as typed in the window.</param>
		///<param name="noteMergedDefault">The merged note to start with.</param>
		///<param name="isMineSigned">True when I signed in the window.</param>
		public FrmProcNoteMerge(string noteTheirs,string userNameTheirs,DateTime dateTimeTheirs,bool isTheirsSigned,string noteMine,string noteMergedDefault,
			bool isMineSigned)
		{
			InitializeComponent();
			_noteTheirs=noteTheirs??"";
			_noteMine=noteMine??"";
			_noteMergedDefault=noteMergedDefault??"";
			_textTheirsHeading=Lang.g("FrmProcNoteMerge","Their note, saved by")+" "+userNameTheirs+" "+dateTimeTheirs.ToString()
				+(isTheirsSigned ? " ("+Lang.g("FrmProcNoteMerge","signed")+")" : "");
			_hasAnySignature=isTheirsSigned || isMineSigned;
			Load+=FrmProcNoteMerge_Load;
			PreviewKeyDown+=FrmProcNoteMerge_PreviewKeyDown;
		}

		private void FrmProcNoteMerge_Load(object sender,EventArgs e) {
			Lang.F(this);
			labelTheirs.Text=_textTheirsHeading;
			textTheirs.Text=_noteTheirs;
			textMine.Text=_noteMine;
			textMerged.Text=_noteMergedDefault;
			labelSignatures.Visible=_hasAnySignature;
			textMerged.Focus();
		}

		private void FrmProcNoteMerge_PreviewKeyDown(object sender,KeyEventArgs e) {
			if(butSaveMerged.IsAltKey(Key.S,e)) {
				butSaveMerged_Click(this,new EventArgs());
			}
		}

		private void butKeepMine_Click(object sender,EventArgs e) {
			MergeChoice=ProcNoteMergeChoice.KeepMine;
			IsDialogOK=true;
		}

		private void butKeepTheirs_Click(object sender,EventArgs e) {
			MergeChoice=ProcNoteMergeChoice.KeepTheirs;
			IsDialogOK=true;
		}

		private void butSaveMerged_Click(object sender,EventArgs e) {
			MergeChoice=ProcNoteMergeChoice.SaveMerged;
			NoteMerged=textMerged.Text;
			IsDialogOK=true;
		}

		private void butCancel_Click(object sender,EventArgs e) {
			MergeChoice=ProcNoteMergeChoice.Cancel;
			IsDialogCancel=true;
		}
	}
}
