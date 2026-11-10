# 폭탄 시각 부품

`Assets/Lab/Art/BombVisual/BombVisual.prefab`은 그림만 가진 프리팹이다. Lab의 `LabOrbAppearance.asset`과 Hapio 전투 테마의 `Assets/Hapio/Resources/Hapio/UI/BattleOrbAppearance.asset` 양쪽 `bombVisualPrefab`에 연결하며, `LabOrbItemMarker`가 기존 표시 상태를 전달한다. Rigidbody·Collider·전달/해금/만료 판정은 기존 구슬 루트와 모델에 남는다. 프리팹을 비우면 이전 폭탄 그림 경로를 사용한다.

## 에셋과 배치

원본은 제공된 `BombVisual-Latest.zip`의 Parts이며, Figma `wW7ny7lNQ4DXNJ4kD46AuR`의 `4434:357`과 `Unity-layout.json`을 따른다. Parts의 PNG 8개와 SVG는 원본 그대로 보관한다. 130×130 좌표 중 몸체 영역 (0,26,104,104)을 단위 너비로 정규화한다. 꼭지는 (52,16,56,56)에 놓고 Figma의 내부 그림 배율인 가로 1.2배(67.2×56)를 적용한다. 심지는 (73,16,48,32), 불꽃은 (91,7,38,38), 숫자는 몸체와 같은 영역이다.

심지 PNG와 SVG에는 흰 배경이 포함되어 있다. `BombFuse.mat`·`BombFuse.shader`는 원본 파일을 바꾸지 않고 그릴 때 흰 배경을 제거하고, SVG의 동일한 cubic 좌표를 기준으로 소진된 끝을 잘라낸다. Sprite Atlas에는 넣지 않는다.

## 기존 표시 상태 연결

- 숫자 PNG는 기존 `countText`(남은 해금 전달 횟수)의 4·3·2·1만 표시하고 0/빈 문자열은 숨긴다. 숫자와 몸체는 회전하는 꼭지 계층 밖에 두어 숫자를 똑바로 유지한다.
- 심지는 기존 `SetFuse`의 `ropeShare`를 그대로 받는다. 기존 `(fuseLeft - warning) / (fuseTotal - warning)` 계산과 마지막 경고 구간 진입 시점은 변경하지 않는다. 새 타이머나 모델 상태를 만들지 않는다.
- 꼭지와 심지는 기존 `BombTurn`·`CapDirection`·깊이 투영값을 사용한다. 심지 끝 좌표에 새 불꽃 그림을 붙이고, 기존 소진 비율에 맞춰 그림 크기만 줄인다. 원래 불꽃의 noise clock·회전·투명도 변화를 재사용한다.
- 경고 점멸은 기존 tint를 몸체와 꼭지에 적용한다. 해금 연기·무지개 링·공격 구슬 2개 전환·시간 초과 폭발 경로는 변경하지 않는다.
- 전투 첫 사용 렌더 준비에는 프리팹 복사본을 추가한다. 실제 구슬·모델·판정에는 연결하지 않는다.

실행 결과와 미확인 범위는 [검증 기록](VALIDATION.md)의 #584 항목을 따른다.

Hapio 실제 전투에서는 `GameBattleUiPresentation.ApplyOrbAppearance()`가 테마의 별도 외형으로 덮어쓴다. Lab 외형만 갱신하면 실제 전투에는 적용되지 않으므로 두 연결을 함께 확인한다.
