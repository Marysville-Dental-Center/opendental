# CareCredit (Synchrony) patient financing: direct-to-vendor spec

Status: draft, 2026-10-18. Baseline: `main` at `6855026f5`. Vendor: Synchrony Bank, product CareCredit, delivered to practice software through Synchrony's Integrated Hosted Solution (IHS) and its Patient Data Service.

## Summary

- **What it is.** CareCredit is a healthcare credit card. The integration lets staff prescreen patients (QuickScreen, one patient or a nightly batch), start a credit application, look up a cardholder, take a purchase or refund, open CareCredit's reports and admin pages, and send a patient a self-service link. Synchrony hosts every one of those screens. Open Dental's job was to prefill them with patient data, open them, and pull the results back into the patient's account, patient fields and payments.
- **What HQ provided.** Open Dental held the Synchrony B2B client credentials and handed out OAuth access tokens (`GetCareCreditOAuthToken`). It also issued legacy web tokens, collected a transaction log from every office, and set the batch schedule. It identified itself to CareCredit with its own partner code (`1065`), received Synchrony's webhooks and relayed them to each office's eConnector. The eConnector and Open Dental Service, which are not in this repository, ran the batch QuickScreen and the pending-result polling.
- **What Synchrony publishes.** The developer portal (`developer.syf.com`) publicly describes the IHS solution, the Patient Data Service (prefill, pullback and self-service lookup), Batch QuickScreen and optional webhooks. It says plainly that auth is OAuth with B2B credentials. **The API reference, the IHS implementation guide, base URLs, field lists and sandbox are behind a portal login.** Portal registration is open to anyone, but Synchrony may verify the developer before issuing keys. Until Synchrony approves the app under the Synchrony Developer Agreement, only Sandbox use with anonymized mock data is allowed.
- **Design.** We build an in-repo client (`OpenDentBusiness/Bridges/CareCredit/`) with the same public surface this repo calls on the prebuilt `Bridges.dll`.
  - It uses the OAuth client-credentials grant from the desktop. No browser sign-in is needed.
  - Credentials and tokens live in the `CareCredit` program's `programproperty` rows, with secrets encrypted in `osd1$`.
  - Polling replaces webhooks.
  - Batch QuickScreen and result polling run in a local job, on the fork's background service host when it exists, and until then on one workstation the office picks.
  - The client never contacts an Open Dental host. It refuses Open Dental's partner code.
- **Until the office has partner credentials and the gated docs,** the code ships configurable but disabled. The setup screen can store credentials, but **Enabled** stays locked until **Test connection** succeeds against the configured host.
- **Feasibility: partner-only** (confirmed in the 2026-10-18 fact-check). The overview pages are public. The API reference, the implementation guide, production approval and the partner code are not. Size: **L** (about 15 to 20 dev-days once the gated docs are in hand). That excludes the wait for Synchrony's approval and the shared local job host.
- **Note on the decoupling plan.** `docs/decoupling-plan.md` (Bridges row) still says "Cut CareCredit", and `features-25.2.md`, `features-25.3.md` and `port-backlog.md` exclude CareCredit items on that basis. This spec follows the owner's newer decision to re-implement it. Section 7 includes a go/no-go gate: if Synchrony won't grant this office (or the project) integration credentials, fall back to removal.

## 1. What the integration does, and what HQ provided

### 1.1 What the office uses it for

Every user-facing action opens a page that Synchrony hosts. Open Dental first sends a prefill request with the patient's data and gets back a URL that carries a session ID. Afterwards it pulls the result back by reference ID. The landing-page codes below are the values of the prebuilt enum `CareCreditLandingPage` as this repo passes them. The `EnumCareCreditPrefillServiceType` codes refine the HM (home) page.

| Feature | Where in the UI | Landing page / service type | Result back in the app | Code |
|---|---|---|---|---|
| Individual QuickScreen (accept or decline a pre-approval) | Appointment right-click menu "CareCredit Accept/Decline Offer", shown when the patient field says Pre-Approved; FormCareCredit **Apply** when pre-approved | `QS`, with the latest batch `RefNumber` as `oldRefId` | Status and amounts written to patient fields | `ControlAppt.cs` ~577–613, 2291–2296; `FormCareCredit.butApply_Click`; `CareCreditL.LaunchQuickScreenIndividualPage` |
| Credit application | Appointment menu "CareCredit Application Needed" (status Declined or Account Not Found); FormCareCredit **Apply** otherwise | `AP` | Application outcome via pullback | `CareCreditL.LaunchCreditApplicationPage` |
| Cardholder lookup | FormCareCredit **Lookup** | `LU`, service type `L` | Available credit | `CareCreditL.LaunchLookupPage` |
| Purchase | Payment window **CareCredit** button: saves the payment, opens the purchase page in WebView2, pulls back on close | `PR`, with `PayNum` as invoice number | Payment marked card-completed, note and merchant fee filled in, `PaymentSource=CareCredit` | `FormPayment.butCareCredit_Click` ~420–513, `IsCareCreditTransStatusCompleted` ~2583; `CareCreditL.GetPurchasePageUrl` |
| Refund | Payment window **Return** on a CareCredit payment; Transactions window **Process Refund** | `RF`, original `RefNumber` and merchant number | Refund recorded via pullback | `CareCreditL.RefundTransaction`, `FormPayment.butReturn_Click` ~697 |
| Self-service link | Account module menu "Get CareCredit Self-Service Link" (shown only when the program is enabled) | `PR` with the amount staff enter. The repo only opens the hosted purchase page on the staff PC (`doShowPage` defaults to true); sending the link to the patient is presumably done from that hosted page, which the public solution page describes as an option | Purchase completed later on the patient's device; needs polling | `ControlAccount.menuItemSendCareCreditCSS_Click` ~1272, `CareCreditL.LaunchPurchasePage` ~209 |
| Reports | FormCareCredit **Report** | `HM`, service type `R` | None (Synchrony page only) | `CareCreditL.LaunchReportsPage` |
| Admin (merchant's promotional financing terms) | FormCareCredit and Setup **Manage/Promotions** (Setup permission) | `HM`, service type `A` | None | `CareCreditL.LaunchAdminPage` |
| Batch QuickScreen | Background, daily | Batch request per eligible patient | Patient fields (status, pre-approval amount, available credit), appointment icons, alerts, Transactions window | Off-repo runtime (see 1.3); data methods in `CareCreditWebResponses.cs` |
| Transactions window | Tools > CareCredit Transactions; alert link | n/a | Lists purchases and refunds, batch results and errors; reprocesses expired batches | `FormCareCreditTransactions.cs` |
| Merchant-fee reporting | Daily Payments report "Show CareCredit fees" | n/a | Reads `payment.MerchantFee`, which holds the fee as a positive number; the report shows it negated in the fee column and subtracts it from the amount | `OpenDental/Forms Reports/FormRpPaySheet.cs`, `AddCareCreditFeeColumn` ~182–192 |

Behaviour from Open Dental's public manual ([CareCredit Setup](https://www.opendental.com/site/carecreditsetup.html), [CareCredit](https://www.opendental.com/site/carecredit.html), [CareCredit Transactions](https://www.opendental.com/site/carecredittransactions.html)), confirmed against this repo where possible:

- Batch QuickScreen runs each morning at 7:20 local time.
  - It screens patients with an appointment within the next 2, 3 or 4 days, as chosen in Setup (`CareCreditQSBatchDays`, default 2). In Lookup Only mode the same setting drives the lookups.
  - The amount sent is the appointment's estimated fee, rounded to the nearest dollar.
  - With **Merchant number by provider** on, the appointment's provider must have a merchant number, or the patient isn't screened.
  - The mode is set by `QSBatchEnabled`:
    - **On (with Bal)** screens only patients with an estimated patient portion over $1.
    - **On (no Bal)** screens everyone with an upcoming appointment.
    - **Lookup Only** only checks cardholder status and available credit, and needs the last four digits of the SSN.
  - Patients with a completed CareCredit purchase, refund or QuickScreen in the last 60 days are skipped (`GetPatNumsForDateRangeToExclude`, which also skips patients whose recent batch error was invalid input or a closed merchant).
  - Minors are screened through their guarantor, and the result is copied to the minors (the private `UpsertPatFieldForMinors`, reached through `AddPatFieldsForMinors` or `UpdateCareCreditPatField(isBatch:true)`). A minor with no family guarantor isn't screened and gets a `BatchError` row.
  - The required demographics are first and last name, street, city, state, ZIP and phone.
  - A batch with no response stays Pending and expires after 24 hours (`ExpiredBatch`). The Transactions window can set it back to Pending.
- A closed CareCredit account is marked by Open Dental putting an `x` in front of the merchant number the next time the batch runs. The app then blocks transactions. `IsMerchantNumClosed` is called in FormCareCreditSetup, FormCareCredit, FormProvAdditional and `CareCreditWebResponses`. The payment window has no direct call; its warning presumably comes from `GetMerchantNumber` throwing inside the prebuilt bridge.
- Merchant numbers are 16 digits. There is one per practice or clinic (`CareCreditMerchantNumber` programproperty, per ClinicNum), or one per provider and clinic when **Merchant number by provider** is on (`providerclinic.CareCreditMerchantId`).
- Since 2020-07-01, a California law bars California providers from submitting CareCredit applications for patients, whether through CareCredit's provider site, integrated practice software or a terminal. Patients apply themselves (CareCredit's own link, website or phone), and can still pay through the integration ([Open Dental, CareCredit](https://www.opendental.com/site/carecredit.html)). The repo has no California check today.

### 1.2 Data the integration already owns (keep, no schema change)

- `program` row `ProgName='CareCredit'` (disabled by default since `ConvertDatabases6`).
- `programproperty` rows:
  - Clinic-scoped: `CareCreditMerchantNumber`, `QSBatchEnabled`, `CareCreditPaymentType`, `CareCreditQSBatchDays`.
  - ClinicNum 0: `CareCreditOAuthToken`, `CareCreditPatField`, `CareCreditPatFieldPreApprovalAmt`, `CareCreditPatFieldAvailableCredit`, `CareCreditIsMerchantNumberByProv`, `Disable Advertising`, and `CareCreditPartnerCode` (seeded with `1065`, Open Dental's code, by `ConvertDatabases8.To23_2_28` and again by `To23_3_2`, lines 849 and 1003).
  - The constants live in `ProgramProperties.PropertyDescs.CareCredit` (`OpenDentBusiness/Data Interface/ProgramProperties.cs` ~642). `CareCreditQSBatchEnabled` maps to the row name `QSBatchEnabled`, and `CareCreditDoDisableAdvertising` to `Disable Advertising`.
- `toolbutitem` rows for the Account module (ToolBar 0) and Treatment Plan module (ToolBar 5), seeded by `ConvertDatabases6` ~4024–4026.
- `providerclinic.CareCreditMerchantId`.
- `carecreditwebresponse`:
  - Status and timestamps.
  - `RefNumber` (Synchrony reference ID).
  - `WebToken` (now holds the prefill session ID).
  - `LastResponseStr` (raw vendor JSON or error).
  - `ServiceType` Batch/Prefill/IndividualQS, `TransType` None/Purchase/Refund, `MerchantNumber`.
  - `HasLogged` (HQ upload flag).
- `patfielddef` types 6, 7 and 8 (`CareCreditStatus`, `CareCreditPreApprovalAmt`, `CareCreditAvailableCredit`).
- Prefs `CareCreditBatchProcessDateTime` and `CareCreditBatchPullbackDateTime` (added in 24.2.48, documented as "Updated by HQ").
- `AlertType.CareCreditBatchError`, `FormType.FormCareCreditTransactions`, `CreditCardSource.CareCredit` (10), `LogSources.CareCredit` (18).
- Introspection test overrides `CareCreditApiURL` (non-IHS endpoints) and `CareCreditIHSApiURL` (IHS endpoint). These show the prebuilt bridge talked to two Synchrony base URLs.

### 1.3 Exactly what Open Dental's HQ provided

Sources: `D:/Dev/opendental-schema/hq-vendor-table.md` (CareCredit row) and the code cited.

| HQ piece | What it did | Evidence in this repo | Direct replacement |
|---|---|---|---|
| `GetCareCreditOAuthToken` | Exchanged Open Dental's own Synchrony B2B client credentials for an access token and returned it to the office. The office never held a client secret. | `IWebServiceMainHQ.cs:81`; NoHq stub `WebServiceMainHQNoHq.cs:70`; token cached in programproperty `CareCreditOAuthToken` (added in `FormCareCreditSetup.AddNeededProgramProperties`) | Office's own client ID and secret, client-credentials grant from the desktop (3.2) |
| `GetCareCreditWebToken` | Issued the legacy "web token" used for pullback before session IDs existed | `IWebServiceMainHQ.cs:97`; `CareCreditWebResponse.WebToken` doc comment ("Old: The web token used for pullback request") | None needed: pullback uses the OAuth bearer token and the reference ID |
| `LogCareCreditTransaction` | Uploaded completed transactions (up to 50 at a time) to HQ, then set `HasLogged=1` | `CareCreditWebResponses.GetPendingForLogging`, `UpdateHasLoggedToTrue`; `WebServiceMainHQMockDemo.LogCareCreditTransactionsDelegate` | **Dropped** (telemetry to Open Dental). `HasLogged` is left alone. |
| `GetCareCreditBatchTimes` | HQ set the time of day for batch processing and for batch pullback | `WebServiceMainHQProxy.GetCareCreditBatchProcessTime` / `GetCareCreditBatchPullbackTime` (now always `DateTime.MinValue`); prefs above | Local schedule settings in the same prefs (3.6) |
| EServiceSetup `SerializedProperties` | Sent each clinic's CareCredit merchant IDs (with any `x` stripped) to HQ during eService signup. HQ could then match Synchrony webhooks, which are keyed by merchant, to the right office. | `WebServiceMainHQProxy.GetSerializedProgramProperties` ~96–131, `CareCreditWebResponses.MERCHANT_ID_PROG_PROPERTY` | **Removed.** No merchant data leaves the office except in calls to Synchrony. |
| Webhook relay | HQ ran the public listener for Synchrony's webhooks and relayed them to the office's eConnector. The eConnector then pulled back and wrote `carecreditwebresponse`. | hq-vendor-table row; Synchrony documents webhooks as an optional add-on (2.4) | Polling (3.5). No inbound listener (rule 6). |
| Partner identity | Open Dental's CareCredit partner code `1065`. The prebuilt bridge presumably sent it with prefill requests (inferred: the repo only stores it). | `ConvertDatabases8.cs:849` and `:1003`, whose comment says the value replaced one hard-coded in the bridge | The office's (or project's) own partner code. `1065` is refused (3.9). |
| Off-repo runtime | The eConnector and Open Dental Service ran Batch QuickScreen, polled pending prefill rows, created payments for transactions finished on the patient's device, and raised alerts. The manual says both are required. | Methods with **no caller in this repo**: `CareCreditWebResponses.GetAllPending`, `SetAllExpiredBatchesToPending`, `GetPatNumsForDateRangeToExclude`, `HandleResponseError`, `AddPatFieldsForMinors`, `UpdateCareCreditPatField(isBatch:true)`, `UpsertPreApprovalAmt`, `UpsertAvailableCredit`, `Payments.InsertFromCareCredit`; `AlertType.CareCreditBatchError` is never raised in-repo | Local job (3.6, 3.7) |

What happens on `main` today: the prebuilt bridge can't get a token, because the NoHq stub throws `HqNotAvailableException.ForVendor("CareCredit")` ("CareCredit is not available until direct vendor integration is configured"), and no batch job runs at all. The setup screen still opens. The Transactions window (Tools menu, shown only while the program is enabled, `FormOpenDental.cs` ~1849) still lists historical rows migrated from Open Dental.

## 2. The vendor's public API

### 2.1 Documentation status

Synchrony runs a developer portal at `https://developer.syf.com/`. Its product pages are public. Everything below the overview level needs a portal login, and the portal registration page itself is open to the public:

- Public:
  - [product catalog](https://developer.syf.com/our-products)
  - [Provider Apply & Buy](https://developer.syf.com/our-products/provider-apply-and-buy) (the PMS solution)
  - [Integrated Hosted Solution (IHS)](https://developer.syf.com/our-products/integrated-hosted-solution)
  - [Quickscreen Apply](https://developer.syf.com/our-products/quickscreen-apply)
  - [FAQ](https://developer.syf.com/faq)
  - [Synchrony Developer Agreement](https://developer.syf.com/terms-of-use)
  - [portal home page](https://developer.syf.com/) (quick start and certificate notice)
- Login-gated (each redirects to `/user/login`):
  - the Patient Data Service API reference (`https://developer.syf.com/api-reference/patient-data-service-v1`)
  - the IHS implementation guide (`https://developer.syf.com/integration/integrated-hosted-solution-ihs-implementation-guide`): its sections on webhooks, Batch QuickScreen, the Admin (promotions) module and the Reporting module
  - the `/docs` page
  - the Knowledge Hub
- Synchrony's [Partner Center](https://www.synchrony.com/business/partner-center) offers integration guidelines and a Business Center login for existing partners. It is aimed at retail and e-commerce partners.

So the overall shape, the auth style and the feature list are public. Endpoint paths, hostnames, request and response schemas, status codes, token lifetime and rate limits are not, and this spec doesn't guess them. Every such value is a setting or a TODO marked "from the gated guide".

### 2.2 Base URLs

- **Production gateway:** the portal's certificate notice says clients and vendors that call Synchrony over the internet do so through `api.syf.com`. Callers must trust the DigiCert root and intermediate certificates, which the portal offers as a download bundle ([home page](https://developer.syf.com/)). The IHS page says the "SYF Secure API Gateway" fronts the Patient Data Service. `https://api.syf.com` is therefore the likely production API host. The exact token and Patient Data paths are not public.
- **IHS hosted pages:** these are returned by the prefill call as a URL that carries a session ID (the repo parses it with `CareCredit.GetSessionIDFromUrl`). The host isn't public. The prebuilt bridge kept a separate base URL for it (`CareCreditIHSApiURL`).
- **Sandbox:** the Developer Agreement defines a Sandbox that is reachable through the portal. Its hostnames aren't public.
- **Design consequence:** the code ships no default URLs. The office (or a later commit, once the gated guide is read and Synchrony confirms the hosts may be published) fills in `CareCreditApiBaseUrl` and `CareCreditIhsBaseUrl` for each environment. The Introspection overrides keep working for tests.

### 2.3 Auth flow (as publicly described)

1. Synchrony issues **B2B credentials** to a registered app. The Developer Agreement calls them Access Keys. That they are a client ID and secret pair is our assumption from the OAuth wording; the public pages don't say. The FAQ says to register, then request an App on the **My Apps** page; Synchrony may verify the developer before issuing keys (Developer Agreement). Keys rotate according to a rotation preference the developer selects, and an earlier rotation is requested through support or the relationship manager ([FAQ](https://developer.syf.com/faq)).
2. The application calls a **REST token endpoint** with those credentials and receives an **OAuth access token**. The IHS page says the application "uses existing B2B credentials" for this ([IHS](https://developer.syf.com/our-products/integrated-hosted-solution)). This is machine-to-machine, which is the OAuth 2.0 client-credentials pattern. There's no user consent step, no redirect URI and no refresh token. The grant type string, the credential placement (HTTP Basic or form body), any scopes and the token lifetime all come from the gated guide.
3. Every Patient Data Service call carries the access token in an HTTP request header. The gateway validates it and forwards the call to the IHS back end.
4. The public IHS page says the gateway answers a prefill with a Reference ID. That the answer also carries a hosted-page URL with a session ID comes from this repo (`SendPrefillReturnPageUrl` returns a URL, and `GetSessionIDFromUrl` parses it), not from the public page. The IHS feature list promises "behind the scenes authentication" with a single logon to the PMS, meaning staff reach CareCredit's hosted pages without a separate login.

### 2.4 Services and fields we need

The services are named on the public pages. Field names below are this repo's names: what the prebuilt bridge received from callers, which tells us what the request must carry. Mapping them to Synchrony's JSON fields is a task for the gated reference.

| Service (public name) | Purpose | Inputs (from this repo's call sites) | Outputs we store |
|---|---|---|---|
| OAuth token | Bearer token | client ID, client secret | `access_token`, expiry |
| Patient Data: **prefill** | Push patient data into IHS for Application (`AP`), Purchase (`PR`), Refund (`RF`), Prequalification/QuickScreen (`QS`), Lookup (`LU`) or a home page (`HM`) with service type `A` (admin), `R` (reports), `L` (lookup) or `C` (default) | merchant number, partner code, landing page, service type, patient demographics (name, address, city, state, ZIP, phone, email, DOB; last four of SSN only where required), `purchaseAmt`, `refundAmt`, `estimatedFeeAmt`, `invoiceNumber` (PayNum), `noteMemo` (practice memo), `promoCode`, `userName` (staff user), `oldRefId` (prior batch or original transaction reference), clinic | Reference ID → `RefNumber`; hosted-page URL; session ID → `WebToken` |
| Patient Data: **pullback** | Get the outcome of what was done in IHS, for reconciliation | reference ID (and merchant number) | status → `ProcessingStatus`, transaction type → `TransType`, amount, promo and fee details (for `GetFormattedNote`, `GetMerchantFee`), raw JSON → `LastResponseStr` |
| Patient Data: **self-service lookup** (the IHS page says "Customer Self Service platform"; the catalog names the product Consumer Self Service, CSS) | Retrieve transactions the patient finished on the self-service platform, for reports or reconciliation | reference ID / merchant | same as pullback |
| **Batch QuickScreen** | Pre-approve consumers "in a bulk mode" | per patient: demographics, estimated fee (rounded), merchant number; lookup-only mode needs last four of SSN | pre-approval status, pre-approval amount, available credit (cardholders), error description, invalid-input flag, merchant-closed signal |
| **Webhook notifications** (optional) | Synchrony posts an event when an application, purchase or refund completes, so the partner can call pullback right away | partner-hosted HTTPS listener | not used (3.5) |

The [Provider Apply & Buy](https://developer.syf.com/our-products/provider-apply-and-buy) page lists the Patient Data Service as the only required API, and Webhook Notifications as the only optional add-on. It also lists Batch QuickScreen, promotion management (Admin module), the Reporting module and account lookup as features. It doesn't say whether those four need separate enablement; their guide sections are behind the login (open question 9). It describes the same three steps the old bridge used: submit consumer data, guide the transaction in the hosted page (staff submit it, or send the patient a link), then sync back with a pullback. The separate [Quickscreen Apply](https://developer.syf.com/our-products/quickscreen-apply) product describes the prescreen engine: name and address in, a soft credit check, a decision in real time. In the PMS case it's reached through IHS. The catalog also lists a **Consumer Self Service** platform that can be prefilled from IHS or directly, and a separate **Credit Authorizations** API (purchase, preauthorization, reversal, refund). That one isn't needed, because purchases and refunds go through the IHS hosted pages ([catalog](https://developer.syf.com/our-products)).

### 2.5 Rate limits

Not published. The Developer Agreement lets Synchrony "restrict, throttle, suspend" access at its discretion. The design is conservative:

- one token per office, shared through the database
- polling intervals that back off
- batch submissions sent one after another, with a small delay between them
- HTTP 429 and `Retry-After` honoured

The real limits come from the gated guide.

### 2.6 Terms that matter

Sources: [Synchrony Developer Agreement](https://developer.syf.com/terms-of-use) (effective 2018-07-16), the portal FAQ, CareCredit's provider pages and Open Dental's public manual. These are summarized here, not quoted.

- **Access Keys are Synchrony's property and must be kept confidential.** They may not be shared with third parties, except contractors working for the developer. **Consequence:** an open-source project can't ship one set of keys to many offices, or route offices through a shared server. Each office (or a single legal entity operating the install) must hold its own keys. That fits the owner's "our own credentials, configured locally" decision, and it is why nothing is compiled in.
- **Production needs Synchrony's approval of the app**, and approval again after any change that affects how the app interacts with the APIs. Synchrony may test the app and require additional terms before go-live. Before approval, only the Sandbox with anonymized mock data is allowed.
- **No endorsement claims.** We may not say the software is certified or endorsed by Synchrony. This matches rule 7.
- **No automated retrieval** of portal materials (no robots or scrapers). Developers read the gated docs by hand.
- **End-user terms.** Synchrony may require us to show its disclosures to end users, or to bind end users to minimum terms.
- **No redistribution of Materials.** The Agreement defines Materials broadly (APIs, specifications, technical documentation, portal content) and forbids distributing them or giving others access to them. It also forbids decompiling or reverse engineering them, and probing or scanning Synchrony's systems.
- **Improvements.** Synchrony owns any modifications or derivatives of its Materials. The developer keeps its own application. **Open question:** may endpoint paths and field names taken from the gated docs be published in a GPL repository? Given the redistribution clause, assume no until Synchrony says yes in writing, and keep them in settings rather than in source (3.3).
- **Partner status.** The Provider Apply & Buy page names two audiences: "3rd Party ISV Partners" and "Direct Integration Partners". The office should ask to be a Direct Integration Partner for its own install. We assume CareCredit's partner code (Open Dental's was `1065`) is issued as part of that relationship; nothing public says so.
- **Merchant enrollment** is separate from API access. The practice must be an enrolled CareCredit provider with a 16-digit merchant number ([CareCredit for providers](https://www.carecredit.com/providers/)).
- **PHI / BAA.** Prefill and batch requests carry patient identity, contact details, appointment-based fee estimates and sometimes DOB or part of the SSN. Nothing public says whether Synchrony signs a BAA or treats this as a payment disclosure. That's an open question for the office's compliance contact and CareCredit's merchant agreement.
- **Prescreening.** QuickScreen is a soft-check preapproval. Synchrony presents and discloses the offer on its hosted page. The office must only show offers through that page, and must not build its own offer text.
- **California.** Staff at California locations may not submit applications for patients. The solution page's step 2 allows a link for the patient to finish on their own device "where permitted". Use that path if the gated guide confirms it covers applications. Otherwise hide **Apply** and "Application Needed" for clinics (or a practice) whose state is CA, and show a neutral note that the patient must apply directly with CareCredit. Don't show CareCredit's marketing URLs (rule 7).

## 3. Desktop design

### 3.1 Shape

```
OpenDentBusiness/Bridges/CareCredit/
  CareCredit.cs                   static facade, same public surface as Bridges.dll (section 4.2)
  CareCreditSettings.cs           reads/writes the program's programproperty rows; encrypts secrets
  ICareCreditApiClient.cs         token, prefill, pullback, CSS lookup, batch submit/results
  CareCreditApiClient.cs          HttpClient implementation (System.Net.Http, Newtonsoft.Json)
  CareCreditApiClientMock.cs      for unit tests (Pearl pattern)
  CareCreditDtos.cs               request/response DTOs, CareCreditQSBatchResponse
  CareCreditEnums.cs              CareCreditLandingPage, EnumCareCreditPrefillServiceType, EnumCareCreditBatch
  CareCreditJobs.cs               batch QuickScreen and pending-result polling (host-agnostic)
```

The pattern is the in-repo Pearl client (`OpenDentBusiness/Bridges/Pearl/`): an interface, a static `Inst` that tests can swap out, and a lock around the token. The facade is called both by the desktop (interactive pages) and by the job host (3.6). Nothing here references WinForms. The two methods that open windows (`ShowPage`, and the purchase flow's `FormCareCreditWeb`) stay in `OpenDental`.

### 3.2 Auth: OAuth client credentials from the desktop

- **Flow:** client credentials. The token call is server-to-server, with no user in the loop, so there's nothing to open in a browser.
  - **PKCE / system-browser loopback:** not applicable. There's no authorization code and no user consent.
  - **Device code:** not described in the public material, and not needed.
  - **Staff sign-in to CareCredit's pages:** happens implicitly through the session URL that the prefill returns.
- **Steps:**
  1. Read the client ID, decrypted secret, environment and API base URL from settings.
  2. If the cached token (`CareCreditOAuthToken`, decrypted) has more than 5 minutes left (per `CareCreditOAuthTokenExpiresUtc`), use it.
  3. Otherwise POST to the token endpoint, under a process lock.
  4. Store the new token encrypted and the expiry in UTC, through `ProgramProperties` (`ClinicNum=0`), so all workstations and the job host share one token.
  5. Send the token in the request header that the gated reference names.
- **Refresh:** client-credentials tokens are simply requested again. There's no refresh token.
  - Refresh early (5-minute margin).
  - On HTTP 401, drop the cached token, fetch a new one once, and retry the call once.
  - A second 401 shows "CareCredit rejected the configured credentials" and records it on the setup screen (`CareCreditLastAuthError`).
  - Two workstations refreshing at once is harmless if Synchrony lets several tokens be valid at once; the last writer wins. If the gated guide says a new token revokes the old one, take a database lock for the refresh (a compare-and-set on `CareCreditOAuthTokenExpiresUtc`, as in 3.6), not just the process lock.
- **TLS:** `CodeBase/Utilities/ODInitialize.cs` already enables TLS 1.2. Windows' DigiCert roots are normally present. If not, the setup screen's **Test connection** reports a certificate-chain error and links nowhere; the office installs the bundle from the portal.

### 3.3 Where credentials and settings live

All settings are rows of the existing `CareCredit` program in `programproperty`. Missing rows are inserted at runtime by `CareCreditSettings.EnsureRows()`, which grows from today's `FormCareCreditSetup.AddNeededProgramProperties`. No schema change and no ConvertDatabases step.

| PropertyDesc | ClinicNum | Stored as | New? | Notes |
|---|---|---|---|---|
| `CareCreditClientId` | 0 | plain | new | Synchrony app client ID |
| `CareCreditClientSecret` | 0 | `osd1$` via `CDT.Class1.TryEncrypt` | new | Masked textbox. Never logged or shown again after saving. |
| `CareCreditEnvironment` | 0 | `Sandbox` / `Production` | new | Default `Sandbox` |
| `CareCreditApiBaseUrl` | 0 | plain | new | Gateway base URL for the selected environment. Empty by default. Must be `https`. |
| `CareCreditIhsBaseUrl` | 0 | plain | new | Hosted-page host. Used to allow WebView2 navigation and to check URLs that come back from prefill. |
| `CareCreditOAuthToken` | 0 | `osd1$` (new values) | existing | May hold Open Dental's last token after migration, in whatever form the prebuilt bridge wrote it. It is ignored and overwritten, because it was issued to Open Dental's app. |
| `CareCreditOAuthTokenExpiresUtc` | 0 | ISO-8601 | new | |
| `CareCreditPartnerCode` | 0 | plain | existing | Migrated value `1065` is treated as empty, and saving refuses it. |
| `CareCreditJobComputerName` | 0 | plain | new | Interim job host (3.7) |
| `CareCreditBatchLastRunDate` | 0 | `yyyy-MM-dd` | new | Last day the batch was claimed. Used only as the compare-and-set lock in 3.6, so the schedule prefs keep their time of day. |
| `CareCreditLastAuthError` | 0 | plain | new | Shown on the setup screen |
| `CareCreditMerchantNumber`, `QSBatchEnabled`, `CareCreditPaymentType`, `CareCreditQSBatchDays` | per clinic | plain | existing | Unchanged |
| `CareCreditPatField*`, `CareCreditIsMerchantNumberByProv` | 0 | plain | existing | Unchanged |
| `Disable Advertising` | 0 | plain | existing | Row kept for schema compatibility. UI removed (3.10). |

- **Schedule:** reuse prefs `CareCreditBatchProcessDateTime` (time of day to submit, default 07:20 local) and `CareCreditBatchPullbackDateTime` (time of day for the first batch pullback, default 07:50). Only the time-of-day part is used. Update the `Pref.cs` comments from "Updated by HQ" to "Set in CareCredit Setup".
- **Note on `osd1$`:** this is obfuscation with a public key (`CDT/README.md`). Anyone who can read the database can decrypt it. That's the fork's standard for bridge secrets and it's acceptable here, but the Synchrony secret should be scoped to this one office's app so a leak can be revoked.

### 3.4 Hosted pages

- `CareCredit.SendPrefillReturnPageUrl` makes the prefill call.
  - It inserts a `carecreditwebresponse` row: `ServiceType=Prefill`, or `IndividualQS` for `QS`; `ProcessingStatus=Created`, then `Pending`; `RefNumber`, `WebToken`=session ID, `MerchantNumber`, `PayNum`, `ClinicNum`, `Amount`, `TransType`.
  - It returns the page URL.
  - It refuses a URL whose host isn't `CareCreditIhsBaseUrl`'s host.
- **Purchase** keeps today's modal `FormCareCreditWeb` (WebView2), so the app can pull back the moment the window closes (`FormPayment.IsCareCreditTransStatusCompleted`).
- `ShowPage(url)` (every other page) opens a **non-modal** `FormCareCreditWeb`. If WebView2 is missing, it opens the default browser instead. Completion then comes from polling (3.5).
- In `FormCareCreditWeb`:
  - Nothing sets `IsUrlSingleUse` today, so the window shows a Refresh button. Set it to true if the gated guide confirms the session URL is single-use, and fix its comment, which names the wrong vendor (Bamboo). The class summary also wrongly calls the control Internet Explorer; it is WebView2.
  - Navigation outside the IHS host (for example a cardholder-agreement PDF) opens in the system browser.
  - Delete the `ODBuild.IsThinfinity()` branches.
- Delete the `ODCloudClient.IsAppStream` branches in `CareCreditL` (ODCloud is removed per the decoupling plan).

### 3.5 Results without webhooks: polling

A webhook needs a public HTTPS listener. That would mean hosting something reachable from the internet, which rule 6 and the office's security posture argue against, and Synchrony lists webhooks as optional. So every result comes from **pullback**:

| Case | When we pull back |
|---|---|
| Purchase in the modal window | When the window closes (existing code), then the poller |
| Any other prefill (`AP`, `QS`, `LU`, `RF`, self-service `PR`) | Poller: every 1 min for the first 15 min, every 10 min until 4 h, every 60 min until the expiry (default 72 h; set it from the gated guide's session lifetime). Then `Expired`. |
| Batch QuickScreen | From `CareCreditBatchPullbackDateTime`: every 15 min until the row resolves or 24 h pass, then `ExpiredBatch` (existing manual behaviour). |

`CareCreditJobs.PollPending()` uses `CareCreditWebResponses.GetAllPending()` and the row method `CareCreditWebResponse.GetLastPendingUpdateDateTime()`, the same shape as the PayConnect monitor in `OpenDentBusiness/WebBridges/PayConnect.cs` ~425–455. `CareCredit.UpdateWebResponse(row)` maps the vendor status onto `CareCreditWebStatus` and writes the row. Then:

- **Purchase or refund completed, `PayNum=0`** (the patient finished it on their own device): `Payments.InsertFromCareCredit(...)` creates the payment with `IsCcCompleted=true`, the CareCredit payment type and status `OnlinePending` (or `OnlineProcessed` when the `OnlinePaymentsMarkAsProcessed` pref is on), so staff see it with the other online payments. Set the row's `PayNum`, note (`GetFormattedNote`) and merchant fee (`GetMerchantFee`). A refund is inserted as a negative amount. **Assumption:** this is inferred from the method's signature and has to be confirmed against how Open Dental's migrated refund rows look in fox-db.
- **Completed with `PayNum`>0:** update that payment the same way `FormPayment` does (`IsCcCompleted`, note, fee). Never create a second payment. The row's `PayNum` is the key that guarantees this.
- **QuickScreen and lookup results:** `UpdateCareCreditPatField`, `UpsertPreApprovalAmt` / `UpsertAvailableCredit`.

### 3.6 Batch QuickScreen job

`CareCreditJobs.RunBatch(DateTime now)`, once per day per clinic, at or after the process time:

1. **Lock.** Claim the run with a compare-and-set on the `CareCreditBatchLastRunDate` programproperty, so two hosts can't both run: `UPDATE programproperty SET PropertyValue=<today> WHERE ProgramNum=<CareCredit> AND PropertyDesc='CareCreditBatchLastRunDate' AND ClinicNum=0 AND PropertyValue=<value read>`, and run only if one row changed. Don't write the schedule pref for this: `CareCreditBatchProcessDateTime` holds the time of day, and stamping it with the run time would move the schedule each day.
2. For each clinic with `QSBatchEnabled`≠Off and a usable merchant number (clinic, practice fallback, or per-provider):
   - Select appointments from today+1 to today+`CareCreditQSBatchDays` (2, 3 or 4), any status. "Within N days" is the manual's wording; confirm the boundary against a 25.3 trial or the office's migrated batch rows.
   - In by-provider mode, skip appointments whose provider has no merchant number for that clinic, and record a `BatchError` row for each.
   - In **On (with Bal)** mode, keep patients with an estimated patient portion over $1 (`Appointments.GetEstPatientPortion`).
3. Exclude patients from `GetPatNumsForDateRangeToExclude(today-60, today)`. Replace minors with their guarantor (`CareCredit.IsPatAMinor`). A minor with no adult guarantor gets a `BatchError` row and an alert, as the manual describes.
4. Check the required demographics. Missing data gives a `BatchError` row with a readable description, and nothing is sent.
5. Insert `Created` batch rows. Submit them one by one through the API (or as one bulk request if the gated guide offers one), set each to `Pending`, and store the reference IDs.
6. Pull back as in 3.5. Map the results to `PreApproved`, `Declined`, `AccountFound`, `AccountNotFoundQS`, `DupQS` or `CallForAuth`. Write the patient fields (copied to minors through `UpsertPatFieldForMinors`). Rows still unresolved after 24 h become `ExpiredBatch`.
7. **Errors:** set `BatchError`, store the response in `LastResponseStr` (scrubbed, 3.8), and raise one `AlertItem` of `AlertType.CareCreditBatchError` per clinic per day, with `FormToOpen=FormCareCreditTransactions`. A merchant-closed response puts `x` in front of the affected merchant number (programproperty or providerclinic), so the existing closed-merchant UI takes over.
8. **Lookup Only** sends lookup requests and fills only status and available credit.

### 3.7 Where the jobs run

- **Target:** the fork's local background service host, the planned replacement for Open Dental Service (`docs/specs/port-backlog.md`, V-PAYSUITE; decoupling plan rule 6). `CareCreditJobs` exposes `RunBatch` and `PollPending` for it to call every minute.
- **Interim, until that host exists:** the desktop app runs them on one workstation. If `Environment.MachineName` equals `CareCreditJobComputerName`, `FormOpenDental` starts an `ODThread` every 60 s that calls `PollPending()`, plus `RunBatch()` once the process time has passed. The setup screen shows "Background tasks run on: <name>" and a **Run on this computer** button. If no computer is set, the poller still runs for rows the current user created during the session (pullback when windows close), and batch QuickScreen doesn't run. The setup screen says so.
- The compare-and-set lock (step 1 above) and the poller's single-row updates (`CareCreditWebResponses.Update(new,old)`) keep two hosts from double-processing.

### 3.8 Errors and offline behaviour

| Situation | Behaviour |
|---|---|
| Integration disabled | No network call of any kind. Buttons and menus hidden (rule 7). Jobs exit at once. |
| Settings incomplete (no client ID or secret, no base URLs, partner code empty or `1065`) | Setup can't be enabled. Actions show "CareCredit is not configured. Open Setup > Program Links > CareCredit." |
| Network down / DNS / TLS failure | Interactive: "Can't reach CareCredit. Nothing was sent." The row stays `Created`/`CreatedError` (`HandleResponseError`). Purchase: the payment saved before launch is left unlinked, as today. Jobs: retry next cycle. Batch rows stay Pending and expire at 24 h. |
| 400 / 401 / 404 / 422 / 500 | Keep `CareCredit.CareCreditException.ErrorCode` and the existing messages in `CareCreditL.TryLaunchCareCreditPage` ~143–164 (422 = "Training Suspended"). 401 follows the retry rule in 3.2. |
| 429 / throttling | Honour `Retry-After`, back off the job, and tell the interactive user to retry shortly |
| Unknown vendor status | `Unknown` / `UnknownError`, raw response kept, shown in the Errors tab |
| Merchant closed | `x` prefix, existing UI blocks new transactions, error rows flagged "Merchant Closed" |
| Old Open Dental rows (pre-migration) | Displayed as-is. Pullback for them is attempted once with our credentials. Rows from Open Dental's partner context may be refused; on refusal, mark them `ErrorAcknowledged` with a note instead of retrying forever. |

- **Privacy in logs:** never log request bodies. Before storing `LastResponseStr`, drop any SSN or DOB fields if the response echoes them.
- **Least data:** send SSN digits only in Lookup Only mode, and only the last four.

### 3.9 No Open Dental hosts and no HQ

- The client talks only to the configured Synchrony hosts. URL settings must be `https`. Saving refuses any host ending in `opendental.com`, `opendentalsoft.com` or `patientviewer.com`, the same list as the Phase 1 grep guard.
- Remove every CareCredit path through `IWebServiceMainHQ` (section 4.4). `GetSerializedProgramProperties` no longer collects merchant IDs.
- **Partner code `1065`:** treated as not set. Sending Open Dental's partner identity would misattribute this office's traffic to Open Dental.
- Nothing writes `HasLogged`, and nothing uploads transactions anywhere.

### 3.10 Vendor-neutral UI (rule 7)

- **Program-link toolbar button.** `ToolButItems.GetForToolBar` (`OpenDentBusiness/Data Interface/ToolButItems.cs` ~116) also returns buttons for disabled "advertising" bridges. Because of that, the CareCredit button shows in the Account and Treatment Plan toolbars while the program is off, unless "Disable Advertising" is set, and clicking it opens the signup page (WPF `ProgramL` ~91–95). Make it return enabled programs only. This is the generic rule-7 change; coordinate it with the other advertising bridges rather than special-casing CareCredit.
- `FormPayment.butCareCredit`:
  - Visibility becomes `programCareCredit.Enabled`, replacing `!IsAdvertisingDisabled` at ~1229. Also fix the fallback branch at ~1210, which shows the button whenever the program row exists.
  - Delete the "not enabled → open signup page" branch at ~427–432.
- `ControlAppt`: delete the advertising branch at ~611–613 that adds a "CareCredit" item opening the signup page, and the `MenuItemNames.CareCredit` case at ~2288.
- `OpenDental/Logic/ProgramL.cs` ~85–108: delete the "Disable Advertising" drop-down.
- `WpfControlsOD/Logic/ProgramL.cs`:
  - ~91–95: when disabled, do nothing (no signup page).
  - ~607–620: delete the "Disable Advertising" drop-down.
- `CareCreditL.TryGetNewProvOrRegisterProv`: drop the "Sign up for CareCredit" option, leaving "Select a new provider" or Cancel.
- `FormCareCreditSetup`:
  - Remove `checkHideAdvertising`.
  - Rename the "Promotions" group to "CareCredit admin" (it manages the office's own financing terms in Synchrony's Admin module, so it's a function, not an ad).
  - The setup text describes what the integration does, and nothing more.
- `CareCredit.ProviderSignupURL` is not re-implemented.

## 4. Code changes

### 4.1 New files

- `OpenDentBusiness/Bridges/CareCredit/*.cs` (3.1), added to `OpenDentBusiness.csproj`.
- `UnitTests/UnitTests/CareCreditTests.cs`, plus the mock client.

### 4.2 Prebuilt `Bridges.dll` types to re-implement in-repo

This surface is reconstructed only from how this repo calls it. Implement it in namespace `OpenDentBusiness.Bridges`.

`Bridges.dll` still supplies QuickBooks Online, Microsoft 365, USPS and PostcardMania types until the Phase 2 split. Two types named `CareCredit` would collide in files that import both, so:

- remove `using Bridges;` from the CareCredit-only files
- add `using OpenDentBusiness.Bridges;`
- in `WpfControlsOD/Logic/ProgramL.cs`, repoint the `xBridges` alias for the CareCredit lines (or qualify them in full)

| Member (as called) | Callers | Behaviour to implement |
|---|---|---|
| `static class CareCredit` | many | Facade |
| `bool IsMerchantNumberByProv { get; }` | FormCareCredit, FormPayment, CareCreditL | Clinics enabled and `CareCreditIsMerchantNumberByProv`=1 |
| `string GetMerchantNumber(long clinicNum,long provNum,bool isProvOverride)` | CareCreditL ~114, FormPayment ~447 | By provider: `providerclinic(prov,clinic)` then `(prov,0)`. Otherwise: clinic row, then practice row (ClinicNum 0). Throws `ODException` when empty, invalid or closed (message names the provider or clinic). |
| `string GetMerchantNumberByClinic(long clinicNum)` | WebServiceMainHQProxy ~118 (being removed) | Clinic row with practice fallback |
| `List<string> GetMerchantNumsAll()` | CareCreditWebResponses ~206 | Distinct merchant numbers from programproperty and providerclinic |
| `bool IsMerchantNumClosed(string)` / `(string,List<string>)` | Setup, FormCareCredit, FormProvAdditional, CareCreditWebResponses | Starts with `x`, or the list contains `"x"+num` |
| `bool IsMerchantNumValid(string,bool allowClosedMerchant)` | Setup, FormProvAdditional | 16 digits, optional leading `x` when allowed |
| `string SendPrefillReturnPageUrl(CareCreditLandingPage,Patient,string invoiceNumber,double purchaseAmt,double refundAmt,string noteMemo,string promoCode,string userName,double estimatedFeeAmt,string oldRefId,string merchantNum,EnumCareCreditPrefillServiceType serviceType,long payNum,long clinicNum)` | CareCreditL ~135 | 3.4. Throws `CareCreditException` (HTTP errors) or `ODException` (configuration). |
| `void ShowPage(string url)` | CareCreditL ~138 and ~204; ControlAppt ~2289 and WpfControlsOD `ProgramL` ~93 (both only for the signup URL, deleted in 3.10) | Moves to `OpenDental` (`CareCreditL.ShowPage`): non-modal `FormCareCreditWeb` or the default browser |
| `string ProviderSignupURL` | FormPayment, ControlAppt, CareCreditL, WpfControlsOD ProgramL | **Not re-implemented**; callers deleted (3.10) |
| `string GetSessionIDFromUrl(string url)` | FormCareCreditWeb ~34 | Parse the session parameter named in the gated guide |
| `void UpdateWebResponse(CareCreditWebResponse)` | FormCareCredit ~82, FormPayment ~2591 | Pullback and status mapping (3.5); updates the row in place |
| `string GetFormattedNote(CareCreditWebResponse)` | FormPayment ~498 | Short payment note from pullback data: transaction type, Ref ID, promotion, amount |
| `double GetMerchantFee(CareCreditWebResponse)` | FormPayment ~501 | Fee from pullback data, returned as a positive number: `FormRpPaySheet` shows `-MerchantFee` in the fee column and subtracts `MerchantFee` from the amount |
| `string GetUserName(CareCreditWebResponse)` | CareCreditL ~85 | Staff user recorded on the original transaction (from the stored response), else the current user's name |
| `CareCreditQSBatchResponse GetBatchResponse(CareCreditWebResponse)` | FormCareCreditTransactions ~354 | `CareCreditQSBatchResponse.From(row)` |
| `bool IsPatAMinor(Patient)` | CareCreditWebResponses ~227 | Valid birthdate (year > 1880) and age < 18, matching `UpsertPatFieldForMinors` |
| `class CareCredit.CareCreditException : Exception { int ErrorCode; }` | CareCreditL ~142 | HTTP status in `ErrorCode` |
| `enum CareCreditLandingPage { HM, AP, LU, PR, QS, RF }` | CareCreditL | Names as used. Values map to the vendor codes in the gated guide. |
| `enum EnumCareCreditPrefillServiceType { A, C, L, R }` | CareCreditL | `C` is the default |
| `enum EnumCareCreditBatch { Off, On, NoBalance, LookupOnly }` with `[Description]` | FormCareCreditSetup | **Stored as int in `QSBatchEnabled`.** Off=0 and On=1 are certain (`ConvertDatabases6`, Setup default "1"). The values of NoBalance and LookupOnly are not visible in this repo (see open question 6). |
| `class CareCreditQSBatchResponse { static From(CareCreditWebResponse); bool IsInvalidInput; string ResponseDesc; }` | CareCreditWebResponses ~208, FormCareCreditTransactions | Deserialize `LastResponseStr`. It must also read the JSON that Open Dental stored in migrated rows (study their shape on a restored fox-db copy; it's data, not a binary). |

### 4.3 Files and members that change

- `OpenDental/Logic/CareCreditL.cs`: switch to the new facade; delete the AppStream branches and the signup option; add `ShowPage`; catch "not configured" and show the setup hint.
- `OpenDental/Forms/FormCareCreditSetup.cs` / `.Designer.cs`:
  - Add a **Connection** group: client ID, secret (masked), environment, API base URL, hosted-page base URL, partner code, **Test connection** (gets a token and shows success or the error).
  - Add a **Background tasks** group: run-on computer, process time, pullback time.
  - `AddNeededProgramProperties` → `CareCreditSettings.EnsureRows`.
  - `butSave_Click`: block **Enabled** unless the connection test passed and the partner code is valid. Drop the `Programs.IsEnabledByHq` check (a no-op now).
  - Remove `checkHideAdvertising`.
- `OpenDental/Forms/FormCareCreditWeb.cs`: non-modal use, host allowlist, remove Thinfinity, set `IsUrlSingleUse` if confirmed, fix the comments.
- `OpenDental/Forms/FormCareCredit.cs`, `FormCareCreditTransactions.cs`, `FormProvAdditional.cs`: `using` change only. Transactions: label the "Merchant Closed" column as now.
- `OpenDental/Forms/FormPayment.cs`: `using` change; rule-7 fixes at ~427–432, ~1210 and ~1229; logic otherwise unchanged.
- `OpenDentBusiness/Data Interface/ToolButItems.cs` `GetForToolBar`: enabled programs only (generic rule-7 change, 3.10).
- `OpenDental/Forms/FormCareCredit.cs`, `ControlAppt.cs`: hide the staff application actions for California locations (2.6).
- `OpenDental/Main Modules/ControlAppt.cs`: rule-7 fixes at ~611–613 and ~2288; drop the `MenuItemNames.CareCredit` constant.
- `OpenDental/Main Modules/FormOpenDental.cs`: start and stop the interim `CareCreditJobs` thread (3.7).
- `OpenDental/Logic/ProgramL.cs`, `WpfControlsOD/Logic/ProgramL.cs`: rule-7 fixes; repoint the `xBridges` alias.
- `OpenDentBusiness/Data Interface/CareCreditWebResponses.cs`: drop `using Bridges;`; delete `GetPendingForLogging`, `UpdateHasLoggedToTrue` and `MERCHANT_ID_PROG_PROPERTY`; keep the rest (used by the jobs).
- `OpenDentBusiness/Data Interface/ProgramProperties.cs` (`PropertyDescs.CareCredit`): add the new property names from 3.3.
- `OpenDentBusiness/TableTypes/Pref.cs`: comments for the two batch prefs.
- `OpenDentBusiness/TableTypes/CareCreditWebResponse.cs`: `HasLogged` comment → "Legacy: set by Open Dental's HQ upload. Not used."

### 4.4 "Not available" stubs and HQ paths that go away

- `IWebServiceMainHQ`: remove `GetCareCreditOAuthToken`, `GetCareCreditWebToken`, `LogCareCreditTransaction` and `GetCareCreditBatchTimes`, plus their implementations:
  - `WebServiceMainHQNoHq.cs:70–73`
  - `WebServiceMainHQMockDemo.LogCareCreditTransaction` and `LogCareCreditTransactionsDelegate`
- `WebServiceMainHQProxy`: delete `GetCareCreditBatchProcessTime` and `GetCareCreditBatchPullbackTime` (~1123–1150), and the CareCredit block of `GetSerializedProgramProperties` (~99–129).
- `NoHqTests/NoHqTests.cs`:
  - Drop the `GetCareCreditOAuthToken` line (~64) and the two batch-time asserts (~80–81).
  - Add a test that a disabled or unconfigured CareCredit makes no HTTP call.
  - Keep the `ForVendor("CareCredit")` message test only while the message is used.
- The generated SOAP proxy (`Web References/WebServiceMainHQ/Reference.cs`, `.wsdl`) keeps its CareCredit operations until Phase 3 deletes the proxy. Nothing calls them.

## 5. Credentials and agreements the office must get

1. **CareCredit merchant account.** Enroll the practice as a CareCredit provider and get a 16-digit merchant number for each location (and for each provider, if the office uses provider merchant numbers). Start at [CareCredit for providers](https://www.carecredit.com/providers/) ("Get Started" leads to `/providers/contact-team/`). Skip this step if the office already has one from its Open Dental days.
2. **Integration partner status.**
   - Ask the CareCredit relationship manager to set up the office as a **Direct Integration Partner** for Provider Apply & Buy / IHS. The [solution page](https://developer.syf.com/our-products/provider-apply-and-buy) names this audience.
   - Ask for the office's own **partner code**, and confirm that a self-operated, open-source PMS install is acceptable.
   - Confirm which optional modules are allowed: Batch QuickScreen, Admin, Reporting, Lookup, Consumer Self Service.
3. **Developer portal account.** Register at [developer.syf.com](https://developer.syf.com/user/register) with the office's legal entity and accept the [Synchrony Developer Agreement](https://developer.syf.com/terms-of-use). Synchrony may verify identity before issuing keys.
4. **Create an App** under My Apps, requesting the **Patient Data Service** (plus Batch QuickScreen if it's separate) ([FAQ](https://developer.syf.com/faq)). Choose a key-rotation preference. Record the sandbox **client ID and secret**.
5. **Read the gated documents by hand** (no scraping): the [IHS implementation guide](https://developer.syf.com/integration/integrated-hosted-solution-ihs-implementation-guide) and the [Patient Data Service reference](https://developer.syf.com/api-reference/patient-data-service-v1). Record for the developer:
   - token URL and grant
   - sandbox and production base URLs, and the IHS page host
   - request and response schemas, status codes
   - session parameter name and lifetime
   - batch format and timing
   - rate limits
   - whether endpoint details may be published in GPL source
6. **Certificates.** If **Test connection** reports a chain error, install the DigiCert bundle linked on the [portal home page](https://developer.syf.com/).
7. **Compliance.**
   - Ask Synchrony whether it signs a BAA or a privacy addendum for PMS prefill and batch data, or confirm in writing that none is needed. Have the office's privacy officer decide.
   - Confirm the FCRA prescreen handling for Batch QuickScreen (offers shown only on Synchrony's pages).
   - Confirm the California application rule.
8. **Sandbox build and test** (section 6) with anonymized data only.
9. **Production approval.** Apply for Synchrony's approval of the app (Developer Agreement, Approval section), accept any Additional Terms, then get production keys. Re-apply after any change that affects the API interaction.
10. **Configure.** In Setup > Program Links > CareCredit, enter:
    - credentials, environment and URLs
    - partner code
    - merchant numbers
    - payment type and patient fields
    - batch options
    - the background-task computer
    Then click **Test connection**, then **Enabled**.

## 6. Test plan

**Automated** (UnitTests, mock `ICareCreditApiClient`):

- Settings:
  - `EnsureRows` inserts missing rows once.
  - The secret round-trips through `osd1$`.
  - Partner code `1065` and non-https or Open Dental hosts are refused.
- Token: cached and reused; refreshed within 5 minutes of expiry; after a 401, one refresh and one retry; after a second 401, `CareCreditLastAuthError` is set.
- Merchant numbers: `GetMerchantNumber` across practice/clinic/provider fallbacks; closed and invalid numbers; `IsMerchantNumValid` with and without `x`.
- Prefill row creation for each landing page (fields, `WebToken`, `RefNumber`, `TransType`, `ServiceType`).
- Pullback status mapping for every `CareCreditWebStatus`:
  - patient-field writes, including minors
  - pre-approval amount vs available credit exclusivity (existing `UpsertPreApprovalAmt` / `UpsertAvailableCredit`)
- Payment linking:
  - completed with a PayNum updates that payment
  - completed with no PayNum inserts one `OnlinePending` payment
  - a repeated pullback never inserts twice
- Batch selection:
  - day window
  - with-balance threshold
  - 60-day exclusion (`CareCreditWebResponseT` helpers)
  - guarantor substitution
  - missing demographics → `BatchError`
  - expiry at 24 h
  - reprocess
  - compare-and-set lock (two runners, one wins)
- `CareCreditQSBatchResponse.From` parses both new responses and a sanitized sample of a migrated Open Dental row.
- No network: with the program disabled or settings incomplete, the mock records zero calls. The NoHq guard grep still passes.
- Rule 7: with the program disabled, `ToolButItems.GetForToolBar` returns no CareCredit button, and FormPayment hides `butCareCredit` in both branches.
- California: with the clinic or practice state set to CA, the staff application actions are hidden.

**Manual, against the Synchrony sandbox:**

1. **Test connection** succeeds; bad secret → clear error; wrong host → refused.
2. From FormCareCredit: **Apply**, **Lookup**, **Report**, **CareCredit admin**. Each opens the hosted page, signed in, with prefilled data matching the patient.
3. Payment window: CareCredit purchase completes in the window → payment shows card-completed, note and merchant fee; Daily Payments report shows the fee.
4. Purchase abandoned (window closed early) → the payment is unlinked and the "could not be completed" message shows, as today.
5. Refund from the Payment window and from the Transactions window → refund row and payment handling.
6. Self-service link: complete the purchase in another browser as the "patient" → within a few minutes the poller creates an `OnlinePending` payment.
7. Appointment menu: Accept/Decline on a pre-approved patient; Application Needed on a declined one.
8. Batch QuickScreen:
   - Set the process time a few minutes ahead and seed appointments inside and just outside the configured window (adult, minor with guarantor, missing ZIP, balance under $1 with the "with Bal" mode).
   - Check that statuses, amounts, icons, the Errors tab and the alert appear.
   - Force no response → `ExpiredBatch` → Reprocess.
9. Merchant closed (if the sandbox can simulate it) → `x` prefix, controls disabled.
10. Clinics on/off; merchant numbers by provider on/off.
11. Offline: block the API host in Windows Firewall → friendly errors, nothing marked complete, jobs recover after unblocking.
12. Token expiry: shorten the stored expiry by hand → transparent refresh.
13. Host check: capture traffic with a proxy for a full test day. The only hosts contacted are the configured Synchrony hosts, none are Open Dental's, and nothing is contacted while the program is disabled.

**Regression on a restored copy of fox-db** (never the live database):

- Migrated `carecreditwebresponse` rows display in all three tabs.
- Old payments keep their CareCredit fee.
- `QSBatchEnabled` values show the right option (this settles open question 6).
- The old `CareCreditOAuthToken` and the `1065` code are ignored.

## 7. Size, risks and open questions

**Size: L** (about 15 to 20 dev-days):

| Piece | Effort |
|---|---|
| API client and token | ~3 days |
| Facade and status mapping | ~3 days |
| Polling and payment linking | ~3 days |
| Batch QuickScreen job | ~4 days |
| Setup UI and rule-7 cleanup | ~2 days |
| Tests and sandbox passes | ~3 days |

Not included:

- calendar time for Synchrony's partner and production approval, which is likely weeks
- the shared local job host (the interim workstation thread is included)
- whether Synchrony's gated API turns out to need more than this spec assumes (for example signed requests or IP allowlisting)

**Go/no-go gate.** Start coding only after steps 2 to 5 of section 5 succeed (partner status, keys, gated docs). If Synchrony declines, cut the integration as the decoupling plan says:

- remove the UI and the jobs
- keep the tables, rows and enum values for schema compatibility
- keep the Daily Payments fee column for history

The office can still take CareCredit through Synchrony's own provider portal, and record the payment with the CareCredit payment type.

**Risks:**

- **Access may be refused or slow.** Synchrony grants IHS access through partnerships, and its public PMS announcements are all with software vendors. A single office running its own build is unusual.
- **Approval is tied to the app's behaviour.** Any later change to the API interaction needs re-approval, which constrains how freely the open-source code can evolve.
- **Docs are gated and possibly confidential.** Endpoint and field details may not be publishable in the GPL repository. The mitigation is settings and a private configuration note, but that makes the code harder to read and test for others.
- **PHI and credit data.** Prefill and batch requests send identity, contact data, fee estimates and sometimes DOB or SSN digits to a lender. Without a clear BAA or payment-disclosure basis, the office carries compliance risk.
- **The batch needs an always-on host.** The interim workstation thread stops when that PC is off or nobody is signed in, so batches then run late or not at all.
- **No webhooks** means up to an hour's delay for transactions the patient finishes on their own device, and more API calls (rate limits unknown).
- **Duplicate payments** if pullback and the purchase window race. Mitigated by the `PayNum` check and the `Update(new,old)` concurrency guard.
- **Migrated rows** were created under Open Dental's partner context. Their pullback may fail, which the 3.8 handling covers.
- **`osd1$` is obfuscation, not protection** against someone with database access.
- **Namespace collision** with `Bridges.dll` until the Phase 2 Bridges split (4.2).

**Open questions:**

1. Will Synchrony make this office (or the project) a Direct Integration Partner, and issue its own partner code?
2. Production and sandbox base URLs, token endpoint, grant details and token lifetime (gated guide).
3. Exact prefill fields and landing-page and service-type codes, the session parameter name and lifetime, and the pullback status vocabulary (gated reference).
4. Is Batch QuickScreen a separate API or bulk request, and what are its schedule rules and result timing? Can lookup-only mode work without SSN digits?
5. May endpoint paths and field names from the gated docs be published in GPL source?
6. Which ints do `EnumCareCreditBatch.NoBalance` and `LookupOnly` use in `QSBatchEnabled`? Settle it from the office's own data. On a restored fox-db copy, compare each clinic's `QSBatchEnabled` value with what Open Dental 25.3's setup window shows, or set each option once in a 25.3 trial install. Either way, the enum must match.
7. BAA or privacy addendum for prefill and batch data, and the FCRA prescreen obligations for Batch QuickScreen.
8. How do refunds finished outside the app appear in pullback (negative purchase, or a separate type), and should they create a negative payment automatically or only flag the original?
9. Is Consumer Self Service reachable through the same Patient Data Service and credentials, or does it need separate enrollment? The same question applies to Batch QuickScreen, the Admin and Reporting modules and account lookup, which the public page lists as features without saying how they're enabled.
10. Does Synchrony require IP allowlisting, mutual TLS or request signing for the gateway? Any of these would change the desktop-direct design.

## References

- Synchrony Developer Portal, home and certificate notice: https://developer.syf.com/
- Product catalog: https://developer.syf.com/our-products
- Provider Apply & Buy (PMS solution, optional modules, webhooks): https://developer.syf.com/our-products/provider-apply-and-buy
- Integrated Hosted Solution / Patient Data Service overview: https://developer.syf.com/our-products/integrated-hosted-solution
- Quickscreen Apply: https://developer.syf.com/our-products/quickscreen-apply
- Portal FAQ (requesting access, key rotation): https://developer.syf.com/faq
- Synchrony Developer Agreement: https://developer.syf.com/terms-of-use
- Gated, login required: https://developer.syf.com/api-reference/patient-data-service-v1 and https://developer.syf.com/integration/integrated-hosted-solution-ihs-implementation-guide
- Synchrony Partner Center: https://www.synchrony.com/business/partner-center
- CareCredit for providers (enrollment): https://www.carecredit.com/providers/
- Open Dental manual, CareCredit Setup: https://www.opendental.com/site/carecreditsetup.html
- Open Dental manual, CareCredit: https://www.opendental.com/site/carecredit.html
- Open Dental manual, CareCredit Transactions: https://www.opendental.com/site/carecredittransactions.html
- In repo: `D:/Dev/opendental-schema/hq-vendor-table.md` (CareCredit row), `docs/decoupling-plan.md`, `docs/specs/port-backlog.md`, `docs/specs/vendor-direct/edgeexpress.md` (format)

## Fact-check log (2026-10-18)

An adversarial review checked this spec against the public Synchrony pages (IHS, Provider Apply & Buy, FAQ, Developer Agreement, home-page certificate notice), Open Dental's public manual, and `main` at `6855026f5`.

- **Confirmed:**
  - The auth description: OAuth access token obtained with B2B credentials from a REST token endpoint, then sent in an HTTP request header to the SYF Secure API Gateway.
  - `api.syf.com` as the public gateway name.
  - The login-gated API reference and implementation guide.
  - The Agreement's terms on keys, approval, Sandbox, endorsement, robots, throttling and end-user terms.
  - Every HQ method and stub location.
  - Every Bridges.dll member in 4.2, against its grep'd call sites.
  - No compiled-in credentials and no Open Dental hosts in the design.
  - Feasibility **partner-only**.
- **Corrected:**
  - The batch day window (2, 3 or 4 days ahead, not "2 to 4 days out").
  - Which forms call `IsMerchantNumClosed`.
  - The `MerchantFee` sign convention.
  - The second `1065` seed (`To23_3_2`).
  - Optional vs listed features on the solution page.
  - The B2B-credentials-as-client-ID/secret claim, now marked as an assumption.
  - The source of the "hosted-page URL with a session" claim (this repo, not the public page).
  - `IsUrlSingleUse`, which is never set today.
  - `GetLastPendingUpdateDateTime`, which is a row method.
  - `InsertFromCareCredit` status behaviour.
  - The self-service link flow.
  - `ShowPage` callers.
- **Design fixes:**
  - The batch lock moved off the schedule pref, which it would have overwritten, to a new `CareCreditBatchLastRunDate` property.
  - Added the program-link toolbar button that shows while the program is disabled (`ToolButItems.GetForToolBar`) and the FormPayment fallback branch to the rule-7 list.
  - Added the Agreement's ban on redistributing Materials to the GPL question.
  - Gave the California rule a concrete UI behaviour.
