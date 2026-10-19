# 기본 전투의 100% 공동 색칠·인접 성장·완성 박 관람 — #187·#191

> **Experiment #196**: 기존 판정 맵 위에 물감 내부 입자·농담을 표시하는 선택 표현이다. 기본 OFF이며 DEV → Gourd → Paint Grain에서 켠다. 원본 도색·성장·완료 규칙은 유지하고 로컬 좌표 고정 RGB 명암만 추가한다. 설정129~132를 추가한 #196은 사용자 승인으로 main에 병합됐으며 기본 OFF를 유지한다. 세부 조절값과 검증은 루트 `README.md`·`docs/DEVELOPER_MODE_PARAMETER_SPEC.md`·`docs/VALIDATION.md`의 #196 항목을 따른다.

기본 실행·빌드 Scene은 `Assets/Lab/Scenes/Lab.unity`다. #153에서 이관한 `Resources/GourdPaint/WhiteGourd.prefab`·`WhiteGourd.mat`·`Stem.mat`의 원본 참조와 GUID, UI 배치와 빌드 Scene 목록을 유지한다. 박 외형과 실제 기존 원통 MeshCollider의 모양 차이도 유지한다. 단일 원본 `LabConfig.asset`은 Unity에서 목표 100과 성장 기본값 6개를 저장했으며 다른 설정의 유효값을 보존했다.

## 모드·고정 목표·성장 설정

| 설정 | 기본값 | DEV 입력 | 적용 |
| --- | --- | --- | --- |
| Gourd Mode | Paint(0) | 80번째 · 인덱스 79 · 정수 0–1 | Paint는 전체 셀의 공동 색칠, Crack(1)은 기존 HP 승리·크랙·마무리다. |
| Paint Victory Coverage (%) | 100% | 88번째 · 인덱스 87 · 정수 100–100 | 기존 이름과 위치를 유지한 고정 목표. 읽기 전용 입력칸에 100을 표시하고 슬라이더는 비활성이다. |

성장 6개는 **DEMO_TUNING_VALUE**로 Inspector `LabConfig.asset`의 Gourd 항목 또는 DEV의 `Gourd` 묶음에서 조정한다. 기존 0~104번 인덱스와 미니맵·북·박 폭탄·음향 설정은 보존하고 끝에 추가했다.

| DEV 입력 이름 | 위치 | 기본값 | 입력 범위 | 효과 |
| --- | --- | ---: | --- | --- |
| Paint Surface Resolution (0 low 1 medium 2 high) | 106번째 · 인덱스 105 | 2 | 정수 0–2 | 0: 64×32, 1: 128×64, 2: 256×128 동일 면적 구면 셀. 새 판에서 적용한다. |
| Paint Initial Coverage (%) | 107번째 · 인덱스 106 | 2 | 0.1–100% | 안 칠한 지점에 처음 맞혔을 때 즉시 칠하는 초기 면적. 전체 셀 수를 기준으로 하며 셀 단위로 양자화한다. |
| Paint Repeat Growth (%) | 108번째 · 인덱스 107 | 4 | 0.1–100% | 이미 칠한 지점에 맞히면 연결된 영역에 추가하는 성장 면적 예산. 기존 셀 색은 바꾸지 않는다. |
| Paint Growth Speed (%/s) | 109번째 · 인덱스 108 | 6 | 0.1–100%/초 | 모든 영역이 함께 나눠 쓰는 전체 표면 기준 성장 속도. 영역 수만큼 속도가 늘지 않는다. |
| Paint Fill Chance (%) | 110번째 · 인덱스 109 | 60 | 0–100% | 이웃이 둘 이상 칠해진 빈 셀을 우선 채울 확률. 해당 후보가 없으면 다른 인접 후보로 진행한다. |
| Paint Vertical Weight | 111번째 · 인덱스 110 | 1.35 | 1–4 | 세로 방향 인접 후보의 선택 가중치. 1은 가로와 같은 가중치다. |

옛 에셋의 직렬화 목표 80은 `PaintVictoryPercent`·`CaptureDeveloperValues`에서 100으로 정규화한다. DEV/공유 설정으로 100 외 목표를 보내면 전체 설정을 거절한다. **START/RESTART SOLO**, **CREATE ROOM**, Host 로비의 **APPLY TO ROOM**, 공동 일시정지한 Host의 **APPLY & RESTART**로 나머지 설정과 함께 검증·적용한다. 새 판의 Rules는 `PaintGrowthSettings` 복사본을 캡처하므로 Inspector·입력칸만 바꿔 진행 중인 판에 적용하지 않는다. 현재 패턴 실험의 공유 설정은 총 **142개**, 프로토콜은 **5636**, 연결 토큰은 `C6PhysicsLab/11`이며 모든 참가자가 같은 새 버전을 사용해야 한다.

## 실제 명중과 공동 승리

1. Host의 실제 투사체와 기존 원통 충돌에서 구면 로컬 방향을 구한다.
2. `LabModel.ConfirmHit`가 전투 시간·Playing·유효 비행 구슬·소유자를 확인하고 구슬 소모·실제 HP 감소·공격자 보상·색칠 공급을 한 처리에서 확정한다. 외부 Coverage 입력으로 승리를 만들지 않는다.
3. Paint의 유효 오행 명중만 `LabPaintSurface.RegisterHit`로 초기 영역 또는 성장 예산을 만든다. Host 시간 진행에서 `Advance`가 빈 인접 셀을 채우며 `CoveredCount == CellCount`이면 전원이 Victory를 받는다. 내부 요괴 HP 0은 Paint의 승리 조건이 아니다. 무속성 릴레이/폭탄·빗나감·중복·종료 뒤 명중은 색칠을 늘리지 않는다.
4. Crack의 기존 HP 0 승리·약 1.2초 마무리, 공통 시간 종료·전원 개인 HP 0 Defeat를 유지한다. Paint 결과와 패배에는 별도 연출 지연이 없다.
5. HUD는 항상 목표 100%를 표시한다. 셀 수가 있는 Snapshot은 `paintCoveredCount / paintCellCount`를 사용하고 숫자를 소수 첫째 자리까지 내림해 미완료를 100%로 표시하지 않는다. Paint는 **GOAL**, Crack은 기존 **YOKAI HP**다.

`actualDamage`는 실제 HP 감소량이다. Paint에서는 내부 HP 0 뒤에도 승인 명중 입자·소리·햅틱·보상을 유지하도록 별도 양수 `impactValue`를 전한다. Paint 결과의 `damageTotal`은 기존 명목 공격값 점수로 유지하고 실제 HP 감소량이나 색칠 기여 면적과 구분한다. Crack은 실제 감소량을 누적한다. Paint는 피해 숫자를 숨기고 색칠·입자로 표현한다.

제한 시간 전에 마지막 셀이 완성되면 공동 Victory가 되고, 마지막 성장 시각과 제한 시간 종료가 정확히 같으면 Defeat가 우선한다. 늦게 도착한 Tick도 제한 시간 전에 완료될 성장은 해당 시각까지 계산한다. 공동 일시정지 동안 성장·전투 시계·입력·물리는 멈추며 재개 뒤 이어간다. 일반 Retry는 모드와 캡처한 성장 설정을 유지하면서 맵·영역·공급 예산·명중 수·난수 진행을 초기화한다. 새 DEV 값은 다음 시작/재시작에 캡처하며 진행 중인 판에는 적용하지 않는다. 로비 복귀와 방 이탈도 표시·수신 상태를 정리한다.

## 완성된 박 관람 (#191)

| DEV 입력 이름 | 위치 | 기본값 | 입력 범위 | 적용 |
| --- | --- | --- | --- | --- |
| Completed Gourd Viewer (0 off, 1 on) | 112번째 · 인덱스 111 · Gourd | OFF(0) | 정수 0–1 | Host가 전원의 관람 허용을 공유한다. 열기·회전·확대/축소는 각자 로컬에서 한다. |

`LabConfig.CompletedGourdViewerEnabled`는 단일 원본의 bool이며 저장 필드가 없는 에셋도 false다. 기존 0~110번과 111번째 `Paint Vertical Weight`를 보존한다. 142개 배열 전체를 검증하므로 잘못된 bool 입력과 구형 141개 이하 배열은 원자적으로 거절한다. **START/RESTART SOLO**, **CREATE ROOM**, Host 로비의 **APPLY TO ROOM**, 공동 일시정지한 Host의 **APPLY & RESTART**에서 기존 지문·참가자 확인 응답과 함께 적용한다. 진행 중인 판에 즉시 바꾸지 않는다. 일반 Release 참가자도 Host 허용값을 따르며 관람에 별도 Debug·실행 인자·Editor 메뉴 조건을 요구하지 않는다.

ON인 Paint **100% Victory**에서 Snapshot과 공개 표시 맵의 세션·라운드·해상도·Revision·칠한/전체 셀 수·명중 수가 일치해야 관람 버튼이 나타난다. Crack·Defeat·분할 수신 중 맵에는 나타나지 않는다. 필요한 결과에서 `Assets/Lab/Resources/GourdResultViewer/ViewerUI.prefab`을 불러오며 드래그 회전·핀치/마우스 휠 확대·축소·**RESET VIEW**는 각자 화면에만 적용한다. 불투명 모달은 아래 결과 버튼 입력을 막는다.

관람은 확정된 색칠 맵을 별도 카메라·텍스처와 복사한 박 표현으로 표시하며 본 전투의 맵·판정·점수·카메라·물리를 변경하지 않는다. **BACK TO RESULTS**로 닫으면 미리보기 자원을 해제한다. 일반 Retry는 허용값을 유지하지만 관람창과 이전 미리보기를 닫고 새 결과를 기다린다. 새 설정 적용·로비 복귀·연결 종료·모드 변경도 이전 UI·미리보기 자원을 정리한다. 저장 Scene의 배치는 유지한다.

## 성장 표면과 색 보존

`LabPaintSurface`는 해상도에 따라 64×32(2,048셀), 128×64(8,192셀), 256×128(32,768셀)의 동일 면적 구면 `Color32` 맵을 소유한다. `u = atan2(z,x)/(2π)+0.5`, `v = 0.5-normalizedY×0.5`로 방향을 셀에 대응하며 전체 표면의 셀별 면적을 같게 잡는다. 권위 성장 맵의 알파는 0 또는 255다.

유효한 오행 명중이 안 칠한 셀에 닿으면 초기 면적을 즉시 칠하고, 이미 칠한 셀에 닿으면 그 연결 영역에 반복 성장 면적을 공급한다. 이후 Host 전투 시계가 진행될 때 미사용 예산을 가진 영역들이 전체 표면 기준 `%/초` 속도를 나눠 쓰며 빈 인접 셀로 자란다. 시간이 흐르는 동안 공급 예산이 없으면 자라지 않으며, 그 대기 시간의 성장량을 미리 적립하지 않는다. 여러 영역은 차례대로 성장하고 서로 닿으면 하나로 병합된다.

이미 칠한 셀의 색은 덧칠하거나 병합할 때 바뀌지 않는다. 명중별 면적 공급은 각 연결 영역 안에서 먼저 들어온 순서(FIFO)대로 소비하고, 병합 뒤에도 공급 순서를 유지한다. 한 셀을 채우는 데 이전 공급의 남은 소수 면적과 다음 공급이 함께 필요하면 **그 셀을 마지막으로 채운 공급자의 색과 소유자**를 사용한다. 마지막 전체 셀을 채운 공급자가 `lastHit`를 받으며, 가장 최근에 던진 사람이 반드시 받는 것은 아니다.

가로 이음새는 연결하고 남·북극에서는 세로 범위를 고정한다. 모든 빈 셀에는 인접 성장 경로가 있으므로 기한 내에 충분한 명중 예산과 성장 시간을 확보하면 같은 지점을 반복해서 맞혀도 전체 100%까지 도달할 수 있다. `Fill Chance`가 0 또는 100이어도 다른 후보로 진행하는 대체 경로를 유지한다. 셀은 투명 또는 불투명이며, 진행도는 `CoveredCount / CellCount`다. 이 비율은 화면 픽셀·조명·실제 메시 삼각형 면적을 측정한 값이 아니다.

이전 128개 자국 배열/상한과 GPU 반복은 사용하지 않는다. `LabPaintSurface.DefaultRadius = 0.15`의 부드러운 `Stamp`와 `LabGourdPaint.splatRadius`는 연결 전 미리보기 전용이다. 실제 성장 면적과 속도는 위 설정을 사용하며 피해 배율로 늘어나지 않는다.

`LabGourdPaint`는 공개 표시 캐시인 `LabNetwork.PaintSurface`를 읽고 객체·세션·라운드·해상도·Revision이 바뀔 때 linear RGBA32 텍스처와 MPB `_PaintMap`·`_PaintMapReady`를 갱신한다. **Point, U Repeat/V Clamp, mipmap 없음**으로 셀 판정과 같은 범위를 보이며 기존 기본색·조명 계산을 유지한다. Renderer 비활성이나 프리팹/텍스처 재생성은 Host의 진행도를 지우지 않고 같은 캐시에서 다시 표시한다. 연결 중 `Paint`/`ClearPaint`는 모델 상태를 바꾸지 않는다. 연결 전 직접 `Paint`/`NotifyApprovedHit`는 미리보기·검사 fixture이며 실제 명중 증거가 아니다.

## Host/Client와 수정 지점

라운드 시작 시 Host가 물감 전용 `LabPaintPalette.ColorFor`를 캡처한다. 작성용 sRGB는 화=#D85736·수=#1471A7·목=#3D8258·금=#F8B72A·토=#6F4A32이며 linear 지도에 맞춰 변환한다. 구슬 재질과 분리되어 있다. 승인 피격 신호의 방향과 RGBA는 피격 표현에 쓰며 Client에서 `RegisterHit`·`Advance`를 재생하지 않는다.

Host의 권위 상태는 `LabModel.PaintSurface` 하나이며, 성장 계산은 Host의 전투 시간 진행에서만 한다. Host 화면도 현재 Snapshot과 같은 버전의 `LabNetwork.PaintSurface` 표시 캐시를 사용해 맵과 HUD를 함께 반영한다. Client는 승인 명중 방향을 재생해 성장시키지 않고 Host가 보낸 셀 변경(delta) 또는 전체 체크포인트를 별도 수신 맵에 적용한다. 분할 수신 중에는 기존 공개 표시 캐시를 유지하고, 완성된 맵의 세션·라운드·해상도·Revision·칠한 셀 수·전체 셀 수·명중 수가 Snapshot 메타데이터와 모두 맞을 때 맵과 HUD를 함께 공개한다. 픽셀이 그대로인 반복 명중도 명중 수가 바뀌면 빈 delta를 발행한다.

전송은 프로토콜 **5636**의 `paint_cells`·`paint_resync` 경로를 사용한다. 셀 ID와 RGBA를 담은 delta보다 전체 맵이 작으면 전체 맵을 보낸다. base64 데이터는 조각당 최대 4,096자, 수신 조각 수는 최대 64개이며 기존 패킷 한도 32 KiB 안에서 전송한다. 최대 해상도 전체 RGBA 맵은 131,072바이트이고 base64 174,764자를 43개로 나눈다. 수신 시 헤더·기준 Revision·셀 ID 범위와 순서·payload 크기·알파/색·칠한 셀 수·SHA-256 해시를 검사한 뒤 전체 맵을 한 번에 교체한다. 중복 완료·이전 전송/스냅샷 번호·이전 라운드를 거절하고, 기준 맵 누락이나 손상에는 Host의 마지막 발행 맵 복사본으로 체크포인트를 요청한다. `sequence`는 연결 전체의 색칠 전송 번호, `epoch`는 그 맵을 발행한 게임 Snapshot 번호다. 체크포인트는 같은 맵 버전의 `epoch`를 유지하며 `sequence`는 새 번호로 증가한다.

이 복구는 **현재 라운드에 계속 연결된 Client의 부분 맵 누락**을 복원하는 기능이다. 연결이 끊어진 뒤 전투에 재접속하는 기능은 포함하지 않는다. 실제 다중 앱·기기에서의 전송과 화면 일치는 자동 패킷 fixture와 구분해 검증한다.

| 파일·API | 책임 |
| --- | --- |
| `Core/LabPaintSurface.cs` — `Reset`, `RegisterHit`, `Advance`, `CopyPixels`, `TryReplacePixels` | Host 인접 성장·FIFO/병합·정수 진행도·Replica 원자적 교체 |
| `Core/LabModel.cs` — `ConfirmHit`, `TryAdvance`, `PaintSurface`, `PaintColorFor` | Host 명중·보상·100% 승리·시간 경계·마지막 셀 기여자·라운드 팔레트 |
| `Core/LabConfig.cs` — `PaintVictoryPercent`, `PaintGrowthSettings`, `CompletedGourdViewerEnabled` | 단일 설정 원본·총 142개·DEV 고정 87번·성장 105~110번·관람 111번·새 판 설정 복사 |
| `Session/LabPaintReplication.cs` — `LabPaintPublisher`, `LabPaintReceiver` | 셀 delta·체크포인트·분할/검증·기준 맵 복구·Snapshot 메타데이터 일치 |
| `Session/LabNetwork.cs` — `PaintSurface`, Snapshot 공개 경로 | Host/Client의 표시 캐시와 HUD 동시 반영·새 판/로비 초기화 |
| `Throw/LabGourdPaint.cs` — `Attach`, `ApplyMode`, `Surface`, `PaintTexture` | 외형 선택·안전 초기화·공개 맵 텍스처 표시. 모델 진행도를 지우지 않음 |
| `Art/GourdPaint/LabGourdPaint.shader` | 맵 1회 샘플링과 기존 조명 |
| `UI/GourdViewer/` — `LabGourdResultViewer`, `LabGourdResultPreview`, `GourdResultViewerUI`, `GourdViewerGestureSurface` | 공유 허용/완료 맵 확인·개별 모달 입력·격리 렌더·미리보기 자원 수명 |
| `UI/LabHud.cs` — `PaintGoalPercentFor`, `FormatPaintGoalProgress`, `PaintGoalFill` | 고정 100%·정수 셀 기반 내림 숫자와 막대 |
| `LabSceneController`·`ConfigApplied`, `LabGourdCrack`·`LabGourdFinish` | 기존 설정 적용 경로와 Crack의 독립된 HP 표현/마무리 |

## 검증을 읽는 법

과거 PR의 EditMode·PlayMode·빌드 PASS는 이번 인접 성장·111개 설정·100% 고정·셀 delta·체크포인트·표시 동시 반영의 결과로 승계하지 않는다. 실제 컴파일·테스트·다중 앱 결과는 `docs/VALIDATION.md`의 #187 기록을 따른다. 이 문서는 실행 PASS를 부여하지 않으며 패킷 fixture를 실제 네트워크 실행으로 보고하지 않는다. 실제 기기·핫스팟·iOS 빌드/조작/성능은 별도 실행 항목으로 남긴다.

#191 통합본은 EditMode 331/331·PlayMode 116/116, 실제 Mac Development Host + Release Client의 ON 관람·같은 방 OFF 재시작/완료 차단 및 최종 일반 Mac 빌드를 새로 확인했다. 두 앱의 각 9개 체크포인트와 종료 코드 0을 기록했다. iOS·실제 터치·핫스팟·3~5기기·기본 밸런스는 NOT_RUN이다. 실행 fixture와 한계는 저장소 `docs/VALIDATION.md`에 기록하며 과거 #187·#189 PASS와 구분한다.

## Experiment #198 — 번짐·흐름 성장

LEGACY의 기존 성장 ORIGINAL을 보존하며 DEV → Gourd → Paint Pattern을 LEGACY로 고른 뒤 Paint Growth Style을 SPREAD + FLOW로 바꾸면 연결된 비대칭 초기 얼룩과 아래 방향의 지속 꼬리를 사용한다. 불규칙도·흐름 선택 확률·구면 각거리 폭/길이·중력 우대는 인덱스134~138이다. 초기/반복 명중 면적과 시간당 성장량은 기존 단일 예산을 쓰며, 흐름의 폭을 채우는 셀도 예산을 소비한다. 막힌 꼬리·바닥·최대 길이는 일반 경계 확장으로 돌아가 마지막 셀까지 도달한다. 원래 색을 덮거나 혼합하지 않는다.

Host가 전투 프레임 경계에서 월드 아래를 `VisualMount` 로컬 방향으로 변환해 다음 성장 구간에 적용한다. 기존 도색은 박에 고정되고, Client는 Host RGBA 지도를 받아 표시한다. 새 입력마다 방향을 다시 뽑지 않으며, 서로 다른 FPS의 독립 시뮬레이션이 같은 지도를 만든다고 보장하지 않는다. Paint Grain과는 독립 설정이다. 사용자 승인으로 main에 포함하며 LEGACY 하위의 ORIGINAL을 유지하며 새 패턴 기본값은 SPLATTER다. 실제 실행 범위는 `docs/VALIDATION.md`의 #198 기록을 따른다.


## Experiment #200 후속 — 실제 명중 패턴4종

DEV → Gourd → Paint Pattern의 LEGACY/BROAD/SPLATTER/FLOW를 새 판에 적용한다. 기본은 첨부 Splatter다. 최초의 독립 튀김점도 같은 초기 예산을 쓰는 실제 불투명 셀이며, 각 연결 성분을 별도 관리하고 반복 명중은 맞은 연결 성분에만 공급한다. 새 Resources 마스크를 최초 명중 때 읽으며 MonoBehaviour 필드 초기화/직렬화에서는 Resources.Load를 호출하지 않는다. 세부 동작·파일 지도·실제 렌더 및 검증 범위는 `docs/PAINT_PATTERN_RUNTIME.md`·`docs/VALIDATION.md`를 따른다.
