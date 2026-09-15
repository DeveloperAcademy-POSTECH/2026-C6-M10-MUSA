> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# P2 빌드·실행 안내

Unity Hub에서 데스크탑 `C6_Prototype`을 Unity **6000.5.7f1**로 연다. 패키지나 렌더러를 업그레이드하지 않는다. 기본 빌드 절차는 기존 [직접 빌드 README](../BUILD_README.md)를 따르되 현재 대상은 아래 P2 씬이다.

1. **C6 → Next Phase → P2 → Prepare Release Throw Battle**을 선택한다. 이미 저장된 씬은 덮어쓰지 않고 검사한다.
2. `Assets/_Project/HapioMVP/Scenes/ThrowBattle.unity`를 연다. 기존 PhysicsBattle은 빌드21의 동작을 유지한다.
3. Play 또는 P2의 Build Mac / Export iOS 메뉴로 실행한다. 활성 빌드 대상을 Mac 또는 iOS로 먼저 선택해야 한다.
4. 기본 출력은 `Builds/NextPhase/P2/`다. 이전 출력이 있으면 새 출력 경로를 지정하며 기존 결과를 지우지 않는다. `C6_P2_OUTPUT_ROOT` 환경 변수로 별도 경로를 지정할 수 있다.
5. iOS 출력 뒤 Xcode의 Unity-iPhone 대상으로 개인 개발 Team과 실제 연결 기기를 선택해 빌드한다. 생성된 Xcode 프로젝트를 열거나 앱이 빌드된 사실만으로 기기 시험 완료를 기록하지 않는다.

최종 검증 출력은 `Builds/NextPhase/P2/build22-final/`에 보관하며, iOS 서명 앱은 그 아래 `C6Prototype.app`이다.

## 실제 두 기기에서 짧게 확인할 내용

같은 Wi-Fi에서 iPhone CREATE ROOM → iPad FIND ROOMS·JOIN → 양쪽 I'M READY → Host START로 시작한다. Generate와 직접 Yin+Yang 조합으로 Combined를 만든다.

- 하단 구슬을 밀어 관성·반발·감속을 확인한다. 자연 접촉으로 합쳐지지 않아야 한다.
- Combined를 상단으로 올린 채 멈추면 들고 있는 표시가 유지되어야 한다. 그대로 놓으면 발사되지 않고 하단으로 돌아온다.
- 다시 상단으로 위쪽 움직임을 주면서 손을 놓는다. 방향과 빠르기에 따라 실제 곡선 궤적·명중 또는 빗나감이 달라지는지 본다.
- 표적에 실제 명중한 경우만 양쪽 HP가20 줄고 공격자에게5가 회복되어야 한다.

이 절은 확인 절차이며 실행 증거가 아니다. [검증 기록](P2_VALIDATION.md)의 실제 결과와 구분한다. 무작정100회 반복하는 대신 한 번의 조작에서 문제가 생긴 동작과 빌드를 기록한다.
