# 현재 상태

## 저장소 기준

- 확인일: 2026-10-29 (한국 시간)
- 저장소: https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker
- 최초 착수 시 공개 저장소, 기본 브랜치 설정 main, branches=[], 기존 파일·커밋 없음. 따라서 착수 기준 커밋 SHA는 없습니다.
- 이 문서가 포함된 최초 준비 커밋은 Git 기록에서 확인합니다. 자기 자신의 커밋 SHA를 문서에 고정하지 않습니다.

## 준비된 내용

T00 최초 구축 범위로 README, 6개 기준 문서, AGENTS 및 최소 폴더 역할 문서를 준비했습니다. 확정 요구사항과 D01~D13 결정 대기를 분리하고 Task별 범위·의존성·완료 조건·작업 환경을 정리했습니다.

현재 유효한 확정 방향은 Windows GUI, 외부 엔진 재사용, 원본 읽기 전용, 대상과 도구 환경 분리, 정적 분석 한계의 명시입니다. WPF·구체 .NET 버전·엔진 조합·PHP 버전·배포 방식은 추천 기준안이며 최종 채택이 아닙니다.

## 검증한 것

- 최신 main과 커밋·관련 PR, 작업 전 로컬 상태를 확인했습니다.
- 공식 릴리스·고정 태그 composer/라이선스·PHPCS JSON/종료 코드 소스 및 Microsoft/PHP 공식 문서를 읽었습니다. 근거는 ARCHITECTURE.md에 있습니다.
- 문서의 로컬 링크, 요구 문서 존재, Task ID·선행 조건 및 Git diff 공백 오류를 점검했습니다.
- T01의 공식 릴리스와 보안 자료를 재확인했습니다. PHPCSUtils 1.1.2는 공개 취약 버전 범위에 해당하며, 1.2.3이 수정판입니다. 영향 코드의 실제 도달 여부와 대체 조합은 미검증입니다. 상세 근거는 [ENGINE_PROBE.md](tools/ENGINE_PROBE.md)에 있습니다.

## 구현·검증하지 않은 것

제품 실행 프로그램, solution/csproj, Composer lock, 설치된 도구, 실행 테스트 결과, CI, 빌드, GUI, Windows ZIP, Release/tag는 없습니다. T01용 실험 manifest·전용 ruleset·8개 PHP fixture·케이스 정의·Windows 명령 초안을 준비하고 정적 구조만 점검했습니다. PHP/.NET/Composer·Windows 실행 도구가 없는 Linux 환경으로 실제 엔진·PowerShell 실행 검증은 하지 않았습니다.

현재 요청한 실험 후보는 PHPCompatibility 10.0.0-alpha2 + PHPCS 4.0.4 + PHPCSUtils 1.2.3입니다. 기존 1.1.2는 공개 보안 문제로 사용하지 않습니다. 수정판의 의존성 범위는 확인했으나 실제 조합은 미검증입니다. PHP 8.2·8.3·8.4 공식 지원, 사전 공개판 안정성, 원본 보호 동작, 완전 오프라인 실행, CRT 없는 PC 실행, 라이선스 최종 적합성을 검증 완료로 보지 않습니다. 자체 MIT 목표는 있으나 LICENSE 선언은 대기입니다.

## 다음 작업

**T01 부분 검증·Windows 실행 차단 / Windows x64 실행 환경과 PHP CLI·Composer 필요.** [보고서](tools/ENGINE_PROBE.md)의 보안 사항을 고려하여 도구 조합을 검증하고 TASKS.md의 완료 조건을 수행해야 합니다. [Windows 준비 절차](tools/probe/README.md)로 실험을 이어갈 수 있으나 아직 명령도 실제 검증되지 않았습니다. 사전 공개판 사용 승인이나 공식 지원 선언은 하지 않았습니다.

T01의 핵심 검증을 보완한 뒤 T02에서 사용자와 필수 결정을 논의합니다. T03~T09는 선행 대기입니다.
