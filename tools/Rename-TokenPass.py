"""The rename's token pass: renames the old internal code names to the new ones as whole words, in any context (strings, comments and
docs included), except inside the KEEP spans below. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the
repository root.

Stage 2 of the rename (docs/port-notes/RENAME.md) ran this pass after tools/Rename-ToOpenPmsDental.ps1, whose C# pass never touches string
literals or comments: over the tests and tools (the source paths the tests read, the built-program paths, the type names built by string),
then over the product code, then over the Markdown docs (--docs). Its result was reviewed by hand. Run it the same way on a branch made
before the rename; see "A branch made before the rename" in RENAME.md.

Usage (Python 3, run isolated with -I):

    python -I tools/Rename-TokenPass.py --ls-files [--docs] [--dry-run] <repo root> <report file> [<prefix> ...]
    <list of files> | python -I tools/Rename-TokenPass.py [--docs] [--dry-run] <repo root> <report file> [<prefix> ...]

--ls-files takes every file git tracks in <repo root>. Otherwise the files are the repository-relative paths read from stdin, one per line
(such as the files a branch changed). A path whose first folder is one of the six old project folders and that does not exist is looked up
in the renamed folder, so a list taken before Rename-ToOpenPmsDental.ps1 moved the folders still works. A prefix is a repository-relative
path prefix with forward slashes (tests/ or tools/AdoptionValidation/); without one, every file is taken. Markdown files and docs/ are skipped
unless --docs is given. Skipped always: the rename script, the leftover-name scanner and this file, the frozen ConvertDatabases*.cs, Open
Dental's seed dump and generated preference tables, the registry test data, and the records in docs (port notes, specs, the binaries audit,
the M1 report). On main the pass changes nothing: its keeps are what the stage 2 review kept, which the scanner allows.

--dry-run counts and reports what would change and writes nothing. The report lists the number of renamed names per file and the total.
The encoding (UTF-8 with or without BOM, else Windows-1252) and the line ends of each file are kept.
"""
import os
import re
import subprocess
import sys

MAP = {
    'OpenDental': 'OpenPmsDental',
    'OpenDentBusiness': 'OpenPmsDentalBusiness',
    'OpenDentalGraph': 'OpenPmsDentalGraph',
    'OpenDentalCloud': 'OpenPmsDentalCloud',
    'OpenDentalServer': 'OpenPmsDentalServer',
    'OpenDentHL7': 'OpenPmsDentalHL7',
}
TOKEN = re.compile(r'(?<![A-Za-z0-9_])(OpenDentBusiness|OpenDentalGraph|OpenDentalCloud|OpenDentalServer|OpenDentHL7|OpenDental)(?![A-Za-z0-9_])')

EXTS = {'.cs', '.xaml', '.resx', '.csproj', '.vcxproj', '.vcxitems', '.filters', '.config', '.settings', '.targets', '.props',
        '.ps1', '.psm1', '.psd1', '.cmd', '.bat', '.sh', '.md', '.txt', '.xml', '.xsd', '.json', '.asmx', '.asax', '.aspx', '.htm',
        '.html', '.rdl', '.tt', '.manifest', '.iss', '.sql', '.js', '.css', '.yml', '.yaml'}

#Spans whose old names are kept: what the scanner's allow-list (tools/Test-NoOldNames.ps1) keeps, plus the stage 2 keeps.
KEEP = [re.compile(p) for p in [
    r'Lans?\.g\(\\?"OpenDental\\?"',                      # translation key
    r'OpenDentBusiness\.UI\.ApptSingleDrawing',            # plugin hook name
    r'open-dent\.com/OpenDentalServer',                    # SOAP namespace
    r'/OpenDentalServer/ServiceMain\.asmx',                # IIS path
    r'(?:localhost|>|\s|^|[\'"(])/OpenDentalServer\b',     # IIS virtual path
    r'OpenDental Middle Tier',                             # Credential Manager prefix
    r'(?<=\.)OpenDentalServer\b(?!\.(?:dll|csproj|ServiceMain))',  # web reference (after a dot)
    r'Web References\\+OpenDentalServer\b',
    r'\b(?:EnumImageAnnotVendor|AccountingSoftware)\.OpenDental\b',  # enum members stored in the database
    r'\b0=OpenDental\b',
    r'name=\\?"OpenDental\\?"',
    r'\|OpenDental\|',
    r'OpenDental\.Imaging\.(?:FileStore|SqlStore)',        # ImageStore preference value
    r'Append\(\\?"OpenDental\\?"\)',                       # Sirona
    r'\\?"OpenDental\|\\?"',                               # Vipersoft
    r'UploaderName=OpenDental',                            # EHG
    r'--source \\*"OpenDental\\*"',                        # Shining3D
    r'OpenDental\.(?:Local\.slnf|sln)\b',                  # solution file names kept
    r'OpenDental, Inc\.',                                  # the company
    r'OpenDental Service\b',                               # Open Dental's own Windows service
    r'Versioned\\+OpenDental\b',                           # Open Dental developers' checkout folder
    r'LegacyLocalAppDataFolderName="OpenDental"',
    r'"OpenDental",ProgramNames\.LegacyLocalAppDataFolderName',
    r'\\"OpenDental\(Lexi\)\?\\"',                          # ErxSourceTests: the removed literals
    r'Items\.Add\("OpenDental",',                          # licence list entry
    r'(?i)\b[a-z0-9.-]*opendental\.(?:com|cloud)\b',       # Open Dental's host names
    r'Client=\\?"OpenDental\\?"',                          # removed DXC partner value
    r'named OpenDentHL7|\(OpenDentHL7\)',                  # the pre-rename HL7 service name
    r'Sometimes OpenDental\.',                             # X12 GS02 sender code (vendor value)
    r'CreateAcks\("OpenDental "',                          # Direct MDN reporting agent (value sent to other systems)
    r'OpenDental staff',                                   # Open Dental's staff
    #Kept by the stage 2 review by hand, and allowed by the scanner; listed here since, so that the pass changes nothing on main.
    r'(?:ProjectReference|HintPath): OpenDental\b',        # RdlSourceBuildTests: rows of the binaries audit, a record
    r'^\s*OpenDental,',                                    # an enum member on its own line (EnumImageAnnotVendor, AccountingSoftware)
    r'\bOpenDentalServer\.ServiceMain\b',                  # the web reference proxy class (OpenPmsDentalBusiness.OpenDentalServer.ServiceMain)
    r'^\s*\[@"[^"]+"\]=new\[\].*',                         # ProgramNamesTests: the literals that must stay out of the routed sites
]]

#Spans kept in one file only (repository-relative path): the scanner's allow-list entries for that path.
KEEP_IN = {
    'tests/AttributionTests/ReleaseIdentityTests.cs': [r'"OpenDental(?: API End User)?"'],  # entries of Open Dental's licence list that are gone
    'docs/adoption-runbook.md': [                                                           # Open Dental's own files and its Middle Tier
        r".*'C:\\Program Files\*\\Open Dental\*'.*",
        r'An `OpenDental\.exe` is always Open Dental|no `OpenDental\.exe` or `FreeDentalConfig\.xml` left',
        r'`OpenDentalServer`(?= (?:web application )?in IIS)',
    ],
}
KEEP_IN = {path: [re.compile(p) for p in patterns] for path, patterns in KEEP_IN.items()}

#Never rewritten: the scripts that name the old names on purpose.
EXCLUDES = ['tools/Rename-ToOpenPmsDental.ps1', 'tools/Test-NoOldNames.ps1', 'tools/Rename-TokenPass.py']

#Records and frozen files that keep the names of their time.
SKIPPED = re.compile(r'ConvertDatabases[^/]*\.cs$|^UnitTests/Resources/dump\.txt$|^DocumentationBuilder/PreferenceTable/|'
                     r'^db/init/new-database\.sql$|^tests/RegistryTests/Data/|^docs/port-notes/|^docs/specs/|'
                     r'^docs/third-party-binaries-audit\.md$|^docs/m1-validation-report\.md$')


def rename_line(line, keep_in=()):
    """The line with its old names renamed, except those inside a match of KEEP or keep_in, and the number renamed."""
    spans = []
    for regex in list(KEEP) + list(keep_in):
        for m in regex.finditer(line):
            spans.append((m.start(), m.end()))
    out = []
    pos = 0
    count = 0
    for m in TOKEN.finditer(line):
        s, e = m.start(), m.end()
        if any(a <= s and e <= b for a, b in spans):
            continue
        out.append(line[pos:s])
        out.append(MAP[m.group(1)])
        pos = e
        count += 1
    out.append(line[pos:])
    return ''.join(out), count


def map_old_folder(root, rel):
    """rel itself when it exists; else rel with its first folder renamed, when that is an old project folder and the renamed path exists."""
    if os.path.exists(os.path.join(root, rel)):
        return rel
    first, sep, rest = rel.partition('/')
    if sep and first in MAP:
        renamed = MAP[first] + '/' + rest
        if os.path.exists(os.path.join(root, renamed)):
            return renamed
    return rel


def main():
    args = sys.argv[1:]
    docs = '--docs' in args
    dry_run = '--dry-run' in args
    ls_files = '--ls-files' in args
    args = [a for a in args if a not in ('--docs', '--dry-run', '--ls-files')]
    if len(args) < 2:
        sys.stderr.write(__doc__)
        return 2
    root = args[0]
    report = args[1]
    prefixes = args[2:]
    if ls_files:
        #git itself lists the files, so no shell re-encodes the paths on the way.
        listing = subprocess.run(['git', 'ls-files', '-z'], cwd=root, check=True, stdout=subprocess.PIPE).stdout
        list_rel = [p for p in listing.decode('utf-8').split('\0') if p]
    else:
        list_rel = sys.stdin.read().splitlines()
    total = 0
    lines_report = []
    seen = set()
    for rel in list_rel:
        #Windows PowerShell may start what it pipes in with a byte order mark.
        rel = rel.strip().lstrip('﻿').replace('\\', '/')
        if not rel:
            continue
        rel = map_old_folder(root, rel)
        if rel in seen or rel in EXCLUDES:
            continue
        seen.add(rel)
        if SKIPPED.search(rel):
            continue
        if not docs and re.search(r'^docs/|\.md$', rel):
            continue
        if prefixes and not any(rel.startswith(p) for p in prefixes):
            continue
        if os.path.splitext(rel)[1].lower() not in EXTS:
            continue
        path = os.path.join(root, rel)
        if not os.path.isfile(path):
            continue
        with open(path, 'rb') as f:
            data = f.read()
        bom = data.startswith(b'\xef\xbb\xbf')
        enc = 'utf-8'
        try:
            text = data.decode('utf-8-sig')
        except UnicodeDecodeError:
            text = data.decode('cp1252')
            enc = 'cp1252'
            bom = False
        if not TOKEN.search(text):
            continue
        parts = re.split(r'(\r\n|\n)', text)
        changed = 0
        keep_in = KEEP_IN.get(rel, ())
        for i in range(0, len(parts), 2):
            new, c = rename_line(parts[i], keep_in)
            if c:
                parts[i] = new
                changed += c
        if changed:
            if not dry_run:
                newtext = ''.join(parts)
                out = newtext.encode(enc)
                if bom:
                    out = b'\xef\xbb\xbf' + out
                with open(path, 'wb') as f:
                    f.write(out)
            total += changed
            lines_report.append('%5d %s' % (changed, rel))
    with open(report, 'w', encoding='utf-8') as f:
        f.write('\n'.join(lines_report) + '\n')
        f.write('TOTAL %d\n' % total)
    print('%s %d tokens' % ('would rename' if dry_run else 'renamed', total))
    return 0


if __name__ == '__main__':
    sys.exit(main())
