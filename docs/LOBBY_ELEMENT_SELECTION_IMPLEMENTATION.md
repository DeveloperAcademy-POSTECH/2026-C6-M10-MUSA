# #56 로비 속성 선택·랜덤 자리·속성별 공격 구현 기록

작성일: 2026-09-29, 후속 수정일: 2026-09-30. 브랜치: `feat/#56-lobby-element-selection`. 최초 구현 당시의 기준 HEAD는 `991339eeeedf18e2baa1fb859f857a1c8859ba2a`였고, 그때는 미커밋 작업이었다. 이후 기능 커밋 `7511ef52`와 `main` 병합 커밋 `f914a25b`까지 반영했다. 최초 사용자 승인: r02의 8개 규칙 + 선택 해제 버튼. 2026-09-30 후속 지시가 Raw 생성 규칙을 변경했다. 계획: [LOBBY_ELEMENT_SELECTION_PLAN.md](LOBBY_ELEMENT_SELECTION_PLAN.md). 병합 후 검증 결과는 [별도 기록](validation/ELEMENT_SELECTION_MAIN_MERGE_20260930.json)을 따른다.

## 현재 사용 흐름

1. `ContinuousTransferBattle`에서 방을 만들거나 참가한다. 처음 선택은 None이다.
2. 로비에서 Fire(화), Water(수), Wood(목), Metal(금), Earth(토) 중 하나를 선택한다. 다른 참가자의 점유 표시가 있는 버튼은 사용할 수 없다. Host가 승인한 값만 표시한다.
3. `CLEAR SELECTION`은 자기 선택을 해제한다. 선택/해제 성공 시 전원 Ready를 해제한다. None은 Ready 불가다. 요청 중에는 선택·해제·Ready를 막는다. 점유된 속성 요청의 거절은 기존 선택과 Ready를 보존한다.
4. 전원 선택·Ready 후 Host가 시작한다. P번호는 입장 당시의 식별자로 유지하고 실제 자리는 별도로 추첨한다. 전투 표시의 `P1 / Fire / Seat 3`은 플레이어·선택·물리적 자리를 구분한다.
5. Host는 **현재 방의 활성 참가자들이 승인받은 선택 속성 중 하나**를 무작위로 뽑아 Raw를 생성한다. 요청자도 속성을 선택한 상태여야 하지만 자기 속성만 나오는 것은 아니다. 다른 속성도 보관·동일 속성 Yin+Yang 조합·전달할 수 있다. 구슬 원소는 전달로 바뀌지 않는다.
6. 현재 소유자의 선택과 Combined 원소가 일치해야 공격 가능하다. 불일치 투척은 OK 경고창을 열고 드래그 시작 위치로 복원한다. ID·소유권·Idle은 보존하며 발사·피해·명중 회복을 만들지 않는다. 전투 시간은 계속 흐른다.
7. Retry는 선택을 유지하고 다음 판의 자리만 재추첨한다. 같은 순서가 다시 나올 수도 있다. 모든 참가자가 새 공유 상태를 적용·ACK한 후 Host의 START가 활성화된다.
8. 결과의 LOBBY 또는 Retry 준비의 `ROOM LOBBY`는 같은 방의 실제 선택 로비로 돌아간다. 연결·P번호·선택은 유지하고 전원 Ready를 해제한다. 속성 변경/해제·재선택 후 다시 Ready하여 새로운 roundId로 시작한다. 새 방에서는 다시 None이다.

자리 추첨은 참가자 ID의 순열이다. Host/P1도 대상이며 판 도중에는 순서가 고정된다. 좌우 전달·시점·투척 위치·요괴 방향은 그 순열을 사용한다. Raw 원소는 Host의 승인 선택 집합에서 별도 난수 흐름으로 뽑으며, 구슬 음양 추첨과 분리한다. 추첨 입력에는 roundId도 포함해 Retry가 이전 판의 속성 순서를 그대로 반복하지 않게 한다. 기존 Raw 생성 비용·음양 Seed·물리·시간 회복·유효 명중 보상·받는 쪽 한도는 변경하지 않았다.

## 코드 연결과 변경 지점

| 책임 | 실제 연결 |
|---|---|
| 선택 승인·중복 점유·Ready 배리어 | `Lobby/LobbyAuthority.cs`의 `Handle` 및 `ReturnToLobby`. `LobbyPlayer.selectedElement`, `selectionRevision` 공유. 이전 선택 revision의 Ready는 거절 |
| 선택 요청·Pending·승인 표시 | `Lobby/T10LobbySession.cs`의 `SelectElement`, `ClearElement`, `CanReady`, `CanSelectElement`; `T10LobbyController`→`T10LobbyHud` 이벤트 |
| 첫 시작 계약 | `LobbyStartContract.selectedElements`는 입장 명단과 같은 순서. `roundSeatOrder`는 별도 순열. `LobbyWire`가 유효성·선택 유일성·계약 일치를 검사 |
| 게임 바인딩·Retry·로비 복귀 | `GameSync/T10GameSession.cs`의 `Attach`, `Retry`, `ReturnToRoomLobby`, `DetachToRoomLobby`. 선택 맵과 자리를 하위 서비스에 전달하고 ACK 전에 Client 프레임 적용. `Attack/AttackSession.cs`의 완전 퇴장 경로는 이전 방의 승인 로스터·선택·자리 상태를 비워 새 방 재입장에 넘기지 않음 |
| 명시 Raw 원소 | `Orbs/OrbModel.cs`의 `OrbRecord.RawElement`, `Attack/AttackWire.cs`의 `OrbWire.rawElement`. 새 계약은 None Raw를 거절. clone/전달/직렬화가 원소를 보존 |
| 승인 선택 집합에서 Raw 생성 | `Resources/HostResourceAuthority.cs`의 `ConfigureSelectedElements`, `SelectedElementPool`, `ElementFor`, 생성 처리. `ResourceSession`이 승인 맵을 전달한다. Host는 자원 세션의 활성 참가자와 승인 맵의 교집합에서 유효 원소를 정렬해 추첨한다. 요청자 선택 누락은 비용·성공 순번 변화 없이 거절한다. 원소 추첨은 음양 추첨과 분리한다 |
| 동일 원소 조합 | `HostOrbRegistry` 예약·commit 및 `OrbElements` record 비교. Raw는 명시 원소, Combined는 새 GUID의 동일 두 원소 suffix로 판정. 과거 진단 ID fallback과 정상 계약의 엄격 검사를 분리 |
| 자리 소비자 | `RoundSeatLayout`, `ParticipantRing`, `AttackSession.RoundSeatOrder`, `ParticipantLaunchFrame`, `ParticipantViewAngle`, `ThrowBattleFraming`, `MonsterAttackPresenter` |
| 공격 권한 | `AttackAuthority.ConfigureSelectedElements`→현재 sender/owner의 선택 검사→예약/투사체. `SELECTED_ELEMENT_MISMATCH`는 예약 전에 거절 |
| 경고·구슬 복원 | `T09BattleController.SubmitDecision`, `RejectElementLaunch`, `OnResolved`. 저장한 dragStart로 복원, 추가 추진 없음. `T09Hud`의 저장된 경고창·OK·자리 표시·Ready 버튼 |
| 원소/자리 무결성 | `GameWire`의 Context/정상 snapshot 검사/hash. 판 도중 선택·자리 변경 및 같은 ID의 Raw 원소 변조 거절. 선택은 owner 변경과 별개 |

정상 실행 Scene은 `Assets/_Project/HapioMVP/Scenes/ContinuousTransferBattle.unity`다. `T10GameSession` 및 `T10LobbySession`에 속성 계약을 켰으며 기존 루트·Canvas·에셋·수동 배치를 보존하고 새 경고·자리 표시·Ready 조작만 추가했다. 저장된 버튼의 이벤트는 Controller에서 연결하므로 Inspector에 같은 동작을 중복 등록하지 않는다.

Unity6000.5.7f1·URP17.5.0·기존 패키지를 유지했다. 기존 UI 폰트와 영문 레이블을 사용한다. 새 로비/게임 계약은 **protocol56**이고 Attack/Resource/Combination payload는 **v2**다. 원소 선택이 없는 옛 정상 앱과 함께 접속하는 계약이 아니므로 참여 앱은 같은 새 코드로 빌드해야 한다. 기존 Scene BuildIdentifier25·앱 표기26은 자동 증가시키지 않았다.

## 검증 도구와 근거

아래 표는 **2026-09-29 최초 #56 구현**의 실행 기록이다. 전체 결과와 비교 정보는 [검증 요약 JSON](validation/ELEMENT_SELECTION_20260929.json)에 기록한다. 2026-09-30 Raw 규칙 및 새 방 정리 수정에 이 결과를 소급 적용하지 않는다. 소스 존재·단위 검사·실제 다인 실행·iOS Touch는 별도로 판정한다.

| 단계 | 수행 환경·실제 결과 | 판정 |
|---|---|---|
| Compile·Scene 연결 | Unity API로 새 UI 저장 및 참조 검사, 최종 Mac 빌드 | PASS |
| 최종 전체 EditMode | 1,661 실행 / 1,661 통과 / 실패·누락0 | PASS |
| 전체 PlayMode | 164 실행 / 127 통과 / 37 실패 / 누락0 | **FAIL** |
| 새 로비 PlayMode | 실제 NGO Host·Pending fixture·UI 배치의 새 검사3개 | PASS 3/3 |
| 변경 전 Play 비교 | 독립 HEAD 소스 복사에서161 실행 / 124 통과 / 같은 테스트37개 실패. 이번 실행의 새로운 실패 이름0. 랜덤 fixture2건은 실패하는 세부 assert가 달랐음 | 기존 실패 재현; 전체 PASS 아님 |
| 최종 Mac 개발 빌드 | errors0·warnings14. 기존 진단/Unity API의 obsolete 경고 포함 | PASS |
| Mac3인 | 독립 앱3개, 모두 exit0 / PASS. 기능 확인87회, round1→2→3의 선택·자리·hash 일치 | PASS — 명시 fixture/합성 입력 |
| Mac5인 | 독립 앱5개, 모두 exit0 / PASS. 기능 확인135회, 전체 속성 점유·해제·교환·새 시작 포함 | PASS — 명시 fixture/합성 입력 |
| iOS·실제 Touch·실네트워크 | 이번 변경으로 수행하지 않음 | NOT_RUN |

Mac5인 첫 판의 자리 순서(플레이어 식별자 기준)는 **P2→P4→P1→P5→P3**, Host/P1은 자리3이었다. Retry 순서는 P1→P5→P4→P3→P2, 로비에서 P1/P2 속성을 교환한 다음 시작은 P1→P3→P2→P5→P4였다. 같은 배치가 나올 수 있다는 규칙은 유지한다. 이 한 실행의 순서를 항상 나오는 규칙으로 해석하지 않는다.

2026-09-29 전체 Play 실패37건은 변경 전 실행에도 존재했다. 오래된 HP100 기대와 현재 저장 HP300 fixture의 불일치, 기존 랜덤 Raw/같은 원소 fixture, 과거 Scene의 framing 누락 등이 포함됐다. 그때 기존 게임 수치나 옛 Scene을 해당 테스트에 맞춰 변경하지 않았다. 후속 수정의 전체 PlayMode 결과는 별도로 기록해야 한다.

자동 검사 항목은 다음 파일에 있다.

- `Tests/Lobby/EditMode/LobbyElementSelectionTests`: 고유 점유·경합·거절 보존·선택 해제·이전 Ready 차단·전투 고정·새 방/로비 복귀.
- `Tests/Lobby/PlayMode/SavedRoomLobbyPlayTests`: 실제 NGO Host에서 선택/해제 버튼 연결, 명시 Pending fixture, 좁은 화면의 선택 UI 배치. 단독 Host 시험을 다인 접속으로 해석하지 않는다.
- `Tests/Attack/EditMode/SelectedElementAttackTests`, `RoundSeatOrderTests`: 일치5/불일치20 공격 자격, 현재 소유자, 예약 보존, Host 포함 자리·투척 프레임.
- `Tests/GameSync/EditMode/ElementSelectionGameWireTests`: 선택/자리/hash·새 판 허용·판 도중 변조 거절·타 속성 Raw 전달. `P4SceneTests`는 저장된 새 UI 참조까지 검사.
- 기존 Orbs/Resources/Combination/Wire/영수증 시험: Raw 원소 보존·생성 비용/거절·동일 원소 조합·변조 거절. 2026-09-30 추가한 `HostResourceAuthorityTests`의 승인 선택 집합·활성 참가자 필터·맵 순서 독립·실패 후 추첨 불변 사례와 `RoundSeatOrderTests`의 새 방 재입장 사례는 **소스 추가**이며, 재실행 결과를 이 문장만으로 PASS 처리하지 않는다.

`GameSync/ElementSelectionValidationProbe.cs`는 **Mac Development 앱에 `-c6Element56`를 명시했을 때만** 자동 부착된다. 일반 실행·Editor·iOS에서는 시험을 시작하지 않는다. 독립 프로세스 3/5개, 실제 인증 메시지·공유 hash·ACK를 기록하고 앱은 스스로 종료한다. runtime fixture는 15초·HP20·공격 예고 60초이며 저장 Config를 수정하지 않는다. 조합 재료 주입·중복 요청의 로컬 점유 가드 우회·합성 포인터 조작을 명시한다. 일반 180초·실제 손가락 조작·새 유효 3D 명중을 통과했다고 해석하지 않는다.

로컬 원시 XML·로그·캡처는 `/private/tmp/c6-56-*`에 보존하고 공개 저장소에는 실행 수·결과·검증 범위·비밀이 없는 요약만 둔다. Mac3 초기 시험의 marker 충돌·Host snapshot 반영 대기 누락·시험 종료 후 Client 퇴장 경합은 검증 도구의 실패 기록으로 남기며 수정 후 실행과 구분한다.

## 직접 확인하는 방법

Unity에서 Main Scene을 열어 Play한다. 두 개 이상 같은 새 앱을 실행해 방 생성/입장→서로 다른 선택→Ready→Host Start 순서로 진행한다. 아래를 한 번의 세션으로 확인할 수 있다.

1. 미선택 Ready 금지, 다른 사람이 선택한 버튼의 점유 표시, 선택 변경/해제로 전원 Ready 해제.
2. Raw가 **현재 활성 참가자의 승인 선택 집합** 안에서 생성되는지, P번호/자리/좌우 표시가 일치하는지 확인한다. 짧은 무작위 표본에 모든 원소의 출현을 요구하지 않는다. 첫 시작과 Retry에서 Host가 첫 자리에 고정되지 않는지 확인하되 동일 배치의 반복은 허용한다.
3. 타 속성 Raw 두 개를 받았을 때 동일 속성 Yin+Yang 직접 드래그 조합·전달은 허용. 다른 원소 또는 같은 음양은 조합되지 않음.
4. 타 속성 Combined 투척에서 경고·원위치·동일 ID/owner/Idle·시간 감소. 원래 그 속성을 선택한 플레이어에게 반환하면 공격 자격이 다시 맞음.
5. 결과→Retry에서 선택 유지·빈 구슬·자원 초기화·새 자리. ROOM LOBBY→해제/교환→Ready/새 시작에서 새로운 선택 적용.

자동 검사 재실행은 같은 프로젝트 Editor가 열려 있는지 먼저 확인한다. 씬의 새 참조를 점검할 필요가 있을 때만 메뉴 `C6/Lobby/Prepare Element Selection UI`를 사용한다. 기존 저장되지 않은 Scene 변경이 있으면 이 준비 도구는 중단하므로 먼저 사용자 변경을 저장한다. Mac 자동 시험용 빌드는 `ElementSelectionSetup.BuildMacValidation`으로 새로운 `C6_56_OUTPUT_ROOT`를 지정한다. 이 진입점은 기존 P4의 Prepare 전체 재생성을 호출하지 않는다.

## 2026-09-29 기준 남은 확인 범위 (당시 기록)

- 이번 코드의 iOS Xcode 출력·빌드·서명·설치·실제 Touch: **NOT_RUN**.
- 실제 iPhone/iPad에서 경고창·자리별 카메라/투척 프레임·Raw 색상/아트 확인: **NOT_RUN**.
- 이번 기능을 일반180초·핫스팟·일반 Wi-Fi·인터넷 없는 LAN에서 실행: **NOT_RUN**. Mac loopback 검증을 네트워크 환경 검증으로 승계하지 않는다.
- 과거 PlayMode 실패는 아래 기준 버전 실행 비교 결과를 따른다. 전체 PlayMode PASS로 표시하지 않는다.
- 커밋·push·PR은 수행하지 않았다.

## 2026-09-30 iPhone 후속 검증과 새 방 오류

[실기기 검증 기록](validation/ELEMENT_SELECTION_IOS_20260930.json)은 **Raw 규칙 수정 전 빌드**의 결과다. Unity iOS 출력, Xcode 개발 빌드·개인 Team 서명, iPhone 17 설치·앱 실행을 확인했다. iPhone과 Mac의 실제 방 입장, 선택·해제·Ready 초기화, 2인 전투 시작·자리, 기존 자기 속성 Raw 생성, 전달·조합·유효 명중, Retry, 양쪽의 타 속성 투척 경고·구슬 복원을 각각 기록했다. 당시 Raw가 자기 속성으로 나왔다는 관찰은 그 빌드의 동작이며, 후속 무작위 생성 규칙의 검증 근거가 아니다.

두 앱이 이전 방을 완전히 나가 새 방에 들어간 뒤에는 둘 다 미선택·Ready 불가가 확인됐다. 그러나 다시 속성을 고르고 Ready한 다음 Host Start에서 `GAME_ATTACH_FAILED`가 발생했다. 원인은 `AttackSession`에 과거 방의 승인 로스터·선택 맵이 남아 새 연결의 참가자 ID와 충돌한 것이다. 새 방의 P번호와 네트워크 전송 ID를 동일시해서는 안 된다. 완전 퇴장 시 이전 방의 승인 세션·라운드·로스터·자리·선택을 비우는 수정과 회귀 테스트 소스를 추가했다. **실패한 빌드의 결과는 FAIL로 보존**하며, 수정 빌드의 재입장/전투 시작은 별도 실행 결과가 있어야 PASS로 기록한다.

후속 사용자 지시에 따라 Host Raw 원소 추첨을 현재 방의 활성 참가자들이 승인받은 속성 집합으로 바꿨다. 요청자 선택은 생성 자격, 생성 원소는 집합에서 별도 결정적 난수로 선택한다. 비용·음양 추첨·실패/중복 요청의 성공 순번은 유지한다. `GameSync/ElementSelectionValidationProbe.cs`의 후보 검사도 해당 규칙으로 변경했다. 이 소스 변경만으로 새 빌드의 자동 검사·실기기 결과를 PASS로 기록하지 않는다.

### 2026-09-30 수정 빌드의 별도 확인

[수정 빌드 검증 기록](validation/ELEMENT_SELECTION_IOS_20260930_B.json)은 위 실패 빌드와 분리한다. Unity EditMode **1,664/1,664 PASS**, 로비 PlayMode 필터 **18/18 PASS**를 XML로 확인했다. 수정된 Mac 개발 앱과 iOS Xcode 프로젝트, iPhone Debug 앱 빌드·개인 Team 서명·설치·실행도 각각 확인했다. iPhone 17과 Mac의 실제 첫 방에서 두 사람의 선택인 화·목 Raw가 양쪽 기기에 나타났고, 각자의 선택과 다른 Raw도 생성됐다. 두 앱이 방을 완전히 나간 뒤 앱 프로세스는 유지한 채 다른 방에 입장해 전투 시작까지 다시 성공했다. Host 로그에는 서로 다른 게임 세션에서 `C6_T10B_START`가 한 번씩 기록됐고, 사용자가 두 번째 전투 화면을 확인했다. 기존 실패 기록은 당시 빌드의 결과로 계속 보존한다.

수정 빌드의 **전체 PlayMode는 미실행**이며 2026-09-29의 37개 실패를 이번 빌드의 통과로 승계하지 않는다. 수정 빌드의 3·5인 합성 프로세스 검증과 3·5대 실기기 검증도 미실행이다. 짧은 실제 생성 표본에서 화·목이 모두 보인 사실은 이번 방의 관찰 결과이며, 각 생성 횟수마다 모든 원소가 반드시 출현한다는 뜻이 아니다.

## 변경 기록

2026-09-29: 사용자 승인한 8개 규칙과 선택 해제 구현. **당시 Raw는 자기 선택 속성만 생성했다.** 선택/자리 데이터를 통신 계약에 추가, 동일 속성 조합 유지, 현재 소유자의 공격 자격과 로컬 경고/복원, 같은 방 로비 복귀·새 roundId 재시작 연결. 게임 규칙 변경과 검증 도구 fixture를 위에서 구분했다.

2026-09-30: 사용자 후속 지시로 Raw를 활성 참가자들의 승인 선택 집합에서 추첨하도록 수정. 실기기에서 발견한 새 방 재입장 오류의 과거 승인 상태 정리 경로를 추가. 두 수정은 최초 2026-09-29 자동 검사 결과 및 수정 전 iOS 결과와 구분한다.

## 2026-09-30 main 병합 후 검증

`main`의 요괴 방해 UI와 #56의 속성 경고·수동 Scene 배치를 함께 유지했다. 병합 후 Unity EditMode는 **1,695/1,695 PASS**, 전체 PlayMode는 **128/164 PASS·36 FAIL**이다. 병합 전 실패 목록과 비교하면 새로 실패한 테스트 이름은 없지만 전체 PlayMode를 PASS로 취급하지 않는다. 그 실행에 포함된 로비 PlayMode는 **18/18 PASS**다. 병합 후 Mac/iOS 앱 빌드와 실기기 플레이는 **NOT_RUN**이며, 앞 절의 iPhone 검증을 병합 후 결과로 승계하지 않는다. 수치와 실행 근거는 [병합 검증 기록](validation/ELEMENT_SELECTION_MAIN_MERGE_20260930.json)에 있다.
