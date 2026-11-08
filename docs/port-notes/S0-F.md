# Port note: S0-F (fixes for the stage 0 window checks)

Package S0-F of stage 0 of the .NET 10 move, on branch `net10/s0f-1` from `main` (bb446f65a), with the review fixes on `net10/s0f-2`. The
stage 0 window checks (computer-use run on
the Debug build, 2026-11-08) found four issues besides the owner decisions they raised: the process stayed alive after File > Exit, a
deleted image left its thumbnail, the log-off prompt's X still logged off, and the Email Authentication box of Edit Email Address was mostly
empty. Synthetic data only; no window of the program was launched. Tests: the new assembly `tests/S0FTests` (scratch database `s0f_*`,
built from `db/init/new-database.sql` and the fork migrations, dropped afterwards), one smoke test in LibWebTests, and two source checks in
LibWebTests and M2IMGTests that follow the Imaging module's new print browser.

## 1. The process stayed alive after File > Exit

**What was seen.** After a long session that had used the EOB window's PDF viewer (WebView2, `EmbeddedBrowserWebView.dll` loaded), File >
Exit closed every window, but the process stayed for more than 3 minutes with 4 waiting threads, no CPU and no network. Fresh starts closed
in about 3 seconds.

**What was found.** No launch was allowed, so the cause was looked for in the code and with off-screen probes: x86 PowerShell with the
WebView2 SDK of `Required dlls` and WebView2 Runtime 157, windows at -6000,-6000 without a task bar entry, own profiles in a temp folder, no
network, and each probe killed after 40 to 60 seconds if it had not ended.

- `FormImages`' viewer is disposed. It is among the window's controls (`LayoutManager.Add` puts it into `PanelClient`), every caller opens
  the window in a `using` block, and `ShowDialog` destroys the window's handle when it returns, which the WebView2 control answers by moving
  its browser off the window before `Dispose` closes it.
- A WebView2 dialog shown with a PDF and disposed that way, also four in a row, with a WPF `ElementHost` in the dialog and in the main window,
  with the viewer hidden before closing (as Delete hides it), never disposed, and a WebView2 in the main window disposed by the main window's
  `Dispose` in `FormClosed`: in every case `Environment.Exit` from the UI thread's `FormClosed` ended the process within about a second, and
  no browser process was left.
- .NET Framework 4.8 ends `Environment.Exit` within seconds even when a finalizer never returns (probe: an object whose finalizer waits
  forever; the process ended 3 seconds after start).
- `FormOpenDental_FormClosed` calls `Dispose()` and then `Environment.Exit(0)`. `Control.Dispose` destroys every window handle first and
  disposes the child controls after, so the windows disappear before the disposal is done.

File > Exit is `Application.Exit`, which raises `FormClosing` and then `FormClosed` on the open windows; the main window stays on the screen
until the `Dispose` in its `FormClosed` destroys its handle. So the windows were gone means `FormClosed` had run at least up to that
`Dispose`. From there, two places fit what was seen, and **the root cause is not proven**:

- **Before `Environment.Exit` (a guess, not shown).** The main thread was held up inside the main window's `Dispose` (after the windows were
  gone), or `Dispose` threw and `Main` returned with a foreground thread left (ODThreads are foreground by default; `QuitSyncAllOdThreads`
  only asks them to stop; the exception would have gone to `Application.ThreadException`, whose error window the check did not see). Such a
  process still has the runtime's own threads (the finalizer, the debugger helper), the WPF render thread once WPF content was shown, the
  threads WebView2 starts in the program's process once it was used, and every ODThread that did not stop. 4 threads in all is low for
  that, though not impossible.
- **Inside `ExitProcess`, where `Environment.Exit` ends.** `ExitProcess` first ends every other thread of the process and then, under the
  loader lock, sends `DLL_PROCESS_DETACH` to each loaded DLL. A detach that waits (for a thread, or a browser process, for example in
  `EmbeddedBrowserWebView.dll`) hangs the process for good, with its main thread and any thread started after that point, which waits for
  the loader lock: few threads, all waiting, no CPU. That fits the 4 waiting threads as well as the guess above, or better. The off-screen
  probes above ended through the same `Environment.Exit` after WebView2 had been used (so with `EmbeddedBrowserWebView.dll` loaded) and did
  not hang, so if the hang is there, it depends on what the long session did.

The check recorded only the number of threads, not which they were, so the count cannot decide it. It was not reproduced with WebView2
alone, so it may not be WebView2's at all; the long session also used the Chart module (EHR amendments), e-mail windows and program link
setup windows, which the fresh-start runs did not. The launch check below is set up to tell the two places apart.

**What changed.**

- `ExitWatchdog` (at the end of `ProgramEntry.cs`, so no file is added to the classic project): started in `FormOpenDental_FormClosed`,
  from where nothing can cancel the exit or waits for the user. If the process still runs 15 seconds later, it writes the last step of the
  closing and the process's threads to the log (Logger, folder `Exit`), calls `Environment.Exit` itself, and 5 seconds after that ends the
  process with `TerminateProcess` (exit code 1). It covers a closing held up before `ExitProcess` (the first place above, and the runtime's
  own shutdown inside `Environment.Exit`). It cannot end a hang inside `ExitProcess`: its threads, `ExitWatchdog` and `ExitWatchdogKill`,
  are ended with the others before the detach starts, so it writes nothing and the process stays as before.
- `FormOpenDental_FormClosed` names its steps for the watchdog, closes every `ODWebView2` of the main window (the Imaging module's PDF
  viewer, the eCW panel) before `Dispose`, while their windows still exist, and logs an exception of `Dispose` instead of skipping
  `Environment.Exit`.
- `ProgramEntry.Main` ends the process (`Environment.Exit(FormOpenDental.ExitCode)`, with the watchdog) should `Application.Run` ever return
  without it.
- `FormImages` disposes its viewer in `FormClosed`, while the window still exists, as `FormCareCreditWeb` does, and `LoadPdf` stops when the
  window closed while a file downloaded or the viewer started.
- `ODWebView2.Init` initializes a control once: a call while the initialization runs, or after it succeeded, gets the same task. Two PDFs
  shown before the viewer had started (the EOB window and the Imaging module call `Init` from `LoadPdf`) made a second environment, which
  WebView2 refuses with an `ArgumentException`; `Init` reported it as "the WebView2 Runtime could not start".
- `ControlImageDisplay.PrintPDF` (Imaging module, Print category) prints with a browser of its own, `_odWebView2Print`, disposed with the
  control. It used to replace the viewer with a new browser in no window that nothing ever closed (inherited from Open Dental 24.3): it ran
  until the program ended, and the viewer then showed nothing.

**Proven by tests.** `ExitWatchdogTests` (the watchdog with its actions replaced: logs the last step and the threads, then
`Environment.Exit`, then terminates; nothing within the grace time; a failing log never keeps the process alive; the wiring of `FormClosed`
and `Main`); `WebViewHostWiringTests` (FormImages, the print browser, `Init` once); LibWebTests
`PdfViewer_StartsOnce_AndLeavesNoBrowserProcessOnceItsWindowCloses` (the viewer as FormImages uses it, off screen: two starts at once are one,
the PDF shows, and once its window closed its browser process ends).

**Launch check (the lead).** Repeat the long session (EOB windows with JPG and PDF imports, zoom, deletes; Chart > EHR > Amendments; the
e-mail windows; program link setup windows), then File > Exit, and watch the process (`OpenPmsDental.exe`) in Task Manager's Details tab
with its Threads column on (right-click a column header, Select columns). The watchdog writes into the `Exit` folder of the program's log:
`Logger\Exit` next to `OpenPmsDental.exe`, or `Documents\Logger\Exit` when verbose logging is on.

- **It ends within a few seconds, no entry in `Logger\Exit`:** the hang did not come back. That does not show which change removed it (the
  browsers now close with their windows) or that it cannot come back; repeat the session once more before calling it gone.
- **It ends after about 15 to 20 seconds, with an entry:** the closing was held up before `ExitProcess`, while the watchdog's threads still
  ran. The entry's last step names where (a browser of the main window, the main window's disposal, or `Environment.Exit`, which then means
  the runtime's own shutdown) and lists the threads; that is where the cause is.
- **It is still running 30 seconds after File > Exit:** before ending it, write down its thread count and capture a full dump (Task
  Manager's Details tab, right-click the process, Create dump file; or `procdump -ma <pid>` where ProcDump is installed). The dump holds the
  process's memory: synthetic data only, and it stays on the test PC. The dump of Task Manager is 64-bit for the 32-bit process; in WinDbg
  `.load wow64exts` and `.effmach x86` before `~*k` show the stacks. Then end the process and look in `Logger\Exit`:
  - **No entry:** the watchdog's threads (`ExitWatchdog`, `ExitWatchdogKill`) had been ended, and only `ExitProcess` does that, before it
    sends `DLL_PROCESS_DETACH`. The hang is inside `ExitProcess`. The main thread's stack proves it: `ntdll!RtlExitUserProcess` and
    `ntdll!LdrShutdownProcess` below the frames of the module whose detach waits, which names the module. Should the main thread be
    anywhere else, the watchdog itself was held up before it wrote, and the stacks show where.
  - **An entry with only its first message:** the closing was held up before `ExitProcess` (the entry names where), and then the
    watchdog's own `Environment.Exit` hung inside `ExitProcess`, which ended `ExitWatchdogKill` as well; the stack of the thread left in
    `ntdll!LdrShutdownProcess` (the `ExitWatchdog` thread) names the module as above.
  - **An entry that also says "Environment.Exit did not end the process within 5 seconds either":** `TerminateProcess` did not end it (a
    thread held in the kernel, for example by a driver); the dump's stacks show which.

A hang inside `ExitProcess` would need a fix of another kind than the watchdog: by the module's owner, or by ending the process without the
DLL detach (`TerminateProcess` once the closing is done, instead of `Environment.Exit`). That is a change of its own, for the owner to decide
once the dump names the module.

## 2. A deleted image left its thumbnail

Inherited: Open Dental 24.3's `ImageStore.DeleteDocuments` deletes the image and its rows but never `Thumbnails\<file name>` in the patient's
folder, so a deleted image stayed on disk as its 100x100 copy. `Mounts.Delete` leaves `Thumbnails\Mount<num>.jpg` the same way.

- `ImageStore.DeleteDocuments` deletes each document's thumbnail after its file (every delete path goes through it).
- The Imaging module's mount delete and its move of a mount to another patient delete the mount's thumbnail.
- `ImageStore.DeleteThumbnailFile`: thumbnails are files only with an A to Z folder (`Documents.GetThumbnail`, `Mounts.GetThumbnail` and
  `ControlImageDisplay` save them nowhere else), so with cloud storage or images in the database nothing is deleted and nothing is sent.
  Only a plain file name in an existing patient folder; never throws (a locked thumbnail stays, as before).

Tests: `ThumbnailDeleteTests` (the file rules, and `DeleteDocuments` on the scratch database with a scratch A-to-Z folder). **Launch check:**
delete an image in the Imaging module; `Thumbnails\` of the patient's folder no longer has it.

## 3. The log-off prompt: X still logged off

In the code X (and Esc) has always meant No: `FormFrame` closes with Cancel, `IsDialogCancel` is true, `AreYouSurePrompt` returns false and
`menuItemLogOff_Click` stops. What logged off was the prompt's countdown (`InputBox.HasTimeout`, inherited, used only by this prompt and
the close prompt): 15 seconds after it shows, it closes itself as Yes, whatever the user is doing, although it is described as "close in 15
seconds if no user input". A computer-use click takes longer than that.

Now any input in the box stops the countdown for good: a key, a mouse button, the mouse moved over the box or over its title bar and borders,
and the box closing. A move counts once the cursor left the spot where it was when the box opened. A prompt nobody touches still goes ahead
after 15 seconds (the log off or close the user asked for); X and Esc stay No. The rules are in `InputBoxTimeout`.

Tests: `LogOffPromptTests` (the countdown alone; a real `InputBox` on an STA thread through its handlers, not shown; the wiring). **Launch
check:** File > Log Off, move the mouse onto the box, wait more than 15 seconds, then X: no log off. Then Log Off and touch nothing for 15
seconds: it logs off.

## 4. Edit Email Address: the Email Authentication box

The box (962x308 at 96 dpi, as in Open Dental) was made for the Gmail and Microsoft 365 sign-in boxes; since ground rules 6 and 7 those
choices show only when their program link is enabled, so with the links off it held one choice and the password field.

`FormEmailAddressEdit.LayoutAuthentication` (from `RefreshEmailTextFields`, so on Load and on every change of choice) stacks the shown
choices from the top, puts the password or the chosen sign-in's box to their right, all at one place (only one shows at a time), and ends the
box just below the taller of the two; the incoming and outgoing settings and the user follow, and so does the window's height (the buttons
are anchored to its bottom; on Load the window stays centered). With the links off the box is one line, 60 instead of 308 pixels; with Gmail
chosen the layout is the designer's. The designer keeps every box in its own place for editing (the password panel moved up 12 pixels so its
label lines up with its choice). No option was removed.

Tests: `EmailAddressLayoutTests` (every state, from the designer's sizes). **Launch check (100% and the designer):** Setup > Manage > E-mail >
Add with the Gmail and Microsoft 365 program links off: the box is one line and the window has no empty area; with a link on, choosing it
shows its box beside the choices and the window grows, choosing Password again shrinks it; open the window in the designer: it opens, and
every box is where it was.
