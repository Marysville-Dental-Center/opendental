# Google (Gmail OAuth email): direct-to-vendor spec

Status: draft, 2026-10-18, fact-checked against Google's public docs and the repo the same day. Baseline: `main` at `6855026f5`. Vendor: Google LLC. APIs: **Google OAuth 2.0 for desktop apps** and the **Gmail API v1** (both public).

## Summary

- **What it is.** In Setup > Email, an office can set up an email address with "Gmail" authentication instead of a password. Outgoing mail (statements, recall, billing, patient email, acknowledgments) then goes out through the Gmail API as that mailbox. With "Download incoming emails" ticked, messages can also be pulled from the mailbox into the email inbox.
- **What HQ provided.** Open Dental's **Google OAuth client** and the token exchanges:
  - The authorize-URL query string (client ID, scopes, `access_type`) from `BuildOAuthUrl(regKey,"GoogleLoopbackIpAddressFlow")`, gated on a valid registration key.
  - The code-for-token exchange and **every refresh**, through `GetGoogleAccessToken(payload)`. HQ held Open Dental's client secret and posted to Google's token endpoint for us.
- **State on `main`.** Both HQ calls throw `HqNotAvailableException`. No office can authorize Gmail, and an address that already has tokens stops sending within about an hour, because the refresh also went through HQ. Tokens carried over from an Open Dental database belong to **Open Dental's** client and can't be refreshed by anyone else.
- **The rest is already direct.** Sending (`users.messages.send`) and reading (`users.messages.list/get`) already call `gmail.googleapis.com` from the workstation through Google's Apache-2.0 .NET client 1.44 (`Required dlls/Google.Apis*.dll`). PKCE, `state` and a loopback listener are already in-repo source in `Google.AuthorizationRequest`. **No Bridges.dll type is involved.**
- **Google Drive: not applicable.** `OpenDentalCloud` has only Dropbox and SFTP back ends, `DataStorageType` has no Google value, and no HQ method mentions Drive. There is nothing to re-implement.
- **Direct design:**
  - Each office creates its **own Google Cloud project and "Desktop app" OAuth client**. A Google Workspace office sets the consent screen to **Internal**: no verification, no "unverified app" screen, and no 7-day token expiry.
  - Auth: system browser, **loopback redirect on `127.0.0.1` and a random free port**, **PKCE S256** and `access_type=offline`. The token exchange goes straight to `oauth2.googleapis.com`.
  - A revoke ends the Google user's whole grant to the project, so the app never revokes a grant that another configured address still uses (3.2).
  - Scopes: `gmail.send` always; `gmail.readonly` only when the address downloads mail; plus `openid email`, used to check that the right account was authorized.
  - The client ID and secret live in a new, disabled-by-default **"GmailApi" program row**, inserted at runtime. The secret is encrypted with `osd1$`.
  - Each address keeps its **refresh token encrypted (`osd1$`) in the existing `emailaddress.RefreshToken` column**. Access tokens live only in memory: each process refreshes on its own, and refreshes no longer write to the database.
- **Pre-existing defects to fix on the way:**
  - the send pre-check calls `getProfile`, which a send-only scope can't call
  - the loopback wait has no timeout or cancel
  - `login_hint` isn't URL-encoded
  - the response page loads opendental.com assets
  - the UID filter strips leading hex characters
  - sign-out never revokes the grant
- **Feasibility: public API.** No partner agreement or NDA documents are needed. The real gates are on the office's side:
  - `gmail.readonly` is a **restricted** scope. For an External app it means the unverified-app warning and a 100-user lifetime cap, or full verification. Internal apps skip both.
  - PHI by email needs **Google Workspace with the HIPAA BAA accepted**. A consumer @gmail.com account can't be covered.
- **Size: M**, about 4 to 5 days.

## 1. What the integration does, and what HQ provided

### 1.1 What the office uses it for

- **Choosing it.** `OpenDental/Forms/FormEmailAddressEdit.cs` offers three authentication radios: Password, Gmail (`OAuthType.Google`=1) and Microsoft (2). For Gmail it shows:
  - a "Sign in with Google" image button (`butAuthGoogle`, resources `google_signin_*.png`)
  - read-only Access Token and Refresh Token boxes
  - "Download incoming emails", "Get unread mail only" and a free-text Gmail search query (`emailaddress.QueryString`)
- **Data model (unchanged by this spec).** The `emailaddress` table holds:
  - `AccessToken` varchar(2000)
  - `RefreshToken` TEXT
  - `AuthenticationType`, `DownloadInbox` and `QueryString`

  Both tokens are stored **in plain text** today.
- **Sending.** All outgoing mail goes through `EmailMessages.SendEmailUnsecure`. Callers include `FormBilling`, `FormRecallList`, `BillingL` and every email form. For a Google address it:
  1. checks the token with `gmailService.Users.GetProfile(...)`
  2. on 401 calls `RefreshGmailToken` (HQ) and retries once
  3. calls `Email.SendEmail.WireEmailUnsecure`, which builds the MIME message with MimeKit, base64url-encodes it and calls `users.messages.send` (`OpenDentBusiness/Email/SendEmails.cs`, `SendEmailOAuth`, `CreateGmailMsg`)
- **Receiving.** `EmailMessages.ReceiveFromInbox` → `RetrieveFromInboxOAuth` → `RetrieveFromGmailInbox`:
  - pages `messages.list` (500 per page, `q`=QueryString, spam and trash excluded)
  - skips IDs already in `emailmessageuid`
  - fetches each new message with `format=raw` and imports it through `ProcessRawEmailMessageIn`

  **Nothing in this repo calls `ReceiveFromInbox`**, and none did at the 24.3.1 import (`git log -S`). In Open Dental the inbox poll ran in the separately shipped OpenDentalService, which is not in this repo. So today the download half is dead code for every account type. See open question 2.
- **Bearer plumbing.** `OpenDentBusiness/WebBridges/Google/GoogleApiConnector.cs` wraps the access token in `ODGoogleUserCredential`, an `IHttpExecuteInterceptor` that adds `Authorization: Bearer`, so that the Google client library never needed Open Dental's client secret.

### 1.2 What Open Dental's HQ provided

| Step | Code on `main` | What HQ did (inferred from the calls) | Now |
|---|---|---|---|
| Authorize URL | `Google.AuthorizationRequest.GetQueryParamsFromHQ()`: `BuildOAuthUrl(PrefName.RegistrationKey, "GoogleLoopbackIpAddressFlow")`. The client appends `login_hint`, `state`, `code_challenge`, `code_challenge_method=S256` and `redirect_uri` | Returned the fixed part of the query: **Open Dental's client ID**, the scopes, `response_type=code` and offline access. The code comment says it "includes the OD client ID for google and all other params that never change". It refused callers without a valid registration key. The real scope list isn't visible in the repo; the demo mock's `openid profile` is a placeholder | Throws (`WebServiceMainHQNoHq.BuildOAuthUrl`) |
| Code exchange | `Google.GetAccessTokenHqOrThrow(code, isRefresh:false, redirectUri, codeVerifier)`, sent as an `eServiceCode.OAuth` payload to `GetGoogleAccessToken` | Posted `grant_type=authorization_code` with **Open Dental's client ID and secret**, our code, verifier and redirect URI. It used the public helper `Google.GetAccessToken(body)`, whose comment says "Called from WebServiceMainHQ". It returned a `GoogleToken {AccessToken, RefreshToken, ErrorMessage}` | Throws (`VendorNotAvailable("Google")`) |
| Refresh | `Google.MakeRefreshAccessTokenRequest(refreshToken)` → `GetAccessTokenHqOrThrow(refreshToken, isRefresh:true)` | Same web method, with `grant_type=refresh_token` and Open Dental's secret | Throws, so every Gmail address dies at access-token expiry |
| Legacy OOB URL | `GetGoogleAuthorizationUrl(long)`, `OAuthApplicationNames.Google` | The copy-paste (OOB) flow that Google retired. No caller in the repo besides the demo mock | Throws |
| Loopback URL | `GetGoogleAuthorizationUrlLoopbackIpAddressFlow(long)` | Alternate form of the query-string call. No caller besides the mock | Throws |
| Browser response page | `Properties.Resources.GoogleAuthCodeResponseHtml` (OpenDentBusiness) | No HQ call, but the page is Open Dental-branded ("Open Dental Software - Google Code") and loads `opendental.com/style.css` and images from opendental.com | Still served, so it contacts opendental.com |
| Cloud relay | `ODCloudClient.ODCloudAuthGoogleListener`, `SendListenerResponse`, `GetRedirectUri` when `ODEnvironment.IsCloudServer` | Ran the listener on the user's PC under Open Dental's hosted cloud (Thinfinity/AppStream) | Dead in this fork. The plan deletes the ODCloud paths |
| API calls | `GoogleApiConnector.CreateGmailService` + Google.Apis.Gmail.v1 | No HQ role | Still works while a token is valid |

Consequences:

- **No Gmail address can be authorized or kept alive.** Nothing short of a new OAuth client fixes that.
- **Migrated tokens are void.** A refresh token issued to Open Dental's client can only be refreshed with Open Dental's client secret, so a database migrated from Open Dental carries refresh tokens no one else can use (section 3.9).
- **Compiled-in credentials.** `WebServiceMainHQMockDemo` embeds the client ID and secret of Google's public sample app. That conflicts with the "never compiled in" rule and goes away with the Google mock members (4.4).

### 1.3 Defects found while reading the code (fix in the same change)

1. **The send pre-check needs a read scope.** `SendEmailUnsecure` calls `users.getProfile` to test the token. That method accepts only `mail.google.com`, `gmail.modify`, `gmail.compose`, `gmail.readonly` or `gmail.metadata`, not `gmail.send` ([getProfile](https://developers.google.com/workspace/gmail/api/reference/rest/v1/users/getProfile)). A least-privilege send-only grant would always fail. Replace it with local expiry tracking plus refresh-on-401 (section 3.4).
2. **The loopback wait can hang forever.** `HttpListener.GetContext()` has no timeout. If the user closes the browser tab, the progress window waits forever, and Cancel can't interrupt the blocking call.
3. **The port search is slow.** `StartListener` probes ports 49152 to 65535 one by one with `HttpListener`. Binding a `TcpListener` to port 0 lets the OS pick a free one in a single call.
4. **`login_hint` isn't escaped** in `BuildAuthorizationUrl`. An address with `+` (sub-addressing) breaks the hint.
5. **The listener accepts any request.** It takes the first request on the port without checking its path or method. A stray request, such as a favicon fetch or a port scan, ends the flow with "Malformed authorization response".
6. **The UID filter strips hex characters.** `RetrieveFromGmailInbox` runs `TrimStart("GmailId".ToCharArray())` on stored UIDs. That strips any leading `G m a i l I d`, including the hex digits `a` and `d`. A stored ID that starts with them no longer matches, and the message is downloaded again on every poll.
7. **Every poll lists the whole mailbox.** With a blank query, `messages.list` pages through the entire mailbox each time before filtering out known IDs.
8. **Sign-out doesn't revoke.** `butClearTokensGmail_Click` only blanks the boxes, so the grant stays live at Google until it expires.
9. **The token endpoint URL is legacy.** `GetAccessToken` (the HQ-side helper) posts through `Request` to `UrlEndpoint.AccessToken`, which is `www.googleapis.com/oauth2/v4/token`. The documented endpoint is `oauth2.googleapis.com/token`. `UrlEndpoint.RefreshToken` already holds that URL but nothing uses it.

## 2. The vendor's public API

### 2.1 Documentation status

The documentation is public, complete and free:

- [OAuth 2.0 for desktop apps](https://developers.google.com/identity/protocols/oauth2/native-app)
- [OAuth 2.0 overview](https://developers.google.com/identity/protocols/oauth2), which covers refresh token expiry
- [OpenID Connect](https://developers.google.com/identity/openid-connect/openid-connect)
- [Gmail API scopes](https://developers.google.com/workspace/gmail/api/auth/scopes)
- the [Gmail API reference](https://developers.google.com/workspace/gmail/api/reference/rest)
- [Gmail usage limits](https://developers.google.com/workspace/gmail/api/reference/quota)
- [Gmail error handling](https://developers.google.com/workspace/gmail/api/guides/handle-errors)
- the [Google API Services User Data Policy](https://developers.google.com/terms/api-services-user-data-policy)

There is **no partner program and no sandbox host**. Testing uses a separate Google Cloud project and test Google accounts (section 6.1).

### 2.2 Base URLs

| Use | URL | Notes |
|---|---|---|
| Consent page (system browser only) | `https://accounts.google.com/o/oauth2/v2/auth` | Embedded web views are refused with `disallowed_useragent` ([desktop apps](https://developers.google.com/identity/protocols/oauth2/native-app)) |
| Token endpoint | `POST https://oauth2.googleapis.com/token` | Code exchange and refresh |
| Revoke | `POST https://oauth2.googleapis.com/revoke` | `token=` an access or refresh token |
| Gmail REST | `https://gmail.googleapis.com/gmail/v1/users/{userId}/...` | `userId` = the address or `me` |
| Gmail media upload | `https://gmail.googleapis.com/upload/gmail/v1/users/{userId}/messages/send` | For large raw messages ([send](https://developers.google.com/workspace/gmail/api/reference/rest/v1/users.messages/send), [uploads](https://developers.google.com/workspace/gmail/api/guides/uploads)) |
| Sandbox | none | The same hosts, used with a test project and test accounts |

### 2.3 Authentication, step by step (desktop app, loopback, PKCE)

Google classes desktop apps as public clients that can't keep a secret confidential, and tells developers to always use PKCE (its parameter table lists `code_challenge` as recommended, not required). The loopback IP redirect is the recommended method for desktop apps on Windows, macOS and Linux. The manual copy/paste (OOB) method is deprecated and unsupported, and custom URI schemes are no longer supported either ([desktop apps](https://developers.google.com/identity/protocols/oauth2/native-app)).

1. **Make a code verifier.** Generate a random `code_verifier` of 43 to 128 unreserved characters. 32 random bytes, base64url without padding, gives 43. The `code_challenge` is base64url(SHA-256(verifier)), with `code_challenge_method=S256`. If `code_challenge_method` is left out, Google defaults to `plain`, so always send it.
2. **Make a state value.** Generate a random `state` for CSRF protection.
3. **Start the listener.** Start an HTTP listener on `127.0.0.1` and a **random free port**. The redirect URI is `http://127.0.0.1:<port>/<path>`. Google documents a random available port, and a Desktop client needs no redirect URI registered. Google's page shows the loopback form without a path. RFC 8252 allows one, and today's code uses `/`; test 6.2 confirms that `/oauth2` is accepted, and falls back to `/` if it isn't. `localhost` also works, but Google warns it can trip client firewalls.
4. **Open the consent page** in the default browser:
   ```
   https://accounts.google.com/o/oauth2/v2/auth
     ?client_id=<office client ID>&response_type=code
     &redirect_uri=http%3A%2F%2F127.0.0.1%3A<port>%2Foauth2
     &scope=openid%20email%20https%3A%2F%2Fwww.googleapis.com%2Fauth%2Fgmail.send[%20...gmail.readonly]
     &code_challenge=<challenge>&code_challenge_method=S256&state=<state>
     &access_type=offline&prompt=consent&login_hint=<escaped address>
   ```
   - `access_type=offline` asks for a refresh token.
   - `prompt=consent` makes sure Google issues one on re-authorization too. Without it, Google can skip the refresh token when the user already granted access.
   - The desktop-app page lists the core parameters. The two above are Google's general web-server parameters, and their effect on a Desktop client is checked in test 6.2.
   - The page also says **incremental authorization is not supported for installed apps**. So request the full scope set each time, and re-authorize when the set changes.
5. **The user signs in and approves.** The browser is redirected to `?code=...&state=...`, or to `?error=access_denied&state=...` if the user declines.
6. **Exchange the code.** `POST /token`, form-encoded:
   - `grant_type=authorization_code`
   - `code`
   - `client_id`
   - `client_secret`
   - `code_verifier`
   - the same `redirect_uri`

   The desktop page marks `client_secret` as optional, but Google issues one for Desktop clients. We send it whenever it is configured (checked in 6.2).

   The response holds:
   - `access_token`
   - `expires_in` (seconds, typically 3599)
   - `refresh_token`
   - `scope`: the scopes **actually granted**, since the user can untick some
   - `token_type=Bearer`
   - `id_token`, because `openid` was requested
7. **Check the account.** Decode the `id_token` payload and read `email` and `email_verified` (both need the `email` scope). Google's OpenID Connect page says a token obtained straight from its token endpoint over HTTPS can be relied on without checking the signature, though it frames that around a client that authenticates with its secret, and requires validation if the token is handed to other components ([OpenID Connect](https://developers.google.com/identity/openid-connect/openid-connect)). Here the token comes back on our own TLS connection and is used once, locally, as a sanity check against picking the wrong account, not as proof of identity. So no JWKS validation is planned.
8. **Refresh.** `POST /token` with:
   - `grant_type=refresh_token`
   - `refresh_token`
   - `client_id`
   - `client_secret`

   The response has a new `access_token`, `expires_in` and `scope`. The refresh token itself isn't normally reissued.
9. **Revoke.** `POST /revoke` with `token=<refresh token>`. It returns 200, or 400 with an error, and can take a little while to take full effect. Revoking removes every scope the user granted to the **project**, which invalidates that user's tokens for every client in the project, not only the one being revoked. Sections 3.2 and 3.4 account for that.

**When a refresh token stops working** ([OAuth overview](https://developers.google.com/identity/protocols/oauth2)). Every case below shows up as `invalid_grant` on refresh, and some carry an `error_subtype` such as `invalid_rapt`:

- **Testing status:** the project has an **External** audience in **Testing**, and the token expires **7 days** after consent. A request limited to the basic name, email and profile scopes (or their OpenID Connect equivalents) is exempt, but any Gmail scope removes the exemption.
- **Unused:** the token wasn't used for 6 months.
- **Password change:** the user changed the account password, and the token has Gmail scopes.
- **Revoked:** the user removed access, or an admin restricted the service (`admin_policy_enforced`).
- **Session control:** a Workspace admin's session-length policy expired.
- **Too many tokens:** the account holds more than **100 live refresh tokens for one client ID**, and the oldest is silently dropped. Google also has a larger, unpublished cap across all clients for one account.
- **Time-based access:** the user granted access for a limited time, and it has run out.

### 2.4 Endpoints and fields we need

| Use in this repo | Endpoint | Scope | Fields |
|---|---|---|---|
| Send any outgoing email (`SendEmailOAuth`) | `POST users/{id}/messages/send` (or the upload URI with `uploadType=media`, `Content-Type: message/rfc822`) | `gmail.send` | `raw`: the RFC 2822 message, base64url. The response gives `id`, `threadId` and `labelIds`. At most **500 recipients** per message ([send](https://developers.google.com/workspace/gmail/api/reference/rest/v1/users.messages/send), [quota](https://developers.google.com/workspace/gmail/api/reference/quota)) |
| List new inbox messages | `GET users/{id}/messages` | `gmail.readonly` | in: `maxResults` (up to 500), `pageToken`, `q`, `includeSpamTrash=false`. out: `messages[].id`, `nextPageToken`. `q` is not allowed with `gmail.metadata` ([list](https://developers.google.com/workspace/gmail/api/reference/rest/v1/users.messages/list)) |
| Fetch one message | `GET users/{id}/messages/{msgId}?format=raw` | `gmail.readonly` | `raw`, base64url. Decoded and loaded by MimeKit ([get](https://developers.google.com/workspace/gmail/api/reference/rest/v1/users.messages/get)) |
| Confirm the authorized account | `id_token` from the token endpoint | `openid email` | `email`, `email_verified`. This replaces the `getProfile` check (defect 1) |
| Sign-out | `POST oauth2.googleapis.com/revoke` | none | `token` |

Today's code relies on two encoding details:

- **Gmail returns `raw` as base64url.** `RetrieveFromGmailInbox` maps `-`/`_` back to `+`/`/`, but it never restores `=` padding. `Convert.FromBase64String` fails on unpadded input, so add the padding before decoding (test 6.4).
- **`CreateGmailMsg` encodes the message as base64url.** It swaps the characters and keeps the padding, which Google accepts.

### 2.5 Rate limits and sending limits

- **API quota** ([usage limits](https://developers.google.com/workspace/gmail/api/reference/quota)):
  - 1,200,000 quota units per minute per project
  - **6,000 units per minute per user**
  - The costs that matter here:
    - `messages.send` 100 units
    - `messages.list` 5 units
    - `messages.get` 60 units with `format=raw`
    - `getProfile` 1 unit
  - So one mailbox can send about **60 messages a minute** through the API and fetch about 100 raw messages a minute.
  - `messages.get` costs 20 units in its lighter formats. It is the `raw` (also `full`/`html`) format this code uses that costs 60.
  - The page dates these limits to 1 May 2026: projects created from then on get them, while projects already using the API between November 2025 and April 2026 keep their old quotas. It also sets a daily threshold of 80,000,000 units per project, under which nothing extra is charged, and says charges for exceeding quota are planned for later in 2026, with at least 90 days' notice. A new per-office project falls under these rules. One office's volume is far below them.
- **Gmail's own sending caps still apply.** They are separate from the API quota ([Workspace sending limits](https://knowledge.workspace.google.com/admin/gmail/gmail-sending-limits-in-google-workspace)):
  - Paid Workspace, per user over a rolling 24 hours: 2,000 messages (1,500 for mail merge), 10,000 total recipients, 3,000 external recipients, and 3,000 unique recipients (2,000 of them external).
  - Trial Workspace: 500 messages a day and 500 unique external recipients, until the domain has paid at least USD 100.
  - The Gmail API allows at most 500 recipients per message.
  - Consumer Gmail limits are lower and aren't on that page.
  - A user who goes over can't send for up to 24 hours.
  - Statement and recall batches must expect a 429 partway through and leave the rest unsent, not retry in a loop.
- **Errors** ([handle errors](https://developers.google.com/workspace/gmail/api/guides/handle-errors)):
  - 401: refresh, then re-authorize.
  - 403 `rateLimitExceeded` / `userRateLimitExceeded`: API rate limits, so exponential backoff. 403 `dailyLimitExceeded` means the project quota is used up.
  - 403 `domainPolicy`: the Workspace domain blocks Gmail apps. Only the domain admin can fix it.
  - 429: the mail sending limits (the response carries a retry time), per-user bandwidth limits, or too many concurrent requests for one user. All of these are shared by every client of that mailbox, and none can be raised.
  - 5xx: backoff, starting at least 1 s after the error and doubling.
  - Google warns that a 200 from `send` doesn't guarantee the message went out. So "sent" in the PMS means "accepted by Gmail".

### 2.6 Terms that matter

- **Scope classes** ([Gmail scopes](https://developers.google.com/workspace/gmail/api/auth/scopes)). `gmail.send` is **sensitive**. `gmail.readonly`, `gmail.modify` and `mail.google.com/` are **restricted**. A send-only office stays in the sensitive class.
- **Verification and when it is not needed.**
  - **Internal audience** ([verification exceptions](https://support.google.com/cloud/answer/13464323), [audience](https://support.google.com/cloud/answer/15549945)): the project is owned by the office's Workspace or Cloud Identity organization, and only its members can authorize. It needs no verification, shows no unverified-app screen, and has no 100-user cap. Restricted scopes may need a Workspace admin to approve the app.
  - **External audience in Testing:** at most 100 listed test users, an unverified-app warning, and authorizations, refresh tokens included, that **expire after 7 days**. That is not usable in practice.
  - **External audience in production, unverified:** allowed for personal use with fewer than 100 users. Users click through the unverified-app warning, and there is a **lifetime cap of 100 new users** for sensitive or restricted scopes. A per-office app needs one user per mailbox, so the cap isn't a practical limit. The office still owns the decision to publish an unverified app.
  - **Full verification:** restricted scopes also need a security assessment when the data is stored on or sent through servers. A per-office, desktop-only app that doesn't seek verification never gets there. A project-wide app shared by many offices would need full restricted-scope verification and that assessment (open question 4).
- **User Data Policy** ([policy](https://developers.google.com/terms/api-services-user-data-policy)). It applies whether or not the app is verified:
  - **Limited Use:** Gmail data is used only for the visible feature, here importing mail into the patient record.
  - Transfers only to provide that feature (with the user's consent), for security, to comply with law, or in a merger with prior consent. No sale to ad platforms or data brokers, and no advertising or credit use.
  - A person reads the data only with the user's explicit agreement, for security investigations, for legal compliance, or in aggregated form for internal operations.
  - Data must be secured in transit and at rest.
  - Client credentials are kept confidential. That rule sits in the policy's "identity and intent" section.
  - The policy's note on the extra scope-specific requirements says they don't apply to an app used only by users within the developer's own domain. It names no exemption for personal use, so a consumer-Gmail office with an External app should assume those requirements apply on paper, even though it never undergoes the assessment that enforces them.
- **PHI and BAA** ([HIPAA with Google Workspace](https://knowledge.workspace.google.com/admin/compliance/hipaa-compliance-with-google-workspace-and-cloud-identity), [HIPAA on Google Cloud and Workspace](https://docs.cloud.google.com/docs/security/compliance/hipaa)):
  - An administrator reviews and accepts the BAA in the Admin console. The record is under Account settings > Legal and compliance.
  - Gmail and Drive are on the list of Workspace services that the BAA covers. The old "HIPAA Included Functionality" URL now redirects to the Google Cloud HIPAA page.
  - Without the BAA, PHI must not be used in Workspace or Cloud Identity. The BAA is offered only through the Workspace/Cloud Identity Admin console, and Google's pages don't offer one for consumer Gmail, so a consumer @gmail.com account has no BAA path.
  - Third-party apps and add-ons are outside the BAA. Here the office's own desktop software talks to Gmail directly, with no third-party server in between, and with HQ gone Open Dental no longer touches the tokens.
- **Branding.** The current control shows Google's "Sign in with Google" artwork. Google's [branding guidelines](https://developers.google.com/identity/branding-guidelines) frame that button as the user signing in to *your app* with a Google account. They don't explicitly forbid it for API authorization, but they do require Google's written consent for brand uses the guide doesn't cover. Here the flow grants Gmail access rather than signing anyone in to the PMS, and the artwork is a vendor logo in our UI. Either way, ground rule 7 decides it: replace it with a plain text button.

## 3. Desktop design

### 3.1 Credential model

- **Each office creates its own Google Cloud project** with the Gmail API enabled and one OAuth client of type **Desktop app** (checklist in section 5).
  - With a Workspace domain the audience is **Internal**. Otherwise it is External, published to production and left unverified (2.6).
  - The office's credentials are the **client ID**, which is public, and the **client secret**. Google says a Desktop client secret isn't really secret, but it is still never compiled in or committed. The secret is stored encrypted.
- **One client for the whole office.** Every Gmail address in the office (practice, clinic and per-user addresses) authorizes against that one client.
- **The authorizing user must be signed in as the mailbox.** Each address grants its own refresh token, and the person authorizing must be signed in to Google as that mailbox.
- **A project-wide client is possible later**, but it brings verification and the restricted-scope security assessment. Not proposed (open question 4).

### 3.2 Auth flow in the WinForms app

`FormEmailAddressEdit` keeps its layout. The Gmail group changes as follows.

**Button and gating:**

- `butAuthGoogle` is a `Label` that shows an image today (`FormEmailAddressEdit.Designer.cs`). It is replaced by a normal `UI.Button` reading **"Authorize..."**, or "Re-authorize..." when the address already holds a token. The three `google_signin_*.png` resources are no longer used.
- If the "GmailApi" program has no client ID, the button shows "Enter the OAuth client ID in Setup > Program Links > Gmail API first." and stops.

**Flow,** on a background task under the existing `ProgressWin`, which gains a working Cancel:

1. **Scopes.** Choose them from the form: `openid email gmail.send`, plus `gmail.readonly` if "Download incoming emails" is ticked.
2. **Listener.** Create the PKCE pair and `state`, in memory only. Start `LoopbackRedirectListener` (new) as follows:
   - a `TcpListener` on `IPAddress.Loopback`, port 0, so the OS picks a free port
   - path `/oauth2`
   - one `GET` accepted; the wrong path, the wrong method or a missing `state` gets a 404, and the listener keeps waiting (defect 5)
   - **a 5-minute timeout**, plus a `CancellationToken` tied to Cancel (defect 2)

   `TcpListener` avoids `HttpListener`'s http.sys URL reservations, and it matches the Dropbox spec's listener, so the two share one class.
3. **Browser.** Build the authorize URL (2.3, step 4) with every value `Uri.EscapeDataString`'d, `login_hint` included (defect 4). Open it with `Process.Start` in the default browser.
4. **Callback.** Parse `code`, `state` and `error`, and reject a `state` that doesn't match.
   - Reply with a **small self-contained HTML page**: no external assets, no vendor or project branding, and the text "Authorization finished. You can close this tab and return to the program." An `error` gets a page with the same look that names the error code.
   - The page replaces the `GoogleAuthCodeResponseHtml` resource.
5. **Exchange** the code (2.3, step 6) with `HttpClient`, honoring the cancellation token.
6. **Account and scope check:**
   - The `id_token` `email` must equal `textUsername.Text`, ignoring case. If it doesn't, show "You authorized <email>, but this address is <username>. Sign in to Google as <username> and try again." and discard the new tokens.
   - Revoke the stray grant **only if no other `emailaddress` row** in the office is a Google address for `<email>`. A revoke kills that Google user's whole grant to the project (2.3, step 9), so it would also sign out a mailbox that was authorized correctly earlier. If such a row exists, just discard the tokens.
   - If "Download incoming emails" is ticked but the granted `scope` lacks `gmail.readonly`, warn "Read access was not granted; this address can send but not download mail" and untick it.
7. **Fill the form.** Keep the new refresh token, the granted scopes and the expiry in form fields. `textAccessTokenGmail` and `textRefreshTokenGmail` are **removed**, and a read-only status label shows "Authorized as <email>". Prime `GoogleTokenCache` with the access token. Nothing touches the database until **Save**, as today.
8. **On Save** (`butSave_Click`), for a Google address:
   - `CDT.Class1.Encrypt(refreshToken, out string encrypted)`, then `RefreshToken = encrypted`. Save is refused if `Encrypt` returns false. Never use `TryEncrypt`, because it returns the plain text when encryption fails.
   - `AccessToken = ""`
   - `DownloadInbox` and `QueryString` as today
   - a security log entry under `EnumPermType.Setup`, the permission that gates Setup > Email (`FormOpenDental.menuItemEmail_Click`); there is no `EmailSetup` permission. The text is "Gmail authorized for EmailAddressNum <n>". The log holds no token and no address.

**Changing the download setting.** Ticking "Download incoming emails" on an address that was authorized without `gmail.readonly` prompts: "Downloading needs read access. Re-authorize now?" Yes runs the flow above. No unticks the box. The granted scopes come from the most recent token response, so nothing extra is stored (3.3).

**Sign-out** (`butClearTokensGmail_Click`, and switching the radio away from Gmail):

- Decrypt the stored refresh token and `POST /revoke` it in the background, best effort. A 400 (already revoked) or a network error is ignored (defect 8).
- Skip the revoke if another `emailaddress` row is a Google address for the same mailbox (`EmailUsername`), since the revoke would sign that row out too (2.3, step 9).
- Then blank the form values.
- If the address is the practice or clinic default, warn that email from it stops until someone authorizes again.

### 3.3 Where credentials and tokens live

**Office client, in a new program row.** There is no Gmail program row today, so one is added as `ProgramName.GmailApi`:

- It is inserted **at runtime** by `GoogleOAuthSettings.EnsureProgram()` (OpenDentBusiness), with `Enabled=false`, `ProgDesc="Gmail API"`, no path and no toolbar button. It returns `true` when it inserted something, and then calls `Signalods.SetInvalid(InvalidType.Programs)`.
- It is called from `FormProgramLinks.FillList()` and from `FormEmailAddress_Load` (the actual name of the edit form's Load handler). When it returns `true`, the UI caller also runs `DataValid.SetInvalid(InvalidType.Programs)`. `DataValid` lives in `WpfControlsOD`, so OpenDentBusiness can't call it (same split as the PostcardMania spec).
- After inserting, it re-reads and deletes any duplicate row with the higher `ProgramNum`, which covers the race between two workstations.
- The helper is the same `Programs.InsertIfMissing` / `ProgramProperties.InsertIfMissing` pair that the PostcardMania spec proposes.
- There is no schema change and no ConvertDatabases step.

| PropertyDesc | Value | Encrypted | IsMasked | IsHighSecurity |
|---|---|---|---|---|
| `Client ID` | `<n>-<id>.apps.googleusercontent.com` | no (public identifier) | no | no |
| `Client Secret` | `osd1$...` from `CDT.Class1.Encrypt` | **yes** | **yes** | **yes** |

Both rows use `ClinicNum=0`, so one client covers every clinic. Readers treat a missing row as blank.

**Per address, in existing `emailaddress` columns:**

| Column | Value for `AuthenticationType=Google` |
|---|---|
| `RefreshToken` (TEXT) | `osd1$...`: the encrypted refresh token. A non-empty value **without** the `osd1$` prefix is a legacy Open Dental token and is treated as "needs re-authorization" (3.9) |
| `AccessToken` (varchar 2000) | Always `""` for Google rows from now on. The access token lives only in memory |
| `DownloadInbox`, `QueryString`, `EmailUsername` | As today |

Notes:

- **Not stored:** access tokens, the PKCE verifier, the `state`, the `id_token` and the granted-scope list. The scope list comes back on every refresh, so `GoogleTokenCache` knows it at runtime.
- **Decryption happens client side.** Code in `OpenDentBusiness` calls `CDT.Class1.Decrypt` just before a token request. `BasicEmailAddress` carries only the fresh access token into `SendEmails`, so the email layer stays free of DB and CDT dependencies, as its comment requires.
- **Middle Tier.** The rows travel in the existing EmailAddresses and ProgramProperties caches, still encrypted. All token calls run on the client, as today (`Meth.NoCheckMiddleTierRole`).
- **`osd1$` is obfuscation, not secrecy.** Its key is public by design (`CDT/README.md`). Anyone who can read `emailaddress` and `programproperty` can recover a refresh token plus the client secret, and with them send mail as the practice (and read it, if `gmail.readonly` was granted). Mitigations:
  - least-privilege scopes (send-only unless downloading)
  - high-security masking of the secret
  - database access control
  - revocation from the Google Account's [third-party connections](https://myaccount.google.com/connections) page
  - for Workspace, the Admin console's app access control

### 3.4 Token runtime and refresh

New `OpenDentBusiness/WebBridges/Google/GoogleTokenCache.cs`:

- **One entry per process per address,** keyed by `EmailAddressNum` plus a SHA-256 of the encrypted refresh token. When the token changes, the entry changes.
- **Each entry holds** the access token, its expiry (`now + expires_in`), the granted scopes, and a `NeedsReauthorization` flag.
- **`GetAccessToken(EmailAddress)`:**
  - Returns the cached token if it is valid for more than 5 more minutes.
  - Otherwise refreshes (2.3, step 8), **single-flight** behind a per-entry `SemaphoreSlim`, so a statement batch triggers only one refresh.
  - Throws `GoogleAuthException(NeedsReauthorization)` without any network call when the refresh token is blank or legacy, or when the flag is already set.
- **`SendEmailUnsecure` changes:**
  - Drop the `getProfile` pre-check (defect 1).
  - Call `GoogleTokenCache.GetAccessToken` and put the result in `BasicEmailAddress.AccessToken` before `WireEmailUnsecure`.
  - On a 401 from `send`, call `Invalidate`, refresh once and retry once. This keeps today's single `hasRetried` retry.
- **`RetrieveFromGmailInbox`** does the same around `list` and `get`.
- **`RefreshGmailToken` is rewritten:** no database read, no `EmailAddresses.Update`, no `Signalods`. Refreshes never write to the database.
- **Why per-process refresh is safe.** Each workstation refreshes independently with the same refresh token. That is safe because:
  - refreshing doesn't issue a new refresh token
  - refreshing doesn't count toward the 100-tokens-per-client limit, which applies to grants
  - one office makes a few refreshes per workstation per hour of activity

  Today's design, where the last writer wins on `emailaddress.AccessToken` and cache invalidation signals fly between workstations, goes away.
- **After re-authorization,** the `InvalidType.Email` signal from Save reloads the cache in every process. The new encrypted value gives a new key, and so a fresh entry.

### 3.5 Errors and offline behaviour

| Condition | Detected by | Behaviour |
|---|---|---|
| Program not set up (no client ID) | `GoogleOAuthSettings.IsConfigured` | Authorize shows the Program Links hint. Sending from a Google address fails with "Gmail sending is not configured. An administrator can set it up in Setup > Program Links > Gmail API." No network call is made |
| Address not authorized, or a legacy token | blank or non-`osd1$` `RefreshToken` | Today's message is kept and reworded: "<address> needs to be authorized. Open Setup > Email, edit the address, and click Authorize." |
| Refresh rejected | `invalid_grant` (including `invalid_rapt`), `unauthorized_client` or `invalid_client` | Set `NeedsReauthorization` for the address, for this process. Show **one** message per address per session, not one per message in a batch. Later sends from that address fail fast. `invalid_client` adds "check the client ID and secret in Program Links" |
| Access token expired | 401 from Gmail | Transparent: refresh once, retry once |
| Missing scope | 403 `insufficientPermissions` on list or get | "This address was authorized for sending only. Re-authorize with Download incoming emails ticked." |
| Admin restriction | `admin_policy_enforced` at authorize time or on refresh, or 403 `domainPolicy` from Gmail | "Your Google Workspace administrator has restricted this app." |
| Sending limit | 429 on `send` whose reason is the mail sending limit (it carries a retry time) | **Not** retried in a loop. The message is marked failed as today. In a batch the remaining messages for that address are skipped with "Gmail sending limit reached for <address>", and other addresses carry on |
| API rate or concurrency limit | 403 `userRateLimitExceeded` / `rateLimitExceeded`, or a concurrency/bandwidth 429, on any call | Exponential backoff of 1, 2, 4 and 8 s with jitter, capped at 32 s. Then a send fails as today, and a download stops this poll |
| Project quota used up | 403 `dailyLimitExceeded` | Fail with "The Gmail API quota for this Cloud project is used up." No retry |
| Google 5xx | 500 to 504 | Interactive send: one retry after 1 s, then fail. Download: backoff as above |
| Offline (DNS, refused connection, TLS, timeout) | `HttpRequestException` or `TaskCanceledException` from the token endpoint or Gmail | "Gmail could not be reached." **Never** sets `NeedsReauthorization`, so a network blip never looks like revoked access. Sends fail, and the existing email-failed handling records them. Nothing else in the program depends on Gmail |
| Wrong account chosen at consent | `id_token` email ≠ address | Refused, with the new grant revoked (3.2) |
| User closes the browser | listener timeout or Cancel | The progress window closes with "Authorization was cancelled." The port is released |

- **Timeouts:** token calls 15 s; Gmail calls 100 s, the existing `HttpClient` and Google library default.
- **Logging:** endpoint, HTTP status, Google's `error` and `error_subtype`, and the address's `EmailAddressNum`. **Never** tokens, codes, verifiers, `id_token`s or message content.

### 3.6 Hosts contacted, and no HQ

- `accounts.google.com`: the browser only, for the consent page.
- `oauth2.googleapis.com`: token and revoke.
- `gmail.googleapis.com`: send, list and get.
- The workstation's own `127.0.0.1:<random port>` during authorization.

Nothing else is contacted: no `opendental.com` (the response page is replaced), no `IWebServiceMainHQ`, and no ODCloud relay. `tools/Test-NoHqHosts.ps1` should then pass for this path. TLS 1.2 is already forced in `CodeBase/Utilities/ODInitialize.cs`.

### 3.7 Vendor neutrality (ground rule 7) and gating (ground rule 6)

- **Off by default.** The `GmailApi` program row is inserted with `Enabled=false`.
- **The Gmail radio shows only when** `Programs.IsEnabled(ProgramName.GmailApi)` is true, **or** the address already has `AuthenticationType=Google`, so a migrated office still sees its own setting.
- **Labels are factual:** the radio "Gmail (OAuth)", and the buttons "Authorize..." and "Sign out". There is no Google logo, no "Sign in with Google" artwork and no sign-up text.
- **Setup > Program Links > Gmail API** opens `FormGmailApiSetup` (new) with:
  - Client ID and Client Secret boxes (the secret is masked and needs `ManageHighSecurityProgProperties` to edit)
  - the Enabled checkbox
  - one line: "Send and receive email through the Gmail API using your own Google Cloud OAuth client."

  No links or promotion.

### 3.8 Receiving mail

The download code is fixed but stays **manual** in v1, because nothing in this repo schedules it (1.1, open question 2):

- **Manual trigger.** `FormEmailInbox.GetMessages()` gains one call for the selected address when it is Google, has `DownloadInbox`, and isn't webmail: `EmailMessages.ReceiveFromInbox` on a background thread, followed by `FillGridInbox()`. It runs on form load and on the existing Refresh.
  - Place the call **before** the existing early return on a blank `Pop3ServerIncoming`. A Gmail OAuth address normally has no POP3 server, so code placed after that check would never run.
- **Default query.** If `QueryString` is blank, use `in:inbox newer_than:30d`, so a poll doesn't list the whole mailbox (defect 7). Show it as the placeholder in the query box.
- **Fix the UID filter.** Compare stored and listed IDs exactly, with no `TrimStart` (defect 6).
- **Fix the padding.** Restore base64 `=` padding before decoding `raw` (2.4).

### 3.9 Data migrated from Open Dental

On a restored `fox-db` copy:

- **Rows with `AuthenticationType=1`** hold plain-text tokens issued to Open Dental's client.
  - They are **not used**: `RefreshToken` lacks `osd1$`, which means legacy (3.3).
  - Sends from those addresses fail with the "needs to be authorized" message.
  - The administrator follows section 5, then re-authorizes each address. Save overwrites the legacy refresh token and blanks the access token.
- **Revoke Open Dental's access.** Each mailbox owner then removes Open Dental's app from their Google Account's [third-party connections](https://myaccount.google.com/connections). A Workspace admin can do it centrally under Security > API controls.
- **Optional cleanup.** A one-time data cleanup could blank legacy `AccessToken` values, so no live Open Dental bearer token sits in the database. It isn't needed for correctness, because the tokens expire within an hour of the last refresh, and HQ refreshes stopped at the cut.

## 4. Code changes

### 4.1 Library choice

| | **A. Keep Google.Apis 1.44 for Gmail calls (recommended for v1)** | B. In-repo REST client for 3 Gmail endpoints | C. NuGet `Google.Apis.Gmail.v1` (current) |
|---|---|---|---|
| What | OAuth becomes in-repo `HttpClient` code (it is mostly written already). Gmail calls stay on `GmailService` with `ODGoogleUserCredential` | `GmailRestClient`: `send`, `list` and `get` over `HttpClient` and Newtonsoft.Json 12.0.3. About 200 lines | The current Google client library, with its own `UserCredential` refresh |
| Dependencies | None new. The 6 Apache-2.0 DLLs stay, as `docs/third-party-binaries-audit.md` already allows | Drop all 6 `Google.Apis*.dll` (and the unused `using` in `Bridges/Pearl/IPearlApiClient.cs`) | Newer Newtonsoft.Json and a move to package references, repo-wide |
| Upside | Smallest change. The send and receive code is untouched apart from the token source | One less vendor library | Least code of our own |
| Downside | An old library version. `GoogleApiException` mapping stays | We own the error mapping, about 1 day | The repo-wide dependency upgrade |

Choose **A** now, and B whenever the binaries cleanup wants the DLLs gone. Either way the OAuth code is ours, because the 1.44 auth helpers would need a file-based token store and give us no control over timeouts, cancellation or the response page.

### 4.2 File-by-file changes

| File | Members | Change |
|---|---|---|
| `OpenDentBusiness/WebBridges/Google/Google.cs` | `GetApiUrl`, `UrlEndpoint` | Endpoints become `accounts.google.com/o/oauth2/v2/auth`, `oauth2.googleapis.com/token` and `oauth2.googleapis.com/revoke` (defect 9) |
| same | `GetAccessTokenHqOrThrow` | **Delete.** It is replaced by `ExchangeCodeAsync(clientId, clientSecret, code, verifier, redirectUri)` and `RefreshAsync(clientId, clientSecret, refreshToken)`, which post form-encoded bodies to the token endpoint |
| same | `GetAccessToken(string body)` ("Called from WebServiceMainHQ"), `Request<T>` | Delete `GetAccessToken`. Replace `Request<T>` (`WebClient`, plus a DEBUG-only branch that returns raw JSON) with `HttpClient` and typed parsing of the success and error bodies (`error`, `error_description`, `error_subtype`) |
| same | `MakeRefreshAccessTokenRequest` | **Delete.** `GoogleTokenCache` replaces it |
| same | new `RevokeAsync(token)`, `ParseIdTokenEmail(idToken)` | Sections 2.3 and 3.2 |
| same | `AuthorizationRequest`: `StartListener`, `MakeAccessTokenRequest(emailAddress)`, `BuildAuthorizationUrl`, `GetQueryParamsFromHQ`, `SendListenerResponse`, `GetAuthCodeFromContextOrThrow`, `GetRedirectUri`, `CloseListener` | Rewrite as `MakeAccessTokenRequest(emailAddress, scopes, CancellationToken)` over `LoopbackRedirectListener`. **Delete** `GetQueryParamsFromHQ` and every `ODEnvironment.IsCloudServer` / `ODCloudClient` branch. Keep the PKCE helpers, but switch `RNGCryptoServiceProvider` to `RandomNumberGenerator.Create()` and dispose it |
| same | `GoogleToken` | Add `ExpiresInSeconds`, `Scope`, `Email` and `ErrorCode`. The `[Serializable]` shape stays compatible |
| `OpenDentBusiness/WebBridges/Google/GoogleTokenCache.cs` (new) | `GetAccessToken(EmailAddress)`, `Invalidate(long emailAddressNum)`, `Prime(...)`, `GetGrantedScopes(...)`, `NeedsReauthorization(...)` | Section 3.4 |
| `OpenDentBusiness/WebBridges/Google/GoogleOAuthSettings.cs` (new) | `EnsureProgram()`, `GetClientId()`, `GetClientSecret()` (decrypts), `SetClientSecret(string)` (encrypts, refusing on failure), `IsConfigured`, `IsLegacyToken(string)`, scope constants | Section 3.3 |
| `CodeBase/Utilities/LoopbackRedirectListener.cs` (new; shared with the Dropbox spec) | `Start(path)`, `RedirectUri`, `WaitForCallbackAsync(expectedState, timeout, CancellationToken)`, `Dispose()` | Section 3.2, step 2. Serves the inline response HTML |
| `OpenDentBusiness/WebBridges/Google/GoogleApiConnector.cs` | `CreateGmailService(BasicEmailAddress)` | Unchanged with option A. It still takes `AccessToken` from `BasicEmailAddress`, which is now filled from the cache |
| `OpenDentBusiness/Data Interface/EmailMessages.cs` | `SendEmailUnsecure` (lines 871-897) | Drop `getProfile`. Get the token from the cache, refresh and retry once on 401, and map errors as in 3.5 (defect 1) |
| same | `RetrieveFromGmailInbox` (1105-1196) | Token from the cache. Exact UID compare (defect 6), default query (defect 7), base64 padding, backoff, and a 403 insufficient-scope message |
| same | `RefreshGmailToken` (1669-1680) | Becomes `GoogleTokenCache.Invalidate` + `GetAccessToken`, with no DB write |
| same | `ODEmailAddressToBasic` | For Google rows, leave `RefreshToken` empty in the basic object (the email layer doesn't need it). `AccessToken` is set by the callers above |
| `OpenDentBusiness/Email/SendEmails.cs` | `SendEmailOAuth` | Keep. Today it wraps every `GoogleApiException` in a new one built from service name and message only, which drops the HTTP status. Rethrow the original instead, so `SendEmailUnsecure` can see a 401 and retry, and 3.5 can read the error reason. Change "Unable to authenticate with Google" to the neutral wording in 3.5. Use the upload URI (`uploadType=media` or `resumable`) for large messages: Google's upload guide suggests simple upload only up to about 5 MB, and the send reference page publishes no size limit for the plain JSON body, so test 6.2 step 6 settles the threshold |
| `OpenDentBusiness/TableTypes/Program.cs` | `ProgramName` | Add `GmailApi`. The enum is stored by name, so the position doesn't matter |
| `OpenDental/Forms/FormEmailAddressEdit.cs` and `FormEmailAddressEdit.Designer.cs` | `butAuthGoogleImageHelper`, `butAuthGoogle_MouseEnter/Leave/Down/Up`, `butAuthGoogle_Click`, `FormEmailAddress_Load`, `RefreshEmailTextFields`, `ClearGmailInfo`, `butClearTokensGmail_Click`, `checkDownloadGmail_CheckedChanged`, `radioGmail_Click` and the other radio handlers, `butSave_Click` | Section 3.2. Replace the `butAuthGoogle` `Label` with a `UI.Button`. Remove `textAccessTokenGmail` and `textRefreshTokenGmail` (and their labels), and add `labelGmailStatus`. `RefreshEmailTextFields` currently treats a blank access-token box as "not signed in"; base that on the stored refresh token instead. Gate `radioGmail` (3.7). "Signed in" now means a non-legacy refresh token. Drop the `ODEnvironment.IsCloudServer` guard |
| `OpenDental/Forms/FormGmailApiSetup.cs` (new) and `.designer.cs` | whole form | Section 3.7 |
| `OpenDental/Forms/FormProgramLinks.cs` | `FillList` (line 26), `gridProgram_CellDoubleClick` | Call `GoogleOAuthSettings.EnsureProgram()` before the cache refresh. Add `case nameof(ProgramName.GmailApi):` to open `FormGmailApiSetup` |
| `OpenDental/Forms/FormEmailInbox.cs` | `GetMessages` (line 213) | Manual receive for Google addresses, placed before the blank-`Pop3ServerIncoming` return (3.8) |
| `OpenDentBusiness/Properties/Resources.resx` and `Resources.Designer.cs` | `GoogleAuthCodeResponseHtml` | **Delete.** The neutral inline page lives in `LoopbackRedirectListener` |
| `OpenDental/Properties/Resources.resx`, `OpenDental/Resources/google_signin_{normal,focus,pressed}.png` | image resources | Delete once nothing references them |
| `CodeBase/Utilities/ODCloudClient.cs` | `ODCloudAuthGoogleListener`, `SendListenerResponse`, `CheckIsListening`, `GetRedirectUri`, `CloseListener`, `HttpListenerGetContext`, `CloudClientAction.ODCloudAuthGoogleListener` / `SendListenerResponse` | Remove the callers here. Delete the members either here or in the planned ODCloud removal, whichever lands first. Keep the enum ordinals if the client protocol still matters |
| `OpenDentBusiness/WebServices/IWebServiceMainHQ.cs`, `WebServiceMainHQNoHq.cs`, `WebServiceMainHQMockDemo.cs` | `GetGoogleAccessToken`, `GetGoogleAuthorizationUrl`, `GetGoogleAuthorizationUrlLoopbackIpAddressFlow`; the mock's `CLIENT_ID_GOOGLE_SAMPLE_APP` / `CLIENT_SECRET_GOOGLE_SAMPLE_APP` and its `case OAuthApplicationNames.Google*` | With no caller left, delete them from the interface and both implementations, which also removes a compiled-in client secret. The generated `Web References/.../Reference.cs` waits for the general HQ cleanup. `BuildOAuthUrl` stays for the other vendors |
| `OpenDentBusiness/Db/Enumerations.cs` | `OAuthApplicationNames.Google`, `GoogleLoopbackIpAddressFlow` | **Keep** them (ordinals 1 and 3). Change the summaries to "Unused; Gmail authorizes locally" |
| `NoHqTests/NoHqTests.cs` | `WebServiceMainHQ_VendorMethodsSayDirectIntegrationIsNeeded` (line 67) | Replace `GetGoogleAccessToken("")` with `BuildOAuthUrl("","GoogleLoopbackIpAddressFlow")`, which still throws. Add a test that the Gmail flow builds no `IWebServiceMainHQ` |
| Tests (`OpenDentBusiness.Tests` or `UnitTests`) | new | Section 6.4 |

### 4.3 Prebuilt Bridges.dll types to re-implement

**None.** The Gmail path uses only in-repo source (`Google.cs`, `GoogleApiConnector.cs`, `EmailMessages.cs`, `SendEmails.cs`, `FormEmailAddressEdit.cs`) and Google's Apache-2.0 client library. The neighbouring `MicrosoftApiConnector` and `MicrosoftTokenHelper` *are* Bridges.dll types, but they belong to the Microsoft 365 spec and aren't touched here.

### 4.4 "Not available" stubs that are replaced or retired

| Stub | Caller today | After |
|---|---|---|
| `WebServiceMainHQNoHq.BuildOAuthUrl(regKey,"GoogleLoopbackIpAddressFlow")` | `Google.AuthorizationRequest.GetQueryParamsFromHQ` | The caller is deleted. The authorize URL is built locally from the office's client ID and our scope list. `BuildOAuthUrl` stays for other vendors |
| `WebServiceMainHQNoHq.GetGoogleAccessToken(officeData)` | `Google.GetAccessTokenHqOrThrow` (exchange **and** refresh) | Replaced by `Google.ExchangeCodeAsync` / `RefreshAsync` against `oauth2.googleapis.com`. The stub and interface member are deleted |
| `WebServiceMainHQNoHq.GetGoogleAuthorizationUrl(long)` | the mock only | Deleted |
| `WebServiceMainHQNoHq.GetGoogleAuthorizationUrlLoopbackIpAddressFlow(long)` | the mock only | Deleted |

### 4.5 Not changed

- the `emailaddress` schema and `OAuthType`
- the Microsoft 365 path (its own spec)
- password/SMTP/POP3 accounts
- `PrefName.ShowFeatureGoogleMaps` and the Google Maps link (no API, no credentials, no HQ)

## 5. Credentials the office must register

All on the office's side. No partner agreement, sandbox request or NDA.

1. **Decide which Google accounts will send.**
   - If email can contain PHI, use **Google Workspace**, not consumer @gmail.com.
   - A Workspace super administrator accepts the **HIPAA BAA** in the [Admin console](https://admin.google.com) > Account settings > Legal and compliance **before** any PHI is sent, and keeps a screenshot of the acceptance ([HIPAA with Google Workspace](https://knowledge.workspace.google.com/admin/compliance/hipaa-compliance-with-google-workspace-and-cloud-identity)).
2. **Create a Google Cloud project** at [console.cloud.google.com/projectcreate](https://console.cloud.google.com/projectcreate).
   - With Workspace, sign in with an account in the practice's domain, so the project belongs to the organization. That is required for the Internal audience.
   - No billing account is needed for normal Gmail API use.
3. **Enable the Gmail API:** [APIs & Services > Library > Gmail API](https://console.cloud.google.com/apis/library/gmail.googleapis.com) > Enable.
4. **Configure the consent screen** under [Google Auth Platform](https://console.cloud.google.com/auth/overview):
   - **Branding:** an app name such as "<Practice> Email", plus a support email. A logo is optional, and adding one can trigger brand verification.
   - **Audience:**
     - Workspace: choose **Internal**.
     - Consumer Gmail: choose **External**, add the mailbox as a test user, and once the flow works click **Publish app**. Leaving it in Testing makes authorizations expire every 7 days ([audience](https://support.google.com/cloud/answer/15549945)). Don't submit for verification: the personal-use exception covers fewer than 100 users ([exceptions](https://support.google.com/cloud/answer/13464323)). Users will see the unverified-app warning when they authorize.
   - **Data access:** add `.../auth/gmail.send`, and also `.../auth/gmail.readonly` if mail will be downloaded. Add `openid` and `.../auth/userinfo.email`.
5. **Create the OAuth client:** [Clients](https://console.cloud.google.com/auth/clients) > Create client > Application type **Desktop app** > name it > Create.
   - Copy the **Client ID** and **Client secret**.
   - Desktop clients have no redirect-URI field, and none is needed.
6. **Workspace admins only:** if the organization restricts third-party API access, mark the new client as **Trusted** under Admin console > Security > Access and data control > API controls > Manage third-party app access. Restricted Gmail scopes may need this ([exceptions](https://support.google.com/cloud/answer/13464323)).
7. **In the PMS:**
   - Setup > Program Links > **Gmail API**: paste the Client ID and Client secret, tick Enabled, and click OK.
   - Setup > Email > edit (or add) the address: choose **Gmail (OAuth)**, tick "Download incoming emails" if wanted, and click **Authorize...**.
   - Sign in **as that mailbox** in the browser and approve.
   - Save.
8. **If you migrated from Open Dental,** re-authorize every Gmail address. Then remove Open Dental's app from each account's [third-party connections](https://myaccount.google.com/connections), or centrally in the Admin console's API controls.
9. **Record** who owns the Cloud project and the client secret, and keep the BAA on file.
   - To rotate the secret: add a new secret in the client page, update Program Links, then delete the old one.
   - Re-authorize after a password change: Google drops Gmail-scoped refresh tokens when the password changes.

## 6. Test plan

### 6.1 Sandbox setup

- **Google has no sandbox.** Use a **dedicated test Cloud project**, separate from any office's production project, as Google advises for testing.
- **Test accounts:**
  - one consumer Gmail account (External audience, published, unverified)
  - one Google Workspace trial domain (Internal audience) with two users, to test the wrong-account check
- **Synthetic data only:** DevDbTool sample patients, never real PHI, in a dev database from `tools/New-DevDatabase.ps1`.

### 6.2 Manual checks

1. **Fresh database.**
   - Setup > Email shows no Gmail radio.
   - Program Links lists "Gmail API" (disabled), and enabling it with a client ID shows the radio.
   - The firewall log shows no Google host contacted before Authorize.
2. **Authorize, send-only (Workspace, Internal).** Check:
   - the status reads "Authorized as ..."
   - `SELECT LEFT(RefreshToken,5), AccessToken FROM emailaddress WHERE EmailAddressNum=<n>` gives `osd1$` and an empty string
   - the client secret row is masked and high-security
   - no token appears in any UI
3. **Client secret.** Is the exchange accepted without `client_secret` for a Desktop client? Record the result. With a wrong secret, the error message points to Program Links.
4. **Consent parameters.** A redirect URI with the `/oauth2` path is accepted for a Desktop client (otherwise switch to `/`). Re-authorizing the same address still returns a refresh token (`prompt=consent`). Unticking `gmail.readonly` on the Google consent page gives the "read access was not granted" warning.
5. **Wrong account.** Approving as user B for user A's address is refused.
   - If B isn't a configured address, B's grant no longer appears in B's third-party connections.
   - If B is already configured and authorized, B's grant stays, and B can still send.
6. **Sending:**
   - single email, with attachments up to about 20 MB. Find the size at which the plain JSON `send` fails, and set the upload-URI threshold below it (4.2, `SendEmailOAuth`)
   - statement batch (`FormBilling`), recall batch (`FormRecallList`)
   - non-ASCII subject and display name (e.g. "Muñoz")
   - "Sent" shows in Gmail with the expected From. A `SenderAddress` that differs from the mailbox needs a Gmail "Send mail as" alias
7. **Refresh.** Keep a workstation open past one hour, or clear the cache with a debug hook. The next send refreshes once: count the `/token` calls in the log. No `emailaddress` row changes.
8. **Concurrency.** Two workstations send at the same time. There are no DB writes or cache-invalidation signals from refreshes.
9. **Revoked access.** Remove the app at myaccount.google.com/connections. The next send shows one "needs to be authorized" message, and the rest of a batch fails fast. Re-authorizing on one workstation recovers the others after the Email cache signal.
10. **Password change** on the test consumer account. The behaviour matches step 9.
11. **External in Testing (negative test).** Note the 7-day expiry warning text, then publish and confirm normal operation.
12. **Download** (with `gmail.readonly`):
    - the inbox loads the last 30 days
    - an ID that starts with `a` or `d` isn't downloaded twice (defect 6)
    - "Get unread mail only" adds `is:unread`
    - a 1,000-message mailbox paginates
    - send-only grants show the insufficient-scope message
13. **Listener.**
    - Closing the browser tab, then Cancel, ends cleanly. Check that no listener remains with `netstat -ano | findstr 127.0.0.1`.
    - A request to the wrong path is ignored.
    - The response page loads no external resource: check the browser network tab.
14. **Sign-out.** The grant disappears from third-party connections. Further sends say "needs to be authorized".
15. **Offline.** Unplug the network:
    - a send fails with "could not be reached" and isn't treated as needing re-authorization
    - reconnecting, the next send succeeds without re-authorizing
    - non-email modules are unaffected
16. **Middle Tier client:** steps 2, 6 and 7.
17. **Migrated Open Dental database (restored copy).** Legacy Gmail addresses make no Google call: check the proxy log. They show "needs to be authorized", and after re-authorizing they work.
18. **Hosts.**
    - A proxy capture shows only `accounts.google.com` (browser), `oauth2.googleapis.com` and `gmail.googleapis.com`.
    - `tools/Test-NoHqHosts.ps1` passes. No `opendental.com` request appears during authorization.

### 6.3 Production pilot

Pilot one address on the office's real Workspace, with the BAA accepted. Run a day of normal sending (statements, recall, single emails), then roll out to the other addresses.

### 6.4 Automated tests (no network)

The tests use a fake `HttpMessageHandler`.

- **PKCE:** the RFC 7636 Appendix B vector (verifier `dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk` gives challenge `E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM`). Verifier length and alphabet.
- **Authorize URL:**
  - every parameter escaped (`login_hint` with `+`)
  - the scope set with and without `gmail.readonly`
  - `access_type=offline`, `code_challenge_method=S256`
  - no `opendental` substring
- **Token parsing:** code-exchange and refresh responses; error bodies with `invalid_grant`, `invalid_rapt`, `invalid_client` and `admin_policy_enforced`.
- **`id_token`:** the `email` claim is read from a sample unsigned JWT, including padding edge cases. The address comparison ignores case.
- **Cache:**
  - a 401 leads to one refresh and one retry
  - an `invalid_grant` refresh sets the flag, and no further network calls follow
  - a network error doesn't set the flag
  - parallel callers share one refresh
  - a legacy or blank token throws without any network call
- **Encryption:** the refresh token round-trips through `CDT.Class1.Encrypt` and `Decrypt`. An `Encrypt` failure blocks Save. `IsLegacyToken` returns true for plain text and false for `osd1$`.
- **Inbox:** exact UID matching with IDs `a1...`, `d2...` and `G...`; base64url without padding decodes; pagination across 3 pages; backoff on 429.
- **Listener:** rejects a wrong `state`, a wrong path or a non-GET request; accepts exactly one valid callback; honors the timeout and cancellation.
- **Revoke guard:** sign-out and the wrong-account path skip `/revoke` when another Google `emailaddress` row has the same mailbox, and call it otherwise.
- **Error mapping:** `SendEmailOAuth` keeps the original `GoogleApiException` status, and `domainPolicy`, `dailyLimitExceeded`, `userRateLimitExceeded` and a sending-limit 429 each map to their 3.5 row.
- **`EnsureProgram`** is idempotent, and a duplicate row gets cleaned up (DevDbTool database).
- **No HQ:** with `WebServiceMainHQProxy` throwing, the whole authorize, send and receive path still works against the fake handler.

## 7. Size, risks and open questions

### 7.1 Size

**M**, about 4 to 5 developer days:

- OAuth client, loopback listener and token cache: 1.5 days (PKCE and URL code already exist)
- `EmailMessages` and `SendEmails` changes, plus the inbox fixes: 0.5 day
- `FormEmailAddressEdit`, `FormGmailApiSetup`, Program Links and the inbox trigger: 1 day
- automated tests: 1 day
- manual QA across Workspace, consumer Gmail, Middle Tier and a migrated database: 0.5 to 1 day

There is no vendor lead time. A Cloud project and Desktop client are self-service in minutes. A Workspace BAA depends only on the office.

### 7.2 Risks

1. **Restricted scope for downloading.** `gmail.readonly` is restricted.
   - For Workspace offices with an Internal audience it costs nothing.
   - For consumer Gmail it means an unverified-app warning that staff must click through, and dependence on Google's personal-use exception. Google can change that policy.
   - Mitigation: send-only by default, with download opt-in.
2. **Consumer Gmail and PHI.** A consumer account can't be covered by a BAA, and the software can't tell the account types apart for certain. A Workspace account's `hd` claim in the `id_token` would be a hint (open question 5).
3. **Testing-status trap.** An office that leaves an External app in Testing has to re-authorize weekly, which looks like a bug. Mitigation: setup text and checklist step 4. The `invalid_grant` message could also mention it once the app has been authorized for about 7 days.
4. **`osd1$` is obfuscation.** Anyone who can read the database can recover the refresh token and client secret, so they can send (and maybe read) mail as the practice. Mitigations: least-privilege scopes, database access control, revocation.
5. **Google policy drift.** Parameters (`prompt`, `access_type`), quota rules (new 2026 quotas, planned usage charges) and the verification rules change over time.
   - The office owns the Cloud project, so a change is a configuration step, not a code release.
   - Watch for DPoP-bound refresh tokens, which the desktop page now mentions as optional.
6. **Sending limits during batches.** A big statement run can hit Gmail's daily cap. Remaining messages fail as "limit reached", and the office has to resend the next day. That was the same before; now it is reported clearly.
7. **Receive has no scheduler.** Until open question 2 is decided, mail arrives only when someone opens the inbox. That is a change from Open Dental, where its separate service polled.
8. **Shared listener class.** `LoopbackRedirectListener` serves Google and Dropbox, and maybe Microsoft and QuickBooks later. A defect there affects several integrations. Its tests cover that.

### 7.3 Open questions

1. **Program row naming.** Is `ProgramName.GmailApi` with ProgDesc "Gmail API" right? The Microsoft 365 spec should follow the same pattern (e.g. `Microsoft365Mail`), so agree on both at once.
2. **Inbox polling.** Is a manual check on inbox open/refresh enough? Or should there be a background `ODThread` in `FormOpenDentalThreads`, every N minutes and on one designated workstation? A new `PrefName` is a data row, not a schema change. The same decision covers POP3 and Microsoft accounts, which also lost their poller.
3. **Does the office use Gmail OAuth today?** Run `SELECT COUNT(*), SUM(DownloadInbox) FROM emailaddress WHERE AuthenticationType=1` on the restored `fox-db` copy. If none, the feature could ship send-only first.
4. **Per-office client (proposed) or one project-wide client?** A shared client would mean Google verification, restricted-scope review and a security assessment, plus publishing a privacy policy. Not recommended.
5. **PHI notice.** Should Setup show a factual note, such as "a BAA with the email provider is required to send PHI", perhaps when the `id_token` has no `hd` (hosted domain) claim? Or does that belong only in the documentation (ground rule 7)?
6. **Library.** Option A or B (4.1). That depends on how soon the binaries cleanup wants `Google.Apis*.dll` gone.
7. **Google Drive.** No Drive storage exists or was brokered. Confirm the owner doesn't want a new Drive A to Z back end. It would be a new feature, not a decoupling task, and would use the same OAuth client with a `drive.file` scope.

## References

- OAuth 2.0 for desktop apps (loopback, PKCE, endpoints, errors): https://developers.google.com/identity/protocols/oauth2/native-app
- OAuth 2.0 overview (refresh token expiry, 100-token limit): https://developers.google.com/identity/protocols/oauth2
- OpenID Connect (`id_token`, `email` claim): https://developers.google.com/identity/openid-connect/openid-connect
- Gmail API scopes and sensitivity: https://developers.google.com/workspace/gmail/api/auth/scopes
- Gmail `users.getProfile`: https://developers.google.com/workspace/gmail/api/reference/rest/v1/users/getProfile
- Gmail `users.messages.send`: https://developers.google.com/workspace/gmail/api/reference/rest/v1/users.messages/send
- Gmail `users.messages.list`: https://developers.google.com/workspace/gmail/api/reference/rest/v1/users.messages/list
- Gmail `users.messages.get`: https://developers.google.com/workspace/gmail/api/reference/rest/v1/users.messages/get
- Gmail uploads: https://developers.google.com/workspace/gmail/api/guides/uploads
- Gmail usage limits: https://developers.google.com/workspace/gmail/api/reference/quota
- Gmail error handling: https://developers.google.com/workspace/gmail/api/guides/handle-errors
- Gmail sending limits in Google Workspace: https://knowledge.workspace.google.com/admin/gmail/gmail-sending-limits-in-google-workspace
- Audience and publishing status: https://support.google.com/cloud/answer/15549945
- When verification is not needed: https://support.google.com/cloud/answer/13464323
- Google API Services User Data Policy: https://developers.google.com/terms/api-services-user-data-policy
- HIPAA compliance with Google Workspace: https://knowledge.workspace.google.com/admin/compliance/hipaa-compliance-with-google-workspace-and-cloud-identity
- HIPAA compliance on Google Cloud and Google Workspace (covered services; the old Included Functionality URL redirects here): https://docs.cloud.google.com/docs/security/compliance/hipaa
- Sign-in branding guidelines: https://developers.google.com/identity/branding-guidelines
- Google Account third-party connections: https://myaccount.google.com/connections
- RFC 7636 (PKCE), Appendix B test vector: https://www.rfc-editor.org/rfc/rfc7636
- RFC 8252 (OAuth for native apps), loopback redirect: https://www.rfc-editor.org/rfc/rfc8252
