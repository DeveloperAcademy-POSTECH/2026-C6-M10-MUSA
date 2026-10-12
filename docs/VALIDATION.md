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

## 생성 확률·아이템 공통 규칙·main(#85) 병합 — 2026-10-12

기준은 Unity **6000.6.5f1**, `feat/#84-orb-generation-items` 브랜치에서 main의 #85(오행 속성)를 병합한 소스다. 이 변경은 GENERATE 확률(DEV 6칸), 아이템 4종의 잠금·아이콘·배지·15초 수명, 이웃 전달마다 잠금 1 감소, 투사체의 속성 색 유지, DEV 48칸을 포함한다. 아이템 효과와 더블 탭은 범위 밖이다.

| 항목 | 상태 | 이번 변경의 근거·범위 |
| --- | --- | --- |
| C# 컴파일 (Runtime·EditMode·PlayMode 어셈블리) | PASS | Unity 6000.6.5f1 내장 Roslyn 컴파일러로 Editor가 만든 컴파일 응답 파일을 사용해 프로젝트 밖 임시 경로에 빌드했다. 오류 0. Unity Editor Console 확인과는 별도다. |
| 테스트 시드 계산 | 참고 | 결정적 추첨을 Python으로 재현해, 수정·추가한 EditMode 테스트의 고정 시드가 필요한 결과(음·양 쌍, 잠금 2·3·4, 다섯 속성 분포)를 내는지 확인했다. 테스트 실행 결과를 대신하지 않는다. |
| EditMode | NOT_RUN | 병합 후 최종 소스의 GUI Test Runner 기록이 아직 없다. 중간 단계(생성 확률 커밋)에서는 EditMode 통과를 사용자가 확인했다. |
| PlayMode | NOT_RUN | 병합 후 최종 소스의 실행 기록이 아직 없다. |
| DEV 솔로: 확률 6칸·합 100 거부·아이콘·잠금 배지·15초 수명 | NOT_RUN | 화면에서 아이콘이 실제로 그려지는지 포함해 확인이 필요하다. |
| Mac 3개 실행본: 이웃 전달마다 잠금 1 감소·배지 숫자 동기화·투사체 속성 색 | NOT_RUN | 솔로에는 이웃이 없어 해금을 확인할 수 없다. |
| iOS 실기기 | NOT_RUN | 이번 변경에서는 기기를 사용하지 않았다. |
