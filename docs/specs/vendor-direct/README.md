# Vendor-direct integrations: overview and build plan

Status: draft, 2026-10-18. Baseline: `main` at `6855026f5`. This page sums up the 14 specs in this folder and turns them into one plan. Each spec has the detail, the citations and the test plan for its vendor. Where this page and a spec disagree, the "Decisions that override the specs" list in section 5 says which one wins.

| Spec | Vendor(s) |
|---|---|
| [quickbooks-online.md](quickbooks-online.md) | Intuit QuickBooks Online (deposits) |
| [microsoft-365.md](microsoft-365.md) | Microsoft Entra ID and Microsoft Graph (email) |
| [google.md](google.md) | Google OAuth and the Gmail API (email) |
| [dropbox.md](dropbox.md) | Dropbox API v2 (A to Z image storage) |
| [usps.md](usps.md) | USPS Addresses API v3 (address check) |
| [carecredit.md](carecredit.md) | Synchrony / CareCredit (patient financing) |
| [postcardmania.md](postcardmania.md) | PostcardMania, PCM Integrations DirectMail API v3 (postcards) |
| [dosespot.md](dosespot.md) | DoseSpot eRx (Medi-Span inside it) |
| [newcrop.md](newcrop.md) | Ensora (NewCrop) eRx, plus the OID root |
| [payconnect.md](payconnect.md) | DentalXChange PayConnect 2 (and the VeriFone / PayConnect 1.0 path) |
| [paysimple.md](paysimple.md) | PaySimple API 4 (cards and ACH) |
| [edgeexpress.md](edgeexpress.md) | Global Payments OpenEdge EdgeExpress (hosted pay page) |
| [pdmp.md](pdmp.md) | Bamboo Health PMP Gateway, LogiCoy PMPlink (prescription monitoring) |
| [canada.md](canada.md) | CDA ITRANS 2.0 carrier list, CCDWS network certificates |

The ground rules that shape every spec (from `docs/decoupling-plan.md`):

- **Optional and off by default (rule 6).** Every integration is a program link with `Enabled=0`. Its buttons, menus and background work appear only after the office turns it on.
- **Vendor-neutral (rule 7).** Setup screens describe what the integration does, and nothing more: no logos, sign-up links, referral URLs or "recommended" defaults.
- **The office's own credentials.** Nothing is compiled in or committed. Credentials live in `programproperty` rows (per-mailbox email tokens in the existing `emailaddress` columns), with secrets as `osd1$` values. Missing rows are inserted at runtime. There are no schema changes and no ConvertDatabases steps.
- **Never an Open Dental host.** Every call goes from the workstation to the vendor. `tools/Test-NoHqHosts.ps1` keeps passing.

---

## 1. Summary table

Feasibility uses three values:

- **Public:** the API is documented in public, and the office can get credentials itself.
- **Public, gated:** the API is public, but production access needs the vendor's approval, a licence or payment.
- **Partner-only:** the API, sandbox or credentials come only under a partner, reseller or certification agreement.

Sizes are developer time for the code. They don't include the vendor's onboarding time, which for the partner-only vendors runs from weeks to months.

| Vendor | Feasibility | Size | Auth flow | Partner agreement? | BAA? |
|---|---|---|---|---|---|
| QuickBooks Online | Public, gated (production keys need Intuit's app questionnaire) | **M**, 5-7 d | OAuth 2.0 authorization code with client secret. System browser. Loopback redirect for sandbox keys only; production needs an HTTPS redirect, so the user pastes the redirected address (or an office-hosted relay page forwards it to loopback). Refresh token rotates about daily, so writes use compare-and-swap | No. Developer Terms and an EULA/privacy URL for the office's private app | **Not available** (Intuit doesn't sign one). Send no PHI: only amounts, dates, memo and account IDs |
| Microsoft 365 email | Public | **M**, 6-8 d | OAuth 2.0 authorization code with PKCE, public client (no secret), through MSAL.NET. System browser, `http://localhost` loopback. Device code as an opt-in fallback | No (single-tenant app in the office's own tenant) | **Yes, Microsoft's** HIPAA BAA, included in the Data Protection Addendum; the office confirms and files it |
| Google (Gmail) email | Public | **M**, 4-5 d | OAuth 2.0 installed-app flow: system browser, loopback on `127.0.0.1` and a random port, PKCE S256, client secret (Desktop client). One refresh token per address | No. Workspace "Internal" audience avoids verification; consumer accounts get the unverified-app screen | **Yes, Google Workspace** BAA, accepted in the Admin console. Consumer @gmail.com can't be covered |
| Dropbox (A to Z storage) | Public | **M**, 4-6 d | OAuth 2.0 code with PKCE, no app secret. Paste-the-code flow by default; fixed-port loopback optional. Long-lived refresh token, not rotated | No (development-status app). Developer Terms §2.6(c) needs the owner's review | **Yes, Dropbox** BAA on a team plan, signed before any upload |
| USPS address check | Public, gated (licence agreement, payment account and USPS approval since August 2026) | **S**, 2-3 d | OAuth 2.0 client credentials (consumer key and secret). No browser. Token lasts about 8 hours, memory only | Licence agreement and payment account, not a partner programme | **Not offered.** Only street, unit, city, state and ZIP are sent; privacy officer signs off |
| CareCredit (Synchrony) | **Partner-only** (API reference, sandbox hosts, partner code and production approval are behind a login) | **L**, 15-20 d to implement; **S** to remove | OAuth client credentials (B2B keys); details in the gated guide. Hosted pages opened from prefill URLs. Polling instead of webhooks | **Yes:** Direct Integration Partner, own partner code, Developer Agreement approval | Ask Synchrony; privacy officer decides |
| PostcardMania | Public | **M**, 4-6 d | API key and secret, `POST /auth/login` returns a 15-minute bearer token. No browser | No (free self-service sign-up) | **Yes, required** (patient names and addresses go to a mail house). Vendor's public material doesn't mention HIPAA; Send stays blocked until "BAA on file" is ticked |
| DoseSpot eRx | **Partner-only** (contract, integration review, Surescripts certification; resource endpoints are public Swagger, auth and SSO aren't) | **M**, 4-6 d code; months of onboarding | Password-style token grant from static contract values (subscription key, clinic ID and key, user ID). SSO URL computed locally into DoseSpot's own UI in WebView2 | **Yes:** DoseSpot Client Service Agreement | **Yes:** DoseSpot Exhibit C BAA (or a Sub-BAA through a support organization) |
| NewCrop (Ensora) eRx | **Partner-only** (no public docs or sign-up; reseller or partner agreement) | **M**, 3-5 d (plus OID root **S**, 1 d, doable now) | Static partner triplet (partner name, account name, password) plus account and site ID, in every SOAP call and click-through post. No tokens | **Yes:** Ensora directly or an authorized reseller | **Yes**, with Ensora or the reseller |
| PayConnect 2 | Public, gated (public OpenAPI and sandbox; issuing a production integrator key is unpublished and at the vendor's discretion) | **M**, 4-6 d; removing PayConnect 1.0 / VeriFone is another **M** | Two static header keys: integrator `API-Key` and merchant `Secret`. No tokens | Possibly: ask whether a practice may hold its own production integrator key | Ask (the `cardHolder` field carries the patient's name) |
| PaySimple | Public, gated (API Terms allow PaySimple to review the integration before production) | **M**, 3-5 d | Static `basic user:key` header (replaces the HMAC scheme). ACH status by local polling instead of HQ's webhook relay | No (merchant key); ask partner support whether a review applies | Not expected (payment-processing exemption, HIPAA §1179); office's counsel confirms |
| EdgeExpress | **Partner-only** (documentation confidential; written permission for a non-certified fork needed) | **M**, about 4 d | Static merchant credentials (X-Web ID, Terminal ID, Auth Key) in XML. Hosted pay page in WebView2; return URL intercepted in-app or caught by a loopback listener | Written OK from OpenEdge, possibly partner registration or certification | Not needed for card processing (§1179) |
| PDMP (Bamboo, LogiCoy) | **Partner-only** (Bamboo onboards EHR vendors after a demo; LogiCoy registers EHR products) | **M**, 5-7 d code; months of onboarding | Bamboo: mutual TLS with a client certificate plus per-request digest headers. LogiCoy: HTTP Basic over TLS. No tokens | **Yes:** become a recognized PMP Gateway / LogiCoy integrator, then each office registers in Customer Connect | Ask Bamboo (BAA or data-use agreement); state PMP agreements per office |
| Canada (ITRANS, CCDWS) | Carrier list: public, local only. Live claims: **partner-only** (CDAnet certification; network vendor certificates for hygienist claims) | **S**, about 2 d (0.5 d if US-only) | None in our app. Reads the carrier list from the office's local ITRANS Claims Director (file, then pinned `https://localhost` API, then `--getncpl`). The agents do mutual TLS | **Yes** for live claims: CDAnet software certification; TELUS / Alberta Blue Cross vendor certification for CCDWS | Not applicable (Canada: PIPEDA and provincial law) |

Two things hold for every row:

- **Code can ship before credentials exist.** Each spec's interim design is "configurable but disabled": the setup screen stores settings, but the integration stays off, and makes no network call, until the office has its credentials and a passing **Test connection**.
- **`osd1$` is obfuscation, not secrecy** (`CDT/README.md`, "Threat model"). Anyone with a database copy and this source can recover every stored secret. The real controls are database access, high-security property flags, least-privilege and revocable credentials, and rotation when a copy leaks.

---

## 2. Credentials and accounts to register

This is the owner's checklist, grouped by vendor. Each item says what to register, where, and **the exact values the program asks for**. Markers:

- **[Self-service, free]**: register online yourself, no fee.
- **[Self-service, paid]**: register online, but it needs a paid plan, licence or payment account.
- **[Partner / sales]**: needs a person at the vendor: a contract, partner status, a reseller or certification.

General rules:

- Register everything in the **office's** name, with an account the office owns, never a staff member's personal login. Record the owner of each account.
- Keep secrets in the office's password manager. Don't send them by email, chat or tickets.
- If the office migrated from Open Dental, also **revoke Open Dental's access** in each vendor's console (the step is listed per vendor). Tokens and keys issued to Open Dental are never reused.
- Before any PHI flows, file the BAA (or the written decision that none is needed) with the office's HIPAA records.

### 2.1 QuickBooks Online (Intuit): [Self-service, free]

Production keys need Intuit's app assessment questionnaire and a website the office controls.

1. Intuit Developer account: <https://developer.intuit.com/>. Then create an app: <https://developer.intuit.com/app/developer/dashboard> > Create an app > QuickBooks Online and Payments. Scope `com.intuit.quickbooks.accounting` only.
2. Development keys: copy the Client ID and Client Secret. Add the redirect URI `http://localhost:8765/callback` (any free port, matched exactly).
3. Sandbox company: <https://developer.intuit.com/app/developer/qbo/docs/develop/sandboxes>. Turn on class tracking.
4. For production, publish on the office's domain:
   - a host domain and a launch URL
   - a disconnect URL and a reconnect URL
   - an EULA URL and a privacy policy URL
   - an HTTPS redirect page (optionally `docs/integrations/qbo-redirect.html`)

   Then complete the questionnaire under Production Settings and register the HTTPS redirect URI. Stay on the free Builder tier.
5. BAA: ask Intuit in writing (none is expected). Record that only non-PHI deposit data goes to QuickBooks Online.
6. Migrating: disconnect Open Dental's app in QuickBooks Online, Settings > Apps.

**The program asks for** (Accounting > Setup > QuickBooks Online):

- **Environment** (Production or Sandbox)
- **Client ID**
- **Client Secret** (masked, high-security)
- **Redirect URI**
- **Relay Port** (optional)

Then **Connect**, and pick the deposit accounts, income accounts and classes.

### 2.2 Microsoft 365 email: [Self-service, free] in the office's tenant

Needs a Microsoft 365 business plan with Exchange Online, which is paid but is the office's existing subscription.

1. Confirm the BAA applies through the Data Protection Addendum. Overview: <https://learn.microsoft.com/en-us/compliance/regulatory/offering-hipaa-hitech>. Send from a custom verified domain.
2. Register the app at <https://entra.microsoft.com> > Entra ID > App registrations > New registration ([guide](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app)):
   - single tenant
   - neutral name
   - platform "Mobile and desktop applications", redirect `http://localhost`
   - no client secret
3. API permissions, Microsoft Graph, delegated:
   - `User.Read`, `Mail.Send`, `offline_access`, `openid`, `profile`
   - plus `Mail.Read` only if mail will be downloaded, and `Mail.ReadWrite` only for attachments over about 3 MB
   - then **Grant admin consent** ([guide](https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/grant-admin-consent))
4. Recommended: Enterprise apps > the app > **Assignment required = Yes**, and add only the users whose mailboxes send.
5. Migrating: delete Open Dental Software's enterprise application in Entra ID > Enterprise apps.

**The program asks for** (Setup > Program Links > Microsoft 365 email):

- **Application (client) ID**
- **Directory (tenant) ID**
- **Redirect URI** (advanced, default `http://localhost`)
- **Sign-in Method** (advanced: system browser or device code)
- **Allow personal accounts** (off)

Then sign in per address: Setup > Email > address > Microsoft > **Sign in...**

### 2.3 Google (Gmail) email: [Self-service, free]

PHI needs Google Workspace (paid) with the BAA accepted.

1. Workspace only: a super administrator accepts the HIPAA BAA in <https://admin.google.com> > Account settings > Legal and compliance ([guide](https://knowledge.workspace.google.com/admin/compliance/hipaa-compliance-with-google-workspace-and-cloud-identity)).
2. Create a Cloud project: <https://console.cloud.google.com/projectcreate>. Use the practice's domain account with Workspace. No billing account is needed.
3. Enable the Gmail API: <https://console.cloud.google.com/apis/library/gmail.googleapis.com>.
4. Consent screen ([Google Auth Platform](https://console.cloud.google.com/auth/overview)):
   - Audience **Internal** (Workspace). Or External, then **Publish app**: in Testing, authorizations expire every 7 days.
   - Scopes: `gmail.send`, plus `gmail.readonly` only for downloading, `openid` and `userinfo.email`.
5. Create the OAuth client: <https://console.cloud.google.com/auth/clients> > Create client > **Desktop app**.
6. Workspace with restricted API access: mark the client **Trusted** under Security > API controls.
7. Migrating: remove Open Dental's app at <https://myaccount.google.com/connections> for each mailbox, or centrally in the Admin console.

**The program asks for** (Setup > Program Links > Gmail API):

- **Client ID**
- **Client Secret** (masked, high-security)

Then per address: Setup > Email > address > **Gmail (OAuth)** > **Authorize...**, signed in to Google as that mailbox.

### 2.4 Dropbox (A to Z storage): [Self-service, free] app

PHI needs a Dropbox team plan (paid) with the BAA signed.

1. A team plan. A team admin signs the BAA in the admin console ([Dropbox and HIPAA](https://help.dropbox.com/security/hipaa-hitech-overview)). Do this before any upload.
2. Create the app in the [App Console](https://www.dropbox.com/developers/apps):
   - **Scoped access**
   - **App folder** access (recommended), or Full Dropbox only to keep an existing tree in place
   - a name without "Dropbox"
3. Permissions: `account_info.read`, `files.metadata.read`, `files.content.read`, `files.content.write`.
4. Settings:
   - Copy the **App key**. The app secret isn't used.
   - **Allow public clients (Implicit Grant & PKCE): Allow.**
   - Optional: register a fixed redirect such as `http://localhost:53682/dropbox`.
   - Leave the app in development status.
5. Migrating: remove Open Dental's app at <https://www.dropbox.com/account/connected_apps>, and check where the old A to Z tree sits.

**The program asks for** (Setup > Data Paths, Dropbox tab; the Dropbox program link must be enabled):

- **App key**
- **Redirect URI** (optional)
- **Dropbox AtoZ Path**

Then **Authorize...**: paste the code Dropbox shows, or let the loopback flow finish.

### 2.5 USPS address check: [Self-service, paid]

The account is self-service, but since August 2026 access needs a signed licence, a payment account and USPS approval, which can take several days.

1. USPS business account in the Customer Onboarding Portal: <https://cop.usps.com> ([getting started](https://developers.usps.com/getting-started)). Note the CRID.
2. Business Portal > My Apps > add an app. Copy the **Consumer Key** and **Consumer Secret**.
3. Read and sign the Addressing API License Agreement: <https://postalpro.usps.com/Addressing_API_License>.
4. Set up an Enterprise Payment System account, add the Addresses API licence, and link the app's credentials to it.
5. Ask USPS to enable TEM (test) and production access: <https://emailus.usps.com/s/usps-APIs>.
6. Read the terms (<https://developers.usps.com/terms-and-conditions>). Single-address checks only, never a bulk clean-up.
7. Privacy officer records that street, unit, city, state and ZIP go to USPS (no BAA offered).

**The program asks for** (Setup > Program Links > USPS address validation):

- **Consumer Key**
- **Consumer Secret** (masked, high-security)
- **Environment** (Production or Testing)

Then **Test connection**, which costs one billed lookup. Keep "Verify Addresses with USPS" ticked in Setup > Preferences > Family > General.

### 2.6 PostcardMania: [Self-service, free] sign-up, but the BAA is unknown

Every order is a purchase billed to the vendor account.

1. **Privacy decision first.**
   - Confirm the mailings are the practice's own communications, with no third party paying.
   - Ask the vendor, through <https://www.pcmintegrations.com/contact/>, whether it signs a HIPAA BAA, and under which entity.
   - **Without a BAA, stop.** Don't use real patient data, even in sandbox.
2. Sign up: <https://portal.pcmintegrations.com/sign-up>. Read the terms first: <https://portal.pcmintegrations.com/terms>.
3. Add a payment method or money on account.
4. My Account > API Keys: generate a **Sandbox** key and secret, and a **Production** pair after testing.
5. Several clinics: one account with a child reference per clinic, or one account per clinic.

**The program asks for** (Setup > Program Links > PostcardMania), per clinic or the default:

- **API Key**
- **API Secret** (masked)
- **Environment** (Sandbox or Production)
- **Child Reference** (optional)

Also **Default Mail Class**, **Use Preferred Name**, **Send Patient Reference** and **BAA on file**. Send is blocked until **BAA on file** is ticked.

### 2.7 CareCredit (Synchrony): [Partner / sales]

Start only if the owner keeps CareCredit (section 4).

1. CareCredit merchant enrollment, with a 16-digit merchant number per location (or per provider): <https://www.carecredit.com/providers/>.
2. Ask the relationship manager to set the office up as a **Direct Integration Partner** for Provider Apply & Buy / IHS:
   - the office's own **partner code**
   - confirmation that a self-run, open-source PMS is acceptable
   - which modules are allowed
3. Developer portal: <https://developer.syf.com/user/register>. Accept the Developer Agreement (<https://developer.syf.com/terms-of-use>).
4. Create an App under My Apps for the Patient Data Service. Record the sandbox **client ID and secret**.
5. Read the gated IHS guide and API reference, and record for the developer:
   - the token URL and grant
   - the sandbox and production hosts, and the hosted-page host
   - the schemas, codes, session lifetime, batch rules and rate limits
   - whether these details may be published in GPL source
6. Ask about a BAA or privacy addendum, the FCRA prescreen duties, and the California rule.
7. Production approval of the app, then production keys.

**The program asks for** (Setup > Program Links > CareCredit):

- **Client ID**, **Client Secret** (masked)
- **Environment**
- **API base URL**, **Hosted-page base URL**
- **Partner code** (Open Dental's `1065` is refused)
- **Merchant numbers** (per clinic or provider)
- payment type, patient fields, batch options
- **Background tasks run on** (computer name)

Then **Test connection**, then **Enabled**.

### 2.8 DoseSpot eRx: [Partner / sales]

1. Decide who holds the contract: the office, or a support organization under a Sub-BAA.
2. Contact DoseSpot sales (`sales@dosespot.com`, per the [Partnership Overview](https://go.dosespot.com/hubfs/DoseSpot/File%20Downloads/DoseSpot_Partnership%20Overview.pdf)). Ask for the **Jumpstart** (SSO) integration type and pricing.
3. Sign the Client Service Agreement and the [Exhibit C BAA](https://dosespot.com/exhibit-c-business-associate-agreement) (or the [Sub-BAA](https://dosespot.com/wp-content/uploads/2025/01/Exhibit-C-Sub-Business-Associate-Agreement-to-Client-Service-Agreement.pdf)).
4. Staging from the integration specialist:
   - a login to <https://docs.dosespot.com/>
   - a staging subscription key
   - two staging clinics (ID and key each)
   - a prescriber user ID and a proxy user ID
   - the token lifetime and rate limits
5. Pass DoseSpot's integration review and Surescripts preparation. Then get the production key, a clinic ID and key per location, and a user ID per prescriber and proxy user.
6. Each prescriber completes identity proofing and, for controlled substances, EPCS enrollment inside DoseSpot.
7. Migrating: ask DoseSpot to move the clinics, clinicians and history out of Open Dental's account.

**The program asks for:**

- Setup > Program Links > eRx (DoseSpot):
  - **Environment** (Production or Staging)
  - **Subscription Key** (masked, high-security)
  - per clinic: **Clinic ID** and **Clinic Key** (masked, high-security)
- Security > Users: **DoseSpot User ID**, per user and clinic.
- Lists > Providers: eRx enabled per prescriber.

### 2.9 NewCrop (Ensora) eRx and the OID root

NewCrop is **[Partner / sales]**. The OID root is **[Self-service, free]**.

1. First run the discovery queries in `newcrop.md` §6, step 1 on a restored `fox-db` copy. If the office doesn't use NewCrop, skip steps 2-5.
2. Choose the route: an authorized Ensora reseller, or Ensora directly ([product page](https://ensorahealth.com/product/erx/features); there is no public partner sign-up).
3. Ask for:
   - preproduction, then production, credentials
   - the current NCScript and `Update1` documentation
   - the account and site ID rules
   - acceptance of the product name, and any certification steps (including EPCS)
   - pricing, and whether the old account and history can move
4. Sign the BAA with Ensora, or with the reseller plus a downstream BAA.
5. Cut over, then cancel the Open Dental-billed NewCrop service.
6. OID root: press **Generate root** (a `2.25.<uuid>` root, no registration), or register a free IANA Private Enterprise Number (<https://www.iana.org/assignments/enterprise-numbers/assignment/apply>) and enter `1.3.6.1.4.1.<PEN>.1`. Tell HL7 and CCD trading partners before changing an existing root.

**The program asks for:**

- Setup > Program Links > eRx (NewCrop panel):
  - **Environment** (Production or Preproduction)
  - **Partner Name**, **Account Name**
  - **Password** (masked, high-security)
  - **Account ID** (entered, or **Generate**)
  - **Site ID** (default `1`)
  - **Product Name**
  - the **Enabled for NewCrop** box per provider
- OID registry: **Generate root** or **Enter root**.

### 2.10 PayConnect 2 (DentalXChange)

The integrator registration is **[Self-service, free]**. The merchant account and the production key are **[Partner / sales]**.

1. Merchant account for each location that takes cards, through DentalXChange Merchant Services (<https://payconnect.dentalxchange.com/provider/payconnect/MerchantProcessing>). Complete PCI onboarding. Get the merchant **Secret** for each merchant account.
2. Register as an integrator: <https://developer.dentalxchange.com/register/>. Request sandbox access ([sandbox](https://developer.dentalxchange.com/sandbox)) and retrieve the sandbox API key.
3. Ask `payconnect@dentalxchange.com` in writing:
   - whether a single practice may hold its own **production API key**, and under what terms
   - whether the key is per integrator or per merchant
   - rate limits and terminal timeouts
   - whether a BAA is needed
   - which hosts serve the embedded page
   - whether any 2.0 terminal needs a local driver
4. Register terminals and note their serials.

**The program asks for** (Setup > Program Links > PayConnect, Version 2.0):

- **API key** (practice-wide, masked, high-security)
- **Use sandbox**
- **Secret** per clinic (masked, high-security)
- payment type
- terminals (name and Terminal ID)

Then **Test connection**.

### 2.11 PaySimple: [Partner / sales]

The merchant account and credentials come from PaySimple staff.

1. A PaySimple merchant account with cards and/or ACH. An office from Open Dental already has one.
2. Before go-live, write to partnersupport@paysimple.com:
   - whether an integration review applies to this software
   - whether the account was boarded through Open Dental's partner relationship
   - whether any reseller webhook still sends events to Open Dental
3. Sandbox credentials from PaySimple support ([sandbox](https://documentation.paysimple.com/docs/sandbox)). Production credentials per merchant account from Customer Care, by encrypted email ([production](https://documentation.paysimple.com/docs/production)).
4. After setup, review the **Webhook subscriptions...** list and remove any that point at Open Dental.
5. Compliance: annual PCI self-assessment (SAQ D while cards are keyed in the desktop) and NACHA authorizations for ACH. Counsel confirms that no BAA is needed.

**The program asks for** (Setup > Program Links > PaySimple), per clinic:

- **API User Name**
- **API Key** (masked)
- **Environment** (Production or Sandbox)
- payment types (card, ACH)

Also **ACH status check** on or off. Then **Test connection**.

### 2.12 EdgeExpress (Global Payments OpenEdge): [Partner / sales]

1. A merchant account with EdgeExpress, card-not-present (hosted pay page) and alias storage. Card-present also needs RCM and a terminal. Contact: `developerservices@openedgepay.com` ([product page](https://openedgepayments.com/edgeexpress/)).
2. Ask in writing whether this independently maintained fork may submit with the merchant's credentials, or whether partner registration or certification is needed. **Keep the program disabled in production until answered.**
3. Ask for:
   - the current CNP / HostPay specification
   - the `RETURNURL` rules
   - the session lifetime and rate limits
   - sandbox credentials, test cards and decline amounts
4. Production credentials per clinic, read out or taken from the merchant portal, never emailed.

**The program asks for** (Setup > Program Links > EdgeExpress), per clinic:

- **X-Web ID** (12 digits)
- **Terminal ID** (8 digits)
- **Auth Key** (32 characters, masked)
- payment type
- **Return mode** (Embedded or Loopback)
- **Return URL** (blank = built-in)
- **Use test gateway** (practice level)

### 2.13 PDMP (Bamboo Health, LogiCoy)

The integrator steps are **[Partner / sales]**, once per integrator. The per-office steps are **[Partner / sales]** too, with state approval.

**Integrator (the project, or an office acting for itself):**

1. Bamboo: `pdmpintegrations@bamboohealth.com`, <https://pmpgateway.zendesk.com/hc/en-us>. Ask for:
   - the PMP Gateway specification, pre-production access and test patients
   - whether client certificates can be issued **per office** (healthcare entity)
   - the `SenderSoftware` values, URLs, schema version and clock tolerance
2. Pass the demo review and get listed as an integrated EHR product.
3. LogiCoy: `pdmp@logicoy.com`. Ask to register the product, and for the per-state URLs, the sender values and the auth method.

**Per office:**

1. Every prescriber has an active state PMP account.
2. Check eligibility at <https://bamboohealth.com/ehr-request/>.
3. Bamboo states: register at <https://connect.bamboohealth.com/>, name this product as the EHR, and sign the state terms. Pay the licence fee in non-funded states. Approval takes about 2-4 weeks.
4. LogiCoy states: register with LogiCoy, or through the state program.

**The program asks for:**

- Setup > Program Links > Bamboo:
  - **Bamboo Url**
  - **Bamboo Username**
  - **Bamboo Password** (masked)
  - **Bamboo FacilityID**
  - **Client certificate...**: a `.pfx`/`.p12` file and its password, or a Windows-store certificate by thumbprint
  - **Sender Developer** / **Sender Product** (only if Bamboo assigns them)
  - **PDMP Provider License Field**
- Setup > Program Links > PDMP (LogiCoy), per state:
  - **{State} PDMP Url**
  - **Username**
  - **Password** (masked)
  - **FacilityID**
  - Sender Developer / Product
- Per clinic when clinics are on. Then **Check setup**.

### 2.14 Canada (ITRANS 2.0, CCDWS)

Per dentist: **[Partner / sales]** through CDA membership. For the project: **[Partner / sales]** certification.

First check whether the office is in Canada at all: `SELECT COUNT(*) FROM carrier WHERE IsCDA=1` on `fox-db`.

1. **Project:**
   - Ask `cdanet@cda-adc.ca` about CDAnet certification, the Development Tools and simulator, and vendor test credentials.
   - Only for hygienist and denturist claims: vendor certification with TELUS Health and Alberta Blue Cross, and the CDHA / DACnet listings.
2. **Each dentist:**
   - Subscribe through PSS (<https://services.cda-adc.ca>) and accept the CDAnet/ITRANS Service Schedule for each location.
   - Install a CDA Digital ID through the PSS tray app.
   - Install ICD v4.2 from the [ICD page](https://www.cda-adc.ca/for-dental-professionals/programs-amp-services/cdanet-and-itrans/certified-vendors/icd-software).
3. Remove any leftover `OPENDENTAL.pem` or `OD_*.pem` files from CCDWS folders.

**The program asks for** (Setup > Clearinghouses). No secrets are stored:

- **CommBridge = ITRANS 2.0**
- **Export path** (the ICD claims folder)
- **Response path** (the ICD install folder)

Then Setup > Insurance > Carriers > **Update Carriers**, or **Import from file...**.

### 2.15 Count

The program asks for **39 credential values** across all vendors, identifiers and secrets together:

| Vendor | Values | Secrets among them |
|---|---|---|
| QuickBooks Online | 2 | 1 |
| Microsoft 365 | 2 | 0 |
| Gmail | 2 | 1 |
| Dropbox | 1 | 0 |
| USPS | 2 | 1 |
| PostcardMania | 2 | 1 |
| CareCredit | 4 (client ID and secret, partner code, merchant numbers) | 1 |
| DoseSpot | 4 (subscription key, clinic ID, clinic key, user ID) | 2 |
| NewCrop | 5 (partner name, account name, password, account ID, site ID) | 1 |
| PayConnect | 2 | 2 |
| PaySimple | 2 | 1 |
| EdgeExpress | 3 | 1 |
| Bamboo | 5 (username, password, facility ID, certificate, certificate password) | 3 |
| LogiCoy | 3 per state (username, password, facility ID) | 1 |

That makes **17 secrets** stored as `osd1$`. Four more secrets are obtained by signing in, never typed: the QuickBooks Online refresh token, the Microsoft 365 token cache, the Gmail refresh token and the Dropbox refresh token.

---

## 3. Build plan

### 3.1 How the work is cut

The rule: **no two packages that run at the same time edit the same file.** Three techniques make that possible:

1. **Foundation first.** Package `F0` builds the shared infrastructure (section 5). It also makes every small edit that several vendors would otherwise each make to a registry-style hot file:
   - the `ProgramName` enum
   - `FormProgramLinks`
   - `ProgramProperties`
   - `Enumerations`
   - `Introspection`
   - the csproj files
   - `FormOpenDentalThreads`
   - `FormOpenDental` and its menus
2. **Extract, then hand over.** Where a hot file needs a vendor-specific change in one block, `F0` moves that block, unchanged, into a new file and leaves a one-line call behind. The vendor package then owns the new file. Examples:
   - the Dropbox startup check in `FormOpenDental.cs`
   - the PDMP branch of `WpfControlsOD/Logic/ProgramL.cs`
   - the Postcards menu click handler
3. **One owner per shared screen or flow.** Files that several vendors change heavily get one integration package, which runs after the vendor libraries it calls:
   - `EMAIL-UI` owns the email files.
   - `PAY-UI` owns the payment windows.
   - `ERX` owns the eRx shell for both DoseSpot and NewCrop.

   Vendor packages only add their own files and remove their own callers. The final `Z1` package deletes the HQ members that are left without callers.

Every package:

- builds and tests against a fake `HttpMessageHandler` (or the vendor's mock hook)
- merges with its program still `Enabled=0`
- puts its tests under `tests/VendorDirectTests/<Vendor>/`. `F0` creates that project with a wildcard `Compile Include`, so no package edits a test csproj. Existing test files (`ErxTests.cs`, `PDMPTests.cs`, `CarrierTests.cs`, `PayConnect2Tests.cs`) are edited only by their owner.

### 3.2 Hot files and their owners

A file listed here is edited by its owner only. Changes other specs ask for in it are made by that owner, against the other package's API.

| Hot file | Owner | Also carries the changes asked for by |
|---|---|---|
| `OpenDental/Forms/FormPayment.cs` (+ designer) | `PAY-UI`; later `PAYCONNECT1-REMOVE` | CareCredit: rule-7 visibility and signup branches, `using` swap. PayConnect: `IsConfigured`, void timeout. PaySimple: text button, ACH status label, context-menu item, removing the "show all processors" branch. EdgeExpress: `EdgeExpressL.ShowPayPage` |
| `OpenDental/Forms/FormClaimPayEdit.cs` (+ designer) | `PAY-UI` | PayConnect `IsConfigured`; PaySimple text button and show-all branch |
| `OpenDentBusiness/Data Interface/RecurringCharges.cs` | `PAY-UI` | PayConnect v2 pre-check and validation detail; PaySimple ACH `ExternalId` and status |
| `OpenDental/Forms/FormCreditCardManage.cs` | `PAY-UI` | EdgeExpress add-card window |
| `OpenDentBusiness/Data Interface/EmailMessages.cs` | `EMAIL-UI` | Gmail: token cache, pre-check removal, inbox fixes. Microsoft 365: session tokens, inbox, `RefreshMicrosoftToken` |
| `OpenDentBusiness/Email/SendEmails.cs` | `EMAIL-UI` | Gmail: rethrow `GoogleApiException`, upload path. Microsoft 365: `using`, `CreateMIMEMsg` made internal |
| `OpenDental/Forms/FormEmailAddressEdit.cs` (+ designer) | `F0` (interim gate only), then `EMAIL-UI` | Gmail and Microsoft 365 groups, status labels, radio gating |
| `OpenDental/Forms/FormEmailMessageEdit.cs`, `FormEmailInbox.cs` | `EMAIL-UI` | Microsoft 365 re-auth; Gmail manual receive |
| `OpenDental/Logic/PatientL.cs` | `USPS` | Bridges-dll `using` removal (USPS is its only Bridges consumer) |
| `OpenDental/Main Modules/ControlChart.cs` | `ERX` | DoseSpot and NewCrop: toolbar gating, `Tool_eRx_Click`, `UpdateErxAccess`, throttled refreshes, log redaction |
| `OpenDentBusiness/WebBridges/Erx/Erx.cs`, `ErxXml.cs`; `OpenDental/Forms/FormErxSetup.cs` (+ designer); `FormProvEdit.cs` | `ERX` | DoseSpot and NewCrop |
| `OpenDentBusiness/WebServices/IWebServiceMainHQ.cs`, `WebServiceMainHQNoHq.cs`, `WebServiceMainHQMockDemo.cs`, `WebServiceMainHQProxy.cs`, `CustomerUpdatesProxy.cs`; `NoHqTests/NoHqTests.cs` | `Z1` (HQ-PRUNE) | Every vendor: delete members and mock cases once no caller remains (list in 3.4) |
| `OpenDentBusiness/OpenDentBusiness.csproj` | `F0` (wildcards, stubs, MSAL reference), then `Z2` (removals) | Every vendor. New business files go into folders `F0` has registered (3.3) |
| `OpenDental/OpenDental.csproj` | `F0` (new-form stubs), then `PAYCONNECT1-REMOVE`, then `Z2` | New forms: `FormMicrosoft365Setup`, `FormGmailApiSetup`, `FormUspsAddressSetup`, `FormEdgeExpressPayPage`, `FormPdmpClientCertificate`, `Logic/EdgeExpressL.cs`. Removals (deleted forms, Intuit/Serilog/DpsPos/Graph references) in `Z2` |
| `OpenDentBusiness/TableTypes/Program.cs` (`ProgramName`) | `F0` | Appends `Microsoft365Email`, `GmailApi`, `USPSAddresses`, `PostcardMania` |
| `OpenDental/Forms/FormProgramLinks.cs` | `F0` | Adds the `IProgramLinkSetup` registry hook (5.6). Vendor packages register their setup forms in their own files |
| `OpenDentBusiness/Data Interface/ProgramProperties.cs`, `Programs.cs` | `F0` | Shared helpers (5.2). Also the small vendor edits that are safe ahead of time: `GetXWebCreds` decrypt (EdgeExpress), `GetQuickBooksOnlineEntityNameById` (QuickBooks Online), the `InsertForClinic` flag fix and skip list (PayConnect), `GetPaySimpleOnlinePaymentCreds` decrypt (PaySimple). New property-name constants live in each vendor's own settings class, not in `PropertyDescs` |
| `OpenDentBusiness/Db/Enumerations.cs` (`OAuthApplicationNames`), `OpenDentBusiness/Misc/Introspection.cs` | `F0` | Mark the members unused; never delete them (section 5). Add `PostcardManiaApiUrl` |
| `OpenDental/Main Modules/FormOpenDentalThreads.cs` | `F0` | Starts the vendor job host (5.6) and adds the Canada clearinghouse gate (`canada.md` 4.1). The PaySimple ACH poll, EdgeExpress reconciliation and CareCredit jobs become job classes in their own packages |
| `OpenDental/Main Modules/FormOpenDental.cs`, `FormOpenDentalMenus.cs` | `F0`, then `Z2` | `F0`: gates the Advertising menu on `Programs.IsEnabled(ProgramName.PostcardMania)`, moves the Postcards click handler to `OpenDental/Logic/VendorDirect/PostcardsMenu.cs` (owned by `POSTCARD`), and moves the Dropbox startup check to `OpenDental/Logic/VendorDirect/DropboxStartupCheck.cs` (owned by `DROPBOX`). `Z2`: removes the NewCrop and HQ tool menu items |
| `WpfControlsOD/Logic/ProgramL.cs` | `CARECREDIT` | `F0` moves the PDMP branch to `WpfControlsOD/Logic/VendorDirect/PdmpLaunch.cs` (owned by `PDMP`) |
| `CodeBase/Utilities/ODCloudClient.cs` | `Z2` | Gmail, Microsoft 365, PayConnect: vendor packages only remove callers; `Z2` (or the ODCloud removal) deletes the members |
| `OpenDental/Properties/Resources.resx` and `Resources.Designer.cs` | `Z2` | Remove the Google sign-in images, the PaySimple logo and the Dropbox licence text once nothing references them |
| `OpenDentBusiness/TableTypes/Pref.cs` | `Z2` | Comment-only changes (CareCredit batch prefs, NewCrop prefs) |
| `tools/Test-NoHqHosts.ps1` | `F0` | One allow-list entry for the shared blocked-host list (`HqHosts.cs`) |
| `docs/third-party-binaries-audit.md` | `Z2` | Intuit, Serilog, Graph, MSAL, Dropbox.Api, Bridges, DpsPos rows |

### 3.3 Where new files go

In `OpenDentBusiness.csproj` (old-style, explicit includes), `F0` adds one wildcard `Compile Include` per **new** folder:

- `Bridges\QuickBooksOnline\**`
- `Bridges\CareCredit\**`
- `Bridges\PostcardMania\**`
- `WebBridges\Microsoft365\**`
- `WebBridges\Usps\**`
- `WebBridges\Erx\DoseSpot\**`
- `VendorDirect\**`

New files that specs place in **existing** folders get an empty stub, registered explicitly by `F0`, because a wildcard there would include the existing files twice:

- `WebBridges/Google/GoogleTokenCache.cs`, `GoogleOAuthSettings.cs`
- `WebBridges/Erx/NewCrop/NewCropConfig.cs`
- `WebBridges/PDMP/PdmpHttp.cs`, `PdmpCertificate.cs`
- `Eclaims/IcdLocalClient.cs`, `CanadianVendorCert.cs`
- `WebBridges/PaySimpleAchStatusSync.cs`

`OpenDentalCloud.csproj` is touched only by Dropbox, so `DROPBOX` owns it.

### 3.4 Work packages

Days are developer days. "Gated" means the code can be finished and merged disabled, but production use waits on section 2.

| ID | Package | Owned files (besides tests) | Depends on | Size |
|---|---|---|---|---|
| **F0** | Foundation and scaffolding | New: `CodeBase/Utilities/VendorDirect/*` (5.1, 5.3, 5.4); `OpenDentBusiness/VendorDirect/*` (5.2, 5.6); `OpenDental/Logic/VendorDirect/{IProgramLinkSetup.cs, ProgramLinkSetupRegistry.cs, VendorJobHost.cs}`; the extracted files listed in 3.2 (then handed over); stubs listed in 3.3; `tests/VendorDirectTests/` and its csproj; solution / `OpenDental.Local.slnf` entry. Edits: every file with owner `F0` in 3.2 | none | **M**, 4-5 d |
| **QBO** | QuickBooks Online direct, Bridges types re-implemented | `OpenDentBusiness/Bridges/QuickBooksOnline/**` (namespace `OpenDentBusiness.Qbo`); `FormQuickBooksOnlineSetup.cs`, `FormQuickBooksOnlineAuthorization.cs` (+ designers); `FormDepositEdit.cs`; `FormQBAccountSelect.cs`; `docs/integrations/qbo-redirect.html`; unused `using Intuit.Ipp.Data;` in `FormSmsTextMessaging.cs` and `FormMapAreaContainerEdit.cs` | F0 | **M**, 5-7 d |
| **GMAIL-LIB** | Gmail OAuth client and setup | `OpenDentBusiness/WebBridges/Google/Google.cs`, `GoogleApiConnector.cs`, `GoogleTokenCache.cs`, `GoogleOAuthSettings.cs`; `OpenDentBusiness/Properties/Resources.resx` and `Resources.Designer.cs` (`GoogleAuthCodeResponseHtml`); new `FormGmailApiSetup` | F0 | **S-M**, 3 d |
| **M365-LIB** | Microsoft 365 MSAL session and Graph client, Bridges types re-implemented | `OpenDentBusiness/WebBridges/Microsoft365/**` (namespace `OpenDentBusiness.Microsoft365`: `MicrosoftApiConnector`, `MicrosoftTokenHelper`, `Microsoft365Session`, `Microsoft365GraphClient`, `Microsoft365Program`, exceptions); new `FormMicrosoft365Setup` | F0 | **M**, 4-5 d |
| **EMAIL-UI** | Email flows for both providers | `EmailMessages.cs`, `SendEmails.cs`, `FormEmailAddressEdit.cs` (+ designer), `FormEmailMessageEdit.cs`, `FormEmailInbox.cs` | GMAIL-LIB, M365-LIB | **M**, 3 d |
| **DROPBOX** | Dropbox PKCE and in-repo REST client | `OpenDentalCloud/Storage/Dropbox.cs` and new `Storage/DropboxApi.cs`, `DropboxSession.cs`, `DropboxOAuth.cs`; `OpenDentalCloud.csproj`; `OpenDentalCloud/Shared/TaskState*.cs`; `OpenDentBusiness/WebBridges/Dropbox.cs`, `CloudStorage.cs`; `FormDropboxAuthorize.cs`, `FormPath.cs` (+ designers); `OpenDental/Logic/VendorDirect/DropboxStartupCheck.cs`; `FormLicense.cs`, `FormRegistrationKey.cs`, `Resources/Dropbox_Api.txt`; the `ProgramEntry.cs` comment | F0 | **M**, 4-6 d (**S** if removed, section 4) |
| **USPS** | USPS Addresses v3 client, Bridges types re-implemented | `OpenDentBusiness/WebBridges/Usps/**` (namespace `OpenDentBusiness`); `PatientL.cs`; `WpfControlsOD/Frms/FrmAddressCompare.xaml(.cs)`; `UserControlFamilyGeneral.cs`; new `FormUspsAddressSetup`; optional `FormSheetImport.cs` (unchanged-address skip) | F0 | **S**, 2-3 d |
| **POSTCARD** | PostcardMania v3 (or its removal) | `OpenDentBusiness/Bridges/PostcardMania/**`; `AdvertisingPostcards.cs`; `FormAdvertisingPostcardsSetup.cs`, `FormAdvertisingPostcardsSend.cs` (+ designers, resx); `FormAdvertisingPatientList.cs`; `OpenDental/Logic/VendorDirect/PostcardsMenu.cs`. Deleting `FormAdvertisingPostcardsAccountSetup` (csproj) happens in Z2 | F0 | **M**, 4-6 d; **S**, 1 d to remove |
| **CARECREDIT** | Remove (default) or implement direct (only past the go/no-go gate) | `OpenDentBusiness/Bridges/CareCredit/**` (namespace `OpenDentBusiness.Bridges`); `CareCreditL.cs`; `FormCareCredit.cs`, `FormCareCreditSetup.cs`, `FormCareCreditTransactions.cs`, `FormCareCreditWeb.cs` (+ designers); `FormProvAdditional.cs`; `CareCreditWebResponses.cs`; `TableTypes/CareCreditWebResponse.cs`; `ControlAppt.cs`; `OpenDental/Logic/ProgramL.cs`; `WpfControlsOD/Logic/ProgramL.cs`; `ToolButItems.cs` (generic rule-7 fix); CareCredit job classes | F0 | **S**, 1-2 d to remove; **L**, 15-20 d gated to implement |
| **ERX** | DoseSpot direct, then NewCrop with Open Dental's compiled-in credentials deleted | `ControlChart.cs`; `Erx.cs`; `ErxXml.cs`; `DoseSpot.cs`; `DoseSpotV2.cs`; `WebBridges/Erx/DoseSpot/**`; `NewCrop.cs`; `NewCropConfig.cs`; `FormErxSetup.cs` (+ designer); `FormDoseSpotProperyEdit.cs` (+ designer); `FormErx.cs`; `FormPatientEdit.cs` (consent block); `FormProvEdit.cs`; `OpenDentBusiness/Properties/Settings.Designer.cs`; `UnitTests/UnitTests/ErxTests.cs`. Deleting the DoseSpot SOAP web reference and the HQ eRx tools happens in Z2 | F0, OID | **L**, 8-11 d (DoseSpot 4-6, NewCrop 3-5), gated |
| **OID** | Local OID root | `OpenDentBusiness/Data Interface/OIDInternals.cs` (add `GenerateUuidRoot`, `IsValidOid`, `ApplyRoot`; rename `OpenDentalOID` to `LegacyOidArc` and keep an `[Obsolete]` alias); `FormOIDRegistryInternal.cs` (+ designer). Deleting `CustomerPatNum` waits for Z2 | F0 | **S**, 1 d |
| **PDMP** | Bamboo and LogiCoy with office certificates | `OpenDentBusiness/WebBridges/PDMP/*` (`PDMP.cs`, `PdmpProperty.cs`, `PDMPBamboo.cs`, `PDMPLogicoy.cs`, `PdmpHttp.cs`, `PdmpCertificate.cs`); `FormProgramLinkEdit.cs` (+ designer); new `FormPdmpClientCertificate`; `WpfControlsOD/Logic/VendorDirect/PdmpLaunch.cs`; the PDMP unit tests | F0 | **M**, 5-7 d, gated |
| **PAYCONNECT-LIB** | PayConnect 2 keys, sandbox switch, reconciliation, WebView2 hardening | `OpenDentBusiness/WebBridges/PayConnect2.cs`; `PayConnect.cs` (constants only); `FormPayConnectSetup.cs` (+ designer); `FormPayConnect2.cs`; `FormPayConnect2iFrame.cs`; `WpfControlsOD/Bridges/PayConnectTerminal.cs` (literals to constants); `PayConnect2Tests.cs` | F0 | **M**, 3-4 d, gated |
| **PAYSIMPLE-LIB** | Basic auth, encryption, ACH status poller | `OpenDentBusiness/WebBridges/PaySimple.cs`; `PaySimpleAchStatusSync.cs` (+ its job class); `Payments.cs` (`GetPaySimpleAchToTrack`); `FormPaySimpleSetup.cs` (+ designer); `FormPaySimple.cs` | F0 | **M**, 2.5-4 d |
| **EDGEEXPRESS-LIB** | WebView2 pay page, return modes, reconciliation | `OpenDentBusiness/WebBridges/EdgeExpress.cs`; `TableTypes/XWebResponse.cs`; `Data Interface/XWebResponses.cs`; `WebTypes/Shared/XWeb/XWebs.cs`; `FormEdgeExpressSetup.cs` (+ designer); `CodeBase/Controls/ODWebView2.cs` (InPrivate overload); new `FormEdgeExpressPayPage`, `OpenDental/Logic/EdgeExpressL.cs`, reconciliation job class | F0 | **M**, 3-3.5 d, gated |
| **PAY-UI** | Payment windows for all processors | `FormPayment.cs` (+ designer), `FormClaimPayEdit.cs` (+ designer), `RecurringCharges.cs`, `FormCreditCardManage.cs` | PAYCONNECT-LIB, PAYSIMPLE-LIB, EDGEEXPRESS-LIB, CARECREDIT | **M**, 3-4 d |
| **PAYCONNECT1-REMOVE** | Remove PayConnect 1.0, VeriFone and `DpsPos.dll` (section 4) | `FormPayConnect.cs` (delete); v1 branches in `FormPayment.cs`; `PayConnectTerminal.cs` (`PosResponse` mapping); the v1 `PayConnectService` web reference and `PayConnectREST` in `PayConnect.cs`; `PrefL.cs`; DpsPos and log4net references in `OpenDental.csproj`, `WpfControlsOD.csproj`, `UnitTests.csproj`; `Resources/DpsPos.dll.config` | PAY-UI | **M**, 3-4 d |
| **CANADA** | Remove HQ relays; local ICD carrier list; read-only certificate check | `OpenDentBusiness/Misc/ItransNCpl.cs`; `Eclaims/Canadian.cs`; `IcdLocalClient.cs`; `CanadianVendorCert.cs`; `FormCarriers.cs` (+ designer); `UnitTests/UnitTests/CarrierTests.cs` | F0 | **S**, 2 d (0.5 d if US-only: section 4.1 of the spec) |
| **Z1** | HQ-PRUNE: delete HQ members with no callers left | `IWebServiceMainHQ.cs`, `WebServiceMainHQNoHq.cs`, `WebServiceMainHQMockDemo.cs`, `WebServiceMainHQProxy.cs`, `CustomerUpdatesProxy.cs`, `NoHqTests/NoHqTests.cs` | every vendor package above | **S**, 1-1.5 d |
| **Z2** | Remove `Bridges.dll` and leftover binaries; final clean-up | Below | Z1, PAYCONNECT1-REMOVE | **S-M**, 2 d |

**Z1 deletes:**

- Gmail: `GetGoogleAccessToken`, `GetGoogleAuthorizationUrl`, `GetGoogleAuthorizationUrlLoopbackIpAddressFlow`, and the mock's compiled-in Google sample secret
- Dropbox: `GetDropboxAccessToken`, `GetDropboxAuthorizationUrl`
- CareCredit: the four token, log and batch members; `GetCareCreditBatch*Time`; the merchant-ID block of `GetSerializedProgramProperties`
- PostcardMania: the four account, SSO and upload members, the proxy region and the mock methods
- USPS: `ProcessUSPSAddressValidationRequest` (proxy)
- Canada: `CanadaCarrierUpdate`, the three `RequestCertCanada*`
- eRx: `GetClinicErxAccess`, `GetErxAccess`, `RequestCustomerID`
- PaySimple: `InsertPaySimpleACHId`, `GetPaySimpleWebHookUrl`
- QuickBooks Online and PayConnect: their mock cases
- **`BuildOAuthUrl`**, once no caller remains

It also updates `NoHqTests` to match. The generated `Web References` proxies go with the Phase 3 HQ removal.

**Z2 removes:**

- every `using Bridges;` and the `xBridges` alias
- the `Bridges` HintPath references in `OpenDentBusiness.csproj`, `WpfControlsOD.csproj` and `UnitTests.csproj`, and the `..\..\Bridges\xBridges.csproj` ProjectReference in `OpenDental.csproj`
- `Required dlls/Bridges.dll`
- `Required dlls/`: the 14 `Intuit.Ipp.*` DLLs, `IppDotNetSdkForQuickBooksApiV3.dll`, the 13 `Serilog*` DLLs, `Microsoft.Graph.dll`, `Microsoft.Graph.Core.dll`, `Azure.Core.dll` (if Graph-only), the `Microsoft.IdentityModel.*` DLLs that MSAL doesn't need, and `Dropbox.Api.dll`, with their `OpenDental.csproj` references
- the unused `using Microsoft.Graph*` lines in `AutoCodes.cs`, `PatientNotes.cs` and `Patients.cs`
- the DoseSpot v1 SOAP web reference
- the forms: `FormAdvertisingPostcardsAccountSetup`, `FormNewCropBilling`, `FormErxAccess`, `FormDoseSpotAssignClinicId`, `FormDoseSpotAssignUserId`, and the matching `FormOpenDental` / `FormOpenDentalMenus` entries
- `OIDInternals.CustomerPatNum`
- the dead `ODCloudClient` members
- unused resources
- the `Pref.cs` comments
- the binaries-audit rows

**Order:**

| Wave | Packages | Notes |
|---|---|---|
| 0 | F0 | Serial; everything else depends on it. Include the interim Microsoft 365 gate (hide the Microsoft radio unless the new program is enabled), because sign-ins still run under Open Dental's app registration today |
| 1 | QBO, GMAIL-LIB, M365-LIB, DROPBOX, USPS, POSTCARD, CARECREDIT, OID, PDMP, PAYCONNECT-LIB, PAYSIMPLE-LIB, EDGEEXPRESS-LIB, CANADA; then ERX after OID | All parallel; no shared files. ERX waits for OID only for the renamed OID constant (or starts at once and uses the old name) |
| 2 | EMAIL-UI, PAY-UI | Parallel with each other |
| 3 | PAYCONNECT1-REMOVE | After PAY-UI (same `FormPayment.cs`); before Z2 (same csproj files) |
| 4 | Z1 | After every vendor package |
| 5 | Z2 | Last. Smoke-test every former Bridges feature (QuickBooks Online, Microsoft 365, USPS, CareCredit or its removal, Postcards) on a build with `Bridges.dll` gone |

Total, with CareCredit and PostcardMania removed and everything else built: about **60-80 developer days** (F0 4-5, QBO 5-7, GMAIL-LIB 3, M365-LIB 4-5, EMAIL-UI 3, DROPBOX 4-6, USPS 2-3, POSTCARD 1, CARECREDIT 1-2, ERX 8-11, OID 1, PDMP 5-7, PAYCONNECT-LIB 3-4, PAYSIMPLE-LIB 2.5-4, EDGEEXPRESS-LIB 3-3.5, PAY-UI 3-4, PAYCONNECT1-REMOVE 3-4, CANADA 0.5-2, Z1 1-1.5, Z2 2). With 4-5 people working in parallel, waves 0-5 take about 4 calendar weeks; the critical paths (F0, then OID and ERX; or F0, then the payment libraries, PAY-UI and PAYCONNECT1-REMOVE; each followed by Z1 and Z2) are about 20 days each. Vendor onboarding (DoseSpot, NewCrop, PDMP, EdgeExpress, PayConnect production keys, USPS licence) runs separately and decides when each integration can be enabled.

### 3.5 Re-implementing the prebuilt `Bridges.dll` surface

`Bridges.dll` is Open Dental's prebuilt binary. We infer what it did only from how this repo calls it; it is never decompiled. It covers five vendors. The replacements live **inside `OpenDentBusiness`**, for two reasons:

- `Bridges.dll` itself references `OpenDentBusiness`: it takes `OpenDentBusiness.Address`, throws `HqNotAvailableException` and calls `IWebServiceMainHQ`.
- `OpenDentBusiness` references `Bridges.dll` back (HintPath).

So a separate in-repo "Bridges" project that needs `OpenDentBusiness` types would make a reference cycle. `WpfControlsOD` and `OpenDental` already reference `OpenDentBusiness`, so every caller can reach the new types.

| Bridges.dll types used today | Callers today | Replaced by | Namespace / folder |
|---|---|---|---|
| `QuickBooksOnline`, `QuickBooksOnlineToken`, `QuickBooksOnlineAccount`, `QuickBooksOnlineClassRef`, `QuickBooksOnlineDepositResponse` | `FormQuickBooksOnlineSetup`, `FormDepositEdit` | QBO (same public members, plus `BeginAuthorization` / `CompleteAuthorization` in place of the URL-only method) | `OpenDentBusiness.Qbo`, `OpenDentBusiness/Bridges/QuickBooksOnline/` |
| `MicrosoftApiConnector`, `MicrosoftTokenHelper` (and Graph's `Message` type in a return value) | `EmailMessages`, `SendEmails`, `FormEmailAddressEdit`, `FormEmailMessageEdit` | M365-LIB (same names; `RetrieveMessages` returns `MicrosoftMessageRef`) | `OpenDentBusiness.Microsoft365`, `OpenDentBusiness/WebBridges/Microsoft365/` |
| `USPSAddressValidationDatas`, `USPSAddressValidationResponse`, `EnumDPVConfirmation` | `PatientL`, `FrmAddressCompare` | USPS (same members) | `OpenDentBusiness`, `OpenDentBusiness/WebBridges/Usps/` |
| `CareCredit` (static), `CareCredit.CareCreditException`, `CareCreditLandingPage`, `EnumCareCreditPrefillServiceType`, `EnumCareCreditBatch`, `CareCreditQSBatchResponse` | CareCredit forms and logic, `FormPayment`, `ControlAppt`, `FormProvAdditional`, `CareCreditWebResponses`, `WebServiceMainHQProxy`, `WpfControlsOD` `ProgramL` | CARECREDIT. Implementing: same surface, except `ProviderSignupURL`. Removing: the callers go, and only data types that migrated rows need stay (`CareCreditQSBatchResponse` for the Transactions window, if it is kept read-only) | `OpenDentBusiness.Bridges`, `OpenDentBusiness/Bridges/CareCredit/` |
| eight `PostcardMania*` request and response types | `AdvertisingPostcards`, three Postcards forms, HQ proxy and mock | Not re-implemented under the same names. POSTCARD writes new v3 DTOs; Z1 deletes the HQ proxy and mock code | `OpenDentBusiness/Bridges/PostcardMania/` |
| (none; stray `using Bridges;`) | `PayConnect2.cs` | PAYCONNECT-LIB deletes the line | n/a |

Rules for every package that swaps a surface:

- **Never declare a type in a namespace called `Bridges`.** While `Bridges.dll` is still referenced, a second `Bridges.X` would give CS0433.
- **Swap the `using` in the same change.** Each caller file drops `using Bridges;` when it switches; with both namespaces imported, the shared simple names give CS0104.
- **Keep the public members and the error style the callers rely on**: for example, the `RequestException` field on QuickBooks Online results, or a blank `AccessToken` meaning "cancelled" on Microsoft 365. Unless the spec changes the style deliberately, in the same commit as its callers.
- **Use lazy loading carefully.** Deleting `Bridges.dll`'s own dependencies (Intuit, Serilog, Graph, MSAL 4.47.1) waits until `Bridges.dll` itself is gone (Z2). .NET loads them lazily, but a remaining Bridges type could touch them during static initialization.

After Z2, `Required dlls/Bridges.dll` is deleted and nothing references it.

On `Directory.Build.targets`: its target is generic. It swaps any missing `x*.csproj` ProjectReference for the DLL in `Required dlls`. For Bridges, removing the `xBridges` ProjectReference from `OpenDental.csproj` ends the substitution, and Z2 also drops "Bridges" from the file's comment. The target itself still serves `VirtualWeb`, `ImagingDeviceManager`, `ODApi` and `OpenDentalHelp`. Delete the whole target, and `OpenDental.Local.slnf`, when those are gone too (decoupling plan, Phase 2).

`VBbridges.dll` is a different binary and is out of scope here. So is `UnitTests/BridgesTests.cs`, which tests in-repo bridges (Sirona).

---

## 4. Integrations to recommend removing instead

These are recommendations for the owner, who decided to re-implement each integration. Removal keeps every table, column, enum value and `programproperty` row, for schema compatibility (rule 2). Only UI, logic and binaries go.

| Integration | Recommendation | Reasons |
|---|---|---|
| **CareCredit** | **Remove**, unless Synchrony grants Direct Integration Partner status and its own partner code (the spec's go/no-go gate). Removal is S | <ul><li>Partner-only: the API reference, hosts and sandbox are behind a login, and may not be publishable in GPL source.</li><li>The largest package (L, 15-20 d), and it needs an always-on job host for batch QuickScreen.</li><li>Sends identity, contact, fee and sometimes SSN digits to a lender, with no confirmed BAA.</li><li>Every change to the API interaction needs Synchrony's re-approval.</li><li>The decoupling plan originally cut it.</li><li>The office can still take CareCredit through Synchrony's provider portal and record the payment with the CareCredit payment type.</li></ul> |
| **PostcardMania** | **Remove** (or defer until the vendor confirms a BAA) | <ul><li>A marketing feature, close to ground rule 7's line.</li><li>The public v3 API doesn't match the old flow (partner accounts, SSO, list upload), so the "re-implementation" is a new feature that places paid orders from inside the PMS.</li><li>Patient names and addresses go to a mail house, which needs a BAA; the vendor's public material doesn't mention HIPAA.</li><li>The office can export a patient list with the existing reports or queries and use any mail house, under its own BAA.</li><li>The decoupling plan originally cut it.</li></ul> |
| **PayConnect 1.0 and VeriFone terminals** (`DpsPos.dll`) | **Remove** (PAYCONNECT1-REMOVE) | <ul><li>DentalXChange has decommissioned PayConnect 1.0.</li><li>`DpsPos.dll` is proprietary, with no public documentation or redistribution terms.</li><li>The v1 code carries Open Dental's compiled-in integrator identity.</li><li>PayConnect 2 terminals go through the cloud API instead.</li><li>First confirm the office's "Program Version" in `fox-db` is 2.</li></ul> |
| **Dropbox A to Z storage** | **Remove**, unless `fox-db` shows `AtoZfolderUsed=2` and the owner accepts the trade-offs. Removal is S | <ul><li>It conflicts with ground rule 6 ("patient data and images stay on local storage").</li><li>Developer Terms §2.6(c) bars tokens that let several app users into one account, which is exactly what shared office storage is.</li><li>Imaging goes offline whenever the internet does.</li><li>Local A to Z, or SFTP to the office's own server, plus the office's own backup or sync tool, cover the need with no vendor integration.</li></ul> |
| **NewCrop (Ensora) eRx** | **Remove the NewCrop option** if `fox-db` shows the office doesn't use it; keep the credential deletion and the OID work either way | <ul><li>Partner-only, through Ensora or a reseller, with no public documentation.</li><li>Open Dental points practices new to eRx at DoseSpot, so offering both partner-only eRx vendors doubles the onboarding for no gain.</li><li>The code carries Open Dental's own compiled-in NewCrop credentials, which must go regardless.</li></ul> |
| **Canadian claims (beyond the clean-up)** | **Park** if the office is US-only: ship `canada.md` 4.1 only (0.5 d) | <ul><li>Nothing suggests the office is in Canada.</li><li>Live claims need CDAnet certification of this product (months, outside the code).</li><li>Hygienist claims also need TELUS and ABC vendor certificates.</li></ul> |

Integrations to **keep**, even though some are gated:

- **QuickBooks Online, Microsoft 365, Gmail and USPS**: public, and the decoupling plan names them.
- **PaySimple, PayConnect 2 and EdgeExpress**: the office needs a card processor. Pick the one the office actually uses, and park the others disabled.
- **DoseSpot**: eRx for controlled substances.
- **PDMP**: state mandatory-query laws make one-click checks valuable, even though onboarding is slow.

---

## 5. Shared infrastructure to build once (package F0)

Each spec designed its own helper for the same job, under four different names. F0 builds each one once. The vendor packages use these, and don't add their own copies.

### 5.1 OAuth loopback and PKCE helper

`CodeBase/Utilities/VendorDirect/` (CodeBase has no database or UI dependency, so `OpenDentalCloud`, `OpenDentBusiness` and `OpenDental` can all use it):

- **`Pkce.Create()`**: an RFC 7636 verifier (43-128 characters, `RandomNumberGenerator`) and its S256 challenge. Tested with the RFC's Appendix B vector.
- **`OAuthState.New()`**: 32 random bytes, base64url. Compared in constant time.
- **`LoopbackListener`**: one class replacing the four in the specs (Google's `LoopbackRedirectListener`, Dropbox's `DropboxLoopbackListener`, QuickBooks Online's `QboLoopbackListener`, EdgeExpress's `LoopbackReturnListener`).
  - **Binding.** A `TcpListener`, not `HttpListener`, so there are no http.sys URL reservations. It binds `IPAddress.Loopback`, plus `IPv6Loopback` when the redirect host is `localhost`. The port is 0 (OS-picked) for Google and EdgeExpress, or fixed for Dropbox, QuickBooks Online sandbox and the QuickBooks Online relay port, because those vendors match ports exactly.
  - **Requests.** It accepts exactly one `GET` on the expected path, which can carry a per-session nonce (EdgeExpress). Any other path or method gets 404 and the wait continues. It reads at most 16 KB, and never logs or stores the request.
  - **Result.** It returns `code`, `state`, `error` and any extra parameters (`realmId`). The caller checks `state`.
  - **Lifetime.** A 5-minute default timeout plus a `CancellationToken` wired to the progress window's Cancel. The socket is always released.
  - **Response page.** A fixed, self-contained "finished, you can close this tab" page, with no external assets, scripts or branding (replaces `GoogleAuthCodeResponseHtml`, which loaded opendental.com files).
- **`OAuthRedirectParser.Parse(string)`**: accepts a pasted full URL or a bare query string (Dropbox codes, the QuickBooks Online production paste flow).
- **`SystemBrowser.Open(url)`**: `Process.Start` with `UseShellExecute=true`. Loopback isn't offered under Thinfinity or AppStream (those paths are being removed anyway); the paste flow covers them.

Microsoft 365 uses MSAL's own loopback listener and PKCE, as its spec recommends, but takes the same success and error page text.

### 5.2 Encrypted credential storage in `programproperty`

`OpenDentBusiness/VendorDirect/` plus edits to `ProgramProperties.cs` and `Programs.cs`:

**Rows:**

- **`Programs.InsertIfMissing(ProgramName, progDesc)`** inserts a `program` row with `Enabled=0` and returns whether it inserted. To cover two workstations racing, it re-reads afterwards and deletes the duplicate with the higher `ProgramNum`. Called for the four new program rows: `Microsoft365Email`, `GmailApi`, `USPSAddresses`, `PostcardMania`.
- **`ProgramProperties.InsertIfMissing(programNum, desc, clinicNum, defaultValue, isMasked, isHighSecurity)`** is idempotent and Middle Tier-safe.
- **`ProgramProperties.GetValOrDefault(programNum, desc, clinicNum)`**: the clinic row, else the ClinicNum 0 row, else `""`, and never throws. The existing helpers either throw on a missing row or ignore the clinic (`pdmp.md` 3.2). Every new reader uses this one, so a workstation never fails just because nobody has opened Setup yet.

**Secrets:**

- **`SecretValue.Protect(plain, out stored)`** uses `CDT.Class1.Encrypt` and **refuses to save when it returns false**. Secret rows are always written with `IsMasked=1` and `IsHighSecurity=1`, and editing them needs `ManageHighSecurityProgProperties`.
- **`SecretValue.Read(stored, LegacyPolicy)`** returns `Ok(plain)`, `Blank`, `Unreadable` or `Legacy`:
  - An `osd1$` value goes through `Decrypt`. On failure the result is `Unreadable`, which counts as "not configured", and the setup field shows blank.
  - A value without the prefix is handled per integration:
    - **`AcceptPlaintext`**: keys the office typed, which Open Dental stored in plain text. PaySimple key, PayConnect Secret, EdgeExpress Auth Key, DoseSpot clinic key, NewCrop reseller password. Used as-is, and encrypted on the next save.
    - **`RejectPlaintext`**: tokens or ciphertexts issued under Open Dental. QuickBooks Online tokens, the Gmail refresh token, the Microsoft 365 cache, the Dropbox legacy token, PDMP certificates and passwords, the CareCredit token. Treated as "needs re-authorization", and never sent.

**Concurrency:**

- **`ProgramProperties.UpdateValueIfUnchanged(programPropertyNum, oldValue, newValue)`**: a compare-and-swap `UPDATE ... WHERE PropertyValue=@old`. It works on ciphertexts because `osd1$` is deterministic. Used for the QuickBooks Online rotating refresh token and the CareCredit batch lock. Also usable for the PaySimple poll lease.

**Two fixes to existing behaviour:**

- `ProgramProperties.InsertForClinic` copies rows without their `IsMasked` / `IsHighSecurity` flags. Copy them.
- `ProgramProperties.Sync` deletes rows missing from a form's list. Every setup form calls `InsertIfMissing` before loading its list.

Per-mailbox email tokens stay in the existing `emailaddress.RefreshToken` column (Gmail refresh token, Microsoft 365 MSAL cache), through the same `SecretValue` functions. Office-wide email settings go in `programproperty`.

### 5.3 Token refresh helper

`CodeBase/Utilities/VendorDirect/TokenCache.cs`:

- **In memory only.** Access tokens are never written to the database, logs or disk. The cache is keyed by a SHA-256 fingerprint of the configuration (client ID, environment, and the encrypted refresh token or secret), so a changed setting gives a fresh entry on the next `InvalidType.Programs` refresh.
- **`GetAsync(fetch)`** returns the cached token while it has more than the margin left: 5 minutes by default, 60 seconds for PostcardMania's 15-minute tokens. Otherwise it fetches **single-flight** behind a `SemaphoreSlim`, so a statement batch or a burst of thumbnails makes one refresh.
- **`SendWithAuthRetryAsync`**: on HTTP 401, invalidate, refresh once and retry once. A second 401 is an auth failure.
- **`NeedsReauthorization`** is a sticky flag per process and key, set only by a definite rejection (`invalid_grant`, MSAL "UI required", a 400 or 401 from the refresh). After it, calls fail fast and the user sees **one** message per session. A network error never sets it.
- **Refresh-token persistence** is a strategy the vendor picks:
  - **Rotating** (QuickBooks Online): read the current value from the database before refreshing, write the new one with `UpdateValueIfUnchanged`, and on `invalid_grant` re-read once in case another workstation rotated it.
  - **Non-rotating** (Gmail, Dropbox): never write on refresh.
  - **MSAL cache** (Microsoft 365): write at most once a day per process, or when the account changes. No `Signalods` storm.
  - **Client credentials** (USPS, PostcardMania, DoseSpot, CareCredit): "refresh" means requesting a new token. CareCredit keeps its spec's shared database copy only if the gated guide says token requests are scarce.

### 5.4 HTTP client policy that never contacts Open Dental hosts

`CodeBase/Utilities/VendorDirect/VendorHttp.cs` and `HqHosts.cs`:

- **One static `HttpClient` per vendor and base URL**, behind a `DelegatingHandler` chain. TLS 1.2 or later (already set in `ODInitialize`). `ConfigureAwait(false)` throughout. Synchronous callers block on a pool thread, never the UI thread.
- **`HqHosts.IsBlocked(Uri)`** is the single home of the blocked list: `opendental.com`, `open-dent.com`, `opendentalsoft.com`, `patientviewer.com`, `opendental.cloud` and their subdomains. One allow-list entry in `tools/Test-NoHqHosts.ps1` covers this file. The handler refuses any request to a blocked host. The same check runs on:
  - every URL a setup screen saves (CareCredit, PDMP, EdgeExpress return URL)
  - every URL a vendor hands back before it is opened (PDMP `ViewableReport` and `ReportLink`, the CareCredit IHS page, the PayConnect embed URL)
  - WebView2 navigation in the vendor windows
- **HTTPS only**, except loopback.
- **Optional per-vendor host allow-list** (for example `*.dentalxchange.com`, `*.paygateway.com`), checked by the same handler and by the WebView2 navigation guard.
- **Timeouts per route.** 30 s is the default. Longer where a spec says so: Graph send 100 s, PayConnect terminals 190 s.
- **Retries.** Idempotent GETs only: up to 2 retries with jittered backoff, honouring `Retry-After`. Interactive waits are capped at about 10 s.
- **POSTs that move money or send messages are never retried automatically.** Use the vendor's idempotency key where one exists (QuickBooks Online `requestid`, PostcardMania `extRefNbr`). Otherwise send a correlation ID and reconcile after a timeout (PayConnect `invoiceNumber`, PaySimple `OrderId`, EdgeExpress `ORDERID`). Microsoft `sendMail` and DoseSpot "add patient" are never auto-retried.
- **Circuit breaker.** After a network-level failure, mark the vendor unreachable for 30 s and fail at once. Used by Dropbox and Microsoft 365, and available to all.
- **Log redaction.** Never log `Authorization`, `API-Key`, `Secret`, `Subscription-Key` or `X-Auth-*` headers, request or response bodies, tokens, codes, or URLs that carry PHI (USPS puts the address in the query string). Log the endpoint name, status, vendor error code and vendor request ID.
- **Client certificates** (PDMP) load with `X509KeyStorageFlags.EphemeralKeySet`.
- **No global `ServicePointManager.ServerCertificateValidationCallback`.** The Canada ICD client pins its localhost certificate on its own handler.
- **Neutral `User-Agent` and sender identity** from one `ProductIdentity` constant. It replaces "Open Dental Software" in the LogiCoy request and NewCrop's product name, and Phase 4 branding sets the final value.
- **Middle Tier.** Every vendor call runs on the client (`Meth.NoCheckMiddleTierRole`), as today. The application server never contacts a vendor.
- **Test seams.** Every client takes an `HttpMessageHandler` for tests. `Introspection` base-URL overrides stay for mock servers.

### 5.5 Decisions that override the specs

Where specs disagree, the following wins:

1. **Encryption calls.** Several specs say `TryEncrypt` / `TryDecrypt`: USPS, PostcardMania, CareCredit, PayConnect, PaySimple and EdgeExpress. Use `SecretValue` (5.2) instead.
   - `TryEncrypt` silently returns plain text on failure.
   - `TryDecrypt` returns a corrupted `osd1$` value unchanged, which would then be sent as the password.
2. **`OAuthApplicationNames`.** Keep every member and mark the unused ones obsolete. `dosespot.md` proposes deleting four members; don't, so the ordinals never shift.
3. **Program names.** `Microsoft365Email` (the Microsoft 365 spec's name, not `google.md`'s `Microsoft365Mail` suggestion), `GmailApi`, `USPSAddresses`, `PostcardMania`. All appended at the end of `ProgramName`. Before release, check `fox-db` for clashes with the queries each spec gives.
4. **One listener class** (5.1), not four.
5. **`DentalTek`.** `canada.md` says `FormCarriers` gets it from the prebuilt Bridges assembly. In fact it is in-repo source (`WpfControlsOD/Bridges/DentalTek.cs`, namespace `OpenDental.Bridges`) and doesn't depend on `Bridges.dll`.

### 5.6 Also built once

- **`IProgramLinkSetup` registry** (`OpenDental/Logic/VendorDirect/`). Each new program row registers `ProgName`, `EnsureProgram()` and `OpenSetup(owner)` in its own package's file. `FormProgramLinks` calls `EnsureProgram()` for every registered setup before `Programs.RefreshCache()`, and opens the registered setup on double-click before falling through to its existing `switch`. No vendor package edits `FormProgramLinks.cs`.
- **Vendor background job host.** An `IVendorBackgroundJob` interface (name, interval, `ShouldRun`, `Run`), and one `ODThread` started from `FormOpenDentalThreads`. It runs only the jobs whose program is enabled, one workstation at a time, through a lease row written with `UpdateValueIfUnchanged`. It serves:
  - PaySimple: the ACH status poll
  - EdgeExpress: pending-payment reconciliation
  - CareCredit: batch QuickScreen and result polling, if implemented
  - later, Gmail and Microsoft 365 inbox polling (both specs' open question)

  It is the interim until a local service host exists (the decoupling plan's rule 6).
- **`ProductIdentity`**: the product name and version used in sender fields and `User-Agent`.
- **`tests/VendorDirectTests`**: a test project with a wildcard include, plus a fake `HttpMessageHandler` and a `CDT` round-trip helper for every package.
