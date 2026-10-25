# B-WIKI: Wiki (port note)

Branch `port/b-wiki`, based on `m1/integration-a2-patch`. Backlog section: [port-backlog.md, B-WIKI](../specs/port-backlog.md). Sources: Open Dental's public release notes for [25.1](https://www.opendental.com/site/version25_1.html) and [25.2](https://www.opendental.com/site/version25_2.html), and the specs in [features-25.1.md](../specs/features-25.1.md) and [features-25.2.md](../specs/features-25.2.md). Nothing was decompiled or observed; no schema change, no fork migration step (the level range 2027001101-2027001199 is unused).

## Status per feature

| Feature | Status | Summary |
|---|---|---|
| `v25_1-misc-wiki-draft-compare` | built | Wiki Drafts has a **Compare** button: the published page on the left, the selected draft on the right, differences marked as in wiki history. |
| `v25_2-wiki-readonly-offline` | built | A static HTML copy of the wiki in a folder the office picks, opened by any browser with no program and no database. Set up in Wiki Setup; kept current after each save. |
| `v25_2-wiki-search-limit` | built | Wiki Search has **Limit results to [N]** (1 to 5000, default 100) and a "Showing first N matches" label; remembered per user. |

## What was built

### Draft compare (v25_1-misc-wiki-draft-compare)

- `FormWikiDrafts`: new **Compare** button (needs exactly one selected draft). It opens `FormWikiCompare` with the published version on the left and the draft on the right, compared on markup text with page titles in the links, the same way wiki history compares revisions.
- `WikiPages.GetPublishedForDraftCompare(title, out isFromHistory)`: the live page, or, when the live page was archived or no longer exists, its newest `wikipagehist` row (`WikiPages.GetLatestHistByTitle`). With neither, the draft is compared with an empty page.
- `FormWikiCompare`: new overload of `InitializeRevisionsSelected` with captions ("Published page: date", "Last history version (the page is archived): date", "Draft by user: date") and help lines for the two sides.
- Permissions: viewing and comparing drafts needs only wiki access. `FormWiki` no longer blocks the **Drafts** button on locked pages; `FormWikiDrafts` disables **Edit** and **Delete** (and double-click editing) on a locked page for users without Wiki Admin and says why.

### Read-only copy of the wiki (v25_2-wiki-readonly-offline)

New `OpenDentBusiness/Misc/WikiStaticExport.cs`. Layout of the folder:

```
<folder>\index.html        all pages, alphabetical, with a title and content search (plain ES5 JavaScript, no libraries)
<folder>\search-data.js    page number, title and lower-case plain text of every page (loaded by a script tag, so it works from file://)
<folder>\pages\<WikiPageNum>.html   one file per page (the number stays the same when a page is renamed)
<folder>\_files\           images and wiki-folder files the pages show or link to, plus _manifest.txt (what the export copied)
```

- Pages are rendered by the existing renderer, `MarkupEdit.TranslateToXhtml`, the same call the Wiki window makes, so the master page and its styles apply. Links are then rewritten: `wiki:` links to the page files (or a marked dead link when the target is archived or missing), images to their copies in `_files`, `[[file:]]` links to the copy when the file is inside the wiki folder and to a `file://` link otherwise, `[[folder:]]` links to `file://`; cloud links are disabled; web links and bookmarks are kept.
- Left out: archived pages, drafts, and pages whose title starts with an underscore (`_Master`).
- Every page and the index carry a banner "Read-only copy exported <date time>" and a link back to the index.
- A page the renderer refuses (broken markup) is still written, as its plain text with a note, so its content stays readable.
- **Wiki Setup** (`FrmWikiSetup`) has a "Read-only copy of the wiki" box: on/off, folder (default `<A-to-Z>\WikiReadOnly`), browse, **Export Now** (full export with a progress window), **Open**, **Desktop Shortcut** (an internet shortcut to `index.html` on the user's desktop), "Update the copy after each save", the last result, and the warning that wiki pages may contain patient information and that the folder should stay reachable when the server is down.
- **Wiki window** (`FormWiki`): a **Read-Only Copy** toolbar button, shown only when a folder is set, opens `index.html` in the default browser.
- Kept current in a background thread, never interrupting the save; failures (for example an unreachable folder) are logged to the `WikiReadOnly` logger folder and shown as the last result in Wiki Setup:
  - after a page save (`FormWikiEdit`, including pages it creates for missing links), a history revert (`FormWikiHistory`), and the link fix-ups in `FormWiki`: that page is exported and `index.html` and the search data are rebuilt;
  - after a rename, an archive, a restore (`FormWiki`, `FormWikiSearch`), a master page change or a new folder (`FrmWikiSetup`): a full export.
- A full export removes only what an export wrote: page files named `<number>.html` that no longer have a page, and files listed in `_files\_manifest.txt` that no page uses any more. Anything else in the folder is left alone. Files are written through a temporary file so a reader never sees half a page.
- `FormWikiFileFolder` (insert file link) tells the user, when the read-only copy is on, that files in the wiki folder are copied and files elsewhere are only linked.
- Settings are two preference rows that are **not** in the `PrefName` enum, read from the preference cache and written with plain SQL, the pattern `DxcPartnerIdentity` uses: `ForkWikiReadOnlyExportPath` (blank or missing = off) and `ForkWikiReadOnlyAutoExport` (blank or missing = on). Rows are inserted the first time the office saves Wiki Setup, so an adopted database needs no migration. No `PrefName` or `PrefInfos.xml` change.
- Nothing is hosted or uploaded; the copy goes only to the local or network folder the office enters.

### Wiki Search limit (v25_2-wiki-search-limit)

- `WikiPages.GetForSearch(..., int limit=0)`: 0 means no limit (24.3 behavior, unchanged). With a limit, the tiers still run in the same order (keywords, titles, content and links), each tier's query gets `LIMIT n`, later tiers are skipped once n titles are collected, and the duplicate removal between tiers is unchanged. Asking each tier for n rows (instead of "the remaining count plus a few") is exact: a duplicate can only be a title an earlier tier collected, so n rows always hold enough new titles. Because the content tier only runs when the title tier was not cut, the link search always uses every title match, and the limited result is exactly the first n titles of the unlimited search (tested for every n). With "Show Main Pages", main pages among the first n move to the top.
- `FormWikiSearch`: **Limit results to** check box and number box (1 to 5000), "Showing first N matches" label when the limit was reached. The limit also applies when the search runs on the report server.
- Per-user memory: `OpenDentBusiness/Misc/WikiSearchLimit.cs`, a second `userodpref` row of the existing wiki search type: `FkeyType` = `WikiSearchIncludeContent` (25) with `Fkey` = 1, `ValueString` "1,N" or "0,N". The Open Dental include-content row keeps `Fkey` 0; `FormWikiSearch` now reads that row by `Fkey` 0. No enum change; a user without a row gets the default.

## Files touched

- `OpenDentBusiness/Data Interface/WikiPages.cs` (owned): `GetForSearch` limit, `GetSearchLimitClause`, `IsSearchLimitReached`, `GetLatestHistByTitle`, `GetPublishedForDraftCompare`, `GetForStaticExport`.
- `OpenDentBusiness/Misc/WikiStaticExport.cs` (new, owned), `OpenDentBusiness/Misc/WikiSearchLimit.cs` (new).
- `OpenDental/Forms/`: `FormWiki.cs`, `FormWikiCompare.cs`, `FormWikiDrafts.cs` + designer, `FormWikiEdit.cs`, `FormWikiFileFolder.cs` + designer, `FormWikiHistory.cs`, `FormWikiSearch.cs` + designer (all owned).
- `WpfControlsOD/Frms/FrmWikiSetup.xaml` + `.xaml.cs` (owned).
- Shared project files (not owned, one small insertion each): `OpenDentBusiness/OpenDentBusiness.csproj` (two `Compile` lines), `OpenDental.sln` and `OpenDental.Local.slnf` (the new test project).
- Tests: `tests/BWIKITests/` (new project, 37 tests). Database tests use a scratch MariaDB database named `bwiki_<UTC time>_<pid>_<random>` on 127.0.0.1, dropped at the end of the run (stale ones older than 12 hours are dropped by later runs); `BWIKI_TEST_DB=skip` skips them; `BWIKI_KEEP_EXPORT=1` keeps the synthetic exported folders for a look in a browser.

## INFERRED behavior (not public; chosen as the most reasonable minimal behavior)

- The whole read-only copy design. Open Dental's notes say only that a read-only wiki can be opened while the program is closed. The spec's static HTML export was built as proposed (its "alternative interpretation", a viewer program that reads the database, does not help when the server is down).
- File names by `WikiPageNum` (stable across renames) rather than by title.
- `index.html` and the search data are rebuilt after every save, not only when a title is added or removed (the search data holds page text, which changes with every save). A rename, archive or restore triggers a full export, so link texts and dead links in other pages stay right.
- `[[file:]]` targets are copied only when they are inside the wiki image folder (`<A-to-Z>\Wiki`); other files are linked with `file://`, so they open only while their location can be reached. Images that cannot be found show a short "[image not available: name]" note.
- Images are shown at their natural size (limited to the page width); the program's renderer reads each image to set width, height and EXIF rotation, which the export avoids so a missing image cannot break the page. Current browsers apply EXIF rotation themselves.
- The read-only copy settings in Wiki Setup need the Setup permission (silent check: the controls are disabled without it), and every change and every Export Now writes a Setup security log entry.
- The search limit is off by default (24.3 behavior until the user checks the box), with N = 100.
- The folder may not be the wiki image folder (or inside it) or the A-to-Z folder itself.

## Deviations from the spec

- Preference names carry a `Fork` prefix (`ForkWikiReadOnlyExportPath`, `ForkWikiReadOnlyAutoExport` instead of the proposed `WikiReadOnlyExportPath`, `WikiReadOnlyAutoExport`), so they can never clash with an Open Dental preference the census may find, and they are not registered in `PrefName`/`PrefInfos.xml` (foundation-owned). A later registry entry with the same names would work unchanged.
- "Open read-only wiki in the Wiki menu" is a toolbar button in the Wiki window (the window has a toolbar, not a menu); the main window's menu is not touched (owned by B-SHELL).
- The search limit is stored in `userodpref` type 25 with `Fkey` 1 instead of a new `UserOdFkeyType` value (a new value would be a foundation registry change and would also need an exception in `RegistryTests`).
- Per-tier `LIMIT n` instead of "remaining plus a few extra rows" (exact, see above).

## Expected interplay with packages built in parallel

- **B-SHELL** (main window): if the main window's Wiki menu should also open the read-only copy, call `WikiStaticExport.GetIndexPath(WikiStaticExport.GetExportPath())` when `WikiStaticExport.IsEnabled()`.
- **M1-02 / M2-02** (registries): nothing needed. If the owner prefers registered names, add `PrefName` entries named exactly `ForkWikiReadOnlyExportPath` and `ForkWikiReadOnlyAutoExport` (no data change), or a fork-only `UserOdFkeyType` for the search limit plus a one-time move of the `Fkey` 1 rows.
- **B-DBM** (`v25_2-tools-dbm-invalid-chars-gdi`) cleans `wikipage.PageContent`; a cleaned page reaches the read-only copy at its next save or full export.
- **B-SERVER** (report server): `FormWikiSearch` passes the limit through `ReportsComplex.RunFuncOnReportServer`, so the limit applies on the report server too.
- Merge: `OpenDental.sln`, `OpenDental.Local.slnf` and `OpenDentBusiness.csproj` get one small insertion each; conflicts with other packages' test projects are append-only.

## Questions for the owner

1. The folder setting is one for the whole office, and its default is inside the A-to-Z folder, which usually lives on the same server as the database. Should there be a per-computer folder (for example on each front-desk computer), or a second copy, so the copy is still there when the server is down? Wiki Setup warns about this today.
2. Should `[[file:]]` targets outside the wiki folder also be copied into the read-only copy (with a size cap), or stay as links?
3. Is "off" the right default for the search limit?
4. Keep the search limit in `userodpref` type 25 / `Fkey` 1, or register a fork-only `UserOdFkeyType`?
5. If the census finds an Open Dental 25.2 preference for the read-only wiki in the office database, should the fork read it instead of its own rows?
6. Is the Setup permission the right gate for the read-only copy settings? Should non-admin users see drafts of locked pages (as the spec says; they cannot edit them)?

## Not tested automatically

- The WinForms and WPF windows (Compare button and captions, the search limit controls and label, Wiki Setup's read-only box, the Read-Only Copy toolbar button, the file-link note): built, not run, because the GUI is not launched in this run.
- The index page's JavaScript search was checked once by hand in a browser (synthetic export served from localhost): title search, content search and the "Include page content" box behave as described. There is no automated browser test.
