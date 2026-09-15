> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# P1 직접 열기와 실행 안내

새 씬은 **PhysicsBattle**, 빌드 식별자는 **21**이다. 이전 `BUILD_README.md`는 빌드20의 상세 안내로 보존한다. 이번 새 씬의 실행·설정은 이 문서를 사용한다.

## Unity에서 열기

1. Unity Hub에서 데스크탑의 `C6_Prototype`을 Unity **6000.5.7f1**으로 연다. 이미 열려 있으면 같은 Editor를 사용한다.
2. 편집 중인 씬이 있다면 저장할지 직접 판단한 뒤, Project 창의 `Assets → _Project → HapioMVP → Scenes → PhysicsBattle`을 연다.
3. Hierarchy의 `P1PhysicsBattle`에서 T09 Battle Controller의 **Orb Physics Enabled**가 켜져 있는지 확인한다. 기존 씬은 이 값이 꺼져 있다.
4. 몬스터는 Hierarchy의 **BenchmarkMonster**를 펼쳐 Visual과 Hitbox가 분리된 것을 확인한다. Scene 뷰에서 Hitbox를 선택하면 실제 맞는 상자 범위를 볼 수 있다.
5. Project 창의 `Config → ScreenLayoutConfig`를 선택한다. **P1 · Flat orb board**가 구슬 물리, **P0 · Benchmark monster**가 표적 규격이다. 수치 비교는 Play를 끝낸 뒤 Config를 바꾸고 다시 실행한다.

이미 새 씬·Prefab이 포함돼 있으므로 매번 생성 메뉴를 누를 필요는 없다. 재현이 필요하면 **C6 → Next Phase → P1 → Prepare Physics Battle**을 사용한다. 이 메뉴는 기존 새 씬·Prefab을 덮어쓰지 않고 검사한다. 메뉴를 눌러도 게임이 자동 실행되거나 테스트 PASS가 기록되지는 않는다.

## 두 참가자로 확인

이번 씬은 기존 2인 방 구조를 유지한다. Unity Editor 한 개와 새 Mac 앱 한 개를 사용하거나 두 휴대기기에 같은 빌드를 설치한다.

1. 한쪽에서 **CREATE ROOM**, 다른 쪽에서 발견한 방의 **JOIN**을 누른다. 발견이 안 되면 같은 네트워크의 Host 주소로 직접 참가할 수 있다.
2. 양쪽에서 **I'M READY**, Host에서 **HOST START**를 누른다.
3. **GENERATE / 20**으로 Raw를 만든다. 정상 시작은 구슬0개·스태미나100이다.
4. 구슬을 하단 빈 공간 쪽으로 밀듯이 드래그한 뒤 놓는다. 움직임·반발·감속·재잡기를 확인한다.
5. Yin과 Yang이 자연히 부딪히면 그대로 남는다. 직접 잡아 서로 겹친 뒤 놓으면 COMB가 된다. 같은 음양은 합쳐지지 않는다.
6. 좌우 전달은 기존처럼 가장자리에서 손을 놓아 실행한다. 받은 구슬은 정지 상태로 반대쪽에 들어온다.
7. 이번 P1의 COMB 공격은 아직 상단 경계를 통과하면 발사되는 기존 방식이다. **손떼기의 방향·세기를 이용한 3D 투척은 후속 P2**다.

## 새 씬 빌드

- Mac: Build Profiles에서 macOS를 활성화한 뒤 **C6 → Next Phase → P1 → Build macOS Physics Battle**.
- iPhone/iPad: iOS를 활성화한 뒤 **C6 → Next Phase → P1 → Export iOS**. Xcode의 Team·기기 선택·서명·설치는 기존 [상세 빌드 안내](../BUILD_README.md)의 해당 절을 따른다.
- 기본 출력은 `Builds/NextPhase/P1/`의 macOS 또는 iOS다. 이미 출력이 있으면 보존을 위해 다시 쓰지 않는다. 새 출력이 필요하면 기존 앱을 별도 이름으로 보관하거나, 명령 실행 시 `C6_P1_OUTPUT_ROOT`를 새로운 폴더로 지정한다.
- 앱 생성, 서명, 실제 설치와 손가락 조작은 서로 다른 결과다. 이번 실제 실행 범위는 [P1 검증](P1_VALIDATION.md)을 확인한다.

Unity 자동 확인은 새 물리 18개를 포함한 기존 EditMode/PlayMode 검사와 Mac 별도 두 프로세스 실행으로 수행한다. Mac 검사 도구는 명시적인 개발 인수를 줬을 때만 동작하며, 평소 앱 실행·Editor·iOS에서 자동 시험이나 무료 구슬 공급을 시작하지 않는다.
