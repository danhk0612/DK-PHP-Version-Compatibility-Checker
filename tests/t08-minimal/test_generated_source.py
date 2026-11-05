"""Check pinned Windows generated source provenance and corruption rejection."""
import argparse
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import zipfile

REPO = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('core_source', REPO / 'tools/license-audit/Collect-PhpCoreSource.py')
core = importlib.util.module_from_spec(spec)
spec.loader.exec_module(core)
sha = lambda data: hashlib.sha256(data).hexdigest()
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--core-source', required=True, type=Path)
parser.add_argument('--sbom', required=True, type=Path)
parser.add_argument('--output', required=True, type=Path)
args = parser.parse_args()
report = json.loads((args.core_source / 'core-source.json').read_text(encoding='utf-8-sig'))
summary = next(row for row in report['sources'] if row['name'] == 'php')
rows = json.loads((args.core_source / summary['inventory']).read_text())['trackedFiles']
sbom = json.loads(args.sbom.read_text(encoding='utf-8-sig'))
archive = args.core_source / summary['archive']
original = archive.read_bytes()
assert sha(original) == summary['archiveSha256']
passed = []

def check(condition, name):
    if not condition:
        raise AssertionError(name)
    passed.append(name)


def rejects(action, name):
    try:
        action()
    except ValueError:
        passed.append(name)
    else:
        raise AssertionError(name)


with tempfile.TemporaryDirectory(prefix='dk-t08-generated-') as temporary:
    root = Path(temporary)
    valid = root / 'valid'
    valid.mkdir()
    (valid / summary['archive']).write_bytes(original)
    generated, provenance = core.generated_windows_source(rows, summary, valid)
    check(len(generated) == 2 and {r['path'] for r in generated} ==
          {'ext/mbstring/libmbfl/config.h', 'ext/date/lib/timelib_config.h'}, 'exact two configured headers')
    check(all('gitBlob' not in r for r in generated), 'generated headers do not claim Git blob identity')
    raw = core.bundled_checks(sbom, rows)
    effective = core.bundled_checks(sbom, rows + generated)
    check(len(raw) == 23 and sum(r['officialAggregateMatchVerified'] for r in raw) == 21,
          'raw-only 21/23 observation preserved')
    check(len(effective) == 23 and all(r['officialAggregateMatchVerified'] for r in effective),
          'configured source matches all 23 official digests')
    check(not any(r['gitByteIdentityVerified'] for r in effective if r['name'] in ('libmbfl', 'timelib')),
          'mixed raw and generated source is not mislabeled as Git bytes')
    check((valid / summary['archive']).read_bytes() == original and archive.read_bytes() == original,
          'input raw source archives unchanged')
    corrupted = root / 'corrupted'
    corrupted.mkdir()
    (corrupted / summary['archive']).write_bytes(original + b'corruption')
    rejects(lambda: core.generated_windows_source(rows, summary, corrupted), 'source archive corruption rejected')

    tampered = root / 'tampered-input'
    tampered.mkdir()
    target = 'ext/date/config.w32'
    prefix = 'php-' + core.PHP_COMMIT + '/'
    with zipfile.ZipFile(archive) as source, zipfile.ZipFile(tampered / summary['archive'], 'w') as dest:
        for member in source.infolist():
            data = source.read(member)
            if member.filename == prefix + target:
                data = data.replace(b'#include <stdint.h>', b'#include <wrong.h>')
            dest.writestr(member, data)
    altered_summary = dict(summary, archiveSha256=sha((tampered / summary['archive']).read_bytes()))
    rejects(lambda: core.generated_windows_source(rows, altered_summary, tampered),
            'changed configuration rejected even with recomputed archive checksum')
    damaged_rows = copy.deepcopy(generated)
    damaged_rows[0]['sha256'] = '0' * 64
    damaged = core.bundled_checks(sbom, rows + damaged_rows)
    check(not all(r['officialAggregateMatchVerified'] for r in damaged), 'changed generated output fails source digest gate')

args.output.write_text(json.dumps({'passed': passed, 'scopePassed': True,
    'scope': 'Pinned Windows configuration provenance and corruption rejection; no native compilation/relink'}, indent=2) + '\n')
print(json.dumps({'scopePassed': True, 'passed': len(passed)}))
