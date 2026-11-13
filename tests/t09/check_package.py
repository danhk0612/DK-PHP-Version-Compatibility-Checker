"""Verify a local audit ZIP payload and its extracted bytes. No license clearance."""
import argparse
import hashlib
import json
from pathlib import Path
import sys
import zipfile

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / 'tools/engine-pack'))
from pack_contract import validate

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--stage', required=True, type=Path)
p.add_argument('--zip', required=True, type=Path)
p.add_argument('--output', required=True, type=Path)
p.add_argument('--post-launch', action='store_true')
a = p.parse_args()
sha = lambda data: hashlib.sha256(data).hexdigest()
files = {f.relative_to(a.stage).as_posix(): sha(f.read_bytes())
         for f in a.stage.rglob('*') if f.is_file()}
required = {'Dk.PhpCompatibilityChecker.exe', 'Dk.PhpCompatibilityChecker.dll',
            'Dk.PhpCompatibilityChecker.runtimeconfig.json', 'coreclr.dll',
            'hostfxr.dll', 'hostpolicy.dll', 'PresentationFramework.dll',
            'LICENSE', 'THIRD-PARTY-NOTICES.md', 'LICENSING.md',
            'tools/runtime/manifest.json', 'WINDOWS11_TEST.md', 'AUDIT-ONLY.txt'}
assert required <= files.keys(), 'Self-contained/package payload is incomplete'
assert any(path.startswith('licenses/') for path in files), 'License texts absent'
manifest = validate(a.stage / 'tools/runtime')
selection = json.loads((REPO / 'tests/t08-minimal/evidence/minimal-runtime-selection.json').read_text(encoding='utf-8-sig'))
expected_php = {'tools/runtime/php/' + row['path']: row['sha256'] for row in selection['keptFiles']}
actual_php = {path: digest for path, digest in files.items() if path.startswith('tools/runtime/php/')}
assert actual_php == expected_php, 'PHP package payload differs from the reviewed official ZIP selection'
assert manifest['runtime']['archiveSha256'] == selection['archiveSha256'], 'PHP package archive origin differs'
for runtime_name, notice_name in (('license.txt', 'ZIP-license.txt'),
                                  ('readme-redist-bins.txt', 'ZIP-readme-redist-bins.txt')):
    assert actual_php['tools/runtime/php/' + runtime_name] == files['licenses/php/' + notice_name], (
        'Bundled PHP notice does not match the reviewed original: ' + runtime_name)
with zipfile.ZipFile(a.zip) as archive:
    names = [i.filename for i in archive.infolist() if not i.is_dir()]
    assert len(names) == len(set(names)), 'Duplicate ZIP entries'
    packed = {name: sha(archive.read(name)) for name in names}
assert packed == files, 'ZIP/extracted payload differs or app wrote inside package'
report = {'scopePassed': True, 'postLaunch': a.post_launch, 'fileCount': len(files),
          'packId': manifest['packId'], 'files': files, 'phpOfficialZipSelectionMatched': True,
          'phpArchiveSha256': selection['archiveSha256'], 'redistributionCleared': False}
a.output.mkdir(parents=True, exist_ok=True)
(a.output / ('post-launch-files.json' if a.post_launch else 'package-files.json')).write_text(
    json.dumps(report, indent=2) + '\n', encoding='utf-8')
print(json.dumps({'scopePassed': True, 'postLaunch': a.post_launch, 'fileCount': len(files)}))
