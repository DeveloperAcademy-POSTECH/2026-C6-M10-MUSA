> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T08 · 실행과 재검증

프로젝트 `/path/to/2026-C6-M10-MUSA`, Unity6000.5.7f1이다. 원본 Editor가 열려 있으면 중복 batch를 실행하지 않는다. 이번 실행은 `/private/tmp/C6_Prototype_T08_Verification`의 별도 사본을 사용했다. 실제 결과·최종 경로는 VALIDATION.md를 따른다.

## 자동 실행

1. 기존 Assets/Packages/ProjectSettings와 meta를 보존한 검증 사본을 준비한다. 기존 Editor·Library·lock을 종료/삭제하지 않는다.
2. 설치 Unity의 실제 활성 대상을 `-buildTarget StandaloneOSX`로 지정하고 `-executeMethod C6.Editor.CombinationSmokeBuild.Prepare`를 실행한다.
3. `-runTests -testPlatform EditMode`와 `PlayMode`를 각각 실행한다. XML 총수·실패·skip과 종료 코드를 확인한다. T08 PlayMode는 저장된 CombinationSmoke 씬을 실제로 불러온다.
4. 새 비어 있는 `C6_T08_OUTPUT_ROOT`에서 `C6.Editor.CombinationSmokeBuild.BuildMac`을 실행한다. 기존 산출물을 덮어쓰지 않는다.
5. 개발 Mac 앱의 명시적 `-c6T08ProbeDirectory`(절대·빈 폴더), `-c6T08Port`, `-c6T08Role host|client`, `-c6T08ProbeQuit`를 사용한다. 두 프로세스 시험에는 `-c6T08NetworkProbe`를 양쪽에 더하고 같은 시험 포트를 쓴다. 원래 T03 Host의7777은 사용하지 않는다.
6. 휴대폰390×844, 태블릿560×746 화면 비율의 PNG와 실제 일반 생성→조합→피격·거부·Reset 보고서를 확인한다. 두 프로세스에서는 개인별 실제 조합2회와 실제 공격2회, 총4hit·HP20·공격자별 보너스5를 확인한다. 시간 회복이 진행 중인 각자의 최종 수치까지 서로 다른 시각의 보고서에서 정확히 같다고 요구하지 않는다. 공통 인벤토리·ID·종류·라운드·HP는 일치해야 한다.
7. iOS는 실제 활성 대상을 `-buildTarget iOS`로 바꾸고 `C6.Editor.CombinationSmokeBuild.ExportIOS`를 실행한다. 생성된 Xcode 프로젝트의 버전0.1.0/빌드10·Bundle ID·세로·iPhone/iPad·Local Network 목적을 확인한다.

Probe는 명시적 개발 Mac 실행에서만 동작한다. 일반 앱·iOS에 자동 Host·자동 입력·무료 구슬을 넣지 않는다. 시험 로그/PNG는 로컬 생성 원본이며 기기 화면 캡처가 아니다.

## T09에서 사람이 확인할 항목

T08만으로 실기기 완료를 표시하지 않는다. T09 요청 후 실제 설치할 최신 빌드의 앱 빌드·서명·설치·실행을 별도로 기록한다. 선택한 개인 Apple Team은 YOUR_TEAM_ID이다.

일반 시작에 구슬이 없고 Stamina100인지 확인하고 비용20의 생성으로 Yin/Yang을 준비한다. Raw를 반대 음양 근처로 놓아 두 재료가 Combined 하나로 바뀌고, 같은 음양은 보존되는지 확인한다. Combined를 Attack Zone으로 드래그해 실제 표적 피격과 공격자 +5를 확인한다. Raw를 위로 보냈을 때는 남아 있어야 한다. Touch·가독성·Safe Area와 전체 Core Loop·승패·Reset은 해당 최신 기기 빌드에서 확인한다.

G2의 iPhone17+iPad 대체 승인은 당시 G2 범위다. T08 Mac 화면 비율을 두 iPhone 화면 QA로 확대하지 않는다.
