> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

## 2026-09-13 · T12 빌드19 추가 검증 마무리

사용자가 추가 사람 조작 없이 직접 검증하도록 지시했다. 이번 요청의 수동 Core Loop 및 자동 승리·초기화 확인과 기록 정리는 완료했다. 게임 소스·Config·씬·패키지·앱은 변경하지 않았고 자동996개를 불필요하게 재실행하지 않았다. 두 기기는 기존 빌드19/GUID2efe6a420e8d426f8927138091a2205b, iPhone17 Host+iPad mini6 Client, 개인 본인 Apple 개발 Team Team이다.

| 실제 확인 범위 | 결과·근거 |
|---|---|
| 새 Bonjour 방·Ready·0개/100/HP100/180 | PASS · 같은 room076ba9f570ed495cbaa39b173107acbb/sessionea14480c5a64456d9fd1f68f074f9302, 실제 P1=0/P2=2. |
| 정상 Raw 손 조작 왕복 | PASS · round1, 같은ID ecde793961c041f5bc61d03c51268e61. 자동준비1회와 수동2회 분리, 최종transferCount3. 공통2348지문 불일치0. |
| 같은 음양 거부·받은 Raw 상단 공격 거부·직접 조합 | PASS · round3. 받은Yang e8c436887de14ca9a09f685fb1223773와 반대Yin 5e31c3b27b4d4ebeb941d409b3db3ed6로 새Combined adbfb962eba149d68665db307138a68d. |
| 수신 Combined 실제 손 발사·물리 피격 | PASS · iPhone 왼쪽 전달→iPad 오른쪽 도착→iPad MOVE 발사, HP100→80. 공통1670지문 불일치0. 사용자 “정상이야”. |
| 정상 생성·실제 명중5회·공격자만 회복5·같은 Victory | PASS · 별도 실제세션 ea14480c5a64456d9fd1f68f074f9302 round6, 일반Seed1989832701, 180초 원래시간. 자동 요청이며 손 입력으로 기록하지 않는다. |
| 결과 고정·Retry·다음 첫 Generate | PASS · 실제 두 기기의 자동 요청/상태/화면 근거. Victory round6 → Ready round7 → Host Start 및 각 기기 첫 Raw1개를 확인했다. 사람 재조작을 요청하지 않았다. |

수동 명중 당시 iPad Stamina가100이어서 그 한 건은 회복 cap유지/추가0이며 숫자+5 증가로 계산하지 않는다. 별도 정상 자동5회 명중의 각 실제 공격자 회복+5와 구분한다. 같은ID 발사 후 장부/뷰/투사체 제거를 확인했지만 수동 capture에 종료 state3가 직접 남았다고 표현하지 않는다.

정확한 짧은 외곽거리 조건은 NOT_RUN이다. 이번 수동 이동88px/97px·실제 요구50px은 이전 조건도 충족했다. 각 입력0.567초로1초 유지 측정도 아니다. iPhone 라벨 여백 때문에 실제 잡기 위치 범위가 매우 좁고, 초기 위치 계산의 라벨 clamp 누락을 정정했다. 해당 세밀한 분기는 자동 재현/회귀44개PASS와 분리하며 원문 AT의 별도 필수 손 조작 횟수로 추가하지 않는다. 추가 사람 확인을 요구하지 않는다.

준비 실패도 보존했다. 첫 준비는 이미Ready round2인 상태에서 정상Retry를 한 번 실행해round3이 됐고, 스크립트가round2를 가정해 멈췄다. 다음 절차는 실제Ready3부터 진행했으며 기존명령을 재전송하지 않았다. round3 후속Victory 시도는 시간이 얼마 남지 않아 CoreDevice에3초timeout을 전달했고, 도구의최소5초 조건에 의해exit64로 종료됐다. 이 실패를 통신 단절이나Victory로 계산하지 않았다. 다음에는 새Ready상태부터 시작/정상5hit를 하나의 절차로 실행했고, 남은시간6초 미만이면 추가전송을 멈추게 했다. 게임의180초를 늘리지 않았다.

빌드18 자동Raw50+Combined50=100회PASS는 그대로 보존한다. 관련49개 소스 중47개가 동일하고 전송권한/장부/네트워크 경로는 바뀌지 않았으며,100회 도구는 이번 변경의 Touch판정을 통과하지 않는다. 추가100회를 실행하지 않을 변경 영향 근거다. **빌드19의100회는 NOT_RUN, 전체T12/G5 최종판정도 NOT_RUN**으로 유지하며 혼합 빌드 근거를 현재빌드 전체PASS로 합치지 않는다. 원문의 실제두iPhone Gate도 사용자승인 iPhone+iPad 범위와 별개다. T13은 시작하지 않았다.

다음 요청 하나는 **T12 검증 기준·증거의 최종 판정**이다. 이미 확인한 조작을 다시 요청하는 단계가 아니며, 빌드별100회 근거와 현재빌드 판정 범위를 정리해야 G5 이후로 넘어갈 수 있다.

[원본 비공개 자료: 수동 Raw 왕복 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 받은 Raw의 직접 조합·피격 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 정상5회 명중·Victory — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: Retry·첫 생성 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 100회 변경 영향 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 원본 보존 — 공개 요약](VALIDATION_SUMMARY.md).

추가 실패 보존: round4는 정상4회 명중 뒤 누적폴더 전체 회수 중 남은읽기예산을 넘겨 process_timeout으로 중단됐다. Victory로 기록하지 않았다. 이후 필요한 상태/응답 파일만 읽는 수집기를 적용하고 기존 실패 원본을 보존했다. 새 수집기의 실제 실행·단일 전송·문맥 확인은 첨부된 원시 기록을 따른다.

그 다음 round5는 실제1회 명중 후 targeted R1 수집 단계에서 실패했다. 006 COPYTO는 성공을 반환했지만 실제 command.json을 다시 읽은 결과005 내용이 남아 있었다. 두 파일의 길이120bytes와 같은 정수초 수정시각을 관찰했으며, 내부 원인은 미확정이다. 이 기록은 명령 전달/검증 실패이고 게임 로직 실패·006 실행·Victory로 계산하지 않는다. 동일 게임 명령을 다시 보내지 않았다.

R2 수집기는 로컬 명령 파일의 수정시각을 실행별 단조 정수초로 구분하고, COPYTO 한 번 뒤 원격 실제 bytes·SHA·명령ID를 읽어 일치할 때만 전송 완료로 인정한다. 로컬 구문·도움말·mock4개 준비 PASS는 실기기 결과와 분리해 보존한다. 이 보완은 로컬 수집 도구에 한정하며 게임/앱/Config를 바꾸지 않았다.

R2의 별도 실기기 capture probe는 PASS다. 관찰 상태는 iphone: round5 Defeat HP80 hit1 / ipad: round5 Defeat HP80 hit1이며, probe는 읽기 경로 확인으로만 집계한다. 이를 정상 전투·Victory 증거로 승계하지 않는다. [원본 비공개 자료: 실제 probe — 공개 요약](VALIDATION_SUMMARY.md).

이후 실제 성공한 round6의 정상5hit·Victory와 round7의 Retry·첫생성은 위 실패 회차와 분리했다. 양쪽 Victory 및 Ready·첫생성 스크린샷은 assistant가 직접 관찰했으며 사람의 추가 Touch 확인으로 기록하지 않는다. [원본 비공개 자료: Victory 화면 관찰 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: Retry·첫생성 화면 관찰 — 공개 요약](VALIDATION_SUMMARY.md).

자동 승리 라운드 공통 지문 390개, Victory 지문 20개가 일치했다.
