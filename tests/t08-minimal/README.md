# T08 B안 — 최소 CLI 팩 비교·회귀·소스 검토

2026-11-05 사용자가 최소 CLI 팩 축소와 재검증을 승인했습니다. 기존 전체 PHP ZIP의 바이트를 보존한 별도 Full 기준 팩과, 같은 공식 ZIP의 허용 파일만 복사한 MinimalCli 후보를 비교합니다. 검사 목표나 엔진 버전·GUI/서비스 계약은 바꾸지 않습니다.

Windows x64/SDK10.0.403에서 재현합니다. 기존 출력 폴더는 덮어쓰지 않으므로 새 checkout 또는 별도 출력 경로를 사용합니다.

```powershell
New-Item artifacts/t08-minimal -ItemType Directory -Force | Out-Null
./tools/engine-pack/Prepare-Pack.ps1 -RuntimeProfile Full -OutputDirectory artifacts/t08-minimal/full-runtime
Move-Item artifacts/t04 artifacts/t08-minimal/full-results
python -c "import sys;sys.path.insert(0,'tools/engine-pack');from pack_contract import inventory,write;from pathlib import Path;write(Path('artifacts/t08-minimal/full-before.json'),inventory(Path('artifacts/t08-minimal/full-runtime')))"
./tools/engine-pack/Prepare-Pack.ps1
Move-Item artifacts/t04 artifacts/t08-minimal/minimal-results
python tests/t08-minimal/compare_pack.py --full artifacts/t08-minimal/full-runtime --minimal tools/runtime --full-before artifacts/t08-minimal/full-before.json --full-results artifacts/t08-minimal/full-results --minimal-results artifacts/t08-minimal/minimal-results --output artifacts/t08-minimal/comparison
python tools/license-audit/Collect-PhpCoreSource.py --sbom tools/runtime/php/extras/sbom/php.cdx.json --php-info artifacts/t08-minimal/comparison/php-info-no-ini.txt --probe artifacts/t08-minimal/comparison/php-probe.json --output artifacts/t08-minimal/core-source
```

`compare_pack.py`는 stdlib만 사용합니다. 두 팩의 schema1/해시/lock/목표 계약, 기존 Full 전체 inventory 불변, 허용 목록과 보존 PHP 파일 바이트, 서로 다른 packId, 양쪽 24개 fixture·9개 annotation·24개 오프라인 대조, 설정 격리·원본 불변을 확인합니다. PE 실행 파일은 php.exe/php8.dll만 허용하고 실제 유지/제외 파일 및 크기 감소를 기록합니다. 메타데이터로 보존하는 원본 SBOM은 전체 입력 ZIP의 후보 목록이며 축소 팩의 정확한 구성 요소 목록으로 부르지 않습니다.

PHP 프로브는 ini를 배제하고 최소 PHP 및 Windows 시스템 경로만 PATH에 둡니다. 설치된 setup-PHP 디렉터리가 누락 DLL을 대신 공급하지 않게 합니다. 원본 Full의 deplister도 DLL이 없는 별도 임시 디렉터리로 복사하여 최소 후보 두 PE의 import를 관측합니다. upstream BindImageEx BOOL의 TRUE인 exit1과 개별 OK/NOTFOUND를 구분합니다. 가상 CRT API-set의 NOTFOUND는 실제 WinDLL 로딩과 Windows 시스템 backing module 경로/해시로 해결 여부를 확인하며 이름만으로 무시하지 않습니다. 환경/서버 변수 없이 제한된 phpinfo와 password_algos/확장 목록을 기록합니다.

[Windows workflow](../../.github/workflows/t08-minimal-cli.yml)는 Full→기본 MinimalCli 준비/비교 후 기존 T05 서비스 61건·T06 화면 21건·T07 실제 GUI 36건을 같은 최소 팩에서 실행합니다. 회귀 실행 PATH도 setup-PHP 없이 dotnet과 Windows 시스템 경로로 제한합니다. 실제 Windows 결과는 아래 보존 근거와 실행 상태를 확인합니다. 코어 소스 관문 실패를 전체 workflow 성공으로 표시하지 않습니다.

코어 소스 수집은 고정된 PHP와 실제 코어 의존성의 전체 tracked 경로와 변환 전 Git blob 바이트를 대조해 소스 ZIP/원문/빌드 자료를 보존하는 관리 절차입니다. 사용자 검사 대상 소스를 실행하거나 컴파일러/라이브러리를 설치하지 않습니다. 실제로 실행하지 않은 native rebuild/relink, 전체 대응 소스 이행, 영구적인 소스 제공을 검증 완료로 표현하지 않습니다. LGPL인 BCMath와 libiconv는 최소 파일 구성에서도 남아 있습니다.

전체 로그·소스 ZIP·원본 source inventory와 GUI 근거는 CI artifact에 7일 보관합니다. Full runtime/clone cache·배포 바이너리는 업로드하지 않습니다. 검증된 요약·해시·대표 근거를 Git에 보존하며 최종 배포에서는 같은 구성의 대응 소스·고지·재구성 자료를 지속 제공해야 합니다. Windows 11/CRT·최종 ZIP 배포는 T09, 실제 팩 교체/복원 통합은 T10의 별도 관문입니다.

## 소스 해시 불일치의 별도 재현

코어 수집기가 기록한 `core-source.json`에서 libmbfl/timelib이 불일치하면 통과로 바꾸지 않습니다. 공식 source tar.xz를 아래 조사 도구로 비교할 수 있습니다. 이 도구는 대조 보고서만 작성하며, 종료 코드 0이 소스 대응 관문 통과를 의미하지 않습니다. `matchedComponents`와 개별 `match`를 확인합니다. 파일을 추출하거나 빌드하지 않습니다.

```powershell
python tests/t08-minimal/compare_release_source.py --archive artifacts/t08-minimal/php-8.4.27.tar.xz --inventory artifacts/t08-minimal/core-source/php-00d8bae801541db0d95a698e108bb4b1b273fe6e-files.json --sbom tools/runtime/php/extras/sbom/php.cdx.json --output artifacts/t08-minimal/release-source-comparison.json
```

archive가 없으면 공식 고정 URL에서 내려받고 공식 공개 SHA-256 `dcf93923b0e38e0d5252b25b796520b5a47761a08b3488b6d0b5d1115764d3f0`과 대조합니다. 실제 로컬 조사에서 tar.xz는 13,862,944바이트이며 libmbfl 98파일/timelib 20파일의 경로·내용이 pinned Git 원문과 동일했습니다. 두 집계는 여전히 in-ZIP SBOM과 달랐습니다. libmbfl은 Full/SBOM 후보이며 최소 팩에 mbstring 확장을 배포하지 않습니다. timelib은 활성 date 코어 관련 항목이므로 최소 팩에서도 미해결 사항입니다. 원인을 미확정으로 남기며, 바이너리 손상이나 실제 라이선스 위반을 단정하지 않습니다.

## 보존한 실제 결과

[Windows 실행 41974331131](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/41974331131)은 head `fd0101dcbb031f9b89e6379df9fc0d98cf6c106c`, merge checkout `086d71d9d0153496fdb43cc6ae9e8f7f56feb955`를 검증했습니다. Full PHP 79파일/96,050,460바이트에서 MinimalCli 7파일/13,043,511바이트로 86.42% 감소했습니다. 전체 팩 파일 수는 프로필 선택 증거를 포함해 Full 866/MinimalCli 794입니다. 이 수치는 최종 앱 ZIP 크기가 아닙니다.

팩 비교 24건·annotation 9건·오프라인 각 24건과 원본 보호, 빌드 경고·오류 0, 서비스 61건·화면 21건·실제 GUI 36건이 통과했습니다. Windows 시스템 UCRT에 대한 API-set 해결도 backing module 경로·SHA로 확인했습니다. 소스 6개/31,375파일은 Git raw blob 검증 후 아카이브로 만들었고 PHP checkout의 7개 ident 확장은 원문과 별도 기록했습니다. bundled digest 23개 중21개만 일치해 **source gate 및 전체 workflow는 실패**, T08은 부분 검증입니다.

[요약](evidence/summary.json), [원본 비교](evidence/comparison.json), [코어 소스 결과](evidence/core-source.json), [공식 tar.xz 조사](evidence/release-source-comparison.json), [실행·아티팩트·근거 해시](evidence/provenance.json)를 보존했습니다. 6개 원본 소스 ZIP과 모든 파일 inventory·GUI PNG·binlog는 해당 7일 artifact에 있습니다. Git에는 그 해시와 재현 절차를 보존하며 최종 배포의 영구 소스 제공을 완료 처리하지 않습니다. 이후 근거/문서·로컬 조사 도구 반영은 제품과 Windows 검증 실행 경로를 변경하지 않습니다.
