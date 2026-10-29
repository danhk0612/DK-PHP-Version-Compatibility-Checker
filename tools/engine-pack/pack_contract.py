"""Schema 1 maintainer pack validation. The Windows app consumer belongs to T05."""
import hashlib
import json
from pathlib import Path, PurePosixPath
import re

PROFILE = 'phpcs4-json-v1'
PACKAGES = {
    'phpcompatibility/php-compatibility': '10.0.0-alpha2',
    'squizlabs/php_codesniffer': '4.0.4',
    'phpcsstandards/phpcsutils': '1.2.3',
    'dealerdirect/phpcodesniffer-composer-installer': 'v1.1.0',
}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def write(path, data):
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')


def inventory(root):
    result = {}
    for p in sorted(root.rglob('*')):
        require(not p.is_symlink() and not (getattr(p.lstat(), 'st_file_attributes', 0) & 0x400), 'Links are not allowed in a pack')
        if p.is_file():
            result[p.relative_to(root).as_posix()] = sha(p)
    return result


def checked_file(root, name):
    require(isinstance(name, str) and name and '\\' not in name and ':' not in name,
            'Expected pack-relative POSIX file path')
    p = PurePosixPath(name)
    require(not p.is_absolute() and all(x not in ('.', '..') for x in p.parts)
            and p.as_posix() == name, 'Unsafe/noncanonical pack path')
    result = root.joinpath(*p.parts)
    require(result.resolve().is_relative_to(root.resolve()), 'Path escaped pack')
    return result


def validate(root):
    root = Path(root)
    m = read(root / 'manifest.json')
    require(type(m['schemaVersion']) is int and m['schemaVersion'] == 1, 'Unsupported schema')
    require(m['reportProfile'] == PROFILE, 'Unsupported report profile')
    require(isinstance(m['packId'], str) and re.fullmatch(r'[A-Za-z0-9._-]+', m['packId']), 'Invalid packId')
    for section, folder in [('runtime', 'php'), ('engine', 'engine'), ('verification', 'verification')]:
        files = m[section]['files']
        require(isinstance(files, dict) and files, 'Empty file inventory')
        for name, digest in files.items():
            p = checked_file(root, name)
            require(name.startswith(folder + '/') and re.fullmatch('[0-9a-f]{64}', digest), 'Invalid inventory entry')
            require(p.is_file() and sha(p) == digest, 'Missing/changed pack file: ' + name)
        actual = {folder + '/' + k: v for k, v in inventory(root / folder).items()}
        require(files == actual, 'Unexpected/unlisted pack file in ' + folder)
    require(set(inventory(root)) == {'manifest.json'} | set(m['runtime']['files'])
            | set(m['engine']['files']) | set(m['verification']['files']), 'Unexpected pack root file')
    require(m['runtime']['executable'] == 'php/php.exe' and m['engine']['entryPoint'] ==
            'engine/vendor/squizlabs/php_codesniffer/bin/phpcs' and m['engine']['ruleset'] ==
            'engine/ruleset.xml', 'Unexpected execution paths')
    require(m['runtime']['executable'] in m['runtime']['files'], 'Missing PHP executable inventory')
    require(m['engine']['entryPoint'] in m['engine']['files'] and m['engine']['ruleset'] in m['engine']['files'], 'Missing engine execution files')
    runtime = m['runtime']
    require(runtime['sourceUrl'].startswith('https://downloads.php.net/~windows/releases/')
            and re.fullmatch('[0-9a-f]{64}', runtime['archiveSha256']), 'Invalid PHP source')
    lock = read(root / 'engine/composer.lock')
    versions = {p['name']: p['version'] for p in lock['packages']}
    require(versions == m['engine']['versions'] and not lock['packages-dev'], 'Lock/version mismatch')
    installed = read(root / 'engine/vendor/composer/installed.json')['packages']
    require({p['name']: p['version'] for p in installed} == versions, 'Installed versions mismatch')
    require({p['name']: p['source']['reference'] for p in installed} ==
            {p['name']: p['source']['reference'] for p in lock['packages']}, 'Installed references mismatch')
    require(m['engine']['lockSha256'] == sha(root / 'engine/composer.lock'), 'Lock hash mismatch')
    evidence = read(checked_file(root, m['verification']['reportPath']))
    require(m['verification']['reportPath'] in m['verification']['files'], 'Untracked evidence')
    require(evidence['reportProfile'] == m['reportProfile'] and evidence['packId'] == m['packId'], 'Evidence profile/id mismatch')
    require(evidence['versions'] == versions and evidence['runtimeVersion'] == runtime['version']
            and evidence['archiveSha256'] == runtime['archiveSha256'], 'Evidence version mismatch')
    require(evidence['engineFiles'] == m['engine']['files'] and evidence['runtimeFiles'] == m['runtime']['files'], 'Evidence configuration/hash mismatch')
    require(evidence['checks'] and all(value is True for value in evidence['checks'].values()), 'Failed verification')
    required = {'fixtureRegression', 'annotations', 'sourceUnchanged', 'offline', 'configIsolation', 'jsonContract', 'withoutComposer'}
    require(required <= evidence['checks'].keys(), 'Missing required checks')
    targets = m['targets']
    require(isinstance(targets, list) and targets, 'No verified targets')
    seen = set()
    for target in targets:
        v = target['version']
        require(isinstance(v, str) and re.fullmatch(r'[0-9]+\.[0-9]+', v) and v not in seen, 'Invalid/duplicate target')
        seen.add(v)
        require(target['verificationRef'] == m['verification']['reportPath'] + '#targets/' + v,
                'Invalid target evidence reference')
        cases = [x for x in evidence['cases'] if x['target'] == v]
        require(any(x['expectation'] == 'finding' for x in cases) and any(x['expectation'] == 'no_findings' for x in cases), 'Missing target controls')
        actual = evidence['targets'].get(v, {})
        require(actual.get('passed') is True and actual.get('limitations') == target['limitations'], 'Unverified target')
        require(isinstance(target['limitations'], list) and all(isinstance(x, str) for x in target['limitations']), 'Missing limitations')
    require(seen == set(evidence['targets']), 'Evidence/target list mismatch')
    return m
