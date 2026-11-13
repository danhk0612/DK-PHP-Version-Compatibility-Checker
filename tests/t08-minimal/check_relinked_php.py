"""Compare two isolated audit PHP builds with a validated, unchanged engine pack.

The marker build changes only a libiconv version variable for a relink positive
control. Its reported 1.20 is a test marker, not an actual library version.
This test executes only owned fixture data through PHPCS; it does not run those
PHP files as code, alter a product pack, or validate redistribution rights.
"""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import zipfile

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / 'tools/engine-pack'))
from pack_contract import inventory, read, require, sha, validate, write


def snapshot(root):
    return {'files': inventory(root),
            'directories': sorted(p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_dir())}


def run_php(php, args, cwd):
    # Restrict native dependency lookup to the selected audit runtime and Windows.
    system = Path(os.environ['SystemRoot'])
    env = dict(os.environ, PATH=os.pathsep.join(map(str, (php.parent, system / 'System32', system))),
               PHPRC='', PHP_INI_SCAN_DIR='', COMPOSER_DISABLE_NETWORK='1')
    return subprocess.run([str(php), '-n', *map(str, args)], cwd=cwd, env=env,
                          capture_output=True, timeout=90)


def validate_php(php, expected):
    require(php.is_file() and php.name.lower() == 'php.exe', 'Expected audit php.exe')
    require((php.parent / 'php8.dll').is_file(), 'Expected audit php8.dll beside php.exe')
    r = run_php(php, ['-r', "echo PHP_VERSION . '|' . ICONV_VERSION;"], php.parent)
    require(r.returncode == 0 and not r.stderr.strip(), 'Audit PHP CLI probe failed')
    actual = r.stdout.decode('utf-8-sig').strip()
    require(actual == '8.4.27|' + expected, 'Unexpected PHP or iconv marker: ' + actual)
    return {'phpVersion': '8.4.27', 'iconvVersion': expected,
            'phpExeSha256': sha(php), 'phpDllSha256': sha(php.parent / 'php8.dll')}


def validate_build_material(audit, profile, php, snapshot_config):
    material = read(audit / (profile + '-build-material.json'))
    require(material['profile'] == profile and material['fullProductProfileVerified'] is False
            and material['redistributionCleared'] is False, 'Audit material scope is invalid')
    require(material.get('snapshotConfiguration', False) is snapshot_config, 'Unexpected build configuration')
    source = (audit / profile).resolve()
    if not snapshot_config:
        source /= 'php-src'
    require(Path(material['sourceRoot']).resolve() == source and php.resolve().is_relative_to(source),
            'Build material is not associated with the selected audit PHP')
    expected = {'--enable-snapshot-build', '--enable-debug-pack', '--disable-zts',
                '--enable-object-out-dir=../obj/', '--enable-com-dotnet=shared', '--without-analyzer'}
    if snapshot_config:
        require(expected <= set(material['configureArguments'])
                and '--disable-all' not in material['configureArguments'], 'Expected Windows snapshot configuration')
    else:
        require('--disable-all' in material['configureArguments'], 'Expected reduced audit configuration')
    require({t['name'] for t in material['tools']} == {'cl.exe', 'link.exe', 'nmake.exe'}
            and all(t['fileVersion'] for t in material['tools']), 'Missing actual build tool versions')
    require(bool(material['windowsSdkVersion']), 'Missing audit Windows SDK version')
    archive = audit / (profile + '-build-material.zip')
    require(material['archive'] == archive.name and archive.stat().st_size == material['archiveBytes']
            and sha(archive) == material['archiveSha256'], 'Build material archive checksum mismatch')
    rows = {r['path']: r for r in material['files']}
    require(len(rows) == len(material['files']), 'Duplicate build material inventory path')
    configuration = {'Makefile', 'config.nice.bat', 'main/config.w32.h'}
    if snapshot_config:
        configuration = {'php-src/' + name for name in configuration | {'Zend/zend_config.w32.h'}}
    require(configuration <= rows.keys(), 'Missing generated build configuration')
    response_count = 0
    with zipfile.ZipFile(archive) as package:
        require(len(package.namelist()) == len(rows) and set(package.namelist()) == set(rows),
                'Build material archive membership mismatch')
        for name, row in rows.items():
            relative = Path(name)
            path = (source / relative).resolve()
            require(not relative.is_absolute() and path.is_relative_to(source) and path.is_file(),
                    'Escaping or missing generated build file')
            response = relative.parent.name == 'resp' and relative.suffix == '.txt'
            require(name in configuration
                    or response or relative.name == 'php8.dll.def', 'Unexpected build material file')
            data = package.read(name)
            require(len(data) == row['bytes'] and sha(path) == row['sha256']
                    and data == path.read_bytes(), 'Build material bytes differ from actual build: ' + name)
            response_count += int(response)
    require(response_count > 0 and response_count == material['responseFileCount'],
            'Missing or inconsistent generated response files')
    return {'profile': profile, 'archive': archive.name, 'archiveSha256': material['archiveSha256'],
            'verifiedFileCount': len(rows), 'responseFileCount': response_count,
            'actualBuildBytesMatched': True, 'snapshotConfiguration': snapshot_config,
            'fullProductProfileVerified': False}


def main():
    p = argparse.ArgumentParser(description=__doc__)
    for name in ('original-php', 'marker-php', 'pack', 'output', 'build-audit'):
        p.add_argument('--' + name, type=Path, required=True)
    p.add_argument('--snapshot', action='store_true')
    a = p.parse_args()
    require(os.name == 'nt', 'Windows audit runtime is required')
    original, marker, pack, output = (a.original_php.resolve(), a.marker_php.resolve(),
                                      a.pack.resolve(), a.output.resolve())
    require(original != marker and original.parent != marker.parent, 'Audit builds must be separate')
    require(not output.exists(), 'Output already exists; preserve the prior audit')
    require(not output.is_relative_to(pack) and not pack.is_relative_to(output), 'Output and product pack must be separate')
    manifest = validate(pack)
    targets = read(REPO / 'tools/engine-pack/sources.json')['targets']
    require([t['version'] for t in manifest['targets']] == targets, 'Unexpected audit target set')
    pack_before = snapshot(pack)
    original_info = validate_php(original, '1.19')
    marker_info = validate_php(marker, '1.20')
    build_materials = [validate_build_material(a.build_audit.resolve(), profile, php, a.snapshot)
                       for profile, php in (('original', original), ('marker', marker))]
    require(original_info['phpDllSha256'] != marker_info['phpDllSha256'], 'Audit PHP DLLs have identical hashes')
    if a.snapshot:
        approved = pack / 'php/php.exe'
        modules = []
        for php in (approved, original, marker):
            probe = run_php(php, ['-r', 'echo json_encode(get_loaded_extensions(), JSON_THROW_ON_ERROR);'], php.parent)
            require(probe.returncode == 0 and not probe.stderr.strip(), 'Could not probe static extensions')
            modules.append(sorted(json.loads(probe.stdout)))
        require(modules[0] == modules[1] == modules[2], 'Snapshot static extension sets differ: ' + repr(modules))
    fixture_root = REPO / 'tests/fixtures'
    fixture_before = snapshot(fixture_root)
    definitions = read(fixture_root / 'cases.json')['cases']
    expected_cases = sorted((case['file'], target) for case in definitions for target in case['targets'])
    require({target for _, target in expected_cases} == set(targets), 'Unexpected fixture target coverage')
    phpcs = pack / 'engine/vendor/squizlabs/php_codesniffer/bin/phpcs'
    rules = pack / 'engine/ruleset.xml'
    results = []
    with tempfile.TemporaryDirectory(prefix='dk-t08-php-relink-') as temp:
        area = Path(temp)
        subject = area / '한글 space & project'
        shutil.copytree(fixture_root / 'compatibility', subject / 'compatibility')
        # A target project configuration must not be loaded by explicit engine args.
        (subject / 'phpcs.xml').write_text('<ruleset name="poison"><rule ref="DoesNotExist"/></ruleset>', encoding='utf-8')
        subject_before = snapshot(subject)
        for case in definitions:
            file = subject / case['file']
            require(file.is_file() and file.resolve().is_relative_to(subject.resolve()), 'Fixture path is invalid')
            for target, expected in case['targets'].items():
                require(target in targets, 'Unexpected target')
                file_list = area / 'files.txt'
                file_list.write_text(str(file) + '\n', encoding='utf-8')
                args = [phpcs, '--standard=' + str(rules), '--runtime-set', 'testVersion', target,
                        '--report=json', '-q', '--no-cache', '--extensions=php,inc',
                        '--file-list=' + str(file_list), '--ignore-annotations']
                outcomes = []
                for php in (original, marker):
                    r = run_php(php, args, pack / 'engine')
                    require(r.returncode in (0, 1, 2, 3) and not r.stderr.strip(), 'Audit PHPCS failed')
                    obj = json.loads(r.stdout.decode('utf-8-sig'))
                    require(len(obj['files']) == 1 and Path(next(iter(obj['files']))).resolve() == file.resolve(),
                            'Selected and processed fixture differ')
                    outcomes.append({'exit': r.returncode, 'report': obj})
                require(outcomes[0] == outcomes[1], 'Original/marker PHPCS results differ')
                messages = next(iter(outcomes[0]['report']['files'].values()))['messages']
                if expected['expectation'] == 'finding':
                    require(any(m['source'].startswith(expected['source_prefix']) and m['line'] == expected['line']
                                and m['type'] == expected.get('type', m['type'])
                                for m in messages), 'Expected finding absent')
                elif expected['expectation'] == 'no_findings':
                    require(outcomes[0]['exit'] == 0 and not messages, 'Unexpected control finding')
                else:
                    require(expected['expectation'] == 'observe_only', 'Unknown expectation')
                results.append({'file': case['file'], 'target': target, 'expectation': expected['expectation'],
                                'exit': outcomes[0]['exit'], 'totals': outcomes[0]['report']['totals'],
                                'messages': messages})
        require(sorted((case['file'], case['target']) for case in results) == expected_cases
                and subject_before == snapshot(subject), 'Fixture coverage or subject protection failed')
    require(fixture_before == snapshot(fixture_root), 'Repository fixtures changed')
    require(pack_before == snapshot(pack), 'Validated engine pack changed')
    output.mkdir(parents=True)
    write(output / 'php-relink-comparison.json', {
        'scopePassed': True, 'original': original_info, 'marker': marker_info,
        'buildMaterials': build_materials,
        'staticExtensionsMatchingApprovedPhp': modules[0] if a.snapshot else None,
        'cases': results, 'comparedCases': len(results), 'subjectUnchanged': True,
        'repositoryFixturesUnchanged': True, 'enginePackUnchanged': True,
        'markerMeaning': 'Audit-only libiconv variable change, not a library version update',
        'limits': ['No proof of original official binary reproducibility', 'No network blocking test',
                   'No legal/redistribution clearance', 'No product pack replacement']})
    print(json.dumps({'scopePassed': True, 'comparedCases': len(results)}))


if __name__ == '__main__':
    main()
