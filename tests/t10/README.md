# T10 개발용 팩 교체·복원 자동 검증

T08 배포 적합성이 아직 미확정이므로 이 시험의 성공은 개발용 동작 근거입니다. T10 최종 완료나 공개 배포를 승인하지 않습니다.

검증된 동일 엔진 팩에서 기존 목표 8.4만 제거한 `reduced`와 기존 전체 목표를 가진 `full`을 만듭니다. 새 PHP 버전이나 엔진 규칙은 추가하지 않습니다. packId, 목표별 실제 근거 및 report hash를 새 파생 구성에 맞춰 함께 고정하고 원본 팩 바이트 불변을 확인합니다. 엔진/PHP 파일은 그대로입니다.

Windows에서 SDK10.0.403 및 준비 도구를 설치한 새 checkout으로 실행합니다.

```powershell
./tools/engine-pack/Prepare-Pack.ps1
python tests/t10/prepare_packs.py tools/runtime artifacts/t10-packs
dotnet run --project tests/t10/T10.Verification.csproj -c Release -- "$pwd" "$pwd/artifacts/t10-packs"
```

실제 표시된 WPF 창과 제품의 `LoadPackAsync`, 검사 버튼 및 `ScanService`를 사용합니다. 창과 소유 서비스를 닫고 같은 활성 경로의 디렉터리를 교체한 뒤 새 창에서 목록/실행을 확인합니다. reduced→full→reduced 복원에서 동일 애플리케이션 assembly SHA를 보존합니다. 각 단계의 실제 fixture 결과, 양성/음성·기존 목표 결과, 복원 결과, 원본 바이트와 파생 입력 팩 불변을 대조합니다.

미검증 목표/파일 해시 손상/미지원 schema/profile/부분 혼합은 제품 로더와 GUI의 빈 목표·비활성 검사 상태로 거부되어야 합니다. 각 단계에서는 같은 검증 실행 파일을 별도 OS 프로세스로 다시 실행하여 실제 WPF 창·같은 제품 assembly SHA·목표 목록·검사 버튼 실행을 확인합니다. 검증 프로세스 안의 창/서비스 종료재개도 함께 수행합니다. 배포용 제품 EXE의 기본 설치 경로를 통한 시작은 배포 검증에 남습니다.

[Windows workflow](../../.github/workflows/t10-pack-exchange.yml)는 활성 PHP 경로에 outbound 차단 규칙을 설치하고 finally에서 제거합니다. 별도 연결 양성 대조는 기본 팩 준비 단계의 근거를 사용하며 이 작업에서 새로 수행하지 않습니다. Windows Server2022 CI는 깨끗한 Windows11의 CRT·ZIP 배포 시험이 아닙니다. 입력 fixture는 자체 작성 자료이며 대상 PHP 파일을 실행하지 않습니다.

실행 결과는 `artifacts/t10/summary.json`, 파생 manifest는 `artifacts/t10-packs/{reduced,full}/manifest.json`입니다. 실제 Windows 실행 전에는 통과로 표기하지 않습니다.
