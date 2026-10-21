# 미사용 변수와 과거 테스트 정리 — #246

작성일: 2026-10-21 KST. 이슈 [#246](https://github.com/DeveloperAcademy-POSTECH/2026-C6-M10-MUSA/issues/246), 브랜치 `refactor/#246-legacy-variable-test-cleanup`. 정리 기준은 로컬 병합 완료 커밋 `2272d25a`다. #240 개인 조합판 개편과 main `ae13b4f1`까지의 #235 소멸·#241 콤보/검사 수정이 포함돼 있다. 이 문서는 해당 기준 이후 정리 작업을 기록하며 이후 다른 PR의 main 병합 여부를 나타내지 않는다.

PR 준비 중 선행 #247의 최종 main `79858189`/`29ce13bc`(#237/PR #239 폭탄 전달·오디오 복구·동적 포트 fixture)를 추가 통합했다. 이때 이미 main에 있는 폭탄 놓기0.75배·감속1.3배와 새 전달 검사를 유지했다. 아래 정리 전후165개 값/48조건 비교는 추가 통합 전 기록이며, 현재 Config를 최신 선행본과 비교하면 여전히 폐기5키만 제거한 차이다. 추가 통합 검증은 VALIDATION의 별도 절에서 구분한다.

## 판단 기준

현재 Runtime·Scene·Prefab·DEV·설정 공유·Editor 비교 도구의 실제 호출을 추적했다. 이름이 오래됐거나 기본 OFF라는 이유로 제거하지 않는다. 현재 쓰는 선택 기능과 테스트의 판정 층은 보존한다. 사용자 Scene·Target2 머터리얼2개·기존 미추적 메타는 작업 시작 해시와 대조하며 이번 정리 대상에 넣지 않는다.

## 제거·이관 내역

| 대상 | 확인 근거와 처리 |
| --- | --- |
| `LabMotionSample`, `LabNetworkPacket.motionSamples` | 현행 보드는 행동별 좌표와 보드 프레임을 사용한다. 생산 코드에서 사용하지 않는 이전 일괄 이동 전송 자료형/필드 제거 |
| 모델 `TryUpdateMotion`·좌표 없는 `TryCombine`·`TryTransfer`·`TryUseItem` | 현재 게임은 `TryCombineAt`·`TryTransferAt`·`TryUseItemAt`을 사용한다. 테스트의 규칙/거절/소비 검사를 좌표 포함 경로로 이관한 뒤 옛 진입점과 nullable 내부 분기 제거 |
| `paintFlowRatio`, `paintDripWidthDegrees`, `paintDripLengthDegrees`, `paintGravityWeight` | 현재 `AdvanceGrowth → ChooseClusterCell`에서 사용하지 않는 과거 셀별 흐름 조절값이다. Inspector/Getter/설정 캡처·적용/DEV/직렬화와 미호출 `ChooseSpreadFlowCell`·`TryChooseFlowCell`·옛 `ChoosePatternCell` 체인 제거 |
| `paintVictoryPercent`·고정 목표 DEV 행·`PaintVictoryCoverage` | 조절해도 이미100%로 고정되는 행과 테스트 전용 getter 제거. `PaintVictoryPercent → DefaultPaintVictoryPercent(100)`·Snapshot/HUD·마지막 실제 셀 완료 규칙은 유지 |
| 도색자 없는 `TryReplacePixels` 오버로드 | 테스트 전용 호출을 명시적 painter 배열로 이관. 현행 맵 색상·도색자 동시 교체 경로는 유지 |
| 기능별 반복 구형 설정 배열 길이 테스트 | 공통 `LabSharedSettingsSchemaTests`로 통합. 현행 설정 범위·잘못된 값 원자적 거절·왕복 검사는 각 기능에 유지 |

주요 파일은 `Assets/Lab/Scripts/Core/LabConfig.cs`, `LabModel.cs`, `LabPaintSurface*.cs`, `Session/LabNetwork.cs`, `UI/LabDeveloperMode.cs`다. 모델 이관 검사는 `LabModelTests`, `LabLocalBoardAuthorityModelTests`, `LabImpactAuthorityTests`, `LabGourdComboTests`, `LabPaintPainterSlotTests`에 있다.

## 유지한 경로

- LEGACY/BROAD/SPLATTER/FLOW, Crack·직구, 질감/플레이어 무늬·완성 박 관람, 확률0 아이템과 기본 OFF DEV 기능.
- 활성 `InitialPatch`·`PatternPatch` 형상/반경·마스크 함수·`FlowDirection`과 Host의 표면 아래 방향 샘플. Flow 방향은 최초 FLOW와 현재 군집 회전에 필요하다.
- 오프라인 `Stamp`·`DefaultRadius` 미리보기, 실제 군집 성장·건조·투척/명중·100% 승리.
- LAN·Bonjour·직접 IPv4/IPv6·핫스팟의 기존 전송 구현. 소유권·중복 소비·오래된 판/패킷 거절·복구.
- 현재 화면/물리/렌더 검사와 #235 수명·예고/연기·#241 콤보 예고/기본값.

## 설정 스키마와 Scene 연결

현재 공유 설정은 **165개·프로토콜5650·연결 토큰 `C6PhysicsLab/14`**다. 구형170개/5649/token13 앱과 섞지 않고 모든 참가자를 같은 새 버전으로 빌드한다. 지문은 현재 전체 배열과 프로토콜을 포함하며 부분 적용하지 않는다.

| 정리 전 인덱스(0부터) | 정리 후 |
| --- | --- |
| 0~86 | 동일 |
| 87(고정 목표) | 제거 |
| 88~134 | 이전 인덱스−1 |
| 135~138(폐기 Flow4값) | 제거 |
| 139~169 | 이전 인덱스−5 |

현재 Pattern134, Viewer110, Player Patterns137, Cluster138~139, Drying148~151, Swipe Block152~154, Combo155~164다. 모든 값과 단위의 원본은 [DEV 명세](DEVELOPER_MODE_PARAMETER_SPEC.md)다.

저장 Scene에는 DEV 입력39행이 있고 제거 지점87보다 앞이다. 기존 `EnsureValueInputs`가 실행 시165행으로 늘리므로 Scene YAML이나 참조를 수정하지 않았다. Config는 Unity `SaveAssetIfDirty`로 다시 저장해 삭제된5키만 제거했다. 남은165개의 이름·순서·유효값을 정리 전170개에서5개를 제외한 결과와 정확히 대조했다. 나머지 저장 수치와 기본값은 바꾸지 않았다.

## 동작 보존 확인

정리 전후 동일한 공개 도색 경로로3해상도×4패턴×2스타일×2seed = **48조건**을 실행했다. 최초/반복 명중과 시간 진행 후 RGBA·도색자 지도 해시 및 실제 칠한 셀 수가 모두 같았다. 건조는 해당 비교에서 껐고, 건조 동작은 별도 기존 전체 검사로 확인했다.48조건은 모든 난수/시간/기기를 증명하는 전수 검사가 아니다.

전체 EditMode는844→794개다. 폐기된 계약 전용55개를 빼고 공통 스키마5개를 더했다. 현재 API를 검사하도록 이관한 핵심 게임 규칙은 삭제하지 않았다. PlayMode243개는 유지했다. 실제 결과와 실패 이력은 [검증 기록](VALIDATION.md)의 #246 절, 분류/실행법은 [테스트 보고서](TEST_SUITE_REPORT.md)를 따른다.

## 정리 범위 밖

새 기능·게임 규칙·패키지·클라우드·재접속 정책·Scene 배치는 변경하지 않는다. iOS 빌드/실기기·무선 핫스팟·모바일 성능은 이번 실행 근거가 없다. 테스트 개수 감소를 FPS·지연 개선으로 해석하지 않는다. 원시 증거와 임시 도구는 저장소 밖에 보관한다.

최종 정리 본문은 main 병합본을 보존한 `470561b` 소스를 기준으로 한다. 초기243개에 선행 검사4개를 보존해 PlayMode247개이며 전체794/794·247/247 통과 결과는 VALIDATION의 PR 최종 절에 기록한다.
