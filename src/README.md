# 제품 소스와 빌드

단일 WPF 앱은 `Dk.PhpCompatibilityChecker/Dk.PhpCompatibilityChecker.csproj`입니다. .NET SDK **10.0.403**을 설치한 Windows x64에서 저장소 루트로부터 실행합니다. `global.json`은 다른 SDK로 자동 전환하지 않습니다.

```powershell
dotnet restore src/Dk.PhpCompatibilityChecker/Dk.PhpCompatibilityChecker.csproj
dotnet build src/Dk.PhpCompatibilityChecker/Dk.PhpCompatibilityChecker.csproj -c Release --no-restore
dotnet run --project src/Dk.PhpCompatibilityChecker/Dk.PhpCompatibilityChecker.csproj -c Release --no-build
```

T06 기본 창은 입력·결과 화면을 제공하고 T05가 공유 데이터 계약과 실제 검사 서비스를 제공합니다. 제품 목표 OS는 Windows 11 x64입니다. CI의 Windows Server 2022 빌드·창 기동 확인은 깨끗한 Windows 11 배포 검증을 대체하지 않습니다. self-contained publish/ZIP은 T09에서 준비합니다.

`Models/`는 T05 실행 연동과 T06 화면이 공유합니다. `ScanRequest.TargetPhpVersion`은 문자열이며 특정 버전 enum/목록은 없습니다. 목표 목록의 manifest 생성은 T04, 소비는 T05/T06입니다. `ScanIssue`는 엔진 type/severity/source/message/fixable을 보존하고 `ScanResult`는 선택 파일 수와 nullable 처리 파일 수, 실패 이유와 엔진 버전을 구분합니다. T05의 Services/가 팩 로딩·검사 실행·JSON 변환을 담당합니다. 공유 모델·프로젝트 설정 변경은 T05/T06 담당 간 조정해야 합니다.

CI와 동일한 기본 창 검증은 빌드 후 `./tests/t03/Test-Window.ps1`로 실행합니다. 결과는 `artifacts/t03/summary.json`입니다. 창의 실제 표시·응답·크기·정상 종료를 검사하며 결과 GUI나 검사 기능 테스트는 아닙니다. 새 프레임워크·별도 프로젝트·엔진 추상화는 도입하지 않았습니다.

## T03 실제 검증 근거

[Windows Actions 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/40796379993)은 Windows Server 2022 x64(10.0.20348), SDK 10.0.403에서 restore/build에 성공했습니다. 빌드 경고 0·오류 0, 실제 창 960×640 표시·응답·정상 종료(exit 0)를 확인했습니다. 검증 head는 `7aca9712a98602eb86640d82bd35a1bc81d8eb8f`, 실제 checkout은 PR merge ref `7d6c208100acb041b83d1cbf93ccb24f3bba4a54`입니다.

[실측 요약](../tests/t03/windows-summary.json)에 실행 ID·OS·SDK·실행 파일 해시를 보존했습니다. CI artifact에는 dotnet-info·build.binlog·summary가 있으며 보관 기간은 7일입니다. 최종 문서/증거 커밋은 앱·워크플로·검증 스크립트를 변경하지 않으므로 재실행하지 않았습니다.

## T05 제품 코드 사용 계약

- `EnginePackLoader.LoadAsync(packDirectory)`는 schema/profile·전체 파일/해시·lock/설치 참조·검증 근거·목표를 대조하여 `EnginePack`을 반환합니다. `Targets`의 Version/VerificationRef/Limitations를 GUI가 사용하며 숫자를 별도 고정하지 않습니다. 신뢰된 팩 구성의 정합성 검증이며 발행자 인증이나 OS sandbox가 아닙니다.
- `await using var service = new ScanService(packDirectory);` 후 `await service.ScanAsync(request)`로 실행합니다. 동시 실행은 InvalidOperationException, Dispose 뒤 호출은 ObjectDisposedException입니다. 폴더/팩/엔진/결과 실패는 ScanResult.Failed로 반환합니다. 수명 종료 시 `await service.DisposeAsync()`로 소유 자식 프로세스 종료와 자체 임시 파일 정리를 기다립니다. 실제 WPF 종료는 T07에서 연결하여 Dispose와 실행 핸들러 완료를 기다립니다. Windows 검증 상태는 CURRENT_STATE.md를 따릅니다.
- 서비스는 검사마다 팩을 다시 검증합니다. .php/.inc를 읽기 전용으로 열거하고 reparse 경로는 제외 목록으로 알립니다. 루트/상위 링크·읽기 불가·소스/팩/작업 폴더 중첩은 실패입니다. Windows directory handle의 NT 경로도 비교하여 SUBST 드라이브 별칭 중첩을 검사하며 해석할 수 없으면 작업 폴더를 만들기 전에 실패합니다.
- 대상 밖 사용자 임시 영역의 file-list, 전용 ruleset/cwd, -n·ini 환경 격리·ignore-annotations·no-cache를 사용합니다. shell과 대상 PHP/Composer/bootstrap을 실행하지 않습니다. stdout/stderr는 동시에 raw stream으로 수집한 뒤 엄격한 UTF-8로 해석합니다.
- `PhpcsReportParser`는 원문 행·파일/전체 totals·선택 파일 대응과 PHPCS4의 exit bit(수정가능 1/나머지 2)를 대조합니다. 같은 줄 다른 규칙도 보존합니다. 실패/부분 결과를 Completed 0건으로 만들지 않습니다.
- ScanResult는 기존 positional 계약을 유지하며 ExcludedPaths(항상 목록), EngineExitCode(int?), EngineStandardError(string?) init 메타데이터를 추가했습니다. ProcessedFileCount의 null은 미확인입니다. stderr를 별도 엔진 ERROR/WARNING으로 집계하지 않습니다.

[검증 콘솔](../tests/t05/README.md)은 제품 프로젝트를 참조하는 별도 테스트 전용 콘솔이며 새 제품 계층·패키지·Python/Composer 런타임 의존성을 추가하지 않습니다. T05는 GUI/XAML을 변경하지 않았으며 T06이 화면·표시 모델을 추가했습니다. 실제 검사 GUI 응답·버튼 흐름은 T07에서 검증했으며 깨끗한 Windows 11/CRT 배포·팩 교체 통합 검증은 T09/T10에 남아 있습니다. 네트워크 공유의 여러 별칭 조합이나 외부 동시 파일 변경에 대한 완전한 격리를 보장하지 않습니다.

T05 [최종 Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/40830447517)은 SDK 10.0.403에서 제품/검증 콘솔 build 경고 0·오류 0, 회귀 61건 통과입니다. 검증 head는 `f08de1dae5d91474ca1672d0cd128bcc0b69c784`, 실제 checkout merge ref는 `522cc635c214f4ab5ddff7dc3c068111125773eb`입니다. [실측 요약](../tests/t05/evidence/summary.json)과 [팩 manifest](../tests/t05/evidence/pack-manifest.json), [해시·실행 근거](../tests/t05/evidence/provenance.json)를 보존했습니다. 이후 커밋은 문서/실측 보존만 변경하여 실행 코드는 재검증하지 않았습니다.

## T06 화면 계약

기본 MainWindow는 `AppContext.BaseDirectory/tools/runtime`의 검증된 엔진 팩을 비동기로 읽습니다. 빌드 출력에 runtime을 자동 복사하거나 새 다운로드/업데이트 UI를 추가하지 않습니다. 팩이 없거나 잘못되면 실제 실패 이유를 표시하고 목표 목록을 비웁니다. 개발자가 팩을 준비하는 절차는 tools/engine-pack/README.md를 따르며 최종 배포 배치는 T09에서 검증합니다.

`ScanPresentation`은 .NET 기본 INotifyPropertyChanged를 사용하는 단일 표시 모델입니다. `SetPack`이 받은 검증 목표 인스턴스만 선택할 수 있고, 기본 재귀는 켬입니다. 폴더/목표/재귀를 바꾸면 이전 집계·문제·실패·제외 목록을 초기화합니다. `ShowRunning()`은 입력을 잠그고 `ApplyResult(ScanResult)`는 결과의 요청과 표시 입력을 맞추며 원문 문제·nullable 처리 수·제외 경로를 보존합니다. 실패/부분 결과·NoFiles·정상 0건은 서로 다른 문구로 표시합니다. 짧은 검사 상태는 고정 헤더에 항상 표시하고 긴 설명·제외 경로·한계는 제한된 viewport에서 스크롤로 접근합니다.

입력 존재 확인과 `CanPrepareScan`은 실행 가능 입력의 표시 기준입니다. T07에서 검증 팩의 ScanService와 버튼을 연결했습니다. 유효 입력·서비스가 있고 로딩/종료 중이 아닐 때만 버튼을 활성화하며 클릭 핸들러에서도 중복 실행을 거부합니다. `ShowRunning()` 뒤 비동기 ScanAsync를 호출하고 결과를 `ApplyResult()`로 반영합니다. 폴더 선택은 WPF 기본 OpenFolderDialog를 사용하며 대상 프로젝트에 파일을 쓰지 않습니다.

`MainWindow(ScanPresentation)` 생성자는 검증 데이터 주입을 위한 직접 구성 경로로 자동 팩 로드를 하지 않습니다. 제품에 데모 모드·테스트 옵션은 없습니다. 테스트 콘솔이 별도 창 제목과 PNG 상단 띠로 검증 데이터임을 명시합니다. 기본 생성자는 실제 팩 부재 상태에서도 임의 목표나 가짜 결과를 만들지 않습니다.

[tests/t06 검증 안내](../tests/t06/README.md)에 실제 표시 창·컨트롤 바인딩·크기·스크롤 검증 범위와 남은 수동/통합 검증을 기록합니다.

T06 [Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/40966862548)은 build 경고·오류 0, 화면 표시 검증 21건 통과입니다. 검증 head `5cf36d114ea3347999d1568d8d03bc1ecb848bc3` / 실제 checkout merge ref `f70e6448e7d7a214286eca8db76609e0e5ed7ab0`. [실측 요약](../tests/t06/evidence/summary.json), [대표 화면](../tests/t06/evidence/completed-findings.png), [실행/아티팩트 해시](../tests/t06/evidence/provenance.json)를 보존했습니다. 캡처는 **검증용 표시 데이터**이며 실제 PHP 검사 결과가 아닙니다. 후속 문서·증거 보존 커밋은 실행 코드를 변경하지 않았습니다.

## T07 연결·수명 계약

`LoadPackAsync`는 UI 스레드에서 호출하고 검증된 팩의 RootDirectory에 ScanService를 연결합니다. 준비 중이나 검사 중에는 다른 팩 로드를 거부합니다. 기본 생성자의 시작 경로는 기존 `AppContext.BaseDirectory/tools/runtime`입니다. 최종 배포 시 해당 폴더 배치는 T09가 담당합니다.

Closing은 첫 닫기를 보류하고 창 입력을 잠근 뒤 팩 로드를 취소·대기하고 소유 ScanService.DisposeAsync와 검사 핸들러 완료를 기다립니다. 이후 다시 닫으며 종료 이후 팩·결과를 반영하지 않습니다. 정리 실패는 창에 실패 이유를 남기며 정상 종료로 숨기지 않습니다. 검사 취소 버튼·새 설정·프레임워크를 추가하지 않았습니다.

[통합 검증](../tests/t07/README.md)은 실제 버튼과 엔진을 연결하며 기존 T06의 합성 표시 검증과 구분합니다. Windows 실행 완료 여부와 남은 검증은 CURRENT_STATE.md에 기록합니다.

T07 [Windows 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/41038802788)에서 build 경고·오류 0, 실제 폴더 선택/취소·목표별 엔진·원본 보호·응답·종료 정리 36건을 통과했습니다. [T06 화면 회귀](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/41038803221) 21건도 통과했습니다. 검증 head `ac175da08b2f66d19fd18ca63bde7fc585790a2e` / checkout merge ref `b33b14f8e21784d9483908328166fc3a34474577`입니다. [실측](../tests/t07/evidence/summary.json)과 대표 PNG/해시를 보존하며 후속 상태·증거 보존 커밋은 실행 코드를 변경하지 않습니다.
