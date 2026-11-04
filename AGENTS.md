# C6 Physics Lab 작업 안내

이 저장소는 기존 MUSA 프로토타입을 대신할 Unity 실험실 프로젝트다. 기준 Unity 버전은 6000.6.5f1이며 실행 Scene은 `Assets/Lab/Scenes/Lab.unity`다. 이전 버전은 `pre-physics-lab-rebuild-2026-10-10` 태그와 그 이전 Git 이력에서 확인한다.

- `README.md`는 게임 소개만 담는다. 개발 문서 진입점은 `docs/README.md`이며 실행·기능별 동작은 연결된 상세 문서, 실험 범위는 `docs/LAB_SCOPE.md`, 검증 범위는 `docs/VALIDATION.md`에서 확인한다. 과거 버전의 문서를 새 코드의 검증 근거로 사용하지 않는다.
- 기능 구현마다 README에 이슈별 설명·설정값·프로토콜 이력·검사 결과를 덧붙이지 않는다. 상세 변경은 해당 기능 문서와 이슈·PR에, 수행한 검증은 `docs/VALIDATION.md`에 기록한다. README는 게임 소개 자체가 달라질 때만 갱신한다.
- 기본 네트워크 방은 Host가 목표 인원 2~5명(기본 3명)을 정하고, 연결된 각 참가자가 로비에서 P1~P(목표 인원) 중 고유 번호를 선택한 뒤 시작한다. Host도 번호를 자유롭게 선택하며, 전투 자리와 좌우 이웃은 선택된 P번호의 순환 순서다. Development 빌드의 1인 솔로 모드는 별도 물리 조정 경로다.
- Lab 로비의 Figma UI는 기존 2~5인·Host 자유 P번호·Direct IP·Development 솔로를 유지한다. Lab의 새 방 만들기는 생성자 소유 대기 광고와 별도 카드 입장을 유지한다. Hapio의 새 방 만들기는 등록 완료를 기다려 같은 이름·포트로 Host 로비에 자동 입장한다(#315). 미입장 대기 방은 앱/Scene 종료 시 정리한다. 별도 Hapio 게임에서는 공통 GameLobby Prefab과 GameSession에 연결하며 LabLobbyView 자체를 실행하지 않는다.
- 수치 원본은 `Assets/Lab/Resources/LabConfig.asset` 하나다. 실행 중 DEV 패널은 원본이 아닌 복사본을 조정한다. 일반 방에서는 Host가 로비의 조정값을 참가자에게 전파하고 모두 확인한 뒤 시작하며, 솔로에는 로컬 복사본을 적용한다.
- Scene과 `.meta`의 참조를 보존한다. Unity에서 화면과 에셋을 변경한 뒤에는 Scene 연결, 컴파일, 해당 EditMode·PlayMode 검사를 별도로 확인한다.
- 이 저장소는 공개다. 인증 정보, 서명 파일, 개인 경로·기기 식별자, 원시 실행 로그와 빌드 산출물을 커밋하지 않는다.
- 소스 존재, 테스트 코드 존재, 과거 로컬 실행 결과와 이 브랜치에서의 검증 결과를 구분한다. 실행하지 않은 Mac·iOS·실기기 항목은 PASS로 쓰지 않는다.

## 별도 개발용 게임 흐름 (#262)

- 기존 Lab Scene과 빌드 경로는 보존한다. 새 게임 진입점은 `Assets/Hapio/Scenes/Home.unity`이며 Home·Lobby·Battle·StageResult·GameOver를 분리한다. 1~3스테이지 전투는 같은 Battle Scene을 재사용한다.
- 새 게임 코드는 `Assets/Hapio`, namespace `Hapio`다. `GameSession`이 연결·진행 데이터를 유지하고 Scene 객체는 연결·해제한다. 기존 `LabNetwork`의 게임 흐름 opt-in은 기본 OFF다.
- 새 빌드는 `Hapio/Development Build` 메뉴를 사용한다. Lab 첫 Build Settings 항목과 기존 개발용 빌드는 바꾸지 않는다. 상세 책임·실행법·종료 조건은 `docs/GAME_FLOW.md`를 따른다.

- #276 별도 게임의 고정 순서는 구미호→야광귀→지귀다. 공통 폭탄·좌우 전달 막기를 고유 패턴 추첨에 넣거나 끄지 않는다. `LabStageYokai.None`은 기존 Lab 동작을 유지한다.
- 요괴별 기본값·규칙은 `Resources/Hapio/StageCatalog`와 StageDefinition, 실행 중 조정은 GameStageSettings 복사본으로 관리한다. #297 모든 스테이지의 표시 모델은 WhiteGourd 내부의 공통 검은 요괴이며, StageDefinition의 모델 override를 비워 두고 갱신 도구에서 구미호를 다시 연결하지 않는다. 원본 LabConfig를 자동 저장하지 않는다.

- #281 2:2 청홍 땅따먹기는 Hapio의 독자 모드(4명, 한 판)다. 규칙은 `LabTeams`·`LabModel.Team`과 공유 설정196~199에 있고 Lab DEV에서도 4명·솔로로 켤 수 있다. Hapio에서 196 켜기는 방 모드가 정하며 DEV 탭에서 바꾸지 않는다. 전투 화면 요소(점유율 막대·피버·벽·무지개)는 실행 중에 만들고, 로비 팝업·팀 자리 화면은 `Hapio > Upgrade 2v2 Lobby UI`로만 저장한다.
- #293/PR #382의 현재 2:2 디자인 결과는 `박 확인`·`나가기`만 표시하고 NEXT를 숨긴다. `나가기`는 `GameSession.GoHome`으로 연결을 종료하고 타이틀로 돌아간다. #281의 같은 방 로비 복귀는 `NextStage`·`ReturnTerritoryToLobby`와 구형 결과 fallback에 남아 있다. 전투 중 참가자 이탈·로딩 실패의 기존 로비 복귀 및 방장 이탈 정책은 유지한다.
- #278 Home/Lobby는 `Assets/Hapio/Art/UI/GameLobby.prefab`의 한지 로비를 공유한다. GameLobbyView는 UI만 관리하고 GameSession/GamePendingRoomRegistry가 연결·대기 광고를 소유한다. 기존 Lab Scene과 전투/결과 Scene을 로비 적용 목적으로 다시 복사하지 않는다.
- 게임 방은 Game-family TXT/연결 토큰을 사용한다. Lab과 같은 Bonjour 서비스 종류라도 검색 결과를 섞지 않는다. 로비 나가기는 방 목록, 최종 결과·게임 오버 HOME은 타이틀 복귀다.

- #299 고해상도 도색은 기본 ON·1024×512(DEV200/201, 도입 당시 총205개)이며 기존 Host 셀 판정과 분리한다. `LabPaintVisualStamp`의 제한된 기록과 확정 색/P 지도를 같은 도색 트랜잭션으로 복원하고 GPU 이미지를 통신하지 않는다. 전투·관람은 실제 표시 텍스처를 쓰며 판정 해시·100% 승리·마르기·소유권 규칙은 유지한다. 자세한 연결과 한계는 `docs/RUNTIME_HIGH_RESOLUTION_PAINT.md`를 따른다.

- #306 실제 게임 시작은 Scene/설정 → 초기 snapshot·도색·owner-board/표현 준비 → 공통 시각 예약·확정 → 3/2/1 → Playing 순서다. `GameStartHeld`는 사용자 Pause와 구분하며 게임 시계·입력·물리만 고정한다. 도입 당시 게임 토큰은 `C6HapioGame/9`, Lab 단판과 기존 Scene 배치는 유지한다. 세대별 ACK·재예약·시간 제한을 우회해 조작을 먼저 열지 않는다.

- #308/#309 통합: 기존0~201을 유지하고 섬광 흔들기202·203, 매혹 예고204를 사용한다. 공유 설정205개·프로토콜5665·Lab26/Game9를 한 세트로 관리한다. 스토리와2:2의 독립 스태미나/수명을 보존하며 과거 단독 PR의 설정 배열을 혼용하지 않는다. 섬광 해제는 동일 회차와 Host 정지 상태를 검증하고 확인 전 같은 회차만 제한적으로 재시도한다.

- #315: 페인트만 사용한다. DEV79는 고정0 예약 슬롯으로 숨겨 후속 설정 번호/총205개를 유지한다. 구형 Crack/분리 연출/실험 Scene은 삭제했고 재생성하지 않는다. 도입 당시 프로토콜5666·Lab27/Game10이며 현재 통합 규약은 아래 #311/#314 항목을 따른다. 일반 협동의 P번호 순환은 위에서 내려다본 시계방향(내가 아래일 때 다음 P=왼쪽)이고, 팀전은 기존 팀별 링·벽 규칙을 유지한다.
- #311 기믹 묶음은 공용(박 폭탄·넘기기 막기)·고유(구미호 여우불·매혹)로 구분한다. 일반 주기는 공용10초·고유20초이며 스테이지별로 조정한다. 야광귀는 아래 #314 시간표 정책을 같은 실행 경로에 공급한다. DEV205~210을 보존하고 시간표211~224를 더해 통합 당시225개였다. 87·88·152·154·167·168은 79처럼 숨긴 예약 슬롯이다. 당시 규약은5668·Lab29/Game12이며 현재 규약은 아래 #324를 따른다. 판별 기록은 Inspector `Record Gimmick Stats` 기본OFF를 유지한다.
- 페인트 완료는 Host의100% 판정·최종 지도를 확정한 뒤 별도 표현 clock으로1.2초/.2배 연출한다. 전역 Time.timeScale/충돌/기여도를 재실행하지 않는다. Hapio는 전원 준비/공유 예약/완료 확인 후 결과 이동하며12초 한도에서 연출을 건너뛸 수 있다. 결과 지도 미동기화의 중간/최종 처리 차이는 아래 #367을 따른다. Lab은 동일 표현의 로컬 대기를 쓴다.

- #320 도입 당시 결과 UI는 게임 연결 토큰 `C6HapioGame/11`을 사용했고 이후 #311/#314 통합 당시에는 Game12였다. 결과 준비 heartbeat와 관람 알림은 같은 run/stage/revision/설정 세대를 검증한다. 당시 일반 중간 결과는 전원 준비 뒤10초 자동 진행하며 관람 중 누적을 멈췄고, 최종/2:2는 자동 진행하지 않았다. 이10초 정책은 아래 #367에서 제거한다. 당시 Lab27·프로토콜5666·공유 설정205개였으며 현재 규약은 아래 #367을 따른다.

- #311/#314 통합: 실행은 `LabModel.CastGimmicks` 하나, 야광귀만 시간표 정책(120초전투/100초정리, 공용25/40/60/80, 고유예고10Flash/35Combo/60Flash/75Combo)을 공급한다. 일반 주기/확률은 다른 스테이지에 유지하고 야광귀의 미사용 필드는 숨긴다. 기존0~210+시간표211~224=225개, 프로토콜5668/Lab29/Game12. 구형 Timeline 엔진·개별 Bomb/Block 반복 타이머를 복구하지 않는다. DEV79 고정0, #315완료 gate·#320결과·#318로비UI를 보존한다.

- #324 음·양 연속 보정은 기본ON/반대75%/최대3(DEV225~227, 총228개)이다. Host가 플레이어별 실제Raw 생성만 기록하며 2연속 후 soft, 최대에선 강제 반대, 별도salt를 사용한다. 아이템 빈도·0% 극성·실패/중복/과열무생성·피버Combined·OFF기존결과·Pause유지/Begin초기화 경계를 보존한다. 현재 규약은5669/Lab30/Game13이며 이전225개 앱과 혼용하지 않는다. 실기기 검증과 수식 재현·자동 검사는 구분한다.

- #332 지귀 120초는 별도 stage 정책이다. A 예고 10/40/60/80·B 30/50/70/90, 공통 20/40/60/80은 기존 가중 난수를 사용한다. 100초에 무벌점 정리 후 자동 회복 2배를 적용한다. 기존 Lab 수동 물 전용 화재는 유지하고 새 정책과 자동 중복 실행하지 않는다. 실제 박 표면에서 helper 근원 → 그룹 해제 → 통상 칠 처리 1회이며, 대상 P만 새 칠 차단/건조 유지 규칙을 적용한다. 준비 전 geometry는 판정하지 않는다. DEV 228~235를 뒤에 붙여 236개, 프로토콜은 5672/Lab33/Game16다. 소모 감소는 미정이며 실제 샤우팅 음성은 미할당이다. docs/JIGWI_LEVEL_DESIGN_120S.md의 11~12절과 VALIDATION의 새 실행 기록을 따른다.

- #328 QA 반영: 잠금이 풀린 폭탄은 던지지 않고 받은 판 안쪽으로 미끄러져 멈춘 뒤 판이 `use`로 열어 공격 구슬2개(판이 가득 차도2개)가 된다. 여우구슬은 두 번 누르기(`use`)로 하나씩 사라지고 옆 가장자리에서 튕긴다. DEV55(폭탄 피해 배율)·211(야광귀 전투 시간)은 79처럼 숨긴 예약 슬롯이며 총228개를 유지한다. 세 스테이지 모두 공용 전투120초, 스태미나 최대·시작120(6칸, 2:2도 6칸·1.7초). #328 도입 당시 규약은5671/Lab32/Game15였다.

- #332 main 통합: #333/#331의 공통 전투120초·스태미나6칸/120·해금폭탄의공격Combined2개·여우불2탭·개별섬광복구를 보존한다. 퇴역55/211의 저장값을 임의0으로 바꾸지 않는다. 지귀 시간표ON의120초/100초정리/피버 정책과 설정236개를 함께 사용하며 최종 접속 규약은5672/Lab33/Game16이다. 이전5671 앱과 혼용하지 않는다.

- #334 구미호 피버 타임: DEV238(시작 남은 초, 기본20·0끔)·239(자동 충전 배율, 기본2)를 더해 총240개, 구미호 탭에만 보이고 Lab DEV에서는 숨긴다. Host가 `feverTimeAt`을 스냅샷에 싣고 그 시각에 기믹 중지·방해 정리(막기·매혹·여우 구슬·잠긴 박 폭탄)를 하며 충전은 시각에서 잘라 배율을 적용한다. 화면은 `LabFeverTimeHud`(두배 말풍선·문구·가장자리·빨간 시간). 현재 규약은5674/Lab35/Game18이며 5671·5672는 #330·#328·#332·#334가 병합 중 겹쳐 쓴 번호다.


## #341 main 통합 당시 기준 (2026-10-28, 현재 시간표·B 결과는 아래 #388로 대체)

- 위 #332의 박 표면 근원/개인 도색 차단을 지귀 시간표 ON에서 개인 발화 A와 전원 화면 소화 B로 대체한다. A는 개인 회복4배·Raw/Combined 소각5초·실제 Combined 박 명중3회 조기 해제다. 새 A/B는 legacy Overheated를 사용하지 않으며100초 정리/회복2배와 일반 Lab 수동 실험을 보존한다.
- B는 박 앞의 큰 불장막과 움직이는 발화점3개를 분리한다. Water 장막 명중은 물 효과/소비만, 실제 발화점 명중만 소화한다. 자기3점을 모두 꺼야 자기 박 공격이 열리고 개별12초 재점화 때 재차단하며 전원 동시 소화로 종료한다. 미해결12초 뒤 별도 반경.65를 적용한다. 최종 Core gate가 칠·피해·보상을 차단하며, Host owner별 콜라이더와 실제 카메라 투영 `Jigwi/ScreenOverlay` 프리팹을 사용한다. 기존 Scene을 복사/재생성하지 않는다.
- main의0~239(236·237 오색 보상과238·239 구미호 피버 포함)를 보존하고 지귀 A/B240~250을 추가해 #341 통합 당시 **공유 설정251개·프로토콜5677/Lab38/Game21**을 사용했다. 위 과거 항목의 규약/설정 수는 도입 당시 기록이며 현재 호환 기준은 아래 #388을 따른다.230~234는 숨긴 예약값으로 보존한다.
- #334 구미호·#343 야광귀 피버와 #346/#349 팝업북 스토리 자료/미리보기 코드를 보존한다. 이 통합에서 오프닝의 앱 시작 자동 진입은 구현하지 않는다. 기존249개 지귀 브랜치·240개 main 앱과 혼용하지 않는다.
- 구현/생성 에셋/과거 검사 이력은 `docs/JIGWI_SCREEN_PATTERNS.md`, 이번 통합의 실제 수행 여부는 `docs/VALIDATION.md`의2026-10-28 절을 따른다. 과거 통과 건수를 최신 통합의 통과로 재사용하지 않는다.

## #354 앱 시작 오프닝

- #354 도입 당시 Hapio Home은 앱 실행 시 기존24컷·26페이지를 자동 시작했다. 현재 진입과 종료 화면은 아래 #359 규칙을 따른다. 같은 실행의 Home 복귀에는 반복하지 않는다. 오프닝은 로컬 표현이며 GameSession·네트워크·Lab 진입을 변경하지 않는다.
- `GameOpening.prefab`과 컨트롤러는 Home의 기존 메뉴 밖에 둔다. 컷/대본/StoryPreview를 재생성하지 말고 `docs/APP_START_OPENING.md`를 따른다. 기존 Home 메뉴 테스트는 오프닝을 실제 Skip한 뒤 기존 흐름을 검사하며 cold-launch 자체는 전용 테스트가 확인한다.

## #359/#360 타이틀 오프닝과 내 P 표시

- Home은 타이틀을 먼저 표시한다. 첫 게임 시작 → 현재25컷·27페이지(#364의19-1 확대 컷 포함) → 완료/스킵 → 방 목록이며, 같은 실행의 후속 게임 시작은 바로 방 목록이다. 시작/종료 포인터 해제 대기와 #356의 수동 터치 진행을 보존한다. DEV·SOLO는 기존 타이틀에서 가능하며, 비활성화/Scene 이탈은 방 검색을 시작하지 않는다.
- 내 P 표시와 자식 도색 패턴·2:2 팀 고리는 기존 LabOrbHudComposition의 구슬 합성 이미지 아래에 둔다. 기존 Image/RectTransform/번호 갱신·카메라/텍스처를 재사용하고 좌우 이웃·미니맵·로비 P와 입력/물리 규칙은 유지한다. Scene이나 UI 전체를 재생성하지 않는다.

## #367 결과 수동 진행과 완료 대기

- 일반 중간 결과는 전원 결과 수신 ACK 후 누구도 박을 관람하지 않을 때 Host가 다음 스테이지 버튼으로 진행한다. 별도 Ready 입력과10초 자동 진행은 없다. OnEnable에서 결과를 초기화하며 최소 다음 프레임·모든 마우스/터치 해제 전에는 NEXT/HOME/관람 버튼과 콜백을 차단한다. 이후 새 입력을 받으며 고정 초 대기는 없다. Hapio Battle에는 독립 Lab 결과 카드·대시보드를 표시하지 않으며 공유 통계·관람, 독립 Lab 단판과2:2 결과·기존 로비 복귀 API는 유지한다. 현재 2:2 디자인 결과의 버튼 정책은 위 #293/PR #382 항목을 따른다.
- 완료 연출1.2초/.2배는 유지한다. 예약을 포함한 prepare에 최종 지도 준비·예약 수신을 한 ACK로 확인하고, Host 최초 Victory의12초 서버 deadline을 prepare/상태로 공유한다. 연출 중 확보한 결과·관람 캐시를 재사용하며 Story 결과에서 추가30초를 기다리지 않는다. 솔로의 예약 여유0.75초만 생략하고 멀티는 유지한다. 한도에서 연출을 생략해도 최종 지도 불일치가 남은 중간 StageResult는 GameOver가 되며, 최종 Completed만 클리어를 보존하고 지도 미동기화 안내를 표시한다. CPU 지도가 일치해도 GPU 관람 사본 생성 실패 시 별도 오류 안내 없이 관람이 비활성화될 수 있는 기존 제약은 남아 있다.
- #367 도입 당시 게임 연결 토큰만 **`C6HapioGame/22`**로 올렸다. 당시 **프로토콜5677·`C6PhysicsLab/38`·공유 설정251개**와 기존 인덱스를 유지했으며 현재 호환 기준은 아래 #388을 따른다. 구현 설명은 `docs/GAME_FLOW.md`·`docs/STAGE_RESULT_UI.md`·`docs/SESSION_FINISH_UI.md`, 실제 수행 여부는 `docs/VALIDATION.md`에서 구분한다.

## #369 수채 질감과 로컬 번짐

- 승인된 파란 시안은 `LabGourdPaint`에서 기존 고해상도 결과 위에 적용한다. `Resources/GourdPaint/WatercolorProfile`은 표현 전용이며 공유 LabConfig/프로토콜에 넣지 않는다. 속성별 정적/활성 농도를 분리하고 새로 이어진 기록만 기본.4초 번지며 완료 시 한 번만 확정한다. 초기/교체/128개 폐기·설정/품질 변경은 정적 복원하며 관람/PNG는 최종 표시를 확정·복사한다. 원본 패턴·Prefab·Scene·성장/건조/100% 판정·P지도·Host 권한은 유지한다.
- `C6 Lab > Experiments > Watercolor Paint`는 같은 런타임의 격리 정적 비교다. 비교 복사본의 Drying OFF를 실제 규칙 변경으로 해석하지 않는다. 실기기 성능과 자동 GPU 검사는 구분하고 `docs/WATERCOLOR_PAINT_EXPERIMENT.md`·VALIDATION을 따른다.

## #402 보류된 요괴 확장 준비 정리

- 현재 요괴 실행 자료는 `Resources/Hapio/StageCatalog`와 기존 구미호→야광귀→지귀의 StageDefinition 3개다. 기존 단계 순서·GUID·규칙·수치·모델 연결을 유지하며, 기존3종은 공통 검은 요괴를 계속 사용한다.
- 10종 확장은 보류되어 #381의 제작용 카탈로그·정의 에셋·전용 코드·생성/검증 도구와 StageDefinition의 선택적 `content` 연결을 제거했다. #393의 공통 모듈 경로는 현재 게임에 추가하지 않는다. 확장을 재개할 때는 PR #383과 Git 이력의 자료를 확인하고 새 작업 범위를 정하며, 제거한 준비 파일을 자동 재생성하지 않는다.


## #388 지귀 A/B 시간 제한·성공 보상·실패 정리

- 현재 지귀 시간표 ON의 A 실제 활성은5/40/70초, 최대15초(DEV240)다. 활성 직전1초 예고, 개인 회복4배·구슬 소각5초·유효 Combined 박 명중3회 조기 종료를 유지한다. B는25/55/85초에10초 미션이며54/84초의 다음 경고는 앞 A와 겹칠 수 있어도 실제 활성은 겹치지 않는다. 같은 경계에서 앞 A를 먼저 종료하며 #341의 슬롯 Skipped 정책은 사용하지 않는다.
- B 전원 동시 소화 성공은 즉시5초 보상이다. 그 시간에 발사 승인된 구슬만 불꽃 잔상·기본 크기1.3배(DEV251)·별도 칠 면적1.5배(DEV252)를 캡처한다. 실제 속성은 유지하고 기존 비행에는 소급하지 않으며 보상 종료 후에도 이미 발사한 공의 캡처는 유지한다. 시각 크기를 칠 면적에 다시 곱하지 않고 기존 작은 공/콤보/오색 및 share 상한3을 보존한다.
- B 미해결10초는 한 번의 불/번쩍임·실패 표현과 함께 모든 참가자의 Raw/Combined·아이템·비행 중 구슬까지 제거하고 스태미나0, 북 보존이다. 폭발·명중/빗나감·피해·도색·보상을 만들지 않으며 자동 회복은 이어진다. 취소·승패·100초 정리는 실패 벌점을 만들지 않는다.
- 구형 B 축소246/247은 숨긴 예약값으로 저장/인덱스만 보존한다.245 기본 재점화12초는 미션10초보다 길어 기본에서는 재점화하지 않으며 DEV로 줄일 때만 미션 안에서 동작한다. 예고는 현행 A/B에서1초 고정이며229는 숨긴 미사용 예약 슬롯으로 저장값만 보존한다. 공통20/40/60/80·100초 방해 정리/회복2배·120초 전투와 일반 Lab 수동 정책은 유지한다.
- 현재 호환 묶음은 **설정253개·프로토콜5678·`C6PhysicsLab/39`·`C6HapioGame/23`**다. 기존0~250을 보존하고251/252를 추가한다. 실패는 session/round별 scalar `clearedThroughRevision` fence로 전파·catch-up하며 제거 ID 배열을 싣지 않는다. 이전 승인/receipt/투사체 부활은 막고 fence 이후 승인·회복·북은 보존한다.
- 현재 동작/코드 연결/기대 검증은 `docs/JIGWI_SCREEN_PATTERNS.md`·`docs/GAME_FLOW.md`·`docs/DEVELOPER_MODE_PARAMETER_SPEC.md`를 따르고, 실행한 검사·미실행 실기기는 `docs/VALIDATION.md`에만 실제 근거로 구분한다. 위 #332/#341의 시간표·축소·규약·검증 횟수는 당시 이력이다.


## #412 지귀 회복 표시와 소화 Water 공급

- 지귀 A는 기존 규칙을 유지하고 실제 스태미나를 따르는 화염 테두리/배율을 표시한다. 공통 소비 표시의 입력은 중복 제거된 승인 `LocalStaminaSpent`이며 snapshot 차이나 구슬 Spawn으로 추측하지 않는다. 원본 Canvas 배치·색·구슬 뒤 위계·야광귀 피버는 보존한다.
- B 일반 조합은 평소 원소다. Host가 활성 즉시1개·2초마다1개, 최초 지급 대상별 미소모 최대5개의 소화용 Water를 공급한다(전달/비행 포함 상한). `jigwiABSupplyEventId`로 일반 Water/구형 Lab 공급과 구분하고 종료 후 선택적 폐기를 유지한다. 실패는 기존 전원 전체제거/스태미나0/북 보존이다. 일반 투척 물리는 바꾸지 않는다.
- 기존0~252는 유지하며 공급 간격/초기 수량/상한/발화점 거리253~256을 더한 **257개·5680/Lab41/Game25**가 #412 당시 묶음이며 현재 기준은 아래 #417을 따른다. 위 과거 규약 기록은 도입 당시 값이며 이전 앱과 혼용하지 않는다. `JIGWI_SCREEN_PATTERNS.md`·`DEVELOPER_MODE_PARAMETER_SPEC.md`를 따르고 수행한 검사만 VALIDATION에 기록한다.


## #417 3:3 땅따먹기 6인 시작

- 조합판 프레임·행동 결과의 플레이어 상한 `LabBoardReplica.MaximumPlayers`는 모든 방 인원의 패킷 상한인 `LabModel.TerritoryMaximumParticipants`(6)다. 기본 모드 2~5인 제한은 `LabModel.ValidRoomSize`가 지키며 이 상한으로 대신하지 않는다. 지귀 상태 검사는 기본 모드 전용이라 `LabModel.MaximumParticipants`(5)를 유지한다. Guest 도색 수신(`LabPaintReceiver`)의 칠한 사람 번호 상한도 `LabPaintSurface.MaxPainterSlot`(6)이다. P6 칠을 5로 거절하면 체크포인트 재요청이 끝나지 않아 Guest 화면이 멈춘다.
- 공유 설정 257개·인덱스는 그대로이며 #417 당시 묶음은 **5681/Lab42/Game26**이었다(현재 기준은 아래 #428). 6명을 거절하던 5680 앱과 혼용하지 않는다. 실행한 검사만 `docs/VALIDATION.md`에 기록하고 6대 실기기 시작은 별도로 확인한다.


## #428 통신 패킷 봉투와 조합판 프레임 전송

- `LabNetworkPacket`에 메시지 본문을 타입 필드로 두지 않는다. JsonUtility는 null인 클래스 필드를 기본값 객체로 써서 모든 패킷이 그 타입의 사본을 실어 가므로, 행동 결과(`LabActionReceipt`)와 도색 프레임(`LabPaintFrame`)은 `payload` JSON 문자열로만 보내고 `LabPacketPayload.Encode/Decode`로 다룬다. `LabBulkPayload`도 같다. 값을 넣는 곳이 없던 `snapshot`·`boardFrame` 필드는 지웠다. 본문 없는 패킷은 약 1 KB다(측정 937 B, 검사 상한 1,100 B; 이전 4,189 B).
- 승인된 행동 직후 `HandleBoardAction`이 보내는 조합판 프레임(`PublishOwnerBoards(..., skipEmpty: true)`)만 변경·제거 구슬이 없고 체크포인트가 아닌 수신자를 건너뛴다. 건너뛴 프레임은 순번을 소비하지 않는다(`PeekBoardSequence`/`CommitBoardSequence`). `PublishSnapshot`이 보내는 프레임(100 ms 타이머, 명중·시작·일시정지/재개·섬광 등 즉시 발행)과 5초 체크포인트는 그대로 전원에게 간다. 재개 스냅샷은 정지 중 헤더와 같은 serverTime을 가지므로 헤더 프레임을 생략하면 Guest가 다음 타이머 프레임까지 정지 상태로 남는다.
- 공유 설정 257개·인덱스는 그대로이며 #428 당시 묶음은 **5682/Lab43/Game27**이었다(현재 기준은 아래 #461). 이전 봉투 형식의 앱과 혼용하지 않는다. 실행한 검사와 패킷 크기 측정만 `docs/VALIDATION.md`에 기록한다.


## #431 iOS 목표 프레임레이트

- `LabMobilePresentationPolicy.InitializePlayer`가 iPhone 플레이어에서만 `Application.targetFrameRate = 60`을 적용한다(`TargetFrameRateFor`). Editor·Mac은 바꾸지 않으며, `QualitySettings` Mobile 레벨의 vSync 0을 유지해야 값이 적용된다. ProMotion 120은 고해상도 도색 재구성 비용 때문에 쓰지 않는다.

## #433 Guest 박 포즈 보정

- Guest의 박 포즈 보정은 `LabNetwork.LocalState.ReconcileTargetPose` 하나로 간다(조합판 프레임·Bulk 스냅샷 공용). 같은 세션/라운드에서 `serverTime`이 마지막 적용 시각 이하인 비정지 샘플은 건너뛰고, 정지 샘플은 항상 적용한다. 스냅샷 경로에서 `ReconcileYaw/ReconcileRope`를 따로 다시 부르지 않는다.
- `HoldTargetPoseForPredictedImpact`/`ReleaseTargetPoseHold`와 `ReconcileRope(..., holdImpulse)`는 투척 로컬 선반영이 쓸 훅이다. 보류 중 비정지 샘플은 로프 위상만 블렌드하고 오프셋·속도·yaw는 로컬 물리에 맡긴다.

## #435 조합판 체크포인트 위상 분산

- Host의 5초 조합판 체크포인트는 수신자마다 위상(`5·i/N`, 로컬 제외 피어 순번)을 두어 같은 프레임에 몰리지 않게 한다. 위상은 라운드 첫 프레임에서만 정하며 체크포인트 기준 시각은 격자에 맞춰 기록하므로 강제 체크포인트(Guest `board_checkpoint`)와 늦은 틱이 위상을 바꾸지 않는다. 주기 5초(`CheckpointIntervalSeconds`)와 프레임 형식·규약은 바꾸지 않는다.
- Host는 Guest의 `board_checkpoint`·`paint_resync` 요청을 피어당 0.5초에 한 번만 처리한다(`LabPeerRequestThrottle`, 처리한 요청만 기록). 실행한 검사만 `docs/VALIDATION.md`에 기록한다.

## #437 Bulk 재전송과 백로그

- `LabBulkSender`의 재전송 대기는 RTT 측정값(RFC 6298, 0.08~1초)이며, 측정은 덮은 조각을 모두 한 번만 보낸 ACK에서만 한다(Karn). 시간 초과 재전송은 공유 백오프를 늘리고 다음 유효 측정에서 되돌린다. 같은 누적 ACK가 세 번 더 오면 선두 조각을 바로 다시 보내되, 이미 재전송한 사본이 만드는 반복 ACK는 빼고 센다. Guest는 매 데이터그램마다 누적 ACK를 보내야 이 동작이 성립한다.
- Host는 피어 백로그가 `BulkBacklogLimitBytes`(64 KiB) 이상일 때만 그 피어의 스냅샷·도색 발행을 건너뛴다. 미확인 조각이 있다는 이유만으로 건너뛰지 않는다. `paint_resync`는 백로그가 가득 찼거나 직전 전체 도색 지도가 미확인일 때만 거절하고, Guest는 받는 중인 도색 프레임이 1초 동안 새 조각을 못 받았을 때만 재요청한다. 실행한 검사만 `docs/VALIDATION.md`에 기록한다.

## #439 Host 도색 발행 캡처 공유

- Host의 피어별 `LabPaintPublisher`는 하나의 `LabPaintPublishCache`를 공유한다. 한 번의 발행(같은 epoch)에서 표면 복사·해시·인코딩은 한 번이며 피어별 발행자는 자기가 마지막으로 보낸 캡처를 기준으로 delta를 만든다. 캡처 키는 표면·epoch·Revision·PaintCount·VisualVersion·크기이므로 픽셀을 바꾸는 표면 경로는 계속 Revision을 올려야 한다. 프레임 바이트는 이전 구현과 같아야 하며 `LabPaintPublishCacheTests`의 main 참조 비교로 고정한다.

## #444 도색 복제본 설치 API

- Guest 도색 복제본은 `TryReplacePixels`(복사), `TryAdoptReplica`(호출자 배열 소유권 이전, 같은 크기·같은 기준 리비전의 복제본이면 바뀐 셀만 검사), `TryCopyReplicaFrom`(같은 크기면 기존 배열에 덮어씀)로 설치한다. 셋은 같은 셀 규칙과 `InstallReplica` 상태를 공유하며 실패 시 표면을 바꾸지 않는다. `TryAdoptReplica`에 넘긴 배열은 이후 호출자가 건드리지 않고, 바뀐 셀 목록 밖의 셀은 현재 지도와 같아야 한다.

## #449 Guest 도색 수신 제자리 적용

- `LabPaintReceiver`는 재사용 버퍼(해독 payload, 이중 버퍼 지도, 바뀐 셀 목록, SHA 청크)로 트랜잭션을 만들고 `TryAdoptReplica`로 설치한다. 셀 규칙·칠해진 셀 수 검사는 표면이 하며, 수신 결과(Applied/Rejected/NeedCheckpoint)는 이전 구현과 같아야 한다. 조각별 base64 해독은 마지막을 뺀 조각이 패딩·공백 없는 순수 base64일 때만 쓴다. `visualBytes == -1`의 스탬프 해시는 `VisualVersion`으로 캐시하므로 스탬프를 바꾸는 표면 경로는 계속 `VisualVersion`을 올려야 한다. `CopyPaintToDisplay`는 `TryCopyReplicaFrom`을 쓴다.

## #451 Guest 공유 연출 시계

- Guest 연출 시간은 `LabNetwork.BattlePresentationTime`(`LabGuestBattleClock`)이다. 스냅샷 객체가 아니라 세션·라운드·`serverTime`·정지 값으로 기준을 잡는다. 같은 라운드 안에서 0.5초 미만으로는 뒤로 가지 않고, 외삽은 최대 1초다. 1초 정체·정지·시작 대기는 `BattlePresentationHeld`로 음성·애니메이션이 멈춘다. 판정이나 Host로 보내는 값에 이 시계를 쓰지 않는다. 지귀 화면은 0.075초(`RewindToleranceSeconds`) 넘게 되감길 때만 새 스테이지로 본다.

## #463 HUD·효과음의 공유 연출 시계

- 전투 HUD·효과음(`LabGourdComboHud`·`LabGourdYokai`·`LabCharmHud`·`LabCharmBubbles`·`LabFeverTimeHud`·`LabYaguangFlashHud`·`LabBattleBgm`·`LabGumihoSound`·`LabYaguangSound`·`LabJigwiHud`)은 Host 시각을 따로 어림하지 않고 `network.BattlePresentationTime`을 읽는다. 새 연출 컴포넌트도 `serverTime + 실제 경과` 같은 자체 어림을 두지 않는다. 테스트용 `Present(snapshot, now)` 진입점은 그대로 둔다.

## #454 매달린 박 렌더 보간

- `LabHangingTarget`의 보간 자세는 URP 렌더 콜백(`beginContextRendering`~`endContextRendering`) 사이에서만 Transform에 있다. 그 밖의 모든 스크립트·물리·네트워크 처리는 고정 스텝 자세를 읽으므로 명중·도색 판정에 보간 자세를 쓰지 않는다. 박에 붙여 화면 위치를 계산하는 표시 전용 UI는 `LabHangingTarget.DrawnPoint`로 그려지는 위치를 얻는다. 판정과 같은 계산을 쓰는 지귀 B 불장막 틀은 고정 자세 그대로다.

## #457 Guest 북 탭 선반영

- Guest 북 탭의 `SpawnStarted`(소리·진동)는 `LabGenerationPrediction.Certain`이 참일 때만 승인 전에 올리고, 그 구슬은 크레딧으로 다시 울리지 않는다. 대기 수는 `localActions`에서 아직 답이 없는 요청만 센다(`actionRequests`는 라운드 동안 남는다). 보관 한도 판정에는 여우불·오색구슬 보상·양쪽 이웃·아이템 열기의 도착 여유를 둔다. 생성 판정·Host 규칙을 바꾸면 이 조건도 함께 맞춘다.

## #459 Guest 내 투척 접촉 즉시 표현

- Guest는 자기 투척의 시각 복제본 접촉(`LabProjectile.LaunchVisual`의 contact 콜백)으로 박 킥·명중 효과·타격음·진동 또는 빗나감 소리를 Host 판정 전에 한 번 낸다(`LabLocalHitPresentation`). Host `targetImpact`·`targetMiss`는 그 표현을 감쇠까지 고려해 보정하거나 되돌리며(`CorrectPredictedImpact`/`UndoPredictedImpact`), 판정이 없으면 전투 연출 시계로 시간이 지나면 되돌린다. 도색·피해·오색·막힘은 항상 Host 결과이고, 지귀 B 불장막·스테이지 불·legacy 불·공급 구슬 중에는 선반영하지 않는다. Host가 판정 패킷을 보내는 경우를 바꾸면 이 경로도 함께 확인한다.

## #461 도색 페이로드 압축 — 최신 호환 기준

- `LabPaintFrame.encoding`은 0 원본, 1 deflate다. Host의 피어별 발행자는 `LabPaintEncodingPolicy.Auto`(압축이 더 짧을 때만 1)를 쓰고, 기본 생성자는 `RawOnly`다. 해시·covered·`Revision`은 늘 풀어 놓은 지도 기준이다. 수신기는 헤더에서 정한 크기 상한(full `columns·rows·5 + 시각 바이트`, delta `columns·rows·7 + 시각 바이트`) 안에서만 풀고, 넘거나 깨지면 체크포인트를 요청한다. 빈 256×128 지도는 도색 프레임 1개(Bulk 2조각)다.
- 공유 설정 257개·인덱스는 그대로이며 최신 묶음은 **5683/Lab44/Game28**이다. 이전 5682 앱과 혼용하지 않으며, 병합 뒤에는 모든 기기가 새 빌드를 써야 한다. 실행한 검사만 `docs/VALIDATION.md`에 기록한다.

## #471 로비 연결 끊김 뒤 핸들러 재등록

- `LabNetwork`는 패킷 핸들러를 등록한 `CustomMessagingManager` 인스턴스를 기억하고, NGO가 새 세션에서 새 인스턴스를 만들면 다시 등록한다. `Stop`을 거치지 않고 끝난 세션(방장 이탈·화면 잠김·Wi-Fi 끊김)이 다음 방의 패킷 수신을 막지 않아야 한다. 게임 흐름 게스트 끊김도 핸들러를 풀고 `C6_LAB_DISCONNECTED`를 남긴다.
- Hapio Lobby에서 연결이 끊기면 `GameSession`이 나가기와 같은 `GoHome`(`network.Stop` 포함)으로 방 목록에 돌아가고 끊김 안내를 표시한다. 게스트가 Host 로비를 받기 전(`LobbyInfoPending`)에는 지난 방 인원·자리 대신 "방 정보를 받고 있소..."를 표시하고, 고를 수 없는 빈 자리는 흐리게 그린다. 자리 선택 규칙·접속 규약·설정 수·Scene/Prefab은 바꾸지 않는다.
