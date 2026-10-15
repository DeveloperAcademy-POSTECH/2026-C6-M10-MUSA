# 박 페인트 (#153)

본 게임(`Assets/Lab/Scenes/Lab.unity`)의 요괴 자리에 **흰 박**을 놓고, 구슬이 맞을 때마다 그 자리에 구슬 속성 색을 칠한다.
따로 장면을 고르지 않아도 기본 빌드(**C6 Lab → Export iOS Xcode Project / Build Mac App**)에 그대로 들어간다.

- 색: Fire=빨강, Water=파랑, Wood=초록, Metal=노랑, Earth=갈색. 구슬 외형 설정의 속성 재질 색(없으면 공통 기본 색)을 쓴다. 속성이 없는 타격은 칠하지 않는다.
- Host가 승인한 타격(`LabTarget.ApprovedHit`)만 칠한다. 피해·승패·네트워크 패킷은 바꾸지 않는다.
- 칠한 곳 위에 덧칠할 수 있고, 박이 흔들리거나 돌아도 자국은 표면에 붙어 있다.
- 새 라운드나 로비로 돌아오면 칠이 지워진다. 한 라운드 최대 128개 (기본 HP 1000 · 피해 20이면 약 50개).

## 구성

| 파일 | 역할 |
| --- | --- |
| `Assets/Lab/Scripts/Throw/LabGourdPaint.cs` | 흰 박 붙이기, 승인된 타격 위치를 박 표면에 투영해 색 자국 기록 |
| `Assets/Lab/Art/GourdPaint/LabGourdPaint.shader` | 자국 위치·색을 받아 박 표면에 그리는 URP 셰이더 (`C6Lab/GourdPaint`) |
| `Assets/Lab/Resources/GourdPaint/` | 흰 박 프리팹 `WhiteGourd`, 페인트 재질 `WhiteGourd`, 꼭지 재질 `Stem` |
| `Assets/Lab/Scripts/UI/LabSceneController.cs` | 게임 시작 시 `LabGourdPaint.Attach`로 붙인다 (장면 파일 수정 없음) |

## 크랙 박(#163)과의 관계

`Lab.unity`에는 #163의 박 모델·크랙·마무리 연출(`LabGourdCrack`, `LabGourdFinish`)이 남아 있지만, 같은 표적에 박 모델을 붙이므로 **페인트를 쓰는 동안 시작 전에 꺼 둔다.** 그래서 금과 처치 마무리 연출은 나오지 않는다.
크랙 박으로 되돌리려면 `LabGourdPaint.EnabledInMainScene = false` (크랙 테스트가 이 방법을 쓴다). 실제 박 모델 위에 페인트와 금을 함께 보이려면 두 셰이더를 합치는 작업이 필요하다.

## 확인

- PlayMode `LabGourdPaintTests`: 본 장면에 흰 박이 붙고 크랙이 꺼지는지, 승인 타격 칠하기·속성 없는 타격 무시·회전 후 좌표 유지·잘못된 좌표 거부·128개 한도
- 사용자 손 입력, 실제 다중 기기 표시, iOS 성능은 기기에서 직접 확인
