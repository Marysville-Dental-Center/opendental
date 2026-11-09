# Port note: EHR-3 (CCD and CCR stylesheets)

Package EHR-3 of `docs/specs/ehr-rebuild-plan.md` (section 7), on branch `ehr/ehr3-1` from `main` `81e701df5`. The owner answered E-1 to
E-19 as recommended on 2026-11-08; this package carries out E-11 (HL7 CDA-core-xsl v4.1.0-beta.2 and the AAFP `ccr.xsl` under Apache-2.0
with notices, the licence text beside file exports, `ccr.xsl` as a copy marked as modified, a modified copy of upstream's `CCD-full.xml` in
the tests), E-12 (XSLT runs only in a child process of the program's own exe, `/ehr-render`), E-13 (file exports write the stylesheets and a
rendered page; Patient Export one page per document) and E-14 (`a48f7ccc6` stands: a stylesheet that came with an e-mail never runs).

## 0. Design, before the code

- **Resources.** `OpenPmsDentalBusiness/Resources/Ehr/`: `CDA.xsl`, `cda_l10n.xml`, `cda_narrativeblock.xml` byte for byte from the tag,
  `ccr.xsl` the copy marked as modified, and `stylesheets-license.txt` (the Apache-2.0 text that file exports write). Each is a
  `ResXFileRef` of `System.String` with `;utf-8` in `Properties/Resources.resx`. `.gitattributes` turns line-ending conversion off for the
  folder, so a checkout keeps upstream's LF bytes and the SHA-256 the notices name.
- **The child** (`OpenPmsDental/Main Modules/EhrRenderChild.cs`), started as `OpenPmsDental.exe /ehr-render 1` (the second argument is the
  protocol version). Frames on standard input: a kind byte (1 CCD, 2 CCR), a 4-byte little-endian length, UTF-8 bytes. Replies on
  standard output: a status byte (0 the page, 1 the reason), a length, UTF-8 bytes. It ends with 0 at the end of its input.
- **The starter** (`EhrStylesheetStarter`, the second class of `EhrSummaryCcds.cs`): the limits (32 MB, 200 levels, no DTD) before any
  child, one frame at a time, 15 seconds per document, a new child after a failure in a batch, a seam (`FuncStartChild`) for tests.
- **Display.** `DisplayCCD` checks the limits on the text, renders through the starter, and gives the window the page
  (`htmlRendered`); without one the window shows the XML view with the reason.
- **Exports.** One helper per kind of site: `FormEhrSummaryOfCare.WriteCcdFiles` for the single-document file exports (the XML, the three
  stylesheet files, the licence text and `ccd.html`), `EhrSummaryCcds.GetCcdStylesheetFiles` for what e-mails attach.
