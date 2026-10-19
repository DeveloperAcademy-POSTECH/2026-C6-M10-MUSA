# 전투 UI 이미지

## CombinationBoard (#227)

사용자 제공 `조합대.png`(804×1748)의 위쪽 투명 여백 914px만 제거한 804×834 PNG다. 질감·테두리·알파 픽셀은 그대로 보존했다. 2026-10-20 재첨부 원본의 RGB 색상으로 다시 교체했으며, 기존 알파·65개씩의 경계와 화면 배치는 동일하다. 원본의 화면 구성을 맞추기 위해 `Lab.unity`의 OrbCamera 높이는 `834 / 1748`, OrbInteractionArea의 상단 앵커도 같은 비율로 저장한다.

`CombinationBoard.mat`는 이 Scene의 OrbBoardPlate에 연결한다. LabOrbAreaProfile의 위·아래 경계는 새 PNG 알파 임계값 0.5, 각 65개 지점으로 베이크했다. 기존 하단 HP 바와 배경은 숨기며, 비활성 하단 배경은 물리 경계에서 제외한다. 타이머·플레이어 배지·HP/스태미나 UI는 별도 작업이다.

이미지를 바꾸면 머터리얼 텍스처와 SourceTexture뿐 아니라 베이크된 경계도 함께 갱신해야 한다. 기존 Fit Curved Orb Area 도구는 구형 BoardPlate.png를 대상으로 하므로 새 조합대에 그대로 실행하지 않는다.

## Player1Circle ~ Player5Circle (#227)

사용자 제공 P1~P5 PNG(각90×94)를 원본 그대로 적용했다. 기존 파일명·GUID와 LabHud.playerCircleSprites의 P1→P5 순서, 번호 선택/좌우 이웃 연결을 유지한다. 좌상단 위치(402×874 기준)는 왼쪽(-6,480), 오른쪽(365,480), 본인(65,809)이다. Figma `2527:7969` 기준45×47 영역에서 preserveAspect로 표시하며, 번호 글자는 PNG에 포함돼 있다.

## StaminaBarConcept (#227)

사용자 제공 `스테미나.png`(446×88)를 원본 보존했다. `C6 Lab/Apply New Stamina Bar`는 베이지 테두리(`StaminaBarFrame.png`)와 내부 다섯 칸의 흰색 알파 마스크(`StaminaBarFillMask.png`,374×50)를 분리해 저장 Scene에 연결한다. 알파와 손그림 가장자리는 원본에서 보존한다. 내부 마스크만 타이트하게 잘라 테두리 바깥 여백이 스태미나 비율에 포함되지 않도록 한다.

402×874 기준 바는223×44, 중심(229,832.5)이며 본인 배지 옆에 표시한다. 기존 LabShapeFillImage에 찬 색 `#2F62A2`, 빈 색 `#C1D2E0`를 설정하고 현재 스태미나/최대 스태미나를 왼쪽부터 연속 반영한다. 생성 시 소모·회복·최대치와 Config 원본은 변경하지 않는다. 구형 `Apply Bottom HUD Design` 대신 새 전용 메뉴로 재적용한다.

기존 `FigmaPlayerHpBar`와 `BottomHudBackground`는 비활성으로 보존한다. 조합판은 숨겨진 HUD의 곡선 대신 실제 종이 하단 곡선을 사용한다. 플레이어 HP 수치와 아이템 효과는 그대로 유지한다.

## Timer / Pause (#227)

사용자 제공 일시정지 PNG(66×68)와 글자 없는 타이머 PNG(156×67)를 기존 PauseButton.png·TimeFrame.png에 적용했다. 402×874 기준 버튼은(340,24),33×34, 타이머는(27.5,25.25),78×33.5로 원본 비율을 유지한다. 기존 숫자 마스크는 숨긴다.

실제 남은 시간은 기존 LabHud의 `MM:SS` 텍스트로 계속 갱신한다. 제공된 ShillaCultureBold.otf를 프로젝트에 포함하고 22pt로 사용한다. Bold 파일 자체를 사용하므로 합성 Bold와 자동 글자 크기 조정은 적용하지 않는다. 숫자가 포함된 예시 PNG는 고정 시간 표시용 에셋으로 사용하지 않는다. 재적용 메뉴는 `C6 Lab/Apply New Timer And Pause`다.

## PaintRatioConcept (#227)

사용자 제공 페인트 비율 PNG를 원본 보존했다. 원본에는 종이 바와 고정 `7.9%` 글자가 함께 들어 있으므로 가장 큰 연결 알파 영역인 바만 분리한다. 예시 빨간 영역 아래에는 인접한 종이 질감을 이어 빈 바(`PaintRatioEmpty.png`)를 만들고, 실제 진행률을 표현할 알파 마스크(`PaintRatioFillMask.png`)를 생성한다. 원본 검정 외곽선과 종이 질감을 유지한다.

`C6 Lab/Apply New Paint Ratio Bar`로 재적용한다. 402×874 기준 중심(192,118.5),232.5×29.5이며 오른쪽(315,104)의 퍼센트는 신라문화체 Bold21pt·검정색(#000000)으로 표시한다. 실제 도색 셀 수와 기존 채움 값을 사용하며, 찬 부분은 원본의 `#AF4738`이다. 새 Lab Scene은 `compactPaintProgress`로 숫자와 `%`만 표시한다. 기존 전체 목표 문구 포맷과 다른 Scene의 기본 경로는 유지한다. GOAL 제목·구형 프레임/트랙 덮개는 숨긴다.

## PlayerPosition (#227)

Figma `2527:7760`의 위치 표시는(323,402),75.5×71 영역이다. 제공된 세 PNG 원본을 보존했다. 기존 P1~P5 배지 에셋과 제공된 솔로 태극에서 분리한 아이콘을 조합해 실제 인원/선택 번호/북 주인을 표시한다. 본인은 아래, 실제 다음 P번호 이웃은 오른쪽 방향에 놓는 기존 전달 순서를 유지한다. 태극은 북 전달 시 이웃 선을 따라 이동하며 여러 북도 각각 추적한다. 솔로는 제공된 솔로 배지를 그대로 사용한다.

`C6 Lab/Apply New Player Position Map`으로 Scene의 LabDrumMinimap에 아트를 저장한다. 기존 Config 기본 Minimap Size50%에서 Figma 크기가 되도록 원본151×142를 사용하며 Config는 수정하지 않는다. DEV의 크기·이동·ON/OFF 조정은 유지한다. 박/배경/구슬 등 Figma의 다른 영역은 이 작업 범위에 포함하지 않는다.

## TemporaryBattleBackground (#227)

사용자 제공804×1162 배경 PNG를 그대로 보존한다. `C6 Lab/Apply Temporary Battle Background`로 BattleCamera 아래의 `Temporary Battle Background`에 적용한다. 조명의 영향을 받지 않는 SpriteRenderer를 카메라 뒤쪽 원경에 배치하고, 원본 비율을 유지한 채 화면 너비에 맞춰 상단 정렬한다. 넓은 화면에서는 빈 공간이 없도록 세로도 채운다. 카메라 시점·FOV·화면 비율 변경에 맞춰 갱신하며 박·줄·투척 구슬은 앞에 표시한다.

기존 BattleFloor는 MeshRenderer만 끄고 빗나간 투척의 Collider는 유지한다. 이후 3D 배경으로 교체할 때 `Temporary Battle Background`를 비활성화하거나 제거하면 된다. 3D 환경에서 기존 바닥이 필요하면 BattleFloor의 MeshRenderer를 다시 켠다. 물리·네트워크·Config 수치는 변경하지 않는다.

조합대 위 남색 띠를 가리기 위해 OrbCamera의 `Temporary Board Background`에도 같은 그림을 연결한다. 화면 상단·너비 및 BattleCamera의 최소 높이 채움 비율을 공유해 경계에서 그림이 이어지며, 조합대보다 뒤에 그린다. 두 배경은 이후 3D 배경 교체 시 함께 비활성화하거나 제거한다.

## 구슬 생성 북 (#227)

사용자 제공198×198 PNG를 기존 `Assets/Lab/Art/BottomHud/GenerateButton.png`에 원본 그대로 교체했다. 기존 meta/GUID와 LabOrbAppearance.drumSprite 연결을 유지한다. 런타임 북은 기존 visualRadius 지름에 맞춰 이미지를 표시하므로 원본 해상도가 커져도 게임 내 크기·충돌·생성·드래그·이웃 전달 동작은 유지한다. 미니맵의 태극 이미지는 별도 에셋이다.
