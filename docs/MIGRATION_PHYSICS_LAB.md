# C6 Physics Lab 교체 이관 기록

작성일: 2026-10-10. 대상: `DeveloperAcademy-POSTECH/2026-C6-M10-MUSA`의 `refactor/physics-lab-rebuild` 브랜치. 기준 main: `381281de3e253a37a919966988f4be0d86254e13`. 이 브랜치는 기존 Unity 게임 프로젝트를 새 실험실 구현으로 교체한다. 기존 버전은 기준 커밋과 `pre-physics-lab-rebuild-2026-10-10` 태그에 남기며 Git 이력을 재작성하지 않는다.

## 이관한 범위

- 새 Unity 6000.6.5f1 프로젝트의 `Assets/`, `Packages/`, `ProjectSettings/`, Scene, C# 소스·테스트, `.meta` 파일을 함께 이관한다. 원본은 로컬 실험실의 `faf571e` 커밋 위에 투척 조정 작업이 적용된 작업 트리다.
- 루트 README와 실험 범위·투척 조정 가이드를 가져와 이 저장소의 `docs/` 경로에 맞춘다. 검증 기록은 공개 가능한 요약으로 새로 작성한다.
- 팀의 `.github` 이슈·PR 양식과 Git 제외 규칙은 유지한다. `AGENTS.md`는 새 프로젝트의 실제 구조와 검증 규칙으로 교체한다.

## 제외·보존

- 이전 `Assets/`, `Packages/`, `ProjectSettings/`, `docs/`의 현재 파일은 새 버전의 작업 트리에서 제거한다. 삭제된 당시 파일은 이전 Git 커밋과 태그에서 복원할 수 있다.
- 이전 작업 폴더와 원본 실험실 폴더의 미커밋 변경은 수정하지 않는다. 별도 작업 공간에서 이관했다.
- `Library/`, `Temp/`, 앱·Xcode 빌드 산출물, 서명 자료와 원본 실험실의 원시 검증 로그는 업로드하지 않는다.

## 확인 결과

원본 실험실에서 통과한 검사와 새 경로에서 실행한 검사는 `docs/VALIDATION.md`에서 구분한다.

| 항목 | 상태 | 범위 |
| --- | --- | --- |
| 원본 대비 Unity 파일·메타데이터 일치 | PASS | Assets·Packages·ProjectSettings 파일 비교 일치; Assets 메타데이터 누락·중복 GUID 0 |
| 개인 정보·비밀 정보·빌드 산출물 제외 | PASS | 공개 전 경로·식별자 문자열 검색 및 앱·서명 파일 검색; 원시 실행 로그 미포함 |
| 이관 경로 Unity 컴파일·자동 검사 | PASS | EditMode 12/12, PlayMode 20/20, 실패·건너뜀 0 |
| 이관 경로 Mac 앱 빌드 | PASS | Development 앱 173,292,078 bytes; 출력은 저장소 밖에 보관 |
| 다인 실행·iOS 기기 | NOT_RUN | 별도 환경 검증 |

새로 작성한 Markdown의 `git diff --check`는 통과했다. 전체 diff에는 Unity가 원본 Scene·`.meta`·ProjectSettings에 직렬화한 빈 값의 후행 공백 경고가 있다. 해당 Unity 파일은 원본과 바이트 단위로 일치하도록 보존했으며, 이관 경로에서 EditMode·PlayMode와 Mac 빌드를 다시 통과했다.

현재 기본 게임은 정확히 3인 직접 IP 연결이다. Development 빌드의 솔로 모드에서 조합과 투척 수치를 시험할 수 있다. 이전 버전의 2~5인·오행·방어 UI 등은 새 버전에 있다고 주장하지 않는다.
