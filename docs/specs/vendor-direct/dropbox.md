# Dropbox (A to Z image storage): direct-to-vendor spec

Status: draft, 2026-10-18; fact-checked against the code and Dropbox's public docs on 2026-10-18. Baseline: `main` at `6855026f5`. Vendor: Dropbox, Inc. API: **Dropbox API v2** (public), OAuth 2.0 with PKCE.

## Summary

- **What it is.** In Setup > Data Paths an office can choose "Store images in Dropbox" instead of a local A to Z folder. The images module, sheets, email and claim attachments, patient merge and the HL7 MedLab archive then read and write the office's Dropbox account through `CloudStorage`, which is referenced roughly 245 times from about 42 caller files.
- **What HQ provided.** Open Dental's **Dropbox app**:
  - Its app key, inside the authorize URL that `BuildOAuthUrl(regKey,"Dropbox")` returned.
  - Its app secret, held at HQ.
  - The code-for-token exchange, `GetDropboxAccessToken(code)`.

  HQ returned one bare access token, which is stored in plain text in `programproperty` "Dropbox API Token". There is no refresh token.
- **State on `main`.** Both HQ calls throw `HqNotAvailableException`, so no office can link Dropbox. A token carried over from an Open Dental database was issued to **Open Dental's** app, and this fork must not keep using it.
- **The rest is already direct.** Everything after authorization already talks straight to Dropbox: `OpenDentalCloud/Storage/Dropbox.cs` is in-repo source on Dropbox's MIT-licensed .NET SDK 4.0 (`Required dlls/Dropbox.Api.dll`). **Bridges.dll is not involved.**
- **Direct design:**
  - Each office registers its **own Dropbox app** in its own Dropbox account. It stays in development status, links one account and needs no app review.
  - Auth is OAuth 2.0 authorization code with **PKCE (S256)** and `token_access_type=offline`. There is **no app secret**.
  - The system browser opens. By default the user pastes the code that Dropbox shows. A registered `http://localhost:<port>/` loopback redirect can be added later.
  - A long-lived **refresh token** is stored encrypted (`osd1$`) in a new masked, high-security `programproperty` row.
  - Short-lived access tokens live only in memory. Each process refreshes its own, so a refresh never writes to the database.
- **Replace SDK 4.0.** It has no PKCE or refresh support, it calls deprecated unsuffixed routes (`files/delete`, `copy`, `move`, `get_thumbnail`) that now have `_v2` replacements, and it pins Newtonsoft.Json 7-era assembly bindings.
  - **Recommended:** a small in-repo REST client over `HttpClient` and the repo's Newtonsoft.Json 12.0.3, covering the 13 endpoints we use. Then delete `Dropbox.Api.dll`.
  - **Alternative:** NuGet `Dropbox.Api` 7.4.0. It needs Newtonsoft.Json 13.x repo-wide.
- **Pre-existing defects to fix on the way:**
  - folder listing ignores `has_more`
  - `Move` builds a doubled source path
  - `FileExists` reports auth failures as "not found"
  - the startup check can't tell "offline" from "needs re-authorization"
- **Feasibility: public API.** No partner agreement or NDA docs are needed. The real gates are on the office's side:
  - a Dropbox **team plan with a signed BAA** before any PHI is uploaded
  - Developer Terms §2.6(c), which bars tokens that let several app users into one account. That is inherent to shared A to Z storage and needs the owner's review.
  - ground rule 6 ("images stay on local storage")
- **Size: M**, about 4 to 6 days.

## 1. What the integration does, and what HQ provided

### 1.1 What the office uses it for

- **Choosing it.** `PrefName.AtoZfolderUsed` = `DataStorageType.DropboxAtoZ` (2) switches the whole A to Z store to the linked Dropbox account. The office picks it in **Setup > Data Paths** (`OpenDental/Forms/FormPath.cs`, Dropbox tab), which offers:
  - an AtoZ path inside Dropbox (default `/AtoZ`)
  - an "Authorize Dropbox" button
  - a read-only "Access Token" box
- **Every read and write goes through `OpenDentBusiness/WebBridges/CloudStorage.cs`:** Upload, Download, Delete, GetThumbnail, Move, Copy, ListFolderContents and FileExists. It in turn calls `OpenDentalCloud.Dropbox.*` (`OpenDentalCloud/Storage/Dropbox.cs`). Callers include:
  - `ImageStore`, `FileAtoZ` and `Documents`
  - sheets
  - email attachments and claim attachments
  - patient merge
  - wiki and markup
  - `OpenDentHL7/ServiceHL7.cs`, the MedLab inbound archive. It runs as a Windows service with no UI.
- **At startup** `FormOpenDental` (lines 690-712) checks that `<AtoZ>/A` exists in Dropbox. If it doesn't, it shows "The Dropbox data path for your A to Z Images Folder is not available".
- **The data model is unchanged by this spec:**
  - The `program` row "Dropbox" was inserted by ConvertDatabases 16.3.1, with `Enabled=0`.
  - It has two `programproperty` rows: "Dropbox AtoZ Path" (`/AtoZ`) and "Dropbox API Token" (blank).
  - `document` rows keep their usual A to Z relative file names.

### 1.2 What Open Dental's HQ provided

| Step | Code on `main` | What HQ did (inferred from the calls) | Now |
|---|---|---|---|
| Authorize URL | `FormDropboxAuthorize_Load`: `IWebServiceMainHQ.BuildOAuthUrl(regKey, OAuthApplicationNames.Dropbox)`, then `Process.Start(url)` | Built `https://www.dropbox.com/oauth2/authorize?...` with **Open Dental's app key**, gated on a valid registration key. There was no redirect URI: the form tells the user to paste the code Dropbox shows. That matches the SDK overload `DropboxOAuth2Helper.GetAuthorizeUri(appkey)` in `OpenDentalCloud/Storage/Dropbox.cs`, the no-redirect code flow. | Throws `HqNotAvailableException` (`WebServiceMainHQNoHq.BuildOAuthUrl`) |
| Code exchange | `butSave_Click`: `GetDropboxAccessToken(code)` | Exchanged the code with **Open Dental's app key and secret**. The comment on `OpenDentBusiness.Dropbox.GetDropboxAccessToken(code,appkey,appsecret)` says HQ ran that wrapper. HQ returned the bare access token. | Throws (`VendorNotAvailable("Dropbox")`) |
| Alternate URL method | `IWebServiceMainHQ.GetDropboxAuthorizationUrl(long)` | Unused in this repo. Only a commented-out line in `WebServiceMainHQMockDemo` refers to it. | Throws |
| Token storage | `ProgramPropertyAccessToken.PropertyValue = token` | No HQ role. The token is stored **in plain text** and shown read-only in FormPath. | Unchanged |
| API calls | `OpenDentalCloud.Dropbox.*` via `new DropboxClient(accessToken)` | No HQ role. The workstation already called `api.dropboxapi.com` and `content.dropboxapi.com` directly. | Still works with a valid token |

Consequences:

- **The fork can't link Dropbox at all**, and nothing renews a token, because no refresh token exists.
- **A database migrated from Open Dental** (Phase 5) carries a token that belongs to Open Dental's Dropbox app. Using it would keep this fork running on Open Dental's vendor registration. The design treats it as void (section 3.9).
- **No Bridges.dll type is used** anywhere in this path. The only prebuilt binary is `Required dlls/Dropbox.Api.dll`, Dropbox's MIT SDK 4.0.6348 (`docs/third-party-binaries-audit.md`).

### 1.3 Defects found while reading the code (fix in the same change)

1. **`Dropbox.ListFolders.PerformIO`** calls `list_folder` once and ignores `has_more`. Large patient folders are listed incompletely ([list_folder](https://docs.dropboxapi.com/dropbox-api/api-reference/user-endpoints/files/list-folder)).
2. **`Dropbox.Move.PerformIO`** (non-copy branch) moves from `CombinePaths(FromPath, fromPathFull)`, but `fromPathFull` already starts with `FromPath`. The source path therefore contains `FromPath` twice, for a single file and for folder contents alike, so moves count as failed. The copy branch passes `fromPathFull` correctly.
3. **`Dropbox.FileExists`** swallows every exception and returns `false`. An expired or revoked token therefore looks like a missing folder, at startup and in FormPath.
4. **`Download`** first calls `FileExists`, an extra round trip, and then downloads. The download alone already returns `path/not_found`.
5. **Thumbnail, delete, copy and move** go through SDK 4.0's `GetThumbnailAsync`, `DeleteAsync`, `CopyAsync` and `MoveAsync`, which use the unsuffixed routes. The current reference documents `get_thumbnail_v2`, `delete_v2`, `copy_v2` and `move_v2` in their place. Upload already uses `upload_session/start`, `append_v2` and `finish`, which are current, as are listing, metadata and download.

## 2. The vendor's public API

### 2.1 Documentation status

Public, complete and free:

- [OAuth guide](https://docs.dropboxapi.com/dropbox-api/docs/oauth)
- [authorization reference](https://docs.dropboxapi.com/dropbox-api/docs/get-started/authorization)
- [endpoint reference](https://docs.dropboxapi.com/dropbox-api/api-reference)
- [error handling](https://docs.dropboxapi.com/dropbox-api/docs/error-handling)
- [performance guide](https://docs.dropboxapi.com/dropbox-api/docs/performance)
- [developer guide](https://docs.dropboxapi.com/dropbox-api/docs/developer-resources/developer-guide)
- [Developer Terms](https://www.dropbox.com/developers/reference/tos) (effective 2025-03-01)

Note: the older `www.dropbox.com/developers/documentation` and `developers.dropbox.com/oauth-guide` URLs now redirect to `docs.dropboxapi.com`.

There is no partner program, no NDA documentation, and no separate sandbox host. Testing uses a development-status app linked to a test Dropbox account (section 6.1).

### 2.2 Base URLs

| Use | URL | Notes |
|---|---|---|
| User consent page (system browser only) | `https://www.dropbox.com/oauth2/authorize` | Must open in a real browser, not an embedded web view ([authorization](https://docs.dropboxapi.com/dropbox-api/docs/get-started/authorization)) |
| Token endpoint | `POST https://api.dropboxapi.com/oauth2/token` | The page's own examples also show `api.dropbox.com`. Use `api.dropboxapi.com`, as the reference states |
| RPC endpoints (JSON in, JSON out) | `https://api.dropboxapi.com/2/...` | [Request formats](https://docs.dropboxapi.com/dropbox-api/docs/technical-reference/request-response-formats) |
| Content upload and download | `https://content.dropboxapi.com/2/...` | Arguments go as JSON in the `Dropbox-API-Arg` header. Download results come back in `Dropbox-API-Result` |
| Sandbox | none | Same production hosts, with a development-status app and a test account |

### 2.3 Authentication, step by step (PKCE, desktop app, no secret)

The OAuth guide recommends **code flow + PKCE + refresh tokens** for desktop apps that need background access. It notes that open-source and serverless desktop apps can't keep a secret ([OAuth guide](https://docs.dropboxapi.com/dropbox-api/docs/oauth), [PKCE: what and why](https://dropbox.tech/developers/pkce--what-and-why-)).

1. **Make a code verifier.** Generate a random `code_verifier`: 43-128 characters from `[0-9A-Za-z-._~]`. 32 random bytes, base64url without padding, gives 43. The `code_challenge` is base64url(SHA-256(verifier)), with `code_challenge_method=S256`.
2. **Make a state value.** Generate a random `state` (at most 2000 bytes) for CSRF protection. Dropbox echoes it on redirect.
3. **Open the authorize page** in the system browser:
   ```
   https://www.dropbox.com/oauth2/authorize
     ?client_id=<app key>&response_type=code&token_access_type=offline
     &code_challenge=<challenge>&code_challenge_method=S256&state=<state>
     &scope=account_info.read files.metadata.read files.content.read files.content.write
     [&redirect_uri=<registered loopback URI>]
   ```
   - Leave out `redirect_uri` and Dropbox shows the code for the user to copy into the app (the "no-redirect" flow).
   - If `redirect_uri` is sent, it must **exactly** match a URI registered in the App Console. Localhost URIs must be registered too, and they are the only ones allowed to use `http`.
   - `scope` is a space-separated subset of the scopes ticked in the console.
4. **The user signs in and approves.** With a redirect, the browser calls back `?code=...&state=...`, or `?error=...&error_description=...&state=...` on refusal.
5. **Exchange the code.** `POST /oauth2/token`, form-encoded:
   - `grant_type=authorization_code`
   - `code`
   - `client_id=<app key>`
   - `code_verifier`
   - the same `redirect_uri`, if one was used

   No `client_secret` is sent. The documented PKCE examples send only `client_id` and `code_verifier`.

   The response holds:
   - `access_token` (opaque, short-lived)
   - `expires_in` (seconds)
   - `token_type=bearer`
   - `refresh_token`, present because of `offline`
   - `scope`
   - `account_id`
6. **Refresh.** `POST /oauth2/token` with `grant_type=refresh_token`, `refresh_token` and `client_id`. The response has a new `access_token` and `expires_in`. **No new refresh token is issued.** The existing one is reused and doesn't expire on its own.
7. **Revoke.** `POST https://api.dropboxapi.com/2/auth/token/revoke` with the bearer access token. It disables that token **and the refresh token it came from**, plus every access token issued from that refresh token ([token/revoke](https://docs.dropboxapi.com/dropbox-api/api-reference/user-endpoints/auth/token-revoke)).

Background:

- **Long-lived tokens are deprecated** ([OAuth guide](https://docs.dropboxapi.com/dropbox-api/docs/oauth)). That is why the current store-one-token design can't be rebuilt as it was.
- **App Console tokens are for testing only.** The console's "Generate" token works only for the developer's own account.

### 2.4 Endpoints and fields we need

| Use in this repo | Endpoint | Host | Scope | Fields |
|---|---|---|---|---|
| Show the linked account; detect a switch to another account | `users/get_current_account` | api | `account_info.read` | `account_id`, `name.display_name`, `email`, `account_type`, `team` ([ref](https://docs.dropboxapi.com/dropbox-api/api-reference/user-endpoints/users/get-current-account)) |
| `FileExists`, startup and Setup checks, Move's file-or-folder test | `files/get_metadata` | api | `files.metadata.read` | in: `path`. out: `.tag` (file or folder), `path_lower`, `path_display`, `size`. Error 409 `path/not_found` ([ref](https://docs.dropboxapi.com/dropbox-api/api-reference/user-endpoints/files/get-metadata)) |
| `ListFolderContents` | `files/list_folder`, `files/list_folder/continue` | api | `files.metadata.read` | in: `path`, `limit`. out: `entries[]`, `cursor`, `has_more`. Loop on `continue` while `has_more` ([ref](https://docs.dropboxapi.com/dropbox-api/api-reference/user-endpoints/files/list-folder)) |
| `Upload` | `files/upload_session/start`, `append_v2`, `finish` | content | `files.content.write` | `start`: `close=false`. `append_v2`: `cursor{session_id,offset}`, `close`. `finish`: `cursor`, `commit{path, mode:"overwrite", autorename:false}`. At most 150 MiB per request; sessions last 7 days; 409 `incorrect_offset` returns `correct_offset` ([start](https://docs.dropboxapi.com/dropbox-api/api-reference/user-endpoints/files/upload-session-start), [append_v2](https://docs.dropboxapi.com/dropbox-api/api-reference/user-endpoints/files/upload-session-append-v-2), [finish](https://docs.dropboxapi.com/dropbox-api/api-reference/user-endpoints/files/upload-session-finish)) |
| `Download` | `files/download` | content | `files.content.read` | in: `path`. Metadata comes back in the `Dropbox-API-Result` header and the bytes in the body ([ref](https://docs.dropboxapi.com/dropbox-api/api-reference/user-endpoints/files/download)) |
| `GetThumbnail` | `files/get_thumbnail_v2` | content | `files.content.read` | `resource{.tag:"path",path}`, `format:"jpeg"`, `size:"w128h128"`, `mode:"strict"`. Only images up to 20 MB, in jpg, jpeg, png, tiff, tif, gif, webp, ppm or bmp ([ref](https://docs.dropboxapi.com/dropbox-api/api-reference/user-endpoints/files/get-thumbnail-v-2)) |
| `Delete` | `files/delete_v2` | api | `files.content.write` | `path`. Deleting a folder deletes its contents ([ref](https://docs.dropboxapi.com/dropbox-api/api-reference/user-endpoints/files/delete-v-2)) |
| `Copy`, `Move` | `files/copy_v2`, `files/move_v2` | api | `files.content.write` | `from_path`, `to_path`, `autorename:false` ([copy](https://docs.dropboxapi.com/dropbox-api/api-reference/user-endpoints/files/copy-v-2), [move](https://docs.dropboxapi.com/dropbox-api/api-reference/user-endpoints/files/move-v-2)) |
| Disconnect | `auth/token/revoke` | api | none listed | No body |

Encoding details that matter for patient folders:

- **Paths** start with `/`, use `/` as the separator, and compare case-insensitively. Use `path_lower` for comparisons and `path_display` for the UI. `CloudStorage.PathTidy` already turns `\` into `/`.
- **The `Dropbox-API-Arg` header must be pure ASCII.** Characters outside ASCII, and 0x7F, go in as `\uXXXX` escapes. A patient named "Muñoz" puts a non-ASCII character in the A to Z path. Serialize with `StringEscapeHandling.EscapeNonAscii` and also replace `\x7f`.
  - Source: the official [JSON encoding](https://docs.dropboxapi.com/dropbox-api/docs/technical-reference/json-encoding) page, which the [request formats](https://docs.dropboxapi.com/dropbox-api/docs/technical-reference/request-response-formats) page links to and which includes a Json.NET sample. Content endpoint pages such as `get_thumbnail_v2` repeat the rule.
  - Dropbox staff said the same on the [developer forum](https://community.dropbox.com/discussions/101000014/accented-characters-in-file-name/222655).

### 2.5 Rate limits

- **No published numbers.** Limits are **per authorization**: per user for a user-linked app. Exceeding one gives HTTP 429 with `too_many_requests` ([performance](https://docs.dropboxapi.com/dropbox-api/docs/performance)).
- **Lock contention.** Writes take a namespace lock, so many parallel writes to one account can give 429 `too_many_write_operations`. Several workstations uploading at once to the same office account is exactly that case. Dropbox suggests upload sessions and batching, and says to retry.
- **Retries.** Always honor `Retry-After` when it is present. Rate-limited calls still count against the limit, so retrying fast without pausing makes it worse. Use exponential backoff for 5xx, and for 403 in background work ([error handling](https://docs.dropboxapi.com/dropbox-api/docs/error-handling)).
- **Other limits.** Some business plans have a separate data transport limit on upload calls (performance guide). Uploads in multiples of 4 MiB per `append_v2` are recommended for throughput. That is required only for `concurrent` sessions; we use sequential ones.

### 2.6 Terms that matter

- **App review / production status** ([developer guide](https://docs.dropboxapi.com/dropbox-api/docs/developer-resources/developer-guide)):
  - A new app is in development status and links only the developer's account, plus any accounts the developer enables, up to 500 users.
  - After **50** linked users the developer has two weeks to win production approval, or new links stop.
  - Internal tools may stay in development status indefinitely. A per-office app links **one** account and never needs review. A single project-wide app shared by many offices would need review, a privacy policy and branding compliance.
- **Branding** ([branding guide](https://docs.dropboxapi.com/dropbox-api/docs/developer-resources/branding-guide)):
  - The app name may not contain "Dropbox" or another Dropbox product name, start with "drop", sound like Dropbox, use abbreviations such as "DB" or "DBX", or suggest affiliation. "for Dropbox" is tolerated only to tell a Dropbox-specific app apart from the developer's other apps; there is no reason to use it here.
  - In the UI, the Dropbox name is used only to identify the integration. That fits ground rule 7.
- **Developer Terms** ([terms](https://www.dropbox.com/developers/reference/tos), effective 2025-03-01):
  - Protect access tokens with reasonable security (§2.1).
  - Keep a security program and report breaches (§2.2, §2.4).
  - Users sign in only on Dropbox's own page, never in our UI (§2.5, §2.6(a)-(b)).
  - Don't get around rate limits (§1.3.2).
  - Provide a privacy policy (§3.1). Under the per-office model the office is its own developer and its own only user.
  - **§2.6(c)** forbids using tokens so that several people using the app reach one Dropbox account. Shared A to Z storage works exactly that way: every staff workstation uses the office's one linked account. Open Dental shipped the same model. See risks (7.2) and open questions (7.3).
- **PHI and BAA** ([Dropbox and HIPAA/HITECH](https://help.dropbox.com/security/hipaa-hitech-overview)):
  - Dropbox signs a BAA only for **team** plans: Standard, Advanced, Business, Business Plus, Enterprise or Education. A team admin of a US-based customer can sign it electronically from the admin console's Account page. Electronic signing is US-only, so anyone else asks Dropbox sales.
  - The BAA must be in place **before** any PHI is uploaded.
  - Personal plans are not covered.
  - Third-party apps and integrations are outside the BAA, and the customer must judge each one. The office's own registered app is such an integration, so the office answers for how this software handles PHI. What this design changes is that no other company's servers sit in the path: the software talks to Dropbox directly, and with HQ gone Open Dental no longer touches the authorization step.
  - Dropbox holds no HIPAA certification. Compliance stays the customer's job: sharing settings, disabling permanent deletion, monitoring access.

## 3. Desktop design

### 3.1 Credential model

- **Each office registers its own Dropbox app** in the Dropbox account that will hold its images (checklist in section 5).
  - It stays in development status, which suits an internal tool.
  - The office's only credential is the **app key**. With PKCE no app secret is used or stored. The key is a public identifier, but it is still never compiled in or committed (ground rule). It lives in the office's database.
- **A project-wide app is possible later.** The UI accepts any app key. But it would make the project a Dropbox "developer", with review, privacy-policy and breach-reporting duties. Not proposed (open question 3).

### 3.2 Auth flow in the WinForms app

`FormDropboxAuthorize` is rewritten. It runs only from Setup > Data Paths (Setup permission). Steps:

1. **Validate the app key.** It is read from FormPath's new "App key" box. Blank shows "Enter the app key from the Dropbox App Console first."
2. **Create the verifier, challenge and state** (section 2.3). Keep them in memory only.
3. **Choose the redirect mode** from "Dropbox Redirect URI":
   - **Blank (default): no-redirect paste flow.** Build the authorize URL without `redirect_uri` and open it with `Process.Start` in the default browser. The form shows "Approve access in the browser, then paste the code Dropbox shows here", a code box, and OK and Cancel. The office doesn't need to register any redirect URI, and the flow works on terminal servers.
   - **Loopback flow.** Use it when the value is `http://localhost:<port>/<path>` or `http://127.0.0.1:<port>/<path>`, registered identically in the App Console.
     - Before opening the browser, start a **`TcpListener` bound to the loopback addresses only**, on that port. Bind `IPAddress.Loopback` and also `IPv6Loopback` when the host is `localhost`.
     - Don't use `HttpListener`: it goes through http.sys URL ACLs and binds by Host header.
     - Accept one `GET` whose path matches. Parse `code`, `state` and `error`, and reject a mismatched `state`.
     - Reply with a small **self-contained** HTML page saying authorization is done and the tab can be closed. It must load no external assets (unlike `GoogleAuthCodeResponseHtml` in `OpenDentBusiness/Properties/Resources.resx`, which links `opendental.com/style.css`).
     - Close the listener on the first valid request, on Cancel, or after 5 minutes.
     - The paste box stays visible as a fallback.
     - Dropbox matches redirect ports exactly and doesn't follow RFC 8252's any-port rule, so the port is fixed in configuration ([forum thread with staff answer](https://community.dropbox.com/discussions/101000014/redirect-uri-with-variable-loopback-port/197621). The official docs say only that the URI must match exactly).
4. **Exchange the code** on a background task: `POST /oauth2/token` (authorization_code + `code_verifier`), and the UI stays responsive.
5. **Look up the account** with the new access token: `users/get_current_account`.
   - If a "Dropbox Account ID" is already stored and differs, confirm first: "This links a different Dropbox account. Images stored in the previous account will not be found. Continue?"
6. **Save** (section 3.3):
   - the encrypted refresh token, the account ID and the display name
   - **blank the legacy "Dropbox API Token"**
   - prime the in-memory token cache with the access token and its expiry
   - `DataValid.SetInvalid(InvalidType.Programs)`
   - `SecurityLogs.MakeLogEntry(EnumPermType.Setup, 0, "Dropbox authorized")`. Log no account email or token.
7. **On error,** show Dropbox's `error` / `error_description` or the HTTP status in a neutral message, and save nothing.

**Disconnect** (new FormPath button, Setup permission):

- If a token can be obtained, call `auth/token/revoke`. This disables the refresh token too.
- Then blank the refresh token, account ID and name. Revoke errors are ignored, because the user may already have unlinked the app on dropbox.com.
- If the storage type is still Dropbox, warn that images will be unreachable until the account is re-authorized.

### 3.3 Where credentials and tokens live

All values are `programproperty` rows of the existing "Dropbox" program, `ClinicNum=0`, `ComputerName=''`.

**New rows are inserted at runtime** by `OpenDentBusiness.Dropbox.EnsurePropertyRows()`. It is idempotent, runs from `FormPath.LoadDropboxSetup()`, and is followed by `DataValid.SetInvalid(InvalidType.Programs)`. There is no schema change and no ConvertDatabases step. Readers treat a missing row as blank (`GetPropValFromList`), so a workstation never fails just because Setup hasn't run yet.

| PropertyDesc | Status | Value | Encrypted | IsMasked | IsHighSecurity |
|---|---|---|---|---|---|
| `Dropbox AtoZ Path` | existing | e.g. `/AtoZ`. It is relative to the app folder when the app has App-folder access | no | no | no |
| `Dropbox API Token` | existing, **legacy** | No longer read. Blanked after the first successful authorization with the office's app. The row stays for schema and data compatibility | n/a | (set yes) | no |
| `Dropbox App Key` | new | The office's app key | no (public identifier) | no | no |
| `Dropbox Refresh Token` | new | Refresh token as `osd1$...`, written with `CDT.Class1.Encrypt(...)`. Store only if `Encrypt` returns true. Never use `TryEncrypt` here, because it falls back to plain text | **yes** | **yes** | **yes** (editing needs `ManageHighSecurityProgProperties`) |
| `Dropbox Redirect URI` | new | Blank means the paste flow. Otherwise a registered `http://localhost:<port>/...` URI | no | no | no |
| `Dropbox Account ID` | new | `account_id` (`dbid:...`), used to detect an account switch | no | no | no |
| `Dropbox Account Name` | new | `display_name` and email, for display only | no | no | no |

Notes:

- **Not stored:** access tokens (memory only, section 3.4), the code verifier, the state, and any app secret.
- **Decryption happens client side.** `OpenDentBusiness.Dropbox.GetSession()` decrypts with `CDT.Class1.Decrypt` and hands `OpenDentalCloud` the app key and plain refresh token in memory. `OpenDentalCloud` doesn't reference CDT.
- **Middle Tier:** the rows travel in the ProgramProperties cache, as the access token does today. `ProgramProperties.Insert` already supports Middle Tier.
- **`osd1$` is obfuscation, not secrecy.** Its key is public by design (`CDT/README.md`), so anyone who can read the `programproperty` table can recover the refresh token, which means full access to the linked folder. Mitigations:
  - Recommend **App-folder** access, which limits the token to `/Apps/<app name>/`.
  - Mark the row high-security.
  - Rotate by re-authorizing and then revoking.

### 3.4 Token runtime and refresh

New `OpenDentalCloud/Storage/DropboxSession.cs`:

- **One per process,** keyed by app key plus a SHA-256 of the refresh token. A new key means new values, which means a new session.
- **Holds** the current access token and its expiry (`now + expires_in`).
- **`GetAccessTokenAsync()`:**
  - Returns the cached token if it is valid for more than 5 more minutes.
  - Otherwise refreshes, **single-flight** behind a `SemaphoreSlim`, so concurrent image loads trigger only one refresh.
- **API calls go through `SendAsync(request)`:**
  - add the bearer token
  - on **401**, invalidate the token, refresh once and retry once
  - if the refresh itself returns 400 or 401 (refresh token revoked or app unlinked), set `NeedsReauthorization` and throw `DropboxAuthException`. Its neutral message is "Dropbox access needs to be re-authorized in Setup > Data Paths."
- **No refresh writes to the database.** Each workstation and service refreshes independently with the same refresh token, which Dropbox allows because refresh tokens aren't rotated. So there is no write contention and no cross-workstation signal.
- **About one refresh call per process per token lifetime.** A 20-workstation office makes a handful per day.
- **Background processes** refresh the same way: `OpenDentHL7` (MedLab archive), plus anything else that links `OpenDentalCloud`. They read the rows from the database and need outbound HTTPS from their host.
- **After re-authorization,** the next cache refresh (`InvalidType.Programs`) gives every process the new refresh token, so it builds a new session. Processes still holding the revoked one get one `DropboxAuthException`, then pick up the new values.

### 3.5 Errors and offline behaviour

| Condition | Detected by | Behaviour |
|---|---|---|
| Not linked: blank app key or refresh token, or only a legacy token | `Dropbox.GetSession()` | Startup message: "Dropbox image storage is not authorized. An administrator can authorize it in Setup > Data Paths." `CloudStorage` calls fail fast. No network call is made |
| Refresh token revoked or app unlinked | Refresh returns 400 or 401 | `NeedsReauthorization`. Show **one** message per session, not one per call: there are 234 call sites and `TaskState` usually swallows errors. Later calls fail fast until the values change |
| Access token expired | 401 | Transparent: refresh once, retry once |
| Missing scope | 401/403 with a scope error, or `invalid_scope` at authorize time | Message naming the scope to tick in the App Console Permissions tab |
| `path/not_found` (409) | Endpoint error body | `FileExists` gives `false`. Download gives "File not found." The startup check reports "AtoZ folder not found in the linked Dropbox account" |
| Storage full | 409 `path/insufficient_space` on upload commit | Show Dropbox's `user_message` if present, otherwise a neutral message |
| Rate limited | 429 | Wait `Retry-After`. Interactive calls wait at most 10 s, then fail with "Dropbox is busy, try again"; background calls back off exponentially up to 60 s. `too_many_write_operations` with `Retry-After: 0` is retried at once, up to 3 times |
| Dropbox 5xx | 500/503 | Up to 2 retries (1 s, then 2 s) for idempotent calls. Upload `finish` is retried only with the same cursor |
| Offline: DNS failure, connection refused, TLS or timeout | `HttpRequestException` or `TaskCanceledException` | **Circuit breaker:** after a network-level failure, mark Dropbox unreachable for 30 s. Calls in that window fail at once with "Dropbox could not be reached", so the imaging module doesn't hang on every thumbnail. The next call after the window probes again |
| Upload interrupted mid-session | 409 `incorrect_offset` | Resume from `correct_offset`. A session lives 7 days, but we restart the upload if the process exits |

- **Timeouts:** RPC calls 15 s; content transfers 100 s per request (the `HttpClient` default); the `FileExists` wrapper stays at 10 s. Use `ConfigureAwait(false)` throughout, because `TaskState.Execute` blocks a thread on a `ManualResetEvent`.
- **Offline means no images.** That is unavoidable in this mode. Charting, scheduling, billing and claims are unaffected, but imaging, attachments and sheets with images are unavailable until the network is back. The Setup screen says the mode needs an internet connection (section 3.8). Ground rule 5 is met only by the office's choice of storage mode; see 7.2.
- **New `Dropbox.TestConnection()`** returns `Ok | NotAuthorized | NeedsReauthorization | AtoZNotFound | Unreachable | Error` plus a message. FormPath Save and the startup check use it in place of the bare `FileExists`. `CloudStorage.FileExists` keeps its bool contract and logs the cause.
- **Logging:** endpoint name, HTTP status, the Dropbox error tag and the `X-Dropbox-Request-Id` response header. **Never** tokens, codes, verifiers or file paths: A to Z folder names contain patient names.

### 3.6 Hosts contacted, and no HQ

- `www.dropbox.com`: the browser only, for the consent page.
- `api.dropboxapi.com`: token, RPC and revoke.
- `content.dropboxapi.com`: uploads, downloads and thumbnails.
- With a loopback redirect, the workstation's own `localhost:<port>`.

Nothing else is contacted. In particular there is no `opendental.com`, `opendentalsoft.com`, `patientviewer.com` or `IWebServiceMainHQ` call, so `tools/Test-NoHqHosts.ps1` keeps passing. TLS 1.2 is already forced in `CodeBase/Utilities/ODInitialize.cs`. No certificate pinning is added, so Dropbox's 2026 root change ([dropbox.tech](https://dropbox.tech/developers/api-server-certificate-changes)) doesn't affect the in-repo client. That change breaks only .NET SDK builds before 7.0.0 that call `DropboxCertHelper.InitializeCertPinning()`. This repo never calls it, so today's SDK 4.0 path is not broken by it.

### 3.7 Vendor neutrality (ground rule 7) and gating (ground rule 6)

- **Off by default.** The Dropbox program row stays `Enabled=0`, and `AtoZfolderUsed` stays `LocalAtoZ`.
- **The "Store images in Dropbox" radio appears only when** the Dropbox program link is enabled (Setup > Program Links > Dropbox) **or** the storage type is already `DropboxAtoZ`, so a migrated office still sees its own setting.
- **Setup text describes what the option does,** and nothing more: "Stores the A to Z images folder in the linked Dropbox account. An internet connection is required." There are no links, logos or sign-up wording, and no "contact support".
- **Remove the Open Dental-specific Save warning** about updating workstations older than 16.3 with `Setup.exe` in the Dropbox AtoZ folder. It refers to Open Dental's installer.
- **Drop the URL from the description.** The program description "Dropbox from www.dropbox.com" can become "Dropbox" through a data update; that is optional.

### 3.8 FormPath, Dropbox tab (new layout)

- **App key** (text): saved to "Dropbox App Key" on Save.
- **Redirect URI (optional)** (text): validated as an `http` loopback URI with an explicit port, or left blank.
- **Linked account** (read-only label): "display name (email)", or "Not authorized", or "Needs re-authorization".
- **Authorize... / Re-authorize...** opens `FormDropboxAuthorize`.
- **Disconnect** (section 3.2).
- **Dropbox AtoZ Path**: kept as is.
- **The read-only "Access Token" box is removed.** Tokens are never shown.
- **`LoadDropboxSetup`:**
  - Calls `EnsurePropertyRows()` and no longer fails when new rows are missing.
  - Its "missing a program property ... contact support" message becomes neutral. It can only fire now if the `program` row is missing.

### 3.9 Data migrated from Open Dental

On a restored `fox-db` copy:

- **If `AtoZfolderUsed=2`,** the "Dropbox API Token" holds a token issued to Open Dental's app. It is **not used**.
  - Startup reports "not authorized" (3.5).
  - The administrator follows section 5 and authorizes with the office's own app.
  - Then, on dropbox.com, they remove Open Dental's app under [connected apps](https://www.dropbox.com/account/connected_apps). That revokes Open Dental's tokens ([help](https://help.dropbox.com/installs-integrations/third-party/third-party-apps)).
- **Folder location.** If Open Dental's app had App-folder access, the existing A to Z tree sits under `/Apps/<Open Dental's app name>/...`, out of reach of the office's new App-folder app. The office either:
  - moves that folder into `/Apps/<new app name>/` in the Dropbox web UI (a server-side move with no re-upload), or
  - registers its app with Full Dropbox access and sets "Dropbox AtoZ Path" to the folder's full path.

  Which applies is unknown until someone looks (open question 2).

## 4. Code changes

### 4.1 Library choice

| | **A. In-repo REST client (recommended)** | B. NuGet `Dropbox.Api` 7.4.0 |
|---|---|---|
| What | `OpenDentalCloud/Storage/DropboxApi.cs`: `HttpClient` plus Newtonsoft.Json 12.0.3 (already in the repo) for the 13 endpoints in 2.4, OAuth (2.3) and the error mapping (3.5). About 500-700 lines | Official SDK: `DropboxClient(refreshToken, appKey, config)` refreshes on its own; `PKCEOAuthFlow` does PKCE ([OAuthPKCE example](https://github.com/dropbox/dropbox-sdk-dotnet/tree/main/dropbox-sdk-dotnet/Examples/OAuthPKCE)) |
| Dependencies | none new. `Required dlls/Dropbox.Api.dll` is deleted | netstandard2.0 only, so .NET Framework 4.8 support is computed, not declared. Needs **Newtonsoft.Json 13.x** (the 7.4.0 package declares `>= 13.0.4`; [NuGet](https://www.nuget.org/packages/Dropbox.Api)), which means a repo-wide Newtonsoft upgrade and binding redirects. Today every project references DLLs by HintPath; there is no `PackageReference` |
| Upside | Full control of timeouts, retries, the circuit breaker and log redaction. Phase 2 has one fewer binary | Less code. Dropbox keeps up with API changes. The SDK README says to use 7.0.0 or newer, because older pinning builds stopped connecting in January 2026 ([README](https://github.com/dropbox/dropbox-sdk-dotnet)) |
| Downside | We own correctness: header escaping, upload sessions | Repo-wide JSON upgrade risk. Still needs our own token cache and error mapping for the single-message and circuit-breaker behaviour |

Choose A unless the Newtonsoft.Json 13 upgrade is already planned. If it is, B is fine at the same size. SDK 4.0 can't stay either way:

- the repo calls it with access tokens only, and nothing there handles refresh tokens or PKCE
- it targets the older routes listed in 1.3

### 4.2 File-by-file changes

| File | Members | Change |
|---|---|---|
| `OpenDentalCloud/OpenDentalCloud.csproj` | references, compile items | A: remove the `Dropbox.Api` reference. Add `Storage/DropboxApi.cs`, `Storage/DropboxSession.cs`, `Storage/DropboxOAuth.cs`, `Storage/DropboxLoopbackListener.cs`. B: swap the HintPath for the 7.4.0 package DLL |
| `OpenDentalCloud/Storage/DropboxOAuth.cs` (new) | `CreatePkce()`, `BuildAuthorizeUrl(appKey, challenge, state, redirectUri, scopes)`, `ExchangeCodeAsync(appKey, code, verifier, redirectUri)`, `RefreshAsync(appKey, refreshToken)`, `RevokeAsync(accessToken)` | Section 2.3. Returns a `DropboxTokenResult {AccessToken, ExpiresUtc, RefreshToken, AccountId, Scope}` |
| `OpenDentalCloud/Storage/DropboxLoopbackListener.cs` (new) | `Start(Uri redirect)`, `WaitForCodeAsync(state, CancellationToken)`, `Dispose()` | Section 3.2, loopback only |
| `OpenDentalCloud/Storage/DropboxSession.cs` (new) | `Get(appKey, refreshToken)`, `GetAccessTokenAsync()`, `SendAsync(...)`, `NeedsReauthorization`, circuit breaker | Section 3.4 and 3.5 |
| `OpenDentalCloud/Storage/Dropbox.cs` | `GetDropboxAuthorizationUrl`, `GetDropboxAccessToken` | **Delete**: these were the HQ-side helpers |
| same | `Upload`, `Download`, `Delete`, `Thumbnail`, `ListFolders`, `Move`, `Copy` constructors | Change `(string accessToken)` to `(DropboxSession session)`; drop `DropboxClient` |
| same | `Upload.PerformIO` | Use 8 MiB `append_v2` chunks (a multiple of 4 MiB). A single `files/upload` is allowed for small files. Keep `mode:overwrite` |
| same | `Download.PerformIO` | Drop the `FileExists` pre-call and map 409 `path/not_found` to "File not found." |
| same | `Thumbnail.PerformIO` | Use `get_thumbnail_v2` |
| same | `Delete.PerformIO` | Use `delete_v2` |
| same | `ListFolders.PerformIO` | Loop `list_folder/continue` while `has_more` (defect 1) |
| same | `Move.PerformIO` | Use `move_v2`/`copy_v2`. Fix the doubled source path (defect 2). List folders with pagination |
| same | `FileExists(...)` | Takes a `DropboxSession`. `path/not_found` gives false; other errors are logged and give false (contract kept) |
| same | new `GetCurrentAccountAsync`, `TestPath` | Back `Dropbox.TestConnection()` |
| `OpenDentalCloud/Shared/TaskState*.cs` | `using Dropbox.Api;` / `using Dropbox.Api.Files;` | Remove the unused imports (option A) |
| `OpenDentBusiness/WebBridges/Dropbox.cs` | `PropertyDescs` | Add `AppKey`, `RefreshToken`, `RedirectUri`, `AccountId`, `AccountName`. Keep `AtoZPath`. Keep `AccessToken` only as the legacy row name |
| same | `AccessToken` getter | **Remove**. Replace with `GetSession()`: it decrypts the refresh token, returns `OpenDentalCloud.DropboxSession`, and throws `DropboxAuthException` when not linked |
| same | `GetDropboxAuthorizationUrl(appkey)`, `GetDropboxAccessToken(code,appkey,appsecret)` | **Delete**. Their comments say they served HQ |
| same | new `EnsurePropertyRows()`, `SaveAuthorization(DropboxTokenResult, account)`, `Disconnect()`, `TestConnection(out string msg)`, `IsAuthorized`, `HasLegacyTokenOnly` | Sections 3.2-3.5 and 3.9 |
| `OpenDentBusiness/WebBridges/CloudStorage.cs` | `Upload`, `Download`, `Delete`, `GetThumbnail`, `Move`, `Copy`, `ListFolderContents`, `FileExists` (lines 86, 139, 165, 194, 223, 257, 285, 316) | Replace `Dropbox.AccessToken` with `Dropbox.GetSession()`. Nothing else changes. All ~234 callers stay untouched |
| `OpenDental/Forms/FormDropboxAuthorize.cs` and `.designer.cs` | whole form | Rewrite as in section 3.2. Remove `IWebServiceMainHQ`, `WebSerializer` and `RegistrationKey`. Remove the public field `ProgramPropertyAccessToken`; the form saves through `Dropbox.SaveAuthorization`. Controls: instructions label, Open browser button, code box, status label, OK, Cancel |
| `OpenDental/Forms/FormPath.cs` and `FormPath.designer.cs` | `_programPropertyDropboxAccessToken`, `LoadDropboxSetup`, `butAuthorize_Click`, `butSave_Click` (lines 428-445), `FormPath_Load`, the Dropbox tab controls | Section 3.8. Gate `radioDropboxStorage` (3.7). Save writes the App key and Redirect URI and validates with `Dropbox.TestConnection()`. Remove the 16.3 warning |
| `OpenDental/Main Modules/FormOpenDental.cs` | startup storage check (lines 690-712) | Replace the property lookup plus `OpenDentalCloud.Dropbox.FileExists(token, ...)` with `Dropbox.TestConnection()`, showing a message for each status (3.5) |
| `OpenDental/Main Modules/ProgramEntry.cs` | comment at 137 | Option A: change the comment. The Newtonsoft force-load stays for its other users |
| `OpenDentBusiness/WebServices/IWebServiceMainHQ.cs`, `WebServiceMainHQNoHq.cs`, `WebServiceMainHQMockDemo.cs` | `GetDropboxAccessToken`, `GetDropboxAuthorizationUrl`, mock `case OAuthApplicationNames.Dropbox` | No caller left, so delete them from the interface and from `WebServiceMainHQNoHq`. `WebServiceMainHQMockDemo` has no Dropbox members of its own: it inherits them from the generated SOAP proxy (`Web References/WebServiceMainHQ/Reference.cs`), so it still compiles. Only its `case OAuthApplicationNames.Dropbox`, which throws `NotImplementedException`, needs tidying. The generated proxy can wait for the general HQ cleanup. Keep `BuildOAuthUrl`: other vendors still use it |
| `OpenDentBusiness/Db/Enumerations.cs` | `OAuthApplicationNames.Dropbox` | **Keep**: it is ordinal 0, and removing it renumbers the rest. Change its summary to "Unused; Dropbox authorizes locally" |
| `OpenDental/Forms/FormLicense.cs`, `FormRegistrationKey.cs`, `OpenDental/Resources/Dropbox_Api.txt`, `Properties/Resources.resx` | Dropbox license entry | A: remove it, since no Dropbox code ships. B: update it to the 7.x MIT text |
| `Required dlls/Dropbox.Api.dll`, `docs/third-party-binaries-audit.md` | binary and audit row | A: delete the DLL and mark the row "removed: replaced by in-repo client" |
| Tests: `NoHqTests`, or a new top-level `CloudTests` project beside `NoHqTests` and `CryptoTests` | new tests | Section 6.4 |

### 4.3 Prebuilt Bridges.dll types to re-implement

**None.** No Bridges.dll type is used anywhere on the Dropbox path: `OpenDentalCloud`, `CloudStorage`, `FormPath`, `FormDropboxAuthorize` and `FormOpenDental`. The only prebuilt binary is `Dropbox.Api.dll`, the vendor's own MIT SDK, handled in 4.1.

### 4.4 "Not available" stubs that are replaced or retired

| Stub | Caller today | After |
|---|---|---|
| `WebServiceMainHQNoHq.BuildOAuthUrl(regKey, "Dropbox")` | `FormDropboxAuthorize_Load` | The caller is replaced by `DropboxOAuth.BuildAuthorizeUrl` with the office's app key. `BuildOAuthUrl` itself stays for the other vendors. `NoHqTests.WebServiceMainHQ_VendorMethodsSayDirectIntegrationIsNeeded` still passes, because it only checks that the generic method throws |
| `WebServiceMainHQNoHq.GetDropboxAccessToken(code)` | `FormDropboxAuthorize.butSave_Click` | Replaced by `DropboxOAuth.ExchangeCodeAsync` (PKCE). The stub and interface member are deleted |
| `WebServiceMainHQNoHq.GetDropboxAuthorizationUrl(long)` | none | Deleted |

### 4.5 Not changed

- `DataStorageType`, `PrefName.AtoZfolderUsed`, the `document` table, the A to Z folder layout
- `CloudStorage`'s public surface
- the SFTP storage path
- `ExternalSourceType.Dropbox` (unused enum value, left as is)

## 5. Credentials the office must register

All on the office's side. No partner agreement, sandbox request or NDA.

1. **Choose the Dropbox account that will hold images.** If the images include PHI, it must be a **team** plan (Standard, Advanced, Business, Business Plus, Enterprise or Education).
2. **Sign Dropbox's BAA before uploading anything.** A team admin of a US-based customer signs electronically: admin console > Account page. Customers elsewhere contact Dropbox sales ([Dropbox and HIPAA/HITECH](https://help.dropbox.com/security/hipaa-hitech-overview)). Dropbox also recommends:
   - restricting sharing
   - disabling permanent deletion
   - monitoring account activity
3. **Create the app.** While signed in to that account, open the [App Console](https://www.dropbox.com/developers/apps) and choose **Create app**:
   - API: **Scoped access**.
   - Access type: **App folder** (recommended) or **Full Dropbox**. Choose Full Dropbox only if an existing A to Z folder must stay where it is (section 3.9).
   - Name: anything that doesn't contain "Dropbox" or begin with "drop", for example "<Practice> Imaging" ([branding rules](https://docs.dropboxapi.com/dropbox-api/docs/developer-resources/branding-guide)).
4. **Permissions tab:** tick `account_info.read`, `files.metadata.read`, `files.content.read` and `files.content.write`, then **Submit**. Leave sharing, file-request, contacts and team scopes off.
5. **Settings tab:**
   - **Copy the App key.** You don't need the App secret, so don't copy it anywhere.
   - **OAuth 2 > Allow public clients (Implicit Grant & PKCE): Allow.** The PKCE exchange without a secret depends on it. Confirm the label in your console; it is checked in 6.2.
   - Optional, for the loopback flow: under **Redirect URIs** add a fixed URI such as `http://localhost:53682/dropbox`, exactly as you'll type it in Setup.
   - Leave the app in **development** status. An internal app that links one account doesn't need production review ([developer guide](https://docs.dropboxapi.com/dropbox-api/docs/developer-resources/developer-guide)).
6. **In the PMS:**
   - Setup > Program Links > **Dropbox**: tick Enabled.
   - Setup > **Data Paths**: choose Store images in Dropbox, paste the **App key**, enter the Redirect URI if you registered one, and click **Authorize**.
   - Sign in on dropbox.com, approve, and paste the code (or let the loopback flow finish).
7. **Create the A to Z folders.** Inside the app folder (or at the full path), create the AtoZ folder with subfolders `A` to `Z`. Then set **Dropbox AtoZ Path** and click **Save**. Save checks that `<path>/A` exists.
8. **If you migrated from Open Dental,** remove Open Dental's app from [connected apps](https://www.dropbox.com/account/connected_apps) once the new link works.
9. **Record who owns the Dropbox app and the account,** and keep the BAA on file. Re-authorize after password resets that unlink apps, or whenever you rotate.

## 6. Test plan

### 6.1 Sandbox setup

- **Dropbox has no sandbox host.** Use a **development-status app** in a **dedicated test Dropbox account**. A free Basic account is enough for functional tests. Testing the BAA steps needs a team trial.
- **Synthetic data only.** Use DevDbTool sample patients and never real PHI, in a dev database from `tools/New-DevDatabase.ps1`.
- **Register two apps:** one App-folder and one Full Dropbox, to cover both path layouts. Register the redirect URI `http://localhost:53682/dropbox` on both.

### 6.2 Manual checks

1. **Fresh database.** The Data Paths screen shows no Dropbox radio. Enabling the Dropbox program link shows it. No Dropbox host is contacted before the office authorizes; confirm in the firewall log.
2. **Paste flow.** Enter the app key and authorize by pasting the code. Check:
   - the linked account name shows
   - the new rows exist (`SELECT PropertyDesc, LEFT(PropertyValue,5), IsMasked, IsHighSecurity FROM programproperty WHERE ProgramNum=<Dropbox>`)
   - the refresh token starts with `osd1$`
   - no token appears in any UI
3. **Public-clients setting.** With "Allow public clients" set to Disallow, the exchange fails with a clear message. With Allow, it succeeds.
4. **Loopback flow.** It works with the registered URI. An unregistered URI makes Dropbox show its error page, and our form times out cleanly; Cancel closes the listener (check with `netstat -ano | findstr 53682`). Test `127.0.0.1` too: is it accepted when only `localhost` is registered?
5. **Folder validation.** Save checks the AtoZ folder. A missing `A` folder gives the not-found message. The A to Z path may be relative to the app folder, or full (Full Dropbox app).
6. **Image workflows:**
   - import an image and a PDF; check thumbnails, open, rotate and save, delete
   - patient merge moves the folder (defect 2 regression)
   - copy
   - sheet with an image; email attachment; claim attachment
   - MedLab HL7 inbound archive through the `OpenDentHL7` service
7. **Non-ASCII.** A patient with a non-ASCII last name (e.g. "Muñoz") round-trips upload, download, list and thumbnail.
8. **Large and empty files.** A file over 150 MiB (multi-chunk session) and a 0-byte file.
9. **A folder with more than 2,000 files** lists completely (pagination, defect 1).
10. **Refresh.** Keep a workstation open past `expires_in`, or clear the in-memory cache through a debug hook. The next call refreshes once (count `oauth2/token` calls in the log) and no database row changes.
11. **Concurrency.** Two workstations and the HL7 service run at once. No refresh writes to the DB. Simultaneous uploads hit 429 at most occasionally and recover.
12. **Revoked access.** Revoke the app on dropbox.com (connected apps). The next action shows one "needs re-authorization" message, not a cascade. Re-authorizing recovers without restarting the other workstations (after the Programs cache refresh).
13. **Different account.** Re-authorizing with another Dropbox account shows the account-switch warning.
14. **Disconnect.** Tokens are revoked: further calls with the old access token get 401. The rows are blanked.
15. **Offline.** Unplug the network:
    - startup shows "could not be reached"
    - the imaging module stays responsive (circuit breaker)
    - non-imaging modules work normally
    - reconnecting recovers within about 30 s
16. **Middle Tier client:** steps 2, 6 and 10.
17. **Migrated Open Dental database (restored copy).** The legacy token is ignored: no API call is made with it (proxy log). The startup message points to Setup. After authorizing, the legacy row is blank.
18. **Hosts and logs.**
    - Firewall or proxy capture shows only `www.dropbox.com` (browser), `api.dropboxapi.com` and `content.dropboxapi.com`.
    - `tools/Test-NoHqHosts.ps1` passes.
    - Logs contain no tokens, codes or patient paths.

### 6.3 Production pilot

One workstation against the office's real team account, with the BAA signed. Run a day of normal imaging use, then roll out to every workstation and the HL7 service host.

### 6.4 Automated tests (no network)

The tests use a fake `HttpMessageHandler`.

- **PKCE:** RFC 7636 Appendix B vector (verifier `dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk` gives challenge `E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM`). Verifier length and alphabet.
- **Authorize URL:** parameter encoding, `scope` string, `redirect_uri` present or absent, `token_access_type=offline`.
- **Token parsing:** authorization_code and refresh_token responses, and the error bodies.
- **`Dropbox-API-Arg`:** "Muñoz" gives `Mu\u00f1oz`, and 0x7F gives `\u007f`. The output is pure ASCII.
- **Session:** 401 leads to one refresh and one retry. A refresh that returns 400 gives `NeedsReauthorization` and no more network calls. Parallel callers share one refresh. 429 waits for `Retry-After`. A network error opens the circuit for 30 s.
- **Listing:** `list_folder` pagination across 3 pages.
- **Move:** path composition for a file and for a folder (defect 2).
- **Upload:** chunk offsets are correct for sizes 0, 1, 8 MiB minus 1, 8 MiB, and 20 MiB plus 3.
- **`EnsurePropertyRows`** is idempotent (DevDbTool database).
- **Encryption:** the refresh token round-trips through `CDT.Class1.Encrypt/Decrypt`. An `Encrypt` failure stores nothing.
- **Loopback listener:** rejects a wrong `state`, a wrong path and non-GET requests, and accepts exactly one request.

## 7. Size, risks and open questions

### 7.1 Size

**M**, about 4 to 6 developer days:

- REST client, OAuth and loopback listener: 2 days
- session, retries and circuit breaker: 0.5 day
- FormPath, FormDropboxAuthorize and the startup check: 1 day
- automated tests: 1 day
- manual QA across image workflows, HL7 and Middle Tier: 1 day

Option B is about the same, plus the Newtonsoft.Json 13 upgrade and its regression testing across every project. There is no vendor lead time: an app key is self-service in minutes. A team plan and BAA depend only on the office.

### 7.2 Risks

1. **Developer Terms §2.6(c).** The terms bar tokens that let several people using the app into one Dropbox account. Shared office storage is exactly that, and Open Dental shipped the same model.
   - Arguments that it is acceptable: under the per-office model the office is the developer, the account owner and the only "user". Staff act for that one business.
   - This is still an interpretation, not a statement from Dropbox. It needs the owner's or counsel's sign-off before the feature is offered.
2. **Ground rule 6 tension.** The plan says patient data and images stay on local storage, but this mode stores them off-site by design. It is optional and off by default, but the owner should confirm it belongs in the product. Alternatives that need no vendor integration:
   - SFTP to the office's own server (`SftpAtoZ`)
   - LocalAtoZ plus any backup or sync tool the office runs itself
3. **Ground rule 5 is met only partly.** With Dropbox storage, imaging is offline whenever the internet is. That follows from the office's choice; the Setup text states it.
4. **PHI and BAA.**
   - Personal Dropbox plans can't sign a BAA.
   - The office must sign it before the first upload.
   - The software can't enforce either. It can at most show `account_type` and team membership, which `get_current_account` returns (open question 7).
5. **`osd1$` is obfuscation.** Database read access exposes the refresh token, which means access to the linked folder (or the whole Dropbox, with Full Dropbox access). Mitigations: App-folder access, a high-security row, database access control, rotation.
6. **Redirect handling.**
   - Redirect ports must match exactly.
   - Whether Dropbox treats `127.0.0.1` as it does `localhost` is not documented. Hence the paste flow is the default and the loopback flow is optional.
   - The "Allow public clients" switch also enables the legacy implicit grant, which this app never uses.
7. **Rate limits and lock contention.** Many workstations write to one namespace. Batch imports (e.g. a large scan session) can hit `too_many_write_operations`, which slows imports. The retry policy handles it.
8. **Owning a client (option A).** Header escaping, upload sessions and future API changes become our code. The scope is small and well documented, and avoiding the Newtonsoft 13 upgrade is the trade.
9. **Behaviour change from fixing Move.** Folder moves (e.g. patient merge) may never have worked on Dropbox before. They will start working, so verify with test data that merges move what's expected.
10. **Migration unknowns.** We don't know where Open Dental's app put the A to Z tree (App folder or root) or how big it is. Moving a large tree in Dropbox is server-side but can take a while.

### 7.3 Open questions

1. **Keep the feature?** Given ground rule 6 and risk 1, the owner may prefer to remove Dropbox storage and point offices at SFTP or local storage. In that case: remove the radio, `FormDropboxAuthorize`, the `OpenDentalCloud` Dropbox classes and `Dropbox.Api.dll`, and keep the enum value and rows for compatibility. That is **S**.
2. **Does the office use Dropbox storage today?** Run `SELECT ValueString FROM preference WHERE PrefName='AtoZfolderUsed'` on the restored `fox-db` copy (2 means Dropbox). If it does, also find where the A to Z tree lives in the account.
3. **Per-office app (proposed) or one project-wide app?** A project-wide app needs:
   - a project Dropbox developer account
   - production review once 50 users link
   - a privacy policy
   - breach-reporting duties
4. **Default access type:** App folder (least privilege, and recommended) or Full Dropbox (no folder move after migration)?
5. **Dropbox team folders / team space.** Should we support `Dropbox-API-Path-Root` and team namespaces? v1 supports only the linked member's own space or app folder.
6. **Offline read cache.** Should there be a local read cache of recently viewed images for offline use? Not in v1. It would put PHI on workstations, so it needs its own design.
7. **Should Setup show a factual PHI notice** ("a BAA with the storage provider is required for PHI"), perhaps triggered by `account_type`? Or does that belong only in the documentation, under ground rule 7?
8. **Library:** option A or B (4.1). That depends on whether the Newtonsoft.Json 13 upgrade is planned anyway.

## References

- Dropbox OAuth guide: https://docs.dropboxapi.com/dropbox-api/docs/oauth
- Authorization reference (`/oauth2/authorize`, `/oauth2/token`): https://docs.dropboxapi.com/dropbox-api/docs/get-started/authorization
- PKCE: what and why: https://dropbox.tech/developers/pkce--what-and-why-
- API reference index: https://docs.dropboxapi.com/dropbox-api/api-reference (endpoint pages linked in 2.4)
- Request and response formats: https://docs.dropboxapi.com/dropbox-api/docs/technical-reference/request-response-formats
- Error handling: https://docs.dropboxapi.com/dropbox-api/docs/error-handling
- Performance guide: https://docs.dropboxapi.com/dropbox-api/docs/performance
- Developer guide (development and production status, permissions): https://docs.dropboxapi.com/dropbox-api/docs/developer-resources/developer-guide
- Branding guide: https://docs.dropboxapi.com/dropbox-api/docs/developer-resources/branding-guide
- Developer Terms and Conditions (effective 2025-03-01): https://www.dropbox.com/developers/reference/tos
- Dropbox and HIPAA/HITECH: https://help.dropbox.com/security/hipaa-hitech-overview
- Third-party apps (unlinking): https://help.dropbox.com/installs-integrations/third-party/third-party-apps
- App Console: https://www.dropbox.com/developers/apps
- API server root certificate changes (2026): https://dropbox.tech/developers/api-server-certificate-changes
- .NET SDK: https://github.com/dropbox/dropbox-sdk-dotnet and https://www.nuget.org/packages/Dropbox.Api
- JSON encoding for `Dropbox-API-Arg`: https://docs.dropboxapi.com/dropbox-api/docs/technical-reference/json-encoding
- Forum, loopback port matching (Dropbox staff): https://community.dropbox.com/discussions/101000014/redirect-uri-with-variable-loopback-port/197621
- Forum, non-ASCII in `Dropbox-API-Arg` (Dropbox staff): https://community.dropbox.com/discussions/101000014/accented-characters-in-file-name/222655
- RFC 7636 (PKCE), Appendix B test vector: https://www.rfc-editor.org/rfc/rfc7636
- RFC 8252 (OAuth for native apps), loopback redirect: https://www.rfc-editor.org/rfc/rfc8252

## Fact-check log (2026-10-18)

- **Feasibility confirmed: public API.** Everything needed is publicly documented, and the office can get it from the App Console on its own. Developer Terms §2.6(c) is still the main open risk (7.2).
- **Code touchpoints checked on `main`:** every file, member and line number cited exists as described. The `CloudStorage` line numbers and the `FormOpenDental` startup check (690-712) match. So do the `FormPath` Save block (428-445), the `ProgramEntry.cs:137` comment, the `NoHqTests` method, the `ConvertDatabases4` 16.3.1 insert (`Enabled=0`, `/AtoZ`, blank token), the `OAuthApplicationNames.Dropbox` ordinal 0 and `ExternalSourceType.Dropbox`.
- **No Bridges.dll surface.** None of the Dropbox files references a Bridges type, and `OpenDentalCloud.csproj` doesn't reference Bridges.dll.
- **Corrections made:**
  - the `CloudStorage` usage count
  - the SDK 4.0 route wording ("deprecated unsuffixed routes", not "v1"); Upload removed from defect 5
  - the defect 2 example
  - the `Dropbox-API-Arg` escaping rule, now cited to the official JSON encoding page
  - the branding rule on "for Dropbox"
  - the BAA signing region, and the third-party-app caveat
  - the 2026 certificate change and why SDK 4.0 isn't affected
  - how `WebServiceMainHQMockDemo` gets its Dropbox members
  - file and test-project names
  - the escaped test vector
  - forum links moved to `community.dropbox.com`
- **No credentials or Open Dental hosts** appear in the design. `opendental.com` is named only as something that is not contacted.
