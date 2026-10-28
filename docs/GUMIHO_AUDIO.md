# 구미호 피버와 스테이지 오디오

기준: #334 피버 규칙과 #351/#358 구미호 오디오가 병합된 main. 이 문서는 README의 개발 설명을 이관한 것이며, 새 기능이나 검증 결과를 추가하지 않는다.

## 피버의 규칙과 화면

구미호는 기본적으로 남은 시간 20초에 피버를 시작한다. Host의 `feverTimeAt`을 기준으로 새 기믹을 멈추고, 진행 중인 넘기기 막기·매혹·여우 구슬·잠긴 박 폭탄을 정리하며 스태미나 **자동 충전만 2배**로 만든다. 명중 보상과 구슬 보유 한도는 유지한다. 이미 잠금이 풀린 폭탄은 공격 구슬 2개로 열린다.

`LabFeverTimeHud`가 스태미나 옆의 “두배” 말풍선·연기, “피버타임! / 시간이 없어! 마구 던져버리자!!” 문구, 붉은 화면 가장자리와 남은 시간 숫자를 표시한다. Hapio DEV **구미호** 탭의 238번(시작 남은 초, 기본 20·0이면 끔)·239번(충전 배율, 기본 2)에서 조절한다. 다른 요괴와 2:2는 각각의 피버 정책을 사용한다.

정확한 범위·수치·화면의 늦은 표시 정책은 [DEV 매개변수 명세의 #334](DEVELOPER_MODE_PARAMETER_SPEC.md)를 따른다. 사운드 경로와 음량은 아래 #351 구현을 따른다.

## 전투 BGM과 피버 곡

`Assets/Lab/Scripts/Audio/LabBattleBgm.cs`가 스냅샷의 전투 상태를 관찰한다. 아래 경로는 `Resources.LoadAll<AudioClip>`에 전달하는 경로이며, 실제 파일은 `Assets/Lab/Resources/` 아래에 있다.

| 구분 | Resources 경로 | 현재 선택과 대체 |
| --- | --- | --- |
| 구미호 전투 | `Audio/GumihoBGM` | 새 라운드 시작 때 구미호이며 파일이 있으면 첫 클립을 선택한다. 없거나 다른 스테이지·2:2·Lab 단판이면 `Audio/BGM`을 쓴다. |
| 구미호 피버 | `Audio/GumihoFever` | Host 피버 시각부터 전용 곡을 쓴다. 비어 있으면 기존 야광귀 피버 곡 `Audio/Fever`로 대체한다. |
| 피버 시작 추임새 | `Audio/ComboClear` | 피버 시작을 1초 미만으로 늦게 관찰한 경우 “좋다” 클립을 한 번 재생한다. 그보다 늦으면 생략한다. |

현재 구미호 전투 곡은 약 162초의 Streaming 클립이고 피버 곡은 약 20초다. 두 BGM AudioSource는 반복 재생하므로 DEV로 피버를 늘리면 곡도 반복한다. 파일의 출처·가공·사용권 확인 사항은 [오디오 크레딧](AUDIO_CREDITS.md)을 따른다.

- 피버 전환은 기본 **0.25초 교차 전환**이다. 피버를 늦게 관찰하면 경과 시간에 맞는 위치에서 재생한다.
- 일시정지는 현재 재생 위치를 멈추고 재개 시 이어 간다. 정지 중에는 피버 시작·종료 판정을 새로 전환하지 않는다.
- 승리·패배에서는 기본 **1.5초**에 걸쳐 줄인다. 새 라운드는 처음부터 시작하며, 스냅샷 소실·로비 복귀·연결 종료는 즉시 정리한다.
- 위 전환 시간은 `LabBattleBgm.feverCrossfadeSeconds`·`fadeOutSeconds` Inspector 값이다. 피버 곡과 추임새는 각각 기기 BGM·효과음 음량 설정을 사용한다.

## 효과음 6종과 실제 알림

`Assets/Lab/Scripts/Audio/LabGumihoSound.cs`는 같은 화면의 네트워크·조합판·매혹 UI에 연결된다. 폴더에 여러 클립이 있으면 매번 무작위로 하나를 선택한다.

| 효과음 | Resources 경로 | 입력 근거와 재생 대상 |
| --- | --- | --- |
| 매혹 걸림 | `Audio/CharmCast` | 스냅샷 `charmStartedAt`에 도달할 때 모든 기기. 예고 시작의 `SabotageCast`에서 바로 재생하지 않는다. |
| 내 구조 탭 완료 | `Audio/CharmCheck` | `LabCharmBubbles.OwnCheckShown`. 자기 몫의 탭을 끝낸 도우미 기기만 재생한다(저장 기본 10회, DEV로 조절). |
| 매혹 풀림 | `Audio/CharmRelease` | 걸린 기기의 `LabCharmHud.CharmReleased`와 양옆 도우미의 `LabCharmBubbles.HelperCharmEnded`. 구조 완료·시간 만료·피버 정리에 반응한다. |
| 여우 구슬 낙하 | `Audio/FoxFall` | `SabotageCast(FoxFire, target)`. 대상 조합판 주인의 기기만 재생한다. |
| 여우 구슬 제거 | `Audio/FoxClear` | `LabOrbBoard.FoxFireCleared`. 두 번 누르기 또는 음·양과의 접촉으로 연기가 나는 조합판에서 재생한다. 수명 만료·피버 정리는 제외한다. |
| 해금 폭탄 분해 | `Audio/BombOpen` | `LabOrbBoard.BombOpening`. 해금 폭탄이 멈춘 뒤 연기로 사라지고 공격 구슬로 열리는 조합판에서 재생한다. 잠긴 폭탄 폭발음과는 별도다. |

매혹 걸림·체크·풀림 알림은 같은 매혹의 시작 시각으로 중복을 막는다. 2인 방의 양쪽 배지와 일시정지 뒤 체크 재표시가 소리를 두 번 만들지 않는다. 라운드 종료로 매혹 UI가 사라질 때는 해제음을 내지 않는다.

매혹 걸림을 처음 알아챈 시각이 실제 시작보다 **1초 초과** 늦으면 재생하지 않는다. 스냅샷 사이 시각은 최대 1초까지 보간한다. 새 이벤트 관찰은 정지 상태에서 멈추지만, 이미 재생 중인 효과음에 별도 Pause/Stop을 적용하는 코드는 없다.

현재 **CharmCast·CharmCheck·BombOpen은 음원 미할당으로 무음**이다. 처리 알림·음량 설정이 있다는 사실을 실제 소리가 있다는 뜻으로 해석하지 않는다. 기존 `Resources/Sabotage/CharmHowl` 재생 경로는 제거됐다. 나머지 효과음 3개와 BGM 2개만 파일이 들어 있다.

## Inspector 음량: 저장값과 코드 기본값

원본은 `Assets/Lab/Resources/LabConfig.asset`, 구현은 `Assets/Lab/Scripts/Core/LabConfig.cs`의 **Sound · 구미호 스테이지**다. 8개 모두 0~1로 제한하며 DEV 필드에는 포함하지 않는다. 저장 자산을 사용하는 실행에서는 아래 저장값이 코드 초기값보다 우선한다.

| 속성 | 저장 자산 | 코드 기본값 |
| --- | ---: | ---: |
| `GumihoBgmVolume` | 0.4 | 0.2 |
| `GumihoFeverBgmVolume` | 0.4 | 0.3 |
| `CharmCastSoundVolume` | 0.4 | 0.7 |
| `CharmCheckSoundVolume` | 0.8 | 0.8 |
| `CharmReleaseSoundVolume` | 0.4 | 0.8 |
| `FoxFallSoundVolume` | 0.5 | 0.8 |
| `FoxClearSoundVolume` | 0.3 | 0.8 |
| `BombOpenSoundVolume` | 0.8 | 0.8 |

최종 BGM 크기 = 해당 Inspector 값 × 기기의 `LabAudioSettings.BgmVolume`. 효과음 크기 = 해당 Inspector 값 × 기기의 `LabAudioSettings.SfxVolume`. 피버 추임새는 `LabBattleBgm.cueVolume` × 기기 효과음 설정이다. 기기별 값은 일시정지 메뉴에서 조절하며 새 공유 DEV 배율은 없다.

#351에서 공통 폭탄 소리의 저장값도 조정했다: 도화선 `fuseSoundVolume` 0.5→0.2, 폭발 `bombSoundVolume` 0.9→0.6. 이 둘은 기존 DEV 배율 50과 기기 효과음 설정 경로를 유지하며, 위 8개 전용 음량과 구분한다.

## Guest 스테이지 정보와 권한 경계

Host는 `LabNetwork.PrepareGameStage`에서 요괴를 설치한다. Guest는 Host 송신자·스테이지 식별·설정 세대·진행 상태가 검증된 뒤 `GameSession.SetLocalState` → `LabNetwork.ObserveGameStage`에서 같은 요괴를 기록한다. `GameStageYokai`를 사용하는 구미호 전투 곡과 야광귀 피버 곡·화면 효과의 기기별 분기를 위한 정보다.

`ObserveGameStage`는 게임 흐름이 꺼졌거나 Host이면 적용하지 않는다. 새 메시지·공유 DEV 필드·통신 토큰을 추가하지 않으며 Host의 소유권·소비·공동 판정 권한도 변경하지 않는다. Home/로비·2:2의 요괴 없는 식별은 `None`이고 연결 정리 시 초기화한다.

## 검증 상태와 확인 방법

이 문서 이관에서 Unity·빌드·자동 검사·실기기를 새로 실행하지 않았다. 기존 실행 결과는 [VALIDATION의 “#358 병합 전 추가 검증 — 2026-10-28”](VALIDATION.md#358-병합-전-추가-검증--2026-10-28)을 그대로 참조한다. 그 기록은 실제 청취·기기 연결·iOS를 **NOT_RUN**으로 구분한다.

자동 확인 대상은 `LabGumihoSoundTests`, `LabCharmBubblesTests`, `LabOrbPhysicsTests`, `LabBattleBgmStageTests`다. 실제 확인 시에는 같은 구미호 세션에서 Host·Guest의 전투 곡, 매혹 구조·여우 제거·해금 폭탄, 피버 직전 정지/재개, 피버 전환과 종료 음량을 비교한다. 미할당 3종은 음원을 넣기 전 소리 확인 대상에서 제외한다. 실제 네트워크 타이밍과 음량 균형은 자동 검사 통과로 대체하지 않는다.
