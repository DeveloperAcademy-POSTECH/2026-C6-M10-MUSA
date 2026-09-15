> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T06 · 2D→3D 공격 전환과 실제 피격 결정

이 문서는 T06 구현 계약과 검증 범위를 설명한다. 코드·설정·절차의 존재를 PASS 근거로 사용하지 않으며, 본문의 실제 실행 사례와 최종 [VALIDATION.md](VALIDATION.md)·`docs/evidence/T06/`를 구분한다. 현재 자동 시험과 Mac 화면 결과를 실제 iPhone 검증으로 확대하지 않는다. 선행 T05의 기록은 `docs/history/2026-09-13-t05/`에 보존한다.

## 범위와 명시적 개발 모드

T06은 기존 T05 입력 계약으로 Combined를 실제 Host의 3D 투사체로 전환하고 고정 표적의 물리 충돌을 확인한다. 입력만 예약했던 T05 씬은 유지하고 `Assets/_Project/HapioMVP/Scenes/AttackSmoke.unity`에 새 동작을 연결한다.

DEV HOST 또는 직접 IP JOIN을 명시적으로 실행해야 공격용 세션과 구슬을 준비한다. `DirectConnectionSession`이 연결됐다는 사실만으로 공격 권한을 만들지 않는다. 일반 앱 실행에서 자동 Host·자동 공격·자동 시험을 시작하지 않는다. 개발 모드가 아닌 빌드에서는 이 Fixture 경로를 시작하지 않는다.

참가자별 공급은 **Combined 5개 + Raw Yin 1개**다. C1~C5는 직접 드래그 시험용이고 Raw는 공격 거부 확인용이다. Host 레지스트리가 GUID와 실제 참가자 소유자를 부여한다. 이는 명시적 `DEV_PHYSICS` Fixture이며 최초 Raw5 생성, Seed 음양 분포, 조합, 비용 또는 Stamina 시스템을 구현한 결과가 아니다.

전체 Battle State와 Host Clock은 후속 Task다. T06은 공개된 개발 시험 상태 `NotStarted → Playing → TargetCleared / Ended / NetworkError`로 발사·피격을 제한한다. HP0의 `TargetCleared`는 시험 표적 완료이며 정식 Victory가 아니다. 숨은 always-playing 경로나 Team HP 감소·타이머·승패·자동 재접속을 추가하지 않는다.

## 같은 ID의 권한 상태 전환

기존 `OrbRecord`와 `HostOrbRegistry`를 재사용한다. 레지스트리의 T05 `Reserve`는 계속 예약만 하며, T06용 확인 메서드 두 개로 다음 전환을 추가한다.

```text
Combined Idle
  → 검증된 Launch 예약
  → Launching (동일 OrbId, 소유자, 행동 시점 정규화 위치·sequence)
  → Projectile (실제 Rigidbody/SphereCollider 생성 확인)
  → Consumed (유효 피격, 만료, 생성 실패 또는 개발 라운드 정리)
```

Launching 승인에서 로컬 2D 뷰를 제거한다. 승인 응답 뒤 오래된 Idle snapshot이 도착하더라도 제거된 뷰를 다시 만들지 않는다. 논리 구슬은 비행 중 Projectile로 남고 2D 제거만으로 미리 Consumed가 되지 않는다. 같은 ID를 재발사하거나 새 복제품으로 복원하지 않는다. Raw 공격 거부는 원래 구슬과 sequence를 유지한다.

권한 상태는 로컬 Dragging/Pending과 별개다. 실제 손가락 이동은 로컬 표시 위치로 관리하고, 요청에 행동 시점의 정규화 위치만 전달한다. T06은 실제 조합·전달을 실행하지 않으며 수평 행동 등 Launch 이외 요청을 거부한다.

## 요청·응답과 권한 경계

실제 NGO Host 서비스가 요청을 처리한다. 원격 송신자는 NGO 콜백의 sender ID이고, 메시지에는 신뢰할 별도 claimed sender 필드를 두지 않는다. 현재 두 참가자 목록과 연결 시 생성한 nonce를 확인한다. Host의 로컬 입력 역시 실제 LocalClientId를 사용한다.

순수 `AttackAuthority`는 명시적으로 시작한 현재 개발 라운드에서만 새로운 공격을 받는다. 기존 레지스트리 검증으로 sessionId·roundId·requestId·OrbId·owner·Combined/None 종류·Idle 상태·기존 잠금·단조 증가 sequence·유한한 [0,1] 정규화 좌표·불필요한 두 번째 OrbId를 검사한다. 실제 Host 확인과 신뢰할 물리 콜백 연결은 서비스 책임이다. 클라이언트가 보내는 hit RPC는 없다.

최초 Attack Zone 진입은 기존 `OrbGestureEngine`이 원본 Pointer 이동 경로와 선분으로 판단한다. 같은 갱신에서 Zone과 수평 이동이 겹치면 Zone을 우선하고, 먼저 예약한 행동은 유지한다. 이 최초 진입 이력은 로컬 입력 계약이며 모든 손가락 이력을 서버에 복제하거나 Host가 다시 증명하는 프로토콜은 아니다. Host는 요청의 권한·상태·좌표를 검증한다. Zone 높이18%는 구역 가정이며 위쪽 Throw 거리나 속도별 위력 규칙이 아니다.

같은 requestId와 동일 송신자·payload는 원래 승인 또는 거부 영수증을 재응답한다. 중복 승인에는 새 생성 지시가 없으므로 두 번째 3D 투사체를 만들지 않는다. requestId를 다른 송신자·OrbId·종류·좌표·sequence로 재사용하면 충돌로 거부한다. 현재 라운드 밖의 오래된 요청·충돌은 현재 결과를 바꾸지 않는다.

응답 누락 때는 같은 요청의 상태 조회만 보낸다. 조회는 불변 영수증과 **조회 시점의 확정 Orb 상태·잠금**을 구분해 반환한다. 알려진 승인과 현재 Projectile/Consumed를 과거 Launching snapshot으로 되돌리지 않는다. 알려지지 않은 요청도 해당 송신자 소유의 확정 상태만 반환하며 새 OrbId·발사·잠금 해제를 만들지 않는다.

로컬 입력은 확인되지 않은 요청을 잠근 채 3초 후 조회하고, 8초까지 확인이 없으면 `NetworkError`로 세션을 종료한다. 단순 타임아웃으로 복제·재발사·Idle 복원을 확정하지 않는다. 확정된 새 라운드나 세션 종료에서는 로컬 Pointer·Pending·오래된 요청·뷰를 정리한다.

## 실제 물리와 한 번의 피해

Host만 `HostProjectile3D`를 만든다. 실제 dynamic Rigidbody와 non-trigger SphereCollider, 중력 없음, 회전 고정, ContinuousDynamic 충돌 검출을 사용한다. 고정 표적에는 `MonsterHitTarget`과 non-trigger BoxCollider가 있으며 AI·이동·반격은 없다.

투사체는 설정된 속도로 실제 공간을 비행한다. HP를 줄이는 유일한 경로는 Host 소유 투사체가 현재 표적의 실제 Collider 충돌을 보고하고, 현재 session/round·OrbId·공격자·Projectile 상태·Playing 조건을 통과한 경우다. 타이머·즉시 호출·예정 도착 시각을 hit로 바꾸지 않는다. 수명 카운터는 빗나감 정리에만 사용한다.

물리 객체는 완료 표시를 먼저 설정하고 추가 collision callback을 막는다. 권한 객체는 해당 Orb를 Consumed로 전환하고 처리 집합에 기록한 뒤 HP를 한 번 낮춘다. hit 결과에 session/round·OrbId·AttackerPlayerId·실제 피해·이전/이후 HP를 남긴다. 이후 미래 T07용 `ValidHit` 이벤트를 한 번 내보내며 현재 Stamina를 생성하거나 회복하지 않는다.

실제 투사체끼리는 생성 시 object pair의 `Physics.IgnoreCollision`으로 충돌을 막는다. 프로젝트 전체 충돌 매트릭스를 변경하지 않는다. 빠른 속도의 관통 방지는 ContinuousDynamic 설정의 존재와 별개로 실제 물리 시험에서 확인해야 한다. 만료·표적 거부·생성 실패는 무피해 정리다.

표적 HP0이면 TargetCleared로 입력과 추가 피해를 막고 남은 비행 논리 구슬과 실제 투사체를 정리한다. Host RESET은 roundId를 증가시키고 HP·라운드별 hit·구슬 ID·투사체·요청 기록을 새로 만든다. 세션 누적 hit·Reset 수와 로컬 Touch 승인 수는 20회 시험을 위해 라운드 사이에 유지하며 새 Host/Join 시 초기화한다.

## 단일 Config와 화면 연결값

원본은 기존 `Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset` 하나다. 기존 GUID와 T04/T05 값을 유지하며 다음 T06 필드를 확장한다.

| 항목 | 현재 기본값 | 구분 |
|---|---:|---|
| Monster Max HP / Base Damage | 100 / 20 | DEMO_TUNING_VALUE |
| Projectile Speed / Lifetime | 12 units/s / 3초 | DEMO_TUNING_VALUE |
| Launch Origin | (0, 1.08, -4.5) | DEMO_ASSUMPTION, 공통 전장 좌표 |
| Launch Width | 3.0 units | 화면 전환 표시 튜닝 |
| Launch Aim | (0, 1.4, 0) | DEMO_ASSUMPTION, 고정 연습 표적 중심 |
| Projectile Radius | 0.165 units | 표시·물리 튜닝 |
| Attack Snapshot Rate | 20Hz | DEMO_TUNING_VALUE, 동일 Config에서1~60Hz로 유효 범위 제한 |
| 상단 / 하단 | 55% / 45% | 기존 설정 유지 |
| Attack Zone | 실제 하단 viewport의 위쪽18% | 기존 구역 가정 유지 |

발사 원점은 `Origin + HorizontalAxis × ((normalizedX - 0.5) × Width)`로 정한다. 고정된 연습 표적 Aim 방향으로 발사하며 정규화 Y는 힘이나 피해로 바꾸지 않는다. `AttackLaunchFrame`이 이 명시적 기준을 공급하므로 게임 규칙이 `Camera.main`, Host의 화면 크기 또는 Host 카메라 객체를 읽지 않는다.

두 기기는 같은 기본 몬스터 구도를 로컬 렌더링한다. 2D 경계의 가로 위치·표시 크기와 3D 시작점의 연결감은 이 조정값으로 실기기에서 확인해야 한다. 기본값을 바꾸면 값·이유·실제 재검증 대상을 기록한다. 화면 캡처나 설정값만으로 실기기 연결감·Safe Area·60fps 목표를 충족했다고 기록하지 않는다.

Mac v1의 실제 Debug Probe는5회 물리 피격·HP100→0을 통과했지만 첫 발사 위치가 상단 viewport 아래에 있어 초기 비행의 화면 검토는 FAIL이었다. 발사 기준을 `(0,0.55,-4.5) → (0,1.08,-4.5)`, 폭을 `3.5 → 3.0`, 반지름을 `0.20 → 0.165`로 조정했다. HP100·피해20·속도12·수명3초는 유지했다.

Mac v2의 실제390×844 단독 Probe는5회 물리 피격과 화면에 보이는 중간 비행을 확인했다. 최초 발사의 화면 X는35.94px(하단 입력35px), 화면 반지름은21.32px(하단21.45px)였고 구체 전체가 상단 viewport 안에 있었다. 이 값은 화면 진단이며 Camera 좌표를 발사 규칙에 역주입하지 않는다. v2의 실제 Probe·이미지 검토 PASS는 해당 Mac 실행에 한정하고 남은 빌드·다른 화면·실기기 결과는 별도로 기록한다.

T06의 C1~C5·RAW 라벨 높이는 기존 HUD Canvas 배율을 반영한 `12 × Canvas.scaleFactor` 화면 픽셀로 정한다. 고해상도에서 고정12픽셀 글자가 너무 작아지는 것을 막는 표시 변경이며, T05 기본 라벨이나 입력·피해 규칙을 바꾸지 않는다. 노치·Home Indicator·실물에서의 가독성은 실제 기기 확인 대상이다.

## 제한된 Host–Client 표시

기존 NGO/Transport 직접 IP 세션의 named message로 T06 요청·조회·응답·snapshot을 전달한다. 패킷은 버전과 길이를 가진 제한된 JSON/UTF-8이고 상한은16KB다. Host는 라운드별·참가자별 서로 다른 requestId를 최대256개까지 다룬다. nonce·현재 sender·session/round·snapshot revision·필드 유효성을 검사한다.

Host는 행동 결과와 물리 결과를 즉시 snapshot으로 보내고, 비행 중 위치는 기본20Hz 간격으로 전송한다. 간격은 동일 `ScreenLayoutConfig`의 `attackSnapshotRateHz`를 유효 범위1~60Hz로 읽어 `1 / AttackSnapshotRateHz`초로 계산한다. 별도 설정 asset이나 중복된20Hz 상수는 두지 않는다. 클라이언트는 위치·반지름·HP·결과를 표시하는 Collider 없는 구체만 만든다. 클라이언트 구체는 Rigidbody·피해·회복 권한을 갖지 않는다.

이 범위는 T06 공격 시연에 필요한 표시다. 자동 방 탐색, Ready/Host Start 전체 흐름, 일반 게임 상태·생성·자원·전달·조합 동기화를 포함한 T10-B 완료가 아니다.

## 검증 구분과 G3 조건

AT-09(최초 입력·중복 방지), AT-10(같은 ID의3D 전환), AT-11(실제 피격·한 번 피해), AT-17(Raw 공격 거부)의 **T06 부분 검증**을 기록한다. AT-11의 실제 개인 Stamina 회복은 T07이고, 최종 전체 Acceptance는 T12다.

컴파일, EditMode, PlayMode 실제 물리, 합성 Mouse/Touch, Mac Debug Probe, Mac 직접 조작·렌더, Host–Client, Unity iOS export, Xcode 앱 빌드·서명, 설치·실행, 실제 iPhone 손가락 시험을 각각 구분한다. Probe의 Controller 주입이나 Input System의 큐 Touch가 성공해도 실제 손가락20회 증거가 되지 않는다.

`T06Probe`는 명시적 인자를 준 Development Standalone 앱에서만 실행한다. 단독 Host 모드는 Debug Pointer5회를 실제 물리와 연결하고, network 모드는 Host2회·Client3회를 구분한다. observer 모드는 원격 Host의 확정 상태를 받아20회와 Reset을 관측하며 Pointer·발사·Reset을 주입하지 않는다.20회를 받으면 결과를 캡처하고 사람의 Host END를 기다려, 결과 화면을 읽기도 전에 연결을 끊지 않는다. 이후 연결 종료와 로컬 정리 결과를 기록한다. observer 자체도 실제 손가락이나 의도된 Host END를 증명하지 않으며 기기 로그와 사용자 확인이 별도로 필요하다. 실제 실행 인자·저장 파일·종료 정리의 성공 여부는 각 실행 결과로 판단한다.

HUD의 TOUCH는 Enhanced Touch 경로에서 시작한 **승인된 발사 수**다. 실제 피격 수와는 다르고 합성 Touch도 같은 경로를 사용할 수 있다. G3 증거에는 현재 빌드의 실제 iPhone 실행 맥락, 사용자의 직접 조작 확인, 포인터·발사·실제 hit 로그를 함께 연결한다.

기본 HP100/피해20에서는5회 유효 피격마다 표적이 종료된다. 실제 iPhone 직접 드래그20회는 최소4개 라운드와 중간 RESET3회를 사용하며 각 Reset·누적 hit·로컬 Touch 승인·잔류/중복 여부를 기록한다. 실기기 증거가 없으면 G3는 NOT_RUN 또는 구체적 차단 사유가 있는 BLOCKED로 남긴다. G2의 iPhone17+iPad 대체 승인은 후속 전체 두-iPhone QA 완료를 의미하지 않는다.

T06 이후 T07은 별도 사용자 요청과 G3 충족 확인이 필요하다. 이번 구현에서 생성·조합·Stamina·약점·속도별 위력·다음 Task를 추가하지 않는다.

## 보존과 적용 차이 기록

기존 T05 씬·씬 meta·이전 검증 증거는 보존한다. 공통 Pointer 입력은 `IOrbPointerSink`로 대상 Controller를 받도록 확장하고 T05 Controller도 같은 인터페이스에 연결한다. OrbView는 T06 C1~C5·RAW 식별용 라벨을 지원한다. 정확한 기존 파일·Config·설정 변경과 원본/검증 사본 차이는 최종 source manifest 및 적용 diff로 기록한다. 이 문서 초안은 보존 해시나 실행 수를 미리 단정하지 않는다.

Unity·URP·패키지를 교체하지 않는다. Mac 앱 식별자는 `com.wolfuraark.c6prototype.t06.desktop`으로 분리해 기존 시험 앱과 구분하고, iOS는 기존 `com.wolfuraark.c6prototype`, 버전0.1.0/빌드8을 유지한다. 개인 Team 선택은 본인 Apple 개발 Team / `YOUR_TEAM_ID`이며 실제 서명·기기 결과는 별도 증거가 필요하다.
