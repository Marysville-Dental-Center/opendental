# Inventory tracking for materials

Status: 2026-11-02. Package INV (roadmap "Next" item 4), from the owner decisions of 2026-11-02. It builds on Open Dental's supply inventory
(`supply`, `supplyorder`, `supplyorderitem`, `supplyneeded`, `dispsupply`, `equipment` and their windows) and replaces nothing.

## 1. What the office gets

- **One storage location.** Stock is counted per supply (`supply.LevelOnHand`) and, for supplies tracked by lot, per lot.
- **Barcode scanning** with a USB keyboard-wedge scanner (it types the code and Enter). A scan is matched against `supply.BarCodeOrID`
  (exact, ignoring case and surrounding blanks; the catalog number is the fallback when no barcode matches). Scanning is used:
  - when **receiving** an order: each scan of an item on the purchase order adds one to what is received now; lot number and expiry date are
    entered for supplies tracked by lot; posting raises stock;
  - in a **stock count**: each scan adds one to the counted quantity of the item; posting writes the differences as count adjustments with a
    reason;
  - in the **Materials used** panel: a scan adds one of the item to the selected procedure.
- **Stock goes down from completed procedures.** A procedure code has default materials (a map from procedure code to supplies with default
  quantities). When procedures are completed in the program, the **Materials used** panel opens by itself with the codes' default materials.
  The assistant confirms or changes quantities, adds items (pick list or scan) or removes them, and what is confirmed is deducted. There is no
  second sign-off.
- **Un-completing or deleting** a completed procedure puts back what it used.
- **Lots and expiry.** A supply can be tracked by lot. Each lot has a lot number and an expiry date. Deductions take the lot that expires
  first. For implant-type supplies the lot is recorded with the patient's procedure (traceability) and shown from the procedure.
- **Alerts** through the existing alert system (`alertitem`, the Alerts menu): low stock (on hand at or below the supply's reorder level) and
  lots expiring soon (within the lead days of a preference), each once.
- **Usage reports**: quantity and cost used, by item and by procedure code, for a date range.
- **A preference turns it on.** It is off for every existing database until the office turns it on. While it is off the panel never opens and
  nothing is deducted.

## 2. Data

All new data is in new tables. No column is added to an Open Dental table, so the schema verifier still compares the Open Dental tables with
the Open Dental 26.1.70 reference exactly; the new tables are listed as fork tables (`SchemaVerifier.ArrayForkTables`), which the verifier
leaves out of the comparison (they are not Open Dental schema; `InventoryTests` checks them against their classes). The tables are MyISAM and
utf8mb3 like every other table.

| Table | Class | One row is |
|---|---|---|
| `supplyledger` | `SupplyLedger` | one stock movement of one supply (and lot): a signed quantity |
| `supplylot` | `SupplyLot` | one lot of a supply: lot number, expiry date, quantity on hand |
| `supplyproccode` | `SupplyProcCode` | one default material of a procedure code: supply and quantity |
| `supplysetting` | `SupplySetting` | the tracking settings of one supply: reorder level, tracked by lot, implant |
| `supplyprocpending` | `SupplyProcPending` | a completed procedure whose materials nobody has confirmed yet |

### 2.1 The stock ledger (`supplyledger`)

Every change of stock is a ledger row; nothing else changes stock.

| Column | Meaning |
|---|---|
| `SupplyLedgerNum` | primary key |
| `SupplyNum` | the supply |
| `SupplyLotNum` | the lot, or 0 for stock without a lot |
| `LedgerType` | `SupplyLedgerType`: 0 Opening, 1 Receive, 2 Use, 3 Restore, 4 CountAdjust, 5 Adjust, 6 Dispense |
| `Quantity` | signed: receipts, restores and positive adjustments add, uses, dispenses and negative adjustments subtract. Quantities have at most 2 decimals (the CRUD writes doubles with 2) |
| `UnitCost` | cost of one unit at the time: the order item's price for a receipt, the supply's price otherwise |
| `ProcNum`, `PatNum` | the procedure and patient, for Use and Restore |
| `SupplyOrderItemNum` | the order item, for Receive |
| `DispSupplyNum` | the dispensary row, for Dispense |
| `UserNum` | who made the movement (0 for the migration) |
| `DateTimeEntry` | when (server time, set on insert) |
| `Reason` | free text; required for count adjustments and manual adjustments |

Rows are never edited or deleted (the one exception: deleting a supply that has only opening and manual adjustment rows deletes those rows
with it). A correction is a new row.

`supply.LevelOnHand` is always the sum of the supply's ledger rows, and `supplylot.QtyOnHand` the sum of the lot's ledger rows.

### 2.2 Consistency without transactions

MyISAM has no transactions, and two workstations can complete procedures at the same time. So:

1. **The ledger is written first.** Each ledger row is one `INSERT`, which MyISAM applies whole or not at all.
2. **Then the counters are recomputed from the ledger**, never incremented from a value the program read earlier:
   `UPDATE supply SET LevelOnHand=(SELECT SUM(Quantity) FROM supplyledger WHERE SupplyNum=...)`, and the same for the lot. MyISAM runs each
   statement under a table lock, so the last recompute sees every row inserted before it, whichever workstation wrote them. A recompute that
   did not run (a lost connection between the two statements) is corrected by the next movement of that supply, or by the repair.
3. **`supply.LevelOnHand` is never written by `Supplies.Update`.** The column is `ExcludeFromUpdate` in the CRUD, so a supply edited in a
   window with an old copy of the row can never overwrite stock. `Supplies.Insert` inserts 0 and writes an opening ledger row for a typed
   quantity (on a database without the inventory tables, as before).
4. **The procedure's own record is written last.** Confirming materials writes the ledger rows, recomputes, and only then deletes the pending
   row. Confirming compares what the panel says with what the ledger already holds for the procedure and writes only the difference, so
   confirming again after a failure part-way never deducts twice.
5. **Repair** (Supply Inventory, Inventory, Repair Stock from Ledger; Inventory Adjust): recomputes every supply's and every lot's quantity
   from the ledger and reports how many changed (`SupplyLedgers.RecomputeAll`, `InventoryTracking.Repair`). It is safe at any time and writes
   a security log entry.

Nothing in this package is cached, so there is no cache to go stale and no signal to send. Every window reads the database when it opens or
refreshes.

### 2.3 Lots (`supplylot`)

`SupplyLotNum`, `SupplyNum`, `LotNumber` (text), `DateExpiry` (date; 0001-01-01 when the lot has none), `QtyOnHand` (from the ledger),
`SupplyOrderItemNum` (the receipt that created it), `DateTimeEntry`. A receipt of a lot number that the supply already has adds to that lot.

**Soonest-expiring first.** The lots of a supply with stock are used in the order: expiry date (dated lots first, earliest first), then lots
without an expiry date, then stock without a lot (opening balances), and by `SupplyLotNum` when equal. When a quantity is larger than the first
lot holds, it is split over the next lots. When no lot has stock, the use is recorded without a lot and stock goes below zero.

### 2.4 Default materials (`supplyproccode`)

`SupplyProcCodeNum`, `CodeNum`, `SupplyNum`, `Quantity`, `ItemOrder`. A code can have any number of materials; a supply can be on many codes.

### 2.5 Supply settings (`supplysetting`)

`SupplySettingNum`, `SupplyNum` (unique), `ReorderLevel` (0: no low-stock alert), `IsLotTracked`, `IsImplant`. A supply without a row has
the defaults (no reorder level, not tracked by lot, not an implant). Marking a supply as an implant also marks it as tracked by lot.

### 2.6 Procedures awaiting materials (`supplyprocpending`)

`SupplyProcPendingNum`, `ProcNum` (unique), `PatNum`, `UserNum` (who completed it, 0 if no user), `DateTimeEntry`. A row is written when a
procedure becomes complete while tracking is on and its code has default materials. It is removed when its materials are confirmed (with or
without items), or when the procedure is un-completed or deleted. Supply Inventory, Awaiting Materials lists them.

## 3. Behaviour

### 3.1 Completing procedures

The business layer (`Procedures.Insert` of a complete procedure, `Procedures.Update` from another status to complete) only records the
procedure as awaiting materials (2.6). **It never deducts.** This is the same for every caller, with or without a user: the Middle Tier,
background threads, an interface. What is not confirmed in the program stays on the Awaiting Materials list until someone confirms it there.

In the program, the business layer also tells the user interface which procedures were completed by the user's own action (an event raised on
the program's main thread only, also when the work runs on the Middle Tier). The main window collects them and, when the program is idle again
and the window the user worked in is active (any message box or other window the same action opened has closed), opens one **Materials used**
panel for every procedure of that action that awaits materials. So the panel opens from every completion path without each path calling it:
Chart module Set Complete (one or several procedures, or an appointment), procedures entered as complete (procedure buttons, quick buttons,
the procedure list), the procedure edit window, the appointment module and the appointment edit window, broken appointment procedures,
Account module procedures. Several procedures completed at once share one panel. Procedures whose codes have no default materials do not open
the panel; materials can still be recorded for them from the procedure edit window (Materials). The program's windows change procedures only
through `Procedures.Insert` and `Procedures.Update` (a test in `InventoryTests` checks the source), which is what makes this hold for every path.
The main window starts the collector (`MaterialsUsedL.Start`); procedures completed on another thread of the program are only put on the
Awaiting Materials list.

Cancelling the panel deducts nothing; the procedures stay on the Awaiting Materials list.

### 3.2 The Materials used panel

One row per material per procedure: procedure (date, code, tooth), supply, quantity, lot, and the stock on hand. It opens filled with the codes'
default materials; for supplies tracked by lot the lots are filled in soonest-expiring first, split over lots when needed. The assistant can
change a quantity, pick another lot, add a supply from the supply list or by scanning, and remove a row. OK deducts. Opened again for a
procedure that already has materials (Materials in the procedure edit window, shown while tracking is on), it shows what the procedure used,
with lots, and OK writes only the differences (Use rows for more, Restore rows for less). For an implant this is where the lot is seen from
the procedure. Changing the materials of a procedure that does not await materials needs Completed Procedure Edit Misc
(`ProcCompleteEditMisc`) for the procedure's date (without it the panel is read-only); confirming awaiting materials needs no permission
beyond completing the procedure.

### 3.3 Un-completing and deleting

`Procedures.Update` from complete to any other status, and `Procedures.Delete`, write a Restore row for every supply and lot the procedure
still holds (the sum of its Use and Restore rows), with the lot it was taken from, and remove the awaiting row. This happens also when tracking
has been turned off since, so stock stays right.

### 3.4 Receiving

Supply Orders, Receive (for a placed order). The grid lists the order's items with ordered quantity, received so far (the order item's Receive ledger
rows) and the quantity to receive now, plus lot number and expiry date for supplies tracked by lot. A scan adds one to the matching item; a
supply that is not on the order is reported. Post writes one Receive row per item (and lot), at the order item's price, creates or finds the
lot, recomputes stock, sets the order item's received date when it is fully received and the order's received date when every item is, and
writes a Supply Purchases security log entry. Receiving more than was ordered asks first. An item that arrives in two lots gets a second row
(Add Lot Row).

### 3.5 Stock counts and adjustments

Supply Inventory, Inventory, Stock Count. Items are added by scanning or from the supply list; each row (supply, and lot for supplies tracked by lot) shows
the quantity the program had when the row was added and the counted quantity. A reason is required. Post writes a CountAdjust row with the
difference counted minus the program's quantity at that time (so procedures completed during the count are not counted twice) and one Inventory
Adjust security log entry per item. A lot found in the count (Lot...) is created when the count is posted. The count lives only while the
window is open. A manual adjustment (supply edit window, Stock..., Adjust; Inventory Adjust) writes an Adjust row with its reason and a
security log entry; the same window lists the supply's lots (a lot number or expiry date typed wrong can be corrected there) and every
ledger row. While tracking is off, the existing On Hand fields of the supply windows still work: an edit becomes an Adjust row ("On hand edited").
While tracking is on, On Hand is read-only there and changes go through counts and adjustments.

### 3.6 Dispensing

`DispSupplies.Insert` (the dental school dispensary) writes a Dispense row while tracking is on. The program has no window that dispenses
today.

### 3.7 Alerts

Two alert types, raised for all clinics (`ClinicNum` -1), linked to the "All" alert category when tracking is turned on:

- **Inventory Low Stock** (`AlertType.InventoryLowStock`, `FKey` the supply): a supply that is not hidden, whose reorder level is above 0
  and whose on hand is at or below it.
- **Inventory Expiring** (`AlertType.InventoryExpiring`, `FKey` the lot): a lot with stock whose expiry date is within the preference's lead
  days (`InventoryExpiryAlertDays`, default 30) or already past.

Both open the Supply Inventory window (`FormType.FormSupplyInventory`), with Mark as Read and Open Form (no Delete: a deleted alert that
still applies would come back). They are refreshed for the supplies of every stock movement, and in full by the program's alert check
at most every 10 minutes. The refresh adds an alert only with one `INSERT ... SELECT ... WHERE NOT EXISTS` statement, which MyISAM runs under
one table lock, so two workstations never create the same alert twice; it removes alerts that no longer apply. Turning tracking off removes
them.

### 3.8 Usage reports

Supply Inventory, Inventory, Usage Report: for a date range (by ledger date), Use minus Restore rows (and Dispense rows, by item), by item
(quantity, cost) or by procedure code (procedures, quantity, cost per supply), with a total and Print. Cost is quantity times the row's unit
cost.

### 3.9 Existing data

The migration writes an Opening ledger row for every supply whose `LevelOnHand` is not 0 (rounded to 2 decimals, at the supply's price, dated 0001-01-01 because the stock from before has no time of its own), so the
ledger and the stock agree from the start. Supplies, orders, suppliers and categories are unchanged.

## 4. Settings, permissions and enum values

- Preferences: `InventoryTrackingEnabled` and `InventoryExpiryAlertDays` (30; 0 turns the expiring alert off). Their rows are written the first
  time the office saves Supply Inventory, Inventory, Setup (needs Setup; a Setup security log entry), by `InventoryTracking.SaveSettings`;
  without a row tracking is off, so every existing and every new database starts with tracking off. Turning tracking on also links the two
  alert types to the "All" alert category where a link is missing, and the first time only grants Inventory Adjust (below).
- Whether the inventory tables exist is checked once per database (`InventoryTracking.IsInstalled`), so the hooks in `Procedures` and
  `Supplies` leave a database without them (a scratch database built from the Open Dental reference) as before.
- Supply Inventory, Inventory, Default Materials of Procedure Codes needs Setup. Changes are saved right away.
- The supply edit window has the barcode (`BarCodeOrID`, which it did not show before), the reorder level, tracked by lot and implant.
- `EnumPermType.InventoryAdjust` (10050, "Inventory Adjust"): posting counts, manual adjustments, correcting a lot and the repair. Security
  log entries for each. The first time tracking is turned on it is granted to every user group that has Supplier Edit, the permission of the
  people who look after supplies today, unless a group has it already (a Security Admin log entry names the groups).
- Receiving uses Supply Purchases (`SupplyPurchases`) for its log entry, as orders do.
- Fork-only values stored in the database (`docs/port-notes/FORK-ENUMS.md`): `EnumPermType.InventoryAdjust` 10050, in the inventory
  range 10050-10099. `AlertType` and `FormType` are stored in `tinyint(4)` columns (at most 127), so 10050-10099 does not fit them; their
  fork values count down from 127 (`ForkEnums.FIRST_VALUE_TINYINT`, the rule shared with routine maintenance, which has 127-120), from the
  inventory range 119-112: `AlertType.InventoryLowStock` 119, `AlertType.InventoryExpiring` 118, `FormType.FormSupplyInventory` 119
  (an owner question records the move away from 10050-10099). The security tree's Set All and Set None, and the Central Manager's alert
  list, loop over the defined values, not over 0 to count-1.

## 5. Migration steps

| Level | Step |
|---|---|
| 2026110211 | `supplyledger`, and in the same step (only when it created the table) the opening ledger rows |
| 2026110212 | `supplylot`, `supplyproccode`, `supplysetting`, `supplyprocpending` |

2026110213 to 2026110219 are kept for this feature. The steps change no Open Dental table, so adopting an Open Dental 25.3.48 database for
this feature only adds the five tables and the opening rows: the adoption tools (`tools/AdoptionValidation`) list the opening rows in
migration-preview, allow the five tables (`allowed-differences.adoption.json`) and hold the adopted copy's ledger to exactly the planned
opening rows (`AdoptionRowCheck`), the other four tables empty.

## 6. Tests

`tests/InventoryTests` (scratch databases `test_inv_*` on the local MariaDB, synthetic rows only): ledger and `LevelOnHand` consistency,
soonest-expiring first, receiving, counts and adjustments, deduct and restore on complete, un-complete and delete, several procedures in one
confirmation, the implant lot on the procedure, alerts without duplicates, the opening-balance migration, and the repair; the inventory
windows built in memory with every control inside them, and the source rule that the program's windows change procedures only through the
business layer. The main test database is the committed `db/init/new-database.sql` with this build's fork migrations run on it, so the tests
pass before and after the file is regenerated with the inventory steps. `RegistryTests` and `M202Tests` treat fork-only enum members as fork
values; `ForkMigrationTests` skips the fork tables where it checks that every table class has an Open Dental table.

## 7. Owner questions

In `docs/owner-questions.md`, "Inventory tracking (INV, 2026-11-02)", each with what the program does today.
