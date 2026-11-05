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
- `php-info-no-ini.txt`, `php-imports-*.txt`, `php-loaded-modules.json`: `-n` CLI의 기능과 실제 로딩 모듈/CRT 위치·버전·SHA. 모듈 관측은 수집기가 만든 `sleep(15);` CLI 조각만 실행하며 검사 대상 소스는 실행하지 않습니다. `-n`에서 로딩되지 않는 확장도 전체 ZIP에 포함된 배포 검토 대상입니다. 전체 ZIP의 모든 EXE/DLL에 대해 deplister 결과의 종료 코드도 보존하며 결과가 없다고 의존성이 없다고 해석하지 않습니다.
- `corresponding-source.json`, `corresponding-source/`: LGPL 두 패키지의 lock source commit을 shallow fetch한 전체 tracked checkout에서 만든 ZIP과 모든 소스 파일·vendor 대조. `git archive`/GitHub source ZIP의 export-ignore로 빠지는 tests/build/숨김 tracked 파일도 포함하고 `.git`과 untracked 파일은 제외합니다. core.autocrlf 변환을 끄고 실제 Git blob ID와 모든 checkout byte를 대조합니다. 설치 vendor의 모든 파일도 checkout의 같은 경로와 SHA가 일치해야 합니다. 차이가 있으면 완료로 숨기지 않습니다.

CI artifact는 7일 보관하며 publish 바이너리와 다운로드 cache는 업로드하지 않습니다. 라이선스 고지와 LGPL 대응 소스 ZIP은 근거로 업로드합니다. 최종 배포 때는 승인된 실제 ZIP과 같은 구성의 고지·대응 소스를 다시 생성·대조해야 하며, CI artifact 링크만을 영구적인 소스 제공 수단으로 사용하지 않습니다. 수집기 실행 성공은 파일·출처 대조의 성공이며 법적 충돌·사용 권한·재배포 의무 이행의 최종 판정을 대신하지 않습니다.
