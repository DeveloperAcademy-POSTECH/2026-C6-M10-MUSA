# 스테이지 사이 스토리·봉인 카드·엔딩

이 문서는 #553의 제작용 부품·미리보기 범위를 설명한다. 현재 일반 게임에 연결된 실제 경기 기록·같은 방 재도전·엔딩 흐름은 [#558 스토리 결과 통합](STAGE_STORY_UI_INTEGRATION.md)을 따른다.

## 미리보기

Unity 메뉴 **Hapio > Story > Open Stage Stories Preview**를 열고 Play한다. 위쪽의 전체 / 구미호 후 / 야광귀 후 / 지귀 후 / 엔딩 / 실패 버튼으로 묶음을 고른다. 이전·다음·다시 보기·자동 진행은 기존 StoryPlayer와 같다. 카드 자체의 클릭·스와이프는 컷 이동과 분리되어 있다.

| 목록 (`Assets/Hapio/Story/StageStories/`) | 순서 |
| --- | --- |
| `AllStageStories.asset` | 구미호 후 → 야광귀 후 → 지귀 후 → 엔딩, 총 16컷 |
| `AfterGumiho.asset` | 구미호 봉인 카드 → 후일담 2컷 |
| `AfterYagwanggui.asset` | 야광귀 봉인 카드 → 후일담 2컷 |
| `AfterJigwi.asset` | 지귀 봉인 카드 → 후일담 2컷 |
| `Ending.asset` | 엔딩 6컷 → 타이틀 1컷 |
| `Failure.asset` | 봉인 실패 장면 → 실패 대시보드 |

실패는 성공 흐름의 대체 분기이므로 전체 성공 목록 뒤에 자동 재생하지 않고 별도 버튼으로 확인한다. 총 18개의 컷 데이터, 카드 뒷면을 포함한 Figma 원본 화면은 21개다. 오프닝 및 Home/Battle/StageResult/GameOver의 실제 연결은 변경하지 않았다.

## 수정 위치

- `Cuts/*.asset`: 컷 순서와 독립적인 ID, 화자·대사 페이지·자동 읽기 시간·음성 슬롯. 엔딩 대사는 Figma 원고를 사용한다. 무대사 컷과 타이틀에는 대사창을 숨긴다.
- `Views/*.prefab`: 그림·위치·크기·레이어 순서, 편집 가능한 카드 이름·소개·기록 Text. 그림은 개별 Image이며 전체 화면 캡처로 합치지 않았다.
- `Opening/SpeakerPalette.asset`: 기존 오프닝과 엔딩의 공통 화자 색상. 엔딩의 불이/솔이 색도 이 설정을 따른다.
- `Art/Story/StageStories/`: 원본 이미지와 제공된 SVG를 래스터화한 UI 먹선. 바이트가 같은 기존 이미지 5개는 기존 경로를 참조한다. 교체 시 `.meta`를 보존한다.
- `ImportedFrames.json`: Figma 원본 노드·이미지 슬롯·좌표·텍스트·기울기·색·자르기 기록. 실제 편집은 Prefab/컷 에셋에서 한다. Figma 자동 동기화는 아니다.

**Create Missing Stage Stories And Ending**은 없는 에셋과 미리보기만 생성한다. 이미 편집한 Prefab·원고·목록은 덮어쓰지 않는다. Figma 재반영은 수정할 프레임을 지정해 필요한 부분만 갱신한다.

## 봉인 카드

세 카드의 `Card Turn Pivot`에 **StoryCardFlip**이 있다. 종이 부분을 클릭하거나 좌우로 스와이프하면 앞·뒷면이 바뀐다. 종이가 떨어져 제자리에 닿으면 짧은 타격음이 나고, 카드 원본의 붉은 세로 ‘봉인’ 글자와 바탕이 함께 내려와 찍힌 뒤 뒤집을 수 있다. 밀기 안내는 한 StoryPlayer에서 처음 본 카드에만 나오며, 뒤집을 때 종이 소리가 난다. 카드 폭이 좁아지는 중간 지점에서 면을 바꾼다. 연속 입력은 회전이 끝날 때까지 무시하고 드래그 뒤 클릭은 중복 처리하지 않는다. 다시 표시할 때는 앞면으로 복원한다.

- `Front` / `Back`: 양면의 독립된 Image·Text. 미리보기의 기록은 Figma 예시다. 실제 경기에서는 `StoryCardRecords.Bind(rate, remainingSeconds, records)`로 표시 값을 전달한다. 기록 배열 순서는 딜왕, 명중왕, 구슬패스왕, 북 패스왕, 북 독점왕이다. 빠진 행은 대시로 표시한다.
- 도장 안의 세로 ‘봉인’ 글씨는 원본 배치에서 2px 아래로 내려 시각적 중앙을 맞춘다.
- `Flip Seconds`: 기본 0.55초. `Swipe Pixels`: 기본 35px.
- `Flip()` / `ResetFront()`: 코드에서 뒤집기 / 앞면 초기화.
- 뒷면 `봉인 박 둘러보기`의 글자와 입력 영역은 붉은 프레임 가운데에 맞췄다. 버튼은 `StoryResultActions.onInspectGourd`를 호출한다. 미리보기에는 실제 경기 박이 없어 관람 창을 열지 않으며, 카드 뒤집기와 입력을 분리했다.

## 컷 연출과 소리

- `Gumiho_After_02`: 화면 구석의 불빛이 꺼진다. 후일담의 진행 문구는 원안대로 “다음 요괴 잡으러 ...”를 사용한다.
- `Ending_01`: 불이와 앞쪽 봉인박만 함께 떠오른다. 만복 대사 페이지에 짧은 꼬르륵 소리를 지정했다. `Ending_02`부터 음악이 잦아들고 `Ending_04`에서 낮은 울림이 난다.
- `StoryVisualBeat`의 Delay·Duration·Offset·Sound로 등장과 효과음을 바꿀 수 있다. 엔딩 음악은 View의 Music·Music Volume, 대사별 효과음은 컷 에셋의 음성·효과음 슬롯에서 바꾼다. 원본 효과음은 `Art/Story/StageStories/Audio/`에 있다.

## 구름 전환과 대사 타이밍 (#589)

- 세 카드 모두 하단 **이야기 계속**으로 후일담을 연다. 앞·뒷면은 같은 버튼을 공유하며 카드 바깥을 누르는 동작은 컷을 넘기지 않는다. 기존 뒤집기와 박 관람은 별도 입력이다.
- 각 카드 View의 `Exit Cloud Wipe`는 공통 `Views/StoryCloudWipe.prefab`을 참조한다. Figma의 가장자리(4411:2248) → 중간(4411:2653) → 완전 덮임(4411:2319) 위치를 따라 큰 한지 구름 6조각과 작은 앞쪽 구름 3조각을 0.62초에 닫고, 화면이 가려졌을 때만 다음 컷을 만든다. 0.10초 뒤 0.80초 동안 걷힌다. 작은 구름 3개의 이동 시간만 각각 0.08초 늘렸으며 큰 구름의 실제 출발·퇴장 시각과 모든 구름의 출발 시각은 유지한다. 걷힘은 위쪽 오른편부터 아래쪽 왼편으로 순차적으로 열린다. `closeEnd`·`openEnd`로 도착도 엇갈리게 하여 늦게 출발한 구름이 같은 마감에 쫓겨 빨라지지 않도록 한다. 바깥·가장자리·중간·덮임 위치는 하나의 3차 베지어 경로의 끝점과 제어점으로 사용하고, `openDrift`로 걷힐 때만 약한 상하 흐름을 더한다. 작은 구름 3개는 첨부 시안의 위·가운데·아래에 배치하고, 가운데·아래 작은 구름이 큰 구름 뒤를 따라 걷히며 마지막에는 아래쪽 작은 구름만 여운을 남긴다. 작은 구름은 `reverseOnOpen`으로 들어오는 경로와 걷히는 방향을 분리하며, 위·아래는 왼쪽, 가운데는 오른쪽으로 걷힌다. 각 구름의 `closeDelay`·`openDelay`는 해당 구간 길이의 비율(0~0.4)이며 서로 다른 시작·도착 시점과 진행도로 움직이되 닫힘 끝에는 모두 완전히 덮고 걷힘 끝에는 모두 화면 밖으로 나간다. 연속 입력을 잠그고, 구름이 걷히는 동안 뒤에서 다음 컷 그림이 펼쳐지고, 글자는 구름과 그림 연출이 끝난 뒤 표시한다. 전환용 중간 프레임을 재생 목록에 넣을 필요는 없다.
- 구미호·야광귀·지귀 후일담의 그림 속 문구는 각 View에 편집 가능한 Text로 남겨뒀다. `StoryVisualBeat`의 Delay·Duration에서 그림이 끝난 뒤 대사까지의 여유를 조절한다. 후일담의 인물과 대사는 제자리에서 투명도를 천천히 올린다.
- 엔딩 6컷은 1.05초 동안 원본 암전 색으로 어두워진 뒤 0.30초 쉬고 “요괴 봉인은 계속된다. / 투 비 컨티뉴드 -”가 나타난다. 제작용 `Ending` 목록의 타이틀은 이어서 밝아진다. 실제 `GameEnding`은 기존 6컷과 종료 이벤트를 사용하므로 게임 흐름을 연결할 때 중복 타이틀 컷을 추가하지 않는다.
- 새 대사와 화살표의 Figma 배치 정보는 `PolishFrames.json`, 구름과 화살표 원본은 `Art/Story/StageStories/Polish/`에 있다.

## 튜토리얼에서 공통 구름 효과 사용

공통 프리팹은 `Assets/Hapio/Story/StageStories/Views/StoryCloudWipe.prefab`이다. `StoryPlayer` 없이 기존 Canvas 아래에 배치하고 `StoryCloudWipe.TryPlay`를 호출할 수 있다. 큰 구름 PNG 5개와 제공된 작은 구름 PNG 1개를 총 9개의 Image에서 공유하며, 완전히 닫힐 때 기존 한지 배경이 틈을 가린다. 402×874 원본 비율을 유지하며 부모 영역을 덮도록 확대하고 바깥은 RectMask2D로 자른다.

1. GraphicRaycaster가 있는 Canvas 안에 화면 전체 크기의 전환용 부모를 둔다. Safe Area 안쪽이나 LayoutGroup 아래가 아니라 전체 화면을 덮는 영역을 사용한다.
2. 공통 프리팹을 그 아래 배치하고 아래 `cloudWipe` 필드에 인스턴스를 연결한다. 교체할 페이지와 전환 오버레이는 별도 형제 객체로 둔다.
3. 다음 페이지는 **onCovered에서만** 교체한다. 완료 후 입력·안내를 열고, 튜토리얼을 닫을 때 Cancel한다.

```csharp
[SerializeField] private Hapio.StoryCloudWipe cloudWipe;
[SerializeField] private GameObject currentPage;
[SerializeField] private GameObject nextPage;

public void NextTutorialPage()
{
    // 재생 중에는 false를 반환하며 기존 콜백을 덮어쓰지 않는다.
    cloudWipe.TryPlay(onCovered: () =>
    {
        currentPage.SetActive(false);
        nextPage.SetActive(true);
        // 새 페이지가 hierarchy 순서를 바꾸더라도 오버레이를 위에 유지한다.
        cloudWipe.transform.SetAsLastSibling();
    });
}

private void OnDisable()
{
    if (cloudWipe != null) cloudWipe.Cancel();
}
```

`TryPlay(onCovered, onCompleted)`는 unscaled time으로 재생한다. onCovered는 화면이 완전히 가려진 뒤 한 번, onCompleted는 완전히 걷힌 뒤 한 번 실행된다. 느린 프레임에서도 완전 가림 상태를 한 프레임 유지한다. 비활성화·Cancel은 남은 콜백을 버리고 구름과 입력 차단을 해제한다. 화면 전환 후 같은 인스턴스로 다시 재생할 수 있다. 콜백에서 오버레이 자체나 그 부모를 끄면 걷힘 연출도 취소되므로 페이지 내용만 교체한다.

재생 중 GraphicRaycaster 입력은 오버레이가 차단한다. 키보드·직접 호출 등 별도 입력 경로는 `IsPlaying`으로 막는다. 이 컴포넌트는 로컬 화면 교체용이며, 비동기 Scene 로드 완료 대기나 네트워크 동기화는 호출 측에서 처리해야 한다. Inspector의 Close Seconds / Covered Seconds / Open Seconds로 시간을 조절할 수 있고, 공통 프리팹 저장값은 0.62 / 0.10 / 0.80초다.

기존 StoryCutView에는 `Exit Cloud Wipe`만 연결하면 된다. StoryPlayer가 수동 시간 진행을 담당하므로 별도로 TryPlay를 중복 호출하지 않는다. 엔딩 마지막 암전·문구·타이틀 페이드는 기존 전용 레이어 타이밍을 유지한다.

## 봉인 실패와 도달 봉인율

`Failure_01.prefab`은 배경·캐릭터·박을 첫 프레임에 모두 표시한다. 글자만 0.42초 뒤 ‘텅’ 소리와 함께 나타나 크기가 잠깐 커졌다가 제자리로 돌아온다.

`Failure_Dashboard.prefab`의 **StorySealGauge**에서 `Target`(0~1), `Duration`, `Delay`를 조정한다. 기본은 Figma의 78% 예시, 0.15초 뒤부터 1.35초 동안 0→목표값이다. 숫자와 막대는 같은 값을 사용하고 목표값에 닿으면 멈추고 숫자가 한 번 강조된다. 재도전·나가기 버튼은 게이지가 차는 중에도 누를 수 있다. 재표시하거나 `Show(value)`를 호출하면 0부터 다시 찬다. 실제 봉인율 판정·전투 상태는 수정하지 않는다.

```csharp
// 결과를 확정한 팀원의 코드에서, 0~1 범위의 실제 봉인율을 넘긴다.
gauge.Show(normalizedSealRate);
```

게이지와 카드 모두 `Time.unscaledDeltaTime`을 사용하므로 게임 시간 정지와 독립적으로 표시된다. UI 먹선의 반투명 색은 밝은 한지 위의 Figma 색 혼합에 가깝도록 전용 재질로 보정한다. 이는 밝은 종이 배경을 위한 표현이며 전투 판정용 재질이 아니다.

## 팀원이 게임 흐름에 연결할 때

기존 로컬 `StoryPlayer.Play(sequence)`와 `Finished(StoryExitReason)`를 사용한다. 재생 전 종료 이벤트를 구독하고 완료/스킵 때 해제한 뒤 다음 단계로 이동한다. 같은 실행에서 새 이야기를 재생할 때도 해당 목록을 지정하면 된다. 실제 연결·Host 권한·참가자 결과 준비·멀티플레이 동기화는 이번 제작용 묶음에 추가하지 않았다.

카드/실패 화면을 결과 UI에 따로 사용할 때는 해당 View Prefab을 기존 Canvas에 넣고 필요한 데이터를 설정한다. 실패 화면은 `StorySealGauge.Show`로 봉인율을, 카드 기록은 `StoryCardRecords.Bind`로 경기 통계를 연결한다. 카드·실패 제목·다음 요괴 예고의 등장 연출은 `StoryPlayer`가 컷 시간에 맞춰 재생한다. `StoryResultActions`의 `onInspectGourd` / `onRetry` / `onExit`에 실제 관람·재도전·나가기 흐름을 연결한다. 제작용 미리보기의 재도전은 실패 목록을 다시 재생하고 나가기는 전체 목록으로 돌아간다.

엔딩 마지막 어두워지는 컷은 기존 레이어 페이드로 표현한다. 다음 컷은 Figma 타이틀이며, 기존 앱의 Home 타이틀이나 실행 내 오프닝 재생 여부는 바꾸지 않는다.

원본: [스테이지 사이 및 엔딩 Figma](https://www.figma.com/design/wW7ny7lNQ4DXNJ4kD46AuR/?node-id=4210-3456). Figma 파일은 읽기만 했다.

## 내 칭호 강조

부적 카드 뒷면과 패배 대시보드의 칭호 표는 실행 중 `StoryRecordHighlight`가 처리한다. 프리팹에 저장된 시안의 빨간 띠 2개(1·4행)를 끄고, 이 기기의 플레이어가 받은 칭호 줄마다(동점 포함) 띠를 복제해 얹는다. 해당 줄의 글자는 밝은색, 나머지는 어두운색이며 내 이름에는 `(나)`를 붙인다. 받은 칭호가 없으면 강조하지 않는다. 패배 대시보드에는 `StoryCardRecords`가 없어 같은 데이터로 누구·기록 칸도 채운다. 줄은 저장된 칭호 글자로 찾으므로 프리팹의 칭호 글자나 `Rectangle 124` 이름을 바꾸면 강조가 동작하지 않는다. 프리팹과 Editor 미리보기에는 시안 띠가 그대로 남는다.
