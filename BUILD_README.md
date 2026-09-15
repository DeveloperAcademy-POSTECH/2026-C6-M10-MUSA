> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](docs/VALIDATION_SUMMARY.md)·[이관 범위](docs/MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

> **코드를 직접 이해하고 수정하려면:** [보고서 3 — 상세 코드 유지보수 안내서](docs/UNITY_BEGINNER_CODE_MAINTENANCE_GUIDE.md)에서 실제 호출 흐름, 코드 예시, Console·중단점·테스트 사용법을 먼저 확인할 수 있습니다.

> **초보자 개발 보고서 후속편:** [빌드21~24의 2D 물리·3D 투척·최대 5인·연속 이동 구조와 수정 방법](docs/UNITY_BEGINNER_PHYSICS_MULTIPLAYER_REPORT.md)을 정리했습니다. 최신 씬의 빌드는 [P4 실행 안내](docs/P4_RUNBOOK.md)를 따르고, 아래 상세 안내는 Unity·Xcode 조작과 서명 절차를 참고하세요.

# Unity에서 iPhone·iPad로 직접 빌드하기

**네트워크 수정 앱26을 재현할 때:** [L2 빌드·실행 절차](docs/L2_CONNECTION_INTEGRATION.md#mac에서-동일-검사를-다시-실행하기)의 `C6.Editor.L2ConnectionBuild.ExportIOS`를 사용한다. 같은 `ContinuousTransferBattle` 씬을 출력하고 Development 빌드에만 `C6_L2_CHECKS` 관찰 도구를 추가한다. 아래 P4 메뉴는 기존 빌드24 출력 절차이므로 앱26 검증본과 구별하며, Xcode 서명·설치 과정은 아래 안내를 그대로 참고한다.

[프로젝트 README](README.md) · [Unity 개발 보고서](docs/UNITY_BEGINNER_DEVELOPMENT_REPORT.md)

**조직 저장소의 Unity 프로젝트를 Mac에서 열어 자신의 iPhone·iPad에 설치하고, 수정한 게임을 다시 반영하는 상세 안내입니다.** Unity를 처음 사용하는 사람도 순서대로 따라갈 수 있도록 클릭 위치, 입력값, 단계별 완료 기준을 함께 적었습니다.

문서 갱신: **2026-09-15 공개 이관본**. 아래 순서는 최신 **빌드24 `ContinuousTransferBattle`**을 기준으로 합니다. 이전 상세 안내의 Unity·Xcode 조작 절차를 유지하고 씬·메뉴·출력 경로와 조작 설명을 현재 구현에 맞췄습니다. 이관 중 게임 코드 변경이나 새 빌드·설치·실기기 시험을 실행한 것은 아닙니다. 과거 검증 범위는 [공개 검증 요약](docs/VALIDATION_SUMMARY.md)에서 확인합니다.

## 먼저 알아둘 전체 순서

**Unity 프로젝트 열기 → iOS 활성화 → C6 메뉴로 Xcode 프로젝트 생성 → Xcode에서 개인 Team과 기기 선택 → Run → 휴대기기 화면 확인**

| 어디에서 하는가 | 하는 일 | 완료 후 생기는 것 |
|---|---|---|
| Unity Hub | 정해진 Editor와 iOS 모듈 준비, 기존 프로젝트 열기 | 편집할 수 있는 Unity 프로젝트 |
| Unity Editor | 게임 소스·씬을 iOS용으로 출력 | `Unity-iPhone.xcodeproj`가 들어 있는 폴더 |
| Xcode | 앱 컴파일·개발용 서명·설치·실행 | iPhone·iPad에서 열리는 C6 앱 |
| 실제 기기 | 화면과 터치, 필요하면 협동 플레이 확인 | 이번 빌드의 실제 동작 확인 |

Unity의 **Play**는 Mac의 Editor 안에서 게임을 실행하는 버튼입니다. 휴대기기에 설치하려면 위의 출력·서명·Run 과정이 필요합니다. Unity의 iOS 빌드는 Unity가 Xcode 프로젝트를 생성하고 Xcode가 앱을 만드는 두 단계입니다. [Unity 6.5 공식 설명](https://docs.unity3d.com/6000.5/Documentation/Manual/iphone-BuildProcess.html)

기기 한 대만 있어도 **앱 설치와 로비 실행까지** 확인할 수 있습니다. 현재 게임은 **2~5명**이 참여하며, **전투 시작은 최소 두 참가자와 전원 Ready**가 필요합니다.

### 필요한 단계로 이동

1. [프로그램과 프로젝트 준비](#prepare)
2. [Unity에서 현재 게임 열기](#open-unity)
3. [iPhone·iPad 연결과 개발자 모드](#device)
4. [Unity에서 iOS 활성화와 설정 확인](#ios-profile)
5. [Xcode 프로젝트 출력하기](#export)
6. [Xcode에서 프로젝트·서명 설정](#signing)
7. [실제 기기 선택과 Run](#run)
8. [두 번째 기기 설치와 협동 시작](#two-devices)
9. [수정한 게임을 다시 설치하기](#rebuild)
10. [오류가 날 때 확인할 순서](#troubleshooting)
11. [선택: Mac 앱 빌드](#mac)
12. [이번 결과 기록과 참고 자료](#records)

<a id="prepare"></a>
## 1. 프로그램과 프로젝트 준비

### 필요한 환경

| 항목 | 이번 프로젝트 기준 |
|---|---|
| 컴퓨터 | Mac. 기존 검증 환경은 Apple Silicon, macOS 26.6.2 |
| Unity Editor | **6000.5.7f1** |
| 추가 Unity 모듈 | 해당 Editor의 **iOS Build Support** |
| Xcode | 기존 검증은 **26.6 (17F113), iOS SDK 26.5**. 연결할 기기의 OS를 지원하는 플랫폼 자료 필요 |
| Apple 계정·Team | 본인이 로그인하고 사용할 수 있는 **Apple 개발 Team**. 개인 개발이라면 Personal Team |
| 실제 기기 | 기존 사용 기기는 iPhone 17·iPad mini 6. 처음 연결할 때는 데이터 통신이 가능한 케이블 준비 |
| 프로젝트 폴더 | 내려받은 저장소 루트. 예: `/path/to/2026-C6-M10-MUSA`를 자신의 실제 경로로 교체 |

엔진 버전은 [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt), 패키지는 [manifest.json](Packages/manifest.json)과 [packages-lock.json](Packages/packages-lock.json)에 기록되어 있습니다. URP 17.5.0, Input System 1.20.0, NGO 2.13.1, Transport 6.5.0을 사용합니다. 처음 빌드하려고 이 버전을 먼저 변경할 필요는 없습니다.

### Unity와 iOS 모듈 확인

1. **Unity Hub**를 실행합니다.
2. 왼쪽 **Installs**를 선택합니다.
3. **6000.5.7f1** 항목을 찾습니다. 설치되지 않았다면 이 정확한 버전의 Editor를 설치합니다.
4. 해당 버전의 **Manage** 또는 관리 메뉴를 열고 **Add modules**를 선택합니다.
5. **iOS Build Support**를 선택해 설치합니다. 다운로드와 설치가 모두 끝날 때까지 기다립니다.

Add modules가 없으면 Hub를 통해 설치한 Editor인지 확인합니다. 일반적인 모듈 추가 위치는 [Unity Hub 공식 안내](https://docs.unity.com/en-us/hub/add-modules)에 있습니다.

**완료 기준:** 6000.5.7f1 Editor에서 iOS 플랫폼을 사용할 수 있어야 합니다. 다른 버전에 iOS 모듈을 설치한 것은 이 Editor의 지원을 추가한 것이 아닙니다.

현재 iOS 출력에는 상태바 동작을 위한 네이티브 소스 처리가 포함됩니다. [처리 코드](Assets/_Project/HapioMVP/Editor/T12ForegroundLifecyclePostprocess.cs)는 Unity 버전과 생성 소스가 예상과 같은지 검사합니다. 6000.5.7f1을 임의로 바꾸면 출력이 중단될 수 있습니다.

### Xcode 준비

1. Mac에 설치된 **Xcode**를 먼저 한 번 실행합니다.
2. 첫 실행에서 필수 구성요소 설치나 초기 설정이 나오면 완료합니다.
3. iOS 플랫폼 자료가 필요하다는 안내가 나오면 Xcode의 다운로드·구성요소 화면에서 설치합니다. 버전에 따라 Settings의 **Components** 또는 **Platforms**에 표시될 수 있습니다.
4. **Xcode → Settings → Apple Accounts**를 열어 기존 개발용 Apple 계정으로 로그인합니다. 버전에 따라 **Accounts**로 표시될 수 있습니다.

계정 로그인 후 실제 프로젝트의 Team을 선택하는 과정은 [6단계](#signing)에서 합니다. 계정 비밀번호나 인증서 파일을 Unity 폴더에 저장할 필요는 없습니다.

<a id="open-unity"></a>
## 2. Unity에서 현재 게임 열기

### 이미 저장소를 내려받은 경우

1. Unity Hub에서 **Projects**를 선택합니다.
2. 목록에서 내려받은 `2026-C6-M10-MUSA` 저장소의 실제 위치를 확인해 엽니다. Hub에 표시되는 이름보다 폴더 위치를 기준으로 확인합니다.
3. 목록에 없으면 **Add / Add project from disk**에서 해당 저장소 루트 폴더를 선택합니다.
4. Editor 버전은 **6000.5.7f1**을 선택합니다.

폴더 안에는 `Assets`, `Packages`, `ProjectSettings`가 나란히 있어야 합니다. `Assets` 폴더만 선택하거나 New Project로 새 프로젝트를 만들지 않습니다. 같은 프로젝트가 이미 Editor에 열려 있다면 그 창을 사용합니다.

### GitHub에서 새로 내려받는 경우

[조직 저장소](https://github.com/DeveloperAcademy-POSTECH/2026-C6-M10-MUSA)에서 Unity 이관 내용이 있는 브랜치를 내려받습니다. PR 병합 전에는 `feature/unity-prototype`을 선택하고, 병합 후에는 `main`에서 받을 수 있습니다. 선택한 브랜치에 `Assets`, `Packages`, `ProjectSettings`가 있는지 확인합니다. 새 위치에 둘 때도 `Assets`·`Packages`·`ProjectSettings`가 함께 있는 폴더를 Hub에 추가합니다. 기존 프로젝트 폴더 위에 다운로드한 파일을 통째로 덮어쓰지 않습니다.

GitHub에는 생성된 `.app`이나 Xcode 출력 폴더가 포함되어 있지 않습니다. 내려받은 소스에서 [5단계](#export)의 Unity 출력을 수행해 만들어야 합니다.

### 현재 씬을 열고 저장하기

1. Editor의 패키지 처리·Import·컴파일이 끝날 때까지 기다립니다.
2. **Project** 창에서 다음 위치를 차례로 엽니다.

   `Assets → _Project → HapioMVP → Scenes → ContinuousTransferBattle.unity`

3. `ContinuousTransferBattle.unity`를 더블 클릭합니다. 이것이 현재 빌드24 씬입니다. `InterruptionBattle` 등 이전 단계 씬도 학습·비교용으로 보존되어 있으므로 이름을 확인합니다.
4. **Window → General → Console**을 열어 빨간 컴파일 오류가 없는지 확인합니다. 오류가 있으면 첫 오류의 메시지와 파일 위치를 먼저 확인합니다.
5. Play 중이면 상단 Play 버튼을 눌러 종료합니다. 수정한 씬은 **File → Save**로 저장합니다.

**완료 기준:** 현재 씬을 열 수 있고, 컴파일이 끝났으며, Play가 꺼져 있어야 합니다. 전용 빌더는 저장된 씬을 사용하므로 수정한 내용을 먼저 저장합니다.

이 씬의 UI와 구슬 일부는 실행 중 코드로 생성됩니다. Play 전에 Hierarchy에 모든 버튼과 구슬이 보이지 않아도 정상입니다. Inspector에서 수정할 값과 코드 위치는 [개발 보고서](docs/UNITY_BEGINNER_DEVELOPMENT_REPORT.md)에 정리했습니다.

<a id="device"></a>
## 3. iPhone·iPad 연결과 개발자 모드

### Mac과 기기 연결

1. 기기를 케이블로 Mac에 연결합니다.
2. 기기의 잠금을 해제하고 홈 화면을 켜 둡니다.
3. 기기에 **이 컴퓨터를 신뢰하겠습니까?**가 나오면 **신뢰**를 선택하고 기기 암호로 확인합니다.
4. Xcode의 실행 대상 목록에서 연결한 기기를 찾습니다. 보이지 않으면 실행 대상 목록의 **Manage Devices** 또는 기기 관리 화면을 엽니다.
5. Xcode 버전에 따라 **Device Hub** 또는 **Window → Devices and Simulators**에서 기기 연결 상태를 확인할 수 있습니다. 준비 중 표시가 있으면 준비가 끝날 때까지 기다립니다.

충전만 되고 기기가 표시되지 않으면 데이터 통신 가능한 케이블인지, 잠금과 신뢰 승인이 완료됐는지 확인합니다.

### 개발자 모드 켜기

1. iPhone·iPad에서 **설정 → 개인정보 보호 및 보안 → 개발자 모드**를 엽니다.
2. 개발자 모드를 켜고 **재시동**을 선택합니다.
3. 재시동 후 잠금을 해제합니다.
4. 개발자 모드를 켤지 묻는 화면에서 **켜기 / Enable**를 승인하고 암호를 입력합니다.
5. Mac 연결을 유지하고 Xcode의 기기 상태를 다시 확인합니다.

개발자 모드 항목이 없으면 먼저 Xcode에서 해당 기기의 연결·페어링을 시작합니다. 스위치를 켠 뒤 재시동 후의 확인까지 끝나야 합니다. 이미 활성화된 기기는 매번 이 과정을 반복하지 않아도 됩니다. [Apple 개발자 모드 안내](https://developer.apple.com/documentation/xcode/enabling-developer-mode-on-a-device/)

**완료 기준:** Xcode가 기기를 인식하고, 기기의 잠금이 해제되어 있으며, 개발자 모드가 켜져 있어야 합니다.

<a id="ios-profile"></a>
## 4. Unity에서 iOS 활성화와 설정 확인

### 빌드 프로필 활성화

1. Unity로 돌아와 **File → Build Profiles**를 엽니다.
2. iOS 프로필이 있으면 선택합니다.
3. 없으면 **Add Build Profile → iOS → Add Build Profile**을 선택합니다.
4. **Switch Profile**을 눌러 실제 활성 프로필을 iOS로 바꿉니다. 이미 활성 상태이면 다시 전환할 필요는 없습니다.
5. 플랫폼 전환과 관련 Import가 끝날 때까지 기다립니다.

목록에서 iOS를 클릭한 것과 활성화한 것은 다릅니다. C6의 빌더는 실제 활성 플랫폼이 iOS가 아니면 중단합니다. iOS 항목을 사용할 수 없으면 [1단계](#prepare)의 모듈 설치를 확인합니다. [Unity Build Profiles 안내](https://docs.unity3d.com/6000.5/Documentation/Manual/iphone-BuildProcess.html)

### 이 프로젝트가 자동으로 설정하는 값

**Edit → Project Settings → Player**의 iOS 설정에서 값을 살펴볼 수 있습니다. 이 안내의 C6 출력 메뉴는 아래의 주요 값을 준비 코드로 설정합니다.

| 항목 | 기준 값 | 의미 |
|---|---|---|
| Product Name | `C6 Prototype` | 앱 이름 |
| Bundle Identifier | 기본값은 `com.wolfuraark.c6prototype`. 자신의 Team에서 사용할 수 없으면 고유 앱 ID 지정 | 앱을 식별하는 ID |
| Version / Build | `0.1.0` / `24` | P4 전용 빌더가 설정하는 버전 표기 |
| Target Device | iPhone and iPad | 두 종류의 기기에 설치 |
| Orientation | Portrait | 세로 화면 |
| SDK | Device SDK | 실제 기기용 출력 |
| Scripting Backend | IL2CPP | C# 게임 코드를 iOS 빌드로 연결 |
| Development Build | 사용 | C6 전용 빌더에서 지정하는 개발용 출력 |
| 최소 iOS 버전 | 저장된 설정 `15.0` | 현재 Player Settings에 기록된 최소 버전 |

최소 OS 15.0이라는 값이 모든 iOS 15 기기를 검증했다는 뜻은 아닙니다. 기존 실기기 검증에 사용한 기기는 iPhone 17과 iPad mini 6입니다. 자신의 다른 기기에서 새로 실행한 결과는 별도로 확인합니다.

**완료 기준:** iOS가 활성화되어 있어야 합니다. 아래 단계의 `Export iOS`가 씬과 주요 설정을 준비하므로 별도로 `Prepare Continuous Transfer Battle`을 먼저 누를 필요는 없습니다.

<a id="export"></a>
## 5. Xcode 프로젝트 출력하기

### 기본 경로에 처음 출력하는 경우

1. Unity 메뉴 **C6 → Next Phase → P4 → Export iOS**를 누릅니다.
2. 출력이 진행되는 동안 기다립니다. 첫 출력은 임포트·변환할 자료가 있어 시간이 걸릴 수 있습니다.
3. Console에서 다음 완료 메시지를 찾습니다.

   `C6_P4_BUILD_COMPLETE target=iOS`

4. Finder에서 프로젝트 안의 다음 폴더를 엽니다.

   `Builds/NextPhase/P4/iOS/` — 내려받은 프로젝트 루트 아래

5. 그 안에 **`Unity-iPhone.xcodeproj`**가 있는지 확인합니다.

프로젝트 위치를 실제 경로로 바꾼 전체 경로 예시는 다음과 같습니다.

```text
/path/to/2026-C6-M10-MUSA/Builds/NextPhase/P4/iOS/Unity-iPhone.xcodeproj
```

이 메뉴는 폴더 선택 창 없이 정해진 경로로 출력합니다. 프로젝트의 일반적인 첫 iOS 출력은 이 **C6 메뉴**로 진행합니다. Unity의 일반 Build / Build and Run 절차와 출력 경로·준비 동작을 섞지 않도록 합니다.

**완료 기준:** 성공 메시지와 Xcode 프로젝트 파일을 모두 확인합니다. 폴더가 생겼더라도 Console에 빌드 오류가 있으면 완료로 판단하지 않습니다. 이 단계만 끝났을 때는 아직 휴대기기에 설치되지 않았습니다.

### 이미 출력한 적이 있거나 `Output exists`가 나오는 경우

현재 빌더는 이전 출력에 내용이 있으면 보존하고 중단합니다. 오류는 다음과 같습니다.

```text
Output exists; use a new C6_P4_OUTPUT_ROOT to preserve previous builds.
```

새 출력 경로는 환경 변수 `C6_P4_OUTPUT_ROOT`로 지정합니다. 이 변수는 **Unity를 시작할 때** 전달해야 합니다. 실행 중인 Unity 옆에서 터미널에 변수를 설정하는 것만으로는 바뀌지 않습니다.

1. 현재 작업을 저장합니다.
2. **Unity → Quit Unity**로 이 프로젝트의 Editor를 정상 종료합니다. 기존 Xcode 출력은 그대로 보관합니다.
3. Mac에서 **터미널**을 엽니다.
4. 아래 예시의 프로젝트 위치·Editor 위치를 확인합니다. `manual-ios-20260915-01`은 이번 출력 이름이므로 기존에 쓰지 않은 이름을 사용합니다.
5. 아래 내용을 터미널에 붙여 넣고 실행합니다. **이 명령은 출력 위치를 지정해 Unity를 다시 여는 명령**이며, 아직 빌드를 자동 실행하지 않습니다.

```sh
C6_PROJECT='/path/to/2026-C6-M10-MUSA'
C6_UNITY='/Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app/Contents/MacOS/Unity'
C6_NEW_OUTPUT="$C6_PROJECT/Builds/NextPhase/P4/manual-ios-20260915-01"

env C6_P4_OUTPUT_ROOT="$C6_NEW_OUTPUT" \
  "$C6_UNITY" -projectPath "$C6_PROJECT" -buildTarget iOS
```

6. 열린 Unity의 처리가 끝나면 **C6 → Next Phase → P4 → Export iOS**를 누릅니다.
7. 이 예시에서는 아래 파일을 사용합니다.

```text
/path/to/2026-C6-M10-MUSA/Builds/NextPhase/P4/manual-ios-20260915-01/iOS/Unity-iPhone.xcodeproj
```

다음 출력은 `manual-ios-20260915-02`처럼 다른 이름으로 반복합니다. 환경 변수에는 **`iOS`의 상위 폴더**를 넣습니다. 같은 프로젝트를 두 Editor에서 동시에 열지 않습니다. 과거 검증 앱의 보관 위치와 이번 출력 경로를 구분합니다. 공개 저장소에는 과거 앱이나 Xcode 출력물이 포함되지 않습니다.

빌더의 메뉴·경로·설정 근거는 [ContinuousTransferBuild.cs](Assets/_Project/HapioMVP/Editor/ContinuousTransferBuild.cs)입니다. 출력 과정에서 로컬 네트워크 설명, Bonjour 서비스 `_c6hapio._udp`, 상태바 대응도 자동 적용됩니다. 상태바 처리 기록은 출력 안의 `C6T12LifecyclePatch/patch-receipt.json`에 남습니다.

<a id="signing"></a>
## 6. Xcode에서 프로젝트·서명 설정

### Xcode의 TARGETS > Unity-iPhone > Signing & Capabilities에서 Automatically manage signing을 켜고, Team에 본인의 Apple ID/Personal Team을 선택해야 빌드 가능.

### 이번에 생성한 프로젝트 열기

1. Finder에서 **이번 출력 폴더의 `Unity-iPhone.xcodeproj`**를 더블 클릭합니다.
2. Xcode가 열리면 프로젝트 분석이 끝날 때까지 기다립니다.
3. 이전 출력도 Xcode에 열려 있다면 창과 경로를 확인해 이번 프로젝트에서 작업합니다.

현재 프로젝트는 `.xcodeproj`로 여는 구성입니다. 별도 `.xcworkspace`나 Pods 설치를 요구하지 않습니다.

### 앱 대상에 개인 Team 지정

1. Xcode 왼쪽 Project navigator에서 맨 위의 **Unity-iPhone 프로젝트 아이콘**을 선택합니다. 왼쪽 목록이 안 보이면 **View → Navigators → Project**를 엽니다.
2. 가운데 편집 화면의 **TARGETS** 목록에서 **Unity-iPhone**을 선택합니다. PROJECT 설정과 앱 TARGET 설정을 구분합니다.
3. 위쪽 탭에서 **Signing & Capabilities**를 선택합니다.
4. **Automatically manage signing**을 켭니다.
5. **Team**에서 **본인의 Apple 개발 Team**을 선택합니다. Team이 없으면 **Xcode → Settings → Apple Accounts / Accounts**의 로그인을 확인합니다.
6. 기본 **Bundle Identifier**는 **`com.wolfuraark.c6prototype`**입니다. 자신의 Team에서 등록할 수 없다는 오류가 나면 이 앱 Target에 본인이 사용할 수 있는 고유 ID를 입력합니다. `com.example...` 같은 설명용 값도 그대로 쓰지 말고 자신의 실제 값을 정합니다.
7. 서명 준비가 완료될 때까지 기다립니다. 등록이 필요해 **Register**가 표시되면 연결한 본인 기기를 등록합니다.

Xcode 버전에 따라 먼저 **Set Up Signing**이 보일 수 있습니다. 이 경우 그 버튼을 눌러 같은 Team과 Bundle Identifier를 지정한 뒤 서명을 설정합니다. [Apple의 서명·기기 실행 안내](https://developer.apple.com/documentation/xcode/running-your-app-on-simulated-or-physical-devices)

| 화면의 항목 | 선택할 내용 |
|---|---|
| TARGETS | `Unity-iPhone` |
| Automatically manage signing | 켬 |
| Team | 본인의 Apple 개발 Team |
| Bundle Identifier | 기본값은 `com.wolfuraark.c6prototype`. 자신의 Team에서 사용할 수 없으면 고유 앱 ID 지정 |

**완료 기준:** 앱 대상에 개인 Team이 선택되어 있고, 해결되지 않은 서명 오류가 없어야 합니다. 현재 Unity 출력은 Team을 미리 채우지 않으므로 새 출력마다 확인합니다. `UnityFramework` 등 다른 대상까지 같은 Bundle Identifier로 바꾸지 않습니다.

Xcode의 Team·앱 ID 변경은 이번 출력에 적용됩니다. 다음 Unity Export에서는 [ContinuousTransferBuild.Prepare()](Assets/_Project/HapioMVP/Editor/ContinuousTransferBuild.cs)가 앱 ID를 기본값으로 다시 설정하므로 확인을 반복합니다. 팀에서 영구적으로 사용할 앱 ID를 정했다면 이 준비 코드의 iOS 식별자와 프로젝트 설정을 함께 관리합니다. 저장소 이름을 바꿨다고 앱 ID가 자동으로 바뀌지는 않습니다.

<a id="run"></a>
## 7. 실제 기기 선택과 Run

### Scheme과 실행 기기 선택

Xcode 상단에는 **무엇을 실행할지 고르는 Scheme**과 **어디에서 실행할지 고르는 기기 목록**이 있습니다.

1. Scheme을 **Unity-iPhone**으로 선택합니다.
2. 옆 실행 대상에서 실제 연결한 **iPhone 17** 또는 자신의 실제 기기 이름을 선택합니다.
3. `Any iOS Device` 같은 일반 대상은 실제 설치할 휴대폰 선택을 완료한 상태가 아닙니다. 이름이 있는 기기를 고릅니다.
4. 현재 출력은 Device SDK용이므로 iPhone Simulator를 실행 대상으로 사용하지 않습니다.

### 개발용 실행 구성 확인

이 안내에서는 오류를 살펴볼 개발용 실행 구성으로 **Debug**를 선택합니다.

1. **Product → Scheme → Edit Scheme**을 엽니다.
2. 왼쪽 **Run**, 오른쪽 **Info**를 선택합니다.
3. **Build Configuration**을 **Debug**로 선택하고 닫습니다.

기존 보관 Xcode 프로젝트의 기본 Run 구성은 `ReleaseForRunning`입니다. 기본값이 이미 Debug라고 가정하지 않습니다. Unity의 Development Build와 Xcode의 Build Configuration은 서로 다른 설정입니다.

### 앱 설치·실행

1. 기기 잠금을 해제하고 화면을 켜 둡니다.
2. Xcode의 **Product → Run** 또는 상단 **▶**를 누릅니다. 단축키는 **⌘R**입니다.
3. Xcode가 컴파일, 서명, 설치, 실행을 진행하는 동안 기다립니다.
4. 기기 접근이나 개발 실행에 대한 승인 요청이 나오면 내용을 확인하고 승인합니다.
5. 기기에서 **C6 로비 화면**이 실제로 열리는지 확인합니다.

오류가 나오면 Xcode의 왼쪽 **Issue navigator**에서 첫 오류를 열어 확인합니다. `Build Succeeded`가 보여도 그 뒤 설치·실행이 실패할 수 있으므로 휴대기기 화면까지 확인합니다. 실행과 오류 확인의 일반 절차는 [Apple 문서](https://developer.apple.com/documentation/xcode/running-your-app-on-simulated-or-physical-devices)를 참고합니다.

| 현재 보이는 결과 | 완료한 단계 |
|---|---|
| Unity의 출력 성공 메시지와 `.xcodeproj` | Unity iOS 출력 |
| Xcode 컴파일 성공 | 앱 빌드 |
| 기기에 앱이 설치됨 | 서명·설치 |
| 기기에서 C6 로비가 실제로 보임 | 이번 앱의 기기 실행 |
| 두 기기의 입장·Ready·전투가 동작함 | 별도의 협동 기능 확인 |

**완료 기준:** 휴대기기에 C6 앱이 열려 로비가 보여야 합니다. 휴대기기 한 대만 설치한 상태에서 HOST START가 비활성인 것은 두 사람을 기다리는 동작일 수 있습니다.

<a id="two-devices"></a>
## 8. 두 번째 기기 설치와 협동 시작

### 같은 앱을 iPad에도 설치

1. iPad도 [3단계](#device)의 신뢰·개발자 모드·잠금 해제를 완료합니다.
2. Xcode에서 **같은 출력 프로젝트**를 사용하고 실행 대상만 iPad로 바꿉니다.
3. 개인 Team을 확인한 뒤 **Run**을 눌러 iPad에 설치·실행합니다.
4. 두 앱의 설치가 끝나면 양쪽 기기에서 C6 앱을 직접 열어 전경에 둡니다. Xcode가 실행 대상을 바꾸며 첫 앱을 중단했더라도 기기에서 다시 열 수 있습니다.

같은 코드로 두 기기에 설치할 때 Unity Export를 기기마다 반복할 필요는 없습니다. 실제 설치와 실행 결과는 기기별로 확인합니다.

### 방 생성부터 첫 공격까지

1. iPhone과 iPad를 **같은 Wi-Fi**에 연결합니다.
2. iPhone에서 **CREATE ROOM**을 누릅니다.
3. iPad에서 **FIND ROOMS**를 누르고 발견한 방의 **JOIN**을 선택합니다.
4. 로컬 네트워크 알림이 나오면 허용합니다. 허용 후 필요하면 탐색·참가를 다시 누릅니다.
5. 양쪽에 같은 방과 **P1·P2**가 보이는지 확인합니다.
6. 양쪽에서 **I'M READY**를 누릅니다.
7. iPhone Host에서 **HOST START**를 누릅니다.
8. PLAYING·시작 구슬 **0개**·스태미나 **100**·몬스터 HP **100**·**180초**에서 진행하는지 확인합니다.
9. **GENERATE / 20**으로 Raw를 만듭니다. Yin과 Yang을 겹쳐 손을 놓으면 Combined가 됩니다. 음양은 무작위이므로 두 번 생성했다고 반드시 반대 음양이 나오지는 않습니다.
10. Combined를 상단 전투 영역으로 올린 뒤 **위로 움직이면서 손을 놓으면** 방향·세기에 따라 투척됩니다. 상단으로 들어갔다는 이유만으로 자동 발사되지 않습니다.
11. 하단 구슬을 좌우로 밀면서 놓으면 관성으로 움직입니다. 놓은 구슬이 좌우 경계에 닿으면 이웃 화면의 반대편으로 자동 전달되고, 남은 속도가 마찰로 줄어 멈출 때까지 이동합니다. 매 경계에서 다시 잡을 필요는 없습니다.

세 명 이상은 최대 다섯 명까지 같은 절차로 참가합니다. 모두 같은 빌드·Config를 사용하고, 각자의 LEFT·RIGHT 이웃을 확인한 뒤 전원이 Ready합니다. 자세한 조작은 [P4 실행 안내](docs/P4_RUNBOOK.md)를 참고합니다.

방이 검색되지 않으면 **같은 Wi-Fi, Host가 방을 유지하는지, 기기의 설정 → 개인정보 보호 및 보안 → 로컬 네트워크에서 C6 허용 여부**를 확인합니다. 직접 IP 접속을 사용한다면 과거 시험 주소가 아니라 현재 Host 주소를 사용합니다.

일반 플레이에는 개발 진단용 실행 인자나 응답 보류 옵션이 필요하지 않습니다. 현재 앱은 자동 재접속하지 않으며 세션이 끝나면 로비에서 새 방을 만들고 다시 참가합니다.

<a id="rebuild"></a>
## 9. 수정한 게임을 다시 설치하기

**Unity에서 수정 → 저장 → 컴파일 → 새 경로로 Export → 새 Xcode 프로젝트 열기 → 서명 확인 → Run**을 반복합니다.

| 바꾼 내용 | 다음에 할 일 |
|---|---|
| Unity C# 코드, 씬, Config, 재질 | Unity에서 다시 Export하고 새 Xcode 프로젝트로 Run |
| Xcode Team·서명 설정만 수정 | 해당 Xcode 프로젝트에서 다시 Run |
| 같은 출력 앱을 두 번째 기기에 설치 | 같은 Xcode 프로젝트에서 실제 실행 대상만 변경 후 Run |
| 이전 앱을 다시 열어 조작 | 기기에서 앱 실행. 새 코드 반영과는 별개 |

### 예: 생성 비용을 조정한 후 반영하기

1. Unity에서 Play를 종료합니다.
2. `Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset`을 선택합니다.
3. 의도한 값을 하나 수정하고 변경 내용을 기록합니다. 이 예시는 조정 절차 설명이며 현재 비용 20을 변경한 작업 기록은 아닙니다.
4. 파일과 씬을 저장하고 Console 컴파일 오류를 확인합니다.
5. [5단계의 새 출력 경로 절차](#export)에 따라 `manual-ios-20260915-02`처럼 새로운 폴더를 사용합니다.
6. 새 `.xcodeproj`를 열고 개인 Team과 실제 기기를 선택한 뒤 Run합니다.
7. 이번에 바꾼 값이 기기 화면과 실제 생성 비용에 반영됐는지 확인합니다. 두 기기로 플레이할 때는 양쪽 앱을 같은 버전으로 맞춥니다.

**옛 Xcode 프로젝트에서 Run만 누르면 새 Unity 변경이 들어가지 않습니다.** 수정 후에도 예전 화면이 보이면 이번 Export 경로를 열었는지부터 확인합니다.

빌드 번호는 자동 증가하지 않습니다. 현재 전용 빌더의 Prepare가 버전 `0.1.0`·빌드 `24`와 앱 ID를 다시 설정합니다. Inspector의 빌드 번호만 바꿔도 다음 출력에서 돌아갈 수 있으므로, 지금은 **변경 커밋·출력 폴더·실행 날짜**도 함께 기록해 수정본을 구분합니다.

<a id="troubleshooting"></a>
## 10. 오류가 날 때 확인할 순서

| 증상·메시지 | 먼저 확인할 곳 | 다음 행동 |
|---|---|---|
| C6 메뉴가 없음 | Unity Console, 프로젝트 위치 | 컴파일 종료를 기다리고 첫 빨간 오류 확인. `Assets`·`Packages`·`ProjectSettings`가 있는 저장소 루트인지 확인 |
| iOS가 없거나 `Missing build support` | Hub → Installs → 6000.5.7f1 → Add modules | 해당 버전에 iOS Build Support 설치 |
| `Active build target ...` | Unity → File → Build Profiles | iOS 선택 후 Switch Profile, 전환 완료 확인 |
| `Stop Play mode first` | Unity 상단 Play | Play 종료·저장 후 Export |
| `Output exists ...` | 이번 출력 폴더 | 이전 출력 보존, 새 출력 경로를 지정해 Unity 재실행 |
| 터미널 명령의 `No such file or directory` | 예시의 프로젝트·Editor 경로 | 설치 위치를 확인해 `C6_PROJECT`, `C6_UNITY` 수정 |
| 새 경로를 넣었는데 계속 기본 위치 사용 | Unity를 연 방식 | 환경 변수를 지정하기 전에 Editor가 이미 실행 중이었는지 확인. 정상 종료 후 예시 명령으로 다시 열기 |
| 버전·native source 불일치 | Editor 버전, 상태바 처리 오류 | 6000.5.7f1과 원본 생성 소스 확인. 검사 코드를 임의로 건너뛰지 않음 |
| Xcode가 iOS 지원 다운로드를 요구 | Xcode Components / Platforms 또는 실행 대상의 Get | 사용하는 기기에 맞는 플랫폼 지원 설치 |
| 기기 이름이 Xcode에 없음 | 케이블·잠금·컴퓨터 신뢰·기기 관리 화면 | 데이터 케이블로 연결, 잠금 해제·신뢰 승인, 준비 완료 확인 |
| Developer Mode disabled | 기기 개인정보 보호 및 보안 | 개발자 모드 켜기, 재시동 후 최종 승인 |
| Team이 없거나 서명에 Team 필요 | Xcode Settings의 Apple 계정, 앱 Target의 Signing | 로그인 후 본인의 Apple 개발 Team 선택·자동 서명 켜기 |
| Bundle Identifier 등록 실패 | 앱 Target의 Team과 Bundle ID | 본인 Team과 사용 가능한 고유 앱 ID 확인. 기본 ID를 등록할 수 없으면 앱 Target에서 변경 |
| Provisioning profile에 기기 없음 | 실제 실행 대상과 Signing | 기기 선택·자동 서명 확인, Register가 표시되면 등록 |
| 빌드는 성공했지만 기기 실행 실패 | Xcode가 표시한 설치·실행 오류 | 잠금·개발자 모드·서명 등 표시된 원인을 해결하고 다시 Run |
| 실행 대상이 일반 iOS 기기·Simulator | Xcode 상단 실행 대상 | 실제 연결한 기기 이름 선택 |
| 기기에서 옛 화면이 보임 | 이번 Export 경로와 Xcode 창 | 새 출력 프로젝트를 열어 다시 설치 |
| 로비에서 HOST START가 비활성 | 참가자 수·양쪽 Ready | 두 참가자 입장·각자 I'M READY 후 Host에서 시작 |
| 방이 검색되지 않음 | 같은 Wi-Fi·로컬 네트워크 허용·Host 방 | 권한 확인 후 다시 탐색. 직접 IP는 현재 Host 주소 사용 |

Xcode 오류를 공유할 때는 첫 오류 메시지, 사용한 출력 폴더, Editor/Xcode 버전, 선택한 기기 종류를 함께 남기면 원인 범위를 줄일 수 있습니다. 계정 비밀번호·서명 개인 키는 포함하지 않습니다.

<a id="mac"></a>
## 11. 선택: Mac 앱 빌드

Mac에서 별도 앱으로 실행하고 싶을 때의 경로입니다.

1. Unity 작업을 저장하고 Play를 종료합니다.
2. **File → Build Profiles**에서 macOS 프로필을 선택하거나 추가하고 **Switch Profile**로 활성화합니다.
3. 전환이 끝나면 **C6 → Next Phase → P4 → Build macOS Continuous Transfer Battle**을 누릅니다.
4. 기본 결과는 `Builds/NextPhase/P4/macOS/C6ContinuousTransfer.app`입니다. `C6_P4_OUTPUT_ROOT`를 지정했다면 그 아래 `macOS/C6ContinuousTransfer.app`에 생성됩니다.
5. Finder에서 앱을 열어 로비를 확인합니다. iPhone·iPad와 함께 플레이할 때도 같은 Wi-Fi와 두 참가자 조건을 따릅니다.

Mac 출력도 기존 결과가 있으면 중단합니다. 새 출력 경로 절차를 사용할 때는 Unity 실행 명령에서 `-buildTarget iOS` 부분을 빼고 Editor를 연 뒤, 위 순서대로 Build Profiles에서 macOS를 활성화합니다. 이후 iOS를 출력할 때는 다시 iOS를 활성화합니다. 이 메뉴는 로컬 개발 앱을 만들며 공증·App Store 배포를 수행하지 않습니다.

혼자 기본 전투를 배우려면 Unity에서 이전 `BattleLoop.unity`를 열어 Play → DEV SOLO ON → HOST → HOST START를 사용할 수 있습니다. DEV SOLO는 Editor·개발 빌드용입니다. 이는 T09 학습 장면이므로 최신 2D 물리·다인 로비·연속 전달까지 포함한 P4 검증을 대신하지 않습니다.

<a id="records"></a>
## 12. 이번 결과 기록과 참고 자료

기기에서 로비까지 확인하면 이번 설치·실행 결과를 기록할 수 있습니다. 아래는 **직접 실행한 뒤 채우는 빈 양식**입니다.

| 항목 | 기록할 내용 |
|---|---|
| 실행 날짜 | 미입력 |
| 소스 커밋·이번 수정 내용 | 미입력 |
| Unity / Xcode 버전 | 미입력 |
| 실제 Export 폴더 | 미입력 |
| 기기 종류·OS | 미입력 |
| Unity 출력 | 미확인 |
| Xcode 빌드·서명·설치 | 미확인 |
| 기기에서 로비 실행 | 미확인 |
| 두 기기 플레이 | 미확인 또는 미실행 |
| 오류와 조치 | 미입력 |

이관된 것은 검토된 소스 snapshot이며 과거에 설치한 앱 자체가 아닙니다. 원본 작업 폴더의 미커밋 설정과 검증 중 자동 변경된 렌더러 설정은 이번 이관과 구분합니다. 새 환경에서 출력한 앱은 새 빌드로 기록하고 직접 확인합니다. 기존 빌드24의 실행·미실행 범위는 [P4 검증 기록](docs/P4_VALIDATION.md)과 [공개 검증 요약](docs/VALIDATION_SUMMARY.md)에 있습니다.

`Builds/`, `Library/`, 원시 `Logs/`, 서명 자료는 Git에 올리지 않습니다. 게임 소스·씬·`.meta`·설정·문서와 검토한 결과를 Git으로 관리합니다. 문서 작성이나 빌드 성공만으로 모든 실기기 기능을 PASS로 기록하지 않습니다.

프로젝트별 메뉴와 경로는 [현재 빌더](Assets/_Project/HapioMVP/Editor/ContinuousTransferBuild.cs), 기기·서명 실행 이력은 [IOS_RUNBOOK](docs/IOS_RUNBOOK.md), 구현을 이해하고 수정할 위치는 [Unity 개발 보고서](docs/UNITY_BEGINNER_DEVELOPMENT_REPORT.md)에서 확인할 수 있습니다.
