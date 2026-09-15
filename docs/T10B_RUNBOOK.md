> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T10-B 실행 및 검증 절차

작성 기준: 2026-09-13, 대상 빌드 `15`.

이 문서는 반복 가능한 실행 절차다. 명령과 시험 항목이 적혀 있다는 사실은 실행 완료를 뜻하지 않는다. 실제 Compile·EditMode·PlayMode·Host/Client·Unity iOS 출력·Xcode 빌드/서명·기기 설치·실기기 결과는 각각 증거를 확인하여 기록한다. 실행 수와 PASS 여부는 root가 확인 후 별도 검증 기록에 채운다.

## 실행 전에 확인할 것

- 실제 개발 루트는 `/path/to/2026-C6-M10-MUSA`이다. 기존 파일·사용자 변경과 과거 증거를 보존한다.
- 같은 경로의 Unity Editor가 열려 있는지 확인한다. 열려 있는 프로젝트에 중복 batchmode를 실행하지 않는다. 이번 검증용 복사본은 `/private/tmp/C6_Prototype_T10B_Verification`이다.
- Unity `6000.5.7f1`과 설치된 iOS 지원, Xcode를 사용한다. 패키지·렌더러를 업그레이드하지 않는다.
- 새 출력과 로그 경로를 선택한다. 이미 사용한 경로를 비우거나 덮어쓰지 않는다. Builder는 내용이 있는 출력 경로를 거부한다.
- Config 원본은 `Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset`이다. 빌드 전 해시와 값, 실행 후 원본 보존을 기록한다.
- iOS 서명은 사용자가 승인한 본인 Apple 개발 Team 개인 Team을 사용한다. 인증서·개인 키·비밀번호를 문서나 로그 발췌에 복사하지 않는다.

현재 설치된 iPhone 앱은 T09 빌드 13이다. 이 초안 작성 시점에서 빌드 15의 설치·실행은 `NOT_RUN`이다. 기존 앱의 화면 확인을 빌드 15 결과로 기록하지 않는다.

## 씬 준비와 빌드

현재 구현은 `C6.Editor.GameSyncBuild`에 있다. `Prepare()`는 새 `TwoPlayerBattle.unity` 씬을 준비하고 기존 씬을 보존한다. 기존 T09 씬과 참조를 복제하며 두 카메라와 몬스터 Collider를 유지한다. 저장되지 않은 씬이나 출력 충돌이 있으면 중단 사유를 먼저 해결한다.

Editor 메뉴는 현재 소스 기준으로 다음과 같다.

| 실행 | 메뉴 / 메서드 |
| --- | --- |
| 씬 준비 | `C6/T10-B/Prepare Room Lobby` / `C6.Editor.GameSyncBuild.Prepare` |
| Mac 검증 앱 | `C6/T10B/Build macOS Room Lobby` / `C6.Editor.GameSyncBuild.BuildMac` |
| Unity iOS 출력 | `C6/T10B/Export iOS` / `C6.Editor.GameSyncBuild.ExportIOS` |

아래는 검증용 복사본에서 실행하는 명령 예시다. 먼저 해당 로그 디렉터리를 만들고 `build15-rerun-01`·로그 파일명을 사용하지 않은 회차로 바꾼다. Mac과 iOS는 각각 맞는 active build target으로 실행해야 한다. 같은 복사본의 두 Unity 작업을 동시에 실행하지 않는다.

```sh
C6_T10B_OUTPUT_ROOT="/path/to/2026-C6-M10-MUSA/Builds/T10-B/build15-rerun-01" \
"/Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -quit \
  -projectPath "/private/tmp/C6_Prototype_T10B_Verification" \
  -buildTarget StandaloneOSX \
  -executeMethod C6.Editor.GameSyncBuild.BuildMac \
  -logFile "/path/to/2026-C6-M10-MUSA/Logs/T10-B/build15-macos-rerun-01.log"
```

```sh
C6_T10B_OUTPUT_ROOT="/path/to/2026-C6-M10-MUSA/Builds/T10-B/build15-rerun-01" \
"/Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -quit \
  -projectPath "/private/tmp/C6_Prototype_T10B_Verification" \
  -buildTarget iOS \
  -executeMethod C6.Editor.GameSyncBuild.ExportIOS \
  -logFile "/path/to/2026-C6-M10-MUSA/Logs/T10-B/build15-ios-export-rerun-01.log"
```

Mac 출력은 선택한 루트의 `macOS/C6Game.app`, iOS 출력은 `iOS/Unity-iPhone.xcodeproj`다. Builder는 실행한 검증 프로젝트의 `Logs/T10-B/`에 고유 이름의 빌드 증거 JSON도 남긴다. PlayerSettings와 최종 앱의 실제 빌드 번호가 15인지 따로 확인한다.

Unity의 iOS 출력은 Xcode 앱 빌드·서명·설치와 별개다. 출력된 Xcode 프로젝트를 열고 사용자가 승인한 개인 Team과 대상 기기를 확인한 뒤 빌드한다. 필요할 때 해당 작업에만 `DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer`를 지정한다. 앱의 Bundle ID·빌드 번호·기기 ID를 확인하고 설치·실행 증거를 별도로 남긴다.

최종 iOS `Info.plist`에는 `NSLocalNetworkUsageDescription`과 `NSBonjourServices`의 `_c6hapio._udp`가 있어야 한다. 특정 Bonjour 서비스 탐색을 위한 현재 구현에는 새 multicast entitlement를 추가하지 않는다. 실제 탐색 결과는 직접 IP 연결과 구분한다.

## Mac 두 프로세스 실행

`T10GameProbe`는 명시적인 개발용 Mac 실행 인자가 있을 때만 붙는다. 평상시 앱과 iOS에서 자동 조작하지 않는다. 아래 절차는 같은 Mac의 두 프로세스와 loopback 직접 IP를 사용하므로 실기기나 Bonjour 탐색의 PASS 근거가 아니다.

1. Host 앱을 새 프로세스로 실행한다. 실행 인자는 `-c6T10BNetworkProbeDirectory <비어 있는 Host 증거 디렉터리> -c6T10BNetworkRole host -c6T10BNetworkPort 25125 -c6T10BNetworkProbeQuit`이다.
2. Host 증거 디렉터리에 `host-lobby-ready.json`이 생기고 로비가 열린 것을 확인한다.
3. 같은 앱을 두 번째 프로세스로 실행한다. 실행 인자는 `-c6T10BNetworkProbeDirectory <비어 있는 Client 증거 디렉터리> -c6T10BNetworkRole client -c6T10BNetworkPort 25125 -c6T10BNetworkProbeQuit`이다.
4. Client는 실제 구현에 따라 `127.0.0.1`로 연결한다. 두 프로세스의 결과와 로그를 모두 확인한다.

Probe는 로비 Ready와 시작, 초기 게임 ACK를 잠시 보류했을 때의 입력 차단, 일반 Seed 생성과 실제 조합·발사, 결과 고정, Retry와 첫 생성, 변경하지 않은 180초 전투의 Defeat를 확인하도록 작성되어 있다. 매 단계의 JSON과 화면을 저장한다. 최종 `result.json`의 결과, 실행 오류, checkpoint, 양쪽 공통 revision의 지문을 확인한다. 파일 존재만으로 PASS로 기록하지 않는다.

## 실제 두 기기에서의 터치 확인

이 절차는 빌드 15를 두 기기에 설치한 뒤 수행한다. T12의 정식 조건은 실제 두 iPhone이다. 지금 확보한 Mac 또는 과거 G2에서 사용한 iPad의 결과는 그 범위에 맞게 따로 기록한다.

1. 두 기기를 같은 Wi-Fi에 연결하고 세로 화면으로 앱을 실행한다. 앱 이름만 보지 말고 설치 증거에서 빌드 15를 확인한다. 화면, 글자, 버튼과 Safe Area를 확인한다.
2. P1에서 방을 만들고 P2에서 방을 찾아 참가한다. 자동 탐색으로 참가했는지, 직접 IP 대체 경로인지 기록한다. 로컬 네트워크 알림이 실제로 나타난 경우에만 표시·허용 결과를 기록한다. 이미 허용된 상태를 최초 권한 시험으로 기록하지 않는다.
3. P1/P2와 두 참가자, Config 확인을 확인하고 양쪽에서 Ready를 누른다. Host가 Start를 누른다. 초기 확인 중에는 생성과 시간이 시작되지 않아야 한다. 두 초기 상태의 확인 후 같은 라운드의 Playing으로 진입해야 한다.
4. 시작 기록의 구슬 0개, 각 스태미나 100/100, 몬스터 HP 100, 기본 시간 180초를 확인한다. 초기 Ready는 짧게 지나갈 수 있으며 Playing 화면의 시간은 이미 정상적으로 감소할 수 있다. 화면의 P1/P2 자원 표시와 라운드를 함께 읽는다.
5. 각 기기에서 Generate를 한 번씩 눌러 자기 하단에 Raw 하나만 생기는지 확인한다. 이어 필요한 만큼 생성한다. 각 승인에는 비용 20이 적용되어야 한다. 시간 회복 때문에 화면의 최종 값은 단순히 버튼 수만 곱한 값보다 클 수 있으므로 비용은 Host 영수증과 함께 확인한다.
6. 각 기기에서 자기 Yin과 Yang Raw를 하단의 원하는 위치로 드래그해 조합한다. 잡은 구슬의 확대·빛 효과, 새 Combined 하나, 기존 재료 두 개의 제거를 확인한다. 같은 음양을 겹친 경우 합쳐지지 않아야 한다. 음양이 부족하면 정상 회복 후 추가 생성한다.
7. P1이 Combined를 실제 상단 전투 경계로 넘겨 발사한다. 같은 ID의 하단 구슬이 제거되고 양쪽에서 비행과 HP 100→80을 확인한다. P2도 자기 Combined로 발사하여 양쪽의 HP 80→60을 확인한다. 각 명중은 Host 실제 피격 한 번과 공격자에게만 회복 5여야 한다. 회복 최대값에 닿은 경우 실제 추가량이 5보다 작을 수 있으므로 시험 전에 공격자의 여유를 확보한다.
8. 양쪽에서 짧은 간격으로 Generate를 눌러 각자의 비용·구슬·owner가 섞이지 않는지 확인한다. 서로 다른 Local Player 자원 값을 서로 비교하지 말고, 두 화면의 P1끼리·P2끼리 비교한다.
9. 두 사람이 합계 5회 실제 명중을 완료하면 양쪽의 Victory·HP 0을 확인한다. 결과의 시간·HP·자원이 더 변하지 않고 새 입력이 적용되지 않아야 한다. 별도 정상 180초 시험에서는 시간 만료의 Defeat를 확인한다. 시간을 줄인 실행은 정상 180초 검증으로 기록하지 않는다.
10. Host가 Retry를 누르면 양쪽이 다음 라운드 Ready·구슬 0개·스태미나 100·HP 100·180초로 돌아와야 한다. 초기 확인 후 Host가 Start를 누른다. 이때 roundId가 다시 증가하지 않아야 한다. 각 기기에서 Generate를 한 번 눌러 각각 자기 구슬 하나만 생기는지 확인한다.

전달 제스처는 이번 검증 대상이 아니다. 하단에서 수평으로 드래그한 구슬을 상대에게 보내는 기능을 기대하거나 T11 완료로 기록하지 않는다.

## 비교 기준과 실패 처리

논리 상태는 같은 `sessionId/roundId/revision`의 양쪽 기록을 골라 비교한다. 수신자 nonce를 제외한 전체 상태 지문은 정확히 같아야 하며, 논리 수치 검증 허용 오차는 `1e-6`이다. 실시간 화면의 시간 차이는 **1.0초 이내**, Team HP 표시 차이는 **1.0초 × 설정된 초당 감소량 이내**로 시험 전에 고정한다. 현재 설정에서는 1 HP다.

낮은 revision, 이전 round, 잘못된 sender·owner, 초기 확인 누락, Config 불일치, 동시·중복 요청, 승인 응답과 Snapshot 도착 순서, Query 후 Pending 해제를 코드 및 Host/Client 시험에서 확인한다. 자동 시험의 구현 유무와 실행 결과는 따로 기록한다.

Pending은 새 구슬을 만들지 않은 채 Host 확인을 기다린다. 약 3초 후 동일 요청을 Query하고 약 8초까지 확정되지 않으면 오류로 세션을 끝낸다. 이를 해결하려고 동일 행동을 새 요청으로 반복하거나 로컬 구슬을 되살리지 않는다. 다시 시작할 때는 명시적으로 방을 나와 새 연결을 만든다.

## 남길 증거

- 실행일, Task, 빌드 15 식별, Unity·Xcode·OS, 기기와 역할, 연결 경로, Config 해시, Seed와 일반/Debug 여부.
- 테스트 XML의 실제 실행 수·실패·누락, 양쪽 Host/Client 결과와 오류, 공통 라운드·revision의 비교 지문.
- Unity 출력, Xcode 빌드·서명, 앱 설치·실행을 구분한 증거. 실제 화면·Touch 확인은 사용자가 확인한 내용과 원시 로그를 함께 기록한다.
- 일반 실행·고정 fixture·오류 주입·시간 단축의 구분. Mac 자동 포인터를 실기기 Touch로 기록하지 않는다.

원시 Unity·기기 로그는 로컬 `Logs/`에 보관하고 공유 전 발췌를 검토한다. 개인 키·인증서·비밀번호·라이선스 정보가 포함된 원문을 문서나 Git에 복사하지 않는다. 이 문서의 절차를 수행하지 못한 항목은 `NOT_RUN` 또는 실제 사유가 있는 `BLOCKED`로 남긴다. T12의 두 iPhone 결과는 별도 Gate 기록이 필요하다.
