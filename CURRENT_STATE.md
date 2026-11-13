# 현재 상태

## 작업 기준

- 확인일: 2026-11-14 (한국 시간).
- 저장소: https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker
- 기본 브랜치: `main`. 최신 디자인 후보는 `t15-window-design`, Draft [PR #14](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/pull/14)입니다. 시험 후보 준비는 기반 PR #13에 있습니다.
- 디자인 및 시험 후보 자동 검증 기준: `e8008e47bde4f8ddf198204626e4eb05e3a1cc9f`.
- 첫 릴리스 목표는 정식 `v0.1.0`과 기본 오프라인 엔진 팩입니다. 공개 게시 상태는 대기입니다.

## 구현된 기능과 유효한 결정

T00~T07과 T11 제외 기능, T12 결과 내보내기를 구현하고 검증했습니다. 폴더 선택 → 목표 PHP 버전 선택 → 검사 → 파일·줄·원문 문제와 집계 확인의 WPF 흐름을 구현했습니다. 검사 대상은 읽기 전용이며 대상에 Composer 설치·캐시 생성·소스 실행/수정을 하지 않습니다. 정적 분석 결과는 실제 PHP 실행·DB·외부 라이브러리·서비스 정상 동작을 보장하지 않습니다.

- Windows 11 x64, .NET 10 WPF, SDK 10.0.403, 프레임워크 종속 포터블 ZIP을 기준으로 합니다. 사용자 PC에 .NET Desktop Runtime 10 x64를 별도로 설치합니다. 깨끗한 Windows 11의 CRT·배포 전제는 아직 확인 대기입니다.
- 공식 PHP 8.4.27-1 NTS VS17 x64, PHPCompatibility 10.0.0-alpha2, PHP_CodeSniffer 4.0.4, PHPCSUtils 1.2.3을 고정했습니다. 검증 팩 목표는 8.2/8.3/8.4/8.5입니다. 사전 공개 엔진과 동적 속성 미탐지 한계를 표시합니다.
- 기본 MinimalCli PHP 구성은 원본 ZIP의 7파일입니다. Full 79파일 대비 PHP 부분의 바이트 수는 86.42% 감소했습니다. 앱 전체 ZIP 감소율이 아닙니다.
- 활성 엔진 팩의 manifest가 목표 목록·구성요소·해시·검증 근거를 제공합니다. 새 PHP 목표는 실제 엔진 규칙과 회귀·보호·오프라인 검증 후 팩으로 추가합니다. 새 버전 발표만으로 지원을 선언하지 않습니다.
- 검사 입력은 링크를 제외한 명시적 파일 목록입니다. 최대 100파일씩 새 PHP 프로세스에서 순차 검사하고 각 프로세스에 512MB를 설정합니다. exit255와 실제 메모리 소진 진단이 확인된 묶음만 절반으로 나누어 재시도합니다.
- 실패한 상위 묶음 결과는 합산하지 않습니다. 단일 파일 실패는 경로를 표시하고 정상 형제/후속 묶음 결과를 보존하며 전체 상태는 실패입니다. 처리 수 미확인이나 중단을 완료 수로 바꾸지 않습니다. 여러 성공 프로세스에 하나의 OS 종료 코드를 만들지 않습니다.
- 실패 영역에서 이유·엔진 종료 코드·stderr 및 JSON 해석 실패 시 stdout 앞/뒤 일부를 읽고 Ctrl+A/C로 복사할 수 있습니다.
- 자체 코드는 MIT입니다. 사용자 승인된 PHP v4/Zend v3 후속 조건 선택과 독립 LGPL/Microsoft 등 외부 조건은 구분합니다. 전체 배포 적합성은 아직 완료가 아닙니다.

세부 계약은 [REQUIREMENTS](REQUIREMENTS.md), [ARCHITECTURE](ARCHITECTURE.md), [제품 코드 안내](src/README.md)를 따릅니다. PHPStan·자동 수정은 보류입니다. T14 다운로드·설치 구현은 Windows 검증용 카탈로그에서 시험했습니다. 공개 다운로드 채널은 준비 전입니다.

## 현재 검증 근거

| 영역 | 확인된 결과 | 근거와 한계 |
| --- | --- | --- |
| 엔진·원본 보호 | 목표별 fixture, 설정 격리, 링크 제외, 입력 목록/해시 불변, 네트워크 차단 검사 | [T01](tools/ENGINE_PROBE.md), [T04](tests/t04/evidence/). 동적 속성 미탐지 등 엔진 한계를 포함한 검증입니다. |
| 최신 서비스·GUI·내보내기 회귀 | T05 77건 / T06 27건 / T07 39건 / T12 19건 통과. 빌드 경고·오류 0 | [Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/43338975882), [T12 근거](tests/t12/evidence/summary.json). Windows Server 2022 CI입니다. |
| 분할 검사 | 실제 PHPCS의 자체 생성 28,817파일, 205입력 100/100/5 새 프로세스, 실제 메모리 소진 재분할, 복수 단일 실패·결과 보존·중단 정리 통과 | 같은 batched-scans 근거. 생성 입력의 통과를 사용자 프로젝트 시험으로 대체하지 않습니다. |
| 다른 Windows 11 PC 실제 검사 | 목표 8.5, 선택·처리 236개, 오류 1건·경고 15건, 폴더 3개 제외, CSV·HTML 내용 일치와 Excel 한글·열 구분 확인 | 2026-11-13 사용자 제공 화면·로그·보고서를 대조했습니다. 개발 도구 및 VC++ 런타임이 있는 환경입니다. |
| 사용자 실제 프로젝트 | 목표 8.4·하위 포함, 선택/처리 28,817, ERROR 550 / WARNING 265, 완료 표시 | 2026-11-09 사용자 제공 화면. 실행 바이너리/ZIP의 정확한 SHA는 미수집입니다. 815건은 탐지 문제 수입니다. |
| 로컬 후보 준비 | PHP/Composer 사전 설치 없이 고정 해시 임시 도구 사용, 중첩 긴 경로 수집·환경 복원·정리 통과 | [Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/42147647503), [근거](tests/t09/evidence/bootstrap-preparation.json). |
| T08 기준 후보 준비·페어링 | 필수 재구성 파일 6개 포함·바이트 대조와 전체 준비 통과. scopePassed=true, candidateForUserTesting=true, publicReleaseApproved=false | [Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/42568208419), [기준 커밋·두 ZIP SHA](tests/t08-minimal/evidence/php-relink/build-materials.json). 누락/변조 거부 13건의 상세 근거는 [reconstruction-inputs](tests/t09/evidence/reconstruction-inputs.json)도 보존합니다. 외부 공개 ZIP은 게시하지 않았습니다. 후속 후보의 페어링은 아래 T15 근거를 따릅니다. |
| T15 화면 디자인·시험 후보 | Windows 회귀 210건, 시작·정상 종료, 1,250파일 불변, 고지 49개 대조, 바이너리·소스 ZIP 페어링 | [Windows 회귀](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/43427066923), [패키징](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/43427066932), [두 ZIP SHA](tests/t15/evidence/summary.json). CI artifact는 메타데이터만 보관합니다. |
| T09 이전 시험 후보 ZIP | 최신 self-contained ZIP의 시작·정상 종료·1,250파일 불변, 대응 소스 ZIP·공식 개발 헤더 2개·고지 49파일 대조 및 페어링 시험 19건 통과 | [Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/43393286157), [두 ZIP SHA와 범위](tests/t09/evidence/latest-paired-candidate.json). Windows Server 2022이며 바이너리는 로컬 시험용으로 생성했습니다. |
| T10 팩 교체 | 같은 앱 assembly에서 reduced→full→reduced, 새 OS 프로세스의 실제 목록·검사·복원 및 잘못된 팩 거부 12건 통과 | [Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/42055646710), [근거](tests/t10/evidence/). 기존 목표의 부분집합을 이용한 개발 시험이며 미래 PHP 버전 지원 검증은 아닙니다. |

batched-scans.json의 “사용자 프로젝트 재시험 전” 문구는 해당 자동 실행 당시의 한계 기록입니다. 현재 사용자 시험 상태는 위 화면 보고가 우선하며 원본 실행 근거를 소급 수정하지 않습니다. 같은 실제 프로젝트의 수동 오프라인 재검사는 사용자 요청으로 **미실행·생략**했습니다. 통과로 간주하지 않고 재요청하지 않습니다. 기존 자동 오프라인 근거와 오프라인 동작 요구사항은 유지합니다.

## 사용자 지정 제외 기능 (T11)

GUI의 ‘검사 제외 폴더·파일’에서 하위 폴더와 여러 파일을 선택하고 목록에서 제거할 수 있습니다. 지정한 폴더는 메타데이터 조회/열기/하위 탐색 전에 제외하며 정확한 파일도 엔진에 전달하지 않습니다. 이름 접두사가 같은 형제 폴더·파일은 유지합니다. 루트 자체/외부/패턴은 거부하고 기존 링크 보호를 유지합니다.

지정 목록은 현재 실행 중 유지하고 검사 폴더 변경 시 초기화합니다. 제외 변경 시 오래된 결과를 초기화하고 검사 중에는 입력을 잠급니다. 요청 목록은 비동기 작업 전에 복사하며 실제 건너뛴 경로는 기존 제외 결과에 표시합니다. 영구 저장·glob은 포함하지 않습니다. 실제 네이티브 폴더/다중 파일 선택·취소, 제외 해제 후 진단 복원과 원본 불변을 확인했습니다. [실측·한계](tests/t11/evidence/summary.json), [실제 시험 화면](tests/t11/evidence/configured-exclusions.png)을 보존합니다.

## 결과 내보내기 (T12)

현재 검사 결과의 CSV·HTML 저장과 취소를 Windows 네이티브 대화상자로 확인했습니다. 완료, 대상 없음, 실패 및 일부 결과의 상태·미확인 수·제외·원문·검사 한계를 함께 저장합니다. CSV는 UTF-8 BOM을 사용하고 HTML은 외부 리소스 없이 열 수 있습니다. 입력을 편집하면 이전 결과를 내보낼 수 없습니다. 원본 내부와 해당 폴더를 가리키는 별칭에는 저장을 거부하고, 기존 외부 하드링크를 덮어쓸 때 원본 내용을 보존합니다. Excel 한글·열 구분은 위 사용자 시험에서 확인했습니다.

## PHP 8.5 목표 (T13)

기존 PHP CLI 8.4.27과 고정 검사 엔진으로 PHP 8.5 목표를 검증했습니다. 긴 형식 `(integer)` 캐스트의 WARNING과 `gzfile()` 정수 인자의 ERROR를 8.5에서 확인했습니다. 동일 입력을 8.4 목표로 검사한 결과와 각 수정 대조군은 탐지 0건입니다. 동적 속성 미탐지 한계가 있으며 PHP 8.5 파이프 문법은 관찰 결과만 기록합니다. 세부 관찰값과 오프라인·원본 보호·목표 교체 검증은 [T13 근거](tests/t13/evidence/summary.json)를 확인합니다.

## 검증된 엔진 팩 다운로드 (T14)

현재 목표를 유지하며 새 목표를 추가하는 팩을 목록에서 확인하고 사용자 확인 후 설치합니다. ZIP 크기·해시·경로와 manifest·구성·목표를 대조한 뒤 다음 실행에서 활성화합니다. 이전 팩은 유지하며 시작 시 네트워크 없이 새 팩·이전 팩·내장 팩을 확인합니다. [카탈로그·설치 계약](tools/engine-pack/DOWNLOAD-CATALOG.md)과 [검증 근거](tests/t14/evidence/summary.json)를 확인합니다. 출시 설정의 카탈로그 URL은 비어 있으며 공개 팩 게시와 배포 조건을 마친 후 연결합니다.

## T08 배포 준비 상태

자체 LICENSE·서드파티 고지, 실제 publish 파일 출처, 엔진 LGPL 소스 바이트 대응을 준비했습니다. 최소 CLI의 코어 원문 6개와 Windows 생성 헤더 2개를 구분하고 설정 포함 bundled digest 23/23을 확인했습니다. 고정 builder 원문과 PHP SDK 레시피·고지, 엔진 소스 2개를 소스·고지 동반 ZIP에 포함합니다.

동반 ZIP에는 재링크의 의존성 후보 해시 목록·결과 비교기와 후보 생성에 필요한 검증/안내 파일 6개를 명시적으로 포함합니다. 이전 필터의 누락을 보완했고, 위 최신 후보 검증에서 존재·현재 checkout 바이트 일치와 누락/변조 거부를 확인했습니다. 관련 Windows 검증 7개(최소 CLI/코어·네이티브 입력·libiconv 빌드·PHP 재링크·소스 동반 ZIP·후보 패키징·팩 교체)도 모두 통과했습니다. 실행 ID와 서비스/GUI 회귀 수는 reconstruction-inputs 근거에 있습니다. 전체 일반 CI 시험 코드까지 동반 ZIP에 포함하는 것은 아닙니다.

격리 libiconv 원본/마커 빌드 및 PHP 재링크 후 PHPCS 24사례 동일 결과를 확인했습니다. 이 감사용 축소 빌드는 실제 제품 PHP 바이너리에 필요한 전체 입력 대응을 입증하지 않습니다. [공식 artifact 직접 대조](tests/t08-minimal/evidence/official-artifact-identity.json)에서는 공식 빌드 산출물 안의 NTS x64 ZIP과 승인 PHP ZIP의 SHA 일치, php.exe/php8.dll 및 기존 Windows source ZIP 해시 일치를 확인했습니다. 과거 SDK 커밋·의존성 ZIP 바이트와 전체 빌드/링크 입력의 충분성은 여전히 미검증입니다. 자세한 원문·해시·실험 범위는 [LICENSING](LICENSING.md), [T08 최소 CLI 근거](tests/t08-minimal/evidence/)에 있습니다.

[최신 재링크 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/42568208420)에서는 원본/마커 각각 생성 자료 19개(응답 파일 각 15개)를 ZIP·inventory로 보존하고 실제 빌드 파일과 바이트를 대조했습니다. 24사례와 입력/엔진 불변 및 관련 Windows 검증 7개가 통과했습니다. [자료·도구 버전·해시](tests/t08-minimal/evidence/php-relink/build-materials.json)의 fullProductProfileVerified=false를 유지합니다. 제품의 전체 configure 옵션은 기존 PHP 실행 정보에 이미 있고, 이번 생성 자료는 --disable-all 감사 구성입니다. 제품 전체 구성의 생성 자료·소스 대응 검토와 혼동하지 않습니다.

T08은 부분 검증입니다. 남은 관문은 실제 배포물에 대응하는 소스·변경·필요 빌드/링크 자료의 충분성과 고지·권리 조건 검토, 최종 바이너리와 소스·고지의 정확한 페어링 및 지속 제공입니다. 공식 바이너리와 동일 해시로 재생성하는 것 자체를 별도 필수 조건으로 추가하지 않습니다. 7일 Actions artifact 보관은 지속 소스 제공 완료가 아닙니다.

## .NET 런타임 별도 설치 후보

[Windows 후보 검증](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/43496962532)에서 커밋 `a303fa8`의 프레임워크 종속 ZIP을 생성했습니다. ZIP의 EXE·DLL은 앱 2개와 공식 PHP 2개로 확인했습니다. 설치된 .NET Desktop Runtime으로 앱 창 표시·정상 종료, 파일 855개 불변, 고지 49개 일치와 소스 ZIP 페어링을 통과했습니다. 런타임 설치 전 동작과 깨끗한 Windows 11 환경은 이 실행의 범위 밖입니다.

| 내부 후보 ZIP | SHA-256 |
| --- | --- |
| `DK-PHP-Compatibility-audit-only.zip` | `94a45fd4f0e7be53e16a99d8a213d4af75b7ba75cb0230ec149f0549b86e4c70` |
| `DK-PHP-Compatibility-sources-and-notices.zip` | `a1df719ddb3b2a15f0bedd7b558f661511cef0c5d63e6c6964f437bbced24bbb` |

## 남은 작업과 시작점

| 구분 | 다음 작업 | 상태·권장 환경 |
| --- | --- | --- |
| T14 공개 다운로드 연결 | 검증 팩·대응 소스·고지의 공개 채널 및 실서버 검증 | 제품 배포 조건과 게시 승인 대기. 설치 기능은 Windows 검증용 카탈로그로 시험했습니다. |
| T08 기술·배포 검토 | 실제 제품 PHP에 필요한 소스/빌드/링크 자료와 고지의 잔여 대응을 LICENSING 기준으로 검토 | 부분 검증. 자료 대조는 저성능 Work, 해석 충돌은 고성능 Chat. 제품 PHP 교체·스택 변경은 임의 수행하지 않습니다. |
| T09 직접 시험 | 다른 목표, 실제 원본 목록/해시 불변, 깨끗한 Windows 11의 CRT/.NET 전제·일반 사용자·읽기 전용 위치 등 미실행 항목 기록 | 깨끗한 Windows 11 시험은 후속으로 보류했습니다. [Windows 11 절차](tests/t09/WINDOWS11_TEST.md). |
| T10 최종 배포 대조 | 배포에 사용할 팩 구성을 T08 고지/대응 소스와 재대조 | 개발 자동시험 완료, T08 배포 조건 대기. 저성능 Work. |
| 첫 정식 릴리스 | `v0.1.0`에 기본 오프라인 팩 포함. 배포 자료 검토 후 최종 바이너리·소스 ZIP을 같은 기준으로 생성하고 GitHub Releases에 게시 | 프레임워크 종속 Windows 후보의 실행과 소스·고지 페어링을 통과했습니다. 공개 게시 및 대응 소스의 지속 제공은 대기입니다. 온라인 엔진 팩 채널은 후속 작업입니다. |

T11→T12→T13→T14 기능 보강을 배포 마무리보다 먼저 진행하는 사용자 지시를 반영했습니다. 남은 배포 조건을 확인하지 않고 T08/T09/T10 전체 완료나 공개 배포 가능으로 표시하지 않습니다.

최신 후보의 PHP 실행 파일 2개는 승인된 공식 ZIP의 해시와 일치합니다. 동반 ZIP에 포함된 PHP SDK 아카이브를 고정 커밋에서 재생성해 CI 기록과 SHA-256을 대조했고, 그 안에서 빌드 도구 EXE 38개와 DLL 58개를 확인했습니다. 승인된 새 구성은 SDK 레시피·고지를 보존하고 96개 EXE·DLL을 제외합니다. Linux에서 원문 232개를 바이트 단위로 대조했습니다. 변경 기준 `768e3cc`의 [Windows 후보 검증](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/43472423443)에서 합성 페어링 24건, 실제 ZIP 페어링·고지 49개 일치, 앱 시작·종료 전후 1,250개 파일 불변을 확인했습니다. [Windows 격리 재링크](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/43472423393)에서 SDK를 별도 확보해 원본/마커 빌드와 PHPCS 42사례 결과 일치·입력 불변을 확인했습니다. 후속 [Windows 스냅샷 검증](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/43481996872)은 의존성 ZIP 39개를 고정하고 공식 x64 NTS 스냅샷 옵션(PGO 생략)으로 CLI와 코어 DLL을 재링크했습니다. 제품 PHP와 기본 로드 확장 27개 일치, PHPCS 42사례와 입력 불변, 실제 빌드 자료 75개씩 대조를 통과했습니다. [후보 ZIP 페어링](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/43481997013)도 통과했습니다. 과거 공식 의존성 ZIP 바이트·PGO 입력 및 공개 소스 지속 제공은 미확인입니다.
