"""Check reconstruction inputs using temporary synthetic audit collections.

This exercises archive preparation and pairing integrity only. Tiny owned input
archives replace collected upstream sources; no native build or license
clearance is claimed. The approved project election and recipes are read only.
"""
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import zipfile


REPO = Path(__file__).resolve().parents[2]
REQUIRED = (
    'tests/t08-minimal/evidence/native-inputs.json',
    'tests/t08-minimal/evidence/official-debug-inputs.json',
    'tools/license-audit/official-php-headers/main/config.w32.h',
    'tools/license-audit/official-php-headers/Zend/zend_config.w32.h',
    'tests/t08-minimal/check_relinked_php.py',
    'tools/license-audit/php-sdk.json',
    'tests/t09/check_pair.py',
    'tests/t09/check_package.py',
    'tests/t09/WINDOWS11_TEST.md',
    'tests/t09/check_source_sidecar.py',
)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')


def fake_source(root, name, files=None):
    """Generate archive/inventory hashes accepted by the collector interface."""
    archive = root / (name + '.zip')
    payload = b'Owned synthetic source for packaging integrity regression.\n'
    if files is None:
        files = {'README.txt': payload}
    with zipfile.ZipFile(archive, 'w') as package:
        for path, data in files.items():
            package.writestr(path, data)
    inventory = root / (name + '-files.json')
    write_json(inventory, [{'path': path, 'sha256': sha(data)} for path, data in files.items()])
    return {
        'name': name, 'commit': '0' * 40,
        'archive': archive.name, 'archiveSha256': sha(archive.read_bytes()),
        'inventory': inventory.name, 'inventorySha256': sha(inventory.read_bytes()),
        'archiveBytesMatchGitBlobs': True,
    }


def prepare_inputs(area):
    core, engine = area / 'core', area / 'engine'
    core.mkdir()
    engine.mkdir()
    sources = [fake_source(core, name) for name in
               ('php', 'libiconv', 'libxml2', 'zlib', 'wineditline', 'libargon2')]
    generated = fake_source(core, 'configured-headers')
    write_json(core / 'generated-windows-config.json', {'syntheticTestInput': True})
    write_json(core / 'core-source.json', {
        'sourceCollectionPassed': True, 'sources': sources,
        'generatedWindowsSource': generated,
    })
    engines = []
    for name, package in (
        ('compatibility', 'phpcompatibility/php-compatibility'),
        ('utils', 'phpcsstandards/phpcsutils'),
    ):
        source = fake_source(engine, name)
        engines.append({
            'package': package, 'sourceCommit': source['commit'],
            'archivePath': source['archive'], 'archiveSha256': source['archiveSha256'],
            'inventoryPath': source['inventory'], 'inventorySha256': source['inventorySha256'],
            'vendorFilesMatchCheckout': True,
        })
    write_json(engine / 'corresponding-source.json', engines)
    return core, engine


def prepare_binary(area):
    package = area / 'package'
    (package / 'evidence').mkdir(parents=True)
    archive = package / 'DK-PHP-Compatibility-audit-only.zip'
    with zipfile.ZipFile(archive, 'w') as bundle:
        bundle.write(REPO / 'tools/license-audit/license-election.json', 'license-election.json')
        selected = 'licenses/php/selected/PHP-Zend-BSD-3-Clause.txt'
        bundle.write(REPO / selected, selected)
        for name in ('composer.json', 'composer.lock', 'ruleset.xml'):
            bundle.write(REPO / 'tools/engine-pack' / name, 'tools/runtime/engine/' + name)
    write_json(package / 'evidence/summary.json', {
        'scopePassed': True, 'archiveSha256': sha(archive.read_bytes()),
    })
    return package


def pair(package, sidecar, output):
    return subprocess.run([
        sys.executable, str(REPO / 'tests/t09/check_pair.py'),
        '--package', str(package), '--sidecar', str(sidecar), '--output', str(output),
    ], capture_output=True, text=True)


def rewrite_sidecar(source_dir, destination, relative, altered):
    """Keep every declared hash truthful, so pairing must detect recipe loss/change."""
    destination.mkdir()
    manifest = json.loads((source_dir / 'source-sidecar.json').read_text(encoding='utf-8'))
    member = 'project/' + relative
    with zipfile.ZipFile(source_dir / manifest['archive']) as source:
        payload = {name: source.read(name) for name in source.namelist()}
    if altered:
        payload[member] += b'\n# synthetic integrity mutation\n'
        row = next(row for row in manifest['members'] if row['path'] == member)
        row.update(bytes=len(payload[member]), sha256=sha(payload[member]))
    else:
        del payload[member]
        manifest['members'] = [row for row in manifest['members'] if row['path'] != member]
    embedded = {key: value for key, value in manifest.items()
                if key not in ('archive', 'archiveBytes', 'archiveSha256')}
    payload['source-sidecar-manifest.json'] = (json.dumps(embedded, indent=2) + '\n').encode()
    archive = destination / manifest['archive']
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as bundle:
        for name, data in payload.items():
            bundle.writestr(name, data)
    manifest.update(archiveBytes=archive.stat().st_size, archiveSha256=sha(archive.read_bytes()))
    write_json(destination / 'source-sidecar.json', manifest)


def main():
    spec = importlib.util.spec_from_file_location(
        'source_sidecar', REPO / 'tools/license-audit/Prepare-SourceSidecar.py')
    collector = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(collector)
    with tempfile.TemporaryDirectory(prefix='dk-sidecar-integrity-') as temporary:
        area = Path(temporary)
        core, engine = prepare_inputs(area)
        sidecar = area / 'source'
        result = collector.prepare(core, engine, sidecar)
        rows = {row['path']: row for row in result['members']}
        with zipfile.ZipFile(sidecar / result['archive']) as bundle:
            for relative in REQUIRED:
                member = 'project/' + relative
                data = (REPO / relative).read_bytes()
                assert bundle.read(member) == data, relative
                assert rows[member]['sha256'] == sha(data), relative
                assert rows[member]['bytes'] == len(data), relative
            for member, row in rows.items():
                assert sha(bundle.read(member)) == row['sha256'], member
        package = prepare_binary(area)
        positive = pair(package, sidecar, area / 'positive')
        assert positive.returncode == 0, positive.stderr
        report = json.loads((area / 'positive/paired-candidate.json').read_text())
        assert report['reconstructionInputsBytesMatched'] is True
        assert report['publicReleaseApproved'] is False
        checks = 1
        for index, relative in enumerate(REQUIRED):
            for altered in (False, True):
                name = f'{index}-' + ('altered' if altered else 'missing')
                changed = area / name
                rewrite_sidecar(sidecar, changed, relative, altered)
                negative = pair(package, changed, area / (name + '-report'))
                assert negative.returncode != 0, name
                expected = relative if altered else 'Missing reconstruction input: ' + relative
                assert expected in negative.stderr, negative.stderr
                assert not (area / (name + '-report/paired-candidate.json')).exists(), name
                checks += 1
        sdk_pin = json.loads((REPO / 'tools/license-audit/php-sdk.json').read_text(encoding='utf-8'))
        sdk_prefix = sdk_pin['name'] + '-' + sdk_pin['commit']
        recipes = {sdk_prefix + '/' + name: b'Owned synthetic recipe or notice'
                   for name in ('LICENSE', 'licenses/README.md', 'phpsdk-vs17-x64.bat')}
        for name, injected in (('sdk-clean', {}), ('sdk-dll', {'test.DLL': b'dummy'}),
                               ('sdk-renamed-exe', {'test.txt': b'MZdummy'})):
            build_root = area / name
            build_root.mkdir()
            sdk_files = dict(recipes)
            sdk_files.update({sdk_prefix + '/' + path: data for path, data in injected.items()})
            sdk = fake_source(build_root, sdk_prefix, sdk_files)
            sdk.update(name=sdk_pin['name'], commit=sdk_pin['commit'])
            write_json(build_root / 'build-source.json', {
                'sourceCollectionPassed': True,
                'sources': [fake_source(build_root, 'builder'), sdk],
            })
            candidate = area / (name + '-sidecar')
            collector.prepare(core, engine, candidate, build_root)
            result = pair(package, candidate, area / (name + '-report'))
            if injected:
                assert result.returncode != 0 and 'PHP SDK binary in source archive' in result.stderr, result.stderr
            else:
                assert result.returncode == 0, result.stderr
                report = json.loads((area / (name + '-report/paired-candidate.json')).read_text())
                assert report['phpSdkRecipeArchiveVerified'] is True
            checks += 1
        print(json.dumps({
            'scopePassed': True, 'pairingCasesPassed': checks,
            'scope': 'Synthetic source-sidecar preparation and reconstruction input integrity only',
            'nativeBuildVerified': False, 'redistributionCleared': False,
        }))


if __name__ == '__main__':
    main()
