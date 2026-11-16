# T17 — PHP 7.4/8.0/8.1 목표 검증 후보

이 파일과 커밋은 **Windows 검증을 시작하기 위한 후보**입니다. `tools/engine-pack/sources.json`의 새 목표는 Windows 실측·원본 보호·오프라인·기존 회귀가 통과하기 전까지 제품에서 검증 완료/공식 지원이라고 선언하지 않습니다. main 및 v0.1.0은 변경하지 않습니다.

## 고정 구성

- 실행 PHP: 공식 PHP 8.4.27-1 NTS x64, 기존 SHA-256 고정.
- PHPCompatibility 10.0.0-alpha2 + PHPCS 4.0.4 + PHPCSUtils 1.2.3 + composer installer 1.1.0: 기존 lock을 유지.
- 테스트 대상: PHP 7.4/8.0/8.1 신규, 8.2/8.3/8.4/8.5 회귀.
- 구성 계약: pack schema 1, report profile `phpcs4-json-v1`.

## 버전 경계 fixture

| 목표 | 양성 fixture | 대조할 상위 목표와 음성 |
|---|---|---|
| PHP 7.4 | `php74_named_argument.php`: PHP 8.0 명명된 인자 | 동일 파일 @ 8.0 |
| PHP 8.0 | `php80_readonly_property.php`: PHP 8.1 readonly 속성 | 동일 파일 @ 8.1 |
| PHP 8.1 | `php81_readonly_class.php`: PHP 8.2 readonly class | 동일 파일 @ 8.2 |

각 사례의 기대 규칙 코드·줄·진단 형식은 `tests/fixtures/cases.json`에 보관합니다. 공통 양성/음성 검증은 기존 `mysql_removed.php`와 `mysql_control.php`를 신규 목표에도 적용합니다.

## 실제 검증 절차 (Windows x64)

동일한 CI 작업 디렉터리 안의 새 checkout에서 준비용 PHP/Composer와 Python을 이용합니다. 검사 대상 PHP 소스에는 의존성 설치 또는 실행을 하지 않습니다.

```powershell
./tools/engine-pack/Prepare-Pack.ps1
python tests/t04/test_pack_contract.py tools/runtime
python tests/t10/prepare_packs.py tools/runtime artifacts/t10-packs
dotnet run --project tests/t10/T10.Verification.csproj -c Release -- "$pwd" "$pwd/artifacts/t10-packs"
dotnet run --project tests/t05/T05.Verification.csproj -c Release -- "$pwd" "$pwd/tools/runtime"
dotnet run --project tests/t07/T07.Verification.csproj -c Release -- "$pwd" "$pwd/tools/runtime"
```

T13/T14 CI workflow와 공통 회귀에서 검사 대상 원본 목록·해시 불변, 검증된 `testVersion`, JSON/종료 코드/파일 수, 설정 격리·부트스트랩 무실행·오프라인 재현을 확인합니다. T10은 검증 팩의 목표 수와 정렬 순서에 관계없이 기존 `8.2/8.3` reduced 대조를 유지합니다.

실패 시에는 원시 결과 및 규칙 지원 한계를 기록하고 해당 목표를 공개 팩에 활성화하지 않습니다. Windows Server 2022 CI의 통과를 깨끗한 Windows 11/공개 배포 검증으로 대체하지 않습니다. 앱 관리 UI·온라인 카탈로그 및 Release 게시에는 관여하지 않습니다.
