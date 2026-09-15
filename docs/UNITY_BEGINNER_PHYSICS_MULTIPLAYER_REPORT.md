> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# C6 / 합이오 Unity 개발 보고서 2

> **상세 코드 후속편:** [보고서 3 — Unity 코드 읽기·유지보수 실습 안내서](UNITY_BEGINNER_CODE_MAINTENANCE_GUIDE.md)에서 이 보고서의 기능을 실제 함수와 코드로 따라가고, Inspector·Console·중단점·테스트로 확인하는 예시를 볼 수 있습니다.

**2D 물리·3D 투척·최대 5인 연결을 이해하고, 화면을 넘나드는 구슬을 직접 수정하기 위한 기록**

| 항목 | 기준 |
|---|---|
| 작성일 | 2026-09-14 |
| 대상 | Unity를 처음 배우며 C6 프로토타입을 직접 수정하려는 개발자 |
| 다루는 범위 | 기존 빌드20 이후 P0~P4, 빌드21~24 |
| 현재 게임 구현 | 빌드24, 소스 커밋 `bbbde33` |
| 현재 씬 | `Assets/_Project/HapioMVP/Scenes/ContinuousTransferBattle.unity` |
| 환경 | Unity 6000.5.7f1, URP 17.5.0, NGO 2.13.1, Transport 6.5.0, Input System 1.20.0 |
| 선행 문서 | [초보자 개발 보고서 1 — 프로젝트 준비부터 빌드20까지](UNITY_BEGINNER_DEVELOPMENT_REPORT.md) |
| 실행 방법 | [현재 빌드24 실행 안내](P4_RUNBOOK.md), [Unity·Xcode 상세 빌드 README](../BUILD_README.md) |

## 1. 이 작업의 목적과 보고서의 범위

이 프로토타입의 목표는 **기본 기획 사양을 실제로 동작하게 만들고, 필요한 기능을 커스텀하면서 직접 개발할 수 있는 기반을 확보하는 것**이다. 첫 보고서는 빈 프로젝트에서 생성·조합·전투·두 사람 연결을 만든 과정을 다뤘다. 이 후속 보고서는 그 위에 구슬의 물리 움직임, 손떼기 투척, 최대 5인 협동과 화면 사이의 연속 이동을 추가한 과정을 설명한다.

사용자는 동작의 의도와 우선순위를 정하고 실제 iPhone·iPad 조작 결과를 확인했다. Codex는 코드·씬 준비 도구·자동 검사·빌드·로그 대조를 수행했다. 따라서 사용자가 모든 C# 코드나 오브젝트 배치를 직접 작성했다는 기록이 아니라, **앞으로 직접 개발하기 위해 협업 결과를 이해하는 문서**다.

이 문서를 작성하며 새 게임 기능이나 물리 수치를 변경하지 않았다. 기존 구현과 실행 증거를 설명하며, 아래의 연습은 앞으로 해볼 제안이다. 원래 기획의 모든 항목이나 미실행 검증을 완료한 것으로 바꾸지 않는다.

## 2. 어떤 기획이 실제 동작으로 바뀌었는가

처음 남은 개발 항목은 2D 구슬 물리, 최대 5인 연결, 3D 공격 물리의 세 가지였다. 구현 과정에서 사용자가 물리와 입력의 의미를 구체화했다.

| 요구 | 이번에 구체화한 의미 | 현재 빌드24의 동작 |
|---|---|---|
| 구슬이 굴러다님 | 평면에서 밀리고 충돌한 뒤 마찰로 멈춤 | 손을 놓은 뒤 관성 이동. 상하 경계와 다른 구슬에는 반발 |
| 구슬을 합침 | 직접 드래그했을 때만 활성화 | Yin을 Yang에 직접 겹쳐 정상적으로 놓으면 Combined. 자연 충돌·같은 음양 접촉은 조합하지 않음 |
| 포켓몬고처럼 던짐 | 손을 놓는 순간의 방향·세기로 투척 | Combined를 상단으로 올려 준비하고, 위로 움직이며 놓으면 중력 비행 |
| 몬스터 사전 정의 | 던지기를 비교할 피격 대상부터 정함 | 고정 허수아비 Prefab, 외형과 실제 피격상자 분리 |
| 3명 이상 연결 | 입장 순서대로 최대 5명의 원형 이웃 구성 | 전원 Ready·초기 상태 확인 후 시작. 좌우 수신자가 각자의 이웃으로 결정됨 |
| 좌우 끝 도달 | 벽에서 튕기는 것이 아니라 다른 화면으로 넘어감 | 놓은 구슬이 끝에 닿으면 자동 전달. 남은 속도로 다음 화면을 계속 지나며 마찰로 정지 |

‘포켓몬고처럼’이라는 표현에서 채택한 것은 손떼기 방향·세기 기반의 던지기다. 위치 서비스, AR 카메라, 포획 판정까지 추가한 것은 아니다.

기존 자원·전투 규칙은 유지했다. 시작 구슬 0개, 스태미나 100/100, Raw 1개 생성 비용 20, 시간 회복 20/3초, 유효 명중 시 공격자 +5이며 상한은 100이다. 회복은 시간에 비례해 연속 계산한다. 몬스터 HP 100, 피해 20, 한 판 180초이며 일반 음양은 Host Seed로 정해진다. 다섯 명이 됐다고 몬스터 HP나 피해를 자동으로 늘리지 않았다.

## 3. 실제 개발 순서와 그 이유

| 단계 | 씬·빌드 | 한 작업에서 해결한 문제 | 다음 단계에 도움이 된 점 |
|---|---|---|---|
| P0/P1 | `PhysicsBattle` ·21 | 기준 몬스터 Prefab, 하단 Rigidbody2D·관성·충돌·감속 | 고정된 표적과 움직이는 구슬을 확보해 다음 투척을 비교할 수 있게 됨 |
| P2 | `ThrowBattle` ·22 | 상단에서 손떼기 방향·세기 기반 3D 투척, 바닥·카메라 구도 | 맞음·빗나감·바닥 충돌을 실제 물리로 나누어 확인 |
| P3 | `FivePlayerBattle` ·23 | 2~5인 로비·원형 이웃·전원 준비·개인 자원·공유 전투 | 여러 사람이 같은 규칙으로 전달·공격하는 기반 확보 |
| P4 | `ContinuousTransferBattle` ·24 | 좌우 자동 통과와 속도·높이·통신 대기 중 감속 | 한 번 민 구슬이 여러 화면을 지나고 정지하는 사용자 의도 반영 |

몬스터를 먼저 정한 것은 비교 기준을 고정하기 위해서다. 표적의 위치·크기·카메라·던지기 감도가 동시에 계속 바뀌면 “던지는 힘이 약한지, 표적이 작은지, 화면 구도가 문제인지” 구분하기 어렵다. 이번에는 표적 규격을 정한 뒤 입력과 비행을 조정했다. 움직이는 몬스터 AI나 공격 패턴을 먼저 구현하지 않아도 투척을 시험할 수 있었다.

P1~P3 당시 좌우는 반발하는 벽이고 수신 구슬은 정지했다. 이후 실제 플레이에서 원래 의도를 확인하고 P4에서 자동 통과로 변경했다. 이전 결과는 당시 사양의 기록으로 보존하며, 이전의 ‘벽 반발 PASS’를 현재의 ‘화면 통과 PASS’로 바꾸지 않았다.

근거: [표적 규격](MONSTER_TARGET_SPEC.md), [P1 구현](P1_IMPLEMENTATION.md), [P2 구현](P2_IMPLEMENTATION.md), [P3 구현](P3_IMPLEMENTATION.md), [P4 변경 결정](P4_IMPLEMENTATION.md).

## 4. Unity에서 처음 살펴볼 장면과 오브젝트

1. Unity Hub에서 `Assets`·`Packages`·`ProjectSettings`가 함께 있는 조직 저장소 루트 폴더를 6000.5.7f1로 연다. 이미 열려 있다면 그 Editor 창을 사용한다.
2. Project 창에서 `Assets/_Project/HapioMVP/Scenes/ContinuousTransferBattle.unity`를 연다.
3. Hierarchy의 `P4ContinuousTransferBattle`을 선택하고 Inspector의 화면 분할·컨트롤러·로비·게임 연결을 살펴본다.
4. `Prefabs/BenchmarkMonster.prefab`을 열어 Visual과 Hitbox를 비교한다. 설정 원본은 `Config/ScreenLayoutConfig.asset`이다.
5. Play 전과 실행 중 Hierarchy를 비교한다. 버튼·글자·게이지와 구슬의 상당 부분은 실행 중 코드가 만든다. 시작 구슬이 없고 GENERATE 후 생성되는 것이 정상이다.

최신 전투는 최소 2명의 실행본이 필요하다. 혼자 이전 `BattleLoop`의 DEV SOLO로 기본 조작을 살펴볼 수 있지만, 그 장면은 최신 연속 전달을 시험하는 장면이 아니다. 현재 앱을 빌드할 때는 **C6 → Next Phase → P4** 메뉴를 사용한다. 세부 준비·Xcode 서명·설치는 [현재 실행 안내](P4_RUNBOOK.md)를 따른다.

원본 보고서에는 실제 Mac 개발 앱의 자동 검사 화면을 첨부했으며, 공개 이관본에는 그 이미지와 원시 증거를 포함하지 않았다. 그 실행은 명시적인 시험용 Raw·Combined를 사용했으므로 iPhone 터치나 일반 생성만으로 진행한 플레이 결과와 구분한다. [공개 검증 요약](VALIDATION_SUMMARY.md)에서 범위를 확인할 수 있다. 실제 배치는 Unity의 Game 창에서 위쪽 3D 전투와 아래쪽 2D 공간을 확인하면 된다.

첫 보고서의 빌드20에는 몬스터 Prefab이 없었지만, 현재는 [BenchmarkMonster.prefab](../Assets/_Project/HapioMVP/Prefabs/BenchmarkMonster.prefab)이 있다. 이전 보고서의 “현재 씬”, “Prefab 없음”, “상단 진입 즉시 발사”는 빌드20 시점의 설명이다.

## 5. 두 종류의 물리와 게임 결과를 나눈 구조

| 구분 | 사용하는 대상 | 담당하는 일 | 최종 게임 결과를 결정하는가 |
|---|---|---|---|
| 하단 2D 움직임 | Rigidbody2D·CircleCollider2D | 소유한 구슬의 관성·반발·감속·손으로 잡기 | 이동 표시를 계산함. 소유권·조합·피해를 혼자 결정하지 않음 |
| 상단 3D 공격 | Host의 Rigidbody·Collider | 투척 비행과 표적·바닥의 실제 충돌 | 유효 충돌을 Host 공격 규칙에 전달 |
| Host 규칙 | 구슬 등록부·자원·전투·로비 담당 코드 | 누가 구슬을 소유하는지, 비용·조합·피해·승패 | 승인된 공통 결과를 확정 |
| Client 표시 | 받은 구슬·공통 상태·발사체 표시 | 자신의 2D 구슬을 움직이고 Host 결과를 화면에 표시 | 별도로 몬스터 HP를 깎지 않음 |

하단의 `Rigidbody2D`를 상단으로 옮긴다고 같은 물리 본체가 3D 공이 되는 구조가 아니다. 발사 승인을 받은 뒤 2D 표시를 제거하고, **같은 논리적 OrbID를 가진 3D 발사체**를 만든다. 논리 ID와 화면의 GameObject는 구분된다. 전달·발사는 ID를 유지하고 조합은 재료 2개를 종료해 새 ID 1개를 만든다.

모든 구슬의 화면 위치를 Host가 매 프레임 계산해 배포하는 구조도 아니다. 하단 물리는 현재 소유자가 계산하고, 소유권 변경과 게임 규칙은 Host가 승인한다. 이 구분을 알아야 화면 이동 코드를 고칠 때 비용·HP를 중복 계산하지 않을 수 있다.

## 6. 기능별로 무엇을 구현했는가

### 6.1 몬스터: 보이는 모양과 맞는 범위 분리

[BenchmarkMonster](../Assets/_Project/HapioMVP/Attack/BenchmarkMonster.cs)는 Config의 위치·표적 ID·피격상자를 적용하는 컴포넌트다. 별도 체력이나 피해를 저장하지 않는다.

Prefab의 Visual은 허수아비 외형이고 Collider가 없다. Hitbox는 정지한 BoxCollider다. 기본 중심은 `(0, 1.4, 0)`, 크기는 `(1.2, 2.6, 0.65)`이며 루트 위치는 `(0, 0, 0)`이다. 팔 끝처럼 외형이 피격상자 밖으로 나올 수 있다. 모양을 크게 바꿨다고 맞는 범위까지 자동으로 바뀌지는 않는다.

다른 몬스터 모양으로 교체하려면 먼저 Visual을 바꾸고, 실제 맞아야 할 범위는 Config의 Hitbox 값으로 함께 검토한다. HP와 피해는 기존 Host 규칙에 남긴다. 표적 이동·적 AI·새 공격 패턴은 이번 구현에 포함하지 않았다.

### 6.2 2D 구슬: 접촉 마찰과 바닥 감속 구분

[LocalOrbPhysicsBoard](../Assets/_Project/HapioMVP/Orbs/LocalOrbPhysicsBoard.cs)는 구슬의 물리 본체와 경계, 최근 드래그 위치·시간을 관리한다. 하단 구슬은 화면 아래로 떨어지지 않도록 2D 중력을 사용하지 않으며, 회전은 고정한다.

자유롭게 움직일 때는 물리 계산을 받는다. 잡을 때는 관성을 멈추고 손을 따라오게 하며, 다른 구슬과 겹쳐 조합할 수 있도록 접촉 상태를 바꾼다. 손을 정상적으로 놓으면 최근 움직임에서 속도를 구해 다시 자유 이동한다. 잡고 있는 모습은 기존 확대·빛 효과로 강조한다.

벽·구슬 접촉의 마찰과, 아무것에도 닿지 않고 이동할 때의 바닥 감속은 다른 설정이다. 접촉 마찰만 조절하면 접촉이 없는 이동 구간의 정지 거리를 의도대로 정하기 어렵기 때문에 별도 감속을 적용했다. 조합 요청은 물리 충돌 이벤트가 아니라 **직접 드래그의 정상 종료**에서 보낸다.

### 6.3 3D 투척: 준비·손떼기·승인·충돌

1. Combined를 잡아 상단으로 올리면 투척 준비 상태가 된다. 아직 구슬을 소비하지 않는다.
2. [ThrowGestureSampler](../Assets/_Project/HapioMVP/Orbs/ThrowGestureSampler.cs)가 최근 손 위치와 시간을 모은다.
3. 움직이면서 놓으면 [ThrowMapping](../Assets/_Project/HapioMVP/Attack/ThrowMapping.cs)이 입력을 3D 초기 속도로 바꾼다. 화면에서 시작점만 보고 표적을 향해 자동 조준하는 방식이 아니다.
4. 입력한 기기에서도 투척 가능 여부를 미리 확인하지만, 실제 발사에 사용할 초기 속도는 Host가 공유 설정으로 다시 계산해 확정한다. Host가 소유자·구슬 종류·입력·중복 요청 등을 확인해 승인하면 2D 표시가 사라지고 3D 비행이 시작된다.
5. [HostProjectile3D](../Assets/_Project/HapioMVP/Attack/HostProjectile3D.cs)의 실제 충돌을 [AttackAuthority](../Assets/_Project/HapioMVP/Attack/AttackAuthority.cs)가 판정한다. 유효 명중은 피해 20·공격자 회복 5를 한 번 적용한다.

Raw, 취소 입력, 움직임이 너무 약한 손떼기는 공격을 만들지 않는다. 바닥 접촉은 몬스터 명중이 아니며, 빗나간 공은 현재 4초 수명 뒤 정리된다. Client의 발사체 표시는 Host 비행을 보여 주는 역할이다.

### 6.4 최대 5인: 인원 숫자 외에 바꿔야 했던 것

[ParticipantRing](../Assets/_Project/HapioMVP/Networking/ParticipantRing.cs)은 시작할 때 확정한 참가자 순서에서 좌우 이웃을 찾는다.

| 3인 예시 | 왼쪽 이웃 | 오른쪽 이웃 |
|---|---|---|
| P1 | P3 | P2 |
| P2 | P1 | P3 |
| P3 | P2 | P1 |

인원이 늘면 로비 명단, 전원 Ready, 모든 Client의 초기 상태 확인, 각자의 자원, 전달 상대, 통신 자료 크기를 함께 다뤄야 한다. 한 사람이 계속 응답한다고 다른 사람의 연결까지 정상으로 취급하지 않도록 참가자별 확인 상태를 관리했다.

두 사람일 때 좌우 상대가 같아 보이는 것은 정상이다. 세 사람부터 방향에 따라 상대가 달라진다. 로비에서 누군가 나가면 번호와 Ready를 정리하지만, 이미 시작한 전투의 참가자 명단은 고정한다. 전투 도중 새 참가, 자동 재접속, Host 이전은 추가하지 않았다.

### 6.5 연속 전달: 경계에서 멈추지 않고 다음 화면으로 이어가기

1. 놓은 구슬이 자유롭게 움직이다 좌우 경계에 도달한다.
2. 물리 보드가 ID·방향·높이·남은 속도를 알리고 컨트롤러가 전달 요청을 만든다. 잡은 상태에서는 이 자동 전달이 발생하지 않는다.
3. Host 승인을 기다리는 동안 보내는 화면 끝에 표시한다. 승인 전부터 상대 화면에 같은 구슬을 복제하지 않는다.
4. Host가 이웃·소유권·수신 20개 한도·속도·순서를 확인해 같은 ID의 소유자를 바꾼다.
5. 받는 쪽은 반대쪽 경계에 구슬을 놓고 승인된 남은 속도로 재개한다. 다시 경계에 닿으면 추가 손 입력 없이 같은 과정이 이어진다.

[OrbTransferMotion](../Assets/_Project/HapioMVP/Orbs/OrbTransferMotion.cs)은 속도와 전달 시각을 함께 다룬다. 두 축의 속도는 모두 **하단 구슬 영역 전체 너비/초**를 기준으로 한다. 예를 들어 가로 속도 1은 감속이 없을 때 1초 동안 그 영역 너비만큼 움직이는 속도다. 높이는 구슬 중심이 움직일 수 있는 세로 범위의 0~1 비율로 전달한다. 기기 픽셀 수나 가로·세로 비율이 달라도 원의 모양과 상대 높이를 유지하려는 기준이다.

통신을 기다린 시간에도 마찰로 속도가 줄어든다. 다만 대기 중 위치를 예측해 먼저 움직이는 구현은 아니다. 네트워크가 느릴 때도 완전히 끊김 없는 이동을 보장한다고 표현하면 안 된다. 수신 공간이 가득 차 거부되면 보내는 쪽 끝에서 멈춘다. 자동으로 계속 재요청하지 않으며, 공간을 확보한 뒤 다시 밀 수 있다.

받은 상태를 다시 읽을 때마다 속도를 초기값으로 되돌리면 계속 가속하거나 같은 전달을 중복 처리할 수 있다. 그래서 새로운 전달 회차에서만 속도를 적용하고, 같은 회차의 반복 상태는 현재 움직임을 유지한다. 한 바퀴 돌아 원래 사용자에게 되돌아오는 경우도 새 소유권 회차로 처리한다.

## 7. 직접 수정할 때 찾아갈 파일

아래는 전체 코드를 읽기 전에 목적에 따라 들어갈 시작점이다. `T09`, `T10`이라는 옛 이름의 코드도 최신 씬에서 재사용한다.

| 바꾸려는 것 | 먼저 읽을 파일·자산 |
|---|---|
| 마찰·반발·밀기 최대 속도 | [ScreenLayoutConfig.asset](../Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset), [설정 정의](../Assets/_Project/HapioMVP/Presentation/ScreenLayoutConfig.cs) |
| 구슬 관성·충돌·경계 감지 | [LocalOrbPhysicsBoard.cs](../Assets/_Project/HapioMVP/Orbs/LocalOrbPhysicsBoard.cs) |
| 손가락 입력·정상 손떼기·취소 | [OrbPointerInput.cs](../Assets/_Project/HapioMVP/Orbs/OrbPointerInput.cs), [OrbGestureEngine.cs](../Assets/_Project/HapioMVP/Orbs/OrbGestureEngine.cs) |
| 확대·빛·구슬 라벨 | [OrbView.cs](../Assets/_Project/HapioMVP/Orbs/OrbView.cs) |
| 입력과 생성·조합·공격·전달 연결 | [T09BattleController.cs](../Assets/_Project/HapioMVP/Battle/T09BattleController.cs) |
| 몬스터 외형·피격상자 | [BenchmarkMonster.prefab](../Assets/_Project/HapioMVP/Prefabs/BenchmarkMonster.prefab), [BenchmarkMonster.cs](../Assets/_Project/HapioMVP/Attack/BenchmarkMonster.cs) |
| 던지기 입력을 속도로 변환 | [ThrowGestureSampler.cs](../Assets/_Project/HapioMVP/Orbs/ThrowGestureSampler.cs), [ThrowMapping.cs](../Assets/_Project/HapioMVP/Attack/ThrowMapping.cs) |
| 상단 카메라·바닥·피격 | [ThrowBattleFraming.cs](../Assets/_Project/HapioMVP/Battle/ThrowBattleFraming.cs), [ThrowBattleFloor.cs](../Assets/_Project/HapioMVP/Battle/ThrowBattleFloor.cs), [AttackAuthority.cs](../Assets/_Project/HapioMVP/Attack/AttackAuthority.cs) |
| 방·Ready·좌우 이웃 | [LobbyAuthority.cs](../Assets/_Project/HapioMVP/Lobby/LobbyAuthority.cs), [ParticipantRing.cs](../Assets/_Project/HapioMVP/Networking/ParticipantRing.cs) |
| 전달 속도·동일 ID·소유권 | [OrbTransferMotion.cs](../Assets/_Project/HapioMVP/Orbs/OrbTransferMotion.cs), [HostOrbRegistry.cs](../Assets/_Project/HapioMVP/Orbs/HostOrbRegistry.cs), [AttackSession.cs](../Assets/_Project/HapioMVP/Attack/AttackSession.cs) |
| 여러 기기의 승인된 상태·중복 검사 | [T10GameSession.cs](../Assets/_Project/HapioMVP/GameSync/T10GameSession.cs), [GameWire.cs](../Assets/_Project/HapioMVP/GameSync/GameWire.cs) |
| 게임 UI 문구·배치 | [T09Hud.cs](../Assets/_Project/HapioMVP/Battle/T09Hud.cs), [T10LobbyHud.cs](../Assets/_Project/HapioMVP/Lobby/T10LobbyHud.cs) |
| 최신 씬 준비·빌드 메뉴 | [ContinuousTransferBuild.cs](../Assets/_Project/HapioMVP/Editor/ContinuousTransferBuild.cs) |

## 8. Inspector에서 조절할 값과 작은 연습

Play를 종료한 뒤 Project 창의 `ScreenLayoutConfig.asset`을 선택한다. 아래 값은 현재 저장된 조작감 기준이다. 런타임 GameObject의 Rigidbody 값을 잠깐 바꾸는 것과 원본 Config를 저장하는 것은 다르다.

| 설정 필드 | 현재 값 | 바꾸면 달라지는 점 |
|---|---:|---|
| `orbFloorDeceleration` | 0.6 너비/초² | 높이면 더 빨리 감속하고 이동 거리가 짧아짐 |
| `orbStopSpeed` | 0.015 너비/초 | 이 이하의 작은 움직임을 정지로 처리 |
| `orbMaxReleaseSpeed` | 2 너비/초 | 정상 손떼기의 최대 초기 속도 |
| `orbRestitution` | 0.65 | 상하 벽·구슬 접촉의 반발 정도. 좌우 통과 자체에는 반발을 적용하지 않음 |
| `orbContactFriction` | 0.15 | 접촉면 마찰. 위의 바닥 감속과 구분 |
| `orbReleaseSampleWindow` | 0.12초 | 하단에서 놓기 직전 속도를 계산할 구간 |
| `throwMinUpSpeed` | 0.35 화면 너비/초 | 3D 투척에 필요한 최소 위쪽 움직임 |
| `throwForwardGain`, `throwUpGain`, `throwLateralGain` | 8, 2.8, 6 | 화면 너비/초 입력을 월드 단위/초 속도로 바꾸는 계수 |
| `throwGravity` | 9.81 월드 단위/초² | 3D 비행의 중력 크기 |
| `throwMaxWorldSpeed` | 30 월드 단위/초 | 3D 초기 속도 상한 |
| `throwBounce`, `throwLifetime` | 0.45, 4초 | 3D 반발과 비행 수명 |
| `monsterHitboxCenter`, `monsterHitboxSize` | (0,1.4,0), (1.2,2.6,0.65) | 실제 맞는 상자의 중심(몬스터 루트 기준)과 크기. 월드 단위 |

최신 손떼기 투척은 `throw...` 설정을 사용한다. 예전 고정 발사의 `projectileSpeed=12`, `projectileLifetime=3`만 보고 현재 투척 속도·수명을 판단하면 안 된다.

설정 원본은 하나지만 **모든 설정을 Host가 자동 배포하는 것은 아니다.** 현재 투척·표적·바닥 설정은 방의 설정 합의에 포함되어 있다. 위의 여섯 2D 물리 필드는 각 앱에 저장된 같은 Config를 사용하는 전제다. 한 기기의 실행 중 Inspector 변경이 나머지 기기에 자동 적용된다고 가정하지 않는다. 조작감을 바꿨다면 모든 참가자가 같은 새 빌드를 사용하고 새 방에서 비교한다.

처음에는 한 번에 하나씩 바꾼다. 예를 들어 `orbFloorDeceleration`만 0.6에서 0.8로 바꾸고 같은 시작 위치·비슷한 밀기로 이동 거리와 정지를 비교할 수 있다. 이 값은 **연습 제안이며 실제로 적용하지 않았다.** 주변 구슬 충돌·입력 속도·통신 지연에 따라 통과 횟수는 달라지므로, “항상 정확히 네 번 전달”을 새 규칙으로 만들지 않는다.

작은 변경의 순서는 다음과 같다.

1. 변경 전 Git 상태와 Config 값을 기록한다.
2. Play를 멈추고 한 항목을 바꿔 저장한다.
3. Editor 또는 Mac 앱에서 일반 이동·직접 조합·좌우 통과를 확인한다.
4. 같은 변경으로 참여 기기들을 다시 빌드·설치한다.
5. 바꾼 값, 빌드, 기기, 실제 관찰을 남긴다. 비교가 끝나면 유지할 값을 결정한다.

## 9. 무엇을 확인했고, 무엇은 아직 확인하지 않았는가

| 시점 | 실행으로 확인된 대표 결과 | 범위 |
|---|---|---|
| 빌드21 | 자동 1,063개, Mac 두 앱 상태29개 일치 | 당시 2D 물리·고정 표적. iOS·실제 Touch 미실행 |
| 빌드22 | 자동 1,192개, 마지막 안내 수정 관련 6개, 최종 Mac 113개 상태 일치 | 실제 Host 명중·빗나감·공격자 +5, iOS 빌드·서명. 해당 빌드 Touch 미실행 |
| 빌드23 | 자동 1,311개, 최종 Mac 2~5인 공통 상태654개 일치 | 최대 인원·원형 이웃·보관 100+비행 100. 후속 iPhone+iPad+Mac 3인 기본 조작 확인 |
| 빌드24 | 자동 1,427개, Mac 2~5인 공통 상태 1,449개 일치·자동 32전달 | iOS 설치·실행과 iPhone+iPad Raw 자동 8전달, 사용자 정지·화면 확인 |

단계별 자동 검사 수에는 이전 검사가 포함된다. 네 행의 숫자를 전부 더해 서로 다른 검사를 그만큼 실행했다고 쓰지 않는다. 최종 1,427개는 EditMode 1,286개와 PlayMode 141개이며, 전체 검사 뒤의 작은 안내·진단 수정은 관련 검사나 최종 앱 실행으로 별도 확인했다.

Mac 시험은 실제 독립 앱과 네트워크, Host 물리를 사용했다. 다만 한 Mac에서 직접 IP로 연결하고 합성 포인터·명시적 시험용 구슬을 사용하는 부분이 있어, 휴대기기 5대의 Touch나 Wi-Fi 성능 확인과는 다르다. 공통 상태 해시가 같다는 것은 같은 승인 상태를 관찰했다는 증거이며, 두 화면의 모든 픽셀이 동일해야 한다는 뜻이 아니다.

빌드23 실기기 후속은 iPhone 17 Host+iPad mini6+Mac의 3인 구성이다. iPad의 두 실제 투척 중 첫 발은 만료되고 두 번째만 맞아 HP 100→80이 됐다. 명중 당시 스태미나는 100이어서 실제 회복량은 0이었다. 상한 처리는 확인했지만 실기기에서 +5가 증가한 결과로 쓰지 않는다.

빌드24 실기기에서는 Raw 1개를 iPhone에서 두 번 밀었다. 오른쪽 4회·왼쪽 4회 자동 전달 로그와 “여러 화면을 자동으로 오가다 멈춤, 화면도 정상”이라는 사용자 확인을 남겼다. 정지는 사용자 관찰이며 기기의 최종 속도 0을 별도 계측한 것은 아니다.

현재 빌드24에서 실기기 Combined 연속 전달, 직접 조합·3D Touch 재검사, 실제 3~5대 기기의 연속 이동, 전체 실기기 상태 해시 비교, 무선 지연·손실 주입, 실기기 수신 20개 한도 시험은 **NOT_RUN**이다. 이전 빌드의 성공을 현재 빌드의 재검사 성공으로 승계하지 않는다.

자세한 증거: [P1 검증](P1_VALIDATION.md), [P2 검증](P2_VALIDATION.md), [P3 검증](P3_VALIDATION.md), [빌드23 실기기](P3_DEVICE_VALIDATION.md), [P4 검증](P4_VALIDATION.md), [원본 비공개 자료: 자동 결과 — 공개 요약](VALIDATION_SUMMARY.md), [원본 비공개 자료: Mac 대조 — 공개 요약](VALIDATION_SUMMARY.md), [원본 비공개 자료: 실기기 대조 — 공개 요약](VALIDATION_SUMMARY.md).

## 10. 문제를 고치면서 배운 점

| 실제 문제 | 원인·수정 | 직접 개발할 때의 교훈 |
|---|---|---|
| 이동 중 구슬을 다시 잡기 어려움 | 물리 위치와 화면에 보이는 위치의 차이를 고려해 표시된 원을 기준으로 선택 | 수치가 맞는 것과 손으로 잘 잡히는 것을 함께 확인 |
| 참가자가 늘며 정상 Ready가 거부됨 | 다른 사람의 Ready로 전체 갱신 번호가 바뀌는 상황과 실제 명단 변경을 구분 | 인원 확장은 목록 길이뿐 아니라 동시에 도착하는 요청 순서도 다룸 |
| 전달 거절 후 구슬이 옛 위치로 돌아감 | 물리 모드를 바꿀 때 Rigidbody와 화면 위치를 경계에 함께 맞춤 | 물리 본체와 표시를 둘 다 관리하는 코드는 전환 시점을 확인 |
| 자동 시험의 Retry가 실패함 | 전투 중에는 원래 Retry가 금지됨. 실제 명중·Victory 후 Retry로 시험 순서를 수정 | 검사 도구의 잘못 때문에 정상 게임 규칙을 느슨하게 바꾸지 않음 |

실패 기록은 삭제하지 않고 최초 실패와 수정 후 결과를 함께 보존했다. 문서 승인, 코드 작성, 컴파일, 자동 검사, 앱 빌드·서명, 실제 기기 설치·터치는 각각 다른 완료 단계다.

## 11. 이번 작업에서 사용한 개발 방식

기존 씬을 덮어쓰기보다 새 씬에서 기능을 켰다. 새 씬·Prefab 연결은 Editor 준비 도구로 반복할 수 있게 하고, 기존 에셋의 `.meta`를 함께 보존했다. UI 일부가 실행 중 생성되므로 문구·배치를 영구 변경하려면 생성하는 HUD 코드를 수정한다.

원래 프로젝트가 Editor에 열려 있을 때는 별도 Git 작업 폴더에서 빌드·검사했다. 검증 후 변경 파일만 원본에 반영했다. 패키지나 렌더러를 임의 교체하지 않았고, Unity가 만든 런타임 직렬화 변화와 실제 게임 변경을 구분했다.

Unity 공식 저장소의 `unity-cli` 스킬은 프로젝트 실행·검사·빌드 절차에 활용했다. 스킬은 작업 지침이고 CLI는 명령을 실행하는 도구다. 구슬 물리를 자동으로 완성하는 기능은 아니다. 설치된 CLI 1.0.0-beta.8의 실제 도움말을 확인했고, 기존 프로젝트에 Pipeline 패키지를 임의 추가하지 않았다. 사용 절차와 고정한 스킬 출처는 [P4 실행 안내](P4_RUNBOOK.md)·[적용 결정](P4_IMPLEMENTATION.md)에 있다.

원본 비공개 저장소에는 P0~P4의 개발 이력과 검토된 증거를 보존한다. 공개 조직 저장소에는 검토한 게임 코드·씬·Prefab·메타·설정·개발 문서와 [검증 요약](VALIDATION_SUMMARY.md)을 이관하고, 전체 Git 이력·원시 증거는 복사하지 않는다. 실행 앱·Xcode 출력·캐시·원시 로그·서명 자료도 포함하지 않는다. 이번 이관이나 문서 업로드를 추가 기능 구현·새 기기 검증으로 계산하지 않는다.
