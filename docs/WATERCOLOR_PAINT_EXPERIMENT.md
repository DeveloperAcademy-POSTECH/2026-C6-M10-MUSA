# 박 표면 수채 질감 비교 — #369

## 현재 범위

`C6 Lab > Experiments > Watercolor Paint`에서 여는 **Editor 전용 파란색 정적 비교 도구**다. 실제 전투에는 아직 연결하지 않았으며 번짐 애니메이션도 없다. 시안 확인 뒤 군집/동기화 연결과 짧은 번짐을 진행한다.

저장된 WhiteGourd Prefab과 실제 불투명 `C6Lab/GourdPaint` 셰이더를 격리된 Preview Scene에서 사용한다. 원본 Prefab·재질·도색 패턴·Scene·LabConfig는 저장하거나 변경하지 않는다. 파란 수채 레퍼런스에서는 농담·중첩·입자·부분적인 가장자리 농축을 참고하며 실루엣과 흰 배경은 복제하지 않는다.

## 실행과 비교

1. 위 메뉴를 열면 왼쪽 CURRENT, 오른쪽 CANDIDATE가 표시된다.
2. `Re-hits after first`는 최초 명중 뒤 0~3회 재명중을 고른다. seed와 `Hit location`은 양쪽에 같은 조건을 적용한다.
3. `Hit location`으로 정면·경도 이음새·극의 타격 위치를 고른다. Yaw/Pitch/Zoom과 Rotate 버튼은 같은 박의 시점만 바꾼다.
4. 후보의 8개 슬라이더로 농도·넓은 농담·중간 얼룩·미세 입자·입자 크기·가장자리 강도/폭·질감 크기를 조절한다. 변경 이벤트를 100ms로 묶어 렌더링하며 매 프레임 갱신하지 않는다.
5. `Export comparison PNG`로 현재 좌우 비교를 저장한다. 출력물은 실제 박의 Editor 렌더이며 실제 플레이·실기기 캡처가 아니다.

초기 후보값은 density .62, broadVariation .55, mottle .38, grain .35, grainScale 1, edgeStrength .6, edgeWidth .022, textureScale 1이다. 표시 전용 값이며 게임 도색량과 무관하다.

![최초 명중, 왼쪽 기존 / 오른쪽 후보](images/paint-watercolor-369-first.png)

![재명중 3회, 왼쪽 기존 / 오른쪽 후보](images/paint-watercolor-369-repeat3.png)

## 코드와 데이터 경계

- `Assets/Editor/WatercolorPaint/LabWatercolorPaintWindow.cs`: UI Toolkit 비교 창·조절·이미지 내보내기. 창 종료/재생성 시 이미지를 해제한다.
- `LabWatercolorPaintStudy.cs`: 저장 설정의 별도 `LabPaintGrowthSettings`로 실제 `LabPaintSurface.RegisterHit/Advance`를 재생한다. 정지 화면 비교를 위해 **이 복사본에서만** Drying을 끈다. 비교 입력은 Water, P1, seed123, 최초1회+재명중0~3회다. 원래 성장량·군집·가중치를 바꾸지 않는다.
- 현재 기준 화면은 실제 `LabHighResolutionPaintRuntime`의 1024×512 결과다. 구형 셀 지도나 이전 Editor 고해상도 후보를 CURRENT로 사용하지 않는다.
- 후보는 같은 확정 표면의 `LabPaintVisualStamp`를 재생해 별도 optical RT를 만든다. 원본 Splatter의 내부 거리 복사본으로 각 얼룩의 가변 폭/강도 가장자리 흔적을 누적한다. 표면 로컬 방향의 미세 결과 얼룩 좌표의 농도 변화를 분리한다.
- `LabWatercolorPaint.shader`: R/B에 크기가 다른 농도 기여, G에 개별 가장자리 기여를 제한된 UNorm 데이터로 쌓는다. 최종 색상/지원 영역은 현재 런타임 결과에서 가져오며 후보 RGB는 premultiplied다. 반복적인 검정 RGB 혼합을 하지 않는다.
- 후보를 다시 만들 때 optical RT를 지우고 같은 기록을 한 번 재생하므로 이전 프레임에 중복 덧칠하지 않는다. 이 방식은 정적 비교용이며 활성 번짐/실시간 배치 처리를 구현한 것이 아니다.
- 원본 브러시는 수정하지 않고 Resources 소유 에셋으로 유지한다. 전용 Material·내부 거리 Texture·색/P 복사본·2개 추가 RT는 Study가 소유/해제한다. 기본 비교의 추가 RT 원시 픽셀은 2×1024×512×4바이트=4MiB이며, 전체 GPU/프로세스 메모리 실측값이 아니다.

## 다음 단계에서 보존할 제약

- Host 셀 진행도·100% 완료·게임의 마르기(셀 삭제)·2:2 덮어쓰기·P지도·프로토콜을 유지한다.
- 최근128개 기록과 확정 지도 fallback을 유지한다. 모든 과거 타격의 개별 경계 영구 보존을 보장하지 않는다.
- 현재 파란색 한 종류만 비교하므로 다른 속성의 혼합/소유권 gate를 실제 전투에 적용했다고 간주하지 않는다.
- 기존 런타임 지원 영역을 그대로 사용하며, 표시가 연해도 미도색 셀로 판정하지 않는다.
- 짧은 번짐은 시안 확인 후 별도 단계다. 기존 확정 결과와 활성 표현을 분리하고 한 얼룩의 최종 누적은 한 번만 수행할 계획이다.
- 신생/복원 기록 구분·피버 연속/동시 타격·최종 관람/PNG·iPhone 성능은 후속 단계에서 검증한다.

## 검증 재현

실제 GPU가 있는 Editor에서 `C6Lab.Experiments.Editor.LabWatercolorPaintValidation.Export(절대 출력 폴더)`를 호출한다. 정면 최초/재명중1·3회와 이음새/극 재명중3회, 총5개 경우의 지도 검사와 PNG를 만든다. 원본 에셋·판정 상태·Scene 보존, 반복 재생의 동일 결과, 표시 영역/색상, 리소스 해제를 확인한다. 숫자 검사 통과는 미술 품질 승인이나 실제 전투 검증을 대신하지 않는다. 실제 수행 기록은 [VALIDATION](VALIDATION.md)의 #369 절을 따른다.
