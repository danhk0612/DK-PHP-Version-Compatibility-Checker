# 현재 상태

## 저장소 기준

- 확인일: 2026-10-26 (한국 시간)
- 저장소: https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker
- 최초 착수 시 공개 저장소, 기본 브랜치 설정 main, branches=[], 기존 파일·커밋 없음. 따라서 착수 기준 커밋 SHA는 없습니다.
- 이 문서가 포함된 최초 준비 커밋은 Git 기록에서 확인합니다. 자기 자신의 커밋 SHA를 문서에 고정하지 않습니다.

## 준비된 내용

T00 최초 구축 범위로 README, 6개 기준 문서, AGENTS 및 최소 폴더 역할 문서를 준비했습니다. 확정 요구사항과 D01~D13 결정 대기를 분리하고 Task별 범위·의존성·완료 조건·작업 환경을 정리했습니다.

현재 유효한 확정 방향은 Windows GUI, 외부 엔진 재사용, 원본 읽기 전용, 대상과 도구 환경 분리, 정적 분석 한계의 명시입니다. WPF·구체 .NET 버전·엔진 조합·PHP 버전·배포 방식은 추천 기준안이며 최종 채택이 아닙니다.

## 검증한 것

- GitHub 연결 조회와 git clone으로 빈 저장소 상태를 확인했습니다.
- 공식 릴리스·고정 태그 composer/라이선스·PHPCS JSON/종료 코드 소스 및 Microsoft/PHP 공식 문서를 읽었습니다. 근거는 ARCHITECTURE.md에 있습니다.
- 문서의 로컬 링크, 요구 문서 존재, Task ID·선행 조건 및 Git diff 공백 오류를 점검했습니다.

## 구현·검증하지 않은 것

실행 프로그램, solution/csproj, Composer manifest/lock, 내장 도구, fixture, 테스트 코드, CI, 빌드, GUI, 실제 검사, Windows ZIP, Release/tag는 없습니다. PHP/.NET/Composer 실행 도구가 없는 Linux 초기 환경에서 문서만 준비했으며 Windows 실행 검증은 하지 않았습니다.

PHPCompatibility 10.0.0-alpha2 + PHPCS 4.0.4 + PHPCSUtils 1.1.2는 공식 의존성 제약을 확인한 **실험 후보**입니다. PHP 8.2·8.3·8.4 공식 지원, 사전 공개판 안정성, 원본 보호 동작, 완전 오프라인 실행, CRT 없는 PC 실행, 라이선스 최종 적합성을 검증 완료로 보지 않습니다. 자체 MIT 목표는 있으나 LICENSE 선언은 대기입니다.

## 다음 작업

**T01 검사 엔진 기술 검증 / 저성능 Work / Windows x64 실행 환경 필요.** TASKS.md의 T01과 AI_WORKFLOW.md의 그대로 사용할 지시문을 따릅니다. T01은 실험과 근거 수집까지이며 사전 공개판 사용 승인이나 공식 지원 선언은 하지 않습니다.

T01 결과가 준비되면 T02에서 사용자와 필수 결정을 확정합니다. 그 뒤 T03~T09를 진행합니다. Windows 환경을 확보하지 못하면 가능한 자료만 기록하고 T01을 부분 검증으로 남겨야 하며 완료 처리하지 않습니다.
