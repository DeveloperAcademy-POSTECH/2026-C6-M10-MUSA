> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T09 조작 변경 · 빌드13

<!-- C6:T09:G4 -->
## 현재 T09 / G4 · 실기기 Core Loop 완료

빌드13의 iPhone17 DEV SOLO에서 정상 생성·같은 음양 거부·터치 조합5회·실제 명중5회·각회복+5·Victory/Defeat·고정 결과·Retry·재시작 첫Generate Raw1/비용20을 사람 확인과 기기 로그로 대조했다. **T09와 G4는 한 기기의 명시적 개발 모드 범위에서 PASS다.**

기존 소스379개는 변경 없이 보존했다. 빌드13의 자동475개(Edit401/Play74)와 Mac 두 화면/두참가자/실제180초 근거를 재확인했으며 이번에는 새 구현·재빌드·자동시험 반복을 하지 않았다. 두 기기 최종 통합·G5/T12·전체 MVP 완료는 별도다.

[현재 G4 검증](T09_G4_VALIDATION.md) · [원본 비공개 자료: 실기기 완료 근거 — 공개 요약](VALIDATION_SUMMARY.md). 다음 요청 하나는 **T10-A 2인 Lobby·방 탐색·Ready**이며 아직 시작하지 않았다. 아래는 이전 작성 시점의 기록을 보존한 것이다.
<!-- C6:T09:G4:END -->

2026-09-13 최신 사용자 지시: “combined only 존은 필요없어. 전체 영역에서 combine ... 아래 영역에서 위 전투 영역으로 드래그 ... 호버링 이펙트와 크기를 변형”. 현재 구현·실행 단계는 아래 기록에 추가한다. 다음 T10은 시작하지 않는다.

## 적용 차이

- 별도 Attack Zone 밴드와 COMBINED ONLY 표시는 제거한다. 하단에서 UI를 제외한 전체 구슬 작업 공간에서 Raw Yin+Yang을 겹친 후 놓아 조합한다.
- Combined는 실제 하단/상단 전투 화면 경계를 처음 넘는 순간 발사 요청한다. 경계 이전에는 들고 있으며, 경계 통과 후 Host 승인으로 하단에서 사라지고 실제 3D 투사체가 된다. 별도 놓기나 공격 버튼은 필요 없다.
- “소모”의 화면 동작을 자동 발사로 적용한다. 기존 ID 유지·Host 승인·실제 Rigidbody/Collider 명중·명중 또는 수명 만료의 최종 Consumed는 보존한다. Raw 공격 허용이나 즉시 피해를 추가하지 않는다.
- 전체 하단 조합을 막던 T09 수평 전달 선행 판정을 끈다. 아직 구현되지 않은 전달 거부를 길게 드래그했을 때 발생시키지 않는다. 향후 T11 전달 입력과 자유 드래그 구분은 그 Task에서 따로 결정한다. T05~08 기본 입력 계약과 중앙 Config 기존값은 보존한다.
- T09에서만 선택 적용하는 확대(약1.24배), 맥동하는 빛, 그림자를 추가한다. 시각 자식만 변형하며 Collider와 원래 좌표는 유지한다. 손을 놓거나 요청 대기·취소·결과·비활성화가 되면 효과를 해제한다.
- 시작0구슬·Stamina100·생성20·3초당20·실제 명중 공격자+5·180초·Host 결과/Reset 규칙은 유지한다.

## 이전 실기기 확인 범위

사용자의 “확인 완료”와 빌드12 실제 로그에서 직접 조합2회, 터치 발사2회, 실제 피격2회(HP100→80→60), 회복2회를 확인했다. 첫 조합·명중은 PASS다. 결과 화면과 Retry 완료는 이 응답으로 확대하지 않는다. 이 빌드12 응답만으로 G4 전체나 새 빌드13을 PASS로 승계하지 않았다. 빌드13 후속 직접 확인은 아래에 따로 기록했다.

근거: [원본 비공개 자료: 빌드12 사용자 확인 — 공개 요약](VALIDATION_SUMMARY.md), [원본 비공개 자료: 선별 실행 행 — 공개 요약](VALIDATION_SUMMARY.md). 빌드12 작성 당시 문서는 history/2026-09-13-t09-build12에 보존했다.

## 빌드13 실행 상태

최종 빌드13 검증은 다음과 같다. 실제 루트 `/path/to/2026-C6-M10-MUSA`, 출력 `Builds/T09-build13`, 씬 BattleLoop.unity다. Unity6000.5.7f1·기존 패키지와 Config를 유지했다. 원래 열린 Editor를 보존하고 격리 복사본에서 실행했다. iOS ID com.wolfuraark.c6prototype·0.1.0/13·본인 Apple 개발 Team 개인 Team YOUR_TEAM_ID·Portrait·최소iOS15·iPhone/iPad 공용이다.

| 검증 | 결과 | 근거 범위 |
|---|---|---|
| Compile·EditMode | PASS | 401/401, 실패/skip0. 새 경계/빠른 이동/Raw 보존/긴 Drop·기존 모드 포함 |
| PlayMode | PASS | 74/74, 실패/skip0. 전체폭·옛 밴드 안 조합, 실제 경계 발사1회, 확대/빛 복귀·Collider 불변·하단 라벨·결과/Reset |
| Mac 휴대폰/태블릿 비율 | PASS | 390×844·560×746 각각 일반 유료생성→중심Drop5→실제hit5→각+5→Victory→Retry·첫Generate20 |
| Mac 두 참가자 | PASS | Host생성7/조합3·Client생성6/조합2. 실제hit5·최종 세 snapshot은 최상위 수신자nonce 외 완전일치. Client Rigidbody0/회복작성0 |
| 실제180초 | PASS / Mac | 180.0016565초 경과 후 Defeat·고정 결과·Retry 확인, 단축시간과 구분 |
| Mac/iOS export | PASS | Unity BuildReport·완료행·새 산출물. Mac GUID dec66b652fe94af3a296b300450b5f27 |
| iOS 앱 빌드·서명 | PASS | Xcode BUILD SUCCEEDED·codesign 검증·선택기기 provisioning·빌드13 |
| iPhone 설치·실행 | PASS | install success와 실제 C6_T09_READY build13. iPhone18,3/iOS26.6.2/1206×2622/Safe(0,102,1206,2334) |
| iPhone 새 조작·효과 | PASS | 사용자 “확대·빛 효과·조합·자동 발사 모두 정상”. 정상생성5·TOUCH 조합1·MOVE 자동발사1·실제hit1 HP100→80·공격자 회복5 |
| 실기기 한 판 결과·Retry/G4 | NOT_RUN | 이번 한 번 확인을 전체 결과/Reset 완료로 확대하지 않음 |
| T10·두 iPhone 화면 QA | NOT_RUN | 이번 범위 밖 |

처음 장면 실행73개 통과 뒤, 확대 라벨이 footer에 가릴 수 있는 여백을 보완하고 해당 회귀 검사를 추가해 최종74개를 다시 실행했다. 기존 실행도 보존했다. Mac 개발 앱의 우하단 Development Build 워터마크가 END RUN 글자 일부와 겹치는 기존 표시 제한은 관찰 기록이며, 입력·잡기 효과 실패로 확대하지 않았다.

현재 소스379개 중 검증 복사본375개와 일치한다. 기존 URP 템플릿4개 차이는 원본 보존 상태 그대로다. 기준698파일 대비 삭제0·기존meta변경0·기존씬/패키지 보존을 확인했다. 수정 전 빌드12 문서·앱·실패/성공 로그를 지우지 않았다. 원래 Editor나 다른 앱을 강제 종료하지 않았다. 커밋·push·배포는 수행하지 않았다.

근거: [원본 비공개 자료: 실행 요약 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 실기기 확인 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 실제 기기 행 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 잡기 화면/Mac — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 두 참가자 대조 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 보존 — 공개 요약](VALIDATION_SUMMARY.md).

## 남은 사람 작업과 다음 요청

이번 조작 변경을 막는 항목은 없다. **T09 실기기 한 판 결과·Reset 검증 완료**를 다음 요청 하나로 선정한다. 새 빌드에서 결과 화면과 Retry 후 구슬0·Stamina100·HP100·180초·첫Generate20까지 실제로 확인해야 G4 전체를 완료할 수 있기 때문이다. 다음 Task 구현은 시작하지 않는다.

