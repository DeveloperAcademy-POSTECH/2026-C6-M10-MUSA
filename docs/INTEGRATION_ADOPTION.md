> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

<!-- C6:P4:ADOPTION:BEGIN -->
## 2026-09-14 P4 적용 차이

사용자 후속 승인으로 기존 좌우 반발·직접 손떼기 전달·정지 수신을 새 빌드24의 자동 경계 통과·속도 보존으로 변경했다. 공통 규칙은 기존 문서를 보존한 채 최신 절로 추가했다. 원본 Editor와 다른 작업 폴더에서 구현·검증했고 기존 씬/Prefab/Config/패키지/.meta를 보존한다. Unity 공식 저장소의 고정 커밋에서 unity-cli 스킬만 설치해 실제 run/test/build에 활용했다. Pipeline 연결은 없으므로 패키지를 추가하지 않고 Editor API를 사용했다. [세부 결정](P4_IMPLEMENTATION.md)과 [실행 결과](P4_VALIDATION.md)를 따른다.
<!-- C6:P4:ADOPTION:END -->

<!-- C6:T13:BUILD20:BEGIN -->
## T13 빌드20 · 중단 처리 구현과 현재 검증

최신 사용자 지시 “실제로 구현해야할게 얼마나 남았는지 알려주고 그걸 시작해”에 따라 T13을 구현했다. 새 InterruptionBattle 씬의 빌드20에서 유효 응답8초 감시·실제 pause 뒤 수동 연결 화면 복귀·종료 사유 안내를 보완했다. 시작0개·스태미나100·생성20·시간회복20/3초·명중회복5와 기존 씬·Config·메타·사용자 변경을 보존했다.

현재 실행한 자동1,037개(기존996+신규41), Mac 두 앱 상태204쌍 일치, iPhone17+iPad mini6의 응답 중단·오류 정리·새 연결·첫 생성·정상 END는 각 범위 PASS다. T13 전체와 원문 G5는 NOT_RUN이며 과거 빌드 PASS를 승계하지 않았다. 실제 강제 종료는 AGENTS의 프로세스 강제 종료 금지에 대한 자동 승인 검토로 BLOCKED이며 실행하지 않았다. 실제 홈 이동·잠금·Wi-Fi 해제·권한 팝업·현재 빌드 상태바 검사는 NOT_RUN이다.

[적용 결정](T13_DECISION.md)·[검증 기록](T13_VALIDATION.md). 현재 결정과 증거를 우선 읽고 아래의 과거 기록은 보존한다. 다음 구현 후보는 T14 무결성·진단 감사 하나이며 자동 시작하지 않는다. 이 요청은 T13 구현 착수 허용이며 미실행 Gate를 PASS로 바꾸는 허용이 아니다. Git commit·push는 수행하지 않았다.
<!-- C6:T13:BUILD20:END -->

<!-- C6:T12:FOLLOWUP19:BEGIN -->
## T12 빌드19 · 추가 조작·승리·초기화 확인

이번 요청의 추가 검증과 기록 정리를 완료했다. 실제 수신Raw 조합/공격 거부·Combined 전달/물리피격 및 별도 정상5hit·공격자회복5·Victory·Retry·첫Generate를 확인했다. 추가 사람 조작을 요구하지 않았고 게임/앱/Config 변경·100회 반복은 없다.

현재 확인 항목은 PASS이며 전체T12/G5 최종판정은 NOT_RUN이다. 빌드18의100회PASS와 빌드19의100회NOT_RUN을 분리한다. 정확한 짧은 외곽거리 직접재현은NOT_RUN으로 남기며 과도한 손 위치 맞추기를 추가 요구하지 않는다. [상세 결과·실패 보존·다음 판정](T12_BUILD19_FOLLOWUP.md)을 따른다. T13은 시작하지 않았다.
<!-- C6:T12:FOLLOWUP19:END -->

<!-- C6:T12:BUILD19:BEGIN -->
## T12 현재 상태 · 빌드19 오른쪽 왕복 확인

빌드19의 자동996개·앱 빌드/서명·두 기기 설치/실행 및 iPhone/iPad 양쪽 오른쪽 Combined 왕복 Touch는 PASS다. 중간 iPad 취소 입력1회와 이후 정상 손떼기 승인·반환을 구분해 기록했다. 최초 빌드18 미전달 원인은 미확정으로 보존한다.

줄어든 외곽 최소거리 구간은 코드 재현/회귀44개PASS이며 실기기에서 그 짧은 거리 자체를 직접 확인한 것은 아니다. 전체T12/G5는 NOT_RUN, 다음은 T12 남은 실기기 확인 하나다. T13은 시작하지 않는다. [최신 기록과 정확한 범위](T12_CONTINUATION.md)를 따른다.
<!-- C6:T12:BUILD19:END -->

<!-- C6:T12:BUILD18:BEGIN -->
## T12 현재 상태 · 빌드18 상태바 중단 수정 완료

2026-09-13 사용자가 지정한 상태바 중단 수정은 **PASS**다. iPhone17 Host와 iPad mini6 Client에서 각각 약13.431초·11.719초 상태바를 펼쳤다가 닫았고, 시간은13.398초·11.669초 감소했다. 같은 방·세션·두 참가자를 유지했으며 pause·연결 오류가 없고 양쪽 clock/snapshot은 계속 진행됐다. 사용자는 두 기기 모두 “잘돼”라고 확인했다. 이후 같은 세션에서 실제180초 종료·Defeat/HP100/TIME0 결과를 확인했다.

자동972개(Edit886/Play86, 기존945개 모두 유지), Mac 실제 두 실행본102전달·공통상태382개 불일치0·받은Combined 실제명중/공격자만+5, iOS export·빌드·개인Team서명·두기기설치/실행은 PASS다. 첫 경계좌표 시험1실패는 보존하고 수정 후 통과했다. 좌측 가까운 구슬의 전달 거리도 보완했으나 **빌드18 실제Touch 좌측 전달은 NOT_RUN**이다.

최신 요청에 따라 이번 마무리는 상태바 수정까지다. 결과 UI·보호버튼은 변경하지 않았다. 전체 T12/G5, 실기기100회, 현재빌드 양쪽 전체조합·전달·공격·Victory/Retry, 실제background 전환은 **NOT_RUN**이다. T13은 시작하지 않았다. 다음 요청 하나는 **T12 남은 실기기 통합 검증**이며, 현재 수정의 실제Touch와 남은 전체루프를 확인해야 G5를 판단할 수 있기 때문이다.

[최종 상세 검증](T12_VALIDATION.md) · [구현](T12_IMPLEMENTATION.md) · [적용 결정](T12_DECISION.md) · [실행 절차](T12_RUNBOOK.md) · [원본 비공개 자료: 근거 목록 — 공개 요약](VALIDATION_SUMMARY.md).

아래는 이전 작성 시점 기록을 보존한 것이다.
<!-- C6:T12:BUILD18:END -->

최신 사용자는 결과 화면 정상 확인 후 상태바 중단만수정하도록범위를지정했다. 시간은기존Host절대시간을유지하며네이티브foreground비활성pause만구분한다. 초기/수신구슬의가까운방향전달거리도기존사용자피드백에따라T12에서만보완했다. [차이](T12_IMPLEMENTATION.md)와[원본 비공개 자료: 이관근거 — 공개 요약](VALIDATION_SUMMARY.md)를따른다.

<!-- T11 current status 2026-09-13 -->
## T11 좌우 전달 — 현재 상태

T11 구현·코드 검증은 PASS다. 새 `OrbTransferBattle.unity`/빌드16에서 하단 자유 조합을 유지하며 좌우 끝에서 손을 놓으면 같은 구슬을 상대의 반대 경계로 전달한다. 시작0개·스태미나100·비용20·시간회복20/3초·유효명중 공격자회복5는 유지한다.

자동914개(기존837+신규77), 실제Mac 두 프로세스 전달17회(Raw8/Combined9), 동일snapshot211개 지문 일치와 수신Combined 실제명중을 확인했다. iOS빌드16 출력·앱빌드·개인Team서명은 PASS다. 빌드16 설치·실제Touch·Bonjour/Wi-Fi·T12/G5전체는 NOT_RUN이며 과거 기기PASS를 승계하지 않는다. 첫 두 Mac 진단 실패도 보존했다.

상세 결과는 [T11 검증](T11_VALIDATION.md), 적용 차이는 [T11 구현](T11_IMPLEMENTATION.md)·[결정](T11_DECISION.md), 조작·T12용100회 분배는 [실행 절차](T11_RUNBOOK.md)를 따른다. T11 미해결 차단은 없으며 다음 요청은 T12 실기기 전체 전투 검증 하나다. 다음 Task는 자동 시작하지 않는다.
<!-- T11 current status end -->

# C6_Prototype · 신규 프로젝트 이관 기록

<!-- C6:T10B:BEGIN -->
## 현재 T10-B · 2인 게임 상태 동기화 완료

2026-09-13 **T10-B 구현·코드 검증 PASS**다. 자동 837개(Edit751/Play86), 실제 Mac 두 실행본의 생성·조합·명중·승패·Retry·180초 종료, 동일 상태 번호 3,856개 지문 일치와 최대 시간 표시 차이 약 0.051초를 확인했다. iOS 빌드15 생성·앱 빌드·개인 Team 서명도 PASS다. 빌드15 설치·실기기·G5 전체/T12는 NOT_RUN이며, 기존 iPhone 빌드13 결과를 승계하지 않는다.

새 씬은 `Assets/_Project/HapioMVP/Scenes/TwoPlayerBattle.unity`, 최종 앱 출력은 `Builds/T10-B-r3/`다. 원본 Config·이전 씬·메타데이터·과거 기록과 사용자 규칙을 보존했다. 전달·카메라 확장은 추가하지 않았다. 다음 요청 하나는 **T11 좌우 전달**이며 아직 시작하지 않았다.

[현재 검증](T10B_VALIDATION.md) · [구현 구조](T10B_IMPLEMENTATION.md) · [반복 절차](T10B_RUNBOOK.md) · [적용 결정](T10B_DECISION.md). 아래 본문은 이전 시점 기록이다.
<!-- C6:T10B:END -->


2026-09-12 사용자 최신 요청: 데스크탑 C6_Prototype 신규 프로젝트와 같은 이름 개인 비공개 GitHub 저장소를 만들고 작업 시작.

## T00 당시 적용

- 실제 루트 `/path/to/2026-C6-M10-MUSA`를 생성했다. 기존 UnityGame·signal-groove·Swift 실험·원래 C6 미러는 게임 소스 이관 대상으로 사용하지 않았다.
- `prototype-author`는 GitHub 인증 API로 확인한 개인 계정이다. 같은 이름 저장소 부재 확인 후 `C6_Prototype`을 private로 생성했다.
- 통합본 v1.0/r03 사본을 실제 Assets/Packages/ProjectSettings 옆에 배치했다. SHA-256 `7dbd2857e42da49faa1f22985d14eab456466b3659d3b1e960dea0474c143a1c`로 원본과 일치한다.
- 새 폴더에 기존 AGENTS가 없음을 확인했다. 미러의 공통 규칙을 병합한 새 AGENTS를 작성하고, “프로젝트 없음/지시만 적용/Git push 범위 밖”이라는 이전 시점 설명은 현재 요청의 신규 생성·초기 push 허용에 맞춰 갱신했다. 다른 원격 배포 권한을 추가하지 않았다.
- 이전 문서·증거는 [원본 비공개 자료: history 보존본 — 공개 요약](VALIDATION_SUMMARY.md)에 원문대로 저장했다. 활성 사양/VALIDATION/RUNBOOK은 실제 새 프로젝트 기준이다. 현재도 docs/만 사용한다.

## 초기 템플릿과 버전 차이

설치 Editor6000.5.7f1의 `com.unity.template.3d-cross-platform-17.0.14.tgz`(내부 com.unity.template.urp-blank, 3D URP)를 선택했다. 향후 상단3D/하단Orthographic와 Universal Renderer 구성을 위한 기본 환경이며 게임 기능을 추가한 것이 아니다.

| 패키지 | 템플릿 요청 | Editor 초기화 후 실제 |
|---|---|---|
| URP | 17.0.1 | 17.5.0 |
| Input System | 1.12.0 | 1.20.0 |
| Test Framework | 1.4.2 | 1.7.0 |
| UGUI | 2.0.0 | 2.5.0 |

신규 템플릿 생성 시 Editor가 설치본 기준으로 의존성을 해결했다. 수동으로 기존 프로젝트를 업그레이드하지 않았고 기능용 NGO/Transport/Discovery 패키지를 추가하지 않았다. 최종 manifest와 packages-lock을 함께 버전 관리한다. 템플릿의 AI Navigation·Multiplayer Center 등 패키지 존재는 몬스터 AI·네트워크 구현 완료를 뜻하지 않는다. 이번에 필요 없다는 이유로 기본 패키지를 임의 제거하지도 않았다.

## T00 당시 검증과 범위

실제 생성·현재 컴파일·iOS 지원 API·Readiness 결과로 T00/G0를 완료했다. 최초 환경 도구의 컴파일 오류를 숨기지 않고 수정 전 실패와 수정 후 성공을 별도로 보존했다. 데이터 기록용 Editor 도구 외에 게임 기능을 추가하지 않았다.

미설정: Portrait·Safe Area·실제 Bundle ID/Signing·T01 씬·Xcode 출력·iPhone 실행. G1~G6/AT/VT는 NOT_RUN이다. 다음 요청은 T01 하나이며 이번에는 시작하지 않는다.

원시 로그·Unity Library/캐시·개인 설정·서명 파일은 .gitignore로 제외하고 .meta·Scene·Asset·Packages·ProjectSettings·문서·정리된 증거를 초기 커밋 대상으로 한다.

## T01 후속 요청 적용 차이 (2026-09-12)

- 사용자의 “다음 작업 진행” 요청에 따라 T00 이후 T01만 시작했다. 처음 지시 적용 시점의 구현 금지와 T00의 “T01 자동 시작 금지”는 과거 범위로 보존하며, 이번 명시적 후속 요청이 현재 작업 범위를 갱신한다.
- 본인 Apple 개발 Team 개인 Team과 현재 연결 가능한 iPhone17은 사용자가 선택했다. 선택 확인을 서명·설치·실행 PASS로 해석하지 않는다.
- 전용 `IOSBuildSmoke.unity`, 빌드 표시 원본 `IOSBuildStamp.asset`, UI·Safe Area·Editor 준비/export 메뉴, EditMode2개·PlayMode3개를 추가했다. 네트워크·구슬·몬스터·정식 UI·스토어 배포는 추가하지 않았다.
- T01 개발용 앱 ID는 `com.wolfuraark.c6prototype`, 앱 버전은0.1.0을 선택했다. 앱 ID의 기존 등록을 확인했다는 뜻은 아니다. A/B는 같은 앱 ID를 유지하고 빌드 표시/빌드 번호만 `C6-T01-A / 1`과 `C6-T01-B / 2`로 구분한다.
- iOS 대상·IL2CPP·Portrait·iPhoneOnly·DeviceSDK를 적용했다. 기존 템플릿 Scene·Asset·.meta는 보존하고 T01 씬만 빌드 활성화했다. Unity·렌더러·패키지 버전은 교체하지 않았다.
- 시작 시 이미 변경돼 있던 `ProjectSettings/PackageManagerSettings.asset`와 `ProjectSettings/URPProjectSettings.asset`을 작업 전 증거로 보존했고 현재 해시가 시작 시점과 일치한다. 자동 정규화나 기존 사용자 변경을 이번 구현으로 섞어 보고하지 않는다.
- 원본 Unity에 미저장 Untitled 씬이 열려 있어 `/private/tmp/C6_Prototype_T01_Verification` 검증 사본을 사용했다. 원본 Editor를 강제 종료하거나 같은 프로젝트에 batchmode를 중복 실행하지 않았다. 관련 소스·씬·Config·패키지의 일치 증거를 결과에 연결한다.
- 같은 이름의 기존 Xcode 출력은 설정 변경 전에 검사하고 덮어쓰지 않으며 새 출력 루트를 요구한다. 반복 export 영수증에는 UTC 시각과 GUID를 붙여 기존 증거를 보존한다. Unity export·미서명 컴파일·개인 Team 서명·실제 A 실행·B 재설치를 각각 기록한다.
- 별도 Unity CLI1.0.0-beta.8 설치를 확인했으나 현재 Editor 연결0개다. 이번에는 설치 Editor의 batchmode CLI로 검증했고 추가 연결 패키지는 설치하지 않았다.
- A/B 각각 EditMode2/2·PlayMode3/3, Export 오류0/경고0, Xcode A 미서명 및 A/B 서명 빌드 종료0을 확인했다. 같은 실제 iPhone17에서 A 실행 후 B 재설치·실행과 사용자 화면 확인을 완료해 G1 PASS다. 실제 FPS와 전체 Safe Area·회전 QA는 NOT_RUN이며 T02 자동 시작 금지는 유지한다.

초기 GitHub private 저장소 생성·초기 push 이외에 App Store/TestFlight·유료 서비스나 다른 배포 범위를 추가하지 않았다. 현재 Task의 커밋·push 여부는 실제 Git 결과로 따로 기록한다.

- Unity export가 사본에 추가한 iPhone batching 설정(static1/dynamic0)은 원본과의 차이가 정확히 그 항목뿐임을 검사한 뒤 반영했다. 최종35개 관련 파일의 원본/사본 해시가 일치한다.
- AGENTS는 현재 Task 안내 한 문장만 T00에서 사용자가 요청한 T01로 갱신했다. 나머지 공통 규칙은 보존했다. 기존 문서의 T00 버전도 `docs/history/2026-09-12-t00/`에 보존했다.
- T01 코드는 로컬 작업 상태로 남겼다. 초기 저장소 생성 때 허용된 push를 후속 Task의 자동 push 허용으로 확대하지 않았다.


## T02 후속 요청 적용 차이 (2026-09-12)

- 사용자의 “다음 작업 진행해” 요청과 G1의 실제 파일·기기 증거를 먼저 확인하고 T02만 시작했다. AGENTS의 현재 작업 문장만 갱신했다.
- 기존 통신이 없어 설치 Unity가 제공하는 NGO2.13.1과 Transport6.5.0을 추가하고 실제 lock 버전을 확인했다. 기존 렌더러·입력 패키지는 교체하지 않았다.
- 별도 DirectConnectionSmoke 씬, 연결 UI·수명주기·두 참가자 승인, 직접 IP 검증, 빌드 후처리, 개발용 별도 프로세스 시험 도구를 추가했다. 게임 동기화·숫자·Relay·자동 방 검색은 추가하지 않았다.
- T01 씬과 Config/런타임을 보존했다. T01의 현재 빌드 씬 가정만 보존 씬 존재 검사로 바꾸고 PlayMode는 빌드 선택과 독립적으로 그 씬을 로드한다. T01 시험5개도 실제 재실행해 통과했다.
- 기존 사용자 설정2개는 작업 전 해시와 동일하다. T02의 iOS 설정·활성 씬과 Mac export가 추가한 Standalone batching만 차이를 확인해 반영했다. 관련63개 파일은 검증 사본과 일치한다.
- iOS 설명은 재현 가능한 후처리로 적용하고 실제 생성 Info.plist 및 서명 앱에서 확인했다. Bonjour·multicast 설정은 추가하지 않았다.
- 50개 자동 시험, 실제 별도 Mac 프로세스 시험, iPhone17→Mac 실제 LAN 연결·수동 재연결을 구분해 PASS로 기록했다. 두 iPhone·T03·G2·AT02는 아직 NOT_RUN이다.
- 사용자는 현재 연결 검증이 충분하다고 판단했다. T02에서 추가 연결 반복을 하지 않는다. 100건은 다음 T03 데이터 정합성 기준이며 연결100회 시험이 아니다. T03이나 추가 Git push는 자동 수행하지 않았다.
- T01 당시 문서는 `docs/history/2026-09-12-t01/`에, T02의 최초 컴파일/시험 구성 실패와 수정 후 결과는 `Logs/T02/` 및 검토된 `docs/evidence/T02/`에 보존했다.


## T03 후속 요청 적용 차이 (2026-09-12~13)

- 사용자의 “다음 작업 진행해”와 T02 실제 파일·50개 시험·iPhone/Mac 연결 증거를 확인하고 T03만 시작했다. 이후 “지금은 iPhone17과 Mac만 사용 가능” 답변에 따라 가능한 장비의 검증을 수행하고 원래 두 iPhone Gate와 분리했다.
- AGENTS는 현재 Task 안내 문장만 T03으로 갱신했다. 기존 공통 규칙·사용자 설정2개·T01/T02 소스/씬/증거를 보존했다. T02 문서는 `docs/history/2026-09-12-t02/`에 복사 보존했다.
- T02 세션의 읽기 전용 OwnedManager 접근자와 시험 어셈블리 참조만 보완하고, T03 권위 모델·전용4개 메시지·초기 전체 상태·숫자 UI·개발용50건/중복 전송·씬/빌드 도구·자동 시험을 추가했다. 패키지·렌더러를 교체하지 않았다.
- 실제 송신자/session/request 중복 키, 원래 처리 응답과 현재 상태 분리, 새 연결 nonce, 이전 요청 응답이 새 대기 요청을 잘못 확인하지 않게 하는 조건을 적용했다. 같은 세션 전체 기록10000개 제한은 DEMO_ASSUMPTION으로 명시하고 단일 상수를 참조한다.
- 빌드 번호4와 CounterSmoke 활성 씬만 설정 차이로 반영했다. 관련107개 파일의 원본/검증 사본 해시가 일치한다. 사본에서 Unity가 직렬화·빌드 처리한 URP 템플릿4개 차이는 기록하고 원본 에셋은 보존했다.
- 최종 자동 시험80개, 실제 두 Mac 앱의 순차/동시100건과 중복, 빌드·서명·iPhone 설치·실제 iPhone↔Mac100건을 각각 구분해 기록했다. 사용자의 DEV 버튼1회는50개의 자동 요청을 시작하며100회 수동 Tap으로 기록하지 않았다.
- 양쪽 값/승인/revision100, 각자 고유 발송/응답50·대기0과 실제 송신자0/1의 적용50건씩을 로그로 확인했고 사용자도 화면 확인을 완료했다. 두 iPhone·최초 권한·이번 실기기 실패 후 수동 재시도는 미실행으로 남겼다. G2는 BLOCKED이며 T04를 시작하지 않았다.
- 문서 승인·소스 존재를 실행 PASS로 보지 않았고 새로운 Git 커밋·push를 하지 않았다. 원시 로그/앱은 로컬 제외 경로, 검토된 XML·요약·발췌·해시는 `docs/evidence/T03/`에 보관했다.


## T03/G2 iPad 대체 요청 적용 차이 (2026-09-13)

- 사용자가 두 번째 iPhone 대신 보유한 iPad로 검증 진행을 요청했다. 최신 사용자 지시에 따라 이번 G2의 기기 조합만 iPhone17 Host+iPad Client로 변경했다.100건·권한 첫 요청·실패 후 수동 재시도는 유지했다. 원래 마스터 파일과 과거 두 iPhone 미실행 기록은 바꾸지 않았다.
- iPad(iPad14,1)/iPadOS26.2.1을 실제 발견한 뒤 신뢰 연결을 완료했다. 개발자 모드disabled를 확인해 필요한 기기 조작을 안내했고 사용자 승인 뒤 enabled·DDI 준비를 확인했다. 개인 Team은 기존 본인 Apple 개발 Team 선택을 유지했다.
- T03 대상은 iPhoneAndiPad, 빌드5, 기존 전체 화면 요구를 명시적으로 유지했다. 실제 공용 앱의 UIDeviceFamily[1,2]·Portrait·두 기기 프로필 포함을 검사했다. 권한 문구는 iPhone 또는 iPad로 바꾸고 CounterBuild가 기존 후처리 상수 하나를 참조하도록 했다. 기기 모델/OS/IP/화면 로그 외 권위·통신 로직은 변경하지 않았다.
- 기존 T03 상태 문서는 `docs/history/2026-09-13-t03-iphone-mac/`에 보존했다. 기존 사용자 설정2개·T01/T02/T03 증거31개를 유지했다. 관련107개 소스는 검증 사본과 일치하며 URP 원본4개도 보존했다.
- 빌드5에서 EditMode67·PlayMode13을 실제 재실행하고 iOS export·서명·두 기기 설치/실행을 별도로 확인했다. 호스트 고유 요청은 실제 송신자0과1의50건씩, 총100건이다. iPad 초기 상태50과 양쪽 값/승인/revision100·고유응답50·대기0을 기록했다.
- iPad의 시간 초과 Failed 이후 별도의 수동 Connecting/Connected를 로그로 확인했다. 사용자가 최초 권한 알림을 보고 허용했음을 명시적으로 확인했다. 입력 포트7778→7777은 사용자 안내/조작 조건이며 수명주기 로그가 포트 자체를 기록한다고 주장하지 않는다.
- 변경된 G2 기준은 PASS로 확정했다. 원래 두 iPhone 조합과 전체 iPad 창 모드·FPS QA는 미실행으로 구분했다. T04는 다음 요청 후보로만 기록하며 자동 시작·새 Git 커밋·push를 하지 않았다.


## T04 후속 요청 적용 차이 (2026-09-13)

- 사용자의 “다음 작업 시작해”에 따라 선행 T03/G2의 실제 파일·최종100건·권한·실패 후 재시도와 승인된 iPhone17+iPad 조합을 재확인하고 T04만 시작했다. AGENTS는 현재 Task 단락만 갱신했다.
- 이전 T03-iPad 현재 문서는 `docs/history/2026-09-13-t03-ipad/`에 복사 보존했다. 기존 Assets·Packages116개, 증거40개, 사용자 설정2개와 이전 씬·meta를 유지했다.
- 단일 화면 Config, 두 Base 카메라의 화면 분할·실제 pixelRect 좌표 변환, Overlay HUD와 안전 영역, 표시 전용 허수아비/배경/그리드, 반복 가능한 Editor 생성·빌드 도구를 추가했다. 구슬 모델·Gesture·공격·게임 자원·연결은 구현하지 않았다.
- 상단0.55만 설정 원본에 저장하고 하단을 유도한다. 0.01~0.99 제한은 DEMO_ASSUMPTION이다. Canvas 안전 영역과 전체 카메라 Viewport를 구분하며 장식은 입력을 받지 않는다.
- 검토한 설정 차이는 빌드6·T04 활성 씬·빈 사용자 레이어8~10의 C6Battle/C6Orbs/C6Background다. TagManager 직렬화 변경도 패치에 기록했다. 패키지·렌더러 교체는 없다.
- 초기 NUnit 컴파일과 새 Config 참조 오류를 실제로 발견·수정했다. 첫 실제 렌더의 머리 가림은 카메라 구도를 바꾸고 다시 빌드해 해결했다. 이전 출력은 덮어쓰지 않았다.
- 최종86개 자동 시험·Mac 렌더390×844와560×746·iOS export를 각각 PASS로 기록했다. Xcode 앱 빌드·서명·빌드6 실기기·FPS·G3 전체는 NOT_RUN이다. 캡처 저장 성공과 시각 검토를 구분했다.
- 검증 사본과181개 파일이 일치한다. 사본의 URP 템플릿4개 차이는 기록하고 원본을 보존했으며 자동 NGO/SceneTemplate 파일3개도 이관하지 않았다. 전체 프로젝트의 완전 일치를 주장하지 않는다.
- T05는 다음 요청 후보로만 남겼다. 새로운 Git 커밋·push와 배포는 하지 않았다. 실제 결과와 파일 링크는 [VALIDATION](VALIDATION.md)을 따른다.


## T05 후속 요청 적용 차이 (2026-09-13)

- 사용자의 다음 작업 요청에 따라 T04 파일·86개 시험·Mac 렌더2종·iOS 생성 근거를 재확인하고 T05만 수행했다. AGENTS 현재 Task 단락만 바꾸고 T04 문서는 history/2026-09-13-t04에 보존했다.
- 별도 OrbInputSmoke 씬, 실제 NGO Host 레지스트리의 명시적 개발 Fixture3개, ID/소유/권한 상태·로컬 상태·행동 요청 모델, Sprite/Collider2D 선택, Mouse/Enhanced Touch 중재와 예약을 추가했다. 일반 Raw5·자원·실제 발사/전달/조합·게임 RPC는 추가하지 않았다.
- 동일 Config·GUID에 수평0.18W·우세1.25·Drop0.08W와 Zone/표시 튜닝만 확장했다. Zone18%는 하단 구역 높이 가정이며 별도 Throw 거리가 아니다. 방어는 false 변수만 둔다.
- 원본 Pointer로 Zone→Swipe→Up Drop을 판단한 후 구슬·글자 표시만 Clamp한다. 중복 요청·두 재료 원자 잠금·취소·추가 Pointer·UI 시작·Held 재입력을 검증했다. 수락 후 입력 후보만 해제하고 Registry 잠금은 새 개발 라운드/세션 종료까지 유지한다.
- 임계값 경계4건, 비포커스 Mouse 시험2건, 잘못된 활성 빌드 대상의 SSAO 리소스 누락, 큰 글자·왼쪽 LOCKED 잘림을 실제로 발견해 수정했다. 실패 출력 T05/T05-v2를 보존하고 T05-v3를 최종으로 사용한다.
- 최종 EditMode137·PlayMode25, 실제 Mac Host/Debug Probe·렌더2종, iOS 프로젝트 생성을 구분해 PASS로 기록했다. 최종 두 빌드 영수증 오류·경고는0이다. 추가 Mac 직접 클릭은 도구 noWindowsAvailable로 BLOCKED, 실제 iOS Touch·Xcode 앱 빌드·서명·설치·G3는 NOT_RUN이다.
- 기존 Assets·Packages159개 중155개는 해시 보존, 기존3개 소스와 단일Config asset만 의도적으로 변경했다. 기존 meta91개·증거50개·사용자 설정2개·T04 씬은 모두 보존했다. 검증 사본과217개 일치, URP 템플릿4개 차이는 기록 후 원본유지, 자동생성3개는 이관하지 않았다.
- T06은 다음 요청 후보이며 시작하지 않았다. 새 Git 커밋·push·배포는 하지 않았다. 구체적인 근거는 [VALIDATION](VALIDATION.md)과 docs/evidence/T05에 남겼다.


## T06 후속 요청 적용 차이 (2026-09-13)

사용자의 다음 Task 요청으로 T06만 수행했다. 기존 T05 소스와 실제162개 시험·Mac 렌더·iOS 생성 근거를 먼저 확인하고 당시 현재 문서7개를 `history/2026-09-13-t05/`에 보존했다. AGENTS는 현재 Task 단락만 갱신했다.

별도 AttackSmoke 씬과 공격 권한·실제 Rigidbody/Collider·Host/Client 표시·T06 HUD·반복 빌더를 추가했다. 기존 입력 어댑터는 공통 인터페이스를 사용하도록 확장하고 T05 동작/시험/씬을 보존했다. 기존 Config 하나에 HP100·피해20·속도12·수명3·발사 기준·20Hz 필드를 추가했다. 발사 경계는 실제 렌더 결과에 따라 Origin(0,1.08,-4.5)·Width3·Radius0.165로 조정했다. 생성·조합·전달·Stamina는 구현하지 않았다.

최종238개 시험·Mac 렌더2종·별도 Host2+Client3·빌드8 iOS 생성/서명/설치/실행을 확인했다. iPhone17 Host에서 직접20회·4라운드·Reset3회와 화면을 확인해 T06/G3를 완료했다. 첫5회가 observer 연결 전에 실행되어 보존하고15회만 추가했다. 실기기 Mac observer와 두 iOS 기기 동시 공격은 이번에 실행하지 않았다. 자동/실기기와 부분 Acceptance 범위는 [VALIDATION](VALIDATION.md)에 분리했다.

기존 Assets/Packages195개 중189개 동일·의도6개 변경, ProjectSettings2개 변경이다. 기존 meta111·씬/meta12·evidence64·과거history52·사용자 설정2개를 보존했다. 원본267개 중검증 사본과263개가 같으며 기존URP4개 처리 차이는 원본을 보존하고 patch로 기록했다. 자동 생성3개는 이관하지 않았다. 정확한 근거는 [원본 비공개 자료: 소스/보존 기록 — 공개 요약](VALIDATION_SUMMARY.md)과 [원본 비공개 자료: 적용 patch — 공개 요약](VALIDATION_SUMMARY.md)를 따른다.

추가 Mac 직접 클릭은 `noWindowsAvailable`로 BLOCKED이며 iPhone 성공으로 대체하지 않았다. 이전 실패 빌드/로그와 기존 T03 Host를 보존했다. T07·Git 커밋/push·배포는 시작하지 않았다.


## 2026-09-13 · T07 사용자 자원 규칙 변경과 실행

T06의 실제 파일·238개 시험·빌드8 iPhone 직접20회 근거를 재확인하고 이전 현재 문서7개를 history/2026-09-13-t06에 보존했다. 최신 사용자 지시로 원문 최초Raw5 배치/비용5/최대5/시간 회복 금지를 시작0개·100/100·매회Raw1/비용20·시간3초당20·실제 공격자+5로 대체했다. 원문은 보존하고 변경표·단일 Config·현재 AGENTS 공통 규칙에 차이를 기록했다.

Host의 실제 송신자/연결 nonce/session/round/request/sequence를 검증한다. 일반 생성과 명시적 Debug Fixture를 분리하고 실제 소비된 Combined의 유효 hit에 공격자 보너스를 한 번만 연결했다. 연속 회복·최대치·중단 시간 제외는 문서화한 적용 방식이다. 기존 T06 기본 Fixture와12개/16KiB 제한은 유지하고 T07에서만 live64개/32KiB를 사용한다.

최종 자동 시험300개·Mac 두 비율 렌더·실제 두 프로세스 개인20/총40개 동기화·Host1+Client4 실제 명중/각+5·iOS 빌드9 export를 확인했다. 앱 빌드/서명/설치/실기기는 미실행이며 G4는T09다. 최초 태블릿 상단 패널 가림과 시험 문법/수명 조건의 수정·이전 결과를 VALIDATION과evidence에 남겼다. 기존 파일·meta·씬·증거와 사용자 변경을 보존했고 T08·커밋·push·배포는 진행하지 않았다.


## 2026-09-13 · T08 적용 차이

T07 소스301개·시험300개·원시로그20개·산출물을 재확인하고 T08만 수행했다. 이전 현재문서6개를 history/2026-09-13-t07에 보존했다. 공유소스4개에 원자조합·종류기능·표시중심Drop·명시적혼합Raw Fixture를 추가하고 별도CombinationSmoke를 만들었다. 기존Config·패키지·meta·씬·증거·사용자변경을 유지했다. 최종시험370개·Mac두비율·별도Host/Client 실제조합4/피격4·각공격자+5·iOS빌드10export를 확인했다. 빌드10실기기와T09는 미실행이다. 기존문서본문은 유지하고 [T08 검증](T08_VALIDATION.md)·[결정](T08_DECISION.md)·[절차](T08_RUNBOOK.md)를 별도추가했다. AGENTS는현재Task단락만변경하며 커밋·push·배포는하지않았다.


## 2026-09-13 T09 적용 차이

T09 Host180초·승패·Retry와 빌드12 실제 조합 간격 수정을 적용했다. 시작0구슬/100, Raw1/20, 연속20/3초·실제명중+5가 원문의 최초5개·1회복 문구보다 우선한다. 빌드11 실제Touch 조합 실패와 자동 시험의 중심 이전 Drop 누락을 보존하고, 중심/가장자리 회귀 및 중심까지 실제 이동하는 자동시험으로 보완했다. 최종 자동446개·빌드12 앱설치실행은 PASS이며 실제Core Loop/G4는 사람 재확인 대기다. 기존 문서 본문·기존meta·씬·패키지를 유지했고 현재상태 링크만 추가했다. 상세는 [T09 검증](T09_VALIDATION.md), [결정](T09_DECISION.md), [실행](T09_RUNBOOK.md)을 따른다. T10-A/B·커밋·push·배포는 진행하지 않는다.


## 2026-09-13 T09 빌드13 사용자 입력 변경

최신 요청에 따라 별도 COMBINED ONLY 구역 제거·전체 하단 조합·상단 전투 경계 자동발사·잡기 확대/호버 효과를 적용한다. 원문 Attack Zone 밴드와 T09 수평 전달 선행 판정의 충돌은 [적용 차이](T09_INPUT_REVISION.md)에 기록했다. 이전 씬·실제 Host 물리·구슬 생명주기·자원 규칙을 보존한다. 이전 빌드12 사람 확인은 조합/명중2회 범위이며 G4 전체나 빌드13 PASS로 승계하지 않는다.

빌드13 최종 실행: Edit401/Play74·Mac 화면/두참가자/실제180초·iOS 빌드/서명/설치/실행·사용자 잡기/조합/경계발사 확인 PASS. 실제 기기 정상생성5·TOUCH 조합1·MOVE발사1·물리hit1/HP80/+5를 로그로 대조했다. 전체 한 판 결과/Retry·G4는 별도 미확인이다. [원본 비공개 자료: 빌드13 실제 근거 — 공개 요약](VALIDATION_SUMMARY.md)를 따른다.


## 2026-09-13 T09 실기기 Core Loop / G4 완료

추가 소스 변경 없이 빌드13에서 정상 생성·같은음양거부·터치조합/실제명중5·각회복5·Victory와180초설정의Defeat·결과고정·Retry·첫Raw1/비용20을 사람 응답과 실제 로그로 대조했다. 통합본5장/903행이 정한 한기기 명시적 개발모드 G4만 PASS로 갱신하며 G5/T12의 두기기최종AT를 완료 처리하지 않는다. 이전 문서와 입력확인 증거는 보존한다. [현재 검증](T09_G4_VALIDATION.md), [원본 비공개 자료: 기기 근거 — 공개 요약](VALIDATION_SUMMARY.md). T10-A/B를 자동 시작하지 않았다.

## 2026-09-13 T10-A 후속 적용

T09/G4 실기기근거를 확인하고 지정 T10-A 하나를 구현했다. 새 RoomLobby 씬과 선택적 연결승인·Bonjour·Config ACK·Ready·시작계약을 추가했다. 기존AGENTS5행만현재상태로갱신하고README/PROTOTYPE_SPEC/VALIDATION/IOS_RUNBOOK앞에새상태를추가하며원래본문을보존했다. 게임규칙·단일Config·이전씬/meta·과거증거는덮지않았다. 실제실행과실패수정·원본/사본차이는 docs/T10A_VALIDATION.md 및 evidence/T10-A를따른다. 자동751개/Mac9개/iOS14앱서명PASS와실기기NOT_RUN을분리한다. 다음T10-B는별도요청이다.
