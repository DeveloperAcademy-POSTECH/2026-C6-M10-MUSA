> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# P2 · 새 씬·몬스터 구도·반발 바닥 적용 메모

2026-09-14. 이 메모는 P2 코드 준비의 결정과 적용 범위다. 실제 Prepare·자동 검사·Mac·iOS·기기 실행 결과는 실행 후 작성한 P2 검증 기록을 따른다. 이 문서 자체로 PASS를 선언하지 않는다.

## 새 씬과 기존 상태 보존

`C6.Editor.ThrowBattleBuild.Prepare`는 저장된 P1 `PhysicsBattle.unity`(빌드21)에서 별도 `ThrowBattle.unity`(빌드22)를 만든다. 기존 한 개 Config와 `BenchmarkMonster.prefab`을 참조하며 이전 씬·Prefab·메타를 덮어쓰지 않는다. 새 씬의 `P2ThrowBattle` 루트에서 P1 2D 물리와 P2 손떼기 투척을 명시적으로 켠다. 원본과 같은 두 참가자 흐름을 유지하고 P3 다인 구현으로 확대하지 않는다.

두 카메라와 Controller·로비·몬스터의 runtime Config 참조는 새 씬으로 다시 연결한다. 이미 저장된 P2 씬은 반복 Prepare에서 새로 덮어쓰지 않고 구조를 검사한다. 기존 결과가 있는 출력 경로도 거부한다. 메뉴는 **C6 → Next Phase → P2 → Prepare Release Throw Battle**이며, `BuildMac`은 `C6_P2_OUTPUT_ROOT` 아래 `macOS/C6Throw.app`, `ExportIOS`는 `iOS/`를 사용한다.

## 몬스터와 HUD가 겹치는 문제

P1 실제 캡처 `docs/evidence/P1/mac-pair/client/02-shared-real-hit.png`에서 TEAM HP 글자와 허수아비 머리가 겹친다. 기존 상단 화면은 전체 높이55%이며 HUD는 Safe Area와 Canvas 배율을 적용하지만, 몬스터를 바라보는 카메라는 HUD의 점유 영역을 고려하지 않았다.

새 `ThrowBattleFraming`은 다음 순서로 처리한다.

1. 실제 상단 Camera.pixelRect와 Safe Area의 교집합을 구한다.
2. 실행 중 생성된 TEAM HP·TIME·ORBS의 RectTransform 아래 경계와, 하단 ResourceMode·ROUND의 위 경계를 화면 좌표로 읽는다. 고정 픽셀 높이를 별도로 복제하지 않는다.
3. 남은 영역의 작은 쪽 길이에 Config `battleFramingPaddingFraction=.025`를 곱한 여백을 뺀다.
4. 몬스터 Visual의 Renderer bounds와 Hitbox bounds 전체를 포함하는 상자의8개 모서리가 유효 영역에 들어오도록 카메라 거리와 수평·수직 위치를 계산한다.

카메라는 **기존 Perspective, 상단55% viewport, 저장된 FOV42도와 시선 방향**을 유지한다. 최초 카메라 자세는 P1 저장 씬의 위치(0,3.1,-9), 바라보던 점(0,2.2,0)을 기준으로 캡처한다. 새 설정 에셋에 카메라 값을 복제하지 않는다. 화면 비율·Safe Area·HUD 배율·표적 bounds가 바뀌면 다시 계산하며, 비활성화되면 저장된 카메라 자세로 복원한다. 변하지 않은 화면에서는 이전 계산을 재사용한다.

이 구도는 몬스터 본체와 피격 영역을 HUD에서 분리하는 기준이다. 모든 투척 방향의 비행 궤적이 항상 화면 안에 머문다는 보장은 아니며, 실제 투척의 가독성과 미명중은 별도 실행에서 확인한다. 유효한 HUD 공간이 없으면 실패 상태를 기록하고 viewport나 GUI 영역을 임의로 넓히지 않는다.

## 피격 표적과 반발 바닥 분리

몬스터는 P0/P1의 기존 `BenchmarkMonster.prefab`을 유지한다. 표적 ID·위치·BoxCollider 기준은 기존 Config를 사용하고, HP·피해·회복은 기존 Host 권한 경로에서 확정한다.

P2 씬에만 `P2ThrowFloor`를 추가한다. 기존 시각 무대·Floor 메시를 보존하면서, 새 non-trigger BoxCollider 하나를 반발에 사용한다. 바닥에는 `MonsterHitTarget`이 없으므로 바닥 접촉이 몬스터 피해가 되지 않는다.

| 항목 | Config 또는 적용값 | 분류 |
|---|---|---|
| 바닥 윗면 | `throwFloorY=0` | DEMO_TUNING_VALUE |
| 바닥 크기 | `throwFloorSize=(18,0.2,18)` | DEMO_TUNING_VALUE |
| Collider 중심 높이 | 윗면 - 높이/2, 기본 -0.1 | 위 두 값에서 계산 |
| 바닥 마찰 | `throwFloorFriction=0.2` | DEMO_TUNING_VALUE |
| 바닥 자체 반발 | 0, bounceCombine=Maximum | 투사체 반발값과 중복해서 키우지 않는 접촉 정책 |
| 접촉 마찰 결합 | frictionCombine=Maximum | 투사체 마찰 0과 접촉해도 Config의 바닥 마찰 0.2를 적용 |
| 투사체 반발 | 기존 한 Config의 P2 `throwBounce=0.45` | 투사체 담당 구현에서 적용 |

바닥 PhysicsMaterial은 실행 중 한 번 생성하고 바닥 제거 시 정리한다. 별도 저장 Material 에셋에 조정값을 중복하지 않는다. 몬스터 AI·이동·공격·별도 HP·피격 애니메이션을 이 구도·바닥 작업에 추가하지 않았다.

## 준비한 검증 범위

새 `ThrowBattleSceneTests`는 저장된 빌드22 선택·참조 연결·Prefab 유지·비표적 바닥·P1 원본 모드 보존과, 여러 휴대폰/태블릿 비율에서의8모서리 투영 계산을 검사하도록 작성했다. 실제 실행 전에는 NOT_RUN이다. 화면의 실제 HUD 점유 영역과 최종 이미지·투척 체감은 P2 PlayMode/Mac 실행에서 별도로 확인한다.
