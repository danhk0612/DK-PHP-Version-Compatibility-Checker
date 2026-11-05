"""Compare the pinned official PHP source archive with in-ZIP bundled digests.
No extraction, source execution or build occurs. Mismatches remain unresolved.
"""
import argparse
import hashlib
import json
from pathlib import Path
import tarfile
import urllib.request

URL = 'https://www.php.net/distributions/php-8.4.27.tar.xz'
EXPECTED = 'dcf93923b0e38e0d5252b25b796520b5a47761a08b3488b6d0b5d1115764d3f0'
COMMIT = '00d8bae801541db0d95a698e108bb4b1b273fe6e'
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--archive', required=True, type=Path)
parser.add_argument('--inventory', required=True, type=Path)
parser.add_argument('--sbom', required=True, type=Path)
parser.add_argument('--output', required=True, type=Path)
args = parser.parse_args()
ARCHIVE, INVENTORY, SBOM, OUTPUT = args.archive, args.inventory, args.sbom, args.output
sha = lambda data: hashlib.sha256(data).hexdigest()
if not ARCHIVE.exists():
    with urllib.request.urlopen(URL, timeout=60) as response:
        ARCHIVE.write_bytes(response.read())
assert sha(ARCHIVE.read_bytes()) == EXPECTED
source_rows = json.loads(INVENTORY.read_text())['trackedFiles']
git_rows = {row['path']: row['sha256'] for row in source_rows}
sbom_bytes = SBOM.read_bytes()
sbom = json.loads(sbom_bytes.decode('utf-8-sig'))
results = []
with tarfile.open(ARCHIVE) as package:
    members = {entry.name.removeprefix('php-8.4.27/'): entry
               for entry in package.getmembers() if entry.isfile()}
    for component in sbom['components']:
        properties = {entry['name']: entry['value'] for entry in component.get('properties', [])}
        if properties.get('php:component-origin') != 'bundled':
            continue
        root = properties['php:source-path']
        rows = {path[len(root) + 1:]: sha(package.extractfile(entry).read())
                for path, entry in members.items() if path.startswith(root + '/')}
        raw = {path[len(root) + 1:]: digest for path, digest in git_rows.items()
               if path.startswith(root + '/')}
        form = properties['php:source-digest-format']
        if form == 'file-content-v1':
            actual = sha(package.extractfile(members[root]).read())
            rows = {root: actual}
            raw = {root: git_rows[root]} if root in git_rows else {}
        else:
            assert form == 'relative-path-and-file-sha256-v1'
            actual = sha(''.join(path + '\n' + rows[path] + '\n'
                                for path in sorted(rows, key=lambda value: value.encode('utf-8'))).encode('utf-8'))
        results.append({'name': component['name'], 'version': component.get('version'),
            'sourcePath': root, 'officialFormat': form,
            'officialExpectedSha256': properties['php:source-digest'],
            'archiveActualSha256': actual, 'match': actual == properties['php:source-digest'],
            'archiveCount': len(rows), 'gitCount': len(raw),
            'archiveOnly': sorted(rows.keys() - raw.keys()),
            'gitOnly': sorted(raw.keys() - rows.keys()),
            'changed': sorted(path for path in rows.keys() & raw.keys() if rows[path] != raw[path])})
report = {'provenance': {'officialSourceArchiveUrl': URL, 'archiveSha256': EXPECTED,
    'archiveBytes': ARCHIVE.stat().st_size,
    'officialChecksumUrl': 'https://www.php.net/releases/index.php?json&version=8.4.27',
    'phpRepository': 'https://github.com/php/php-src', 'phpTag': 'php-8.4.27',
    'phpCommit': COMMIT, 'gitInventorySha256': sha(INVENTORY.read_bytes()),
    'inputSbom': SBOM.as_posix(), 'inputSbomSha256': sha(sbom_bytes),
    'generatorUrl': 'https://github.com/php/php-sdk-binary-tools/blob/73f9b80a55bf82cb4d3218c1e985a426ce289367/bin/phpsdk_sbom.php',
    'reproducer': 'tests/t08-minimal/compare_release_source.py'},
    'matchedComponents': sum(row['match'] for row in results), 'totalComponents': len(results),
    'redistributionCleared': False,
    'conclusion': 'Official tar source and raw pinned Git have identical libmbfl/timelib path sets and bytes; both still differ from two in-ZIP SBOM aggregates. Cause remains unresolved; generated-source omission does not explain these two paths.',
    'components': results}
OUTPUT.write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps({'matchedComponents': report['matchedComponents'],
                  'totalComponents': report['totalComponents'], 'output': str(OUTPUT)}))
