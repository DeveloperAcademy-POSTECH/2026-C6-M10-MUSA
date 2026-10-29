# 요괴 콘텐츠 파일 구조와 편집 안내

이 문서는 #381에서 준비한 **10종의 제작용 목록**과 **현재 전투에 연결된 3종**을 구분한다. 추가 7종은 이름·식별자·출처를 저장할 자리를 만든 상태이며, 새로운 전투나 공격 패턴을 구현한 상태가 아니다. 실제 검사 수행 여부와 결과는 [검증 기록](VALIDATION.md)을 확인한다.

## 1. 현재 등록 범위

| Figma 참고 순번 | 표시명 | 고정 ID `contentId` | 폴더명 | 구현 상태 | 기존 전투 단계 |
| --- | --- | --- | --- | --- | --- |
| 1 | 구미호 | `gumiho` | `Gumiho` | `Integrated` | 1 |
| 2 | 장산범 | `jangsanbeom` | `Jangsanbeom` | `Planned` | 연결 없음 |
| 3 | 야광귀 | `yaguang` | `Yaguang` | `Integrated` | 2 |
| 4 | 거구귀 | `geogugwi` | `Geogugwi` | `Planned` | 연결 없음 |
| 5 | 어둑시니 | `eoduksini` | `Eoduksini` | `Planned` | 연결 없음 |
| 6 | 두억시니 | `dueoksini` | `Dueoksini` | `Planned` | 연결 없음 |
| 7 | 그슨대 | `geuseundae` | `Geuseundae` | `Planned` | 연결 없음 |
| 8 | 지귀 | `jigwi` | `Jigwi` | `Integrated` | 3 |
| 9 | 창귀 | `changgwi` | `Changgwi` | `Planned` | 연결 없음 |
| 10 | 지하국대적 | `jihagukdaejeok` | `JihagukDaejeok` | `Planned` · 보스 표시 | 연결 없음 |

그슨대의 별칭에는 `그신새`, `그신대`를 저장한다. 지하국대적의 `isBoss`는 참고 자료의 보스 표기를 기록한 값이며, 이 값만으로 별도 보스 전투가 실행되지는 않는다.

현재 3종은 개별 전용 모델 대신 기존의 **공통 검은 요괴 모델**을 유지한다. `Integrated`는 기존 스테이지에 연결되었다는 콘텐츠 상태다. 개별 모델·초상·음성·VFX까지 제작 완료되었다는 뜻이 아니다.

## 2. 파일은 어디에 있는가

```text
Assets/Hapio/
├── Content/Yokai/                       # 제작용 목록. Resources 바깥에 둔다.
│   ├── YokaiCatalog.asset               # 10종 정의 참조
│   ├── Gumiho/Gumiho.asset
│   ├── Jangsanbeom/Jangsanbeom.asset
│   └── ...                              # 위 표의 폴더명/폴더명.asset
├── Scripts/Yokai/
│   ├── GameYokaiDefinition.cs            # 한 종의 식별 정보와 선택적 에셋 참조
│   └── GameYokaiCatalog.cs               # 제작용 목록 검증·ID 조회
├── Scripts/Core/
│   ├── GameStageDefinition.cs           # 현재 전투 한 단계의 규칙·기본값
│   └── GameStageCatalog.cs              # 기존 3단계 실행 목록
├── Resources/Hapio/
│   ├── StageCatalog.asset
│   └── Stages/
│       ├── Stage01_gumiho.asset
│       ├── Stage02_yaguang.asset
│       └── Stage03_jigwi.asset
└── Editor/Yokai/
    └── GameYokaiCatalogValidator.cs      # 생성·저장 데이터 검사 메뉴
```

`YokaiCatalog`는 제작 자료를 모은 목록이다. 여기에 항목을 추가하거나 상태를 바꾸는 것만으로 전투 선택지나 스테이지 수가 늘어나지 않는다. 실행 목록의 원본은 기존 `Resources/Hapio/StageCatalog.asset`이다.

기존 `GameStageDefinition`에는 `content` 참조가 추가된다. 구미호·야광귀·지귀의 각 스테이지는 같은 ID의 `GameYokaiDefinition`을 가리킨다. 단계 번호·`LabStageYokai`·전투 수치·개별 모델 적용은 계속 기존 스테이지 구조가 담당한다.

## 3. 비슷해 보이는 세 번호의 차이

| 필드 | 의미 | 편집 기준 |
| --- | --- | --- |
| `contentId` | 요괴를 찾는 고정 식별자 | 소문자 영문으로 시작하고 소문자 영문·숫자·밑줄만 사용한다. 표시 이름이나 Figma 순서가 바뀌어도 유지한다. |
| `referenceOrder` | Figma 요괴 목록의 참고 순번 | 전투 순서를 뜻하지 않는다. 예: 지귀는 8이다. |
| `stageIndex` | 현재 게임에서 진행하는 단계 | 기존 전투 순서 구미호 1 → 야광귀 2 → 지귀 3을 유지한다. |

예를 들어 지귀는 `contentId = jigwi`, `referenceOrder = 8`, 기존 스테이지의 `stageIndex = 3`이다. Figma에서 여덟 번째라는 이유로 전투를 8스테이지로 바꾸지 않는다.

## 4. Inspector에서 확인·편집하기

1. Unity의 Project 창에서 `Assets/Hapio/Content/Yokai`를 연다.
2. 확인할 요괴 폴더 안의 `.asset` 파일을 선택한다. 예: `Jangsanbeom/Jangsanbeom.asset`.
3. Inspector에서 아래 필드를 확인한다. `.cs` 파일이 아닌 `.asset`을 선택해야 저장된 실제 값이 보인다.
4. 필요한 편집 후 `Hapio > Yokai > Validate Content Catalog`를 실행한다. 오류가 있으면 Console에 나온 항목부터 확인한다. 자동 복구 메뉴는 아니다.

| Inspector 필드 | 용도와 현재 상태 |
| --- | --- |
| Content Id | 위 표의 고정 ID. 기존 3종은 연결된 `stageId`와 같아야 한다. |
| Display Name / Aliases | 표시 이름과 별칭. 이름은 필수이며 별칭은 비워둘 수 있다. 빈 별칭·중복 별칭·표시명과 같은 별칭은 허용하지 않는다. |
| Reference Order / Reference Url | Figma 참고 순번과 개별 노드 링크. |
| Description | 설명을 저장할 자리. 현재 10종 모두 빈 값이다. 확인되지 않은 설화나 공격 규칙을 채워 넣지 않는다. |
| Is Boss | 참고 자료의 보스 표기. 지하국대적만 켜져 있다. |
| Implementation State | `Planned` / `InDevelopment` / `Integrated`. 현재 범위는 기존 3종 `Integrated`, 추가 7종 `Planned`다. |
| Visual Prefab / Portrait | 향후 모델·초상 참조를 보관할 선택적 필드. 비어 있어도 정의 자체는 유효하다. |
| Audio Clips / Effects | 향후 소리·효과 참조 배열. 비어 있어도 된다. |

현재 저장 목록 검사는 표의 이름·순번·보스·구현 상태와 기존 3종의 연결을 확인한다. 따라서 제작을 시작했다고 Inspector의 상태만 바꾸면 현 범위 검사에서 차이가 보고될 수 있다. 후속 기능 작업에서는 데이터·검사 기준·문서를 함께 갱신해야 한다.

`GameYokaiDefinition.visualPrefab`은 **제작용 참조**다. 여기에 Prefab을 넣어도 게임 모델이 자동으로 바뀌지 않는다. 현재 화면의 모델 연결은 `GameStageDefinition.yokaiVisual`과 [GameYokaiPresentation](../Assets/Hapio/Scripts/UI/GameYokaiPresentation.cs)이 담당한다. 기존 3종의 `yokaiVisual`은 비워 공통 모델을 계속 사용한다. 콘텐츠 준비를 위해 이 참조를 임의로 교체하지 않는다.

## 5. 생성·검사 메뉴 사용법

### Hapio > Yokai > Create Missing Definitions

개발용 생성 도구다. 정해진 경로에서 없는 요괴 정의와 없는 카탈로그만 만든다.

- 기존 정의의 이름·설명·에셋 참조 등은 덮어쓰지 않는다.
- 기존 카탈로그의 Entries도 다시 채우거나 재정렬하지 않는다.
- 기존 3종 스테이지의 `content`가 비어 있을 때는 해당 요괴 정의를 연결한다. 이미 있는 참조는 바꾸지 않는다.
- 같은 경로에 다른 종류의 에셋이나 사용할 수 없는 경로가 있으면 오류로 중단한다. 기존 파일을 삭제해 대체하지 않는다.
- 이 도구를 반복 실행해도 누락 항목이 없으면 새 정의를 추가하지 않는다. 잘못 편집한 기존 값의 복구는 별도 확인이 필요하다.

일부 정의를 새로 만들었지만 기존 카탈로그에서 참조가 빠진 경우도 자동으로 Entries를 고치지 않는다. 검사 결과를 확인하고 의도한 변경인지 판단한 뒤 Inspector에서 연결한다.

### Hapio > Yokai > Validate Content Catalog

저장된 에셋을 읽어 다음 조건을 검사한다. 검사 통과는 플레이 테스트나 iPhone 검증을 뜻하지 않는다.

- 요괴 정의의 ID·이름·순번·상태·별칭 형식이 유효하다.
- 카탈로그의 참조가 비어 있지 않고 ID·참고 순번이 중복되지 않는다.
- 저장된 목록이 위 10종과 경로·순서·이름·보스·현재 상태에 맞고 필수 별칭을 포함한다.
- 제작용 카탈로그가 `Resources` 밖에 있다.
- 실행 목록은 기존 3단계 순서와 경로를 유지하며 각 단계가 같은 ID의 `Integrated` 정의를 참조한다.

일반 `GameYokaiCatalog.Validate()`는 재사용 가능한 목록 검사여서 10개를 강제하지 않는다. **현재 저장 목록이 정확히 10종인지** 확인하는 것은 위 Editor 메뉴와 해당 검사 코드다.

## 6. 아트가 생겼을 때의 폴더 관례

콘텐츠 데이터와 제작 아트는 분리한다. 향후 아트가 실제로 준비되면 아래 경로를 사용한다.

```text
Assets/Hapio/Art/Yokai/<위 표의 폴더명>/
├── Models/
├── Materials/
├── Textures/
├── Prefabs/
├── Animations/
├── Audio/
└── VFX/
```

이는 예정 경로 관례다. **이번 작업에서는 빈 아트 폴더를 미리 만들지 않는다.** 실제 파일이 필요한 폴더부터 Unity Project 창에서 만든다. 예를 들어 장산범 모델이 생기면 `Art/Yokai/Jangsanbeom/Models`를 만들고, 조립한 Prefab은 `Prefabs`에 둔다. 폴더·에셋을 이동할 때는 Unity 안에서 처리하고 `.meta`도 함께 유지해 참조가 끊기지 않게 한다.

아트 참조를 저장하는 것과 전투에 적용하는 작업은 별개다. 후속 적용 시 모델 위치·크기·기존 도색 표면·피격 Collider가 보존되는지 따로 확인한다.

## 7. 참고 자료와 아직 비워 둔 정보

Figma의 요괴 설명 페이지는 이번 범위에서 **이름·별칭·보스 표기·출처 링크** 확인에 사용했다. 개별 링크는 각 정의의 `referenceUrl`과 [생성 도구의 Roster](../Assets/Hapio/Editor/Yokai/GameYokaiCatalogValidator.cs)에 있다.

7번 그슨대 옆 설명은 망태기할아버지에 관한 내용으로 보이는 불일치가 있어, 그슨대의 설명으로 확정하지 않았다. 설화 본문과 실제 공격 규칙은 확인이 필요하다. 이 때문에 그슨대뿐 아니라 10종 전체의 `description`을 비워 두었다. 빈 값을 완성된 설정으로 해석하거나 임의의 전승·수치를 넣지 않는다.

추가 7종의 전용 패턴, `LabStageYokai` 확장, 전투 단계 추가, 선택 UI, 네트워크 메시지·프로토콜 변경은 이번 범위 밖이다. `Planned` 항목은 기존 전투 실행 목록에 들어가지 않는다.

## 8. 확인 시나리오와 예상 기준

아래는 수행 결과가 아니라 확인 절차다. 실행한 환경·버전·결과는 [검증 기록](VALIDATION.md)에 별도로 남긴다.

| 시나리오 | 확인 행동 | 예상 기준 |
| --- | --- | --- |
| 저장 목록 | Validate Content Catalog 실행 | 10종 메타데이터·7종 Planned·기존 3종 연결이 저장 기준과 일치한다. |
| 반복 생성 | 누락 없는 상태에서 Create Missing Definitions를 다시 실행 | 기존 정의·카탈로그 값과 이미 연결된 stage.content를 변경하지 않는다. |
| 제작 중 편집 보존 | 테스트용 복사 프로젝트에서 설명을 편집한 뒤 생성 메뉴 재실행 | 기존 설명을 초기값으로 덮어쓰지 않는다. |
| 잘못된 데이터 | 테스트용 정의에 잘못된 ID·중복 순번·빈 참조 등을 설정해 검증 | 해당 오류를 보고하고 유효한 목록으로 간주하지 않는다. 검사가 끝나면 시험 데이터를 복구한다. |
| 기존 게임 회귀 | 기존 게임에서 구미호 → 야광귀 → 지귀를 진행 | 3단계 순서·각 패턴·도색·기존 공통 모델이 유지되고 Planned 요괴가 끼어들지 않는다. |
| 멀티플레이 회귀 | 기존 연결 경로로 같은 방에서 게임 진행 | 이번 제작용 데이터 추가가 인원·소유권·피격·동기화 동작을 변경하지 않는다. |

관련 자동 검사의 원본은 [GameYokaiCatalogTests](../Assets/Hapio/Tests/EditMode/GameYokaiCatalogTests.cs)다. 검사 코드가 존재한다는 이유로 실행 완료로 기록하지 않는다.

## 9. 후속 작업에서 먼저 볼 코드

- 데이터 필드·정의 검사: [GameYokaiDefinition](../Assets/Hapio/Scripts/Yokai/GameYokaiDefinition.cs)
- 제작용 목록·ID 조회: [GameYokaiCatalog](../Assets/Hapio/Scripts/Yokai/GameYokaiCatalog.cs)
- 저장 10종과 생성 경로: [GameYokaiCatalogValidator](../Assets/Hapio/Editor/Yokai/GameYokaiCatalogValidator.cs)
- 현재 전투 단계·콘텐츠 연결: [GameStageDefinition](../Assets/Hapio/Scripts/Core/GameStageDefinition.cs), [GameStageCatalog](../Assets/Hapio/Scripts/Core/GameStageCatalog.cs)
- 실제 게임 진입·진행: [게임 흐름](GAME_FLOW.md)

표시 이름 수정, 아트 참조 저장, 새로운 전투 구현은 영향 범위가 다르다. 변경할 대상에 맞는 원본을 확인하고, 제작용 데이터의 존재를 실행 기능의 완성으로 간주하지 않는다.
