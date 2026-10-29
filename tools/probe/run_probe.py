"""T01 experiment only. Run on Windows after resolving tools/probe dependencies."""
import base64
import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import struct
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
PROBE = ROOT / 'tools' / 'probe'
OUT = ROOT / 'artifacts' / 't01'
INPUTS = OUT / 'inputs'
PHP = shutil.which('php')
PHPCS = PROBE / 'vendor' / 'squizlabs' / 'php_codesniffer' / 'bin' / 'phpcs'
RULESET = PROBE / 'ruleset.xml'
ENV = dict(os.environ, PHP_INI_SCAN_DIR='', PHPRC='')


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def snapshot(folder):
    return {str(p.relative_to(folder)): ('directory' if p.is_dir() else digest(p))
            for p in sorted(folder.rglob('*'))}


def save_json(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def scan(label, target, version='8.4', standard=RULESET, extra_args=(), env=None, extensions='php'):
    command = [PHP, '-n', str(PHPCS), '--standard=' + str(standard),
               '--runtime-set', 'testVersion', version, '--report=json', '-q',
               '--no-cache', '--extensions=' + extensions, *extra_args]
    if target is not None:
        command.append(str(target))
    result = subprocess.run(command, cwd=PROBE, env=ENV if env is None else env, capture_output=True, timeout=120)
    (OUT / (label + '.stdout')).write_bytes(result.stdout)
    (OUT / (label + '.stderr')).write_bytes(result.stderr)
    save_json(OUT / (label + '.command.json'), {'args': command, 'cwd': str(PROBE),
                                              'exit': result.returncode})
    try:
        report = json.loads(result.stdout)
        messages = [m for item in report['files'].values() for m in item['messages']]
        valid = all(isinstance(m['line'], int) and isinstance(m['column'], int)
                    and isinstance(m['severity'], int) and isinstance(m['source'], str)
                    and m['type'] in ('ERROR', 'WARNING') and isinstance(m['message'], str)
                    for m in messages)
        valid = valid and sum(m['type'] == 'ERROR' for m in messages) == report['totals']['errors']
        valid = valid and sum(m['type'] == 'WARNING' for m in messages) == report['totals']['warnings']
        return {'exit': result.returncode, 'json_valid': valid,
                'files': list(report['files']), 'totals': report['totals'], 'messages': messages}
    except (ValueError, KeyError, TypeError):
        return {'exit': result.returncode, 'json_valid': False, 'messages': [],
                'stdout_excerpt': result.stdout.decode('utf-8', 'replace')[-3000:],
                'stderr_excerpt': result.stderr.decode('utf-8', 'replace')[-3000:]}


def main():
    if sys.platform != 'win32' or struct.calcsize('P') != 8:
        raise SystemExit('Actual Windows x64 is required; no engine tests were run.')
    OUT.mkdir(parents=True, exist_ok=True)
    if not PHP or not PHPCS.is_file():
        raise SystemExit('PHP or prepared PHPCS is missing.')
    INPUTS.mkdir(exist_ok=False)
    fixtures = ROOT / 'tests' / 'fixtures'
    copied = INPUTS / 'compatibility'
    shutil.copytree(fixtures / 'compatibility', copied)
    before = snapshot(INPUTS)
    php_version = subprocess.check_output([PHP, '-n', '-v'], env=ENV).decode('utf-8', 'replace')
    php_modules = subprocess.check_output([PHP, '-n', '-m'], env=ENV).decode('utf-8', 'replace')
    summary = {'environment': {'windows': platform.platform(), 'python': sys.version,
                              'php': php_version, 'php_modules': php_modules,
                              'php_executable_sha256': digest(Path(PHP)),
                              'runner_image': os.environ.get('ImageVersion'),
                              'run_id': os.environ.get('GITHUB_RUN_ID'),
                              'commit': os.environ.get('GITHUB_SHA')},
               'cases': [], 'probes': {},
               'unverified': ['PHP ZIP source URL/hash and clean-PC CRT prerequisites',
                              'real network isolation', 'read-denied input',
                              'junction/symlink policies', 'stdout contamination handling']}
    failures = []
    definitions = json.loads((fixtures / 'cases.json').read_text(encoding='utf-8'))
    for case in definitions['cases']:
        for version, expected in case['targets'].items():
            label = Path(case['file']).stem + '-' + version
            actual = scan(label, INPUTS / case['file'], version)
            passed = actual['json_valid'] and actual['exit'] in (0, 1, 2, 3)
            passed = passed and len(actual.get('files', [])) == 1
            if expected['expectation'] == 'no_findings':
                passed = passed and actual['exit'] == 0 and not actual['messages']
            elif expected['expectation'] == 'finding':
                passed = passed and actual['exit'] != 0 and any(
                    m['source'].startswith(expected['source_prefix']) and m['line'] == expected['line']
                    for m in actual['messages'])
            actual.update(file=case['file'], target=version, expected=expected, passed=passed)
            summary['cases'].append(actual)
            if not passed:
                failures.append(label)
    summary['probes']['fixture_tree_unchanged'] = snapshot(INPUTS) == before
    if not summary['probes']['fixture_tree_unchanged']:
        failures.append('fixture_tree_changed')

    empty = INPUTS / 'empty'
    empty.mkdir()
    before = snapshot(INPUTS)
    summary['probes']['empty'] = scan('empty', empty)
    summary['probes']['invalid_standard'] = scan('invalid-standard', copied / 'mysql_control.php',
                                                standard=PROBE / 'missing-standard.xml')
    if summary['probes']['invalid_standard']['exit'] in (0, 1, 2, 3):
        failures.append('invalid_standard_was_not_an_execution_error')
    summary['probes']['failed_scan_tree_unchanged'] = snapshot(INPUTS) == before
    if not summary['probes']['failed_scan_tree_unchanged']:
        failures.append('failed_scan_tree_changed')

    guarded = INPUTS / '한글 space & meta'
    guarded.mkdir()
    sentinel = "<?php\nthrow new RuntimeException('T01_INPUT_EXECUTED');\n"
    (guarded / 'subject.php').write_text(sentinel, encoding='utf-8')
    (guarded / 'bootstrap.php').write_text(sentinel, encoding='utf-8')
    (guarded / 'vendor').mkdir()
    (guarded / 'vendor' / 'autoload.php').write_text(sentinel, encoding='utf-8')
    (guarded / 'phpcs.xml').write_text(
        '<ruleset name="must-not-load"><bootstrap>bootstrap.php</bootstrap>'
        '<rule ref="MissingStandardForT01"/></ruleset>', encoding='utf-8')
    save_json(guarded / 'composer.json', {'scripts': {'pre-install-cmd': '@php bootstrap.php'}})
    (guarded / 'php.ini').write_text('auto_prepend_file=bootstrap.php\n', encoding='utf-8')
    before = snapshot(guarded)
    actual = scan('guarded-path', guarded / 'subject.php')
    actual['tree_unchanged'] = snapshot(guarded) == before
    actual['passed'] = actual['json_valid'] and actual['exit'] == 0 and not actual['messages']
    actual['passed'] = actual['passed'] and actual['tree_unchanged']
    summary['probes']['guarded_path_and_config'] = actual
    if not actual['passed']:
        failures.append('guarded_path_and_config')

    lock = PROBE / 'composer.lock'
    if lock.is_file():
        shutil.copy2(lock, OUT / 'composer.lock')
        summary['environment']['composer_lock_sha256'] = digest(lock)
        print('T01_LOCK_BASE64=' + base64.b64encode(lock.read_bytes()).decode('ascii'))
    summary['failures'] = failures
    summary['scope_passed'] = not failures
    save_json(OUT / 'summary.json', summary)
    print('T01_SUMMARY_JSON=' + json.dumps(summary, ensure_ascii=True))
    print(f'T01: {len(summary["cases"])} cases, {len(failures)} failed; unverified checks remain.')
    raise SystemExit(1 if failures else 0)


if __name__ == '__main__':
    main()
