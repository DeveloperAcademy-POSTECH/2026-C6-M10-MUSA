# 승리·패배 판정 결과 UI (#320)

Figma 파일 `wW7ny7lNQ4DXNJ4kD46AuR`의 승리 `3077:13264`, 패배 `3126:13414`를 각각 `StageResult.unity`, `GameOver.unity`에 적용했다. 사용자 제공 승리·패배 배경 PNG를 원본 장식 에셋으로 사용하고, 기존 `ShillaCultureBold.otf`를 재사용한다. 제목·표·안내·버튼은 배경에 포함된 글자가 아니라 Inspector에서 편집 가능한 Canvas 객체다.

## 구조와 연결

- `GameStageResultView`: 402×874 기준 디자인, Safe Area에 맞는 축소, 실제 기록 행과 내 기록 강조, 긴 목록 스크롤을 담당한다. Figma 원본의 행 강조와 구분선 SVG는 동일한 모양의 PNG로 렌더링해 Unity Sprite로 저장했다. 임시 Figma URL은 실행 코드에 없다.
- `GameScreen`: 기존 `LabResultStats`/`LabResultTable.RowsFor` 계산과 세션 API를 연결한다. 승리에는 확정 `ResultSnapshot`, 패배에는 같은 battle session/round의 `Defeat` 스냅샷만 사용한다. 패배를 승리 기록 캐시에 넣지 않는다. 기록이 없거나 중도 이탈로 최종 스냅샷이 없으면 값을 만들어 내지 않고 `기록 없음`/`—`를 표시한다.
- 박 관람은 기존 `PreviewTexture`가 있는 승리에서만 활성화된다. 패배 화면에는 관람 버튼 객체 자체가 없다. 기존 회전·확대·초기화·닫기 처리는 유지한다.
- 일반 승리 결과에는 다음 스테이지 버튼이 없다. Host는 전원이 결과를 받은 뒤 결과 화면을 보여준 시간을 10초 누적해 다음 스테이지로 자동 진행한다. 누구든 박을 관람하는 동안 누적을 멈추고 모두 닫으면 남은 시간을 이어간다. 관람 상태는 즉시 전송하고 기존 결과 준비 heartbeat에도 포함하며, run/stage/revision/설정 identity를 확인한다. 마지막 스테이지는 자동 진행하지 않으며 전체/현재 스테이지 기록 전환과 나가기를 유지한다. 결과 수신 ACK와 Host 권한은 기존 `GameSession`이 계속 검사한다.
- 각 스테이지 제목과 박 관람 문구는 현재 StageCatalog에서 가져온다. 구미호는 STAGE 1, 야광귀는 STAGE 2, 지귀는 STAGE 3이며 승리·패배 모두 해당 스테이지의 실제 스냅샷을 사용한다. 스테이지3 승리는 `ALL STAGES CLEAR / 모든 요괴 / 최종 승리 !`와 전체 스테이지 기록을 먼저 표시한다. 사용자가 3스테이지 기록 보기를 누르면 지귀의 현재 판 기록을 볼 수 있다. 스테이지3 패배는 STAGE 3·지귀·봉인 실패와 그 판 기록을 표시한다.
- 기존 2:2 결과 UI는 `LegacyResultContent`에서 유지한다. Lab, Home, Lobby, Battle Scene과 게임 설정·통신 규칙은 변경하지 않았다.

## 편집

각 결과 Scene의 Canvas 아래 `SafeArea/FigmaStageResult/Design402x874`에서 배치를 수정한다. 배경은 Canvas의 `ResultBackground`다. `AwardTemplate`는 실행 중 실제 수상 행에 재사용되며 원본 템플릿은 비활성화 상태로 저장한다. 5행을 초과하면 본문만 스크롤되고 표 머리글은 고정된다.

`Hapio > Upgrade Figma Stage Results`는 초기 적용용이다. 이미 `resultView`가 연결된 Scene은 다시 만들지 않으므로 이후 Inspector 수정을 보존한다.

## 검증 범위

Unity 6000.6.5f1 / macOS / Metal에서 컴파일 및 402×874 두 화면 렌더를 확인했다. 렌더에는 검수용 예시 기록을 넣었으며, 저장 Scene과 실행 코드에는 그 예시 결과가 저장되지 않는다. iPhone 실기기의 Safe Area·터치·여러 기기 동시 진행은 별도 확인이 필요하다. 실행 수치와 알려진 기존 검사 실패는 `VALIDATION.md`의 #320 항목을 따른다.
