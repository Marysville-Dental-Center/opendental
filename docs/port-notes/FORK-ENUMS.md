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
