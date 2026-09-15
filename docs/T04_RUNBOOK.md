> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T04 · 화면 골격 반복 준비·검증

이 문서는 T04의 반복 실행 절차다. 실제 결과는 [VALIDATION.md](VALIDATION.md)와 실행별 `docs/evidence/T04/` 증거를 따른다. 절차·코드·씬의 존재만으로 PASS를 기록하지 않는다. 화면 구성 계약은 [LAYOUT_DECISION.md](LAYOUT_DECISION.md)를 참조한다.

## 준비와 원본 보존

실제 개발 루트는 `/path/to/2026-C6-M10-MUSA`이다. Unity6000.5.7f1, 기존 URP17.5.0·패키지와 iPhone+iPad 공용 대상을 유지한다. 작업 전 AGENTS, Git diff, Config·씬·meta·사용자 변경과 이전 증거의 해시를 기록한다.

자동 실행에는 별도 검증 사본을 사용한다. 아래 `/private/tmp/C6_Prototype_T04_Verification`는 예시이며 해당 경로를 이미 사용했다면 기존 결과를 보존하고 새 사본/실행 경로를 정한다. Assets·Packages·ProjectSettings와 필요한 meta를 그대로 복사한다. 원본이나 같은 검증 사본이 Editor에 열려 있으면 중복 batchmode를 실행하지 않는다. Editor 강제 종료·lock 또는 Library 삭제·사용자 변경 되돌리기를 하지 않는다.

Unity 실행파일은 `/Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app/Contents/MacOS/Unity`다. Xcode 개발자 경로는 명령별 `DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer`로 지정한다. 전역 개발자 경로를 바꾸지 않는다.

## 저장 씬 준비

Unity 메뉴 `C6 > T04 > Prepare Battle Layout Scene` 또는 `C6.Editor.BattleLayoutBuild.Prepare`를 실행한다. Play mode에서는 중단하고 GUI에서 미저장 Untitled 씬을 대체하지 않는다. 필요하면 사용자 작업을 보존한 검증 사본으로 진행한다.

저장 대상은 `Assets/_Project/HapioMVP/Scenes/BattleLayout.unity`, 비율 원본은 `Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset`이다. 기존 대상 씬과 Config·재질이 있으면 유지한다. 구조를 변경해야 한다면 기존 씬을 삭제해 재생성하지 말고 변경을 검토해 필요한 참조만 수정한다.

예시 실행은 아래와 같다. 매 실행 로그 경로는 사용하지 않은 새 위치로 지정한다.

```sh
env DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  /Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit \
  -projectPath /private/tmp/C6_Prototype_T04_Verification \
  -buildTarget iOS \
  -executeMethod C6.Editor.BattleLayoutBuild.Prepare \
  -logFile /private/tmp/C6_Prototype_T04_Verification/Logs/T04/prepare.log
```

종료 코드, 컴파일 오류, `C6_T04_PREPARED`, 실제 저장 씬/Config 참조를 함께 확인한다. 이후 Prepare를 다시 실행해 기존 씬·Config·재질 해시와 중복 객체가 유지되는지 확인한다. 테스트·빌드가 실행 중일 때 검증 사본의 meta를 일괄 복사하지 않는다. 최종 파일을 원본에 반영할 때는 이번 Task 파일과 의도된 설정만 선택하고 기존 변경·증거 해시를 대조한다.

## 자동 시험

EditMode와 PlayMode는 같은 검증 사본에서 순차 실행한다. **`-runTests`에는 `-quit`를 넣지 않는다.** Unity Test Runner가 결과 XML을 저장하고 종료하도록 한다. 예시의 EditMode를 PlayMode로 바꾸고 별도의 XML·로그 경로를 사용해 각각 실행한다.

```sh
env DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  /Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /private/tmp/C6_Prototype_T04_Verification \
  -runTests -testPlatform EditMode \
  -testResults /private/tmp/C6_Prototype_T04_Verification/Logs/T04/editmode.xml \
  -logFile /private/tmp/C6_Prototype_T04_Verification/Logs/T04/editmode.log
```

T04 시험과 기존 T01~T03 회귀 시험의 실제 실행 총수·통과·실패·skip·inconclusive를 XML에서 집계한다.0개·타임아웃·XML 미생성은 PASS가 아니다. 실패가 있으면 해당 로그·XML을 보존하고 수정 후 새 실행 기록을 만든다.

T04 확인 범위는 다음과 같다.

- 저장 씬의 Config 참조와 Base 카메라2개, 분리된 Culling Mask, RenderTexture/Stack 부재, AudioListener1개.
- 복제한 시험 Config의 비율을0.55→0.65로 바꿨을 때 카메라와 실제 입력 경계가 함께 바뀌는지 확인. 원본 Config는 수정하지 않는다.
- 하단 내부 좌표의 XY 평면 변환·화면 왕복, 상단 및 배제되는 경계 좌표의 거부.
- 실제 화면 Safe Area를 쓰는 HUD, 전체 화면을 분할한 카메라, 미연결 수치·비활성 생성 버튼·장식 입력 통과.
- 씬을 다시 불러온 뒤 HUD·Canvas·EventSystem·AudioListener가 중복되지 않고 게임·네트워크 상태를 시작하지 않는지 확인.

## iOS 프로젝트 생성과 Mac 빌드

`BattleLayoutBuild.ExportIOS`와 `BattleLayoutBuild.BuildMac`은 각각 `iOS/`, `macOS/C6Layout.app`을 출력한다. 기본 루트는 검증 사본의 `Builds/T04/`이며, 실제 프로젝트의 산출물 폴더로 분리하려면 `C6_T04_OUTPUT_ROOT`에 절대 경로를 지정한다. 해당 출력이 비어 있지 않으면 도구가 중단하므로 매 재빌드에는 새 루트를 사용한다.

```sh
env DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  C6_T04_OUTPUT_ROOT=/path/to/2026-C6-M10-MUSA/Builds/T04-v2 \
  /Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit \
  -projectPath /private/tmp/C6_Prototype_T04_Verification \
  -buildTarget iOS \
  -executeMethod C6.Editor.BattleLayoutBuild.ExportIOS \
  -logFile /private/tmp/C6_Prototype_T04_Verification/Logs/T04/export-ios.log
```

Mac은 별도 실행에서 `-buildTarget StandaloneOSX`, `-executeMethod C6.Editor.BattleLayoutBuild.BuildMac`, 별도 로그 경로를 사용한다. 같은 검증 사본의 Unity 빌드를 동시에 실행하지 않는다.

`Logs/T04/build-<target>-<UTC>-<GUID>.json`에 실제 BuildReport 결과·오류·경고·소요 시간·출력·빌드6이 기록된다. 종료 코드와 원시 로그도 대조한다. iOS에서는 실제 `Unity-iPhone.xcodeproj/project.pbxproj`, `Info.plist`를 읽고 버전0.1.0/빌드6, iPhone+iPad 대상, 세로·전체 화면, 기존 Bundle ID, 로컬 네트워크 목적 문구를 확인한다.

Unity iOS export는 Xcode 앱 빌드·서명·설치·실기기 실행과 별도다. T04 완료 기준은 iOS용 프로젝트 재생성이며, 이번 실행에서 앱 빌드나 실기기를 수행하지 않았다면 해당 항목은 NOT_RUN으로 남긴다. 추가 앱 빌드가 필요하면 기존 본인 Apple 개발 Team 개인 Team 선택과 [IOS_RUNBOOK](IOS_RUNBOOK.md)을 따른다.

## 실제 Mac 렌더 확인

먼저 개발 Mac 앱을 실제로 실행해 상단 허수아비와 배경, 하단 빈 구슬 예정 영역, 경계선, HUD의 표시를 확인한다. HP·Stamina는 `-- / --`, Team Time은 `--:--`, 연결은 `OFFLINE`, Generate는 `NOT WIRED`/비활성이어야 한다. UI 표시가 게임 수치로 변하거나 자동으로 연결을 시작해서는 안 된다.

명시적 캡처 인자는 `-c6CaptureDirectory <새 절대 경로> -c6CaptureQuit`다. 예시는 다음과 같다. Mac 앱 실행파일 경로는 빌드된 `Info.plist`의 실행파일 이름과 대조한다.

그래픽을 켠 개발 Standalone 앱에서 실행하며 **이 렌더 캡처 실행에는 `-nographics`를 넣지 않는다.** `T04Capture`가8프레임과 EndOfFrame 이후 캡처를 요청하고10초 안에 PNG 완료를 확인한다. `render.png` 또는 `render.json`이 이미 있으면 덮어쓰지 않으므로 새 캡처 디렉터리를 사용한다.

```sh
"/path/to/2026-C6-M10-MUSA/Builds/T04-v2/macOS/C6Layout.app/Contents/MacOS/C6 Prototype" \
  -screen-fullscreen 0 -screen-width 390 -screen-height 844 \
  -c6CaptureDirectory /path/to/2026-C6-M10-MUSA/Logs/T04/render-phone-portrait \
  -c6CaptureQuit \
  -logFile /path/to/2026-C6-M10-MUSA/Logs/T04/render-phone-portrait.log
```

`render.png`와 `render.json`, 실제 로그를 확인한다. `-c6CaptureQuit`가 있으면 결과 기록 후 성공0/실패1로 종료하며, 그 인자가 없으면 앱을 계속 실행한다. 인자를 전달한 크기와 실제 캡처 해상도가 같은지는 결과에서 확인한다. Retina 배율이나 창 크기 차이가 있을 수 있으므로 요청한390×844를 관측값으로 대신 적지 않는다. iPad에 가까운 세로 종횡비도 별도의 실제 Mac 실행·새 캡처 폴더로 확인한다. 화면에는 허수아비·HUD 가림, 두 영역의 틈이나 덮임, 텍스트 잘림, 장식이 조작 예정 영역을 가리는지 살핀다.

캡처는 개발 앱의 실제 렌더 증거이며 목업이 아니다. 이미지·실행 로그·화면 크기·Safe Area·카메라 영역·빌드·Config를 연결해 보관한다. Mac 창에서의 결과를 iPhone/iPad 실행이나 Touch·FPS 검증으로 표시하지 않는다.

캡처 JSON의 `result`는 PNG 저장 결과만 뜻한다. 이미지가 만들어졌다는 사실에서 레이아웃 품질을 추정하지 말고 실제 `render.png`를 직접 검토한 화면 결과를 따로 기록한다.

## 증거 정리와 다음 단계

원시 로그와 캡처 작업 파일은 `Logs/T04/`, 산출물은 `Builds/T04/`에 별도 보관한다. 검토된 XML·렌더 이미지·실행 요약·소스/에셋 해시·설정 diff만 `docs/evidence/T04/`에 연결한다. 과거 T03-iPad 결과와 이번 실행을 구분하고 T04의 Compile·EditMode·PlayMode·실제 Mac 렌더·iOS export·Xcode 앱 빌드·실기기를 각각 기록한다.

화면·입력의 필수 실기기 확인은 통합본의 T06에서 수행한다. T04 완료로 T06 또는 G3 전체를 PASS 처리하지 않는다. T05 구슬 모델·Touch·Gesture 중재는 다음 요청 후보이며, 이번 절차를 마쳤다고 자동 시작하지 않는다.

현재 성공 산출물은 `Builds/T04-v2/`다. 위 예시 경로는 이미 사용했으므로 다시 실행할 때 새 출력·로그·캡처 경로를 선택한다. 원본 `Builds/T04/`와 `Logs/T04/render-phone-v1/`은 HUD가 머리를 가렸던 수정 전 렌더 기록이다. Mac 캡처가 창 비활성으로 대기하면 해당 앱 창을 전경으로 선택한다. 캡처 완료 후 명시적 Quit 인자에 따라 정상 종료한다.
