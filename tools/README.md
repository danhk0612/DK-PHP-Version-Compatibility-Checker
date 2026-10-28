# 검사 도구와 패키징 준비 영역

T01 [실험 manifest·전용 ruleset·Windows 절차 초안](probe/README.md)을 준비했습니다. 도구·다운로드 코드·Composer lock·실제 실행 결과는 아직 없습니다. [ENGINE_PROBE.md](ENGINE_PROBE.md)에 현재 근거·준비 상태와 실행 차단을 기록합니다. T02 승인 뒤 T04에서 제품용 구성을 만듭니다.

검사 대상 프로젝트에 Composer를 실행하지 않습니다. 준비된 vendor와 PHP 바이너리를 Git에 무작정 올리지 않고 재현·배포 절차로 관리합니다. 라이선스와 대응 소스는 T08에서 실제 포함 파일 기준으로 검토합니다. 자동 다운로드·업데이트·PHPStan 구현은 현재 범위가 아닙니다.
