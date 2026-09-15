> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# C6 / 합이오 Unity 프로토타입 개발 보고서

> **상세 코드 후속편:** [보고서 3 — Unity 코드 읽기·유지보수 실습 안내서](UNITY_BEGINNER_CODE_MAINTENANCE_GUIDE.md)는 현재 빌드24의 함수 호출, C# 발췌, 디버깅과 수정 예시를 설명합니다. 이 문서의 과거 개발 기록은 그대로 보존합니다.

> **이 보고서는 빌드20 시점의 기록입니다.** 이후 빌드21~24에서 추가된 2D 물리, 손떼기 3D 투척, 최대 5인 연결, 화면 사이의 연속 이동은 [초보자 개발 보고서 2](UNITY_BEGINNER_PHYSICS_MULTIPLAYER_REPORT.md)에 정리했습니다. 아래 본문의 당시 사양과 검증 기록은 보존합니다.

**기획 사양을 실행 가능한 게임으로 옮기고, 직접 수정하며 개발하기 위한 기록**

| 구분 | 기준 |
|---|---|
| 작성일 | 2026-09-13 |
| 프로젝트 | C6_Prototype / 합이오 |
| 구현 기준 | 빌드20, 소스 보관 커밋 `d7e0735` |
| 당시 개발 위치 | Mac의 `C6_Prototype` 폴더. 이관 후에는 내려받은 조직 저장소 루트에서 연다 |
| 현재 공개 이관본 | [DeveloperAcademy-POSTECH/2026-C6-M10-MUSA](https://github.com/DeveloperAcademy-POSTECH/2026-C6-M10-MUSA). 당시 개발 이력·원시 근거는 원본 비공개 저장소에 보존 |
| 대상 독자 | Unity를 처음 배우며 기존 프로토타입을 직접 이해·수정하려는 개발자 |
| 함께 읽을 문서 | [직접 빌드하기 README](../BUILD_README.md) |

## 1. 이번 프로토타입의 목표와 현재 결과

현재 목표는 **기본 기획서의 사양을 동작하는 형태로 구현하고, 필요한 기능을 커스텀하면서 직접 개발할 수 있는 기반을 확보하는 것**이다. 이 보고서는 그 기반이 어떤 순서로 만들어졌고, Unity에서 무엇을 열어 어떤 부분을 수정하면 되는지 설명한다.

개발은 빈 Unity 프로젝트에서 시작했다. 먼저 iPhone에 작은 앱을 설치하고 수정본을 다시 반영했다. 이후 두 참가자의 통신, 구슬 입력, 실제 3D 피격, 자원과 조합, 한 판의 승패, 로비와 좌우 전달을 차례로 연결했다. 현재는 같은 Wi-Fi에서 iPhone과 iPad로 방을 만들고, 생성·조합·전달·공격을 수행할 수 있는 소스와 씬이 남아 있다.

현재 사양은 **원래 통합 기획에서 채택한 범위에 사용자가 이후 지정한 변경을 반영한 결과**다. 처음의 스태미나 문장, 조합 영역, 드래그 조작을 실제 사용 의도에 맞춰 수정했다. 원문을 변경 없이 전부 구현했다고 해석하면 안 된다.

사용자는 현재 수준을 프로토타입으로 충분하다고 수용했다. 추가 안정화와 T14~T16은 진행하지 않았고, 빌드20까지의 작업을 GitHub에 보관했다. 이 보고서와 빌드 README를 작성하는 동안 게임 코드 수정이나 새 빌드·실기기 시험은 수행하지 않았다.

### 작업의 역할

사용자는 기획과 우선순위를 정하고, 스태미나·조합·전달 등의 변경 방향을 전달했으며, 실제 기기에서 조작한 결과를 확인했다. Codex는 프로젝트 구성, C# 코드와 씬 준비 도구, 빌드·설치·자동 검사, 로그 대조와 기록을 담당했다. 사용자가 모든 컴포넌트를 Editor에서 직접 배치하거나 코드를 직접 작성한 작업 이력은 아니다. 앞으로 직접 개발할 수 있도록 이 협업의 결과를 풀어 쓴 기록이다.

## 2. 기본 기획과 사용자 커스텀의 연결

| 항목 | 원래 채택 사양 또는 초기 구현 | 현재 적용된 동작 |
|---|---|---|
| 협동 구조 | 같은 Wi-Fi의 P1 Host·P2 Client, 2명 | 유지. 두 기기의 결과는 Host가 결정 |
| 검증 기기 | iPhone 2대 | 보유 기기에 맞춰 iPhone 17 + iPad mini 6 사용 승인 |
| 시작 자원·구슬 | 최대/시작 자원 5, 첫 생성 5개/비용 5, 이후 1개/비용 1 | 시작 구슬 **0개**, 자원 **100/100**, 모든 생성은 **Raw 1개/비용 20** |
| 시간 회복 | 원래 채택 범위에서는 없음 | **3초당 20을 연속 회복**. 최대 100 |
| 명중 회복 | 실제 유효 피격의 공격자 +1 | 실제 유효 피격의 **공격자에게 +5**, 최대 100 |
| 음양 조합 | Yin + Yang → Combined | 유지. 같은 음양끼리는 거부하며 재료가 남음 |
| 조합 장소 | 초기 화면에 구역을 나누어 안내 | 별도 Combined only 띠 제거. UI를 제외한 **하단 구슬 작업 공간 전체**에서 조합 |
| 공격 조작 | Attack Zone 진입 판정 | Combined를 하단에서 **상단 전투 영역으로 드래그**하면 승인 후 자동 발사 |
| 잡기 표현 | 기본 드래그 표시 | 잡은 구슬 확대·빛·그림자로 들고 있음을 강조 |
| 좌우 전달 | 왼쪽→상대 오른쪽, 오른쪽→상대 왼쪽 | 유지. 수평으로 이동하고 **손을 놓을 때** 전달. 가장자리 조작 거리 보완 |
| 전경의 상태바 | 초기에는 상태바 동작도 중단으로 처리되는 문제 | 상태바·제어 센터를 열어도 전경에서는 시간과 연결 유지 |
| 기본 전투 | 몬스터 HP 100, 기본 피해 20, 한 판 180초 | 유지. 실제 유효 명중 5회로 HP 0, 시간 종료 시 패배 |

“최초 5개”는 현재 **가득 찬 100 게이지로 비용 20을 다섯 번 지불할 수 있다**는 뜻이다. 시작할 때 구슬 다섯 개를 자동 배치하거나 첫 버튼 한 번으로 다섯 개를 생성하지 않는다. 화면의 5개 구분선도 내부 자원을 정수 0~5로 제한하지 않는다.

회복은 3초를 기다렸다가 한꺼번에 20을 주는 방식이 아니다. Host가 경과 시간에 비례해 초당 약 6.67씩 계산하고 게이지를 올린다. 일반 음양은 Host Seed로 결정하며, 연속 두 번 눌렀다고 반드시 Yin과 Yang이 하나씩 나오지는 않는다.

근거: [통합 지시서](../C6_Hapio_Codex_Integrated_Master_Prompt.md), [스태미나 변경](T07_RULE_CHANGE.md), [입력 변경](T09_INPUT_REVISION.md), [기기 대체 결정](G2_DEVICE_CHANGE.md), [상태바·전달 보완](T12_IMPLEMENTATION.md).

## 3. 개발 환경과 Unity에서 쓰인 개념

### 실제 사용한 환경

| 구성 | 기록된 기준 | 이 프로젝트에서 하는 일 |
|---|---|---|
| Unity Editor | 6000.5.7f1 | 씬 편집, C# 실행, 게임 출력 |
| URP | 17.5.0 | 카메라와 재질을 이용한 화면 렌더링 |
| Input System | 1.20.0 | 마우스·터치 입력 처리 |
| Netcode for GameObjects / Transport | 2.13.1 / 6.5.0 | Host·Client 통신 |
| UGUI | 2.5.0 | Canvas 위의 버튼·글자·게이지 |
| Test Framework | 1.7.0 | 규칙과 실행 동작 자동 검사 |
| Mac / Xcode | macOS 26.6.2 arm64 / Xcode 26.6 | iOS 앱을 빌드하고 서명 |
| 실제 시험 기기 | iPhone 17, iPad mini 6 | 터치·화면·같은 Wi-Fi의 두 참가자 확인 |
| 당시 앱 서명 | 개인 Apple 개발 Team으로 개발 서명 | 다시 빌드하는 사람은 자신의 Team을 선택 |

이는 개발 기록의 환경이며 다른 버전에서의 성공을 보증하는 호환성 표는 아니다. 다시 열 때는 저장소의 [ProjectVersion](../ProjectSettings/ProjectVersion.txt)과 [패키지 목록](../Packages/manifest.json)을 기준으로 버전을 맞춘다. 환경·설치 기록은 [IOS_RUNBOOK](IOS_RUNBOOK.md)에 있다.

### 초보자가 알아둘 용어를 현재 프로젝트에 대입하기

| 용어 | 쉬운 의미 | C6에서 실제로 보는 것 |
|---|---|---|
| Project | 게임을 만드는 전체 폴더 | `Assets`, `Packages`, `ProjectSettings`를 포함한 `C6_Prototype` |
| Scene | 오브젝트와 연결을 저장한 장면 파일 | 현재 앱의 `InterruptionBattle.unity` |
| GameObject | 장면 안에서 기능을 담는 오브젝트 | `T13InterruptionBattle`, 카메라, 훈련 표적 |
| Component | 오브젝트에 붙어 일을 하는 기능 | 화면 분할, 입력 처리, 방 연결 등의 C# 컴포넌트 |
| Inspector | 선택한 대상의 연결·값을 보는 창 | Config 참조, 카메라 참조, 설정 값 확인 |
| ScriptableObject | 데이터를 에셋으로 저장하는 Unity 방식 | `ScreenLayoutConfig.asset`에 체력·비용·회복 등 저장 |
| Canvas / HUD | 화면 위 안내와 조작 UI | HP, 시간, 게이지, GENERATE, 로비 버튼 |
| Rigidbody / Collider | 물리 이동과 충돌 모양 | 발사체와 표적의 실제 피격 판정 |
| Material | 표면의 색과 표시 방식 | 무대·더미·구슬의 재질 |
| `.meta` | Unity가 에셋의 고유 참조를 유지하는 파일 | 씬·스크립트·재질과 함께 Git에 보관 |
| Host / Client | 최종 결과를 결정하는 쪽 / 요청하고 결과를 받는 쪽 | P1 / P2 |

Unity의 GameObject는 컴포넌트를 조합해 동작하며, Inspector와 스크립트로 속성을 다룬다. ScriptableObject는 GameObject에 붙이는 동작 컴포넌트와 구분되는 데이터 에셋이다. 일반 개념은 [Unity 컴포넌트 문서](https://docs.unity3d.com/6000.0/Documentation/Manual/UsingComponents.html)와 [ScriptableObject 문서](https://docs.unity3d.com/6000.0/Documentation/Manual/class-ScriptableObject.html)를 참고한다. 두 링크는 Unity 6.0의 개념 설명이며 현재 코드 기준 버전은 위의 6000.5.7f1이다.

## 4. 어떤 순서로 개발했는가

Task는 개발 단위이고 Gate는 여러 조건을 묶은 통과 기준이다. 어떤 기능의 구현이나 개별 시험이 끝났다고 해당 Gate 전체가 통과한 것은 아니다. 아래 결과는 각 단계의 당시 빌드를 기준으로 요약했다.

| 단계 | 실제 작업과 결과 | 초보자가 이 과정에서 이해할 점 |
|---|---|---|
| T00 · 프로젝트 준비 | 새 프로젝트·폴더·문서·비공개 저장소 구성. 컴파일과 iOS 지원 확인 | 코드 작성 전에 엔진 버전과 프로젝트 위치부터 일치시킨다 |
| T01 · 최소 앱 | 글자와 클릭 횟수가 있는 A를 설치하고, B로 변경해 같은 iPhone에 재설치·확인 | 작은 화면 하나로 수정→빌드→기기 반영 경로를 먼저 익힌다 |
| T02 · 직접 연결 | 주소·포트를 입력해 Host/Join. iPhone↔Mac 연결과 재연결 확인 | 연결 성공과 게임 규칙 구현을 나누어 확인한다 |
| T03 · 공유 숫자 | Host가 숫자 증가 요청을 승인. 승인된 iPhone+iPad 조건에서 각 50건, 총 100건 확인 | 복잡한 게임 이전에 두 화면이 같은 값을 보는 원리를 만든다 |
| T04 · 화면 골격 | 상단 3D·하단 2D, 두 카메라, Safe Area, UI 배치. 자동 검사·Mac 화면·iOS 출력 확인 | 카메라가 그리는 영역과 손가락 좌표를 연결한다 |
| T05 · 구슬 입력 | 구슬 ID, 선택·드래그·놓기·취소, 잠금 처리. 자동·Mac 입력 확인 | 손가락 입력과 게임 규칙의 승인을 구분한다 |
| T06 · 실제 공격 | 2D 구슬을 3D 발사체로 연결하고 실제 충돌로 피해 계산. 빌드8 iPhone 직접 20회 피격 확인 | 화면에 날아가는 연출과 물리 충돌이 모두 맞아야 한다 |
| T07 · 생성·자원 | 시작 0개·자원 100·비용 20·연속 회복·명중 +5. 자동·Mac 검사와 iOS 출력 | 기획 문장의 뜻을 수치와 규칙으로 구체화한다 |
| T08 · 조합 | Yin+Yang을 새 Combined로 만들고 같은 음양은 거부. 자동·Mac 검사와 iOS 출력 | 조합은 그림을 바꾸는 것과 함께 재료·ID를 처리해야 한다 |
| T09 · 한 판 연결 | 생성→조합→발사→회복, 180초, 승패, 결과 고정, Retry. 빌드13 iPhone DEV SOLO에서 확인 | 기능을 시작·진행·종료·재시작의 한 흐름으로 묶는다 |
| T10-A · 로비 | Bonjour 방 탐색, 참가자, Ready, Host Start. Mac 실제 탐색 및 빌드14 서명 | 게임 전에 두 참가자가 같은 방과 설정에 합의한다 |
| T10-B · 전투 동기화 | 로비와 전투 연결, 생성·조합·시간·승패·Retry 공유. Mac 두 앱 상태 일치 및 빌드15 서명 | 각 화면이 별도로 계산한 결과가 어긋나지 않게 한다 |
| T11 · 좌우 전달 | 손떼기 승인, 구슬 ID 유지, 소유자 변경, 상대 반대편 도착. Mac 전달·피격 및 빌드16 서명 | 전달을 복제 대신 소유권과 표시 위치의 이동으로 다룬다 |
| T12 · 두 기기 통합 | 실제 iPhone+iPad에서 로비·전투·전달·조합 확인. 빌드18 상태바, 빌드19 가장자리 조작 보완 | PC 검사 후 실제 손 위치·화면 크기·OS 동작을 확인한다 |
| T13 · 연결 중단 보완 | 빌드20 유효 응답 감시와 오류 안내·수동 재진입. 자동 검사 및 두 기기 일부 중단 상황 확인 | 연결이 끊긴 뒤의 화면과 입력 상태도 정리한다 |

T04·T05·T07·T08의 iOS 출력은 Xcode 프로젝트 생성까지의 결과다. T10-A·T10-B·T11의 서명된 앱도 그 빌드의 실기기 완료를 뜻하지 않는다. 실제 터치·두 기기 결과는 T06·T09·T12·T13의 해당 기록으로 구분한다.

초기 과정의 근거: [원본 비공개 자료: T00 — 공개 요약](VALIDATION_SUMMARY.md), [원본 비공개 자료: T01 — 공개 요약](VALIDATION_SUMMARY.md), [직접 연결](T02_RUNBOOK.md), [원본 비공개 자료: 최종 G2 기기 대체 — 공개 요약](VALIDATION_SUMMARY.md), [화면](T04_RUNBOOK.md), [입력](T05_RUNBOOK.md), [원본 비공개 자료: 물리 피격 20회 — 공개 요약](VALIDATION_SUMMARY.md).

게임과 협동 과정의 근거: [T07](T07_DECISION.md), [T08](T08_VALIDATION.md), [T09/G4](T09_G4_VALIDATION.md), [T10-A](T10A_VALIDATION.md), [T10-B](T10B_VALIDATION.md), [T11](T11_VALIDATION.md), [T12 빌드19](T12_BUILD19_FOLLOWUP.md), [T13 빌드20](T13_VALIDATION.md).

## 5. 지금 프로젝트를 열면 무엇이 있는가

### 폴더 지도

| 위치 | 역할 |
|---|---|
| [Assets/_Project/HapioMVP](../Assets/_Project/HapioMVP/) | 현재 게임의 씬·스크립트·설정·시험 |
| [Scenes](../Assets/_Project/HapioMVP/Scenes/) | 단계별 저장 씬. 이전 단계를 보존하면서 새 통합 씬을 추가 |
| [Config/ScreenLayoutConfig.asset](../Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset) | 게임 수치의 단일 원본 |
| [Editor](../Assets/_Project/HapioMVP/Editor/) | 씬 준비·검사·Mac 빌드·iOS 출력 도구 |
| [Packages](../Packages/) / [ProjectSettings](../ProjectSettings/) | 의존성 버전과 Unity 프로젝트 설정 |
| [docs](./) / [공개 검증 요약](VALIDATION_SUMMARY.md) | 결정·실행 절차·검증 범위. 원시 evidence 디렉터리는 공개 이관에 포함하지 않음 |
| `Builds/`, `Logs/`, `Library/` | 로컬 빌드·원시 로그·Unity 캐시. Git 업로드 대상에서 제외 |

### 빌드20 당시 씬은 `InterruptionBattle.unity`

[보존된 빌드20 씬](../Assets/_Project/HapioMVP/Scenes/InterruptionBattle.unity)의 중심 GameObject는 `T13InterruptionBattle`이다. 두 카메라, 무대·훈련 표적, 입력·로비·게임 연결 컴포넌트와 참조가 저장되어 있다. 빌드20 당시 앱 빌드에 활성화한 씬은 이 씬 하나였다.

**모든 버튼과 구슬을 Editor에서 미리 배치한 구조는 아니다.** `T09Hud`와 `T10LobbyHud`가 실행 중 Canvas·글자·버튼을 만들고, 구슬과 발사체도 필요할 때 코드로 생성한다. 따라서 Play 전 Hierarchy에서 GENERATE 버튼이나 시작 구슬을 찾지 못하는 것이 이상 현상은 아니다. 빌드20 당시 `Assets`에는 `.prefab` 파일이 없었으며 Prefab 조립 중심의 구현은 아니었다. 현재 이관본에는 이후 추가한 몬스터 Prefab도 포함된다.

이 방식은 반복 가능한 코드로 화면을 준비하는 데 도움이 된다. 대신 초보자가 화면 문구나 버튼 배치를 영구 변경하려면 실행 중 만들어진 오브젝트뿐 아니라 **그것을 만드는 HUD 코드**를 알아야 한다. 기존 `T09`·`T10` 이름의 스크립트가 최신 T13 씬에서 사용되는 것도 앞선 기능을 재사용했기 때문이다.

### 빌드20 장면을 학습용으로 둘러보는 순서

1. Unity Hub에서 `Assets`만이 아니라 `Assets`·`Packages`·`ProjectSettings`가 함께 있는 조직 저장소 루트 폴더를 연다. 6000.5.7f1로 패키지 불러오기와 컴파일을 기다린다.
2. Project 창에서 `Assets/_Project/HapioMVP/Scenes/InterruptionBattle.unity`를 연다.
3. Hierarchy의 `T13InterruptionBattle`을 선택하고 Inspector에서 연결된 Config·카메라·컨트롤러를 살펴본다.
4. Play를 눌러 실행 중 만들어진 UI와 Hierarchy 변화를 비교한다. 최신 씬은 두 참가자가 준비해야 전투를 시작한다.
5. Play를 멈추고 Config 에셋을 선택해 저장된 수치를 확인한다. 변경 전후는 Git의 파일 차이로 확인한다.

혼자 생성·조합·공격을 익히려면 이전 [BattleLoop.unity](../Assets/_Project/HapioMVP/Scenes/BattleLoop.unity)를 열고 Play → DEV SOLO ON → HOST → HOST START로 진행할 수 있다. DEV SOLO는 Editor·개발 빌드용이다. **이 장면은 T09의 기본 전투 학습용**이며 최신 방 탐색·좌우 전달·중단 처리를 모두 체험하는 장면은 아니다. 당시 빌드20의 출력 메뉴는 T13이었다. 현재 최신 앱을 만들 때는 [직접 빌드 README](../BUILD_README.md)의 P4 절차를 사용한다.

## 6. 한 번의 조작이 코드에서 처리되는 방식

두 참가자의 게임은 Host가 결과를 확정하는 구조다. 화면에서 버튼을 눌렀다는 사실만으로 구슬이나 피해가 확정되지 않는다.

**생성:** GENERATE 입력 → Host에 요청 → 현재 라운드·소유자·자원·보관 한도 확인 → 비용 20과 Raw 1개를 함께 확정 → 양쪽에 승인된 상태 전달.

**조합:** 사용 가능한 같은 소유자의 Yin과 Yang을 겹쳐 놓음 → Host가 재료 확인 → 재료 두 개 종료 → 새로운 ID의 Combined 하나 생성. 같은 음양이면 합쳐지지 않는다.

**전달:** 구슬을 수평으로 가장자리까지 이동하고 손을 놓음 → Host 승인 → 같은 구슬 ID의 소유자 변경 → 상대 반대쪽 가장자리에 표시. 두 사람뿐이므로 왼쪽·오른쪽 이웃이 모두 같은 상대여도 정상이다.

**공격:** Combined가 상단 전투 영역 경계를 통과 → Host 승인 → 2D 표시 제거와 3D 발사 → Host의 Rigidbody/Collider가 실제 피격 확인 → 몬스터 피해와 공격자 회복 적용 → 결과 동기화. Client의 비행 표시는 결과를 보여 주는 역할이며 별도로 피해를 확정하지 않는다.

이 과정에서 전달·발사는 ID를 유지하고, 조합은 새 ID를 만든다. 중복 요청이나 이전 판의 요청으로 비용·구슬·피해가 두 번 반영되지 않도록 요청과 상태를 검사한다. 초보자가 버튼 코드에 체력 감소를 직접 추가하면 이 승인 흐름과 충돌할 수 있으므로, 바꾸려는 규칙의 담당 코드를 먼저 찾는 것이 좋다.

### 기능별 수정 위치

아래 경로는 모두 `Assets/_Project/HapioMVP/` 아래다.

| 바꾸려는 내용 | 먼저 읽을 파일 |
|---|---|
| 상단·하단 비율과 카메라 영역 | [Presentation/SplitScreenLayout.cs](../Assets/_Project/HapioMVP/Presentation/SplitScreenLayout.cs) |
| 전투 글자·버튼·배치 | [Battle/T09Hud.cs](../Assets/_Project/HapioMVP/Battle/T09Hud.cs) |
| 로비 글자·버튼·배치 | [Lobby/T10LobbyHud.cs](../Assets/_Project/HapioMVP/Lobby/T10LobbyHud.cs) |
| 터치·마우스 입력과 드래그 판정 | [Orbs/OrbPointerInput.cs](../Assets/_Project/HapioMVP/Orbs/OrbPointerInput.cs), [OrbGestureEngine.cs](../Assets/_Project/HapioMVP/Orbs/OrbGestureEngine.cs) |
| 구슬 색·형태·들기 확대와 빛 | [Orbs/OrbView.cs](../Assets/_Project/HapioMVP/Orbs/OrbView.cs) |
| 입력과 전투 동작 연결 | [Battle/T09BattleController.cs](../Assets/_Project/HapioMVP/Battle/T09BattleController.cs) |
| 생성·시간 회복·명중 보상 | [Resources/HostResourceAuthority.cs](../Assets/_Project/HapioMVP/Resources/HostResourceAuthority.cs) |
| 조합 승인과 구슬 ID·소유권 | [Combination/HostCombinationAuthority.cs](../Assets/_Project/HapioMVP/Combination/HostCombinationAuthority.cs), [Orbs/HostOrbRegistry.cs](../Assets/_Project/HapioMVP/Orbs/HostOrbRegistry.cs) |
| 피해와 실제 3D 충돌 | [Attack/AttackAuthority.cs](../Assets/_Project/HapioMVP/Attack/AttackAuthority.cs), [HostProjectile3D.cs](../Assets/_Project/HapioMVP/Attack/HostProjectile3D.cs), [MonsterHitTarget.cs](../Assets/_Project/HapioMVP/Attack/MonsterHitTarget.cs) |
| 시간·승패·Retry | [Battle/HostBattleClock.cs](../Assets/_Project/HapioMVP/Battle/HostBattleClock.cs), [BattleSession.cs](../Assets/_Project/HapioMVP/Battle/BattleSession.cs) |
| 방과 Ready | [Lobby/T10LobbySession.cs](../Assets/_Project/HapioMVP/Lobby/T10LobbySession.cs), [LobbyAuthority.cs](../Assets/_Project/HapioMVP/Lobby/LobbyAuthority.cs) |
| 전체 상태 동기화·응답 중단 | [GameSync/T10GameSession.cs](../Assets/_Project/HapioMVP/GameSync/T10GameSession.cs), [PeerResponseWatchdog.cs](../Assets/_Project/HapioMVP/GameSync/PeerResponseWatchdog.cs) |

## 7. Inspector에서 직접 조절할 수 있는 값

원본은 [ScreenLayoutConfig.asset](../Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset) 하나다. 항목 정의는 [ScreenLayoutConfig.cs](../Assets/_Project/HapioMVP/Presentation/ScreenLayoutConfig.cs)에 있다. Inspector는 코드명의 단어를 띄워 보여 주므로 아래 코드명을 함께 보면 찾기 쉽다.

| 설정 | 코드 필드 | 현재 저장 값 | 바뀌는 내용 |
|---|---|---:|---|
| 상단 비율 | `upperFraction` | 0.55 | 전투 55%·하단 작업 45% |
| 몬스터 HP | `monsterMaxHp` | 100 | 승리까지 필요한 피해량 |
| 기본 피해 | `baseDamage` | 20 | 유효 피격 한 번의 피해 |
| 최대·시작 자원 | `staminaMax`, `staminaStart` | 100, 100 | 개인 게이지 범위와 시작량 |
| 생성 비용 | `generateCost` | 20 | Raw 1개에 필요한 자원 |
| 회복량·기준 시간 | `staminaRecoveryAmount`, `staminaRecoverySeconds` | 20, 3 | 두 값의 비율로 초당 회복량 계산 |
| 명중 보상 | `staminaHitRecovery` | 5 | 공격자만 받는 자원 보너스 |
| 보관 한도 | `orbStorageLimit` | 20 | 개인 보관 한도, 코드상 1~20 범위 |
| 전투 시간 | `battleDurationSeconds` | 180 | 한 판 제한 시간 |
| 팀 HP 비율 | `teamHpDecayPerSecond` | 1 | 남은 시간과 팀 HP의 대응 |
| 조합 판정 거리 | `combinationRadiusFraction` | 0.08 | 화면 너비에 대한 거리 비율 |
| 구슬 반지름 비율 | `orbRadiusScreenFraction` | 0.055 | 화면 배치 조건에 따라 조정되는 표시 크기의 기준 |
| 발사체 속도·수명·반지름 | `projectileSpeed`, `projectileLifetime`, `projectileRadius` | 12, 3, 0.165 | 비행과 실제 충돌의 조건 |

현재 팀 HP는 몬스터 공격으로 깎이는 별도 체력이 아니라 **남은 시간 × 비율**이며 기본 시작값은 180이다. 실제 방어와 몬스터 공격 시스템은 구현 범위에 포함하지 않았다.

기존 설정에는 예전 씬에서 쓰던 `attackZoneHeightFraction`도 남아 있다. 이를 바꾸는 것을 최신 씬의 상단 경계 발사 변경과 동일시하면 안 된다. 수평 전달에도 일반 이동 기준과 최신 가장자리 보정이 함께 적용된다. 시작 구슬 0개 역시 Inspector의 시작 수량 항목이 아니라 현재 라운드 규칙이다.

### 수치 변경과 새 규칙 추가는 작업 범위가 다르다

최신 씬의 [GameRuntimeConfig](../Assets/_Project/HapioMVP/GameSync/GameRuntimeConfig.cs)는 원본 에셋에서 실행용 사본을 만든다. Host가 방 설정을 보내고 Client는 [LobbyHostConfig](../Assets/_Project/HapioMVP/Lobby/LobbyHostConfig.cs)의 필드·범위와 설정 지문을 검사해 같은 값을 적용한다. 네트워크에서 받은 값으로 원본 에셋을 덮어쓰지 않는다.

표의 모든 설정이 네트워크로 전달되는 것은 아니다. 예를 들어 구슬 표시 크기 기준은 각 앱의 로컬 에셋 값이므로, 관련 값을 바꾸면 두 기기에서 같은 설정으로 다시 빌드해 확인한다.

기존 체력·비용 값을 조정하는 것과 “세 번째 구슬 종류” 같은 규칙을 추가하는 것은 다르다. 새 규칙에는 설정뿐 아니라 Host 승인, 전달할 데이터, Client 적용, 화면, 관련 시험의 변경이 필요할 수 있다. 구슬 크기도 가장자리 전달 계산과 연결되어 있으므로 단순한 장식 값으로만 보지 않는다.

### 앞으로 해볼 수 있는 작은 연습 — 아직 적용·실행하지 않음

| 연습 제안 | 예상되는 변화 | 직접 확인할 내용 |
|---|---|---|
| `baseDamage`를 20에서 25로 | HP 100이라면 유효 명중 4회로 승리 | 실제 충돌 때만 감소하는지, 두 화면 HP·결과가 같은지 |
| 회복 기준 시간을 3에서 4로 | 초당 회복이 약 6.67에서 5로 감소 | Playing 중 게이지 속도, 최대 100 제한, 생성 비용 유지 |
| HUD 안내 문구 하나 수정 | 규칙은 유지하고 안내만 변경 | 다시 Play했을 때 문구가 유지되고 기기에서 잘 읽히는지 |

위 표는 학습 제안이며 변경 완료나 시험 PASS 기록이 아니다. 한 번에 값 하나를 바꾸고 Play를 멈춘 상태에서 원본 에셋을 편집하면 원인을 추적하기 쉽다. ScriptableObject 에셋 수정은 Play 종료로 자동 복구된다고 가정하지 말고 Git 차이를 확인한다. 기존 시험이 원래 수치를 명시하고 있다면 실패 이유가 의도한 사양 변경인지 먼저 확인하고, 시험을 무조건 삭제하거나 비활성화하지 않는다.

## 8. 실제 문제를 고치며 남긴 개발 경험

### 기획 문장의 해석을 구현 전에 맞추기

“최초 5개”를 구슬 개수로 읽는 것과 자원으로 읽는 것은 시작 화면과 생성 요청 전체를 바꾼다. 사용자 설명을 통해 시작 0개·최대 100·비용 20을 확정하고, 시간 회복과 명중 +5도 별도 규칙으로 기록했다. 이후 UI, Host 계산, 시험이 같은 의미를 사용하도록 연결했다. 근거: [T07 규칙 변경](T07_RULE_CHANGE.md).

### 충돌 성공과 화면에서 보이는 성공을 구분하기

T06에서는 실제 피격은 발생하지만 투사체가 화면 아래쪽에 가려지는 문제가 있었다. 발사 위치·폭·반지름을 조정한 뒤 실제 표시와 터치를 확인했다. HP가 감소했다는 로그만으로 비행 연출까지 정상이라고 결론 내릴 수 없다는 사례다. 근거: [T06 결정](T06_DECISION.md).

### 손가락으로 잡는 위치와 드래그 취소를 확인하기

사용자는 구슬을 들고 있다는 표시를 원했고, 확대와 빛을 추가했다. 실제 두 기기에서는 가장자리의 구슬을 바깥쪽에서 잡는 위치와 남은 이동 거리가 전달 판정에 영향을 주었다. 전달 거리를 보완하면서 손을 놓기 전에는 전달하지 않는 규칙을 유지했다.

한 번은 왕복 정상이라는 응답 뒤 로그에서 iPad의 취소 입력과 남아 있는 구슬을 확인했다. 이후 정상 손떼기로 반환된 기록을 별도로 확보했다. 취소 원인을 확인 없이 특정 OS 동작으로 단정하지 않았다. 근거: [입력 개정](T09_INPUT_REVISION.md), [빌드19 후속](T12_CONTINUATION.md).

### 상태바와 실제 백그라운드를 구분하기

시간 종료 뒤 자동으로 로비에 돌아온다는 보고를 조사했다. 최초 사례의 원인은 미확정으로 남겼고, 별도 재현에서는 시간이 약 103초 남았을 때 iPhone의 상태바 조작에 따른 일시 중단을 확인했다. 사용자는 결과 화면은 유지하고 이 중단 처리만 수정하도록 요청했다. 빌드18에서 전경의 상태바·제어 센터와 실제 백그라운드를 구분하고, 두 기기 각각의 상태바 조작 중 연결과 시간이 유지되는지 확인했다. 이를 실제 홈 이동·잠금에서도 계속 동작한다는 결과로 확대하지 않았다. 근거: [T12 검증](T12_VALIDATION.md).

## 9. 어디까지 확인했고 무엇이 남았는가

검증 기록은 `PASS` 실행·통과, `FAIL` 실행·실패, `NOT_RUN` 미실행, `BLOCKED` 실행을 막는 조건 발생으로 구분했다. 문서가 존재하거나 사용자가 사양을 승인했다는 사실을 실행 증거로 대신하지 않았다.

| 확인 층위 | 확인하는 것 | 이 프로젝트의 대표 기록 |
|---|---|---|
| 컴파일 | C#과 참조가 빌드 가능한지 | 단계별 Unity 실행 기록 |
| EditMode / PlayMode | 규칙과 Unity 실행 동작 | 빌드20 EditMode 948 + PlayMode 89 = **1,037개 실행·통과** |
| Mac 두 앱 | 실제 통신과 상태 동기화 | 빌드20 동일 상태 204쌍, 지문 불일치 0 |
| iOS 출력 | Xcode 프로젝트가 생성되는지 | 단계별 export 결과. 앱 설치와 별도 |
| 앱 빌드·서명 | Xcode가 기기에 설치할 앱을 만드는지 | 빌드20 앱 생성·개인 Team 서명 |
| 실제 손 조작 | 기기 화면과 터치가 맞는지 | 빌드13 기본 전투, 빌드18 상태바, 빌드19 전달·조합·피격 |
| 실제 두 기기의 자동 시험 | 사람이 반복 누르지 않아도 규칙이 적용되는지 | 빌드19 별도 정상 생성·5회 명중·공격자 +5·Victory·Retry |
| 일부 중단 상황 | 유효 응답이 끊긴 뒤 오류와 새 연결 | 빌드20 두 기기 응답 보류·오류 정리·새 방·첫 생성·정상 END |

빌드20의 자동 시험 1,037개는 게임 기능 1,037개를 만들었다는 뜻이 아니다. 같은 규칙의 정상·거절·중복·이전 요청·경계 조건을 검사하는 사례들이 포함되어 있다. 자동으로 공급한 시험 구슬과 정상 생성, 자동 공격과 실제 손가락 공격도 기록에서 분리했다.

현재 보존한 한계는 다음과 같다.

- T09/G4의 통과는 빌드13 **한 기기 DEV SOLO** 범위다. 최신 두 기기 전체 Gate의 통과로 승계하지 않는다.
- T12/G5 전체와 T13 전체는 `NOT_RUN`이다. 구현된 기능과 실행한 개별 항목의 결과는 그대로 보존한다.
- 빌드20의 실제 홈 이동·잠금·Wi-Fi 해제·권한 팝업·상태바 시험은 `NOT_RUN`이다. 실제 강제 종료 시험은 당시 `BLOCKED`였으며 수행하지 않았다. 추가 중단 시험은 사용자 수용 결정에 따라 중단했다.
- 빌드18의 반복 전달 결과를 빌드19·20에서 다시 실행한 것으로 기록하지 않는다. 가장자리의 아주 짧은 최소 이동 거리 자체도 실기기에서 입증했다고 확대하지 않는다.
- 빌드20 검증 사본에서 자동 변경된 렌더 설정 4개와 SceneTemplate 설정은 실제 개발 루트에 이관하지 않았다. 검증 앱과 저장소가 모든 설정·바이너리까지 동일하다는 주장은 하지 않는다. 직접 새로 빌드한 결과는 새 실행으로 확인한다.

상세 근거: [빌드20 검증](T13_VALIDATION.md), [원본 비공개 자료: 시험 집계 — 공개 요약](VALIDATION_SUMMARY.md), [원본 비공개 자료: Mac 결과 — 공개 요약](VALIDATION_SUMMARY.md), [원본 비공개 자료: 기기 결과 — 공개 요약](VALIDATION_SUMMARY.md), [원본 비공개 자료: 설정 차이 — 공개 요약](VALIDATION_SUMMARY.md).

## 10. 직접 개발을 이어갈 때의 기준

현재 결과물은 **기본 협동 전투를 실행하고 코드를 읽으며, 수치를 바꾸고 필요한 동작을 추가할 수 있는 개발용 프로토타입**이다. 정식 아트, 3인 이상, 실제 방어·몬스터 공격, 자동 재접속, Host 교체, 외부 온라인 서비스 등을 구현한 제품은 아니다. 현재 사용 목적을 위해 이 항목들을 모두 먼저 만들 필요는 없으며, 새로운 필요가 생기면 범위를 정한다.

직접 개발할 때는 “원하는 동작 한 문장 → 담당 설정·코드 찾기 → 작은 변경 → Editor 확인 → 필요할 때 두 앱·실기기 확인 → 변경과 결과를 Git에 기록” 순서로 진행할 수 있다. 예를 들어 구슬 생성 비용을 바꾼다면 숫자뿐 아니라 버튼 표시, 승인된 차감, 두 참가자의 결과가 의도대로 맞는지 함께 확인한다.

빌드20 당시 게임 작업은 원본 비공개 저장소의 `d7e0735`에 보관했다. 당시에는 소스·씬·`.meta`·Config·설정·문서와 검토된 증거를 함께 관리했다. 현재 공개 조직 저장소에는 이관 snapshot의 소스·문서·공개 검증 요약을 포함하고 전체 Git 이력과 원시 증거는 복사하지 않았다. 생성된 Xcode 프로젝트·앱·원시 로그·서명 자료·Unity 캐시도 포함하지 않는다. 새 환경에서 최신 빌드24를 만들 때는 [직접 빌드하기 README](../BUILD_README.md)를 따른다.

이전 문서의 “다음 Task”, “아직 push하지 않음”은 각 작성 시점의 역사 기록이다. 현재는 빌드20 구현을 보존한 상태에서 개발 학습과 커스텀을 위한 문서를 추가한 것이며, T14~T16을 자동으로 재개한 것이 아니다.
