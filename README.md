# C6 Physics Lab

합이오의 **연결·구슬 물리·전달·투척**만 살펴보고 조정하기 위한 Unity 프로젝트입니다. 원래 프로젝트를 복제하거나 Task별 Scene을 누적하지 않았습니다. 필요한 동작을 역할별 새 코드와 한 개의 실험 Scene으로 구성합니다. 이전 MUSA 프로토타입은 `pre-physics-lab-rebuild-2026-10-10` 태그와 이전 Git 이력에 보존했습니다. [교체 이관 범위](docs/MIGRATION_PHYSICS_LAB.md)를 참조하세요.

## 개발자 설정 빠른 참고

Editor 또는 Development Build에서 **DEV**를 열면 요괴·개인 HP·시간·스태미나·생성 확률·아이템·구슬 물리·카메라·투척 관련 숫자 **52개**를 숫자 입력과 슬라이더로 조정할 수 있습니다. 이 값은 솔로 라운드 또는 Host가 만드는 공유 방에 적용합니다. [개발자 모드 조정값 상세 명세](docs/DEVELOPER_MODE_PARAMETER_SPEC.md)에 모든 값의 현재 기본값, 입력 범위, 단위, 실제 효과, 값 사이의 제약과 코드 연결을 정리했습니다. 투척값을 비교하는 순서는 [투척 물리 실험 가이드](docs/THROW_TUNING_GUIDE.md)를 보세요.

| 조정 영역 | 기본값 예시 | 무엇이 바뀌나 |
| --- | --- | --- |
| 전투 | 요괴 HP 1,000 · 개인 HP 300 · 피해 20 · 시간 180초 | 새 라운드의 요괴·플레이어별 체력, 제한 시간·요괴 명중 피해 |
| 스태미나·생성 | 시작 100 · 생성 비용 20 · 회복량 20/3초 | 생성 가능 횟수와 **연속 회복 속도** |
| 생성 확률·아이템 | 음 40 · 양 40 · 아이템 4종 각 5 (%) · 아이템 수명 15초 · 잠금 2~4회 | 생성되는 종류의 비율과 아이템 해금 난이도 |
| 3D 조합판 구슬 | 수명 8초 · 탄성 0.8 · 감속 2 | 보관 시간과 평면 위 3D 구체의 이동·반발 |
| 카메라·발사점 | 카메라 거리 8 · 발사 거리 4 | 보이는 시점과 3D 투척 시작 위치 |
| 투척 | 상승 배율 2.1 · 전진 배율 4.5 · 중력 9.81 | 손동작 판정과 공의 초기 속도·비행 궤적 |

DEV 값은 **START/RESTART SOLO**로 1인 라운드에, **CREATE ROOM**으로 새 공유 방에 적용됩니다. Host는 전투 시작 전 **APPLY TO ROOM**으로 공유 설정을 다시 보낼 수 있습니다. 저장된 설정 에셋은 바꾸지 않습니다.

## 열기와 실행

- Unity Hub에서 이 저장소 루트를 프로젝트로 추가하고 **Unity 6000.6.5f1**로 엽니다. 버전 기준은 `ProjectSettings/ProjectVersion.txt`입니다.
- 실험 Scene은 `Assets/Lab/Scenes/Lab.unity`입니다. Scene에 저장된 Canvas와 GameObject는 Hierarchy/Inspector에서 직접 조정합니다.
- 게임 수치는 `Assets/Lab/Resources/LabConfig.asset`의 **단일 설정 원본**에서 조정합니다. 속성을 바꿨다면 새 세션에서 확인합니다.
- 구슬의 **보이는 형태**는 `Assets/Lab/Art/Orbs/`의 에셋에서 조정합니다. 게임 수치와 시각 에셋은 분리되어 있습니다.
- 공유 방은 Host를 포함해 **2~5명**, 기본값은 **3명**입니다. Host는 일반 로비의 **ROOM PLAYERS** 옆 `−`·`+`로 인원을 정한 뒤 **HOST**로 방을 만듭니다. DEV의 **ROOM PLAYERS**도 같은 인원 설정을 사용하며, 개발자 수치를 함께 조정할 때는 **DEV → CREATE ROOM**으로 방을 만듭니다. 방을 만든 후에는 인원수를 바꿀 수 없습니다. Host 로비에는 네트워크에 실제 등록된 방 이름이 표시됩니다. 참가자는 **FIND ROOMS**로 방 목록을 열고 원하는 이름을 눌러 입장하거나, Host 주소와 포트 `7777`을 입력해 **JOIN**합니다. 설정이 모든 참가자에게 확인되고 목표 인원이 모이면 Host의 **START**가 활성화됩니다. 같은 Mac에서 직접 IP로 접속한다면 주소는 `127.0.0.1`입니다.

핫스팟에서 시험할 때는 iPhone에서 개인용 핫스팟을 켜고 그 iPhone에서 방을 만듭니다. 참가 기기를 해당 핫스팟에 연결한 뒤 **FIND ROOMS**에서 Host 화면의 방 이름과 같은 항목을 눌러 입장합니다. 목록에는 방마다 한 행만 나타나며, 선택한 방의 IPv4·IPv6 주소는 접속 경로로 함께 시도합니다. 목록의 **REFRESH**는 다시 검색하고 **CANCEL**은 검색을 닫습니다. 검색이 되지 않으면 참가 기기에서 Host의 실제 IP 주소와 포트로 **JOIN**할 수 있습니다. 두 앱의 로컬 네트워크 접근 권한을 허용하고, 방 검색 실패와 직접 IP 접속 실패는 구분해 기록합니다. 기존 프로토타입의 핫스팟 성공 기록은 이 Lab 버전의 검증 결과가 아니므로 [검증 기록](docs/VALIDATION.md)에서 새 실행 결과를 확인하세요.

### 한 대로 조합·투척 실험하기

Unity Editor에서 Play를 누르거나 **Development Build**로 만든 앱을 실행합니다. 화면 오른쪽의 **DEV**를 누르면 52개 수치의 숫자 입력과 슬라이더가 있는 패널이 열립니다. `Monster Max HP`, `Orb Generation Cost`, `Orb Lifetime (seconds)`, `Throw ...` 등 필요한 값을 조정하고 **START SOLO**를 누르세요. 한 명의 Host 세션이 즉시 시작되며, **GENERATE**로 Yin/Yang을 만든 뒤 서로 다른 두 구슬을 직접 겹쳐 Combined를 만들고 위쪽으로 빠르게 스와이프해 원통에 던질 수 있습니다. GENERATE에서는 아이템도 섞여 나옵니다. 좌우 끝 전달은 이웃이 없으므로 발생하지 않고, 같은 이유로 **잠긴 아이템도 솔로에서는 풀리지 않습니다.** 풀린 아이템을 혼자 보려면 `Item Unlock Swipes Min`·`Max`를 0으로, 아이템 없이 투척만 비교하려면 `Yin Chance`·`Yang Chance`를 50, 아이템 확률을 0으로 둡니다.

값을 다시 조정하려면 **DEV → 값 변경 → RESTART SOLO**를 누릅니다. 새 값으로 새 판을 시작하며 구슬·요괴/개인 HP·시간·스태미나가 초기화됩니다. 예를 들어 느긋하게 조합을 시험하려면 `Battle Duration (seconds) = 600`, `Orb Lifetime (seconds) = 60`, `Orb Generation Cost = 1`로 입력합니다. **DEFAULTS**는 저장된 `LabConfig.asset` 값을 입력칸에 불러오고, **END SOLO**는 로비로 돌아갑니다. 오류가 있는 값은 적용하지 않고 패널에 이유를 표시합니다. 일반 빌드에서는 DEV 버튼이 숨겨집니다.

### 공유 방의 값 바꾸기

Host에서 **DEV**를 열고 **ROOM PLAYERS**를 2~5명 중 하나로 정한 뒤, 52개 값을 숫자 입력 또는 슬라이더로 조정합니다. 숫자 입력은 정확한 값을 지정할 때 사용하며 두 조작은 서로 같은 값을 보여줍니다. DEV의 **ROOM PLAYERS**는 일반 로비의 인원 선택과 같은 값을 표시·변경합니다. **CREATE ROOM**을 누르면 선택한 인원과 조정값으로 방을 만들고, 참가자가 **FIND ROOMS**의 방 목록에서 이름을 선택하거나 직접 IP **JOIN**으로 들어오면 Host 설정을 받습니다. Host 로비에서 전투 시작 전에 다시 DEV를 열어 값을 바꿀 수 있으며, 이때 버튼은 **APPLY TO ROOM**입니다. 변경된 설정에 대해 참가자의 확인이 끝나야 **START**할 수 있습니다. 인원수는 방을 만들기 전에 선택하며, 전투 중에는 설정을 바꾸지 않습니다. 설정은 실행 중인 세션의 복사본에만 적용되고 `LabConfig.asset`에는 저장되지 않습니다.

전투 중 각 플레이어는 원통 요괴를 둘러싼 자기 자리의 시점에서 봅니다. **GENERATE**는 아래에서 솟아오르는 Yin·Yang Raw 구슬 또는 아이템 한 개를 설정 확률에 따라 만듭니다. 조합판의 구슬은 3D 구체이며 `Rigidbody`·`SphereCollider`의 PhysX 충돌을 사용합니다. 위치와 속도의 Z축은 고정해 기존 XY 평면에서만 움직입니다. 손가락이나 마우스로 밀면 관성으로 이동하다 감속하고, 상하에서 반발합니다. 좌우 끝으로 나가면 이웃 화면으로 속도와 높이를 이어 전달합니다. **Yin을 Yang에, 또는 Yang을 Yin에 직접 드래그하여 놓았을 때만** Combined가 생깁니다. 자연 충돌은 조합하지 않습니다. Combined에는 Host가 화·수·목·금·토 중 하나의 속성을 같은 확률로 정해 주며, 조합판과 날아가는 투사체 모두 그 속성 색으로 보입니다.

아이템(릴레이 구슬·시크릿 상자·회복물약·폭탄)은 구슬 정면의 아이콘으로 구분하며 **잠긴 채로** 생깁니다. 오른쪽 아래 배지의 숫자는 남은 잠금 횟수(기본 2~4)이고, 아이템을 **옆 사람 화면으로 넘길 때마다 1씩** 줄어 0이 되면 배지가 사라집니다. 아이템은 15초 뒤 사라지며 음·양과 조합되지 않습니다. 아이템의 실제 효과와 더블 탭 사용은 아직 구현되지 않았습니다.

Combined를 잡고 **구슬 판 안에서 위쪽으로 빠르게 움직이며 손을 놓으면** 그 자리에서 3D 투척합니다. 손을 놓는 가로 위치와 움직임의 방향·세기가 발사 위치와 속도에 반영되므로, 중앙으로 던지면 원통을 맞힐 수 있고 옆으로 치우치면 빗나갑니다. 느리게 옮기면 투척되지 않고 판 위에 남으며, 좌우 끝으로 빠져나가면 기존 이웃 전달이 우선합니다. 보이는 Unity Cylinder가 실제 피격체이며 Host에서 충돌을 확인한 명중만 HP를 줄입니다. 새 조작의 속도·생성 상승 거리·카메라 거리 등은 `LabConfig.asset`에서 조정합니다.

손을 놓아 투척한 Combined에는 속성 색의 짧은 비행 잔상이 남습니다. **Host가 실제 명중과 HP 감소를 확정한 경우에만** 요괴의 맞은 지점에서 원형 표시가 커졌다 사라지고, 근처에 속성 색의 `-실제 감소 HP` 숫자가 뜹니다. 마지막 명중으로 남은 HP가 설정 피해보다 적다면 숫자도 실제 감소량만 보여 줍니다. 빗나감에는 피해 숫자·피격 원형이 나오지 않습니다. 숫자와 원형은 저장 Scene의 `LabCanvas / LabImpactFeedback` 컴포넌트에서, 잔상 길이·폭·머티리얼은 `Assets/Lab/Art/Orbs/LabOrbAppearance.asset`에서 조정합니다. 모두 시각 효과이며 투사체의 물리·판정을 바꾸지 않습니다.

직선 투척의 세기 반응·발사각·공기 저항은 **DEV**의 `Throw Power Exponent`, `Throw Loft Offset (degrees)`, `Throw Air Damping`으로 한 판씩 비교할 수 있습니다. 명중·빗나감 뒤 화면에 비행 시간·최고 상승·수평 이동 거리가 나타납니다. 값의 의미와 반복 시험 순서는 [직선 투척 물리 실험 가이드](docs/THROW_TUNING_GUIDE.md)를 참조하세요.

## 개인 HP 확인

새 라운드에서 Host는 각 참가자의 개인 HP를 `Player Max HP`로 초기화합니다(기본 300, DEV 허용 범위 1~300). 전투 화면에는 **자기 HP만** `PLAYER HP 현재값 / 최대값`으로 표시하고, 로비·연결 전에는 `PLAYER HP —`로 표시합니다. 참가자는 Host가 보낸 확정 스냅샷의 값을 읽습니다. 한 사람만 0이 되어도 라운드는 계속되고, 모든 참가자의 개인 HP가 0이면 `DEFEAT`로 끝납니다. 결과 화면에는 마지막 확정 HP가 남습니다.

**현재 구슬·요괴 기능은 플레이어 HP를 감소시키지 않습니다.** Host 모델에는 이후 피해 기능이 호출할 수 있는 개인 HP 변경 경로만 있습니다. 그러므로 DEV의 최대 HP 슬라이더로 새 라운드의 시작 HP를 비교할 수 있지만, 실제 플레이에서 전원 0 패배를 재현하는 공격 패턴이나 DEV 피해 버튼은 아직 없습니다. 이 종료 규칙은 자동 검사와 실제 피해 기능 추가 후의 플레이 검증을 구분합니다.

## 빌드 출력 위치

Unity 메뉴 **C6 Lab → Build Mac App**과 **C6 Lab → Export iOS Xcode Project**의 기본 출력은 프로젝트 루트의 `Builds/Lab/<빌드 시각-식별자>/`입니다. Mac 앱은 그 안의 `macOS/C6_Physics_Lab.app`, iOS Xcode 프로젝트는 `iOS/Unity-iPhone.xcodeproj`에 만들어집니다. 매번 새 폴더를 사용하므로 이전 빌드를 덮어쓰지 않습니다. `Builds/`는 Git에서 제외되어 있습니다.

빌드가 끝나면 Console의 `C6_LAB_BUILD_OK` 메시지에 **전체 출력 경로**가 나타납니다. Unity 메뉴 **C6 Lab → Open Last Build Folder**를 누르면 이 Editor 실행 중 마지막으로 성공한 빌드의 위치가 Finder에 열립니다. 기존처럼 `C6_LAB_BUILD_OUT` 환경변수에 직접 출력 경로를 지정할 수도 있습니다. 이때도 이미 존재하는 앱/폴더에는 덮어쓰지 않고 오류를 표시하므로 새 경로를 지정하세요. 상대 경로는 프로젝트 루트 기준입니다. 실행 중인 Unity Editor에는 나중에 바꾼 환경변수가 반영되지 않을 수 있으므로, 경로를 바꾼 뒤에는 Editor를 다시 열어 빌드하세요.

## Mac에서 여러 실행본 만들기

1. Unity에서 `Lab.unity`를 연 뒤 **C6 Lab → Build Mac App**을 누릅니다. 또는 **File → Build Profiles**에서 이 Scene을 포함해 원하는 경로로 빌드할 수 있습니다.
2. 프로젝트 루트에서 아래 명령을 실행합니다. 첫 줄은 가장 최근에 만든 기본 경로의 Mac 앱을 찾습니다. 출력 위치를 직접 지정했다면 `APP`을 해당 `.app`의 전체 경로로 바꾸세요.

```sh
APP="$(find "$PWD/Builds/Lab" -maxdepth 3 -type d -name C6_Physics_Lab.app -print | sort | tail -n 1)"
test -n "$APP" || { echo 'Mac 앱을 찾지 못했습니다. 빌드 출력 경로를 확인하세요.'; exit 1; }
open -n -a "$APP" --args --lab-host --lab-port 7777
open -n -a "$APP" --args --lab-join 127.0.0.1 --lab-port 7777
open -n -a "$APP" --args --lab-join 127.0.0.1 --lab-port 7777
```

위 명령은 기본 목표 인원 **3명**의 예입니다. Host 화면에 `3 / 3`이 보이고 설정 확인이 끝나 **START**가 활성화되면 누릅니다. **2명 실험**에는 `--lab-host` 없이 앱을 두 번 연 뒤 Host 일반 로비의 **ROOM PLAYERS**를 **2**로 바꾸고 **HOST**를 누릅니다. 다른 실행본에서는 **FIND ROOMS**의 방 이름을 눌러 입장하거나 직접 IP로 **JOIN**합니다. DEV 패널에서도 같은 인원값을 선택할 수 있습니다. 자동 검증에서만 Host 인자에 `--lab-auto-start --lab-auto-generate --lab-auto-transfer`를 추가합니다. 한 프로젝트 폴더를 Editor 여러 개에서 동시에 열 필요는 없습니다. 독립 실행 앱으로 네트워크 경로를 확인합니다.

iOS Xcode 프로젝트는 Unity 메뉴 **C6 Lab → Export iOS Xcode Project**에서 만듭니다. Console에 표시된 `.../iOS` 출력 폴더에서 `Unity-iPhone.xcodeproj`를 열고, Xcode에서 개발 Team을 선택해 기기에 설치한 뒤 로컬 네트워크 권한을 허용합니다. **Unity의 iOS 출력 폴더에는 Xcode 프로젝트가 있고, Xcode가 빌드한 설치용 앱은 별도의 DerivedData에 저장됩니다.** Xcode GUI에서 Run하면 보통 사용자 Library 아래 DerivedData를 사용합니다.

Xcode가 만든 앱 파일까지 같은 보이는 빌드 폴더에서 찾으려면, 서명 설정을 마친 프로젝트에 대해 아래처럼 `-derivedDataPath`를 지정해 터미널에서 빌드할 수 있습니다. `IOS_EXPORT`에는 Unity Console에 표시된 이번 `iOS` 폴더의 전체 경로를 입력하세요. 결과 앱은 `iOS` 폴더와 나란히 만들어지는 `XcodeDerivedData/Build/Products/` 아래에 생깁니다. Xcode 빌드·서명·기기 설치는 Unity의 Xcode 프로젝트 출력 성공과 별도로 확인해야 합니다.

```sh
IOS_EXPORT="빌드 로그에 표시된 iOS 출력 폴더의 전체 경로"
DEVELOPER_DIR="/Applications/Xcode.app/Contents/Developer" xcodebuild \
  -project "$IOS_EXPORT/Unity-iPhone.xcodeproj" \
  -scheme Unity-iPhone -configuration Debug -destination 'generic/platform=iOS' \
  -derivedDataPath "$IOS_EXPORT/../XcodeDerivedData" build
```

## 프로젝트 구성

| 위치 | 역할 |
| --- | --- |
| `Assets/Lab/Scenes/Lab.unity` | 저장된 전투 화면·Canvas·카메라·컴포넌트 참조 |
| `Assets/Lab/Resources/LabConfig.asset` | HP·시간·스태미나·생성 확률·구슬/아이템 수명·아이템 잠금·물리·투척 설정 |
| `Assets/Lab/Art/` | 구슬 재질·외형(`Orbs/`)과 아이템 아이콘(`Items/`) |
| `Assets/Lab/Scripts/Session/` | 방 이름 등록·목록 검색·선택 입장·직접 IP 2~5인 연결, Host 설정 승인·상태 전파 |
| `Assets/Lab/Scripts/Core/` | 세션·구슬·자원·승패 데이터와 규칙 |
| `Assets/Lab/Scripts/Orb/` | 소유자 화면의 XY 평면에 고정된 3D Rigidbody·SphereCollider 물리와 구슬 표시 |
| `Assets/Lab/Scripts/Throw/` | 자리별 발사 위치·속도, Rigidbody 투사체·비행 잔상, Cylinder 피격체와 바닥 빗나감 |
| `Assets/Lab/Scripts/UI/` | 자리별 카메라, 저장된 UI 참조에 게임 값 반영과 버튼 연결 |
| `Assets/Lab/Tests/` | 옮긴 기능에 필요한 EditMode·PlayMode 검사 |

`LabOrbView`는 구슬 ID·3D 물리 루트와 교체 가능한 `Visual` 자식을 분리합니다. 조합판 구슬과 날아가는 투사체는 같은 `OrbVisual.prefab`과 Yin·Yang·Combined 머티리얼(Combined는 속성별 머티리얼, 비어 있으면 임시 속성 색)을 사용하지만, 조합판의 평면 물리와 투사체의 3D 비행은 각자의 물리 루트에서 처리합니다. 보드 구슬의 접촉 그림자도 물리 충돌체와 분리되어 있습니다. 요괴는 Cylinder의 피격 MeshCollider와 나중에 바꿀 수 있는 시각 Mount를 분리합니다. 아이템의 아이콘·잠금 배지는 구슬 루트에 붙어 굴러도 돌지 않고 충돌체가 없습니다. 아이템 아이콘 이미지 외의 표현은 Unity에서 만든 도형과 재질만 사용합니다.

### 조합판 구슬의 외형 바꾸기

1. `Assets/Lab/Resources/LabConfig.asset`의 **Orb Appearance**가 `Assets/Lab/Art/Orbs/LabOrbAppearance.asset`을 가리키는지 확인합니다. 이 에셋에서 공통 Visual Prefab, 종류별·속성별 Material, 아이템 아이콘 4종, 접촉 그림자의 색·크기·위치를 교체할 수 있습니다.
2. `Assets/Lab/Art/Orbs/OrbYin.mat`, `OrbYang.mat`, `OrbCombined.mat`에서 각 구슬의 색·광택을 조정합니다. `OrbVisual.prefab`에서는 구체 메시와 회전 표시를 직접 조정합니다. 외형 Prefab에 Collider나 Rigidbody를 추가하지 마세요. 충돌과 구슬 ID는 런타임 루트가 담당합니다.
3. `Assets/Lab/Scenes/Lab.unity`의 **OrbBoard → OrbBoardPlate / OrbBoardLight**에서 판 색과 빛을 조정합니다. `LabCanvas/BoardAreaTint`는 기존 배치를 보존하되 Image가 꺼져 있어 3D 조명을 가리지 않습니다. 판 크기는 `Board Height`와 화면 비율에 맞춰 실행 시 갱신됩니다.
4. Play 또는 Development Build의 DEV 솔로에서 Yin·Yang을 생성하고 직접 조합·투척해 보세요. Material 색을 변경한 뒤에는 새 라운드를 시작해야 이미 표시된 구슬에도 새 색이 적용됩니다. 외형 변경은 구슬의 반지름·소유권·전달·피격 판정을 바꾸지 않습니다.

화면 간 이동 데이터는 여전히 **구슬 ID·소유자·정규화한 XY 위치와 XY 속도**입니다. Host가 승인한 좌우 전달에서 이 값을 이어 주고, 조합판 내부의 3D 물리 컴포넌트 자체는 네트워크로 전송하지 않습니다. 확정 피격 신호에는 구슬 ID·라운드·속성·실제 피해량·요괴 기준 충돌 위치를 담습니다. 접속 프로토콜은 `5607`이며 다른 버전의 앱과 같은 방에 접속할 수 없습니다.

## 현재 기준 수치

공유 방은 Host 포함 **2~5명**이며 기본 목표는 **3명**입니다. 자리는 입장 순서이고 좌우 이웃은 원형으로 이어집니다. 개발자 솔로 모드에서는 한 명의 Host가 같은 생성·조합·투척 규칙을 시험합니다. 기본값은 요괴 HP **1,000**, 플레이어별 HP **300**, 요괴 명중 피해 **20**, 제한 시간 **180초**입니다. 개인 스태미나는 **100**으로 시작하고 구슬 생성에 **20**을 쓰며 **3초당 20**을 회복합니다. 유효 명중한 공격자에게만 **5**를 추가 회복합니다. GENERATE는 음 **40%**, 양 **40%**, 아이템 4종 각 **5%**로 생성하며, 아이템은 **2~4회** 넘겨야 풀립니다. 모든 Raw/Combined 구슬은 생성 후 **8초**, 아이템은 **15초**에 만료되며 Combined는 조합 시 새 수명이 시작됩니다. 기본 수치는 `LabConfig.asset`에서, 실행 중 솔로와 Host 방 시험 값은 DEV 패널에서 조정할 수 있습니다.

## 검증 상태

이전 3D 구슬 외형 변경에서는 Unity 6000.6.5f1의 **EditMode 12/12, PlayMode 24/24**, Mac Development 앱 빌드, iOS Xcode 프로젝트와 서명 앱 빌드가 통과했습니다. Mac 화면에서는 Yin·Yang 구체와 판 배경을, iPhone 17에서는 판 배경과 Yin·Yang·Combined의 입체 표시를 확인했습니다. **방 인원·설정 공유·핫스팟·빌드 위치 변경과 #84 생성 확률·아이템 변경의 검증 결과는 별도로 기록**하며 과거 결과를 그대로 적용하지 않습니다. 각각의 환경과 근거는 [검증 기록](docs/VALIDATION.md)에 구분해 남깁니다.

세부 범위와 합격 조건은 [docs/LAB_SCOPE.md](docs/LAB_SCOPE.md)를 참조합니다.
