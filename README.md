# DK-PHP-Version-Compatibility-Checker

Windows에서 PHP 소스 폴더를 선택하고, 지정한 PHP 버전의 정적 호환성 문제를 확인하는 GUI 프로그램을 준비합니다.

**현재 단계: 최소 WPF 앱 골격과 공유 데이터 모델 준비 완료. 검사 기능과 배포 ZIP은 아직 없습니다. 초기 목표 PHP 버전은 8.2/8.3/8.4로 확정했으며, 정적 분석 한계를 안내합니다.**

예정 사용 흐름은 `소스 폴더 선택 → 목표 PHP 버전 선택 → 검사 → 파일·줄 번호·문제 내용 확인`입니다. 검사 대상 소스를 수정하거나 프로젝트에 패키지를 설치하지 않습니다. 정적 검사에서 문제가 발견되지 않아도 실제 서비스의 정상 동작을 보장하지 않습니다.

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

T01 기술 검증, T02 구현 계약, T03 최소 WPF 골격을 마쳤습니다. [빌드·실행 안내](src/README.md)를 따릅니다. 다음 작업은 **T04 고정 도구 구성과 오프라인 준비 / 저성능 Work**입니다. PR #2는 PR #1을 기반으로 하며 모두 main 미병합 상태입니다.

## 기술·라이선스 상태

C#/.NET 10 WPF, 공식 PHP CLI와 PHP_CodeSniffer/PHPCompatibility 사전 공개판 조합, 런타임 포함 x64 ZIP으로 확정했습니다. 초기 목표는 8.2/8.3/8.4이며 이후에는 검증된 엔진 팩·목표 목록 교체로 확장하도록 구현합니다. 팩 구성과 GUI/실행 연동은 후속 Task에서 구현합니다. 자체 프로그램은 MIT 선택을 확정했고 실제 LICENSE/외부 고지·재배포 검증은 T08에서 수행합니다. 외부 구성 요소에는 각각의 라이선스가 적용됩니다.
