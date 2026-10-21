# 포물선·직구 투척 물리 조정 가이드

기준: Unity 6000.6.5f1, `Assets/Lab/Scenes/Lab.unity`. Combined 구슬을 현재 박 표적의 실제 Collider로 던질 때 **기존 Arc와 추가한 Straight**를 비교하는 가이드다. 기본 모드는 Arc이며 기존 포물선 계산을 유지한다. 설정 원본은 `Assets/Lab/Resources/LabConfig.asset` 하나다. Inspector의 `Projectile throw form`에서 모드를 바꾸거나, Editor/Development Build의 **DEV → Throw form**에서 실행용 복제본을 바꾼 뒤 새 판을 시작한다. DEV는 원본 에셋을 자동 저장하지 않는다. 이 문서는 설정과 코드 경로를 설명하며 실행 결과는 `docs/VALIDATION.md`의 #190 기록에서 별도로 확인한다.

## 기존 입력·조준·세기 유지

구슬을 놓기 전 **0.12초**의 조합판 XY 이동 방향·속도를 사용한다. 좌우 경계의 이웃 전달이 우선이고, 판 내부에서는 기존 위로 스와이프하는 거리·속도·상향 우세 조건을 만족해야 던진다. 두 폼 모두 같은 입력 판정, 가로 놓기 위치(`releaseX01`), 좌우 조준, 스와이프 세기 변환·초기 속력 상한을 사용한다. 카메라·FOV·플레이어 자리를 변경하지 않으며 이동한 출발점에서 요괴를 향해 자동 재조준하지 않는다.

조합판 구슬은 XY 평면에 제한된 3D 구체·`Rigidbody`·`SphereCollider`다. 투척하면 별도의 비행 물리 루트를 만들며 구체 표시와 실제 `SphereCollider`가 같은 발사 위치·Rigidbody를 따른다. 궤적·기존 피격·색칠은 실제 Host 투사체의 접촉 결과를 재사용한다. Client의 복제 투사체는 표시용이고 피해·도색을 확정하지 않는다.

## 기존 공통 조절값

아래 값은 **저장된 `LabConfig.asset`의 현재 기준값**이다. `ScriptableObject.CreateInstance<LabConfig>()`의 코드 기본값과 다를 수 있으며 DEFAULTS는 저장 에셋 값을 불러온다.

| DEV 입력 이름 | 현재 원본 값 | 조정 효과 |
| --- | ---: | --- |
| Throw Power Exponent | 1.2 | 기준 스와이프 속도(초당 판 너비 1.5배)보다 빠른 입력은 더 강하고 느린 입력은 더 약해진다. 1이면 선형 비교 조건이다. |
| Throw Loft Offset (degrees) | 0 | 기존 Arc 속도 계산의 상승·전진 발사각을 조절한다. Straight도 이 Arc 결과에서 세기·수평 조준을 가져온다. |
| Throw Air Damping | 0 | 두 폼에 공통으로 적용되는 `Rigidbody.linearDamping`. |
| Throw Maximum World Speed | 12.5 | 두 폼의 동일한 최종 **초기 속력** 상한. |
| Throw Gravity | 30 | Arc의 중력 가속도. Straight에서는 아래 배율을 곱한다. |
| Throw Lifetime (seconds) | 4 | 명중·바닥 접촉이 없을 때 MISS로 종료할 최대 비행 시간. |

`Throw Lateral/Upward/Forward Gain`은 기존 손동작 변환 배율이다. 상한에 자주 걸리면 Gain이나 Power Exponent를 높여도 초기 속력 차이가 작다. `Throw Minimum Upward Speed`는 Arc 계산의 상승 성분 하한이며 Straight의 실제 상승 속도 하한은 아니다.

## 직구 설정 7개

현재 DEV의 `Throw form`7개는 인덱스 **111~117**에 있다. `Throw Form`은 ARC/STRAIGHT 토글이고 나머지 6개는 숫자 입력과 슬라이더다. 토글을 바꾸면 **아직 APPLY하지 않은 입력값**을 기준으로 직구 전용 행을 즉시 켜거나 끈다. Arc에서는 직구 6개 행이 비활성화되지만 입력값은 유지된다. 기본 프로필은 **DEMO_TUNING_VALUE**이며 최종 조작감의 확정값이 아니다.

| DEV 입력 / Inspector 필드 | 인덱스 | 기본값 | DEV 허용 범위 | 의미 |
| --- | ---: | ---: | --- | --- |
| Throw Form / Throw Form | 111 | 0: Arc | 정수 0–1 | 0: 기존 포물선, 1: 직구. |
| Straight Origin Offset X / Straight Origin Offset.x | 112 | 0.35 | −5–5 / 월드 거리 | 던진 사람의 화면 오른쪽으로 더하는 거리. 0이면 기존 가로 출발 위치를 유지한다. |
| Straight Origin Offset Y / Straight Origin Offset.y | 113 | 0.4 | −5–5 / 월드 거리 | 월드 위쪽으로 더하는 높이. |
| Straight Origin Offset Z / Straight Origin Offset.z | 114 | 0 | −5–5 / 월드 거리 | 해당 자리에서 타깃 쪽 수평 방향으로 더하는 거리. |
| Straight Pitch Offset (degrees) / Straight Pitch Offset Degrees | 115 | 3 | −30–30 / 도 | 낮춘 발사각에 추가할 각도. 음수면 아래쪽으로 향할 수 있다. |
| Straight Loft Scale / Straight Loft Scale | 116 | 0.1 | 0–1 / 배율 | Arc의 실제 상승각을 남기는 비율. 0이면 Pitch Offset만 사용한다. |
| Straight Gravity Scale / Straight Gravity Scale | 117 | 0.1 | 0–2 / 배율 | 공통 Throw Gravity에 곱한다. 0이면 중력 없음, 기본 원본 값 기준 30×0.1=3이다. |

### 발사 위치·방향의 계산

1. 기존 `SeatPosition`과 가로 놓기 위치로 Arc 발사 위치를 구한다.
2. Straight에만 `위치 = Arc 위치 + 오른쪽×X + 월드 위×Y + 자리의 타깃 쪽 수평 방향×Z`를 적용한다. 이 축은 **구슬 소유자의 실제 자리**에서 구하며 Host의 현재 카메라 좌표를 사용하지 않는다.
3. 기존 Arc 계산으로 얻은 최종 초기 속도 `vArc`에서 속력 `s=|vArc|`, 수평 단위 방향 `h`, 상승각 `a=atan2(vArc.y, |vArc 수평 성분|)`를 구한다.
4. Straight의 상승각은 `b=clamp(a×LoftScale + PitchOffset, −30°, 60°)`다. `vStraight=s×(h×cos(b) + 위×sin(b))`로 재배분한다. 같은 입력이면 Arc와 **초기 속력·수평 조준 방향**은 같고 상향 비중만 달라진다.
5. Arc 중력은 기존 `ThrowGravity`, Straight 중력은 `ThrowGravity×StraightGravityScale`이다. 중력은 공에만 적용하며 Unity 전역 중력을 바꾸지 않는다.

`X=0`으로 놓으면 기존 중앙 기준, 양수면 오른쪽 출발을 비교할 수 있다. 실제 보이는 높이와 충돌은 Y·Z·기존 `Throw Origin`·가로 놓기 위치에도 영향을 받는다. 극단적인 오프셋·음수 각도는 바닥 접촉·빗나감·화면 밖 출발을 만들 수 있으며 자동 보정하지 않는다. `Loft Scale=1`이어도 Straight의 각도 제한이 있으므로 기존 포물선 자체를 비교하려면 **ARC**를 선택한다.

## 적용 시점·공유 설정

- 솔로: DEV에서 **START SOLO / RESTART SOLO**로 새 판에 적용한다.
- 공유 방: Host가 접속 전 **CREATE ROOM**, 전투 전 **APPLY TO ROOM**, 공동 일시정지 뒤 **APPLY & RESTART**를 사용한다. Client는 Host 설정을 받으며 직접 변경하지 않는다.
- 설정은 **165개 전체**를 원자적으로 검증하고 공유 지문에 포함한다. 프로토콜은 **5650**, 연결 토큰은 `C6PhysicsLab/14`이며 길이가 다른 배열·다른 프로토콜을 혼합하지 않는다.
- 각 공은 발사 순간 중력·수명을 캡처한다. 초기 위치·속도·반경·감쇠도 발사 시 정하며, 비행 중 원본 Inspector나 DEV 초안을 바꿔 이미 날아가는 공을 꺾지 않는다.
- Host가 구슬 소유자의 자리로 위치·속도를 계산해 Client에 보내며 Client는 그 발사값을 재생한다. 실제 접촉·명중 보상·피해·색칠 승리는 기존 Host 판정을 따른다.

## 비교 시험 순서

1. `Lab.unity`를 Play하거나 Mac Development 앱을 실행한다. **DEV**에서 전투 시간·구슬 수명을 늘리면 비교할 시간을 확보할 수 있다. 생성 확률은 Yin/Yang 50/50, 아이템 4개는 0으로 두면 Combined 준비에 집중할 수 있다.
2. DEFAULTS를 불러 **ARC**로 시작한다. 저장 원본의 Power Exponent는 **1.2**다. 과거 선형 응답과 비교할 때만 1로 바꾸고 실험 기록에 명시한다.
3. 북을 탭해 Yin/Yang을 만들고 조합한다. 같은 가로 위치에서 위로 던져 `SHOT HIT/MISS | 비행 시간 | peak 최고 상승 | range 수평 이동`을 기록한다.
4. **DEV → Throw form → STRAIGHT → RESTART SOLO**로 전환한다. 우선 다른 값은 DEFAULTS와 동일하게 유지하고, 출발 위치·상승 높이·명중 지점을 비교한다.
5. 이후 `Origin Offset X`, `Pitch Offset`, `Loft Scale`, `Gravity Scale`을 한 번에 하나씩 바꾸고 같은 입력 방향·비슷한 속도로 여러 번 던진다. `C6_LAB_THROW` 로그의 form·origin·launch·gravity와 `C6_LAB_PROJECTILE` 결과를 함께 기록한다.
6. 공유 시험에서는 Host 설정이 확인된 같은 판에서 각 자리의 발사점과 Client 표시를 비교한다. Inspector의 `Throw Form`을 바꾼 경우에도 새 판을 시작해 비교한다.

이 순서는 검증 시나리오이며 수행 완료를 뜻하지 않는다. 실제 터치 조작감·시각 피드백은 iPhone Development Build에서 별도 확인한다. Mac 자동 입력·단위 검사·PlayMode의 직접 메서드 호출을 실기기 통과로 기록하지 않는다.

관련 코드: `LabConfig`(단일 원본·7개 설정·165개 공유 값), `LabDeveloperMode`(Throw form 토글·슬라이더), `LabOrbBoard`(기존 입력), `LabThrowMath`(자리별 출발점·모드별 방향), `LabProjectile`(발사 시 캡처한 중력·수명·실제 충돌), `LabNetwork`(Host 확정·공유 설정·발사값), `LabSessionSettings`(지문).
