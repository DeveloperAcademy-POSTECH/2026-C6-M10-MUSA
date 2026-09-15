> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T09 실행 절차

<!-- C6:T09:INPUT13 -->
## 현재 T09 / G4 · 실기기 Core Loop 완료

빌드13의 iPhone17 DEV SOLO에서 정상 생성·같은 음양 거부·터치 조합5회·실제 명중5회·각회복+5·Victory/Defeat·고정 결과·Retry·재시작 첫Generate Raw1/비용20을 사람 확인과 기기 로그로 대조했다. **T09와 G4는 한 기기의 명시적 개발 모드 범위에서 PASS다.**

기존 소스379개는 변경 없이 보존했다. 빌드13의 자동475개(Edit401/Play74)와 Mac 두 화면/두참가자/실제180초 근거를 재확인했으며 이번에는 새 구현·재빌드·자동시험 반복을 하지 않았다. 두 기기 최종 통합·G5/T12·전체 MVP 완료는 별도다.

[현재 G4 검증](T09_G4_VALIDATION.md) · [원본 비공개 자료: 실기기 완료 근거 — 공개 요약](VALIDATION_SUMMARY.md). 다음 요청 하나는 **T10-A 2인 Lobby·방 탐색·Ready**이며 아직 시작하지 않았다. 아래는 이전 작성 시점의 기록을 보존한 것이다.
<!-- C6:T09:INPUT13:END -->

실제 루트는 `/path/to/2026-C6-M10-MUSA`, 씬은 `Assets/_Project/HapioMVP/Scenes/BattleLoop.unity`, 설정은 기존 `ScreenLayoutConfig.asset` 하나다. 기존 Editor가 열려 있으면 같은 프로젝트에 batchmode를 추가하지 않고 검증 복사본을 사용한다. 설치 Unity6000.5.7f1을 유지한다.

## 준비·시험·빌드

`C6.Editor.BattleLoopBuild.Prepare`는 기존 CombinationSmoke 씬을 보존한 별도 BattleLoop를 준비한다. 동일 씬이 이미 있으면 덮어쓰지 않는다. 원래 공유 Config의 다른 값을 변경하지 않고 T09 duration180/decay1 기본 필드를 저장한다.

설치 Unity의 `-runTests -testPlatform EditMode`와 `PlayMode`를 각각 실행하고 XML의 실제 실행 수·실패·skip을 확인한다. 물리·UI PlayMode에는 그래픽 환경을 사용한다. 새 터치 회귀 시험은 인접한 두 Raw의 중심까지 여러 Move로 드래그하며 긴 가로 Swipe가 조합으로 바뀌지 않는지도 확인한다. 이 배치는 시험용 geometry Fixture다.

Mac은 `-buildTarget StandaloneOSX -executeMethod C6.Editor.BattleLoopBuild.BuildMac`, iOS는 `-buildTarget iOS -executeMethod C6.Editor.BattleLoopBuild.ExportIOS`다. `C6_T09_OUTPUT_ROOT`로 **아직 비어 있는** 새 출력 경로를 지정한다. 이전 빌드11은 Builds/T09 및 기록기 수정 Mac은 Builds/T09-probe2에 보존한다. 터치 수정 빌드12 출력은 Builds/T09-build12다.

iOS export 후 Xcode26.6의 Unity-iPhone scheme/Debug/iphoneos를 `CODE_SIGN_STYLE=Automatic DEVELOPMENT_TEAM=YOUR_TEAM_ID`로 빌드한다. `DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer`를 해당 명령에만 지정한다. 전역 SDK·팀·Bundle ID를 바꾸지 않는다. 앱 ID는 com.wolfuraark.c6prototype이며 실제 앱 build number·Portrait·UIDeviceFamily1,2·최소15.0·LocalNetwork문구·서명·프로비저닝을 검사한다. 사용자가 선택한 iPhone17에 설치하고 기기 콘솔을 붙여 실행한다. 기기가 잠겼다면 사용자에게 잠금 해제를 요청한다.

## 자동 Mac 실행

개발 빌드에서 명시적 인자가 있어야만 Probe를 실행한다. 일반 실행·iOS에는 자동 입력이 없다.

- 한 기기 정상 한 판: `-c6T09ProbeDirectory <빈 절대 폴더> -c6T09Role solo -c6T09Port 25101 -c6T09ProbeQuit`.
- 실제 180초: 위 인자에서 Role을 clock, Port를25103으로 지정한다. 단축 시간이 아니며 약180초를 실제로 기다린다.
- 기본 2인: `-c6T09NetworkProbeDirectory <빈 절대 폴더> -c6T09NetworkRole host|client -c6T09NetworkPort 25102 -c6T09NetworkProbeQuit`. Host를 먼저 실행하고 host-lobby-ready.json이 생긴 뒤 Client를 시작한다.

각 PNG/JSON은 AUTOMATED·physicalDevice=false와 실제 빌드GUID를 기록한다. 결과만 보지 말고 source manifest, raw log hash, 실행 종료 코드, 비용20 영수증, 조합 새 ID와 실제 충돌·+5, 결과/Reset을 함께 확인한다. 두 참가자의 nonce는 서로 다르지만 같은 결과 snapshot의 나머지 값은 같아야 한다. 일반 Seed가 한쪽으로 치우치면 회복 후 추가 유료 생성한다. 무료 Combined나 Force Result를 증거로 쓰지 않는다.

## 실제 iPhone Core Loop

1. 시작 화면에서 DEV SOLO를 ON으로 바꾸고 HOST를 누른다. READY·0구슬·100/100·180초가 보이는지 확인한다. 기본 모드는2인이며 이 시험만 명시적1인이다.
2. HOST START 후 GENERATE를5회 눌러 한 번마다 Raw1/비용20이 적용되는지 확인한다. 가까운 YIN과 YANG을 겹쳐 놓아 COMB를 만든다. COMB를 Attack Zone으로 드래그해 실제 비행·피격·HP20감소·공격자+5(100상한)를 확인한다.
3. 일반 음양 결과에 따라 회복 후 유료 생성을 추가해 총5유효명중으로 VICTORY를 확인한다. 원한다면 같은음양 거부도 확인하되, 이를 성공한 조합으로 세지 않는다. 시간 만료로 Defeat가 나왔다면 그 결과와 원인을 그대로 기록한다.
4. 결과의 최종 HP·시간·스태미나가 고정되고 조합/생성/공격이 멈추는지 확인한다. RETRY로 구슬0·HP100·Stamina100·180초 Ready인지 확인한다. HOST START 후 첫 GENERATE가 Raw1/비용20인지 확인한다.
5. 사람의 화면·직접Touch 확인을 같은 빌드의 기기 로그와 연결한다. 이전 기기나 Mac 화면을 현재 빌드의 실기기 PASS로 승계하지 않는다. G4 완료 전 T10-A를 시작하지 않는다.

빌드11에서는 실제 조합 드래그가 전달로 먼저 잡혀 실패했다. 이 실패 원시 로그와 사람 보고를 보존하며, 수정 빌드의 실제 확인을 별도로 기록한다. 반복 횟수100회나 실제180초5회는 이번 T09의 수동 확인으로 요구하지 않는다.
