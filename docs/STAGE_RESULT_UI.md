# 승리·패배 판정 결과 UI (#320·#367)

Figma 파일 `wW7ny7lNQ4DXNJ4kD46AuR`의 승리 `3077:13264`, 패배 `3126:13414`를 각각 `StageResult.unity`, `GameOver.unity`에 적용했다. 사용자 제공 승리·패배 배경 PNG를 원본 장식 에셋으로 사용하고, 기존 `ShillaCultureBold.otf`를 재사용한다. 제목·표·안내·버튼은 배경에 포함된 글자가 아니라 Inspector에서 편집 가능한 Canvas 객체다.

## 구조와 연결

- `GameStageResultView`: 402×874 기준 디자인, Safe Area에 맞는 축소, 실제 기록 행과 내 기록 강조, 긴 목록 스크롤을 담당한다. Figma 원본의 행 강조와 구분선 SVG는 동일한 모양의 PNG로 렌더링해 Unity Sprite로 저장했다. 임시 Figma URL은 실행 코드에 없다.
- `GameScreen`: 기존 `LabResultStats`/`LabResultTable.RowsFor` 계산과 세션 API를 연결한다. 승리에는 확정 `ResultSnapshot`, 패배에는 같은 battle session/round의 `Defeat` 스냅샷만 사용한다. 패배를 승리 기록 캐시에 넣지 않는다. 기록이 없거나 중도 이탈로 최종 스냅샷이 없으면 값을 만들어 내지 않고 `기록 없음`/`—`를 표시한다.
- 박 관람은 기존 `PreviewTexture`가 있는 승리에서만 활성화된다. 패배 화면에는 관람 버튼 객체 자체가 없다. 기존 회전·확대·초기화·닫기 처리는 유지한다.
- CPU 최종 지도 일치와 GPU 관람 사본 생성 성공은 별개다. 현재는 CPU 지도가 일치해도 GPU 사본 생성이 실패하면 별도 오류 안내 없이 관람이 비활성화될 수 있다. 이 기존 제약을 #367에서 새로 보완하지는 않는다. 공통 한도 내 최종 지도 불일치가 남으면 중간 StageResult는 GameOver로 종료하고, 최종 Completed만 클리어를 보존하며 지도 미동기화 안내를 표시한다.
- 일반 중간 승리 결과는 Host의 **다음 스테이지** 버튼으로만 진행한다(#367). 10초 자동 진행과 별도 Ready 입력은 없다. 전원 결과 수신 ACK가 모이고 누구도 박을 관람하지 않을 때 버튼을 사용할 수 있으며, 참가자는 Host의 입력을 기다린다. 관람 상태는 즉시 전송하고 기존 결과 준비 heartbeat에도 포함하며, run/stage/revision/설정 identity를 확인한다. 마지막 스테이지는 전체/현재 스테이지 기록 전환과 나가기를 유지한다. 결과 수신 ACK와 Host 권한은 기존 `GameSession`이 계속 검사한다.
- 결과 화면은 OnEnable에서 내용을 초기화하고, 최소 다음 프레임 및 모든 마우스·터치 해제 전에는 NEXT/HOME/관람 버튼을 비활성화하며 해당 콜백도 거절한다. 전투 입력이 해제된 뒤 새 입력으로만 결과 행동을 시작한다. 입력 보호를 위한 고정 초 대기는 없다.
- 각 스테이지 제목과 박 관람 문구는 현재 StageCatalog에서 가져온다. 구미호는 STAGE 1, 야광귀는 STAGE 2, 지귀는 STAGE 3이며 승리·패배 모두 해당 스테이지의 실제 스냅샷을 사용한다. 스테이지3 승리는 `ALL STAGES CLEAR / 모든 요괴 / 최종 승리 !`와 전체 스테이지 기록을 먼저 표시한다. 사용자가 3스테이지 기록 보기를 누르면 지귀의 현재 판 기록을 볼 수 있다. 스테이지3 패배는 STAGE 3·지귀·봉인 실패와 그 판 기록을 표시한다.
- Hapio Battle에는 독립 Lab의 결과 카드와 대시보드를 표시하지 않는다. 결과 Scene에서 기존 `LabResultStats`·`LabResultTable`과 확보한 관람 사본을 사용하며, 독립 Lab의 결과·관람 동작은 유지한다. 기존 2:2 결과 UI와 같은 방 로비 복귀는 `LegacyResultContent`에서 유지한다.
- #320 도입 당시에는 공유 설정205개·프로토콜5666·Lab27을 유지하고 게임 토큰을 `C6HapioGame/11`로 구분했다. 현재 #367은 게임 토큰만 `C6HapioGame/22`로 올리며 공유 설정251개·프로토콜5677·`C6PhysicsLab/38`을 유지한다.

## 편집

각 결과 Scene의 Canvas 아래 `SafeArea/FigmaStageResult/Design402x874`에서 배치를 수정한다. 배경은 Canvas의 `ResultBackground`다. `AwardTemplate`는 실행 중 실제 수상 행에 재사용되며 원본 템플릿은 비활성화 상태로 저장한다. 5행을 초과하면 본문만 스크롤되고 표 머리글은 고정된다.

`Hapio > Upgrade Figma Stage Results`는 초기 적용용이다. 이미 `resultView`가 연결된 Scene은 다시 만들지 않으므로 이후 Inspector 수정을 보존한다.

## #320 도입 당시 검증 범위

아래는 #320 당시 기록이며 #367의 실행 결과가 아니다. Unity 6000.6.5f1 / macOS / Metal에서 컴파일 및 402×874 두 화면 렌더를 확인했다. 렌더에는 검수용 예시 기록을 넣었으며, 저장 Scene과 실행 코드에는 그 예시 결과가 저장되지 않는다. iPhone 실기기의 Safe Area·터치·여러 기기 동시 진행은 별도 확인이 필요하다. 실행 수치와 알려진 기존 검사 실패는 `VALIDATION.md`의 #320 항목을 따른다. #367의 실제 수행 여부는 같은 문서의 별도 기록으로 구분한다.
