> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T05 · 개발 구슬과 입력 반복 실행

이 문서는 T05의 반복 준비·실행 절차다. 실제 실행 결과는 [VALIDATION.md](VALIDATION.md)와 해당 빌드의 증거에 기록한다. 아래 절차·예상 동작·코드의 존재는 실행 완료가 아니다. 구현 범위와 설정 근거는 [ORB_INPUT_DECISION.md](ORB_INPUT_DECISION.md)를 따른다.

## 환경과 작업 보존

실제 루트는 `/path/to/2026-C6-M10-MUSA`이다. Unity6000.5.7f1와 기존 URP17.5.0·Input System1.20.0·NGO2.13.1·Transport6.5.0을 유지한다. 개발 앱은 0.1.0/빌드7, 기존 Bundle ID `com.wolfuraark.c6prototype`, iPhone+iPad 공용·세로·IL2CPP를 사용한다.

매 실행 전 실제 루트의 AGENTS, Git diff, Config·씬·meta·사용자 변경·과거 증거 해시를 확인한다. 자동 검증은 별도 사본에서 수행하고 같은 사본이 Editor에 열려 있으면 중복 batchmode를 시작하지 않는다. 사용자 Editor 강제 종료, lock·Library 삭제, 기존 빌드·로그 덮어쓰기를 하지 않는다. 명령별 `DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer`를 사용하고 전역 설정을 바꾸지 않는다.

아래 `/private/tmp/C6_Prototype_T05_Verification`와 `Logs/T05/`는 실행 위치 예시다. 이미 사용한 사본/출력/로그라면 보존하고 새 실행 이름을 정한다. 검증 중 생성되는 모든 meta를 일괄 복사하지 않고, 최종 반영할 파일의 대응 asset과 기존 GUID를 확인한다.

## 씬 준비

Editor 메뉴 `C6 > T05 > Prepare Orb Input Scene` 또는 `C6.Editor.OrbInputBuild.Prepare`를 실행한다. Play mode에서는 중단한다. GUI의 미저장 Untitled 씬은 먼저 보존하거나 검증 사본으로 진행한다.

준비 도구는 기존 `Assets/_Project/HapioMVP/Scenes/BattleLayout.unity`와 `Config/ScreenLayoutConfig.asset`를 요구한다. 저장된 T04 씬을 별도 Preview로 읽어 `Scenes/OrbInputSmoke.unity`를 만들고 T05 HUD·Controller·입력 어댑터·Probe를 연결한다. 이미 있는 T05 씬은 유지한다. T04 화면 비율과 동일 Config GUID에 입력 필드의 기본값만 추가하며, 임의로 화면 비율을 다시 지정하지 않는다.

```sh
env DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  /Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit \
  -projectPath /private/tmp/C6_Prototype_T05_Verification \
  -buildTarget iOS \
  -executeMethod C6.Editor.OrbInputBuild.Prepare \
  -logFile /private/tmp/C6_Prototype_T05_Verification/Logs/T05/prepare.log
```

종료 코드·컴파일 오류·실제 `C6_T05_PREPARED` 로그·저장 씬 참조를 확인한다. 기존 씬·Config·meta 보존과 활성 Build Scene 변경을 따로 기록한다. Prepare 재실행은 이미 저장된 T05 씬을 덮어써 문제를 숨기는 수단으로 사용하지 않는다.

## 자동 시험

EditMode와 PlayMode를 같은 사본에서 순차 실행한다. `-runTests`에는 `-quit`를 넣지 않는다. 아래 EditMode를 PlayMode로 바꾸고 별도 XML·로그 이름을 사용한다.

```sh
env DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  /Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /private/tmp/C6_Prototype_T05_Verification \
  -runTests -testPlatform EditMode \
  -testResults /private/tmp/C6_Prototype_T05_Verification/Logs/T05/editmode.xml \
  -logFile /private/tmp/C6_Prototype_T05_Verification/Logs/T05/editmode.log
```

실제 XML의 총수·실행 수·통과·실패·skip·inconclusive·누락을 집계한다. 0개 실행, 시간 초과, XML 미생성은 성공으로 기록하지 않는다. 실패 로그를 보존하고 수정 후 새 실행 결과를 만든다. 기존 T01~T04 회귀 시험과 T05 시험의 실제 실행 여부를 함께 확인한다.

레지스트리 시험은 개발 모드·세션·소유권·종류·Raw 발사 거부·불변 ID·중복/변조·sequence·두 재료의 원자적 잠금·명시적 새 라운드/종료를 검사한다. Gesture 시험은 UI 시작, 추가 Pointer, Zone/Swipe/Drop 중재, 원본 좌표와 Clamp, 취소와 pending 수명을 검사한다.

저장 씬 PlayMode 시험은 실제 NGO Host를 시험용 포트25005로 시작하고 고정 개발 구슬을 검증한다. MouseState와 TouchState를 테스트 소유 장치에 큐잉하여 실제 어댑터 경로를 실행하고 장치를 정리한다. 이 시험은 합성 입력이며 iPhone 손가락 조작이나 두 기기 Host-Client 결과로 기록하지 않는다. 같은 포트가 사용 중이면 원인과 실행 로그를 확인하고 기존 앱을 강제로 종료하지 않는다.

## Mac 개발 빌드와 iOS 프로젝트 생성

`C6.Editor.OrbInputBuild.BuildMac`은 `macOS/C6OrbInput.app`, `ExportIOS`는 `iOS/`를 만든다. 기본 출력 루트는 검증 사본의 `Builds/T05/`다. `C6_T05_OUTPUT_ROOT`에 새 절대 경로를 주면 산출물을 분리할 수 있다. 해당 출력이 이미 있으면 빌더가 준비 작업 전에 중단하므로 새 루트를 사용한다.

빌더가 요청한 대상과 Editor의 활성 Build Target이 다르면 준비·출력 전에 오류를 내고 중단한다. GUI에서는 해당 플랫폼을 먼저 선택하고, batchmode에서는 아래 `-buildTarget`을 생략하지 않는다. 설치 URP는 활성 대상을 기준으로 렌더링 자원을 수집하므로, iOS가 활성인 채 Mac BuildPlayer만 요청하면 Mobile 자원만 포함되어 PC 렌더링에 필요한 SSAO 자원이 빠질 수 있다. 이 경우 기존 출력과 실패 로그를 보존하고 올바른 대상으로 새 경로에 빌드한다. 해결을 위해 기존 URP 에셋이나 stripping 설정을 임의로 바꾸지 않는다.

```sh
env DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  C6_T05_OUTPUT_ROOT=/path/to/2026-C6-M10-MUSA/Builds/T05-new-run \
  /Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit \
  -projectPath /private/tmp/C6_Prototype_T05_Verification \
  -buildTarget StandaloneOSX \
  -executeMethod C6.Editor.OrbInputBuild.BuildMac \
  -logFile /private/tmp/C6_Prototype_T05_Verification/Logs/T05/build-mac.log
```

iOS는 다음과 같이 별도 실행한다. 두 빌드를 같은 프로젝트에서 동시에 실행하지 않는다.

```sh
env DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  C6_T05_OUTPUT_ROOT=/path/to/2026-C6-M10-MUSA/Builds/T05-new-run \
  /Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit \
  -projectPath /private/tmp/C6_Prototype_T05_Verification \
  -buildTarget iOS \
  -executeMethod C6.Editor.OrbInputBuild.ExportIOS \
  -logFile /private/tmp/C6_Prototype_T05_Verification/Logs/T05/export-ios.log
```

`Logs/T05/build-<target>-<UTC>-<GUID>.json`의 실제 BuildReport·요청/활성 Build Target·오류·경고·출력 검증과 프로세스 종료 코드·원시 로그를 대조한다. Mac 로그의 포함 URP 에셋은 PC_RPAsset, iOS는 Mobile_RPAsset인지 확인한다. iOS는 실제 `Unity-iPhone.xcodeproj/project.pbxproj`와 `Info.plist`에서 버전0.1.0/빌드7·공용 대상·세로·전체 화면·기존 Bundle ID·기존 로컬 네트워크 목적 문구를 확인한다.

Unity export와 Xcode 앱 빌드·서명·기기 설치·실행은 각각 별도 결과다. 빌드7에서 수행하지 않은 항목은 NOT_RUN으로 남긴다. 기존 본인 Apple 개발 Team 개인 Team 선택은 유지하되 과거 빌드5의 iPhone/iPad 결과를 이번 빌드로 승계하지 않는다.

## 실제 Mac 창에서 입력 확인

Probe 인자 없이 Mac 개발 앱을 열면 자동 연결이나 자동 Fixture가 시작되지 않아야 한다. DEV HOST를 누른 뒤 `DEV HOST / P1`, Raw Yin·Raw Yang·Combined 세 개와 개발 공급 안내를 확인한다. 실제 송신자·소유자는 로그의 Host LocalClientId로 대조한다.

다음 항목은 실제 창에서 Mouse로 조작한 값과 화면 관측을 기록한다. 시험 중 RESET FIXTURE로 새 라운드와 새 ID를 공급하며, 이전 잠금이 단순 시간 경과나 Pointer Up으로 풀렸다고 해석하지 않는다.

1. 작은 Raw 드래그·놓기, 취소 후 복구, UI 버튼에서 시작한 드래그의 구슬 입력 차단을 확인한다.
2. Raw를 Attack Zone으로 보내도 발사 예약·삭제가 없는지 확인한다.
3. Raw Yin을 Yang 위로 옮겨 접촉만 한 상태와 놓은 뒤 조합 예약 상태를 구분한다. 두 재료는 LOCKED지만 레지스트리 총수는 그대로여야 한다.
4. Combined를 위쪽 Zone으로 옮겨 발사 예약 후 LOCKED를 확인한다. 실제 3D 발사나 피해·회복은 없어야 한다.
5. 충분한 수평 이동 후 전달 예약을 확인한다. 같은 ID·소유자를 유지하며 실제 상대 기기로 보내지 않는다.
6. RESET FIXTURE·END와 다시 시작하기, 창 이탈·화면 크기 변경 뒤 입력 상태를 확인한다.

상단 허수아비·HUD·Zone 문구와 구슬·하단 버튼이 잘리거나 겹치지 않는지 휴대폰 세로 비율과 태블릿에 가까운 세로 비율에서 각각 확인한다. 요청한 창 크기와 실제 화면 픽셀을 구분한다. Mac 조작 결과는 실제 iOS Touch·노치 Safe Area·성능 측정이 아니다.

## 명시적 Probe와 실제 렌더 증거

`T05Probe`는 Development Standalone 플레이어에서만 동작하며 `-c6T05ProbeDirectory <새 절대 경로>`를 명시해야 시작한다. 같은 인자로 실행한 개발 앱의 실제 NGO Host·레지스트리와 화면을 읽지만 Pointer는 Controller 메서드로 주입한다. 일반 사용자 입력이나 실물 Touch와 별도인 Debug 주입이다.

선택 인자는 `-c6T05ProbePort <포트>`와 `-c6T05ProbeQuit`다. Probe 포트 기본값은25015로, 일반 DEV HOST 기본7777과 구분한다. 인자 이름과 값은 별도 토큰으로 전달한다. 출력 폴더가 비어 있지 않으면 중단한다. 앱 실행파일 이름은 실제 번들의 Info.plist와 대조한다.

```sh
"/path/to/2026-C6-M10-MUSA/Builds/T05-new-run/macOS/C6OrbInput.app/Contents/MacOS/C6 Prototype" \
  -screen-fullscreen 0 -screen-width 390 -screen-height 844 \
  -c6T05ProbeDirectory /path/to/2026-C6-M10-MUSA/Logs/T05/probe-phone-new-run \
  -c6T05ProbePort 25015 -c6T05ProbeQuit \
  -logFile /path/to/2026-C6-M10-MUSA/Logs/T05/probe-phone-new-run.log
```

그래픽을 켠 앱에서 실행하고 `-nographics`를 사용하지 않는다. 이 명시적 Probe 실행만 창이 포커스를 잃어도 캡처를 계속하도록 설정한다. Probe는 실제 Host 준비 후 Fixture 캡처, Raw 공격 거부, 접촉과 Pointer Up Drop 구분, 새 Fixture에서 Zone 우선·1회 예약, 확정 상태 유지, 종료 정리를 검사한다.

출력은 `fixture.png`, `reserved.png`, `probe.json`이다. JSON에서 실제 빌드 GUID·시각·화면 크기·Safe Area·하단 viewport·Zone·Config·세션·소유자·Fixture ID·예약 상태·최종 라운드·종료 정리를 읽는다. 정상 종료 인자가 있으면 기록 후 결과에 따라 종료한다. 첫 오류에 중단할 수 있으므로 코드에 있는 모든 검사를 실행했다고 추측하지 않는다.

PNG 완전성 확인이나 Probe의 내부 검사 결과는 화면 품질 검토를 대신하지 않는다. 두 이미지를 실제로 열어 허수아비·HUD·구슬·LOCKED·버튼의 가림과 잘림을 검토한다. Probe 결과, 이미지 시각 검토, Mac 창의 직접 조작, 합성 Input System 시험을 별도 증거로 기록한다.

## 기록과 다음 Task

원시 로그·실행 중간 산출물은 `Logs/T05/`, 빌드는 고유한 `Builds/T05.../` 경로에 보관한다. 검토된 실행 요약·XML·실제 렌더·소스/Config/기존 에셋 해시·의도된 설정 diff만 `docs/evidence/T05/`에 연결한다. T04 문서와 이전 실행 증거를 보존한다.

보고에는 개발 Fixture, 예약만 수행한다는 범위, 컴파일·EditMode·PlayMode·실제 NGO Host·Mac Debug 주입·Mac 직접 조작·iOS export·Xcode 앱 빌드·실기기를 나눠 적는다. 실행하지 않은 항목과 사람이 해야 하는 작업은 숨기지 않는다.

T06은 실제 2D→3D 전환·Rigidbody/Collider 피격과 직접 드래그 20회 실기기 Gate를 포함한다. T05 결과로 G3 전체를 완료 처리하지 않는다. 현재 G2에 한정했던 iPad 대체 승인을 후속 전체 기기 QA에 자동 확대하지 않는다. T06은 별도 다음 요청에서 시작한다.
