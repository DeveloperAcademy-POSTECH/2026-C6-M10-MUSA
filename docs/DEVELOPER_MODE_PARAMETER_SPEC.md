# C6 Physics Lab 개발자 모드 조정값 명세

> 기본 수치 기준: Unity 6000.6.5f1의 현재 `Assets/Lab/Resources/LabConfig.asset`과 `LabConfig` 런타임 보정값. 원래 39개 수치는 PR #76의 설정 에셋을 기준으로 기록했으며, 이후 생성 확률·아이템 9개와 개인 HP 1개를 더했고, 아이템 효과·결합·카메라·요괴 회전·생성 연출·소리·햅틱·생성 북 값을 차례로 더해 현재 83개다. 64~74번째(인덱스 63~73) 소리 음량·햅틱 11개(#142)는 이 문서에 아직 따로 설명하지 않았다. 80~83번째(인덱스 79~82)는 #167의 폭탄 충격파·폭발 진동 값이다. 직렬화된 값이 없으면 C# 필드 초기값이 쓰인다. 실행 검증 결과는 `docs/VALIDATION.md`에서 변경별로 확인한다.

이 문서는 **새 실험실(C6_Physics_Lab)**의 개발자 패널에 실제로 연결된 숫자 83개를 설명한다. 이전 C6 프로토타입이나 향후 기획값을 정의하지 않는다. 각 값의 입력 허용 범위는 `LabConfig.DeveloperFields`, 게임에서 읽는 유효값은 `LabConfig` 속성, 사용 지점은 아래 코드 근거를 기준으로 했다.

## 1. 패널의 적용 방식

1. `Assets/Lab/Scenes/Lab.unity`를 Unity Editor에서 실행하거나 **Development Build**를 실행한다. 일반 배포 빌드에서는 DEV 버튼이 숨겨진다. 참가자는 일반 빌드여도 Host가 확정한 세션 설정을 수신한다.
2. 접속 전 또는 **전투 시작 전 Host 로비**에서 **DEV**를 연다. 참가자와 전투 진행 중에는 설정 변경을 열지 않는다. 숫자 입력칸 83개와 대응하는 슬라이더가 서로 값을 반영한다. 정수 필드는 슬라이더 값도 정수로 표시하며, 매우 넓은 범위는 슬라이더 조작 구간을 비선형으로 배분한다. 숫자 입력으로 허용 범위의 정확한 값을 지정할 수 있다.
3. 공유 실험을 시작하려면 별도 **ROOM PLAYERS** 슬라이더에서 2~5명(기본 3명)을 정하고 **CREATE ROOM**을 누른다. Host가 검증된 83개 값으로 방을 만든 뒤 참가자에게 전파한다. 기존 **HOST** 버튼을 사용하면 기본 인원과 현재 설정으로 방을 만든다. 방 생성 후 아직 전투 전이라면 **APPLY TO ROOM**으로 다시 조정할 수 있으며 이전 참가자 확인은 무효화된다. 목표 인원이 모이고 각 참가자가 최신 설정을 확인한 뒤에만 Host의 **START**가 활성화된다.
4. 혼자 시험할 때는 **START SOLO**를 누른다. 이미 솔로 전투 중이면 **RESTART SOLO**가 표시된다. 새 라운드가 시작되므로 요괴/개인 HP·시간·스태미나·구슬이 초기화된다. 진행 중인 네트워크 라운드에 값을 즉시 적용하는 동작은 없다.
5. **DEFAULTS**는 저장된 `LabConfig.asset`의 숫자를 입력칸과 슬라이더에 불러올 뿐이다. 공유 방이나 솔로에 적용하려면 해당 시작·적용 버튼을 눌러야 한다. **END SOLO**는 솔로를 끝내고 원본 설정으로 복귀한다. 런타임 복사본은 앱이 실행 중인 동안만 유지되며 원본 에셋에는 저장되지 않는다.

패널을 열어도 이미 진행 중인 Host 전투 시간은 멈추지 않는다. 마지막 `SHOT HIT/MISS` 보고는 솔로를 끝내거나 연결을 끊으면 지워질 수 있으므로 먼저 기록한다. 공유 방에서는 Host의 설정 지문과 확인 응답을 기준으로 시작을 판정한다. 이 절은 코드상 경로 설명이며 실제 기기 동기화 결과는 검증 기록과 구분한다.

입력은 **83개 전체를 한 번에** 검사한다. 숫자가 아니거나 무한대/NaN, 범위 밖, 정수 전용 필드의 소수 입력이 있으면 아무 값도 적용하지 않는다. 아래 표의 넓은 허용 범위는 “게임플레이에 적합함”을 뜻하지 않는다. 극단적인 카메라·속도·수명 값은 화면 밖 이동이나 빗나감을 일으킬 수 있다.

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
| Player Max HP | 300 | 정수 1–300 / 개인 HP | 각 플레이어의 새 라운드 최대·초기 체력. DEV 필드 배열에서는 **49번째(인덱스 48)**로 추가되어 앞선 48개 설정 순서를 보존한다. 한 사람의 개인 HP가 0이어도 전투를 계속하며 전원 0이면 패배한다. 진행 중인 개인 HP를 직접 바꾸는 슬라이더가 아니다. |

개인 HP는 Host의 플레이어별 상태와 스냅샷에 보관되며 화면에는 내 값만 표시한다. 현재 요괴 공격이나 아이템 피해 이벤트는 이 값을 깎지 않는다. Host 모델의 `TryApplyPlayerDamage`는 향후 이벤트용 상태 변경 경로로, 양수 피해·유효한 플레이어·진행 중인 판에서만 적용하고 0에서 멈춘다. 실제 플레이에서 전원 0 패배를 시험하려면 별도 피해 이벤트 구현이 필요하다.

근거: `LabModel.BeginWithSeats`, `TryApplyPlayerDamage`, `ConfirmHit`, `TryAdvance`, `LabHud`.

## 4. 스태미나·구슬 생성

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Maximum Stamina | 100 | 1–1,000,000 / 스태미나 | 개인 스태미나 상한. 시작량·생성 비용·회복량·명중 보상은 이 값 이하여야 한다. |
| Starting Stamina | 100 | 0–1,000,000 / 스태미나 | 새 라운드에서 각 플레이어가 갖는 양. 높이면 처음 만들 수 있는 구슬 수가 늘 수 있다. |
| Orb Generation Cost | 20 | 0.001–1,000,000 / 스태미나/개 | `GENERATE` 요청이 Host에서 승인될 때 구슬(음·양·아이템) 한 개당 소비. 현재 보유량이 비용 미만이면 생성이 거절된다. |
| Stamina Recovery Amount | 20 | 0–1,000,000 / 스태미나 | 아래 간격과 함께 자동 회복 **속도**를 정한다. 0이면 시간에 따른 회복이 없다. |
| Stamina Recovery Interval (seconds) | 1.7 | 0.001–86,400 / 초 | 회복 속도 = `Recovery Amount ÷ Recovery Interval`. 현재 값은 **초당 약 11.76씩 연속 회복**한다. “1.7초마다 한 번에 20”으로 처리하지 않는다. 북 1개가 세 사람을 한 바퀴 도는 약 6~9초 안에 빈 스태미나가 거의 다 차도록(약 8.5초) #146에서 3초 → 1.7초로 줄였다. |
| Stamina Reward per Hit | 10 | 0–1,000,000 / 스태미나 | Host가 실제 명중을 확정했을 때 그 투사체의 공격자에게만 즉시 더한다. 최대 스태미나를 넘지 않으므로 이미 가득 차 있으면 버려진다. 빗나감은 보상이 없다. #146에서 5 → 10(구슬 ½개)으로 올렸다. |
| Stored Orb Limit | 20 | 정수 1–100 / 구슬 개수 | 각 플레이어가 보관하는 비행 전 구슬의 수량 상한. 생성자뿐 아니라 전달받는 사람의 남은 자리도 검사한다. |

예시: 기본값에서 구슬 한 개를 만들면 100→80이 된다. 다른 행동이 없다면 1.7초 동안 `20/1.7 × 1.7 = 20`이 연속으로 회복돼 다시 100이 된다. 0에서 100까지는 약 8.5초가 걸린다. 실제 회복은 Host 시간 경과를 처리할 때 갱신된다. 생성되는 종류는 아래 **8절의 생성 확률**을 따른다.

근거: `LabConfig.StaminaRecoveryPerSecond`, `LabModel.TryGenerate`, `TryTransfer`, `ConfirmHit`, `TryAdvance`.

## 5. 구슬 수명·3D 조합판·생성 연출

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Orb Lifetime (seconds) | 8 | 0.1–86,400 / 초 | 보관 중인 Raw·Combined의 수명(아이템은 8절의 `Item Lifetime`). Raw는 생성 시점, Combined는 조합 시점부터 센다. 이웃에게 전달해도 수명은 초기화되지 않는다. 이미 발사돼 비행 중인 구슬은 이 만료 대상이 아니다. |
| Orb Bounce | 0.8 | 0–1 / 3D 접촉 탄성 | 구슬·상하 벽의 `PhysicsMaterial.bounciness`. 높이면 접촉 후 더 잘 튀는 경향이 있다. 충돌 결과는 상대 물체와 물리 설정에도 영향을 받는다. |
| Orb Floor Deceleration | 2 | 0–100,000 / 보드 폭/초² | 손을 놓은 뒤 XY 평면에서 움직이는 3D 구슬의 속력에서 매 물리 스텝 감산한다. 높이면 더 빨리 멈춘다. Unity의 `Rigidbody.linearDamping`이나 접촉 재질의 마찰을 조정하는 값은 아니다. |
| Orb Stop Speed | 0.065 | 0–100,000 / 보드 폭/초 | 구슬 속력이 이 값 이하이면 0으로 만든다. 높이면 낮은 속도의 구슬이 일찍 멈춘다. `Orb Maximum Release Speed` 이하여야 한다. |
| Orb Maximum Release Speed | 3 | 0.001–100,000 / 보드 폭/초 | 잡은 조합판 구슬을 놓을 때 부여할 최대 평면 이동 속력. 높이면 더 세게 밀 수 있다. Host가 받는 이동 샘플의 안전 상한도 이 값의 16배로 계산된다. 3D 투척 속도 상한과 별개다. |
| Orb Radius | 0.32 | 0.001–100 / 월드 단위 | 조합판 구슬 루트의 3D `SphereCollider` 반경과 기본 구체 표시 반경. 잡기·직접 겹침 조합 거리, 판 가장자리 여백에도 쓰인다. 크기를 키우면 아래의 판 높이 조건을 다시 맞춰야 한다. |
| Orb Board Height | 5 | 0.1–1,000 / 월드 단위 | 구슬 전용 직교 카메라의 세로 표시 높이. 가로 길이는 화면 비율에 따라 달라지고 판의 물리 경계도 함께 다시 계산된다. |
| Spawn Pop Start Size (0-1 of the orb) | 0.3 | 0–1 / 구슬 크기 대비 비율 | 새로 만든 구슬의 **Visual 자식**이 북 한가운데에서 나타날 때의 크기. 3D 물리 루트는 처음부터 북 바로 바깥에서 출발해 날아가고, 그림만 북 안에서 커지며 따라 나온다. 구슬 ID·Host 위치·충돌 크기는 바꾸지 않는다. #137 전에는 `Spawn Rise Distance`였다. |
| Spawn Pop Duration (seconds) | 0.15 | 0–2 / 초 | 그림이 북에서 나와 제 크기가 되는 시간. 이 동안 구슬은 이미 날아가며 다른 구슬·북과는 부딪히지 않고(벽에는 부딪힘), 연타한 구슬끼리 막히지 않는다. 0이면 연출 없이 바로 제 모습이다. #137 전에는 `Spawn Rise Duration`이었다. |
| Combine Speed | 0 | 0–10 / 보드 폭/초 | 닿으면 결합의 기준 속도. 두 구슬이 서로 다가간 속도(`Collision.relativeVelocity`를 보드 폭으로 나눈 값)가 이 값 이상일 때만 결합을 요청한다. 0이면 스쳐도 결합하고, 높이면 세게 부딪힐 때만 결합한다. DEV 필드 배열의 **50번째(인덱스 49)**. |
| Drop Combine (0 off, 1 on) | 1 | 정수 0–1 | 1이면 잡은 음/양 구슬을 반대 음양 구슬 위에 놓아 결합한다. 0이면 겹쳐 놓아도 결합하지 않는다. 단, 닿으면 결합이 켜져 있으면 손을 뗄 때 겹친 충돌로 결합될 수 있다. **51번째(인덱스 50)**. |
| Contact Combine (0 off, 1 on) | 1 | 정수 0–1 | 1이면 음/양 구슬이 서로 닿을 때 자동 결합을 요청한다(연쇄 결합 포함). 잡고 있는 구슬, 경계에서 넘어가는 중인 구슬, 잠긴 구슬, 결합 대기 중인 구슬, 아이템·북은 제외한다. 북에서 튀어나오는 중인 구슬은 다른 구슬과 닿지 않으므로 결합되지 않는다. 판정은 구슬이 있는 기기가 하고 Host의 결합 승인으로 확정한다. **52번째(인덱스 51)**. |

### 생성 퍼짐과 생성 북

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Spawn Fan Angle (deg each side, 180 = all directions) | 180 | 0–180 / 도 | 구슬이 북에서 튀어나가는 방향의 범위. 위쪽 기준 좌우 각각 이 각도까지 Host가 고르게 뽑는다. 180이면 모든 방향, 0이면 바로 위로만 나간다. **61번째(인덱스 60)**. |
| Spawn Minimum Speed (board widths/s) | 1 | 0–10 / 보드 폭/초 | 튀어나가는 속력의 하한. Host가 최소~최대 사이에서 뽑는다. 북이 옆 가장자리에 가까우면 그쪽으로 가는 구슬은 바닥 감속으로 멈출 때 가장자리 0.08 보드 폭 안쪽에 서도록 이보다 느려질 수 있다. 최대 이하여야 한다. **62번째(인덱스 61)**. |
| Spawn Maximum Speed (board widths/s) | 1.8 | 0–10 / 보드 폭/초 | 튀어나가는 속력의 상한. **63번째(인덱스 62)**. |
| Drum Count | 1 | 정수 1–5 / 개 | 한 판에 놓이는 생성 북 수. 판 시작 때 Host가 북마다 받을 사람을 무작위로 뽑는다(북이 인원 이하이면 서로 다른 사람, 많으면 모두 하나씩 받은 뒤 나머지도 무작위). 북은 받은 사람 판의 가운데에서 시작하고, **북이 있는 사람만 생성**할 수 있다. 공유 방 설정에 포함된다. **75번째(인덱스 74)**. |
| Drum Radius | 0.6 | 0.1–1.5 / 월드 단위 | 북의 충돌 구 반지름이자 북 그림 크기. 기본은 예전 하단 생성 버튼의 화면 크기에 맞췄다(구슬 지름의 약 2배). 잡기 판정도 이 크기에 맞춰 넓어진다. 새 라운드부터 적용된다. **76번째(인덱스 75)**. |
| Drum Drift Speed (board widths/s) | 0.1 | 0–1 / 보드 폭/초 | 가만히 둔 북이 저절로 떠도는 최고 속력(실제 속력은 그 50~100% 사이에서 천천히 변함). 0이면 떠돌지 않는다. 밀리거나 튕긴 북은 구슬처럼 감속하다 이 속력까지 줄면 다시 떠돈다. 이웃에게 넘어가려면 바깥쪽 속력이 이 값의 1.5배를 넘어야 하며, 그보다 느리게 가장자리에 닿으면 되돌아온다. **77번째(인덱스 76)**. |
| Drum Drift Turning (per second) | 0.25 | 0–2 / 초⁻¹ | 떠도는 방향이 바뀌는 빠르기(부드러운 노이즈의 진행 속도). 높이면 자주 방향을 틀고, 0이면 같은 방향을 유지한다. 벽·옆 가장자리 0.15 보드 폭 안에서는 방향과 상관없이 멀어지는 쪽으로 튼다. **78번째(인덱스 77)**. |
| Drum Spin Strength | 1 | 0–3 / 배 | 북 그림 회전의 세기. 떠도는 길이 휘는 쪽으로 도는 회전(최대 초당 90° × 세기)과, 구슬·벽이 스치며 접점이 미끄러지는 방향으로 도는 회전(최대 초당 540°)에 모두 곱한다. 회전은 그림만 바꾸고 충돌·위치에는 영향이 없다. 0이면 돌지 않는다. **79번째(인덱스 78)**. |

판 높이와 반경은 별도 기하 검사를 통과해야 한다. #154의 종이판·하단 HUD 배치는 유지하고, `LabOrbAreaProfile`의 종이판 상하 윤곽과 HUD 불투명 상단 윤곽을 합성한 **실제로 보이는 곡선 영역**에서 구슬과 북의 원형 몸체가 전체 가로 구간·양끝 통로를 통과할 수 있어야 APPLY가 진행된다. 기울어진 경계는 실제 반지름과 선분의 거리를 반영한다. 곡선 물리를 위해 그림을 위로 옮기거나 축소하지 않으며 일괄 0.05 여유·수평 HUD 컷을 적용하지 않는다. 후보 `BoardHeight`로 계산만 하며 검증 중 카메라 배율은 바꾸지 않는다. 화면 비율·HUD 배치·윤곽 제어점에 따라 같은 값의 통과 여부가 달라질 수 있다. 근거는 `LabSceneController.ValidateBoardTuning`, `LabOrbAreaProfile.TryBuildShape(plateWorldRect, hudWorldRect, ...)`, `LabOrbAreaShape.CanFitRadius`다.

근거: `LabModel.TryAdvance`·`TryCombine`, `LabOrbBoard.Configure`·`EndDragAtWorld`·`FixedUpdate`·`BuildHorizontalWalls`, `LabOrbView.PlaySpawnRise`, `LabSceneController.ReconfigureBoard`.

## 6. 전투 카메라·3D 발사점

카메라와 발사점은 모두 요괴의 `target.transform.position`을 기준으로 각 플레이어 자리 방향에 놓인다. **카메라만** 바꾸면 충돌 물리는 바뀌지 않지만 보이는 궤적은 달라질 수 있다.

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Camera Radius | 8 | 0.001–1,000 / 월드 단위 | 요괴 중심에서 플레이어 전투 카메라까지의 수평 거리. 높이면 시점이 뒤로 물러난다. |
| Camera Height Offset | 0.65 | −1,000–1,000 / 월드 단위 | 요괴 중심에 대한 카메라 높이 차이. 카메라는 아래 `Camera Look Height Offset`의 지점을 바라본다. |
| Camera Look Height Offset | 2 | −10–10 / 월드 단위 | 카메라가 바라보는 지점의 높이를 요괴(피냐타) 중심에서 위아래로 옮긴다. 값을 올리면 화면에서 피냐타가 아래로, 내리면 위로 보인다. 카메라·발사 지점이 피냐타 중심을 따라가므로 피냐타 Transform을 옮겨도 화면 구도는 거의 같고, 구도는 이 값과 `Camera Height Offset`·`Camera Radius`로 바꾼다. 투척·명중 판정에는 영향이 없다. DEV 필드 57번째(인덱스 56). |
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
| Throw Lateral Gain | 4.5 | 0.001–100,000 / 속도 변환 배율 | `vₓ`를 공의 좌우 초기 속도로 바꾼다. 높이면 같은 비스듬한 손동작에서 측면 이동과 빗나감이 커질 수 있다. |
| Throw Upward Gain | 2.1 | 0.001–100,000 / 속도 변환 배율 | `vᵧ`를 위쪽 초기 속도로 바꾼다. 높이면 대체로 더 높이 올라간다. 최소 상승 속도나 최종 속도 상한에 걸리면 차이가 줄어든다. |
| Throw Forward Gain | 2.5 | 0.001–100,000 / 속도 변환 배율 | `vᵧ`를 요괴 쪽 수평 초기 속도로 바꾼다. 높이면 더 빠르게 전진해 표적에 일찍 도달하며 포물선의 하강 부분이 덜 보일 수 있다. |
| Throw Maximum World Speed | 12.5 | 0.001–100,000 / 월드 단위/초 | 좌우·위·전진을 합친 3D **초기 속력**의 상한. 여기에 자주 걸리면 Gain이나 Power Exponent를 높여도 결과 차이가 작아진다. |
| Throw Gravity | 30 | 0.001–100,000 / 월드 단위/초² | 비행 중 공에만 아래 방향 가속을 매 물리 스텝 적용한다. 높이면 빨리 낙하한다. Unity 전역 `Physics.gravity`를 바꾸는 값은 아니다. |
| Throw Lifetime (seconds) | 4 | 0.001–86,400 / 초 | 명중이나 바닥 접촉이 없더라도 투사체를 MISS로 끝내는 최대 비행 시간. 바닥·요괴에 먼저 닿으면 즉시 결과가 난다. |
| Throw Radius | 0.165 | 0.001–100 / 3D 월드 단위 | 3D `SphereCollider`와 기본 구체 표시 반경. 높이면 화면 크기와 피격 판정 크기가 함께 바뀐다. |
| Throw Power Exponent | 1.2 | 0.25–3 / 지수 | 손 속력 반응 곡선. 기준 입력 속도 **1.5 보드 폭/초**에서 출력이 같고, 1이면 선형이다. 1보다 크면 기준보다 빠른 동작이 더 강해지고 느린 동작이 더 약해진다. 1보다 작으면 반대다. |
| Throw Loft Offset (degrees) | 0 | −30–30 / 도 | 계산된 상승·전진 발사각에 더한다. 양수는 같은 종방향 속력을 더 위로, 덜 앞으로 배분한다. 최종 발사각은 3°–87°로 제한된다. |
| Throw Air Damping | 0 | 0–5 / Rigidbody 선속도 감쇠값 | 비행 중 `linearDamping`. 높이면 전체 선속도가 줄어 사거리와 높이에 영향을 준다. 공을 좌우로 휘게 만드는 힘은 아니다. |

현재 발사 계산은 개념적으로 다음 순서다.

1. 손 속력 `s`에 대해 `shaped = 1.5 × (s / 1.5) ^ PowerExponent`를 구해 가로·세로 입력에 같은 비율로 적용한다.
2. `up = max(MinimumUpwardSpeed, shapedY × UpwardGain)`, `forward = shapedY × ForwardGain`, `lateral = shapedX × LateralGain`을 구한다.
3. `atan2(up, forward) + LoftOffset`을 3°–87°로 제한해 위·전진 성분을 다시 나누고, 전체 벡터 크기를 `MaximumWorldSpeed`로 제한한다.
4. 투사체 Rigidbody는 이 초기 속도로 출발한다. 이후 매 물리 스텝 자체 중력과 `linearDamping`을 받고, Host의 Collider 충돌 또는 수명 종료로 HIT/MISS가 결정된다. Host만 HP와 명중 보상을 확정하며 다른 기기의 투사체는 표시용이다.

**포물선 비교 팁:** `Forward Gain`만 올리면 목표에 너무 빨리 닿아 공이 거의 직선처럼 보일 수 있다. `Upward Gain`·`Forward Gain`·`Gravity`를 한 번에 모두 바꾸기보다 기준 조건을 기록한 뒤 하나씩 비교한다. `Power Exponent`는 세기 곡선, `Loft Offset`은 발사각, `Air Damping`은 비행 중 감속이므로 서로 다른 질문에 사용한다.

근거: `LabOrbBoard.ReleaseVelocity`·`EndDragAtWorld`, `LabThrowMath.IsThrowGesture`·`LaunchVelocity`, `LabProjectile.Prepare`·`FixedUpdate`·`Resolve`, `LabNetwork.SpawnHostProjectile`·`OnProjectileResolved`.

### 요괴 피격 회전

세 항목은 기존 57개 설정 뒤에 추가됐다. 원통의 실제 MeshCollider에 유효한 투사체가 맞으면 Host는 **피격점과 몸체 중심 사이의 거리 중 현재 회전축에 수직인 성분 × 투사체 진행 방향**으로 몸체의 Y축 회전 충격량을 계산한다. 중심에 가까운 명중은 회전이 작고, 좌우 빗맞힘은 반대 방향으로 돈다. 각도는 한 바퀴에서 잘리지 않으며 속도만 제한·감쇠한다. 기존 X/Z 흔들림·상하 바운스와 피해 판정은 유지한다.

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Target Spin Strength | 80 | 0–200 / 회전 충격량 배율 | 같은 빗맞힘에서 처음 추가되는 Y축 각속도를 조정한다. 0이면 회전 충격이 없다. 58번째(인덱스 57). |
| Target Spin Damping | 1.2 | 0–10 / 초⁻¹ | 매 물리 스텝 각속도를 지수적으로 줄인다. 높이면 빨리 멈추고, 0이면 자동 감쇠하지 않는다. 59번째(인덱스 58). |
| Target Spin Maximum Speed (deg/s) | 720 | 30–1,440 / 도/초 | 연속 명중을 포함한 Y축 각속도의 상한. 60번째(인덱스 59). |

Host의 확정 피격 패킷은 부호 있는 회전 충격량을 전달하며 스냅샷은 풀린 각도와 각속도를 보정한다. 다른 기기는 피격 위치로 회전력을 다시 계산하지 않는다. 로비 복귀와 새 판에서는 회전 상태를 초기화하고, 일시정지 중에는 물리 업데이트가 멈춘다. 저장 Scene의 `Assets/Lab/Art/Targets/TargetSpinGradient.png`에는 비대칭 색 변화와 화살표가 있어 실제 회전 방향을 눈으로 확인할 수 있다. 이는 요괴의 최종 그래픽이 아닌 진단용 재질이다.

근거: `LabHangingTarget.ApplyHostImpact`·`FixedUpdate`·`ReconcileYaw`, `LabNetwork.OnProjectileResolved`·`PublishSnapshot`, `LabConfig.DeveloperFields`.

## 8. 생성 확률·아이템

GENERATE 한 번마다 Host가 아래 6개 확률로 종류를 정한다. 매 생성은 독립 추첨이며 같은 극성이 이어져도 보정하지 않는다. 결합 구슬의 오행 속성(화·수·목·금·토)은 결합 순간 같은 확률로 정해지며 이 패널로 조정하지 않는다.

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Yin Chance (%) | 40 | 정수 0–100 / % | 생성 결과가 음(Yin)일 확률. |
| Yang Chance (%) | 40 | 정수 0–100 / % | 생성 결과가 양(Yang)일 확률. |
| Relay Orb Chance (%) | 5 | 정수 0–100 / % | 릴레이 구슬 아이템 확률. |
| Secret Box Chance (%) | 5 | 정수 0–100 / % | 시크릿 상자 아이템 확률. |
| Potion Chance (%) | 5 | 정수 0–100 / % | 회복물약 아이템 확률. |
| Bomb Chance (%) | 5 | 정수 0–100 / % | 폭탄 아이템 확률. 6개 확률의 합은 반드시 100이어야 한다. |
| Item Lifetime (seconds) | 15 | 0.1–86,400 / 초 | 보관 중인 아이템의 수명. 생성 시점부터 세며 이웃에게 전달해도 초기화되지 않는다. |
| Item Unlock Swipes Min | 2 | 정수 0–20 / 회 | 아이템이 생성될 때 정해지는 잠금 횟수의 하한. 잠금은 **이웃 화면으로 넘길 때마다** 1씩 준다. |
| Item Unlock Swipes Max | 4 | 정수 0–20 / 회 | 잠금 횟수의 상한. Min ≤ Max여야 한다. Min·Max를 모두 0으로 두면 아이템이 처음부터 해금된 상태로 생긴다. |
| Potion Heal (HP) | 10 | 정수 0–300 / 개인 HP | 해금된 회복물약을 더블 탭했을 때 사용자의 개인 HP에 더하는 양. `Player Max HP`를 넘지 않는다. HP가 가득 차 있거나 0이면 사용이 거절되고 물약은 남는다. |
| Relay x2 Damage Multiplier | 2 | 1–20 / 배 | 릴레이 구슬이 해금 후 한 번 더 넘겨져 `×2` 단계일 때 `Damage per Hit`에 곱하는 값. 해금 직후 `×1` 단계는 항상 1배다. |
| Relay MAX Damage Multiplier | 4 | 1–20 / 배 | 릴레이 구슬이 해금 후 두 번 넘겨져 `MAX` 단계일 때의 배율. 이후 더 넘겨도 오르지 않는다. |
| Bomb Damage Multiplier | 3 | 1–20 / 배 | 해금된 폭탄을 던져 명중했을 때 `Damage per Hit`에 곱하는 값. 빗나가면 피해도 폭발도 없다. |
| Bomb Blast Radius (board widths) | 0.4 | 0–2 / 보드 폭 | 잠긴 폭탄이 터질 때 충격파가 닿는 거리. 폭탄 중심에서 이 거리 안에 중심이 있는 구슬·북·잡고 있던 구슬을 밀어낸다. 0이면 밀지 않고 이펙트만 나온다. 이펙트의 흰 원이 이 범위까지 퍼진다. **80번째(인덱스 79)**. |
| Bomb Blast Strength (board widths/s) | 9 | 0–20 / 보드 폭/초 | 폭탄 바로 옆이 밀려나는 속도. 범위 끝으로 갈수록 0까지 줄고, 원래 움직이던 속도에 더해진다. 9면 범위 중간(판 폭 20%)의 구슬도 바닥 감속으로 멈추기 전 판 폭의 약 5배를 갈 만큼 세서, 범위 안 구슬은 대부분 옆 가장자리를 넘어 이웃에게 간다. #167에서 3으로 시작해 화면 확인 후 9로 올렸다. **81번째(인덱스 80)**. |

아이템은 음·양과 조합되지 않는다. 해금된 **회복물약·시크릿 상자**는 같은 구슬을 0.35초 안에 두 번 탭해 그 자리에서 사용한다(한 번의 탭은 0.3초 이내, 이동 거리 구슬 반지름의 절반 이하). 시크릿 상자는 양옆에 속성 구슬 2개를 만들고 새 `Orb Lifetime`을 주며 보관 칸 하나가 더 필요하다. 해금된 **릴레이 구슬·폭탄**은 Combined처럼 위로 그어 던진다. 잠긴 폭탄의 타이머는 `Item Lifetime`이며, 잠긴 채 끝나면 보유자의 판에서 터져 충격파로 주변을 밀어낸다(아무것도 사라지지 않음). 터질 때의 진동은 `Sound and haptics` 칸의 `Bomb Haptic Style`(82번째, 기본 2 = Heavy)·`Bomb Haptic Intensity`(83번째, 기본 1, 0이면 끔)로 정하고, 남은 초가 몇 초부터 깜빡일지·빠르기·색은 `LabOrbAppearance`의 Inspector(`Bomb Fuse Warning`, 기본 3초·초당 2→8회)에서 정한다. 해금되는 순간 타이머가 꺼지고 일반 공격 구슬처럼 해금 시점부터 `Orb Lifetime`이 새로 시작되며, 끝나면 폭발 없이 사라진다. 피해 배율의 결과는 반올림한 정수이며 최소 1이다. 1인 솔로에는 이웃이 없어 잠긴 아이템이 해금되지 않으므로, 효과를 혼자 시험하려면 `Item Unlock Swipes Min`·`Max`를 0으로 둔다.

근거: `LabConfig.GenerationChances`, `LabModel.TryGenerate`·`TryCombine`·`TryTransfer`·`TryUseItem`·`TryAdvance`·`ConfirmHit`, `LabOrbBoard.EndDragAtWorld`, `LabOrbItemMarker.Show`, `LabNetwork.SyncBoard`.

## 9. 입력 제약과 조정 실험 예시

**값 간 관계:** `Starting Stamina`, `Orb Generation Cost`, `Stamina Recovery Amount`, `Stamina Reward per Hit`은 모두 `Maximum Stamina` 이하여야 한다. `Orb Stop Speed ≤ Orb Maximum Release Speed`, `Throw Minimum Swipe Speed ≤ Throw Maximum Swipe Speed`, `Throw Minimum Upward Speed ≤ Throw Maximum World Speed`, 생성 확률 6개의 합 = 100, `Item Unlock Swipes Min ≤ Item Unlock Swipes Max`도 필수다. 관계 위반 시 전체 입력이 거절된다.

**기준 실험:** DEFAULTS를 불러와 APPLY로 솔로를 시작한다. 비교 시간을 확보하려면 전투 시간·구슬 수명을 늘리고 생성 비용을 낮추되, 변경값을 함께 기록한다. Yin과 Yang을 직접 겹쳐 Combined를 만든 후 판 중앙에서 위로 던진다. 투척 비교에서 아이템이 섞이지 않게 하려면 `Yin Chance`·`Yang Chance`를 50으로, 아이템 확률을 0으로 둔다. `SHOT HIT/MISS | 비행 시간 | peak +최고 상승 | range 수평 이동`와 HP 변화를 적는다. 같은 손동작을 여러 번 반복하고 **한 번에 한 값**만 바꿔 APPLY + RESTART SOLO로 비교한다. Console/Player 로그에는 `C6_LAB_THROW`(입력·발사 속도)와 `C6_LAB_PROJECTILE`(충돌·비행 지표)가 남는다.

**예시 질문별 첫 조정 대상**

| 관찰·목표 | 먼저 비교할 값 | 함께 확인할 것 |
| --- | --- | --- |
| 공이 너무 곧장 가서 하강이 안 보임 | Upward Gain ↑, Forward Gain ↓를 각각 시험 | peak·비행 시간·실제 HIT; 카메라가 정면이면 깊이감이 약할 수 있음 |
| 약한 손동작도 투척으로 처리됨 | Minimum Swipe Speed 또는 Minimum Swipe Distance ↑ | 조합판 평면 이동과 투척의 경계, 최근 0.12초 입력 |
| 강하게 던져도 거리 차이가 작음 | Maximum World Speed 상한 여부, Power Exponent | `C6_LAB_THROW`의 실제 launch 속도 |
| 구슬이 금방 사라져 조합하기 어려움 | Orb Lifetime ↑ | Raw/Combined의 각각 생성 시각, 전달 후 수명 |
| 구슬이 너무 오래 굴러다님 | Orb Floor Deceleration ↑ 또는 Orb Stop Speed ↑ | 충돌·전달 속도까지 영향을 받는지 |
| 측면에서 던진 공이 잘 빗나감 | Origin Lateral Range 또는 Lateral Gain 비교 | 자동 조준 기능은 현재 없음; 판정 반경 변경은 난이도도 바꿈 |
| 아이템이 너무 자주/드물게 나옴 | 아이템 4종 확률과 Yin/Yang 확률을 함께 조정 | 합 100 유지, 보유 한도에 아이템도 포함됨 |
| 아이템이 해금되기 전에 사라짐 | Item Lifetime ↑ 또는 Item Unlock Swipes Max ↓ | 세 명이 실제로 넘기는 데 걸리는 시간 |
| 폭탄 충격파가 너무 약하거나 셈 | Bomb Blast Strength ↑↓, 넓이는 Bomb Blast Radius | 범위 안 구슬이 얼마나 이웃에게 넘어가는지 |
| 폭탄을 해금할 시간이 부족함 | Item Lifetime ↑ 또는 Item Unlock Swipes Max ↓ | 잠긴 폭탄 타이머도 Item Lifetime을 쓰므로 다른 아이템 수명도 함께 바뀜. 해금 후 던질 시간은 Orb Lifetime |
| 릴레이 MAX가 너무 약하거나 강함 | Relay MAX Damage Multiplier | 요괴 HP와 MAX까지 넘기는 데 걸리는 시간 |

이 표의 화살표는 **시험 방향**이지 검증된 추천값이 아니다. 잘못된 투척을 자동 보정하거나 요괴를 추적하는 기능은 현재 없다.

## 10. 이 패널로 조정할 수 없는 것·검증 범위

방의 **목표 인원**은 83개 수치 필드와 별도의 `ROOM PLAYERS` 슬라이더에서 방 생성 전에 선택한다. 좌석의 입장 순서, 네트워크 주소·포트, 생성 추첨 방식(독립 추첨), 오행 속성 확률(균등), 조합·전달의 승인 규칙, 북을 받을 사람의 무작위 배치, 손동작 샘플 창 **0.12초**, 타깃 Cylinder의 실제 크기·위치, 전투 카메라의 FOV와 화면 내 viewport는 83개 입력칸으로 조정하지 않는다. DEV 솔로에는 이웃이 없어 화면 간 전달과 아이템 해금을 시험할 수 없다.

기존 PR #76 기준 문서는 해당 코드·Scene·설정 에셋과 당시 `docs/VALIDATION.md`의 증거를 **읽어 확인**해 작성됐다. 그 기록의 EditMode **12/12**, PlayMode **20/20** 및 Mac Development 앱 빌드는 이번 3D 조합판 전환의 통과 증거가 아니다. 이 전환의 Unity 컴파일·EditMode·PlayMode, Mac·iOS 빌드, 실제 조작은 새 실행 기록으로 구분해야 한다. 문서의 물리 용어를 갱신한 것만으로 실행 결과를 PASS로 보지 않는다.

## 11. 코드·Scene 연결 지도

- 설정 필드·범위·일괄 검사: `Assets/Lab/Scripts/Core/LabConfig.cs` — `DeveloperFields`, `CaptureDeveloperValues`, `TryApplyDeveloperValues`.
- 저장 기본값: `Assets/Lab/Resources/LabConfig.asset`.
- 저장된 DEV 버튼·입력칸·슬라이더(39개 행이 저장돼 있고 나머지는 실행 시 마지막 행을 복제해 83개로 늘림)·목표 인원·Host 적용 버튼 연결: `Assets/Lab/Scenes/Lab.unity`의 `LabDeveloperMode` 컴포넌트.
- 패널 열기·DEFAULTS·SOLO/Host 설정 적용·슬라이더 변환: `Assets/Lab/Scripts/UI/LabDeveloperMode.cs`.
- Host 전투 수치·개인 HP·전원 0 패배·스태미나·생성 확률·속성·아이템 잠금·구슬 만료: `Assets/Lab/Scripts/Core/LabModel.cs`.
- 아이템 더블 탭 사용·효과(물약·상자), 릴레이 단계, 폭탄 폭발·피해 배율: `Assets/Lab/Scripts/Core/LabModel.cs`(`TryUseItem`·`TryTransfer`·`TryAdvance`·`ConfirmHit`)와 `LabOrbBoard.EndDragAtWorld`.
- 폭탄 폭발: Host 폭발 기록은 `LabModel.TryAdvance`·`DrainBombExplosions`, 보유자 전달은 `LabNetwork`의 `bombBlast` 패킷, 충격파는 `LabOrbBoard.Blast`, 이펙트는 `LabBombBlastEffect`, 진동은 `LabHaptics.PlayForBomb`, 깜빡임은 `LabOrbItemMarker.SetFuse`.
- 아이템 아이콘·자물쇠·잠금/단계 배지·폭탄 남은 초·해금 연출: `Assets/Lab/Scripts/Orb/LabOrbItemMarker.cs`, 아이콘·자물쇠·속성 재질과 잠금·해금 연출 값: `Assets/Lab/Art/Orbs/LabOrbAppearance.asset`. 해금 연출 값은 DEV 패널이 아닌 Inspector에서 조정한다.
- 3D 조합판 구슬 물리·XY 손동작·공통 구체 외형: `Assets/Lab/Scripts/Orb/LabOrbBoard.cs`와 `LabOrbView.cs`.
- 생성 북: 무작위 배치·북 보유자만 생성·북 위치와 360° 방향 추첨·벽 근처 속도 제한·북 전달은 `LabModel.cs`(`PlaceDrums`·`TryGenerate`·`SpawnVelocityFor`·`TryTransfer`), 탭 생성·끌기·떠돌기·가장자리 되돌림·튀어나오기는 `LabOrbBoard.cs`(`EndDragAtWorld`·`Drift`·`UpsertOrb`), 북 그림·회전은 `LabOrbView.cs`, 북 그림 지정은 `LabOrbAppearance.asset`의 `Drum Sprite`.
- 발사 위치·초기 속도: `Assets/Lab/Scripts/Throw/LabThrowMath.cs`.
- 공의 중력·저항·충돌·수명: `Assets/Lab/Scripts/Throw/LabProjectile.cs`.
- 요괴 매달림·Y축 회전 및 실제 피격체 이동: `Assets/Lab/Scripts/Throw/LabHangingTarget.cs`; 진단용 재질: `Assets/Lab/Art/Targets/TargetSpinGradient.mat`.
- Host의 방 설정·동기화·발사·피격 확정·결과 로그: `Assets/Lab/Scripts/Session/LabNetwork.cs`와 `LabSessionSettings.cs`.
- 카메라·보드 크기 적용: `Assets/Lab/Scripts/UI/LabSeatCamera.cs`와 `LabSceneController.cs`.
- 과거 검증 기록: `docs/VALIDATION.md`. 투척 비교 절차: `docs/THROW_TUNING_GUIDE.md`.
