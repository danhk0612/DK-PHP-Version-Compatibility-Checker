# T08 Windows 배포 구성·라이선스 근거 수집

승인된 SDK 10.0.403과 고정 엔진 팩을 사용하는 **관리자용 조사 절차**입니다. 제품 또는 검사 대상 폴더에 설치·수정을 하지 않습니다. 준비된 `tools/runtime`와 `artifacts/t08`이 없는 새 Windows x64 checkout에서 실행합니다.

```powershell
./tools/engine-pack/Prepare-Pack.ps1
./tools/license-audit/Collect-Windows.ps1
```

수집기는 제품 csproj를 바꾸지 않고 `dotnet publish -c Release -r win-x64 --self-contained true`를 실행합니다. single-file과 trimming은 끄고 산출물 전체를 조사합니다. 이는 배포 ZIP 생성이나 공개 배포, 깨끗한 Windows 11 기동 검증이 아닙니다.

`artifacts/t08/`에는 다음 근거가 저장됩니다.

- `summary.json`, `publish-result.json`, `dotnet-info.txt`, `publish.log`/binlog: 실제 OS·SDK·checkout·명령·결과. 요약만으로 라이선스 적합성을 합격 처리하지 않습니다.
- `publish-files.json`, `dotnet-packages.json`, `licenses/dotnet/`: 모든 self-contained 파일의 SHA-256·버전·회사 표시와 정확한 NuGet runtime/host 패키지의 원본 license/third-party notices/nuspec, 패키지 및 payload 해시. 파일이 동일한 패키지 payload와 SHA로 연결합니다. SDK가 이름·내용을 바꾼 apphost와 자체 생성 파일은 그 출처를 별도로 표시합니다. 매핑이 없는 파일은 실패로 남깁니다.
- `dependency-data/`: 실제 project.assets/deps/runtimeconfig, Composer lock/installed 데이터. runtime 패키지 버전은 기대값을 꾸며 넣지 않고 실제 deps에서 읽습니다. WindowsDesktop 패키지에 빠진 WPF/WinForms 고지는 해당 nuspec의 정확한 dotnet/dotnet source commit에서 별도 보충하며 패키지 원본과 구분합니다. cache에 nupkg가 없으면 동일 ID/버전의 공식 NuGet flat-container에서 고지 대조용으로 확보하고 출처/해시를 기록합니다.
- `pack-files.json`, `pack-manifest.json`, `copied-pack-documents.json`, `licenses/pack/`: 전체 PHP ZIP·Composer vendor를 포함한 팩의 파일·SHA·버전과 license/readme/copyright/notice 원본, `php/extras/sbom/*` 원본. PHP 디렉터리 소속이라는 사실이 모든 파일에 PHP 라이선스를 적용한다는 뜻은 아닙니다.
- `php-info-no-ini.txt`, `php-imports-*.txt`, `php-loaded-modules.json`: `-n`과 INFO_GENERAL/CONFIGURATION/MODULES/LICENSE만 선택한 CLI의 기능과 실제 로딩 모듈/CRT 위치·버전·SHA. 모듈 관측은 수집기가 만든 `sleep(15);` CLI 조각만 실행하며 검사 대상 소스는 실행하지 않습니다. `-n`에서 로딩되지 않는 확장도 전체 ZIP에 포함된 배포 검토 대상입니다. 전체 ZIP의 모든 EXE/DLL에 대해 deplister 결과의 종료 코드도 보존하며 결과가 없다고 의존성이 없다고 해석하지 않습니다.
- `corresponding-source.json`, `corresponding-source/`: LGPL 두 패키지의 lock source commit을 shallow fetch한 전체 tracked checkout에서 만든 ZIP과 모든 소스 파일·vendor 대조. `git archive`/GitHub source ZIP의 export-ignore로 빠지는 tests/build/숨김 tracked 파일도 포함하고 `.git`과 untracked 파일은 제외합니다. core.autocrlf 변환을 끄고 실제 Git blob ID와 모든 checkout byte를 대조합니다. 설치 vendor의 모든 파일도 checkout의 같은 경로와 SHA가 일치해야 합니다. 차이가 있으면 완료로 숨기지 않습니다.

CI artifact는 7일 보관하며 publish 바이너리와 다운로드 cache는 업로드하지 않습니다. 라이선스 고지와 LGPL 대응 소스 ZIP은 근거로 업로드합니다. 최종 배포 때는 승인된 실제 ZIP과 같은 구성의 고지·대응 소스를 다시 생성·대조해야 하며, CI artifact 링크만을 영구적인 소스 제공 수단으로 사용하지 않습니다. 수집기 실행 성공은 파일·출처 대조의 성공이며 법적 충돌·사용 권한·재배포 의무 이행의 최종 판정을 대신하지 않습니다.

## 실측과 판정

[Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/41941111848)에서 Windows Server 2022 x64 / SDK10.0.403, self-contained publish 경고·오류 0, 파일 400개 출처 매핑/팩 865개 inventory와 엔진 LGPL vendor 302개 바이트 대조를 통과했습니다. 검증 head `5290d3b7a45e6c06444d9d6011c0b691d61797b6`, 실제 checkout merge ref `2db833a86dc4dcdb2f4b8b89f90f1bc4e77b5eb4`입니다. 후속 고지·근거·상태 커밋은 수집기/제품/감사 job의 실행 단계를 변경하지 않습니다. 일회성 이전 아티팩트 정리 job은 성공 후 제거했습니다.

[원본 실측](../../tests/t08/evidence/summary.json), [전체 근거 해시](../../tests/t08/evidence/provenance.json), [배포 판정·미해결 조건](../../LICENSING.md)을 확인합니다. `.NETCore`/`WindowsDesktop`/host는 실제 10.0.14이며 Composer 생성기는 실측2.11.1입니다. 원본 engine license5개와 source license를 실제 생성기 버전에서도 대조했습니다.

PHP `deplister`는 BindImageEx BOOL을 반환하여 exit1이 TRUE입니다. 각 import의 OK/NOTFOUND를 별도로 보존하며 Firebird PDO의 fbclient.dll NOTFOUND도 그대로 기록했습니다. 64개 이미지 관측이나 소스 ZIP2개 생성만으로 native 출처·고지·재링크 의무가 해결된 것은 아닙니다. 환경변수/서버 변수는 수집하지 않습니다. PHP 코어 BCMath가 -n에서도 활성임을 확인했으므로 최소 CLI 후보 역시 대응 소스 검토가 필요합니다.

Git에는 원문 고지·실측/파일/소스 대조 메타데이터를 보존하고, 생성한 대응 소스 ZIP의 해시를 기록했습니다. ZIP2개와 전체 로그/binlog는 7일 artifact에 있습니다. 최종 배포에서는 이를 재생성·대조하고 같은 배포 위치에서 지속 제공해야 합니다.

## 승인된 B안의 PHP 코어 소스 검토

최소 CLI 비교·회귀와 `Collect-PhpCoreSource.py`의 정확한 명령은 [tests/t08-minimal](../../tests/t08-minimal/README.md)을 따릅니다. 안전한 PHP 정보/알고리즘 프로브와 실제 in-ZIP CDX를 입력합니다. PHP 태그·고정 커밋과 선택한 코어 의존성 전체 tracked 경로의 변환 전 Git blob 바이트, 원문 고지·빌드 자료 및 공식 bundled-source digest를 기록합니다. Windows configure가 만드는 헤더 2개는 원본 소스와 분리해 입력 Git blob/레시피/출력 SHA를 기록하고, 원본21/23과 생성물 포함23/23을 함께 남깁니다. 코어 소스 수집 성공은 native rebuild/relink·모든 바이너리 대응·재배포 적합성 완료를 의미하지 않습니다. 최종 배포에서는 재현 가능한 소스 아카이브와 고지·재구성 자료를 같은 배포와 지속 제공해야 합니다.


## 소스·고지 동반 ZIP 준비

실제 core source 및 engine source 수집이 완료된 뒤 다음 명령을 사용합니다. 기존 출력을 덮어쓰지 않습니다.

```powershell
python tools/license-audit/Collect-BuildSources.py --output artifacts/t08-build-source
python tools/license-audit/Prepare-SourceSidecar.py --core-audit artifacts/t08-core-source --engine-audit artifacts/t08 --build-audit artifacts/t08-build-source --output artifacts/t08-source-sidecar
```

[전용 Windows workflow](../../.github/workflows/t08-source-sidecar.yml)는 팩 준비→실제 publish/엔진 source 수집→core/build source 수집→동반 ZIP 순으로 실행합니다. source archive와 inventory의 기록 SHA, collector의 Git-byte 검증 및 vendor 대조 결과를 확인한 후 소스·원문 고지·현재 프로젝트 레시피를 묶습니다. core 원본6개·LGPL engine2개·builder/SDK2개와 별도 설정 생성 ZIP을 포함하며 `--build-audit`를 생략한 경우에는 buildRecipeSourcesIncluded=false로 기록합니다. 생성 결과가 법적 배포 승인이나 영구 소스 제공을 뜻하지 않습니다. PHP/Zend 후속 조건 선택은 사용자 승인으로 license-election.json에 반영했습니다. 최종 바이너리와 동반 ZIP을 같은 배포 위치에서 지속 제공하고 각각 SHA를 고정해야 합니다.

재링크 workflow가 사용하는 `tests/t08-minimal/evidence/native-inputs.json`(의존성 후보 URL·고정 SHA)과 `tests/t08-minimal/check_relinked_php.py`, 후보 준비에 필요한 `tests/t09/`의 `check_pair.py`·`check_package.py`·`WINDOWS11_TEST.md`·`check_source_sidecar.py`도 동반 ZIP에 포함합니다. 페어링 검증은 여섯 파일의 존재와 현재 checkout 바이트 일치를 확인합니다. 이 목록의 해시는 현재 서버에서 수집한 후보의 해시이며 과거 공식 빌드 입력과 동일하다는 증거로 승격하지 않습니다. 동반 ZIP은 모든 일반 GUI/서비스 CI 시험 코드를 포함하지 않으므로 전체 CI 재실행에는 해당 저장소 checkout을 사용합니다.
