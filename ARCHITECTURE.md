# 아키텍처 기준안

## 상태와 결정 원칙

2026-10-29 사용자 승인과 향후 목표 버전 확장 지시를 반영한 **구현 기준 계약**입니다. T03의 단일 WPF 골격·공유 모델과 T05의 팩 로더·검사 서비스·JSON 해석을 구현했습니다. T06 입력·결과 화면도 구현했으며 T07에서 실제 검사 GUI·종료 수명을 연결했으며 검증 상태는 CURRENT_STATE.md를 따릅니다. T01의 Windows 기본 엔진 실행은 검증했으며 범위·한계는 [보고서](tools/ENGINE_PROBE.md)를 따릅니다. 확정 요구·보류 항목은 REQUIREMENTS.md가 관리합니다. D06과 D10, D07의 자동 다운로드/갱신 UI는 보류입니다. 개발 중인 브랜치의 문서를 배포 태그의 계약으로 대체하지 않습니다.

## 기술 검토 결과

| 구성 | 확인 사실 | 제안과 남은 검증 |
| --- | --- | --- |
| GUI | WPF는 Windows 전용 .NET UI [S7] | C#/WPF/.NET 10, SDK 10.0.403, Windows 11 x64로 확정 |
| .NET 배포 | self-contained는 .NET 런타임 포함; framework-dependent는 런타임 필요 [S8] | self-contained x64 ZIP 확정. 런타임 포함 크기·패치 유지 부담을 수용 |
| PHPCompatibility | 공개 릴리스 최신은 10.0.0-alpha2, 안정판은 9.3.5 [S1] | 초기 alpha2 채택 확정. 사전 공개판·탐지 누락 한계 표시 |
| 고정판 의존성 | alpha2는 PHP >=5.4, PHPCS ^3.13.3 또는 ^4.0, PHPCSUtils ^1.1.2 요구 [S2] | 실행 PHP 최저 요구와 검사 대상 PHP 버전을 혼동하지 않음 |
| PHPCS | 4.0.4 composer는 PHP >=7.2.0 및 libxml/simplexml/tokenizer/xmlwriter 요구 [S3] | alpha2 + 4.0.4 + Utils 1.2.3을 초기 조합으로 확정. 기존 1.1.2는 보안 공지로 제외. Windows 기본·추가 기술 검증 완료. 탐지·탐색 한계와 제품 결정은 보고서/T02 참조 |
| 설치 | alpha2는 Composer 설치를 공식 지원 경로로 안내 [S2] | 개발·패키징 단계의 도구 전용 Composer 환경에서 의존성을 구성; 사용자 프로젝트에 설치하지 않음 |
| PHP CLI | 공식 Windows 다운로드 목록 접근 확인 [S9]; CRT 필요 [S10] | 공식 8.4.27-1 x64 NTS를 초기 팩 기준으로 확정. 해시·출처는 T01 보고서, 실제 번들 최소 구성은 T04/T09에서 검증 |

주의: 확인 시점의 develop 브랜치는 alpha2와 요구 버전이 다릅니다. develop은 PHP >=7.2 및 PHPCS ^4.0.2를 요구하지만 alpha2에 그 요구를 소급하지 않습니다. 움직이는 dev-develop을 제품 의존성으로 고정하지 않습니다.

PHPCompatibility는 탐지 범위가 완전하지 않음을 명시합니다 [S2]. `testVersion`에 8.4를 전달할 수 있다는 사실만으로 PHP 8.4 완전 지원을 선언할 수 없습니다. 버전별 대표 변경의 양성·음성 fixture와 누락 목록이 필요합니다.

2026-10-29 재확인: PHPCSUtils 1.1.2는 [보안 공지](https://github.com/PHPCSStandards/PHPCSUtils/security/advisories/GHSA-r6hr-vr92-vv28)의 영향 범위이며 수정판 1.2.3을 실험 후보로 사용합니다. [1.2.3 manifest](https://github.com/PHPCSStandards/PHPCSUtils/blob/1.2.3/composer.json)는 PHPCS ^3.13.5 또는 ^4.0.1을 요구합니다. 아래 1.1.2 라이선스 확인은 과거 검토 근거이며 현재 실행 추천이 아닙니다. 실측 상태와 준비물은 [ENGINE_PROBE.md](tools/ENGINE_PROBE.md)를 따릅니다.

## 최소 논리 구조

아래는 역할 구분이며 아직 별도 프로젝트·프레임워크·DI 계층을 만들라는 뜻이 아닙니다. T02에서 단일 WPF 앱 src/Dk.PhpCompatibilityChecker와 Models/Services 최소 구분을 확정했습니다.

| 역할 | 책임 | 경계 |
| --- | --- | --- |
| WPF 화면 | 폴더·대상 버전 입력, 실행 상태·요약·목록 | 검사 규칙 재구현 없음 |
| 검사 실행 | 승인된 입력을 고정된 PHP/PHPCS 인자로 전달, stdout/stderr·종료 수집 | shell·대상 소스·대상 설정 실행 없음 |
| JSON 해석 | 엔진 JSON을 결과 데이터로 변환 | 엔진 오류를 성공 결과로 변환하지 않음 |
| 도구 영역 | 버전 고정 PHP·엔진·설정·고지 | 대상 프로젝트와 분리; 사용자 전역 PHP/Composer 변경 없음 |

초기 소스는 src/, 검증은 tests/, 엔진 구성·패키징 자료는 tools/로 구분합니다. 현재 src/에는 단일 WPF 앱의 App/MainWindow 입력·결과 화면·Models/ 공유/표시 계약·Services/ 팩 검증/검사 실행/JSON 해석이 있으며 tools/probe/와 tests/fixtures/에는 T01 검증 입력·수집기·실제 lock/결과가 있습니다. 플러그인 시스템, 다중 엔진 공통 프레임워크, 저장소 DB는 도입하지 않습니다.

## 검사 흐름 및 보호 경계

1. 승인된 지원 목록과 폴더를 확인하고 확정된 파일 탐색 정책을 적용합니다.
2. 대상 밖의 프로그램 작업 영역에 설정·필요 임시 파일을 준비합니다.
3. 프로그램이 관리하는 php.exe와 PHPCS를 절대 경로로 실행합니다. ProcessStartInfo.ArgumentList 등 인자 분리 방식을 사용하며 cmd.exe/PowerShell 명령 문자열을 조합하지 않는 안을 우선합니다.
4. 작업 디렉터리는 대상 프로젝트가 아닌 도구 관리 영역입니다. 명시적 내장 ruleset과 PHP 설정을 사용합니다. 대상의 phpcs.xml, bootstrap, vendor/autoload.php, composer scripts, PHP_INI_SCAN_DIR 및 외부 전역 설정에 검사 동작이 바뀌지 않도록 T01에서 실제 차단 방법을 검증합니다.
5. PHPCompatibility만 실행하고 JSON과 stderr·종료 코드를 수집합니다. phpcbf·소스 include·lint 실행·대상 웹서비스 기동은 사용하지 않습니다.
6. 결과를 해석하여 완료/실패와 탐지 결과를 별도로 전달합니다. UI는 검사가 끝날 때까지 정지하지 않아야 합니다.
7. 작업 파일은 대상 밖에서 정리하고 대상의 파일 목록·해시 불변을 검증합니다.

PHP를 `-n`만으로 실행하면 PHPCS 필수 확장이 빠질 수 있습니다. 프로그램 전용 ini 또는 -n과 명시 확장 로딩 중 실제 ZIP에서 동작하는 방법을 T01에서 검증합니다. 원본 보호는 현재 설계 의도이며 OS 수준 sandbox가 구현됐다는 뜻이 아닙니다.

개념적 인자는 `php <관리영역의 phpcs> --standard=<관리 ruleset 절대 경로> --runtime-set testVersion <대상 단일 버전> --report=json -q --no-cache <승인된 입력>`입니다. 실행 가능한 최종 명령은 T01 결과로 고정합니다. 파일 목록 사용·재귀 여부·제외·annotation 정책도 함께 검증하며 임의 기본값을 숨기지 않습니다.

T01 추가 실험에서는 PHP 프로세스의 통신 차단·설정 주입 차단·원본 불변을 확인했습니다. native junction 탐색은 exit255, symlink는 선택 폴더 밖 파일까지 추적했습니다. 링크 제외 명시적 file-list 방식이 검증됐으므로 D09에서 사용자와 이 정책을 판단해야 합니다. 사용자는 T02 계약안의 링크 제외·명시적 file-list 방식을 승인했습니다. 제품 구현과 annotation 추가 검증은 T04/T05에서 수행합니다.

## 결과 계약 초안

PHPCS 4.0.4 Json.php [S4]에는 totals(errors, warnings, fixable), 파일별 errors/warnings/messages가 있습니다. messages에는 message, source(규칙 코드), severity, type, line, column, fixable을 보존하는 안을 사용합니다. 오류·경고는 엔진 분류이며 실제 서비스 장애 등급이 아닙니다. fixable은 표시용 원값일 뿐 자동 수정 허용이 아닙니다.

현재 JSON의 totals에는 전체 검사 파일 수가 없습니다. files 항목과 별도 입력 목록의 관계를 성공·빈 폴더·읽기 실패에서 대조한 뒤 실제 검사 파일 수 정의를 T01/T02에서 확정합니다. 입력 파일 수를 검사 완료 파일 수로 잘못 표시하지 않습니다.

PHPCS 4.0.4의 종료 코드는 0=문제 없음, 1=수정 가능 탐지 비트, 2=수정 불가 탐지 비트, 조합 3=혼합 탐지이며, 처리 오류 16과 요구 환경 미충족 64가 구별됩니다 [S5]. 버전별 의미를 확인해야 하므로 비제로 전체를 실행 실패로 취급하지 않습니다. 반대로 JSON이 있어도 처리 오류·불완전 결과를 정상 완료로 취급하지 않습니다. stdout JSON 오염, stderr, 종료 코드, 파일 처리 상태를 함께 판단할 계약은 T01에서 실증합니다.

화면 계약 후보는 `대기 / 검사 중 / 완료(탐지 있음 또는 없음) / 검사 실패`입니다. 취소 버튼·진행률 퍼센트·실시간 스트리밍·필터·내보내기는 확정 기능이 아닙니다. 실패 후 부분 결과 취급과 세부 심각도는 D08/D12에서 정합니다.

## 배포 및 도구 관리 기준안

Windows x64 포터블 ZIP에 GUI 산출물과 분리된 tools/php/, tools/engine/, licenses/를 포함하는 형태를 추천합니다. 이는 배포 산출물의 예시이며 현재 저장소에 바이너리를 넣지 않습니다. 작업·로그·캐시 위치는 대상 밖이어야 하며 읽기 전용 설치 경로에서의 동작도 검토합니다.

도구 버전과 원본 URL·해시·의존성 lock을 기록하여 재현 가능하게 구성합니다. 기본 검사 도구를 미리 준비하여 네트워크 없이 검사하는 방향입니다. Composer는 빌드 과정에만 필요하도록 검증합니다. 사용자 측 Composer 설치를 전제하지 않습니다.

self-contained를 선택해도 PHP의 CRT 등 네이티브 의존성이 없어지는 것은 아닙니다. .NET 없는 PC와 VC++ 런타임 없는 PC를 구별하여 확인해야 합니다. 모든 PC에서 설치 없이 실행된다는 보장은 T09 검증 전 하지 않습니다. 다운로드·업데이트 UI는 현재 구현 범위에 넣지 않으며 도구 폴더 분리만으로 업데이트 기능 완료라고 하지 않습니다.

## 라이선스 검토와 배포 관문

자체 프로그램의 MIT 선택은 사용자 승인으로 확정했습니다. 실제 LICENSE 작성과 외부 재배포 적합성 검증은 T08에서 수행합니다. 별도 프로세스로 호출한다는 설계만으로 외부 도구의 재배포 의무가 사라진다고 판단하지 않습니다.

| 구성 / 확인한 버전 | 확인한 표시 | 후속 조치 |
| --- | --- | --- |
| PHPCompatibility 10.0.0-alpha2 | LGPL-3.0-or-later; LICENSE 전문 확인 [S2, S6] | LGPL와 참조 GPL 전문·저작권 보존, 배포본에 대응하는 소스 제공 방법과 교체·수정 권리 검토 |
| PHPCSUtils 1.1.2 | LGPL-3.0-or-later; LICENSE 전문 확인 [S11] | 위와 같은 배포 의무 및 정확한 포함 소스 확인 |
| PHP_CodeSniffer 4.0.4 | BSD-3-Clause; licence.txt 확인 [S3, S12] | 저작권·조건·면책 고지 보존 |
| Composer installer 1.1.0 후보 | MIT 메타데이터·LICENSE.md 확인 [S13] | 빌드 전용 여부와 vendor 포함 여부 확인 후 고지 |
| PHP 8.4.27 검토 예시 | 태그 LICENSE는 PHP-3.01 [S14] | 실제 선택 ZIP의 PHP·Zend·포함 DLL별 라이선스 확인; 예시를 최종 배포판으로 간주하지 않음 |
| PHPStan | 초기 배포 범위 미정 | 포함 승인 전 구현·번들 없음. 채택 시 실제 버전 LICENSE 재확인 |
| .NET 및 네이티브 DLL | 배포 방식·버전 미정 | 실제 산출물의 THIRD-PARTY-NOTICES 및 재배포 조건 확인 |

안전한 준비안은 LGPL 구성 요소를 수정 없이 소스 형태로 분리하고 정확히 대응하는 소스와 라이선스를 함께 제공하는 것입니다. 최종 적합성 판정은 T08에서 실제 파일 목록을 기준으로 합니다. 공식 PHP 라이선스 안내 [S15]는 새 라이선스도 설명하지만 이를 특정 과거 ZIP의 실제 고지로 대체하지 않습니다. 제품명 관련 조건이 최종 적용 라이선스와 충돌하는지 T08에서 확인하고 충돌하면 사용자에게 판단을 요청합니다.


## T02 확정 구현 계약

확정 기준: 2026-10-29 사용자 지시 “이후 최신 버전이 나와도 지원 가능한 시스템을 구축해. 나머지는 그대로 확정하자.” T01의 실측을 바탕으로 앞서 제시한 계약을 승인하고 D07 지원 확장 구조를 추가했습니다. 아래 계약은 구현 기준이며 제품 코드·프로젝트 파일·LICENSE 및 배포 검증은 후속 Task에서 수행합니다.

### 확정한 결정

| 결정 | 확정안 | 이유·대가·검증 경계 |
| --- | --- | --- |
| D01 대상 PHP | 초기 선택 목록 8.2 / 8.3 / 8.4. GUI 명칭은 **목표 PHP 버전**, 업그레이드하려는 버전을 사용자가 선택해야 실행 가능. 향후 목록은 팩 데이터에서 확장. 범위 검사·원본 버전 입력은 추가하지 않음 | 대표 사례만 검증했으므로 정적 문제 탐지 대상으로 표시. 완전 호환·모든 변경 지원이라고 표현하지 않음 |
| D02 엔진 | PHPCompatibility 10.0.0-alpha2 + PHPCS 4.0.4 + PHPCSUtils 1.2.3. 실험 lock의 installer 1.1.0 포함 | 사전 공개판 채택 승인 완료. 안정판 9.3.5만으로 8.x 범위를 지원한다고 오해시키지 않음. 현재 동적 속성 예제 미탐지를 안내 |
| D03 실행 PHP | 공식 PHP 8.4.27-1 x64 NTS VS17 ZIP을 초기 고정 기준으로 채택. URL·SHA-256은 ENGINE_PROBE.md 값 사용 | 검사 대상 8.2/8.3/8.4 실행 파일을 각각 내장하지 않음. T04 준비·T09 배포 전에 보안 상태 재확인. 교체 필요 시 새 버전·해시·재검증 근거를 기록하고 임의 변경하지 않음 |
| D04 GUI·SDK | C# / WPF / .NET 10 LTS, net10.0-windows. SDK 10.0.403을 global.json에 고정하고 사전 공개 SDK는 사용하지 않음. 언어 버전은 해당 SDK의 .NET 10 기본값 | 초기 제품 검증 대상은 Windows 11 x64. Windows Server 2022 CI는 엔진/빌드 검증 근거이며 데스크톱 제품 실행 검증을 대신하지 않음. OS 범위 확대는 별도 검증 후 판단 |
| D05 배포 | Windows x64 ZIP 한 종류, self-contained로 .NET Desktop Runtime 포함. PHP/기본 엔진도 준비해 기본 검사는 오프라인 | 압축 크기·런타임 패치 유지 부담 수용. PHP의 Visual C++ Runtime 전제는 별개이며 미충족 시 필수 조건을 안내하고 사용자가 설치. CRT 설치 자동화는 범위에 넣지 않음 |
| D07 지원 확장 | 아래 엔진 팩/목표 목록 계약으로 수동 교체 가능한 구조 확정 | 새 목표는 검증 후 활성화. 자동 다운로드·업데이트 UI는 별도 보류 |
| D08 결과 | ERROR/WARNING, 규칙 코드·원문 메시지·숫자 severity·위치·fixable 원값 보존. GUI 항목명은 한국어. 다른 규칙의 같은 줄 결과는 모두 표시 | 번역·추가 장애 등급·임의 중복 제거는 초기 구현 범위에 넣지 않음. fixable 값이 있어도 소스 수정 동작 없음 |
| D09 탐색 | .php/.inc 파일, 하위 폴더 포함 기본 켬·체크박스로 변경. 링크/junction/reparse point 제외, 링크인 선택 루트는 안내 후 검사하지 않음. 승인된 파일 목록을 대상 밖에 생성해 --file-list로 전달 | native junction exit255와 symlink 경계 이탈에 대응하는 안. vendor 등 일반 하위 폴더는 기본 포함하고 폴더별 자동 제외·별도 제외 UI는 추가하지 않음. 클라우드 placeholder도 reparse point일 수 있어 제외 사실을 안내하고 지원 범위에 명시 |
| D09 주석 지시 | --ignore-annotations로 대상 소스의 PHPCS 제외 주석을 무시하고 선택한 파일 전체를 검사 | 원본 주석을 수정하지 않음. 이 정책은 T01에서 별도 실증하지 않았으므로 T04에서 ignore 주석 fixture로 검증한 뒤 T05/T07에 연결. 검사되지 않은 파일을 검사 완료 수에 포함하지 않음 |
| D11 Git | Task별 브랜치·검토용 PR. 병합은 사용자 명시 지시가 있을 때만 수행. Release/tag도 별도 지시 | 현재 PR #1은 유지. T02 확정 후 문서 반영·T01/T02 검토를 마쳐도 자동 병합하지 않음. T03 시작 브랜치는 실제 병합 상태와 선행 문서를 확인해 생성 |
| D12 화면·내부 구조 | 단일 WPF 앱 프로젝트 src/Dk.PhpCompatibilityChecker, 폴더는 Models/Services로 최소 구분. MainWindow에서 폴더·버전·하위 포함·검사 버튼·상태·요약·결과 목록 제공 | 별도 DI/MVVM 프레임워크·엔진 플러그인 계층을 도입하지 않음. 제품 검사 실행은 UI 스레드를 막지 않게 하고 중복 실행을 방지. 창 닫기 때 관리 중인 자식 프로세스를 남기지 않는 동작은 T05/T07에서 검증 |
| D13 자체 라이선스 | 자체 작성 프로그램은 MIT로 선언. 외부 도구·PHP·DLL·.NET은 각 라이선스와 고지로 구분 | 자체 MIT 선택 승인 완료. T08에서 자체 LICENSE 및 실제 번들의 고지·대응 소스를 작성/검증. MIT 선택 자체를 LGPL/PHP/CRT 재배포 적합성 판정으로 간주하지 않음 |

D06 PHPStan, D10 내보내기와 D07의 자동 다운로드/업데이트 UI는 보류입니다. 엔진/목표 목록의 수동 교체·확장 구조는 확정입니다. 자동 수정·IDE·실제 서비스 실행도 범위에 넣지 않습니다.

### 확정 실행·결과 계약

- ScanRequest: 선택 폴더, 단일 대상 PHP 버전, 하위 포함 여부. 사용자가 임의 CLI 옵션·ruleset·실행 파일을 입력하는 경로는 초기 구현에 두지 않습니다.
- 탐색 결과는 선택 파일 목록과 제외/읽기 불가 항목을 구분합니다. 발견 단계부터 읽을 수 없는 경로가 있으면 누락을 숨기지 않고 검사 불완전으로 알립니다. 원본은 변경하지 않습니다.
- 결과 한 행은 file/line/column/type/severity/source/message/fixable 원값을 갖습니다. 새 심각도 의미를 만들지 않습니다. 엔진 버전과 실행 조건은 내부 결과 메타데이터에 보존합니다.
- 화면 상태는 대기 / 검사 중 / 완료 / 검사 대상 없음 / 검사 실패로 구분합니다. 취소 버튼·진행률 퍼센트·실시간 결과 스트리밍은 추가하지 않습니다.
- 선택 파일 수는 입력 목록 수, 검사 완료 파일 수는 유효 JSON의 실제 files 수입니다. 엔진 실패나 불량 JSON으로 완료 수를 판단할 수 없으면 미확인으로 표시합니다. 선택 수를 완료 수로 대신 표시하지 않습니다.
- 파일 목록 0개는 검사 대상 없음으로 안내하며 호환성 통과가 아닙니다. 정상 JSON에 모든 선택 파일이 대응하고 exit가 0/1/2/3일 때만 완료 후보입니다. exit 0인데 메시지/총수·파일 수가 모순되면 완료 처리하지 않습니다.
- exit16/64/255 등 실행 실패, 불량/오염 JSON, 읽기 실패, 선택 파일과 결과 files 불일치는 검사 실패입니다. 유효한 부분 결과가 있으면 미완료임을 표시하여 문제 목록을 확인할 수 있게 하되 전체 완료로 표시하지 않습니다.
- 완료 0건의 문구는 정적 검사에서 문제를 발견하지 못함으로 제한하고 런타임 호환을 보장하지 않음을 함께 표시합니다. 동적 속성 사례 미탐지·사전 공개판 사용을 한계 안내에 포함합니다.
- 캐시·임시 목록·원시 출력은 대상 폴더 밖의 프로그램 작업 영역에 둡니다. 임시 파일 경로가 대상/하위 경로와 겹치지 않아야 합니다. 기본은 사용자 로컬 임시 영역이며 원본 폴더에 캐시나 결과를 쓰지 않습니다.

사용자 승인 내용을 REQUIREMENTS.md의 D 상태와 관련 Task에 반영했습니다. 보류 항목은 구현 Task에 확정 전제로 넣지 않습니다. T03은 앱 골격·빌드·기본 창까지, T04는 고정 도구와 추가 정책 검증까지 수행하도록 기존 범위를 유지합니다.

### 향후 목표 버전을 위한 엔진 팩 계약

큰 플러그인 프레임워크를 만들지 않습니다. **단일 활성 도구 폴더와 manifest.json**을 사용하고 앱·검사 실행 계약은 유지합니다. 배포 구성은 tools/runtime/manifest.json과 tools/runtime/ 아래 php/, engine/으로 구분합니다. packId는 검증한 구성의 식별자이고 목표 PHP 버전과 별개입니다.

T04의 [준비·검증 안내](tools/engine-pack/README.md)에 실제 schema 1 필드·경로·정합성 검증기·Windows 근거를 기록했습니다. 생성물 tools/runtime/은 Git 제외이며 검증된 manifest 예시·파일 목록은 tests/t04/evidence/에 보존합니다. T05의 EnginePackLoader가 동일 계약을 C#에서 검증하며 ScanService가 검사 시 다시 로드합니다. 데이터 계약은 다음 정보로 한정합니다.

| 필드 | 의미 |
| --- | --- |
| schemaVersion | 앱이 이해하는 팩 manifest 형식. 처음에는 1 |
| packId | 구성요소·목표 목록·검증 자료가 함께 고정된 팩 식별자 |
| reportProfile | 해석할 출력·종료 계약. 처음에는 phpcs4-json-v1 |
| runtime | PHP 배포 버전/공식 출처/ZIP SHA-256·실행 파일 및 필요한 DLL 파일 해시 |
| engine | PHPCompatibility/PHPCS/Utils/installer의 고정 버전과 Composer lock 해시 |
| targets | version 문자열(major.minor), verificationRef, limitations. 활성 목록에는 해당 팩으로 검증한 목표만 포함 |
| verification | 팩별 fixture·기존 목표 회귀·원본 보호·오프라인·출력 계약 검증 근거 |

GUI 목록과 ScanRequest 검증은 같은 manifest의 targets를 사용합니다. 초기 8.2/8.3/8.4를 enum/XAML/실행 분기에 중복 하드코딩하지 않습니다. 프로그램은 활성 팩에 없는 버전을 임의 문자열로 엔진에 전달하지 않습니다. 단순히 targets에 숫자를 쓰는 것을 엔진 규칙 검증으로 인정하지 않습니다.

새 목표 지원 절차는 **엔진 규칙 확보 → 공식 도구와 lock/해시 고정 → 새 목표 양성/음성 및 기존 목표 회귀·안전성/오프라인 검증 → 검증 자료와 목표 목록이 포함된 새 팩 제작 → 앱 종료 후 runtime 폴더 수동 교체 → 앱 재시작 시 목록 반영**입니다. 기존 폴더를 보관해 이전 검증 팩으로 복원할 수 있도록 교체 절차를 제공합니다. 사용자 소스 폴더에 도구나 갱신 파일을 쓰지 않습니다.

팩 전체를 하나의 버전 단위로 취급하며 PHP·엔진·목표 목록 일부만 섞어 바꾸지 않습니다. 실행 중 핫스왑은 지원하지 않습니다. 스키마/report profile을 이해하지 못하거나 파일 해시·필수 구성 검증이 실패하면 그 팩의 검사를 시작하지 않고 원인을 알립니다. manifest에 임의 shell 명령·대상 부트스트랩을 넣어 실행하는 확장 경로는 만들지 않습니다.

현재 포맷·실행 계약이 유지되는 새 목표는 팩 데이터/엔진 교체만으로 추가합니다. PHPCS JSON/종료 의미나 엔진 요구 PHP/호스트 조건이 바뀌면 별도 어댑터/런타임 변경 Task로 분리하고 재검증합니다. 모든 미래 엔진을 코드 변경 없이 지원한다고 보장하지 않습니다.

T10에서 팩 교체·이전 팩 복원·미검증/비호환 팩 거부를 통합 검증합니다. 코드 재빌드 없는 목록 추가의 양성 시험은 이미 엔진 검증한 목표를 한 개 제외한 시험 팩에서 그 목표를 포함한 팩으로 교체하여 수행합니다. 가상의 미래 버전이나 미검증 버전을 지원한 것처럼 만드는 시험은 금지합니다. 실제 새 PHP 목표를 배포하려면 그 버전의 규칙과 fixture를 새로 검증해야 합니다.

### 개발 기준의 추가 공식 근거

2026-10-29 Microsoft 공식 metadata를 실제 열어 SDK 10.0.403, runtime 10.0.14, .NET 10 LTS 지원 종료 2028-11-14를 확인했습니다. 공식 metadata 확인과 실제 동작은 구별합니다. T03의 Windows Server 2022 SDK 설치·restore/build·창 기동 근거는 src/README.md를 따릅니다.

- https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json
- https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
- https://learn.microsoft.com/en-us/dotnet/core/install/windows

## 공식 근거

2026-10-26 아래 URL의 본문 또는 태그 파일에 실제 접근하여 확인했습니다. 실행 실험 근거가 아닙니다. T01 착수 시 릴리스와 보안 상태를 다시 확인하고 실험 버전을 기록합니다.

- S1: https://github.com/PHPCompatibility/PHPCompatibility/releases — alpha2(2025-11-28), 9.3.5(2019-12-27)
- S2: https://github.com/PHPCompatibility/PHPCompatibility/blob/10.0.0-alpha2/composer.json 및 같은 태그 README.md
- S3: https://github.com/PHPCSStandards/PHP_CodeSniffer/blob/4.0.4/composer.json
- S4: https://github.com/PHPCSStandards/PHP_CodeSniffer/blob/4.0.4/src/Reports/Json.php
- S5: https://github.com/PHPCSStandards/PHP_CodeSniffer/blob/4.0.4/src/Util/ExitCode.php
- S6: https://github.com/PHPCompatibility/PHPCompatibility/blob/10.0.0-alpha2/LICENSE
- S7: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/
- S8: https://learn.microsoft.com/en-us/dotnet/core/deploying/ 및 https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core — .NET 10 LTS 지원 종료 2028-11-14; .NET 8은 2026-11-10
- S9: https://downloads.php.net/~windows/releases/ — 공식 Windows 배포 목록 접근 확인
- S10: https://www.php.net/manual/en/install.windows.manual.php — PHP Windows 네이티브 실행 의존성
- S11: https://github.com/PHPCSStandards/PHPCSUtils/blob/1.1.2/composer.json 및 같은 태그 LICENSE
- S12: https://github.com/PHPCSStandards/PHP_CodeSniffer/blob/4.0.4/licence.txt
- S13: https://github.com/PHPCSStandards/composer-installer/blob/v1.1.0/composer.json 및 같은 태그 LICENSE.md
- S14: https://github.com/php/php-src/blob/php-8.4.27/LICENSE
- S15: https://www.php.net/license/
