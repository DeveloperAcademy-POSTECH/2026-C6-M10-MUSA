# #56 로비 속성 선택·매판 랜덤 자리·속성별 공격 — 구현 계획

작성일: 2026-09-29. 최신 적용 버전: **r04 — 2026-09-30 Raw 생성 규칙 수정**. 사용자의 “선택 해제 버튼도 포함한 개선안으로 구현을 진행” 요청으로 r02의 규칙과 선택 해제 UI를 승인받아 구현했다. 이후 사용자가 Raw 원소 추첨 방식을 수정했다. 아래 계획 본문은 r02 검토 당시 상태·검증 판정을 보존하고, 현재 규칙은 r04 수정 내용을 따른다. 현재 구현·실행 결과는 [구현 기록](LOBBY_ELEMENT_SELECTION_IMPLEMENTATION.md)을 따른다. 계획 당시 NOT_RUN 표시는 실행 결과로 소급 변경하지 않는다.

## 1. 브랜치 확인 결과

| 항목 | 확인 결과 |
|---|---|
| 실제 Unity 저장소 | `DeveloperAcademy-POSTECH/2026-C6-M10-MUSA` |
| 현재 브랜치 | `feat/#56-lobby-element-selection` — 사용자 지정과 일치 |
| upstream | `origin/feat/#56-lobby-element-selection` — 정상 연결 |
| 기준 커밋 | `991339eeeedf18e2baa1fb859f857a1c8859ba2a` |
| 로컬 main / origin/main / 작업 브랜치 | 모두 위 커밋, 작업 브랜치의 별도 구현 커밋 없음 |
| GitHub 실제 조회 | 최초 계획 조사 때 원격 main과 작업 브랜치도 위 커밋으로 일치. r02에서는 원격 조회를 반복하지 않음 |
| 작업 폴더 | 최초 조사 시 미커밋 변경 없음. r02 시작 시 위 브랜치·커밋 유지, 기존 계획서만 untracked |
| 포함된 선행 작업 | PR #50 / `feat/#49-same-element-combine` 병합, 동일 속성 조합 및 아트 변경 포함 |
| 환경/진입점 | Unity6000.5.7f1, 기존 저장 패키지 유지, `ContinuousTransferBattle.unity` |

브랜치는 작업을 시작할 수 있는 상태다. 이번 계획은 위 커밋을 기준으로 하며, 앞서 작성했던 Spec의 `a5838889`와는 후속 #49 변경 차이가 있다.

## 2. 현재 구현과 이번 변경

| 사용자 요구 | 현재 코드에서 확인한 상태 | 이번 작업 |
|---|---|---|
| 플레이어별 화·수·목·금·토 선택, 중복 금지 | `LobbyPlayer`에 선택 속성이 없고 로비 선택 UI/요청 없음 | 선택 UI·Host의 유일한 점유 승인·공유 데이터 추가 |
| 방 참가자가 선택한 속성 집합에서 Raw 원소를 무작위 생성 | 계획 기준 코드에서는 Raw ID hash와 공통 TeamElements로 결정 | Host의 승인 선택 맵에서 활성 참가자만 후보로 모아 원소 추첨·구슬 원소 명시 저장·전달 시 불변 보존 |
| 같은 방 로비에서 속성 변경 | 정상 게임의 `ReturnToLobby`는 승인된 Retry 콜백을 호출하며 실제 Lobby phase 복귀와 구분되지 않음 | Retry와 로비 복귀를 분리. 같은 방의 참가자·선택을 유지한 채 선택 가능한 로비로 복귀 |
| 매판 랜덤 자리 | 입장 순서의 명단이 좌우 이웃/카메라/투척/요괴 회전의 자리로 사용됨 | 참가자 정체성은 유지하고 별도 라운드 좌석 순서를 공유 |
| 같은 속성의 Yin+Yang만 조합 | **#49에서 구현됨.** Host 예약 전과 조합 commit에서 동일 원소 재검사, 불일치 `ELEMENT_MISMATCH` | 규칙 유지. 새 Raw 원소 데이터로 비교 경로를 연결하고 회귀 검증 |
| 선택 속성 Combined만 공격, 다른 속성은 경고 | Combined/소유권/상태 검사는 있지만 선택 속성 검사 없음. 거절은 일반 진단 문구 | 로컬 안내+Host 거절, 실제 경고창, 원본 구슬 보존 |

현재 확인은 소스·직렬화 연결의 읽기 조사다. 관련 테스트 파일이 있어도 이번에 실행한 것은 아니므로 기존/신규 기능 PASS로 기록하지 않는다.

## 3. 사용자 수정으로 확정한 동작 기준

아래 8개 규칙은 r02에서 사용자와 확정했다. **4번의 ‘자기 선택 속성만 생성’은 2026-09-30 후속 지시로 폐기하고, 현재는 방의 승인된 활성 참가자들이 선택한 속성 중 무작위 생성한다.** r01의 중복 선택 허용과 선택 여부에 관계없는 전체 오행 랜덤 생성안은 채택하지 않았다.

1. **서로 같은 속성 선택을 허용하지 않는다.** 이미 다른 참가자가 선택한 속성은 선택할 수 없다. 각 참가자의 선택은 Host가 중복 없이 승인한다.
2. **방 입장 후, 전투 시작 전 로비에서 선택한다.** 최초에는 미선택이며 선택 완료 전에는 Ready를 할 수 없다. 이미 Ready여도 속성을 바꾸면 전원 Ready를 해제한다. Host가 승인한 선택이 화면에 반영되며 요청 중에는 중복 변경을 막는다.
3. **전투 동안 선택을 고정한다.** Retry에서는 선택을 유지하고 자리만 새로 추첨한다. 속성을 바꾸려면 방 로비로 돌아간다. 새 방 입장에서는 다시 선택한다.
4. **Raw는 방의 활성 참가자들이 Host에게 승인받은 선택 속성 중에서 무작위 생성한다.** 요청자의 속성도 선택되어 있어야 생성할 수 있지만, 생성 원소를 그 속성으로 고정하지 않는다. 음/양 추첨·생성 비용·스태미나 회복은 유지한다. 선택은 공격 자격이며 다른 속성 구슬도 보관·조합·전달할 수 있다. 전달로 구슬 원소를 바꾸지 않는다.
5. **자리는 첫 시작과 매 Retry 준비 때 Host가 추첨한다.** Host/P1도 랜덤 자리 대상이다. 이전과 같은 결과가 나올 수 있으며, 각 판의 자리 순서는 판이 끝날 때까지 고정한다.
6. **P번호와 역할은 유지한다.** P1은 여전히 Host의 플레이어 식별자지만 원형 배치의 첫 자리에 고정되지 않는다. ‘P3 / 화 / 자리1’처럼 플레이어 식별과 실제 자리를 구분한다. 좌우 이웃·시점·투척 위치는 실제 자리로 계산한다.
7. **공격 권한은 지금 던지는 소유자의 선택으로 판단한다.** 화 Combined를 전달받은 화 플레이어는 공격 가능하고 수 플레이어는 공격 불가다. 누가 만들었는지는 공격 권한의 기준이 아니다.
8. **다른 속성 투척은 경고와 함께 취소한다.** 구슬의 ID/소유권/Idle 상태를 유지하고 드래그 시작 위치로 돌린다. 투사체·피해·명중 보상을 만들지 않는다. 전투 시간은 계속 진행한다.

### 예정 UI

- 로비에 `화 / 수 / 목 / 금 / 토` 다섯 선택 버튼을 추가한다. 선택 표시와 Host 승인 대기 표시를 구분한다. 다른 참가자가 점유한 버튼은 해당 P번호와 함께 비활성 표시하고, 동시 선택으로 거절되면 최신 점유 상태를 반영한다.
- 참가자 목록에 각 P번호의 선택 속성·Ready를 보여 준다. 자리 추첨 전에는 ‘시작 시 결정’, 라운드 준비 후에는 확정 자리/좌우 이웃을 표시한다.
- 경고창 예시: **“내 속성은 화입니다. 화 조합 구슬만 공격할 수 있습니다. 현재 구슬: 수.”**와 확인 버튼.
- 경고창은 새 속성 경고용 패널이다. 기존 `MonsterAttackWarning`(요괴 공격 위험 안내)과 분리한다.
- 기존 배치·아트를 가능한 범위에서 유지하고 선택·안내 기능을 중심으로 작업한다. 현재 로비는 코드로 만드는 uGUI Canvas이므로 그 연결 방식에 맞춘다.

### 추가 점검 대상: 선택 해제 버튼

5명이 전부 선택하면 모든 속성이 점유되어 바로 다른 속성으로 변경할 수 없다. 로비에 **선택 해제** 버튼을 추가하는 안을 제안한다. 해제하면 자신은 미선택/Ready 불가, 해당 속성은 다시 선택 가능, 전원 Ready와 선택 배리어가 초기화된다. 이를 이용해 서로 속성을 바꿀 수 있으며 자동 맞교환은 추가하지 않는다.

이 버튼은 위 8개 사용자 규칙을 구현하는 방식에 관한 **추가 제안**이다. 함께 승인되지 않으면 5인 포화 상태의 다른 속성 변경은 점유 거절로 처리하고 기존 선택/Ready를 보존한다.

## 4. 데이터와 네트워크 설계

### 4.1 플레이어 정체성·선택·자리의 분리

다음은 **추가 예정 개념/필드명**이다. 현재 구현에 이미 존재한다고 해석하지 않는다.

| 개념 | 예정 데이터 | 책임 |
|---|---|---|
| 참가자 정체성 | 기존 `clientId`, `playerNumber`, Host 역할, 입장 명단 | 승인/송신 대상/응답 감시. 순서를 임의로 바꾸지 않음 |
| 선택 속성 | `LobbyPlayer.selectedElement`, Host 승인 `clientId → element` | 중복 없는 선택·Ready·활성 참가자의 Raw 후보 집합·라운드 공격 권한 |
| 속성 점유 | 승인된 참가자 선택에서 계산하는 `element → clientId` | 다른 플레이어의 점유와 동시 선택 검사. 독립 저장 원본을 중복으로 만들지 않음 |
| 구슬 원소 | 새 Raw의 `OrbRecord.element` 및 snapshot/wire 원소 값 | 생성 당시의 속성을 구슬 자체에 고정. 현재 owner와 독립적으로 전달/조합/뷰에 사용 |
| 라운드 자리 | `roundSeatOrder` 또는 같은 역할의 배열 | 승인 참가자를 정확히 한 번 포함하는 순열. 좌우/카메라/투척/요괴 바라보는 방향 |
| 상태 일치 | 현재 round/context와 선택·자리 내용을 포함한 초기/진행 snapshot hash | 모든 기기가 같은 선택·자리로 시작하도록 확인 |
| 사용 가능한 오행 | 유효한 화·수·목·금·토 정의 | 선택 enum·아트·계약 검증. 개인 선택 맵과 분리 |

현재 여러 코드가 **Host=P1=명단 첫 항목**을 전제로 한다. `participantIds` 배열만 섞으면 Host 검사, `participants.Skip(1)` 방송/ACK, 초기 계약 비교가 깨질 수 있다. 따라서 승인 입장 명단을 유지하고 실제 배치를 별도 배열로 추가한다.

### 4.2 선택 요청

`선택 버튼 → T10LobbyController → T10LobbySession → LobbyAuthority → LobbySnapshot → 로비 표시` 경로를 추가한다.

- `SelectElement`와 같은 전용 로비 요청을 추가한다. 요청은 자기 선택만 변경하며 Host는 인증 sender의 참가자 레코드를 수정한다.
- 유효 오행인지, 현재 로비인지, 가입·초기 확인·context/revision/sequence가 맞는지 검사한다. None·범위 밖 enum·타인 수정·시작 이후 변경을 거절한다.
- 새 속성이 다른 참가자에게 점유되어 있으면 `ELEMENT_ALREADY_SELECTED`와 같은 사유로 거절한다. 같은 속성의 동시 요청은 **Host의 처리 순서에서 한 명만 승인**하며 다른 요청자는 자동으로 다른 속성을 배정받지 않는다.
- 변경 성공은 새 속성의 점유 가능성을 검사한 뒤 **기존 점유 해제·새 선택 확정·전원 Ready 해제·선택 revision 증가를 한 번에 처리**한다. 먼저 기존 속성을 해제한 뒤 실패하는 중간 상태를 만들지 않는다.
- 정상 선택 변경은 revision 갱신과 전원 Ready 해제를 동반한다. 동일 속성 재선택은 상태 변경 없이 처리하도록 한다.
- 변경 거절/동일 속성 재선택은 기존 선택·점유·Ready·상태 revision을 유지한다. requestId/sequence는 기존 중복 방지 정책에 따라 소모될 수 있다.
- 현재 Ready는 일부 오래된 일반 revision을 허용하므로, 선택 변경 때 `selectionRevision` 또는 동등한 명단 배리어를 갱신한다. **변경 전 선택 기준으로 보낸 지연 Ready는 거절**한다. 선택 완료 전이나 요청 중에 Ready를 보내는 경로도 막는다.
- 기존 로비의 중복 요청 거절 정책을 따른다. 클라이언트가 버튼 색을 바꿨다는 이유만으로 선택 확정으로 취급하지 않는다.
- 자기 선택 요청이 Pending인 동안 모든 선택 버튼과 Ready를 잠그고, UI 및 Session 제출 지점 모두에서 두 번째 선택 요청 발송을 막는다. 승인/거절·기존 요청 종료 정책에 따라 대기를 해제하며, 낡은 응답으로 최신 선택을 덮어쓰지 않는다.
- 실제 로비 퇴장으로 참가자가 제거될 때 그 속성도 같은 처리에서 선택 가능하게 된다. UI를 닫거나 요청이 진행 중이라는 이유만으로 점유를 해제하지 않는다. Host 퇴장으로 방이 닫히면 해당 방의 선택 상태를 정리한다.
- 로비 snapshot에서는 여러 명의 미선택(None)을 허용하지만 유효한 선택끼리는 중복을 금지한다. 시작/Game 계약에서는 **전원 유효 선택·중복 없음·전체 참가자 맵 일치**를 Host와 Client 모두 검사한다.
- 다른 4명의 선택 후 남은 참가자에게 남은 한 속성을 표시하지만 사용자가 누르고 Host가 승인하기 전에는 자동 선택/Ready로 간주하지 않는다.
- 선택 해제 버튼을 승인하면 별도 `ClearElement` 동작으로 None을 허용한다. 일반 SelectElement 요청의 비유효 None 입력과 명시적 해제를 구분한다.

### 같은 방 로비로 돌아가는 경로

현재 정상 멀티플레이에서 `T09BattleController.ReturnToLobby`는 `approvedRetry`를 호출하고, `T10GameSession.Retry`는 새 전투 Ready를 준비한다. `LobbyAuthority`에는 Playing에서 Lobby로 돌아가는 동작이 없다. 따라서 화면만 로비처럼 보이게 해서는 속성 변경 조건을 충족하지 못한다.

- 기존 Host의 LOBBY 동작을 Retry와 분리하고, 결과 또는 Retry 준비 상태에서 Host가 **같은 방 로비 복귀**를 요청하는 경로를 추가한다. Playing 중 선택을 푸는 우회 경로는 만들지 않는다.
- Host가 게임 정리와 로비 phase 복귀를 승인하고 모든 기기에 반영한다. 방/연결·인증 참가자·P번호·현재 선택은 유지하고 전원 Ready를 해제한다. 전투 구슬/투사체·드래그 Pending·결과 연출·초기 ACK 상태는 정리하고 로비 선택 UI를 다시 활성화한다.
- 새 선택을 승인하면 §4.2의 유일 점유/Ready 규칙을 적용한다. 다시 전원이 Ready하고 시작할 때 새로운 선택 맵과 자리 순열을 확정한다. 방을 나간 뒤 다른 방에 입장하는 경우에는 선택을 None부터 시작한다.
- 현재 시작 계약과 Attach 경로의 고정 round=1 처리도 점검한다. 같은 방의 Retry와 로비 재시작을 통틀어 이전 라운드 식별자를 재사용하지 않고, 이전 전투 snapshot/ACK/공격 요청이 새 판에 적용되지 않도록 한다.
- `LobbyWire`의 phase 전환 검증, `StartConfirmed` 재발행, GameSync의 해제/재연결, 방 광고를 함께 연결한다. `Leave`로 연결을 끊고 새 방을 만드는 동작으로 대체하지 않는다.

### 4.3 랜덤 자리 확정

- Host가 참가자들의 순열을 Fisher–Yates 방식으로 한 번 생성하고 공유한다. 각 Client가 따로 추첨하지 않는다.
- 자리용 난수는 기존 구슬 음양 seed/성공 생성 순열과 분리한다. 자리 추첨 횟수가 구슬 추첨을 바꾸지 않도록 한다.
- 배열 길이·중복·누락·미참가자·round를 검증한다. 첫 시작 계약과 각 라운드 snapshot에 확정 좌석을 담는다.
- 좌석 배열과 선택 맵은 사본을 만들어 전달하고 같은 라운드에는 불변으로 취급한다. 임의 배열 수정이나 Playing 중 좌석 교체를 허용하지 않는다.
- 첫 판과 Retry의 **초기 상태 hash ACK 전에** 자리·카메라·Host 투척 기준을 적용한다. 모든 Client 확인 전에는 입력/시작을 막는다.
- Retry에서 새 자리 배열을 배포하고 Client도 round 변경을 받아 시점을 다시 적용한다. 선택은 그대로 유지한다.
- 기존 `ConfigureRoster/ConfigureParticipantThrowFrames`는 최초 bind 전용이므로, Retry 준비 상태에서만 좌석을 교체할 수 있는 별도 라운드 준비 경로를 만든다. Playing 중 호출은 거절한다.
- 좌우 이웃, 수신 반대쪽, 개인 카메라, Host 계산 투척 프레임, 요괴 공격 시 대상 방향을 모두 같은 배열에서 계산한다.
- 방송 대상·Host 여부·P번호·개인 자원 키는 기존 참가자 정체성을 계속 사용한다.

### 4.4 활성 참가자 선택 집합의 Raw 생성과 원소 보존

현재 `HostResourceAuthority.Generate → HostOrbRegistry.RegisterGeneratedRaw`는 임의 GUID를 발급하고 `OrbElements.RawElement(id)`가 ID hash로 원소를 결정한다. 이 경로를 새 정상 세션에서는 다음과 같이 바꾼다.

1. GameSync가 시작 계약의 승인 선택 맵을 자원 권한에 전달한다. Host는 요청자가 승인된 속성을 가지고 있는지 확인하고, **현재 자원 세션의 활성 참가자 중 승인된 선택**만 Raw 후보 집합에 넣는다. 현재 원소 필드가 없는 GenerateRequest 구조를 유지하며 Client가 희망 원소를 지정하지 못하게 한다. `ResourceSession.RefreshBinding`이 Retry마다 권한 객체를 다시 만들므로 매 라운드 재생성 때도 승인 선택 맵을 전달한다.
2. 기존 회복 정산·상태/sequence/비용/보관 한도 검증과 음양 추첨을 유지한 뒤, Host가 후보 집합에서 원소 하나를 추첨하여 Raw에 등록한다. 후보는 승인된 유효 선택으로 한정하며 요청자 선택 누락/비유효 값은 실패한다. 실패·중복 요청은 비용과 성공 생성 횟수, 다음 추첨 순서를 증가시키지 않는다.
3. 새 Raw의 원소를 `OrbRecord`와 공통 `OrbWire`에 명시 저장한다. ID는 기존 GUID 형식으로 발급한다. 원하는 hash가 나올 때까지 ID를 반복 생성하는 방식은 사용하지 않는다. Combined의 원소는 기존 ID의 두 슬롯을 원본으로 유지하고, 새 Raw 전용 필드는 Combined에서 None으로 고정해 중복 표현을 만들지 않는다.
4. `T09BattleController`의 뷰 생성에서 `OrbView.Configure`까지 명시된 Raw 원소를 전달하고 표시·조합 helper가 이를 읽도록 연결한다. `SameElement(id,id)`의 기존 hash 기반 비교만 남겨 새 생성 원소를 무시하지 않는다. 과거 T05~T08와 Sandbox 호출은 별도 호환 경로로 점검한다.
5. 전달·예약·소비 등 immutable 레코드 재생성에서도 원소를 복사한다. 같은 ID의 원소는 owner 변경이나 snapshot 갱신으로 바뀌지 않도록 검증한다. **전달받은 Raw를 새 owner의 선택과 비교해 바꾸거나 잘못 거절하지 않는다.**
6. 조합은 두 Raw의 실제 원소를 검사하고, 현재 #49처럼 그 원소를 Combined ID 양쪽 슬롯에 인코딩한다. Combined ID 형식·전달/발사 ID 유지 규칙은 그대로다.
7. 원소 추첨은 Host Seed·요청자 ID·성공 생성 순번을 사용하는 별도 결정적 난수 흐름으로 처리한다. 후보 집합의 저장 순서에 따라 결과가 달라지지 않게 정렬하고, 자리 추첨·원소 추첨이 기존 음양 순열이나 자원 수치를 바꾸지 않게 한다. 활성 참가자 한 명의 선택이 빠지면 해당 원소는 후보에 포함하지 않는다.

새 Raw의 명시 원소는 등록부/snapshot 검증과 CanonicalHash에 포함한다. 공통 `OrbWire.FromRecord/ToRecord`, `AttackWire.ValidOrb`, `ResourceWire.ValidOrb`, `CombinationWire.ValidOrb`와 생성·전달·조합 영수증의 confirmed orb가 모두 같은 규약을 따른다. `GameWire`의 같은 ID 상태 전환과 영수증/공유 상태 비교에서는 원소 불변을 검증하되, 전달 후 owner의 선택 속성과 Raw 원소가 다르다는 이유로 거절하지 않는다. 구/신 생성 표현이 섞인 정상 세션은 허용하지 않는다. 과거 원소 없는 Smoke/fixture 경로는 별도로 구분하고 새 정상 게임에서 누락된 원소를 ID hash로 조용히 대체하지 않는다.

### 4.5 서로 다른 빌드 섞임 방지

선택/좌석/Raw 원소는 공유 게임 계약을 바꾸므로 Lobby/Game/Raw snapshot의 호환 식별자·메시지/검증 범위를 함께 갱신한다. 이전 빌드와 새 빌드를 같은 방에 입장시키지 않는다. 광고·직접 IP·Host 승인·초기 상태 검증이 같은 기준을 사용해야 한다.

선택/좌석의 비교는 DTO 필드 추가만으로 끝내지 않는다. `LobbyWire`의 플레이어/receipt/명단 전환 비교, `GameWire`의 CanonicalHash·전달 소유권 진행 검증, `T10GameSession.Publish`의 `frozenSignature`에 모두 반영한다. 준비 상태에서 자리만 바뀌었을 때도 새 snapshot을 반드시 발행하고 ACK 대상 hash에 포함해야 한다.

**개인 선택 맵을 `OrbElements.Configure/TeamElements`에 넣지 않는다.** 그 방식은 전달 후 원소 해석과 다른 참가자의 구슬을 바꿀 수 있다. 정상 게임의 생성은 승인 선택 맵, 구슬 판정/표시는 명시 원소를 사용한다. Config의 기존 TeamElements·ID hash는 legacy/fixture 경로와 구분하며 정상 생성의 원소 원본으로 사용하지 않는다.

## 5. 조합·공격 판정

### 5.1 같은 원소 조합 — 유지할 구현

- 현재 `HostOrbRegistry.ValidateAndReserve`와 `TryCompleteReservedCombination`의 Raw/반대 음양/동일 원소/소유자/Idle 검사를 유지한다.
- 동일 원소 규칙 자체는 #49를 유지하되, 두 Raw를 ID hash로 비교하던 지점을 명시된 원소 비교로 연결한다. 예약 전 검사와 commit 재검사 모두 같은 원소 원본을 사용한다.
- 서로 다른 원소가 거절됐을 때 재료 ID·사용 가능 상태·예약을 보존한다.
- 결과 Combined는 하나의 원소를 Yin/Yang 두 ID 슬롯에 동일하게 인코딩한다.
- 직접 드래그에서만 조합하며 자연 물리 접촉을 조합으로 바꾸지 않는다.
- **선택 속성과 다른 원소라도 같은 원소의 음양이면 조합할 수 있다.** 공격 제한과 조합 제한은 서로 다른 조건이다.

### 5.2 로컬 안내와 최종 Host 거절

로컬의 빠른 안내와 Host의 최종 검사를 모두 추가한다.

**로컬 경로:** 정상 상단 release에서 Combined의 두 원소 슬롯과 승인된 자기 선택을 비교한다. 불일치이면 요청을 보내기 전에 전용 경고창을 열고 제스처 Pending/샘플/preview를 정리한다. 시작 위치·속도0으로 복원하며 물리 release가 이를 다시 발사/전달하는 두 번째 행동을 만들지 않도록 한다.

**Host 경로:** `AttackAuthority.RequestLaunch`에서 인증 sender와 현재 owner/라운드 선택 맵을 확인하고, **등록부 예약·Launching 전** 동일 검사한다. 공격 요청에 실린 희망 속성을 신뢰하지 않는다. 선택 누락·원소 None·서로 다른 두 원소가 들어간 Combined도 정상 라운드에서 발사하지 않는다.

- 예시 거절 코드 `SELECTED_ELEMENT_MISMATCH`를 reply로 전달하고 로컬에서도 동일 경고를 표시한다. 정확한 코드명은 구현 시 일관되게 확정한다.
- 판정은 제작자가 아닌 현재 소유자 기준이다. 전달 후 소유권 승인과 선택 맵을 함께 사용한다.
- 거절은 구슬 소비/HP 변경/명중 회복을 만들지 않는다. 기존 요청 ID·sequence·영수증 처리와 충돌하지 않게 한다.
- 경고 패널은 한 번에 하나만 열고, 확인/라운드 전환/세션 종료에서 닫는다. 팀 타이머를 멈추지 않는다. 열려 있는 동안 새로운 구슬 제스처를 시작하지 않게 한다.
- 현재 숨겨진 `ActionStatus/ActionDetail`에 문구만 쓰는 방식으로 요구를 완료 처리하지 않는다.
- 과거 Smoke/테스트 전용 원소 없는 경로와 새 정상 세션을 명확히 분리한다. 정상 세션에서 선택 맵 누락을 이유로 검사를 자동 우회하지 않는다.

## 6. 주요 변경 지점

| 묶음 | 파일/현재 역할 | 예정 변경 |
|---|---|---|
| 로비 DTO·권한 | [LobbyContracts](../Assets/_Project/HapioMVP/Lobby/LobbyContracts.cs), [LobbyAuthority](../Assets/_Project/HapioMVP/Lobby/LobbyAuthority.cs), [LobbyWire](../Assets/_Project/HapioMVP/Lobby/LobbyWire.cs) | 선택 요청/선택 데이터/시작 계약/유효성·revision·Ready 검사 |
| 로비 연결·UI | [T10LobbySession](../Assets/_Project/HapioMVP/Lobby/T10LobbySession.cs), [T10LobbyController](../Assets/_Project/HapioMVP/Lobby/T10LobbyController.cs), [T10LobbyHud](../Assets/_Project/HapioMVP/Lobby/T10LobbyHud.cs), [LobbyUiState](../Assets/_Project/HapioMVP/Lobby/LobbyUiState.cs) | 버튼·선택 요청/승인 표시·명단 속성·자리 안내/패널 높이 |
| 게임 계약·Retry·로비 복귀 | [T10GameSession](../Assets/_Project/HapioMVP/GameSync/T10GameSession.cs), [GameWire](../Assets/_Project/HapioMVP/GameSync/GameWire.cs) | 선택·자리의 고정/검증/복사/hash/Client 적용/Retry ACK, 실제 로비 복귀와 라운드 재사용 방지 |
| 실제 자리 소비자 | [ParticipantRing](../Assets/_Project/HapioMVP/Networking/ParticipantRing.cs), [AttackSession](../Assets/_Project/HapioMVP/Attack/AttackSession.cs), [AttackAuthority](../Assets/_Project/HapioMVP/Attack/AttackAuthority.cs) | 전달 이웃과 Host 투척 프레임에 별도 자리 배열 적용. 입장 명단 용도와 분리 |
| 카메라·요괴 방향 | [ThrowBattleFraming](../Assets/_Project/HapioMVP/Battle/ThrowBattleFraming.cs), [MonsterAttackPresenter](../Assets/_Project/HapioMVP/Battle/MonsterAttackPresenter.cs), [ParticipantViewAngle](../Assets/_Project/HapioMVP/Presentation/ParticipantViewAngle.cs) | 고정 P번호 대신 실제 seatIndex로 각도 계산/재적용 |
| 구슬 조작·경고 | [T09BattleController](../Assets/_Project/HapioMVP/Battle/T09BattleController.cs), [T09Hud](../Assets/_Project/HapioMVP/Battle/T09Hud.cs), [ContinuousTransferBattle](../Assets/_Project/HapioMVP/Scenes/ContinuousTransferBattle.unity) | 로컬 제한·거절 복원·선택/자리 표시·저장 Canvas 경고 패널 |
| 생성·자원 | [HostResourceAuthority](../Assets/_Project/HapioMVP/Resources/HostResourceAuthority.cs), [ResourceSession](../Assets/_Project/HapioMVP/Resources/ResourceSession.cs) | Host 승인 선택 맵·활성 참가자에서 Raw 원소 후보를 만들고 별도 난수로 추첨, 비용/음양/중복 처리 유지 |
| 원소·조합·뷰 | [OrbModel](../Assets/_Project/HapioMVP/Orbs/OrbModel.cs), [OrbElements](../Assets/_Project/HapioMVP/Orbs/OrbElements.cs), [HostOrbRegistry](../Assets/_Project/HapioMVP/Orbs/HostOrbRegistry.cs), [OrbView](../Assets/_Project/HapioMVP/Orbs/OrbView.cs), [OrbElementCombinationTests](../Assets/_Project/HapioMVP/Tests/Combination/EditMode/OrbElementCombinationTests.cs) | Raw 명시 원소·clone 보존·조합 비교·뷰 연결, #49 규칙 회귀 |
| 호환·전송 | [AttackWire](../Assets/_Project/HapioMVP/Attack/AttackWire.cs), [ResourceWire](../Assets/_Project/HapioMVP/Resources/ResourceWire.cs), [CombinationWire](../Assets/_Project/HapioMVP/Combination/CombinationWire.cs), Lobby/Game 식별자·광고·GameWire 검증·주 실행 Scene | 공통 OrbWire와 각 계약의 Raw 원소 직렬화/hash·불변 검증, 새 계약 호환성 일치 |

파일 목록은 조사한 연결 지점이며 모든 파일을 반드시 수정한다는 뜻은 아니다. 작은 순수 helper를 추가하더라도 기존 asmdef 의존 방향을 유지한다. Attack이 Lobby 전체를 직접 참조하지 않고 GameSync가 승인 데이터를 전달하는 구조를 따른다.

## 7. 승인 후 구현 순서

1. **공유 데이터·권한부터:** 선택·유일 점유·Raw 원소·라운드 자리 데이터와 계약/hash 검증, 핵심 EditMode 시험.
2. **로비 선택 연결:** 다섯 버튼·점유 표시·Host echo·미선택 Ready 제한·변경 성공 시 전원 Ready 초기화·같은 방 로비 복귀·추가 승인된 경우 선택 해제 UI.
3. **선택 집합 원소 생성 연결:** 자원 권한→등록부→Raw snapshot→뷰/직접 조합, 전달 후 원소 불변. 활성 참가자들의 승인 속성 집합에서 추첨하고 기존 비용/음양/한도 유지.
4. **라운드 자리 연결:** 첫 시작/Retry, 전달 이웃·카메라·투척·요괴 방향 적용과 초기 ACK.
5. **공격 제한과 경고:** 로컬 검사/복원, Host 예약 전 검사, 새 경고 패널. #49 동일 원소 조합 회귀 확인.
6. **통합 검증·문서:** 관련 Edit/PlayMode, 3/5인 독립 실행, 준비된 실기기 검증. 실행 결과/미실행을 별도 기록.

이 단계는 한 브랜치의 승인된 기능을 안전하게 연결하기 위한 순서다. 작업 도중 무관한 UI 재설계, 물리 튜닝, Photon 도입, 패키지/Unity 변경은 포함하지 않는다. 계획 승인만으로 테스트·빌드·실기기 완료 상태를 기록하지 않는다.

## 8. 완료 기준과 압축 검증

AT56-01의 브랜치 확인은 읽기로 완료했다. **그 외 구현·실행 검증은 모두 예정 / NOT_RUN**이다. 수동 반복을 줄이도록 경우의 수는 자동 검사에 맡기고 실기기는 한 묶음 절차로 확인한다.

| ID | 확인 조건 | 기대 결과·검증 방법 |
|---|---|---|
| AT56-01 | 정확한 branch/upstream/base | 위 §1은 읽기 확인 완료. 구현 시작 때 다시 변동 확인 |
| AT56-02 | 다섯 속성 선택, None/비유효/타인/시작 후 변경 | 자기 선택만 Host 승인. 미선택 Ready 금지. 변경 시 Ready 초기화. EditMode + UI PlayMode |
| AT56-03 | 같은 속성 동시 요청·점유한 속성 변경·동일 재선택·이전 Ready/응답 지연·Pending 중 자기 버튼 연타 | 중복 선택 금지, 동시 요청 1명만 승인. 자기 Pending 중 추가 선택 요청 미발송. 거절은 기존 선택/점유/Ready 보존. 성공 변경 후 이전 속성 재사용 가능. 지연 Ready가 다시 붙지 않음 |
| AT56-04 | 2/3/5인 자리, Host가 첫 자리 아닌 사례 | 모든 참가자 정확히 한 번, 모든 기기 동일, 좌우 wrap·카메라·투척·요괴 방향 일치. 난수 생성은 고정 주입 시험과 실제 Host 실행을 구분 |
| AT56-05 | Retry와 오래된 라운드/자리 snapshot, Client 한 명 ACK 보류·이전 판 ACK 재생 | 선택 유지, 새 자리의 Ready 초기 상태 배포·적용 후 전원 hash ACK를 받아 Host 시작 허용. 보류/이전 판 ACK로 시작 불가. 같은 배치 재추첨 가능성을 실패로 세지 않음 |
| AT56-06 | 다섯 동일 원소의 Yin+Yang 양쪽 재료 순서, 다른 원소/같은 음양/자연 접촉 | #49 정상 조합 유지, 거절은 원본·예약 보존, 자연 접촉 무조합. 기존 관련 시험 재실행 |
| AT56-07 | 선택5종×Combined5종 | 일치5경우 발사 가능, 불일치20경우 거절. Host 직접 요청으로 로컬 검사를 우회해도 거절. ID/owner/Idle/미예약·HP/보상 보존 |
| AT56-08 | 선택 집합에서 생성된 한 원소의 Raw/Combined를 다른 속성 플레이어에게 전달·조합·공격 시도 후 그 원소를 선택한 플레이어에게 반환 | 전달 후 원소/ID 불변. 타 속성도 보관/같은 원소 조합/전달 가능, 공격만 거절. 반환 후 일치 소유자는 발사 가능 |
| AT56-09 | 경고창, 반복 시도, 확인, 라운드/세션 종료 | 실제 표시, 한 패널, 정상 복원, 원본 재사용/전달 가능, 시간 지속. 숨겨진 debug 문구로 대체하지 않음 |
| AT56-10 | 구/신 빌드 및 불완전 선택·자리 계약 | 구 빌드 입장 거절, 누락/중복/미참가 자리 및 선택을 초기 상태 단계에서 거절 |
| AT56-11 | 기존 전투 회귀 | 물리·연속 전달·자원·유효 충돌·방어·승패·Retry의 관련 시험. 전체/선택 subset 결과를 구분 |
| AT56-12 | 2~5인 승인 선택 집합의 Raw 반복 생성·음양, 요청자 선택 누락, 활성 참가자 밖의 선택 맵 항목, 맵 삽입 순서 변화, 생성 거절·중복 | 모든 Raw 원소가 현재 활성 참가자의 승인 선택 집합에 속함. 요청자 속성에 고정되지 않고 다른 참가자 원소도 나올 수 있음. 음양·비용·한도 유지, 실패·중복은 성공 순번과 다음 추첨을 바꾸지 않음. 짧은 무작위 표본에 모든 원소가 반드시 등장해야 한다고 판단하지 않음 |
| AT56-13 | 실제 로비 퇴장, 5인 마지막 원소·전원 선택, 추가 승인된 경우 선택 해제 | 퇴장 후 재사용, 마지막 원소는 명시 선택 후 승인, 포화 상태의 점유 변경 거절. 해제 포함 시 미선택/Ready 불가/다시 점유 가능 |
| AT56-14 | Raw wire enum/누락, 같은 ID의 원소 변경, 타 속성 owner로 전달 후 snapshot, 명시 원소와 ID hash가 다른 사례 | 정상 계약은 누락/변조 거절. 합법적 owner 변경은 원소를 유지하며 허용. 생성→직렬화→뷰→전달→조합까지 명시 원소 유지. 동일 ID의 원소를 복사/상태 전환에서 보존하고 hash에 반영 |
| AT56-15 | 결과/Retry 준비에서 Host LOBBY → 속성 변경 → Ready/재시작, 새 방 입장, 이전 판 메시지 재생 | 같은 방/참가자/P번호를 유지해 로비로 복귀하고 전원 Ready 해제. 선택 변경 후 새 생성/공격 자격 적용, 자리 재추첨, 새 라운드 승인. 새 방은 미선택. 이전 판 메시지로 재시작/피해 불가 |

### 실기기 한 묶음 절차

준비된 iPhone+iPad를 연결하고 필요하면 Mac을 세 번째 참가자로 사용한다. 같은 속성 요청의 거절 확인 → 각자 다른 속성 선택 → Ready/시작 → 양쪽 Raw가 **현재 참가자의 승인 선택 집합 안에서** 생성되는지·좌우/자리 표시 확인 → 한 속성의 음양을 다른 플레이어에게 보내 조합 → 다른 속성으로 공격 시 경고/원본 보존 → **그 원소를 선택한 플레이어에게 돌려보내** 투척/실제 HP 감소 → 결과/Retry의 선택 유지·자리 재확정 → 같은 방 로비 복귀·비점유 속성 변경·전원 Ready/재시작 순서로 검증한다. 몇 번의 무작위 생성만으로 후보 전체 출현을 요구하지 않는다. 중복 선택 금지이므로 별도의 두 번째 동일 속성 플레이어를 만들지 않는다. 구슬 준비·타격 등은 명시 fixture/자동화로 가능한 범위에서 압축하고 실제 손가락 조작과 구분한다.

2기기+Mac 검증은 모바일3~5대 검증으로 표시하지 않는다. 기기가 준비되지 않으면 iOS 설치/실기기는 NOT_RUN으로 남긴다. 자동 실행 결과는 XML의 총수/실패/누락과 실제 기준 commit을 기록한다. 현재 문서의 계획·테스트 소스 존재를 PASS로 옮기지 않는다.

## 9. 구현 시 주의할 확인점

- **P1과 첫 자리를 혼동하지 않는다.** 역할/입장 명단을 임의 shuffle하지 않는다. ACK와 송신 루프, legacy p1/p2 mirror를 유지한다.
- **표시와 각도의 분리:** 현재 controller의 `approvedPlayerNumber`는 P번호 표시와 투척 각도에 함께 쓰인다. 별도 seatIndex를 추가해 P표시를 바꾸지 않고 실제 배치만 바꾼다. `MonsterAttackPresenter.TargetYaw`와 `GameWire.ValidTransferProgression`도 동일 좌석 배열을 따른다.
- **원소 원본 통일:** 새 Raw는 명시 원소를 사용한다. ID hash와 현재 owner 선택을 동시에 원소 원본으로 사용하지 않는다. 전달/소비/스냅샷 레코드 복사에서 새 필드를 빠뜨리지 않는다.
- **선택 점유/지연:** 선택 성공만 전원 Ready와 배리어를 갱신한다. 변경 실패에 기존 선택을 잃거나, 이전 Ready가 재활성화되는 중간 상태를 만들지 않는다.
- **Retry와 로비 복귀 분리:** 현재 두 버튼이 같은 승인 Retry를 호출하는 경로를 유지하면 로비에서 속성을 변경할 수 없다. 방 연결을 유지한 실제 Lobby phase 복귀·다음 시작 계약을 함께 구현한다.
- **거절 후 구슬 복원:** 제스처 엔진이 이미 Pending을 만든 경우도 처리한다. 취소 상태와 비행 승인 상태를 혼동해 복제하지 않는다.
- **숨겨진 안내:** 현재 Scene의 ActionStatus/ActionDetail은 일반 사용자 경고창의 대체물이 아니다.
- **한글 렌더링:** 현재 로비는 `LegacyRuntime.ttf`를 사용하며 조사한 프로젝트에 별도 TTF/OTF가 보이지 않았다. `화·수·목·금·토`와 경고 문장이 iOS에서 읽히는지 확인하고, 필요한 경우 해당 UI용 CJK 폰트 자산만 추가한다. 전체 UI 시스템을 바꾸지 않는다.
- **기존 문서 차이:** README/AGENTS의 과거 ‘오행/공격/방어 없음’ 문구는 현재 연결과 다르다. 이번 기능 판단은 실제 코드 기준이며 계획에 과거 PASS를 승계하지 않는다.

## 10. 승인 범위

사용자에게 §3의 기본안과 §7~8의 구현/검증 범위를 점검받은 뒤 시작한다. 이번 파일을 작성한 시점에는 게임 코드·Scene·Prefab·에셋·패키지를 수정하지 않았고 Unity 실행·빌드·검증도 수행하지 않았다.

r02 작성 당시 사용자 수정으로 **속성 중복 금지**, **Raw는 자기 선택 속성만 생성**, **Retry는 선택 유지·자리 재추첨**을 계획에 반영했다. 이후 Raw 규칙은 r04에서 **현재 방의 활성 참가자들이 승인받은 속성 집합에서 무작위 생성**으로 바뀌었다. 선택 해제 버튼은 r03 구현 승인에 포함되었다. 이 절의 계획 당시 NOT_RUN 기록은 후속 실행 결과로 바꾸지 않는다. 커밋·push·PR은 후속 정리 요청 범위에서 수행한다.

### 계획 변경 기록

| 버전 | 내용 | 구현/검증 상태 |
|---|---|---|
| r01 | 최초 브랜치/코드 조사와 제안. 중복 허용·오행 랜덤 생성 기본안 | 계획만 작성, 게임 실행 NOT_RUN |
| r02 | 사용자 수정 8규칙 반영. 중복 점유의 원자 승인/거절 보존, 당시의 자기 선택 원소 생성·명시 Raw 데이터·전달 불변, Retry/같은 방 로비 복귀 분리, 검증 및 구현 순서 보완. 선택 해제 UI는 별도 제안 | 계획만 수정, 코드/Scene/에셋 변경 없음, 실행 NOT_RUN |
| r03 | 선택 해제 포함 구현 승인 및 최초 #56 구현 | 실행 결과는 구현 기록과 2026-09-29 검증 기록에 구분 |
| r04 | 사용자 후속 지시로 Raw 원소를 활성 참가자들의 승인 속성 집합에서 Host가 무작위 선택하도록 변경. 방 완전 퇴장 후 새 방의 로스터/선택 잔존 오류에 대한 정리 경로를 추가 | 수정 소스의 실행 검증은 별도 기록이 있어야 PASS |

## r03 적용 확정

- `CLEAR SELECTION`은 로비에서 승인된 자기 선택을 None으로 바꾸는 요청이다. 전원 Ready를 해제하고 점유를 반환한다. None이면 Ready 불가이며 이미 None인 경우 버튼은 비활성이다.
- 선택/해제 요청 중 추가 선택·해제·Ready를 막는다. Host가 확인한 선택만 표시한다. 선택 취소·점유 거절은 기존 상태를 유지한다.
- 결과 또는 Retry Ready에서 Host의 `ROOM LOBBY`는 연결을 유지한 실제 방 로비 복귀다. Retry와 별도로 동작하며, 재시작은 새로운 roundId와 선택·자리 계약을 사용한다.
- 첫 시작/Retry의 새 자리는 이전과 같을 수 있다. 자리 변화 여부 자체를 통과 조건으로 삼지 않는다.
- 현재 UI는 기존 영문 로비와 동일한 폰트를 유지하며 Fire(화)·Water(수)·Wood(목)·Metal(금)·Earth(토)로 표시한다. 별도 한글 폰트 추가·패키지 변경은 하지 않았다.
- 커밋·push·PR은 이번 구현 요청으로 자동 수행하지 않는다.

| 버전 | 내용 | 상태 |
|---|---|---|
| r03 | 사용자 승인에 따라 선택 해제 버튼 포함 구현 진행. r02의 확정 8개 규칙 유지 | 구현 및 이번 실행 근거는 별도 구현 기록 참조 |

## r04 후속 수정 범위 (2026-09-30)

- Raw 생성 요청자의 승인 선택 유무는 계속 검사한다. 생성할 원소는 요청자 본인 속성으로 제한하지 않고, 현재 게임의 활성 참가자들이 승인받은 선택 속성을 후보로 사용한다. 후보는 중복 없이 정렬하고 Host가 별도 난수 흐름으로 한 원소를 선택한다. 따라서 선택되지 않은 오행은 이번 판의 정상 Raw 후보가 아니다.
- 공격 권한은 기존대로 **투척 순간 현재 소유자의 선택**과 Combined 원소를 비교한다. Raw 추첨 방식만 달라지고 조합·전달·경고·복원 규칙은 유지한다.
- 첫 실기기 실행에서 같은 앱을 종료하지 않고 새 방에 재입장한 뒤 전투 시작이 실패했다. 과거 방의 `AttackSession` 승인 로스터와 선택 맵이 남아 있었고 새 연결의 참가자 ID와 충돌했다. 완전 퇴장 경로에서 승인 방 상태를 비우도록 수정했다. 실패한 빌드의 기록은 [2026-09-30 실기기 검증](validation/ELEMENT_SELECTION_IOS_20260930.json)에 그대로 남긴다. 수정 빌드의 새 방 재입장 검증은 별도 결과로 판정한다.
