# 합이오 개발용 3요괴 게임 흐름 — #276

기존 `Assets/Lab/Scenes/Lab.unity`는 실험·회귀 검사용으로 유지한다. 새 게임은 `Assets/Hapio`에 별도 저장된 Scene·Canvas·영속 세션 Prefab을 사용한다. 아직 배포용 설정으로 전환하지 않았으며 기존 Lab 빌드 메뉴·앱 식별자·공유 설정 원본을 교체하지 않는다.

## 실행과 빌드

1. Unity에서 `Hapio > Open Home`을 선택하거나 `Assets/Hapio/Scenes/Home.unity`를 연다.
2. Play 후 Home에서 방을 만들거나 발견된 **Hapio-…** 방에 참가한다. 기존과 같이 같은 LAN·핫스팟에서 연결하며 Direct IP도 사용할 수 있다.
3. 로비에서 각자 고유한 P번호를 선택한다. 목표 인원·자리 선택·설정 확인이 완료되면 Host가 START를 누른다.
4. 1·2스테이지의 결과에서 Host가 NEXT를 눌러 전원이 다음 스테이지로 이동한다. 3스테이지 결과에서는 각자 HOME으로 돌아간다.

빠른 로컬 확인은 Home의 개발자 SOLO 버튼으로 가능하다. 일반 방은 기존 2~5명 연결 규칙을 사용한다. 새 게임과 구형 Lab은 연결 승인 토큰이 다르므로 같은 방에서 섞어 사용할 수 없다. 새 게임 방 이름은 `Hapio-`로 시작한다. Bonjour 서비스 자체는 공유하므로 검색 목록에 구형 Lab 방이 보일 수 있으나 입장은 거절된다.

새 게임 빌드는 `Hapio > Development Build > Build Mac App / Export iOS Xcode Project`를 사용한다. 새 Scene 5개만 포함하고 Home이 첫 진입점이다. 결과물은 프로젝트의 `Builds/Hapio/<날짜-고유번호>/`에 생성하며 이전 결과물을 덮어쓰지 않는다. 항상 Development Build다. 기본 Build Settings는 기존 Lab 첫 항목을 유지하고 새 Scene을 뒤에 추가하므로 일반 Unity Build 버튼 대신 위 전용 메뉴를 사용한다. 이번 통합은 #262의 흐름을 계승하되 main의 기본 진입점이나 기존 Lab 파일 경로를 바꾸지 않는다.

## Scene의 책임

| Scene | 저장된 구성과 역할 |
| --- | --- |
| Home | 방 생성·검색·Direct IP, 인원 조정, DEV 설정, 개발자 Solo |
| Lobby | 방 이름·참가자·P번호 선택, Host START, 로비 나가기, Host DEV 설정 |
| Battle | 기존 Lab 전투를 복사한 독립 Scene. **1~3스테이지 모두 같은 경로를 로드**한다. 전투 UI·카메라·구슬·박·일시정지·기존 DEV 조작을 재사용한다. |
| StageResult | 스테이지별 기록, 완성 박 관람, Host NEXT, 중도 QUIT RUN 또는 최종 HOME |
| GameOver | 종료 사유와 HOME. 자동으로 홈으로 사라지지 않는다. |

모든 새 메뉴의 Canvas·버튼·Text·슬라이더·입력·안전 영역은 저장된 GameObject다. `GameScreen`은 참조를 읽어 값과 버튼 동작만 연결한다. `GameSceneBuilder.CreateMissing`은 없는 에셋만 Unity API로 생성하며 이미 편집한 새 Scene을 재생성하지 않는다. 구형 Lab Scene은 저장하지 않는다.

이번 main 동기화에서는 최신 Lab의 3D 배경·미니맵 배치를 별도 Battle에 반영했다. Battle은 저장된 복사본이므로 이후 Lab Scene 수정이 자동으로 전달되지는 않는다. `Hapio > Upgrade Existing Game Scenes From Current Lab`은 명시적인 동기화 도구이며 Battle의 전투 구성을 다시 복사한다. Battle 전용 수정은 먼저 차이를 확인해 반영한다. 같은 도구의 Home/Lobby 설정 확장은 기존 객체와 배치를 유지하면서 새 DEV 행을 추가한다. 일반 Play·빌드는 이 도구를 자동 호출하지 않는다.

## 세션과 전투의 수명

`Resources/Hapio/GameSession.prefab`의 `GameSession`·`LabNetwork`·NGO·DualStackTransport가 방 연결과 진행 데이터를 유지한다. Scene 로딩은 NGO의 자동 Scene 전환 대신 Host가 승인한 작은 진행 메시지와 각 기기의 로컬 Scene 로드를 사용한다.

- `GameRunFlow`가 `runId`, `stageIndex`(1~3), 상태, revision, 종료 사유를 관리한다.
- 기존 전투의 `sessionId/round`는 별개다. 매 스테이지 새 전투 ID로 이전 판의 구슬·전달·투척 요청을 차단한다.
- `GameBattleBindings`가 새 Battle의 객체를 영속 Network에 연결한다. 이 연결은 기존 전투 초기화 코드보다 먼저 이뤄진다. Scene을 떠날 때 해당 참조·입력 구독·투사체를 정리한다.
- 전원 Battle 로딩 완료 + 최신 설정 확인 후 Host가 전투를 시작한다. 이전 스테이지의 로딩 ACK는 다음 스테이지에 사용할 수 없다.
- 첫 스테이지 준비부터 홈 복귀 전까지 P번호 변경과 새 참가 승인을 잠근다. Scene 로딩 중 전투 모델이 잠시 없어도 이 잠금은 유지하며, 새 방에서는 해제한다.
- 결과도 각 기기가 실제 결과·완성 도색 사본을 보관하고 StageResult를 로드한 뒤 ACK한다. Host NEXT와 최종 HOME은 이 준비를 기다린다.
- 로딩/결과 동기화 대기 한도는 `GameSession.loadingTimeoutSeconds`(Prefab Inspector, 기본 30초)다. 중간 단계 동기화 실패는 게임 오버로 종료한다. 이미 확정된 최종 클리어는 유지하며 관람 사본을 확보하지 못했다는 안내와 함께 최종 결과로 이동한다. 없는 도색을 만들어내지 않는다.

## 스테이지 경계

방 연결·참가자·고유 P번호·좌우 순서·승인된 설정은 유지한다. 매 스테이지는 새 모델로 제한시간·개인 HP·스태미나를 설정 시작값으로 복원하고 구슬·북 배치·투사체·도색·흔들림·콤보·방해 이벤트·통계를 초기화한다. 스테이지별 규칙·DEV 설정은 아래 카탈로그를 따른다. 누적 점수는 추가하지 않는다.

결과는 전투의 실제 종료 스냅샷을 별도 복사한다. 완성 박은 기존 관람 복제 기능을 사용해 텍스처·재질·렌더 카메라를 독립 보관한 뒤 Battle을 언로드한다. 관람에서 돌아온 뒤 NEXT를 사용할 수 있고, Host가 NEXT를 누르면 다른 참가자의 관람도 닫힌다. 다음 스테이지/홈 복귀 때 사본을 해제한다.

## 종료·정지·DEV

- 진행 중(중간 결과 포함) 나가기, 참가자 이탈, Host 연결 상실 → 전원 게임 오버. Host 상실은 각 기기가 실제 연결 종료를 감지해 처리한다.
- 시간 초과·전원 개인 HP 0 → 즉시 게임 오버. 전투 패배 결과나 자동 Retry로 진행하지 않는다.
- 로비에서 나가기는 홈으로 복귀한다. 최종 클리어 뒤 정상 종료는 남아 있는 참가자의 클리어 결과를 게임 오버로 바꾸지 않는다.
- 게임 오버 상태와 종료 사유는 홈 입력까지 유지한다. 늦게 도착한 전투 스냅샷으로 다시 전투에 진입하지 않는다.
- 상태바/제어센터를 여는 동작 자체를 이탈로 추가하지 않는다. 기존 실제 백그라운드·연결 종료 동작은 별도다.
- 공동 일시정지와 Host 재개를 유지한다. 정지 메뉴의 QUIT RUN은 전원 게임 오버 경로다.
- Home/Lobby의 DEV 조정과 Battle의 기존 DEV를 사용한다. Host가 정지 중 설정을 적용하면 **현재 스테이지 번호를 유지**하고 새 Battle 인스턴스를 로드한다. 새 설정의 ACK와 전원 로딩 후 재시작한다.

## 변경 위치와 검증

새 코드: `Assets/Hapio/Scripts/Core/{GameSession,GameRunFlow,GameFlowState,GameFlowSnapshot,GameScenePaths}.cs`, `Scripts/UI`, `Editor/GameSceneBuilder.cs`, `Editor/GameBuild.cs`.

기존 코드의 연결부: `LabNetwork.GameFlow.cs`와 `LabNetwork.cs`의 opt-in 분기. 기본값은 OFF이므로 기존 Lab은 종전 연결 토큰·단판 동작을 사용한다. 공유 설정은 **196개·프로토콜5660**다. 기존0~191 인덱스는 유지하고 지귀 자동 패턴4칸을192~195에 추가한다. 새 게임 연결 토큰 `C6HapioGame/3`와 단일 Lab의 `C6PhysicsLab/21`은 분리한다. 서로 다른 규약의 빌드를 혼용하지 않는다.

검사 코드는 `Assets/Hapio/Tests`와 `LabGameFlowBridgeTests`다. 실제 실행 결과는 `VALIDATION.md`의 #262 절을 참조한다. Scene 흐름 검사의 승리 유도는 CPU 도색 완료용 fixture이며 실제 투척 명중을 검증했다고 해석하지 않는다. 기존 투척·도색 검사는 별도로 유지한다.

## 브랜치 운영

#262의 Draft 이력은 #276으로 계승한다. 기존 main/Lab을 유지하며 이 통합은 별도 기능 브랜치에서 검증한다. Lab 폴더 이동·프로젝트 이름 변경·Shared 폴더로의 일괄 이전은 하지 않는다. #275의 로비 UI/방 구조 변경은 호환성 보완 전까지 포함하지 않는다.

## 스테이지 카탈로그와 DEV 범위

`Assets/Hapio/Resources/Hapio/StageCatalog.asset`이 순서를 고정한다. 각 StageDefinition은 요괴 종류·고유 ID·표시명·고유 기본값만 가진다. 공통 수치는 원본 `LabConfig.asset`에서 읽고 실행 중 복사본으로 관리한다.

| 스테이지 | 전용 패턴 | 공통 패턴 | 표시 |
| --- | --- | --- | --- |
| 1 구미호 | 기존 여우불·매혹 가중 추첨 | 박 폭탄·좌우 전달 막기 | 기존 구미호 FBX를 박 내부 장식 슬롯에 연결 |
| 2 야광귀 | 기존 색상 콤보·셈법·섬광, 화면별 이벤트 차례/휴식 | 박 폭탄·좌우 전달 막기 | 기존 박 내부 요괴 외형, 스테이지명과 패턴으로 구분 |
| 3 지귀 | 소화 도전·생성 화상 자동 추첨, 기존 DEV 수동 발동 유지 | 박 폭탄·좌우 전달 막기 | 기존 박 내부 요괴 외형, 스테이지명·불길로 구분 |

야광귀·지귀 전용 3D 모델은 이번 범위에 없다. 공통 WhiteGourd의 도색 메시·충돌체·밧줄·회전·완성 박 관람은 그대로 사용한다. 구미호는 장식만 교체하고 게임 판정 대상을 대체하지 않는다.

Home/Lobby DEV의 `공통 / 구미호 / 야광귀 / 지귀`에서 해당 값만 편집한다. 공통은 시간·개인HP·구슬·물리·도색·폭탄·막기 등 모든 스테이지에 공유한다. 전용 값은 해당 스테이지만 바뀐다. 기본값을 초기화할 때만 StageDefinition의 토글을 적용하며 매 NEXT/재시작마다 사용자 OFF를 다시 ON으로 덮지 않는다.

- 구미호: 기존 Sabotage 고유 수치166~177. 폭탄·막기 추첨 가중치169/170은 스테이지에서 사용하지 않아 숨긴다. 공통 폭탄/막기는 독립 규칙으로 유지한다.
- 야광귀: 콤보155~164, 섬광184~190, 셈법191. 최초 기본값은 콤보·섬광·셈법 ON이다.
- 지귀: 수동 패턴178~183, 자동 간격192/193(기본20~30초), 소화/생성 화상 가중치194/195(기본1:1). 가중치0은 해당 자동 선택에서 제외하고 둘 다0이면 지귀 자동만 멈춘다. 공통 패턴은 계속된다.

새 Scene·카탈로그 생성/명시적 갱신은 `Hapio > Upgrade Three Yokai Stages`를 사용한다. 이 도구는 별도 Battle을 현재 저장 Lab에서 복사하므로 Battle 전용 수동 수정이 있다면 먼저 차이를 확인한다. 일반 Play/빌드에서는 자동 실행하지 않는다.

## 검증 상태

이번 #276의 실제 실행 결과는 `VALIDATION.md`의 별도 기록을 따른다. #262·#270·#274의 과거 PASS는 새 3요괴 흐름의 검증으로 승계하지 않는다. 실제 iPhone/iPad·LAN·핫스팟은 수행하지 않았다면 NOT_RUN이다.
