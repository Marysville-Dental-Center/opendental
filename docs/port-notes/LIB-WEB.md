# Port note: LIB-WEB, WebView2 in place of the Internet Explorer WebBrowser control

Branch `next/libweb-1`, cut from `next/libbase-2` (0c5fa6369); the review fixes are on `next/libweb-2` (own page files and history never
raise `LinkClicked`, what a page may load, find in page, one profile folder for the Images print). Owner approval of 2026-11-02 (roadmap
item 2, library updates for maintenance). The WebView2 SDK stays at 1.0.2420.47 (`Required dlls`: `Microsoft.Web.WebView2.Core`, `.WinForms`, `.Wpf`, `WebView2Loader.dll`);
no DLL was added, replaced or removed, so the binding redirects, the release allow list, the installer file list and the notices are unchanged.

## Sources and rules

- WebView2's behaviour comes from Microsoft's public WebView2 documentation; no binary was decompiled. The smoke test (`tests/LibWebTests`) checks
  the behaviour the windows rely on against the installed runtime. In particular `CoreWebView2.WebResourceRequested` is raised for file, http
  and https addresses (Microsoft's reference; not for virtual host mappings, which is why the pages stay `file:` pages), and
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
- **`ODWebView2.NavigateToHtml(html, folder)`** replaces the IE control's `DocumentText`. The page is written to a new file (UTF-8 with a byte
  order mark) in the program's temp folder, or in `folder`, and that file is shown, so the local images the wiki and emails refer to still load
  (a page from a string cannot load `file:` images). While such a page is shown:
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
  - the page file is deleted when the next page replaces it and when the control is disposed (the temp folder is also emptied at close).
  - The control initializes itself on the first `NavigateToHtml`; a missing runtime is reported once by `Init`.
- What each window allows its pages (`MarkupEdit.GetImageFolders` gives the A to Z `Wiki` or `EmailImages` folder when the A to Z folder is
  local or on a share, and the temp folder that holds images downloaded from cloud storage):

  | Window | Folders | Web content |
  |---|---|---|
  | `FormWiki` and its previews (`FormWikiAllPages`, `FormWikiDrafts`, `FormWikiEdit`, `FormWikiHistory`, `FormWikiIncomingLinks`, `FormWikiSearch`) | wiki images | yes (the office's own pages; the master page may name a web style sheet) |
  | `FormEmailEdit`, `FormEmailTemplateEdit` | email images | yes (the office's own email, as the recipient will see it) |
  | `EmailPreviewControl`, sent email and drafts | email images | yes |
  | `EmailPreviewControl`, received email | its own folder only | **no**, until the user clicks Show Images, which now also appears when the email asked for web content (a remote image tells the sender when and from where the email was read) |
  | `UserControlEmailTemplate` (no caller) | email images | no |
  | `FormEhrSummaryCcdEdit` (the stylesheet can come with an email), `FormWebView.HtmlContent` (a clearinghouse's response), `FormWebBrowserPrefs` | its own folder only | no |

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
- `CodeBase.HtmlPage` builds the pages the program writes itself, with `Encode` for every text from the database, and `TransformXsl` applies an
  XSLT stylesheet (no `document()`, no `xsl:include`/`xsl:import`, no scripts, no external entities, an entity expansion limit).

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
| `OpenPmsDental/User Controls/EmailPreviewControl.cs` | received and sent emails (untrusted HTML); "Show images" writes the MIME images and the page to a temp folder | links to the default browser, `DocumentText` read back; no find (shortcuts off) | **Ported**: scripts off, `NavigateToHtml(html, folder)` for "Show images", `LinkClicked`; a meta refresh or script in an email can no longer open anything. A received email loads nothing from the web and no file outside its folder until Show Images (see above). |
| `OpenPmsDental/User Controls/UserControlEmailTemplate.cs` | template preview (the control has no caller) | find (shortcuts on) | **Ported**: email image folders, nothing from the web, `AreBrowserKeysEnabled`. |
| `OpenPmsDental/Forms/FormEhrEduBrowser.cs` | an education resource's web page (office-configured URL), reached from the EHR tools (`FormEHR`, `FormEhrEduResourcesPat`) | print (records the EHR measure), find (shortcuts on) | **Ported**: http/https only, pop-ups to the default browser, `ShowPrintDialog`, `AreBrowserKeysEnabled`. |
| `OpenPmsDental/Forms/FormEhrSummaryCcdEdit.cs` | a CCD file with its XSL stylesheet (IE ran XSLT) | XSLT, print, find (shortcuts on) | **Ported**: the program applies the stylesheet (`HtmlPage.TransformXsl`) and shows the HTML with scripts off, no file but its own and nothing from the web, `AreBrowserKeysEnabled`. Still reachable: Open Dental's stylesheets came from EHR.dll (`EhrSummaryCcds.GetEhrResource` returns "" since the decoupling), but an email attachment that brings its own `.xsl` opens here (`EmailPreviewControl.OpenFile`). |
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
- `HtmlGenerationTests`: encoding, the CCD transform and its limits, the preference page, the wiki's link kinds.
- `WebView2SmokeTests` (off-screen windows):
  - a generated page: encoded text shows as text, the page's script does not run, a local image loads, clicked wiki and web links raise
    `LinkClicked` and are not followed, a meta refresh and a frame are cancelled; two pages shown back to back without waiting, then
    `GoBack`, raise no `LinkClicked` (with Runtime 156 the first page's own navigation does start, user initiated, after the second page
    took its place, and Back is user initiated too: the case the review found);
  - what a page may load: from html from outside the office, only the image in its folder, never one in an allowed folder, outside the
    folder (also through `../`), `C:\Windows\win.ini`, `http://img.invalid/` or `file://unc.invalid/` (each request is seen and refused
    by the control; the share is tried only after a local file was seen refused, so nothing reaches `unc.invalid`), and
    `RemoteContentBlocked` once; from the office's own page also the allowed folder and the web image (which the test answers itself).
  Only local and `.invalid` addresses, background networking off. Inconclusive without the runtime or with `LIBWEB_SKIP_WEBVIEW2=1`.

## Not verified here

- How the wiki, email and CCD pages look and print on a real screen and printer (the smoke test checks behaviour, not layout); the eCW panel
  against an eCW server; the context menu item names on the installed runtime; Ctrl+click and "Open link in new window" in the wiki.
- The find bar (Ctrl+F) on screen, and the Show Images button for an email with web images, in the running program; printing a PDF from
  the Images module as a standard user.
- Hints such as `<link rel="dns-prefetch">` or `preconnect` in a received email are no requests, so `WebResourceRequested` does not see
  them; the browser may still look up or connect to such a host (no request is sent, nor credentials). Not checked.

## Owner questions

1. The installer still downloads the Visual C++ runtime on request (`/PREREQS=download`, or the wizard's choice). This package read "the installer
   downloads nothing" as the WebView2 Runtime only. Should the Visual C++ download go too (then setup only names Microsoft's installer)?
2. Pages the program shows (wiki pages, emails) are written as temporary files in the program's temp folder while they are shown (deleted when the
   page changes, the window closes and the program exits). An in-memory alternative (a virtual host) would need the images served the same way.
   Is the temp-file approach acceptable for email content?
3. The old WebView2 profile folders (`%LOCALAPPDATA%\WindowsFormsWebView2`) are left on workstations. Remove them on upgrade?
4. A received email no longer loads images from the web until the user clicks Show Images (Internet Explorer loaded them at once). Keep this,
   or add a preference to load them always?
5. The browser keys (`AreBrowserKeysEnabled`) bring back find in page where Internet Explorer had it, and with it Ctrl+P (the browser's
   print preview) and Ctrl+Plus/Minus (zoom). Keep all of them, or only find?
