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
