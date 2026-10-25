# Intraoral X-ray capture: clinic setup and runbook

Status: 2026-10-25. Package CAP-0 of the native capture design. For the clinic administrator, and for the staff who take X-rays.

This runbook covers intraoral X-rays from a digital sensor through Open Dental's existing **Mount / Acquire**, with the protections built into Open Dental's TWAIN client:

- the **source gate**, which decides per computer which TWAIN sources may be opened;
- the **locked main window** and the small **Capture** window with Cancel, while the sensor software is open;
- the **capture journal**, a copy of every image the sensor delivers;
- the **capture quarantine**, for images that arrive after a series stopped.

A later version adds a capture session window with a patient banner. This runbook will change then.

The runbook names no sensor, camera or software vendor. It uses these terms:

| Term | Meaning |
|---|---|
| the sensor's TWAIN source | the 32-bit TWAIN data source that the sensor supplier installs for the sensor |
| the source's contract | the versioned document in which the supplier publishes how its source behaves through TWAIN: capabilities, installation settings, condition codes, and how sessions end |
| the sensor software | the window the source opens while it waits for an exposure |
| product name | the exact name a TWAIN source reports (its TWAIN ProductName) |

## 1. Rules that always apply

1. **Never re-expose a patient until the images are checked.** When an image seems to be missing, check in this order:
   1. the mount and the unmounted items;
   2. any **Capture** notice on the screen;
   3. the capture quarantine and the capture journal (section 10);
   4. the sensor software and the source's own recovery (section 10.3).

   Re-expose only when the dentist decides that the image is lost or not usable.
2. **No patients before acceptance.** On each computer, use only a phantom and a test patient record (not a real person) until all of these hold:
   1. the installed version of the source's contract publishes **distinct end reasons** (why a session ended without an image) and **safe disable** (a stop that never loses an image), and their tests in section 12 passed on that computer;
   2. the sensor supplier's own comparison of the source's images with the sensor manufacturer's imaging software has passed;
   3. the acceptance in section 12 passed on that computer;
   4. the dentist has signed off image equivalence against written criteria agreed before the run.
3. **No batch capture on patients.** Device type Twain Multi delivers all images of a batch at the end, in arrival order. One skipped or repeated exposure shifts every later image, together with its tooth numbers and rotation, and an image that cannot be read can stop the rest of the batch from arriving. Use Twain Multi only on the phantom.
4. **The capture folders hold patient images.** Treat them like the image folder: include them in the backup, never copy them to removable media, and remove files only as this runbook says.

## 2. Prerequisites (every capture computer)

1. **Windows accounts.** Staff run Open Dental as ordinary Windows users, not administrators. You need an administrator account only for the setup steps.
2. **32-bit TWAIN data source manager.** Open Dental is a 32-bit program and uses the first of these files that exists:
   1. `TWAINDSM.dll` next to `OpenDental.exe`;
   2. `C:\Windows\SysWOW64\TWAINDSM.dll`;
   3. `C:\Windows\twain_32.dll`, which is part of Windows.

   If none exists, no TWAIN source can be listed or opened. If the sensor's source is missing from the Twain Name list (section 6) while only `twain_32.dll` is present, install the TWAIN Working Group's signed 32-bit data source manager (`TWAINDSM.dll`) and check again.
3. **The sensor's TWAIN source.** Install it, and the sensor's driver, with the supplier's own installer. Open Dental installs and configures nothing for it. It must be a 32-bit source. Connect the sensor.
4. **The source's contract.** Get the contract for the installed version of the source. Record its version line. While it is marked as a draft, record its revision identifier as well. Keep a copy with the acceptance record.
5. **Installation settings.** The contract lists the source's installation settings that change what the source delivers or how its sessions end. Typical examples:
   - a mode that overrides how many images a session delivers;
   - image enhancement, brightness and contrast;
   - a rotation or flip of the delivered image;
   - the reported resolution;
   - timeouts.

   Check each one against the neutral value the contract gives, by reading the setting or from the source's log, as the contract describes. Open Dental never reads these settings. Rotation and flip must be neutral, because Open Dental applies each mount slot's own rotation; a rotation in the source would be applied twice. Record every value. The one allowed exception is the source's own session timeout (section 4.3).
6. **No Import Automatically.** Make sure nobody runs Imaging > Import > Import Automatically on a capture computer. It keeps running when the patient changes and can file images under the wrong patient.
7. **Automatic logoff.** Know the automatic logoff time that applies to each capture user (section 4.3).

## 3. Create capture.ini and set the folder permissions

capture.ini holds this computer's capture settings. Create it by hand, once per computer.

1. Sign in to Windows as an administrator and open **Command Prompt** with "Run as administrator".
2. Create the folder:

   ```
   mkdir "%ProgramData%\OpenPMS-Dental\Imaging"
   ```

3. Give the local Users group Modify on it, inherited by everything inside:

   ```
   icacls "%ProgramData%\OpenPMS-Dental\Imaging" /grant *S-1-5-32-545:(OI)(CI)M
   ```

   `S-1-5-32-545` is the local Users group in any Windows language. Every Windows user who runs Open Dental on this computer must be able to write capture.ini and the capture folders. Open Dental adds the same rule itself when it creates the folder, but it cannot add it to a folder an administrator created, so this command is the backstop.
4. Create the file:

   ```
   notepad "%ProgramData%\OpenPMS-Dental\Imaging\capture.ini"
   ```

   Enter the `[Twain]` keys from section 4 and save. Notepad's default encoding (UTF-8) is fine.
5. Check the permissions:

   ```
   icacls "%ProgramData%\OpenPMS-Dental\Imaging\capture.ini"
   ```

   The output must list `BUILTIN\Users` with `(M)`, usually shown as `(I)(M)`.

How Open Dental uses the file:

- It reads capture.ini again before it opens any TWAIN source. A change therefore applies from the next slot or scan, without a restart. Do not edit the file during a series.
- Without the ProgramData file, it reads `%LOCALAPPDATA%\OpenPMS-Dental\Imaging\capture.ini` of the Windows user instead. Without either file it uses the defaults: no source gate, journal on. A file it cannot read counts as empty, so the gate is then off too. Every journal sidecar records which file was read (`ConfigFile`) and any problem reading it (`ConfigError`).
- Format: a `[Twain]` header, then one `Key=Value` per line. Names ignore case. If a key appears twice, the last one counts. `;` starts a comment, also after a value. Lists are separated by `|`. On and off are `1` and `0`. Hex numbers are written `0x` followed by the digits.
- Any Windows user of the computer can change capture.ini. The source gate prevents accidents, not misuse. Hand edits are not detected. Record the values in the acceptance record. Every journal sidecar also records the values in effect for its image.

## 4. The [Twain] keys

Mount / Acquire reads only the `[Twain]` section. Other sections are for later versions; leave them out.

| Key | Default | Set it to | What it does |
|---|---|---|---|
| `ThreadModal` | `1` | `1` | While the sensor software is open for a Mount / Acquire slot, Open Dental's windows are locked and the Capture window offers Cancel. Use `0` only to troubleshoot, with the phantom and the test patient: then Open Dental's main window stays usable during a slot, there is no Capture window and no Cancel, and nothing stops a patient change in the middle of a series. Scans are never affected. |
| `RestrictSources` | `0` | `1` on every computer | Only sources listed in `AllowedSources` can be opened, by Mount / Acquire and by the Scan buttons. Any other source is refused with "This TWAIN source is not enabled on this computer." |
| `AllowedSources` | empty | first every scanner this computer already uses; then the sensor's source, but only on a computer that passed acceptance, or while its own acceptance runs (phantom and test patient only) | Product names separated by `\|`. Each must match a whole product name; case and surrounding spaces are ignored. The gate checks the source that would actually open, so a shortened Twain Name does not get round it. With `RestrictSources=1` and an empty list, nothing can be opened, not even a scanner. A name that contains `;` cannot be listed. |
| `JournalKeep` | `200` | `200` or more | How many of the most recent journal copies are kept. `0` turns the journal off: never use `0`. Each copy is the uncompressed image, about 1 to 3 MB for an intraoral image, so 200 copies take roughly 0.2 to 0.6 GB. |
| `DisableIsSafeSources` | empty | the sensor's product name, only when the installed contract version has safe disable: on the test computer for the safe-disable test, on other computers only after that test passed on the test computer | Open Dental asks the listed source for safe disable before each slot. |
| `SafeDisableCap` | empty | the safe-disable capability number from the contract, in hex | Set together with `DisableIsSafeSources`. |
| `RefusedDisableCodes` | empty | the contract's "disable refused" condition codes, in hex, separated by `\|` | Set together with `DisableIsSafeSources`. When the source refuses a stop with one of these codes, Open Dental waits up to 30 s for it to finish and keeps every image it then delivers. |
| `AcquireTimeoutMinutes` | `0` (off) | `0` until the safe-disable test passed on this computer; then a value in the order of section 4.3 | Stops a slot in which no image arrived after this many minutes, which ends the series. It counts per slot, from the moment the sensor software opens. It is honoured only for Twain Radiograph slots of a source listed in `DisableIsSafeSources` that accepted safe disable for that slot; otherwise the capture log says why not. When it expires, the series ends without a notice, like a cancel. |
| `Allow16Bit` | `0` | `0` | Not used by Mount / Acquire. |

Leave `DisableIsSafeSources`, `SafeDisableCap` and `RefusedDisableCodes` empty when the installed contract version has no safe disable. Then Cancel shows a warning first, `AcquireTimeoutMinutes` is not honoured, and any stop that Open Dental starts can lose an exposure that the sensor software still holds (section 10.3).

### 4.1 Finding the exact product names

1. On the computer, open Setup > Imaging > Devices and click **Add**.
2. Open the **Twain Name** list. It shows every TWAIN source installed on this computer; the source gate hides none of them.
3. Copy the names exactly, then close the window without saving.
4. Ask the staff which scanners they use on this computer: Scan Document, Scan Multi-Page, Scan Radiograph, Scan Photo, and any film or phosphor plate scanner. Include the default scanner from Setup > Imaging > Scanning (Set Default Scanner).

### 4.2 Examples

A computer that has not passed acceptance:

```ini
[Twain]
ThreadModal=1
RestrictSources=1
AllowedSources=<product name of the document scanner>
JournalKeep=200
AcquireTimeoutMinutes=0
```

The test computer, or a computer that passed acceptance:

```ini
[Twain]
ThreadModal=1
RestrictSources=1
AllowedSources=<product name of the document scanner>|<product name of the sensor's source>
JournalKeep=200
; Only when the installed contract version has safe disable.
; On other computers than the test computer, only after the
; safe-disable test passed on the test computer.
DisableIsSafeSources=<product name of the sensor's source>
SafeDisableCap=<capability number from the contract, hex>
RefusedDisableCodes=<first code from the contract, hex>|<second code, hex>
; 0 until the safe-disable test passed on this computer (section 4.3).
AcquireTimeoutMinutes=0
```

Replace each `<...>` with the real value. A value that is still a placeholder reads as missing.

### 4.3 Timeouts

The order must be:

**the source's own session timeout < `AcquireTimeoutMinutes` < automatic logoff time**, and **automatic logoff time > the longest series**.

1. **Find the automatic logoff time** that applies to each capture user. It is set in Setup > Security, Global Security Settings: "Automatic logoff time in minutes (0 to disable)". If "Allow user override for automatic logoff" is checked there, a user's own value in the user's edit window applies: "Automatic logoff time in minutes (0 is disabled, blank is global value)".
2. **Compare it with the longest series.** Time the longest series (usually a full-mouth series) on the phantom. Time spent in the sensor software and pressing the sensor's controls does not count as activity in Open Dental, so a whole series counts as idle time. Every capture user's logoff time must be longer than the longest series, with a margin: for example 30 minutes for a series that takes 20. If it is shorter, raise the office-wide value or that user's own value.
3. **AcquireTimeoutMinutes**, only after the safe-disable test passed on this computer: shorter than the logoff time, and longer than one slot normally takes (placing the sensor and exposing).
4. **The source's own session timeout**, if its contract has one: when you use `AcquireTimeoutMinutes`, set the source's session timeout shorter, so that the source ends the session cleanly with its own reason before Open Dental stops it. This is the one installation setting that may differ from its neutral value; record it. With `AcquireTimeoutMinutes=0` it may stay neutral.

Example: source session timeout 4 minutes, `AcquireTimeoutMinutes=5`, automatic logoff 30 minutes.

Without safe disable, a stop that Open Dental starts (Cancel anyway, an automatic logoff) can make the source wait while the sensor software closes; the contract says how long, often about 15 s. Open Dental looks busy during that time. Wait. Never end Open Dental in Task Manager during a series.

## 5. Updating the sensor's source

When the supplier releases a new version of the source:

1. Get the contract for the new version and record its version line.
2. Check the installation settings again (section 2, step 5). An installer can reset them.
3. Check that the product name is unchanged. If it changed, update the device's Twain Name (section 6), `AllowedSources` and `DisableIsSafeSources` on every computer.
4. Before patient use, repeat acceptance steps 1, 2 and 5 (section 12) and one phantom series on the test computer.

## 6. Set up the imaging device in Open Dental

1. Open Setup > Imaging > Devices and click **Add**. The Edit Imaging Device window opens.
2. **Description:** a plain name, for example "Intraoral sensor". Staff pick the device by this name.
3. **Computer Name:** the test computer until it passed acceptance (on that computer, click **This**). After acceptance, either leave it blank (all computers; the source gate still blocks the sensor where it is not listed) or add one device row per accepted computer.
4. **Device Type:** Twain Radiograph (one exposure per slot). Twain Multi only on the test computer, for the phantom batch test.
5. **Twain Name:** pick the sensor's source from the list. Do not type it. It is stored as data and must be the exact product name.
6. **Show Twain UI:** checked. The sensor software's window is where the operator sees the sensor's state.
7. Click **Save**.

**Scanning on capture computers:**

- Never choose the sensor's source as the default scanner (Setup > Imaging > Scanning, Set Default Scanner), or in the Select Source window. The Scan buttons have none of the capture protections (no journal, no Capture window). On a computer where the sensor is allowed, the source gate cannot tell a scan from an X-ray.
- Leave "Show Select Scanner Window" unchecked on capture computers, so scans always use the default scanner.

## 7. Set up the mounts

Open Setup > Imaging > Mounts and edit or add the mount definitions (for example BWX-4 and FMX-18).

1. **Item Order** is the exposure order. Group the slots that use the same sensor size, so that the sensor is changed at most twice per series.
2. **Slot size 1:1.** Make each image slot's width and height, in mount pixels, equal to the pixel size of the image delivered by the sensor size used in that slot. Swap width and height when the slot rotates by 90 or 270 degrees. Take the image sizes from the source's contract, or from the `Width` and `Height` lines of a phantom exposure's journal sidecar (section 11).
3. **Rotate degrees when Acquire** (per slot: 0, 90, 180 or 270): confirm each one with the phantom (section 12, step 4). Leave **Flip on Acquire** (whole mount) off unless the phantom shows that it is needed.
4. **Tooth Numbers** per slot.
5. **Default Cat:** the X-ray category.
6. **Use Adj Mode After Series:** as the office prefers.
7. **Measurement Scale:** fill it in only when every image slot is 1:1 (step 2). Then:
   - **Scale, pixels per unit:** the delivered resolution in pixels per inch divided by 25.4 (for example, 1270 dpi gives 50);
   - **Decimal Places:** 1;
   - **Optional Units:** mm.

   Otherwise leave it empty, so that nobody measures with a wrong scale. Measurements are approximate: the scale assumes the reported resolution and is not calibrated.
8. Text items (labels) cannot hold images.

## 8. Taking X-rays

### 8.1 A series (BWX or FMX)

1. Select the patient and open Imaging. In the tree, select a category or another item that is not a mount.
2. Click **Mount / Acquire**. In Select Mount and Acquire, pick the mount and the device ("Intraoral sensor"), then click **Create Mount and Acquire**.
3. The sensor software opens for the first slot. Open Dental's windows are locked, and a small **Capture** window says "Sensor software is open. Cancel ends this series." If the capture quarantine holds images, it also says "N unassigned capture(s) on this computer."
4. Expose. With Twain Radiograph the sensor software closes by itself after one image. The image appears in the slot, with that slot's rotation and tooth numbers.
5. The sensor software opens for the next empty slot, and so on. After the last slot the series ends, and Adj mode opens if the mount has it set. Open Dental comes back to the front about 1 s later, unless another program is in front.
6. Check every slot before the patient leaves.

### 8.2 Ending a series early: close, Done or Cancel

- **Preferred:** when the sensor software opens for a slot you do not want, close its window before exposing. The series stops without a notice, and the unmounted bar stays open.
- **Batch (Twain Multi, phantom only):** end with **Done** in the sensor software, or close its window. Every image taken arrives, in order.
- **Cancel** in the Capture window is for when the sensor software cannot be reached or does not respond.
  - **Without safe disable**, the first Cancel shows a warning instead of stopping. For one exposure per slot: "If an exposure was just taken, wait until it appears before cancelling." For a batch: "Cancel here may lose images already taken in this series; use Done in the sensor software to keep them." **Keep waiting** goes back. **Cancel anyway** stops, and an exposure that the sensor software still holds may then be lost for Open Dental: follow section 10.3.
  - **With safe disable**, Cancel stops at once, without a warning, and no image is lost. An image that the source still delivers goes to the capture quarantine, not into the mount, and an administrator must recover it (section 10.2). Done or closing the sensor software is therefore still better.
  - Closing the Capture window (for example with Alt+F4) counts as Cancel.
- **"Waiting for the sensor software to finish."** After a stop, the source may finish first. This can take up to 30 s, and Cancel is off meanwhile. Wait.
- Do not close Open Dental and do not log off during a series.

### 8.3 Single PA

1. In the tree, select a category or another item that is not a mount. This step matters.
2. Click **Mount / Acquire**, pick only the device, and click **Acquire**.
3. One image goes into the selected category, and the acquisition ends.

If a mount is selected instead, the image goes into that mount's next empty slot, with that slot's rotation and tooth numbers, and the sensor software opens again for each remaining empty slot (close it to stop). If that mount is full, the image becomes an unmounted item of that mount.

### 8.4 Retake

The Retake button moves the image but starts no acquisition (a known limitation of this version). To retake:

1. The dentist decides that a retake is needed.
2. Move the rejected image out of its slot: select the slot and click **Retake** on the unmounted bar. The image moves to the unmounted items and is kept. Nothing else starts.
3. Select the now empty slot.
4. Click **Mount / Acquire**, pick only the device, and click **Acquire**.
5. The sensor software opens for that slot. After it, the acquisition continues with every other empty slot of the mount. Close the sensor software when you are done.

Never delete the rejected image during the series. Deleting needs the ImageDelete permission, as usual.

## 9. Notices and what they mean

Capture notices appear in a small window titled **Capture**. One window lists every notice of one acquisition. Each notice is also written to the capture log (section 11).

| What you see | What it means | What to do |
|---|---|---|
| "This TWAIN source is not enabled on this computer.", usually followed by Open Dental's own "Imaging Device unavailable." or "Default scanner could not be opened" | The source gate refused the source. | On a computer that has not passed acceptance, this is expected for the sensor. For a scanner, add its exact product name to `AllowedSources`. |
| "Sensor software is open. Cancel ends this series." | Normal: the sensor software is open for a slot. | Expose, or close the sensor software to stop. |
| "N unassigned capture(s) on this computer." In the Capture window. After 30 days it adds "The oldest is from (date). Ask an administrator to recover them now (see the runbook)." | The capture quarantine holds images. | Tell the administrator (section 10.2). |
| "If an exposure was just taken, wait until it appears before cancelling." or "Cancel here may lose images already taken in this series; ..." | Cancel was clicked without safe disable. | Click Keep waiting, and close the sensor software or use Done instead (section 8.2). |
| "Waiting for the sensor software to finish." | The source is finishing after a stop (up to 30 s). | Wait. |
| "N image(s) received after the series stopped were saved to the capture quarantine folder (time). Do not re-expose. Ask an administrator to recover them (see the runbook)." | Images arrived after the series stopped, or the source sent more images than asked for. They are safe, but not filed. | Do not re-expose. Note the patient, the mount and the time. The administrator recovers them (section 10.2). |
| "N image(s) received after the series stopped could not be saved to the capture quarantine folder (time): (reason). A copy is in the capture journal folder. Do not re-expose. ..." | As above, but the quarantine could not be written. When the notice mentions the journal, the journal copy is the only copy. | Do not re-expose. The administrator fixes the folder (permissions, disk space) and recovers the journal copy as in section 10.2. |
| "The sensor software may still hold images from this series. Do not re-expose. See the runbook." | The source did not finish within 30 s after a stop. Images it took may still be in the sensor software. | Section 10.3. |
| "Sensor software ended without an image: (reason). Do not re-expose until you have checked the sensor software." | The source ended the session with a reason: for example no sensor connected, no calibration, a session timeout, or a failure. The reason is the source's text, or a hex code whose meaning is in the source's contract. | Section 10.4. |
| "Image not received. Do not re-expose until you have checked the sensor software." As Open Dental's message box, or as a notice. | A transfer failed, or the source stopped responding. | Section 10.4. |
| "Unable to save: (message)" (Open Dental's own message box) | The image arrived but could not be stored. Its journal copy may be the only copy. | Section 10.1, before anything else. |

## 10. Failure procedures

### 10.1 "Unable to save": recover from the journal

The image reached Open Dental, and its journal copy may now be the only copy. The journal keeps only the newest `JournalKeep` copies, so recover the image **before the series is restarted** and before more X-rays are taken on this computer.

1. **Stop.** Do not re-expose. Do not restart the series.
2. **Note** the time of the message, the patient, the mount and the slot that stayed empty.
3. **Fix the cause** of the save failure, for example storage that cannot be reached. Ask the administrator if needed.
4. **Find the journal copy.** Open `%ProgramData%\OpenPMS-Dental\Imaging\Capture\journal` in Explorer and sort by name; names sort in receive order. Open the `.txt` sidecars of the newest `.bmp` files. The copy you want:
   - has `ReceivedAt` a few seconds before the message;
   - has `Source` = the sensor's product name, and `Computer` and `WindowsUser` of this session;
   - has `Delivered=Caller`;
   - has no matching "Radiograph Created" entry for the patient in Tools > Audit Trail (permission ImageCreate; opening the audit trail needs the AuditTrail permission). The images that were saved have one each, at the time of their own journal copy.
5. **Second person.** A second person checks that the receive time matches the failed save, and that the image matches the patient and the empty slot (anatomy, the slot's teeth, the rest of the series). If either person is not sure, do not import; ask the dentist.
6. **Import.** In Imaging, select the patient, the mount and the empty slot. Click **Import** and choose the journal `.bmp`. It goes into the selected slot and gets that slot's tooth numbers. Its date is the time the journal copy was written, which is the receive time. For a single PA, select the X-ray category instead of a mount.
7. **Finish the image.** Import does not apply the slot's acquire rotation, and it marks a `.bmp` as a photo:
   - rotate or flip the image to match the slot;
   - click **Info** and set Type to Radiograph and tick Print Heading.
8. Only now continue with the remaining empty slots (section 8.4, steps 3 to 5).

Import the image only once. Leave the journal copy in place; the journal removes old copies by itself.

### 10.2 Quarantine recovery (administrator)

Quarantine images are marked "patient unknown": Mount / Acquire does not tell the TWAIN client which patient a series is for. Never file one by guesswork. Recover each one like this:

1. **Read the sidecar.** Open `%ProgramData%\OpenPMS-Dental\Imaging\Capture\quarantine`. For each `.bmp`, open the `.txt` of the same name and note `ReceivedAt`, `Source`, `Computer`, `WindowsUser`, `AcquireId`, `Reason` and `StopReason`. A `.bmp` without a sidecar is also "patient unknown"; its name starts with the receive time (`yyyyMMdd-HHmmss-fff`).
2. **Read the capture log.** In `Capture\capture-log.txt` (and `capture-log.1.txt` for older entries), find the line "Quarantine image (file name) ...". The lines before it, "Journal copy ... (acquire ..., delivered: Caller)", are the images of the same series that went into Open Dental, with their times.
3. **Find the series in the audit trail.** Open Tools > Audit Trail. Choose that day and the ImageCreate permission. Look for the entries from the same computer at the times of those journal-copy lines: a "Mount Created" entry and "Radiograph Created" entries for one patient. That patient's series is the candidate. Allow for a small difference between the computer's clock and the database server's clock.
4. **Find the empty slot.** Open the candidate mount. The image belongs to the slot that was next when the series stopped: the empty slot after the last filled one. A `Reason` that says the source sent more images than asked for is an extra exposure of the same series.
5. **Second person.** A second person checks the image against the patient: the anatomy fits the slot's teeth and the other images of the series, and nothing contradicts it. If either person is not certain, stop here: leave the file in the quarantine, record it, and refer it to the dentist.
6. **Import.** Select the patient, the mount and the empty slot. Click **Import** and choose the quarantine `.bmp`. Every quarantine image also has a journal copy with the same `Sha256`, whose sidecar says `Delivered=Quarantine`. Import only one of the two. Then finish the image as in section 10.1, step 7.
   - If the slot is no longer empty (for example, the slot was re-exposed), select the patient's X-ray category instead of the mount, import the image there, and in Info set its Description to "Recovered: (mount) slot (n)". Every exposure that reached Open Dental is kept.
7. **Record** the date, the file name, the patient, the mount and slot, who recovered it and who checked it.
8. **Move the files** (`.bmp` and `.txt`) out of the quarantine folder into a protected archive folder, for example `%ProgramData%\OpenPMS-Dental\Imaging\Capture\recovered`, and keep them per the office's retention policy. Nothing is removed automatically. Until a file leaves the quarantine folder (subfolders count too), the Capture window keeps counting it, and after 30 days it warns.

If `Reason` says that the source sent more images than the one asked for, also check the source's installation settings (section 2, step 5): a mode that overrides the image count is not at its neutral value.

### 10.3 "The sensor software may still hold images"

Open Dental stopped the source, but the source did not finish within 30 s. Images it took may still be held by the sensor software. Open Dental did not receive them, so they are in neither the journal nor the quarantine.

1. Do not re-expose.
2. Recover them with the source's own recovery, as the source's documentation and contract describe. This happens outside Open Dental. Some sources offer undelivered images again only for their most recent session and only for a limited time, so do it promptly, and before anyone starts another series with that source on that computer.
3. If the recovery produces image files, file them like a journal copy: match the time to the stopped slot, have a second person check, then Import (section 10.1, steps 5 to 8).
4. Without safe disable there is no such notice. Treat any stop that Open Dental started during a slot in which an exposure may have been taken (Cancel anyway, an automatic logoff, closing Open Dental) the same way.

### 10.4 "Ended without an image" and "Image not received"

1. Do not re-expose.
2. Look at the sensor software's window if it is still open: it usually shows the reason.
3. For "ended without an image", read the reason. A hex code (for example `0x80nn`) is explained in the source's contract. Fix the cause first: connect the sensor, restore its calibration, or restart the sensor software.
4. For "Image not received", check the journal for a copy received at that time. If there is one, recover it (section 10.1). If there is none, the source may still hold the image (section 10.3).
5. Re-expose only after these checks, and only when the dentist decides to.

### 10.5 Automatic logoff or Open Dental closing during a series

The automatic logoff closes Open Dental's windows. The series then stops, and any image that arrives afterwards goes to the quarantine. The same happens when Open Dental is closed during a series. The logoff can also close a Capture notice before anyone reads it.

1. Log back in. Do not re-expose.
2. Open the mount and note the last filled slot.
3. Ask the administrator to check the capture quarantine and the capture log for images received around the logoff (section 10.2).
4. Without safe disable, also follow section 10.3.
5. Then make sure the logoff time is longer than the longest series (section 4.3).

## 11. Capture folders, log and backup

Everything is under `%ProgramData%\OpenPMS-Dental\Imaging\Capture`:

| Path | Contents |
|---|---|
| `journal\` | A copy of every image the sensor delivered to Mount / Acquire, written before Open Dental stores it, and of every quarantine image. Only the newest `JournalKeep` are kept. |
| `quarantine\` | Images that arrived after a series stopped. Nothing is removed automatically. |
| `capture-log.txt` | One line per event, with the time and the Windows user. Above 1 MB it becomes `capture-log.1.txt`, replacing the previous one. |

**Files.** Each image is a `.bmp` file: the image exactly as the sensor delivered it, uncompressed. It has a `.txt` sidecar of the same name. Names look like `20261025-143012-123-000017-a3f9`: the receive time to the millisecond, then a counter and a random part. Names sort in the order the images arrived.

**Journal sidecar fields:**

- `ReceivedAt`, `Source`, `Computer`, `WindowsUser`, `AcquireId` (ties together the journal, quarantine and log entries of one acquisition);
- `PatNum`: always empty with Mount / Acquire;
- `Delivered`: `Caller` (Open Dental received it and may have stored it), `Quarantine` (it went to the quarantine; `DeliveredDetail` names the file) or `None` (this copy is the only one);
- `Width`, `Height`, `BitsPerPixel`, `XDpi`, `YDpi`, `Sha256`;
- the `Twain.*` values in effect, `ThreadModalInEffect`, `SafeDisableInEffect`, and `ConfigFile` (which capture.ini was read).

**Quarantine sidecar fields:** the same origin and image fields, `Patient=patient unknown`, and three fields that say why the image is there:

- `Reason`: the explanation in words;
- `DrainReason`: `Stop` (it was waiting when the series stopped), `RefusedDisable` (the source delivered it while finishing after a stop) or `ExtraImage` (the source sent more images than asked for);
- `StopReason`: why the series stopped: `Cancel`, `Timeout` (`AcquireTimeoutMinutes`), `ParentDestroyed` (the window that started the series was closed, for example by an automatic logoff), `Quit` (Open Dental was closing), `Closed`, `Error` or `None`.

**What to look for in the capture log:**

| Line starts or contains | Meaning |
|---|---|
| "The source gate ([Twain] RestrictSources=1) refused" | A source not in `AllowedSources` was refused. |
| "Safe disable is in effect for" | Safe disable was accepted for this slot. Expected on every slot once it is configured. |
| "does not support the safe-disable capability" or "did not accept TRUE for the safe-disable capability" | The installed source has no safe disable, or the number is wrong. Check the contract and the keys. |
| "[Twain] AcquireTimeoutMinutes is not honoured" | Says why the timeout was ignored. |
| "refused MSG_DISABLEDS (condition code ...). Waiting up to 30 s" | Normal after a stop with safe disable. |
| "Contract deviation:" | The source refused a stop with a code that `RefusedDisableCodes` does not list. Check the codes against the contract; if they are right, report the lines to the supplier. |
| "No notification from ... The source may still hold images" | Section 10.3. |
| "ended the session with condition code" | The end reason behind an "ended without an image" notice. |
| "Journal copy" / "Quarantine image" | Each copy written, with its file name and acquisition id. |
| "delivered more images than the one asked for" | Extra images went to the quarantine. Check the installation settings. |

**Permissions.** The local Users group needs Modify on these folders (section 3). When a permission problem stops the journal or the quarantine, the capture log says so.

**Backup.** Add `%ProgramData%\OpenPMS-Dental\Imaging` to the computer's backup. It holds patient images.

## 12. Acceptance (CAP-0)

Run this on the test computer first, with a phantom and a test patient record only. Write the results into the acceptance record (section 12.2).

**Prepare:**

- A phantom with radiopaque laterality and orientation markers (a lead letter plus the sensor's orientation mark) for each slot. Orientation is judged against the markers.
- A line-pair or step-wedge phantom, and written pass criteria agreed with the dentist before the run (for example the line pairs that must be visible and the steps that must be distinguishable). The reference is the original image from the sensor manufacturer's own imaging software.
- capture.ini on the test computer as in section 4.2, with the sensor in `AllowedSources` and, when the installed contract version has safe disable, the three safe-disable keys. Keep `AcquireTimeoutMinutes=0` until step 5.6 passes.
- The device row bound to the test computer (section 6), and the mounts (section 7).

### 12.1 Steps

1. **Installation and source gate.**
   1. The Twain Name list shows the sensor's source (with `twain_32.dll`, or with `TWAINDSM.dll` if needed). Record which data source manager is in use.
   2. The sensor software starts when Open Dental runs as an ordinary Windows user. Record whether it needs write access to its own installation folder.
   3. Every installation setting in the contract has its neutral value (or the recorded exception of section 4.3), checked by a person or from the source's log. If the contract lists more than one kind of delivered 8-bit image, record which one real exposures deliver.
   4. Record the contract version (and its revision identifier while it is a draft).
   5. On a computer that has not passed acceptance (`RestrictSources=1`, sensor not listed), Mount / Acquire with the device shows "This TWAIN source is not enabled on this computer." **Scan Document still works** there, and on the test computer.
2. **Single PA.** With a category selected (not a mount), take one image end to end (section 8.3). Measure the time from the sensor software opening to the sensor being ready; the target is under 5 s. Open the image's journal sidecar: `ConfigFile` is the ProgramData capture.ini, the `Twain.*` values are the ones you set, and `SafeDisableInEffect=1` when safe disable is configured. Then, with a mount selected, check that the image goes into the next empty slot and that the sensor software opens again for the remaining slots.
3. **Series.** Take a BWX-4 and an FMX-18 with Twain Radiograph. Every slot fills in order, and each image has a journal copy (`Delivered=Caller`). On the phantom only, run one batch with Twain Multi and skip one exposure on purpose: confirm that every later image shifts by one slot, as section 1 warns.
4. **Orientation.** For each slot and each sensor size, the image matches the phantom's markers. Adjust and record the mount rotations until they do. Judge image equivalence on the line-pair or step-wedge phantom against the written criteria.
5. **Fault injection.**
   1. **Close the sensor software** without exposing: the series stops without a notice.
   2. **Unplug the sensor** before a slot, then close the sensor software: the notice "Sensor software ended without an image" gives the reason.
   3. **No calibration**, produced as the contract or the supplier describes: the reason is shown.
   4. **Save failure:** on the test patient, make storage fail just before an exposure (for example, remove write access to the test patient's image folder, or disconnect the image share), then restore it. "Unable to save" appears, and journal recovery (section 10.1) brings the image into the mount.
   5. **Stop with images pending (safe disable):** on the phantom, with a Twain Multi batch, take 3 exposures and click Cancel before Done. No warning appears; all 3 images arrive in the quarantine with the quarantine notice. With Twain Radiograph, click Cancel while the sensor software is still processing an exposure: that image arrives in the quarantine. The capture log shows "refused MSG_DISABLEDS ... Waiting up to 30 s", and no "Contract deviation" or "may still hold images" lines.
   6. **Stop while idle (safe disable):** click Cancel while the sensor software waits for an exposure. "Waiting for the sensor software to finish." appears briefly, then the series ends. The capture log shows the refused stop followed by success within the wait. Only after 5.5 and 5.6 pass, set `AcquireTimeoutMinutes` (section 4.3).
   7. **The source's session timeout first:** with the source's session timeout shorter than `AcquireTimeoutMinutes`, leave a slot waiting. The source ends the session with its own reason ("Sensor software ended without an image: ...") before Open Dental's timeout.
   8. **Open Dental's timeout before the logoff:** with the source's session timeout neutral, leave a slot waiting. The series ends after `AcquireTimeoutMinutes`, without a notice, and Open Dental is still logged in.
   9. **Automatic logoff during an acquisition:** give a test user a short logoff time (for example 2 minutes) and `AcquireTimeoutMinutes=0`. On the phantom with Twain Multi, take 2 exposures and wait without clicking Done. The logoff stops the series, both images are in the quarantine, and their sidecars' `StopReason` says why the series stopped. Restore the logoff time afterwards.
6. **Patient change and logoff time.** While the sensor software is open, try to select another patient in the main window: it is locked. Confirm that every capture user's automatic logoff time is longer than the longest series (section 4.3).
7. **Storage.** Repeat one series in each storage mode the clinic uses.
8. **Quarantine recovery.** Recover one quarantine image from step 5 with the procedure in section 10.2, on the test patient.
9. **Rollback.** On the test computer, do rollback steps 1 and 3 (section 13). With the sensor removed from `AllowedSources`, Mount / Acquire with it is refused and Scan Document still works. With `ThreadModal=0`, a phantom slot runs without the Capture window, and the main window is not locked during the slot: confirm that it responds, without changing the patient. Then restore the settings, including `ThreadModal=1`.

### 12.2 Acceptance record

Record at least:

- the date, the computer name, the people present, and the Open Dental build;
- the source's version and the contract version (with its revision identifier while it is a draft);
- the data source manager in use;
- every installation setting and its value;
- the full `[Twain]` section of capture.ini;
- the automatic logoff time for each capture user, and the timed longest series;
- the result of each step in 12.1, with the measured per-slot time;
- the dentist's sign-off of image equivalence.

### 12.3 Further computers

Each further computer runs a shorter acceptance before patient use there:

1. Do sections 2, 3 and 4 on it. For the acceptance run, list the sensor in `AllowedSources` and, once the safe-disable test passed on the test computer, in `DisableIsSafeSources`, with `AcquireTimeoutMinutes=0`. Make the device available there: a device row for this computer, or a blank Computer Name.
2. Until it passes, use only the phantom and the test patient record on that computer.
3. Run steps 1, 2, 5.5 and 5.6, then set `AcquireTimeoutMinutes` (section 4.3), then run steps 5.8 and 6 and one phantom series.
4. If a step fails, remove the sensor from `AllowedSources` on that computer until the cause is fixed.
5. Add the results to the acceptance record.

## 13. Rollback

Use the smallest step that solves the problem. Settings take effect from the next slot or scan.

1. **Stop sensor capture on one computer:** remove the sensor's product name from `AllowedSources` (keep `RestrictSources=1`). Mount / Acquire with the sensor is then refused with the gate notice.
2. **Stop it everywhere:** set the device row's Computer Name back to the test computer, or delete the device row (Setup > Imaging > Devices).
3. **Problems with the locked windows or the Capture window:** set `ThreadModal=0`, only to troubleshoot with the phantom and the test patient, or with the series supervised. Open Dental's main window then stays usable during a slot, there is no Capture window and no Cancel, and nothing stops a patient change in the middle of a series: staff must not click in Open Dental or change the patient until the slot ends. Report the problem, and set `ThreadModal=1` again as soon as it is solved.
4. **Remove the source gate:** set `RestrictSources=0`. Every source can then be opened again, so do this only on a computer where the sensor's source is not installed (step 5). Elsewhere it could be opened like any scanner.
5. **Remove the sensor's source:** uninstall it with the supplier's uninstaller.
6. Keep the Capture folder until every quarantine image is recovered (section 10.2).
7. **Check:** Scan Document works, and Mount / Acquire with the sensor is refused or the device is no longer listed.
