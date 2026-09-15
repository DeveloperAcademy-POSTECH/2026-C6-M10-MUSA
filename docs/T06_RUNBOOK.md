> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T06 · 실제 공격 전환 반복 실행

이 문서는 T06 준비·반복 실행 절차다. 아래 명령·예상 동작을 실행 결과로 해석하지 않는다. 실제 결과는 [VALIDATION.md](VALIDATION.md), 구현 계약과 표시 조정 기록은 [T06_DECISION.md](T06_DECISION.md)를 따른다. 본문의 Mac 실행 사례는 현재 iPhone 직접 시험의 완료를 의미하지 않는다.

## 시작 전 확인과 보존

실제 프로젝트는 `/path/to/2026-C6-M10-MUSA`이다. Unity6000.5.7f1, URP17.5.0, Input System1.20.0, NGO2.13.1, Transport6.5.0과 기존 렌더러를 유지한다. AGENTS, 통합본 T06, 선행 T05의 실제 파일·시험 XML·빌드/렌더 증거, Git 변경, 단일 Config와 기존 씬·meta 해시를 먼저 확인한다.

자동 검증은 별도 사본에서 진행한다. 아래 사본·출력·로그 이름은 실행 위치 예시다. 같은 사본이 Editor에 열려 있으면 중복 batchmode를 시작하지 않고, 사용자 프로세스 강제 종료·lock/Library 삭제·이전 빌드/로그 덮어쓰기를 하지 않는다. 실패 로그도 남기고 재시도에는 새 이름을 사용한다. 명령별 `DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer`를 지정하며 전역 설정을 바꾸지 않는다.

버전은0.1.0/빌드8이다. iOS Bundle ID는 기존 `com.wolfuraark.c6prototype`, Mac은 `com.wolfuraark.c6prototype.t06.desktop`이다. iOS는 iPhone+iPad 공용, 세로, IL2CPP, 전체 화면이며 실제 export 설정을 확인한다. 서명은 사용자가 선택한 본인 Apple 개발 Team 개인 Team `YOUR_TEAM_ID`를 사용한다. 과거 빌드의 앱 실행 결과를 빌드8로 승계하지 않는다.

## 씬 준비

Editor 메뉴 `C6 > T06 > Prepare Attack Scene` 또는 `C6.Editor.AttackSmokeBuild.Prepare`를 실행한다. Play mode에서는 실행하지 않는다. GUI의 미저장 Untitled 씬은 먼저 보존하거나 검증 사본에서 작업한다.

준비 도구는 저장된 `Scenes/OrbInputSmoke.unity`와 기존 `Config/ScreenLayoutConfig.asset`를 요구한다. T05 저장 씬을 별도 Preview로 읽어 `Scenes/AttackSmoke.unity`를 만들고 T06 HUD·Controller·Pointer 어댑터·발사 기준·고정 Hit Collider를 연결한다. 실제 Host/Client 서비스는 Controller가 런타임에 구성한다. 이미 있는 AttackSmoke 씬은 덮어쓰지 않는다.

```sh
env DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  /Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit \
  -projectPath /private/tmp/C6_Prototype_T06_Verification \
  -buildTarget iOS \
  -executeMethod C6.Editor.AttackSmokeBuild.Prepare \
  -logFile /private/tmp/C6_Prototype_T06_Verification/Logs/T06/prepare-new.log
```

종료 코드·컴파일 오류·실제 `C6_T06_PREPARED` 로그·저장된 참조를 확인한다. Config 기본값 확장과 활성 Build Scene 변경을 기록하며, 기존 T05 씬을 새 동작에 맞춰 다시 저장하지 않는다. Prepare 존재나 실행만으로 테스트·앱 빌드·기기 검증을 완료 처리하지 않는다.

## EditMode와 PlayMode

같은 검증 사본에서 순차 실행한다. `-runTests`에는 `-quit`를 넣지 않는다. 아래 EditMode 실행 뒤 PlayMode로 바꾸고 각각 고유한 XML·로그를 남긴다.

```sh
env DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  /Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /private/tmp/C6_Prototype_T06_Verification \
  -buildTarget StandaloneOSX \
  -runTests -testPlatform EditMode \
  -testResults /private/tmp/C6_Prototype_T06_Verification/Logs/T06/editmode-new.xml \
  -logFile /private/tmp/C6_Prototype_T06_Verification/Logs/T06/editmode-new.log
```

XML의 실제 total·passed·failed·skipped·inconclusive와 실행 누락을 집계한다. 실행0개·타임아웃·XML 미생성은 PASS가 아니다. 기존 T01~T05 회귀 시험과 신규 T06 시험의 실행 여부를 확인한다. 컴파일 성공과 EditMode/PlayMode 성공을 분리한다.

권한 시험은 Raw 거부, 소유자·session/round/request/sequence 검증, 동일 ID 전환, 변조된 중복 요청, 승인/거부 영수증, 응답 누락 상태 조회, 실제 spawn 확인 전 hit 거부, 복수·재진입 hit의 한 번 처리, 무피해 만료, HP0·종료·새 라운드를 확인한다.

물리 시험은 실제 Rigidbody/SphereCollider 비행·고정 Collider 충돌·빠른 발사의 관통 방지·투사체끼리 충돌 차단·빗나감 수명 종료를 확인한다. 시험용 고속/짧은 수명 결과는 기본값12 units/s·3초 결과와 분리한다. 합성 MouseState/TouchState는 실제 Input System 어댑터를 통과하더라도 실물 손가락 입력이 아니다.

## Mac 빌드와 iOS 프로젝트 생성

`C6.Editor.AttackSmokeBuild.BuildMac`은 `macOS/C6Attack.app`, `ExportIOS`는 `iOS/`를 만든다. 기본 출력 루트는 검증 사본의 `Builds/T06/`이며 `C6_T06_OUTPUT_ROOT`에 고유한 절대 경로를 지정할 수 있다. 해당 출력이 이미 있으면 빌더가 중단한다. 이전 산출물을 지우고 다시 사용하지 않는다.

**`-buildTarget`을 반드시 실제 빌드 대상과 맞춘다.** 설치 URP는 활성 플랫폼을 기준으로 렌더링 자원을 수집한다. 빌더는 요청 대상과 활성 대상이 다르면 Prepare/출력 전에 거부한다. GUI에서도 대상 플랫폼을 먼저 선택한다. 이를 우회하려고 URP 에셋·stripping 설정을 교체하지 않는다.

```sh
env DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  C6_T06_OUTPUT_ROOT=/path/to/2026-C6-M10-MUSA/Builds/T06-new-run \
  /Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit \
  -projectPath /private/tmp/C6_Prototype_T06_Verification \
  -buildTarget StandaloneOSX \
  -executeMethod C6.Editor.AttackSmokeBuild.BuildMac \
  -logFile /private/tmp/C6_Prototype_T06_Verification/Logs/T06/build-mac-new.log
```

iOS는 Mac 빌드 종료 뒤 별도 실행한다.

```sh
env DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  C6_T06_OUTPUT_ROOT=/path/to/2026-C6-M10-MUSA/Builds/T06-new-run \
  /Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit \
  -projectPath /private/tmp/C6_Prototype_T06_Verification \
  -buildTarget iOS \
  -executeMethod C6.Editor.AttackSmokeBuild.ExportIOS \
  -logFile /private/tmp/C6_Prototype_T06_Verification/Logs/T06/export-ios-new.log
```

`Logs/T06/build-<target>-<UTC>-<GUID>.json`의 BuildReport·요청/활성 대상·오류·경고·outputValidation을 프로세스 종료 코드와 대조한다. 실제 Mac 앱의 Info.plist와 실행파일, iOS의 `Unity-iPhone.xcodeproj/project.pbxproj`·`Info.plist`를 확인한다. iOS 버전/빌드·Bundle ID·Device Family·최소OS·세로·전체 화면·로컬 네트워크 목적 문구를 실제 export에서 기록한다.

Unity export와 Xcode 앱 빌드·서명·설치·실행은 별개다. 실행하지 않은 단계는 NOT_RUN으로 남긴다.

## 실제 Mac 화면과 개발 입력

자동 시험 인자 없이 앱을 열면 초기 화면은 명시적 연결을 기다려야 한다. DEV HOST를 누르거나 현재 Host 주소/포트를 입력해 JOIN한다. 이전 Task의 IP를 현재 주소로 단정하지 않고 실제 인터페이스 주소를 확인한다. 일반 기본 포트는7777이며 시험용 별도 포트는 실제 실행값을 기록한다.

연결 후 개발 역할, HP100, C1~C5와 Raw1개, Attack Zone, Round/Reset/Touch 수를 확인한다. Combined를 위로 끌어 Zone에 처음 진입시키면 승인 후2D가 사라지고 상단의 같은 논리 구슬이 실제 비행·충돌한 뒤 HP가20씩 줄어야 한다. Pointer를 계속 유지·이동해도 같은 구슬이 두 번 발사되면 안 된다. Raw를 위로 움직여도 발사·삭제·HP 감소가 없어야 한다.

현재 전환 표시 기본값은 Launch Origin `(0,1.08,-4.5)`, Width3.0, Radius0.165다. Aim `(0,1.4,0)`·HP100·피해20·속도12·수명3초는 유지한다. T06 라벨은 `12 × 기존 HUD Canvas.scaleFactor` 픽셀 높이로 표시한다. 실제 해상도에서 구슬·라벨·버튼을 함께 확인하고 카메라 진단값과 실제 발사 규칙을 구분한다.

앞선 Mac v1은5회 물리 hit가 성공했지만 초기 투사체가 상단 viewport 아래에 숨는 화면 실패가 있었다. Origin Y0.55→1.08, Width3.5→3.0, Radius0.20→0.165로 바꾼 Mac v2의390×844 Probe와 중간 비행 이미지 검토는 통과했다. 최초 X35.94px 대 하단35px, 반지름21.32px 대 하단21.45px이며 구체 전체가 viewport 안에 있었다. 이 진단 결과를 다른 화면이나 실제 iPhone 결과로 승계하지 않는다.

휴대폰과 태블릿에 가까운 세로 화면에서 실제 픽셀 크기와 Safe Area를 기록하고 HUD·표적·발사 경계·구슬 라벨·하단 버튼의 가림/잘림을 확인한다. 요청한 창 크기만으로 실제 렌더 크기를 기록하지 않는다. Mac 화면 검토는 iOS 노치·Home Indicator·실제 Touch·성능 측정을 대신하지 않는다.

## 명시적 Mac Probe

`T06Probe`는 Development Standalone에서 `-c6T06ProbeDirectory`를 명시한 경우에만 실행된다. 해당 인자가 없으면 자동으로 붙거나 연결·공격하지 않는다. 새 절대 출력 경로를 사용하며 같은 경로가 파일이거나 비어 있지 않은 폴더이면 거부한다. 일반 렌더를 사용하므로 `-nographics`를 넣지 않는다. 창이 포커스를 잃어도 계속 실행하는 설정은 이 명시적 Probe에만 적용한다.

| 인자 | 값·기본값 | 의미 |
|---|---|---|
| `-c6T06ProbeDirectory` | 새 절대 경로, 필수 | 실행별 증거 폴더 |
| `-c6T06Role` | `host` 기본 / `client` / `observer` | 실제 실행 역할 |
| `-c6T06Host` | `127.0.0.1` 기본 | Client/Observer가 연결할 주소 |
| `-c6T06Port` | Host/Client25066, Observer7777 기본 | 일반 수동 시험과 구분한 실제 연결 포트 |
| `-c6T06NetworkProbe` | 값 없는 선택 플래그 | Host2회·Client3회 Debug 입력 시나리오, client 역할에는 필수 |
| `-c6T06ProbeQuit` | 값 없는 선택 플래그 | 증거 저장 후 결과에 따른 앱 종료 요청 |

인자와 값은 별도 토큰으로 전달한다. 실제 번들의 Info.plist에서 실행파일 이름을 확인한 뒤 다음처럼 단독 Host Probe를 실행한다.

```sh
"/path/to/2026-C6-M10-MUSA/Builds/T06-new-run/macOS/C6Attack.app/Contents/MacOS/C6 Prototype" \
  -screen-fullscreen 0 -screen-width 390 -screen-height 844 \
  -c6T06ProbeDirectory /path/to/2026-C6-M10-MUSA/Logs/T06/probe-phone-new-run \
  -c6T06Role host -c6T06Port 25066 -c6T06ProbeQuit \
  -logFile /path/to/2026-C6-M10-MUSA/Logs/T06/probe-phone-new-run.log
```

단독 Host 모드는 개발 구슬을 받은 뒤 Controller 메서드에 Debug Pointer를 주입하여5회 발사와 실제 Host 충돌·HP100→0을 확인하는 절차다. 첫 비행 중 캡처와 마지막 결과 캡처, 명시적 Reset에 따른 새 roundId·새 ID·HP 복원, 종료 정리를 시도한다. Pointer 서비스 호출이므로 직접 Mouse/Touch 입력 증거가 아니다.

두 Mac 프로세스의 network Probe는 각각 다른 출력 폴더·로그를 사용하고 같은 포트를 지정한다. Host에는 `-c6T06Role host -c6T06NetworkProbe`, Client에는 `-c6T06Role client -c6T06NetworkProbe -c6T06Host 127.0.0.1`을 준다. 둘 다 연결되어 참가자별6개 Fixture를 확인한 뒤 Host가2회, Client가3회 Debug 발사를 요청한다. 실제 물리·피해는 Host만 처리하고 Client의 Rigidbody는0이어야 한다. 양쪽 독립 기록의 동일 session/round·HP0·누적5회·공격자2+3을 대조한다. 같은 Mac에서의 결과를 두 실기기 시험으로 기록하지 않는다.

observer는 실제 iPhone이 Host인 직접 시험에서 Mac을 표시 전용 참가자로 연결하는 용도다. 실제 Host IP를 지정하고 `-c6T06Role observer -c6T06Host <현재 iPhone Host 주소> -c6T06Port 7777`을 사용한다. 최초 hit 전에 연결해야 하며 Pointer·발사·Reset을 주입하지 않는다. 최대20분 동안 Host의 누적20회·최소3번 Reset과 현재 결과를 기다린다.20회 수신 후 결과를 저장하고 자동으로 연결을 끊지 않으며, 사람의 Host END를 최대20분 더 기다린다. iPhone 화면·수치를 확인한 뒤 Host의 END를 누르고, 기기의 의도된 종료 로그와 observer의 연결 종료·로컬 정리 결과를 대조한다. iPhone이 Client인 시험에는 이 observer 구성을 그대로 사용하지 않는다. observer 결과도 실제 손가락 증명을 대체하지 않는다.

출력은 `fixture.png`, `result.png`, `probe.json`이며 단독 Host 모드는 `flight.png`도 생성한다. JSON은 빌드 GUID·실행 시각·역할·화면/Safe Area·설정·Fixture ID·상태 변화·요청/공격자·로컬 hit·Debug/Touch 수와 최초/중간 비행의 진단 투영값 등을 기록한다. observer는20회 결과 캡처 후 Host END를 기다리므로 그 사이에 최종 `probe.json`이 아직 없을 수 있다. 완료되지 않은 단계와 종료 정리의 성공 여부는 실제 로그·JSON·프로세스 종료 코드를 대조하고, 코드에 있는 모든 검사를 실행했다고 추정하지 않는다. 빈 terminal snapshot은 종료 화면용 기록일 수 있으므로 실제 연결·Registry·Authority·투사체·Pointer/Pending 정리와 구분한다.

PNG 저장 완료와 Probe의 내부 결과는 시각 품질 검토가 아니다. 각 이미지를 실제로 열어 HUD·표적·구슬·발사 경계의 가림/잘림과 실제 비행 장면을 확인한다. 자동 Probe 결과가 실제 iPhone20회 실기기 Gate를 채우지 않는다.

## 빌드8 iPhone 설치와 연결

현재 연결된 iPhone17의 실제 기기 식별자·OS·연결/개발자 모드 상태를 다시 읽는다. 생성된 빌드8 Xcode 프로젝트에서 기존 Bundle ID와 본인 Apple 개발 Team 개인 Team을 확인한 뒤 현재 iPhone 대상으로 앱을 빌드·서명·설치한다. 실제 완료 로그와 설치 앱의 버전/빌드를 기록한다. 프로젝트 생성 결과만으로 앱 설치를 추정하지 않는다.

iPhone을 잠금 해제하고 앱을 전경에 둔다. Mac과 같은 Wi-Fi를 사용한다. 최초 로컬 네트워크 권한 알림이 실제 표시되면 사용자가 허용하고 필요하면 JOIN을 다시 누른다. 과거 빌드에서 이미 허용되어 알림이 없을 수도 있으므로 현재 관측을 적는다.

Mac Host+iPhone Client 또는 iPhone Host+Mac Client 중 실제 실행 역할을 기록한다. iPhone이 Client이면 Host가 RESET을 실행한다. 이 조합의 결과를 두 iPhone 화면 QA나 G2에서 승인했던 iPad 대체 범위의 확대라고 기록하지 않는다. G3에서 요구하는 직접 입력 기기는 실제 iPhone이다.

## G3 · 실제 손가락20회

자동 Probe와 합성 Touch 주입을 종료한 새 개발 세션에서 진행한다. 시험 시작 전 HP100·누적 hit0·로컬 TOUCH0·Reset0을 확인한다. Mac 자동 공격과 직접 iPhone 공격을 섞지 않는다. 한 사용자의 iPhone 직접 조작과 해당 기기의 Pointer/Touch 로그를 연결한다.

1. iPhone에서 C1~C5를 하나씩 손가락으로 위쪽 Attack Zone까지 끌어 발사한다. 각 투사체의 실제 비행·충돌과 HP 감소를 기다린다. Raw도 별도로 위로 옮겨 발사되지 않고 남는지 확인하되20회 Combined 발사 수와 구분한다.
2. 첫5회 유효 피격 뒤 HP0, 누적 hit5, 해당 iPhone TOUCH5, TargetCleared를 확인한다. 원래2D 잔류·중복 발사·추가 피해가 없어야 한다.
3. Host의 RESET으로 새 roundId·새 Fixture ID·HP100을 확인한다. 누적 hit와 iPhone TOUCH는 유지해야 한다. 같은 방식으로5회를 더 실시한다.
4. 총4개 라운드에서20회를 완료한다. 기본 수치라면 중간 RESET은 최소3회다. 라운드별5회·최종 누적 hit20·해당 iPhone TOUCH20·실제 Reset 수를 기록한다. 추가/조기 Reset을 사용했다면 횟수와 이유를 남긴다.
5. 화면의 위아래 전환 위치·크기·이동 방향, 노치와 Home Indicator 주변 HUD/구슬, 버튼·추가 손가락·UI 시작 입력, 마지막 잔류 투사체와 종료 후 입력 차단을 실제 관측으로 확인한다.

observer를 함께 사용했다면20회 완료 화면과 수치를 먼저 확인한 뒤 iPhone Host의 END를 누른다. observer가 먼저 연결을 끊어 결과 화면을 NetworkError로 바꾸도록 하지 않는다. 최종 보고 저장과 종료 정리는 그 뒤 실제 결과로 확인한다.

TOUCH는 Enhanced Touch로 시작한 승인 발사 수이고 실제 hit 수는 별도다. `C6_T06_POINTER_BEGIN source=TOUCH` → `C6_T06_TOUCH_LAUNCH` → 동일 OrbId/공격자의 `C6_T06_HIT`를 기기·session/round 맥락에서 대조한다. 합성 Touch도 이 경로를 사용할 수 있으므로 카운터20만으로 실기기 완료를 선언하지 않는다. 사용자 확인에는 실제 화면·직접 조작·각 Reset 결과를 포함한다.

중복·잔류·미명중이나 입력 누락이 있으면 해당 시점의 로그와 화면을 남긴다. 자동 반복으로 부족한 직접 횟수를 채우지 않는다. 연결 해제나8초 미확인으로 NetworkError가 되면 세션을 종료하고 실행 경계를 기록한 뒤 수동으로 새 시험을 시작한다. 이전 세션의 카운터를 새 세션의 연속20회로 합산하지 않는다.

## 증거와 다음 Task

원시 로그·빌드·기기 실행 자료는 고유한 `Logs/T06/` 및 `Builds/T06.../` 경로에 보관한다. 공유할 XML·BuildReport·검토된 로그 발췌·실제 화면·Config/소스 해시·기기 실행 기록만 `docs/evidence/T06/`에 정리한다. 라이선스·서명 관련 민감 자료를 문서나 Git에 남기지 않는다.

실행 요약에는 현재 앱 빌드·Unity/패키지·Config·DEV_PHYSICS Fixture·Host/Client 역할·기기/OS·실행일·Task/부분 AT·각 결과의 증거 경로를 적는다. 보존한 T05 파일/씬/meta/이전 evidence와 의도된 기존 파일 변경은 해시와 diff로 구분한다. 실패 또는 미실행 단계를 생략하지 않는다.

G3는 현재 빌드의 실제 iPhone 직접 드래그20회와 화면/전환·피격 증거가 있어야 완료할 수 있다. 없으면 NOT_RUN, 사람이 해결해야 하는 구체적 차단이 있으면 BLOCKED와 필요한 작업을 기록한다. T06 부분 AT를 T07 Stamina 회복이나 T12 전체 Acceptance 완료로 확대하지 않는다. **T07은 별도 요청과 G3 확인 후에만 시작한다.**

## 이번 실제 실행과 절차의 차이

최종 산출물은 `Builds/T06-v3/`, iOS 빌드8이다. iPhone17 Host에서 직접20회·4라운드·RESET3회와 화면을 확인했다. 사용자가 observer 연결 전에5회를 완료하여 이를 보존하고15회만 추가 요청했다. 따라서 이번 실기기 시험은 iPhone Host 단독이며 Mac observer 절차는 NOT_RUN이다. Mac Host2+Client3 네트워크 시험은 별도 실제 두 프로세스로 통과했다. 만료·복수 충돌·관통은 실제 PlayMode에서 검증했으며 iPhone의 별도 미명중 시험은 하지 않았다.
