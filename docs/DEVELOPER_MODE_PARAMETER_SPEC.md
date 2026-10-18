# C6 Physics Lab 개발자 모드 조정값 명세

> 기본 수치 기준: Unity 6000.6.5f1의 현재 `Assets/Lab/Resources/LabConfig.asset`과 `LabConfig` 런타임 보정값. 설정은 총 118개이며 기존 0~110번 인덱스를 보존하고 직구 폼 7개를 111~117번에 추가했다. 색칠 성장 6개는 기존 105~110번을 유지한다. 88번째(인덱스 87) `Paint Victory Coverage (%)`는 100% 고정 행으로 유지한다. 64~74번째(인덱스 63~73) 소리 음량·햅틱 11개(#142)는 이 문서에 아직 따로 설명하지 않았다. 미니맵·북·박 폭탄·폭탄 소리의 기존 인덱스와 기본값은 유지한다. 실행 검증 결과는 `docs/VALIDATION.md`에서 변경별로 확인한다.

이 문서는 **새 실험실(C6_Physics_Lab)**의 개발자 패널에 실제로 연결된 설정 118개를 설명한다. 이 중 색칠 승리 목표는 읽기 전용 100% 고정값이다. 이전 C6 프로토타입이나 향후 기획값을 정의하지 않는다. 각 값의 입력 허용 범위는 `LabConfig.DeveloperFields`, 게임에서 읽는 유효값은 `LabConfig` 속성, 사용 지점은 아래 코드 근거를 기준으로 했다.

## 1. 패널의 적용 방식

1. `Assets/Lab/Scenes/Lab.unity`를 Unity Editor에서 실행하거나 **Development Build**를 실행한다. 일반 배포 빌드에서는 DEV 버튼이 숨겨진다. 참가자는 일반 빌드여도 Host가 확정한 세션 설정을 수신한다.
2. 접속 전 또는 **전투 시작 전 Host 로비**에서 **DEV**를 연다. 공유 전투 중에는 Host가 먼저 공동 일시정지한 뒤 DEV를 열어 **APPLY & RESTART**로 적용한다. Client는 설정을 직접 바꾸지 않는다. 총 118개 설정 중 일반 숫자 입력칸과 대응 슬라이더는 서로 값을 반영한다. `Paint Victory Coverage (%)`는 100을 표시하는 읽기 전용 입력칸이며 슬라이더는 비활성이다. 단, 0/1 항목 7개(Drop Combine, Contact Combine, Haptics, Gourd Mode, Minimap, Bomb Sound, Throw Form)는 입력칸·슬라이더 대신 누를 때마다 바뀌는 토글 버튼(ON/OFF, Gourd Mode는 PAINT/CRACK, Bomb Sound는 BOOM/PPEONG, Throw Form은 ARC/STRAIGHT)으로 보이며, 값은 그대로 0/1로 적용·공유된다. 정수 필드는 슬라이더 값도 정수로 표시하며, 매우 넓은 범위는 슬라이더 조작 구간을 비선형으로 배분한다. 숫자 입력으로 허용 범위의 정확한 값을 지정할 수 있다. `Throw form`의 직구 전용 6개 행은 미적용 초안의 ARC/STRAIGHT 토글에 맞춰 즉시 비활성/활성화되며 값은 유지한다.
3. 공유 실험을 시작하려면 별도 **ROOM PLAYERS** 슬라이더에서 2~5명(기본 3명)을 정하고 **CREATE ROOM**을 누른다. Host가 검증된 118개 값으로 방을 만든 뒤 참가자에게 전파한다. 기존 **HOST** 버튼을 사용하면 기본 인원과 현재 설정으로 방을 만든다. 방 생성 후 아직 전투 전이라면 **APPLY TO ROOM**으로 다시 조정할 수 있으며 이전 참가자 확인은 무효화된다. 목표 인원이 모이고 각 참가자가 최신 설정을 확인한 뒤에만 Host의 **START**가 활성화된다.
4. 혼자 시험할 때는 **START SOLO**를 누른다. 이미 솔로 전투 중이면 **RESTART SOLO**가 표시된다. 새 라운드가 시작되므로 요괴/개인 HP·시간·스태미나·구슬이 초기화된다. 진행 중인 네트워크 라운드에 값을 즉시 적용하는 동작은 없다.
5. **DEFAULTS**는 저장된 `LabConfig.asset`의 숫자를 입력칸과 슬라이더에 불러올 뿐이다. 공유 방이나 솔로에 적용하려면 해당 시작·적용 버튼을 눌러야 한다. **END SOLO**는 솔로를 끝내고 원본 설정으로 복귀한다. 런타임 복사본은 앱이 실행 중인 동안만 유지되며 원본 에셋에는 저장되지 않는다.

패널 열기 자체는 시간을 멈추지 않는다. 공유 전투에서 설정을 바꾸려면 먼저 공동 일시정지를 승인받아야 하며, 이때 시간은 그 일시정지 상태를 따른다. 마지막 `SHOT HIT/MISS` 보고는 솔로를 끝내거나 연결을 끊으면 지워질 수 있으므로 먼저 기록한다. 공유 방에서는 Host의 설정 지문과 확인 응답을 기준으로 시작을 판정한다. 이 절은 코드상 경로 설명이며 실제 기기 동기화 결과는 검증 기록과 구분한다.

입력은 **118개 전체를 한 번에** 검사한다. 숫자가 아니거나 무한대/NaN, 범위 밖, 정수 전용 필드의 소수 입력이 있으면 아무 값도 적용하지 않는다. 아래 표의 넓은 허용 범위는 “게임플레이에 적합함”을 뜻하지 않는다. 극단적인 카메라·속도·수명 값은 화면 밖 이동이나 빗나감을 일으킬 수 있다.

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

### 박 모드·100% 공동 목표·성장 — 80·88·106~111번째 설정

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Gourd Mode (0 paint, 1 crack) | 0 (Paint) | 정수 0–1 / 표현 모드 | 80번째(인덱스 79), `Gourd` 묶음. Paint는 흰 박의 공동 색칠, Crack은 기존 박·HP 단계 크랙·승리 마무리다. 원통 충돌·회전·투척 입력을 공유한다. |
| Paint Victory Coverage (%) | 100 | 정수 100–100 / 전체 구면 % | 88번째(인덱스 87), 기존 이름·인덱스를 보존한 고정 목표. 숫자 입력은 읽기 전용, 슬라이더는 비활성이다. 100 외 값을 받은 DEV/공유 설정은 다른 값과 함께 원자적으로 거절한다. Crack의 HP 승리에는 사용하지 않는다. |

아래 6개는 **DEMO_TUNING_VALUE**이며 Inspector 원본 `LabConfig.asset`의 Gourd 항목 또는 DEV의 `Gourd` 묶음에서 조정한다. #187에서 기존 0~104번 뒤에 추가했으며 #190의 직구 항목 추가 후에도 105~110번과 기본값을 유지한다. 수치 튜닝은 새 판에 적용하며, 현재 실행 중인 판의 규칙을 즉시 바꾸지 않는다.

| DEV 입력 이름 | 위치 | 기본값 | 입력 범위 | 효과 |
| --- | --- | ---: | --- | --- |
| Paint Surface Resolution (0 low 1 medium 2 high) | 106번째 · 인덱스 105 | 2 | 정수 0–2 | 0: 64×32, 1: 128×64, 2: 256×128 동일 면적 구면 셀. 새 판에서 적용한다. |
| Paint Initial Coverage (%) | 107번째 · 인덱스 106 | 2 | 0.1–100% | 안 칠한 지점에 처음 맞혔을 때 즉시 칠하는 초기 면적. 전체 셀 수를 기준으로 하며 셀 단위로 양자화한다. |
| Paint Repeat Growth (%) | 108번째 · 인덱스 107 | 4 | 0.1–100% | 이미 칠한 지점에 맞히면 연결된 영역에 추가하는 성장 면적 예산. 기존 셀 색은 바꾸지 않는다. |
| Paint Growth Speed (%/s) | 109번째 · 인덱스 108 | 6 | 0.1–100%/초 | 모든 영역이 함께 나눠 쓰는 전체 표면 기준 성장 속도. 영역 수만큼 속도가 늘지 않는다. |
| Paint Fill Chance (%) | 110번째 · 인덱스 109 | 60 | 0–100% | 이웃이 둘 이상 칠해진 빈 셀을 우선 채울 확률. 해당 후보가 없으면 다른 인접 후보로 진행한다. |
| Paint Vertical Weight | 111번째 · 인덱스 110 | 1.35 | 1–4 | 세로 방향 인접 후보의 선택 가중치. 1은 가로와 같은 가중치다. |

`DefaultPaintVictoryPercent`·`PaintVictoryPercent`는 100, `PaintVictoryCoverage`는 1이다. 예전 에셋의 직렬화 목표 80이나 비정상 목표도 유효값과 `CaptureDeveloperValues`에서는 100으로 정규화한다. 저장 원본도 Unity에서 목표 100과 성장 기본값 6개를 기록했다. 원본 에셋을 불러오는 것과 DEV 입력을 검증하는 것은 구분한다. 이미 만들어진 구형 80 설정 배열은 현재 프로토콜과 100 고정 검사로 거절한다.

118개 전체를 **START/RESTART SOLO**, **CREATE ROOM**, Host 로비의 **APPLY TO ROOM**, 공동 일시정지한 Host의 **APPLY & RESTART**에서 검증·적용한다. `LabConfig.PaintGrowthSettings`는 매번 새 설정 복사본을 반환하고 Host Rules는 새 라운드 시작 때 이를 캡처한다. 일반 Retry는 캡처한 설정을 유지한다. `LabSceneController`의 초기 적용/`LabNetwork.ConfigApplied`가 `LabGourdPaint.Attach`·`ApplyMode`를 호출하며 Client는 Host 모드를 받는다.

유효한 오행 명중이 안 칠한 셀에 닿으면 초기 면적을 즉시 칠하고, 이미 칠한 셀에 닿으면 그 연결 영역에 반복 성장 면적을 공급한다. 이후 Host 전투 시계가 진행될 때 미사용 예산을 가진 영역들이 전체 표면 기준 `%/초` 속도를 나눠 쓰며 빈 인접 셀로 자란다. 시간이 흐르는 동안 공급 예산이 없으면 자라지 않으며, 그 대기 시간의 성장량을 미리 적립하지 않는다. 여러 영역은 차례대로 성장하고 서로 닿으면 하나로 병합된다.

이미 칠한 셀의 색은 덧칠하거나 병합할 때 바뀌지 않는다. 명중별 면적 공급은 각 연결 영역 안에서 먼저 들어온 순서(FIFO)대로 소비하고, 병합 뒤에도 공급 순서를 유지한다. 한 셀을 채우는 데 이전 공급의 남은 소수 면적과 다음 공급이 함께 필요하면 **그 셀을 마지막으로 채운 공급자의 색과 소유자**를 사용한다. 마지막 전체 셀을 채운 공급자가 `lastHit`를 받으며, 가장 최근에 던진 사람이 반드시 받는 것은 아니다.

가로 이음새는 연결하고 남·북극에서는 세로 범위를 고정한다. 모든 빈 셀에는 인접 성장 경로가 있으므로 기한 내에 충분한 명중 예산과 성장 시간을 확보하면 같은 지점을 반복해서 맞혀도 전체 100%까지 도달할 수 있다. `Fill Chance`가 0 또는 100이어도 다른 후보로 진행하는 대체 경로를 유지한다. 셀은 투명 또는 불투명이며, 진행도는 `CoveredCount / CellCount`다. 이 비율은 화면 픽셀·조명·실제 메시 삼각형 면적을 측정한 값이 아니다.

제한 시간 전에 마지막 셀이 완성되면 공동 Victory가 되고, 마지막 성장 시각과 제한 시간 종료가 정확히 같으면 Defeat가 우선한다. 늦게 도착한 Tick도 제한 시간 전에 완료될 성장은 해당 시각까지 계산한다. 공동 일시정지 동안 성장·전투 시계·입력·물리는 멈추며 재개 뒤 이어간다. 일반 Retry는 모드와 캡처한 성장 설정을 유지하면서 맵·영역·공급 예산·명중 수·난수 진행을 초기화한다. 새 DEV 값은 다음 시작/재시작에 캡처하며 진행 중인 판에는 적용하지 않는다. 로비 복귀와 방 이탈도 표시·수신 상태를 정리한다.

HUD 목표는 항상 100%다. `snapshot.paintCellCount > 0`이면 `paintCoveredCount / paintCellCount`를 사용하며 숫자는 소수 첫째 자리까지 내림한다. 예를 들어 마지막 한 셀이 남으면 99.9%로 표시하고 막대도 미완료 상태를 유지한다. 셀 수가 없는 예전 검사 fixture만 `paintCoverage`를 보완값으로 쓴다. 상단 Paint 제목은 **GOAL**, Crack은 기존 **YOKAI HP**다. UI 배치·Scene 객체를 새로 만들지 않는다. Renderer 비활성·재생성은 Host의 색칠 상태를 지우지 않는다. `LabGourdPaint.splatRadius`와 `LabPaintSurface.Stamp`의 기존 부드러운 브러시는 연결 전 미리보기 전용이다.

Host의 권위 상태는 `LabModel.PaintSurface` 하나이며, 성장 계산은 Host의 전투 시간 진행에서만 한다. Host 화면도 현재 Snapshot과 같은 버전의 `LabNetwork.PaintSurface` 표시 캐시를 사용해 맵과 HUD를 함께 반영한다. Client는 승인 명중 방향을 재생해 성장시키지 않고 Host가 보낸 셀 변경(delta) 또는 전체 체크포인트를 별도 수신 맵에 적용한다. 분할 수신 중에는 기존 공개 표시 캐시를 유지하고, 완성된 맵의 세션·라운드·해상도·Revision·칠한 셀 수·전체 셀 수·명중 수가 Snapshot 메타데이터와 모두 맞을 때 맵과 HUD를 함께 공개한다. 픽셀이 그대로인 반복 명중도 명중 수가 바뀌면 빈 delta를 발행한다.

전송은 현재 프로토콜 **5629**의 `paint_cells`·`paint_resync` 경로를 사용한다. 셀 ID와 RGBA를 담은 delta보다 전체 맵이 작으면 전체 맵을 보낸다. base64 데이터는 조각당 최대 4,096자, 수신 조각 수는 최대 64개이며 기존 패킷 한도 32 KiB 안에서 전송한다. 최대 해상도 전체 RGBA 맵은 131,072바이트이고 base64 174,764자를 43개로 나눈다. 수신 시 헤더·기준 Revision·셀 ID 범위와 순서·payload 크기·알파/색·칠한 셀 수·SHA-256 해시를 검사한 뒤 전체 맵을 한 번에 교체한다. 중복 완료·이전 전송/스냅샷 번호·이전 라운드를 거절하고, 기준 맵 누락이나 손상에는 Host의 마지막 발행 맵 복사본으로 체크포인트를 요청한다. `sequence`는 연결 전체의 색칠 전송 번호, `epoch`는 그 맵을 발행한 게임 Snapshot 번호다. 체크포인트는 같은 맵 버전의 `epoch`를 유지하며 `sequence`는 새 번호로 증가한다.

이 복구는 **현재 라운드에 계속 연결된 Client의 부분 맵 누락**을 복원하는 기능이다. 연결이 끊어진 뒤 전투에 재접속하는 기능은 포함하지 않는다. 실제 다중 앱·기기에서의 전송과 화면 일치는 자동 패킷 fixture와 구분해 검증한다.

#187에서 프로토콜 **5628**과 당시 111개 설정·색칠 전송 형식을 추가했다. #190은 직구 설정을 덧붙여 프로토콜 **5629**와 총 **118개** 설정을 사용하며 기존 색칠 전송 형식은 유지한다. 111개 배열이나 다른 프로토콜은 현재 방 설정과 섞지 않는다. 이전 PASS는 직구 폼의 실행 결과로 승계하지 않으며 실제 컴파일·EditMode·PlayMode·다중 앱·기기·iOS 결과는 `docs/VALIDATION.md`에서 변경별로 구분한다.

근거: `LabConfig.PaintGrowthSettings`·`DeveloperFields`·`CaptureDeveloperValues`·`TryApplyDeveloperValues`, `LabPaintSurface.RegisterHit`·`Advance`·`TryReplacePixels`·`CopyPixels`, `LabModel.ConfirmHit`·`TryAdvance`·라운드 Rules, `LabPaintPublisher`·`LabPaintReceiver`, `LabNetwork.PaintSurface`·Snapshot 공개 경로, `LabGourdPaint.Attach`·`ApplyMode`, `LabHud`, `LabGourdCrack`·`LabGourdFinish`.

### 박 폭탄 방해공작 — 89·90번째 설정 (#177)

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Gourd Bomb Interval Min (seconds, 0 = off) | 20 | 0–600 / 초 | 89번째(인덱스 88), `Gourd` 묶음. 박이 무작위 한 명에게 폭탄을 떨어뜨리는 간격의 하한. 매 폭탄마다 Min~Max 사이에서 새로 뽑는다. Min ≤ Max여야 한다. |
| Gourd Bomb Interval Max (seconds, 0 = off) | 30 | 0–600 / 초 | 90번째(인덱스 89), `Gourd` 묶음. 간격의 상한. **0이면 박 폭탄이 나오지 않는다.** 아주 작은 값을 넣어도 한 번에 쏟아지지 않도록 실제 간격은 최소 1초다. |

Host 모델이 라운드 시작 시각부터 간격을 세고, 시각마다 대상(활성 자리 중 무작위, 같은 사람 연속 허용)과 착지 위치(판 폭 20~80%, 높이 0.6)를 세션·라운드 seed로 추첨해 잠금 없는 폭탄(출처 `Gourd`)을 만든다. 가득 찬 판에도 넣고 스태미나를 쓰지 않으며 생성 기록에 세지 않는다. 일시정지 동안은 전투 시계와 함께 멈추고, 판이 끝나면 더 만들지 않는다. 기본값이면 180초 한 판에 6~9개다.

Host는 박 폭탄 신호(받는 사람·폭탄 ID·착지 위치)를 그 폭탄이 처음 담긴 스냅샷보다 먼저 모든 참가자에게 보낸다. 모든 화면은 전투 화면 박 옆에 "!" 말풍선(`LabGourdAlert`, 약 1.4초)을 띄우고, 받는 화면은 착지 지점 "!" 경고(`LabDropWarning`) 뒤 폭탄을 구슬판 위쪽에서 떨어뜨려 튕긴다. 경고·낙하·튕김 시간과 높이, 경고 색은 `LabOrbAppearance`의 **Gourd bomb drop**(기본 0.6·0.45·0.45초, 튕김 높이 구슬 반지름 1배), 말풍선 크기·시간은 `LabGourdAlert`의 기본값(150px·1.4초)이며 DEV로 조정하지 않는다. 내려앉기 전에는 잡기·충돌·폭발 밀림·떠돌기가 없다.

근거: `LabConfig.GourdBombIntervalMin`·`GourdBombIntervalMax`, `LabModel.DropGourdBombs`·`GourdBombDelay`·`DrainGourdBombs`, `LabNetwork.AnnounceGourdBombs`·`GourdBombDropped`, `LabOrbBoard.ExpectDrop`, `LabOrbView.PlayDrop`, `LabDropWarning`, `LabGourdAlert`.

## 4. 스태미나·구슬 생성

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Maximum Stamina | 100 | 1–1,000,000 / 스태미나 | 개인 스태미나 상한. 시작량·생성 비용·회복량·명중 보상은 이 값 이하여야 한다. |
| Starting Stamina | 100 | 0–1,000,000 / 스태미나 | 새 라운드에서 각 플레이어가 갖는 양. 높이면 처음 만들 수 있는 구슬 수가 늘 수 있다. |
| Orb Generation Cost | 20 | 0.001–1,000,000 / 스태미나/개 | `GENERATE` 요청이 Host에서 승인될 때 구슬(음·양·아이템) 한 개당 소비. 현재 보유량이 비용 미만이면 생성이 거절된다. |
| Stamina Recovery Amount | 20 | 0–1,000,000 / 스태미나 | 아래 간격과 함께 자동 회복 **속도**를 정한다. 0이면 시간에 따른 회복이 없다. |
| Stamina Recovery Interval (seconds) | 1.7 | 0.001–86,400 / 초 | 회복 속도 = `Recovery Amount ÷ Recovery Interval`. 현재 값은 **초당 약 11.76씩 연속 회복**한다. “1.7초마다 한 번에 20”으로 처리하지 않는다. 북 1개가 세 사람을 한 바퀴 도는 약 6~9초 안에 빈 스태미나가 거의 다 차도록(약 8.5초) #146에서 3초 → 1.7초로 줄였다. |
| Stamina Reward per Hit | 10 | 0–1,000,000 / 스태미나 | Host가 실제 명중을 확정했을 때 그 투사체의 공격자에게만 즉시 더한다. 최대 스태미나를 넘지 않으므로 이미 가득 차 있으면 버려진다. 빗나감은 보상이 없다. #146에서 5 → 10(구슬 ½개)으로 올렸다. |
| Stored Orb Limit | 20 | 정수 1–100 / 구슬 개수 | 각 플레이어가 보관하는 비행 전 구슬의 수량 상한. 생성자뿐 아니라 전달받는 사람의 남은 자리도 검사한다. |

예시: 기본값에서 구슬 한 개를 만들면 100→80이 된다. 다른 행동이 없다면 1.7초 동안 `20/1.7 × 1.7 = 20`이 연속으로 회복돼 다시 100이 된다. 0에서 100까지는 약 8.5초가 걸린다. 실제 회복은 Host 시간 경과를 처리할 때 갱신된다. 생성되는 종류는 아래 **8절의 생성 확률**을 따른다.

근거: `LabConfig.StaminaRecoveryPerSecond`, `LabModel.TryGenerate`, `TryTransfer`, `ConfirmHit`, `TryAdvance`.

## 5. 구슬 수명·3D 조합판·생성 연출

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Orb Lifetime (seconds) | 8 | 0.1–86,400 / 초 | 보관 중인 Raw·Combined의 수명(아이템은 8절의 `Item Lifetime`). Raw는 생성 시점, Combined는 조합 시점부터 센다. 이웃에게 전달해도 수명은 초기화되지 않는다. 이미 발사돼 비행 중인 구슬은 이 만료 대상이 아니다. |
| Orb Bounce | 0.8 | 0–1 / 3D 접촉 탄성 | 구슬·상하 벽의 `PhysicsMaterial.bounciness`. 높이면 접촉 후 더 잘 튀는 경향이 있다. 충돌 결과는 상대 물체와 물리 설정에도 영향을 받는다. |
| Orb Floor Deceleration | 2 | 0–100,000 / 보드 폭/초² | 손을 놓은 뒤 XY 평면에서 움직이는 3D 구슬의 속력에서 매 물리 스텝 감산한다. 높이면 더 빨리 멈춘다. Unity의 `Rigidbody.linearDamping`이나 접촉 재질의 마찰을 조정하는 값은 아니다. |
| Orb Stop Speed | 0.065 | 0–100,000 / 보드 폭/초 | 구슬 속력이 이 값 이하이면 0으로 만든다. 높이면 낮은 속도의 구슬이 일찍 멈춘다. `Orb Maximum Release Speed` 이하여야 한다. |
| Orb Maximum Release Speed | 3 | 0.001–100,000 / 보드 폭/초 | 잡은 조합판 구슬을 놓을 때 부여할 최대 평면 이동 속력. 높이면 더 세게 밀 수 있다. Host가 받는 이동 샘플의 안전 상한도 이 값의 16배로 계산된다. 3D 투척 속도 상한과 별개다. |
| Orb Radius | 0.32 | 0.001–100 / 월드 단위 | 조합판 구슬 루트의 3D `SphereCollider` 반경과 기본 구체 표시 반경. 잡기·직접 겹침 조합 거리, 판 가장자리 여백에도 쓰인다. 크기를 키우면 아래의 판 높이 조건을 다시 맞춰야 한다. |
| Orb Board Height | 5 | 0.1–1,000 / 월드 단위 | 구슬 전용 직교 카메라의 세로 표시 높이. 가로 길이는 화면 비율에 따라 달라지고 판의 물리 경계도 함께 다시 계산된다. |
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
| Drum Count | 1 | 정수 1–5 / 개 | 한 판에 놓이는 생성 북 수. 판 시작 때 Host가 북마다 받을 사람을 무작위로 뽑는다(북이 인원 이하이면 서로 다른 사람, 많으면 모두 하나씩 받은 뒤 나머지도 무작위). 북은 받은 사람 판의 가운데에서 시작하고, **북이 있는 사람만 생성**할 수 있다. 공유 방 설정에 포함된다. **75번째(인덱스 74)**. |
| Drum Radius | 0.6 | 0.1–1.5 / 월드 단위 | 북의 충돌 구 반지름이자 북 그림 크기. 기본은 예전 하단 생성 버튼의 화면 크기에 맞췄다(구슬 지름의 약 2배). 잡기 판정도 이 크기에 맞춰 넓어진다. 새 라운드부터 적용된다. **76번째(인덱스 75)**. |
| Drum Drift Speed (board widths/s) | 0.1 | 0–1 / 보드 폭/초 | 가만히 둔 북이 저절로 떠도는 최고 속력(실제 속력은 그 50~100% 사이에서 천천히 변함). 0이면 떠돌지 않는다. 밀리거나 튕긴 북은 구슬처럼 감속하다 이 속력까지 줄면 다시 떠돈다. 이웃에게 넘어가려면 바깥쪽 속력이 이 값의 1.5배를 넘어야 하며, 그보다 느리게 가장자리에 닿으면 되돌아온다. **77번째(인덱스 76)**. |
| Drum Drift Turning (per second) | 0.25 | 0–2 / 초⁻¹ | 떠도는 방향이 바뀌는 빠르기(부드러운 노이즈의 진행 속도). 높이면 자주 방향을 틀고, 0이면 같은 방향을 유지한다. 벽·옆 가장자리 0.15 보드 폭 안에서는 방향과 상관없이 멀어지는 쪽으로 튼다. **78번째(인덱스 77)**. |
| Drum Spin Strength | 1 | 0–3 / 배 | 북 그림 회전의 세기. 떠도는 길이 휘는 쪽으로 도는 회전(최대 초당 90° × 세기)과, 구슬·벽이 스치며 접점이 미끄러지는 방향으로 도는 회전(최대 초당 540°)에 모두 곱한다. 회전은 그림만 바꾸고 충돌·위치에는 영향이 없다. 0이면 돌지 않는다. **79번째(인덱스 78)**. |

판 높이와 반경은 별도 기하 검사를 통과해야 한다. #154의 종이판·하단 HUD 배치는 유지하고, `LabOrbAreaProfile`의 종이판 상하 윤곽과 HUD 불투명 상단 윤곽을 합성한 **실제로 보이는 곡선 영역**에서 구슬과 북의 원형 몸체가 전체 가로 구간·양끝 통로를 통과할 수 있어야 APPLY가 진행된다. 기울어진 경계는 실제 반지름과 선분의 거리를 반영한다. 곡선 물리를 위해 그림을 위로 옮기거나 축소하지 않으며 일괄 0.05 여유·수평 HUD 컷을 적용하지 않는다. 후보 `BoardHeight`로 계산만 하며 검증 중 카메라 배율은 바꾸지 않는다. 화면 비율·HUD 배치·윤곽 제어점에 따라 같은 값의 통과 여부가 달라질 수 있다. 근거는 `LabSceneController.ValidateBoardTuning`, `LabOrbAreaProfile.TryBuildShape(plateWorldRect, hudWorldRect, ...)`, `LabOrbAreaShape.CanFitRadius`다.

근거: `LabModel.TryAdvance`·`TryCombine`, `LabOrbBoard.Configure`·`EndDragAtWorld`·`FixedUpdate`·`BuildHorizontalWalls`, `LabOrbView.PlaySpawnRise`, `LabSceneController.ReconfigureBoard`.

### 북 멈춤 구간 — 92~99번째 설정 (#178)

옆 사람에게 넘긴 북이 받는 판 어디에서 멈출지를 Host가 넘길 때마다 정한다. 거리는 **북이 들어온 쪽 가장자리에서 잰 판 폭 비율**이다. 모두 정수이고 `Drum` 묶음에 있다.

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Drum Stop Near From (% of board from entry) | 20 | 정수 0–100 / % | 가까운 쪽 구간의 시작. **92번째(인덱스 91)**. |
| Drum Stop Near To / Middle From (%) | 40 | 정수 0–100 / % | 가까운 쪽 끝이자 가운데 시작. **93번째(인덱스 92)**. |
| Drum Stop Middle To / Far From (%) | 60 | 정수 0–100 / % | 가운데 끝이자 먼 쪽 시작. **94번째(인덱스 93)**. |
| Drum Stop Far To (%) | 80 | 정수 0–100 / % | 먼 쪽 끝. 폭발에 밀려 넘어온 북도 이 안에서 멈춘다. **95번째(인덱스 94)**. 네 경계는 앞에서 뒤로 커지거나 같아야 한다. |
| Drum Stop Near Chance (%) | 40 | 정수 0–100 / % | 가까운 쪽 구간에서 멈출 확률. **96번째(인덱스 95)**. |
| Drum Stop Middle Chance (%) | 35 | 정수 0–100 / % | 가운데 구간. **97번째(인덱스 96)**. |
| Drum Stop Far Chance (%) | 15 | 정수 0–100 / % | 먼 쪽 구간. **98번째(인덱스 97)**. |
| Drum Pass Through Chance (%) | 10 | 정수 0–100 / % | 멈추지 않고 그다음 사람에게 통과할 확률. 네 확률의 합은 반드시 100이어야 한다. **99번째(인덱스 98)**. |

Host는 구간을 확률로 뽑은 뒤 구간 안의 한 지점을 고르게 뽑는다(seed와 넘김 순번으로 정해져 모든 기기가 같은 결과). 앞 판을 실제로 통과해 온 북은 다음 판의 가까운 쪽 구간에서 반드시 멈추고, 폭발 충격파에 밀려 넘어온 북은 제 속도로 미끄러지며 보통 감속하되 먼 쪽 끝을 넘을 때만 더 세게 감속해 그 안에서 멈춘다. 받는 화면은 들어온 속도를 이어받아 남은 거리만큼만 감속해 지점에 선다. 너무 세면 들어오며 감속 상한(8 판 폭/초²)에 맞게 줄이고, 너무 느리면 1.2초 안에 도착하도록 올린다. 통과 북은 최소 0.9 판 폭/초로 반대편까지 간다. 이 시간·감속·통과 속도는 코드 상수이며 DEV로 바꾸지 않는다. 멈춘 뒤에는 보통처럼 떠돌고, 도착 중에 잡거나 폭발에 맞으면 도착이 끝난다. 확률 합이 100이 아니거나 경계 순서가 틀리면 DEV 적용을 통째로 거부하고, Inspector 배열이 잘못되면 기본값을 쓴다.

근거: `LabConfig.DrumStopBounds`·`DrumStopChances`, `LabModel.TryTransfer`·`ArriveDrum`, `LabOrbBoard.StartArrival`·`Arrive`·`Blast`, `LabNetwork.OnEdgeCrossed`·`SyncBoard`.

### 미니맵 — 100~103번째 설정 (#181)

| 항목 | 기본값 | 허용 범위 / 단위 | 실제 효과 |
| --- | --- | --- | --- |
| Minimap (0 off, 1 on) | 1 | 정수 0–1 | 전투 화면 오른쪽 아래(오른쪽 이웃 동그라미 위)의 미니맵을 켜고 끈다. **100번째(인덱스 99)**. Inspector `Minimap Enabled`와 같은 값이다. |
| Minimap Size (%, 50 = default) | 50 | 정수 0–100 / % | 미니맵 크기. 기본 50이 가운데이고 그린 크기(128×116)의 절반(64×58)이다. 100이면 그린 크기, 0이면 보이지 않는다. Mac 3앱에서 확인해 50을 기본으로 정했다. 오른쪽 아래 모서리를 기준으로 커지거나 작아진다. **101번째(인덱스 100)**. Inspector `Minimap Size Percent`와 같은 값이다. |
| Minimap Move X (- left, + right) | 0 | 정수 -300–100 / UI 단위 | 기본 자리(오른쪽 이웃 동그라미 위)에서 좌우로 옮기는 거리. 화면 폭 390 기준. **102번째(인덱스 101)**. Inspector `Minimap Move X`. |
| Minimap Move Y (- down, + up) | 0 | 정수 -300–500 / UI 단위 | 기본 자리에서 위아래로 옮기는 거리. 화면 높이 844 기준. **103번째(인덱스 102)**. Inspector `Minimap Move Y`. |

미니맵은 플레이어마다 원 하나를 정다각형 꼭짓점에 놓고 이웃끼리 선으로 잇는다. 폰마다 "나"(갈색 원)를 아래 가운데에, 오른쪽 이웃(다음 번호)을 오른쪽 위, 왼쪽 이웃(이전 번호)을 왼쪽 위에 둔다. 북을 가진 사람의 원은 태극 무늬(`Resources/Minimap/Taegeuk.png`)로 덮이고, 북이 넘어가면 태극이 이웃 선을 따라 한 칸에 0.35초씩 미끄러진다(통과한 북은 중간 원을 거친다). 전투 중(Playing)에만 보이며 화면 표시일 뿐 판정에는 영향이 없다. 색·선 굵기·이동 시간은 `LabDrumMinimap` 컴포넌트 값이고 DEV로 바꾸지 않는다.

근거: `LabConfig.MinimapEnabled`·`MinimapSizePercent`·`MinimapMove`, `LabDrumMinimap.UnitPosition`·`Rebuild`·`SyncDrums`·`Advance`, `LabSceneController.Awake`.

## 6. 전투 카메라·3D 발사점

카메라와 발사점은 모두 요괴의 `target.transform.position`을 기준으로 각 플레이어 자리 방향에 놓인다. **카메라만** 바꾸면 충돌 물리는 바뀌지 않지만 보이는 궤적은 달라질 수 있다.

| DEV 입력 이름 | 현재 기본값 | DEV 범위 / 단위 | 실제 동작과 조정 효과 |
| --- | ---: | --- | --- |
| Camera Radius | 8 | 0.001–1,000 / 월드 단위 | 요괴 중심에서 플레이어 전투 카메라까지의 수평 거리. 높이면 시점이 뒤로 물러난다. |
| Camera Height Offset | 0.65 | −1,000–1,000 / 월드 단위 | 요괴 중심에 대한 카메라 높이 차이. 카메라는 아래 `Camera Look Height Offset`의 지점을 바라본다. |
| Camera Look Height Offset | 2 | −10–10 / 월드 단위 | 카메라가 바라보는 지점의 높이를 요괴(피냐타) 중심에서 위아래로 옮긴다. 값을 올리면 화면에서 피냐타가 아래로, 내리면 위로 보인다. 카메라·발사 지점이 피냐타 중심을 따라가므로 피냐타 Transform을 옮겨도 화면 구도는 거의 같고, 구도는 이 값과 `Camera Height Offset`·`Camera Radius`로 바꾼다. 투척·명중 판정에는 영향이 없다. DEV 필드 57번째(인덱스 56). |
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

기존 **Arc** 발사 계산은 다음 순서이며 기본 모드도 Arc다. 저장 원본의 `Throw Power Exponent`는 1.2다. 코드로 새 `LabConfig`를 생성했을 때의 1과 구분한다.

1. 손 속력 `s`에 대해 `shaped = 1.5 × (s / 1.5) ^ PowerExponent`를 구해 가로·세로 입력에 같은 비율로 적용한다.
2. `up = max(MinimumUpwardSpeed, shapedY × UpwardGain)`, `forward = shapedY × ForwardGain`, `lateral = shapedX × LateralGain`을 구한다.
3. `atan2(up, forward) + LoftOffset`을 3°–87°로 제한해 위·전진 성분을 다시 나누고, 전체 벡터 크기를 `MaximumWorldSpeed`로 제한한다.
4. 투사체 Rigidbody는 이 초기 속도로 출발한다. 이후 매 물리 스텝 발사 때 캡처한 자체 중력과 `linearDamping`을 받고, Host의 Collider 충돌 또는 발사 때 캡처한 수명 종료로 HIT/MISS가 결정된다. Host만 HP·명중 보상·색칠을 확정하며 다른 기기의 투사체는 표시용이다.

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

솔로는 START/RESTART SOLO, 공유 방은 Host의 CREATE ROOM/APPLY TO ROOM/공동 일시정지 뒤 APPLY & RESTART로 적용한다. **118개 전체**를 검증·지문화한 설정을 참가자에게 전파하고 확인받은 뒤 시작한다. 프로토콜은 **5629**이며 Host는 확정한 발사 위치·속도를 Client에 보내고 Client는 공유 설정의 같은 중력을 사용한다. Client가 직접 모드를 바꾸지 않는다. Inspector 변경도 새 판 시작 시점에 적용해 비교한다.

Inspector의 비정상 enum은 Arc로 보정하고 오프셋·각도·배율의 유효 Getter는 범위 제한과 유한수 대체값을 사용한다. DEV/네트워크는 잘못된 값 하나라도 있으면 118개 전체 적용을 거절한다. 극단적인 오프셋이나 음수 발사각은 화면 밖 출발·바닥 접촉·빗나감을 만들 수 있으며 자동 보정 대상이 아니다.

근거: `LabConfig.ThrowForm`·`StraightOriginOffset`·`StraightPitchOffsetDegrees`·`StraightLoftScale`·`StraightGravityScale`·`EffectiveThrowGravity`, DEV 인덱스 111~117, `LabDeveloperMode.RefreshThrowFormInputs`, `LabThrowMath.LaunchPosition`·`LaunchVelocity`·`ArcVelocity`, `LabProjectile.Prepare`·`FixedUpdate`·`Update`, `LabNetwork.SpawnHostProjectile`, `LabSessionSettings.Fingerprint`. 이 절은 코드 연결 설명이며 실제 실행 결과를 단정하지 않는다.

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
| Bomb Chance (%) | 0 | 정수 0–100 / % | 북에서 나오는 폭탄 확률. 북 폭탄도 박 폭탄과 같은 규칙(잠금 없음·던질 수 없음)을 따른다. 6개 확률의 합은 반드시 100이어야 한다. |
| Item Lifetime (seconds) | 15 | 0.1–86,400 / 초 | 보관 중인 아이템의 수명. 생성 시점부터 세며 이웃에게 전달해도 초기화되지 않는다. 폭탄에게는 도화선이며, 끝나면 그때 보유자의 판에서 터진다. |
| Item Unlock Swipes Min | 2 | 정수 0–20 / 회 | 폭탄을 뺀 아이템이 생성될 때 정해지는 잠금 횟수의 하한(폭탄은 항상 잠금 0). 잠금은 **이웃 화면으로 넘길 때마다** 1씩 준다. |
| Item Unlock Swipes Max | 4 | 정수 0–20 / 회 | 잠금 횟수의 상한. Min ≤ Max여야 한다. Min·Max를 모두 0으로 두면 아이템이 처음부터 해금된 상태로 생긴다. |
| Potion Heal (HP) | 10 | 정수 0–300 / 개인 HP | 해금된 회복물약을 더블 탭했을 때 사용자의 개인 HP에 더하는 양. `Player Max HP`를 넘지 않는다. HP가 가득 차 있거나 0이면 사용이 거절되고 물약은 남는다. |
| Relay x2 Damage Multiplier | 2 | 1–20 / 배 | 릴레이 구슬이 해금 후 한 번 더 넘겨져 `×2` 단계일 때 `Damage per Hit`에 곱하는 값. 해금 직후 `×1` 단계는 항상 1배다. |
| Relay MAX Damage Multiplier | 4 | 1–20 / 배 | 릴레이 구슬이 해금 후 두 번 넘겨져 `MAX` 단계일 때의 배율. 이후 더 넘겨도 오르지 않는다. |
| Bomb Damage Multiplier | 3 | 1–20 / 배 | 폭탄을 던져 명중했을 때 `Damage per Hit`에 곱하는 값. #177부터 폭탄은 던질 수 없어 **지금은 쓰이지 않는다.** 폭탄 기믹을 다시 다듬을 때를 위해 값을 남겨 둔다. |
| Bomb Blast Radius (board widths) | 0.4 | 0–2 / 보드 폭 | 폭탄이 터질 때 충격파가 닿는 거리. 폭탄 중심에서 이 거리 안에 중심이 있는 구슬·북·잡고 있던 구슬을 밀어낸다. 0이면 밀지 않고 이펙트만 나온다. 이펙트의 흰 원이 이 범위까지 퍼진다. **81번째(인덱스 80)**. |
| Bomb Blast Strength (board widths/s) | 9 | 0–20 / 보드 폭/초 | 폭탄 바로 옆이 밀려나는 속도. 범위 끝으로 갈수록 0까지 줄고, 원래 움직이던 속도에 더해진다. 9면 범위 중간(판 폭 20%)의 구슬도 바닥 감속으로 멈추기 전 판 폭의 약 5배를 갈 만큼 세서, 범위 안 구슬은 대부분 옆 가장자리를 넘어 이웃에게 간다. #167에서 3으로 시작해 화면 확인 후 9로 올렸다. **82번째(인덱스 81)**. |

아이템은 음·양과 조합되지 않는다. 해금된 **회복물약·시크릿 상자**는 같은 구슬을 0.35초 안에 두 번 탭해 그 자리에서 사용한다(한 번의 탭은 0.3초 이내, 이동 거리 구슬 반지름의 절반 이하). 시크릿 상자는 양옆에 속성 구슬 2개를 만들고 새 `Orb Lifetime`을 주며 보관 칸 하나가 더 필요하다. 해금된 **릴레이 구슬**은 Combined처럼 위로 그어 던진다. **폭탄**은 잠금이 없고 던질 수 없으며 넘겨도 도화선이 다시 시작되지 않는다. 도화선은 `Item Lifetime`이고, 끝나면 그때 보유자의 판에서 터져 충격파로 주변을 밀어낸다(아무것도 사라지지 않음). 몸통의 빨간 숫자가 남은 초이며, 도화선 끝 불꽃과 치지직 소리가 함께 난다. 터질 때의 진동은 `Sound and haptics` 칸의 `Bomb Haptic Style`(83번째, 기본 2 = Heavy)·`Bomb Haptic Intensity`(84번째, 기본 1, 0이면 끔)로 정하고, 남은 초가 몇 초부터 깜빡일지·빠르기·색은 `LabOrbAppearance`의 Inspector(`Bomb Fuse Warning`, 기본 3초·초당 2→8회)에서 정한다. 폭탄 그림(몸통 `ItemBombBody`·꼭지와 도화선 `ItemBombFuse`)의 크기·꼭지 방향·불꽃 위치·남은 초 글씨의 색·크기도 같은 Inspector의 **Bomb art**에서 정한다. 폭탄은 가만히 두면 북의 `Drum Drift Speed`·`Drum Drift Turning`으로 떠돌고 혼자서는 이웃 화면으로 넘어가지 않으며, 움직인 만큼 굴러 꼭지가 공 둘레를 돈다(몸통 뒤로 숨지 않음). 피해 배율의 결과는 반올림한 정수이며 최소 1이다. 1인 솔로에는 이웃이 없어 잠긴 아이템이 해금되지 않으므로, 효과를 혼자 시험하려면 `Item Unlock Swipes Min`·`Max`를 0으로 둔다.

근거: `LabConfig.GenerationChances`, `LabModel.TryGenerate`·`TryCombine`·`TryTransfer`·`TryUseItem`·`TryAdvance`·`ConfirmHit`, `LabOrbBoard.EndDragAtWorld`, `LabOrbItemMarker.Show`, `LabNetwork.SyncBoard`.

### 결합 소리·햅틱 — 85~87번째 설정

| 순서 | 항목 | 기본값 | 입력 범위 | 효과 |
| --- | --- | --- | --- | --- |
| 85 (인덱스 84) | `Combine Sound Volume (50 = Inspector)` | 50 | 0~100 | 내가 요청한 결합을 Host가 승인했을 때 결합한 사람 기기에서 나는 소리의 크기. 아래 "DEV 소리 크기" 규칙을 따른다(Inspector 기본 0.8). 소리 파일은 `Assets/Lab/Resources/Audio/Combine`의 파일 중 하나를 무작위로(연속 같은 소리 없이) 재생하며, 파일이 없으면 소리가 나지 않는다. |
| 86 (인덱스 85) | `Combine Haptic Style (0-4)` | 0 = Light | 0~4 정수 | 결합 진동 종류. 0 Light, 1 Medium, 2 Heavy, 3 Soft, 4 Rigid. |
| 87 (인덱스 86) | `Combine Haptic Intensity` | 1 | 0~1 | 결합 진동 세기. 0이면 진동하지 않는다. `Haptics (0 off, 1 on)`이 0이면 함께 꺼진다. |

결합이 거절되면 소리·진동이 나지 않는다. Host는 승인 즉시, Client는 Host가 보내는 `combined` 알림을 받았을 때 재생한다. 아이폰 빌드에서만 실제로 진동한다.

### DEV 소리 크기 — 64~67번째, 85번째, 91번째 설정

BGM·생성·타격·빗나감·결합·도화선 소리 크기 6개(인덱스 63~66, 84, 90)는 DEV에서 **0~100**으로 입력하며 기본값은 **50**이다. 50은 Inspector(`Resources/LabConfig`)에 적힌 크기를 그대로 쓴다는 뜻이고, 실제 크기 = Inspector 값 × (DEV 값 ÷ 50)이다. 100은 Inspector 값의 2배, 25는 절반, 0은 소리 없음. DEV 값은 Inspector에 보이지 않으며 원본 에셋을 바꾸지 않는다. 기기별 일시정지 메뉴의 BGM/효과음 슬라이더가 여기에 한 번 더 곱해진다.

91번째(인덱스 90) `Fuse Sound Volume (50 = Inspector)`는 #177의 도화선 치지직 소리다(Inspector 기본 0.5). 내 판에 폭탄이 타는 동안 반복하며, 가장 빨리 터질 폭탄의 마지막 3초에는 크기가 0.6배에서 1배로, 빠르기가 1.35배까지 오른다. 일시정지 중이거나 전투가 끝나면 멈춘다. `Assets/Lab/Resources/Audio/Fuse`에 소리 파일을 넣으면 그 첫 파일을, 없으면 코드로 만든 1초 반복음을 쓴다.

104번째(인덱스 103) `Bomb Sound Volume (50 = Inspector)`는 #183의 폭탄 "뻥이요" 소리다(Inspector 기본 0.9). 내 판에서 폭탄이 터질 때 그 판 주인 기기에서만 한 번 난다. `Assets/Lab/Resources/Audio/Bomb`의 파일 중 하나를 무작위로(연속 같은 소리 없이) 재생하며, 파일이 없으면 소리가 나지 않는다. 다른 소리 크기와 같이 50 = Inspector 값, 100 = 2배, 0 = 끔이다.

105번째(인덱스 104) `Bomb Sound (0 boom, 1 ppeong)`는 폭탄 소리 종류를 고르는 토글이다(기본 1 = PPEONG). BOOM은 `Audio/Bomb/Boom`의 일반 폭발음, PPEONG은 `Audio/Bomb/Ppeong`의 뻥튀기 기계 소리를 쓴다. 고른 쪽 파일이 없으면 다른 쪽을 쓴다. 팀 플레이 테스트 뒤 하나로 정할 예정이며, Inspector `Bomb Sound Style`과 같은 값이다.

#172 아이폰 확인 뒤 진동 기본값을 정리했다: 생성 Medium·1, 타격 Heavy·1, 아이템 타격 Heavy·1, 폭탄 Heavy·1, 결합 Light·1. 세기 차이는 진동 종류(Light < Medium < Heavy)로 낸다.

## 9. 입력 제약과 조정 실험 예시

**값 간 관계:** `Starting Stamina`, `Orb Generation Cost`, `Stamina Recovery Amount`, `Stamina Reward per Hit`은 모두 `Maximum Stamina` 이하여야 한다. `Orb Stop Speed ≤ Orb Maximum Release Speed`, `Throw Minimum Swipe Speed ≤ Throw Maximum Swipe Speed`, `Throw Minimum Upward Speed ≤ Throw Maximum World Speed`, 생성 확률 6개의 합 = 100, `Item Unlock Swipes Min ≤ Item Unlock Swipes Max`도 필수다. 관계 위반 시 전체 입력이 거절된다.

**기준 실험:** DEFAULTS를 불러와 APPLY로 솔로를 시작한다. 비교 시간을 확보하려면 전투 시간·구슬 수명을 늘리고 생성 비용을 낮추되, 변경값을 함께 기록한다. Yin과 Yang을 직접 겹쳐 Combined를 만든 후 판 중앙에서 위로 던진다. 투척 비교에서 아이템이 섞이지 않게 하려면 `Yin Chance`·`Yang Chance`를 50으로, 아이템 확률을 0으로 둔다. `SHOT HIT/MISS | 비행 시간 | peak +최고 상승 | range 수평 이동`와 Paint 진행도 또는 Crack HP 변화를 적는다. 같은 손동작을 여러 번 반복하고 **한 번에 한 값**만 바꿔 APPLY + RESTART SOLO로 비교한다. Console/Player 로그에는 `C6_LAB_THROW`(입력·발사 속도)와 `C6_LAB_PROJECTILE`(충돌·비행 지표)가 남는다.

**예시 질문별 첫 조정 대상**

| 관찰·목표 | 먼저 비교할 값 | 함께 확인할 것 |
| --- | --- | --- |
| 직구를 낮고 빠르게 보이게 비교함 | Throw Form → STRAIGHT, Origin/Pitch/Loft/Gravity를 하나씩 변경 | 같은 초기 속력·수평 조준 유지, peak·실제 접촉점·색칠·소유자 자리별 출발점을 비교 |
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
| 박 폭탄이 너무 잦거나 드묾 | Gourd Bomb Interval Min·Max ↑↓ (Max 0이면 끔) | 180초 한 판에 떨어지는 수(기본 6~9개)와 한 사람이 연달아 받는 빈도 |
| 릴레이 MAX가 너무 약하거나 강함 | Relay MAX Damage Multiplier | Crack 요괴 HP/실제 피해 또는 Paint 공격값 기록, MAX까지 넘기는 시간; 무속성 릴레이는 색칠하지 않음 |
| 공동 색칠의 진행 속도·초기 면적을 비교함 | Paint Initial Coverage · Repeat Growth · Growth Speed를 한 번에 하나씩 조정 | 목표는 100% 고정. 같은 지점 반복 명중 뒤 인접 성장, 모든 영역 합계 속도, GOAL 숫자/막대와 Host 결과를 함께 기록 |

이 표의 화살표는 **시험 방향**이지 검증된 추천값이 아니다. 잘못된 투척을 자동 보정하거나 요괴를 추적하는 기능은 현재 없다.

## 10. 이 패널로 조정할 수 없는 것·검증 범위

방의 **목표 인원**은 118개 설정 필드와 별도의 `ROOM PLAYERS` 슬라이더에서 방 생성 전에 선택한다. 좌석의 입장 순서, 네트워크 주소·포트, 생성 추첨 방식(독립 추첨), 오행 속성 확률(균등), 조합·전달의 승인 규칙, 북을 받을 사람의 무작위 배치, 손동작 샘플 창 **0.12초**, 타깃 Cylinder의 실제 크기·위치, 전투 카메라의 FOV와 화면 내 viewport, 박 폭탄의 대상·착지 위치 추첨과 경고·낙하·말풍선 연출 시간, 북 도착의 감속 상한·최대 도착 시간·통과 속도는 118개 입력칸으로 조정하지 않는다. 색칠 승리 목표는 100% 고정이며 이 패널로 낮출 수 없다. DEV 솔로에는 이웃이 없어 화면 간 전달과 아이템 해금을 시험할 수 없다.

기존 PR #76 기준 문서는 해당 코드·Scene·설정 에셋과 당시 `docs/VALIDATION.md`의 증거를 **읽어 확인**해 작성됐다. 그 기록의 EditMode **12/12**, PlayMode **20/20** 및 Mac Development 앱 빌드는 이번 3D 조합판 전환의 통과 증거가 아니다. 이 전환의 Unity 컴파일·EditMode·PlayMode, Mac·iOS 빌드, 실제 조작은 새 실행 기록으로 구분해야 한다. 문서의 물리 용어를 갱신한 것만으로 실행 결과를 PASS로 보지 않는다.

## 11. 코드·Scene 연결 지도

- 설정 필드·범위·일괄 검사: `Assets/Lab/Scripts/Core/LabConfig.cs` — `DeveloperFields`, `CaptureDeveloperValues`, `TryApplyDeveloperValues`.
- 저장 기본값: `Assets/Lab/Resources/LabConfig.asset`.
- 저장된 DEV 버튼·입력칸·슬라이더(39개 행이 저장돼 있고 나머지는 실행 시 마지막 행을 복제해 118개로 늘림)·목표 인원·Host 적용 버튼 연결: `Assets/Lab/Scenes/Lab.unity`의 `LabDeveloperMode` 컴포넌트.
- 패널 열기·DEFAULTS·SOLO/Host 설정 적용·슬라이더 변환: `Assets/Lab/Scripts/UI/LabDeveloperMode.cs`.
- Host 전투 수치·개인 HP·전원 0 패배·스태미나·생성 확률·속성·아이템 잠금·구슬 만료: `Assets/Lab/Scripts/Core/LabModel.cs`.
- 아이템 더블 탭 사용·효과(물약·상자), 릴레이 단계, 폭탄 폭발·피해 배율: `Assets/Lab/Scripts/Core/LabModel.cs`(`TryUseItem`·`TryTransfer`·`TryAdvance`·`ConfirmHit`)와 `LabOrbBoard.EndDragAtWorld`.
- 폭탄 폭발: Host 폭발 기록은 `LabModel.TryAdvance`·`DrainBombExplosions`, 보유자 전달은 `LabNetwork`의 `bombBlast` 패킷, 충격파는 `LabOrbBoard.Blast`, 이펙트는 `LabBombBlastEffect`, 진동은 `LabHaptics.PlayForBomb`, 깜빡임은 `LabOrbItemMarker.SetFuse`.
- 북 멈춤 구간(#178): 추첨은 `LabModel.ArriveDrum`(`TryTransfer`에서 호출), 넘김 이유는 `LabOrbEdgeCrossing.Kind`·`transfer` 패킷 `crossing`, 받는 판의 도착은 `LabOrbBoard.StartArrival`·`Arrive`, 스냅샷 적용은 `LabNetwork.SyncBoard`(`LabOrbState.JustArrived`).
- 박 폭탄(#177): 간격·대상·착지 추첨은 `LabModel.DropGourdBombs`·`GourdBombDelay`·`DrainGourdBombs`, 신호는 `LabNetwork.AnnounceGourdBombs`(`gourdBomb` 패킷)·`GourdBombDropped`, 받는 판의 경고·낙하·튕김은 `LabOrbBoard.ExpectDrop`·`LabDropWarning`·`LabOrbView.PlayDrop`, 박 말풍선은 `LabGourdAlert`, 폭탄 그림·굴림·남은 초는 `LabOrbItemMarker.ShowBomb`, 치지직은 `LabFuseSound`, 떠돌기는 `LabOrbBoard.Drift`.
- 아이템 아이콘·자물쇠·잠금/단계 배지·폭탄 남은 초·해금 연출: `Assets/Lab/Scripts/Orb/LabOrbItemMarker.cs`, 아이콘·자물쇠·속성 재질과 잠금·해금 연출 값: `Assets/Lab/Art/Orbs/LabOrbAppearance.asset`. 해금 연출 값은 DEV 패널이 아닌 Inspector에서 조정한다.
- 3D 조합판 구슬 물리·XY 손동작·공통 구체 외형: `Assets/Lab/Scripts/Orb/LabOrbBoard.cs`와 `LabOrbView.cs`.
- 생성 북: 무작위 배치·북 보유자만 생성·북 위치와 360° 방향 추첨·벽 근처 속도 제한·북 전달은 `LabModel.cs`(`PlaceDrums`·`TryGenerate`·`SpawnVelocityFor`·`TryTransfer`), 탭 생성·끌기·떠돌기·가장자리 되돌림·튀어나오기는 `LabOrbBoard.cs`(`EndDragAtWorld`·`Drift`·`UpsertOrb`), 북 그림·회전은 `LabOrbView.cs`, 북 그림 지정은 `LabOrbAppearance.asset`의 `Drum Sprite`.
- 발사 위치·초기 속도: `Assets/Lab/Scripts/Throw/LabThrowMath.cs`의 `LaunchPosition`·`LaunchVelocity`·기존 `ArcVelocity`. 직구 7개 설정은 `LabConfig` 인덱스 111~117과 `Throw form` DEV 묶음에 연결한다.
- 공의 중력·저항·충돌·수명: `Assets/Lab/Scripts/Throw/LabProjectile.cs`. `Prepare`에서 `EffectiveThrowGravity`와 수명을 캡처하고 같은 물리 루트의 SphereCollider와 표시를 이동한다.
- 요괴 매달림·Y축 회전 및 실제 피격체 이동: `Assets/Lab/Scripts/Throw/LabHangingTarget.cs`; 진단용 재질: `Assets/Lab/Art/Targets/TargetSpinGradient.mat`.
- Host의 방 설정·동기화·발사·피격 확정·결과 로그: `Assets/Lab/Scripts/Session/LabNetwork.cs`와 `LabSessionSettings.cs`.
- 박 모드·고정 목표·성장 설정: `LabConfig.GourdMode`·DEV 인덱스 79, `PaintVictoryPercent`·인덱스 87(100% 고정), `PaintGrowthSettings`·인덱스 105~110, `LabSceneController`의 초기/`ConfigApplied` 연결, `LabGourdPaint.Attach`·`ApplyMode`.
- Paint 성장·승리·표시: `Core/LabPaintSurface`의 `RegisterHit`·`Advance`·`CopyPixels`·`TryReplacePixels`, `LabModel.ConfirmHit`·`TryAdvance`·`PaintSurface`, `LabPaintReplication`의 `LabPaintPublisher`·`LabPaintReceiver`, `LabNetwork.PaintSurface`의 표시 캐시/Snapshot 동시 반영, `LabGourdPaint.Surface`·`PaintTexture`, `LabGourdPaint.shader`. View의 비활성/초기화는 모델을 지우지 않는다.
- 목표 UI: `LabHud.PaintGoalPercentFor`·`FormatPaintGoalProgress`·`PaintGoalFill`; Crack의 HP 단계/마무리는 `LabGourdCrack`·`LabGourdFinish`.
- 카메라·보드 크기 적용: `Assets/Lab/Scripts/UI/LabSeatCamera.cs`와 `LabSceneController.cs`.
- 과거 검증 기록: `docs/VALIDATION.md`. 투척 비교 절차: `docs/THROW_TUNING_GUIDE.md`.
