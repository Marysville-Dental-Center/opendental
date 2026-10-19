# PostcardMania (Advertising Postcards): direct-to-vendor spec

Status: draft, 2026-10-18. Fact-checked the same day against the live v3 docs and `main`. Baseline: `main` at `6855026f5`. Vendor: PostcardMania, through its API product **PCM Integrations**. API: **DirectMail API v3**. The v3 docs were last updated 2026-10-13; v2 is legacy.

## Summary

- **A public, self-serve API exists.** PCM Integrations publishes the DirectMail API v3 reference on a public Stoplight site. Any business can sign up for free in the portal and generate its own **API key and secret**.
  - Auth is a plain key-and-secret login. `POST /auth/login` returns a bearer token that lasts 15 minutes.
  - Sandbox keys and production keys use the same host.
  - There is no OAuth, no app review and no partner agreement.
- **What HQ brokered doesn't exist in the public API.** Open Dental's HQ did three things with Open Dental's partner key, none of them public:
  - **Provisioning:** HQ created and renamed PostcardMania accounts for the office, identified by a ServicesHQ GUID and an email.
  - **SSO:** HQ returned a single-sign-on URL into PostcardMania's web app.
  - **List upload:** HQ uploaded a named "patient list" to the account. The office then designed and paid for the mailing inside PostcardMania's web app.
- **The direct design re-shapes the feature around what v3 offers:**
  - **Accounts:** the office signs up itself and enters its key and secret per clinic. Optional child apps (`childRefNbr`) keep designs and orders apart per clinic.
  - **SSO:** replaced by the vendor's documented embedded design editor and gallery URLs. Those URLs carry a short-lived token. An "Open portal" button also opens the vendor portal in the system browser, where the user signs in themselves.
  - **List upload:** v3 has no list resource. It is replaced by **placing a postcard order** (`POST /order/postcard`) for the selected patients, against a design the office picks. Up to 50,000 recipients go in one request. The office sees a price estimate (`POST /order/postcard/pricing`) and confirms first.
- **HIPAA is the gating issue, not the API.** Patient names and addresses sent to a mail house are PHI disclosed to a business associate, so a **BAA** is needed.
  - The PCM Integrations trust center lists SOC 2 Type 1 (in progress) and GDPR (started). Neither the trust center, the product site nor the API terms of service mentions HIPAA or BAAs.
  - So the integration ships **disabled**. "Send" stays locked until the office records that a BAA is on file.
  - Only name and mailing address leave the building. HQ used to receive the full `PatientInfo`: birthdate, email, status and appointment dates.
- **Feasibility: public API.** **Size: M**, about 4 to 6 days plus sandbox time.

## 1. What the integration does, and what HQ provided

### 1.1 What the office uses it for

**Tools > Advertising > Postcards** lets an office mail a marketing postcard to a chosen set of its own patients. Typical uses are reactivation, new-service announcements and seasonal offers. From the code and Open Dental's public manual ([workflow](https://opendental.com/manual/postcardsadvertisingworkflow.html), [setup](https://opendental.com/manual/postcardssetup.html), [upload](https://opendental.com/manual/postcardsupload.html)):

1. **Setup** (`FormAdvertisingPostcardsSetup`):
   - lists the office's "Postcards accounts"
   - lets the user add one (`FormAdvertisingPostcardsAccountSetup`: account title, email, first and last name, phone, mobile)
   - marks one account as default (pref `AdvertisingPostCardGuid`)
   - with clinics on, maps each clinic to an account (clinicpref `AdvertisingPostCardGuid`)
   - shows an HQ-supplied web page (`displayURL`) in an embedded `WebBrowser`
2. **Upload recipients** (`FormAdvertisingPostcardsSend`):
   - pick an account
   - **Select Patients** (`FormAdvertisingPatientList`, filters or a user query) chooses the patients
   - name the list, then **Save/Upload**
   - the list is sent, and an SSO window into the vendor's web app opens (`FormWebView`, single-use URL)
3. **In the vendor's web app** (per the manual), the office:
   - picks or designs a postcard and the quantity
   - can buy a prospect list
   - pays by card at checkout, outside Open Dental billing
4. **Audit:** `SecurityLogs.MakeLogEntry(EnumPermType.Advertising, …)` on each upload and each portal visit. The upload entry is written *before* the upload is tried, so a failed upload still logs. Permission `Advertising` (216) is checked in the menu click handler. The menu item itself is always visible.

### 1.2 What Open Dental's HQ provided

All four calls went through `WebServiceMainHQProxy` (`OpenDentBusiness/WebServices/WebServiceMainHQProxy.cs:1073-1104`, region "Advertising - Postcards"), wrapped by `OpenDentBusiness/Data Interface/AdvertisingPostcards.cs`. Every payload went through `PayloadHelper.CreatePayload`, which adds the office's **registration key, practice title, practice phone and program version**. HQ used those to identify the office and attach Open Dental's PostcardMania partner key. The office never held a PostcardMania credential.

| HQ method (`IWebServiceMainHQ`) | Request (Bridges type) | Response (Bridges type) | Called from | What HQ did |
|---|---|---|---|---|
| `GetAdvertisingPostcardsAccounts` | none (office identity only) | `PostcardManiaMetaData`: `Accounts`, `displayURL`, plus an upload chunk size (per the XML doc comment) | `AdvertisingPostcards.GetPostcardManiaMetaData` → both forms on load | Listed the PostcardMania accounts HQ had created for this registration key |
| `ManageAdvertisingPostcardsAccount` | `PostcardManiaAccountCreateRequest(accountTitle, guid, email, fName, lName, "", company, extRefNum, phone, mobile)` | `PostcardManiaAccountCreateResponse`: `AccountId`, `Company`, `Email` | `AdvertisingPostcards.ManageAccount` ← `FormAdvertisingPostcardsAccountSetup.SaveChanges` | Created a vendor account with the partner key, or renamed one (same guid and email) |
| `GetPostcardManiaSSO` | `PostcardManiaSSORequest(guid, email, "")` | `PostcardManiaSSOResponse.SSO` (URL) | `AdvertisingPostcards.GetSSO` ← Send's `ViewAccount_Click` and `ViewAccount()` (after upload), and `FormAdvertisingPostcardsAccountSetup` (line 99) | Minted a one-time login URL into the vendor's web app |
| `UploadPostcardManiaPatientList` | `PostcardManiaUploadPatientsRequest(guid, listName, email, listPatientData, "")` with full `List<PatientInfo>` | `PostcardManiaUploadPatientsResponse`: `ListID`, `Success` | `AdvertisingPostcards.UploadPatients` ← `FormAdvertisingPostcardsSend.butSave_Click` | Relayed the patient list to the vendor under the account |

`PatientInfo` (`OpenDentBusiness/WebTypes/Shared/PatientInfo.cs`) carries:

- PatNum, names and preferred name
- **birthdate**
- address, **email**, country
- contact method, **patient status**, clinic
- **last and next appointment dates**

The upload request carried the whole `List<PatientInfo>` (the `PayloadItem` serializes the request object), so presumably every one of these fields went to HQ. This is inferred from the call site, since Bridges.dll isn't inspected. `GetListPatientInfos` also adds **one row per email address**, so a patient with two emails was uploaded twice.

### 1.3 State today (after Phase 1)

- `WebServiceMainHQNoHq` lines 74-77: all four methods throw `HqNotAvailableException.ForVendor("PostcardMania")`.
- The menu item is always shown (`FormOpenDentalMenus.cs:385-388`, `437-440`). `menuItemAdvertisingPostcards_Click` (`FormOpenDental.cs:6514-6524`) opens Setup whenever the pref is blank, and Setup shows the error from `RefreshData`. The handler then opens Send regardless, and Send's `FillData` hits the same error.
- Setup still contains promotional UI that ground rule 7 forbids: `labelNotEnabled` ("No accounts added, click here to signup") and the `webBrowser` that navigated to HQ's `displayURL`.
- No `program` row exists for this feature. The only stored state is pref `AdvertisingPostCardGuid`, matching clinicpref rows, and the permission. After a `fox-db` migration, those GUIDs are ServicesHQ identifiers with no meaning here.

## 2. The vendor's public API

### 2.1 Documentation status

- **Public.** The DirectMail API v3 reference, guides and schemas are on [docs.pcmintegrations.com](https://docs.pcmintegrations.com/), which PostcardMania's API site [pcmintegrations.com](https://www.pcmintegrations.com/) links to.
  - The site is a Stoplight project that renders in the browser.
  - The page content is also readable as JSON from the same host's `/api/v1/projects/cHJqOjE3Njk0Ng/nodes/{id}` route. That route is how this spec was researched.
  - A v2 branch still exists. It is legacy, and v3 is the default.
- **Not public:** the partner/reseller API Open Dental used. That covers account creation under a partner, SSO into partner sub-accounts and named list upload. We don't need it.
- **Terms of service:** accepted by click-through at sign-up ([Getting Started](https://docs.pcmintegrations.com/docs/directmail-api/ir9e5i8q5s85t-getting-started)). The text is also public at [portal.pcmintegrations.com/terms](https://portal.pcmintegrations.com/terms), the link in the product site's footer. (`www.pcmintegrations.com/terms-of-service/` is a 404, which is why the first draft missed it.) Points that matter here, summarised (not legal advice):
  - The contracting party is Joy Rockwell Enterprises, Inc., a Florida corporation doing business as PostcardMania. Florida law governs.
  - The vendor says it tries to keep uploaded mailing lists on file for about a year, then stops keeping them. That is a retention period for patient names and addresses, and it belongs in the BAA discussion (section 5).
  - Reselling, sublicensing or redistributing API access is prohibited. Our design fits this: each office signs up for and uses its own key. We never ship a shared key.
  - The terms speak of privacy law only in general. They don't mention HIPAA, PHI or health data.

### 2.2 Base URLs

| Purpose | URL | Source |
|---|---|---|
| API, production **and** sandbox | `https://v3.pcmintegrations.com` | [service definition](https://docs.pcmintegrations.com/docs/directmail-api/84aca6606cef4-direct-mail-api-v3) ("Live Server") |
| Sandbox vs production | chosen by **which API key** you log in with, not by host | [Logging In](https://docs.pcmintegrations.com/docs/directmail-api/ffef03a112bb0-logging-in) |
| Portal (sign-up, API keys, billing, webhooks, settings) | `https://portal.pcmintegrations.com/` | Logging In; [Getting Started](https://docs.pcmintegrations.com/docs/directmail-api/ir9e5i8q5s85t-getting-started) |
| Embedded editor and gallery | `https://portal.pcmintegrations.com/integrated/embed/editor/{designID}?token=…`, `https://portal.pcmintegrations.com/integrated/gallery?token=…` | [Embedded Portal](https://docs.pcmintegrations.com/docs/directmail-api/l4shfk7o4vif4-embedded-portal) |

Sandbox caveat from the docs: sandbox **orders and batches** aren't processed, so nothing is printed or charged. Other calls are **not** sandboxed, though. Deleting a design with a sandbox key deletes the real design.

### 2.3 Authentication, step by step

[Logging In](https://docs.pcmintegrations.com/docs/directmail-api/ffef03a112bb0-logging-in), [Child Apps](https://docs.pcmintegrations.com/docs/directmail-api/tulhk8xls7s4a-child-apps), [Rate Limits](https://docs.pcmintegrations.com/docs/directmail-api/uuk76s7muod83-rate-limits):

1. The office creates API keys in the portal: **My Account > API Keys**. There is a Sandbox section and a Production section, and generating a secret produces a key and secret pair.
2. The client sends `POST https://v3.pcmintegrations.com/auth/login` with JSON `{"apiKey": "...", "apiSecret": "...", "childRefNbr": "..."}`. `childRefNbr` is optional, unique per child account, and at most 50 characters.
3. A 200 response is `{"token": "<JWT>", "expires": "<ISO-8601 date-time>"}`. 401 or 404 means the key or secret is invalid.
4. Every other call sends `Authorization: Bearer <token>`.
5. Tokens are valid for **15 minutes**, and the docs ask clients to reuse them. v3 documents **no refresh endpoint**. The Logging In text mentions a refresh token, but the response schema has only `token` and `expires`. v2 had `/auth/refresh`. To renew, log in again.

No browser, redirect URI, PKCE, device code or consent screen is involved. It works like OAuth client credentials, except the "client" is the office's own account.

### 2.4 Endpoints and fields we need

**v1 (this spec):**

| Call | Use in the app | Key fields |
|---|---|---|
| `POST /auth/login` | Get a token, and **Test connection** | above |
| `GET /integration/balance` ([doc](https://docs.pcmintegrations.com/docs/directmail-api/f2b471cbf9ecb-get-balances)) | Test connection; show the money on account | `moneyOnAccount` (dollars) |
| `GET /design?productType=postcard&page=&perPage=` ([doc](https://docs.pcmintegrations.com/docs/directmail-api/3d3a3000ff275-get-all-designs)) | Design picker | `designID`, `friendlyName`, `size.key` and `size.label`, `mailClasses[]`, `proofFront` and `proofBack` (URLs), `designFields[]` (`fieldKey`, `mandatory`), `approvalDateTime`, `isUpload` |
| `GET /design/{designID}/edit?mode=embed[&duplicate=true]` ([doc](https://docs.pcmintegrations.com/docs/directmail-api/wys5o1p1fiq6g-edit-designer-design)) | **Edit design** in an embedded window (replaces SSO) | returns `designID` and `url`, an editor URL that already carries a token. Only designer designs can be edited. Uploaded designs (`isUpload=true`) can't. A template is always copied, so the returned `designID` can differ from the one requested |
| `POST /design/custom` ([doc](https://docs.pcmintegrations.com/docs/directmail-api/76to9yvq9gav1-new-designer-design)) | **New design** | `name`, `size` (enum `46`, `57`, `58`, `68`, `69`, `611`, `811`, `BRO`; the postcard sizes are `46` through `611`) → `designID`, `url`. Note that `46S` isn't in this enum |
| `POST /recipient/verify` ([doc](https://docs.pcmintegrations.com/docs/directmail-api/k49y925eo4waw-verify-recipients)) | Optional **Check addresses** before sending | body: array of `Address`. Returns `validCount`, `invalidCount`, `results.valid[]`, `results.invalid[]` |
| `POST /order/postcard/pricing` ([doc](https://docs.pcmintegrations.com/docs/directmail-api/ng34lty7kjptz-get-estimated-postcard-order-price)) | **Estimate** shown before confirming | same body as the order → `estimatedTotalPrice`, `lineItems[]` (`name`, `price`, `quantity`, `lineTotal`) |
| `POST /order/postcard` ([doc](https://docs.pcmintegrations.com/docs/directmail-api/24b58db675dad-place-postcard-order)) | **Send** (replaces list upload). This is a purchase | required: `mailClass` (`FirstClass` or `Standard`) and `recipients[]` (1 to 50,000). We also send `designID`, `extRefNbr` (order), `mailDate` (`YYYY-MM-DD`) and `returnAddress`, plus optional `addressing.exceptionalAddressingType` and `globalDesignVariables[]`. Returns 201 with `orderID`, `batchID` and `extRefNbr`. Also 404 (design not found), **409 (duplicate `extRefNbr`)** and 422 (recipient failed verification) |
| `GET /order?extRefNbr=&page=&perPage=` ([doc](https://docs.pcmintegrations.com/docs/directmail-api/fdc898a0bcad2-get-all-orders)) | **Recent orders**, and the idempotency check after a timeout | `orderID`, `batchID`, `status` (`Pending`, `Processing`, `Processed`, `Delivered`, `Undeliverable`, `Canceled`), `orderDate`, `designID`, `extRefNbr` |
| `DELETE /order/{orderID}` ([doc](https://docs.pcmintegrations.com/docs/directmail-api/iuc7kuzptpgwv-cancel-order)) | **Cancel** a pending order | 204 on success, 400 if it can no longer be canceled, 404 for an unknown `orderID`. Cutoff is 11:30 PM Eastern daily, when orders go to press |
| `GET /batch/{batchID}` ([doc](https://docs.pcmintegrations.com/docs/directmail-api/b84f873587f9b-get-batch-by-id)) | Show batch status and payment state | `status`, including `Pending Payment` and `Failed Payment`, which are resolved in the portal. Also `invoiceURL` |

**Recipient fields** ([Recipient model](https://docs.pcmintegrations.com/docs/directmail-api/814247a823488-recipient)): `firstName` and `lastName` (or `company`), `address`*, `address2`, `city`*, `state`*, `zipCode`*, `extRefNbr`, `variables[]` (`key`, `value`). Fields marked * are required. There is **no country field**, so this is US mail.

**Mapping from `PatientInfo`.** We send these fields and nothing else:

| Recipient | Source | Note |
|---|---|---|
| `firstName` | `PatientInfo.FName` (or `Preferred` when the office ticks "Use preferred name") | |
| `lastName` | `PatientInfo.LName` | |
| `address`, `address2` | `Address`, `Address2` | omit `address2` when blank |
| `city`, `state`, `zipCode` | `City`, `State`, `Zip` | |
| `extRefNbr` | blank by default. `"P"+PatNum` only if the office turns on "Send patient reference" | Lets undeliverable reports be matched back to patients. Off by default (minimum necessary) |

**Return address** ([Return Address](https://docs.pcmintegrations.com/docs/directmail-api/7997131f14684-return-address)):

- **With clinics:** the clinic's address.
- **Without clinics:** the practice prefs `PracticeTitle`, `PracticeAddress`, `PracticeAddress2`, `PracticeCity`, `PracticeST`, `PracticeZip`.
- **Why it matters:** the docs note that USPS recommends a return address for First Class mail.

**Postcard sizes**, per the order and design docs: `46S` (4×6) and `46` (4.25×6) can only be mailed First Class. The others are `57` (5×7), `58` (5.5×8.5), `68` (6×8.5), `69` (6×9) and `611` (6×11). Each design's `mailClasses[]` lists the classes it supports, and the Send form should offer only those.

**Later (not v1):**

- suppression list (`GET`, `POST` and `DELETE /suppression-list`, [doc](https://docs.pcmintegrations.com/docs/directmail-api/953lez0oqsza9-get-suppressed-addresses))
- undeliverable recipients (`GET /recipient/undeliverable`, [doc](https://docs.pcmintegrations.com/docs/directmail-api/wg1e1306pkidh-get-undeliverable-recipients))
- drip campaigns (`GET /campaign`, `POST /campaign/{id}/recipients`, [doc](https://docs.pcmintegrations.com/docs/directmail-api/k7p3w1n8d5h2x-add-recipient-to-drip-campaign)). Campaigns themselves are created in the portal.
- QR codes
- purchased-list counts (`/list/count/*`)

**Not used:**

- **Webhooks** ([guide](https://docs.pcmintegrations.com/docs/directmail-api/keltqbg2rolz6-creating-new-webhook), [security](https://docs.pcmintegrations.com/docs/directmail-api/e7k3evogf3sgx-webhook-security)). They need a public HTTPS receiver, which ground rule 6 rules out. Status comes from polling on demand.

### 2.5 Rate limits

From [Rate Limits](https://docs.pcmintegrations.com/docs/directmail-api/uuk76s7muod83-rate-limits). Limits are counted **per integration**: all keys and child apps share them, and every workstation of the office counts together.

| Scope | Limit |
|---|---|
| All endpoints combined | 400 requests per 10 s |
| Order placement and cancellation (each) | 250 per 10 s |
| `POST /recipient/verify` | 150 per 10 s |
| Proofs and list counts (each) | 60 per 60 s |
| `POST /auth/login` | 400 per 60 s **per IP** |
| Anything else (each) | 250 per 10 s |

- The windows are fixed.
- Responses carry `X-RateLimit-Limit`, `X-RateLimit-Remaining` and `X-RateLimit-Reset`, plus `-global` variants.
- A `429` comes with `Retry-After`. Nothing is processed or charged on a 429, so retrying is safe.

Our use is a handful of calls per mailing, far below any of these limits.

### 2.6 Errors

Errors use one shape ([_ApiError](https://docs.pcmintegrations.com/docs/directmail-api/c1d9e32fda2e8-api-error)):

- `error.code` (the HTTP code)
- `error.message`, which is always "One or more errors occurred"
- `error.data[]` with `message` and `property`
- `documentation` (a URL)

We show the `data[].message` values.

### 2.7 Terms that matter

- **No app review or partner agreement** for one office's own use. Sign-up is free, with no setup fee or minimum according to the vendor's site, and pricing is per piece.
- **Payment happens in the portal.** Orders are billed to the account's payment method or its money on account. A batch can stop at `Pending Payment` or `Failed Payment` until someone pays in the portal ([Get All Batches](https://docs.pcmintegrations.com/docs/directmail-api/2af7a5c1f97ee-get-all-batches)).
- **Placing an order is a purchase.** The app must show the estimate and get an explicit confirmation (3.4).
- **Cancel cutoff:** 11:30 PM Eastern each day.
- **NCOA is on by default** ([NCOA](https://docs.pcmintegrations.com/docs/directmail-api/1tizq9hdy44wn-using-the-national-change-of-address-ncoa-feature)). The vendor matches recipients against the USPS change-of-address database and prints the updated address on the card. The office can opt out in three ways: per order (`addressing.exceptionalAddressingType`: `resident`, `occupant` or `business`, which adds an "or Current …" line), by addressing a recipient as "Current Resident", or account-wide in portal settings.
- **Child apps** ([doc](https://docs.pcmintegrations.com/docs/directmail-api/tulhk8xls7s4a-child-apps)) only partly isolate data. Orders, designs and QR codes get a child owner. But list calls such as `GET /order` and `GET /design` still return the parent's full set.
- **HIPAA:**
  - **PHI:** a patient list sent to a mail house is PHI, even when it holds only names and addresses, because being on the list says the person is a patient of the practice. A vendor that receives PHI to perform a service is a **business associate**, and HHS requires a BAA ([HHS: business associates](https://www.hhs.gov/hipaa/for-professionals/privacy/guidance/business-associates/index.html); [45 CFR 164.504(e)](https://www.ecfr.gov/current/title-45/subtitle-A/subchapter-C/part-164/subpart-E/section-164.504)).
  - **Marketing:** HIPAA's definition of marketing excludes a covered entity's own communications about its own health-related services, such as a practice announcing its services to its patients, **unless a third party pays for the communication**. Marketing that does fall under the definition needs written patient authorization ([HHS marketing guidance](https://www.hhs.gov/hipaa/for-professionals/privacy/guidance/marketing/index.html); [45 CFR 164.501](https://www.ecfr.gov/current/title-45/subtitle-A/subchapter-C/part-164/subpart-E/section-164.501), [164.508(a)(3)](https://www.ecfr.gov/current/title-45/subtitle-A/subchapter-C/part-164/subpart-E/section-164.508)).
  - **Vendor posture:** the [PCM Integrations trust center](https://trust.pcmintegrations.com) lists SOC 2 Type 1 (in progress) and GDPR (started). Neither it nor the [API terms](https://portal.pcmintegrations.com/terms) mentions HIPAA or BAAs. Whether the vendor will sign a BAA is **unknown** and has to be asked (section 5).
  - This is not legal advice. The office's privacy officer decides.

## 3. Desktop design

### 3.1 Auth flow on the desktop

**Key-and-secret login, no browser.** `PostcardManiaApiClient.GetToken(account)`:

1. Look in an in-memory cache keyed by (API key, `childRefNbr`). If the cached token's `expires` is more than 60 seconds away, reuse it.
2. Otherwise `POST /auth/login` with the decrypted secret, then cache `token` and `expires`. Take `expires` from the response. Don't hard-code 15 minutes.
3. Calls run inside `UI.ProgressWin` on a worker thread, as the forms do today. A `lock` per cache key stops two threads from logging in at once.

No loopback listener, PKCE or device code is needed, because nobody consents interactively.

### 3.2 Where credentials and settings live

**A new program row**, `ProgramName.PostcardMania`:

- `ProgDesc` is "PostcardMania (PCM Integrations) postcards". It's descriptive, not promotional.
- It is inserted **at runtime** with `Enabled=false` the first time someone opens **Setup > Program Links**.
- It needs no ConvertDatabases step and no schema change.

**Properties** are `programproperty` rows. A row with `ClinicNum=0` is the default. A row for a specific clinic overrides it, read with `ProgramProperties.GetPropValForClinicOrDefault`, the pattern other bridges use. Missing rows are inserted when Setup saves.

| PropertyDesc (constant in `PostcardManiaProgram`) | Value | Stored as |
|---|---|---|
| `API Key` | key from the portal | plain text |
| `API Secret` | secret from the portal | `CDT.Class1.TryEncrypt` (`osd1$…`), `IsMasked=true`. Read with `TryDecrypt` |
| `Environment` | `Sandbox` or `Production`. Must match the key. Drives a red "Sandbox: nothing will be printed or mailed" banner | plain |
| `Child Reference` | optional `childRefNbr`, e.g. `clinic-12` | plain |
| `Default Mail Class` | `FirstClass` (default) or `Standard` | plain |
| `Use Preferred Name` | `0` or `1` | plain |
| `Send Patient Reference` | `0` (default) or `1`. Sends `P{PatNum}` as the recipient `extRefNbr` | plain |
| `BAA On File` | `0` (default) or `1`. **Send is blocked while 0** | plain |
| `BAA Confirmed` | "user, date" text written when the box is ticked | plain |

**Not stored:**

- **Tokens:** kept in memory only, since they last 15 minutes.
- **Design lists and order history:** fetched live.
- **Patient lists:** no copy is kept.

The legacy pref and clinicpref `AdvertisingPostCardGuid` rows are left alone, because ground rule 2 keeps them for schema compatibility. They are no longer read.

Who can change what:

- **Setup:** opened from Program Links, which already requires the `Setup` permission.
- **Send:** requires `Advertising` (216), as today.
- **Secret:** shown masked. Only re-entering it changes it.

A caveat on `osd1$`: CDT/README.md says plainly that it is **obfuscation with a fixed public key**. Anyone with database access and this source can recover the secret. That matches every other stored bridge secret here. Section 7.2 covers it.

### 3.3 Refresh handling

- There is no refresh token, so the client simply **logs in again** whenever:
  - the cached token is within 60 seconds of `expires`
  - or a call returns **401**
- After a 401 the client drops the cached token, logs in once and repeats the call **once**. A second 401 is reported as bad credentials.
- The secret is decrypted only for the login request. The token is never written to disk or to logs.

### 3.4 User flows

**Setup** (`FormAdvertisingPostcardsSetup`, rewritten; opened from Program Links and from the Send window's Setup menu):

- **Enabled** checkbox.
- **Description:** two sentences on what the integration does, with no links. Per ground rule 7 there is no sign-up link, logo, promo page or "learn more". The step-by-step registration instructions live in our docs (section 5), not as buttons.
- **Grid** with one row for **Default** (ClinicNum 0) plus one per clinic when clinics are on:
  - API Key, Secret (masked), Environment, Child Reference
  - "Use default" when a clinic has no rows of its own
- **Test connection** runs login and `GET /integration/balance`, then reports "Connected (Sandbox/Production). Money on account: $x.xx". Nothing else is shown.
- **Defaults:** Default Mail Class, Use Preferred Name, Send Patient Reference.
- **BAA on file** checkbox with the text "A signed business associate agreement with the vendor is on file", stamped with the user and date.
- **Removed:** `webBrowser` and `displayURL`, `labelNotEnabled` ("click here to signup"), the accounts grid, "Add Account", and the clinic-to-account combo grid.

**Send** (`FormAdvertisingPostcardsSend`, rewritten; Tools > Advertising > Postcards):

1. **Clinic.** Pick it from a combo when clinics are on, then resolve that clinic's account, falling back to the default.
2. **Select Patients** (existing `FormAdvertisingPatientList`, with the Postcards filter changes in 3.5).
3. **Design.** A combo filled from `GET /design?productType=postcard`, with the `proofFront` image shown as a preview. Beside it:
   - **Edit design**: `GET /design/{id}/edit?mode=embed&duplicate=…`
   - **New design**: `POST /design/custom`
   Both open the returned URL in `FormWebView` (WebView2) with `IsUrlSingleUse=true`. After the window closes, refresh the design list and select the `designID` the call returned. Editing a template creates a copy, and so does `duplicate=true`. **Edit design** is disabled for uploaded designs (`isUpload`), because the API edits only designer designs. Designs with mandatory `designFields` other than the address fields get a warning, because v1 doesn't fill custom variables.
4. **Mail class**, **mail date** (today or later) and a **return address** preview.
5. **Check addresses** (optional) calls `/recipient/verify` and shows how many addresses are valid and invalid. The user can drop the invalid ones.
6. **Estimate** calls `/order/postcard/pricing` and shows the total and the line items.
7. **Send.** A confirmation dialog states:
   - the number of recipients, the design name and the mail class and date
   - the estimated total
   - the environment
   - "This places a paid order with the vendor"
   The user must click **Place order**. Only then does the app call `POST /order/postcard`.
8. **Result:** order ID(s), batch ID and status. A `SecurityLog` entry (`EnumPermType.Advertising`) records the clinic, design ID, recipient count, estimate, order IDs and environment, with no patient names.
9. **Recent orders** tab: `GET /order` paged, with the status. **Cancel** calls `DELETE /order/{id}`, is enabled for `Pending`, and shows the 11:30 PM Eastern cutoff.
10. **Open vendor portal** opens `https://portal.pcmintegrations.com/` in the **system browser**, with no token, for billing, payment problems, drip campaigns and NCOA settings. The user signs in with their own portal login.

**Menu:** **Tools > Advertising** shows only when `Programs.IsEnabled(ProgramName.PostcardMania)` (ground rule 7).

### 3.5 Recipients and minimum necessary

`AdvertisingPostcards.BuildRecipients(List<PatientInfo>, account)`:

- **One card per patient.** Undo the per-email fan-out from `PatientInfo.GetListPatientInfos` with `DistinctBy(PatNum)`.
- **Household option.** One card per address: case-insensitive match on `Address`, `Address2` and `Zip`, addressed to the guarantor's name. Off by default.
- **US only.** Keep `Country` values that are blank, "US", "USA" or "United States". The API has no country field. Skipped patients are counted and reported.
- **Only the fields in 2.4 are sent.** Birthdate, email, status, contact method and appointment dates never leave the building.
- **Opt-outs.** The schema has no "do not mail" flag (`ContactMethod` has `DoNotCall` but nothing about mail). v1 relies on the existing filters and user query. Open question 7.3.4 proposes a patient-field-based exclusion.
- **More than 50,000 recipients** are split into several orders, with `extRefNbr` suffixes `-1`, `-2`, and so on.

### 3.6 Errors and offline behaviour

- **No background work:** nothing at startup, no timers, no threads. The vendor is contacted only when the user opens the Postcards window or presses a button there. Core functions never touch this code (ground rule 5).

| Condition | Behaviour |
|---|---|
| DNS or TLS failure, no connection, timeout | "Could not reach the postcard service (v3.pcmintegrations.com). Check the internet connection and try again." Nothing is saved. The rest of the app is unaffected |
| 400 or 422 | Show `error.data[].message` (and `property`). For 422 on an order, suggest **Check addresses** |
| 401 on login | "The API key or secret for <clinic> was rejected. Check them in Setup." |
| 401 on another call | Re-login once (3.3), then report as above |
| 404 design | "That design no longer exists on the vendor account." Then refresh the design list |
| **409 duplicate `extRefNbr`** | The order was already placed. Look it up with `GET /order?extRefNbr=` and show it. Never place it again |
| 429 | Wait for `Retry-After` (at most 30 s) and retry, up to 3 times with jitter. Safe, because nothing is charged |
| 5xx, or a timeout **on `POST /order/postcard`** | Don't blindly retry. Each Send generates its `extRefNbr` once (e.g. `ops-{yyyyMMddHHmmss}-{6 random}`) and keeps it until the dialog closes. The app checks `GET /order?extRefNbr=` first, and offers **Retry** only if no order exists. Retrying with the same `extRefNbr` is protected by the 409 |
| `Pending Payment` or `Failed Payment` batch | Show "Payment is needed in the vendor portal", next to the **Open vendor portal** button |
| BAA not confirmed | Send, Check addresses and Estimate are disabled, because each one sends recipient data. Setup explains why |

Middle Tier: like today's `AdvertisingPostcards`, the methods are `Meth.NoCheckMiddleTierRole` and run on the workstation. They read credentials from the program property cache. The application server never contacts the vendor.

### 3.7 Hosts contacted and HQ removal

- `v3.pcmintegrations.com` (API)
- `portal.pcmintegrations.com` (embedded editor and gallery in WebView2; system-browser portal)
- the proof image URLs the API returns in `proofFront` and `proofBack`, loaded for the preview. Their host is whatever the vendor returns, presumably its own storage (open question 7.3.5).

**No Open Dental host and no HQ call remains:**

- The four HQ methods are deleted from the interface, the NoHq stub, the mock and the proxy.
- `Test-NoHqHosts.ps1` keeps passing.
- The API base URL is a constant. It can be overridden through a new `Introspection.IntrospectionEntity.PostcardManiaApiUrl`, which points tests at a local mock server.

## 4. Code changes

### 4.1 OpenDentBusiness

| File | Change |
|---|---|
| `TableTypes/Program.cs` (enum `ProgramName`, line 50+) | Add `PostcardMania`. It is persisted by name in `program.ProgName`, so adding it is safe |
| **New** `Bridges/PostcardMania/PostcardManiaProgram.cs` | PropertyDesc constants (3.2). `EnsureProgram()` inserts the `program` row, `Enabled=false`, if missing, then calls `Signalods.SetInvalid(InvalidType.Programs)` and returns `true`. `DataValid` lives in `WpfControlsOD`, so OpenDentBusiness can't call it. The UI caller can call `DataValid.SetInvalid(InvalidType.Programs)` when the method returns `true`. `GetAccount(long clinicNum)` returns a `PostcardManiaAccount` with clinic, key, decrypted secret, child ref, environment and BAA flag, falling back to ClinicNum 0. `SaveAccount(...)` inserts missing property rows and encrypts the secret. Small helpers `Programs.InsertIfMissing` and `ProgramProperties.InsertIfMissing` could be shared with the other vendor-direct specs (open question 7.3.7) |
| **New** `Bridges/PostcardMania/PostcardManiaApiClient.cs` (+ `IPostcardManiaApiClient`) | `HttpClient` with base URL `Introspection.GetOverride(PostcardManiaApiUrl,"https://v3.pcmintegrations.com")`, following the shape of `Bridges/Pearl/PearlApiClient.cs` (a static `HttpClient` plus Newtonsoft.Json). Set the serializer explicitly for this API: camelCase property names, `NullValueHandling.Ignore`, and unknown fields ignored. Members: `GetToken`, `TestConnection` (login and balance), `GetDesigns(productType,page,perPage)`, `GetDesignEditUrl(designID,duplicate)`, `CreateDesign(name,size)`, `VerifyAddresses(List<Address>)`, `EstimatePostcardOrder(req)`, `PlacePostcardOrder(req)`, `GetOrders(extRefNbr,page,perPage)`, `CancelOrder(orderID)`, `GetBatch(batchID)`. Also handles retries and 429 |
| **New** `Bridges/PostcardMania/PostcardManiaModels.cs` | DTOs: `AuthRequest` and `AuthResponse`, `Address`, `Recipient`, `ReturnAddress`, `DesignVariable`, `AddressingConfig`, `PostcardOrderRequest`, `OrderResponse`, `Order`, `Batch`, `Design` (+ `DesignSize`, `DesignField`), `PricingResponse` (+ line item), `BalanceResponse`, `AddressVerificationResponse`, `PagedResult<T>` (+ `Pagination`), `ApiError` |
| **New** `Bridges/PostcardMania/PostcardManiaException.cs` | `ODException` subclass with `HttpStatusCode`, `List<string> Messages` and `RetryAfterSeconds` |
| **New** `Bridges/PostcardMania/PostcardManiaApiClientMock.cs` | In-memory fake for tests and demo mode, like `PearlApiClientMock` |
| `Data Interface/AdvertisingPostcards.cs` | **Rewrite.** Remove `using Bridges`, the `MassPostcard*Node` constants, `GetPostcardManiaMetaData`, `ManageAccount`, `GetSSO` and `UploadPatients`. Add `GetAccount(clinicNum)`, `GetDesigns(clinicNum)`, `GetDesignEditorUrl(clinicNum,designID)`, `CreateDesign(clinicNum,name,size)`, `BuildRecipients(...)` (3.5), `BuildReturnAddress(clinicNum)`, `VerifyRecipients(...)`, `Estimate(...)`, `PlaceOrder(...)` (chunking, `extRefNbr`, 409 and timeout handling), `GetRecentOrders(...)` and `CancelOrder(...)` |
| `WebServices/IWebServiceMainHQ.cs:92-95` | Delete `GetAdvertisingPostcardsAccounts`, `GetPostcardManiaSSO`, `ManageAdvertisingPostcardsAccount` and `UploadPostcardManiaPatientList` |
| `WebServices/WebServiceMainHQNoHq.cs:74-77` | Delete the four **"not available" stubs** (`VendorNotAvailable("PostcardMania")`) |
| `WebServices/WebServiceMainHQMockDemo.cs:408-432` | Delete the four mock methods (they reference Bridges types) |
| `WebServices/WebServiceMainHQProxy.cs:1073-1104` | Delete region "Advertising - Postcards" |
| `Web References/WebServiceMainHQ/Reference.cs`, `.wsdl` | Leave as generated. The SOAP proxy methods become unreferenced and go with the Phase 3 web-reference cleanup |
| `Misc/Introspection.cs` (enum `IntrospectionEntity`) | Add `PostcardManiaApiUrl` |
| `OpenDentBusiness.csproj` | Add the new files |

### 4.2 OpenDental

| File | Change |
|---|---|
| `Forms/FormAdvertisingPostcardsSetup.cs`, `.Designer.cs`, `.resx` | Rewrite as the credentials and settings form in 3.4. Drop `_postcardManiaMetaData`, `RefreshData` (HQ), `FillAccountsGrid`, `FillClinicGrid` (combo mapping), `ManageMassPostcardAccount`, `gridAccounts_*`, `gridClinics_CellSelectionCommitted`, `webBrowser`, `labelNotEnabled` and `label2` |
| `Forms/FormAdvertisingPostcardsAccountSetup.*` | **Delete**. The API has no account creation. Remove `OpenDental.csproj` lines 1027-1031 and 7273-7274 |
| `Forms/FormAdvertisingPostcardsSend.cs`, `.Designer.cs`, `.resx` | Rewrite per 3.4. Drop `_postcardManiaMetaData`, `FillData` (HQ), both `ViewAccount` variants and `GetSSO` use. Replace `butViewAccount` with Edit/New design and Open vendor portal. Turn `butSave` into Estimate and Send. Add the Recent orders grid |
| `Forms/FormAdvertisingPatientList.cs:169-174` | Postcards filter: also skip non-US `Country` (3.5). MassEmail is unchanged |
| `Forms/FormProgramLinks.cs` | In `FillList()` (line 26), call `PostcardManiaProgram.EnsureProgram()` before `Programs.RefreshCache()`. In the double-click switch, add `case nameof(ProgramName.PostcardMania):` to open `FormAdvertisingPostcardsSetup` |
| `Main Modules/FormOpenDentalMenus.cs:385-388` | Keep the Advertising submenu in a field (`_menuItemAdvertising`) |
| `Main Modules/FormOpenDental.cs` | In the `InvalidType.Programs` region (~line 1844), set `_menuItemAdvertising.Available=Programs.IsEnabled(ProgramName.PostcardMania)`, next to the CareCredit line (1849). The CareCredit line uses `Programs.GetCur(...).Enabled`. Use the null-safe `Programs.IsEnabled` here instead, because the PostcardMania row doesn't exist until Program Links has been opened once. `menuItemAdvertisingPostcards_Click` (6514): drop the `AdvertisingPostCardGuid` check. If no account has an API key, open Setup when the user has `Setup`, otherwise say so. Then open Send |
| `Forms/FormWebView.cs` | No change. It is already WebView2 with single-use URLs |

### 4.3 Tests

- `NoHqTests/NoHqTests.cs:65`: remove `GetPostcardManiaSSO` from the vendor-call list, since the member no longer exists.
- New tests, see 6.3.

### 4.4 Bridges.dll types: what this repo calls

This repo uses eight `PostcardMania*` types from the prebuilt `Bridges.dll`. The surface below is inferred from call sites only:

| Type | Members this repo uses | Where |
|---|---|---|
| `PostcardManiaMetaData` | `new()`; `Accounts` (`List<PostcardManiaAccountData>`: `Insert`, `First`, `FindIndex`, `Select`, `Where`, `IsNullOrEmpty`); `displayURL` (string). The XML-doc mentions a chunk size, but nothing references it | AdvertisingPostcards, HQ proxy, mock, Setup, Send |
| `PostcardManiaAccountData` | `new(string,string,string)` (Setup passes "Default" three times); `Guid`, `Email`, `AccountTitle` | Setup, Send, AccountSetup |
| `PostcardManiaAccountCreateRequest` | `new(accountTitle, guid, email, fName, lName, string, company, extRefNum, phone, mobile)`. The sixth argument is always "" | AdvertisingPostcards (constructs), proxy (parameter) |
| `PostcardManiaAccountCreateResponse` | `new()`; `AccountId` (assigned an int), `Company`, `Email`. Only the mock sets these. AccountSetup discards the return value | AdvertisingPostcards (return type), mock, proxy |
| `PostcardManiaSSORequest` | `new(guid, email, string)` | AdvertisingPostcards (constructs), proxy (parameter) |
| `PostcardManiaSSOResponse` | `new()`; `SSO` | AdvertisingPostcards, mock, proxy |
| `PostcardManiaUploadPatientsRequest` | `new(guid, listName:, email:, listPatientData: List<PatientInfo>, string)` | AdvertisingPostcards (constructs), proxy (parameter) |
| `PostcardManiaUploadPatientsResponse` | `new()`; `ListID` (assigned an int), `Success` (bool). Only the mock sets these, and no caller reads them | AdvertisingPostcards (return type), mock, proxy |

**Recommendation: re-implement none of them under the same names.**

- All callers are in this repo and get rewritten in the same change.
- The types describe HQ's partner protocol: a GUID-and-email account identity, an SSO string and a named list. None of that exists in the public API.
- A drop-in copy would keep dead, misleading fields.
- The new `OpenDentBusiness.PostcardMania` DTOs (4.1) replace them:
  - `PostcardManiaAccountData` becomes `PostcardManiaAccount`
  - `UploadPatientsRequest` and `UploadPatientsResponse` become `PostcardOrderRequest` and `OrderResponse`
  - the SSO types become `GetDesignEditUrl`
  - `MetaData` and the AccountCreate types are dropped
- After this change no file references a `PostcardMania*` Bridges type. Six files have `using Bridges;` today: `AdvertisingPostcards.cs`, the three `FormAdvertisingPostcards*` forms, `WebServiceMainHQProxy.cs` and `WebServiceMainHQMockDemo.cs`. The import goes away from the first four, with AccountSetup deleted outright. For the proxy and the mock, remove the import only if the build shows nothing else in them still needs it. We can't tell from source which other types in those files come from Bridges.dll. That helps the planned Bridges.dll removal, which still waits on CareCredit, QuickBooks Online, Microsoft 365 and USPS.

**If the owner wants a smaller first step instead,** the minimum compatible shim is `PostcardManiaMetaData` and `PostcardManiaAccountData`, as plain in-repo classes with the members above, so Setup and Send compile unchanged. But Setup and Send need rewriting for the new flow anyway, so the shim saves little.

### 4.5 "Not available" stubs replaced

`WebServiceMainHQNoHq.GetAdvertisingPostcardsAccounts`, `GetPostcardManiaSSO`, `ManageAdvertisingPostcardsAccount` and `UploadPostcardManiaPatientList` are deleted, together with their interface members. `HqNotAvailableException.ForVendor("PostcardMania")` disappears with them. The callers now use `PostcardManiaApiClient` through `AdvertisingPostcards`.

## 5. Credentials the office must register

1. **Privacy decision first** (privacy officer, or counsel):
   1. Confirm the planned mailings are the practice's own communications about its own services, with no third party paying. If a third party pays, written patient authorization is required ([HHS marketing guidance](https://www.hhs.gov/hipaa/for-professionals/privacy/guidance/marketing/index.html)).
   2. Ask the vendor, through [pcmintegrations.com/contact](https://www.pcmintegrations.com/contact/), whether **PostcardMania / PCM Integrations will sign a HIPAA BAA**. Also ask whether the signing entity is Joy Rockwell Enterprises, Inc. (d/b/a PostcardMania), the contracting party in the terms. Ask which subcontractors handle recipient data, how breaches are notified, and whether order recipients can be deleted sooner than the roughly one-year list retention the terms describe. Their [trust center](https://trust.pcmintegrations.com) doesn't cover HIPAA.
   3. **Without a signed BAA, stop here.** The integration stays disabled. Don't use real patient data even in sandbox, because sandbox requests still send it to the vendor.
2. **Create the vendor account** at [portal.pcmintegrations.com/sign-up](https://portal.pcmintegrations.com/sign-up) ([Getting Started](https://docs.pcmintegrations.com/docs/directmail-api/ir9e5i8q5s85t-getting-started)). Registration takes a login email, the office's contact details and a password, and ends with the click-through terms. Read the [terms of service](https://portal.pcmintegrations.com/terms) beforehand. They're public, and 2.1 summarises them.
3. **Billing:** add a payment method, or money on account, in the portal. Otherwise batches stop at `Pending Payment`.
4. **API keys:** go to the portal's **My Account > API Keys** ([Logging In](https://docs.pcmintegrations.com/docs/directmail-api/ffef03a112bb0-logging-in)).
   - Generate a **Sandbox** key and secret first.
   - Generate a **Production** key and secret after testing.
   - Store them in the office password manager, since the secret may be shown only once.
5. **Several clinics, choose one model:**
   - (a) **One vendor account.** Give each clinic a `Child Reference`, such as `clinic-<ClinicNum>`. Billing is shared, and designs and orders are owned per clinic.
   - (b) **A separate vendor account per clinic**, each with its own keys and billing.
6. **Optional portal settings:**
   - NCOA on or off and the exceptional-addressing default ([NCOA](https://docs.pcmintegrations.com/docs/directmail-api/1tizq9hdy44wn-using-the-national-change-of-address-ncoa-feature))
   - one or more approved postcard designs
7. **In the app:** go to **Setup > Program Links > PostcardMania**.
   1. Tick **Enabled**.
   2. Enter the key, secret, Environment and Child Reference for Default and for each clinic.
   3. Click **Test connection**.
   4. Once the BAA is signed, tick **BAA on file**.
8. **Not needed:** webhooks, raised rate limits, any partner agreement, or anything from Open Dental.

## 6. Test plan

### 6.1 Sandbox setup

- Use a dev database from `tools/New-DevDatabase.ps1`, with **synthetic patients only**. Give them valid-format US addresses, such as the office's own address, two with the same household, one with two emails, one non-US and one with a missing zip.
- Create a free vendor account and generate a **Sandbox** key. Template designs come with a new account.
- Set Environment to Sandbox and check that the banner shows.
- Don't delete designs while testing: design deletion isn't sandboxed (2.2).

### 6.2 Manual checks

1. With the program disabled, **Tools > Advertising** is hidden. Enable it in Program Links and the submenu appears with no restart (signal refresh).
2. The `program` row is created once. Open Program Links on two workstations at once, then check `SELECT * FROM program WHERE ProgName='PostcardMania'` returns one row.
3. Setup shows no links, logos or sign-up text. The secret round-trips as `osd1$…` in `programproperty` and is masked in the UI.
4. **Test connection:** a good key reports "Connected (Sandbox)" and the balance. A wrong secret gives the "rejected" message. Remove the network (or block `v3.pcmintegrations.com` in the hosts file) and the unreachable message appears with no hang.
5. Send is disabled until **BAA on file** is ticked. The `BAA Confirmed` value records the user and date.
6. Select Patients sends one recipient per patient, and the two-email patient appears once. The non-US patient is skipped and counted. Household option on: the two same-household patients give one card.
7. The design picker lists the templates and shows the proof image. **Edit design** opens the embedded editor in WebView2. Close it, then check the token URL isn't written to the log or the clipboard. **New design** creates one.
8. **Check addresses** reports how many are valid and invalid. The missing-zip patient never reaches the API, because the local filter drops it.
9. **Estimate** shows a total and line items.
10. **Send:** the confirmation shows the count, design, class, date, estimate and "Sandbox". After placing, the order and batch IDs show. Then:
    - `GET /order?extRefNbr=…` finds the order.
    - The `SecurityLog` entry has no patient names.
    - Sandbox orders aren't processed (2.2).
11. **Idempotency:** simulate a timeout on order placement (mock or proxy) and check that the app finds the existing order instead of placing a second one. Force the same `extRefNbr` and the 409 path reports "already placed".
12. **Cancel** a Pending sandbox order and the status changes to Canceled.
13. **Clinic override:** a clinic with its own key uses it, and one without falls back to Default. With a Child Reference set, the new design belongs to the child.
14. **Token expiry:** wait more than 15 minutes with the window open, then Estimate. The app logs in again without showing an error.
15. **Middle Tier client:** the same flow works and the server makes no vendor calls.
16. **Offline day:** with the vendor host blocked, normal charting, scheduling and billing show no errors (ground rule 5).
17. `tools/Test-NoHqHosts.ps1` passes. A grep for `GetPostcardManiaSSO|UploadPostcardManiaPatientList|PostcardManiaMetaData` finds no live code.
18. **Production smoke test, after the BAA:** a production key, one recipient (the office's own address) and the smallest card. Cancel it before 11:30 PM Eastern, or let it mail and check delivery.

### 6.3 Automated tests (no network)

All of these run against a fake `HttpMessageHandler` or `PostcardManiaApiClientMock`:

- **Serialization:** `PostcardOrderRequest` serializes to the documented camelCase names. Null optionals are omitted, and `mailDate` is formatted as `yyyy-MM-dd`.
- **Recipients:** `BuildRecipients` dedupes, filters non-US, maps the minimum fields, and uses `Preferred` only when the setting is on.
- **Chunking:** 50,001 recipients give two orders, with `-1` and `-2` suffixes.
- **Token cache:** a token is reused before `expires - 60 s` and renewed after. A 401 leads to one re-login, then one retry.
- **429:** the client honours `Retry-After`, caps the wait and stops after 3 tries.
- **Error parsing:** `_ApiError` becomes `PostcardManiaException.Messages`.
- **Timeouts:** a timeout on place-order triggers the lookup by `extRefNbr` and never a second POST.
- **Program row:** `EnsureProgram` inserts once and is a no-op afterwards. `GetAccount` falls back to the default.
- **Secret round-trip:** `osd1$` encrypts and decrypts, and a blank secret stays blank.

## 7. Size, risks and open questions

### 7.1 Size: M (about 4 to 6 days, plus sandbox time and the BAA wait)

- API client, models, mock and retry and token handling: ~1.5 days
- program row, properties and the Setup form: ~1 day
- the Send form rewrite (designs, estimate, confirm, orders, cancel): ~1.5 days
- HQ clean-up, menu gating and the patient-list filter: ~0.5 day
- tests and the manual pass: ~1 day

### 7.2 Risks

- **BAA availability (could block the whole feature).** The vendor's public material doesn't mention HIPAA. If it won't sign, the code can ship but stays disabled.
- **Real money.** Every Send is a purchase billed to the vendor account. Mitigations:
  - the estimate plus an explicit confirmation
  - the sandbox banner
  - the cancel window
  - `extRefNbr` idempotency
  - Send held behind the `Advertising` permission
- **Workflow change.** The old flow uploaded a named list and finished in the vendor's web app, with prospect lists and add-ons. The new flow sends a finished order from the app. Prospect-list purchases, add-ons and drip campaigns stay in the vendor portal.
- **Token in URL.** The embedded editor and gallery are the vendor's documented approach, and their URL carries a 15-minute bearer token. Mitigations:
  - open them only in in-process WebView2 with `IsUrlSingleUse`
  - never send them to `Process.Start` or the system browser
  - never log them
- **`osd1$` is obfuscation only** (fixed key, per `CDT/README.md`). Anyone with database access and the source can recover the API secret, and with it place orders on the vendor account. This is the same exposure as every other bridge secret. Keep Setup and database access restricted. Stronger per-install key storage is a separate, cross-cutting decision.
- **Sandbox isn't complete.** Design deletes and renames act on production data even with a sandbox key.
- **Child-app isolation is partial.** `GET /order` and `GET /design` return the parent's records across children. So the Recent orders grid filters by `extRefNbr` prefix and clinic, and design lists may show other clinics' designs.
- **API drift.** The v3 docs changed as recently as 2026-10-13, and some endpoints are BETA. Mitigations: tolerant JSON with unknown fields ignored, all DTOs in one file, and a mock for tests.
- **No country field.** The vendor mails US addresses only. Foreign addresses are filtered out.
- **No local opt-out flag.** Until 7.3.4 is settled, the office has to exclude opt-outs through filters or queries.
- **Program-row insertion race.** Two workstations can insert the row at the same time. `EnsureProgram` re-reads after inserting and deletes any duplicate with the higher `ProgramNum`.

### 7.3 Open questions

1. **BAA.** Will PostcardMania or PCM Integrations sign one, and which entity? This must be answered before production use.
2. **Terms of service.** The public [terms](https://portal.pcmintegrations.com/terms) don't forbid a dental office mailing its own patients through its own key. They do describe keeping mailing lists for about a year, and they don't mention HIPAA. Counsel should confirm that a BAA would override those terms where they conflict.
3. **Verify limits.** Is there a maximum array size for `/recipient/verify`? It isn't documented. We'll chunk at 1,000 until we know.
4. **Opt-outs.** How should the office mark a patient "no marketing mail"? Options:
   - a `PatFieldDef` (data only, no schema change) that the Postcards filter excludes
   - the vendor suppression list (`POST /suppression-list`), which sends the opted-out address to the vendor, so it's less private
5. **Proof image host.** Which host serves `proofFront`? If it isn't a vendor domain, show the preview only on request.
6. **Mail classes.** Should `Standard` be offered? The docs list it but say nothing about minimums or pricing. Default to `FirstClass`.
7. **Shared helpers.** `Programs.InsertIfMissing` and `ProgramProperties.InsertIfMissing` should be shared with the other vendor-direct specs that add program rows at runtime. Agree on them once.
8. **`fox-db`.** On the restored copy, check for a stray program row (`SELECT ProgName FROM program WHERE ProgName LIKE '%Postcard%'`). None is expected, because Open Dental used prefs only.
9. **Undeliverable reports.** With "Send patient reference" on, should undeliverable recipients be surfaced so staff can fix addresses? That would read `GET /recipient/undeliverable`. It's out of scope for v1.
10. **Mass Email.** Tools > Advertising had only Postcards in this build, and Mass Email is an HQ eService outside this spec. Should the Advertising submenu be renamed to "Postcards" once it holds a single item?

## References

PCM Integrations (PostcardMania) DirectMail API v3. The pages render in the browser at these URLs:

- Product site: https://www.pcmintegrations.com/ · portal: https://portal.pcmintegrations.com/ · sign-up: https://portal.pcmintegrations.com/sign-up · terms of service: https://portal.pcmintegrations.com/terms · contact: https://www.pcmintegrations.com/contact/ · trust center: https://trust.pcmintegrations.com
- Getting Started: https://docs.pcmintegrations.com/docs/directmail-api/ir9e5i8q5s85t-getting-started
- Service definition (server, bearer security): https://docs.pcmintegrations.com/docs/directmail-api/84aca6606cef4-direct-mail-api-v3
- Logging In: https://docs.pcmintegrations.com/docs/directmail-api/ffef03a112bb0-logging-in
- Rate Limits: https://docs.pcmintegrations.com/docs/directmail-api/uuk76s7muod83-rate-limits
- Child Apps: https://docs.pcmintegrations.com/docs/directmail-api/tulhk8xls7s4a-child-apps
- Embedded Portal: https://docs.pcmintegrations.com/docs/directmail-api/l4shfk7o4vif4-embedded-portal · Advanced options: https://docs.pcmintegrations.com/docs/directmail-api/y9aq55ohkl4ur-advanced-embedded-options
- Place Postcard Order: https://docs.pcmintegrations.com/docs/directmail-api/24b58db675dad-place-postcard-order · Estimated price: https://docs.pcmintegrations.com/docs/directmail-api/ng34lty7kjptz-get-estimated-postcard-order-price
- Get All Orders: https://docs.pcmintegrations.com/docs/directmail-api/fdc898a0bcad2-get-all-orders · Cancel Order: https://docs.pcmintegrations.com/docs/directmail-api/iuc7kuzptpgwv-cancel-order
- Get All Batches: https://docs.pcmintegrations.com/docs/directmail-api/2af7a5c1f97ee-get-all-batches · Get Batch by ID: https://docs.pcmintegrations.com/docs/directmail-api/b84f873587f9b-get-batch-by-id
- Designs: https://docs.pcmintegrations.com/docs/directmail-api/3d3a3000ff275-get-all-designs · Edit: https://docs.pcmintegrations.com/docs/directmail-api/wys5o1p1fiq6g-edit-designer-design · New: https://docs.pcmintegrations.com/docs/directmail-api/76to9yvq9gav1-new-designer-design
- Verify Recipients: https://docs.pcmintegrations.com/docs/directmail-api/k49y925eo4waw-verify-recipients · Recipient model: https://docs.pcmintegrations.com/docs/directmail-api/814247a823488-recipient · Return Address: https://docs.pcmintegrations.com/docs/directmail-api/7997131f14684-return-address · Error model: https://docs.pcmintegrations.com/docs/directmail-api/c1d9e32fda2e8-api-error
- Balances: https://docs.pcmintegrations.com/docs/directmail-api/f2b471cbf9ecb-get-balances · Suppression list: https://docs.pcmintegrations.com/docs/directmail-api/953lez0oqsza9-get-suppressed-addresses · Undeliverable: https://docs.pcmintegrations.com/docs/directmail-api/wg1e1306pkidh-get-undeliverable-recipients
- NCOA: https://docs.pcmintegrations.com/docs/directmail-api/1tizq9hdy44wn-using-the-national-change-of-address-ncoa-feature
- Webhooks (not used): https://docs.pcmintegrations.com/docs/directmail-api/keltqbg2rolz6-creating-new-webhook · https://docs.pcmintegrations.com/docs/directmail-api/e7k3evogf3sgx-webhook-security
- Drip campaigns (later): https://docs.pcmintegrations.com/docs/directmail-api/k7p3w1n8d5h2x-add-recipient-to-drip-campaign

HIPAA:

- HHS, business associates: https://www.hhs.gov/hipaa/for-professionals/privacy/guidance/business-associates/index.html
- HHS, marketing: https://www.hhs.gov/hipaa/for-professionals/privacy/guidance/marketing/index.html
- 45 CFR 164.501 (definitions, including marketing): https://www.ecfr.gov/current/title-45/subtitle-A/subchapter-C/part-164/subpart-E/section-164.501
- 45 CFR 164.504(e) (business associate contracts): https://www.ecfr.gov/current/title-45/subtitle-A/subchapter-C/part-164/subpart-E/section-164.504
- 45 CFR 164.508(a)(3) (authorization for marketing): https://www.ecfr.gov/current/title-45/subtitle-A/subchapter-C/part-164/subpart-E/section-164.508

Open Dental public manual. Used to describe the old workflow only:

- https://opendental.com/manual/postcardsadvertisingworkflow.html
- https://opendental.com/manual/postcardssetup.html
- https://opendental.com/manual/postcardsupload.html

In-repo:

- `OpenDentBusiness/Data Interface/AdvertisingPostcards.cs`
- `OpenDentBusiness/WebServices/WebServiceMainHQProxy.cs` (1073-1104), `WebServiceMainHQNoHq.cs` (74-77), `IWebServiceMainHQ.cs` (92-95), `WebServiceMainHQMockDemo.cs` (408-432)
- `OpenDental/Forms/FormAdvertisingPostcards{Setup,Send,AccountSetup}.cs`, `FormAdvertisingPatientList.cs`
- `OpenDentBusiness/WebTypes/Shared/PatientInfo.cs`
- `CDT/Class1.cs`, `CDT/README.md`
- `D:/Dev/opendental-schema/hq-vendor-table.md`
- `docs/decoupling-plan.md`
