"""Collect pinned public build recipe sources, without installing toolchains."""
import argparse
import importlib.util
import json
from pathlib import Path

spec = importlib.util.spec_from_file_location('php_core_source', Path(__file__).with_name('Collect-PhpCoreSource.py'))
core = importlib.util.module_from_spec(spec)
spec.loader.exec_module(core)
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output', required=True, type=Path)
a = parser.parse_args()
core.require(not a.output.exists(), 'Preserve existing build-source collection')
a.output.mkdir(parents=True)
identities = [
    {'name': 'php-windows-builder', 'repository': 'https://github.com/php/php-windows-builder.git',
     'commit': '68dc945d9509c41b72332c74c8b99731bb75bc25', 'basis': 'Observed official PHP 8.4.27 build 1 workflow head'},
]
sources = [core.collect_source(identity, a.output)[0] for identity in identities]
sdk = json.loads(Path(__file__).with_name('php-sdk.json').read_text(encoding='utf-8'))
sources.append(core.collect_source(sdk, a.output, exclude_suffixes=('.exe', '.dll'))[0])
report = {'sources': sources, 'sourceCollectionPassed': True,
          'historicalBuildByteIdentityVerified': False, 'redistributionCleared': False,
          'scope': 'Pinned build recipes and original notices; Windows PHP SDK tool binaries excluded'}
core.write(a.output / 'build-source.json', report)
print(json.dumps({'sourceArchives': len(sources), 'redistributionCleared': False}))
