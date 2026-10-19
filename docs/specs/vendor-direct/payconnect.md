# PayConnect 2 (and VeriFone terminals): direct-to-vendor spec

Status: draft, 2026-10-18, fact-checked the same day against the public docs and `main` (see "Fact-check log" at the end). Baseline: `main` at `6855026f5`. Vendor: DentalXChange (EDI Health Group, Inc.), PayConnect merchant services. API: XConnect **Payment API 2.12.0** (public OpenAPI file).

## Summary

- **The PayConnect 2 calls already go straight to the vendor.** `OpenDentBusiness/WebBridges/PayConnect2.cs` is in-repo source. It sends every request to `https://api.dentalxchange.com/payments/...` with two headers:
  - `API-Key`: the **integrator** key. HQ supplied it.
  - `Secret`: the **merchant** key. The office already enters it per clinic in Setup (`programproperty` "API Secret").
- **HQ supplied one working credential, the API key.** `GetPayConnect2ApiKey()` called `BuildOAuthUrl(regKey,"PayConnect2")`. Despite the method's name, HQ evidently returned the raw key string: the result goes straight into the `API-Key` header, and the enum comment calls it the "PayConnect2 API key". Gating it on the registration key meant only paying Open Dental customers got it.
  - On `main` that call now throws `HqNotAvailableException`, so **every PayConnect 2 call fails**: setup's Merchant Info, the card iFrame, token charges, terminals, voids, refunds and recurring charges.
- **The "DLL key" is dead code.** `GetPayConnect2DLLKey()` is never called anywhere in this repo. It also asks HQ for the wrong app name (`PayConnect2`, not `PayConnect2Dll`). Delete it.
- **No Bridges.dll type is involved.** PayConnect2.cs has a stray `using Bridges;` and nothing else. That line has to go before Bridges.dll can be removed.
- **Direct design:**
  - The office enters its own integrator **API key** in PayConnect Setup. It is stored once for the whole practice as a new `programproperty` row, encrypted as `osd1$`, masked and high-security.
  - The existing per-clinic **Secret** starts being encrypted too. Plain-text rows keep working until their next save.
  - A **Use sandbox** box replaces today's build-type switch, which points Debug builds at a staging host that isn't publicly documented.
  - Auth is two static keys: no OAuth, no browser hop, no tokens and nothing to refresh.
- **VeriFone terminals: removal recommended.** The VeriFone path is PayConnect **1.0**:
  - It runs on the proprietary `DpsPos.dll` over a COM port, plus a VeriFone USB driver that HQ used to host.
  - Open Dental's public manual says DentalXChange has decommissioned PayConnect 1.0.
  - PayConnect 2 terminals are driven by the cloud API (`POST /payments/terminals/transactions`). The public terminal model lists Clover and Ingenico devices, and the v2 code uses no local DLL or key for them.
  - Open Dental's manual still shows a driver step under 2.0 terminal processing, and the setup window shows the same driver button for both versions. So the driver picker stays until DentalXChange confirms that 2.0 terminals don't need it (4.2, 7.3 Q4).
- **Feasibility: public API, with a vendor gate on production keys.**
  - Public: the Payment API reference, an OpenAPI file, a self-service developer portal and a `/sandbox` prefix.
  - Not public: how a production `API-Key` is issued. The portal's sign-up form asks for a "partnering interest", and nothing published says whether DentalXChange will issue a production key to a single practice acting as its own integrator.
- **Size: M**, about 4 to 6 days, plus vendor lead time for the production key. Removing PayConnect 1.0 and VeriFone is another **M** and can ship separately.

## 1. What the integration does, and what HQ provided

### 1.1 What the office uses it for

PayConnect is DentalXChange's card-processing service for dental offices. In this codebase, with Program Version 2 selected (`programproperty` "Program Version" = 2), it does the following:

| Feature | Where | API call |
|---|---|---|
| Take a card payment by keying or swiping into a DentalXChange-hosted form ("Embedded Payment", Payment type), shown in a WebView2 window | `FormPayConnect2` (web service mode, no saved card), `FormPayConnect2iFrame` | `POST /payments/embeds/sessions`, then `GET /payments/transactions/status` |
| Save a card as a token with no charge (Tokenizer type) | `FormCreditCardManage` → `FormPayConnect2iFrame(isAddingCard:true)` | `POST /payments/embeds/sessions` |
| Charge or authorize a saved token | `FormPayConnect2`, `PayConnect2.PostCreateTransactionByToken` | `POST /payments/transactions` (with or without surcharge) |
| Recurring charges on saved tokens | `RecurringCharges.SendPayConnectV2` (run from `FormCreditRecurringCharges`) | `POST /payments/transactions` with `frequency: Recurring` |
| Charge on a counter terminal | `FormPayConnect2` (terminal mode, `payterminal` rows) | `POST /payments/terminals/transactions` |
| Void, full or partial refund | `FormPayConnect2`, `FormPayment.VoidPayConnectTransaction` | `PUT /payments/transactions/voids`, `POST /payments/transactions/refunds` |
| Attach an on-screen signature | `FormPayConnect2.SendSignature` | `PUT /payments/transactions/signature` |
| Check credentials ("Merchant Info") | `FormPayConnectSetup.butMerchantInfo_Click` | `GET /payments/merchants` |
| Surcharge accounts (the card fee is passed to the cardholder where state law allows) | `programproperty` "PayConnect2.0 Integration Type: 0 for normal, 1 for surcharge" | same create route, with the surcharge body |

The integration is optional and off by default (`program.Enabled` for PayConnect). Button visibility today is weaker than it looks:
- **Clinics off:** the Payment window's PayConnect button shows whenever the program is enabled, with no check for a Secret (`FormPayment.cs` 1241–1242).
- **Clinics on:** it needs a Secret and a valid payment type for the payment's clinic (`FormPayment.cs` 1255–1259).
- `FormClaimPayEdit.cs` (~198–210) has its own PayConnect button with the same logic.

4.2 replaces both checks with `PayConnect2.IsConfigured`. Patient-portal payments (the "Payment Portal Payments" group) belonged to Open Dental's hosted eServices and are out of scope; see 4.4.

### 1.2 What Open Dental's HQ provided

From `D:/Dev/opendental-schema/hq-vendor-table.md` (row "PayConnect 2 (and VeriFone driver)") and the code:

| What | How it reached the office | State on `main` |
|---|---|---|
| **PayConnect 2 integrator API key** (the `API-Key` header) | `PayConnect2.GetPayConnect2ApiKey()` (`PayConnect2.cs` 27–41): the Introspection override `PayConnect2Key` if one is set, otherwise `WebServiceMainHQProxy...BuildOAuthUrl(RegistrationKey,"PayConnect2")`. The result is cached in a static field for the life of the process. | `WebServiceMainHQNoHq.BuildOAuthUrl` (62–64) throws `HqNotAvailableException`, which comes back wrapped as "Error occurred retrieving PayConnect key: ...". Every v2 request fails before it is sent. |
| **"DLL key"** | `GetPayConnect2DLLKey()` (43–57), same pattern with Introspection `PayConnect2DLLKey`. It passes `OAuthApplicationNames.PayConnect2` instead of `PayConnect2Dll`, an upstream copy-paste slip. | **Never called** in this repo. Inferred to be reserved for a DentalXChange terminal library that upstream added after 24.3. Nothing here consumes it. |
| **VeriFone USB driver** (`VeriFoneUSBUARTDriver_Vx_1.0.0.52_B5.zip`) | Downloaded from opendental.com and its `Setup.exe` run (`FormPayConnectSetup.butDownloadDriver_Click`) | Commit `7d0d01dcd` replaced the download with a local zip file picker. The picker still runs `Setup.exe` from the zip. |
| **Kill switch** | `Programs.IsEnabledByHq` blocked enabling PayConnect | Now a no-op that only checks that the program row exists (`Programs.cs` 108–122) |
| Customer gating | The registration key went with the key request, so only Open Dental customers received the integrator key | Gone with the registration key |

The API key is not the only Open Dental credential in the code. **PayConnect 1.0 has Open Dental's integrator identity compiled in:**
- the `Client` names and DCI `ServiceID` values in `PayConnect.GetCredentials` (`PayConnect.cs` ~30–42)
- the same in `PayConnectREST.GetClientRequestHeaders` and `GetClientRequestHeadersForWebURL` (a second class in the same file, `PayConnect.cs` ~741–756). The portal variant carries a secret-looking `ServiceID` value.

They don't come from HQ, but they are Open Dental's credentials, and they break the "never compiled in" rule. They go away with PayConnect 1.0 (4.5). This spec does not reproduce their values.

The Introspection overrides (`PayConnect2Key`, `PayConnect2DLLKey`, `PayConnectRestURL`) are a developer testing mode, not office configuration:
- They only apply when the `IntrospectionItems` preference exists.
- In that mode, `GetOverride` throws for any entity missing from the JSON (`Introspection.cs` 91–103).
- That mode also switches PayConnect 1.0 to test service IDs.

## 2. The vendor's public API

### 2.1 Documentation status

- **Public.** The reference is at <https://developer.dentalxchange.com/payment-api>. It renders the OpenAPI 3 file <https://portal-assets.dentalxchange.com/docs/payment-api.yaml> (`info.version` 2.12.0), with a PDF copy (<https://api.dentalxchange.com/docs/latest/payment-api.pdf>) and a Postman workspace (<https://www.postman.com/dentalxchange/workspace/xconnect-apis>).
- Narrative pages (their text is injected by script, so a plain text fetch may show only the menu):
  - Embedded Payment, the hosted iFrame: <https://developer.dentalxchange.com/embedded-payment>. Its getting-started list begins by having the integrator request an API key and a Secret.
  - Sandbox: <https://developer.dentalxchange.com/sandbox>. It describes the sandbox as a **simulated** copy of the API that checks requests and responses against the schema, and it warns that the sandbox gives only a limited view. Do not assume it processes cards, declines, terminals or hosted-form sessions realistically (6.2).
- Sign-up is self-service on a Tyk developer portal: <https://developer.dentalxchange.com/register/>. The form asks for company details and a "brief description of your partnering interest".
- The API support contact in the spec is `payconnect@dentalxchange.com`.
- **Not public:**
  - how production keys are approved
  - integrator or partner agreement terms
  - rate limits
  - terminal timeouts
  - how long an embed session lasts, and whether it is single-use
  - the host that serves the embed session URL
  - test card data
  - how far the sandbox simulates real processing (terminals, declines, the hosted form)
  - whether the sandbox and production keys are the same. The OpenAPI Sandbox tag says the sandbox takes the same `API-Key` and `Secret`, but the Sandbox page says going live needs a production `API-Key`.

### 2.2 Base URLs

| Environment | Base used by our routes | Source |
|---|---|---|
| Production | `https://api.dentalxchange.com/payments` | OpenAPI `servers` is `https://api.dentalxchange.com` and every path starts with `/payments/` |
| Sandbox | `https://api.dentalxchange.com/sandbox/payments` | Sandbox tag in the OpenAPI file and the Sandbox page: put `/sandbox` in front of any endpoint, e.g. `/sandbox/payments/merchants`. They say it uses the same endpoints, keys, validations and models. |
| `https://staging-api.dentalxchange.com/payments` | Today forced for every Debug build (`PayConnect2.GetApiBaseUrl`, 196–202) | Not in the public docs. It looks like a partner staging host. **Drop it.** |

The Embedded Payment session URL comes back in the `url` field of the embed-session response. Its host is not documented (the docs show a placeholder). See 3.6.

### 2.3 Authentication, step by step

There is no OAuth. Every request carries two API-key headers (`components.securitySchemes`: `API-Key` and `Secret`, both `in: header`):

1. **Integrator registration.** Register on the developer portal, request sandbox access from the portal menu, then retrieve an API key (Sandbox page). Once the sandbox integration works, the Sandbox page says "a production API-Key" is all that is needed to go live. How that key is approved isn't published.
2. **Merchant secret.** Each office (or each clinic with its own merchant account) gets a **Secret** from PayConnect. Open Dental's public manual tells offices that PayConnect provides the Secret and that it is entered in setup (<https://opendental.com/manual/payconnectsetup.html>).
3. **Each request** sends `API-Key: <integrator key>`, `Secret: <merchant secret>` and `Content-Type: application/json` over HTTPS. That is exactly what `PayConnect2.GetHeadersForApi` already builds.
4. **Error shapes** (OpenAPI `components.responses`):
   - **401:** either `{ "error": "Authorization field missing" }`, a plain string meaning the API key is missing or unknown, or `{ "errorType":"RESPONSE", "error": { "message": ... } }`, a secret error.
     - The spec's sample message for the secret error reads "Invalid API key", even though it is the Secret case. Tell the two cases apart by the body's shape (string `error`, or `errorType` with an object), never by the message text.
   - **403:** `{ "error": "Access to this API has been disallowed" }`, meaning the key isn't entitled to the Payment API.
   - **400:** `errorType: VALIDATION` with an array of `{message,param,location}`.
   - **404 and 500:** `errorType: RESPONSE`.

Nothing expires, so there is nothing to refresh. Rotating a key means typing in the new one.

### 2.4 Endpoints and fields we need

All amounts are integers in cents.

| Endpoint | Use | Request fields we send | Response fields we read |
|---|---|---|---|
| `GET /payments/merchants` | Test connection, "Merchant Info" | none | `merchantId, name, address…, dxcGroupId, status, refundWithoutReferenceEnabled, text2PayEnabled` |
| `POST /payments/transactions` | Token sale or authorize, one-time or recurring | `amount, type (Sale/AuthorizeOnly), frequency (OneTime/Recurring), cardToken, expiry (MMYY or YYYYMM), cardHolder, zipCode` (required for surcharge accounts), optional `invoiceNumber`, `documentNumber`, `cvv` | `referenceId, transactionId, status` (Processed/Authorized/Declined/Error/…), `authCode, amount, amountAuthorized, amountCaptured, amountSurcharged, surchargePercent, merchantId, orderId, paymentMethod.cardPaymentMethod{cardToken,cardLast4Digits,expiry,network,cardHolder}` |
| `PUT /payments/transactions/voids` | Void before the batch settles | `referenceId` only (the 2.12 schema has `additionalProperties: false`) | as above, plus `amountVoided` |
| `POST /payments/transactions/refunds` | Refund after settlement, full or partial | `referenceId`, optional `amount` | as above, plus `amountRefunded` |
| `GET /payments/transactions/status` | Look up one transaction | query `referenceId`, `transactionId` or `orderId` | same transaction model |
| `GET /payments/transactions/list` | **New:** reconcile after a timeout | `dateFrom`, `dateTo` (YYYY-MM-DD, within the last 30 days, at most 30 days apart), `page`, `limit` | transactions, including `invoiceNumber`, `documentNumber` and `terminal` |
| `PUT /payments/transactions/signature` | Attach a signature image | `referenceId` (or `transactionId`; 2.12 still has both request shapes), `signature` (base64) | `status` |
| `POST /payments/terminals/transactions` | Sale or authorize on a counter terminal | `amount, terminal` (ID or serial), `type, frequency, signature` (bool), optional `invoiceNumber, documentNumber` | transaction model, plus `amountAuthorized/Captured` |
| `GET /payments/terminals` | **New, optional:** import the merchant's terminals into `payterminal` | none | `terminalId`, `terminal` (serial), `name, type` (Clover/Ingenico/null), `capabilities{emv,nfc,msr,pinPad,signature,printer}` |
| `POST /payments/embeds/sessions` | Get a hosted card-entry URL | `type` (Tokenizer/Payment), `swiper, amount, usePatient, useInvoiceNumber, buttonLabel`. We don't send `surcharge`; the schema defaults it to **true**, so a surcharge merchant gets the fee on the hosted form, as today. | `url, orderId` |
| `GET /payments/health` | Optional check that the service is up | none | health flags |

**Embedded Payment results** (Embedded Payment page): the hosted page `postMessage`s `{status, response}` to its parent. `status` is one of:
- `ONLOAD`
- `SUCCESS`: the response carries `cardToken`, `cardHolder`, `expiryMonth/Year`, `zipCode`, and for Payment also `referenceId`, `authCode`, amounts, `type` and transaction `status`
- `ERROR`
- `SESSION_EXPIRED`

The docs say the hosted form keeps the integrator out of PCI audit scope.

Endpoints we deliberately don't use:
- **Text-to-Pay** (`/payments/texttopay*`): it texts payment links to patients, an eServices-type feature.
- `transactions/status/patients`: it needs a patient account number sent to the vendor.
- **Terminal receipt printing:** optional later.
- The invoice-number variants of void and refund that `PayConnect2.cs` still models (`PutVoidWithInvoiceNumber`, `PostRefundWithInvoiceNumber`): nothing calls them, and the 2.12 schemas accept only `referenceId`.

### 2.5 Rate limits

- **Not documented.** No endpoint in the OpenAPI file defines a 429 response, and no page mentions throttling.
- Our traffic is user-driven, one call per action. The only batch is a recurring-charge run, which goes sequentially, one call per card.
- The design never polls in a loop and never retries a POST automatically (2.6).
- Ask DentalXChange for limits and for how long a terminal transaction may run (7.3).

### 2.6 Terms that matter

- **Integrator access.** The API key comes from DentalXChange's developer portal. The production key's terms (agreement, fees, certification, whether one key may serve several offices) are not published.
  - The vendor and partner pages point to a partnering form and 800.576.6412 ext. 459 (<https://payconnect.dentalxchange.com/partner/Partners>, <https://payconnect.dentalxchange.com/partner/Vendors>).
  - The site-wide user agreement (<https://www.dentalxchange.com/user-agreement>) only grants personal, non-commercial use of the websites. Commercial API use therefore rests on whatever DentalXChange issues with the production key.
- **Merchant account.** Each office needs a PayConnect merchant account. DentalXChange's FAQ says a merchant account plus processing equipment is required, and that PCI onboarding (questionnaire, possibly a network scan) follows sign-up (<https://dentalxchange.com/resources/faq>). Merchant Services contact: 800.576.6412 ext. 472 (<https://payconnect.dentalxchange.com/provider/payconnect/MerchantProcessing>).
- **PCI.**
  - Card numbers are never keyed into our own windows. Only the hosted form (or the terminal) sees them, and we store tokens.
  - The spec advises against sending clear card data; we never do. See 7.2 for one code path that needs checking.
  - The office is still a merchant with its own PCI duties.
- **Surcharging** is regulated state by state and by card-network rules. The public DentalXChange pages don't describe how a merchant gets a surcharge account; ask Merchant Services. The surcharge request body requires `zipCode`.
- **Hardware.** The Merchant Services page lists a stand-alone Verifone terminal, a MagTek PC card reader, a Topaz signature pad and Clover POS. Whether each one works with the API's terminal routes is not stated (7.3).
- **PHI and a BAA.**
  - The API accepts a `patient` object with name, address, phone, email and account number. The public pages say nothing about a business associate agreement for PayConnect.
  - Our design sends **no `patient` object**. We keep `usePatient=false` and `useInvoiceNumber=false` on the hosted form.
  - **But a patient name does go out today.** For token charges, the required `cardHolder` field is filled with the *patient's* name (`PostCreateTransactionByToken`: `pat.GetNameFLnoPref()`, called from `FormPayConnect2` and from recurring charges with the card's patient), not the name on the card. `creditcard` has no cardholder-name column, and we make no schema change, so the design keeps this. Treat it as a patient name tied to a payment, and include it in the BAA question (step 6e, 7.3 Q6).
  - Any `invoiceNumber` or `documentNumber` we generate is opaque, never a PatNum or a patient name.
  - The office should still ask whether DentalXChange's merchant agreement covers HIPAA, and sign a BAA if it advises one.
- **Support boundary.** Open Dental's manual tells PayConnect 2 offices to contact PayConnect for compatible terminals. Terminals are registered to the merchant account by DentalXChange.

## 3. Desktop design

### 3.1 Auth flow on the desktop

**Static API keys, which is the client-credentials case.** No system browser, loopback redirect, PKCE or device code is needed.

1. In **Setup > Program Links > PayConnect** (Version 2.0), an administrator enters:
   - **API key**, practice-wide: the integrator key from the developer portal
   - **Secret**, per clinic or practice: the merchant secret from PayConnect
   - **Use sandbox**, practice-wide: a check box
2. **Merchant Info** becomes a **Test connection** button. It calls `GET /payments/merchants` with the typed values, before they are saved. Success shows the merchant name and status; failure shows the mapped message from 3.5.
3. Every call builds its headers from the stored values: decrypt, then send. Calls run on the workstation where the user clicked, through `ProgressWin` for the long ones. Nothing goes through a server, eConnector or Middle Tier hop: `PayConnect2` has no `Meth.Get*` remoting.
4. Card entry uses the vendor's hosted session URL in WebView2, as today. The API key and secret never reach the browser control. They are only used server-to-server, from our process to `api.dentalxchange.com`, to mint the session URL.

### 3.2 Where tokens and credentials live

All values live in `programproperty` rows of the PayConnect program, so there is no schema change and no ConvertDatabases step. Missing rows are added at runtime (below).

| PropertyDesc | ClinicNum | Value | Flags | Status |
|---|---|---|---|---|
| `API Key` | 0 only (practice-wide) | `CDT.Class1.TryEncrypt(key)` (`osd1$…`) | `IsMasked=1`, `IsHighSecurity=1` | **new row** |
| `API Secret` | 0 and each clinic | `TryEncrypt(secret)` on save; read with `TryDecrypt`, which passes legacy plain text through unchanged | set `IsMasked=1`, `IsHighSecurity=1` when written | existing rows (`ConvertDatabases7` To23_1_x) |
| `PayConnect2 Use Sandbox` | 0 only | `0`/`1`, default `0` | none | **new row** |
| `Program Version`, `PayConnect2.0 Integration Type: …`, `TerminalProcessingEnabled`, `DefaultProcessingMethod`, `PaymentType`, `PayConnectPreventSavingNewCC` | per clinic | unchanged | unchanged | existing |
| `payterminal` table (`Name`, `TerminalID`, `ClinicNum`) | per clinic | terminal serials | – | existing table |

Rules:

- **Insert missing rows at runtime, on Save only.** `FormPayConnectSetup_Load` calls a new `PayConnect2.AddMissingProperties(List<ProgramProperty>,programNum,listClinicNums)`. It adds in-memory `ProgramProperty` objects (`ProgramPropertyNum=0`) for any missing `API Key`, `PayConnect2 Use Sandbox` or per-clinic `API Secret` row.
  - The form's existing `ProgramProperties.Sync(...)` inserts them on Save. Crud `Sync` inserts rows whose primary key is 0.
  - Opening the window writes nothing.
- **Reading the API key:** `ProgramProperties.GetPropVal(programNum,"API Key",0)`, then `TryDecrypt`. Always ClinicNum 0. `ProgramProperties.InsertForClinic` copies ClinicNum-0 rows to new clinics; those copies are ignored, and an optional cleanup excludes `API Key` from that copy.
- **Flags on copied rows:** `InsertForClinic` copies only `ProgramNum, PropertyDesc, PropertyValue, ComputerName`, so a new clinic's `API Secret` copy has `IsMasked=0` and `IsHighSecurity=0` (and carries the ClinicNum-0 ciphertext). `AddMissingProperties` and Save must therefore set both flags on every `API Secret` row, not just on rows they create.
- **Encryption** is deterministic (`CDT/SecretBox.cs`), so an unchanged secret saves to the same ciphertext. The form's existing high-security audit (`FormPayConnectSetup.butSave_Click` 563–579) then logs only real changes.
  - One exception: the first save after upgrade turns a plain-text Secret into `osd1$` and logs it once. That is expected.
- **Who can edit:** `ProgramProperties.CanEditProperties` already requires `ManageHighSecurityProgProperties` when any row is high-security.
  - The API key box is read-only for clinic-restricted users, because it affects all clinics.
  - Both boxes are masked (`UseSystemPasswordChar`), with no "show" toggle; re-typing replaces the value.
- **Developer override:** `Introspection.PayConnect2Key` and `PayConnectRestURL` stay as overrides checked first, for engineers only. They are never the documented office path.
- **Never logged:** header values are never written to logs, exceptions or `SecurityLogs`. The existing code doesn't log them, and the new HTTP helper must not either.
- **Threat model:** `osd1$` is obfuscation with a fixed public key (`CDT/README.md`, "Threat model"). It stops casual disclosure, not someone who has a database copy and this source.
  - A leaked API key plus Secret could void or refund that merchant's transactions. Refunds go back to the original card.
  - If a database copy leaks, the office should rotate both keys with DentalXChange.

### 3.3 Refresh handling

- **There are no tokens to refresh.**
- Drop the static `_payConnectApiKey` cache. Read the key from the `ProgramProperty` cache on each call (it is cheap), so a key changed in Setup takes effect on every workstation at the next cache refresh (`InvalidType.Programs` signal). No restart needed.
- **Card tokens** (`creditcard.PayConnectToken`) don't expire on a schedule. They follow the card's expiry (`PayConnectTokenExp`), as today.
- **Embed session URLs** can expire or become invalid; the hosted page then posts `SESSION_EXPIRED`. Their lifetime, and whether they are single-use, is not published. On `SESSION_EXPIRED` the window says so and offers **Reload**, which gets a new session (3.5).
- **Sandbox switch safety:** while "Use sandbox" is on, nothing is really charged, yet the program would still record payments. Show a clear "Sandbox (test) mode" label in the PayConnect windows, and add "SANDBOX" to the payment note and receipt, so test rows can't pass for real ones.

### 3.4 Duplicate safety and reconciliation

The API has no idempotency key, so a lost response can't simply be retried.

- **Never retry a POST or PUT automatically.** Only GET status and list calls may be retried, once, after 2 seconds.
- **Timeouts per route:**
  - 30 s by default
  - 190 s for `terminals/transactions`
  - Today's `WebClient` default of 100 s is shorter than the "wait up to 2 minutes" message already shown on the terminal. A slow customer can make the call time out while the terminal still approves the charge.
- **Correlation ID.** Every create (token, terminal) sends an opaque `invoiceNumber` = `"OPMS-"+<12 hex chars of a new Guid>`. It is kept on the form, and no patient data goes into it.
- **On a timeout or connection drop after the request was sent:**
  1. call `GET /payments/transactions/list` with `dateFrom` = yesterday and `dateTo` = today (paging until found or exhausted). The docs don't say which time zone the dates use, so one extra day covers a transaction near midnight.
  2. match on `invoiceNumber`
  3. if found, continue as if the response had arrived: payment row, receipt, saved card
  4. if not found, show today's warning that the payment may or may not have gone through, and add the correlation ID to the security log entry so staff can find it in the PayConnect portal
- **Embedded Payment:** the embed response's `orderId` is kept. If the WebView2 window closes after a `SUCCESS` but before our status call returns, `GET /payments/transactions/status?orderId=...` recovers the result.

### 3.5 Errors and offline behaviour

| Situation | Behaviour |
|---|---|
| API key or Secret not configured | The PayConnect button stays hidden in the Payment window (`FormPayment` ~1255 also checks `PayConnect2.IsConfigured(clinicNum)`). Recurring charges mark those cards failed with "PayConnect is not set up for this clinic" instead of aborting the batch: today an exception from `GetHeadersForApi` escapes the `try/finally` in `RecurringChargerator` (~422–497). |
| 401 `Authorization field missing` / 403 `disallowed` | "The PayConnect API key was not accepted. Check the key in PayConnect Setup, or whether it is enabled for the Payment API." |
| 401 secret error | "The PayConnect Secret for this clinic was not accepted." |
| 400 VALIDATION | List each `param: message`. Today's code collapses this to "Validation error" in recurring charges. |
| Declined / Error status | As today: shown in the window, payment not created, recurring charge marked declined |
| No network, DNS failure, TLS failure | "Could not reach PayConnect. No charge was made." This is only safe to say when the request never left (connect or DNS failure). After the request was sent, follow 3.4. |
| Timeout after send | Reconcile (3.4) |
| Hosted form `ERROR` / `SESSION_EXPIRED` | Show the message. Offer Reload, which calls a new embed session. |
| Vendor down | The rest of the program is untouched. Staff can still record a card payment taken on a stand-alone terminal as an ordinary payment. Core offline use (ground rule 5) never depends on PayConnect. |

Robustness fixes in the same change:
- `FormPayConnectSetup.butMerchantInfo_Click` (436–472) dereferences a null response after an exception. It also calls `JObject.Parse` on `Error`, which throws when the 401 body's `error` is a plain string.
- `PayConnect2.GetTransactionStatus` (161–172) overwrites the query string when it gets both arguments, where it should append.

### 3.6 Hosts contacted, and no HQ

- **Only DentalXChange hosts:**
  - `api.dentalxchange.com`, for both the API and the sandbox prefix
  - whatever host the embed-session `url` points at, which WebView2 loads
- **WebView2 hardening** (`FormPayConnect2iFrame` 66–67, `FormPayConnect2` ~410–413):
  - **How it works today.** WebView2 navigates its top-level page straight to the session URL. `AddScriptToExecuteOnDocumentCreatedAsync` then injects a small relay script into that vendor page. The script forwards **every** `window` `message` event to `chrome.webview.postMessage`, whatever its origin.
  - **So checking `args.Source` alone is not enough.** `args.Source` is the URL of the top-level document, which is always the vendor page. It says nothing about which frame or origin sent the original `postMessage`.
  - **Preferred fix, to confirm in the sandbox:** follow the vendor's documented pattern. Load a small local wrapper page, served through `SetVirtualHostNameToFolderMapping` on a reserved, never-resolving host such as `https://cardentry.invalid/`. The wrapper puts the session URL in an `<iframe>` and listens for `message` itself. It forwards a message only when `event.origin` is on the allowed list, and passes the origin along so C# can check it again.
    - This way none of our script runs inside the vendor's card-entry page. That matters for the integrator's PCI scope, which the hosted form is meant to keep small.
    - Risk: whether the vendor page posts to a parent with a fixed `targetOrigin` isn't documented. If it does, the wrapper needs an origin the vendor accepts.
  - **Fallback:** keep today's relay, but have the injected script drop any event whose `event.origin` is not on the allowed list. C# also checks `args.Source`.
  - Allowed origins: HTTPS with host `dentalxchange.com` or a subdomain of it, held in one constant (7.2 risk 8).
  - Cancel `NavigationStarting` to any other host, so a hosted page can't take the window elsewhere.
  - Leave out the swiper button if no reader is configured (cosmetic).
- **No Open Dental hosts and no HQ:**
  - `PayConnect2.cs` no longer touches `WebServiceMainHQProxy`.
  - `WebServiceMainHQNoHq.BuildOAuthUrl` stays for the other app names but stops being reached for PayConnect.
  - The VeriFone driver download is already gone.
- `tools/Test-NoHqHosts.ps1` must still pass. The `staging-api` Debug override goes.

### 3.7 Vendor neutrality (ground rule 7)

- Remove `linkLabel1`, "The PayConnect website is at www.payconnect.com" (`FormPayConnectSetup` designer line 81, handler 365–372). It is a vendor link on a setup screen.
- The setup window describes what each field is for and nothing more. It has no sign-up, pricing or "get a merchant account" links.
- PayConnect stays disabled by default, and its Payment-window button only shows once it is enabled and configured.

## 4. Code changes

### 4.1 `OpenDentBusiness/WebBridges/PayConnect2.cs`

| Member | Change |
|---|---|
| `using Bridges;` (13), `using OpenDentBusiness.com.dentalxchange.webservices;` (14), `using System.Windows.Controls;` (16) | Delete. None of them is used; the first is the file's only tie to the prebuilt Bridges.dll. |
| `_payConnectApiKey`, `GetPayConnect2ApiKey()` (21–41) | Replace with `GetApiKey()`: Introspection override if present, otherwise `TryDecrypt(GetPropVal(prog,"API Key",0))`. Throws `ODException("PayConnect API key is not set. Enter it in Setup, Program Links, PayConnect.")` when empty. No static cache, no HQ. |
| `_payConnectDllKey`, `GetPayConnect2DLLKey()` (23–24, 43–57) | **Delete.** It has no callers. |
| `GetApiSecretForClinic` (214–217) | Wrap in `CDT.Class1.TryDecrypt`. Callers: `FormPayment` 1256, `FormClaimPayEdit` 204, `GetHeadersForApi`. |
| new `IsConfigured(long clinicNum)` | True when the program is enabled, version 2, and both API key and clinic Secret are non-empty |
| `GetApiBaseUrl()` (196–202) | Introspection `PayConnectRestURL`, otherwise `https://api.dentalxchange.com/sandbox/payments` when "PayConnect2 Use Sandbox" is 1, otherwise `https://api.dentalxchange.com/payments`. Remove the `ODBuild.IsDebug()` staging override. |
| `GetHeadersForApi` (204–212) | Add an overload taking explicit key and secret, for Test connection before saving |
| `Request` (226–286) | Move to a shared `HttpClient` with per-route timeouts (3.4). Keep the `Mock` hook. Parse 401/403 bodies whose `error` is a string. Return a typed `RequestOutcome` (NotSent / SentNoResponse / Response) so callers can choose between "no charge" and "reconcile". |
| `GetTransactionStatus` (161–172) | Fix the query-string concatenation. Add an `orderId` parameter. |
| new `GetTransactionList(DateTime from, DateTime to, int page, int limit)` | `GET /transactions/list`, for reconciliation |
| new `GetTerminals()` | `GET /terminals`, for the optional import button |
| `CreateTerminalTransactionRequest`, `CreateTransactionRequest` | Add `DocumentNumber`. Callers set `InvoiceNumber` to the correlation ID. |
| `EmbedSessionRequest.Amount` | Make it `int?`, so a Tokenizer session doesn't send `amount: 0` (`NullValueHandling.Ignore` doesn't drop a non-nullable 0). Check in the sandbox. |
| `EmbedSessionResponse` | Add `OrderId` |
| `PutVoidWithInvoiceNumber`, `PostRefundWithInvoiceNumber`, `VoidInvoiceNumberRequest`, `RefundInvoiceNumberRequest` | Delete. Nothing calls them, and the 2.12 void and refund schemas accept only `referenceId`. |
| `PutSignatureWithTransactionID`, `AddSignatureTransactionIDRequest` | Optional delete: nothing calls them. The route is still valid in 2.12 (`AddSignatureWithTransactionIDRequest`). The schema types `transactionId` as an integer, while the model sends a string, so fix the type if it is kept. |
| `CreateTransactionRequest.Patient`, `CreateTerminalTransactionRequest.Patient` | Never set today. Keep them unset (2.6), or remove the fields so no one fills them later. |
| new `AddMissingProperties(...)` and property-name constants | Add `ApiKey="API Key"`, `ApiSecret="API Secret"`, `UseSandbox="PayConnect2 Use Sandbox"`, `IntegrationType="PayConnect2.0 Integration Type: 0 for normal, 1 for surcharge"` to `PayConnect.ProgramProperties` (`PayConnect.cs` 529–536). Replace the string literals in `PayConnect2.cs`, `FormPayConnectSetup`, `FormPayment` and `PayConnectTerminal.cs` with them. |

### 4.2 Forms (`OpenDental`)

- **`Forms/FormPayConnectSetup.cs` and `.designer.cs`:**
  - Version 2 layout (`ResetUIForVersion` 152–187): add a practice-wide **API key** text box and a **Use sandbox** check box in the version group, outside the clinic group. Mask the existing `textAPISecret`.
  - `FillFields` (104–150): decrypt both.
  - `UpdateListProgramPropertiesForClinic` (271–311): encrypt the Secret. Write the API key and sandbox flag on the ClinicNum-0 rows.
  - Load (46–102): call `AddMissingProperties`.
  - `butSave_Click` (474–582): v2 and enabled requires an API key as well as a Secret. Drop the meaningless `IsEnabledByHq` call (481).
  - Rename **Merchant Info** to **Test connection** (it still lists the merchant details). Fix the error handling (3.5).
  - Optional: an **Import from PayConnect** button on the Terminals group (`GET /terminals` → `payterminal` rows for the selected clinic, skipping serials already present).
  - Remove `linkLabel1`.
  - `butDownloadDriver` ("Download Driver") does **not** depend on the version. `checkTerminal` ("Enable terminal processing", in `groupPaySettings`, shown for both versions) toggles it (`checkTerminal_CheckedChanged` 374–376). Open Dental's public manual still lists a driver step under 2.0 terminal processing, and the handler's comment names only the VeriFone USB driver.
    - Until DentalXChange answers 7.3 Q4, keep the button for 2.0 as the existing local zip picker, with a neutral label ("Install terminal driver from file...").
    - Hide it for 2.0 only if DentalXChange confirms that no 2.0 terminal needs a local driver.
- **`Forms/FormPayConnect2.cs`:**
  - Correlation ID and reconciliation for the token and terminal paths (`ProcessTransaction` 135–297).
  - `SESSION_EXPIRED`/`ERROR` handling and origin checks in `GetTransactionResult` (447–).
  - Keep `orderId` from `GetiFrameUrl` (426–445).
- **`Forms/FormPayConnect2iFrame.cs`:** the same message-origin, navigation and `SESSION_EXPIRED` handling (66–67, 103–129). Remove the dead Thinfinity branches as part of the existing Thinfinity removal.
- **`Forms/FormPayment.cs`:** `butPayConnect` visibility (1238–1260) uses `PayConnect2.IsConfigured` for version 2, with clinics on or off (today the clinics-off branch never checks the Secret). The v2 void (~3381–3384) gets the 3.4 timeout handling.
- **`Forms/FormClaimPayEdit.cs`** (~198–210): the same `IsConfigured` change for its PayConnect button.
- **`Forms/FormCreditCardManage.cs`** (425–455): behaviour unchanged. See 7.2 for the track-2 check.

### 4.3 Other business-layer touchpoints

- **`OpenDentBusiness/Data Interface/RecurringCharges.cs`:**
  - `SendPayConnectV2` (971–): pre-check `IsConfigured(clinicNumCur)`, and mark a card failed instead of throwing.
  - Report the validation error details.
  - `SendPayConnect` (858–866): read "Program Version" for the charge's clinic, not `Clinics.ClinicNum`.
- **`OpenDentBusiness/Db/Enumerations.cs`** `OAuthApplicationNames.PayConnect2/PayConnect2Dll` (1455–1458): keep the values, since later members' numbers depend on them, and mark them obsolete in the doc comment.
- **`OpenDentBusiness/WebServices/WebServiceMainHQMockDemo.cs`** 341–343: remove the PayConnect cases.
- **`OpenDentBusiness/Misc/Introspection.cs`** `PayConnect2DLLKey` (210): keep the entity, since existing `IntrospectionItems` JSON may name it. It is unused.
- **`OpenDentBusiness/Data Interface/ProgramProperties.cs`** `InsertForClinic` (157–): optionally skip `API Key` when copying the ClinicNum-0 rows.

### 4.4 "Not available" stubs that are replaced or retired

| Stub | Today | After |
|---|---|---|
| `WebServiceMainHQNoHq.BuildOAuthUrl(…,"PayConnect2")` via `GetPayConnect2ApiKey` | throws, so all v2 calls fail | no longer called; the key comes from `programproperty` |
| `BuildOAuthUrl(…,"PayConnect2")` via `GetPayConnect2DLLKey` | unreachable | method deleted |
| VeriFone driver "choose the zip from the vendor" (`FormPayConnectSetup.butDownloadDriver_Click` 391–434) | runs `Setup.exe` from a user-picked zip | kept for 2.0 (neutral label) until DentalXChange confirms that no 2.0 terminal needs a driver; then deleted with 1.0 (4.2, 4.5) |
| Payment Portal Payments group (`checkPatientPortalPayEnabled`, token) | drives Open Dental's hosted patient portal | hide in the Phase 3 eServices removal. Not part of this change, but don't extend it. |

### 4.5 Prebuilt binaries: Bridges.dll and DpsPos.dll

- **Bridges.dll:** PayConnect uses **no** Bridges.dll type, so nothing needs re-implementing. The only coupling is the stray `using Bridges;` in `PayConnect2.cs` (4.1).
- **DpsPos.dll** (EDI Health Group, proprietary, flagged in `docs/third-party-binaries-audit.md`) is the **PayConnect 1.0 VeriFone terminal** module. **Recommendation: remove PayConnect 1.0, with VeriFone and DpsPos, in a follow-up change (size M):**
  - DentalXChange has decommissioned 1.0, per Open Dental's public manual.
  - DentalXChange publishes no DpsPos documentation.
  - Its binary has no public redistribution terms.
  - The PayConnect 2 code doesn't call it: v2 terminal sales go through the cloud API.

  What the removal touches:
  - `OpenDental/Forms/FormPayConnect.cs` (DpsPos config write 63–68, `PosRequest` 555–585, the whole v1 window)
  - `FormPayment.cs` 3376–3379 (v1 void through DpsPos) and the v1 branches at ~1240–1253 and ~3705–3718
  - `WpfControlsOD/Bridges/PayConnectTerminal.cs` `ToPayConnectResponse(PosResponse)` (93–119). Keep `BuildReceiptString`, which v2 uses through `FormPayment`.
  - the `DpsPos` references in `OpenDental.csproj` (125–127), `WpfControlsOD.csproj` (49–50) and `UnitTests.csproj` (106); `Resources/DpsPos.dll.config` (csproj 10429, `Resources.Designer.cs` 705); `OpenDental/Logic/PrefL.cs` 356; and `log4net`, which only DpsPos needs
  - the v1 SOAP `PayConnectService` web reference and `PayConnectREST`. Both carry Open Dental's compiled-in client identity (1.2).
  - Some v1 types (`PayConnectService.transType`, `PayConnectResponse`) are reused by v2 code for receipts and transaction types. Move them to a small in-repo enum and DTO first.
  - The `ODCloudClient` PayConnect terminal relay (`CodeBase/Utilities/ODCloudClient.cs` 384, 1152, 1260) goes with the AppStream removal.

  If the owner keeps 1.0 for a while, the audit's fallback applies: an optional plugin assembly that binds at runtime to a `DpsPos.dll` the office installs from DentalXChange, with the VeriFone driver also from DentalXChange. That path still uses Open Dental's 1.0 client identity, so it can't be shipped as is.

## 5. Credentials the office must register

| # | Step | Where | Notes |
|---|---|---|---|
| 1 | Open (or confirm) a **PayConnect merchant account** for each location that takes cards. If wanted, opt into surcharging where state law allows. | DentalXChange Merchant Services, 800.576.6412 ext. 472; <https://payconnect.dentalxchange.com/provider/payconnect/MerchantProcessing> | Underwriting and pricing are between the office and DentalXChange. An office migrating from Open Dental 2.0 already has this. |
| 2 | Complete **PCI onboarding** (questionnaire, possibly a network scan) | Instructions arrive after sign-up (FAQ: <https://dentalxchange.com/resources/faq>) | The merchant's own obligation |
| 3 | Get the **merchant Secret** for each merchant account | PayConnect support or the merchant portal | Entered per clinic in Setup. Open Dental offices already have it in `programproperty` "API Secret". |
| 4 | **Register as an integrator** on the developer portal. Company = the practice (or the project's legal entity). Describe the use: direct integration of the practice's own PMS with PayConnect. | <https://developer.dentalxchange.com/register/> | Self-service form. Approval is DentalXChange's call. |
| 5 | **Request sandbox access** from the portal menu, and retrieve the sandbox API key. Ask PayConnect for a sandbox merchant Secret and test card data. | <https://developer.dentalxchange.com/sandbox> | Used for the test plan in 6 |
| 6 | **Request a production API key.** Ask in writing: (a) whether a single practice may hold its own integrator key; (b) any agreement, fee or certification; (c) whether the key is per integrator or per merchant; (d) rate limits and terminal timeouts; (e) whether a BAA is needed when no `patient` object is sent but `cardHolder` carries the patient's name (2.6); (f) how realistic the sandbox is, and whether test cards and a test Secret exist. | `payconnect@dentalxchange.com` (API support contact in the OpenAPI file); partnering line 800.576.6412 ext. 459 (<https://payconnect.dentalxchange.com/partner/Partners>) | Keep the answers with the office's vendor records. The integration stays disabled until the key is issued. |
| 7 | **Register terminals** with DentalXChange and note each serial (Terminal ID). Confirm the model is supported for API terminal transactions. | PayConnect support | The public model lists Clover and Ingenico device types. Not needed for card-not-present use. |
| 8 | **BAA**: sign one if DentalXChange advises it (step 6e). | DentalXChange | No `patient` object is sent, but the `cardHolder` field carries the patient's name for token charges (2.6), plus the card ZIP |
| 9 | In the program: Setup, Program Links, PayConnect → Version 2.0 → API key, Use sandbox (off for production), Secret for each clinic, payment type, Test connection, terminals, Enabled → Save | local | Only a user with ManageHighSecurityProgProperties can edit the keys |

Interim state until step 6 is done: the window can be filled in and tested against the sandbox, but production stays **disabled** (`program.Enabled` off). Nothing else in the program changes.

## 6. Test plan

### 6.1 Sandbox setup

- Dev database: `tools/New-DevDatabase.ps1 -Name opendental_pc2 -Force` (local MariaDB, sample data).
- Developer-portal sandbox API key, plus the sandbox Secret and test cards from step 5.
- Watch traffic with a local proxy (Fiddler or mitmproxy, trusting its root certificate on the dev machine). Use it to confirm hosts and headers, and that nothing leaves except to `*.dentalxchange.com`.

### 6.2 Manual checks (sandbox, `Use sandbox` on)

The sandbox is a schema-validating simulator with "a limited view" (2.1). Steps that need realistic behaviour may only be partly testable there: 6, 7, 10 and 13 (hosted form, declines, session expiry, list contents), and anything involving a terminal. Note any step the sandbox can't exercise, and run it in the 6.3 pilot instead.

1. **Setup, empty.** Open the PayConnect window on a fresh database. Nothing is written until Save.
2. **Setup, saved.** Save with the key and Secret. `programproperty` now has `API Key` (ClinicNum 0) and `PayConnect2 Use Sandbox` rows, and both secrets start with `osd1$`. One security-log entry per changed high-security row.
3. **Test connection:** with a good key, the merchant details appear. A blank key gives the "API key not accepted" message, and a wrong Secret the Secret message. No crash on a string `error` body.
4. **Upgrade path:** put a plain-text Secret in the row (as Open Dental left it). Charges still work. The next Save encrypts it.
5. **Clinics.** Turn clinics on with two clinics and different Secrets. Each clinic's charge goes out with its own Secret. A clinic-restricted user can't edit the API key.
6. **Add card (Tokenizer).** In Credit Card Manage, add a card with a sandbox test card. Check that the token, masked number and expiry are saved, and that the stored token is not the card number (7.2).
7. **Hosted payment.** Payment window → PayConnect → web service, no saved card. Pay in the hosted form. A payment row is created with the reference number, the receipt prints, and optionally the card is saved.
8. **Token charges:** sale and authorize on a saved token; surcharge account, if the sandbox merchant supports it (the receipt shows the fee); partial refund; full refund; void; signature upload.
9. **Recurring charges:** run on two token cards, one valid and one declined. A third card belongs to a clinic with no Secret: it is marked failed, and the batch completes.
10. **Session expired:** leave the hosted form open past its lifetime. `SESSION_EXPIRED` is handled and Reload works.
11. **Origin check:** use a test hook to navigate the WebView2 to a non-DentalXChange page that posts a fake `SUCCESS`. It is ignored. Also have the card-entry window receive a fake `SUCCESS` from a non-DentalXChange child frame; it is ignored too (3.6).
12. **Offline.** Unplug the network or block `api.dentalxchange.com` in the firewall, then try a sale. You get "Could not reach PayConnect. No charge was made." The rest of the program keeps working.
13. **Lost response.** With the proxy, drop the response to `POST /transactions` after forwarding the request. Reconciliation finds the transaction by `invoiceNumber` and posts the payment once. No second charge is sent.
14. **No Open Dental traffic.** With the proxy on, nothing goes to Open Dental hosts during steps 1–13. `tools/Test-NoHqHosts.ps1` returns 0.

### 6.3 Production pilot (one workstation)

- Small sale on a staff card, then void it the same day.
- Small sale, then refund it after the batch settles (next day).
- One terminal sale, if a terminal is registered (the sandbox may not simulate terminals; 7.3), then void it.
- Check each against the merchant portal's transaction list.

### 6.4 Automated tests (no network)

New `tests/PayConnectTests` project (same style as `tests/CryptoTests`), plus the existing `UnitTests/UnitTests/PayConnect2Tests.cs`. Cover:
- base URL selection: production, sandbox and Introspection
- header building, and that headers never appear in exception text
- key resolution order and the missing-key exception
- status query strings (`referenceId`, `transactionId`, `orderId`, combinations)
- parsing a 401 string `error`, a 400 validation array and a RESPONSE object
- `EmbedSessionRequest` not serializing `amount` when null
- reconciliation matching on `invoiceNumber` across pages
- origin-check helper accepting `*.dentalxchange.com` and rejecting look-alikes (`dentalxchange.com.evil.test`)
- `AddMissingProperties` being idempotent

Use the existing `PayConnect2.Mock` hook, or a fake `HttpMessageHandler`.

## 7. Size, risks and open questions

### 7.1 Size

**M overall, about 4 to 6 developer days for PayConnect 2:**

| Work | Size |
|---|---|
| key storage and base URL (4.1) | S |
| HTTP helper, error mapping and reconciliation | M |
| setup window (4.2) | S |
| WebView2 hardening and session-expired handling | S |
| recurring-charge fixes | S |
| tests | S |

Not counted:
- DentalXChange's lead time for a production key, which could be weeks
- PayConnect 1.0, VeriFone and DpsPos removal (4.5), another M

### 7.2 Risks

1. **Production key refusal or terms.**
   - DentalXChange may refuse to issue a production `API-Key` to a practice or an open-source project, or attach an agreement or certification to it. Mitigation: ask first (step 6), and keep the integration disabled meanwhile.
   - If one project-wide key would serve several offices, that may break DentalXChange's terms, and a revocation would hit them all. Each office gets its own key, entered locally and never committed.
2. **Double charges after a timeout.** The API has no idempotency key. Mitigation: no automatic POST retries, the correlation `invoiceNumber`, and list-based reconciliation (3.4). Terminal timeouts are undocumented, so 190 s is a guess to confirm.
3. **Schema drift.** The public 2.12 file differs from the 24.3 code models:
   - void and refund by invoice number are gone
   - `orderId`, `documentNumber` and `surcharge` are new
   - many schemas set `additionalProperties: false`, so an unknown field we send could be rejected

   Run every request in the sandbox before production.
4. **Possible card-number storage.** When the tokenizer returns a value in track-2 form (`;…=…?`), `FormPayConnect2iFrame.GetTransactionResult` (~115–120) extracts the account number. `FormCreditCardManage` then stores that value as `PayConnectToken`.
   - If it is a real card number rather than a token, that is stored card data.
   - Check in the sandbox with a swiped test card. If it is a card number, refuse to save it and ask the user to key the card instead.
5. **Secret obfuscation, not protection** (3.2). Anyone with a database copy and this source can recover the keys. Restrict database access, and rotate the keys if a copy leaks.
6. **Spoofed hosted-form results.** Today the injected relay forwards any `message` event from any origin, and the C# handler doesn't check where it came from. The `event.origin` filter (preferably in a local wrapper page) and the navigation checks (3.6) close that; checking `args.Source` alone would not.
7. **Losing VeriFone.** Offices still on 1.0 and VeriFone lose terminal processing once 1.0 is removed. DentalXChange has already decommissioned 1.0, so the migration source (Open Dental 25.3) is most likely on 2.0, but this is unverified. Confirm the office's actual version in `fox-db` (`programproperty` "Program Version") before removing.
8. **Embed-session host unknown.** If the session URL points outside `*.dentalxchange.com`, the origin check must allow that host. Make the allowed suffix list a constant and confirm it in the sandbox.
9. **Debug builds hit a non-public host** until the `ODBuild.IsDebug()` override is removed (4.1).

### 7.3 Open questions (for DentalXChange, step 6)

1. Will DentalXChange issue a production integrator key to a single practice using its own PMS? Under what agreement or fee?
2. Is the `API-Key` per integrator (shared across that integrator's merchants) or per merchant? Do the sandbox and production keys differ?
3. What are the rate limits? How long can `POST /terminals/transactions` block? How long does an embed session live?
4. Which terminal models work through `/terminals/transactions`? Does the sandbox simulate a terminal? Does any 2.0 terminal need a local driver (Open Dental's manual still mentions a driver step)?
5. Which hosts serve the embedded payment page, so the origin check can allow them?
6. Is a BAA needed when no `patient` object is sent, but `cardHolder` carries the patient's name and the card ZIP?
7. What did the Open Dental "DLL key" unlock? Asked only to confirm that nothing in 2.0 still needs it.
8. When does the batch settle, which is the void cut-off before refunds become necessary?
9. How realistic is the Payment API sandbox: hosted-form sessions, declines, transaction lists, terminals? Are there test cards and a test merchant Secret?

## Fact-check log (2026-10-18)

An adversarial review checked this spec against the OpenAPI file (2.12.0), the developer-portal pages (Sandbox, Embedded Payment, Register), the DentalXChange partner, merchant, FAQ and user-agreement pages, Open Dental's public PayConnect manual page, and the `main` source. Every cited URL returned 200.

**Confirmed:**
- Two static header keys (`API-Key`, `Secret`), no OAuth
- Production base `https://api.dentalxchange.com`, every path under `/payments/`, sandbox by a `/sandbox` prefix
- Every endpoint, field and constraint listed in 2.4, including list paging and its 30-day window, void and refund taking only `referenceId`, the embed-session fields, and no 429 or idempotency header
- Phone extensions 459 and 472, and the contact email
- Every code touchpoint and line range spot-checked. Only the `GetApiSecretForClinic` callers named in 4.1 call it. `GetPayConnect2DLLKey` and the invoice-number void and refund have no callers. `staging-api` exists only in the Debug override.
- **Bridges.dll:** `PayConnect2.cs` imports the namespace and uses no Bridges type; the same holds for the `com.dentalxchange.webservices` and `System.Windows.Controls` imports.
- **Spec hygiene:** no credential values are reproduced. The only Open Dental host named is the public manual, cited as a source and never contacted by the app.

**Corrected:**
1. **Button visibility (1.1, 4.2).** With clinics off, the PayConnect button shows whenever the program is enabled, with no Secret check. `FormClaimPayEdit` has a second button with the same logic. Both now go through `IsConfigured`.
2. **Driver button (Summary, 4.2, 4.4).** "Download Driver" follows "Enable terminal processing" for **both** versions, and Open Dental's manual still lists a driver step under 2.0. It is no longer hidden for 2.0 until DentalXChange confirms.
3. **WebView2 origin check (3.6, 6.2, 7.2).** The injected relay runs inside the vendor page and forwards messages from every origin. `args.Source` is the top-level URL, so it can't validate the sender. The spec now filters on `event.origin`, preferably in a local wrapper page that keeps our script out of the card-entry page.
4. **PHI (2.6, 5, 7.3).** Token charges send the *patient's* name as `cardHolder`. The "cardholder name only" claim and the BAA question were reworded to match.
5. **Sandbox (2.1, 6.2, 7.3).** It is a schema-validating simulator with "a limited view". The two vendor sources disagree on whether its keys match production. A caveat was added to the test plan, plus open question 9.
6. **Errors (2.3).** The published secret-error sample says "Invalid API key". Map 401s by body shape, not text.
7. **Signature by `transactionId` (2.4, 4.1).** It is still valid in 2.12. Its deletion is now optional, and the integer/string type mismatch is noted.
8. **Embed `surcharge` (2.4).** It defaults to true when omitted.
9. **Session lifetime (3.3).** Single use is not documented. The text was softened, and a sandbox-mode labelling rule was added.
10. **`InsertForClinic` (3.2).** It drops the `IsMasked`/`IsHighSecurity` flags on copied rows.
11. **Reconciliation dates (3.4).** The query now covers yesterday through today, because the time zone is undocumented.
12. **Smaller fixes.** `PayConnectREST` lives in `PayConnect.cs`. Surcharge opt-in is not in the public docs. DpsPos has no *public* redistribution terms. The migration source's PayConnect version is unverified.

**Feasibility: public-api, confirmed.** The reference, the OpenAPI file and sandbox sign-up are public and self-service. The gate is the vendor's issuing of a production `API-Key`: approval, terms and per-office eligibility aren't published. Until that key is in hand, the integration stays configurable but disabled.

## References

- DentalXChange Payment API reference: <https://developer.dentalxchange.com/payment-api>
- Payment API OpenAPI 3 file, version 2.12.0: <https://portal-assets.dentalxchange.com/docs/payment-api.yaml>
- Payment API PDF: <https://api.dentalxchange.com/docs/latest/payment-api.pdf>
- Postman collections: <https://www.postman.com/dentalxchange/workspace/xconnect-apis>
- Embedded Payment guide: <https://developer.dentalxchange.com/embedded-payment>
- Sandbox: <https://developer.dentalxchange.com/sandbox>
- Developer portal registration: <https://developer.dentalxchange.com/register/>
- XConnect APIs overview: <https://www.dentalxchange.com/product/xconnect-api>
- Partnering with DentalXChange: <https://payconnect.dentalxchange.com/partner/Partners>
- Vendor partner page: <https://payconnect.dentalxchange.com/partner/Vendors>
- Merchant Services: <https://payconnect.dentalxchange.com/provider/payconnect/MerchantProcessing>
- DentalXChange FAQ (merchant services, PCI): <https://dentalxchange.com/resources/faq>
- DentalXChange user agreement: <https://www.dentalxchange.com/user-agreement>
- Open Dental manual, PayConnect setup (1.0 decommissioned, Secret from PayConnect): <https://opendental.com/manual/payconnectsetup.html>
- In repo:
  - `D:/Dev/opendental-schema/hq-vendor-table.md`
  - `docs/decoupling-plan.md` (ground rules 5–7)
  - `docs/third-party-binaries-audit.md` (DpsPos)
  - `CDT/README.md` (osd1 threat model)
