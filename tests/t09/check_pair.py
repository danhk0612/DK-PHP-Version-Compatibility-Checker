"""Bind local audit binary and source ZIPs without asserting release clearance."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import zipfile

REPO = Path(__file__).resolve().parents[2]
sha = lambda data: hashlib.sha256(data).hexdigest()
read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--package', type=Path, required=True)
parser.add_argument('--sidecar', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
a = parser.parse_args()
election_path = REPO / 'tools/license-audit/license-election.json'
election = read(election_path)
assert election['electionMade'] and election['phpVersion'] == '4' and election['zendVersion'] == '3.0'
source = read(a.sidecar / 'source-sidecar.json')
assert source['licenseElectionMade'] and source['licenseElectionSha256'] == sha(election_path.read_bytes())
source_zip = a.sidecar / source['archive']
assert sha(source_zip.read_bytes()) == source['archiveSha256']
package = a.package / 'DK-PHP-Compatibility-audit-only.zip'
package_summary = read(a.package / 'evidence/summary.json')
assert package_summary['scopePassed'] and sha(package.read_bytes()) == package_summary['archiveSha256']
with zipfile.ZipFile(source_zip) as sz, zipfile.ZipFile(package) as bz:
    members = {row['path']: row for row in source['members']}
    for name, row in members.items():
        assert sha(sz.read(name)) == row['sha256'], name
    sdk_pin = read(REPO / 'tools/license-audit/php-sdk.json')
    sdk_archives = [name for name in sz.namelist()
                    if name.startswith('sources/build/php-sdk-binary-tools-') and name.endswith('.zip')]
    if source['buildRecipeSourcesIncluded'] or sdk_archives:
        sdk_name = sdk_pin['name'] + '-' + sdk_pin['commit']
        sdk_member = 'sources/build/' + sdk_name + '.zip'
        assert sdk_archives == [sdk_member] and sdk_member in members, 'Expected the pinned PHP SDK recipe archive'
        with zipfile.ZipFile(io.BytesIO(sz.read(sdk_member))) as sdk:
            sdk_paths = [entry for entry in sdk.infolist() if not entry.is_dir()]
            required = ('LICENSE', 'licenses/README.md', 'phpsdk-vs17-x64.bat')
            assert all(sdk_name + '/' + name in sdk.namelist() for name in required), 'Missing PHP SDK recipes or notices'
            for entry in sdk_paths:
                with sdk.open(entry) as blob:
                    assert Path(entry.filename).suffix.lower() not in ('.exe', '.dll') and blob.read(2) != b'MZ', (
                        'PHP SDK binary in source archive: ' + entry.filename)
    notices = []
    for name in bz.namelist():
        if name in ('LICENSE', 'LICENSING.md', 'THIRD-PARTY-NOTICES.md', 'license-election.json', 'tools/license-audit/license-election.json') or name.startswith('licenses/'):
            if name.endswith('/'):
                continue
            relative = 'tools/license-audit/license-election.json' if name == 'license-election.json' else name
            data = bz.read(name)
            assert data == sz.read('project/' + relative), name
            notices.append({'path': name, 'sha256': sha(data)})
    assert 'license-election.json' in {row['path'] for row in notices}
    assert 'licenses/php/selected/PHP-Zend-BSD-3-Clause.txt' in {row['path'] for row in notices}
    for n in ('composer.json', 'composer.lock', 'ruleset.xml'):
        assert bz.read('tools/runtime/engine/' + n) == sz.read('project/tools/engine-pack/' + n), n
    for relative in members:
        if relative.startswith('project/src/'):
            assert sz.read(relative) == (REPO / relative.removeprefix('project/')).read_bytes(), relative
    for relative in ('tests/t08-minimal/evidence/native-inputs.json',
                     'tests/t08-minimal/evidence/snapshot-deps.json',
                     'tests/t08-minimal/evidence/official-debug-inputs.json',
                     'tools/license-audit/official-php-headers/main/config.w32.h',
                     'tools/license-audit/official-php-headers/Zend/zend_config.w32.h',
                     'tests/t08-minimal/check_relinked_php.py',
                     'tests/t09/check_pair.py', 'tests/t09/check_package.py',
                     'tools/license-audit/php-sdk.json',
                     'tests/t09/WINDOWS11_TEST.md', 'tests/t09/check_source_sidecar.py'):
        member = 'project/' + relative
        assert member in members, 'Missing reconstruction input: ' + relative
        assert sz.read(member) == (REPO / relative).read_bytes(), relative
report = {'scopePassed': True, 'licenseElectionMade': True,
          'candidateForUserTesting': True, 'publicReleaseApproved': False,
          'binaryArchive': package.name, 'binaryArchiveSha256': sha(package.read_bytes()),
          'sourceArchive': source_zip.name, 'sourceArchiveSha256': source['archiveSha256'],
          'sourceManifestSha256': sha((a.sidecar / 'source-sidecar.json').read_bytes()),
          'licenseElectionSha256': sha(election_path.read_bytes()), 'matchedNotices': notices,
          'engineRecipeBytesMatched': True, 'currentProjectSourceBytesMatched': True,
          'reconstructionInputsBytesMatched': True,
          'phpSdkRecipeArchiveVerified': bool(sdk_archives),
          'allSourceMembersVerified': True, 'fullBinarySourceCorrespondenceVerified': False,
          'durablePairedPublicationVerified': False, 'redistributionCleared': False,
          'limits': ['Pairing identifies exact local audit archives, not legal clearance',
                     'Official PHP build correspondence remains reviewed separately',
                     'Clean Windows 11 and missing-CRT behavior require user testing',
                     'Keep both archives and this manifest together; CI retention is temporary']}
a.output.mkdir(parents=True, exist_ok=True)
(a.output / 'paired-candidate.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
print(json.dumps({'scopePassed': True, 'matchedNotices': len(notices)}))
