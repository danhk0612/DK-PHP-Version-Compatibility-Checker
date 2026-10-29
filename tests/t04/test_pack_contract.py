"""Mutation tests for real prepared pack acceptance boundaries (no engine execution)."""
import copy
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'tools/engine-pack'))
from pack_contract import read, require, sha, validate, write

pack = Path(sys.argv[1]).resolve()
manifest = pack / 'manifest.json'
original = manifest.read_bytes()
accepted = validate(pack)
rejected = []


def reject(name, mutate):
    data = copy.deepcopy(accepted)
    mutate(data)
    write(manifest, data)
    try:
        validate(pack)
    except (ValueError, KeyError, TypeError):
        rejected.append(name)
    else:
        raise AssertionError('Invalid pack accepted: ' + name)
    finally:
        manifest.write_bytes(original)


reject('unknown schema', lambda m: m.update(schemaVersion=2))
reject('unknown profile', lambda m: m.update(reportProfile='future-report'))
reject('unverified target', lambda m: m['targets'].append({'version': '99.0', 'verificationRef': 'verification/results.json#targets/99.0', 'limitations': []}))
reject('duplicate target', lambda m: m['targets'].append(copy.deepcopy(m['targets'][0])))
reject('version mismatch', lambda m: m['engine']['versions'].update({'phpcompatibility/php-compatibility': '0.0'}))
reject('lock hash mismatch', lambda m: m['engine'].update(lockSha256='0' * 64))
reject('traversal path', lambda m: m['engine']['files'].update({'../escape.php': '0' * 64}))
reject('absolute path', lambda m: m['runtime']['files'].update({'C:/outside/php.exe': '0' * 64}))
reject('invalid evidence ref', lambda m: m['targets'][0].update(verificationRef='verification/missing.json'))
# Evidence whose manifest hash is updated must still satisfy acceptance policy.
evidence_path = pack / 'verification/results.json'
evidence_before = evidence_path.read_bytes()
for name, mutation in [
    ('failed verification evidence', lambda e: e['checks'].update(offline=False)),
    ('missing required evidence', lambda e: e['checks'].pop('offline')),
    ('missing target controls', lambda e: e.update(cases=[])),
]:
    evidence = read(evidence_path)
    mutation(evidence)
    write(evidence_path, evidence)
    try:
        reject(name, lambda m: m['verification']['files'].update({'verification/results.json': sha(evidence_path)}))
    finally:
        evidence_path.write_bytes(evidence_before)
# Mutate actual files and always restore. Hash inventory must reject these changes.
for name in ['engine/ruleset.xml', 'engine/composer.lock', 'php/php.exe', 'verification/results.json']:
    path = pack / name
    before = path.read_bytes()
    try:
        path.write_bytes(before + b'\nCHANGED')
        try:
            validate(pack)
        except ValueError:
            rejected.append('changed ' + name)
        else:
            raise AssertionError('Changed file accepted: ' + name)
    finally:
        path.write_bytes(before)
extra = pack / 'engine/unlisted.php'
try:
    extra.write_text('<?php // unexpected')
    try:
        validate(pack)
    except ValueError:
        rejected.append('unlisted file')
    else:
        raise AssertionError('Unlisted file accepted')
finally:
    extra.unlink()
require(validate(pack) == accepted and manifest.read_bytes() == original, 'Original pack not restored')
result = {'passed': True, 'rejectedCases': rejected, 'restoredPackValid': True}
write(Path('artifacts/t04/contract-tests.json'), result)
print('T04_CONTRACT_JSON=' + __import__('json').dumps(result))
