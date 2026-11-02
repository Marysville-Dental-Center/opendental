# Fork-only enum values

Open PMS - Dental adds values to some enums whose numbers are stored in the database, for example `EnumPermType`
(`grouppermission.PermType`) and `AlertType` (`alertitem.Type`). Open Dental keeps adding values to the same enums in its own
releases, numbered upward from its last value. So that a later port of an Open Dental release never reuses one of our numbers,
fork-only values start at 10000.

| Range       | Feature                         |
|-------------|---------------------------------|
| 10000-10049 | Routine maintenance             |
| 10050-10099 | Inventory tracking              |
| 10100-      | Free; reserve a range here first |

Rules:

- A released value is never renumbered or reused.
- Each fork-only value has a comment that names its feature.
- Before adding a value, check that no code sizes an array from the enum's count, indexes by the value or treats the enum as flags.
  If a high value is unsafe for one enum, say so here and give that enum its own rule.
- Check the type of every column that stores the enum. 10000 needs at least `smallint`.

`OpenPmsDentalBusiness.ForkEnums` holds the first fork-only value of each kind (`FIRST_VALUE`, `FIRST_VALUE_TINYINT`) and tells
whether a value is fork-only (`ForkEnums.IsForkOnly`); tests use it to leave fork-only values out of the checks against Open Dental's
published numbers.

## tinyint enums: AlertType and FormType

`AlertType` is stored in `tinyint(4)` columns (`alertitem.Type`, `alertcategorylink.AlertType`, `alertsub.Type`), and so is `FormType`
(`alertitem.FormToOpen`). A `tinyint(4)` holds -128 to 127, so 10000 does not fit, and widening Open Dental's columns would make the
schema differ from Open Dental's (the schema verifier). Their fork-only values therefore count **down from 127**, while Open Dental's
count up from 0 (51 for `AlertType` and 21 for `FormType` in 26.1); 100 to 127 is the fork-only region (`ForkEnums.FIRST_VALUE_TINYINT`).

| Range (tinyint enums) | Feature                         |
|-----------------------|---------------------------------|
| 127-120               | Routine maintenance             |
| 119-112               | Inventory tracking              |
| 111-100               | Free; reserve a range here first |

Within a range, values are taken from the top down (routine maintenance: `AlertType` 127 `MaintenanceDueSoon`, 126
`MaintenanceOverdue`; `FormType` 127 `FormMaintenanceBoard`). If Open Dental's own values ever come near 100, a port must stop and
the owner decides.

## Code that was unsafe for high values (fixed with routine maintenance)

- `EnumPermType`: the security tree's Set All and Set None (`UserControlSecurityTree.SetAll`, `SetNone`) looped over `0..count-1` and
  cast the index to the enum. With fork-only values that loop skips them and visits numbers that are not defined (and that a later Open
  Dental release may define). Both now loop over `Enum.GetValues`. The tests that pinned "values 0 to n without gaps" (RegistryTests
  `EnumPermType_IsContiguous`, M202Tests `EnumPermType_EndsAt276WithoutGaps`) now check the values below 10000 only, and that every
  value from 10000 up is in a reserved range.
- `AlertType`: the Central Manager's user window (`FormCentralUserEdit`) filled and saved its alert list by index (`(AlertType)i`).
  It now uses `Enum.GetValues` in both places.
- Searched and found safe: `FormAudit` (`Enum.GetValues`), `FrmAlertCategoryEdit` (`Enum.GetValues`), the alert menu (switch on the
  value), `GroupPermissions` (no arrays), `SecurityLogs` (`smallint` column). `EnumPermType` columns (`grouppermission.PermType`,
  `securitylog.PermType`) are `smallint(6)`, which holds 10000.
