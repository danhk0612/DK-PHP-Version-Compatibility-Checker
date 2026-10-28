# 아키텍처 기준안

## 상태와 결정 원칙

2026-10-26 공식 자료를 확인한 **설계 기준안**입니다. 제품 구현은 아직 없습니다. T01의 Windows 기본 엔진 실행은 검증했으며 범위·한계는 [보고서](tools/ENGINE_PROBE.md)를 따릅니다. 확정 요구는 REQUIREMENTS.md가 관리하며 D01~D13을 사용자 판단 없이 확정하지 않습니다. 개발 중인 브랜치의 문서를 배포 태그의 계약으로 대체하지 않습니다.

## 기술 검토 결과

| 구성 | 확인 사실 | 제안과 남은 검증 |
| --- | --- | --- |
| GUI | WPF는 Windows 전용 .NET UI [S7] | 기획의 C#/WPF 유지. .NET 10 LTS 우선 추천; 구체 버전·OS·SDK는 D04 |
| .NET 배포 | self-contained는 .NET 런타임 포함; framework-dependent는 런타임 필요 [S8] | 간단한 실행을 위해 전자 추천. ZIP 크기·업데이트 부담 비교 후 D05 결정 |
| PHPCompatibility | 공개 릴리스 최신은 10.0.0-alpha2, 안정판은 9.3.5 [S1] | 8.x 규칙을 포함한 alpha2를 고정 실험. 채택은 D02 승인 필요 |
| 고정판 의존성 | alpha2는 PHP >=5.4, PHPCS ^3.13.3 또는 ^4.0, PHPCSUtils ^1.1.2 요구 [S2] | 실행 PHP 최저 요구와 검사 대상 PHP 버전을 혼동하지 않음 |
| PHPCS | 4.0.4 composer는 PHP >=7.2.0 및 libxml/simplexml/tokenizer/xmlwriter 요구 [S3] | alpha2 + 4.0.4 + Utils 1.2.3을 실험 후보로 요청. 기존 1.1.2는 보안 공지로 제외. Windows 기본·추가 기술 검증 완료. 탐지·탐색 한계와 제품 결정은 보고서/T02 참조 |
| 설치 | alpha2는 Composer 설치를 공식 지원 경로로 안내 [S2] | 개발·패키징 단계의 도구 전용 Composer 환경에서 의존성을 구성; 사용자 프로젝트에 설치하지 않음 |
| PHP CLI | 공식 Windows 다운로드 목록 접근 확인 [S9]; CRT 필요 [S10] | 지원 중인 보안 수정판 x64 NTS 후보. 정확한 버전·해시·배포 DLL은 T01에서 선정하여 제안 |

주의: 확인 시점의 develop 브랜치는 alpha2와 요구 버전이 다릅니다. develop은 PHP >=7.2 및 PHPCS ^4.0.2를 요구하지만 alpha2에 그 요구를 소급하지 않습니다. 움직이는 dev-develop을 제품 의존성으로 고정하지 않습니다.

PHPCompatibility는 탐지 범위가 완전하지 않음을 명시합니다 [S2]. `testVersion`에 8.4를 전달할 수 있다는 사실만으로 PHP 8.4 완전 지원을 선언할 수 없습니다. 버전별 대표 변경의 양성·음성 fixture와 누락 목록이 필요합니다.

2026-10-29 재확인: PHPCSUtils 1.1.2는 [보안 공지](https://github.com/PHPCSStandards/PHPCSUtils/security/advisories/GHSA-r6hr-vr92-vv28)의 영향 범위이며 수정판 1.2.3을 실험 후보로 사용합니다. [1.2.3 manifest](https://github.com/PHPCSStandards/PHPCSUtils/blob/1.2.3/composer.json)는 PHPCS ^3.13.5 또는 ^4.0.1을 요구합니다. 아래 1.1.2 라이선스 확인은 과거 검토 근거이며 현재 실행 추천이 아닙니다. 실측 상태와 준비물은 [ENGINE_PROBE.md](tools/ENGINE_PROBE.md)를 따릅니다.

## 최소 논리 구조

아래는 역할 구분이며 아직 별도 프로젝트·프레임워크·DI 계층을 만들라는 뜻이 아닙니다. T02에서 최소 물리 구성을 고정합니다.

| 역할 | 책임 | 경계 |
| --- | --- | --- |
| WPF 화면 | 폴더·대상 버전 입력, 실행 상태·요약·목록 | 검사 규칙 재구현 없음 |
| 검사 실행 | 승인된 입력을 고정된 PHP/PHPCS 인자로 전달, stdout/stderr·종료 수집 | shell·대상 소스·대상 설정 실행 없음 |
| JSON 해석 | 엔진 JSON을 결과 데이터로 변환 | 엔진 오류를 성공 결과로 변환하지 않음 |
| 도구 영역 | 버전 고정 PHP·엔진·설정·고지 | 대상 프로젝트와 분리; 사용자 전역 PHP/Composer 변경 없음 |

초기 소스는 src/, 검증은 tests/, 엔진 구성·패키징 자료는 tools/로 구분합니다. 현재 src/에는 역할 README만 있으며 tools/probe/와 tests/fixtures/에는 T01 검증 입력·수집기·실제 lock/결과이 있습니다. 플러그인 시스템, 다중 엔진 공통 프레임워크, 저장소 DB는 도입하지 않습니다.

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

T01 추가 실험에서는 PHP 프로세스의 통신 차단·설정 주입 차단·원본 불변을 확인했습니다. native junction 탐색은 exit255, symlink는 선택 폴더 밖 파일까지 추적했습니다. 링크 제외 명시적 file-list 방식이 검증됐으므로 D09에서 사용자와 이 정책을 판단해야 합니다. 제품 탐색 구현이나 기본값을 이 조사만으로 확정하지 않습니다.

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

자체 MIT는 목표이며 아직 선언하지 않았습니다. 별도 프로세스로 호출한다는 설계만으로 외부 도구의 재배포 의무가 사라진다고 판단하지 않습니다.

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
