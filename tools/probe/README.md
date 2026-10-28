# T01 Windows 검증 준비

상태: **미실행 준비안**. 이 manifest는 제품의 엔진 채택 결정이 아닙니다. Composer validate/update, PHP 실행, 아래 PowerShell 절차는 아직 실행하지 않았습니다. composer.lock과 실제 JSON 결과를 임의로 만들지 않았습니다.

## 실험 조합

PHPCompatibility 10.0.0-alpha2 / PHP_CodeSniffer 4.0.4 / PHPCSUtils 1.2.3 / Composer installer 1.1.0을 정확한 버전으로 요청합니다. Utils 1.1.2의 공개 보안 문제 때문에 수정판 1.2.3을 후보로 사용합니다. 실제 의존성 해석·추가 보안 검사는 미완료입니다. Composer 보안 차단이 발생하면 우회하지 않고 원인을 보고합니다.

- [Utils 1.2.3 manifest](https://github.com/PHPCSStandards/PHPCSUtils/blob/1.2.3/composer.json): PHPCS ^3.13.5 또는 ^4.0.1 요구. 4.0.4는 선언 범위에 포함됩니다.
- [Utils 보안 공지](https://github.com/PHPCSStandards/PHPCSUtils/security/advisories/GHSA-r6hr-vr92-vv28)
- [alpha2 manifest](https://github.com/PHPCompatibility/PHPCompatibility/blob/10.0.0-alpha2/composer.json)

## 1. 환경 확보와 설치

실제 Windows x64에서 저장소를 준비합니다. 공식 보안 수정판 PHP x64 CLI와 Composer PHAR를 검증 관리 영역에 확보하고 다운로드 URL·버전·원본 공식 해시·실측 SHA-256·Windows 빌드·CRT 상태를 ENGINE_PROBE.md에 기록합니다. PHP ZIP의 필수 DLL을 임의 삭제하지 않습니다. 대상 버전별 PHP 실행 파일은 필요하지 않습니다.

다음은 **PowerShell 명령 초안**입니다. 경로는 실제 확보한 경로로 대체합니다. 검사 대상 프로젝트에서는 Composer를 실행하지 않습니다. 이 디렉터리는 도구 준비용 관리 영역입니다.

```powershell
$repo = (Resolve-Path 'C:\work\DK-PHP-Version-Compatibility-Checker').Path
$php = 'C:\dk-probe-runtime\php\php.exe'
$composer = 'C:\dk-probe-runtime\composer.phar'
$probe = Join-Path $repo 'tools\probe'
$results = Join-Path $repo 'artifacts\t01'
New-Item -ItemType Directory -Force -Path $results | Out-Null
$env:PHP_INI_SCAN_DIR = ''

& $php -n -v
& $php -n -m
Get-FileHash $php -Algorithm SHA256
Get-FileHash $composer -Algorithm SHA256
& $php $composer --version
& $php $composer --working-dir=$probe validate --no-check-publish
if ($LASTEXITCODE -ne 0) { throw 'Composer validate failed; record the error.' }
& $php $composer --working-dir=$probe update --no-dev --prefer-dist --no-interaction --no-scripts
# 실패하면 검사를 시작하지 않고 원인을 기록합니다.
if ($LASTEXITCODE -ne 0) { throw 'Composer update failed; record the error.' }
& $php $composer --working-dir=$probe show --format=json
& $php $composer --working-dir=$probe audit --format=json
if ($LASTEXITCODE -ne 0) { throw 'Composer audit failed; record the error.' }
```

Composer는 검증 호스트의 도구 설정을 사용합니다. PHP 설정이 필요하면 검증 전용 ini를 명시하고 내용을 보존합니다. PHPCS 필수 확장 libxml/SimpleXML/tokenizer/xmlwriter가 `-n -m`에서 유효한지 확인합니다. 부족하면 이 명령을 그대로 실행하지 않고 전용 ini와 명시한 확장만으로 재시험하여 방법을 기록합니다. Composer 플러그인 허용은 표준 등록용 installer에 한정합니다. 생성된 composer.lock과 실제 설치 목록을 검증 후 기록·커밋하며 vendor/는 커밋하지 않습니다. manifest는 내부 실험용이므로 자체 프로그램의 최종 라이선스를 선언하지 않습니다. 라이선스 미기재 경고와 버전 해석 오류를 구분하여 기록합니다.

## 2. 최소 사례를 하나씩 정적 검사

[케이스 정의](../../tests/fixtures/cases.json)의 8개 파일을 대상 8.2·8.3·8.4로 각각 비교합니다. 기대값은 태그 규칙을 읽은 예측이며 실행 결과가 아닙니다. PHP 소스는 실행하지 않고 반드시 PHPCS의 입력으로 전달합니다.

```powershell
$phpcs = Join-Path $probe 'vendor\squizlabs\php_codesniffer\bin\phpcs'
$ruleset = Join-Path $probe 'ruleset.xml'
Push-Location $probe
try {
    & $php -n $phpcs -i
    $cases = Get-Content -LiteralPath (Join-Path $repo 'tests\fixtures\cases.json') -Raw | ConvertFrom-Json
    foreach ($case in $cases.cases) {
        foreach ($target in @('8.2', '8.3', '8.4')) {
            $inputFile = Join-Path (Join-Path $repo 'tests\fixtures') $case.file
            $stem = [IO.Path]::GetFileNameWithoutExtension($inputFile) + '-' + $target
            $report = Join-Path $results ($stem + '.json')
            $arguments = @('-n', $phpcs, ('--standard=' + $ruleset), '--runtime-set', 'testVersion', $target,
                '--report=json', ('--report-file=' + $report), '-q', '--no-cache', '--extensions=php', $inputFile)
            & $php @arguments 1> (Join-Path $results ($stem + '.stdout.txt')) 2> (Join-Path $results ($stem + '.stderr.txt'))
            $exitCode = $LASTEXITCODE
            $exitCode | Set-Content -LiteralPath (Join-Path $results ($stem + '.exit.txt'))
        }
    }
} finally {
    Pop-Location
}
```

JSON은 PHPCS 자체가 검사 대상 밖의 artifacts/에 씁니다. PowerShell의 stdout/stderr 리다이렉트는 호스트 버전에 따라 인코딩 등을 바꾸므로 초기 수집 절차로만 취급합니다. 생바이트·잘못된 JSON을 엄밀히 검증하려면 별도의 프로세스 수집 절차가 필요합니다. 과거 결과를 잘못 읽지 않도록 각 시험 전 해당 report/stdout/stderr/exit 파일이 남아 있지 않은지 확인합니다. 출력 파일이 없는 시험을 0건으로 처리하지 않습니다.

각 결과는 case의 target 기대값과 비교합니다. finding은 source prefix·line이 일치하는 문제를 찾아 type/severity/column/message·총수·종료값과 추가 탐지도 기록합니다. no_findings는 engine errors/warnings와 messages가 실제 0인지 확인합니다. observe_only는 탐색적 사례이며 0건을 성공이나 엔진 결함으로 단정하지 않습니다. 차이를 숨기기 위해 기대값을 바꾸지 않습니다.

## 3. 최소 사례 다음에 필요한 검증

이 준비물만으로 T01은 완료되지 않습니다. TASKS.md의 전체 완료 조건을 계속 수행해야 합니다.

- 성공/탐지/실패 전후의 대상 파일·디렉터리 목록과 파일별 SHA-256을 비교합니다. 대상 밖의 artifacts/와 도구 영역은 비교 대상 밖에 둡니다.
- 빈 폴더, 읽기 거부, 잘못된 ruleset, 출력 오염을 개별 실측합니다. fixture 전체 검사 전 단일 파일 JSON과 실제 처리 파일 수를 대조합니다.
- phpcs.xml/bootstrap, vendor/autoload.php, Composer scripts, 외부 PHP 설정의 영향이 차단되는지 별도 격리 fixture로 검증합니다. 보호용 fixture는 아직 준비하지 않았습니다.
- 일반 사용자 읽기 거부, 한글/공백/메타문자 경로, 하위 폴더, 확장자, 제외, junction/symlink를 비교합니다. 최종 제품 탐색 정책과 구분합니다.
- 준비 후 네트워크를 실제 차단하고 Composer를 호출하지 않은 채 PHPCS만으로 같은 시험을 실행합니다. CRT 미설치 환경도 구분합니다.

Windows 실험 결과, 명령 확정, lock, 원본 해시, 실제 JSON, 보호·탐색·오프라인 시험이 확보되기 전 T01을 완료로 처리하지 않습니다.

## GitHub Actions Windows 검증

2026-10-29 사용자가 Windows 검증용 workflow 추가·실행을 승인했습니다. `.github/workflows/t01-windows-probe.yml`은 이 PR의 검사 준비물 변경 시 Windows Server 2022에서 실행합니다. checkout, setup-php, upload-artifact는 조회한 커밋 SHA로 고정했으며 제품 의존성이 아닙니다. setup-php의 PHP 8.4 계열은 실험 실행용이며 실제 패치 버전·실행 파일 SHA-256을 결과에 기록합니다.

`run_probe.py`는 표준 라이브러리만 사용하는 검증용 수집기입니다. 24개 버전별 사례, 입력 전후 파일·디렉터리/해시, 빈 폴더·잘못된 ruleset 관찰, 한글·공백·메타문자 경로와 대상 설정·소스 비실행 감지를 수집합니다. PHP 소스를 직접 실행하지 않습니다. 원본 fixture를 대상 밖 artifacts/t01/inputs에 복사해 검사합니다. raw stdout/stderr·실행 인자·종료값·summary.json·composer.lock은 7일간 workflow artifact로 보존합니다. 실제 결과 확인 전 통과로 기록하지 않습니다.

이 workflow의 성공은 T01 전체 완료와 다릅니다. 실제 네트워크 차단, 읽기 거부, 링크 탐색, 깨끗한 PC의 CRT 의존성 및 PHP ZIP 원본 출처/해시는 별도 검증 항목입니다. 초기 JSON 기대값과 차이가 발생하면 실제 원인을 검토하며 결과에 맞춰 조용히 기준을 낮추지 않습니다.
