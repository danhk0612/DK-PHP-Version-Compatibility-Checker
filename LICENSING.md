# T08 배포 라이선스 검토

## 현재 판정

**부분 검증 — 현재 전체 PHP ZIP 구성은 공개 배포 승인 상태가 아닙니다.** 자체 코드의 MIT, 엔진 원본, Windows publish 구성·출처와 엔진 LGPL 소스 대조를 준비했습니다. PHP 네이티브 코드의 정확한 포함·라이선스·대응 소스/재링크 자료와 최종 배포 시의 소스 동시 제공은 남아 있습니다. 제품 코드·팩 구성·이름·스택은 변경하지 않았습니다.

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

## PHP 네이티브 검토에서 확인한 것

현재 팩은 공식 NTS ZIP 전체를 그대로 포함합니다. GLib DLL 3개, libenchant2와 Hunspell plugin, php_gmp/mbstring/dba 확장 등 실제 배포 파일이 있습니다. 로딩되지 않는 확장도 파일을 배포한다면 고지·소스 검토 범위입니다.

`php -n -i`에서 BCMath가 기본 활성화됨을 확인했습니다. 따라서 최소 CLI 파일만 남겨도 PHP에 정적으로 포함된 LGPL 코드의 대응 소스 검토가 없어지지 않습니다. libbcmath와 libmbfl의 원문은 LGPL-2.1입니다. SBOM의 GLib·Enchant·MPIR·libintl/iconv/qdbm 등 후보는 [pending 목록](tests/t08/evidence/php-native-pending.json)에 버전·원본 source commit과 함께 기록했습니다. FreeType나 zstd의 대안 라이선스는 OR 조건을 보존하며 전체 앱이 무조건 GPL이라고 단정하지 않습니다.

공식 in-ZIP CDX와 나중에 제공되는 웹 sidecar는 바이트가 달랐습니다. 이 보고서의 파생 목록은 **실제 ZIP에서 추출한 CDX**를 기준으로 합니다. SBOM은 Apache/APR/Lua 같은 후보와 중복 버전도 포함하므로 102개를 실제 배포 구성 요소 수로 부르지 않습니다. SHA·파일/import·빌드 설정과 대조해야 합니다. DLL import만으로 정적 라이브러리 부재나 소스 완전성을 증명할 수 없습니다.

`deplister`는 PHP 소스의 BindImageEx BOOL 반환을 그대로 사용하므로 exit 1은 TRUE입니다. [원문](https://github.com/php/php-src/blob/php-8.4.27/win32/build/deplister.c)과 [import 관측](tests/t08/evidence/php-imports.json)을 함께 보존했습니다. 64개 이미지 중 Firebird PDO 확장에서 `fbclient.dll,NOTFOUND`가 관측됐습니다. 이를 숨기거나 전체 네이티브 의존성 충족으로 처리하지 않습니다. 이 선택 확장은 기본 PHPCS 검사에 로딩되지 않으며 Firebird client 파일은 현재 배포물에 없습니다.

## 배포 전 해소할 조건

1. 실제 포함되는 네이티브 구성마다 고정된 소스/버전·라이선스 전문·저작권 고지를 파일 및 정적 포함 코드에 연결합니다. PHP README가 외부 소스 LICENSE를 가리키는 부분도 전문을 포함합니다. 현재 확보한 내장 고지만으로 외부 네이티브 구성 전부를 충족한다고 보지 않습니다.
2. PHP의 LGPL 내장 코드와 실제 포함 native LGPL에 대해 변경 사항·빌드 스크립트·필요 대응 application code 등 선택한 라이선스 경로의 소스/재링크 자료를 확보하고 검증합니다. Composer 2개 source ZIP으로 이를 대신하지 않습니다.
3. 현재 보존한 PHP 3.01/Zend 2.00의 고지·해당 조건을 유지하거나 공식 허용 후속 라이선스 선택을 명시적으로 기록합니다. 현재 감사에서 후속 라이선스 선택이나 제품 이름 변경을 임의 확정하지 않았습니다. PHP 자체의 후속 라이선스 선택이 독립 native 라이브러리까지 재허가하지는 않습니다.
4. Microsoft 제한 바이너리는 자체 MIT/일반 LGPL 수정 권한의 적용 범위에서 구분하고, 해당 조건의 재배포·사용자/배포자 보호 요건을 최종 배포에 반영합니다. PHP용 시스템 CRT를 별도로 복사하지 않습니다.
5. 최종 배포물, 대응 소스, 전체 고지 및 해시를 함께 고정·제공합니다. T08 미해결 상태에서 T10/T09의 선행 조건을 합격 처리하지 않습니다.

## 배포 구성 판단안

| 안 | 작업 | 영향 |
| --- | --- | --- |
| A: 현재 전체 ZIP 유지 | 모든 실제 native 구성의 고지·대응 소스/재링크 검토를 계속 완료 | 기존 팩 형태를 유지하나 쓰지 않는 확장·개발 도구까지 배포 검토에 포함 |
| B: 검사에 필요한 CLI 팩으로 축소 검토 | 필요한 파일/확장/DLL을 실측으로 정하고 변경 팩의 전체 회귀·오프라인·보호·목표 검증 수행 | 불필요한 배포 범위를 줄일 수 있으나 T04 팩 구성·manifest가 바뀜. PHP 코어 LGPL 대응 소스 검토는 여전히 필요 |

권장 검토 방향은 B입니다. 단, 파일 삭제나 최소 구성을 지금 확정하지 않았습니다. 이는 문서만의 수정이 아니라 검증된 팩 구성을 바꾸는 선택이므로 사용자 판단 후 별도 범위와 재검증 조건을 고정합니다. 현재 저장소에는 A 구성의 감사 결과와 미해결 관문을 보존했습니다.

## 재현과 공식 근거

[Windows 수집 절차](tools/license-audit/README.md)를 따릅니다. 배포 바이너리·NuGet download cache·vendor 소스 자체는 Git에 추가하지 않았습니다. 원문 고지·파일 inventory·source 대조·검토 근거만 보존합니다. `source-checks`의 실제 소스 ZIP은 일시 artifact에 있으며 최종 배포 시 재생성·동시 제공해야 합니다.

2026-11-05에 현재 접근 가능 여부와 내용을 확인한 공식 근거:

- [엔진 고정 커밋·원문·해시](tools/license-audit/engine-sources.json)
- [PHP Licensing](https://www.php.net/license/) 및 [배포 지침](https://www.php.net/license/distrib-guidelines-code.php)
- [PHP 8.4.27 원문](https://github.com/php/php-src/tree/php-8.4.27)과 [실제 ZIP SBOM](tests/t08/evidence/php.cdx.json)
- [Microsoft .NET Windows 파일별 정책](https://github.com/dotnet/core/blob/main/license-information-windows.md), [NET Library terms](https://dotnet.microsoft.com/en-us/dotnet_library_license.htm), [Windows SDK terms](https://learn.microsoft.com/en-us/legal/windows-sdk/license)
- [Visual C++ 재배포 조건](https://learn.microsoft.com/cpp/windows/redistributing-visual-cpp-files)
- [추가 원문 보존·HTML 추출 근거](tools/license-audit/review-sources.json)
