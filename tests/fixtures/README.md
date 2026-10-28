# T01 최소 정적 검사 입력

8개 PHP 파일과 [cases.json](cases.json)을 준비했습니다. **모든 실제 엔진 결과는 미측정**입니다. 실행용 애플리케이션이나 실제 프로젝트 소스가 아닙니다. 파일을 PHP로 직접 실행하지 않습니다. 도구 설치와 명령 초안은 [검증 준비 안내](../../tools/probe/README.md)를 따릅니다.

| 사례 | 목표 | 기대 기준 |
| --- | --- | --- |
| mysql_removed / mysql_control | 제거된 함수와 mysqli 대조 | 대상 8.2/8.3/8.4에서 제거 함수 탐지 vs 0건 예측 |
| get_class_no_args / get_class_control | PHP 8.3 인자 생략 폐기 | 8.2는 0건, 8.3/8.4는 경고; 명시 인자 대조는 0건 예측 |
| implicit_nullable / explicit_nullable | PHP 8.4 암묵적 nullable 폐기 | 8.2/8.3는 0건, 8.4는 경고; 명시 nullable 대조는 0건 예측 |
| dynamic_property / declared_property | PHP 8.2 동적 속성 폐기 탐색 | 동적 사례는 observe_only로 실제 탐지/미탐지를 기록; 선언 속성은 0건 예측 |

mysql 계열은 이미 PHP 7.0에서 제거됐으므로 7.4→8.2에서 새로 발생하는 변경이 아닙니다. 오래된 코드 문제의 탐지 기준으로만 사용합니다. 동적 속성은 PHP 변경 사실을 확인했지만 alpha2에서 해당 코드가 탐지된다는 근거는 확보하지 못해 finding으로 고정하지 않았습니다. 이 사례 0건은 검사 통과를 뜻하지 않습니다.

finding의 source_prefix는 태그 규칙을 읽은 예측이며 실제 오류 코드 접미사가 붙을 수 있습니다. line은 입력 파일의 1기준 줄 번호입니다. 실제 결과에서 prefix/line과 type, severity, column, message를 함께 대조하고 추가 탐지·누락도 기록합니다. 기대값은 actual 결과가 아닙니다. tests/fixtures를 통째로 검사한 총수로 개별 사례의 기대를 판정하지 않습니다.

## 근거

2026-10-29 본문·태그 소스를 다시 읽었습니다. 상위 코드를 복사한 fixture가 아니라 최소 자체 작성 사례입니다.

- [PHP 8.2 변경](https://www.php.net/manual/en/migration82.deprecated.php)
- [PHP 8.3 변경](https://www.php.net/manual/en/migration83.deprecated.php)
- [PHP 8.4 변경](https://www.php.net/manual/en/migration84.deprecated.php)
- [alpha2 RemovedFunctions](https://github.com/PHPCompatibility/PHPCompatibility/blob/10.0.0-alpha2/PHPCompatibility/Sniffs/FunctionUse/RemovedFunctionsSniff.php)
- [alpha2 RemovedGetClassNoArgs](https://github.com/PHPCompatibility/PHPCompatibility/blob/10.0.0-alpha2/PHPCompatibility/Sniffs/ParameterValues/RemovedGetClassNoArgsSniff.php)
- [alpha2 RemovedImplicitlyNullableParam](https://github.com/PHPCompatibility/PHPCompatibility/blob/10.0.0-alpha2/PHPCompatibility/Sniffs/FunctionDeclarations/RemovedImplicitlyNullableParamSniff.php)

원본 보호·설정 부작용·읽기 실패·빈 폴더·경로/탐색·오프라인 시험은 아직 이 fixture에 포함하지 않았습니다. 전체 완료 조건은 TASKS.md를 따릅니다.
