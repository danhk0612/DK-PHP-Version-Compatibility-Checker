# 검사 도구와 패키징 준비 영역

T01 [실험 manifest·전용 ruleset·Windows 절차 초안](probe/README.md)을 준비했습니다. Windows workflow에서 도구 조합 실행을 검증했고 실제 Composer lock·결과 JSON을 보존했습니다. T04 [고정 엔진 팩 준비·검증](engine-pack/README.md)을 추가했습니다. 제품용 runtime은 재현 절차로 생성하며 바이너리는 Git에 넣지 않습니다. [ENGINE_PROBE.md](ENGINE_PROBE.md)에 현재 근거·실측과 미검증 항목을 기록합니다. 앱에서의 팩 소비는 T05/T06에 남아 있습니다.

검사 대상 프로젝트에 Composer를 실행하지 않습니다. 준비된 vendor와 PHP 바이너리를 Git에 무작정 올리지 않고 재현·배포 절차로 관리합니다. 라이선스와 대응 소스는 T08에서 실제 포함 파일 기준으로 검토합니다. 자동 다운로드·업데이트·PHPStan 구현은 현재 범위가 아닙니다.
