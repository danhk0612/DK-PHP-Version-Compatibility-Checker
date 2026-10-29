# T05 Windows 실행·회귀 검증

검증 전용 .NET 10 콘솔입니다. 새 NuGet/테스트 프레임워크 없이 제품 프로젝트를 참조합니다. 제품의 진입점·XAML은 변경하지 않습니다.

Windows x64/SDK 10.0.403에서 T04 절차로 `tools/runtime`을 준비한 뒤 저장소 루트에서 실행합니다.

```powershell
dotnet restore tests/t05/T05.Verification.csproj
dotnet build tests/t05/T05.Verification.csproj -c Release --no-restore
dotnet run --project tests/t05/T05.Verification.csproj -c Release --no-build -- $PWD "$PWD/tools/runtime"
```

실제 manifest 목표별 fixture 기대 결과·원본 파일/디렉터리/SHA-256 불변, 입력 설정/ini 격리, 재귀·확장자·링크/reparse 제외·읽기 거부, 실제 팩 정합성과 복원, JSON 원값/위치/총수/종료·실패·부분 결과를 확인합니다. 링크·ACL 생성은 Windows 검증 환경에서 실제 양성 대조가 성공해야 통과합니다. 기본 runtime과 저장소 fixture는 수정하지 않고 자체 임시 복사본만 변경합니다. SUBST 회귀는 자체 관리 fixture를 빈 드라이브 문자에 매핑하고 TMP/TEMP도 그 fixture로 잠시 지정하여 물리적인 소스/작업 경로 중첩 거부를 확인합니다. 실제 전역 temp 폴더를 별칭으로 검사하지 않으며 환경 변수와 드라이브 매핑을 finally에서 복원합니다.

프로세스 회귀는 별도 임시 팩 복사본의 PHPCS 진입점에 자체 작성한 PHP helper를 배치하고 해당 manifest·증거 파일 해시를 맞춘 **합성 테스트 도구**를 사용합니다. stdout/stderr 각 1MiB의 동시 수집 및 single-flight/Dispose의 자식 종료·자체 작업 폴더 정리를 확인합니다. 이 팩의 규칙/버전 지원 검증을 주장하지 않으며 검사 대상 PHP를 실행하지 않습니다. 제품에 임의 명령 실행 seam을 추가하지 않습니다.

실측은 `artifacts/t05/summary.json`, `T05_SUMMARY_JSON=` 콘솔 출력에 남깁니다. 하나라도 실패하면 exit 1입니다. 테스트 구현 자체는 실행 성공 근거가 아니며 실제 Windows CI 결과를 별도 확인합니다. GUI 연결(T07), 깨끗한 Windows 11 배포(T09), 팩 교체/복원 통합(T10)은 이 검증으로 완료 처리하지 않습니다.
