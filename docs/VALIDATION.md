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

## 요괴 피격 Y축 회전·진단용 그레디언트 — 2026-10-13, 이슈 #115

기준은 Unity **6000.6.5f1**, `origin/main`의 `03fc1ce7`에서 분기한 `feat/#115-yokai-hit-spin`이다. 회전은 유효 명중 후 Host가 피격점·속도로 계산하며, 클라이언트는 승인된 부호 있는 충격량을 재생하고 스냅샷 각도·속도를 보정한다. 원래 X/Z 흔들림·바운스와 피해 경로는 유지한다. 아래 상태는 **이 브랜치의 현재 검사만** 나타낸다.

| 항목 | 상태 | 실행 결과·남은 확인 |
| --- | --- | --- |
| 저장 Scene·에셋 정적 연결 | PASS (구조 확인) | 요괴 Hitbox Cylinder의 Material GUID가 새 URP Lit `TargetSpinGradient.mat`을 가리키고, Material의 `_BaseMap`은 1024×512 비대칭 그레디언트 PNG를 가리킨다. Scene의 변경은 해당 Material 참조 한 곳뿐이며 MeshCollider·기존 컴포넌트 참조는 보존됐다. 실제 Unity import·화면 표시는 별도다. |
| C# 오프라인 컴파일 | PASS (컴파일만) | Unity 6000.6.5f1 내장 Roslyn과 기존 main의 Bee 참조를 사용해 이 브랜치의 Runtime·Editor·EditMode·PlayMode 소스 네 어셈블리를 각각 컴파일했다. 네 단계 종료 코드 0, C# 오류 0. 기존 폐기 예정 API 경고가 남아 있다. Unity Editor 컴파일·테스트 실행을 뜻하지 않는다. |
| EditMode 실제 실행 | BLOCKED | `unity test`와 Editor 직접 `-runTests`를 각각 시도했으나 Licensing Client IPC 연결/초기화가 시간 초과됐다. 기존 라이선스는 CLI에서 active/signed-in으로 확인됐지만 이 실행의 XML은 생성되지 않았다. 실행 수·통과 수는 기록하지 않는다. |
| PlayMode 실제 실행 | NOT_RUN | 피격 위치별 반대 방향, 중심 명중, 360° 이상 회전·감쇠·속도 상한, 반복 타격·reset·재질 보존 검사가 추가돼 있다. 라이선스 초기화가 막혀 실행 결과는 없다. |
| Mac Host/Client 회전 동기화·일시정지·Retry 화면 | NOT_RUN | 두 앱에서 동일 명중의 회전 방향·각도 수렴, 네트워크 지연 시 보정, 일시정지·새 판의 초기화를 화면으로 확인해야 한다. |
| iOS Xcode 출력·실기기 | NOT_RUN | 이 변경에 대한 Xcode 프로젝트 생성, 앱 빌드·서명·설치 및 실기기 렌더·터치 확인은 별도다. |

추가한 검사는 `LabHangingPresentationTests`, `LabDeveloperConfigTests`, `LabImpactAuthorityTests`, `LabSessionSettingsTests`에 있다. 소스와 테스트의 존재를 실행 성공으로 해석하지 않는다.

## 아이템 잠금 자물쇠·해금 연출 — 2026-10-13, 이슈 #119

기준은 Unity **6000.6.5f1**, 최신 main(`81b0169a`, #114·#115 포함)에서 분기한 `feat/#119-item-unlock-effect`다. QA에서 전달로 잠금을 푸는 방식이 직관적이지 않다는 의견이 나와, 잠긴 아이템은 아이콘을 흐리게(기본 40%) 하고 자물쇠를 겹쳐 표시한다. 마지막 잠금을 푸는 전달을 Host가 승인하면 그 시각(`unlockedAt`)을 구슬 스냅샷에 담는다. 받는 화면은 1초 안의 해금만 감지해 구슬당 한 번 연출한다: 자물쇠 상승·사라짐과 아이콘 선명(0~0.3초) → 1.5배 금색 원이 구슬 크기로 수축(0.1~0.5초) → 한 번 반짝임(0.5~0.6초). 다음 전달에서는 해금 시각을 지워 그다음 화면에는 연출이 없다. 스냅샷 필드가 늘어 프로토콜을 `5614`로 올렸다. 자물쇠 이미지는 36×50 원본을 256×256 정사각형으로 키워 `Assets/Lab/Art/Items/ItemLock.png`에 두었다.

| 항목 | 상태 | 실행 결과·남은 확인 |
| --- | --- | --- |
| 에셋 정적 연결 | PASS (구조 확인) | `LabOrbAppearance.asset`의 `lockIcon`이 `ItemLock.png.meta`의 GUID를 가리킨다. 실제 화면 표시는 별도다. |
| C# 오프라인 컴파일 | PASS (컴파일만) | Unity 6000.6.5f1 내장 Roslyn으로 Runtime·EditMode·PlayMode 소스를 단계마다 컴파일했다. 오류 0. Unity Editor 컴파일·테스트 실행을 뜻하지 않는다. |
| EditMode·PlayMode 실제 실행 | NOT_RUN | 아래 검사를 추가했으며 실행 결과는 아직 기록하지 않았다. |
| 3인 실행: 받는 화면의 해금 연출, 다음 화면의 무연출 | NOT_RUN | Mac 앱 3개에서 잠금 1회 아이템을 넘겨 받는 화면만 연출되는지, 원·반짝임의 크기와 밝기가 적당한지 확인해야 한다. |
| iOS 실기기 | NOT_RUN | 실기기 화면에서 자물쇠·원의 선명도와 크기 확인이 필요하다. |

추가·변경한 검사: `LabModelTests.OnlyTheUnlockingHandoffStampsTheUnlockTimeAndTheNextHandoffClearsIt`, `LabSessionSettingsTests`의 스냅샷 왕복(해금 시각 포함), `LabOrbPhysicsTests`의 자물쇠·흐림 확인, `JustUnlockedItemPlaysTheUnlockEffectOnceAndAnOpenItemDoesNot`, `UnlockLiftsThePadlockThenLandsAShrinkingRingWithOneGlow`.

## 요괴 피격 입자 효과 — 2026-10-13, 이슈 #108

기준은 Unity **6000.6.5f1**, `main`의 `e2b02839`에서 분기한 `feat/#108-hit-particle-vfx`와 [V-17](LAB_SCOPE.md)이다. 대상은 현재 `Lab.unity`의 **Cylinder Hitbox**이며, 별도 구미호 모델 작업(#128)은 포함하지 않는다. 기존 Host 확정 피해·속성색 숫자와 투척 잔상은 유지하고, 이전 Canvas 선분 원형만 월드 공간의 풀링된 `ParticleSystem` 코어·스파크·확대 충격파로 교체하는 범위다. `LabNetwork`의 Host 명중 확정 및 수신 승인 경로가 `LabImpactFeedback.PlayHit`을 호출하고, 여기서 `LabImpactParticleVfx`가 기존 피격점과 충돌 속도를 받아 표시한다. 입자 자체는 피해 판정이나 추가 네트워크 메시지를 만들지 않는다. 아래 상태는 **이 브랜치에서 새로 수행한 검사만** 나타내며 #96의 통과 기록을 승계하지 않는다.

| 항목 | 상태 | 이번 변경의 실행 결과·남은 근거 |
| --- | --- | --- |
| Unity Editor 컴파일 | PASS | 2026-10-13 Unity Editor 6000.6.5f1에서 최종 소스의 Runtime·Editor·테스트 어셈블리를 다시 컴파일했다. `Logs/Editor.log`의 마지막 `Tundra build success`와 C# 오류 0을 확인했다. 기존 폐기 예정 API 경고는 남아 있다. |
| 저장 Scene·프리팹 연결 | PASS (구조 확인) | Unity에서 `Lab.unity`를 열어 `Impact Particle VFX` 오브젝트를 확인했다. 저장된 `LabCanvas.LabImpactFeedback.particleVfx` → Manager → 프리팹·BattleCamera 참조가 유효하다. 프리팹의 코어·스파크·충격파 Renderer 3개에 URP 파티클 머티리얼이 연결되고 PlayMode에서 해당 셰이더가 지원되는지 검사했다. 실제 전투 화면의 크기·밝기 평가는 아직 아니다. |
| EditMode 실제 실행 | PASS | Unity Test Runner XML `/private/tmp/c6_issue108_editmode.xml`: `total=60`, `passed=60`, `failed=0`, `skipped=0`, `result=Passed`. 기존 판정·네트워크 규칙을 포함한 전체 EditMode 검사다. |
| PlayMode 실제 실행 | PASS (자동 검사) | 최종 Unity Test Runner XML `/private/tmp/c6_issue108_playmode_final.xml`: `total=54`, `passed=54`, `failed=0`, `skipped=0`, `result=Passed`. 실제 저장 입자 프리팹을 사용한 2개 피격 검사에서 피해 숫자·속성색·이동 Hitbox 추적·Collider 부재·풀 상한·일시정지·Clear를 확인했다. 전체 실행 종료 때 발견한 파괴된 `LabTarget` 참조 오류는 두 Reset 경로를 고쳐 재실행한 최종 Editor 로그 구간에서 재발하지 않았다. 실전 Host/Client 시각 품질의 통과 판정은 아니다. |
| Mac Host/Client 동시 화면 | NOT_RUN | 동일 명중의 양쪽 화면에 같은 속성색·피격점의 숫자와 입자가 표시되고, 빗나감·중복 패킷은 효과가 없으며 재시작·이탈 뒤 잔상이 남지 않는지 확인한다. |
| iOS Xcode 출력·빌드·실기기 | NOT_RUN | Unity Xcode 프로젝트 생성, Xcode 빌드·서명, iPhone 설치·실행과 실제 피격 시 크기·가독성·프레임 흐름은 각각 별도 검증이 필요하다. |

사운드·진동·카메라 흔들림, 데미지 계산 변경, 투척 궤적 변경은 이번 이슈의 검증 범위가 아니다. 실행 결과가 확보되면 날짜·커밋·환경과 XML 또는 화면 근거를 함께 기록한다.

2026-10-14 PR 준비 중 `origin/main`의 `e5b6ded1`을 병합했다. `Lab.unity`에는 하단 HUD 변경과 이 이슈의 `Impact Particle VFX` 오브젝트·프리팹 참조가 모두 남아 있으며 충돌 마커는 없다. **병합 후 EditMode·PlayMode 재실행은 NOT_RUN**이다. GUI Editor의 Fatal Error 보고 창은 09:29:58에 열렸으나 원인은 확인되지 않았다. 이후 09:30:23에 시작한 별도 Unity CLI 재검사는 `attempt to write a readonly database`가 출력되어 중단했다. 새 결과 XML은 생성되지 않았으므로 위 60/60·54/54는 병합 전 `3fb12790`에서 수행한 결과로만 읽는다.

## 로비 P번호 선택·인원별 순환 자리 — 2026-10-14, 이슈 #139

기준은 Unity **6000.6.5f1**, `origin/main`의 `a8df7f8e`에서 분기한 `feat/#139-player-slot-ring`이다. 기존 접속순 자리를 로비에서 Host가 승인한 P1~PN 선택으로 바꾸며, N은 방 생성 전에 정한 2~5명이다. P번호 오름차순으로 360°/N 간격의 원형 자리를 구성하고 PN에서 P1로 이어진다. 이 절은 이 브랜치에서 새로 확보한 증거만 기록한다.

| 항목 | 상태 | 실행 결과·남은 확인 |
| --- | --- | --- |
| 저장된 참조를 이용한 C# 오프라인 컴파일 | PASS (Unity 실행 아님) | 설치된 Unity 6000.6.5f1의 Roslyn·프로젝트 `Library/Bee` 참조로 이 브랜치의 Runtime 33개, EditMode 7개, PlayMode 5개, Editor 13개 소스를 별도 컴파일했다. 오류 0건이며 기존 폐기 예정 API 경고만 있었다. Unity Editor의 실제 에셋 임포트·컴파일 판정으로 대체하지 않는다. |
| Unity Editor 컴파일·Scene 로드 | BLOCKED | 2026-10-14 이 브랜치의 Unity 실행은 Licensing Client channel 초기화에서 약 75초 뒤 실패했다. C# 컴파일 및 Scene 로드 단계에는 도달하지 않았다. `unity license status`도 `LICENSING_CLIENT_UNAVAILABLE`을 반환했다. |
| 저장 Scene P번호 선택 UI 참조 | PASS (파일 구조만) | `Lab.unity`에 P1~P5 Button과 `LabHud` 직렬화 참조를 저장했다. 새 구성요소 61개의 파일 ID 중복·부모/자식·참조 검사와 `git diff --check`는 통과했다. **Unity Editor 로드와 실제 화면은 미검증**이다. |
| P번호 증가 방향의 기하 검사 | PASS (수학적 검사만) | 3·4·5인 자리에서 각 P의 화면 오른쪽 벡터와 P+1 자리 방향의 내적이 양수인 것을 별도 계산했다. 실제 카메라·전달 화면은 PlayMode와 여러 실행본에서 확인해야 한다. |
| EditMode | NOT_RUN | 슬롯 중복·인원 범위·퇴장·선택순 전달·재시작 검사를 추가하되, Unity Test Runner XML의 실행 수와 결과가 나오기 전에는 PASS로 기록하지 않는다. |
| PlayMode | NOT_RUN | 저장 Scene의 선택 버튼 표시·Host 승인·P번호 HUD·카메라 및 기존 전투 흐름을 실행해야 한다. 3~5인에서는 오른쪽 전달 P+1과 실제 화면 오른쪽 자리의 일치를 확인한다. |
| Mac 다중 실행본·iOS 실기기 | NOT_RUN | 2~5인 방에서 서로 다른 접속순/P번호, 동시 점유 시도, 좌우 전달·투척·재입장·재시작을 확인해야 한다. |

PR 전 읽기 검토에서 방 검색의 Bonjour TXT 버전만 `5614`로 남아 있던 불일치를 발견했다. 이를 접속 프로토콜 상수 `5615`에서 생성하도록 고친 뒤 Runtime·EditMode·PlayMode·Editor 소스 오프라인 컴파일을 다시 수행해 C# 오류 0건을 확인했다. 실제 방 검색과 접속은 위 표대로 미실행이다.

## 생성 북: 무작위 배치·탭 생성·튀어나오기·떠돌기 — 2026-10-14, 이슈 #137

기준은 Unity **6000.6.5f1**, 최신 main(`a8df7f8e`, #117 생성 부채꼴 포함)에서 분기한 `feat/#137-movable-drum`이다. 화면 아래 고정 생성 버튼을 조합판 위의 **생성 북**으로 바꿨다.

- **배치**: 북은 기본 한 게임에 1개(DEV 1~5개)이고, 판마다 Host가 받을 사람을 무작위로 뽑는다. 북이 있는 사람만 생성할 수 있다.
- **생성**: 북을 탭할 때마다 Host가 북 위치에서 360° 무작위 방향으로 구슬을 만든다. 벽 근처에서는 그쪽 옆 가장자리에 닿기 전에 멈출 속도로 제한한다.
- **연출**: 기존의 0.25초 상승(물리 정지) 연출을 없앴다. 구슬은 북 바깥에서 바로 날아가고, 그림만 북 한가운데에서 커지며 나온다(0.15초). 그동안 다른 구슬·북과는 부딪히지 않는다.
- **북 조작**: 북은 끌기·좌우 전달이 되고 던져지지 않는다. 수명·보관 한도·조합·폭탄의 대상이 아니다.
- **떠돌기**: 가만히 두면 사방으로 천천히 떠돌고 회전하며, 혼자서는 이웃 화면으로 넘어가지 않는다.
- **설정·통신**: DEV 항목에 북 5개(인덱스 74~78)를 더해 79개가 됐고, 17·18번은 튀어나오기 크기·시간으로 의미가 바뀌었다. 통신 버전은 #139와 합치며 `5616`으로 올렸다.
- **씬·에셋**: 하단 `FigmaGenerateButton`은 저장 Scene에서 비활성화했고, `LabOrbAppearance.asset`의 `Drum Sprite`에 `GenerateButton.png`를 연결했다.

| 항목 | 상태 | 실행 결과·남은 확인 |
| --- | --- | --- |
| C# 오프라인 컴파일 | PASS (컴파일만) | Unity 6000.6.5f1 내장 Roslyn으로 Runtime·EditMode·PlayMode·Editor(`Assembly-CSharp-Editor`) 네 묶음을 단계마다 컴파일했다. 오류 0. 2단계 중 Editor 묶음을 빠뜨려 Unity에서 `LabSceneBuilder` 컴파일 오류가 한 번 났고, 이후 네 묶음 모두 확인했다. |
| 에셋·Scene 정적 연결 | PASS (구조 확인) | `LabOrbAppearance.asset`의 `drumSprite`가 `GenerateButton.png` 스프라이트 GUID를 가리킨다. Scene의 `FigmaGenerateButton`은 `m_IsActive: 0`이고 `LabHud`의 생성 버튼 참조는 정리됐다. |
| EditMode·PlayMode 실제 실행 | NOT_RUN (최종 기록 없음) | 단계별로 Test Runner를 실행하며 실패를 고쳤다: 솔로 통합 검사의 접촉 결합 설정 누락, 숨긴 버튼 판정, 튀어나오기 첫 프레임 위치. 최종 전체 통과 수는 아직 기록하지 않았다. |
| DEV 솔로 화면 | NOT_RUN (부분 관찰) | 2단계에서 탭 생성은 확인했고 북 그림·하단 버튼 문제를 고쳤다. 4·3단계 이후 튀어나오기·연타·떠돌기·회전 화면 확인 기록은 없다. |
| 3인 실행: 북 무작위 배치·전달·연타, 북 2~5개 | NOT_RUN | Mac 앱 3개에서 북을 받은 사람만 생성되는지, 좌우 스와이프로 넘어가는지, 떠돌기만으로는 넘어가지 않는지 확인해야 한다. |
| iOS 실기기 | NOT_RUN | 북 그림(98×98 원본) 선명도, 탭·끌기 판정 크기, 연타 체감 확인이 필요하다. |

추가·변경한 검사:
- **EditMode** `LabModelTests`:
  - `OneDrumGoesToARandomPlayerAndOnlyItsHolderCanGenerate`
  - `DrumsGoToDifferentPlayersBeforeAnyoneGetsASecond`
  - `DrumPassesToANeighborButIsNeverStoredThrownExpiredOrBlownUp`
  - `GeneratedOrbsLeaveTheDrumInEveryDirectionWithoutCrossingASideEdge`
- **EditMode** `LabDeveloperConfigTests`: DEV 79개·기본값·북 개수 1~5 정수
- **EditMode** `LabSessionSettingsTests`: 북 스냅샷 왕복
- **PlayMode** `LabOrbPhysicsTests`:
  - 북 탭·끌기·던지기 불가
  - 북 그림
  - 튀어나오기와 연타
  - 떠돌기, 가장자리 되돌림, 스치는 충돌 회전
- **PlayMode** `LabDeveloperSoloIntegrationTests`: 북 탭으로 생성 → 결합 → 명중, 하단 버튼 숨김

## 스태미나 회복 간격·명중 보상 — 2026-10-14, 이슈 #146

기준은 Unity **6000.6.5f1**, #143(생성 북) 머지 후 main(`3fef812d`)에서 분기한 `feat/#146-stamina-tuning`이다. 북이 한 게임에 1개라 받은 사람이 스태미나를 몰아 쓰고 넘긴다. 세 사람을 한 바퀴 도는 약 6~9초 안에 빈 스태미나가 거의 다 차도록 회복 간격을 **3초 → 1.7초**(회복량 20 유지, 초당 약 11.8, 0→100 약 8.5초)로 줄이고, 명중 보상을 **5 → 10**(구슬 ½개)으로 올렸다. `LabConfig.asset`과 코드 기본값을 같은 값으로 맞췄다. 같은 브랜치에서 #125 작업 중 에셋에 잘못 저장된 퍼짐 각도 55°를 180°(모든 방향)로 되돌렸다.

| 항목 | 상태 | 실행 결과·남은 확인 |
| --- | --- | --- |
| C# 오프라인 컴파일 | PASS (컴파일만) | Unity 6000.6.5f1 내장 Roslyn으로 Runtime·EditMode·PlayMode·Editor를 컴파일했다. 오류 0. |
| EditMode·PlayMode 실제 실행 | NOT_RUN | 명중 보상 검사를 설정값·상한 기준으로 바꾸고, 저장된 설정의 퍼짐 각도·스태미나 값을 확인하는 검사를 추가했다. |
| 3인 실행: 생성 흐름 | NOT_RUN | 북을 든 사람이 스태미나를 기다리는지, 다들 늘 가득 차 북만 기다리는지, 명중 보상이 상한에 막혀 버려지는지 확인해야 한다. |



## 현재 UI에 맞춘 조합 영역 — 2026-10-15, 이슈 #155

기준은 Unity **6000.6.5f1**, main `d3aadebc`에서 분기한 `feat/#155-orb-interaction-area`다. 현재 종이판의 투명한 곡선 모서리와 하단 HUD를 피해 Canvas의 `FigmaBattleUi/OrbInteractionArea`를 실제 조작 영역의 기준으로 추가했다. 기본 anchor는 x 0~1, y 0.0881007~0.40이고, 런타임에는 연결한 `BottomHudBackground`의 위쪽에 Canvas 4단위 여유를 더한다. 이 사각형을 카메라의 XY 평면으로 투영하고 구슬 반지름+0.05를 안쪽 여백으로 적용한다. 카메라 viewport·배율, 구슬 반지름·좌우 폭과 Host의 네트워크 형식은 변경하지 않았다.

화면 크기/가이드 Rect 변경에는 `TryResizeBounds`로 같은 ID의 정규화 위치·속도 및 전달/조합 대기를 유지한다. 큰 북만 새 상하 여유 안으로 보정하고 드래그 중에는 갱신을 보류한다. 새 입력 시작은 이 사각형 안에서만 허용하며 잡고 있는 드래그의 영역 밖 손떼기·투척·전달은 이어진다. DEV 후보 크기는 실제 사각형에 구슬과 북이 들어가는지 검사한다.

| 항목 | 상태 | 이번 변경의 실행 결과·근거 |
| --- | --- | --- |
| Unity Editor 컴파일 | PASS | 2026-10-15 열린 Editor 6000.6.5f1에서 Runtime·Editor·EditMode·PlayMode 어셈블리를 실제 컴파일했다. 새 메뉴 실행과 Test Runner 실행에 성공했고 `Logs/Editor.log`의 `Tundra build success` 및 C# 오류 부재를 확인했다. |
| Scene 저장·참조 | PASS | Unity의 `C6 Lab/Fit Orb Interaction Area`로 저장하고 `Validate Orb Interaction Area`로 검사했다. 기존 Scene에는 기준 RectTransform 1개·Canvas 자식 연결·controller 참조 3개만 추가했다. 기존 HUD 위치 값은 바꾸지 않았다. |
| EditMode 실제 실행 | PASS | `Logs/Issue155-EditMode.xml`: total=93, passed=93, failed=0, skipped=0. 390×844·430×932·768×1024의 투영 검사, HUD 제외 뒤 가로 폭·카메라 배율 유지, 큰 북/무효 영역 및 DEV 후보 높이 검사를 포함한다. 이는 기기 화면 검증이 아니다. |
| PlayMode 실제 실행 | PASS | 최종 `Logs/Issue155-PlayMode.xml`: total=74, passed=74, failed=0, skipped=0. 저장 Scene의 하단 가림 방지·가이드 변경 반영, 리사이즈 시 위치/속도·ID·반지름·잠금/전달/조합 대기 보존, 드래그 보류, 새 상하 벽, 큰 북 여유와 기존 생성·조합·전달·투척 검사를 포함한다. |
| 실제 다중 실행본·iOS | NOT_RUN | 서로 다른 실제 화면비의 양방향 전달, 북/구슬의 터치와 iPhone/iPad의 화면·안전 영역 체감은 별도 확인이 필요하다. 이번 변경에서 Mac/iOS 앱 빌드·서명·설치·실기기 검사는 수행하지 않았다. |

첫 PlayMode 실행은 72/74였다. 새 저장 Scene 검사가 끝난 뒤 남긴 상하 벽 때문에 기존 `TopBoundaryReflectsInsteadOfTransferring`와 `ABurstOfOrbsFromOneDrumFliesApartWithoutJammingOrShovingIt`가 실패했다. 새 검사의 UnityTearDown에서 Lab Scene을 내리도록 고친 뒤 전체 74개를 다시 실행해 통과했다. 첫 XML은 `Logs/Issue155-PlayMode-first.xml`에 보존한다. XML·원시 로그는 로컬 증거이며 Git에는 포함하지 않는다.


## 종이판 곡선과 실제 조합 영역 일치 — 2026-10-15, 이슈 #158

Unity **6000.6.5f1**, main `15697552`에서 분기한 `feat/#158-curved-orb-area` 기준이다. #155의 보수적인 내부 사각형을 현재 동작의 기준으로 사용하지 않는다. `BoardPlate.png`의 alpha >= 0.5인 연속 외곽을 Editor에서 상하 각 65점으로 추출해 `OrbBoardPlate/LabOrbAreaProfile`에 저장했다. Runtime에서 픽셀을 읽지 않으며 기존 텍스처 Import·머티리얼·패키지는 바꾸지 않았다. 같은 source로 재적용할 때 수동 조정한 제어점을 보존한다.

Canvas `OrbInteractionArea`는 화면 아래 약 8.8%의 HUD 위부터 구슬 카메라의 위쪽 46%까지 판을 배치하는 상자다. 종이판의 세로 위치·높이를 맞춰 아래 곡선을 HUD 위로 올렸고, 카메라 viewport·배율·기존 HUD 위치·구슬 반지름은 유지했다. 실제 판정은 상자의 직선이 아닌 동일한 상하 곡선으로 수행한다. `LabOrbBoard`는 상하에만 정적 3D MeshCollider를 만들며 좌우 전달 통로는 열어 둔다. 반지름에 따른 법선 여유를 반영해 생성·드래그·수신·북 회피와 크기 변경에 사용한다. 곡선 밖 새 터치는 받지 않지만 이미 잡은 구슬의 영역 밖 손떼기·투척·전달은 이어진다.

곡선에서 높이 0~1은 해당 X와 몸체 반지름의 유효 수직 구간을 기준으로 한다. `TryGetMotion`, 전달 높이, 수신 배치와 리사이즈가 같은 계산을 사용한다. ID·소유권·잠금·전달/조합 대기와 보드 폭 기준 속도는 유지한다. 기존 사각형은 저장 Lab Scene에서 사용하지 않고 작은 테스트 fixture 호환 경로에만 남는다. 매 프레임 같은 곡선을 다시 만들지 않도록 Profile에서 검증된 형상을 캐시한다.

| 항목 | 상태 | 이번 변경의 실제 결과·근거 |
| --- | --- | --- |
| Editor 컴파일 | PASS | 열린 6000.6.5f1 Editor에서 코드와 검사 어셈블리 컴파일. `Logs/Editor.log`의 Tundra build success 및 C# 오류 부재 확인. |
| Scene 저장·참조 | PASS | `C6 Lab/Fit Curved Orb Area`와 `Validate Curved Orb Area` 실행. 실제 Scene에 Profile 1개·각 65점·Controller 참조 저장. `C6_LAB_CURVED_AREA_SAVED`·`C6_LAB_CURVED_AREA_VALID` 기록. |
| EditMode 실행 | PASS | `Logs/Issue158-EditMode.xml`: total=101, passed=101, failed=0, skipped=0. 원반/곡선의 법선 여유, 끝 통로와 크기 거부, 국소 정규화 왕복, 사각형 호환 등 포함. |
| PlayMode 실행 | PASS | `Logs/Issue158-PlayMode.xml`: total=81, passed=81, failed=0, skipped=0. 실제 3D 구체의 상하 기울기 반발, 양끝 전달 높이·속도와 수신, 영역 밖 손떼기, 큰 북·생성, 리사이즈·잠금 상태 및 기존 조합·투척 검사 포함. |
| 실제 저장 Scene과 그림 일치 | PASS | Scene 검사에서 65점의 실제 Quad Transform 좌표와 physics profile 일치, HUD 위의 아래 곡선, 프로필 누락 거부·캐시·제어점 변경 반영을 검사. Editor Game View 390×844 정지 화면에서 위·아래 곡선이 보이는 것도 관찰했다. 이는 실기기 플레이 검증이 아니다. |
| 다중 실행본·iOS·실기기 | NOT_RUN | 실제 Host/Client의 화면비가 다른 기기 간 왕복 전달, 터치 체감·화면·안전 영역은 별도 검증 필요. 이번 변경에서는 앱 빌드·서명·설치·실기기 실행을 하지 않았다. |

XML과 원시 로그는 로컬 증거이며 Git에 포함하지 않는다. #155의 과거 실행 결과를 새 곡선 동작의 통과 증거로 사용하지 않았다.


## #154 조합대 UI 배치 복원 — 2026-10-15, 이슈 #160

기준 UI는 PR #154 `[Add] 하단바 UI 축소`의 merge `6f8392ef`, 수정 브랜치는 main `060f29ef`에서 분기한 `fix/#160-preserve-board-layout`이다. #158은 원본 종이판의 전체 외곽을 HUD 위로 보이게 하려고 종이판을 위로 옮기고 세로로 줄였다. 이는 사용자 기준 배치를 바꾼 회귀였다. 이번에는 종이판의 저장 Transform을 #154와 동일한 local position `(0,0,.55)`·scale `(10,5,1)`로 복원하고, 실행 시에도 #154의 하단 카메라 전체 크기에 맞추는 방식을 유지한다. 하단 HUD·그림·색·에셋의 배치는 변경하지 않는다.

실제 보이는 아래 경계는 `max(종이판 하단, BottomHudBackground의 불투명 상단)`이다. HUD PNG의 alpha >= .5 상단 각 65점을 저장하고 실제 Canvas Rect의 X 초과 폭·Y offset을 그대로 투영한다. 두 곡선의 꼭짓점과 선분 교차점을 모두 포함해 합성하며 사각형 HUD top이나 일괄 여백으로 대체하지 않는다. 실제 종이 Quad의 좌표에서 원본 상하 곡선을 계산하므로 `Camera.pixelRect`의 소수점 높이를 되투영해 그림과 물리가 어긋나지 않는다. 구슬·북 반지름, 열린 좌우 전달·높이 정규화·요청 대기 보존은 #158 경로를 유지한다.

| 항목 | 상태 | 이번 수정의 근거 |
| --- | --- | --- |
| #154 UI 파일 대조 | PASS | Unity 저장 뒤 기존 `FigmaBattleUi`의 GameObject·RectTransform·UI 컴포넌트 102개를 #154와 대조해 변경 0개 확인(추가한 비시각 가이드 자식은 비교에서 구분). 종이판 Transform·Renderer·Mesh는 #154와 같고 종이/하단 HUD PNG·머티리얼 차이도 없다. |
| Editor 컴파일·Scene 연결 | PASS | Unity Hub에서 정상 Lab 창을 다시 연 뒤 컴파일. `Fit Curved Orb Area`·`Validate Curved Orb Area` 실행과 저장/참조 성공 marker 확인. 실행 로그는 `Logs/Editor.log`. |
| EditMode | PASS | 최종 `Logs/Issue160-EditMode.xml`: total=109, passed=109, failed=0, skipped=0. HUD offset·곡선 교차 합성·제어점과 캐시·크기 거부 및 기존 검사 포함. |
| PlayMode | PASS | 최종 `Logs/Issue160-PlayMode.xml`: total=81, passed=81, failed=0, skipped=0. #154 런타임 배치, 실제 HUD 곡선 합성, HUD 변경 시 판 배치 보존, 필수 참조 거부 및 기존 물리·전달·투척 검사 포함. |
| 사용자 변경 보존 | PASS | 수정 전부터 남아 있던 Target2 머티리얼 2개를 별도 보존본과 바이트 단위 비교해 유지했다. 기존 미추적 `Assets/_Project/HapioMVP/`도 포함하거나 삭제하지 않는다. |
| 실제 다중 앱·iOS·실기기 | NOT_RUN | 이번 수정에서 빌드·서명·설치·실기기 터치나 네트워크 왕복은 수행하지 않았다. |

첫 PlayMode는 79/81이었다. 신규 배치 검사 2건에서 논리 viewport 높이 388.24와 실제 렌더 픽셀 높이 388의 차이가 드러났다. 종이의 물리 원본 좌표를 실제 Quad와 동일한 #154 크기로 계산하고, 검사도 실제 렌더 픽셀을 비교하도록 수정했다. 첫 XML은 로컬 `Logs/Issue160-PlayMode-first.xml`에 보존한다. 라이선스 연결 대기 중에는 임시 출력 경로로 C# 컴파일만 먼저 확인했으며, 이후 Unity Hub에서 정상 재실행했다. 원시 로그·XML·임시 컴파일 산출물은 Git에 포함하지 않는다.


## 기본 Lab의 박 외형·크랙·마무리 연결 — 2026-10-15, 이슈 #163

main `ab283ef3`에서 분기한 `fix/#163-gourd-main-integration`, Unity **6000.6.5f1** 기준이다. #152의 코드와 실험 Scene은 main에 남아 있었지만 기본 `Lab.unity`의 Target에는 크랙/마무리 컴포넌트가 없었다. 기본 BuildSettings와 Mac/iOS 빌드 도구도 `Lab.unity`를 선택하므로 별도 실험 Scene의 박이 기본 플레이에 나타나지 않는 상태였다.

이번에는 현행 기본 Scene의 기존 `Pinata Target`에 `LabGourdCrack`·`LabGourdFinish`와 기존 `Target2_Intact`·`GourdCrack.mat` 참조만 추가했다. #154·#160의 UI·종이판·곡선 영역을 실험 Scene으로 교체하지 않았다. Play 시작 시 박을 장착해 원통 렌더링을 숨기고, 원통 MeshCollider와 기존 매달림·회전·Host 확정 피해 경로는 유지한다. 박과 원통의 판정 모양 차이는 그대로다.

장착 인스턴스를 `MountedVisual`·`VisualInstance`로 명시해 첫 번째 자식 순서에 의존하지 않는다. 외형 초기화 실패·컴포넌트 비활성 시 승리 UI는 지연하지 않으며, 잘못된 껍질은 원통 표시로 복귀한다. 준비된 외형은 기존 HP 단계·1.2초 마무리·라운드 키 1회 재생을 유지한다.

| 항목 | 상태 | 실제 결과·근거 |
| --- | --- | --- |
| Editor 컴파일 | PASS | 열린 Unity 6000.6.5f1에서 코드와 검사 어셈블리 컴파일. `Logs/Editor.log`의 Tundra build success와 C# 오류 부재 확인. |
| 기본 Scene 연결·저장 | PASS | `C6 Lab/Apply Gourd To Main Scene` 및 `Validate Main Gourd References` 실행. `C6_LAB_MAIN_GOURD_SAVED`·`C6_LAB_MAIN_GOURD_VALID` marker 확인. |
| 현행 UI·물리 저장값 보존 | PASS | 수정 전 기본 Scene 2,625개 직렬화 레코드와 대조. 비의미적 빈 이름 공백을 제외하면 기존 Target의 컴포넌트 목록 1개만 변경되고 표현 컴포넌트 2개만 추가됨. 기존 레코드 삭제 0개. UI·곡선 Profile·Collider·매달림 저장값 변경 없음. |
| EditMode | PASS | `Logs/Issue163-EditMode.xml`: total=109, passed=109, failed=0, skipped=0. |
| PlayMode | PASS | `Logs/Issue163-PlayMode.xml`: total=83, passed=83, failed=0, skipped=0. 기존 전달·곡선 물리·회전·투척 검사와 기본 Lab 통합 검사 포함. |
| 기본 Lab 실제 명중→결과 | PASS | `MainSceneHostProjectilesDriveCracksFinishAndResultRestart`: 저장된 기본 Lab의 Solo Host·보드 제스처·실제 SphereCollider/원통 충돌을 사용. HP 60→40→20→0, 크랙 1→2→3, 명중 보상 이벤트 3회, 연출 중 결과 숨김·완료 후 VICTORY, 결과 Restart 버튼으로 새 판·HP/스태미나/외형 초기화, Stop 후 로비 복귀 확인. |
| 외형·실패 경계 | PASS | 실제 박 Shell 렌더러 표시·원통 Renderer 숨김/Collider 유지·박 물리 제거·Animator 비활성 검사. 누락 프리팹과 비활성 연출의 승리 지연 없음, 반복 Victory의 중복 연출 없음, 패배 무지연은 추가/기존 검사로 확인. |
| 렌더 관찰 | PASS | `Library/Issue163Captures/main-playing.png`·`main-cracks-stage-2.png`·`main-finish.png`에서 기본 Lab BattleCamera의 박·크랙·갈라짐을 직접 확인했다. BattleCamera 단독 렌더이므로 전체 Canvas나 실기기 화면 검증으로 취급하지 않는다. |
| 사용자 변경 보존 | PASS | 기존 Target2 머티리얼 2개를 작업 전 보존본과 바이트 비교해 유지. 미추적 `Assets/_Project/HapioMVP/`도 유지. |
| 다중 실행본·Mac/iOS 빌드·실기기 | NOT_RUN | 이번에는 네트워크 왕복·앱 빌드·서명·설치·실기기 성능을 검사하지 않았다. 기존 빌드/기기 PASS를 승계하지 않는다. |

통합 검사는 **명시적 DEV fixture**를 사용한다. HP 60·피해 20·Relay 100%·잠금 0·일부 생성/투척 값 고정으로 3번의 실제 충돌을 재현했다. 각 발 전에는 기존 매달림을 정지 상태로 되돌렸으며, 연속 회전/흔들림 자체는 기존 별도 검사에서 확인한다. 저장된 `LabConfig.asset`과 일반 판의 확률/수치에는 fixture를 반영하지 않았다. 이는 손 입력·무작위 생성 확률·다중 기기 실험을 대신하지 않는다.

XML·원시 로그·렌더 캡처는 로컬 증거이며 Git에 포함하지 않는다. 임시 Editor 검사 실행기는 결과 저장 후 제거한다. #150 당시 실험 Scene 검증 기록은 당시 범위로 보존한다.


### 검사 후 일반 Play 재진입 관찰

전체 자동 검사와 임시 실행기 제거 후 첫 일반 Play에서는 DEV 버튼이 보이지 않았고 종료 시 `Unity.Netcode.NetworkSceneManager.Dispose()` → `NetworkManager.OnDestroy()`의 `NullReferenceException` 2건을 관찰했다. 해당 일반 Play/종료 시도는 **FAIL**로 구분한다. 오류 스택에는 박 크랙·마무리 코드가 없었지만, 테스트 잔류 상태가 원인인지는 확정하지 못했다. 기존 Editor 설정은 Domain·Scene Reload 모두 비활성이며 이 설정을 변경하지 않았다.

Scene 파일을 저장하거나 덮어쓰지 않고 **File → Open Scene으로 저장된 `Lab.unity`를 다시 연 후**, 일반 Play → 화면의 DEV → START SOLO로 기본값 전투를 실행했다. 현재 HUD·#154/#160 조합판과 매달린 박, HP 1000/1000·시간 03:00·북 표시를 Game View 390×844에서 확인했다. 해당 재실행·종료는 **PASS**이며 후속 로그에 같은 예외가 추가되지 않았다. 반복 재현과 정확한 원인 확정은 **NOT_RUN**이다. 네트워크 코드·서드파티·Editor 설정은 이 관찰을 이유로 변경하지 않았다.


## 흰 박·오행 색칠의 기본 전투 통합 — 2026-10-15, 이슈 #166

기준 main `90a8a782`, 작업 브랜치 `feat/#166-gourd-paint-integration`, Unity **6000.6.5f1**. 원본 #153 `experiment/#153-gourd-paint`의 `5d133197`에서 흰 박·재질·셰이더의 GUID와 색칠 규칙을 이관했다. 사용자가 **흰 박·색칠만 적용하고 크랙·마무리를 끄는 방향**을 승인했다. 과거 #163 기록은 당시 버전의 검증이며 현재 기본 전투의 크랙 활성 상태를 뜻하지 않는다.

기본 `Lab.unity`의 기존 Target에 페인트 참조를 저장하고 크랙·마무리 컴포넌트는 참조를 보존한 채 비활성화했다. 실행 순서에 의존하는 전역 static 스위치/자동 컴포넌트 추가와 별도 페인트 실험 Scene·BuildSettings 변경은 이관하지 않았다. 기존 원통 Collider·매달림·회전·HP·보상·승패·피격 효과·구슬 조작은 유지했다. Host `ConfirmHit` 성공 후와 Client 기존 승인 수신 게이트 통과 후의 이벤트 발행 2줄만 네트워크에 추가했으며 패킷 형식·전송 방식·송신자 검증을 변경하지 않았다.

| 항목 | 상태 | 이번 작업의 실제 결과·근거 |
| --- | --- | --- |
| Editor 컴파일 | PASS | 열린 6000.6.5f1 Editor에서 코드/검사 어셈블리 컴파일. `Logs/Editor.log`의 Tundra build success 및 C# 오류 부재 확인. 임시 실행기 제거 후에도 컴파일 확인. |
| Scene 저장·참조 | PASS | `C6 Lab/Apply White Gourd Paint`·`Validate White Gourd Paint` 실행, `C6_LAB_WHITE_GOURD_PAINT_SAVED`·`C6_LAB_WHITE_GOURD_PAINT_VALID` marker 확인. |
| 현재 Scene 구성 보존 | PASS | 작업 전 2,631개 직렬화 레코드와 공백 정규화 비교: 기존 Target 컴포넌트 목록과 Crack/Finish의 enabled 2개만 변경, Paint 컴포넌트 1개 추가, 기존 레코드 삭제 0개. UI·곡선·플레이어 표시·Collider/매달림 저장값 유지. |
| EditMode | PASS | `Logs/Issue166-EditMode.xml`: total=111, passed=111, failed=0, skipped=0. 기존 승인·중복/이전 판 타격 게이트 등 포함. |
| PlayMode | PASS | `Logs/Issue166-PlayMode.xml`: total=88, passed=88, failed=0, skipped=0. 신규 페인트 UnityTest 5개와 기존 물리·전달·회전·투척 검사 포함. |
| 기본 Lab 실제 속성 명중·승리 | PASS | `SavedSoloHostElementHitsPaintAndFinishImmediatelyThenRetryAndDevReset`: 실제 Host 생성·상자 더블 탭·속성 Combined 투척·구/원통 충돌로 HP 40→20→0과 PaintCount 0→1→2 확인. 마지막 명중의 Victory 결과가 크랙/마무리 지연 없이 표시되고 Retry·DEV 재시작에서 자국/상태 초기화. |
| 실제 무속성 명중 | PASS | 같은 통합 검사에서 Relay 구슬의 실제 Host 충돌은 HP를 줄이고 페인트를 추가하지 않는 것을 확인. 속성이 없는 공격의 기존 피해 규칙 유지. |
| 색·상한·좌표·초기화 경계 | PASS | 기본 Scene의 직접 승인 이벤트/명시적 API fixture로 5색 MPB 데이터·None/잘못된 속성 무시·NaN/무한/영벡터 거부·128개 상한·129번째 기존 자국 보존·공통 회전 전후 동일 표면 좌표 검사. 새 판 시작 직후 Update 이전의 첫 자국 유지, Stop/새 방 로비 초기화, 비활성/재활성·누락 참조 거부 포함. 직접 이벤트 fixture는 실제 네트워크/충돌 검증과 구분한다. |
| 기존 크랙 실험 보존 | PASS | 기존 실제 3회 명중·크랙·1.2초 마무리·결과/Restart 검사를 `LabGourdCrack.unity` 실험 Scene으로 옮겨 유지했다. Ignore/전역 페인트 우회 없이 전체 PlayMode 실행에 포함. 실험 Scene 파일은 변경하지 않았다. |
| 렌더 관찰 | PASS | `Library/Issue166Captures/approved-element-hit.png`·`victory-two-marks.png`에서 실제 명중 자국과 덧칠 렌더를 직접 확인했다. BattleCamera 단독 캡처이며 전체 Canvas/실기기 검증은 아니다. 5색은 MPB 데이터 검사로 확인했고 `five-colors.png`는 로비 반대편 자국 캡처이므로 5색 시각 확인 증거로 사용하지 않는다. |
| 일반 Play 화면 | PASS | 검사 후 저장 Lab을 다시 열고 일반 Play → DEV → START SOLO로 기본값 전투 실행. Game View 390×844에서 흰 박·현재 HUD·조합대·북·본인 P1·HP1000/1000·03:00 표시를 관찰 후 종료. 손으로 색칠 투척을 반복한 검증은 아니다. |
| 사용자 변경·설정 보존 | PASS | 기존 Target2 머티리얼 2개는 작업 전 보존본과 바이트 일치. 기존 미추적 폴더 유지. Config·패키지·ProjectSettings·빌드 Scene 목록 변경 없음. |
| 실제 다중 실행본·빌드·iOS·실기기 | NOT_RUN | Host/Client 양 기기에서의 색 자국 일치와 핫스팟 왕복, Mac/iOS 앱 빌드·서명·설치, 128자국에서의 iOS 성능은 실행하지 않았다. 기존 브랜치/기기의 PASS는 승계하지 않는다. |

실제 충돌 통합 검사는 **DEV fixture**(HP40/피해20, SecretBox100%·잠금0, 생성 속도0, 고정 투척 설정)로 속성 구슬 2개를 얻어 짧게 실행했다. 각 발 전 매달림을 정지시키며 이어지는 회전·흔들림은 별도 기존 검사에서 확인했다. 무속성 경로는 실행 중 복사본을 Relay100%로 전환해 재시작 후 확인했다. 원본 `LabConfig.asset`과 일반 생성 확률·게임 수치를 저장하지 않았다.

원시 로그·XML·렌더 캡처는 로컬 증거이며 Git에 포함하지 않는다. 임시 `Issue166ValidationRunner`는 결과 저장 후 제거했다. 일반 Play 전 저장 Scene을 재열어 #163 당시 관찰한 검사 후 Editor 상태와 구분했다. 이번 실행 로그에는 동일한 NGO 종료 NullReferenceException은 관찰되지 않았고, Play 진입 시 FMOD 오디오 재초기화 경고는 관찰했다. 음향 재생 검증은 이번 범위에 포함하지 않는다.


## #166 후속 — PR #168의 박 모드·런타임 연결 통합 — 2026-10-15

기준 main `90a8a782`, 브랜치 `feat/#166-gourd-paint-integration`, 원본 PR #168 head `85455acd`, Unity **6000.6.5f1**. 위 #166의 Scene 직렬화 방식은 이 후속 구현으로 대체하며 당시 기록과 실행 결과는 보존한다. 사용자가 기본 Paint와 선택 가능한 Crack, 기본 Scene 변경 없는 런타임 연결 계획을 승인했다. 원본 PR의 코드/검사 존재나 이전 111/111·88/88은 새 버전의 PASS로 승계하지 않았다.

`LabConfig.GourdMode`를 Inspector와 DEV **80번째 항목(인덱스 79, 0 Paint / 1 Crack)**에 추가했다. 이전 79개 인덱스는 유지한다. `LabSceneController.Awake`가 `LabGourdPaint.Attach(network)`로 Resources의 기존 GUID 에셋을 연결하고, 이후 `ConfigApplied`에서 모드를 적용한다. DEV Solo는 기존 런타임 복사본을 직접 설치하므로 `BeginDeveloperSoloRound`에서 같은 설정 알림을 발행한다. 공유 방은 기존 설정 전파·fingerprint·ACK 경로를 사용한다. 프레임별 Config 감시와 테스트 전용 static 스위치는 사용하지 않는다. Inspector/DEV 입력칸 편집만으로 진행 중 외형을 즉시 전환하지 않는다.

기존 연출 취소와 소유 외형 해제 후 다음 모드를 초기화하며, 같은 모드의 반복 적용은 외형·자국을 다시 만들지 않는다. Crack의 `Initialize`·재활성화는 중복 장착을 차단한다. Paint에서는 크랙/마무리를 끄고 기존 HP 종료 즉시 결과를 표시하며, Crack에서는 기존 HP 단계와 승리 후 1.2초 마무리를 유지한다. Retry는 모드를 유지하고 새 판 상태/자국을 초기화한다. 초기화 실패는 원통 표시로 복귀하고 승리 UI를 무한 지연하지 않는다. #166의 첫 명중 보존·좌표·MPB 보존·128자국 상한·무속성 무색칠도 유지한다.

| 항목 | 상태 | 이번 통합 버전의 실제 근거 |
| --- | --- | --- |
| Editor 컴파일 | PASS | 열린 6000.6.5f1에서 Runtime·Editor·테스트 컴파일 성공. `Logs/Editor.log`의 Tundra build success 확인. |
| 기본 Scene·설정 보존 | PASS | Unity 임시 도구로 이전 #166의 Paint 저장 컴포넌트만 제거하고 Crack/Finish의 원래 활성 상태 복원. 의미 있는 차이가 없음을 검사한 뒤 원본 공백까지 보존하여 `Lab.unity`는 main과 바이트 일치한다. 기존 크랙 실험 Scene·Config 에셋·BuildSettings·Packages/ProjectSettings도 변경 없음. |
| EditMode | PASS | `Logs/Issue166Mode-EditMode.xml`: total=118, passed=118, failed=0, skipped=0. UTC 12:20:51 실행. 모드 Capture/Apply 왕복·이전 79개 값 보존·잘못된 모드 원자적 거절·79/81개 배열 거부·fingerprint/JSON 설정 왕복 포함. |
| PlayMode | PASS | `Logs/Issue166Mode-PlayMode.xml`: total=91, passed=91, failed=0, skipped=0. UTC 12:21:09–12:21:54 실행. 아래 모드 검사와 기존 전체 곡선/구슬/전달/회전/투척 검사 포함. |
| 실제 DEV 입력·Solo 모드 전환 | PASS | `DeveloperSliderAppliesModesToFreshSoloRoundsAndDoesNotRemountUnchangedMode`: 저장된 DEV 슬라이더→입력 동기화·미적용 값의 무효·실제 START/RESTART SOLO로 Paint→Crack→Paint·일시정지 해제·세션 변경·모드별 단일 외형·같은 모드 반복 초기화 시 ID/자국 유지·재시작 자국 초기화·원본 Config 보존 확인. |
| 실제 Host 로비 설정 적용 | PASS | `DeveloperRoomCreationAndLobbyRetuningApplyTheHostGourdMode`: 저장된 DEV의 2인 CREATE ROOM→Crack, Host 로비 APPLY TO ROOM→Paint, 보드/카메라에 같은 Config 적용 및 다른 참가자/설정 확인 없이는 START 불가 확인. **단일 Host만 실행했으며 실제 Client 수신 검증이 아니다.** |
| Paint 실제 충돌·승리·Retry | PASS | `SavedSoloHostElementHitsPaintAndFinishImmediatelyThenRetryAndDevReset`: 실제 Host 생성·상자 더블 탭·속성 구슬 보드 제스처·Sphere/원통 충돌로 HP40→20→0, 자국0→1→2·즉시 Victory·Retry/DEV 초기화 확인. 무속성 Relay의 실제 충돌은 피해만 적용. |
| Crack 실제 충돌·승리·Retry | PASS | `MainSceneCrackModeHostProjectilesDriveCracksFinishAndResultRestart`: **기본 Lab에서 Crack Config 선택** 후 실제 Relay 3회 충돌로 HP60→40→20→0·단계1→2→3·마무리 중 결과 숨김·1.2초 내 완료 후 Victory·Retry에서 Crack 모드/HP/스태미나/외형 초기화 확인. Stop 후 기본 Paint로 복귀. |
| 실패·좌표·초기화 경계 | PASS | 깨진 Paint/Crack 참조의 원통 복귀·Collider 유지·승리 무지연·참조 복구 재시도, 새 판 첫 자국 유지, 5색 MPB·다른 MPB 속성 보존·공통 회전 좌표·128개 상한·None/NaN 등 기존 강화 검사 포함. 직접 승인 이벤트·Stage/Finish fixture와 실제 충돌 검사를 구분한다. |
| 렌더 관찰 | PASS | `Library/Issue166ModeCaptures/approved-element-hit.png`에서 실제 명중 색 자국, `Crack/main-finish.png`에서 실제 3회 명중 후 갈라짐/영혼 표현을 직접 확인. BattleCamera 단독 렌더이므로 전체 Canvas/실기기 검증이 아니다. 5색은 MPB 데이터로 검사했고 반대편 로비 캡처를 5색 시각 증거로 사용하지 않는다. |
| 사용자 수정 보존 | PASS | 기존 Target2 머티리얼 2개를 작업 전 보존본과 바이트 비교해 유지. 기존 미추적 폴더도 유지하고 이번 변경에 포함하지 않는다. |
| 새 프로토콜의 실제 혼용 거부 | NOT_RUN | 80개 설정을 위한 `LabProtocolVersion=5617` 및 NGO의 기존 버전 검사 연결을 코드로 확인했다. 구버전/새 버전 앱을 동시에 띄워 거부 메시지를 관찰하지는 않았다. |
| 실제 다중 실행본·공동 재시작·핫스팟·빌드·iOS | NOT_RUN | Host→Client 모드/자국 일치, 참가자 ACK 후 공동 재시작, 실제 접속/전달, Mac/iOS 앱 빌드·서명·설치·128자국 iOS 성능은 이번에 실행하지 않았다. 모든 기기에 같은 새 버전이 필요하다. |

실제 충돌은 앞선 #166과 같은 **명시적 DEV fixture**(Paint는 HP40/피해20·SecretBox100%·잠금0, Crack은 HP60/피해20·Relay100%·잠금0·고정 투척값)로 재현했다. 각 발 전 매달림을 정지시키며 이어지는 회전 자체는 별도 기존 검사로 확인한다. 일반 Config 에셋·확률·게임 수치를 저장하지 않았다. Host 로비 검사와 fingerprint JSON 왕복만으로 실제 Client 동기화 성공을 주장하지 않는다.

원시 XML·로그·렌더 캡처는 로컬 증거이며 공개 저장소에 포함하지 않는다. 임시 `Issue166ModeValidationRunner`는 실행 후 제거한다. 첫 시도부터 EditMode·PlayMode 모두 통과했으며 실패/건너뛴 항목은 없다.


### 통합 검사 후 일반 실행·마무리

임시 실행기 제거 후 Editor 재컴파일 성공을 확인했다. 저장된 `Lab.unity`를 다시 열어 검사 잔류 상태와 구분한 뒤 일반 Play → DEV에서 **Gourd 묶음·모드 0 입력칸·슬라이더**가 보이는 것을 확인했다. START SOLO로 기본 Paint 전투를 시작해 Game View 390×844의 흰 박·HP1000/1000·03:00·현재 종이판/북/하단 HUD/본인 P1 표시를 관찰하고 Play를 종료했다. 손 입력으로 명중을 반복한 검사는 아니며, 전투 캡처·일반 UI 관찰·자동 실제 충돌 검사를 구분한다. Play 진입 시 기존 FMOD 오디오 출력 재초기화 경고(32)가 관찰됐고 음향은 검증하지 않았다.

## 폭탄 충격파·폭발 이펙트·깜빡임 경고 — 2026-10-15, 이슈 #167

기준은 Unity **6000.6.5f1**, main(`90a8a782`, #146 스태미나·#154/#158 곡선 구슬판 포함)에서 분기한 `feat/#167-bomb-shockwave`다.

- **규칙**: 잠긴 폭탄이 시간이 다 되어 터져도 보유자 판의 구슬을 지우지 않는다. Host가 폭탄만 없애고 보유자에게 폭탄의 마지막 위치를 담은 폭발 신호(`bombBlast`)를 보낸다. 같은 판의 스냅샷보다 먼저 보낸다.
- **충격파**: 보유자 화면이 폭탄의 현재 위치에서 범위(기본 판 폭 40%) 안의 구슬·북·잡고 있던 구슬을 거리에 비례해 밀어낸다(기본 9 판 폭/초, 범위 끝 0). 옆 가장자리를 넘은 것은 기존 전달 흐름으로 이웃에게 가며 잠긴 아이템은 잠금이 1 준다. Host 응답을 기다리는 구슬은 밀지 않는다.
- **연출**: 만화풍 3겹 섬광·충격파 원·연기·"BOMB!"(0.9초), 폭발 진동(Heavy), 남은 3초부터 빨간 깜빡임(초당 2→8회).
- **설정·통신**: DEV 항목 80 → 84개(충격파 범위·세기, 폭발 진동 종류·세기, 인덱스 80~83). 통신 버전은 #166의 `5617`과 겹쳐 `5618`로 올렸다.

| 항목 | 상태 | 실행 결과·남은 확인 |
| --- | --- | --- |
| C# 오프라인 컴파일 | PASS (컴파일만) | Unity 6000.6.5f1 내장 Roslyn으로 Runtime·EditMode·PlayMode·Editor를 단계마다 컴파일했다. 오류 0. |
| EditMode·PlayMode 실제 실행 | NOT_RUN (최종 기록 없음) | 아래 검사를 추가했다. 단계별 실행 결과와 최종 전체 통과 수는 기록하지 않았다. |
| DEV 솔로 화면 | NOT_RUN (부분 관찰) | 2단계 뒤 충격파를 화면으로 확인해 세기가 약하다고 판단해 3 → 9로 올렸다. 이펙트·깜빡임 화면 확인 기록은 없다. |
| 3인 실행: 밀린 구슬·북의 이웃 전달, 잠금 감소 | NOT_RUN | Mac 앱 3개에서 폭발 후 이웃 화면으로 넘어오는지, 잠긴 아이템의 숫자가 주는지 확인해야 한다. |
| iOS 실기기: 폭발 진동 | NOT_RUN | 진동은 iOS 빌드에서만 동작한다. |

추가·변경한 검사:
- **EditMode** `LabModelTests`: `UnthrownBombBlastsWhereItsHolderLastHadItWithoutRemovingAnyOrb`
- **EditMode** `LabHapticsTests`: `ABombBlastPlaysAHeavyHapticUnlessHapticsAreOffOrSilent`
- **EditMode** `LabDeveloperConfigTests`: DEV 84개·충격파·폭발 진동 기본값
- **PlayMode** `LabOrbPhysicsTests`:
  - `ABlastFlingsNearbyOrbsDrumsAndTheHeldOrbAwayWithoutRemovingAny`
  - `AnOrbAndADrumFlungPastASideEdgeGoToTheNeighbour`
  - `ABlastShowsACartoonBurstWithBombThatClearsItself`
  - `ALockedBombFlashesRedInItsLastSecondsFasterAsItRunsOut`

## #175 박 색칠 목표 — 원본 PR 기록, 2026-10-16

**아래는 보완 전 구현의 기록이며 현재 규칙·수치·검증은 이어지는 “#175 / PR #176 보완” 절을 따른다.**

기준 `origin/main` 29889344, Unity 6000.6.5f1, 작업 브랜치 `feat/#175-gourd-paint-goal`. 기본 Paint 모드는 승인된 오행 명중으로 박 전체 표면의 80%를 칠하면 승리한다. Crack 모드의 기존 HP 승리는 유지한다. 상단 GOAL 바는 현재 모드의 진행도를 표시한다.

| 항목 | 결과 | 범위 |
| --- | --- | --- |
| EditMode | PASS 123/123 | Paint 진행도 승리·초기화, Crack HP 승리와 기존 모델 검사. |
| PlayMode | PASS 95/95 | 저장된 기본 Lab의 명중·색 자국·진행도, 같은 자리 반복 칠하기 무진행, 128개 한도 전에 80% 도달 가능성, 기존 물리·UI 검사. |
| Scene·참조 | PASS (직렬화 확인) | 기본 `Lab.unity`에서 상단 레이블 `YOKAI HP`를 `GOAL`로 한 줄 변경. 기존 참조와 `.meta` 유지. |
| 다중 기기·iOS·손 조작 | NOT_RUN | Host/Client 색 자국·진행도·결과 일치, 실제 조준으로 80% 도달 가능성, 성능 확인 필요. |

진행도는 Host의 4,096개 균등 표면 표본으로 계산한다. Paint 모드의 기존 내부 HP 수치는 더 이상 승리를 결정하지 않으며 화면에서 숨긴다. 진행 중 재접속의 과거 자국 복원은 구현하지 않았다. Unity 배치 테스트에서 화면 캡처는 건너뛰었으며 별도 육안 렌더 검증은 하지 않았다. XML과 실행 로그는 로컬 `work/`에 보관하고 커밋 대상에서 제외한다.


## #175 / PR #176 보완 — 공동 색칠·DEV 목표 비율 — 2026-10-16

기준은 main `94b926b0`를 포함한 PR 원본 `119fbd39`와 `feat/#175-gourd-paint-goal`의 이 보완 변경, Unity **6000.6.5f1**, 기본 `Assets/Lab/Scenes/Lab.unity`다. 바로 위 원본 PR의123/123·95/95는 과거 결과로 보존하며 이번 결과로 승계하지 않는다. 기존4,096개 별도 표본/128개 자국 배열은 아래 단일 누적 맵으로 대체했다.

- **판정·표시**: Host 모델이256×128 동일 면적 구면 RGBA 맵을 소유한다. 실제 유효 명중의 구슬 소모·HP 실제 감소·보상·색칠·승리를 같은 `ConfirmHit`에서 확정한다. 알파가 있는 셀 수÷32,768이 진행도이고 렌더도 같은 맵을 Point 샘플링한다. 겹친 셀은 다시 세지 않으며 자국 개수 제한은 없다.
- **설정**: DEV `Gourd`의 **Paint Victory Coverage (%)**, 정수1~100, 기본80. 인덱스87에 추가해 총88개이며 기존0~86은 유지한다. 판 시작 시 Rules/스냅샷 목표와 팔레트를 확정하고 일반 Retry는 모드/목표를 유지한다. 변경된 설정/승인 피격 형식의 네트워크 버전은5621이다.
- **기존 동작**: Paint HP0 이후에도 명중 보상·입자/소리/햅틱 경로를 유지하도록 실제 HP 감소와 별도 명중값을 나눴다. Paint의 결과 `damageTotal`은 명목 공격값 점수이고 Crack은 실제 HP 감소량이다. Crack의 HP0 승리·크랙·마무리, UI 배치·곡선판·물리·폭탄/결합 효과는 유지한다. 저장 Scene은 main과 바이트 일치하고 제목은 HUD에서 모드별로 갱신한다.

| 항목 | 상태 | 새로 수행한 결과·근거 |
| --- | --- | --- |
| Editor 컴파일·최종 소스 | PASS | 실행 중인6000.6.5f1 Editor에서 Runtime/Editor/검사 컴파일. 임시 검사 도구 제거 후 최종 Mac Development 빌드도 성공. 같은 프로젝트에 중복 batchmode는 실행하지 않음. |
| EditMode | PASS | `Logs/Issue175Completion-EditMode.xml`, UTC01:23:14–01:23:15: **total173, passed173, failed0, skipped0**, result Passed. 목표1/80/100·설정88개/왕복/fingerprint/잘못된 목표 원자적 거부·라운드 목표/팔레트 캡처·중복/종료/일시정지 논리 시계 경계·맵/표시 계산 포함. |
| 첫 검사 실패·조치 | 수정 후 PASS | 첫 EditMode는172/173, 실패1. 새 팔레트 보존 검사에서 Material 색을 완전 일치로 비교해 생긴 부동소수점 오차였으며 색 벡터 거리1e-5 미만으로 검사식을 수정했다. 보존 검사의 목적과 실제 코드는 유지하고 전체 재실행173/173로 확인. 최초 XML은 로컬에 별도 보존. |
| PlayMode 전체 | PASS | `Logs/Issue175Completion-PlayMode.xml`, UTC01:23:33–01:24:22: **total96, passed96, failed0, skipped0**, result Passed. 저장 Scene 연결과 기존 구슬/곡선/전달/회전/폭탄/소리·햅틱 로직/Crack 검사를 포함. 소리·햅틱 API 검사는 실기기 청취/진동의 증거가 아님. |
| 실제 단일 Host 투사체·Paint | PASS | `SavedSoloHostElementHitsReachHpZeroWithoutWinningAndSurviveVisualReinitialization`: 실제 생성→상자 더블 탭→Combined 보드 투척→Sphere/원통 충돌2회. HP40→20→0이나80% 미달이면 Playing 유지. 2번째 충돌 중 표현 비활성→재활성 후 같은CPU맵/텍스처 복원. 연결 중 직접Paint/Clear/Notify로 상태 변경 불가 확인. |
| DEV 목표·실제 승리·Retry | PASS | `OnePercentDeveloperGoalWinsByRealHostCollisionAndResultRetryResetsTheMap`: 저장 DEV입력1%로 실제 Solo시작→실제 충돌→진행도1% 이상/HP20 남은Victory→결과 Retry로1% 유지·구슬/맵 초기화→다시 실제Victory→DEV80% 재시작. GOAL 제목·내림 숫자·Fill·결과 연결 포함. |
| 맵·중첩·좌표·경계 | PASS | EditMode `LabPaintSurfaceTests`와 PlayMode의 오프라인 preview fixture에서128회 같은 지점→129번째 덧칠은 면적 불변,130번째 새 방향은 증가.5색 RGBA→텍스처 일치·다른MPB값 보존·seam/pole·NaN/영벡터·초기화·회전좌표 확인. 이것은128번 실제 손 투척을 수행한 증거가 아님. |
| 실제 Mac2프로세스 연결 | PASS | 같은 Development 앱의 Host/Client를 실제 NGO/UTP localhost로 연결했다.2명/설정ACK 후 시작→Client실제 보드 생성/더블 탭/투척2회→Host충돌 승인. HP1→0 뒤에도2회 모두 승인/명중 이벤트, PaintCount2·coverage0.03900146484375로 Playing 유지. 양쪽 CPU맵 및 재활성 텍스처 SHA256 일치. |
| 공동 일시정지·DEV 목표 재시작 | PASS | 같은2프로세스에서 Host일시정지 동안 시계/맵 유지, `RestartBattleWithDeveloperConfig`의 목표1% 전파·ClientACK→새Playing. 한 실제 충돌 뒤 양쪽coverage0.021759033203125·PaintCount1·Victory·맵/텍스처 SHA256 일치. |
| 같은 방 로비·Crack 회귀 | PASS | 위 연결을 유지해 전원 같은 로비로 복귀하고 양쪽맵0 확인. Host로비 설정을 Crack/HP20으로 적용·ACK→시작→Client실제 충돌→양쪽HP0/Victory·기존 유한 시간 마무리 완료. 두 프로세스 모두 최종 pass=true 및 종료코드0. |
| Mac 앱 빌드 | PASS | 기존 `C6 Lab/Build Mac App`로 검사용 빌드 성공(322,451,111 bytes). 임시 Runtime/Editor도구4파일(.cs/.meta) 제거 후 같은 메뉴의 **최종 일반 빌드 성공**, `C6_LAB_BUILD_OK target=StandaloneOSX`,322,891,538 bytes. 기존 출력을 덮어쓰지 않는 새 `Builds/Lab/<run>/macOS/` 사용. 최종 일반 빌드의 별도 수동 플레이는 미실행. |
| 렌더 관찰 | PASS (범위 한정) | 새 `Library/Issue175Captures/approved-element-hit.png`에서 실제 승인 명중의 색 자국을 직접 관찰했다.5색은 CPU/텍스처 검사이며 반대편 로비 캡처를5색 육안 증거로 사용하지 않는다. BattleCamera단독 렌더로 전체 Canvas/실기기 화면 검증과 구분. |
| Scene·원본 설정·사용자 수정 | PASS | `Lab.unity`는 기준main과 바이트 일치. Config에셋·Packages·ProjectSettings·빌드 Scene목록 불변. 기존 Target2머티리얼2개는 작업 전 백업과 바이트 일치하고 기존 미추적 폴더는 그대로 보존/커밋 제외. |
| iOS·실기기·핫스팟·3~5실기기 | NOT_RUN | 이번에 Xcode출력/앱빌드·서명·설치, 휴대폰/태블릿 조작·진동/소리·장시간 성능, 실제 네트워크 핫스팟/3~5대 연결은 실행하지 않음. 모든 참가자에게 같은5621버전 필요. |
| 일반 손 투척으로80~100% 달성 | NOT_RUN | 자동 규칙/맵 검사와 낮춘 목표1% 실제 충돌 승리를 확인했으며 기본 시간/확률/수동 조준으로 전체 목표에 도달하는 난이도는 확인하지 않음. |

실제 충돌 검사는 **명시적 DEV fixture**로 수행했다. Solo는HP40/피해20·SecretBox100%·잠금0·생성속도0과 고정 투척값,2프로세스는HP1/피해20·북2개·동일 상자/투척값·30초 판·목표80→1%를 사용했다. 각 발 앞에서 매달림을 정지시키므로 연속 회전 중 조준 난이도 검사가 아니다. 일반 Config에셋을 저장하지 않았다.2프로세스 검사는 임시 입력 도구가 공개 보드/네트워크 API를 호출했고 모델 직접 명중/위조패킷/파일IPC로 결과를 만들지 않았다.

진행도는 **구면 셀의 양자화 비율**이며 화면 픽셀이나 실제 저해상도 Sphere메시 면적 측정이 아니다. 실제 피격체와 박 외형의 차이·진행 중 재접속의 과거 맵 복원 미지원은 유지한다. 원시 XML·Editor/Player로그·앱·임시JSONL/검사도구는 로컬 검증자료이며 공개Git에 포함하지 않는다. 임시 도구는 최종 프로젝트에서 제거했다.

## 박 폭탄 방해공작 — 2026-10-17, 이슈 #177

기준은 Unity **6000.6.5f1**, main(`ff17e312`, #176 색칠 목표 포함)에서 분기한 `feat/#177-gourd-bomb-drop`이다.

- **규칙**: 북은 기본으로 음·양만 낸다(50/50, 아이템 4종 0%; 아이템 규칙 코드는 유지). Host가 20~30초(DEV `Gourd Bomb Interval Min`·`Max`, 최대 0이면 끔, 최소 1초)마다 무작위 한 명의 판에 폭탄(출처 `Gourd`)을 만든다. 가득 찬 판·같은 사람 연속을 허용하고 스태미나를 쓰지 않는다. 폭탄은 **잠금이 없고 던질 수 없으며** 넘겨도 도화선(아이템 수명 15초)이 이어지고, 끝나면 그때 보유자 판에서 터진다(#167 충격파 유지). 이 때문에 `Bomb Damage Multiplier`와 폭탄 사용 기록은 지금 쓰이지 않는다.
- **통신**: Host가 박 폭탄 신호(`gourdBomb`: 받는 사람·폭탄 ID·착지 위치)를 그 폭탄이 처음 담긴 스냅샷 조각보다 먼저 모든 참가자에게 보낸다. DEV 항목 88 → 91개(인덱스 88·89 박 폭탄 간격, 90 도화선 소리 크기). 통신 버전 `5621` → `5623`.
- **연출**: 모든 화면은 전투 화면 박 옆 "!" 말풍선(`LabGourdAlert`). 받는 화면은 착지 지점 "!" 경고 0.6초 → 구슬판 위쪽에서 낙하 0.45초 → 두 번 튐 0.45초(`LabDropWarning`·`LabOrbView.PlayDrop`), 착지 전 잡기·충돌·폭발 밀림·떠돌기 차단. 폭탄은 몸통(`ItemBombBody.png`)·꼭지와 도화선(`ItemBombFuse.png`) 두 장으로 그리고, 움직인 만큼 굴러 꼭지가 공 둘레를 돌되 몸통 뒤로 숨지 않는다. 몸통에 빨간 남은 초, 도화선 끝 불꽃, 치지직 소리(`LabFuseSound`, 기본 코드 생성음), 마지막 3초 빨간 깜빡임과 소리 가속. 북처럼 혼자 떠돌되 혼자서는 이웃 화면으로 넘어가지 않으며, 누르면 누른 지점을 잡는다.
- **설정 에셋**: `LabConfig.asset` 생성 확률 50/50/0/0/0/0(1단계 커밋의 40/40/0/0/0/0 합계 80을 3단계 커밋에서 바로잡음), `LabOrbAppearance.asset`의 Bomb Art·Bomb Fuse Art 연결은 Inspector에서 저장했다.

| 항목 | 상태 | 실행 결과·남은 확인 |
| --- | --- | --- |
| C# 오프라인 컴파일 | PASS (컴파일만) | Unity 6000.6.5f1 내장 Roslyn으로 Runtime·EditMode·PlayMode·Editor를 단계마다 컴파일했다. 오류 0. |
| EditMode·PlayMode 실제 실행 | NOT_RUN (최종 기록 없음) | 중간 실행에서 `DrumPassesToANeighborButIsNeverStoredThrownExpiredOrBlownUp` 1건이 실패했다(폭탄 잠금을 가정한 기대값). 잠금 0으로 고쳤고 재실행 결과와 최종 전체 통과 수는 기록하지 않았다. |
| 폭탄 굴림 계산 | 확인 (Unity 밖) | 같은 계산식으로 좌우 굴림 장면을 이미지로 그려 꼭지가 앞쪽·테두리에만 머무는지 확인했다. Unity 화면 확인이 아니다. |
| DEV 솔로 화면 | NOT_RUN (부분 관찰) | 2단계 뒤 폭탄 그림이 회전하지 않는 점과 누를 때 꼭지가 사라지는 현상을 화면에서 보고 고쳤다. 고친 뒤의 굴림·낙하·말풍선·치지직 화면/소리 확인 기록은 없다. |
| 3인 실행: 무작위 대상, 받지 않은 화면의 말풍선, 폭탄 전달·폭발 | NOT_RUN | Mac 앱 3개에서 모든 화면에 말풍선이 뜨고 받은 사람 화면에만 경고·낙하가 나오는지, 넘긴 폭탄의 남은 초가 이어지는지 확인해야 한다. |
| iOS 실기기 | NOT_RUN | 치지직 소리 크기, 말풍선 크기(기본 150px)·위치, 낙하 느낌을 실기기에서 확인해야 한다. |

추가·변경한 검사:
- **EditMode** `LabModelTests`:
  - 추가: `GourdDropsABombOnOnePlayerEveryTwentyToThirtySeconds`, `GourdBombsReachEveryPlayerAndCanRepeatOnTheSamePlayer`, `GourdBombLandsOnAFullBoardWithoutSpendingStamina`, `GourdBombsStopWhenTheMaximumIntervalIsZeroOrTheRoundIsOver`, `DrumMakesOnlyYinAndYangByDefault`, `ABombHasNoLockItsFuseBurnsOnHoweverOftenItIsPassedAndItIsNeverThrown`
  - 변경: `GenerationFollowsConfiguredItemChancesAndIncludesEveryItem`(기본값 대신 40/40/5/5/5/5 지정), `EachHandoffToANeighborRemovesOneLockAndSoloCannotUnlock`(물약으로), `ItemsCountWhenUsedOrThrownButNotWhenRefused`(폭탄 투척 거절), `DrumPassesToANeighborButIsNeverStoredThrownExpiredOrBlownUp`(폭탄 잠금 0)
  - 삭제: `UnlockedBombIsDefusedAndLivesAFreshOrbLifetimeLikeAnAttackOrb`, `UnlockedBombHitsForTripleDamageAndAMissNeverExplodes`(폭탄 해금 규칙 제거)
- **EditMode** `LabDeveloperConfigTests`: DEV 91개·박 폭탄 간격·도화선 소리 기본값, `GourdBombIntervalIsAppendedToTheGourdCategoryAndMinCannotExceedMax`, `SavedConfigDrumMakesOnlyYinAndYangAndTheGourdDropsBombs`, `SavedAppearanceDrawsTheCartoonBomb`
- **PlayMode** `LabOrbPhysicsTests`:
  - 추가: `ABombWithArtDrawsTheCartoonBombWithItsRedSecondsAndASparkingFuse`, `AMovingBombRollsItsCapAndFuseAroundTheBallIn3D`, `AnExpectedGourdBombWarnsOnItsSpotThenFallsInAndBouncesBeforeItCanBeTouched`, `ABombDriftsOnItsOwnButOnlyAPushOrSwipePassesItToANeighbour`, `TheFuseSizzlesWhileABombBurnsAndStopsWithoutAFuse`, `TheGeneratedSizzleIsAOneSecondAudibleLoopWithinRange`, `BombCountsDownItsFuseAndAnUpwardFlickNeverThrowsIt`
  - 변경: `LockedItemMovesLikeAnyOrbAndItsBadgeFollowsTheHostCount`(물약으로), #167의 `ALockedBombFlashesRedInItsLastSecondsFasterAsItRunsOut` → `ABombFlashesRedInItsLastSecondsFasterAsItRunsOut`
- **PlayMode** `LabGourdAlertTests`(새 파일): `TheAlertPopsAnExclamationBubbleBesideTheGourdFollowsItAndLeavesByItself`
- **PlayMode** `LabDeveloperSoloIntegrationTests`: `SavedSceneSoloGourdBombWarnsThenFallsOntoTheLocalBoard`(저장 Scene에서 실제 Host 신호 → 경고·낙하·말풍선 → 착지). 구슬 수를 세는 기존 저장 Scene 검사와 `LabGourdPaintTests`·`LabGourdCrackTests` fixture는 박 폭탄을 끈다.

## 북 멈춤 구간 — 2026-10-17, 이슈 #178

기준은 Unity **6000.6.5f1**, main(`54cdc1d9`, #177 PR #179 포함)에서 분기한 `feat/#178-drum-stop-zones`다.

- **규칙**: 북을 넘길 때마다 Host가 받는 판의 도착 방식을 정해 북 상태(`arrival`·`stopAt`·`arrivedAt`)에 담는다. 스와이프는 들어온 가장자리에서 판 폭 20~40%·40~60%·60~80% 구간(40·35·15%)의 한 지점에 멈추거나 그다음 사람에게 통과(10%). 앞 판을 통과해 온 북은 가까운 쪽 구간, 폭탄 충격파로 넘어온 북은 제 속도로 미끄러지되 80% 안에서 멈춘다. 뽑기는 seed와 넘김 순번으로 정해진다. 일반 구슬의 전달은 바뀌지 않는다.
- **받는 화면**: 들어온 속도를 이어받아 남은 거리만큼만 감속해 지점에 선다(감속 상한 8 판 폭/초², 최대 1.2초). 통과면 최소 0.9 판 폭/초로 반대편까지 가며 Host에 "통과 중"으로 알린다. 폭발로 민 북은 느려져 떠돌기로 돌아가기 전까지 "폭발"로 알린다. 잡기·폭발·전달 거절이 도착을 끝낸다. 1초가 지난 전달(재접속 등)은 다시 적용하지 않는다.
- **설정·통신**: DEV 91 → 99개(인덱스 91~94 경계, 95~98 확률, 순서·합 100 검사). `transfer` 패킷에 넘김 이유(`crossing`)를 더했다. 통신 버전 `5623` → `5624`.

| 항목 | 상태 | 실행 결과·남은 확인 |
| --- | --- | --- |
| C# 오프라인 컴파일 | PASS (컴파일만) | Unity 6000.6.5f1 내장 Roslyn으로 Runtime·EditMode·PlayMode·Editor를 단계마다 컴파일했다. 오류 0. |
| 도착 감속 계산 | 확인 (Unity 밖) | 같은 식을 물리 단계 0.02초로 시뮬레이션해 들어온 속도 0.05~8 판 폭/초·목표 20~80% 모두 목표의 0.5% 안에서 1.2초 이내 정지를 확인했다. Unity 실행 결과가 아니다. |
| EditMode·PlayMode 실제 실행 | NOT_RUN (최종 기록 없음) | 중간 실행에서 #177의 기존 검사 2건이 실패했다: 폭탄 남은 초 글자색 정확 비교(TextMesh 8비트 색) → `Color32` 비교로, 폭탄 깜빡임 0.6초 고정 관찰(느린 프레임에서 놓침) → 최대 2초·조건 충족 시 종료로 고쳤다. 박 말풍선 검사도 같은 이유로 프레임 속도와 무관하게 바꿨다. 수정 뒤 전체 재실행 결과는 기록하지 않았다. 같은 실행에서 Unity의 `_AdditionalShadowParams … Restart Unity` 렌더링 경고가 반복됐다(테스트 코드와 무관, Editor 재시작으로 해소). |
| 3인 실행: 멈추는 자리의 변화·통과 체감·폭발로 넘어간 북 | NOT_RUN | 솔로에는 이웃이 없어 확인할 수 없다. Mac 앱 3개에서 같은 힘으로 여러 번 넘겨 자리가 달라지는지, 통과가 가끔(약 10%) 나오고 그다음 판 가까운 쪽에 서는지, 폭발로 넘어간 북이 받은 판 안에서 서는지 확인해야 한다. |
| iOS 실기기 | NOT_RUN | 손가락 스와이프 세기에 따른 체감 확인 필요. |

추가·변경한 검사:
- **EditMode** `LabModelTests`: `DrumHandoffDrawsAStopSpotFromTheEntryEdgeOrAPassByTheConfiguredChances`(400회 분포·구간·같은 방 같은 결과·1초 창), `APassingDrumStopsNearOnTheNextBoardABlastFlungDrumCoastsAndOrbsHaveNoArrival`
- **EditMode** `LabDeveloperConfigTests`: DEV 99개·기본값, `DrumStopZonesAreAppendedToTheDrumCategoryAndRejectBadOrderOrTotals`
- **EditMode** `LabSessionSettingsTests`: 북 도착 정보의 스냅샷 JSON 왕복
- **PlayMode** `LabOrbPhysicsTests`: `AHandedOnDrumComesToRestOnTheHostsSpotWhateverSpeedItCameInWith`, `APassingDrumRunsOnToTheNextPlayerAndSaysItIsPassing`, `GrabbingOrBlastingAnArrivingDrumHandsItBackToPhysics`, `ADrumABlastFlungInSlidesOnItsOwnSpeedButStopsInsideTheBoard`, `GrabbingABlastFlungDrumMakesItsNextCrossingAnOrdinarySwipe`; 변경 `AnOrbAndADrumFlungPastASideEdgeGoToTheNeighbour`(넘김 이유 확인), `ABombWithArtDrawsTheCartoonBombWithItsRedSecondsAndASparkingFuse`·`ABombFlashesRedInItsLastSecondsFasterAsItRunsOut`(위 실패 수정)
- **PlayMode** `LabGourdAlertTests`: 프레임 속도와 무관하게 보완


## 로비 빠른 연결 — 2026-10-17, 이슈 #185

- 기준: `origin/main`의 `5436cceb`에서 분기한 `feat/#185-lobby-quick-connect`. Unity **6000.6.5f1**, `Assets/Lab/Scenes/Lab.unity`, Mac Development 빌드.
- 범위: 앱 시작 자동 Bonjour 목록, 검색 중 방 생성·인원·DEV 사용, DIRECT IP 접기, 접속 중 행 유지·중복 요청 차단·취소, NGO 종료 완료 후 검색 복귀, 실패 사유 유지, Host 확정 이름의 Client 표시. 기존 전투 규칙·105개 설정 원본·IPv4/IPv6 경로 순서·경로별 12초/전체 60초는 변경하지 않았다. 이름을 로비 패킷에 추가해 통신 버전은 **5626 → 5627**이다. 모든 참가자는 같은 새 앱을 사용한다.
- Scene은 기존 로비 객체를 재사용해 Unity Editor 메뉴 `C6 Lab/Upgrade Quick Connect UI`로 저장했다. 직렬화 레코드 **2,631 → 2,648**, 새 레코드 17개·삭제 0개. 변경 전후 전투 UI 컴포넌트와 설정 JSON이 같다는 메뉴 검사를 통과했고, 최종 Scene의 의미 없는 공백만 정리했다. 작업 전부터 있던 사용자 머터리얼 변경·미추적 폴더는 수정·커밋 대상에서 제외한다.

| 검증 | 상태 | 실행 방법·결과·근거 |
| --- | --- | --- |
| Editor 컴파일 | PASS | 실행 중인 기존 Editor에서 컴파일. 신규 PlayMode 검사의 NGO 직접 타입 참조로 발생한 CS0012 2건은 공개 연결 게이트 검사로 바꿨다. 패키지·asmdef 변경 없음. 이후 실제 Test Runner와 두 Mac 빌드 성공. |
| EditMode 전체 | PASS | Test Runner 실제 실행 **200개 / 통과 200 / 실패 0 / 생략 0**. 최종 XML `Issue185-EditMode.xml`, 11:05:42 UTC 종료. 일반 NGO disconnect는 다음 경로 재시도, Host 명시 사유는 종료하는 7개 분류 사례 포함. |
| PlayMode 최초 실행 | FAIL → 수정 | **113개 / 통과 109 / 실패 4**. 3건은 같은 Update에서 검색 재개 후 이전 시각을 Tick에 전달한 역행 예외. `activeDiscovery.Tick()`에서 새 시각을 읽도록 수정. 1건은 Solo 종료 후 기존 접속 패널을 기대하던 검사여서 새 목록 패널 기대값으로 변경. 게임 규칙 검사를 제거하지 않았다. |
| PlayMode 최종 전체 | PASS | 11:06:16~11:07:17 UTC, **113개 / 통과 113 / 실패 0 / 생략 0**. XML `Issue185-PlayMode.xml`. 저장 Scene의 자동 목록·인원·DIRECT IP·DEV 접근, 응답 없는 로컬 UDP 접속 취소·완전 종료 게이트, 빈 주소 및 Host 포트 오류 안내를 포함한다. |
| Mac 빌드·실제 Bonjour 목록 접속 | PASS | 검증용 Development 앱(20:09 KST)을 같은 Mac에서 두 프로세스로 실행. 별도 포트의 실제 Bonjour 서비스를 검색하고 일반 목록 행의 onClick으로 선택했다. 직접 localhost Join 성공을 대체 근거로 사용하지 않았다. Host 7개·Client 9개 체크포인트 모두 통과, 양쪽 같은 확정 방 이름·설정 확인·공유 전투 시작 확인. |
| Mac 취소·실패·복귀 | PASS | 위 실행에서 목록 선택 직후 취소 → NGO 종료 후 검색 → 늦은 연결 없음, 빈 직접 주소 사유 유지 → REFRESH → 실제 방 재선택·입장, Host 일시정지·같은 방 로비 복귀 → Client LEAVE → 자동 검색을 확인했다. 최종 JSONL의 모든 체크포인트 `pass=true`. 시험 도구는 검증 후 프로젝트에서 제거했다. |
| 실제 Mac 화면 | PASS (관찰 범위) | 390×844 실행 창에서 LOCAL ROOMS·CREATE ROOM·인원 조정·방 행·REFRESH, DIRECT IP 펼침, DEV 설정 접근을 관찰했다. 전체 UI 디자인 개편이나 모든 해상도 검증은 아니다. |
| 임시 도구 제거 후 일반 Mac 빌드 | PASS | 20:18 KST 일반 Development 빌드 `C6_LAB_BUILD_OK`, 크기 318,187,003바이트. 검증용 런타임/Editor 스크립트와 meta를 제거한 상태이며, 결과는 보이는 `Builds/Lab` 아래 새 폴더에 생성했다. |
| iOS 출력·앱 서명·실기기·핫스팟 | NOT_RUN | 이번 브랜치에서 실행하지 않았다. 두 Mac 프로세스 결과를 iOS 권한·핫스팟·서로 다른 기기의 통신 성공으로 승계하지 않는다. |
| 3~5기기·불량 네트워크·버전 혼합 | NOT_RUN | 현재 정원 허용 범위는 2~5명이나 이번 네트워크 실행은 두 Mac 프로세스다. 경로 전체 60초 소진, 실기기 권한 거절, 정원 초과·전투 중의 실제 다른 앱 접속은 이번에 재현하지 않았다. |

### 결과 해석과 남은 확인

- `LabJoinRejectionTests`는 거절 사유 분류의 단위 검사이며 실제 구버전 앱 접속 검사가 아니다. Host가 제공한 정원 초과·전투 중·토큰 불일치는 구체적 사유를 표시한다. NGO가 프로토콜 불일치를 사유 없이 끊으면 일반 전송 실패와 완전히 구별할 수 없으므로, 모든 버전 불일치에 구체적 원인이 표시된다고 보장하지 않는다.
- 새로고침의 캐시 행은 최대 5초 유지하지만 새 주소 확인 전에는 클릭을 막는다. 결과 0개 안내는 검색을 끝내지 않으며, native 검색 오류는 REFRESH로 재시도한다. 정원·전투 상태를 Bonjour TXT로 실시간 광고하는 기능은 추가하지 않았다.
- Test Runner XML·Editor/Player 로그·검증용 앱·임시 실행 도구는 로컬 증거로 보존하고 공개 Git에 넣지 않는다. 문서의 PASS는 위 환경과 수행 시나리오에만 해당한다. 기존 과거 검증 기록은 그대로 보존한다.

## 박 색칠 인접 성장·100% 고정 목표 — 2026-10-17, 이슈 #187

기준은 main `a6f27ec2`에서 분기한 `feat/#187-gourd-paint-growth`, Unity **6000.6.5f1**, 기본 `Assets/Lab/Scenes/Lab.unity`다. 아래 결과는 이 변경의 최종 소스를 실행한 기록이며, #175·#185 등 과거 버전의 PASS를 승계하지 않는다. Scene 배치는 변경하지 않았고 기존 Crack·로비·북·폭탄·미니맵·음향 경로를 유지했다.

- **설정**: DEV는 105 → **111개**다. 기존 0~104번 인덱스를 보존하고 인덱스 105~110에 해상도·초기 면적·반복 공급량·전역 성장 속도·채움 확률·세로 가중치를 추가했다. **DEMO_TUNING_VALUE** 기본값은 각각 **2(256×128), 2%, 4%, 6%/초, 60%, 1.35**다. 허용 범위는 해상도 정수 0~2(64×32/128×64/256×128), 초기·반복 면적 0.1~100%, 성장 속도 0.1~100%/초, 채움 확률 0~100%, 세로 가중치 1~4다. 기존 인덱스 87 `Paint Victory Coverage (%)`는 이름·위치를 보존하고 **100~100 고정**, 읽기 전용 입력·슬라이더 비활성으로 바꿨다. 옛 에셋의 목표 80은 유효값·Capture에서 100으로 정규화하고, DEV/공유 설정의 100 외 목표는 전체 설정과 함께 거절한다. 단일 `LabConfig.asset`은 Unity에서 목표 100과 성장 기본값을 저장했으며 기존 북 멈춤 구간 등 다른 설정의 유효값을 보존했다.
- **Host 규칙**: 안 칠한 지점 명중은 초기 면적을 즉시 칠하고, 이미 칠한 지점 명중은 연결 영역에 성장 예산을 공급한다. Host 시간 진행에서 빈 인접 셀을 채우며 여러 영역이 전체 표면 기준 속도를 나눠 쓴다. 병합은 기존 셀 색과 영역별 FIFO 공급 순서를 보존한다. 한 셀을 완성한 마지막 소수 공급 기여자가 그 셀의 색·소유자를 정하고, 최종 셀 공급자가 `lastHit`를 받는다. 제한 시간 전 100%는 Victory, 정확한 deadline 동률은 Defeat다. pause는 성장과 전투 시계를 멈추고 Retry는 캡처 설정을 유지하면서 맵·예산·명중 수·난수 진행을 초기화한다.
- **통신·표시**: 프로토콜 **5627 → 5628**. Host의 권위 `LabModel.PaintSurface`만 성장 계산을 하며 Host 화면도 Snapshot과 같은 버전의 표시 캐시를 사용한다. Client는 Host의 셀 delta/전체 체크포인트를 별도 수신 맵에 원자적으로 적용하고, 세션·라운드·해상도·Revision·칠한/전체 셀 수·명중 수가 Snapshot과 맞을 때 맵과 HUD를 함께 공개한다. 명중 수만 바뀌면 빈 delta도 발행한다. 수신 중 부분 맵은 공개 표시 캐시로 복사하지 않는다. 기준 맵 누락·손상은 체크포인트로 복구한다. 이 기능은 **계속 연결된 현재 라운드의 부분 맵 복구**이며 연결이 끊긴 뒤 전투에 재접속하는 기능은 아니다.

| 항목 | 상태 | 이번 변경의 실제 근거·남은 확인 |
| --- | --- | --- |
| Unity Editor 컴파일·테스트 로드 | PASS | 실행 중인 Unity 6000.6.5f1에서 최종 Runtime·Editor·테스트 코드를 컴파일한 뒤 아래 실제 Test Runner 전체 실행을 완료했다. 앱 빌드·기기 실행 결과와 구분한다. |
| 최종 EditMode 전체 | PASS | `Logs/Issue187-EditMode.xml`, **2026-10-17 13:42:36Z~13:42:38Z**. `result=Passed`, **total=304, passed=304, failed=0, skipped=0, inconclusive=0**. 설정·Host 모델·성장 표면·전송·HUD 검사와 기존 전체 EditMode를 포함한다. |
| 최초 PlayMode 실행 | FAIL | 로컬 보존본 `PlayMode-initial.xml`, **13:36:38Z~13:37:41Z**. **total=113, passed=111, failed=2, skipped=0**. `OfflinePreviewCannotLeakIntoConnectedRoundsAndUnapprovedViewCallsAreInert`와 `SavedSoloHostElementHitsReachHpZeroWithoutWinningAndSurviveVisualReinitialization`에서 CPU 맵과 텍스처 RGBA 불일치를 확인했다. 실패 기록을 삭제하거나 최종 결과로 덮어 해석하지 않는다. |
| 실패 원인·수정 | PASS | 텍스처 업로드 캐시가 Surface/Revision만 비교해 세션·라운드 전환의 같은 Revision에서 이전 미리보기 텍스처가 남았고, 표시 맵 commit 뒤 텍스처 갱신이 다음 프레임까지 늦어졌다. 캐시 키에 세션·라운드를 포함하고 Host/Client commit 경로에서 `RefreshCommittedMap`을 호출했다. 수정 후 중간 PlayMode 113/113을 확인했으며 아래 최종 전체 검사도 다시 실행했다. |
| 후속 경계 보완 | PASS | 새로 일치한 Snapshot을 공개할 때 이전 `pendingPaintSnapshot`을 정리해 오래된 복구 대기가 남지 않게 했다. 초기 명중 영역의 즉시 병합으로 완성되는 경우에도 이번 공격자 대신 실제 최종 셀의 FIFO 공급자에게 `lastHit`를 부여하도록 바로잡았다. `AnImmediateNewSeedThatMergesUsesTheLastCellsActualSupplyOwner`를 포함한 최종 EditMode 전체가 통과했다. |
| 최종 PlayMode 전체 | PASS | `Logs/Issue187-PlayMode.xml`, **2026-10-17 13:43:06Z~13:44:11Z**. `result=Passed`, **total=113, passed=113, failed=0, skipped=0, inconclusive=0**. 저장 Scene의 물리·투척·성장·모드·UI 및 기존 PlayMode 전체를 실행했다. |
| 설정·HUD·표면 경계 | PASS | `LabDeveloperConfigTests` 57개, `LabPaintGoalHudTests` 31개, `LabPaintSurfaceTests` 27개가 최종 EditMode에 포함돼 모두 통과했다. 111개 왕복·지문·이전 105개 라벨/인덱스·6개 범위/해상도·잘못된 목표의 원자적 거절·직렬화 목표 80 정규화, 정수 셀 수의 미완료 100% 표시 방지, 인접 성장·전역 속도·병합/FIFO·소수 공급·이음새/극·동일 지점 반복 100% 도달 등을 검사한다. 이는 기본 수치로 실제 손 투척을 반복한 밸런스 검사가 아니다. |
| Host 모델·시간·최종 기여자 | PASS | 최종 `LabModelTests` 74개에 100% 공동 승리·진단 HP 0 이후 진행·지연 Tick·deadline 동률 Defeat·성장 예산의 최종 공급자·즉시 병합 시 lastHit·설정/팔레트 캡처·pause/Retry 검사가 포함돼 모두 통과했다. 모델 시간 fixture와 실제 물리 충돌은 별도 범위다. |
| 전송 단위 fixture·맵 복구 | PASS | `LabPaintReplicationTests` **46/46**. 저/중/고 해상도 전체 맵 3/11/43조각, 순서 교란·부분/완료 중복·단계별 원자성·delta·명중 수만 바뀐 빈 delta·기준 Revision 누락 후 체크포인트 복구·stale session/round/epoch/sequence·잘못된 헤더/payload/알파/해시/셀 ID·크기 제한·Snapshot 메타데이터 일치를 검사했다. 조각당 base64 최대 4,096자·수신 최대 64조각·32 KiB 패킷 한도, 최대 해상도 전체 맵 131,072바이트/base64 174,764자/43조각을 확인한다. **가짜 패킷을 직접 전달한 단위 fixture이며 실제 NGO/UTP 통신 성공 증거가 아니다.** |
| 저장 Scene의 실제 Host 충돌·성장·pause·Retry | PASS | `FixedHundredPercentGoalGrowsFromRepeatedRealHitsFreezesOnPauseAndResetsOnRetry`: 저장 DEV 폼 → 실제 솔로 시작 → 상자 더블 탭 → Combined 보드 투척 → 실제 Sphere/원통 충돌 2회로 진행했다. 첫 50% 초기 면적 뒤 반복 명중은 즉시 재색칠하지 않고 성장 예산을 남긴다. 두 번째 명중 승인 직후 pause해 0.75초 동안 맵·Revision·남은 시간·플레이 시간이 고정됨을 확인하고 재개 후 추가 명중 없이 100% Victory·GOAL/Fill·결과 표시를 확인했다. Retry 후 같은 과정을 다시 통과하고 DEV 재시작의 맵 초기화도 확인했다. |
| 진단 HP 0·표현 재초기화·Crack 회귀 | PASS | 실제 Host 충돌 2회로 HP40→20→0 뒤에도 100% 미달이면 Playing을 유지하며, 표현 비활성 중 명중과 재활성 뒤 같은 맵/텍스처 복원을 확인했다. 연결 중 직접 `Paint`/`ClearPaint`/미리보기 호출은 권위 상태를 바꾸지 않는다. 기존 Crack HP 승리·마무리·Retry와 모드 전환 검사는 최종 PlayMode 전체에서 통과했다. |
| CPU 맵·텍스처 데이터 일치 | PASS | PlayMode에서 공개 표시 캐시의 `Color32` 배열과 `PaintTexture.GetPixels32()`·크기·MPB 텍스처 연결을 직접 비교했다. 세션/라운드 전환과 commit 직후 갱신도 포함한다. 자동 카메라 캡처 파일 생성과 이 데이터 검사를 전체 Game View·실기기 화면의 육안 확인으로 해석하지 않는다. |
| Scene·설정 원본·사용자 수정 보존 | PASS | 기본 `Lab.unity`는 기준 main과 차이가 없다. `LabConfig.asset`만 Unity에서 고정 목표·성장 기본값을 저장했고 Scene UI 배치를 바꾸지 않았다. 작업 전부터 있던 `Target2_Binding.mat`·`Target2_Fracture.mat` 사용자 변경과 기존 미추적 폴더를 보존하며 이번 작업의 커밋 대상에서 제외한다. |
| Mac 검증용 Development 앱 빌드 | PASS | `Builds/Lab/20261017-224614-ff4cf144/macOS/`의 검증용 앱, **318,244,816바이트**. 실제 통신 검사를 위한 임시 Runtime/Editor 도구를 포함한 빌드이며, 도구 제거 후 최종 일반 빌드와 구분한다. |
| 실제 Mac 2프로세스·공유 설정·진단 HP 0 | PASS | **13:47:14Z~13:47:25Z**, 같은 Mac의 독립 Host/Client가 **127.0.0.1:17987**의 실제 NGO/UTP로 연결해 설정 ACK 후 시작했다. 64×32·초기2%/반복4%/성장6%/초·HP1·고정 목표100% fixture에서 Client의 실제 보드 투척 2회가 Host 충돌로 승인됐다. HP0·Playing·명중2·**42/2,048셀**인 상태에서 공동 pause 후 양쪽의 세션·Revision·셀/명중 수와 CPU 맵/재활성 텍스처 SHA-256이 같았다. Client Renderer 비활성 중에도 수신 캐시가 유지되고 재활성 뒤 같은 맵을 올렸다. 자동 단위 패킷 fixture와 구분한 실제 전송 검사이며 핫스팟 검사가 아니다. |
| 실제 Mac 공동 DEV 재시작·성장 Victory | PASS | 같은 연결의 Host가 초기50%/반복50%/성장100%/초로 DEV 재시작하고 Client 확인 후 새 세션의 빈 맵으로 시작했다. 실제 첫 충돌은 **1,024/2,048셀·Playing**, 반복 명중 뒤 시간 성장으로 **2,048/2,048셀·Victory**가 됐다. 양쪽 `sameVersion=true`, 같은 세션·Revision2050·명중2·Snapshot 메타데이터와 같은 맵/텍스처 SHA-256을 기록했다. 목표는 전 과정 100%이며 낮추지 않았다. |
| 실제 Mac 같은 방 로비·Crack 회귀 | PASS | 같은 두 참가자의 연결을 유지한 채 로비로 돌아가고, 공유 설정으로 Crack/HP20을 확인한 뒤 시작했다. Client의 실제 충돌 1회로 양쪽 HP0/Victory, 색칠0을 확인했다. 로컬 `network-host.jsonl`·`network-client.jsonl`은 각각 10개 체크포인트가 모두 `pass=true`이고 최종 `final`을 기록했다. 검사 완료 뒤 두 앱을 일반 종료했으며 두 실행 세션 모두 종료 코드 0을 확인했다. 자동 검증의 체크포인트 완료와 앱 종료를 구분해 확인했다. |
| 임시 도구 제거 후 최종 일반 Mac 앱 빌드 | PASS | 검증용 Runtime/Editor 스크립트 2개와 `.meta`를 프로젝트에서 제거하고 에셋 Refresh·재컴파일 뒤 GUI 메뉴로 새 빌드를 생성했다. `C6_LAB_BUILD_OK target=StandaloneOSX`, `Builds/Lab/20261017-225010-2b73db59/macOS/C6_Physics_Lab.app`, **318,211,875바이트**. 임시 메뉴가 사라진 것을 확인했으며 최종 앱 어셈블리에 `Issue187NetworkHarness`가 없다. 이 일반 빌드를 다시 두 앱으로 연결한 검사는 미실행이다. |
| iOS Xcode 출력·앱 빌드·서명·설치·실행 | NOT_RUN | 이번 변경에서 실행하지 않았다. 각 단계를 독립적으로 확인해야 한다. |
| 핫스팟·서로 다른 기기·3~5인·구버전 혼용·불량 네트워크 | NOT_RUN | 실제 로컬 네트워크 권한·핫스팟·기기 간 전송, 3~5명 동시 플레이·새/구버전 앱의 실제 거절은 실행하지 않았다. 실제 전송에서 조각을 의도적으로 잃거나 순서를 바꿔 체크포인트 복구시키는 검사도 이번 2프로세스 시나리오에는 포함하지 않았다. |
| 기기 성능·장시간 실행·소리·진동 | NOT_RUN | 최대 해상도·분할 전송의 실기기 프레임 시간/메모리/발열과 장시간 플레이, 실제 청취·진동은 확인하지 않았다. API·데이터 검사는 이를 대신하지 않는다. |
| 기본 수치·일반 손 투척의 100% 난이도 | NOT_RUN | 기본 초기 2%·반복 4%·성장 6%/초·180초·음양 50/50과 실제 조준으로 100%에 도달하는 시간·난이도는 별도 플레이가 필요하다. 짧은 DEV 충돌 fixture와 규칙 검사로 기본 밸런스 PASS를 주장하지 않는다. |

### Fixture와 증거의 범위

실제 충돌 PlayMode는 **명시적 DEV fixture**다. 목표는 낮추지 않고 100%를 유지했으며, 64×32 해상도·HP40/피해20·30초 판·생성 비용1·SecretBox100%·잠금0·생성/북 이동0·박 폭탄 끔과 고정 투척값으로 재현했다. HP 0 이후 계속 진행 검사는 초기 2%, 성장 승리 검사는 초기50%·반복50%·성장100%/초·채움100%로 짧게 실행했다. 각 발 전 매달림을 정지시키므로 연속 흔들림 중 수동 조준 난이도를 측정한 검사는 아니다. 일반 `LabConfig.asset`의 기본 성장값과 이 fixture를 구분한다.

실제 Mac 2프로세스도 **검증용 입력 도구와 튜닝 fixture**를 사용했다. 첫 판은 저해상도·HP1·초기2%/반복4%/성장6%/초, 둘째 판은 목표100%를 유지한 초기50%/반복50%/성장100%/초, Crack은 HP20이다. 임시 도구는 공개 보드/네트워크 경로로 생성·상자 사용·손동작 투척을 요청했고 Host의 실제 물리 충돌로 결과를 만들었다. 맵·모델 직접 주입이나 파일을 이용한 게임 진행 동기화로 전송 검사를 대신하지 않았다. localhost 두 프로세스의 맵/텍스처 데이터 일치와 일반 수동 플레이·실제 기기·핫스팟의 결과는 구분한다.

최종 전체 XML은 `Logs/Issue187-EditMode.xml`·`Logs/Issue187-PlayMode.xml`이다. 최초 실패와 중간 수정 확인 XML도 로컬 보존본으로 구분한다. `Library/Issue187Captures/`의 카메라 캡처는 자동 검사 산출물이며, 관찰 범위를 따로 기록하지 않은 시각 결과에 PASS를 부여하지 않는다. 원시 XML·Editor/Player 로그·앱·임시 실행기와 JSONL은 로컬 증거로 보존하고 공개 Git에 넣지 않는다. 두 Mac 프로세스의 JSONL은 독립적으로 수집해 비교했고 일반 종료 코드 0을 확인했다. 도구 제거 후 최종 일반 빌드는 해당 행에서 별도로 기록한다. 실행되지 않은 NOT_RUN 행을 이전 검사로 PASS로 변경하지 않는다.


## 완성 박 관람 DEV·공유 세션 통합 — #191 / 2026-10-18

기준 main `e3e3be4e`, 브랜치 `feat/#191-gourd-result-viewer-dev`, Unity **6000.6.5f1**, 저장 Scene `Assets/Lab/Scenes/Lab.unity`. #189 실험 코드를 일반 Runtime·Resources와 기존 테스트 어셈블리로 이관한 새 통합본의 실행 기록이다. #189 당시 사용법·검증은 `docs/experiments/gourd-result-viewer-189/`에 역사 자료로 보존했으며 아래 결과로 승계하지 않는다.

| 항목 | 상태 | 근거·범위 |
| --- | --- | --- |
| Unity 컴파일·설정·프리팹 연결 | PASS | 별도 작업 폴더에서 실제 Unity import/compile, `ISSUE191_PREPARE_OK fields=112 viewerDefault=OFF prefabReferences=OK`. Unity API로 Config의 bool 0과 기존 GUID의 프리팹 컴포넌트/라벨을 저장했고, 기존 111개 유효 설정값의 동일성을 검사했다. Scene·패키지·프로젝트 설정은 변경하지 않았다. |
| 전체 EditMode | PASS | `editmode.xml` total=331, passed=331, failed=0, skipped=0, inconclusive=0. 2026-10-18 **07:51:08Z–07:51:10Z**. 기본 OFF/112개 왕복/구형 111개·잘못된 bool 원자적 거절/지문·JSON·Client 적용/100% 결과 자격 검사 포함. |
| 전체 PlayMode | PASS | `playmode.xml` total=116, passed=116, failed=0, skipped=0, inconclusive=0. **07:51:43Z–07:52:51Z**. 공식 DEV 폼으로 ON 적용→실제 SecretBox 생성·보드 투척·Host 충돌→100% Victory→관람 진입·복사 픽셀 동일·격리 렌더·회전/줌/초기화/닫기·재진입·Retry 정리→OFF 재시작→두 번째 실제 충돌 Victory에서도 UI/Preview null→END SOLO 원본 OFF 복귀를 확인했다. |
| Development Host + Release Client 실제 공유 실행 | PASS | 07:55:22Z–07:55:31Z. Development Host(186,377,437바이트)와 Release Client(91,886,142바이트)를 실제 NGO/UTP localhost로 연결했다. 각 앱 JSONL의 9개 체크포인트가 모두 pass=true·final까지 도달했고 두 프로세스 정상 종료 코드 0을 확인했다. 일반 Release Client는 Debug.isDebugBuild=false이며 관람 활성화 인자를 받지 않았다. |
| 공유 ON 관람·원본 보존 | PASS | 112개 설정/지문/ACK 뒤 Client 보드의 실제 상자 사용·투척과 Host 물리 충돌로 양쪽 100% Victory를 만들었다. 양쪽 관람 버튼이 작동하고 복사 텍스처/원본 맵 SHA-256이 같으며 서로 다른 로컬 Quaternion을 유지했다. 입력 직후 원본 target·전투 카메라 pose와 도색 hash가 불변이었다. 각 450×900 실제 UI/RenderTexture PNG에서 모델과 버튼을 확인했다. 이는 한 색으로 완료하는 fixture이며 다색 완성 플레이의 시각 검사는 아니다. |
| 공유 OFF 전파·같은 방 재시작 | PASS | 관람 중 Host의 ReturnAllToLobby로 두 앱의 Ui/Preview가 null, 소유 Texture/RenderTexture가 파괴된 것을 확인했다. 같은 연결의 ApplyHostLobbyConfig로 OFF를 전파하고 Client ACK 뒤 시작했다. 두 번째 실제 Client 투척/Host 충돌로 다시 2,048/2,048셀 Victory가 되어도 양쪽 CanInspect=false·Ui/Preview=null이었다. |
| 임시 도구 제거 후 일반 Mac 빌드 | PASS | 임시 Runtime/Editor 실행기 2개와 대응 .meta를 제거한 뒤 `C6Lab.Editor.LabBuild.Mac`으로 새 빌드했다. `C6_LAB_BUILD_OK`, `Builds/Lab/20261018-165600-79e6e8e5/macOS/C6_Physics_Lab.app`, **186,344,398바이트**. 최종 `C6Lab.Runtime.dll`에 관람 클래스가 있고 `Issue191NetworkHarness`가 없음을 검사했다. 이 일반 앱으로 다시 두 프로세스를 연결한 검사는 미실행이다. |
| iOS 출력·서명·설치·실기기 | NOT_RUN | 실제 핀치/손가락 인계·safe area·방향 전환·핫스팟·3~5기기·메모리/성능은 이번에 실행하지 않았다. 포인터 핸들러 회귀 검사와 실제 손가락 검증은 구분한다. |
| 기본 성장 밸런스 | NOT_RUN | 관람 재현 fixture는 저해상도 64×32·초기100%·SecretBox100%/잠금0·박 폭탄 끔이다. 기본 초기2%/반복4% 성장 난이도 검사가 아니다. |

### 적용·보존 범위

- 새 허용값은 DEV `Gourd`의 마지막 **112번째/index111 `Completed Gourd Viewer (0 off, 1 on)`**이며 원본 기본 OFF다. 이전 0~110번과 고정100% 목표를 보존했다. 설정 배열 변경에 따라 프로토콜 **5629**, 연결 토큰 **`C6PhysicsLab/9`**로 구분하므로 이전 앱과 섞어 접속하지 않는다.
- 기본 OFF와 ON의 Playing에서는 네트워크당 가벼운 관찰자만 존재한다. Paint 100% Victory와 일치한 공개 맵에서만 UI를 지연 생성하고, 버튼을 눌러야 독립 모델·카메라·RenderTexture·도색 복사본을 만든다. 회전/줌은 로컬이며 원본 물리·카메라·도색/통계는 수정하지 않는다.
- 저장된 UI는 `Assets/Lab/Resources/GourdResultViewer/ViewerUI.prefab`, 실행 진입은 `LabSceneController.Awake → LabGourdResultViewer.Attach(network)`다. 실험용 CLI·Editor SessionState·Debug 관람 제한은 제거했지만 기존 DEV 설정 권한은 유지한다.
- OFF 적용/다음 판/로비/연결 종료/컴포넌트 해제는 관람 Preview와 UI를 정리한다. 관람 중 Config를 직접 편집하는 새 경로를 추가하지 않았다.
- 데스크탑 체크아웃의 별도 #190 작업과 사용자 Scene·Material은 건드리지 않았다. 원시 XML·Editor/Player 로그·PNG·앱과 임시 실행기는 로컬 증거로만 보존하고 공개 Git에 넣지 않는다.

검증용 통신 입력 도구는 Host/Client가 각자의 JSONL에만 기록하며 파일로 게임 진행을 동기화하지 않았다. 공개 생성·아이템·보드 손동작·네트워크 경로와 실제 Host 물리 충돌을 사용했으며, 맵/승패 직접 주입을 하지 않았다. 기존 Scene/Material과 자동 생성 Performance/URP 재직렬화 항목은 이번 변경에 포함하지 않는다.


## #190 — 직구형 투구폼·Inspector/DEV 비교 (2026-10-18)

이 절은 최초 main `e3e3be4e`에서 검사한 단계다. 이후 main의 #192 관람 기능과 함께 통합한 최종 설정 119개·프로토콜 5630은 다음 후속 기록을 기준으로 한다. 아래 118개·5629 및 337/118 결과는 최초 단계의 기록으로 보존한다.

기준 main `e3e3be4e`, 브랜치 `feat/#190-straight-throw-form`, Unity **6000.6.5f1**, 저장 Scene `Assets/Lab/Scenes/Lab.unity`. 관람 실험 #189 커밋 `283dcb16`을 포함하지 않는 별도 main 기반 작업이다. 기존 로컬 Scene 공백 변경과 Target2 머티리얼 2개의 변경은 보존하고 이번 커밋에서 제외한다. 아래 결과는 이 로컬 환경에서 실행한 근거이며 iOS·실기기 결과로 승계하지 않는다.

### 반영한 동작과 기본값

- 기본 **Arc**는 기존 위치·속도 계산식을 보존한다. **Straight**는 기존 입력 판정·가로 놓기 위치·초기 속력·수평 조준을 유지하고 출발 오프셋·상승각·중력을 다르게 적용한다. 카메라·FOV·자리는 변경하지 않는다.
- 직구 **DEMO_TUNING_VALUE**: 소유자 자리 기준 오른쪽/위/타깃 쪽 오프셋 `(0.35, 0.4, 0)`, 발사각 오프셋 `3°`, 기존 상승각 비율 `0.1`, 중력 배율 `0.1`. 최종 상승각은 `-30~60°` 제한이다. 저장 원본의 기존 속력 상한 **12.5**, 중력 **30**, Power Exponent **1.2**를 보존했다. 기본 직구 중력은 **3**이다. 허용 범위 전체의 명중·가시성을 보장하는 튜닝은 아니다.
- DEV는 기존 **0~110번**을 유지하고 **111~117번**에 폼·직구 6개 값을 덧붙여 **118개**가 됐다. `Throw form`에서 ARC/STRAIGHT 토글을 바꾸면 아직 적용하지 않은 초안을 기준으로 6개 숫자/슬라이더를 비활성/활성화한다. 원본 Config는 저장하지 않으며 기존 솔로·Host 로비·일시정지 후 재시작 적용 경로를 사용한다.
- 공유 설정·지문에 7개를 포함하고 프로토콜을 **5628 → 5629**로 올렸다. Host는 투척 소유자의 자리에서 계산한 월드 위치·초기 속도를 기존 발사 패킷으로 보낸다. 발사 때 중력·수명을 캡처하고 기존 Rigidbody/SphereCollider·Host 충돌·색칠·피격 효과·잔상을 재사용한다.
- 단일 Config 에셋은 Unity의 에셋 저장 API로 새 필드만 저장했다. 기존 원본의 모든 줄과 값이 그대로이며 새 직렬화 5줄만 추가된 것을 diff로 확인했다. Scene·Prefab·패키지·카메라 설정은 수정하지 않았다.

### 실행 결과

| 검증 | 상태 | 실제 결과·근거 |
| --- | --- | --- |
| 초기 EditMode 시작 시도 | NOT_RUN | 이전 실험의 Play 상태가 남아 Test Runner가 `This cannot be used during play mode`로 시작하지 못했다. Play를 종료하고 아래 검사를 실행했으며 이 시도를 PASS로 세지 않았다. |
| Editor 컴파일 | PASS | 기존 Editor에서 소스 새로고침 후 테스트 어셈블리 실행·최종 일반 Mac 빌드 성공. 임시 네트워크 도구의 누락된 `Set` 보조 함수는 검사 빌드 전 보완했다. 기존 obsolete 경고를 오류 없음과 구분한다. |
| EditMode 전체 | PASS | `Logs/Issue190-EditMode.xml`: **total=337, passed=337, failed=0, skipped=0**, **07:50:11Z~07:50:12Z**. 기존 304개와 신규 수학 22개·설정 11개. Arc 기준 벡터·같은 초기 속력/수평 조준·2~5 자리별 좌표·입력 게이트·극값·118개 공유 왕복/지문·기존 111개 순서·잘못된 값 원자적 거절을 포함한다. |
| 최초 PlayMode | FAIL | 로컬 보존본 `PlayMode-initial.xml`: **118/117/1/0**(전체/통과/실패/건너뜀), **07:50:34Z~07:51:42Z**. 새 충돌 검사가 앞선 테스트에서 남은 NGO `DontDestroyOnLoad` 네트워크를 찾아 `target` 참조가 없어 중단됐다. 실제 저장 Scene의 Paint/network 연결로 대상을 고르고 신규 테스트의 세션 종료·파괴/Scene 해제를 추가했다. 게임 규칙을 바꾸거나 검사를 생략해 통과시키지 않았다. |
| 최종 PlayMode 전체 | PASS | `Logs/Issue190-PlayMode.xml`: **total=118, passed=118, failed=0, skipped=0**, **07:54:28Z~07:55:36Z**. 기존 113개와 신규 5개. 실제 저장 Scene 보드의 위 스와이프 → Host 실제 충돌 1회 → HP 감소/색칠 1회, 시작 구체의 중심·상하좌우 투영점 화면 내부, 표시/Collider 동일 형상, 카메라 pose/viewport/FOV 불변을 확인했다. 별도 실제 Rigidbody 비교로 Straight의 최고 상승이 Arc보다 낮고 초기 속력이 같음, 비행 중 Config 교체 후 중력/수명 유지, DEV 초안 토글·슬라이더/숫자 동기화·원본 보존·Straight→Arc 솔로 재시작도 확인했다. |
| 최초 두 앱 형상 검사 | FAIL | 첫 실제 네트워크 실행은 렌더 bounds와 물리 bounds를 LateUpdate에서 1mm 이내로 직접 비교해 중단됐다(`network-initial/host.jsonl`, `client.jsonl`). 임시 도구가 기존 Rigidbody 보간 시차를 실제 메시 오프셋과 구분하지 못한 문제였다. 생산 물리는 변경하지 않고 아래 정밀 측정으로 재검사했다. |
| 실제 Mac Host·Client | PASS | 일반 저장 Scene Development 앱 2개를 실제 transport로 `127.0.0.1`에 연결. **07:58:59Z** 양쪽 최종 기록과 프로세스 종료 코드 **0** 확인. Host 설정 **118개**·protocol **5629**의 같은 지문을 수신/확인해 2인 시작했다. Client 보드에서 실제 상자 생성·더블 탭 해금·위 스와이프를 보내 Host가 발사했다. 양쪽 초기 위치 `(0.35000035, 1.25, 6)`, 속도 약 `(0, 1.3717885, 11.167445)`, 중력 **3**, 수명 **4초**가 같고 계산 예상값과 오차 한도 이내였다. Host 승인 **1회**, 양쪽 HP **980**, 명중 수 **1**, PaintCount **1**, 칠한 셀 **40/2048**, Revision **42**, CPU 맵·표시 텍스처의 SHA-256가 모두 같았다. 실 네트워크 로그는 `host.jsonl`·`client.jsonl`, 비교 요약은 `network-final-summary.json`에 로컬 보존했다. |
| 실제 비행 형상·화면 | PASS | 위 두 앱에서 메시 로컬 중심/반지름 오차 **0**, 물리 Collider/Body 중심 오차 **0**, 렌더 보간 변위를 제외한 잔차 **0**. Host·Client의 렌더/물리 bounds 차이 약 **0.0833/0.1128**은 각각 같은 크기의 기존 Rigidbody 보간 변위로 설명됐다. 별도의 가짜 비행이나 이격된 Collider를 만들지 않았다. 실제 `client-straight-flight.png`를 열어 기존 전투 viewport의 낮은 위치에 직구 구체가 보이는 장면을 확인했다. 이는 손 터치 조작감 검사와 다르다. |
| Mac 앱 빌드 | PASS | 임시 도구 포함 검사 빌드 후 도구 `.cs/.meta`를 제거하고 기존 `C6 Lab/Build Mac App`로 최종 일반 빌드를 실행했다. `Builds/Lab/20261018-165946-f9770304/macOS/C6_Physics_Lab.app`, `C6_LAB_BUILD_OK target=StandaloneOSX`, **318,215,967 bytes**를 확인했다. 일반 빌드의 별도 수동 플레이는 미실행이다. |
| iOS export·서명·실기기·핫스팟·3~5앱 동시 연결 | NOT_RUN | 이번 변경에서는 수행하지 않았다. 실기기 손 스와이프의 느낌·기기별 발사점 가시성·네트워크 지연에서의 렌더 보간은 추가 확인이 필요하다. 2~5명 좌표 수학 검사와 Mac 로컬 2앱 결과를 이 항목의 PASS로 해석하지 않는다. |

두 앱 검사는 **Fixture**다. 전투 45초·상자 100%·생성 비용 1·해금 횟수 0·정지한 북 2개·생성 직후 속도/팝 0·박 폭탄 끔·표면 64×32로 실제 투척 입력을 반복 없이 준비했다. 기존 투척 Gain·Power Exponent·속력 상한·공통 중력은 원본 그대로 사용하고 Straight만 선택했다. 이 fixture를 기본 밸런스나 실기기 입력 증거로 기록하지 않는다.

원시 로그·XML·PNG·빌드는 로컬에만 보존하며 공개 저장소에 올리지 않는다. 새 테스트 4파일과 각각의 Unity 생성 `.meta`는 커밋하고 임시 Runtime/Editor runner는 제거했다. 기존 Scene·사용자 머티리얼 2개·미추적 과거 폴더는 작업 전 보존본과 바이트 비교해 유지했다. 사용법·수치 원본·극값 한계는 [투척 가이드](THROW_TUNING_GUIDE.md)와 [DEV 상세 명세](DEVELOPER_MODE_PARAMETER_SPEC.md)를 참조한다.


### #190 후속 — main #192 관람 설정과 통합한 최종 기준

작업 도중 `origin/main`에 #192(`#191`)가 `6b5e407c`로 추가돼 최초 #190 커밋 `a2858fa`와 기능 브랜치에서 병합했다. main은 변경하지 않았다. 두 작업이 독립적으로 DEV 끝의 인덱스 111과 프로토콜 5629를 사용한 충돌을 해결했다. **관람 111번을 유지하고 직구 112~118번을 추가한 총 119개**, 프로토콜 **5630**, 연결 토큰 `C6PhysicsLab/10`이 최종 기준이다. 기존 관람 기능·UI 프리팹·검사·기록을 모두 보존했고 관람 검사의 배열 길이만 새 전체 수에 맞췄다. 최초 단계 337/118 결과를 아래 결과로 승계하지 않고 새로 실행했다.

| 최종 통합 검증 | 상태 | 실제 근거 |
| --- | --- | --- |
| EditMode 전체 | PASS | `Logs/Issue190-Integrated-EditMode.xml`, **total=366, passed=366, failed=0, skipped=0**, **08:04:18Z~08:04:19Z**. main 관람 331개 + 직구 33개 + 구형 배열 거절 2개 추가. 관람 111번을 ON으로 둔 상태의 직구 공유 왕복·앞선 112개 값 보존과 111/112/118/120개 배열의 원자적 거절도 포함한다. |
| PlayMode 전체 | PASS | `Logs/Issue190-Integrated-PlayMode.xml`, **total=121, passed=121, failed=0, skipped=0**, **08:04:51Z~08:05:59Z**. main 관람 116개와 직구 5개. 관람 허용/차단·자원 정리·입력과 직구 실제 충돌·낮은 비행·카메라 불변·DEV 전환을 함께 통과했다. 테스트 종료 뒤 NGO 다중 Singleton 정리 경고가 남았으므로 Console 전체가 경고 없이 깨끗하다는 의미는 아니다. |
| 실제 Mac Host·Client | PASS | 최종 검사 빌드 `Builds/Lab/20261018-170634-ad244f80/`의 앱 2개, 실제 로컬 transport. **08:07:06Z**, 양쪽 **119개·관람 ON·직구 ON**, 같은 설정 지문·자리별 발사 위치/초기 속도·중력 3·수명 4초 확인. Host 승인 **1회**, 양쪽 HP **980**, hitTotal **1**, PaintCount **1**, 칠한 셀 **40/2048**, Revision **42**, CPU 맵/표시 텍스처 해시 일치. 양측 메시 중심/반지름·물리 중심·보간 제거 잔차 **0**, 실제 Client 비행 PNG도 열어 확인했다. 각각 7개 checkpoint와 종료 코드 **0**. `network-integrated/host.jsonl`, `client.jsonl`, `final-summary.json`, `client-straight-flight.png`는 로컬 보존본이며 공개 저장소에 올리지 않는다. |
| 임시 도구 없는 최종 일반 Mac 빌드 | PASS | 임시 Runtime/Editor `.cs/.meta` 제거 후 기존 메뉴로 새 출력 `Builds/Lab/20261018-170748-c6d03bd0/macOS/C6_Physics_Lab.app`, **318,251,379 bytes**, `C6_LAB_BUILD_OK target=StandaloneOSX` 확인. 일반 빌드의 별도 수동 플레이는 미실행이다. |
| iOS·실기기·핫스팟·3~5앱 동시 연결 | NOT_RUN | 최초 단계와 동일하게 미실행이다. 실제 손 스와이프의 느낌과 기기별 시작점 가시성은 별도 확인이 필요하다. |

최종 두 앱 fixture는 최초 단계의 상자/정지 북/45초/저해상도 준비값을 사용하되 **관람 토글도 ON**으로 공유해 두 기능의 동시 적용을 검사했다. 실제 관람창을 여는 동작은 위 PlayMode의 관람 검사 근거와 구분한다. source Config의 기본값은 **관람 OFF·Arc**, 기존 투척 속력 상한·Gain·중력은 그대로다. 기존 사용자 Scene·머티리얼 2개는 작업 전 사본과 바이트가 같으며, 원본 Config는 main의 관람 bool과 직구 필드 추가 외에 기존 값을 보존했다. 최종 통합 사용법은 [직구 가이드](THROW_TUNING_GUIDE.md)의 119개 설정 기준을 따른다.

### #194 박 줄 움직임과 측면 타격 기울기 — 2026-10-18

현재 작업 브랜치의 최종 129개 DEV 설정과 프로토콜 5632 기준이다. 원래 프로젝트가 Unity Editor에 열린 상태여서 동일 소스를 임시 프로젝트에 복사해 Unity 6000.6.5f1로 검사했다. XML과 로그는 `/tmp/c6-swing-*`에만 있으며 저장소에 포함하지 않는다.

| 항목 | 상태 | 확인 범위 |
| --- | --- | --- |
| EditMode 전체 | PASS | `c6-swing-editmode-final.xml`: **370/370 통과**. 설정값 원자 적용·범위 검사·지문 변경을 포함한다. |
| PlayMode 전체 | PASS | `c6-swing-playmode.xml`: **123/123 통과**. 저장 Scene의 DEV 적용, 줄의 실제 충돌체 이동, 빠른 측면 타격의 추가 기울기와 40도 상한을 포함한다. 이후 측면 타격 테스트의 빠른 공을 저장된 최대 투척 속도 12.5로 낮춰 `c6-swing-sidehit.xml`에서 **1/1 재통과**했다. |
| 실제 화면에서 아랫면 조준 시간 | NOT_RUN | 카메라·모델의 가시성과 손 스와이프 타이밍은 Editor 수동 플레이나 기기에서 별도 확인해야 한다. |
| 다중 기기 동기화·iOS 빌드 | NOT_RUN | 설정 전파 코드는 기존 Host 경로에 연결했지만, 이번 변경에서 새 앱 빌드나 2~5기기 접속은 실행하지 않았다. |


### #195 병합 전 보완·DEV 조작 재검증 — 2026-10-18

검토 기준 main `fbec1b0d`, PR #195 원본 head `d0019dee`, Unity **6000.6.5f1**, 저장 Scene `Assets/Lab/Scenes/Lab.unity`. 위 #194의 작성자 실행 기록을 이번 결과로 승계하지 않고 원본 PR과 보완 소스를 별도 작업 폴더에서 새로 검사했다. 기존 119개 설정·관람·직구·100% 도색 규칙과 사용자 Scene·Material 변경을 보존한다.

정지 Snapshot을 반복 수신해도 Client의 줄 위상·명중 탄성과 몸체 Y축 회전이 계속 보간되지 않게 수정했다. 정지 시에는 유효한 Host 상태에 즉시 맞추고 각속도·탄성 속도는 재개용으로 보존한다. 정지 직후 들어오는 FixedUpdate도 TimeScale이 0이면 적분하지 않는다. 진행 중의 기존 완만한 보정은 유지한다. 새 조절값·범위·기본값·프로토콜5632/토큰10은 PR 원본 그대로다. DEV 생산 코드는 이미 129행을 확장·분류·연결하므로 중복 UI를 추가하지 않았다.

| 항목 | 상태 | 근거·실행 범위 |
| --- | --- | --- |
| Unity 컴파일·Config | PASS | 별도 폴더의 실제 import/compile, `ISSUE195_COMPILE_CONFIG_OK fields=129 amplitude=0.45`. 관람 기본OFF·기존 Arc 기본값을 확인했다. 최종 보완 후 아래 전체 검사와 일반 빌드도 실행했다. |
| 최종 전체 EditMode | PASS | `verified-editmode.xml`, **391/391**, failed/skipped/inconclusive=0. **10:07:44Z–10:07:45Z**. 새10값 각각의 min/max·비정상값 원자 거절·이전119개/관람/직구 보존·rope3값 JSON/분할 전송 왕복을 포함한다. |
| 최종 전체 PlayMode | PASS | `verified-playmode.xml`, **132/132**, failed/skipped/inconclusive=0. **10:08:08Z–10:09:18Z**. 기존123개 + DEV4개 + 정지 보완5개. 최초391/132 및 중간 yaw 보완391/132 통과 후 아래 실제 두 앱에서 드러난 정지 직후 물리 갱신까지 보완해 다시 실행했다. |
| 저장 DEV 화면의 10개 조작 | PASS | `LabGourdRopeDeveloperTests` 4개: Gourd 펼침·10개 숫자/슬라이더 양방향 연결·마지막128번 행의 실제 ScrollRect 스크롤/Canvas 입력 raycast 접근, 닫기/재열기·DEFAULTS·행 중복 없음, 두 솔로 프로필 적용/재시작과 실제 Hanging10속성/원본 불변을 확인했다. 실제 Host 생성/로비 재적용·무효값 거절도 포함한다. 이 검사의 참가자는 Host 한 명이며 Client ACK 성공을 대신하지 않는다. 실제 앱의 스크롤 전후 PNG 두 장도 열어 마지막 행까지 보이는지 확인했다. |
| 정지·재개·Retry 회귀 | PASS | `LabRopeReconciliationTests`는 실제 Snapshot commit 메서드에 조건을 주입한 회귀 fixture다. TimeScale0에서 줄 상태와 yaw0→10도·각속도0/80의 첫 패킷 확정, 같은 Snapshot12회 뒤 Collider/visual/rope pose 불변, 재개·Retry 초기화를 검사했다. 정지 확정 직후 FixedUpdate를 직접 호출해도 상태가 변하지 않는 검사도 포함한다. 실제 네트워크 수신 결과와 구분한다. 비유한값 무시·기존 unpaused 보간·물리 한계도 포함한다. |
| 최초 실제 두 앱 정지 검사 | FAIL | `network-initial-failed/` 양쪽 JSONL·Player 로그 보존. 10:06:48Z Client의 정지 수신 직후 줄 위상이 Host보다 **0.02초** 앞서고 탄성·yaw도 정확히 FixedUpdate 1회 적분한 값이었다. 이후 정지 패킷이 이를 되돌려 pose 불변 검사가 실패했다. Host는 Client 종료 뒤 공동 정지 해제로 실패했다. 예약된 물리 콜백이 한 번 더 실행됐다는 진단을 근거로 TimeScale 가드를 추가했으며, 아래는 두 앱을 모두 재빌드한 결과다. |
| 최종 실제 Development Host + Release Client | PASS | `HostVerified.app` **186,405,793 bytes**, `ClientVerified.app` **91,911,759 bytes**. 실제 NGO/UTP `127.0.0.1` 2프로세스, **10:11:07Z** 양쪽 final과 종료 코드 **0** 확인. Host 실제 DEV 입력/슬라이더로 두 프로필을 적용해 **129개 전체/새10개**가 Client·실제 Hanging 설정과 일치했다. Client 실제 보드 투척 → 움직이는 Host Collider 명중 후 **40/2048셀·명중1·HP0이지만 Playing**이 양쪽 같고 접촉 셀 RGBA·맵 해시가 일치했다. 공동 정지 2실시간초 동안 각 앱의 pose 최대 이동 **0**·줄 상태/yaw/전투 시계 불변, 재개 후 이동을 확인했다. Host 공동 정지 DEV 변경→실제 Client ACK→자동 새 판과 도색 초기화를 확인했다. 재시작 뒤 두 번째 실제 명중으로 **2048/2048·100% Victory**, 맵/관람 텍스처 해시 일치·개별 관람/회전 시 원본 불변·같은 방 로비 복귀 시 UI/텍스처 정리까지 확인했다. Host45·Client42개 기록 모두 pass이며 네트워크 비교 요약은 `network-verified-summary.json`이다. |
| 임시 도구 없는 일반 Mac 빌드 | PASS | Runtime/Editor 임시 실행기와 `.meta`를 제거한 뒤 기존 빌드 메뉴로 `Builds/Lab/20261018-191140-46a8953e/macOS/C6_Physics_Lab.app`, **186,353,463 bytes**, `C6_LAB_BUILD_OK target=StandaloneOSX`와 종료 코드 **0** 확인. 빌드된 Runtime 어셈블리에 임시 네트워크 실행기가 없고 관람 코드가 남아 있음을 확인했다. 이 일반 빌드의 별도 수동 플레이는 미실행이다. |
| iOS·실기기·핫스팟·3~5기기·인위적 지연/손실 | NOT_RUN | 이번 변경에서 실행하지 않았다. 실제 손 스와이프·아랫면 조준 기회·기본 밸런스/성능도 별도 확인이 필요하다. 측면 X/Z 흔들림은 기존 impact 재생 방식이며 각도·각속도 Snapshot은 없다. 로컬 2앱 성공을 지연/손실 환경의 전 기기 pose 일치로 확대하지 않는다. |

두 앱 검사는 **Fixture**다. 공통 준비는 전투45초·표면64×32·상자100%·해금0·생성 비용1·북 정지·생성 직후 팝/속도0·박 폭탄OFF·관람ON이다. 최초 도색2%·반복 명중 성장4%·성장 속도6%/초·메우기100%·수직 가중치1.35를 사용하며 첫 판 한 번의 명중으로 남은 부분 도색에서 정지 중 탄성·회전을 측정했다. 재시작 판만 최초 도색100%로 바꿔 실제 한 번의 명중으로 완료·관람 연결을 확인했다. 줄/기울기는 첫 프로필 `[0.2, 2.4, 0.25, 32, 2.5, 0.5, 10, 35, 12, 1.8]`에서 두 번째 `[0.15, 3.2, 0.42, 48, 3.2, 0.8, 15, 50, 16, 2.4]`로 DEV에서 바꿨다(index119~128 순서). 투척 fixture는 Arc·반지름4·높이 오프셋−0.5·최소 상승2·좌우/상승/전방 Gain2.5/2.1/4.5·최대속력18·중력9.81·Power1·Loft0·공기감쇠0으로 조준했고 HP1/피해20을 사용했다. target pose·도색 맵을 직접 주입하거나 ResetSwing으로 결과를 만들지 않았다. 이 값을 기본 밸런스나 실기기 입력 증거로 기록하지 않는다.

DEV 사용 방법은 [DEV 상세 명세](DEVELOPER_MODE_PARAMETER_SPEC.md)의 줄·기울기 조정 순서를 따른다. 수치를 바꾼 후 솔로 시작/재시작, Host 방 생성/로비 적용 또는 공동 정지 후 적용/재시작을 눌러 새 판에 반영한다. 숫자를 움직이기만 해서는 진행 중 값이나 원본 에셋을 바꾸지 않는다. 미적용 상태로 닫고 열면 마지막 적용값을 불러오며 DEFAULTS는 폼만 복원한다.

원시 XML·Editor/Player 로그·앱·PNG·임시 실행기와 자동 생성 Performance/URP 직렬화 변경은 공개 Git에 넣지 않는다. 위 실행과 단순 코드 존재, 작성자의 과거 임시 프로젝트 실행을 구분한다.

### Experiment #196 — 박 물감 내부 입자·농담 — 2026-10-18

기준 main `e429da10`, 브랜치 `experiment/#196-gourd-paint-grain`, Unity **6000.6.5f1 / URP17.6.0**. 정식 반영 전 실험이며 main에 자동 적용하거나 병합하지 않는다. 사용자 Scene·머티리얼 수정이 남은 원본 작업 폴더를 건드리지 않고 분리된 작업 폴더에서 검사한다.

도색 지도의 RGB에만 박 로컬 방향의 고정3D 노이즈를 곱해 미세 입자와 큰 농담을 표현한다. OFF 또는 두 강도0은 기존 합성식을 사용하고 미도색 종이·alpha·도색 셀·성장량·색 소유·승리 판정은 유지한다. 시간·화면 좌표·기기별 난수·새 텍스처를 사용하지 않으며 미해상 입자는 fwidth로 대비를 낮춘다. 기기간 GPU의 미세 픽셀까지 비트 단위로 같다는 보장은 하지 않는다. 실제 RGBA 지도·진행률을 공유하는 구조는 유지한다.

DEV의 Gourd에 인덱스129~132 네 개를 추가해 총133개, 프로토콜5633/기존 토큰10을 사용한다. 기본 OFF·크기1·입자 강도0.3·농담0.18이다. 해당 실험 버전끼리 접속하며 구 main 앱과 혼합하지 않는다. 기존 설정129개·관람111번·직구112~118번·줄/기울기119~128번을 보존한다. Config 에셋은 Unity 저장 API로 새 네 필드만 기록했다. MPB는 맵 revision과 별도로 설정 변경을 감지하고 기존 다른 속성을 보존하며, 완료품 관람 복제도 같은 설정을 캡처한다.

| 검증 | 상태 | 실행 근거·범위 |
| --- | --- | --- |
| Unity import/컴파일·저장 Config | PASS | 분리 프로젝트의 `PaintGrainValidation.Initialize`, `PAINT_GRAIN_EXPERIMENT_CONFIG_OK fields=133 enabled=False size=1 strength=0.3 mottle=0.18` 확인. 원본 Config diff는 새 네 필드 추가뿐이다. |
| 최종 전체 EditMode | PASS | `editmode.xml`: **404/404**, failed/skipped/inconclusive=0, **11:52:56Z–11:52:58Z**. 기존391+신규13. 네 설정의 끝값·JSON/공유 fingerprint·기존129개 보존·잘못된 값 원자 거절·구형 스키마 거절·Inspector 범위 방어를 포함한다. 이것은 실제 두 앱 연결 검증과 다르다. |
| 최종 전체 PlayMode | PASS | `playmode.xml`: **136/136**, failed/skipped/inconclusive=0, **11:53:16Z–11:54:30Z**. 기존132+신규 DEV2/렌더2를 전체 실행했다. GPU 부재의 렌더 Ignore는 발생하지 않았다. |
| 실제 곡면 셰이더 비교 | PASS | `SavedGourdShaderChangesOnlyPigmentAndZeroStrengthMatchesOffExactly`: 저장 WhiteGourd 실제 메시·재질의 512×512 렌더에서 미도색/불투명 alpha 불변, 도색 내부 변경100픽셀 초과, 시간 경과 후 같은 이미지, 크기 변경 시 다른 입자, 두 강도0/OFF의 원래 이미지 일치, CPU 맵·Revision·면적·명중 수 보존을 확인했다. `grain-partial-OFF.png`·`grain-partial-ON-default.png`·`grain-partial-ON-size3.png` 세 장을 열어 미세/굵은 입자 차이를 확인했다. 단순 PNG 파일 생성만으로 PASS를 기록하지 않았다. |
| DEV·완료품 관람 연결 | PASS | `LabPaintGrainDeveloperTests` 2개: 실제 저장 패널의 ON/OFF 버튼·세 슬라이더/숫자·마지막 행 스크롤/raycast·초안 폐기·솔로 ON→OFF 재시작·원본 보존. `UnchangedMapRebindsGrainWithoutEditingSharedMaterialAndViewerCopiesOverrides`: 같은 Revision에서 설정만 갱신하고 다른 MPB 속성/공유 재질을 보존하며, 완료 관람의 독립 재질/맵/새 네 값이 원본 변경 후에도 유지됨을 확인했다. 실제 연결 Client ACK나 실제 피격 완료는 이 검사의 범위가 아니다. |
| 일반 Mac 빌드 | PASS | 임시 Editor 저장기 `.cs/.meta`를 제거하고 기존 메뉴로 `Builds/Lab/20261018-205501-f03f0eb4/macOS/C6_Physics_Lab.app` 빌드. **186,359,980 bytes**, `C6_LAB_BUILD_OK target=StandaloneOSX`, 종료0. Runtime 어셈블리에 질감 설정 코드가 있고 임시 저장기는 없다. 이 앱의 별도 수동 플레이는 미실행이다. |
| 실제2앱·iOS·실기기·핫스팟·3~5기기·성능 | NOT_RUN | 이 실험에서 실행하지 않았다. 실제 손 조작·원거리/회전 시 반짝임·GPU 시간·발열·메모리·전송 화면 일치의 결과로 확대하지 않는다. |

렌더 검사는 저장 WhiteGourd 메시와 기존 셰이더 재질을 사용하는 **Fixture**다. 미리보기 `Paint`로 부분 색을 넣거나 일치하는 완료 맵/결과를 직접 준비한다. 실제 투척·Host 물리 피격·일반 플레이 승리 증거가 아니다. 원래 RGBA 지도와 revision을 보존한 채 OFF/ON/크기3/두 강도0을 비교한다. 그림은 로컬에만 보존하며 공개 저장소에 원시 XML·로그·PNG·빌드·임시 검사 실행기를 올리지 않는다.

이번 범위에는 도색 외곽 모양·위성 비산점·흰 구멍·Normal/높이맵·새 조명·Decal·계산 해상도 변경이 없다. 질감 사용법과 조절값은 [DEV 상세 명세](DEVELOPER_MODE_PARAMETER_SPEC.md)의 Experiment #196 항목을 따른다. 실험의 기본은 OFF이며 원본 프로젝트의 사용자 변경은 작업 전 해시와 비교해 보존한다.

#196 후속: 2026-10-18 사용자가 질감 실험의 PR 작성·main 병합을 승인했다. 위 기록은 당시 실험 실행 근거로 보존한다. 코드·기본 OFF 설정은 검증한 `4949ad9`와 같으며 병합용 후속 변경은 사용 문구 정리뿐이다. 미실행 실기기·실제2앱·성능 항목을 PASS로 승계하지 않는다.


### Experiment #198 — 박 물감 불규칙 번짐·아래 흐름 — 2026-10-18

기준 main `a26811b2`(#196 질감의 PR #197 병합본), 브랜치 `experiment/#198-gourd-spread-flow`, Unity **6000.6.5f1 / URP17.6.0**. 정식 반영 전 별도 실험이며 이번 요청으로 main에 병합하지 않는다. 원본 프로젝트의 커밋하지 않은 Scene/머티리얼 변경을 보존하기 위해 분리된 작업 폴더에서 실행했다. 저장 Scene·Prefab·그림·셰이더는 수정하지 않았다.

기본 **ORIGINAL(0)**은 기존 초기 선택·난수 순서·성장 경로를 유지한다. **SPREAD + FLOW(1)**은 같은 초기 면적 예산의 연결 비대칭 패치와 지속 끝점의 아래 흐름을 사용한다. 흐름 폭을 채우는 셀도 같은 면적 예산을 소비하고, 기존 도색을 덮거나 섞지 않는다. 흐름은 실제 아래쪽 인접 셀에서 출발하며, 계속되는 끝점도 위로 올라가지 않는다. 길이 한계·막힘·극점에서는 원래 경계 확장으로 돌아가 정지한 박의 마지막 빈 셀까지 채운다. 일반 번짐의 위쪽 확장은 완료 경로의 일부이며 중력 흐름과 구분한다.

Host는 프레임 경계에서 월드 아래를 `VisualMount` 로컬 방향으로 바꾼다. `LabModel.SetPaintFlowDirection`은 이전 방향으로 지난 구간을 먼저 소비한 뒤 새 샘플을 저장한다. 생성·Snapshot·명중 등 다른 요청은 마지막 샘플을 재사용한다. 서로 다른 FPS에서 각자 시뮬레이션한 지도의 비트 일치를 보장하지 않으며, Client는 기존 방식대로 Host의 정수 RGBA 지도/Revision을 수신한다. 기존 색 공급 FIFO·면적·속도·100% 목표·시간 만료 우선순위는 유지한다.

설정133~138의 모드·불규칙도0.55·흐름 확률0.35·전체 폭3도·최대 길이60도·중력 우대3은 **DEMO_TUNING_VALUE**다. 기존0~132를 유지해 총139개·프로토콜5634/기존 연결 토큰10을 사용한다. 같은 실험 버전끼리 접속한다. Config 에셋은 Unity 저장 API로 새6개 필드만 추가했으며 기본 ORIGINAL·Paint Grain OFF다. DEV 초안은 새 판/Host 적용에만 반영하고 원본을 저장하지 않는다. DEFAULTS 때 숨겨진 값과 토글 문구가 같은 버튼 동작에서 함께 갱신되도록 보완했다.

| 검증 | 상태 | 실행 근거·범위 |
| --- | --- | --- |
| 최초 컴파일/Config 저장·원본 Legacy 직접 비교 | PASS | 임시 Editor 저장기로139개·성장0·질감OFF를 확인하고 원본 `a26811b2`의 변경 없는 surface 코드와 현재 ORIGINAL을 동일 fixture로 실행했다. 세 해상도 모두 RGBA 일치. SHA-256은 각각 `4273982525467f9bc010a2268509c7fbdd39cbbc78b0c34a2593cd7fb9b98bab`, `707515ff7b960a3bf0a7261a1e0e49d92ffc347c3394c2fe51681ac118b9ee99`, `6e4a8fca6a46d335e91ef789e80e1891dc35df6a59513a0553d234a37dfa3b23`이며 영구 golden 회귀3건으로 남겼다. 임시 원본 복사/저장기는 제거했다. |
| 최초 전체 EditMode | FAIL | 464건 중463통과/1실패. 기존 테스트의 Gourd 마지막 인덱스132 기대값이 새138을 반영하지 못한 것이며 실패 XML/로그를 보존했다. 이후 아래 전체 검사를 실행했다. |
| 최종 전체 EditMode | PASS | **468/468**, failed/skipped/inconclusive=0. `editmode.xml` **13:04:18Z–13:04:20Z**. 기존404+새64. Legacy3·면적/시간 분할·연결/색/FIFO·모든 해상도·정지한 박의 마지막 셀·극점/장애/최대 길이·세 방향의 상향 흐름 방지·폭/길이 영향·Host 모델 샘플 시간 경계·Config 원자 검증을 포함한다. |
| 최초 전체 PlayMode | FAIL | 141건 중139통과/2실패. 첫 검사는 과거 fixture의 남은 전역 Network를 골라 target이 null이었으며 저장 Scene의 명시 참조를 사용하고 실패 시에도 정리하도록 수정했다. 둘째는 DEFAULTS 직후 토글 문구가 다음 프레임까지 남는 지연으로 실제 패널 갱신을 보완했다. 최초 실패 XML/로그 보존. |
| 최종 전체 PlayMode | PASS | **141/141**, failed/skipped/inconclusive=0. `playmode.xml` **13:04:56Z–13:06:08Z**. 저장 Scene DEV2·Host 샘플/공동 정지1·실제 메시 렌더2가 신규5건이다. 렌더 GPU 부재 Ignore는 없었다. |
| 실제 저장 메시의 성장/질감 비교 | PASS | 같은 seed198·초기3%·반복6% 두 번·성장10%/초의 명시 fixture로 ORIGINAL/새모드를 초기/0.25/0.75/2.75초에 비교했다. 각 시점 셀 수 일치·새 지도/화면 모양 차이·색3종 FIFO·예산 소진 후 정지·곡면 셰이더 노출을 검사했다. 별도5색 fixture로 전체 정수 셀100%와5시점의 미도색 종이 구멍 부재를 검사했다. 내부 질감ON은 지도/alpha/미도색 배경을 바꾸지 않았다. 렌더는256×128 표면·불규칙도0.55·흐름0.35·폭3도·길이60도·중력3을 썼다. `spread-flow-comparison.png`와 `spread-flow-settled-grain-ON.png`를 직접 열어 아래 꼬리 및 바닥의 상향 잔가지 제거를 확인했다. 실제 투척의 실행 증거와 구분한다. |
| 실제 Development Host + Release Client | PASS | `HostFlow.app` **186,423,222 bytes**, `ClientFlow.app` **91,924,332 bytes**. 실제 NGO/UTP `127.0.0.1` 2프로세스, 양쪽 종료0·final **13:08:49Z**, Host45·Client45개 기록 모두 pass. Host가 공식 DEV 폼으로 보낸139개 전체/새6개·질감ON이 Client와 일치했다. Client의 실제 보드 투척→움직이는 Host Collider 승인으로 부분40/2048셀의 RGBA 맵 해시가 같고 HP0이지만 Playing을 유지했다. 공동 정지2실시간초의 clock/pose 불변·재개, Host DEV 재설정→실제 Client ACK→새 판, 두 번째 실제 명중의2048/2048 Victory·관람 맵 해시 일치·개별 관람/회전·같은 방 로비 복귀 정리를 확인했다. `network-summary.json`과 양쪽 JSONL/Player 로그를 로컬에 보존했다. |
| 임시 도구 없는 일반 Mac 빌드 | PASS | 임시 Runtime/Editor 검사기와 `.meta`를 제거한 뒤 기존 빌드 메뉴로 `Builds/Lab/20261018-220951-4cb1472d/macOS/C6_Physics_Lab.app` 생성. **186,370,932 bytes**, `C6_LAB_BUILD_OK target=StandaloneOSX`, 종료0. 이 일반 앱의 별도 수동 플레이는 미실행이다. |
| iOS·실기기·핫스팟·3~5기기·성능 | NOT_RUN | 이 변경에서 실행하지 않았다. Mac 루프백 두 앱의 성공을 실기기/핫스팟/지연·손실 환경에 확대하지 않는다. 반복 명중 뒤 시간에 따른 꼬리 확장을 실제 두 앱에서 연속 관찰하는 검사도 미실행이며, 모양/예산/시간 검사는 위 CPU·GPU fixture로 구분한다. |

두 앱 검사는 **Fixture**다. 원시 로그·JSONL·XML·PNG·앱·임시 실행기는 공개 Git에 넣지 않는다. 첫 판은 표면64×32·초기2%의 실제 한 번 명중으로40셀을 칠해 부분 지도/정지를 확인하고, Host DEV 재시작 뒤만 초기100%로 실제 한 번 명중해2048셀 완료/관람 연결을 확인했다. 새 모양은 SPREAD + FLOW·불규칙도0.55·흐름0.35·폭3도·길이60도·중력3, 내부 질감ON·크기1·입자0.3·농담0.18이다. 나머지 도색 반복4%·성장6%/초·메우기100%·세로1.35와 투척/북/움직이는 표적 조건은 위 #195 병합 전 보완의 실제 두 앱 fixture를 재사용했다. target pose·명중·도색 맵을 직접 주입하지 않았다. 위 렌더 fixture의 직접 RegisterHit/Advance, 일반 플레이 기본 수치/손 입력과 구분한다.

사용법은 [DEV 상세 명세](DEVELOPER_MODE_PARAMETER_SPEC.md)의 #198 항목을 따른다. 이번 실험은 내부 질감과 독립적으로 켜며 혼색·기존 도색 덮기·공중 방울·위성 비산점·유체/점성 물리·새 Normal/조명은 포함하지 않는다. 도색 지도의 현재 색/불투명 셀·성장 공급·완료는 Host가 관리하며 프레임당 GPU/CPU 시간·발열·메모리·기기별 시각 품질은 아직 측정하지 않았다.


#198 후속: 2026-10-18 사용자가 PR 작성·검토 후 main 병합을 승인했다. 위 기록은 구현 커밋 `93c0f397`에서 실행한 근거로 보존한다. 병합 준비 후속 변경은 README·DEV/에셋 안내·이 기록의 적용 범위 문구뿐이며 게임 코드·Config·Scene·Prefab·패키지·테스트 내용은 실행 당시와 같다. 기본 성장 ORIGINAL과 질감 OFF를 유지한다. iOS·실기기·성능 등 NOT_RUN 항목을 병합만으로 PASS로 바꾸지 않는다.


### Experiment #200 — 실제 박 물감3종·사용자5색 비교 — 2026-10-18

기준 main `a3cf6f3e`, 브랜치 `experiment/#200-gourd-paint-style-comparison`, Unity **6000.6.5f1 / URP17.6.0**. Editor 비교 도구의 구현·실행 기록이다. 기존 사용자 Scene/머티리얼이 남은 원본 프로젝트와 분리해 검사했으며 전투 Scene·Config·Prefab·Shader·Host 도색/네트워크 코드는 변경하지 않았다. 본 게임에 자동 연결하거나 main에 병합하지 않는다.

넓은 얼룩형·튀긴 물감형·흐르는 물감형의 내장 image_gen 원본 마스크1254×1254를 만들어 실제 저장 WhiteGourd 프리팹·기존 셰이더로 렌더했다. 마스크의 실제 RGB에는 최대15/255의 채널 편차가 있어 luminance로 해석하며0.15 임계값 미만 배경 노이즈는 발자국이 아니다. 원본을 다시 그리거나 파일을 임의 보정하지 않았다. 생성 프롬프트·에셋 근거는 해당 폴더의 `GENERATION.md`에 남겼다.

사용자 후속 팔레트는 첨부 이미지의 프로필을 sRGB로 변환해 주황 `#D85736`·노랑 `#F8B72A`·초록 `#3D8258`·파랑 `#1471A7`·갈색 `#6F4A32`를 사용한다. linear 지도에 쓰기 전에 sRGB→linear 변환을 적용했다. 이는 비교 도구의 기준색 변경이며 기존 전투의 속성/구슬색·Host 팔레트는 그대로다. 조명·내부 농담에 따른 화면 밝기 차이는 유지한다.

| 검증 | 상태 | 근거·범위 |
| --- | --- | --- |
| 최초 Unity 컴파일 | FAIL | `render-initial-compile-failed.log` 보존. Unity6.6의 Scene.handle이 SceneHandle로 바뀌어 int 암시 변환이 오류였다. Scene 상태 비교를 Scene 값 자체로 보관하도록 수정한 뒤 재실행했다. |
| 최초 임시 UI 검사 | FAIL | 렌더9개 출력은 끝났지만 패널이 없는 CreateInstance 창에서 Close하거나 값 변경 이벤트를 기대한 임시 검사기가 실패했다. 로그를 보존하고 실제 Show로 패널을 만든 뒤 조작하도록 수정했다. 게임 코드의 실패로 기록하지 않으며 아래 재실행 전에는 전체 PASS로 표시하지 않았다. |
| 최종 실제 곡면 렌더·UI Toolkit 조작 | PASS | `render-editor.log`, `PAINT_STYLE_COMPARISON_EXPORTED count=9`, `PAINT_STYLE_VALIDATION_OK actualRenders=9 uiImages=3 yawChanged=True sourceConfigUnchanged=True`, 종료0. 실제 Metal·WhiteGourd 프리팹·기존 paint 셰이더로3스타일×3단계의512×512 그림9개와1536×512 비교3개를 출력했다. 메타데이터 시각 **14:02:29Z**. 각 그림의 실제 물감 픽셀·색 변화가 존재하고 단색 배경만 출력되지 않았는지 확인했다. 실제 UXML/USS를 가져온 창에 Image3개가 연결되고, Show 후 Yaw40 변경 시 이미지가 달라지며 Stage Single 전환 시6.0% 표시로 바뀌었다. 원본 Config 직렬화와 기존 활성/로드 Scene·dirty 상태 보존도 확인했다. 임시 검사기는 삭제했다. |
| 같은 실제 표면 면적·모양 차이 | PASS | `paint-style-evidence.json`:1024×512 등면적 구면 지도·seed200·같은 색/위치/카메라. Single은세스타일모두31,457셀(6% 내림), Multicolor는94,371셀(18% 내림), Complete는524,288셀(100%)이다. 각 단계의3개 지도 해시·PNG 해시가 모두 다르다. 각 비교 그림을 직접 열어 큰 덩어리·튀긴 경계·가는 흐름이 구분되는지 확인했다. |
| 최초 전체 EditMode | FAIL | **497건 중494통과/3실패**, skipped0. 세 마스크 바깥 픽셀에 RGB0이 아닌 어두운 생성 노이즈가 있어 ‘정확한 검정’ 기대가 틀렸다. 실제 발자국 판정과 같은 luminance0.15 미만인지 검사하도록 수정했으며 최초 XML/로그를 보존했다. |
| 최종 전체 EditMode | PASS | **497/497**, failed/skipped/inconclusive=0. `editmode.xml` **14:05:21Z–14:05:24Z**. 기존468+새29. 마스크 복사·보간·바깥0·잘못된 입력, 동일 면적6/18/100%·결정성·3스타일 차이·실제 에셋/가져오기 설정·요청 팔레트5값을 포함한다. |
| 최종 전체 PlayMode | PASS | **141/141**, failed/skipped/inconclusive=0. `playmode.xml` **14:06:02Z–14:07:18Z**. 임시 검사기 제거 후 기존 Lab 전투·도색/DEV/관람 회귀를 실행했다. 이 결과는 새 마스크가 실제 Host 전투에 연결됐다는 의미가 아니다. |
| Mac/iOS 플레이어 빌드·실기기·핫스팟·다중 앱·성능 | NOT_RUN | 이번 변경에서 실행하지 않았다. Editor 도구의 GPU 출력·CPU 검사·기존 PlayMode 성공을 새 스타일의 게임/기기 검증으로 승계하지 않는다. |

비교는 **준비된 시각 fixture**다. 단일 얼룩6%·다색18%·전체100%를 사용하며 Complete는 빈 셀을 가까운 기준 타격색으로 채운 미리보기다. 시간 성장·명중 승인·피격·실제 게임 승리나 네트워크 상태를 만들지 않는다. 같은 지점의 반복 명중 색띠 문제나 덧칠 규칙을 해결했다는 증거가 아니다. 실험 사용법/팔레트·기술 한계는 [3종 비교 안내](PAINT_STYLE_COMPARISON_EXPERIMENT.md)에 정리했다.

전체 원시 PNG·XML·Editor 로그·메타데이터·임시 실행기는 로컬에 보존하고 공개 Git에 넣지 않는다. 개인 정보가 없는 선택용 다색 비교1장만 `docs/images/gourd-paint-style-comparison.png`에 그대로 복사했다(추가 합성·보정 없음). 자동 생성 URP 직렬화 변경은 보존 후 제외했고, 기존 사용자 Scene/머티리얼은 작업 전 해시와 비교해 보존했다.


### Experiment #200 후속 — DEV4종·기본 Splatter·실제 초기 튀김점 — 2026-10-18

기준 main `a3cf6f3e`, 실험 브랜치 `experiment/#200-gourd-paint-style-comparison`의 비교 도구 커밋 `766101e0` 이후 후속 변경이다. Unity **6000.6.5f1 / URP17.6.0**, 기본 `Assets/Lab/Scenes/Lab.unity`. 사용자가 **초기 독립 튀김점도 실제 도색으로 집계**, **기존 포함4종 DEV 선택**, **Splatter 기본값**을 승인했다. 기존 #200 Editor 전용497/141을 이번 기능의 PASS로 승계하지 않았다.

실제 입력 경로는 Host 승인 물리 명중 → `LabModel.ConfirmHit` → `LabPaintSurface.RegisterHit` → 같은 영역의 `Advance` → 기존 셀 복제/표시다. 미도색 최초 명중의 마스크를 연결 성분별로 나눠 초기 면적 예산을 소비한다. 떨어진 튀김점도 opaque셀·Region·진행률에 포함하고, 이후 명중은 맞은 연결 영역에만 공급한다. 면적/속도/100% 목표·기존색/FIFO 보존·일반 인접 fallback을 유지했다. 새 설정140개·프로토콜5635·토큰 `C6PhysicsLab/11`이며 기존0~138번은 보존하고139번을 추가했다. Config 에셋의 `paintPattern: 2`는 Unity SerializedObject/SaveAssetIfDirty로 기록했다.

| 검증 | 상태 | 실행 근거·범위 |
| --- | --- | --- |
| 컴파일·에셋 기본값·실제 GPU 렌더 | PASS | `prepare-final-editor.log`, 종료0, `PAINT_PATTERN_DEFAULT_SAVED pattern=Splatter fields=140`, `PAINT_RUNTIME_PATTERN_VALIDATION_OK renders=12 default=Splatter`. 실제 저장 WhiteGourd·기존 셰이더·Metal·256×128 Point로4종×초기/성장/완료를 출력했다. `runtime-pattern-evidence.json`의 생성 시각은 **2026-10-18T14:32:22.2578110Z**다. Config 원본의 검사 전후 JSON 일치와 SourceConfig 보존을 확인했다. |
| 동일 예산의 실제 셀·독립 튀김점 | PASS | 명시적 렌더 fixture의 초기6%는4종 모두1,966/32,768셀. Splatter34·Flow42·Broad3개 실제 연결 성분(위치/seed/해상도별로 달라지는 측정값)을 기록했다. 같은 지점4회 반복 공급 뒤4종 모두7,208셀, 실제 공급/Advance로32,768셀까지 완료했다. 초기/성장 sheet를 직접 열어 실루엣·색띠·흐름 차이를 확인했다. 비교 도구의1024×512 지도/강제 완성은 사용하지 않았다. |
| 최초 EditMode | FAIL | 560건 중558통과/2실패, skipped0. 기존 Gourd 카테고리의 전체 배열 기대2곳에 새139번이 누락됐다. 표시 순서에 맞춰 갱신했으며 새 도색 검사 자체는 통과했다. `editmode-initial-failed.xml`·로그 보존. |
| 최초 PlayMode | FAIL | 145건 중14통과/131실패, skipped0. Surface 기본 생성자에서 새 Resources 마스크를 읽어 Unity의 Scene 직렬화 중 Load 금지 예외가 발생했다. 생성자/Reset에서 리소스를 읽지 않고 실제 신규 명중 시점으로 지연했다. 잘못된 패턴의 Reset 거부와 마스크 실패 시 명중 상태 보존을 유지했다. `playmode-initial-failed.xml`·로그 보존. 예외를 Ignore/Expect로 숨기지 않았다. |
| 최종 전체 EditMode | PASS | **560/560**, failed/skipped/inconclusive=0. `editmode.xml` **14:33:34Z–14:33:38Z**. 신규63건은 실제3Resources·면적/해상도·분리 성분/해당 영역만 성장·유한 공급·seam/극점·정지한 박의 마지막 셀·결정성·실제 RGBA 복제·4종 설정·잘못된 입력 원자 거부·팔레트를 포함한다. 기존 Legacy 검사는 두 성장 방식의 의미를 명시하고 기존 Original3해상도 해시를 유지했다. |
| 최종 전체 PlayMode | PASS | **145/145**, failed/skipped/inconclusive=0. `playmode.xml` **14:35:44Z–14:36:56Z**. 저장 Canvas의 이름4버튼·선택 표시·Legacy 토글 비활성·DEFAULTS 즉시 반영·솔로4종 재시작·초기화·Host 로비의 인원/ACK/공동 재시작 경계 검사4건을 추가했다. GPU 필요 검사가 건너뛰어진 항목은 없다. |
| 최초 실제 두 앱 검사 | FAIL | 검증 도구가 Host에서도 표시용 Replica의 Pattern을 권위 성장 설정으로 오인했다. 실제 Flow Config·물리 명중·Client 완료 맵은 기록됐지만 Host 검사 중단으로 Client도 연결을 잃었다. 게임 코드가 아닌 임시 검사기의 읽기 경로를 Host의 Model로 수정했다. `network-initial-failed/`에 양쪽 JSONL/Player 로그·캡처를 보존했다. 이 실행을 전체 PASS로 보지 않았다. |
| 실제 Mac Development Host + Release Client | PASS | 새 `HostPatternsV2.app` **195,894,908 bytes**, `ClientPatternsV2.app` **101,388,021 bytes** 빌드 후 NGO/UTP `127.0.0.1`의 실제2프로세스. Host51·Client46개 기록 모두 pass·양쪽 종료0, 마지막 **14:43:58Z**. 실제 DEV4개 이름 버튼으로 Splatter→Flow→Legacy→Broad를 선택·공유하고 Client 수신/확인 응답 뒤 진행했다. SourceConfig140개·Splatter 기본값 보존을 매 기록에서 확인했다. |
| 두 앱 실제 명중·맵·재시작·관람 | PASS | 첫 판은 초기2%/64×32의 Splatter로 실제 Client 보드 투척→움직이는 Host Collider 승인→**40/2,048셀**. 양쪽 맵 SHA-256 `617b4a4b…d7b1d03` 일치, 실제 목 물감 접촉 RGBA `0c3919ff`를 확인했다. 공동 일시정지2실시간초의 시계·포즈 불변 후, Host DEV Flow 초기100%로 설정 공유/ACK/재시작해 구도색0을 확인했다. 두 번째 실제 물리 명중으로**2,048/2,048 Victory**, 양쪽 맵 `58f6743e…60393aa`·관람 맵 일치·개별 회전·같은 방 로비 복귀 자원 정리를 확인했다. 이어 같은 방의 Legacy/Broad 공유가 확인됐다. `network-summary.json`·양쪽 JSONL/로그가 근거다. |
| 임시 도구 없는 일반 Mac 빌드 | PASS | 임시 Runtime/Editor 검사기와 `.meta`를 분리 보관한 뒤 기존 빌드 메뉴로 `Builds/Lab/20261018-234512-7ae5ad6b/macOS/C6_Physics_Lab.app` 생성. **195,834,139 bytes**, `C6_LAB_BUILD_OK target=StandaloneOSX`, 종료0. 일반 앱의 별도 수동 플레이는 미실행이며 위 자동화용 두 앱 실행과 구분한다. |
| iOS·실기기·핫스팟·3~5기기·모바일 성능 | NOT_RUN | 이번 변경에서 실행하지 않았다. 실제 두 Mac 앱의 결과를 iOS/핫스팟/터치·연속 미도색 명중의 지연·GC·발열 결과로 확대하지 않는다. |

두 앱은 **자동화 Fixture**다. 기본180초/256×128/초기2%의 일반 손 플레이 전체를 검증한 기록이 아니다. 시간45초·표면64×32·초기2%→100%·아이템 상자100%와 해금0·생성 비용1·투척/움직임을 제어해 실제 충돌을 만들었으며 target pose·명중·색칠 맵을 직접 주입하지 않았다. 후속100%는 초기 한 번 명중 완료의 극단 설정 검사이고, 반복 성장으로 마지막 셀까지 도달하는 검사는 위 실제 CPU/GPU fixture와 구분한다. #198 때 사용한 움직임/투척 fixture를 이번 소스로 다시 빌드·실행했다.

코드/Config/최초도색량/속도/Scene/구슬재질/패키지의 불필요한 변경은 분리한다. Scene·원본 작업 폴더의 사용자 파일은 작업 전 해시와 대조한다. Unity가 자동 재직렬화한 Target2 머티리얼 색 미세차는 별도 보존 후 분리 작업 폴더에서만 제외한다. 전체 원시 로그·XML·앱·임시 검사기는 공개 Git에 넣지 않고, 개인 정보 없는 실제4종 박/DEV 캡처만 문서에 포함한다. 실제 사용법·파일 지도·해상도/색띠/성능 한계는 [실제 전투 패턴 안내](PAINT_PATTERN_RUNTIME.md)를 따른다.


### Feat #204 — 로비 P번호 선택 복원 — 2026-10-19

기준 main `01df1bd6`(#203 추임새 빈도 반영본), 브랜치 `feat/#204-lobby-position-selection`, Unity **6000.6.5f1 / URP17.6.0 / macOS ARM64**, 기본 Scene `Assets/Lab/Scenes/Lab.unity`. 이전 #139의 선택 UI·Host 승인 경로를 활용하되 당시 검증 결과를 승계하지 않았다. 일반 방의 접속순 자동 재배정을 제거하고 Host를 포함한 미선택 → 고유 P1~PN 선택 → 전투 동안 고정 흐름을 복원했다. 프로토콜은 **5637**, 연결 토큰은 기존 `C6PhysicsLab/11`, #203의 공유 설정 **142개**를 유지했다.

| 검증 | 상태 | 실행 근거·범위 |
| --- | --- | --- |
| 코드 차이·Scene/설정 보존 | PASS | `git diff --check` 및 최종 변경 파일 확인. 저장 Lab Scene·LabConfig.asset·패키지 파일은 기준 main과 동일하다. 원본 작업 폴더의 사용자 Scene·Target2 머티리얼은 별도 작업 폴더에서 수정하지 않았고 보존 해시와 대조했다. Unity가 재직렬화한 작업 사본의 머티리얼 소수점 차이만 별도 보존 후 이번 변경에서 제외했다. |
| 전체 EditMode | PASS | **566/566**, failed/skipped/inconclusive=0. `editmode.xml`, **2026-10-19 03:32:31Z–03:32:35Z**. 2~5인 고유 선택·중복 점유·해제·번호 순서/각도와 명시적 자동 검사 모드·같은 방 유지/초기화를 포함한다. |
| 전체 PlayMode | PASS | **150/150**, failed/skipped/inconclusive=0. `playmode.xml`, **03:33:03Z–03:34:16Z**. 저장 로비 Canvas의 P1~PN 버튼·Host P3 선택·본인 번호 해제·DEV 가림·선택 수 표시, 승인/매핑 순서·오래된 응답·거절·5초 시간 초과·DEV 재시작 잠금 검사와 기존 전투 회귀를 실행했다. 시작 조건 일부는 합성 로비 상태를 사용하는 검사이며 실제 통신 결과와 구분한다. |
| 독립 Mac 앱 2~5인·수동 로비 경로 | PASS | 임시 관찰/입력 도구를 포함한 Development 앱, 실제 NGO/UTP `127.0.0.1` 전송. **71/71 checkpoint**, 앱 2+3+4+5개의 종료 코드 모두0. 일반 UI Host/Join/P선택/Start를 호출했고 `--lab-auto-start`와 모델/자리 직접 주입은 사용하지 않았다. 각 인원에서 Host PN 선점 후 참가자 미선택 입장, START 차단, 본인 선택 해제, 점유 버튼 비활성 및 공개 API를 통한 점유 요청 거절, 3~5인 동시 같은 번호 요청의 단일 승인, 확정 점유표 일치·142개 설정, DEV 뒤 가림, P순서 Snapshot·좌우 이웃, 같은 방 복귀·퇴장한 자리만 해제·재입장 미선택·새 방 전원 초기화를 확인했다. `network-checks.json`, `network-run.txt`, 앱별 `state.json`·명령 JSONL·Player 로그가 로컬 근거다. |
| 실제 2인 DEV 재시작·Retry | PASS | 같은 독립 앱에서 Client의 일시정지 요청 → Host DEV 설정 공유/ACK 재시작 → **3초** 전투의 실제 Defeat → 저장 RETRY 버튼 순서로 실행. 각 시작의 Host/Client P번호와 P순서가 유지됐다. 수동 손 조작·장시간 전투를 검증한 기록은 아니다. |
| 임시 도구 없는 일반 Mac 빌드 | PASS | 임시 검사 C#·meta를 프로젝트 밖으로 제거한 뒤 기존 `C6Lab.Editor.LabBuild.Mac` 실행. `Builds/Lab/issue204-final/macOS/C6_Physics_Lab.app`, **195,836,115 bytes**, `C6_LAB_BUILD_OK target=StandaloneOSX`, 종료0. 일반 앱의 별도 수동 플레이는 미실행이다. |
| iOS·실기기·핫스팟·모바일 성능 | NOT_RUN | 이번 변경에서 실행하지 않았다. 실제 Mac 프로세스 수를 실기기 수로 간주하지 않는다. |
| 이번 브랜치의 실제 앱 구슬 전달·시점/투척 위치 화면 관찰 | NOT_RUN | 관련 P순서·전달 모델/각도·기존 투척 검사는 EditMode/PlayMode에서 통과했고 실제 앱의 Snapshot 좌우 이웃도 확인했다. 이번 다중 앱 실행에서 구슬을 직접 전달하거나 각 화면의 시점·투척 위치를 시각적으로 비교하지는 않았다. |

선택 응답은 요청 ID·번호가 일치해야 하며 승인 결과가 지정한 로비 Revision 이상의 실제 점유표까지 받아야 연속 변경 잠금이 풀린다. 거절·시간 초과는 승인되지 않은 번호를 만들지 않는다. DEV 재시작의 설정 ACK 대기는 Host가 `restarting` 상태로 공유해 번호 변경을 잠근다. 일반 같은 방 로비 복귀에서는 변경을 다시 허용한다. 시작 조건은 설정 인원 전원 입장·고유 P번호 선택·최신 설정 ACK이며, Host 권한과 P번호는 독립이다. 솔로 P1과 명시적 자동 검증용 빈 번호 할당은 별도 경로다.

원시 로그·XML·명령 JSON·앱·임시 검사 도구는 공개 Git에 포함하지 않는다. 이번 작업에서 기존 사용자 Scene·에셋 배치를 덮어쓰거나 저장 Scene을 다시 생성하지 않았다.


### Feat #206 — 실제 투척자 P번호별 도색 무늬 — 2026-10-19

기준 main `b438581f`(#204), 브랜치 `feat/#206-player-paint-patterns`, Unity **6000.6.5f1 / URP17.6.0 / macOS ARM64**. 프로토콜 **5638**, 공유 설정 **142개**를 유지했다. 기존 사용자 Scene·머터리얼·미추적 파일은 분리 작업 폴더 밖에서 보존하고 해시를 대조했다.

| 검증 | 상태 | 근거·범위 |
| --- | --- | --- |
| 전체 EditMode | PASS | **609/609**, failed/skipped/inconclusive=0. `editmode.xml`. 새 귀속 코어33건·전송10건을 포함한다. P1~P5, 실제 투척자와 생성자 분리, 동일색/다른P·다른색/동일P, FIFO 마지막 공급자·부분 성장량·영역 합류·기존 성장/난수/완료 불변, 원자적 색상/투척자 복제·손상 복구·최대54조각을 확인했다. |
| 최초 전체 PlayMode | FAIL | **152/153**, 실패1. 새 GPU 검사의 이동 전후 픽셀 완전 일치가 큰 스테이징 좌표의 투영 반올림 차이를 허용하지 않았다. 새 UI 검사 .meta의33자리 GUID도 발견돼 두 검사가 수집되지 않았다. 결과를 PASS로 기록하지 않았다. `first-attempt-playmode.xml/log`에 보존했다. |
| 보완 후 무늬/UI 한정 검사 | FAIL | **4/5**, 실패1. GPU 비교를 비주기 이동·서브바이트 평균 오차 조건으로 바꾸고 UI GUID를 수정했다. 5종 실제 GPU 검사와 결과 복사는 통과했다. UI 검사는 숨겨진 로비의 저장 좌표를 LayoutGroup 최초 배치 후 좌표와 비교하는 검사 조건 문제였다. 기존 레이아웃 변경 없이 기준 시점을 Canvas 배치 완료 후로 수정했다. `pattern-playmode.xml/log`. |
| 최종 전체 PlayMode | PASS | **155/155**, failed/skipped/inconclusive=0. `playmode-final.xml`, **04:15:50Z–04:17:04Z**. 저장 Canvas8개 표본·선택/DEV/좌우P 연결, 실제 저장 박 메시/셰이더 P0~P5 GPU 픽셀·도색 알파/피복/미도색 표면 보존, 이동/시간 불변, 결과 관람의 독립 P맵 복사·해제를 포함했다. GPU 검사 건너뜀은 없다. 합성 지도/스냅샷을 쓰는 로컬 검사와 실제 물리·통신 검증은 구분한다. |
| 실제 독립 Mac Host/Client | PASS | 임시 opt-in 도구를 포함한 Development 앱의 실제 NGO/UTP `127.0.0.1`. Host **14/14**, Client **14/14** 기록과 교차 비교 **32/32** 통과, 두 앱 종료0. 저장 DEV142개·Host P2/Client P1 선택·Join/Start → 실제 음양 생성·드래그 조합 → Client P1 물리 명중 → 다시 만든 Combined를 실제 보드 끝으로 전달 → Host P2 물리 명중을 실행했다. 모델·목표·피격 상태를 직접 주입하지 않았다. |
| 실제 두 투척자·맵·결과·Retry | PASS | **04:18:12Z** 첫 명중 **1,024/2,048셀**, P1 **1,024**, P2 **0**. **04:18:14Z** 두 번째 명중 Victory **2,048/2,048셀**, P1·P2 각각 **1,024**. 최초 생성자가 P1인 두 번째 구슬의 실제 투척자 P2가 새 셀에 기록됐다. 양쪽과 Host 모델의 색상+P 해시 `eed692c9…0194c1e` 및 결과 관람 복제 맵 해시가 일치했다. **04:18:17Z** 저장 RETRY 뒤 도색/명중0·P맵 전부0·선택 P1/P2 유지를 확인했다. `network-01/network-summary.json`·양쪽 JSONL/로그·PNG가 로컬 근거다. |
| 시각 확인 | PASS (범위 한정) | 실제 GPU의 같은 색상 P1~P5 비교 이미지, 두 앱 결과 화면과 독립 완성 박 관람 PNG를 열어 확인했다. [5종 비교](images/player-paint-patterns.png)는 합성 부분 맵의 실제 GPU 렌더, [두 투척자 완성 박](images/player-paint-two-throwers.png)은 실제 위 물리/통신 실행 결과다. 기본256×128·일반 수동 플레이 전체를 확인한 기록은 아니다. |
| 임시 도구 없는 일반 Mac 빌드 | PASS | 임시 Runtime/Editor 검사기·meta를 프로젝트 밖으로 제거한 뒤 기존 `C6Lab.Editor.LabBuild.Mac` 성공. `Builds/Lab/issue206-final/macOS/C6_Physics_Lab.app`, **195,847,690 bytes**, `C6_LAB_BUILD_OK`, 종료0. 일반 앱 별도 수동 플레이는 미실행. |
| iOS·실기기·핫스팟·3~5실기기·모바일 성능 | NOT_RUN | 이번 변경에서 실행하지 않았다. 실제 Mac 두 프로세스·인원별 자동 검사를 모바일 검증으로 확대하지 않는다. |

실제 두 앱은 빠른 경계 검증을 위한 **64×32·최초/반복 도색50%·성장100%/초·시간70초·생성비용1·Yin/Yang50:50·직선 투척·움직임 조정**의 세션 복사본을 사용했다. 마지막 빈 셀까지 일반 성장 예산으로 완료하며, 원본 Config는 모든 기록에서 변경되지 않았음을 확인했다. 실제 실행에서는 소유 패턴 전체5종이 아닌 P1/P2를 확인했고 P3~P5는 코어/전송/GPU 검사로 구분했다.

Scene은 기존 UI 위치·크기를 유지하며 자식 Image8개(새 YAML블록32개), 부모 연결8개, HUD참조를 저장했다. Unity 저장 시 기존 코드 기본값과 같은 `cycleAmplitude:0`, `cyclePeriod:3`도 직렬화됐다. 객체/참조/값을 바꾸지 않는 새 블록·기존 빈 이름의 공백 정리만 추가했다. Config·패키지는 기준 main과 동일하며 Unity가 재직렬화한 Target2 머티리얼 소수점 차이는 별도 보존 후 제외했다.

원시 로그·XML·개인 경로·임시 검사 도구·빌드 산출물은 공개 Git에 넣지 않는다. 시나리오·코드 존재를 실행 통과로 기록하지 않았다.

### Feat #206 후속 — 개인 무늬·완성 박 관람 기본 ON 및 독립 DEV 토글 — 2026-10-19

위 최초 구현 커밋 `c5c9fdd` 이후 같은 `feat/#206-player-paint-patterns`에서 수행했다. Unity **6000.6.5f1 / URP17.6.0 / macOS ARM64**, 저장 Scene `Assets/Lab/Scenes/Lab.unity`. 공유 설정은 **143개**, 개인 무늬는 마지막 **인덱스142**, 기존 관람은 **111**을 보존한다. 프로토콜 **5639**로 이전142개 설정 앱과 구분한다. C# 초기값과 단일 원본 Config 모두 두 기능을 ON으로 저장했다. 기존 Scene·패키지는 이 후속에서 변경하지 않았다.

| 검증 | 상태 | 근거·범위 |
| --- | --- | --- |
| Unity 설정 저장·컴파일 | PASS | 임시 Editor 검사기를 통해 SerializedObject로 `completedGourdViewerEnabled`·`playerPaintPatternsEnabled`를 true로 저장했다. `PLAYER_PAINT_DEFAULTS_OK fields=143 viewer=ON patterns=ON otherValuesUnchanged=true`. 다른141개 유효값 불변을 확인했고 임시 소스/meta는 검사 후 제거했다. |
| 전체 EditMode | PASS | **625/625**, failed/skipped/inconclusive=0. `editmode.xml`, **04:28:05Z–04:28:09Z**. 새 설정16건은 저장/코드 기본 ON·독립4조합·복사/공유 JSON·지문·기존 인덱스·범주·0/1 외 입력 원자적 거절·구형/미지 배열 차단을 검사했다. 실제 원격 앱 연결 검사가 아니다. |
| 전체 PlayMode | PASS | **161/161**, failed/skipped/inconclusive=0. `playmode.xml`, **04:28:26Z–04:29:45Z**. DEV 버튼과 입력·DEFAULTS 미적용 초안/복원·솔로 재시작·Host 로비 적용·인원/일시정지 조건, 무늬8개 ON→OFF→ON과 P번호/라벨/원본/배치 보존을 검사했다. UI의 전투 이웃 확인은 합성 Snapshot fixture이며 실제5앱 실행으로 해석하지 않는다. |
| GPU 무늬 OFF·복원 | PASS | 실제 저장 박 메시/셰이더의 P2 도색을 ON→OFF→ON→OFF로 렌더했다. Revision·칠한 셀 수·색상/P텍스처 객체/픽셀 불변, 도색 데이터 변경 없이 표시 설정 즉시 반영, alpha 불변, OFF 그림 일치·ON 무늬 복원을 확인했다. 저장된 ON/OFF PNG를 열어 사선 표시 차이를 확인했다. 합성 부분 도색 지도에 대한 GPU 검사로 실제 투척/모바일 결과가 아니다. |
| 완성 박 관람 4조합 | PASS | 로컬 PlayMode의 실제 충돌 명중·성장으로 네 조합 모두100% Victory를 만들었다. 관람은 관람 토글에만 따라 결과 버튼을 제공하고 자동으로 열리지 않았다. ON에서 독립 맵·표현값을 복사하며 OFF에서 버튼/미리보기가 생성되지 않았다. 별도 복제 검사는 일반 및 submesh MaterialPropertyBlock의 무늬 플래그·독립 P맵 교체·회전/줌/해제를 확인했다. |
| 임시 도구 없는 일반 Mac Development 빌드 | PASS | 기존 `C6Lab.Editor.LabBuild.Mac`로 **195,848,670 bytes**, `C6_LAB_BUILD_OK`, 종료0. `Builds/Lab/issue206-toggle-defaults/macOS/C6_Physics_Lab.app`. 저장 Scene과 일반 코드만 포함하며 별도 빌드 앱 수동 플레이는 미실행이다. |
| 이 후속의 독립 Host/Client 앱·iOS·실기기·핫스팟·모바일 성능 | NOT_RUN | 최초 프로토콜5638의 두 앱 실행 기록을 이번5639의 통과로 승계하지 않는다. 이번 공유 경로 근거는 EditMode 전송/지문과 로컬 Host 설정 PlayMode다. |

무늬 OFF는 도색 알파·색상·투척자 기록·성장량·승리 판정을 바꾸지 않고 박의 RGB 무늬와 HUD 표본만 숨긴다. 두 설정은 기존 새 판 적용 경계를 따르며, DEFAULTS 자체는 초안만 복원한다. 관람은 100% Paint 결과의 버튼으로 수동 진입한다. 원본 작업 폴더의 사용자 Scene·머터리얼·미추적 참조4개는 기존 해시와 동일했다. Unity의 Target2 머터리얼 소수점 재직렬화2건은 사본을 보존한 뒤 이번 변경에서 제외했다. 원시 로그·XML·임시 검사 도구·빌드는 로컬에만 보관한다.

### Feat #208 — 재타격의 경계 인접 둥근 군집 — 2026-10-19

기준 main `e55bf231`(#207), 브랜치 `feat/#208-clustered-paint-growth`, Unity **6000.6.5f1 / URP17.6.0 / macOS ARM64**. 기존143개 설정값과 인덱스를 보존하고 **143/144번**에 `Paint Cluster Compactness`(0.85)·`Paint Cluster Irregularity`(0.25), 범위0~1을 추가했다. 공유 **145개·프로토콜5640**, 기존 재타격4%·전역 성장6%/초·개인 무늬/완성 박 관람 기본ON은 유지한다.

| 검사 | 상태 | 실행 근거·범위 |
| --- | --- | --- |
| Unity 설정 저장·컴파일 | PASS | 임시 Editor 검사기에서 SerializedObject로 두 새 float를 저장했다. `CLUSTER_DEFAULTS_OK fields=145 compactness=0.85 irregularity=0.25 old143Unchanged=true`. 임시 소스/meta는 저장 후 제거했다. 기존 설정·Scene·패키지·셰이더는 변경하지 않았다. |
| 최초 전체 EditMode | FAIL | **657/666**, 실패9·건너뜀0. `editmode.xml/log`, **06:15:08Z–06:15:13Z**. 새 군집 코어26건은 모두 통과했다. 실패는 기존 SpreadFlow의3해상도 전체 타임라인 고정 해시·중력 방향 꼬리1건·지속 꼬리3축·폭/길이 변화2건이었다. 이번에 교체한 재타격 동작을 요구하던 검사이므로 PASS로 기록하지 않았다. |
| 최종 전체 EditMode | PASS | **669/669**, failed/skipped/inconclusive=0. `editmode-final.xml`, **06:21:00Z–06:21:14Z**. 구형9건을 초기 얼룩 보존·중력/구형 꼬리 설정에 독립적인 새 군집·실제 인접 성장·면적/소유권 보존으로 갱신했다. 검사를 건너뛰거나 새 전체 지도 해시로 덮어쓰지 않았다. 뭉침/불규칙함은 각각 따로 변경하고, 분리된 마지막 흰 구멍 검사를 해상도3종으로 확장했다. |
| 둥근 군집의 집중·넓은 연결 | PASS (정의한 fixture) | 128×64, 초기6.25%·재타격3.125%·기본0.85/0.25, 난수5개. 각 타격 추가256셀이 하나의 연결 군집이며 기존 칠은 불변이었다. 새 군집의 각거리 RMS/같은 면적 원판 반경은 **0.8206–0.8462**, 기존 칠과 접하는 경계 비율은 **0.3478–0.3636**이었다. 연속 대기 타격이 첫 군집을 바꾸지 않는지, 소수 예산·병합·시작점 점유·분리 빈칸·이음새/극점·4초/40분할·4패턴×3해상도의100% 완료를 확인했다. 실제 모바일 프레임 성능을 뜻하지 않는다. |
| 전체 PlayMode | PASS | **166/166**, failed/skipped/inconclusive=0. `playmode.xml`, **06:18:54Z–06:20:13Z**. 두 슬라이더/숫자/스크롤·raycast·잘못된 입력 원자적 거절·DEFAULTS 초안·솔로/Host 적용·설정 지문/인원/재시작 조건·기존 투척/관람/무늬 검사를 포함한다. 실제 원격 참가자 ACK는 별도 두 앱 검사로 구분했다. |
| 실제 박 GPU 비교 | PASS (로컬 지도 fixture) | 저장 WhiteGourd 메시·기존 GourdPaint 셰이더, 초기/재타격/전체 면적과 난수 고정. 보존된 구형 셀 선택 함수를 직접 호출한 기준과 새 `Advance`를 비교했다. LEGACY 연결 초기6.25%·재타격3.125% 및 기본 SPLATTER 초기2%·재타격4%, 각각3회. 구형 기준은 재타격 등록 때 새로 소비되는 anchor RNG만 복원해 종전 선택 경로를 재현했다. 지도의 실제 추가량·잔여 예산이 같고 새 군집이 한쪽에 둥글게 붙는 PNG를 열어 확인했다. [비교 이미지](images/paint-cluster-growth-comparison.png)는 전자의 실제 GPU 결과이며 마지막 타격만 파랑이다. 비교용으로만 무늬·내부 질감을 숨겼다. 실제 투척·전체 플레이·구형 앱 실행 결과가 아니다. |
| 실제 독립 Mac Host/Client | PASS | 임시 opt-in 자동 입력/관찰 도구를 포함한 Development 앱, 실제 NGO/UTP `127.0.0.1`. 프로토콜5640·145개 설정·기본 군집0.85/0.25를 전파/ACK한 뒤 Host P2·Client P1, 실제 생성/드래그 조합·P1 물리 명중·같은 Combined를 보드 끝으로 P2에게 전달·P2 물리 명중·완성 박 관람·Retry를 실행했다. Host **14/14**, Client **14/14**, 교차검사 **32/32**, 두 프로세스 종료0. 모델/피격/도색 직접 주입은 없었다. |
| 두 앱의 군집 확장·결과 일치 | PASS | **06:22:38Z** 첫 명중에서1,024/2,048셀·P1 1,024. **06:22:40Z** 두 번째 명중의 성장으로2,048/2,048셀·P1/P2 각1,024, 양쪽/Host 모델/관람 복제본의 색상+P 해시 **`92277952…ecdab566`** 일치. **06:22:42Z** Retry 후0/2,048·P맵 전부0, 선택 P1/P2 유지. `network-01/network-summary.json`·양쪽 JSONL/로그/PNG가 로컬 근거다. |
| 임시 도구 없는 일반 Mac 빌드 | PASS | 임시 Runtime/Editor 검사기 및meta를 제거한 뒤 기존 `C6Lab.Editor.LabBuild.Mac` 성공, **195,854,605 bytes**, 종료0·`C6_LAB_BUILD_OK`. `Builds/Lab/issue208-final/macOS/C6_Physics_Lab.app`. 최종 일반 앱 별도 수동 플레이는 미실행이다. |
| iOS·실기기·핫스팟·3~5실기기·모바일/장시간 성능 | NOT_RUN | 이번 변경에서 실행하지 않았다. 자동 로컬 지도/GPU·Mac 두 앱 결과를 해당 환경의 통과로 승계하지 않는다. |

두 실제 앱은 빠른 경계 확인을 위해 **64×32·최초/반복50%·성장100%/초·시간70초·생성비용1·Yin/Yang50:50·고정 목표 동작/직선 투척**의 공유 DEV 복사본을 사용했다. 원본 Config145개는 실행 전후 동일했다. 기본256×128에서의 모든 수동 플레이 밸런스나 터치를 검증한 실행이 아니다.

타격의 `Ink`가 원래 anchor·형태 난수·지역 heap을 보관하고 실제 FIFO 잔량만 소비한다. 초기 얼룩이 병합 중 이전 잉크를 소비하는 기존 순서도 유지한다. 새 빈 셀은 확정된 칠에 인접해야 하며 기존 root의 칠은 무료로 통과해 분리된 흰 구멍에 도달한다. 형태 선호는 도달 가능성을 막는 조건이 아니다. 표면당 최대32,768셀까지 한 군집 탐색이 넓어질 수 있으므로 모바일 최악 프레임 시간은 별도 미검증이다. 구형 Flow Ratio·Drip Width/Length·Gravity Weight는 새 재타격 군집에 영향을 주지 않으며 이전 인덱스와 입력 검증은 보존했다.

원본 작업 폴더의 사용자 Scene·머터리얼·미추적 파일4개의 해시를 대조해 보존했다. 작업 복사본에서 Unity가 재직렬화한 Target2 머터리얼 소수점2건은 사본 보존 후 제외했다. 원시 로그·XML·임시 도구·빌드 산출물은 공개 Git에 넣지 않는다.

### Fix #211 — 최초 SPLATTER와 재타격의 물감 형태 일치 — 2026-10-19

기준 main `19289cbf`(#209), 브랜치 `fix/#211-splatter-growth-consistency`, Unity **6000.6.5f1 / URP17.6.0 / macOS ARM64**. 실제 플레이의 보고는 ‘잘게 흩어진 색·지저분한 경계·최초 얼룩과 성장한 부분의 이질감’이었다. 사용자는 최초 SPLATTER 유지, 재타격을 같은 물감이 터진 모양으로 조정하는 방향을 선택했다. #208의 한 지점 반복/LEGACY 비교와 50% 두 앱 검증은 이 다지점·기본 도색량의 시각 품질을 입증하지 못했으며 이번 결과로 승계하지 않는다.

| 검사 | 상태 | 실행 근거·범위 |
| --- | --- | --- |
| 기준 버전 결함 재현 | FAIL 재현 | 동일한 신규 코어21건을 기준 main에 실행한 `baseline-core-final.xml`: **12 PASS / 9 FAIL**, skipped0, **06:52:35Z–06:52:48Z**. 세 해상도 각각 초기 마스크의 겹침 확대·이전 대기 잉크 소비·후반 전역 빈칸 즉시 채우기 총9건을 재현했다. 테스트 보조 함수의 ‘변경 없는 Publish도 Applied’ 요구를 고친 뒤의 최종 수치이며 이를 생산 코드 결함으로 세지 않았다. |
| 기준 GPU 결함 재현 | FAIL 재현 | `baseline-render-final.xml`의3건 중 **1 PASS / 2 FAIL**. 기본2%/4%에서 새 최초 얼룩이 이전 P2 잉크 **600셀**을 시간 성장 전에 소비했다. 동일 최초 지도·RNG에서 재타격만 SPLATTER/Legacy로 비교하면 차이가 **0셀**로, 선택 마스크와 무관한 매끈한 군집임을 확인했다. |
| 수정 전 비교 캡처 | PASS (비교용) | 위 두 새로운 결함 기대값만 명시적 기준 캡처 모드로 제외한 `baseline-render-captures.xml` **3/3**, **06:50:46Z–06:50:53Z**. 최초·3회 재타격 및 다양한 위치5색을100%까지 캡처했다. 이 실행을 기준 코드의 결함 해결로 해석하지 않는다. |
| 최초 모양 보존 | PASS | seed200·최초2%의 기존 RGBA+P 지문을64×32/128×64/256×128에서 대조하고 고정했다. 각각40/163/655셀이며 SHA256 앞부분 `906e24b3…`, `bb126779…`, `98477080…`가 기준과 동일하다. 원본 마스크/Config는 바꾸지 않았다. |
| 최초 수정 코드 전체 EditMode | FAIL | `editmode.xml` **686/690**, 실패4. 새 복제 검사3건은 변경 없는 마지막 Publish의 빈 배열을 ‘수신 실패’로 간주한 보조 함수 오류였다. 기존 모델1건은 겹친 초기 마스크가 기존 잉크로 즉시 Victory를 만드는 종전 동작을 요구했다. 둘을 구분해 기록한다. |
| 최종 전체 EditMode | PASS | `editmode-final.xml` **690/690**, failed/skipped/inconclusive0, **06:50:54Z–06:51:23Z**. 빈 Publish는 지도 불변 검사를 유지한 무변경 처리로 수정했다. 모델 검사는 즉시 LastPainter=연결ID20(P2)·Playing·시간 성장 잔량, 이후 실제 성장의 FIFO 최종 공급자 연결ID10(P1)·Victory로 갱신했다. 최초 지문·고정 겹침 마스크·자기 Ink·면적/소수 예산·다지점/시간 분할·최종100%·전송 일치를 검사한다. |
| 전체 PlayMode | PASS | `playmode.xml` **169/169**, failed/skipped/inconclusive0, **06:48:58Z–06:50:27Z**. 기존166건과 새 실제 박 GPU3건을 포함한다. 변경 후 렌더의 신규 형상·자기 잉크 기대값은 생략하지 않았다. |
| 실제 저장 박 GPU 비교 | PASS (합성 지도 fixture) | production `RegisterHit`/`Advance`, 저장 WhiteGourd 메시·기존 셰이더의4방향 렌더와 실제 색상/P지도를 저장했다. 기본256×128·최초2%·재타격4%·성장6%/초, 난수7/123/777. 최초 반복 군집에서 새 셀의 최대 연결 묶음은 **93.1–99.1%**, RMS/동일 면적 원판 반경은 **0.843–0.931**이었다. 선택 마스크의 본체 윤곽 반영과 지역 집중을 함께 확인했고 작은 분리 셀까지 없어진다고 주장하지 않는다. |
| 다지점·5색·완료 | PASS (합성 지도 fixture) | 난수211로 미도색/도색 명중을 섞어 구면의 여러 방향에 적용했다. 각 새 셀의 실제 색상과 P번호 일치, 기존 칠 불변, 각 플레이어100셀 초과 기여, 마지막 셀까지 **32,768/32,768**, 잔량0을 확인했다. 25/50/75/100%·무늬ON/OFF·앞뒤를 캡처했다. 독립 투사체나 실제 세션을 플레이한 기록은 아니다. |
| 일반 Mac Development 빌드 | PASS | 기존 `C6Lab.Editor.LabBuild.Mac`, **BuildReport 195,857,118 bytes**, `C6_LAB_BUILD_OK`, 종료0. `Builds/Lab/issue211-final/macOS/C6_Physics_Lab.app`. 이번 전용 Runtime/Editor 자동화 도구는 없으며 테스트 어셈블리는 일반 앱에 포함되지 않는다. 빌드 앱 별도 수동 플레이는 미실행이다. |
| 독립 Host/Client 앱·iOS·실기기·핫스팟·모바일 성능 | NOT_RUN | 이번 수정에서는 실행하지 않았다. 새 도색 맵 복제 근거는 코어의 실제 full/delta 셀 프레임 검사이며 이를 네트워크 실환경 통과로 확대하지 않는다. |

형상은 같은 마스크 본체의128방향 윤곽을 완만하게 정리해 타격별 고정 거리 비용에 반영한다. 최초 튀김점은 남기고 재타격이 빈 표면을 건너뛰어 새 점을 찍지는 않는다. 시간 성장의 FIFO는 유지하고, 초기 마스크의 즉시 칠만 해당 타격의 Ink를 직접 소비한다. 초기 겹침 잔량은 마스크를 넓히는 대신 명중점 군집의 시간 성장으로 넘긴다. 마지막 빈틈 도달을 막는 마스크 경계는 없으며 이미 칠한 색을 덮지도 않는다.

[최초·재타격 비교](images/paint-splatter-first-repeat-comparison.png)와 [다지점100% 비교](images/paint-splatter-mixed-comparison.png)는 실제 Unity 캡처를 배치한 검토 이미지다. 사진 보정이나 생성형 이미지로 결과를 대체하지 않았다. 일반 Point 맵의 계단형 가장자리, 최초 위성점, 기존 색 사이에 남은 최후의 빈틈은 표현상 남을 수 있다. 모든 색 영역의 단일 연결·안티앨리어싱·덧칠/혼색을 추가한 변경이 아니다.

현재 사용자 Editor의 플레이와 원본 작업 파일을 유지하고 별도 복사본에서 실행했다. Scene·Prefab·마스크·Config145개·패키지·프로토콜5640은 기준과 동일하다. Unity의 Target2 머터리얼 소수점 재직렬화2건은 로컬 사본 보존 후 제외했다. 원시 로그·XML·빌드·기기 정보는 공개 Git에 넣지 않는다.

### Fix #211 최종 기본값 — 재타격4% → 1.5% — 2026-10-19

사용자의 추가 요청에 따라 재타격 기본값만1.5%로 줄였다. 위 #211의4% 비교는 당시 이전 기본값을 고정한 회귀 조건이며 현재 기본값의 결과가 아니다. 최초2%·성장6%/초·군집0.85/0.25·나머지144개 설정과 인덱스·프로토콜5640을 유지한다. 새 판/DEV DEFAULTS/Host 공유 경계에 적용하며 진행 중인 판이나 DEV에서 명시적으로 선택한 값을 덮어쓰지 않는다.

| 검사 | 상태 | 최종 실행 근거·범위 |
| --- | --- | --- |
| Unity 설정 저장 | PASS | 임시 Editor 저장기에서 SerializedObject로 원본 `paintRepeatPercent`만1.5로 저장했다. `REPEAT_DEFAULT_OK fields=145 repeat=1.5 other144Unchanged=true`. 에셋 diff도 해당 한 줄뿐이다. 코드 초기값·성장 설정 초기값·비유한수 대체값도1.5로 맞췄다. 임시 저장기와meta는 검사/빌드 전에 프로젝트 밖으로 이동했다. |
| 최초 기본값 변경 검사 | FAIL | `editmode-default1_5.xml` **687/693**, 실패6·skipped0, **07:12:26Z–07:12:54Z**. Config 기본값4%를 요구하던2건과 고정26회 공급으로100%를 기대한 완료4건이었다. 줄어든 공급량에 필요한 타격 횟수를 반영하지 않은 검사로 구분했다. |
| 최종 전체 EditMode | PASS | `editmode-default1_5-final.xml` **693/693**, failed/skipped/inconclusive0, **07:14:04Z–07:14:32Z**. Config 기대값을1.5로 변경하고 완료 검사는 `ceil((100-initial)/repeat)`회 공급으로100% 단언을 유지했다. 신규3해상도 기본값 검사는 재타격의 실제 추가 면적1.5%±1셀·기존 칠 불변·소수 잔량을 확인했다. |
| 최종 전체 PlayMode | PASS | `playmode-default1_5.xml` **170/170**, failed/skipped/inconclusive0, **07:15:03Z–07:16:31Z**. 기존4% 고정 회귀 fixture와 새1.5% 저장 원본 GPU검사를 구분했다. DEV·실제 투척/피격·관람의 기존 검사도 포함한다. |
| 1.5% 실제 저장 박 렌더 | PASS (로컬 지도 fixture) | 저장 Config를 직접 읽은256×128·seed123, 최초655셀 뒤 재타격3회가 각각491~492셀만 추가했다. 실제 도색/색상/P번호/이전 칠/소수 잔량을 검사하고4방향 캡처를 확인했다. 세 번 뒤 **2,129/32,768셀(약6.5%)**이며 같은 알고리즘의4% 비교는 **4,587셀(약14%)**다. [동일 조건 크기 비교](images/paint-splatter-repeat-default1_5.png)는 실제 Unity 렌더를 배치했다. |
| 최종 일반 Mac Development 빌드 | PASS | 임시 도구 없는 기존 `C6Lab.Editor.LabBuild.Mac`, `C6_LAB_BUILD_OK`, 종료0, **BuildReport195,857,117 bytes**. `Builds/Lab/issue211-repeat1_5/macOS/C6_Physics_Lab.app`. 별도 빌드 앱 수동 플레이는 미실행이다. |
| 독립 Host/Client 앱·iOS·실기기·핫스팟·모바일 성능 | NOT_RUN | 이번 최종 기본값 변경에서도 실행하지 않았다. 이전 다중 앱 실행이나 로컬 전송 fixture를 실환경 검증으로 승계하지 않는다. |

독립 소스 검토에서 초기 자기 Ink·겹침 예산·최후 빈칸 도달·1.5% 기본값 일치와 변경된 완료 검사에 병합 차단 사항을 찾지 못했다. 사용자의 원본 Scene/머터리얼/미추적 파일과 패키지를 보존하고 검사 복사본의 Target2 float 재직렬화2건을 제외한다. 최종 변경에는 사용자 요청의 Config 한 값만 포함하며 기존 검증의 ‘Config 불변’은 위 최초4% 단계의 범위다.

### Fix #211 — 병합 직전 main #214 통합 — 2026-10-19

PR #216의 첫 병합 시도 직전 #214가 main `929d9244`에 들어와 실제 GitHub 병합이 충돌로 거절됐다. main을 수정 브랜치에 병합하고 두 텍스트 충돌(`LabPaintClusterConfigTests`, DEV 명세)을 해결했다. 새 박/요괴 메시·애니메이션·WhiteGourd 프리팹·LabGourdPaint 크기 적용·146번째 설정 `Gourd Visual Size`(인덱스145·기본1.4)·프로토콜5641은 최신 main과 동일하게 보존한다. Config는 main 대비 재타격1.5% 한 값만 다르다. 위145개/5640 결과는 통합 전 단계이며 현재 계약으로 해석하지 않는다.

| 검사 | 상태 | 통합 최종 근거·범위 |
| --- | --- | --- |
| 통합 최초 전체 EditMode | FAIL | `editmode-main214.xml` **694/696**, 실패2·skipped0, **07:22:42Z–07:23:09Z**. main에 추가된 Gourd 카테고리 선두145번을 예상 배열2개가 반영하지 못했다. 유효해진146개 스키마를 거절하던 구형 검사도145개 구형·147개 미지원으로 갱신해 검증 계약을 유지했다. |
| 통합 최종 전체 EditMode | PASS | `editmode-main214-final.xml` **696/696**, failed/skipped/inconclusive0, **07:24:58Z–07:25:26Z**. 새 크기 설정의 카테고리/공유 스키마·1.5% 기본값·#211 면적/소유권/완료 검사를 포함한다. |
| 통합 전체 PlayMode | PASS | `playmode-main214.xml` **170/170**, failed/skipped/inconclusive0, **07:25:45Z–07:27:11Z**. 새 저장 박 표면 메시와1.5%/4% fixture·DEV·투척/피격·관람을 포함한다. 자동 지도/렌더와 실제 물리 검사 범위를 구분한다. |
| 새 박 표면 GPU | PASS (분리 메시 fixture) | `default1_5-repeat3` 등4방향을 다시 렌더했다. [새 표면의1.5% 결과](images/paint-splatter-main214-repeat1_5.png)는 새 PaintBody 메시를 분리한 렌더로, 전체 프리팹의 이동/크기/애니메이션이나 실제 명중점 정합성 검증이 아니다. 기존3개 비교 이미지는 통합 전 메시의 조건부 기록으로 보존한다. |
| 통합 일반 Mac Development 빌드 | PASS | 기존 `C6Lab.Editor.LabBuild.Mac`, `C6_LAB_BUILD_OK`, 종료0, **BuildReport198,906,969 bytes**. `Builds/Lab/issue211-main214/macOS/C6_Physics_Lab.app`. 새 main 에셋을 포함하고 임시 전용 도구는 포함하지 않는다. 별도 빌드 앱 수동 플레이는 미실행이다. |
| 독립 Host/Client 앱·iOS·실기기·핫스팟·모바일 성능 | NOT_RUN | 통합본에서도 실행하지 않았다. 앞선 통합 전 결과나 다른 변경의 실기기 기록을 승계하지 않는다. |

새 main 모델의 Host 피격 방향과 shell-local 셰이더 방향은 부모/자식 위치·크기에 따라 달라질 수 있다. 저장 메시 정점과 Transform을 사용한 정적 대조에서 차이를 확인했지만 실제 충돌/화면 재현은 하지 않았다. 이 기존-main 확인 항목은 [#218](https://github.com/DeveloperAcademy-POSTECH/2026-C6-M10-MUSA/issues/218)에 분리했고 #211에서 해당 코드·프리팹·물리 좌표는 변경하지 않았다. 통합본 검사의 PASS를 이 미확인 항목의 해결로 해석하지 않는다.

## #219 — 오행 먹선·담채 비행 트레일

기준은 `origin/main`의 `9d290fc9`에 분리한 `feat/#219-element-flight-trails` 작업이다. Unity6000.6.5f1·URP17.6.0의 macOS 환경에서 실행했다. 기존 검사 정책이나 검사 파일은 제거하지 않았으며 신규 표현 검사9건을 추가했다. 원시 XML·로그와 빌드 산출물은 저장소에 포함하지 않는다.

| 확인 범위 | 결과 | 실제 근거·제한 |
| --- | --- | --- |
| Unity 에셋 작성·컴파일 | PASS | Unity `AssetDatabase`·`SerializedObject`로 텍스처 가져오기, 공용 머터리얼,5종 프로필과 저장 외형 참조를 작성했다. 임시 작성 스크립트는 제거했다. |
| 투명 텍스처 | PASS(파일 확인) | 6개 PNG 모두 RGBA1024×1536, 알파 범위0~254이며 실제 투명 픽셀을 가진다. Unity 최대 가져오기512. 글씨·한지·구슬 본체를 포함하지 않는 중립 먹선/흰 담채 텍스처다. |
| 최종 전체 EditMode | PASS | `editmode-final.xml`: total696, passed696, failed0, skipped0. 2026-10-19 08:12:16~08:12:42 UTC, 약26초. 소스의 테스트 존재만으로 판정한 결과가 아니다. |
| 최종 전체 PlayMode | PASS | `playmode-final.xml`: total179, passed179, failed0, skipped0, 08:12:47~08:14:16 UTC, 약89초. 기존170건과 신규9건을 모두 실행했다. |
| 실제 경로·수명·재사용·정리 | PASS(PlayMode fixture) | 경로의 꺾임·세계 좌표 고정·최대 길이, 거리 간격, 금의 세 리본/세 붓선 묶음, 재사용 시 초기화, 단일 Host 피격 보고, 일시정지 중 잔상 보존·재개 후 소멸, 솔로 재시작·연결 종료 정리를 확인했다. |
| 저장 Scene 생성→조합→투척→피격 | PASS(PlayMode 통합) | 기존 저장 Scene 검사가 새 오행 프로필 연결을 사용하며 Collider·피해 감소·기존 피해 색을 확인했다. 별도 손가락 조작 시험은 아니다. |
| 실제 GPU 표현 | PASS(범위 한정) | 실제 `LabProjectile.Create/LaunchVisual`5개를 개별 표현으로 렌더했다. 단일 프로세스의 분리된 PhysX Scene, 동일 발사값, 실제 경로. 기본 정면·경로를 담은 측면·확대 정면의0.20초 비행과 확대 정면0.45초 비행에서 구슬 본체를 숨겨도5개 효과의 색 픽셀이 남고 오류용 마젠타가 없는지 확인했다. 표현 PNG는 `docs/images/element-trails-*.png`. 픽셀 검사는 예술적 완성도·손맛·기기 성능 판정이 아니다. |
| Mac Development 빌드 | PASS(빌드) | 임시 작성·튜닝 스크립트를 제거한 일반 `C6Lab.Editor.LabBuild.Mac` 빌드가 종료0과 `C6_LAB_BUILD_OK`를 기록했다. BuildReport 크기203,130,920바이트. 별도 앱 실행·실기기 실행 결과는 아니다. |
| iOS·실기기·다기기·핫스팟·모바일 성능 | NOT_RUN | 이번 변경에서는 실행하지 않았다. Host/Client 코드가 같은 효과를 사용한다는 확인을 실제 다기기 결과로 확대하지 않는다. |

초기 전체 PlayMode는175/179였다. 신규 fixture3건은 코루틴이 `LateUpdate` 기록 전에 값을 읽는 순서 문제였으며 제한시간 내 실제 이력·입자가 생길 때까지 기다리도록 수정했다. GPU1건은 비교용 측면 카메라의 잘못된 화면 범위였다. 기본 정면의 전투 카메라 비율을 보존하고 보조 시점은 실제 기록 경로를 같은 배율로 담도록 바꿨다. 이어 금의 짧은 정면 비행에서 세 리본이 원근 축소로 사라지는 문제를 발견해, 실제 거리 샘플에 세 붓자국을 함께 남기는 표현을 추가했다. 중간 실패를 최종 PASS로 덮어쓰거나 게임 판정을 변경하지 않았다.

투척 입력·Rigidbody 수치·피해·도색·게임 설정146개·프로토콜5641·Scene·Prefab은 변경하지 않았다. `LabOrbAppearance.asset`은 새5개 프로필 참조를 추가하며 Unity가 기존 `FormerlySerializedAs` 필드 이름과 이미 코드에 있던 낙하 기본값도 재저장했다. 값의 의미는 기존과 같다. 기존 main의 미저장/미커밋 Scene·머터리얼·구형 폴더 파일은 별도 작업 폴더 사용으로 보존했다.

## 폭탄 잠금 복원·무거운 폭탄 — 2026-10-19, 이슈 #212

기준은 Unity **6000.6.5f1**, main(`19289cbf`, #208 PR #209 포함)에서 분기한 `feat/#212-bomb-rework`이며, PR 전에 최신 main(`9d290fc9`, #214·#216 포함)을 병합했다.

- **잠금·해금**: 북 폭탄과 박 폭탄 모두 다른 아이템처럼 잠금 2~4(`Item Unlock Swipes Min`~`Max`)로 생긴다. 옆 사람에게 넘길 때마다 1 줄고, 잠긴 채 `Item Lifetime`(15초)이 끝나면 보유자 판에서 터진다. 마지막 잠금을 푸는 전달에서 Host가 수명을 새로 시작해, 해금된 폭탄은 `Orb Lifetime`(8초) 동안 던질 수 있는 공격 구슬이 된다(피해 `Bomb Damage Multiplier` 3배, 빗나감·만료 시 폭발 없음). Host는 잠긴 폭탄에만 도화선 시간을 보낸다.
- **박 폭탄 낙하**: 착지 지점 "!" 경고(`LabDropWarning`)를 지우고, 신호를 받으면 곧바로 떨어져 튕긴다. 박의 "!" 말풍선은 그대로다.
- **그림**: 몸통의 빨간 숫자는 남은 해금 횟수다. 꼭지에서 뻗은 밧줄 도화선(그림 기준 길이 × `Bomb Rope Length` 2)이 `Item Lifetime − 3초`(기본 12초) 동안 끝부터 타 들어가고, 두 색 별 모양 불꽃이 탄 끝을 따라온다. 마지막 3초는 기존 빨간 깜빡임이다. 해금 시 불이 연기와 함께 꺼지고 무지개 테두리가 돈다. 꼭지 그림 `ItemBombFuse.png`는 꼭지만 남기고 같은 GUID로 교체했다.
- **무거운 폭탄**: DEV 147~150번째(인덱스 146~149) `Bomb Drag Lag` 0.12초·`Bomb Release Speed` 0.5배·`Bomb Slide Deceleration` 2배·`Bomb Mass` 4배. 위로 던지는 투척 세기는 바꾸지 않는다.
- **설정·통신**: main #214의 `Gourd Visual Size`(인덱스 145)가 먼저 들어와, 병합하면서 무거운 폭탄을 146~149로 옮겼다. 설정 146 → 150개. 통신 버전은 main의 5641과 데이터가 달라 `5641` → `5642`로 올렸다.

| 항목 | 상태 | 실행 결과·남은 확인 |
| --- | --- | --- |
| C# 오프라인 컴파일 | PASS (컴파일만) | Unity 6000.6.5f1 내장 Roslyn으로 Runtime·EditMode·PlayMode·Editor를 단계마다, main 병합 뒤에도 컴파일했다. 오류 0. |
| EditMode·PlayMode 실제 실행 | NOT_RUN (최종 기록 없음) | 1단계 중간 실행에서 EditMode 7건이 실패했다: 팀원 검사 6개 파일의 프로토콜 고정값(5640)이 갱신되지 않았다 → 고정값을 갱신했다. 그 뒤 단계와 main 병합 뒤 전체 재실행 결과는 기록하지 않았다. |
| 솔로 화면: 도화선·숫자·무지개·연기, 즉시 낙하, 무거운 끌기 | NOT_RUN | 솔로에서는 이웃이 없어 해금이 일어나지 않는다. 해금 화면은 `Item Unlock Swipes Min`·`Max` = 0(처음부터 해금된 폭탄)이나 3인 실행으로 확인한다. |
| 3인 실행: 넘길 때 숫자 감소·해금 투척·잠긴 폭발, 무거운 폭탄 넘기기 손맛 | NOT_RUN | Mac 앱 3개에서 확인해야 한다. |
| iOS 실기기 | NOT_RUN | 손가락으로 끌 때의 지연과 넘기기 세기 체감 확인 필요. |

추가·변경한 검사:
- **EditMode** `LabModelTests`: `ALockedBombBlastsAtItsFuseButHandoffsDefuseItIntoAnAttackOrb`, `AnUnlockedBombHitsForTripleDamageAndAMissNeverExplodes`(기존 `ABombHasNoLockItsFuseBurnsOnHoweverOftenItIsPassedAndItIsNeverThrown` 대체)
- **EditMode** `LabDeveloperConfigTests`: `HeavyBombFieldsJoinTheItemOrbsGroupWithTheirDefaultsAndApply`; DEV 개수·프로토콜을 고정한 팀원 검사들을 150개·5642로 갱신, `LabPaintClusterConfigTests`는 143번부터 끝까지가 아니라 2개만 읽도록 수정
- **PlayMode** `LabOrbPhysicsTests`: `ALockedBombCannotBeThrownButAnUnlockedOneCanWithAnUpwardFlick`, `ABombWithArtDrawsTheCartoonBombWithItsRedUnlockCountAndABurningFuse`, `TheFuseRopeBurnsDownTowardTheCapOverTheFuseLessItsWarningSeconds`, `AnExpectedGourdBombFallsInAtOnceWithNoLandingWarningAndBouncesBeforeItCanBeTouched`, `AHeavyBombTrailsTheFingerAndOutweighsAnOrbWhileAnOrbSticksToTheFinger`, `AHeavyBombLeavesTheFingerSlowerAndSlidesToAStopSoonerThanAnOrb`
- **PlayMode** `LabDeveloperSoloIntegrationTests`: `SavedSceneSoloGourdBombFallsStraightOntoTheLocalBoard`(착지 경고 대신 즉시 낙하·잠금 2~4 확인)


## #219·#212 — 두 PR 최종 통합 검증 — 2026-10-19

사용자가 두 PR 모두 검증 후 main 병합을 승인했다. 기준 main `9d290fc9`, 트레일 PR #221의 `ccadb694`, 폭탄 PR #220의 `d56c80da`를 합친 코드에서 실행했다. Unity **6000.6.5f1 / URP17.6.0 / macOS ARM64**. 위 각 PR의 과거 검증·미실행 기록과 아래 합본 결과는 구분한다.

- 실제 충돌은 `README.md`와 이 검증 문서의 추가 문단 두 곳이었다. 오행 트레일 설명과 폭탄 기능·과거 검증 기록을 모두 보존했다. 현재 계약은 **설정150개·프로토콜5642**이며 기존 #214 크기 설정 인덱스145와 폭탄146~149를 유지한다. 반복 성장 기본값1.5%도 보존한다.
- Unity API로 외형 에셋의 폭탄 도화선·해금 횟수 필드를 다시 저장하고 오행 프로필5개·기존 숫자색을 보존했다. 삭제한 착지 경고 스크립트 참조가 저장 Scene·Prefab·에셋에 남지 않았는지 확인했다. Scene·Prefab·패키지는 수정하지 않았다.
- 독립 소스 검토에서 새 수신 View의 마지막 해금 전달에 연기가 빠지는 경로를 찾았다. 수신 View는 기존에 불타던 도화선이 없으므로 `SetFuse`의 소화 조건만으로는 효과를 시작하지 못했다. `PlayUnlock`에서 폭탄일 때 연기를 시작하되 이미 재생 중이면 재시작하지 않도록 최소 보완했다.

| 확인 범위 | 결과 | 실제 근거·제한 |
| --- | --- | --- |
| 해금 연기 결함 재현 | FAIL 재현 | `merge220-smoke-before-fix.xml`: 신규 수신 Board 회귀1건을 보완 전 코드에서 실행해0/1, 실제 SmokeVisible=false를 재현했다. 08:35:25 UTC. 테스트 작성만으로 결함 재현을 주장하지 않는다. |
| 최종 전체 EditMode | PASS | `merge220-final-editmode.xml`: **698/698**, failed/skipped/inconclusive0, 08:36:36~08:37:03 UTC, 약27.5초. 폭탄 규칙·설정150개·프로토콜5642와 기존 도색/게임 검사를 포함한다. |
| 최종 전체 PlayMode | PASS | `merge220-final-playmode.xml`: **183/183**, failed/skipped/inconclusive0, 08:37:08~08:38:38 UTC, 약89.5초. 오행 트레일9건·폭탄 물리/표현과 신규 수신 해금 회귀를 함께 실행했다. |
| 수신 해금·중복·종료 경계 | PASS(PlayMode fixture) | `ABombReceivedOnItsUnlockingHandoffSmokesOnceWithoutAPriorLocalFuse`: 실제 `LabOrbBoard.UpsertOrb`로 이전 도화선이 없는 새 View에서 연기·무지개·불꽃 종료를 확인했다. 반복 스냅샷이 효과를 재시작하지 않고 연기가 끝나며, 처음부터 열린 폭탄은 해금 연기를 만들지 않는지도 검사했다. 독립 기기 간 전달 실험은 아니다. |
| 일반 Mac Development 빌드 | PASS(빌드) | 임시 작성/검증 도구를 제거한 기존 `C6Lab.Editor.LabBuild.Mac`, 종료0·`C6_LAB_BUILD_OK`, **BuildReport203,134,644바이트**. `Builds/Lab/issue219-220-verified/macOS/C6_Physics_Lab.app`. 빌드 앱의 별도 수동 플레이는 미실행이다. |
| 코드·에셋 호환 검토 | PASS(정적 확인) | None 속성 폭탄의 기존 잔상 경로와 오행의 새5개 프로필, 삭제 스크립트 참조, 공유 설정 인덱스·통신 버전 및 충돌 표식을 확인했다. Unity 재직렬화만 생긴 Target2 머터리얼2개는 제외했다. |
| 독립 Host/Client·3인 앱·iOS·실기기·핫스팟·모바일 성능 | NOT_RUN | 이번 통합에서는 실행하지 않았다. 원래 PR의 수동 확인 표시와 오프라인 컴파일을 이번 합본의 플레이 검증 결과로 승계하지 않는다. 모든 참가자는 프로토콜5642인 새 빌드를 사용해야 한다. |

원본 작업 폴더의 사용자 Scene·Target2 머터리얼2개·미추적 Scenes.meta는 별도 보존하며 이 통합 커밋에는 포함하지 않는다. 원시 XML·로그·빌드 산출물과 임시 검사 도구도 공개 Git에 넣지 않는다. 기존 main 명중점 정합성 확인 항목 #218은 이번 표현·폭탄 통합으로 해결됐다고 표시하지 않는다.

## 박 색상 콤보 실험 — 2026-10-19, 이슈 #222

기준은 Unity **6000.6.5f1**, main(`dc4917d3`, #212 PR #220 포함)에서 분기한 `experiment/#222-gourd-color-combo`다.

- **DEV**: 맨 아래 `Combo` 묶음에 151~157번째(인덱스 150~156) 7개를 추가했다. `Gourd Color Combo` 기본 OFF, `Combo First Start` 20초, `Combo Repeat Interval` 30초, `Combo Time Limit` 10초, `Combo Penalty Duration` 10초, `Combo Penalty Size (x)` 0.5, `Combo Penalty Target` TEAM(THROWER 선택). `LabConfig.asset`은 바꾸지 않았고 저장 필드가 없으면 코드 기본값을 쓴다.
- **판정(Host)**: `LabGourdCombo`가 4칸 순서/색을 판정한다. 틀린 색은 즉시 실패(던진 사람 기록), 시간 초과는 마감 시각에 실패. 색 없는 공(폭탄/릴레이)은 무시한다. `LabModel`이 박 폭탄과 같은 방식으로 전투 시계 기준 예약(시간을 건너뛰어도 순서대로 처리)하고 색은 라운드 seed로 뽑는다.
- **페널티**: 실패하면 `LabPlayerState.comboPenaltyUntil`을 기록한다(TEAM 전원 / THROWER 틀린 사람만 / 시간 초과는 언제나 전원). 던질 때 `ThrowScaleFor`로 정한 배율을 공 ID별로 기억해 공 반지름(`LabProjectile.SetRadiusScale`)과 칠 면적(`LabPaintSurface.RegisterHit(..., areaScale)`)에 함께 쓴다. 다른 기기에는 `projectile` 패킷의 `sizeScale`로 같은 크기를 보낸다.
- **화면**: `LabGourdComboHud`가 실행 시 붙어 4칸 색/체크/남은 시간 막대/`CLEAR!`, `WRONG COLOR`, `TIME UP`/`SMALL BALL` 남은 시간을 그린다. 씬 파일은 바꾸지 않았다.
- **성공 보상(후속)**: 성공하면 팀 전체 `comboRewardUntil`을 기록하고 `Combo Reward Duration` 10초 동안 `Combo Reward Size (x)` 1.5배로 던지고 칠한다. 보상과 페널티는 마지막 결과만 남으며 한 사람에게 겹치면 페널티가 우선이다. HUD는 노란 `BIG BALL` 남은 시간을 보여준다. 콤보 패널 기본 칸 크기는 64 → 40px로 줄였고 글자/막대가 칸 크기에 비례한다.
- **설정/통신**: 설정 150 → 159개(콤보 7개 + 보상 2개), 스냅샷에 콤보 필드 추가, 프로토콜 `5642` → `5643`.

| 항목 | 상태 | 실행 결과/남은 확인 |
| --- | --- | --- |
| C# 문법 검사 | PASS (문법만) | 변경 파일을 Mono `mcs --parse`로 검사했다. `LabGourdCombo`는 Unity 없이 단독 컴파일했다. Unity 컴파일은 아래 실행 결과로 확인한다. |
| EditMode 실제 실행 | NOT_RUN (기록 없음) | `LabGourdComboTests` 14개, `LabDeveloperConfigTests`와 개수/프로토콜 고정 검사 갱신분, 기존 전체 EditMode. |
| PlayMode 실제 실행 | NOT_RUN | 개수 고정을 갱신한 Developer PlayMode 검사와 기존 전체 PlayMode. |
| 솔로 화면: 콤보 표시/체크/시간/결과 연출, 실패 후 작은 공/작은 칠, OFF 시 기존과 동일 | NOT_RUN | DEV → Combo ON, `Combo First Start`를 5초 정도로 두면 빠르게 확인할 수 있다. |
| 3인 실행: 모든 화면 같은 콤보, 클라이언트에서 작은 공이 같은 크기로 보임, THROWER 모드에서 틀린 사람만 페널티 | NOT_RUN | Mac 앱 3개에서 확인해야 한다. |
| iOS 실기기 | NOT_RUN | HUD 위치가 위쪽 HUD/노치와 겹치지 않는지(`LabGourdComboHud.panelAnchor`) 확인 필요. |

추가/변경한 검사:
- **EditMode** `LabGourdComboTests`(새 파일): `FourHitsInOrderAndColorSucceed`, `AWrongColorFailsAtOnceAndRemembersTheThrower`, `TheTimeLimitEndsTheComboAtItsDeadline`, `StartRejectsNonElementColors`, `WhenTheToggleIsOffNoComboEverAppears`, `ATimedOutComboPenalizesTheWholeTeamAndTheNextComboFollowsTheInterval`, `EveryDeviceSeesTheSameColorsForTheSameRound`, `AWrongColorPenalizesTheTeamOrOnlyTheThrowerAsConfigured`(TEAM/THROWER 2건), `APenalizedThrowRemembersItsSizeUntilItLandsOrMisses`, `AScaledPaintHitCoversLessAndTheNextHitIsFullSizeAgain`, `ASuccessGivesTheWholeTeamBigBallsAndEndsAPenalty`, `ASuccessEndsARunningPenalty`, `ARewardPaintHitCoversMore`
- **EditMode** `LabDeveloperConfigTests`: `GourdColorComboFieldsFormTheComboGroupAndStartOff`; 무거운 폭탄 검사는 146~149번만 읽도록 범위 고정. 개수/프로토콜을 고정한 팀원 검사들을 159개/5643으로 갱신.
