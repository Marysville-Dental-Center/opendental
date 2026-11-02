# Port note: LIB-SPELL, WeCantSpell.Hunspell in place of NHunspell

Branch `next/libspell-1`, cut from `next/libbase-2` (0c5fa6369). Owner approval of 2026-11-02 (roadmap item 2, library updates for
maintenance: "a managed Hunspell (no native DLL)").

## Sources and rules

- The library is the netstandard2.0 build of the NuGet package `WeCantSpell.Hunspell` 7.0.1 (`lib/netstandard2.0/WeCantSpell.Hunspell.dll`,
  SHA-256 `e2b11702...29eea`), restored by the owner's library manifest. Its API comes from the project's public source at the release commit
  (`ab5709d95b2d`) and its readme; no binary was decompiled.
- Licence: MPL 1.1 / GPL 2.0 or later / LGPL 2.1 or later, at the user's choice. The release uses the GPL / LGPL option and distributes it under
  the LGPL 2.1 or later with `LICENSE-LGPL-2.1.txt`, as it did NHunspell.
- Synthetic data only. No table, column or preference changed; the custom word list stays the `dictcustom` table. No migration.

## Files

| File | Before | Now |
|---|---|---|
| `Required dlls/NHunspell.dll` | NHunspell 0.9.6, referenced by OpenPmsDental, WpfControlsOD and UnitTests | **Deleted** |
| `Required dlls/Hunspellx86.dll` | native Hunspell 1.2, copied next to the program (a Release build threw without it) | **Deleted**; on the release deny list; the installer's `[InstallDelete]` removes the copy v0.0 installed |
| `Required dlls/Hunspellx64.dll` | native Hunspell 1.2 x64, never loaded by the x86 program | **Deleted**; on the deny list |
| `Required dlls/WeCantSpell.Hunspell.dll` | | **New**: assembly 7.0.0.0, file 7.0.1.0, no strong name (so no binding redirect). It needs `System.Memory`, `System.Buffers` and `System.Runtime.CompilerServices.Unsafe`, which `Required dlls` has at the library-update versions with redirects in every config. Referenced by WpfControlsOD only; ships (allow list, notices). |
| `OpenPmsDental/Resources/NHunSpell.txt` | NHunspell's licence summary in the licence list | **Replaced** by `WeCantSpellHunspell.txt`, the package's own `license.txt` |

The dictionary is unchanged: the OpenOffice.org en_US dictionary embedded in WpfControlsOD (`Resources/en_US_aff`, `en_US_dic`, 48,245 words,
`SET ISO8859-1`, which the .NET Framework reads without an extra code page provider).

## The spell checker (`WpfControlsOD/Logic/SpellCheckL.cs`)

`WpfControls.SpellCheckL` is the one spell checker of every `ODtextBox` (Windows Forms) and `TextRich` (WPF):

- The dictionary is read once per process through a `Lazy<WordList>` (`ExecutionAndPublication`) and shared by every box on every thread
  (WeCantSpell.Hunspell may be queried concurrently). Each box calls `ReadInBackground` when it is created, which starts the read on a thread
  pool thread and returns; the first lookup waits for that read instead of starting another. Before, the first box of each kind built its own
  NHunspell object on the UI thread, so the program held two copies, and a Debug build copied the native files at run time.
- `IsInDictionary` (as typed; cached per word, since the dictionary never changes while the program runs), `IsCustomWord` (the `DictCustoms`
  cache, ignoring case, false without a database), `IsCorrect` (as typed, in lower case, or an office word: the rule both boxes had),
  `Suggest` (best first; the library stops after 250 ms, `QueryOptions.TimeLimitSuggestGlobal`), `GetMisspelledWords` (TextRich's word regex,
  each distinct word checked once per pass) and `AddCustomWord` (inserts into `dictcustom` and signals `DictCustoms`, as both boxes did).
- If the embedded dictionary cannot be read, nothing is underlined (`DictionaryReadError` says why) instead of an exception in the box's
  constructor.

The boxes keep their behaviour: the red underline, the first five suggestions in the context menu, "Add to Dictionary" and "Disable Spell
Check". ODtextBox keeps its own word cache and drawing; TextRich shares the answers of one pass across its runs of text.

Hunspell 1.7.2 (the port) is newer than NHunspell's Hunspell 1.2, so a few words may be judged or suggested differently with the same
dictionary.

## Release

`tools/Build-Release.ps1` ships `WeCantSpell.Hunspell.dll` (with a reason when it is missing) and denies `NHunspell.dll` and `Hunspellx*.dll`;
`Hunspellx86.dll` leaves the native-file checks. `THIRD-PARTY-NOTICES.txt`, `NOTICE`, the licence list, `README.md` and
`docs/release-process.md` name the new library.

## Tests

`tests/LibSpellTests` (new): correct and misspelled words, case, suggestions, a note of over 30,000 words (tens of milliseconds) and 8,000 new
words, the embedded dictionary's encoding, one dictionary read per process (fresh AppDomains: 16 threads at once, `ReadInBackground`, ODtextBox
and TextRich built on an STA thread), the office's own words on a scratch database (`test_libspell_...`), and NHunspell gone from the repository,
projects, sources and build output. AttributionTests, ReleaseFlowTests (expected lists) and M2INSPLANTests (no native copy items) follow.

The legacy `UnitTests/UnitTests/ODtextBoxTests.cs` counts misspelled words with fixed numbers measured under Hunspell 1.2; that project is not
part of the fork's test runs and was not re-run.

## Owner questions

- The corresponding source of `WeCantSpell.Hunspell.dll` under the LGPL (`docs/owner-questions.md`, "The source of the spell checker"): the
  notices point to the upstream repository at the release commit and to the package.
