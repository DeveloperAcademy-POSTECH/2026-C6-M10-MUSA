# C6 Physics Lab 개발자 모드 조정값 명세

> 기본값 기준: 2026-10-10, Unity 6000.6.5f1, PR #76 병합 커밋 `854cdd9c`의 설정 에셋. 아래 기본값은 C# 필드 초기값이 아니라 직렬화된 `Assets/Lab/Resources/LabConfig.asset`의 값이다. 구슬 물리 설명은 현재 3D 조합판 전환 코드에 맞췄으며, 이 전환의 실행 검증 결과를 뜻하지 않는다. 이후 값이 바뀌면 문서를 함께 갱신해야 한다.

이 문서는 **새 실험실(C6_Physics_Lab)**의 개발자 패널에 실제로 연결된 숫자 39개를 설명한다. 이전 C6 프로토타입이나 향후 기획값을 정의하지 않는다. 각 값의 입력 허용 범위는 `LabConfig.DeveloperFields`, 게임에서 읽는 유효값은 `LabConfig` 속성, 사용 지점은 아래 코드 근거를 기준으로 했다.

## 1. 패널의 적용 방식

1. `Assets/Lab/Scenes/Lab.unity`를 Unity Editor에서 실행하거나 **Development Build**를 실행한다. 일반 배포 빌드에서는 DEV 버튼이 숨겨진다.
2. 세 명의 일반 방에 접속하지 않은 상태에서 **DEV**를 연다. 일반 3인 방 안에서는 패널을 열거나 값을 적용할 수 없다.
3. 숫자를 바꾸고 **APPLY + START SOLO**를 누른다. 이미 솔로 전투 중이면 **APPLY + RESTART SOLO**가 표시된다. 이 버튼은 값을 적용하고 **새 솔로 라운드를 시작**하므로 HP·시간·스태미나·구슬이 초기화된다. 진행 중인 라운드에 즉시 반영하는 버튼이 아니다.
4. **DEFAULTS**는 저장된 `LabConfig.asset`의 숫자를 입력칸에 채울 뿐이다. 실제 적용에는 다시 **APPLY**가 필요하다. 적용하지 않고 패널을 닫았다가 열면 입력칸은 마지막 *적용값*으로 다시 채워진다.
5. **END SOLO**를 누르면 일반 세션용 원본 설정으로 돌아간다. 다만 솔로에서 마지막으로 적용한 숫자는 앱이 실행 중인 동안 개발자용 복제본에 남아 다음 DEV 진입 때 표시된다. 앱을 종료하면 사라지며 `LabConfig.asset`에는 저장되지 않는다.

패널을 열어도 Host 전투 시간은 멈추지 않는다. 화면의 마지막 `SHOT HIT/MISS` 보고도 솔로를 끝내거나 연결을 끊으면 지워지므로 먼저 기록한다. 일반 3인 플레이의 값은 각 실행본에 포함된 설정 에셋에서 오며, DEV 솔로 조정값이 다른 참가자에게 전파되지 않는다.

입력은 **39개 전체를 한 번에** 검사한다. 숫자가 아니거나 무한대/NaN, 범위 밖, 정수 전용 필드의 소수 입력이 있으면 아무 값도 적용하지 않는다. 아래 표의 넓은 허용 범위는 “게임플레이에 적합함”을 뜻하지 않는다. 극단적인 카메라·속도·수명 값은 화면 밖 이동이나 빗나감을 일으킬 수 있다.

## 2. 단위와 공통 판정

- **월드 단위(m)**: 이 프로젝트의 Unity 월드 거리. `m/s`와 `m/s²`는 그 거리 기준 속도·가속도를 설명하기 위한 표기다.
- **보드 폭**: 3D 구슬이 XY 평면에서 움직이는 조합판의 가로 너비를 1로 본 단위. 손가락 입력 속도, 조합판 구슬의 방출·감속·정지 값은 보드 폭을 기준으로 정규화된다. 따라서 이 값과 비행 투사체의 월드 단위/초를 직접 비교하면 안 된다.
- **높일 때의 효과**는 다른 수치와 손동작이 같다는 조건에서의 경향이다. 충돌, 속도 상한, 발사각 제한 때문에 항상 같은 크기로 나타나지는 않는다.
- 표의 **범위**는 DEV 입력칸의 범위다. Inspector의 `[Min]`·`[Range]`와 런타임 속성의 방어적 보정은 별도이며, 패널 적용 시에는 표의 범위와 값 간 관계를 먼저 검사한다.

## 3. 전투 결과

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Monster Max HP | 1000 | 정수 1–1,000,000 / HP | 새 라운드의 요괴 최대·초기 체력. 높이면 같은 피해량에서 더 많은 명중이 필요하다. 진행 중인 HP를 직접 고치는 값은 아니다. |
| Damage per Hit | 20 | 정수 1–1,000,000 / HP | Host가 3D 투사체의 실제 요괴 피격을 확정할 때마다 차감할 체력. 빗나감에는 적용되지 않는다. HP는 0 아래로 내려가지 않는다. |
| Battle Duration (seconds) | 180 | 1–86,400 / 초 | Host가 라운드 시작 시 계산하는 제한 시간. 시간이 끝나면 패배한다. 패널을 열어도 시간은 흐른다. |

근거: `LabModel.BeginWithSeats`, `ConfirmHit`, `TryAdvance`.

## 4. 스태미나·구슬 생성

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Maximum Stamina | 100 | 1–1,000,000 / 스태미나 | 개인 스태미나 상한. 시작량·생성 비용·회복량·명중 보상은 이 값 이하여야 한다. |
| Starting Stamina | 100 | 0–1,000,000 / 스태미나 | 새 라운드에서 각 플레이어가 갖는 양. 높이면 처음 만들 수 있는 구슬 수가 늘 수 있다. |
| Orb Generation Cost | 20 | 0.001–1,000,000 / 스태미나/개 | `GENERATE` 요청이 Host에서 승인될 때 Raw 한 개당 소비. 현재 보유량이 비용 미만이면 생성이 거절된다. |
| Stamina Recovery Amount | 20 | 0–1,000,000 / 스태미나 | 아래 간격과 함께 자동 회복 **속도**를 정한다. 0이면 시간에 따른 회복이 없다. |
| Stamina Recovery Interval (seconds) | 3 | 0.001–86,400 / 초 | 회복 속도 = `Recovery Amount ÷ Recovery Interval`. 현재 값은 **초당 약 6.67씩 연속 회복**한다. “3초마다 한 번에 20”으로 처리하지 않는다. |
| Stamina Reward per Hit | 5 | 0–1,000,000 / 스태미나 | Host가 실제 명중을 확정했을 때 그 투사체의 공격자에게만 즉시 더한다. 최대 스태미나를 넘지 않는다. 빗나감은 보상이 없다. |
| Stored Orb Limit | 20 | 정수 1–100 / 구슬 개수 | 각 플레이어가 보관하는 비행 전 구슬의 수량 상한. 생성자뿐 아니라 전달받는 사람의 남은 자리도 검사한다. |

예시: 기본값에서 구슬 한 개를 만들면 100→80이 된다. 다른 행동이 없다면 3초 동안 `20/3 × 3 = 20`이 연속으로 회복돼 다시 100이 된다. 실제 회복은 Host 시간 경과를 처리할 때 갱신된다. Raw는 Host가 Yin 또는 Yang으로 정하며 이 패널에는 음양 확률 입력칸이 없다.

근거: `LabConfig.StaminaRecoveryPerSecond`, `LabModel.TryGenerate`, `TryTransfer`, `ConfirmHit`, `TryAdvance`.

## 5. 구슬 수명·3D 조합판·생성 연출

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Orb Lifetime (seconds) | 8 | 0.1–86,400 / 초 | 보관 중인 Raw·Combined의 수명. Raw는 생성 시점, Combined는 조합 시점부터 센다. 이웃에게 전달해도 수명은 초기화되지 않는다. 이미 발사돼 비행 중인 구슬은 이 만료 대상이 아니다. |
| Orb Bounce | 0.8 | 0–1 / 3D 접촉 탄성 | 구슬·상하 벽의 `PhysicsMaterial.bounciness`. 높이면 접촉 후 더 잘 튀는 경향이 있다. 충돌 결과는 상대 물체와 물리 설정에도 영향을 받는다. |
| Orb Floor Deceleration | 2 | 0–100,000 / 보드 폭/초² | 손을 놓은 뒤 XY 평면에서 움직이는 3D 구슬의 속력에서 매 물리 스텝 감산한다. 높이면 더 빨리 멈춘다. Unity의 `Rigidbody.linearDamping`이나 접촉 재질의 마찰을 조정하는 값은 아니다. |
| Orb Stop Speed | 0.065 | 0–100,000 / 보드 폭/초 | 구슬 속력이 이 값 이하이면 0으로 만든다. 높이면 낮은 속도의 구슬이 일찍 멈춘다. `Orb Maximum Release Speed` 이하여야 한다. |
| Orb Maximum Release Speed | 3 | 0.001–100,000 / 보드 폭/초 | 잡은 조합판 구슬을 놓을 때 부여할 최대 평면 이동 속력. 높이면 더 세게 밀 수 있다. Host가 받는 이동 샘플의 안전 상한도 이 값의 16배로 계산된다. 3D 투척 속도 상한과 별개다. |
| Orb Radius | 0.32 | 0.001–100 / 월드 단위 | 조합판 구슬 루트의 3D `SphereCollider` 반경과 기본 구체 표시 반경. 잡기·직접 겹침 조합 거리, 판 가장자리 여백에도 쓰인다. 크기를 키우면 아래의 판 높이 조건을 다시 맞춰야 한다. |
| Orb Board Height | 5 | 0.1–1,000 / 월드 단위 | 구슬 전용 직교 카메라의 세로 표시 높이. 가로 길이는 화면 비율에 따라 달라지고 판의 물리 경계도 함께 다시 계산된다. |
| Spawn Rise Distance | 0.65 | 0–1,000 / 월드 단위 | 새 Raw의 **Visual 자식**이 본체 위치 아래에서 시작하는 거리. 3D 물리 루트·구슬 ID·Host 위치를 이동시키지 않는다. 0이면 상승 연출이 생략된다. |
| Spawn Rise Duration (seconds) | 0.25 | 0–86,400 / 초 | Visual이 본체 위치까지 올라오는 시간. 연출 중 입력·물리 이동은 잠시 잠긴다. 0이면 연출이 즉시 끝난다. |

판 높이와 반경은 별도 기하 검사를 통과해야 한다. `BoardHeight/2 > OrbRadius + 0.05`와 `(BoardHeight/2) × 구슬 카메라 aspect > OrbRadius + 0.05`를 모두 만족해야 **APPLY**가 진행된다. 판의 화면 비율이 바뀌면 같은 값도 통과 여부가 달라질 수 있다.

근거: `LabModel.TryAdvance`·`TryCombine`, `LabOrbBoard.Configure`·`EndDragAtWorld`·`FixedUpdate`·`BuildHorizontalWalls`, `LabOrbView.PlaySpawnRise`, `LabSceneController.ReconfigureBoard`.

## 6. 전투 카메라·3D 발사점

카메라와 발사점은 모두 요괴의 `target.transform.position`을 기준으로 각 플레이어 자리 방향에 놓인다. **카메라만** 바꾸면 충돌 물리는 바뀌지 않지만 보이는 궤적은 달라질 수 있다.

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Camera Radius | 8 | 0.001–1,000 / 월드 단위 | 요괴 중심에서 플레이어 전투 카메라까지의 수평 거리. 높이면 시점이 뒤로 물러난다. |
| Camera Height Offset | 0.65 | −1,000–1,000 / 월드 단위 | 요괴 중심에 대한 카메라 높이 차이. 카메라는 계속 요괴 중심을 바라본다. |
| Throw Origin Radius | 4 | 0.001–1,000 / 월드 단위 | 요괴 중심에서 공의 기본 발사점까지의 수평 거리. 높이면 날아가야 하는 거리가 늘어난다. |
| Throw Origin Height Offset | −0.5 | −1,000–1,000 / 월드 단위 | 요괴 중심에 대한 공의 초기 높이. 궤적과 바닥·요괴 접촉 위치에 영향을 준다. |
| Throw Origin Lateral Range | 1 | 0–1,000 / 월드 단위 | 손을 놓은 조합판의 가로 위치를 0–1로 환산해 3D 발사점을 좌우 `±Range`만큼 옮긴다. 0이면 중앙에서만 출발한다. 측면에서 놓아도 자동으로 요괴를 향해 조준하지 않는다. |

근거: `LabSeatCamera.LateUpdate`, `LabThrowMath.SeatPosition`·`LaunchPosition`.

## 7. 스와이프 판정·3D 투척

입력은 손을 놓기 직전 **0.12초**의 조합판 XY 위치 샘플에서 계산한다. 구슬 자체는 3D 물리 루트를 갖지만 이 단계의 손동작은 평면 좌표다. 손을 놓은 지점이 좌우 전달 경계이고 바깥 방향으로 움직이면 이웃 **전달 판정이 먼저**이며, 투척은 그다음이다. Combined 구슬만 투척할 수 있다. 아래 표의 `vₓ`·`vᵧ`는 각각 보드 폭/초 단위의 가로·위쪽 입력 속도다.

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Throw Minimum Swipe Speed | 0.8 | 0.001–100,000 / 보드 폭/초 | `vᵧ ≥ 이 값`이어야 투척한다. 높이면 약한 위쪽 동작은 판 위의 굴림으로 남는다. |
| Throw Minimum Swipe Distance | 0.12 | 0.001–1,000 / 보드 폭 | 최근 0.12초 샘플 구간의 이동 거리 하한. 전체 드래그 시작점부터의 거리가 아니다. 높이면 짧은 튕김이 투척으로 인식되기 어려워진다. |
| Throw Upward Dominance | 0.65 | 0–1,000 / 비율 | `vᵧ ≥ abs(vₓ) × 이 값`을 요구한다. 높이면 대각선·가로 동작이 투척보다 굴림으로 남기 쉽다. |
| Throw Maximum Swipe Speed | 6 | 0.001–100,000 / 보드 폭/초 | 측정된 손 속도 벡터를 먼저 이 크기로 **잘라서** 판정·발사에 사용한다. 더 빠른 스와이프가 곧바로 거절되는 값은 아니다. |
| Throw Minimum Upward Speed | 2 | 0.001–100,000 / 월드 단위/초 | 손동작으로 계산한 **발사각 조정 전** 상승 성분의 하한. 약한 입력에 주로 영향을 준다. Loft 보정·최종 속도 상한을 거친 뒤의 실제 Y 속도 하한을 보장하지는 않는다. |
| Throw Lateral Gain | 2.5 | 0.001–100,000 / 속도 변환 배율 | `vₓ`를 공의 좌우 초기 속도로 바꾼다. 높이면 같은 비스듬한 손동작에서 측면 이동과 빗나감이 커질 수 있다. |
| Throw Upward Gain | 2.1 | 0.001–100,000 / 속도 변환 배율 | `vᵧ`를 위쪽 초기 속도로 바꾼다. 높이면 대체로 더 높이 올라간다. 최소 상승 속도나 최종 속도 상한에 걸리면 차이가 줄어든다. |
| Throw Forward Gain | 4.5 | 0.001–100,000 / 속도 변환 배율 | `vᵧ`를 요괴 쪽 수평 초기 속도로 바꾼다. 높이면 더 빠르게 전진해 표적에 일찍 도달하며 포물선의 하강 부분이 덜 보일 수 있다. |
| Throw Maximum World Speed | 18 | 0.001–100,000 / 월드 단위/초 | 좌우·위·전진을 합친 3D **초기 속력**의 상한. 여기에 자주 걸리면 Gain이나 Power Exponent를 높여도 결과 차이가 작아진다. |
| Throw Gravity | 9.81 | 0.001–100,000 / 월드 단위/초² | 비행 중 공에만 아래 방향 가속을 매 물리 스텝 적용한다. 높이면 빨리 낙하한다. Unity 전역 `Physics.gravity`를 바꾸는 값은 아니다. |
| Throw Lifetime (seconds) | 4 | 0.001–86,400 / 초 | 명중이나 바닥 접촉이 없더라도 투사체를 MISS로 끝내는 최대 비행 시간. 바닥·요괴에 먼저 닿으면 즉시 결과가 난다. |
| Throw Radius | 0.165 | 0.001–100 / 3D 월드 단위 | 3D `SphereCollider`와 기본 구체 표시 반경. 높이면 화면 크기와 피격 판정 크기가 함께 바뀐다. |
| Throw Power Exponent | 1 | 0.25–3 / 지수 | 손 속력 반응 곡선. 기준 입력 속도 **1.5 보드 폭/초**에서 출력이 같고, 1이면 선형이다. 1보다 크면 기준보다 빠른 동작이 더 강해지고 느린 동작이 더 약해진다. 1보다 작으면 반대다. |
| Throw Loft Offset (degrees) | 0 | −30–30 / 도 | 계산된 상승·전진 발사각에 더한다. 양수는 같은 종방향 속력을 더 위로, 덜 앞으로 배분한다. 최종 발사각은 3°–87°로 제한된다. |
| Throw Air Damping | 0 | 0–5 / Rigidbody 선속도 감쇠값 | 비행 중 `linearDamping`. 높이면 전체 선속도가 줄어 사거리와 높이에 영향을 준다. 공을 좌우로 휘게 만드는 힘은 아니다. |

현재 발사 계산은 개념적으로 다음 순서다.

1. 손 속력 `s`에 대해 `shaped = 1.5 × (s / 1.5) ^ PowerExponent`를 구해 가로·세로 입력에 같은 비율로 적용한다.
2. `up = max(MinimumUpwardSpeed, shapedY × UpwardGain)`, `forward = shapedY × ForwardGain`, `lateral = shapedX × LateralGain`을 구한다.
3. `atan2(up, forward) + LoftOffset`을 3°–87°로 제한해 위·전진 성분을 다시 나누고, 전체 벡터 크기를 `MaximumWorldSpeed`로 제한한다.
4. 투사체 Rigidbody는 이 초기 속도로 출발한다. 이후 매 물리 스텝 자체 중력과 `linearDamping`을 받고, Host의 Collider 충돌 또는 수명 종료로 HIT/MISS가 결정된다. Host만 HP와 명중 보상을 확정하며 다른 기기의 투사체는 표시용이다.

**포물선 비교 팁:** `Forward Gain`만 올리면 목표에 너무 빨리 닿아 공이 거의 직선처럼 보일 수 있다. `Upward Gain`·`Forward Gain`·`Gravity`를 한 번에 모두 바꾸기보다 기준 조건을 기록한 뒤 하나씩 비교한다. `Power Exponent`는 세기 곡선, `Loft Offset`은 발사각, `Air Damping`은 비행 중 감속이므로 서로 다른 질문에 사용한다.

근거: `LabOrbBoard.ReleaseVelocity`·`EndDragAtWorld`, `LabThrowMath.IsThrowGesture`·`LaunchVelocity`, `LabProjectile.Prepare`·`FixedUpdate`·`Resolve`, `LabNetwork.SpawnHostProjectile`·`OnProjectileResolved`.

## 8. 입력 제약과 조정 실험 예시

**값 간 관계:** `Starting Stamina`, `Orb Generation Cost`, `Stamina Recovery Amount`, `Stamina Reward per Hit`은 모두 `Maximum Stamina` 이하여야 한다. `Orb Stop Speed ≤ Orb Maximum Release Speed`, `Throw Minimum Swipe Speed ≤ Throw Maximum Swipe Speed`, `Throw Minimum Upward Speed ≤ Throw Maximum World Speed`도 필수다. 관계 위반 시 전체 입력이 거절된다.

**기준 실험:** DEFAULTS를 불러와 APPLY로 솔로를 시작한다. 비교 시간을 확보하려면 전투 시간·구슬 수명을 늘리고 생성 비용을 낮추되, 변경값을 함께 기록한다. Yin과 Yang을 직접 겹쳐 Combined를 만든 후 판 중앙에서 위로 던진다. `SHOT HIT/MISS | 비행 시간 | peak +최고 상승 | range 수평 이동`와 HP 변화를 적는다. 같은 손동작을 여러 번 반복하고 **한 번에 한 값**만 바꿔 APPLY + RESTART SOLO로 비교한다. Console/Player 로그에는 `C6_LAB_THROW`(입력·발사 속도)와 `C6_LAB_PROJECTILE`(충돌·비행 지표)가 남는다.

**예시 질문별 첫 조정 대상**

| 관찰·목표 | 먼저 비교할 값 | 함께 확인할 것 |
| --- | --- | --- |
| 공이 너무 곧장 가서 하강이 안 보임 | Upward Gain ↑, Forward Gain ↓를 각각 시험 | peak·비행 시간·실제 HIT; 카메라가 정면이면 깊이감이 약할 수 있음 |
| 약한 손동작도 투척으로 처리됨 | Minimum Swipe Speed 또는 Minimum Swipe Distance ↑ | 조합판 평면 이동과 투척의 경계, 최근 0.12초 입력 |
| 강하게 던져도 거리 차이가 작음 | Maximum World Speed 상한 여부, Power Exponent | `C6_LAB_THROW`의 실제 launch 속도 |
| 구슬이 금방 사라져 조합하기 어려움 | Orb Lifetime ↑ | Raw/Combined의 각각 생성 시각, 전달 후 수명 |
| 구슬이 너무 오래 굴러다님 | Orb Floor Deceleration ↑ 또는 Orb Stop Speed ↑ | 충돌·전달 속도까지 영향을 받는지 |
| 측면에서 던진 공이 잘 빗나감 | Origin Lateral Range 또는 Lateral Gain 비교 | 자동 조준 기능은 현재 없음; 판정 반경 변경은 난이도도 바꿈 |

이 표의 화살표는 **시험 방향**이지 검증된 추천값이 아니다. 잘못된 투척을 자동 보정하거나 요괴를 추적하는 기능은 현재 없다.

## 9. 이 패널로 조정할 수 없는 것·검증 범위

정확히 3명인 일반 방의 인원 조건, 좌석의 입장 순서, 네트워크 주소·포트, 음양 생성 알고리즘, 조합·전달의 승인 규칙, 손동작 샘플 창 **0.12초**, 타깃 Cylinder의 실제 크기·위치, 전투 카메라의 FOV와 화면 내 viewport는 이 39개 입력칸에 없다. 필요하면 별도 코드·Scene 변경이 필요하다. DEV 솔로에는 이웃이 없어 화면 간 전달을 시험할 수 없다.

기존 PR #76 기준 문서는 해당 코드·Scene·설정 에셋과 당시 `docs/VALIDATION.md`의 증거를 **읽어 확인**해 작성됐다. 그 기록의 EditMode **12/12**, PlayMode **20/20** 및 Mac Development 앱 빌드는 이번 3D 조합판 전환의 통과 증거가 아니다. 이 전환의 Unity 컴파일·EditMode·PlayMode, Mac·iOS 빌드, 실제 조작은 새 실행 기록으로 구분해야 한다. 문서의 물리 용어를 갱신한 것만으로 실행 결과를 PASS로 보지 않는다.

## 10. 코드·Scene 연결 지도

- 설정 필드·범위·일괄 검사: `Assets/Lab/Scripts/Core/LabConfig.cs` — `DeveloperFields`, `CaptureDeveloperValues`, `TryApplyDeveloperValues`.
- 저장 기본값: `Assets/Lab/Resources/LabConfig.asset`.
- 저장된 DEV 버튼·39개 입력칸 연결: `Assets/Lab/Scenes/Lab.unity`의 `LabDeveloperMode` 컴포넌트.
- 패널 열기·DEFAULTS·APPLY·SOLO 종료: `Assets/Lab/Scripts/UI/LabDeveloperMode.cs`.
- Host 전투 수치·스태미나·생성·구슬 만료: `Assets/Lab/Scripts/Core/LabModel.cs`.
- 3D 조합판 구슬 물리·XY 손동작·공통 구체 외형: `Assets/Lab/Scripts/Orb/LabOrbBoard.cs`와 `LabOrbView.cs`.
- 발사 위치·초기 속도: `Assets/Lab/Scripts/Throw/LabThrowMath.cs`.
- 공의 중력·저항·충돌·수명: `Assets/Lab/Scripts/Throw/LabProjectile.cs`.
- Host의 발사·피격 확정·결과 로그: `Assets/Lab/Scripts/Session/LabNetwork.cs`.
- 카메라·보드 크기 적용: `Assets/Lab/Scripts/UI/LabSeatCamera.cs`와 `LabSceneController.cs`.
- 과거 검증 기록: `docs/VALIDATION.md`. 투척 비교 절차: `docs/THROW_TUNING_GUIDE.md`.
