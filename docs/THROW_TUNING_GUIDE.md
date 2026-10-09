# 직선 투척 물리 실험 가이드

기준: Unity 6000.6.5f1, `Assets/Lab/Scenes/Lab.unity`. 이 가이드는 커브볼 없이 Combined 구슬을 원통으로 던질 때의 조절값을 비교하기 위한 것이다. 기본값은 이번 변경 전의 중앙 투척 궤적을 유지한다. 새 수치는 `Assets/Lab/Resources/LabConfig.asset`에서 설정할 수 있고, Editor 또는 Development Build에서는 **DEV** 패널의 실행 중 복제본을 바꿔 솔로 라운드별로 시험할 수 있다. DEV 변경은 원본 에셋에 저장되지 않는다.

## 우선 조절할 값

| DEV 입력 이름 | 기본값 | 올렸을 때의 효과 |
| --- | ---: | --- |
| Throw Power Exponent | 1 | 기준 스와이프 속도(초당 구슬 판 너비 1.5배)보다 빠른 손동작은 더 강해지고, 느린 손동작은 더 약해진다. 1이면 종전 선형 변환이다. |
| Throw Loft Offset (degrees) | 0 | 발사 속력은 유지하면서 위쪽 비중을 높이고 전진 비중을 낮춘다. 음수는 더 낮고 평평한 궤적이다. 허용 범위는 -30°~30°다. |
| Throw Air Damping | 0 | 비행 중 Rigidbody의 선형 감쇠가 커져 이동 거리가 짧아진다. 0이면 종전처럼 감쇠가 없다. |

기존 `Throw Forward Gain`, `Throw Upward Gain`, `Throw Lateral Gain`은 손동작의 전진·상향·좌우 속도 배율이다. `Throw Gravity`는 아래 방향 가속도, `Throw Maximum World Speed`는 최종 초기 속도 상한이다. 상한에 자주 걸리면 Power Exponent나 Gain을 높여도 차이가 작다. `Throw Minimum Upward Speed`는 약한 던지기의 상승 속도 하한이다. `Throw Lifetime (seconds)` 전에 명중·바닥 접촉이 없으면 빗나감으로 종료한다.

구슬 판에서 손을 놓기 전 **0.12초**의 이동 방향·속도를 입력으로 사용한다. 좌우 끝의 이웃 전달 판정이 우선이며, 판 내부에서 Combined를 위로 튕겨 놓아야 투척한다. 발사 위치는 놓은 가로 위치를 따른다. Host만 실제 Collider 명중과 HP 감소를 확정하며, 클라이언트 투사체는 표시용이다. 회전·커브볼은 이 실험에 포함하지 않는다.

## 비교 시험 순서

1. `Lab.unity`를 Play하거나 Mac Development 앱을 실행한다. **DEV**에서 `Battle Duration (seconds) = 600`, `Orb Lifetime (seconds) = 60`, `Orb Generation Cost = 1`로 두면 조합 시간이 넉넉하다.
2. 기준 조건을 `Throw Power Exponent = 1`, `Throw Loft Offset (degrees) = 0`, `Throw Air Damping = 0`으로 둔다. **APPLY + START SOLO**를 누른다.
3. **GENERATE**로 Yin과 Yang을 만든 뒤 직접 겹쳐 Combined를 만든다. 구슬 판 중앙에서 위쪽으로 던진다. 발사 높이와 손동작을 가능한 한 비슷하게 유지한다.
4. 명중·빗나감 때 화면의 `SHOT HIT/MISS | 비행 시간 | 최고 상승 | 수평 이동 거리`와 HP를 기록한다. Console/Player 로그의 `C6_LAB_THROW`에는 입력 속도·3D 발사 속도·설정값, `C6_LAB_PROJECTILE`에는 충돌 결과·지표·접촉점이 남는다.
5. **DEV → 값 하나만 변경 → APPLY + RESTART SOLO**로 비교한다. 예를 들어 먼저 Power Exponent `1 → 1.4`, 다음에는 기준으로 돌리고 Loft Offset `0 → 10`, 마지막으로 Air Damping `0 → 0.3`을 각각 시험한다. 한 조건당 여러 번 던져 손동작 차이와 설정 효과를 구분한다.

실제 손가락 투척의 조작감과 궤적은 iPhone Development Build에서 별도 확인해야 한다. Mac 마우스 자동 드래그와 PlayMode의 직접 메서드 호출은 실기기 터치 검증을 대신하지 않는다. 다인 연결에서 사용하는 값은 각 앱의 `LabConfig.asset`에 들어 있으므로, 서로 다른 설정으로 빌드하면 클라이언트 표시 궤적과 Host 판정 궤적이 달라질 수 있다.

관련 코드: `LabOrbBoard`(손동작), `LabThrowMath`(2D→3D 발사), `LabProjectile`(중력·저항·충돌), `LabNetwork`(Host 확정·시험 결과), `LabDeveloperMode`(솔로 값 적용).
