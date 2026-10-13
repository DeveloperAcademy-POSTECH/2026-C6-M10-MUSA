# C6 Physics Lab 검증 범위

기준 프로젝트는 Unity 6000.6.5f1의 `Assets/Lab/Scenes/Lab.unity`다. 이전 MUSA 프로젝트의 검증 결과를 새 구현의 결과로 승계하지 않는다.

## 원본 실험실의 로컬 기록

원본 실험실 작업 폴더에서 2026-10-10에 기록한 최신 EditMode 결과는 12/12, PlayMode 결과는 20/20이다. PlayMode에는 저장 Scene을 통한 개발자 솔로 시작, Yin/Yang 직접 조합, Rigidbody 투척과 Cylinder 피격, 새 투척 수치 적용 검사가 포함됐다. Mac Development 앱은 빌드·실행되어 DEV 입력칸 39개, 솔로 시작과 구슬 생성을 화면에서 확인했다. 이 기록은 **원본 폴더의 당시 실행 결과**이며 이관 브랜치의 실행 결과가 아니다.

원본 실험실의 원시 XML·Player/Editor 로그는 공개 저장소에 넣지 않았다. 개인 경로와 실행 환경 정보가 포함될 수 있고, 공개용 요약만 이 문서에 남긴다. 실제 손/마우스 드래그 조합·투척, 이번 투척 변경의 iOS Xcode 출력·앱 서명·실기기 설치는 원본에서도 NOT_RUN이었다.

## 이관 브랜치에서 새로 확인한 항목

| 항목 | 상태 | 근거·범위 |
| --- | --- | --- |
| Unity 소스·Scene·설정·메타데이터 이관 | PASS | 원본 실험실의 `Assets/`, `Packages/`, `ProjectSettings/`와 파일 단위 비교가 일치했다. Assets 항목 50개에 대응하는 `.meta` 50개, 누락·중복 GUID 0. |
| 이관 경로의 컴파일·EditMode | PASS | Unity 6000.6.5f1의 EditMode XML에서 **12/12**, 실패 0, 건너뜀 0, 결과 Passed. |
| 이관 경로의 PlayMode | PASS | 같은 경로의 PlayMode XML에서 **20/20**, 실패 0, 건너뜀 0, 결과 Passed. |
| 이관 경로의 Mac 앱 빌드 | PASS | `C6Lab.Editor.LabBuild.Mac` Development 빌드 성공, `C6_LAB_BUILD_OK`, 출력 크기 173,292,078 bytes, 실행 파일 존재. 앱 화면을 이 이관 경로에서 다시 관찰한 것은 아니다. |
| 이관 경로의 3개 Mac 앱 직접 연결 | NOT_RUN | 별도 실행 필요 |
| 이관 경로의 iOS 출력·네이티브 빌드·실기기 | NOT_RUN | 별도 실행 필요 |

위 XML과 Editor 전체 로그는 별도 로컬 검증 공간에만 보관한다. 이관 브랜치에서 완료한 컴파일·자동 검사·Mac 빌드를 다인 실행이나 iOS 실기기 검증으로 확대하지 않는다.

## 3D 구슬 전환 검증 — 2026-10-10

기준은 Unity **6000.6.5f1**, `Assets/Lab/Scenes/Lab.unity`와 `feat/3d-orbs`의 3D 구슬 변경이다. 아래 결과는 이 절의 변경에 대해 새로 실행한 것이며, 위 이관 기록과 합산하지 않는다. 원시 XML·Editor/Player 로그와 빌드 산출물은 개인 경로에만 보관하고 저장소에는 넣지 않았다.

| 항목 | 상태 | 근거·범위 |
| --- | --- | --- |
| Scene 업그레이드·컴파일 | PASS | 저장된 Scene을 Unity Editor API로 열어 `LabOrbBoard` 레이어와 Orb/Battle 카메라 마스크를 저장했다. 컴파일 오류 없이 `C6_LAB_3D_ORB_SCENE_SAVED` 기록 확인. |
| EditMode | PASS | XML `total=12`, `passed=12`, `failed=0`, `skipped=0`, `result=Passed`. |
| PlayMode | PASS | XML `total=23`, `passed=23`, `failed=0`, `skipped=0`, `result=Passed`. 실제 PhysX 구슬의 평면 제한·반발·감속·전달 우선순위, 교체 시각의 2D/3D 물리 제거, 저장 Scene에서 Raw→Combined→3D 투사체→Cylinder 명중·HP 감소를 포함한다. |
| Mac Development 앱 빌드 | PASS | 최종 소스로 `C6_LAB_BUILD_OK target=StandaloneOSX`, 출력 173,280,456 bytes와 앱 실행 파일 확인. |
| Mac 화면 관찰 | 부분 확인 | 변경 중간 빌드에서 DEV 솔로 시작 후 Yin(파랑)·Yang(주황) Raw가 음영 있는 3D 구체로 보이고 생성 수·스태미나가 갱신됨을 확인했다. 최종 빌드는 로비 렌더링·실행까지 확인했다. 최종 빌드의 손/마우스 조합·투척 시각 확인은 미실행. |
| iOS Xcode 프로젝트 생성 | PASS | 최종 소스로 `C6_LAB_BUILD_OK target=iOS`, `Unity-iPhone.xcodeproj/project.pbxproj` 생성 확인. Xcode 네이티브 빌드·서명·설치는 별개다. |
| 새 버전 3개 실행본 연결·자동 전달 | NOT_RUN | 구버전과 접속 토큰을 분리한 코드는 확인했지만 세 앱 연결·실제 전달을 이번 변경에서 실행하지 않았다. |
| iOS 실기기 조작·렌더링 | NOT_RUN | Xcode 앱 빌드·서명·설치·실기기 드래그는 수행하지 않았다. |

PlayMode의 합격은 실제 카메라 화면의 프레임별 시각 품질이나 손가락 감각을 보증하지 않는다. 다음 실기기 점검에서는 Raw와 Combined 생성, 좌우 전달의 속도 유지, 투척 후 동일 구체 표시와 Cylinder 피격을 화면과 로그로 함께 확인한다.

## 조합판 3D 외형 개선 — 2026-10-10

기준은 같은 Unity **6000.6.5f1**과 `feat/3d-orbs`의 외형 에셋·Scene 변경이다. 아래 결과는 이 변경 후 새로 실행한 검사다. 원시 XML·로그·빌드·임시 화면 이미지는 로컬 검증 공간에만 보관한다.

| 항목 | 상태 | 확인 범위 |
| --- | --- | --- |
| Scene·에셋 연결 및 컴파일 | PASS | Unity Editor 업그레이드 명령으로 저장 Scene에 공통 Visual Prefab, 종류별 Material, 판 배경·조명을 연결했다. 기존 Canvas 객체를 보존하고 `BoardAreaTint` Image만 비활성화했다. 컴파일 오류 없음. |
| EditMode | PASS | 최종 소스 XML `total=12`, `passed=12`, `failed=0`, `skipped=0`. |
| PlayMode | PASS | 최종 소스 XML `total=24`, `passed=24`, `failed=0`, `skipped=0`. 저장 Scene의 외형 참조, 구슬·투사체의 Material 분기, 루트 물리와 시각 자식의 분리, 기존 조합·투척 흐름을 포함한다. |
| Unity 카메라 렌더 검사 | PASS | 저장 Scene의 OrbCamera 픽셀 렌더에서 Yin 파랑·Yang 주황·Combined 보라의 구체 음영과 판 배경을 확인했다. BattleCamera에서는 원통과 바닥을 확인했다. |
| Mac Development 앱 빌드·화면 | 부분 확인 | 카메라 순서 수정까지 포함한 최종 앱 `C6_LAB_BUILD_OK target=StandaloneOSX`, 출력 172,867,651 bytes. 수정 전 Mac DEV 솔로 화면에서 Yin·Yang Raw의 입체 음영, 판 배경·원통 표시, 생성 수·스태미나 갱신을 관찰했다. 최종 앱의 수동 Combined 투척 시각 확인은 미실행. |
| iOS Xcode 프로젝트 생성 | PASS | 최종 외형·카메라 변경 소스로 Unity `C6_LAB_BUILD_OK target=iOS`, `Unity-iPhone.xcodeproj/project.pbxproj` 생성 확인. |
| iOS 네이티브 앱 빌드·설치·실행 | PASS | Xcode Debug `BUILD SUCCEEDED` 후 기존 앱과 구분된 Bundle ID의 개발 앱을 연결된 iPhone 17에 설치하고 `devicectl` 실행 성공을 확인했다. 개인 Team 서명 설정은 생성된 로컬 Xcode 프로젝트에만 적용했고 저장소에는 포함하지 않았다. |
| iOS 조합판·구슬 표시 | PASS | 첫 기기 빌드에서 UI 숫자는 갱신됐으나 조합판 배경과 구슬이 모두 보이지 않아 FAIL을 확인했다. 저장 Scene의 두 Base 카메라가 같은 Depth 0이었고, OrbCamera를 BattleCamera보다 나중에 그리도록 수정했다. 재빌드·재설치 후 사용자가 iPhone 17에서 판 배경, Yin·Yang 입체 구슬, 직접 조합한 보라색 Combined와 잡기·이동 중 표시를 확인했다. |
| iOS 실기기 투척 외형 | NOT_RUN | Combined를 날릴 때의 3D 비행 화면은 이번 기기 확인 범위에 포함되지 않았다. 자동 PlayMode의 투척·피격 검사와 구분한다. |
| 3개 실행본 연결·전달 | NOT_RUN | 이번 외형 변경 후 다시 실행하지 않았다. |

## 로컬 방·개발자 설정·빌드 경로 변경 — 2026-10-12

기준은 Unity **6000.6.5f1**, `feat/lab-hotspot-session-settings`의 `Assets/Lab/Scenes/Lab.unity`다. 아래 자동 검사는 이번 브랜치의 저장 Scene과 소스에서 새로 실행했다. 원시 XML·Editor 로그·빌드 산출물은 Git에 넣지 않고 별도 로컬 검증 폴더에 보관한다. 이전 버전의 iPhone 결과를 이번 핫스팟 지원의 결과로 승계하지 않는다.

| 항목 | 상태 | 이번 변경의 근거·범위 |
| --- | --- | --- |
| Scene 연결·Unity 컴파일 | PASS | 저장 Scene에 설정 슬라이더 39개, 방 인원 슬라이더, CREATE ROOM/APPLY TO ROOM, FIND HOST, IPv4/IPv6 전송 컴포넌트 참조를 확인했다. Unity GUI Console 컴파일 오류 0. |
| EditMode | PASS | 최종 소스의 `EditMode-GUI-final.xml`: `total=26`, `passed=26`, `failed=0`, `skipped=0`, `result=Passed`. 2~5명 자리·이웃, 설정 지문·구간 분할·주소 후보 등의 검사 포함. |
| PlayMode | PASS | 최종 소스의 `PlayMode-GUI-final.xml`: `total=28`, `passed=28`, `failed=0`, `skipped=0`, `result=Passed`. 저장 Scene의 솔로·구슬·투척 경로와 2/4/5인 자리별 투척 기하 검사 포함. |
| Mac Editor의 DEV/Host 로비 UI | PASS | Unity Play에서 39개 설정 슬라이더와 인원 슬라이더를 확인했다. 인원 3→2 변경 뒤 Host가 `PLAYERS 1/2`로 열리고, 로비의 HP 값 수정 후 APPLY TO ROOM 알림을 확인했다. 다른 앱의 설정 수신을 이 관찰로 확인한 것은 아니다. |
| Mac Editor의 Bonjour 등록 | PASS | Play 진입 후 약 11초 기다려 Host를 열었을 때 `stage=register result=confirmed`를 확인했다. 앞선 첫 시도는 시작 시각을 0초로 둔 오류로 timeout이었고, 시각 수정 후 재확인했다. 방 검색·상대 기기 접속은 별개다. |
| Mac Development 앱 빌드·가시적 경로 | PASS | GUI 메뉴 `C6 Lab/Build Mac App`에서 `C6_LAB_BUILD_OK target=StandaloneOSX`, 출력 173,314,828 bytes. `Builds/Lab/20261012-144508-fd800f57/macOS/C6_Physics_Lab.app`와 실행 파일을 확인했다. 이 빌드 자체의 다중 실행은 아래 별도 항목이다. |
| iOS Xcode 프로젝트 출력·가시적 경로 | PASS | GUI 메뉴 `C6 Lab/Export iOS Xcode Project`에서 `C6_LAB_BUILD_OK target=iOS`. `Builds/Lab/20261012-144749-98a3e3c8/iOS/Unity-iPhone.xcodeproj` 및 `Info.plist`의 `_c6lab._udp`, 로컬 네트워크 사용 설명을 확인했다. Xcode 네이티브 빌드·서명은 별개다. |
| 자동 테스트용 batchmode 진입 | BLOCKED | 터미널 Unity의 Licensing Client IPC 단계가 응답하지 않아 중단했다. 위 EditMode/PlayMode 결과는 GUI Test Runner 실행의 XML로 확인했다. |
| Mac 3개 실행본의 직접 IP·기본 설정 공유 | PASS | 새 Development 앱의 Host 1개와 Client 2개를 `127.0.0.1:7777`로 실행했다. 두 Client의 `C6_LAB_CONNECTED`·같은 revision 1 설정 지문 `C6_LAB_CONFIG_READY`, Host의 `C6_LAB_START seats=0,2,1`을 확인했다. Host Raw가 소유자 0→2→1로 두 번 전달되며 속도 3.00→2.16을 기록했다. 이 검사는 같은 Mac의 기본 설정이며 핫스팟·기기 검증이 아니다. |
| Mac 앱의 FIND HOST·변경된 DEV 값의 다중 수신 | NOT_RUN | Editor에서 Host 로비 변경은 확인했으나 별도 Client가 바뀐 39개 값을 받아 적용한 실행 기록은 아직 없다. |
| iPhone 핫스팟 Host ↔ iPad 입장·설정 공유·전투 | NOT_RUN | 사용자가 이번에는 기기 검증 없이 진행하도록 요청했다. |
| iOS 네이티브 앱 빌드·서명·설치·실행 | NOT_RUN | Unity의 Xcode 프로젝트 출력과 구분하며, 이번에는 기기를 사용하지 않는다. |

기기 검증을 하지 않은 상태에서 핫스팟 호환성을 PASS로 표시하지 않는다.

## 2~5인 로비·방 목록 선택 입장 — 2026-10-12

기준은 Unity **6000.6.5f1**, `feat/#86-two-player-room-browser`의 저장 Scene `Assets/Lab/Scenes/Lab.unity`다. 이 절은 이슈 #86 변경의 검사만 기록하며, 이전 브랜치의 다중 연결 결과를 승계하지 않는다. 원시 XML·실행 로그·빌드는 로컬에만 보관한다.

| 항목 | 상태 | 이번 변경의 근거·범위 |
| --- | --- | --- |
| EditMode | PASS | `/private/tmp/C6-issue86-EditMode.xml`: `total=29`, `passed=29`, `failed=0`, `skipped=0`, `result=Passed`. |
| PlayMode | PASS | 마지막 Scene 행 높이 수정 후 `/private/tmp/C6-issue86-PlayMode-post-row-fix.xml`: `total=29`, `passed=29`, `failed=0`, `skipped=0`, `result=Passed`. 저장 Scene의 방 목록 행 참조·레이아웃 높이 검사 포함. |
| Mac Development 앱 빌드 (행 높이 수정 전) | PASS | Unity GUI 빌드 `C6_LAB_BUILD_OK target=StandaloneOSX`, `Builds/Lab/20261012-171745-bf272d49/macOS/C6_Physics_Lab.app` (305,232,449 bytes). |
| Mac Development 앱 빌드 (최종 Scene) | BLOCKED | 행 높이 수정 후 Unity Editor가 Test Runner 창에서 입력에 응답하지 않아 재빌드하지 못했다. 실행 중인 Editor를 강제 종료하거나 같은 프로젝트를 중복 실행하지 않았다. |
| Mac 앱의 2인 방 개설·방 이름 | PASS | 실행한 앱에서 목표 인원을 2/5로 변경해 Host를 열었다. Bonjour 등록 완료 후 방 이름 `C6Lab-dca7eafe`, `PLAYERS 1/2`를 화면에서 확인했다. 참가자 입장 결과와는 별개다. |
| Client의 Bonjour 방 발견 (행 높이 수정 전) | PASS | Mac Client에서 `FIND ROOMS` 후 `C6_LAB_ROOM_LIST count=1` 및 “Select a room to join.”을 확인했다. 당시 목록 행은 레이아웃 높이 0으로 보이지 않아 선택할 수 없었다. |
| Client 목록 행 표시·선택 입장·2/2 시작 (최종 Scene) | NOT_RUN | `childControlHeight=true`로 수정하고 저장 Scene 검사를 통과했으나 새 앱의 실제 화면과 연결 흐름은 확인하지 못했다. |
| iOS Xcode 출력·네이티브 빌드·서명·설치·실행 | NOT_RUN | 이번 변경에서 수행하지 않았다. |
| iPhone 핫스팟 Host ↔ iPad 검색·입장 | NOT_RUN | 사용자가 이번에는 기기 검증 없이 진행하도록 요청했다. |

자동 검사의 PASS는 Mac 또는 실기기의 방 탐색·입장 성공을 뜻하지 않는다. 최종 Scene 수정 후 Unity 편집기가 Test Runner 창에서 입력에 응답하지 않아 재빌드·Mac 2앱 입장은 수행하지 못했다. 남은 연결 확인은 새 빌드에서 Client 목록 행 표시, 해당 방 선택 입장, 양쪽 2/2 시작을 관찰하는 것이다.

## 생성 확률·아이템 공통 규칙·main(#85) 병합 — 2026-10-12

기준은 Unity **6000.6.5f1**, `feat/#84-orb-generation-items` 브랜치에서 main의 #85(오행 속성)를 병합한 소스다. 이 변경은 GENERATE 확률(DEV 6칸), 아이템 4종의 잠금·아이콘·배지·15초 수명, 이웃 전달마다 잠금 1 감소, 투사체의 속성 색 유지, DEV 48칸을 포함한다. 아이템 효과와 더블 탭은 범위 밖이다.

| 항목 | 상태 | 이번 변경의 근거·범위 |
| --- | --- | --- |
| C# 컴파일 (Runtime·EditMode·PlayMode 어셈블리) | PASS | Unity 6000.6.5f1 내장 Roslyn 컴파일러로 Editor가 만든 컴파일 응답 파일을 사용해 프로젝트 밖 임시 경로에 빌드했다. 오류 0. Unity Editor Console 확인과는 별도다. |
| 테스트 시드 계산 | 참고 | 결정적 추첨을 Python으로 재현해, 수정·추가한 EditMode 테스트의 고정 시드가 필요한 결과(음·양 쌍, 잠금 2·3·4, 다섯 속성 분포)를 내는지 확인했다. 테스트 실행 결과를 대신하지 않는다. |
| EditMode | PASS | 커밋 `427dd367`(#85 병합·문서 반영) 소스에서 사용자가 GUI Test Runner로 전체 통과를 확인했다. 통과 개수는 기록하지 않았다. |
| PlayMode | PASS | 위와 같은 소스에서 사용자가 GUI Test Runner로 전체 통과를 확인했다. 통과 개수는 기록하지 않았다. |
| DEV 솔로: 확률 6칸·합 100 거부·아이콘·잠금 배지·15초 수명 | NOT_RUN | 화면에서 아이콘이 실제로 그려지는지 포함해 확인이 필요하다. |
| Mac 3개 실행본: 이웃 전달마다 잠금 1 감소·배지 숫자 동기화·투사체 속성 색 | NOT_RUN | 솔로에는 이웃이 없어 해금을 확인할 수 없다. |
| iOS 실기기 | NOT_RUN | 이번 변경에서는 기기를 사용하지 않았다. |

## #86과 최신 main 통합 — 2026-10-12

`feat/#86-two-player-room-browser`의 `3838823b`와 `origin/main`의 `010350de`를 병합한 소스를 기준으로 한다. 위 두 절의 개별 실행 결과를 통합 결과로 승계하지 않는다.

| 항목 | 상태 | 통합 후 근거·범위 |
| --- | --- | --- |
| C# 오프라인 컴파일 | PASS | Unity 6000.6.5f1의 Bee 응답 파일·Roslyn으로 Runtime(25개 소스), Editor(9개), EditMode 테스트(3개), PlayMode 테스트(4개) 어셈블리를 각각 컴파일했다. 오류 0. 산출물은 `/private/tmp/C6-issue86-merged-compile`에 보관하며 Unity Editor 검사와는 별개다. |
| Unity Editor 컴파일·EditMode·PlayMode | NOT_RUN | 병합 소스를 Unity Editor에서 다시 열어 실행하지 않았다. #86의 29/29는 병합 전 결과다. |
| 최종 Mac 방 목록 표시·2인 입장·전투 시작 | NOT_RUN | 병합 빌드의 실제 화면·네트워크 연결은 확인하지 않았다. |
| iOS·핫스팟 실기기 | NOT_RUN | 사용자 요청에 따라 이번 작업에서 기기 검증을 수행하지 않았다. |

## 플레이어별 개인 HP — 2026-10-12

기준은 Unity **6000.6.5f1**의 `Assets/Lab/Scenes/Lab.unity`와 이슈 #93의 개인 HP 변경이다. 아래 상태는 이 변경 자체의 실행 결과만 나타낸다. 기존 #86 통합의 오프라인 컴파일이나 다른 브랜치의 Unity 검사 결과를 승계하지 않는다. 개인 HP 감소를 일으키는 실제 요괴 공격·아이템 효과는 이번 범위에 없다.

| 항목 | 상태 | 확인할 내용·현재 근거 |
| --- | --- | --- |
| 설정 범위·슬라이더 | NOT_RUN | 49번째 `Player Max HP` 기본 300, 정수 1~300 허용과 범위 밖·소수 거절, 기존 48개 인덱스 보존을 EditMode와 DEV 화면에서 확인해야 한다. |
| Host 모델의 개인 HP·전멸 판정 | NOT_RUN | 2~5인·솔로의 독립 HP 초기화, 한 명만 0인 상태 유지, 마지막 0에서 Defeat, 무효/시간 역행/종료 후 피해 거절과 Retry 초기화의 새 EditMode 검사가 작성됐다. 실행 결과는 아직 없다. |
| 설정·스냅샷 동기화 | NOT_RUN | `Player Max HP`가 공유 설정 지문과 플레이어별 JSON/분할 스냅샷에 포함되고 프로토콜 `5606`의 참가자에게 동일하게 보이는지 검사해야 한다. |
| 저장 Scene의 정적 참조 | PASS | `Assets/Lab/Scenes/Lab.unity`의 2,394개 Unity fileID 중 중복이 없고, `PlayerHpText`가 `BattleHeader` 자식이며 `LabHud.playerHpText`에 연결된 것을 구조 검사로 확인했다. 실제 화면 표시는 별도다. |
| 저장 Scene·내 HP HUD 실행 | NOT_RUN | 로비의 `PLAYER HP —`, 전투/결과의 자기 HP 현재값/최대값, 다른 플레이어 HP가 화면에 노출되지 않는지 PlayMode·실제 화면에서 확인해야 한다. |
| Unity C# 컴파일 | PASS | Unity 6000.6.5f1이 변경된 Runtime·EditMode·PlayMode 소스보다 늦은 2026-10-12 20:00:49에 각 어셈블리 DLL을 생성했고 해당 Editor 로그에 C# 컴파일 오류가 없었다. 이후 Scene 파일의 정적 UI 참조만 수정했다. |
| EditMode·PlayMode 실행 | BLOCKED | 분리된 검증 복사본의 `unity test`는 Unity 라이선스 클라이언트 초기화 시간 초과로 중단해 XML 결과가 생성되지 않았다. 작업용 GUI Editor는 열렸지만 CLI 연결 패키지 서버가 응답하지 않아 테스트를 실행하지 못했다. 테스트 총수·통과 수는 기록하지 않는다. |
| Mac 공유 방·DEV 솔로 | NOT_RUN | Host가 2~5인 방에 정한 개인 HP가 참가자의 자기 HUD에 적용되는지, 솔로 RESTART에서 초기화되는지 새 앱으로 확인해야 한다. |
| iOS Xcode 출력·앱 빌드·실기기 | NOT_RUN | 이 변경의 Unity 출력, Xcode 빌드·서명, 설치·화면 확인은 각각 별도 실행이 필요하다. |

현재 게임플레이에는 개인 HP 피해 이벤트가 없으므로 **전원 0 Defeat의 실제 플레이 검증은 불가능**하다. 자동 모델 검사와 실제 요괴 공격·아이템 효과를 붙인 후의 플레이 검증을 구분한다.

## 투척·피격 시각 피드백 — 2026-10-12, 이슈 #96

기준은 Unity **6000.6.5f1**, `feat/#96-throw-impact-feedback`의 `Assets/Lab/Scenes/Lab.unity`다. 과거 브랜치의 테스트·기기 결과를 이 변경의 결과로 승계하지 않는다. 궤적은 투척 직후부터 그리며, 피해 숫자와 피격점 원형은 Host가 실제 충돌 및 HP 감소를 확정할 때만 그린다.

| 항목 | 상태 | 이번 변경의 근거·범위 |
| --- | --- | --- |
| 저장 Scene·에셋 참조 정적 검사 | PASS | `LabCanvas`의 `LabImpactFeedback`, BattleCamera, `LabNetwork.impactFeedback`, `LabOrbAppearance.throwTrailMaterial`의 GUID/fileID 연결 및 중복 fileID 없음 확인. 실제 Unity 로드는 별도다. |
| C# 오프라인 컴파일 | PASS | Unity 6000.6.5f1의 Roslyn과 기존 Bee 응답 파일을 바탕으로 Runtime, Editor, EditMode 테스트, PlayMode 테스트 어셈블리를 각각 컴파일했다. 4개 모두 종료 코드 0, `error CS` 0. 기존 코드의 폐기 예정 API 경고는 남아 있다. 로그는 `/private/tmp/C6-issue96-offline-compile/`에 보관한다. Unity Editor 컴파일·테스트 실행과는 구분한다. |
| EditMode 실제 실행 | BLOCKED | 새 검사는 실제 피해량·속성 및 중복·이전 라운드 신호 거절을 검사하도록 작성했다. `unity test`는 라이선스 클라이언트 IPC 연결 시간 초과를 반복했고 `/private/tmp/C6-issue96-EditMode.xml`을 생성하지 못했다. 테스트 통과 수는 기록하지 않는다. |
| PlayMode 실제 실행 | NOT_RUN | 새 검사는 비행 궤적 수명, 맞은 지점 원형·피해 숫자, 저장 Scene의 명중 경로와 참조를 검사하도록 작성했다. 테스트 실행 결과는 아직 없다. |
| Mac Host/Client 동시 화면 | NOT_RUN | 두 실행본에서 같은 확정 피해량·속성색·피격점 표시와 빗나감 효과 부재를 확인해야 한다. |
| iOS 빌드·실기기 화면 | NOT_RUN | Xcode 출력·네이티브 빌드·서명·기기 설치·터치 투척을 이번 변경에서 수행하지 않았다. |

오프라인 컴파일과 정적 연결 확인은 Unity가 Scene·머티리얼을 실제로 불러와 화면에 그렸다는 증거가 아니다. Unity Test Runner 결과와 실제 화면 관찰은 각각 별도로 갱신한다.

## 아이템 4종 효과·더블 탭 — 2026-10-13, 이슈 #92

기준은 Unity **6000.6.5f1**, `feat/#92-item-effects` 브랜치다. 이 변경은 회복물약·시크릿 상자의 더블 탭 사용, 릴레이 구슬 단계(×1·×2·MAX)와 투척 피해, 폭탄 타이머·폭발·투척 피해, DEV 56칸을 포함한다. 시각 연출은 범위 밖이다.

| 항목 | 상태 | 이번 변경의 근거·범위 |
| --- | --- | --- |
| C# 오프라인 컴파일 | PASS | Unity 6000.6.5f1 내장 Roslyn으로 현재 디스크의 Runtime·EditMode·PlayMode 소스를 단계마다 컴파일했다. 오류 0. |
| PlayMode (1단계 시점) | 부분 확인 | 더블 탭·회복물약 커밋 직후 GUI Test Runner: 41개 중 37개 통과, 4개 실패. 새 더블 탭 검사는 통과했다. 실패 4개는 아래 표의 기존 main 문제다. |
| EditMode·PlayMode (최종 소스) | NOT_RUN | 시크릿 상자·릴레이 구슬·폭탄 검사를 추가한 뒤의 실행 결과는 아직 기록하지 않았다. |
| DEV 솔로: 물약 `HP_FULL` 거절·상자 열기·릴레이 ×1 투척·폭탄 타이머·폭발 | NOT_RUN | 화면 확인이 필요하다. 솔로에서 효과를 보려면 `Item Unlock Swipes Min`·`Max`를 0으로 둔다. |
| Mac 3개 실행본: 릴레이 ×2·MAX 단계, 넘겨받은 사람의 폭발과 알림 | NOT_RUN | 전달이 필요한 동작이라 솔로로는 확인할 수 없다. |
| iOS 실기기 | NOT_RUN | 이번 변경에서는 기기를 사용하지 않았다. |

이 브랜치 이전 main(#94·#95·#97·#98 병합 후)에서 이미 실패하는 PlayMode 검사는 다음과 같다. 이번 변경의 원인이 아니며 별도 수정 대상이다.

| 검사 | 원인 |
| --- | --- |
| `SavedHudAndDeveloperSliderShowLocalPlayerHpAcrossSoloRestart` | 솔로 재시작은 항상 새 세션(1라운드)을 만드는데 2라운드를 기대한다. |
| `PhysicalContactAloneNeverCombinesButDirectYinYangDropDoes` | #98에서 접촉 자동 결합이 기본으로 켜졌는데 접촉만으로는 결합하지 않기를 기대한다. |
| `FlightTrailUsesElementColorAndExpiresAfterTheProjectile` | 궤적 색이 8비트로 저장되어 0.90 대신 0.90196이 되어 허용 오차 0.001을 넘는다. |
| `SavedSceneSoloCanGenerateCombineAndHitTheCylinder` | #95의 저장 투척 수치 변경 뒤 검사의 고정 손동작이 원통에 미치지 못한다. |

## 투척 기본값·카메라 바라보는 높이 — 2026-10-13, 이슈 #100

기준은 Unity **6000.6.5f1**, `fix/#100-throw-pinata-defaults` 브랜치다. 저장 기본값 `Throw Gravity`를 15에서 30으로 바꾸고, 전투 카메라가 바라보는 높이를 DEV 57번째 값 `Camera Look Height Offset`(기본 2, 기존 동작과 같음)으로 분리했다. 솔로 명중 PlayMode 검사는 저장 투척 기본값과 분리하도록 검사 안에서 투척 수치를 고정했다.

| 항목 | 상태 | 이번 변경의 근거·범위 |
| --- | --- | --- |
| C# 오프라인 컴파일 | PASS | Unity 6000.6.5f1 내장 Roslyn으로 Runtime·EditMode·PlayMode 소스를 컴파일했다. 오류 0. |
| EditMode·PlayMode | NOT_RUN | #100 브랜치 단독 실행 결과는 기록하지 않았다. 병합 후 #101 검사와도 구분한다. |
| DEV 솔로: 중력 30 체감·명중, 카메라 바라보는 높이 조정 | NOT_RUN | 사용자가 DEV로 고른 값을 저장 기본값에 반영했으며, 반영 후 화면 확인 기록은 아직 없다. |
| Mac 3개 실행본·iOS 실기기 | NOT_RUN | #100 변경에서는 실행하지 않았다. |

## Figma 전투 UI·공동 일시정지 — 2026-10-13, 이슈 #101

기준은 Unity **6000.6.5f1**, `feat/#101-figma-battle-ui-pause`의 **#100 병합 전** `Assets/Lab/Scenes/Lab.unity`다. 검증 항목은 [범위 문서의 V-22~V-24](LAB_SCOPE.md)와 대응한다. #100과 합친 소스의 재검증은 별도이며, 이전 브랜치의 Unity·Mac·iPhone 성공 기록을 이번 Scene과 네트워크 변경의 통과 결과로 승계하지 않는다.

| 항목 | 상태 | 이번 변경의 근거·남은 실행 조건 |
| --- | --- | --- |
| Figma 투명 원본 | PASS (파일 정적 확인) | `FigmaUiAtlas.png` 1536×1024와 `BoardPlate.png` 940×794의 좌상단 RGBA가 모두 `(0,0,0,0)`임을 확인했다. 폐기한 개별 프레임 export의 불투명 파란 배경 문제는 이 원본에는 없다. 최종 Sprite 분리·Unity 표시 검사는 별개다. |
| 최종 UI PNG 분리 | PASS (파일 정적 확인) | 최종 PNG의 크기: `GenerateButton` 963×747, `NeighborBadge` 128×128, `PauseButton` 142×150, `StaminaBall`·`StaminaSocket` 각 58×58, `StaminaBar` 481×125, `TimeFrame` 472×153, `YokaiHpFrame` 560×128. 각 좌상단 α=0이다. 채움용 `SolidFill.png` 16×16은 의도적으로 불투명하다. Unity Sprite Import·화면 배치 결과가 아니다. |
| Unity Sprite Import·저장 Scene 연결 | PASS (구조 확인) | Unity 6000.6.5f1 batch의 `C6Lab.Editor.LabFigmaUiUpgrade.Apply`가 저장 Scene을 열고 UI Sprite·Material을 연결해 저장했다. `LabHud`의 전투 UI, HP 채움, 스태미나 5칸, 좌우 배지/P Text, 정지·재개·로비·DEV 버튼 참조가 비어 있지 않고 `OrbBoardPlate`가 `FigmaBoardPlate.mat`를 참조한다. Scene 렌더 화면은 별도다. |
| 결과 전용 컨트롤 Scene 연결 | PASS (구조 확인) | 최종 저장 Scene에서 `FigmaResultControls`와 `LabHud.resultControls`, 결과 문구, 재시작·같은 방 로비 복귀·나가기 버튼의 직렬화 참조가 모두 비어 있지 않다. Victory/Defeat의 시각 배치와 버튼 동작은 아직 미실행이다. |
| Unity 컴파일·UI 적용 명령 | PASS | 마지막 스태미나 소켓 보정 후 `/private/tmp/c6_issue101_upgrade_final3.log`의 `CompileScripts`, `C6_LAB_FIGMA_UI_APPLIED scene=Assets/Lab/Scenes/Lab.unity`, `Exiting batchmode successfully now!`를 확인했다. 종료 코드 0, `error CS` 없음. 실제 PlayMode 결과로 해석하지 않는다. |
| EditMode | PASS | 마지막 Scene 적용 뒤 `/private/tmp/c6_issue101_editmode_final.xml`: `total=55`, `passed=55`, `failed=0`, `skipped=0`, `result=Passed`를 확인했다. 일시정지 시계·스냅샷, 자리 기준 이웃 P번호와 기존 Core 검사를 포함한다. 이는 Unity Test Runner의 EditMode 결과이며 실제 Host/Client 화면 검증과는 별개다. |
| PlayMode 첫 실행 | FAIL | `/private/tmp/c6_issue101_playmode.xml`: `total=43`, `passed=38`, `failed=5`, `skipped=0`. 네 가지 기존 실패는 문서 위쪽의 목록과 동일하다. 추가 실패 `SavedDeveloperPanelCreatesAndRetunesTwoPlayerHostLobby`는 Host 생성 기대 직후 `network.IsHost == false`이며 실행 로그에 시험 포트 `17779` 대신 Host `7777`이 나타났다. 뒤의 단독·전체 재실행과 테스트 참조 수정으로, 이전 Scene의 인스턴스 참조가 원인이었을 가능성이 높다. |
| 실패한 Host 로비 검사 단독 재실행 | PASS (해당 검사만) | `/private/tmp/c6_issue101_host_isolated.xml`: `total=1`, `passed=1`, `failed=0`, `skipped=0`, `result=Passed`. 동일 검사가 단독 실행에서는 통과했다. 첫 전체 실행의 실패는 이전 Scene의 `NetworkManager` 인스턴스를 테스트가 참조한 영향으로 추정하며, 전체 suite의 PASS를 의미하지 않는다. |
| PlayMode 최종 전체 재실행 (#100 병합 전) | FAIL (3건) | `/private/tmp/c6_issue101_playmode_final2.xml`: `total=43`, `passed=40`, `failed=3`, `skipped=0`. 새 Host 로비·Scene UI 검사와 기존 솔로 HP 검사는 통과했다. 실패는 당시 소스의 고정 투척 손동작, #98 접촉 조합 기본값, 비행 궤적 색 허용 오차 검사다. #100은 투척 검사 수치를 변경했으므로 합친 소스에서도 고정 투척 검사가 실패한다고 단정하지 않는다. 솔로 HP 검사는 실제 구현에 맞게 새 세션·1라운드를 기대하도록 수정해 통과했다. 이 실행의 전체 PlayMode는 PASS가 아니다. |
| 마지막 UI 보정 뒤 PlayMode 핵심 UI 검사 | PASS (해당 검사만) | `/private/tmp/c6_issue101_ui_final.xml`: `total=1`, `passed=1`, `failed=0`, `skipped=0`. 저장 Scene의 Figma HUD 참조, 개인 HP/스태미나 UI와 DEV Solo 재시작을 검사했다. 전체 PlayMode 3건 실패 상태는 유지된다. |
| 승리·패배 화면의 UI 배치 | NOT_RUN | 결과 스냅샷의 마지막 HP·시간·스태미나는 유지하고 구형 `sessionControls`는 숨기도록 코드가 연결됐다. 종료 상태의 실제 Scene 렌더에서 결과 안내와 HUD가 겹치지 않는지 확인해야 한다. |
| Mac Host/Client 공유 방 | NOT_RUN | 두 실행본에서 Client 요청 후 Host 승인된 동시 정지, Host 재개, 같은 방 로비 복귀, Host DEV 갱신 ACK 후 새 판 시작과 Client의 권한 거절을 각각 관찰해야 한다. |
| iOS Xcode 출력·네이티브 빌드·서명 | NOT_RUN | Unity 출력, Xcode 빌드·서명은 별개의 검사다. 이번 변경의 실행 결과가 아직 없다. |
| iPhone/iPad 화면과 조작 | NOT_RUN | 실제 화면에서 UI 배치·문구 중첩·기기 해상도·터치·좌우 이웃·동시 일시정지를 확인해야 한다. 과거 기기 빌드의 PASS를 재사용하지 않는다. |

수동 화면 검증 때는 시간을 180초에서 내려가는 **분:초**, 실제 명중 후 요괴 HP 감소, DEV에서 조정한 내 HP 시작값, 스태미나 100→80→회복에 따른 뒤 다섯 칸, 2인 판의 양쪽 동일 P번호를 먼저 확인한다. 현재는 플레이어 HP 피해 이벤트가 없어 전투 중 개인 HP 감소 시각 검사를 할 수 없다. Figma 원본의 `03:00` 및 `구미호` 문구가 동적 숫자·내 HP 이름과 겹치지 않는지도 확인한다. 일시정지에서는 구슬·폭탄 수명과 전투 시간이 멈추고, Host 재개 후 남은 시간에서 이어지는지 확인한다.

## #100·#101 병합 후 자동 검사 — 2026-10-13

기준은 두 변경을 합친 `feat/#101-figma-battle-ui-pause` 소스, 접속 프로토콜 `5612`, 저장 기본값 `Throw Gravity = 30` 및 DEV 57번째 `Camera Look Height Offset`이다. 아래 결과는 위 #100 단독 기록 및 #101 병합 전 검사와 구분한다.

| 항목 | 상태 | 실행 결과·근거 |
| --- | --- | --- |
| EditMode 전체 | PASS | Unity 6000.6.5f1 Test Runner XML `/private/tmp/c6_issue101_merged_editmode.xml`: `total=55`, `passed=55`, `failed=0`, `skipped=0`, `result=Passed`. |
| PlayMode 전체 | FAIL (기존 2건) | Unity 6000.6.5f1 Test Runner XML `/private/tmp/c6_issue101_merged_playmode.xml`: `total=43`, `passed=41`, `failed=2`, `skipped=0`. 실패는 `PhysicalContactAloneNeverCombinesButDirectYinYangDropDoes`(#98 기본 접촉 결합과 기대값 충돌)와 `FlightTrailUsesElementColorAndExpiresAfterTheProjectile`(8비트 색 허용 오차)다. #101 병합 전 실패했던 `SavedSceneSoloCanGenerateCombineAndHitTheCylinder`는 이번 실행에서 통과했다. 전체 PlayMode는 PASS가 아니다. |
| Mac 공유 방·iOS 실기기 | NOT_RUN | 병합 소스로 두 실행본의 공동 정지·재개·로비 복귀·DEV 재시작이나 iPhone/iPad 화면·조작을 시험한 기록은 아직 없다. |

## 전투 배경 밝기·PlayMode 검사 갱신 — 2026-10-13, 이슈 #107

기준은 Unity **6000.6.5f1**, `fix/#107-battle-background-playmode`의 `Assets/Lab/Scenes/Lab.unity`다. 이 절은 #101 병합 당시의 41/43 결과를 덮어쓰지 않으며, 새 브랜치에서 다시 실행한 결과만 기록한다. 저장 Scene의 카메라 원래 색은 로비에 유지하고, `LabHud`의 전투 전용 색은 상단 RGB `(0.32, 0.42, 0.49)`, 하단 RGB `(0.18, 0.24, 0.29)`로 설정했다. `LabSceneBuilder`의 새 Scene 생성 경로에도 두 카메라 참조를 연결했다.

| 항목 | 상태 | 실행 결과·근거 |
| --- | --- | --- |
| Unity 컴파일·EditMode 전체 | PASS | `unity test`(Editor 6000.6.5f1) XML `/private/tmp/c6_issue107_editmode.xml`: `total=55`, `passed=55`, `failed=0`, `skipped=0`, `result=Passed`. 앞선 컴파일 시도의 테스트 코드 접근 제한 오류는 수정한 뒤 이 실행에서 컴파일과 검사 모두 완료했다. |
| PlayMode 전체 | PASS | `unity test` XML `/private/tmp/c6_issue107_playmode.xml`: `total=44`, `passed=44`, `failed=0`, `skipped=0`, `result=Passed`. 접촉 조합을 명시적으로 OFF/ON으로 나눠 검사하고, 물 속성 궤적 색은 8비트 채널 반올림 후 비교해 피격·잔상 소멸까지 실행했다. 전체 1건 증가는 접촉 ON 검사 추가에 따른 것이다. |
| 저장 Scene 카메라 연결 | PASS (구조 확인) | `LabHud.battleCamera`가 `BattleCamera`, `LabHud.orbCamera`가 `OrbCamera`의 fileID를 참조하고 두 전투색이 직렬화됐다. 기존 로비 카메라색·조합대·HUD 참조는 유지했다. 실제 화면의 명도 판단과는 구분한다. |
| 로비·전투·일시정지·결과 Game View 밝기와 대비 | NOT_RUN | 이 작업용 체크아웃의 Game View에서 상단 바닥·하단 나무판과 몬스터·구슬·시간·HP의 실제 대비를 직접 확인한 기록은 없다. 로비 복귀·Retry 때 색 전환도 화면으로 확인해야 한다. |
| Mac 공유 방·iOS 실기기 | NOT_RUN | 두 실행본의 상태 전환, iPhone 세로 비율과 기기 화면은 이번 변경에서 실행하지 않았다. |

## 해금된 폭탄 무폭발 — 2026-10-13, 이슈 #114

기준은 Unity **6000.6.5f1**, `fix/#114-unlocked-bomb` 브랜치다. QA에서 잠금을 모두 푼 폭탄도 생성 15초 뒤 폭발하는 것을 발견했다. #92의 "잠금을 풀어도 타이머가 멈추지 않는다" 규칙을 바로잡아, 해금된 폭탄은 해금 시점부터 구슬 수명(기본 8초)을 새로 갖는 일반 공격 구슬로 동작하고 폭발하지 않게 했다. 남은 초 표시는 잠긴 동안만 보인다.

| 항목 | 상태 | 이번 변경의 근거·범위 |
| --- | --- | --- |
| C# 오프라인 컴파일 | PASS | Unity 6000.6.5f1 내장 Roslyn으로 Runtime·EditMode·PlayMode 소스를 컴파일했다. 오류 0. |
| EditMode·PlayMode | NOT_RUN | 해금 폭탄 무폭발·수명 재시작 검사를 추가했으며 실행 결과는 아직 기록하지 않았다. |
| 3인 실행 QA: 해금 폭탄이 터지지 않음 | NOT_RUN | 원래 버그를 발견한 3인 흐름으로 재확인이 필요하다. |

