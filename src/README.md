# 제품 소스와 빌드

단일 WPF 앱은 `Dk.PhpCompatibilityChecker/Dk.PhpCompatibilityChecker.csproj`입니다. .NET SDK **10.0.403**을 설치한 Windows x64에서 저장소 루트로부터 실행합니다. `global.json`은 다른 SDK로 자동 전환하지 않습니다.

```powershell
dotnet restore src/Dk.PhpCompatibilityChecker/Dk.PhpCompatibilityChecker.csproj
dotnet build src/Dk.PhpCompatibilityChecker/Dk.PhpCompatibilityChecker.csproj -c Release --no-restore
dotnet run --project src/Dk.PhpCompatibilityChecker/Dk.PhpCompatibilityChecker.csproj -c Release --no-build
```

T03은 빈 기본 창과 공유 데이터 계약만 제공합니다. 제품 목표 OS는 Windows 11 x64입니다. CI의 Windows Server 2022 빌드·창 기동 확인은 깨끗한 Windows 11 배포 검증을 대체하지 않습니다. self-contained publish/ZIP은 T09에서 준비합니다.

`Models/`는 T05 실행 연동과 T06 화면이 공유합니다. `ScanRequest.TargetPhpVersion`은 문자열이며 특정 버전 enum/목록은 없습니다. 목표 목록의 manifest 생성은 T04, 소비는 T05/T06입니다. `ScanIssue`는 엔진 type/severity/source/message/fixable을 보존하고 `ScanResult`는 선택 파일 수와 nullable 처리 파일 수, 실패 이유와 엔진 버전을 구분합니다. 런타임 검증·JSON 변환은 아직 없습니다. 공유 모델·프로젝트 설정 변경은 T05/T06 담당 간 조정해야 합니다.

CI와 동일한 기본 창 검증은 빌드 후 `./tests/t03/Test-Window.ps1`로 실행합니다. 결과는 `artifacts/t03/summary.json`입니다. 창의 실제 표시·응답·크기·정상 종료를 검사하며 결과 GUI나 검사 기능 테스트는 아닙니다. 새 프레임워크·별도 프로젝트·엔진 추상화는 도입하지 않았습니다.

## T03 실제 검증 근거

[Windows Actions 실행](https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/actions/runs/40796379993)은 Windows Server 2022 x64(10.0.20348), SDK 10.0.403에서 restore/build에 성공했습니다. 빌드 경고 0·오류 0, 실제 창 960×640 표시·응답·정상 종료(exit 0)를 확인했습니다. 검증 head는 `7aca9712a98602eb86640d82bd35a1bc81d8eb8f`, 실제 checkout은 PR merge ref `7d6c208100acb041b83d1cbf93ccb24f3bba4a54`입니다.

[실측 요약](../tests/t03/windows-summary.json)에 실행 ID·OS·SDK·실행 파일 해시를 보존했습니다. CI artifact에는 dotnet-info·build.binlog·summary가 있으며 보관 기간은 7일입니다. 최종 문서/증거 커밋은 앱·워크플로·검증 스크립트를 변경하지 않으므로 재실행하지 않았습니다.
