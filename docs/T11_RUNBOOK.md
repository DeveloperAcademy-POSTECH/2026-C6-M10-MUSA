> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T11 실행 및 검증 절차

작성 기준: 2026-09-13. 대상 씬은 `OrbTransferBattle.unity`, 앱 빌드는 `16`이다.

이 문서는 재현 절차다. 실제 실행 수와 Compile·EditMode·PlayMode·Mac Host/Client·Unity iOS 출력·Xcode 앱 빌드/서명·설치·실기기 상태는 [T11_VALIDATION.md](T11_VALIDATION.md)에서 각각 확인한다. 이전 빌드의 PASS를 승계하지 않는다.

## 실행 전 확인

- 실제 프로젝트: `/path/to/2026-C6-M10-MUSA`.
- T11 검증용 복사본: `/private/tmp/C6_Prototype_T11_Verification`.
- Unity: `/Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app/Contents/MacOS/Unity`와 설치된 iOS 지원을 사용한다. 렌더러·패키지를 바꾸지 않는다.
- 같은 프로젝트의 Editor가 열려 있으면 중복 batchmode를 실행하지 않는다. 기존 Editor를 강제 종료하거나 Lock·Library를 삭제하지 않는다.
- 기존 Config는 `Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset` 하나다. 작업 전후 파일 해시와 실제 적용 값을 기록한다. 원본 씬·`.meta`·사용자 변경과 과거 증거를 보존한다.
- 빌드·로그·실험 출력은 매 회차 새 경로를 쓴다. 사용한 경로를 비우거나 이전 결과를 덮어쓰지 않는다.

T11은 새 씬에서 전달을 켠다. 이전 `TwoPlayerBattle.unity`는 전달이 꺼진 T10-B 모드로 유지된다. 기존 `BattleLoop.unity`와 다른 Task 씬도 보존한다.

## 씬 준비와 앱 빌드

구현 진입점은 `C6.Editor.OrbTransferBuild`다. Prepare는 저장된 `TwoPlayerBattle`의 preview에서 새 씬을 파생하고, 카메라·입력·몬스터·로비 참조를 다시 연결한다. 게임·로비·연결 서비스는 기존 것 하나씩 재사용한다. 새 씬에서 `ConfigureTransfers(true)`와 빌드16을 적용한다. 기존 경로에 다른 씬이나 잘못된 설정이 있으면 이를 덮어쓰지 않고 중단한다.

| 작업 | Editor 메뉴 | 실행 메서드 |
| --- | --- | --- |
| 씬 준비 | `C6/T11/Prepare Orb Transfer Battle` | `C6.Editor.OrbTransferBuild.Prepare` |
| Mac 앱 | `C6/T11/Build macOS Orb Transfer Battle` | `C6.Editor.OrbTransferBuild.BuildMac` |
| Unity iOS 출력 | `C6/T11/Export iOS` | `C6.Editor.OrbTransferBuild.ExportIOS` |

다음은 검증 복사본에서 실행하는 예시다. 먼저 로그 디렉터리를 준비하고 `build16-rerun-01`을 사용하지 않은 회차로 바꾼다. 각 명령을 순서대로 실행하며 Mac과 iOS는 각각 해당 active build target으로 시작한다.

```sh
C6_T11_OUTPUT_ROOT="/path/to/2026-C6-M10-MUSA/Builds/T11/build16-rerun-01" \
"/Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -quit \
  -projectPath "/private/tmp/C6_Prototype_T11_Verification" \
  -buildTarget StandaloneOSX \
  -executeMethod C6.Editor.OrbTransferBuild.BuildMac \
  -logFile "/path/to/2026-C6-M10-MUSA/Logs/T11/build16-macos-rerun-01.log"
```

```sh
C6_T11_OUTPUT_ROOT="/path/to/2026-C6-M10-MUSA/Builds/T11/build16-rerun-01" \
"/Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -quit \
  -projectPath "/private/tmp/C6_Prototype_T11_Verification" \
  -buildTarget iOS \
  -executeMethod C6.Editor.OrbTransferBuild.ExportIOS \
  -logFile "/path/to/2026-C6-M10-MUSA/Logs/T11/build16-ios-export-rerun-01.log"
```

Mac 출력은 선택한 출력 루트의 `macOS/C6Transfer.app`, iOS 출력은 `iOS/Unity-iPhone.xcodeproj`다. Builder 실행 프로젝트의 `Logs/T11/`에는 회차별 빌드 JSON이 남는다. 출력 파일 존재뿐 아니라 BuildReport 결과·오류·경고와 실제 앱의 빌드 번호를 확인한다.

Unity iOS 출력은 앱 빌드·서명·설치와 별개다. Xcode에서는 사용자가 승인한 본인 Apple 개발 Team 개인 Team을 사용하고, 필요하면 해당 작업에만 `DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer`를 지정한다. iOS Bundle ID는 `com.wolfuraark.c6prototype`, Mac 진단 앱 식별자는 `com.wolfuraark.c6prototype.t11.desktop`이다. 서명 인증서·개인 키·비밀번호를 증거 문서에 넣지 않는다.

최종 iOS `Info.plist`의 `NSLocalNetworkUsageDescription`과 `NSBonjourServices`의 `_c6hapio._udp`를 확인한다. 현재 특정 Bonjour 서비스 탐색에는 새 multicast entitlement를 추가하지 않는다. 자동 탐색과 직접 IP 연결의 시험 결과는 따로 기록한다.

## 조작과 예상 피드백

1. 두 참가자가 같은 방에 들어와 Ready를 누르고 Host가 Start를 누른다. 양쪽 초기 게임 상태의 확인 후 Playing이 시작된다. 시작 구슬0, 각 스태미나100, HP100, 기본 시간180초 규칙은 그대로다.
2. Generate로 Raw를 만든다. 구슬을 잡았을 때 확대·빛 효과를 확인한다. 하단 가운데로 옮겨 놓으면 그 자리에 머물러야 한다.
3. Yin과 Yang을 하단 원하는 위치에서 겹쳐 놓아 조합한다. 같은 음양 또는 부적합한 구슬에 내려놓으면 전달로 바뀌지 않고 조합이 거부되어야 한다.
4. 전달할 Raw 또는 Combined를 잡고 충분히 가로로 움직여 왼쪽 또는 오른쪽 가장자리에서 손을 뗀다. 거리는 화면 너비의18% 이상이고 가로 이동량은 세로 이동량의1.25배 이상이어야 한다. 가장자리 판정은 하단 화면 너비의5.5%다. 빠르게 던질 필요는 없다.
5. 들고 있는 동안에는 전달되지 않아야 한다. 손을 떼면 송신 측은 `PASSING LEFT/RIGHT`로 잠기고, Host 승인 후 `PASS CONFIRMED` 또는 `PASS REJECTED`가 표시되어야 한다.
6. Left는 수신 측 Right Edge, Right는 수신 측 Left Edge에 같은 구슬로 도착한다. `RECEIVED / LEFT/RIGHT EDGE`와 함께 한 개만 조작 가능해야 한다. 새로 잡기 전에는 자동으로 반송하지 않는다.
7. 수신한 Raw를 실제 조합 재료로 쓰고, 수신한 Combined는 상단 전투 경계로 넘겨 발사한다. 같은 ID의 Host 실제 비행·피격, HP 변화와 실제 공격자만 회복5를 확인한다. 최대 스태미나에 닿으면 회복 실증이 어려우므로 실행 로그와 공격자의 여유를 함께 확인한다.
8. 서로 다른 화면 비율에서 위·중간·아래의 전달을 확인한다. 높이는 각 화면에서 구슬 중심이 이동 가능한 범위에 대한 상대값이며 HUD·구슬·문구 여백을 제외한다. 같은 픽셀 높이를 요구하지 않는다.

Pending 중에는 새 복제 구슬을 만들지 않는다. 기존 요청의 확인을 약3초 후 조회하고 약8초까지 확정되지 않으면 오류로 세션을 종료한다. 응답이 늦었다는 이유로 같은 동작을 새 요청으로 반복하지 않는다. 다시 연결하려면 명시적으로 방을 나와 새로 참가한다.

## Mac 두 프로세스 진단

반복 실행 도구는 `tools/t11_mac_smoke.py`다. `--app`에 앱 내부 실행 파일의 절대 경로, `--output`에 존재하지 않는 새 증거 폴더를 지정한다. 새 회차를 생성하고 두 창의 결과와 동일 revision 지문까지 비교한다. 오류·예외 또는 비교 미완료는 PASS가 아니다.

`T11TransferProbe`는 개발용 Mac 앱에 명시적인 실행 인자가 있을 때만 붙는다. 평상시 실행과 iOS에는 자동 조작하지 않는다. 동일 Mac의 loopback 직접 IP 시험이므로 실기기 Touch·실제 Wi-Fi·Bonjour PASS로 기록하지 않는다.

1. 공통 회차 디렉터리 아래에 Host와 Client 증거 경로를 정한다. 두 경로는 같은 부모를 가져야 하며 각각 비어 있어야 한다.
2. Host 앱을 별도 프로세스로 실행한다. 인자는 `-c6T11ProbeDirectory <Host 경로> -c6T11Role host -c6T11Port 25126 -c6T11Quit`이다.
3. Host 경로의 `host-lobby-ready.json` 생성과 실제 로비 시작을 확인한다.
4. Client 앱을 두 번째 프로세스로 실행한다. 인자는 `-c6T11ProbeDirectory <Client 경로> -c6T11Role client -c6T11Port 25126 -c6T11Quit`이다. 두 프로세스는 동일 포트를 사용한다.
5. 높이 대응을 확인할 때 서로 다른 창 비율을 사용하고 실제 출력된 화면 크기를 기록한다. 요청한 창 크기만 보고 다른 비율로 실행됐다고 기록하지 않는다.
6. 양쪽 `result.json`, 단계별 전달 JSON·화면, 로그를 모두 대조한다.

진단은 일반 Seed로 Raw8회·Combined9회, 총17회를 전달하도록 준비되어 있다. 실제 반환된 Raw ID를 조합 재료로 고정하고, Combined의 마지막 수신자가 발사해 HP100→80과 실제 피격1회를 확인한다. Client 첫 전달의 응답을1.5초 동안 폐기와 동일 요청 중복 주입도 분리해 기록한다.

전달 전후 살아 있는 ID 집합, 종류·음양, 소유자, `transferCount`, sequence, 반대 `entrySide`, 정규화 높이를 확인한다. 중복 주입 횟수만으로 중복 처리 PASS를 기록하지 않는다. Host에서 같은 request가 최초·중복 모두 처리되었고 소유권 변경 횟수는 한 번인 실제 로그가 필요하다. 응답 누락 뒤 Query로 해제되었는지도 확인한다. 자동 포인터의 결과를 Touch 횟수로 기록하지 않는다.

## T12용 총100회 전달 절차 — 준비만

이 절차는 T12를 요청하고 대상 기기·실행 범위를 확정한 뒤 수행한다. 이번 T11에서 자동으로 시작하지 않는다. T12 정식 조건은 실제 iPhone 두 대다. 과거 확인된 가용 장비는 iPhone17과 iPad이며, G2의 iPad 대체 승인을 T12 전체 승인으로 확대하지 않는다. 두 번째 iPhone 확보 또는 iPhone+iPad 대체 범위는 T12 요청 시 확인한다.

100회는 승인된 한 방향 전달100회를 뜻한다. 실패·재시도·중복 요청을 성공 횟수에 더하지 않는다. 다음 분배로 송신자와 방향을 균등하게 포함한다.

| 실제 Sender / 요청 방향 | Raw | Combined | 합계 |
| --- | ---: | ---: | ---: |
| P1 / Left | 13 | 12 | 25 |
| P1 / Right | 12 | 13 | 25 |
| P2 / Left | 12 | 13 | 25 |
| P2 / Right | 13 | 12 | 25 |
| 합계 | 50 | 50 | 100 |

같은 ID를 P1에서 보내고 P2가 다시 P1에게 반환하는 왕복을 사용하면 준비 동작과 집계가 간단해진다. Raw와 Combined를 P1에서 정상 생성·조합해 준비한 뒤 다음 순서를 실행할 수 있다.

| 구슬 | P1 요청 → P2 반환 요청 | 왕복 횟수 | 승인 전달 수 |
| --- | --- | ---: | ---: |
| Raw | Left → Left | 12 | 24 |
| Raw | Right → Right | 12 | 24 |
| Raw | Left → Right | 1 | 2 |
| Combined | Left → Left | 12 | 24 |
| Combined | Right → Right | 12 | 24 |
| Combined | Right → Left | 1 | 2 |
| 합계 |  | 50 | 100 |

각 왕복도 두 개의 별도 승인이다. 직전 승인과 수신 상태를 확인한 다음 새 요청을 보낸다. 전투가 끝나면 다음 정상 라운드를 시작하고 새 round와 OrbId로 나머지 계획을 이어간다. 완료 전 종료를 막으려고 시간을 임의로 늘리거나 일반 전투 시험으로 가장하지 않는다. 중간 실패·연결 종료는 그 회차에서 별도로 기록하며 성공 수만 남기고 실패를 지우지 않는다.

사용자에게 100번 수동 조작을 요구하는 방식으로 진행하지 않는다. T12에서 원하면 승인된 요청 경로를 사용하는 명시적 DEV 자동 송신 기능을 별도 구현하여 반복 부담을 줄일 수 있다. 현재 T11 기능은 Mac17회 진단이며, 기기용 자동100회 도구는 아직 구현·실행되지 않았다. 자동 요청 검증을 선택하더라도 네 방향의 실제 잡기·손 떼기·조합 우선·확대·빛·수신 표시와 화면 검증은 대표 동작을 사람이 확인한다. 자동 요청 결과와 실제 Touch 결과를 구분한다.

100회의 성공은 정해진 시험 범위를 통과했다는 뜻이며 네트워크 안정성 보증이 아니다. 별도 오류 주입·연결 중단·상대 만원·이전 요청·동시 행동 시험을 대체하지 않는다.

## 기록할 근거

- 실행일, Task·AT, 빌드16 실제 식별, Unity·Xcode·OS, Config 해시, Seed, 일반 실행/오류 주입/자동 포인터 구분.
- 테스트 XML의 실제 실행 수·실패·누락, 빌드 결과와 오류, Mac 두 프로세스의 결과 및 화면 크기.
- 동일 session/round/revision의 양쪽 상태 지문과 전달 전후 살아 있는 ID 집합. 서로 다른 revision을 같다고 비교하지 않는다.
- 실제 request·송신자·구슬 종류·음양·owner·방향·sequence·전달 횟수·수신 경계·높이, 승인/거부/중복/조회 결과.
- Unity 출력, Xcode 앱 빌드·서명, 설치, 실행, 실제 화면·Touch 확인을 각각 분리한 증거.

공유할 증거는 검토한 발췌와 해시로 남기고 원시 Unity·기기 로그는 로컬 `Logs/`에 보관한다. 미실행은 `NOT_RUN`, 실제 차단은 사유와 함께 `BLOCKED`로 남긴다. 이 절차의 존재나 Mac 반복 결과를 두 iPhone 검증·T12·G5 완료로 기록하지 않는다.
