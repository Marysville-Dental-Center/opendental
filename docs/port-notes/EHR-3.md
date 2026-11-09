# Port note: EHR-3 (CCD and CCR stylesheets)

Package EHR-3 of `docs/specs/ehr-rebuild-plan.md` (section 7), on branch `ehr/ehr3-1` from `main` `81e701df5`; fix round 1 of the
review on `ehr/ehr3-2` from `ehr/ehr3-1` `f56e94014` (section 10; the sections above describe the result). The owner answered E-1 to
E-19 as recommended on 2026-11-08; this package carries out E-11 (HL7 CDA-core-xsl v4.1.0-beta.2 and the AAFP `ccr.xsl` under Apache-2.0
with notices, the licence text beside file exports, `ccr.xsl` as a copy marked as modified, a modified copy of upstream's `CCD-full.xml` in
the tests), E-12 (XSLT runs only in a child process of the program's own exe, `/ehr-render`), E-13 (file exports write the stylesheets and a
rendered page; Patient Export one page per document) and E-14 (`a48f7ccc6` stands: a stylesheet that came with an e-mail never runs).
No schema change, no new `PrefName`, no new third-party binary, no download beyond batch E, and no project file edited except the comment
of `tests/EhrTests`' own (it names EHR-3's tests). Synthetic data only.

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
- **Exports.** `FormEhrSummaryOfCare.WriteCcdFiles` for the single-document file exports (the XML, the three stylesheet files, the licence
  text and `ccd.html`), `WriteCcdPages` for Patient Export, `EhrSummaryCcds.GetCcdStylesheetFiles` for what e-mails attach.

## 1. The files (download batch E, `.git/od-tools/downloads/batch-e-20261109/`)

Every hash was checked against the batch's `MANIFEST.txt` before the files were copied; the files were read as data and run only through
the program's own child in tests.

| File in the repository | Source | SHA-256 |
|---|---|---|
| `OpenPmsDentalBusiness/Resources/Ehr/CDA.xsl` | CDA-core-xsl tag `v4.1.0-beta.2` = commit `bfb832026c19055faa182a7a8a4e5a2d6bd8796b`, unmodified | `f1c32f11186cf69d64f874cc4c94eb51ca8f696b54b4b6833fb54c6afdd7acc1` |
| `OpenPmsDentalBusiness/Resources/Ehr/cda_l10n.xml` | same tag, unmodified | `d8677885ef78afc0842484ecc70a65f8f17eea286e990bd39fa948cac7f1a884` |
| `OpenPmsDentalBusiness/Resources/Ehr/cda_narrativeblock.xml` | same tag, unmodified | `608b518182a1418e8865e5e8437083bad034762cf078750ec27c10d17a2285a9` |
| `OpenPmsDentalBusiness/Resources/Ehr/ccr.xsl` | the AAFP's "CCR XSL V2.0" file release, **modified** (below) | `1d84391fd7761a15d90f0b8a866ebf099a960f2b0c75ebdbb2e5b357b59af739` (the first copy, before fix round 1: `f9ce1d989571301902bfb90f5a1dcd13dbcc4a328ee4d878114fea5a036ede84`) |
| (the release as published, not in the repository) | `sourceforge.net/projects/ccr-resources/files/ccr-xslt-html/CCR%20XSL%20V2.0/ccr.xsl`, uploaded 2007-11-21 | `4ab70db9f8684f6b738f1e9cfa17f0165622f40c48a9f46105cd31a862cca4c0` |
| `OpenPmsDentalBusiness/Resources/Ehr/stylesheets-license.txt` | written here: a short header and the Apache-2.0 text of `THIRD-PARTY-NOTICES.txt`, word for word | `42fc960b2795cdae5e7835d393fad970f53ec932246f186611c580aee7643c74` |
| `tests/EhrTests/Golden/CCD-full.modified.xml` | upstream's `examples/CCD-full.xml` at commit `22977c4c788cccfca2de542436180e77b7c3f56d` (default branch, 2026-10-29; upstream SHA-256 `05d5ac38247a6e403bc7031ddfda2eff1f425de4ea0002ac116db02804dabd57`), **modified** (below) | `c6bbeffe1e6217cfb07aa682c8b16aade0f1f80fbd377174b9a88b1f63739f68` (as committed, LF line ends) |

The README of the tag (SHA-256 `8e27586b241522d8b806115b76a1a3b051c18685b1185e24a7e54e444b1bb03a`) was read for the licence and the
companions' names; it is not shipped. The tag has no LICENSE or NOTICE file, so the notice cites the README and the stylesheet's header.

**Which `ccr.xsl`, and why.** The pinned one is the "CCR XSL V2.0" file release (one self-contained file, header "Date 2007-06-01,
Version 2.0", reads its date tables with `document('')` as the plan describes). The SVN head (r46, `CcrXslt2/src/ccr.xsl`, 2009-09-18) is a
modular rewrite whose `ccr.xsl` imports or includes 60 other files (`sections/`, `templates/`, `xsllib/`); the child's resolver serves
exactly four resources and refuses everything else, so it cannot serve r46.

**Do the released dates render wrongly? Yes.** Before the copy was changed, the released file was embedded unmodified (commit
`3d29e9dff`) and a synthetic CCR (`tests/EhrTests/Golden/CCR-dates.xml`) went through the child. Its `date:format-date` took the last six
characters of any value as a time zone, unless the value ended with `Z`. So:

| `ExactDateTime` | Released `ccr.xsl` | The copy shipped |
|---|---|---|
| `2007-06-01` (a problem's onset) | `Jan 01, 2007` | `Jun 01, 2007` |
| `1980-12-31` (a date of birth) | `Jan 01, 1980` | `Dec 31, 1980` |
| `2008-11-23T14:05:00` (no time zone) | nothing | `Nov 23, 2008` |
| `2013-07-19T10:15:00` (a result, pattern with the time) | nothing | `Jul 19, 2013 : 10:15 AM` |
| `2010-08`, `2011` | nothing | `Aug 2010`, `2011` |
| `2015-05-06` (a vital sign, pattern with the time) | not recorded | `May 06, 2015` |
| `2009-02-28T23:59:59Z`, `2025-03-14T09:26:53-05:00`, `2012-04-05T08:00:00+02:00` | right | unchanged |

A date of birth shown as January 1 is a clinical-safety defect, so the copy marked as modified (E-11) fixes it. So is any part of a date
the document does not have: the first copy (before fix round 1) still showed `2010-08` as `Aug 01, 2010` and `2011` as `Jan 01, 2011`
(the template's month and day default to 1), and, recorded with that copy, a date without a time where the pattern shows one as
`May 06, 2015 : 12:00 AM`. The copy shipped now shows only the parts a value has.

**The changes of `ccr.xsl`** (`git diff 3d29e9dff 9c31c177e -- OpenPmsDentalBusiness/Resources/Ehr/ccr.xsl`; also listed in a comment
after the AAFP's header, which stays unchanged, with the released file's SHA-256):
1. `date:format-date` takes a time zone only when the value ends with `Z`, `+hh:mm` or `-hh:mm`, as EXSLT's own template does. And
   (fix round 1) it shows no part a value does not have: a year with the pattern `yyyy`, a year and month with `MMM yyyy`, and a date
   without a time with the pattern's date part (the part before its first quoted text, for the file's two patterns with `hh:mm`:
   `EEE MMM dd, yyyy 'at' hh:mm aa zzz` and `MMM dd, yyyy ':' hh:mm aa zzz`).
2. `date:format-date` shows a value it cannot read as it came, instead of nothing.
3. `date:_format-date`'s pattern letter `z` writes nothing for a value without a time zone, instead of "UTC".
4. The five `disable-output-escaping="yes"` attributes are removed (social history, the two result rows, the plan of care's order and
   goal `Description/Text`). The child's escaping writer stays as well.
5. The footer says that the page was made with a derived work: the AAFP's header says "Derived works MUST change the footer.xsl template
   to denote that the resulting HTML is a derived work from the AAFP's XSLT or remove the display" of the AAFP's name. This fifth change
   was not in E-11's list; it follows from the header once the copy is modified. The lead may want counsel to see it with E-11.

**The test copy of `CCD-full.xml`.** Every code of a code system other than HL7's own vocabulary (`2.16.840.1.113883.5.*` and the HL7
value sets used as code systems), CDCREC, CVX and HCPCS Level II, with its display name, was replaced with made-up values (`TEST0001` and
up, "TEST display 1" and up): 57 codes of LOINC, SNOMED CT, CPT, RxNorm, NDC, NUCC and CDC's HSLOC, also where the same code appears in
text or a comment, and where the display name appears in text. Three of them appear in comments only, and the first copy missed them
(the review found them; each is upstream's own text): SNOMED CT `418038007` with its name in two comments (now `TEST0055`, "TEST display
53"), SNOMED CT `46680005` (`TEST0056`) and LOINC `20565-8` in two comments (`TEST0057`). `CdaRenderTests.CcdFullFixture_HasNoLicensedCodes`
now scans the whole file, comments included, for tokens shaped like a LOINC code, a SNOMED CT identifier or a CDT code, and allows only
the fixture's known non-codes (dates, id extension values such as the made-up NPIs, CDCREC codes, the names of ISO standards); it finds
those four in the first copy. A comment after the XML declaration says it is a modified copy, with the source, the commit, the licence
and the changes. Nothing else changed. The golden page was recorded from the pinned tag's `CDA.xsl`, not taken from upstream's
`CCD-full.html`; it did not change when the comments did (`CDA.xsl` does not show comments).

## 2. The child and the starter

**Child** (`EhrRenderChild`, the only file of the program that names an XSLT class):
- `ProgramEntry.Main`'s first statement: `if(args.Length==2 && args[0]=="/ehr-render") { Environment.Exit(EhrRenderChild.Run(args[1])); }`,
  before `RenderOptions.ProcessRenderMode=SoftwareOnly` (which stays the next statement, O-34 (b1)), `Application`, `ODInitialize` and
  everything else. No window, database or `Application` call.
- `SetErrorMode(SEM_FAILCRITICALERRORS|SEM_NOGPFAULTERRORBOX)` first; an unknown protocol version ends it with 2. `Console.Out` goes to
  nothing, because standard output carries the frames only.
- Reads frames from `Console.OpenStandardInput()` (raw bytes). A leading UTF-8 byte order mark is skipped: on a computer whose code page is
  UTF-8, .NET Framework's `Process.StandardInput` writes one before the first frame (kind bytes are 1 and 2, so it cannot be mistaken).
- Decodes the document as UTF-8 (invalid bytes refuse it), checks the limits again, reads it with an `XmlReader` over a `StringReader`
  (`DtdProcessing.Prohibit`, no resolver, at most 32 M characters).
- Compiles each stylesheet once per process from an `XmlReader` whose base URI is `ehr-res:///CDA.xsl` or `ehr-res:///ccr.xsl`, with
  `XsltSettings(enableDocumentFunction:true,enableScript:false)`. Its `ResourceResolver` serves exactly the four files under those URIs
  (a relative path resolves against the stylesheet; `../` stays under `ehr-res:///`) and refuses every other URI: files, shares, the web,
  another name, a host, a query or a fragment. It is passed to `Load` and to `Transform`.
- CDA.xsl's parameters: `limit-external-images=yes`, `external-image-whitelist` empty, `limit-pdf=yes`, `useJavascript=false`.
- `xsl:message` text is dropped (an `XsltMessageEncountered` handler): without one, `XslCompiledTransform` writes it to the console, which
  would have broken the frames. Both stylesheets send messages; CDA.xsl ends a document with a `javascript:` reference with one
  ("Javascript injection attempt detected"), which becomes the reason.
- **`PageXmlWriter`**, the writer of both pages: for a CCR, text marked `disable-output-escaping` (`WriteRaw`) is written escaped; for a
  CCD it is written as it is (CDA.xsl's own CSS). For both, an attribute through which a page loads something by itself (`src`, `data`,
  `background`, `poster`, `srcset`, `xlink:href`, ...) is dropped unless its value is a `data:` image. This was not in the plan: the hostile
  test found that CDA.xsl writes a share path (`\\192.0.2.10\share\x.png`, which has no colon, so its sanitizer keeps it) as an image's
  `src`. An `href` of any element but `a` and `area` (`link`, `image`, `base`) and a namespaced one (`xlink:href`) count as such
  attributes, and so does `ping`. A link (the `href` of `a` or `area`, a `cite`) is dropped unless it starts, read as a browser reads it,
  with `#`, `http://`, `https://` or `mailto:` (fix round 1: `ccr.xsl` writes a CCR's reference locations as links, unchecked); the link's
  text stays. The CCD window opens only http, https and mailto links anyway, in the default browser. The golden pages did not change when
  the writer came in, nor when it began to check links.
- Each transform runs on a thread with a 16 MB stack; the page goes back as UTF-8 without a byte order mark. A watchdog thread ends the
  process (exit code 3) after 20 seconds without progress, so a child whose parent died does not linger. A frame it cannot read ends it
  with 4.

**Starter** (`EhrStylesheetStarter`, business library; no `File.`, `Path.GetTemp`, `FileStream` or `StreamWriter` in it, no database, so
nothing goes through the Middle Tier: the child runs on the computer that shows or exports the document):
- `GetLimitError`: empty, over 32 MB in UTF-8 bytes, deeper than 200 levels (counted with an `XmlReader`, which does not recurse), a
  document type declaration, or not well-formed XML: refused before any child starts. It fails closed (fix round 1): a document its reader
  cannot read to the end is refused ("The document is not well-formed XML: " and the reader's reason), because the depth after the place
  the reader stopped was never counted. The reader is `GetXmlReaderSettings` (no DTD, no resolver, characters checked);
  `LoadXmlDocument` builds the program's own `XmlDocument` with it, never with `LoadXml`, whose legacy reader accepts a character reference
  such as `&#0;` that stops the strict one. The child reads with the same settings.
- `Render`, `RenderBatch`: starts `ProgramNames.ExeFileName` from `AppDomain.CurrentDomain.BaseDirectory` with `UseShellExecute=false`
  (explicit), redirected streams and `CreateNoWindow`, through `FuncStartChild` when a test sets it (`StartProcess` is the real one). One
  frame out and one back at a time, on a thread, so neither pipe can fill; standard error is read all the time and kept up to 64 KB.
- 15 seconds per document (compiling the stylesheet included); then the child is killed and the reason says so. A crash, a broken frame or
  a non-zero exit code gives `null` and a reason with the exit code; `0x800703E9` (COR_E_STACKOVERFLOW) and `0xC00000FD`
  (STATUS_STACK_OVERFLOW) are named; the first line of standard error is added. In a batch the document is recorded as failed and a new
  child takes the rest; a document the stylesheet refuses keeps the child. A child that cannot be started gives every document the same
  reason. The document never goes to disk.

## 3. Display (`FormEhrSummaryOfCare.DisplayCCD`, `FormEhrSummaryCcdEdit`)

- `DisplayCCD` runs `GetLimitError` on the text first. A document over the limits, or not well-formed, never reaches `XmlDocument` (whose
  `InnerXml` writes recursively and whose parser would expand a DTD's entities in the program's process): the window opens with the text,
  no page, no reconcile buttons and the reason. The window shows such a text plain, encoded and not indented (the indentation grows with
  the depth squared), cut after 1 MB. Otherwise `DisplayCCD` builds its `XmlDocument` with `EhrStylesheetStarter.LoadXmlDocument`.
- Otherwise a CCD (`ClinicalDocument`) or a CCR (`ContinuityOfCareRecord`) is rendered through the starter, with or without an attached
  stylesheet; the attachment is never read (`strAlterateFilPathXslCCD` is only tested for being empty). Before, a CCR always showed as raw
  text, and a CCD without an attached stylesheet did too.
- The note: none when the page is shown and no stylesheet came along; "The stylesheet that came with this document was not run. The
  document is shown with the program's own stylesheet." with one; when rendering fails, the XML view with the old fallback line (when a
  stylesheet came along) or "The program's stylesheet could not show this document, so it is shown as XML", and the reason.
- `FormEhrSummaryCcdEdit(xmlCcd,htmlRendered,patCur,note)`: `BuildPage` shows the page with the note, encoded, as its first paragraph; the
  XML view without one. Print, find and the three reconcile buttons (which read the XML) stay; the window's settings are unchanged (scripts
  off, nothing from the web, no file but its own), and it reads and writes no file. The reconcile buttons check the limits again and read
  the text with `LoadXmlDocument` (`GetXmlDocumentForReconcile`), because the reconcile code walks the document recursively.
- A CCR has no reconcile buttons, as before (fix round 1): `DisplayCCD` gives the window `GetPatientReconcile(isCcr,patCur)`, no patient
  for a CCR, and the window shows the buttons only when `IsReconcileShown` (a patient of the database).

## 4. Exports and e-mails (E-13), the eight lines in six files

| Site | Now |
|---|---|
| `FormContinuityOfCareDocument.cs` (Chart > CCD export) | `FormEhrSummaryOfCare.WriteCcdFiles` |
| `FormEhrClinicalSummary.cs`, export | `WriteCcdFiles` |
| `FormEhrClinicalSummary.cs`, patient portal web mail | attachments `ccd.xsl`, `cda_l10n.xml`, `cda_narrativeblock.xml` (shown names; stored as `<base>_<name>`) |
| `FormEhrElectronicCopy.cs`, export | `WriteCcdFiles` |
| `FormEhrPatientExport.cs` | each written `<name>.xml` is collected; after the loop `WriteCcdPages` writes one `<name>.html` per XML through one batch child and one set of stylesheet files with the licence text. `ccd.xsl` replaces the former `CCD.xsl`, the name the documents' instruction uses |
| `FormEhrSummaryOfCare.cs`, export | `WriteCcdFiles` |
| `FormEhrSummaryOfCare.cs`, e-mail | attachments `ccd.xsl` and the two companions |
| `WpfControlsOD/Frms/FrmReferralsPatient.xaml.cs`, referral e-mail | attachments `ccd.xsl` and the two companions |

`WriteCcdFiles` writes `ccd.xml`, `ccd.xsl` (the CDA.xsl text, because `EhrCCD.cs:208` points to `ccd.xsl`), `cda_l10n.xml`,
`cda_narrativeblock.xml`, `stylesheets-license.txt` and `ccd.html`. When the page cannot be made, the other files are written, an older
`ccd.html` in the folder is deleted (it would show another document) and the "Exported" message says why. E-mails carry no licence text
and no HTML, as E-13 says; QRDA is untouched here.

## 5. Notices

- `THIRD-PARTY-NOTICES.txt`: two component entries after "Pearl API notice", "HL7 CDA-core-xsl" and "AAFP CCR stylesheet", with the
  files (embedded in `OpenPmsDentalBusiness.dll`), the version and tag or release, every SHA-256 (the released and the shipped `ccr.xsl`),
  the copyright line, "License: Apache License 2.0" (the text already in the file) and a note that Apache-2.0 fits GPL v3, not v2 (the
  program as distributed is conveyed under v3; E-11). The `ccr.xsl` entry lists the changes. No other entry was touched.
- `ReleaseIdentityTests.cs`: the two names in `_arrayShippedComponents`. Help > About > Licenses lists them by itself (`LicenseList` reads
  the embedded notices file), so `FormLicense.cs` needs no change.

## 6. The test that forbids XSLT (`tests/LibWebTests/HtmlGenerationTests.cs`)

- `NoStylesheet_FromAnEmail_RunsInTheProgram`: the source scan allows XSLT names in `RdlEngine\`, `DocumentationBuilder\` and exactly
  `OpenPmsDental\Main Modules\EhrRenderChild.cs`, and asserts it saw that file; `HtmlPage` has no `TransformXsl`; `DisplayCCD` never reads
  the attachment and keeps the fallback line. Its summary says why the child is allowed.
- `BuiltAssemblies_XsltOnlyInTheChild` (Mono.Cecil): `CodeBase.dll`, `OpenPmsDentalBusiness.dll`, `WpfControlsOD.dll` and
  `DataConnectionBase.dll` reference no `System.Xml.Xsl` type; in `OpenPmsDental.exe` every method whose signature, locals or IL use one,
  and every field of such a type, is in `OpenPmsDental.EhrRenderChild` or a type nested in it; `XslTransform` is used nowhere.
- `Child_HasOneWayIn_FirstInMain`: `EhrRenderChild.Run` has one caller, `ProgramEntry.Main`, and nothing else of the exe uses the class;
  the source shows the `/ehr-render` test as Main's first statement, `RenderOptions` right after it, and no `Application.`, `ODInitialize.` or
  `RenderOptions.` before it.
- `CcdView_KeepsTheDocumentInMemory_AndWritesNoFile`: the constructor's parameters are `xmlCcd`, `htmlRendered`, `patCur`, `note`;
  `DisplayCCD` contains `new FormEhrSummaryCcdEdit(doc.InnerXml,htmlRendered,patReconcile,note)` and checks the limits before
  `doc=EhrStylesheetStarter.LoadXmlDocument(strXmlCCD);`; the three reconcile buttons call `GetXmlDocumentForReconcile`, which checks the
  limits before `LoadXmlDocument`; neither file uses `.LoadXml(`; the window and `DisplayCCD` write no file; the starter's class has no file
  access.
- `CcdView_ReconcileButtons_OnlyForACcd` (fix round 1): `GetPatientReconcile` gives no patient for a CCR and the patient for a CCD;
  `IsReconcileShown` is false for none and for a patient not in the database; `DisplayCCD` passes `patReconcile` (never `patCur` itself) and
  the window's Load hides the buttons by `IsReconcileShown`.
- `CcdView_Page_RenderedXmlOrPlainText`: the window's page in its three forms. `WebView2PolicyTests` is unchanged (its pins still hold).

## 7. Tests

New in `tests/EhrTests` (every `.cs` file of the SDK-style project is compiled; the golden files are read from the repository folder and
are not project items). Every page comes from a real `OpenPmsDental.exe /ehr-render` started by the starter from the test folder; no XSLT
runs in the test process and no window is shown. `EHR_RECORD_GOLDEN=1` records the golden pages again.

- `CdaRenderTests` (7): CCD-full's golden page (no script, no `javascript:`, nothing loaded from a file, share or the web); the fixture has
  no licensed code anywhere, comments included (fix round 1, section 1); the CCR with dates (golden page, every date readable, a year and
  month as `Aug 2010`, a year as `2011`, a vital sign's date without a time as `May 06, 2015`, never `Aug 01, 2010`, `Jan 01, 2011` or a
  made-up `12:00 AM`; the accents kept, the derived work's footer); a batch of three in one child, each page equal to a single render; a
  batch with a document CDA.xsl refuses (a `javascript:` link) keeps the child, and a malformed one in it is refused before the child; a
  broken frame ends the real child with 4 and a new child takes the next document; an `xml-stylesheet` instruction pointing to the web is
  never followed.
- `CdaHostileTests` (17): 100,000 levels refused before any child (no child started) and shown as plain text; 200 levels pass, 201 do
  not; `&#0;` before 100,000 levels refused as not well-formed by `GetLimitError`, `Render` and `RenderBatch` with no child started, shown
  as plain text, and refused by `LoadXmlDocument` although `LoadXml` accepts it (fix round 1); 33 MB and 17 M two-byte characters refused
  before any child; a billion-laughs DTD and external entities to a file, a share and the web refused before any child; a malformed
  document refused before any child; the resolver serves the four files and refuses eleven other URIs; a CCD's narrative with script
  elements in two namespaces, event handlers, and links and images to a file, a share and the web: no script, handler or outside source in
  the page, the text kept; `javascript:` links in four spellings and a `javascript:` image reference: CDA.xsl refuses the document or the
  page has no script URL; a CCR whose five description texts hold a script element, a `javascript:` link, share and file images and an
  iframe: shown as text; a CCR whose reference locations are `javascript:alert(1)`, ` JaVa&#x09;ScRiPt:alert(2)`, `vbscript:x`,
  `file:///C:/Windows/win.ini` and `\\192.0.2.10\share\x.exe`: no such href, the text shown, a web link kept (fix round 1); the page writer
  (raw text escaped for a CCR, kept for a CCD; sources dropped except `data:` images; links kept only to `#`, `http://`, `https://` and
  `mailto:`, thirteen others dropped); `ccr.xsl` has no `disable-output-escaping`; through the seam: a child that stalls is killed at 15
  seconds (the test takes about 15 s) and the next document gets a new child, children ending with `0x800703E9`, `0xC00000FD` and 5 give the
  reason with the code and standard error's first line, a child that cannot start; frames round trip and broken frames throw.
  `AssertHarmless`, which every hostile page passes, now also requires every link to go to `#`, `http(s)://` or `mailto:`.
- `CdaResourceTests` (5): each resource's UTF-8 text and file have the pinned SHA-256; the `.resx` entries with `;utf-8` and the
  `.gitattributes` line; `GetEhrResource`'s names and `GetCcdStylesheetFiles`; the copy of `ccr.xsl` marked as modified (with the partial
  date patterns); the notices' entries and hashes, and the licence file is the notices' Apache-2.0 text, ASCII.
- `CdaExportTests` (5): a single export writes exactly the six files, each equal to its resource or render; without a page the older
  `ccd.html` goes and the message says why; Patient Export of three documents: three pages, one child, one set of stylesheet files; a
  document without a page is named; the eight lines of the six files use the helpers and none writes `ccd.xsl` alone.
- `CdaGeneratedCcdTests` (4): a summary of care, a clinical summary (with instructions) and a patient export of a patient named
  "Zoë Ångström-Núñez" in "Søndre Exampleton", built by `EhrCCD` on a scratch database `ehr3_<UTC start>_<process>_<random>_ccd` (from
  `db/init/new-database.sql` and the fork migrations, dropped at the end, stale `ehr3_` leftovers older than 12 hours dropped first;
  inconclusive without the server). Before rendering, GUIDs, the export's own time stamps and time-zone offsets are fixed, and the codes
  of systems other than HL7's, CDCREC, CVX and HCPCS (the LOINC section codes, the NUCC taxonomy) become TEST values, so no golden page holds
  a literal of the engine's codes. Synthetic rows: a provider with a made-up NPI, the practice, one SNOMED row `TEST0001`, CVX `08` with a
  made-up description, CDCREC `2106-3`/`2186-5`. After each test the business layer goes back to the EHR-1 scratch database with its caches.

Changed: `tests/LibWebTests/HtmlGenerationTests.cs` (section 6), `tests/AttributionTests/ReleaseIdentityTests.cs` (two names),
`tests/ReleaseFlowTests/ProgramNamesTests.cs` (`EhrSummaryCcds.cs` joins `RuntimeNameSites_UseProgramNames` with `"OpenPmsDental.exe"` and
`"OpenDental.exe"` forbidden).

Targeted runs (`run-tests.ps1 -Mode Targeted`, NoHqTests always included), after the full lane build: see section 9.

## 8. For the lead

- **Release notes.**
  - Received CCDs and CCRs (Chart > EHR > Summary of Care, e-mail attachments) show as readable pages again, made with HL7's CDA
    stylesheet (CDA-core-xsl 4.1.0-beta.2) or the AAFP's CCR stylesheet, which this program ships with their notices (Help > About >
    Licenses). A stylesheet that comes with an e-mail is still never used; the window says so. When a document cannot be shown that way
    (too large, nested too deeply, a DTD, not well-formed XML, or the stylesheet refuses it, for example for a `javascript:` link), it is
    shown as XML or as its text, with the reason. Links in a shown document open only when they go to the web or to an e-mail address.
    A CCR has no reconcile buttons, as before.
  - The stylesheets run in a short-lived second copy of `OpenPmsDental.exe` (`/ehr-render`) with no window, which ends after the document;
    it may show in Task Manager for a moment. An office that configured Windows Error Reporting to collect dumps may get one if that
    process crashes.
  - CCD file exports (Chart > CCD, Clinical Summary, Electronic Copy, Summary of Care) write `ccd.xml`, `ccd.xsl`, `cda_l10n.xml`,
    `cda_narrativeblock.xml`, `stylesheets-license.txt` and `ccd.html`, a page that opens in any browser, because browsers stop running
    stylesheets (Chrome 158, November 2026). Patient Export writes one HTML file beside each XML file. E-mailed and portal summaries carry
    the stylesheet and its two companion files.
  - Received CCRs show their dates correctly: the AAFP stylesheet as published showed a date without a time zone as January 1 of its year
    (a date of birth among them) or as nothing; this program ships a corrected copy, which shows a year alone as the year, a year and month
    as month and year, and a date without a time with no time.
- **Project baseline (stage 2's checks).** No project item was edited (only the comment of `tests/EhrTests/EhrTests.csproj`); the globs
  add these items to `items.txt`:
  - `OpenPmsDentalBusiness|None|Resources\Ehr\CDA.xsl|`, `...|None|Resources\Ehr\ccr.xsl|`, `...|None|Resources\Ehr\cda_l10n.xml|`,
    `...|None|Resources\Ehr\cda_narrativeblock.xml|`, `...|None|Resources\Ehr\stylesheets-license.txt|` (the five embedded files, `None`
    items of the default glob like the other ResXFileRef sources)
  - `OpenPmsDental|Compile|Main Modules\EhrRenderChild.cs|`
  - `EhrTests|Compile|CdaExportTests.cs|`, `CdaGeneratedCcdTests.cs`, `CdaHostileTests.cs`, `CdaRenderTests.cs`, `CdaResourceTests.cs`
  - `resources.txt` is unchanged (the files are inside `OpenPmsDentalBusiness.Properties.Resources.resources`); the golden files under
    `tests/EhrTests/Golden/` are not items (`EnableDefaultNoneItems` is off for test projects). The record was not re-taken here (the
    script runs MSBuild itself); these lines come from the globs and are for the lead's re-record.
- **Release dry run.** See section 9: the staged file list is unchanged, because the stylesheets are embedded.
- **Not changed, for a follow-up.** The labels of the three export windows (`FormEhrClinicalSummary.Designer.cs:122`,
  `FormEhrElectronicCopy.Designer.cs:133`, `FormEhrSummaryOfCare.Designer.cs:161`) still say "includes 2 files: ccd.xml, ccd.xsl"; they are
  designer files outside this package's list, and the longer text may need a larger label.
- **Counsel (E-11).** Whether e-mails and the portal should carry `stylesheets-license.txt` too (the plan says exports only); and the
  fifth change of `ccr.xsl` (the footer the AAFP's header asks of derived works).
- **Launch checks (section 11, item 6, on a test database).**
  - Chart > CCD export: the folder has `ccd.xml`, `ccd.xsl`, `cda_l10n.xml`, `cda_narrativeblock.xml`, `ccd.html` and
    `stylesheets-license.txt`; `ccd.html` opens readable in a browser, with no script.
  - Patient Export of three patients: three XML files, three HTML files, one set of stylesheet files.
  - A received CCD in e-mail shows rendered, with no note. One with its own `.xsl` shows rendered, with the note that its stylesheet was
    not run and the program's own was used.
  - A CCR with dates shows rendered, with readable dates (a date of birth on its own day; a year-only onset as the year, not January 1),
    and without the three reconcile buttons.
  - A received CCR whose description texts hold markup shows that markup as text, with no link or image from it. One whose reference
    location is a `file:` or share path shows it as text, not as a link.
  - A received CCD that is not well-formed (for example with `&#0;` in its text) opens in the text view with the reason and no reconcile
    buttons, and the program stays up.
  - A patient name with accents shows correctly (CDA.xsl writes the family name's a to z in capitals and leaves other letters as they
    are, as upstream does).
  - The deep-nesting CCD gives the text view with the reason, and the program stays up. No window flashes while documents render; the
    `/ehr-render` process ends by itself (Task Manager).
  - Item 9: the Release dry run's staged list is unchanged.

## 9. Runs

- **Full lane build** (Debug|x86, `OpenDental.Local.slnf`): 0 errors, no warning in a file of this package.
- **Targeted runs** (`run-tests.ps1 -Mode Targeted`, after the full build):

  | Assembly | Result |
  |---|---|
  | EhrTests | 122 of 122 passed (34 new) |
  | LibWebTests | 64 of 64 passed (the WebView2 smoke tests ran) |
  | PdfLibraryTests | 41 of 41 passed |
  | S0FTests | 29 of 29 passed |
  | AttributionTests | 50 of 50 passed |
  | ReleaseFlowTests | 120 of 120 passed (the release output of the dry run was there) |
  | NoHqTests | 58 of 58 passed |

  `InitSqlTests` and `ForkMigrationTests` were left out, as the brief says (no schema change). No `ehr1_` or `ehr3_` database was left.
- **Fix round 1** (`ehr/ehr3-2`): full lane build 0 errors, no warning in a file of this package; Targeted runs after it: EhrTests 126 of
  126 (4 new), LibWebTests 65 of 65 (1 new), PdfLibraryTests 41 of 41, S0FTests 29 of 29, AttributionTests 50 of 50, NoHqTests 58 of 58,
  ReleaseFlowTests 119 passed and 1 skipped, none failed (`BuildIdFiles_AreAllInTheReleaseOutput` needs a Release output; the Release dry
  run, which archives the repository, was not run in this round: no file is added, so the staged list cannot change). No `ehr1_` or
  `ehr3_` database was left. The builds ran with `ImportDirectoryPackagesProps=false`, as below.
- **Release dry run** (`tools/Build-Release.ps1 -BuildLane <lane> -OutDir <scratch> -SkipInstaller`, Release|x86 through the lane): 81 files
  staged, the same 81 names as the script's allow-list (`-ListFiles`); `OpenPmsDentalBusiness.dll` grows by about 1.1 MB (the embedded
  stylesheets). The staged file list is unchanged.
- **Environment.** `main` gained a `Directory.Packages.props` after this branch was cut (central package management, stage 2). This
  worktree lies under the main checkout's folder, so NuGet found that file above the worktree, and every restore of this branch failed with
  NU1008 and NU1004. The lane builds and the dry run of this package ran with the environment variable `ImportDirectoryPackagesProps=false`
  (only for those processes; no file changed). Any worktree of a branch cut before that commit needs the same until it is merged with
  `main`.

## 10. Fix round 1 (`ehr/ehr3-2`)

The review's five findings, each fixed in its own commit:

1. **The limits failed open.** `GetLimitError` returned null for an `XmlException` that was not a DTD, so `&#0;` before 100,000 nested
   elements stopped its reader at depth 0 and passed; `DisplayCCD` and the reconcile buttons then parsed it with `XmlDocument.LoadXml`, whose
   legacy reader accepts the reference, and the recursive reconcile code (`EhrCCD.GetNodesByTagNameAndAttributes`) would overflow the
   program's stack. Now `GetLimitError` refuses any document its reader cannot read to the end, and the program builds its own
   `XmlDocument` only with `LoadXmlDocument` (the same strict reader); the reconcile buttons check the limits again first. Test:
   `InvalidCharacterReference_BeforeDeepNesting_RefusedBeforeAnyChild` (it also shows `LoadXml` accepting the reference on a small
   document); the assertion that a malformed document passes the limits became one that it is refused before any child.
2. **A CCR's page could carry a `javascript:`, `file:` or share link** (`ccr.xsl` writes reference locations as `href`, unchecked). Now the
   page writer keeps a link only to `#`, `http://`, `https://` or `mailto:`, for both kinds of page (section 2); not a sixth change of
   `ccr.xsl`. Tests: `Ccr_ReferenceLocations_NoScriptFileOrShareLinks`, `PageWriter_KeepsOnlyLinksToThePageTheWebOrEmail`, and
   `AssertHarmless` checks every link.
3. **A CCR showed the reconcile buttons.** Now it gets no patient (`GetPatientReconcile`), so no buttons, as the plan's step 4 says. Test:
   `CcdView_ReconcileButtons_OnlyForACcd`.
4. **The test copy of `CCD-full.xml` kept four licensed codes in comments.** Replaced (section 1), its SHA-256 re-taken, and
   `CcdFullFixture_HasNoLicensedCodes` scans the whole file. `CCD-full.modified.html` did not change.
5. **`ccr.xsl` still made up a month and day for a partial date** (`2011` as `Jan 01, 2011`, `2010-08` as `Aug 01, 2010`). Now a year
   shows as the year and a year and month as month and year; a date without a time also no longer gets a made-up `12:00 AM` where the
   pattern shows a time (found while fixing this: recorded with the first copy, the new vital sign of `CCR-dates.xml` showed
   `May 06, 2015 : 12:00 AM`). Change 1 of the copy's header and the notices' entry say so; the copy's SHA-256 changed in
   `CdaResourceTests`, `THIRD-PARTY-NOTICES.txt` and section 1. `CCR-dates.html` was re-recorded: only those three dates and the new
   vital sign differ.

Files changed in this round: `OpenPmsDentalBusiness/Data Interface/EhrSummaryCcds.cs`, `OpenPmsDentalBusiness/Resources/Ehr/ccr.xsl`,
`OpenPmsDental/Forms/FormEhrSummaryOfCare.cs`, `OpenPmsDental/Forms/FormEhrSummaryCcdEdit.cs`, `OpenPmsDental/Main Modules/EhrRenderChild.cs`,
`THIRD-PARTY-NOTICES.txt` (the AAFP entry only), `tests/EhrTests/CdaHostileTests.cs`, `CdaRenderTests.cs`, `CdaResourceTests.cs`,
`tests/EhrTests/Golden/CCD-full.modified.xml`, `CCR-dates.xml`, `CCR-dates.html`, `tests/LibWebTests/HtmlGenerationTests.cs`, the CCD
window's row of `docs/port-notes/LIB-WEB.md` and this note. No project item and no schema changed; the baseline lines of section 8 are
unchanged.
