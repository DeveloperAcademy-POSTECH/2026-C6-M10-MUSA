# 기본 전투의 박 표현 선택·색칠 — #166 / PR #168 통합

원본 `experiment/#153-gourd-paint`의 `5d133197`에서 흰 박 프리팹·머티리얼·셰이더와 색칠 동작을 이관한다. 기본 실행·빌드 Scene은 계속 `Assets/Lab/Scenes/Lab.unity`이며 별도 페인트 실험 Scene과 BuildSettings 항목은 이관하지 않는다.

기본 **Paint(0)**는 흰 박·색칠을 사용하고 크랙·마지막 갈라짐을 끈다. **Crack(1)**을 선택하면 기존 `LabGourdCrack`·`LabGourdFinish` 표현으로 바뀌며 HP 단계·승리 후 약 1.2초 마무리를 유지한다. Paint의 승리와 두 모드의 패배는 즉시 결과를 표시한다. 두 모드는 기존 HP·시간·개인 HP 종료 규칙, 원통 MeshCollider·Host 명중 판정·매달림·회전·피격 입자·피해 숫자·보상·구슬 조작과 현재 UI를 공유한다. 박 외형과 원통 판정의 모양은 일치하지 않는다.

## 모드 선택과 적용

`LabConfig` Inspector의 **Gourd Mode** 또는 DEV **`Gourd Mode (0 paint, 1 crack)`**에서 고른다. DEV의 80번째(인덱스 79) 정수 값이며 이전 79개 인덱스를 유지한다. 저장 에셋에 새 필드가 없으면 코드 기본값 Paint를 사용한다.

**START/RESTART SOLO**, **CREATE ROOM**, Host 로비의 **APPLY TO ROOM**, 공동 일시정지한 Host의 **APPLY & RESTART**로 나머지 설정과 함께 적용한다. Client는 Host 모드를 사용한다. 일반 결과 Retry는 모드를 유지하면서 자국을 초기화한다. Inspector·입력칸만 바꿔 진행 중 전투를 즉시 전환하지 않는다. 프로토콜 `5617`로 설정을 공유하므로 모든 기기에 같은 새 버전 앱을 사용한다.

## 표시 규칙

- Host가 승인한 속성 명중 위치에만 칠한다. Client는 기존 승인·중복 수신 차단을 통과한 타격을 표시한다.
- 구슬 외형 설정의 속성 재질 색(`_BaseColor` 또는 `_Color`)을 먼저 사용하고, 없으면 기존 공통 팔레트를 사용한다. 기본 팔레트는 화=빨강·수=파랑·목=초록·금=노랑·토=갈색이다.
- 무속성 타격은 피해·보상을 기존대로 처리하되 칠하지 않는다. 빗나감도 칠하지 않는다.
- 구형 박 표면의 로컬 좌표로 기록하므로 회전·흔들림을 따라간다. 같은 위치에 덧칠할 수 있다.
- 한 판 최대 128개이며 이후 자국은 추가하지 않고 기존 자국을 유지한다. 기본 반경은 Sphere 로컬 좌표 기준 `.15`이고 Inspector 범위는 `.05~.3`이다.
- 새 세션·라운드에서 초기화하며, 첫 승인 명중을 칠하기 전에 해당 라운드의 초기화를 수행한다. 로비 복귀·방 이탈에서도 자국을 지운다.

## 연결과 수정 지점

현재 기본 `Lab.unity`의 참조를 다시 저장하지 않는다. `LabSceneController.Awake`가 `LabGourdPaint.Attach(network)`를 호출해 기존 Target에 실행 중 표현을 연결하고 Resources의 흰 박·머티리얼을 읽는다. 초기 설정과 `ConfigApplied`에서 `ApplyMode()`를 호출해 Paint/Crack 중 하나를 활성화한다. 솔로 시작/재시작도 같은 설정 이벤트로 처리한다. 모드는 전역 static 스위치가 아닌 세션 `LabConfig`에 보관한다.

에셋은 `Assets/Lab/Resources/GourdPaint/`의 원본 GUID를 유지한다. Scene·BuildSettings·기본 Config 에셋을 Paint 연결용으로 덮어쓰지 않으며, 이전 로컬 #166의 **Apply White Gourd Paint / Validate White Gourd Paint** 도구는 현재 연결 절차에서 제거했다.

- `LabGourdPaint.cs`: `Attach`·`ApplyMode`의 표현 연결/선택, 장착·색칠·라운드 초기화·수신 구독 관리.
- `LabConfig`·`LabSceneController`·`LabNetwork.ConfigApplied`: 선택값 원본·초기 적용·솔로/Host/Client 설정 적용 연결.
- `LabGourdPaint.shader`: 구형 표면의 색 자국 표시.
- `LabTarget.ApprovedHit`: 승인된 피격 위치·속성 전달.
- `LabNetwork`: 기존 Host/Client 승인 타격 경로에서 이벤트 발행.
- `LabOrbAppearance`: 구슬과 페인트가 같은 속성 색 원본을 사용하도록 조회.

색칠 면적 계산·봉인 완료·색칠에 따른 승패 변경·재접속 후 페인트 상태 복원은 이번 범위에 포함하지 않는다. 실제 검증 결과와 미실행 항목은 `docs/VALIDATION.md`의 모드 통합 기록을 참조한다. 이전 로컬 #166의 111/111·88/88은 Scene에 Paint 참조를 직접 저장했던 버전의 기록이며 이 런타임 연결·설정 전파 경로의 PASS가 아니다. 원본 실험이나 이전 로컬 버전의 검사 결과를 이번 통합에 승계하지 않는다.
