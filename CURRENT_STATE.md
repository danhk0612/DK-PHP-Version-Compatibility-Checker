# 현재 상태

## 저장소 기준

- 확인일: 2026-11-05 (한국 시간)
- 저장소: https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker
- 최초 착수 시 공개 저장소, 기본 브랜치 설정 main, branches=[], 기존 파일·커밋 없음. 따라서 착수 기준 커밋 SHA는 없습니다.
- 이 문서가 포함된 최초 준비 커밋은 Git 기록에서 확인합니다. 자기 자신의 커밋 SHA를 문서에 고정하지 않습니다.

## 준비된 내용

T00 최초 구축 범위로 README, 6개 기준 문서, AGENTS 및 최소 폴더 역할 문서를 준비했습니다. 확정 요구사항과 D01~D13 결정 대기를 분리하고 Task별 범위·의존성·완료 조건·작업 환경을 정리했습니다.

T02는 사용자 승인으로 확정했습니다. 초기 목표 8.2/8.3/8.4, 공식 PHP 8.4.27-1, alpha2/PHPCS 4.0.4/Utils 1.2.3, .NET 10 WPF·SDK 10.0.403·Windows 11 x64·self-contained ZIP·자체 MIT·링크 제외 file-list를 구현 기준으로 사용합니다. 외부 라이선스 적합성과 제품 동작은 별도 검증합니다. 명칭은 목표 PHP 버전입니다.

## 검증한 것

- **T01 기술 검증 완료**. [최종 Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/40761510481)에서 기본·추가 검증 조건을 충족했습니다. engine 실패와 미탐지도 결과에 포함한 기술 조사 완료이며 제품 합격 선언이 아닙니다.
- 공식 PHP 8.4.27-1 x64 NTS ZIP의 공개/실측 SHA-256, php.exe와 DLL을 대조했습니다. Composer lock과 alpha2/PHPCS 4.0.4/Utils 1.2.3 조합을 실제 검증했습니다.
- 기본 24개(필수 기대 비교 21개 일치·동적 속성 3개 미탐지), PHP 통신 차단 상태의 동일 24개, 입력 불변·설정 격리·외부 ini 주입 대조·오염 JSON·읽기 거부·재귀/확장자/제외/링크 탐색을 수행했습니다.
- junction 직접 탐색과 읽기 거부는 exit 255. symlink는 선택 밖 파일까지 추적합니다. 링크 제외 명시적 file-list는 외부 파일 없이 131개 정상 검사됐습니다. 빈 폴더·잘못된 ruleset은 exit 16입니다.
- [ENGINE_PROBE.md](tools/ENGINE_PROBE.md)에 완료 조건 1~7 대조·실제 해시·실행 근거·한계를, [실측 JSON](tools/T01_WINDOWS_RESULTS.json)에 실제 결과를 보존했습니다.

- **T03 최소 WPF 골격 완료**. [Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/40796379993)에서 SDK 10.0.403 restore/build(경고·오류 0), 960×640 기본 창 표시·응답·정상 종료를 확인했습니다. 앱·모델·빌드 절차는 [src/README.md](src/README.md), 실행 환경·바이너리 해시는 [실측 요약](tests/t03/windows-summary.json)을 따릅니다. Windows Server 2022 검증이며 Windows 11 배포 합격은 아닙니다.

- **T04 고정 엔진 팩 완료**. [Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/40797806955)에서 공식 PHP ZIP 해시·제품 Composer lock/정확한 설치 버전·standard 등록·audit 성공, 사례 24건·annotation 대조 9건·통신 차단/Composer 없는 동일 검사 24건·대상 파일/디렉터리/해시 불변을 확인했습니다. 잘못된 팩 17건 거부와 정상 팩 복원도 통과했습니다. [팩 계약/준비](tools/engine-pack/README.md), [실측·manifest·865개 파일/6개 license 원본 목록](tests/t04/evidence/)을 보존했습니다. 라이선스 파일명 목록은 T08 적합성 판정을 대체하지 않습니다.

- **T05 검사 서비스 완료**. [Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/40830447517)에서 SDK 10.0.403 restore/build(경고·오류 0)와 회귀 61건을 통과했습니다. 실제 목표별 fixture·실패/부분 JSON·탐색/annotation·권한/링크/SUBST 중첩·원본 불변·합성 도구 대량출력/Dispose 자식 종료·자체 임시폴더 정리를 검증했습니다. [제품 코드 계약](src/README.md), [검증 안내](tests/t05/README.md), [실측/팩 구성/아티팩트 근거](tests/t05/evidence/)를 보존했습니다. GUI 흐름·종료 이벤트 연결은 T06/T07입니다.

- **T06 입력·결과 GUI 완료**. [최종 Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/40966862548)에서 SDK 10.0.403 build 경고·오류 0, 실제 WPF HWND 표시/바인딩/상태·집계/긴 문구·최소 크기·정상 종료 검증 21건이 통과했습니다. 테스트 데이터 PNG임을 명시하고 캡처 여백과 최소 크기 상태 가시성을 시각 검토 후 보완했습니다. [검증 안내](tests/t06/README.md), [실측과 대표 화면](tests/t06/evidence/)을 보존했습니다. T06 당시 분리했던 실제 엔진 연결·모달 폴더 선택 조작은 아래 T07에서 검증했습니다. Windows Server 2022 CI이며 Windows 11 배포 검증을 대체하지 않습니다.

- **T07 GUI 실제 검사 연결·보호 검증 완료**. [Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/41038802788)에서 SDK 10.0.403 build 경고·오류 0, 통합 36건을 통과했습니다. 실제 폴더 선택·취소/목표/버튼/엔진·대표 24 fixture·0건/NoFiles/읽기 실패/합성 부분 결과·한국어와 설정 격리·원본 불변·응답/중복 방어를 확인했습니다. 검사 중 반복 닫기에서 실제 PHP 자식 종료와 자체 작업 정리를 기다렸고 팩 준비 중 닫기도 통과했습니다. 1000개 파일의 2000개 문제를 파일별 원값 및 마지막 grid 행까지 대조했습니다. 약 1.49초, Dispatcher 최대 관측 간격 54.72ms, GUI/PHP working set 표본 최대 약 176.85/29.34 MiB이며 해당 fixture/CI의 관측값입니다. [화면 회귀](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/41038803221) 21건도 통과했습니다. [통합 안내](tests/t07/README.md), [원본 요약·화면·해시](tests/t07/evidence/)를 보존했습니다. Windows 11 배포·완전한 런타임 호환 보장은 아닙니다.

- **T08 부분 검증, B안 승인·최소 CLI 검증**. [Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/41941111848)에서 SDK 10.0.403 self-contained publish 경고·오류 0, 400파일 출처 매핑과 기존 Full 팩 865파일 inventory를 보존했습니다. 엔진 LGPL 2개의 전체 pinned source checkout을 만들고 배포 파일 302개가 원본과 일치함을 확인했습니다. 자체 MIT LICENSE, 엔진/GPL/PHP/Zend/일부 내장 고지·실제 NuGet 고지와 Microsoft 예외 조건을 준비했습니다. PHP 전체 ZIP의 네이티브 코드 고지·대응 소스/재링크 및 최종 소스 동시 제공은 미완료입니다. [LICENSING.md](LICENSING.md)의 승인된 구성·잔여 관문과 [실측](tests/t08/evidence/)을 확인합니다. 수집기 성공을 배포 허가로 처리하지 않습니다.

- **승인된 T08 B안 기능·설정 포함 소스 대조 통과**. [Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/41976238445)에서 Full 79개 PHP 파일/96,050,460바이트 → MinimalCli 7개/13,043,511바이트(86.42% 감소), 원본/Full 불변·목표/엔진/lock 불변·기본 24건·annotation 9건·오프라인 각24건을 확인했습니다. 빌드 경고·오류 0, T05 61/T06 21/T07 36건과 생성 소스 변조 대조 9건이 통과했습니다. 원본 소스 6개/31,375파일 및 별도 설정 헤더2개를 추적합니다. raw21/23의 원인은 Windows configure 생성물이었고, 생성물을 포함하면23/23입니다. 이전에 생성 파일 영향이 아니라고 판단한 내용을 정정했습니다. 공식 Windows source ZIP 25,836파일은 pinned Git과 경로가 같고7개 ident 확장만 다릅니다. 공식 빌드의 정적 libiconv/libxml2 선택·버전·PGO 경로도 확인했습니다. [실측·소스/빌드 근거](tests/t08-minimal/evidence/), [재현 안내](tests/t08-minimal/README.md)를 사용합니다. 공식 배포물의 정확한 재현·최종 고지/소스 지속 제공은 미완료로 T08 부분 검증을 유지합니다.

- **T08 네이티브 입력·libiconv 부분 검증**. [Windows 입력 수집](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/42008865873)에서 공식 의존성5개의 현재 ZIP·SHA를 기록했습니다. 과거 공식 빌드 입력과 바이트 동일성은 미확인입니다. [Windows 빌드](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/42009675167)에서 고정 libiconv 원문 1,148파일을 Git blob과 대조하고 원본 및 버전 표시만 변경한 검증용 사본을 Windows SDK 10.0.26100.0으로 각각 빌드했습니다. 두 정적 라이브러리 해시가 다르고 `_libiconv_version` 링커 심볼이 존재합니다. [실측·해시](tests/t08-minimal/evidence/native-rebuild-provenance.json). 마커는 실제 버전 변경이 아닙니다. 이 단독 빌드만으로는 PHP 재링크를 확인하지 못했으며, 후속 격리 재링크 결과는 아래에 기록합니다. 최종 라이선스/소스 제공은 미검증입니다.

- **T08 격리 PHP 수정 재링크 검증 통과**. [Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/42018682117)에서 고정 PHP 8.4.27 소스와 원본/마커 libiconv를 각각 빌드·링크했습니다. `ICONV_VERSION`은 각각 1.19/1.20으로 확인했으며 1.20은 수정 라이브러리가 사용됐는지 확인하는 테스트 마커입니다. 같은 PHPCS 엔진·목표에서 fixture 24개 결과가 일치했고 검사 대상·fixture·엔진 팩 불변을 확인했습니다. [결과·해시](tests/t08-minimal/evidence/php-relink/provenance.json). 검증용 `--disable-all` 빌드는 공식 배포 PHP 바이너리의 재현이 아니며, 이 실험의 네트워크 차단은 검증하지 않았습니다. 실제 배포와의 정확한 입력 대응, 최종 고지·소스 지속 제공은 미완료입니다.

## 확정한 향후 버전 확장 구조

사용자가 이후 최신 PHP도 지원 가능한 시스템을 요청했습니다. 목표 목록·구성요소 버전·검증 근거를 앱 코드와 분리한 단일 활성 엔진 팩으로 관리합니다. 새 목표는 해당 엔진 규칙과 회귀·안전성·오프라인 검증을 거쳐 팩 데이터로 추가하며 GUI/실행 코드에 초기 세 버전을 고정하지 않습니다. 호환 스키마/profile의 팩은 앱 종료 후 수동 교체합니다. 자동 다운로드/갱신 UI는 아직 보류입니다.

T04의 실제 manifest 생성·팩 정합성 검증기를 구현했습니다. C# 팩 소비·검사 실행·JSON 해석은 T05에 구현했습니다. 입력·결과 GUI와 실제 검사 연결은 T06/T07에 구현·검증했으며 팩 교체·복원·확장 통합 검증은 T10에 남아 있습니다. 새 버전이 발표됐다는 이유만으로 지원 완료를 선언하지 않습니다.

## 미구현·검증 한계

WPF 입력·결과 화면과 공유 표시 모델, C# 검사 서비스·manifest 소비를 구현했습니다. T07에서 검사 버튼과 ScanService·창 종료 정리를 연결하고 Windows Server 2022에서 통합 검증했습니다. 팩 교체 통합도 미완료입니다. 깨끗한 Windows 11 사용자 PC의 CRT·최소 배포 DLL·ZIP 사용성은 T09에 남아 있습니다. 대표 fixture 외 프로젝트의 성능은 보장하지 않습니다. 모든 PHP 변경·링크 순환·권한 조합·실제 런타임 호환성을 보장하지 않습니다. 엔진 사전 공개판과 알려진 동적 속성 미탐지를 명시합니다.

D06 PHPStan, D10 내보내기와 자동 다운로드/갱신 UI는 보류입니다. 자체 MIT LICENSE와 서드파티 고지 초안을 작성했습니다. 외부 native 구성의 라이선스/대응 소스·최종 배포 조건은 T08 부분 검증으로 남습니다.

## 다음 작업과 사용자 확인

2026-11-06 지시에 따라 사용자 확인 이전의 자동화 준비를 분담했습니다. [소스·고지 동반 ZIP 생성](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/42055646759), [팩 교체·새 OS 프로세스·복원 시험](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/42055646710), [self-contained ZIP 생성·한글 경로 창 표시/종료·파일 불변](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/42055646466)이 Windows Server 2022에서 통과했습니다. tests/t10/evidence, tests/t09/evidence와 tests/t08-minimal/evidence/source-sidecar.json에 근거를 보존했습니다. 실행 ZIP은 runner 내부에서만 만들고 외부 업로드하지 않았습니다.

2026-11-06 사용자가 PHP v4/Zend v3 후속 조건 선택을 승인했고 license-election.json과 고지·공식 원문에 반영했습니다. 자체 MIT와 독립 LGPL/Microsoft 조건은 별개입니다. 같은 checkout에서 후보와 source sidecar를 만들고 두 ZIP SHA·고지·선택 원문·엔진 레시피·프로젝트 소스 대응을 검증합니다. 로컬 자가사용 후보 생성은 tools/packaging/Prepare-LocalTest.ps1을 사용합니다. 공개 Release와 지속 배포 승인은 별도입니다. Windows 11 직접 시험은 tests/t09/WINDOWS11_TEST.md를 따릅니다. T08 최종 배포 조건과 깨끗한 Windows 11 검증은 아직 완료가 아니며 T09/T10의 개발자 자동시험을 최종 배포 승인으로 승격하지 않습니다.

현재 작업은 Draft PR #8(t08-minimal-cli), main 미병합입니다. 병합·Release/tag는 별도 명시 지시가 필요합니다.

[승인 후 페어링 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/42058108214)에서 `licenseElectionMade=true`, `scopePassed=true`, `candidateForUserTesting=true`, `publicReleaseApproved=false`를 확인했습니다. 해당 실행·ZIP/고지/소스 해시는 tests/t09/evidence/approved-pair/provenance.json을 따릅니다. 사용자 Windows11 자가사용 시험은 준비 명령과 문서로 진행할 수 있으며 공개배포는 별도입니다.

로컬 준비 명령은 PHP·Composer 사전 설치 없이 공식 PHP ZIP·Composer 2.11.1을 고정 해시로 검증하여 임시 관리 영역에서 사용하고 정리합니다. [전역 PHP·Composer 없는 Windows 전체 준비 검증](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/42147647503)에서 엔진 준비·자체 포함 publish·소스/고지 ZIP·앱 기동·페어링·환경 복원·준비 도구 제거를 통과했습니다. 중첩 checkout에서 이전 소스 경로가 269자이고 전역 core.longpaths=false인 조건도 통과했습니다. 엔진·PHP 코어·빌드 레시피의 소스 checkout은 짧은 시스템 TEMP 경로에서 수행하고 성공/실패 시 정리하며, 최종 ZIP·inventory·고지는 artifacts에 보존합니다. 실행 기준 commit과 최신 두 ZIP SHA는 tests/t09/evidence/bootstrap-preparation.json을 따릅니다. 사용자 Windows 11 시험과 공개 배포의 잔여 조건은 유지합니다.

검사 실패 화면은 기존 이유와 엔진 종료 코드·stderr 앞/뒤 일부·JSON 구문 해석 실패 시 stdout 앞/뒤 일부를 표시합니다. 읽기 전용 오류 영역을 클릭하고 Ctrl+A/C로 선택·복사할 수 있습니다. 사용자 시험의 47파일 검사는 완료됐고, 28,817파일 실패는 제공된 진단에서 PHP 128MB 메모리 소진·exit255로 확인됐습니다. ScanService는 사용자 php.ini를 읽지 않은 채 메모리 한도를 512MB로 명시합니다.

[Windows 회귀](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/42174958344)에서 T05 64건/T06 25건/T07 36건을 통과했습니다. 합성 엔진의 실제 160MiB 할당은 128M 실패/512M 성공을 대조했고, 실제 PHPCS는 자체 생성 PHP 28,817개를 정확한 파일 수·원본 불변으로 완료했습니다. 오류 내용의 실제 클립보드 복사·읽기 전용 동작도 통과했습니다. [후보 페어링](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/42174958368)도 통과했습니다. 최신 수정 기준과 해당 후보 ZIP 해시는 tests/t09/evidence/memory-and-copy.json을 따릅니다. scan-failure-diagnostics는 이전 진단 표시 보완, bootstrap-preparation은 긴 경로 준비 검증 근거입니다.

다음은 tests/t09/WINDOWS11_TEST.md의 로컬 진단 재빌드로 사용자 전체 프로젝트를 재검사하는 것입니다. 자체 생성 파일 통과가 사용자 프로젝트의 512MB 내 완료를 보장하지 않으며 해당 재시험과 나머지 Windows 11 직접 시험은 남습니다.
