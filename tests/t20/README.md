# T20 — GitHub 엔진 채널 및 로컬 배포 후보

T16~T19에서 작성한 엔진 팩 관리 기능의 **배포 채널 준비** 단계입니다. 현재 상태는 배포 후보이며, GitHub Release/tag를 만들거나 기존 v0.1.0 배포물과 main을 수정하지 않습니다.

## 사전 확인

- 기존 기본 PHP CLI 8.4.27·고정 PHPCS/PHPCompatibility를 동봉한 다음 앱 후보에 PHP 7.4~8.5 검증 팩을 포함합니다.
- 제품에서 허용하는 단일 카탈로그 URL은 `https://raw.githubusercontent.com/danhk0612/DK-PHP-Version-Compatibility-Checker/main/tools/engine-pack/catalog-v2.json`입니다. `EnginePackInstaller`의 URL 출처 제한을 유지합니다.
- 최초 카탈로그는 **`{"schemaVersion":2,"packs":[]}`**입니다. 게시되지 않은 엔진 팩 ZIP 링크/해시를 만들어 넣지 않습니다. 이 상태에서는 수동 조회는 가능하나 새로운 팩은 제공되지 않습니다.
- 추후 실제 추가 엔진 팩을 공개하려면 정확한 `runtime/` 구성, manifest 검증, 대응 소스·고지 ZIP, 태그 고정 Release 자산의 정확한 SHA/크기, 신뢰 카탈로그 항목과 순서를 먼저 준비해야 합니다.

## 작업 브랜치 시험

`.github/workflows/t20-channel-candidate.yml`의 Windows x64 단계에서 카탈로그 v2가 비어 있는 것을 확인하고 `tools/packaging/Prepare-LocalTest.ps1`을 통해 프로그램 후보 ZIP·대응 소스/고지 ZIP·paired-candidate.json을 생성 및 대조합니다. 산출 ZIP 자체는 공개하거나 임의 Actions artifact로 배포하지 않고 검사 결과 메타데이터만 제한적으로 보존합니다.

## 공개 게시 전 엔진 단독 ZIP의 로컬 후보

기존 [T09 쌍 ZIP 검증](../t09/check_pair.py)까지 완료된 준비 폴더에서 다음 명령을 실행하면, `runtime/`이 최상위인 **엔진 전용 ZIP**과 `engine-candidate.json`을 추가로 생성합니다. 기존 앱 ZIP·소스/고지 ZIP을 덮어쓰지 않고, 서로의 SHA-256과 프로그램 ZIP 내부 `tools/runtime/`의 실제 파일 바이트까지 다시 대조합니다.

```powershell
python tools/engine-pack/prepare_unpublished_candidate.py --runtime tools/runtime --sidecar artifacts/t08-source-sidecar --package artifacts/t09 --output artifacts/t20/engine-candidate
if ($LASTEXITCODE -ne 0) { throw 'T20 엔진 단독 후보 생성 실패' }
Get-Content -Raw artifacts/t20/engine-candidate/engine-candidate.json
```

여기서 만든 `DK-PHP-engine-unpublished.zip`은 사용자가 직접 설치하는 앱 ZIP이 **아니며**, GitHub Actions나 Release에 게시하지 않습니다. 제품 설치기의 `runtime/` ZIP 구성과 manifest SHA를 준비 단계에서 검증하기 위한 후보입니다. 대응 소스는 앞서 생성한 쌍 ZIP을 그대로 참조합니다. `engine-candidate.json`에는 임의의 GitHub Release URL이나 `releaseSequence`를 넣지 않습니다. 이 값들은 정식 게시 승인과 고정 자산 검증을 거친 뒤 확정해야 합니다. CI에도 이 로컬 검증을 추가하지만 ZIP 자체는 artifact로 업로드하지 않고 검증 메타데이터만 보존합니다.

T20 Windows Server CI에는 실제 **비공개 앱 ZIP을 한글·공백 경로에 압축 해제한 실행 파일**을 두 번의 별도 OS 프로세스로 시작하고, 응답하는 WPF 메인 창의 표시 및 정상 종료를 확인한 후 ZIP과 압축 해제된 전체 파일을 다시 대조하는 검증도 포함합니다. 절차는 [Verify-PackagedRestart.ps1](Verify-PackagedRestart.ps1)에 있습니다. 이는 실제 패키지 EXE의 기본 시작·종료·재시작에 한정되며, **온라인 엔진 팩 설치 후 WPF 앱을 재시작하여 갱신 엔진을 활성화하는 실사용 경로, 깨끗한 Windows 11, DPI별 사용자 조작은 검증하지 않습니다.** [Windows Server 2022 x64 T20 CI #43997446311](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/43997446311)에서 두 번의 실제 패키지 EXE 실행·창 응답·정상 종료, EXE SHA-256 불변 및 ZIP 전체 파일 대조가 통과했습니다. 동일 실행의 앱 EXE SHA-256은 `a4eb416db3d2068b0dc04a4c1f99955bff5544c5f7eb9cbb55e68cabcf866432`입니다. 이는 공개 배포 자산의 고정 해시나 엔진 업데이트 후 앱 재시작 성공을 뜻하지 않습니다.

T20 설치 통합시험은 설치·기본 팩 복원 후 **각각 새로운 .NET OS 프로세스에서 WPF `MainWindow`를 표시**하고, 실제 UI가 참조하는 활성 팩의 루트와 PHP 목표 및 엔진 표시를 확인합니다. 시험 코드가 관리형 팩 경로를 명시적으로 주입해 실행하며, 제품의 기본 저장 경로나 별도 배포 후보 EXE를 수정하지 않습니다. 이 확인은 관리형 팩 설치 후 WPF 로드 및 복원 후 내장 팩 선택의 검증이지만, **공개 GitHub 다운로드·실제 패키지 EXE에서 관리형 업데이트 활성화·Windows 11 사용자 시험의 검증은 아닙니다.** [Windows Server 2022 x64 T20 CI #44001059120](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/44001059120)에서 새 OS 프로세스의 WPF 관리형 팩 로드 및 내장 팩 복원(`freshProcessWpfManagedPackVerified`, `freshProcessWpfBundledRestoreVerified`)이 통과했습니다. 실제 앱 ZIP의 EXE가 관리형 팩 설치 후 재실행되는 배포 경로는 별도 미검증입니다.

로컬 엔진 ZIP의 실제 설치 가능 여부도 같은 Windows CI에서 격리된 `EnginePackInstaller`로 확인합니다. 허구의 공개 URL이나 네트워크 접속을 쓰지 않고, 검증된 ZIP을 실제 설치 서비스에 전달한 뒤 새로운 로더가 해당 팩을 다시 읽는지 확인합니다. 통과 시 `artifacts/t20/local-install/summary.json`에 결과를 기록하며, **공개 GitHub 채널 시험을 대신하지는 않습니다.**

## Windows 11 검사용 ZIP을 직접 준비하는 절차

CI에서는 배포 승인 전 실행 ZIP 자체를 GitHub Actions artifact로 배포하지 않고 증거 메타데이터만 보존합니다. 따라서 사용자 PC에서 T20 **작업 브랜치**를 별도 폴더에 받아 후보 ZIP과 대응 소스·고지 ZIP을 생성합니다. 아래는 **준비용 PC**의 Windows x64 PowerShell 7에서 수행하며, 준비 도구를 검사 대상 PHP 프로젝트에 설치하지 않습니다.

사전 도구: Git, Python 3.12 이상, .NET SDK 정확히 10.0.403, 네트워크. 패키지 준비의 방화벽 검증을 위해 **준비 단계에서만 관리자 권한**이 필요하며, 결과 앱의 실행/검사에서는 관리자 권한을 요구하지 않습니다.

```powershell
git clone --branch t20-engine-channel-candidate --single-branch https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker.git DK-PHP-T20-Test
Set-Location ./DK-PHP-T20-Test
$testedCommit = (git rev-parse HEAD).Trim()
if ((git status --porcelain)) { throw '새로운 빈 checkout에서만 준비하세요.' }
pwsh -NoProfile -File ./tools/packaging/Prepare-LocalTest.ps1
if ($LASTEXITCODE -ne 0) { throw 'T20 로컬 후보 생성 실패' }
$pair = Get-Content -LiteralPath ./artifacts/t09/evidence/paired-candidate.json -Raw | ConvertFrom-Json
if (-not $pair.scopePassed -or -not $pair.candidateForUserTesting -or $pair.publicReleaseApproved) {
    throw '사용자 시험 가능한 비공개 후보가 아닙니다.'
}
$binary = Join-Path (Resolve-Path ./artifacts/t09).Path $pair.binaryArchive
$source = Join-Path (Resolve-Path ./artifacts/t08-source-sidecar).Path $pair.sourceArchive
if ((Get-FileHash -LiteralPath $binary -Algorithm SHA256).Hash -ne $pair.binaryArchiveSha256) {
    throw '실행 ZIP 해시 불일치'
}
if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $pair.sourceArchiveSha256) {
    throw '대응 소스·고지 ZIP 해시 불일치'
}
Write-Output "시험 커밋: $testedCommit"
Write-Output "실행 ZIP: $binary"
Write-Output "대응 소스·고지 ZIP: $source"
Write-Output "검증 기록: $((Resolve-Path ./artifacts/t09/evidence/paired-candidate.json).Path)"
```

**주의:** 후보 생성 시 소스/도구 다운로드가 일어나며, 준비 결과 ZIP은 `artifacts/`에만 생성됩니다. 공개 업로드나 Release 게시를 하지 않습니다. 실행 ZIP, 대응 소스·고지 ZIP, `paired-candidate.json`, 시험 커밋 SHA를 한 세트로 보관합니다. 생성 단계에서 일부 파일을 만들었거나 실패했다면 기존 내용을 덮어쓰지 말고 새로운 checkout에서 재시도합니다.

준비가 끝나면 **검사 대상 PHP 원본과 별개의 Windows 11 PC/VM**에서 일반 사용자로 ZIP을 풀어 아래 체크리스트를 수행합니다. 개발 도구/관리자 권한은 앱 사용에 필요한 전제가 아닙니다. 대응 소스 ZIP은 실행 시 함께 압축 해제할 필요가 없지만 같은 빌드의 자료로 보존해야 합니다.

## Windows 11 최종 사용자 시험 인계 (실행 전 체크리스트)

기존 [T09 Windows 11 사용자 확인 절차](../t09/WINDOWS11_TEST.md)의 해시 대조·일반 사용자 권한·오프라인·읽기 전용 검사·고지 항목을 재사용합니다. **이 단계를 CI 서버 시험으로 완료 처리하지 않습니다.** 릴리스된 `v0.1.0`이 아니라 *T20 후보 커밋과 쌍을 이룬 ZIP*을 사용해야 합니다.

- 깨끗한 Windows 11 x64에서 후보 실행 ZIP과 대응 소스 ZIP, `paired-candidate.json`의 SHA-256·빌드 기준 커밋을 대조한 후 한글/공백 경로에 압축 해제합니다.
- 관리자 권한이나 PHP/Composer 설치 없이 실행되는지, 필요한 .NET Desktop Runtime 10 x64/VC++ 런타임의 실제 조건이 무엇인지 기록합니다.
- 기본 팩으로 PHP `7.4`/`8.0`/`8.1`/`8.2`/`8.3`/`8.4`/`8.5` 단일 목표를 각각 선택할 수 있는지 확인합니다. 문제가 있는 샘플/없는 샘플 및 JSON·오류·경고·파일 수를 확인하고 정적 검사 한계가 UI에 드러나는지 확인합니다.
- 검사 중 원본 파일·빈 디렉터리·내용 SHA-256 불변 및 대상 프로젝트 설정/스크립트 미실행, CSV/HTML 저장 위치가 원본 밖인지 기록합니다.
- 엔진 관리 창을 메인 버튼으로 열고 100%·125%·150% 화면 배율, 최소 `520×440` 창과 기본 `680×610` 창에서 버전·한계·진행률·닫기/복원 버튼·가로 잘림을 확인합니다. 긴 한글 경로와 복사 가능한 오류 문구를 확인합니다.
- 검증 팩 설치/복원 자체는 T18의 내부 테스트와 별개로 실제 사용자 조작 및 앱 종료→재실행을 확인해야 합니다. **현재 원격 카탈로그는 비어 있으므로 공개 팩 다운로드는 시험할 수 없습니다.** 임의 검증되지 않은 ZIP을 배포 자산으로 가장하지 않습니다.
- 앱을 오프라인에서 다시 실행해 기본 검사·기존 결과 표시·CSV/HTML 내보내기가 정상인지 확인합니다. 네트워크 실패로 검사 동작이 막히면 실패로 기록합니다.

기록해야 할 값: 사용한 후보 SHA와 두 ZIP SHA, Windows 빌드·화면 배율, 실행 계정·전제 런타임, 테스트 대상 자체 샘플과 목표 버전, 기대/실제 결과, 각 단계의 통과/실패/미실행 및 화면 캡처. 실제 고객 PHP 소스·환경 비밀을 업로드하지 않습니다.

## 공개 배포 전 출시 범위·순서 확인 (미승인)

현재 `main`은 기존 `v0.1.0`이고 T16~T20은 Draft PR #15~#19에 있습니다. 이번 변경의 버전 번호·태그·게시 시점은 아직 확정하지 않습니다.

**출시 범위 판단:**
- **기본 검사 앱 우선 출시:** PHP 7.4~8.5가 내장된 새 앱 ZIP을 준비하되 `catalog-v2.json`은 `packs=[]`로 유지합니다. 기본 오프라인 검사와 수동 업데이트 화면의 기반은 제공하지만, **현재 다운로드 가능한 새 엔진 업데이트는 없습니다.** 온라인 채널 실서버 검증은 계속 미완료로 기록합니다.
- **온라인 업데이트까지 함께 출시:** 기본 내장 팩과 다른 `packId`의 **별도로 검증된 실제 엔진 팩** 및 그 대응 소스·고지가 먼저 필요합니다. 현재 `prepare_unpublished_candidate.py`의 엔진 ZIP은 내장 `tools/runtime/`과 바이트 일치하는 같은 팩입니다. 설치기 `FindUpdateAsync`는 같은 `packId`를 업데이트 후보에서 제외하므로 이 ZIP을 공개하기만 해서는 엔진 갱신 흐름을 실증할 수 없습니다. 숫자·해시만 바꾼 가짜 신규 팩은 만들지 않습니다.

최신 [T08 대응 소스·고지·재링크 Windows CI #44001059330](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/44001059330)에서 6개 PHP/네이티브 원본 아카이브 수집(31,375 tracked 파일), 소스·고지 ZIP, 후보 페어링, 동반 소스 기반 PHP/libiconv 재링크와 **54개 비교 사례**가 통과했습니다. 동일 실행의 `PHP_CORE_SOURCE_JSON.redistributionCleared=false` 및 후보 패키지의 `releaseGatePassed=false`는 유지합니다. 즉, **기술적 대응 소스 준비 검증 성공과 최종 법적/배포 승인 판정은 별개**입니다.

**어느 출시 범위든 먼저 필요한 관문:**
1. T08: 실제 배포 바이너리와 동봉 라이선스·고지·대응 소스·빌드/재링크 자료의 충분성을 최종 대조합니다. `tools/license-audit/license-election.json`의 `redistributionCleared=false`는 전체 적합성 완료 판정이 아님을 나타냅니다. 최종 판정 없이 공개하지 않습니다.
2. T09/T20: 검증할 정확한 커밋의 **깨끗한 Windows 11 x64 일반 사용자 환경**에서 .NET Desktop Runtime/VC++ 전제, 한글·공백 및 읽기 전용 경로, 기본 7개 목표·오프라인 검사·원본 불변, 관리 GUI의 100%/125%/150% DPI를 기록합니다. Windows Server CI는 이를 대신하지 않습니다.
3. 보안·버전: 내장 PHP CLI 8.4.27-1과 고정 엔진/lock의 실제 파일·해시 및 공지된 보안 상태를 다시 확인합니다. 확인 없이 의존성만 교체하지 않습니다.
4. 사용자에게 최종 **릴리스 버전/태그, 출시 범위, 병합 및 공개 게시**를 명시적으로 승인받습니다.

**게시 승인 뒤에만 적용할 순서:**
1. 승인된 PR과 `main`의 포함 범위를 확인한 후 최종 릴리스 커밋을 고정합니다. 새 Windows 후보를 이 **동일 커밋**에서 만들고 실행 ZIP·대응 소스/고지 ZIP·`paired-candidate.json`의 SHA-256을 다시 대조합니다. 과거 CI 실행의 다른 ZIP 해시를 재사용하지 않습니다.
2. 정확한 승인 태그와 함께 사용자용 앱 ZIP·대응 소스/고지 ZIP·페어링 JSON을 동일 GitHub Release에 함께 게시하고 지속 제공을 확인합니다. 실제 게시 파일명을 고지/안내와 일치시킵니다.
3. 온라인 엔진 팩까지 제공하는 **별도 승인 범위**라면 태그 고정 엔진 ZIP·대응 소스 ZIP의 실물과 해시를 먼저 확정하고, 승인된 `releaseSequence`와 불변 URL만 `main` 카탈로그 v2에 기재합니다. 내장 팩과 동일한 팩을 갱신으로 홍보하지 않습니다.
4. 익명 사용자로 공개 다운로드·해시·설치·WPF 앱 종료→재실행·복원 및 관리형 팩만 갱신한 뒤 **앱 EXE SHA-256 불변**을 확인합니다. 검증되지 않은 실서버 결과는 통과로 기록하지 않습니다.

현재는 **릴리스 전 준비 상태**이며 `main` 병합·tag/Release 게시·외부 자산 공개를 수행하지 않습니다.

## T20 완료 전 필수 관문

1. T17 신규 목표 PHP 7.4/8.0/8.1 및 8.2~8.5 실제 Windows 회귀 통과. T18 같은 목표 엔진 갱신·이전/기본 팩 복원, T19 별도 WPF 관리 창 테스트 통과.
2. 패키징 후보와 대응 소스·고지의 SHA 및 T08/T09 고지·재링크·배포 자료 요구를 재대조. 공개 배포 적합성은 별도 검토.
3. **깨끗한 Windows 11 x64** 사용자 권한에서 .NET Desktop Runtime 및 필요한 CRT 의존 조건, 실제 GUI 시작·검사·원본 불변, 오프라인 동작 검증.
4. 새 엔진 팩 ZIP 및 대응 소스·고지가 고정 태그의 GitHub Release에 **별도 게시 승인**에 따라 준비된 뒤 실제 익명 HTTPS 다운로드/리디렉션/해시·설치·재시작·복원을 검증.
5. 최초 새 앱 릴리스에서는 온라인 채널 활성화가 필요합니다. 이후 동일 EXE 해시를 유지한 팩 단독 업데이트를 실제 검증합니다.

**현재 불가:** main에 카탈로그가 아직 없고 새 엔진 팩 Release 자산도 승인·게시되지 않았으므로, 원격 실서버 성공 또는 T20 완료를 선언할 수 없습니다. 빈 카탈로그는 배포 대상 가짜 팩을 만들지 않기 위한 임시 상태입니다.

게시·병합·tag/Release 생성은 이 PR에서 실행하지 않습니다.
