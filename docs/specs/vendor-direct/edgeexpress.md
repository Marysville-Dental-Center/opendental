# EdgeExpress hosted payment page: direct-to-vendor spec

Status: draft, 2026-10-18, fact-checked the same day (see "Fact-check log" at the end). Baseline: `main` at `6855026f5`. Vendor: Global Payments Integrated (OpenEdge), product EdgeExpress.

## Summary

- **The API calls are already direct.** The desktop app posts XML straight to `ee.paygateway.com` using the office's own merchant credentials (X-Web ID, Terminal ID, Auth Key). Open Dental's HQ never brokered credentials, tokens or API calls for this integration.
- **What HQ provided was a web page.** The default return URL of the hosted payment page pointed at a page on `patientviewer.com`. Wave 1 (`b5ca25960`) replaced it with a reserved `.invalid` address that the in-app browser intercepts and never loads.
- **What's left** is to make that local return handling dependable and verified, and to clean up the credential handling:
  - move the pay page from the IE (MSHTML) control to WebView2
  - add an optional loopback listener
  - make the return URL and the sandbox switch per-office settings
  - encrypt the Auth Key
  - reconcile payments left pending locally (the eConnector used to do this)
  - remove an `opendental.com` link and Open Dental's compiled-in X-Web test credentials
  - fix an existing bug where the result query can be sent with a different clinic's credentials than the session was created with
- **Documentation is partner-only.** Global Payments makes EdgeExpress available only to its software partners, and the detailed API spec is confidential. This spec is built from this repository's GPL code (which ran against the live API in Open Dental 24.3), public third-party material, and the questions the office must put to OpenEdge.
- **Feasibility: partner-only. Size: M.** Technically the integration already talks to the vendor directly; what is gated is the current spec, sandbox credentials and OpenEdge's written OK for this software (section 5).

## 1. What the integration does, and what HQ provided

### 1.1 What the office uses it for

EdgeExpress is a card processor integration with two paths:

| Path | What it does | Code |
|---|---|---|
| **RCM (card present)** | Sends sale, return, debit, void, force and alias requests to the vendor's locally installed RCM client. RCM drives a physical terminal. The URL is `https://localsystem.paygateway.com:21113/RcmService.svc/Initialize`, a vendor host name that resolves to 127.0.0.1. | `EdgeExpress.RCM` in `OpenDentBusiness/WebBridges/EdgeExpress.cs` (lines 139–273) |
| **CNP (card not present)** | Keyed card entry on the vendor's hosted pay page, so the card number never touches our app. Also covers charges, voids, returns, forces and alias (token) update or delete without a page. | `EdgeExpress.CNP` in the same file (lines 278–890) |

The CNP path is used from these places:

- `FormPayment.MakeEdgeExpressTransactionCNP`: the CreditSale and CreditAuth cases open the pay page. Return, Void and Force are page-less.
- `FormCreditCardManage` (around line 295): adds a card on file through a CreditAuth pay page.
- `FormCreditCardEdit`: `CNP.UpdateAlias` and `CNP.DeleteAlias`.
- `RecurringCharges.SendEdgeExpress`: `CNP.ProcessPaymentDirect` charges a stored alias.
- `FormEdgeExpressSetup`: the per-clinic credentials and options.

The RCM path is used from `FormPayment` (`RCM.SendEdgeExpressRequest`), `FormCreditCardManage` (`RCM.CreateAlias`) and `FormEdgeExpressTrans` (`RCM.IsRCMRunning`).

The program link is off by default. The EdgeExpress UI is gated on the program being enabled (`Programs.IsEnabled(ProgramName.EdgeExpress)` in `FormCreditCardManage` and `FormCreditCardEdit`; `Program.Enabled` plus non-blank clinic credentials in `FormPayment` and `FormClaimPayEdit`), so ground rules 6 and 7 already hold for the buttons.

### 1.2 What Open Dental's HQ provided

| Item | Where | Status on `main` | This spec |
|---|---|---|---|
| Return page for the hosted pay page: `https://www.patientviewer.com/PortalPayDone.aspx`, hard-coded as the default `RETURNURL` | `EdgeExpress.CNP.SendEdgeExpressRequest` | Replaced in wave 1 by `CNP.ReturnUrlDefault` = `https://edgeexpress-return.invalid/PayDone`. `FormWebBrowser.UrlCloseOn` cancels the navigation and closes the window, and the guard script no longer allow-lists this file. | Keep the idea. Move it to WebView2, make the URL configurable, add a loopback option, and verify it in the sandbox (section 3). |
| Polling of pending hosted-pay transactions. `XWebResponses.GetPendingHostedPay` and `GetExpiredEdgeExpressResponses` have **no callers in this repository**; presumably Open Dental's eConnector service (not in this repo) called them. | `OpenDentBusiness/Data Interface/XWebResponses.cs` lines 83 and 96 | Nothing reconciles a transaction the user abandoned mid-page | Add local reconciliation (section 3.7) |
| Patient-portal payments (`EdgeExpressPaymentPortal*` card sources, `UploadPatientPortalXWebResponses`) | eServices | `WebServiceMainHQNoHq.UploadPatientPortalXWebResponses` returns "not available" | Out of scope (eServices are removed). The stub stays. |
| "EdgeExpress website" link. Its label shows a Global Payments address, but the click opens `https://opendental.com/resources/redirects/redirectopenedge.html` | `FormEdgeExpressSetup.linkLabel1_LinkClicked` | Still present (covered by the wildcard partner-redirect allow-list entry in `tools/Test-NoHqHosts.ps1`) | Remove it. Ground rule 7: setup screens only describe the integration. |

**Not provided by HQ:**
- the merchant credentials, which the office types in
- the API endpoints, which are vendor hosts
- any OAuth client
- any `Bridges.dll` type

The inventory (`hq-vendor-table.md`) lists no HQ web method for EdgeExpress.

## 2. The vendor's API

### 2.1 Documentation status

- **Partner-only.** The 2017 launch announcement says EdgeExpress Cloud is available only to Global Payments software partners ([press release](https://investors.globalpayments.com/news-events/press-releases/detail/130/global-payments-launches-edgeexpress-cloud-a-new-platform)).
- **No public reference portal.** The product page lists hosted payment forms, eCommerce and a cloud-based option among the environments it supports. It links no API reference; for sample code or a test environment it points developers to a contact form or `developerservices@openedgepay.com` ([OpenEdge EdgeExpress](https://openedgepayments.com/edgeexpress/)). Global Payments runs a general developer portal (developer.globalpayments.com), but a search on 2026-10-18 found no EdgeExpress, HostPay or `paygateway.com` material on it.
- **Copies of vendor guides on third-party sites were not used.** Copies of the CNP API specification carry an OpenEdge notice marking the content confidential and forbidding redistribution without written consent. Older EdgeExpress API guides (for example a 2019 v2.6.2 PDF) are also findable on integrators' download servers; they are not published by the vendor, so they were not used either. The office has to get the current spec through its OpenEdge relationship (section 5).
- **Sources used here, in order of trust:**
  1. This repository's own code. It is GPL source of Open Dental 24.3.63, which shipped against the live API. Field names and URLs below come from it.
  2. A public third-party tutorial on the pay page ([Tudip](https://tudip.com/?p=52743)).
  3. A public response-code table in an integrator's help file ([iCare help PDF](https://icarenextgen.icaresoftware.com/EdgExpHelp.pdf)). For every code that `XWebResponseCodes` in `OpenDentBusiness/TableTypes/XWebResponse.cs` defines, the meanings match. The table also lists codes the enum lacks, notably 006 (session timed out) and 103 (cancelled by the user, spec version 3.7 and later); see 2.4.

  Everything below must be re-checked against the current spec once the office has it.

### 2.2 Base URLs

| Purpose | Production | Sandbox | Source |
|---|---|---|---|
| HostPay: create a pay-page session | `https://ee.paygateway.com/HostPayService/v1/hostpay/transactions/` | `https://ee.test.paygateway.com/HostPayService/v1/hostpay/transactions` | `EdgeExpress.CNP._edgeExpressHostPayUrl`. Tudip's walkthrough creates the session by posting to a `.../hostpay/paypage/` path instead; which path is current is an open question for OpenEdge. |
| DirectPay: page-less requests and result query | `https://ee.paygateway.com/HostPayService/v1/directpay/express` | `https://ee.test.paygateway.com/HostPayService/v1/directpay/express` | `EdgeExpress.CNP._edgeExpressDirectPayUrl`. Tudip shows the same sandbox path. |
| Pay page shown to the user | Returned in `PAYPAGEURL` | Returned in `PAYPAGEURL` | Tudip shows `.../hostpay/paypage/{sessionToken}` |
| RCM (card present) | `https://localsystem.paygateway.com:21113/RcmService.svc/Initialize` (loopback) | same | `EdgeExpress.RCM` |

Today the sandbox is chosen by `ODBuild.IsDebug() || XWebs.UseXWebTestGateway`, where the second is read from a `UseXWebTestGateway` node in the local connection config (`CentralConnections.cs`, `FreeDentalConfig.xml`). The same flag also switches the X-Web (X-Charge) gateway in `XWebs.XWebInputAbs._gatewayUrl`. `Introspection` can override either URL through the local `IntrospectionItems` preference. Two inconsistencies to fix: the production HostPay URL has a trailing slash and the sandbox one doesn't, and `SendEdgeExpressRequest` compares URLs to decide whether to add UI parameters.

### 2.3 Authentication

- There is **no OAuth, no token exchange and no refresh**. Every request is an HTTPS POST of an XML `REQUEST` document (`Content-Type: application/xml`) that carries the merchant credentials in the body (`WriteEdgeExpressBaseRequest`):
  - `XWEBID`: 12 digits
  - `XWEBTERMINALID`: 8 digits
  - `XWEBAUTHKEY`: 32 alphanumeric characters
- The formats are enforced by `ProgramProperties.GetXWebCreds` and `FormEdgeExpressSetup.ValidateXWeb`.
- OpenEdge issues these per merchant account (per clinic here). Sandbox and production credentials are different. Using one set against the other environment gives response code 806, 807 or 808 (mode mismatch).
- Open question: Tudip says an `sw-apikey` header is required on the query call, without saying where its value comes from. This repo never sends one, and Open Dental's shipped integration worked without it.
- The RCM path sends the same three credentials inside the query string of a GET to the local RCM service (`?xl2Parameters=<xml>`). That traffic stays on the machine, but RCM's own logs may record the URL; this is the vendor client's design and is not changed here.

### 2.4 Endpoints and fields we use

| Call | URL | `TRANSACTIONTYPE` | Request fields (besides credentials) | Response fields read |
|---|---|---|---|---|
| Sale via pay page | HostPay | `CREDITSALE` | `ORDERID`, `AMOUNT`, `CUSTOMERNAME`, `CREATEALIAS` (only when true), `ALIAS` (only when given), `ALLOWDUPLICATES`, `INVOICENO`, `CLERK` (only for a logged-in non-eService user; truncated to 20 characters because a code comment says testing showed longer values fail); plus `HOSTPAYSETTING` (below) | `SESSIONTOKEN`, `PAYPAGEURL` |
| Card on file or pre-auth via pay page | HostPay | `CREDITAUTH` | as above (amount can be 0) | `SESSIONTOKEN`, `PAYPAGEURL` |
| Result of a pay-page session | DirectPay | `QUERYPAYMENT` | `ORDERID` | `RESULT` document → `EdgeExpressResponse` (`RESPONSECODE`, `RESPONSEDESCRIPTION`, `TRANSACTIONID`, `APPROVALCODE`, `APPROVEDAMOUNT`, `MASKEDCARDNUMBER`, `CARDBRAND`, `CARDTYPE`, `EXPMONTH`, `EXPYEAR`, `ALIAS`, `BATCHNO`, `RECEIPTID`, …) |
| Charge a stored alias | DirectPay | `CREDITSALE` | `ORDERID`, `AMOUNT`, `ALIAS`, `ALLOWDUPLICATES`, `INVOICENO`, `CLERK` | `RESULT` |
| Void, return, force | DirectPay | `CREDITVOID`, `CREDITRETURN`, `CREDITONLINECAPTURE` | `TRANSACTIONID`, `AMOUNT`, `INVOICENO`; plus `CLERK` for return and force (not void); `ALIAS` when given for void and return; `CREATEALIAS` when requested for return. A 814 here throws instead of being treated as pending. | `RESULT` |
| Alias update or delete | DirectPay | `ALIASUPDATE`, `ALIASDELETE` | `ALIAS`, plus `EXPDATE` (MMyy) for update | `RESPONSECODE`, `RESPONSEDESCRIPTION` |

`HOSTPAYSETTING`, from `AddUIParams`:
- `POSDEVICE/TYPE=KEYED`
- `RETURNOPTION/RETURNURL` and `RETURNOPTION/RETURNTARGET=_self`
- `DISABLEFRAMING=false`
- `CUSTOMIZATION/PAGE` hides the billing first, middle and last name, company and title fields, and sets the order-info label to "Payment Information"

Response codes that matter (from `XWebResponseCodes`):

| Code | Meaning |
|---|---|
| 000 | approval |
| 001 | decline |
| 005 | alias success |
| 006 | session timed out. **Not in `XWebResponseCodes`**; it maps to `Undefined`, which `ProcessTransaction` treats as still pending. Tudip names it as the expiry signal for the query call. |
| 032 | zero-dollar auth approval |
| 100 | OTK success |
| 101 | expired without approval |
| 102 | pending |
| 103 | cancelled by the user (spec version 3.7 and later, per the iCare table). **Not in `XWebResponseCodes`**; same `Undefined` → pending handling as 006. |
| 801 | request too large |
| 805 | authentication error |
| 806 / 807 / 808 | environment mismatch |
| 813 | duplicate |
| 814 | invalid reference (the code treats this as "still pending" for `QUERYPAYMENT`) |

### 2.5 Return-URL requirements: what is known

| Question | What we know | Source |
|---|---|---|
| How is the return URL set? | Per request, in `HOSTPAYSETTING/RETURNOPTION/RETURNURL`. No portal-side registration appears anywhere in the code. | `AddUIParams`; Tudip |
| What triggers the return? | After the card is submitted, the pay page sends the browser to `RETURNURL`. `RETURNTARGET=_self` keeps that navigation in the page's own browsing context instead of the top window (from the code's own comment). | `AddUIParams` |
| Does the app need anything from the return? | **No.** The code never reads the return request. The result always comes from `QUERYPAYMENT` with the `ORDERID` and the merchant credentials (`CNP.GetOtkStatus` → `ProcessTransaction`). Tudip recommends the same pattern. | `ProcessTransaction`; Tudip |
| Must it be HTTPS? Is a loopback address or an unresolvable host accepted? Is there an allow-list on the merchant account? | **Not publicly documented.** Open Dental always used a public HTTPS URL. Wave 1's `.invalid` value is valid syntax but has **not yet been run against the gateway**. | Open question, settled by sandbox tests T1–T3 |
| Is the return a GET or a POST, and are result fields appended? | **Not publicly documented.** The design must not depend on it. | Sandbox test T3 |
| How long does a session live? | Tudip says the page URL expires after 5 minutes and that the query then answers 006. The code assumes 10 minutes (`_formTimeout`, whose comment still describes eConnector polling) plus 5 minutes of grace before it calls a session expired. | Sandbox test T5 |
| Can the page be framed? | `DISABLEFRAMING=false` allows an iframe. We load `PAYPAGEURL` as the top-level document, so framing isn't needed. | `AddUIParams` |

The return URL is therefore only a **"the user is done" signal**. Any value works as long as (a) the gateway accepts it in the request and (b) our app notices the navigation. Nothing ever needs to be served there.

### 2.6 Limits

- **Request size:** 2048 bytes. `XWebs.XWebInputAbs.UploadData` throws at 2048 bytes or more, and the gateway answers 801. The pay-page request with its customization block is the largest request. Long patient names push it toward the limit, so it needs a test (section 6).
- **Rate limits:** none published.
- **Duplicates:** the duplicate-check window is a gateway setting. A repeat inside the window gives 813 unless `ALLOWDUPLICATES` is true.
- **Encoding:** requests are encoded as ASCII, so non-ASCII letters in names become `?`. This is existing behaviour; `CUSTOMERNAME` is only informational.

### 2.7 Terms that matter

- **Partner program and certification.** Access is through Global Payments' software partner program. Open Dental was the certified integrator. A renamed fork using the same request format may need its own partner registration or certification before production use. The merchant agreement may also restrict which software can submit transactions. **This is the main open question for the office** (section 5).
- **PCI DSS.** The card number is typed into the vendor's page, so our app and database never see it. We store only the alias, the masked number and the expiry date. The workstation where staff key in cards is still part of the office's card environment. The office confirms its self-assessment type with its acquirer ([PCI SSC document library](https://www.pcisecuritystandards.org/document_library/)).
- **HIPAA / BAA.** Each request sends:
  - the patient's name (`CUSTOMERNAME`, and for RCM `CUSTOMERFIRSTNAME` and `CUSTOMERLASTNAME`)
  - `PAT{PatNum}` (`INVOICENO`, and for RCM `RECEIPTLINEITEMS`)
  - the clerk's user name

  Payment processing by financial institutions is outside HIPAA's privacy and transaction rules ([42 U.S.C. § 1320d-8](https://www.law.cornell.edu/uscode/text/42/1320d-8)), so a BAA isn't normally expected for card processing. The office's privacy officer should record that decision. If OpenEdge provides services beyond payment processing, the question comes back. No clinical data is sent.
- **App review:** none published, apart from the certification question above.

## 3. Desktop design

### 3.1 Flow (CNP pay page)

1. The user picks CreditSale or CreditAuth in `FormPayment`, or "add card" in `FormCreditCardManage`.
2. `EdgeExpress.CNP.GetUrlForPaymentPage` / `GetUrlForCreditCardAlias` read the clinic's credentials, build the XML and include the clinic's **return URL** (3.2). It is POSTed to HostPay and the `SESSIONTOKEN` and `PAYPAGEURL` are stored in a new `xwebresponse` row (status `EdgeExpressPending`, `HpfExpiration`). This is unchanged.
3. The app shows the pay page (3.3 or 3.4).
4. One of three things ends the wait:
   - the return navigation is seen
   - the user closes the window
   - the session lifetime runs out
5. The app runs `QUERYPAYMENT` for the `ORDERID` (3.7). It uses the result only if it is final. The return navigation itself is never trusted, and nothing it carries is read.
6. Approved: payment, paysplit and card-on-file rows are written as today (`ProcessTransaction`). Still pending: the row stays pending and local reconciliation takes over (3.7).

### 3.2 Return URL: options and decision

| Option | How it works | Network | Verdict |
|---|---|---|---|
| **A. In-app interception of a sentinel URL** (wave 1) | `RETURNURL=https://edgeexpress-return.invalid/PayDone`. The embedded browser cancels any navigation whose URL starts with it and closes the window. | None. `.invalid` is reserved and never resolves ([RFC 6761 §6.4](https://www.rfc-editor.org/rfc/rfc6761#section-6.4), [RFC 2606](https://www.rfc-editor.org/rfc/rfc2606)), and the request is cancelled before it is sent. | **Default.** Simplest option with nothing to serve. It only depends on the gateway accepting the value (test T1). |
| **B. Loopback listener** | `RETURNURL=http://127.0.0.1:{port}/edgeexpress/return/{nonce}`. A one-shot listener on the loopback interface answers with a static "you can close this window" page. | Loopback only. Plain HTTP is acceptable because the traffic never leaves the machine ([RFC 8252 §7.3 and §8.3](https://www.rfc-editor.org/rfc/rfc8252#section-7.3)). 127.0.0.1 is a potentially trustworthy origin for browsers ([W3C Secure Contexts](https://www.w3.org/TR/secure-contexts/#is-origin-trustworthy)). | **Optional mode.** Needed when the page opens in the system browser (WebView2 missing, or the office prefers it). Also the fallback if the gateway rejects A but accepts a loopback URL. In the embedded browser, mode B also intercepts, so the listener is a backstop there. |
| C. Office-owned HTTPS page | The office sets `RETURNURL` to a page on its own website. The embedded browser still intercepts, so the page is never loaded from the app. | None from the app | **Last resort**, only if the gateway requires a public, resolvable HTTPS host (tests T1–T2). Configuration only, no code. Not an Open Dental host. |
| D. Custom URI scheme (`openpms-pay:`) | Registered protocol handler | None | Rejected. Hosted pages usually require http(s), and it needs registry writes. |
| E. Open Dental's page | — | `patientviewer.com` | Removed (Phase 1). |

**Decision:**
- Ship A as the default and B as a per-clinic mode. Allow C by letting the office edit the URL.
- Store the URL and the mode in program properties (3.5). A blank URL means the built-in default.
- In every mode the window also closes on a manual Close. The query in step 5 decides the outcome either way.

### 3.3 Embedded pay-page window (modes A and B)

Add a new `FormEdgeExpressPayPage` on `CodeBase.Controls.ODWebView2` (WebView2, Chromium). It replaces `FormWebBrowser` (the IE11 MSHTML control) for this flow. The IE11 desktop app is retired, and the engine only receives security fixes ([Microsoft lifecycle FAQ](https://learn.microsoft.com/en-us/lifecycle/faq/internet-explorer-microsoft-edge)), so a vendor page could stop supporting it at any time.

- **Interception:**
  - Handle `CoreWebView2.NavigationStarting`, plus `FrameNavigationStarting` in case the page ever runs in a frame ([navigation events](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/navigation-events)).
  - When the URI starts with the clinic's return URL (case-insensitive, compared as parsed absolute URIs), set `Cancel=true` and close with `DialogResult.OK`. Same behaviour as `FormWebBrowser.UrlCloseOn` today.
  - Because the navigation is cancelled before any request goes out, a POST body (if the gateway sends one) never leaves the browser.
- **Navigation allow-list.** Allow only:
  - the host of `PAYPAGEURL` (`ee.paygateway.com` or `ee.test.paygateway.com`)
  - `about:blank`
  - the return prefix

  Cancel anything else, and open it in the system browser only if the user confirms. `NewWindowRequested` is handled and cancelled. The window is never a general browser.
- **Privacy:**
  - Use an InPrivate profile, so no cookies or history persist in the shared `WindowsFormsWebView2` folder. The referenced SDK, 1.0.2420.47, has it: `CoreWebView2ControllerOptions.IsInPrivateModeEnabled`, also exposed on the WinForms control as `CreationProperties.IsInPrivateModeEnabled` ([API reference](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.winforms.corewebview2creationproperties.isinprivatemodeenabled)). `ODWebView2.Init` today calls `EnsureCoreWebView2Async(environment)` with no controller options, so add an `Init` overload that passes options created by `environment.CreateCoreWebView2ControllerOptions()`.
  - Set `IsGeneralAutofillEnabled=false` and `IsPasswordAutosaveEnabled=false` so keyed card numbers are never offered back to the next user.
  - Turn dev tools off (`ODWebView2.CoreWebView2_InitializationCompleted` already does, along with browser accelerator keys).
  - Hide the Refresh, Back and Forward toolbar buttons.
- **Test gateway banner:** when the sandbox is selected, the title bar says so.
- **Fallback:** if the WebView2 runtime is missing, `ODWebView2.Init` throws. Fall back to `FormWebBrowser` with `UrlCloseOn` (already implemented), or to mode B with the system browser if the office chose it.

### 3.4 Loopback listener (mode B)

Add `CodeBase/LoopbackReturnListener.cs`, a generic one-shot listener that the other vendor-direct specs (Google, QuickBooks Online, Microsoft 365) can reuse.

- **Binding:**
  - Use `System.Net.Sockets.TcpListener` on `IPAddress.Loopback` with port 0, so the OS picks a free ephemeral port. Try `IPAddress.IPv6Loopback` if IPv4 binding fails.
  - Don't use `HttpListener`. It goes through http.sys, which may need a URL reservation (admin) for some prefixes. A raw socket on loopback needs no rights, and binding to loopback alone should not raise a Windows Firewall prompt (verify in T9).
- **Path:**
  - `/edgeexpress/return/{nonce}`, where the nonce is 128 random bits (base64url) generated per session.
  - Any request without that exact path gets 404 and doesn't end the wait. This stops a stray local process or web page from ending the wait early. Nothing would be gained by doing so, since the result comes from the query anyway.
- **Handling:**
  - Read the request line and headers. Read at most 16 KB of body, then discard it unread. Nothing from the request is ever logged or stored.
  - Answer `200 text/html` with a fixed page: "Payment step finished. You can close this tab and return to the program." It has no external assets, no scripts and no vendor or project branding.
  - Close the socket, then signal the waiting form.
- **Lifetime:** start just before the session is created (the port has to go into `RETURNURL`). Stop when the return arrives, when the user cancels, or after session lifetime plus 1 minute.
- **System-browser mode:**
  - Open `PAYPAGEURL` with `Process.Start` in the default browser.
  - Show a small modal: "Enter the card in your browser. This window closes when the card processor finishes." It has **I'm finished** and **Cancel** buttons.
  - Either way, step 5 (query) follows.

### 3.5 Credentials and settings storage

All settings are per clinic, in `programproperty` rows of the EdgeExpress program (ClinicNum 0 means practice level / no clinics):

| PropertyDesc | Value | Notes |
|---|---|---|
| `EdgeExpressXWebID` | 12 digits | Existing. Not secret on its own, so stored as plain text. |
| `EdgeExpressTerminalID` | 8 digits | Existing. Plain text. |
| `EdgeExpressAuthKey` | `osd1$…` | Existing row. **Change:** write `CDT.Class1.TryEncrypt(key)`; read it with `TryDecrypt` in `GetXWebCreds`. Open Dental stores this key as plain text (see the comment in `FormEdgeExpressSetup.FillFields`), and `TryDecrypt` returns non-`osd1$` input unchanged, so migrated values keep working. Encryption is deterministic, so `FormEdgeExpressSetup.SyncWithHQ`'s value comparisons still work (see `CDT/README.md`). "HQ" in that method means the practice-level (ClinicNum 0) credentials, not Open Dental's HQ. The other readers of this row (`FormPayment`, `FormClaimPayEdit`, `FormCreditCardManage`) only test it for non-blank, so they are unaffected. |
| `EdgeExpressPaymentType`, `PromptSignature`, `PrintReceipt`, `ForceRecurringCharge`, `PreventSavingNewCC`, `IsOnlinePaymentsEnabled` | existing | Unchanged |
| `EdgeExpressReturnUrl` | absolute URL or blank | **New**, inserted at runtime by the setup form's existing `UpdateProperty`, which creates missing rows. Blank means `CNP.ReturnUrlDefault`. |
| `EdgeExpressReturnMode` | `Embedded` (default) or `Loopback` | **New**. In `Loopback` mode the URL is generated per session and `EdgeExpressReturnUrl` is ignored. |
| `EdgeExpressUseTestGateway` | `0`/`1` | **New**, practice level (ClinicNum 0). Selects the sandbox base URLs in Release builds. The existing `ODBuild.IsDebug()` and `UseXWebTestGateway` triggers stay. |

Notes:
- Readers treat a missing row as the default.
- No schema change and no `ConvertDatabases` step.
- `osd1$` is obfuscation at rest, not strong security (`CDT/README.md`, "Threat model"). The real controls are MySQL grants and access to the server. The Auth Key can be revoked and re-issued by OpenEdge.
- The key is never logged. `GetRequestLogText` already masks half of it. `XWebs.OnInputEvent` raises the full request XML (including `XWEBAUTHKEY`), so mask it there too; nothing in the repo subscribes today.

### 3.6 Refresh handling

There are no tokens to refresh. Instead:

- **Expired session.** When `QUERYPAYMENT` reports 101, or the session lifetime passes, the pay window closes with "The card entry page expired. No charge was made." The user can start again, which creates a new `ORDERID` and session.
- **Session timed out or cancelled (006 / 103).** Treat these as final ("No charge was made") once the sandbox confirms they are returned (T5). Today they map to `Undefined` and the row sits pending until `HpfExpiration` plus 5 minutes.
- **Credential rotation.** On 805 the message says: "The card processor rejected the X-Web ID, Terminal ID or Auth Key for this clinic. Check them in Setup > Program Links > EdgeExpress." If a stored `osd1$` value can't be decrypted (corrupted or edited by hand; Open Dental itself never writes `osd1$`), the setup form shows a blank Auth Key so it gets re-entered. For that the form must use `IsEncrypted` plus `Decrypt`, because `TryDecrypt` would hand back the raw `osd1$…` text.

### 3.7 Result, reconciliation and duplicate safety

- **After the window closes**, whether by return, Close or timeout:
  - Call `ProcessTransaction`. **Fix first:** it must query with the clinic stored on the row (`xWebResponseOld.ClinicNum`, set by `SendEdgeExpressRequest`). Today it uses the patient's clinic (`existingPayment==null`) or the payment's clinic. Sessions are created with other clinics in two places: `FormCreditCardManage` passes `doUseCurrentClinicNum:true`, and the `FormPayment` CreditAuth case uses the patient's clinic but queries with `_payment.ClinicNum`. When clinics have different credentials, the query goes out under the wrong merchant, gets 814, and the transaction looks pending until it expires, even if the card was charged.
  - If the result is still pending (102, or 814 treated as pending), re-query up to 3 more times, 3 seconds apart, with a "Checking with the card processor…" progress bar. This catches a card submitted just before the user closed the window.
- **Still pending.** Show the payment as "not completed" and leave the `xwebresponse` row in `EdgeExpressPending`. This replaces the eConnector's role:
  - A **local reconciliation thread** in `FormOpenDentalThreads` runs only while EdgeExpress is enabled and pending rows exist, every 2 minutes. It needs a **new query**, `XWebResponses.GetPendingEdgeExpress(DateTime notBefore)`: `TransactionStatus=EdgeExpressPending`, `TransactionType` in (`CreditSale`, `CreditAuth`) and `HpfExpiration` later than now minus 30 minutes. The existing helpers don't fit. `GetPendingHostedPay` filters `PaymentNum=0`, which skips every desktop sale (`FormPayment` passes `paymentNum: _payment.PayNum`), and it also returns X-Web `HpfPending` rows. `GetExpiredEdgeExpressResponses` also returns finished `EdgeExpressExpired` and `EdgeExpressMonitoringError` rows and has no age bound.
  - A late approval on a sale may point at a `PaymentNum` whose payment the user has since cancelled and deleted in `FormPayment`. The alert below has to cover that case.
  - **Claim before processing:** run `UPDATE xwebresponse SET DateTUpdate=NOW() WHERE XWebResponseNum=@n AND DateTUpdate=@old`. If 0 rows change, another workstation has the row. This needs no schema change and no new enum value.
  - **An approval found late is not posted automatically.** Call `ProcessTransaction` with `doCreatePayment:false`, which marks the row `EdgeExpressCompletePaymentApproved` (or `...AliasCreated`) and inserts the card-on-file row if an alias came back. Then raise a local `alertitem` ("Card payment of $X for patient Y was approved after the payment window closed. Attach or void it."). Money movement stays a human decision.
  - **No result past the window:** mark `EdgeExpressExpired`, same as today.
- **Duplicates:** `ALLOWDUPLICATES` stays false for interactive sales. 813 shows a clear message.

### 3.8 Errors and offline behaviour

| Situation | Behaviour |
|---|---|
| No internet when starting | HostPay POST fails. "Could not reach the card processor. No charge was made." No row is written, since `FinishEdgeExpressUrlRequest` isn't reached. |
| Connection lost while the page is open | The page can't submit. On close, the query fails, the row stays pending, and the user sees "Payment status unknown. The program will keep checking." Reconciliation picks it up. |
| Gateway 820 / 818 / 819 (temporary) | "The card processor is temporarily unavailable. Try again in a minute." No automatic retry of charges. |
| 805 / 806 / 807 / 808 | Credential or environment message pointing to setup. |
| 801 (too large) | Caught before sending (`UploadData`). The fix is to drop the optional `CUSTOMIZATION` block, then truncate `CUSTOMERNAME`. |
| WebView2 runtime missing | Fall back to the IE control (mode A) or the system browser (mode B). `ODWebView2.Init` already offers Microsoft's runtime download. |
| Loopback bind fails | Fall back to embedded mode A and log a warning. |

Core features never depend on any of this (ground rule 5): charting, scheduling, billing, claims and imaging work with EdgeExpress disabled or offline.

### 3.9 Hosts contacted

| Host | Contacted |
|---|---|
| `ee.paygateway.com`, `ee.test.paygateway.com` | Yes (vendor) |
| `localsystem.paygateway.com` | Yes, only by the RCM path; resolves to 127.0.0.1 (vendor) |
| `127.0.0.1` / `[::1]` | Yes, mode B only |
| `edgeexpress-return.invalid` | Never contacted |
| Open Dental hosts and HQ | None |

`tools/Test-NoHqHosts.ps1` already guards the code. Removing `linkLabel1` drops one more wildcard-allowed reference.

## 4. Code changes

| File | Members | Change |
|---|---|---|
| `OpenDentBusiness/WebBridges/EdgeExpress.cs` | `CNP.ReturnUrlDefault` | Keep. |
| same | new `CNP.GetReturnUrl(long clinicNum)` | Reads `EdgeExpressReturnUrl` and falls back to the default. Validates that the value is an absolute http(s) URI. |
| same | `CNP.SendEdgeExpressRequest`, `GetUrlForPaymentPage`, `GetUrlForCreditCardAlias` | Take the return URL from the caller (mode B passes the loopback URL) or from `GetReturnUrl`. Stop comparing URL strings to decide UI parameters: pass an explicit `isHostPay` flag. |
| same | `CNP.AddUIParams` | Remove the redundant debug branch. |
| same | `CNP._edgeExpressHostPayUrl`, `_edgeExpressDirectPayUrl` | Also honour `EdgeExpressUseTestGateway`. Make the trailing slash consistent. |
| same | `CNP.ProcessTransaction` | Query with `xWebResponseOld.ClinicNum` instead of the patient's or payment's clinic (3.7). Add an optional short re-poll (3.7). Treat 006 / 103 as final once T5 confirms them (3.6). Keep its persistence behaviour. |
| `OpenDentBusiness/TableTypes/XWebResponse.cs` | `XWebResponseCodes` | Add `SessionTimedOut = 006` and `TransactionCancelled = 103` (enum values only). The `XWebResponseCode` column stores the enum name as text (`EnumAsString`) and `ResponseCode` stores the number, so there is no schema change. After a rollback, Open Dental would not recognise the two new names on old rows (rollback note in section 7). |
| same | new `CNP.ReconcilePending()` | Claim, query, mark and raise an alert (3.7). |
| same | `_formTimeout` | Make it a named constant documented as the session lifetime. Its value is set by sandbox test T5. |
| `OpenDentBusiness/Data Interface/ProgramProperties.cs` | `PropertyDescs.EdgeExpress` | Add `ReturnUrl`, `ReturnMode` and `UseTestGateway`. |
| same | `GetXWebCreds` | `TryDecrypt` the Auth Key (EdgeExpress and X-Charge rows) **before** the format regex. |
| `OpenDentBusiness/Data Interface/XWebResponses.cs` | new `TryClaim(long xWebResponseNum, DateTime dateTUpdateOld)` | Conditional update used by reconciliation. |
| same | new `GetPendingEdgeExpress(DateTime notBefore)` | The reconciliation query from 3.7. `GetPendingHostedPay` and `GetExpiredEdgeExpressResponses` stay as they are. |
| `OpenDentBusiness/WebTypes/Shared/XWeb/XWebs.cs` | `UseTestAccountOD`, `UseTestAccountLoopback`, and their two branches inside `XWebInputAbs.GatewayParams` | **Remove** the flags and the two branches that hard-code X-Web test credentials (an Open Dental test account and an X-Web loopback test account). Nothing in the repo sets either flag. **Keep `GatewayParams` itself**, along with its production branch: it builds every X-Web (X-Charge) gateway request from the office's own credentials. Credentials must not be compiled in. |
| same | `OnInputEvent` | Mask `XWEBAUTHKEY` / `AuthKey` before raising. |
| `OpenDental/Forms/FormEdgeExpressSetup.cs` and `.designer.cs` | `linkLabel1`, `linkLabel1_LinkClicked` | Remove (the `opendental.com` redirect). |
| same | `FillFields`, `UpdateAllProperties`, `SyncWithHQ` | Decrypt the Auth Key on load (`IsEncrypted` + `Decrypt`, blank on failure; 3.6) and encrypt it on save. `SyncWithHQ` compares the stored values, so compare encrypted to encrypted. Add a "Card entry page" group: return mode combo, return URL text box (blank = default) and "Use test gateway" checkbox. Text describes the function only. |
| same | `butSave_Click` | Drop the `Programs.IsEnabledByHq` check. On `main` it only checks that the program row exists. Optional cleanup. |
| `CodeBase/Controls/ODWebView2.cs` | `Init` | Add an overload that takes `CoreWebView2ControllerOptions` (InPrivate; 3.3). |
| **New** `OpenDental/Forms/FormEdgeExpressPayPage.cs` (+ designer) | — | WebView2 pay-page window (3.3). |
| **New** `CodeBase/LoopbackReturnListener.cs` | `Start(string pathPrefix)`, `Url`, `WaitAsync(TimeSpan, CancellationToken)`, `Dispose` | One-shot loopback listener (3.4). |
| **New** `OpenDental/Logic/EdgeExpressL.cs` | `ShowPayPage(XWebResponse response, long clinicNum, Form owner)` | Picks the mode, shows the window or browser plus listener, and returns how it ended. |
| `OpenDental/Forms/FormPayment.cs` | `MakeEdgeExpressTransactionCNP` (CreditSale and CreditAuth cases) | Replace the `FormWebBrowser` block with `EdgeExpressL.ShowPayPage`. Pass the clinic. For mode B, create the listener before the session. |
| `OpenDental/Forms/FormCreditCardManage.cs` | add-card branch (~line 295) | Same replacement. |
| `OpenDental/Main Modules/FormOpenDentalThreads.cs` | new thread | Runs `CNP.ReconcilePending` while EdgeExpress is enabled and pending rows exist. |
| Tests (`OpenDentBusiness.Tests` or `NoHqTests`) | — | See section 6. |

**Prebuilt `Bridges.dll` types to re-implement:** none. EdgeExpress doesn't use `Bridges.dll`: `EdgeExpress.cs`, `XWebs.cs` and `FormEdgeExpressSetup.cs` reference no `Bridges` type, and nothing under `OpenDental/Bridges` mentions EdgeExpress or OpenEdge.

**"Not available" stubs replaced:** none.
- The only HQ dependency, the return page, was already replaced in wave 1.
- `WebServiceMainHQNoHq.UploadPatientPortalXWebResponses` stays "not available", because the patient portal is an eService and out of scope.
- `OpenDental/Forms/FormWebBrowser.cs` keeps `UrlCloseOn` for the fallback.

## 5. Credentials and agreements the office must get

1. **Merchant account.** A Global Payments Integrated / OpenEdge merchant account with EdgeExpress, with **card-not-present (keyed, hosted pay page)** and **alias (token) storage** enabled. Card-present needs RCM and a supported terminal. Start with the office's existing OpenEdge relationship manager. The vendor's public contact for development is `developerservices@openedgepay.com` ([OpenEdge EdgeExpress](https://openedgepayments.com/edgeexpress/)).
2. **Permission to use this software.** Ask OpenEdge in writing whether a renamed, independently maintained fork of Open Dental may submit transactions with the merchant's credentials. Or does the project, or the office, need its own partner registration or integration certification ([launch announcement: partners only](https://investors.globalpayments.com/news-events/press-releases/detail/130/global-payments-launches-edgeexpress-cloud-a-new-platform))? Keep the answer on file. **Until it is in hand, leave the EdgeExpress program link disabled in production.**
3. **Current API documentation.** Ask for the current EdgeExpress CNP / HostPay specification under whatever agreement OpenEdge requires (NDA or partner terms). Ask specifically about:
   - `RETURNURL` rules: HTTPS required? loopback or unresolvable hosts allowed? any allow-list on the account?
   - GET or POST on return
   - session lifetime
   - the `sw-apikey` header
   - any request rate limits
4. **Sandbox credentials.** A test X-Web ID, Terminal ID and Auth Key for `ee.test.paygateway.com`, plus OpenEdge's test card numbers and the amounts that trigger declines.
5. **Production credentials, per clinic.**
   - Enter the 12-digit X-Web ID, 8-digit Terminal ID and 32-character Auth Key in **Setup > Program Links > EdgeExpress**, one clinic at a time.
   - Don't send the Auth Key by email or chat. Have OpenEdge read it to the person entering it, or use their merchant portal.
   - Pick the payment type for each clinic.
6. **Gateway settings.** Confirm with OpenEdge which card brands are enabled, whether CSC/AVS is required for keyed entry (codes 829–831), and the duplicate-check window (813).
7. **PCI.** Complete the self-assessment the acquirer asks for, covering the workstations used for keyed entry ([PCI SSC](https://www.pcisecuritystandards.org/document_library/)).
8. **HIPAA.** The privacy officer records that card processing falls under the payment-processing exemption ([42 U.S.C. § 1320d-8](https://www.law.cornell.edu/uscode/text/42/1320d-8)), so no BAA is needed for it. Revisit if OpenEdge provides anything beyond payment processing.
9. **Workstations.** The Microsoft WebView2 Runtime must be installed (standard on Windows 11 and on updated Windows 10). Terminals also need RCM.

## 6. Test plan

All in the sandbox (`EdgeExpressUseTestGateway=1`) with the office's test credentials, on a Release build, signed in as a **non-administrator Windows user**.

**Return-URL determination.** Do these first; they settle section 2.5.

| # | Test | Expected / record |
|---|---|---|
| T1 | Create a CreditSale session with `RETURNURL=https://edgeexpress-return.invalid/PayDone` | `SESSIONTOKEN` and `PAYPAGEURL` come back. If the gateway rejects the URL, record the code (likely 811) and switch the default to mode B or C. |
| T2 | Same with `http://127.0.0.1:{port}/edgeexpress/return/{nonce}` | Accepted or rejected. Record it. |
| T3 | Complete a payment in mode B with a debug build that logs **only** the HTTP method and the parameter **names** (never values) | GET or POST, and which fields are sent. Confirm the design ignores them. |
| T4 | Embedded mode: confirm `NavigationStarting` sees the return URL and that `RETURNTARGET=_self` navigates the top-level document | The window closes immediately with no error page. |
| T5 | Open a page and submit after 4, 6 and 11 minutes; also open one and press the page's own cancel control, if it has one | Record when it expires and the code returned (101, or 006 as Tudip says), and whether a cancel returns 103. Set `_formTimeout`. |
| T6 | Load the page in the IE fallback (`FormWebBrowser`) | Works, or document that the fallback is unusable. |

**Functional.**

| # | Test | Expected |
|---|---|---|
| F1 | Sale approved (test card) | Payment and paysplit created, receipt correct, masked number shown |
| F2 | Sale declined (decline amount) | No payment. Note shows the decline. |
| F3 | CreditAuth with "save card" | Card-on-file row with alias and expiry. Alias works for F4. |
| F4 | Recurring charge with alias (`ProcessPaymentDirect`) | Approved. Recurring charge row marked successful. |
| F5 | Alias update (expiry) and delete | 005 / alias success. Card row updated or removed. |
| F6 | Same-day void, then return of another sale, then force | Each approved. Notes and receipts correct. |
| F7 | Same card and amount twice | 813 message, no second charge |
| F8 | Wrong Auth Key; test credentials with the sandbox switch off | 805; 806 or 807. Messages point to setup. |
| F9 | Close the window before submitting | Pending, then expired. No payment. |
| F10 | Submit, then close the window before the return | Re-poll or reconciliation finds the approval. An alert is raised. Nothing is posted automatically. |
| F11 | Two workstations open at once with one pending row | Only one claims it (check the log). One alert. |
| F12 | Network cable pulled before starting; and while the page is open | Messages from 3.8. No orphan charge. |
| F13 | Patient with a 60-character name and a 20-character user name | Request under 2048 bytes (unit test too) |
| F14 | Clinics on, two clinics with different credentials. Include a patient whose clinic differs from the current clinic (add a card in `FormCreditCardManage`) and a payment whose clinic differs from the patient's (CreditAuth in `FormPayment`). | Each session and its query use the same clinic's credentials (the row's `ClinicNum`); the result is found, not left pending. The return URL and mode are per clinic. |
| F15 | WebView2 runtime uninstalled (VM) | Falls back as configured |

**Credentials and hosts.**

| # | Test | Expected |
|---|---|---|
| C1 | Save an Auth Key | The DB row starts with `osd1$`. Reload shows the key. Payments work. |
| C2 | Put a plain-text Auth Key in the row (as migrated from Open Dental) | Still works. Next save encrypts it. |
| C3 | Put a corrupted `osd1$` value in the row | The setup field is blank and the user re-enters it. A payment attempt before that fails with the credentials message, not a crash. |
| T9 | Mode B on a machine with Windows Firewall on | No firewall prompt. The listener stops after use (`netstat -ano` shows no port left). |
| H1 | Watch traffic during F1–F6 (Resource Monitor or a proxy) | Only `*.paygateway.com` and loopback |
| H2 | `powershell -File tools\Test-NoHqHosts.ps1` | Passes |

**Unit tests:**
- `GetReturnUrl` default and override
- `GetXWebCreds` with encrypted and plain keys
- request-size bound with maximum-length fields
- listener path and nonce matching: wrong path gets 404 and does not complete; a body over 16 KB is discarded
- `XWebs` no longer contains any hard-coded credential, and `GatewayParams` still emits the office's `XWebID`, `AuthKey` and `TerminalID` (X-Charge regression)
- `ProcessTransaction` queries with the row's `ClinicNum`
- 006 / 103 map to the new enum values

## 7. Size, risks and open questions

**Size: M.** About 4 days of development and tests:
- WebView2 window (including the `ODWebView2` InPrivate overload): ~1 day
- loopback listener and helper: ~1 day
- settings, encryption and cleanup (including the `XWebs` test-credential removal): ~0.5 day
- reconciliation, the new pending query, the clinic fix in `ProcessTransaction`, and the alert: ~1.5 days

Sandbox testing comes on top and waits on OpenEdge supplying test credentials and the spec.

**Risks**

1. **Contract and certification.** The integration may work technically while being outside the merchant's or partner's terms for a non-certified product. Mitigation: item 2 of section 5, and keep the program disabled until it is answered.
2. **Gateway rejects the sentinel return URL.** Mitigation: mode B, or an office-owned URL (mode C), selectable without code changes.
3. **No public spec.** Field behaviour may have changed since 24.3. Mitigation: sandbox run of T1–T6 and F1–F15 before production.
4. **Abandoned or late-completed sessions** charge a card with no payment recorded. Mitigation: re-poll, local reconciliation and an alert. Staff must act on the alerts.
5. **Rollback to Open Dental.** After the Auth Key is saved as `osd1$`, Open Dental 25.3 can't read it (it expects plain text). The pilot rollback plan must include re-entering the Auth Key in Open Dental. `xwebresponse` rows that carry the new `XWebResponseCode` names (`SessionTimedOut`, `TransactionCancelled`) are finished rows, but check how Open Dental parses an unknown name before rolling back.
6. **IE fallback.** The vendor may stop supporting the MSHTML engine. Then the fallback fails and WebView2 becomes a hard requirement for CNP.
7. **Loopback edge cases.** Endpoint security software that blocks local listeners; IPv6-only loopback. Mitigation: fall back to embedded mode A.
8. **Shared state across workstations.** The reconciliation claim relies on `DateTUpdate` resolution (seconds). Two claims in the same second are unlikely but possible. Mitigation: claim by `XWebResponseNum` and re-read the status after the claim before acting.
9. **Vendor or brand changes** (Global Payments / OpenEdge naming, host names). Mitigation: base URLs stay overridable through `Introspection` and the test-gateway switch.

**Open questions (for OpenEdge)**

1. Is a non-certified fork allowed to submit with a merchant's credentials? If not, what does partner registration or certification involve, and how long does it take?
2. `RETURNURL` rules: scheme, host resolution, loopback, any allow-list, and length limit.
3. Return method (GET or POST) and the fields included.
4. Pay-page session lifetime, and whether it can be set per request.
5. Is the `sw-apikey` header required on any call?
6. Rate limits or throttling on HostPay and DirectPay.
7. Is the `HOSTPAYSETTING` block still the supported way to customize, or has the pay page moved to a newer API (EdgeExpress Cloud)?
8. Which session-creation path is current: `.../hostpay/transactions/` (this repo) or `.../hostpay/paypage/` (Tudip)?
9. Does `QUERYPAYMENT` return 006 for a timed-out session and 103 for a user cancel, and which spec version does the gateway assume when the request carries none?

## References

- OpenEdge, EdgeExpress product page: https://openedgepayments.com/edgeexpress/
- Global Payments, EdgeExpress Cloud launch (partner-only availability): https://investors.globalpayments.com/news-events/press-releases/detail/130/global-payments-launches-edgeexpress-cloud-a-new-platform
- Tudip, OpenEdge pay page integration walkthrough (third party): https://tudip.com/?p=52743
- iCare, EdgeExpress response code table (third-party help file): https://icarenextgen.icaresoftware.com/EdgExpHelp.pdf
- RFC 6761 §6.4, the `.invalid` name: https://www.rfc-editor.org/rfc/rfc6761#section-6.4
- RFC 2606, reserved top-level names: https://www.rfc-editor.org/rfc/rfc2606
- RFC 8252 §7.3 and §8.3, loopback redirects for native apps: https://www.rfc-editor.org/rfc/rfc8252#section-7.3
- W3C Secure Contexts, potentially trustworthy origins: https://www.w3.org/TR/secure-contexts/#is-origin-trustworthy
- Global Payments developer portal (no EdgeExpress material found, 2026-10-18): https://developer.globalpayments.com/
- Microsoft, WebView2 navigation events: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/navigation-events
- Microsoft, WebView2 WinForms `CoreWebView2CreationProperties.IsInPrivateModeEnabled` (listed for SDK 1.0.2420.47): https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.winforms.corewebview2creationproperties.isinprivatemodeenabled
- Microsoft, Internet Explorer lifecycle FAQ: https://learn.microsoft.com/en-us/lifecycle/faq/internet-explorer-microsoft-edge
- 42 U.S.C. § 1320d-8 (HIPAA, financial institutions): https://www.law.cornell.edu/uscode/text/42/1320d-8
- PCI Security Standards Council document library: https://www.pcisecuritystandards.org/document_library/
- In repo:
  - `OpenDentBusiness/WebBridges/EdgeExpress.cs`
  - `OpenDentBusiness/WebTypes/Shared/XWeb/XWebs.cs`
  - `OpenDentBusiness/Data Interface/ProgramProperties.cs`
  - `OpenDental/Forms/FormEdgeExpressSetup.cs`
  - `OpenDental/Forms/FormWebBrowser.cs`
  - `CodeBase/Controls/ODWebView2.cs`
  - `CDT/README.md`
  - commit `b5ca25960`
  - `OpenDentBusiness/Data Interface/XWebResponses.cs`
  - `OpenDentBusiness/TableTypes/XWebResponse.cs`
  - `D:/Dev/opendental-schema/hq-vendor-table.md`

## Fact-check log (2026-10-18)

An adversarial review against `main` at `6855026f5` and the vendor's public pages.

**Confirmed:**
- Feasibility **partner-only**. The 2017 launch release still says EdgeExpress Cloud is exclusive to Global Payments software partners. The product page still offers sample code and test environments only on request. No public API reference was found, including on the Global Payments developer portal.
- The base URLs, `Introspection` overrides, the sandbox switch, the XML-over-POST authentication with XWEBID / XWEBTERMINALID / XWEBAUTHKEY, the 2048-byte limit and ASCII encoding, the `HOSTPAYSETTING` contents, the 814 → pending mapping for `QUERYPAYMENT`, and the 10 + 5 minute expiry logic all match the code.
- Every code touchpoint exists: the line ranges for `RCM` (139–273) and `CNP` (278–890), `FormPayment.MakeEdgeExpressTransactionCNP`, `FormCreditCardManage` line 295, `FormCreditCardEdit`, `RecurringCharges.SendEdgeExpress`, `XWebResponses` lines 83 and 96 (no callers), `WebServiceMainHQNoHq.UploadPatientPortalXWebResponses`, `FormWebBrowser.UrlCloseOn`, `FormEdgeExpressSetup.linkLabel1`, and `FormOpenDentalThreads`.
- No `Bridges.dll` surface; no HQ web method; no OAuth.
- No Open Dental hosts in the design. The spec itself names `opendental.com` and `patientviewer.com` only as things to remove or already removed.
- The proposed UI text is neutral.

**Corrected:**
1. `XWebs.GatewayParams` must **not** be removed. It builds every X-Web (X-Charge) request. Only the two hard-coded test-account branches and their flags go.
2. Reconciliation can't reuse `GetPendingHostedPay`: its `PaymentNum=0` filter skips desktop sales, and it also returns X-Web rows. `GetExpiredEdgeExpressResponses` returns finished rows with no age bound. A new query was specified.
3. Added an existing clinic-mismatch bug: `ProcessTransaction` queries under the patient's or payment's clinic, not the clinic the session was created with.
4. Added response codes 006 (session timed out) and 103 (user cancelled), which the public table lists but the enum lacks; today they are treated as pending.
5. Auth Key migration: Open Dental stores the key as plain text, so "data copied from Open Dental" never yields an undecryptable `osd1$` value. Rewrote 3.6 and test C3. The blank-field behaviour needs `IsEncrypted` + `Decrypt`, not `TryDecrypt`.
6. Void requests carry no `CLERK`. Return requests can carry `ALIAS` / `CREATEALIAS`. `CLERK` is sent only for a logged-in non-eService user. The customization block also hides the title field and sets a label.
7. InPrivate is confirmed in SDK 1.0.2420.47, but `ODWebView2.Init` needs an overload to pass controller options.
8. Gating wording: `FormPayment` and `FormClaimPayEdit` check `Program.Enabled` plus non-blank credentials rather than `Programs.IsEnabled`. Added the RCM call sites.
9. Noted Tudip's different session-creation path (`/hostpay/paypage/`) and its 006 expiry code as open questions. Noted that RCM receives the credentials in a loopback URL.
10. `IsEnabledByHq` only checks that the program row exists. `SyncWithHQ` refers to practice-level credentials, not Open Dental's HQ.
11. Size raised from 3–4 to about 4 days for items 2–4 and 7.
