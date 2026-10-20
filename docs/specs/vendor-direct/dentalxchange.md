# DentalXChange (ClaimConnect, XConnect): partner identity, attachment flag and claim validation

Status: implemented by package V-DXC1 (M1, wave A1), 2026-10-19. Not yet run against a DentalXChange test account. Feature specs: `v24_4-claims-dxc-attachment-submitted` in [../features-24.4.md](../features-24.4.md) and `v25_2-account-xconnect-claim-validation` in [../features-25.2.md](../features-25.2.md).

## Partner identity (decoupling)

DentalXChange's SOAP web services (DWS: eligibility, ERAs, payer list, claim validation; DEA partner service: attachments) and the DCI claim upload expect a partner identity next to the office's clearinghouse login: a **Client** name and a **DCI web service ID**. Open Dental 24.3 compiled in its own (and Practice-Web's). The fork compiles in nothing.

- **Where:** Setup > Family/Insurance > Clearinghouses, the ClaimConnect clearinghouse, DentalXChange box: **Client** and **Service ID**. One value for the whole office, edited from Unassigned/Default.
- **Storage:** two fork-only `preference` rows, `ForkDxcPartnerClient` and `ForkDxcPartnerServiceID` (not in the `PrefName` enum, like the migration runner's rows). They are inserted the first time the office saves; an adopted Open Dental database has none and reads as blank. No schema change.
- **Blank means off.** While either value is blank, nothing that sends the identity runs and no network call is made: claim upload (`ClaimConnect.Launch`, which also sends the Client as `UploaderName`), eligibility (`Benefits270`, and the Practice-Web path in `FormInsPlan`), ERA download (`Retrieve`), the payer list, DWS claim validation and the partner-service attachment calls. Each says what to set up.
- **XConnect does not need it.** XConnect calls send the office's own login, password and XConnect API key as headers, and no partner identity.
- **To do with DentalXChange:** ask for the practice's own partner Client and service ID (or written permission and values to use). Until then, offices that send claims through DentalXChange's SOAP services can't send from this fork; XConnect validation and attachments work with the office's own API key.

## Attachment "Submitted" flag (Open Dental 24.4)

- `claim.AttachedFlags` gets the keyword `Submitted` once the claim's DentalXChange attachment has been sent (`ClaimConnect.SendAttachmentImages`, used by the paste, snip and image-picker windows). It also gets `Misc`, as before, so the 837 writes the attachment ID in its PWK segment; `Mail` is dropped.
- New images clear the flag in memory until every call has succeeded. Clear Attachment removes it, and so does every Edit Claim save once the claim has no attachments left, whatever the clearinghouse (XConnect included) or claim status (`FormClaimEdit.UpdateClaim` calls `ClaimAttaches.SyncDxcSubmittedWithAttachments`).
- Every writer keeps keywords it does not manage: Edit Claim's check boxes keep `Submitted` and `Unsup` (`ClaimAttaches.RebuildFromCheckBoxes`).
- The 837 builders ignore `Submitted` (`ClaimAttaches.HasPaperworkFlags`), so `Mail,Submitted` asks for no paperwork, and they skip "Attachment ID missing" once the attachment was submitted. XConnect's attachment messages and the DWS "attachment required" prompt are skipped too.
- Edit Claim's DentalXChange tab, Attachment History and the paste and image-picker windows show the state. Before a window adds images to an attachment that was already submitted, it asks once.

## XConnect claim validation (Open Dental 25.2)

- **On/off:** `clearinghouse.EnableXConnect` (Enable XConnect check box, per clinic like attachment sending) and the XConnect API key in `LocationID` (labelled API Key for ClaimConnect). `XConnect.IsEnabled` needs ClaimConnect, the switch and the key. Turning it on or off writes a Setup security log line per clinic.
- **Migration:** fork step `2026101951` (`XConnectEnableStep`) turns the switch on where 24.3 had XConnect on (a ClaimConnect row with a key, and clinic overrides that use one), only for databases that come from 24.3.
- **Save (Edit Claim, Waiting to Send):** validates for at most 10 seconds and never blocks saving. A failed call gives a non-modal note; problems open the Claim Validation window non-modally.
- **Send (Edit Claim):** errors keep the claim unsent and open the window; warning-only claims go after Send Anyway. A failed call keeps the claim unsent, as in 24.3.
- **Send Claims window:** each claim is validated; one combined window lists the problems grouped by claim. Claims with errors (or a failed call) stay in the queue, warning-only claims go after Send Anyway, and clean claims go. Re-validating in the window can free a claim.
- **Send from the "Outstanding ... claims" prompt** (after a primary claim is received): the electronic claims whose clearinghouse has XConnect on are validated before sending, with the same combined window and the same rules; claims with errors or a failed call stay waiting to send. The validation runs at that call site (`ClaimL.FilterQueueItemsByXConnect`), not in `SendClaimSendQueueItems`, so the Send Claims window does not validate twice.
- **Claim Validation window** (`FormXConnectValidation`): patient, claim date and carrier; Severity, Location, Message, How to fix, Status. Unresolved rows are yellow; problems a re-validation no longer returns turn white and say Resolved. Buttons: Edit Claim, Re-validate, Send Anyway (send paths, warnings only), Close.
- Only primary, secondary and tertiary claims are validated; preauthorizations are skipped.
- **Log:** every call is an `etrans` pair: the request as `Claim_XConnect` (36) and the response as `ClaimAck_XConnect` (37), linked through `AckEtransNum`. A call without a response logs the request with a note.
- "How to fix" is the fork's own hint, chosen from the claim part and message DentalXChange returns.

## Not done / open

- Nothing was run against DentalXChange: no test account was available. The response mapping follows the 24.3 XConnect response model in `ClaimConnect.cs`.
- Claim submission through XConnect (25.2 open question 6) is not implemented; claims still go through ClaimConnect's upload.
- Tests: `tests/VendorDirectTests/DentalXChange` (flags, validation rules, the HTTP call against a fake handler, partner identity, the etrans log and the migration step on a scratch MariaDB database).
