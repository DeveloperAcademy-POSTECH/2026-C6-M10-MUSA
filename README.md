# C6 Physics Lab

합이오의 **연결·구슬 물리·전달·투척**만 살펴보고 조정하기 위한 Unity 프로젝트입니다. 원래 프로젝트를 복제하거나 Task별 Scene을 누적하지 않았습니다. 필요한 동작을 역할별 새 코드와 한 개의 실험 Scene으로 구성합니다. 이전 MUSA 프로토타입은 `pre-physics-lab-rebuild-2026-10-10` 태그와 이전 Git 이력에 보존했습니다. [교체 이관 범위](docs/MIGRATION_PHYSICS_LAB.md)를 참조하세요.

## 개발자 설정 빠른 참고

Editor 또는 Development Build에서 **DEV**를 열면 HP·시간·스태미나·구슬 물리·카메라·투척 관련 숫자 **39개**를 솔로 라운드별로 바꿀 수 있습니다. [개발자 모드 조정값 상세 명세](docs/DEVELOPER_MODE_PARAMETER_SPEC.md)에 모든 값의 현재 기본값, 입력 범위, 단위, 실제 효과, 값 사이의 제약과 코드 연결을 정리했습니다. 투척값을 비교하는 순서는 [투척 물리 실험 가이드](docs/THROW_TUNING_GUIDE.md)를 보세요.

| 조정 영역 | 기본값 예시 | 무엇이 바뀌나 |
| --- | --- | --- |
| 전투 | 요괴 HP 1,000 · 피해 20 · 시간 180초 | 라운드 체력·제한 시간·명중 피해 |
| 스태미나·생성 | 시작 100 · 생성 비용 20 · 회복량 20/3초 | 생성 가능 횟수와 **연속 회복 속도** |
| 3D 조합판 구슬 | 수명 8초 · 탄성 0.8 · 감속 2 | 보관 시간과 평면 위 3D 구체의 이동·반발 |
| 카메라·발사점 | 카메라 거리 8 · 발사 거리 4 | 보이는 시점과 3D 투척 시작 위치 |
| 투척 | 상승 배율 2.1 · 전진 배율 4.5 · 중력 9.81 | 손동작 판정과 공의 초기 속도·비행 궤적 |

DEV 값은 **APPLY + START/RESTART SOLO**로 새 1인 라운드를 시작할 때 적용되며 저장된 설정 에셋이나 일반 3인 방에는 반영되지 않습니다.

## 열기와 실행

- Unity Hub에서 이 저장소 루트를 프로젝트로 추가하고 **Unity 6000.6.5f1**로 엽니다. 버전 기준은 `ProjectSettings/ProjectVersion.txt`입니다.
- 실험 Scene은 `Assets/Lab/Scenes/Lab.unity`입니다. Scene에 저장된 Canvas와 GameObject는 Hierarchy/Inspector에서 직접 조정합니다.
- 게임 수치는 `Assets/Lab/Resources/LabConfig.asset`의 **단일 설정 원본**에서 조정합니다. 속성을 바꿨다면 새 세션에서 확인합니다.
- 화면에서 한 실행본은 **HOST**, 나머지 두 실행본은 Host IP와 포트 `7777`을 입력해 **JOIN**합니다. 세 명이 연결되면 Host가 **START**를 누릅니다. 같은 Mac의 세 실행본이라면 Host IP는 `127.0.0.1`입니다. 다른 기기라면 Host가 연결된 로컬 네트워크의 IP를 입력합니다.

### 한 대로 조합·투척 실험하기

Unity Editor에서 Play를 누르거나 **Development Build**로 만든 앱을 실행합니다. 화면 오른쪽의 **DEV**를 누르면 39개 수치가 있는 패널이 열립니다. `Monster Max HP`, `Orb Generation Cost`, `Orb Lifetime (seconds)`, `Throw ...` 등 필요한 값을 직접 입력하고 **APPLY + START SOLO**를 누르세요. 한 명의 Host 세션이 즉시 시작되며, **GENERATE**로 Yin/Yang을 만든 뒤 서로 다른 두 구슬을 직접 겹쳐 Combined를 만들고 위쪽으로 빠르게 스와이프해 원통에 던질 수 있습니다. 좌우 끝 전달은 이웃이 없으므로 발생하지 않습니다.

값을 다시 조정하려면 **DEV → 값 변경 → APPLY + RESTART SOLO**를 누릅니다. 새 값으로 새 판을 시작하며 구슬·HP·시간·스태미나가 초기화됩니다. 예를 들어 느긋하게 조합을 시험하려면 `Battle Duration (seconds) = 600`, `Orb Lifetime (seconds) = 60`, `Orb Generation Cost = 1`로 입력합니다. **DEFAULTS**는 저장된 `LabConfig.asset` 값을 입력칸에 불러오고, **END SOLO**는 로비로 돌아갑니다. 이 패널의 변경은 실행 중인 앱의 솔로 세션에만 적용되며 설정 에셋과 일반 3인 세션에는 저장·전파되지 않습니다. 오류가 있는 값은 적용하지 않고 패널에 이유를 표시합니다. 일반 빌드에서는 DEV 버튼이 숨겨집니다.

전투 중 각 플레이어는 원통 요괴를 둘러싼 자기 자리의 시점에서 봅니다. **GENERATE**는 아래에서 솟아오르는 Yin 또는 Yang Raw 구슬 한 개를 만듭니다. 조합판의 구슬은 3D 구체이며 `Rigidbody`·`SphereCollider`의 PhysX 충돌을 사용합니다. 위치와 속도의 Z축은 고정해 기존 XY 평면에서만 움직입니다. 손가락이나 마우스로 밀면 관성으로 이동하다 감속하고, 상하에서 반발합니다. 좌우 끝으로 나가면 이웃 화면으로 속도와 높이를 이어 전달합니다. **Yin을 Yang에, 또는 Yang을 Yin에 직접 드래그하여 놓았을 때만** Combined가 생깁니다. 자연 충돌은 조합하지 않습니다.

Combined를 잡고 **구슬 판 안에서 위쪽으로 빠르게 움직이며 손을 놓으면** 그 자리에서 3D 투척합니다. 손을 놓는 가로 위치와 움직임의 방향·세기가 발사 위치와 속도에 반영되므로, 중앙으로 던지면 원통을 맞힐 수 있고 옆으로 치우치면 빗나갑니다. 느리게 옮기면 투척되지 않고 판 위에 남으며, 좌우 끝으로 빠져나가면 기존 이웃 전달이 우선합니다. 보이는 Unity Cylinder가 실제 피격체이며 Host에서 충돌을 확인한 명중만 HP를 줄입니다. 새 조작의 속도·생성 상승 거리·카메라 거리 등은 `LabConfig.asset`에서 조정합니다.

직선 투척의 세기 반응·발사각·공기 저항은 **DEV**의 `Throw Power Exponent`, `Throw Loft Offset (degrees)`, `Throw Air Damping`으로 한 판씩 비교할 수 있습니다. 명중·빗나감 뒤 화면에 비행 시간·최고 상승·수평 이동 거리가 나타납니다. 값의 의미와 반복 시험 순서는 [직선 투척 물리 실험 가이드](docs/THROW_TUNING_GUIDE.md)를 참조하세요.

## Mac에서 세 실행본 만들기

1. Unity에서 `Lab.unity`를 연 뒤 **C6 Lab → Build Mac App**을 누릅니다. 기본 출력은 `/private/tmp/C6_Physics_Lab.app`입니다. 또는 **File → Build Profiles**에서 이 Scene을 포함해 원하는 경로로 빌드할 수 있습니다.
2. 터미널에서 아래 명령을 실행합니다. 앱을 다른 경로/이름으로 만들었다면 `APP`만 바꿉니다.

```sh
APP="/private/tmp/C6_Physics_Lab.app"
open -n -a "$APP" --args --lab-host --lab-port 7777
open -n -a "$APP" --args --lab-join 127.0.0.1 --lab-port 7777
open -n -a "$APP" --args --lab-join 127.0.0.1 --lab-port 7777
```

Host 화면에 `3 / 3`이 보이면 **START**를 누릅니다. 자동 검증에서만 Host 인자에 `--lab-auto-start --lab-auto-generate --lab-auto-transfer`를 추가합니다. 한 프로젝트 폴더를 Editor 세 개에서 동시에 열 필요는 없습니다. 세 독립 실행 앱으로 네트워크 경로를 확인합니다.

iOS Xcode 프로젝트는 Unity 메뉴 **C6 Lab → Export iOS Xcode Project**에서 만듭니다(기본 출력 `/private/tmp/C6_Physics_Lab_iOS`). 이 단계는 기기용 앱의 서명·설치와 다릅니다. Xcode에서 개인 Team을 선택해 기기에 설치하고 로컬 네트워크 권한을 허용해야 합니다.

## 프로젝트 구성

| 위치 | 역할 |
| --- | --- |
| `Assets/Lab/Scenes/Lab.unity` | 저장된 전투 화면·Canvas·카메라·컴포넌트 참조 |
| `Assets/Lab/Resources/LabConfig.asset` | HP·시간·스태미나·구슬 수명·물리·투척 설정 |
| `Assets/Lab/Scripts/Session/` | 직접 IP 3인 연결과 Host 승인·상태 전파 |
| `Assets/Lab/Scripts/Core/` | 세션·구슬·자원·승패 데이터와 규칙 |
| `Assets/Lab/Scripts/Orb/` | 소유자 화면의 XY 평면에 고정된 3D Rigidbody·SphereCollider 물리와 구슬 표시 |
| `Assets/Lab/Scripts/Throw/` | 자리별 발사 위치·속도, Rigidbody 투사체, Cylinder 피격체와 바닥 빗나감 |
| `Assets/Lab/Scripts/UI/` | 자리별 카메라, 저장된 UI 참조에 게임 값 반영과 버튼 연결 |
| `Assets/Lab/Tests/` | 옮긴 기능에 필요한 EditMode·PlayMode 검사 |

`LabOrbView`는 구슬 ID·3D 물리 루트와 교체 가능한 `Visual` 자식을 분리합니다. 조합판 구슬과 날아가는 투사체는 같은 `LabOrbVisualFactory`의 Unity 구체 표현을 사용하지만, 조합판의 평면 물리와 투사체의 3D 비행은 각자의 물리 루트에서 처리합니다. 시각 자식은 조합판의 소유권·충돌 판정을 바꾸지 않고 교체할 수 있습니다. 요괴도 Cylinder의 피격 MeshCollider와 나중에 바꿀 수 있는 시각 Mount를 분리합니다. 현재 표현은 Unity에서 생성한 도형과 색만 사용하며 외부 그래픽 에셋은 사용하지 않습니다.

화면 간 이동 데이터는 여전히 **구슬 ID·소유자·정규화한 XY 위치와 XY 속도**입니다. Host가 승인한 좌우 전달에서 이 값을 이어 주고, 조합판 내부의 3D 물리 컴포넌트 자체는 네트워크로 전송하지 않습니다. 물리 방식이 다른 구버전과 섞이지 않도록 실험실 접속 프로토콜 버전을 올렸습니다.

## 현재 기준 수치

일반 방은 정확히 **3명**이 참여하며 자리는 입장 순서입니다. 개발자 솔로 모드에서는 한 명의 Host가 같은 생성·조합·투척 규칙을 시험합니다. 기본값은 요괴 HP **1,000**, 명중 피해 **20**, 제한 시간 **180초**입니다. 개인 스태미나는 **100**으로 시작하고 구슬 생성에 **20**을 쓰며 **3초당 20**을 회복합니다. 유효 명중한 공격자에게만 **5**를 추가 회복합니다. 모든 Raw/Combined 구슬은 생성 후 **8초**에 만료되며 Combined는 조합 시 새 수명이 시작됩니다. 기본 수치는 `LabConfig.asset`에서, 실행 중 솔로 시험 값은 DEV 패널에서 조정할 수 있습니다.

## 검증 상태

3D 구슬 전환 후 Unity 6000.6.5f1에서 **EditMode 12/12, PlayMode 23/23**, Mac Development 앱 빌드와 iOS Xcode 프로젝트 생성이 통과했습니다. Mac 화면에서는 개발자 솔로 Raw 구슬의 3D 표시를 확인했습니다. 새 버전 세 실행본 연결과 iOS 앱의 네이티브 빌드·실기기 조작은 아직 실행하지 않았습니다. 각각의 환경과 근거는 [검증 기록](docs/VALIDATION.md)에 구분해 남겼습니다.

세부 범위와 합격 조건은 [docs/LAB_SCOPE.md](docs/LAB_SCOPE.md)를 참조합니다.
