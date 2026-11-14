"""Compare fresh Full/MinimalCli packs and execute isolated Windows CLI probes."""
import argparse
import ctypes
import ctypes.wintypes
import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import struct
import subprocess
import sys
import tempfile
import traceback

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / 'tools/engine-pack'))
from pack_contract import inventory, read, require, sha, validate, write

REQUIRED_CHECKS = {'fixtureRegression', 'annotations', 'sourceUnchanged', 'offline',
                   'configIsolation', 'jsonContract', 'withoutComposer'}


def normalized_snapshot(value):
    # The workflow may save the map directly, or wrap it as a source snapshot.
    return value['files'] if isinstance(value, dict) and 'files' in value else value


def verified_results(directory):
    result = read(directory / 'results.json')
    require(REQUIRED_CHECKS <= result['checks'].keys(), 'Missing required preparation checks')
    require(all(result['checks'][key] is True for key in REQUIRED_CHECKS), 'Preparation check failed')
    require(result['sourceSnapshots']['before'] == result['sourceSnapshots']['after'], 'Inspected fixture changed')
    targets = set(read(REPO / 'tools/engine-pack/sources.json')['targets'])
    definitions = read(REPO / 'tests/fixtures/cases.json')['cases']
    expected_cases = sorted((case['file'], target) for case in definitions for target in case['targets'])
    expected_annotations = sorted((file.name, target) for file in (REPO / 'tests/t04/annotations').iterdir()
                                  for target in targets)
    require(sorted((case['file'], case['target']) for case in result['cases']) == expected_cases
            and sorted((case['file'], case['target']) for case in result['annotations']) == expected_annotations,
            'Unexpected fixture/annotation coverage')
    require(len(result['offlineComparisons']) == len(expected_cases) and all(x is True for x in result['offlineComparisons']),
            'Offline results differ or coverage is incomplete')
    return result


def runtime_environment(php_directory):
    # A setup-PHP/full-runtime PATH must not rescue omitted native dependencies.
    system = Path(os.environ['SystemRoot'])
    return dict(os.environ, PHPRC='', PHP_INI_SCAN_DIR='', COMPOSER_DISABLE_NETWORK='1',
                PATH=os.pathsep.join(map(str, [php_directory, system / 'System32', system])))


def php_probes(full, minimal, output):
    php_directory = minimal / 'php'
    php = php_directory / 'php.exe'
    env = runtime_environment(php_directory)

    def run(arguments, name):
        result = subprocess.run([str(x) for x in arguments], cwd=php_directory, env=env,
                                capture_output=True, timeout=45)
        (output / (name + '.stdout')).write_bytes(result.stdout)
        (output / (name + '.stderr')).write_bytes(result.stderr)
        return result

    code = "echo json_encode(['phpVersion'=>PHP_VERSION,'passwordAlgorithms'=>password_algos(),'extensions'=>get_loaded_extensions()], JSON_THROW_ON_ERROR);"
    probe = run([php, '-n', '-r', code], 'php-probe')
    require(probe.returncode == 0 and not probe.stderr.strip(), 'Minimal CLI probe failed')
    data = json.loads(probe.stdout.decode('utf-8-sig'))
    require(data['phpVersion'] == read(minimal / 'manifest.json')['runtime']['cliVersion'], 'Probe PHP version differs')
    required_extensions = {'libxml', 'SimpleXML', 'tokenizer', 'xmlwriter'}
    require(required_extensions <= set(data['extensions']), 'PHPCS required extensions are unavailable')
    write(output / 'php-probe.json', data)

    info = run([php, '-n', '-r', 'phpinfo(INFO_GENERAL | INFO_CONFIGURATION | INFO_MODULES | INFO_LICENSE);'], 'php-info')
    require(info.returncode == 0 and not info.stderr.strip(), 'Limited PHP configuration inspection failed')
    text = info.stdout.decode('utf-8-sig')
    require(not any(token in text for token in ('$_ENV[', '$_SERVER[', 'GITHUB_', 'ACTIONS_', 'RUNNER_', 'GH_TOKEN')),
            'Unexpected environment/server-variable output; do not publish')
    (output / 'php-info-no-ini.txt').write_bytes(info.stdout)

    imports = []
    # Use the unchanged official tool, but its temporary directory contains no
    # other PHP DLLs. Do not let its Full executable directory rescue a dependency.
    with tempfile.TemporaryDirectory(prefix='dk-minimal-deplister-') as temporary:
        tool = Path(temporary) / 'deplister.exe'
        shutil.copyfile(full / 'php/deplister.exe', tool)
        require(sha(tool) == sha(full / 'php/deplister.exe'), 'Inspection tool copy changed')
        for name in ('php.exe', 'php8.dll'):
            result = run([tool, php_directory / name], 'minimal-imports-' + name)
            # The upstream tool returns BindImageEx BOOL directly: TRUE is exit1.
            require(result.returncode == 1 and not result.stderr.strip(), 'Native import inspection did not return TRUE')
            rows = []
            for line in result.stdout.decode('utf-8-sig').splitlines():
                require(',' in line, 'Unrecognized dependency row')
                module, status = line.rsplit(',', 1)
                row = {'module': module, 'result': status}
                if status == 'NOTFOUND' and module.lower().startswith('api-ms-win-crt-'):
                    # BindImageEx searches physical files; API-set contracts can
                    # instead be mapped by the actual Windows loader to UCRT.
                    # Verify that mapping rather than ignoring a filename prefix.
                    library = ctypes.WinDLL(module)
                    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
                    filename = kernel.GetModuleFileNameW
                    filename.argtypes = (ctypes.wintypes.HMODULE, ctypes.wintypes.LPWSTR, ctypes.wintypes.DWORD)
                    filename.restype = ctypes.wintypes.DWORD
                    buffer = ctypes.create_unicode_buffer(32768)
                    require(filename(library._handle, buffer, len(buffer)) > 0, 'Cannot resolve API-set backing module')
                    backing = Path(buffer.value).resolve()
                    require(backing.is_file() and backing.is_relative_to(Path(os.environ['SystemRoot']).resolve()),
                            'API-set backing module is not a Windows system file')
                    row.update({'windowsLoaderResolved': True, 'backingModule': str(backing), 'backingSha256': sha(backing)})
                rows.append(row)
            require(rows and all(row['result'] == 'OK' or row.get('windowsLoaderResolved') is True for row in rows),
                    'Minimal runtime has an unresolved dependency')
            imports.append({'image': name, 'exitCode': result.returncode, 'imports': rows})
    write(output / 'minimal-imports.json', {'toolSource': 'https://github.com/php/php-src/blob/php-8.4.27/win32/build/deplister.c',
                                           'isolation': 'temporary tool copy; minimal cwd and Windows-only PATH', 'images': imports})
    return data, imports


def compare(args):
    full, minimal = args.full.resolve(), args.minimal.resolve()
    full_manifest, minimal_manifest = validate(full), validate(minimal)
    before = normalized_snapshot(read(args.full_before))
    require(before == inventory(full), 'Full baseline changed while preparing MinimalCli')
    full_result, minimal_result = verified_results(args.full_results), verified_results(args.minimal_results)
    require(full_result['cases'] == minimal_result['cases'], 'Full/MinimalCli case results differ')
    require(full_result['annotations'] == minimal_result['annotations'], 'Full/MinimalCli annotation results differ')
    require(full_result['offlineComparisons'] == minimal_result['offlineComparisons'], 'Full/MinimalCli offline results differ')
    for name in ('versions', 'runtimeVersion', 'cliVersion', 'archiveSha256', 'targets', 'reportProfile'):
        require(full_result[name] == minimal_result[name], 'Changed runtime/engine/target contract: ' + name)
    require(full_manifest['packId'] != minimal_manifest['packId'], 'Candidate needs a distinct pack ID')
    for name in ('version', 'cliVersion', 'sourceUrl', 'archiveSha256'):
        require(full_manifest['runtime'][name] == minimal_manifest['runtime'][name], 'Changed PHP identity: ' + name)
    require(full_manifest['engine']['versions'] == minimal_manifest['engine']['versions'], 'Changed engine versions')
    require(full_manifest['engine']['lockSha256'] == minimal_manifest['engine']['lockSha256'], 'Changed engine lock')
    for package in full_manifest['engine']['versions']:
        prefix = 'engine/vendor/' + package + '/'
        full_package = {k: v for k, v in full_manifest['engine']['files'].items() if k.startswith(prefix)}
        minimal_package = {k: v for k, v in minimal_manifest['engine']['files'].items() if k.startswith(prefix)}
        require(full_package and full_package == minimal_package, 'Changed fixed package source: ' + package)
    for name in ('engine/composer.json', 'engine/composer.lock', 'engine/ruleset.xml'):
        require(sha(full / name) == sha(minimal / name), 'Changed engine input: ' + name)

    configured = read(REPO / 'tools/engine-pack/minimal-cli.json')['files']
    expected = {'php/' + name for name in configured}
    full_php = full_manifest['runtime']['files']
    minimal_php = minimal_manifest['runtime']['files']
    require(len(configured) == len(expected) and set(minimal_php) == expected, 'Minimal payload differs from the allowlist')
    require({name for name in minimal_php if Path(name).suffix.lower() in ('.exe', '.dll')} == {'php/php.exe', 'php/php8.dll'},
            'Minimal executable payload is not limited to php.exe/php8.dll')
    require(expected < set(full_php), 'Candidate is not a strict subset of original PHP payload')
    require(all(full_php[name] == digest for name, digest in minimal_php.items()), 'Retained PHP bytes changed')
    full_bytes = sum((full / name).stat().st_size for name in full_php)
    minimal_bytes = sum((minimal / name).stat().st_size for name in minimal_php)
    require(minimal_bytes < full_bytes, 'Candidate did not reduce PHP payload size')
    kept = [{'path': name, 'sha256': minimal_php[name], 'bytes': (minimal / name).stat().st_size} for name in sorted(minimal_php)]
    omitted = [{'path': name, 'sha256': full_php[name], 'bytes': (full / name).stat().st_size} for name in sorted(set(full_php) - expected)]
    for label, pack in (('full', full), ('minimal', minimal)):
        shutil.copyfile(pack / 'manifest.json', args.output / (label + '-manifest.json'))
        shutil.copyfile(pack / 'verification/runtime-selection.json', args.output / (label + '-runtime-selection.json'))
    probes, imports = php_probes(full, minimal, args.output)
    require(before == inventory(full), 'Full baseline changed during comparison/probes')
    return {'scopePassed': True, 'environment': {'windows': platform.platform(), 'python': platform.python_version(),
              'runId': os.environ.get('GITHUB_RUN_ID'), 'sourceSha': os.environ.get('GITHUB_SHA')},
            'fullPackId': full_manifest['packId'], 'minimalPackId': minimal_manifest['packId'],
            'fullManifestSha256': sha(full / 'manifest.json'), 'minimalManifestSha256': sha(minimal / 'manifest.json'),
            'fullBaselineUnchanged': True, 'targets': [row['version'] for row in minimal_manifest['targets']],
            'caseComparisons': len(full_result['cases']), 'annotationComparisons': len(full_result['annotations']),
            'offlineComparisonsPerProfile': len(full_result['offlineComparisons']), 'sourceSnapshotsUnchanged': True,
            'fixedEngineSourceUnchanged': True, 'fullPhpFileCount': len(full_php), 'minimalPhpFileCount': len(minimal_php),
            'fullPhpBytes': full_bytes, 'minimalPhpBytes': minimal_bytes, 'savedPhpBytes': full_bytes - minimal_bytes,
            'phpByteReductionPercent': (full_bytes - minimal_bytes) * 100.0 / full_bytes,
            'keptFiles': kept, 'omittedFiles': omitted, 'probe': probes, 'nativeImagesInspected': len(imports),
            'scope': 'Fresh Windows Full/MinimalCli manifest, exact subset, fixture/annotation/offline/source protection and isolated CLI probes; not native license clearance or Windows11 ZIP deployment.'}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('full', 'minimal', 'full-before', 'full-results', 'minimal-results', 'output'):
        parser.add_argument('--' + name, required=True, type=Path)
    args = parser.parse_args()
    require(os.name == 'nt' and struct.calcsize('P') == 8, 'Actual Windows x64 is required')
    args.output.mkdir(parents=True, exist_ok=True)
    require(not (args.output / 'comparison.json').exists(), 'Preserve previous comparison evidence; choose a fresh output')
    try:
        summary = compare(args)
    except Exception as error:
        summary = {'scopePassed': False, 'failure': str(error), 'details': traceback.format_exc()}
        write(args.output / 'comparison.json', summary)
        print('MINIMAL_COMPARISON_JSON=' + json.dumps(summary))
        return 1
    write(args.output / 'comparison.json', summary)
    print('MINIMAL_COMPARISON_JSON=' + json.dumps({key: value for key, value in summary.items() if key not in ('keptFiles', 'omittedFiles')}))
    return 0


if __name__ == '__main__':
    sys.exit(main())
