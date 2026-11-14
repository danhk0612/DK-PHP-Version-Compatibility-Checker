# 서드파티 고지

이 문서는 Windows x64 배포에 포함된 외부 구성 요소의 고지입니다. [v0.1.0 배포 페이지](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/releases/tag/v0.1.0)에서 대응 소스·고지 ZIP과 재링크 안내를 제공합니다.

자체 제작 코드에는 루트 [MIT LICENSE](LICENSE)를 적용합니다. 아래 외부 코드·바이너리의 저작권과 별도 조건을 유지합니다. 루트 MIT가 전체 배포물의 외부 구성 요소를 재허가하지 않습니다.

## 고정 검사 엔진

| 구성 | 고정 버전 | 적용 라이선스·원문 |
| --- | --- | --- |
| PHPCompatibility | 10.0.0-alpha2 | LGPL-3.0-or-later · [원문](licenses/engine/PHPCompatibility-10.0.0-alpha2-LICENSE) |
| PHPCSUtils | 1.2.3 | LGPL-3.0-or-later · [원문](licenses/engine/PHPCSUtils-1.2.3-LICENSE) |
| PHP_CodeSniffer | 4.0.4 | BSD-3-Clause · [원문](licenses/engine/PHP_CodeSniffer-4.0.4-licence.txt) |
| PHPCS Composer installer | v1.1.0 | MIT · [원문](licenses/engine/composer-installer-1.1.0-LICENSE.md) |
| Composer 생성 autoloader | 생성 도구는 실측에서 2.11.1 | MIT · [원문](licenses/engine/Composer-generated-autoloader-LICENSE) |

각 원문의 저작권·조건·면책을 보존합니다. LGPL-v3가 포함하는 [GPL-v3 전문](licenses/common/GPL-3.0.txt)도 함께 제공합니다. Composer 실행 파일은 현재 사용자용 팩에 포함하지 않습니다.

PHPCompatibility와 PHPCSUtils는 별도 PHP 소스 라이브러리로 배포합니다. 해당 LGPL 코드의 수정·교체 및 수정 디버깅에 필요한 권리를 제한하지 않습니다. 전체 대응 소스와 사용·재구성 방법은 최종 배포와 함께 지속적으로 제공해야 합니다. 일시적인 CI artifact나 upstream 링크만으로 소스 제공 조건을 충족했다고 보지 않습니다. [정확한 커밋·원본·소스 제공 근거](tools/license-audit/engine-sources.json)를 확인합니다.

## PHP와 Zend

PHP 실행 환경은 공식 `php-8.4.27-1-nts-Win32-vs17-x64.zip`의 승인된 최소 CLI 선택입니다. `php.exe`/`php8.dll`과 원문 license/readme·SBOM을 보존하고 선택 확장/DLL은 포함하지 않습니다. 원본 ZIP의 [license.txt](licenses/php/ZIP-license.txt), [추가 구성 고지](licenses/php/ZIP-readme-redist-bins.txt), 소스 태그의 [PHP License 3.01](licenses/php/PHP-8.4.27-LICENSE) 및 [Zend Engine License 2.00](licenses/php/Zend-8.4.27-LICENSE)를 보존합니다.

This product includes PHP software, freely available from <http://www.php.net/software/>.

This product includes the Zend Engine, freely available at http://www.zend.com

The Zend Engine is freely available at http://www.zend.com

이는 구성 요소 포함 고지이며 PHP Group 또는 Zend의 보증·공식 인증을 뜻하지 않습니다. 2026-11-06 사용자 승인으로 covered PHP code에는 PHP License v4, covered Zend Engine code에는 Zend Engine License v3.0 후속 조건을 선택합니다. 적용 조건은 BSD-3-Clause이며 [공식 공통 원문](licenses/php/selected/PHP-Zend-BSD-3-Clause.txt)과 [범위·출처·해시](tools/license-audit/license-election.json)를 동봉합니다. 원본 PHP 3.01/Zend 2.00 전문과 source headers는 출처 자료로 계속 보존합니다.

PHP 내부 libmagic, libmbfl, libbcmath, libavifinfo, Lexbor와 libcperciva의 별도 [원문 고지](licenses/php/embedded/)도 보존합니다. 기존 Full 기준의 [후보 목록](tests/t08/evidence/php-native-pending.json)은 축소 팩의 실제 포함 목록이 아닙니다. 최소 팩의 `php8.dll`에 포함된 libbcmath, libiconv 및 libcharset에는 각 원문의 LGPL 조건이 적용됩니다. [libbcmath 원문](licenses/php/embedded/libbcmath-LICENSE)과 [libiconv/libcharset 원문](licenses/php/core-source/libiconv/source/)을 제공합니다. 기본 팩을 변경하는 방법은 [재빌드 절차](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/blob/v0.1.0/tools/license-audit/README.md#동봉-소스로-windows-재링크)를 참고합니다. SPDX 후보 목록 자체가 라이선스 전문이나 대응 소스를 대체하지 않습니다.

## 최소 CLI의 코어 소스 원문

소스 수집에서 확보한 libiconv/libcharset, libxml2, zlib, wineditline, Argon2 및 timelib의 [원문 파일](licenses/php/core-source/)을 보존했습니다. 정확한 [경로·커밋·Git blob·해시](tests/t08-minimal/evidence/core-legal-files.json)를 함께 봅니다. libiconv 소스의 COPYING은 함께 수집한 도구 소스에 관한 원문이며, 해당 도구 실행 파일이 최소 팩에 포함된다는 뜻이 아닙니다. Argon2 등 원문에 있는 대안 조건을 보존하고 임의로 전체 배포의 라이선스를 선택하지 않았습니다. Boost.Context의 [Boost Software License](licenses/php/embedded/Boost.Context-LICENSE.txt)와 SHA-3 코드의 [Brian Gladman 고지](licenses/php/embedded/XKCP-Brian-Gladman-NOTICE.txt)를 동봉합니다. libcperciva의 SHA-NI·SSE2 고지는 위 내장 원문 폴더에 있고, PCRE2와 xxHash의 고지는 공식 ZIP의 추가 구성 고지에 있습니다. timelib의 SBOM 대조는 별도 Windows 설정 헤더를 포함해 해소했습니다. 원본·생성물의 구분과 실제 재링크·최종 재배포 관문은 유지합니다.

## Windows .NET 및 앱 호스트

Windows ZIP은 Microsoft의 [.NET Desktop Runtime 10 x64](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)가 설치된 PC에서 실행합니다. 앱 실행 파일에 사용한 .NET x64 앱 호스트의 [MIT 원문과 서드파티 고지](licenses/dotnet/Microsoft.NETCore.App.Host.win-x64/10.0.14/)를 동봉합니다. Microsoft가 별도로 제공하는 Desktop Runtime에는 해당 배포의 라이선스가 적용됩니다.

## 대응 소스와 수정·재링크 자료

배포 준비 시 별도 `DK-PHP-Compatibility-sources-and-notices.zip`을 만들고 파일 목록·SHA를 `source-sidecar.json`에 기록합니다. 이 준비물에는 원문 PHP/관련 라이브러리/엔진 소스, 별도로 추적한 Windows 설정 생성물, 현재 프로젝트·빌드·검증 레시피 및 원문 고지를 포함합니다. 동반 ZIP의 생성만으로 실제 배포 바이너리 대응과 지속 제공이 완료되는 것은 아닙니다. 최종 배포 시 같은 위치에서 정확히 짝지은 바이너리와 동반 ZIP을 계속 제공해야 합니다. 라이브러리 수정 및 그 수정의 디버깅을 위한 재링크 권리를 일반적인 변경 금지 조항으로 제한하지 않습니다.

PHP/Zend 후속 조건 선택은 사용자 승인으로 확정했습니다. [선택 기록](tools/license-audit/license-election.json)과 [공식 근거 snapshot](licenses/php/selected/README.md)을 함께 제공합니다. 자체 MIT와 독립 LGPL·Microsoft 조건은 이 선택과 별도로 유지합니다.

## PHP의 CRT 전제

PHP가 사용하는 시스템 VCRUNTIME/UCRT는 현재 PHP ZIP에 포함되어 있지 않습니다. 사용자 PC의 CRT 전제는 별도로 안내·검증합니다. runner에서 관측한 시스템 DLL을 복사하거나 .NET의 `vcruntime140_cor3.dll`을 이름 변경해 PHP용으로 재배포하는 절차는 없습니다. CRT 설치 프로그램을 나중에 포함하려면 별도 Microsoft 재배포 권한·조건을 확인해야 합니다.
