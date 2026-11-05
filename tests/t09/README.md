# T09 패키징 리허설

`tools/packaging/Prepare-AuditPackage.ps1`은 Windows x64/SDK 10.0.403/검증된 tools/runtime에서 self-contained publish, 현행 고지·원문 동봉, ZIP roundtrip·엔진 manifest/해시 검증, 한글·공백 경로의 실제 apphost 창 표시/응답·정상 종료와 패키지 파일 불변을 확인합니다. 사용자 프로젝트에서 실행하지 않습니다. 기존 artifacts/t09는 덮어쓰지 않습니다.

```powershell
./tools/engine-pack/Prepare-Pack.ps1
./tools/packaging/Prepare-AuditPackage.ps1
```

ZIP은 runner 내부의 `artifacts/t09`에만 남습니다. T08/T10 선행 조건이 완료되지 않은 동안 공개 저장소 Actions artifact에는 ZIP·제품/외부 바이너리를 올리지 않고 `evidence/` 메타데이터와 publish 로그만 보존합니다. 원문을 동봉했다는 사실만으로 최종 고지·대응 소스 이행이나 재배포 적합성을 통과하지 않습니다.

실제 사용자에게 제공할 후보에는 T08에서 검증한 matching source·재구성 자료 및 최종 고지가 함께 필요합니다. [Windows 11 직접 확인 절차](WINDOWS11_TEST.md)는 승인된 후보를 받았을 때 사용하는 시험 안내입니다. 현재 CI는 깨끗한 Windows 11/CRT 누락 상태·실제 GUI 검사 흐름·일반 사용자 권한을 검증하지 않습니다. T09 완료나 공개 배포 승인으로 표시하지 않습니다.
