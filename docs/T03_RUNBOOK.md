> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T03 · 호스트 권위 숫자 반복 실행

실행 결과는 [VALIDATION](VALIDATION.md)과 `docs/evidence/T03/`를 따른다. 아래 절차만 읽거나 코드를 준비한 것은 PASS가 아니다.

## 준비

Unity6000.5.7f1, 기존 NGO2.13.1/Transport6.5.0, 같은 Wi-Fi의 참가자 두 명을 사용한다. 원본 Editor에 미저장 씬이 있으면 저장하거나 검증 사본을 사용한다. Editor 강제 종료·Library 삭제·기존 빌드 덮어쓰기를 하지 않는다.

Unity 메뉴 `C6 > T03 > Prepare Shared Counter Scene`이 저장된 `CounterSmoke.unity`와 기존 연결 UI, 숫자 UI, 요청 처리를 연결한다. 기존 T01/T02 씬은 보존하고 T03만 빌드 활성화한다. `Export iOS`와 `Build macOS Counter Test`는 `C6_T03_OUTPUT_ROOT` 또는 `Builds/T03/`에 출력한다. 출력이 있으면 새 경로를 지정한다.

앱 표시는 C6-T03, 버전0.1.0/빌드5, Bundle ID `com.wolfuraark.c6prototype`이다. iOS는 기존 개인 Team 선택을 유지하며 iPhone+iPad 공용 대상이다. 현재 실제 출력은 `C6_T03_OUTPUT_ROOT=.../Builds/T03-iPad`를 지정해 분리했다. Xcode 개발자 경로는 명령별 `DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer`로 지정하고 전역 설정을 바꾸지 않는다.

## 화면 사용

1. P1에서 Host를 누른다. 화면은 세로로 스크롤되며 연결 입력은 공유 숫자 패널 아래에 있다.
2. P2에서 Host IPv4와 Port를 입력하고 Join을 누른다. 권한 알림이 실제로 나오면 허용하고 필요하면 수동으로 다시 Join한다.
3. 양쪽 Connected/참가자2명과 같은 Session 표시를 확인한다. P2는 호스트의 전체 상태를 받기 전에는 숫자 요청을 보낼 수 없다.
4. `+1`은 고유 요청 하나를 보낸다. 숫자는 호스트가 확정한 응답·상태를 받은 뒤 갱신된다.
5. `DEV: Send 50 requests`는 명시적인 버튼 입력으로 50건을 자동 전송한다. 연결만으로 시작하지 않는다. 두 참가자가 각50건을 보낸 뒤 양쪽 Value=Host approved=100, Rejected=0, Revision=100, Local sent=50, Acknowledged=50, Pending=0을 확인한다. 전송 중 연결이나 세션이 끝나면 묶음도 중단된다.
6. `DEV: Replay last request`를 눌러 Duplicate receipts만 증가하고 값·승인·revision·고유 발송/확인 수는 그대로인지 확인한다.
7. 연결을 종료한 뒤 수동 Host/Join으로 새 세션을 만든다. Session ID가 달라지고 숫자·발송·확인·중복 집계가0인지 확인한다. 자동 재접속은 없다.

고유 발송 수는 로컬에서 발급한 요청 ID 수다. 전송 호출만으로 호스트 승인을 추정하지 말고 Acknowledged/Pending과 양쪽 확정 상태를 확인한다.

## 자동 실행과 실기기 구분

EditMode는 주소·참가 승인 회귀와 순차/동시/중복 권위 모델, 잘못된 패킷을 검사한다. PlayMode는 저장된 씬의 UI와 실제 로컬 Host 요청 경로를 검사한다. 실제 실행 XML에서 총수·통과·실패·누락을 확인한다.

`tools/t03_two_process.py --app <C6Counter.app> --output <새 결과 폴더>`는 별도 Mac 프로세스 두 개로 순차/동시 100건과 중복 응답을 시험한다. 순차 시나리오는 호스트50건 뒤 클라이언트가 참가해 초기 전체 상태50을 받는 것도 확인한다. 이 결과를 두 iPhone 결과로 표시하지 않는다.

개발 앱의 명시적 `-c3Role host -c3Scenario lan -c3Port 7777 -c3Results <결과.json>`은 Mac Host가50건을 먼저 확정한 뒤 실제 iPhone의 Join/50건 전송을 기다리는 보조 시험이다. 보통 실행에는 자동 연결·자동 전송이 없다.

원래 G2는 같은 Wi-Fi의 실제 iPhone 두 대를 요구했으나, 이번에는 사용자 승인으로 **iPhone17 Host+iPad Client**를 사용했다. [기기 변경과 시험 순서](G2_DEVICE_CHANGE.md).100건 고유 요청·승인/상태 일치·권한 첫 요청·실패 후 수동 재시도 조건은 유지했고 빌드5에서 확인했다. 빌드4의 iPhone+Mac 결과는 과거 증거로 보존한다. 처음 권한 알림을 보지 못한 재실행에서는 해당 항목을 미관측으로 기록하며 기기의 전체 권한을 임의 초기화하지 않는다.

## 증거 보관

앱·Xcode 산출물은 `Builds/T03/`, 원시 로그는 `Logs/T03/`에 로컬 보관한다. 실행 종료 시점의 로그 사본·해시, XML, 실제 상태 발췌, 소스 일치표를 검토해 `docs/evidence/T03/`에 연결한다. 사용자 관측과 자동 입력은 따로 기록하며 미실행 Gate는 PASS로 바꾸지 않는다. T04는 자동 시작하지 않는다.
