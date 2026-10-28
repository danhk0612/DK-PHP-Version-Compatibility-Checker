# T01 검사 엔진 검증 보고

상태: **부분 검증**. Windows 실행 환경 차단은 해소됐습니다. 기본 엔진·24개 사례 실행은 성공했지만 T01 전체 완료 조건은 아직 충족하지 않았습니다. 제품 채택·공식 지원·라이선스 최종 판단이 아닙니다.

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

## 아직 수행하지 않은 검증

- PHP ZIP 원본 다운로드 URL·공식 해시와 실측 ZIP 해시, 깨끗한 PC의 CRT·DLL 전제
- 실제 네트워크 차단 상태의 오프라인 재검사 (이번 실행은 Composer 없이 PHPCS를 호출했지만 네트워크 자체는 활성)
- 일반 사용자 읽기 거부, 하위 폴더·확장자·제외·junction/symlink 탐색
- 외부 PHPRC/PHP_INI_SCAN_DIR 주입 감지, stdout 오염·잘못된 JSON 처리 및 실패의 세부 계약
- 대규모/부분 처리의 실제 검사 파일 수 비교와 추가 대표 변경 사례

## 다음 작업과 T02 전달

Windows 환경 부재 자체는 더 이상 차단 사유가 아닙니다. 위 미검증 항목을 T01에서 보완합니다. T02는 필수 근거 확보 후 사용자와 alpha 채택·지원 범위·탐색·결과 계약을 결정합니다. 기본 조합이 실행된다는 근거는 확보했으나 동적 속성 사례의 탐지 누락이 있어 PHP 8.2~8.4 전체 지원을 선언하지 않습니다.

[실행 수집기](probe/run_probe.py), [기대 사례](../tests/fixtures/cases.json), [Windows 안내](probe/README.md), workflow를 이용하여 동일 범위의 검사를 재현할 수 있습니다. 커밋한 lock을 기준으로 후속 workflow는 Composer install을 수행합니다. 사전 공개판 사용·공식 지원·자체 라이선스는 아직 승인하지 않았습니다.

## lock 설치 재현 확인

[Windows 실행 #2](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/40758275512)도 completed / success입니다. 소스 head `0317fc79f66256b37d57e8276483277eff076a83`의 커밋된 lock으로 Composer install을 수행하고 같은 24개 사례·보호 검사를 다시 실행했습니다. 필수 비교 실패는 0건이고 탐색용 동적 속성 미탐지도 동일합니다.

두 번째 Windows checkout의 lock SHA-256은 `1b7c997436fc8878e9cd09a637f6e4c62f86439f32251f804d6ca36e8c6e3486`입니다. 첫 실행 lock은 LF, 두 번째 checkout은 390개 CRLF 줄바꿈을 사용했습니다. 실제 두 lock을 비교하여 JSON 전체 및 줄바꿈 정규화 후 바이트가 같음을 확인했습니다. 버전·참조·메타데이터 변경은 없으며 원시 파일 해시가 같다고 기록하지 않습니다. 이 재현 성공은 남은 T01 검증 항목을 대체하지 않습니다.
