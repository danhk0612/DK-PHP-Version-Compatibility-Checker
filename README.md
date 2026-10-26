# DK-PHP-Version-Compatibility-Checker

Windows에서 PHP 소스 폴더를 선택하고, 지정한 PHP 버전의 정적 호환성 문제를 확인하는 GUI 프로그램을 준비합니다.

**현재 단계: 요구사항·아키텍처 기준안 및 개발 계획 수립. 실행 프로그램과 배포 ZIP은 아직 없습니다. 공식 지원 PHP 버전도 아직 확정되지 않았습니다.**

예정 사용 흐름은 `소스 폴더 선택 → 대상 PHP 버전 선택 → 검사 → 파일·줄 번호·문제 내용 확인`입니다. 검사 대상 소스를 수정하거나 프로젝트에 패키지를 설치하지 않습니다. 정적 검사에서 문제가 발견되지 않아도 실제 서비스의 정상 동작을 보장하지 않습니다.

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

다음 작업은 **T01 검사 엔진 기술 검증**이며, 권장 환경은 **Windows 실행 검증이 가능한 저성능 Work**입니다. 현재는 빌드할 프로젝트 파일이 없습니다.

## 기술·라이선스 상태

C#/.NET WPF와 PHP CLI + PHP_CodeSniffer + PHPCompatibility를 우선 검토합니다. 구체적인 버전, 배포 형태 및 사전 공개판 사용은 검증·판단 대기입니다. 자체 프로그램은 MIT를 목표로 검토 중이며, 아직 최종 라이선스를 선언하지 않았습니다. 외부 구성 요소에는 각각의 라이선스가 적용됩니다.
