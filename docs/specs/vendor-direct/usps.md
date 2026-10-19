# USPS address validation: direct-to-vendor spec

Status: draft, 2026-10-18, fact-checked and corrected the same day (see "Fact-check log" at the end). Baseline: `main` at `6855026f5`. Vendor: United States Postal Service. API: **USPS Addresses API v3** (OpenAPI `info.version` 3.3.1, the "Enhanced Addresses" release), publicly documented, OAuth 2.0 **client credentials**. **Since August 2026, access is licensed and paid** (section 2.7).

## Summary

- **What it is.** When the feature is on and a patient address is saved, the program sends the address to USPS. The user then sees **Address Compare** (`FrmAddressCompare`): the address as entered on one side, the USPS-standardized address with ZIP+4 on the other, and a red deliverability warning when USPS could not confirm the delivery point. The user accepts USPS's version, keeps their own, or goes back to editing. There are **two entry points**: OK in **Edit Patient** (`FormPatientEdit`) and **Import** of a patient form or eForm (`FormSheetImport`). Open Dental ships this behind the `AddressVerifyWithUSPS` preference.
- **What HQ provided.**
  - **Open Dental's own USPS credentials** (and, by extension, Open Dental's own USPS licence). The office never had any.
  - **The network relay.** `WebServiceMainHQProxy.ProcessUSPSAddressValidationRequest(Address)` sent the address to HQ inside a payload tagged `eServiceCode.SoftwareUpdate`. HQ called USPS and sent the answer back.
  - **A support gate.** `PatientL.CanVerifyPatientAddressInteraction` also required `ODHelp.IsEncryptedKeyValid()`, which means an active Open Dental support contract.
- **State on `main`.** The proxy throws `HqNotAvailableException`. `PatientL` catches it and asks "Save anyway?", so with the preference on, every address check ends in an error prompt. In Release builds the help-key gate usually stops it earlier. The call goes through the prebuilt **Bridges.dll** type `USPSAddressValidationDatas`.
- **Direct design:**
  - **Credentials and licence.** Each office opens its own USPS business account in the Customer Onboarding Portal, creates an app there, **signs USPS's Addressing API License Agreement, sets up a USPS Enterprise Payment System account, links the app's credentials to the licence and waits for USPS to grant access** (section 2.7, section 5). The project never holds a USPS key or a USPS licence.
  - **Auth.** The office's Consumer Key and Consumer Secret are exchanged for a bearer token (`POST /oauth2/v3/token`, `grant_type=client_credentials`). There is no browser, no redirect and no refresh token. The token lasts about 8 hours and lives only in memory, in each process.
  - **Storage.** A new `program` row `USPSAddresses` is inserted at runtime with `Enabled=0`. Its `programproperty` rows hold the key, the secret (`osd1$`, masked, high-security) and the environment. Nothing goes in the schema and there is no ConvertDatabases step.
  - **Code.** The three Bridges.dll types this repo uses (`USPSAddressValidationDatas`, `USPSAddressValidationResponse`, `EnumDPVConfirmation`) are re-implemented in-repo with the same member names. The HQ proxy method and the help-key gate are deleted.
- **Feasibility: public API, but licence- and fee-gated.** The specs, hosts and auth flow are public, and no software-vendor partner programme or NDA is involved: the office deals with USPS directly. But access is no longer free or instant:
  - **Licence and fees.** USPS turned the Addresses API into a paid, licensed offering (USPS announced it in May 2026; cut-over postponed from 2026-07-12 to 2026-08-01). The office must sign the Addressing API License Agreement and pay tiered per-lookup fees from a USPS payment account. Third parties report a minimum of about $10 per month for the first 2,000 lookups; USPS has not published the rate card on a page we could read (section 2.7).
  - **Terms.** The developer-portal terms limit use to the licensee's own USPS shipping and mailing, and forbid address-list building, datamining and building databases from the results. Checking one patient mailing address at a time fits. A bulk "clean all addresses" tool would not, and must never be added. The licence agreement itself (a PDF on PostalPro) must be read by the office before signing; we could not review its text (open question 7).
  - **Quota.** USPS's pages give no number. Third parties report about 60 address calls per hour per app (one reports a per-minute figure instead). That budget is shared by every workstation using the office's key, and every call is now also billed.
  - **HIPAA.** USPS publishes no BAA. Only street, unit, city, state and ZIP are sent, never a name. The office's privacy officer should sign off (section 2.6).
- **Interim state.** Until the office has a signed licence, a payment account and USPS's access approval, the program row stays disabled and the code makes no network calls. The code itself can be built and tested against fake HTTP handlers now.
- **Size: S** for the code, about 2 to 3 days including the setup window and tests. USPS onboarding (licence, payment account, approval) is extra calendar time on the office's side, reported as several days.

---

## 1. What the integration does, and what HQ provided

### 1.1 What the office uses it for

| Step | Code on `main` |
|---|---|
| Preference | `PrefName.AddressVerifyWithUSPS`. `ConvertDatabases8` inserted it with `'1'` (on) in the 24.2.23 step, and `To24_2_26` turned it off again when the culture does not end in "US". It appears in **Setup > Preferences > Family > General** as "Verify Addresses with USPS" (`UserControlFamilyGeneral.cs`: `checkAddressVerifyWithUSPS`, hidden unless the culture is exactly `en-US`). Note the mismatch: the gate below accepts any culture ending in "US" (e.g. `es-US`), the checkbox only `en-US`. |
| Trigger 1: Edit Patient | `FormPatientEdit.cs` ~2694-2725. The OK button checks four things: (1) not every address field is empty, (2) the patient is new or one of Address1, Address2, City, State or Zip changed, (3) the Country box is hidden or empty, and (4) `PatientL.CanVerifyPatientAddressInteraction()`. It then builds an `OpenDentBusiness.Address` from the text boxes and calls `PatientL.VerifyPatientAddressInteraction(address,this)`. If that returns `null`, the save is cancelled and the user stays in the form. Otherwise the text boxes take the chosen address. |
| Trigger 2: form import | `FormSheetImport.cs` ~3234-3251, in the "Patient Form" region of `butImport_Click` (patient-form sheets and eForms). When `CanVerifyPatientAddressInteraction()` is true it builds an `Address` from the patient's (possibly just-imported) address fields and calls the same `VerifyPatientAddressInteraction`. `null` aborts the import before `Patients.Update`; otherwise the patient fields take the chosen address. **There is no "address changed" check here**, so every patient-form import makes a USPS call (and, now, a billed lookup) even if the address is unchanged (open question 8). |
| Not callers | The Add Family window and the other address forms never verify. `WpfControlsOD/Frms/FrmEFormImportRules.xaml` has a checkbox named `checkAddressVerifyWithUSPS`, but it is an unused mock-up: its only construction in `FormPatientForms.cs:229` is commented out, and it is not wired to the preference. |
| Gate | `PatientL.CanVerifyPatientAddressInteraction()` (`OpenDental/Logic/PatientL.cs` ~284): the culture ends in "US", the preference is on, `!ODBuild.IsTrial()`, and `ODHelp.IsEncryptedKeyValid()` (the support key). |
| Pre-checks | `VerifyPatientAddressInteraction` (~302): Address1 and State are required, otherwise it asks "Import anyway?" (the wording comes from the import caller). A state written out in full is mapped to its two-letter code through `USlocales.ListAll`. |
| Call | `VerifyPatientAddressInteractionHelper` (~337): `USPSAddressValidationDatas.GetUSPSAddressValidationResponse(address)` (Bridges.dll). It catches `HqNotAvailableException` and asks "Save anyway?". A `null` response, or a response with `address==null`, gives "Failed to verify address with USPS… Save Anyway?". |
| Compare | `WpfControlsOD/Frms/FrmAddressCompare.xaml.cs` reads `response.address.{streetAddress, secondaryAddress, city, state, ZIPCode, ZIPPlus4}` and `response.additionalInfo.DPVConfirmation`. It parses the confirmation into `EnumDPVConfirmation`, treating null or unparsable as `None`. For anything other than `Y` it shows `Lang.g(this, GetDescription())` in red. **Yes** takes the USPS values: Address1 = streetAddress, Address2 = secondaryAddress, Zip = `ZIPCode-ZIPPlus4`. **No** keeps the typed address. **X** returns `null` (back to editing). |

### 1.2 What Open Dental's HQ provided

| Piece | Evidence in this repo | Now |
|---|---|---|
| USPS credentials | `hq-vendor-table.md`: HQ proxied with Open Dental's own USPS credentials. The office stored nothing locally: no program row and no property. | Gone |
| Relay | `WebServiceMainHQProxy.ProcessUSPSAddressValidationRequest(Address)` (~1216) serialized the `Address` with `PayloadHelper.CreatePayload(..., eServiceCode.SoftwareUpdate)` and called SOAP `ProcessUSPSAddressValidationRequest(string)` on `www.opendental.com/OpenDentalWebServiceHQ`. It returned a string. Inferred from the call sites only: Bridges.dll calls this proxy and turns the string into `USPSAddressValidationResponse`. The repo passes an `OpenDentBusiness.Address` into the Bridges method, and `PatientL` expects `HqNotAvailableException` to come out of it, so Bridges.dll must reference OpenDentBusiness. (OpenDentBusiness also references Bridges.dll by HintPath, so the two prebuilt assemblies depend on each other; this is one more reason to move the types in-repo.) | Throws `HqNotAvailableException.ForVendor("USPS address validation")` while `!IsHqAvailable`. `WebServiceMainHQNoHq.ProcessUSPSAddressValidationRequest` returns `VendorNotAvailable(...)`. `WebServiceMainHQMockDemo` returns `null`. |
| Gate | `ODHelp.IsEncryptedKeyValid()` (OpenDentalHelp.dll, support status). The `SoftwareUpdate` eService code on the payload suggests HQ also checked support status on its side. | The plan already says to remove this gate (`decoupling-plan.md`, OpenDentalHelp row). |
| Response shape | The field names the callers read (`streetAddress`, `secondaryAddress`, `ZIPCode`, `ZIPPlus4`, `additionalInfo.DPVConfirmation`) are the USPS Addresses v3 JSON names. Inferred: HQ called the v3 API and passed its JSON through more or less unchanged. | Re-implemented in section 4.2 |

Consequences:

- Every gate after the culture check can go. We call USPS ourselves, with the office's own app and licence.
- **One fewer party sees the address.** Before, the patient's address went to Open Dental HQ and then to USPS. Now it goes only to USPS.
- `NoHqTests` asserts that the proxy throws (`NoHqTests.cs:66` for the interface method, `:82` for the proxy helper). These assertions change with the code (section 4.4).

---

## 2. The vendor's public API

Sources:

- Addresses OpenAPI spec (3.3.1): <https://developers.usps.com/sites/default/files/apidoc_specs/addresses-v3r2_0.yaml> (rendered at <https://developers.usps.com/addressesv3>)
- OAuth OpenAPI spec (3.1.5): <https://developers.usps.com/sites/default/files/apidoc_specs/oauth2_update_1.yaml> (rendered at <https://developers.usps.com/Oauth>)
- USPS samples: <https://github.com/USPS/api-examples> (README)
- Getting started: <https://developers.usps.com/getting-started>
- FAQ: <https://developers.usps.com/faq>
- Developer portal terms (updated 2026-01-22): <https://developers.usps.com/terms-and-conditions>
- Addressing API License Agreement (PostalPro, 2026-04-13, PDF): <https://postalpro.usps.com/Addressing_API_License>
- USPS newsroom, competitive price changes for July 2026 (new "Addresses API" offering): <https://about.usps.com/newsroom/national-releases/2026/0511-usps-recommends-competitive-price-changes-for-july-2026.htm>

### 2.1 Base URLs

| Environment | OAuth | Addresses |
|---|---|---|
| Production | `https://apis.usps.com/oauth2/v3` | `https://apis.usps.com/addresses/v3` |
| Testing Environment for Mailers (TEM) | `https://apis-tem.usps.com/oauth2/v3` | `https://apis-tem.usps.com/addresses/v3` |

Both specs list these `servers`. TEM is used with the **same production Consumer Key and Secret**: only the host changes (FAQ, "How can I Test my Application using USPS APIs?", and Getting Started step 5). There is no separate sandbox account. Under the licensed regime, one implementer reports having to ask USPS for TEM access to the enhanced API after signing the licence (section 2.7).

The legacy XML **Web Tools** `Verify` API is not an option: USPS retired Web Tools on 2026-01-25 (reported by several third parties, e.g. <https://directmail.io/blog/usps-api-marketplace-explained-npf-2026>; Smarty's post says only that it has been retired). Nothing in this spec uses it.

### 2.2 Auth flow (client credentials)

1. **Register.** Get the office's app credentials once (section 5). The Consumer Key is the `client_id` and the Consumer Secret is the `client_secret` (FAQ; Getting Started step 4).
2. **Request a token.** `POST {oauth}/token`. The spec accepts `application/json` or `application/x-www-form-urlencoded`; its client-credentials schema allows only `grant_type`, `client_id`, `client_secret` and an optional `scope`. USPS's README example posts JSON with the first three fields and no scope. Our request adds the scope:
   ```json
   { "grant_type": "client_credentials", "client_id": "<key>", "client_secret": "<secret>", "scope": "addresses" }
   ```
   If `scope` is left out, the app's registered default scope applies. The spec says to send it only to ask for *less* than the default. Asking for `addresses` alone is least privilege (open question 4).
3. **Read the response.** It carries `access_token` (a JWT), `token_type` (`Bearer`), `issued_at` (epoch ms), `expires_in` (seconds; the spec example is 28799, about 8 hours), `status`, `scope`, `issuer`, `client_id`, `application_name`, `api_products` and `public_key`. The spec types `expires_in` as an integer, but the GitHub README example shows it quoted as a string, so parse either.
4. **No refresh token.** The client-application response schema has no refresh token; only the authorization-code (resource-owner) response does. To get a new token, repeat step 2 before the old one expires.
5. **Call the API.** Send `Authorization: Bearer <access_token>` on every Addresses call. A missing or expired token gets **401**.
6. **Revoke.** `POST /revoke` (HTTP Basic with key and secret) is described as revoking refresh tokens, since access tokens are self-contained JWTs. We never call it.

Token errors to handle: **400** (`invalid_request`, `unsupported_grant_type`, `invalid_scope`…); **401** (`invalid_client`, with `WWW-Authenticate`); **429** and **503**, both with `Retry-After`.

The USPS README says a CRID (Customer Registration ID) and MID (Mailer ID) from the Business Customer Gateway are needed to get a token; the OAuth spec, Getting Started and the FAQ do not mention either. Whether an Addresses-only app needs a MID is open question 3.

### 2.3 Endpoint we need: `GET /address`

`GET {addresses}/address?streetAddress=…&secondaryAddress=…&city=…&state=…&ZIPCode=…&ZIPPlus4=…`, with `Accept: application/json`, scope `addresses`.

Request rules from the spec:

- `streetAddress` is **required**. Per the spec's `x-anyOf-required` rule it must come with `city` and `state`, or with `ZIPCode` (or all three).
- `state` is two characters and must match the spec's USPS code pattern (states, DC, territories, military codes).
- `ZIPCode` must be exactly 5 digits and `ZIPPlus4` exactly 4. Values that don't match must be **left out**, not sent.
- `secondaryAddress` holds the unit (APT, STE…). It may also be appended to `streetAddress` instead.
- Optional, unused here: `firm` (max 50 characters) and `urbanization` (Puerto Rico).
- **Everything is in the query string.** The API has no POST form of this call (section 7, risk 4).

**200 response (`AddressResponse`).** The fields we use or keep:

| JSON | Use |
|---|---|
| `address.streetAddress` | Address1 (max 50). It may already include the unit when USPS folds `secondaryAddress` into it |
| `address.secondaryAddress` | Address2. Can be null even when the input had a unit (see the row above) |
| `address.city`, `address.state` | City, State. Upper case, USPS standard |
| `address.ZIPCode`, `address.ZIPPlus4` | Zip = `ZIPCode` or `ZIPCode-ZIPPlus4`. `ZIPPlus4` is nullable |
| `additionalInfo.DPVConfirmation` | `Y` primary (and secondary, if given) confirmed; `D` primary confirmed, secondary missing; `S` primary confirmed, the secondary given didn't confirm; `N` not confirmed. The spec notes that `Y` alone does not mean USPS delivers there. |
| `additionalInfo.DPVEnhancedConfirmation` | Added in the enhanced release. Adds `R`: confirmed, but USPS does not deliver to the address. Not used in v1 (open question 6) |
| `additionalInfo.DPVCMRA`, `DPVVacant` / `vacant`, `carrierRoute`, `deliveryPoint` | Kept on the DTO. Not shown in v1. (`business` is still in the schema but reported as deprecated in the enhanced release; do not rely on it.) |
| `matches[]`, `corrections[]` (`code`, `text`), `warnings[]` | Kept on the DTO. In `matches`, code 31 is an exact single match. In `corrections`, 32 is a "default" match (more detail such as a unit is needed) and 22 means several addresses matched |

The enhanced release added several dozen more `additionalInfo` indicators. The DTO ignores unknown fields.

**Errors** (`ErrorMessage`: `{ apiVersion, error: { code, message, errors[] } }`):

| Status | Meaning | Our handling |
|---|---|---|
| 400 | Malformed request, e.g. a bad state or ZIP pattern | "USPS could not check this address: {message}". Save anyway? |
| 401 | Missing, expired or invalid token | Get a new token once and retry once. If it fails again, report bad credentials |
| 403 | The app lacks access to the Addresses product (for example, the licence is not linked or access not yet granted) | "Your USPS app does not have access to the Addresses API." Save anyway? |
| 404 | No match. The spec's examples include address not found, no match, invalid state, invalid city, unverifiable city and state, insufficient data, invalid delivery address and multiple addresses found | Return a response with `address=null` and `ErrorMessage` = the USPS message. The existing "Failed to verify… Save Anyway?" prompt shows it |
| 429 | Quota exceeded or spike arrest; `Retry-After` header | "USPS address checks are temporarily over the limit (retry after N s)." Save anyway? No automatic retry |
| 503 or other | USPS unavailable | Treated like offline |

We don't use `GET /city-state` (ZIP to city and state) or `GET /zipcode` (address to ZIP+4) in v1. A later "fill city and state from ZIP" helper could use `/city-state`; it would spend the same quota and, presumably, the same billed lookups.

### 2.4 Rate limits

- **Official.** The FAQ says API products limit access by time, scope and quota, that a 429 means the **hourly** call quota is used up, and that increases go through a USPS service request. The developer terms speak of request limits that vary by API and are shown on each API's page, which USPS may change without notice. The Addresses page we read states no number.
- **Reported number.** Third parties report **60 requests per hour** for address validation (<https://www.smarty.com/blog/usps-api-rate-limit>, updated 2026-04-28); one reports about 60 per minute per token instead (directmail.io, above). Treat 60 per hour per app as the conservative planning number until the office sees its real figure (open question 2).
- **The token call counts too.** OAuth is part of the same app. Caching the token (section 3.3) keeps token calls to about one per workstation per 8 hours.
- **The budget is shared.** All workstations use the office's one key. A practice editing about 30 addresses an hour fits, but patient-form imports also call USPS every time (section 1.1), which matters on busy check-in mornings. Conversions and Add Family never call USPS.

### 2.5 Terms that matter

From the developer-portal terms (<https://developers.usps.com/terms-and-conditions>, updated 2026-01-22), in our own words:

- **Purpose.** The licence is limited, non-exclusive and non-transferable, and is for using USPS shipping and mailing services. A dental office checks patient addresses so that statements, recall cards and correspondence reach patients by USPS mail. That is a mailing purpose. The office should still read the terms and agree to them itself (section 5).
- **Prohibited uses.** Building address lists or databases from the service data for others, datamining or scraping, reselling or licensing the data, making derivatives of it, using it to build databases or AI models, and altering API responses. Writing the one standardized address the user accepted back into that patient's record is ordinary use for the office's own mail, but the office should confirm this reading against the Addressing API licence (open question 7). The rules for the legacy address APIs went further: per transaction, as a person enters an address, never batch cleanup (<https://www.usps.com/business/web-tools-apis/address-information-api.htm>). **Design rule: verification runs only for one address a user just entered or imported. No batch or "verify all" feature, ever.**
- **No sharing of credentials.** The licensee must keep credentials confidential and must not rent, sublicense or hand them to third parties; its own authorized staff may use them. This is why each office registers its **own** app and the project never ships a shared key.
- **Authorized representative.** Whoever accepts the terms for the practice must be authorized to bind it.
- **Termination.** USPS may end access or revoke credentials at any time without notice. The feature must fail soft (section 3.4).
- **Operational-test clause.** The terms call use of the Addressing API an operational test that can be stopped at any time, tie the data to the user's AMS-API licence, and say the user must sign an Operational Test API License when USPS provides one or lose access. Read together with the 2026 Addressing API License Agreement (section 2.7), this now looks like the licence every Addresses user must sign, not something only AMS licensees face.

### 2.6 PHI, BAA and privacy

- **What is sent:** street, unit, city, state, ZIP, and the office's app identity in the token. **Not sent:** the patient's name, birthdate or PatNum, and `firm`.
- A street address is one of the HIPAA identifiers (HHS de-identification guidance, <https://www.hhs.gov/hipaa/for-professionals/special-topics/de-identification/index.html>). Coming from a dental office's account, a lookup could be read as "this address belongs to a patient".
- **No BAA.** USPS publishes no BAA or healthcare terms for its APIs. We found none; the developer terms point only to the general USPS privacy policy (<https://www.usps.com/privacypolicy>). We could not read the Addressing API licence PDF, so whether it says anything about data handling is unknown (open question 7).
- **Compared with before:** the Open Dental path sent the same fields to two parties (HQ, then USPS). The direct path sends them to one.
- **Recommendation:** the office's privacy officer records the decision. The setup window says in plain words what is sent (section 3.2). The integration stays off until someone enables it.

### 2.7 Licensing and fees (new in 2026)

What USPS has published:

- The May 2026 USPS newsroom release announced a new Address Management System offering called **Addresses API** among the competitive price changes planned for 2026-07-12, pending Postal Regulatory Commission review.
- PostalPro hosts an **Addressing API License Agreement** (page dated 2026-04-13, PDF dated 2026-04-08) and a 2026-06-01 industry alert announcing the "Enhanced Addresses API" for July 2026.
- The developer FAQ still lists Addresses in the "default product" of every new app. That text predates the licensing change and should not be read as "free and automatic".

What implementers report (third-party, so treat as unconfirmed):

- The cut-over moved from 2026-07-12 to **2026-08-01**. The host, path, parameters and the existing consumer key and secret stayed the same (<https://github.com/CannObserv/address-validator/issues/155>).
- To keep or get access: sign the licence, create a USPS **Enterprise Payment System** account with payment details, add the Addresses API licence in the Business Portal, link the app's credentials to it, and ask USPS (via <https://emailus.usps.com/s/usps-APIs>, "API Onboarding & IP Agreement") to enable TEM and production access. One user waited several days for approval (<https://www.ablecommerce.com/forums/posts/m8721-USPS-Addresses-API-change>).
- Fees are tiered per lookup. One published tier table: $10 flat for up to 2,000 lookups a month, then roughly $4.00 to $4.50 per 1,000 (<https://apicostcalc.com/blog/usps-address-api-no-longer-free-2026.html>). Another vendor says USPS has not broadly published the prices. A small practice will very likely stay in the lowest tier.

Effect on this design:

- No code change in the API calls. The licence, payment account and approval are office-side prerequisites, listed in section 5.
- **No credentials or licence on the project side.** Under ground rule 7 the setup window does not explain how to buy anything; our own docs do.
- Every call is billable, so the design keeps calls to the minimum: token cache, no batch, a cool-down after 429, and (open question 8) a "changed" check on the import path.

---

## 3. Desktop design

### 3.1 Auth flow on the desktop: client credentials, no browser

```
FormPatientEdit OK  /  FormSheetImport Import
  -> PatientL.VerifyPatientAddressInteraction  (unchanged pre-checks)
     -> USPSAddressValidationDatas.GetUSPSAddressValidationResponse(address)     [OpenDentBusiness, in-repo]
        -> UspsApiClient.GetAccessToken(config)   memory cache, else POST /oauth2/v3/token
        -> UspsApiClient.GetAddress(token, query) GET /addresses/v3/address
        <- USPSAddressValidationResponse (or address=null + ErrorMessage)
     -> FrmAddressCompare (unchanged UI)
```

- No PKCE, loopback listener or device code. Nobody signs in to USPS interactively. The app authenticates as itself.
- The network call runs on a worker thread under `ProgressWin` (`WpfControlsOD/UI/Progress/ProgressWin.cs`; "Checking address with USPS…"), so the window doesn't freeze. Timeout: 15 s for the token and 15 s for the address call.
- **Middle Tier.** The call runs in the **client** process (`Meth.NoCheckMiddleTierRole()`), like the other web bridges. Settings come from the client's `Programs` and `ProgramProperties` caches. The secret is decrypted in memory on the client only.
- **HTTP.** One static `HttpClient` per process with `BaseAddress` set for each environment. TLS 1.2 or later comes from the OS on .NET 4.8. Newtonsoft.Json (the 12.0.3 DLL in `Required dlls`) does the parsing. No SDK is needed.

### 3.2 Where credentials and settings live

**A new `program` row**, `ProgramName.USPSAddresses`:

- It is appended at the **end** of the `ProgramName` enum (after `ZImage`), because ProgName is stored as text.
- `ProgDesc`: "USPS address validation".
- `Enabled=0`. It is inserted at runtime the first time **Setup > Program Links** opens, through a shared `Programs.InsertIfMissing` helper (proposed in `postcardmania.md`, open question 7; it does not exist yet). There is no ConvertDatabases step, and no `toolbutitem`: this integration has no toolbar button.

**Properties** (`programproperty`, `ClinicNum=0`, practice-wide). Missing rows are added in memory when Setup loads and inserted when Setup saves (proposed `ProgramProperties.InsertIfMissing`):

| PropertyDesc (constants on `UspsAddresses`) | Value | Stored as | Flags |
|---|---|---|---|
| `Consumer Key` | `client_id` from the Customer Onboarding Portal | plain text | none |
| `Consumer Secret` | `client_secret` | `CDT.Class1.TryEncrypt` (`osd1$…`). Read back with `TryDecrypt` | `IsMasked=1`, `IsHighSecurity=1` |
| `Environment` | `Production` (default) or `Testing` (TEM) | plain text | none |

**Not stored:**

- **Access tokens.** They are kept in memory only, about 8 hours (section 3.3).
- **Addresses and responses.** Nothing goes to the database (other than the address the user accepts into the patient record), the logs or the disk.

**Setup window** (`FormUspsAddressSetup`, opened from `FormProgramLinks.gridProgram_CellDoubleClick`, `case "USPSAddresses"`):

- **Enabled** checkbox.
- **Consumer Key** text box.
- **Consumer Secret**, masked. It changes only when re-entered, and it is never shown again.
- **Environment** combo: Production or Testing.
- **Test connection** button. It requests a token and checks one fixed, non-patient address: USPS headquarters at 475 L'Enfant Plaza SW, Washington DC 20260. It reports "Connected (scope: addresses)" or the USPS error. This costs one call from the quota and one billed lookup.
- A short neutral description: "Sends the street, unit, city, state and ZIP of a patient address being saved or imported to the U.S. Postal Service for standardization. No patient name is sent. Requires the office's own USPS API credentials and licence; USPS may charge per lookup."
- **Not included:** links, logos or sign-up prompts, per ground rule 7. The credential and licence steps go in our own docs (section 5), not in the window.

**Permissions.** Program Links already requires `Setup`. Saving a high-security property writes a security-log entry, as for other masked bridge secrets.

**The preference stays.** `AddressVerifyWithUSPS` remains the per-office switch. Verification runs only when **both** the program is enabled **and** the preference is on. Because Open Dental databases have the preference at `1`, the program row is what keeps the feature **off by default**. The Preferences checkbox is shown only when the program is enabled (ground rule 7) and the culture is US.

**Threat model.** `osd1$` is obfuscation with a fixed public key (`CDT/README.md`). Anyone with a database copy and this source can recover the secret. The secret lets someone spend this office's USPS address quota and, now, run up billed lookups on the office's USPS account, so the impact is low but no longer zero. Rotation: ask USPS for new credentials (the README says to contact USPS if credentials leak; we found no documented self-service reissue), then re-enter them.

### 3.3 Token lifetime and refresh

- **Cache.** One static entry per process: `{token, expiresUtc, configFingerprint}`, guarded by a `lock`.
  - `expiresUtc` is the time of receipt plus `expires_in`, minus a 5-minute margin.
  - `configFingerprint` is a SHA-256 of key, secret and environment. Changing any of them in Setup drops the token on that workstation at its next cache refresh (`InvalidType.Programs`).
- **Getting a token.** Use the cached token unless it is expired or the fingerprint changed. Otherwise `POST /token`.
- **401 on `/address`.** Drop the token, get a new one, and repeat the call **once**. A second 401 is reported as "USPS rejected the app credentials".
- **No refresh token.** Repeating the client-credentials request *is* the refresh. Nothing about tokens is written to the database, so workstations never contend for a row.
- **Quota cost.** About one token call per process per 8 hours. Even 20 workstations open all day add only about 40 token calls to the day.

### 3.4 Errors and offline behaviour (fail soft, never block a save)

| Situation | What the user sees | Result |
|---|---|---|
| Program disabled, preference off, or culture not US | Nothing. No call is made | Saves as entered |
| Program enabled but key or secret empty | Nothing at save time. Setup shows "Not configured" | Saves as entered. `CanVerify…` returns false |
| Address1 or State missing, or state not mappable | The existing "Import anyway?" prompts | Unchanged |
| No network, DNS failure, timeout or 503 | "Could not reach USPS to check the address. Save anyway?" | Yes saves as entered. No returns to editing |
| 401 twice, or 403 | "USPS rejected the address check ({message}). Check Setup > Program Links > USPS address validation. Save anyway?" | Same |
| 429 | "USPS address checks are over the limit right now. Save anyway?" Then a 2-minute cool-down **in that process**, during which saves skip verification quietly | Saves as entered |
| 404 no match | Existing "Failed to verify address with USPS…" prompt, plus the USPS message, e.g. "More than one address was found…" | Unchanged choice |
| 200 | `FrmAddressCompare`. Red text for DPV `D`, `S`, `N` or none | Unchanged |

Core offline use is untouched (ground rule 5). The feature only ever adds a prompt, and the user can always save.

**Logging.** Log the HTTP status, the USPS error code and the elapsed time only. Never log the URL (it holds the address), the token or the secret.

### 3.5 No Open Dental hosts, no HQ

- The only hosts contacted are `apis.usps.com` and `apis-tem.usps.com`.
- `WebServiceMainHQProxy.ProcessUSPSAddressValidationRequest` is deleted. Nothing in this path touches `IWebServiceMainHQ`, `PayloadHelper` or `eServiceCode`.
- The `ODHelp.IsEncryptedKeyValid()` and `ODBuild.IsTrial()` gates are removed from `CanVerifyPatientAddressInteraction`.
- No key, secret or licence identifier is compiled in or committed. Tests use fake handlers and made-up values.

---

## 4. Code changes

### 4.1 Files and members

| File | Change |
|---|---|
| `OpenDentBusiness/TableTypes/Program.cs` | Append `USPSAddresses` to `enum ProgramName`, after `ZImage`. |
| `OpenDentBusiness/WebBridges/Usps/UspsAddresses.cs` (**new**) | `public static class UspsAddresses`: <br>• property-name constants <br>• `GetConfig()`, which returns key, decrypted secret and environment from the cache <br>• `IsConfigured()` (enabled, key and secret present) <br>• `InsertProgramIfMissing()` <br>• `AddMissingProperties(List<ProgramProperty>,long programNum)` |
| `OpenDentBusiness/WebBridges/Usps/UspsApiClient.cs` (**new**) | `internal` client: `GetAccessToken(UspsConfig)` (cache, lock, 5-minute margin) and `GetAddress(UspsConfig, Address)` (builds the query and maps the status codes in 2.3). A constructor overload takes an `HttpMessageHandler` for tests. Public typed exception: `UspsApiException : ODException`, with `HttpStatus`, `UspsMessage` and `Kind` {Offline, Credentials, Forbidden, Quota, BadRequest}. |
| `OpenDentBusiness/WebBridges/Usps/USPSAddressValidationDatas.cs` (**new**) | The re-implemented Bridges surface (4.2). |
| `OpenDentBusiness/OpenDentBusiness.csproj` | Add the three `Compile Include` items. |
| `OpenDentBusiness/WebServices/WebServiceMainHQProxy.cs` | Delete `ProcessUSPSAddressValidationRequest(Address)` (~1214-1227). |
| `OpenDental/Logic/PatientL.cs` | **`CanVerifyPatientAddressInteraction`:** keep the culture and preference checks; add `UspsAddresses.IsConfigured()`; delete the `IsTrial` and `IsEncryptedKeyValid` checks, and drop `using OpenDentalHelp;` (line 295 is its only use in this file). <br>**`VerifyPatientAddressInteractionHelper`:** run the call inside `ProgressWin`. Replace `catch(HqNotAvailableException)` with `catch(UspsApiException ex)`, which builds the per-`Kind` prompt (3.4) and keeps the same Yes/No result. Show `response.ErrorMessage` in the "Failed to verify" prompt when it is set. <br>**Usings:** drop `using Bridges;`. The only Bridges types this file uses are the USPS ones, which now resolve through the existing `using OpenDentBusiness;`. Keeping both usings would make the names ambiguous (CS0104) while Bridges.dll is still referenced. |
| `WpfControlsOD/Frms/FrmAddressCompare.xaml.cs` | Replace `using Bridges;` with `using OpenDentBusiness;`. The file has **no** `using OpenDentBusiness;` today (it writes `OpenDentBusiness.Address` in full), so without the new using `USPSAddressValidationResponse` and `EnumDPVConfirmation` would not resolve. No logic change. Fix the label typo "Reccomended" in `FrmAddressCompare.xaml` (`labelUSPSReccomended`) as an optional nicety. |
| `OpenDental/Forms/FormSheetImport.cs` | No change required (it calls the same `PatientL` methods). Optional, per open question 8: skip verification when the imported address equals `_patientOld`'s, to save quota and fees. |
| `OpenDental/Forms/FormProgramLinks.cs` | Call `UspsAddresses.InsertProgramIfMissing()` at the start of `FillList()`. Add `case "USPSAddresses":` in `gridProgram_CellDoubleClick` to open `FormUspsAddressSetup`. |
| `OpenDental/Forms/FormUspsAddressSetup.cs` + `.Designer.cs` (**new**) | The setup window (3.2). Register it in `OpenDental.csproj`. |
| `OpenDental/User Controls/Preferences/UserControlFamilyGeneral.cs` | Show `checkAddressVerifyWithUSPS` only when the culture is en-US **and** `Programs.IsEnabled(ProgramName.USPSAddresses)` (~line 115). |
| `OpenDentBusiness/WebServices/WebServiceMainHQMockDemo.cs`, `WebServiceMainHQNoHq.cs`, `IWebServiceMainHQ.cs`, `Web References/WebServiceMainHQ/Reference.cs` | No change here. The SOAP method `ProcessUSPSAddressValidationRequest(string)` stays until the HQ interface itself is deleted (Phase 3). Nothing calls it any more. |
| `NoHqTests/NoHqTests.cs` | Delete the line-82 assertion (the proxy helper is gone). Keep line 66 while the interface method exists. |

### 4.2 Bridges.dll types to re-implement in-repo

**Namespace `OpenDentBusiness`**, not `Bridges`. Bridges.dll stays referenced (by OpenDentBusiness and WpfControlsOD through HintPaths, by OpenDental through the substituted `xBridges` ProjectReference, and by UnitTests) for QuickBooks Online and Microsoft 365 until its own split. Defining `Bridges.USPSAddressValidationResponse` in a second assembly that these projects also reference would cause CS0433. With the types in `OpenDentBusiness`, OpenDentBusiness files that already have `using Bridges;` are unaffected (types in the enclosing namespace win), but the two caller files in other namespaces must lose `using Bridges;` (4.1).

Below is the public surface this repo uses, exactly as the call sites show it, plus a few fields. Nested class names are ours, because callers never name them:

```csharp
namespace OpenDentBusiness {
  public static class USPSAddressValidationDatas {
    ///<summary>Throws UspsApiException on transport/auth/quota errors. Returns a response with address==null (and ErrorMessage set) when USPS finds no match.</summary>
    public static USPSAddressValidationResponse GetUSPSAddressValidationResponse(Address address);
  }
  public class USPSAddressValidationResponse {      // JSON: AddressResponse
    public string firm;
    public USPSAddressValidationAddress address;     // .streetAddress .secondaryAddress .city .state .ZIPCode .ZIPPlus4
    public USPSAddressValidationAdditionalInfo additionalInfo; // .DPVConfirmation (string) + DPVCMRA, DPVVacant, vacant, carrierRoute, deliveryPoint
    public List<USPSAddressValidationCodeText> corrections;
    public List<USPSAddressValidationCodeText> matches;
    public List<string> warnings;
    [JsonIgnore] public string ErrorMessage;        // our addition: USPS 404 message
  }
  public enum EnumDPVConfirmation {
    [Description("USPS could not confirm this address is deliverable.")] None,
    [Description("")] Y,
    [Description("USPS confirmed the building but the apartment/suite number is missing.")] D,
    [Description("USPS confirmed the building but not the apartment/suite number entered.")] S,
    [Description("USPS could not confirm this street number.")] N,
  }
}
```

Surface actually used by the repo (grep of `USPSAddressValidation|EnumDPVConfirmation`): `USPSAddressValidationDatas.GetUSPSAddressValidationResponse(OpenDentBusiness.Address)` (`PatientL.cs:341`); `USPSAddressValidationResponse` as a type and its members `.address` (`streetAddress`, `secondaryAddress`, `city`, `state`, `ZIPCode`, `ZIPPlus4`) and `.additionalInfo.DPVConfirmation` (`PatientL.cs`, `FrmAddressCompare.xaml.cs`); `EnumDPVConfirmation.None`, `.Y`, `Enum.Parse` on it and `GetDescription()` (`FrmAddressCompare.xaml.cs`). Nothing else from Bridges.dll is used for USPS.

- **Public fields** with lower-camel JSON names (`ZIPCode` and `ZIPPlus4` keep their case). Newtonsoft binds them with no attributes. The callers' member access compiles unchanged.
- `Enum.Parse(typeof(EnumDPVConfirmation), "Y")` in `FrmAddressCompare` works, because the member names are the USPS codes. An unexpected value (or an empty string) throws and is caught there, giving `None`.
- `GetDescription()` is the existing `CodeBase` extension (`ODPrimitiveExtensions.cs`) that reads `[Description]`. The text is ours.
- **Request mapping** in `GetUSPSAddressValidationResponse`:
  - Trim every field. Send `streetAddress=Address1` (cut to 50 characters, the response schema's limit) and `secondaryAddress=Address2` if not empty.
  - Send `city` if not empty, and `state` upper-cased, only if it matches the spec pattern.
  - `Zip`: if it matches `^\d{5}(-?\d{4})?$`, split it into `ZIPCode` and `ZIPPlus4`. Otherwise leave both out and rely on city and state.
  - Never send `firm`.
- **Validation before the call.** If the request still has neither (city and state) nor ZIPCode, return `address=null` with `ErrorMessage` "City and state, or a ZIP Code, are needed to check the address" and skip the call (and the fee).

### 4.3 "Not available" stubs replaced

| Stub | Replacement |
|---|---|
| `WebServiceMainHQProxy.ProcessUSPSAddressValidationRequest(Address)`, which throws `HqNotAvailableException` | Deleted. `USPSAddressValidationDatas` in-repo calls USPS directly |
| `PatientL.VerifyPatientAddressInteractionHelper`: `catch(HqNotAvailableException)` with its comment "used to be relayed through Open Dental HQ" | `catch(UspsApiException)` with the specific prompts |
| `PatientL.CanVerifyPatientAddressInteraction`: `ODHelp.IsEncryptedKeyValid()` support gate | `UspsAddresses.IsConfigured()` |

### 4.4 Tests (automated)

New `UnitTests` class `UspsAddressTests` (or `NoHqTests`, which already runs with no database). It uses a fake `HttpMessageHandler` and makes **no network calls**:

1. **Deserialization:**
   - A response modelled on the USPS README sample (3120 M ST NW, Washington DC) maps to `address.ZIPCode=20007`, `ZIPPlus4=3704`, `DPVConfirmation=Y`.
   - `expires_in` parses whether it comes as a string or an integer.
   - Unknown `additionalInfo` fields are ignored.
2. **Request building:**
   - `Zip "12345-6789"` gives ZIPCode and ZIPPlus4. `"12345"` gives ZIPCode only. `"1234"` gives neither.
   - The state is upper-cased, and an invalid state is left out.
   - Address1 is cut to 50 characters. No `firm` is sent.
   - The values are URL-encoded (e.g. `#`, `&`, `'`).
3. **Token cache:**
   - Two calls within the lifetime make one token request.
   - Past `expiresUtc` minus 5 minutes, a new token is requested.
   - A fingerprint change drops the token.
   - 401, new token, retry happens exactly once.
4. **Errors:** for each of the spec's 404 examples, `address==null` and `ErrorMessage` is set. 400, 403, 429 (`Retry-After`), 503, a timeout and an `HttpRequestException` each give the right `UspsApiException.Kind`.
5. **Gates:**
   - `IsConfigured` is false with the program disabled, the key empty, or the secret empty.
   - An `osd1$` secret round-trips through `TryEncrypt` and `TryDecrypt`.
6. **No Open Dental hosts:** every URL the client builds starts with `https://apis.usps.com/` or `https://apis-tem.usps.com/`.

---

## 5. Credentials and licence the office must obtain (checklist)

The office does this itself, in its own name. The project never supplies a USPS key or licence.

1. **Create or sign in to a USPS business account** in the Customer Onboarding Portal: <https://cop.usps.com> (Getting Started, step 1, <https://developers.usps.com/getting-started>).
   - Use the practice's legal name and address.
   - The person who registers must be authorized to bind the practice (developer terms).
   - Note the account's CRID; the USPS README says token requests need one.
2. **Create an app.** In the Business Portal, open **My Apps** and add an app with a name such as "Practice PMS – address check" (Getting Started, step 2).
3. **Copy the Consumer Key and Consumer Secret** from the app's Credentials section (Getting Started, step 3). Keep the secret in the practice's password manager, not in email or chat.
4. **Licence and payment (required since August 2026, section 2.7).**
   1. Read the Addressing API License Agreement (<https://postalpro.usps.com/Addressing_API_License>) and sign it.
   2. Set up a USPS Enterprise Payment System account with a payment method.
   3. Add the Addresses API licence in the Business Portal and link the app's credentials to it.
   4. Ask USPS to enable access (TEM and production) through <https://emailus.usps.com/s/usps-APIs>. Allow several days.
   5. Note the fee tier. A small practice is expected to fit the lowest tier; check the patient-form import volume too.
5. **MID, if asked.** If token requests fail and mention a missing Mailer ID, ask the account's Business Customer Gateway administrator for the MID. Report it in the project issue (open question 3).
6. **Quota.** If staff regularly see the "over the limit" prompt, ask USPS for a quota increase through a service request: <https://emailus.usps.com/s/usps-APIs> (FAQ).
7. **Read and accept the terms:** <https://developers.usps.com/terms-and-conditions> and <https://about.usps.com/termsofuse.htm>. Confirm the practice will check only addresses staff enter or import one at a time, so that patient mail is delivered. No batch cleanup.
8. **Privacy sign-off.** The privacy or HIPAA officer records that street, unit, city, state and ZIP (no name) are sent to USPS, which offers no BAA (section 2.6). The officer approves or declines before the integration is enabled.
9. **Configure the integration.**
   1. In **Setup > Program Links > USPS address validation**, enter the key and secret.
   2. Choose **Testing**, then **Test connection**.
   3. Switch to **Production**, test again, tick **Enabled** and click **OK**.
   4. In **Setup > Preferences > Family > General**, make sure "Verify Addresses with USPS" is ticked.
10. **Rotation and offboarding.** If the secret leaks, contact USPS for new credentials, then re-enter them. To stop, untick **Enabled**; also end the USPS licence and payment account if the practice no longer wants to be billed. The rows stay, and the secret can be cleared.

---

## 6. Test plan

There is no separate sandbox account. The **TEM** host (`apis-tem.usps.com`) is used with the office's production credentials (FAQ), once USPS has enabled the licensed access. Do these steps on a restored copy of `fox-db` or a dev database, with Windows set to culture **en-US**. Every production call is billable; keep production checks to a handful.

1. **Off by default:**
   - On a fresh database and on a migrated Open Dental database (preference = 1), save a patient with a changed address and import a patient form. Expect no prompt, no network traffic (check with Fiddler or a firewall log) and no error.
   - The Preferences checkbox is hidden.
2. **Setup:**
   - Opening Program Links creates the disabled "USPS address validation" row exactly once. Reopening does not duplicate it.
   - Save the key and secret. In `programproperty`, `Consumer Secret` starts with `osd1$`, is masked and is high-security, and a security-log entry is written.
3. **Test connection on TEM:**
   - Good credentials report connected.
   - A wrong secret shows the USPS 401 message.
   - Credentials not yet linked to a licence show the 403 message.
   - A blank key won't save while Enabled is ticked.
4. **Happy path on TEM**, with Enabled and the preference on. Edit a patient as in the table. Use only test or fake patients and public landmark addresses, never a real patient's address, until the privacy sign-off is done.

   | Input | Expect |
   |---|---|
   | `3120 M St NW`, Washington, DC, `20027` (modelled on the README sample, which sends "NW" as `secondaryAddress`) | Compare shows `3120 M ST NW`, WASHINGTON, DC, `20007-3704`, with no red warning |
   | `475 L'Enfant Plaza SW`, Washington, DC (no ZIP) | ZIP and ZIP+4 filled in. Exercises the apostrophe encoding |
   | A known multi-unit building with no unit | Red "…apartment/suite number is missing" (DPV `D`), or correction code 32 |
   | The same building with a unit that doesn't exist | Red "…not the apartment/suite number entered" (DPV `S`) |
   | A made-up street number on a real street | 404 message in "Failed to verify…", or DPV `N` |
   | State "Virginia" | Mapped to VA by the existing pre-check, then verified |
   | Zip `1234` | ZIP left out. Verified by city and state |

   For each row, check that Yes, No and X behave as in 1.1. Yes writes Zip as `NNNNN-NNNN`.
5. **Form import:** import a test patient form with an address. The compare window appears; X aborts the import, Yes and No continue it.
6. **Production:** switch the environment and repeat two rows.
7. **Failure modes:**
   - With the network cable pulled or `apis.usps.com` blocked, the "Could not reach USPS… Save anyway?" prompt appears within 15 s, Yes saves, and the program doesn't hang.
   - With the secret changed to a wrong value on another workstation, after the cache refresh the next save shows the credentials message.
   - Simulate 429 with the fake handler (unit test). Expect the "over the limit" prompt, then a quiet skip for 2 minutes. Do not hammer TEM or production to provoke it: every call may be billed.
8. **Token reuse:** on one workstation, 5 saves in a row make 1 token request and 5 address requests (Fiddler).
9. **Middle Tier:** on a Middle Tier client, the call goes out from the client machine and verification works.
10. **Culture:** with en-CA or en-GB, there is no prompt and the checkbox is hidden.
11. **Log hygiene:** grep the application logs after these steps. They contain no address text, token or secret.

---

## 7. Size, risks and open questions

### 7.1 Size: **S** (about 2 to 3 days of development)

| Item | Estimate |
|---|---|
| DTOs, enum, request mapping, `UspsApiClient` with token cache and error mapping | 0.75 d |
| `UspsAddresses` config, program and property insert helpers, `FormProgramLinks` hook | 0.5 d |
| `FormUspsAddressSetup` with Test connection | 0.5 d |
| `PatientL`, `FrmAddressCompare`, Preferences and NoHq edits; deleting the proxy method | 0.25 d |
| Unit tests and the manual TEM pass | 0.5–1 d |

Not included: the office's USPS onboarding (licence, payment account, access approval), which is calendar time outside development and blocks the manual TEM pass.

### 7.2 Risks

1. **Licence and cost.** Access now needs a signed licence and a payment account, and every lookup is billed. USPS could change the terms, price or availability again (the cut-over already moved once). *Mitigation:* the integration is optional and off by default; it fails soft; the office, not the project, holds the licence; calls are kept to the minimum.
2. **Terms and purpose.** The licence is tied to USPS mailing and shipping, and it bans address lists, datamining and building databases. Interactive checks of a patient's mailing address fit. Any future batch or "verify all" feature would not. USPS may also end access without notice. *Mitigation:* single-address use only (written into the code comments), fail-soft design, and the office accepts the terms itself.
3. **Quota.** The reported default is 60 calls per hour per app, shared by all of the office's workstations and both entry points, and USPS may change it. A busy check-in morning with many form imports could hit 429. *Mitigation:* token cache, a cool-down after 429, never blocking a save, the quota-increase path in section 5, and the import "changed" check (open question 8).
4. **The address travels in the URL query string.** That is the API's design, and there is no POST form. TLS protects it on the wire, but a TLS-inspecting office proxy or USPS's own access logs could keep it. *Mitigation:* never log URLs on our side. Offices with an inspecting proxy should exclude `apis.usps.com` from logging.
5. **HIPAA and no BAA.** An address is an identifier, and USPS offers no BAA. *Mitigation:* only address fields are sent, the feature is off by default, the setup text says exactly what is sent, and the privacy officer signs off. This is still less exposure than the Open Dental path, which also went through HQ.
6. **API drift.**
   - The enhanced release (3.3.1) added `DPVEnhancedConfirmation` with `R` ("no USPS delivery"), many more `additionalInfo` indicators and a top-level firm field; `business` is reported deprecated and `state` reported optional. Unknown fields are ignored (lenient Newtonsoft settings).
   - `expires_in` arrives as either a string or a number.
   - The developer FAQ still reads as if Addresses were free in every app; expect USPS's pages to keep changing.
7. **Unit folded into Address1.** USPS may return the unit inside `streetAddress` with `secondaryAddress` null, so accepting the result blanks Address2. This matches what Open Dental users saw (same field mapping), but staff may notice. The compare window shows it before they accept.
8. **Upper case.** USPS returns upper case, so accepting changes the patient's address casing. Open Dental behaved the same way.
9. **ProgName collision.** If an Open Dental 25.3 database already holds a program row with the same name and other properties, `InsertIfMissing` would adopt it. Check `fox-db` first (open question 1).
10. **`osd1$` is obfuscation only.** Same as every bridge secret. The impact is low (quota and billed lookups on the office's account).
11. **Bridges.dll split ordering.** If the Bridges split lands first and defines these types in a `Bridges` namespace, this spec's `OpenDentBusiness` types must replace them, not sit beside them. Coordinate with the QuickBooks Online and Microsoft 365 specs.

### 7.3 Open questions

1. **`fox-db` check.** `SELECT ProgName,Enabled FROM program WHERE ProgName LIKE '%USPS%' OR ProgDesc LIKE '%USPS%';` and `SELECT ValueString FROM preference WHERE PrefName='AddressVerifyWithUSPS';`. None is expected, because Open Dental used only the preference. Confirm before choosing the final ProgName `USPSAddresses`.
2. **Real quota and tier.** After onboarding, what hourly quota and fee tier does the office's app actually get? Is 60 per hour enough, or should the office request more from the start?
3. **CRID/MID.** The USPS README says a CRID and MID are needed for a token. Does an Addresses-only app from a new portal account work without a MID? Confirm in TEM at the first registration and update section 5.
4. **Scope.** Send `scope=addresses` (least privilege) or leave it out (app default)? Confirm in TEM that `addresses` is accepted.
5. **Title case.** Should the program convert USPS upper case to title case ("3120 M St NW") before it fills the boxes? It's an office preference, and out of scope for v1 unless the owner wants it.
6. **Enhanced DPV.** Should v2 read `DPVEnhancedConfirmation` and show "USPS does not deliver to this address" for `R`, and warn on vacant or CMRA?
7. **Licence text.** The office (or the owner) must read the Addressing API License Agreement PDF before signing: permitted use, whether storing the accepted address in the patient record is allowed, data handling, term, fees. We could not review its text. Ask USPS API support about anything unclear in the same service request as the quota question.
8. **Entry points and call volume.** Verification runs in Edit Patient (only when the address changed) and in patient-form import (every time). Should the import path skip unchanged addresses to save quota and fees? Should the Add Family window, Referral Edit or Employer Edit verify too? Each extra entry point spends the shared, billed quota. This needs product input.
9. **Shared helpers.** `Programs.InsertIfMissing` and `ProgramProperties.InsertIfMissing` should be agreed once across the vendor-direct specs (see `postcardmania.md`, open question 7).

### 7.4 References

- USPS Addresses 3.0 OpenAPI (3.3.1): <https://developers.usps.com/sites/default/files/apidoc_specs/addresses-v3r2_0.yaml>, page <https://developers.usps.com/addressesv3>
- USPS OAuth 2.0 OpenAPI (3.1.5): <https://developers.usps.com/sites/default/files/apidoc_specs/oauth2_update_1.yaml>, page <https://developers.usps.com/Oauth>
- USPS API examples (README: token, address samples, TEM host, CRID/MID, leaked credentials): <https://github.com/USPS/api-examples>
- Getting started: <https://developers.usps.com/getting-started>
- FAQ (keys, default product, TEM, 429 and quota): <https://developers.usps.com/faq>
- Developer terms and conditions: <https://developers.usps.com/terms-and-conditions>; USPS terms of use: <https://about.usps.com/termsofuse.htm>
- Addressing API License Agreement (PostalPro): <https://postalpro.usps.com/Addressing_API_License>; Enhanced Addresses API industry alert (2026-06-01): <https://postalpro.usps.com/node/15211>
- USPS newsroom, July 2026 competitive price changes (new Addresses API offering): <https://about.usps.com/newsroom/national-releases/2026/0511-usps-recommends-competitive-price-changes-for-july-2026.htm>
- USPS API support (service requests, onboarding): <https://emailus.usps.com/s/usps-APIs>
- Customer Onboarding Portal: <https://cop.usps.com>
- Legacy address API use rules (Web Tools): <https://www.usps.com/business/web-tools-apis/address-information-api.htm>
- Secondary unit designators (Pub 28): <https://pe.usps.com/text/pub28/28c2_003.htm>
- Third-party reports (unconfirmed by USPS pages): rate limit, <https://www.smarty.com/blog/usps-api-rate-limit>; Web Tools retirement and per-token limits, <https://directmail.io/blog/usps-api-marketplace-explained-npf-2026>; licensed cut-over and onboarding steps, <https://github.com/CannObserv/address-validator/issues/155> and <https://www.ablecommerce.com/forums/posts/m8721-USPS-Addresses-API-change>; fee tiers, <https://apicostcalc.com/blog/usps-address-api-no-longer-free-2026.html>
- HHS de-identification guidance (address as an identifier): <https://www.hhs.gov/hipaa/for-professionals/special-topics/de-identification/index.html>
- In-repo: `D:/Dev/opendental-schema/hq-vendor-table.md` (USPS row), `docs/decoupling-plan.md` (ground rules 5–7, OpenDentalHelp and Bridges rows), `docs/third-party-binaries-audit.md` (Bridges.dll), `CDT/README.md` (osd1 threat model), `docs/specs/vendor-direct/postcardmania.md` (runtime program-row pattern)

---

## Fact-check log (2026-10-18)

Changes made to the first draft:

1. **Feasibility corrected** from "public API, free and self-service, no agreement" to "public API, licence- and fee-gated". USPS made the Addresses API a licensed, paid offering in 2026 (newsroom release, PostalPro licence page; cut-over reported as 2026-08-01). Added section 2.7 and the licence and payment steps in section 5. The developer FAQ's "default product" wording is stale.
2. **Second caller added.** `FormSheetImport.cs` ~3234 also calls `VerifyPatientAddressInteraction`, on every patient-form or eForm import, with no "changed" check. The draft said Edit Patient was the only caller. Noted the unused `FrmEFormImportRules` checkbox.
3. **`FrmAddressCompare.xaml.cs` needs `using OpenDentBusiness;`.** It has none today, so the draft's "just drop `using Bridges;`" would not compile.
4. **Token example.** The USPS README example sends no `scope`; the draft said USPS's own example included `scope: addresses`.
5. **Terms.** Rewrote the purpose and prohibited-use bullets in our own words and removed the direct quote. Added the bans on building databases or AI models and the credential-sharing nuance. The operational-test clause is now read as applying to all Addresses users (it requires signing a licence or losing access), not only AMS licensees.
6. **Codes.** Match code 31 sits in `matches`; codes 32 and 22 sit in `corrections`. `DPVVacant` added; `business` marked as reportedly deprecated.
7. **Rate limits and Web Tools.** The Smarty post gives no exact retirement date; cited a source that does. Noted the conflicting per-minute report and that the developer terms put limits on each API's page (none shown for Addresses).
8. **API drift.** The "2026-07-12 enhancements" were the licensed Enhanced Addresses cut-over, later postponed to 2026-08-01.
9. **Rotation.** No documented self-service reissue; the README says to contact USPS.
10. **Smaller fixes.** OAuth spec version (3.1.5), CRID/MID appearing only in the README, Bridges.dll reference graph (OpenDentBusiness also references it, so the inferred dependency runs both ways), the `using OpenDentalHelp;` removal in `PatientL` (no other use), the culture mismatch between gate and checkbox, `ProgressWin`'s location, `ProgramName` ending at `ZImage`, the `InsertIfMissing` helpers being proposals, test-connection and test-plan cost notes, and a form-import test step.

Checked and found correct: base URLs and TEM; client-credentials flow, response fields and the absence of a refresh token; `/address` parameters, patterns and the no-POST design; 404 examples; every other code touchpoint and line number; no compiled-in credentials; no Open Dental hosts in the new path; a neutral setup window.
