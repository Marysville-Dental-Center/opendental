# Capture metadata, version 1 (`capture-metadata/1`)

Status: 2026-10-31. Defined by the Open Dental fork for its native capture path (design section 7.9, package CAP-3). Reader: `ImagingCapture/Metadata/CaptureMetadataParser.cs`.

This document defines the JSON object a TWAIN source sends with each image it delivers, so that the fork can record where the image came from and how it was made. It names no source, sensor or vendor. A source's maker publishes which fields its source fills, and with which values, in the source's own contract.

## 1. Transport

- The object travels in the extended image information item `TWEI_TWAINDIRECTMETADATA` (0x124B, TWAIN 2.4 chapter 9): a handle holding UTF-8 JSON text, ended by a NUL. The application frees the handle the way it frees native image handles (with `DSM_MemFree` when the DSM supplied its entry points, otherwise with `GlobalFree`).
- The fork asks for it only when `ICAP_SUPPORTEDEXTIMAGEINFO` lists 0x124B. It sets `ICAP_EXTIMAGEINFO` to TRUE in state 4 when `MSG_QUERYSUPPORT` allows it, and calls `DAT_EXTIMAGEINFO / MSG_GET` in state 7: after `TWRC_XFERDONE` and before `MSG_ENDXFER`.
- An item the source does not return (`TWRC_INFONOTSUPPORTED`, or a failed call) means the image has no metadata. Capture never depends on metadata.
- One object per image.

## 2. Where the object may be

The reader takes the **first JSON object anywhere in the document whose `schema` member is the string `"capture-metadata/1"`**:

- The object may be the top-level value, which is the layout sources use today.
- It may be nested at any depth, inside objects or arrays, for example as a vendor-specific object inside TWAIN Direct metadata. The reader then needs no change.
- "First" means document order, depth first: an object is checked before anything nested in it, and members and array items are visited in the order they appear.
- Everything outside that object is ignored.

The image counts as having **no metadata** when the text is missing or empty, when it is not one well-formed JSON value (RFC 8259, no comments, no trailing commas), when it is nested more than 64 arrays or objects deep, when it is longer than 4 MiB, or when no object has that exact `schema` string. A byte order mark at the start and whitespace or NUL characters at the end are accepted.

## 3. Fields

Every field is a member of the object. "Nullable" means the source may send `null`. The reader is tolerant: a member that is missing, `null` or of the wrong type reads as absent, and a value that breaks a rule below is logged but never makes the whole object absent. Member names are case-sensitive.

| Field | Type | Nullable | Units and rules |
|---|---|---|---|
| `schema` | string | no | Exactly `"capture-metadata/1"`. |
| `manufacturer` | string | no | The device maker as the source reports it. Copied into DICOM Manufacturer (0008,0070). Never interpreted. |
| `model` | string | yes | The device model, null when the source does not know it. |
| `serial` | string | no | The sensor's serial number as the sensor software reports it. |
| `captureId` | string | no | Opaque. Unique per image, and **the same when the image is delivered again** (section 4). It lets the journal and the quarantine match a redelivered image to an earlier copy. |
| `sequence` | integer | no | The image's **1-based** number within its source session (from enable to disable). **A redelivered image keeps its original number.** |
| `acquiredAt` | string | no | When the image was taken or saved by the sensor software: ISO 8601 with milliseconds and the UTC offset, for example `2026-10-31T14:25:30.123+01:00` (`Z` for UTC is accepted). The PC's clock. |
| `pixelSpacingMm` | array of 2 numbers | no | `[x, y]`: the distance between pixel centres across a row (x) and down a column (y), in millimetres. Both positive. 25.4 / pixels per inch, for example `[0.02, 0.02]` at 1270 ppi. |
| `bitsStored` | integer | no | 8 or 16: the bit depth of the delivered image. |
| `effectiveBits` | integer | yes | How many low bits of a 16-bit sample are significant (1-16). **Null until the source's maker has measured it.** |
| `byteOrder` | string | yes | `"LE"` at 16 bits. **Null at 8 bits.** |
| `photometric` | string | no | `"MONOCHROME2"` when 0 is black, following `ICAP_PIXELFLAVOR` (`TWPF_CHOCOLATE`). |
| `window` | object | yes | `{low, high}`: integers, the source's own display window of a 16-bit image in raw sample values (its 0.5 / 99.5 percentile window). **Null at 8 bits.** The fork computes its own window for DICOM and does not use this one. |
| `rendering` | string | no | Which rendering the image is: `original`, `vendor8`, `vendor8-hd` or `window8` (section 3.1). A 16-bit image is always `original`. When `adjustments` is present, `rendering` names the image the adjustments were applied to. |
| `adjustments` | object | yes | **Present only when the source's installation adjustments were applied** to the delivered image; missing or null otherwise. See section 3.2. |
| `detectorType` | string | yes | The DICOM Detector Type (0018,7004) term if known: `DIRECT`, `SCINTILLATOR`, `STORAGE` or `FILM`. |
| `redelivered` | boolean | no | **Always present.** True when the image is delivered again by a redelivery (section 4), otherwise false. A reader treats a missing value as false and logs it. |
| `configuredExposure` | object | yes | Exposure values typed into the sensor software, never measured. See section 3.3. Null when nothing is set. |
| `dsVersion` | string | no | The source's version text (its TW_IDENTITY Version.Info). |
| `vendor` | object | no | Free content for the source's maker. **The fork ignores it.** |

A reader also accepts a number where a string is expected (a serial or version written as a number) and reads it as written.

### 3.1 Rendering values

| Value | Meaning |
|---|---|
| `original` | The sensor software's stored original. Every 16-bit image is this: not windowed, not enhanced, never adjusted or rotated. An 8-bit image is `original` only when the sensor software stored it at 8 bits. |
| `vendor8` | The sensor software's standard 8-bit display rendering. |
| `vendor8-hd` | The sensor software's high-definition 8-bit enhancement. |
| `window8` | The source's own linear window of the 16-bit image, reduced to 8 bits. |

### 3.2 `adjustments`

| Member | Type | Rules |
|---|---|---|
| `brightness` | integer | 0-100. 50 means unchanged. |
| `contrast` | integer | 0-100. 50 means unchanged. |
| `rotate` | integer | Clockwise degrees: 0, 90, 180 or 270. |
| `flip` | string | `none`, `h` (mirrored left to right), `v` (mirrored top to bottom) or `both`. **Applied after the rotation.** |

The object is **neutral** when brightness and contrast are 50 (or missing) and rotate is 0 and flip is `none` (or missing). It is **orientation-neutral** when rotate and flip are. A value the reader cannot read counts as not neutral. The fork warns when an image's adjustments are not orientation-neutral, because the slot's own rotation (design 7.10) would then be applied on top of the source's.

### 3.3 `configuredExposure`

| Member | Type | Units |
|---|---|---|
| `kv` | number | kV (tube voltage) |
| `ma` | number | mA (tube current) |
| `s` | number | seconds (exposure time) |
| `dap` | number | mGy·cm² (dose-area product) |
| `source` | string | always `"configured"` |

- **A value that is not set is left out, never null.** When nothing is set, the whole member is `null`. A reader treats an object with no value as null.
- These are the values configured in the sensor or generator settings, not measurements. The fork writes them only into DICOM Image Comments (0020,4000), labelled "configured, not measured", and **never** into KVP (0018,0060), X-Ray Tube Current (0018,1151), Exposure Time (0018,1150) or any other dose tag.

## 4. Redelivery

A source that can deliver undelivered images again (on the application's request, through a capability named in the source's contract) sends them with `redelivered: true`, the same `captureId` as the first delivery, and the original `sequence` and `acquiredAt`. The source knows no patient, so the fork places a redelivered image only after the operator confirms it, with its `acquiredAt` and `captureId` shown.

## 5. How the fork uses the fields

| Use | Fields |
|---|---|
| Journal and quarantine sidecars | `captureId`, `sequence` (and the raw text) |
| 16-bit DICOM header (design 7.8) | `manufacturer` → Manufacturer; `model` → Manufacturer's Model Name; `serial` → Device Serial Number and Detector ID; `acquiredAt` → Acquisition DateTime; `detectorType` → Detector Type; `effectiveBits` → the BitsStored rule; `pixelSpacingMm` → Imager Pixel Spacing when the image has no resolution; `configuredExposure` → Image Comments only |
| 8-bit captures | `serial`, `model` and `captureId` in an extra security-log entry for the document |
| Warnings | `adjustments` not orientation-neutral |
| Redelivery confirmation | `redelivered`, `acquiredAt`, `captureId` |

The fork never writes this metadata back to the source and never reads the source's own records.

## 6. Example

```json
{
  "schema": "capture-metadata/1",
  "manufacturer": "Example Imaging",
  "model": null,
  "serial": "SN-000123",
  "captureId": "c-7f3a91",
  "sequence": 3,
  "acquiredAt": "2026-10-31T14:25:30.123+01:00",
  "pixelSpacingMm": [0.02, 0.02],
  "bitsStored": 16,
  "effectiveBits": null,
  "byteOrder": "LE",
  "photometric": "MONOCHROME2",
  "window": { "low": 312, "high": 3900 },
  "rendering": "original",
  "detectorType": null,
  "redelivered": false,
  "configuredExposure": { "kv": 70, "ma": 7, "s": 0.125, "source": "configured" },
  "dsVersion": "1.1.0",
  "vendor": {}
}
```

The same object nested in a wrapper is read the same way:

```json
{ "metadata": { "vendorSpecific": [ { "schema": "capture-metadata/1", "captureId": "c-7f3a91", "redelivered": false } ] } }
```

## 7. Versioning

- The `schema` string carries the major version: `capture-metadata/<major>`.
- **Additive changes keep the string.** New members may be added to the object or to its nested objects at any time. Readers ignore members they do not know, so a version 1 reader keeps working.
- **A change that breaks a reader gets a higher major number** (`capture-metadata/2`): removing or renaming a member, changing a type, a unit or a meaning. A version 1 reader treats an object with a higher major number as **no metadata**. A source that wants to serve both kinds of reader may send a version 2 object together with a version 1 object, nested or side by side; a version 1 reader finds the version 1 object by the rule in section 2.
- The reader compares the `schema` string exactly: `capture-metadata/1.1`, `Capture-Metadata/1` and `capture-metadata/1 ` are not version 1.
