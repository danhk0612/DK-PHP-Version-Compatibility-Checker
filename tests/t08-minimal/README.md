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
python tools/license-audit/Collect-PhpCoreSource.py --sbom tools/runtime/php/extras/sbom/php.cdx.json --php-info artifacts/t08-minimal/comparison/php-info-no-ini.txt --output artifacts/t08-minimal/core-source
```

`compare_pack.py`는 stdlib만 사용합니다. 두 팩의 schema1/해시/lock/목표 계약, 기존 Full 전체 inventory 불변, 허용 목록과 보존 PHP 파일 바이트, 서로 다른 packId, 양쪽 24개 fixture·9개 annotation·24개 오프라인 대조, 설정 격리·원본 불변을 확인합니다. PE 실행 파일은 php.exe/php8.dll만 허용하고 실제 유지/제외 파일 및 크기 감소를 기록합니다. 메타데이터로 보존하는 원본 SBOM은 전체 입력 ZIP의 후보 목록이며 축소 팩의 정확한 구성 요소 목록으로 부르지 않습니다.

PHP 프로브는 ini를 배제하고 최소 PHP 및 Windows 시스템 경로만 PATH에 둡니다. 설치된 setup-PHP 디렉터리가 누락 DLL을 대신 공급하지 않게 합니다. 원본 Full의 deplister도 DLL이 없는 별도 임시 디렉터리로 복사하여 최소 후보 두 PE의 import를 관측합니다. upstream BindImageEx BOOL의 TRUE인 exit1과 개별 OK/NOTFOUND를 구분합니다. 환경/서버 변수 없이 제한된 phpinfo와 password_algos/확장 목록을 기록합니다.

[Windows workflow](../../.github/workflows/t08-minimal-cli.yml)는 Full→기본 MinimalCli 준비/비교 후 기존 T05 서비스 61건·T06 화면 21건·T07 실제 GUI 36건을 같은 최소 팩에서 실행합니다. 회귀 실행 PATH도 setup-PHP 없이 dotnet과 Windows 시스템 경로로 제한합니다. 이 스크립트를 작성한 것만으로 실제 Windows 통과를 선언하지 않습니다.

코어 소스 수집은 고정된 PHP와 실제 코어 의존성의 전체 tracked checkout·Git blob 바이트를 대조해 소스 ZIP/원문/빌드 자료를 보존하는 관리 절차입니다. 사용자 검사 대상 소스를 실행하거나 컴파일러/라이브러리를 설치하지 않습니다. 실제로 실행하지 않은 native rebuild/relink, 전체 대응 소스 이행, 영구적인 소스 제공을 검증 완료로 표현하지 않습니다. LGPL인 BCMath와 libiconv는 최소 파일 구성에서도 남아 있습니다.

전체 로그·소스 ZIP·원본 source inventory와 GUI 근거는 CI artifact에 7일 보관합니다. Full runtime/clone cache·배포 바이너리는 업로드하지 않습니다. 검증된 요약·해시·대표 근거를 Git에 보존하며 최종 배포에서는 같은 구성의 대응 소스·고지·재구성 자료를 지속 제공해야 합니다. Windows 11/CRT·최종 ZIP 배포는 T09, 실제 팩 교체/복원 통합은 T10의 별도 관문입니다.
