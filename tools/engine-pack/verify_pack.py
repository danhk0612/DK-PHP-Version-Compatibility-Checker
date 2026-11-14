"""Windows maintainer preparation/verification only; no GUI or app execution service."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import platform
import shutil
import struct
import subprocess
import tempfile
import zipfile

from pack_contract import PROFILE, PACKAGES, inventory, read, require, sha, validate, write

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]


def prefix_inventory(root, folder):
    return {folder + '/' + k: v for k, v in inventory(root / folder).items()}


def source_snapshot(root):
    return {'files': inventory(root),
            'directories': sorted(p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_dir())}



def runtime_configuration(source, profile):
    require(profile in ('Full', 'MinimalCli'), 'Unsupported runtime profile')
    if profile == 'Full':
        return source['packId'], None, None
    config = source['minimalCli']
    filename = config['selectionFile']
    require(isinstance(filename, str) and Path(filename).name == filename, 'Invalid runtime selection configuration path')
    path = HERE / filename
    selection = read(path)
    require(selection['profile'] == profile and isinstance(selection['files'], list)
            and selection['files'] and len(selection['files']) == len(set(selection['files'])),
            'Invalid runtime selection allowlist')
    return config['packId'], selection['files'], path


def select_runtime(pack, unpacked, archive, profile):
    """Copy an unchanged official-archive subset into a new managed pack."""
    source = read(HERE / 'sources.json')
    require(sha(archive) == source['php']['sha256'], 'Official PHP archive hash mismatch')
    pack_id, allowlist, selection_path = runtime_configuration(source, profile)
    php = pack / 'php'
    require(not php.exists(), 'PHP output already exists; preserve it and choose a fresh directory')
    extracted = inventory(unpacked)
    archive_files = {}
    with zipfile.ZipFile(archive) as bundle:
        for entry in bundle.infolist():
            if entry.is_dir():
                continue
            name = entry.filename
            canonical = PurePosixPath(name)
            require(not canonical.is_absolute() and '\\' not in name and ':' not in name
                    and all(part not in ('.', '..') for part in canonical.parts)
                    and canonical.as_posix() == name and name not in archive_files,
                    'Unsafe or duplicate official archive entry')
            digest = hashlib.sha256()
            with bundle.open(entry) as stream:
                for chunk in iter(lambda: stream.read(1024 * 1024), b''):
                    digest.update(chunk)
            digest = digest.hexdigest()
            archive_files[name] = {'path': name, 'sha256': digest, 'bytes': entry.file_size}
    require(extracted == {name: row['sha256'] for name, row in archive_files.items()},
            'Extracted PHP files differ from the verified archive')
    selected = sorted(archive_files if allowlist is None else allowlist)
    require(all(name in archive_files for name in selected), 'Allowlisted PHP file is absent from the official archive')
    php.mkdir(parents=True)
    for name in selected:
        target = php.joinpath(*PurePosixPath(name).parts)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(unpacked.joinpath(*PurePosixPath(name).parts), target)
    require(inventory(php) == {name: archive_files[name]['sha256'] for name in selected},
            'Selected PHP copy differs from the verified archive')
    kept = [archive_files[name] for name in selected]
    omitted = [archive_files[name] for name in sorted(archive_files) if name not in selected]
    record = {
        'profile': profile, 'packId': pack_id,
        'sourceUrl': source['php']['url'], 'archiveSha256': sha(archive), 'archiveBytes': archive.stat().st_size,
        'selectionFile': selection_path.name if selection_path else None,
        'selectionFileSha256': sha(selection_path) if selection_path else None,
        'sourceFileCount': len(archive_files), 'sourceBytes': sum(row['bytes'] for row in archive_files.values()),
        'selectedFileCount': len(kept), 'selectedBytes': sum(row['bytes'] for row in kept),
        'omittedFileCount': len(omitted), 'omittedBytes': sum(row['bytes'] for row in omitted),
        'keptFiles': kept, 'omittedFiles': omitted,
        'scope': 'Unmodified bytes from the hash-verified official ZIP; original SBOMs describe the input archive candidate superset, not a trimmed component list. No license clearance is implied.',
    }
    (pack / 'verification').mkdir(parents=True, exist_ok=True)
    write(pack / 'verification/runtime-selection.json', record)
    print('RUNTIME_SELECTION_JSON=' + json.dumps({key: value for key, value in record.items()
                                               if key not in ('keptFiles', 'omittedFiles')}))


def prepare(pack, profile=None):
    require(os.name == 'nt' and struct.calcsize('P') == 8, 'Actual Windows x64 required')
    source = read(HERE / 'sources.json')
    selection_path = pack / 'verification/runtime-selection.json'
    selection = read(selection_path) if selection_path.is_file() else None
    profile = profile or (selection['profile'] if selection else 'Full')
    pack_id, allowlist, config_path = runtime_configuration(source, profile)
    if selection is not None:
        require(selection['profile'] == profile and selection['packId'] == pack_id
                and selection['archiveSha256'] == source['php']['sha256'], 'Runtime selection/profile identity mismatch')
        if config_path is not None:
            require(selection['selectionFileSha256'] == sha(config_path)
                    and set(allowlist) == {row['path'] for row in selection['keptFiles']}, 'Runtime allowlist changed since selection')
    else:
        require(profile == 'Full', 'MinimalCli preparation requires verified runtime-selection evidence')
    versions = {p['name']: p['version'] for p in read(pack / 'engine/composer.lock')['packages']}
    require(versions == PACKAGES, 'Approved engine versions differ')
    require(sha(pack / 'engine/composer.lock') == sha(HERE / 'composer.lock'), 'Lock changed during installation')
    php = pack / 'php/php.exe'
    phpcs = pack / 'engine/vendor/squizlabs/php_codesniffer/bin/phpcs'
    engine = pack / 'engine'
    env = dict(os.environ, PHPRC='', PHP_INI_SCAN_DIR='', COMPOSER_DISABLE_NETWORK='1')
    # Do not let setup PHP directories supply omitted DLLs during candidate checks.
    system_root = Path(os.environ['SystemRoot'])
    env['PATH'] = os.pathsep.join([str(pack / 'php'), str(system_root / 'System32'), str(system_root)])
    raw = REPO / 'artifacts/t04'
    raw.mkdir(parents=True, exist_ok=True)
    def run(args, label, environment=env):
        r = subprocess.run([str(x) for x in args], cwd=engine, env=environment, capture_output=True, timeout=120)
        (raw / (label + '.stdout')).write_bytes(r.stdout)
        (raw / (label + '.stderr')).write_bytes(r.stderr)
        return r
    # Force relative registration; no references to a build machine or target project.
    registration = run([php, '-n', phpcs, '--config-set', 'installed_paths',
                        '../../phpcompatibility/php-compatibility,../../phpcsstandards/phpcsutils'], 'registration')
    require(registration.returncode == 0, 'Could not register bundled standards')
    standards = run([php, '-n', phpcs, '-i'], 'standards')
    require(standards.returncode == 0 and b'PHPCompatibility' in standards.stdout and b'PHPCSUtils' in standards.stdout, 'Missing standards')
    runtime_version = run([php, '-n', '-r', 'echo PHP_VERSION;'], 'php-version').stdout.decode()
    require(runtime_version == source['php']['cliVersion'], 'Unexpected runtime version')
    (pack / 'verification/fixtures').mkdir()
    shutil.copytree(REPO / 'tests/fixtures/compatibility', pack / 'verification/fixtures/compatibility')
    shutil.copy2(REPO / 'tests/fixtures/cases.json', pack / 'verification/fixtures/cases.json')
    shutil.copytree(REPO / 'tests/t04/annotations', pack / 'verification/fixtures/annotations')
    definitions = read(pack / 'verification/fixtures/cases.json')['cases']
    require(set(source['targets']) == {v for case in definitions for v in case['targets']}, 'Targets without regression fixtures')
    with tempfile.TemporaryDirectory(prefix='dk-t04-') as area:
        area = Path(area)
        subject = area / '한글 space & project'
        shutil.copytree(pack / 'verification/fixtures/compatibility', subject / 'compatibility')
        shutil.copytree(pack / 'verification/fixtures/annotations', subject / 'annotations')
        (subject / 'phpcs.xml').write_text('<ruleset name="poison"><rule ref="DoesNotExist"/></ruleset>')
        (subject / 'bootstrap.php').write_text('<?php throw new Exception("TARGET_EXECUTED");')
        (subject / 'vendor').mkdir()
        (subject / 'vendor/autoload.php').write_text('<?php throw new Exception("AUTOLOAD_EXECUTED");')
        (subject / 'composer.json').write_text('{"scripts":{"pre-install-cmd":"@php bootstrap.php"}}')
        (subject / 'php.ini').write_text('auto_prepend_file="bootstrap.php"\n', encoding='utf-8')
        # External ini positive control uses ASCII managed paths, as in T01.
        # PHPRC did not load the Unicode-path ini in the initial Windows control.
        poison = area / 'poison.php'
        poison.write_text('<?php throw new Exception("INI_EXECUTED");', encoding='utf-8')
        ini = area / 'poison.ini'
        ini.write_text('auto_prepend_file="' + poison.as_posix() + '"\n', encoding='utf-8')
        ini_scan = area / 'ini-scan'
        ini_scan.mkdir()
        shutil.copy2(ini, ini_scan / 'poison.ini')
        clean = area / 'clean.php'
        clean.write_text('<?php echo "CLEAN";', encoding='utf-8')
        poisoned = dict(env, PHPRC=str(ini), PHP_INI_SCAN_DIR=str(ini_scan))
        before = source_snapshot(subject)
        positive = run([php, clean], 'ini-positive', poisoned)
        require(positive.returncode != 0 and b'INI_EXECUTED' in positive.stdout + positive.stderr, 'Poison control did not execute')
        counter = 0
        def scan(file, version, annotations=True):
            nonlocal counter
            counter += 1
            file_list = area / 'files.txt'
            file_list.write_text(str(file) + '\n', encoding='utf-8')
            args = [php, '-n', phpcs, '--standard=' + str(engine / 'ruleset.xml'), '--runtime-set',
                    'testVersion', version, '--report=json', '-q', '--no-cache', '--extensions=php,inc',
                    '--file-list=' + str(file_list)]
            if annotations:
                args.append('--ignore-annotations')
            r = run(args, 'scan-' + str(counter), poisoned)
            report = json.loads(r.stdout)
            require(r.returncode in (0, 1, 2, 3), 'Unexpected engine exit')
            files = report['files']
            # ignoreFile intentionally yields no processed file in the control run.
            require(len(files) == 1 if annotations else len(files) <= 1, 'Selected/processed file mismatch')
            if files:
                require(Path(next(iter(files))).resolve() == file.resolve(), 'Processed path mismatch')
            messages = next(iter(files.values()))['messages'] if files else []
            require(sum(m['type'] == 'ERROR' for m in messages) == report['totals']['errors']
                    and sum(m['type'] == 'WARNING' for m in messages) == report['totals']['warnings'], 'JSON totals mismatch')
            for m in messages:
                require(m['type'] in ('ERROR', 'WARNING') and type(m['line']) is int
                        and type(m['column']) is int and type(m['severity']) is int
                        and type(m['fixable']) is bool and isinstance(m['source'], str)
                        and isinstance(m['message'], str), 'JSON row contract mismatch')
            return {'exit': r.returncode, 'processedFiles': len(files), 'totals': report['totals'], 'messages': messages}
        baseline = []
        for case in definitions:
            for version, expected in case['targets'].items():
                require(version in source['targets'], 'Unexpected target fixture')
                result = scan(subject / case['file'], version)
                expectation = expected['expectation']
                case_label = f"{case['file']} (target PHP {version})"
                if expectation == 'finding':
                    expected_type = expected.get('type')
                    require(expected_type in (None, 'ERROR', 'WARNING'), f'Invalid expected finding type: {case_label}')
                    matched = any(
                        message['source'].startswith(expected['source_prefix'])
                        and message['line'] == expected['line']
                        and (expected_type is None or message['type'] == expected_type)
                        for message in result['messages']
                    )
                    require(matched, f"Missing expected finding: {case_label}, line {expected['line']}, "
                            f"{expected_type or 'any type'} {expected['source_prefix']}")
                elif expectation == 'no_findings':
                    require(result['exit'] == 0 and not result['messages'], f'Unexpected control finding: {case_label}')
                else:
                    require(expectation == 'observe_only', f'Unknown fixture expectation: {case_label}')
                baseline.append({'file': case['file'], 'target': version, 'result': result,
                                 'expectation': expectation})
        annotation_results = []
        for f in sorted((subject / 'annotations').iterdir()):
            for version in source['targets']:
                respected = scan(f, version, False)
                ignored = scan(f, version)
                require(not respected['messages'] and respected['exit'] == 0, 'Annotation control not suppressed')
                require(any(m['source'].startswith('PHPCompatibility.FunctionUse.RemovedFunctions.mysql_query') for m in ignored['messages']), 'Ignore-annotations did not expose finding')
                annotation_results.append({'file': f.name, 'target': version, 'control': respected, 'ignored': ignored})
        # Remove Composer from scan PATH and deny this PHP executable's outbound traffic.
        pwsh = shutil.which('pwsh')
        network_env = dict(env, DK_T04_PHP=str(php), DK_T04_RULE='DK-T04-' + os.environ.get('GITHUB_RUN_ID', 'local'))
        def powershell(code, label):
            return run([pwsh, '-NoProfile', '-NonInteractive', '-Command', "$ErrorActionPreference='Stop';" + code], label, network_env)
        socket = [php, '-n', '-r', '$s=@fsockopen("repo.packagist.org",443,$e,$m,5);exit($s?0:7);']
        require(run(socket, 'network-before').returncode == 0, 'Network positive control failed')
        created = powershell('New-NetFirewallRule -Name $env:DK_T04_RULE -DisplayName $env:DK_T04_RULE -Direction Outbound -Action Block -Program $env:DK_T04_PHP -Profile Any | Out-Null', 'firewall-create')
        require(created.returncode == 0, 'Could not create isolated PHP firewall rule')
        offline_equal = []
        old_path = poisoned['PATH']
        try:
            require(run(socket, 'network-blocked').returncode == 7, 'PHP network was not blocked')
            poisoned['PATH'] = str(pack / 'php')
            require(shutil.which('composer', path=poisoned['PATH']) is None, 'Composer still on scan PATH')
            for entry in baseline:
                result = scan(subject / entry['file'], entry['target'])
                offline_equal.append(result == entry['result'])
            require(all(offline_equal), 'Offline results differ')
        finally:
            poisoned['PATH'] = old_path
            removed = powershell('Remove-NetFirewallRule -Name $env:DK_T04_RULE', 'firewall-remove')
            require(removed.returncode == 0, 'Could not remove PHP firewall rule')
        require(run(socket, 'network-after').returncode == 0, 'Network not restored')
        after = source_snapshot(subject)
        require(after == before, 'Target source tree changed')
    php_files = prefix_inventory(pack, 'php')
    if selection is not None:
        require(php_files == {'php/' + row['path']: row['sha256'] for row in selection['keptFiles']},
                'Runtime files changed after verified archive selection')
    engine_files = prefix_inventory(pack, 'engine')
    limitations = ['PHPCompatibility 10.0.0-alpha2 is a pre-release; static checks do not guarantee runtime compatibility.',
                   'The dynamic-property fixture is not detected by this engine.']
    targets = {v: {'passed': True, 'limitations': limitations} for v in source['targets']}
    if '8.5' in targets:
        targets['8.5']['limitations'] = limitations + [
            'PHP 8.5 compatibility rules run on PHP CLI 8.4. New PHP 8.5 syntax is not fully validated.',
            'The PHP 8.5 pipe operator fixture is observational only. Its result does not establish syntax support.',
        ]
    results = {
        'packId': pack_id, 'reportProfile': PROFILE, 'runtimeProfile': profile,
        'runtimeSelectionPath': 'verification/runtime-selection.json' if selection else None,
        'environment': {'windows': platform.platform(), 'python': platform.python_version(),
                        'runId': os.environ.get('GITHUB_RUN_ID'), 'sourceSha': os.environ.get('GITHUB_SHA')},
        'versions': versions, 'runtimeVersion': source['php']['version'], 'cliVersion': runtime_version, 'archiveSha256': source['php']['sha256'],
        'engineFiles': engine_files, 'runtimeFiles': php_files, 'targets': targets,
        'checks': {k: True for k in ['fixtureRegression', 'annotations', 'sourceUnchanged', 'offline',
                                    'configIsolation', 'jsonContract', 'withoutComposer']},
        'cases': baseline, 'annotations': annotation_results, 'offlineComparisons': offline_equal,
        'sourceSnapshots': {'before': before, 'after': after},
        'unverified': ['Clean Windows 11 CRT/DLL deployment (T09)', 'App pack consumption (T05/T06)',
                       'License compliance and corresponding source delivery (T08)', 'Pack replacement/restore (T10)'],
    }
    write(pack / 'verification/results.json', results)
    licenses = []
    for folder in ['php', 'engine']:
        for name, digest in prefix_inventory(pack, folder).items():
            if any(x in Path(name).name.lower() for x in ['licen', 'copying', 'notice', 'copyright']):
                licenses.append({'path': name, 'sha256': digest})
    write(pack / 'verification/license-files.json', licenses)
    manifest = {
        'schemaVersion': 1, 'packId': pack_id, 'reportProfile': PROFILE,
        'runtime': {'version': source['php']['version'], 'cliVersion': runtime_version, 'sourceUrl': source['php']['url'],
                    'archiveSha256': source['php']['sha256'], 'executable': 'php/php.exe', 'files': php_files},
        'engine': {'versions': versions, 'lockSha256': sha(engine / 'composer.lock'),
                   'entryPoint': 'engine/vendor/squizlabs/php_codesniffer/bin/phpcs',
                   'ruleset': 'engine/ruleset.xml', 'files': engine_files},
        'targets': [{'version': v, 'verificationRef': 'verification/results.json#targets/' + v,
                     'limitations': targets[v]['limitations']} for v in source['targets']],
        'verification': {'reportPath': 'verification/results.json', 'files': prefix_inventory(pack, 'verification')},
    }
    write(pack / 'manifest.json', manifest)
    validate(pack)
    shutil.copy2(pack / 'manifest.json', raw / 'manifest.json')
    shutil.copy2(pack / 'verification/results.json', raw / 'results.json')
    shutil.copy2(pack / 'verification/license-files.json', raw / 'license-files.json')
    write(raw / 'files.json', inventory(pack))
    if selection is not None:
        shutil.copy2(selection_path, raw / 'runtime-selection.json')
    summary = {'packId': manifest['packId'], 'runtimeProfile': profile,
               'runtimeFileCount': len(php_files),
               'runtimeBytes': sum((pack / name).stat().st_size for name in php_files),
               'omittedRuntimeFileCount': selection['omittedFileCount'] if selection else None,
               'runtimeSelectionPath': 'verification/runtime-selection.json' if selection else None,
               'environment': results['environment'], 'checks': results['checks'],
               'cases': len(baseline), 'annotationComparisons': len(annotation_results),
               'offlineComparisons': len(offline_equal), 'versions': versions, 'runtimeVersion': source['php']['version'], 'cliVersion': runtime_version,
               'manifestSha256': sha(pack / 'manifest.json'), 'fileCount': len(inventory(pack)),
               'licenseFileCount': len(licenses), 'targets': list(targets), 'unverified': results['unverified']}
    write(raw / 'summary.json', summary)
    print('T04_SUMMARY_JSON=' + json.dumps(summary))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--prepare', action='store_true')
    parser.add_argument('--select-runtime', action='store_true')
    parser.add_argument('--profile', choices=('Full', 'MinimalCli'))
    parser.add_argument('--archive', type=Path)
    parser.add_argument('--archive-directory', type=Path)
    parser.add_argument('pack', type=Path)
    args = parser.parse_args()
    if args.select_runtime:
        require(not args.prepare and args.profile is not None and args.archive is not None
                and args.archive_directory is not None, 'Runtime selection needs profile, archive and extracted directory')
        select_runtime(args.pack.resolve(), args.archive_directory.resolve(), args.archive.resolve(), args.profile)
    elif args.prepare:
        prepare(args.pack.resolve(), args.profile)
    else:
        validate(args.pack.resolve())
        print('Pack contract validation passed.')
