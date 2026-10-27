# C6 Physics Lab 개발자 모드 조정값 명세

> **현행 설정 기준 (#246 정리·#251 방해공작12개·#272 지귀6개·#268 야광귀 섬광 7개·#271 야광귀 셈법1개·#276 지귀 자동4개·#281 2:2 땅따먹기4개·#299 고해상도 도색2개·#301 섬광 흔들어 풀기2개·#305 매혹 예고1개 추가):** Unity 6000.6.5f1, `Assets/Lab/Resources/LabConfig.asset`과 `LabConfig.DeveloperFields`에 연결된 공유 설정은 **205개**, 프로토콜은 **5665**, 연결 토큰은 `C6PhysicsLab/26`(게임 `C6HapioGame/9`)다. 사용하지 않는 고정 승리 목표 행과 옛 셀별 Flow 조절 4개를 제거했다. 100% 도색 승리 규칙·현재 4종 패턴·LEGACY 초기 Style/Raggedness·근처 명중 건조·개인 조합판 구조는 유지한다. 아래 인덱스는 정리 후 기준이며 옛 설정 배열과 혼합하지 않는다. 실행 결과는 [VALIDATION](VALIDATION.md)의 #246 기록에서 확인한다.

이 문서는 현재 Lab의 DEV에 연결된 값의 기본값·입력 범위·적용 경계·코드 연결을 설명한다. 원본 Inspector와 DEV 입력, 실행 증거는 구분한다.

## 현재 실험: 도색 패턴 4종 (#200 후속)

- **Gourd → Paint Pattern**: `LEGACY / BROAD / SPLATTER / FLOW`의 이름 버튼 4개(2×2). 인덱스 **134**, 정수 **0~3**, 기본 **SPLATTER(2)**. 화면에서는 기존 성장 토글 앞에 배치한다.
- `LEGACY(0)`에서만 기존 `Paint Growth Style` 인덱스132을 편집한다. ORIGINAL(0)·SPREAD + FLOW(1)를 그대로 보존한다. 새 패턴에서는 그 값을 저장하되 사용하지 않는다.
- `Paint Edge Raggedness`는 새 성장 경계의 요철 선호도에 관여한다. 사용되지 않는 옛 셀별 Flow 조절 4개는 #246에서 제거했다. 초기 3종 외곽은 원본 마스크로 정하고, 초기/반복 면적·속도·메우기·세로 가중치는 기존 공통 값을 사용한다.
- DEFAULTS는 원본 Config의 Splatter로 돌아간다. 솔로 시작/재시작·Host 방 생성·로비 적용·공동 일시정지 후 APPLY & RESTART에서만 확정한다. 선택 버튼만 눌러서는 진행 중인 도색을 바꾸지 않는다.
- 최초 튀김점은 같은 초기 면적 예산을 소비하는 실제 opaque 셀이다. 떨어진 성분은 별도 Region이며 반복 명중은 그 지점의 연결 성분에만 공급한다. 접촉하면 기존 FIFO 규칙으로 병합한다.
- 실제 물감은 공용 `LabPaintPalette`의 사용자5색이며 구슬 재질과 분리된다. [패턴 구현·검증 안내](PAINT_PATTERN_RUNTIME.md)를 참조한다. 현재 설정205개·프로토콜5665·토큰 `C6PhysicsLab/26`을 사용한다. 다른 길이의 배열과 다른 버전 참가자는 새 설정을 일부만 적용할 수 없다.

> main `f3c7cec4`의 #236/PR #248 북 AUTO와 #244/PR #249 콤보 요괴 연출을 통합했다. `Drum Count`는 기존 인덱스74를 유지하고 기본값0의 의미를 AUTO로 사용한다. 공유 필드는 추가하지 않아166개이며 현재 규약은5653/token15다. 과거5650/token14·170개 #248 실행 결과와 현재 통합 결과는 [VALIDATION](VALIDATION.md)에서 분리한다. 콤보 요괴는 기존 Host 스냅샷으로 구동하며 공유 DEV 필드를 추가하지 않는다.

## 1. 패널의 적용 방식

1. `Assets/Lab/Scenes/Lab.unity`를 Unity Editor에서 실행하거나 **Development Build**를 실행한다. 일반 배포 빌드에서는 DEV 버튼이 숨겨진다. 참가자는 일반 빌드여도 Host가 확정한 세션 설정을 수신한다.
2. 접속 전 또는 **전투 시작 전 Host 로비**에서 **DEV**를 연다. 공유 전투 중에는 Host가 먼저 공동 일시정지한 뒤 DEV를 열어 **APPLY & RESTART**로 적용한다. Client는 설정을 직접 바꾸지 않는다. 총 205개 설정 중 일반 숫자 입력칸과 대응 슬라이더는 서로 값을 반영한다. 도색 승리는 100% 고정 게임 규칙이며 별도 DEV 행은 없다. 단, 0/1 항목 14개(Drop Combine, Contact Combine, Haptics, Gourd Mode, Minimap, Bomb Sound, Completed Gourd Viewer, Throw Form, Paint Grain, Paint Growth Style, Player Paint Patterns, Paint Drying, Gourd Color Combo, Combo Penalty Target)는 입력칸·슬라이더 대신 누를 때마다 바뀌는 토글 버튼(ON/OFF, Gourd Mode는 PAINT/CRACK, Bomb Sound는 BOOM/PPEONG, Throw Form은 ARC/STRAIGHT, Paint Growth Style은 ORIGINAL/SPREAD + FLOW, Combo Penalty Target은 TEAM/THROWER)으로 보이며, 값은 그대로 0/1로 적용·공유된다. 정수 필드는 슬라이더 값도 정수로 표시하며, 매우 넓은 범위는 슬라이더 조작 구간을 비선형으로 배분한다. 숫자 입력으로 허용 범위의 정확한 값을 지정할 수 있다.
3. 공유 실험을 시작하려면 별도 **ROOM PLAYERS** 슬라이더에서 2~5명(기본 3명)을 정하고 **CREATE ROOM**을 누른다. Host가 검증된 205개 값으로 방을 만든 뒤 참가자에게 전파한다. 기존 **HOST** 버튼을 사용하면 기본 인원과 현재 설정으로 방을 만든다. 방 생성 후 아직 전투 전이라면 **APPLY TO ROOM**으로 다시 조정할 수 있으며 이전 참가자 확인은 무효화된다. 목표 인원이 모이고 각 참가자가 최신 설정을 확인한 뒤에만 Host의 **START**가 활성화된다.
4. 혼자 시험할 때는 **START SOLO**를 누른다. 이미 솔로 전투 중이면 **RESTART SOLO**가 표시된다. 새 라운드가 시작되므로 요괴/개인 HP·시간·스태미나·구슬이 초기화된다. 진행 중인 네트워크 라운드에 값을 즉시 적용하는 동작은 없다.
5. **DEFAULTS**는 저장된 `LabConfig.asset`의 숫자를 입력칸과 슬라이더에 불러올 뿐이다. 공유 방이나 솔로에 적용하려면 해당 시작·적용 버튼을 눌러야 한다. **END SOLO**는 솔로를 끝내고 원본 설정으로 복귀한다. 런타임 복사본은 앱이 실행 중인 동안만 유지되며 원본 에셋에는 저장되지 않는다.

패널 열기 자체는 시간을 멈추지 않는다. 공유 전투에서 설정을 바꾸려면 먼저 공동 일시정지를 승인받아야 하며, 이때 시간은 그 일시정지 상태를 따른다. 마지막 `SHOT HIT/MISS` 보고는 솔로를 끝내거나 연결을 끊으면 지워질 수 있으므로 먼저 기록한다. 공유 방에서는 Host의 설정 지문과 확인 응답을 기준으로 시작을 판정한다. 이 절은 코드상 경로 설명이며 실제 기기 동기화 결과는 검증 기록과 구분한다.

입력은 **205개 전체를 한 번에** 검사한다. 숫자가 아니거나 무한대/NaN, 범위 밖, 정수 전용 필드의 소수 입력이 있으면 아무 값도 적용하지 않는다. 아래 표의 넓은 허용 범위는 “게임플레이에 적합함”을 뜻하지 않는다. 극단적인 카메라·속도·수명 값은 화면 밖 이동이나 빗나감을 일으킬 수 있다.

## 2. 단위와 공통 판정

- **월드 단위(m)**: 이 프로젝트의 Unity 월드 거리. `m/s`와 `m/s²`는 그 거리 기준 속도·가속도를 설명하기 위한 표기다.
- **보드 폭**: 3D 구슬이 XY 평면에서 움직이는 조합판의 가로 너비를 1로 본 단위. 손가락 입력 속도, 조합판 구슬의 방출·감속·정지 값은 보드 폭을 기준으로 정규화된다. 따라서 이 값과 비행 투사체의 월드 단위/초를 직접 비교하면 안 된다.
- **높일 때의 효과**는 다른 수치와 손동작이 같다는 조건에서의 경향이다. 충돌, 속도 상한, 발사각 제한 때문에 항상 같은 크기로 나타나지는 않는다.
- 표의 **범위**는 DEV 입력칸의 범위다. Inspector의 `[Min]`·`[Range]`와 런타임 속성의 방어적 보정은 별도이며, 패널 적용 시에는 표의 범위와 값 간 관계를 먼저 검사한다.

## 3. 전투 결과

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Monster Max HP | 1000 | 정수 1–1,000,000 / HP | 새 라운드의 요괴 최대·초기 체력. Crack에서는 HP 0 승리까지 필요한 명중 수를 바꾼다. Paint에서는 감소하는 내부 진단 HP이고 색칠 목표나 승리를 바꾸지 않는다. 진행 중인 HP를 직접 고치는 값은 아니다. |
| Damage per Hit | 20 | 정수 1–1,000,000 / HP | Host가 실제 명중을 확정할 때 쓰는 기본 공격값. HP는 0 아래로 내려가지 않으며 `actualDamage`는 실제 감소량이다. Paint는 HP 0 이후에도 양수 `impactValue`와 기존 명목 공격값 점수를 유지한다. 이 값/아이템 배율은 초기·반복 색칠 면적이나 성장 속도를 늘리지 않는다. 빗나감에는 적용되지 않는다. |
| Battle Duration (seconds) | 180 | 1–86,400 / 초 | Host가 라운드 시작 시 계산하는 제한 시간. 시간이 끝나면 패배한다. 패널 열기 자체는 시간을 멈추지 않으며 승인된 공동 일시정지만 시간에서 제외한다. |
| Player Max HP | 300 | 정수 1–300 / 개인 HP | 각 플레이어의 새 라운드 최대·초기 체력. DEV 필드 배열에서는 **49번째(인덱스 48)**로 추가되어 앞선 48개 설정 순서를 보존한다. 한 사람의 개인 HP가 0이어도 전투를 계속하며 전원 0이면 패배한다. 진행 중인 개인 HP를 직접 바꾸는 슬라이더가 아니다. |

개인 HP는 Host의 플레이어별 상태와 스냅샷에 보관되며 화면에는 내 값만 표시한다. 현재 요괴 공격이나 아이템 피해 이벤트는 이 값을 깎지 않는다. Host 모델의 `TryApplyPlayerDamage`는 향후 이벤트용 상태 변경 경로로, 양수 피해·유효한 플레이어·진행 중인 판에서만 적용하고 0에서 멈춘다. 실제 플레이에서 전원 0 패배를 시험하려면 별도 피해 이벤트 구현이 필요하다.

Paint의 HP와 결과 대시보드는 구분한다. Paint의 `damageTotal`은 기존 공격값 누계로 유지되며 실제 HP 감소량이나 면적 기여도를 뜻하지 않는다. Crack은 실제 감소량을 누적한다. Host의 `ConfirmHit`는 시간 경과/종료를 먼저 확인한 뒤 같은 구슬의 소모·HP·보상·색칠·승리를 함께 확정한다.

근거: `LabModel.BeginWithSeats`, `TryApplyPlayerDamage`, `ConfirmHit`, `TryAdvance`, `LabHud`.

### 박 모드·100% 공동 목표·성장

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Gourd Mode (0 paint, 1 crack) | 0 (Paint) | 정수 0–1 / 표현 모드 | 80번째(인덱스 79), `Gourd` 묶음. Paint는 흰 박의 공동 색칠, Crack은 기존 박·HP 단계 크랙·승리 마무리다. 원통 충돌·회전·투척 입력을 공유한다. |

아래 6개는 **DEMO_TUNING_VALUE**이며 Inspector 원본 `LabConfig.asset`의 Gourd 항목 또는 DEV의 `Gourd` 묶음에서 조정한다. 수치 튜닝은 새 판에 적용하며, 현재 실행 중인 판의 규칙을 즉시 바꾸지 않는다.

| DEV 입력 이름 | 위치 | 기본값 | 입력 범위 | 효과 |
| --- | --- | ---: | --- | --- |
| Paint Surface Resolution (0 low 1 medium 2 high) | 105번째 · 인덱스 104 | 2 | 정수 0–2 | 0: 64×32, 1: 128×64, 2: 256×128 동일 면적 구면 셀. 새 판에서 적용한다. |
| Paint Initial Coverage (%) | 106번째 · 인덱스 105 | 2 | 0.1–100% | 미도색 명중의 전체 초기 면적 예산. 고정 크기 마스크의 빈 셀만 즉시 칠하고 겹침 잔량은 명중점 주변 시간 성장으로 넘긴다. 전체 셀 수 기준으로 양자화한다. |
| Paint Repeat Growth (%) | 107번째 · 인덱스 106 | 1.5 | 0.1–100% | 이미 칠한 지점에 맞히면 연결된 영역에 추가하는 성장 면적 예산. 기존 셀 색은 바꾸지 않는다. |
| Paint Growth Speed (%/s) | 108번째 · 인덱스 107 | 6 | 0.1–100%/초 | 모든 영역이 함께 나눠 쓰는 전체 표면 기준 성장 속도. 영역 수만큼 속도가 늘지 않는다. |
| Paint Fill Chance (%) | 109번째 · 인덱스 108 | 60 | 0–100% | 이웃이 둘 이상 칠해진 빈 셀을 우선 채울 확률. 해당 후보가 없으면 다른 인접 후보로 진행한다. |
| Paint Vertical Weight | 110번째 · 인덱스 109 | 1.35 | 1–4 | 세로 방향 인접 후보의 선택 가중치. 1은 가로와 같은 가중치다. |

`DefaultPaintVictoryPercent`·`PaintVictoryPercent`는 100이고 도색 승리는 마지막 실제 빈 셀을 칠해 `Coverage == 1`이 되면 확정한다. 목표 비율의 직렬화 필드·DEV 행·공유 배열 항목은 #246에서 제거했으며 낮은 비율의 승리를 설정할 수 없다.

200개 전체를 **START/RESTART SOLO**, **CREATE ROOM**, Host 로비의 **APPLY TO ROOM**, 공동 일시정지한 Host의 **APPLY & RESTART**에서 검증·적용한다. `LabConfig.PaintGrowthSettings`는 매번 새 설정 복사본을 반환하고 Host Rules는 새 라운드 시작 때 이를 캡처한다. 일반 Retry는 캡처한 설정을 유지한다. `LabSceneController`의 초기 적용/`LabNetwork.ConfigApplied`가 `LabGourdPaint.Attach`·`ApplyMode`를 호출하며 Client는 Host 모드를 받는다.

유효한 오행 명중이 안 칠한 셀에 닿으면 초기 마스크의 빈 부분을 해당 타격의 잉크로 즉시 칠하고 겹침 잔량은 명중점에 고정한 군집으로 넘긴다. 이미 칠한 셀에 닿으면 그 연결 영역에 반복 성장 면적을 공급한다. 이후 Host 전투 시계가 진행될 때 미사용 예산을 가진 영역들이 전체 표면 기준 `%/초` 속도를 나눠 쓰며 빈 인접 셀로 자란다. 시간이 흐르는 동안 공급 예산이 없으면 자라지 않으며, 그 대기 시간의 성장량을 미리 적립하지 않는다. 여러 영역은 차례대로 성장하고 서로 닿으면 하나로 병합된다.

이미 칠한 셀의 색은 덧칠하거나 병합할 때 바뀌지 않는다. 초기 마스크는 자기 타격의 예산만 즉시 소비한다. 시간 성장의 면적 공급은 각 연결 영역 안에서 먼저 들어온 순서(FIFO)대로 소비하고, 병합 뒤에도 공급 순서를 유지한다. 한 셀을 채우는 데 이전 공급의 남은 소수 면적과 다음 공급이 함께 필요하면 **그 셀을 마지막으로 채운 공급자의 색과 소유자**를 사용한다. 마지막 전체 셀을 채운 공급자가 `lastHit`를 받으며, 가장 최근에 던진 사람이 반드시 받는 것은 아니다.

가로 이음새는 연결하고 남·북극에서는 세로 범위를 고정한다. 모든 빈 셀에는 인접 성장 경로가 있으므로 기한 내에 충분한 명중 예산과 성장 시간을 확보하면 같은 지점을 반복해서 맞혀도 전체 100%까지 도달할 수 있다. `Fill Chance`가 0 또는 100이어도 다른 후보로 진행하는 대체 경로를 유지한다. 셀은 투명 또는 불투명이며, 진행도는 `CoveredCount / CellCount`다. 이 비율은 화면 픽셀·조명·실제 메시 삼각형 면적을 측정한 값이 아니다.

제한 시간 전에 마지막 셀이 완성되면 공동 Victory가 되고, 마지막 성장 시각과 제한 시간 종료가 정확히 같으면 Defeat가 우선한다. 늦게 도착한 Tick도 제한 시간 전에 완료될 성장은 해당 시각까지 계산한다. 공동 일시정지 동안 성장·전투 시계·입력·물리는 멈추며 재개 뒤 이어간다. 일반 Retry는 모드와 캡처한 성장 설정을 유지하면서 맵·영역·공급 예산·명중 수·난수 진행을 초기화한다. 새 DEV 값은 다음 시작/재시작에 캡처하며 진행 중인 판에는 적용하지 않는다. 로비 복귀와 방 이탈도 표시·수신 상태를 정리한다.

HUD 목표는 항상 100%다. `snapshot.paintCellCount > 0`이면 `paintCoveredCount / paintCellCount`를 사용하며 숫자는 소수 첫째 자리까지 내림한다. 예를 들어 마지막 한 셀이 남으면 99.9%로 표시하고 막대도 미완료 상태를 유지한다. 셀 수가 없는 예전 검사 fixture만 `paintCoverage`를 보완값으로 쓴다. 상단 Paint 제목은 **GOAL**, Crack은 기존 **YOKAI HP**다. 기존 전투 HUD 배치와 저장 Scene 객체는 유지한다. 완성 박 관람 UI는 별도 런타임 프리팹으로 불러온다. Renderer 비활성·재생성은 Host의 색칠 상태를 지우지 않는다. `LabGourdPaint.splatRadius`와 `LabPaintSurface.Stamp`의 기존 부드러운 브러시는 연결 전 미리보기 전용이다.

Host의 권위 상태는 `LabModel.PaintSurface` 하나이며, 성장 계산은 Host의 전투 시간 진행에서만 한다. Host 화면도 현재 Snapshot과 같은 버전의 `LabNetwork.PaintSurface` 표시 캐시를 사용해 맵과 HUD를 함께 반영한다. Client는 승인 명중 방향을 재생해 성장시키지 않고 Host가 보낸 셀 변경(delta) 또는 전체 체크포인트를 별도 수신 맵에 적용한다. 분할 수신 중에는 기존 공개 표시 캐시를 유지하고, 완성된 맵의 세션·라운드·해상도·Revision·칠한 셀 수·전체 셀 수·명중 수가 Snapshot 메타데이터와 모두 맞을 때 맵과 HUD를 함께 공개한다. 픽셀이 그대로인 반복 명중도 명중 수가 바뀌면 빈 delta를 발행한다.

전송은 프로토콜 **5662**의 `paint_cells`·`paint_resync` 경로를 사용한다. 셀 ID·RGBA·투척자 P번호를 담은 delta/전체 체크포인트와 공동 Snapshot은 별도 Bulk 스트림을 사용한다. 도색 논리 조각은 최대2,048자·107개이며 Bulk가 접두부 포함900문자·외부 메시지1,200바이트 이하로 다시 나눈다. 순서·누적 ACK·재전송·통합 해시 검증 후 색상/P맵을 원자적으로 공개하고 행동 승인용 통신과 분리한다. 상세 경계는 [LOCAL_BOARD_AUTHORITY](LOCAL_BOARD_AUTHORITY.md)를 따른다.

이 복구는 **현재 라운드에 계속 연결된 Client의 부분 맵 누락**을 복원하는 기능이다. 연결이 끊어진 뒤 전투에 재접속하는 기능은 포함하지 않는다. 실제 다중 앱·기기에서의 전송과 화면 일치는 자동 패킷 fixture와 구분해 검증한다.

현재 프로토콜은 **5662**, 연결 토큰은 `C6PhysicsLab/23`다. 박 줄·측면 타격 움직임을 포함한 200개 설정을 지문·참가자 확인 응답에 사용하며 현재 200개가 아닌 배열은 거절한다. 이전 모드·폭탄·북·미니맵·소리·성장 설정과 색칠 전송 형식은 유지한다. 모든 참가자가 같은 새 버전을 사용해야 하며 이전 PASS를 이번 통합의 결과로 승계하지 않는다. 실제 실행 결과는 `docs/VALIDATION.md`에서 변경별로 구분한다.

근거: `LabConfig.PaintGrowthSettings`·`DeveloperFields`·`CaptureDeveloperValues`·`TryApplyDeveloperValues`, `LabPaintSurface.RegisterHit`·`Advance`·`TryReplacePixels`·`CopyPixels`, `LabModel.ConfirmHit`·`TryAdvance`·라운드 Rules, `LabPaintPublisher`·`LabPaintReceiver`, `LabNetwork.PaintSurface`·Snapshot 공개 경로, `LabGourdPaint.Attach`·`ApplyMode`, `LabHud`, `LabGourdCrack`·`LabGourdFinish`.

### 완성 박 관람 — 111번째 설정 (#191)

| DEV 입력 이름 | 위치 | 기본값 | 입력 범위 | 실제 동작 |
| --- | --- | --- | --- | --- |
| Completed Gourd Viewer (0 off, 1 on) | 111번째 · 인덱스 110 · Gourd | ON(1) | 정수 0–1 | OFF는 관람 진입을 숨긴다. ON은 Host가 모든 참가자에게 Paint 100% 승리 결과의 관람을 허용한다. |

Inspector의 `Completed Gourd Viewer Enabled`와 `LabConfig.CompletedGourdViewerEnabled`를 사용한다. 저장 필드가 없는 에셋도 true가 기본이다. 성장6개와 별개의 기능 허용 토글이다. 도색 승리100%는 별도 DEV 행 없는 고정 게임 규칙이다. 현재200개 전체의 유한값·범위·정수 검사를 먼저 통과해야 하므로 소수·범위 밖 bool이나 길이가 다른 배열은 일부 값도 적용하지 않는다.

**START/RESTART SOLO**, **CREATE ROOM**, Host 로비의 **APPLY TO ROOM**, 공동 일시정지한 Host의 **APPLY & RESTART**에서 기존 공유 설정과 함께 적용한다. Host 설정 지문과 전원 확인 응답이 시작을 제어한다. Client가 로컬로 허용값을 바꾸는 기능은 없으며 진행 중인 판에 즉시 적용하지 않는다. **일반 Release 참가자도 Host 설정을 따른다.** DEV 패널만 Editor·Development Build에 제한하며 관람 자체에는 Debug 빌드·실행 인자·Editor 메뉴 활성화가 필요하지 않다.

관람 진입은 Paint의 **100% Victory**이고 Snapshot과 공개 표시 맵의 세션·라운드·해상도·Revision·칠한/전체 셀 수·명중 수가 일치한 때만 보인다. Crack·Defeat·분할 수신 중인 미완성 맵은 대상이 아니다. `Assets/Lab/Resources/GourdResultViewer/ViewerUI.prefab`을 필요한 결과에서만 불러오고, 각자가 열기·닫기·드래그 회전·핀치/마우스 휠 확대·축소·**RESET VIEW**를 수행한다. 이 동작과 카메라 상태는 공유하지 않으며 게임 맵·판정·점수도 변경하지 않는다. 모달이 아래 결과 버튼 입력을 막고 **BACK TO RESULTS**는 미리보기 자원을 해제한다.

일반 Retry는 공유 허용값을 유지하면서 관람창과 이전 미리보기를 닫고 새 판을 기다린다. 새 설정 적용·로비 복귀·연결 종료·모드 변경도 UI와 미리보기 카메라·텍스처·복사 재질을 정리한다. 저장 Scene과 기존 카메라·물리·렌더 상태는 유지한다. 기존 #189 실험 분류와 기록은 `docs/experiments/`에 보존하며, 현재 통합 기능은 별도 실험 활성화 경로를 사용하지 않는다.

근거: `LabConfig.CompletedGourdViewerEnabled`, `LabGourdResultViewer.CanInspect`·`RefreshState`·`OpenViewer`·`CloseViewer`, `LabGourdResultPreview`, `GourdResultViewerUI`, `GourdViewerGestureSurface`.

### 박 폭탄 방해공작 — 89·89번째 설정 (#177)

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Gourd Bomb Interval Min (seconds, 0 = off) | 20 | 0–600 / 초 | 88번째(인덱스 87), `Gourd` 묶음. 박이 무작위 한 명에게 폭탄을 떨어뜨리는 간격의 하한. 매 폭탄마다 Min~Max 사이에서 새로 뽑는다. Min ≤ Max여야 한다. |
| Gourd Bomb Interval Max (seconds, 0 = off) | 30 | 0–600 / 초 | 89번째(인덱스 88), `Gourd` 묶음. 간격의 상한. **0이면 박 폭탄이 나오지 않는다.** 아주 작은 값을 넣어도 한 번에 쏟아지지 않도록 실제 간격은 최소 1초다. |

Host 모델이 라운드 시작 시각부터 간격을 세고, 시각마다 대상(활성 자리 중 무작위, 같은 사람 연속 허용)과 착지 위치(판 폭 20~80%, 높이 0.6)를 세션·라운드 seed로 추첨해 잠긴 폭탄(출처 `Gourd`, 잠금 횟수는 `Item Unlock Swipes Min`~`Max`, #212)을 만든다. 가득 찬 판에도 넣고 스태미나를 쓰지 않으며 생성 기록에 세지 않는다. 일시정지 동안은 전투 시계와 함께 멈추고, 판이 끝나면 더 만들지 않는다. 기본값이면 180초 한 판에 6~9개다.

Host는 박 폭탄 신호(받는 사람·폭탄 ID·착지 위치)를 그 폭탄이 처음 담긴 스냅샷보다 먼저 모든 참가자에게 보낸다. 모든 화면은 전투 화면 박 옆에 "!" 말풍선(`LabGourdAlert`, 약 1.4초)을 띄우고, 받는 화면은 경고 없이 곧바로 폭탄을 구슬판 위쪽에서 떨어뜨려 튕긴다(#212에서 착지 "!" 경고 제거). 낙하·튕김 시간과 높이는 `LabOrbAppearance`의 **Gourd bomb drop**(기본 0.45·0.45초, 튕김 높이 구슬 반지름 1배), 말풍선 크기·시간은 `LabGourdAlert`의 기본값(150px·1.4초)이며 DEV로 조정하지 않는다. 내려앉기 전에는 잡기·충돌·폭발 밀림·떠돌기가 없다.

근거: `LabConfig.GourdBombIntervalMin`·`GourdBombIntervalMax`, `LabModel.DropGourdBombs`·`GourdBombDelay`·`DrainGourdBombs`, `LabNetwork.AnnounceGourdBombs`·`GourdBombDropped`, `LabOrbBoard.ExpectDrop`, `LabOrbView.PlayDrop`, `LabGourdAlert`.

### 박의 넘기기 막기 — 153~155번째 설정 (#215)

DEV의 `Gourd` 묶음에서 박 폭탄 간격(89·89번째) 바로 뒤에 있다.

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Swipe Block Interval (seconds, 0 = off) | 40 | 0–600 / 초 | 153번째(인덱스 152). 박이 무작위 한 명의 한쪽 넘기기를 막는 간격. 라운드 시작 뒤 이 시간에 처음 걸리고, 그 뒤 시작 시각마다 이만큼 지나서 다시 걸린다(지금 막기가 끝나기 전에는 걸리지 않음). **0이면 막지 않는다.** |
| Swipe Block Seconds | 10 | 1–60 / 초 | 154번째(인덱스 153). 한 번 막으면 이어지는 시간. 막힌 판의 남은 초 표시도 이 시간에서 줄어든다. |
| Swipe Block Bomb Gap (seconds) | 4 | 0–15 / 초 | 155번째(인덱스 154). 막기가 박 폭탄이 떨어졌거나 떨어질 시각과 최소 이만큼 떨어져 시작하도록 한다. 가까우면 그 폭탄 뒤로 미룬다. 0이면 겹쳐도 그대로 시작한다. |

대상(활성 자리 중 무작위, 같은 사람 연속 허용)과 방향(왼쪽·오른쪽)은 세션·라운드 seed와 막기 순번으로 Host가 정해 스냅샷의 `swipeBlockedPlayer`·`swipeBlockedRight`·`swipeBlockStartedAt`·`swipeBlockedUntil`로 전한다. 막히는 것은 그 사람이 그 방향으로 스스로 넘기는 "스와이프" 넘김뿐이며 Host가 `SWIPE_BLOCKED`로 거절한다. 폭발에 밀린 것(#215부터 구슬도 "폭발"로 넘기며, 멈추거나 손으로 잡으면 보통 구슬로 돌아감)·앞사람이 넘겨 지나가는 북(#178)·들어오는 넘김은 막지 않는다. 일시정지 동안은 전투 시계와 함께 멈춘다.

막힌 사람 판은 그 가장자리를 벽처럼 튕기고(반발 `Orb Restitution`), 시작 때 진한 빨강으로 3번(0.35초씩) 번쩍이며 빛이 평소의 약 2배 폭까지 퍼진 뒤, 끝날 때까지 연한 빨간빛(판 폭 10%, 천천히 숨쉬기)과 가장자리 선, 세로 가운데의 남은 초(올림)를 보여 준다. 구슬이 튕기면 잠깐 밝아진다. 전투 화면 박 옆에는 "← 🚫" 또는 "→ 🚫" 말풍선이 약 2초 뜬다(막기 한 번에 한 번, 시작 1초 뒤에 처음 본 막기는 띄우지 않음). 이 표시는 모두 막힌 사람 화면에만 있으며 DEV로 조정하지 않는다.

근거: `LabConfig.SwipeBlockInterval`·`SwipeBlockSeconds`·`SwipeBlockBombGap`, `LabModel.StartSwipeBlocks`·`ClearOfGourdBombs`·`SwipeBlocked`·`TryTransferAt`, `LabSnapshot.SwipeBlockOn`, `LabOrbBoard.SetSwipeBlock`·`Blast`, `LabOrbEdgeBlock`, `LabNetwork.SwipeBlockStarted`, `LabGourdAlert.ShowBlock`.

## 4. 스태미나·구슬 생성

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Maximum Stamina | 160 | 1–1,000,000 / 스태미나 | 개인 스태미나 상한. 시작량·생성 비용·회복량·명중 보상은 이 값 이하여야 한다. #305에서 100 → 160: 가득 찬 막대로 8번 생성하며, 전투 화면 막대도 8칸이다(칸 1개 = 생성 1번). |
| Starting Stamina | 160 | 0–1,000,000 / 스태미나 | 새 라운드에서 각 플레이어가 갖는 양. 높이면 처음 만들 수 있는 구슬 수가 늘 수 있다. #305에서 최대와 함께 160(가득). |
| Orb Generation Cost | 20 | 0.001–1,000,000 / 스태미나/개 | `GENERATE` 요청이 Host에서 승인될 때 구슬(음·양·아이템) 한 개당 소비. 현재 보유량이 비용 미만이면 생성이 거절된다. |
| Stamina Recovery Amount | 20 | 0–1,000,000 / 스태미나 | 아래 간격과 함께 자동 회복 **속도**를 정한다. 0이면 시간에 따른 회복이 없다. |
| Stamina Recovery Interval (seconds) | 1 | 0.001–86,400 / 초 | 회복 속도 = `Recovery Amount ÷ Recovery Interval`. 현재 값은 **초당 20씩 연속 회복**(1초에 구슬 1개분)한다. “1초마다 한 번에 20”으로 처리하지 않는다. #146에서 3초 → 1.7초, #305에서 구슬·북을 기다리는 동안에도 자주 만들 수 있게 1.7초 → 1초(시작값, DEV로 비교 중). |
| Stamina Reward per Hit | 10 | 0–1,000,000 / 스태미나 | Host가 실제 명중을 확정했을 때 그 투사체의 공격자에게만 즉시 더한다. 최대 스태미나를 넘지 않으므로 이미 가득 차 있으면 버려진다. 빗나감은 보상이 없다. #146에서 5 → 10(구슬 ½개)으로 올렸다. |
| Stored Orb Limit | 20 | 정수 1–100 / 구슬 개수 | 각 플레이어가 보관하는 비행 전 구슬의 수량 상한. 생성자뿐 아니라 전달받는 사람의 남은 자리도 검사한다. |

예시: 기본값에서 구슬 한 개를 만들면 160→140이 된다. 다른 행동이 없다면 1초 동안 20이 연속으로 회복돼 다시 160이 된다. 0에서 160까지는 8초가 걸린다. 2:2 방은 아래 #281 절의 자기 값(최대 160·1.7초)을 쓴다. 실제 회복은 Host 시간 경과를 처리할 때 갱신된다. 생성되는 종류는 아래 **8절의 생성 확률**을 따른다.

근거: `LabConfig.StaminaRecoveryPerSecond`, `LabModel.TryGenerate`, `TryTransferAt`, `ConfirmHit`, `TryAdvance`.

## 5. 구슬 수명·3D 조합판·생성 연출

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Orb Lifetime (seconds) | 15 | 0.1–86,400 / 초 | #305에서 8 → 15. 보관 중인 Raw·Combined의 수명(아이템은 8절의 `Item Lifetime`). Raw는 생성 시점, Combined는 조합 시점부터 센다. 이웃에게 전달해도 수명은 초기화되지 않는다. 이미 발사돼 비행 중인 구슬은 이 만료 대상이 아니다. 마지막 3초에는 구슬이 하얗게 번쩍이고 끝나면 연기와 함께 사라진다(#235, 번쩍임 시간은 Inspector `Expiry Warning Seconds`). |
| Orb Bounce | 0.8 | 0–1 / 3D 접촉 탄성 | 구슬·상하 벽의 `PhysicsMaterial.bounciness`. 높이면 접촉 후 더 잘 튀는 경향이 있다. 충돌 결과는 상대 물체와 물리 설정에도 영향을 받는다. |
| Orb Floor Deceleration | 2 | 0–100,000 / 보드 폭/초² | 손을 놓은 뒤 XY 평면에서 움직이는 3D 구슬의 속력에서 매 물리 스텝 감산한다. 높이면 더 빨리 멈춘다. Unity의 `Rigidbody.linearDamping`이나 접촉 재질의 마찰을 조정하는 값은 아니다. |
| Orb Stop Speed | 0.065 | 0–100,000 / 보드 폭/초 | 구슬 속력이 이 값 이하이면 0으로 만든다. 높이면 낮은 속도의 구슬이 일찍 멈춘다. `Orb Maximum Release Speed` 이하여야 한다. |
| Orb Maximum Release Speed | 3 | 0.001–100,000 / 보드 폭/초 | 잡은 조합판 구슬을 놓을 때 부여할 최대 평면 이동 속력. 높이면 더 세게 밀 수 있다. Host가 받는 이동 샘플의 안전 상한도 이 값의 16배로 계산된다. 3D 투척 속도 상한과 별개다. |
| Orb Radius | 0.32 | 0.001–100 / 월드 단위 | 조합판 구슬 루트의 3D `SphereCollider` 반경과 기본 구체 표시 반경. 잡기·직접 겹침 조합 거리, 판 가장자리 여백에도 쓰인다. 크기를 키우면 아래의 판 높이 조건을 다시 맞춰야 한다. |
| Orb Board Height | 5 | 0.1–1,000 / 월드 단위 | 구슬 전용 직교 카메라의 세로 표시 높이. 가로 길이는 화면 비율에 따라 달라지고 판의 물리 경계도 함께 다시 계산된다. |
| Board Orb Trail (0 off, 1 on) | ON | 토글 | 166번째(인덱스 165, #254). 켜면 조합판의 결합(오행) 구슬이 움직일 때 던진 구슬과 같은 오행 먹 꼬리를 판 크기(Orb Radius / Throw Radius × 0.6)로 얕게 남긴다. 화면 위 이동 속도가 초당 반지름 1.5개 이상일 때 나타나 10개에서 가장 진하며(던진 꼬리의 0.55배), 멈추면 사라진다. 음양 구슬·아이템·북에는 없다. 크기·진하기·나타나는 속도는 `LabOrbBoardTrail` Inspector. 화면 표시만 하며 물리·판정·통신은 바꾸지 않는다. |
| Spawn Pop Start Size (0-1 of the orb) | 0.3 | 0–1 / 구슬 크기 대비 비율 | 새로 만든 구슬의 **Visual 자식**이 북 한가운데에서 나타날 때의 크기. 3D 물리 루트는 처음부터 북 바로 바깥에서 출발해 날아가고, 그림만 북 안에서 커지며 따라 나온다. 구슬 ID·Host 위치·충돌 크기는 바꾸지 않는다. #137 전에는 `Spawn Rise Distance`였다. |
| Spawn Pop Duration (seconds) | 0.15 | 0–2 / 초 | 그림이 북에서 나와 제 크기가 되는 시간. 이 동안 구슬은 이미 날아가며 다른 구슬·북과는 부딪히지 않고(벽에는 부딪힘), 연타한 구슬끼리 막히지 않는다. 0이면 연출 없이 바로 제 모습이다. #137 전에는 `Spawn Rise Duration`이었다. |
| Combine Speed | 0 | 0–10 / 보드 폭/초 | 닿으면 결합의 기준 속도. 두 구슬이 서로 다가간 속도(`Collision.relativeVelocity`를 보드 폭으로 나눈 값)가 이 값 이상일 때만 결합을 요청한다. 0이면 스쳐도 결합하고, 높이면 세게 부딪힐 때만 결합한다. DEV 필드 배열의 **50번째(인덱스 49)**. |
| Drop Combine (0 off, 1 on) | 1 | 정수 0–1 | 1이면 잡은 음/양 구슬을 반대 음양 구슬 위에 놓아 결합한다. 0이면 겹쳐 놓아도 결합하지 않는다. 단, 닿으면 결합이 켜져 있으면 손을 뗄 때 겹친 충돌로 결합될 수 있다. **51번째(인덱스 50)**. |
| Contact Combine (0 off, 1 on) | 1 | 정수 0–1 | 1이면 음/양 구슬이 서로 닿을 때 자동 결합을 요청한다(연쇄 결합 포함). 잡고 있는 구슬, 경계에서 넘어가는 중인 구슬, 잠긴 구슬, 결합 대기 중인 구슬, 아이템·북은 제외한다. 북에서 튀어나오는 중인 구슬은 다른 구슬과 닿지 않으므로 결합되지 않는다. 판정은 구슬이 있는 기기가 하고 Host의 결합 승인으로 확정한다. **52번째(인덱스 51)**. |

### 생성 퍼짐과 생성 북

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Spawn Fan Angle (deg each side, 180 = all directions) | 180 | 0–180 / 도 | 구슬이 북에서 튀어나가는 방향의 범위. 위쪽 기준 좌우 각각 이 각도까지 Host가 고르게 뽑는다. 180이면 모든 방향, 0이면 바로 위로만 나간다. **61번째(인덱스 60)**. |
| Spawn Minimum Speed (board widths/s) | 1 | 0–10 / 보드 폭/초 | 튀어나가는 속력의 하한. Host가 최소~최대 사이에서 뽑는다. 북이 옆 가장자리에 가까우면 그쪽으로 가는 구슬은 바닥 감속으로 멈출 때 가장자리 0.08 보드 폭 안쪽에 서도록 이보다 느려질 수 있다. 최대 이하여야 한다. **62번째(인덱스 61)**. |
| Spawn Maximum Speed (board widths/s) | 1.8 | 0–10 / 보드 폭/초 | 튀어나가는 속력의 상한. **63번째(인덱스 62)**. |
| Drum Count | AUTO (0) | 선택 AUTO·1–5 / 개 | 한 판에 놓이는 생성 북 수. **AUTO(값 0, 기본, #236)**는 판을 시작할 때 그 판 인원으로 정한다: 솔로·2·3명 1개, 4·5명 2개(`LabConfig.DrumsForPlayers`). 1~5를 고르면 인원과 상관없이 그 개수다. DEV에서는 숫자 칸 대신 `AUTO` `1`~`5` 버튼 6개(한 줄에 3개, 두 줄)로 고른다. 판 시작 때 Host가 북마다 받을 사람을 무작위로 뽑는다(북이 인원 이하이면 서로 다른 사람, 많으면 모두 하나씩 받은 뒤 나머지도 무작위). 북은 받은 사람 판의 가운데에서 시작하고, **북이 있는 사람만 생성**할 수 있다. 공유 방 설정에 포함된다. **75번째(인덱스 74)**. |
| Drum Radius | 0.6 | 0.1–1.5 / 월드 단위 | 북의 충돌 구 반지름이자 북 그림 크기. 기본은 예전 하단 생성 버튼의 화면 크기에 맞췄다(구슬 지름의 약 2배). 잡기 판정도 이 크기에 맞춰 넓어진다. 새 라운드부터 적용된다. **76번째(인덱스 75)**. |
| Drum Drift Speed (board widths/s) | 0.1 | 0–1 / 보드 폭/초 | 가만히 둔 북이 저절로 떠도는 최고 속력(실제 속력은 그 50~100% 사이에서 천천히 변함). 0이면 떠돌지 않는다. 밀리거나 튕긴 북은 구슬처럼 감속하다 이 속력까지 줄면 다시 떠돈다. 이웃에게 넘어가려면 바깥쪽 속력이 이 값의 1.5배를 넘어야 하며, 그보다 느리게 가장자리에 닿으면 되돌아온다. **77번째(인덱스 76)**. |
| Drum Drift Turning (per second) | 0.25 | 0–2 / 초⁻¹ | 떠도는 방향이 바뀌는 빠르기(부드러운 노이즈의 진행 속도). 높이면 자주 방향을 틀고, 0이면 같은 방향을 유지한다. 벽·옆 가장자리 0.15 보드 폭 안에서는 방향과 상관없이 멀어지는 쪽으로 튼다. **78번째(인덱스 77)**. |
| Drum Spin Strength | 1 | 0–3 / 배 | 북 그림 회전의 세기. 떠도는 길이 휘는 쪽으로 도는 회전(최대 초당 90° × 세기)과, 구슬·벽이 스치며 접점이 미끄러지는 방향으로 도는 회전(최대 초당 540°)에 모두 곱한다. 회전은 그림만 바꾸고 충돌·위치에는 영향이 없다. 0이면 돌지 않는다. **79번째(인덱스 78)**. |

판 높이와 반경은 별도 기하 검사를 통과해야 한다. #154의 종이판·하단 HUD 배치는 유지하고, `LabOrbAreaProfile`의 종이판 상하 윤곽과 HUD 불투명 상단 윤곽을 합성한 **실제로 보이는 곡선 영역**에서 구슬과 북의 원형 몸체가 전체 가로 구간·양끝 통로를 통과할 수 있어야 APPLY가 진행된다. 기울어진 경계는 실제 반지름과 선분의 거리를 반영한다. 곡선 물리를 위해 그림을 위로 옮기거나 축소하지 않으며 일괄 0.05 여유·수평 HUD 컷을 적용하지 않는다. 후보 `BoardHeight`로 계산만 하며 검증 중 카메라 배율은 바꾸지 않는다. 화면 비율·HUD 배치·윤곽 제어점에 따라 같은 값의 통과 여부가 달라질 수 있다. 근거는 `LabSceneController.ValidateBoardTuning`, `LabOrbAreaProfile.TryBuildShape(plateWorldRect, hudWorldRect, ...)`, `LabOrbAreaShape.CanFitRadius`다.

근거: `LabModel.TryAdvance`·`TryCombineAt`, `LabOrbBoard.Configure`·`EndDragAtWorld`·`FixedUpdate`·`BuildHorizontalWalls`, `LabOrbView.PlaySpawnRise`, `LabSceneController.ReconfigureBoard`.

### 북 멈춤 구간 — 91~98번째 설정 (#178)

옆 사람에게 넘긴 북이 받는 판 어디에서 멈출지를 Host가 넘길 때마다 정한다. 거리는 **북이 들어온 쪽 가장자리에서 잰 판 폭 비율**이다. 모두 정수이고 `Drum` 묶음에 있다.

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Drum Stop Near From (% of board from entry) | 20 | 정수 0–100 / % | 가까운 쪽 구간의 시작. **91번째(인덱스 90)**. |
| Drum Stop Near To / Middle From (%) | 40 | 정수 0–100 / % | 가까운 쪽 끝이자 가운데 시작. **92번째(인덱스 91)**. |
| Drum Stop Middle To / Far From (%) | 60 | 정수 0–100 / % | 가운데 끝이자 먼 쪽 시작. **93번째(인덱스 92)**. |
| Drum Stop Far To (%) | 80 | 정수 0–100 / % | 먼 쪽 끝. 폭발에 밀려 넘어온 북도 이 안에서 멈춘다. **94번째(인덱스 93)**. 네 경계는 앞에서 뒤로 커지거나 같아야 한다. |
| Drum Stop Near Chance (%) | 40 | 정수 0–100 / % | 가까운 쪽 구간에서 멈출 확률. **95번째(인덱스 94)**. |
| Drum Stop Middle Chance (%) | 35 | 정수 0–100 / % | 가운데 구간. **96번째(인덱스 95)**. |
| Drum Stop Far Chance (%) | 15 | 정수 0–100 / % | 먼 쪽 구간. **97번째(인덱스 96)**. |
| Drum Pass Through Chance (%) | 10 | 정수 0–100 / % | 멈추지 않고 그다음 사람에게 통과할 확률. 네 확률의 합은 반드시 100이어야 한다. **98번째(인덱스 97)**. |

Host는 구간을 확률로 뽑은 뒤 구간 안의 한 지점을 고르게 뽑는다(seed와 넘김 순번으로 정해져 모든 기기가 같은 결과). 앞 판을 실제로 통과해 온 북은 다음 판의 가까운 쪽 구간에서 반드시 멈추고, 폭발 충격파에 밀려 넘어온 북은 제 속도로 미끄러지며 보통 감속하되 먼 쪽 끝을 넘을 때만 더 세게 감속해 그 안에서 멈춘다. 받는 화면은 들어온 속도를 이어받아 남은 거리만큼만 감속해 지점에 선다. 너무 세면 들어오며 감속 상한(8 판 폭/초²)에 맞게 줄이고, 너무 느리면 1.2초 안에 도착하도록 올린다. 통과 북은 최소 0.9 판 폭/초로 반대편까지 간다. 이 시간·감속·통과 속도는 코드 상수이며 DEV로 바꾸지 않는다. 멈춘 뒤에는 보통처럼 떠돌고, 도착 중에 잡거나 폭발에 맞으면 도착이 끝난다. 확률 합이 100이 아니거나 경계 순서가 틀리면 DEV 적용을 통째로 거부하고, Inspector 배열이 잘못되면 기본값을 쓴다.

근거: `LabConfig.DrumStopBounds`·`DrumStopChances`, `LabModel.TryTransferAt`·`ArriveDrum`, `LabOrbBoard.StartArrival`·`Arrive`·`Blast`, `LabNetwork.OnEdgeCrossed`·`SyncBoard`.

### 미니맵 — 99~102번째 설정 (#181)

| 항목 | 기본값 | 허용 범위 / 단위 | 실제 효과 |
| --- | --- | --- | --- |
| Minimap (0 off, 1 on) | 1 | 정수 0–1 | 전투 화면 오른쪽 아래(오른쪽 이웃 동그라미 위)의 미니맵을 켜고 끈다. **99번째(인덱스 98)**. Inspector `Minimap Enabled`와 같은 값이다. |
| Minimap Size (%, 50 = default) | 50 | 정수 0–100 / % | 미니맵 크기. 기본 50이 가운데이고 그린 크기(128×116)의 절반(64×58)이다. 100이면 그린 크기, 0이면 보이지 않는다. Mac 3앱에서 확인해 50을 기본으로 정했다. 오른쪽 아래 모서리를 기준으로 커지거나 작아진다. **100번째(인덱스 99)**. Inspector `Minimap Size Percent`와 같은 값이다. |
| Minimap Move X (- left, + right) | 0 | 정수 -300–100 / UI 단위 | 기본 자리(오른쪽 이웃 동그라미 위)에서 좌우로 옮기는 거리. 화면 폭 390 기준. **101번째(인덱스 100)**. Inspector `Minimap Move X`. |
| Minimap Move Y (- down, + up) | 0 | 정수 -300–500 / UI 단위 | 기본 자리에서 위아래로 옮기는 거리. 화면 높이 844 기준. **102번째(인덱스 101)**. Inspector `Minimap Move Y`. |

미니맵은 플레이어마다 원 하나를 정다각형 꼭짓점에 놓고 이웃끼리 선으로 잇는다. 폰마다 "나"(갈색 원)를 아래 가운데에, 오른쪽 이웃(다음 번호)을 오른쪽 위, 왼쪽 이웃(이전 번호)을 왼쪽 위에 둔다. 북을 가진 사람의 원은 태극 무늬(`Resources/Minimap/Taegeuk.png`)로 덮이고, 북이 넘어가면 태극이 이웃 선을 따라 한 칸에 0.35초씩 미끄러진다(통과한 북은 중간 원을 거친다). 전투 중(Playing)에만 보이며 화면 표시일 뿐 판정에는 영향이 없다. 색·선 굵기·이동 시간은 `LabDrumMinimap` 컴포넌트 값이고 DEV로 바꾸지 않는다.

근거: `LabConfig.MinimapEnabled`·`MinimapSizePercent`·`MinimapMove`, `LabDrumMinimap.UnitPosition`·`Rebuild`·`SyncDrums`·`Advance`, `LabSceneController.Awake`.

## 6. 전투 카메라·3D 발사점

카메라와 발사점은 모두 요괴의 `target.transform.position`을 기준으로 각 플레이어 자리 방향에 놓인다. **카메라만** 바꾸면 충돌 물리는 바뀌지 않지만 보이는 궤적은 달라질 수 있다.

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Camera Radius | 8 | 0.001–1,000 / 월드 단위 | 요괴 중심에서 플레이어 전투 카메라까지의 수평 거리. 높이면 시점이 뒤로 물러난다. |
| Camera Height Offset | 0.65 | −1,000–1,000 / 월드 단위 | 요괴 중심에 대한 카메라 높이 차이. 카메라는 아래 `Camera Look Height Offset`의 지점을 바라본다. |
| Camera Look Height Offset | 0.8 | −10–10 / 월드 단위 | 카메라가 바라보는 지점의 높이를 요괴(피냐타) 중심에서 위아래로 옮긴다. 값을 올리면 화면에서 피냐타가 아래로, 내리면 위로 보인다. 카메라·발사 지점이 피냐타 중심을 따라가므로 피냐타 Transform을 옮겨도 화면 구도는 거의 같고, 구도는 이 값과 `Camera Height Offset`·`Camera Radius`로 바꾼다. 투척·명중 판정에는 영향이 없다. DEV 필드 57번째(인덱스 56). |
| Throw Origin Radius | 4 | 0.001–1,000 / 월드 단위 | 요괴 중심에서 공의 기본 발사점까지의 수평 거리. 높이면 날아가야 하는 거리가 늘어난다. |
| Throw Origin Height Offset | −0.5 | −1,000–1,000 / 월드 단위 | 요괴 중심에 대한 공의 초기 높이. 궤적과 바닥·요괴 접촉 위치에 영향을 준다. |
| Throw Origin Lateral Range | 1 | 0–1,000 / 월드 단위 | 손을 놓은 조합판의 가로 위치를 0–1로 환산해 3D 발사점을 좌우 `±Range`만큼 옮긴다. 0이면 중앙에서만 출발한다. 측면에서 놓아도 자동으로 요괴를 향해 조준하지 않는다. |

근거: `LabSeatCamera.LateUpdate`, `LabThrowMath.SeatPosition`·`LaunchPosition`.

## 7. 스와이프 판정·3D 투척

입력은 손을 놓기 직전 **0.12초**의 조합판 XY 위치 샘플에서 계산한다. 구슬 자체는 3D 물리 루트를 갖지만 이 단계의 손동작은 평면 좌표다. 손을 놓은 지점이 좌우 전달 경계이고 바깥 방향으로 움직이면 이웃 **전달 판정이 먼저**이며, 투척은 그다음이다. Combined 구슬만 투척할 수 있다. 아래 표의 `vₓ`·`vᵧ`는 각각 보드 폭/초 단위의 가로·위쪽 입력 속도다.

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Throw Minimum Swipe Speed | 0.8 | 0.001–100,000 / 보드 폭/초 | `vᵧ ≥ 이 값`이어야 투척한다. 높이면 약한 위쪽 동작은 판 위의 굴림으로 남는다. |
| Throw Minimum Swipe Distance | 0.12 | 0.001–1,000 / 보드 폭 | 최근 0.12초 샘플 구간의 이동 거리 하한. 전체 드래그 시작점부터의 거리가 아니다. 높이면 짧은 튕김이 투척으로 인식되기 어려워진다. |
| Throw Upward Dominance | 0.65 | 0–1,000 / 비율 | `vᵧ ≥ abs(vₓ) × 이 값`을 요구한다. 높이면 대각선·가로 동작이 투척보다 굴림으로 남기 쉽다. |
| Throw Maximum Swipe Speed | 6 | 0.001–100,000 / 보드 폭/초 | 측정된 손 속도 벡터를 먼저 이 크기로 **잘라서** 판정·발사에 사용한다. 더 빠른 스와이프가 곧바로 거절되는 값은 아니다. |
| Throw Minimum Upward Speed | 2 | 0.001–100,000 / 월드 단위/초 | 손동작으로 계산한 **발사각 조정 전** 상승 성분의 하한. 약한 입력에 주로 영향을 준다. Loft 보정·최종 속도 상한을 거친 뒤의 실제 Y 속도 하한을 보장하지는 않는다. |
| Throw Lateral Gain | 4.5 | 0.001–100,000 / 속도 변환 배율 | `vₓ`를 공의 좌우 초기 속도로 바꾼다. 높이면 같은 비스듬한 손동작에서 측면 이동과 빗나감이 커질 수 있다. |
| Throw Upward Gain | 2.1 | 0.001–100,000 / 속도 변환 배율 | `vᵧ`를 위쪽 초기 속도로 바꾼다. 높이면 대체로 더 높이 올라간다. 최소 상승 속도나 최종 속도 상한에 걸리면 차이가 줄어든다. |
| Throw Forward Gain | 2.5 | 0.001–100,000 / 속도 변환 배율 | `vᵧ`를 요괴 쪽 수평 초기 속도로 바꾼다. 높이면 더 빠르게 전진해 표적에 일찍 도달하며 포물선의 하강 부분이 덜 보일 수 있다. |
| Throw Maximum World Speed | 12.5 | 0.001–100,000 / 월드 단위/초 | 좌우·위·전진을 합친 3D **초기 속력**의 상한. 여기에 자주 걸리면 Gain이나 Power Exponent를 높여도 결과 차이가 작아진다. |
| Throw Gravity | 30 | 0.001–100,000 / 월드 단위/초² | 비행 중 공에만 아래 방향 가속을 매 물리 스텝 적용한다. 높이면 빨리 낙하한다. Unity 전역 `Physics.gravity`를 바꾸는 값은 아니다. |
| Throw Lifetime (seconds) | 4 | 0.001–86,400 / 초 | 명중이나 바닥 접촉이 없더라도 투사체를 MISS로 끝내는 최대 비행 시간. 바닥·요괴에 먼저 닿으면 즉시 결과가 난다. |
| Throw Radius | 0.165 | 0.001–100 / 3D 월드 단위 | 3D `SphereCollider`와 기본 구체 표시 반경. 높이면 화면 크기와 피격 판정 크기가 함께 바뀐다. |
| Throw Power Exponent | 1.2 | 0.25–3 / 지수 | 손 속력 반응 곡선. 기준 입력 속도 **1.5 보드 폭/초**에서 출력이 같고, 1이면 선형이다. 1보다 크면 기준보다 빠른 동작이 더 강해지고 느린 동작이 더 약해진다. 1보다 작으면 반대다. |
| Throw Loft Offset (degrees) | 0 | −30–30 / 도 | 계산된 상승·전진 발사각에 더한다. 양수는 같은 종방향 속력을 더 위로, 덜 앞으로 배분한다. 최종 발사각은 3°–87°로 제한된다. |
| Throw Air Damping | 0 | 0–5 / Rigidbody 선속도 감쇠값 | 비행 중 `linearDamping`. 높이면 전체 선속도가 줄어 사거리와 높이에 영향을 준다. 공을 좌우로 휘게 만드는 힘은 아니다. |

현재 발사 계산은 개념적으로 다음 순서다.

1. 손 속력 `s`에 대해 `shaped = 1.5 × (s / 1.5) ^ PowerExponent`를 구해 가로·세로 입력에 같은 비율로 적용한다.
2. `up = max(MinimumUpwardSpeed, shapedY × UpwardGain)`, `forward = shapedY × ForwardGain`, `lateral = shapedX × LateralGain`을 구한다.
3. `atan2(up, forward) + LoftOffset`을 3°–87°로 제한해 위·전진 성분을 다시 나누고, 전체 벡터 크기를 `MaximumWorldSpeed`로 제한한다.
4. 투사체 Rigidbody는 이 초기 속도로 출발한다. 이후 매 물리 스텝 자체 중력과 `linearDamping`을 받고, Host의 Collider 충돌 또는 수명 종료로 HIT/MISS가 결정된다. Host만 HP와 명중 보상을 확정하며 다른 기기의 투사체는 표시용이다.

**포물선 비교 팁:** `Forward Gain`만 올리면 목표에 너무 빨리 닿아 공이 거의 직선처럼 보일 수 있다. `Upward Gain`·`Forward Gain`·`Gravity`를 한 번에 모두 바꾸기보다 기준 조건을 기록한 뒤 하나씩 비교한다. `Power Exponent`는 세기 곡선, `Loft Offset`은 발사각, `Air Damping`은 비행 중 감속이므로 서로 다른 질문에 사용한다.

근거: `LabOrbBoard.ReleaseVelocity`·`EndDragAtWorld`, `LabThrowMath.IsThrowGesture`·`LaunchVelocity`, `LabProjectile.Prepare`·`FixedUpdate`·`Resolve`, `LabNetwork.SpawnHostProjectile`·`OnProjectileResolved`.

### 투구폼·직구 프로필 — 112~118번째 설정 (#190)

Inspector의 `Projectile throw form` 또는 DEV의 **Throw form** 묶음에서 Arc와 Straight를 비교한다. Inspector의 원본 `Throw Form` 기본값은 Arc라서 기존 궤적을 유지한다. Straight는 위쪽 스와이프 조건·가로 놓기 위치·좌우 조준·세기 변환·최대 초기 속력·카메라·자리 배치를 바꾸지 않고 출발점·상승각·중력만 다르게 적용한다. 직구 초기값은 **DEMO_TUNING_VALUE**다.

| DEV 입력 이름 | 순서 / 인덱스 | 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | --- | ---: | --- | --- |
| Throw Form (0 arc, 1 straight) | 112번째 / 111 | 0: Arc | 정수 0–1 | ARC/STRAIGHT 토글. Arc는 기존 `ArcVelocity` 결과를 그대로 사용한다. |
| Straight Origin Offset X | 113번째 / 112 | 0.35 | −5–5 / 월드 거리 | 구슬 소유자 자리의 화면 오른쪽으로 더하는 거리. 0이면 기존 가로 놓기 위치를 유지한다. |
| Straight Origin Offset Y | 114번째 / 113 | 0.4 | −5–5 / 월드 거리 | 기존 발사 위치에서 월드 위로 더하는 높이. |
| Straight Origin Offset Z | 115번째 / 114 | 0 | −5–5 / 월드 거리 | 해당 자리에서 타깃 쪽 수평 방향으로 더하는 거리. |
| Straight Pitch Offset (degrees) | 116번째 / 115 | 3 | −30–30 / 도 | 낮춘 상승각에 더하는 값. 음수는 아래 방향으로 던질 수도 있다. |
| Straight Loft Scale | 117번째 / 116 | 0.1 | 0–1 / 배율 | 기존 Arc 최종 속도의 상승각을 남기는 비율. 0이면 Pitch Offset만 남는다. |
| Straight Gravity Scale | 118번째 / 117 | 0.1 | 0–2 / 배율 | `Throw Gravity`에 곱하는 값. 0이면 중력이 없고, 현재 원본 기준 기본 직구 가속도는 30×0.1=3이다. |

직구 전용 6개 행은 숫자 입력과 슬라이더를 모두 제공한다. ARC에서는 비활성화하고 STRAIGHT에서는 활성화한다. 이 전환은 아직 APPLY하지 않은 **폼 입력값**으로 즉시 반영하며 원본 에셋이나 이미 적용한 세션 설정을 즉시 바꾸지 않는다. ARC로 돌려도 직구 6개 값은 버리지 않으며 DEFAULTS를 누르면 저장 원본의 값으로 되돌린다.

발사 위치는 `기존 LaunchPosition + 오른쪽×Offset.x + 월드 위×Offset.y + 타깃 쪽 수평 방향×Offset.z`다. Host가 **그 구슬 소유자의 실제 자리**를 사용하므로 Host의 로컬 카메라와 다인 플레이 자리 방향을 혼동하지 않는다. 위치를 옮긴 뒤 요괴 중심으로 자동 조준하지 않는다.

기존 Arc의 최종 초기 속도를 `vArc`, 그 속력을 `s`, 수평 단위 방향을 `h`, 실제 상승각을 `a=atan2(vArc.y, |vArc 수평 성분|)`라고 하면 직구 상승각은 `b=clamp(a×StraightLoftScale + StraightPitchOffsetDegrees, −30°, 60°)`다. 직구 초기 속도는 `s×(h×cos(b)+위×sin(b))`이며 기존 초기 속력과 수평 조준 방향을 유지한다. 직구 모드도 `Throw Power Exponent`, 기존 Gain, `Throw Loft Offset`을 거친 Arc 결과를 기준으로 한다. `Loft Scale=1`이어도 직구의 각도 제한이 있으므로 기존 포물선 자체는 ARC에서 비교한다.

`EffectiveThrowGravity`는 Arc일 때 기존 `ThrowGravity`, Straight일 때 `ThrowGravity×StraightGravityScale`다. 공은 발사 순간 중력·수명을 별도로 캡처하며 초기 위치·속도·충돌 반경·감쇠도 발사 시 설정한다. 비행 중 Inspector나 DEV 초안을 바꿔 이미 날아가는 공을 꺾지 않는다. 보이는 구슬과 실제 SphereCollider는 동일한 물리 루트에서 이동하고 기존 궤적·피격·색칠 경로를 재사용한다.

솔로는 START/RESTART SOLO, 공유 방은 Host의 CREATE ROOM/APPLY TO ROOM/공동 일시정지 뒤 APPLY & RESTART로 적용한다. **200개 전체**를 검증·지문화한 설정을 참가자에게 전파하고 확인받은 뒤 시작한다. 프로토콜은 **5662**이며 Host는 확정한 발사 위치·속도를 Client에 보내고 Client는 공유 설정의 같은 중력을 사용한다. Client가 직접 모드를 바꾸지 않는다. Inspector 변경도 새 판 시작 시점에 적용해 비교한다.

Inspector의 비정상 enum은 Arc로 보정하고 오프셋·각도·배율의 유효 Getter는 범위 제한과 유한수 대체값을 사용한다. DEV/네트워크는 잘못된 값 하나라도 있으면 200개 전체 적용을 거절한다. 극단적인 오프셋이나 음수 발사각은 화면 밖 출발·바닥 접촉·빗나감을 만들 수 있으며 자동 보정 대상이 아니다.

근거: `LabConfig.ThrowForm`·`StraightOriginOffset`·`StraightPitchOffsetDegrees`·`StraightLoftScale`·`StraightGravityScale`·`EffectiveThrowGravity`, DEV 인덱스 111~117, `LabDeveloperMode.RefreshThrowFormInputs`, `LabThrowMath.LaunchPosition`·`LaunchVelocity`·`ArcVelocity`, `LabProjectile.Prepare`·`FixedUpdate`·`Update`, `LabNetwork.SpawnHostProjectile`, `LabSessionSettings.Fingerprint`. 이 절은 코드 연결 설명이며 실제 실행 결과를 단정하지 않는다.


### 박 줄의 주기 움직임·탄성 — 119~124번째 설정

`Gourd` 묶음의 아래 6개 행은 숫자 입력과 슬라이더를 함께 제공한다. 기본값은 `LabConfig.asset`에 있으며 DEV에서 조정한 값은 **START/RESTART SOLO**, **CREATE ROOM**, **APPLY TO ROOM**, 공동 일시정지 후 **APPLY & RESTART**로 새 판에 적용된다. 이미 진행 중인 판에는 즉시 반영되지 않는다.

| DEV 입력 이름 | 순서 / 인덱스 | 기본값 | 범위 | 효과 |
| --- | --- | ---: | --- | --- |
| Gourd Rope Cycle Amplitude (world units) | 119번째 / 118 | 0.45 | 0–1 | 줄이 스스로 늘고 줄어드는 거리. 0이면 주기 움직임을 끈다. |
| Gourd Rope Cycle Period (seconds) | 120번째 / 119 | 3 | 0.5–10초 | 늘었다 줄어드는 한 주기. 작을수록 빠르다. |
| Gourd Rope Hit Bounce | 121번째 / 120 | 0.32 | 0–2 | 명중 때 추가로 아래로 당기는 세기. |
| Gourd Rope Spring | 122번째 / 121 | 42 | 0–100 | 명중 후 원래 길이로 돌아가는 복원력. |
| Gourd Rope Damping | 123번째 / 122 | 2 | 0–20 | 명중 후 상하 왕복의 감쇠. 작을수록 오래 흔들린다. |
| Gourd Rope Maximum Hit Extension | 124번째 / 123 | 0.65 | 0–3 월드 단위 | 명중으로 추가 늘어나는 길이의 상한. 0이면 명중 탄성을 끈다. |

주기 움직임과 명중 탄성은 합쳐져 **보이는 줄·박 모델·실제 충돌체**를 함께 움직인다. Host의 주기 위상과 명중 탄성 상태는 Snapshot으로 전달해 Client 표시를 보정한다. Host의 충돌 판정이 기준이므로 실제 다중 기기에서 빠른 움직임의 시야·명중 일치를 별도로 확인해야 한다.

### 강한 측면 타격의 박 기울기 — 125~128번째 설정

같은 방향에서 더 빠르게 날아온 공은 박에 더 큰 흔들림 속도를 준다. 박이 기울면 모델과 실제 충돌체의 아랫면도 함께 기울어져 다른 플레이어가 공략할 수 있다. 기본 최대 기울기는 40도다. 아래 값은 `Gourd` 묶음에 있으며 줄 설정과 같이 새 판을 시작할 때 Host가 공유한다.

| DEV 입력 이름 | 순서 / 인덱스 | 기본값 | 범위 | 효과 |
| --- | --- | ---: | --- | --- |
| Gourd Swing Impact Strength | 125번째 / 124 | 12 | 0–20 | 타격 속도를 흔들림 속도로 바꾸는 배율. |
| Gourd Swing Maximum Angle (degrees) | 126번째 / 125 | 40 | 1–75도 | 수직에서 기울어질 수 있는 최대 각도. |
| Gourd Swing Return Strength | 127번째 / 126 | 10 | 0–100 | 중앙으로 돌아오는 힘. |
| Gourd Swing Damping | 128번째 / 127 | 1.4 | 0–20 | 좌우 흔들림이 사라지는 속도. 작을수록 공격 기회가 오래 남는다. |

최대 각도에 닿으면 더 세게 맞아도 그 이상 기울지 않는다. 카메라에서 실제 아랫면이 보이고 맞는 시간은 화면과 기기에서 조정해야 한다.

### 줄·기울기 값을 DEV에서 조정하는 순서

1. Editor 또는 Development 앱에서 **DEV → Gourd 제목**을 눌러 펼친다. 박 줄 6개(index118~123)와 기울기 4개(index124~127)는 모두 **숫자 입력·슬라이더**로 조정한다. 패널 안을 스크롤하면 마지막 `Gourd Swing Damping`까지 접근할 수 있다.
2. 솔로는 **START/RESTART SOLO**, Host 방 생성 전은 **CREATE ROOM**, 같은 방 로비는 **APPLY TO ROOM**을 누른다. 공유 전투 중에는 공동 일시정지 후 Host의 **APPLY & RESTART**로 참가자 확인을 받고 새 판을 시작한다. Client는 Host가 확정한 값을 사용한다.
3. 입력·슬라이더 조작은 폼만 변경하며 적용 버튼을 눌러야 새 판에 반영한다. **DEFAULTS**도 폼만 복원한다. 미적용 상태로 닫고 다시 열면 마지막 적용값을 다시 읽는다. 원본 에셋에는 저장하지 않는다.

상하 주기만 끄려면 `Cycle Amplitude`를 0으로 둔다. 명중 상하 탄성을 끄려면 `Maximum Hit Extension`을 0으로 둔다. 타격 기울기의 세기는 `Swing Impact Strength`, 한계는 `Swing Maximum Angle`로 비교하며, 몸체 Y축 회전은 별도 **Target spin** 항목이다. 각 효과는 서로 다른 설정이므로 한 항목씩 바꿔 비교한다.

일시정지 때는 Host의 줄 위상·탄성 상태와 몸체 Y축 각도·속도를 확정해 같은 정지 스냅샷을 여러 번 받아도 위치·회전이 계속 보간되지 않는다. 재개 뒤에는 보존한 속도에서 기존 시뮬레이션·진행 중 보정을 이어간다. 실제 검증 범위는 `docs/VALIDATION.md`의 #195 보완 기록을 참조한다.

### Experiment #196 — 물감 내부 입자·농담 (129~132번째)

사용자 승인으로 main에 반영하는 기본 OFF 선택 표현이다. 현재 질감4개는128~131번이며 다른 게임 수치를 바꾸지 않는 표현 설정이다. 단일 `LabConfig.asset`의 기본은 OFF이며 DEV는 실행 중 복사본만 조정한다. #196 당시 공유 전체는133개·프로토콜5633이었다. 이 문서의 현재 #268·#272 통합본은200개·5662이며 같은 버전끼리 접속한다.

| DEV 입력 이름 | 인덱스 | 기본값 | 범위 | 효과 |
| --- | ---: | ---: | --- | --- |
| Paint Grain | 128 | OFF(0) | OFF/ON | 도색 내부 질감 실험. OFF는 기존 셰이더의 단색 합성식을 사용한다. |
| Paint Grain Size | 129 | 1 | 0.25~4 | 클수록 큰 입자. 박 로컬 방향에 고정되며 도색 계산 해상도와 별개다. |
| Paint Grain Strength | 130 | 0.3 | 0~1 | 작은 어두운 입자의 강도. |
| Paint Mottle Strength | 131 | 0.18 | 0~1 | 큰 단위로 불규칙한 밝기·농담이 바뀌는 강도. |

DEV의 Gourd를 펼쳐 토글/숫자/슬라이더로 변경한 뒤 솔로 시작·재시작, 방 생성, Host 로비 적용 또는 공동 정지 후 Host 적용·재시작을 사용한다. 값을 입력하기만 해서는 원본 에셋을 저장하거나 진행 중 판을 바꾸지 않는다. 두 강도가0이면 ON이어도 원래 색이다.

질감은 셀 RGBA를 바꾸지 않고 도색 RGB의 밝기만 변조한다. 미도색의 종이색·알파·진행률·연결 영역·성장 공급·색 소유·100% 완료는 유지한다. 공통 고정 노이즈를 박 로컬 좌표에 사용하며 시간·화면 좌표·기기별 난수를 쓰지 않는다. 완성품 미리보기는 적용된 PropertyBlock을 복제한다. 표면 요철·새 조명·Decal·외곽 비산점은 이번 실험 범위가 아니다.

근거: `LabConfig.PaintGrainEnabled/Size/Strength`·`PaintMottleStrength`, `LabGourdPaint.RefreshPaintMap`, `LabGourdPaint.shader`의 `GrainNoise/GrainColor`, `LabGourdResultPreview.CopyHierarchy`. 실행 여부는 `docs/VALIDATION.md` #196 기록과 분리한다.

### LEGACY 최초 얼룩 모양 — #198에서 유지한 활성 설정

현재 LEGACY의 최초 얼룩에 사용하는 Style와 Edge Raggedness 두 항목을 유지한다. 기본은 ORIGINAL이며 실제 패턴 기본값은 SPLATTER다. #246에서 현재 군집에 영향이 없는 옛 Flow Ratio·Drip Width·Drip Length·Gravity Weight와 과거 셀별 흐름 분기를 제거했다. 아래는 현재 활성 값이며 기본 밸런스나 기기 성능 검증값을 뜻하지 않는다.

| DEV 입력 이름 | 인덱스 | 기본값 | 범위 | 의미 |
| --- | ---: | ---: | --- | --- |
| Paint Growth Style | 132 | ORIGINAL(0) | ORIGINAL / SPREAD + FLOW | LEGACY 최초 얼룩의 모양을 선택한다. 재타격 군집은 공통 경로다. |
| Paint Edge Raggedness | 133 | 0.55 | 0~1 | Legacy 초기 얼룩의 비대칭 굴곡을 조절한다. #211에서는 마스크 군집의 본체 윤곽 선호 강도에도 관여한다. |

초기 면적·반복 공급량·전체 표면 성장 속도·메우기 비중·세로 우대는 유지한다. LEGACY의 Style0은 기존 초기 셀/RNG 선택을, Style1은 불규칙 초기 얼룩을 사용한다. 현재 재타격은 패턴에 맞는 지역 군집을 선택하며 실제 인접 새 셀만 예산을 소비한다.

Host는 새 성장 구간에 사용할 세계 아래 방향을 박 로컬 좌표로 변환한다. 모델의 `SetPaintFlowDirection(localDown,now)`는 먼저 이전 방향으로 지난 시간을 소비하고 새 방향을 저장한다. 일반 요청·Snapshot은 마지막 Host 샘플을 공유한다. 샘플은 초기 시작과 Host 프레임 경계에만 잡으며 Client는 성장/중력을 다시 계산하지 않는다. 고정 방향과 같은 샘플 시간표에서의 시간 분할 일치와, 서로 다른 FPS로 회전하는 박의 비트 단위 동일 결과를 구분한다. 후자는 이번 구현의 보장이 아니다.

현재 시간 성장은 `PaintCell/Consume`을 거쳐 새 미도색 셀 하나당 예산1을 소비한다. 기존 색·투척자와 공급 FIFO를 유지하며 마지막 실제 빈 셀을 칠해야100%다. 정지·재개와 제한시간 동시 완료 우선순위도 유지한다. 과거 #198의 셀별 흐름 실행 기록은 현행 군집 검증으로 승계하지 않는다.

### 요괴 피격 회전

세 항목은 기존 57개 설정 뒤에 추가됐다. 원통의 실제 MeshCollider에 유효한 투사체가 맞으면 Host는 **피격점과 몸체 중심 사이의 거리 중 현재 회전축에 수직인 성분 × 투사체 진행 방향**으로 몸체의 Y축 회전 충격량을 계산한다. 중심에 가까운 명중은 회전이 작고, 좌우 빗맞힘은 반대 방향으로 돈다. 각도는 한 바퀴에서 잘리지 않으며 속도만 제한·감쇠한다. 기존 X/Z 흔들림·상하 바운스와 피해 판정은 유지한다.

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Target Spin Strength | 80 | 0–200 / 회전 충격량 배율 | 같은 빗맞힘에서 처음 추가되는 Y축 각속도를 조정한다. 0이면 회전 충격이 없다. 58번째(인덱스 57). |
| Target Spin Damping | 1.2 | 0–10 / 초⁻¹ | 매 물리 스텝 각속도를 지수적으로 줄인다. 높이면 빨리 멈추고, 0이면 자동 감쇠하지 않는다. 59번째(인덱스 58). |
| Target Spin Maximum Speed (deg/s) | 720 | 30–1,440 / 도/초 | 연속 명중을 포함한 Y축 각속도의 상한. 60번째(인덱스 59). |

Host의 확정 피격 패킷은 부호 있는 회전 충격량을 전달하며 스냅샷은 풀린 각도와 각속도를 보정한다. 다른 기기는 피격 위치로 회전력을 다시 계산하지 않는다. 로비 복귀와 새 판에서는 회전 상태를 초기화하고, 일시정지 중에는 물리 업데이트가 멈춘다. 저장 Scene의 `Assets/Lab/Art/Targets/TargetSpinGradient.png`에는 비대칭 색 변화와 화살표가 있어 실제 회전 방향을 눈으로 확인할 수 있다. 이는 요괴의 최종 그래픽이 아닌 진단용 재질이다.

근거: `LabHangingTarget.ApplyHostImpact`·`FixedUpdate`·`ReconcileYaw`, `LabNetwork.OnProjectileResolved`·`PublishSnapshot`, `LabConfig.DeveloperFields`.

## 8. 생성 확률·아이템

GENERATE 한 번마다 Host가 아래 6개 확률로 종류를 정한다. 매 생성은 독립 추첨이며 같은 극성이 이어져도 보정하지 않는다. 결합 구슬의 오행 속성(화·수·목·금·토)은 결합 순간 같은 확률로 정해지며 이 패널로 조정하지 않는다.

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Yin Chance (%) | 50 | 정수 0–100 / % | 생성 결과가 음(Yin)일 확률. |
| Yang Chance (%) | 50 | 정수 0–100 / % | 생성 결과가 양(Yang)일 확률. #177부터 북은 기본으로 음·양만 낸다. |
| Relay Orb Chance (%) | 0 | 정수 0–100 / % | 릴레이 구슬 아이템 확률. 아이템 규칙 코드는 남아 있어 값을 올리면 다시 나온다. |
| Secret Box Chance (%) | 0 | 정수 0–100 / % | 시크릿 상자 아이템 확률. |
| Potion Chance (%) | 0 | 정수 0–100 / % | 회복물약 아이템 확률. |
| Bomb Chance (%) | 0 | 정수 0–100 / % | 북에서 나오는 폭탄 확률. 북 폭탄도 박 폭탄과 같은 규칙(잠긴 채 생성, 다 풀면 던질 수 있음, #212)을 따른다. 6개 확률의 합은 반드시 100이어야 한다. |
| Item Lifetime (seconds) | 15 | 0.1–86,400 / 초 | 보관 중인 아이템의 수명. 생성 시점부터 세며 이웃에게 전달해도 초기화되지 않는다. 잠긴 폭탄에게는 도화선이며(밧줄 그림은 마지막 3초 전까지 타 들어감), 끝나면 그때 보유자의 판에서 터진다. 해금된 폭탄은 이 값 대신 해금 순간부터 `Orb Lifetime`을 센다(#212). |
| Item Unlock Swipes Min | 2 | 정수 0–20 / 회 | 아이템(#212부터 폭탄 포함)이 생성될 때 정해지는 잠금 횟수의 하한. 잠금은 **이웃 화면으로 넘길 때마다** 1씩 준다. |
| Item Unlock Swipes Max | 4 | 정수 0–20 / 회 | 잠금 횟수의 상한. Min ≤ Max여야 한다. Min·Max를 모두 0으로 두면 아이템이 처음부터 해금된 상태로 생긴다. |
| Potion Heal (HP) | 10 | 정수 0–300 / 개인 HP | 해금된 회복물약을 더블 탭했을 때 사용자의 개인 HP에 더하는 양. `Player Max HP`를 넘지 않는다. HP가 가득 차 있거나 0이면 사용이 거절되고 물약은 남는다. |
| Relay x2 Damage Multiplier | 2 | 1–20 / 배 | 릴레이 구슬이 해금 후 한 번 더 넘겨져 `×2` 단계일 때 `Damage per Hit`에 곱하는 값. 해금 직후 `×1` 단계는 항상 1배다. |
| Relay MAX Damage Multiplier | 4 | 1–20 / 배 | 릴레이 구슬이 해금 후 두 번 넘겨져 `MAX` 단계일 때의 배율. 이후 더 넘겨도 오르지 않는다. |
| Bomb Damage Multiplier | 3 | 1–20 / 배 | 폭탄을 던져 명중했을 때 `Damage per Hit`에 곱하는 값. #212부터 해금된 폭탄을 던져 맞히면 다시 쓰인다(기본 3배 = 피해 60). 잠긴 폭탄은 던질 수 없다. |
| Bomb Blast Radius (board widths) | 0.4 | 0–2 / 보드 폭 | 폭탄이 터질 때 충격파가 닿는 거리. 폭탄 중심에서 이 거리 안에 중심이 있는 구슬·북·잡고 있던 구슬을 밀어낸다. 0이면 밀지 않고 이펙트만 나온다. 이펙트의 흰 원이 이 범위까지 퍼진다. **81번째(인덱스 80)**. |
| Bomb Blast Strength (board widths/s) | 9 | 0–20 / 보드 폭/초 | 폭탄 바로 옆이 밀려나는 속도. 범위 끝으로 갈수록 0까지 줄고, 원래 움직이던 속도에 더해진다. 9면 범위 중간(판 폭 20%)의 구슬도 바닥 감속으로 멈추기 전 판 폭의 약 5배를 갈 만큼 세서, 범위 안 구슬은 대부분 옆 가장자리를 넘어 이웃에게 간다. #167에서 3으로 시작해 화면 확인 후 9로 올렸다. **82번째(인덱스 81)**. |

아이템은 음·양과 조합되지 않는다. 해금된 **회복물약·시크릿 상자**는 같은 구슬을 0.35초 안에 두 번 탭해 그 자리에서 사용한다(한 번의 탭은 0.3초 이내, 이동 거리 구슬 반지름의 절반 이하). 시크릿 상자는 양옆에 속성 구슬 2개를 만들고 새 `Orb Lifetime`을 주며 보관 칸 하나가 더 필요하다. 해금된 **릴레이 구슬**은 Combined처럼 위로 그어 던진다. **폭탄**은 다른 아이템처럼 잠긴 채 생기며 몸통의 빨간 숫자가 남은 해금 횟수다(#212). 잠긴 동안은 던질 수 없고 넘겨도 도화선이 다시 시작되지 않는다. 도화선은 `Item Lifetime`이며, 그림의 밧줄은 마지막 경고 3초를 뺀 시간 동안 끝부터 꼭지까지 타 들어가고 끝의 불꽃과 치지직 소리가 함께 난다. 잠긴 채 끝나면 그때 보유자의 판에서 터져 충격파로 주변을 밀어낸다(아무것도 사라지지 않음). 마지막 잠금을 푸는 전달에서 불이 연기와 함께 꺼지고 무지개 테두리가 돌며, 해금 순간부터 `Orb Lifetime` 동안 Combined처럼 위로 던질 수 있는 공격 구슬이 된다(빗나가거나 수명이 끝나도 터지지 않음). 터질 때의 진동은 `Sound and haptics` 칸의 `Bomb Haptic Style`(83번째, 기본 2 = Heavy)·`Bomb Haptic Intensity`(84번째, 기본 1, 0이면 끔)로 정하고, 몇 초 남았을 때부터 깜빡일지·빠르기·색은 `LabOrbAppearance`의 Inspector(`Bomb Fuse Warning`, 기본 3초·초당 2→8회)에서 정한다. 폭탄 그림(몸통 `ItemBombBody`·꼭지 `ItemBombFuse`)의 크기·꼭지 방향, 밧줄 도화선의 길이(`Bomb Rope Length`, 기본 2)·색·굵기, 해금 횟수 글씨의 색·크기도 같은 Inspector의 **Bomb art**에서 정한다. 폭탄은 가만히 두면 북의 `Drum Drift Speed`·`Drum Drift Turning`으로 떠돌고 혼자서는 이웃 화면으로 넘어가지 않으며, 움직인 만큼 굴러 꼭지가 공 둘레를 돈다(몸통 뒤로 숨지 않음). 피해 배율의 결과는 반올림한 정수이며 최소 1이다. 1인 솔로에는 이웃이 없어 잠긴 아이템이 해금되지 않으므로, 효과를 혼자 시험하려면 `Item Unlock Swipes Min`·`Max`를 0으로 둔다(이때 폭탄은 처음부터 해금돼 던질 수 있고 터지지 않는다).

근거: `LabConfig.GenerationChances`, `LabModel.TryGenerate`·`TryCombineAt`·`TryTransferAt`·`TryUseItemAt`·`TryAdvance`·`ConfirmHit`, `LabOrbBoard.EndDragAtWorld`, `LabOrbItemMarker.Show`, `LabNetwork.SyncBoard`.

### 무거운 폭탄 — 142~145번째 설정 (#212)

DEV의 `Item orbs` 묶음 끝에 있다. 네 값 모두 폭탄에만 쓰이며 구슬·북·다른 아이템의 움직임과 위로 던지는 투척 세기는 바꾸지 않는다.

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Bomb Drag Lag (seconds, 0 = sticks to finger) | 0.12 | 0–1 / 초 | 142번째(인덱스 141). 끄는 폭탄이 손가락을 따라오는 시간. 이 시간이 지날 때마다 손가락과의 간격이 약 63%씩 줄어든다(50ms 뒤 약 1/3). 0이면 구슬처럼 손가락에 바로 붙는다. 클수록 묵직하지만 빠른 손놀림에서 손가락과 멀어진다. |
| Bomb Release Speed (x orb) | 0.75 | 0.05–1 / 배 | 143번째(인덱스 142). 손을 놓을 때 미끄러지는 속도 = 같은 손짓으로 구슬이 받는 속도 × 이 값(최대 속도 제한도 같은 배율). 작을수록 옆 사람에게 넘기려면 더 세게 그어야 한다. 해금된 폭탄을 위로 던지는 투척에는 쓰지 않는다. |
| Bomb Slide Deceleration (x orb) | 1.3 | 1–5 / 배 | 144번째(인덱스 143). 미끄러질 때의 감속 = `Orb Floor Deceleration` × 이 값. 플레이어가 놓은 폭탄은 멈출 때까지 미끄러진 뒤 떠돌기로 돌아가고(#237), 폭발에 밀린 폭탄은 떠돌기 속도까지 줄면 북처럼 떠돈다. 클수록 판 위에서 빨리 선다. |
| Bomb Mass (x orb) | 4 | 1–20 / 배 | 145번째(인덱스 144). 폭탄 `Rigidbody`의 질량(구슬 = 1). 클수록 굴러온 구슬과 부딪혀도 덜 밀리고 구슬 쪽이 튕겨 나간다. 폭발 충격파는 속도를 직접 주므로 질량과 무관하다. |

#237에서 놓기 속도 0.5 → 0.75, 감속 2 → 1.3으로 바꿨다. 미끄러지는 거리는 "놓기 속도² ÷ 감속"에 비례해 구슬의 12.5%에서 약 43%가 되었다(판 가운데에서 가장자리까지 보내려면 구슬을 판 끝까지 보내는 스와이프의 약 1.5배 세기). 비교 후보: 0.85/1.15(약 63%), 0.9/1.1(약 74%). 또 플레이어가 스와이프하거나 끌어서 놓은 폭탄은 멈출 때까지 미끄러지고, 바깥쪽으로 가장자리에 닿으면 속도와 상관없이 "스와이프"로 넘어간다(넘기기 막기는 그대로 튕김). 멈추거나 다시 잡히거나 폭발에 밀리면 이 효과가 풀리고, 혼자 떠도는 폭탄은 지금처럼 느리면 가장자리에서 되돌아온다.

근거: `LabConfig.BombDragLag`·`BombReleaseSpeed`·`BombSlideDeceleration`·`BombMass`, `LabOrbBoard.DragToWorld`·`EndDragAtWorld`·`Drift`·`UpsertOrb`, 가장자리 전달은 `LabOrbBoard`의 `PlayerSent`(#237).

### 폭발 패널티 — 146~148번째 설정 (#213)

DEV의 `Item orbs` 묶음 끝, 무거운 폭탄 다음에 있다. 폭탄 충격파에 밀린 구슬(북·폭탄 제외)에만 쓰인다.

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Blast Burn Seconds | 4 | 3–5 / 초 | 146번째(인덱스 145). 밀린 구슬에 노란 불이 붙어 있는 시간. Host가 불붙인 시각부터 Host 시계로 세므로, 옆 사람에게 넘어간 구슬도 받은 화면에서 남은 만큼만 탄다. 불은 표시일 뿐 규칙에 영향이 없다. |
| Blast Shrink Scale (x radius) | 0.5 | 0.2–1 / 배 | 147번째(인덱스 146). 밀린 구슬의 반지름 배율. 사라질 때까지 유지되며 보이는 크기, 다른 구슬과 부딪히는 범위, 잡히는 범위(반지름의 1.25배), 던졌을 때 투사체의 몸체·그림(박에 맞는 범위)에 모두 쓰인다. 1이면 줄지 않는다. 새로 작아지는 구슬부터 적용된다. |
| Small Orb Paint Share (x paint) | 0.5 | 0–1 / 배 | 148번째(인덱스 147). 작아진 구슬(또는 그것으로 만든 결합 구슬)이 박에 맞았을 때 처음 튀는 양과 다시 맞혔을 때 퍼지는 양에 곱한다. 0이면 칠하지 않는다. Crack 모드의 피해량은 바꾸지 않는다. |

어떤 구슬이 맞았는지는 폭탄이 터진 판만 정확히 안다(기기마다 판 비율이 다름). 그래서 그 판이 밀어낸 구슬 ID를 `blastHit`로 Host에 보내고, Host가 3초 안에 그 사람에게 보낸 폭발인지·그 사람 판에 있는 날아가지 않은 구슬인지 확인해 `burnedAt`(불붙은 Host 시각)과 `small`을 기록한다. 폭발마다 보고는 한 번만 받는다. Host 자신의 판에서 터지면 스냅샷을 만드는 도중이므로 바로 기록하고 다음 정기 스냅샷(0.1초 안)에 실어 보낸다. 작은 구슬이 하나라도 들어간 결합 구슬은 작고 남은 불을 이어받는다.

불 모양(뒤에서 솟는 불길 5개·윗가장자리 불꽃 3개·빛무리·불티 5개, 공 크기에 비례)과 작아질 때 0.3초 동안 줄어드는 연출은 코드에 고정돼 있으며 DEV로 조정하지 않는다. 불길은 구슬 중심 평면에 있어 불투명한 공이 뿌리를 가리므로 구슬 색은 그대로 보인다.

근거: `LabConfig.BlastBurnSeconds`·`BlastShrinkScale`·`SmallOrbPaintShare`, `LabOrbBoard.Blast`·`RadiusFor`, `LabNetwork.ReportBlast`, `LabModel.MarkBlasted`·`TryCombineAt`, `LabOrbState.BurnSecondsLeft`, `LabProjectile.Create`, `LabPaintSurface.RegisterHit`, `LabOrbBurn`, `LabOrbView.SetBurn`·`PlayShrink`.

### 결합 소리·햅틱 — 85~87번째 설정

| 순서 | 항목 | 기본값 | 입력 범위 | 효과 |
| --- | --- | --- | --- | --- |
| 85 (인덱스 84) | `Combine Sound Volume (50 = Inspector)` | 50 | 0~100 | 내가 요청한 결합을 Host가 승인했을 때 결합한 사람 기기에서 나는 소리의 크기. 아래 "DEV 소리 크기" 규칙을 따른다(Inspector 기본 0.8). 소리 파일은 `Assets/Lab/Resources/Audio/Combine`의 파일 중 하나를 무작위로(연속 같은 소리 없이) 재생하며, 파일이 없으면 소리가 나지 않는다. |
| 86 (인덱스 85) | `Combine Haptic Style (0-4)` | 0 = Light | 0~4 정수 | 결합 진동 종류. 0 Light, 1 Medium, 2 Heavy, 3 Soft, 4 Rigid. |
| 87 (인덱스 86) | `Combine Haptic Intensity` | 1 | 0~1 | 결합 진동 세기. 0이면 진동하지 않는다. `Haptics (0 off, 1 on)`이 0이면 함께 꺼진다. |

결합이 거절되면 소리·진동이 나지 않는다. Host는 승인 즉시, Client는 Host가 보내는 `combined` 알림을 받았을 때 재생한다. 아이폰 빌드에서만 실제로 진동한다.

### DEV 소리 크기 — 64~67번째, 85번째, 90번째 설정

BGM·생성·타격·빗나감·결합·도화선 소리 크기 6개(인덱스 63~66, 84, 89)는 DEV에서 **0~100**으로 입력하며 기본값은 **50**이다. 50은 Inspector(`Resources/LabConfig`)에 적힌 크기를 그대로 쓴다는 뜻이고, 실제 크기 = Inspector 값 × (DEV 값 ÷ 50)이다. 100은 Inspector 값의 2배, 25는 절반, 0은 소리 없음. DEV 값은 Inspector에 보이지 않으며 원본 에셋을 바꾸지 않는다. 기기별 일시정지 메뉴의 BGM/효과음 슬라이더가 여기에 한 번 더 곱해진다.

90번째(인덱스 89) `Fuse Sound Volume (50 = Inspector)`는 #177의 도화선 치지직 소리다(Inspector 기본 0.5). 내 판에 잠긴 폭탄이 타는 동안 반복하며(해금되면 불이 꺼져 멈춘다, #212), 가장 빨리 터질 폭탄의 마지막 3초에는 크기가 0.6배에서 1배로, 빠르기가 1.35배까지 오른다. 일시정지 중이거나 전투가 끝나면 멈춘다. `Assets/Lab/Resources/Audio/Fuse`에 소리 파일을 넣으면 그 첫 파일을, 없으면 코드로 만든 1초 반복음을 쓴다.

103번째(인덱스 102) `Bomb Sound Volume (50 = Inspector)`는 #183의 폭탄 "뻥이요" 소리다(Inspector 기본 0.9). 내 판에서 폭탄이 터질 때 그 판 주인 기기에서만 한 번 난다. `Assets/Lab/Resources/Audio/Bomb`의 파일 중 하나를 무작위로(연속 같은 소리 없이) 재생하며, 파일이 없으면 소리가 나지 않는다. 다른 소리 크기와 같이 50 = Inspector 값, 100 = 2배, 0 = 끔이다.

104번째(인덱스 103) `Bomb Sound (0 boom, 1 ppeong)`는 폭탄 소리 종류를 고르는 토글이다(기본 1 = PPEONG). BOOM은 `Audio/Bomb/Boom`의 일반 폭발음, PPEONG은 `Audio/Bomb/Ppeong`의 뻥튀기 기계 소리를 쓴다. 고른 쪽 파일이 없으면 다른 쪽을 쓴다. 팀 플레이 테스트 뒤 하나로 정할 예정이며, Inspector `Bomb Sound Style`과 같은 값이다.

#201: 현재136·137번째(인덱스135·136) `Chuimsae Chance (%)`·`Chuimsae Min Gap (generates)`는 구슬 생성 소리에 섞이는 추임새(얼쑤·얼씨구)의 빈도를 정한다. 생성할 때마다 Chance(기본 15%) 확률로 장구 대신 추임새가 나고, 한 번 나온 뒤에는 Min Gap(기본 5번)만큼 장구만 나서 연달아 나오지 않는다(기본값이면 평균 약 9~10번에 한 번). 둘 다 정수이고 Chance 0이면 추임새를 끈다. 이전의 "세 번째마다 고정" 방식(`voiceEvery`)을 대체한다. `Sound and haptics` 칸에서 `Generate Sound Volume` 바로 아래에 보이며, Inspector `Chuimsae Chance Percent`·`Chuimsae Min Gap`과 같은 값이다. #201 당시 기존 0~139번을 유지하고 끝에 2개를 추가해 총 142개·프로토콜5636으로 공유했다. 현재는200개·5662/토큰23의 같은 빌드끼리 접속한다. 코드: `LabGenerateSound.ShouldPlayVoice`·`PlayNext`.

#172 아이폰 확인 뒤 진동 기본값을 정리했다: 생성 Medium·1, 타격 Heavy·1, 아이템 타격 Heavy·1, 폭탄 Heavy·1, 결합 Light·1. 세기 차이는 진동 종류(Light < Medium < Heavy)로 낸다.

### 박 색상 콤보 실험 — 156~165번째 설정 (#222, #241)

DEV `야광귀` 묶음의 위쪽 `── 콤보 ──` 아래 11줄이다(#271의 `야광귀 셈법` 포함)(#268에서 `Combo` 묶음을 야광귀 묶음으로 옮김, 번호는 그대로). 첫 토글은 기본 OFF다(#241에서 OFF → ON, #266에서 다시 OFF). 켜면 아래 시간 값대로 동작한다. OFF면 콤보가 예약되지 않고 공 크기/칠 면적 배율도 늘 1이라 기존 플레이와 같다. 시각은 모두 Host 전투 시계(일시정지 제외) 기준이다. 묶음 안 표시 순서는 인덱스155, 156, **164**, 157~163이다(예고를 첫 시작 바로 아래에 둠). 기본값은 코드와 `LabConfig.asset`에 같은 값으로 저장되어 있다.

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Gourd Color Combo (0 off, 1 on) | OFF | 토글 | 156번째(인덱스 155). 켜면 전투 중 박이 4칸 색 콤보(화/수/목/금/토 중 무작위, 반복 허용)를 낸다. 색은 Host가 라운드 seed로 뽑아 스냅샷으로 모든 기기에 같게 보낸다. |
| 야광귀 셈법 (0 off, 1 on) | 켬 | 끔·켬 | 192번째(인덱스 191, #271; #272 지귀·#268 섬광 뒤). 묶음에서 콤보 토글 바로 아래. 켜면 예고 동안 박 옆 말풍선이 남은 초를 `넷!` `셋!` `둘!` `하나!`로 세고(넷까지밖에 몰라 4초보다 길면 다시 넷부터), 맞힌 칸 위에 그 칸의 수(하나~넷)가 톡 튀어나와 남으며, 넷 다음 칸은 패널 밖에 흐린 `?`로 매달린다. 예고 동안 말풍선은 하나만 떠 있고 글자만 바뀐다. 성공 문구는 그대로 `CLEAR!`다. 화면 표시만 바뀌고 규칙은 그대로다. 끄면 예전 `COMBO! 4 3 2 1`. |
| Combo First Start (seconds) | 50 | 1–99 / 초 | 157번째(인덱스 156). #257에서 20 → 50. 전투 시작 후 첫 콤보 4칸이 나오는 시각. 예고는 이 시각 직전에 온다. |
| Combo Warning (seconds) | 4 | 0–8 / 초 | 165번째(인덱스 164, #241). #271에서 3 → 4(야광귀 셈법이 1초에 하나씩 넷·셋·둘·하나). 야광귀 셈법이 켜져 있으면 패널 자리의 `COMBO!` 예고 없이 박 옆 말풍선만 수를 센다. 꺼져 있으면 4칸이 나오기 전 콤보 패널 자리에 `COMBO!`와 남은 초(3 → 2 → 1)를 보여주는 시간. 예고 중에는 색을 숨기고 맞힌 구슬을 판정하지 않으며 SMALL/BIG 배지도 잠시 숨긴다. 0이면 예고 없음. 첫 시작 시각이 이보다 짧으면 예고도 그만큼 짧다. |
| Combo Repeat Interval (seconds) | 60 | 20–100 / 초 | 158번째(인덱스 157). #257에서 15 → 60(180초 전투에 50/110/170초). 앞 콤보가 **나온 시각**부터 다음 콤보까지의 간격. 다만 다음 콤보는 아무리 빨라도 앞 콤보가 끝난 시각 + `Combo Warning` 뒤에 나온다(예고를 다 보여주기 위해, #241). |
| Combo Time Limit (seconds) | 15 | 5–25 / 초 | 159번째(인덱스 158). #257에서 10 → 15(3~4명 기준 이상적 성공률 약 70~85%, 근거는 VALIDATION #257). 4칸을 모두 맞혀야 하는 제한 시간. 지나면 마감 시각에 `TIME UP` 실패. |
| Combo Penalty Duration (seconds) | 15 | 0–30 / 초 | 160번째(인덱스 159). #268에서 10 → 15, 범위 가운데가 기본값. 실패 후 페널티 시간. 0이면 페널티 없음. 페널티 중 다시 실패하면 더 늦은 끝 시각을 쓴다(겹쳐 늘지 않음). |
| Combo Penalty Size (x) | 0.1 | 0.01–0.19 / 배 | 161번째(인덱스 160). 페널티 중 던지는 공 반지름(충돌/모양/꼬리)과 그 공이 박에 칠하는 면적(처음 칠 면적/재타격 성장량)에 곱한다. 던진 순간의 배율이 착지까지 유지된다. 조합판 구슬 그림은 이 값 대신 `LabOrbBoard.comboSmallScale`(0.6, 그림만)로 줄인다(#257). 화면에는 숫자 없는 SMALL 게이지 배지로 보인다. |
| Combo Penalty Target (0 team, 1 thrower) | TEAM | 토글 TEAM / THROWER | 162번째(인덱스 161). TEAM은 팀 전체, THROWER는 틀린 색을 던진 사람만 페널티. 시간 초과는 틀린 사람이 없으므로 언제나 팀 전체. |
| Combo Reward Duration (seconds) | 15 | 0–30 / 초 | 163번째(인덱스 162). #268에서 10 → 15, 범위 가운데가 기본값. 성공 후 팀 전체 보상 시간. 0이면 보상 없음. |
| Combo Reward Size (x) | 2.5 | 1–4 / 배 | 164번째(인덱스 163). 보상 중 던지는 공 반지름과 칠 면적에 곱한다. 조합판 구슬 그림은 `LabOrbBoard.comboBigScale`(1.4, 그림만)로 키운다(#257). 화면에는 배지 대신 `CLEAR!`와 동시에 쏟아지는 부적(#268·#271, 임시 그림 `Resources/Combo/Bujeok.png`, 별의 2.7배 크기·절반 개수, 그림이 없으면 별, `LabComboRewardStars`)으로 보인다. |

판정: 박에 맞은 결합 구슬(색이 있는 구슬)만 순서대로 본다. 순서와 색이 맞으면 다음 칸, 다른 색이면 즉시 `WRONG COLOR`. 폭탄/릴레이는 무시한다. 성공하면 `CLEAR!`와 함께 팀 전체가 보상(큰 공/넓은 칠)을 받는다. 보상과 페널티는 마지막 결과 하나만 남는다: 성공은 진행 중이던 페널티를 끝내고, 실패는 페널티를 받는 사람의 보상을 끝낸다. 한 사람에게 둘이 겹치면 페널티가 우선이다.

근거: `LabConfig.GourdColorComboEnabled`/`ComboFirstStartSeconds`/`ComboRepeatIntervalSeconds`/`ComboTimeLimitSeconds`/`ComboPenaltyDurationSeconds`/`ComboPenaltySizeScale`/`ComboPenaltyThrowerOnly`/`ComboRewardDurationSeconds`/`ComboRewardSizeScale`/`ComboWarningSeconds`, `LabGourdCombo`, `LabModel.AdvanceCombo`/`OnComboFinished`/`ComboWaiting`, `LabSnapshot.comboWarningAt`/`comboNextAt`/`ComboWarningOn`, `LabModel.ThrowScaleFor`/`LaunchScaleOf`, `LabPaintSurface.RegisterHit(..., areaScale)`, `LabProjectile.SetRadiusScale`, `LabNetworkPacket.sizeScale`, `LabGourdComboHud`.

## 9. 입력 제약과 조정 실험 예시

**값 간 관계:** `Starting Stamina`, `Orb Generation Cost`, `Stamina Recovery Amount`, `Stamina Reward per Hit`은 모두 `Maximum Stamina` 이하여야 한다. `Orb Stop Speed ≤ Orb Maximum Release Speed`, `Throw Minimum Swipe Speed ≤ Throw Maximum Swipe Speed`, `Throw Minimum Upward Speed ≤ Throw Maximum World Speed`, 생성 확률 6개의 합 = 100, `Item Unlock Swipes Min ≤ Item Unlock Swipes Max`도 필수다. 관계 위반 시 전체 입력이 거절된다.

**기준 실험:** DEFAULTS를 불러와 APPLY로 솔로를 시작한다. 비교 시간을 확보하려면 전투 시간·구슬 수명을 늘리고 생성 비용을 낮추되, 변경값을 함께 기록한다. Yin과 Yang을 직접 겹쳐 Combined를 만든 후 판 중앙에서 위로 던진다. 투척 비교에서 아이템이 섞이지 않게 하려면 `Yin Chance`·`Yang Chance`를 50으로, 아이템 확률을 0으로 둔다. `SHOT HIT/MISS | 비행 시간 | peak +최고 상승 | range 수평 이동`와 Paint 진행도 또는 Crack HP 변화를 적는다. 같은 손동작을 여러 번 반복하고 **한 번에 한 값**만 바꿔 APPLY + RESTART SOLO로 비교한다. Console/Player 로그에는 `C6_LAB_THROW`(입력·발사 속도)와 `C6_LAB_PROJECTILE`(충돌·비행 지표)가 남는다.

**예시 질문별 첫 조정 대상**

| 관찰·목표 | 먼저 비교할 값 | 함께 확인할 것 |
| --- | --- | --- |
| 공이 너무 곧장 가서 하강이 안 보임 | Upward Gain ↑, Forward Gain ↓를 각각 시험 | peak·비행 시간·실제 HIT; 카메라가 정면이면 깊이감이 약할 수 있음 |
| 약한 손동작도 투척으로 처리됨 | Minimum Swipe Speed 또는 Minimum Swipe Distance ↑ | 조합판 평면 이동과 투척의 경계, 최근 0.12초 입력 |
| 강하게 던져도 거리 차이가 작음 | Maximum World Speed 상한 여부, Power Exponent | `C6_LAB_THROW`의 실제 launch 속도 |
| 구슬이 금방 사라져 조합하기 어려움 | Orb Lifetime ↑ | Raw/Combined의 각각 생성 시각, 전달 후 수명 |
| 구슬이 너무 오래 굴러다님 | Orb Floor Deceleration ↑ 또는 Orb Stop Speed ↑ | 충돌·전달 속도까지 영향을 받는지 |
| 측면에서 던진 공이 잘 빗나감 | Origin Lateral Range 또는 Lateral Gain 비교 | 자동 조준 기능은 현재 없음; 판정 반경 변경은 난이도도 바꿈 |
| 아이템이 너무 자주/드물게 나옴 | 아이템 4종 확률과 Yin/Yang 확률을 함께 조정 | 합 100 유지, 보유 한도에 아이템도 포함됨 |
| 넘긴 북이 너무 가깝거나 멀리 섬 | Drum Stop … 경계 4개 | 받는 사람이 북을 잡기 쉬운 거리 |
| 북이 너무 자주/드물게 통과함 | Drum Pass Through Chance ↓↑ (나머지 확률을 함께 맞춰 합 100) | 세 사람 중 한 명이 북을 오래 못 받는지 |
| 아이템이 해금되기 전에 사라짐 | Item Lifetime ↑ 또는 Item Unlock Swipes Max ↓ | 세 명이 실제로 넘기는 데 걸리는 시간 |
| 폭탄 충격파가 너무 약하거나 셈 | Bomb Blast Strength ↑↓, 넓이는 Bomb Blast Radius | 범위 안 구슬이 얼마나 이웃에게 넘어가는지 |
| 폭탄을 넘길 시간이 너무 짧거나 김 | Item Lifetime ↑↓ | 폭탄 도화선도 Item Lifetime을 쓰므로 다른 아이템 수명도 함께 바뀜 |
| 폭탄 해금이 너무 쉽거나 어려움 | Item Unlock Swipes Min·Max ↓↑ | 다른 아이템의 잠금도 함께 바뀜; 15초 안에 실제로 몇 번 넘길 수 있는지 |
| 폭탄을 옆으로 넘기기가 너무 쉽거나 어려움 | Bomb Release Speed ↓↑, Bomb Slide Deceleration ↑↓ | 세게 그어야 넘어가는 정도와 판 위에서 멈추는 거리 |
| 폭탄을 끌 때 너무 가볍거나 답답함 | Bomb Drag Lag ↑↓ | 손가락과 폭탄 사이 간격, 빠르게 끌 때의 체감 |
| 폭탄에 맞은 구슬의 패널티가 너무 세거나 약함 | Blast Shrink Scale ↑↓, Small Orb Paint Share ↑↓ | 작은 구슬을 잡고 박에 맞히기 어려운 정도, 색칠 진행 속도 |
| 폭탄 때문에 온 구슬을 알아보기 어렵거나 불이 너무 오래 남음 | Blast Burn Seconds ↑↓ | 넘겨받은 사람이 불을 보고 바로 알아차리는지, 판이 어지럽지 않은지 |
| 박 폭탄이 너무 잦거나 드묾 | Gourd Bomb Interval Min·Max ↑↓ (Max 0이면 끔) | 180초 한 판에 떨어지는 수(기본 6~9개)와 한 사람이 연달아 받는 빈도 |
| 넘기기 막기가 너무 잦거나 드묾 | Swipe Block Interval ↑↓ (0이면 끔) | 180초 한 판에 걸리는 수(기본 4번 안팎)와 협동 경로를 바꾸는 빈도 |
| 막기가 너무 길거나 짧음 | Swipe Block Seconds ↑↓ | 막힌 사람이 반대쪽으로 돌려 넘기는 데 걸리는 시간, 남은 초가 잘 읽히는지 |
| 막기와 박 폭탄 연출이 겹쳐 보임 | Swipe Block Bomb Gap ↑ | 말풍선 두 개가 이어서 뜨지 않는지, 막기가 너무 자주 미뤄지지 않는지 |
| 릴레이 MAX가 너무 약하거나 강함 | Relay MAX Damage Multiplier | Crack 요괴 HP/실제 피해 또는 Paint 공격값 기록, MAX까지 넘기는 시간; 무속성 릴레이는 색칠하지 않음 |
| 완성된 박을 여러 방향에서 봄 | Completed Gourd Viewer를 ON으로 적용한 새 판 | Paint 100% Victory와 일치한 맵에서 개별 관람·회전/줌, 종료/Retry 자원 정리. 전투 밸런스는 바뀌지 않음 |
| 공동 색칠의 진행 속도·초기 면적을 비교함 | Paint Initial Coverage · Repeat Growth · Growth Speed를 한 번에 하나씩 조정 | 목표는 100% 고정. 같은 지점 반복 명중 뒤 인접 성장, 모든 영역 합계 속도, GOAL 숫자/막대와 Host 결과를 함께 기록 |

이 표의 화살표는 **시험 방향**이지 검증된 추천값이 아니다. 잘못된 투척을 자동 보정하거나 요괴를 추적하는 기능은 현재 없다.

## 10. 이 패널로 조정할 수 없는 것·검증 범위

방의 **목표 인원**은 200개 설정 필드와 별도의 `ROOM PLAYERS` 슬라이더에서 방 생성 전에 선택한다. 좌석의 입장 순서, 네트워크 주소·포트, 생성 추첨 방식(독립 추첨), 오행 속성 확률(균등), 조합·전달의 승인 규칙, 북을 받을 사람의 무작위 배치, 손동작 샘플 창 **0.12초**, 타깃 Cylinder의 실제 크기·위치, 전투 카메라의 FOV와 화면 내 viewport, 박 폭탄의 대상·착지 위치 추첨과 낙하·말풍선 연출 시간, 구슬 소멸 예고의 번쩍임·연기(Inspector `LabOrbAppearance` Orb expiry), 폭탄 그림의 밧줄 길이·무지개·연기, 폭발 불의 모양·색과 축소 연출 시간, 북 도착의 감속 상한·최대 도착 시간·통과 속도는 200개 입력칸으로 조정하지 않는다. 색칠 승리 목표는 100% 고정이며 이 패널로 낮출 수 없다. DEV 솔로에는 이웃이 없어 화면 간 전달과 아이템 해금을 시험할 수 없다.

기존 PR #76 기준 문서는 해당 코드·Scene·설정 에셋과 당시 `docs/VALIDATION.md`의 증거를 **읽어 확인**해 작성됐다. 그 기록의 EditMode **12/12**, PlayMode **20/20** 및 Mac Development 앱 빌드는 이번 3D 조합판 전환의 통과 증거가 아니다. 이 전환의 Unity 컴파일·EditMode·PlayMode, Mac·iOS 빌드, 실제 조작은 새 실행 기록으로 구분해야 한다. 문서의 물리 용어를 갱신한 것만으로 실행 결과를 PASS로 보지 않는다.

이번 #191 통합본에서 EditMode 331/331·PlayMode 116/116, 실제 Mac Development Host + Release Client의 공유 ON 관람·같은 방 OFF 재시작과 완료 차단, 최종 일반 Mac 빌드를 새로 확인했다. 기기 터치·iOS·핫스팟·3~5기기·기본 밸런스는 NOT_RUN이다. `docs/VALIDATION.md`의 #191 항목에서 실행 방법·fixture·근거를 확인한다. #187·#189의 과거 PASS를 승계하지 않는다.

## 11. 코드·Scene 연결 지도

- 설정 필드·범위·일괄 검사: `Assets/Lab/Scripts/Core/LabConfig.cs` — `DeveloperFields`, `CaptureDeveloperValues`, `TryApplyDeveloperValues`.
- 저장 기본값: `Assets/Lab/Resources/LabConfig.asset`.
- 저장된 DEV 버튼·입력칸·슬라이더(39개 행이 저장돼 있고 나머지는 실행 시 마지막 행을 복제해 200개로 늘림)·목표 인원·Host 적용 버튼 연결: `Assets/Lab/Scenes/Lab.unity`의 `LabDeveloperMode` 컴포넌트.
- 패널 열기·DEFAULTS·SOLO/Host 설정 적용·슬라이더 변환: `Assets/Lab/Scripts/UI/LabDeveloperMode.cs`.
- Host 전투 수치·개인 HP·전원 0 패배·스태미나·생성 확률·속성·아이템 잠금·구슬 만료: `Assets/Lab/Scripts/Core/LabModel.cs`.
- 아이템 더블 탭 사용·효과(물약·상자), 릴레이 단계, 폭탄 폭발·피해 배율: `Assets/Lab/Scripts/Core/LabModel.cs`(`TryUseItemAt`·`TryTransferAt`·`TryAdvance`·`ConfirmHit`)와 `LabOrbBoard.EndDragAtWorld`.
- 폭탄 폭발: Host 폭발 기록은 `LabModel.TryAdvance`·`DrainBombExplosions`, 보유자 전달은 `LabNetwork`의 `bombBlast` 패킷, 충격파는 `LabOrbBoard.Blast`, 이펙트는 `LabBombBlastEffect`, 진동은 `LabHaptics.PlayForBomb`, 깜빡임은 `LabOrbItemMarker.SetFuse`.
- 북 멈춤 구간(#178): 추첨은 `LabModel.ArriveDrum`(`TryTransferAt`에서 호출), 넘김 이유는 `LabOrbEdgeCrossing.Kind`·`transfer` 패킷 `crossing`, 받는 판의 도착은 `LabOrbBoard.StartArrival`·`Arrive`, 스냅샷 적용은 `LabNetwork.SyncBoard`(`LabOrbState.JustArrived`).
- 박 폭탄(#177): 간격·대상·착지 추첨은 `LabModel.DropGourdBombs`·`GourdBombDelay`·`DrainGourdBombs`, 신호는 `LabNetwork.AnnounceGourdBombs`(`gourdBomb` 패킷)·`GourdBombDropped`, 받는 판의 낙하·튕김은 `LabOrbBoard.ExpectDrop`·`LabOrbView.PlayDrop`(#212에서 착지 경고 제거), 박 말풍선은 `LabGourdAlert`, 폭탄 그림·굴림·해금 횟수는 `LabOrbItemMarker.ShowBomb`, 밧줄 도화선·불꽃·연기는 `SetFuse`, 무지개 테두리는 `SetLocked`, 치지직은 `LabFuseSound`, 떠돌기는 `LabOrbBoard.Drift`.
- 구슬 소멸 예고(#235): 수명 규칙은 `LabOrbKindRules.LifetimeFor`·`LabOrbState.LifeSecondsLeft`, Host 만료 보고는 `LabModel.DrainOrbExpiries`, 주인 화면 알림은 `LabNetwork.AnnounceOrbExpiries`(`orbExpired` 패킷)·`OrbExpired`, 판의 연기와 제거는 `LabOrbBoard.Vanish`·`LabOrbVanishSmoke`, 하얀 번쩍임은 `LabOrbView.SetLife`, 값은 `LabOrbAppearance`의 Orb expiry 칸.
- 넘기기 막기(#215): 일정·대상·방향·폭탄 간격은 `LabModel.StartSwipeBlocks`·`ClearOfGourdBombs`, 거절은 `TryTransferAt`(`SwipeBlocked`, `SWIPE_BLOCKED`), 스냅샷은 `LabSnapshot.swipeBlockedPlayer`·`swipeBlockedRight`·`swipeBlockStartedAt`·`swipeBlockedUntil`, 막힌 판의 튕기기·표시는 `LabOrbBoard.SetSwipeBlock`·`LabOrbEdgeBlock`, 박 말풍선은 `LabNetwork.SwipeBlockStarted`·`LabGourdAlert.ShowBlock`, 폭발에 밀린 구슬의 "폭발" 넘김은 `LabOrbBoard.Blast`(`Blasted`). 종이 배경을 모든 판 스프라이트 아래에 두는 정렬 순서는 `LabSceneController.FitPlateToCamera`(`PlateSortingOrder` -100).
- 폭탄 잠금·해금(#212): 잠금 추첨은 `LabModel.TryGenerate`·`DropGourdBombs`(`LockSwipesFor`), 해금과 수명 재시작은 `TryTransferAt`, 잠긴 폭탄만 폭발은 `TryAdvance`, 해금 폭탄 투척 허용은 `LabOrbKindRules.CanThrow`, 도화선 시간은 잠긴 폭탄에만 보내는 `LabNetwork.SyncBoard`. 무거운 폭탄은 `LabOrbBoard.DragToWorld`·`EndDragAtWorld`·`Drift`와 `UpsertOrb`의 `Rigidbody.mass`.
- 폭발 패널티(#213): 밀린 구슬 목록은 `LabOrbBoard.Blast`, 보고는 `LabNetwork.ReportBlast`(`blastHit` 패킷), Host 확인·기록은 `LabModel.MarkBlasted`(`LabOrbState.burnedAt`·`small`), 결합 상속은 `TryCombineAt`, 크기는 `LabOrbBoard.RadiusFor`·`LabOrbView.PlayShrink`, 투사체 크기는 `LabProjectile.Create`(`sizeScale`, `projectile` 패킷의 `small`), 색칠량은 `LabPaintSurface.RegisterHit`(`share`), 불은 `LabOrbBurn`(`LabOrbView.SetBurn`).
- 아이템 아이콘·자물쇠·잠금/단계 배지·폭탄 해금 횟수·해금 연출: `Assets/Lab/Scripts/Orb/LabOrbItemMarker.cs`, 아이콘·자물쇠·속성 재질과 잠금·해금 연출 값: `Assets/Lab/Art/Orbs/LabOrbAppearance.asset`. 해금 연출 값은 DEV 패널이 아닌 Inspector에서 조정한다.
- 3D 조합판 구슬 물리·XY 손동작·공통 구체 외형: `Assets/Lab/Scripts/Orb/LabOrbBoard.cs`와 `LabOrbView.cs`.
- 생성 북: 무작위 배치·북 보유자만 생성·북 위치와 360° 방향 추첨·벽 근처 속도 제한·북 전달은 `LabModel.cs`(`PlaceDrums`·`TryGenerate`·`SpawnVelocityFor`·`TryTransferAt`), 탭 생성·끌기·떠돌기·가장자리 되돌림·튀어나오기는 `LabOrbBoard.cs`(`EndDragAtWorld`·`Drift`·`UpsertOrb`), 북 그림·회전은 `LabOrbView.cs`, 북 그림 지정은 `LabOrbAppearance.asset`의 `Drum Sprite`.
- 발사 위치·초기 속도: `Assets/Lab/Scripts/Throw/LabThrowMath.cs`.
- 공의 중력·저항·충돌·수명: `Assets/Lab/Scripts/Throw/LabProjectile.cs`.
- 요괴 매달림·Y축 회전 및 실제 피격체 이동: `Assets/Lab/Scripts/Throw/LabHangingTarget.cs`; 진단용 재질: `Assets/Lab/Art/Targets/TargetSpinGradient.mat`.
- Host의 방 설정·동기화·발사·피격 확정·결과 로그: `Assets/Lab/Scripts/Session/LabNetwork.cs`와 `LabSessionSettings.cs`.
- 박 모드·고정 목표·성장 설정: `LabConfig.GourdMode`·DEV 인덱스 79, `PaintVictoryPercent`의100% 고정 게임 규칙(DEV 항목 아님), `PaintGrowthSettings`·인덱스 104~109, `LabSceneController`의 초기/`ConfigApplied` 연결, `LabGourdPaint.Attach`·`ApplyMode`.
- 근처 명중 건조: `Core/LabPaintSurface.Drying.cs`의 `RefreshNearbyPaint`·`Advance`·`ErodeBoundaryTick`·`RebuildAfterErosion`, `LabConfig.PaintDryingEnabled`·`PaintDryingDelaySeconds`·`PaintDryingPercentPerSecond`·`PaintNearbyHitRadiusDegrees`, DEV 인덱스148~151.
- Paint 성장·승리·표시: `Core/LabPaintSurface`의 `RegisterHit`·`Advance`·`CopyPixels`·`TryReplacePixels`, `LabModel.ConfirmHit`·`TryAdvance`·`PaintSurface`, `LabPaintReplication`의 `LabPaintPublisher`·`LabPaintReceiver`, `LabNetwork.PaintSurface`의 표시 캐시/Snapshot 동시 반영, `LabGourdPaint.Surface`·`PaintTexture`, `LabGourdPaint.shader`. View의 비활성/초기화는 모델을 지우지 않는다.
- 완성 박 관람 허용: `LabConfig.CompletedGourdViewerEnabled`·DEV 인덱스 110. 관람 실행·입력·격리 렌더: `Assets/Lab/Scripts/UI/GourdViewer/`의 `LabGourdResultViewer`·`LabGourdResultPreview`·`GourdResultViewerUI`·`GourdViewerGestureSurface`; 저장 UI: `Assets/Lab/Resources/GourdResultViewer/ViewerUI.prefab`.
- 목표 UI: `LabHud.PaintGoalPercentFor`·`FormatPaintGoalProgress`·`PaintGoalFill`; Crack의 HP 단계/마무리는 `LabGourdCrack`·`LabGourdFinish`.
- 카메라·보드 크기 적용: `Assets/Lab/Scripts/UI/LabSeatCamera.cs`와 `LabSceneController.cs`.
- 과거 검증 기록: `docs/VALIDATION.md`. 투척 비교 절차: `docs/THROW_TUNING_GUIDE.md`.

#190 통합 후의 실제 실행 결과는 `docs/VALIDATION.md`의 후속 통합 기록을 참조한다. 위 #191의 331/116 결과는 관람 통합 당시 기록이며 현재200개 설정 검증으로 승계하지 않는다.


### 실제 투척자 무늬·완성 박 관람의 기본값 — #206

DEV → Gourd의 `Player Paint Patterns`(138번째, 인덱스137)와 `Completed Gourd Viewer`(111번째, 인덱스110)는 **서로 독립적인 ON/OFF 버튼이며 코드·저장 Config 기본값은 모두 ON**이다. `DEFAULTS`는 두 입력을 ON으로 되돌리며, 기존 APPLY/START/RESTART를 눌러 적용한다. 무늬 OFF는 박의 RGB 무늬와 로비·내/좌우 표본만 숨긴다. 투척자 P기록·색상·알파·성장·100% 목표는 유지한다. 관람 ON은 동기화된 Paint 100% Victory에서 결과 관람 버튼을 제공하며 자동 진입하지 않는다. 관람창은 무늬 설정과 색상/투척자 맵을 복사해 회전·확대하므로 무늬 OFF의 완성품도 색상만으로 관람할 수 있다. Host가 200개 값과 지문을 공유하고 참가자 ACK를 받은 뒤 시작한다. 프로토콜5662에서는 이전 설정 개수의 앱을 섞어 접속하지 않는다.


### 재타격 군집 설정 — #208

| 설정 | 인덱스 | 저장 기본값 | 범위 | 적용 |
| --- | --- | --- | --- | --- |
| Paint Repeat Growth (%) |106|1.5|기존 범위 유지|타격 한 번의 면적 공급. 해상도와 독립적인 전체 표면 비율|
| Paint Growth Speed (%/s) |107|6|기존 범위 유지|모든 연결 영역이 나누는 전역 성장 속도|
| Paint Cluster Compactness |138|0.85|0~1|군집의 넓은 접촉·빈틈 메우기 선호도.0이어도 지역 군집 탐색은 유지|
| Paint Cluster Irregularity |139|0.25|0~1|군집별 고정된 완만한 구면 굴곡. 셀별 백색 잡음은 추가하지 않음|

두 새 숫자/슬라이더는 Gourd 묶음의 재타격 성장량 다음에 배치된다. 원본 저장값·DEFAULTS와 Capture/Apply가 같은 값을 사용하며 새 판에만 적용한다. Host는200개와5662 지문을 공유해 참가자 ACK 후 시작한다. #246에서 미사용 항목을 뺀 현행 배열 순서를 사용한다. 수직 우대(109)와 Fill Chance(108)는 한 타격의 경계 시작점 선택에만 작용한다. 셀마다 그 방향을 계속 우대해 군집을 가늘게 늘이지 않는다.

과거 #198의 셀마다 흐름 시도/아래 방향/꼬리 폭·길이 설명은 과거 구현이다. 현재 최초 명중의 LEGACY Style/Edge Raggedness와4종 마스크 선택은 유지하지만 재타격은 공통 군집 경로다. #246에서 결과에 영향이 없는 옛 Flow 조절4값과 미사용 분기를 제거했다. 구현 근거는 `LabPaintSurface.RegisterHit`·`Supply`·`ChooseClusterCell`·`GrowthCluster.Take/Commit`·`PaintCell/Consume/Merge`와 `LabConfig.PaintGrowthSettings`다.

#211은 새 DEV 필드를 추가하지 않는다. Broad/Splatter/Flow의 재타격은 같은 최초 마스크 본체의 큰 굴곡을 선호하며, Compactness/Irregularity와 기존 초기·반복 면적/속도 설정을 재사용한다. 원본 마스크와 실제 성장 규칙은 유지한다. 후속 요청으로 재타격 기본값만4%에서1.5%로 낮추며 초기2%·성장6%/초와 나머지 기본값은 유지한다. 단일 원본 `LabConfig.asset`과 코드의 같은 기본값을 맞추며 DEV DEFAULTS는 원본 값을 불러온다. 솔로 START/RESTART 또는 Host의 기존 방 적용·새 판 시작 경계에서 공유 설정으로 확정한다. 진행 중인 판과 명시적으로 조정한 값은 자동 덮어쓰지 않는다. 옛 #198의 흐름 설명과 #208의 패턴에 무관한 매끈한 윤곽 및 재타격4%로 고정한 비교·검증은 해당 버전/fixture의 기록이다.


### Gourd Visual Size

DEV → Gourd → `Gourd Visual Size (x)`는 #214에서 추가한 **141번째/인덱스140** 설정이다. 범위0.5~3.0, 기본1.4이며 Paint 모드의 몸체·뚜껑·요괴·장식을 함께 확대/축소하고 목의 연결 위치를 유지한다. 기존 APPLY/새 판 적용 경계와 Host 공유200개 설정·프로토콜5662를 사용한다. 충돌체 크기는 변경하지 않는다. #211의 재타격 기본값1.5%를 보존한다.


### 근처 유효 명중으로 유지하는 건조 — 149~152번째 설정 (#225)

DEV **Gourd**의 마지막 네 행이다. `Paint Drying`이 OFF이면 세 숫자 입력·슬라이더를 잠그며 값을 삭제하지 않는다. ON에서 조정한 후 기존 솔로 시작/재시작, Host 방 생성/로비 적용 또는 공동 일시정지 후 **APPLY & RESTART**로 새 판에 적용한다. 진행 중인 판에서 토글을 누른 것만으로 즉시 켜거나 끄지 않는다.

| DEV 입력 이름 | 인덱스 | 현재 기본값 | DEV 범위 / 단위 | 실제 적용 |
| --- | ---: | ---: | --- | --- |
| Paint Drying | 148 | ON(1) | OFF / ON | Paint 전투에서 경계 침식 활성화. OFF이면 기존 누적 도색을 유지한다. |
| Drying Delay (seconds) | 149 | 5 | 0~60초 | 각 셀의 최초 도색 또는 마지막 근처 명중에서 줄기 시작하기까지의 유예. |
| Drying Rate (%/s) | 150 | 0.2 | 0~5%p/초 | 전체 박 표면 기준 총 감소 속도. 0이면 ON이어도 도색을 지우지 않는다. |
| Nearby Hit Radius (surface degrees) | 151 | 20 | 1~90도 | 실제 피격점의 정규화된 박 로컬 방향에서 주변 도색을 보호하는 구면각도. |

`LabConfig` 공개 속성은 `PaintDryingEnabled`, `PaintDryingDelaySeconds`, `PaintDryingPercentPerSecond`, `PaintNearbyHitRadiusDegrees`다. `PaintGrowthSettings`는 라운드 시작에 이 값을 복사한다. 현재 전체 **200개**를 유한수·범위·정수 조건으로 원자적으로 검사·캡처·적용한다. 네 값 모두 프로토콜 **5662**의 설정 지문과 참가자 확인에 포함한다. 길이가 다른 배열을 부분 적용하지 않는다. Inspector 비정상 수치는 유효 Getter에서 범위 제한 또는 독립 기본값으로 대체하지만 DEV/네트워크 입력은 거절한다.

실제 명중점의 주변 기존 칠(다른 색·P번호·분리된 영역 포함)에만 시간을 갱신하며 기존 색/무늬는 유지한다. 빈곳 첫 명중도 주변 보호를 갱신하고, 성장량이 생기지 않는 유효 재타격도 보호한다. 군집의 무작위 성장 시작점은 보호 지점이 아니다. 새 성장 셀만 자체 유예를 받으며 자동 성장은 기존 칠의 시간을 연장하지 않는다. 무효·중복·share 0인 요청은 보호하지 않는다.

Host 전투 시계의 0.1초 고정 tick에서 빈 표면과 맞닿은 경계를 지운다. 색 사이 접점은 경계가 아니다. 삭제는 실제 셀·RGBA/P번호·진행률에 함께 반영되며 셀 해상도에 따라 계단식으로 양자화한다. 0.2는 현재 칠의 0.2%가 아니라 **전체 표면의 0.2%p/초**다. 일시정지·결과에서는 멈추고 100% Victory 이후 완성 박을 보존한다. 연결 분리와 동률 순서 등 전체 규칙은 [PAINT_DRYING](PAINT_DRYING.md)을 참조한다.

과거 #225의 `LabConfig.asset` Unity API 재저장은 당시 새 건조4값과 함께, 원래 코드 기본값으로 읽던 기존 무거운 폭탄 4값·폭발 패널티 3값도 명시적으로 직렬화한다. 기존153개 값의 인덱스나 유효 게임 수치를 새로 설계한 변경이 아니다. 실행 근거와 미실행 환경은 [VALIDATION](VALIDATION.md)에서 따로 기록한다.

## 요괴 방해공작 — Sabotage 묶음 (#251)

DEV → **Sabotage**의 12개 칸(167~178번째, 인덱스 166~177)이다. 실험이라 **기본은 꺼짐**이며, 꺼져 있으면 게임은 이전과 완전히 같다. 코드·저장 `LabConfig.asset` 기본값이 같다(저장 원본에 칸이 없으면 코드 기본값을 쓴다). 다른 공유 설정처럼 Host가 200개 전체를 검사·지문화해 참가자 확인 뒤 새 판에 적용한다.

| 이름 | 기본 | 범위 / 단위 | 설명 |
| --- | --- | --- | --- |
| Sabotage Mix (0 off, 1 on) | OFF | 켜기·끄기 | 인덱스 166. 켜면 박 폭탄(`Gourd Bomb Interval`)·넘기기 막기(`Swipe Block Interval`)의 각자 타이머를 멈추고, 아래 간격마다 방해공작 하나를 비율대로 골라 무작위 한 명에게 건다. 콤보는 따로 진행한다. |
| Sabotage Interval Min (seconds) | 20 | 1–600 / 초 | 인덱스 167. 다음 방해공작까지의 간격을 매번 최소~최대 사이에서 뽑는다. 최소가 최대보다 크면 적용하지 않는다. |
| Sabotage Interval Max (seconds) | 30 | 1–600 / 초 | 인덱스 168. |
| Sabotage Weight Gourd Bomb | 1 | 정수 0–10 | 인덱스 169. 박 폭탄의 비율. |
| Sabotage Weight Swipe Block | 1 | 정수 0–10 | 인덱스 170. 넘기기 막기의 비율. |
| Sabotage Weight Fox Fire | 1 | 정수 0–10 | 인덱스 171. 여우불의 비율. |
| Sabotage Weight Charm | 1 | 정수 0–10 | 인덱스 172. 매혹의 비율. |
| Fox Fire Count Min | 2 | 정수 1–10 / 개 | 인덱스 173. 한 번에 떨어지는 여우구슬 수의 최소. 최소가 최대보다 크면 적용하지 않는다. |
| Fox Fire Count Max | 5 | 정수 1–10 / 개 | 인덱스 174. 판이 거의 차 있으면 빈 칸 수만큼만 떨어진다. |
| Fox Fire Safe Seconds | 1 | 0–3 / 초 | 인덱스 175. 떨어져 땅에 닿은 뒤 이 시간 동안은 음·양과 닿아도 사라지지 않는다. Host는 떨어진 시각부터 센다. |
| Charm Seconds | 10 | 1–30 / 초 | 인덱스 176. 매혹이 이어지는 시간. |
| Charm Rescue Taps per Neighbor (0 = none) | 10 | 정수 0–10 / 번 | 인덱스 177. #305부터 **양옆 사람이 각자** 이만큼 말풍선을 눌러야 하고, 두 사람이 모두 채우는 순간 풀린다(둘이 하는 판은 상대 한 사람만). 먼저 다 채운 사람의 말풍선 위에는 체크가 뜨고 더 누른 것은 세지 않는다. 0이면 시간으로만 풀린다. #251 당시에는 양옆이 합쳐 3번이었다. |
| Charm Warning Seconds (0 = none) | 3 | 0–10 / 초 | 인덱스 204 (#305, 마지막에 추가). 매혹 대상이 정해진 뒤 이 시간 동안 모든 화면의 박 위(기본은 "정신 차림!" 자리, `LabCharmHud`의 `Warning Anchor`·`Warning Size`로 조정)에 "요괴가 P2에게 / 매혹을 걸려고 한다!"(걸릴 사람에게는 "나에게")를 보여 주고, 그 뒤에 매혹이 시작된다. 예고 중에는 던질 수 있고 깨우기 탭은 받지 않는다. 울음·박 속 요괴는 예고가 시작될 때, 걸린 사람 화면의 박 옆 하트 말풍선은 매혹이 실제로 시작될 때 나온다. 0이면 예고 없이 바로 걸린다. Hapio DEV에서는 구미호 탭에 보인다. |

**비율 계산**: 확률 = 그 방해공작의 비율 ÷ 지금 걸 수 있는 방해공작 비율의 합이다(합이 100일 필요 없음). 기본 1·1·1·1이면 각 25%다. 지금 걸 수 없는 것은 그 차례의 후보에서 빠지고 남은 것끼리 다시 나눈다: 이미 걸린 넘기기 막기·매혹(한 번에 하나), 받을 사람 판이 꽉 찬 여우불. 받을 사람에서는 바로 앞 방해공작을 받은 사람(솔로 제외), 넘기기 막기·매혹이 걸려 있는 사람이 빠진다. 걸 곳이 하나도 없으면 그 차례는 건너뛴다. 추첨은 seed와 순번으로 정해 모든 기기가 같은 결과를 받는다.

**여우불**: 받은 사람 판에 푸른 여우구슬이 떨어진다(박 폭탄 낙하 재사용). 음·양 구슬과 닿거나 끌어서 겹치면(접촉·드롭 조합 경로) 둘 다 푸른 연기로 사라지고, 결합 구슬은 생기지 않는다. 옆 가장자리를 넘으면(스와이프·폭발 모두, 솔로 포함) 푸른 연기로 사라져 이웃에게 가지 않는다. 넘기기 막기가 걸린 쪽에서는 튕긴다. 폭발에 밀리지만 불붙음·축소(#213)는 없다. 보유 한도 20에 들고, 수명은 일반 구슬과 같으며(8초), 위로 던질 수 없다. 그림은 `Resources/Sabotage/FoxOrb.png`이고 둘레의 푸른 빛·연기 색은 `LabOrbAppearance`의 Fox fire 칸에서 바꾼다.

**매혹**: 걸린 사람은 정해 둔 시간 동안 위로 던지지 못한다(Host도 `CHARMED`로 거절). 위로 그어도 보통 구슬처럼 미끄러져 판 위쪽에 튕긴다. 걸린 사람 화면에는 박 옆 하트 말풍선, 던지는 쪽 가장자리(판 위쪽 곡선)의 은은한 빨간 띠, 북 미니맵 위의 남은 시간 게이지(임시)가 뜨고, 풀리면 `정신 차림!`이 나온다. 양옆 사람 화면에는 그 사람 이름표 위에 하트 눈 말풍선이 떠 누를 때마다 안쪽의 연한 노란 게이지가 차오르고, 다 차면 풀리며 놀란 얼굴의 뾰족 말풍선이 튀어나온다. 시간이 다 되어 풀리면 하트 말풍선이 서서히 사라진다. 둘이 하는 판에서는 상대가 양쪽 이웃이라 양쪽 이름표에 말풍선이 뜬다. 오른쪽 이름표 위 말풍선 자리를 비우려고 북 미니맵을 이름표 위 120만큼 띄웠다(`LabDrumMinimap.gapAboveBadge`). 띠·게이지 색은 `LabCharmEdgeFlash.GlowColor`·`LabCharmBubbles.WakeFillColor`, 말풍선 그림은 `Resources/Sabotage/CharmBubbleLeft·Right·Woken.png`, 울음소리는 `Resources/Sabotage/CharmHowl`(아직 없음, 없으면 소리 없이 진행)이다.

**통신**: 각 방해공작은 `sabotage` 알림(종류·대상)으로, 여우구슬 낙하는 `foxFire`, 여우구슬이 음양을 데려가는 요청은 보드 행동 `fox`, 깨우기 탭은 `charmTap`, 깨워서 풀림은 `charmWoken`으로 오간다. 매혹 상태(대상·시작·끝·탭 수)는 스냅샷과 조합판 신호 머리에 실린다. 박 속 요괴는 통합 추첨이 거는 모든 방해공작에 나온다(폭탄은 기존 신호로).

근거: `LabModel.CastSabotages`·`DropFoxFire`·`TryFoxFireAt`·`StartCharm`·`TryCharmTap`, `LabOrbBoard`(여우불 닿음·가장자리, `SetCharm`), `LabCharmEdgeFlash`, `LabCharmHud`, `LabCharmBubbles`, `LabGourdAlert.ShowHeart`, `LabSabotageArtImporter`.

## 야광귀 섬광 — 야광귀 묶음 (#268)

DEV → **야광귀**의 아래쪽 `── 섬광 ──` 7칸(185~191번째, 인덱스 184~190, #272 지귀 6칸 뒤)이다. 야광귀 스테이지에서 쓸 방해로, 박 색상 콤보(156~165번째)와 한 묶음에 모았다. 방해공작 묶음(#251)과 따로 돈다. **기본은 꺼짐**이며 코드·저장 `LabConfig.asset` 기본값이 같다. 다른 공유 설정처럼 Host가 200개 전체를 검사·지문화해 참가자 확인 뒤 새 판에 적용한다. 스테이지별 켜고 끄기는 스테이지 작업에서 이 토글들을 쓰면 된다.

**야광귀 묶음 화면**: 묶음 제목 오른쪽 버튼(`끔`/`켬`/`일부`) 하나로 `Gourd Color Combo`(인덱스 155)와 `야광귀 섬광`(인덱스 184)을 함께 켜고 끈다. 둘 다 켜져 있으면 `켬`이고 누르면 둘 다 끈다. 둘 다 꺼져 있거나(`끔`) 하나만 켜져 있으면(`일부`) 누르면 둘 다 켠다. 새 설정값이 아니라 두 토글의 입력칸을 바꾸는 단축키이므로 APPLY 전에는 적용되지 않고, 묶음이 접혀 있어도 보인다. 묶음 안에는 `── 콤보 ──`, `── 섬광 ──` 구분선이 보인다(`LabDeveloperCategory.WithSwitch`·`WithDividers`, 화면 표시만).

| 이름 | 기본 | 범위 / 단위 | 설명 |
| --- | --- | --- | --- |
| 야광귀 섬광 (0 off, 1 on) | 끔 | 끔·켬 | 인덱스 184. 켜면 아래 시각마다 박 속 요괴가 빼꼼 나오고 박 옆 말풍선에 눈이 떴다가 빛난 뒤(예고) 섬광이 터진다. |
| 섬광 대상 (0 모두, 1 한 명) | 모두 | 모두·한 명 | 인덱스 185. 모두면 모든 사람이 동시에 맞는다. 한 명이면 매번 seed와 순번으로 한 명을 뽑아 모든 기기가 같은 사람을 받고, 두 명 이상이면 바로 앞에 맞은 사람은 빠진다. |
| 섬광 첫 시작 (초) | 10 | 1–19 / 초 | 인덱스 186. 전투 시작 후 첫 섬광이 번쩍이는 시각. 예고는 그 직전에 온다. #271에서 31 → 10(범위 1–61 → 1–19, 기본값이 가운데): 판 첫머리가 20초 넘게 비지 않도록 야광귀가 7초에 먼저 등장한다. 이후 섬광 70·130초, 콤보 50·110초 근처로 번갈아 온다. |
| 섬광 간격 (초) | 60 | 20–100 / 초 | 인덱스 187. 번쩍인 시각부터 다음 섬광까지. 예고 + 눈부심 + 회복보다 짧게 하면 그만큼은 띄운다. |
| 섬광 예고 (초) | 3 | 0–6 / 초 | 인덱스 188. 요괴가 빼꼼 나오고 박 옆 말풍선에 야광귀 두 눈이 뜬다. 예고의 30%까지는 보통 눈, 30~45%에 눈이 갑자기 노랗게 빛나고, 남은 55%(기본 약 1.6초)는 빛나는 노란 눈의 둘레 빛이 두근거리다 번쩍인다(#271에서 2 → 3초, 노란 눈을 더 오래 보이게). 맞을 사람 화면은 눈이 빛나는 만큼 가장자리 노란 빛이 차오른다. 콤보 한 판 중에는 눈 말풍선과 요괴가 나오지 않는다. 0이면 예고 없음. |
| 섬광 눈부심 (초) | 10 | 1–19 / 초 | 인덱스 189. 번쩍인 뒤 판의 음양·결합 구슬과 아이템(여우구슬 포함)이 완전히 하얗게 보이는 시간. 북·박 색칠·HUD·콤보 패널은 그대로다. #301부터 폰을 흔들어 풀지 못했을 때의 상한이며, 흔들기(약 2초)에 맞춰 7 → 10초(범위 가운데). |
| 섬광 회복 (초) | 1 | 0–2 / 초 | 인덱스 190. 눈부심이 시간으로 끝난 뒤 색이 부드럽게 돌아오는 시간 (#301에서 2 → 1초). 0이면 바로 돌아온다. 흔들어 풀 때는 흔든 만큼 걷히며 Inspector `Shake Clear Speed`로 부드럽게 따라간다. |
| 섬광 흔들어 풀기 (0 off, 1 on) | 켬 | 끔·켬 | 인덱스 202 (#301). 켜면 눈부신 사람 화면에 흔들리는 폰 그림·"흔들어!"·게이지가 뜨고, 폰을 세게 흔들 때마다 하얀 막이 조금씩 걷힌다. 정해진 횟수를 채우면 Host가 그 사람 눈부심만 푼다. 눈부신 사람이 있는 동안 박 속 요괴가 나와 있다. Editor·DEV 빌드는 K 키로 시험한다. 끄면 시간으로만 풀린다. |
| 섬광 흔들기 횟수 | 6 | 2–10 / 회(정수) | 인덱스 203 (#301). 풀리기까지 세게 흔들어야 하는 횟수. 6번이면 보통 약 2초. 흔들기 세기 기준(기본 중력의 1.6배)·간격(기본 0.25초)은 Inspector `LabYaguangFlashHud`에서 조절한다. |

숫자 5칸은 DEV 범위의 가운데가 기본값이라, DEFAULTS 상태에서 슬라이더가 모두 가운데에 있다.

**기본 시간 근거**: 섬광 한 번은 예고 3 + 눈부심 7 + 회복 2 = 12초다. 콤보(50초 시작·60초 간격)는 예고 4 + 제한 15 + 결과 약 1.6 + 효과 15 ≈ 36초를 쓴다(#268에서 페널티/보상 10 → 15초, #271에서 예고 3 → 4초). 같은 60초 박자에서 콤보 사이 틈은 약 25초이고, 섬광 12초를 그 가운데에 놓도록 콤보 19초 전(31/91/151초)에 번쩍인다. 콤보가 가장 늦게 끝나도(제한 15초를 다 쓰고 실패) 섬광 앞뒤로 약 6~7초씩 평소 플레이가 남고, 성공은 대개 더 빨라 틈이 더 길다. 예고 3초는 보통 눈을 알아볼 0.9초와 눈이 빛나는 0.45초, 빛나는 노란 눈을 감상할 1.65초를 담고, 회복 2초는 긴 눈부심 뒤에 색이 너무 갑자기 돌아오지 않게 한다.

| 시각(초) | 일 |
| --- | --- |
| 28–40 | 섬광 (예고 28–31, 눈부심 31–38, 회복 38–40) |
| 46–약 82 | 콤보 |
| 88–100 | 섬광 |
| 106–약 142 | 콤보 |
| 148–160 | 섬광 |
| 166–180 | 콤보 (전투 끝) |

**콤보와 겹칠 때**: 콤보 한 판(예고 → 4칸 → 결과 → 페널티/보상 효과)이 진행 중이거나, 지금 시작하면 섬광이 끝나기 전에 다음 콤보 예고가 올 때(#271) 섬광 예고가 시작될 차례면 기다렸다가, 콤보가 끝난 순간부터 예고를 처음부터 다 보여주고 번쩍인다.

**화면**: 모든 기기의 박 옆 말풍선에 야광귀 두 눈(보통 → 갑자기 노랗게 빛남, `LabGourdAlert.ShowEyes`·`SetEyeGlow`). 맞을 사람 화면에만 눈 빛에 맞춘 가장자리 노란 빛 → 0.3초 하얀 번쩍임과 진동(iPhone) → 눈부심 동안 옅은 하얀 막과 판 구슬·아이템 하얀 덮개 → 회복. 터치는 막지 않아 잡기·넘기기·합치기·던지기 모두 평소처럼 된다. 박 속 요괴는 대상과 상관없이 모든 기기에서 예고 동안 쏙 올라와 두리번거리고, 번쩍일 때 톡 튄 뒤 0.6초 뒤 들어간다.

**통신**: 새 알림 없이 스냅샷에 다음 섬광 시각·예고 시각·대상, 섬광 수, 마지막 섬광 시각·대상(`flashWarningAt`/`flashAt`/`flashTarget`/`flashCount`/`flashLastAt`/`flashLastTarget`)이 실린다. 하얀 정도는 각 기기가 이 시각과 DEV 시간 값으로 계산한다(`LabYaguangFlash`).

근거: `LabModel.AdvanceFlash`·`DrawFlashTarget`, `LabYaguangFlash`, `LabYaguangFlashHud`, `LabOrbView.SetGlare`, `LabOrbBoard.SetFlashGlare`, `LabGourdYokai.InFlashWindow`, `LabHaptics.PlayForFlash`, `LabDeveloperMode.BuildCategorySwitch`.

**콤보 슬라이더 (#271)**: 콤보 숫자 칸 8개의 DEV 범위를 지금 기본값이 슬라이더 가운데에 오도록 바꿨다(첫 시작 1–99, 간격 20–100, 제한 5–25, 페널티·보상 시간 0–30, 페널티 크기 0.01–0.19, 보상 크기 1–4, 예고 0–8). Inspector `Range`도 같다. 모델은 이전처럼 더 넓은 값을 받으므로(규칙 검사의 1초 제한 등) 저장 원본에서 범위 밖 값을 쓰면 DEV 적용 때만 거절된다.

**야광귀 모드의 이벤트 차례 (#271)**: 콤보나 섬광이 켜져 있으면 **각자의 화면에서** 이벤트가 한 번에 하나만 열린다. 박 폭탄, 넘기기 막기, 섬광, 콤보 한 판, 방해공작 묶음의 여우불·매혹 가운데 하나가 어떤 사람 화면에 열려 있으면, 그 화면에는 새 이벤트를 걸지 않는다. 시각을 미리 비켜 가게 계산하는 것이 아니라 매 순간 화면마다 무엇이 열려 있는지 보고 막는다. 화면별로 열린 상태는 다음과 같다.

| 이벤트 | 열려 있는 화면과 동안 |
| --- | --- |
| 박 폭탄 | 그 폭탄을 지금 가진 사람 화면. 터지거나 쓰여 없어질 때까지 (한 화면에 폭탄 하나) |
| 넘기기 막기 | 막힌 사람 화면. 막기가 걸려 있는 동안 (막기는 한 번에 하나) |
| 섬광 | 맞을 사람 화면(모두면 모든 화면). 예고가 시작된 뒤 눈부심·회복이 끝날 때까지 |
| 콤보 한 판 | 예고·4칸 동안은 모든 화면, 그 뒤 페널티/보상 효과는 받은 사람 화면 |
| 여우불·매혹 | 걸린 사람 화면 |

- 박 폭탄·넘기기 막기·한 명 섬광·여우불·매혹은 뽑힌 사람 화면이 이벤트 중이면 비어 있는 화면에서 다시 뽑는다(모든 기기가 같은 결과). 비어 있는 화면이 없으면 기다렸다가 생기는 순간 건다.
- 콤보와 모두 섬광은 모든 화면이 비어 있어야 시작한다. 기다린 콤보·섬광은 예고부터 처음 보여주고, 미뤄질 동안에는 예고 시각을 알리지 않는다.
- **차례 지키기**: 콤보·섬광이 예고를 시작할 때가 됐는데 화면이 비지 않아 기다리기 시작하면, 그동안 새 박 폭탄·넘기기 막기·방해공작을 어느 화면에도 걸지 않는다(이미 열린 것은 끝까지 간다). 그래서 길어야 폭탄 한 개 수명(15초)만큼 기다리고 차례가 온다. 둘 다 기다리면 먼저 기다리기 시작한 쪽이 먼저 한다.
- **콤보와 섬광**: 먼저 예고를 시작한 쪽이 먼저 하고, 다른 쪽은 그 한 판(섬광은 회복까지, 콤보는 효과까지)이 끝나기를 기다렸다가 예고부터 한다. 섬광이 곧 올 콤보를 위해 미리 비켜 주지는 않는다(예전에는 비켜 줘서 섬광이 거의 나오지 않았다).
- **쉬는 시간 3초**: 한 화면의 이벤트가 닫히면 그 화면은 3초(`LabModel.EventGapSeconds`) 동안 쉬고, 그 뒤에야 다음 이벤트가 열린다. 모든 화면이 필요한 콤보·모두 섬광은 모든 화면이 쉬기를 마쳐야 시작한다. 콤보·섬광 자기 다음 차례와의 간격은 원래 규칙(간격·예고)대로 잰다. 끝 시각을 아는 이벤트(섬광 회복, 콤보 효과, 넘기기 막기, 매혹)는 그 시각부터, 모르는 것(폭탄·여우구슬이 없어진 때)은 Host가 알게 된 프레임부터 잰다.
- 같은 순간에 차례가 겹치면 박 폭탄 → 넘기기 막기 → 방해공작 → 콤보 → 섬광 순서로 먼저 연다.
- 콤보와 섬광이 모두 꺼져 있으면 예전처럼 각자 시각대로 돈다. 근거: `LabModel.ScreenOpen`·`FreeTarget`.

## 지귀 수동 패턴 (#272)

DEV **Jigwi** 카테고리에 인덱스178~183의6개 슬라이더를 추가했다. 기본값·범위·소화/소각 규칙·Host 전용 수동 발동 방법은 [지귀 패턴 안내](JIGWI_PATTERNS.md)에 정리했다. 생성 화상은 기존 폭탄 축소와 별도이며 기존178개 인덱스를 이동하지 않았다. 자동 방해공작 추첨에는 지귀를 추가하지 않는다.

## #276 별도 3요괴 게임의 지귀 자동 설정

기존0~191은 유지하며 아래4칸을 마지막에 추가한다. Lab의 기본 `None` 정책은 자동 지귀를 실행하지 않아 기존 단일 Scene 실험을 유지한다. 별도 게임의 지귀 단계에서만 자동 추첨에 사용한다.

| 인덱스 | 필드 | 기본값 | DEV 범위 |
| --- | --- | --- | --- |
| 192 | Jigwi Auto Interval Min (seconds) | 20초 | 1~600 |
| 193 | Jigwi Auto Interval Max (seconds) | 30초 | 1~600, min 이상 |
| 194 | Jigwi Auto Fire Weight | 1 | 정수0~10 |
| 195 | Jigwi Auto Generation Burn Weight | 1 | 정수0~10 |

별도 Home/Lobby는 공통·구미호·야광귀·지귀 범위 탭을 제공한다. 공통 조정값은 모든 스테이지에, 고유 값은 해당 단계에만 적용한다. Battle의 기존 DEV는 모든 항목을 보여 주지만 현재 단계와 공통 값만 변경할 수 있고 타 단계 수정은 재시작 전에 거절한다. 기본 ON을 다시 강제하지 않으므로 DEV에서 끈 값은 같은 단계 재시작에도 유지한다. 자세한 규칙/외형 연결/종료 조건은 [GAME_FLOW](GAME_FLOW.md), 실제 수행은 [VALIDATION](VALIDATION.md)의 #276 기록을 따른다.

## #281 2:2 청홍 땅따먹기 (197~200번째, 인덱스 196~199)

기존0~195는 유지하며 아래4칸을 마지막에 추가한다. Lab DEV에서는 `2:2 땅따먹기` 묶음으로 보이고, 켜도 **4명 판 또는 DEV 솔로**에서만 팀전이 된다(다른 인원은 기존 규칙). 별도 Hapio 게임에서는 196 켜기를 방 모드(새 방 만들기 → 2 : 2 땅따먹기)가 정하므로 어느 탭에도 보이지 않고, Home/Lobby DEV의 다섯 번째 **2:2** 탭에서 197~199를 조정한다. 2:2 프로필은 공통 값을 함께 받되, **스태미나·생성·구슬 수명(3~8, 10번)은 2:2가 따로 가진다**(#305). 공통 탭에서 바꿔도 2:2에는 가지 않고, 2:2 탭이나 2:2 전투 DEV에서 바꿔도 공통·스토리에는 가지 않는다. 2:2의 저장 기본값은 `Hapio/StageCatalog`의 `Territory Default Values`다: 최대·시작 160, 생성 비용 20, 1.7초당 20 회복, 명중 보상 10, 구슬 수명 8초. 전투 막대 그림(8칸)을 모든 모드가 함께 쓰므로 최대 ÷ 생성 비용은 어느 모드든 8이어야 칸 1개 = 구슬 1개가 된다(#305, 검사로 고정). 회복과 수명만 2:2가 지금 따로 다르다. Lab 단독 장면의 DEV 솔로 2:2는 이 분리를 쓰지 않고 `LabConfig` 값을 그대로 쓴다.

| 인덱스 | 필드 | 기본값 | DEV 범위 |
| --- | --- | --- | --- |
| 196 | 2:2 땅따먹기 (0 off, 1 on) | 끔 | 토글 |
| 197 | 2:2 제한 시간 (초) | 180초 | 30~600 |
| 198 | 피버 필요 명중 수 (0 = 피버 없음) | 15 | 정수0~50 |
| 199 | 피버 시간 (초) | 8초 | 1~30 |

팀전에서는 요괴 스테이지·박 폭탄·넘기기 막기·방해공작·콤보·섬광·마르기를 끄고, 제한 시간은 197을 쓴다. 덮어쓰기는 얼룩 무늬가 필요하므로 `Paint Pattern`이 LEGACY면 SPLATTER로 바꿔 쓴다. 피버는 팀 명중이 198에 닿으면 199초 동안 그 팀 판의 음·양·결합 구슬을 팀 색 결합 구슬로 바꾸고, 그동안 생성도 팀 색 결합 구슬을 낸다(무지개 고리 표시). 규칙과 화면은 [게임 흐름](GAME_FLOW.md)의 #281 절, 실행 결과는 [VALIDATION](VALIDATION.md)의 #281 기록을 따른다.


## #299 고해상도 도색 (인덱스200~201)

| 인덱스 | 필드 | 기본값 | DEV 범위 |
| --- | --- | --- | --- |
| 200 | High Resolution Paint | ON(1) | OFF / ON |
| 201 | High Resolution Paint Quality | 0 | 0=1024×512 / 1=2048×1024 |

두 항목은 Gourd 카테고리·별도 게임 공통 설정으로 모든 스테이지와2:2에 적용한다. 품질 변경은 도색 판정 셀 해상도를 바꾸지 않는다. 결과 관람은 전투의 표시 품질을 독립 복사한다. 실행법·메모리 계산·지원 실패 시 기존 표현 사용은 [런타임 고해상도 도색](RUNTIME_HIGH_RESOLUTION_PAINT.md)을 따른다.
