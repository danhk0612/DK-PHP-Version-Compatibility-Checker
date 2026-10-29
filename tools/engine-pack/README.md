# 고정 엔진 팩 준비 (T04)

이 폴더는 개발자용 도구 준비 절차입니다. 검사 대상 프로젝트와 분리된 **새 저장소 checkout**에서 실행합니다. PHP/Composer를 대상 프로젝트에 설치하지 않습니다. 생성물 `tools/runtime/`은 Git에서 제외하며 앱·ZIP·Release를 만들지 않습니다.

## 준비와 검증

Windows x64, PowerShell 7, Python 3.12 이상, 준비용 PHP/Composer 2가 필요합니다. 실제 검사에는 공식 ZIP의 별도 PHP만 사용합니다. Windows CI는 고정된 setup-php action으로 준비용 환경을 제공하며 준비용 PHP를 제품 runtime으로 복사하지 않습니다.

```powershell
./tools/engine-pack/Prepare-Pack.ps1
python tools/engine-pack/verify_pack.py tools/runtime
python tests/t04/test_pack_contract.py tools/runtime
```

첫 명령은 인터넷이 필요합니다. 공식 PHP ZIP을 sources.json의 정확한 URL에서 받아 SHA-256을 대조하고, 별도 Composer HOME/cache에서 제품 lock을 install·audit합니다. `--no-scripts`를 사용하며 승인한 installer 플러그인만 허용합니다. 기존 runtime은 덮어쓰지 않습니다. 실패한 준비 폴더는 활성 팩으로 사용하지 말고 보존/조사 후 새 checkout에서 재시도합니다. 의존성 변경이 필요하면 승인 버전·lock·검증 근거를 먼저 갱신하며 자동 최신 버전 선택은 하지 않습니다.

첫 명령의 실제 검증 단계는 관리자 권한의 Windows 검증 환경이 필요합니다. 생성된 PHP 실행 파일에만 임시 outbound 방화벽 규칙을 적용하고 finally에서 제거합니다. 인터넷 연결 양성 대조→차단 확인→동일 검사→규칙 제거와 연결 복구를 검증합니다. 이는 개발자용 검증의 권한이며 일반 사용자의 앱 검사에 관리자 권한을 요구하는 설계가 아닙니다. Composer를 검사 PATH에서 제외하고 PHP를 절대 경로로 실행합니다. 일반 PC에서 실행 가능한 제품인지, CRT가 없는 PC에서 어떻게 동작하는지는 T09에서 확인합니다.

전용 ruleset, `-n`, `--ignore-annotations`, `--no-cache`, `--file-list`, PHP_INI_SCAN_DIR/PHPRC 격리를 사용합니다. 공식 NTS ZIP에서는 libxml/SimpleXML/tokenizer/xmlwriter가 `-n` 상태에서도 제공되며 이 조합을 실측합니다. 선택/검사 완료 파일 대응·원문 JSON 필드와 totals, annotation 양성 대조, 대상 설정/ini/autoload/Composer scripts의 미실행, 원본 파일/디렉터리/해시 불변을 확인합니다. GUI용 파일 탐색 구현은 T05, 사용자 흐름 검증은 T07입니다.

## 생성 구조와 계약

```text
tools/runtime/
  manifest.json
  php/                 # 공식 ZIP 전체 (DLL 최소화는 하지 않음)
  engine/              # composer.json/lock, ruleset.xml, vendor/
  verification/        # 실제 results/audit, fixture, license 파일 목록
```

`sources.json`은 승인된 초기 PHP ZIP·목표 목록·팩 ID의 입력입니다. PHP 배포판 `8.4.27-1`과 PHP_VERSION `8.4.27`은 각각 runtime.version/cliVersion으로 기록합니다. 제품 Composer 파일은 T01 lock의 동일 package version/source reference를 재사용하며 프로젝트 이름 변경에 따른 content-hash만 재계산했습니다. Windows Composer validate/install로 lock 유효성을 확인합니다. 실행 시 Composer가 필요하지 않으며 준비용 Composer 자체는 팩에 포함하지 않습니다.

`manifest.json`은 **검증이 성공한 뒤에만** 생성합니다. `schemaVersion=1`, `reportProfile=phpcs4-json-v1`입니다. 목표는 major.minor 문자열 배열이며 코드 enum/XAML 목록을 만들지 않습니다. 초기 값 8.2/8.3/8.4도 실제 fixture가 있는 목표만 승인하며 버전 숫자 추가로 검증을 대체하지 않습니다.

| 필드 | 실제 형식 / 의미 |
| --- | --- |
| packId | sources.json의 초기 구성 식별자. 새 구성/목표/검증 자료에는 새 ID 사용 |
| runtime | version, cliVersion, sourceUrl, archiveSha256, executable, files |
| engine | versions(패키지명→정확한 lock 버전), lockSha256, entryPoint, ruleset, files |
| targets[] | version, verificationRef, limitations |
| verification | reportPath, files |
| files | 팩 루트 기준 POSIX 경로→소문자 SHA-256 객체. php/, engine/, verification/의 모든 파일 포함 |
| verificationRef | `verification/results.json#targets/8.2`와 같은 근거 파일·해당 목표 키 참조 |

진입점은 `php/php.exe`, `engine/vendor/squizlabs/php_codesniffer/bin/phpcs`, 전용 설정은 `engine/ruleset.xml`입니다. 경로는 팩 루트 기준으로 해석하며 shell 명령 필드는 없습니다. 각 files 목록은 실제 폴더와 정확히 일치해야 합니다. manifest 자체는 자기 해시에서 제외합니다. verification의 results는 검사 당시 runtime/engine 파일 목록을 포함하여 다른 구성의 실측을 재사용하지 못하게 대조합니다. T05는 이 계약을 C#에서 소비하며 Python을 제품 runtime 의존성으로 추가하지 않습니다.

`pack_contract.py`는 미지원 스키마/profile, 손상·누락·추가 파일, 링크/reparse point, 경로 이탈, lock/설치 버전/reference 불일치, 잘못된 목표/근거/실패 검증을 거부합니다. 해시는 **신뢰한 manifest에 대한 구성 정합성**을 확인합니다. manifest와 증거까지 함께 위조한 팩의 발행자 인증을 제공하지 않습니다. 공식 배포처와 신뢰한 저장소의 검증된 팩을 사용합니다. 새 엔진 인터페이스·새 목표는 T10 관문과 필요 시 별도 계약 변경을 거쳐야 합니다.

## T08 인계와 한계

`artifacts/t04/files.json`은 실제 팩 전체 파일·SHA-256 목록, `license-files.json`은 실제 license/copying/notice/copyright 이름 파일의 경로·해시 목록입니다. 각 원본은 runtime 내부 원래 위치에 보존합니다. PHP ZIP 전체와 vendor 전체를 포함하므로 .NET/CRT를 제외한 DLL·문서까지 열거합니다. installer도 vendor에 포함되어 있습니다. CodeSniffer.conf만 표준 등록용 상대 경로로 생성하며 외부 라이브러리 소스는 수정하지 않습니다.

이 목록은 라이선스 **원본 파일 인계**이며 T08 적합성 판정이 아닙니다. 파일명 검색만으로 DLL별 고지/대응 소스를 모두 찾았다고 선언하지 않습니다. T08은 실제 전체 목록을 기준으로 PHP/Zend/DLL·LGPL/GPL 대응 소스·MIT/BSD 고지를 확인하고 최종 publish의 .NET/CRT도 추가해야 합니다.

`artifacts/t04/`에는 raw stdout/stderr, summary/results, manifest, 파일·license 목록, 계약 거부 검증 결과를 보존합니다. CI artifact 보관은 7일이므로 핵심 실측은 저장소의 tests/t04/evidence/에 별도 보존합니다. 제품 바이너리는 Git이나 이번 artifact에 포함하지 않습니다. 앱 소비·팩 수동 교체/복원·배포 검증은 T05/T06·T10·T09에 남아 있습니다.

## 2026-10-29 공식 출처 재확인

- [공식 Windows metadata](https://downloads.php.net/~windows/releases/releases.json)의 8.4.27-1 NTS VS17 x64 URL/SHA-256이 sources.json과 일치함을 재확인했습니다.
- [PHP 8.4.27 공지](https://www.php.net/releases/8_4_27.php)는 보안 릴리스입니다. 배포 전 최신 보안 재확인은 T09에서 반복합니다.
- [PHPCSUtils 공식 보안 공지](https://github.com/PHPCSStandards/PHPCSUtils/security/advisories/GHSA-r6hr-vr92-vv28)는 1.2.3 미만 영향·1.2.3 수정을 명시합니다. 준비 시 Composer audit도 실행하며 통과를 모든 미공개 취약점 부재로 표현하지 않습니다.
- [PHPCompatibility alpha2](https://github.com/PHPCompatibility/PHPCompatibility/releases/tag/10.0.0-alpha2), [PHPCS 4.0.4](https://github.com/PHPCSStandards/PHP_CodeSniffer/releases/tag/4.0.4)의 실제 태그에 접근했습니다. 사전 공개판·기존 미탐지 한계를 유지합니다.

## 실제 T04 검증 결과

[최종 Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/40797806955)은 SDK GUI 검증과 별개인 Windows Server 2022 x64/Python 3.12.10 엔진 팩 검증입니다. 준비용 Composer는 setup 로그의 2.10.3이며 PHP 배포판 8.4.27-1/CLI 8.4.27과 승인한 네 패키지 버전을 사용했습니다.

- 기본 사례 24건(필수 비교 21건, dynamic property 관찰 3건); 기존 미탐지 한계 유지.
- annotation 3종 × 목표 3개 = 9건: ignoreFile/ignore/disable 대조는 억제, ignore-annotations에서는 파일 1개와 탐지 복구. ignoreFile 대조의 처리 파일 수는 0개입니다.
- PHP outbound 차단 및 Composer 없는 scan PATH에서 24건 동일, 연결 전/차단/복구 대조 통과.
- 외부 ini 실제 주입 대조 뒤 -n으로 차단, 대상 설정/소스 미실행, 파일·빈 디렉터리까지 전후 동일. 외부 ini 대조는 ASCII 관리 경로이며 실제 검사 대상에는 한글·공백·&를 포함했습니다.
- 미지원 schema/profile, 미검증/중복 목표, version/lock/hash/경로/검증 근거 손상, 누락된 검증 조건·대조, 추가 파일 등 17건 거부 후 원본 팩 검증 성공.

검증 head는 `c8776b443597ad2a138d7200b90c64f004bfc497`, 실제 checkout merge ref는 `64751b83e80c1b180bf8ad18875a239790e0be73`입니다. [summary](../../tests/t04/evidence/summary.json), [manifest](../../tests/t04/evidence/manifest.json), [results](../../tests/t04/evidence/results.json), [전체 파일](../../tests/t04/evidence/files.json), [license 원본 목록](../../tests/t04/evidence/license-files.json), [거부 시험](../../tests/t04/evidence/contract-tests.json), [실행/아티팩트 근거](../../tests/t04/evidence/provenance.json)를 보존했습니다. 이후 문서·실측 보존 커밋은 준비/검증 실행 코드를 변경하지 않아 재실행하지 않았습니다.

865개 파일은 준비된 팩의 포함 예정 목록입니다. 실제 ZIP 목록은 T09에서 다시 대조합니다. Composer 생성 파일은 준비 도구 버전에 따라 달라질 수 있으므로 새 준비물은 자체 manifest/hash를 검증하고 기존 실측과 비교합니다. 구성이 달라지면 새 packId와 해당 검증 자료를 사용합니다. 이번 파일명 검색에서 찾은 license 원본은 6개이며 T08은 이것 외에 각 DLL·Zend·대응 소스·Composer autoloader 고지도 검토해야 합니다.
