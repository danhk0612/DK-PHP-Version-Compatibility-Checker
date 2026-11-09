# 현재 상태

## 작업 기준

- 확인일: 2026-11-09 (UTC).
- 저장소: https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker
- 기본 브랜치: `main`. 현재 개발 결과는 `t08-minimal-cli`, Draft [PR #8](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/pull/8)에 있으며 main에는 미병합입니다.
- 기능 검증 기준 커밋: `2a2db7a5413fa463a258bafd62d8c94ca47e77ce`. 이후 문서 정리는 새 기능 검증이나 새 바이너리/소스 페어링을 뜻하지 않습니다.
- 병합·tag·Release는 별도 명시 지시가 필요합니다. 공개 배포 승인 상태는 `false`입니다.

## 구현된 기능과 유효한 결정

T00~T07은 완료했습니다. 폴더 선택 → 목표 PHP 버전 선택 → 검사 → 파일·줄·원문 문제와 집계 확인의 WPF 흐름을 구현했습니다. 검사 대상은 읽기 전용이며 대상에 Composer 설치·캐시 생성·소스 실행/수정을 하지 않습니다. 정적 분석 결과는 실제 PHP 실행·DB·외부 라이브러리·서비스 정상 동작을 보장하지 않습니다.

- Windows 11 x64, .NET 10 WPF, SDK 10.0.403, self-contained 포터블 ZIP을 기준으로 합니다. 사용자 PC의 .NET 별도 설치를 요구하지 않는 방향입니다. 깨끗한 Windows 11의 CRT·배포 전제는 아직 확인 대기입니다.
- 공식 PHP 8.4.27-1 NTS VS17 x64, PHPCompatibility 10.0.0-alpha2, PHP_CodeSniffer 4.0.4, PHPCSUtils 1.2.3을 고정했습니다. 목표는 8.2/8.3/8.4입니다. 사전 공개 엔진과 동적 속성 미탐지 한계를 표시합니다.
- 기본 MinimalCli PHP 구성은 원본 ZIP의 7파일입니다. Full 79파일 대비 PHP 부분의 바이트 수는 86.42% 감소했습니다. 앱 전체 ZIP 감소율이 아닙니다.
- 활성 엔진 팩의 manifest가 목표 목록·구성요소·해시·검증 근거를 제공합니다. 새 PHP 목표는 실제 엔진 규칙과 회귀·보호·오프라인 검증 후 팩으로 추가합니다. 새 버전 발표만으로 지원을 선언하지 않습니다.
- 검사 입력은 링크를 제외한 명시적 파일 목록입니다. 최대 100파일씩 새 PHP 프로세스에서 순차 검사하고 각 프로세스에 512MB를 설정합니다. exit255와 실제 메모리 소진 진단이 확인된 묶음만 절반으로 나누어 재시도합니다.
- 실패한 상위 묶음 결과는 합산하지 않습니다. 단일 파일 실패는 경로를 표시하고 정상 형제/후속 묶음 결과를 보존하며 전체 상태는 실패입니다. 처리 수 미확인이나 중단을 완료 수로 바꾸지 않습니다. 여러 성공 프로세스에 하나의 OS 종료 코드를 만들지 않습니다.
- 실패 영역에서 이유·엔진 종료 코드·stderr 및 JSON 해석 실패 시 stdout 앞/뒤 일부를 읽고 Ctrl+A/C로 복사할 수 있습니다.
- 자체 코드는 MIT입니다. 사용자 승인된 PHP v4/Zend v3 후속 조건 선택과 독립 LGPL/Microsoft 등 외부 조건은 구분합니다. 전체 배포 적합성은 아직 완료가 아닙니다.

세부 계약은 [REQUIREMENTS](REQUIREMENTS.md), [ARCHITECTURE](ARCHITECTURE.md), [제품 코드 안내](src/README.md)를 따릅니다. PHPStan·내보내기·온라인 자동 다운로드/갱신 UI·자동 수정은 구현 범위에 추가하지 않았습니다.

## 현재 검증 근거

| 영역 | 확인된 결과 | 근거와 한계 |
| --- | --- | --- |
| 엔진·원본 보호 | 목표별 fixture, 설정 격리, 링크 제외, 입력 목록/해시 불변, 네트워크 차단 검사 | [T01](tools/ENGINE_PROBE.md), [T04](tests/t04/evidence/). 동적 속성 미탐지 등 엔진 한계를 포함한 검증입니다. |
| 최신 서비스·GUI 회귀 | T05 71건 / T06 25건 / T07 36건 통과 | [Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/42279858379), [근거](tests/t09/evidence/batched-scans.json). Windows Server 2022 CI입니다. |
| 분할 검사 | 실제 PHPCS의 자체 생성 28,817파일, 205입력 100/100/5 새 프로세스, 실제 메모리 소진 재분할, 복수 단일 실패·결과 보존·중단 정리 통과 | 같은 batched-scans 근거. 생성 입력의 통과를 사용자 프로젝트 시험으로 대체하지 않습니다. |
| 사용자 실제 프로젝트 | 목표 8.4·하위 포함, 선택/처리 28,817, ERROR 550 / WARNING 265, 완료 표시 | 2026-11-09 사용자 제공 화면. 실행 바이너리/ZIP의 정확한 SHA는 미수집입니다. 815건은 탐지 문제 수입니다. |
| 로컬 후보 준비 | PHP/Composer 사전 설치 없이 고정 해시 임시 도구 사용, 중첩 긴 경로 수집·환경 복원·정리 통과 | [Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/42147647503), [근거](tests/t09/evidence/bootstrap-preparation.json). |
| 최신 후보 준비·페어링 | 필수 재구성 파일 6개 포함·바이트 대조, 누락/변조 거부 등 13건과 실제 전체 준비 통과. scopePassed=true, candidateForUserTesting=true, publicReleaseApproved=false | [Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/42549726060), [기준 커밋·두 ZIP SHA와 근거](tests/t09/evidence/reconstruction-inputs.json). 프로젝트 소스·엔진 레시피도 대조했으며 외부 공개 ZIP은 게시하지 않았습니다. |
| T09 ZIP 자동시험 | self-contained ZIP 생성, 한글 경로 앱 창 표시·응답·종료·파일 불변 통과 | [Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/42055646466), [근거](tests/t09/evidence/). 깨끗한 Windows 11 시험은 아닙니다. |
| T10 팩 교체 | 같은 앱 assembly에서 reduced→full→reduced, 새 OS 프로세스의 실제 목록·검사·복원 및 잘못된 팩 거부 12건 통과 | [Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/42055646710), [근거](tests/t10/evidence/). 기존 목표의 부분집합을 이용한 개발 시험이며 미래 PHP 버전 지원 검증은 아닙니다. |

batched-scans.json의 “사용자 프로젝트 재시험 전” 문구는 해당 자동 실행 당시의 한계 기록입니다. 현재 사용자 시험 상태는 위 화면 보고가 우선하며 원본 실행 근거를 소급 수정하지 않습니다. 같은 실제 프로젝트의 수동 오프라인 재검사는 사용자 요청으로 **미실행·생략**했습니다. 통과로 간주하지 않고 재요청하지 않습니다. 기존 자동 오프라인 근거와 오프라인 동작 요구사항은 유지합니다.

## T08 배포 준비 상태

자체 LICENSE·서드파티 고지, 실제 publish 파일 출처, 엔진 LGPL 소스 바이트 대응을 준비했습니다. 최소 CLI의 코어 원문 6개와 Windows 생성 헤더 2개를 구분하고 설정 포함 bundled digest 23/23을 확인했습니다. builder/SDK 소스 2개도 수집해 엔진 2개와 함께 소스·고지 동반 ZIP을 생성하는 절차가 있습니다.

동반 ZIP에는 재링크의 의존성 후보 해시 목록·결과 비교기와 후보 생성에 필요한 검증/안내 파일 6개를 명시적으로 포함합니다. 이전 필터의 누락을 보완했고, 위 최신 후보 검증에서 존재·현재 checkout 바이트 일치와 누락/변조 거부를 확인했습니다. 전체 일반 CI 시험 코드까지 동반 ZIP에 포함하는 것은 아닙니다.

격리 libiconv 원본/마커 빌드 및 PHP 재링크 후 PHPCS 24사례 동일 결과를 확인했습니다. 이 감사용 축소 빌드는 실제 제품 PHP 바이너리에 필요한 전체 입력 대응을 입증하지 않습니다. [공식 artifact 직접 대조](tests/t08-minimal/evidence/official-artifact-identity.json)에서는 공식 빌드 산출물 안의 NTS x64 ZIP과 승인 PHP ZIP의 SHA 일치, php.exe/php8.dll 및 기존 Windows source ZIP 해시 일치를 확인했습니다. 과거 SDK 커밋·의존성 ZIP 바이트와 전체 빌드/링크 입력의 충분성은 여전히 미검증입니다. 자세한 원문·해시·실험 범위는 [LICENSING](LICENSING.md), [T08 최소 CLI 근거](tests/t08-minimal/evidence/)에 있습니다.

T08은 부분 검증입니다. 남은 관문은 실제 배포물에 대응하는 소스·변경·필요 빌드/링크 자료의 충분성과 고지·권리 조건 검토, 최종 바이너리와 소스·고지의 정확한 페어링 및 지속 제공입니다. 공식 바이너리와 동일 해시로 재생성하는 것 자체를 별도 필수 조건으로 추가하지 않습니다. 7일 Actions artifact 보관은 지속 소스 제공 완료가 아닙니다.

## 남은 작업과 시작점

| 구분 | 다음 작업 | 상태·권장 환경 |
| --- | --- | --- |
| T08 기술·배포 검토 | 실제 제품 PHP에 필요한 소스/빌드/링크 자료와 고지의 잔여 대응을 LICENSING 기준으로 검토 | 부분 검증. 자료 대조는 저성능 Work, 해석 충돌은 고성능 Chat. 제품 PHP 교체·스택 변경은 임의 수행하지 않습니다. |
| T09 직접 시험 | 다른 목표, 실제 원본 목록/해시 불변, 깨끗한 Windows 11의 CRT/.NET 전제·일반 사용자·읽기 전용 위치 등 미실행 항목 기록 | [Windows 11 절차](tests/t09/WINDOWS11_TEST.md). 8.4 대규모 완료 확인과 생략한 중복 오프라인 재검사를 다시 요구하지 않습니다. |
| T10 최종 배포 대조 | 배포에 사용할 팩 구성을 T08 고지/대응 소스와 재대조 | 개발 자동시험 완료, T08 배포 조건 대기. 저성능 Work. |
| 최종 후보·공개 운영 | 최종 checkout에서 후보와 소스 ZIP 재생성·페어링 후 결과 검토. 병합·tag·Release 및 지속 제공은 명시 지시 후 진행 | 현재 미승인. 기존 로컬 진단 재빌드는 새 후보 페어링을 대신하지 않습니다. |

현재 기능 추가 구현 Task는 없습니다. 남은 배포 조건을 확인하지 않고 T08/T09/T10 전체 완료나 공개 배포 가능으로 표시하지 않습니다.

다음 Work는 현재 원격 브랜치와 이 문서·TASKS·LICENSING을 먼저 확인하고 **T08의 잔여 입력 대응/고지 검토**만 진행합니다. 기존 수집·재링크·페어링 근거를 재사용하고, 새로운 공식 자료가 있으면 출처와 해당 제품과의 연결을 검증합니다. 검증 불가 항목은 원인과 필요한 자료를 구체적으로 남깁니다. 별도 승인 없는 배포·PHP 교체·새 기능·미실행 Windows 시험의 완료 처리는 금지합니다. 작업 원칙은 [AI_WORKFLOW](AI_WORKFLOW.md)를 따릅니다.
