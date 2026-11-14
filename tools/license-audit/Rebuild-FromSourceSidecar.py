"""Run the Windows PHP relink audit using the two paired candidate ZIPs."""
import argparse
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import urllib.request
import zipfile


def digest(data):
    return hashlib.sha256(data).hexdigest()


def read_json(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def check(condition, message):
    if not condition:
        raise ValueError(message)


def save(root, name, data):
    path = PurePosixPath(name)
    check(path.parts and path.as_posix() == name and not path.is_absolute()
          and all(part not in {'.', '..', '.git'} and ':' not in part and '\\' not in part
                  for part in path.parts), 'Unsafe ZIP path: ' + name)
    destination = root.joinpath(*path.parts)
    destination.parent.mkdir(parents=True, exist_ok=True)
    with destination.open('xb') as stream:
        stream.write(data)


def extract_source(companion, inventory_rows, core, component, destination):
    item = next(row for row in core['sources'] if row['name'] == component)
    member = 'sources/core/' + item['archive']
    archive = companion.read(member)
    check(digest(archive) == item['archiveSha256'], 'Core source archive hash mismatch: ' + component)
    inventory_name = 'inventories/core/' + item['inventory']
    inventory_bytes = companion.read(inventory_name)
    check(digest(inventory_bytes) == item['inventorySha256'], 'Core source inventory hash mismatch: ' + component)
    inventory = json.loads(inventory_bytes)
    prefix = Path(item['archive']).stem + '/'
    paths = {prefix + row['path']: row for row in inventory['trackedFiles']}
    with zipfile.ZipFile(io.BytesIO(archive)) as source:
        check(sorted(source.namelist()) == sorted(paths), 'Core source membership mismatch: ' + component)
        for name, row in paths.items():
            data = source.read(name)
            check(digest(data) == row['sha256'], 'Core source file hash mismatch: ' + name)
            save(destination, row['path'], data)
    if component == 'libiconv':
        save(inventory_rows, 'libiconv-inventory.json', inventory_bytes)
    return {'component': component, 'archiveSha256': item['archiveSha256'],
            'commit': item['commit'], 'files': len(paths)}


def run():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', required=True, type=Path)
    parser.add_argument('--binary', required=True, type=Path)
    parser.add_argument('--pair', required=True, type=Path)
    parser.add_argument('--work', required=True, type=Path)
    args = parser.parse_args()
    work = args.work.resolve()
    pair = read_json(args.pair)
    check(pair['scopePassed'] and pair['allSourceMembersVerified'], 'Expected verified ZIP pair')
    for path, field in ((args.source, 'sourceArchiveSha256'), (args.binary, 'binaryArchiveSha256')):
        check(digest(path.read_bytes()) == pair[field], 'Candidate archive hash mismatch: ' + path.name)
    work.mkdir()
    project, php, iconv = work / 'project', work / 'php', work / 'iconv'
    deps, sdk, pack, output = work / 'deps', work / 'sdk', work / 'runtime', work / 'result'
    with zipfile.ZipFile(args.source) as companion:
        manifest = json.loads(companion.read('source-sidecar-manifest.json'))
        core = json.loads(companion.read('inventories/core-source.json'))
        sources = [extract_source(companion, work, core, name, directory)
                   for name, directory in (('php', php), ('libiconv', iconv))]
        for row in manifest['members']:
            if row['path'].startswith('project/'):
                data = companion.read(row['path'])
                check(digest(data) == row['sha256'], 'Companion project file mismatch: ' + row['path'])
                save(project, row['path'].removeprefix('project/'), data)
    with zipfile.ZipFile(args.binary) as binary:
        for name in binary.namelist():
            if name.startswith('tools/runtime/') and not name.endswith('/'):
                save(pack, name.removeprefix('tools/runtime/'), binary.read(name))

    inputs = read_json(project / 'tests/t08-minimal/evidence/snapshot-deps.json')['files']
    for row in inputs:
        check(row['url'] == 'https://downloads.php.net/~windows/php-sdk/deps/vs17/x64/' + row['name'],
              'Unexpected dependency URL')
        print('Downloading ' + row['name'], flush=True)
        with urllib.request.urlopen(row['url'], timeout=90) as response:
            data = response.read()
        check(digest(data) == row['sha256'], 'Dependency SHA mismatch: ' + row['name'])
        with zipfile.ZipFile(io.BytesIO(data)) as archive:
            archive.extractall(deps)
    if (deps / 'openssl.cnf').is_file():
        (deps / 'template/ssl').mkdir(parents=True, exist_ok=True)
        shutil.move(deps / 'openssl.cnf', deps / 'template/ssl/openssl.cnf')

    sdk_pin = read_json(project / 'tools/license-audit/php-sdk.json')
    subprocess.run(['git', 'init', '--quiet', str(sdk)], check=True)
    subprocess.run(['git', '-C', str(sdk), 'fetch', '--depth=1', '--no-tags',
                    sdk_pin['repository'], sdk_pin['commit']], check=True)
    subprocess.run(['git', '-C', str(sdk), 'checkout', '--detach', '--quiet', 'FETCH_HEAD'], check=True)
    head = subprocess.check_output(['git', '-C', str(sdk), 'rev-parse', 'HEAD'], text=True).strip()
    check(head == sdk_pin['commit'], 'PHP SDK commit mismatch')

    # Run relative to the short workspace because the upstream SDK uses cmd paths.
    (work / 'relink.bat').write_text(
        '@echo off\n'
        'pwsh -NoProfile -File project\\tools\\license-audit\\Test-PhpRelink.ps1 '
        '-PhpSource php -LibiconvSource iconv -LibiconvInventory libiconv-inventory.json '
        '-DepsDirectory deps -Snapshot -OutputDirectory result\n'
        'exit /b %errorlevel%\n', encoding='ascii')
    print('Building PHP and libiconv from source companion', flush=True)
    subprocess.run(['cmd.exe', '/d', '/c', 'call', r'sdk\phpsdk-vs17-x64.bat', '-t', 'relink.bat'],
                   cwd=work, check=True)
    builds = read_json(output / 'php-relink.json')['results']
    comparison = work / 'comparison'
    subprocess.run([sys.executable, str(project / 'tests/t08-minimal/check_relinked_php.py'),
                    '--snapshot', '--original-php', builds[0]['executable'],
                    '--marker-php', builds[1]['executable'], '--pack', str(pack),
                    '--build-audit', str(output), '--output', str(comparison)], check=True)
    report = read_json(comparison / 'php-relink-comparison.json')
    evidence = {'sourceArchiveSha256': pair['sourceArchiveSha256'],
                'binaryArchiveSha256': pair['binaryArchiveSha256'], 'sourceInputs': sources,
                'sdkCommit': head, 'dependencyArchives': len(inputs),
                'comparedCases': report['comparedCases'],
                'staticExtensionsMatchingApprovedPhp': report['staticExtensionsMatchingApprovedPhp'],
                'scopePassed': True}
    (work / 'companion-relink.json').write_text(json.dumps(evidence, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    run()
