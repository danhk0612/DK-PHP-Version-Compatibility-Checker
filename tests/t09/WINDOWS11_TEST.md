# Windows 11 사용자 확인 절차

사용자가 승인한 PHP/Zend 후속 조건과 고지/소스 묶음을 포함한 로컬 자가사용 시험 후보를 준비합니다. `paired-candidate.json`의 `scopePassed`와 `candidateForUserTesting`이 true인 경우 아래 시험을 진행할 수 있습니다. 공개 배포/Release 승인은 별도이며 `publicReleaseApproved=false`입니다.

## 로컬 후보 생성: 단일 진입 명령

새 checkout에서 PowerShell 7을 열고 아래 명령을 실행합니다. 준비에는 Windows x64, .NET SDK 10.0.403, Python 3.12 이상, Git, 인터넷이 필요합니다. PHP·Composer 사전 설치는 필요하지 않습니다. 준비 명령이 공식 PHP ZIP과 Composer 2.11.1을 고정 SHA-256으로 검증한 뒤 저장소 artifacts 아래 임시 준비 폴더에서만 사용하고 종료 시 제거합니다. 사용자 PATH·PHP 설정은 실행 중에만 변경하며 종료 시 복원합니다. 이 준비용 전체 PHP는 제품의 최소 CLI 팩에 포함하지 않습니다. 대응 소스의 checkout은 긴 저장소 경로 영향을 피하도록 시스템 TEMP에서 수행하고 정리합니다. 기존 출력은 덮어쓰지 않으므로 재시도 시 기존 artifacts 출력 폴더를 보존용 이름으로 옮기거나 새 checkout을 사용합니다. 팩 준비의 방화벽 검증 때문에 **준비 명령에만 관리자 권한**이 필요합니다. 생성된 앱의 기본 실행/검사는 일반 사용자로 수행합니다. Visual Studio C++ 컴파일러는 이 후보 생성에 필요하지 않습니다.

```powershell
pwsh -NoProfile -File ./tools/packaging/Prepare-LocalTest.ps1
```

완료 시 표시되는 실행 ZIP, 소스·고지 ZIP, `paired-candidate.json`을 한 세트로 보존합니다. `paired-candidate.json`의 두 SHA-256을 실제 ZIP과 대조하고 압축 해제해 테스트합니다. 준비 명령은 로컬 파일만 생성하며 ZIP을 외부에 업로드하지 않습니다. 명령이 실패하면 오류와 `artifacts/`의 근거를 보존하고 실패 상태를 통과로 취급하지 않습니다.

## 검사 실패 원인 확인용 로컬 진단 빌드

이미 단일 준비 명령을 완료했고 `artifacts/t09/stage`가 있는 PC에서, 실패 상세 표시 등 코드 변경을 빠르게 확인할 때만 사용합니다. 저장소에서 `git pull --ff-only` 후 아래를 실행합니다. 기존 stage/ZIP/페어링 근거는 보존하며 새 폴더는 로컬 진단용 빌드입니다. 기존 소스 ZIP·paired-candidate.json은 이 새 앱 바이너리를 검증한 근거가 아닙니다. 정식 로컬 후보를 다시 만들 때는 단일 준비 명령과 페어링 검증을 사용합니다.

```powershell
$diagnostic = Join-Path $PWD ('artifacts/diagnostic-' + [guid]::NewGuid().ToString('N'))
Copy-Item -LiteralPath './artifacts/t09/stage' -Destination $diagnostic -Recurse
if (-not $?) { throw '기존 준비 폴더 복사 실패' }
dotnet publish ./src/Dk.PhpCompatibilityChecker/Dk.PhpCompatibilityChecker.csproj -c Release -r win-x64 --self-contained true '-p:PublishSingleFile=false' '-p:PublishTrimmed=false' -o $diagnostic
if ($LASTEXITCODE -ne 0) { throw '진단용 빌드 실패' }
& (Join-Path $diagnostic 'Dk.PhpCompatibilityChecker.exe')
```

실패하면 상태 영역에서 엔진 종료 코드·상세 오류·JSON 해석에 실패한 표준 출력 일부를 확인합니다. 긴 내용은 기존 상태 영역에서 스크롤합니다. 검사는 최대 100개 파일씩 새 PHP 프로세스로 순차 처리하며 사용자 php.ini를 읽지 않고 각 PHP 메모리 한도를 512MB로 명시합니다. 메모리 소진이 확인된 묶음만 더 작게 재검사하고, 파일 하나도 실패하면 그 경로와 유효한 일부 결과를 표시합니다. 일부 실패를 전체 완료로 표시하지 않습니다. 오류 내용은 읽기 전용 영역을 클릭한 뒤 Ctrl+A, Ctrl+C로 복사할 수 있습니다. 원본 소스나 진단 전체를 자동 전송하지 않습니다.

## 준비

1. Windows 11 x64의 버전·빌드, VC++ x64 런타임 설치 여부, 일반 사용자 계정 여부를 기록합니다. 가능하면 개발 도구/.NET SDK가 없는 VM 또는 PC를 사용합니다. .NET Desktop Runtime의 별도 설치 없이 후보가 실행되는지 확인합니다.
2. 제공된 ZIP SHA-256을 `Get-FileHash -Algorithm SHA256 <ZIP>`으로 확인합니다. 문서/고지/대응 소스 묶음과 버전이 같은지 확인합니다.
3. ZIP을 `다운로드\PHP 검사 한글 & 공백` 같은 새 폴더에 풀고 앱 EXE를 직접 실행합니다. 명령어 입력이나 관리자 권한이 기본 검사에 필요하면 실패로 기록합니다. .NET 런타임을 추가 설치해야 실행되면 실패입니다. CRT 전제가 충족되지 않은 경우의 실제 메시지를 기록합니다.

## 실행

현재 확인 범위: 2026-11-09 사용자 화면에서 목표 8.4·하위 포함·선택/처리 28,817파일·ERROR 550/WARNING 265·완료 표시를 확인했습니다. 바이너리/ZIP SHA는 수집하지 않았습니다. 동일 프로젝트의 수동 오프라인 재검사는 사용자 요청으로 **미실행·생략** 처리하며 다시 요구하지 않습니다. 기존 자동 오프라인 근거는 유지하고, 아래의 다른 미실행 항목을 통과로 간주하지 않습니다.

- 알려진 안전한 자체 PHP 시험 폴더를 선택하고 8.2/8.3/8.4 목표별로 검사합니다. 폴더 선택 취소, 하위 폴더 포함 켬/끔, 빈 폴더, 탐지 없는 파일 및 제거 함수가 있는 파일을 각각 확인합니다.
- 검사 중 창이 응답하는지, 오류/경고/파일 수와 결과 경로·줄 번호가 올바른지 확인합니다. 0건을 전체 런타임 호환 보장으로 설명하지 않는지 확인합니다.
- 오프라인 동작은 자동 검증 근거를 확인합니다. 실제 PC에서 동일 프로젝트를 다시 검사하는 수동 단계는 위 사용자 결정에 따라 생략합니다. 다운로드나 Composer 설치 없이 기본 검사할 수 있어야 한다는 요구는 유지합니다.
- 원본 시험 폴더의 파일/빈 디렉터리 목록 및 SHA-256을 전후 비교합니다. 검사로 파일이나 디렉터리가 생성되거나 내용이 바뀌면 실패입니다.
- 앱 파일을 읽기 전용 설치 위치에 두고 일반 사용자로 실행해 검사할 수 있는지 확인합니다. 사용자 원본 폴더에 캐시/결과가 생기면 실패입니다.
- 충분히 큰 자체 시험 폴더에서 결과 마지막 행까지 확인하고, 검사 중 창을 닫아 남은 PHP 프로세스가 없는지 확인합니다.
- 앱을 종료한 상태에서 승인된 팩 교체/복원 절차를 수행해 목록·실행 반영을 확인합니다. 임의의 미래 버전이나 손상된 팩을 정상 지원으로 표시하면 실패입니다.

## 결과 전달

후보 commit/ZIP SHA, Windows 빌드, CRT 설치 여부, 오프라인 여부, 단계별 통과/실패, 표시 문구와 화면을 기록합니다. 실제 프로젝트 소스·비밀정보는 업로드하지 않습니다. 미실행 항목은 미검증으로 표시합니다. 실패하면 오류 내용을 먼저 보존하고 프로그램/원본을 임의로 덮어쓰지 않습니다.

Windows Server CI의 패키징·창 표시 통과는 이 Windows 11 시험을 대체하지 않습니다. 최종 공개 Release/tag는 별도 승인 작업입니다.
