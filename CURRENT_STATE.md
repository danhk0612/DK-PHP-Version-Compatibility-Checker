# 현재 상태

## 저장소 기준

- 확인일: 2026-11-05 (한국 시간)
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

- **T06 입력·결과 GUI 완료**. [최종 Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/40966862548)에서 SDK 10.0.403 build 경고·오류 0, 실제 WPF HWND 표시/바인딩/상태·집계/긴 문구·최소 크기·정상 종료 검증 21건이 통과했습니다. 테스트 데이터 PNG임을 명시하고 캡처 여백과 최소 크기 상태 가시성을 시각 검토 후 보완했습니다. [검증 안내](tests/t06/README.md), [실측과 대표 화면](tests/t06/evidence/)을 보존했습니다. T06 당시 분리했던 실제 엔진 연결·모달 폴더 선택 조작은 아래 T07에서 검증했습니다. Windows Server 2022 CI이며 Windows 11 배포 검증을 대체하지 않습니다.

- **T07 GUI 실제 검사 연결·보호 검증 완료**. [Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/41038802788)에서 SDK 10.0.403 build 경고·오류 0, 통합 36건을 통과했습니다. 실제 폴더 선택·취소/목표/버튼/엔진·대표 24 fixture·0건/NoFiles/읽기 실패/합성 부분 결과·한국어와 설정 격리·원본 불변·응답/중복 방어를 확인했습니다. 검사 중 반복 닫기에서 실제 PHP 자식 종료와 자체 작업 정리를 기다렸고 팩 준비 중 닫기도 통과했습니다. 1000개 파일의 2000개 문제를 파일별 원값 및 마지막 grid 행까지 대조했습니다. 약 1.49초, Dispatcher 최대 관측 간격 54.72ms, GUI/PHP working set 표본 최대 약 176.85/29.34 MiB이며 해당 fixture/CI의 관측값입니다. [화면 회귀](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/41038803221) 21건도 통과했습니다. [통합 안내](tests/t07/README.md), [원본 요약·화면·해시](tests/t07/evidence/)를 보존했습니다. Windows 11 배포·완전한 런타임 호환 보장은 아닙니다.

- **T08 부분 검증, 배포 구성 판단 필요**. [Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/41941111848)에서 SDK 10.0.403 self-contained publish 경고·오류 0, 400파일 출처 매핑과 팩 865파일 inventory를 보존했습니다. 엔진 LGPL 2개의 전체 pinned source checkout을 만들고 배포 파일 302개가 원본과 일치함을 확인했습니다. 자체 MIT LICENSE, 엔진/GPL/PHP/Zend/일부 내장 고지·실제 NuGet 고지와 Microsoft 예외 조건을 준비했습니다. PHP 전체 ZIP의 네이티브 코드 고지·대응 소스/재링크 및 최종 소스 동시 제공은 미완료입니다. [LICENSING.md](LICENSING.md)의 A/B 구성 판단안과 [실측](tests/t08/evidence/)을 확인합니다. 수집기 성공을 배포 허가로 처리하지 않습니다.

## 확정한 향후 버전 확장 구조

사용자가 이후 최신 PHP도 지원 가능한 시스템을 요청했습니다. 목표 목록·구성요소 버전·검증 근거를 앱 코드와 분리한 단일 활성 엔진 팩으로 관리합니다. 새 목표는 해당 엔진 규칙과 회귀·안전성·오프라인 검증을 거쳐 팩 데이터로 추가하며 GUI/실행 코드에 초기 세 버전을 고정하지 않습니다. 호환 스키마/profile의 팩은 앱 종료 후 수동 교체합니다. 자동 다운로드/갱신 UI는 아직 보류입니다.

T04의 실제 manifest 생성·팩 정합성 검증기를 구현했습니다. C# 팩 소비·검사 실행·JSON 해석은 T05에 구현했습니다. 입력·결과 GUI와 실제 검사 연결은 T06/T07에 구현·검증했으며 팩 교체·복원·확장 통합 검증은 T10에 남아 있습니다. 새 버전이 발표됐다는 이유만으로 지원 완료를 선언하지 않습니다.

## 미구현·검증 한계

WPF 입력·결과 화면과 공유 표시 모델, C# 검사 서비스·manifest 소비를 구현했습니다. T07에서 검사 버튼과 ScanService·창 종료 정리를 연결하고 Windows Server 2022에서 통합 검증했습니다. 팩 교체 통합도 미완료입니다. 깨끗한 Windows 11 사용자 PC의 CRT·최소 배포 DLL·ZIP 사용성은 T09에 남아 있습니다. 대표 fixture 외 프로젝트의 성능은 보장하지 않습니다. 모든 PHP 변경·링크 순환·권한 조합·실제 런타임 호환성을 보장하지 않습니다. 엔진 사전 공개판과 알려진 동적 속성 미탐지를 명시합니다.

D06 PHPStan, D10 내보내기와 자동 다운로드/갱신 UI는 보류입니다. 자체 MIT LICENSE와 서드파티 고지 초안을 작성했습니다. 외부 native 구성의 라이선스/대응 소스·최종 배포 조건은 T08 부분 검증으로 남습니다.

## 다음 작업

**T08 재개 전 배포 구성 판단 / 고성능 Chat, 후속 검증은 저성능 Work.** [LICENSING.md](LICENSING.md)의 A(전체 PHP ZIP 유지) 또는 B(검사에 필요한 CLI 팩 축소 검토)를 판단합니다. 권장 검토 방향은 B이지만 아직 승인하거나 팩을 변경하지 않았습니다. B도 기본 PHP의 BCMath 등 LGPL 대응 소스 의무가 없어지는 것은 아닙니다. 선택 후 해당 범위의 네이티브 출처/고지·소스/재링크·팩 재검증 관문을 완료하고 T08 상태를 갱신합니다. T10/T09는 T08 미완료로 선행 대기입니다.

현재 작업은 PR #1~#6 기반 PR #7(t08-licenses)에 있습니다. main 미병합이며 제품/팩/버전·이름/스택을 바꾸지 않았습니다. 새 작업은 최신 원격 상태와 관련 문서를 재확인하고 사용자 구성 판단 없이 파일을 제거하지 않습니다. 병합·Release/tag는 별도 명시 지시가 필요합니다.
