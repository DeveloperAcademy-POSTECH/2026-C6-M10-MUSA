> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# G2 · iPhone+iPad 실기기 조건 변경

2026-09-13. 사용자가 “아이폰은 2개까지는 없고 아이패드 하나 있는데 그걸로도 검증 가능해? 가능하다면 해당 방향으로 진행하자”라고 요청했다. 최신 사용자 지시 우선순위에 따라 이번 T03/G2의 두 실기기 조합을 **iPhone17 Host + iPad Client**로 변경한다. 이 변경 자체는 실행 PASS가 아니다.

## 유지하는 조건

- 같은 Wi-Fi에서 실제 기기 두 대가 직접 통신한다. Mac은 빌드·서명·설치·로그 수집에 사용하며 공유 숫자의 Host 또는 Client로 참여하지 않는다.
- 총100개 고유 요청을 처리하고 양쪽 값=호스트 승인=revision100, 각자 발송/응답50, 대기0, 거부0을 확인한다. 개발용50건 전송은 자동 입력으로 구분한다.
- 최초 로컬 네트워크 권한 요청과 실패 후 수동 재시도를 실제로 확인한다. 존재하지 않은 알림이나 수행하지 않은 조작을 추정하지 않는다.
- 기존 호스트 권위·session/request·중복 처리·세션 정리를 유지한다. T04 이후 기능은 자동 시작하지 않는다.

## 적용 범위와 한계

현재 앱은 Unity의 `iPhoneAndiPad` 대상으로 빌드한다. 이 설정은 iPhone과 iPad를 함께 지원하는 Universal 대상이다. [Unity 공식 API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/iOSTargetDevice.html)

T03 빌드 번호를4에서5로 변경하고 출력은 `Builds/T03-iPad/`로 분리한다. 이전 iPhoneOnly 빌드4와 iPhone↔Mac 실행 증거는 그대로 보존한다. 권한 문구는 iPhone 또는 iPad와 연결한다는 내용으로 바꿨고, 후처리의 단일 원본을 CounterBuild에서도 참조한다.

세로·Safe Area·전체 화면 요구를 유지하며 실제 기기를 전경에 둔 조건으로 확인한다. iPad의 창 모드까지 모두 검증했다고 보고하지 않는다. Apple은 최신 iPadOS에서 `UIRequiresFullScreen`의 동작이 창/호환 모드에 따라 달라질 수 있음을 설명한다. [Apple 공식 설명](https://developer.apple.com/documentation/BundleResources/Information-Property-List/UIRequiresFullScreen)

이번 결과는 **사용자 승인으로 변경된 G2 기준**으로 판정한다. 원래 두 iPhone 기종 사이의 화면·성능·입력 검증을 수행한 것은 아니며 그 결과는 NOT_RUN으로 보존한다. 이후 게임 화면이나 모든 Gate의 기기 기준까지 자동 변경하지 않는다.

## 실제 시험 순서

1. 같은 공용 빌드5를 두 기기에 설치하고 실행한다. 개인 Team은 기존 본인 Apple 개발 Team 선택을 유지한다.
2. iPhone에서 Host를 누르고 DEV50건을 한 번 보낸다. iPhone의 Wi-Fi IPv4와 값50을 확인한다.
3. iPad에서 그 IPv4와 사용하지 않는 포트7778로 Join한다. 처음 네트워크 권한 알림이 나타나면 허용하고 실패/재시도 가능 상태를 확인한다.
4. iPad의 포트를 실제 Host 포트7777로 고쳐 수동 Join한다. 처음 받은 전체 상태가50이고 참가자2명인지 확인한다.
5. iPad DEV50건을 한 번 보내고 양쪽 최종 숫자·승인·revision·응답·대기를 대조한다. 중복 처리와 숫자 유지 조건은 이번 빌드의 자동 시험으로 확인하며 실기기 추가 재전송은 별도 결과로 구분한다.
6. 실제 기기·앱 버전·세션·송신자·요청 수·권한/재시도 관측·사용자 화면 확인을 `docs/evidence/T03-iPad/`와 VALIDATION에 기록한다.

원본 마스터 문서의 두 iPhone 문구는 역사적 사양으로 보존한다. 이번 변경은 AGENTS·현재 PROJECT INPUT 설명·검증 기록에서 우선 적용하며 [적용 차이](INTEGRATION_ADOPTION.md)에 연결한다.

## 실제 결과 (2026-09-13)

같은 공용 빌드5의 iPhone17 Host+iPad Client에서 최초 권한·실패 후 수동 재시도·초기50 수신·총100건 승인과 상태 일치를 확인했고 사용자가 양쪽 화면도 확인했다. 이번 사용자 승인 조건의 G2는 PASS다. [원본 비공개 자료: 실행 증거 — 공개 요약](VALIDATION_SUMMARY.md)를 따른다. 원래 두 iPhone 조합은 NOT_RUN으로 보존하며 T04는 아직 시작하지 않았다.
