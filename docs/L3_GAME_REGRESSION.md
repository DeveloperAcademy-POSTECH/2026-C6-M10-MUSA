# L3 게임·다인 회귀 결과

2026-09-15 · **PASS(아래 범위)** · Unity6000.5.7f1 / 앱26·게임 계약24

사용자의 검증 압축 요청에 따라 기존 자동 검사를 각1회, Mac3인·5인 시나리오를 각1회 수행했다. 실패와 재검사는 없었다. 소스와 설정이 일치하는 기존 검증 폴더를 재사용했다.

| 검사 | 실제 결과 |
|---|---|
| 전체 EditMode | 1,417/1,417 PASS, 실패·skip0 |
| 전체 PlayMode | 146/146 PASS, 실패·skip0 |
| Mac3인 IPv4/IPv6 혼합 | 공통 상태355개 비교, 불일치0, 전달8회, 실제 명중5회, 전원 Victory·Retry 정상 |
| Mac5인 IPv4/IPv6 혼합 | 공통 상태351개 비교, 불일치0, 전달8회, 실제 명중5회, 전원 Victory·Retry 정상 |

게임 기능 수정은 없었다. 기존 P4 진단 코드1줄에 `-c6P4Host` 주소 옵션만 추가했다(기본 IPv4 유지). Mac 빌드1회로 홀수 Client는 IPv6, 짝수 Client는 IPv4에 연결했으며 실제 Connected 로그의 주소 계열도 검사했다. 원형 이웃·소유권·동일ID·속도/높이 보존·마찰 정지·들고 있을 때 유지·수신20개 제한·Retry 초기화를 대조했다. 패키지·Config·씬·Prefab·기존 meta·원본 렌더링 에셋은 보존했다.

## 이번에 검증하지 않은 범위

다인 앱 시험은 Raw/Combined/수신한도 fixture와 합성 포인터를 사용했다. 자원을 쓰는 생성·직접 Yin+Yang 조합·Client 명중 보상은 기존 Editor 검사에서 확인했으며, 이번 혼합 다인 시나리오에서 처음부터 끝까지 직접 수행한 것은 아니다. 패배/시간 종료 규칙도 자동 검사 범위이며 실제 모바일180초 한 판을 새로 확인하지 않았다.

동일 Mac의 독립3/5프로세스 결과다. 실제 모바일3/5대·Touch·Bonjour·불량 Wi-Fi·인터넷 없는 LAN의 성공으로 해석하지 않는다. iOS 재빌드·재설치와 수동 조작은 반복하지 않았다. **다음은 L4 실제 환경 검증**이다.

## 재실행

L2 빌드 안내의 Mac 빌더로 현재 코드를 출력한 뒤 실행한다. 각 출력은 새 절대 경로를 사용한다.

```bash
python3 tools/run_l3_mixed.py --app /path/to/App.app/Contents/MacOS/EXECUTABLE --output /private/tmp/l3-new-3 --count 3 --port 25333
python3 tools/run_l3_mixed.py --app /path/to/App.app/Contents/MacOS/EXECUTABLE --output /private/tmp/l3-new-5 --count 5 --port 25335
```

[기계 판독용 결과](validation/L3_20260915.json). 원시 XML·빌드 로그·스크린샷·프로세스 기록은 로컬 `c6-l3-checks` 폴더에 보존한다. 공개 결과에는 기기/서명 식별자·주소·원시 로그를 넣지 않는다. GitHub push와 main 병합은 이 단계에 포함하지 않는다.
