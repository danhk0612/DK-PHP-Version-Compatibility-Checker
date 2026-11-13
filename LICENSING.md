# T08 배포 라이선스 검토

## 현재 판정

**부분 검증 — 승인된 최소 CLI 구성도 공개 배포 승인 상태가 아닙니다.** 자체 코드의 MIT, 엔진 원본, Windows publish 구성·출처와 엔진 LGPL 소스 대조를 준비했습니다. PHP 네이티브 코드의 정확한 포함·라이선스·대응 소스/재링크 자료와 최종 배포 시의 소스 동시 제공은 남아 있습니다. 사용자가 승인한 B안으로 팩의 PHP 파일 구성을 축소했습니다. 제품 C# 코드·엔진 버전·목표·이름·스택은 변경하지 않았습니다.

확인 기준일은 2026-11-05이며 [고지 초안](THIRD-PARTY-NOTICES.md), [실측](tests/t08/evidence/summary.json), [실행·해시 근거](tests/t08/evidence/provenance.json)를 함께 봅니다. 자료 수집 성공을 전체 라이선스 적합성의 합격으로 승격하지 않습니다.

## 실제 대조한 구성

[Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/41941111848)은 Windows Server 2022 x64 / SDK 10.0.403에서 self-contained publish 경고 0·오류 0을 확인했습니다. 제품 파일 400개를 NuGet 원본 payload 또는 자체 앱/SDK 생성물에 모두 연결했고 미매핑 파일은 0개입니다. 엔진 팩 865개 파일도 경로·SHA-256·PE 메타데이터와 함께 보존했습니다. 이는 깨끗한 Windows 11 배포 검증이 아닙니다.

| 영역 | 확인한 근거 | 아직 남은 관문 |
| --- | --- | --- |
| 자체 C#/문서/관리 스크립트 | 승인한 루트 MIT LICENSE | 실제 배포에 자체 고지 포함 |
| Composer 4패키지와 생성 autoloader | 고정 source commit·원본 5개·T04 해시 일치 | 최종 팩 변경 시 재대조 |
| PHPCompatibility/PHPCSUtils 대응 소스 | 전체 pinned Git checkout 783/516개 tracked 파일, 배포 파일 244/58개 모두 바이트 일치, source ZIP 2개 생성 | 같은 최종 배포와 함께 소스 ZIP·전문·재구성 절차를 지속 제공 |
| .NET runtime/WindowsDesktop/host 10.0.14 | NuGet archive 해시·400파일 매핑·MIT/고지·Microsoft 파일별 예외 | 최종 ZIP의 Microsoft 보호 조건·고지/사용자 안내 이행·최종 파일 대조 |
| PHP/Zend 및 내장 구성 원문 | ZIP license/readme, 태그 PHP 3.01/Zend 2.00, 내장 라이선스/고지 7개 | 최종 적용 경로와 네이티브 전체 고지/소스 연결 |
| PHP 네이티브 DLL/정적 코드 | in-ZIP SBOM 102개 후보, EXE/DLL 64개 import 관측·모듈/CRT 정보 | 후보의 실제 포함·정적 링크·버전/소스 대응, native LGPL 소스/재링크 자료 검증 |

모든 파일은 [publish-files.json](tests/t08/evidence/publish-files.json), [pack-files.json](tests/t08/evidence/pack-files.json)에 있습니다. NuGet의 패키지 MIT 선언만으로 Windows용 네이티브 예외까지 MIT라고 표시하지 않습니다. Microsoft .NET Library License 적용 5개와 Windows SDK License 적용 1개를 [파일별 대조](tests/t08/evidence/publish-license-map.json)에 구분했습니다. 원본 고지와 보충한 소스 고지의 출처도 구분합니다.

## 엔진 소스와 수정·교체

Composer dist는 `.gitattributes`의 export-ignore 때문에 tests/build/설정 파일을 제외합니다. 수집기는 `git archive` 대신 lock의 전체 고정 checkout에서 모든 tracked 파일을 열거하고 실제 Git blob ID와 바이트를 대조해 ZIP을 만듭니다. 배포 vendor 파일 302개도 동일 경로/해시를 모두 확인했습니다. [원본 source 비교 근거](tests/t08/evidence/source-checks/)와 [커밋·ZIP 해시](tests/t08/evidence/corresponding-source.json)를 보존했습니다.

이 소스 ZIP은 CI에서 생성·검증했지만 7일 artifact만으로 최종 소스 제공 의무가 끝난 것은 아닙니다. 최종 배포 시 같은 다운로드 위치에서 대응 소스·license·NOTICE·재현/재구성 방법을 함께 지속 제공하고 해시를 고정해야 합니다. T09에서는 새로 만든 최종 배포 구성과 다시 대조합니다.

GUI는 별도 프로세스의 PHP/PHPCS에 인자를 전달하고 JSON을 해석합니다. GUI에 엔진 코드를 복제하거나 링크하지 않았습니다. 자체 MIT와 외부 LGPL을 구분하고, LGPL 코드 수정 및 수정 디버깅에 필요한 권리를 금지하지 않습니다. 팩의 manifest/report 해시는 편집 가능한 정합성 자료이며 발행자 서명이 아닙니다. 변경한 팩은 자체 검증 결과와 파일/보고서 해시를 다시 생성해야 하며 기존 검증 결과를 사실과 다르게 재사용하면 안 됩니다. T10의 팩 제작/교체 절차는 이 권리와 검증 기준을 함께 유지해야 합니다.

## 보존한 Full 기준의 PHP 네이티브 검토

기존 Full 기준은 공식 NTS ZIP 전체를 그대로 포함합니다. GLib DLL 3개, libenchant2와 Hunspell plugin, php_gmp/mbstring/dba 확장 등 실제 배포 파일이 있습니다. 로딩되지 않는 확장도 파일을 배포한다면 고지·소스 검토 범위입니다.

환경/서버 변수를 제외한 제한된 PHP 정보에서 BCMath가 기본 활성화됨을 확인했습니다. 따라서 최소 CLI 파일만 남겨도 PHP에 정적으로 포함된 LGPL 코드의 대응 소스 검토가 없어지지 않습니다. libbcmath와 libmbfl의 원문은 LGPL-2.1입니다. SBOM의 GLib·Enchant·MPIR·libintl/iconv/qdbm 등 후보는 [pending 목록](tests/t08/evidence/php-native-pending.json)에 버전·원본 source commit과 함께 기록했습니다. FreeType나 zstd의 대안 라이선스는 OR 조건을 보존하며 전체 앱이 무조건 GPL이라고 단정하지 않습니다.

공식 in-ZIP CDX와 나중에 제공되는 웹 sidecar는 바이트가 달랐습니다. 이 보고서의 파생 목록은 **실제 ZIP에서 추출한 CDX**를 기준으로 합니다. SBOM은 Apache/APR/Lua 같은 후보와 중복 버전도 포함하므로 102개를 실제 배포 구성 요소 수로 부르지 않습니다. SHA·파일/import·빌드 설정과 대조해야 합니다. DLL import만으로 정적 라이브러리 부재나 소스 완전성을 증명할 수 없습니다.

`deplister`는 PHP 소스의 BindImageEx BOOL 반환을 그대로 사용하므로 exit 1은 TRUE입니다. [원문](https://github.com/php/php-src/blob/php-8.4.27/win32/build/deplister.c)과 [import 관측](tests/t08/evidence/php-imports.json)을 함께 보존했습니다. 64개 이미지 중 Firebird PDO 확장에서 `fbclient.dll,NOTFOUND`가 관측됐습니다. 이를 숨기거나 전체 네이티브 의존성 충족으로 처리하지 않습니다. 이 선택 확장은 기본 PHPCS 검사에 로딩되지 않으며 Firebird client 파일은 현재 배포물에 없습니다.

## 배포 전 해소할 조건

1. 실제 포함되는 네이티브 구성마다 고정된 소스/버전·라이선스 전문·저작권 고지를 파일 및 정적 포함 코드에 연결합니다. PHP README가 외부 소스 LICENSE를 가리키는 부분도 전문을 포함합니다. 현재 확보한 내장 고지만으로 외부 네이티브 구성 전부를 충족한다고 보지 않습니다.
2. PHP의 LGPL 내장 코드와 실제 포함 native LGPL에 대해 변경 사항·빌드 스크립트·필요 대응 application code 등 선택한 라이선스 경로의 소스/재링크 자료를 확보하고 검증합니다. Composer 2개 source ZIP으로 이를 대신하지 않습니다.
3. 현재 보존한 PHP 3.01/Zend 2.00 고지를 provenance로 유지하고 사용자 승인된 PHP v4/Zend v3 후속 선택을 명시적으로 기록합니다. PHP 자체의 후속 라이선스 선택이 독립 native 라이브러리까지 재허가하지는 않습니다.
4. Microsoft 제한 바이너리는 자체 MIT/일반 LGPL 수정 권한의 적용 범위에서 구분하고, 해당 조건의 재배포·사용자/배포자 보호 요건을 최종 배포에 반영합니다. PHP용 시스템 CRT를 별도로 복사하지 않습니다.
5. 최종 배포물, 대응 소스, 전체 고지 및 해시를 함께 고정·제공합니다. T08 미해결 상태에서 T10/T09의 선행 조건을 합격 처리하지 않습니다.

## 승인된 최소 CLI 구성 (B안)

2026-11-05 사용자가 B안을 승인했습니다. 기본 `Prepare-Pack.ps1`은 `minimal-cli.json`의 PHP 7파일만 보존하고 Full은 별도 새 경로로 준비합니다. `php.exe`/`php8.dll`, 원문 license/readme, 원본 SBOM 3개이며 모든 유지 파일은 같은 공식 ZIP의 원본 바이트입니다. 별도 packId를 사용하고 기존 Full 팩을 덮어쓰지 않습니다. [비교·회귀·코어 소스 검토](tests/t08-minimal/README.md)를 따릅니다.

원본 SBOM과 readme는 입력 ZIP의 상위 집합을 설명합니다. GLib/Enchant/Firebird 확장 등 제거 파일의 과거 import/pending 목록을 최소 팩의 실제 포함 목록으로 다시 사용하지 않습니다. 반대로 DLL 제거만으로 PHP 코어에 정적으로 포함된 코드가 없어졌다고 판단하지 않습니다. BCMath와 libiconv의 LGPL 검토는 남습니다.

코어 소스 수집기는 PHP 태그의 고정 커밋과 libiconv/libxml2/zlib/wineditline 및 관측된 Argon2 후보의 전체 tracked 경로·변환 전 Git blob 바이트를 대조합니다. export-ignore로 생략되는 파일을 포함해 ZIP을 만들고 공식 SBOM bundled digest를 대조합니다. Argon2 런타임 관측은 알고리즘 활성 여부이며 버전·소스 커밋은 SBOM 근거로 구분합니다. 이 수집은 네이티브 바이너리 재현, 전체 application code/빌드 설정·재링크 경로의 검증이나 지속적인 소스 제공을 대신하지 않습니다.

### Windows 설정 생성물과 소스 대조

원본만 대조하면 libmbfl/timelib의 집계가 달랐던 원인을 확인했습니다. pinned `ext/mbstring/config.w32`는 `config.h.w32`를 `libmbfl/config.h`로 복사하고, `ext/date/config.w32`는 10개 문자열을 ASCII·CRLF로 `timelib_config.h`에 기록합니다. 이 두 헤더를 별도 생성물로 포함하면 공식 bundled digest 23개가 모두 일치합니다. 원본 Git·Unix tar·Windows source ZIP에는 설정 전에 만드는 두 파일이 없으므로 원본끼리 동일하다는 사실만으로 생성물 영향을 배제했던 이전 판단은 잘못됐습니다.

원본 소스 ZIP은 수정하지 않습니다. `rawBundledSourceChecks`는 21/23을 보존하고, `generatedWindowsSource`에는 입력 3개 Git blob/SHA·검토한 생성 절차·출력 2개 SHA와 별도 ZIP을 기록합니다. 생성 파일을 Git 원문이라고 표시하지 않습니다. [생성물 근거](tests/t08-minimal/evidence/generated-windows-config.json)와 [설정 포함 대조](tests/t08-minimal/evidence/core-source.json)를 사용합니다. 입력 또는 생성물이 바뀌면 실패하도록 하고 변조 대조를 수행합니다.

공식 Windows `php-8.4.27-1-src.zip`은 공개 SHA-256 `9b3078d9ea77ceb49becb0198540527b03840b8a13b020aa5a80215d6bab418d`와 일치합니다. [전체 25,836파일 대조](tests/t08-minimal/evidence/windows-source-comparison.json)에서 경로가 pinned Git과 동일하고 차이 7개는 `$Id$` 확장뿐입니다. 이 source ZIP도 설정 전 원문이며 두 생성 헤더는 별도로 추적합니다. libmbfl은 전체 ZIP/SBOM 후보이고 최소 팩에 mbstring 확장을 추가하지 않았습니다.

### 실제 빌드·재링크에 남은 관문

[공식 Windows 빌드 조사](tests/t08-minimal/evidence/official-build-evidence.json)와 [선택 로그](tests/t08-minimal/evidence/official-build-selected-log.txt)에서 builder head `68dc945d9509c41b72332c74c8b99731bb75bc25`, x64 NTS 작업, SDK 설정 2.9.0, MSVC 도구 14.44.35207/컴파일러 19.44.35229, PGI→default/cache 훈련→PGO→snap 절차를 확인했습니다. `libiconv_a.lib`와 `libxml2_a_dll.lib`의 정적 선택도 관측했습니다. [공식 artifact 직접 대조](tests/t08-minimal/evidence/official-artifact-identity.json)에서 artifact 12641062548의 353,269,590바이트·공개 SHA를 확인했고, 그 안의 NTS x64 ZIP이 승인 ZIP SHA `dbae154a0a89791f36c3d48e68bae797878333e7d6cb5721e74617de59b63ab7`와 일치했습니다. php.exe/php8.dll 해시와 Windows source ZIP도 기존 근거와 일치합니다. 이 연결로 과거 SDK 태그 해석·의존성 ZIP 바이트까지 확인한 것은 아닙니다. 개발 팩의 .lib 38개는 전체 재링크 객체/응답 입력의 확보를 뜻하지 않으며 .obj/.rsp는 없었습니다.

builder/SDK의 고정 소스와 고지는 수집했습니다. 실제 제품의 전체 configure 명령도 [PHP 실행 정보](tests/t08/evidence/php-info-no-ini.txt)에 보존했습니다. snapshot/debug-pack·NTS·object-out-dir·shared COM·without-analyzer·PGO 설정을 미확인으로 취급하지 않습니다. 남은 검토는 실제 제품 구성에 대응하는 생성 Makefile/설정/링커 응답 자료와 의존성 입력·소스의 충분성입니다. PHP의 `win32/build/Makefile`은 코어 링크 명령에 `@`를 사용해 명령 자체를 출력하지 않으므로 로그만을 전체 링크 입력 목록으로 사용하지 않습니다.

[공식 PDB·생성 헤더 대조](tests/t08-minimal/evidence/official-debug-inputs.json)에서 NTS x64 `php.exe`·`php8.dll`의 CodeView GUID/age와 동봉 PDB가 각각 일치했습니다. PHP DLL의 PDB 모듈 기록에서 `libiconv_a.lib`, `libxml2_a_dll.lib`, `zlib_a.lib`, `argon2_a.lib`, `edit_a.lib` 및 bundled BCMath 개체 이름을 확인했습니다. 같은 공식 artifact의 개발 ZIP에서 `main/config.w32.h`와 `Zend/zend_config.w32.h`를 확보하고 해시를 기록했습니다. 공식 아카이브에서 개발 ZIP을 다시 받아 기존 artifact의 개발 ZIP과 SHA-256이 일치함을 확인했습니다. 두 헤더를 [공식 개발 헤더](tools/license-audit/official-php-headers/)에 원본 바이트로 보존합니다. `main/config.w32.h`에서 x64·VS17과 BCMath·libiconv·Argon2·zlib·libxml 활성화를 확인했습니다. 공식 Windows 소스 ZIP에는 이 두 생성 헤더가 없으며, 코어 수집기가 별도로 보존하는 libmbfl/timelib 생성물과 경로가 다릅니다. [개발 ZIP 출처·헤더 해시](tests/t08-minimal/evidence/official-debug-inputs.json)

[정적 링크·소스 매핑](tests/t08-minimal/evidence/official-native-source-map.json)은 PDB에 기록된 외부 라이브러리 5개의 이름을 배포 ZIP 내부 SBOM이 명시한 소스 커밋 및 의존성 ZIP 이름과 대조합니다. 수집한 대응 소스 5개의 저장소·커밋이 일치합니다. SBOM은 해당 의존성 ZIP의 해시를 제공하지 않으므로 현재 서버에서 수집한 후보 ZIP의 해시를 과거 빌드 입력의 해시로 사용하지 않습니다.

PDB와 생성 헤더에서 확인할 수 있는 범위는 모듈 이름과 구성입니다. 과거 빌드의 .obj/.lib 바이트와 전체 Makefile·링커 응답 파일은 미확인입니다.

격리 감사 절차는 원본/마커 빌드의 생성 Makefile·config.nice.bat·main/config.w32.h·응답 파일·정의 파일을 별도 ZIP과 SHA inventory로 보존하고, 비교기가 실제 빌드 폴더와 바이트를 대조합니다. 이는 축소 감사 구성의 자료이며 제품 전체 구성과의 대응을 완료 처리하지 않습니다. 제품 바이너리·목표·엔진·스택은 바꾸지 않습니다. 특수 헤더 고지·선택한 LGPL 이행 경로·최종 소스 지속 제공도 남습니다. 소스 수집기 자체의 `sourceCollectionPassed=true`는 설정 포함 소스 해시 대조만 뜻하며, 과거 수집 결과의 `nativeBuildOrRelinkVerified=false`를 후속 격리 실험의 성공과 혼동하지 않습니다. `redistributionCleared=false`는 유지합니다.

[생성 자료 실측](tests/t08-minimal/evidence/php-relink/build-materials.json)에서 원본/마커 각 19개 파일·응답 파일 15개와 실제 빌드 바이트 일치, 24사례 회귀를 확인했습니다. 수집 도구는 PATH 우선순위의 MSVC 도구 하나를 선택하며 Git의 동명 link.exe와 경로를 합치지 않습니다. 이 결과의 fullProductProfileVerified=false와 일시 artifact 보관 한계는 유지합니다.

### 검증용 libiconv 빌드 진행

[공식 의존성 후보 해시](tests/t08-minimal/evidence/native-inputs.json)는 2026-11-05 다운로드한 현재 파일의 해시이며, 과거 공식 빌드의 실제 입력과 바이트 일치를 뜻하지 않습니다. [독립 Windows 빌드 결과](tests/t08-minimal/evidence/libiconv-rebuild.json)는 고정 libiconv 소스 원본과 검증용 마커 사본의 정적 라이브러리 빌드·서로 다른 SHA·`_libiconv_version` 링커 심볼을 확인합니다. SDK는 10.0.26100.0입니다. source commit·artifact 해시는 [실행 근거](tests/t08-minimal/evidence/native-rebuild-provenance.json)를 따릅니다.

마커는 격리된 감사용 사본의 `_LIBICONV_VERSION + 1`이며 실제 버전을 의미하지 않습니다. 이 libiconv 단독 결과와 별도로, 아래 PHP 재링크 실험을 수행했습니다.

### 격리 PHP 재링크 실험

[Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/42018682117)에서 PHP 8.4.27 Windows 소스와 검증용 원본·마커 libiconv를 각각 빌드·링크했습니다. 원본 `ICONV_VERSION=1.19`, 마커 `1.20`을 관측했고 두 PHP DLL의 해시가 다릅니다. 1.20은 실제 버전이 아닌 테스트 마커입니다. 같은 PHPCS 엔진으로 24 fixture/목표 조합의 JSON·종료 코드가 동일했으며 검사 대상·저장소 fixture·엔진 팩 불변을 확인했습니다. [원본 결과·로그·해시](tests/t08-minimal/evidence/php-relink/provenance.json)를 보존합니다.

이 검증은 축소 configure 옵션을 사용한 독립 감사 빌드입니다. 공식 배포 PHP의 바이너리 재현, 과거 공식 빌드 의존성 ZIP과 동일성, 이 실험에서의 네트워크 차단, 최종 배포 고지·대응 소스·LGPL 의무 이행을 증명하지 않습니다. `php-relink.json`의 `phpcsRegressionVerified=false`는 그 파일이 별도 비교 단계 이전에 작성됐기 때문이며 후속 `php-relink-comparison.json`에 실제 24건 결과가 있습니다.

### 배포 라이선스 선택 — 사용자 승인 반영

2026-11-06 사용자가 권장안을 승인했습니다. covered PHP code에는 PHP License v4, covered Zend Engine code에는 Zend Engine License v3.0 후속 조건을 선택하며 적용 조건은 BSD-3-Clause입니다. [공식 공통 본문](licenses/php/selected/PHP-Zend-BSD-3-Clause.txt), [공식 선택 허용 근거 snapshot](licenses/php/selected/php-licensing.html), [출처·SHA·승인·범위 기록](tools/license-audit/license-election.json)을 보존합니다. 원문 공통 본문은 PHP Group과 Zend 양측 저작권을 포함합니다. 별도의 Zend v3 전문을 임의 작성하지 않았습니다.

원본 PHP 3.01/Zend 2.00 전문과 원본 source headers는 provenance로 그대로 보존합니다. 이번 선택으로 covered code에 적용하는 legacy 이름 조건을 후속 조건으로 전환하며 제품 이름을 바꾸지 않습니다. 이 선택은 독립 BCMath/libiconv/엔진 LGPL 및 다른 내장 라이브러리·Microsoft 조건과 자체 MIT를 재허가하지 않습니다. 고지에 포함된 PHP/Zend의 명칭은 제휴·보증을 뜻하지 않습니다.

라이선스 선택 대기로 로컬 자가 빌드·사용자 시험을 막는 상태는 해소했습니다. 시험 후보는 정확히 짝지은 바이너리·고지·대응 소스 및 재구성 자료를 제공하는 절차로 검증합니다. 공개 Release는 별도 승인 전이며, 이 문단 자체가 모든 외부 구성 요소에 관한 법적 적합성을 일괄 보증하는 것은 아닙니다.

### 재링크 의무와 바이너리 재현성의 구분

보존한 [LGPL 2.1 전문 §6(a)](licenses/php/core-source/libiconv/source/COPYING.LIB)은 라이브러리의 대응 소스와 필요한 application code를 제공하여 사용자가 수정 라이브러리로 다시 링크할 수 있게 하는 경로를 설명합니다. 동일 해시의 원래 실행 파일을 재생성하는 bit-for-bit reproducibility를 별도 요건으로 적고 있지는 않습니다. 따라서 공식 바이너리의 완전한 해시 재현 자체를 T08의 독립 라이선스 의무로 추가하지 않습니다. 다만 **실제 배포물에 대응하는 정확한 소스·변경·빌드/링크 자료가 충분한지**는 계속 확인해야 하며, 작은 감사용 빌드의 성공만으로 그 대응성을 대체하지 않습니다. §6의 수정·디버깅 권한, 고지 및 다른 포함 라이브러리 조건도 함께 유지해야 합니다. 이 문단은 원문에 따른 배포 준비 기준을 정리한 것이며 법률 적합성의 최종 보증이 아닙니다.

### 대응 소스·고지 동시 제공 준비

`Prepare-SourceSidecar.py`는 검증된 core 6개 및 LGPL engine 2개 소스 ZIP·inventory의 기록 SHA를 먼저 확인하고, 별도 Windows 생성 헤더 ZIP·입력 레시피, 현재 프로젝트 소스/검증 스크립트/워크플로 및 원문 라이선스·고지를 한 동반 ZIP으로 묶습니다. 재링크 workflow가 참조하는 의존성 후보 URL/해시 목록과 결과 비교기도 명시적으로 포함하고 페어링에서 현재 파일과 바이트를 대조합니다. `Collect-BuildSources.py`로 수집한 고정 builder 소스와 SDK 레시피·원문 고지도 함께 전달합니다. SDK 도구 EXE·DLL은 별도 확보합니다. 출력 manifest는 모든 멤버의 SHA와 승인된 적용 라이선스 선택을 기록합니다. 전체 배포 적합성은 개별 배포 자료 대조로 판단하며 선택 승인만으로 일괄 완료하지 않습니다. 이 ZIP은 검토 가능한 소스 제공 준비물입니다. 최종 바이너리와 정확히 짝지은 이름·SHA로 함께 지속 배포해야 하며 7일 Actions artifact만으로 영구 제공을 완료 처리하지 않습니다. 실제 수집·동반 ZIP 생성 결과는 전용 Windows workflow로 검증해야 합니다.

### PHP SDK 레시피와 도구 확보

2026-11-14에 [최신 후보 패키징 CI](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/43427066932)의 artifact 14037897608을 내려받아 GitHub의 SHA-256과 대조했습니다. 동반 ZIP manifest는 `sources/build/php-sdk-binary-tools-81b356186db123ce9cd65149cee4683211a59ebd.zip`을 포함합니다. 고정 커밋에서 `Collect-BuildSources.py`로 SDK ZIP을 재생성했고 CI와 같은 SHA-256 `2da08a1a9d1d6b95c55e66429d15bf0b7a22f922f89daaccee9d0d4680f7db40`을 확인했습니다. 아카이브의 328개 Git 파일 중 PE 바이너리는 EXE 38개·DLL 58개입니다. SDK가 사용하는 PHP, MSYS2 도구 및 DLL도 포함됩니다. [고정 SDK의 구성별 고지](https://github.com/php/php-sdk-binary-tools/blob/81b356186db123ce9cd65149cee4683211a59ebd/licenses/README.md)를 참조합니다.

앱 ZIP의 `tools/runtime/php/php.exe`와 `php8.dll`은 [승인한 공식 PHP ZIP](tests/t08-minimal/evidence/official-artifact-identity.json)의 각 SHA-256과 일치했습니다. 사용자가 승인한 새 동반 ZIP 구성은 SDK 도구 EXE·DLL을 제외하고 고정 커밋의 레시피와 원문 고지를 보존합니다. [SDK 확보 및 재링크 절차](tools/license-audit/README.md#windows-php-sdk-확보와-재링크)에서 별도 checkout의 커밋을 확인합니다. 페어링 검증은 SDK 레시피 ZIP의 고지·시작 스크립트가 있고 EXE·DLL 또는 MZ 파일이 없음을 확인합니다. 실제 배포 PHP 전체 구성의 재링크 입력 대응은 별도 검토 항목입니다.

## 재현과 공식 근거

공식 빌드와 승인 바이너리의 직접 연결은 [artifact 대조](tests/t08-minimal/evidence/official-artifact-identity.json)의 절차·해시로 재확인할 수 있습니다. 이 실측은 기존 빌드 조사 당시의 미확인 부분만 보완하며 과거 실행 기록을 소급 수정하지 않습니다.

[Windows 수집 절차](tools/license-audit/README.md)를 따릅니다. 배포 바이너리·NuGet download cache·vendor 소스 자체는 Git에 추가하지 않았습니다. 원문 고지·파일 inventory·source 대조·검토 근거만 보존합니다. `source-checks`의 실제 소스 ZIP은 일시 artifact에 있으며 최종 배포 시 재생성·동시 제공해야 합니다.

2026-11-05에 현재 접근 가능 여부와 내용을 확인한 공식 근거:

- [엔진 고정 커밋·원문·해시](tools/license-audit/engine-sources.json)
- [PHP Licensing](https://www.php.net/license/) 및 [배포 지침](https://www.php.net/license/distrib-guidelines-code.php)
- [PHP 8.4.27 원문](https://github.com/php/php-src/tree/php-8.4.27)과 [실제 ZIP SBOM](tests/t08/evidence/php.cdx.json)
- [Microsoft .NET Windows 파일별 정책](https://github.com/dotnet/core/blob/main/license-information-windows.md), [NET Library terms](https://dotnet.microsoft.com/en-us/dotnet_library_license.htm), [Windows SDK terms](https://learn.microsoft.com/en-us/legal/windows-sdk/license)
- [Visual C++ 재배포 조건](https://learn.microsoft.com/cpp/windows/redistributing-visual-cpp-files)
- [추가 원문 보존·HTML 추출 근거](tools/license-audit/review-sources.json)
