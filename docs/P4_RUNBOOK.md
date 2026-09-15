> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# P4 좌우 연속 이동 · 빌드24 실행 안내

구슬이 좌우 끝에 닿으면 이웃 화면으로 넘어가고, 남은 속도가 마찰로 줄어들 때까지 이어서 움직이는 `ContinuousTransferBattle`의 사용 절차다. 2~5인 연결과 직접 드래그 조합, 손떼기 3D 투척을 함께 사용한다. **과거의 실제 실행 결과·미실행 항목은 [P4 검증 보고서](P4_VALIDATION.md)를 기준으로 확인한다.** 이 문서는 실행 방법이며, 작성 자체를 빌드나 실기기 확인 완료로 기록하지 않는다.

Unity를 처음 사용한다면 아래 메뉴 순서부터 따라간다. Xcode 화면과 기기 준비를 더 자세히 보려면 현재 P4에 맞춘 [직접 빌드 README](../BUILD_README.md)를 함께 읽는다. 과거 보고서의 다른 단계 메뉴와 섞지 않고 **P4 메뉴·빌드24**를 사용한다.

## 1. 기존 Unity 프로젝트와 새 씬 열기

1. Unity Hub에서 내려받은 조직 저장소의 루트 폴더를 **Unity 6000.5.7f1**로 연다. `Assets`, `Packages`, `ProjectSettings`가 나란히 있는 폴더이며, `/path/to/2026-C6-M10-MUSA` 같은 예시는 자신의 실제 위치로 바꾼다. 같은 폴더가 이미 Editor에 열려 있다면 그 창을 사용한다.
2. 편집 중인 씬·에셋의 변경을 저장하고 Play 모드를 종료한다. Import와 컴파일이 끝날 때까지 기다린다.
3. **C6 → Next Phase → P4 → Prepare Continuous Transfer Battle**을 선택한다.
4. Project 창에서 `Assets → _Project → HapioMVP → Scenes → ContinuousTransferBattle.unity`를 더블 클릭한다.
5. Play를 눌러 로비를 살펴본다. 실제 협동 전투에는 같은 버전의 실행본이 최소2개 필요하다.

Prepare는 처음에 저장된 `FivePlayerBattle`을 Unity의 에셋 복사 API로 별도 씬으로 만들고, 새 씬의 연속 전달 설정을 켠다. 이미 새 씬이 있으면 덮어쓰지 않고 필요한 참조와 설정을 검사한다. 기존 `FivePlayerBattle` 빌드23과 과거 씬·메타·프리팹은 유지한다. 새 씬의 기준 몬스터와 `ScreenLayoutConfig.asset`도 기존 자산을 참조한다.

빌드24는 로비·게임 프로토콜24를 사용한다. **모든 참여자는 같은 빌드24와 설정을 사용한다.** 빌드23 실행본과 한 방에 섞어 접속할 수 없다. Unity·렌더러·패키지 버전을 이 작업 때문에 변경하지 않는다.

## 2. Unity 메뉴로 Mac 또는 iOS 빌드하기

**File → Build Profiles**에서 원하는 플랫폼을 먼저 활성화한다. iOS가 선택된 상태로 Mac 빌드 메뉴를 누르는 것처럼 서로 다른 대상을 섞으면 빌더가 중단한다.

| 대상 | Unity 메뉴 | 환경 변수가 없을 때의 기본 출력 |
|---|---|---|
| macOS | C6 → Next Phase → P4 → Build macOS Continuous Transfer Battle | `Builds/NextPhase/P4/macOS/C6ContinuousTransfer.app` |
| iPhone·iPad | C6 → Next Phase → P4 → Export iOS | `Builds/NextPhase/P4/iOS/Unity-iPhone.xcodeproj` |

Unity 출력은 프로젝트 폴더를 기준으로 한다. `C6_P4_OUTPUT_ROOT`를 지정한 프로세스에서는 지정 폴더 아래에 `macOS` 또는 `iOS`가 생성된다. 기존 출력이 있는 경로에는 덮어쓰지 않는다. 반복 빌드에는 아래 CLI 예시처럼 매번 새로운 출력 폴더를 사용한다. 이미 켜진 Editor는 나중에 다른 터미널에 설정한 환경 변수를 자동으로 받지 않는다.

Mac 앱의 식별자는 `com.wolfuraark.c6prototype.p4.desktop`, iOS 식별자는 `com.wolfuraark.c6prototype`, iOS Build는 `24`다. 빌더의 실행 결과 JSON은 `Logs/NextPhase/P4/`에 남는다. iOS Export의 결과는 Xcode 프로젝트이며, 기기에 설치할 서명 앱은 다음 단계에서 만든다.

## 3. Xcode에서 iPhone·iPad에 설치하기

1. iPhone 또는 iPad를 Mac에 연결하고 잠금을 해제한다. 처음 연결하면 Mac 신뢰를 승인하고, 개발자 모드를 켠 뒤 재시동 확인까지 마친다.
2. 앞에서 생성한 `iOS/Unity-iPhone.xcodeproj`를 Xcode로 연다.
3. Scheme은 **Unity-iPhone**, 실행 대상은 연결한 실제 iPhone 또는 iPad를 선택한다.
4. 앱 Target의 **Signing & Capabilities**에서 **Automatically manage signing**을 켜고 **본인이 사용할 수 있는 Apple 개발 Team**을 선택한다. `YOUR_TEAM_ID`는 실제 값이 아니므로 입력하지 않는다. 기본 Bundle Identifier는 `com.wolfuraark.c6prototype`이며, 자신의 Team에서 등록할 수 없다면 앱 Target에 사용 가능한 고유 ID를 지정한다. 다음 Unity Export 때 빌더가 기본 ID를 다시 설정하므로 새 출력마다 확인한다. 영구 변경 지점은 [ContinuousTransferBuild.Prepare()](../Assets/_Project/HapioMVP/Editor/ContinuousTransferBuild.cs)다.
5. **Run**을 누르고 앱 빌드·서명·기기 설치가 끝날 때까지 기다린다. 기기 화면에 C6 로비가 실행되는지 확인한다.
6. 두 번째 기기에도 같은 출력의 앱을 설치한다. 앱 이름이 같다는 것만으로 빌드가 같다고 판단하지 말고 Xcode의 Build 값과 [현재 검증 기록](P4_VALIDATION.md)을 확인한다.

기기 잠금·Mac 신뢰·개발자 모드가 준비되지 않았다면 해당 기기에서 해결한 뒤 다시 Run한다. 생성된 앱·서명 자료·원시 로그는 GitHub에 포함하지 않는다. 앱 컴파일·서명, 설치·실행, 실제 화면·터치는 별도 확인 항목이다.

## 4. 2~5명이 함께 시작하기

모든 기기를 같은 Wi-Fi에 연결하고 C6 앱 화면을 켜 둔다. Mac 앱도 한 명으로 참여할 수 있다.

1. Host가 **CREATE ROOM**을 누른다. Host는 P1이 된다.
2. 나머지는 **FIND ROOMS → 같은 방의 JOIN**을 누른다. 최초 로컬 네트워크 권한 알림이 나오면 허용한다. 방을 찾지 못할 때는 DIRECT IP에서 Host의 현재 Wi-Fi IPv4와 같은 Port를 입력한다. 기본 Port는7777이며 과거 시험에 사용한 IP를 그대로 고정하지 않는다.
3. 입장 순서의 P1~P5와 LEFT·RIGHT 이웃을 확인한다. 참여할 사람이 모두 들어온 뒤 각자 **I'M READY**, 이어 Host가 **HOST START**를 누른다.
4. 전원의 최초 상태 확인이 끝나면 전투가 시작된다. 시작은 구슬0개·스태미나100·몬스터 HP100·시간180초다.

세 명일 때 연결은 P1 → P2 → P3 → P1 순서다. 오른쪽으로 나가면 다음 사람의 왼쪽에, 왼쪽으로 나가면 이전 사람의 오른쪽에 도착한다. 두 명이면 좌우 이웃은 같은 상대다. 로비에서 누군가 나가면 번호가 정리되고 전원의 Ready가 해제된다. 전투 중 이탈은 기존 방 종료 정책을 따른다.

## 5. 바뀐 구슬 조작

**GENERATE / 20**을 누르면 스태미나20을 사용해 Raw 한 개가 생긴다. 스태미나는 최대100이며 시간 회복은3초당20이다.

- **굴리기:** 구슬을 잡아 왼쪽이나 오른쪽으로 움직이면서 손을 놓는다. 손을 멈춘 뒤 놓으면 움직임이 남지 않거나 매우 약하다. 구슬을 잡고 있는 동안에는 화면 밖으로 자동 전달되지 않는다.
- **화면 통과:** 놓은 구슬이 관성이나 다른 구슬과의 접촉으로 움직이다 좌우 끝에 닿으면 해당 이웃에게 전달된다. 전달할 때마다 다시 잡거나 손을 놓을 필요는 없다. 같은 구슬이 반대쪽 경계에 도착해 남은 속도로 계속 굴러간다.
- **마찰과 충돌:** 이동 중 속도는 마찰로 줄어든다. 충분한 속도가 있으면 여러 화면을 지나며, 느려지면 도착한 화면 안에서 멈춘다. 상하 경계와 다른 구슬에는 기존처럼 반발한다. 전달 중 경과 시간에도 마찰을 반영하므로 네트워크를 통과할 때 속도가 늘어나지 않는다.
- **직접 조합:** Raw Yin과 Yang을 직접 드래그해 겹친 뒤 정상적으로 손을 놓으면 Combined가 된다. 자연스럽게 부딪히거나 같은 음양이 겹쳤다는 이유만으로 합쳐지지 않는다.
- **3D 공격:** Combined를 상단 전투 영역으로 올리고 위쪽 움직임을 주면서 손을 놓는다. 손떼기 방향·속도로 투척하며 실제 몬스터 피격만 공통 HP를20 줄인다. 실제 공격자는 스태미나5를 최대100까지 회복한다. Raw는 공격할 수 없다.

받는 사람의 보관 한도20개가 가득 차 전달이 거부되면 기존 구슬은 보내는 쪽 경계에 멈춘다. 구슬을 복제하거나 반복 요청으로 밀어 넣지 않는다. 이 경우 공간을 확보한 뒤 다시 밀 수 있다. 화면 크기·Safe Area·물리 설정을 실행 중 바꾸면 기존 조작과 속도는 정리되므로, 조작감 비교는 같은 화면 조건에서 한다.

## 6. 선택: Unity CLI로 반복 준비·테스트·빌드하기

이번 작업에는 [Unity 공식 skills 저장소](https://github.com/Unity-Technologies/skills)의 `unity-cli` 스킬을 참고했다. 로컬 CLI는 **1.0.0-beta.8**이며 아래 옵션은 해당 설치본의 `run --help`, `test --help`, `build --help`를 기준으로 작성했다. 스킬 문서가 더 최신 CLI를 설명할 수 있으므로 설치본의 도움말을 먼저 확인한다.

현재 프로젝트에는 연결된 Editor를 제어하는 `com.unity.pipeline` 패키지가 없다. 따라서 live `unity command` 대신, 별도 작업 폴더에서 CLI가 Editor를 실행하고 저장된 C# 빌더와 Test Runner를 호출한다. 새 패키지를 추가하지 않아도 `unity run`·`unity test`와 아래 빌드 메서드를 사용할 수 있다. **같은 프로젝트에 Editor와 일괄 실행을 중복으로 열지 않는다.** 원본 Editor는 그대로 두고 아래처럼 별도 사본을 사용한다.

먼저 원하는 변경을 검토·커밋한 뒤, 아직 없는 위치에 빌드용 worktree를 만든다. 다음 명령은 HEAD에 커밋된 파일만 포함하며 원본의 미커밋 수정은 복사하지 않는다. 이미 같은 위치가 있으면 새 번호를 사용한다.

```bash
git -C '/path/to/2026-C6-M10-MUSA' worktree add --detach \
  '/private/tmp/C6_Prototype_P4_ManualBuild_01' HEAD
```

이후 같은 터미널에서 경로를 설정한다. `C6_CLI`에는 자신의 Unity CLI 실행 파일 경로를 넣는다. `command -v unity`로 찾을 수 있다면 그 결과를 사용하고, 설치 위치가 다르면 실제 파일 위치를 확인한다. 아래 `/path/to/...` 예시는 그대로 실행할 수 있는 경로가 아니다. `C6_CHECK_ROOT`는 이번 실행의 새 로그·출력 폴더다. 이미 사용한 폴더가 있다면 다음 번호를 선택한다.

```bash
C6_CLI='/path/to/developer-home/.unity/bin/unity'
C6_EDITOR_APP='/Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app'
C6_BUILD_PROJECT='/private/tmp/C6_Prototype_P4_ManualBuild_01'
C6_CHECK_ROOT='/private/tmp/c6-p4-manual-check-01'
mkdir "$C6_CHECK_ROOT"
```

beta.8의 `--editor-path`에는 위의 **`Unity.app` 경로**를 넣는다. CLI 도움말에 binary라고 표시되더라도 이번 설치 환경에서는 `.app` 내부 `Contents/MacOS/Unity` 실행 파일 경로와 구분해야 한다.

아래 명령은 한 번에 하나씩 실행하고, 앞 명령의 종료와 결과를 확인한 뒤 다음으로 진행한다. `--timeout`은 지정하지 않는다. CLI timeout은 프로세스를 강제로 종료할 수 있으므로 기존 프로젝트의 강제 종료 금지 원칙에 맞춰 세 timeout 환경 변수도 각 호출에서 해제한다. 실행이 오래 걸려도 기존 Editor를 종료하거나 `Library`·lock 파일을 지워 해결하지 않는다.

### 새 씬 준비

```bash
env -u UNITY_RUN_TIMEOUT -u UNITY_TEST_TIMEOUT -u UNITY_BUILD_TIMEOUT \
  "$C6_CLI" run "$C6_BUILD_PROJECT" \
  --editor-path "$C6_EDITOR_APP" --format json \
  -- -buildTarget StandaloneOSX \
  -executeMethod C6.Editor.ContinuousTransferBuild.Prepare \
  -logFile "$C6_CHECK_ROOT/prepare.log"
```

`unity run`은 batchmode·projectPath·quit를 관리한다. `--` 뒤에 `-batchmode`, `-projectPath`, `-quit`를 다시 넣지 않는다. 씬 YAML은 직접 수정하지 않고 이 빌더가 Unity API로 준비하게 한다.

### EditMode·PlayMode 검사

```bash
env -u UNITY_RUN_TIMEOUT -u UNITY_TEST_TIMEOUT -u UNITY_BUILD_TIMEOUT \
  "$C6_CLI" test "$C6_BUILD_PROJECT" \
  --editor-path "$C6_EDITOR_APP" --format json \
  --mode EditMode --output "$C6_CHECK_ROOT/edit.xml" \
  -- -buildTarget StandaloneOSX -logFile "$C6_CHECK_ROOT/edit.log"
```

```bash
env -u UNITY_RUN_TIMEOUT -u UNITY_TEST_TIMEOUT -u UNITY_BUILD_TIMEOUT \
  "$C6_CLI" test "$C6_BUILD_PROJECT" \
  --editor-path "$C6_EDITOR_APP" --format json \
  --mode PlayMode --output "$C6_CHECK_ROOT/play.xml" \
  -- -buildTarget StandaloneOSX -logFile "$C6_CHECK_ROOT/play.log"
```

`unity test`에도 `-quit`를 추가하지 않는다. Test Runner가 결과를 기록하고 종료하도록 둔다. 종료 코드만 보지 말고 XML의 실행 수·실패·누락·skip을 확인한다. 테스트0개·결과 파일 없음은 통과가 아니다. 수정 후 다시 검사할 때는 새 출력 폴더를 써서 실패 기록을 보존한다.

### Mac 앱 빌드

아래 명령은 검토한 빌드 사본에 Prepare가 기록한 설정 변경을 포함한다. 그래서 `--allow-dirty-build`를 명시한다. 원본 프로젝트의 검토하지 않은 수정에 이 옵션을 붙여 무조건 실행하지 않는다.

```bash
env -u UNITY_RUN_TIMEOUT -u UNITY_TEST_TIMEOUT -u UNITY_BUILD_TIMEOUT \
  C6_P4_OUTPUT_ROOT="$C6_CHECK_ROOT/build-mac" \
  "$C6_CLI" build "$C6_BUILD_PROJECT" \
  --editor-path "$C6_EDITOR_APP" --format json \
  --target StandaloneOSX \
  --execute-method C6.Editor.ContinuousTransferBuild.BuildMac \
  --allow-dirty-build --no-tail --log-file "$C6_CHECK_ROOT/mac-build.log"
```

결과 앱은 `$C6_CHECK_ROOT/build-mac/macOS/C6ContinuousTransfer.app`이다. 이 커스텀 빌더의 출력 선택은 `C6_P4_OUTPUT_ROOT`가 담당하므로 CLI의 `--output-path`를 별도로 넣지 않는다.

### iOS용 Xcode 프로젝트 출력

```bash
env -u UNITY_RUN_TIMEOUT -u UNITY_TEST_TIMEOUT -u UNITY_BUILD_TIMEOUT \
  C6_P4_OUTPUT_ROOT="$C6_CHECK_ROOT/build-ios" \
  "$C6_CLI" build "$C6_BUILD_PROJECT" \
  --editor-path "$C6_EDITOR_APP" --format json \
  --target iOS \
  --execute-method C6.Editor.ContinuousTransferBuild.ExportIOS \
  --allow-dirty-build --no-tail --log-file "$C6_CHECK_ROOT/ios-export.log"
```

`$C6_CHECK_ROOT/build-ios/iOS/Unity-iPhone.xcodeproj`를 Xcode로 열어 앞의 본인 Apple Team 서명·실기기 Run 순서를 진행한다. Xcode를 터미널에서 사용할 때는 `DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer`를 지정한다. 이 Mac의 기본 Command Line Tools 선택과 실제 Xcode 위치를 혼동하지 않는다.

## 7. 이번 실행에서 확인할 것

짧은 실제 조작으로 다음 흐름을 확인한다. 같은 동작을 기계적으로100번 반복할 필요는 없다.

- 한 번 밀어 놓은 Raw가 손을 다시 대지 않아도 이웃 화면에 들어오고, 이동 방향과 남은 속도를 이어가는지 본다. 반대 방향도 확인한다.
- 세 명일 때 좌우 이웃이 서로 다른지, 충분한 속도의 구슬이 여러 화면을 이어서 지나고 결국 마찰로 멈추는지 본다. 받는 기기의 화면 비율이 달라도 상대 높이와 원형 크기가 정상인지 확인한다.
- 구슬끼리 자연스럽게 닿아도 자동 조합되지 않고, 직접 Yin+Yang 드래그 조합은 되는지 본다.
- 받은 Combined의 손떼기 투척과 실제 명중, 모든 화면의 공통 HP를 확인한다. 회복 확인 시 공격자 스태미나가100이면 실제 증가량은 상한 때문에0일 수 있다.
- 결과 화면·Retry·이탈 처리는 실제 확인한 범위만 별도로 기록한다.

빌드 번호·Config·기기·실행 날짜·사용한 입력을 결과에 남긴다. Mac의 자동 포인터나 시험용 구슬·속도 주입은 일반 Touch와 구분하며, 직접 IP 연결은 Bonjour 탐색을 확인한 것으로 기록하지 않는다. [P4 검증 보고서](P4_VALIDATION.md)에서 현재 완료 범위와 다음 확인을 이어간다.
