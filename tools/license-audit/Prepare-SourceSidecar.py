"""Build a reviewable source/notices companion from verified audit collections.

The output is preparation material, never a license clearance or publication.
No network requests, source execution, binary replacement or Git mutation occurs.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import zipfile

REPO = Path(__file__).resolve().parents[2]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def require(value, message):
    if not value:
        raise ValueError(message)


def inside(root, relative):
    path = (root / relative).resolve()
    require(path.is_relative_to(root.resolve()) and path.is_file(), 'Missing or escaping input: ' + relative)
    return path


def prepare(core_root, engine_root, output, build_root=None):
    require(not output.exists(), 'Preserve existing sidecar output; use a fresh path')
    core, engine = read(core_root / 'core-source.json'), read(engine_root / 'corresponding-source.json')
    require(core.get('sourceCollectionPassed') is True, 'Core configured-source comparison did not pass')
    require(len(core['sources']) == 6 and len(engine) == 2, 'Expected six core and two LGPL engine sources')
    require({s['name'] for s in core['sources']} == {'php', 'libiconv', 'libxml2', 'zlib', 'wineditline', 'libargon2'}, 'Unexpected core source collection')
    require({s['package'] for s in engine} == {'phpcompatibility/php-compatibility', 'phpcsstandards/phpcsutils'}, 'Unexpected engine source collection')
    election_path = REPO / 'tools/license-audit/license-election.json'
    election = read(election_path)
    require(election.get('electionMade') is True and election.get('phpVersion') == '4'
            and election.get('zendVersion') == '3.0', 'Approved PHP/Zend election is required')
    for item in election['selectedLicenseFiles'] + election['provenanceFiles']:
        require(sha(inside(REPO, item['path']).read_bytes()) == item['sha256'], 'Election text/provenance checksum mismatch')
    payload, origins = {}, []

    def add(path, destination, expected=None):
        data = path.read_bytes()
        require(expected is None or sha(data) == expected, 'Input checksum mismatch: ' + str(path))
        require(destination not in payload, 'Duplicate output member: ' + destination)
        payload[destination] = data
        return {'member': destination, 'sha256': sha(data), 'bytes': len(data)}

    for source in core['sources']:
        require(source.get('archiveBytesMatchGitBlobs') is True, 'Core archive lacks Git byte verification')
        origins.append(dict(component=source['name'], commit=source['commit'], **add(inside(core_root, source['archive']), 'sources/core/' + source['archive'], source['archiveSha256'])))
        add(inside(core_root, source['inventory']), 'inventories/core/' + source['inventory'], source['inventorySha256'])
    generated = core['generatedWindowsSource']
    add(inside(core_root, generated['archive']), 'sources/configured/' + generated['archive'], generated['archiveSha256'])
    add(core_root / 'generated-windows-config.json', 'inventories/generated-windows-config.json')
    add(core_root / 'core-source.json', 'inventories/core-source.json')
    for source in engine:
        require(source.get('vendorFilesMatchCheckout') is True, 'Engine source does not match installed vendor')
        origins.append(dict(component=source['package'], commit=source['sourceCommit'], **add(inside(engine_root, source['archivePath']), 'sources/engine/' + Path(source['archivePath']).name, source['archiveSha256'])))
        add(inside(engine_root, source['inventoryPath']), 'inventories/engine/' + Path(source['inventoryPath']).name, source['inventorySha256'])
    add(engine_root / 'corresponding-source.json', 'inventories/corresponding-source.json')

    build_sources_included = build_root is not None
    if build_root is not None:
        build = read(build_root / 'build-source.json')
        require(build.get('sourceCollectionPassed') is True and len(build['sources']) == 2,
                'Build source collection did not pass')
        for source in build['sources']:
            require(source.get('archiveBytesMatchGitBlobs') is True, 'Build source archive lacks Git byte verification')
            origins.append(dict(component=source['name'], commit=source['commit'], **add(inside(build_root, source['archive']), 'sources/build/' + source['archive'], source['archiveSha256'])))
            add(inside(build_root, source['inventory']), 'inventories/build/' + source['inventory'], source['inventorySha256'])
        add(build_root / 'build-source.json', 'inventories/build-source.json')

    # Include the exact current project source/recipes/notices, not export-ignore
    # archives or a link to a moving branch. Git metadata and build outputs stay out.
    tracked = subprocess.check_output(['git', '-C', str(REPO), 'ls-files', '-z']).split(b'\0')
    recipe_paths = {p.decode('utf-8') for p in tracked if p}
    recipe_paths.update(p.relative_to(REPO).as_posix() for p in (REPO / 'tools/license-audit').glob('*') if p.is_file())
    recipe_paths.update(p.relative_to(REPO).as_posix() for p in (REPO / '.github/workflows').glob('*.yml'))
    for relative in sorted(recipe_paths):
        if (relative.startswith(('src/', 'tools/', 'licenses/', 'tests/fixtures/', '.github/workflows/'))
                or relative in {'LICENSE', 'THIRD-PARTY-NOTICES.md', 'LICENSING.md', 'ARCHITECTURE.md', 'REQUIREMENTS.md', 'global.json'}):
            add(inside(REPO, relative), 'project/' + relative)
    instructions = '''This companion preserves original core/engine source archives and separate Windows configured headers.
The project/ directory contains the current build, pack and audit recipes and notices.
Build/audit steps: tools/engine-pack/README.md and tools/license-audit/README.md.
The marker relink experiment is not an official PHP binary reproduction.
Keep this companion and its SHA256 beside the exact reviewed binary distribution, on durable storage.
Temporary CI artifacts or upstream links alone are not the final source-delivery mechanism.
The PHP/Zend successor-license election is recorded in project/tools/license-audit/license-election.json.
Final payload correspondence and durable paired delivery remain release gates.
No compiler/SDK proprietary binaries are supplied in this source companion.
'''.encode('utf-8')
    payload['READ-ME.txt'] = instructions
    manifest = {'schemaVersion': 1, 'sources': origins, 'buildRecipeSourcesIncluded': build_sources_included,
                'members': [{'path': n, 'bytes': len(d), 'sha256': sha(d)} for n, d in sorted(payload.items())],
                'licenseElectionMade': True, 'licenseElection': election, 'licenseElectionSha256': sha(election_path.read_bytes()), 'redistributionCleared': False,
                'scope': 'Reviewed-input source and notice companion preparation; actual binary pairing and durable publication not established'}
    payload['source-sidecar-manifest.json'] = (json.dumps(manifest, indent=2) + '\n').encode()
    output.mkdir(parents=True)
    archive = output / 'DK-PHP-Compatibility-sources-and-notices.zip'
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as package:
        for name, data in sorted(payload.items()):
            member = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
            member.create_system = 3
            member.external_attr = 0o100644 << 16
            member.compress_type = zipfile.ZIP_DEFLATED
            package.writestr(member, data)
    result = dict(manifest, archive=archive.name, archiveBytes=archive.stat().st_size,
                  archiveSha256=sha(archive.read_bytes()))
    (output / 'source-sidecar.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--core-audit', type=Path, required=True)
    parser.add_argument('--engine-audit', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--build-audit', type=Path)
    args = parser.parse_args()
    result = prepare(args.core_audit.resolve(), args.engine_audit.resolve(), args.output.resolve(), args.build_audit.resolve() if args.build_audit else None)
    print(json.dumps({'archiveSha256': result['archiveSha256'], 'sourceArchives': len(result['sources']), 'redistributionCleared': False}))
