# Port note: LIB-WEB, WebView2 in place of the Internet Explorer WebBrowser control

Branch `next/libweb-1`, cut from `next/libbase-2` (0c5fa6369); the review fixes are on `next/libweb-2` (own page files and history never
raise `LinkClicked`, what a page may load, find in page, one profile folder for the Images print), the second round on `next/libweb-3` (no
email stylesheet runs in the program, a runtime that will not start is reported, smoke tests fail on the control's own errors). Owner approval of 2026-11-02 (roadmap
item 2, library updates for maintenance). The follow-up of the owner answers of 2026-11-02 to questions 2 and 7 (pages from memory, one message
when a window's browser cannot start) is on `next/fu-web-1`, cut from `next/int-2`: see [Follow-up](#follow-up-owner-answers-to-questions-2-and-7). The WebView2 SDK stays at 1.0.2420.47 (`Required dlls`: `Microsoft.Web.WebView2.Core`, `.WinForms`, `.Wpf`, `WebView2Loader.dll`);
no DLL was added, replaced or removed, so the binding redirects, the release allow list, the installer file list and the notices are unchanged.

## Sources and rules

- WebView2's behaviour comes from Microsoft's public WebView2 documentation; no binary was decompiled. The smoke test (`tests/LibWebTests`) checks
  the behaviour the windows rely on against the installed runtime. In particular `CoreWebView2.WebResourceRequested` is raised for file, http
  and https addresses (Microsoft's reference; not for virtual host mappings, which is why the pages stay `file:` pages; the follow-up answers
  the requests of a page's own `file:` address from memory), and
  `CoreWebView2NavigationStartingEventArgs.NavigationKind` exists from SDK 1.0.1901 on.
- Synthetic data only. No database, table, column or preference name changed. No migration.

## The shared setup (`CodeBase/Controls`)

- **`WebView2Policy`** holds the rules every browser of the program follows, for the WinForms wrapper (`ODWebView2`) and the WPF one
  (`WpfControls.UI.WebView2`):
  - The user data folder is `%LOCALAPPDATA%\OpenPMS-Dental\WebView2` (`ProgramNames.LocalAppDataFolderName`), per Windows user, never the
    installation folder. Before, the WinForms control used `%LOCALAPPDATA%\WindowsFormsWebView2` and the WPF one the program's temp folder,
    which the program empties at every close. The old folders are left alone (the first has a generic name another program could share).
  - No host objects (`AreHostObjectsAllowed=false`; nothing calls `AddHostObjectToScript`), dev tools only in Debug builds.
  - Link rules for pages the program shows: only http, https and mailto addresses are ever handed to the shell (`OpenInSystemBrowser`); a
    relative link such as `www.example.com` counts as http, as the old control did.
- **`ODWebView2.NavigateToHtml(html, ...)`** replaces the IE control's `DocumentText`. On `next/libweb-3` the page was written to a new file
  (UTF-8 with a byte order mark) in the program's temp folder, or in a folder the window gave, and that file was shown, so the local images the
  wiki and emails refer to still load (a page from a string cannot load `file:` images). Since the follow-up the page and the files of its own
  folder are in memory and nothing is written (see [Follow-up](#follow-up-owner-answers-to-questions-2-and-7)); everything below holds for
  them as it did for the files. While such a page is shown:
  - scripts, web messages, script dialogs, autofill and password saving are off; the program's own `ExecuteScriptAsync` calls still run;
  - every other top-level navigation is cancelled; a link the user clicked raises `LinkClicked` (with `IsNewWindow` for Ctrl+click and
    "Open link in new window"); a navigation the page starts by itself (meta refresh) raises nothing;
  - WebView2 counts its own `Navigate`/`GoBack` calls, Back and Forward (mouse buttons, Alt+Left) and reloads as user initiated, so
    `LinkClicked` is raised only for a navigation to a new document (`NavigationKind`, SDK 1.0.1901 and later; an older runtime keeps the
    old rule), and never for a page file the control wrote itself: the control remembers every page file, and a navigation to one that is
    not the current page is cancelled silently. This covers two pages shown back to back (arrowing through the inbox, where the first page's
    own navigation starts after the second took its place) and Back to a replaced page. Swipe navigation is off for these pages.
    `GetSystemBrowserUrl` also never turns a file or folder that is there into a web address;
  - **what the page may load** (`WebResourceRequested` with a `*` filter for every request kind, `WebView2Policy.IsPageResourceAllowed`):
    files only from the page's own folder and the folders the window passes (`listFoldersAllowed`), `data:`, `blob:` and `about:`, and
    http/https only when the window passes `isRemoteContentAllowed`. Everything else gets an empty 403, so the request never leaves the
    computer. In particular a `file://other-computer/share/...` image never loads: Windows would open it over SMB or WebDAV and send the
    user's NTLM credentials to that computer. A path with an escaped separator, a `..` segment or a stream name is refused. Until the
    browser shows the current page (its `Source`), a request may still come from the page before it, so nothing remote loads in that
    moment. `RemoteContentBlocked` tells the window once per page that the page wanted web content. Internet Explorer probably loaded all
    of this; the port now writes every received email to a `file:` page, so the rules are needed here;
  - frames load only `about:blank`, new windows, downloads, external protocols and permission requests are refused;
  - the context menu keeps only Copy, Select all, the link and image copy commands and "Open link in new window"
    (`IsContextMenuEnabled=false` removes it);
  - the page leaves memory when the next page replaces it and when the control is disposed (on `next/libweb-3`: the page file was deleted then,
    and the temp folder emptied at close).
  - The control initializes itself on the first `NavigateToHtml`; a missing runtime is reported by `Init`.
- **A runtime that will not start** (installed but damaged, `msedgewebview2.exe` blocked by antivirus or AppLocker, a profile folder that
  cannot be used): `Init` catches what `CreateEnvironmentAsync` and `EnsureCoreWebView2Async` throw, and the initialization event reports
  `!IsSuccess` the same way. The error goes to the log (`WebView2` folder), and the user sees one message per program session (a static flag,
  so the inbox's two previews and every wiki window do not each show one) with the error, its HRESULT and what an administrator can do:
  repair or reinstall the runtime (Apps and features), check that `msedgewebview2.exe` is not blocked, check that the profile folder is
  writable. `Init` then throws `ODException`, so the windows' "Init told the user" catches are true. A control closed while it starts
  (disposed, `E_ABORT`) is no failure and is not reported. Before, only a missing runtime was reported: a failing one left the windows that
  only call `NavigateToHtml` blank without a word, and `FormWebView` and `FormWebBrowserPrefs` closed without one.
  Windows that show `FriendlyException` when `Init` throws (`FormCareCreditWeb`, `FormPayConnect2`, `FormPayConnect2iFrame`, the Images
  module's PDF print) showed it after that message on `next/libweb-3`, as they already did for a missing runtime; since the follow-up they
  show exactly one message (owner answer to question 7, see [Follow-up](#follow-up-owner-answers-to-questions-2-and-7)).
- What each window allows its pages (`MarkupEdit.GetImageFolders` gives the A to Z `Wiki` or `EmailImages` folder when the A to Z folder is
  local or on a share, and the folder that holds images downloaded from cloud storage: for the wiki the in-memory pages' shared folder since the
  follow-up, for email the temp folder). A page's "own folder" is its in-memory folder, which holds the page and the files the window gave
  it (an email's images after Show Images), nothing else; on `next/libweb-3` it was the program's temp folder unless the window gave a folder:

  | Window | Folders | Web content |
  |---|---|---|
  | `FormWiki` and its previews (`FormWikiAllPages`, `FormWikiDrafts`, `FormWikiEdit`, `FormWikiHistory`, `FormWikiIncomingLinks`, `FormWikiSearch`) | wiki images | yes (the office's own pages; the master page may name a web style sheet) |
  | `FormEmailEdit`, `FormEmailTemplateEdit` | email images | yes (the office's own email, as the recipient will see it) |
  | `EmailPreviewControl`, sent email and drafts | email images | yes |
  | `EmailPreviewControl`, received email | its own folder only | **no**, until the user clicks Show Images, which now also appears when the email asked for web content (a remote image tells the sender when and from where the email was read) |
  | `UserControlEmailTemplate` (no caller) | email images | no |
  | `FormEhrSummaryCcdEdit` (the CCD can come with an email), `FormWebView.HtmlContent` (a clearinghouse's response), `FormWebBrowserPrefs` | its own folder only | no |

  `EmailPreviewControl` now also forgets the image parts and the Show Images button of the email shown before (the inbox reuses one control).
- **Browser keys**: `ODWebView2` turned off the browser's accelerator keys everywhere, which removed find in page (Ctrl+F, F3) from windows
  whose Internet Explorer control kept its shortcuts (`WebBrowserShortcutsEnabled` left at its default, true). `AreBrowserKeysEnabled`
  (default false) keeps them; it is true on `FormWiki`, `FormWikiIncomingLinks`, `FormEhrSummaryCcdEdit`, `FormEhrEduBrowser`,
  `FormEmailTemplateEdit`, `UserControlEmailTemplate`, the eCW panel and `FormWebView` showing html (the old `FormWebBrowser` of
  `FormEtrans270Edit`). The windows whose IE control had them off (`FormEmailEdit`, `EmailPreviewControl`, `FormWikiAllPages`,
  `FormWikiDrafts`, `FormWikiEdit`, `FormWikiHistory`, `FormWikiSearch`) and the windows that were WebView2 already keep them off. The keys
  also bring Ctrl+P (the browser's print preview), reload (the same page again) and Alt+Left/Right (cancelled on these pages); the dev
  tools stay off in Release builds (`AreDevToolsEnabled`).
- **One profile folder for every environment**: `ControlImageDisplay` printed PDFs with print settings from a second environment created
  without a user data folder (`CoreWebView2Environment.CreateAsync()`), whose profile WebView2 puts next to the exe, in the installation
  folder, which users cannot write to, so printing a PDF from the Images module is expected to fail for them (the catch only sets
  `ShowError`; not reproduced here, the line predates this branch). It now uses the
  control's own environment (`CoreWebView2.Environment.CreatePrintSettings()`); a test checks that only the two wrappers create an
  environment and that every control is initialized with one.
- `ShowPrintDialog()` (Windows' print dialog through `CoreWebView2.ShowPrintUI`), `StatusText`/`StatusTextChanged`, `GetScrollTopAsync`,
  `SetScrollTop`, `SetBodyZoom` and `SetCookie` (the eCW panel) cover what the windows used from the IE control.
- `Init` no longer offers to download the runtime: it says that Windows 11 includes it and that an administrator installs it from Microsoft.
- `CodeBase.HtmlPage` builds the pages the program writes itself, with `Encode` for every text from the database, and `BuildXml` shows an
  XML document as encoded, indented text (its document type skipped: no entity expands, nothing external is read).

## Every use of the IE browser, and what became of it

| File | What it showed | What it needed | Now |
|---|---|---|---|
| `CodeBase/Utilities/MiscUtils.cs` `TryUpdateIeEmulation` | nothing (wrote `FEATURE_BROWSER_EMULATION` under HKCU) | IE emulation mode | **Deleted**: no caller (the CareCredit page that used it moved to WebView2 earlier, see the comment in `FormPayment`). |
| `OpenPmsDental/Forms/FormWebBrowser.cs` (+ designer, resx) | remote pages (URL, optional POST) or an HTML string | navigation events (`UrlCloseOn`), SHDocVw `NewWindow2/3`, back/forward, print | **Deleted.** Its two callers are ported (next rows). Its `EnumFormName.FormWebBrowser` had no launcher and is removed. |
| `OpenPmsDental/Logic/EdgeExpressL.cs` | the card processor's hosted pay page, IE fallback in embedded mode without WebView2 | `UrlCloseOn` | **Ported**: without WebView2 both return modes open the page in the system browser with the wait window ("I'm finished"); the built-in return address never resolves. |
| `OpenPmsDental/Forms/FormEtrans270Edit.cs` | the clearinghouse's HTML benefits response | an HTML string, print, find (`FormWebBrowser`'s IE shortcuts were on) | **Ported** to `FormWebView.HtmlContent` (scripts off, links to the default browser, no file but its own and nothing from the web, Ctrl+F through `AreBrowserKeysEnabled`). |
| `OpenPmsDental/Forms/FormWebView.cs` | vendor pages (PDMP reports, postcard editor), now also HTML strings | | **Fixed**: `HtmlContent` was navigated before `Init` (never shown), Print did nothing, the title event was never subscribed, Refresh repainted instead of reloading. Vendor pages load only http/https; pop-ups go to the default browser through `SystemBrowser.Open`. |
| `OpenPmsDental/OpenPmsDental.csproj` | | COM reference `SHDocVw` (embedded types) | **Removed** (used only by `FormWebBrowser` and an unused `using` in `ControlChart`). |
| `OpenPmsDental/Forms/FormWiki.cs` (+ designer) | generated wiki pages (`MarkupEdit.TranslateToXhtml`) with local images | `Navigating` for `wiki:`, `wikifile:`, `folder:`, `wikifilecloud:`, `foldercloud:` and web links, `NewWindow` (right-click), `StatusText`, print, `CoInternetSetFeatureEnabled` (IE click sound), find (Ctrl+F; IE shortcuts on) | **Ported**: `NavigateToHtml` (wiki image folders, web content allowed), `LinkClicked` with `FormWiki.ParseLink`, `StatusTextChanged`, `ShowPrintDialog`, Ctrl+F and F3 through `AreBrowserKeysEnabled`. The urlmon call is IE-only and removed. |
| `FormWikiAllPages`, `FormWikiDrafts`, `FormWikiHistory`, `FormWikiIncomingLinks`, `FormWikiSearch` | wiki page previews | `AllowNavigation` toggled to ignore links; context menu off; find only in `FormWikiIncomingLinks` (the others set `WebBrowserShortcutsEnabled=false`) | **Ported**: `NavigateToHtml` (wiki image folders, web content allowed), no link handler (links do nothing), `IsContextMenuEnabled=false` where IE's menu was off, `AreBrowserKeysEnabled` on `FormWikiIncomingLinks`. |
| `OpenPmsDental/Forms/FormWikiEdit.cs` | live preview of the page being edited | DOM `scrollTop` kept across refreshes, body `zoom`; no find (shortcuts off) | **Ported**: `GetScrollTopAsync`/`SetScrollTop`/`SetBodyZoom` after `NavigationCompleted`; wiki image folders, web content allowed. |
| `OpenPmsDental/Forms/FormEmailEdit.cs` | preview of the email being written (generated or raw HTML) | links to the default browser, `DocumentText` read back as `HtmlText`; no find (shortcuts off) | **Ported**: `NavigateToHtml` (email image folders, web content allowed), `LinkClicked`; `HtmlText` is the kept HTML string. |
| `OpenPmsDental/Forms/FormEmailTemplateEdit.cs` | preview of a template in the master template | links to the default browser, find (shortcuts on) | **Ported**: email image folders, web content allowed, `AreBrowserKeysEnabled`. |
| `OpenPmsDental/User Controls/EmailPreviewControl.cs` | received and sent emails (untrusted HTML); "Show images" writes the MIME images and the page to a temp folder | links to the default browser, `DocumentText` read back; no find (shortcuts off) | **Ported**: scripts off, `NavigateToHtml` with the MIME images as the page's own files for "Show images" (in memory since the follow-up; `next/libweb-3` wrote them and the page to a temp folder), `LinkClicked`; a meta refresh or script in an email can no longer open anything. A received email loads nothing from the web and no file outside its folder until Show Images (see above). |
| `OpenPmsDental/User Controls/UserControlEmailTemplate.cs` | template preview (the control has no caller) | find (shortcuts on) | **Ported**: email image folders, nothing from the web, `AreBrowserKeysEnabled`. |
| `OpenPmsDental/Forms/FormEhrEduBrowser.cs` | an education resource's web page (office-configured URL), reached from the EHR tools (`FormEHR`, `FormEhrEduResourcesPat`) | print (records the EHR measure), find (shortcuts on) | **Ported**: http/https only, pop-ups to the default browser, `ShowPrintDialog`, `AreBrowserKeysEnabled`. |
| `OpenPmsDental/Forms/FormEhrSummaryCcdEdit.cs` | a CCD file with its XSL stylesheet (IE ran XSLT) | XSLT, print, find (shortcuts on) | **Ported without the stylesheet**: Open Dental's stylesheets came from EHR.dll (`EhrSummaryCcds.GetEhrResource` returns "" since the decoupling), so the only stylesheet left is one an email brings along (`EmailPreviewControl.OpenFile`), and the program never runs it. XSLT in the program's process (the first round's `HtmlPage.TransformXsl`) let a received `.xsl` end the program: a self-recursive template overflows the stack, and a `StackOverflowException` ends the process whatever catches it (the review reproduced this in a 32-bit .NET Framework 4.8 process); a looping one hangs it. `DisplayCCD` no longer reads the attachment: the window shows the CCD as encoded, indented XML (`HtmlPage.BuildXml`) with a line saying that the stylesheet that came with it was not run, keeps print, find and the reconcile buttons of a Direct message, scripts off, no file but its own and nothing from the web. Without an attached stylesheet `DisplayCCD` shows the raw text as before. **Since EHR-3** (`docs/port-notes/EHR-3.md`): the program's own stylesheets are back (HL7's CDA-core-xsl `CDA.xsl` for a CCD, the AAFP's `ccr.xsl`, a copy marked as modified, for a CCR), and they run only in a short-lived child process of the program's own exe (`OpenPmsDental.exe /ehr-render`, `EhrRenderChild`, started and killed by `EhrStylesheetStarter`), never in the program's process; a stylesheet that came with an email still never runs. `DisplayCCD` checks the limits on the text (32 MB, 200 levels, no DTD), renders a CCD or a CCR (with or without an attached stylesheet) and gives the window the page (`htmlRendered`); when that fails the window shows the XML view with the reason, and a document over the limits as plain text. The window's settings are unchanged. `HtmlGenerationTests` allow XSLT in exactly that child. |
| `OpenPmsDental/Main Modules/ControlChart.cs` (+ designer) | eCW's medical panel (remote dashboard on the eCW server) | session cookies through wininet `InternetSetCookie` (IE's cookie store), find (shortcuts on) | **Ported**: cookies through WebView2's `CookieManager` for the eCW host, http/https only, pop-ups to the default browser, a label instead of a message box without the runtime, `AreBrowserKeysEnabled`. An invalid Medical Panel URL no longer navigates anyway. |
| `FormPayConnect2`, `FormPayConnect2iFrame` | nothing (hidden IE control next to the WebView2 one; its `message` handler was empty) | | **Deleted** (dead). |
| `FormWebBrowserPrefs` (from `FormPreferences`) | preference details (HTML from `PrefInfos.xml`) | | Hidden IE control **deleted**; the WebView2 page uses `NavigateToHtml`, links open in the default browser, label and preference name are encoded (`BuildHtml`). |
| `WpfControlsOD/UI/Controls/WebBrowser.xaml(.cs)` | WPF wrapper of `System.Windows.Controls.WebBrowser` | | **Deleted**; its only user was `FrmTestAllControls`. |
| `WpfControlsOD/Frms/FrmTestAllControls`, `FrmTestSplitters` | developer test windows browsing a remote site in IE | | Now a local test page in `WpfControls.UI.WebView2`, nothing online. |
| `WpfControlsOD/UI/Controls/WebView2.xaml.cs` | (WebView2 wrapper, unused elsewhere) | | **Fixed**: the shared user data folder, secure settings, `ShowPrintDialog`; it called the IE control's `ActiveXInstance` after every navigation, which WebView2 does not have. |
| `OpenPmsDental/UIManagement/WpfConverter.cs` | (developer tool) | converted WebBrowser to `ui:WebBrowser` | Converts `ODWebView2` to `ui:WebView2`. |
| `UnitTests/UI Testing/FormTestAllControls.Designer.cs` | (test form) | | IE control removed (UnitTests does not reference WebView2). |
| `FormAbout.cs`, `FormEligibilityResponseDisplay.cs`, `ControlImagesOld.cs`, `MarkupEdit.cs`, `EdgeExpress.cs` | | commented `using mshtml;`, an unused load counter and commented IE code, comments | Removed or reworded. |

Third-party: `Microsoft.Identity.Client.dll` (MSAL) contains the IE sign-in window of .NET Framework. The program never uses it: the
Microsoft 365 sign-in passes `WithUseEmbeddedWebView(false)` (system browser), and `LibWebTests` checks every interactive sign-in for it.

## Installer

`installer/OpenPmsDental.iss` still checks the WebView2 Runtime (the `pv` value of its EdgeUpdate client key). When it is missing, a wizard
page tells the administrator that Windows 11 includes it and how to install it (Microsoft's Evergreen Bootstrapper or Standalone Installer,
Windows Update or device management), and the log says the same for silent installs. Setup no longer downloads or runs the WebView2 installer;
`/PREREQS` now covers the Visual C++ runtime only.

## Tests

`tests/LibWebTests` (new, in `OpenDental.sln` and `OpenDental.Local.slnf`):

- `IeBrowserRemovedTests`: no IE browser type, mshtml, SHDocVw, emulation key or IE-only call in shipped source, project files,
  `Required dlls` or the built program folder (Mono.Cecil); the installer never downloads the runtime, nor does the program.
- `WebView2PolicyTests`: profile folder (and that only the two wrappers create an environment, every control gets one, and the Images
  print uses its control's), dev tools, host objects, link (a file that is there is never a web address), frame, page resource (own and
  allowed folders, shares, escaped separators, the web), context menu and cookie rules, swipe off, and what each window allows its pages
  and where the browser keys stay on.
- `HtmlGenerationTests`: encoding, the CCD as XML (`BuildXml`: indented, encoded, no entity, no external file, broken XML as it came), that
  no shipped source outside the report engine and a developer tool, and none of the program's built assemblies, uses an XSLT processor, and
  that `DisplayCCD` never reads the attachment; the preference page, the wiki's link kinds.
- `WebView2StartFailureTests`: through internal seams (`ODWebView2.FuncCreateEnvironment`, `FuncIsRuntimeAvailable`, `ActionShowMessage`;
  `InternalsVisibleTo("LibWebTests")` in CodeBase), an environment that fails: `NavigateToHtml` does not throw, the user is told once with the
  error, HRESULT and the administrator's steps, a second control does not tell again, `Init` throws `ODException`; the initialization event
  reports the same way, but not for a closed control or `E_ABORT`; a missing runtime is still told as before.
- `WebView2SmokeTests` (off-screen windows):
  - a generated page: encoded text shows as text, the page's script does not run, a local image loads, clicked wiki and web links raise
    `LinkClicked` and are not followed, a meta refresh and a frame are cancelled; two pages shown back to back without waiting, then
    `GoBack`, raise no `LinkClicked` (with Runtime 156 the first page's own navigation does start, user initiated, after the second page
    took its place, and Back is user initiated too: the case the review found);
  - what a page may load: from html from outside the office, only the image in its folder, never one in an allowed folder, outside the
    folder (also through `../`), `C:\Windows\win.ini`, `http://img.invalid/` or `file://unc.invalid/` (each request is seen and refused
    by the control; the share is tried only after a local file was seen refused, so nothing reaches `unc.invalid`), and
    `RemoteContentBlocked` once; from the office's own page also the allowed folder and the web image (which the test answers itself).
  Only local and `.invalid` addresses, background networking off. Inconclusive only when the runtime is missing, when a plain WebView2
  control (none of the program's code) cannot start in the test's environment, or with `LIBWEB_SKIP_WEBVIEW2=1`. After that any error fails:
  an exception of the test or the control, an exception in an event handler on the window's thread (`Application.SetUnhandledExceptionMode`
  and `ThreadException` on that thread, so WinForms shows no dialog that would block it), a message the control shows, the 3 minutes running
  out, and an exception the control's own code throws on that thread (first-chance, by the throwing method): WebView2 drops an exception
  thrown in a handler of its events, so neither the test nor `ThreadException` would see it. Checked by mutation with Runtime 156: a throw
  in the control's `NavigationCompleted` handler fails both tests ("The control threw on the window's thread"), and a throw in the posted
  `LinkClicked` callback fails the first test within a second, with no dialog. Before, both turned into Inconclusive (reported as skipped).

## Not verified here

- How the wiki, email and CCD pages look and print on a real screen and printer (the smoke test checks behaviour, not layout); the eCW panel
  against an eCW server; the context menu item names on the installed runtime; Ctrl+click and "Open link in new window" in the wiki.
- The find bar (Ctrl+F) on screen, and the Show Images button for an email with web images, in the running program; printing a PDF from
  the Images module as a standard user.
- Hints such as `<link rel="dns-prefetch">` or `preconnect` in a received email are no requests, so `WebResourceRequested` does not see
  them; the browser may still look up or connect to such a host (no request is sent, nor credentials). Not checked.
- A runtime that is really damaged or blocked: the start failure is checked through the seam only (an environment that throws), and the
  message on screen in the running program. The CCD's XML view on screen and on paper.

## Owner questions

1. The installer still downloads the Visual C++ runtime on request (`/PREREQS=download`, or the wizard's choice). This package read "the installer
   downloads nothing" as the WebView2 Runtime only. Should the Visual C++ download go too (then setup only names Microsoft's installer)?
   **Answered 2026-11-02: keep it** (the optional download stays).
2. Pages the program shows (wiki pages, emails) are written as temporary files in the program's temp folder while they are shown (deleted when the
   page changes, the window closes and the program exits). An in-memory alternative (a virtual host) would need the images served the same way.
   Is the temp-file approach acceptable for email content? **Answered 2026-11-02: no; patient email must not sit on disk. Done on
   `next/fu-web-1`, see [Follow-up](#follow-up-owner-answers-to-questions-2-and-7).**
3. The old WebView2 profile folders (`%LOCALAPPDATA%\WindowsFormsWebView2`) are left on workstations. Remove them on upgrade? **Answered
   2026-11-02: leave them.**
4. A received email no longer loads images from the web until the user clicks Show Images (Internet Explorer loaded them at once). Keep this,
   or add a preference to load them always? **Answered 2026-11-02: keep Show Images.**
5. The browser keys (`AreBrowserKeysEnabled`) bring back find in page where Internet Explorer had it, and with it Ctrl+P (the browser's
   print preview) and Ctrl+Plus/Minus (zoom). Keep all of them, or only find? **Answered 2026-11-02: keep all of them.**
6. A CCD that came by email with a stylesheet now shows as XML (the stylesheet is never run in the program). A styled view would need either
   a stylesheet the program ships itself (reviewed, then safe to run) or the received one run in a short-lived child process with a time
   and output limit. Is the XML view enough, or which of the two? **Answered 2026-11-02: the XML view is enough.**
7. `FormCareCreditWeb`, `FormPayConnect2`, `FormPayConnect2iFrame` and the Images module's PDF print show two error dialogs when the WebView2
   Runtime is missing or fails (`Init`'s message, then the window's own `FriendlyException`). **Answered 2026-11-02: show exactly one clear
   message. Done on `next/fu-web-1`, see [Follow-up](#follow-up-owner-answers-to-questions-2-and-7).**

## Follow-up: owner answers to questions 2 and 7

Branch `next/fu-web-1`, cut from `next/int-2` (the integrated branch, where `next/libweb-3` is merged), and its review fix `next/fu-web-2`
(the CCD document in memory). Synthetic data only; no database, table, column or preference changed; no migration; no DLL added.

### Question 2: pages from memory

Wiki pages, emails (received ones included), the CCD view and the other pages of `NavigateToHtml` are no longer written to disk while they are
shown, nor are the images of an email's Show Images, nor the CCD document itself, so a patient's email never sits in the temp folder and a
crash leaves nothing behind.

- **`CodeBase/Controls/WebView2PageStore`** keeps every page and the files of its own folder in memory. A page gets a `file:` address in a
  folder of its own, `%TEMP%\openpms-dental\pages-in-memory\<random>\page.html`, a folder that is never created. `ODWebView2` answers the
  browser's request for anything under `pages-in-memory` from memory in `WebResourceRequested` (with `Cache-Control: no-store`), 404 for a
  name the page does not have, and never lets such a request reach the disk: a file someone put at that path is never read. The page leaves
  memory when the next page replaces it and when the control is disposed.
- **Why a `file:` address answered from memory**, of the three ways to show a page without a file:
  - `NavigateToString` holds at most 2 MB (an email with inline images is often more), and its page has no folder: it cannot load the `file:`
    images the wiki and the email editor name, and an image name of Show Images has nothing to resolve against.
  - A virtual host name (`https://<name>.invalid/`) answered by `WebResourceRequested`: the page becomes an https page, and the browser itself
    refuses every `file:` image of an https page before the program sees the request. The wiki's images (`file:///` addresses from
    `MarkupEdit.TranslateToXhtml`) and the email editor's (`C:\` paths) would all have to be rewritten into virtual addresses (every `img`,
    `background` and style sheet `url()`), and the rules of `next/libweb-3` (own folder, allowed folders, the relative links that open as web
    addresses, the link rules) would all change.
  - A `file:` address answered from memory keeps every rule and behaviour of `next/libweb-3` as it was, because WebView2 raises
    `WebResourceRequested` for `file:` requests, the page's own included (Microsoft's reference; checked by the smoke tests with Runtime 156,
    which see the page and its image answered with 200 by the control). So links, images (the wiki folder, Show Images, the web only when
    allowed), printing, find, the CCD's XML view, the navigation limits and the secure settings are unchanged; only where the bytes come
    from changed.
- `NavigateToHtml(html, isRemoteContentAllowed, listFoldersAllowed, dictFiles)`: `dictFiles` gives the files of the page's own folder by name
  (an email's images, by the name that replaced their `cid:` address); the `folder` parameter is gone, with `WebView2Policy.GetPageFolder`
  and every page file write and delete of `ODWebView2`.
- `EmailPreviewControl`'s Show Images no longer makes a temp folder: `EmailMessages.GetMimeImageBytes` (in place of `SaveMimeImageToFile`)
  gives each image's bytes, from the same source and in the same format (a format without an encoder, such as `.ico`, falls back to PNG, as
  saving it to a file did).
- A received email's own folder was the program's temp folder on `next/libweb-3` (when shown without Show Images), so it could load any file
  there, another email's Show Images images included; it now holds the email's own images only.
- **The CCD document**: `FormEhrSummaryOfCare.DisplayCCD` wrote the CCD (or CCR) of a received email with a stylesheet to
  `%TEMP%\openpms-dental\ccd.xml` (or `ccr.xml`) while `FormEhrSummaryCcdEdit` showed it, and deleted it only after the window closed, because
  the window read the file again for its page, its three reconcile buttons and Show XML. The window now gets the XML text (`doc.InnerXml`,
  what the file held) and keeps it in a field for all of them; `DisplayCCD` writes no file. `DeleteOldCcdFiles` deletes, when the window
  opens, a `ccd.xml`, `ccd.xsl`, `ccr.xml` or `ccr.xsl` an older version left there (it wrote the stylesheet too) when it ended with the window
  open, if more than an hour old (an older version showing a CCD at the same time reads its file again to reconcile); it creates no folder,
  and a file in use stays. The program also empties the folder when it closes. The four names are those the older versions deleted after the
  window, so nothing else is touched.
- Wiki images downloaded from cloud storage (SFTP, Dropbox) go into the shared folder `pages-in-memory\shared` (by a hash of their A to Z path,
  so an image shown again replaces itself; at most 64 MB, the oldest go first), and `MarkupEdit.GetImageFolders(isEmail:false)` allows that
  folder in place of the temp folder. The image is read from the downloaded bytes (`FileAtoZ.GetImage` on the temp file's path downloaded it
  again from cloud storage, by the temp path).
- **Not changed**: email images downloaded from cloud storage are still temp files (`MarkupEdit.TranslateEmailImages`), because sending the
  email attaches them from there (`EmailMessages.FindAndReplaceImageTags` with `AreImagesDownloaded`); these are the office's own email
  images, not a received email. The S/MIME signature of a signed email (`smime.p7s`) is still copied to a temp file to read the signer's
  certificate (`X509Certificate.CreateFromSignedFile` reads files only); it holds the certificate, not the message.
- **Old page files**: once per program session, at the first page shown, `WebView2PageStore.DeleteOldPageFiles` deletes what an older version
  left in `%TEMP%\openpms-dental` when it ended without closing (the program empties that folder when it closes): files named like
  `ABC12320261102093000.html` (only `ODWebView2` named files so), and folders named like `ABC12320261102093000` that hold such a page (Show
  Images), with their files, when the page is more than an hour old. The hour leaves alone a page an older version running at the same time
  (another user session, a test run) has just written; deleting one it shows longer changes nothing on screen.

### Question 7: one message

- `ODWebView2.Init` throws `ODWebView2InitException` (an `ODException`, so every "Init told the user" catch still holds) with `IsUserTold`
  (Init showed its message: always for a missing runtime, once per program session for one that will not start) and `IsClosed` (the window
  closed while its browser started). `ODWebView2.TryShowInitFailure(ex)` is true for such an exception; it shows the start failure's message
  itself when Init held it back (already shown in this session), without the "shows once" line, and nothing for a closed window.
- `WebView2L.TryInit(control, textError, funcSetup)` (OpenPmsDental) runs `Init` and the window's own setup (events, scripts) and shows exactly
  one message when either fails: Init's for the runtime, or `FriendlyException` with `textError` for anything else (the setup failing).
  `FormCareCreditWeb`, `FormPayConnect2`, `FormPayConnect2iFrame` and `ControlImageDisplay.PrintPDF` use it in place of their try/catch.

### Tests (`tests/LibWebTests`)

- `WebView2PageStoreTests` (no browser): a page and its files are found by the escaped `file:` address the browser asks for, in any letter
  case, and nothing else is (another page's file, the folder, `..` or escaped separators, a stream name, outside the root, a share); nothing is
  written; a replaced page is gone; names with a folder are left out; the response is never cached; the shared folder replaces and evicts; a
  link to an in-memory image is a file, a relative name without a file stays a web address; what an in-memory page may load; the old page
  file clean-up (pattern, Show Images folders, the hour); a source check that `ODWebView2`, `EmailPreviewControl` and `EmailMessages` write no
  page or image file any more.
- `HtmlGenerationTests` (no window): `CcdView_KeepsTheDocumentInMemory_AndWritesNoFile` (the CCD window has no file path, its constructor
  takes the XML, it reads and writes no file, its reconcile buttons and Show XML use the text; `DisplayCCD` writes nothing and hands it
  `doc.InnerXml`) and `DeleteOldCcdFiles_OnlyTheFilesAnOlderVersionLeft` (the four names only, in the folder only, the hour, a file in use
  stays, no folder created). The window itself is not shown in a test (it needs the program's translations and layout); its page goes
  through the same `NavigateToHtml` as the smoke test's pages.
- `WebView2InitMessageTests` (seams, no window, no browser): a missing runtime, a runtime that will not start (also for a window opened after
  Init told the session once and a wiki page held it back) and a window closed while starting each show exactly one message, or none; the
  window's own setup never runs without its browser; the four windows go through `WebView2L.TryInit`.
- `WebView2SmokeTests` (with the runtime; Inconclusive without it): the pages come from memory (their address is under `pages-in-memory`,
  no file or folder there, only the shown page in memory, none after dispose); `WikiPageAndEmail_AreShownFromMemory_AndWriteNoTemporaryFile`
  shows a wiki page with an image from a wiki folder and a received email after Show Images with its image in memory, and compares the
  program's temp folder and the temp folder before, while each page is shown, and after (no new file holds either page's text or the email's
  image, no page file is created, also for a moment, watched with a `FileSystemWatcher`), and searches the test's WebView2 profile for them.
  Checked by mutation: an `ODWebView2` that writes each page to the program's temp folder and deletes it at the next page fails this test
  ("A temporary file held a page ... while it was shown") and the source check.

### Not verified here

- Printing (Windows' print dialog), find (Ctrl+F) and the CCD's XML view on screen in the running program: they work on the shown document,
  which is the same as before; only where its bytes come from changed. The CCD window's reconcile buttons and Show XML with a real Direct
  message (they parse the same text the file held).
- Wiki images from cloud storage (no SFTP or Dropbox A to Z folder here): the store and the allowed folder are unit tested, the download
  path is not.
