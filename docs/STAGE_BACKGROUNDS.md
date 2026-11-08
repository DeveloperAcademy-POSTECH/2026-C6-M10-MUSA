# 스테이지 배경 편집

Hapio는 하나의 Battle Scene에서 배경 세트를 선택한다. `BattleStage/Stage Backgrounds`의 `GameStageBackgrounds`가 현재 GameSession의 스테이지를 따라 기본 세트 또는 override를 활성화한다. Stage 1과 땅따먹기는 기본 배경, Stage 2는 야광귀 밤 배경, Stage 3는 지귀 석양 배경을 사용한다. 별도 Lab Scene은 변경하지 않는다.

## 미리보기와 PNG 교체

1. `Assets/Hapio/Scenes/Battle.unity`를 연다.
2. Hierarchy의 `BattleStage/Stage Backgrounds`를 선택하고 Inspector의 Stage 1/2/3 미리보기 버튼을 누른다. 편집 미리보기 상태를 저장해도 실제 게임 진입에서는 GameSession의 스테이지가 우선한다.
3. Stage 2 그림은 `BattleStage/Stage02 Background Studio`에서 편집한다. 기본 배경은 기존 `Background Studio`다.
4. PNG·색·투명도 등을 바꾼 뒤 기존 `이미지만 적용 / Preserve Layout` 버튼을 누르고 Scene을 저장한다. 원형 배치 재계산은 개별 Transform 조정을 덮어쓰므로 의도적으로 전체 재배치할 때만 사용한다.

Stage 2 PNG는 `Assets/Hapio/Art/Environment/Stage02/Textures`와 `Extras`, 독립 재질은 `Materials`에 있다. 같은 프로젝트 파일을 덮어쓸 때 `.meta`를 유지하면 기존 참조를 재사용한다. 별도 이름의 PNG는 해당 Studio 슬롯에 직접 연결한다. 폴더 업로드 자동 분류 기능은 이번에 추가하지 않았다.

## Stage 2의 초기 구성

산·소나무 군락·바위 소나무·대문과 담장·바위·풀은 기본 세트의 카드 Transform과 개별 조정을 복사했다. 하늘은 `Sky_Hanji`, 바닥 및 보드 뒤 빈틈 채우기는 `HanjiSand_BaseColor`를 사용한다. 기존 기본 배경과 재질을 공유하지 않는다.

- 큰 소나무 가지는 비활성 상태로 유지한다.
- 구름은 기존 3종과 순환 애니메이션을 유지하며 푸른 회색 tint를 적용한다.
- 나비는 Stage 2에서 비활성화했다. 기본 세트의 나비는 유지한다.
- 달 1개는 원경 위쪽, 한옥 2채는 전장 바깥쪽, 도깨비불 5개는 바깥 원에 배치했다. 모두 물리 충돌과 조명 없이 그리는 장식 카드다. 도깨비불은 정적 PNG이며 전투 기믹이 아니다.
- 하단 HUD 자체의 이미지는 교체하지 않았다. 바닥·하늘 반복 이음새와 기기별 색감은 추가 미술 검수가 가능하다.

Ground PNG는 원래 BattleFloor 아래, Board Hanji Backing은 원래 OrbCamera 아래에 전용 복사본을 둔다. 배경 선택기는 Studio·바닥·보드 backing 세 개를 함께 전환하며 카메라·충돌 바닥·게임 설정은 공유한다.

## 추가 배경 세트

기본 세트와 같은 방식으로 별도 Studio·바닥·보드 backing을 만들고 재질을 분리한 뒤 `Stage Overrides`에 스테이지 번호와 세 참조를 등록한다. 하나의 스테이지 번호에는 하나의 override만 등록한다. 미등록 스테이지는 기본 배경으로 돌아간다. 배경 선택기의 목록은 확장 가능하지만 실제 게임의 스테이지 수는 기존 3개이며 Stage 4 이상 게임 진행을 추가하지 않았다.

화면·연결·자동 검사의 실제 수행 결과는 `VALIDATION.md`를 따른다.

## Stage 2 기와집·호롱불 교체

Stage 2의 Rock and pines 그룹은 비활성화했다. 기존 Hanok 그룹의 두 카드는 신발·짚신이 놓인 Hanok_Shoes PNG로 바꾸고 바위소나무의 두 위치(각도150/330, 반경11)로 이동했다. 집 높이2.8·너비10, 각 집 옆에 Lantern_Post_Yellow 카드1개씩(총2개)을 둔다. 기존 단독 한옥 위치에는 중복 배치하지 않는다. 새 재질/PNG는 Stage02 전용 폴더에 있다. 호롱불은 현재 정적 이미지이며 점등 애니메이션은 아직 추가하지 않았다.

Stage 2의 도깨비불은 미술 피드백으로 visible=false 및 그룹 비활성화했다. PNG와 기존 카드 참조는 보존하고 기와집·노란 호롱불은 유지한다.

## 은은한 Stage 2 눈

Stage02 Background Studio/Subtle Snow는 기본 ParticleSystem이다. 넓이32×32에서 초당5개, 최대110개, 크기.035~.065·alpha.32, 하강속도.48을 사용한다. 16~20초 수명과 진입/소멸 fade, 미세 drift/noise로 성기게 내린다. Stage2 Studio 아래라 다른 스테이지에서는 꺼진다. 충돌·조명·쌓임은 없고 게임 규칙에 관여하지 않는다. Inspector Emission의 Rate over Time으로 양, Main의 Start Color alpha로 밝기를 조정한다.

## 노란 반딧불이 한 쌍

Stage02 Background Studio/Firefly Couple에는 정확히2개의 Firefly가 있다. 32초 반복 Animator clip이 같은 경로 주변을 함께 이동하며 서로 가까이 원을 그리게 한다. 각 개체는 billboard인 작은 노란 core/옅은 halo와 .65초 TrailRenderer를 사용한다. halo 크기만 미세하게 맥동하고 잔상 alpha는 끝에서0이 된다. 전투 판정·물리·실제 조명은 없고 Stage2 외에는 Studio와 함께 꺼진다. 수정 지점은 Weather/FireflyCouple.anim, FireflyGlow.mat, FireflyTrail.mat 및 각 TrailRenderer다.

반딧불이 시인성 조정: core 지름.15→.45(3배), halo 약.46→1.1(2.4배), halo opacity.23→.42, trail 폭.045→.095·시작alpha.5. Animator 상태 speed2.2로32초 clip의 실제 반복 주기는 약14.5초다.

기와집 접지 보정: 가져온 텍스처의 실제 카드 높이5에 맞춰 Hanok 그룹 center height를2.8에서2.23으로 낮췄다. 두 집의 돌기단 하단을 지면에 약간 묻어 어두운 공중 틈을 없애고 신발은 유지한다.

## 지나가는 짚신 발자국

Stage02 Background Studio/Passing Footsteps는 바닥 위14개 짚신 밑창 자국을 좌우 교대로 표시한다. .16초 간격으로 찍히고 각 자국은 약2.3초 유지 뒤3.5초까지 순서대로 사라진다. 전체10초 반복. 가까운 바닥에 .72×1.25 크기·먹회색alpha.94로 배치해 전투 카메라에서도 보이게 했다. 기본 Animator와 재질 alpha 커브를 사용하며 충돌/게임 규칙 변경은 없다.

발자국 경로는 가로 이동 대신 화면 전경에서 기와집 문 앞으로 향하도록 변경했다. 시작(2.4,-.06,13)에서 끝(5.2,-.06,1.9)으로 완만히 이어지며 각 밑창도 진행 방향을 향한다.

## Stage 3 지귀 석양 PNG

Stage03 Background Studio에 석양 하늘, 첨성대·석등·부처 바위·소나무와 구름·불티·연무·불꽃 PNG를 정적으로 배치했다. PNG 및 독립 재질은 Art/Environment/Stage03에 있으며 금팔찌 단독 PNG는 제외했다. 애니메이션은 추가하지 않았다.

## 요괴별 바닥 PNG

Stage01_Ground_Gumiho(옅은 청록), Stage02_Ground_Yagwanggwi(청회색), Stage03_Ground_Jigwi(황토·주황)을 각 스테이지의 ground 및 board backing에 연결한다. 재질은 Stage01/02/03의 YokaiGround·YokaiBacking으로 분리하며 tint는 흰색이다. 하단 조합판 UI 자체 이미지는 변경하지 않는다.

Stage 1 바닥·조합판 뒤는 Stage01_Ground_SandBeige다(#490에서 사용자 확인). BlushBeige·Gumiho PNG는 후보로 남아 있으며 연결하지 않는다.

2스테이지는 #487에서 Stage02 Background Studio의 ground·board backing PNG와 Stage02 YokaiGround·YokaiBacking 재질의 `_BaseMap`을 모두 Stage02_Ground_Yagwanggwi로 연결했다. 기기는 저장된 재질의 `_BaseMap`을 그리므로 studio 값과 재질 값이 같아야 하며 `GameStageBackgroundAssetTests`가 세 스테이지 모두 이를 검사한다.

## 저장 재질과 숨은 사본 (#504)

바닥·한지 배경·보드 뒤 재질은 URP Lit/Unlit이다. 이 셰이더는 쓰지 않는 `_MainTex`·`_Color` 사본도 저장한다. Editor가 재질 속성을 처음 읽을 때(그리기·Inspector 등) URP가 이 사본을 `_BaseMap`(텍스처·타일링·오프셋)과 `_BaseColor`에서 다시 복사한다. 그 결과가 저장된 사본과 다르면 재질이 저장 대상(dirty)이 되고, 다음 저장 때 의도하지 않은 .mat 변경이 생긴다. #504에서 본 변경이 이것이다.

URP는 `_Color`에 `GetColor("_BaseColor")`가 돌려준 값을 쓴다. 일부 색은 이 값이 저장값보다 float 한두 단계 작다(예: 0.55 → 0.54999995). 그래서 `_Color`에 같은 숫자를 그대로 저장해도, URP가 다시 복사하면 값이 달라져 dirty가 된다.

- `이미지만 적용`(`LabBackgroundStudio.ApplyImage`)은 URP와 같은 방식으로 사본을 쓴다. `_BaseMap`·`_BaseColor`를 먼저 쓰고, 그 값을 다시 읽어 복사한다. 장식 카드의 InkCard 셰이더에는 사본이 없다.
- 저장된 재질이 Studio의 PNG·색·타일링과 다르면 Studio Inspector가 경고한다. 바닥은 노멀 맵·강도·부드러움도 비교한다. 게임은 재질을 그리므로, 경고가 보이면 `이미지만 적용`을 누르고 Scene을 저장한다.
- `GameStageBackgroundAssetTests`는 Battle의 세 Studio와 Lab Studio의 재질 12개를 확인한다. 모두 적용된 상태인지 보고, 재질 파일을 직접 읽어 `_MainTex`가 `_BaseMap`과 같은지, `_Color`가 URP가 `_BaseColor`에서 복사할 값과 같은지 본다. 불러온 재질에는 이미 다시 복사된 값이 보일 수 있어서 파일을 읽는다.
- 재질을 Editor 스크립트로 바꿀 때도 `_BaseMap`만 바꾸지 않는다. Editor 전용인 `ApplyImage`를 쓰거나, URP처럼 기본값을 다시 읽어 사본을 쓴다.

## 조명 작업실

Hapio > 배틀 조명 작업실에서 Battle Scene 조명 색·강도·방향과 환경광·반사를 조절한다. 편집 모드에서 스테이지 미리보기와 씬 저장을 지원하고 플레이 중 변경은 임시다. 창을 열었을 때 상태 복원 및 현재 상태를 복원 기준으로 지정할 수 있다. 복원 기준은 창 세션 안에서 유지하며 플레이 상태 전환 시 새로 잡는다. 조명은 세 스테이지 공통이고 하늘 PNG는 별도 Unlit 재질이다.
