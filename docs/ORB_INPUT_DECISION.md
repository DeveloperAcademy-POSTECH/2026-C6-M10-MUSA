> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T05 · 구슬 모델과 입력 중재 결정

이 문서는 통합본 PROJECT INPUT 2.4·2.7·2.9와 T05에 따라 구현한 계약이다. 코드 검토 시점의 설명이며 실행 결과를 대신하지 않는다. 컴파일·자동 시험·Mac 실행·iOS export·실기기 결과는 [VALIDATION.md](VALIDATION.md)에서 각각 확인한다. 반복 절차는 [T05_RUNBOOK.md](T05_RUNBOOK.md)를 따른다.

## 개발 공급과 권한 경계

`OrbInputSmoke.unity`는 저장된 T04 `BattleLayout.unity`를 보존하면서 분리한 개발 입력 씬이다. 상단 허수아비와 카메라 구조를 재사용하고 하단에 `DEV HOST`, `RESET FIXTURE`, `END`를 제공한다. 씬을 여는 것만으로 연결이나 구슬 공급을 시작하지 않는다.

Editor 또는 Development Build에서 사용자가 DEV HOST를 선택하면 기존 `DirectConnectionSession`으로 실제 NGO Host를 시작한다. 서비스는 소유한 NetworkManager의 `IsHost`와 연결 상태를 확인한 다음 개발 레지스트리를 연다. 송신자와 구슬 소유자는 UI 입력값 대신 그 NetworkManager의 실제 `LocalClientId`를 사용한다. 이는 Host 로컬 입력 경로이며, Client 구슬 동기화·게임 요청 RPC·자동 방 탐색의 구현을 뜻하지 않는다.

아래 세 구슬은 고정 개발 Fixture다. 위치는 하단 카메라 영역 안의 정규화 좌표이고 새 등록마다 새 GUID를 갖는다.

| 개발 구슬 | 종류·음양 | 위치 |
|---|---|---|
| 음 | Raw / Yin | (0.30, 0.54) |
| 양 | Raw / Yang | (0.46, 0.54) |
| 완성 | Combined / None | (0.73, 0.54) |

이 공급은 일반 최초 Raw 5개 생성·Stamina 5 소비가 아니다. Seed 추첨·소비·회복을 실행하지 않는다. T07의 생성 규칙과 T08의 조합 결과를 미리 구현한 것으로 기록하지 않는다. HUD·로그에 개발 모드와 예약만 수행한다는 범위를 표시한다.

## 확정 구슬·로컬 상태·예약 분리

`OrbRecord`는 `OrbId`, 종류, 음양, `OwnerPlayerId`, 권한 상태, `NormalizedPosition`, `EntrySide`, `SequenceNumber`를 갖는 변경 불가 데이터다. 종류 Raw/Combined, 음양 Yin/Yang/None, 권한 상태 Idle/Launching/Projectile/Consumed를 구분한다. Dragging/Pending은 로컬 표시 상태이며 Combined를 상태 enum에 섞지 않는다. Element는 게임 규칙에 사용하지 않는다.

전달·발사의 계약은 같은 ID를 유지하는 것이고, 후속 조합 구현의 계약은 재료 두 ID 종료 후 새 Combined ID 생성이다. **T05는 이 전환들을 실행하지 않고 예약만 한다.** 예약된 구슬도 레지스트리 안에서는 같은 ID·소유자·Idle 상태·확정 위치로 남으며 구슬 총수도 유지된다. 2D 뷰를 없애거나 Projectile을 만들지 않는다.

행동의 원본 좌표와 순서는 별도 불변 `OrbActionRequest`에 담는다. 요청에는 sessionId·roundId·requestId·대상 ID·필요한 두 번째 ID·행동 종류·sequence·행동 시점의 정규화 좌표가 포함된다. T05의 확정 레코드 sequence는 초기값을 유지하고, 레지스트리의 별도 마지막 수락 sequence가 증가 조건을 검사한다. 매 프레임 드래그 위치는 로컬 표시 자료일 뿐 호스트 확정 상태나 네트워크 snapshot으로 복제하지 않는다.

`HostOrbRegistry`는 순수 상태 객체이며 실제 Host 인증은 호출 서비스의 책임이다. 레지스트리는 활성 세션·라운드, 요청 ID, 실제 송신자 소유권, 사용 가능한 Idle 상태, pending 여부, 유효한 종류·음양, 유한한 [0,1] 좌표, 증가하는 sequence를 검사한다. 같은 requestId와 같은 송신자·payload의 재전송은 이전 수락/거부 영수증을 돌려주며 두 번째 예약을 만들지 않는다. 같은 requestId의 payload나 송신자가 달라지면 충돌로 거부한다.

Raw 발사, 같은 ID의 조합, 같은 음양, 다른 소유자, Raw+Combined, Combined+Combined, 없는 재료, 이미 잠긴 재료는 거부한다. 조합은 같은 소유자의 사용 가능한 반대 음양 Raw 두 개를 모두 검증한 후 함께 예약한다. 두 번째 재료가 실패해도 첫 재료만 잠기는 부분 성공은 없다.

## Pending의 의미와 해제

서비스가 레지스트리의 동기 수락/거부를 받은 뒤 `OrbGestureEngine.ResolvePending`을 호출한다. 여기서 해제하는 것은 **입력 엔진의 전달 대기 후보**다. 수락한 레지스트리의 구슬 잠금은 유지하고 해당 뷰를 LOCKED로 표시한다. 다른 Idle 구슬은 새 Pointer로 잡을 수 있지만, 같은 손가락을 계속 움직여 추가 요청을 만들 수 없다.

T05에는 발사·전달·조합 완료 응답이 없으므로 수락된 구슬을 다시 조작하려면 명시적인 RESET FIXTURE를 사용한다. 이 동작은 roundId를 증가시키고 이전 Fixture·예약·영수증을 비운 다음 새 ID의 개발 구슬 세 개를 공급한다. 같은 roundId나 과거 roundId로 잠금을 풀 수 없다. END 또는 연결 종료는 개발 세션 전체를 무효화하고 소유한 연결·입력·뷰를 정리한다. 이것은 후속 전투 Reset·자원 복원의 구현이 아니다.

타임아웃, Pointer Up, Touch 취소, 포커스 상실, 화면 변화만으로 레지스트리의 pending을 풀지 않는다. 실패 응답을 확인한 경우에는 레지스트리 원본을 유지하며 로컬 입력과 표시만 복구한다. 후속 비동기 네트워크 구현에서도 응답 누락은 호스트 조회로 해결해야 하며 추측으로 복제·복구를 확정하면 안 된다.

## 입력과 중앙 설정

T04의 `Config/ScreenLayoutConfig.asset`와 GUID를 유지하고 같은 원본에 T05 필드를 추가했다. 화면 비율을 다른 Config에 복제하지 않는다.

| 항목 | 값·단위 | 근거 |
|---|---|---|
| 상단 / 하단 | 0.55 / 1−상단 | 기존 DEMO_TUNING_VALUE |
| 수평 Swipe 거리 | 전체 화면 너비의 0.18 | 통합본 DEMO_TUNING_VALUE |
| 수평 방향 우세 | abs(dx) ≥ abs(dy) × 1.25 | 통합본 DEMO_TUNING_VALUE |
| Raw Drop 거리 | 전체 화면 너비의 0.08 | 통합본 DEMO_TUNING_VALUE |
| Attack Zone | 실제 하단 viewport의 위쪽 높이 18% | DEMO_ASSUMPTION: 최초 구역 높이 선택 |
| 표시 구슬 반지름 | 전체 화면 너비의 0.055 | 표시용 DEMO_TUNING_VALUE |
| 방어 | EnableDefense=false, IsDefenseHeld=false | 예약 변수만, Long Press 없음 |

Attack Zone의 18%는 구역 높이이며 보류된 위쪽 Throw 거리 0.15 화면 높이 규칙을 채택한 것이 아니다. 하단 카메라의 실제 pixelRect에서 구역을 유도한다. 수평 Swipe와 Drop 거리의 기준은 하단 높이나 기기 DPI 대신 전체 화면 너비다. 별도 속도 기반 플릭은 없다. 포함 경계는 정규화 값의 Mathf.Approximately 동등성으로 float 오차만 처리하며 고정 픽셀 허용치를 추가하지 않는다.

설정의 비정상 수치는 기본값으로 복구하고 범위 밖 값은 제한한다. 수평 거리 0.01~1, 우세 비율 1~5, Drop 거리 0.01~0.5, Zone 높이 0.01~0.5, 표시 반지름 0.01~0.1 제한은 DEMO_ASSUMPTION이다. 기존 화면 비율 제한 0.01~0.99를 유지한다. 실행 중 화면 영역·Safe Area·입력 설정·표시 크기가 바뀌면 진행 중 드래그를 취소하고 새 제스처에서 새 설정을 사용한다.

SpriteRenderer와 선택용 CircleCollider2D로 구슬을 선택한다. Collider는 Trigger이고 Rigidbody/Rigidbody2D, 실제 밀어내기, 공격 Collider는 만들지 않는다. 동일 구슬을 여러 손가락이 잡거나 다른 구슬을 동시 드래그하는 기능은 P0에서 지원하지 않는다. 추가 Pointer는 기존 소유를 빼앗지 않고 자기 Up/Cancel 전까지 무시한다.

EnhancedTouch의 touchId를 추적하고 Editor Mouse도 같은 Begin/Move/End/Cancel 경로로 연결한다. UI 위에서 시작했는지는 그 시점의 GraphicRaycaster 결과로 판정해 Pointer 전체 수명에 유지한다. 장식 UI는 입력을 차단하지 않는다. Touch가 진행 중이거나 같은 프레임에 Touch를 처리한 경우 Mouse 경로를 억제해 한 입력이 두 번 처리되지 않도록 한다.

중재 순서는 다음과 같다.

1. UI 시작·추가 Pointer·소유권·이미 pending인 구슬을 제외한다.
2. 원본 Pointer 이동으로 Attack Zone 진입과 수평 Swipe를 검사한다. 표시 Clamp와 구슬을 잡은 오프셋이 행동 판정을 대신하지 않는다.
3. Combined가 Zone에 처음 진입하면 발사를 예약한다. 빠른 이동이 Zone을 통과하면 구간 교차로 감지한다. 같은 갱신에서 수평 Swipe도 충족하면 Zone을 우선한다. Raw는 이 경로로 발사하지 않는다.
4. 먼저 예약한 행동에 잠근다. 예약이 없던 Raw의 Pointer Up에서만 반대 음양 Raw의 Drop 거리를 검사한다. 스치거나 겹치는 것만으로 조합하지 않는다.
5. 로컬 구슬 표시를 Safe Area와 HUD 하단 제어 영역 안으로 Clamp한다. glyph 높이로 약12px 글자 크기를 계산하고 idle/LOCKED 최대 너비를 캐시해 화면 가장자리 글자도 보존한다. 요청 위치는 하단 viewport 기준 [0,1] 값으로 보관한다.

Touch 취소, 입력 중단, 앱 포커스/일시정지, 장치 제거, 화면 이탈, 연결 종료에서 Pointer 소유를 정리한다. 취소한 미예약 구슬의 표시 위치는 드래그 시작 위치로 돌아간다. 단순 배치는 로컬 표시 위치만 바꾸며 확정 레지스트리 위치를 쓰지 않는다.

## 보존과 증거 경계

T01~T04 저장 씬·기존 meta·사용자 설정·검증 기록을 보존한다. T05 Prepare는 기존 T04 씬을 Preview로 읽어 별도 씬을 만들며 이미 저장된 T05 씬을 통째로 재생성하지 않는다. T05가 활성 Build Scene이 되므로 T04 회귀 시험은 T04의 보존과 구조를 검사하고 T04만 활성이라는 과거 Task 전용 가정은 현재 활성 씬과 구분한다. T04 HUD의 사용 중단 예정 검색 API는 같은 중복 방지 목적의 현재 API로 교체한다.

증거는 다음 범위로 나눠 기록한다.

- EditMode: 순수 레지스트리·Gesture 계약의 실제 실행 결과.
- PlayMode: 저장 씬·실제 NGO Host와 큐에 넣은 Input System Mouse/Touch 이벤트의 실제 실행 결과. 합성 이벤트이며 손가락 시험이 아니다.
- T05Probe: 실제 Mac 개발 플레이어와 실제 렌더에서 실행하지만 Pointer 서비스 호출은 명시적인 Debug 주입이다. Host가 실제라는 사실이 입력까지 실물이라는 뜻은 아니다.
- Mac UI 직접 조작: 앱 창에서 실제 Pointer/UI 경로로 수행한 동작과 관측값. Probe 결과로 대신하지 않는다.
- iOS export: Xcode 프로젝트 생성과 설정 확인. Xcode 앱 빌드·서명·설치·실기기 실행과 분리한다.
- T06 실기기: 실제 손가락 드래그와 화면·Safe Area·2D→3D·피격 Gate. T05 코드·합성 Touch·Mac 결과를 승계하지 않는다.

현재 문서는 시험 절차와 구현 계약을 제공하며 실행 성공을 선언하지 않는다. 다음 Task 후보는 T06이고, 실제 2D→3D 전환·피격 및 직접 드래그 20회 Gate를 별도 요청에서 수행한다. T06은 자동 시작하지 않는다.
