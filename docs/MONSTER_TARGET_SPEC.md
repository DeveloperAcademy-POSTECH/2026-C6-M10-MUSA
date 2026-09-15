> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

> **2026-09-14 P2 적용:** 표적 규격과 Prefab은 유지하고, 실제 HUD를 제외한 카메라 구도·바닥을 새 ThrowBattle에 연결했다. 투척·표적·바닥 설정은 schema2 Host 합의에 포함한다. 아래 P1 시점 설명과 [현재 P2 구현](P2_IMPLEMENTATION.md)·[실행 기록](P2_VALIDATION.md)을 구분한다.

# P0 기준 몬스터 규격 · P1 씬 연결

2026-09-14. 사용자가 다음 단계의 기본안을 수용하고 **몬스터 피격 규격을 먼저 정한 뒤 투척을 구현**하도록 승인했다. 이번 기준 몬스터는 이후 투척의 방향·세기를 맞출 고정 표적이며, 적 AI나 몬스터 공격 패턴을 추가하는 작업이 아니다.

이 문서는 규격과 연결 구조를 설명한다. 코드·준비 도구의 존재를 컴파일·물리 시험·앱 빌드·실기기 PASS로 해석하지 않는다. 실제 실행 결과는 이번 P1 검증 기록에서 따로 확인한다.

## 유지할 게임 규칙과 조정 가능한 표적 기준

| 구분 | 내용 | 분류 |
|---|---|---|
| 몬스터 HP·피해 | 기존 Config `monsterMaxHp=100`, `baseDamage=20` | 현재 사용자 채택 게임 규칙 유지 |
| 피해 확정 | Host의 기존 AttackAuthority, 실제 Rigidbody/Collider 충돌 | 기존 권한·피격 규칙 유지 |
| 명중 자원 보상 | 실제 공격자 +5, 최대100 | 현재 사용자 채택 게임 규칙 유지 |
| 표적 ID | `dev-training-dummy` | 기존 표적 식별자 유지 |
| 루트 위치 | 세계 좌표 (0, 0, 0) | DEMO_TUNING_VALUE, 기존 표적 위치를 기준으로 선택 |
| 피격상자 중심 | 루트 기준 (0, 1.4, 0) | DEMO_TUNING_VALUE, 기존 T06/T13 표적과 같은 기준 |
| 피격상자 크기 | (1.2, 2.6, 0.65) Unity unit | DEMO_TUNING_VALUE, 투척 조정을 위한 초기 규격 |
| 피격상자 종류 | 정지한 non-trigger BoxCollider 1개, Rigidbody 없음 | DEMO_ASSUMPTION, 고정 허수아비로 투척부터 비교 |
| Visual | 기존 저장 씬의 허수아비 형태·재질 재사용, Collider 없음 | DEMO_ASSUMPTION, 외형과 판정을 분리 |

규격 원본은 기존 **`Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset` 한 개**다. 필드는 `monsterTargetId`, `monsterPosition`, `monsterHitboxCenter`, `monsterHitboxSize`이며 HP·피해·스태미나와 같은 Config에서 관리한다. 별도 몬스터 체력 에셋이나 Prefab 안의 중복 HP를 만들지 않는다.

기본 루트에서 BoxCollider 범위는 X -0.6~0.6, Y 0.1~2.7, Z -0.325~0.325다. 이 상자가 유효 피격 범위다. 팔 등 시각 메시가 상자 밖으로 돌출될 수 있으므로 **시각 메시의 모든 부분이 맞는다는 뜻은 아니다**. 외형을 수정해도 자동으로 피격상자가 메시 형태를 따라 바뀌지는 않는다. 필요하면 위 Config 규격을 함께 조정하고 다시 확인한다.

## 카메라·발사 거리 기준

P1은 저장된 기존 Battle Camera 위치 `(0, 3.1, -9)`, 바라보는 기준점 `(0, 2.2, 0)`, 세로 FOV42°를 유지한다. 상단 viewport는 전체 높이55%다. 기존 발사 기준은 `(0, 1.08, -4.5)`이고 표적 중심까지 Z방향 간격은4.5unit이다. 이 값은 새 투척의 세기나 최소 명중 속도를 확정했다는 뜻이 아니다.

Mac 390×844와560×746에서 새 표적 표시를 확인했다. 560×746에서는 기존 TEAM HP 문구가 표적 머리 부분과 겹치는 구도가 관찰됐다. P2에서는 이 기준을 출발점으로 HUD를 제외한 목표 표시 범위와 카메라 구도를 먼저 조정한 뒤 던지기 감도를 맞춘다. 현재 캡처를 모든 화면 비율의 최종 QA 통과로 확대하지 않는다.

## Prefab 구조

```text
BenchmarkMonster
├─ MonsterHitTarget          표적 식별자
├─ BenchmarkMonster          Config와 자식 참조 적용, HP 없음
├─ Visual                    기존 허수아비 메시·재질, Collider 없음
└─ Hitbox                    정적 BoxCollider 1개
```

Prefab은 `Assets/_Project/HapioMVP/Prefabs/BenchmarkMonster.prefab`에 생성한다. `BenchmarkMonster`의 실행 순서는 runtime Config 사본 준비 뒤, 전투 연결 이전이다. 새 씬에서는 `SplitScreenLayout.Config`의 실행용 사본에 연결하며, 표적 ID·위치·피격상자는 그 사본으로 적용한다. 네트워크가 소유하는 HP·피해나 승패를 이 컴포넌트에서 별도 계산하지 않는다.

이번 P1의 새 몬스터·2D 물리 설정은 같은 빌드와 같은 에셋을 사용하는 로컬 기준이다. 새 필드 모두를 Host 설정 패킷으로 전송하도록 기존 2인 프로토콜을 확장한 것은 아니다. 실행 중 한 기기의 Inspector 값을 바꾸면 자동으로 다른 기기까지 같은 값이 된다고 가정하지 않는다. P3 다인 통합에서는 새 필드의 합의·검증·설정 지문 범위를 함께 다룬다.

## 반복 가능한 새 씬 준비

Editor 메뉴 **C6 → Next Phase → P1 → Prepare Physics Battle** 또는 `C6.Editor.PhysicsBattleBuild.Prepare`를 실행한다.

1. 저장된 `InterruptionBattle.unity`를 읽고 원본을 보존한다.
2. 기존 시각 더미를 새 몬스터 Prefab의 Visual로 복사한다.
3. 별도 `PhysicsBattle.unity`에서만 이전 시각 더미와 독립 피격상자를 제거하고 Prefab 인스턴스 하나로 교체한다.
4. 기존 두 카메라·로비·게임·Controller의 참조를 새 씬 대상으로 연결하고 P1 구슬 물리를 명시적으로 켠다.
5. 현재 두 참가자 흐름과 기존 구슬·자원·조합·실제 피격 처리를 유지하며 빌드 식별자는21로 구분한다.

이미 저장된 Prefab·씬은 반복 Prepare에서 다시 덮어쓰지 않고 구조를 검사한다. 사용 중인 경로나 고아 `.meta`가 있으면 새 자산으로 덮어쓰지 않는다. 열려 있는 수정 중 씬 대신 저장된 원본의 Preview를 읽는다. 대화형 Editor에서는 새 씬을 Additive로 생성·저장한 뒤 작업 전 씬으로 돌아오며, 검증 사본의 batchmode에서는 기본 미저장 씬 때문에 Additive 생성이 거부되므로 새 Single 씬을 사용한다. 사용자의 원본 Editor에 중복 batchmode를 실행하지 않는다. 생성된 Prefab과 씬은 기존 `.meta`와 함께 보존한다.

`C6.Editor.PhysicsBattleBuild.ValidateSavedScene`은 저장 참조와 단일 표적 구조를 검사한다. `BuildMac`은 `C6_P1_OUTPUT_ROOT` 아래 `macOS/C6Physics.app`에 출력한다. 출력 경로가 이미 있으면 이전 결과를 지우지 않고 중단한다.

## 이번 구현과 후속 투척의 경계

P1은 하단 2D 관성·마찰·충돌·반발과 기준 몬스터를 준비한다. 기존 Combined의 상단 경계 통과 발사·고정 방향 비행은 그대로다. 손떼기에서 방향·세기·중력을 계산하는 새 3D 투척은 P2에서 다루며, 이 문서와 Prefab 준비만으로 구현·검증됐다고 기록하지 않는다.

후속 P2에서는 이 표적을 기준으로 발사 시작점, 유효한 손떼기 방향·속도 범위, 중력·최대 속도·수명, 화면 밖 미명중, 실제 충돌 한 번 처리, 공격자 보상 귀속을 함께 확인한다. 지금은 표적 이동·공격·체력 UI 중복·피격 애니메이션·AI를 추가하지 않았다.
