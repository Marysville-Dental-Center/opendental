# Inventory tracking for materials

Status: 2026-11-02. Package INV (roadmap "Next" item 4), from the owner decisions of 2026-11-02. It builds on Open Dental's supply inventory
(`supply`, `supplyorder`, `supplyorderitem`, `supplyneeded`, `dispsupply`, `equipment` and their windows) and replaces nothing. The owner
answered the questions of section 7 on 2026-11-02 ("all as recommended"): every default stays as built, except that a stock count is now saved
while it is counted (INV 11; sections 2.7 and 3.5).

## 1. What the office gets

- **One storage location.** Stock is counted per supply (`supply.LevelOnHand`) and, for supplies tracked by lot, per lot.
- **Barcode scanning** with a USB keyboard-wedge scanner (it types the code and Enter). A scan is matched against `supply.BarCodeOrID`
  (exact, ignoring case and surrounding blanks; the catalog number is the fallback when no barcode matches). Scanning is used:
  - when **receiving** an order: each scan of an item on the purchase order adds one to what is received now; lot number and expiry date are
    entered for supplies tracked by lot; posting raises stock;
  - in a **stock count**: each scan adds one to the counted quantity of the item; posting writes the differences as count adjustments with a
    reason. The count is saved line by line as it is counted, and any user can resume it on any workstation;
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

All new data is in new tables. No column is added to an Open Dental table, so a migrated database still matches the Open Dental 26.1.70
reference. The new tables are this program's own tables, declared once with their exact definitions in `InventoryTables` (a `ForkTableSet`,
in `InventorySteps.cs`), the shared mechanism for fork-only tables (`OpenPmsDentalBusiness/Misc/ForkMigrations/README.md`, "Fork-only
tables"): the schema verifier allows each as an extra table (a documented exception), `ForkTables.VerifyDatabase` holds each to its
declaration, and the migration steps create them from those definitions. The tables are MyISAM and utf8mb3 like every other table.

| Table | Class | One row is |
|---|---|---|
| `supplyledger` | `SupplyLedger` | one stock movement of one supply (and lot): a signed quantity |
| `supplylot` | `SupplyLot` | one lot of a supply: lot number, expiry date, quantity on hand |
| `supplyproccode` | `SupplyProcCode` | one default material of a procedure code: supply and quantity |
| `supplysetting` | `SupplySetting` | the tracking settings of one supply: reorder level, tracked by lot, implant |
| `supplyprocpending` | `SupplyProcPending` | a completed procedure whose materials nobody has confirmed yet |
| `supplycount` | `SupplyCount` | one stock count: open (at most one), being posted, posted or discarded |
| `supplycountline` | `SupplyCountLine` | one counted line of a stock count: supply, lot, the program's quantity when counted, the counted quantity |

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
| `SupplyCountLineNum` | for a CountAdjust row, the saved stock count line it was posted from (2.7), written in the row's own `INSERT`; 0 for every other row and for a count posted from lines that are not saved |

Rows are never edited or deleted (the exceptions: deleting a supply that has only opening and manual adjustment rows deletes those rows
with it, and merging two patients moves `PatNum` of the ledger and awaiting rows to the patient kept, as it moves the procedures:
`supplyledger.PatNum` and `supplyprocpending.PatNum` are in `Patients.StringArrayPatNumForeignKeys`). A correction is a new row.

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
   confirming again after a failure part-way never deducts twice. After writing, it reads the procedures' statuses again: a procedure that
   another workstation un-completed or deleted in the meantime (its restore found nothing of this confirmation's rows) gets back what this
   confirmation took, and is reported as not deducted.
5. **Un-completing and deleting write the status first, then restore** (`Procedures.Update` and `Procedures.Delete`). A failure in between
   leaves a procedure that is no longer complete but still holds materials, which the repair finds; never a complete procedure whose
   materials are back in stock. A Restore row is written only while the procedure still holds exactly what it puts back (one
   `INSERT ... SELECT ... WHERE` statement, under one MyISAM table lock), so two workstations restoring the same procedure at once put it back
   once (`SupplyLedgers.InsertRestoresIfStillHeld`). In `Procedures.Update` the inventory hooks run last, after the procedure and its note
   and signature are saved, and neither hook ever throws (`InventoryTracking.OnProcSetComplete`, `OnProcNoLongerComplete`): a failing
   inventory table (a MyISAM table marked as crashed, a lost connection, a deadlock), also on a database whose office never turned tracking
   on, is written to the log, and the procedure is still saved, deleted and given its note. A restore that did not run is put back by the
   repair; a completion whose awaiting row could not be written is not on the Awaiting Materials list, so nothing is deducted for it, which is
   never a silent deduction. The procedure edit window hides Materials when the inventory tables cannot be read.
6. **Receiving, counts and adjustments are safe to retry.** Once their ledger rows are written, what follows (received dates, audit entries,
   alerts) can no longer make the call fail: a failure there is written to the log (`InventoryTracking` folder). When the call itself fails
   (part-way, or a lost Middle Tier answer), the windows read the database again before another post is possible: Receive reloads Received
   from the ledger and clears Receive Now, a failed stock count post opens the count again with the lines it adjusted showing no difference
   (each takes the CountAdjust rows that name it into its snapshot) and every other line keeping the program's quantity of when it was
   counted, never today's and never the lot counter's, which the failed write may have left at its old value (a re-post writes only what the
   failed post did not; 2.7), and the adjustment window closes with a message to check the ledger in Stock.
7. **Repair** (Supply Inventory, Inventory, Repair Stock from Ledger; Inventory Adjust): first puts back what procedures that are no longer
   complete (or no longer exist) still hold (`SupplyLedgers.GetProcNumsHeldNotComplete`, reason "Repair: procedure no longer complete") and
   removes the awaiting rows of procedures that are no longer complete; then recomputes every supply's and every lot's quantity from the
   ledger and reports how many procedures, awaiting rows, supplies and lots it changed (`InventoryTracking.Repair`,
   `SupplyLedgers.RecomputeAll`). It is safe at any time and writes a security log entry.

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

### 2.7 Stock counts (`supplycount`, `supplycountline`)

A stock count is saved while it is counted (owner decision INV 11), so closing its window, a crash or a power cut loses nothing of it.

`supplycount`: `SupplyCountNum`, `CountStatus` (`SupplyCountStatus`: 0 Open, 1 Posting, 2 Posted, 3 Discarded), `UserNum` and
`DateTimeEntry` (who started it and when, server time), `UserNumClosed` and `DateTimeClosed` (who posted or discarded it and when; while it is
Posting, when the post began), `Reason` (the reason of the post). The office has at most one count that is Open or Posting (one storage
location, one count): "the open count". Closed counts stay, with their lines, as the record of what was counted.

`supplycountline`: `SupplyCountLineNum`, `SupplyCountNum`, `SupplyNum`, `SupplyLotNum` (0 for stock without a lot, and for a lot found in
the count), `LotNumber` and `DateExpiry` (only for a lot found in the count, which the post creates; blank for an existing lot, whose number
and expiry date are read from `supplylot`), `QtyProgram` (the program's quantity of that supply and lot, from the ledger, when the line was
last counted: its snapshot), `QtyCounted`, `QtyPosted` (the sum of the line's CountAdjust rows that its snapshot already includes: 0 until a
post that failed or did not finish adjusted it, item 3), `UserNum` and `DateTimeEntry` (who counted it last, and when). One line per supply
and lot: a unique key on `SupplyCountNum`, `SupplyNum`, `SupplyLotNum`, `LotNumber`.

Without transactions (`SupplyCounts`, `SupplyCountLines`, `InventoryTracking`):

1. **Starting.** The first counted line starts the count with one `INSERT ... SELECT ... WHERE NOT EXISTS` statement (no Open or Posting
   count), which MyISAM runs under one lock of `supplycount`: two workstations starting at the same moment share one count.
2. **Saving a line** is one `INSERT ... SELECT ... FROM supplycount WHERE` (the count is Open) `... ON DUPLICATE KEY UPDATE` statement. It
   writes a line only into an open count, and a supply and lot counted again replaces its line (counted quantity, a new snapshot, user and
   time) instead of adding a second one, also when two workstations save it at the same moment. A scan adds one to the line's counted quantity
   inside that statement, so scans of the same item on two workstations all count. A line is saved into the count the window shows; a window
   that shows none saves into the open count (one started elsewhere meanwhile is joined) or starts one. A line the statement refuses because
   the count is no longer open (posted or discarded elsewhere) is reported to the user, never lost or moved into a new count without a word;
   the window then shows the office's count as it is now. A lot found in the count whose number has become a lot of the supply since
   (received meanwhile) is saved as that lot, and its earlier line follows it.
3. **Posting** first marks the count Posting (`UPDATE ... WHERE CountStatus=Open`): of two workstations posting the same count only one
   posts it, and from then on no line can be saved into it. Then it reads the lines, writes the count adjustments (`PostCount`, 3.5), each
   CountAdjust row with its line in `supplyledger.SupplyCountLineNum` (in the row's own `INSERT`, so a row is never written without it), and
   marks the count Posted. When the post fails, wherever it fails, the count is made Open again (`ReopenAfterFailedPost`). A snapshot is never
   replaced with the program's quantity of that moment: a saved count can stay open for days, and the stock used since a line was counted
   would be counted again. Instead each line takes the CountAdjust rows of its own that its snapshot does not include yet into it
   (`QtyProgram` plus their sum minus `QtyPosted`, then `QtyPosted` is that sum, in one `UPDATE` that sums the rows from the ledger itself,
   so running it again changes nothing). So a line the post adjusted now
   shows no difference and posting again writes nothing for it, and every other line keeps the snapshot of when it was counted, so posting
   again writes exactly its difference: the same stock as an uninterrupted count, whatever was used meanwhile. A lot found in the count that
   the post created becomes that lot's line. A line counted again afterwards gets a new snapshot as always, which includes what was written;
   `QtyPosted` keeps saying so, so a second failed post never takes the first one's rows for its own. When even opening it again fails (a lost
   connection, a crash, a power cut during the post), or everything was written and only closing the count failed, the count stays Posting;
   the Supply Inventory window offers to resume it once the post began more than 5 minutes ago (`InventoryTracking.MINUTES_POST_STALE`;
   before that another workstation may still be posting it), which opens it the same way.
4. **Discarding** marks the open count Discarded (`UPDATE ... WHERE CountStatus=Open`) and writes no ledger row.

## 3. Behaviour

### 3.1 Completing procedures

The business layer (`Procedures.Insert` of a complete procedure, `Procedures.Update` from another status to complete) only records the
procedure as awaiting materials (2.6). **It never deducts.** This is the same for every caller, with or without a user: the Middle Tier,
background threads, an interface. What is not confirmed in the program stays on the Awaiting Materials list until someone confirms it there.

In the program, the business layer also tells the user interface which procedures were completed by the user's own action (an event raised in
the program, on the thread that completed them). `Procedures.Insert` and `Procedures.Update` raise it on both sides of their Middle Tier
check. Four business methods complete procedures entirely on the Middle Tier server for a Middle Tier client, where the program does not hear
the `Update` calls inside them:

- `Procedures.SetCompleteInApptInList`, which the program calls itself for the Chart module's Set Complete of an appointment's procedures
  (`ProcedureL.SetCompleteInAppt`) and for per-visit codes added complete from the Chart and Appointments modules and Other Appointments
  (`Appointments.TryAddPerVisitProcCodesToAppt` runs in the program);
- `Appointments.ApptSaveHelper`, the appointment edit window's OK: its `Procedures.UpdateProcsInApptHelper` (which calls
  `SetCompleteInAppt`, and so `SetCompleteInApptInList`, on the server) and its per-visit codes complete the appointment's procedures on the
  server, so `SetCompleteInApptInList`'s own branch never runs for this window;
- `Appointments.CompleteClick`, the Appointments module's Set Complete;
- `Procedures.SetCanadianLabFeesCompleteForProc` (Canada): a procedure's lab fees, and a lab fee's parent procedure, completed from the
  procedure edit window.

In their Middle Tier client branch they read the procedures' statuses before the remote call and raise it after the call returns
(`InventoryTracking.NotifyProcsNowComplete`), for every procedure that is complete then and was not before (for `ApptSaveHelper` and
`CompleteClick` the appointment's procedures are read again, because what they return does not show the procedures the server completed or
added). A test in `InventoryTests` checks the four branches. The other business methods that reach `UpdateProcsInApptHelper` or
`SetCompleteInAppt` have no user (`Appointments.UpsertApptFromWeb`, the HL7 and eCW interfaces), so their procedures stay on the Awaiting
Materials list, or only schedule an appointment (`SchedulePlannedApt`, appointment moves), which completes nothing.

The main window collects the procedures of its own (main) thread and, when the program is idle again
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
default materials. The assistant can change a quantity, pick a lot, add a supply from the supply list or by scanning, and remove a row. OK
deducts.

**Lots are chosen when OK is pressed.** Every material except an implant is taken soonest-expiring first unless the assistant picks a lot:
its row has lot -1 ("soonest-expiring first" in the Lot list), and `InventoryTracking.ConfirmMaterials` chooses its lots from the stock as it
is when OK is pressed (`InventoryTracking.ResolveAutoLines`), procedure by procedure, so a quantity raised above what the first lot holds
splits over the lots, a scan or Add adds to that row, and a panel that was open while another workstation took the last of a lot takes the
next lot. The grid shows the lots that would be taken now (the same rule, on the lots the panel read; "SOON (1), LATE (1)" when split). A
lot the assistant picks is kept as it is; the rest of the supply on lot -1 is chosen around it. What a procedure already holds of a supply
stays in the lots it was taken from unless its quantity is lowered (then stock without a lot is given back first, then the lot that expires
last).

**Implants** keep the lot shown on their row (the panel fills in the soonest-expiring lot that still has one free after the panel's other
rows), because the lot is recorded with the patient's procedure. "(no lot)" is offered for an implant only while no lot has stock. Lot
Number takes the lot number printed on the package, typed or scanned, and finds or creates the lot; for a supply tracked by lot whose lot
holds less than is taken (an implant that came in as an opening balance has no lot), OK first moves the missing quantity from the supply's
stock without a lot into the lot (two Adjust rows, "Lot number entered: moved from stock without a lot", and an Inventory Adjust audit
entry), up to what that stock holds. An implant taken without a lot needs a reason, which goes to the audit trail
("Implant deducted without a lot number"); without one `ConfirmMaterials` writes nothing. OK warns when an explicit lot holds less than is
taken from it (read again from the database; for a lot whose number was entered with Lot Number, the stock without a lot counts too).

**Scanning.** The Scan box is first in the tab order and has the focus when the panel opens; rows are selected in the Supply column, never
in the Qty column, so no edit box opens to take a keyboard-wedge scanner's digits, and after a scan the focus goes back to the Scan box. A
quantity typed above 9999 is refused (a barcode in the wrong box looks like that); above 99 the panel asks first. Receive and Stock Count
work the same way (they ask above 999, or above the ordered quantity, or ten times the program's quantity, when that is larger).

Opened again for a procedure that already has materials (Materials in the procedure edit window), it shows what the procedure used, with
lots, and OK writes only the differences (Use rows for more, Restore rows for less). A procedure that is still on the Awaiting Materials list
always opens from its code's defaults, also when it already holds some materials (a confirmation that failed part-way wrote some of its rows
and left it on the list): a supply it holds shows the larger of what it holds and the default, a supply it does not hold yet the default, so
OK deducts what is still missing. Only a procedure that does not await materials shows just what it holds. For an implant this is where the lot is seen from the
procedure; the Materials button's tooltip also names the implant lots. The Materials button is shown while tracking is on, and after
tracking is turned off for a procedure that holds materials, read-only then. Changing the materials of a procedure that does not await
materials needs Completed Procedure Edit Misc (`ProcCompleteEditMisc`) for the procedure's date (without it the panel is read-only);
confirming awaiting materials needs no permission beyond completing the procedure.

### 3.3 Un-completing and deleting

`Procedures.Update` from complete to any other status, and `Procedures.Delete`, after writing the procedure's new status, write a Restore row
for every supply and lot the procedure still holds (the sum of its Use and Restore rows), with the lot it was taken from, and remove the
awaiting row. This happens also when tracking has been turned off since, so stock stays right.

### 3.4 Receiving

Supply Orders, Receive (for a placed order). The grid lists the order's items with ordered quantity, received so far (the order item's Receive ledger
rows) and the quantity to receive now, plus lot number and expiry date for supplies tracked by lot. A scan adds one to the matching item; a
supply that is not on the order is reported. Post writes one Receive row per item (and lot), at the order item's price, creates or finds the
lot, recomputes stock, sets the order item's received date when it is fully received and the order's received date when every item is, and
writes a Supply Purchases security log entry. Receiving more than was ordered asks first. An item that arrives in two lots gets a second row
(Add Lot Row).

### 3.5 Stock counts and adjustments

Supply Inventory, Inventory, Stock Count, or the Stock Count button of Supply Inventory. The window shows the office's open count, if there is
one, with its lines, to every user who can open it: a count started on another workstation, by another user, or before the window or the
program was closed is resumed where it was (2.7). Items are added by scanning or from the supply list; each row (supply, and lot for supplies
tracked by lot) shows the program's quantity and the counted quantity. The program's quantity is read from the ledger
(`InventoryTracking.GetCountLines`, `GetQtyProgram`), never from the counters, which a write that failed part-way can leave stale. Every lot
whose stock is not 0 gets a row, a lot below zero too (a lot taken past its stock, a lot number entered from the package, two workstations
taking the last unit), so a full count corrects it; a scan adds to the selected row of the supply, otherwise to its first row with stock.
A supply the window shows from saved lines (a resumed count, or lines another workstation counted) also gets the rows of its lots not
counted yet, after its counted rows, and Add or a scan of a supply that has rows adds only the rows it is missing: a partly counted supply is
finished in its own lots, never scanned into a lot that was counted.

**Each counted row is saved at once** (`InventoryTracking.SaveCountLine`): a scan, a typed quantity, and a lot found in the count (Lot...)
once it is counted. Saving takes the program's quantity at that moment as the line's snapshot, so a supply and lot counted again replaces its
line with the new quantity and a new snapshot. Remove, or clearing a counted quantity, deletes the line (`DeleteCountLine`). Rows of lots not
counted yet are not saved: they hold nothing counted. After every save the window reads the count's lines again, so it shows what other
workstations counted. The window closes without a warning, since nothing is lost.

**Post** (Inventory Adjust; a reason is required; `InventoryTracking.PostOpenCount`) works as before counts were saved: for every line whose
counted quantity differs from its snapshot, a CountAdjust row of the difference (counted minus the program's quantity when the line was
counted, so procedures completed during the count are not counted twice) with the reason, and one Inventory Adjust security log entry per
adjusted line; a lot found in the count is created when the count is posted. Then the count is closed as Posted (who, when, the reason). A
failed post leaves the count open: the lines it adjusted show no difference any more, and every other line keeps the program's quantity of
when it was counted, so posting again writes only what the failed post did not (2.7).

**Discard** (Inventory Adjust, after a confirmation; `InventoryTracking.DiscardOpenCount`) closes the open count without changing stock and
writes an Inventory Adjust security log entry naming who started it, when, and how many lines it had.

**Supply Inventory** shows whether a count is open, under its buttons: "Stock count open: started by <user> on <date and time>, N line(s)."
(or that its post is running, or did not finish and can be resumed), and its Stock Count button then reads Resume Count. Counting needs no
permission beyond opening Supply Inventory, as before; posting and discarding need Inventory Adjust.

A manual adjustment (supply edit window, Stock..., Adjust; Inventory Adjust) writes an Adjust row with its reason and a
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
  days (the `ForkInventoryExpiryAlertDays` preference, default 30) or already past.

Both open the Supply Inventory window (`FormType.FormSupplyInventory`), with Mark as Read and Open Form (no Delete: a deleted alert that
still applies would come back). They are refreshed for the supplies of every stock movement, and in full by the program's alert check
at most every 10 minutes. The refresh adds an alert only with one `INSERT ... SELECT ... WHERE NOT EXISTS` statement, which MyISAM runs under
one table lock, so two workstations never create the same alert twice; it removes alerts that no longer apply. With random primary keys
(replication) that statement takes the alert's key from `ReplicationServers.GetKey`, as the generated CRUD and `InsertRestoresIfStillHeld`
do, never from `AUTO_INCREMENT`, which two replicated servers would hand out twice. Turning tracking off removes them.

### 3.8 Usage reports

Supply Inventory, Inventory, Usage Report: for a date range (by ledger date), Use minus Restore rows (and Dispense rows, by item), by item
(quantity, cost) or by procedure code (procedures, quantity, cost per supply), with a total and Print. Cost is quantity times the row's unit
cost.

### 3.9 Existing data

The migration writes an Opening ledger row for every supply whose `LevelOnHand` is not 0 (rounded to 2 decimals, at the supply's price, dated 0001-01-01 because the stock from before has no time of its own), so the
ledger and the stock agree from the start. Supplies, orders, suppliers and categories are unchanged.

## 4. Settings, permissions and enum values

- Preferences: `ForkInventoryTrackingEnabled` and `ForkInventoryExpiryAlertDays` (30; 0 turns the expiring alert off), fork-owned preference
  rows outside the `PrefName` enum, which holds only the names Open Dental publishes (the "Fork" prefix keeps a later Open Dental name from
  clashing): `InventoryTracking.PREF_ENABLED` and `PREF_EXPIRY_ALERT_DAYS`, read with `PrefC.GetRawOrDefault` and written with
  `PrefC.UpsertRawNoCache`, as every fork-owned setting is. Their rows are written the first
  time the office saves Supply Inventory, Inventory, Setup (needs Setup; a Setup security log entry), by `InventoryTracking.SaveSettings`;
  without a row tracking is off, so every existing and every new database starts with tracking off. Turning tracking on also links the two
  alert types to the "All" alert category where a link is missing, and the first time only grants Inventory Adjust (below).
- Whether the inventory tables exist is checked once per database (`InventoryTracking.IsInstalled`), so the hooks in `Procedures` and
  `Supplies` leave a database without them (a scratch database built from the Open Dental reference) as before.
- Supply Inventory, Inventory, Default Materials of Procedure Codes needs Setup. Changes are saved right away.
- The supply edit window has the barcode (`BarCodeOrID`, which it did not show before), the reorder level, tracked by lot and implant.
- `EnumPermType.InventoryAdjust` (10050, "Inventory Adjust"): posting and discarding counts, manual adjustments, correcting a lot and the
  repair. Security log entries for each. The first time tracking is turned on it is granted to every user group that has Supplier Edit, the permission of the
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
| 2026110213 | `supplycount`, `supplycountline` (stock counts saved while they are counted, owner decision INV 11), and `supplyledger.SupplyCountLineNum` with its key on a ledger an earlier build created without them (2026110211 creates the ledger with them) |

2026110214 to 2026110219 are kept for this feature. The steps change no Open Dental table, so adopting an Open Dental 25.3.48 database for
this feature only adds the seven tables and the opening rows. The adoption tools (`tools/AdoptionValidation`) treat them as every fork-only
table (a `TableAdded` rule each in `allowed-differences.adoption.json`, the six tables without rows listed under "Fork tables:" and required
empty, so an adopted database has no open stock count), with one documented exception: the stock ledger is the one table of this program
that adoption writes rows into, so migration-preview lists its opening rows (`InventoryPlan`) and adopt's row check holds it to exactly those
rows ("Stock ledger:", `AdoptionRowCheck.CheckInventoryLedger`) instead of requiring it empty.

## 6. Tests

`tests/InventoryTests` (scratch databases `test_inv_*` on the local MariaDB, synthetic rows only): ledger and `LevelOnHand` consistency,
soonest-expiring first (lots chosen when deducted: a raised quantity splits over lots, two confirmations against the last unit of a lot,
explicit lots kept beside lot -1), receiving, counts and adjustments (and a failure after the ledger write; the program quantity read from the
ledger after a missed recompute, so a re-post writes nothing more; a lot below zero listed and corrected by a full count), saved stock counts
(`CountTests` and `CountMigrationTests`: lines saved at once and read back after the window is closed and reopened, resumed by another user, one open count only, a
recount replacing its line with a new snapshot, two workstations saving lines into the same open count at the same moment, a post after a
resume giving the same results as an uninterrupted count, a failed post opened again and a post interrupted by a crash resumed (each with
stock used between counting and the failure, and between the failure and the post again: the same stock and adjustments as an
uninterrupted count), a post that failed before it wrote anything, a line counted again after a failed post followed by a second failed
post, a lot-tracked supply counted in one lot and resumed in the window (its other lot shown, a scan into the selected lot, Add without
duplicates), a discard with its audit entry, and step 2026110213 on a database without the inventory tables and on one already migrated
with stock, where it also adds the ledger's `SupplyCountLineNum` and keeps every row), deduct and restore
on complete, un-complete and delete (a procedure un-completed while it is confirmed, restores written twice, and the stock ledger and default
materials renamed away: the procedure is still completed, un-completed with its note and deleted, and the repair puts back afterwards), several
procedures in one confirmation, the implant lot on the procedure (a lot entered for stock without a lot, an implant without a lot), the
patient merge, alerts without duplicates (and with random primary keys, a replication key), the opening-balance migration and the tables
against their declarations, the fork-owned preference rows, and the repair (procedures no longer complete); the inventory windows built in
memory with every control inside them, the Materials used panel loaded and scanned into with a window handle but never shown (and reopened
for a procedure whose confirmation failed part-way: it deducts the missing default once), the source rule that the program's windows change
procedures only through the business layer, the four Middle Tier branches that raise the event, and the order of the writes in
`Procedures.Delete`. The running program with a scanner was not tried (the build rules forbid starting the program). The main test database is the committed `db/init/new-database.sql` with this build's fork migrations run on it, so the tests
pass before and after the file is regenerated with the inventory steps. `RegistryTests` and `M202Tests` treat fork-only enum members as fork
values; `ForkMigrationTests` maps the table classes of fork-only tables to their declarations where it checks that every table class has a
table.

## 7. Owner questions

In `docs/owner-questions.md`, "Inventory tracking (INV, 2026-11-02)". The owner answered them on 2026-11-02 ("all as recommended"):

- INV 1 to 10, 12 to 15, 17 and 18: kept as built (the defaults this document describes).
- INV 11: changed. A stock count is saved while it is counted: each counted line as soon as it is entered, one open count at a time, which
  any user allowed to count can resume; posting works as before and closes the count; an open count can be discarded after a confirmation,
  with a security log entry (2.7, 3.5, step 2026110213).
- INV 16 (the alert values in the tinyint range) is settled when the packages are merged.
