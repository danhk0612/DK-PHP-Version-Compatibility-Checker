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
3. 현재 보존한 PHP 3.01/Zend 2.00의 고지·해당 조건을 유지하거나 공식 허용 후속 라이선스 선택을 명시적으로 기록합니다. 현재 감사에서 후속 라이선스 선택이나 제품 이름 변경을 임의 확정하지 않았습니다. PHP 자체의 후속 라이선스 선택이 독립 native 라이브러리까지 재허가하지는 않습니다.
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

[공식 Windows 빌드 조사](tests/t08-minimal/evidence/official-build-evidence.json)와 [선택 로그](tests/t08-minimal/evidence/official-build-selected-log.txt)에서 builder head `68dc945d9509c41b72332c74c8b99731bb75bc25`, x64 NTS 작업, SDK 설정 2.9.0, MSVC 도구 14.44.35207/컴파일러 19.44.35229, PGI→default/cache 훈련→PGO→snap 절차를 확인했습니다. `libiconv_a.lib`와 `libxml2_a_dll.lib`의 정적 선택도 관측했습니다. 해당 공식 작업 산출물과 실제 배포 ZIP 사이의 직접 해시 연결은 아직 수행하지 않았습니다.

다음 T08 범위는 builder/SDK 전체 소스·고지와 정확한 의존성 ZIP 해시/도구 설정을 고정하고, 이미 수집한 libiconv 등 관련 정적 라이브러리를 재빌드한 뒤 PHP application code를 수정 라이브러리로 재링크·회귀 검증하는 것입니다. 관리 영역의 검증이며 제품 바이너리 교체·목표/엔진/스택 변경을 뜻하지 않습니다. 특수 헤더 고지·선택한 LGPL 이행 경로·최종 소스 지속 제공도 남습니다. `sourceCollectionPassed=true`는 설정 포함 소스 해시 대조만 통과했다는 뜻이며 `nativeBuildOrRelinkVerified=false`, `redistributionCleared=false`를 유지합니다.

### 검증용 libiconv 빌드 진행

[공식 의존성 후보 해시](tests/t08-minimal/evidence/native-inputs.json)는 2026-11-05 다운로드한 현재 파일의 해시이며, 과거 공식 빌드의 실제 입력과 바이트 일치를 뜻하지 않습니다. [독립 Windows 빌드 결과](tests/t08-minimal/evidence/libiconv-rebuild.json)는 고정 libiconv 소스 원본과 검증용 마커 사본의 정적 라이브러리 빌드·서로 다른 SHA·`_libiconv_version` 링커 심볼을 확인합니다. SDK는 10.0.26100.0입니다. source commit·artifact 해시는 [실행 근거](tests/t08-minimal/evidence/native-rebuild-provenance.json)를 따릅니다.

마커는 격리된 감사용 사본의 `_LIBICONV_VERSION + 1`이며 실제 버전을 의미하지 않습니다. 이 libiconv 단독 결과와 별도로, 아래 PHP 재링크 실험을 수행했습니다.

### 격리 PHP 재링크 실험

[Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/42018682117)에서 PHP 8.4.27 Windows 소스와 검증용 원본·마커 libiconv를 각각 빌드·링크했습니다. 원본 `ICONV_VERSION=1.19`, 마커 `1.20`을 관측했고 두 PHP DLL의 해시가 다릅니다. 1.20은 실제 버전이 아닌 테스트 마커입니다. 같은 PHPCS 엔진으로 24 fixture/목표 조합의 JSON·종료 코드가 동일했으며 검사 대상·저장소 fixture·엔진 팩 불변을 확인했습니다. [원본 결과·로그·해시](tests/t08-minimal/evidence/php-relink/provenance.json)를 보존합니다.

이 검증은 축소 configure 옵션을 사용한 독립 감사 빌드입니다. 공식 배포 PHP의 바이너리 재현, 과거 공식 빌드 의존성 ZIP과 동일성, 이 실험에서의 네트워크 차단, 최종 배포 고지·대응 소스·LGPL 의무 이행을 증명하지 않습니다. `php-relink.json`의 `phpcsRegressionVerified=false`는 그 파일이 별도 비교 단계 이전에 작성됐기 때문이며 후속 `php-relink-comparison.json`에 실제 24건 결과가 있습니다.

## 재현과 공식 근거

[Windows 수집 절차](tools/license-audit/README.md)를 따릅니다. 배포 바이너리·NuGet download cache·vendor 소스 자체는 Git에 추가하지 않았습니다. 원문 고지·파일 inventory·source 대조·검토 근거만 보존합니다. `source-checks`의 실제 소스 ZIP은 일시 artifact에 있으며 최종 배포 시 재생성·동시 제공해야 합니다.

2026-11-05에 현재 접근 가능 여부와 내용을 확인한 공식 근거:

- [엔진 고정 커밋·원문·해시](tools/license-audit/engine-sources.json)
- [PHP Licensing](https://www.php.net/license/) 및 [배포 지침](https://www.php.net/license/distrib-guidelines-code.php)
- [PHP 8.4.27 원문](https://github.com/php/php-src/tree/php-8.4.27)과 [실제 ZIP SBOM](tests/t08/evidence/php.cdx.json)
- [Microsoft .NET Windows 파일별 정책](https://github.com/dotnet/core/blob/main/license-information-windows.md), [NET Library terms](https://dotnet.microsoft.com/en-us/dotnet_library_license.htm), [Windows SDK terms](https://learn.microsoft.com/en-us/legal/windows-sdk/license)
- [Visual C++ 재배포 조건](https://learn.microsoft.com/cpp/windows/redistributing-visual-cpp-files)
- [추가 원문 보존·HTML 추출 근거](tools/license-audit/review-sources.json)
