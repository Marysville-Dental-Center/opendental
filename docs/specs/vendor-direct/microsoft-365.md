# Microsoft 365 email: direct-to-vendor spec

Status: draft, 2026-10-18; fact-checked against the code and Microsoft's public docs the same day. Baseline: `main` at `6855026f5`. Vendor: Microsoft. APIs: **Microsoft identity platform** (OAuth 2.0 authorization code with PKCE, public client) and **Microsoft Graph v1.0** (Outlook mail). Both are public.

## Summary

- **What it is.** In Setup > Email, an email address can use "Microsoft" sign-in instead of an SMTP password. The program then:
  - sends that address's mail (patient email, statements, any `EmailMessages.SendEmail` caller) through Microsoft Graph
  - can download the mailbox's inbox into the Email Inbox
- **What Open Dental provided.** No HQ web method was involved: the hq table lists "none in repo". The prebuilt **`Bridges.dll`** (`MicrosoftApiConnector`, `MicrosoftTokenHelper`) did the whole flow with **Open Dental's own Microsoft Entra app registration**. Its client ID sits inside that binary. Bridges.dll handled:
  - the MSAL sign-in
  - the token cache
  - every Graph call
- **State on `main`: nothing is stubbed.** Four files (`FormEmailAddressEdit.cs`, `FormEmailMessageEdit.cs`, `EmailMessages.cs`, `SendEmails.cs`) still call the prebuilt Bridges.dll, so a sign-in today still runs under Open Dental's app registration. This is a live decoupling gap. `tools/Test-NoHqHosts.ps1` can't catch it, because only Microsoft hosts are contacted. Until the replacement lands, the Microsoft option should be hidden (section 3.8).
- **Direct design.** Each office registers **its own single-tenant public-client app** in its own Microsoft Entra tenant. There is no secret.
  - **Sign-in:** MSAL.NET in the system browser, with an `http://localhost` loopback redirect and PKCE.
  - **Per-mailbox storage:** MSAL's serialized token cache, which holds the refresh token, is encrypted with `CDT.Class1.Encrypt` (`osd1$`) into the existing `emailaddress.RefreshToken` TEXT column.
  - **Office-wide storage:** the client ID and tenant go in `programproperty` rows of a new runtime-inserted `program` row.
  - **API calls:** a small in-repo `HttpClient` client for the five Graph endpoints we use replaces the 10 MB Graph SDK 4.42.
  - **Bridges.dll types re-implemented:** `MicrosoftApiConnector` and `MicrosoftTokenHelper`.
- **Feasibility: public API.** There is no partner program, NDA or app review for a single-tenant app. The gates are on the office's side:
  - a Microsoft 365 business plan with Exchange Online
  - Microsoft's HIPAA BAA, which is in the Data Protection Addendum by default for covered entities
  - a tenant admin who can register the app and grant consent
- **Size: M**, about 6 to 8 days. Inbox polling is extra (open question 1).

## 1. What the integration does, and what Open Dental provided

### 1.1 What the office uses it for

- **Setup.** `OpenDental/Forms/FormEmailAddressEdit.cs` has three radio buttons: Password, Gmail and Microsoft.
  - Choosing **Microsoft** and clicking `butAuthMicrosoft` signs the user in.
  - The form then fills `textAccessTokenMicrosoft` and `textRefreshTokenMicrosoft`, sets `SMTPserver="smtp.outlook.com"` and `ServerPort=0` as placeholders, and offers "Download incoming emails".
  - `butClearTokensMicrosoft` signs out.
  - Save writes `emailaddress.AuthenticationType=2` (`OAuthType.Microsoft`), `AccessToken`, `RefreshToken` and `DownloadInbox`.
- **Sending.**
  - `EmailMessages.SendEmailUnsecure` (`OpenDentBusiness/Data Interface/EmailMessages.cs` ~900-920) first calls `MicrosoftApiConnector.GetProfile` as a token probe.
  - If the error text contains `InvalidAuthenticationToken`, it calls `RefreshMicrosoftToken` (1683-1708) and retries once.
  - `SendEmails.SendEmailOAuth` (`OpenDentBusiness/Email/SendEmails.cs` 206-219) then calls `MicrosoftApiConnector.SendEmail(username, accessToken, BasicEmailMessage)`.
  - Every sender goes through this path: the email window, statements, and anything else that calls `EmailMessages.SendEmail`.
- **Re-authentication on send.** `FormEmailMessageEdit.butSend_Click` (807-845) handles an address whose `RefreshToken` is blank:
  - It offers a Microsoft sign-in.
  - It checks that the account matches `EmailUsername`.
  - It then saves the new tokens.
- **Receiving.** `EmailMessages.RetrieveFromMicrosoftInbox` (1198-1245):
  - lists messages (`RetrieveMessages`, which returns `List<Microsoft.Graph.Message>`)
  - skips IDs already in `emailmessageuid`
  - downloads each message as MIME (`GetMIMEMessage`, which returns `MimeKit.MimeMessage`)
  - imports it with `ProcessRawEmailMessageIn`

  **`ReceiveFromInbox` has no caller in this repository.** In Open Dental the eConnector service polled inboxes on `PrefName.EmailInboxCheckInterval`, and that service is out of scope (eServices). So inbox download is unreachable today (open question 1).
- **Thinfinity/AppStream branches.** The three token call sites in the forms (`butAuthMicrosoft_Click`, `butClearTokensMicrosoft_Click`, the re-auth block in `butSend_Click`) branch on `ODEnvironment.IsCloudServer`, and `EmailMessages.RefreshMicrosoftToken` branches on `ODEnvironment.IsCloudInstance`. Those branches go through `ODCloudClient.GetMicrosoftAccessToken` / `MicrosoftSignOutUser`, part of the ODCloud browser shim. The catch block in `FormEmailMessageEdit.butSend_Click` (879-891) that clears the tokens on `InvalidAuthenticationToken` is also cloud-only: it is guarded by `ODEnvironment.IsCloudServer && ex.InnerException!=null`. The Graph calls themselves (`GetProfile`, `RetrieveMessages`, `GetMIMEMessage`, `SendEmail`) have no cloud branch. ODCloud is cut (`docs/third-party-binaries-audit.md`), so all of these branches are deleted, not re-implemented.

### 1.2 What Open Dental's side provided

| Piece | Where on `main` | What it did (inferred from how this repo calls it; the DLL was not inspected) | Now |
|---|---|---|---|
| Azure/Entra app registration | inside `Required dlls/Bridges.dll` | Open Dental's client ID for an MSAL **public client**. MSAL 4.47.1 is shipped next to it but referenced by no `.csproj`, so only Bridges.dll loads it | Still in use: the gap |
| Sign-in and refresh (one method) | `MicrosoftApiConnector.GetAccessToken(username, accountInfo)` returning `Task<MicrosoftTokenHelper>`, awaited inside `Task.Run(...).GetAwaiter().GetResult()` at all three call sites | The helper returns `AccessToken`, `AccountInfo`, `EmailAddress` and `ErrorMessage`. An empty `AccessToken` with no error means the user cancelled. The callers pass different second arguments: `FormEmailMessageEdit` (820) passes `""`; `FormEmailAddressEdit.butAuthMicrosoft_Click` (428) passes `textRefreshTokenMicrosoft.Text`, which is blank before the first sign-in and the stored cache afterwards (the button stays enabled once signed in); `EmailMessages.RefreshMicrosoftToken` (1699) passes `emailAddress.RefreshToken`. So the DLL most likely goes silent (MSAL `AcquireTokenSilent`) when given a cache and interactive (`AcquireTokenInteractive`) otherwise, possibly falling back to interactive when silent fails. That is an inference from the call sites only | Still in use |
| Token cache | `AccountInfo`, stored in `emailaddress.RefreshToken` | `ConvertDatabases7.To22_3_22` tests this value with `MiscUtils.Decrypt` (a fixed-key AES helper) and names the result `decryptedCache`, so it is most likely a **serialized MSAL token cache** encrypted with `MiscUtils`' fixed key (inferred; the DLL was not inspected) | Still in use |
| Sign-out | `SignOutUser(username, accountInfo)` returning `Task<MicrosoftTokenHelper>`, read with `.Result` **on the UI thread** (FormEmailAddressEdit 333) | Probably MSAL `RemoveAsync` on the cached account. The code comment says Microsoft needs its own sign-out step rather than just clearing the token | Still in use |
| Graph calls | `GetProfile`, `RetrieveMessages`, `GetMIMEMessage`, `SendEmail` (all synchronous) | Graph SDK 4.42 calls (`Required dlls/Microsoft.Graph.dll`). Errors surfaced as an exception whose `InnerException.Message` contains the Graph error code, which suggests a blocking wait on a Task | Still in use |
| Token storage | `emailaddress.AccessToken` varchar(2000), `RefreshToken` TEXT | The plain access token, plus the encrypted MSAL cache | Unchanged |

Consequences:

- **Every Microsoft sign-in today is consent to Open Dental Software's app.** The office's tenant ends up with an "Open Dental" enterprise application holding `Mail.Send` / `Mail.Read` grants. Section 5, step 11 removes it.
- **A database migrated from Open Dental** (Phase 5) carries caches issued to Open Dental's client ID and encrypted with `MiscUtils`' fixed key. MSAL wouldn't find those accounts under our client ID anyway. Ground rule 3 means we don't preserve them: the rows are treated as "needs re-authentication" (section 3.9).

### 1.3 Defects found while reading the code (fix in the same change)

1. **`ex.InnerException.Message.Contains("InvalidAuthenticationToken")`** appears unguarded in `EmailMessages.cs` (912 and 1207). It throws `NullReferenceException` when `InnerException` is null, for example on a DNS failure. Replace it with a typed exception (section 4.3). The same test in `FormEmailMessageEdit.cs` (882) is null-guarded and sits in the cloud-only branch, which is deleted (1.1). `SendEmails.SendEmailOAuth` (213-216) already checks for null.
2. **Access token stored in `varchar(2000)`.** Entra access tokens for Graph can be longer than that once group or role claims are added. The design stops persisting access tokens (section 3.3).
3. **The probe call doubles traffic.** `SendEmailUnsecure` calls `GetProfile` before every send, only to see whether the token still works. MSAL's silent call already returns a valid token or refreshes it, so the probe goes away.
4. **Two refreshers race.** `RefreshMicrosoftToken` compares the in-memory access token with the database copy. Two workstations refreshing at the same moment both write, and the last write wins. That is harmless, because Entra doesn't revoke the old refresh token ([refresh tokens](https://learn.microsoft.com/en-us/entra/identity-platform/refresh-tokens)), but it fires a `Signalods.SetInvalid(InvalidType.Email)` cache flush at every workstation on every refresh. Section 3.4 removes the signal.
5. **`TrimStart("MicrosoftId".ToCharArray())`** in `RetrieveFromMicrosoftInbox` (1216) strips any leading characters from the set {M, i, c, r, o, s, f, t, I, d}, not the prefix "MicrosoftId". IDs are stored without a prefix (1220), so today it is a no-op only because Graph IDs usually start with `A`. Remove it, or compare exactly (IDs are case-sensitive, 2.4).
6. **Sign-out blocks the UI thread on `.Result`** (FormEmailAddressEdit 333). The replacement must use `ConfigureAwait(false)` inside, or the caller must move to `Task.Run(...)` like the sign-in path, to avoid a WinForms deadlock.

## 2. The vendor's public API

### 2.1 Documentation status

Public and complete:

- Identity platform:
  - [Auth code flow protocol](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-auth-code-flow)
  - [refresh tokens](https://learn.microsoft.com/en-us/entra/identity-platform/refresh-tokens)
  - [desktop app configuration](https://learn.microsoft.com/en-us/entra/identity-platform/scenario-desktop-app-configuration)
- MSAL.NET:
  - [system browser](https://learn.microsoft.com/en-us/entra/msal/dotnet/acquiring-tokens/using-web-browsers)
  - [token cache serialization](https://learn.microsoft.com/en-us/entra/msal/dotnet/how-to/token-cache-serialization)
- Graph:
  - [sendMail](https://learn.microsoft.com/en-us/graph/api/user-sendmail?view=graph-rest-1.0)
  - [list messages](https://learn.microsoft.com/en-us/graph/api/user-list-messages?view=graph-rest-1.0)
  - [MIME of a message](https://learn.microsoft.com/en-us/graph/outlook-get-mime-message)
  - [large attachments](https://learn.microsoft.com/en-us/graph/outlook-large-attachments)
  - [immutable IDs](https://learn.microsoft.com/en-us/graph/outlook-immutable-id)
  - [throttling limits](https://learn.microsoft.com/en-us/graph/throttling-limits)
- Entra admin: [register an app](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app), [grant admin consent](https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/grant-admin-consent)
- Retirement notice: [SMTP AUTH Basic deprecation timeline](https://techcommunity.microsoft.com/blog/exchange/updated-exchange-online-smtp-auth-basic-authentication-deprecation-timeline/4489835)
- Exchange: [Exchange Online limits](https://learn.microsoft.com/en-us/office365/servicedescriptions/exchange-online-service-description/exchange-online-limits)
- Compliance: [HIPAA/HITECH offering](https://learn.microsoft.com/en-us/compliance/regulatory/offering-hipaa-hitech)

There is no partner agreement and no NDA documentation. There is no separate sandbox host: testing uses a separate test tenant (section 6.1).

### 2.2 Base URLs

| Use | URL | Notes |
|---|---|---|
| Authorize (system browser only) | `https://login.microsoftonline.com/{tenant}/oauth2/v2.0/authorize` | `{tenant}` is the office's tenant ID (recommended), `organizations`, `common` or `consumers` |
| Token | `POST https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token` | MSAL calls it. No `client_secret` for a public client |
| Graph | `https://graph.microsoft.com/v1.0` | `/me/...` only (delegated) |
| Attachment upload sessions | `https://outlook.office.com/...` (opaque `uploadUrl` returned by `createUploadSession`) | Only on the large-message path (2.4, 3.6). Pre-authenticated: send no bearer token, and use the URL exactly as returned |
| Loopback redirect | `http://localhost` (any port) | Registered once. MSAL picks a free port at runtime. The identity platform accepts any port for `localhost` on native apps ([MSAL browsers](https://learn.microsoft.com/en-us/entra/msal/dotnet/acquiring-tokens/using-web-browsers)) |
| Sandbox | none | Same hosts, with a test tenant and test mailboxes |
| National clouds | `login.microsoftonline.us`, `graph.microsoft.us`, … | Out of scope (open question 6) |

### 2.3 Auth flow, step by step (public client, PKCE, system browser)

MSAL.NET does steps 1-6. They are listed so the design and the error handling make sense.

1. **Build the public client.** `PublicClientApplicationBuilder.Create(clientId).WithAuthority("https://login.microsoftonline.com/{tenant}").WithRedirectUri("http://localhost")`.
   - On .NET Framework 4.6.2+ MSAL **defaults to an embedded web view** (the legacy WinForms browser), so the call must add `.WithUseEmbeddedWebView(false)` to use the system browser ([MSAL browsers](https://learn.microsoft.com/en-us/entra/msal/dotnet/acquiring-tokens/using-web-browsers)).
2. **Generate PKCE and state.** MSAL makes the `code_verifier` and the S256 `code_challenge`, and a random `state`.
3. **Open the authorize URL.** The default browser opens it with:
   - `response_type=code`
   - `redirect_uri=http://localhost:<port>`
   - `scope=openid profile offline_access User.Read Mail.Send` (+ `Mail.Read` when inbox download is on)
   - `login_hint=<EmailUsername>`
   - `code_challenge`
   - `state`

   MSAL listens on the loopback port.
4. **The user signs in** in the browser. MFA and Conditional Access happen there too, and so does consent, unless the admin already granted it. The browser is redirected to `http://localhost:<port>/?code=…&state=…`, or to `?error=…&error_description=…` on refusal ([auth code flow](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-auth-code-flow)).
5. **Redeem the code.** MSAL posts to the token endpoint:
   - `grant_type=authorization_code`
   - `code`
   - `redirect_uri`
   - `code_verifier`
   - `client_id`

   The response has:
   - `access_token` (about 60-90 minutes; use `expires_in`)
   - `refresh_token`
   - `id_token`
   - `scope`

   Codes expire after about 1 minute.
6. **Refresh.** `AcquireTokenSilent(scopes, account)` returns a cached access token or redeems the refresh token. Rules for refresh tokens ([refresh tokens](https://learn.microsoft.com/en-us/entra/identity-platform/refresh-tokens)):
   - They **rotate on every use**, and the old one isn't revoked.
   - Their default lifetime is **90 days**.
   - The service can revoke them at any time, for example after a password change or reset, or when the user or an admin revokes sessions.

   Revocation surfaces as `MsalUiRequiredException` (`invalid_grant` / `interaction_required`), and the only fix is an interactive sign-in.
7. **Sign out.**
   - There is no revocation endpoint for public clients. Sign-out removes the account from our cache (`RemoveAsync`) and blanks the stored blob.
   - Real revocation is done by the user ("Sign out everywhere" in their Microsoft account) or by an admin (revoke sessions, or delete the enterprise app).

Device code flow (`AcquireTokenWithDeviceCode`) is the documented alternative where no browser can reach a loopback port. It needs "Allow public client flows" turned on in the registration ([desktop configuration](https://learn.microsoft.com/en-us/entra/identity-platform/scenario-desktop-app-configuration)). This spec offers it only as an opt-in fallback (3.2). The **WAM broker** (`Microsoft.Identity.Client.Broker`) is the platform's preferred option on Windows 10+, but it adds a package and a parent-window handle, so it is left for later (open question 5).

### 2.4 Endpoints and fields we need

| Use in this repo | Endpoint | Delegated permission | Fields / notes |
|---|---|---|---|
| Account check after sign-in (`GetProfile`) | `GET /me?$select=id,mail,userPrincipalName,displayName` | `User.Read` | Compare `EmailUsername` with `mail` or `userPrincipalName`, case-insensitively |
| Send (`SendEmail`) | `POST /me/sendMail`, `Content-Type: text/plain`, body = base64 of the whole MIME message | `Mail.Send` | Returns **202 Accepted**: queued, not delivered. Saved to Sent Items. Attachments and S/MIME must be inside the MIME ([sendMail](https://learn.microsoft.com/en-us/graph/api/user-sendmail?view=graph-rest-1.0)). Malformed base64 gives 400 `ErrorMimeContentInvalidBase64String` |
| Send, large message | `POST /me/messages` (draft, JSON, no attachments), then for each attachment `POST /me/messages/{id}/attachments` (< 3 MB) or `.../attachments/createUploadSession` then `PUT` byte ranges (3-150 MB), then `POST /me/messages/{id}/send` | `Mail.ReadWrite` + `Mail.Send` | Microsoft documents 3 MB as the ceiling for a single-request attachment; above that an upload session is required ([large attachments](https://learn.microsoft.com/en-us/graph/outlook-large-attachments)). The upload session's `uploadUrl` is a **pre-authenticated URL on `outlook.office.com`**: the `PUT`s carry no `Authorization` header, and Microsoft advises keeping each range under 4 MB. Only used above the MIME threshold (3.6). Requesting `Mail.ReadWrite` only then is open question 3 |
| List inbox (`RetrieveMessages`) | `GET /me/mailFolders/inbox/messages?$select=id,receivedDateTime&$filter=receivedDateTime ge {since}&$orderby=receivedDateTime desc&$top=100`, with header `Prefer: IdType="ImmutableId"` | `Mail.ReadBasic` for listing; `Mail.Read` overall (next row) | Default page size is 10, `$top` 1 to 1000. Follow `@odata.nextLink` as-is; don't parse `$skip`. A property in `$orderby` must also appear in `$filter`, in the same order and ahead of other properties, or Graph returns `InefficientFilter` ([list messages](https://learn.microsoft.com/en-us/graph/api/user-list-messages?view=graph-rest-1.0)). Default message IDs change when a message is moved to another folder; immutable IDs don't, and both are case-sensitive ([immutable IDs](https://learn.microsoft.com/en-us/graph/outlook-immutable-id)). Use immutable IDs for every call, because `emailmessageuid` dedupes on the ID |
| Download one (`GetMIMEMessage`) | `GET /me/messages/{id}/$value` | `Mail.Read` | Returns RFC 822 MIME, parsed with `MimeKit.MimeMessage.Load` ([MIME](https://learn.microsoft.com/en-us/graph/outlook-get-mime-message)) |
| Token for all of the above | identity platform (2.3) | `openid`, `profile`, `offline_access` | `offline_access` is what returns a refresh token |

All are `/me` calls. Sending as a different `SenderAddress` (a shared mailbox or alias) is **not** supported in this version. Exchange rejects a MIME `From` the signed-in user has no Send As right for. `/users/{shared}/sendMail` would need `Mail.Send.Shared` (open question 4).

### 2.5 Rate and size limits

- **Graph Outlook limits, per app ID and mailbox** ([throttling limits](https://learn.microsoft.com/en-us/graph/throttling-limits)):
  - 10,000 requests per 10 minutes
  - 4 concurrent requests
  - 150 MB uploaded per 5 minutes

  Throttled calls get **429** with `Retry-After`. Honour it, and back off exponentially when it is missing.
- **Exchange Online sending limits** ([Exchange limits](https://learn.microsoft.com/en-us/office365/servicedescriptions/exchange-online-service-description/exchange-online-limits)):
  - 30 messages per minute
  - 10,000 recipients per day, per mailbox
  - up to 1,000 recipients per message
  - default maximum message size 35 MB sent and 36 MB received; an admin can raise it to 150 MB
  - a tenant-wide external recipient limit (TERRL) that depends on the licence count; trial tenants are capped at 5,000 a day
  - mail from the default `*.onmicrosoft.com` domain is limited to 100 external recipients a day

  **A statement run of 200 emails takes at least 7 minutes at 30 a minute.** The sender has to pace itself (3.6).
- **Request size.** The `sendMail` page states no size limit for a MIME body. Microsoft's own guidance is the 3 MB single-request attachment ceiling and the under-4-MB upload range, both in [large attachments](https://learn.microsoft.com/en-us/graph/outlook-large-attachments). A request-body ceiling of about 4 MB is widely reported for Graph, but it isn't documented on the pages cited here. The 2.9 MB MIME threshold (before base64, about 3.9 MB after) is therefore a conservative design choice, and manual check 6.2.5 has to confirm it. Larger messages go through the draft path in 2.4.

### 2.6 Terms that matter

- **No app review for a single-tenant app.** Each office registers "Accounts in this organizational directory only". No publisher verification is needed. Unverified multitenant apps show "unverified" on the consent screen, and tenant consent policies can block them. That is one more reason not to ship a project-wide multitenant registration.
- **Consent.**
  - `Mail.Send`, `Mail.Read` and `User.Read` are delegated permissions that users can normally consent to themselves.
  - Many tenants turn user consent off. The checklist therefore has the admin **grant admin consent** once (section 5, step 7).
- **PHI and BAA** ([HIPAA/HITECH](https://learn.microsoft.com/en-us/compliance/regulatory/offering-hipaa-hitech)):
  - Microsoft's HIPAA BAA is part of the **Microsoft Online Services Data Protection Addendum, by default**, for customers who are covered entities or business associates.
  - Exchange Online and Microsoft Entra ID are in scope for commercial Office 365.
  - Microsoft won't sign a customer's own BAA.
  - Compliance remains the office's job: encryption of patient email, retention, access control.
  - **Personal Microsoft accounts (outlook.com, hotmail.com) are not enterprise online services**, and nothing indicates the BAA covers them. Section 3.7 blocks them by default.
- **No intermediary.** With this design no third party sits between the office and Microsoft. The office's own software, under the office's own app registration, talks to the office's own tenant. Open Dental is no longer in the authorization path (contrast 1.2).
- **Developer terms.** Use of Graph falls under the [Microsoft APIs Terms of Use](https://learn.microsoft.com/en-us/legal/microsoft-apis/terms-of-use) (updated October 2025). Under the per-office model the office is both the developer and the customer. The duties that matter:
  - **Least privilege (§3(b)(5)).** Request only the permissions in use. That is why `Mail.Read` and `Mail.ReadWrite` are requested only when the features that need them are on.
  - **Limits (§3(b)(7)).** Don't get around throttling.
  - **Credentials (§2(b)).** Keep them secret.
  - **Email APIs (§3(c)).** Without permission expressly granted by the customer, email APIs may only be used to sync or back up mail. Sending and importing mail goes beyond that, so it rests on the office's express permission: the office is the customer, and its admin registers the app and grants consent (section 5).
  - **Security (§4).** Use reasonable security, and report vulnerabilities and breaches to the Microsoft Security Response Center.
  - **Privacy (§5).** Keep a privacy statement. It must link users to Microsoft's revoke-consent pages when Microsoft doesn't provide the sign-in UI. Here Microsoft provides it, because sign-in happens in the browser on Microsoft's page.
- **Basic SMTP AUTH is being retired.** Exchange Online is retiring Basic authentication for SMTP client submission. Under the revised timeline of January 2026, it stays as it is through December 2026. At the end of December 2026 it is turned off by default for existing tenants, though admins can turn it back on. A final removal date is to be announced in the second half of 2027 ([Exchange Team blog, updated timeline](https://techcommunity.microsoft.com/blog/exchange/updated-exchange-online-smtp-auth-basic-authentication-deprecation-timeline/4489835)). Offices on the Password option with `smtp.office365.com` will hit `550 5.7.30`, so this integration becomes the supported route for Microsoft 365 mailboxes. That is a reason to keep it, not a promotion; the UI stays neutral (3.8).

## 3. Desktop design

### 3.1 Credential model

- **One app registration per office tenant** (checklist in section 5).
  - The only identifiers are the **Application (client) ID** and the **Directory (tenant) ID**. Both are public, but they are still configured per office, never compiled in or committed.
  - There is **no client secret and no certificate**. A public client must not use one ([auth code flow](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-auth-code-flow)).
- **One sign-in per mailbox.** Each `emailaddress` row with `AuthenticationType=2` holds its own MSAL token cache for exactly one account.

### 3.2 Auth flow in the WinForms app

`butAuthMicrosoft_Click` (FormEmailAddressEdit) and the re-auth prompt in `FormEmailMessageEdit.butSend_Click` both start an interactive sign-in. Today only FormEmailMessageEdit passes `""`. FormEmailAddressEdit passes `textRefreshTokenMicrosoft.Text`, which is non-blank when the address is already signed in (1.2). Under the new contract (4.2), a non-blank value means "silent only", so **`butAuthMicrosoft_Click` must be changed to pass `""`**. Otherwise clicking Sign in on a signed-in address would silently reuse the old account instead of prompting. `GetAccessToken(username, "")` then runs:

1. **Check configuration.** `Microsoft365Program.GetConfig()` reads the client ID and tenant (3.3). If either is blank, show: "Enter the Application (client) ID and Directory (tenant) ID in Setup > Program Links > Microsoft 365 email first." No network call is made.
2. **Build the public client** for this mailbox, with these options:
   - `WithRedirectUri(config.RedirectUri)`, default `http://localhost`
   - `WithAuthority(AzureCloudInstance.AzurePublic, tenant)`
   - `WithClientName(<product name>)`
   - `WithLegacyCacheCompatibility(false)`
   - `WithLogging(..., enablePiiLogging:false)`

   Bind its `UserTokenCache` to an empty in-memory blob (3.4).
3. **Acquire interactively.** Run `AcquireTokenInteractive(scopes)` on a background task inside `ProgressWin`, the pattern `butAuthGoogle_Click` already uses. Options:
   - `.WithUseEmbeddedWebView(false)`
   - `.WithLoginHint(textUsername.Text)` (when not blank)
   - `.WithPrompt(Prompt.SelectAccount)`
   - `.WithSystemWebViewOptions(new SystemWebViewOptions{ HtmlMessageSuccess=…, HtmlMessageError=… })`. The pages are **self-contained neutral HTML** that load no external assets ("Sign-in complete. You can close this tab.")
   - `CancellationTokenSource(TimeSpan.FromMinutes(5))`. MSAL can't tell whether the user closed the browser, and the docs recommend a timeout of several minutes. ProgressWin's Cancel cancels it too.

   `scopes` = `User.Read`, `Mail.Send`, plus `Mail.Read` when "Download incoming emails" is ticked. MSAL adds `openid profile offline_access` itself.
4. **Check the account** with `GET /me`. If `mail` and `userPrincipalName` both differ from a non-blank `textUsername.Text`, say so and save nothing. The re-auth path already does this check. Otherwise set `textUsername.Text` from `mail` (or the UPN when `mail` is blank).
5. **Serialize the cache** (`SerializeMsalV3`), encrypt it with `CDT.Class1.Encrypt` (3.3), and return:

   ```
   MicrosoftTokenHelper {
     AccessToken  = <account marker, 3.3>,
     AccountInfo  = "osd1$…",
     EmailAddress = <mail or UPN>,
     ErrorMessage = ""
   }
   ```

   The form fills its two text boxes from these values as it does today. The boxes become read-only status displays (3.8).
6. **Cancel** (or `MsalClientException` `authentication_canceled`) returns a blank `AccessToken` and a blank `ErrorMessage`, which the callers already treat as "cancelled".
7. **Errors** are mapped to neutral messages (3.5). `error_description` is shown in Details, never a token.

Optional fallback, program property `Sign-in Method = DeviceCode`, for locked-down terminal servers where no browser can reach the loopback port:

- `AcquireTokenWithDeviceCode` shows the user code and the verification URL in a dialog with a Copy button.
- It needs "Allow public client flows = Yes" in the registration.
- It is off by default, because device-code phishing is a known risk and many tenants block it with Conditional Access.

### 3.3 Where credentials and tokens live

**Office-wide, in `programproperty`:**

- A new `program` row, `ProgramName.Microsoft365Email`, appended at the end of the enum. The UI calls it "Microsoft 365 email".
- `Enabled=0` and `ProgDesc="Microsoft 365 email (Microsoft Graph)"`.
- It is inserted at runtime by `Microsoft365Program.EnsureProgram()`, the first time Setup > Program Links is opened. This is the same pattern as `postcardmania.md` §3.2, and both should share `Programs.InsertIfMissing` / `ProgramProperties.InsertIfMissing`.
- No schema change and no ConvertDatabases step.
- All rows use `ClinicNum=0` and `ComputerName=''`.

| PropertyDesc | Value | Encrypted | Masked |
|---|---|---|---|
| `Application (client) ID` | GUID from the registration | no (public identifier) | no |
| `Directory (tenant) ID` | Tenant GUID (recommended) or verified domain. `organizations` is allowed. `common`/`consumers` only with "Allow personal accounts" | no | no |
| `Redirect URI` | Default `http://localhost`. `http://localhost:<port>` only if the office registered a fixed port | no | no |
| `Sign-in Method` | `SystemBrowser` (default) or `DeviceCode` | no | no |
| `Allow personal accounts` | `0` (default) or `1` (3.7) | no | no |

There are **no secrets** at office level, so these rows need no `IsHighSecurity`. Changing them needs the `Setup` permission, as Program Links already does.

**Per mailbox, in the existing `emailaddress` columns.** The integration has always stored its per-mailbox state there, and `programproperty` can't be keyed per address. No schema change.

| Column | New content | Notes |
|---|---|---|
| `RefreshToken` (TEXT) | `osd1$` + the MSAL v3 cache for this one account, which holds the refresh token, the ID token, the account record and the current access token | Roughly 8 KB of plain text before encryption, estimated from Microsoft's per-token sizes for an auth-code user cache: about 4 KB for an access token and 2 KB each for the refresh and ID tokens ([cache sizes](https://learn.microsoft.com/en-us/entra/msal/dotnet/how-to/token-cache-serialization); those figures are given for web apps, but the token types are the same). That is roughly 11 KB once encrypted and base64-encoded. That fits in TEXT (64 KB). Written with `CDT.Class1.Encrypt` and **stored only if it returns true**. Never use `TryEncrypt`: it falls back to plain text. Read with `CDT.Class1.Decrypt`; a failure counts as "not signed in" |
| `AccessToken` (varchar 2000) | **Account marker**: the MSAL `HomeAccountId.Identifier` (`<objectId>.<tenantId>`, about 73 characters) | No longer a bearer token. It keeps the UI's "non-blank means signed in" checks working and is MSAL's key for `GetAccountAsync`. That fixes defect 2 |
| `EmailUsername` | `mail` or UPN | unchanged |
| `SMTPserver` / `ServerPort` / `Pop3ServerIncoming` / `ServerPortIncoming` | `smtp.outlook.com` / `0` / `pop.outlook.com` / `0` placeholders | Unchanged, because other code checks `SMTPserver!=""` (FormEmailMessageEdit 802) and `Pop3ServerIncoming!=""` (FormEmailInbox 219) |

Notes:

- **`osd1$` is obfuscation, not secrecy.** CDT/README.md says its key is public. Anyone who can read `emailaddress` can recover a refresh token and send mail as that mailbox until it is revoked. Mitigations:
  - **Assignment required** on the enterprise app, so only chosen users can sign in (section 5, step 8)
  - Conditional Access, if the office has Entra ID P1
  - revoking sessions when a workstation or a database copy is lost
  - never logging the column

  Same threat model as the other bridge secrets (7.2).
- **Middle Tier:** the rows go through the existing `EmailAddresses` cache and `EmailAddresses.Update`. Decryption and every Microsoft call run on the client, as today.

### 3.4 Token runtime and refresh

New `OpenDentBusiness/WebBridges/Microsoft365/Microsoft365Session.cs`:

- **One session per process per `EmailAddressNum`**, keyed by (client ID, tenant, `EmailAddressNum`). A config change means a new key, which means a new session.
- **Each session owns one `IPublicClientApplication`**, with `UserTokenCache.SetBeforeAccess` / `SetAfterAccess`:
  - **BeforeAccess:** if the in-memory cache is empty, decrypt `emailAddress.RefreshToken` and `DeserializeMsalV3` it.
  - **AfterAccess:** if `args.HasStateChanged`, serialize and encrypt into memory. Write to the database (`EmailAddresses.Update`) only when one of these holds:
    1. the account changed (sign-in or sign-out), or
    2. this process hasn't written this mailbox in the last 24 hours.

    The second rule keeps a recent refresh token in the database for new processes: tokens rotate, but each lasts 90 days. Hourly access-token refreshes don't write at all. **No `Signalods` is sent:** each workstation keeps its own rotating chain in memory, and the old one stays valid.
- **`GetAccessTokenAsync(forceRefresh:false)`:**
  - Under a per-session `SemaphoreSlim`, run `AcquireTokenSilent(scopes, account)`, where `account` is found by the stored `HomeAccountId`.
  - MSAL hands back the cached token while it is valid (it refreshes about 5 minutes before expiry) and redeems the refresh token otherwise.
  - On `MsalUiRequiredException`, set `NeedsReauthentication` and throw `Microsoft365AuthException` (3.5). Background callers never prompt.
- **Graph 401 (`InvalidAuthenticationToken`):** call `AcquireTokenSilent(...).WithForceRefresh(true)` once and retry the request once. A second 401 counts as needing re-authentication.
- **Background senders** (statements, other `EmailMessages.SendEmail` callers):
  - They get tokens the same way, silently, from whatever process sends.
  - They need outbound HTTPS to `login.microsoftonline.com` and `graph.microsoft.com`.
  - An unattended service that has never had a UI sign-in can still send, because the cache comes from the database.
- **Concurrency per mailbox:** at most 2 Graph calls in flight from one process (a per-session semaphore). That stays well under Outlook's limit of 4 even with two workstations sending at once.

### 3.5 Errors and offline behaviour

| Condition | Detected by | Behaviour |
|---|---|---|
| Not configured (no client ID or tenant) | `Microsoft365Program.GetConfig()` | Setup shows the message in 3.2 step 1. Sending throws `ODException`: "Microsoft 365 email is not set up. An administrator can configure it in Setup > Program Links." No network call |
| Not signed in, or a legacy Open Dental cache (3.9) | blank or undecryptable `RefreshToken`, or no account | The existing message: "{address} needs to be re-authenticated. Please sign out and back in to continue." FormEmailMessageEdit offers sign-in as it does today |
| Refresh token revoked or expired (password reset, admin revoke, 90 days unused) | `MsalUiRequiredException` (`invalid_grant`, `interaction_required`, AADSTS50173, AADSTS700082) | `NeedsReauthentication`. **One alert per mailbox per day** (an `AlertItem` with `FormToOpen.FormEmailAddresses`, as `ConvertDatabases7.To22_2_51` already does), not one per send. Later calls in that process fail fast until the row changes |
| Wrong client ID or tenant | AADSTS700016 / AADSTS90002 | "The app registration was not found in this tenant. Check the Application (client) ID and Directory (tenant) ID." |
| Redirect URI not registered | AADSTS50011 | "Add http://localhost as a Mobile and desktop applications redirect URI in the app registration." |
| Consent missing | AADSTS65001, `consent_required`, or the user can't consent | "An administrator must grant consent for this app (Microsoft Entra admin center > App registrations > API permissions)." |
| Blocked by Conditional Access or assignment | AADSTS53003 / AADSTS50105 | Show `error_description`. The office's IT has to allow it |
| Access token expired | Graph 401 `InvalidAuthenticationToken` | Transparent: force refresh once, retry once (3.4) |
| Mailbox has no Exchange Online licence, or is on-premises | Graph 404/403 `MailboxNotEnabledForRESTAPI` / `ResourceNotFound` | "This account has no Exchange Online mailbox." |
| Missing scope (inbox download turned on later) | Graph 403 `ErrorAccessDenied` | Ask the user to sign in again. Interactive sign-in then requests `Mail.Read` (incremental consent) |
| Too large | Graph 413 / `RequestBodyTooLarge` / `ErrorMessageSizeExceeded` | MIME path above the threshold: use the draft and upload path (3.6). Above the tenant's MaxSendSize, "Message exceeds the mailbox's maximum size ({n} MB)." |
| Throttled | 429 / 503 with `Retry-After` | Interactive calls wait at most 10 s, then fail with "Microsoft 365 is busy; try again." Background calls back off exponentially up to 60 s, at most 5 times. Exchange's 30 messages a minute is enforced before sending (3.6) |
| Graph 5xx | 500/502/504 | Retry idempotent GETs 2 times (1 s, then 2 s). **`sendMail` is never retried automatically after a 5xx or a timeout,** because the message may already be queued, and resending would duplicate patient mail. The user sees the failure and decides |
| Offline: DNS failure, refused connection, TLS failure or timeout | `HttpRequestException`, `TaskCanceledException`, `MsalServiceException` with `request_timeout`, or `MsalClientException` with a network code | **Circuit breaker:** after a network-level failure, mark Microsoft 365 unreachable for 30 s, and fail calls at once with "Microsoft 365 could not be reached." Email is never needed for charting, scheduling, billing or claims (ground rule 5); only sending and downloading wait for the network |

- **Timeouts:**
  - token calls: MSAL's default
  - Graph JSON calls: 30 s
  - `sendMail` and `$value`: 100 s

  Use `ConfigureAwait(false)` throughout. The synchronous wrappers (`GetProfile`, `SendEmail`, …) block with `GetAwaiter().GetResult()` on a pool thread, never on the UI thread.
- **Logging** (`ODException` details, `Logger`): endpoint, HTTP status, Graph `error.code`, and the `request-id` / `client-request-id` headers. **Never** log tokens, the cache, the code, subjects, addresses or MIME.

### 3.6 Sending

`MicrosoftApiConnector.SendEmail(username, accessToken, BasicEmailMessage)`:

1. Build the MIME with the existing `SendEmails.CreateMIMEMsg`. Change it from private to internal so the Gmail and Microsoft paths share one builder. It already handles inline images, attachments, Bcc and the forced base64 encoding for PDFs.
2. Write it to a `MemoryStream`. **If its length is at most 2,900,000 bytes**, POST its standard base64 (not URL-safe) to `/me/sendMail` with `Content-Type: text/plain`. The 4/3 base64 growth plus overhead stays under about 4 MB.
3. **Otherwise use the draft path:**
   - `POST /me/messages` with the JSON body: subject, body, To/Cc/Bcc, inline images as `fileAttachment` with `isInline` and `contentId`
   - add each attachment: one POST under 3 MB, otherwise `createUploadSession`, then `PUT` ranges of 3 MiB (Microsoft advises under 4 MB each) to the returned `uploadUrl` on `outlook.office.com`, with **no** `Authorization` header
   - `POST /me/messages/{id}/send`

   On failure, `DELETE` the draft (best effort).
4. **Pacing.** A per-mailbox token bucket allows 30 sends per rolling minute. A caller that would go over waits, and interactive callers see "Sending…" while they do. The 10,000-recipients-a-day limit isn't tracked locally; Exchange's NDR or 429 is surfaced as-is.
5. **Direct/S/MIME messages** (`SendDirectUnsecure`, `EmailMessages.cs` ~940) go out by SMTP to the Direct HISP, not through this path. Unchanged.

### 3.7 Account types and PHI

- **Default authority: the office's tenant ID.** Only accounts in that directory can sign in. This keeps mail inside the services the BAA covers (2.6).
- **Personal accounts** need `Directory (tenant) ID` set to `common` or `consumers`, **and** "Allow personal accounts" ticked in Setup, **and** a registration created with "personal Microsoft accounts" as a supported account type.
  - Setup shows: "Microsoft's HIPAA business associate agreement covers its business online services. Do not use personal Microsoft accounts for patient information."
  - This mirrors the BAA gate in other vendor-direct specs, without promoting anything.

### 3.8 UI, vendor neutrality and gating (ground rules 6 and 7)

- **Off by default.**
  - The program row is `Enabled=0`.
  - `radioMicrosoft` and `groupMicrosoft` are visible only when `Programs.IsEnabled(ProgramName.Microsoft365Email)` **or** the address being edited is already `OAuthType.Microsoft`, so a migrated row still shows its state.
  - **Interim, before the code lands:** hide `radioMicrosoft` behind the same check, so nobody signs in again under Open Dental's registration.
- **New `FormMicrosoft365Setup`**, opened by double-clicking the row in Program Links:
  - Fields: Enabled, Application (client) ID, Directory (tenant) ID, Redirect URI (advanced), Sign-in method (advanced), Allow personal accounts.
  - Each field is validated: client ID is a GUID; tenant is a GUID, a domain, or one of the reserved words; redirect is `http://localhost[:port][/path]`.
  - A short help text gives the registration steps by name (section 5), with no links to buy anything.
- **FormEmailAddressEdit, Microsoft group:**
  - `textAccessTokenMicrosoft` and `textRefreshTokenMicrosoft` become read-only labels: **"Signed in as {mail}"** or **"Not signed in"** or **"Needs sign-in"**.
  - Tokens and the cache are never shown. The text boxes stay, hidden, so the designer and the save logic keep working; or the labels replace them.
  - `butAuthMicrosoft` becomes a plain "Sign in…" text button. Today it shows Microsoft sign-in artwork, loaded from the form's resx (`butAuthMicrosoft.Image`) and rescaled by `butAuthMicrosoftImageHelper`. Drop the image and the helper call in `FormEmailAddress_Load`. Microsoft's artwork could be kept under Microsoft's branding rules, but a text button is simpler and fully neutral. Open question 7.
- **Wording** names the integration only ("Microsoft 365 email"). There are no logos in Setup, no sign-up wording and no "contact support".

### 3.9 Data migrated from Open Dental

On a restored `fox-db` copy:

- **Rows with `AuthenticationType=2`** carry a `RefreshToken` that isn't `osd1$` (it uses the `MiscUtils.Encrypt` key) and an MSAL cache for **Open Dental's** client ID.
  - `Microsoft365Session` treats a non-`osd1$` or undecryptable value as "not signed in". It never decrypts it with `MiscUtils.Decrypt`.
  - On first use it blanks `AccessToken` and `RefreshToken` for that row (a runtime data update, not a migration) and raises the re-authentication alert.
  - The admin then signs each address in again under the office's registration.
- **The tenant admin removes Open Dental's enterprise app** (section 5, step 11). That revokes Open Dental's grants and the refresh tokens behind them.
- **Check for an existing program row** named like Microsoft on the copy: `SELECT ProgName FROM program WHERE ProgName LIKE '%icrosoft%'`. None is expected, and `EnsureProgram` uses a distinct name either way.

### 3.10 Hosts contacted, and no HQ

- **The workstation:**
  - `login.microsoftonline.com`: authority, instance discovery, token
  - `graph.microsoft.com`
  - `outlook.office.com`, only for attachment upload sessions on the large-message path (2.4)
  - its own `localhost:<random port>` during sign-in
- **The system browser** also loads Microsoft's sign-in assets: `login.live.com` for personal accounts, and Microsoft's CDNs. This happens only in the browser.

No Open Dental host, no `IWebServiceMainHQ` and no `ODCloudClient`. `tools/Test-NoHqHosts.ps1` keeps passing. TLS 1.2 is already forced in `ODInitialize`.

## 4. Code changes

### 4.1 Library choice

| | **Recommended** | Alternative |
|---|---|---|
| Auth | **MSAL.NET `Microsoft.Identity.Client`, current 4.x**. NuGet lists 4.91.0 (2026-10-12) as the newest, which is less than two weeks old on 2026-10-18; pin 4.90.1 (2026-09-25) or a later release once it has aged two weeks. It does PKCE, the loopback listener, rotation, cache serialization and Conditional Access claims challenges, and the docs advise against hand-written protocol code ([auth code flow](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-auth-code-flow)). Supports net462 and net472 | Hand-written PKCE and loopback, as the Dropbox spec does. Not recommended: Entra adds claims challenges, Conditional Access and rotation |
| Graph | **In-repo REST client** over `HttpClient` and Newtonsoft.Json 12.0.3 for the five calls in 2.4, about 400-500 lines | NuGet `Microsoft.Graph` 5.x/6.x (Kiota). That is a large dependency tree for five calls, and it renames every model namespace anyway |
| Removes | `Microsoft.Graph.dll` (10,439 KiB, about 10.7 MB, SDK 4.42), `Microsoft.Graph.Core.dll`, `Azure.Core.dll` (Graph-only), `Microsoft.IdentityModel.JsonWebTokens.dll` (already unused), and probably `IdentityModel.Logging`, `.Tokens` and `.Protocols*` (check what still needs them). `Microsoft.IdentityModel.Abstractions` stays as an MSAL dependency | |

For .NET Framework, MSAL 4.9x depends on these packages ([NuGet](https://www.nuget.org/packages/Microsoft.Identity.Client)):

- `Microsoft.IdentityModel.Abstractions` ≥ 8.14.0. `Required dlls` has 6.23.1, so this one **conflicts**.
- `System.Text.Json` ≥ 6.0.10. `Required dlls` has 6.0.9, so this one **conflicts**.
- `System.Diagnostics.DiagnosticSource` ≥ 6.0.1. The shipped 7.0.4 satisfies it, though it may still need a binding redirect.
- `System.Formats.Asn1` ≥ 8.0.1. It is not shipped today and has to be added.
- `System.ValueTuple` 4.5.0, net462 target only.

The two conflicts need newer copies with binding redirects, or the planned NuGet move (7.2). If that is blocked, keep the shipped MSAL 4.47.1 for the first cut. It already supports `WithUseEmbeddedWebView(false)` and the cache callbacks.

### 4.2 Prebuilt Bridges.dll types re-implemented in-repo

New folder `OpenDentBusiness/WebBridges/Microsoft365/`, namespace **`OpenDentBusiness.Microsoft365`**. The types keep their class names and their public surface as this repo calls them. The namespace changes so that the `Bridges` namespace in the still-referenced Bridges.dll never makes a name ambiguous: the four files drop `using Bridges;`. On `main`, Bridges.dll is still used through `using Bridges;` by 20 other files: QuickBooks Online, USPS address verification (`FrmAddressCompare`), and the not-yet-cut CareCredit, PostcardMania and PayConnect2 code.

```csharp
public class MicrosoftTokenHelper {
  public string AccessToken="";   // now the account marker (3.3), "" = cancelled/none
  public string AccountInfo="";   // osd1$ MSAL cache
  public string EmailAddress="";
  public string ErrorMessage="";
}
public static class MicrosoftApiConnector {
  // accountInfo blank => interactive (UI caller only); non-blank => silent only, never prompts.
  // Callers today: FormEmailMessageEdit passes "" (unchanged); FormEmailAddressEdit passes the text box and must switch to "" (3.2);
  // RefreshMicrosoftToken passes the stored cache and is replaced by Microsoft365Session (4.3).
  public static Task<MicrosoftTokenHelper> GetAccessToken(string emailUsername,string accountInfo);
  // Called with .Result on the UI thread today: the implementation must ConfigureAwait(false) throughout (defect 6).
  public static Task<MicrosoftTokenHelper> SignOutUser(string emailUsername,string accountInfo);
  public static MicrosoftProfile GetProfile(string emailUsername,string accessToken);           // was: return unused
  public static List<MicrosoftMessageRef> RetrieveMessages(string emailUsername,string accessToken); // was List<Microsoft.Graph.Message>; callers use .Id only
  public static MimeKit.MimeMessage GetMIMEMessage(string emailUsername,string accessToken,string messageId);
  public static void SendEmail(string emailUsername,string accessToken,BasicEmailMessage message);
}
```

- **`accessToken` arguments are real bearer tokens**, obtained just before the call by `Microsoft365Session.GetAccessToken(emailAddress)`. They are never read from the `AccessToken` column, which now holds the marker.
- **Failures throw `Microsoft365Exception`** with `HttpStatus`, `GraphErrorCode`, `RequestId` and `IsAuthError`, or `Microsoft365AuthException`, which derives from it, for re-authentication. The `Message` is neutral. For one release `InnerException` stays non-null and its message starts with the Graph code, so a missed string check still works.
- **`MicrosoftMessageRef`** has `Id` and `ReceivedDateTime`. **`MicrosoftProfile`** has `Id`, `Mail`, `UserPrincipalName` and `DisplayName`.

### 4.3 File-by-file changes

| File | Members | Change |
|---|---|---|
| **New** `OpenDentBusiness/WebBridges/Microsoft365/MicrosoftApiConnector.cs`, `MicrosoftTokenHelper.cs`, `Microsoft365Session.cs`, `Microsoft365GraphClient.cs`, `Microsoft365Program.cs`, `Microsoft365Exception.cs` | as in 4.2, 3.3 and 3.4 | `Microsoft365Program`: PropertyDesc constants, `EnsureProgram()`, `GetConfig()` and `IsEnabled`. `Microsoft365GraphClient`: the HttpClient, retries, circuit breaker, pacing (3.5, 3.6). `Microsoft365Session`: the MSAL client and cache binding (3.4) |
| `OpenDentBusiness/OpenDentBusiness.csproj` | references, compile items | Add the `Microsoft.Identity.Client` reference (MSAL is referenced by no `.csproj` today). Remove `Microsoft.Graph` and `Microsoft.Graph.Core`. Add the new files |
| `OpenDentBusiness/TableTypes/Program.cs` | `enum ProgramName` | Append `Microsoft365Email` at the end |
| `OpenDentBusiness/Data Interface/EmailMessages.cs` | `using Bridges;` | Replace with `using OpenDentBusiness.Microsoft365;` |
| same | `SendEmailUnsecure` Microsoft branch (~900-920) | Delete the `GetProfile` probe and the string check. Today the basic address is built inline in the `WireEmailUnsecure(ODEmailAddressToBasic(emailAddress),…)` call (921). Build it into a local first, then set `basicEmailAddress.AccessToken = Microsoft365Session.GetAccessToken(emailAddress)` before handing it to `SendEmails`. On `Microsoft365AuthException`, throw the existing "needs to be re-authenticated" `ODException`. Retry once on an auth `Microsoft365Exception` from send, with force refresh |
| same | `RetrieveFromMicrosoftInbox` (1198-1245) | `List<MicrosoftMessageRef>`. Get the token from the session. Remove the `TrimStart` char-set call (defect 5) and compare IDs exactly. Replace the `InnerException` check with `catch(Microsoft365Exception ex) when(ex.IsAuthError && !hasRetried)`. Pass a `since` date: the newest `emailmessageuid` date for that address, or 30 days the first time |
| same | `RefreshMicrosoftToken` (1683-1708) | Becomes `Microsoft365Session.Get(emailAddress).ForceRefresh()`. Remove the `ODEnvironment.IsCloudInstance` branch and the `Signalods` call (3.4) |
| same | `ODEmailAddressToBasic` | No change for the password. For Microsoft, leave `AccessToken` blank (the caller sets it) and never copy `RefreshToken` into the basic object |
| `OpenDentBusiness/Email/SendEmails.cs` | `using Bridges;`, `SendEmailOAuth` (206-219), `CreateMIMEMsg` | Change the using. The call is unchanged. Make `CreateMIMEMsg` `internal` |
| `OpenDental/Forms/FormEmailAddressEdit.cs` | `using Bridges;`, `butAuthMicrosoft_Click`, `butClearTokensMicrosoft_Click`, `FormEmailAddress_Load`, `RefreshEmailTextFields`, `butSave_Click` | Change the using. Delete the `IsCloudServer` / `ODCloudClient` branches. `butAuthMicrosoft_Click` passes `""` instead of `textRefreshTokenMicrosoft.Text` (3.2). `butClearTokensMicrosoft_Click` stops blocking on `.Result` (defect 6). Drop the `butAuthMicrosoftImageHelper` call (3.8). Sign-in runs inside `ProgressWin` with Cancel (3.2). Add the status labels (3.8). Gate `radioMicrosoft` (3.8). `checkDownloadMicrosoft` ticked on an address that was signed in without `Mail.Read` prompts sign-in again on Save |
| `OpenDental/Forms/FormEmailAddressEdit.designer.cs` | `groupMicrosoft` | Status label, "Sign in…" button, read-only or hidden token boxes |
| `OpenDental/Forms/FormEmailMessageEdit.cs` | `using Bridges;`, re-auth block (807-845), catch block (879-891) | Change the using. Delete the cloud branches, including the cloud-only catch branch (879-891) that clears the tokens on `InvalidAuthenticationToken`. The re-auth block keeps its flow but compares against `mail`/UPN case-insensitively. Add a new, non-cloud `catch(Microsoft365AuthException)` that **doesn't** clear the tokens automatically and offers "Sign in again" instead |
| **New** `OpenDental/Forms/FormMicrosoft365Setup.cs` (+ designer) | whole form | Section 3.8 |
| `OpenDental/Forms/FormProgramLinks.cs` | `FillList`, double-click switch | Call `Microsoft365Program.EnsureProgram()` before `Programs.RefreshCache()`. Add `case nameof(ProgramName.Microsoft365Email)` to open `FormMicrosoft365Setup` |
| `CodeBase/Utilities/ODCloudClient.cs` | `GetMicrosoftAccessToken`, `MicrosoftSignOutUser`, `ODCloudClientData.MicrosoftRefreshToken` | Delete them. Keep the `CloudClientAction` members as `[Obsolete]` so the enum ordinals stay put, or remove them with the general ODCloud cleanup |
| `OpenDentBusiness/Data Interface/AutoCodes.cs`, `PatientNotes.cs`, `Patients.cs` | `using Microsoft.Graph;` / `using Microsoft.Graph.ExternalConnectors;` | Remove. These are unused, and they block deleting `Microsoft.Graph.dll` |
| `Required dlls/` | Graph, Graph.Core, Azure.Core, IdentityModel.* | Delete per 4.1 **after** Bridges.dll is gone or proven never to load its Microsoft types: Bridges.dll's metadata references Graph, but the CLR resolves it only when those methods run. Update MSAL if the NuGet move is in place |
| `docs/third-party-binaries-audit.md` | rows for MSAL, Graph, Graph.Core, Azure.Core, IdentityModel.*, Bridges.dll | Mark them "replaced by in-repo Microsoft 365 client" or "removed" |
| Tests | new | Section 6.3 |

### 4.4 "Not available" stubs replaced

None exist for this integration. Nothing in `WebServiceMainHQNoHq` or `VendorNotAvailable` relates to Microsoft. The change replaces **live calls into the prebuilt Bridges.dll**, not stubs. The interim gate in 3.8 is the only thing to do before the code lands.

## 5. Credentials the office must register (checklist)

Done once per office in the office's Microsoft 365 tenant. Registering the app needs at least the **Application Developer** role. Granting tenant-wide admin consent to Microsoft Graph **delegated** permissions needs **Cloud Application Administrator** or **Application Administrator** (or Privileged Role Administrator) ([grant admin consent](https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/grant-admin-consent)). In practice, one person with Cloud Application Administrator can do every step.

1. **Plan and BAA.**
   - Confirm that the mailboxes are on a Microsoft 365 / Office 365 business plan that includes **Exchange Online**.
   - As a covered entity, confirm that the [Microsoft HIPAA BAA](https://servicetrust.microsoft.com/DocumentPage/d60051b9-8b5a-4794-9844-033773faaeb0) applies through the Data Protection Addendum ([overview](https://learn.microsoft.com/en-us/compliance/regulatory/offering-hipaa-hitech)), and keep a copy with the office's HIPAA records.
   - Use a **custom verified domain** for sending, not `*.onmicrosoft.com`, which is limited to 100 external recipients a day.
2. **Open the admin center.** Sign in at [entra.microsoft.com](https://entra.microsoft.com) and go to **Entra ID > App registrations > New registration** ([register an app](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app)). Older portal layouts showed this path as Identity > Applications > App registrations.
3. **Register the app.**
   - **Name:** something neutral that staff will recognize on the consent screen, for example "<Office name> practice software email".
   - **Supported account types:** the single-tenant option. The current portal labels it *Single tenant only - <your tenant>*; older layouts say *Accounts in this organizational directory only*.
   - **Redirect URI:** add it after registering, under **Authentication > Add a platform > Mobile and desktop applications**, with the exact value `http://localhost` ([desktop configuration](https://learn.microsoft.com/en-us/entra/identity-platform/scenario-desktop-app-configuration)).
4. **Copy the IDs.** From **Overview**, copy the **Application (client) ID** and the **Directory (tenant) ID**.
5. **Authentication.**
   - Confirm that `http://localhost` is listed under *Mobile and desktop applications*.
   - Leave **Allow public client flows = No**, unless the office wants the device-code fallback (3.2).
   - **Don't** add a Web or SPA platform.
6. **Certificates & secrets:** create **nothing**. The app is a public client.
7. **API permissions.**
   - Add **Microsoft Graph > Delegated**: `User.Read` (already present), `Mail.Send`, `offline_access`, `openid`, `profile`.
   - Add `Mail.Read` only if staff will download incoming email into the program.
   - Add `Mail.ReadWrite` only if attachments over about 3 MB must be sent (open question 3).
   - Click **Grant admin consent for <tenant>**.
8. **Limit who can use it (recommended).** **Entra ID > Enterprise apps** > the new app > **Properties** > **Assignment required = Yes**. Then, under **Users and groups**, add only the people whose mailboxes the program will send from.
9. **Optional: Conditional Access.** With Entra ID P1, allow sign-in to this app only from the office network or from compliant devices.
10. **In the program.**
    - **Setup > Program Links > Microsoft 365 email**: tick Enabled and enter the two IDs.
    - Then **Setup > Email > (address) > Microsoft > Sign in…** for each address.
    - Tick "Download incoming emails" only if `Mail.Read` was added.
11. **If the office used Open Dental's Microsoft sign-in before** (any `emailaddress` row with AuthenticationType=2 on the old database):
    - In **Entra ID > Enterprise apps**, find Open Dental Software's application and **delete it**, or remove its permissions. That revokes its grants.
    - Then sign each address in again (3.9).
12. **Record it in the office's IT notes:** the app name, the client ID, who has access, and that rotating means **Revoke sessions** for a user, or deleting the enterprise app. Add one line to the office's privacy and HIPAA policies: practice email is sent through Microsoft 365 under the office's own app registration. Staff can review or revoke consent at [myapps.microsoft.com](https://myapps.microsoft.com). This meets the API Terms' privacy-statement duty (§5).

No partner agreement, NDA or Microsoft review is involved.

## 6. Test plan

### 6.1 Sandbox

- **Preferred:** a [Microsoft 365 Developer Program](https://learn.microsoft.com/en-us/office/developer-program/microsoft-365-developer-program) E5 sandbox, if the developer qualifies. Eligibility is limited to:
  - Visual Studio Professional or Enterprise subscribers
  - ISV Success members and eligible Microsoft AI Cloud Partner Program partners
  - Premier or Unified Support customers

  The program isn't offered in the government clouds.
- **Otherwise:**
  - a Microsoft 365 Business Basic **trial** tenant (5,000 external recipients a day)
  - or one low-cost Business Basic licence in a test tenant separate from the office's production tenant
- **Test data.** Register the app there exactly as in section 5, with two licensed test users. Use only fabricated patients from `tools/New-DevDatabase.ps1`, and send only to the test mailboxes and to a throwaway external address.

### 6.2 Manual checks

1. **Not configured.**
   - With the program disabled, the Microsoft radio is hidden.
   - Enabled with blank IDs, Sign in gives the "not set up" message and makes no network call (check with Fiddler or a firewall log).
2. **Sign-in.**
   - The system browser opens. MFA works.
   - The local success page loads no external assets.
   - The form shows "Signed in as …".
   - The `emailaddress` row has `osd1$…` in `RefreshToken` and a short marker in `AccessToken`.
   - Cancelling and closing the browser time out cleanly after 5 minutes. Cancel in ProgressWin works.
3. **Wrong account.** Sign in as user B while `EmailUsername` is A: the form refuses and saves nothing.
4. **Send plain and HTML** with an inline image, Cc and **Bcc**. Check:
   - delivery and the headers
   - that Bcc is hidden from the To/Cc recipients
   - that the message appears in Sent Items
5. **Attachments.**
   - 1 MB PDF: MIME path. Open the PDF to confirm it isn't corrupted.
   - 5 MB and 20 MB PDFs: draft and upload path, with the draft deleted on a forced failure.
   - A message above the tenant's MaxSendSize gives a clean error.
6. **Restart.**
   - Close and reopen the program, then send with no prompt: the cache comes from the database.
   - Do the same from a second workstation at once: both send, and there is no `Signalods` storm.
7. **Expiry.** Wait more than 90 minutes, or use the "revoke sessions" test below. Sending refreshes silently. Then confirm a database write happens at most once in 24 hours per process.
8. **Revocation.**
   - An admin runs **Revoke sessions** on the user: the next send gives one re-authentication alert and fails fast afterwards. Signing in again in FormEmailMessageEdit recovers.
   - A password reset behaves the same.
9. **Consent and Conditional Access.**
   - With admin consent removed and user consent off, the consent message appears.
   - Assignment required with the user unassigned gives AADSTS50105 shown neutrally.
10. **Offline.**
    - Unplug the network: the send fails within the timeout with "could not be reached", and the next 30 s fail at once. Charting and scheduling are unaffected.
    - After reconnecting, the send works.
11. **Throttling.** Send 40 statements in a loop: pacing holds 30 a minute and nothing is duplicated.
12. **Inbox** (once open question 1 is decided). Send 3 messages to the mailbox, then run the download: they are imported once, a second run imports nothing, and a message with an inline image keeps it.
13. **Sign out.** The row is blanked, and the next send asks for sign-in.
14. **Migrated row.** Restore an Open Dental row with AuthenticationType=2: it is treated as not signed in, cleared on first use, and an alert is raised.
15. **Middle Tier.** Repeat checks 2, 4 and 6 from a Middle Tier client.
16. **Hosts.** Capture traffic during checks 2-12: only `login.microsoftonline.com`, `graph.microsoft.com`, `outlook.office.com` (upload sessions in check 5 only) and localhost from the program. Run `tools/Test-NoHqHosts.ps1`.

### 6.3 Automated tests (`NoHqTests` or a new `OpenDentBusiness.Tests` class)

- **Cache round trip.** `Encrypt` → `Decrypt` → `DeserializeMsalV3` on a **synthetic** MSAL v3 cache fixture (dummy tokens and IDs). Never commit a cache captured from a real tenant, even a test tenant. A non-`osd1$` value counts as not signed in.
- **Graph client against a fake `HttpMessageHandler`:**
  - 401 → one forced refresh → retry
  - 429 with `Retry-After`
  - 503
  - `sendMail` never retried after a timeout
  - `@odata.nextLink` paging
  - the 2.9 MB threshold picks the right path
- **Errors.** Mapping of the AADSTS codes in 3.5 to messages.
- **No secrets in logs.** A log-capture test asserts that no token, cache or address is written.

## 7. Size, risks and open questions

### 7.1 Size: M, about 6 to 8 days

- MSAL session, cache binding and token runtime: 1.5 days
- Graph client with send (both paths), list, MIME, errors, throttling and circuit breaker: 2 days
- Program row, setup form, FormEmailAddressEdit and FormEmailMessageEdit changes, gating: 1.5 days
- Removing the cloud branches, the Graph DLLs and the `using` lines; the csproj and audit updates: 0.5 day
- Tests and the manual pass in a test tenant: 1-2 days
- Not included: an inbox poller (open question 1, about 1-2 days more), and the repo-wide NuGet/binding-redirect work if MSAL is upgraded (shared with Phase 2)

### 7.2 Risks

- **Status quo risk (high).** Until this lands, Microsoft sign-in keeps running under Open Dental's app registration. Apply the 3.8 gate now.
- **`osd1$` is not secrecy.** A database copy exposes refresh tokens that can send mail as clinic mailboxes. Mitigations: assignment required, Conditional Access, revoke on loss, and audit whoever can read `emailaddress`. The same applies to every stored bridge credential.
- **Shared refresh chain across workstations.** It relies on Entra not revoking old refresh tokens on rotation ([refresh tokens](https://learn.microsoft.com/en-us/entra/identity-platform/refresh-tokens)). If Microsoft ever enforces strict rotation for public clients, workstations would have to coordinate. That is detectable as `invalid_grant` storms; the fallback is to write on every rotation with a `Signalods`, as the old code did.
- **Dependency conflicts.** MSAL 4.9x needs newer `System.Text.Json` (6.0.10, shipped 6.0.9) and `IdentityModel.Abstractions` (8.14.0, shipped 6.23.1) than `Required dlls` holds, plus `System.Formats.Asn1`, which isn't shipped at all (4.1). Pin 4.47.1 if the NuGet move isn't ready.
- **Sending duplicates.** A 5xx or a timeout after Graph accepted the message: we never auto-retry `sendMail`, so the user may see a failure for a message that was in fact sent. The message stays in Sent Items, and the error text says to check Sent Items.
- **Bcc via MIME `sendMail`** is expected to work (Exchange strips the header), but Graph doesn't document it explicitly. It is check 6.2.4. If it fails, use the JSON path for messages with Bcc.
- **`EmailUsername` vs UPN.** An alias, or a UPN that differs from the primary SMTP address, can fail the account match. `proxyAddresses` needs a broader scope, so the check uses `mail` or the UPN and lets the admin edit `EmailUsername` before signing in.
- **Basic SMTP retirement** (2.6) may push more offices onto this path at once. That is a support load, not a code risk.
- **System browser on locked-down terminals.** Some RDS hosts block localhost listeners or the default browser. The device-code fallback exists; WAM is open question 5.

### 7.3 Open questions

1. **Inbox download has no host.** `ReceiveFromInbox` was driven by the eConnector, which is cut. Options:
   - a timer in the client (`FormOpenDentalThreads`), elected to one workstation by `EmailInboxCheckInterval`
   - an OpenDentalService task
   - drop "Download incoming emails" for Microsoft (and Gmail) for now

   `Mail.Read` should only be requested once one of these exists.
2. **Per-clinic registrations.** One office-wide client ID is assumed (ClinicNum 0). Multi-org groups with separate tenants would need per-clinic properties via `GetPropValForClinicOrDefault`.
3. **Large attachments.** They need `Mail.ReadWrite` (draft path). Options: accept that scope, cap Microsoft messages at about 2.9 MB of MIME with a clear error, or use SMTP AUTH with XOAUTH2 (MailKit is already in the repo, scope `https://outlook.office.com/SMTP.Send`, about 35 MB). The last option needs SMTP AUTH enabled per mailbox, which many tenants turn off.
4. **Sending as a shared mailbox or alias** (`SenderAddress`): it needs `Mail.Send.Shared` and Send As rights in Exchange. In scope or not?
5. **WAM broker** (`Microsoft.Identity.Client.Broker`) gives better SSO on Windows 10+ and no localhost listener. Add it as a third sign-in method later?
6. **GCC / GCC High tenants** (US government clouds) need different authority and Graph hosts. Out of scope unless an office needs them.
7. **Sign-in button artwork.** Keep Microsoft's sign-in image (allowed by Microsoft) or use a plain text button (fully neutral)? The spec assumes plain text.
8. **Shared helpers.** `Programs.InsertIfMissing` and `ProgramProperties.InsertIfMissing` should be agreed once across the vendor-direct specs (see `postcardmania.md` 7.3.7).
