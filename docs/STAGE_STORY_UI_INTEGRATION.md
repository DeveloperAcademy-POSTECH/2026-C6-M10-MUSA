# 스테이지 스토리·전투 UI 통합 — #558 / 결과 흐름 정리 #566

이 문서는 #556의 저장 전투 UI와 #553의 스토리 부품을 일반 Hapio 게임 흐름에 연결한 범위와 변경 지점을 설명한다. 스토리 제작용 미리보기의 예시 데이터와 실제 경기 결과를 구분한다. 실행 결과·통과 건수는 이 문서에 기록하지 않으며 [VALIDATION](VALIDATION.md)을 따른다.

## 적용 범위와 실행

Unity의 `Hapio > Open Home` → Play에서 기존 방 생성/입장·P번호 선택을 사용한다. 일반 Story는 같은 Battle Scene으로 1~3스테이지를 진행한다. 오프닝·Home/Lobby·튜토리얼·LAN/핫스팟 연결·구슬/도색 판정·DEV 규칙은 기존 경로를 유지한다. 실행/빌드는 [GAME_FLOW](GAME_FLOW.md)를 따른다.

- **Story 승리:** 확정된 Victory와 해당 스테이지의 실제 기록 → 봉인 카드 1회·후일담 → 마지막 컷을 유지한 결과 대기/관람 → Host의 다음 스테이지.
- **최종 Story 승리:** 지귀 카드 1회·후일담 → 봉인 총평(또는 전체 기록 버튼으로 동일 총평)·완성 박 관람 → 총평의 이야기 계속 → 구름 → 각자 엔딩 → 각자 Home.
- **정상 Story 패배:** 확정된 Defeat → 실패 이야기 → 도달 봉인율 대시보드 → Host의 같은 스테이지 재도전 또는 나가기.
- **나가기·연결 끊김·로드 실패:** 스토리 패배 재도전으로 해석하지 않는다. 종료 사유와 Home 경로를 유지한다.
- **Territory/튜토리얼:** 이번 Story 어댑터를 사용하지 않는다. Territory의 기존 단판 결과·같은 방 복귀 API와 튜토리얼 종료 분기를 유지한다.

이야기와 카드 뒤집기는 각 기기의 로컬 표시다. Host가 컷 번호·페이지·뒤집기 각도를 동기화하지 않는다. 실제 결과 수신과 로컬 이야기 완료 여부만 진행 권한에 연결한다.

## 저장된 연결

| 파일/구성 | 책임 |
| --- | --- |
| `Assets/Hapio/Scenes/StageResult.unity` / `GameOver.unity` | 기존 결과 UI를 보존하고 새 어댑터·스토리 Canvas 참조를 저장 |
| `Assets/Hapio/Art/UI/GameStageStoryCanvas.prefab` | 게임 전용 StoryPlayer·터치 표면·402×874 그림과 같은 좌표계의 단일 진행/관람/나가기/전체 기록 버튼 |
| `Assets/Hapio/Scripts/UI/GameStageStoryPresentation.cs` | 결과와 스토리 연결, 경기 기록 바인딩, 이야기 확인·관람·재도전/엔딩 UI 수명 |
| `Assets/Hapio/Scripts/UI/GameResultActionLayout.cs` | 같은 PageFit 안에서 상태별 버튼 행·Safe Area 하단 여백·실패 안내 위치를 계산 |
| `Assets/Hapio/Scripts/UI/GameScreen.cs` | 기존 결과 입력 보호와 버튼 권한 유지, 어댑터/관람/전체 기록으로 연결 |
| `Assets/Hapio/Story/StageStories/AfterGumiho.asset`·`AfterYagwanggui.asset`·`AfterJigwi.asset` | 스테이지별 카드·후일담 목록 |
| `Assets/Hapio/Story/StageStories/Failure.asset` | 첫 실패 장면·실패 대시보드 |
| `Assets/Hapio/Story/StageStories/GameEnding.asset` | 봉인 총평1컷 + 실제 게임용 엔딩6컷. 제작용 `Ending.asset`의 Figma 타이틀 컷은 포함하지 않음 |
| `Assets/Hapio/Scripts/Story/StoryPlayer.cs` / `StoryCardRecords.cs` / `StorySealGauge.cs` | 기존 컷 재생·카드 기록·실패 게이지 부품 재사용 |
| `Assets/Hapio/Editor/GameStageStoryUpgrade.cs` | 명시적 저장 에셋 연결 메뉴. Play/빌드에서 자동 재생성하지 않음 |

저장 연결을 복구할 때는 Play를 멈추고 미저장 Scene을 먼저 보존한 뒤 `Hapio > Story > Connect Stage Stories To Game Results`를 실행한다. 기존 Canvas/게임용 엔딩은 재생성하지 않는다. 게임 Canvas의 기존 버튼을 재사용하고 필요한 관람 버튼·화면 맞춤 연결만 업그레이드한다. Battle Scene과 일반 빌드 Scene 목록을 교체하는 도구가 아니다. 제작용 미리보기의 편집 위치와 효과음은 [STAGE_STORY_PRESENTATION](STAGE_STORY_PRESENTATION.md)을 참고한다.

## 실제 경기 데이터와 표시 순서

`GameSession`이 종료 스냅샷을 JSON 복사해 별도 결과로 보관한다. `GameStageStoryPresentation`은 run·스테이지·전투ID·라운드·결과 phase가 맞는 데이터만 사용한다. 결과 상태가 먼저 도착했어도 스냅샷 없이 예시78%나 이전 스테이지의 기록을 표시하지 않으며 결과 기록 수신 안내를 유지한다.

- 봉인 카드의 봉인율·남은 시간은 해당 Victory 스냅샷에서 읽는다.
- 카드 기록은 `LabResultStats.Awards`의 딜왕·명중왕·구슬패스왕·북패스왕·북독점왕을 기존 `StoryCardRecords.Record` 순서로 바인딩한다.
- 실패 게이지는 해당 Defeat의 실제 `paintCoverage`를 `StorySealGauge.Show`에 전달한다. 표시 애니메이션이 실제 판정 지도를 변경하지 않는다.
- StageResults에는 성공한 스테이지만 추가한다. 실패 시도는 전체 성공 기록을 덮어쓰지 않는다.
- 완성 박은 기존 결과 관람 사본을 사용한다. 실패 결과에는 완성 박을 만들거나 이전 성공의 박을 재사용하지 않는다.
- 최종 전체 기록은 기존 전체 결과 계산과 UI를 사용하고 떠났던 결과 화면으로 돌아올 수 있다.

StoryPlayer는 스킵/완료 이벤트를 어댑터에 알린다. #566에서는 정상 후일담 완료 시 마지막 `CurrentView`를 그대로 유지한다. 승리 카드·후일담을 SKIP하면 현재 컷의 같은 인스턴스를 완료 자세로 정지 표시하며, 카드·대사·등장 효과음을 다시 재생하지 않는다. 빠른 SKIP에서도 들어오는 중의 반투명/부분 자세에 멈추지 않도록 조용한 정지 샘플을 쓴다. 원본 제작용 미리보기의 재생·AUTO는 유지한다.

실패 첫 대사를 SKIP할 때는 기존처럼 최종 봉인율 대시보드로 한 번 이동한다. 실패 결과의 원본 재도전·나가기 버튼을 쓰기 위한 경로이며, 승리 봉인 카드를 되돌리는 동작과 다르다. 실패 대시보드의 게이지가 차는 동안도 Host 재도전은 다른 조건이 충족되면 허용된다. 다음 스테이지 자동 진행과 새로운 고정 대기는 추가하지 않는다.

게임용 미리보기 툴바의 `Next`와 후일담 그림의 별도 `continueButton`은 숨긴다. `StoryPlayer.hideInlineContinueButtons`는 게임 Canvas에서만 켜며 제작용 미리보기의 기본값은 OFF다. 이야기 중에는 화면 터치와 SKIP, 승리 이야기 종료 후에는 Host의 다음 스테이지와 별도 역할의 관람·나가기, 최종 결과에는 엔딩 1개와 관람·전체 기록을 표시한다. 현재 컷에 원본 관람 버튼이 있으면 같은 역할의 하단 버튼은 숨긴다. 실패 화면은 그림 안의 원본 재도전·나가기만 사용한다. Guest 재도전은 비활성 상태와 방장 대기 문구를 함께 표시한다.

## 결과 확인·관람·입력 보호

`GameSession.SetResultPresentationBusy(bool)`는 로컬 이야기를 확인 중인지 알리는 API다. Guest는 기존 진행 제어 메시지의 run·스테이지·revision·설정 identity를 붙인 `result_presentation`으로 Host에 알리고, `result_ready` 주기 전송에도 현재 값을 함께 보낸다. 다른 스테이지/재시도에서 온 늦은 메시지는 기존 identity 검사로 거절한다.

Host NEXT/재도전은 모두 다음 조건을 기다린다.

1. 해당 결과 Scene 로딩 완료와 정확한 종료 스냅샷 수신.
2. 연결된 전원 결과 준비 확인.
3. 전원 로컬 이야기 확인 완료.
4. 누구도 완성 박 관람 중이 아님.

이야기 확인과 박 관람은 별도 상태다. 관람을 열면 스토리 Canvas만 숨기며 컷을 새로 시작하지 않는다. 닫으면 떠났던 같은 결과 컷으로 돌아온다. 어댑터를 종료할 때 이벤트 구독·실패 대시보드용 임시 목록·로컬 진행 상태를 해제한다. 오래된 세대의 종료 콜백으로 새 세대의 진행 상태를 변경하지 않는다.

기존 결과 입력 보호를 공유한다. 결과 전환 직후와 이야기 종료 직후에는 최소 다음 프레임 및 눌린 터치/마우스 해제를 기다린다. 이야기 종료 터치가 새 결과 버튼을 누르지 않도록 대기하며, 해제 확인 뒤 로컬 이야기 완료를 Host에 알린다. 한 번 누른 입력으로 관람·재도전·다음 스테이지가 중복 실행되지 않도록 기존 pending 상태와 서버 조건을 함께 확인한다.

최종 엔딩은 각 기기에서 따로 시작한다. Host의 최종 Home은 기존 전원 결과 준비 조건을 유지하지만 다른 사람의 엔딩 종료를 기다리지 않는다. 최종 Completed 뒤 누군가 나가도 다른 기기의 확정 클리어와 로컬 엔딩을 GameOver로 바꾸지 않는다. 게임용 엔딩 끝에는 Figma 타이틀 컷을 한 번 더 보여주는 대신 입력 해제 뒤 실제 Home으로 돌아간다.

## 같은 방에서 실패한 스테이지 재도전

정상 Story 전투의 시간 초과 또는 전원HP0에서만 `GameRunFlow.MarkDefeat`가 일치하는 전투ID·라운드를 확인하고 `GameOver.retryableDefeat`를 켠다. 이 flag는 일반적인 GameOver를 재개할 수 있게 하는 옵션이 아니다.

- `IsStageRetryAvailable`: 정상 Story Defeat이며 아직 같은 방 연결이 유지돼 재도전 UI를 제공할 수 있는 상태.
- `CanRetryStage`: Host, 원래 참가자 전원 유지, 로딩/중복 재시작 없음, 정확한 실패 스냅샷·결과 준비·이야기 확인·관람 종료까지 확인한 실행 권한.
- `RetryStage()`: Host가 정확한 run·스테이지·revision에서 `RetryFailedStage`를 승인한 뒤 기존 `PrepareStage`를 호출한다. Guest·중복 클릭·오래된 revision은 새 준비를 만들지 못한다.

방 연결·원래 참가자·P번호·runId·현재 스테이지 번호·승인된 설정·이전 성공 StageResults는 유지한다. 실패한 시도의 ResultSnapshot/관람 상태를 해제하고, 기존 새 스테이지 초기화를 사용해 구슬·북·투사체·도색·HP·스태미나·시간·기믹·통계를 재설정한다. 새 전투 identity와 설정 확인 → Scene 준비 → 첫 상태/표현 준비 → 공유 카운트다운을 모두 다시 통과한다.

일반 GameOver와 Completed는 기존처럼 terminal이다. snapshot replica에는 **정상 재도전 flag가 있는 동일 run·동일 스테이지의 더 새 LoadingBattle**만 예외로 허용한다. Playing으로 바로 건너뛰는 상태, 전투 identity가 남은 load, 다른 run/스테이지, 이전 결과 ACK는 이 예외에 해당하지 않는다. 실패 화면에서 원래 참가자가 끊기면 `EndDefeatRetry`로 자격을 닫는다. 재접속해 원래 시도를 이어가는 기능은 추가하지 않는다.

## 혼잡한 연결의 마지막 결과 복구

기존 Bulk는 피어 대기열이64KiB 이상일 때 새 공동 스냅샷 발행을 건너뛴다. 정상 Defeat 직후 GameOver가 전투 발행을 동결하면 다음 주기 발행이 없어질 수 있다. `LabNetwork.TerminalSnapshots`는 마지막 결과 발행에서 건너뛴 피어만 보관하고, 대기열이 기준 아래로 줄어들면 **기존 paint frame + snapshot chunk 트랜잭션**을 같은 Bulk 채널에 한 번 큐에 넣는다. 이미 큐에 들어간 조각의 재전송은 기존 선택ACK·RTO가 맡는다.

전투 시뮬레이션·도색 성장·새 입력을 다시 시작하지 않는다. owner-board 헤더에서 피해·봉인율·승패를 추정하지 않는다. 정확한 지도와 종료 스냅샷이 도착해야 결과 준비가 된다. 새 라운드·전송 초기화 또는 해당 피어 이탈에서는 남은 복구 대상을 제거한다. 현재 helper는 Game의 Victory/Defeat terminal 발행을 대상으로 하며 독립 Lab의 기본 발행 정책은 유지한다.

## 호환 범위와 확인할 검사

#558 당시 묶음은 **공유 설정259개 · 프로토콜5691 · `C6PhysicsLab/52` · `C6HapioGame/39`**였다(현재 Game 기준은 `AGENTS.md`의 #501 절). 새 GameOver 재도전 flag와 이야기 확인 제어 때문에 Game 토큰을 올렸다. Lab 토큰·DEV 필드 수·지도 압축·Bulk 선택ACK 형식은 그대로다. Game38 이하 앱을 Game39 방과 혼용하지 않는다.

관련 자동 검사:

- `GameRunFlowTests`: 정상 패배만 같은 스테이지를 새 준비 세대로 전환, 이전 ACK·중복·이탈 종료 차단.
- `GameSessionFinishTests`: 실패 결과 복사, 이전 성공 기록 보존, 이야기 확인 상태 수명.
- `LabTerminalSnapshotCatchupTests`: Bulk 백로그로 누락된 terminal 지도/스냅샷 복구, 중복 큐/새 라운드 누출 차단.
- `GameStageRetryPlayModeTests`: 저장 Home/Battle/GameOver를 통한 실제 Host 모델의 패배·동일 방 재시작·리소스 초기화·이전 성공 기록 보존.
- `GameStageStoryAssetTests` / `GameStageStoryPresentationTests`: 저장 참조·실제 카드/게이지·Story 전환·관람/종료 수명.

이 목록은 검사의 존재와 담당 범위다. 실행 여부·환경·성공/실패를 뜻하지 않는다. iOS 일반Wi-Fi/핫스팟·다인원 시각 확인은 수행 여부를 별도 검증 기록에서 확인한다.

## #642 봉인 총평

- Figma `wW7ny7lNQ4DXNJ4kD46AuR`, 프레임 `4593:1603`의 402×874 배치와 원본 요괴·도장 이미지를 사용한다. `SealRunSummary.prefab`은 `GameEnding.asset`의 첫 컷이며 그 뒤의 6개 엔딩 컷은 유지한다.
- `GameStageStoryPresentation.BindView`가 현재 실행의 `GameSession.StageResults`와 `Network.LocalPlayerId`를 `SealRunSummary.Bind`에 넘긴다. 기존 `RecordStageResult`가 보관하는 성공 결과만 사용하므로 실패·재도전 대기·후일담 시간은 더하지 않는다.
- 각 행은 성공한 한 판의 `PlayedSeconds`, 총 도장은 세 판 원본 시간을 합산한 뒤 초 미만을 버린 `분:초`다. 60분 이상도 분을 누적 표시한다. 세 성공 기록이 모두 없으면 총합은 `—`이며 이전 경기나 디자인 예시로 채우지 않는다.
- 내 기여도는 해당 판의 `damageTotal`(Host가 확정한 타격의 누적 공격 점수) 중 로컬 플레이어 몫을 정수 퍼센트로 반올림한 값이다. 최종 칠 소유 면적·명중률과는 별도다. 기록에 없는 플레이어는 `—`, 공격 점수 합계가 0인 참가자는 `0%`다. 반올림 때문에 참가자별 정수 합은 100과 조금 다를 수 있다.
- 요청한 평가 문장 10개 중 인스턴스 첫 바인딩 때 하나만 선택하며, 같은 화면의 데이터 갱신으로 바뀌지 않는다. 최신 Figma에 따라 “수고했소.”는 표시하지 않고, 22px/35px 행간의 평가 두 줄을 표시한다. 10개 문장은 사용자가 지정한 위치에 명시적 개행을 넣으며 자동 줄바꿈을 하지 않는다. 도장은 최신 원본(불투명도 100%)과 170.511×156 크기, 시간 숫자 32.911px 및 수정 위치를 따른다.
- 세 봉인 완료 그림은 0.12/0.50/0.88초에 회전을 시작해 각각 0.68초 동안 감속·정지한다. 총 시간 도장은 1.7초에 착지해 0.3초 동안 눌림을 풀고, 2초 이후 계속 버튼을 표시한다. 총평에서는 배경 탭과 건너뛰기를 숨긴다. 버튼은 `StoryPlayer`의 기존 `StoryCloudWipe` 경로로 다음 엔딩 컷을 완전히 가려진 순간 교체한다.
- 새 미리보기: `Hapio > Story > Open Seal Run Summary Preview` → Play. 총평과 엔딩만 재생하며, `SealRunSummaryPreview.samplePlayer`를 1/2로 바꾸어 각자의 수치를 확인할 수 있다. 예시 데이터는 이 전용 미리보기 구성에만 있고 실제 결과 연결에는 쓰지 않는다. 게임 Scene·빌드 목록은 변경하지 않는다.
- `Hapio > Story > Create Seal Run Summary`는 빠진 신규 에셋만 만들고 같은 컷을 중복 삽입하지 않는다. 이미 만든 프리팹의 수작업 편집은 덮어쓰지 않는다.
- 색상은 Figma 명시값을 따르며 표 제목 `TextColor/DarkRed/900`, 헤더 `5OrbsColor/FireRed/900`, 행 `TextColor/SecondaryLightIvory/800`, 평가 `TextColor/PrimaryBrown/500***` 역할을 사용한다. 기존 한지 배경 재질과 신라문화체를 재사용한다.


### #647 Host-controlled stage clear

`StoryStageClearControls` owns role-specific UI on all three After_02 prefabs. Only the host sees Next Seal; existing readiness, viewer and touch-release gates control interaction. Background taps cannot advance. Gumiho/Yagwanggui guests see the host-wait message. Jigwi guests hide both the next-yokai heading and host-wait message, while host UI remains unchanged.

Host advances everyone to the next game for stages 1/2. After Jigwi, StartSummary broadcasts a sticky summary-start flag for the matching completed run/stage/revision/settings/battle/round. Repeated state broadcasts recover missed messages. Summary/ending reading thereafter remains individual. Game connection token is C6HapioGame/49; older clients cannot mix. StageStoryShowcase.previewAsHost previews the role without network progression.
