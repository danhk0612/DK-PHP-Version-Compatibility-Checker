"""Windows maintainer preparation/verification only; no GUI or app execution service."""
import argparse
import json
import os
from pathlib import Path
import platform
import shutil
import struct
import subprocess
import tempfile

from pack_contract import PROFILE, PACKAGES, inventory, read, require, sha, validate, write

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]


def prefix_inventory(root, folder):
    return {folder + '/' + k: v for k, v in inventory(root / folder).items()}


def prepare(pack):
    require(os.name == 'nt' and struct.calcsize('P') == 8, 'Actual Windows x64 required')
    source = read(HERE / 'sources.json')
    versions = {p['name']: p['version'] for p in read(pack / 'engine/composer.lock')['packages']}
    require(versions == PACKAGES, 'Approved engine versions differ')
    require(sha(pack / 'engine/composer.lock') == sha(HERE / 'composer.lock'), 'Lock changed during installation')
    php = pack / 'php/php.exe'
    phpcs = pack / 'engine/vendor/squizlabs/php_codesniffer/bin/phpcs'
    engine = pack / 'engine'
    env = dict(os.environ, PHPRC='', PHP_INI_SCAN_DIR='', COMPOSER_DISABLE_NETWORK='1')
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
        ini = subject / 'php.ini'
        ini.write_text('auto_prepend_file="' + (subject / 'bootstrap.php').as_posix() + '"\n', encoding='utf-8')
        poisoned = dict(env, PHPRC=str(ini), PHP_INI_SCAN_DIR=str(subject))
        before = inventory(subject)
        # Positive control: poison ini genuinely executes without -n.
        positive = run([php, subject / 'compatibility/mysql_control.php'], 'ini-positive', poisoned)
        require(positive.returncode != 0 and b'TARGET_EXECUTED' in positive.stdout + positive.stderr, 'Poison control did not execute')
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
            require(len(report['files']) == 1 and Path(next(iter(report['files']))).resolve() == file.resolve(), 'Selected/processed file mismatch')
            messages = next(iter(report['files'].values()))['messages']
            require(sum(m['type'] == 'ERROR' for m in messages) == report['totals']['errors']
                    and sum(m['type'] == 'WARNING' for m in messages) == report['totals']['warnings'], 'JSON totals mismatch')
            for m in messages:
                require(m['type'] in ('ERROR', 'WARNING') and type(m['line']) is int
                        and type(m['column']) is int and type(m['severity']) is int
                        and type(m['fixable']) is bool and isinstance(m['source'], str)
                        and isinstance(m['message'], str), 'JSON row contract mismatch')
            return {'exit': r.returncode, 'totals': report['totals'], 'messages': messages}
        baseline = []
        for case in definitions:
            for version, expected in case['targets'].items():
                require(version in source['targets'], 'Unexpected target fixture')
                result = scan(subject / case['file'], version)
                if expected['expectation'] == 'finding':
                    require(any(m['source'].startswith(expected['source_prefix']) and m['line'] == expected['line']
                                for m in result['messages']), 'Missing expected finding')
                elif expected['expectation'] == 'no_findings':
                    require(result['exit'] == 0 and not result['messages'], 'Unexpected control finding')
                baseline.append({'file': case['file'], 'target': version, 'result': result,
                                 'expectation': expected['expectation']})
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
        require(inventory(subject) == before, 'Target source tree changed')
    php_files = prefix_inventory(pack, 'php')
    engine_files = prefix_inventory(pack, 'engine')
    limitations = ['PHPCompatibility 10.0.0-alpha2 is a pre-release; static checks do not guarantee runtime compatibility.',
                   'The dynamic-property fixture is not detected by this engine.']
    targets = {v: {'passed': True, 'limitations': limitations} for v in source['targets']}
    results = {
        'packId': source['packId'], 'reportProfile': PROFILE,
        'environment': {'windows': platform.platform(), 'python': platform.python_version(),
                        'runId': os.environ.get('GITHUB_RUN_ID'), 'sourceSha': os.environ.get('GITHUB_SHA')},
        'versions': versions, 'runtimeVersion': source['php']['version'], 'cliVersion': runtime_version, 'archiveSha256': source['php']['sha256'],
        'engineFiles': engine_files, 'runtimeFiles': php_files, 'targets': targets,
        'checks': {k: True for k in ['fixtureRegression', 'annotations', 'sourceUnchanged', 'offline',
                                    'configIsolation', 'jsonContract', 'withoutComposer']},
        'cases': baseline, 'annotations': annotation_results, 'offlineComparisons': offline_equal,
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
        'schemaVersion': 1, 'packId': source['packId'], 'reportProfile': PROFILE,
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
    summary = {'packId': manifest['packId'], 'environment': results['environment'], 'checks': results['checks'],
               'cases': len(baseline), 'annotationComparisons': len(annotation_results),
               'offlineComparisons': len(offline_equal), 'versions': versions, 'runtimeVersion': source['php']['version'], 'cliVersion': runtime_version,
               'manifestSha256': sha(pack / 'manifest.json'), 'fileCount': len(inventory(pack)),
               'licenseFileCount': len(licenses), 'targets': list(targets), 'unverified': results['unverified']}
    write(raw / 'summary.json', summary)
    print('T04_SUMMARY_JSON=' + json.dumps(summary))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--prepare', action='store_true')
    parser.add_argument('pack', type=Path)
    args = parser.parse_args()
    if args.prepare:
        prepare(args.pack.resolve())
    else:
        validate(args.pack.resolve())
        print('Pack contract validation passed.')
