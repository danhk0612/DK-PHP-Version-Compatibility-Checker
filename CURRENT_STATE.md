# 현재 상태

## 저장소 기준

- 확인일: 2026-10-29 (한국 시간)
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

## 확정한 향후 버전 확장 구조

사용자가 이후 최신 PHP도 지원 가능한 시스템을 요청했습니다. 목표 목록·구성요소 버전·검증 근거를 앱 코드와 분리한 단일 활성 엔진 팩으로 관리합니다. 새 목표는 해당 엔진 규칙과 회귀·안전성·오프라인 검증을 거쳐 팩 데이터로 추가하며 GUI/실행 코드에 초기 세 버전을 고정하지 않습니다. 호환 스키마/profile의 팩은 앱 종료 후 수동 교체합니다. 자동 다운로드/갱신 UI는 아직 보류입니다.

T04의 실제 manifest 생성·팩 정합성 검증기를 구현했습니다. C# 팩 소비·검사 실행·JSON 해석은 T05에 구현했습니다. 입력·결과 GUI는 T06, 실제 화면 연결은 T07, 팩 교체·복원·확장 통합 검증은 T10에 남아 있습니다. 새 버전이 발표됐다는 이유만으로 지원 완료를 선언하지 않습니다.

## 미구현·검증 한계

빈 WPF 기본 창과 공유 모델은 구현했습니다. C# 검사 서비스와 manifest 소비를 구현했으며 아직 입력·결과 GUI, 화면과 실제 검사의 연결, 팩 교체 통합 기능은 없습니다. 깨끗한 일반 사용자 PC의 CRT·최소 배포 DLL·실제 GUI/성능은 T04/T07/T09에서 검증합니다. 모든 PHP 변경·링크 순환·권한 조합·실제 런타임 호환성을 보장하지 않습니다. 엔진 사전 공개판과 알려진 동적 속성 미탐지를 명시합니다.

D06 PHPStan, D10 내보내기와 자동 다운로드/갱신 UI는 보류입니다. 자체 MIT 선택은 확정했으며 실제 LICENSE·외부 고지/대응 소스 및 배포 검증은 T08 범위입니다.

## 다음 작업

**T06 입력·결과 GUI / 저성능 Work.** T05의 팩 목표 목록과 공유 결과 계약을 사용하여 승인된 최소 화면을 구현하고 테스트 데이터로 Windows 표시를 검증합니다. 실제 검사 연결은 T07입니다. T08 라이선스·고지 검증은 UI와 변경 경로가 분리되어 별도 작업으로 진행할 수 있습니다.

현재 구현과 문서는 PR #1/#2/#3 기반의 PR #4(t05-scan-service)에 있으며 main에는 아직 병합하지 않았습니다. 새 작업은 최신 main 및 네 PR의 실제 상태·유효 문서를 확인하고 최신 선행 구현을 보존해야 합니다. 사용자의 계약 승인은 병합·Release/tag 지시로 해석하지 않습니다.
