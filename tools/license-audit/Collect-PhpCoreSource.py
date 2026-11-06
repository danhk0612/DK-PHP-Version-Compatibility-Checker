"""Maintainer-only pinned PHP/core-dependency source collection; no build execution.

Creates matching source/provenance candidates, not a redistribution clearance or
proof that all compiler inputs/relink requirements have been satisfied.
"""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import subprocess
import tempfile
import zipfile

PHP_VERSION = '8.4.27'
PHP_TAG = 'php-8.4.27'
PHP_COMMIT = '00d8bae801541db0d95a698e108bb4b1b273fe6e'
PHP_REPOSITORY = 'https://github.com/php/php-src.git'
CORE_DEPENDENCIES = {'libiconv': '1.19', 'libxml2': '2.11.9-8',
                     'zlib': '1.3.2-1', 'wineditline': '2.208'}
REPO_TIMEOUT = 300
SOURCE_DIGEST_GENERATOR_COMMIT = '73f9b80a55bf82cb4d3218c1e985a426ce289367'
SOURCE_DIGEST_GENERATOR_URL = ('https://github.com/php/php-sdk-binary-tools/blob/'
                               + SOURCE_DIGEST_GENERATOR_COMMIT + '/bin/phpsdk_sbom.php')


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def run(arguments):
    result = subprocess.run(arguments, capture_output=True, timeout=REPO_TIMEOUT)
    require(result.returncode == 0,
            'Source collection command failed: ' + ' '.join(arguments[:5]) + '\n'
            + result.stderr.decode('utf-8', errors='replace')[-2000:])
    return result.stdout


def git(checkout, *arguments):
    return run(['git', '-c', 'protocol.file.allow=never', '-C', str(checkout), *arguments])


def source_identity(component):
    properties = {p['name']: p['value'] for p in component.get('properties', [])}
    repository = properties.get('php:source-url', '')
    reference = properties.get('php:source-commit', '')
    require(re.fullmatch(r'https://github\.com/winlibs/[A-Za-z0-9_.-]+', repository),
            'Unexpected core dependency repository: ' + repository)
    require(re.fullmatch(r'[0-9a-f]{40}', reference), 'Expected pinned source commit')
    return {'name': component['name'], 'version': component['version'],
            'repository': repository + '.git', 'commit': reference,
            'sourceRef': properties.get('php:source-ref'),
            'sbomRef': component.get('bom-ref'), 'licenses': component.get('licenses', []),
            'inclusionBasis': 'Core source/build and safe PHP module observations; not all SBOM candidates are included'}


def checked_name(name):
    path = PurePosixPath(name)
    require(not path.is_absolute() and path.as_posix() == name
            and all(part not in ('', '.', '..', '.git') for part in path.parts),
            'Noncanonical tracked source path: ' + name)
    return path


def raw_blobs(checkout, entries):
    """Read original objects through one batch process, independent of checkout filters."""
    process = subprocess.Popen(['git', '-c', 'protocol.file.allow=never', '-C', str(checkout),
                                'cat-file', '--batch'], stdin=subprocess.PIPE,
                               stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    try:
        for filename, mode, blob in entries:
            process.stdin.write((blob + '\n').encode('ascii'))
            process.stdin.flush()
            header = process.stdout.readline().split()
            require(len(header) == 3 and header[0].decode() == blob and header[1] == b'blob',
                    'Unexpected Git batch object header: ' + filename)
            size = int(header[2])
            data = process.stdout.read(size)
            require(len(data) == size and process.stdout.read(1) == b'\n',
                    'Truncated Git batch object: ' + filename)
            actual = hashlib.sha1(b'blob ' + str(size).encode() + b'\0' + data).hexdigest()
            require(actual == blob, 'Raw Git blob SHA-1 mismatch: ' + filename)
            yield filename, mode, blob, data
        process.stdin.close()
        require(process.wait(timeout=REPO_TIMEOUT) == 0, 'Git batch object reader failed')
    finally:
        if process.poll() is None:
            process.kill()
            process.wait()
        process.stdout.close()
        process.stderr.close()
        if not process.stdin.closed:
            process.stdin.close()


def collect_source(identity, output):
    name, commit = identity['name'], identity['commit']
    # Source filenames can exceed Windows MAX_PATH below a nested project path.
    # Keep only final archives/inventories under output; remove the short checkout
    # on both successful collection and exceptions.
    with tempfile.TemporaryDirectory(prefix='dk-src-') as work:
        checkout = Path(work) / 's'
        run(['git', 'init', '--quiet', str(checkout)])
        for key, value in [('core.autocrlf', 'false'), ('core.eol', 'lf'),
                           ('core.symlinks', 'false'), ('core.longpaths', 'true')]:
            git(checkout, 'config', key, value)
        git(checkout, 'remote', 'add', 'origin', identity['repository'])
        git(checkout, 'fetch', '--no-tags', '--depth', '1', 'origin', commit)
        git(checkout, 'checkout', '--detach', '--quiet', 'FETCH_HEAD')
        actual = git(checkout, 'rev-parse', 'HEAD').decode().strip()
        require(actual == commit, 'Fetched source commit mismatch: ' + name)
        entries = []
        for entry in git(checkout, 'ls-files', '--stage', '-z').split(b'\0'):
            if not entry:
                continue
            header, raw_name = entry.split(b'\t', 1)
            mode, blob, stage = header.decode().split()
            filename = raw_name.decode('utf-8')
            checked_name(filename)
            require(stage == '0' and mode in ('100644', '100755', '120000'),
                    'Unsupported Git source mode (including unresolved submodule): ' + header.decode() + ' ' + filename)
            entries.append((filename, mode, blob))
        entries.sort()
        require(entries, 'Empty source checkout: ' + name)
        archive = output / (name + '-' + commit + '.zip')
        prefix = name + '-' + commit + '/'
        rows, legal, build, checkout_differences = [], [], [], []
        with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as package:
            for filename, mode, blob, data in raw_blobs(checkout, entries):
                # core.symlinks=false materializes link blobs as plain target text.
                # Retain their Git mode in both the manifest and ZIP Unix attributes.
                source = checkout.joinpath(*PurePosixPath(filename).parts)
                require(not source.is_symlink(), 'Unexpected filesystem symlink: ' + filename)
                checkout_data = source.read_bytes()
                if checkout_data != data:
                    checkout_differences.append({'path': filename, 'checkoutBytes': len(checkout_data),
                        'checkoutSha256': digest(checkout_data), 'gitBlobSha256': digest(data),
                        'differenceKind': ('git-ident-expansion' if
                            re.sub(rb'\$Id: [0-9a-f]{40} \$', b'$Id$', checkout_data) == data
                            else 'line-ending-conversion' if
                            checkout_data.replace(b'\r\n', b'\n') == data.replace(b'\r\n', b'\n')
                            else 'other-checkout-transformation')})
                row = {'path': filename, 'gitMode': mode, 'gitBlob': blob,
                       'bytes': len(data), 'sha256': digest(data)}
                rows.append(row)
                info = zipfile.ZipInfo(prefix + filename, date_time=(1980, 1, 1, 0, 0, 0))
                info.create_system = 3
                info.external_attr = int(mode, 8) << 16
                info.compress_type = zipfile.ZIP_DEFLATED
                package.writestr(info, data)
                basename = PurePosixPath(filename).name
                if re.match(r'(?i)^(license|licence|copying|notice|copyright)', basename) or filename == 'README.REDIST.BINS':
                    destination = output / 'licenses' / name / filename
                    destination.parent.mkdir(parents=True, exist_ok=True)
                    destination.write_bytes(data)
                    legal.append({'sourcePath': filename, 'storedPath': destination.relative_to(output).as_posix(),
                                  'gitBlob': blob, 'sha256': row['sha256'], 'bytes': len(data)})
                if (re.search(r'(?i)(\.w32$|\.vcxproj$|\.sln$|\.mak$|(^|/)(makefile|configure|buildconf|cmakelists|meson\.build))', filename)
                        or filename in ('README.md', 'README', 'INSTALL')):
                    build.append(filename)
        if checkout_differences:
            names = b''.join(row['path'].encode('utf-8') + b'\0' for row in checkout_differences)
            result = subprocess.run(['git', '-C', str(checkout), 'check-attr', '--cached', '-z',
                                     '--stdin', 'text', 'eol', 'filter', 'working-tree-encoding', 'ident'],
                                    input=names, capture_output=True, timeout=REPO_TIMEOUT)
            require(result.returncode == 0, 'Unable to inspect pinned checkout attributes')
            values = result.stdout.rstrip(b'\0').split(b'\0')
            attributes = {}
            require(len(values) % 3 == 0, 'Unexpected Git attribute output')
            for offset in range(0, len(values), 3):
                path, attribute, value = (item.decode('utf-8') for item in values[offset:offset + 3])
                attributes.setdefault(path, {})[attribute] = value
            for row in checkout_differences:
                row['pinnedGitAttributes'] = attributes.get(row['path'], {})
        manifest = output / (name + '-' + commit + '-files.json')
        write(manifest, {'repository': identity['repository'], 'commit': actual,
                         'trackedFiles': rows, 'legalFiles': legal, 'buildMaterialPaths': build,
                         'checkoutDifferences': checkout_differences})
        summary = dict(identity, checkoutCommit=actual, trackedFileCount=len(rows),
                       archiveBytesMatchGitBlobs=True, gitBlobSha1Verified=True,
                       archiveSource='git cat-file --batch raw blob objects; no checkout conversion',
                       checkoutBytesMatchGitBlobs=not checkout_differences,
                       checkoutDifferenceCount=len(checkout_differences),
                       archive=archive.name, archiveSha256=digest(archive.read_bytes()),
                       archiveBytes=archive.stat().st_size, inventory=manifest.name,
                       inventorySha256=digest(manifest.read_bytes()), legalFiles=legal,
                       buildMaterialCount=len(build), buildMaterialPaths=build,
                       gitSymlinkCount=sum(mode == '120000' for _, mode, _ in entries))
        return summary, rows


def bundled_checks(sbom, php_rows):
    """Use the publisher's hash_source_path encoding, pinned in provenance.

    The official generator hashes a file directly. For directories it sorts
    normalized paths with SORT_STRING and hashes relative-path LF SHA256-hex LF.
    This compares the observed SBOM with collected Git-byte-verified source;
    it does not equate a source match with binary reproduction or legal clearance.
    """
    result = []
    by_name = {row['path']: row for row in php_rows}
    for component in sbom.get('components', []):
        properties = {item['name']: item['value'] for item in component.get('properties', [])}
        if properties.get('php:component-origin') != 'bundled':
            continue
        source_path = properties.get('php:source-path', '')
        checked_name(source_path)
        expected = properties.get('php:source-digest')
        source_format = properties.get('php:source-digest-format')
        actual, pending = None, None
        if source_format == 'file-content-v1':
            entry = by_name.get(source_path)
            rows = [entry] if entry else []
            if entry and entry['gitMode'] != '120000':
                actual = entry['sha256']
            else:
                pending = 'Source file missing or symlink semantics require separate review'
        elif source_format == 'relative-path-and-file-sha256-v1':
            rows = [dict(row, path=row['path'][len(source_path) + 1:])
                    for row in php_rows if row['path'].startswith(source_path + '/')]
            rows.sort(key=lambda row: row['path'].encode('utf-8'))
            if rows and not any(row['gitMode'] == '120000' for row in rows):
                payload = ''.join(row['path'] + '\n' + row['sha256'] + '\n'
                                  for row in rows).encode('utf-8')
                actual = digest(payload)
            else:
                pending = 'Source directory missing or symlink semantics require separate review'
        else:
            rows = []
            pending = 'Unsupported official source digest format'
        matched = actual is not None and actual == expected
        if actual is not None and not matched:
            pending = 'Collected source differs from the publisher source-digest assertion'
        generated_count = sum(row.get('origin') == 'generated-config' for row in rows)
        item = {'name': component['name'], 'version': component.get('version'),
                       'sourcePath': source_path, 'sourceFileCount': len(rows),
                       'gitByteIdentityVerified': bool(rows) and generated_count == 0,
                       'officialExpectedSha256': expected, 'officialActualSha256': actual,
                       'officialFormat': source_format,
                       'officialFormatSource': SOURCE_DIGEST_GENERATOR_URL,
                       'officialAggregateComparisonPerformed': actual is not None,
                       'officialAggregateMatchVerified': matched, 'pending': pending}
        if generated_count:
            item.update({'rawGitSourceFileCount': len(rows) - generated_count,
                         'generatedInputFileCount': generated_count,
                         'generatedInputBasis': 'Pinned Windows configure header recipe; not Git blob bytes'})
        result.append(item)
    return result


def generated_windows_source(php_rows, php_summary, output):
    """Reconstruct only the reviewed headers hashed after Windows configure.

    Raw Git archives remain unchanged. No JavaScript, configure or native build
    executes; the two generated inputs have separate files and provenance.
    """
    require(php_summary['commit'] == PHP_COMMIT, 'Unexpected PHP source generation commit')
    archive = output / php_summary['archive']
    require(digest(archive.read_bytes()) == php_summary['archiveSha256'],
            'Raw PHP source archive changed before generated input reconstruction')
    indexed = {row['path']: row for row in php_rows}
    reviewed = {
        'ext/mbstring/config.w32': '9d17f9be4068ce1961400250444d9477d62320833ae2c6a3929862e5577e03e9',
        'ext/mbstring/libmbfl/config.h.w32': '5554a29351d0aca23bde2bf26790ee7b4ff43578e6101ee5fefc1c1134287535',
        'ext/date/config.w32': '6c7603af630544ef0805ef35c579a94dc7d380b9ee1b97d5fd8132b8e153a3a7',
    }
    inputs, payloads = [], {}
    with zipfile.ZipFile(archive) as package:
        for path, expected in reviewed.items():
            row = indexed.get(path)
            require(row is not None and row['gitMode'] in ('100644', '100755'),
                    'Missing pinned configure input: ' + path)
            data = package.read('php-' + PHP_COMMIT + '/' + path)
            blob = hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()
            require(blob == row['gitBlob'] and digest(data) == row['sha256'] == expected,
                    'Pinned configure input bytes changed: ' + path)
            payloads[path] = data
            inputs.append({'sourcePath': path, 'gitBlob': blob, 'sha256': expected,
                           'bytes': len(data), 'sourceCommit': PHP_COMMIT})

    mbstring = payloads['ext/mbstring/config.w32'].decode('utf-8')
    require('FSO.CopyFile("ext\\\\mbstring\\\\libmbfl\\\\config.h.w32",' in mbstring
            and '"ext\\\\mbstring\\\\libmbfl\\\\config.h", true);' in mbstring,
            'Reviewed libmbfl CopyFile recipe changed')
    date = payloads['ext/date/config.w32'].decode('utf-8')
    require('FSO.CreateTextFile("ext/date/lib/timelib_config.h", true)' in date,
            'Reviewed timelib header destination changed')
    literals = re.findall(r'tl_config.WriteLine\("(.*)"\);', date)
    require(len(literals) == 10, 'Reviewed timelib literal count changed')
    # These pinned literals use JSON-compatible JavaScript escapes; this is
    # literal decoding, never evaluation of a general JavaScript expression.
    date_data = ('\r\n'.join(json.loads('"' + value + '"') for value in literals)
                 + '\r\n').encode('ascii')
    outputs = [
        ('ext/mbstring/libmbfl/config.h', payloads['ext/mbstring/libmbfl/config.h.w32'],
         '5554a29351d0aca23bde2bf26790ee7b4ff43578e6101ee5fefc1c1134287535',
         'Copy config.h.w32 byte-for-byte to config.h per ext/mbstring/config.w32',
         ['ext/mbstring/config.w32', 'ext/mbstring/libmbfl/config.h.w32']),
        ('ext/date/lib/timelib_config.h', date_data,
         '4851e9b846d1af42859d4abb3b63c59824751730233beeff0e33be07a6ccca2d',
         'Decode ten reviewed WriteLine string literals; ASCII TextStream.WriteLine CRLF per ext/date/config.w32',
         ['ext/date/config.w32']),
    ]
    destination = output / 'generated-source'
    generated_archive = output / 'generated-windows-config.zip'
    require(not destination.exists() and not generated_archive.exists(),
            'Preserve existing generated input evidence; use a fresh output')
    rows, generated = [], []
    with zipfile.ZipFile(generated_archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as package:
        for path, data, expected, recipe, source_paths in outputs:
            require(path not in indexed and digest(data) == expected,
                    'Generated header changed or would replace a raw Git file: ' + path)
            file = destination / path
            file.parent.mkdir(parents=True, exist_ok=True)
            file.write_bytes(data)
            info = zipfile.ZipInfo(path, date_time=(1980, 1, 1, 0, 0, 0))
            info.create_system = 3
            info.external_attr = int('100644', 8) << 16
            info.compress_type = zipfile.ZIP_DEFLATED
            package.writestr(info, data)
            rows.append({'path': path, 'gitMode': '100644', 'bytes': len(data),
                         'sha256': expected, 'origin': 'generated-config'})
            generated.append({'path': path, 'storedPath': file.relative_to(output).as_posix(),
                              'bytes': len(data), 'sha256': expected, 'recipe': recipe,
                              'inputSourcePaths': source_paths, 'gitByteIdentityVerified': False})
    require(digest(archive.read_bytes()) == php_summary['archiveSha256'],
            'Raw PHP source archive changed during generated input reconstruction')
    provenance = {'sourceCommit': PHP_COMMIT, 'rawArchive': php_summary['archive'],
                  'rawArchiveSha256': php_summary['archiveSha256'], 'rawArchiveUnchanged': True,
                  'inputs': inputs, 'outputs': generated, 'archive': generated_archive.name,
                  'archiveSha256': digest(generated_archive.read_bytes()),
                  'archiveBytes': generated_archive.stat().st_size,
                  'scope': 'Two configured header inputs, separate from unmodified Git sources; no configure script or native build executed',
                  'nativeBuildOrRelinkVerified': False, 'redistributionCleared': False}
    write(output / 'generated-windows-config.json', provenance)
    return rows, provenance


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--sbom', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--php-info', type=Path, help='safe candidate module/configuration text, with environment/variables excluded')
    parser.add_argument('--probe', type=Path, help='safe candidate phpVersion/passwordAlgorithms/extensions JSON')
    args = parser.parse_args()
    require(not args.output.exists(), 'Output exists; preserve it and use a fresh collection directory')
    sbom_bytes = args.sbom.read_bytes()
    sbom = json.loads(sbom_bytes.decode('utf-8-sig'))
    require(sbom['metadata']['component']['name'] == 'php'
            and sbom['metadata']['component']['version'] == PHP_VERSION, 'Unexpected PHP source version in SBOM')
    args.output.mkdir(parents=True)
    require(args.sbom.resolve() != args.output.resolve()
            and not args.sbom.resolve().is_relative_to(args.output.resolve()), 'SBOM must be outside the fresh output')
    tag_rows = run(['git', '-c', 'protocol.file.allow=never', 'ls-remote', '--tags',
                    PHP_REPOSITORY, 'refs/tags/' + PHP_TAG, 'refs/tags/' + PHP_TAG + '^{}']).decode().splitlines()
    tag_values = dict(line.split('\t', 1)[::-1] for line in tag_rows)
    tag_commit = tag_values.get('refs/tags/' + PHP_TAG + '^{}', tag_values.get('refs/tags/' + PHP_TAG))
    require(tag_commit == PHP_COMMIT, 'Official PHP tag moved or no longer resolves to the pinned source commit')
    php_identity = {'name': 'php', 'version': PHP_VERSION, 'repository': PHP_REPOSITORY,
                    'commit': PHP_COMMIT, 'sourceRef': PHP_TAG,
                    'inclusionBasis': 'Application source for approved PHP CLI and php8.dll; source archive alone is not relink/legal clearance'}
    identities = [php_identity]
    for name, version in CORE_DEPENDENCIES.items():
        matches = [c for c in sbom.get('components', []) if c.get('name') == name and c.get('version') == version
                   and any(p.get('name') == 'php:source-url' for p in c.get('properties', []))]
        require(len(matches) == 1, 'Expected one exact core source identity in SBOM: ' + name + '@' + version)
        identities.append(source_identity(matches[0]))
    observations = {'provided': args.php_info is not None, 'probeProvided': args.probe is not None}
    if args.probe:
        probe_bytes = args.probe.read_bytes()
        probe = json.loads(probe_bytes.decode('utf-8-sig'))
        require(isinstance(probe, dict) and set(probe) == {'phpVersion', 'passwordAlgorithms', 'extensions'},
                'Expected only safe version/algorithm/extension probe fields')
        require(probe['phpVersion'] == PHP_VERSION, 'Probe PHP version differs from the pinned source')
        algorithms = probe['passwordAlgorithms']
        require(isinstance(algorithms, list) and all(isinstance(item, str) for item in algorithms),
                'Expected password algorithm names in candidate probe')
        observations.update({'probeSha256': digest(probe_bytes), 'passwordAlgorithms': algorithms})
        if any(algorithm.startswith('argon2') for algorithm in algorithms):
            matches = [component for component in sbom.get('components', [])
                       if component.get('name') == 'libargon2' and component.get('version') == '20190702']
            require(len(matches) == 1, 'Expected one exact libargon2 source candidate for enabled Argon2')
            identity = source_identity(matches[0])
            require(identity['commit'] == 'fb53ef37be212380d51877b70f514f3bf2c1ee40',
                    'Argon2 source candidate differs from reviewed SBOM identity')
            identity['inclusionBasis'] = ('Candidate password_algos enables Argon2; source version/ref is from '
                                          'the official SBOM, not a runtime-reported library version')
            identities.append(identity)
            observations['argon2SourceVersionBasis'] = 'SBOM source candidate; runtime reports algorithm availability only'
    if args.php_info:
        info = args.php_info.read_bytes()
        text = info.decode('utf-8-sig')
        require(not any(value in text for value in ('$_SERVER[', '$_ENV[', 'GITHUB_', 'ACTIONS_', 'RUNNER_', 'GH_TOKEN')),
                'Use safe PHP module/configuration output, not environment/variable data')
        require('iconv library version => 1.19' in text and 'BCMath support => enabled' in text,
                'Candidate core observation differs from pinned source assumptions')
        observations.update({'sha256': digest(info), 'iconvVersion': '1.19', 'bcmathEnabled': True,
                             'note': 'Compiled/runtime metadata supports inclusion but does not prove every static object or complete relink inputs'})
    sources, php_rows, php_summary = [], None, None
    for identity in identities:
        summary, rows = collect_source(identity, args.output)
        sources.append(summary)
        if identity['name'] == 'php':
            php_rows, php_summary = rows, summary
        print('Collected source ' + identity['name'] + ': ' + str(summary['trackedFileCount']) + ' checked files', flush=True)
    raw_bundled = bundled_checks(sbom, php_rows)
    generated_rows, generated_provenance = generated_windows_source(php_rows, php_summary, args.output)
    bundled = bundled_checks(sbom, php_rows + generated_rows)
    source_matches = bool(bundled) and all(row['officialAggregateMatchVerified'] for row in bundled)
    report = {'phpVersion': PHP_VERSION, 'phpSourceCommit': PHP_COMMIT,
              'inputSbomSha256': digest(sbom_bytes), 'inputSbomComponentCount': len(sbom['components']),
              'sbomScope': 'Candidate superset; only independently scoped core dependencies selected here',
              'observations': observations, 'sources': sources,
              'rawBundledSourceChecks': raw_bundled,
              'generatedWindowsSource': generated_provenance,
              'bundledSourceChecks': bundled,
              'sourceDigestGenerator': {'repository': 'https://github.com/php/php-sdk-binary-tools',
                                        'commit': SOURCE_DIGEST_GENERATOR_COMMIT,
                                        'file': 'bin/phpsdk_sbom.php', 'url': SOURCE_DIGEST_GENERATOR_URL,
                                        'gitBlob': 'c616a6c3a4f896ca9708e89476340e3c9ce00f20'},
              'sourceCollectionPassed': source_matches, 'bundledSourceMatchesSbom': source_matches, 'redistributionCleared': False,
              'sourceCollectionScope': 'Raw tracked source archives plus two pinned Windows configure header inputs reproduce bundled source digests; not complete binary reproduction or relink clearance',
              'nativeBuildOrRelinkVerified': False,
              'remaining': ['Confirm complete binary-to-source/build-configuration correspondence and relink route',
                            'Provide matching source, legal texts and reconstruction material durably with the final distribution',
                            'Retain Microsoft/proprietary toolchain terms separately; no compiler/toolchain files were copied or executed']}
    write(args.output / 'core-source.json', report)
    print('PHP_CORE_SOURCE_JSON=' + json.dumps({'phpSourceCommit': PHP_COMMIT,
          'archives': len(sources), 'trackedFiles': sum(x['trackedFileCount'] for x in sources),
          'sourceCollectionPassed': source_matches, 'redistributionCleared': False}))
    require(source_matches, 'Pinned PHP source does not match all official bundled-source digest assertions; inspect core-source.json')


if __name__ == '__main__':
    main()
