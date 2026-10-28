"""Additional T01 experiments; only disposable Windows runner fixtures are changed."""
import json
import hashlib
import io
import urllib.request
import zipfile
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time

import run_probe as base

AREA = base.OUT / 'extended'
ENV = dict(base.ENV)


def command(label, args, env=ENV):
    result = subprocess.run(args, env=env, cwd=base.PROBE, capture_output=True, timeout=45)
    (base.OUT / ('extended-' + label + '.stdout')).write_bytes(result.stdout)
    (base.OUT / ('extended-' + label + '.stderr')).write_bytes(result.stderr)
    return {'exit': result.returncode,
            'stdout': result.stdout.decode('utf-8', 'replace').strip(),
            'stderr': result.stderr.decode('utf-8', 'replace').strip()}


def powershell(label, script, env=ENV):
    return command(label, ['pwsh', '-NoProfile', '-NonInteractive', '-Command',
                           "$ErrorActionPreference='Stop'; " + script], env)


def main():
    if sys.platform != 'win32':
        raise SystemExit('Actual Windows is required.')
    AREA.mkdir(exist_ok=False)
    results = {'run_id': os.environ.get('GITHUB_RUN_ID'),
               'commit': os.environ.get('GITHUB_SHA'), 'checks': {}, 'failures': [],
               'unverified': ['clean-PC CRT/DLL requirements',
                              'general non-admin user behavior', 'arbitrary link cycles',
                              'all possible PHP changes and application runtime behavior']}

    def check(name, passed, observation):
        results['checks'][name] = {'passed': bool(passed), 'observation': observation}
        if not passed:
            results['failures'].append(name)

    clean = AREA / 'clean.php'
    clean.write_text('<?php\n$value = 1;\n', encoding='utf-8')

    # Positive control: the injected ini actually prepends managed test code without -n.
    poison = AREA / 'poison.php'
    poison.write_text("<?php echo 'T01_INI_POISON';\n", encoding='utf-8')
    ini = AREA / 'poison.ini'
    ini.write_text('auto_prepend_file="' + poison.as_posix() + '"\n', encoding='utf-8')
    ini_dir = AREA / 'ini-scan'
    ini_dir.mkdir()
    shutil.copy2(ini, ini_dir / 'poison.ini')
    poisoned = dict(ENV, PHPRC=str(ini), PHP_INI_SCAN_DIR=str(ini_dir))
    positive = command('ini-positive', [base.PHP, str(clean)], poisoned)
    before = base.snapshot(AREA)
    actual = base.scan('extended-ini-isolation', clean, env=poisoned)
    check('external_ini_isolation', 'T01_INI_POISON' in positive['stdout']
          and actual['json_valid'] and actual['exit'] == 0 and not actual['messages']
          and base.snapshot(AREA) == before, {'control': positive, 'scan': actual})

    # Managed PHPCS bootstrap contaminates stdout; target PHP is never executed.
    actual = base.scan('extended-contaminated', clean, extra_args=['--bootstrap=' + str(poison)])
    raw = (base.OUT / 'extended-contaminated.stdout').read_bytes()
    check('contaminated_json_rejected', raw.startswith(b'T01_INI_POISON') and not actual['json_valid'],
          {'scan': actual, 'prefix': raw[:80].decode('utf-8', 'replace')})

    # Controlled recursive batch: 131 PHP files and one INC file, plus a TXT exclusion.
    tree = AREA / 'traversal'
    for folder in [tree, tree / 'nested', tree / 'skip', tree / 'bulk']:
        folder.mkdir()
    for relative in ['a.php', 'nested/b.php', 'skip/c.php', 'extra.inc', 'extra.txt']:
        (tree / relative).write_text('<?php\n$value = 1;\n', encoding='utf-8')
    for i in range(128):
        (tree / 'bulk' / f'file-{i:03}.php').write_text('<?php\n$value = 1;\n', encoding='utf-8')
    before = base.snapshot(tree)
    for name, flags, expected in [('recursive_php', [], 131), ('local_only', ['-l'], 1),
                                  ('php_and_inc', [], 132),
                                  ('exclude_skip', ['--ignore=*/skip/*'], 130)]:
        started = time.monotonic()
        actual = base.scan('extended-' + name, tree, extra_args=flags,
                           extensions='php,inc' if name == 'php_and_inc' else 'php')
        actual['elapsed_seconds'] = round(time.monotonic() - started, 3)
        check(name, actual['json_valid'] and actual['exit'] == 0 and not actual['messages']
              and len(actual.get('files', [])) == expected and base.snapshot(tree) == before, actual)

    # Observe whether native directory traversal crosses a junction or symlink.
    file_list = AREA / 'selected-files.txt'
    file_list.write_text('\n'.join(str(tree / name) for name, kind in before.items()
                                  if kind != 'directory' and name.endswith('.php')) + '\n', encoding='utf-8')
    outside = AREA / 'outside-selected-root'
    outside.mkdir()
    (outside / 'outside.php').write_text('<?php\n$value = 1;\n', encoding='utf-8')
    outside_before = base.snapshot(outside)
    link_env = dict(ENV, T01_LINK_TARGET=str(outside))
    for kind, ps_kind in [('junction', 'Junction'), ('symlink', 'SymbolicLink')]:
        link = tree / ('link-' + kind)
        link_env['T01_LINK_PATH'] = str(link)
        created = powershell('create-' + kind,
                             "New-Item -ItemType " + ps_kind +
                             " -Path $env:T01_LINK_PATH -Target $env:T01_LINK_TARGET | Out-Null", link_env)
        observation = {'creation': created}
        try:
            if created['exit'] == 0:
                actual = base.scan('extended-' + kind, tree)
                observation['scan'] = actual
                observation['outside_file_reported'] = any(
                    Path(p).name == 'outside.php' for p in actual.get('files', []))
                observation['engine_succeeded'] = actual['json_valid'] and actual['exit'] == 0
                if kind == 'junction':
                    # Reproduce the observed engine crash; do not label native traversal safe.
                    observed = actual['exit'] == 255 and not actual['json_valid']
                    observation['known_native_failure_reproduced'] = observed
                else:
                    observed = actual['json_valid'] and actual['exit'] == 0
                check(kind + '_observation', observed, observation)
                selected = base.scan('extended-selected-' + kind, None,
                                     extra_args=['--file-list=' + str(file_list)])
                check(kind + '_explicit_file_list', selected['json_valid'] and selected['exit'] == 0
                      and len(selected.get('files', [])) == 131
                      and not any(Path(p).name == 'outside.php' for p in selected.get('files', [])), selected)
            else:
                check(kind + '_observation', False, observation)
        finally:
            if link.is_dir():
                os.rmdir(link)  # Remove only the test link, never its target.
        if base.snapshot(outside) != outside_before:
            results['failures'].append(kind + '_outside_changed')
    check('traversal_inputs_unchanged', base.snapshot(tree) == before
          and base.snapshot(outside) == outside_before, {'outside_unchanged': base.snapshot(outside) == outside_before})

    # Deny only this generated file, confirm PHP cannot read it, then restore the test ACL.
    denied = AREA / 'denied.php'
    denied.write_text('<?php\n$value = 1;\n', encoding='utf-8')
    original_hash = base.digest(denied)
    user = subprocess.check_output(['whoami'], text=True).strip()
    apply = command('deny-acl', ['icacls', str(denied), '/deny', user + ':(R)'])
    try:
        control = command('deny-control', [base.PHP, '-n', '-r',
                          'exit(@file_get_contents($argv[1])===false?9:0);', str(denied)])
        actual = base.scan('extended-denied', denied)
        check('read_denied', apply['exit'] == 0 and control['exit'] == 9 and actual['exit'] != 0,
              {'acl': apply, 'control': control, 'scan': actual})
    finally:
        restored = command('restore-acl', ['icacls', str(denied), '/remove:d', user])
    check('read_denied_restored', restored['exit'] == 0 and base.digest(denied) == original_hash, restored)

    # Verify the official candidate ZIP, without treating it as a product selection.
    metadata_url = 'https://downloads.php.net/~windows/releases/releases.json'
    metadata_bytes = urllib.request.urlopen(metadata_url, timeout=45).read()
    metadata = json.loads(metadata_bytes)['8.4']
    entry = metadata['nts-vs17-x64']['zip']
    zip_url = 'https://downloads.php.net/~windows/releases/' + entry['path']
    archive_bytes = urllib.request.urlopen(zip_url, timeout=90).read()
    archive_hash = hashlib.sha256(archive_bytes).hexdigest()
    (base.OUT / 'official-php-metadata.json').write_bytes(metadata_bytes)
    with zipfile.ZipFile(io.BytesIO(archive_bytes)) as archive:
        exe_hash = hashlib.sha256(archive.read('php.exe')).hexdigest()
        dll_differences = []
        for name in archive.namelist():
            if name.lower().endswith('.dll'):
                installed = Path(base.PHP).parent / name
                if not installed.is_file() or base.digest(installed) != hashlib.sha256(archive.read(name)).hexdigest():
                    dll_differences.append(name)
    check('official_php_zip', archive_hash == entry['sha256'] and exe_hash == base.digest(Path(base.PHP)),
          {'metadata_url': metadata_url, 'distribution_version': metadata['version'],
           'zip_url': zip_url, 'official_sha256': entry['sha256'], 'downloaded_sha256': archive_hash,
           'php_exe_sha256': exe_hash, 'runtime_exe_matches': exe_hash == base.digest(Path(base.PHP)),
           'dll_missing_or_different_from_setup_runtime': dll_differences})

    # Firewall rule applies only to this PHP executable; runner/upload processes keep networking.
    rule_env = dict(ENV, T01_RULE_NAME='DK-T01-' + os.environ.get('GITHUB_RUN_ID', 'local'),
                    T01_PHP_PATH=str(Path(base.PHP).resolve()))
    socket_code = '$s=@fsockopen("repo.packagist.org",443,$e,$m,5);exit($s?0:7);'
    network_before = command('network-before', [base.PHP, '-n', '-r', socket_code])
    created = powershell('firewall-create',
        'New-NetFirewallRule -Name $env:T01_RULE_NAME -DisplayName $env:T01_RULE_NAME '
        '-Direction Outbound -Action Block -Program $env:T01_PHP_PATH -Profile Any | Out-Null', rule_env)
    offline = {'before': network_before, 'rule_creation': created}
    try:
        blocked = command('network-blocked', [base.PHP, '-n', '-r', socket_code])
        offline['blocked'] = blocked
        baseline = json.loads((base.OUT / 'summary.json').read_text())['cases']
        offline['case_comparisons'] = []
        before_offline = base.snapshot(base.INPUTS)
        for case in baseline:
            actual = base.scan('extended-offline-' + Path(case['file']).stem + '-' + case['target'],
                               base.INPUTS / case['file'], case['target'])
            equal = all(actual.get(k) == case.get(k) for k in ['exit', 'json_valid', 'files', 'totals', 'messages'])
            offline['case_comparisons'].append({'file': case['file'], 'target': case['target'], 'same_as_online': equal})
        offline['input_tree_unchanged'] = base.snapshot(base.INPUTS) == before_offline
        check('php_network_blocked_scan', network_before['exit'] == 0 and created['exit'] == 0
              and blocked['exit'] == 7 and offline['input_tree_unchanged']
              and all(c['same_as_online'] for c in offline['case_comparisons']), offline)
    finally:
        removed = powershell('firewall-remove',
                             'Remove-NetFirewallRule -Name $env:T01_RULE_NAME', rule_env)
    network_after = command('network-after', [base.PHP, '-n', '-r', socket_code])
    check('php_network_restored', removed['exit'] == 0 and network_after['exit'] == 0,
          {'removal': removed, 'after': network_after})

    results['scope_passed'] = not results['failures']
    base.save_json(base.OUT / 'extended-summary.json', results)
    print('T01_EXTENDED_JSON=' + json.dumps(results, ensure_ascii=True))
    raise SystemExit(1 if results['failures'] else 0)


if __name__ == '__main__':
    main()
