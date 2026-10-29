# T01 검사 엔진 검증 보고

상태: **T01 기술 검증 완료**. 기본·추가 실험을 실제 Windows에서 수행하고 탐지 누락·실행 실패와 환경 제약을 확인했습니다. 이는 엔진의 모든 사례 통과, 제품 채택·공식 지원·배포 적합성 승인이 아닙니다. 최신 유효 실험은 아래 공식 ZIP 기반 추가 검증입니다.

## 실행 근거

- 실행일: 2026-10-29 (한국 시간)
- [Windows workflow 실행 #1](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/40757585741): completed / success
- 소스 head: `67d96a1dc9b2198283d5fb6b685738de8ef8c4a5`; 실행한 PR merge ref: `21dbb91d614f43abff88a8767094a0ac89854d83`; 기준 main: `c875900a630bbd5c78b8f26d62c7a604edf8400c`
- Windows Server 2022 x64 / OS 10.0.20348 / runner image 20261026.347.1 / Python 3.12.10
- PHP CLI **8.4.27 NTS Visual C++ 2022 x64**, Composer **2.10.3**. setup-php가 확보한 실행 파일이며 ZIP의 원본 URL·공식 ZIP 해시는 아직 확보하지 않았습니다.
- php.exe 실측 SHA-256: `e40fb5aa840778d9c058cb5e5dafde5d87790edc1dc764cb409a11599049c7d3`
- 실제 해석·설치: PHPCompatibility 10.0.0-alpha2 / PHP_CodeSniffer 4.0.4 / PHPCSUtils 1.2.3 / installer v1.1.0
- [실제 Composer lock](probe/composer.lock) SHA-256: `8e096e4d39ccfbacd6251e428ec329124b776ebfb0da7f59836e887093ef5272`
- Composer validate는 경고와 함께 유효 판정, update 및 audit 종료값 0. 고정 버전 요구와 라이선스 미기재 경고는 내부 실험 manifest의 상태이며 제품 라이선스 선언이 아닙니다. audit 통과는 모든 보안 위험이 없다는 보장이 아닙니다.
- [실제 결과 JSON](probe/evidence/windows-2022-summary.json)은 workflow 로그에서 추출했습니다. 실제 경로·명령별 raw stdout/stderr·lock은 [artifact](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/40757585741/artifacts/12846882328)에도 있으며 7일 후 만료됩니다. 버전별 메시지·파일·줄·심각도와 핵심 관찰은 저장소 JSON으로 보존합니다.

## 실제 결과

**21개 기대값 비교가 모두 일치했고 탐색용 동적 속성 3개는 미탐지였습니다.** workflow의 24건 성공은 실행·기대 비교의 성공이며, 탐색용 3건을 호환성 탐지 성공으로 포함하지 않습니다.

| 사례 | 대상 8.2 | 대상 8.3 | 대상 8.4 |
| --- | --- | --- | --- |
| mysql_query 제거 함수 | ERROR 2건, exit 2 | ERROR 2건, exit 2 | ERROR 2건, exit 2 |
| mysqli 대조 | 0건, exit 0 | 0건, exit 0 | 0건, exit 0 |
| get_class 인자 생략 | 0건, exit 0 | WARNING 1건, exit 2 | WARNING 1건, exit 2 |
| get_class 인자 명시 | 0건, exit 0 | 0건, exit 0 | 0건, exit 0 |
| 암묵적 nullable | 0건, exit 0 | 0건, exit 0 | WARNING 1건, exit 2 |
| 명시적 nullable | 0건, exit 0 | 0건, exit 0 | 0건, exit 0 |
| 동적 속성(탐색) | **미탐지** | **미탐지** | **미탐지** |
| 선언된 속성 대조 | 0건, exit 0 | 0건, exit 0 | 0건, exit 0 |

mysql_query 한 줄에 제거 확장과 제거 함수의 서로 다른 규칙이 각각 보고됐습니다. 임의 중복 제거를 하지 않았습니다. mysql은 PHP 7.0부터 제거된 문제이며 8.2 신규 제거로 해석하지 않습니다. 동적 속성 예제는 PHP 8.2 변경에 해당해도 이 조합에서 탐지되지 않았으므로 사용자 안내와 지원 한계에 반영해야 합니다. 다른 모든 동적 속성을 절대 탐지하지 못한다고 일반화하지 않습니다.

모든 단일 파일 JSON은 files 1개이며 messages와 totals의 오류/경고 수가 일치했습니다. file/line/column/message/type/source/severity 필드를 수집했습니다. 이는 대규모 폴더·읽기 실패에서 파일 수 계약이 검증됐다는 뜻은 아닙니다.

| 추가 검증 | 실제 결과와 한계 |
| --- | --- |
| 성공·탐지 전후 입력 보호 | fixture 파일·디렉터리 목록 및 SHA-256 동일 |
| 실패 전후 입력 보호 | 빈 폴더·잘못된 ruleset 검사 후 목록 및 해시 동일 |
| 빈 폴더 | exit 16, 정상 JSON 없음. 완료 0건으로 취급하면 안 됨 |
| 잘못된 ruleset | exit 16, 정상 JSON 없음 |
| 한글·공백·& 경로 | 개별 파일 정상 검사, exit 0, 원본 불변 |
| 대상 설정·소스 격리 사례 | 대상에 phpcs.xml/bootstrap/vendor autoload/Composer scripts/php.ini를 둔 상태에서 명시적 관리 ruleset과 -n으로 개별 subject.php 검사 성공. throw가 있는 대상 코드를 실행하지 않았고 원본 불변 |
| -n 필수 확장 | 해당 Windows PHP에서 libxml/SimpleXML/tokenizer/xmlwriter 활성 확인 |

격리 사례는 위 조합·명령·단일 파일에서의 근거입니다. 외부 환경 설정 주입, 전체 폴더 탐색 또는 OS sandbox를 전부 검증한 것은 아닙니다. 정적 검사 성공은 실제 서비스·DB·외부 라이브러리 런타임 정상 동작을 보장하지 않습니다.

## 공식 자료와 보안 확인

2026-10-29 다음 자료를 열어 확인했다. 자료 확인은 실행이나 전체 보안 감사가 아니다.

- [PHPCompatibility 릴리스](https://github.com/PHPCompatibility/PHPCompatibility/releases): 10.0.0-alpha2는 2025-11-28 사전 공개판이다. [태그 manifest](https://github.com/PHPCompatibility/PHPCompatibility/blob/10.0.0-alpha2/composer.json)의 요구 범위는 PHPCS `^3.13.3 || ^4.0`, Utils `^1.1.2`; 기본 설치·해석은 위 Windows 실행에서 검증했다.
- [PHPCS 릴리스](https://github.com/PHPCSStandards/PHP_CodeSniffer/releases): 4.0.4는 4.0.2의 보안 수정 내용을 포함한다고 설명한다. [GHSA-hmqg-cxww-wqhq](https://github.com/advisories/GHSA-hmqg-cxww-wqhq)는 특정 blame 보고서에서 파일명 메타문자를 통한 명령 실행을 다루며 일반 JSON 보고서의 실행 안전성을 증명하지 않는다.
- [Utils 보안 공지 GHSA-r6hr-vr92-vv28](https://github.com/advisories/GHSA-r6hr-vr92-vv28): `>=1.0.0-alpha1, <1.2.3`은 특정 추상 sniff 메서드를 호출하는 경우 신뢰하지 않는 입력으로 임의 코드 실행 위험이 있으며 1.2.3이 수정판이다. 기존 후보 1.1.2가 영향 범위에 포함된다. PHPCompatibility의 해당 경로 도달 여부는 확인하지 않았다. [릴리스](https://github.com/PHPCSStandards/PHPCSUtils/releases)는 1.2.3을 보안 릴리스로 명시한다. 수정판은 위 선언된 버전 범위에는 들어가지만 기본 의존성 해석·실행은 위 Windows 결과에서 확인했으나 전체 입력 안전성을 증명한 것은 아니다.
- [PHP Windows 다운로드](https://www.php.net/downloads.php?os=windows&osvariant=windows-downloads): x64 및 CLI용 NTS 안내와 Visual C++ 재배포 패키지 요구를 확인했다. 최종 제품 ZIP은 미선정이며 이번 실행에서는 setup-php가 확보한 PHP CLI를 사용했다. [지원 기간](https://www.php.net/supported-versions.php)은 8.2 보안 지원 2026-12-31, 8.3 2027-12-31, 8.4 2028-12-31까지로 표시한다. 실행 PHP 버전과 검사 대상 버전은 별개다.
- PHP 공식 변경: [8.2 동적 속성](https://www.php.net/manual/en/migration82.deprecated.php), [8.3 인자 없는 get_class](https://www.php.net/manual/en/migration83.deprecated.php), [8.4 암묵적 nullable](https://www.php.net/manual/en/migration84.deprecated.php). 이 문서는 PHP 동작 근거일 뿐 alpha2의 탐지 근거가 아니다.

## 공식 ZIP 기반 추가 검증

[최종 Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/40761510481)은 completed / success입니다. 소스 head는 `4c3572d99576a2bfecc44fb61c6b1acc5c2fbd5b`, 실행 merge ref는 `d73b9e174ce39e18dcfcc02c22a8727eb3afa35e`입니다. [기본·추가 실측 JSON](T01_WINDOWS_RESULTS.json)에 결과를 보존했습니다. files 배열의 경로만 저장소 상대 경로로 정규화했으며 원래 raw 출력·명령은 해당 workflow artifact에 있습니다.

최신 검사 runtime은 setup-php의 실행 파일이 아니라 공식 배포 ZIP에서 준비했습니다. setup-php는 Composer 준비용이고, 실제 분석 프로세스는 아래 공식 파일을 사용했습니다.

- [공식 metadata](https://downloads.php.net/~windows/releases/releases.json): 배포 버전 **8.4.27-1**, x64 NTS VS17
- [실제 다운로드 ZIP](https://downloads.php.net/~windows/releases/php-8.4.27-1-nts-Win32-vs17-x64.zip)
- 공식·실측 ZIP SHA-256 일치: `dbae154a0a89791f36c3d48e68bae797878333e7d6cb5721e74617de59b63ab7`
- 실제 분석 php.exe SHA-256: `69f59f1e561c24c5204800ad8ecc1585d79dfa03d07a644d66f4241c86b5991e`
- 공식 ZIP 내 php.exe 및 DLL과 실제 분석 runtime의 파일 해시가 일치했습니다. CLI 표시 버전은 8.4.27이며 배포 revision -1과 구별합니다.
- Windows Server 2022 x64 / runner image 20261022.344.1. 필수 libxml/SimpleXML/tokenizer/xmlwriter는 -n에서 유효했습니다. 소스·도구 원본은 수정하지 않았습니다.

기존 setup-php 실행 파일은 같은 CLI 버전 표시라도 공식 revision -1 ZIP의 실행 파일과 달랐습니다. [대조 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/40761046871)에서 ZIP 해시는 일치했지만 실행 파일 비교가 실패하여 workflow를 실패로 남겼고, 공식 ZIP으로 검증 runtime을 준비한 뒤 전체 실험을 다시 수행했습니다. 이를 변조의 증거로 해석하지 않으며, 버전 문자열만으로 바이너리 출처·동일성을 보장할 수 없다는 근거로 남깁니다.

| 추가 항목 | 공식 runtime에서의 실제 결과 | 판단 |
| --- | --- | --- |
| 온라인 기본 24개 | 기존 결과와 동일. 21개 필수 기대 비교 일치, 동적 속성 3개 미탐지 | 탐지 한계 유지 |
| PHP 외부 통신 차단 | 차단 전 repo.packagist.org:443 접속 성공, PHP 실행 파일 전용 outbound block 후 접속 실패. 같은 상태에서 Composer 없이 24개 결과 모두 동일, 입력 불변 | 검사 프로세스의 오프라인 실행 확인. 전체 호스트 네트워크 차단은 아님 |
| 방화벽 복구 | 임시 PHP 전용 규칙 제거 후 같은 접속 다시 성공 | runner·업로드 프로세스는 차단하지 않음 |
| 읽기 거부 | 자체 fixture ACL로 실제 PHP 읽기 실패 확인. 엔진 exit 255, 정상 JSON 없음 | 16 외의 비정상 종료도 실패로 처리해야 함 |
| 읽기 거부 복구 | 임시 deny ACE 제거 후 원본 해시 동일 | 일반 사용자 계정 전체 동작 시험은 별도 |
| PHPRC/INI scan 주입 | -n 없는 관리용 양성 대조는 주입 마커 출력, -n 엔진 검사는 정상 JSON·0건·원본 불변 | 주입이 실제 활성인 대조와 차단 확인 |
| stdout 오염 | 관리용 PHPCS bootstrap으로 마커를 출력하면 수집기의 JSON 판정 실패 | 프로세스 exit만으로 검사 완료 판정 불가 |
| 하위 포함 PHP | 예상 131개 = JSON files 131개, exit 0 | 대표 batch에서 실제 파일 수 일치 |
| 현재 폴더만(-l) | 1개 | 탐색 옵션 후보 검증이며 최종 UI 정책 아님 |
| php,inc 확장자 | 132개 | TXT는 제외 |
| skip 폴더 제외 | 130개 | --ignore 후보 검증 |
| junction 직접 탐색 | exit 255, 정상 JSON 없음. junction을 fopen하려다 Permission denied로 비정상 종료 | **정상 탐색으로 합격 처리하지 않음** |
| symlink 직접 탐색 | 선택한 폴더 밖 outside.php까지 JSON에 포함 | **선택 폴더 경계가 자동 보장되지 않음** |
| 링크 제외 명시적 file-list | junction/symlink 각각 131개만 정상 검사. 외부 파일 미포함·원본 불변 | T02의 파일 탐색 계약에 추천할 방식 |

추가 수집기의 scope_passed는 위 관찰·비교 조건이 충족됐다는 뜻입니다. junction_observation의 passed=true는 알려진 exit 255 실패 양상을 재현했다는 의미이며 engine_succeeded=false를 함께 기록합니다. 엔진의 junction 처리나 동적 속성 탐지가 정상이라는 뜻이 아닙니다. 스니프나 외부 엔진 코드는 변경하지 않았습니다.

첫 추가 실행에서는 검증 도구가 --extensions를 두 번 전달하여 php,inc 기대 비교가 실패했습니다. [실패 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/40760523515)의 근거를 보존하고, 검증 도구가 확장자 인자를 한 번만 전달하도록 고친 뒤 132개를 실제 확인했습니다. 기대 파일 수를 줄이지 않았습니다. 같은 실행에서 확인한 junction 실패도 숨기지 않고 최종 실험에서 재현·회피 방식을 구분했습니다.

## 완료 조건 대조와 남은 범위

| TASKS T01 조건 | 확보한 근거 |
| --- | --- |
| 1. 환경·출처·해시·lock·명령 | 공식 ZIP·실측 SHA-256, 실제 php.exe/DLL 대조, Composer lock, workflow·실행 인자 artifact |
| 2. 버전별 양성·음성 | 8개 소스×3개 대상. 제거 함수·인자 생략·nullable·동적 속성의 대조와 미탐지 기록 |
| 3. JSON·종료·실패·파일 수 | 0/2/16/255, 필드·총수·단일 및 batch 파일 수, 빈 폴더/불량 ruleset/읽기 거부/출력 오염 |
| 4. 경로·탐색 | 한글/공백/& 경로, 재귀/현재 폴더/확장자/제외/junction/symlink·명시적 file-list |
| 5. 원본·설정 격리 | 대상 설정·bootstrap·autoload·Composer scripts·PHP 소스 비실행 감지, PHPRC/scan dir 양성 대조, 파일·디렉터리/해시 비교 |
| 6. 오프라인·환경 제약 | PHP 프로세스 outbound 차단 양성/음성 대조·24개 재검사·복구, 필수 확장·CRT 전제와 제약 기록 |
| 7. 지원/누락·추천 | 위 표와 아래 T01 조사 당시 추천안. 현재 채택 계약은 ARCHITECTURE.md의 T02 확정 내용 참조 |

깨끗한 일반 사용자 Windows PC의 CRT 미설치 동작·최소 DLL 배포 구성·GUI 응답·대규모 제품 성능은 아직 검증하지 않았으며 T04/T07/T09에서 실제 배포 구성으로 수행합니다. 131개 실험의 시간은 보편적 성능 보장이 아닙니다. 임의 링크 순환, 모든 경로 문자·권한 조합, 모든 PHP 변경, 실제 서비스의 런타임 호환성을 완전 검증한 것으로 간주하지 않습니다. T01은 이러한 한계와 환경 제약을 남긴 기술 검증이며 제품 완료가 아닙니다.

## T01 조사 당시 T02 추천안

2026-10-29 사용자가 T02 권장안과 향후 목표 버전 확장을 승인했습니다. 현재 확정 계약은 [ARCHITECTURE.md](../ARCHITECTURE.md)를 따릅니다. 아래는 실측에서 도출한 추천 근거이며 제품 구현·배포 검증 완료를 뜻하지 않습니다.

- 엔진 후보: PHPCompatibility alpha2 + PHPCS 4.0.4 + Utils 1.2.3. 안정판 선택으로 8.x 검사 범위를 오해하게 하지 않으며 사전 공개판·미탐지 한계를 표시합니다.
- 실행 PHP 후보: 이번에 검증한 공식 8.4.27-1 x64 NTS 계열. 제품 내장판은 보안 상태를 다시 확인해 결정합니다.
- 탐색: 초기에는 프로그램이 승인한 PHP 파일 목록을 만들고 링크/junction을 기본 제외하는 안을 우선 추천합니다. 사용자 결정 없이 구현·기본값으로 확정하지 않습니다.
- 결과: 엔진 규칙·ERROR/WARNING·severity 원값 보존. 같은 줄의 다른 규칙을 임의 중복 제거하지 않습니다. exit 0이라도 JSON 불량이면 실패, 16/255와 부분 처리도 완료 0건으로 표시하지 않습니다.
- 지원 표시: 선택 대상 8.2/8.3/8.4의 정적 문제 탐지 범위로 설명하되 완전 호환 보장으로 표시하지 않습니다. 이번 동적 속성 사례 미탐지를 알려진 한계로 명시합니다.
- 나머지 D04/D05/D11~D13은 기존 WPF/.NET 10·간단한 ZIP 배포·자체 MIT 목표 등 기준안을 사용자와 확정합니다. PHPStan·업데이트·내보내기를 선행 구현하지 않습니다.

T02는 T01 PR #1의 현재 branch 근거를 읽어 진행할 수 있으나 main에는 아직 병합하지 않았습니다. 제품 코드·최종 라이선스·배포·Release/tag는 이번 작업에서 수행하지 않았습니다.

## lock 설치 재현 확인

[Windows 실행 #2](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/40758275512)도 completed / success입니다. 소스 head `0317fc79f66256b37d57e8276483277eff076a83`의 커밋된 lock으로 Composer install을 수행하고 같은 24개 사례·보호 검사를 다시 실행했습니다. 필수 비교 실패는 0건이고 탐색용 동적 속성 미탐지도 동일합니다.

두 번째 Windows checkout의 lock SHA-256은 `1b7c997436fc8878e9cd09a637f6e4c62f86439f32251f804d6ca36e8c6e3486`입니다. 첫 실행 lock은 LF, 두 번째 checkout은 390개 CRLF 줄바꿈을 사용했습니다. 실제 두 lock을 비교하여 JSON 전체 및 줄바꿈 정규화 후 바이트가 같음을 확인했습니다. 버전·참조·메타데이터 변경은 없으며 원시 파일 해시가 같다고 기록하지 않습니다. 이 재현 성공은 남은 T01 검증 항목을 대체하지 않습니다.
