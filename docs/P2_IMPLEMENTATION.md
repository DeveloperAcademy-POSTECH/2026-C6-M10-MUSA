> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# P2 · 손떼기 3D 투척 구현

2026-09-14. 기준 커밋 `97ca744`, Unity 6000.5.7f1, 새 `ThrowBattle` 씬·빌드22. 사용자가 승인한 새 계획 중 P2 하나를 적용했다. 최대5인 연결은 P3 후속 작업이다. 실행 결과는 [P2 검증 기록](P2_VALIDATION.md)에서 별도로 관리한다.

## 조작과 게임 결과

Combined를 하단에서 잡아 상단으로 올리면 손을 따라가는 확대·빛 표시와 THROW READY가 나타난다. 아직 구슬을 소비하지 않는다. 위로 움직이면서 정상적으로 손을 놓으면 최근 입력으로 던지며, 다시 하단으로 가져오면 기존 조합·좌우 전달·2D 관성 놓기를 적용한다.

상단에서 멈춘 뒤 놓기, 약한 흔들림, Raw, 취소 입력, UI에서 시작한 입력, 다른 손가락은 새 공격을 만들지 않는다. 기준 미달 투척은 구슬을 하단 유효 위치로 돌려놓는다. 승인 대기는 실제 Host 응답으로만 해제한다. 타임아웃으로 구슬을 다시 만들지 않는다.

Host가 소유자·세션·라운드·구슬 ID·요청 ID·순서·입력을 검증한다. 유효한 손떼기만 한 번 승인하며 같은 요청을 재수신해도 같은 초기 속도를 다시 계산하거나 발사하지 않는다. 실제 Rigidbody와 Collider가 표적에 닿아야 피해20·공격자 회복5가 한 번 적용된다. 바닥은 표적 식별자가 없어 접촉 자체로 피해를 만들지 않는다. 빗나간 공은4초 뒤 소비 상태로 정리된다.

## Unity 초보자를 위한 코드 위치

| 바꾸고 싶은 부분 | 시작할 파일 | 역할 |
|---|---|---|
| 손떼기 감도 | `Config/ScreenLayoutConfig.asset` | 아래 입력·중력·반발 등 한 개 원본 설정 |
| 최근 손동작 수집 | `Orbs/ThrowGestureSampler.cs` | 위치와 단조 증가 시간을 모으고 최근 구간을 계산 |
| 투척 준비·취소 | `Battle/T09BattleController.cs`, `Orbs/OrbGestureEngine.cs` | 잡기→상단 준비→손떼기의 우선순위 |
| 손 위치 표시 | `Battle/ThrowHeldPreview.cs` | 물리나 피해가 없는 Canvas 표시 |
| 방향·세기 변환 | `Attack/ThrowMapping.cs` | 화면 너비 기준 속도를 월드 초기 속도로 변환 |
| 실제 비행·충돌 | `Attack/HostProjectile3D.cs` | Host의 중력, 충돌, 반발, 수명 |
| 승인·재전송·동기화 | `Attack/AttackAuthority.cs`, `AttackSession.cs`, `AttackWire.cs` | 소비 전 검증과 같은 ID의 비행 상태 |
| 다른 기기에서 보이는 공 | `Battle/BallisticProjectileDisplay.cs` | Host 위치·속도로 최대0.1초 표시 예측 후 보간 |
| 몬스터 구도·바닥 | `Battle/ThrowBattleFraming.cs`, `ThrowBattleFloor.cs` | 실제 HUD 공간에 표적을 맞추고 바닥 충돌 연결 |

위 경로는 모두 `Assets/_Project/HapioMVP/` 아래다. 표시용 공에는 Collider를 두지 않으며 Client가 피해를 다시 계산하지 않는다. 기존 고정 조준 씬은 별도 옵션을 켜지 않는 한 이전 공격 경로를 유지한다.

## 조절값과 단위

모두 **DEMO_TUNING_VALUE**이며 실제 손가락 조작감 확정값이 아니다. 화면 X와 Y를 모두 전체 화면 너비로 나눠 화면 비율에 따른 감도 차이를 줄인다. 손떼기 직전0.12초를 보간하고 최소0.02초의 관측을 요구한다. 멈춰 있는 동안에도 위치를 기록해 예전의 빠른 움직임을 재사용하지 않는다.

| Config 필드 | 기본값 | 의미 |
|---|---:|---|
| `throwSampleWindow` / `throwMinDuration` | 0.12 / 0.02초 | 최근 수집 구간 / 최소 관측 시간 |
| `throwMinUpSpeed` / `throwMaxInputSpeed` | 0.35 / 8 | 화면 너비/초 기준 최소 상향·최대 입력 속도 |
| `throwForwardGain` / `throwUpGain` / `throwLateralGain` | 8 / 2.8 / 6 | 전방 Z·상향 Y·좌우 X 속도 변환 비율 |
| `throwMaxWorldSpeed` | 30 unit/초 | 초기 월드 속도 상한 |
| `throwGravity` | 9.81 unit/초² | 아래 방향 가속도 |
| `throwBounce` | 0.45 | 공의 반발 |
| `throwLifetime` | 4초 | 실제 명중하지 않은 공의 최대 수명 |
| `projectileRadius` | 0.165 unit | 기존 공통 충돌 반지름 |
| `launchOrigin` / `launchWidth` | (0,1.08,-4.5) / 3 | 발사 기준·화면 X에 대응하는 좌우 범위 |
| `battleFramingPaddingFraction` | 0.025 | HUD를 제외한 표적 영역의 여백 |
| `throwFloorY` / `throwFloorSize` / `throwFloorFriction` | 0 / (18,0.2,18) / 0.2 | 바닥 윗면·크기·접촉 마찰 |

예를 들어 최근 상향 속도1.25 화면너비/초를 중앙에서 놓으면 초기 속도는 `(0,3.5,10)`이다. 이후 궤적은 물리가 계산한다. 기존 `launchAim`으로 표적을 향하게 보정하지 않는다. 손동작의 방향·세기가 달라지면 빗나갈 수 있다.

자동 입력 비교는 화면 너비가 다른 동일 비율 입력과 불규칙 시간 샘플을 사용한다. 최근 구간의 위치·시간 비교 허용 오차는 0.000001, 초기 속도 비교는 0.00001 unit/초이며, 실제 기기 프레임률별 손가락 조작감 비교와 구분한다. 2인 확장 인벤토리의 용량 검사는 최대128자 ID의 구슬64개와 발사체64개를 명시적으로 구성한다. 5인×20개 용량 검증은 P3에 남긴다.

중력은 공에 `AddForce(Acceleration)`로 적용한다. 전역 `Physics.gravity`나 다른 씬의 물리를 변경하지 않는다. Rigidbody 초기 속도는 발사 때 한 번 설정하고 매 프레임 덮어쓰지 않는다. [Unity 6000.5 linearVelocity](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/Rigidbody-linearVelocity.html)·[연속 충돌 감지](https://docs.unity3d.com/6000.5/Documentation/Manual/ContinuousCollisionDetection.html)를 확인했다.

몬스터 구도와 바닥의 상세 결정은 [씬 적용 기록](P2_SCENE_DECISION.md)을 참고한다.

## 설정 합의와 보존

Host 설정 패킷은 schema2로 구분하고, 새 투척·표적·바닥·구도 값을 엄격한 필수 필드 검사와 설정 지문에 포함한다. 각 기기는 기존 한 개 Config의 실행용 사본에 승인된 값을 적용한다. 저장 에셋은 덮어쓰지 않는다. 조절 후 새 방을 만들고 양쪽을 같은 빌드로 실행한다. 진행 중인 방의 Inspector 임의 변경을 동기화된 설정 변경 기능으로 취급하지 않는다.

P2 공격 메시지는 `C6.P2.*`로 분리해 이전 직접 IP 공격 메시지가 새 투척으로 해석되지 않게 한다. 빌드 식별자22도 로비 합의에 사용한다. 큰 인벤토리 snapshot만 유한64KiB로 확장하고 일반 요청·응답의16KiB 상한은 유지한다. 새로운 곡선 속도·중력·경과시간·수명을 포함한 실제 최대 용량을 검사한다.

원본 Editor를 유지하고 `/private/tmp/C6_Prototype_NextPhase_P2`에서 실행한다. 이전 씬·Prefab·`.meta`·과거 검증은 보존하며 이관 전 원본 해시를 비교한다. 생성된 URP 설정 재직렬화는 명시적 기능 변경이 아니므로 이관 대상에서 제외한다.
