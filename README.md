# DK-PHP-Version-Compatibility-Checker

Windows에서 PHP 소스 폴더를 선택하고, 지정한 PHP 버전의 정적 호환성 문제를 확인하는 GUI 프로그램을 준비합니다.

**현재 단계: 기본 검사와 분할 검사 구현·Windows CI 검증을 마쳤고 사용자 지정 제외와 CSV·HTML 저장을 추가·검증했습니다. 배포 ZIP·팩 교체 자동시험은 통과했으며 최종 배포 조건은 검토 중입니다. 현재 검증 팩의 목표는 8.2/8.3/8.4/8.5입니다. 실행 PHP CLI는 8.4.27입니다.**

사용 흐름은 `소스 폴더 선택 → 목표 PHP 버전 선택 → 검사 → 파일·줄 번호·문제 내용 확인`입니다. 현재 결과는 CSV 또는 HTML로 저장할 수 있습니다. 검사 대상 소스를 수정하거나 프로젝트에 패키지를 설치하지 않습니다. 정적 검사에서 문제가 발견되지 않아도 실제 서비스의 정상 동작을 보장하지 않습니다.

승인된 추가 개발 순서는 **제외 폴더·파일(T11) → CSV/HTML 결과 내보내기(T12) → PHP 8.5 검증·추가(T13) → 검증된 엔진 팩 자동 다운로드(T14)**입니다. 기능별 구현·검증 상태는 [CURRENT_STATE.md](CURRENT_STATE.md), 범위와 선행 조건은 [TASKS.md](TASKS.md)를 따릅니다. 공개 Release는 아직 없습니다.

## 실행 환경

Windows 11 x64에서 [Microsoft .NET Desktop Runtime 10 x64](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)를 설치하고, 실행 ZIP을 풀어 `Dk.PhpCompatibilityChecker.exe`를 실행합니다. 기본 PHP 검사 엔진은 ZIP에 들어 있어 준비 후 오프라인에서 검사할 수 있습니다. PHP 실행에 VC++ 런타임이 필요한 경우 [Microsoft Visual C++ 재배포 패키지 x64](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist)를 설치합니다.

## 개발 시작

[AGENTS.md](AGENTS.md)의 시작 절차를 따르고, 다음 문서를 확인합니다.

| 문서 | 역할 |
| --- | --- |
| [PROJECT.md](PROJECT.md) | 목적·범위·비목표 |
| [REQUIREMENTS.md](REQUIREMENTS.md) | 요구사항·검증 기준·미확정 항목 |
| [ARCHITECTURE.md](ARCHITECTURE.md) | 기술 기준안·공식 근거·위험·결정 대기 |
| [CURRENT_STATE.md](CURRENT_STATE.md) | 실제 구현·검증 상태와 다음 작업 |
| [TASKS.md](TASKS.md) | Task별 범위·의존성·완료 조건 |
| [AI_WORKFLOW.md](AI_WORKFLOW.md) | 작업 환경 선택·Git 운영·인계 지시문 |

T01 기술 검증, T02 구현 계약, T03 최소 WPF 골격, T04 고정 엔진 팩 준비와 T05 검사 실행·결과 해석과 T06 입력·결과 화면을 마쳤습니다. [빌드·실행 안내](src/README.md)를 따릅니다. T07에서 실제 버튼·엔진·종료 정리를 연결하고 통합 36건/화면 회귀 21건을 통과했습니다. [통합 실측](tests/t07/README.md)을 확인합니다. T08에서 400파일 publish와 팩 865파일 및 엔진 대응 소스를 대조했습니다. B안 최소 CLI 팩과 [설정 생성물 포함 소스 대조](tests/t08-minimal/README.md)를 검증했습니다. PHP 코어 고지·바이너리 대응/재링크 검토는 T08에 남아 있습니다. [검토·판단안](LICENSING.md)을 확인합니다. [팩 준비·검증 안내](tools/engine-pack/README.md)를 확인합니다. PR #8은 PR #7 기반이며 PR #1~#8 모두 main 미병합 상태입니다.

## 기술·라이선스 상태

C#/.NET 10 WPF, 공식 PHP CLI와 PHP_CodeSniffer/PHPCompatibility 사전 공개판 조합, 프레임워크 종속 Windows x64 ZIP으로 배포하기로 했습니다. 초기 목표는 8.2/8.3/8.4이며 이후에는 검증된 엔진 팩·목표 목록 교체로 확장하도록 구현합니다. 팩 준비·검사 서비스·GUI 실제 사용자 흐름을 구현하고 검증했습니다. 자체 프로그램은 [MIT](LICENSE)이며 [외부 고지 초안](THIRD-PARTY-NOTICES.md)과 실제 파일 대조를 준비했습니다. 외부 native 코드의 최종 고지·대응 소스/재배포 검토는 T08에 남아 있습니다. 외부 구성 요소에는 각각의 라이선스가 적용됩니다.
