# Experiment #189 검증 기록

작성일: 2026-10-18. 기준 main `e3e3be4e`, Unity **6000.6.5f1**, 브랜치 `experiment/#189-gourd-result-viewer`. 정식 반영/병합 전 관람 실험이며 과거 버전의 PASS를 승계하지 않습니다.

## 구현 범위

Paint Victory·100%·공개 맵과 Snapshot의 메타데이터 일치 때만 최종 도색을 별도 복사해 보여줍니다. 각 기기의 회전·확대는 로컬이고 원본 도색·전투 카메라·물리·결과를 쓰지 않습니다. 기본 실행에는 관람 UI가 없고 Editor 메뉴 또는 Development 실행 인자로만 켭니다. Canvas UI는 이 폴더의 편집 가능한 프리팹입니다.

## 검사 결과

| 항목 | 상태 | 근거·범위 |
| --- | --- | --- |
| Unity 컴파일 | PASS | 기존 Editor에 새 실험 Runtime/Editor/테스트 어셈블리를 로드하고 실제 검사를 실행했습니다. 같은 프로젝트를 중복 batchmode로 열지 않았습니다. |
| EditMode 전체 | PASS | `Logs/Issue189-EditMode.xml`: total=317, passed=317, failed=0, skipped=0. 2026-10-18 07:34:48Z ~ 2026-10-18 07:34:49Z. 기존 검사를 포함한 전체 실행. |
| PlayMode 전체 | PASS | `Logs/Issue189-PlayMode.xml`: total=116, passed=116, failed=0, skipped=0. 2026-10-18 07:34:56Z ~ 2026-10-18 07:36:01Z. 기존 검사를 포함한 전체 실행. |
| 기본 opt-in 부재 | PASS | 기본 Lab Scene을 로드했을 때 실험 컨트롤러와 UI 인스턴스가 없음을 확인했습니다. Editor opt-in SessionState는 검사 중만 OFF로 보존/복원했습니다. |
| 실제 솔로 완료→관람 | PASS | 저장 Scene의 보드에서 SecretBox를 더블 탭하고 만들어진 Combined를 실제로 던져 Host 충돌로 완료한 뒤 entry 버튼을 눌렀습니다. 관람 텍스처 픽셀과 원본 맵 해시가 같고, 회전/확대/Reset/Close·반복 진입·Retry 자동 종료·원본 기록 불변을 확인했습니다. |
| 관람 렌더 | PASS | 독립 RenderTexture가 단색 배경 외 실제 모델 픽셀을 그리는지 PlayMode에서 readback으로 검사했습니다. 원본 Material과 관람 Material/도색 텍스처가 별개이고 관람 모델에는 Collider/Rigidbody/MonoBehaviour가 없습니다. |
| 핀치 뒤 포인터 인계 | PASS | `RemainingPointerResumesWithANewBaselineAfterTheFirstPointerLifts`에서 A/B 포인터 인계 후 최초 좌표는 회전 없이 기준점으로 삼고, 다음 이동에서 정규화 회전이 1회 발생하며 이미지 밖 휠은 무시됨을 확인했습니다. 실제 두 손가락 Touchscreen 이벤트가 아니라 A/B 포인터 이벤트를 UI 핸들러에 보내는 회귀 검사입니다. 실제 핀치·손가락 전환은 실기기 미검증입니다. |
| 실제 Mac 2앱 관람 | PASS | **최종 소스 검증용 빌드** `Builds/Lab/20261018-163620-545fac83/macOS/` (318,305,736바이트), 07:36:58Z 관람 캡처 ~ 07:37:02Z 완료. 실제 NGO/UTP localhost 연결·설정 ACK·Client 보드 투척 1회와 Host 물리 충돌로 양쪽 Victory를 만들었습니다. 양쪽 entry 버튼을 통해 관람하고 서로 다른 Quaternion을 유지하며 원본/복제 RGBA 해시가 같았습니다. 로컬 관람 입력 직후 원본 target와 전투 카메라의 pose가 변하지 않았습니다. 각 배우가 자기 JSONL에만 기록했고 파일로 진행을 동기화하지 않았습니다. |
| Host 재시작→양쪽 자동 종료 | PASS | 관람 중 Host가 기존 StartBattle 경로로 다음 판을 시작하자 양쪽 관람 패널·이미지·Preview가 닫히고 기존 Texture/RenderTexture가 파괴됐습니다. 같은 방 2인 연결과 새 빈 도색 맵은 유지됐으며 두 프로세스가 정상 종료 코드 0으로 끝났습니다. |
| 최종 일반 Mac 빌드 | PASS | 임시 Editor/통신 실행기와 해당 `.meta` 4개를 보관 후 제거하고 새로 빌드했습니다. `C6_LAB_BUILD_OK`, `Builds/Lab/20261018-163756-ce5b340c/macOS/C6_Physics_Lab.app`, **318,272,841바이트**. 최종 실험 DLL에 임시 하네스가 없음을 확인했습니다. 이 일반 빌드를 다시 두 앱으로 실행한 검사는 미실행입니다. |
| iOS·실제 터치·핫스팟·3~5기기 | NOT_RUN | iOS 출력/서명/설치·실행, 실제 기기의 핀치·포인터 취소·세로 회전·좁은 안전 영역, 기기 간 연결·핫스팟·3~5기기와 성능은 실행하지 않았습니다. |
| 기본 도색 밸런스 | NOT_RUN | 관람을 짧게 재현하는 DEV fixture를 사용했습니다. 기본 2% 초기 도색·4% 반복 공급으로 100% 도달하는 플레이 난이도 검사가 아닙니다. |

## Fixture·보완·보존

- 실제 완료 검사는 저해상도 64×32, 초기 도색100%, SecretBox100%/잠금0, 생성 비용1, 고정 투척값·정지 표적·박 폭탄 끔을 사용합니다. 맵/모델 직접 주입으로 실제 충돌을 대신하지 않았습니다. 각 테스트에서 원본 Config를 복제해 적용하고 복원합니다.
- 초기 전체 EditMode317/317·PlayMode115/115 통과 후 독립 검토에서 A 손가락을 먼저 떼면 B의 회전이 멈추는 문제가 확인됐습니다. 남은 포인터의 첫 좌표를 새 기준점으로 받아 점프 없이 인계하도록 보완하고, EndDrag·focus 상실·Disable에서 입력을 정리했습니다. 최종 실행 결과는 위 별도 행입니다.
- 기존 Paint shader의 고정 월드 조명에 맞춰 관람 카메라와 모델의 staging rig만 함께 회전했습니다. 상대 구도·로컬 입력·맵 픽셀은 유지하고 단색 완료에서도 곡면 명암을 볼 수 있게 했습니다. 게임 shader/공유 Material은 바꾸지 않았습니다.
- 검사 시작 때 Scene 저장 확인이 나와 취소했습니다. 최초 자동 승인 검토는 Scene 저장/재직렬화와 프로젝트 안 사본 생성을 사용자 변경 위험으로 거절했고 실행하지 않았습니다. 사용자가 **사본 보존 후 저장·검사**를 승인한 뒤 디스크·메모리 사본을 프로젝트 밖에 보존하고 저장했습니다. 저장 전후 Scene 차이는 빈 `m_Name` 뒤 공백 6곳뿐이며 이 Scene은 실험 커밋에서 제외합니다. 기존 Material 2개와 미추적 폴더도 보존하고 제외합니다.
- 기존 gameplay 코드·Scene 구조·단일 Config·패키지·프로젝트 설정·네트워크 프로토콜은 바꾸지 않습니다. 실험 파일과 Unity에서 생성한 프리팹/.meta만 별도 폴더로 관리합니다.
- 원시 XML·Editor/Player 로그·PNG·앱과 임시 테스트/통신 실행기는 로컬 증거로 보존하고 공개 Git에 넣지 않습니다. 임시 실행기는 최종 일반 빌드 전에 제거합니다.
