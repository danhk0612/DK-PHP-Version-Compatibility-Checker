# 검증된 엔진 팩 다운로드 계약

T14는 사용자가 GUI에서 엔진 팩 확인을 선택할 때 목록을 읽습니다. 현재 목표를 모두 유지하면서 새 목표를 추가하는 첫 팩을 표시하고, 사용자가 확인하면 다운로드합니다. 목록 순서는 발행자가 정합니다. 설치가 성공하면 현재 창은 기존 팩을 계속 사용합니다. 새 실행에서 설치된 팩을 읽고, 유효하지 않으면 이전 검증 팩과 기본 내장 팩을 순서대로 확인합니다. 검사와 시작에는 목록 서버 접속이 필요하지 않습니다. 설치 후 다음 실행까지 같은 창에서는 추가 다운로드를 잠급니다.

## 목록과 ZIP

제품에는 승인된 단일 카탈로그 HTTPS 주소를 지정합니다. 현재 주소는 미설정입니다. 제품용 주소는 이 저장소의 GitHub raw main 경로를 사용하며, 팩 ZIP과 대응 소스 ZIP은 같은 저장소의 태그별 GitHub Release 자산으로 제공하는 계약입니다. 실제 자산과 목록을 준비할 때 T08/T09 배포 조건 및 게시 승인을 적용합니다.

목록은 UTF-8 JSON 객체입니다. 최상위 `schemaVersion`은 1이며 `packs`는 배열입니다. 각 행에 다음 값이 필요합니다.

| 필드 | 값 |
| --- | --- |
| `packId` | 검증 팩 manifest의 `packId` |
| `packSchemaVersion`, `reportProfile` | 현재 팩은 1과 `phpcs4-json-v1` |
| `assetUrl`, `sourceUrl` | 정확한 태그별 팩 ZIP 및 대응 소스·고지 ZIP URL |
| `archiveBytes`, `archiveSha256` | 완성된 팩 ZIP의 바이트 수와 소문자 SHA-256 |
| `manifestSha256` | ZIP의 `runtime/manifest.json` 원본 바이트에 대한 소문자 SHA-256 |
| `phpRuntimeVersion` | manifest의 `runtime.version` |
| `engineVersions` | manifest의 `engine.versions` 객체 전체 |
| `targets` | manifest 순서대로 각 `version`과 `limitations` |

검증 팩의 manifest와 php·engine·verification을 ZIP의 `runtime/`에 그대로 넣습니다. 별도 고지가 필요하면 `notices/`에 둘 수 있습니다. ZIP 경로 구분자는 `/`입니다. 목록 256 KiB, ZIP 128 MiB, 풀린 데이터 256 MiB, ZIP 항목 4,096개를 상한으로 둡니다. 설치기는 경로 탈출, 링크, 중복·대소문자 충돌, Windows 예약 이름을 거부합니다.

새 PHP 목표가 생기면 먼저 [팩 준비 절차](README.md)의 양성·음성 대조 및 기존 회귀를 실행합니다. 생성된 manifest와 ZIP에 맞춰 카탈로그 행을 작성합니다. 같은 팩 스키마와 보고서 형식을 사용하는 새 목표는 C#이나 XAML에서 버전 번호를 바꿀 필요가 없습니다.

## 설치와 실패 처리

다운로드와 설치는 검사 대상과 분리한 `%LOCALAPPDATA%/DK-PHP-Version-Compatibility-Checker/engines`에서 실행합니다. 검사 대상 경로를 서버로 보내지 않습니다. 팩을 실행하기 전에 ZIP과 manifest 해시, 엔진 구성·목표·검증 결과를 대조합니다. 같은 사용자 계정의 프로그램 관리 영역을 신뢰 경계로 삼습니다. 카탈로그 권한은 앱에 지정한 저장소·경로와 HTTPS 연결에 의존합니다.

다운로드 실패나 취소는 기존 팩과 선택 정보를 보존합니다. 설치가 끝난 팩은 ZIP 해시별 폴더에 둡니다. 손상된 설치 폴더는 자동으로 삭제하지 않으므로 같은 ZIP으로 복구하려면 앱을 닫은 뒤 해당 손상 폴더를 따로 옮겨야 합니다. 다른 검증 팩이나 내장 팩은 사용할 수 있습니다.
