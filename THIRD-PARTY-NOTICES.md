# 서드파티 고지 — 배포 전 검토 중

이 문서는 현재 개발 구성의 고지 초안입니다. **PHP 네이티브 구성의 고지·대응 소스 검토가 남아 있어 공개 배포용 최종 고지가 아닙니다.** 판정과 남은 조건은 [LICENSING.md](LICENSING.md)를 따릅니다.

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

이는 구성 요소 포함 고지이며 PHP Group 또는 Zend의 보증·공식 인증을 뜻하지 않습니다. 현재 감리에서 후속 PHP/Zend 라이선스로의 선택을 선언하지 않았습니다. 현재 공식 페이지의 후속 라이선스 선택 가능성과 원본의 실제 조건을 구분합니다.

PHP 내부 libmagic, libmbfl, libbcmath, libavifinfo, Lexbor와 libcperciva의 별도 [원문 고지](licenses/php/embedded/)도 보존합니다. 기존 Full 기준의 [후보 목록](tests/t08/evidence/php-native-pending.json)은 축소 팩의 실제 포함 목록이 아닙니다. 최소 팩에도 BCMath/libiconv 등 정적 코드가 남으며, [현재 검토](LICENSING.md)의 코어 출처·원문 고지·대응 소스/재링크 조건을 배포 전 해소해야 합니다. SPDX 후보 목록 자체가 라이선스 전문이나 대응 소스를 대체하지 않습니다.

## 최소 CLI의 코어 소스 원문

소스 수집에서 확보한 libiconv/libcharset, libxml2, zlib, wineditline, Argon2 및 timelib의 [원문 파일](licenses/php/core-source/)을 보존했습니다. 정확한 [경로·커밋·Git blob·해시](tests/t08-minimal/evidence/core-legal-files.json)를 함께 봅니다. libiconv 소스의 COPYING은 함께 수집한 도구 소스에 관한 원문이며, 해당 도구 실행 파일이 최소 팩에 포함된다는 뜻이 아닙니다. Argon2 등 원문에 있는 대안 조건을 보존하고 임의로 전체 배포의 라이선스를 선택하지 않았습니다. 헤더에만 있는 특수 고지 및 실제 링크·바이너리 대응의 최종 검토는 남습니다. timelib의 SBOM 해시 불일치도 미해결입니다.

## Windows .NET self-contained 구성

실측 publish는 .NETCore runtime, WindowsDesktop runtime, apphost의 **10.0.14** NuGet 패키지를 사용했습니다. 각 패키지의 MIT 원문과 서드파티 고지·nuspec을 [licenses/dotnet](licenses/dotnet/)에 보존했습니다. WindowsDesktop 패키지에 누락된 WPF·WinForms 고지는 그 패키지 nuspec의 정확한 `dotnet/dotnet` source commit에서 보충했으며 원본 패키지 파일과 구분합니다.

Microsoft의 [Windows별 파일 정책](licenses/dotnet/UPSTREAM-WINDOWS-LICENSE-INFORMATION.md)에 따라 다음 예외를 구분합니다.

| 실제 포함 파일 | 별도 적용 조건 |
| --- | --- |
| coreclr.dll, Microsoft.DiaSymReader.Native.amd64.dll, PresentationNative_cor3.dll, vcruntime140_cor3.dll, wpfgfx_cor3.dll | [Microsoft .NET Library License](licenses/dotnet/Microsoft.NET-Library-License.txt) |
| D3DCompiler_47_cor3.dll | [Microsoft Windows SDK License](licenses/dotnet/Windows-SDK-License.txt) |
| 나머지 .NET 파일 | 해당 MIT 및 별도 서드파티 고지; [파일별 대조](tests/t08/evidence/publish-license-map.json) |

해당 Microsoft 바이너리를 수정하거나 자체 MIT로 재허가하지 않습니다. Microsoft 조건의 배포 형태·고지 보존·외부 배포자/사용자에게 적용할 보호 조건은 최종 배포에서 이행해야 하며, 자체 코드나 LGPL 라이브러리의 권리를 제한하는 일반 금지 조항으로 확대하지 않습니다. 원본 라이선스와 [출처·변환 근거](tools/license-audit/review-sources.json)를 함께 확인합니다.

## PHP의 CRT 전제

PHP가 사용하는 시스템 VCRUNTIME/UCRT는 현재 PHP ZIP에 포함되어 있지 않습니다. 사용자 PC의 CRT 전제는 별도로 안내·검증합니다. runner에서 관측한 시스템 DLL을 복사하거나 .NET의 `vcruntime140_cor3.dll`을 이름 변경해 PHP용으로 재배포하는 절차는 없습니다. CRT 설치 프로그램을 나중에 포함하려면 별도 Microsoft 재배포 권한·조건을 확인해야 합니다.
