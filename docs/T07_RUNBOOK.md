> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T07 · 개인 Stamina와 생성 반복 실행

이 문서는 준비·실행 절차와 예상 동작이다. 명령이나 예상 수치의 존재는 실행 증거가 아니다. 실제 실행 상태는 [VALIDATION.md](VALIDATION.md), 사양 변경과 권한 계약은 [T07_DECISION.md](T07_DECISION.md)를 따른다.

## 시작 전 확인

실제 프로젝트는 `/path/to/2026-C6-M10-MUSA`이다. 루트와 하위 AGENTS, 최신 사용자 변경, 통합본 T07, 현재 사양·검증 문서, 선행 T06의 소스·시험·빌드 8·실제 iPhone 기록을 먼저 확인한다. 최신 규칙은 **일반 시작 구슬 0개, Stamina 100/100, 매 생성 Raw 1개/비용 20, 연속 자동 회복 20/3초, 유효 피격의 공격자 +5**다.

Unity 6000.5.7f1과 기존 URP·패키지·렌더러를 유지한다. 테스트는 별도 검증 사본에서 수행하고 기존 씬·meta·이전 로그·빌드·사용자 변경을 보존한다. 같은 사본이 Editor에 열려 있으면 중복 배치 실행을 시작하지 않는다. 프로세스 강제 종료, lock/Library 삭제, 이전 실패 로그 삭제로 실행 환경을 맞추지 않는다.

명령별 `DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer`를 지정하며 전역 선택은 바꾸지 않는다. 아래 사본·로그·출력 경로는 새 실행에 사용할 예시이며 실제 사용 경로와 종료 코드를 별도로 기록한다.

## 씬 준비

메뉴는 `C6 > T07 > Prepare Resource Scene`, 배치 메서드는 `C6.Editor.ResourceSmokeBuild.Prepare`다. 저장된 `Scenes/AttackSmoke.unity`와 기존 `Config/ScreenLayoutConfig.asset`가 필요하다. 빌더는 별도 `Scenes/ResourceSmoke.unity`를 만들고 T07 HUD·Controller·Pointer를 연결하며, 복사한 T06 카메라·발사 기준·고정 표적 Collider를 참조한다. 기존 T06 씬과 이미 존재하는 T07 씬을 덮어쓰지 않는다.

```sh
env DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  /Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit \
  -projectPath /private/tmp/C6_Prototype_T07_Verification \
  -buildTarget StandaloneOSX \
  -executeMethod C6.Editor.ResourceSmokeBuild.Prepare \
  -logFile /private/tmp/C6_Prototype_T07_Verification/Logs/T07/prepare-new.log
```

종료 코드, 컴파일 오류, `C6_T07_PREPARED` 로그, 저장 씬의 참조를 확인한다. 실제 설정이 iOS 버전 0.1.0/빌드 9, iOS ID `com.wolfuraark.c6prototype`, Mac ID `com.wolfuraark.c6prototype.t07.desktop`인지 기록한다. Prepare만으로 시험·플레이어 앱·기기 검증을 완료 처리하지 않는다.

## 자동 시험

EditMode와 PlayMode는 같은 사본에서 순차 실행한다. `-runTests`에 `-quit`를 함께 주지 않는다. 각 실행은 고유한 XML과 로그를 남긴다.

```sh
env DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  /Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /private/tmp/C6_Prototype_T07_Verification \
  -buildTarget StandaloneOSX \
  -runTests -testPlatform EditMode \
  -testResults /private/tmp/C6_Prototype_T07_Verification/Logs/T07/editmode-new.xml \
  -logFile /private/tmp/C6_Prototype_T07_Verification/Logs/T07/editmode-new.log
```

이 실행이 종료된 뒤 `testPlatform`을 `PlayMode`로 바꾸고 별도 파일명으로 실행한다. XML의 실제 total·passed·failed·skipped·inconclusive와 신규·기존 시험의 누락 여부를 집계한다. 0개·타임아웃·XML 미생성은 실행 성공이 아니다.

T07은 빈 시작과 100/100, 첫 요청부터 Raw 1개/20, 비용·보관 부족 거절, 중복·오래된 요청·잘못된 소유자, 정상 시간 경과·정지·최대값, 독립된 개인 자원, 일반 Seed 정책을 확인한다. 기존 첫 묶음 5개 시험을 실행했다고 적지 않는다. 실제 Host 통합 시험은 생성 결과의 2D 뷰·버튼·격자, 정상 Reset, Debug 표시, 실제 Rigidbody 충돌의 공격자 +5, 마지막 타격과 중복 hit·만료를 확인한다. 합성 입력은 실제 손가락 입력과 구분한다.

## Mac 빌드와 iOS 프로젝트 생성

Mac 메서드는 `C6.Editor.ResourceSmokeBuild.BuildMac`, iOS는 `C6.Editor.ResourceSmokeBuild.ExportIOS`다. 기본 출력은 검증 사본의 `Builds/T07/`이며 `C6_T07_OUTPUT_ROOT`로 새 절대 경로를 지정할 수 있다. Mac 앱은 `macOS/C6Resources.app`, iOS는 `iOS/`에 생성한다. 비어 있지 않은 기존 출력은 재사용하지 않는다.

**실제 대상과 활성 `-buildTarget`을 일치시킨다.** 설치 URP의 플랫폼별 렌더링 자원 수집 때문에 Mac은 `StandaloneOSX`, iOS는 `iOS`를 명시해야 한다. 빌더의 대상 불일치 검사를 우회하지 않는다.

```sh
env DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  C6_T07_OUTPUT_ROOT=/path/to/2026-C6-M10-MUSA/Builds/T07-new-run \
  /Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit \
  -projectPath /private/tmp/C6_Prototype_T07_Verification \
  -buildTarget StandaloneOSX \
  -executeMethod C6.Editor.ResourceSmokeBuild.BuildMac \
  -logFile /private/tmp/C6_Prototype_T07_Verification/Logs/T07/build-mac-new.log
```

Mac 빌드 종료 뒤 별도로 실행한다.

```sh
env DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  C6_T07_OUTPUT_ROOT=/path/to/2026-C6-M10-MUSA/Builds/T07-new-run \
  /Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit \
  -projectPath /private/tmp/C6_Prototype_T07_Verification \
  -buildTarget iOS \
  -executeMethod C6.Editor.ResourceSmokeBuild.ExportIOS \
  -logFile /private/tmp/C6_Prototype_T07_Verification/Logs/T07/export-ios-new.log
```

`Logs/T07/build-<target>-<UTC>-<GUID>.json`의 실제 BuildReport·오류·경고·대상·출력 검증과 종료 코드를 대조한다. Mac 실행파일과 Info.plist, iOS의 Xcode 프로젝트·Info.plist·로컬 네트워크 목적 문구·Device Family·Portrait·최소 OS를 확인한다.

Unity export, Xcode 앱 빌드, 서명, 기기 설치, 실제 실행은 각각 별도 단계다. 사용자 선택 서명은 본인 Apple 개발 Team 개인 Team `YOUR_TEAM_ID`다. 현재 연결된 iPhone 17과 실제 OS·UDID를 다시 확인하고 해당 빌드의 서명·설치·READY 로그를 남긴다. 비밀 키·서명 인증서를 증거에 복사하지 않는다. T06의 빌드 8 실행 결과를 T07 빌드 9로 승계하지 않는다.

## 일반 모드 확인

자동 시험 인자 없이 열면 연결을 기다려야 한다. HOST 또는 현재 Host IPv4와 Port를 입력한 JOIN으로 세션을 시작한다. 일반 포트는 7777이며, 별도 동시 시험은 충돌 없는 다른 포트를 사용하고 실제 값을 기록한다. 과거 IP를 현재 주소라고 단정하지 않는다.

1. 연결 후 `NORMAL / EMPTY START`, HP 100, 개인 Stamina 100/100, `ORBS 0 / 20`을 확인한다. 하단에 미리 생성된 구슬이 없어야 한다.
2. `GENERATE / 20`을 한 번 누른다. Raw 구슬이 정확히 1개 생기고 Host 영수증에서 비용 20이 차감되어야 한다. 첫 동작으로 5개가 나타나면 사양 불일치다.
3. 앱을 전경에 둔 채 자동 회복을 관찰한다. 값과 막대가 연속적으로 증가하고 최대 100에서 멈춰야 한다. 기본값으로 20을 회복하는 시간은 3초다. 막대의 다섯 구분선은 유지되지만 내부 값이 20 단위로 뛰어서는 안 된다.
4. 빠르게 여러 번 생성한 뒤 자원이 20 미만이면 버튼이 비활성화되고, 회복하여 비용을 충족하면 다시 활성화되는지 확인한다. 사람이 누르는 동안에도 시간이 지나므로 다섯 번 생성 뒤 표시가 정확히 0이어야 한다고 요구하지 않는다. 비용 검증은 각 Host 영수증과 함께 판단한다.
5. 보관 수가 20이면 Stamina가 충분해도 일반 Generate가 비활성화되어야 한다. 새 구슬·비용 차감이 없는지 확인한다. 20개가 서로 가리지 않고 하단 UI·Attack Zone 밖에 초기 배치되는지 실제 화면을 확인한다.
6. Raw를 Attack Zone으로 옮겨도 실제 발사·삭제·피해·피격 보너스가 없어야 한다. 아직 실제 Yin+Yang 조합과 전달은 T08 이후 범위다.
7. Host의 `RESET EMPTY`는 새 roundId와 HP 100, 빈 구슬 보관함, Stamina 100으로 되돌린다. 이전 구슬·Pending·Debug 모드가 남지 않아야 한다.

Host 앱의 실제 일시 정지·복귀를 시험할 때는 정지 구간에 해당하는 회복이 복귀 순간 지급되지 않아야 한다. 단순 Mac 창 포커스 이동은 이 일시 정지 시험이 아니다.

별도 Host/Client 시험에서는 두 앱의 실제 세션·라운드·참가자 ID·Seed를 기록한다. 각 기기의 버튼은 자신의 구슬·자원만 바꿔야 한다. Client는 Host 승인 이전에 구슬을 만들거나 자원을 계산하지 않는다. 두 참가자의 개인 값이 서로 달라도 정상이다. 같은 참가자를 가리키는 Host 기록과 Client 표시가 일치하는지 비교한다.

## 명시적 Debug Fixture와 피격 보너스

일반 생성 확인과 별도 실행 또는 별도 라운드로 구분한다. Host가 `DEBUG FIXTURE`를 누르면 새 라운드의 `DEBUG_TEST_MODE`가 표시되고, P1에는 Yin 5개, P2에는 Yang 5개가 무료 공급되어야 한다. 고정 Fixture·일반 Seed·정상 생성 영수증을 섞어 집계하지 않는다.

Debug 모드에서도 Generate는 Raw 1개/비용 20이다. 피격 보너스를 확인하려면 먼저 일반 Generate로 자원을 일부 소비한 뒤 `DEV HIT ORB`로 Combined 1개를 명시적으로 공급하고, 그 구슬을 Attack Zone으로 끌어 실제 비행·충돌시킨다. 이 무료 Combined는 T08 조합의 결과가 아니다.

같은 OrbId의 실제 hit·공격자·피해 로그와 `C6_T07_HIT_RECOVERY`의 `added`를 연결한다. 충분한 여유가 있으면 보너스는 5, 최대값 근처에서는 남은 여유만큼이다. 화면 전후 차이는 비행 중의 자동 회복과 합산될 수 있으므로 단순 차이가 정확히 5인지로 판정하지 않는다. 상대방도 자신의 시간 회복은 계속되지만 내 hit의 보너스를 추가로 받으면 안 된다.

기본 HP 100/피해 20에서 마지막 다섯 번째 유효 피격은 HP 0이 된 뒤에도 해당 공격자의 보너스가 한 번 처리되어야 한다. TargetCleared 뒤에는 시간 회복·새 생성·새 발사가 멈춰야 한다. `RESET EMPTY`는 다시 일반 모드의 빈 시작으로 돌아가야 한다.

## 화면·로그·Gate 기록

휴대폰·태블릿 비율의 실제 렌더 크기, Safe Area, 개인 게이지·소수 값·Generate/Reset 버튼·20개 격자·라벨·Debug 표시를 확인한다. 요청 창 크기와 실제 화면 크기를 구분하며, Mac 화면 확인을 iPhone 노치·Home Indicator·직접 Touch 결과로 대체하지 않는다. 자동 Probe를 썼다면 명시적 인자·역할·입력 방식·실행 파일·Config·Seed·일반/Debug 모드를 기록한다.

주요 로그는 `C6_T07_READY`, `C6_T07_ROUND`, `C6_T07_REQUEST`, `C6_T07_REPLY`, `C6_T07_DEBUG_FIXTURE`, `C6_T07_HIT_RECOVERY`, `C6_T07_TOUCH_LAUNCH`다. 재사용한 실제 물리·연결 서비스는 일부 기존 `C6_T06_*` 로그 이름을 유지한다. Task 접두사만으로 과거 빌드 증거라고 판단하지 말고 현재 실행파일·세션·라운드·타임스탬프와 연결한다.

컴파일·EditMode·PlayMode·Host/Client·렌더·iOS export·앱 빌드·서명·설치·실기기 입력을 PASS/FAIL/NOT_RUN/BLOCKED로 각각 기록한다. 이 문서 초안은 어느 항목의 실행 성공도 선언하지 않는다. 수정된 AT-04·AT-15·AT-16과 AT-11 회복 연결의 실제 범위를 기록하며 전체 T12 Acceptance와 구분한다.

정식 Core Loop의 필수 실기기 G4 Gate는 T09에서 수행한다. T07 개발 화면이나 자동 시험만으로 G4 전체 완료를 선언하지 않는다. 다음 요청 후보는 T08의 Raw Yin+Yang 1단계 조합 하나이며 자동으로 시작하지 않는다.
