# T14 Windows 검증

T10의 검증된 reduced/full 개발용 팩 디렉터리를 사용합니다. full은 reduced의 모든 목표와 하나 이상의 새 목표를 포함해야 합니다. 테스트는 이 디렉터리에서 ZIP을 만들고, 주입한 HTTP 응답으로 앱의 설치 경로를 실행합니다.

```powershell
dotnet run --project tests/t14/T14.Verification.csproj -c Release -- "$PWD/artifacts/t10-packs" "$PWD/artifacts/t14"
```

출력 경로는 없어야 합니다. 결과는 출력 경로의 `summary.json`에 기록됩니다. ZIP과 설치 작업 폴더는 테스트 입력 및 임시 산출물입니다.

- 정확한 ZIP 설치와 별도 프로세스의 시작 시 선택 검증
- ZIP/manifest 해시 불일치, 경로 탈출, 목록과 검증된 목표 불일치, 소스 경로 겹침 거부
- 전송 도중 취소 및 설치 실패 후 기존 활성 팩·포인터·소스 유지
- 새 팩 설치 후 현재 검사 서비스 유지, 다음 프로세스의 새 목표 로드 및 실제 검사
- 현재 팩이 손상된 경우 이전 검증 팩으로 시작하고 목표를 제거하는 설치 거부
- WPF 다운로드 확인 및 다시 시작 검증
