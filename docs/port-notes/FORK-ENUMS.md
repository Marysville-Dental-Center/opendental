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
- Check the width of every column the enum is stored in. 10000 needs at least `smallint` (`grouppermission.PermType` and
  `securitylog.PermType` are `smallint(6)`, at most 32767).

## tinyint enums

Some enums are stored in `tinyint(4)` columns, which hold at most 127: `AlertType` (`alertitem.Type`, `alertsub.Type`,
`alertcategorylink.AlertType`) and `FormType` (`alertitem.FormToOpen`). Widening an Open Dental column would break the schema match
with Open Dental, so their fork-only values are 100 to 127, from the top of the range Open Dental numbers upward into (it is at 51 and 21
in 26.1):

| Range   | Feature                          |
|---------|----------------------------------|
| 100-109 | Routine maintenance              |
| 110-119 | Inventory tracking               |
| 120-127 | Free; reserve a range here first |

When Open Dental's own values come within 10 of 100, move this rule before the next port.

## Values in use

| Enum | Value | Name | Feature |
|---|---|---|---|
| `EnumPermType` | 10050 | `InventoryAdjust` | Inventory tracking |
| `AlertType` | 110 | `InventoryLowStock` | Inventory tracking |
| `AlertType` | 111 | `InventoryExpiring` | Inventory tracking |
| `FormType` | 110 | `FormSupplyInventory` | Inventory tracking |
