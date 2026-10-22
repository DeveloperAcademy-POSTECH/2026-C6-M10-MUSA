# C6 Physics Lab 작업 안내

이 저장소는 기존 MUSA 프로토타입을 대신할 Unity 실험실 프로젝트다. 기준 Unity 버전은 6000.6.5f1이며 실행 Scene은 `Assets/Lab/Scenes/Lab.unity`다. 이전 버전은 `pre-physics-lab-rebuild-2026-10-10` 태그와 그 이전 Git 이력에서 확인한다.

- 현재 동작은 `README.md`, 실험 범위는 `docs/LAB_SCOPE.md`, 검증 범위는 `docs/VALIDATION.md`를 먼저 확인한다. 과거 버전의 문서를 새 코드의 검증 근거로 사용하지 않는다.
- 기본 네트워크 방은 Host가 목표 인원 2~5명(기본 3명)을 정하고, 연결된 각 참가자가 로비에서 P1~P(목표 인원) 중 고유 번호를 선택한 뒤 시작한다. Host도 번호를 자유롭게 선택하며, 전투 자리와 좌우 이웃은 선택된 P번호의 순환 순서다. Development 빌드의 1인 솔로 모드는 별도 물리 조정 경로다.
- 수치 원본은 `Assets/Lab/Resources/LabConfig.asset` 하나다. 실행 중 DEV 패널은 원본이 아닌 복사본을 조정한다. 일반 방에서는 Host가 로비의 조정값을 참가자에게 전파하고 모두 확인한 뒤 시작하며, 솔로에는 로컬 복사본을 적용한다.
- Scene과 `.meta`의 참조를 보존한다. Unity에서 화면과 에셋을 변경한 뒤에는 Scene 연결, 컴파일, 해당 EditMode·PlayMode 검사를 별도로 확인한다.
- 이 저장소는 공개다. 인증 정보, 서명 파일, 개인 경로·기기 식별자, 원시 실행 로그와 빌드 산출물을 커밋하지 않는다.
- 소스 존재, 테스트 코드 존재, 과거 로컬 실행 결과와 이 브랜치에서의 검증 결과를 구분한다. 실행하지 않은 Mac·iOS·실기기 항목은 PASS로 쓰지 않는다.

## 별도 개발용 게임 흐름 (#262)

- 기존 Lab Scene과 빌드 경로는 보존한다. 새 게임 진입점은 `Assets/Hapio/Scenes/Home.unity`이며 Home·Lobby·Battle·StageResult·GameOver를 분리한다. 1~3스테이지 전투는 같은 Battle Scene을 재사용한다.
- 새 게임 코드는 `Assets/Hapio`, namespace `Hapio`다. `GameSession`이 연결·진행 데이터를 유지하고 Scene 객체는 연결·해제한다. 기존 `LabNetwork`의 게임 흐름 opt-in은 기본 OFF다.
- 새 빌드는 `Hapio/Development Build` 메뉴를 사용한다. Lab 첫 Build Settings 항목과 기존 개발용 빌드는 바꾸지 않는다. 상세 책임·실행법·종료 조건은 `docs/GAME_FLOW.md`를 따른다.

- #276 별도 게임의 고정 순서는 구미호→야광귀→지귀다. 공통 폭탄·좌우 전달 막기를 고유 패턴 추첨에 넣거나 끄지 않는다. `LabStageYokai.None`은 기존 Lab 동작을 유지한다.
- 요괴별 기본값·장식 참조는 `Resources/Hapio/StageCatalog`와 StageDefinition, 실행 중 조정은 GameStageSettings 복사본으로 관리한다. 원본 LabConfig를 자동 저장하지 않는다.
