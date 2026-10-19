# PDMP and Bamboo Health (prescription monitoring): direct-to-vendor spec

Status: draft, 2026-10-18; fact-checked the same day against the code and the public sources (feasibility confirmed as partner-only). Baseline: `main` at `6855026f5`. Vendors: Bamboo Health (formerly Appriss Health), product PMP Gateway; LogiCoy, product PMPlink. Also covers direct-to-state options (California CURES IEWS, Washington through OneHealthPort).

## Summary

- **The API calls are already direct and in-repo.** Both bridges post XML from the workstation straight to the vendor:
  - `PDMPBamboo` posts to Bamboo PMP Gateway
  - `PDMPLogicoy` posts to LogiCoy

  The code is in `OpenDentBusiness/WebBridges/PDMP/`. Nothing in this integration uses the prebuilt `Bridges.dll`.
- **HQ supplied Open Dental's own integrator identity**, and hid it from the office:
  - per-state LogiCoy URLs
  - the Bamboo Gateway URL
  - Open Dental's Bamboo **client certificate** (a PKCS#12 file used for mutual TLS) and its password

  These were pushed into `programproperty` rows through `EnableAdditionalFeatures` → `HqProgram.Download`. `PDMP.FilterAndSortProperties` hid every `Url` and `Client` property. The LogiCoy request also hard-codes `SenderSoftware Developer="Open Dental Software"`.
- **Feasibility: partner-only.** Neither Bamboo nor LogiCoy publishes its API. To get API documentation, pre-production access, test patients and a client certificate, an EHR vendor has to engage Bamboo and pass a demo review. Offices then request credentials through Bamboo Customer Connect and name their EHR vendor. So this project (or an office acting as its own integrator) has to become a recognized PMP Gateway integrator before the Bamboo bridge can work.
- **Code work is medium (M)**:
  - show and validate the URL properties
  - add a client-certificate import dialog
  - stop sending Open Dental's identity
  - read properties per clinic, with a clinic selector in setup
  - request the Bamboo report link only after the user clicks OK
  - send the clinic's location data instead of the patient's address, and send the SSN only where a state requires it
  - harden HTTP and error handling
  - move the report window to WebView2

  The calendar work is the partner onboarding, which Bamboo itself says can take months.
- **Interim:** both program links stay disabled by default (they already are). With these changes they are fully configurable. They can't work until the office has a URL, Gateway credentials and a client certificate issued for this software. Legacy values that Open Dental wrote are never used.

---

## 1. What the integration does, and what HQ provided

### 1.1 What the office uses it for

A US prescriber has to check the state Prescription Drug Monitoring Program (PDMP) before prescribing controlled substances. The integration replaces "log into the state portal, retype the patient, search" with one toolbar click from the patient's chart.

There are two program links, one per hub. Both are inserted disabled by `ConvertDatabases6.cs` and are chosen by state:

| Program (`ProgName`) | Hub | States (per the Open Dental manual) | What the user sees |
|---|---|---|---|
| `PDMP` | LogiCoy PMPlink | CA, IL, KY, MD, UT, WA | The state report opens in an in-app browser |
| `Bamboo` (renamed from `Appriss` by `ConvertDatabases8.cs`) | Bamboo Health PMP Gateway | All other states | A message box with NarxCare risk scores and messages. **OK** opens the full report through a single-use link |

Runtime flow, from `WpfControlsOD/Logic/ProgramL.cs` (PDMP branch, about lines 303-355) and `OpenDentBusiness/WebBridges/PDMP/*`:

1. The user clicks the button with a patient selected. The user must be linked to a provider.
2. `PDMP.SendData(program,patient,provider)` builds a `PdmpProperty`:
   - the state where the provider is licensed, and the DEA number, from `ProviderClinics`
   - the license number or State Rx ID, per the "PDMP Provider License Field" property
   - the property values, through `LoadPropertiesForProgram`
3. `PdmpProperty.Validate` applies per-state rules:
   - LogiCoy: a dictionary of CA, IL, KY, MD, UT and WA rules (DEA, provider name, license, SSN for KY, address for MD, birthdate, URL)
   - Bamboo: DEA, name, URL, license, birthdate, zip, state, client key and client password
4. The vendor call:
   - **LogiCoy** (`PDMPLogicoy.GetURL`) does one HTTP POST of a `PrescriptionSummaryRequest` (namespace `http://xml.logicoy.com/pdmp/prescription/summary`) with HTTP Basic auth. The response has `Report.ReportRequestURLs.ViewableReport`, a URL that opens in `FormWebBrowser`.
   - **Bamboo** (`PDMPBamboo.DownloadData`) works in two steps. Both use mutual TLS plus digest headers (section 2.1.3).
     - Step 1 POSTs a `PatientRequest` (namespace `http://xml.appriss.com/gateway/v5_1`). The response is a `PatientResponse` holding either a `Report` (`NarxScores`, `Message`, `ReportRequestURLs.ViewableReport`) or `Error`/`Disallowed` items.
     - Step 2 POSTs a `ReportRequest` to the `ViewableReport` URL. The response is a `ReportResponse` whose `ReportLink` is a single-use URL. It opens in `FormWebBrowser` with `IsUrlSingleUse=true`, which hides Refresh.
     - **Both POSTs run inside the `ProgressWin`, before the message box appears.** `GetURL` is called straight after step 1, so the report link has already been requested when the user sees the scores. The box's **OK** only opens a link that already exists, and **Cancel** only skips opening it and writing the audit row (section 3.4 changes this).
5. When the report is opened (not when the user cancels), `RxPats.CreatePdmpAccessLog` writes an `rxpat` row with `RxType` set to `LogicoyAccess` or `BambooAccess`. That row is the office's local access audit. The LogiCoy path has no confirmation box, so it writes the row whenever a URL comes back.

The request/response classes are in-repo, xsd-generated code: `BambooScript.cs` (4,649 lines) and `PDMPScript.cs` (2,146 lines). Unit tests are in `UnitTests/PdmpPropertyTest.cs`, `UnitTests/UnitTests/PDMPTests.cs` and `UnitTests/UnitTests/ProgramPropertiesTests.cs`.

### 1.2 What Open Dental's HQ provided

The inventory row in `hq-vendor-table.md` says per-state PDMP URLs and the Bamboo client key and password were pushed as in-memory program property overrides through `EnableAdditionalFeatures`. The code confirms this and shows a bit more:

- **The mechanism.**
  - `HqProgram.Download()` called `IWebServiceMainHQ.EnableAdditionalFeatures`.
  - It deserialized `ListHqPrograms`. Each `HqProgram` carried a `ListProperties` of `HqProgramProperty` (desc and value).
  - `UpdateCaches()` **wrote those values into the database rows** (`ProgramProperties.Update`).
  - `ProgramProperties.GetHqPropertyValue` also overrode them in memory.

  The download was removed in `9bccbad44`. `HqProgram.GetAll()` is now always empty. The plumbing that remains (`DoUseCacheValues`, `GetHqPropertyValue`) is inert. The manual's requirements that the office be "on support" and run the Open Dental Service came from this path.
- **The values HQ owned**, as inferred from what the filter hides and how the code uses each value:

  | Program | Property rows (existing `PropertyDesc`) | Purpose |
  |---|---|---|
  | `PDMP` | `California PDMP Url`, `Illinois PDMP Url`, `Kentucky PDMP Url`, `Maryland PDMP Url`, `Utah PDMP Url`, `Washington PDMP Url` | LogiCoy endpoint per state |
  | `Bamboo` | `Bamboo Url` | PMP Gateway `PatientRequest` endpoint |
  | `Bamboo` | `Bamboo Client Key` | Base64 PKCS#12 client certificate, CDT-encrypted. `PDMPBamboo.GetCertificate` notes these certificates expire 5 years after creation |
  | `Bamboo` | `Bamboo Client Password` | Password of that PKCS#12, CDT-encrypted |

  `PDMP.FilterAndSortProperties`:
  - hides `*Url*` for both programs
  - hides `*Client*` for Bamboo, because those "are only needed for us to access bridge"
- **The values the office entered:**
  - `PDMP`: `{State} PDMP Username`, `{State} PDMP Password`, `{State} PDMP FacilityID`
  - `Bamboo`: `Bamboo Username`, `Bamboo Password`, `Bamboo FacilityID`
  - both: `PDMP Provider License Field`

  Passwords are CDT-encrypted by `FormProgramProperty`. `FormProgramLinkEdit` opens it in password mode whenever the description contains "Password". On read, `LoadPDMPProps`/`LoadBambooProps` use `CDT.Class1.TryDecrypt`, which returns any value **without** the `osd1$` prefix unchanged.
- **Open Dental's identity in requests.**
  - `PDMPLogicoy.MakeRequester` sends `SenderSoftware` Developer `Open Dental Software`, Product `PDMP-LINK`, Version `v1`.
  - `PDMPBamboo` sends no `SenderSoftware`. Bamboo recognizes the integrator by the client certificate.
- **The kill switch.** HQ could also disable the programs (`IsDisabledByHq`/`CustErr`). That was removed in Phase 1.

**Where that leaves us today:**
- On a new database the URL and certificate rows are blank, so validation fails with "Program url was not set. Please contact support."
- On a database migrated from Open Dental 25.3.48, the rows still hold whatever HQ last wrote. The certificate is in Open Dental's own ciphertext, so the in-repo `CDT.Class1.Decrypt` rejects it ("Security certificate was not set"). Even if it could be decrypted, it is **Open Dental's** integrator credential and must never be used.
- The office's own `Bamboo Password` and `{State} PDMP Password` rows on such a database are also in Open Dental's ciphertext. `TryDecrypt` passes them through unchanged, so today the ciphertext itself would be hashed or sent as the password, and the hub would reject it. Ground rule 3 already says these passwords are re-entered after migration.

In effect the integration is not available.

---

## 2. The vendor's public API

### 2.1 Bamboo Health PMP Gateway

#### 2.1.1 Documentation status

**Not public.** State welcome packets describe the process:

1. The healthcare entity (HCE) requests integration through Customer Connect.
2. If its EHR vendor is not yet integrated, Bamboo sets up a meeting with the vendor.
3. Bamboo gives the vendor the technical API documentation, plus testing instructions and test patients.
4. Bamboo asks the vendor to demo the integration before production.
5. The packets warn this can take several months.

Sources: [SD welcome packet](https://doh.sd.gov/media/hulprg0b/pdmp_ehrintegrationguide_welcomepacket.pdf), [Georgia FAQ](https://dph.georgia.gov/pdmp/faqs) (vendors ask Bamboo Support for the API specifications), [Missouri Gateway welcome packet, 2024](https://pdmp.mo.gov/wp-content/uploads/2024/01/gateway-integration-guide.pdf) (a Bamboo implementation engineer supplies the technical documentation, a test environment and test scenarios to the vendor).

Bamboo's own pages, re-checked on 2026-10-18, show no developer portal, public API reference or partner track: [EHR request](https://bamboohealth.com/ehr-request/) (only Customer Connect is linked), [partnership](https://bamboohealth.com/partnership/), [PMP Gateway](https://bamboohealth.com/solutions/pmp-gateway/) (a demo request only). The Massachusetts packet mentions an "API Version 5.1" that returns an HTML report and a separate ASAP-XML option ([search summary of mass.gov packet](https://www.mass.gov/doc/masspat-ehr-integration-welcome-packet/download); the page returned 403 to our fetch). The Missouri packet says two-way SSL certificates are required with Gateway API 5.1.

Everything below is what is **publicly knowable**. It comes from this repo's own code and from a public GPL-3.0 OpenEMR module that integrates the same gateway ([juggernautsei/oe-bamboo-pmp](https://github.com/juggernautsei/oe-bamboo-pmp); its announcement is at [community.open-emr.org](https://community.open-emr.org/t/bamboo-integration-complete/23315)). The real specification has to come from Bamboo.

#### 2.1.2 Base URLs

| Environment | URL | Status |
|---|---|---|
| Production | Not published. HQ supplied it, and Bamboo gives it at onboarding. Stored in `Bamboo Url` | Must come from Bamboo |
| Pre-production | Bamboo provides a separate test URL to the vendor (Missouri packet). The host `secure.prep.pmpgateway.net` appears only as a hard-coded `Host` header in the public OpenEMR module (`src/Controllers/GatewayRequests.php`); the request URL itself is passed in | Unverified; confirm with Bamboo |
| Report step | The absolute URL returned in `Report/ReportRequestURLs/ViewableReport` | Dynamic, per response |
| Report display | The `ReportLink` returned by the report step | Single-use. `FormWebBrowser` comments that a refresh gets "unauthorized" |

#### 2.1.3 Authentication (step by step)

There is no OAuth, browser login or token. Every request carries two layers:

1. **Mutual TLS.** The client presents the integrator's X.509 certificate, delivered as PKCS#12 (`.pfx`/`.p12`) with a password. Missouri's packet confirms two-way SSL is mandatory for API 5.1. The OpenEMR module keeps its `.pfx` in a git-ignored `certificate/` folder and keeps credentials out of source control. Our code's comment says the certificate is generated per Bamboo's documentation and expires after 5 years.
2. **Digest headers**, computed with the HCE's Gateway username and password:
   - `X-Auth-Username`: the Gateway username
   - `X-Auth-Nonce`: a fresh random value with no `:`. We use a GUID, and OpenEMR uses 16 random bytes in hex
   - `X-Auth-Timestamp`: Unix time in seconds (UTC)
   - `X-Auth-PasswordDigest`: lowercase hex SHA-256 of `password:nonce:timestamp`

   `BambooAuth.GetPasswordDigest` already does this, and `PDMPTests.PDMPBamboo_PasswordDigest` covers it. One detail: it hashes `Encoding.Default` bytes. It should hash UTF-8, which is identical for ASCII passwords.
3. Every request is an HTTP POST with an XML body and `Accept: application/xml`.

There is nothing to refresh. The nonce and timestamp are fresh on each request, and the certificate is static until it expires.

#### 2.1.4 Endpoints and fields we need

All fields below exist in the in-repo `BambooScript.cs` (schema `v5_1`).

| Step | Request root | Fields we send | Response root | Fields we read |
|---|---|---|---|---|
| 1. Patient query (POST `Bamboo Url`) | `PatientRequest` | `Requester`: `SenderSoftware` (Developer, Product, Version; **to add**), optionally `LicenseeRequestId` and `RequestDestinations/Pmp` (state codes), `Provider` (Role=Dentist, FirstName, LastName, DEANumber, NPINumber, `ProfessionalLicenseNumber` Type, Value, StateCode), `Location` (Name=FacilityID, DEANumber, NPINumber, Address/StateCode). `PrescriptionRequest/Patient`: Name (First, Middle, Last), Birthdate, SexCode, Address (Street, City, StateCode, ZipCode) | `PatientResponse` | `Report`: `NarxScores[]` (ScoreType, ScoreValue), `NarxMessages[]`, `MandatoryReview[]`, `Message`, `ReportExpiration`, `ReportRequestURLs/ViewableReport` (and `StorableReport`). Or `Error` (Message, Details, Source), or `Response[]` with `Disallowed`/`Error` |
| 2. Report link (POST `ViewableReport` URL) | `ReportRequest` | `Requester`: Provider (as above) and Location (Name, DEA, NPI, Address). This requester type has **no** `SenderSoftware`, `LicenseeRequestId` or `RequestDestinations`; its only other element is an optional `ReportLink` token | `ReportResponse` | `ReportRequestId`; `Items`: `ReportLink` (anyURI, single-use), or `Error`/`Disallowed`/`ExternalLinks` |

The schema also defines optional patient fields: Phone, SSN, DriversLicenseIdentifier, MedicalRecordID and benefits IDs. We send none of them to Bamboo today, and adding them is not needed.

Mapping problems in today's requests, from the code:
- `MakeReportRequest` fills `Location/Address` with the **patient's** street, city and zip. `MakeRequester` sends only the state.
- Both requests fill `Location/DEANumber` and `Location/NPINumber` with the **provider's** DEA and NPI. Missouri's packet defines the facility identifier as the facility's NPI or DEA, and lists facility name, identifier and state among the fields audited per request.
- The fix is to send the clinic's (or practice's) address and identifiers, and to confirm the rules with Bamboo (open question 2).

The "disallowed" responses are business outcomes, not faults. Examples are "no unique patient" and "permissions problem". The user then has to search in the state portal ([NJ integration guide](https://bamboohealth.com/wp-content/uploads/2022/04/NJPMPIntegrationGuide.pdf)).

#### 2.1.5 Rate limits

None are published. The use is interactive: one patient per click, with no batch or bulk search. Bulk search is excluded from integrations, per the NJ guide.

#### 2.1.6 Terms that matter

- **Who can use it.**
  - Every prescriber must hold an active account in the state PMP. Searches fail for providers who aren't registered ([SD](https://doh.sd.gov/media/hulprg0b/pdmp_ehrintegrationguide_welcomepacket.pdf), [NJ](https://bamboohealth.com/wp-content/uploads/2022/04/NJPMPIntegrationGuide.pdf)).
  - Some states exclude delegates from integrated access; SD, for example, keeps licensed delegates on the web portal. Dentists are an eligible role in SD.
  - Each query must carry the provider's name, license, DEA and NPI.
- **Who signs what.**
  - The HCE creates a Customer Connect account at <https://connect.bamboohealth.com/>, submits the request and signs the agreements there ([Alabama](https://www.alabamapublichealth.gov/pharmacy/ehr-integration.html)). In NJ the state's Terms & Conditions are signed through HelloSign. WRS describes accepting a EULA when the licensee profile is submitted ([WRS Health guide](https://elearning.wrshealth.com/?p=10438)).
  - The state PDMP administrator approves the request. WRS says this typically takes 2-4 weeks. SD says that, for a vendor that is already integrated, Bamboo creates production credentials in 4-5 business days, and the state then approves them.
  - Credentials go to the HCE **or to its EHR vendor**, according to the vendor's onboarding instructions.
- **The vendor gate.** Bamboo provides the API documentation to the EHR vendor, asks for a demo before production (state staff may attend), and approves the integration ([SD](https://doh.sd.gov/media/hulprg0b/pdmp_ehrintegrationguide_welcomepacket.pdf)). An office can only name a vendor that Bamboo knows.
- **Changing EHR vendor.** Missouri's packet says a new vendor receives new credentials, but the HCE does not need fresh state approval ([MO](https://pdmp.mo.gov/wp-content/uploads/2024/01/gateway-integration-guide.pdf)).
- **Funding varies by state.**
  - Funded: Minnesota's statewide license, funded since September 2019 with no end date given ([MN](https://mn.gov/boards/pharmacy-pmp/integration/)); Alabama, free to providers ([AL](https://www.alabamapublichealth.gov/pharmacy/ehr-integration.html)); Georgia, which pays license and implementation fees ([GA](https://dph.georgia.gov/pdmp/faqs)).
  - Texas restored state funding on 2023-09-01, described as covering **two years** ([TMA](https://www.texmed.org/TexasMedicineDetail.aspx?id=62741)). Whether it was extended past September 2025 is unverified, so check with the Texas State Board of Pharmacy.
  - Non-funded states charge the practice; the fees are not public ([WRS](https://elearning.wrshealth.com/?p=10438)). Before 2023, TMA cited about $50 per prescriber per year for Gateway, plus more for NarxCare; treat those figures as dated ([TMA](https://www.texmed.org/TexasMedicineDetail.aspx?id=58335)). Both TMA pages returned 404 to our fetch on 2026-10-18, although search engines still index them.
- **PHI and BAA.**
  - The office is the covered entity. It sends patient demographics to the state PDMP's operator under state law and the agreements it signs in Customer Connect.
  - This project's software runs on the office's machines and **never receives PHI**, so no project-to-office BAA is needed.
  - Bamboo announced HITRUST CSF certification covering PMP Gateway among other products ([press release](https://bamboohealth.com/press-release/bamboo-health-achieves-hitrust-csf-certification-to-further-mitigate-risk-in-third-party-privacy-security-and-compliance)). Its current status is unverified.
  - Whether Bamboo requires a BAA or another agreement from the integrator is an open question (section 7).
- **Clinical.** NarxCare is decision support and does not replace clinical judgment. Bamboo may ask for display changes at the demo. Some features stay in the state portal: delegates, partial-name search, multi-match, history and interstate options (NJ guide).
- **Support.** The Gateway help center is at <https://pmpgateway.zendesk.com/hc/en-us> (it returned 403 to our fetch; the Missouri packet points support requests there). Email: `pdmpintegrations@bamboohealth.com` (pre-production, funded states) and `accountsupport@bamboohealth.com` (pre-production, non-funded states) ([WRS](https://elearning.wrshealth.com/?p=10438)).

### 2.2 LogiCoy PMPlink (the `PDMP` program)

- **Status: not public.** LogiCoy's product page names the integration options (PMIX, NCPDP, FHIR) and the security options (OAuth2, JWT, two-way mutual SSL), but publishes no API and names no states ([PMPlink](https://www.logicoy.com/pmplink-your-solution-for-efficient-prescription-drug-monitoring/), re-checked 2026-10-18). The Pennsylvania-hosted "XML Implementation Guide" that search results list no longer resolves.
- **Practice credentials.**
  - The practice registers with LogiCoy and **selects its EHR vendor and version** in the form.
  - LogiCoy issues a practice-wide web-service username and password. These are not the state PMP login ([NextGen KB](https://www.community.nextgen.com/mtknowledge/articles/Knowledge/PDMP-Registration-with-LogiCoy)).
  - The Open Dental manual sends CA, KY, MD, UT and WA offices to `https://signup.logicoy.com/PDMPSystemEhrSignup/register`, and Illinois offices to the ILPMP PMPnow registration ([OD manual, PDMP bridge](https://www.opendental.com/manual/bridgepdmpillinois.html)). On 2026-10-18 the first host did not resolve and the ILPMP page returned 404, so get the current links from LogiCoy (`pdmp@logicoy.com`, `info@logicoy.com`).
- **What we know from our code:**
  - XML POST to a per-state URL
  - HTTP Basic auth with the practice credentials
  - `PrescriptionSummaryRequest` → `PrescriptionSummaryResponse`
  - the response carries `Report/ReportRequestURLs/ViewableReport` or `Error/Message`
  - `Requester/SenderSoftware` identifies the EHR product, and `RequestDestinations` lists the state
  - `MakePrescriptionRequest` sends the patient's **SSN in every request**, whatever the state, although only the KY validator requires it. Send it only where the state requires it (data minimization).
- **States and funding.** The state list has drifted since our code was written:
  - NextGen lists LogiCoy-only states as IL, MD, NE and WA, and lists CA, KY, PA and UT under both hubs
  - Illinois is state-funded; other states are charged per provider ([NextGen KB](https://www.community.nextgen.com/mtknowledge/articles/Knowledge/PDMP-Registration-with-LogiCoy))

### 2.3 Direct-to-state options with public specifications (not in the current code)

Two states publish integration specs that a future adapter could target without a hub. Both carry NCPDP SCRIPT `RxHistoryRequest`/`RxHistoryResponse`, not the Bamboo or LogiCoy XML. Both return the history data itself rather than a viewer link, so we would have to render the report.

- **California CURES IEWS** ([DOJ page](https://www.oag.ca.gov/cures/iews), [onboarding](https://www.oag.ca.gov/sites/all/files/agweb/pdfs/pdmp/cures-iews-onboarding.pdf), [NCPDP 2023011 guide, rev. 02/2026](https://oag.ca.gov/system/files/media/cures_Info_exchange_web_service_implementation_guide_NCPDP_2023011.pdf), [MOU](https://www.oag.ca.gov/system/files/media/cures-iews-mou.pdf)):
  - Requires an MOU with DOJ and a $1,500 connectivity fee; the maintenance fee is waived until further notice.
  - RESTful endpoints (`/iews/patients` for search, `/iews/prescriptions` for a patient picked from a multi-match list) carrying SCRIPT XML.
  - Mutual TLS with a client certificate signed from the entity's CSR; the CSR's common name becomes the CURES login ID. Testing goes through a DOJ test harness.
  - Supports SCRIPT 2017071 and 2023011 (added from July 2025); 2017071 is decommissioned 2027-12-31.
  - Alternatively, use an approved IEWS entity, which needs no MOU.
  - The guide's revision record notes that from 2026-05-01 most CA-signed certificates no longer include the Client Authentication EKU, which IEWS had required.
- **Washington through the OneHealthPort HIE** ([implementation guide, Feb 2026](https://www.onehealthport.com/sites/default/files/content-uploads/documents/OHP%20HIE%20PMP%20API%20IG%20February%202026.pdf), re-checked 2026-10-18):
  - OneHealthPort issues the organization a JWT that is valid for 6 months.
  - The client exchanges it with grant type `urn:ietf:params:oauth:grant-type:jwt-bearer` at `https://{uat|prd}-v2-onehealthport-api.axwaycloud.com/ohp/oauth/jwt/token` for a 1-hour bearer token.
  - It then POSTs SCRIPT XML to `/ncpdp_requests` with the `x-doc-type: PMP`, `x-org-facility-id` and `x-ref-id` headers.
  - Separate UAT and production tokens; 401 means the token expired, and 503 means maintenance.

These are a separate, later decision (open question 7). This spec keeps the two existing hub bridges.

---

## 3. Desktop design

### 3.1 Auth flow

No interactive flow is needed. Loopback redirect, PKCE and device code don't apply, because neither hub uses OAuth for this API.

- **Bamboo:**
  - mutual TLS with the client certificate
  - the four `X-Auth-*` headers computed per request from the stored Gateway username and password (section 2.1.3)
  - two sequential POSTs on a background thread inside the existing `ProgressWin`
- **LogiCoy:** HTTP Basic over TLS with the stored per-state username and password.
- **Fixed rules:**
  - HTTPS only. Reject any `http://` URL except `localhost` in debug and unit-test builds.
  - TLS 1.2 or later. `CodeBase/Utilities/ODInitialize.cs` already ORs in `Tls12`.
  - Certificate validation stays on.

### 3.2 Where credentials and settings live

Settings live in `programproperty` rows of the existing `Bamboo` and `PDMP` programs. There is no schema change and no ConvertDatabases step.

Missing rows are inserted at runtime by a new `PdmpProperty.EnsureProperties(Program)`. It runs when the setup window opens and before the first use. It inserts with `ClinicNum=0`, a blank value, and `IsMasked=true` for secrets.

Reads become clinic-aware: use the clinic's own row if one exists, else the `ClinicNum=0` row, and return `""` when neither exists. **No existing helper does all of that:**
- `GetPropVal(programNum,desc,clinicNum)` matches the exact `ClinicNum` only, with no fallback to clinic 0. It returns `""` when nothing matches.
- `GetPropValForClinicOrDefault(programNum,desc,clinicNum)` falls back to clinic 0, but throws `NullReferenceException` when the row is missing.
- `GetPropVal(programNum,desc)`, used today, ignores `ClinicNum` (it returns whichever row the cache yields first) and throws on a missing row.

So add a small null-safe `PdmpProperty.GetVal(programNum,desc,clinicNum)` built on `GetListForProgramAndClinicWithDefault`. `PDMP.SendData` already passes `Clinics.ClinicNum` into `LoadPropertiesForProgram`; only `LoadPDMPProps`/`LoadBambooProps` drop it. Bamboo issues credentials per HCE location (Missouri's packet covers facilities in several states), so multi-clinic offices need this. `ProgramProperties.InsertForClinic` already exists; it copies **every** clinic 0 row of the program to each listed clinic.

**Bamboo (`ProgName='Bamboo'`):**

| PropertyDesc | Exists? | Stored as | Supplied by | Notes |
|---|---|---|---|---|
| `Bamboo Url` | yes | plain | Bamboo, at onboarding | Now **visible and editable**. Validated as an absolute `https` URL, and not an Open Dental host |
| `Bamboo Username` | yes | plain | Bamboo, per HCE | |
| `Bamboo Password` | yes | `osd1$` (`CDT.Class1.Encrypt`) | Bamboo, per HCE | Already encrypted by `FormProgramProperty` |
| `Bamboo FacilityID` | yes | plain | office | Sent as `Location/Name` |
| `Bamboo Client Key` | yes | `osd1$` of the base64 PKCS#12 bytes | integrator certificate (section 5) | Written **only** by the new import dialog. Hidden from the generic grid, which shows a status row instead |
| `Bamboo Client Password` | yes | `osd1$` | with the certificate | Written by the import dialog |
| `Bamboo Client Certificate Thumbprint` | **new** | plain | office IT | Optional alternative to the DB blob: find the certificate in `CurrentUser\My`, then `LocalMachine\My`. Lets IT deploy a non-exportable key |
| `Bamboo Sender Developer`, `Bamboo Sender Product` | **new** | plain | Bamboo, if it assigns values | Blank means use the built-in product identity (section 4). `Version` always comes from the assembly version |
| `PDMP Provider License Field` | yes | plain | office | `StateLicense` or `StateRxID` (existing combo box) |

**LogiCoy (`ProgName='PDMP'`):**

| PropertyDesc | Exists? | Stored as | Notes |
|---|---|---|---|
| `{State} PDMP Url` (CA, IL, KY, MD, UT, WA) | yes | plain | Now **visible**, with the same validation as `Bamboo Url` |
| `{State} PDMP Username`, `{State} PDMP FacilityID` | yes | plain | |
| `{State} PDMP Password` | yes | `osd1$` | |
| `PDMP Sender Developer`, `PDMP Sender Product` | **new** | plain | LogiCoy matches the registered EHR product; blank means the built-in identity |
| `PDMP Provider License Field` | yes | plain | |

**Legacy values from a migrated Open Dental database:**
- A `Bamboo Client Key` or `Bamboo Client Password` without the `osd1$` prefix (`CDT.Class1.IsEncrypted` is false) is treated as **not set**. It is never passed to `X509Certificate2`, and the setup window says "Not set: import the certificate issued for this software."
- The same applies to a non-empty `Bamboo Password` or `{State} PDMP Password` without the `osd1$` prefix. `FormProgramProperty` always stores the `osd1$` format, so such a value can only be Open Dental's ciphertext. Read it as blank instead of passing it through `TryDecrypt`, and show "Re-enter the password" (ground rule 3).
- URLs are kept, but the setup window marks them "carried over, confirm with your vendor". The office should replace them with the URL in its own onboarding papers.

**Security note.** `osd1$` is obfuscation with a key that is public in this repo (`CDT/Class1.cs`, `CDT/SecretBox.cs`). Anyone who can read the database and the source can recover the PKCS#12 and its password. Mitigations:
- prefer the certificate-store option (thumbprint, non-exportable key)
- set `IsHighSecurity` on the certificate rows, so edits are logged (`FormProgramProperty` already does this)
- treat database read access as access to the certificate

Section 7 covers the custody question in full.

### 3.3 Setup UX (vendor-neutral, ground rule 7)

Setup stays in **Setup > Program Links > Bamboo / PDMP**, in `FormProgramLinkEdit`, which already special-cases both programs. The programs remain disabled by default, and their toolbar buttons appear only when enabled. Changes:

- **Grid.**
  - Show the URL rows.
  - For Bamboo, replace the two `Client` rows with one read-only row, "Client certificate", showing the subject, issuer, expiry and thumbprint, or "Not set".
  - **Clinic selector.** `FillGrid` loads rows with `ProgramProperties.GetForProgram`, which returns the rows of **every** clinic, and the grid has no clinic column. Once per-clinic rows exist, the grid would list the same description several times with no way to tell them apart. When clinics are enabled, add a clinic combo for these two programs ("Default" = clinic 0) and fill the grid from `GetListForProgramAndClinicWithDefault`. Editing a row that is still inherited from the default inserts a row for the selected clinic.
- **"Client certificate..." button** (Bamboo only). Opens the new `FormPdmpClientCertificate`:
  1. The user picks a `.pfx`/`.p12` file and enters its password.
  2. The dialog loads the file with `X509KeyStorageFlags.EphemeralKeySet`. That flag is in .NET 4.7.2+, so the private key never touches the machine key store.
  3. It checks that the certificate has a private key and is inside its validity dates. If an EKU extension is present, it must include Client Authentication (`1.3.6.1.5.5.7.3.2`).
  4. It base64-encodes the file and saves it with `CDT.Class1.Encrypt` into `Bamboo Client Key`, and the password into `Bamboo Client Password`.
  5. It writes a `SecurityLog` entry under `ManageHighSecurityProgProperties`.
  6. Alternatively, the user can pick a certificate from the Windows store; only the thumbprint is saved.
  7. "Remove" clears all three rows.
- **"Check setup" button.** Validates without sending patient data:
  - required rows present, URL is HTTPS and not a blocked host
  - the certificate loads and expires in more than 30 days (warning) or has expired (error)
  - the hub host resolves and a TLS handshake with the client certificate succeeds. It sends a bodyless `GET` to the URL's origin, and any HTTP status counts as reachable
- **Wording.** Describe what the integration does and nothing more. No sign-up links, no logos. Replace "Please contact support." with "Set it in Setup > Program Links > {program}."

### 3.4 Request handling

- One shared helper, `PdmpHttp`, used by both bridges:
  - `System.Net.Http.HttpClient` over a `WebRequestHandler` with `ClientCertificates` (Bamboo only)
  - 30-second timeout
  - `Accept`/`Content-Type: application/xml`
  - UTF-8 body
  - a fresh nonce and timestamp per request
- The certificate is loaded per click with `EphemeralKeySet` from the DB blob or from the store by thumbprint. That drops today's `MachineKeySet`, which needs write access to the machine key container and fails for some non-admin Windows users.
- `SenderSoftware` is filled from `PdmpProperty` (override rows, else the built-in identity) in the LogiCoy request and in the Bamboo `PatientRequest`. The Bamboo `ReportRequest` has no such element. Open Dental's name is **never** sent.
- **Bamboo step order.** Run only step 1 inside the `ProgressWin`. Do step 2 (the `ReportRequest`) only after the user clicks **OK**, then open the link at once. That way Cancel produces no report request at the PMP, and the single-use link can't expire while the message box is open.
- **Missing report URL.** If the `Report` has no `ReportRequestURLs/ViewableReport`, skip step 2 and show the message. Today `GetURL` dereferences `report.ReportRequestURLs.ViewableReport.Value` unconditionally, which throws `NullReferenceException` on a message-only report (for example, no match).
- **Location fields.** Send the clinic's (else the practice's) address and its facility NPI or DEA in `Location`, not the patient's address or the provider's numbers (section 2.1.4).
- Responses deserialize into the existing `BambooScript`/`PDMPScript` types. Keep the `xsi:type` stripping fallback.

### 3.5 Refresh handling

There are no tokens, so nothing to refresh.

- **Certificate lifetime:**
  - "Check setup" and every click warn when fewer than 30 days remain
  - an expired certificate blocks the call with a message naming the expiry date
- **Per-request freshness:** a new nonce and timestamp every call.
- **Clock skew:** if the gateway rejects the digest, the message tells the user to check the workstation clock. The tolerance isn't published (open question 3).
- **Report link:** single-use and short-lived (`ReportExpiration`). It is opened immediately and never cached or reused. Refresh stays hidden.

### 3.6 Errors and offline behaviour

| Condition | Detected as | User message (no PHI in logs) | Retry |
|---|---|---|---|
| No network, DNS failure, timeout | `HttpRequestException`/`TaskCanceledException`; `WebExceptionStatus` NameResolutionFailure, ConnectFailure or Timeout | "The prescription monitoring service could not be reached. Use your state PMP website." | None automatic; the user clicks again. A duplicate query would show up in the state's audit |
| TLS or client certificate rejected | `TrustFailure`/`SecureChannelFailure`, or HTTP 403 before any XML | "The client certificate was rejected or has expired. Check Setup." | No |
| 401 | HTTP 401 | "The username or password was rejected." | No |
| 5xx, 503 | HTTP status | "The service is temporarily unavailable." | No |
| `Disallowed` | Response item | The vendor message, plus "Search for this patient on your state PMP website." | No |
| `Error` | Response item | Message, details, source | No |
| Malformed XML | Deserialize failure | "Unexpected response from the service." | No |

Behavior to fix along the way:
- `PDMPLogicoy.Request` dereferences `wex.Response` when there is no response (DNS or connect failure) and throws `NullReferenceException`.
- Both bridges put raw response bodies, or `JsonConvert.SerializeObject(response)`, into exception text, which can end up in logs. Show only the status and the vendor's error fields.
- Remove the `ODBuild.IsDebug()` branch in `PDMPBamboo.Request` that tries `Convert.ChangeType` on a `WebResponse`.
- `PDMPBamboo.GetURL` throws `NullReferenceException` when the report carries no `ReportRequestURLs` (section 3.4).
- In `ProgramL`, a Bamboo result with neither a message nor a URL shows an empty message box. Show "No report was returned" instead.

Nothing runs at startup or in the background, so offline use of the rest of the program is unaffected (ground rules 5 and 6). The `rxpat` audit row is still written only when a report actually opens.

### 3.7 Report window

`ProgramL` opens `EnumFormName.FormWebBrowser`. That is the IE/MSHTML control, whose own constructor comments say not to use it and to use `FormWebView` instead. Switch both bridges to `EnumFormName.FormWebView` (WebView2), which already supports `UrlBrowseTo` and `IsUrlSingleUse`. Modern report pages and NarxCare visualizations may not render in MSHTML.

### 3.8 Hosts contacted

Only the hosts the office configured: the `Bamboo Url` host, the `ViewableReport` and `ReportLink` hosts Bamboo returns, and the per-state LogiCoy URL hosts. There are no Open Dental hosts and no HQ.

The URL guard rejects the same five domains, and their subdomains, that `tools/Test-NoHqHosts.ps1` checks: `opendental.com`, `open-dent.com`, `opendentalsoft.com`, `patientviewer.com` and `opendental.cloud`. Watch out: that script scans string literals in `.cs` files, so a host list written into `PdmpProperty.cs` would itself fail it. Put the list in **one shared helper** (for example `CodeBase` `HqHosts.IsBlocked(Uri)`, usable by other bridges too), and give that one file an allow-list entry in the script with its reason. Apply the guard to `ViewableReport` and `ReportLink` too, not only to the configured URL.

---

## 4. Code changes

### 4.1 Files and members

| File | Member | Change |
|---|---|---|
| `OpenDentBusiness/WebBridges/PDMP/PDMP.cs` | `FilterAndSortProperties` | Stop hiding `Url` rows. For Bamboo, hide only `Bamboo Client Key`/`Bamboo Client Password`, which the dialog manages. Update the summary, which says the URL is managed by HQ |
| | `SendData` | Call `EnsureProperties` first. It already passes `Clinics.ClinicNum` to `LoadPropertiesForProgram` |
| `OpenDentBusiness/WebBridges/PDMP/PdmpProperty.cs` | constants | Add `BambooClientCertThumbprint`, `BambooSenderDeveloper`, `BambooSenderProduct`, `PdmpSenderDeveloper`, `PdmpSenderProduct` |
| | `EnsureProperties(Program)` (new) | Runtime insert of missing rows |
| | `LoadPropertiesForProgram`, `LoadPDMPProps`, `LoadBambooProps`, new `GetVal` | Pass `clinicNum` down; clinic-aware, null-safe reads with fallback to clinic 0 (section 3.2). Treat Client Key, Client Password and office Password values without `osd1$` as blank. Read the thumbprint and sender overrides |
| | `TryValidateUrl` | Absolute HTTPS URL, not a blocked host (shared helper, section 3.8); new message |
| | `TryValidateKey`, `TryValidatePassword` | Accept the thumbprint as an alternative; check expiry; new messages. Note: they currently skip under `Introspection.IsTestingMode`; keep that |
| | `SenderDeveloper`, `SenderProduct`, `SenderVersion` (new) | Overrides, else the built-in identity |
| `OpenDentBusiness/WebBridges/PDMP/PDMPBamboo.cs` | `GetCertificate` | Use `EphemeralKeySet`; add the store lookup by thumbprint; clearer errors |
| | `Request<T>` | Move to `PdmpHttp`; error mapping per section 3.6; no PHI in exceptions |
| | `MakeRequester` | Fill `Requester.SenderSoftware`. Send the clinic's address and facility identifiers in `Location` |
| | `MakeReportRequest` | No `SenderSoftware` here (the schema has none). Replace the patient's address and the provider's DEA and NPI in `Location` with the clinic's |
| | `DownloadData`, `GetURL` | Split into `QueryPatient` (step 1) and `GetReportLink` (step 2, called by `ProgramL` after OK). Null-check `ReportRequestURLs`. Drop the `JsonConvert.SerializeObject` dumps |
| | `BambooAuth.GetPasswordDigest` | Hash UTF-8 bytes |
| | usings | Drop `Health.Direct.Common.Extensions`; use `string.IsNullOrEmpty` |
| `OpenDentBusiness/WebBridges/PDMP/PDMPLogicoy.cs` | `MakeRequester` | Replace `"Open Dental Software"`/`"PDMP-LINK"`/`"v1"` with `PdmpProperty.Sender*` |
| | `MakePrescriptionRequest` | Send `SSN` only for states whose validator requires it (KY today) |
| | `Request<T>` | Move to `PdmpHttp`; fix the null `wex.Response` crash |
| `OpenDentBusiness/WebBridges/PDMP/PdmpHttp.cs` (new) | `Post<T>(url,body,headers,cert)` | Shared HTTP, timeout, host guard, error mapping |
| `OpenDentBusiness/WebBridges/PDMP/PdmpCertificate.cs` (new) | `Import(bytes,password)`, `Load(PdmpProperty)`, `Describe(cert)` | PKCS#12 validation, EKU and expiry checks, load from blob or store |
| `OpenDentBusiness/` (shared) | product identity constant | A single place for the product's developer and product name. Phase 4 branding sets the final value; until then use a neutral placeholder |
| `OpenDental/Forms/FormProgramLinkEdit.cs` (+ designer) | `FillGrid`, new buttons, clinic combo | Certificate status row; "Client certificate..." (Bamboo) and "Check setup" (both) buttons; a clinic selector for these two programs when clinics are on (section 3.3) |
| `CodeBase` (shared, new) + `tools/Test-NoHqHosts.ps1` | `HqHosts.IsBlocked(Uri)`; one allow-list entry | Single home for the blocked-host list (section 3.8) |
| `OpenDental/Forms/FormPdmpClientCertificate.cs` (new, + designer, resx) | — | Import, store-pick and remove dialog (section 3.3) |
| `WpfControlsOD/Logic/ProgramL.cs` | PDMP branch (~303-356) | Open `FormWebView` instead of `FormWebBrowser` for both bridges. For Bamboo, request the report link only after OK (section 3.4). New messages; keep `RxPats.CreatePdmpAccessLog` |
| `OpenDentBusiness/Data Interface/ProgramProperties.cs` | `FilterProperties` comment; `DoUseCacheValues`, `GetHqPropertyValue` | Comment fix. Optionally delete the inert HQ override path, which is Phase 3 dead weight |
| `OpenDentBusiness/WebBridges/HqProgram.cs` | whole class | Not required here. Delete in Phase 3 together with `GetHqPropertyValue` |
| `OpenDentBusiness/Misc/Introspection.cs` | `PDMPTest*`, `BambooTest*` | Keep. These are developer overrides that read the local `IntrospectionItems` pref and have nothing to do with HQ |
| Tests | `UnitTests/PdmpPropertyTest.cs`, `UnitTests/UnitTests/PDMPTests.cs`, `UnitTests/UnitTests/ProgramPropertiesTests.cs` | Add the tests in section 6 |

### 4.2 Prebuilt `Bridges.dll` types to re-implement

**None.** The PDMP integration never used `Bridges.dll`. The XML types are the in-repo, xsd-generated `BambooScript` (`http://xml.appriss.com/gateway/v5_1`) and `PDMPScript` (`http://xml.logicoy.com/pdmp/prescription/summary`). Keep them as they are. Regenerate them only if Bamboo's current documentation says the schema has moved past `v5_1`.

### 4.3 "Not available" stubs that get replaced

**None dedicated.** HQ's supply path was deleted outright, not stubbed: `HqProgram.Download` and `DownloadListHqPrograms` are gone. `WebServiceMainHQNoHq.EnableAdditionalFeatures` keeps returning "not available", and nothing in PDMP calls it. What changes is:
- the hidden-property behavior (`PDMP.FilterAndSortProperties`)
- the "contact support" validation messages (`PdmpProperty.TryValidateUrl`, `TryValidateKey`, `TryValidatePassword`)
- the certificate source, which moves from HQ-written rows to an office-imported or store certificate

---

## 5. Credentials and agreements the office must get

### 5.1 Once, by whoever acts as the integrator (the project, or an office that integrates for itself)

This is the partner gate. Until it is done, the Bamboo bridge cannot work for anyone.

1. Contact Bamboo as an EHR vendor that is not yet integrated. Ask for the PMP Gateway API specification, pre-production access and test patients. Contacts: `pdmpintegrations@bamboohealth.com`, the help center at <https://pmpgateway.zendesk.com/hc/en-us>, and <https://bamboohealth.com/contact/>. ([GA FAQ](https://dph.georgia.gov/pdmp/faqs), [SD packet](https://doh.sd.gov/media/hulprg0b/pdmp_ehrintegrationguide_welcomepacket.pdf))
2. Sign whatever vendor agreement or NDA Bamboo requires. Ask explicitly:
   - whether the client certificate is per vendor or can be issued **per HCE**
   - which `SenderSoftware` values to send
   - the current schema version
   - the production and pre-production URLs
   - the timestamp tolerance
3. Generate the key pair or CSR as Bamboo instructs and receive the pre-production client certificate. Never commit it. Store it per section 3.2.
4. Build and test against pre-production with Bamboo's test patients (section 6.3).
5. Demo the integration to Bamboo, and possibly to a state PDMP, before production approval.
6. Get listed as an integrated vendor, so offices can name this product in Customer Connect.
7. LogiCoy, likewise: email `pdmp@logicoy.com` or `info@logicoy.com` and ask:
   - to be added as an EHR product in their registration form
   - for the per-state endpoint URLs and the `SenderSoftware` values
   - whether Basic auth is still accepted, given that their page lists OAuth2, JWT and mutual SSL ([PMPlink](https://www.logicoy.com/pmplink-your-solution-for-efficient-prescription-drug-monitoring/))

### 5.2 Per office (each healthcare entity or location)

1. Make sure every prescriber who will use the button has an active account in the state PMP. Note any portal-login frequency rules; for example, the Open Dental manual notes LogiCoy states need a portal login every 90 days.
2. Check eligibility and funding for the state: Bamboo's map at <https://bamboohealth.com/ehr-request/>, or the state PDMP page.
3. **Bamboo states:**
   1. An authorized decision maker creates an account at <https://connect.bamboohealth.com/> and enters the locations and states.
   2. They select this product as the EHR. That only works once step 5.1.6 is done.
   3. They sign the agreements, EULA and state terms.
   4. In non-funded states, they contract and pay the license fee ([WRS guide](https://elearning.wrshealth.com/?p=10438)).
4. Wait for state approval (about 2-4 weeks). Record the Gateway URL, username, password and facility identifier that Bamboo issues.
5. Get the client certificate under whichever custody model Bamboo approves (open question 1). If IT installs it into the Windows store, record the thumbprint.
6. **LogiCoy states:** register with LogiCoy, or through the state program in Illinois, and name this product as the EHR. Record the per-state URL, username, password and facility ID.
7. In the program:
   1. Setup > Program Links > Bamboo or PDMP: enter the URL, username, password and FacilityID.
   2. Import the certificate (Bamboo).
   3. Set "PDMP Provider License Field".
   4. Make sure each provider has a DEA number, an NPI and a state license or Rx ID for the clinic.
   5. Link each user to their provider.
   6. Run "Check setup", then enable the program and choose its toolbar.
8. **Offices migrating from Open Dental:**
   - Do not reuse the Open Dental-era URL or certificate.
   - Expect new Gateway credentials for the new EHR product. Missouri's packet says a vendor change brings new credentials but no new state approval; confirm for each state with Bamboo, and ask LogiCoy the same (open question 4).

---

## 6. Test plan

### 6.1 Unit tests (no network)

- **Digest:** the existing vector test, plus a UTF-8 password case. The nonce has no `:`. The epoch is in seconds.
- **Serialization:** the Bamboo `PatientRequest` and the LogiCoy request include `SenderSoftware` from overrides and from the built-in identity. The LogiCoy request never contains "Open Dental". `Location` carries the clinic's address and identifiers, never the patient's address. The LogiCoy request omits `SSN` outside KY.
- **`FilterAndSortProperties`:** URL rows visible for both programs; Bamboo certificate rows hidden; license field first.
- **Legacy values:** a non-`osd1$` `Bamboo Client Key` reads as not set, and `Validate` reports "certificate not set". A non-`osd1$` `Bamboo Password` or `{State} PDMP Password` reads as blank.
- **`EnsureProperties` and `GetVal`:** inserts missing rows once (idempotent); a clinic row overrides the clinic 0 row; a missing row returns `""` without throwing.
- **Message-only report:** a `PatientResponse` whose `Report` has a `Message` but no `ReportRequestURLs` gives the message, with no exception and no step 2.
- **URL guard:** `http://` rejected except `localhost` in debug; all five Open Dental domains and their subdomains rejected; `tools/Test-NoHqHosts.ps1` still passes with the one allow-list entry.
- **`PdmpCertificate`:** test certificates are generated at test time with `CertificateRequest` (.NET 4.7.2+), never committed. Cases:
  - accepts a client-auth certificate with a private key
  - rejects no private key, an expired certificate, a wrong password, and server-only EKU
- **Error mapping:** a localhost `HttpListener` stub returns:
  - 401, 403 and 503
  - a `PatientResponse` with `Disallowed`
  - an `Error`
  - malformed XML

  Assert the user message, and that no response body or patient name appears in the exception text.

### 6.2 Manual checks against a local database

- Both programs disabled by default; no toolbar button until enabled (ground rule 7).
- With clinics on, set different credentials for two clinics and confirm the grid shows one set per selected clinic, not duplicate rows.
- On a **restored copy** of a database migrated from Open Dental:
  - the certificate shows "Not set", and the office password rows ask to be re-entered
  - URLs show "carried over, confirm"
  - clicking the button gives the setup message and no crash
- Disconnect the network or block DNS, then click: a friendly message, no `NullReferenceException`, and the rest of the program unaffected.
- Run as a non-admin Windows user and confirm the certificate still loads (`EphemeralKeySet`).
- Try the store certificate by thumbprint, both `CurrentUser` and `LocalMachine`.
- Set the clock 15 minutes off, then confirm the message mentions the clock. Exact behavior depends on the gateway.
- Capture traffic (firewall log or Fiddler): only the configured hosts are contacted. `tools/Test-NoHqHosts.ps1` passes.

### 6.3 Vendor pre-production (once 5.1 is granted)

For Bamboo, using its test patients and pre-production URL and certificate:
- a match with NarxCare scores: the message box shows the scores, OK opens the report in WebView2, and an `rxpat` row is written with `RxType=BambooAccess`
- no match
- multiple matches (Disallowed)
- provider not registered with the PMP (Disallowed)
- wrong password (401)
- wrong or expired certificate (TLS failure)
- the report link is single-use: no Refresh, and reopening the same link fails gracefully
- Cancel at the message box writes no `rxpat` row and sends no `ReportRequest`
- a clinic-specific credential set is used when the patient's clinic has one
- print from WebView2 works

For LogiCoy, per state with LogiCoy test credentials: a report opens and the row has `RxType=LogicoyAccess`; also test bad credentials (401) and per-state validation (KY needs the SSN, MD needs the address).

The Bamboo demo review (5.1.5) is the acceptance gate. Record the changes Bamboo asks for, such as score display wording and disclaimers.

---

## 7. Size, risks and open questions

### 7.1 Size

**Code: M**, about 5-7 developer days:
- property visibility and runtime rows
- certificate dialog and loader
- HTTP helper and error mapping
- product identity and the shared blocked-host helper
- Bamboo two-step split (link only after OK) and the request field fixes
- WebView2 switch
- clinic-aware reads and the setup clinic selector
- tests

**Calendar:** gated by partner onboarding. Bamboo warns it can take "several months", and the state approval takes 2-4 weeks per office after that.

### 7.2 Risks

1. **Partner gate.** Bamboo or LogiCoy may delay or decline an unlisted open source EHR. Until the product is listed, offices can't name it in Customer Connect, and the bridge stays dead.
2. **Key custody.** If Bamboo issues one integrator certificate per vendor, an open source project would have to hand the same private key to every office. That is a shared secret across organizations: likely against Bamboo's terms, with a revocation that hits every office at once. It also pushes toward a central service, which conflicts with ground rule 6. A certificate per HCE, or each office acting as its own integrator, is the clean model, but it may not be offered.
3. **Weak at-rest protection.** `osd1$` is reversible by anyone with database access and this source. Mitigate with the certificate store option, `IsHighSecurity`, and database access control.
4. **Re-credentialing.** Office Gateway and LogiCoy credentials were issued under Open Dental's vendor integration. For Bamboo, at least Missouri issues new credentials when an HCE changes vendor (without fresh state approval), so plan on re-issuance rather than carry-over. LogiCoy is unknown.
5. **Legacy Open Dental values** in migrated databases (URLs and Open Dental's encrypted certificate) must never be used. This is handled by ignoring non-`osd1$` values and flagging carried-over URLs. Still, nothing is ever deleted automatically.
6. **Identity and trademark.** The LogiCoy request currently sends "Open Dental Software". It must change, and changing it may break acceptance until LogiCoy registers the new product.
7. **API and state drift.**
   - The schema (`v5_1`) and the LogiCoy six-state validator table are years old.
   - KY and UT are now served by Bamboo too; NE and PA by LogiCoy.
   - WA offers an HIE route, and CA a direct DOJ route.
   - The state validation rules in `PdmpProperty._dictStateRequirements` are hard-coded.
8. **Certificate EKU.** Per the CURES IEWS guide, most CA-signed certificates stop carrying the Client Authentication EKU from May 2026. If a hub moves to CA-issued client certificates, check the EKU at import.
9. **Rendering.** MSHTML may not render the report. WebView2 needs its runtime on each workstation.
10. **Clinical and legal.** State mandatory-query laws apply to the office. NarxCare is decision support. Bamboo may impose display requirements at the demo.

### 7.3 Open questions

1. Will Bamboo onboard this open source product as an integrator? Does it issue client certificates per HCE (each office as its own integrator), or only one per vendor?
2. What are the production and pre-production base URLs and the current schema version? Are `SenderSoftware`, `LicenseeRequestId` and `RequestDestinations` required in `PatientRequest`? Today the Bamboo request sends none of them. Which `Location` identifiers (facility NPI or DEA) and which address does Bamboo expect?
3. Digest timestamp tolerance and nonce reuse rules; rate limits; `ReportLink` lifetime.
4. Do existing office credentials survive a change of EHR product? For Bamboo, Missouri's packet says no: the new vendor gets new credentials, but no new state approval is needed. Confirm for other states. For LogiCoy, unknown.
5. Is there a vendor certification fee? Does Bamboo require a BAA or data-use agreement from an integrator that never sees PHI?
6. LogiCoy: will they register the product? Is HTTP Basic still accepted, or do they now require OAuth2, JWT or mutual TLS? Which states and URLs are current?
7. Should a later phase add an NCPDP SCRIPT `RxHistory` adapter for direct-to-state routes (CA CURES IEWS, WA OneHealthPort)? Those have public specifications, so the gate there is an MOU or HIE onboarding rather than a hub partnership.
8. Keep two program links, or merge them into one neutral "Prescription monitoring" setup with a hub selector? This is a UI neutrality and simplicity choice.

---

## References

- Bamboo Health: [EHR request and eligibility](https://bamboohealth.com/ehr-request/), [Customer Connect](https://connect.bamboohealth.com/), [PMP Gateway](https://bamboohealth.com/solutions/pmp-gateway/), [HITRUST press release](https://bamboohealth.com/press-release/bamboo-health-achieves-hitrust-csf-certification-to-further-mitigate-risk-in-third-party-privacy-security-and-compliance), [partnership](https://bamboohealth.com/partnership/), [Gateway help center](https://pmpgateway.zendesk.com/hc/en-us)
- State packets and pages: [South Dakota welcome packet](https://doh.sd.gov/media/hulprg0b/pdmp_ehrintegrationguide_welcomepacket.pdf), [Missouri Gateway welcome packet](https://pdmp.mo.gov/wp-content/uploads/2024/01/gateway-integration-guide.pdf), [New Jersey integration guide](https://bamboohealth.com/wp-content/uploads/2022/04/NJPMPIntegrationGuide.pdf), [Georgia FAQ](https://dph.georgia.gov/pdmp/faqs), [Minnesota](https://mn.gov/boards/pharmacy-pmp/integration/), [Alabama](https://www.alabamapublichealth.gov/pharmacy/ehr-integration.html), [Massachusetts packet](https://www.mass.gov/doc/masspat-ehr-integration-welcome-packet/download)
- Funding: [TMA, reactivate PMP access](https://www.texmed.org/TexasMedicineDetail.aspx?id=58335), [TMA, integration free to physicians](https://www.texmed.org/TexasMedicineDetail.aspx?id=62741), [WRS Health onboarding guide](https://elearning.wrshealth.com/?p=10438)
- Open source reference implementation (GPL-3.0, read for host names and header names only): [juggernautsei/oe-bamboo-pmp](https://github.com/juggernautsei/oe-bamboo-pmp), [OpenEMR forum announcement](https://community.open-emr.org/t/bamboo-integration-complete/23315)
- LogiCoy: [PMPlink](https://www.logicoy.com/pmplink-your-solution-for-efficient-prescription-drug-monitoring/), [eRx PDMP integration](https://erx.logicoy.com/pdmp-integration/), [NextGen KB: LogiCoy registration](https://www.community.nextgen.com/mtknowledge/articles/Knowledge/PDMP-Registration-with-LogiCoy)
- Open Dental public manual (what the office used to enter): [Bamboo bridge](https://www.opendental.com/manual/bridgeappriss.html), [PDMP bridge](https://www.opendental.com/manual/bridgepdmpillinois.html), [PDMP overview](https://opendental.com/manual/pdmp.html)
- Direct-to-state: [CURES IEWS](https://www.oag.ca.gov/cures/iews), [CURES IEWS onboarding](https://www.oag.ca.gov/sites/all/files/agweb/pdfs/pdmp/cures-iews-onboarding.pdf), [CURES IEWS NCPDP 2023011 guide](https://oag.ca.gov/system/files/media/cures_Info_exchange_web_service_implementation_guide_NCPDP_2023011.pdf), [OneHealthPort HIE PMP API guide](https://www.onehealthport.com/sites/default/files/content-uploads/documents/OHP%20HIE%20PMP%20API%20IG%20February%202026.pdf)
- In repo: `OpenDentBusiness/WebBridges/PDMP/*`, `OpenDentBusiness/WebBridges/HqProgram.cs` (history: `be14fe1be`, `9bccbad44`), `OpenDentBusiness/Data Interface/ProgramProperties.cs`, `OpenDental/Forms/FormProgramLinkEdit.cs`, `OpenDental/Forms/FormProgramProperty.cs`, `OpenDental/Forms/FormWebView.cs`, `WpfControlsOD/Logic/ProgramL.cs`, `OpenDentBusiness/Data Interface/RxPats.cs`, `CDT/Class1.cs`, `tools/Test-NoHqHosts.ps1`, `D:/Dev/opendental-schema/hq-vendor-table.md`, `docs/decoupling-plan.md`
