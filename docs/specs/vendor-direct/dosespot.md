# DoseSpot eRx (and Medi-Span): direct-to-vendor spec

Status: draft, 2026-10-18, fact-checked 2026-10-18 (see "Fact-check log" at the end). Baseline: `main` at `6855026f5`. Vendor: DoseSpot (PRN Software LLC). Drug data inside DoseSpot: Medi-Span (Wolters Kluwer).

## Summary

- **Feasibility: partner-only (confirmed).** Every credential the integration needs (subscription key, clinic ID and key, clinician user IDs) is issued only under a DoseSpot contract with a BAA and an integration review. There is no self-serve sign-up and no public sandbox account.
  - The narrative guides on [docs.dosespot.com](https://docs.dosespot.com/) (getting started, auth, SSO) sit behind a partner login.
  - However, DoseSpot serves the machine-readable **Swagger 2.0 contract for each plan publicly** from its own API host, for example [JumpstartV2](https://my.dosespot.com/webapi/v2/swagger/docs/JumpstartV2) and [FullV2](https://my.dosespot.com/webapi/v2/swagger/docs/FullV2). Those files cover the resource endpoints and models, but not the token endpoint, the auth headers or SSO. So the endpoints in §2.5 can be checked today; the auth and SSO details in §2.3 and §2.4 cannot.
- **The code is already mostly direct.** The desktop app calls `my.dosespot.com/webapi/v2` and opens DoseSpot's own prescribing screen in WebView2 through a single sign-on (SSO) URL that it computes locally. Open Dental's HQ supplied a partner **subscription key**, approved each **provider** and **clinic**, and handed out the per-clinic IDs and keys. It also supplied three IDs that only served a one-time v1 to v2 / Medi-Span migration.
- **eRx is dead in Release builds today.** The subscription key comes back empty, so every v2 call fails. The HQ approval gates also never flip to "Enabled", so the eRx window is blocked with "Contact support to enable eRx".
  - A database whose `DoseSpotApiVersion` property is still `1` (a fresh database seeds `1`) takes the **v1** code path to `/webapi` instead. It can never move to v2, because `MigrateDoseSpotIfNeeded` returns as soon as its HQ `BuildOAuthUrl` call throws. §3.2 forces v2 instead.
- **Direct design:**
  - The office (or whoever holds the DoseSpot contract for it) enters a subscription key, a clinic ID and clinic key per location, and a DoseSpot user ID per user.
  - The secrets are stored as `osd1$` values in the eRx program's `programproperty` rows.
  - The workstation gets a short-lived bearer token straight from DoseSpot's token endpoint. It uses a password-style grant with those static values, so there's no browser redirect, loopback listener or PKCE.
  - HQ's approval step becomes a local check. DoseSpot itself remains the authority on whether a clinician may prescribe.
- **Medi-Span needs nothing from us.** The Medi-Span client ID was only used to ask DoseSpot to migrate Open Dental's clinics from Lexicomp to Medi-Span. A new v2 account starts on Medi-Span.
- **Interim:** ship the code configurable but disabled. Until a DoseSpot contract and staging credentials exist, the eRx program stays off and its button stays hidden.
- **Size: M** for the code (about 4 to 6 days including staging checks). Calendar time is dominated by DoseSpot onboarding.

## 1. What the integration does, and what HQ provided

### 1.1 What the office uses it for

All of this is optional and runs only when **Setup > Program Links > eRx** is enabled with the DoseSpot option. Manual prescriptions (Chart > New Rx) never depend on it.

| Feature | Where in code | DoseSpot calls |
|---|---|---|
| Write a prescription for the current patient in DoseSpot's own UI (new Rx, controlled substances with EPCS, pharmacy search, interaction checks, refills) | `ControlChart.Tool_eRx_Click` → `FormErx.ComposeNewRxDoseSpot` (WebView2) | token, add or edit patient, then navigate to the SSO URL |
| Staff prescribing on behalf of a provider (proxy user) | `Tool_eRx_Click` provider picker, `OnBehalfOfUserId` SSO parameter | as above |
| Open the refill-request and transmission-error queue | eRx drop-down menu, `ErxXml.BuildDoseSpotPostDataBytesRefillsErrors` | SSO with `RefillsErrors=1` |
| Counts on the eRx button (refill requests, transaction errors, pending prescriptions) | `ControlChart.RefreshDoseSpotNotifications` (background thread, on every patient selection in Chart) | token, `GET /api/notifications/counts` |
| Pull prescriptions and self-reported medications back into `rxpat` and `medicationpat`, apply deletions and discontinuations, fire the RxCreate automation | `DoseSpotV2.SyncPrescriptionsFromDoseSpot` | `GET .../prescriptions`, `GET .../selfReportedMedications` (paged), `GET /api/pharmacies/{id}` |
| Patient consent to medication history | `FormPatientEdit` consent checkbox, renewed on every refresh | `POST /api/patients/{id}/medications/history/consent` |
| Register an additional clinic (location) from an already registered one | `FormDoseSpotPropertyEdit.butRegisterClinic_Click` → `DoseSpotV2.RegisterClinic` | `POST /api/clinics` returns a new clinic ID and key |

### 1.2 What Open Dental's HQ provided

The sources are `D:/Dev/opendental-schema/hq-vendor-table.md`, the code below, and Open Dental's public manual pages [DoseSpot Setup](https://opendental.com/manual/dosespotsetup.html) and [DoseSpot requirements](https://opendental.com/manual/dosespotrequiredinfo.html). Rows marked *inferred* are reasoned from how the code behaves, not from HQ source.

| What HQ provided | How it reached the office | Code | Needed after decoupling? |
|---|---|---|---|
| **DoseSpot partner subscription key**, sent on every v2 request | `BuildOAuthUrl(regKey,"DoseSpotSubscriptionKey")`, cached in memory | `DoseSpotRESTV2.GetDoseSpotSubscriptionKeyDecrypted` (`DoseSpotV2.cs:1834`), header added in `Request` (`:1858`) | **Yes.** The office's own key, configured locally. |
| **DoseSpot Admin ID**: a partner admin user used to get a v1 token for the migration call | `BuildOAuthUrl(...,"DoseSpotAdminID")` | `DoseSpotRESTV2.MigrateDoseSpotIfNeeded` (`:1737`) | No. The method is deleted. |
| **DoseSpot Client ID**: the partner account ID in the migration route | `BuildOAuthUrl(...,"DoseSpotClientID")` | `MigrateDoseSpotIfNeeded` → `DoseSpotREST.PostInitiateDrugDbMigration` (`DoseSpot.cs:1631`) | No |
| **Medi-Span client ID**: tells DoseSpot which Medi-Span licence to migrate the clinics onto | `BuildOAuthUrl(...,"MedispanClientID")` | same | No. See §2.7. |
| **Provider eRx access**: per-NPI enable status, identity-proofed flag and DoseSpot user ID. This was also HQ's monthly billing check (`DoseSpotDateLastAccessCheck`). | `CustomerUpdates.GetErxAccess` (XML with the registration key and NPIs) | `ControlChart.UpdateErxAccess` (`ControlChart.cs:9154`), gate at `:8733` | No. Replaced by a local check (§3.4). |
| **Clinic eRx access**: per clinic ID and key enable status | `CustomerUpdates.GetClinicErxAccess` | `DoseSpot.SyncClinicErxsWithHQ` (`DoseSpot.cs:738`) and its V2 copy (`DoseSpotV2.cs:735`), gate at `ControlChart.cs:8746` | No. Replaced by a local check. |
| **Provisioning**: created the office's DoseSpot clinic(s) under Open Dental's partner account, generated the clinic ID and key and user IDs, and registered proxy users. The office emailed `erx@opendental.com` and got the values back by email in 5 to 10 business days. | Manual, per the manual. *Inferred:* also pushed through the eConnector (statuses `PendingEconnTransmit` and `InTransitToEconn`, alerts `DoseSpotClinicRegistered` and `DoseSpotProviderRegistered`, forms `FormDoseSpotAssignClinicId` and `FormDoseSpotAssignUserId`) | `Erx.ErxStatus`, `DoseSpot.MakeClinicErxAlert` and `MakeProviderErxAlert` (no callers in this repo) | Yes, but now done by DoseSpot or the office's contract holder, not by us |
| **Customer number for the OID root** of the DoseSpot patient-ID mapping, on first use | `OIDInternals.CustomerPatNum` → `RequestCustomerID` | `DoseSpot.GetDoseSpotRoot` (`DoseSpot.cs:46`) | No. A local root (§3.2). |
| **Billing**: per-prescriber monthly charge (repeat charge `Z100`, `DS;` account IDs) | HQ-side | `FormRepeatChargeEdit` (HQ tool) | No. Billing is between the office and DoseSpot or its contract holder. |
| Permission to use eRx at all | `Programs.IsEnabledByHq` | Now a local stub that always passes | No |

## 2. The vendor's API

### 2.1 Documentation status

- **Guides: not public.** [docs.dosespot.com](https://docs.dosespot.com/) is DoseSpot's developer portal. Its [getting started](https://docs.dosespot.com/staging/docs/getting-started) and [API guide](https://docs.dosespot.com/staging/docs/api-guide) pages show anonymous visitors only a title and a "Log in" link. That is where the token, SSO and onboarding instructions live.
- **Endpoint contracts: public.** DoseSpot's API host serves a Swagger 2.0 file per plan at `https://my.dosespot.com/webapi/v2/swagger/docs/<Plan>`, with no login. Checked on 2026-10-18:
  - [JumpstartV2](https://my.dosespot.com/webapi/v2/swagger/docs/JumpstartV2) has the title "Plan: Jumpstart - V2", host `my.dosespot.com` and base path `/webapi/v2`.
  - The [staging copy](https://my.staging.dosespot.com/webapi/v2/swagger/docs/JumpstartV2) declares host `my.staging.dosespot.com` with the same base path.
  - [FullV2](https://my.dosespot.com/webapi/v2/swagger/docs/FullV2) is the Full plan.
  - None of them defines `securityDefinitions`, a `/connect/token` path or a subscription-key header parameter. Auth is described only in the login-walled guides.
  - Third parties confirm the plan list: an independent, daily-regenerated Rust client ([autogen-dosespot](https://docs.rs/autogen-dosespot/latest/autogen_dosespot/)) builds from these files and lists Full, Hybrid, Jumpstart (each with an EPCS variant) and ReadOnly. A catalog entry ([apis.io](https://apis.io/providers/dosespot/)) lists the "Full + EPCS" and "JumpStart + EPCS" contracts and says credentials come from an assigned integration specialist.
- **What DoseSpot says publicly:**
  - The [Partnership Overview](https://go.dosespot.com/hubfs/DoseSpot/File%20Downloads/DoseSpot_Partnership%20Overview.pdf) describes two integration types.
    - **Jumpstart** embeds DoseSpot's UI through SSO, with 70+ API calls and roughly 0 to 3 months to go live (the brochure says it can be under 30 days).
    - **Full Integration** means building your own screens on 250+ calls, roughly 3 to 6 months.
    - Either way, onboarding goes through a kickoff, a build-out with QA alongside DoseSpot, and Surescripts and Drummond (EPCS) certification before go-live.
  - DoseSpot's [v2 and Medi-Span transition notice](https://dosespot.com/transitioning-to-api-v2-and-the-medi-span-drug-database) says partners had to move to the v2 endpoints, switch the token call to a new security mechanism (it doesn't name it), and expect HTTP 429 rate limiting on every endpoint. It set a 2024 deadline (start by April 30, finish by the end of summer).
- **What this spec relies on:**
  - The **endpoints and fields in §2.5** were checked against DoseSpot's public JumpstartV2 contract.
  - The **auth and SSO details in §2.3 and §2.4** come from this repository's existing v2 client (`DoseSpotV2.cs`), which Open Dental wrote from DoseSpot's partner guide. The code comments cite "DoseSpot API Guide 12.8" for the SSO algorithm. The community Rust client above independently agrees on the token URL, the `Subscription-Key` header, the password grant and the clinic key being the `password` value. They **must be re-checked against the partner guide** once access is granted.
  - Open questions are listed in §7.

This integration matches the **Jumpstart** model: DoseSpot's UI does the prescribing, and our app only pushes demographics and pulls results.

### 2.2 Base URLs

| Purpose | Production | Staging (sandbox) | Source |
|---|---|---|---|
| REST API v2 | `https://my.dosespot.com/webapi/v2` | `https://my.staging.dosespot.com/webapi/v2` | `DoseSpotRESTV2.GetApiUrl`. Confirmed by the `host` and `basePath` of DoseSpot's public production and staging Swagger files (§2.1). |
| Token | `{api}/connect/token` | same | `GetApiUrl(ApiRoute.Token)`. Not in the public Swagger files; the community client uses the same path. |
| SSO into DoseSpot's UI | `https://my.dosespot.com/LoginSingleSignOn.aspx` | `https://my.staging.dosespot.com/LoginSingleSignOn.aspx?b=2` | `Erx.GetRxDoseSpotUrl`. Not publicly documented. The meaning of `b=2` is unknown (§7). |
| Old v1 REST (`/webapi`) and SOAP (`/api/12/api.asmx`) | superseded by v2; DoseSpot's notice set a 2024 migration deadline | | delete |

### 2.3 Authentication (as implemented in-repo; confirm with DoseSpot)

The credentials are all issued by DoseSpot or its contract holder. None of them is a user's own password.

| Value | Scope | Secret? |
|---|---|---|
| Subscription key | Partner or contract account; sent as a header on every call | Yes |
| Clinic ID | One per DoseSpot clinic (location) | No (identifier) |
| Clinic key | One per clinic | **Yes**: it signs SSO codes and acts as the token's client secret |
| User ID | One per clinician or proxy user, per clinic | No |

Token request, step by step:

1. `POST {api}/connect/token` with header `Subscription-Key: <key>` and a form-encoded body. The in-repo client and the independent community client both use `Subscription-Key`. A web-search summary of the login-walled getting-started page named it `Ocp-Apim-Subscription-Key` instead. That summary is unreliable, since it also gave a staging host that DoseSpot's own staging contract contradicts. Treat the name as unconfirmed until the partner guide is in hand (§7).
2. The body has six fields:

   | Field | Value |
   |---|---|
   | `grant_type` | `password` |
   | `client_id` | the clinic ID |
   | `client_secret` | the clinic key |
   | `username` | the DoseSpot user ID |
   | `password` | the clinic key |
   | `scope` | `api` |

3. Today's code also sends `Authorization: Basic base64(userId:<SSO code>)`. That looks like a holdover from v1, where Basic auth carried the clinic ID and a hashed clinic key. Keep it only if the partner guide requires it.
4. The response is JSON with `access_token`. The code reads nothing else. A standard OAuth response should also carry `expires_in` and `token_type`. This grant normally returns no refresh token. A third-party integration write-up ([Mindbowser](https://www.mindbowser.com/?p=53580), describing the older Basic-auth token call) reports that staging tokens last 10 minutes.
5. Every API call then sends `Authorization: Bearer <access_token>`, the `Subscription-Key` header and JSON bodies.
6. Response envelope: business errors come back as `Result.ResultCode` containing `ERROR`, with the reason in `Result.ResultDescription`. Lists are `Items[]` plus `PageResult { CurrentPage, HasNext, ... }`. Single objects are `Item`. Created objects return `Id`.

Bugs to fix in the rewrite:

- The form values are not URL-encoded.
- The content type is sent as `x-www-form-urlencoded` (missing `application/`).
- `Accept` is set to the same wrong value.

### 2.4 SSO into DoseSpot's UI

The SSO algorithm and URL are not in any public DoseSpot source; this section describes the in-repo implementation (`DoseSpot.CreateSsoCode`, `CreateSsoUserIdVerify`, `GetSingleSignOnQueryString`). The workstation computes the SSO values locally from the clinic key. It makes no network call to build them. In our own words:

1. Make a fresh random 32-character phrase.
2. The **SSO code** is the phrase, followed by the Base64 SHA-512 hash of (phrase + clinic key), with a trailing `==` removed.
3. The **user verification code** is the Base64 SHA-512 hash of (user ID + the first 22 characters of the same phrase + clinic key), also with a trailing `==` removed.
4. Navigate WebView2 to the SSO URL with these query parameters:

   | Parameter | Value |
   |---|---|
   | `SingleSignOnClinicId` | the clinic ID |
   | `SingleSignOnUserId` | the user ID |
   | `SingleSignOnCode` | the SSO code from step 2 |
   | `SingleSignOnUserIdVerify` | the user verification code from step 3 |
   | `PatientId` | the DoseSpot patient ID; or `RefillsErrors=1` for the queue |
   | `OnBehalfOfUserId` | for proxy users only |

DoseSpot's UI then handles identity proofing (IDP), EPCS two-factor and its own clinician agreements.

### 2.5 Endpoints and fields we need

All paths are relative to `{api}` = `.../webapi/v2`. The fields listed are the ones the code reads or sends. Every path below except `/connect/token` appears in DoseSpot's public **JumpstartV2** contract, so the Jumpstart plan covers all of them. That contract also names the patient enums (Gender: Male, Female, Unknown; WeightMetric: lb, kg; HeightMetric: inch, cm) and the envelope (`Result`, `Items`, `Item`, `Id`, `PageResult`).

| Call | Request | Response fields used |
|---|---|---|
| `POST /connect/token` | §2.3 | `access_token` (also read `expires_in`) |
| `POST /api/patients` | `FirstName`, `LastName`, `DateOfBirth`, `Gender` (our enum + 1, with Other sent as Unknown), `Address1`, `City`, `State`, `ZipCode`, `PrimaryPhone`, `PrimaryPhoneType`, `Active`. Under 18 also needs `Weight`, `WeightMetric`, `Height` and `HeightMetric` from vitals. | `Id` (stored in `oidexternal`) |
| `PUT /api/patients/{patientId}` | same body | `Result` |
| `GET /api/notifications/counts` | none | `RefillRequestsCount`, `TransactionErrorsCount`, `PendingPrescriptionsCount` (the contract also returns `PendingRxChangeCount`, which the code ignores) |
| `GET /api/patients/{patientId}/prescriptions?pageNumber=n` | none | `PrescriptionId`, `WrittenDate`, `Directions`, `Quantity`, `Refills`, `PharmacyId`, `PharmacyNotes`, `PrescriberId`, `Status`, `MedicationStatus`, `DateInactive`, `DisplayName`, `GenericProductName`, `RxCUI`, `Schedule`, `EffectiveDate`, `LastFillDate`; plus `PageResult` |
| `GET /api/patients/{patientId}/selfReportedMedications?pageNumber=n` | none | `SelfReportedMedicationId`, `DisplayName`, `Status`, `DiscontinuedDate`, `DatePrescribed`, `DosageText`, `Dispense`, `Refills`, `RxCUI`, `Schedule`, `Comment` |
| `GET /api/pharmacies/{pharmacyId}` | none | `Item.StoreName` |
| `POST /api/patients/{patientId}/medications/history/consent` | empty | `Result` |
| `POST /api/clinics` | `ClinicName`, `Address1`, `City`, `State`, `ZipCode`, `PrimaryPhone`, `PrimaryPhoneType` | `ClinicId`, `ClinicKey` |
| `POST`/`PUT /api/patients/{id}/selfReportedMedications/freetext[/{medId}]` | `DisplayName`, `Status`, `Comment` (500 characters max), `InactiveDate` | `Id`. **Currently unused:** `SyncPrescriptionsToDoseSpot` has no callers. |
| `POST /api/clinicGroups` | | HQ-only tool. Delete. |
| `GET /api/clinician?npi=&dea=` | none | `Id` (an `IdentifierResponse`). **Not used today.** It's an optional way to fill in a prescriber's DoseSpot user ID from the NPI (§7 question 8). |

Prerequisite data (from the code's validators and the [requirements page](https://opendental.com/manual/dosespotrequiredinfo.html)):

- **Practice or clinic:** a 10-digit phone (area code not 555, and the digits can't start with 0 or 1, per `IsPhoneNumberValid` and `CheckAreaCode`), a street address that isn't a PO box, a 2-letter state and a ZIP code. The code's ZIP check only rejects a blank value (its format regex is joined with `&&` to the blank test, so it never fires); the rewrite should accept `#####`, `#####-####` or `#########` as the regex intends. The Open Dental manual asks for 9 digits.
- **Prescriber:** a real person with a first and last name, a 10-digit NPI, a DEA number or "NONE", a state licence and licensing state, and a birthdate.
- **Patient:** name, date of birth, address and a valid phone.

### 2.6 Rate limits

- DoseSpot states that **all v2 endpoints are rate limited** and return **HTTP 429** when a limit is exceeded. The limit values come from the integration specialist and aren't published ([transition notice](https://dosespot.com/transitioning-to-api-v2-and-the-medi-span-drug-database)).
- DoseSpot also offers push notifications to cut polling. Those need a publicly reachable receiver, so they don't fit ground rule 6. We poll, with throttling (§3.3).
- Today's code is chatty. Every patient selection in Chart costs a token, a counts call, a consent post, two paged list calls and a pharmacy lookup per prescription. §3.3 fixes this.

### 2.7 Terms that matter

- **Contract:** API access is sales-gated. DoseSpot's clients range from telehealth and EHR/PM software companies to dental service organizations ([Partnership Overview](https://go.dosespot.com/hubfs/DoseSpot/File%20Downloads/DoseSpot_Partnership%20Overview.pdf); contact `sales@dosespot.com`).
  - Pricing isn't public. Third-party reviews mention per-prescriber fees and one-time integration fees, which is unverified.
  - Whether DoseSpot will contract directly with a single practice that runs self-maintained open-source software is **unknown** (§7).
- **BAA:** DoseSpot publishes its [Business Associate Agreement](https://dosespot.com/exhibit-c-business-associate-agreement) (Exhibit C to its Client Service Agreement, last updated 2024-08-15).
  - In it, DoseSpot is the business associate and the client is the covered entity.
  - There is a separate [Sub-BAA](https://dosespot.com/wp-content/uploads/2025/01/Exhibit-C-Sub-Business-Associate-Agreement-to-Client-Service-Agreement.pdf) for a client that is itself a business associate. For example, a support company holding the contract for offices would sign this one.
- **Certification:** go-live requires DoseSpot's integration review plus Surescripts certification prep and testing. EPCS (controlled substances) requires DoseSpot's Drummond-certified flow, prescriber identity proofing and two-factor enrollment. All of that happens inside DoseSpot's UI in this design.
- **Clinician agreements:** DoseSpot shows its own agreements to clinicians before they can prescribe. Partners report this as a HIPAA step ([example](https://help.akutehealth.com/article/38-dosespot-e-prescribing-authorization-agreement)).
- **PHI:** demographics, medication lists and prescriptions flow to DoseSpot. Only the office's covered-entity relationship under the BAA permits that.
- **Medi-Span:**
  - DoseSpot v2 uses Wolters Kluwer's Medi-Span compendium in place of the retired Lexicomp ([transition notice](https://dosespot.com/transitioning-to-api-v2-and-the-medi-span-drug-database)).
  - The drug data is licensed and served by DoseSpot. Our app never queries Medi-Span directly; it only stores the `RxCUI` and names DoseSpot returns.
  - So the office needs **no Medi-Span licence or ID**. HQ's "Medispan client ID" was only the input to the one-time v1 to v2 migration request. Confirm with DoseSpot that a new account starts on Medi-Span.
- **Existing Open Dental-provisioned accounts:**
  - An office migrating from Open Dental has its DoseSpot clinic, clinicians and prescription history under **Open Dental's partner account**.
  - Its old clinic keys almost certainly won't work with a different subscription key. Using them outside Open Dental's agreement would be improper anyway.
  - Continuity of history, IDP and EPCS enrollment needs DoseSpot to move the records to the new contract holder. Otherwise everyone re-registers and re-proofs (§7).

## 3. Desktop design

### 3.1 Flow choice

- **No system-browser OAuth, PKCE, device code or loopback redirect.** DoseSpot v2 issues tokens for a password-style grant built from static, contract-issued values: the clinic ID and key as client credentials and the clinician's user ID as the username.
  - The workstation posts them straight to DoseSpot's token endpoint. In effect it's a client-credentials flow per clinic and user.
  - The interactive part is DoseSpot's SSO page in our existing WebView2 window (`FormErx`). DoseSpot's own UI handles IDP, EPCS two-factor and agreements.
- **Sequence when the user clicks eRx:**
  1. Load the local config.
  2. Validate the practice, provider and patient.
  3. Get or reuse a token.
  4. Add or update the patient.
  5. Build the SSO URL.
  6. Open `FormErx`.
  7. On close, sync prescriptions and refresh the counts.

### 3.2 Where credentials and settings live

Everything lives on the existing **eRx program** (`ProgramName.eRx`) in `programproperty`. There are **no schema changes and no ConvertDatabases steps**.

- A helper, `Erx.EnsureDoseSpotProperties()`, inserts missing rows at runtime the first time eRx Setup opens.
- Secrets are written with `CDT.Class1.Encrypt` (`osd1$`) and have `IsMasked=1` and `IsHighSecurity=1`. Use `Encrypt` and check its return value, or check `CDT.Class1.IsEncrypted` after `TryEncrypt`. `TryEncrypt` silently returns the plain text when encryption fails, and that must not be saved as if it were protected. Editing them needs `ManageHighSecurityProgProperties` (`ProgramProperties.CanEditProperties`).

| Where | PropertyDesc / key | ClinicNum | Value | Notes |
|---|---|---|---|---|
| programproperty | `eRx Option` | 0 | `1` (DoseSpot) | existing |
| programproperty | `DoseSpot Environment` | 0 | `Production` or `Staging` | **new**, default `Production`. Replaces the `ODBuild.IsDebug()` switch, so a Debug build never silently sends production credentials to staging, or the reverse. |
| programproperty | `DoseSpot Subscription Key` | 0 only | `osd1$...` | **new**, secret. It's never copied per clinic; reads ignore clinic rows. |
| programproperty | `DoseSpot Clinic ID` | per clinic (0 = no clinics, or HQ) | number | existing |
| programproperty | `DoseSpot Clinic Key` | per clinic | `osd1$...` | existing; **plain text today**, so it becomes encrypted |
| programproperty | `DoseSpotApiVersion` | 0 | forced to `2` on save; ignored on read | existing (a fresh database seeds `1`) |
| programproperty | `DoseSpotApiMigrationRequested` | 0 | left untouched | existing, unused |
| userodpref | FkeyType `Program`, Fkey = eRx ProgramNum | per user, per clinic | DoseSpot user ID | existing, entered in **Security > User Edit**; not secret |
| oidexternal | root `2.16.840.1.113883.3.4337.25128.<n>.<IdentifierType.Patient>` | | DoseSpot patient ID per PatNum | existing; see the OID root note below |
| clinicerx, providererx | | | | kept for schema compatibility; no longer read for DoseSpot gating (§3.4) |

Read rules:

- If a value has the `osd1$` prefix, call `CDT.Class1.Decrypt` (not `TryDecrypt`, which hands back the ciphertext on failure). On failure, treat it as **not configured** and ask the user to re-enter it. Never send an undecryptable value.
- A value without the prefix is plain text, as left by Open Dental. Use it as is and re-encrypt it the next time eRx Setup is saved.
- `clinicerx.ClinicKey` holds plaintext keys copied by Open Dental. On the first save of eRx Setup, overwrite those columns with the encrypted value or blank them. That's a data update, not a schema change.

OID root:

- `GetDoseSpotRoot` builds a new root from HQ's customer number, which now throws.
- New rule: if no root row matches the `...4337.25128` prefix, insert the root with a local suffix `0`. The value is only a local lookup key and is never sent to DoseSpot.
- The prefix stays so that Open Dental-migrated rows keep matching.

**Never stored:** access tokens, SSO codes, or the HQ-era Admin, Client and Medi-Span IDs.

### 3.3 Tokens, refresh and throttling

- **Token cache:**
  - Tokens are held in memory only, keyed by (environment, clinic ID, user ID).
  - Each lives until `expires_in` minus 60 seconds. If `expires_in` is missing, assume 10 minutes until DoseSpot confirms the lifetime.
  - Tokens are never written to the database, logs or `erxlog`.
- **Refresh:**
  - Re-run the token request on expiry. There's no refresh token.
  - On a 401 from an API call, drop the cached token, get a new one and retry that call **once**.
- **Thread safety:**
  - Today the SSO phrase lives in a static field shared by the UI thread and the background refresh thread, and `GetToken` never resets it, so phrases get reused.
  - Instead, generate the phrase per call with `RNGCryptoServiceProvider` and pass it explicitly to both hash functions.
- **Polling throttle**, replacing "everything on every patient selection":
  - Notification counts: at most once every 2 minutes per user and clinic.
  - Prescription sync for the selected patient: when `FormErx` closes, on **eRx > Refresh**, or when the patient is selected and wasn't synced in the last 10 minutes.
  - Medication-history consent: re-posted at most once per 24 hours per patient per workstation.
- **HTTP:**
  - One shared `HttpClient` (TLS 1.2 or later), with a 30-second timeout.
  - A neutral `User-Agent` (our product name and version).
  - Honour `Retry-After` on 429.

### 3.4 Replacing HQ's provider and clinic approval

| Before (HQ) | After (local) |
|---|---|
| `providererx.IsEnabled==Enabled`, set by HQ through `GetErxAccess` | The provider has eRx enabled in **Lists > Providers** (`Provider.IsErxEnabled`) and passes `DoseSpot.ValidateProvider`. The user (or the on-behalf-of provider) has a DoseSpot user ID. |
| `clinicerx.EnabledStatus==Enabled`, set by HQ through `GetClinicErxAccess` | The clinic has a clinic ID and a key that decrypts, and the subscription key is set |
| "Contact support to enable eRx for provider/clinic" | A neutral message naming the missing item and the screen where it's set |

- **DoseSpot is the real authority.** An unregistered or inactive clinician fails the token request or sees DoseSpot's IDP screen.
- The plug-in hook `ContrChart.Tool_eRx_Click_Authorize` stays.
- The `if(!ODBuild.IsDebug())` wrapper around the gate goes, so Debug and Release behave the same.

### 3.5 Errors and offline behaviour

| Condition | Interactive eRx click | Background refresh |
|---|---|---|
| Not configured (no subscription key, clinic ID or key, or user ID) | A message naming what's missing and where to set it; nothing is sent | Silent; the button shows no counts |
| Token rejected (400 `invalid_client` or `invalid_grant`, or 401) | "DoseSpot rejected the clinic ID/key for <clinic> or the user ID for <user>." Don't open the window. | Button in the error state |
| Not authorized for the user (existing `DoseSpotNotAuthorized` code) | Show DoseSpot's message | Not shown in red (as today) |
| 429 | "DoseSpot is busy. Try again in N seconds." | Skip polls until `Retry-After` (default 60 seconds) |
| No network (no HTTP response) | "DoseSpot could not be reached." Chart keeps working, and New Rx (manual) is still available. | Button in the error state; no dialogs from the background thread |
| 5xx | One retry with jitter for GETs only. **Never auto-retry `POST /api/patients`**, because it can create a duplicate patient. | Same |
| `Result.ResultCode=ERROR` | Show `ResultDescription` | Logged; counts unchanged |

**Logging:** `erxlog.MsgText` currently stores the full SSO query string, including `SingleSignOnCode` and `SingleSignOnUserIdVerify`. Store it with both values replaced by `***`. Old `erxlog` rows in a migrated database also contain them; the office may want to purge them (§7).

### 3.6 Hosts contacted

- **Only** `my.dosespot.com`, or `my.staging.dosespot.com` when the environment is `Staging`.
- Nothing goes to `*.opendental.com`, `opendentalsoft.com` or `patientviewer.com`. `tools/Test-NoHqHosts.ps1` must keep passing.
- The URL overrides `Introspection.DoseSpotURL` and `DoseSpotSingleSignOnURL` stay, for testing.
- The `DoseSpotV2DecryptedSubscriptionKey` override goes, because it would hold a secret in plain text in a preference.

### 3.7 Off by default, vendor-neutral

- The Chart **eRx** button is added only when the eRx program is enabled. `ControlChart.LayoutToolBar` (`:4993`, button added at `:5024`) adds it unconditionally today, except for the eCW bridge cases.
- Setup and enable texts describe what the integration does, and nothing else. Remove the "see our online manual", "contact support to sign up" and "acknowledge eRx fees, see the website" wording in:
  - `FormErxSetup`
  - `FormProvEdit`
  - `Tool_eRx_Click`

## 4. Code changes

### 4.1 Files and members

| File | Member | Change |
|---|---|---|
| `OpenDentBusiness/WebBridges/Erx/Erx.cs` | `PropertyDescs` | Add `SubscriptionKey="DoseSpot Subscription Key"` and `Environment="DoseSpot Environment"` |
| | `GetRxDoseSpotUrl` | Choose the URL by the environment property, not `ODBuild.IsDebug()` |
| | new `EnsureDoseSpotProperties`, `GetDoseSpotSecret`/`SetDoseSpotSecret`, `IsDoseSpotStaging` | Insert missing rows at runtime; encrypt and decrypt per §3.2 |
| `OpenDentBusiness/WebBridges/Erx/DoseSpotV2.cs` | `DoseSpotRESTV2.GetToken` | Rewrite per §2.3: proper form encoding and content type, token cache, `expires_in`; keep or drop the Basic header per the partner guide |
| | `GetDoseSpotSubscriptionKeyDecrypted` | Read the local property. Remove the HQ `BuildOAuthUrl` call and the Introspection key override. The 24-hour re-check never fires today (its date subtraction has the wrong sign); refresh when the Programs cache is invalidated instead. |
| | `MigrateDoseSpotIfNeeded` | **Delete.** It was the HQ Admin, Client and Medi-Span ID path. |
| | `Request`, `GetApiUrl` | Shared `HttpClient`, environment-based URL, 401 refresh-and-retry once, 429 `Retry-After`, a typed `DoseSpotException : ODException` |
| | `PostAddClinicToClinicGroup` | Delete (HQ-only tool) |
| | `DoseSpotV2.SyncClinicErxsWithHQ` | Delete |
| | `DoseSpotV2` duplicates of `DoseSpot` helpers | Merge into one class (the callers mostly use `DoseSpot.*` already) |
| `OpenDentBusiness/WebBridges/Erx/DoseSpot.cs` | `SyncClinicErxsWithHQ`, `MakeClinicErxsForDoseSpot` | Delete |
| | `GetDoseSpotRoot` | Local root (§3.2); no `OIDInternals.CustomerPatNum` |
| | `CreateSsoCode`, `CreateSsoUserIdVerify`, `Get32CharPhrase`, `_randomPhrase32` | Generate the phrase per call with a cryptographic RNG and pass it explicitly; drop the static field |
| | `GetClinicIdAndKey` | Decrypt the key; neutral error text |
| | `RegisterClinic` | Save the new key encrypted, with the masked and high-security flags |
| | `GetUserID` | The message points to Security > User Edit, not "call support" |
| | clinic validator (the `clinic.Zip` check, `DoseSpot.cs:1095` and `DoseSpotV2.cs:1092`) | Fix the `&&` so the ZIP format is actually checked |
| | `GetSingleSignOn`, `MakeDoseSpotClinic`, `MakeDoseSpotClinician` | Replace the `DoseSpotService.SingleSignOn` type with a small in-repo `DoseSpotSso` class. The other two are dead; delete them. |
| | `DoseSpotREST` (v1 `GetToken`, `Request`, `GetApiUrl`, `PostInitiateDrugDbMigration`, all v1 calls) | Delete. Keep the `MedicationStatus` and `PrescriptionStatus` enums and `PropertyDescs`, or move them to a `DoseSpotModels.cs`. |
| | `MakeClinicErxAlert`, `MakeProviderErxAlert`, `GenerateAccountId`, `IsDoseSpotAccountId`, `PatNumDoseSpotCustomer` | HQ provisioning and billing leftovers; delete with Phase 3, together with `FormDoseSpotAssignClinicId`, `FormDoseSpotAssignUserId`, the `FormOpenDental` alert cases, `FormErxAccess` and the `FormRepeatChargeEdit` `DS;` code |
| `OpenDentBusiness/Web References/DoseSpotService/*` | | Delete the v1 SOAP proxy and its csproj entries |
| `OpenDentBusiness/WebBridges/Erx/ErxXml.cs` | `BuildDoseSpotPostDataBytes`, `...RefillsErrors` | Same signatures; use the new SSO builder |
| `OpenDentBusiness/WebServices/CustomerUpdatesProxy.cs` | `ICustomerUpdates.GetClinicErxAccess` and the NoHq stub | Delete once no callers remain. `GetErxAccess` goes when the NewCrop path in `UpdateErxAccess` is also local (NewCrop spec). |
| `OpenDentBusiness/Db/Enumerations.cs` | `OAuthApplicationNames.DoseSpotSubscriptionKey`, `DoseSpotClientID`, `DoseSpotAdminID`, `MedispanClientID` | Delete; they're never stored in the database |
| `OpenDentBusiness/Misc/Introspection.cs` | `IntrospectionEntity.DoseSpotV2DecryptedSubscriptionKey` | Delete. Deserialization already skips unknown keys. |
| `OpenDental/Main Modules/ControlChart.cs` | `LayoutToolBar` | Add the eRx button only when the program is enabled |
| | `RefreshDoseSpotNotifications` (`:7976`) | v2 only, throttled (§3.3); remove the `MigrateDoseSpotIfNeeded` call and the version branching |
| | `Tool_eRx_Click` DoseSpot branch (`:8556`) | Local gate (§3.4); drop the version branch and the `IsDebug` wrapper; neutral texts; redact `ErxLog.MsgText` |
| | `UpdateErxAccess` (`:9154`) | Remove the DoseSpot branch; the NewCrop branch follows the NewCrop spec |
| | `ErxBrowserClosed`, `menuItemErxRefresh_Click`, `ModuleSelectedErx` | Call the throttled refresh |
| `OpenDental/Forms/FormErxSetup.cs` + designer | | Masked subscription-key field and Environment selector at the top of the DoseSpot section; the Clinic Key column shows `••••`; encrypt on save; set `DoseSpotApiVersion=2`; require the high-security permission; neutral radio-button messages |
| `OpenDental/Forms/FormDoseSpotProperyEdit.cs` + designer | | Masked key box with a "show" toggle; drop the version branch; neutral text after registering |
| `OpenDental/Forms/FormPatientEdit.cs` | consent block (`:2726`) | Drop the version branch |
| `OpenDental/Forms/FormProvEdit.cs` | eRx enable confirmation (`:523`) | Neutral text, with no fee or website wording |
| `UnitTests/UnitTests/ErxTests.cs`, a new `DoseSpotTests.cs`, `NoHqTests` | | Known-answer SSO test with a fixed phrase; token body encoding; secret round trip and plain-text passthrough; 429 and 401 handling with a fake `HttpMessageHandler`; `GetDoseSpotRoot` on an empty table never touches `CustomerUpdatesProxy` |

### 4.2 Prebuilt `Bridges.dll` types

**None.** No DoseSpot or Medi-Span type comes from `Bridges.dll`. The only generated proxy in use, `DoseSpotService` (SOAP v1), is in-repo source and is deleted per §4.1.

### 4.3 "Not available" stubs this replaces

| Stub | Reached from | Replaced by |
|---|---|---|
| `WebServiceMainHQNoHq.BuildOAuthUrl("DoseSpotSubscriptionKey")` | `GetDoseSpotSubscriptionKeyDecrypted` | the local `DoseSpot Subscription Key` property |
| `BuildOAuthUrl("DoseSpotAdminID"/"DoseSpotClientID"/"MedispanClientID")` | `MigrateDoseSpotIfNeeded` | nothing; the method is deleted |
| `CustomerUpdatesNoHq.GetClinicErxAccess` | `DoseSpot.SyncClinicErxsWithHQ`, `DoseSpotV2.SyncClinicErxsWithHQ` | the local clinic check (§3.4) |
| `CustomerUpdatesNoHq.GetErxAccess` (DoseSpot path) | `ControlChart.UpdateErxAccess` | the local provider check (§3.4) |
| `CustomerUpdatesNoHq.RequestCustomerID` | `OIDInternals.CustomerPatNum` ← `DoseSpot.GetDoseSpotRoot` | the local OID root suffix |

## 5. Credentials the office must register

There is no self-serve route. Someone has to hold a DoseSpot contract. That's either the office itself (DoseSpot lists dental service organizations among its clients) or an organization that supports offices running this software, which would sign as a business associate. Decide that first (§7).

1. **Contact DoseSpot sales** (`sales@dosespot.com`, per the [Partnership Overview](https://go.dosespot.com/hubfs/DoseSpot/File%20Downloads/DoseSpot_Partnership%20Overview.pdf)).
   - Ask for the **Jumpstart** integration type, which is SSO into DoseSpot's UI.
   - Ask for pricing, minimums and any one-time fees.
2. **Sign the Client Service Agreement and the BAA:**
   - [Exhibit C BAA](https://dosespot.com/exhibit-c-business-associate-agreement) if the office signs.
   - The [Sub-BAA](https://dosespot.com/wp-content/uploads/2025/01/Exhibit-C-Sub-Business-Associate-Agreement-to-Client-Service-Agreement.pdf) if a support organization signs for offices. That organization also needs its own BAA with each office.
3. **Get staging access** from the assigned integration specialist:
   - a login to [docs.dosespot.com](https://docs.dosespot.com/) and the Postman collection
   - a **staging subscription key**
   - two staging clinics (clinic ID and key each)
   - one staging prescriber user ID and one staging proxy (staff) user ID
   - the rate-limit values and the token lifetime
4. **Confirm the contract details** in §7 questions 3 to 5 against the partner guide, and adjust the code before testing.
5. **Run the test plan** (§6) in staging. Then complete DoseSpot's **integration review** and Surescripts certification prep and testing. EPCS certification is DoseSpot's (Drummond), but the review confirms our flow.
6. **Get production values:**
   - the production subscription key
   - the clinic ID and key for each office location (register additional locations from eRx Setup with **Register**, or have DoseSpot create them)
   - a user ID for each prescriber and proxy user
7. **For each prescriber,** register with DoseSpot:
   - NPI, DEA number (or none), state licence and licensing state, and birthdate
   - complete **identity proofing** inside DoseSpot's UI on the first eRx launch
   - for **EPCS**: enroll two-factor, then have the practice administrator (a non-prescriber) approve EPCS in DoseSpot's admin console
8. **If migrating from Open Dental's DoseSpot account,** ask DoseSpot to transfer:
   - the clinic(s)
   - clinicians, including IDP and EPCS status
   - patients and prescription history

   If DoseSpot won't, plan a re-registration date and keep the old account's history exportable.
9. **Configure locally:**
   - In **Setup > Program Links > eRx**: choose DoseSpot, then set the Environment, the Subscription Key and the Clinic ID and Key per clinic.
   - In **Security > Users**: enter the DoseSpot User ID per user, per clinic where it differs.
   - In **Lists > Providers**: enable eRx per prescriber.
10. **Check the data requirements:**
    - practice or clinic phone, street address (not a PO box) and ZIP code
    - provider NPI and licence fields
    - patient address and phone, plus height and weight for patients under 18
11. **Network:**
    - Allow outbound HTTPS from workstations to `my.dosespot.com`, and to `my.staging.dosespot.com` while testing.
    - Install the WebView2 runtime.
12. **First use:** each clinician accepts DoseSpot's in-app agreements.

## 6. Test plan

Before an office has a contract, only the unit tests and the "disabled" checks can run.

**Unit tests (offline):**

- SSO code and user-verification code match a known answer for a fixed phrase, key and user ID.
- The token body is form-encoded. A clinic key containing `+`, `/` and `=` survives the round trip.
- Subscription and clinic keys round-trip as `osd1$`.
  - A plain-text value passes through.
  - A corrupted `osd1$` value reports "not configured".
- The token cache honours `expires_in`.
  - A 401 triggers exactly one re-token and retry.
  - A 429 with `Retry-After` suppresses polls.
- `GetDoseSpotRoot` on an empty `oidexternal` inserts the local root. It never calls `CustomerUpdatesProxy` (a fake throws if it's called).
- The `NoHqTests` scan shows no DoseSpot code referencing `WebServiceMainHQProxy` or `CustomerUpdatesProxy`.

**Manual checks** on a dev database (`tools/New-DevDatabase.ps1`) with **staging** credentials:

1. **Off by default:** a fresh database has eRx disabled. There's no eRx button in Chart, and a proxy or firewall log shows no DoseSpot traffic.
2. **Setup:**
   - Enable eRx, choose DoseSpot, set Environment to Staging, and enter the subscription key and the HQ clinic ID and key.
   - Confirm in SQL that the key rows start with `osd1$` and have `IsMasked=1` and `IsHighSecurity=1`.
   - Confirm the grid masks the key, and that a user without `ManageHighSecurityProgProperties` can't edit it.
3. **Prescriber flow:**
   - Set the user's DoseSpot user ID.
   - Open a patient with complete data and click eRx. DoseSpot's UI should open on that patient.
   - An `oidexternal` row should hold the DoseSpot patient ID under the local root.
4. **Write a prescription** to a staging test pharmacy and close the window.
   - The `rxpat` and `medicationpat` rows appear with the right send status and pharmacy name.
   - The RxCreate automation fires.
5. **Discontinue and delete in DoseSpot,** then refresh. The local medication gets a stop date, the Rx is deleted, and security log entries are written.
6. **Proxy flow:** a staff user picks a provider. DoseSpot shows "on behalf of" that provider.
7. **Refills/errors menu** opens the queue. The button counts match DoseSpot.
8. **Consent:** tick the consent box in Patient Edit. The POST succeeds, and it isn't re-posted within 24 hours.
9. **Register Clinic** for a second clinic. The new ID and key are saved encrypted, and eRx works there.
10. **Error cases** (each should show the clear message from §3.5, or exercise backoff):
    - a wrong clinic key
    - a wrong subscription key
    - a missing user ID
    - a patient under 18 without vitals
    - a forced 429 (fake handler)
11. **Unplug the network.** Chart loads, the eRx button shows the error state with no modal dialogs, and New Rx (manual) still works.
12. **Logs:** `erxlog.MsgText` has `***` in place of the SSO codes. No token appears in any log.
13. **Middle tier** (OpenDentalServer): the same flow works, with the decrypt done client-side.
14. **Migrated copy** of an Open Dental database (a copy, never production):
    - Plain-text clinic keys pass through and are encrypted on save.
    - Record what DoseSpot returns for Open Dental-issued keys with our subscription key. A rejection is the expected result.
    - Run these read-only queries:
      ```sql
      SELECT pp.PropertyDesc,pp.ClinicNum,IF(pp.PropertyDesc LIKE '%Key%','(set)',pp.PropertyValue) Val
        FROM programproperty pp JOIN program p ON p.ProgramNum=pp.ProgramNum WHERE p.ProgName='eRx';
      SELECT COUNT(*) FROM oidexternal WHERE rootExternal LIKE '2.16.840.1.113883.3.4337.25128%';
      SELECT EnabledStatus,COUNT(*) FROM clinicerx GROUP BY EnabledStatus;
      ```
15. **Guard:** `tools/Test-NoHqHosts.ps1` passes, and a firewall log shows only DoseSpot hosts.
16. **Production smoke test** after the integration review: one prescriber, one real patient, one non-controlled prescription to a pharmacy the office knows, then verify the sync.

## 7. Size, risks and open questions

**Size: M.**

- The code is about 4 to 6 developer-days:
  - credential storage and setup UI: 1 day
  - REST client rewrite with caching, throttling and errors: 1.5 days
  - local gating and removing HQ, v1 and migration code: 1 day
  - neutrality and toolbar changes: 0.5 day
  - tests: 1 day
  - staging verification: 1 day
- Calendar time depends on DoseSpot. Its own estimate for Jumpstart is up to about 3 months from kickoff to go-live.

**Risks:**

- **Partner-only:**
  - The resource endpoints are publicly specified, but we can't verify the token, header and SSO contract without a DoseSpot login, and we can't run anything without contract credentials.
  - DoseSpot may decline a single practice or an open-source fork, and pricing is unknown.
  - Until a contract exists, the feature stays disabled.
- **Continuity:** the office's existing clinics, clinicians, IDP/EPCS enrollment and prescription history belong to Open Dental's DoseSpot account. Moving them needs DoseSpot's cooperation, and possibly Open Dental's. Never reuse Open Dental-issued keys under another contract.
- **Shared clinic key:**
  - Every workstation must be able to read the clinic key to build SSO codes. So anyone with database read access can open DoseSpot as any user of that clinic, for non-controlled prescribing.
  - `osd1$` is obfuscation only (see `CDT/README.md`).
  - Mitigations: MySQL grants, masking, high-security permission, no keys in logs. EPCS still needs the prescriber's two-factor.
- **Secrets already at rest:**
  - `erxlog.MsgText` holds old SSO query strings.
  - `clinicerx.ClinicKey` and the `programproperty` clinic keys hold plain-text keys from Open Dental.
  - Encrypt or blank them on first save, and decide whether to purge old `erxlog` text.
- **Rate limiting:** today's per-patient-selection polling could trip 429s on a busy day. Throttling is part of this work, not optional.
- **Concurrency bug:** the shared static SSO phrase can be reused across threads and requests. DoseSpot is said to flag reused phrases, which would show up as intermittent SSO or token failures.
- **Token request quirks:** the unencoded form body, the wrong content type and the odd Basic header may only work by accident today.
- **Push notifications** need a public endpoint. We stay with polling, so counts lag by up to the poll interval.
- **OID arc:** the patient-ID lookup key keeps Open Dental's registered OID prefix (`2.16.840.1.113883.3.4337`) for compatibility. It's local only and never sent to DoseSpot.

**Open questions:**

1. Who signs with DoseSpot: the office directly, or a support organization under a Sub-BAA? Will DoseSpot accept either for this software?
2. Will DoseSpot transfer the office's clinics, clinicians, patients and history out of Open Dental's account? Does the office use DoseSpot today? Run the queries in §6, step 14.
3. The exact v2 token contract:
   - Is the Basic header required?
   - Is `scope=api` correct?
   - What is the token lifetime?
   - Is the subscription-key header `Subscription-Key` (in-repo and community clients) or `Ocp-Apim-Subscription-Key` (unverified search summary)?
4. The v2 SSO URL:
   - Is `LoginSingleSignOn.aspx` still right for v2?
   - What does `b=2` mean?
   - Is a phrase-length parameter needed?
   - Is there a time limit or single-use rule on SSO codes?
5. ~~Does Jumpstart include every endpoint in §2.5?~~ **Answered:** yes, the public JumpstartV2 contract has all of them, including `POST /api/clinics`. Still open: may this contract use `POST /api/clinics` at all, and must new clinics still be reported to DoseSpot after creation (a code comment near `DoseSpotV2.cs:1646` points to a DoseSpot notification inbox for that)?
6. What are the rate-limit values, and is there a recommended polling interval for notification counts?
7. Does DoseSpot require its integration review again for this fork before it issues production keys?
8. Could DoseSpot user IDs be looked up by NPI, so offices don't type them in? **Partly answered:** the JumpstartV2 contract has `GET /api/clinician` with `npi` and `dea` query parameters, returning an ID. Still open: whether it only finds clinicians in the token's own clinic, and whether DoseSpot allows using it this way.
9. Should the `DoseSpotWithNewCrop` option survive? Coordinate with the NewCrop spec, which owns `UpdateErxAccess`'s NewCrop branch and `GetErxAccess`.
10. Should the office purge old `erxlog` SSO text from the migrated database?

## References

- DoseSpot API portal (partner login required): https://docs.dosespot.com/ ; https://docs.dosespot.com/staging/docs/getting-started ; https://docs.dosespot.com/staging/docs/api-guide
- DoseSpot Partnership Overview (PDF): https://go.dosespot.com/hubfs/DoseSpot/File%20Downloads/DoseSpot_Partnership%20Overview.pdf
- DoseSpot, Transitioning to API v2 and the Medi-Span drug database: https://dosespot.com/transitioning-to-api-v2-and-the-medi-span-drug-database
- DoseSpot Exhibit C Business Associate Agreement: https://dosespot.com/exhibit-c-business-associate-agreement
- DoseSpot Exhibit C Sub-Business Associate Agreement (PDF): https://dosespot.com/wp-content/uploads/2025/01/Exhibit-C-Sub-Business-Associate-Agreement-to-Client-Service-Agreement.pdf
- DoseSpot public Swagger 2.0 contracts (no login): https://my.dosespot.com/webapi/v2/swagger/docs/JumpstartV2 ; https://my.dosespot.com/webapi/v2/swagger/docs/FullV2 ; staging: https://my.staging.dosespot.com/webapi/v2/swagger/docs/JumpstartV2
- apis.io DoseSpot provider listing (third party): https://apis.io/providers/dosespot/
- autogen-dosespot community Rust client (third party; token URL, `Subscription-Key` header, plan list): https://docs.rs/autogen-dosespot/latest/autogen_dosespot/ ; https://github.com/steventhanna/autogen-dosespot
- Mindbowser DoseSpot integration write-up (third party; older Basic-auth token call, 10-minute staging tokens): https://www.mindbowser.com/?p=53580
- Medplum DoseSpot getting started (third party; uses the v2 base URLs only as identifier-system strings, not as documented endpoints): https://www.medplum.com/docs/integration/dosespot/getting-started
- Akute Health, DoseSpot e-prescribing authorization agreement (third party): https://help.akutehealth.com/article/38-dosespot-e-prescribing-authorization-agreement
- Open Dental manual (public), DoseSpot Setup: https://opendental.com/manual/dosespotsetup.html ; DoseSpot requirements: https://opendental.com/manual/dosespotrequiredinfo.html
- In repo: `OpenDentBusiness/WebBridges/Erx/{DoseSpot,DoseSpotV2,Erx,ErxXml}.cs`, `OpenDental/Main Modules/ControlChart.cs`, `OpenDental/Forms/{FormErx,FormErxSetup,FormDoseSpotProperyEdit,FormPatientEdit,FormProvEdit,FormUserEdit}.cs`, `OpenDentBusiness/WebServices/{CustomerUpdatesProxy,WebServiceMainHQNoHq}.cs`, `OpenDentBusiness/Data Interface/OIDInternals.cs`, `CDT/Class1.cs`, `CDT/README.md`

## Fact-check log (2026-10-18)

An adversarial review re-checked this spec against DoseSpot's public sources and the `main` branch.

**Confirmed:**

- Feasibility is **partner-only**. Credentials come only through a DoseSpot contract and integration specialist.
- The Partnership Overview claims hold: Jumpstart vs Full, 70+ vs 250+ calls, 0 to 3 vs 3 to 6 months, the onboarding steps, `sales@dosespot.com` and dental service organizations as clients.
- The transition notice claims hold: v2 move, new token mechanism, HTTP 429, Medi-Span.
- The BAA and Sub-BAA hold. In the Sub-BAA, the client is the business associate and DoseSpot is the subcontractor ("Vendor").
- Every code touchpoint and line reference exists, apart from the corrections below. The token body, the Basic header (`userId:` plus an SSO code), the content-type bug, the never-firing 24-hour key re-check and the static-phrase reuse after `GetToken` all match the code.
- No `Bridges.dll` type is involved. `DoseSpotService` is in-repo source (`OpenDentBusiness/Web References/DoseSpotService/Reference.cs`).
- The spec compiles in no credentials, sends nothing to Open Dental hosts, and keeps setup and messages neutral.

**Corrected:**

- **Documentation status (§2.1).** DoseSpot publicly serves Swagger contracts per plan from `my.dosespot.com/webapi/v2/swagger/docs/<Plan>`, so the endpoints are publicly specified. Only the auth, SSO and onboarding guides are behind a login. §2.5 is now verified against JumpstartV2.
- **Base URLs (§2.2).** These are now sourced from DoseSpot's own Swagger `host` fields. Medplum only uses the URLs as identifier strings. The v1 "retired" wording is softened to the notice's 2024 deadline.
- **Header name (§2.3).** The `Ocp-Apim-Subscription-Key` claim is downgraded to unverified, and the corroboration for `Subscription-Key` is added.
- **Token lifetime (§2.3).** Added the third-party report of 10 minutes in staging.
- **Endpoints (§2.5).** Added `PendingRxChangeCount` and the optional `GET /api/clinician?npi=` lookup.
- **Prerequisites (§2.5).** The phone rule rejects a 555 *area code* only, not a 555 exchange. Added the never-firing ZIP check.
- **Line references.** The gates are at `ControlChart.cs:8733` and `:8746`, and the toolbar button at `:5024`.
- **Summary.** Added that v1-flagged databases take the dead v1 path.
- **Encryption (§3.2).** Added the warning that `TryEncrypt` and `TryDecrypt` pass plain text or ciphertext through on failure.
- **Open questions (§7).** Questions 5 and 8 are partly answered from the public contract.
