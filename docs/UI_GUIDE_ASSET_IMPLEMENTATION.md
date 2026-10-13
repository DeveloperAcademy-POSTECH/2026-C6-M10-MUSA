# Figma 전투 UI 이식·유지보수 기록

기준: 이슈 #101, Unity 6000.6.5f1, `Assets/Lab/Scenes/Lab.unity`. 디자인 원본은 [Figma `UI 가이드` 프레임](https://www.figma.com/design/wW7ny7lNQ4DXNJ4kD46AuR/?node-id=2005-13394)이다. 이 문서는 현재 Lab 전투 화면을 그 가이드에 맞추는 UI 작업의 **에셋·데이터 연결**을 설명한다. 실행 통과 여부는 [검증 기록](VALIDATION.md)에만 적는다.

## 에셋과 화면의 대응

Figma의 UI 원본 구성요소를 투명 `FigmaUiAtlas.png`에 담고 필요한 영역을 **무손실로 잘라낸 개별 PNG**를 만든다. 에디터 업그레이더는 이 PNG들을 Sprite로 가져와 Scene Image에 연결한다. Atlas 자체는 출처용 원본이다. 조합대는 별도 투명 `BoardPlate.png`를 3D 재질에 연결한다. 원본 프레임 전체를 한 장으로 쓰지 않는다. 개별 프레임을 그대로 내보내면 알파 채널이 있어도 배경 픽셀이 불투명하므로, 최종 Sprite의 외곽 알파 픽셀까지 확인한다. 원본 Atlas의 배경 풍경·구름은 Scene UI에 배치하지 않으며 요괴·플레이어 캐릭터 그림도 이번 이식 대상에서 제외한다.

| Figma 요소 | 저장 에셋 | Unity 표시·값 연결 |
| --- | --- | --- |
| 조합대 (`2005:14930`) | `BoardPlate.png` | 기존 3D 조합판의 시각 재질에 사용한다. 구슬의 Collider/Rigidbody·이동 범위·입력 판정은 유지한다. |
| 일시정지 (`2005:13409`) | `PauseButton.png` | 전투 중 버튼. Client도 요청할 수 있지만 실제 정지는 Host의 승인 후 스냅샷으로 표시한다. |
| 제한시간 (`2005:13410`) | `TimeFrame.png` | `LabHud.timeText`가 Host 스냅샷의 남은 초를 `mm:ss`로 표시한다. 원본 그림에 들어 있는 고정 `03:00`은 동적 숫자에 남지 않게 가린다. |
| 요괴 HP (`2005:13422`) | `YokaiHpFrame.png`, 기술용 `SolidFill.png` | `LabHud.hpText`와 `yokaiHpFill`이 현재 HP/설정 최대 HP를 표시한다. 채움은 `Image.fillAmount`가 변하는 별도 Sprite다. |
| 내 HP (`2005:19362`) | 같은 `YokaiHpFrame.png`·`SolidFill.png` | 같은 프레임을 재사용한다. 원본의 `구미호` 문구는 가리고, `LabHud.playerHpText`/`playerHpFill`에 **로컬 플레이어의 HP**만 연결한다. |
| 스태미나 (`2005:19866`, `2005:20022`) | `StaminaBar.png`, `StaminaBall.png`, `StaminaSocket.png` | 첫 파란 볼은 Figma 장식 마커로 두고, 뒤의 다섯 칸은 `LabHud.staminaCells`가 현재/최대 스태미나의 비율을 반영한다. 기본 100·생성 비용 20 규칙은 바꾸지 않는다. |
| 구슬 생성 (`2005:20026`) | `GenerateButton.png` | 기존 `LabHud.generateButton`을 통해 `LabNetwork.Generate()`를 호출한다. |
| 좌우 이웃 (`2005:13417`, `2005:13418`) | `NeighborBadge.png`와 동적 Unity Text | `LabHud.GetNeighborLabels`가 스냅샷의 `seat` 순서로 양옆 플레이어를 찾고 현재 방의 `seat + 1`을 P번호로 표시한다. 재입장으로 네트워크 ID가 커져도 2인 방은 P1·P2다. 이름·속성 데이터는 추가하지 않는다. |

디자인은 세로 프레임 **402×874**이며 실제 게임의 CanvasScaler 기준은 **390×844**이다. Inspector의 RectTransform으로 기기별 겹침을 조정한다. 판은 3D 구슬보다 뒤에, 전투 버튼·막대·정지 메뉴는 조작 가능한 UI 계층에 둔다. 가이드 위치를 이식하더라도 조합판 물리 영역의 위/아래·좌/우 판정 경계를 이미지 크기만 보고 변경하지 않는다.

## 런타임 연결

`LabHud`는 저장 Scene의 직렬화된 참조를 사용한다. `LabNetwork.Snapshot`에 전투 또는 결과 상태가 있으면 전투 UI를 표시하고, 확정 `hp`, `remaining`, 로컬 `personalHp`, 로컬 `stamina`, 참가자 `seat`/`id`를 매 프레임 표시용 값으로 읽는다. 승리·패배에서는 마지막 확정 HP·시간·스태미나가 남는다. 시간 표시는 남은 초를 올림해 두 자리 분·초로 나타낸다. HP 채움은 현재값/최대값 비율이며, 다섯 스태미나 칸은 같은 비율을 순차적으로 보여준다. 값의 원본은 세션에 승인된 `LabConfig`와 Host 스냅샷이고, 그림이나 UI Text가 게임 수치를 결정하지 않는다.

현재 Lab에는 플레이어 HP를 깎는 실제 요괴 공격 이벤트가 없다. 내 HP 바가 연결되어 있어도 일반 전투에서는 시작값으로 가득 차 있는 것이 기본 동작이다. DEV에서 최대 HP를 바꿔 새 판의 시작 수치를 확인할 수 있으며, 피해 연출이 이미 완성됐다고 해석하지 않는다.

일시정지 버튼은 `LabNetwork.PauseBattle()`을 호출한다. Host는 즉시, Client는 요청을 Host에 보낸 뒤 Host의 스냅샷을 기다린다. 승인된 `paused` 상태는 모든 화면의 정지 메뉴와 입력 가능 상태에 반영한다. Host만 `ResumeBattle()`과 `ReturnAllToLobby()`를 실행한다. 로비 복귀는 네트워크 방과 참가자 연결을 유지하며 전투 오브젝트를 정리한다. 개발 빌드의 Host는 정지 메뉴에서 `LabDeveloperMode`를 열어 새 설정을 입력하고 `RestartBattleWithDeveloperConfig()`를 호출할 수 있다. Host가 설정을 전파하고 참가자가 확인한 뒤 **새 판**으로 시작한다. 원본 `LabConfig.asset`은 수정하지 않는다.

기본 코드 연결은 `Assets/Lab/Scripts/UI/LabHud.cs`, `Assets/Lab/Scripts/UI/LabDeveloperMode.cs`, `Assets/Lab/Scripts/Session/LabNetwork.cs`, `Assets/Lab/Scripts/Core/LabContracts.cs`이다. Scene 구조·머티리얼·Sprite import에 관한 구체 상태는 Unity Editor에서 저장 Scene을 로드해 Inspector 참조를 확인해야 한다. `Assets/Lab/Art/UI/`의 PNG 파일 존재만으로 화면 이식을 통과 처리하지 않는다.

## Unity에서 위치를 고치는 곳

에디터 전용 `Assets/Editor/Bootstrap/LabFigmaUiUpgrade.cs`의 **C6 Lab → Apply Figma UI Guide**는 기존 저장 Scene을 바탕으로 UI 객체를 만들거나 이동시키고 직렬화 참조를 지정하는 일회성 이식 도구다. 이 도구를 다시 실행하면 가이드 기준 위치와 크기가 재적용되므로, 배치를 직접 조정한 뒤에는 의도 없이 재실행하지 않는다.

Scene 변환 후에는 `LabCanvas/FigmaBattleUi` 아래에서 `FigmaTimeFrame`, `FigmaYokaiHpBar`, `FigmaPlayerHpBar`, `FigmaStaminaBar`, `FigmaPauseButton`, `FigmaGenerateButton`, `LeftNeighborBadge`, `RightNeighborBadge`의 RectTransform을 각각 바꾼다. `LabCanvas/FigmaPauseMenu`에는 정지 상태의 버튼이, `LabCanvas/FigmaResultControls`에는 승리·패배 결과 문구와 버튼이 있다. 결과에서 마지막 HP·시간·스태미나를 남기고, 기존 로비 패널은 숨긴다. `OrbBoardPlate`는 별도의 3D MeshRenderer이므로 Canvas의 순서만으로 조합대의 깊이가 바뀌지 않는다. 색·Sprite·폰트·배치 수정은 이 계층의 Inspector에서 하고, `LabHud` 참조 필드를 지우지 않는다. 저장 Scene의 최종 계층과 렌더 결과는 Unity에서 확인해야 한다.

| 바꾸고 싶은 부분 | 저장 Scene에서 확인할 자식 |
| --- | --- |
| 숫자 시계 | `FigmaTimeFrame/ClockValueMask`와 같은 프레임의 `BattleTimeText` |
| 두 HP 막대 | 각 `FigmaYokaiHpBar`, `FigmaPlayerHpBar`의 `TrackMask`, `DynamicFill`, `TitleMask`와 기존 `YokaiHpText`·`PlayerHpText` |
| 스태미나 | `FigmaStaminaBar/StaminaSocket1~6`은 빈 홈, `StaminaMarker`는 고정 표시, `StaminaCell1~5`는 값에 따라 채워지는 칸 |
| 좌우 이웃 | `LeftNeighborBadge/PNumber`, `RightNeighborBadge/PNumber` |
| 생성과 정지 | `FigmaGenerateButton`은 투명 터치 영역이고 자식 `FigmaGenerateVisual`이 실제 그림, `Label`이 문구다. 그림의 Raycast는 꺼져 있어 버튼 터치를 받는다. 정지는 `FigmaPauseButton`, 메뉴는 `LabCanvas/FigmaPauseMenu`의 세 동작 버튼 |
| 승리·패배 | `LabCanvas/FigmaResultControls`의 결과 문구·재시작·같은 방 로비 복귀·나가기 버튼 |
| 구슬 아래 조합대 | `OrbBoard/OrbBoardPlate` MeshRenderer의 `Assets/Lab/Art/UI/FigmaBoardPlate.mat` |

## 직접 확인할 순서

1. Unity 6000.6.5f1에서 `Lab.unity`를 열고 `LabCanvas`의 전투 요소 및 `LabHud` 참조 누락을 확인한다. 플레이 전 화면에는 전투 HUD가 보이지 않아야 한다.
2. 솔로 또는 공유 방 전투를 시작한다. `03:00`이 실제 남은 시간으로 갱신되는지, 요괴/개인 HP의 정적 문구가 겹치지 않는지, 스태미나 100에서 생성 한 번 뒤 다섯 칸이 비율대로 변하고 회복하는지 확인한다.
3. 2인 방에서 양쪽 P번호가 같은 상대를 가리키는지, 솔로에서는 `P—`인지 확인한다. 더 많은 인원의 경우 자리의 좌우에 해당하는 번호인지 확인한다.
4. 참가자에서 정지를 요청하고 Host가 승인한 뒤 양쪽의 시간·구슬/폭탄 수명·물리 움직임이 멈추는지 확인한다. Host가 재개하면 이전 남은 시간에서 다시 진행해야 한다.
5. Host가 **모두 로비로**를 누르면 참가자 연결과 방 이름이 유지되는지 확인한다. 다시 시작하거나 DEV 설정을 바꾼 새 판을 열었을 때 양쪽에 동일 설정이 적용되는지도 확인한다.

위 항목은 **검증 절차**이며, 실행 전에는 PASS가 아니다. 현 브랜치의 실제 결과는 [검증 기록](VALIDATION.md)을 따른다.
