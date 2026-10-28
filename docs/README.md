# 합이오 개발 문서

저장소의 [README](../README.md)는 게임 소개다. 개발·유지보수할 때는 이 목차에서 필요한 문서로 이동한다.

## 실행과 작업 시작

- 개발용 게임은 Unity의 `Hapio > Open Home`에서 실행한다. 1~3스테이지는 공통 Battle Scene을 사용한다. 메뉴·방 입장·빌드 경로는 [게임 흐름](GAME_FLOW.md)의 **실행과 빌드**를 따른다.
- 기존 `Assets/Lab/Scenes/Lab.unity`는 단판 실험·회귀 확인용이다. 별도 게임과 구분하고 [Lab 범위](LAB_SCOPE.md)를 먼저 확인한다.
- 수치와 Inspector·DEV 변경 지점은 [개발자 모드 설정](DEVELOPER_MODE_PARAMETER_SPEC.md), 작업 규칙은 [AGENTS](../AGENTS.md)를 따른다.
- 이전 버전의 설명과 현재 코드·저장 에셋이 다르면 기준 커밋과 적용 범위를 확인한다. 문서에 있는 테스트 이름이나 과거 통과 횟수를 현재 브랜치의 실행 결과로 간주하지 않는다.

## 기능별 안내

| 확인할 내용 | 문서 |
| --- | --- |
| Home·Lobby·3스테이지·결과·재시작·빌드 | [게임 흐름](GAME_FLOW.md) |
| 오프닝 순서·진입 정책·터치 진행 | [앱 시작 오프닝](APP_START_OPENING.md) |
| 오프닝 그림·대사·제작용 미리보기 편집 | [팝업북 스토리 제작](POPUP_STORY_WORKFLOW.md) |
| 구슬의 가장자리 전달·정체 보정·세로 방향·iOS 제스처 | [모바일 구슬 조작](MOBILE_ORB_CONTROLS.md) |
| 개인 조합판·Host 검증·전달·공동 전투 동기화 | [개인 조합판 권한 구조](LOCAL_BOARD_AUTHORITY.md) |
| 공유 수치·음양 연속 보정·스태미나·기믹·피버 조정 | [개발자 모드 설정](DEVELOPER_MODE_PARAMETER_SPEC.md) |
| 방 자동 입장·완료 연출·HUD 위계·결과 박 비율 | [세션 완료와 UI](SESSION_FINISH_UI.md) |
| 스테이지 결과 화면 | [결과 UI](STAGE_RESULT_UI.md) |
| 도색 패턴과 투척자 무늬 | [런타임 도색 패턴](PAINT_PATTERN_RUNTIME.md) |
| 도색 건조·근처 재타격 | [페인트 건조](PAINT_DRYING.md) |
| 실제 전투·완성 박의 고해상도 표현 | [런타임 고해상도 도색](RUNTIME_HIGH_RESOLUTION_PAINT.md) |
| 투척 수치·물리 조정 | [투척 조정](THROW_TUNING_GUIDE.md) · [박 물리 조정](PINATA_TUNING_GUIDE.md) |
| 오행 비행 트레일 | [트레일 안내](ELEMENT_FLIGHT_TRAILS.md) |
| 현재 지귀 개인 발화·전원 불장막 | [지귀 화면 패턴](JIGWI_SCREEN_PATTERNS.md) |
| 구미호 피버 곡·효과음·Inspector 음량 | [구미호 소리](GUMIHO_AUDIO.md) |
| 소리 출처·가공·권리 확인 상태 | [오디오 출처](AUDIO_CREDITS.md) |
| 완성 박 PNG 내보내기 | [박 PNG 내보내기](GOURD_PNG_EXPORT.md) |

규칙과 수치의 원본은 코드·저장 설정이며, 표의 문서는 변경 지점과 적용 범위를 설명한다. 실험 또는 과거 구현의 문서를 현재 게임 사양으로 그대로 적용하지 않는다.

## 실험·과거 구현 자료

- [파란 수채 질감 실험](WATERCOLOR_PAINT_EXPERIMENT.md): 실제 박의 기존/후보 정적 비교와 조절 도구. 전투 연결·번짐은 아직 적용하지 않았다.
- [고해상도 도색 비교 실험](HIGH_RESOLUTION_PAINT_EXPERIMENT.md): 실제 박에 최초 명중1회·재타격3회를 적용한 표현 비교. 현행 런타임 적용은 위 고해상도 도색 문서를 따른다.
- [도색 스타일 비교](PAINT_STYLE_COMPARISON_EXPERIMENT.md): 패턴 비교 실험의 기준과 결과.
- [Lab 수동 지귀 시험](JIGWI_PATTERNS.md): 기존 단판 Lab에서 수동 발동하는 시험 경로. Hapio의 현재 A/B와 구분한다.
- [지귀120초 초기 기획·구현 이력](JIGWI_LEVEL_DESIGN_120S.md): 변경 당시 기록이며 현재 화면 패턴은 위 지귀 화면 패턴 문서를 따른다.
- [원본 실험실 이관](MIGRATION_PHYSICS_LAB.md) · [구형 코드·검사 정리](LEGACY_VARIABLE_TEST_CLEANUP.md): 이관·정리 당시의 경계와 기록.

## 검증 기록

실제로 수행한 환경·버전·결과와 미실행 범위는 [VALIDATION](VALIDATION.md)에 기록한다. 검사 구성은 [검사 모음 보고서](TEST_SUITE_REPORT.md)를 참고한다. 이 목차와 기능 설명에는 과거 통과 건수를 복제하지 않는다.

## 문서 유지 규칙

1. README에는 게임 소개·핵심 경험·플레이 방법·팀 소개만 둔다. 개발 문서는 이 목차로 연결한다.
2. 기능 수정은 기존 담당 문서를 먼저 갱신한다. 기존 문서에 없는 독립된 안내만 새 문서로 만든다.
3. 작업 목적·진행 상태·변경 이력은 이슈·PR, 실제 실행 결과는 검증 기록에 남긴다. 같은 설명을 여러 문서에 복사하지 않는다.
4. 출시일·인원·시간·프로토콜 같은 값은 적용 범위와 근거를 확인한다. 출처가 다른 과거 수치를 함께 현재값으로 쓰지 않는다.
