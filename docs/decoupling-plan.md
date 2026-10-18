# Decoupling plan: from Open Dental fork to independent open source project

Status: draft, 2026-10-18. Baseline: upstream Open Dental 24.3.63.0 (`main`).

## Goal

Turn this fork into a truly open source dental practice management system with **no dependency on Open Dental Software, Inc.**:

- no calls to their servers (HQ web services, eServices, updates, telemetry, support, registration keys)
- no proprietary prebuilt binaries in the build
- no Open Dental branding

After that, port desktop features from Open Dental 26.1.70, **re-implemented from Open Dental's public documentation** (manual and release notes). We don't decompile their binaries.

## Ground rules

1. **We don't merge from upstream anymore.** Public source stops at 24.3.63, so `.csproj` and solution files can now be edited freely. The `Directory.Build.targets` DLL substitution and `OpenDental.Local.slnf` are temporary and go away in Phase 2.
2. **Stay schema-compatible with Open Dental's database**, so the office's data (Open Dental 25.3.48 on `fox-db`) can migrate onto this fork. Rename or drop tables only with a migration.
3. **No encrypted data to preserve.** Per the owner, existing data isn't encrypted, so Open Dental's crypto doesn't need to be reproduced.
   - Bridge passwords that Open Dental stored masked will be re-entered after migration.
4. **Nothing proprietary in the repo**: no ADA CDT code text, no ADA claim-form artwork, no "Open Dental" trademark in the product. GPL v2 copyright notices stay.
5. **Core offline use must keep working at every step**: charting, scheduling, billing, claims, imaging, reports.
6. **Everything runs locally; nothing depends on the cloud.**
   - The database runs on the office's own MariaDB server.
   - Any service or API the product needs is self-hosted on the office network: updates via the MariaDB update path, background services, and any future patient-facing services.
   - Patient data and images stay on local storage.
   - Third-party vendor integrations that are inherently online (QuickBooks Online, Microsoft 365 email, USPS, card processors, eRx, …) are optional and off by default. They are configured per office and call the vendor directly from the office. Core functionality never requires them.
7. **Completely neutral toward third-party vendors; no vendor promotion of any kind.**
   - An integration's buttons, menus and panels appear only after the office has enabled that integration.
   - No ads, partner promos, "learn more" / "sign up" / "apply now" / free-trial links, referral or affiliate URLs, vendor logos used as promotion, or "recommended vendor" defaults.
   - Integration setup screens describe what the integration does, and nothing more.
   - Open Dental's "Disable Advertising" mechanism and its server override are removed (the stored property rows stay for schema compatibility).
   - Vendor-neutral features stay, e.g. the monthly backup reminder.

## What we found

### Proprietary prebuilt DLLs (`Required dlls/`)

| DLL | What it actually is | Our usage | Decision |
|---|---|---|---|
| **CDT** | Secrets encryption (`Encrypt`/`Decrypt`, SFTP and clearinghouse variants), salted hash for `SecurityHash` columns, registration-key validation, ADA claim-form images. The CDT code text is already empty in the shipped "foreign" build. | ~145 call sites | Replace with an in-repo library. New AES-GCM secret storage with a per-install key, HMAC-SHA256 for `SecurityHash`, key validation removed, claim-form backgrounds blank or our own artwork. CDT codes come from a user import. |
| **ODCrypt** | Standard algorithms: MD5, SHA3-512, salt and random generation, gzip | ~20 | Thin in-repo shim over .NET and BouncyCastle; stored user password hashes stay byte-compatible |
| **VirtualWeb** | Thinfinity "ODCloud" browser-hosting shim | 42 files, almost all behind `IsThinfinity()` | Delete the Thinfinity code paths |
| **OpenDentalHelp** | Help-key/support-status check, manual URL builder, HQ SOAP client | 11 | Replace with an in-repo help-URL map. Remove the key gates, which also **un-break imaging acquisition, video capture and USPS address checks in Release builds** |
| **ODApi** | Open Dental's local REST API server (port 30222), with key checks against HQ | 6 calls, 2 files | Remove (≈40 lines). Revisit only if a local tool needs an API. |
| **Bridges** | Five integrations: CareCredit, PostcardMania, QuickBooks Online, Microsoft 365 email (Open Dental's Azure app), USPS address validation (via HQ) | ~150 refs, 23 files | Cut CareCredit and PostcardMania. QuickBooks Online, Microsoft 365 and USPS: see the open decisions below. |
| **ImagingDeviceManager** | Wrapper over the commercial EZTwain TWAIN library (plus a dead XDR sensor wrapper) | ~84 calls, 5 files | **Keep the feature**: re-implement scanning on NTwain (MIT) or WIA and drop `Eztwain4.dll` |
| **EHR** | Not shipped at all; Meaningful Use 2014 dashboard, loaded by reflection | 0 at compile time | Remove the reflection hooks and the MU forms |

`Required dlls/` holds 158 files in total. Phase 2 includes a license audit of all of them. `xImageDeviceManager.dll` (Suni sensors), `Eztwain4.dll` and others are proprietary too.

### Couplings to Open Dental's servers

| Area | Mechanism | Decision |
|---|---|---|
| HQ web services | `WebServiceMainHQ` (88 methods) and `CustomerUpdates` (26) SOAP proxies, plus SheetsSynch (Web Forms). A ready-made mock already exists: `WebServiceMainHQMockDemo`. | "NoHq" stub implementations behind the existing factories, then delete the callers feature by feature |
| Registration key and license | Startup key validation (exits if invalid), forced license-signature prompt, 10-minute "unauthorized version" nag, dev-key check, help key | Remove all of them |
| HQ kill switch | `Programs.IsEnabledByHq` can **delete** program links when HQ says so; HQ also overwrites program properties | Remove |
| Updates | "Check for updates" contacts HQ and downloads `Setup.exe` from opendental.com. The workstation-to-workstation update propagation is local. | Keep local propagation; remove the HQ update check; later add our own release channel |
| Telemetry | Unhandled exceptions are **auto-sent to HQ by default**: stack trace, patient number, server and database names, reporting-server credentials. Also eService logs, version history and enabled-program lists. | Off and removed |
| Support | Online Support, Support Status, Request Features, Training Videos, and Remote Support (downloads and runs an exe from opendental.com) | Remove |
| eServices | Web Sched, texting, eClipboard, patient portal, secure email, payment portal, Web Forms, eConnector. Signup state comes from HQ. | Out of scope: remove the UI and code. Self-hosted replacements can come later as separate modules. |
| Help | F1 manual links (via OpenDentalHelp) plus ~64 hard-coded opendental.com links | One configurable help base URL; our own docs later |
| HQ staff tools | `InternalTools` (~270 files: Job Manager, phones, web chat, resellers), gated by `IsODHQ` | Pin `IsODHQ` to false, then delete. Keep the table schema. |
| Other downloads | Medication lists, translations, Canadian fee schedules, code systems, partner redirect pages | Make configurable or bundle locally |
| Branding | `SoftwareName` pref, splash, icons, ~144 "Open Dental" strings | New name (to be decided), done gradually |

### Schema baseline

`fox-db` (Open Dental 25.3.48, 422 MyISAM tables) compared with our 24.3.63 code:
- **15 new tables**: intra-office chat (7), dental school scheduling (3), `feeschednote`, `inspending`, `queryfilter`, `branding`, `deletedobject`, `sequencecounter`.
- **47 new columns**: appointment mirroring, fee effective dates, eForms fields, check printing on journal entries, hidden medications, tooth-range benefits, and others.
- **One rename**: `task.triagecategory` → `task.category`.
- **54 tables in our code that `fox-db` lacks.** Almost all are HQ-internal: Job Manager, phones, resellers, web chat, billing, bug submissions.

The full diff is kept outside the repo: `D:\Dev\opendental-schema\schema-diff-24.3.63-vs-25.3.48.txt`.

## Phases

### Phase 1: Cut the network cord. Size: M, ~3–5 days.

1. "NoHq" implementations of `IWebServiceMainHQ`, `CustomerUpdates` and SheetsSynch, returning "not available". Route the direct `new Service1()` call sites through the factories first.
2. Remove the registration-key gate, license-signature prompt, nag thread, dev-key check, help-key gates and `IsEnabledByHq`.
3. Make bug submission a no-op, with `SendUnhandledExceptionsToHQ` off. Keep local logging.
4. Remove the support, update-check and feature-request menu items, and hide eServices setup.
5. Add a migration that blanks the HQ URL prefs (`WebServiceHQServerURL`, `UpdateServerAddress`, `UpdateWebsitePath`, `WebHostSynchServerURL`, …).
6. Pin `IsODHQ` to false.

**Done when:** a Release build runs a full day of normal use against a local database with outbound traffic to `*.opendental.com`, `opendentalsoft.com` and `patientviewer.com` blocked, with no errors or nags. A grep check in CI keeps those hosts out of live code.

### Phase 2: Replace the proprietary DLLs. Size: L, ~2–3 weeks.

ODCrypt shim → VirtualWeb/Thinfinity removal → OpenDentalHelp replacement → ODApi removal → EHR removal → CDT replacement → Bridges split → TWAIN on NTwain → `Required dlls` license audit. Then delete `Directory.Build.targets`'s substitution and the solution filter, and give the solution only real projects.

**Done when:** the solution builds from a clean clone with no proprietary binaries; scanning works with a TWAIN scanner; claims print on pre-printed ADA forms.

### Phase 3: Remove dead weight. Size: M, ~1 week.

Delete `InternalTools`, eServices UI and code, the Thinfinity/ODCloud/AppStream paths, MU/EHR forms, and HQ-only tables from the code. The tables stay in the database for compatibility.

### Phase 4: Identity and distribution. Size: M.

New name, branding, and an installer (Open Dental's installer isn't in the repo, so this means WiX or Inno Setup). Our own docs and help base URL. Releases through GitHub, plus a self-hosted update manifest for local propagation.

### Phase 5: Office migration path. Size: L.

- Port the 24.3 → 25.3 schema changes above, with the matching features, at least where the office already has data in them.
- Migrate a **restored copy** of `fox-db`, never the live database: re-enter masked passwords, rehash `SecurityHash`, verify.
- Pilot one workstation, with rollback to Open Dental 25.3.48.

### Phase 6: Feature parity with 26.1.70.

Prioritized from the release notes. The 25.3 → 26.1 schema changes need a 26.1 database to compare against; a fresh 26.1 trial install is one source.

## Open decisions

1. **Integrations to keep**, each re-implemented with our own vendor credentials: QuickBooks Online, Microsoft 365 email, USPS address validation, the local REST API.
2. **Project name.**
3. **Procedure codes for new installs**: CDT import by the user (needs an ADA license), or ship only non-CDT codes.
4. **eServices**: drop permanently, or plan self-hosted replacements (Web Sched, texting, patient forms) as later modules.
