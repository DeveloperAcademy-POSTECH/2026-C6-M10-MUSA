> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T02 직접 IP 연결 반복 절차

2026-09-12. 아래는 실행 절차다. 실제 결과는 [VALIDATION](VALIDATION.md), 버전·설정 근거는 [NETWORK_DECISION](NETWORK_DECISION.md)을 따른다. T01/G1의 설치·수정 반영 근거를 보존하며 T02와 이후 G2 시험을 별도로 기록한다.

## 준비와 보존

실제 루트는 `/path/to/2026-C6-M10-MUSA`이다. Unity 6000.5.7f1·URP 17.5.0을 유지한다. 본 프로젝트가 Editor에 열려 있으면 같은 경로에 batchmode를 추가 실행하지 않는다. Editor 메뉴를 사용하거나 `Assets`·`Packages`·`ProjectSettings`를 별도 검증 사본에 복사한다. 사본 실행 결과를 본 프로젝트의 근거로 삼을 때 관련 소스·씬·패키지 해시 일치를 남긴다.

`C6 > T02 > Prepare Direct Connection Scene`은 `Assets/_Project/HapioMVP/Scenes/DirectConnectionSmoke.unity`를 준비한다. 기존 씬은 보존하며 T02 씬만 빌드에서 활성화한다. 첫 생성 때 미저장 Untitled 씬이 열려 있으면 중단하므로 해당 씬을 강제로 닫지 않는다. `Export iOS`와 `Build macOS Connection Test`는 같은 준비 절차를 포함한다.

기본 출력은 `Builds/T02`다. `C6_T02_OUTPUT_ROOT`로 실행별 새 출력 루트를 정할 수 있다. iOS는 그 아래 `iOS/`, Mac은 `macOS/C6Connection.app`에 생성한다. 내용이 있는 출력은 준비·빌드 전에 거부하므로 이전 출력이나 Xcode 수정 내용을 삭제하지 말고 새 루트를 사용한다.

## Unity CLI

아래 `C6_T02_WORK`에는 Editor에서 열지 않은 검증 대상 경로를 지정한다. 각 명령이 끝난 뒤 종료 코드·로그를 확인하고 다음 명령을 실행한다.

```sh
C6_T02_WORK=/private/tmp/C6_Prototype_T02_Verification
C6_T02_UNITY=/Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app/Contents/MacOS/Unity
C6_T02_RUN_ID=$(date +%Y%m%d-%H%M%S)
C6_T02_LOGS="$C6_T02_WORK/Logs/T02/$C6_T02_RUN_ID"
C6_T02_OUTPUT="$C6_T02_WORK/Builds/T02/$C6_T02_RUN_ID"
mkdir -p "$C6_T02_LOGS"
```

```sh
env DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  "$C6_T02_UNITY" -batchmode -nographics -quit \
  -projectPath "$C6_T02_WORK" -buildTarget iOS \
  -executeMethod C6.Editor.DirectConnectionBuild.Prepare \
  -logFile "$C6_T02_LOGS/prepare.log"
```

```sh
env DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  C6_T02_OUTPUT_ROOT="$C6_T02_OUTPUT" \
  "$C6_T02_UNITY" -batchmode -nographics -quit \
  -projectPath "$C6_T02_WORK" -buildTarget iOS \
  -executeMethod C6.Editor.DirectConnectionBuild.ExportIOS \
  -logFile "$C6_T02_LOGS/export-ios.log"
```

```sh
env DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  C6_T02_OUTPUT_ROOT="$C6_T02_OUTPUT" \
  "$C6_T02_UNITY" -batchmode -nographics -quit \
  -projectPath "$C6_T02_WORK" -buildTarget StandaloneOSX \
  -executeMethod C6.Editor.DirectConnectionBuild.BuildMac \
  -logFile "$C6_T02_LOGS/build-mac.log"
```

별도로 설치한 Unity CLI의 Editor 연결 여부와 무관하게, 위 명령은 설치된 Editor 자체의 CLI로 실행한다.

생성할 iOS 앱 설정은 `C6-T02`, 버전 0.1.0, 빌드 번호 3, Portrait·iPhoneOnly·DeviceSDK·IL2CPP다. 기본 Bundle ID는 `com.wolfuraark.c6prototype`이다. 필요한 경우의 변경 변수는 `C6_IOS_BUNDLE_ID`이며 시험 중 임의로 바꾸지 않는다.

## 자동 시험과 두 Mac 프로세스

EditMode/PlayMode는 다음 형식으로 따로 실행한다. 시험 실행에는 `-quit`을 붙이지 않는다.

```sh
env DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  "$C6_T02_UNITY" -batchmode -nographics \
  -projectPath "$C6_T02_WORK" -buildTarget iOS \
  -runTests -testPlatform EditMode \
  -testResults "$C6_T02_LOGS/editmode.xml" -logFile "$C6_T02_LOGS/editmode.log"
```

PlayMode는 `-testPlatform PlayMode`와 별도 XML·로그 이름으로 바꾼다. 종료 0만으로 PASS로 삼지 않고 XML의 total/passed/failed/skipped와 대상 시험을 확인한다. T01의 저장된 씬 시험·명시적 로드를 유지하며 T02의 초기 UI와 실제 내보내기·실행 결과는 별도로 검증한다.

Mac 연결 시험은 같은 빌드를 **서로 다른 OS 프로세스**에서 Host와 Client로 실행한다. `DirectConnectionAutomation`은 개발 빌드에서 명시적으로 `-c6Role`을 지정할 때만 동작한다. 일반 실행은 수동 UI를 사용한다. 자동화 입력은 개발 검증용이며 수동 조작·실기기 입력의 증거가 아니다.

- `-c6Role host` / `client`, `-c6Host 127.0.0.1`, `-c6Port 7777`을 사용한다. 포트 생략 시 Session의 기본값을 참조한다.
- `-c6Scenario cycles -c6Cycles 2`로 연결·종료·명시적인 새 연결을 관측한다. 각 프로세스에 다른 `-c6Results`·`-logFile`을 지정한다.
- 같은 두 참가자 ID 집합에 Host 0과 각자의 ID가 포함되는지, 종료 후 참가자를 비우는지, Manager가 중복 생성되지 않는지 확인한다.
- 추가 접속 거부는 정원 초과라는 이유와 기존 연결 유지를 확인한다. 연결 실패·시간 초과·취소 후 수동 재시도, Host/Client 각각이 종료를 시작한 경우도 나눠 기록한다.
- 두 Mac 프로세스의 Loopback 성공은 두 iPhone·같은 Wi-Fi·Local Network 권한의 성공을 뜻하지 않는다.

반복 실행 도구는 `tools/t02_two_process.py`다. 아직 존재하지 않는 출력 폴더를 사용한다. 이 도구가 생성한 시험 Player 프로세스만 관리하며 사용자 Editor를 종료하지 않는다.

```sh
python3 /path/to/2026-C6-M10-MUSA/tools/t02_two_process.py \
  --app "$C6_T02_OUTPUT/macOS/C6Connection.app" \
  --output "$C6_T02_LOGS/two-process"
```

현재 결과는 `Logs/T02/two-process-01/summary.json`이며 전체 종료0/PASS다. 실행별 UDP 포트는 시험 도구가 비어 있는 loopback 포트를 골라 사용한다. 제품 기본 포트7777과 구분한다. 현재 추가 연결 반복은 하지 않는다.

## iOS 출력·서명·실기기

1. 실제 `iOS/Unity-iPhone.xcodeproj/project.pbxproj`와 `iOS/Info.plist`를 확인한다. 후처리 결과 `NSLocalNetworkUsageDescription`이 `Connect to another iPhone on the same Wi-Fi for the C6 cooperative prototype.`으로 설정됐는지 읽는다. 사용하지 않는 Bonjour·multicast 설정을 추가하지 않는다.
2. [IOS_RUNBOOK](IOS_RUNBOOK.md)의 Xcode 절차를 T02 출력에 적용한다. 미서명 컴파일·개인 Team 서명·설치·실행을 따로 기록한다. Unity export는 앱 빌드 성공이 아니다.
3. 사용자가 지정한 본인 Apple 개발 Team 개인 Team과 iPhone 17은 다시 선택을 요청하지 않고 사용한다. 두 번째 iPhone의 모델·iOS·대상 선택과 양쪽의 동일 빌드를 확인한다. 원시 서명 정보·기기 ID·인증서를 공유 문서에 복사하지 않는다.
4. 두 기기를 같은 Wi-Fi에 연결하고 앱을 전경에 둔다. Host 화면에서 IPv4 후보를 읽는다. 후보가 없거나 여러 개면 해당 기기의 Wi-Fi 설정에서 IP 주소를 확인한다.
5. 한쪽에서 Host를 누르고 다른 쪽에 그 IPv4·같은 포트를 입력해 Join을 누른다. 필요한 Local Network 접근 허용은 사람이 선택한다. 8초 안에 허용 조작을 마치지 못했다면 허용 후 수동으로 재시도한다.
6. 두 화면의 Connected·역할·참가자 2·같은 ID 집합을 확인하고 화면 관측과 `C6_T02_STATE` 로그를 연결한다. 한쪽에서 종료한 뒤 양쪽의 종료·수동 새 연결을 확인한다. 반대쪽에서 시작한 종료도 확인한다.
7. 실패 시 IPv4·포트, 같은 Wi-Fi, 네트워크 격리, Local Network 접근을 차례로 확인한다. 원인 미확인 실패를 권한 거부로 단정하지 않는다.

## 기록과 다음 작업의 경계

현재 자동 시험50개와 실제 두 Mac 프로세스 시험은 PASS다. iPhone17→Mac LAN 연결과 수동 재연결도 확인했다. 최초 권한 알림의 표시·거부 복구를 독립 시나리오로 검증한 것은 아니다. 실행한 사실만 VALIDATION에 반영한다. 두 iPhone 시험은 T03과 함께 수행할 준비이며 현재 **NOT_RUN**이다. G2는 T03의 두 iPhone·100개 고유 요청·승인 수와 상태 일치까지 확인해야 통과한다. IP 연결 성공을 AT-02 자동 탐색의 PASS로 기록하지 않는다.

기록에는 Task/Build ID, 날짜, Unity·패키지, 입력 조건, 기기, 실행 수, 실패, 증거 경로를 연결한다. 원시 로그는 `Logs/T02/`, 빌드는 `Builds/T02/`에 보관하고 검토한 요약·XML·해시만 `docs/evidence/T02/`에 남긴다. 기존 사용자 변경·T01 씬·과거 XML·서명 변경을 보존한다. 다음 T03이나 Git push는 자동으로 실행하지 않는다.

사용자는 현재 직접 IP 연결 검증이 충분하다고 판단했다. T02에서는 추가 연결 반복을 하지 않는다. T03의 100건은 연결100회가 아닌 아직 구현하지 않은 공유 숫자의 요청 정합성 기준이며, 100회 수동 입력으로 고정된 요구가 아니다. 이번에는 T03을 구현·실행하지 않았다.
