# T20 — GitHub 엔진 채널 및 로컬 배포 후보

T16~T19에서 작성한 엔진 팩 관리 기능의 **배포 채널 준비** 단계입니다. 현재 상태는 배포 후보이며, GitHub Release/tag를 만들거나 기존 v0.1.0 배포물과 main을 수정하지 않습니다.

## 사전 확인

- 기존 기본 PHP CLI 8.4.27·고정 PHPCS/PHPCompatibility를 동봉한 다음 앱 후보에 PHP 7.4~8.5 검증 팩을 포함합니다.
- 제품에서 허용하는 단일 카탈로그 URL은 `https://raw.githubusercontent.com/danhk0612/DK-PHP-Version-Compatibility-Checker/main/tools/engine-pack/catalog-v2.json`입니다. `EnginePackInstaller`의 URL 출처 제한을 유지합니다.
- 최초 카탈로그는 **`{"schemaVersion":2,"packs":[]}`**입니다. 게시되지 않은 엔진 팩 ZIP 링크/해시를 만들어 넣지 않습니다. 이 상태에서는 수동 조회는 가능하나 새로운 팩은 제공되지 않습니다.
- 추후 실제 추가 엔진 팩을 공개하려면 정확한 `runtime/` 구성, manifest 검증, 대응 소스·고지 ZIP, 태그 고정 Release 자산의 정확한 SHA/크기, 신뢰 카탈로그 항목과 순서를 먼저 준비해야 합니다.

## 작업 브랜치 시험

`.github/workflows/t20-channel-candidate.yml`의 Windows x64 단계에서 카탈로그 v2가 비어 있는 것을 확인하고 `tools/packaging/Prepare-LocalTest.ps1`을 통해 프로그램 후보 ZIP·대응 소스/고지 ZIP·paired-candidate.json을 생성 및 대조합니다. 산출 ZIP 자체는 공개하거나 임의 Actions artifact로 배포하지 않고 검사 결과 메타데이터만 제한적으로 보존합니다.

## T20 완료 전 필수 관문

1. T17 신규 목표 PHP 7.4/8.0/8.1 및 8.2~8.5 실제 Windows 회귀 통과. T18 같은 목표 엔진 갱신·이전/기본 팩 복원, T19 별도 WPF 관리 창 테스트 통과.
2. 패키징 후보와 대응 소스·고지의 SHA 및 T08/T09 고지·재링크·배포 자료 요구를 재대조. 공개 배포 적합성은 별도 검토.
3. **깨끗한 Windows 11 x64** 사용자 권한에서 .NET Desktop Runtime 및 필요한 CRT 의존 조건, 실제 GUI 시작·검사·원본 불변, 오프라인 동작 검증.
4. 새 엔진 팩 ZIP 및 대응 소스·고지가 고정 태그의 GitHub Release에 **별도 게시 승인**에 따라 준비된 뒤 실제 익명 HTTPS 다운로드/리디렉션/해시·설치·재시작·복원을 검증.
5. 최초 새 앱 릴리스에서는 온라인 채널 활성화가 필요합니다. 이후 동일 EXE 해시를 유지한 팩 단독 업데이트를 실제 검증합니다.

**현재 불가:** main에 카탈로그가 아직 없고 새 엔진 팩 Release 자산도 승인·게시되지 않았으므로, 원격 실서버 성공 또는 T20 완료를 선언할 수 없습니다. 빈 카탈로그는 배포 대상 가짜 팩을 만들지 않기 위한 임시 상태입니다.

게시·병합·tag/Release 생성은 이 PR에서 실행하지 않습니다.
