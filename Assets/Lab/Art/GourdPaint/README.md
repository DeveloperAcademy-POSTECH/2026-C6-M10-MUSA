# 기본 전투의 공동 색칠 목표·박 모드 — #175 / PR #176 보완

기본 실행·빌드 Scene은 계속 `Assets/Lab/Scenes/Lab.unity`다. #153에서 이관한 `Resources/GourdPaint/WhiteGourd.prefab`·`WhiteGourd.mat`·`Stem.mat`의 원본 참조와 GUID를 유지한다. 이번 변경은 누적 표면 코드·셰이더와 Host 승리 규칙을 바꾸며, 프리팹·원본 재질·기본 Config 에셋·UI 위치·빌드 Scene 목록은 색칠 목표 때문에 덮어쓰지 않는다. 박 외형과 실제 기존 원통 MeshCollider의 모양 차이는 유지된다.

## 모드·목표 설정

| 설정 | 기본값 | DEV 입력 | 적용 |
| --- | --- | --- | --- |
| Gourd Mode | Paint(0) | 80번째/인덱스 79, 정수0~1 | Paint는 공동 색칠, Crack(1)은 기존 HP 승리·크랙·마무리 |
| Paint Victory Coverage (%) | 80% | 88번째/인덱스 87, 정수 1~100 | Paint의 전체 구면 목표. Crack에서는 값만 보존하고 HP 승리에는 쓰지 않음 |

Inspector 원본은 `LabConfig`의 `Gourd Mode`·`Paint Victory Percent`다. 저장 필드가 없는 에셋은 코드 기본값 Paint·80%를 사용한다. 기존 87개 인덱스는 유지하고 88번째를 추가한다. **START/RESTART SOLO**, **CREATE ROOM**, Host 로비의 **APPLY TO ROOM**, 공동 일시정지한 Host의 **APPLY & RESTART**로 나머지 설정과 함께 검증·적용한다. Client는 Host의 설정을 받는다. Inspector·입력칸만 바꾸어 진행 중인 판에 즉시 적용하지 않는다. 프로토콜은 **5621**이며 모든 참가자가 같은 새 형식을 사용해야 한다.

## 실제 명중과 공동 승리

1. Host의 실제 투사체/기존 원통 충돌에서 표면 방향을 구한다.
2. `LabModel.ConfirmHit`가 시간 경과·현재 Playing·유효 비행 구슬·소유자를 확인한다. 같은 구슬을 소모하고 실제 HP 감소·공격자 보상·공동 색칠·승리를 한 처리에서 확정한다. 별도의 외부 Coverage 값을 받아 승리시키지 않는다.
3. Paint에서는 유효한 오행 명중만 `LabPaintSurface.Stamp`에 누적하고 목표 이상이면 전원의 Victory를 확정한다. 내부 요괴 HP 0은 승리 조건이 아니다. 무속성 릴레이/폭탄·빗나감·중복/이미 종료된 명중은 목표를 늘리지 않는다.
4. Crack은 기존 HP 0 승리와 약 1.2초 마무리를 유지한다. 두 모드 모두 시간 종료와 전원 개인 HP 0은 Defeat다. Host 공동 일시정지에서는 전투 시계·입력·물리가 멈추며, 재개 후 이어간다.
5. Snapshot의 `paintCoverage`·`paintGoalPercent`·phase가 공통 진행도·목표·결과의 기준이다. HUD의 Paint 숫자는 소수 첫째 자리까지 내림하고 막대는 진행도÷목표다. Crack에서는 기존 YOKAI HP/HP 비율을 표시한다.

`actualDamage`는 항상 실제 HP 감소량이다. Paint에서 HP 0 이후에도 승인 명중의 입자·소리·햅틱·보상을 유지할 수 있도록 별도 양수 `impactValue`를 전한다. Paint 결과 대시보드의 `damageTotal`은 기존 명목 공격값 점수이며 HP 실제 감소량/색칠 기여 면적이 아니다. Crack의 누계는 실제 HP 감소량이다. Paint는 피해 숫자를 숨기고 색칠·입자로 표현한다.

## 누적 표면과 렌더 연결

`LabPaintSurface`는 **256×128 = 32,768개**의 동일 면적 구면 셀과 premultiplied RGBA `Color32` 맵을 소유한다. 구면 로컬 방향을 `u = atan2(z,x)/(2π)+0.5`, `v = 0.5-normalizedY×0.5`로 매핑하므로 셀 하나마다 같은 구면 면적이다. 기존 불규칙 가장자리·부드러운 잉크·93% 덧칠을 CPU에서 누적하고, 알파가 0보다 큰 실제 저장 셀의 비율을 `Coverage`로 사용한다. 이미 칠한 셀을 중복 계산하지 않으며 뒤의 색이 앞의 색 위에 누적된다.

이전의 **128개 자국 배열/상한과 GPU 반복을 제거**했다. 같은 위치를 128회 맞힌 뒤에도 새 위치를 계속 칠할 수 있고, 렌더와 판정은 같은 고정 크기 맵을 사용한다. 다만 이는 화면 픽셀이나 실제 저해상도 Sphere 메시 삼각형의 면적을 측정한 값이 아닌 **양자화된 구면 진행도**다. 실제 브러시 반경은 `LabPaintSurface.DefaultRadius = 0.15`이며 피해 배율과 무관하다. `LabGourdPaint.splatRadius`는 연결 전 미리보기용 Inspector 값이다.

`LabGourdPaint`는 Host 모델 또는 Client 캐시의 `LabNetwork.PaintSurface`를 읽는다. Surface 객체/Revision이 바뀔 때만 같은 맵을 linear RGBA32 텍스처로 올리고 MPB `_PaintMap`·`_PaintMapReady`로 연결한다. 텍스처는 **Point, U Repeat/V Clamp, mipmap 없음**으로 셀 판정과 같은 범위를 표시한다. 기존 셰이더의 기본색·조명 계산은 유지한다. Renderer를 끄거나 프리팹/텍스처를 재생성해도 Host의 진행도는 지우지 않으며 복구하면 같은 맵을 다시 표시한다.

실제 연결 중 `Paint`/`ClearPaint`는 모델 상태를 바꾸지 않는다. 연결 전의 직접 `Paint`/`NotifyApprovedHit`는 미리보기·검사 fixture이며 실제 명중 증거가 아니다. View의 `OnHit`는 연결 중 이미 확정된 맵을 업로드할 뿐 다시 Stamp하지 않는다. 새 라운드/일반 Retry는 모드·목표를 유지하고 맵을 초기화하며, 로비 복귀·방 이탈도 Network/Model에서 상태를 초기화한다.

## Host/Client와 수정 지점

라운드 시작 시 Host가 구슬 외형의 오행 재질 색(`_BaseColor`→`_Color`→공통 팔레트)을 캡처한다. 기본색은 화=빨강·수=파랑·목=초록·금=노랑·토=갈색이다. 승인 피격 신호에 정규화된 색칠 방향과 Host RGBA를 포함한다. Client는 기존 송신자·세션·라운드·중복 검증을 통과한 뒤 캐시에 재생하고 독자적으로 승리를 정하지 않는다. 실제 Host/Client 맵 일치와 기기 성능은 검증 기록에 별도로 남긴다. 진행 중 재접속에서 과거 맵 전체를 복원하는 기능은 없다.

| 파일·API | 책임 |
| --- | --- |
| `Core/LabPaintSurface.cs` — `Stamp`, `Clear`, `CopyPixels` | 누적 데이터·CoveredCount/Coverage·Revision, 렌더와 같은 맵 |
| `Core/LabModel.cs` — `ConfirmHit`, `PaintSurface`, `PaintColorFor` | Host 명중·보상·목표·승리의 원자적 확정과 라운드 팔레트 |
| `Core/LabConfig.cs` — `PaintVictoryPercent`, `PaintVictoryCoverage` | 단일 설정 원본·DEV88번째·새 판 Rules 입력 |
| `Session/LabNetwork.cs` — `PaintSurface`, 승인 피격/설정·Snapshot 경로 | Host 모델/Client 캐시, 방향·RGBA 재생, 공통 진행도·목표·결과 |
| `Throw/LabGourdPaint.cs` — `Attach`, `ApplyMode`, `Surface`, `PaintTexture` | 외형 선택·안전 초기화·누적 맵 표시. 모델 진행도 삭제 금지 |
| `Art/GourdPaint/LabGourdPaint.shader` | 누적 맵 1회 샘플링과 기존 조명 |
| `UI/LabHud.cs` — `PaintGoalPercentFor`, `FormatPaintGoalProgress`, `PaintGoalFill` | Snapshot 목표 우선, 내림 숫자·목표 대비 막대 |
| `LabSceneController`·`ConfigApplied`, `LabGourdCrack`·`LabGourdFinish` | 기존 설정 적용 경로와 Crack의 독립된 HP 표현/마무리 |

## 검증을 읽는 법

원본 PR #176의 EditMode123/123·PlayMode95/95, #166의 과거 PASS는 당시 버전의 기록이다. 새 누적 맵·설정88개·원자적 승리·방향/RGBA 패킷의 통과로 승계하지 않는다. 현재 컴파일·EditMode·PlayMode의 실제 결과는 `docs/VALIDATION.md`의 보완 기록을 따른다. 이 문서 자체는 실행 PASS를 부여하지 않는다. 실제 다중 기기·핫스팟·iOS 빌드/조작/성능은 이번 자동 검사와 구분하며 실기기 미실행으로 남긴다.
