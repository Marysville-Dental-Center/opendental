# PaySimple: direct-to-vendor spec

Status: draft, 2026-10-18; fact-checked against PaySimple's public docs and `main` on 2026-10-18. Baseline: `main` at `6855026f5`. Vendor: PaySimple (EverCommerce). API: PaySimple API 4.0, plus the API 5 webhook-subscription endpoints.

## Summary

- **The payment API calls are already direct.** `OpenDentBusiness/WebBridges/PaySimple.cs` is in-repo source. It calls `api.paysimple.com/v4` with the office's own merchant **API username and API key**, stored per clinic in `programproperty`.
  - No Bridges.dll type is involved.
  - Open Dental's HQ never supplied a PaySimple credential, partner key or token. The brief assumed HQ held partner keys, but the code doesn't bear that out.
- **HQ provided one thing: the ACH settlement relay.** Two HQ calls supported it, both now "not available" stubs:
  - `GetPaySimpleWebHookUrl()` returned the URL of Open Dental's hosted webhook receiver. On Save, the setup window subscribed the office's PaySimple account to `payment_failed`, `payment_returned` and `payment_settled`, delivered to that URL.
  - `InsertPaySimpleACHId(PaymentId)` registered each new ACH payment ID with HQ. HQ could then route PaySimple's events back to this office (through the eConnector, outside this repo) and move `payment.PaymentStatus` from Posted to Settled or Failed.
- **Direct replacement: local polling, not webhooks.** A webhook needs a public HTTPS endpoint, and ground rule 6 keeps everything on the office network. Instead, the client asks PaySimple for the status of each Posted ACH payment (`GET /v4/payment/{id}`), plus one list call to catch late returns. It updates `PaymentStatus` locally. No inbound port and no HQ.
- **Clean-up in the same change:**
  - switch the deprecated `PSSERVER` HMAC header to the current `basic username:key` scheme
  - encrypt the API key (`osd1$`)
  - replace the build-type sandbox switch with a per-office setting
  - remove the Open Dental partner-referral link and the PaySimple logo buttons
  - stop showing the PaySimple button to offices that haven't enabled it
  - delete a commented-out credential inherited from upstream
  - let the office review and remove webhook subscriptions that still send its payment events to Open Dental
- **Feasibility: public API.** PaySimple publishes the API 4 reference, sandbox and webhooks, and a merchant key is all that's needed. Two caveats:
  - PaySimple's API Terms say production keys follow PaySimple's review of the integration. The Production page and FAQ, by contrast, only tell the authorized signer to call Customer Care, so whether an existing merchant key needs a new review is an open question (1).
  - PaySimple asks developers to talk to its integration team before building.
- **Size: M**, about 3 to 5 days plus sandbox time.

## 1. What the integration does, and what HQ provided

### 1.1 What the office uses it for

PaySimple is a card and ACH (bank debit) processor. The integration is optional and off by default (`program.Enabled`). Once enabled, it supports:

| Feature | Where | API calls |
|---|---|---|
| Card sale, with an optional saved card | `FormPaySimple` (from `FormPayment`) | `POST /v4/customer` (first time per patient), `POST /v4/account/creditcard`, `POST /v4/payment` |
| Add a card without charging (Auth) | `FormPaySimple` (`isAddingCard`), `FormCreditCardManage` | `POST /v4/customer`, `POST /v4/account/creditcard` |
| Void a same-day card payment, or return an older one | `FormPaySimple`, `FormPayment.VoidPaySimpleTransaction` | `PUT /v4/payment/{id}/void`, `PUT /v4/payment/{id}/reverse` |
| ACH payment from a checking or savings account | `FormPaySimple` ACH tab | `POST /v4/account/ach`, `POST /v4/payment` (`PaymentSubType=PPD`) |
| Delete a stored card or bank account | `FormCreditCardEdit` | `DELETE /v4/account/creditcard/{id}`, `DELETE /v4/account/ach/{id}` |
| Recurring charges against a stored token | `RecurringCharges.SendPaySimple` | `POST /v4/payment` |
| Prepaid insurance cards (no patient) | `FormClaimPayEdit` | `POST /v4/customer` ("UNKNOWN"), card, payment |
| Receipts, payment note text | `PaySimple.ApiResponse.BuildReceiptString`, `ToNoteString` | none |

Local data:

- `creditcard.PaySimpleToken`: the PaySimple account ID of the stored card or bank account.
- `creditcard.CCSource`: `PaySimple`, `PaySimpleACH`, or the portal variants.
- `patientlink` with `LinkType=PaySimple`: patient to PaySimple customer ID.
- `payment.ExternalId`: the PaySimple payment ID.
- `payment.PaymentStatus`: `PaySimpleAchPosted`, `PaySimpleAchSettled` or `PaySimpleAchFailed`.

Settings are `programproperty` rows per clinic (ClinicNum 0 is the default, labelled "Headquarters"). They are listed in section 3.2.

Open Dental's public manual describes the ACH tab as requiring a registration key and the eConnector. It also says payments stay pending until settlement updates arrive, and that clicking OK once in the setup window turns on automatic Settled/Failed updates ([Open Dental manual: PaySimple](https://opendental.com/manual/paysimple.html)). That matches the code below.

### 1.2 What Open Dental's HQ provided

From `D:/Dev/opendental-schema/hq-vendor-table.md` (PaySimple row) and the code on `main`:

| HQ piece | Code on `main` | What it did | State now |
|---|---|---|---|
| `IWebServiceMainHQ.GetPaySimpleWebHookUrl()` | `OpenDental/Forms/FormPaySimpleSetup.cs:322`, inside `WebhookHelper`, run from `butSave_Click` for every clinic with credentials | Returned the URL of Open Dental's webhook receiver. The form then posted subscriptions for the missing `payment_failed`, `payment_returned` and `payment_settled` events to `/ps/webhook/subscription` in the office's own PaySimple account | `WebServiceMainHQNoHq` throws `HqNotAvailableException.ForVendor("PaySimple")`. The HQ call is only reached when a clinic's account lacks at least one of the three subscriptions. Those clinics get an error line in the dialog on every Save with PaySimple enabled. An account that Open Dental already subscribed sees no error, and its events keep flowing to Open Dental's receiver (section 3.6) |
| `IWebServiceMainHQ.InsertPaySimpleACHId(payload)` | `OpenDental/Forms/FormPaySimple.cs:381-389`, `ProcessPaymentACH` | Sent the new PaySimple payment ID (`eServiceCode.PaySimple`) to HQ, so HQ's receiver could map events to this office | Throws. After every ACH sale, and also after adding a bank account without charging (the call sits outside the `_isAddingCard` branch), the user sees "Unable to register for ACH Settled event" |
| Write-back of Settled or Failed (inferred) | Nothing in this repo writes `PaySimpleAchSettled` or `PaySimpleAchFailed`. `EServiceSignal` type 28 is described as "Originally for paysimple ACH payments", and `Payments.GetForExternalId` is commented "Used in OpenDentalWebApps" | HQ and the eConnector (both outside this repo) updated the office's payment rows | Nothing updates them. ACH payments stay "Posted" forever |
| Payment Portal (eService) | `ProgramProperties.GetPaySimpleOnlinePaymentCreds`, property `PaySimpleIsOnlinePaymentsEnabled`, `PaySimple.GetCheckoutToken` / `GetCustomerToken` (no desktop callers), `WebTypes/Payments/PaySimpleStatus.cs` | Open Dental's hosted patient portal took payments with the office's key | Out of scope: eServices are removed (decoupling plan). The checkbox should be hidden |
| Partner referral link | `FormPaySimpleSetup.linkLabel1` → `https://paysimple.com/partner/open-dental` | Open Dental's referral page at PaySimple | Must go (ground rule 7) |

Not provided by HQ: the API username and key. The office got these from PaySimple and typed them in.

## 2. The vendor's public API

### 2.1 Documentation status

- The API is publicly documented at [documentation.paysimple.com](https://documentation.paysimple.com/docs/welcome). The welcome page calls API 4 the legacy, full-featured API and says it remains fully supported.
- The same page asks developers to meet PaySimple's integration team before starting, at partnersupport@paysimple.com.
- An interactive API 4 reference with the URI list, paging and filters is at [sandbox-api.paysimple.com/v4/help](https://sandbox-api.paysimple.com/v4/help).

### 2.2 Base URLs

| Environment | API 4 | Webhook subscriptions (API 5 style) | Merchant web UI |
|---|---|---|---|
| Production | `https://api.paysimple.com/v4` | `https://api.paysimple.com/ps/webhook/...` | PaySimple merchant login |
| Sandbox | `https://sandbox-api.paysimple.com/v4` | `https://sandbox-api.paysimple.com/ps/webhook/...` | `https://sandbox-payments.paysimple.com` |

Sources: [Sandbox](https://documentation.paysimple.com/docs/sandbox), [Production](https://documentation.paysimple.com/docs/production), [Webhooks](https://documentation.paysimple.com/docs/webhooks). The Webhooks page names only the production host. The sandbox webhook path is inferred from the code: `GetApiUrl` already sends `/ps/webhook/...` to `sandbox-api.paysimple.com` in debug builds. Confirm it in the sandbox.

- PaySimple supports TLS 1.2 and above only ([Security and Compliance](https://documentation.paysimple.com/docs/security-and-compliance)). `CodeBase/Utilities/ODInitialize.cs` already turns on TLS 1.2.
- The code keeps `Introspection.IntrospectionEntity.PaySimpleApiURL` as a base-URL override.

### 2.3 Authentication, step by step

There is no OAuth, no token endpoint, no browser step and no refresh token. Each request carries static merchant credentials.

1. The merchant's authorized signer asks PaySimple Customer Care for an **API username** and **API key** (a shared secret) ([Production](https://documentation.paysimple.com/docs/production), [FAQ](https://documentation.paysimple.com/docs/faq)). Production credentials arrive by encrypted email ([Authorization](https://documentation.paysimple.com/docs/authorization)).
2. Every request sends `Authorization: basic <apiUsername>:<apiKey>`. This looks like HTTP Basic, but PaySimple specifies **no Base64 encoding**. The authentication overview says API 4 now accepts the same schemes as API 5 ([Authentication and Authorization](https://documentation.paysimple.com/reference/authentication-overview)).
3. The other current scheme, `reseller <resellerKey>` plus a `PaySimple-Merchant-Id` header, is for partners. We don't need it.
4. Today's code sends the legacy header: `PSSERVER AccessId=...; Timestamp=...; Signature=base64(HMAC-SHA256(key, timestamp))`. PaySimple says it is no longer recommended. It also fails if the PC clock is more than five minutes off PaySimple's server time ([Legacy Authorization](https://documentation.paysimple.com/reference/legacy-authorization)). The webhook calls in the same file already use `basic`.
5. PaySimple tells integrators to keep the key encrypted on the server and never send it to a browser ([Authorization](https://documentation.paysimple.com/docs/authorization)).

### 2.4 Endpoints and fields we need

Everything below is already called by `PaySimple.PaySimpleApi`, except the rows marked **new**.

| Purpose | Method and path | Fields that matter | Reference |
|---|---|---|---|
| Create customer | `POST /v4/customer` | `FirstName`, `LastName` (each up to 100), `ShippingSameAsBilling`, `CustomerAccount` (up to 28: we send `PatNum: n`) | [New Customer](https://documentation.paysimple.com/reference/new-customer) |
| Add card | `POST /v4/account/creditcard` | `CustomerId`, `CreditCardNumber`, `ExpirationDate` (`MM/yyyy`), `Issuer`, `BillingZipCode`, `IsDefault` → `Id` (our `PaySimpleToken`) | [New Credit Card](https://documentation.paysimple.com/reference/new-credit-card) |
| Add bank account | `POST /v4/account/ach` | `CustomerId`, `RoutingNumber`, `AccountNumber`, `BankName`, `IsCheckingAccount`, `IsDefault` → `Id` | [New ACH](https://documentation.paysimple.com/reference/new-ach) |
| Delete card or bank account | `DELETE /v4/account/creditcard/{id}`, `DELETE /v4/account/ach/{id}` | 204 on success | [Delete Credit Card](https://documentation.paysimple.com/reference/delete-credit-card), [Delete ACH](https://documentation.paysimple.com/reference/delete-ach) |
| Charge | `POST /v4/payment` | `AccountId`, `Amount`, `CVV`, `PaymentSubType` (card: `Moto`/`Swipe`; ACH: `Web`/`Tel`/`Ppd`/`Ccd`), `OrderId` (up to 50, **new**: local correlation ID), `CardOnFileType` / `NetworkTransactionId` (see open question 3) → `Id`, `Status`, `ProviderAuthCode`, `FailureData`, `CanVoidUntil` | [New Payment](https://documentation.paysimple.com/reference/new-payment), [Payment Response](https://documentation.paysimple.com/reference/payment-object) |
| Void | `PUT /v4/payment/{id}/void` | Only before the batch cut-off (`CanVoidUntil`). ACH only while Posted, typically until 5 PM Eastern on that date | [Void Payment](https://documentation.paysimple.com/reference/void-payment) |
| Refund a settled payment | `PUT /v4/payment/{id}/reverse` | Original becomes `Reversed`. A new credit is created (`ReversePosted` for ACH); `ReferenceId` links the two | [Reverse Payment](https://documentation.paysimple.com/reference/reverse-payment) |
| **New:** payment status | `GET /v4/payment/{id}` | `Status`, `ActualSettledDate`, `ReturnDate`, `FailureData{Code,Description,MerchantActionText,IsDecline}` | [Payment](https://documentation.paysimple.com/reference/payment) |
| **New:** late-return sweep | `GET /v4/payment?status=Returned,ReverseNSF,ChargeBack&startdate=YYYY-MM-DD&lite=true&page=n&pagesize=200` | Comma-separated status filter, date range (`YYYY-MM-DD`; on the customer-payments route the dates filter on creation date), paging (default 200, no documented maximum). The help page's own date examples use `startDate` and the plural `/v4/payments`, so confirm the exact spelling in the sandbox | [API 4 help](https://sandbox-api.paysimple.com/v4/help) |
| **New:** reconcile after a timeout | `GET /v4/customer/{customerId}/payments` | Find a payment with our `OrderId` | [Customer payments](https://documentation.paysimple.com/reference/customercustomeridpayments) |
| **New:** credential test | `GET /v4/customer?page=1&pagesize=1` | 200 means the credentials work; 401 means they don't | [List Customers](https://documentation.paysimple.com/reference/list-customers) |
| Webhook subscriptions (review and remove only) | `GET /ps/webhook/subscriptions`, `DELETE /ps/webhook/subscription/{id}` | `id`, `url`, `event_types`, `is_active` | [Webhooks](https://documentation.paysimple.com/docs/webhooks) |

**ACH statuses and how we map them.** Status values are from [Payment Statuses](https://documentation.paysimple.com/reference/payment-statuses).

| PaySimple `Status` | Meaning (paraphrased) | Local `PaymentStatus` |
|---|---|---|
| `Pending`, `Posted` | Entered, not yet settled | stays `PaySimpleAchPosted` |
| `Settled` | Funds collected | `PaySimpleAchSettled` |
| `Returned`, `ReverseNSF`, `ChargeBack`, `Failed` | Bank return, insufficient funds, dispute, never completed | `PaySimpleAchFailed`, plus a note and an alert |
| `Voided` | Cancelled before the batch | `PaySimpleAchFailed`, with the note "voided at PaySimple" |
| `Reversed` (original), `ReversePosted` / `RefundSettled` (the credit) | Refunded after settlement | stays `PaySimpleAchSettled`, with the note "refunded at PaySimple" |
| `Authorized`, `RequiresAction`, or anything unknown | Card-only states; not expected for an ACH payment | unchanged, and logged |

**Webhooks (documented, but not used in v1).**

- PaySimple POSTs small JSON events (`event_type`, `event_id`, `merchant_id`, `data.payment_id`, `data.payment_status`, return or failure codes) to a public URL ([Payment Events](https://documentation.paysimple.com/docs/payment-events)).
- Each event is signed in the `paysimple-hmac-sha256` header: an uppercase-hex HMAC-SHA256 of the body, keyed with the subscription's `signature_secret`.
- The receiver must answer with a 2xx within 10 seconds. Otherwise PaySimple retries hourly for 72 hours, and events can arrive out of order ([Webhooks](https://documentation.paysimple.com/docs/webhooks)).
- A receiver would have to be reachable from the internet, which is why v1 polls instead (section 3.4).

### 2.5 Rate limits

PaySimple publishes no rate limits for API 4 or the webhook API. Neither the reference, the FAQ nor the help page mentions any. The design is conservative:

- calls from one process run one at a time
- the poller makes at most about 1 request per second
- GETs back off on 429 or 5xx
- POSTs are never retried (section 3.6)

### 2.6 Terms that matter

- **API Terms of Use** ([paysimple.com/api-terms-of-use](https://paysimple.com/api-terms-of-use/), modified 2026-03-30):
  - A sandbox key comes first. A production key is issued after PaySimple reviews and approves the integration, at its discretion.
  - Keys are confidential, and PaySimple can revoke them at any time.
  - The licensee must comply with PCI DSS, card-brand, surcharging and NACHA ACH rules.
  - No rights to PaySimple marks are granted. The logo button images therefore go (section 4).
  - PaySimple may change or retire API versions, and integrations must keep up.
- **Terms of Service and Acceptable Use Policy** ([Terms of Service](https://paysimple.com/terms-of-service/), [AUP](https://paysimple.com/acceptable-use-policy/)): the merchant certifies PCI compliance every year and follows the NACHA rules for ACH.
- **PCI scope.** PaySimple's security page says that sending raw card data through your own systems requires SAQ D. Using the PaySimpleJS iframe can qualify for SAQ A, but only for merchants under 20,000 card transactions a year who also run quarterly ASV scans of the web server hosting the iframe ([Security and Compliance](https://documentation.paysimple.com/docs/security-and-compliance)). Today the desktop app keys card and bank numbers itself, so the office is in SAQ D scope. Open question 10 covers PaySimpleJS.
- **ACH authorizations.** The code sends `PaymentSubType=PPD` (a prearranged payment, which needs the patient's written authorization). The office must keep those authorizations under NACHA rules.
- **Partner agreement: not needed.** Reseller keys are for partners who act for many merchants, and most partners use one reseller key across them ([Authorization](https://documentation.paysimple.com/docs/authorization)). Each office uses its own merchant key with the `basic` scheme. PaySimple also publishes an [Integrated Partner Program guide](https://pages.paysimple.com/rs/982-FCR-503/images/Integrated%20Partner%20Program%20Guide%20-%20v1.0.pdf) (PDF). The file exists, but its text could not be extracted during this review, so nothing here depends on it. The public developer docs describe no partner-only gate for a merchant using its own key.
- **PHI and BAA.** PaySimple's developer docs say nothing about HIPAA or BAAs. HHS's 2013 Omnibus Rule preamble says the HIPAA rules, including the business-associate provisions, don't apply to financial institutions for the payment-processing activities in HIPAA §1179 ([78 FR 5566](https://www.federalregister.gov/d/2013-01073)). We send only what payment needs:
  - patient first and last name
  - `CustomerAccount = "PatNum: n"`
  - amount and card or bank details

  We must never put procedure or clinical text in `Description`. The office should confirm with its own counsel; this spec isn't legal advice.

## 3. Desktop design

### 3.1 Auth flow on the desktop

- **No interactive flow.** No system browser, loopback redirect, PKCE or device code is needed, because PaySimple has no OAuth. The office pastes the API username and key into the PaySimple Setup window once per clinic.
- `PaySimple.GetAuthHeader(clinicNum)` builds `basic {user}:{key}` for both API 4 and webhook calls. The PSSERVER builder (`PaySimpleApi.GetAuthHeader`, with its local-time timestamp and HMAC) is deleted, which removes the clock-skew failure.
- If sandbox testing shows an account that still rejects `basic` on `/v4`, keep the PSSERVER builder as a fallback behind the hidden property `PaySimpleUseLegacyAuth` (open question 4). Otherwise don't add it.
- The **Test connection** button and Save call `GET /v4/customer?page=1&pagesize=1` for each clinic that has credentials. A 401 shows the existing "Invalid PaySimple credentials" message for that clinic.

### 3.2 Where credentials and settings live

All settings are `programproperty` rows on the PaySimple program. ClinicNum 0 holds the default; clinic rows override it, through the existing `GetPropValForClinicOrDefault`. No schema change and no ConvertDatabases step. Rows missing on older databases are inserted at runtime by `FormPaySimpleSetup.AddNeededProgramProperties`, and by a new `PaySimple.EnsureProperties()` called before background use.

| PropertyDesc | Scope | Value | Status |
|---|---|---|---|
| `PaySimple API User Name` | per clinic | plain text (an identifier, not a secret) | existing |
| `PaySimple API Key` | per clinic | **`osd1$` ciphertext** via `CDT.Class1.TryEncrypt` on save; read with `TryDecrypt`. `TryDecrypt` passes plain-text legacy values through unchanged, so they keep working until the next save | existing; becomes encrypted |
| `PaySimple Payment Type CC`, `PaySimple Payment Type ACH` | per clinic | `definition.DefNum` | existing |
| `PaySimplePreventSavingNewCC`, `PaySimplePrintReceipt` | per clinic | `0`/`1` | existing |
| `PaySimpleIsOnlinePaymentsEnabled` | per clinic | kept for schema compatibility and forced to `0` on save; checkbox hidden (Payment Portal is an eService) | existing |
| `PaySimple Environment` | per clinic | `Production` (default) or `Sandbox`. Replaces the `ODBuild.IsDebug()` switch in `GetApiUrl`. The `Introspection` override still wins when set | **new** |
| `PaySimpleAchStatusCheck` | ClinicNum 0 | `1` (default once PaySimple is enabled) or `0` | **new** |
| `PaySimpleAchStatusLease` | ClinicNum 0 | `<UTC ISO-8601>|<computer name>` of the last poll run (section 3.4) | **new** |

Notes:

- The `osd1$` format uses a key that is public in the repo's source (`CDT/README.md`). It keeps the key out of plain-text dumps and screenshots, but anyone with both database access and the source can reverse it. That matches every other stored secret in this build.
- `FormPaySimpleSetup.SynchWithHQ` compares the default clinic's credentials with each clinic's. Here "HQ" means the "Headquarters" clinic (ClinicNum 0), not Open Dental HQ. The comparison stays correct with encryption because `osd1$` is deterministic: equal plain text gives equal ciphertext. The form keeps the list values encrypted and only decrypts into the text box.
- The Key text box gets `UseSystemPasswordChar=true`.
- `AddNeededProgramProperties` only runs when clinics are enabled, only for the user's clinic rows (never ClinicNum 0), and only adds to the in-memory list, which is persisted by `ProgramProperties.Sync` on Save. Several of its existing branches dereference `FirstOrDefault(... ClinicNum==0).PropertyValue` without a null check. The new rows must use `?.PropertyValue ?? <default>`. The ClinicNum 0 rows (`PaySimple Environment`, `PaySimpleAchStatusCheck`, `PaySimpleAchStatusLease`) must be created by `PaySimple.EnsureProperties()`, not by the form.

### 3.3 Refresh handling

None is needed. The credentials don't expire and there are no tokens. If PaySimple rotates or revokes a key, the next call returns 401. The office then re-enters the key in Setup. For the poller, see section 3.5.

### 3.4 ACH status sync (replaces HQ's webhook relay)

The new class `OpenDentBusiness/WebBridges/PaySimpleAchStatusSync.cs` has one entry point, `Run(bool isManual)`.

1. **Gate.** Return if the PaySimple program is disabled, or if `PaySimpleAchStatusCheck` is `0` and the run isn't manual.
2. **Lease.** This keeps several workstations from polling at once.
   - Read `PaySimpleAchStatusLease`. If another computer wrote it less than 55 minutes ago and the run isn't manual, return.
   - Otherwise write the current UTC time and `ODEnvironment.MachineName`.
   - A race between two workstations is harmless, because every update below is idempotent.
3. **Candidates.** The new `Payments.GetPaySimpleAchToTrack(DateTime dateFrom)` is Middle Tier aware. It selects payment rows where:
   - `ExternalId` is not empty
   - `PaymentStatus` is `PaySimpleAchPosted`, or `PaySimpleAchSettled` with `PayDate` within the last 75 days

   Don't filter on `PaymentSource`. `PaymentStatus` is only ever set for PaySimple ACH. The prepaid branch of `FormPayment.MakePaySimpleTransaction` sets `PaymentStatus` and `ExternalId` but never sets `PaymentSource`, so a `PaymentSource` filter would miss those rows.

   The 75 days cover NACHA's 60-day window for consumer unauthorized-debit returns, which is counted from settlement, with some margin.
4. **Group by credentials.** Group by `payment.ClinicNum`, resolve each clinic's username, key and environment, and skip clinics without credentials, logging the skip. The charge used `_payment.ClinicNum` (`FormPayment.MakePaySimpleTransaction`), so this matches unless a user later moves the payment to a clinic with a different merchant account. In that case the GET returns 400 "not found". Log it and leave the status alone.
5. **Posted payments.** One `GET /v4/payment/{ExternalId}` per payment, about 1 per second, mapped as in section 2.4.
6. **Settled payments.** Per credential set, one paged `GET /v4/payment?status=Returned,ReverseNSF,ChargeBack&startdate=<today-75>&lite=true`. Match each returned `Id` to `ExternalId`.
7. **Apply.** For each payment whose status changed:
   - Re-read the payment and continue only if its status is still the old value.
   - Set `PaymentStatus`.
   - Append one line to `PayNote`, e.g. "PaySimple ACH settled 2026-10-21", or "PaySimple ACH Returned: <FailureData.Description> (<Code>)".
   - Write a `SecurityLogs` entry (`PaymentEdit`, LogSources.None).
   - On a failure, add an `AlertItem` (`AlertType.Generic`, the payment's clinic) telling staff to review the payment.
   - **Never** change `PayAmt`, paysplits or deposits automatically. As before, staff correct the account by hand.
8. **Where it runs.** A new hourly `ODThread`, `BeginPaySimpleAchStatusThread`, in `OpenDental/Main Modules/FormOpenDentalThreads.cs` (new `FormODThreadNames.PaySimpleAchStatus`), started only when PaySimple is enabled. Manual runs:
   - a **Check ACH statuses now** button in PaySimple Setup
   - a **Check ACH status** item in `FormPayment`'s PaySimple context menu, for the open payment only
9. **Display.** `FormPayment` shows a read-only "ACH status: Posted/Settled/Failed" label next to the PaySimple button when `PaymentStatus` isn't `None`.

Recurring ACH charges are tracked too. `RecurringCharges.CreatePayment` gets an optional `externalId` parameter. For ACH sources, `SendPaySimple` passes `response.RefNumber` and sets `PaymentStatus=PaySimpleAchPosted`. Today recurring ACH payments are created without either, so neither HQ nor the poller could ever track them.

Latency is up to an hour, versus near real time with webhooks. That's acceptable: ACH settles in days. If no workstation is open, nothing updates until one is.

### 3.5 Errors and offline behaviour

- **No network.** The transaction window shows the error, and nothing is stored or queued. Charges are never queued for later. The poller logs the error and tries again next hour; payments stay Posted, which is safe.
- **401.** Interactive calls show the existing message. The poller logs the error and raises at most one `AlertItem` per clinic per day ("PaySimple credentials rejected; re-enter the key in PaySimple Setup").
- **400 with `Meta.Errors`.** Existing `HandleWebException` → `PaySimpleException`, including the existing `CustomerDoesNotExist` recovery.
- **429 or 5xx on a GET.** Back off 2, 4 and 8 seconds, then end this run.
- **Timeout or dropped connection on `POST /v4/payment`.** The result is unknown, so never auto-retry.
  - Every charge sends `OrderId = "OPMS-" + Guid` (the prefix is ours).
  - After a timeout, call `GET /v4/customer/{customerId}/payments` and look for that `OrderId`.
  - If it's found, treat the payment as a success.
  - Otherwise tell the user the result is unknown and to check PaySimple's web portal before charging again.
  - Recurring charges mark the row declined, with that text in the note.
- **PaySimple outage.** Core billing is unaffected. Payments can still be entered with other payment types (ground rule 5).

### 3.6 Hosts contacted and HQ removal

- Only `api.paysimple.com` or `sandbox-api.paysimple.com` are contacted, or the `PaySimpleApiURL` introspection override.
- No Open Dental host is contacted, and no HQ call remains on any PaySimple path.
- `InsertPaySimpleACHId` and `GetPaySimpleWebHookUrl` stay on `IWebServiceMainHQ` / `WebServiceMainHQNoHq` as stubs until the Phase 3 clean-up. They then go, together with the `NoHqTests.cs:70` line that exercises `GetPaySimpleWebHookUrl`. No test calls `InsertPaySimpleACHId`.

**Stale webhook subscriptions.** An office migrating from Open Dental will usually still have subscriptions in its PaySimple account that deliver its payment events (payment IDs, amounts, customer IDs) to Open Dental's receiver.

- Setup gets a **Webhook subscriptions...** button. It lists `GET /ps/webhook/subscriptions` (URL, event types, active) and offers **Remove** for the selected rows (`DELETE /ps/webhook/subscription/{id}`) after confirmation.
- Nothing is removed automatically, because another product could own a subscription.
- Save no longer creates subscriptions. If any exist, it shows a one-line note pointing to the button.

### 3.7 Vendor neutrality (ground rule 7)

- Delete `linkLabel1` (the partner-referral URL) and its `ODCloudClient.IsAppStream` branch.
- Replace the `PaySimple_Button_2019_26x75` logo with a plain text button "PaySimple" in `FormPayment` and `FormClaimPayEdit`. The API Terms grant no rights to PaySimple's marks.
- Remove the "if none enabled, show all so user can pick" branches: `FormPayment.cs:1230-1236`, `FormClaimPayEdit.cs:175-183`. Those branches cover all processors, so coordinate with the PayConnect, XCharge and EdgeExpress specs.

## 4. Code changes

**Bridges.dll: none.** PaySimple doesn't touch the prebuilt Bridges.dll. `PaySimple.cs`, `FormPaySimple*.cs` and `FormPaySimpleSetup*.cs` are all in-repo source, so nothing needs re-implementing.

**"Not available" stubs replaced:**

| Stub | Replacement |
|---|---|
| `GetPaySimpleWebHookUrl()` (FormPaySimpleSetup.cs:322) | No webhook creation. Credential test plus subscription review (3.6) |
| `InsertPaySimpleACHId(...)` (FormPaySimple.cs:381-389) | Deleted. `payment.ExternalId` is already stored and the local poller (3.4) uses it |
| eConnector write-back of Settled/Failed (outside the repo) | `PaySimpleAchStatusSync` |

**Files and members:**

| File | Member | Change |
|---|---|---|
| `OpenDentBusiness/WebBridges/PaySimple.cs` | `GetAuthHeader(long,bool)` | Decrypt the key with `CDT.Class1.TryDecrypt`; always use `basic user:key`; drop `isWebhook`. **Delete the `ODBuild.IsDebug()` block (lines 294-297) and the commented-out sandbox username and key on lines 295-296**, inherited from upstream. It is already in git history. Treat it as Open Dental's burned sandbox credential: never use it, and don't copy it into tests or docs |
| same | `PaySimpleApi.GetAuthHeader`, `GetWebhookAuthHeader` | Merge into one `basic` builder; remove the PSSERVER HMAC (keep only if open question 4 needs it) |
| same | `PaySimpleApi.GetApiUrl` | Pick the base URL from `PaySimple Environment`, not `ODBuild.IsDebug()`; keep the Introspection override |
| same | `PaySimpleApi.Request<T>` | Explicit 30 s timeout (`WebClient` has no timeout property, so override `GetWebRequest` in a small subclass or use `HttpWebRequest` throughout); GET-only backoff on 429/5xx; on a POST timeout, throw a new `PaySimpleResultUnknownException` carrying the `OrderId` |
| same | `PaySimpleApi.MakeNewPaymentData`, `MakeNewPaymentACHData` | Add `OrderId`. Add `CardOnFileType` and `NetworkTransactionId` only once open question 3 is settled |
| same | `GetPayment` → **new** `GetPaymentStatus(long clinicNum,string paymentId)` | Return `Status`, `ActualSettledDate`, `ReturnDate`, `FailureData` (also add `ReturnDate` to the anonymous response type) |
| same | **new** `ListPayments(long clinicNum,string[] statuses,DateTime start,int page)`, `FindPaymentByOrderId(long clinicNum,long customerId,string orderId)`, `TestCredentials(long clinicNum)`, `DeleteWebhook(long clinicNum,string id)`, `EnsureProperties()` | As in section 3 |
| same | `PostWebhook`, `PaySimpleApi.PostWebhook`, `MakeNewWebhookData` | Remove (no caller left), or keep unused for a future self-hosted receiver |
| same | `PropertyDescs` | Add `PaySimpleEnvironment`, `PaySimpleAchStatusCheck`, `PaySimpleAchStatusLease` |
| **new** `OpenDentBusiness/WebBridges/PaySimpleAchStatusSync.cs` | `Run(bool)`, `MapStatus(string)` (pure, unit-tested) | Section 3.4 |
| `OpenDentBusiness/Data Interface/Payments.cs` | **new** `GetPaySimpleAchToTrack(DateTime)` | Middle Tier aware SELECT (section 3.4) |
| `OpenDentBusiness/Data Interface/RecurringCharges.cs` | `SendPaySimple`, `CreatePayment` | Pass the ACH `ExternalId` and set `PaymentStatus=PaySimpleAchPosted` |
| `OpenDentBusiness/Data Interface/ProgramProperties.cs` | `GetPaySimpleOnlinePaymentCreds` | Decrypt with `TryDecrypt`, or delete (eService-only, no desktop caller) |
| `OpenDental/Forms/FormPaySimpleSetup.cs` (+ `.designer.cs`) | `FillFields`, `butSave_Click`, `comboClinic_SelectionChangeCommitted`, `AddNeededProgramProperties` | Encrypt on save, decrypt on fill; insert the new rows; environment combo; hide `checkAllowOnlinePayments`; mask `textKey` |
| same | `WebhookHelper` | Replace with `TestCredentials` plus the webhook-subscription review dialog |
| same | `linkLabel1_LinkClicked`, `linkLabel1` | Delete |
| same | `butSave_Click` | Drop the no-op `Programs.IsEnabledByHq` check (optional) |
| same | new buttons | **Test connection**, **Check ACH statuses now**, **Webhook subscriptions...** |
| `OpenDental/Forms/FormPaySimple.cs` | `ProcessPaymentACH` | Delete the `InsertPaySimpleACHId` try/catch; handle `PaySimpleResultUnknownException` in both card and ACH paths |
| `OpenDental/Forms/FormPayment.cs` (+ `.designer.cs`) | processor-button visibility block (≈1200-1340), `MakePaySimpleTransaction`, `contextMenuPaySimple` | Remove the show-all branch; text button; ACH status label; **Check ACH status** menu item |
| `OpenDental/Forms/FormClaimPayEdit.cs` (+ `.designer.cs`) | visibility method (≈175) | Remove the show-all branch; text button |
| `OpenDental/Properties/Resources.resx`, `Resources.Designer.cs`, `OpenDental.csproj` | `PaySimple_Button_2019_26x75`, `PaySimple_Button.png` | Remove the logo resources once unused |
| `OpenDental/Main Modules/FormOpenDentalThreads.cs` | **new** `BeginPaySimpleAchStatusThread`; `FormODThreadNames.PaySimpleAchStatus` | Hourly thread |
| `OpenDentBusiness/WebServices/IWebServiceMainHQ.cs`, `WebServiceMainHQNoHq.cs`, `NoHqTests/NoHqTests.cs:70`, `OpenDentBusiness/WebTypes/Payments/PaySimpleStatus.cs` | | Unchanged now; delete in Phase 3 |

## 5. Credentials the office must register

There are no OAuth app registrations, redirect URIs or developer-portal apps. The checklist:

1. **A PaySimple merchant account** with the transaction types it needs (cards, ACH, or both). An office coming from Open Dental already has one.
2. **Ask PaySimple about the integration, before going live.** Write to partnersupport@paysimple.com (named on the [welcome page](https://documentation.paysimple.com/docs/welcome)):
   - The office is changing software: it will call API 4 directly with its own merchant key from self-hosted, open-source practice software.
   - Ask whether PaySimple requires an integration review under its [API Terms](https://paysimple.com/api-terms-of-use/), and whether the existing production key may be used with it.
   - Ask whether the account is tied to Open Dental's partner or reseller relationship (open question 2), and whether any reseller-level webhook still sends the office's events to Open Dental.
3. **Sandbox credentials**, for testing (section 6). Request a Sandbox 4.0 API key with the transaction types the office uses. PaySimple support returns:
   - a sandbox API username and key
   - a login for `sandbox-payments.paysimple.com`

   Channels: phone, per [Sandbox](https://documentation.paysimple.com/docs/sandbox), or care@paysimple.com, per the [FAQ](https://documentation.paysimple.com/docs/faq).
4. **Production credentials, one set per merchant account.** Each clinic that has its own PaySimple merchant account needs its own set. The authorized signer requests them from PaySimple Customer Care ([Production](https://documentation.paysimple.com/docs/production)), and they arrive by encrypted email. Don't forward them by plain email or put them in tickets.
5. **Enter them.** Setup → Program Links → PaySimple: pick the clinic, enter the username and key, choose payment types and the environment, then **Test connection** → Save.
6. **Review webhook subscriptions.** Setup → **Webhook subscriptions...** Remove any whose URL isn't a system the office controls, which includes subscriptions created by Open Dental.
7. **Compliance paperwork. The office owns this, not the software:**
   - the annual PCI self-assessment (expect SAQ D while card numbers are keyed into the desktop) and PaySimple's AUP certification
   - signed NACHA authorizations for every patient ACH (PPD) debit
   - a HIPAA review by the office's own counsel. No BAA is expected for payment processing under §1179 (section 2.6), but confirm it.
8. **Not needed:** a reseller key, the Integrated Partner Program, an Open Dental registration key or the eConnector.

## 6. Test plan

### 6.1 Sandbox setup

- Sandbox account and login as in section 5, step 3.
- `PaySimple Environment = Sandbox` on a test clinic, in a **test copy** of the database.
- Test values from PaySimple's [Sandbox page](https://documentation.paysimple.com/docs/sandbox):
  - Cards (CVV2 996): Visa `4111111111111111`, MasterCard `5454545454545454`, Amex `371449635398456`, Discover `6011000995504101`.
  - The listed expiry, 12/2023, is in the past, and `MakePayment` rejects past years. Use a future expiry and confirm the sandbox accepts it.
  - Bank: routing `307075259` or `325272034`, account `751111111`, "PaySimple Bank".
- Declines. Amounts trigger them, and the amounts depend on the processor ([Generating Test Errors](https://documentation.paysimple.com/reference/generating-test-errors)):
  - TSYS cards: 9999.01 to 9999.05 and 9999.30
  - Forte ACH: 9999.01 to 9999.07 and 9999.12
- Settlement. `PUT /v4/MarkPaymentStatus {"PaymentId":n}` marks a sandbox payment Settled at once, or wait about three business days ([Reverse Payment](https://documentation.paysimple.com/reference/reverse-payment)).

### 6.2 Manual checks

1. **Setup.**
   - Wrong key → **Test connection** shows the invalid-credentials message.
   - Right key → OK.
   - After Save, `programproperty` holds `osd1$...` for the key and plain text for the username.
   - Reopening the form shows the key masked.
   - A database copied from Open Dental with a plain-text key still works before re-saving.
2. **Clinics.**
   - Two clinics with different sandbox keys. Each charges against its own account (check the sandbox UI).
   - Change the default clinic's key while another clinic shares it: `SynchWithHQ` carries the change over.
3. **Card sale.**
   - New patient: a customer is created with `CustomerAccount` = `PatNum: n`, the card is stored, and the payment is approved.
   - Receipt prints.
   - `creditcard.PaySimpleToken` is set, and so is the `patientlink` row.
4. **Saved card sale; Auth only; Void the same day; Return on the next day** (after MarkPaymentStatus). Check the receipt text, payment note and amounts for each.
5. **Decline** (TSYS amount): the PaySimple description and merchant action text are shown, and no payment is saved.
6. **ACH sale.**
   - The payment is saved with `PaymentStatus=1` (Posted) and `ExternalId` set.
   - **No** "Unable to register for ACH Settled event" dialog appears.
   - The status label reads Posted.
7. **ACH settle.**
   - MarkPaymentStatus Settled, then **Check ACH statuses now**: status becomes 2, the note line is added, and there's a security log entry.
   - A second run changes nothing (idempotent).
8. **ACH failure.**
   - A Forte decline amount → Failed: status 3, note with reason, alert shown.
   - `PayAmt` and splits are unchanged.
9. **Late return.** If the sandbox can produce Returned or ReverseNSF on a settled payment (open question 9), the sweep marks it Failed. Otherwise, test with a unit test using the mock.
10. **Recurring charges.** A card token and an ACH token both charge. The ACH payment gets `ExternalId` and Posted, and the poller picks it up.
11. **Delete a stored card or bank account** in `FormCreditCardEdit`: it's gone in the sandbox UI.
12. **Prepaid insurance card** in `FormClaimPayEdit`: an UNKNOWN customer is created, and the charge is approved.
13. **Webhook review.**
    - Create a dummy subscription to `https://example.invalid/hook` with Postman.
    - **Webhook subscriptions...** lists it; Remove deletes it.
    - Save creates nothing.
14. **Lease.** Two workstations open: only one polls per hour (check the lease value and logs). A manual run works from either.
15. **Offline.**
    - Block `sandbox-api.paysimple.com`. A sale shows a network error and nothing is saved. The poller logs the error and statuses are unchanged.
    - Unblock: the next run catches up.
16. **POST timeout** (point the Introspection URL at a local proxy that holds the response). The user gets "result unknown". If the proxy then lets the request through, the `OrderId` lookup finds the payment.
17. **Neutrality.**
    - With PaySimple disabled, no PaySimple button appears in Payment or Claim Pay, and no link appears in Setup.
    - Enabled, the buttons show text, not a logo.
18. **No HQ.** Phase 1 guard script and `NoHqTests` pass. Traffic capture during all of the above shows only `sandbox-api.paysimple.com`.

### 6.3 Automated tests (no network)

- `PaySimpleAchStatusSync.MapStatus`: every status in section 2.4, including unknown values, which leave the status unchanged.
- Auth header: exactly `basic user:key`, with no Base64.
- Key round trip: `TryEncrypt` → `TryDecrypt`; plain-text pass-through.
- Poller against an extended `PaySimple.MockPaySimple` (canned GET payment and list responses): status transitions, the note text, no change to `PayAmt`, and idempotence.
- `GetApiUrl`: the environment property and the Introspection override.
- Existing `NoHqTests` still pass, unchanged.

## 7. Size, risks and open questions

**Size: M.** About 3 to 5 developer days:

- auth, encryption and environment: 0.5 day
- setup UI, webhook review and neutrality: 1 day
- poller, thread and recurring ACH: 1.5 days
- tests: 1 day

Plus elapsed time waiting for PaySimple's sandbox and answers.

**Risks**

- **PaySimple approval.** The API Terms let PaySimple review integrations and revoke keys. It could refuse or delay production use with this fork. Mitigation: contact partner support first (section 5, step 2). Until then, the integration stays configurable but disabled.
- **Event data still going to Open Dental.** Merchant-level webhook subscriptions, and possibly reseller-level ones the office can't see, may keep sending (PaySimple's [Webhooks](https://documentation.paysimple.com/docs/webhooks) page says a reseller key can subscribe to events for all of its merchants) the office's payment events to Open Dental. Mitigation: the review dialog, plus asking PaySimple to detach the account.
- **PCI scope.** This is existing behaviour: card and bank numbers pass through the desktop process, so the office is in SAQ D scope. Card numbers are never stored locally (only masked numbers and PaySimple account IDs), but they are in memory and on screen.
- **Duplicate charges on timeouts.** There's no documented idempotency key. Mitigation: `OrderId` plus a lookup, never auto-retrying, and an explicit "result unknown" message.
- **Weak key protection.** `osd1$` uses a public key, so it's obfuscation, not secrecy against someone with database access.
- **Latency and coverage.** Hourly polling only runs while a workstation is open. The 75-day sweep window could miss very late chargebacks, which PaySimple's portal and email still report.
- **API 4 lifecycle.** API 4 is "legacy, fully supported", but the terms allow retirement. The calls are few and isolated in `PaySimpleApi`, so moving to API 5 later would be contained.
- **Concurrent edits.** The poller appends to `PayNote` while a user may have the payment open. Mitigation: re-read before update and append only. A stale `FormPayment` save could overwrite the note line, but the status column survives, because CRUD `Update(new,old)` writes only changed fields.

**Open questions**

1. Does PaySimple require a review of this fork before an existing production key may be used with it? Ask partner support.
2. Was the office's merchant account boarded through Open Dental's partner or reseller relationship (the `paysimple.com/partner/open-dental` link)? If so:
   - Does Open Dental still have reseller-key access to the account's data or events?
   - Do pricing or support change if the office leaves that relationship?
3. **Card-on-file fields.** [New Payment](https://documentation.paysimple.com/reference/new-payment) now marks `CardOnFileType` as required for every card-on-file charge. The values are `None`, `MerchantInitiated`, `CustomerInitiated`, `Recurring` and `Installment`. It also says `NetworkTransactionId` is required whenever `CardOnFileType` is anything other than `None`. That ID comes back on the first successful transaction and must be stored and replayed ([Payment Response](https://documentation.paysimple.com/reference/payment-object)). The 24.3 code sends neither field.
   - Sending `CardOnFileType` alone, without `NetworkTransactionId`, contradicts the docs as written and will probably be rejected. Don't plan on it.
   - Does PaySimple's vault fill these in for stored accounts, or still accept stored-account charges that send neither field? Ask partner support and test in the sandbox.
   - If the ID must be stored, where would it live without a schema change? There's no free column on `creditcard`. This needs an owner decision on a schema addition.
   - Interim: keep today's behaviour (send neither field). If the sandbox rejects that, card-on-file and recurring PaySimple card charges stay disabled until the owner decides. One-time keyed sales are unaffected.
4. Does `basic user:key` work on every `/v4` endpoint for every account? The docs say API 4 accepts the API 5 schemes. Verify in the sandbox before deleting PSSERVER.
5. The code sends `Issuer` as integers (12 to 15), but the docs list strings (`Visa`, `Master`, `Amex`, `Discover`). Verify in the sandbox, and switch to strings if the integers are rejected.
6. ACH `PaymentSubType`: the code always sends `PPD`. Phone-authorized debits arguably need `Tel`, and PaySimple's default is `Web`. This is a compliance choice for the office. A per-transaction option would be a later UI change.
7. Rate limits are unpublished. Ask PaySimple, and set the poller pace to match.
8. Did the office's Open Dental 25.3 database (`fox-db`) store the PaySimple key encrypted by Open Dental? Ground rule 3 says the owner reports no encrypted data, and 24.3 stores this key in plain text. If 25.3 did encrypt it, though, `TryDecrypt` passes the ciphertext through, auth fails, and the key must be re-entered after migration (`CDT/README.md`). Add a check to the Phase 5 re-entry checklist.
9. Can the sandbox simulate ACH returns (Returned or ReverseNSF on a settled payment)? Only MarkPaymentStatus Settled is documented.
10. Later, to reduce PCI scope: should card and bank entry move to PaySimpleJS?
    - The flow: a checkout token from `POST /v4/checkouttoken`, then PaySimple's iframe inside a WebView2 page ([PaySimpleJS guide](https://documentation.paysimple.com/docs/paysimplejs-getting-started)).
    - That could qualify for SAQ A. It's a larger UI change. It also needs confirmation that a local (non-web-origin) host page is supported, and how PaySimple's quarterly ASV scan condition applies when no web server hosts the iframe.
11. Should a self-hosted webhook receiver (signature check on `paysimple-hmac-sha256`) be offered later as an optional module, for offices that already expose an HTTPS endpoint? Out of scope for v1, given ground rule 6.

## References

- PaySimple docs: [Welcome](https://documentation.paysimple.com/docs/welcome), [Sandbox](https://documentation.paysimple.com/docs/sandbox), [Production](https://documentation.paysimple.com/docs/production), [FAQ](https://documentation.paysimple.com/docs/faq), [Authorization](https://documentation.paysimple.com/docs/authorization), [Authentication overview](https://documentation.paysimple.com/reference/authentication-overview), [Legacy Authorization](https://documentation.paysimple.com/reference/legacy-authorization), [Security and Compliance](https://documentation.paysimple.com/docs/security-and-compliance), [Webhooks](https://documentation.paysimple.com/docs/webhooks), [Payment Events](https://documentation.paysimple.com/docs/payment-events), [Payment Statuses](https://documentation.paysimple.com/reference/payment-statuses), [New Payment](https://documentation.paysimple.com/reference/new-payment), [Payment Response](https://documentation.paysimple.com/reference/payment-object), [Get Payment](https://documentation.paysimple.com/reference/payment), [Void](https://documentation.paysimple.com/reference/void-payment), [Reverse](https://documentation.paysimple.com/reference/reverse-payment), [New Customer](https://documentation.paysimple.com/reference/new-customer), [New Credit Card](https://documentation.paysimple.com/reference/new-credit-card), [New ACH](https://documentation.paysimple.com/reference/new-ach), [Generating Test Errors](https://documentation.paysimple.com/reference/generating-test-errors), [PaySimpleJS](https://documentation.paysimple.com/docs/paysimplejs-getting-started), [API 4 help](https://sandbox-api.paysimple.com/v4/help)
- PaySimple legal: [API Terms of Use](https://paysimple.com/api-terms-of-use/), [Terms of Service](https://paysimple.com/terms-of-service/), [Acceptable Use Policy](https://paysimple.com/acceptable-use-policy/), [Integrated Partner Program guide](https://pages.paysimple.com/rs/982-FCR-503/images/Integrated%20Partner%20Program%20Guide%20-%20v1.0.pdf)
- HIPAA §1179: [HHS Omnibus Final Rule, 78 FR 5566 (2013)](https://www.federalregister.gov/d/2013-01073)
- Open Dental public manual, used for behaviour only: [PaySimple](https://opendental.com/manual/paysimple.html), [PaySimple Setup](https://opendental.com/manual/paysimplesetup.html)
- This repo on `main`: `OpenDentBusiness/WebBridges/PaySimple.cs`, `OpenDental/Forms/FormPaySimpleSetup.cs`, `OpenDental/Forms/FormPaySimple.cs`, `OpenDental/Forms/FormPayment.cs`, `OpenDentBusiness/Data Interface/RecurringCharges.cs`, `OpenDentBusiness/WebServices/WebServiceMainHQNoHq.cs`, `CDT/README.md`; `D:/Dev/opendental-schema/hq-vendor-table.md`; `docs/decoupling-plan.md`
