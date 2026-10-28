# T01 검사 엔진 검증 보고

상태: **차단**. 2026-10-29 (한국 시간) 공식 자료 조사만 수행했고 실제 검사 실행은 하지 못했다. 기준 main `c875900a630bbd5c78b8f26d62c7a604edf8400c`. 제품 채택·공식 지원·라이선스 판단이 아니다.

## 환경과 차단

작업 환경은 Linux x86_64 (`uname -a`: Linux 6.18.55); `command -v`로 php, composer, wine, wine64, powershell, pwsh, docker, podman이 모두 없음을 확인했다. 실제 Windows x64 호스트는 제공되지 않았다. 따라서 Windows 버전, 실제 PHP/Composer/엔진 버전, PHP ZIP과 의존성의 실측 SHA-256, Composer lock, 필수 DLL/확장의 실제 목록은 **없음**. 도구 다운로드·설치·fixture 실행도 하지 않았다. Composer가 생성하지 않은 lock이나 실제 실행하지 않은 JSON을 만들어 검증 결과로 제시하지 않는다. 아래의 기대는 검증 목표이며 실제 결과가 아니다.

## 공식 자료와 보안 확인

2026-10-29 다음 자료를 열어 확인했다. 자료 확인은 실행이나 전체 보안 감사가 아니다.

- [PHPCompatibility 릴리스](https://github.com/PHPCompatibility/PHPCompatibility/releases): 10.0.0-alpha2는 2025-11-28 사전 공개판이다. [태그 manifest](https://github.com/PHPCompatibility/PHPCompatibility/blob/10.0.0-alpha2/composer.json)의 요구 범위는 PHPCS `^3.13.3 || ^4.0`, Utils `^1.1.2`; 설치·해석 결과는 미검증.
- [PHPCS 릴리스](https://github.com/PHPCSStandards/PHP_CodeSniffer/releases): 4.0.4는 4.0.2의 보안 수정 내용을 포함한다고 설명한다. [GHSA-hmqg-cxww-wqhq](https://github.com/advisories/GHSA-hmqg-cxww-wqhq)는 특정 blame 보고서에서 파일명 메타문자를 통한 명령 실행을 다루며 일반 JSON 보고서의 실행 안전성을 증명하지 않는다.
- [Utils 보안 공지 GHSA-r6hr-vr92-vv28](https://github.com/advisories/GHSA-r6hr-vr92-vv28): `>=1.0.0-alpha1, <1.2.3`은 특정 추상 sniff 메서드를 호출하는 경우 신뢰하지 않는 입력으로 임의 코드 실행 위험이 있으며 1.2.3이 수정판이다. 기존 후보 1.1.2가 영향 범위에 포함된다. PHPCompatibility의 해당 경로 도달 여부는 확인하지 않았다. [릴리스](https://github.com/PHPCSStandards/PHPCSUtils/releases)는 1.2.3을 보안 릴리스로 명시한다. 수정판은 위 선언된 버전 범위에는 들어가지만 실제 의존성 해석·실행은 미검증이다.
- [PHP Windows 다운로드](https://www.php.net/downloads.php?os=windows&osvariant=windows-downloads): x64 및 CLI용 NTS 안내와 Visual C++ 재배포 패키지 요구를 확인했다. 실제 사용할 ZIP은 선택·다운로드하지 않았다. [지원 기간](https://www.php.net/supported-versions.php)은 8.2 보안 지원 2026-12-31, 8.3 2027-12-31, 8.4 2028-12-31까지로 표시한다. 실행 PHP 버전과 검사 대상 버전은 별개다.
- PHP 공식 변경: [8.2 동적 속성](https://www.php.net/manual/en/migration82.deprecated.php), [8.3 인자 없는 get_class](https://www.php.net/manual/en/migration83.deprecated.php), [8.4 암묵적 nullable](https://www.php.net/manual/en/migration84.deprecated.php). 이 문서는 PHP 동작 근거일 뿐 alpha2의 탐지 근거가 아니다.

## 결과와 남은 재현 작업

| 대상/항목 | 기대 및 확인할 비교 | 실제 결과 |
| --- | --- | --- |
| 7.4→8.2 | 제거된 mysql 계열과 동적 속성 양성/음성; 규칙의 태그별 적용 확인 | 실행·탐지·누락 모두 미확인 |
| 8.2→8.3 | 클래스 내부 인자 없는 get_class와 인자 있는 대조 | 실행·탐지·누락 모두 미확인 |
| 8.2→8.4 | `T $x = null`과 `?T $x = null` 대조 | 실행·탐지·누락 모두 미확인 |
| JSON·종료·파일 수 | 0건, 오류/경고, 빈 폴더, 잘못된 설정, 읽기 불가, 출력 오염 각각의 stdout/stderr, exit, JSON file/line/type/source/severity 및 실제 처리 파일 수 대조 | 샘플·종료 코드·파일 수 모두 미측정. 소스상 종료 코드 및 JSON 초안은 ARCHITECTURE.md 참조; 실행 검증 아님 |
| 경로·탐색 | 한글·공백·메타문자 경로, 재귀·확장자·제외·symlink/junction 후보와 실제 처리 목록 비교 | 미실행; 정책 미결정 |
| 대상 보호 | 성공·탐지·실패 전후 파일과 디렉터리 목록 및 SHA-256 동일; 대상 설정·bootstrap·autoload·Composer·외부 PHP 설정과 대상 코드의 부작용 감지 | 미실행; 차단을 보장할 수 없음 |
| 오프라인 | 도구 준비 후 네트워크와 Composer 없이 실행; CRT·확장·DLL을 별도 확인 | 미실행 |

탐지/누락 표에서 각 버전의 모든 사례는 **미검증**이며 0건이나 미지원으로 해석하지 않는다. 정적 분석은 실제 서비스·DB·외부 라이브러리의 런타임 호환을 보장하지 않는다.

## T02에 전달할 자료와 다음 실행

alpha2는 최신 PHP 변경을 다루는 후보이나 사전 공개판이며 완전한 탐지와 안정성은 입증되지 않았다. 수정된 Utils 1.2.3 조합을 격리된 Windows x64 검증 환경에서 우선 재검토할 것을 추천한다. 이는 버전 변경 승인이나 제품 채택이 아니다. 알려진 취약 후보를 신뢰하지 않는 입력에 사용하지 말고, 공지의 도달 조건과 추가 보안 공지를 확인한다.

Windows 환경에서 도구 관리 영역과 별도의 자체 작성 fixture를 준비한 뒤 실제 PHP ZIP URL·실측 SHA-256·버전·CRT/확장, Composer 버전·manifest·생성된 lock·설치 버전을 기록한다. 대상 밖의 명시적 ruleset/ini와 인자 분리 실행을 검증하고, 각 명령의 작업 경로·환경·기대/실제 stdout/stderr/종료 코드 및 전후 목록/해시를 보존한다. 대상의 코드와 설정을 실행하거나 대상에 도구·캐시·결과를 쓰지 않는다. 오프라인 시험은 준비 후 네트워크 차단 상태를 확인해 수행한다. 이 단계는 아직 실행 가능한 완성 절차가 아니며, 정확한 명령·fixture·lock 및 결과가 확보되어야 T01 완료를 판정할 수 있다.
