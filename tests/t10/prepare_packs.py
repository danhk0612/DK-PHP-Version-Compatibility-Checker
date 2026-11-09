"""Derive development-only subset/full packs from already verified evidence."""
import argparse
import copy
import shutil
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'tools/engine-pack'))
from pack_contract import inventory, read, require, sha, validate, write
p = argparse.ArgumentParser(description=__doc__)
p.add_argument('pack', type=Path)
p.add_argument('output', type=Path)
a = p.parse_args()
require(not a.output.exists(), 'Preserve existing T10 packs; use fresh output')
before = inventory(a.pack)
base = validate(a.pack)
require([t['version'] for t in base['targets']] == ['8.2', '8.3', '8.4'], 'Unexpected verified baseline')
for label, versions in [('reduced', ['8.2', '8.3']), ('full', ['8.2', '8.3', '8.4'])]:
    root = a.output / label
    shutil.copytree(a.pack, root)
    m = read(root / 'manifest.json')
    report = m['verification']['reportPath']
    e = read(root / report)
    m['packId'] += '-t10-' + label
    e['packId'] = m['packId']
    m['targets'] = [t for t in m['targets'] if t['version'] in versions]
    e['targets'] = {v: t for v, t in e['targets'].items() if v in versions}
    e['cases'] = [c for c in e['cases'] if c['target'] in versions]
    e['derivation'] = {'scope': 'Development-only restriction/reintroduction of previously verified targets; no new engine or future PHP claim', 'basePackId': base['packId'], 'baseManifestSha256': sha(a.pack / 'manifest.json')}
    write(root / report, e)
    m['verification']['files'][report] = sha(root / report)
    write(root / 'manifest.json', m)
    validate(root)
require(before == inventory(a.pack), 'Source pack changed during derivation')
print('T10 derived reduced/full development packs; source unchanged')
