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

## 합이오 UI 컬러 시스템

- 앞으로 Hapio UI를 새로 만들거나 수정할 때 색상 기준은 `Assets/Hapio/Art/UI/Source/Hapio.tokens.json`이다. 전체 팔레트와 사용 안내는 `docs/COLOR_SYSTEM.md`에서 확인한다.
- Figma 변수 ID·토큰 경로와 UI 역할을 맞춰 기존 토큰을 적용한다. 임의의 유사 HEX를 새로 만들지 않고, 코드·Prefab에 적용한 색의 토큰 경로를 추적할 수 있게 남긴다. 별도 색상이 명시된 요청은 그 요청을 따른다.
- 색과 투명도는 원본의 `$value.components`·`colorSpace`·`alpha`를 함께 따른다. 이름에 붙은 `%`로 alpha를 추측하지 않는다. 특히 `DarkIvory/500***70%`의 실제 alpha는 0.5다.
- 이 규칙은 앞으로 작업하는 UI에 적용한다. UI 작업을 이유로 게임 판정용 팔레트·네트워크 설정·기존 화면 전체를 일괄 치환하지 않는다.

## 별도 개발용 게임 흐름 (#262)

- 기존 Lab Scene과 빌드 경로는 보존한다. 새 게임 진입점은 `Assets/Hapio/Scenes/Home.unity`이며 Home·Lobby·Battle·StageResult·GameOver를 분리한다. 1~3스테이지 전투는 같은 Battle Scene을 재사용한다.
- 새 게임 코드는 `Assets/Hapio`, namespace `Hapio`다. `GameSession`이 연결·진행 데이터를 유지하고 Scene 객체는 연결·해제한다. 기존 `LabNetwork`의 게임 흐름 opt-in은 기본 OFF다.
- 새 빌드는 `Hapio/Development Build` 메뉴를 사용한다. Lab 첫 Build Settings 항목과 기존 개발용 빌드는 바꾸지 않는다. 상세 책임·실행법·종료 조건은 `docs/GAME_FLOW.md`를 따른다.

- #276 별도 게임의 고정 순서는 구미호→야광귀→지귀다. 공통 폭탄·좌우 전달 막기를 고유 패턴 추첨에 넣거나 끄지 않는다. `LabStageYokai.None`은 기존 Lab 동작을 유지한다.
- 요괴별 기본값·규칙은 `Resources/Hapio/StageCatalog`와 StageDefinition, 실행 중 조정은 GameStageSettings 복사본으로 관리한다. #297 당시 모든 스테이지의 표시 모델은 WhiteGourd 내부의 공통 검은 요괴였고, 현재는 아래 #507처럼 세 스테이지가 각자 구운 박(구미호·야광귀·지귀)을 쓴다. StageDefinition의 `yokaiVisual` override를 비워 두고 갱신 도구에서 구미호를 다시 연결하지 않는다. 원본 LabConfig를 자동 저장하지 않는다.

- #281 2:2 청홍 땅따먹기는 Hapio의 독자 모드(4명, 한 판)다. 규칙은 `LabTeams`·`LabModel.Team`과 공유 설정196~199에 있고 Lab DEV에서도 4명·솔로로 켤 수 있다. Hapio에서 196 켜기는 방 모드가 정하며 DEV 탭에서 바꾸지 않는다. 전투 화면 요소(점유율 막대·피버·벽·무지개)는 실행 중에 만들고, 로비 팝업·팀 자리 화면은 `Hapio > Upgrade 2v2 Lobby UI`로만 저장한다.
- #700 땅따먹기 디자인 결과는 `봉인 박 둘러보기`·`다시 도전`·`나가기`를 표시한다. 방장이 다시 도전하면 같은 방 로비로 돌아가며 DEV 솔로는 새 판을 시작한다. #603의 불이/금옥이 GameOver 화면은 정상 Story 패배(실패 이야기·재도전)가 아닌 모든 GameOver에 쓴다(이탈·연결 끊김·준비·로딩·동기화 실패). `나가기`는 `GameSession.GoHome`으로 연결을 종료하고 타이틀로 돌아간다. #281의 같은 방 로비 복귀는 `NextStage`·`ReturnTerritoryToLobby`와 구형 결과 fallback에 남아 있다. 전투 중 참가자 이탈·로딩 실패의 기존 로비 복귀 및 방장 이탈 정책은 유지한다.
- #278 Home/Lobby는 `Assets/Hapio/Art/UI/GameLobby.prefab`의 한지 로비를 공유한다. GameLobbyView는 UI만 관리하고 GameSession/GamePendingRoomRegistry가 연결·대기 광고를 소유한다. 기존 Lab Scene과 전투/결과 Scene을 로비 적용 목적으로 다시 복사하지 않는다.
- 게임 방은 Game-family TXT/연결 토큰을 사용한다. Lab과 같은 Bonjour 서비스 종류라도 검색 결과를 섞지 않는다. 로비 나가기는 방 목록, 최종 결과·게임 오버 HOME은 타이틀 복귀다.

- #299 고해상도 도색은 기본 ON·1024×512(DEV200/201, 도입 당시 총205개)이며 기존 Host 셀 판정과 분리한다. `LabPaintVisualStamp`의 제한된 기록과 확정 색/P 지도를 같은 도색 트랜잭션으로 복원하고 GPU 이미지를 통신하지 않는다. 전투·관람은 실제 표시 텍스처를 쓰며 판정 해시·100% 승리·마르기·소유권 규칙은 유지한다. 자세한 연결과 한계는 `docs/RUNTIME_HIGH_RESOLUTION_PAINT.md`를 따른다.

- #306 실제 게임 시작은 Scene/설정 → 초기 snapshot·도색·owner-board/표현 준비 → 공통 시각 예약·확정 → 3/2/1 → Playing 순서다. `GameStartHeld`는 사용자 Pause와 구분하며 게임 시계·입력·물리만 고정한다. 도입 당시 게임 토큰은 `C6HapioGame/9`, Lab 단판과 기존 Scene 배치는 유지한다. 세대별 ACK·재예약·시간 제한을 우회해 조작을 먼저 열지 않는다.

- #308/#309 통합: 기존0~201을 유지하고 섬광 흔들기202·203, 매혹 예고204를 사용한다. 공유 설정205개·프로토콜5665·Lab26/Game9를 한 세트로 관리한다. 스토리와2:2의 독립 스태미나/수명을 보존하며 과거 단독 PR의 설정 배열을 혼용하지 않는다. 섬광 해제는 동일 회차와 Host 정지 상태를 검증하고 확인 전 같은 회차만 제한적으로 재시도한다.

- #315: 페인트만 사용한다. DEV79는 고정0 예약 슬롯으로 숨겨 후속 설정 번호/총205개를 유지한다. 구형 Crack/분리 연출/실험 Scene은 삭제했고 재생성하지 않는다. 도입 당시 프로토콜5666·Lab27/Game10이며 현재 통합 규약은 아래 #311/#314 항목을 따른다. 일반 협동의 P번호 순환은 위에서 내려다본 시계방향(내가 아래일 때 다음 P=왼쪽)이고, 팀전은 기존 팀별 링·벽 규칙을 유지한다.
- #311 기믹 묶음은 공용(박 폭탄·넘기기 막기)·고유(구미호 여우불·매혹)로 구분한다. 일반 주기는 공용10초·고유20초이며 스테이지별로 조정한다. 야광귀는 아래 #314 시간표 정책을 같은 실행 경로에 공급한다. DEV205~210을 보존하고 시간표211~224를 더해 통합 당시225개였다. 87·88·152·154·167·168은 79처럼 숨긴 예약 슬롯이다. 당시 규약은5668·Lab29/Game12이며 현재 규약은 아래 #324를 따른다. 판별 기록은 Inspector `Record Gimmick Stats` 기본OFF를 유지한다.
- 페인트 완료는 Host의100% 판정·최종 지도를 확정한 뒤 별도 표현 clock으로1.2초/.2배 연출한다(스토리 1~3은 아래 마지막 일격 다시보기로 대체, 2:2·Lab은 이 1.2초 유지). 전역 Time.timeScale/충돌/기여도를 재실행하지 않는다. Hapio는 전원 준비/공유 예약/완료 확인 후 결과 이동하며12초 한도에서 연출을 건너뛸 수 있다. 결과 지도 미동기화의 중간/최종 처리 차이는 아래 #367을 따른다. Lab은 동일 표현의 로컬 대기를 쓴다.

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
- 2:2·Lab과 다시보기 대체 경로의 완료 연출1.2초/.2배는 유지한다(스토리 1~3 기본은 아래 마지막 일격 다시보기). 예약을 포함한 prepare에 최종 지도 준비·예약 수신을 한 ACK로 확인하고, Host 최초 Victory의12초 서버 deadline을 prepare/상태로 공유한다. 연출 중 확보한 결과·관람 캐시를 재사용하며 Story 결과에서 추가30초를 기다리지 않는다. 솔로의 예약 여유0.75초만 생략하고 멀티는 유지한다. 한도에서 연출을 생략해도 최종 지도 불일치가 남은 중간 StageResult는 GameOver가 되며, 최종 Completed만 클리어를 보존하고 지도 미동기화 안내를 표시한다. CPU 지도가 일치해도 GPU 관람 사본 생성 실패 시 별도 오류 안내 없이 관람이 비활성화될 수 있는 기존 제약은 남아 있다.
- #367 도입 당시 게임 연결 토큰만 **`C6HapioGame/22`**로 올렸다. 당시 **프로토콜5677·`C6PhysicsLab/38`·공유 설정251개**와 기존 인덱스를 유지했으며 현재 호환 기준은 아래 #388을 따른다. 구현 설명은 `docs/GAME_FLOW.md`·`docs/STAGE_RESULT_UI.md`·`docs/SESSION_FINISH_UI.md`, 실제 수행 여부는 `docs/VALIDATION.md`에서 구분한다.

## #369 수채 질감과 로컬 번짐

- 승인된 파란 시안은 `LabGourdPaint`에서 기존 고해상도 결과 위에 적용한다. `Resources/GourdPaint/WatercolorProfile`은 표현 전용이며 공유 LabConfig/프로토콜에 넣지 않는다. 속성별 정적/활성 농도를 분리하고 새로 이어진 기록만 기본.4초 번지며 완료 시 한 번만 확정한다. 초기/교체/128개 폐기·설정/품질 변경은 정적 복원하며 관람/PNG는 최종 표시를 확정·복사한다. 원본 패턴·Prefab·Scene·성장/건조/100% 판정·P지도·Host 권한은 유지한다.
- `C6 Lab > Experiments > Watercolor Paint`는 같은 런타임의 격리 정적 비교다. 비교 복사본의 Drying OFF를 실제 규칙 변경으로 해석하지 않는다. 실기기 성능과 자동 GPU 검사는 구분하고 `docs/WATERCOLOR_PAINT_EXPERIMENT.md`·VALIDATION을 따른다.

## #402 보류된 요괴 확장 준비 정리

- 현재 요괴 실행 자료는 `Resources/Hapio/StageCatalog`와 기존 구미호→야광귀→지귀의 StageDefinition 3개다. 기존 단계 순서·GUID·규칙·수치·모델 연결을 유지하며, 세 스테이지의 박 외형은 #507의 구운 스테이지 박이다.
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

- `LabBulkSender`의 재전송 대기는 RTT 측정값(RFC 6298, 0.08~1초)이며, 측정은 덮은 조각을 모두 한 번만 보낸 ACK에서만 한다(Karn). 시간 초과 재전송은 공유 백오프를 늘리고 다음 유효 측정에서 되돌린다. 같은 누적 ACK가 세 번 더 오면 선두 조각을 바로 다시 보내되, 이미 재전송한 사본이 만드는 반복 ACK는 빼고 센다. Guest는 매 데이터그램마다 누적 ACK를 보내야 이 동작이 성립한다. 중복 ACK 빠른 재전송과 표본 규칙의 현재 기준은 아래 `Bulk 선택 ACK와 창 64` 절이다.
- 공유 백오프는 첫 RTT 표본 전에도 0.25초 기준에 걸린다(0.25→0.5→1초, RFC 6298 5.5). 표본 전을 0.25초 고정으로 되돌리면 Bulk 왕복이 처음부터 0.25초를 넘는 연결은 Karn 규칙 때문에 표본을 끝내 얻지 못하고 모든 조각을 한 번씩 다시 보낸다. 송신 측 타이머만의 규칙이라 와이어 형식·접속 규약은 바꾸지 않았다.
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

## #461 도색 페이로드 압축

- `LabPaintFrame.encoding`은 0 원본, 1 deflate다. Host의 피어별 발행자는 `LabPaintEncodingPolicy.Auto`(압축이 더 짧을 때만 1)를 쓰고, 기본 생성자는 `RawOnly`다. 해시·covered·`Revision`은 늘 풀어 놓은 지도 기준이다. 수신기는 헤더에서 정한 크기 상한(full `columns·rows·5 + 시각 바이트`, delta `columns·rows·7 + 시각 바이트`) 안에서만 풀고, 넘거나 깨지면 체크포인트를 요청한다. 빈 256×128 지도는 도색 프레임 1개(Bulk 2조각)다.
- 공유 설정 257개·인덱스는 그대로이며 #461 당시 묶음은 **5683/Lab44/Game28**이었다(현재 기준은 아래 #509 절). 이전 5682 앱과 혼용하지 않으며, 병합 뒤에는 모든 기기가 새 빌드를 써야 한다. 실행한 검사만 `docs/VALIDATION.md`에 기록한다.

## #471 로비 연결 끊김 뒤 핸들러 재등록

- `LabNetwork`는 패킷 핸들러를 등록한 `CustomMessagingManager` 인스턴스를 기억하고, NGO가 새 세션에서 새 인스턴스를 만들면 다시 등록한다. `Stop`을 거치지 않고 끝난 세션(방장 이탈·화면 잠김·Wi-Fi 끊김)이 다음 방의 패킷 수신을 막지 않아야 한다. 게임 흐름 게스트 끊김도 핸들러를 풀고 `C6_LAB_DISCONNECTED`를 남긴다.
- Hapio Lobby에서 연결이 끊기면 `GameSession`이 나가기와 같은 `GoHome`(`network.Stop` 포함)으로 방 목록에 돌아가고 끊김 안내를 표시한다. #501부터 게스트는 먼저 같은 방에 다시 들어가고, 그게 끝내 안 될 때만 이 경로로 간다. 게스트가 Host 로비를 받기 전(`LobbyInfoPending`)에는 지난 방 인원·자리 대신 "방 정보를 받고 있소..."를 표시하고, 고를 수 없는 빈 자리는 흐리게 그린다. 자리 선택 규칙·접속 규약·설정 수·Scene/Prefab은 바꾸지 않는다.

## #476 방 검색·방 공개 자동 복구와 화면 잠금

- `LabBonjourDiscovery`는 주소 TTL이 지나면 같은 호스트를 한 번 다시 조회하고(응답 없으면 8초 뒤 제거), 검색이 살아 있는 동안 확인에 실패한 방을 최대 3회 다시 확인한다(`serviceRetries`). 교체된 항목에 남은 조회가 같은 키의 새 항목을 지우지 않게 한다. 재시도 간격·횟수는 순수 함수 `LabDiscoveryBackoff`다.
- `LabNetwork.Recovery.cs`가 검색 재시작(1~30초, 최대 6회)·방장 공개 재등록(같은 요청 이름, 이관 방은 이름 고정)·모바일 복귀 처리를 맡는다. `DisposeDiscovery`가 대기 중인 재시도를 지우므로 실패 경로는 그 뒤에 예약한다. 살아 있는 등록은 복귀 때 다시 만들지 않는다.
- iPhone만 연결·입장 중·검색 중(`RoomSearchKeepsAwake`, Hapio는 방 목록이 보일 때만)·재시도 대기에 `SleepTimeout.NeverSleep`이다. NGO·전송 소켓 재생성, 전투 중 백그라운드 처리, 접속 규약·TXT·설정 수·Scene/Prefab은 바꾸지 않는다.

## #503 대기 방 공개 재시도

- 방장 입장 전 대기 방(`LabPendingRoom`)은 이름 충돌이 아닌 등록 실패를 `LabDiscoveryBackoff`의 1~30초 간격, 최대 6회로 다시 등록한다(`RetryPending`·`RetryAttempts`). 기다리는 동안 예약 포트와 카드를 유지하고, 다른 카드의 등록 순서를 막지 않으며, `Failed`는 6회가 끝난 뒤에만 참이다. 이름 충돌은 기존 다음 번호 재시도를 그대로 쓴다. 확정된 카드는 자기 번호를 다시 요청하고, 확정될 때마다 재시도 횟수와 이름 충돌 예산을 초기화한다.
- Hapio 방 만들기의 30초 한도는 그대로다. 재시도 중 안내와 한도 도달 안내만 바꿨다. 상세는 `docs/GAME_FLOW.md`의 같은 항목을 따른다. 접속 규약·TXT·설정 수는 바꾸지 않는다.

## #481 전투 초반 프레임 계측

- Development/Editor(`Debug.isDebugBuild`)에서만 `GameSession`이 `GameFrameSpikeProbe`를 런타임에 붙인다. 전투 단계(LoadingBattle~Playing)의 25 ms 초과 프레임과 그 프레임에 처음 실행된 일(`LabFirstUse`의 `Hapio.FirstUse.*` 마커)을 미리 할당한 배열에만 담고, 스테이지가 끝나거나 같은 스테이지를 다시 불러올 때 `HAPIO_FRAME_SPIKE`·`HAPIO_FRAME_SUMMARY`로 스택 없이 한 번 출력한다. 출력한 프레임은 다음 스테이지 측정에서 뺀다.
- 측정 전용이다. 마커를 감싼 코드의 동작·판정·설정·규약은 바꾸지 않는다. 새 첫 사용 비용 지점을 추가하면 `LabFirstUseKind`에 뒤에만 덧붙인다. Editor 프레임 시간은 기기 PSO·오디오 판단 근거가 아니다.

## #482 도색 첫 명중 작업 미리 하기

- 수채 붓 거리장은 `LabWatercolorBrushDistance.GetOrCreate`의 패턴별 공유 텍스처다(앱 수명, 패턴당 약 4 MB·최대 3개). 런타임 `Dispose`는 지우지 않고, 재생 시작·Editor 스크립트 재로드·편집 모드 복귀에서만 `ReleaseShared`가 지운다.
- `LabGourdPaint`는 두 도색 런타임을 만든 직후 라운드 붓 무늬(`RoundBrushPattern`: 라운드 config, 땅따먹기의 옛 둥근 붓은 흩뿌림)로 `Prewarm`을 1회 부른다. 실제와 같은 형식의 작은 임시 RT에만 그리고 표시 RT·Ready·Error·스탬프 수는 건드리지 않는다. 방장은 라운드 시작 직후 `PreloadPatternMask`로 판정 마스크를 미리 푼다. 판정·셀·해시·표시 결과는 같아야 한다.

## #507 박 모델 계약

- 스테이지 박은 `GameStageDefinition.gourdVisualResource`(Resources 경로, 비면 흰 박)를 `GameBattleBindings.MountStageGourd`가 `LabGourdPaint.PrepareVisual`로 `Attach`보다 먼저 지정한다. 2:2·Lab 단판·튜토리얼(#477)은 흰 박이다. 프리팹 직접 참조로 바꾸지 않는다.
- `LabGourdVisualRig`가 없는 프리팹(흰 박)은 기존 경로를 글자 그대로 탄다. 흰 박의 마운트·판정·칠·요괴 자세는 `Assets/Lab/Tests/PlayMode/Golden/`의 main 기록과 같아야 하며, 골든은 흰 박이 바뀌는 의도된 변경에서만 `GOURD_GOLDEN_RECORD=1`로 다시 기록한다. 계약 검사 실패는 `C6_GOURD_RIG_FALLBACK`과 함께 흰 박으로 돌아간다. 상세는 `docs/GOURD_MODELS.md`.
- 세 스테이지 모두 `gourdVisualResource = Hapio/Gourds/Stage0N_<요괴>_Battle`(구미호·야광귀·지귀 박)이며 판정·도색·요괴 반응·완성/결과/PNG까지 이 박을 쓴다. 경로는 굽기 도구만 설정하고 `GameStageAssets.EnsureAssets`는 건드리지 않는다. 아트 원본 프리팹은 고치지 않고 `Hapio > Gourds > Bake All Stage Gourds`(`GameGourdRigBaker`, 스테이지별 메뉴도 있음)로만 다시 굽는다. 미리보기 덧씌우기(`GameGourdModelPreview`·`GourdPreview` 리소스)는 삭제했으며 재생성하지 않는다.
- #526: 2스테이지 굽기 원본은 경량 `Yagwanggwi_Gourd_100K_GameReady`(`Spec.SourceVariant`, 전투 박 71,011삼각형)다. 원본 FBX·GameReady는 보존하고, 다른 경량 재출력도 같은 방식으로 새 GameReady를 추가해 굽는다.
- 1·3스테이지 판정 모양까지 바뀌어 도입 당시 묶음은 **공유 설정 257개·5683/`C6PhysicsLab/44`/`C6HapioGame/30`**였다(현재 기준은 아래 #509 절)(2스테이지만 바뀐 묶음은 Game29). Game28·29 앱과 혼용하지 않는다.

## #507 연속 곡선 줄

- Hapio 전투는 `GameBattleBindings.Awake`가 모든 스테이지의 `LabHangingTarget`에 `LabRopeStyle.Curved`를 런타임으로 지정한다. 고정점에서 박 연결점까지 피벗을 제어점으로 둔 베지어 한 줄이며, 물리·피벗 높이·판정·접속 규약은 바꾸지 않는다. Lab Scene과 Battle.unity 저장값은 `Segmented`이며 YAML을 고치지 않는다. 기존 `Rope`·`Upper Rope`는 transform을 계속 계산하고 렌더러만 끈다. 상세는 `docs/PINATA_TUNING_GUIDE.md`.

## #473 모든 박 1.1배

- `Gourd Visual Size`(DEV 140) 기본값은 1.54(`LabConfig.DefaultGourdVisualSize`, 이전 1.4)다. 흰 박(크기/1.8)과 세 스테이지 박(크기/`referenceVisualSize` 1.4)이 모두 1.1배가 되고 판정 충돌체도 같은 배율로 따라간다. 굽기 기준 `GameGourdRigBake.ReferenceVisualSize`·`TargetBodyHeight`(크기 1.4 기준)는 바꾸지 않으며 박을 다시 굽지 않는다.
- 흰 박 마운트 골든(`WhiteGourdMount.txt`)은 이 변경으로 다시 기록했다(요괴 골든은 그대로). Home·Lobby의 저장 DEV 140행은 `Hapio/Upgrade Menu DEV Rows`로 저장했다. Host가 보내는 공유값만 바뀌므로 설정 259개와 접속 묶음은 그대로다.

## #466 수채 브러시 누적 작품

- 일반 협동은 `WatercolorStampProfile` V2 기본 ON·농도0.9, 기존 수채는 프로필 OFF의 비교 경로다. CPU 패턴·실제 셀/P지도·성장/건조·난수·면적·투척 규칙은 그대로이며 땅따먹기는 기존 청홍 표현이다. 원본은 Editor `BrushMasters`, 512² 표현 데이터는 `GourdPaint/WatercolorBrushes`다. 베이크 해시/설정을 라운드에 고정하고, 판정용 패턴 텍스처를 표현 목적으로 교체하지 않는다.
- Host `LabPaintArtJournal`은 실제 셀 추가/삭제 지점만 별도 기록한다. 128개 기존 힌트/`paint_cells` 압축 형식을 바꾸거나 Guest 예측 명중을 확정 도색으로 기록하지 않는다. 4KiB 원본 페이지·피어당1개 미확인 페이지·게임 상태 우선·64MiB 원본/2MiB 페이지 캐시를 유지한다. 현재 라운드/관람 뒤 소유한 임시 파일만 정리한다.
- 수신·적용·GPU 완료는 별도다. `LabGourdPaint`는 GPU 완료 콜백 후 새 순번도 세션에 전달해야 한다. 정확한 관람은 최종 기록/지도/렌더 일치가 필요하나 작품 오류를 게임 판정 실패로 바꾸지 않는다. GPU 재생성은 같은 기록을 재생하며, 연출은 `BattlePresentationTime`을 쓴다.
- V2는 원본 대비/광학 농도·색역을 보존하는 채도·seed 고정의 끊긴 테두리를 누적 지도에 적용한다. 미세 안료는 경계 보간 뒤 셸 셰이더에서 물체 로컬 방향·실제 종이결로 표시하고 지도 Alpha/P/미도색 종이를 바꾸지 않는다. 최종 표면 입자를 `VisualPaintTexture`에 다시 쌓거나 등장 연출 아래층에 이중 누적하지 않는다. 전투/관람/관람PNG는 같은 지도와 전체 표현 블록을 사용한다. V1 당시 이미지는 이력으로 보존한다.
- 최신 호환 묶음은 **5684/Lab45/Game29**, 공유 설정257개 그대로이며 표현 버전은 `watercolor-stamps-v2`다. 새 안료 대비1.6·채도1.15·입자0.25/120·테두리 끊김0.45와 기존 농도/테두리 조절을 표현 지문과 라운드 고정에 포함한다. 구현/비교는 `docs/WATERCOLOR_BRUSH_STAMPS.md`, 실제 실행 결과는 `docs/VALIDATION.md`를 따른다.

- #466 V3는 `Assets/Editor/WatercolorPaint/V3`의 비교 전용 시제품이다. 게임 기본값은 V2를 유지하며 시각 승인 전 V3를 Resources/Scene/실제 라운드 준비에 연결하지 않는다. 별도 셰이더/브러시 주입은 UNITY_EDITOR에서만 컴파일하고 기본 생성자를 보존한다. V3는 원본 V2 기록을 읽는 비교 ID이며 게임 접속 버전과 혼동하지 않는다. 원본 브러시/한지·V2 베이크/프로필을 덮어쓰지 않는다.


## #466 최신 승인 — 불투명 한지 얼룩 스티커

- 일반 협동의 저장 프로필은 `PaperSticker`·`paper-stickers-v1`이 기본이다. 기존 Watercolor 생성자/자료/V3 실험은 비교용으로 보존한다. 최신 표현은 `docs/PAPER_STICKER_PAINT.md`를 따르며 위 수채 관련 문장은 해당 중간 버전 이력이다. Scene·Prefab·박 모델을 새 시안 모양으로 자동 교체하지 않는다.
- 새 CPU/GPU 공통 Splatter 마스크는 `GourdPaint/PaperSticker`에 별도 보관한다. Host/Solo는 빈 라운드에서 명시적으로 선택한다. 기존 패턴 캐시와 Broad/Flow/Legacy·땅따먹기는 유지한다. 초기/반복 예산은 유지하지만 새 외곽의 실제 셀 위치·색/P해시는 의도적으로 달라진다.
- 새 지급 셀은 도장 UV 범위와 무관하게 Alpha1로 채우고, 기존 칠 위 겹침은 새 마스크 안에서 불투명 덮기다. 종이결·얇은 경계는 RGB만 바꾸며 P지도/면적/건조 판정으로 역산하지 않는다. 보조 최상층 ID 자원도 복구와 GPU 작업 예산에 포함한다.
- 저널/4KiB증분/Bulk/압축 형식과 공유 설정257개를 보존한다. 새 모드·설정·베이크 버전은 라운드에 고정하며 접속은 **5685/Lab46/Game30**이다. 모든 기기가 같은 새 빌드를 사용한다. 빌드 전 새 데이터 검사는 읽기 전용이고 자동 재베이크하지 않는다. 실제 실행 결과만 VALIDATION에 기록한다.


## #466 별도 곡면 스티커 시제품 — 외형 승인 대기

- `Assets/Editor/WatercolorPaint/SurfaceSticker`는 실제 박 곡면에 별도 MeshRenderer로 RGBA 에셋을 붙이는 Editor-only 비교다. 게임 기본 PaperSticker/저널/프로토콜/판정/Scene은 변경하지 않는다. 실행·열별 의미·한계는 `docs/SURFACE_STICKER_PROTOTYPE.md`를 따른다.
- raw 열은 에셋 원형을 보여주는 외형 비교이며 실제 도색 면적 일치를 보장하지 않는다. fitted 열은 기존 지급 셀을 유지하되 에셋 Alpha 대신 실제 영역을 쓰므로 셀 윤곽이 남는다. 외형과 판정의 대응을 승인받기 전 기본 게임에 연결하지 않는다. 기존 칠 위 재덮기·성장 중 UV 고정·실시간 누적·관람/PNG/네트워크 연결 완료로 해석하지 않는다.
- 원본 박/한지/생성 에셋과 이전 #466 작업을 보존한다. `LabPaintStudyRenderer`의 선택적 `configureStudyInstance`는 Preview Scene 복사본만 조작한다. 검증 앱과 실기기는 이번 범위에서 실행하지 않는다.


## #506 현재 기본 표현 — 곡면 클레이 스티커

- 사용자 원형 부착 승인과 #506에 따라 저장 프로필은 `ClaySticker`·`clay-stickers-v1`이다. 이전 Paper/Watercolor와 Editor 비교 자료를 보존하며 현재 연결·범위는 `docs/CLAY_STICKER_PAINT.md`가 우선한다. 원본 박 재질은 색 지도 Ready0, 별도 `Clay Sticker Overlay`가 원본 메시를 공유해 누적 색/높이를 표시한다. 도장마다 메시·GameObject를 생성하지 않는다. 한지 내부결 대신 둥근 클레이 표면/겹침 음영을 쓴다.
- 실제 Host 접촉 법선/속도의 정면 속도를 초기0~20m/s 기준으로 정규화해 `LabPaintVisualStamp.impact01` 예약 바이트에 보존한다. 0은legacy0.5, 44바이트 크기는 유지한다. 농도 기본값은 min=max=1(칠 팔레트 그대로, 2026-11-11 변경, 이전 .65~1은 흰클레이와 섞여 흐렸다)이며 Alpha/면적/성장/물리/스태미나/P판정을바꾸지않는다. 농도 범위는 표현 버전 fingerprint에 포함된다.
- 원형 표현은 고정2셀 시각여유를 허용하며 면적에는포함하지않는다. 100%는 실제셀판정이고마지막표시빈틈은별도클레이바탕으로채운다. 건조는모든누적색/높이를제거하며0셀에서시각여유까지비운다. 저널/페이지/캐시/과거자국보존정책은유지한다.
- DEV257(두께0~.02, 기본.006)·258(광택0~1, 기본.25)을뒤에붙여259개다. Host Pause중즉시미리보기는 `clay_appearance`의session/round/config/sequence를검사한다. 실제렌더는 `network.ClayThickness/ClayGloss`, sourceConfig를재저장하지않는다. HapioCommon프로필은두값을다음stage에도기억한다. 새접속묶음5686/Lab47/Game31, 원격main5683과혼용금지.
- 최초준비는라운드생성성공→Synchronizing→기존ready/Countdown gate순서다. 첫프레임표시미준비를라운드생성실패로간주해Loading에갇히게하지않는다. 기존입력차단/전원시작확인은우회하지않는다.
- 관람은색/P/높이를모두독립복사하고기본박Ready0을유지한다. 클레이해제는지연Destroy전에Renderer를즉시끈다. 빌드검사는읽기전용이며원본/데이터팩을자동베이크하지않는다. 실제실행결과만VALIDATION에기록하며iOS/실네트워크검증은이번범위에포함하지않았다.

- #506 최신 main 통합: #507의 스테이지별 몸통/뚜껑 Rig·판정·연속 줄·요괴를 유지한다. 클레이는 각 paintShell/paintExtra의 identity 자식이며 결과 복제도 각 base의 Ready0과 coat의 Ready1을 유지한다. #482 도색 준비를 유지하고 새 클레이의 3브러시×색/높이 패스도 별도 작은 타깃에서 준비한다. 실제 지도·진행 순번·난수는 프리워밍으로 바꾸지 않는다.
- Home·Lobby에 저장된 DEV 패널은 257행(`LabConfig.ClayThicknessField`)이고 257·258은 실행 중 `GameScreen.EnsureClayDeveloperRows`가 붙인다. Editor 도구(`Hapio/Upgrade Menu DEV Rows`·`Upgrade Three Yokai Stages`·`Upgrade 2v2 Lobby UI`·`Upgrade Figma Game Lobby`, 새 메뉴 생성)는 `GameSceneBuilder.SavedDeveloperRowCount`까지만 저장·확인하고, 예전 도구가 저장한 257행부터는 지운다. 글꼴 갱신은 Prefab 인스턴스(공통 로비·오프닝)에 덮어쓰기를 만들지 않는다. `LabConfig.asset`의 DEV 기본값을 바꾸면 `Upgrade Menu DEV Rows`로 저장 행을 다시 맞춘다.

## #479 지귀 A 부적

- A는 5/40/70초에 **모든 자리**에서 동시에 열린다. 예전의 대상 한 명·박 3번 명중·회복 4배는 없다. 자리마다 스냅샷 `LabJigwiABPlayerFire.talismanAt`(아직 안 썼으면 음수)을 둔다.
- 소각은 한꺼번에가 아니라 **한 개씩** 탄다. 첫 구슬은 DEV243(기본 1초) 뒤, 이후 Inspector `jigwiABBurnStaggerSeconds`(1초) 간격이며 소유자별 생성 순서(createdAt/id)를 따른다(`NextJigwiABBurnAt`).
- 각 기기의 `LabJigwiTalismanOverlay`(`LabJigwiScreenPresentation.EnsureTalisman`이 만듦)가 조합판 위 **전투 영역**에 세로 한지 부적을 띄우고, 쓰는 동안 `board.SetUiInputBlocked`로 조합판 조작을 막는다. 신라문화체 모양의 점선 "물"(88점, `LabJigwiTalismanStroke.Water`)을 Inspector 기준 90% 덮으면 `jigwiTalisman` 요청(firstId)을 보내고 1초마다 다시 보낸다. 판정은 Host `TryCompleteJigwiTalisman`만 한다.
- Host는 그 자리의 소각 예약을 지우고 `jigwiTalismanReward` Water 5개(수명 15초, 칠 1.5배, B 불 꼬리 없음)를 준다. 모든 자리가 쓰면 A를 Completed로 일찍 끝내고, 못 쓰면 15초에 벌점 없이 끝난다.
- DEV241/242는 은퇴 슬롯("Reserved …(unused)", 인덱스 보존, Home/Lobby 저장 라벨도 맞춤)이다. A 스태미나 화염 테두리, 화면 붉은 기운/양옆 불기둥, 구슬 위 불꽃 그림은 지웠고(구슬은 마감 전 1초 동안 `LabJigwiOrbFlame`이 아래에서 위로 타 들어가고, 그 선 위에 먹선 불꽃이 따라 올라감), A 외침은 `PatternAudioProfile.personalVoiceEnabled`(0)로 꺼 두었다(B 외침은 유지).
- 에디터 전용: `Hapio > DEV SOLO 시작 스테이지` 메뉴(EditorPrefs, Home에서 Play할 때만 적용). 빌드와 Test Runner에는 영향이 없다.
- #479 당시 호환 묶음은 설정 259개(#506 클레이 257·258 포함)·**5687**·`C6PhysicsLab/48`·`C6HapioGame/32`였다(현재 기준은 아래 #509 절). main의 #466/#506/#507이 5684~5686·Lab45~47·Game29~31을 먼저 써서 #479는 그 위로 올렸다. 이전 빌드와 혼용하지 않는다.

## 마지막 일격 다시보기 (스토리 1·2·3)

- 100% 뒤 Host가 마지막 일격(결과의 `LastPainter`가 최근 4초 안에 던진 칠 명중, 없으면 최근 명중. 구슬이 아닌 칠(지귀 공통 부적 폭포 등)로 끝나 4초 안 명중이 없으면 명중 때 저장해 둔 자세로 이번 라운드의 가장 최근 명중) 기록 `LabFinishReplayShot`을 finish prepare/commit의 `replay` 문자열에 싣는다. 모든 기기가 공통 `StartsAt`부터 세 각도(던진 사람·옆면·확대, 1.31/1.5/1.8초, 점점 느리게)와 전환 0.5초, 총 5.11초를 재생한 뒤 한지색 `GameSceneFade` 아래에서 결과로 간다. 후보가 없거나 12초 한도에 들어가지 않으면 기존 1.2초 연출이다. 2:2·Lab은 바꾸지 않는다.
- 표현 전용이다. 실제 박은 숨기지 않고 렌더 콜백 사이에서만 기록 자세로 그린다(`LabHangingTarget.SetPresentationPose`). 구슬은 물리 없는 ghost, 카메라는 전용 연출 카메라다. 판정·통계·네트워크 상태·BattleCamera 자세·전역 `Time.timeScale`을 바꾸지 않는다.
- 새 탭은 그 기기만 건너뛴다. release는 모든 참가자의 done(끝남·건너뜀)을 기다린다(`TryRelease` 최소 0, 기존 연출은 1.2초 유지). 솔로는 건너뛰면 바로 결과로 간다.
- #525 당시 호환 묶음은 설정 259개·**5687**·`C6PhysicsLab/48`·**`C6HapioGame/33`**이며 Game32 앱과 혼용하지 않는다. 상세는 `docs/FINAL_HIT_REPLAY.md`, 실행한 검사는 `docs/VALIDATION.md`를 따른다.

## #509 지귀 B 지목 동시 명중

- 시간표: A 5/42/70초, B 25/55/85초. A2는 공통 폭탄(40초)과 겹치지 않게 42초로 옮겼고, B는 15초(`LabJigwiABTimeline.GroupMissionSeconds`)다. B3는 100초 정리로 벌점 없이 취소된다.
- 예고는 활성 2초 전(`LabJigwiABTimeline.WarningSeconds`, 이전 1초)이고 상단에 지귀 도발만 띄운다: A "타올라라, 모두 재가 되어라!", B "흩어진 물방울로는 어림없다!". 파훼법은 문구로 설명하지 않는다.
- B는 1~2인 1명 / 3인 2명 / 4·5인 3명을 seed로 뽑는다(`GroupPickCount`, `JigwiGroupPicks`). `round.players`에는 지목된 자리만 있다. 아무도 막히지 않고(`JigwiABBlocksBoss` false), 불기둥/장막/콜라이더가 없다.
- 지목된 전원이 첫 명중부터 1초 안에 **박**을 Water Combined로 맞히면(`RecordJigwiBossSyncHit` → `RecordJigwiSyncHit`) `syncCount`+1, 늦으면 0으로 초기화(`syncResetAt`). 혼자면 1번 = 1회. 3회면 성공.
- 성공(물벼락): 3번째 동시 명중 순간 지목된 사람마다 마지막으로 맞힌 자리에 Water 칠을 한 번 더(Inspector `jigwiBSplashPaintShare`, 기본 3 = 한 타격 최대). 5초 발사 보상은 없다(`rewardUntil` -1, DEV251/252 은퇴).
- 실패(조합판 불바다): 15초 만료 시 `boardFireUntil` = 만료+Inspector `jigwiBBoardFireSeconds`(기본 3초, 100초 정리에서 자름)까지 모든 자리의 `TryGenerate`가 `JIGWI_BOARD_FIRE`로 거절된다. 판 구슬/스태미나는 그대로이고 판 비우기 fence(`clearedThroughRevision`)는 늘 0이다.
- 무료 물 구슬은 도우미 판에만 준다(도우미가 없으면 지목된 자리). 이 물은 진행에만 쓰이고 칠하지 않는다.
- 화면은 `LabJigwiSyncPillarView`(클래스 이름만 예전 그대로, 물방울 3칸·1초 막대·빨간 흔들림·iOS 기본 진동, 글자 없음)와 글자 없는 붉은 실패 오버레이다. B 동안 화면 글자는 예고 2초의 지귀 도발뿐이다(지목 이름/진행/안내/결과/다음 기믹 문구 없음). 지귀 A/B 외침 목소리는 지웠다.
- DEV244/245/251/252는 은퇴 슬롯(인덱스·저장값 보존, Home/Lobby 라벨도 맞춤)이다.
- #509 당시 묶음은 설정 259개·**5688**·`C6PhysicsLab/49`·`C6HapioGame/34`였다. main #525가 Game33을 먼저 써서 34로 올렸다. 5687/Game33(#479·#525) 앱과 혼용하지 않는다.
- #526 당시 호환 묶음은 설정 259개·**5688**·`C6PhysicsLab/49`·**`C6HapioGame/35`**였다. #526에서 2스테이지 박의 칠·판정 껍데기를 100K 모델로 다시 구워 Game만 올렸다. Game34 앱과 혼용하지 않는다. 현재 기준은 아래 `Bulk 선택 ACK와 창 64` 절이다.

## #540 지귀 A 부적 실패 벌 — 도입 당시 호환 이력

- A가 끝날 때까지 부적을 못 쓴 자리는 그 판에 남은 구슬(원소·결합)이 만든 순서대로 0.12초 간격으로 모두 탄다(기존 `jigwiABBurnAt` 소각 경로). 북은 막지 않는다. 다 쓴 자리와 게임 종료로 취소된 A는 벌을 받지 않는다. 보상·벌 모두 자리별이다.
- A 제한 시간은 LabConfig 에셋에서 10초, 구슬 소각 간격은 2초다(코드 기본값은 그대로).
- #540 시간표(야광귀와 같은 흐름): LabConfig `jigwiFlowSchedule`(Host, 에셋에서 켬)이면 A1 10~20초 / B1 45~60 / A2 66~76 / B2 83~98, 공통 기믹은 40초부터 15초마다(40·55·70·85, `jigwiCommonStartSeconds`·`jigwiCommonIntervalSeconds`), 100초부터 피버. 스냅샷 `jigwiAB.schedule`(0 = #509 시간표, 1 = 새 시간표)로 검증한다. 코드 기본값은 #509 시간표라 기존 모델 테스트는 그 시간표를 쓴다.
- 부적 구슬(A 보상, `jigwiTalismanReward`): 판·비행 중 모두 오색구슬 모습(LabFiveColorSparkle)을 푸른 톤(`BlueTones`)으로 쓰고 부적에 쓴 "물"을 금빛으로 얹는다(`LabJigwiTalismanOverlay.GlyphSprite`, 획 순서 점을 붓 획으로 이어 그림). 명중은 Host `LastHitTalisman` → 타격 패킷 `talisman`으로 모든 기기에 알리고, 맞은 자리에 푸른 가루·별가루만 낸다(부적이 붙는 연출은 없음, 칠은 3배 그대로).
- 부적 구슬은 3D에서 구슬 하나가 넓은 파도를 타고 간다(`LabJigwiWaveRide`: 구슬 아래 넓은 물결 판 + 앞머리 하얀 물마루 + 말려 넘어가는 물보라, 박에 닿거나 사라지면 폭 전체가 부서짐). 옆 두 갈래는 보이지 않게 날아가며(`LabJigwiHiddenShard`) 칠만 한다. 내 부적 구슬이 맞으면 내 폰이 세게 진동한다(`talismanHitHapticIntensity`, 던진 사람만, 한 번 던지면 한 번).
- 부적 구슬은 판·비행 모두 보통 구슬의 1.5배(`LabJigwiWaterVolley.SizeScale`, 판은 `jigwiABScale`>1로 키움). 던지면 가운데 구슬 양옆으로 물구슬 둘이 같이 날아가는 3갈래 발사다(`LabModel.JigwiTalismanVolleyCount`, 옆 구슬 id `JigwiTalismanShardId`, 자리·속도는 `LabJigwiWaterVolley.ShardPosition/ShardVelocity`로 모든 기기가 같게 계산, 새 패킷 필드 없음). Host에서는 셋 다 진짜 투사체라 각각 박에 맞으면 각각 물로 칠한다(옆 구슬은 `ConfirmJigwiTalismanShardHit`, 같은 칠 배율, 피해·다른 보상 없음, 빗나가면 `DropJigwiTalismanShard`). 떠날 때 물기둥 소용돌이(`LabJigwiWaterVolley.Launch`), 날아가는 동안 물줄기 꼬리·물방울·물안개(`LabJigwiWaterStream`), 맞을 때 큰 물보라(`Splash`, `PresentTalismanHit`)는 그림만이다(`LabJigwiWaterFx`가 알갱이를 모아 움직임).
- 부적 구슬이 박에 맞으면(3갈래 중 어느 것이든, `LabNetwork.TalismanHit` → `LabJigwiTalismanOverlay.OnTalismanHit`) 불이 물에 꺼지는 "치이익"(`JigwiFireDouse`, 한 번 던지면 `douseGapSeconds` 안에 한 번)이 나고, 맞은 자리에서 하얀 수증기가 크게 피어오르며 3D 화면이 짧게 흔들리고 옅은 파란빛으로 번쩍인다(`LabJigwiWaterFx.Steam/Shake/Flash`, 흔들림은 그리는 동안만 카메라를 옮겼다 되돌림). 대사("너무 차가워")는 쓰지 않는다.
- 부적을 다 쓰면 경보는 멈추고 불덩이 반복·타닥 소리는 `finishFadeSeconds`(1.2초)에 걸쳐 잦아들며, 김 빠지는 숨 + 따뜻한 화음의 안도 소리(`JigwiTalismanRelief`)가 그 빈자리를 채운다.
- 획 순서 안내: 쓰는 동안 지금 써야 할 획만 글자 안에서 은은하게 빛나고(`strokeGuideColor`), 파란 화살표가 그 획의 아직 안 쓴 첫 점부터 쓰는 방향으로 되풀이해 달린다(`strokeArrowColor`). 순서는 안내일 뿐 다른 곳을 칠해도 된다(`EnforceOrder` 끔 그대로).
- 부적 구슬은 수명이 없고(`JigwiTalismanOrbLifetimeSeconds`, 오색구슬과 같은 방식) A 소각·실패 벌에도 타지 않는다. `jigwiTalismanWaterLifetimeSeconds` 설정은 지웠다.
- A "다 태워버리겠어" 불덩이(`LabJigwiFireball`, 먹색 테두리의 만화풍 2D 불꽃 `LabProceduralFire.CartoonFlame`, 그 자리 사람 전투 화면에만): 예고 2초에 박 위에 불씨가 붙고, A 동안 불덩이가 커지며(남은 시간 표시), 다 쓰면 그 자리에 멈춰 사라지고, 못 쓰면 조합판으로 떨어진다. 숫자·두루마리·구슬 밀어내기는 없다. LabConfig `jigwiAHoldBurnUntilEnd`(Host, 에셋에서 켬)이면 쓰는 동안 판의 구슬은 타지 않고, 못 쓰고 끝날 때만 그 판 구슬이 모두 탄다(코드 기본값은 #479처럼 A 동안 하나씩 탐). 이때 A가 시작될 때 판에 있던 구슬(원소·결합)은 그 사람이 쓰는 동안 수명이 멈춘다(Host가 A 길이만큼 expiresAt을 미루고, 일찍 다 쓰면 남은 만큼 되돌림). 불씨는 예고 0.7초(지귀 "타올라라")에 붙고, 쓰는 동안 깔리는 반복 소리(`JigwiFireballLoop`, 경보처럼 들리는 "우웅")는 불덩이와 함께 점점 커지고, 불덩이는 초반부터 빠르게 커진다(`LabJigwiFireball.Grown`). 못 쓰면 A 동안 가장자리에서 타 들어가던 불이 따로 새 불 없이 그대로 안쪽까지 다 태우고(`burnSeconds` 1.2초, 그을린 띠 바깥 다 탄 곳은 `RectMask2D`로 네 변을 잘라 부적이 가장자리부터 줄어듦), 불덩이가 떨어지며 "모두 재가 되어라!"(`JigwiVoiceAFail`), 0.45초 뒤 판에 닿아 불이 확 번지는 소리(`JigwiFireSpread`)가 나고, "모두 재가 되어라!"가 끝나면(A 끝 3.7초 뒤, `LabModel.JigwiTalismanFailFirstBurnSeconds`) 그 순간 판에 있는 구슬(원소·결합, 비행 중·부적 구슬 제외, 대사 중에 새로 만든 구슬 포함)이 모두 함께 1초(`LabModel.JigwiTalismanFailBlazeSeconds`) 동안 부적 가장자리가 탈 때와 같은 그림(그을린 띠, 밝은 불씨 줄과 안쪽 주황 빛, 불 줄 위에 선 작은 불꽃, 오르는 불티, 떨어지는 재)으로 둘레에서 가운데로 타 들어가며 재가 되어 사라진다(Host `IgniteDueJigwiFailBurns`가 그 순간에 고름, `LabJigwiAshBurn`, 구슬 그림 자체가 둘레부터 줄어들고(크기, SpriteMask는 구슬 재질에 따라 듣지 않아 안 씀) 그 가장자리에 그을린 띠·불씨 줄이 얹힘, 다 탄 구슬은 사라질 때 연기 없음 `LabOrbBoard.Vanish`). 그 뒤 만든 구슬은 타지 않는다. 화면 전체 불길은 없다. 쓰는 동안 공격 영역(조합판 위)이 경보처럼 붉게 번쩍이고(끝이 다가올수록 빠르게, 번쩍일 때마다 `JigwiAlarmPulse`와 Rigid 진동 `alarmHapticIntensity`, 끝이 다가올수록 세짐), 못 쓴 판의 구슬이 타기 시작하는 순간 1초짜리 타는 소리(`JigwiOrbBurn`)가 한 번 난다. 그 뒤 LabConfig `jigwiAFailDrumSlipSeconds`(각 기기, 에셋 15초, 다른 기믹 예고·피버가 오면 그 전에 꺼짐) 동안 그 사람 북이 부적이 탈 때처럼 겉 테두리부터 그을림과 불씨 줄이 번져 들어와 테두리 쪽에서 멈춘 채 유지되고(`LabJigwiAshBurn`, 불꽃 없음, 북은 타 없어지지 않음) 가벼워진다(`LabOrbBoard.SetJigwiDrumSlip`, `jigwiAFailDrumSlipBoost` 2 = 옆으로 미는 빠르기 2배·감속 1/2).
- 부적 완성 연출 길이는 LabConfig `jigwiTalismanRewardDelaySeconds`(Host) 하나로 맞춘다. 부적 화면이 도장 뒤 머무는 시간을 그 값에 맞춰 늘이고 줄인다.
- 부적 완성 흐름(약 2.9초, 에셋 `jigwiTalismanRewardDelaySeconds` 2.9): 숨 고르기 0.1초 → 금빛 훑기와 가락 "솔 도 시 라"(불은 그동안 그 자리에 멈춰 옅어짐) → "라!"에 도장 → 부적이 한 바퀴 빙글 말리며 구슬 크기로 오므라들고 가운데서 자라난 부적 구슬(물빛 + 금빛 "물")에 스며듦 → 구슬이 살짝 튀었다가 Host가 진짜 부적 구슬을 놓는 자리(`LabModel.JigwiTalismanWaterSpot`, 화면 위치는 `LabOrbBoard.TryGetScreenSpot`)로 톡 떨어짐. 진짜 부적 구슬은 위에서 떨어지지 않고 그 자리에 나타나며, 떨어진 구슬은 그것이 보이면(최대 0.6초) 사라진다. 부적을 다 쓰면 그 사람 판 구슬은 보상이 판에 닿고 1초(`LabModel.JigwiTalismanCalmSeconds`)까지는 수명이 다하지 않는다.
- #540부터 지귀 불 연출(불덩이·부적 테두리 불·구슬이 타오르는 불, 북 불은 예외로 폭탄 불)은 그림 파일(Bomb/BlastFire 등)을 쓰지 않고 `LabProceduralFire`로 코드에서 그린다.
- #540 당시 호환 묶음은 설정 259개·**5691**·`C6PhysicsLab/52`·**`C6HapioGame/38`**였다. main의 Bulk 선택 ACK(5690·Lab51·Game37)를 받은 위에 #540을 올렸다. 현재 기준은 아래 #551 절이다.

## #551 지귀 B 3-2-1 동시 던지기

- LabConfig `jigwiBCountdown`(Host, 에셋에서 켬): 지목된 사람이 **모두** 자기 특수 물구슬을 손가락으로 잡고 있으면 Host가 3-2-1을 시작한다(`round.countdownAt`, 요청 `jigwiBReady`, `LabModel.TrySetJigwiBReady`). "던져!"는 `countdownAt + LabJigwiABTimeline.CountdownSeconds`(1.8초).
- "던져!" 전에는 특수 물구슬이 던져지지 않는다(판 `LabOrbBoard.ThrowGate`가 미끄러지기만 하게, Host도 `JIGWI_B_WAIT`로 거절). 그 전에 누가 놓으면 3-2-1이 취소되고 다시 모두 잡으면 다시 시작한다.
- "던져!" 뒤 `ReleaseSeconds`(1초) 안에 지목된 사람이 모두 위로 휙 던져야 한다(`seat.launchedAt`, 손만 떼면 던진 것이 아님). 늦으면 그 자리에서 실패(`FailLateJigwiBRelease`). 첫 명중부터 `round.syncWindowSeconds`(1.5초) 안에 모두 맞히면 성공. 빗나가면 실패(한 번의 기회).
- 실패 벌(조합판 불바다)은 에셋 `jigwiBBoardFireSeconds` 0으로 껐다(코드 기본값 3초 그대로, 새 벌은 나중에 정함). 실패 순간의 화면 불꽃도 판 불바다가 켜졌을 때만 보이고, 지금은 짧은 붉은 기운만 남는다(`LabJigwiScreenPresentation.DrawFailure`).
- 3-2-1 화면(`LabJigwiBCountdownView`, 모든 기기): 지목된 자리마다 P번호 동그라미(잡고 있으면 파랗게, 내 자리는 굵은 금색 테두리), 모두 잡으면 가운데 큰 "3·2·1"(박자마다 `JigwiBBeat` 북소리) → "던져!"(`JigwiBGo`, 내 구슬에서 위로 솟는 화살표 3개), 지목된 사람 폰은 박자마다 진동. "꾹 잡아" 안내: 아직 안 잡은 자리 동그라미는 고리가 두근거리고(모두가 봄), 내 판의 특수 물구슬 둘레에도 빈 고리가 두근거리며 "꾹!"이 뜬다. 잡으면 고리가 파랗게 한 바퀴 차오른다(게이지).
- 끄면(코드 기본값) #509 규칙 그대로: 아무 때나 던지고 1초 안에 모두 맞히기.
- B만 시험: LabConfig `jigwiBTestOnly`(Host, Inspector, 에셋 기본 끔)를 켜면 지귀 스테이지가 B만 4초부터 17초마다(4/21/38/55/72초, `LabJigwiABTimeline.BTestSchedule` = 스냅샷 `schedule` 2) 내고 A와 공통 기믹은 없다. 시험 뒤 끄고, 켠 에셋은 커밋하지 않는다.
- 실패한 B의 `boardFireUntil`은 불바다가 꺼져 있으면 -1이어도 검증을 통과한다(전에는 Guest가 실패한 B 스냅샷을 거절할 수 있었음).
- LabConfig `jigwiBBucket`(Host, 에셋에서 켬) — **개별 나무 물통**(11-08 오후 확정): B에 지목이 없고 **여우구슬처럼 따로따로**, B 시작 `BucketLeadSeconds`(3.5초) 뒤부터(2초 예고 때 나온 지귀 대사를 다 듣고, 박이 달아오른 다음) 한 사람씩 `BucketStaggerSeconds`(1초) 간격(무작위 순서)으로 자기 양동이가 온다(`seat.readyAt` = 도착, `round.bucket`, 특수 물구슬/3-2-1 없음, `countdownAt` -1). 도착부터 `BucketSeatSeconds`(7초) 안에(5명이어도 3.5 + 4 + 7 = 14.5초로 15초 B 안) 쏟아야 한다. 폰을 좌우로 기울여 `sloshDegrees`(20°)를 넘겨 꺾을 때마다(방향을 바꿔야 다음) 출렁, 3번(`sloshesToPour`)이면 마지막으로 꺾은 쪽으로 쏟고 `jigwiBPull`을 보낸다(`LabModel.TryJigwiBPull`, `seat.launchedAt`). Host는 받자마자 그 사람 쪽 박에 큰 물 칠(`jigwiBSplashPaintShare`). 모두 쏟으면 완료, 아니면 15초에 Expired. **못 쏟은 사람은 그 화면에서 지귀 연기가 화면 전체(박이 보이는 3D 영역과 조합판)를 회색으로 덮고(판 입력 막힘), 90% 문질러야(`steamClearLeft` 10% 아래) 걷힌다**(화면만, Host 상태 없음). 쏟는 방향은 그림만 다르다.
- 물통 화면 `LabJigwiBucketHandleView`(2D, **글자 없음**): ① **위협** — 내 양동이가 오기 `threatLeadSeconds`(1.2초) 전부터 박이 내 쪽으로 시뻘겋게 달아오르고 연기가 모인다(처음부터 또렷하게 붉고, 남은 시간이 줄수록 커지고 빨리 뛰며 가운데가 하얗게 달아오름 = 시간 표시. 위치는 박 충돌체를 전투 카메라로 비춘 자리). ② 박 바로 위(`bucketHang` (.5, .93))에 🪣 양동이가 끈에 매달려 내려오고, 폰 기울기를 따라 같은 쪽으로 기운다(`bucketFollow`). 출렁마다 양동이가 흔들리며 물방울이 넘치고, **양동이 위 흔든 양 게이지**(출렁 수 + 다음 출렁까지 기운 정도, 칸 구분선)가 찬다(시간 게이지 없음). 판 가운데 📱 그림은 템플런처럼 폰 전체가 좌우로 기우는 모습(글자 없음). ③ **성공** — 뒤집혀 물줄기가 박으로, 불 꺼지는 소리(`JigwiFireDouse`), 박이 하얀 김을 뿜으며 식는다. ④ **실패** — 양동이가 떨어지고 지귀 대사(`failVoiceFolder`, 말풍선 없음) → 박이 숨을 들이켜듯 부풀었다가 뿜으면(화면 흔들림) → 달아오를 때 박 위에 피던 그 연기 덩어리 16개가 그대로 부풀며 화면 전체로 퍼져 회색으로 채우고 → 두꺼운 연기 막이 깔리며 쿵(`fogHitSoundFolder` = `JigwiBucketFogHit`, 강한 햅틱) → 작은 검지 손이 3D 영역에서 문지르는 시늉. 양동이는 박을 가리지 않게 `bucketHang` (.5, .93), 쏟을 때 3D 화면이 흔들린다(`pourShake`, `LabJigwiWaterFx.Shake`). 김 위 손은 검지만 펴고 나머지 손가락을 접은 앞모습 👆. 쏟은 물은 박 윗부분(`BucketPourPitch` 58°, 그 사람 쪽으로 기운 위)에 칠한다. 물통 회차에는 #509 물방울 칸(`LabJigwiSyncPillarView`)을 숨긴다. **소리**: 달아오르는 동안 지글지글 반복(`heatLoopFolder` = `JigwiBucketHeat`, 뜨거울수록 크고 높게, 식으면 멈춤), 쏟을 때 쏴아(`pourSoundFolder` = `JigwiBucketPour`), 연기가 뿜어질 때(`smokeSoundFolder` = `JigwiBucketSmoke`). 출렁마다 찰랑(`sloshSoundFolder` = `JigwiBucketSlosh`, 기운 쪽 스피커로 치우침). 대사: 쏟아 식을 때 지귀 비명 "앗 차가!!"(`pourVoiceFolder` = `JigwiVoiceBucketPour`, 강한 햅틱), 실패 말풍선 때 "연기로 뒤덮어주마!"(`failVoiceFolder` = `JigwiVoiceBucketFail`). 다 걷힌 순간(`clearSoundFolder` = `JigwiBucketClear`), 연기를 문지를 때 부채질 휘익(`rubSoundFolder` = `JigwiBucketFan`, 4%마다, 0.25초 간격). 폴더가 비어 있으면 조용히 넘어간다. **햅틱**: 10°마다 약한 톡, 출렁 1·2·3번 점점 세게, 쏟을 때 길고 세게, 말풍선 때 툭, 김 덮일 때 쿵. Editor/DEV 빌드는 **A/D 또는 ←/→**(`keyTiltDegrees` 35°). 지귀 부적(A)을 공용 기믹으로 바꾸는 일은 새 이슈로 따로 한다.
- #551 당시 호환 묶음은 설정 259개·**5697**·`C6PhysicsLab/58`·**`C6HapioGame/44`**였다(현재 Game 기준은 아래 #501 절, main #558의 5691·Lab52·Game39 위). 5696 이하 앱과 혼용하지 않는다.

## 변경 범위 기반 검사와 검사 전용 시작 단축 (2026-11-10)

- PR마다 전체 검사를 의무 실행하지 않는다. 검사 전에 실제 diff와 실행·호출 경로를 확인해 아래 기준으로 범위를 선택한다. 상세 기준·실행 예시는 `docs/TEST_SUITE_REPORT.md`의 현재 검사 선택 기준을 따른다. 현행 동작·계약을 지키는 핵심 검사를 삭제하거나 단언을 완화해 시간을 줄이지 않는다. 미사용·퇴역 기능 전용 검사는 아래 정리 기준에 따라 제거한다.
- 문서·동작 변경 없는 주석만 바뀌면 내용·링크·diff 정합성을 확인하고 Unity 검사·빌드는 생략한다. Scene·Prefab·Material·Shader·Importer·설정·테스트 코드 변경은 문서 변경으로 분류하지 않는다.
- 일반 UI·VFX·개별 기능 변경은 컴파일, 전체 EditMode, 변경 기능과 영향받는 호출·연결 경로의 PlayMode를 실행한다. 화면 표현 변경은 관련 렌더링·참조 검사와 가능한 환경의 실제 화면 확인을 포함하고, 실행하지 못한 화면·실기기 확인은 미검증으로 남긴다.
- 세션·동기화·통신 규약/직렬화·공유 설정 계약, 공통 입력/물리/투사체 수명, 도색 면적·성장·건조·소유권·승패 판정, Scene 전환/공유 수명, Unity·패키지·플랫폼/빌드 설정의 동작을 바꾸거나 영향 범위를 판단하기 어려우면 전체 EditMode·PlayMode를 실행한다. 파일명이 아니라 실제 동작과 의존 관계로 판단한다.
- 검사에는 NUnit `[Category]` `Slow`(fixture 5초 이상)·`GPU`·`Bonjour`·`LabOnly`·`Legacy`를 붙인다. 새 검사도 같은 기준으로 붙이며 현재 경로를 지키는 유일한 검사에는 `Legacy`를 붙이지 않는다. 빠른 묶음 `-testCategory "!Slow;!Legacy;!LabOnly"`은 개발 중 선택 가능한 보조 묶음이다. 변경 경로의 관련 검사는 카테고리와 관계없이 직접 포함한다. 카테고리는 검사 삭제나 필요한 검사 생략의 근거가 아니다.
- 통과 결과는 관련 코드·에셋·검사·패키지·설정과 실행 환경이 같은 경우 재사용한다. 검증 기록·PR 설명 등 동작에 영향 없는 문서만 바뀌면 재실행하지 않는다. 수정이나 main 통합 후에는 기존 검증 버전과의 diff·연결 경로를 비교해 영향 검사부터 재실행하고, 위 전체 검사 조건에 해당하면 전체를 실행한다. 실행 환경·플랫폼 차이를 같은 검증으로 취급하지 않는다.
- PR과 `docs/VALIDATION.md`에는 검사 범위 선정 이유, 기준 버전·환경·수행 결과, 전체 검사 생략 이유와 미검증 항목을 구분해 기록한다. 실패 후 통과해도 최초 실패와 원인·수정/재실행 결과를 보존하며, 실행하지 않은 검사는 PASS로 쓰지 않는다.
- `GameSession.Start.cs`의 `GameStartTiming` 기본값(3초·1초·5초·0.5초)은 게임 코드에서 바꾸지 않는다. `Hapio.PlayModeTests`의 `GameTestStartTiming`(SetUpFixture)만 길이를 줄이며 #306의 순서·ACK·재예약·로드 한도는 같다. 카운트다운 길이에 기대는 검사는 기본값으로 되돌려 실행하고, 시작 직후 던지는 다시보기 검사는 `WaitReplayPoseWindow`로 자세 기록 창을 기다린다.
- 실제 Host를 여는 PlayMode 검사는 고정 UDP 포트 대신 `LabSavedSceneNetworkFixture.UseAvailablePort`·`AvailableDualFamilyPort()`를 쓴다.


## 미사용·퇴역 기능의 검사 정리 (2026-11-11)

- 기능을 삭제·대체하거나 테스트를 정리할 때는 관련 검사도 함께 검토한다. 폐기된 기능에만 해당하고 현재 지원하는 동작·계약을 보호하지 않는 검사는 근거를 확인한 뒤 제거한다. 과거에 작성됐다는 이유만으로 계속 보존하지 않는다.
- 사용 여부는 앱/빌드 진입점, 호출자와 간접 호출, Scene·Prefab·컴포넌트·설정의 직렬화 참조, Resources 등 동적 로드, Editor 메뉴, 플랫폼·컴파일 조건을 필요한 범위에서 추적해 판단한다. 지원 중인 DEV·Lab·비교/실험 경로도 포함한다. 기본 OFF·숨김·파일명·오래된 Task 번호·`Legacy`/`LabOnly` 카테고리·한 번의 검색 결과만으로 미사용을 확정하지 않는다.
- fixture에 현행 검사와 퇴역 검사가 섞였으면 메서드·단언 단위로 정리한다. 현행 조합·전달·투척·도색·승패 등의 핵심 단언이 구형 Scene/API에만 있으면 현행 경로로 먼저 옮겨 통과를 확인한 뒤 구형 부분을 삭제한다. 같은 기능명의 모델·물리·UI·통신·GPU 검사는 서로 다른 층이므로 중복으로 간주하지 않는다. 실제 중복을 제거할 때는 남은 검사가 같은 조건·실패 경계·계약을 확인하는지 대응시킨다.
- 게임에서 더 이상 직접 쓰지 않는 데이터라도 저장값·예약 인덱스·구형 패킷 거절 등 현재 유지하는 호환성 계약의 검사는 남긴다. 사용 여부나 남은 계약이 불명확하면 `[확인 필요]`와 부족한 근거를 기록하고 해당 검사는 유지한다.
- 실패·간헐 실패·느린 실행만으로 검사를 삭제·Ignore 처리하거나 단언을 완화하지 않는다. 변경된 규칙과 검사 전제가 충돌하면 현재 규칙을 확인해 전제나 기대값을 고치고, 실제 결함이면 구현을 수정한다. 기능 폐기가 별도로 확인된 경우에만 퇴역 기준으로 제거한다.
- 정리 PR과 `docs/VALIDATION.md`에 삭제한 파일/검사, 폐기·미사용의 근거, 남은 계약, 핵심 단언을 옮긴 위치 또는 중복을 맡는 검사, 실제 검증 결과를 기록한다. 검사 코드 삭제는 문서 변경으로 분류하지 않는다. 공유 준비·정리 코드와 어셈블리/에셋 참조의 영향을 확인하고 위 변경 범위 기반 정책으로 남은 관련 검사를 실행하며, 영향 범위를 좁힐 수 없으면 전체를 실행한다. 과거 검증 기록을 소급 삭제하거나 미실행 결과를 PASS로 바꾸지 않는다.

## DEV 표시 정리 (2026-11-05)

- 효과 없는 10칸(0·1·41·43·48·52·53·54·72·73)은 `LabConfig.InactiveDeveloperFields`다. 은퇴가 아니라 숨기기만 한다. 코드가 아직 읽으므로 라벨·저장값·Inspector·폼 전달은 그대로이고, Lab DEV 묶음에 넣지 않으며 Hapio는 `GameStageSettings.IsFieldShown`에서만 뺀다(`IsFieldVisible` 적용 범위는 공통 그대로).
- Hapio 지귀 탭은 폼의 228이 켜져 있으면 178~183·192~195·205를 접는다(`LabConfig.IsReplacedByJigwiTimeline`). Hapio 전투 중 DEV(Lab 패널)도 228 ON이면 `Jigwi` 묶음을 숨기고, 지귀 스테이지에서만 205도 숨긴다(`HiddenByJigwiTimelineInGame`). Lab 단독 장면은 그대로 보인다. 237 오색구슬 칠 면적은 Hapio 공통 탭이다(236은 야광귀 탭). 은퇴 슬롯(#509까지 25개)은 모두 `HideInInspector`다. 설정 259개와 접속 묶음은 바꾸지 않았다(현재 기준은 아래 `Bulk 선택 ACK와 창 64` 절). 상세는 `docs/DEVELOPER_MODE_PARAMETER_SPEC.md`의 같은 절을 따른다.


## #524 스티커별 코팅 광택 v2

- 현재 클레이 표현은 `clay-stickers-v2`다. 높이 R은 기존 변위, GB는 커버리지와 함께 인코딩한 공통 표면 기준 조명 기울기(최대25도), A는 실제 스티커 커버리지다. 완료용 바탕층은 색만 채우며 광택 마스크를 채우지 않는다. Gloss는 기존 공통0~1이고 두께와 독립적이다. 색/R/판정/통신 형식은 유지한다.
- 관람/PNG는 전체 높이 채널을 복사한다. Mac 실험은 `clay-preview-sample-v2`와 Scene에 고정된 메타데이터를 검사하며 구형 EXR를 거절한다. Prepare/Rebake에서만 샘플을 재생성하고 Build는 읽기 전용으로 검사한다. 일반 Scene/원본 모델은 바꾸지 않는다.
- Mac 실험의 높이 EXR 5개(`Sample`, `Rounded`의 `OriginalV2`·`OriginalV3`·`RoundedV3`·`CoatingV4`)는 Default와 같은 RGBA Half·무압축 iOS override를 유지한다. iOS가 활성인 Editor에서 Default만 따르면 RGBM으로 가져와져 R·GB·A가 모두 달라진다. `ClayPreviewBuild.ImportMap`이 쓰고, `ValidateMap`과 PlayMode 검사가 iOS RGBA Half·최대 크기 1024 이상을 확인한다.
- Editor 전용 v1 reference shader 2개는 실제 GPU 결과의 독립 회귀 기준이다. Player 의존성에 넣지 않는다. 최신 main 통합 후 접속 묶음5688/Lab49/Game35·설정259개를 유지했다(현재 기준은 아래 Bulk 선택 ACK 절). #524는 표현 버전만 v2로 바꾸며 이 접속 번호를 되돌리지 않는다. 사용법은 CLAY_STICKER_PAINT, 실제 실행 결과만 VALIDATION에 기록한다.


## #527 둥근 클레이 비교 실험

- `Assets/Experiments/ClayPreview/Rounded/Scenes/RoundedClayPreview.unity`는 원본 #524·기존 윤곽 개선 재질·둥근 윤곽 개선 재질의 Mac 전용 비교다. 일반 Home/Lab 실행 Scene 목록과 게임의 v2 표현 기본값을 변경하지 않는다.
- 표현용 브러시/색 농도는 `LabClayStickerRuntime`의 명시적 생성자에만 주입한다. 게임 기본 생성자는 기존 Resources와 라운드 프로필을 유지한다. 실험 v3 metadata와 원래 v2 게임 기록 형식을 혼동하지 않는다.
- 원본 생성 PNG는 보존하고 Alpha에서 순백색 마스크·20px 둥근 높이를 베이크한다. 같은 seed123·실제 67회 명중 저널의 판정/기록 해시를 세 프리셋에서 유지한다. 기존 v2 샘플을 자동 갱신해 비교 기준을 바꾸지 않는다.
- 준비는 `C6 Lab/Experiments/Rounded Clay/Prepare Imported Comparison`, 빌드는 `Build Mac Comparison App`이다. 준비는 명시적으로만 수행하고 빌드 검증은 샘플을 다시 굽지 않는다. 사용법/한계는 `docs/ROUNDED_CLAY_STICKER_EXPERIMENT.md`, 수행 결과는 `docs/VALIDATION.md`를 따른다.


## #527 네 번째 스티커 코팅 V4 실험

- V4는 별도 `Assets/Experiments/ClayPreview/Rounded/Scenes/CoatingClayPreview.unity`의 네 번째 기본 프리셋이다. 기존 3종 Scene과 `OriginalV2/OriginalV3/RoundedV3`의 색·높이·JSON 파일은 내용·수정 시각까지 보존하며 자동 재베이크하지 않는다. 일반 게임 기본값·Resources·Scene 목록·판정·통신 규약에는 연결하지 않는다.
- 샘플은 `clay-preview-sample-v4` / `clay-stickers-v4`, 높이 계약은 `R:physical-height;GB:oct-object-normal;A:actual-coverage`다. GB는 `A × Oct01(normalOS)`이며 3D 법선을 혼합한 뒤 재인코딩한다. 박의 법선을 중복 가산하지 않는다. 동일 저널의 둥근 V3와 색 RGBA·높이 R·커버리지 A를 유지하고 완료 바탕은 코팅 반사에서 제외한다.
- 준비는 `C6 Lab/Experiments/Rounded Clay/Prepare Coating Comparison`, 빌드는 `Build Coating Mac App`이다. 새 자료는 `Rounded/CoatingV4`에만 추가한다. compositor/surface 소스 SHA256이 바뀌거나 누락되면 명시적인 재준비 전 빌드를 거부하며, 준비 후 새 Scene 메타데이터 연결을 갱신한다. 기존 3종 준비·빌드 메뉴는 유지한다.
- V3/V4 반사 비교는 같은 두께·카메라·조명·Gloss에서 Linear Half RT→Float 차분 `G−0`으로 만들고 PNG 저장 시에만 sRGB로 변환한다. 배율을 숨기거나 LDR clamp로 NaN/Inf를 가리지 않는다. 전체 보기·확대 크롭과 내부 `_SpecularOnly` 출력을 구분한다. 사용법은 `docs/ROUNDED_CLAY_STICKER_EXPERIMENT.md`, 실제 수행 결과만 `docs/VALIDATION.md`에 기록한다.

- #527 이동 광원: 비교 앱의 복제 재질만 `_ClayPreviewLighting=1`로 설정한다. 세 surface 셰이더의 기본0과 일반 게임 설정을 유지하고 전용 Directional Light만 조절한다. 기존 도안을 그대로 두는 surface-only 조명 변경은 명시적 `Refresh Preview Lighting Metadata`로 검증 후 surfaceSHA/Coating Scene 연결만 갱신할 수 있다. compositor·브러시·자료규약 변경은 전체 Prepare가 필요하며 Build는 어느 갱신도 자동 실행하지 않는다.


- #527 팔레트 실험: CoatingClayPreview의 선택적 paletteData만 원래67회 저널을 보관한다. sRGB RGB0~255/HEX6자리 입력은 실험의 색 resolver로만 치환하며 게임 팔레트·설정·판정·기록은 저장하지 않는다. 기본색은 고정지도를 직접 쓰고 변경색은 제한된 재생/GPU 완료 후에만 표시한다. Prepare Palette Preview는 명시적 준비, Build는 읽기 전용 검사다. LIGHT 메타데이터 갱신은 연결된 palette의 V4핀도 검증·복구하며 원본 지도/저널/브러시를 다시 굽지 않는다.


- #543 이미지 파일 실험은 `ImageSamples/Scenes/ImageClayPreview.unity`만 켠다. 표면 질감은 Linear 중립0.5 Half/mipmap 데이터로 실제 스티커 A지원에만, 배경은 sRGB Unlit 카메라 고정 층에 적용한다. PNG/JPEG·2048px·16MiB 사전검사, 소유 결과 교체/해제, 취소·실패의 이전값 보존을 유지한다. 게임 지도·저널·기본 재질을 파일로 교체하지 않는다. Native picker는 `tools/ClayPreviewNative` 소스에서 비교 Mac 빌드 때만 임시 stage하며 일반 빌드에 남은 stage가 있으면 중단한다. 이미지 전용 Prepare/Build는 기존 Coating 메뉴와 구분하고 Build는 샘플·소스 핀을 자동 갱신하지 않는다.

## Bulk 선택 ACK와 창 64 (2026-11-06)

- Guest의 Bulk ACK(`LabBulkEnvelope`)는 누적 `ack`와 함께 `sack`(64비트)을 싣는다. bit i는 `ack + 2 + i` 조각을 버퍼에 가졌다는 뜻이다(`LabBulkReceiver.SelectiveAck`). 데이터그램마다 보내며, ACK가 사라져도 다음 ACK가 같은 상태를 다시 싣는다. NGO는 한 프레임의 메시지를 함께 보내므로 버스트 마지막 프레임의 ACK 패킷이 사라지면 다음 발행까지 소식이 없어 Host RTO가 먼저 터진다. 그래서 Bulk 데이터가 끊긴 첫 프레임에 마지막 ACK를 한 번 더 보낸다(`RepeatBulkAck`).
- `LabBulkSender`는 선택 ACK된 조각을 다시 보내지 않고 그 바이트를 바로 `QueuedBytes`(64 KiB 백로그 판정)에서 뺀다. 순번 창은 그대로 누적 ACK 기준이다. 수신기는 버퍼에 넣은 조각을 지우지 않으므로 되돌림이 없다.
- 손실 판정: 모든 송신에 순번을 매기고, 확인된 조각의 첫 송신보다 앞서 마지막으로 나간 미확인 조각을 바로 다시 보낸다(공유 백오프 없음). 창 안의 모든 구멍에 적용하며, 다시 보낸 뒤에는 그보다 뒤에 보낸 조각이 도착해야 다시 손실로 본다. #437의 중복 ACK 3회 빠른 재전송과 에코 할인은 지웠다. 경로가 순서를 지킨다는 가정이며, 순서가 뒤바뀌면 조각 하나를 한 번 더 보낼 뿐이다.
- RTT 표본은 이번 ACK가 새로 확인한 조각(선택 ACK 포함) 중 가장 최근에 보낸 조각이 한 번만 보낸 조각일 때 잰다(Karn). RTO 식·0.08~1초·시간 초과 공유 백오프는 #437 그대로다. 표본 전 RTO는 0.25초 기준에 같은 공유 백오프가 걸린다(위 #437 절, PR #545).
- 창은 32→64조각(수신 버퍼 128), 공동 스냅샷 조각은 512→4000자다(`LabSessionSettings.SnapshotChunkCharacters`). 문자당 6바이트 escape에서도 한 조각이 Bulk 논리 본문 32,768자 안에 든다는 것을 검사로 고정했다. 선택 ACK 비트가 `AckThrough + 65`까지만 덮으므로 창을 65보다 키우려면 비트맵도 넓혀야 한다.
- 이 절이 들어올 때 호환 묶음은 설정 259개·**5690**·`C6PhysicsLab/51`·**`C6HapioGame/37`**이었다(5689/Lab50/Game36은 `feat/#540` 브랜치가 먼저 썼다). #540이 이 위에 올라간 당시에는 **5691**·`C6PhysicsLab/52`·**`C6HapioGame/38`**이었다. 현재 기준은 #551 절이다. 5690 이하 앱과 혼용하지 않는다. 동작과 대안 비교는 `docs/LOCAL_BOARD_AUTHORITY.md`의 Bulk 절, 실행한 검사·측정은 `docs/VALIDATION.md`에만 기록한다.

## #558 Story 결과 통합과 같은 방 재도전

- #558 머지 당시 호환 묶음은 설정259개·**5691**·`C6PhysicsLab/52`·**`C6HapioGame/39`**였다(현재 기준은 아래 #501 절). Lab/Bulk 형식은 유지하고 Game38 이하와 혼용하지 않는다. `GameFlowState.retryableDefeat`와 결과 이야기 확인 제어가 추가됐다.
- 저장 StageResult/GameOver의 `GameStageStoryPresentation`은 실제 일치하는 Victory/Defeat 복사본만 카드·게이지에 연결한다. 제작용 예시값이나 앞선 성공 결과로 실패를 표시하지 않는다. 로컬 컷·카드 뒤집기는 공유하지 않고 결과/이야기 확인·관람 상태만 Host 진행 조건에 쓴다.
- 정상 Story 패배만 Host가 같은 방·스테이지를 재시도한다. 원래 참가자·P·승인 설정·이전 성공 기록은 유지하고 기존 prepare/start 장벽으로 새 전투를 만든다. 이탈·연결 끊김·로드 실패는 Home만 남기며 오래된 ACK/중복/Guest 요청으로 재도전하지 않는다.
- GameOver 동결 뒤에도 마지막 terminal 발행에서 Bulk 백로그로 빠진 피어에게는 대기열이 줄면 기존 paint+snapshot 트랜잭션을 한 번 큐에 넣는다. 실제 terminal 상태를 헤더에서 추정하거나 게임 시뮬레이션을 재개하지 않는다.
- 실제 연결·에셋·종료 수명은 `docs/STAGE_STORY_UI_INTEGRATION.md`, 과거 제작용 미리보기는 `docs/STAGE_STORY_PRESENTATION.md`, 실행 결과는 `docs/VALIDATION.md`를 따른다.

## #504 배경 재질의 숨은 사본

- 배경 Studio의 바닥·한지 배경·보드 뒤 재질(URP Lit/Unlit)은 숨은 `_MainTex`·`_Color`를 URP가 `_BaseMap`·`_BaseColor`에서 복사하는 값 그대로 저장한다. Editor가 재질 속성을 처음 읽을 때 URP가 다시 복사하고, 결과가 다르면 재질이 dirty가 되어 다음 저장 때 .mat이 바뀐다. 일부 색은 `GetColor`가 float 몇 단계 다르게 돌려주므로 `_Color`에 같은 숫자를 쓰면 안 된다. Editor 스크립트로 바꿀 때도 `LabBackgroundStudio.ApplyImage`(Editor 전용)를 쓴다. `GameStageBackgroundAssetTests`가 재질 파일을 읽어 확인한다. 상세는 `docs/STAGE_BACKGROUNDS.md`를 따른다.

## #501 로비 재입장과 #502 전투 중 백그라운드

- Hapio 로비 게스트가 끊기면 같은 방·같은 P번호로 다시 들어간다(`LabNetwork.LobbyRejoin.cs`). 게스트는 방마다 무작위 표(16진수 32자리, 저장 안 함)를 연결 토큰 뒤에 붙이고(`token|ticket`, 게임 방만), 방장은 나가기 신호(`lobby_leave`, 또는 전투 중 `exit` → `MarkLobbyLeaver`) 없이 끊긴 로비 게스트의 자리와 P를 90초(`LobbySeatHoldSeconds`) 맡아 같은 표에 돌려준다. 입장시킨 뒤 패킷을 한 번도 받지 못한 게스트(`heardLobbyMembers`)는 맡지 않는다. 맡아 둔 자리는 옛 연결 id로 `seats`에 남고 lobby 패킷의 `ids`가 맡아 둔 자리를 알린다(새 필드 없음). 정원·Bonjour 인원에는 세고 `ParticipantCount`·시작 조건에는 세지 않는다.
- 게스트는 끊기거나, iPhone이 전송 제한 시간 이상 멈췄다 돌아오면(벽시계) `IsJoiningRoom`을 유지한 채 30초(`LobbyRejoinBudgetSeconds`, 진행 중인 시도도 끊음) 동안 다시 시도하고, 끝내 안 되면 #471 경로로 방 목록에 간다. 방장이 방을 닫으면(`lobby_closed`) 다시 들어가지 않는다. `GameSession`이 로비 단계에서만 `LobbyRejoinAllowed`를 켠다. 표를 가진 방은 정원이 차 보여도 방 목록에서 들어갈 수 있고(`HoldsRoomTicket`, 판단은 방장의 승인), 나가기 신호를 보낸 게스트는 표를 버린다. 승인 대기 중인 연결 중 자기 자리를 되찾는 것은 새 자리로 세지 않는다(`PendingNewPlaces`).
- 전투 중 백그라운드(#502)는 현재 동작 유지로 정했다. 신호·일시정지 제안·표시는 없고, 30초 뒤 끊기면 기존 게임 오버·2:2 로비 복귀를 따른다. 전투 중에는 재입장을 하지 않으며 방장 앱의 백그라운드와 방장 소켓 재생성도 다루지 않는다.
- 현재 호환 묶음은 설정259개·**5697**·`C6PhysicsLab/58`·**`C6HapioGame/45`**다(프로토콜·Lab 토큰은 #551 그대로, Game만 #501로 44에서 올림). Lab 방의 토큰·패킷은 #501로 바뀌지 않았다. Game44 이하와 혼용하지 않는다. 동작은 `docs/GAME_FLOW.md`의 같은 항목, 실행한 검사는 `docs/VALIDATION.md`만 따른다.

## #498 전투 첫 사용 렌더링

- `LabBattleRenderWarmup`은 준비 화면 아래에서 비공개 복사본만 그린다. 실제 보드·투사체 루트·명중 풀을 쓰지 않고, `GameSession.Start`의 `initial_ready` 조건으로만 기다린다(`IsPresentationReady`에 넣지 않는다). 첫 사용 비용이 있는 새 그림을 추가하면 여기에 실제 카메라·레이어로 함께 넣는다. 실기기 효과는 `docs/VALIDATION.md`에서 측정 여부를 구분한다.

## 멀티 완료 연출 준비 판정

- `GameSession.Finish`의 최종 칠 일치 판정은 기기마다 다른 `LabPaintSurface.VisualVersion`이 아니라 모든 기기가 같은 `LastVisualSequence`(`PaintFinishControl.visualSequence`)로 한다. `VisualVersion`은 같은 기기 안의 변경 감지에만 쓰고 기기 사이에 비교하지 않는다.
- 이 수정 당시 Game 토큰은 46이었다(현재 기준은 아래 스테이지 박 크기 절).

## 스토리 스테이지 박 크기

- 1~3스테이지 박과 판정 껍데기는 Gourd Visual Size(DEV 140) 값에 더해 `LabGourdVisualRig.StageSizeMultiplier` 1.4배로 그린다(실제 화면 시안으로 승인). `LabGourdPaint`가 rig 기준 크기를 이 값으로 나눠 쓰므로 rig가 없는 흰 박(튜토리얼·2:2·Lab)과 그 골든은 바뀌지 않는다. 다시 굽지 않으며 프리팹의 `referenceVisualSize` 1.4도 그대로다.
- 박은 줄 쪽 기준점을 유지한 채 아래로 커진다. 정지 자세의 실제 바닥은 땅 위지만 줄 흔들림의 아래쪽에서는 잠깐 땅과 겹칠 수 있다.
- 당시 호환 묶음은 설정259개·5697·`C6PhysicsLab/58`·**`C6HapioGame/47`**였다(현재 기준은 아래 #647 절). 판정 모양이 달라져 Game46 이하 앱과 혼용하지 않는다.
- #595 지귀 연기 흐름(round.smoke / round.common)으로 호환 묶음이 설정259개·**5698**·`C6PhysicsLab/59`·**`C6HapioGame/48`**이 됐다(현재 기준은 아래 #647 절). 5697 이하 앱과 혼용하지 않는다.

## 시안에 없는 안내 줄

- StageResult 이야기 화면의 `Status`와 Home·Lobby의 `ConnectionMessage`는 승인된 시안에 없어 실행 중 숨긴다. 프리팹을 지우지 않는다. 위 #471 등의 "안내 표시" 문장은 이 줄에 쓰던 당시 동작이며 지금은 화면에 나오지 않는다. `GameLobbyView.message.text`는 계속 계산한다. Direct IP 창의 안내 문구는 유지한다.

## 플레이어별 칠 무늬 기본 OFF

- `Player Paint Patterns`(DEV 인덱스137, `LabConfig.playerPaintPatternsEnabled`)의 코드·저장 기본값은 꺼짐이다. 박 표면의 P번호별 무늬와 플레이어 원 오른쪽 아래·로비 P 자리의 무늬 표시가 함께 꺼진다. 색·면적·P지도·승리 판정은 그대로다. 기능 코드와 DEV 행·인덱스는 남겨 DEV로 다시 켤 수 있다. config가 없을 때도 꺼짐으로 본다. 설정 259개와 접속 묶음은 바꾸지 않았다.


## #642 봉인 총평의 실제 도색 기여도

- 스테이지별 봉인 시간은 Host 최종 성공 snapshot의 `playedSeconds`, 총 소요 시간은 성공한1~3판의 원본 전투 시간 합이다. 실패 시도·로딩·후일담은 합산하지 않고 누락 판은 `—`로 표시한다. 총평 전용 `GameSession.SealStageResults`가 스테이지별 누락 자리를 유지하고, `ResultLocalPlayerId`는 완료 후 연결이 끝나도 개인 식별을 보존한다.
- 내 기여도는 Host의 `LabPlayerState.paintedCellsTotal`(이번 라운드 누적 신규 동일 면적 셀 수) 중 내 비율이다. `LabPaintSurface.PaintFundedCell`의 실제 빈 칸→칠한 칸 변경에서 최종 공급 `Ink.Owner`에게만 한 번 적립한다. 초기·성장·분수 예산을 포함하며 덮어쓰기·미사용 예산·시각 여백은 제외한다. 건조로 차감하지 않고 마른 칸 재칠은 다시 더한다. 기존 공격 점수·땅따먹기 소유 면적·난수/예산/색/P지도·승패는 변경하지 않는다.
- 통계는 기존 플레이어 snapshot/헤더/승인 경로로 Host가 보내고 Guest는 임의로 집계하지 않는다. 호환 묶음은 **설정259개·5699·`C6PhysicsLab/60`·`C6HapioGame/50`**이며 Game49는 별도 #647이 사용해 건너뛴 값이다. 구현과 검증 범위는 `docs/STAGE_STORY_UI_INTEGRATION.md`·`docs/VALIDATION.md`를 따른다.

## #647 스테이지 클리어 방장 진행

- 각 스테이지 마지막 컷(`*_After_02`)의 `StoryStageClearControls`에서 방장만 진행 버튼을 누른다. 구미호·야광귀는 방장·구성원 모두 "다음 요괴 잡으러 ..."를 보고, 방장은 "다음 봉인", 구성원은 "방장의 시작을 기다리시오."를 본다. 지귀는 "다음 요괴 잡으러 ..."를 아무에게도 보이지 않고 방장 버튼은 "마치기"다. 프리팹은 `Hapio/Story/Update Stage Clear Host Controls` 도구로만 다시 저장한다. 마지막 컷에는 하단 완성 박 보기·나가기 버튼을 두지 않는다(승인된 시안). 완성 박은 봉인 카드의 "봉인 박 둘러보기"로 연다.
- 지귀의 "다음 봉인"은 `GameSession.StartSummary`로 결과 제어 패킷의 `summaryStarted`를 켜고, 모든 기기가 봉인 총평(#646)과 엔딩으로 간다. 최종 결과에서 끊긴 구성원의 읽는 중·관람 중 표시는 지운다. 방장이 총평 전에 끊기면 구성원은 `HostLostAfterClear`로 스스로 총평·엔딩으로 넘어간다.
- 현재 호환 묶음은 설정259개·**5699**·`C6PhysicsLab/60`·**`C6HapioGame/51`**다(#642의 Game50 위). Game50 이하 앱과 혼용하지 않는다.

## 땅 아래 박과 관람 확대 잘림

- Hapio 전투의 `GameGourdOverGround`는 땅이 가린 박의 아래쪽을 다시 그리는 표현 전용 단계다. 렌더링 레이어 비트 20(땅)·21(박)과 스텐실 비트 64를 쓰므로 다른 기능에서 이 값을 쓰지 않는다. 박의 높이·움직임·크기, 카메라, 배경, 렌더러 에셋은 이 문제로 바꾸지 않는다. 상세는 `docs/PINATA_TUNING_GUIDE.md`의 같은 절을 따른다.
- 봉인 박 관람은 `ExtendFrame(1.7, 2.8)`으로 시야만 넓혀 같은 크기로 사각형 밖까지 그린다. 회전 제스처는 기존 사각형이 받는다. 상세는 `docs/SEALED_GOURD_HELP_UI.md`를 따른다.


## #659 마지막 일격·오색 클레이 VFX 통일

- 마지막 일격은 전투와 같은 클레이 메시·재질·프로필을 전용 수동 시계 풀로 그린다. 전용 풀은 Scene 서비스 등록을 덮어쓰거나 실제 전투 풀을 정리하지 않는다. 기록·공유 시간표·물리·판정·통계는 바꾸지 않는다. 각도 전환/Reset은 전용 효과를 지우고 Skip은 기존처럼 현재 프레임을 보존한다.
- 오색의 공통 클레이 비행·눌림·진흙 접촉과 색 흐름은 유지하고, 예전 명중 먼지·별가루 중첩은 제거한다. 실제 오색 투사체의 기본 TrailRenderer 대체 경로도 일반 구슬의 폭·수명·곡선을 쓰며 오색 색상만 적용한다. 지귀 부적/물·보상 불꽃·보드의 오색 그림과 피버 UI·음악·명중 알림은 유지한다.
- 상세 동작과 한계는 `docs/FINAL_HIT_REPLAY.md`·`docs/FIVE_ELEMENT_VFX.md`, 실제 검사는 `docs/VALIDATION.md`를 따른다. #659는 표현만 바꾸며 #647의 설정259개·5699/Lab60/Game51을 유지한다.

- #659 후속 오색 강화: 승인된 오색 전용 아트값을 `FiveElements/Profile`에 둔다(기본ON, 꼬리2.4D/폭1.4, 퍼짐 선형1.35, 큰5색 방울5개·저품질3). 일반 구슬·실제 반경·물리·도색·기여도·통신은 유지한다. 비교 Scene의 현재/강화 오색 버튼은 풀의 임시 override만 바꾸고 Resources를 저장하지 않는다. 오색 Wet shader 실패는 같은 메시의 Unlit 간소 표현을 사용하며 다시보기에서 구형 먼지·별을 되살리지 않는다. 실제 실행 결과는 VALIDATION의 후속 절을 따른다.

- #659 별 파열 후속: 강화 오색 명중에만 기존 `FiveColorSparkle.png`를 쓰는10개/0.45초 별을 더한다(저품질5개, Inspector ON/OFF·개수·크기·거리·시간). `LabClayImpactSlot`의 Age·seed와 공유 메시/재질로 재생하며 구형 독립 `Burst`·Dust는 복구하지 않는다. 일반/현재 비교는 별 없이 유지하고 준비 화면·마지막 일격도 같은 시계를 사용한다. 판정·통신은 유지한다.


## 인원별 스토리 도색량 (#676)

- DEV105/106의 저장값1.3%/1.0%는 5인 기준이다. 일반 스토리2~5인은 `LabConfig.CapturePaintGrowthSettings`에서5/실제 인원을 한 번 곱한 사본을 Rules가 Begin 때 고정한다. 솔로·튜토리얼/LabNone·땅따먹기는 그대로다. 원본/공유 배열을 유효값으로 덮거나 성장·건조·보상 개수까지 보정하지 않는다. DEV 안내는 기준과 현재 인원 적용값을 구분한다.
- 설정259개·5701/Lab61/Game52가 현재 접속 묶음이다.5700 이하 앱과 혼용하지 않는다. 이전 #676 공통 기본값만 조정한 빌드는 인원 보정이 없으므로 후속 빌드와 구분한다. 실제 검사 결과는 VALIDATION에 기록한다.
- #676 병합 당시 묶음이 위의 설정259개·5701/Lab61/Game52다. #664(구미호 협동 기믹, 설정265개)도 자기 브랜치에서 같은 번호를 썼다. 그래서 둘을 합친 현재 접속 묶음은 **설정265개·5702·`C6PhysicsLab/62`·`C6HapioGame/53`**이다. 5701 이하 앱과 혼용하지 않는다.


## #687 스토리 준비 대기와 그만두기
- 마지막 스테이지 그림의 다음 봉인/마치기는 모든 참여자의 명시적 준비다. 읽기 종료만으로 준비시키거나 방장 클릭 한 번으로 다음 스테이지를 자동 시작하지 않는다. 전원 준비·결과 동기화·관람/연출 종료 뒤 별도 방장 시작 버튼으로 진행한다.
- 대기는 기존 GameLoadingArt 5종을 사용하며 같은 결과 명단/공통 순열로 기기마다 다른 캐릭터를 배정한다. 그만두기 확인은 Home 방 목록/방 만들기로 복귀하고 취소는 준비 상태를 보존한다.
- 현재 이 변경의 접속 묶음은 설정265개·5702/Lab62/**Game54**다. 실행 검사는 docs/VALIDATION.md, 상세는 docs/STAGE_STORY_UI_INTEGRATION.md를 따른다.


## #706 야광귀 3인 시연 빌드

- 일반 Story `[1,2,3]`와 시연 `[2]`는 `GameRunProfile`로 선택한다. 시연도 실제 Stage2/yaguang/Yaguang 설정·리소스·결과 키를 유지하며 진행 위치1/1과 구분한다. `stageIndex <= totalStages`나 마지막 번호=스테이지 수로 판단하는 분기를 다시 만들지 않는다. 원본3개 StageCatalog와 일반 Scene을 보존한다.
- 시연 여부는 전용 빌드의 `HAPIO_YAGUANG_DEMO`이며 Development 여부/EditorPrefs로 콘텐츠를 고르지 않는다.3인 Story만 생성/입장/시작하고 튜토리얼·솔로·팀전을 열지 않는다. 일반 Game54/Lab62·5702·265개 설정은 유지하고 시연의 토큰 `C6HapioDemo/1`과 Game Bonjour `C6-HAPIO-DEMO-1-5702`를 함께 구분한다.
- 시연은 메인 스토리→방 목록→3인 야광귀→해당 후일담→전원 준비·방장 총평→실제Stage2만의 총평→엔딩6컷→타이틀이다. 일반 엔딩/총평을 덮어쓰지 않고 `Resources/Hapio/Demo/Yaguang`의 사본을 사용한다. 준비는 `Hapio/Demo/Update Yaguang Demo Stories`의 명시적 동작이고 빌드는 이를 자동 재생성하지 않는다.
- 전용 Release/Development QA 메뉴와 출력·앱ID를 구분하고 빌드 임시 설정/플랫폼/Scene 목록을 복원한다. 홈 복귀는 연결 종료가 끝날 때까지 재입장 전이를 막아 타이틀/방목록 목적지를 유지한다. 사용법은 `docs/YAGUANG_DEMO.md`, 실제 검증은 `docs/VALIDATION.md`를 따른다.
