# Port note: LIB-WEB, WebView2 in place of the Internet Explorer WebBrowser control

Branch `next/libweb-1`, cut from `next/libbase-2` (0c5fa6369). Owner approval of 2026-11-02 (roadmap item 2, library updates for
maintenance). The WebView2 SDK stays at 1.0.2420.47 (`Required dlls`: `Microsoft.Web.WebView2.Core`, `.WinForms`, `.Wpf`, `WebView2Loader.dll`);
no DLL was added, replaced or removed, so the binding redirects, the release allow list, the installer file list and the notices are unchanged.

## Sources and rules

- WebView2's behaviour comes from Microsoft's public WebView2 documentation; no binary was decompiled. The smoke test (`tests/LibWebTests`) checks
  the behaviour the windows rely on against the installed runtime.
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
  - frames load only `about:blank`, new windows, downloads, external protocols and permission requests are refused;
  - the context menu keeps only Copy, Select all, the link and image copy commands and "Open link in new window"
    (`IsContextMenuEnabled=false` removes it);
  - the page file is deleted when the next page replaces it and when the control is disposed (the temp folder is also emptied at close).
  - The control initializes itself on the first `NavigateToHtml`; a missing runtime is reported once by `Init`.
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
| `OpenPmsDental/Forms/FormEtrans270Edit.cs` | the clearinghouse's HTML benefits response | an HTML string, print | **Ported** to `FormWebView.HtmlContent` (scripts off, links to the default browser). |
| `OpenPmsDental/Forms/FormWebView.cs` | vendor pages (PDMP reports, postcard editor), now also HTML strings | | **Fixed**: `HtmlContent` was navigated before `Init` (never shown), Print did nothing, the title event was never subscribed, Refresh repainted instead of reloading. Vendor pages load only http/https; pop-ups go to the default browser through `SystemBrowser.Open`. |
| `OpenPmsDental/OpenPmsDental.csproj` | | COM reference `SHDocVw` (embedded types) | **Removed** (used only by `FormWebBrowser` and an unused `using` in `ControlChart`). |
| `OpenPmsDental/Forms/FormWiki.cs` (+ designer) | generated wiki pages (`MarkupEdit.TranslateToXhtml`) with local images | `Navigating` for `wiki:`, `wikifile:`, `folder:`, `wikifilecloud:`, `foldercloud:` and web links, `NewWindow` (right-click), `StatusText`, print, `CoInternetSetFeatureEnabled` (IE click sound) | **Ported**: `NavigateToHtml`, `LinkClicked` with `FormWiki.ParseLink`, `StatusTextChanged`, `ShowPrintDialog`. The urlmon call is IE-only and removed. |
| `FormWikiAllPages`, `FormWikiDrafts`, `FormWikiHistory`, `FormWikiIncomingLinks`, `FormWikiSearch` | wiki page previews | `AllowNavigation` toggled to ignore links; context menu off | **Ported**: `NavigateToHtml`, no link handler (links do nothing), `IsContextMenuEnabled=false` where IE's menu was off. |
| `OpenPmsDental/Forms/FormWikiEdit.cs` | live preview of the page being edited | DOM `scrollTop` kept across refreshes, body `zoom` | **Ported**: `GetScrollTopAsync`/`SetScrollTop`/`SetBodyZoom` after `NavigationCompleted`. |
| `OpenPmsDental/Forms/FormEmailEdit.cs` | preview of the email being written (generated or raw HTML) | links to the default browser, `DocumentText` read back as `HtmlText` | **Ported**: `NavigateToHtml`, `LinkClicked`; `HtmlText` is the kept HTML string. |
| `OpenPmsDental/Forms/FormEmailTemplateEdit.cs` | preview of a template in the master template | links to the default browser | **Ported**. |
| `OpenPmsDental/User Controls/EmailPreviewControl.cs` | received and sent emails (untrusted HTML); "Show images" writes the MIME images and the page to a temp folder | links to the default browser, `DocumentText` read back | **Ported**: scripts off, `NavigateToHtml(html, folder)` for "Show images", `LinkClicked`; a meta refresh or script in an email can no longer open anything. |
| `OpenPmsDental/User Controls/UserControlEmailTemplate.cs` | template preview | | **Ported**. |
| `OpenPmsDental/Forms/FormEhrEduBrowser.cs` | an education resource's web page (office-configured URL), reached from the EHR tools (`FormEHR`, `FormEhrEduResourcesPat`) | print (records the EHR measure) | **Ported**: http/https only, pop-ups to the default browser, `ShowPrintDialog`. |
| `OpenPmsDental/Forms/FormEhrSummaryCcdEdit.cs` | a CCD file with its XSL stylesheet (IE ran XSLT) | XSLT, print | **Ported**: the program applies the stylesheet (`HtmlPage.TransformXsl`) and shows the HTML with scripts off. Still reachable: Open Dental's stylesheets came from EHR.dll (`EhrSummaryCcds.GetEhrResource` returns "" since the decoupling), but an email attachment that brings its own `.xsl` opens here (`EmailPreviewControl.OpenFile`). |
| `OpenPmsDental/Main Modules/ControlChart.cs` (+ designer) | eCW's medical panel (remote dashboard on the eCW server) | session cookies through wininet `InternetSetCookie` (IE's cookie store) | **Ported**: cookies through WebView2's `CookieManager` for the eCW host, http/https only, pop-ups to the default browser, a label instead of a message box without the runtime. An invalid Medical Panel URL no longer navigates anyway. |
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
- `WebView2PolicyTests`: profile folder, dev tools, host objects, link, frame, context menu and cookie rules.
- `HtmlGenerationTests`: encoding, the CCD transform and its limits, the preference page, the wiki's link kinds.
- `WebView2SmokeTests`: a generated page in an off-screen window: encoded text shows as text, the page's script does not run, a local image
  loads, clicked wiki and web links raise `LinkClicked` and are not followed, a meta refresh and a frame are cancelled. Only local and
  `.invalid` addresses, background networking off. Inconclusive without the runtime or with `LIBWEB_SKIP_WEBVIEW2=1`.

## Not verified here

- How the wiki, email and CCD pages look and print on a real screen and printer (the smoke test checks behaviour, not layout); the eCW panel
  against an eCW server; the context menu item names on the installed runtime; Ctrl+click and "Open link in new window" in the wiki.

## Owner questions

1. The installer still downloads the Visual C++ runtime on request (`/PREREQS=download`, or the wizard's choice). This package read "the installer
   downloads nothing" as the WebView2 Runtime only. Should the Visual C++ download go too (then setup only names Microsoft's installer)?
2. Pages the program shows (wiki pages, emails) are written as temporary files in the program's temp folder while they are shown (deleted when the
   page changes, the window closes and the program exits). An in-memory alternative (a virtual host) would need the images served the same way.
   Is the temp-file approach acceptable for email content?
3. The old WebView2 profile folders (`%LOCALAPPDATA%\WindowsFormsWebView2`) are left on workstations. Remove them on upgrade?
