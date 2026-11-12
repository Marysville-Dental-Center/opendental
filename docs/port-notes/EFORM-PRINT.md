# EFORM-PRINT: printing an eForm from the Fill eForm window (port note)

Bug fix package, found by the stage 1 gate's headless window checks (check D5, branch `winchk/headless-1`,
`tests/WindowChecksTests/InProc/InProcChecks.DE.cs`). Branch `fix/eformprint-1`.

## The failure

Main toolbar Forms > Add eForm > a copy of the internal "Patient Registration" eForm > the Fill eForm window opens > Print.
An unhandled `System.ArgumentOutOfRangeException`: "The parameter value must be greater than zero. Parameter name: pixelHeight". No preview
opens. Stack: `RenderTargetBitmap..ctor` < `OpenPmsDental.Drawing.Graphics.DrawFrameworkElement` (`WpfControlsOD/UI/Graphics.cs`) <
`CtrlEFormFill.Pd_PrintPage` (`WpfControlsOD/UI/Controls/UserControls/CtrlEFormFill.xaml.cs`) < `PrinterL.CreateFixedDocument` <
`PrinterL.PreviewClassic`. Reproduced 3 times by the window checks.

## The cause

`CtrlEFormFill` lays an eForm out as a vertical stack panel of rows (`gridWrap`: a drop border on the left and a wrap panel of the row's fields).
`SetVisibilities` hides a field that conditional logic hides (`EFormField.IsHiddenCondit`) by collapsing the field's own grid (`gridForField`), not
its row. A row whose fields are **all** hidden therefore stays in the stack panel with an `ActualHeight` of 0 (its left drop border has a width
and no height).

`Pd_PrintPage` walks the rows, skips the drag borders and draws a line for page breaks ("a pageBreak will also be 0 height"), and hands every
other row to `Graphics.DrawFrameworkElement`. That method makes a `RenderTargetBitmap` of the element's size, rounded up, and `RenderTargetBitmap`
refuses 0 pixels. So any eForm with a row of conditionally hidden fields throws when printed. That is most eForms with conditional logic: Patient
Registration (32 of its fields depend on the responsible party and insurance questions, which are unanswered on a new form), Medical History and Dental History all
threw; Consent and HIPAA did not.

The same path is the one Save to Images uses (`FrmEFormFillEdit.butSaveImage_Click` calls `PrinterL.CreateFixedDocument` too), so it failed the
same way.

`Graphics.DrawFrameworkElement` has no other caller: only `Pd_PrintPage` (a row, and a signature box's label).

## Did stage 1 cause it? No.

S1-4 (merge `f012859f4`) moved WpfControlsOD to the SDK's WPF markup compiler. A XAML resource or style compiled differently could, in
principle, give a field no height. It does not here:

- **History.** The bug is an upstream regression in Open Dental 24.3, carried in our code since then (`git log -L` on each method):
  - `Pd_PrintPage`: in Open Dental 24.3.1.0 (`be14fe1be`) it skipped **every** row of zero height. After a loop that drew the line for a page
    break, and before the loop that looked for a signature box, it had
    `if(frameworkElement.ActualHeight==0){ //a pageBreak will also be 0 height` `_printedStackPanelChildren=i; continue; }`. That skip caught
    page breaks and rows whose fields are all hidden alike. Open Dental 24.3.5.0 (`379b84bae`) rewrote the method: it replaced the page break
    loop with the test `eFormField.FieldType==EnumEFormFieldType.PageBreak` (which draws the line and continues on its own) and the signature box
    loop with a single test, and dropped the general zero-height skip. From 24.3.5.0 on, a row of hidden fields falls through to
    `DrawFrameworkElement`. The later 24.3 versions did not touch the method; the only change since is the M2-EF port (`b3509b350`), which added
    the signed caption under a signature box (`PrintSigCaption`).
  - `SetVisibilities`: the per-field collapse for conditional logic (`gridForField` collapsed, not the row) is in 24.3.1.0 already, so 24.3.1.0
    also had rows of height 0 when printing; its skip handled them. Open Dental 24.3.10.0 (`3bdbc0448`) changed the method: the drag location
    border above a row (`borderTopOfWrap`) is now collapsed too when every field in the row below is hidden by conditional logic, so the screen
    keeps no extra space. The row itself is still left in the stack panel with height 0.
  - `Graphics.DrawFrameworkElement`: unchanged since 24.3.1.0 (`be14fe1be`) until this fix; it never guarded against a zero size.

  All of these commits predate S1-4. The rows and fields are built in code (`RefreshLayout`), not in XAML; `CtrlEFormFill.xaml` holds only the
  scroll viewer, the stack panel and the page buttons.
- **The cause, checked directly.** `PatientRegistration_ZeroHeightRowsAreExactlyTheConditionallyHiddenOnes` shows that after the print layout
  (every page shown) a row has height 0 exactly when all its fields are hidden by conditional logic, and every row with a field to show has a
  height. It passes before and after the fix.
- **The old build.** The focused tests were run on `dad90694b`, the main commit just before S1-4's merge (its first parent; WpfControlsOD still
  a classic project with the classic markup compiler), checked out detached in this worktree, built through the lane, with the test project
  added only in the working tree (never committed there; it needed three temporary changes for that commit: no `DevDbTls`, which came later,
  `LangVersion` 8.0 and no package lock file, which the test projects did not have yet). Result: the same 7 of 8 failures as on this branch
  before the fix, with the same exception and stack (`Graphics.cs` line 221 < `CtrlEFormFill.xaml.cs` line 525); the zero-height test passed
  there too. The scratch database was dropped. The worktree was then restored and switched back.

So the bug was there before stage 1. It is an upstream regression from Open Dental 24.3.5.0, carried in the 24.3 code we imported.

## The fix

1. `CtrlEFormFill.Pd_PrintPage`: the fix puts back 24.3.1.0's skip in the same place. A row of height 0 is marked printed and skipped, after
   the page break branch (a page break row is 0 high and still prints its line, as before) and before the signature box branch, just as
   24.3.1.0's skip came after its page break loop and before its signature box loop. So a hidden signature box prints neither its label nor
   its signature, as in 24.3.1.0. The one addition: a row of width 0 is skipped too, since `RenderTargetBitmap` refuses that as well. A comment
   at the check cites 24.3.1.0.
2. `Graphics.DrawFrameworkElement`: a collapsed element, or one without width or height, draws nothing instead of throwing. This covers the
   other call (a signature box's label) and any later caller.

The precedent is upstream's own 24.3.1.0 code, which 24.3.5.0 dropped in a refactor (see History above). No later upstream version in
our history (it ends at 24.3.63.0, `d804c1954`) put the skip back, and we have no upstream source after 24.3 and do not decompile. Either way,
the fix matches the screen: a row that shows nothing prints nothing.

### A second fault the tests found: an empty label

Printing an eForm with an empty label (a Label field whose ValueLabel is "") never got as far as printing. `FillFieldsFromControls` writes each
label's document back through `EFormFields.SerializeFlowDocument`. `XamlWriter` writes an empty document self-closing,
`<FlowDocument PagePadding="0,0,0,0" AllowDrop="True" xmlns="..." />`, and the pattern that strips the opening tag's attributes
(`<FlowDocument[^>]*>`) also swallowed the slash. The label came back as an unclosed `<FlowDocument>`, and the next `RefreshLayout` (the fill
window's own sizing on load, or the one after Print) threw `XamlParseException`. `SerializeFlowDocument` now first gives a self-closing tag its
closing tag, so the empty document is stored as `<FlowDocument></FlowDocument>`, which `DeserializeFlowDocument` loads. Documents with content
are written exactly as before. This is also 24.3 code.

## Tests

New test assembly `tests/EFormPrintTests` (MSTest, x86, in `OpenDental.sln` and `OpenDental.Local.slnf`). The fill control is built on its own
STA thread the way `FrmEFormFillEdit` sets it up on load and sizes it (`SetCtrlWidth`), laid out without a window, and printed the way Print does:
`FillFieldsFromControls`, a `Printout` with `Pd_PrintPage` and half-inch margins, `PrinterL.CreateFixedDocument` (the document of the print
preview). No window is shown and the program is not launched. One scratch database `efp_*` from `db/init/new-database.sql` and the fork
migrations, with one synthetic patient; dropped afterwards (skipped, inconclusive, without a local MariaDB or with `EFORMPRINT_TEST_DB=skip`).

| Test | Before the fix | After |
|---|---|---|
| `PatientRegistration_Prints`: a new Patient Registration eForm, filled for the patient as Forms > Add eForm does, prints; it has rows of height 0; every visible row is drawn once | ArgumentOutOfRangeException (pixelHeight) | passes |
| `EveryInternalEForm_Prints`: Patient Registration, Medical History, Consent, Dental History, HIPAA | 3 throw (Patient Registration, Medical History, Dental History) | passes |
| `PatientRegistration_ZeroHeightRowsAreExactlyTheConditionallyHiddenOnes` (the cause, see above) | passes | passes |
| `HiddenAndEmptyFields_Print`: a hidden text field, a hidden row of two fields, an empty label without a border, a visible field; only the visible rows are drawn | XamlParseException (empty label) | passes |
| `HiddenAndEmptySignatureBoxes_Print`: the same with a hidden signature box and one with an empty label | XamlParseException (empty label) | passes |
| `PageOfHiddenFieldsOnly_Prints`: a page break followed only by hidden fields | ArgumentOutOfRangeException (pixelHeight) | passes |
| `DrawFrameworkElement_ZeroSize_DrawsNothing`: 0 x 0, 100 x 0, 0 x 40 and a collapsed element draw nothing; a sized one draws | ArgumentOutOfRangeException (pixelWidth) | passes |
| `SerializeFlowDocument_EmptyDocument_RoundTrips`: an empty document loads again; a document with text is written as before | XamlParseException | passes |

Targeted runs: NoHqTests and EFormPrintTests, plus M2EFTests and EFormTests, whose tests read `CtrlEFormFill`'s source and use
`SerializeFlowDocument`.

## Not done

- No on-screen pass: the print preview window itself (`FrmPrintPreview`) was not opened. The window checks' D5 should be run again after the
  merge; it should now open the preview.
