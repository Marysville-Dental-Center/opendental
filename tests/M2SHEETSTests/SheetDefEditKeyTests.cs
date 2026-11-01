// The Sheet Def editor's key and mouse handling around the duplicate field warning (fix round t1). The editor is built in memory and never
// shown; its key and mouse handlers are called the way Windows calls them, and the warning is answered by a stand-in for the modal box. The
// stand-in returns without a key-up, just as the real box swallows the key-up of the Ctrl (Ctrl+V) or Alt (Alt+click stamp) that started the
// paste. No database: en-US needs no translations and nothing here reads the cache. Synthetic sheet only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDentalBusiness;

namespace M2SHEETSTests {
	[TestClass]
	public class SheetDefEditKeyTests {
		private const BindingFlags FLAGS=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;

		///<summary>Runs the action on a single-threaded apartment thread in en-US, as WinForms controls need.</summary>
		private static void RunSta(Action action) {
			Exception exception=null;
			Thread thread=new Thread(() => {
				try {
					Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
					Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
					action();
				}
				catch(Exception ex) {
					exception=ex;
				}
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			thread.Join();
			if(exception!=null) {
				throw new AssertFailedException("Failed on the STA thread: "+exception,exception);
			}
		}

		///<summary>The Sheet Def editor with a patient form that has one "misc" input field with a reportable name, selected and copied with Ctrl+C.
		///The 24.3 copy prompt does not flag "misc" fields, so pasting that copy asks the duplicate field question; Answer answers it.</summary>
		private class Editor:IDisposable {
			public readonly FormSheetDefEdit Form;
			public readonly SheetFieldDef FieldOriginal;
			///<summary>How many times the duplicate field question was asked.</summary>
			public int CountAsked;
			///<summary>The answer to the question: true for OK, false for Cancel.</summary>
			public bool Answer;

			public Editor() {
				FieldOriginal=new SheetFieldDef {
					FieldType=SheetFieldType.InputField,
					FieldName="misc",
					ReportableName="Reason for visit",
					FieldValue="",
					RadioButtonValue="",
					RadioButtonGroup="",
					Language="",
					FontName="Arial",
					FontSize=11f,
					XPos=40,
					YPos=900,
					Width=200,
					Height=40,
				};
				SheetDef sheetDef=new SheetDef {
					SheetType=SheetTypeEnum.PatientForm,
					Description="Synthetic form",
					FontName="Arial",
					FontSize=11f,
					Width=850,
					Height=1100,
					SheetFieldDefs=new List<SheetFieldDef> { FieldOriginal },
					Parameters=new List<SheetParameter>(),
				};
				Form=new FormSheetDefEdit(sheetDef);
				FieldInfo fieldInfoAsk=typeof(FormSheetDefEdit).GetField("_funcAskDuplicateFields",FLAGS);
				Assert.IsNotNull(fieldInfoAsk,"The editor asks through _funcAskDuplicateFields.");
				fieldInfoAsk.SetValue(Form,new Func<string,bool>(msg => {
					CountAsked++;
					StringAssert.Contains(msg,"misc (Reason for visit)");
					return Answer;
				}));
				Form.FillFieldList();
				Assert.AreEqual(1,ListBox.Items.Count);
				ListBox.SetSelected(0);
				KeyDown(Keys.ControlKey|Keys.Control);
				KeyDown(Keys.C|Keys.Control);
				KeyUp(Keys.ControlKey);//No box for a "misc" field, so the key-up arrives.
				Assert.AreEqual(1,Get<List<SheetFieldDef>>("_listSheetFieldDefsCopyPaste").Count,"Copied.");
				Assert.IsFalse(Get<bool>("_isDuplicatePasteAccepted"));
				Assert.IsFalse(IsCtrlDown);
				Assert.AreEqual(0,CountAsked);
			}

			public OpenPmsDental.UI.ListBox ListBox => Get<OpenPmsDental.UI.ListBox>("listBoxFields");

			public bool IsCtrlDown => Get<bool>("_isCtrlDown");

			public bool IsAltDown => Get<bool>("_isAltDown");

			///<summary>How many copies of the field the sheet has, the original included.</summary>
			public int CountFields => Form.SheetDef_.SheetFieldDefs.Count(x => x.FieldName=="misc" && x.ReportableName=="Reason for visit");

			public T Get<T>(string name) {
				FieldInfo fieldInfo=typeof(FormSheetDefEdit).GetField(name,FLAGS);
				Assert.IsNotNull(fieldInfo,"No field "+name);
				return (T)fieldInfo.GetValue(Form);
			}

			private void Invoke(string methodName,object eventArgs) {
				MethodInfo methodInfo=typeof(FormSheetDefEdit).GetMethod(methodName,FLAGS);
				Assert.IsNotNull(methodInfo,"No method "+methodName);
				try {
					methodInfo.Invoke(Form,new object[] { Form,eventArgs });
				}
				catch(TargetInvocationException ex) {
					throw ex.InnerException;
				}
			}

			///<summary>The window's KeyDown. keyData includes the modifier flags, for example Keys.V|Keys.Control.</summary>
			public void KeyDown(Keys keyData) {
				Invoke("FormSheetDefEdit_KeyDown",new KeyEventArgs(keyData));
			}

			public void KeyUp(Keys keyData) {
				Invoke("FormSheetDefEdit_KeyUp",new KeyEventArgs(keyData));
			}

			///<summary>A left click on the sheet, in panel pixels. Near the top left corner (the default) is blank sheet.</summary>
			public void Click(int x=2,int y=2) {
				Invoke("panelMain_MouseDown",new System.Windows.Forms.MouseEventArgs(MouseButtons.Left,1,x,y,0));
				Invoke("panelMain_MouseUp",new System.Windows.Forms.MouseEventArgs(MouseButtons.Left,1,x,y,0));
			}

			public void Dispose() {
				Form.Dispose();
			}
		}

		///<summary>Scenario 1 of the review: Ctrl+V of a copy that duplicates a field, answered OK. Before the fix Ctrl stayed "down" because the box
		///ate its key-up: a click on blank sheet kept the selection (Ctrl+click adds to it), and V alone pasted another copy without asking.</summary>
		[TestMethod]
		public void CtrlV_AfterOk_CtrlIsNoLongerDown() {
			RunSta(() => {
				using Editor editor=new Editor();
				editor.Answer=true;
				editor.KeyDown(Keys.ControlKey|Keys.Control);
				editor.KeyDown(Keys.V|Keys.Control);
				Assert.AreEqual(1,editor.CountAsked,"Asked once.");
				Assert.AreEqual(2,editor.CountFields,"Pasted.");
				Assert.IsFalse(editor.IsCtrlDown,"The box ate the key-up of Ctrl, so Ctrl is reset.");
				Assert.AreEqual(1,editor.ListBox.SelectedIndices.Count,"The pasted field is selected.");
				editor.Click();
				Assert.AreEqual(0,editor.ListBox.SelectedIndices.Count,"A plain click on blank sheet clears the selection.");
				editor.KeyDown(Keys.V);
				Assert.AreEqual(2,editor.CountFields,"V alone does not paste.");
				//Ctrl pressed again works as before: the user agreed to this copy, so the paste does not ask again.
				editor.KeyDown(Keys.ControlKey|Keys.Control);
				editor.KeyDown(Keys.V|Keys.Control);
				Assert.AreEqual(1,editor.CountAsked,"Not asked again for the same copy.");
				Assert.AreEqual(3,editor.CountFields);
				Assert.IsTrue(editor.IsCtrlDown,"No box this time, so Ctrl stays down until its key-up.");
				editor.KeyUp(Keys.ControlKey);
				Assert.IsFalse(editor.IsCtrlDown);
			});
		}

		///<summary>Ctrl+V answered Cancel: nothing is pasted, Ctrl is not stuck, and V alone neither pastes nor asks.</summary>
		[TestMethod]
		public void CtrlV_AfterCancel_CtrlIsNoLongerDown() {
			RunSta(() => {
				using Editor editor=new Editor();
				editor.Answer=false;
				editor.KeyDown(Keys.ControlKey|Keys.Control);
				editor.KeyDown(Keys.V|Keys.Control);
				Assert.AreEqual(1,editor.CountAsked);
				Assert.AreEqual(1,editor.CountFields,"Not pasted.");
				Assert.IsFalse(editor.IsCtrlDown);
				editor.KeyDown(Keys.V);
				Assert.AreEqual(1,editor.CountAsked,"V alone does not paste, so it does not ask.");
				Assert.AreEqual(1,editor.CountFields);
				Assert.IsFalse(editor.Get<bool>("_isDuplicatePasteAccepted"),"Cancel does not accept the copy.");
			});
		}

		///<summary>Scenario 2 of the review: Alt+click stamping. Before the fix, after OK every later click stamped another copy (with the cross
		///cursor still showing), and after Cancel every click asked again, until Alt was pressed again.</summary>
		[TestMethod]
		public void AltClick_AfterOkOrCancel_AltIsNoLongerDown() {
			RunSta(() => {
				using Editor editor=new Editor();
				//Cancel.
				editor.Answer=false;
				editor.KeyDown(Keys.Menu|Keys.Alt);
				Assert.IsTrue(editor.IsAltDown);
				Assert.AreEqual(Cursors.Cross,editor.Form.Cursor,"Stamp cursor.");
				editor.Click(300,300);
				Assert.AreEqual(1,editor.CountAsked);
				Assert.AreEqual(1,editor.CountFields,"Not stamped.");
				Assert.IsFalse(editor.IsAltDown,"The box ate the key-up of Alt, so Alt is reset.");
				Assert.AreEqual(Cursors.Default,editor.Form.Cursor);
				editor.Click(300,300);
				Assert.AreEqual(1,editor.CountAsked,"A plain click does not ask again.");
				Assert.AreEqual(1,editor.CountFields);
				//OK.
				editor.Answer=true;
				editor.KeyDown(Keys.Menu|Keys.Alt);
				editor.Click(300,300);
				Assert.AreEqual(2,editor.CountAsked);
				Assert.AreEqual(2,editor.CountFields,"Stamped.");
				Assert.IsFalse(editor.IsAltDown);
				Assert.AreEqual(Cursors.Default,editor.Form.Cursor);
				Assert.AreEqual(0,editor.Get<int>("_pasteOffset"),"A stamp lands where clicked and does not move the cascade of the next Ctrl+V.");
				editor.Click(300,300);
				Assert.AreEqual(2,editor.CountFields,"A plain click does not stamp.");
				//Alt pressed again stamps as before, without asking again for the same copy.
				editor.KeyDown(Keys.Menu|Keys.Alt);
				editor.Click(300,300);
				editor.Click(400,400);
				Assert.AreEqual(2,editor.CountAsked,"Not asked again for the same copy.");
				Assert.AreEqual(4,editor.CountFields,"Stamped twice while Alt is held.");
				Assert.IsTrue(editor.IsAltDown,"No box this time, so Alt stays down until its key-up.");
				Assert.AreEqual(0,editor.Get<int>("_pasteOffset"));
				editor.KeyUp(Keys.Menu);
				Assert.IsFalse(editor.IsAltDown);
				Assert.AreEqual(Cursors.Default,editor.Form.Cursor);
			});
		}

		///<summary>Ctrl+V of a copy that duplicates nothing shows no box and leaves Ctrl down for the next Ctrl+key, as in 24.3, and moves the
		///cascade offset for the next paste.</summary>
		[TestMethod]
		public void CtrlV_WithoutDuplicate_NoBoxAndCtrlStaysDown() {
			RunSta(() => {
				using Editor editor=new Editor();
				editor.Form.SheetDef_.SheetFieldDefs.Remove(editor.FieldOriginal);
				editor.Form.FillFieldList();
				editor.KeyDown(Keys.ControlKey|Keys.Control);
				editor.KeyDown(Keys.V|Keys.Control);
				Assert.AreEqual(0,editor.CountAsked);
				Assert.AreEqual(1,editor.CountFields,"Pasted.");
				Assert.IsTrue(editor.IsCtrlDown);
				Assert.AreEqual(10,editor.Get<int>("_pasteOffset"),"Ctrl+V cascades.");
				editor.KeyDown(Keys.V|Keys.Control);
				Assert.AreEqual(1,editor.CountAsked,"The second paste duplicates the first.");
				Assert.IsFalse(editor.IsCtrlDown);
			});
		}
	}
}
