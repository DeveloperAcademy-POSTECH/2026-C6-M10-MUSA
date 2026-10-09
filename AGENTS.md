# C6 Physics Lab 작업 안내

이 저장소는 기존 MUSA 프로토타입을 대신할 Unity 실험실 프로젝트다. 기준 Unity 버전은 6000.6.5f1이며 실행 Scene은 `Assets/Lab/Scenes/Lab.unity`다. 이전 버전은 `pre-physics-lab-rebuild-2026-10-10` 태그와 그 이전 Git 이력에서 확인한다.

- 현재 동작은 `README.md`, 실험 범위는 `docs/LAB_SCOPE.md`, 검증 범위는 `docs/VALIDATION.md`를 먼저 확인한다. 과거 버전의 문서를 새 코드의 검증 근거로 사용하지 않는다.
- 기본 방은 직접 IP로 정확히 3명 연결하고 입장 순서대로 자리를 배정한다. Development 빌드의 1인 솔로 모드는 물리 조정 전용이며 일반 3인 규칙과 구분한다.
- 수치 원본은 `Assets/Lab/Resources/LabConfig.asset` 하나다. 실행 중 DEV 패널의 변경은 솔로 세션의 복사본에만 적용한다.
- Scene과 `.meta`의 참조를 보존한다. Unity에서 화면과 에셋을 변경한 뒤에는 Scene 연결, 컴파일, 해당 EditMode·PlayMode 검사를 별도로 확인한다.
- 이 저장소는 공개다. 인증 정보, 서명 파일, 개인 경로·기기 식별자, 원시 실행 로그와 빌드 산출물을 커밋하지 않는다.
- 소스 존재, 테스트 코드 존재, 과거 로컬 실행 결과와 이 브랜치에서의 검증 결과를 구분한다. 실행하지 않은 Mac·iOS·실기기 항목은 PASS로 쓰지 않는다.
