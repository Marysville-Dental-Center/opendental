# QuickBooks Online (Intuit): direct-to-vendor spec

Status: draft, 2026-10-18, fact-checked the same day against Intuit's public docs and the code on `main`. Baseline: `main` at `6855026f5`. Vendor: Intuit Inc. API: **QuickBooks Online Accounting API v3** (public), OAuth 2.0 authorization code grant with a client secret.

## Summary

- **What it is.** An office whose deposit software is QuickBooks Online (Setup > Preferences > Manage - General, `PrefName.AccountingSoftware` = 2) can press **Send QBO** on a deposit slip. That creates a Deposit in its QuickBooks Online company: one line per selected payment, posted to a chosen bank account and income account, with an optional class. Setup is under Accounting > Setup > QuickBooks Online, or Program Links > QuickBooks Online.
- **What HQ provided.** Only **Open Dental's Intuit app credentials**. The prebuilt `Bridges.QuickBooksOnline` called `IWebServiceMainHQ.BuildOAuthUrl(regKey, "QuickBooksOnline")`. According to the mock's comment, that call returned `clientId:clientSecret`, not a URL. Bridges.dll then did the OAuth exchange and every Accounting API call itself, from the workstation, through the Intuit .NET SDK. The redirect page (where the user read the code and realm ID to paste back) was Open Dental's page (inferred).
- **State on `main`.** `BuildOAuthUrl` throws `HqNotAvailableException`, so Bridges.dll can't get credentials. Authenticate, refresh, the account and class lists, and Send QBO all fail. Tokens migrated from an Open Dental database were issued to Open Dental's app and encrypted with Open Dental's key. `CDT.Class1.Decrypt` returns blank for them, so they're unusable twice over.
- **Direct design:**
  - Each office registers its **own Intuit app** (it can be private and unlisted) and enters its client ID and secret in the setup form. The secret is stored as `osd1$` in a masked, high-security `programproperty` row.
  - The **system browser** opens Intuit's consent page.
  - **Sandbox:** the redirect goes to a **loopback listener** (`http://localhost:<port>/callback`), which Intuit allows only for sandbox (development) keys.
  - **Production:** Intuit requires an **HTTPS, non-localhost, non-IP** redirect URI. The office registers a URL on a domain it controls, and the user **pastes the redirected address** back into the app. The app parses `code`, `state` and `realmId` from it. An optional static relay page can forward that redirect to the loopback listener.
  - Intuit **rotates refresh tokens** (a new value about every 24 hours). Every workstation shares one stored refresh token, so refreshes write back with an optimistic **compare-and-swap** on the `programproperty` row. Access tokens (1 hour) stay in memory only.
- **Replace Bridges.dll's QuickBooks Online types** with in-repo code in `OpenDentBusiness/Bridges/QuickBooksOnline/` (namespace `OpenDentBusiness.Qbo`). The data types and the account, class and deposit methods keep the signatures this repo calls. **Recommended:** a small `HttpClient` + Newtonsoft.Json 12 client, deleting the 14 `Intuit.Ipp.*` DLLs and 13 `Serilog*` DLLs. The current Intuit SDK (Apache-2.0, fine to ship) needs Newtonsoft.Json 13 or newer.
- **Pre-existing defects fixed on the way:**
  - stale refresh tokens across workstations
  - a possible duplicate deposit when the retry follows a lost response (fix: Intuit's `requestid`)
  - account lookup by display name
  - migrated ciphertexts silently blank
- **Feasibility: public API.** No partner agreement or NDA is needed. The gates are on the office's side:
  - **Production keys** require Intuit's app assessment questionnaire, even for a private app, plus host domain, launch, disconnect and reconnect URLs, an EULA and a privacy policy URL.
  - The production redirect rule above.
  - Intuit's **App Partner Program** meters read calls. The free Builder tier covers far more than this integration uses.
  - Intuit does **not** sign a BAA (per secondary sources), so no PHI may be sent. Only amounts, dates, a memo and account IDs are sent today, and that must stay so.
- **Size: M**, about 5 to 7 days.

## 1. What the integration does, and what HQ provided

### 1.1 What the office uses it for

- **Turning it on:**
  - The `program` row `QuickBooksOnline` (ConvertDatabases6 `To20_4_5`, `Enabled=0`; its ProgDesc was renamed to "QuickBooks Online" in `To20_4_20`) is enabled in Program Links. `FormProgramLinks` opens `FormQuickBooksOnlineSetup(true)`, which is the only place the Enabled box shows.
  - **Deposit Software** is set to QuickBooks Online in Setup > Preferences > Manage - General (`UserControlManageGeneral`). It refuses if the program isn't enabled.
- **Setup** (`OpenDental/Forms/FormQuickBooksOnlineSetup.cs`):
  - **Authenticate** opens the browser. `FormQuickBooksOnlineAuthorization` then asks the user to paste an **Authorization Code** and a **Realm ID**. The code is exchanged and the refresh token and realm ID are saved encrypted.
  - Next the form loads **deposit accounts, income accounts and classes** from QuickBooks Online. The user moves the ones staff may pick into "in OD" lists.
  - The picks are saved as `Name,Id|Name,Id` strings in "Deposit Accounts", "Income Accounts" and "Class Refs". Names containing `,` or `|` are refused.
- **Daily use** (`OpenDental/Forms/FormDepositEdit.cs`):
  - **Send QBO** (or Save on a new deposit) opens `FormQBAccountSelect(true)`, which offers the chosen deposit and income account names.
  - `CreateDepositQBO` looks up the IDs by name (`ProgramProperties.GetQuickBooksOnlineEntityIdByName`) and calls `QuickBooksOnline.CreateDeposit(realmId, token, depositAcct, incomeAcct, classRef, amounts, memo, date)`. `amounts` holds **one decimal per selected patient or insurance payment**.
  - On success, `deposit.IsSentToQuickBooksOnline=1`, a `DepositSlips` security log entry is written, and the button is disabled. Resending asks for confirmation.
  - On failure, the user can still save the deposit locally ("create the deposit locally anyway?").
- **Other touch points:**
  - `Accounts.DepositsLinked()`: QuickBooks Online counts as linked when both account lists are non-empty.
  - The `FormDepositEdit` 18-item slip warning.
  - `FormAccounting`'s "QuickBooks Online" setup menu item, which needs the program to be enabled.
- **Data model, unchanged by this spec:**
  - The program row and its properties: "Deposit Accounts", "Income Accounts" and "Class Refs" in plain text; "Refresh Token" and "Realm ID" as `osd1$`, masked and high-security (ConvertDatabases7 `To22_3_1`).
  - The "Access Token" row, already deleted by ConvertDatabases7 `To22_3_1`.
  - `deposit.IsSentToQuickBooksOnline`.

### 1.2 What Open Dental's HQ provided

| Step | Code on `main` | What HQ did (inferred from the calls) | Now |
|---|---|---|---|
| App credentials | Bridges `QuickBooksOnline.GetQuickBooksOnlineAuthorizationUrl()`, `MakeAccessTokenRequest`, `RefreshTokens`: each needs a client ID and secret | `BuildOAuthUrl(regKey, OAuthApplicationNames.QuickBooksOnline)` returned **Open Dental's Intuit client ID and secret**, gated on a registration key. The proof is the commented-out `retVal=QUICKBOOKSONLINE_CLIENT_ID+":"+QUICKBOOKSONLINE_CLIENT_SECRET` in `WebServiceMainHQMockDemo.cs:336-340` | `WebServiceMainHQNoHq.BuildOAuthUrl` throws `HqNotAvailableException` |
| Redirect page | `FormQuickBooksOnlineAuthorization`: "enter the Authorization Code from your browser" and "the Realm Id" | Open Dental's app had a registered HTTPS redirect URI, presumably an Open Dental page that displayed `code` and `realmId` for copying. Not visible in this repo | Gone. The office registers its own (section 3.2) |
| Token exchange, refresh, API calls | `Bridges.QuickBooksOnline.*` | **Not HQ** (inferred). The forms hand Bridges.dll only a realm ID and tokens, never an HQ object, so the calls ran on the workstation. `NoHqTests/bin/Debug` gets `Intuit.Ipp.OAuth2PlatformClient.dll` and several `Serilog*` DLLs copied in as Bridges.dll dependencies. `OpenDental.csproj` references all 14 `Intuit.Ipp.*` assemblies, and the binaries audit attributes them to Bridges.dll. Whether the data calls went through `Intuit.Ipp.DataService` or plain HTTP isn't visible from this repo | Unreachable without credentials |
| Sandbox switch | `Introspection` `QuickBooksOnlineSandboxUrl`, `QuickBooksOnlineSandboxEnvironment`, `QuickBooksOnlineSandboxClientIdAndSecret` | Developer overrides that only Bridges.dll reads. The repo has no other consumers | Replaced by an "Environment" property (3.3) |

So HQ's only role was handing out a secret. Everything else already went straight from the workstation to Intuit. This spec replaces the secret with the office's own and moves the Bridges code into the repo.

### 1.3 Defects found while reading the code (fix in the same change)

1. **Refresh-token rotation and stale caches.**
   - Both forms read "Refresh Token" from the Programs cache when they load, refresh with it, and write the result back through `UpdateProgramPropertyWithValue` against that cached row.
   - Intuit replaces the refresh token value about every 24 hours (2.3). Workstation B can therefore hold a value that A has already rotated away, and gets `invalid_grant`.
   - Fix: re-read the row from the database before each refresh and write with compare-and-swap (3.4).
2. **Possible duplicate deposits.** `CreateDepositQBO` retries `CreateDeposit` after a token refresh. Nothing makes the create idempotent, so if the first POST succeeded and only its response was lost, the retry posts a second deposit. Fix: Intuit's `requestid` query parameter, one GUID per Send (2.4).
3. **Lookup by display name.** `GetQuickBooksOnlineEntityIdByName` returns the first pair whose name matches. Two sub-accounts with the same `Name` under different parents collide. Fix: store and show `FullyQualifiedName` (open question 3) and match on ID wherever an ID is at hand.
4. **Silent blank after migration.** `CDT.Class1.Decrypt`'s `false` return is ignored, so Open Dental-encrypted values just look empty. Fix: show "Not connected" and say why (3.9).
5. **Dead field.** `FormDepositEdit._programPropertyQboAccessToken` is unused. Its row was deleted in ConvertDatabases7. Remove the field.

## 2. The vendor's public API

### 2.1 Documentation status

- Intuit's docs are public, with no login or NDA, at developer.intuit.com. Those pages render client side, so the facts below were checked against:
  - the machine-readable OpenID discovery documents
  - Intuit's GitHub SDKs and samples
  - Intuit's developer blog
  - search excerpts of the doc pages

  Each point cites its source. Check the pages again in a browser when implementing.
- **No partner agreement is needed** for the Accounting API. The Developer Terms of Service and Exhibit A apply to every app (2.6).

### 2.2 Base URLs

| Purpose | Production | Sandbox |
|---|---|---|
| Consent page (browser) | `https://appcenter.intuit.com/connect/oauth2` | same |
| Token (code exchange, refresh) | `https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer` | same |
| Revoke | `https://developer.api.intuit.com/v2/oauth2/tokens/revoke` | same |
| Discovery document | `https://developer.api.intuit.com/.well-known/openid_configuration` | `https://developer.intuit.com/.well-known/openid_sandbox_configuration` |
| Accounting API | `https://quickbooks.api.intuit.com/v3/company/{realmId}/...` | `https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/...` |

- The OAuth endpoints come from the two discovery documents. The production document lists only the `code` response type, and client authentication by `client_secret_basic` or `client_secret_post`. It lists **no** `code_challenge_methods_supported`, so **PKCE is not documented**, and the client secret is mandatory.
- The sandbox API host is shown in Intuit's Node.js OAuth client README ([intuit-oauth](https://unpkg.com/intuit-oauth@4.2.5/README.md)).
- Production and sandbox share the OAuth endpoints. What differs is which **keys** you use (Development or Production) and which API host you call.

### 2.3 Authentication, step by step (authorization code + client secret)

1. **Authorize** (browser): `GET https://appcenter.intuit.com/connect/oauth2` with:
   - `client_id`
   - `response_type=code`
   - `scope=com.intuit.quickbooks.accounting`. This is the only scope needed. No `openid` and no payments scope ([sample scopes](https://github.com/IntuitDeveloper/OAuth2-Dotnet-WithoutSDK)).
   - `redirect_uri`, which must match a URI registered for that key set **exactly**, including any trailing slash ([Intuit blog](https://blogs.intuit.com/2017/10/31/oauth-2-0-integration-tips-careful-entering-redirect-uri/))
   - `state`: 32 random bytes, base64url

   The user signs in, picks a company and approves.
2. **Callback:** Intuit redirects to `redirect_uri?code=...&state=...&realmId=...`. `realmId` is the company ID used in every API path. The [intuit-cli](https://github.com/intuit/intuit-cli-for-quickbooks) README shows `code` and `realmId` on the callback. It doesn't mention `state`, which is echoed back as in any OAuth 2.0 code flow; confirm against a sandbox response. The code is short-lived and single use.
3. **Exchange:** `POST .../tokens/bearer` with:
   - headers `Authorization: Basic base64(client_id:client_secret)`, `Content-Type: application/x-www-form-urlencoded` and `Accept: application/json`
   - body `grant_type=authorization_code&code=...&redirect_uri=...`

   The response has `access_token`, `refresh_token`, `token_type=bearer`, `expires_in` (3600) and `x_refresh_token_expires_in` (seconds) ([intuit-oauth README](https://unpkg.com/intuit-oauth@4.2.5/README.md)).
4. **Refresh:** same endpoint and headers, with body `grant_type=refresh_token&refresh_token=...`.
   - Access tokens last **one hour**.
   - Refresh tokens are valid for **100 days, rolling**, extended on each use.
   - Intuit **changes the refresh token value about every 24 hours**, or on the first refresh after that. Apps must always keep the value from the latest response ([Authorization FAQ](https://developer.intuit.com/app/developer/qbo/docs/develop/authentication-and-authorization/faq)).
   - Intuit announced on 12 November 2025 (updated February 2026) that every refresh token now has a **hard five-year maximum**. For the accounting and payments scopes, the first tokens hit that cap in October 2028. Intuit also said the refresh response would gain a field giving the expiry, with details to follow ([Intuit developer blog](https://blogs.intuit.com/2025/11/12/important-changes-to-refresh-token-policy), which now redirects to [Medium](https://medium.com/intuitdev/important-changes-to-refresh-token-policy-8443779d40db); [Authorization FAQ](https://developer.intuit.com/app/developer/qbo/docs/develop/authentication-and-authorization/faq)). The field's JSON name wasn't found in any public source (open question 6). Read `x_refresh_token_expires_in`, and treat any extra expiry field as optional.
5. **Revoke:** `POST .../v2/oauth2/tokens/revoke` with the same Basic header and JSON `{"token":"<refresh_token>"}`. This disconnects the app from the company.
6. **Redirect URI rules:**
   - **Sandbox (Development keys):** `http://localhost` is allowed.
   - **Production keys:** the URI must be **HTTPS**, and **localhost and IP addresses are refused** ([Set redirect URIs](https://developer.intuit.com/app/developer/qbo/docs/develop/authentication-and-authorization/set-redirect-uri); [intuit-cli README](https://github.com/intuit/intuit-cli-for-quickbooks): Intuit rejects localhost for production apps; [developer forum](https://help.developer.intuit.com/s/question/0D54R00007Bf8sfSAB/why-does-production-redirect-uri-not-allow-localhost)).

   This rule drives the desktop design in 3.2.

### 2.4 Endpoints and fields we need

Every call sends `Authorization: Bearer <access_token>`, `Accept: application/json` and `minorversion=75`. Since 1 August 2025, versions 1 to 74 are retired and the API answers as 75 whatever is sent ([Intuit blog](https://blogs.intuit.com/?p=34635); [Codat summary](https://docs.codat.io/updates/250219-qbo-minor-versions-update)).

| Use | Call | Fields used |
|---|---|---|
| Deposit-to accounts (setup) | `GET /v3/company/{realmId}/query?query=select * from Account where AccountType in ('Bank','Other Current Asset') and Active = true STARTPOSITION n MAXRESULTS 1000` (URL-encoded) | `QueryResponse.Account[].Id`, `.Name`, `.FullyQualifiedName`. The type filter is our choice; see open question 2 ([Account](https://developer.intuit.com/app/developer/qbo/docs/api/accounting/all-entities/account)) |
| Income accounts (setup) | same, with `AccountType in ('Income','Other Income')` | same |
| Classes (setup) | `GET .../query?query=select * from Class where Active = true STARTPOSITION n MAXRESULTS 1000` | `QueryResponse.Class[]` ([Class](https://developer.intuit.com/app/developer/qbo/docs/api/accounting/all-entities/class)) |
| Linked company (setup, once per connect) | `GET .../companyinfo/{realmId}` | `CompanyInfo.CompanyName`, for display only |
| Create deposit | `POST /v3/company/{realmId}/deposit?minorversion=75&requestid=<guid>` with body `{"TxnDate":"yyyy-MM-dd","PrivateNote":memo,"DepositToAccountRef":{"value":id},"Line":[{"Amount":12.34,"DetailType":"DepositLineDetail","DepositLineDetail":{"AccountRef":{"value":incomeId},"ClassRef":{"value":classId}}}, ...]}` | `Deposit.Id`, `Deposit.Line[].DepositLineDetail.ClassRef`. A missing ClassRef means class tracking is off, which is `IsClassRefNull()` ([Deposit](https://developer.intuit.com/app/developer/qbo/docs/api/accounting/all-entities/deposit)) |

Notes:

- **No field lists.** The query language accepts only `select *` or `select count(*)`. Naming fields (`select Id, Name ...`) isn't supported, and every populated field comes back. Pick the fields out client side. `OR`, `GROUP BY` and `JOIN` aren't supported either; `IN` is ([data queries](https://developer.intuit.com/app/developer/qbo/docs/learn/explore-the-quickbooks-online-api/data-queries)).
- **Paging.** A query returns at most 1000 rows. `STARTPOSITION` is 1-based. Loop until a page comes back short.
- **Idempotent create.** Intuit recommends a `requestid` on every write. A repeated request with the same ID returns the original response instead of creating a second record. The ID must be unique per company and at most 50 characters ([field definitions](https://developer.intuit.com/app/developer/qbo/docs/learn/learn-basic-field-definitions); [Intuit blog](https://blogs.a.intuit.com/2015/04/06/15346/)). Generate one GUID (`N` format, 32 characters) per Send click and reuse it for every retry of that Send.
- **Errors** come back as `{"Fault":{"Error":[{"Message","Detail","code"}],"type":...}}`, for example `ValidationFault` or `AuthenticationFault`. HTTP 401 means the access token expired. Log the `intuit_tid` response header for support.
- **What goes to Intuit:** amounts, the deposit date, the memo, and account and class IDs. **Never** patient names, check numbers or payment notes. That matches today's behaviour, and section 2.6 explains why it must stay that way.

### 2.5 Rate limits and metering

- **Throttles:** 500 requests per minute per realm ID, and 10 requests per second per realm ID and app. Batch calls (unused here) have their own limit. HTTP 429 (`ThrottleExceeded`, error code 003001) means throttled. Back off before retrying; the wait of about 60 s used below is our choice ([REST API features](https://developer.intuit.com/app/developer/qbo/docs/learn/rest-api-features); [limits and throttles](https://developer.intuit.com/app/developer/qbo/docs/learn/limits-and-throttles)). An older Intuit blog describes the 10 limit as concurrent requests rather than per second; either way it can't bite at this integration's volume.
- This integration makes at most a few calls per user action, so throttling is only a corner case.
- **App Partner Program** (from 28 July 2025, US):
  - **Core** calls (data in: creates and updates) are free and unlimited.
  - **CorePlus** calls (data out: reads and queries) are metered.
  - The free **Builder** tier includes 500,000 CorePlus credits a month. Calls over that are **blocked**, not billed ([Partner FAQ](https://developer.intuit.com/app/developer/qbo/docs/get-started/partner-faq); [Apideck summary](https://www.apideck.com/blog/quickbooks-api-pricing-and-the-intuit-app-partner-program)).
  - Our reads are the account, class and company lookups on the setup screen: roughly 3 to 6 credits per open. Deposits are Core.
  - So the office stays in Builder indefinitely. Don't add background polling.

### 2.6 Terms that matter

- **Developer Terms and Exhibit A** ([Terms](https://developer.intuit.com/app/developer/qbo/docs/legal-agreements/intuit-terms-of-service-for-intuit-developer-services); [Exhibit A](https://developer.intuit.com/app/developer/qbo/docs/legal-agreements/intuit-terms-of-service-for-intuit-developer-services/intuit-data-services-quickbooks)):
  - The app's developer, and not Intuit, is the licensor to end users.
  - The developer must give each end user an EULA or terms of service, and must provide support.

  With a per-office app, the office is both developer and only user. That's a formality, but it is still the office's legal act (5.4).
- **Production keys** need the **app assessment questionnaire** (app details plus compliance), even for a private app that is never listed on the QuickBooks App Store. The app also needs:
  - a host domain, a launch URL and a disconnect URL
  - an EULA URL and a privacy policy URL ([DBSync walkthrough](https://docs.mydbsync.com/cloud-workflow/connectors/quickbooks-online/create-a-private-quickbooks-online-qbo-app-on-intuit-developer-portal); [Apideck app compliance](https://developers.apideck.com/connectors/quickbooks/docs/application_owner+app_compliance))
  - a **Reconnect URL**. Intuit added this field to the app settings around February 2026, alongside the five-year refresh token cap. The Medium copy of Intuit's announcement calls it mandatory; whether the portal enforces it for a private app wasn't confirmed ([Intuit developer blog](https://blogs.intuit.com/2025/11/12/important-changes-to-refresh-token-policy); [release notes](https://developer.intuit.com/app/developer/qbo/docs/release-notes/general-release-notes)). Provide one anyway.

  These are third-party walkthroughs and an Intuit blog, so expect the portal wording to differ.
- **PHI / BAA.** Secondary sources say Intuit won't sign a BAA for any QuickBooks product ([Accountable](https://www.accountablehq.com/post/is-quickbooks-online-hipaa-compliant-short-answer-no-here-s-what-to-do-instead)). Treat QuickBooks Online as **outside** the HIPAA boundary:
  - send no patient identifiers
  - say in Setup that the deposit memo is sent to the accounting service
  - leave the office to confirm with Intuit in writing (5.4)
- **The SDK's license.** The Intuit .NET SDK is Apache-2.0 (Copyright 2016 Intuit, Inc.; [License.md](https://github.com/intuit/QuickBooks-V3-DotNET-SDK/blob/master/License.md)). Redistributing it is allowed with the license and NOTICE. The recommended design drops it anyway (4.1).
- **No app review or listing** is needed for a private app beyond the questionnaire.

## 3. Desktop design

### 3.1 Credential model

- **Each office registers its own Intuit app** at developer.intuit.com, signed in with an Intuit account the office controls (section 5). The app holds:
  - **Development keys**, which work only against sandbox companies
  - **Production keys**, released after the questionnaire
- The office types its **client ID and client secret** into the setup form. Nothing is compiled in or committed.
- **A project-wide app is possible later** (one app, many offices). But it would make the project the "developer" under Exhibit A, with EULA, support and redirect-hosting duties, and every office would hold the same secret. Not proposed (open question 1). The setup form accepts any client ID, so either model works.

### 3.2 Auth flow in the WinForms app

`FormQuickBooksOnlineSetup.butAuthenticate_Click` (renamed **Connect...**) and `FormQuickBooksOnlineAuthorization` are rewritten. It needs the Setup permission, and editing the secret needs `ManageHighSecurityProgProperties`. Steps:

1. **Validate the settings.** Client ID, secret and redirect URI must not be blank. The redirect URI must be absolute.
   - **Production:** `https` with a host that isn't `localhost` or an IP literal. Otherwise show "Intuit only accepts HTTPS redirect addresses for production keys."
   - **Sandbox:** may also be `http://localhost:<port>/<path>`.
2. **Create `state`** (32 random bytes, base64url). Keep it in memory only.
3. **Build the consent URL** (2.3 step 1) and open it in the default browser: `Process.Start(new ProcessStartInfo(url){UseShellExecute=true})`, keeping the existing `ODCloudClient.LaunchFileWithODCloudClient` branch for AppStream.
4. **Receive the callback.** Which mode runs is chosen from the redirect URI:
   - **Loopback (sandbox, or a relay page).** Use this when the registered URI is `http://localhost:<port>/<path>` (sandbox only), or when "Relay to port" is set (production with a relay page, below).
     - Before opening the browser, start `QboLoopbackListener`: a `TcpListener` on `IPAddress.Loopback`, plus `IPv6Loopback` when the host is `localhost`, on that port. Use a `TcpListener`, not `HttpListener`, to avoid http.sys URL ACLs.
     - Accept one `GET` on the path. Parse `code`, `state`, `realmId` and `error`, and reject a `state` that doesn't match.
     - Reply with a **self-contained** HTML page saying the connection is done and the tab can be closed. It loads no external assets.
     - Stop on the first valid request, on Cancel, or after 5 minutes.
     - The paste box below stays visible as a fallback.
     - Not offered when `ODBuild.IsThinfinity()` or `ODCloudClient.IsAppStream`, because the browser doesn't run on the same machine.
   - **Paste (production default).** The browser ends on the office's HTTPS redirect page with the parameters in the address bar.
     - The form shows "After approving, copy the full address from your browser's address bar and paste it here", one text box, OK and Cancel.
     - `QboRedirectParser.Parse(text)` accepts a full URL or a bare query string, and pulls out `code`, `state` and `realmId`.
     - For backward compatibility it also accepts the two old boxes (code and realm ID), shown under "Enter separately"; `state` can't be checked in that case.
     - Reject a mismatched `state` with "This address is from a different connection attempt. Click Connect again."
   - **Relay page (optional, production).** The office can host the static `docs/integrations/qbo-redirect.html` (new, about 30 lines, no external assets) at its registered HTTPS URI.
     - By default the page shows the parameters with a Copy button. When its `RELAY_PORT` constant is set (edited once by the office when publishing it), it instead forwards the browser to `http://localhost:<RELAY_PORT>/callback?<same query>`.
     - Intuit's redirects carry no fragment of ours, so the port can't come from the URL. The app's "Relay Port" setting must equal the page's constant, and the app listens on that port before opening the browser.
     - Intuit sees only the HTTPS URI, so its rule is met. This removes copy and paste without any server-side code.
5. **Exchange the code** on a background task (2.3 step 3), so the UI stays responsive.
6. **Fetch `CompanyInfo`** for the company name.
   - If a "Realm ID" is already stored and differs, confirm first: "This connects a different QuickBooks Online company. The saved deposit accounts, income accounts and classes belong to the previous company and will be cleared. Continue?"
   - On yes, blank the three entity lists.
7. **Save** (3.3):
   - the encrypted refresh token and realm ID, the refresh token expiry (UTC), and the company name
   - prime the in-memory access token cache
   - `DataValid.SetInvalid(InvalidType.Programs)`
   - `SecurityLogs.MakeLogEntry(EnumPermType.Setup, 0, "QuickBooks Online connected")`, with no tokens and no company name

   Then load the account and class lists.
8. **On error,** show Intuit's `error` or `error_description`, or the HTTP status, in a neutral message, and save nothing.

**Disconnect** (new button):

- Call the revoke endpoint with the refresh token.
- Then blank the refresh token, the realm ID, the expiry and the company name. Revoke errors are logged and ignored, because the user may already have disconnected the app in QuickBooks Online.
- If Deposit Software is QuickBooks Online, warn that Send QBO stops working until the office reconnects.

### 3.3 Where credentials and tokens live

All values are `programproperty` rows of the existing `QuickBooksOnline` program, with `ClinicNum=0` and `ComputerName=''`.

**New rows are inserted at runtime** by `QboSettings.EnsurePropertyRows()`. It is idempotent and runs from `FormQuickBooksOnlineSetup_Load`; the form then calls `DataValid.SetInvalid(InvalidType.Programs)`. There is no schema change and no ConvertDatabases step. Readers must treat a missing row as blank: use `GetPropValFromList`, or a null check, because `ProgramProperties.GetPropForProgByDesc`, which both forms use today, returns null for a missing row. That matters in `FormDepositEdit`, which can run before anyone opens the setup form. It should use the shared `ProgramProperties.InsertIfMissing` helper proposed in the PostcardMania spec.

| PropertyDesc | Status | Value | Encrypted | IsMasked | IsHighSecurity |
|---|---|---|---|---|---|
| `Deposit Accounts` | existing | `Name,Id\|Name,Id`. The name becomes `FullyQualifiedName` (1.3 item 3) | no | no | no |
| `Income Accounts` | existing | same | no | no | no |
| `Class Refs` | existing | same | no | no | no |
| `Refresh Token` | existing | `osd1$...` via `CDT.Class1.Encrypt`. Saved only when `Encrypt` returns true. **Not `TryEncrypt`**, which falls back to plain text | **yes** | yes | yes |
| `Realm ID` | existing | `osd1$...` (kept as is, although the ID isn't secret) | yes | yes | yes |
| `Client ID` | new | The office's client ID (a public identifier) | no | no | no |
| `Client Secret` | new | `osd1$...` via `Encrypt` | **yes** | **yes** | **yes** |
| `Environment` | new | `Production` (default) or `Sandbox`. It chooses the API host and which key set the user should enter | no | no | no |
| `Redirect URI` | new | Exactly as registered in the Intuit portal for that environment | no | no | no |
| `Relay Port` | new | Blank, or a port for the relay page (3.2 step 4) | no | no | no |
| `Refresh Token Expires` | new | UTC ISO 8601: now plus `x_refresh_token_expires_in`, updated on each refresh | no | no | no |
| `Company Name` | new | From `CompanyInfo`, display only | no | no | no |

- **Not stored:** access tokens (memory only), `state`, the authorization code.
- **The secret is deliberately in the database,** so every workstation can refresh. `osd1$` is obfuscation with a public key (`CDT/README.md`). Database read access therefore exposes the secret and the refresh token, which together mean read and write access to the company's books. Mitigations:
  - the rows are high-security
  - normal database access control
  - Disconnect and secret rotation in the Intuit portal if a copy leaks (7.2)
- **Environment and keys must match.** Switching Environment blanks the refresh token, realm ID and company name after a confirmation, because tokens from one key set don't work against the other host.

### 3.4 Token runtime and refresh

New `QboTokenManager` (one per process, in `OpenDentBusiness.Qbo`):

- **Holds** `{AccessToken, ExpiresUtc, RefreshTokenUsed}` keyed by client ID plus realm ID.
- **`GetAccessToken()`:**
  - Returns the cached token if it is valid for more than 5 more minutes.
  - Otherwise refreshes, **single-flight** behind a `SemaphoreSlim` so concurrent callers in one process trigger one refresh.
- **A refresh:**
  1. Reads the current "Refresh Token" value **from the database**, not the cache. The existing `ProgramProperties.GetValFromDb(programNum, "Refresh Token")` already does this and is Middle Tier-safe (`Meth.GetString`). It doesn't filter on `ClinicNum`, which is fine because these rows are only `ClinicNum=0`. No new read method is needed.
  2. Decrypts it and calls Intuit.
  3. If the returned refresh token differs, writes it with **compare-and-swap**: new `ProgramProperties.UpdateValueIfUnchanged(programPropertyNum, oldCipher, newCipher)` runs `UPDATE programproperty SET PropertyValue=@new WHERE ProgramPropertyNum=@n AND PropertyValue=@old` and returns whether one row changed. Deterministic `osd1$` encryption (`CDT/README.md`) is what makes comparing ciphertexts valid. It is Middle Tier-safe.
  4. If the CAS fails, another workstation rotated at the same moment. Keep our new access token, which is valid for an hour regardless, and leave the database value alone. The next refresh reads it.
  5. Updates "Refresh Token Expires" when it changed by more than a day, to avoid a write per refresh.
  6. Only if a row changed, signals other workstations with `Signalods.SetInvalid(InvalidType.Programs)` and refreshes this process's cache with `Cache.Refresh(InvalidType.Programs)`. `DataValid` lives in `WpfControlsOD`, which `OpenDentBusiness` can't reference, so `QboTokenManager` can't call it. (Alternatively, return a "changed" flag and let the form call `DataValid.SetInvalid`.) The cache refresh matters less than it looks, because step 1 always reads the database.
- **`invalid_grant` on refresh:**
  - Re-read the row once. If it differs from the value just used, another workstation rotated it, so retry once with the new value.
  - Otherwise set `NeedsReconnect`. Throw `QboAuthException`, whose neutral message is "QuickBooks Online needs to be reconnected in Accounting > Setup > QuickBooks Online."
- **API calls** go through `QboApiClient.Send(request)`. On a **401**, invalidate the token, refresh once and retry once.
- **Load.** Refreshes happen only when someone uses the feature: one per workstation per hour of use. That meets Intuit's "always use the latest value" rule ([FAQ](https://developer.intuit.com/app/developer/qbo/docs/develop/authentication-and-authorization/faq)) without a background thread.
- **Expiry warning.** The setup form shows "Connection valid until <date>" from "Refresh Token Expires". `FormDepositEdit` warns once per session when less than 14 days remain: the five-year cap, or 100 days unused.

### 3.5 Errors and offline behaviour

| Condition | Detected by | Behaviour |
|---|---|---|
| Not configured (blank client ID, secret, refresh token or realm) | `QboSettings.Load()` | Send QBO: "QuickBooks Online is not connected. Connect it in Accounting > Setup > QuickBooks Online." No network call |
| Migrated Open Dental values (`Decrypt` returns false for a non-blank value) | `QboSettings.Load()` | Treated as not connected. The setup form says "Saved connection is from another installation and cannot be used. Click Connect." (1.3 item 4, 3.9) |
| Access token expired | 401 | Transparent: refresh once, retry once |
| Refresh token revoked, expired or rotated away | `invalid_grant` (400) | Section 3.4, then `NeedsReconnect`, shown once per session |
| Wrong environment or keys | `invalid_client` (401 from the token endpoint) | "Intuit rejected the client ID or secret. Check that the keys match the selected environment." |
| Validation fault on deposit (closed period, inactive account, bad class) | 400 `ValidationFault` | Show Intuit's `Message` and `Detail`. The existing "create the deposit locally anyway?" prompt stays |
| Throttled | 429 | Interactive calls don't wait 60 s: "QuickBooks Online is busy. Try again in a minute." |
| Intuit 5xx | 500/502/503 | Retry twice (1 s, then 3 s). For the deposit POST, retry only with the **same `requestid`** |
| Offline (DNS, refused, TLS, timeout) | `HttpRequestException` / `TaskCanceledException` | "QuickBooks Online could not be reached." Then the existing local-save prompt. A deposit saved locally keeps `IsSentToQuickBooksOnline=0`, and **Send QBO** later sends it with a new `requestid` |
| Response lost after a successful POST | timeout on the POST | Retry with the same `requestid`. Intuit returns the original deposit, so there is no duplicate (1.3 item 2) |

- **Timeouts:** 30 s per call. Use `ConfigureAwait(false)`. The UI shows a wait cursor, as it does today, and calls block only inside the existing modal flow.
- **Offline is harmless.** Deposits, payments and every core module work without QuickBooks Online. Only Send QBO and the setup lists need the network (ground rule 5).
- **Logging** (`Logger`, category `QuickBooksOnline`): endpoint, HTTP status, Fault `code` and `intuit_tid`. **Never** tokens, codes, the secret, memos or amounts.

### 3.6 Hosts contacted, and no HQ

- `appcenter.intuit.com`: the browser only.
- `oauth.platform.intuit.com`: token exchange and refresh.
- `developer.api.intuit.com`: revoke.
- `quickbooks.api.intuit.com` or `sandbox-quickbooks.api.intuit.com`: Accounting API.
- In loopback mode, the workstation's own `localhost:<port>`. In paste or relay mode, the office's own redirect host (browser only).

Nothing else is contacted: no `opendental.com`, no `IWebServiceMainHQ`, no `BuildOAuthUrl`. `tools/Test-NoHqHosts.ps1` keeps passing. TLS 1.2 is already enabled: `CodeBase/Utilities/ODInitialize.cs` ORs `Tls12` into `ServicePointManager.SecurityProtocol`. `Introspection.QuickBooksOnlineSandboxUrl` stays as an optional API base URL override (for a mock server). The other two Introspection entries become unused (4.4).

### 3.7 Vendor neutrality (ground rule 7) and gating (ground rule 6)

- **Off by default.** The program row stays `Enabled=0`, and `AccountingSoftware` stays `OpenDental` (0).
- **Existing gates stay:**
  - the setup menu needs the program enabled
  - the Manage - General combo refuses QuickBooks Online unless enabled
  - Send QBO shows only when it is the deposit software
- **Setup wording is functional:** "Sends deposit slips to a QuickBooks Online company using your own Intuit developer app. An internet connection is required. The deposit memo, amounts and date are sent to Intuit." There are no logos, sign-up links or promotional text.
- **No help links.** The setup form links to nothing except, optionally, the Intuit developer portal URL in a tooltip for the client ID box.

### 3.8 FormQuickBooksOnlineSetup (new layout)

- **Connection group:**
  - Environment (combo)
  - Client ID
  - Client Secret: masked, with a "Change..." button, and needs `ManageHighSecurityProgProperties`
  - Redirect URI
  - Relay port (optional)
  - Status label: "Connected to <Company Name> (valid until <date>)", "Not connected" or "Needs reconnecting"
  - **Connect... / Reconnect...** and **Disconnect** buttons
- **The masked "Realm ID" and "Refresh Token" boxes are removed.** Tokens are never shown.
- **Account and class lists** keep today's add and remove UI. Display text uses `FullyQualifiedName`.
- **Loading lists** runs on a background task with a "Loading from QuickBooks Online..." label, instead of blocking `Load`.
- **Save** writes the new rows; the secret is written only when changed. It keeps the "set it as your Deposit Software?" prompt.

### 3.9 Data migrated from Open Dental

On a restored `fox-db` copy:

- "Refresh Token" and "Realm ID" hold Open Dental-encrypted values, issued to **Open Dental's** client ID. `Decrypt` fails, so the status is "not connected" (3.5). Even decrypted, they would be useless with the office's client ID.
- "Deposit Accounts", "Income Accounts" and "Class Refs" are plain text and refer to the **same QuickBooks Online company's** IDs. They **stay valid** when the office reconnects the same company with its own app. The realm check in 3.2 step 6 blanks them only if the company differs, and here no realm ID was readable.
  - So on the first connect after migration the lists are **kept**.
  - The display names may be updated to `FullyQualifiedName` by matching IDs.
- **After connecting its own app,** the office disconnects Open Dental's app inside QuickBooks Online: Settings > Apps > Connected or My apps. That revokes Open Dental's tokens to the company.
- Deposits already flagged `IsSentToQuickBooksOnline` stay flagged.

## 4. Code changes

### 4.1 Library choice

| | **A. In-repo REST client (recommended)** | B. NuGet `IppDotNetSdkForQuickBooksApiV3` 14.7.x |
|---|---|---|
| What | `QboOAuth` and `QboApiClient` on `HttpClient` and Newtonsoft.Json 12.0.3 (already in the repo): 4 OAuth calls, 3 queries, 1 read, 1 create. About 500 to 700 lines with DTOs | Intuit's SDK: `OAuth2Client` plus `DataService` and `QueryService` |
| Dependencies | None new. Delete the 14 `Intuit.Ipp.*` DLLs, `IppDotNetSdkForQuickBooksApiV3.dll` and the 13 `Serilog*` DLLs from `Required dlls`. Also delete the 27 `Intuit.Ipp.*` and `Serilog*` references in `OpenDental.csproj`; `IppDotNetSdkForQuickBooksApiV3.dll` isn't referenced by any project | The latest package (14.7.1.6, Sept 2026) targets net461, net472 and netstandard2.0. It needs **Newtonsoft.Json 13.0.2 or newer**, the Serilog set and Microsoft.Extensions.Configuration 2.2 ([NuGet](https://www.nuget.org/packages/IppDotNetSdkForQuickBooksApiV3)). That means a repo-wide Newtonsoft upgrade from 12.0.3, about 30 new DLLs, and binding redirects |
| License | n/a | Apache-2.0: allowed, needs a NOTICE entry |
| Upside | Small, auditable, full control of timeouts, logging redaction, `requestid` and CAS refresh | Intuit maintains the entity models |
| Downside | We own the DTOs for four entities | Heavy dependency for four calls. Its logging (Serilog) must be checked for token leaks |

**Choose A.** The 14.4 DLLs shipped today exist only because Bridges.dll pulled them in. After this change no in-repo code references them, apart from the stray `using Intuit.Ipp.Data;` in `OpenDental/Forms/FormSmsTextMessaging.cs` and `OpenDental/InternalTools/Phones/FormMapAreaContainerEdit.cs`. Delete those usings. Note that `docs/third-party-binaries-audit.md` currently says the NuGet SDK would back the replacement. Choosing A means updating those rows (4.2).

### 4.2 File-by-file changes

| File | Members | Change |
|---|---|---|
| **New** `OpenDentBusiness/Bridges/QuickBooksOnline/QuickBooksOnline.cs` (namespace `OpenDentBusiness.Qbo`) | static facade, same names as Bridges (4.3) | Re-implemented surface. It delegates to `QboTokenManager` and `QboApiClient` |
| **New** `.../QuickBooksOnlineToken.cs`, `QuickBooksOnlineAccount.cs`, `QuickBooksOnlineClassRef.cs`, `QuickBooksOnlineDepositResponse.cs` | data types (4.3) | Same public members. Add `FullyQualifiedName` to the account and class types |
| **New** `.../QboOAuth.cs` | `BuildAuthorizeUrl(settings, state)`, `ExchangeCode(settings, code)`, `Refresh(settings, refreshToken)`, `Revoke(settings, refreshToken)` | Section 2.3. Returns `QboTokenResult {AccessToken, ExpiresUtc, RefreshToken, RefreshExpiresUtc}` |
| **New** `.../QboApiClient.cs` | `Query<T>(entity, where)`, `GetCompanyInfo()`, `CreateDeposit(dto, requestId)`, `Send(...)` | Sections 2.4 and 3.5. Base URL chosen by Environment, or `Introspection.GetOverride(QuickBooksOnlineSandboxUrl, ...)` |
| **New** `.../QboTokenManager.cs` | `GetAccessToken()`, `Invalidate()`, `NeedsReconnect` | Section 3.4 |
| **New** `.../QboSettings.cs` | PropertyDesc constants, `EnsurePropertyRows()`, `Load()`, `SaveClientSettings(...)`, `SaveConnection(QboTokenResult, realmId, companyName)`, `Disconnect()`, `IsConnected`, `HasUnreadableLegacyValues` | Section 3.3 |
| **New** `.../QboLoopbackListener.cs`, `QboRedirectParser.cs` | `Start(Uri)`, `WaitForCallback(state, CancellationToken)`; `Parse(string)` | Section 3.2 |
| **New** `.../QboException.cs` | `QboAuthException`, `QboApiException {HttpStatus, FaultType, Code, IntuitTid}` | `ODException` subclasses |
| `OpenDentBusiness/Data Interface/ProgramProperties.cs` | new `UpdateValueIfUnchanged(long, string, string)`; `InsertIfMissing` (shared). Reuse the existing `GetValFromDb(long, string)` for the re-read | Section 3.4. Middle Tier-safe (`Meth.GetBool` for the CAS) |
| same | `GetQuickBooksOnlineEntityIdByName`, `GetQuickBooksOnlineEntityNames` | Kept. Add `GetQuickBooksOnlineEntityNameById` for ID-based matching |
| `OpenDentBusiness/OpenDentBusiness.csproj` | compile items | Add the new files |
| `OpenDental/Forms/FormQuickBooksOnlineSetup.cs` and `.designer.cs` | `using Bridges` becomes `using OpenDentBusiness.Qbo`; `butAuthenticate_Click`, `SaveTokensAndRealmId`, `FillTokenFields`, `FormQuickBooksOnlineSetup_Load`, `GetAccountsAndClassRefsFromQuickBooksOnline` | Sections 3.2 and 3.8. Token saving moves into `QboTokenManager`, so `SaveTokensAndRealmId` becomes `QboSettings.SaveConnection`. Add the Connection group and the Disconnect button |
| `OpenDental/Forms/FormQuickBooksOnlineAuthorization.cs` and `.designer.cs` | whole form | Paste box plus optional separate code and realm boxes, a loopback "waiting" state, and a `state` check. Exposes `QboAuthorizationResult` |
| `OpenDental/Forms/FormDepositEdit.cs` | `using Bridges`; `FormDepositEdit_Load` QBO branch; `CreateDepositQBO`; `SaveTokens`; field `_programPropertyQboAccessToken` | Swap the using. Load reads `QboSettings.Load()`. `CreateDepositQBO` creates **one `requestid` per call** and passes it on every retry. Remove the manual refresh and `SaveTokens`, which `QboTokenManager` now handles. Remove the dead field |
| `OpenDental/Forms/FormQBAccountSelect.cs` | `FillQuickBooksOnlineAccounts` | No logic change. Names shown are now fully qualified |
| `OpenDentBusiness/WebServices/WebServiceMainHQMockDemo.cs` | `case OAuthApplicationNames.QuickBooksOnline` | Delete the case and its comment. The default branch then covers it |
| `OpenDentBusiness/Db/Enumerations.cs` | `OAuthApplicationNames.QuickBooksOnline` (ordinal 2) | **Keep** to preserve ordinals. Summary: "Unused; QuickBooks Online authorizes locally" |
| `OpenDentBusiness/Misc/Introspection.cs` | `QuickBooksOnlineSandboxEnvironment`, `QuickBooksOnlineSandboxClientIdAndSecret` | Keep the members, since the JSON is keyed by name and unknown keys are skipped anyway. Mark them unused. `QuickBooksOnlineSandboxUrl` becomes the API base override |
| `OpenDental/OpenDental.csproj` | `Intuit.Ipp.*` (14) and `Serilog*` (13) references | Delete (option A), along with `Required dlls/Intuit.Ipp.*.dll`, `IppDotNetSdkForQuickBooksApiV3.dll` and `Serilog*.dll`. **Only after** confirming that the remaining Bridges.dll types (Microsoft 365, USPS) don't load them. They shouldn't, since .NET loads references lazily per method, but check with a smoke test of those features (7.2) |
| `OpenDental/Forms/FormSmsTextMessaging.cs`, `OpenDental/InternalTools/Phones/FormMapAreaContainerEdit.cs` | `using Intuit.Ipp.Data;` | Delete (unused) |
| `docs/third-party-binaries-audit.md` | Intuit and Serilog rows | Mark them "removed: replaced by in-repo client" |
| **New** `docs/integrations/qbo-redirect.html` | static relay or copy page | Section 3.2 step 4. Self-contained, no external assets |
| Tests: `OpenDentBusiness.Tests` (or `NoHqTests`) | new | Section 6.4 |

### 4.3 Prebuilt Bridges.dll types to re-implement

This is the public surface this repo calls, taken from `FormQuickBooksOnlineSetup.cs` and `FormDepositEdit.cs`. Re-implement it in `OpenDentBusiness.Qbo`. **Remove `using Bridges;` from both forms**: Bridges.dll still exists for other vendors, and with both namespaces imported the same simple names would be ambiguous.

| Type | Members called | Re-implementation notes |
|---|---|---|
| `QuickBooksOnlineToken` | `ctor(string accessToken, string refreshToken)`; `AccessToken {get;set;}`; `RefreshToken {get;set;}`; `RequestException {get;}` | Kept as a thin carrier for compatibility. Real state lives in `QboTokenManager` |
| `QuickBooksOnlineAccount` | `ctor(string name, string id)`; `Name`; `Id`; `RequestException`; `static List<QuickBooksOnlineAccount> StringAccountsToList(string)`; `static string ListAccountsToString(List<QuickBooksOnlineAccount>)` | Format: `Name,Id` pairs joined by `\|`, matching `ProgramProperties.GetQuickBooksOnlineEntity*`. A blank string gives an empty list. Reference equality is enough (the forms use `Contains` on the same instances). Add `FullyQualifiedName` |
| `QuickBooksOnlineClassRef` | `ctor(string name, string id)`; `Name`; `Id`; `RequestException`; `static StringClassRefsToList(string)`; `static ListClassRefsToString(List<>)` | Same format |
| `QuickBooksOnlineDepositResponse` | `RequestException`; `bool IsClassRefNull()` | `IsClassRefNull()` is true when a ClassRef was sent but any returned line lacks `DepositLineDetail.ClassRef`. Add `DepositId` |
| `QuickBooksOnline` (static) | `GetQuickBooksOnlineAuthorizationUrl()` | **Changed:** becomes `BeginAuthorization()`, returning `QboAuthRequest {Url, State, RedirectUri, UsesLoopback}`. The old name is removed, because a URL alone can't carry the state check |
| | `MakeAccessTokenRequest(string authCode)` → `QuickBooksOnlineToken` | **Changed:** `CompleteAuthorization(QboAuthRequest, QboAuthorizationResult)` checks state, exchanges, fetches CompanyInfo and saves. It returns the token with `RequestException` set on failure, keeping that style |
| | `RefreshTokens(QuickBooksOnlineToken)` → `QuickBooksOnlineToken` | Kept. Calls `QboTokenManager` (3.4), and **persists the rotated token itself** |
| | `RefreshTokensIfNeeded(Exception, QuickBooksOnlineToken)` → `bool` | Kept. Returns true and refreshes when the exception is an expired-token 401. The API methods already retry once internally, so callers rarely see true |
| | `GetDepositAccounts(string realmId, QuickBooksOnlineToken)`, `GetIncomeAccounts(...)` → `List<QuickBooksOnlineAccount>` | Kept. On failure they return **a one-element list whose `RequestException` is set**, which the forms' convention expects. Better: switch the forms to try/catch (4.2) and have these throw. Pick one style and do it in the same change |
| | `GetClassRefs(string realmId, QuickBooksOnlineToken)` → `List<QuickBooksOnlineClassRef>` | Same |
| | `CreateDeposit(string realmId, QuickBooksOnlineToken, QuickBooksOnlineAccount deposit, QuickBooksOnlineAccount income, QuickBooksOnlineClassRef classRef, List<decimal> amounts, string memo, DateTime date)` → `QuickBooksOnlineDepositResponse` | Kept, plus an optional `string requestId=null` (a fresh GUID when null). One `Line` per amount, rounded to 2 dp. `classRef==null` means no ClassRef. `memo` goes to `PrivateNote`, truncated to 4000 characters |

Bridges.dll's behaviour wasn't inspected beyond these calls. The account-type filters, the `Name` vs `FullyQualifiedName` choice and the line layout are choices made here (open questions 2 and 3).

### 4.4 "Not available" stubs that are replaced or retired

| Stub | Caller today | After |
|---|---|---|
| `WebServiceMainHQNoHq.BuildOAuthUrl(regKey, "QuickBooksOnline")` | Prebuilt `Bridges.QuickBooksOnline` (inside Bridges.dll) | No caller remains in this flow. The in-repo facade reads the office's client ID and secret from `QboSettings`. `BuildOAuthUrl` itself stays for vendors not yet migrated. `NoHqTests.WebServiceMainHQ_VendorMethodsSayDirectIntegrationIsNeeded` still passes |
| `WebServiceMainHQMockDemo.BuildOAuthUrl` `QuickBooksOnline` case | Demo mode | Deleted (4.2) |
| Introspection `QuickBooksOnlineSandboxClientIdAndSecret`, `QuickBooksOnlineSandboxEnvironment` | Bridges.dll only | Superseded by the "Client ID", "Client Secret" and "Environment" properties |

### 4.5 Not changed

- the `deposit` table and `IsSentToQuickBooksOnline`
- `PrefName.AccountingSoftware` and the `AccountingSoftware` enum
- the property string format `Name,Id|Name,Id`
- QuickBooks Desktop (`WpfControlsOD/Bridges/QuickBooks.cs`, `Interop.QBFC10.dll`), which the binaries audit covers separately
- `Accounts.DepositsLinked`

## 5. Credentials the office must register

### 5.1 Developer account and app

1. **Create an Intuit Developer account** at <https://developer.intuit.com/> with an Intuit login the **office** owns, not a staff member's personal one. Record the owner.
2. **Create an app:** Dashboard > Create an app > QuickBooks Online and Payments ([dashboard](https://developer.intuit.com/app/developer/dashboard)). Name it neutrally, for example "<Office> deposits". Select **only** the scope `com.intuit.quickbooks.accounting`.
3. **Development keys:**
   - Keys & credentials > Development: copy the **Client ID** and **Client Secret**.
   - Add the redirect URI `http://localhost:8765/callback` (any free port; it must match the setup form exactly).

### 5.2 Sandbox company

4. Intuit gives each developer account a US sandbox company, and more can be added ([sandboxes](https://developer.intuit.com/app/developer/qbo/docs/develop/sandboxes)).
   - In the sandbox company, turn on **class tracking**: Settings > Account and settings > Advanced > Categories.
   - Create a few classes.
   - Confirm there are Bank and Income accounts.

### 5.3 Production keys

5. **Prepare the URLs the questionnaire asks for,** on a domain the office controls (its website):
   - host domain
   - launch URL (any page)
   - disconnect URL (a page saying the connection was removed)
   - **Reconnect URL**, added to the portal in 2026 and possibly mandatory (2.6) (a page saying "Reconnect in the practice software: Accounting > Setup > QuickBooks Online")
   - EULA URL and privacy policy URL. A short internal-use EULA and privacy notice are enough for a private, single-company app, but the office decides the wording.
6. **The redirect page:** an HTTPS URL on the same domain, for example `https://<office-domain>/qbo/callback`.
   - Either publish `docs/integrations/qbo-redirect.html` there (3.2 step 4), or use any page; the user copies the address bar either way.
   - Avoid third-party URL shorteners or hosts the office doesn't control. The URL carries the authorization code, which is short-lived and useless without the secret, but still shouldn't sit in someone else's logs.
7. **Complete the app assessment questionnaire** under Production Settings: app details plus compliance ([walkthrough](https://docs.mydbsync.com/cloud-workflow/connectors/quickbooks-online/create-a-private-quickbooks-online-qbo-app-on-intuit-developer-portal)).
   - Answer that the app is private, used by one company, and stores tokens in the office's own on-premises database.
   - Regulated industries: answer truthfully. It's a dental office, but no PHI is sent to Intuit (2.4). Intuit's questions may change.
8. **Production keys** appear after approval. Copy the production Client ID and Secret. Register the HTTPS redirect URI from step 6 under Production.
9. **App Partner Program:** stay on the free **Builder** tier. Nothing to buy ([Partner FAQ](https://developer.intuit.com/app/developer/qbo/docs/get-started/partner-faq)).

### 5.4 Legal and compliance (office decisions)

10. **BAA:** ask Intuit in writing whether a BAA is available. If none is (expected), record the decision that only non-PHI deposit data goes to QuickBooks Online, and tell staff not to put patient names in deposit memos.
11. **Developer Terms:** whoever registers the app accepts Intuit's Developer Terms and Exhibit A for the office. Keep the EULA and privacy URLs live while the app is in use.

### 5.5 In the practice software

12. In Program Links, enable QuickBooks Online. Then in Accounting > Setup > QuickBooks Online:
    - pick the Environment
    - enter the Client ID, Client Secret and Redirect URI
    - click **Connect**
    - pick the accounts and classes
    - Save
13. In Setup > Preferences > Manage - General, set Deposit Software to QuickBooks Online.
14. **If migrating from Open Dental,** disconnect Open Dental's app in QuickBooks Online: Settings > Apps (3.9).

## 6. Test plan

### 6.1 Sandbox setup

- Use the Development keys, the sandbox company (5.2), and a loopback redirect on port 8765.
- Use a dev database (`tools/New-DevDatabase.ps1`) with the `QuickBooksOnline` program row present (it ships disabled).
- Use the [OAuth 2.0 Playground](https://developer.intuit.com/app/developer/playground) only to compare responses, never as the app's redirect.

### 6.2 Manual checks

1. **Off by default.** On a fresh database the program is disabled, the setup menu says it must be enabled first, and Deposit Software can't be set to QuickBooks Online.
2. **Validation.** A production environment with an `http://localhost` redirect is rejected with the HTTPS message. A blank secret is rejected.
3. **Loopback connect (sandbox):**
   - Connect opens the browser, consent completes, and the tab shows the local "done" page.
   - The status shows the sandbox company name and an expiry date.
   - The database holds an `osd1$` refresh token and realm ID, and no access token row.
4. **State check.** Paste a callback URL from an earlier attempt: it is rejected.
5. **Paste connect.** Set the redirect to an HTTPS URI registered under Development (also allowed). Approve, paste the address bar, and it connects.
6. **Lists.**
   - Deposit accounts show Bank and Other Current Asset accounts, and income shows Income and Other Income. Sub-accounts show as `Parent:Child`.
   - Classes appear.
   - Names with `,` or `|` are refused as today.
   - After making an account inactive in QuickBooks Online, it shows "(No longer in QBO)".
7. **Send deposit.** Select 3 payments and Send QBO. In QuickBooks Online, the deposit has 3 lines, the date, the memo, the deposit-to account, the income account and the class. `IsSentToQuickBooksOnline=1`, and a security log entry exists.
8. **Class tracking off.** Turn class tracking off in the sandbox and send with a class. The "did not use the Class Ref" message shows.
9. **Expired access token.** Wait more than 60 minutes, or clear the cache through the debugger, and send. It refreshes silently, and the token row changes only when Intuit rotated the token.
10. **Two workstations.**
    - Run two clients against one database. Force a refresh on A (set the clock forward 25 h on a VM, or wait), then send from B. B succeeds after re-reading the token (3.4).
    - Then race a forced refresh on both. Neither ends up needing to reconnect.
11. **Duplicate guard.** Make the client drop the first POST response (a debugger breakpoint after send, then a timeout). The retry with the same `requestid` returns the same Deposit Id, and there is no second deposit in QuickBooks Online.
12. **Offline.** Unplug the network and save a new deposit. You get the "could not be reached" message and the local-save prompt. Saved locally, the deposit later sends with Send QBO.
13. **Revoked.** Disconnect the app inside QuickBooks Online, then send. You get one "needs to be reconnected" message, and core modules are unaffected.
14. **Disconnect button.** The revoke call is made and the rows are blanked. Deposit Software QuickBooks Online shows the warning.
15. **Company switch.** Connect a second sandbox company. The confirmation shows and the lists are cleared.
16. **Environment switch.** The confirmation shows, and the tokens are blanked.
17. **Migration.** Restore a `fox-db` copy:
    - The status says the saved connection can't be used, and the lists are kept.
    - Connecting the same company with the office's app keeps the lists, and the IDs still resolve on a test send (to a sandbox only if the IDs come from sandbox; otherwise do this in the production pilot).
18. **Permissions.** A user without `ManageHighSecurityProgProperties` can't view or change the secret.
19. **No HQ.** `tools/Test-NoHqHosts.ps1` passes. A network trace (Fiddler) during connect and send shows only the hosts in 3.6.
20. **Logs.** Search the logs for the access token, refresh token, secret and memo text: no hits.
21. **Remaining Bridges features.** With the Intuit and Serilog DLLs deleted, smoke-test Microsoft 365 email and USPS address validation (whatever state they are in) to confirm Bridges.dll still loads.

### 6.3 Production pilot

- Once production keys are approved: switch Environment to Production and connect with the paste flow (or the relay page).
- Send one small real deposit, check it in QuickBooks Online, and delete or void it there if it was only a test.
- Watch for a week for `invalid_grant` across workstations.

### 6.4 Automated tests (no network)

With a fake `HttpMessageHandler`:

- token response parsing, including a missing `x_refresh_token_expires_in` and an extra expiry field
- `QboRedirectParser`: full URL, query only, URL-encoded values, missing `realmId`, `error=access_denied`
- the deposit JSON shape: lines, rounding, no ClassRef when null, memo truncation
- `IsClassRefNull`
- Fault parsing to `QboApiException`
- one refresh and retry on 401, and no infinite loop on a second 401
- the same `requestid` on retries
- the CAS path: an unchanged row updates; a changed row isn't overwritten and the re-read retry runs
- `StringAccountsToList` / `ListAccountsToString` round trip, including a blank string
- the production redirect validator (https only; no localhost or IP)

## 7. Size, risks and open questions

### 7.1 Size

**M, about 5 to 7 developer days:**

- REST client, DTOs and OAuth: 1.5 days
- token manager, CAS and ProgramProperties helpers: 1 day
- loopback listener, redirect parser and relay page: 1 day
- setup and authorization form rework: 1.5 days
- FormDepositEdit changes, DLL removal and tests: 1 day

The office's Intuit registration and production questionnaire run in parallel. Expect days to weeks of Intuit review time, which is outside our control.

### 7.2 Risks

1. **The production redirect rule.** A plain loopback flow is impossible with production keys. The paste flow is clumsy but robust, and the relay page needs the office to host one static file. If Intuit ever blocks client-side relays or tightens rules (for example, domain verification), the paste flow still works.
2. **The questionnaire burden for a private app.** It needs a public website, EULA, privacy policy, and launch, disconnect and reconnect URLs, and Intuit may change the questions. If the office has no website, it can't get production keys. Feasibility then falls back to "configured but disabled".
3. **Refresh-token rotation across workstations.** CAS plus re-read covers it. A residual double rotation in the same second is handled by the `invalid_grant` re-read. If Intuit invalidates the previous value instantly at rotation and two workstations rotate at once, one may need a second retry. Log it and watch the pilot.
4. **The five-year cap and 100-day idle expiry.** An office that stops sending deposits for over 100 days must reconnect. The warning in 3.4 helps.
5. **`osd1$` is obfuscation.** The client secret and refresh token are recoverable from a database copy, and they give read and write access to the books. Mitigations: high-security rows, database access control, and rotating the secret in the Intuit portal plus Disconnect if a copy leaks.
6. **PHI.** No BAA. The memo is free text, so staff could type patient names. The setup text and a field hint say not to. Optional: warn when the memo contains a name that matches a patient on the deposit (cheap check, open question 5).
7. **Pricing changes.** Intuit could meter Core calls or shrink Builder. Our call volume is tiny, but blocked CorePlus calls would break the setup lists, not Send QBO.
8. **Deleting the Intuit and Serilog DLLs while Bridges.dll remains.** Bridges.dll references them. Lazy loading should make this safe, but if a remaining Bridges type touches them during static initialization, Microsoft 365 email or USPS would fail. Mitigation: check 21 in 6.2, or delete them in the same change that removes Bridges.dll.
9. **The class-tracking setting.** QuickBooks Online may ignore ClassRef silently. The existing message covers it.

### 7.3 Open questions

1. **Per-office app or project app?** Recommended: per office (3.1). A project-wide app would need a hosted redirect, a public EULA and privacy policy, and shared-secret handling. Owner decision.
2. **Account types.** Which ones did Bridges.dll offer for deposit-to and income? Proposed: Bank plus Other Current Asset (so Undeposited Funds is possible), and Income plus Other Income. Check against the office's current picks in `fox-db`: look up their `AccountType` after connecting.
3. **`Name` vs `FullyQualifiedName`.** Switching changes what is shown and stored for sub-accounts. Existing stored names may be plain `Name`. Re-sync names by ID when the lists load (3.9).
4. **Relay page.** Ship `qbo-redirect.html` in v1, or paste only? Recommended: ship it. It is small and optional.
5. **Memo PHI check** (7.2 item 6): add it, or rely on staff guidance?
6. **The new refresh-token expiry field name** from Intuit's 2026 change. Confirm in the portal docs, or from a live sandbox response, during implementation.
7. **Multiple companies.** The rows are `ClinicNum=0`, so there is one company per database. Should clinics be able to post to different QuickBooks Online companies? Not today; out of scope.
8. **Shared helpers.** `ProgramProperties.InsertIfMissing`, the loopback listener and the redirect parser could be shared with the Google and Microsoft 365 specs. Agree on them once (also PostcardMania 7.3.7 and Dropbox 3.2).

## References

Intuit developer documentation (pages render in the browser):

- OAuth 2.0: https://developer.intuit.com/app/developer/qbo/docs/develop/authentication-and-authorization/oauth-2.0
- Set redirect URIs: https://developer.intuit.com/app/developer/qbo/docs/develop/authentication-and-authorization/set-redirect-uri
- Authorization FAQ (token lifetimes, rotation): https://developer.intuit.com/app/developer/qbo/docs/develop/authentication-and-authorization/faq
- Discovery, production: https://developer.api.intuit.com/.well-known/openid_configuration
- Discovery, sandbox: https://developer.intuit.com/.well-known/openid_sandbox_configuration
- Limits and throttles: https://developer.intuit.com/app/developer/qbo/docs/learn/limits-and-throttles · REST API features (throttles): https://developer.intuit.com/app/developer/qbo/docs/learn/rest-api-features
- Data queries (`select *` only, paging): https://developer.intuit.com/app/developer/qbo/docs/learn/explore-the-quickbooks-online-api/data-queries
- Release notes (Reconnect URL, refresh token lifetime): https://developer.intuit.com/app/developer/qbo/docs/release-notes/general-release-notes
- Basic field definitions (`requestid`): https://developer.intuit.com/app/developer/qbo/docs/learn/learn-basic-field-definitions
- Entities: Deposit https://developer.intuit.com/app/developer/qbo/docs/api/accounting/all-entities/deposit · Account https://developer.intuit.com/app/developer/qbo/docs/api/accounting/all-entities/account · Class https://developer.intuit.com/app/developer/qbo/docs/api/accounting/all-entities/class
- Sandboxes: https://developer.intuit.com/app/developer/qbo/docs/develop/sandboxes
- OAuth Playground: https://developer.intuit.com/app/developer/playground
- App Partner Program FAQ: https://developer.intuit.com/app/developer/qbo/docs/get-started/partner-faq
- Developer Terms of Service: https://developer.intuit.com/app/developer/qbo/docs/legal-agreements/intuit-terms-of-service-for-intuit-developer-services
- Exhibit A: https://developer.intuit.com/app/developer/qbo/docs/legal-agreements/intuit-terms-of-service-for-intuit-developer-services/intuit-data-services-quickbooks

Intuit blogs, GitHub and packages:

- Refresh token policy change (12 November 2025; five-year maximum, Reconnect URL): https://blogs.intuit.com/2025/11/12/important-changes-to-refresh-token-policy (redirects to https://medium.com/intuitdev/important-changes-to-refresh-token-policy-8443779d40db)
- Minor version 75 baseline: https://blogs.intuit.com/?p=34635
- Request IDs: https://blogs.a.intuit.com/2015/04/06/15346/
- Redirect URI exact match: https://blogs.intuit.com/2017/10/31/oauth-2-0-integration-tips-careful-entering-redirect-uri/
- Intuit CLI (sandbox localhost callback; production localhost refused): https://github.com/intuit/intuit-cli-for-quickbooks
- OAuth2 .NET sample without SDK (scopes, keys): https://github.com/IntuitDeveloper/OAuth2-Dotnet-WithoutSDK
- Node OAuth client README (token fields, sandbox host): https://unpkg.com/intuit-oauth@4.2.5/README.md
- .NET SDK and license (Apache-2.0): https://github.com/intuit/QuickBooks-V3-DotNET-SDK · https://github.com/intuit/QuickBooks-V3-DotNET-SDK/blob/master/License.md
- NuGet package: https://www.nuget.org/packages/IppDotNetSdkForQuickBooksApiV3
- Developer forum, production localhost: https://help.developer.intuit.com/s/question/0D54R00007Bf8sfSAB/why-does-production-redirect-uri-not-allow-localhost

Third-party (secondary, use with care):

- Private app production keys walkthrough: https://docs.mydbsync.com/cloud-workflow/connectors/quickbooks-online/create-a-private-quickbooks-online-qbo-app-on-intuit-developer-portal
- App compliance: https://developers.apideck.com/connectors/quickbooks/docs/application_owner+app_compliance
- Partner program pricing: https://www.apideck.com/blog/quickbooks-api-pricing-and-the-intuit-app-partner-program
- Minor versions summary: https://docs.codat.io/updates/250219-qbo-minor-versions-update
- HIPAA / BAA: https://www.accountablehq.com/post/is-quickbooks-online-hipaa-compliant-short-answer-no-here-s-what-to-do-instead

Repository:

- `OpenDental/Forms/FormQuickBooksOnlineSetup.cs`, `FormQuickBooksOnlineAuthorization.cs`, `FormDepositEdit.cs`, `FormQBAccountSelect.cs`, `FormAccounting.cs`, `FormProgramLinks.cs`
- `OpenDentBusiness/Data Interface/ProgramProperties.cs` (QBO entity helpers), `Accounts.cs` (`DepositsLinked`)
- `OpenDentBusiness/Misc/ConvertDatabases6.cs` (`To20_4_5`), `ConvertDatabases7.cs` (Access Token removal, encryption)
- `OpenDentBusiness/WebServices/WebServiceMainHQNoHq.cs` (`BuildOAuthUrl`), `WebServiceMainHQMockDemo.cs` (client ID and secret comment)
- `OpenDentBusiness/Misc/Introspection.cs`, `OpenDentBusiness/Db/Enumerations.cs`
- `CDT/README.md` (`osd1$`), `docs/third-party-binaries-audit.md` (Intuit and Serilog rows), `D:/Dev/opendental-schema/hq-vendor-table.md`
