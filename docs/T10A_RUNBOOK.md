> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T10-A · 반복 실행과 기기 확인

새 씬은 `Assets/_Project/HapioMVP/Scenes/RoomLobby.unity`다. Unity6000.5.7f1의 C6/T10-A/Prepare Room Lobby로 기존 단일 Config에 연결된 새 씬을 준비한다. 이미 있는 씬은 재생성하지 않는다. Mac과 iOS는 각각 실제 활성 Build Target을 선택하고 `C6.Editor.LobbyBuild.BuildMac` / `ExportIOS`를 실행한다. 기존 출력과 충돌하면 거부하므로 새 C6_T10A_OUTPUT_ROOT를 지정한다. 기본은 Builds/T10-A다.

## T12에서 수행할 정식 두 iPhone 절차

1. 같은 빌드14(또는 당시 최종 동일빌드)를 두 iPhone에 설치하고 같은 Wi-Fi에 연결한다. 기기·OS·빌드·시각과 Config 해시를 기록한다. 세로 화면과 Safe Area를 실제 확인한다.
2. P1은 Create Room, P2는 Find Rooms를 누른다. 최초 로컬 네트워크 권한 알림의 실제 표시/허용 여부를 기록한다. P2가 주소를 직접 입력하지 않고 발견한 방의 Join을 눌러 P2로 들어오는지 확인한다.
3. 양쪽2/2·자기P1/P2·좌우다른참가자·설정수신을 확인한다. 한쪽Ready만으로 시작할 수 없고, 양쪽Ready 뒤 Host만 Start가 활성화되는지 확인한다. 두 화면의 START CONFIRMED와 동일한 room/session/round1/Seed/Config 로그를 대조한다.
4. 참가 전 검색취소/Refresh, 방종료 목록제거를 확인한다. 다른 버전·만원·만료 항목은 fixture/별도 실행 로그와 구분하며 실기기에서 실행한 항목만 PASS로 기록한다. 세번째 참가와 Playing 중 새참가 거절은 별도 실행본을 써 확인한다.
5. 한 참가자가 나가면 양쪽종료·명시적새방의 Ready초기화를 확인한다. 전경/배경과 Wi-Fi 단절도 당시 범위에 맞게 기록한다.
6. 탐색 실패는 AT-02 FAIL 또는 기기/권한 미준비 BLOCKED/NOT_RUN으로 기록한다. 필요하면 Direct IP에서 Host의 Wi-Fi IPv4와port를 입력해 원인을 분리한다. IP성공으로 Bonjour PASS를 대체하지 않는다.

이번 T10-A의 두 iPhone 시험은 T12 요구사항이며 아직 NOT_RUN이다. Mac 두프로세스, Mac+iPhone, G2의 iPhone+iPad 결과는 각각 해당 범위로만 기록한다. T10-B 전체게임 동기화는 다음 별도 요청이다.
