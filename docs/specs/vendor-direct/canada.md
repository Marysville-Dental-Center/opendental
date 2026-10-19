# Canadian claim networks (ITRANS carrier list, CCDWS network certificates): direct-to-vendor spec

Status: draft, 2026-10-18; fact-checked the same day (see "Fact-check log" at the end). Baseline: `main` at `6855026f5`. Vendors: the **Canadian Dental Association (CDA)**, which runs CDAnet and ITRANS 2.0, plus the claim **networks**: TELUS Health (TELUS Group B; CDA says Group A is inactive), Alberta Blue Cross (ABC) and instream. The ITRANS 2.0 back-end hosts in the default `ICD.ini` (ITS, digital-ID and restricted-list services) are on `continovation.com`, so CDA appears to use Continovation Services as its operator; CDA's documents don't say so in as many words. CDA's ICD page describes the CCDWS communication agent as software from the Canadian Life and Health Insurance Association (CLHIA), distributed by Harder Software.

**ITRANS v1 is gone.** CDA retired the original ITRANS (the ICA agent) on June 30, 2026 ([ITRANS updates](https://www.cda-adc.ca/en/services/cdanet/itrans_updates.asp)). Since then only ITRANS 2.0 (the ICD) carries dentist claims, so the legacy `EclaimsCommBridge.ITRANS` setting can no longer send anything.

**Priority: LOW, pending confirmation.** Nothing in the repo or the docs says the office is in Canada, and the hints point the other way:
- the other vendor-direct specs cover US-only services (USPS, US state PDMPs, 10DLC texting, HIPAA BAAs)
- `fox-db` is the office's database, but nobody has checked whether it holds Canadian carriers or a Canadian clearinghouse. A quick check: `SELECT COUNT(*) FROM carrier WHERE IsCDA=1` and the `clearinghouse.Eformat` values.
- the Canadian code is gated by the Windows culture. The hourly carrier thread starts on any workstation whose culture name ends in `CA`, whatever the clearinghouse. The **Update Carriers** box in `FormCarriers` also needs the default clearinghouse to use the Canadian format, and claim sending needs a Canadian clearinghouse.

If the office is US-only, do only the small clean-up in section 4.1 (it is safe and removes dead HQ paths) and park the rest. Section 7 lists what to ask the owner.

## Summary

- **Carrier list (N-CPL).** This is a **vendor feed, not reference data**, so it stays. Only the **HQ relay is removed**.
  - CDA publishes the Network Claims Processor List (N-CPL) to certified software vendors through CDA's own ITRANS Claims Director (ICD), which the office installs locally.
  - ICD downloads the list itself, refreshes it after every transaction, and writes `n-cpl.json` to a local folder. It also serves the list over a local REST call (`GET /ICD/GetNCPL/` on `https://localhost:9660`).
  - Our code already supports the local ICD path (`ITRANS Claims Director.exe --getncpl`). The HQ branch only existed for offices without ICD, and it simply relayed CDA's file.
  - Direct design: read the list from the local ICD (file first, then local API, then the `--getncpl` command). Never fetch it from any internet host ourselves.
- **Network certificates.** **Partner-only. Nothing to download.**
  - The `.pem` files HQ handed out (`OPENDENTAL.pem`, `OD_2023-02-20_2028-02-24_prod.pem`, and the staging copy) are **Open Dental Software's vendor certificates**. The CCDWS agent uses them to identify the *software vendor* to Alberta Blue Cross and TELUS.
  - No public API or self-serve portal issues these certificates. A software publisher gets its own only by **certifying with each network** (and with CDA for CDAnet).
  - We must not reuse Open Dental's certificate: it names another company and its private key came from HQ.
- **Dentist claims don't need a vendor certificate at all any more.**
  - Since ICD v4, ITRANS sends dentist claims straight to the networks over mutual TLS, using each dentist's **CDA Digital ID**. The office gets that ID from CDA's Practice Support Services site, and ICD renews it automatically.
  - CCDWS, and so a vendor certificate, is only needed for **independent dental hygienists and denturists**.
  - Routing a hygienist or denturist claim through ICD doesn't remove that need. ICD spots a non-dentist provider number (prefixes `8` and `202` by default) and passes the claim to CCDWS, which still connects with the vendor certificate. Our code never sees that hand-off, so ICD and CCDWS report a missing certificate themselves.
  - Recommended path: route every Canadian claim through ITRANS 2.0 (ICD v4). Keep ClaimStream/CCDWS configurable, but block it until the office has a vendor certificate issued to *this* product.
- **Bigger blocker: CDAnet software certification.** CDA says claims go through practice software that CDA has certified, and its public list names certified vendors and products. Open Dental Software is on that list; this fork isn't, and the listing doesn't transfer. Before any Canadian office uses this fork for live claims, the project must get CDAnet certification, or the office must accept that risk with CDA. Hygienist claims add a second certification: CDHA tells hygienists they need "CCDWS certified" software for the TELUS and instream route ([CDHA claim forms](https://www.cdha.ca/claimforms)).
- **Feasibility: partner-only (confirmed)**, for live claims. The carrier-list part on its own is publicly documented and buildable now, because it only talks to the office's own ICD. **Size:** S for the code (about 2 days). Certification is external and open-ended (XL in calendar time, outside the code).

## 1. What the integration does, and what HQ provided

### 1.1 What the office uses it for

Canadian offices send CDAnet transactions to insurers electronically: claims, predeterminations, eligibility, reversals, outstanding-transaction requests and reconciliations. Each transaction is a fixed-format CDAnet message. This program writes it as `input.###` into a folder watched by a local **communication agent** and waits up to 120 seconds for `output.###` (`Canadian.PassToIca`, `OpenDentBusiness/Eclaims/Canadian.cs:1517`).

There are two agents, picked by `clearinghouse.CommBridge`:

| CommBridge | Agent | Who it serves | How it authenticates to the network |
|---|---|---|---|
| `ITRANS2` | CDA **ITRANS Claims Director** (ICD), a Windows service. v4.2 (June 2026) is current; CDA's vendor manuals are written for v4.1 | Dentists, all networks. Since v4, ICD hands non-dentist claims to CCDWS itself | Per-dentist **CDA Digital ID** (X.509 issued by `CanDentAssocCA`) from the Windows certificate store. ICD installs and renews them |
| `ITRANS` (legacy) | The ITRANS v1 **ICA** agent | Nobody: CDA retired ITRANS v1 on June 30, 2026 | Not applicable. Offices still set to `ITRANS` must move to `ITRANS2` |
| `Claimstream` | **CCDWS** (Common Communication Driver for Web Services), one sub-folder per network. Our code supports only `C:\ccd\abc`, `telusa`, `telusb` and `instream` (`Canadian.cs:1542-1557`). ICD's own `[Networks]` list also has `mbc` and `pbc` folders, which our ClaimStream path rejects as "not supported" | Hygienists and denturists; historically ABC | ABC and TELUS: a **software-vendor certificate** (`.pem` in the network sub-folder, named in `ccdws.ini`). instream: no vendor certificate. Our code expects the provider's own certificate to be installed (comment at `Canadian.cs:1575`). For hygienists, CDHA describes instream as a separate paid registration (about $180 a year) |

Two pieces of this used HQ.

**A. Carrier list import** (`OpenDentBusiness/Misc/ItransNCpl.cs`):
- `TryCarrierUpdate` loads CDA's N-CPL JSON. For every carrier whose BIN matches an `IsCDA` carrier, it updates the network, the CDAnet version and the supported-transaction flags (`CanadianSupportedTypes`).
- Depending on the `ItransImportFields` pref, it also fills in a blank phone or address, renames the carrier, or adds missing carriers.
- It runs in two places:
  - **hourly** on every workstation whose culture name ends in `CA`, with no clearinghouse check (`BeginCanadianItransCarrierThread`, `OpenDental/Main Modules/FormOpenDentalThreads.cs:156-170`). Only the ITRANS2 branch of `TryCarrierUpdate` limits automatic runs to the `WebServiceServerName` host (`ItransNCpl.cs:46`)
  - on demand from **Setup > Insurance > Carriers > Update Carriers** (`FormCarriers.butItransUpdateCarriers_Click`, `OpenDental/Forms/FormCarriers.cs:219`). That button sits in the `groupItrans` box ("Itrans Web Import"), which is shown only when the culture ends in `CA` and the default clearinghouse uses `ElectronicClaimFormat.Canadian` (`FormCarriers.cs:58-60`)
- Today's ITRANS2 branch runs `ITRANS Claims Director.exe --getncpl`, waits with no timeout, then reads `<ResponsePath>\n-cpl.json` (`ItransNCpl.cs:42-60`). It never reads `ICD.ini`.
- Each import is saved as an `etrans` row of type `ItransNcpl`. An automatic run skips the import when the text matches the newest such row; a manual run imports anyway.

**B. Vendor certificate provisioning** (`Canadian.PassToIca`, lines 1533-1657):
- For ClaimStream sends to ABC or TELUS A/B, it works out the network sub-folder and a hard-coded certificate file name. If the file is missing, it **downloads it from HQ** and writes it there.
- It then **rewrites `ccdws.ini`** so that older Open Dental certificate names point at the current ones.

### 1.2 What Open Dental's HQ provided

| Need | HQ call (now stubbed) | Request | Response | Stub today |
|---|---|---|---|---|
| N-CPL for offices **without** a usable local ITRANS2 install | `IWebServiceMainHQ.CanadaCarrierUpdate(officeData)` via `WebServiceMainHQProxy` (`ItransNCpl.cs:68`) | `PayloadHelper.CreatePayload("",eServiceCode.Undefined)`: registration key and office identity | The N-CPL JSON as a serialized string. HQ was relaying CDA's file | `WebServiceMainHQNoHq.CanadaCarrierUpdate` returns a "not available" result (`WebServiceMainHQNoHq.cs:81`). `ItransNCpl.cs:63-66` returns the vendor message first |
| ABC vendor certificate | `ICustomerUpdates.RequestCertCanadaABC(officeData)` (`Canadian.cs:1595`) | XML `<Request><RegistrationKey/></Request>` | XML with `<FileData64>` (base64 `.pem`), or `<Error>` / `<KeyDisabled>` | Throws `HqNotAvailableException` (`CustomerUpdatesProxy.cs:43`) |
| TELUS A/B vendor certificate (prod) | `RequestCertCanadaTelusAandB` (`Canadian.cs:1602`) | Registration key plus `RelativeFilePath=eclaim_certs/OD_2023-02-20_2028-02-24_prod.pem` | Same | Throws (`CustomerUpdatesProxy.cs:44`) |
| TELUS A/B vendor certificate (staging, Debug builds) | `RequestCertCanadaTelusAandBtest` (`Canadian.cs:1599`) | `…/OD_2023-02-05_2028-02-09_staging.pem` | Same | Throws (`CustomerUpdatesProxy.cs:45`) |

Side effects worth noting:
- The certificate call also toggled `PrefName.RegistrationKeyIsDisabled` based on HQ's `<KeyDisabled>` answer (`Canadian.cs:1612-1625`). It was a licence check riding on the certificate download.
- The `ccdws.ini` rewrite (`Canadian.cs:1641-1656`) silently pointed the office's CCDWS at Open Dental's current certificate names.

**No Bridges.dll types are involved.** All of this is plain code in `OpenDentBusiness` plus the two HQ interfaces, now served by in-repo stubs. Checked by grep: `Canadian.cs`, `ItransNCpl.cs` and `FormOpenDentalThreads.cs` reference nothing from Bridges. `FormCarriers.cs` does have `using OpenDental.Bridges;`, but only for the click-to-dial `DentalTek.PlaceCall` in `gridMain_CellClick` (line 215). `DentalTek` has no source in this repo, so it comes from the prebuilt Bridges assembly. It has nothing to do with the carrier import, and this spec leaves it alone.

## 2. The vendor's public interfaces

There is no OAuth API here. CDAnet is a message standard carried by locally installed agents. The "API" a practice management system uses is the **local ICD**: a folder interface plus a local HTTPS REST interface. CDA documents both publicly in two vendor PDFs:

- *ITRANS 2.0 Technical Reference for Dental Software Vendors*, revision 1.5, April 7, 2025, written for ICD v4.1: https://media.cda-adc.ca/sitefinity-blobs/docs/default-source/icd-software/itrans_2-0_vendor_technical_reference_1-5.pdf
- *ITRANS 2.0 Getting Started for Dental Software Vendors*, revision 1.5, April 7, 2025, written for ICD v4.1: https://media.cda-adc.ca/sitefinity-blobs/docs/default-source/icd-software/itrans_2-0_vendor_getting_started_1-5.pdf
- ICD downloads and doc index (ICD v4.2, released June 4, 2026, for Windows, Mac and Linux; error code list): https://www.cda-adc.ca/for-dental-professionals/programs-amp-services/cdanet-and-itrans/certified-vendors/icd-software
- No v4.2 edition of the manuals was published when this was checked. Re-check the `ICD.ini` keys below against a real v4.2 install.

### 2.1 N-CPL (carrier list)

**What it is.** A JSON list of every CDAnet carrier. For each carrier it gives:
- its claims processor and the network(s) that carry it (`network_folder` codes `ABC`, `INS`, `MBC`, `PBC`, `TGA`, `TGB`, `SIM`)
- the supported transaction types (`claim_01` … `attachment_19`, valued `Y`, `N`, `X` or `Z`)
- the supported CDAnet versions
- policy, division and certificate number formats
- COB instructions and mailing addresses
- `rot_bins`, the list of BINs to poll for outstanding transactions
- `change_log`

The schema is in Technical Reference Appendix A. Our `ItransNCpl` class already models it.

CDA explicitly offers N-CPL to PMS vendors to configure carriers automatically, and says it is committed to keeping it available long term (Getting Started section 5).

**Where it comes from.**
- ICD downloads it from `NCPLBaseUrl` (default `https://n-cpl.cda-adc.ca/api`). Technical Reference section 2.6 and Getting Started section 3.2 both say that download needs no authentication.
- ICD keeps a hash of the current list. Each time it reports a transaction to CDA's transaction store (ITS), the reply carries the current hash, and ICD refetches the list if the two differ (Technical Reference sections 2.5.5, 3.4 and 4.2).
- ICD saves the list to `NCPLFilePath`, default `C:\Program Files (x86)\CDA\ICD\n-cpl.json` (section 2.9.2). `ICD.ini` lives in the ICD installation folder (section 2.2).
- The CDA Practice Support tray app can also force a refresh (Technical Reference section 5.3).

**How a PMS reads it.** CDA documents three ways:

| Way | Detail | Source |
|---|---|---|
| Local file | Read `n-cpl.json` at `NCPLFilePath` from `ICD.ini` | Getting Started section 3.5.1; Technical Reference sections 2.5.5 and 2.9.2 |
| Force refresh by command line | `"…\ITRANS Claims Director.exe" --getncpl` while the ICD service runs; it replaces `n-cpl.json` | Technical Reference section 2.5.5; Getting Started section 4.1.1 |
| Local REST | `GET /ICD/GetNCPL/` on the ICD web interface (`[WebInterface] Port`, default **9660**, HTTPS). CDA says it downloads the latest list and returns it: `200` with `{carriers, rot_bins, change_log}`, or `404`, `408` or `500`. The manual doesn't say whether the call needs any client credential | Technical Reference Appendix D ("ICD Local Web API Specification 1.0"), section 6.2 |

**Notes:**
- The ICD web interface serves a certificate issued by `CanDentAssocCA` to "CDA Practice Support Services", not to `localhost`. CDA says the vendor's software has to override its own server-certificate check to trust it (section 2.9.5).
- The local REST call only works **on the machine that runs the ICD service**. Other workstations can't reach it on `localhost`. They have to read the file over the share in `ResponsePath`, as today's code does.
- Offices with **no dentists**, such as independent hygienists or denturists, send nothing to ITS, so ICD never sees a hash change. CDA's advice is that the vendor offers an "update carrier list" button or runs `--getncpl` periodically (Getting Started section 6).
- We **do not call `n-cpl.cda-adc.ca` directly.** Its paths are not documented for vendors, and CDA presents ICD as the supported channel.

**Rate limits.** None are published. ICD refreshes on its own, so the PMS only needs to read the local copy. We should run `--getncpl` (or `GET /ICD/GetNCPL/`) at most once a day automatically, plus on the manual button.

**Is it reference data?** It isn't. The decoupling plan ("Other downloads") puts the static lists that Open Dental's HQ curated and served, such as medication lists and Canadian fee schedules, under "make configurable or bundle locally". N-CPL is different:
- an **operational routing feed** published by the network operator (CDA)
- delivered by the office's own claims software
- something ICD itself depends on to route claims.

Only the HQ relay of that feed was HQ-specific, and that relay is removed.

### 2.2 Certificates and authentication

**CDA Digital IDs (dentists, via ICD).**
- An X.509 certificate and key per dentist, issued by CDA's CA (`CanDentAssocCA`).
- ICD uses them for mutual TLS with each network (Technical Reference sections 2.4.1 to 2.4.4).
- The dentist, or delegated staff, signs in to **CDA Practice Support Services** at https://services.cda-adc.ca and gets a one-time **secure code**. They paste it into the **CDA Practice Support** tray app, which installs the ID in the Windows store of the account the ICD service runs as. Sources: Getting Started sections 3.3 (the service account) and 4.1.2 (the secure-code steps); Technical Reference sections 2.4.2 and 5.2.
- ICD checks expiry at startup and every 24 hours, and renews IDs that fall inside the `CertificatePreRenewalDays` window (Technical Reference sections 2.4.4 and 2.5.7). The documents disagree on the default: 30 or 60 days. It is ICD's setting, not ours.
- **The PMS never touches these IDs.** There is nothing for us to store.

**Vendor certificates (non-dentists, via CCDWS).**
- For hygienist and denturist claims, ICD passes the message to CCDWS. CCDWS then authenticates to the network with the software vendor's certificate instead of a CDA Digital ID (Getting Started section 6).
- Technical Reference section 2.8 says those provider types rely on a separate certificate scheme kept in files, and refers readers to the CCDWS manual. Their claims also don't reach ITS, so CDA's help desk can't see them.
- Downloads and docs (installer and `ccdws-latest-doc.zip`, CCDWS 2.0.3.x, January 2024): https://ccdws.hardersoft.com/, linked from CDA's ICD page. The CCDWS manual is public; only the network certificates are not.
- In a May 2021 bulletin to dental hygienists, Alberta Blue Cross says the software vendor must be certified with ABC before the office can send claims over the internet to ABC's dental web services ([Dental Benefact 123](https://www.ab.bluecross.ca/pdfs/Dental-benefact-123-may-2021.pdf)). It names no vendor process; its Dental Provider Contact Centre is 1-800-567-8104.
- TELUS Health's insurer-side claims-exchange page says only that the dental software vendors in the Canadian market are certified and supported by TELUS Health. It has no vendor onboarding document. The page returned HTTP 403 to an automated fetch on 2026-10-18, so that summary comes from the search index: https://www.telus.com/en/health/organizations/group-health-benefits/insurers/claims-exchange
- **Publicly known:** the certificate belongs to the software vendor, is held per network, and is issued after that network certifies the vendor.
- **Not public:** how to apply, test criteria, cost, certificate format and lifetime, and whether the network allows one vendor certificate to be used across many offices. Getting it needs direct contact with each network and possibly NDA documents.

**instream.** No vendor certificate is involved. The code comment at `Canadian.cs:1575-1576` expects the provider's own certificate. `CanadianNetworks.GetNetwork` (`OpenDentBusiness/Data Interface/CanadianNetworks.cs:91-96`) sends ClaimStream hygienist and denturist claims for instream carriers to TELUS B instead, and TELUS B does need the vendor certificate. No change.

**TELUS Group A.** CDA notes the TELUS A network is inactive (Technical Reference section 3.3.4). CDA publishes no per-network carrier counts. Check the real `n-cpl.json` before assuming which network carries what.

### 2.3 Terms that matter

- **CDA-certified software.** CDA's CDAnet page says offices submit claims from practice software that CDA has certified ([CDAnet and ITRANS](https://www.cda-adc.ca/for-dental-professionals/programs-amp-services/cdanet-and-itrans); [certified vendors list](https://www.cda-adc.ca/for-dental-professionals/programs-amp-services/cdanet-and-itrans/certified-vendors)).
  - The list says its products passed CDAnet certification for the v2 and v4 message standards. CDA vouches for nothing beyond electronic claim sending.
  - **This fork isn't certified.** "Open Dental Software" is listed; that listing doesn't transfer to the fork.
  - The certified-vendors page gives **no** certification contact or process. It only links CDA's general contact page.
  - Contacts in CDA's vendor documents: `cdanet@cda-adc.ca` (credentials for the vendor test-certificate page, Getting Started section 4.1.2), the document contact in Technical Reference section 1.4 (at Continovation), and the Practice Support Services help desk at 1-866-788-1212 / `pss@cda-adc.ca`.
- **Dentist eligibility.** The service is for dentists licensed and in good standing with their provincial or territorial dental association, or CDA Affiliate Members in Quebec. Members pay no extra fee.
  - The dentist subscribes through PSS by accepting the CDAnet/CDA ITRANS Claims **Service Schedule**, with a separate schedule for each practice location ([Service Schedule](https://cda-adc.ca/en/services/agreements/cdanet.asp)).
  - Getting Started section 3.5.1 says the ICD licence is part of that dentist subscription. The ICD needs no separate agreement with the PMS vendor.
- **Network vendor certification.** Needed for ABC and TELUS through CCDWS. It is a partner relationship; see 2.2.
- **Privacy.**
  - Canadian claims fall under PIPEDA and provincial health-privacy laws, not HIPAA, so there is no BAA concept.
  - ICD sends **de-identified** copies of each transaction to CDA's ITS (Technical Reference section 4).
  - The file method leaves unencrypted claim files on disk briefly. CDA recommends the API method partly for that reason (Getting Started sections 2 and 3).

## 3. Desktop design

### 3.1 Auth flow

**There is no user-facing auth flow in our app.** No OAuth, PKCE, device code or client credentials apply:

- **Network authentication** is mutual TLS done by the agents:
  - ICD uses CDA Digital IDs, installed and renewed by CDA's own tray app and service.
  - CCDWS uses the vendor certificate named in `ccdws.ini`.
- **The local ICD REST call** (`GET /ICD/GetNCPL/`, `https://localhost:<port>`) needs no credentials, but it does need TLS validation:
  - Use a dedicated `HttpClientHandler` with `ServerCertificateCustomValidationCallback` (available on .NET Framework 4.7.1 and later).
  - Accept the certificate only when **all** of these hold:
    - the host is `localhost` or `127.0.0.1`
    - the issuer CN equals the `[WebInterface] CertificateIssuedBy` value from `ICD.ini` (default `CanDentAssocCA`)
    - the subject CN equals `CertificateIssuedTo` (default `CDA Practice Support Services`)
    - the only chain errors are a name mismatch, or an untrusted root when the CDA root is missing from the PMS user's store (ICD installs CDA's certificates under the **service** account). Treat any other chain error, such as an expired certificate, as a failure.
  - Matching names alone is weak: any local process could present a certificate with those names. That is acceptable for `GetNCPL`, because the carrier list is public and the result is only parsed as data. Any later `SendPayload` work, which carries PHI, needs a stronger pin, such as the thumbprint of the ICD certificate in the Windows store.
  - Never set the global `ServicePointManager.ServerCertificateValidationCallback`.
  - The call only works on the ICD host (see 2.1).

### 3.2 Where configuration lives

**No new secrets and no new stored settings are needed.** Everything is derived from rows and files the office already maintains:

| Setting | Where it lives | Notes |
|---|---|---|
| Which agent, and the claim folder | `clearinghouse.CommBridge`, `ExportPath` (existing) | Unchanged |
| ICD install folder | `clearinghouse.ResponsePath` (existing; already used to find `ITRANS Claims Director.exe`) | Unchanged |
| N-CPL file path, ICD web port, ICD certificate names | **Read from `ICD.ini`** in the ICD install folder: `[ICD] NCPLFilePath`, `[WebInterface] Port`, `CertificateIssuedBy`, `CertificateIssuedTo` | Fall back to the documented defaults when a key is missing. `NCPLFilePath` is a path **on the ICD host**. On any other workstation, or when `ResponsePath` is a UNC share, map it: if it points into the ICD install folder, use `<ResponsePath>\<file name>` (today's behaviour); otherwise fail with a clear message instead of reading a local path that doesn't exist |
| Which carrier fields to import | `PrefName.ItransImportFields` (existing) | Unchanged |
| Vendor certificate file for each CCDWS network | **Read from `ccdws.ini`** (already located at `<ExportPath parent>\ccdws.ini`, `Canadian.cs:1540`) | CCDWS owns this file. We only read it to check the referenced `.pem` exists, and **never rewrite it** |
| Vendor certificate private key or passphrase | CCDWS's own files, protected by NTFS ACLs on the claim server | We never read, copy or store it. Nothing goes in the database, and it is not encrypted by us because CCDWS has to read it as-is |

There is no `program` row for Canadian claims, and none is needed. If the owner later wants per-office overrides (for example, turning off the automatic carrier refresh), add them as `programproperty` rows under a runtime-inserted program row, following the pattern in the other vendor-direct specs. They aren't part of this spec. CDA Digital IDs live in the Windows certificate store, managed by ICD, and **must never** be copied into the database.

### 3.3 Carrier-list flow

`ItransNCpl.TryCarrierUpdate(isAutomatic, fields)` becomes:

1. Get the default dental clearinghouse.
   - If it isn't `ITRANS2`, or `ResponsePath` is empty: return a plain message, "Carrier list import needs the ITRANS Claims Director (ITRANS 2.0) installed and selected in Setup > Clearinghouses."
   - Don't mention HQ, and don't suggest a vendor.
2. Read `ICD.ini` from `ResponsePath` to get `NCPLFilePath` and `[WebInterface] Port`.
3. **Automatic run** (background thread):
   - Read `n-cpl.json` from the path in 3.2. Take the timestamp from `File.GetLastWriteTime`. The current code uses `GetCreationTime`, which doesn't change when a file is overwritten in place, and Windows file-name tunnelling can keep it even when the file is deleted and recreated.
   - If that write time is more than 24 hours old, first force a refresh (step 4), so that dentist-free offices still get updates. CDA says `--getncpl` replaces the file every time (Getting Started section 4.1.1), so the file time records the last refresh with no new stored state. Don't use the newest `etrans` for this: an automatic run adds no `etrans` row when the list is unchanged, so it would force a refresh every hour.
   - Keep the existing rule that the automatic run only happens on the `WebServiceServerName` host. The design assumes the ICD service runs on that host; if it doesn't, the forced refresh fails and is logged.
4. **Forced refresh** (manual button, or the daily automatic run):
   - (a) Only on the ICD host: `GET https://localhost:<port>/ICD/GetNCPL/` with a 60-second timeout. Use the JSON body directly.
   - (b) If that fails because there is no listener, the port is closed, the TLS check fails, or the status isn't 200: run `ITRANS Claims Director.exe --getncpl` as today, but with `WaitForExit(120000)`, then re-read the file. Today's `WaitForExit()` has no timeout and can hang the thread. CDA documents `--getncpl` only for the ICD machine with the service running, so on another workstation skip (a) and (b) and just read the shared file.
5. Parse, compare with the last `etrans` text (unchanged = skip when automatic), store the new `etrans`, then call `UpdateCarriersFromJsonList`. That method is unchanged apart from the network mapping below.
6. **Manual "Import from file…"** (new, on `FormCarriers`):
   - Lets an office without a local ICD (for example a ClaimStream-only workstation) pick an `n-cpl.json` copied from a machine that has ICD.
   - It only reads a local file and makes no network call.
   - It goes through the same parse, store and update path with `isAutomatic=false`.

Network mapping (`TrySetCanadianNetworkNum`):
- Today the switch maps only `ABC`, `TGA`, `TGB` and `INS` (`ItransNCpl.cs:247-259`).
- Add `MBC` → `MBC` (Manitoba Blue Cross) and `PBC` → `PBC` (Pacific Blue Cross). Both are network codes in the Technical Reference (sections 2.9.6 and 2.9.7). Both already exist as `canadiannetwork` rows: first inserted in `ConvertDatabases1.cs:3449-3450`, then re-seeded with transaction prefixes in `ConvertDatabases2.cs:4914-4916`. `DatabaseMaintenances.cs:1704` also looks up `MBC`. No schema or data migration is needed.
- `SIM` (CDA's messaging simulator) needs no row. It should stay unmapped and be logged.
- This mapping is **not** cosmetic. The carrier's network supplies the transaction prefix written into message field A01 (`CanadianTransactionPrefix`, `Canadian.cs:202-211`, and the same pattern throughout `CanadianOutput.cs`). With ITRANS2, ICD picks the route from the BIN and the N-CPL (Technical Reference section 2.5.3), but the prefix still goes out in the message. So test an `MBC` or `PBC` carrier end to end (section 6) before shipping.
- With ClaimStream, an `MBC` or `PBC` carrier gets the existing "Transaction not supported for network" error (`Canadian.cs:1554-1557`) instead of a wrong network.
- In the `default` branch, replace `BugSubmissions.SubmitException` with a local `Logger` line. Since the telemetry removal, that method only writes to the local log anyway (`BugSubmissions.cs:397-418`).

### 3.4 Certificate flow (ClaimStream / CCDWS)

Replace `Canadian.cs:1563-1656` (download, write and `ccdws.ini` rewrite) with a **read-only check**, `CanadianVendorCert.Validate(network, saveFolder, configFilePath, out string errorMsg)`. It runs before any `input.###` is written, so claim data never lands on disk when it can't be sent:

1. **instream (`CSI`):** no vendor certificate is needed, so pass. This is today's behaviour.
2. **ABC or TELUS A/B:**
   - If `ccdws.ini` is missing, fail with "CCDWS is not configured for network X".
   - Find the `.pem` file names referenced in the section or keys for that network. The exact key names come from the CCDWS manual (`ccdws-latest-doc.zip`, see section 7). Until confirmed, fall back to any `*.pem` token in the file that also exists in the network sub-folder.
   - If none is found, or the file is missing, fail with: "No software-vendor certificate is installed for network X. Claims for hygienists and denturists sent through CCDWS need a vendor certificate issued by that network to the publisher of this software. Dentist claims can be sent through ITRANS 2.0 instead."
   - If the referenced file is one of **Open Dental's** certificates, **fail** with "This certificate belongs to another software vendor and can't be used by this program." Open Dental's files are named `OPENDENTAL.pem`, `OD_*.pem` (matched as `^OD_\d{4}-\d{2}-\d{2}_\d{4}-\d{2}-\d{2}_(prod|staging)\.pem$`), and the template placeholder `<vendor_prod_cert_filename>.pem`. See the open question in section 7; the recommendation is to block.
3. **Never** download, create or edit certificate files, and never edit `ccdws.ini`.
4. Remove the `RegistrationKeyIsDisabled` toggling. Registration keys are gone (decoupling plan, "Registration key and license").
5. Remove the `ODBuild.IsDebug()` switch between Open Dental's staging and production file names. Choosing staging or production becomes the office's CCDWS configuration.

### 3.5 Errors and offline behaviour

- **ICD service stopped or not installed:**
  - The local file read still works if the file exists.
  - A forced refresh fails with "The ITRANS Claims Director service isn't responding. Start the service and try again."
  - The automatic thread swallows the error and logs it, as today.
- **CDA unreachable (office offline):** ICD keeps serving the last `n-cpl.json`. Our import is unaffected and works on the last good copy. Claim sending fails inside ICD with its documented error codes; `Canadian.cs` already reports ICD and CCDWS errors.
- **Malformed JSON:** keep "Failed to import json." Don't overwrite carriers.
- **Vendor certificate missing:** a clear message before any file is written (3.4). The claim stays unsent, and the office can print it or send it through ITRANS if the provider is a dentist.
- **No Open Dental hosts and no HQ anywhere.**
  - The only network traffic our code causes is the `localhost` call to ICD.
  - ICD and CCDWS talk to CDA, Continovation and the networks themselves, under the office's own CDA and network relationships.
  - Delete `WebServiceMainHQProxy` and `CustomerUpdatesProxy` from this flow.

### 3.6 Ground rules 6 and 7

- **Off by default (rule 6).** After the 4.1 change, nothing runs unless the culture is Canadian **and** the default clearinghouse is a Canadian one. Today the background thread checks only the culture; 4.1 makes it also require `ITRANS2` with a `ResponsePath`, so a US office never starts it. The UI box was already gated on culture plus `ElectronicClaimFormat.Canadian`.
- **Vendor-neutral (rule 7).** Messages name the component the office already chose (ITRANS 2.0 or CCDWS), with no links to sign-up pages and no named vendor in the UI. The Open Dental certificate check names no company: the message just says "another software vendor".
  - The "Import from file…" button sits in the existing ITRANS group box, which is already hidden unless Canadian. That box is only 106×105 px today (`FormCarriers.designer.cs:171-173`), so it has to grow to fit the button.
- **No credentials compiled in.** The only constants are Open Dental's certificate **file names** in the block list, plus CDA's documented `ICD.ini` defaults. No key, certificate or password is embedded, and no secret is stored, so `CDT.Class1.TryEncrypt` isn't needed here.

## 4. Code changes

### 4.1 Clean-up (do even if the office is US-only)

| File | Member | Change |
|---|---|---|
| `OpenDentBusiness/Misc/ItransNCpl.cs` | `TryCarrierUpdate` (lines 62-74) | Delete the HQ `else` branch (the `IsHqAvailable` check and `CanadaCarrierUpdate`). Return the plain "needs ITRANS 2.0" message from 3.3 step 1 |
| `OpenDental/Main Modules/FormOpenDentalThreads.cs` | `BeginCanadianItransCarrierThread` (156-170) | Also require `Clearinghouses.GetDefaultDental()?.CommBridge==EclaimsCommBridge.ITRANS2` with a non-empty `ResponsePath` before starting the thread |
| `OpenDentBusiness/Eclaims/Canadian.cs` | `PassToIca` (1578-1656) | Remove the HQ certificate download, the `RegistrationKeyIsDisabled` toggling and the `ccdws.ini` rewrite. Fail with the message from 3.4 when the `.pem` isn't present |
| `OpenDentBusiness/WebServices/CustomerUpdatesProxy.cs` | `ICustomerUpdates.RequestCertCanadaABC`, `RequestCertCanadaTelusAandB`, `RequestCertCanadaTelusAandBtest` and their `CustomerUpdatesNoHq` stubs (lines 15-17, 43-45) | Delete once `Canadian.cs` no longer calls them |
| `OpenDentBusiness/WebServices/WebServiceMainHQNoHq.cs` and `OpenDentBusiness/WebServices/IWebServiceMainHQ.cs` | `CanadaCarrierUpdate` (`WebServiceMainHQNoHq.cs:81`, `IWebServiceMainHQ.cs:61`) | Delete together with the caller. `WebServiceMainHQMockDemo` gets the method from the generated proxy base class, so it still compiles. The generated `Web References` proxies (`OpenDentBusiness/Web References/WebServiceMainHQ`, `OpenDental/Web References/customerUpdates` and `localhost`) go with the wider HQ removal |
| `OpenDentBusiness/Misc/ItransNCpl.cs` | `TryCarrierUpdate` (line 57) | Even in the clean-up, replace the unbounded `process.WaitForExit()` with `WaitForExit(120000)`, so a stuck ICD can't hang the hourly thread |

### 4.2 Direct implementation (only if the office is Canadian)

| File | Change |
|---|---|
| `OpenDentBusiness/Eclaims/IcdLocalClient.cs` (new) | `IcdIni Read(string icdFolder)`: a small INI reader returning `NCPLFilePath`, `WebPort`, `CertIssuedBy` and `CertIssuedTo`, with defaults. `bool TryGetNcpl(IcdIni ini,out string json,out string error)`: the localhost HTTPS GET with the pinned validation from 3.1 and a 60-second timeout. `bool TryRunGetNcplCommand(string icdFolder,out string error)`: the existing `Process` code moved here, with a 120-second timeout. Optional later: `SendPayload` for the API method (see 7) |
| `OpenDentBusiness/Misc/ItransNCpl.cs` | `TryCarrierUpdate` follows 3.3: local file first, a daily forced refresh, and `GetLastWriteTime`. Add `public static string TryCarrierUpdateFromFile(string path,ItransImportFields fields)`. Map `MBC` and `PBC` in `TrySetCanadianNetworkNum`, and log locally instead of calling `BugSubmissions` |
| `OpenDentBusiness/Eclaims/CanadianVendorCert.cs` (new) | `static bool Validate(CanadianNetwork network,string networkFolder,string ccdwsIniPath,out string errorMsg)` per 3.4, including detection of Open Dental's certificate names |
| `OpenDentBusiness/Eclaims/Canadian.cs` | `PassToIca`: call `CanadianVendorCert.Validate` where `certFileName` is chosen today (1563-1577). Drop the hard-coded `OPENDENTAL.pem` and `OD_…pem` names |
| `OpenDental/Forms/FormCarriers.cs` and `FormCarriers.designer.cs` | Add an "Import from file…" button to `groupItrans` (enlarge the box) that uses `OpenFileDialog` for `*.json` and calls `TryCarrierUpdateFromFile`. `butItransUpdateCarriers_Click` is unchanged apart from the new messages. Leave the unrelated `DentalTek.PlaceCall` click-to-dial alone |
| `UnitTests/UnitTests/CarrierTests.cs` | Add tests for the INI parsing, the file-import path, `MBC`/`PBC` mapping and certificate validation with temporary `ccdws.ini` files. The existing tests that use `test_n_cpl` stay |

**Bridges.dll:** nothing to re-implement. No Bridges types are on this path (see the grep note in 1.2).

**Stubs replaced:** `WebServiceMainHQNoHq.CanadaCarrierUpdate` and the three `CustomerUpdatesNoHq.RequestCertCanada*` stubs are deleted rather than replaced. Their jobs move to the local ICD and to the office's own CCDWS configuration.

**No schema changes and no ConvertDatabases migrations.** Existing `canadiannetwork`, `clearinghouse`, `carrier`, `etrans` and `pref` rows are reused.

## 5. What the office (and the project) must obtain

**A. The project, before any live Canadian use:**

1. **Ask CDA about CDAnet certification** for this product. CDA publishes no certification process or contact. Start with `cdanet@cda-adc.ca`, the address CDA's vendor guide gives for vendor test access; the PSS help desk (1-866-788-1212) is for offices, not certification. Ask for:
   - the certification process
   - the *CDAnet Development Tools* document (the CDAnet Messaging Simulator, network `SIM`), which the Technical Reference (section 1.2) lists but CDA doesn't publish
   - vendor credentials for the test-certificate page on https://services.cda-adc.ca.

   Background: [certified vendors](https://www.cda-adc.ca/for-dental-professionals/programs-amp-services/cdanet-and-itrans/certified-vendors).
2. **Only if hygienist or denturist claims are needed:**
   - Apply to **TELUS Health** for vendor certification and a CCDWS vendor certificate. No public vendor contact was found. The claims-exchange page (https://www.telus.com/en/health/organizations/group-health-benefits/insurers/claims-exchange) is aimed at insurers and blocked automated fetches, so a person has to look up the contact.
   - Apply to **Alberta Blue Cross** for the same. The only public number is its Dental Provider Contact Centre, 1-800-567-8104 ([Dental Benefact 123](https://www.ab.bluecross.ca/pdfs/Dental-benefact-123-may-2021.pdf)), which serves providers, not vendors. Ask them to route the request.
   - Ask CDHA and the Denturist Association of Canada how a product gets onto their certified-software lists: CDHA's list for CCDWS and its DACnet list ([CDHA](https://www.cdha.ca/claimforms), [DACnet](https://denturist.org/en/dacnettm-2)).
   - Expect NDA documents and conformance testing.
3. Get the CCDWS documentation (`ccdws-latest-doc.zip` from https://ccdws.hardersoft.com/) and confirm the `ccdws.ini` keys that name the vendor certificate (section 7).

**B. Each Canadian office (dentists):**

1. Each dentist is a member in good standing of the provincial or territorial dental association (CDA Affiliate Member in Quebec). Each dentist subscribes through **PSS** (https://services.cda-adc.ca, help desk 1-866-788-1212, `pss@cda-adc.ca`) and accepts the CDAnet/ITRANS Service Schedule once per practice location. See [CDAnet and ITRANS](https://www.cda-adc.ca/for-dental-professionals/programs-amp-services/cdanet-and-itrans) and the [Service Schedule](https://cda-adc.ca/en/services/agreements/cdanet.asp).
2. Pick one Windows account on the claim server to run the ICD service.
3. Signed in as that account, install each dentist's **CDA Digital ID**: get a secure code on PSS and paste it into the CDA Practice Support tray app.
4. Install **ICD v4.2** from https://www.cda-adc.ca/for-dental-professionals/programs-amp-services/cdanet-and-itrans/certified-vendors/icd-software. Set the claims folder (default `C:\ICD`). For hybrid or non-dentist offices, also install CCDWS and set the CCDWS network folders (Getting Started section 6; CDA recommends ICD even in offices with no dentists, so they still get the N-CPL).
5. In this program, open **Setup > Clearinghouses** and set the Canadian clearinghouse:
   - CommBridge = ITRANS 2.0
   - Export path = the ICD claims folder
   - Response path = the ICD install folder, the one containing `ITRANS Claims Director.exe` and `ICD.ini`.
6. **Setup > Insurance > Carriers > Update Carriers**: pick the import options, then run it.

**C. Offices with independent hygienists or denturists (only after A.2):**

1. Register for CDHAnet or DACnet with the relevant association.
2. Install CCDWS from https://ccdws.hardersoft.com/.
3. Put the network-issued vendor certificate in the network sub-folder and reference it in `ccdws.ini`, following the CCDWS manual.
4. Remove any `OPENDENTAL.pem` or `OD_*.pem` files left by a previous Open Dental install.

## 6. Test plan

**Environment.**
- A Windows VM with the en-CA culture, MariaDB and a test database.
- ICD v4.2 with the installer's default (production) configuration. Getting Started section 3.2 mentions test N-CPL and ITS sites, but the public manuals give no URLs or `ICD.ini` values for them. Ask CDA for them along with the simulator (5.A.1).
- CDA's test dentist ("A Dentist", UIN 530123401, office 1234). Install its digital ID from the vendor test-certificate page on PSS. CDA's guide says to leave that ID's type on *Production*. Credentials for the page come from `cdanet@cda-adc.ca`.
- For end-to-end transactions, the **CDAnet Messaging Simulator** (network code `SIM`), described in CDA's *CDAnet Development Tools*. That document isn't published; ask CDA for it.

**Carrier list:**
1. With ICD running, click **Update Carriers**. Expect an `etrans` row of type `ItransNcpl`, carriers updated, and "Done."
2. Stop the ICD service and click again. Expect the file to be read, the forced refresh to fail with the "service isn't responding" message, and no crash.
3. Point `[WebInterface] Port` at a closed port. Expect a fallback to `--getncpl`. Then rename the exe and expect a clear error after at most 120 seconds, with no hang.
4. Serve a self-signed `localhost` certificate on 9660 (simple test listener). Expect the TLS pin to reject it and the code to fall back to the command.
5. **Import from file…** with the unit-test `test_n_cpl` JSON. Expect the same result as the existing `CarrierTests` cases.
6. A carrier with `network_folder` `MBC` or `PBC` gets the matching `CanadianNetworkNum`. Then send a test transaction to that carrier through ICD and check that the A01 prefix it carries is accepted (see 3.3).
6a. On a second workstation, with `ResponsePath` a UNC share to the ICD folder, click **Update Carriers**. Expect the shared `n-cpl.json` to be read, no `localhost` call, and no attempt to start `ITRANS Claims Director.exe` there.
7. Set the culture to en-US, or make the default clearinghouse non-ITRANS2. Expect the background thread not to start and nothing to be logged.
8. Use a firewall rule that blocks everything except localhost. Expect no outbound connection from `OpenDental.exe` during any of the above (watch with `netstat` or a firewall log).

**Certificates (ClaimStream):**
9. A `C:\ccd\telusb` folder with no `.pem`, and send a hygienist claim. Expect the vendor-certificate message, **no `input.###` written**, and no network call.
10. `ccdws.ini` referencing `OD_2023-02-20_2028-02-24_prod.pem` (an empty dummy file). Expect the "belongs to another software vendor" error.
11. `ccdws.ini` referencing a dummy `acme_vendor.pem` that exists. Expect the check to pass, and the existing CCDWS timeout or error path to run as before.
12. instream: no certificate check. Behaviour is unchanged.
13. Confirm `ccdws.ini` is byte-identical before and after each send.

**Claims (regression, CDA's documented live smoke test):**
14. Send CDA's sample claim for the test dentist through ICD. Expect a rejection from the live claims processor, because that dentist or patient is unknown there. That proves ICD, the digital ID and the routing work. This is CDA's own documented check in Getting Started section 4.1.2. It goes to production systems, so send only CDA's sample message, never real patient data.

## 7. Size, risks and open questions

**Size: S.**

| Part | Estimate |
|---|---|
| Clean-up (4.1) | 0.5 day |
| ICD local client and the carrier-list rework | 0.5 to 1 day |
| Certificate validation | 0.5 day |
| Tests | 0.5 day |
| **Total** | **About 2 days** |

External work isn't included: CDAnet certification and the network vendor certificates take months and depend on CDA, TELUS Health and ABC.

**Risks:**
- **Certification.** Running an uncertified PMS against CDAnet may breach CDA's terms. That is a business and legal risk for any Canadian office, independent of the code.
- **Vendor certificate.** Without our own TELUS or ABC vendor certificate, hygienist and denturist e-claims stop working for offices that relied on Open Dental's. Those claims go on paper until certification is done.
- **Leftover Open Dental certificates.** Offices migrating from Open Dental may still have `OD_*.pem` files in place. Open Dental's production certificate is valid until February 2028. Blocking it is the honest choice, but it will look like a regression to users.
- **Schema drift.** The field names in the manual disagree with the code and with each other:
  - The Appendix A schema has `summary_reconciliation_04`, `summary_reconciliation_15`, `payment_reconciliation_06` and `payment_reconciliation_16`.
  - The examples in section 3.2 and Appendix D have `summary_reconciliation_04`, `_15`, `_06` and `_16`, with no `payment_` fields.
  - Our class maps `Summary_Reconciliation_05` and `Payment_Reconciliation_06` (`ItransNCpl.cs:289-292, 340-346`).
  - Our test resource `UnitTests/Resources/test-n-cpl.txt` uses `summary_reconciliation_05` and `payment_reconciliation_06`, which matches CDAnet's numbering (05 = summary reconciliation request, 06 = payment reconciliation request).

  So the manual is probably wrong. Still, check against a real `n-cpl.json` from ICD 4.2: an unmatched name reads silently as `Missing` and drops the flag.
- **ICD TLS pinning.** The localhost certificate's names come from `ICD.ini`. If CDA changes them, the GET fails and we fall back to the command, so the failure is safe.
- **File method.** Unencrypted claim files still pass through `C:\ICD`. CDA recommends the API method (`POST /ICD/SendPayload/`); see the follow-up question below.

**Open questions for the owner:**
1. **Is the office US-only?** If yes, ship 4.1 only and close the rest as "not needed". Nothing found suggests Canada, but nobody has checked `fox-db` (see the query at the top).
2. **Should Open Dental's leftover certificates be blocked or only warned about?** Recommendation: block, because they identify another vendor and we can't vouch for how the private key was obtained.
3. **Will the project seek CDAnet certification** and network vendor certification? If not, should the Canadian claim UI warn that the product isn't CDA-certified, while still allowing claims to be printed?
4. **Should claim transport move to the ICD API method** (`POST /ICD/SendPayload/`) to stop writing PHI to disk? That would be a separate spec. It replaces the file loop in `PassToIca` for `ITRANS2` and needs the same pinned localhost TLS.
5. **What are the CCDWS `ccdws.ini` keys** that name the vendor certificate per network? They are in the CCDWS manual (`ccdws-latest-doc.zip`, public), which hasn't been read for this spec: the zip wasn't downloaded during the fact-check. Until then, validation uses the `*.pem` token fallback in 3.4.
6. **Legacy `ITRANS` offices.** Since ITRANS v1 was retired on 2026-06-30, should sending with `CommBridge=ITRANS` fail early with "ITRANS v1 has been retired; switch this clearinghouse to ITRANS 2.0"? Recommendation: yes, as a small add-on to 4.1.

## Sources

- CDA, CDAnet and ITRANS: https://www.cda-adc.ca/for-dental-professionals/programs-amp-services/cdanet-and-itrans
- CDA, certified vendors: https://www.cda-adc.ca/for-dental-professionals/programs-amp-services/cdanet-and-itrans/certified-vendors
- CDA, ICD software and vendor docs: https://www.cda-adc.ca/for-dental-professionals/programs-amp-services/cdanet-and-itrans/certified-vendors/icd-software
- CDA, ITRANS 2.0 Technical Reference revision 1.5 (April 7, 2025; ICD v4.1): https://media.cda-adc.ca/sitefinity-blobs/docs/default-source/icd-software/itrans_2-0_vendor_technical_reference_1-5.pdf
- CDA, ITRANS 2.0 Getting Started revision 1.5 (April 7, 2025; ICD v4.1): https://media.cda-adc.ca/sitefinity-blobs/docs/default-source/icd-software/itrans_2-0_vendor_getting_started_1-5.pdf
- CDA, ITRANS v1 retirement (June 30, 2026): https://www.cda-adc.ca/en/services/cdanet/itrans_updates.asp
- CDA, CDAnet/CDA ITRANS Claims Service Schedule: https://cda-adc.ca/en/services/agreements/cdanet.asp
- CDHA, CDHAnet routes for independent hygienists (CCDWS-certified software, instream option): https://www.cdha.ca/claimforms
- Denturist Association of Canada, DACnet: https://denturist.org/en/dacnettm-2
- CDA, error codes (ICD, ICA, CCDWS, CDAnet): https://media.cda-adc.ca/sitefinity-blobs/docs/default-source/icd-software/errorcodes-icd-ica-ccdws-cdanet.pdf
- CDA, CDAnet carrier list (human-readable): https://www.cda-adc.ca/for-dental-professionals/programs-amp-services/cdanet-and-itrans/carrier-list
- CCDWS downloads and docs: https://ccdws.hardersoft.com/
- Alberta Blue Cross, Dental Benefact 123 (vendor certification for internet claims): https://www.ab.bluecross.ca/pdfs/Dental-benefact-123-may-2021.pdf
- TELUS Health, claims exchange (HTTP 403 to automated fetch on 2026-10-18; summary from the search index): https://www.telus.com/en/health/organizations/group-health-benefits/insurers/claims-exchange
- Open Dental manual, ClaimStream (how the old setup used `OPENDENTAL.pem`): https://opendental.com/manual/claimstream.html. This is a citation only; no Open Dental host is called by any code in this spec.

## Fact-check log (2026-10-18)

Checked against the CDA manuals (revision 1.5, text extracted from the PDFs), CDA's web pages, the CCDWS download index, the ABC bulletin, CDHA's page, and `main` at `6855026f5`. Changes made:

- **Feasibility:** partner-only is **confirmed** for live claims: CDAnet certification, plus network and CCDWS certification for hygienist and denturist claims. The carrier-list part is noted as publicly documented and buildable now.
- **Added:** ITRANS v1 was retired on 2026-06-30, so the legacy `ITRANS` CommBridge is dead (new table row; open question 6).
- **Added:** hygienist and denturist claims sent *through ICD* still reach CCDWS and still need the vendor certificate. The summary had implied that ITRANS 2.0 avoids this.
- **Added:** CDHA states the "CCDWS certified" software requirement, and instream is a paid hygienist registration.
- **Corrected:** the manuals are written for ICD v4.1; v4.2 (June 4, 2026) is the current installer.
- **Corrected:** the Continovation "operates ITRANS" claim was softened to what the `ICD.ini` defaults show.
- **Corrected:** the US-office "evidence" was reworded (none of it is direct), and a `fox-db` check was added.
- **Corrected:** the gating description. Today the hourly thread checks only the culture; the UI box checks culture plus the Canadian claim format.
- **Corrected:** "the owner removed reference-data downloads". The decoupling plan actually says "make configurable or bundle locally".
- **Corrected:** CDAnet eligibility and the Service Schedule wording, now with a source link. Also, the certified-vendors page gives no certification contact, and 1-866-788-1212 is the office help desk, not a vendor line.
- **Corrected:** the claim that TELUS B carries almost all carriers was removed, since nothing published supports it. The TELUS page could not be fetched, which is now said.
- **Corrected:** section citations (renewal is Technical Reference 2.4.4/2.5.7, and the default window differs between documents; ICD.ini location is 2.2; tray app is 5.2/5.3; API rationale is Getting Started 2 and 3).
- **Design fix:** `NCPLFilePath` is a path on the ICD host. Other workstations must use `ResponsePath`, and the localhost REST call and `--getncpl` only work on the ICD host (new test 6a).
- **Design fix:** the daily forced refresh is now tracked by the file's write time, not the newest `etrans`, which isn't written when the list is unchanged.
- **Design fix:** the TLS check now allows only the documented name mismatch (and a missing CDA root). It also notes that name matching is too weak for a future PHI-carrying `SendPayload`.
- **Design fix:** `MBC`/`PBC` mapping is not cosmetic. It changes the A01 transaction prefix (`Canadian.cs:211`), so it needs an end-to-end test. Added the `ConvertDatabases2.cs:4914-4916` seeding and `SIM` handling.
- **Design fix:** the clean-up now also bounds `WaitForExit`, and the 4.1 table lists the exact interface files and the generated proxies.
- **Code touchpoints:** all re-verified by grep and line number. `BugSubmissions.SubmitException` is confirmed local-only (`BugSubmissions.cs:397-418`).
- **Bridges.dll:** none on the Canadian path, confirmed. The one Bridges type in `FormCarriers.cs` (`DentalTek.PlaceCall`, click-to-dial) is now named and left untouched.
- **Schema-drift risk:** restated with the exact field names. The repo's test data supports our `_05`/`_06` mapping.
- **Test plan:** CDA's documented message test uses the production configuration against live processors with CDA's sample claim. The test-environment URLs and the *CDAnet Development Tools* document are not public.
- **Copied text:** quotes from CDA and ABC were replaced with paraphrases. One short CDHA phrase remains, in quotation marks.
- **Rule checks:** no credentials compiled in, no Open Dental hosts called, and the UI names no vendor. All confirmed.
