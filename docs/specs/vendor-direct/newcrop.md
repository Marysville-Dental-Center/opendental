# NewCrop (Ensora) eRx account ID and the OID root: direct-to-vendor spec

Status: draft, 2026-10-18, fact-checked the same day (see "Fact-check log" at the end). Baseline: `main` at `6855026f5`. Vendor: Ensora Health, formerly NewCrop (the hosts are still `*.newcropaccounts.com`). The OID root part has no vendor.

## Summary

- **One HQ call fed two things.** `CustomerUpdates.RequestCustomerID` returned the office's Open Dental customer number, and the code used it twice:
  - **NewCrop account ID.** On the first eRx click, `ControlChart` built `<custnum>-<3 random chars><2-digit checksum>` and saved it to the `NewCropAccountId` pref.
  - **OID root.** `FormOIDRegistryInternal` built `2.16.840.1.113883.3.4337.1486.<custnum>`, under Open Dental's own registered OID arc. `OIDInternals.CustomerPatNum` (used for the DoseSpot patient-ID root) reads the same number.
  - Both calls now throw `HqNotAvailableException`.
- **The bigger NewCrop dependency is compiled into the source, not served by HQ.** `NewCrop.NewCropAccountName` and `NewCropAccountPasssword` return **Open Dental's own NewCrop partner credentials**: encrypted constants that `CodeBase.MiscUtils.Decrypt` (an in-repo key) turns back into plain text. Separate constants exist for Debug, FDB and LexiData. `NewCropPartnerName` returns `OpenDental` or `OpenDentalLexi`, and the product name is hard-coded as `OpenDental`.
  - We must **not** use these. They belong to Open Dental's partner contract.
  - They also conflict with ground rule 4 (no Open Dental trademark) and with the rule against compiled-in credentials.
  - They get deleted, not replaced by different constants.
- **Feasibility: partner-only** (confirmed on 2026-10-18).
  - Ensora sells eRx to EHR vendors as an embedded product, but it publishes no developer documentation, no sandbox sign-up and no self-serve onboarding. Its own quick-start guide assumes the EMR is already integrated before an office can subscribe prescribers ([Ensora eRx quick start](https://experience.ensorahealth.com/get-started-49/eprescribe-quick-start-2516)).
  - The SOAP endpoints can be reached, and their auto-generated operation list is public. But the credentials, the click-through XML schema and the validation process only come with a partner agreement, or through an authorized Ensora reseller.
- **Auth is simple once credentials exist.** There is no OAuth: every SOAP call and every click-through XML carries a static triplet (`PartnerName`, `Name`, `Password`), plus `AccountId` and `SiteId`. So the desktop app needs no browser redirect, loopback listener, PKCE or token refresh.
- **Direct design:**
  - eRx Setup gets a NewCrop panel that stores these values as `programproperty` rows on the eRx program, with the password as `osd1$`. The partner name, account name and account ID are not secret. A "Test connection" button calls `GetAccountStatusV2`.
  - The account ID is entered (when Ensora or a reseller assigns it) or generated locally. The HQ checksum format goes away.
  - HQ's provider gating, which was also NewCrop's billing control, becomes a local per-provider enable list held in the existing `providererx` table.
  - This is the existing "distributor" code path made general, with Open Dental's own credentials removed.
- **OID root is fully doable now, offline:**
  - **Generate** a root under the ITU-T X.667 UUID arc (`2.25.<128-bit decimal>`). This needs no registration.
  - Or **enter** a root the office registered: a free IANA Private Enterprise Number (`1.3.6.1.4.1.<PEN>`), or an HL7 registry OID (paid).
  - Existing rows are never rewritten silently.
- **Interim:** NewCrop stays configurable but disabled. With no credentials configured, the eRx button refuses NewCrop with a neutral "NewCrop is not configured" message, and the Chart refresh skips NewCrop. The OID change can ship right away.
- **Size: M** overall: about 1 day for the OID work and 3 to 5 days for NewCrop, plus preproduction checks. Calendar time depends on Ensora onboarding. Open Dental now points practices that are new to eRx at DoseSpot, while existing Ensora users may stay or switch ([Open Dental DoseSpot page](https://www.opendental.com/site/dosespot.html)). So first check whether this office uses NewCrop at all (§6, step 1). If it doesn't, ship only the OID part and the credential removal, and leave NewCrop dormant.

## 1. What the integration does, and what HQ provided

### 1.1 What the office uses it for

All of it is optional. It runs only when **Setup > Program Links > eRx** is enabled with the NewCrop option, or "DoseSpot with NewCrop". Manual prescriptions never depend on it.

| Feature | Where in code | NewCrop interaction |
|---|---|---|
| Write a prescription in NewCrop's own web UI (compose, pharmacy, interaction checks, EPCS, renewals) | `ControlChart.Tool_eRx_Click` (`ControlChart.cs:8487` on) → `FormErx.ComposeNewRxNewCrop` (WebView2) | `POST` of base64 click-through XML (`NCScript`) to `interfacev7/rxentry.aspx` |
| Staff prescribing for a provider | `ErxXml.BuildNewCropClickThroughXml` (`Staff` / `UserRole` elements) | same |
| Pull current prescriptions back into `rxpat` / `medicationpat` on every Chart refresh and after the eRx window closes | `ControlChart.NewCropRefreshPrescriptions` (`:7851`) → `NewCrop.RefreshPrescriptions` | SOAP `Update1.GetPatientFullMedicationHistory6`, 3-second timeout |
| Provider and clinic (location) data so NewCrop can identity-proof and bill prescribers | `ErxXml` `LicensedPrescriber`, `MidlevelPrescriber`, `Location` | part of the click-through |
| A hidden "send provider data without opening eRx" path, used while a provider was not yet enabled | `Erx.ComposeNewRxNewCropWebRequest` (`Erx.cs:325`) | same POST, response discarded |

Identifiers in that traffic: `Account.ID` comes from the `NewCropAccountId` pref, and `Account.siteID` is always `"1"`. `Location.ID` is `ClinicNum`, or `"0"`. `LicensedPrescriber.ID` is the NPI, because NewCrop bills per distinct prescriber ID per month. `Patient.ID` is the PatNum.

Open Dental's public manual describes the office side: identity proofing through Exostar, EPCS administrators, and staff prescribing via the RxCreate permission ([NewCrop eRx FAQ](https://opendental.com/manual/newcropfaq.html)). It also describes the setup screen and the HQ-only "Clear Account ID" button ([eRx program link](https://opendental.com/Manual/bridgeerx.html)).

### 1.2 What Open Dental provided

Sources: `D:/Dev/opendental-schema/hq-vendor-table.md` (row "NewCrop eRx account ID (also OID root)") and the code below.

| What | How it reached the office | Code | Needed after decoupling? |
|---|---|---|---|
| **Customer number**, used as the NewCrop account ID prefix | `CustomerUpdates.RequestCustomerID` (XML with the registration key) | `ControlChart.cs:8493–8540`; checksum check in `NewCrop.NewCropIsAccountIdValid` (`NewCrop.cs:82`) | **No.** A locally entered or generated account ID (§3.2). |
| **Customer number**, used as the OID root suffix (`2.16.840.1.113883.3.4337.1486.<n>`) | same call, with `RegKeyDisabledOverride=true` | `FormOIDRegistryInternal.butRetrieveOIDs_Click` (`:86`); `OIDInternals.CustomerPatNum` (`:19`), read by `DoseSpot(V2).GetDoseSpotRoot` | **No.** A local root (§3.5). |
| **NewCrop partner credentials** (partner name, account name, password), separate sets for production FDB, production LexiData and Debug/preproduction | **compiled in**, decrypted with the in-repo `MiscUtils.Decrypt` | `NewCrop.NewCropPartnerName`, `NewCropAccountName`, `NewCropAccountPasssword` (`NewCrop.cs:16–71`) | **Yes, but our own**, configured per office (§3.2). The constants are deleted. |
| **Product name** `OpenDental` sent to NewCrop | compiled in | `NewCrop.NewCropProductName` | Yes, our own product name as registered with Ensora |
| **Per-provider eRx enable and IDP status** (and HQ's monthly billing check, `NewCropDateLastAccessCheck`) | `CustomerUpdates.GetErxAccess` | `ControlChart.UpdateErxAccess` (`:9154`), gate at `:8852–8870` | **No.** A local enable list (§3.4). |
| HQ "kill switch" | `Programs.IsEnabledByHq(ProgramName.eRx)` | `ControlChart.cs:8422` (eRx click), `:7857` (NewCrop history refresh), `FormErxSetup.butSave_Click` | No. Phase 1 removes it. |
| HQ and distributor back-office tools: NewCrop billing import, the per-customer eRx access editor, the "eRxTeamPW" password on Clear Account ID | in-repo forms gated by `DistributorKey` / chart buttons | `FormNewCropBilling`, `FormErxAccess`, `ControlChart.butErxAccess_Click`, `FormErxSetup.butClearAccountId_Click` | **No.** Delete them (Phase 3). The hard-coded support password goes away. |
| "Distributor" override: `NewCropName`, `NewCropPartnerName` and `NewCropPassword` prefs (plain text, no UI) | prefs set by a reseller | `NewCrop.cs`, `ControlChart.cs:8546`, `:9191` | This becomes **the** path, moved into encrypted program properties. |

**Bridges.dll is not involved.** NewCrop uses only in-repo generated code:

- the `NewCropWebService` web reference (`OpenDentBusiness/Web References/NewCropWebService/Reference.cs`, generated from the `Update1.asmx` WSDL)
- `NCScript.cs`, generated from NewCrop's click-through XSD

## 2. The vendor's public API

### 2.1 What is public

- **Product and company.** Ensora markets the product as "Ensora eRx (formerly NewCrop)": Surescripts routing, EPCS, formulary data, and FDB or LexiData drug checks. The product page talks about EHR partners and integration in marketing terms and lists general sales and support phone numbers, but has no developer, API or partner-sign-up information ([Ensora eRx features](https://ensorahealth.com/product/erx/features)). Search results also show a page aimed at healthcare IT vendors (`ensorahealth.com/who-we-serve/healthcare-it-vendors`), but it returned 404 when checked on 2026-10-18.
- **Office onboarding, per Ensora.** Ensora's quick-start guide says the EMR must already be integrated. After that, an organization administrator subscribes prescribers and staff. Each prescriber then registers for eRx with EPCS (identity proofing and a token), for eRx only, or as admin/nurse, and EPCS administrators are named ([Ensora eRx quick start](https://experience.ensorahealth.com/get-started-49/eprescribe-quick-start-2516)). The guide doesn't name the identity-proofing provider. Users of OpenEMR (another open source EHR) also describe the rename ([OpenEMR community](https://community.open-emr.org/t/new-crop-name-change/25264)).
- **Access model.** OpenEMR reaches NewCrop only through authorized vendors. After the practice contracts with one, that vendor puts the Ensora account settings into OpenEMR ([OpenEMR ePrescribe wiki](https://www.open-emr.org/wiki/index.php/OpenEMR_ePrescribe)). A practice that asked NewCrop directly was told to go through the EHR instead ([OpenEMR forum thread](https://community.open-emr.org/t/newcrop-registration-process/21154)). The same wiki says the OpenEMR vendor holds its own DEA EPCS certification and that it can't be used by other vendors. A 2015 VistA community write-up (not official NewCrop documentation) describes the path for a new integration: contract, test scenarios against the demo system, demos to NewCrop, Surescripts certification, then a production account, with controlled substances certified separately ([VistApedia episode 78](https://vistapedia.com/index.php/Episode_78_ePrescribing(eRX)_Certification_with_NewCrop)).
- **SOAP service.** The ASP.NET help page of `Update1.asmx` is reachable without authentication on both production and preproduction. It lists 110 operations on both hosts (checked 2026-10-18), including `GetPatientFullMedicationHistory6`, `GetAccountStatusV2`, `LicensedPrescriberStatusV2` and `RegisterLicensedPrescriberV2`. It shows that `GetAccountStatusV2` takes `credentials` (`PartnerName`, `Name`, `Password`), `accountRequest` (`AccountId`, `SiteId`), `locationId`, `userType` and `userId`. It returns a `result` (`Status`, `Message`, `XmlResponse`, `RowCount`, `Timing`) plus account counters such as pending prescriptions and unanswered renewals ([Update1.asmx](https://secure.newcropaccounts.com/v7/WebServices/Update1.asmx); [GetAccountStatusV2](https://secure.newcropaccounts.com/v7/WebServices/Update1.asmx?op=GetAccountStatusV2); [preproduction](https://preproduction.newcropaccounts.com/v7/WebServices/Update1.asmx)). The `Status` values the schema lists are `Unknown`, `OK`, `Fail` and `NotFound`. The help page gives no semantics, field rules or error meanings beyond that.
- **Not public:** the click-through (NCScript) specification, account ID rules, the partner on-boarding and validation process, rate limits, pricing, or any sandbox sign-up. These come with a partner agreement.

### 2.2 Base URLs

| Purpose | Production | Preproduction (sandbox) | Source |
|---|---|---|---|
| Click-through UI (browser POST) | `https://secure.newcropaccounts.com/interfacev7/rxentry.aspx` | `https://preproduction.newcropaccounts.com/interfaceV7/rxentry.aspx` | `Erx.GetLegacyUrl` (`Erx.cs:316`); the preproduction host answers |
| SOAP `Update1` | `https://secure.newcropaccounts.com/v7/WebServices/Update1.asmx` | `https://preproduction.newcropaccounts.com/v7/WebServices/Update1.asmx` | `NewCrop.RefreshPrescriptions`; both help pages are public |

Today a **Debug build** silently switches both URLs to preproduction, with Open Dental's Debug credentials. That switch is replaced by an explicit environment property (§3.2). `Introspection.NewCropRxEntryURL` and `NewCropUpdate1URL` remain as developer overrides.

`OpenDentBusiness/Properties/Settings.Designer.cs:148` carries a default `http://secure...Update1.asmx` value for the web reference. Both `app.config` files (`OpenDentBusiness/app.config:54`, `OpenDental/app.config:23`) already use `https`. `RefreshPrescriptions` always overwrites `Url`, so the `http` default is harmless. Change it to `https` anyway, to keep the CI host grep clean.

The host name `secure.newcropaccounts.com` also appears in `NCScript.cs` (XML namespaces) and in the generated `Reference.cs` (SOAP namespaces and SOAPAction values). These are identifiers, not network calls, so they stay as they are.

### 2.3 Auth flow

1. Ensora (or a reseller) issues a **partner name**, an **account name** and a **password** for the preproduction environment, and later for production. It also agrees on, or assigns, an **account ID** and a **site ID** for the office.
2. **Click-through:** the app builds `NCScript` XML containing `Credentials` (the triplet plus `productName` and `productVersion`), `Account`, `Location`, the prescriber or staff member, the patient and the destination page. It base64-encodes the XML, URL-escapes it, and POSTs it as `RxInput=base64:...` (form-encoded) from the WebView2 control to `rxentry.aspx`. NewCrop authenticates the triplet, creates or updates the account, location, prescriber and patient on the fly, and renders its UI. There's no token or session handed back to us.
3. **SOAP:** every `Update1` call carries the same `Credentials` object and an `AccountRequest` (`AccountId`, `SiteId`). Again there are no tokens. A failed credential check comes back as a `Status` other than `OK`. The schema's values are `Unknown`, `OK`, `Fail` and `NotFound`. The existing code comment (`NewCrop.cs:173`) says bad credentials give `Fail`, and a bad patient or account ID gives `NotFound`.

So **no OAuth, PKCE, device code or loopback redirect applies.** It works like a long-lived client credential, and there's nothing to refresh.

### 2.4 Endpoints and fields we need

| Call | Request (ours) | Response (used) | Status |
|---|---|---|---|
| `rxentry.aspx` (POST) | `NCScript`: `Credentials`, `UserRole`, `Destination.requestedPage=compose`, `Account` (ID, name, siteID, address, phone, fax), `Location` (ID = ClinicNum or 0, address), `LicensedPrescriber` / `MidlevelPrescriber` (ID = NPI, name, DEA, licence), `Staff`, `Patient` (ID = PatNum, demographics, no SSN), diagnoses, height and weight | the HTML UI | existing; only the credential and account sources change |
| `GetPatientFullMedicationHistory6` | credentials, account, `PatientId`, history window, `PrescriptionStatus=C`, `SubStatus=S`, `ArchiveStatus=N`, requester type and ID, `includeSchema=N` | `Result.Status`, base64 `XmlResponse` (drug, dispense, sig, `ExternalPatientID`, prescriber, pharmacy, GUID) | existing |
| `GetAccountStatusV2` | credentials, account, `locationId`, `userType`, `userId` | `AccountStatusDetailV2Result.result.Status` and `.Message` | **new**, used only by the "Test connection" button. The in-repo proxy already has it (`Reference.cs:2936`), so nothing needs regenerating. Field semantics (which `userType`/`userId` to send for a pure credential check) must be confirmed in the partner docs. |
| `LicensedPrescriberStatusV2` | credentials, account, `licensedPrescriberId`, `locationId` (signature from the in-repo proxy, `Reference.cs:3408`) | `RegisterPrescriberDetailV2Result`: prescriber registration and IDP state | **optional, later.** It could fill `providererx.IsIdentifyProofed` automatically. It isn't built until the partner docs confirm what it returns. `RegisterLicensedPrescriberV2` (`Reference.cs:3375`) also appears in the operation list. Its name suggests a way to register a prescriber without a click-through, but only the partner docs can confirm that. |

### 2.5 Rate limits

None published. The app's own load is one `GetPatientFullMedicationHistory6` per Chart refresh while NewCrop is active. §3.4 adds a throttle so that one busy workstation can't produce hundreds of calls an hour.

### 2.6 Terms that matter

- **Partner agreement or reseller.** It decides who holds the triplet and who Ensora bills. Ensora may require its own validation of a new integrating product, because the product name and version are sent on every click-through, and it may also require Surescripts-related certification. Confirm both (§7).
- **BAA.** The click-through and the history call carry PHI: patient demographics, diagnoses and medications. The office, as a HIPAA covered entity, needs a business associate agreement with Ensora, or with the reseller plus a downstream BAA ([HHS business associates](https://www.hhs.gov/hipaa/for-professionals/privacy/guidance/business-associates/index.html)).
- **Billing per prescriber.** NewCrop bills per distinct `LicensedPrescriber.ID` per month (code comment in `ErxXml.cs:114–121`). HQ's gating existed partly to stop accidental charges. The local enable list keeps that protection.
- **Identity proofing and EPCS.** According to Open Dental's manual, prescribers complete IDP through Exostar inside NewCrop, and the DEA requires at least two EPCS administrators, one of whom must hold EPCS rights ([NewCrop eRx FAQ](https://opendental.com/manual/newcropfaq.html)). Ensora's own guide describes the EPCS registration and administrators but doesn't name the IDP provider ([Ensora eRx quick start](https://experience.ensorahealth.com/get-started-49/eprescribe-quick-start-2516)). These are vendor-side steps, and we only link the provider. **Open point:** OpenEMR's vendor says it holds its own DEA EPCS certification, which other vendors can't use ([OpenEMR ePrescribe wiki](https://www.open-emr.org/wiki/index.php/OpenEMR_ePrescribe)). Ask Ensora whether a click-through integration like ours needs its own EPCS audit or certification, or whether Ensora's covers it.
- **Product name.** We must send our own product name and not `OpenDental` (ground rule 4). Ensora must accept the new name.

## 3. Desktop design

### 3.1 Auth flow on the workstation

There's nothing interactive. On each use, the app reads the stored triplet, decrypts the password and builds the click-through or the SOAP credentials. It talks only to `*.newcropaccounts.com` over TLS 1.2+. No Open Dental host, no HQ, and no loopback listener.

Hardening for the embedded browser:

- **Dev tools off in `FormErx`.** The password travels inside the form post, so set `CoreWebView2.Settings.AreDevToolsEnabled=false` for the NewCrop path. A local user could otherwise inspect the post.
- **No credentials in logs or dialogs.** `erxlog.MsgText` and the Introspection `MsgBoxCopyPaste` dialog get the click-through XML with the `<Credentials>` element **redacted**. Today the full XML, password included, is written to `erxlog` (`ControlChart.cs:8922`) and shown in the Introspection dialog (`:8878–8881`).
- **No PHI in temp files.** In Debug builds `RefreshPrescriptions` writes the decoded medication XML to a temp file (`NewCrop.cs:184–188`). Delete that block along with the other Debug branches.

### 3.2 Where configuration lives

These are all rows on the eRx `program`. Missing rows are inserted at runtime by a `NewCropConfig.InsertMissingProperties()`. There is no schema change and no ConvertDatabases step.

| Where | PropertyDesc / key | ClinicNum | Value | Notes |
|---|---|---|---|---|
| programproperty | `eRx Option` | 0 | `0` NewCrop or `2` DoseSpot with NewCrop | existing |
| programproperty | `NewCrop Environment` | 0 | `Production` or `Preproduction` | **new**, default `Production`. Replaces `ODBuild.IsDebug()`. |
| programproperty | `NewCrop Partner Name` | 0 | text | **new**, not secret |
| programproperty | `NewCrop Account Name` | 0 | text | **new**, the credentials `Name`, not secret |
| programproperty | `NewCrop Password` | 0 | `osd1$...` via `CDT.Class1.Encrypt` (check its return value) | **new**, secret. `IsMasked=1` and `IsHighSecurity=1`, so editing it needs `ManageHighSecurityProgProperties` (`ProgramProperties.CanEditProperties`), as in `dosespot.md`. |
| programproperty | `NewCrop Account ID` | 0 | text | **new.** Entered, or generated locally (below). |
| programproperty | `NewCrop Site ID` | 0 | default `1` | **new.** Kept configurable in case Ensora assigns another. |
| programproperty | `NewCrop Product Name` | 0 | our registered product name | **new**, default the build's product name constant. Configurable only because Ensora approves the exact string. |
| preference | `NewCropAccountId` | | Open Dental-issued ID, for example `1234-W6c43` | **left untouched**, shown read-only in Setup as "Previous account ID" for the migration conversation with Ensora (§7). Never sent under our credentials unless the office copies it into `NewCrop Account ID` after Ensora agrees. |
| preference | `NewCropName`, `NewCropPartnerName`, `NewCropPassword` | | reseller values, plain text | **imported once:** if present and the new properties are empty, copy them in (password encrypted), then blank `NewCropPassword`. That's a data update, not a schema change. |
| preference | `NewCropIsLexiData`, `NewCropIsLegacy`, `NewCropDateLastAccessCheck` | | | no longer read; kept for schema compatibility |
| providererx | rows with `ErxType=0` (NewCrop) | | `IsEnabled`, `IsIdentifyProofed` | existing table, now **set locally** (§3.4); `IsSentToHq` ignored |

**Read rules,** the same as the DoseSpot spec:

- A value with the `osd1$` prefix is decrypted with `CDT.Class1.Decrypt(value, out plain)`, **not** `TryDecrypt`. `TryDecrypt` hands back the ciphertext unchanged when decryption fails (`CDT/Class1.cs:73`), which would send garbage as the password. If `Decrypt` returns false, the setting counts as not configured, and the office must re-enter it. Never send it.
- Writes use `CDT.Class1.Encrypt` and check its result. `TryEncrypt` silently returns the plain text on failure, and that must not be saved as if it were protected.
- An unprefixed value is used as is and encrypted on the next save.
- `osd1$` is obfuscation at rest with a fixed key in this repo, not strong secrecy (`CDT/README.md`, "Threat model"). Anyone with database read access and this source can recover the password. That is one more reason to use an office-scoped NewCrop credential (§7).
- `FormErxSetup.butSave_Click` saves through `ProgramProperties.Sync(_listProgramProperties, ...)`, which deletes rows missing from the list. The NewCrop panel must edit the same in-memory list (or `InsertMissingProperties` must run before the form loads it). Otherwise a save from the DoseSpot grid wipes the NewCrop rows.

**Account ID generation.** "Generate" produces a random ID from `RNGCryptoServiceProvider`: a fixed project prefix plus 10 uppercase alphanumeric characters. It's unique per database without any central registry. The length and character limits must be confirmed against the partner's NCScript spec. If Ensora or a reseller assigns the ID, the office types it in and generation is never used. `NewCropIsAccountIdValid` and its checksum are deleted, so "valid" now means "all required properties present".

**Never stored:** anything from NewCrop's responses beyond the existing `rxpat` / `medicationpat` data.

### 3.3 Refresh handling

There are no tokens, so nothing needs refreshing. Password rotation is a re-entry in eRx Setup, and the next call uses the new value. Workstations don't cache the decrypted password beyond a single call.

### 3.4 Gating, throttling, errors and offline behaviour

- **Local provider gate.** This replaces `GetErxAccess`.
  - eRx Setup's NewCrop panel lists providers who have an NPI, each with an "Enabled for NewCrop" box that maps to `providererx.IsEnabled=Enabled`. Editing it needs the `SecurityAdmin` permission, which replaces the hard-coded "eRxTeamPW".
  - `UpdateErxAccess` for NewCrop only inserts a missing `providererx` row (Disabled). It makes no network call, and the `UpdateServerAddress` "opendentalsoft.com" check is deleted.
  - `Tool_eRx_Click` blocks a provider who isn't enabled, with a neutral message: "Provider X is not enabled for eRx in eRx Setup."
  - In "DoseSpot with NewCrop" mode, the NewCrop choice is offered only when `provider.IsErxEnabled==EnabledWithLegacy` (`ControlChart.cs:8445`). That flag is set by the "allow legacy" box in `FormProvEdit` (`:186`, `:571–572`). It stays as the per-provider switch for the combined mode, and the local `providererx` gate applies after it.
  - The local `IsIdentifyProofed` check is **dropped**, because NewCrop enforces IDP itself. A later version may fill it from `LicensedPrescriberStatusV2`.
- **Hidden POST removed.** `Erx.ComposeNewRxNewCropWebRequest`, the "send provider info without access" path, is deleted. A provider gets registered at NewCrop the first time an enabled provider clicks through.
- **Throttle on history refresh.** `NewCropRefreshPrescriptions` runs when the eRx window closes, on explicit Refresh, and otherwise at most once per patient per 10 minutes per workstation (an in-memory map).
  - The 3-second timeout stays.
  - It runs only if `NewCropConfig.IsConfigured()`.
- **Errors:**
  - A `Result.Status` other than `OK`, or a timeout, is skipped silently in the Chart (as today), and a line goes to the local log.
  - "Test connection" shows the status and message text.
  - A WebView2 navigation failure shows a message in `FormErx`.
  - A decrypt failure means "not configured".
- **Offline:** Chart, manual Rx and everything else work. The eRx button reports that NewCrop can't be reached, and history sync resumes on the next refresh. No queueing is needed: NewCrop is the record of truth, and the pull is idempotent (keyed by the prescription GUID).
- **Hosts:** only `secure.newcropaccounts.com` and `preproduction.newcropaccounts.com`. No Open Dental host and no HQ.

### 3.5 OID root (no vendor)

What OIDs do here: `oidinternal` holds a root plus `root.1` … `root.8` (LabOrder, Patient, Provider, CqmItem, Problem, Appointment, InsPlan, Procedure). These are sent to outside systems in HL7 v2 messages (`HL7/FieldConstructor.cs`), CCDs (`EhrCCD.cs:231`), Infobutton requests and EHR lab orders. They're matched on inbound HL7 (`MessageParser`, `FieldParser`, `Patients.cs:1503`). The root has to be globally unique and controlled by the office. Minting new ones under Open Dental's arc `2.16.840.1.113883.3.4337` isn't ours to do.

New behaviour in `FormOIDRegistryInternal`:

- **"Retrieve OIDs"** becomes **"Generate root"**. It builds `2.25.` followed by the decimal value of a new random (version 4) UUID, per ITU-T X.667 | ISO/IEC 9834-8. That arc allows OIDs without any registration procedure ([ITU-T UUID page](https://www.itu.int/en/ITU-T/asn1/Pages/UUID/uuids.aspx); [2.25 arc](https://www.alvestrand.no/objectid/2.25.html)). The ITU page warns that weak randomness makes duplicate UUIDs more likely, so use a cryptographic RNG (`RNGCryptoServiceProvider`), not `Guid.NewGuid()` on an unknown platform. The result is at most 44 characters, and 46 with a one-digit sub-arc.
- **"Enter root"** takes an OID the office obtained:
  - **IANA Private Enterprise Number:** free, gives `1.3.6.1.4.1.<PEN>` ([PEN application](https://www.iana.org/assignments/enterprise-numbers/assignment/apply); [RFC 9371](https://iana.org/go/rfc9371)). We recommend `1.3.6.1.4.1.<PEN>.1` as the practice-management root, leaving the PEN's other arcs free.
  - **HL7 OID registry:** a paid registration, reported as USD 500 per OID ([Infoway forum summary of HL7's fee](https://infocentral.infoway-inforoute.ca/en/forum/221-hl7-canada/3736-need-to-register-an-oid-for-your-hl7-project-do-it-with-infoway); [HL7 OID FAQ](https://wiki.hl7.org/HL7_OID_Registry_Frequently_Asked_Questions)). Only worth it if a trading partner insists.
  - Validation: `^[0-2](\.(0|[1-9][0-9]*))+$`, at most 64 characters for the root.
- Child rows are filled as `root.N` as today. The existing prompt before overwriting custom child values stays. Its text changes from "the root provided by Open Dental" to "the new root".
- **Existing values are never changed automatically.** If the root starts with `2.16.840.1.113883.3.4337.1486.`, the form shows a note: the root was issued by the previous vendor, and changing it changes the identifiers that trading partners may already hold. The office decides (§7).
- `OIDInternals.CustomerPatNum` is **deleted**. For `DoseSpot(V2).GetDoseSpotRoot`, follow `dosespot.md` §3.2: a local suffix `0` under the existing `...4337.25128` prefix, which is only a local lookup key and is never sent anywhere. `OIDInternals.OpenDentalOID` stays only as the prefix that legacy rows match on. Rename it `LegacyOidArc` with a comment.

## 4. Code changes

### 4.1 Files and members

| File | Member | Change |
|---|---|---|
| `OpenDentBusiness/WebBridges/Erx/NewCrop/NewCropConfig.cs` | *new* | Property names; `InsertMissingProperties`; `Load()` (decrypts) / `Save()` (encrypts); `IsConfigured()`; `GetRxEntryUrl()` and `GetUpdate1Url()` from the environment (Introspection overrides honoured); `GenerateAccountId()`; `ImportLegacyPrefs()`; `TestConnection()` → `GetAccountStatusV2` |
| `OpenDentBusiness/WebBridges/Erx/NewCrop/NewCrop.cs` | `NewCropPartnerName`, `NewCropAccountName`, `NewCropAccountPasssword`, `NewCropProductName` (`:16–75`) | Read from `NewCropConfig`. **Delete the encrypted Open Dental constants, the hard-coded `OpenDental` / `OpenDentalLexi` partner names, the `OpenDental` product name and the Debug branches.** |
| | `NewCropIsAccountIdValid` | Delete. Callers use `NewCropConfig.IsConfigured()`. |
| | `RefreshPrescriptions` | URL and credentials from config; site ID from config; early return if not configured. Delete the `ODBuild.IsDebug()` URL switch (`:125–127`) and the Debug temp-file dump (`:184–188`). |
| `OpenDentBusiness/WebBridges/Erx/ErxXml.cs` | `BuildNewCropClickThroughXml` | `Account.ID` and `siteID` from config; credentials via `NewCrop.*` |
| | *new* `RedactNewCropCredentials(string xml)` | Blanks `<Credentials>` children for logging and display |
| `OpenDentBusiness/WebBridges/Erx/Erx.cs` | `GetLegacyUrl` (`:316`) | → `NewCropConfig.GetRxEntryUrl()` (rename); drops its `ODBuild.IsDebug()` switch |
| | `ComposeNewRxNewCropWebRequest` (`:325`) | Delete |
| `OpenDental/Main Modules/ControlChart.cs` | `Tool_eRx_Click` NewCrop branch (`:8487–8555`) | Delete the `RequestCustomerID` block, the checksum ID and the "fees… see our online manual" prompt. If not configured, show "NewCrop is not configured. See Setup > Program Links > eRx." and return. Replace the "eRx is currently disabled… see our online manual" text (`:8543`; the DoseSpot branch has the same text at `:8558`) and the "not available while using Open Dental Cloud" text (`:8490`) with neutral wording, or drop the cloud branch if Phase 3 removes cloud mode. |
| | provider gate (`:8852–8870`) | Local `providererx.IsEnabled` only; IDP block removed; distributor and "call support" texts removed |
| | `:8878–8881`, `:8922` | Redacted XML for the Introspection dialog and `erxlog` |
| | `:8908–8919` | Remove the hidden POST `else` branch. Also delete the commented-out IE launch block (`:8882–8899`), which still holds an `http://preproduction...` URL. |
| | `UpdateErxAccess` (`:9154`) | NewCrop: insert the missing row only, with no `GetErxAccess`, `UpdateServerAddress` check or date pref. The DoseSpot path follows `dosespot.md`; land the two together. |
| | `NewCropRefreshPrescriptions` (`:7851`) | Throttle (§3.4); drop its `IsEnabledByHq` check (`:7857`) |
| | `butErxAccess_Click` (`:383`) | Delete, along with `FormErxAccess` |
| | `:8422` `IsEnabledByHq` | Removed by Phase 1, if it isn't already |
| `OpenDental/Forms/FormErx.cs` | `ComposeNewRxNewCrop` | URL from config; dev tools off |
| `OpenDental/Forms/FormErxSetup.cs` (+ Designer) | NewCrop panel | Fields from §3.2 (password masked), Generate, Test connection, read-only "Previous account ID", provider enable grid. `butClearAccountId_Click`'s hard-coded password is replaced by a `SecurityAdmin` check, and the button now clears the new property. `radioDoseSpot_Click`'s "contact support to cancel NewCrop charges" text and `radioDoseSpotLegacy_Click`'s "contact support to sign up for eRx" text become neutral. `butSave_Click`'s `IsEnabledByHq` check goes (Phase 1). Keep the `ProgramProperties.Sync` caveat from §3.2 in mind. |
| `OpenDental/Forms/FormNewCropBilling.cs`, `OpenDental/Forms/FormErxAccess.cs`, `OpenDental/Main Modules/FormOpenDentalMenus.cs` (`_menuItemNewCropBilling`, `:44`, `:409–410`), `OpenDental/Main Modules/FormOpenDental.cs:1759`, `:6652` | | Delete the HQ and distributor tools (Phase 3) |
| `OpenDentBusiness/Data Interface/OIDInternals.cs` | `CustomerPatNum` | Delete |
| | *new* `GenerateUuidRoot()`, `IsValidOid(string)`, `ApplyRoot(string root,bool overwriteChildren)` | |
| | `OpenDentalOID` | Rename to `LegacyOidArc` (DoseSpot prefix match) |
| `OpenDental/Forms/FormOIDRegistryInternal.cs` (+ Designer) | `butRetrieveOIDs_Click` (`:86`), the load handler's label text (`FormReminders_Load`, `:30`), the `:136` status text and the `:148` prompt | Generate / Enter root (§3.5) and the legacy-root note |
| `OpenDentBusiness/WebBridges/Erx/DoseSpot.cs`, `DoseSpotV2.cs` | `GetDoseSpotRoot` | Per `dosespot.md` (local suffix) |
| `OpenDentBusiness/WebServices/CustomerUpdatesProxy.cs` | `ICustomerUpdates.RequestCustomerID` | Remove from the interface and the NoHq stub once no callers remain |
| `OpenDentBusiness/TableTypes/Pref.cs` | `NewCrop*` summaries | Mark as legacy, read only for the one-time import |
| `OpenDentBusiness/Properties/Settings.Designer.cs:148` | `OpenDentBusiness_NewCrop_Update1` | `http` → `https` (both `app.config` files are already `https`) |
| Entry point | `FormEhrSetup.cs:159`, `FormEhrQualityMeasures2014.cs:197` | If Phase 3 deletes the EHR forms, move the OID registry button to HL7 setup, because HL7 still uses the OIDs |
| `UnitTests/UnitTests/ErxTests.cs` | | Tests in §6 |

### 4.2 Bridges.dll types to re-implement

**None.** NewCrop never touches Bridges.dll. The generated `NewCropWebService` proxy and `NCScript.cs` stay in-repo. Regenerate them only from the WSDL and XSD that Ensora supplies under the agreement, and only if Ensora says the versions in use are retired.

### 4.3 "Not available" stubs this replaces

| Stub | Reached from | Replaced by |
|---|---|---|
| `CustomerUpdatesNoHq.RequestCustomerID` | `ControlChart.Tool_eRx_Click` (NewCrop first use) | `NewCropConfig` account ID (entered or generated) |
| `CustomerUpdatesNoHq.RequestCustomerID` | `FormOIDRegistryInternal.butRetrieveOIDs_Click` | `OIDInternals.GenerateUuidRoot` / entered root |
| `CustomerUpdatesNoHq.RequestCustomerID` | `OIDInternals.CustomerPatNum` ← `DoseSpot(V2).GetDoseSpotRoot` | local suffix (per `dosespot.md`) |
| `CustomerUpdatesNoHq.GetErxAccess` (NewCrop path) | `ControlChart.UpdateErxAccess` | local `providererx` enable list |
| (not a stub) compiled-in Open Dental NewCrop credentials | `NewCrop.cs` | office-configured, encrypted properties |

## 5. Credentials the office must register

### 5.1 NewCrop / Ensora (partner-only)

1. **Decide whether NewCrop is needed at all.** Run the queries in §6, step 1. If the office doesn't use NewCrop (Open Dental points new eRx practices at DoseSpot), stop here: ship the credential removal and the OID work, and leave NewCrop unconfigured.
2. **Pick the contracting route:**
   - an **authorized Ensora reseller** willing to issue partner credentials for this software (the model OpenEMR uses; [OpenEMR ePrescribe wiki](https://www.open-emr.org/wiki/index.php/OpenEMR_ePrescribe)). OpenEMR's resellers built and certified an interface for OpenEMR specifically, so a reseller would probably have to treat this software as a new integration; or
   - **Ensora directly**, as an EHR partner. There is no public partner sign-up; the [Ensora eRx features page](https://ensorahealth.com/product/erx/features) lists only general sales and support phone numbers.
3. **Ask Ensora or the reseller for:**
   - the partner name, account name and password for **preproduction**, and later for production
   - the current NCScript and `Update1` documentation, and confirmation that interface v7 and `GetPatientFullMedicationHistory6` are still supported
   - the account ID and site ID rules: will they assign the ID, or accept ours? Length and characters?
   - acceptance of our **product name** and any validation or certification steps (test scenarios, Surescripts certification, and whether EPCS needs a separate certification for our product; see §2.6)
   - the drug database choice (FDB or LexiData) on the account
   - per-prescriber and EPCS pricing
   - whether the office's existing account (the `NewCropAccountId` pref, under Open Dental's partner) and its prescription history can be **moved** to the new partner
4. **Sign the BAA** with Ensora, or with the reseller plus a downstream BAA ([HHS guidance](https://www.hhs.gov/hipaa/for-professionals/privacy/guidance/business-associates/index.html)).
5. In **eRx Setup**: set Environment = Preproduction, enter the triplet and the account ID, press **Test connection**, and enable one test provider.
6. Complete Ensora's validation in preproduction (§6). Then switch to the production credentials and Environment = Production.
7. **Prescribers** are subscribed by the organization administrator. They then register and complete identity proofing (Exostar, per Open Dental's manual) and, for controlled substances, the EPCS set-up with at least two EPCS administrators ([Ensora eRx quick start](https://experience.ensorahealth.com/get-started-49/eprescribe-quick-start-2516); [NewCrop eRx FAQ](https://opendental.com/manual/newcropfaq.html)).
8. **Cut over:** once the new account works, cancel the Open Dental-billed NewCrop service with Open Dental, so the office isn't billed twice.

### 5.2 OID root

1. If `oidinternal` already holds a root (§6, step 1), decide whether to keep it for continuity or replace it (§7). If you keep it, there's nothing more to do.
2. Otherwise either:
   - press **Generate root** (no registration, works offline), or
   - request a free **IANA PEN** through the [IANA application form](https://www.iana.org/assignments/enterprise-numbers/assignment/apply). Answer IANA's confirmation e-mail; IANA says the number normally follows within seven days of that reply. Then enter `1.3.6.1.4.1.<PEN>.1` as the root.
3. Tell any HL7 or CCD trading partner (labs, HIEs) about the new root before the first message goes out.

## 6. Test plan

**Step 1, discovery on a restored copy of `fox-db` (read-only).** Don't select password values.

```sql
SELECT PrefName, IF(PrefName='NewCropPassword', LENGTH(ValueString), ValueString) AS v
  FROM preference WHERE PrefName LIKE 'NewCrop%';
SELECT pp.PropertyDesc, pp.PropertyValue FROM programproperty pp
  JOIN program p ON p.ProgramNum=pp.ProgramNum
  WHERE p.ProgName='eRx' AND pp.PropertyDesc='eRx Option';
SELECT p.Enabled FROM program p WHERE p.ProgName='eRx';
SELECT COUNT(*), SUM(IsEnabled=1) FROM providererx WHERE ErxType=0;
SELECT COUNT(*), MAX(RxDate) FROM rxpat
  WHERE ErxGuid REGEXP '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$';
SELECT IDType, IDRoot FROM oidinternal;
SELECT COUNT(*) FROM oidexternal WHERE rootExternal LIKE '2.16.840.1.113883.3.4337.%';
```

**Unit tests** (`ErxTests`, new `OIDInternalsTests`):

- `BuildNewCropClickThroughXml` takes the partner name, account name, password, account ID and site ID from the properties. A fake config makes this independent of the build configuration.
- An undecryptable `osd1$` password (corrupt the ciphertext) makes `IsConfigured()` false, and nothing is built or sent. This catches a read path that uses `TryDecrypt` by mistake.
- A failed `Encrypt` on save leaves the old value in place and shows an error. Plain text is never written to the password row.
- `RedactNewCropCredentials` leaves no password in the output. The `erxlog` insert path gets redacted text.
- No test reaches `CustomerUpdatesProxy`: a fake that throws if called is installed for every eRx and OID test.
- `GenerateUuidRoot` matches `^2\.25\.[1-9][0-9]{0,38}$`, and two calls differ. `IsValidOid` accepts `1.3.6.1.4.1.99999.1` and rejects leading zeros, an empty string and a trailing dot. `ApplyRoot(false)` keeps custom children.
- `ImportLegacyPrefs` moves reseller prefs into properties, encrypts the password and blanks the pref. It runs only once.
- A grep test: no `newcropaccounts.com` URL string in code outside `NewCropConfig`, and no `MiscUtils.Decrypt("` in `NewCrop.cs`. The test must allow the XML and SOAP namespace values in `NCScript.cs` and `Web References/NewCropWebService/` (`Reference.cs`, `.wsdl`, `.disco`, `.map`), plus the web reference default in `Settings.Designer.cs` and the two `app.config` files. Those are identifiers or overwritten defaults, not calls.

**Manual checks, preproduction** (Ensora's sandbox credentials, a test database):

1. Fresh database: eRx disabled, no NewCrop button behaviour, and the Chart makes no NewCrop calls (watch with a proxy or the firewall log).
2. Enable NewCrop with no credentials: the neutral "not configured" message.
3. Enter the preproduction triplet: Test connection OK. A wrong password gives a clear failure, and nothing is logged in plain text.
4. A provider not enabled is blocked locally, with no request to NewCrop.
5. An enabled provider opens compose. Write a test prescription to a test pharmacy, close the window: `rxpat` and `medicationpat` rows appear, and the RxCreate automation fires.
6. Staff user on behalf of a provider; clinics on, with the location equal to the ClinicNum.
7. Inspect `erxlog.MsgText`: the credentials are redacted. WebView2 dev tools can't be opened.
8. Network cable out: the Chart loads, manual Rx works, and the eRx button shows an unreachable message.
9. Throttle: switching between patients repeatedly makes at most one history call per patient per 10 minutes.
10. Release build with `*.opendental.com`, `opendentalsoft.com` and `patientviewer.com` blocked: no errors.

**OID manual checks:**

1. Blank `oidinternal`: Generate root fills root and children. Send an HL7 ADT or SIU and a CCD, and check that the new root appears.
2. Legacy root present: the note shows, nothing changes without confirmation, and the overwrite prompt protects custom children.
3. The DoseSpot root on an empty `oidexternal` is created without any HQ call (`dosespot.md` test).

## 7. Size, risks and open questions

**Size.**

| Piece | Estimate |
|---|---|
| OID root (generate or enter, validation, delete `CustomerPatNum`, form text) | S, about 1 day |
| Credential removal plus `NewCropConfig` plus Setup panel plus local gate plus redaction plus throttle | M, 3 to 5 days |
| Preproduction validation with Ensora | 1 to 2 days of work; calendar time set by Ensora |
| **Total** | **M** |

**Risks.**

- **No partner, no NewCrop.** Without a reseller or partner agreement, NewCrop cannot work at all. The design leaves it cleanly dormant.
- **Prescription continuity.** The office's existing NewCrop account and history live under Open Dental's partner. A new partner means a new account unless Ensora moves it. Until then, old prescriptions stay in `rxpat` but don't sync.
- **Password exposure.** It is in the browser post by NewCrop's design, and at rest it is only obfuscated (`osd1$`, with a key that is in this repo). Dev tools off and log redaction reduce the exposure but can't remove it, so use credentials scoped to the office or reseller, never a shared multi-office secret.
- **Old credentials in history.** Open Dental's encrypted credentials stay in git history even after deletion. They're readable with the in-repo key, so treat them as public. Never use them, and tell Open Dental only if the owner wants to.
- **Old `erxlog` rows** already contain Open Dental's partner password in plain text inside the stored XML. Decide whether to purge or redact them (it's a data update).
- **Interface age.** v7 and `GetPatientFullMedicationHistory6` may be deprecated without public notice. Confirm before investing in more than this spec.
- **Changing the OID root** changes identifiers already held by HL7 and CCD trading partners. A `2.25` root is long (up to 46 characters with a sub-arc), and some receiving systems may cap OID length. That's our own caution, not a published ITU statement: the OID repository says registration of UUID OIDs is optional and encouraged only because of collision risk. Prefer a PEN if a partner rejects the UUID root.
- **EHR form removal (Phase 3)** could orphan the OID registry UI unless it's moved to HL7 setup.

**Open questions.**

1. Does the office use NewCrop today (§6, step 1)? If not, should the NewCrop option be hidden or deleted, keeping only DoseSpot?
2. Who holds the NewCrop agreement: the office, a reseller, or a project organisation? Will Ensora accept a GPL product under a new product name?
3. Who assigns the account ID, and what are its length and character limits? Will Ensora carry over the existing Open Dental-era account and history?
4. Is interface v7 still current, and does `GetAccountStatusV2` work as a credential check?
5. Should `providererx.IsIdentifyProofed` be filled from `LicensedPrescriberStatusV2`, or left to NewCrop?
6. OID: keep an existing `2.16.840.1.113883.3.4337.1486.<n>` root that Open Dental assigned to this office, or move to a UUID or PEN root? Recommendation: keep it for migrated offices until trading partners are told, and use a generated root for new installs.
7. Purge or redact the historic `erxlog.MsgText` that holds Open Dental's credentials?

**Sources used:**

- Code in this repo (paths above)
- `D:/Dev/opendental-schema/hq-vendor-table.md`
- `docs/decoupling-plan.md`
- `docs/specs/vendor-direct/dosespot.md`
- the vendor and registry pages cited inline

## Fact-check log (2026-10-18)

The vendor pages, the public `Update1.asmx` help pages on both hosts, and every code reference were checked again against `main` at `6855026f5`. **Feasibility stays partner-only.** What changed:

- **Vendor facts.**
  - Added Ensora's own onboarding guide: the EMR must already be integrated, then an organization administrator subscribes prescribers, who register with or without EPCS.
  - The Ensora product page does mention EHR partners and integration, in marketing terms only. Ensora's healthcare IT vendor page returned 404.
  - Added the full `GetAccountStatusV2` response shape and the `Status` values from the public schema (`Unknown`, `OK`, `Fail`, `NotFound`).
  - Recorded that 110 operations are listed on both hosts, including `LicensedPrescriberStatusV2` and `RegisterLicensedPrescriberV2`, and that the in-repo proxy already has `GetAccountStatusV2`, so "Test connection" needs no regeneration.
  - Corrected the OpenEMR access-model wording to what the sources say, and added that OpenEMR's vendor holds its own DEA EPCS certification, which raises an EPCS certification question for us.
  - Added what the VistA write-up says the certification path was. It is a 2015 community page, not vendor documentation.
  - The IDP provider (Exostar) is now credited to Open Dental's manual, since Ensora's guide doesn't name it.
- **Removed an unsupported claim.** "Open Dental has moved its offices to DoseSpot" is not what the cited page says. Open Dental points practices new to eRx at DoseSpot and lets existing Ensora users stay or switch (now cited).
- **OID facts.**
  - The IANA PEN form has moved to `iana.org/assignments/enterprise-numbers/assignment/apply`. It states registration is free and an assignment within seven days of answering the confirmation e-mail. The "60 days / 30 days" figures were removed, as was "reviewed by hand".
  - The risk note blamed ITU's encouragement to register `2.25` OIDs on long-OID handling. The sources tie it to collision risk, so the note now says that, and the length caution is flagged as ours.
  - Added the ITU warning about weak randomness: use a cryptographic RNG.
- **Encryption.** The read rule said to use `CDT.Class1.TryDecrypt` and treat failure as "not configured". `TryDecrypt` returns the ciphertext on failure, so failure can't be detected that way. The rules now use `Decrypt`/`Encrypt` with return-value checks, matching `dosespot.md`. The password row gets `IsMasked`/`IsHighSecurity`, and the spec notes that `osd1$` is obfuscation only.
- **Code touchpoints.** All files and members exist. Corrections:
  - Line numbers: `UpdateErxAccess` `:9154`; provider gate `:8852–8870`; Introspection dialog `:8878–8881`; `erxlog` write `:8922`; hidden POST `else` `:8908–8919`; `butRetrieveOIDs_Click` `:86`; `CustomerPatNum` `:19`; NewCrop branch end `:8555`.
  - `FormOpenDentalMenus.cs` and `FormOpenDental.cs` are in `OpenDental/Main Modules/`, not `Forms/`.
  - Only `Settings.Designer.cs:148` has the `http` default. Both `app.config` files already use `https`.
  - Added the touchpoints the spec had missed:
    - `IsEnabledByHq` in `NewCropRefreshPrescriptions` (`:7857`) and in `FormErxSetup.butSave_Click`
    - the `EnabledWithLegacy` per-provider switch for "DoseSpot with NewCrop" (`ControlChart.cs:8445`, `FormProvEdit`)
    - the `ProgramProperties.Sync` delete-missing behaviour in `FormErxSetup`
    - the Debug temp-file dump of medication XML (PHI)
    - the commented-out IE block with an `http` URL
    - non-neutral UI texts: the "Open Dental Cloud" message, "see our online manual", and `radioDoseSpotLegacy_Click`'s "contact support"
  - The grep test would have failed on the `newcropaccounts` XML and SOAP namespaces in `NCScript.cs` and the web reference. It now allows them.
- **Bridges.dll.** Confirmed: no NewCrop, eRx or OID file references Bridges. The SOAP proxy is the in-repo `Web References/NewCropWebService`, and the click-through schema classes are `NCScript.cs`.
- **Credentials, hosts and UI.**
  - The spec compiles in no credentials and quotes no secret values.
  - The only runtime hosts are `secure.newcropaccounts.com` and `preproduction.newcropaccounts.com`. Open Dental URLs appear only as citations of its public manual.
  - The proposed UI texts are vendor-neutral.
- **Wording.** Rewrote the paraphrased source passages in our own words. No source text is quoted beyond short UI strings from this repo.
