# 2026-C6-M10-MUSA
친구들과 어울리며, 함께 게임하고 싶은 유저가 3~5대의 휴대폰으로 요괴를 둥글게 둘러싸고, 좌우로 스와이프해 오행 구슬을 전달하고 조합해 공격하며 실시간으로 함께 싸우는 전투를 통해 제한된 시간 안에 아슬아슬하게 합을 맞추는 긴장감, 한국 전설 속 요괴를 함께 물리치며 느끼는 유대감과 성취감을 경험하도록 돕는 모바일 게임.

## Unity 프로토타입 · 빌드24

기존 별도 프로젝트의 구현을 `feature/unity-prototype` 브랜치로 이관했습니다. **이 저장소 루트가 Unity 프로젝트 루트**이며 `Assets`, `Packages`, `ProjectSettings`가 함께 있습니다. Unity Hub에서 내려받은 저장소 폴더를 Unity **6000.5.7f1**로 열고 아래 문서 순서로 확인하세요.

| 지금 구현된 기능 | 현재 범위 |
|---|---|
| 협동 연결 | 같은 Wi-Fi에서 2~5인, 방 탐색·Ready·공통 시작, 원형 좌우 이웃 |
| 하단 구슬 | 평면 2D 관성·마찰·충돌, 직접 드래그한 Yin+Yang만 조합 |
| 좌우 이동 | 놓은 구슬이 끝에 닿으면 이웃 화면으로 자동 전달, 남은 속도와 높이 유지 |
| 공격 | Combined를 위로 움직이며 손을 놓으면 방향·세기 기반 3D 투척, Host의 실제 충돌로 피해 확정 |
| 한 판 | 시작 구슬0·스태미나100, 생성 비용20, 시간 회복20/3초, 명중 회복5, HP100·피해20·180초·결과·Retry |

위 팀 소개는 게임의 기획 방향입니다. **이번 프로토타입에는 오행 속성·상성·요괴 AI·몬스터 공격·실제 방어가 포함되지 않습니다.** 현재 구슬 규칙은 Yin/Yang/Combined이며 이관 과정에서 새 게임 기능을 추가하지 않았습니다.

### 처음 열기와 빌드

1. Unity 6000.5.7f1과 필요한 iOS Build Support를 준비합니다.
2. Unity Hub에서 저장소 루트를 열고 Import·컴파일을 기다립니다.
3. `Assets/_Project/HapioMVP/Scenes/ContinuousTransferBattle.unity`를 엽니다.
4. 협동 플레이는 같은 빌드의 실행본이 최소 2개 필요합니다.
5. 직접 빌드는 [상세 빌드 안내](BUILD_README.md)와 [P4 실행 안내](docs/P4_RUNBOOK.md)를 따릅니다. iOS 서명에는 본인이 사용할 수 있는 Apple 개발 Team이 필요합니다.

Unity·URP·네트워크 패키지 버전은 저장된 설정을 유지합니다. 현재 기본 Bundle ID와 앱 출력 이름도 원래 프로토타입 값을 유지하므로, 제품명·배포 식별자 변경은 별도 작업으로 진행하세요.

### 개발 문서

- [계획 — 추가 패키지 없는 핫스팟·Wi-Fi 호환성 개선](docs/LOCAL_NETWORK_COMPATIBILITY_PLAN.md) · L1 핫스팟 데이터 왕복 확인 완료, L2 게임 통합은 미시작
- [L1 네트워크 진단 코드·검증·빌드 방법](docs/L1_NETWORK_DIAGNOSTICS.md)
- [보고서 1 — Unity 프로젝트 준비와 기본 게임](docs/UNITY_BEGINNER_DEVELOPMENT_REPORT.md)
- [보고서 2 — 2D 물리·3D 투척·최대5인 확장](docs/UNITY_BEGINNER_PHYSICS_MULTIPLAYER_REPORT.md)
- [보고서 3 — 실제 코드 읽기·유지보수·디버깅 실습](docs/UNITY_BEGINNER_CODE_MAINTENANCE_GUIDE.md)
- [현재 연속 전달 구현](docs/P4_IMPLEMENTATION.md)
- [공개 검증 요약과 확인 한계](docs/VALIDATION_SUMMARY.md)
- [이관 범위·원본 보존·정적 검사](docs/MIGRATION.md)

이번 업로드는 기존 구현의 파일 이관입니다. 이 새 경로에서 Unity 실행·새 빌드·실기기 검사를 완료했다는 뜻은 아닙니다. 원시 기기 로그·서명 자료·과거 Git 이력은 공개하지 않았으며 기존 검증 보고와 이번 이관 검사를 구분합니다.

### 로컬 네트워크 호환성 개선 L2

앱26의 IPv4/IPv6 수신, 여러 주소 재시도와 취소, 검색 만료, 최초 상태 확인을 적용했다. 기존 게임 계약과 규칙은 빌드24를 유지한다. iPhone 핫스팟→iPad의 실제 방 입장·최초 상태·양쪽 Ready를 확인했다. 현재 검증 범위와 실행 방법은 [L2 기록](docs/L2_CONNECTION_INTEGRATION.md)을 확인한다. 일반 Wi-Fi·인터넷 없는 LAN·전체 다인 게임 검사는 같은 빌드의 별도 검증이 필요하다.
