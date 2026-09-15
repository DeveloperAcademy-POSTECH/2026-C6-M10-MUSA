> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T10-B 구현 구조

작성 기준: 2026-09-13, 빌드 식별자 `15`, Unity `6000.5.7f1`.

이 문서는 T10-B의 구현 범위와 코드 연결을 설명한다. 실행 수와 PASS 여부는 별도 검증 기록 및 원시 증거를 확인한 뒤 기록한다. 문서와 소스의 존재만으로 Compile, 테스트, 빌드, 설치 또는 실기기 검증이 완료된 것으로 보지 않는다.

## 범위와 보존

T10-A에서 확정한 두 참가자, 방, Config, Seed와 시작 정보를 T06~T09의 기존 Host 처리 함수에 연결한다. 새 씬은 `Assets/_Project/HapioMVP/Scenes/TwoPlayerBattle.unity`다. 기존 `BattleLoop.unity`의 저장된 배치와 참조를 별도 씬으로 복제하고, 기존 씬·몬스터 Collider·카메라·공유 에셋은 보존한다.

구현 범위는 각 플레이어의 생성·조합·발사 요청, 공통 전투 상태와 개인 자원의 동기화, 초기 상태 확인, 결과와 Retry다. 좌우 전달은 T11이며 이번 씬에서도 비활성이다. 카메라 확장, 실제 방어, 자동 재접속, 3인 이상은 추가하지 않는다.

사용자 확정 규칙을 유지한다.

- 시작 구슬은 0개이며 각 플레이어의 스태미나는 100/100이다.
- Generate 한 번은 Raw 구슬 하나이며 비용은 20이다.
- 시간 회복은 3초당 20의 비율로 적용하고, 실제 유효 명중은 공격자만 5 회복한다. 최대값은 100이다.
- Yin과 Yang Raw 두 개를 조합하면 재료 두 ID는 종료되고 새 Combined ID가 하나 생긴다.
- 하단 사용 영역에서 자유롭게 조합한다. 별도 Combined 전용 영역은 없다.
- Combined를 하단에서 실제 상단 전투 경계로 넘기면 한 번 발사한다. 잡고 있는 구슬의 확대·빛 효과는 유지한다.

## 코드 연결

| 담당 | 파일 | 역할 |
| --- | --- | --- |
| 씬 준비·빌드 | `Assets/_Project/HapioMVP/Editor/GameSyncBuild.cs` | 별도 씬, 빌드 15, iPhone·iPad 공용 설정, Bonjour 선언과 출력 증거 |
| 런타임 Config | `Assets/_Project/HapioMVP/GameSync/GameRuntimeConfig.cs` | 저장된 Config의 메모리 복사본에 승인된 Host 값을 적용하고 왕복 비교 |
| 연결·초기 확인·전체 Snapshot | `Assets/_Project/HapioMVP/GameSync/T10GameSession.cs` | 로비 시작 계약, 초기 ACK, 공통 상태 발행·적용, Retry 연결 |
| 데이터 계약 | `Assets/_Project/HapioMVP/GameSync/GameSnapshot.cs` | 전체 상태와 신뢰된 연결 문맥 |
| 검증·전송·비교 | `Assets/_Project/HapioMVP/GameSync/GameWire.cs` | 64KB 제한, sender·문맥·자원·소유자·상태 검증, 비교 지문 |
| 발사·실제 피격 | `Assets/_Project/HapioMVP/Attack/AttackSession.cs` | 승인된 기존 연결에 부착하고 기존 `AttackAuthority`·Rigidbody 처리 재사용 |
| 생성·자원 | `Assets/_Project/HapioMVP/Resources/ResourceSession.cs` | 승인된 Seed, 기존 `HostResourceAuthority`, 응답과 상태 확인 |
| 조합 | `Assets/_Project/HapioMVP/Combination/CombinationSession.cs` | 기존 `HostCombinationAuthority`와 공유 구슬 Registry 재사용 |
| 전투 시계·결과 | `Assets/_Project/HapioMVP/Battle/BattleSession.cs` | 기존 `HostBattleClock`, 준비된 라운드 Start, 결과 고정 |
| 입력·표시 | `Assets/_Project/HapioMVP/Battle/T09BattleController.cs` | 기존 드래그·조합·경계 발사·ID별 2D 뷰와 Client 발사체 표시 |

이전 씬은 기존 경로를 유지한다. T10-B는 `SetAggregateMode`와 승인된 연결 부착을 선택적으로 사용한다. 게임 규칙을 Client 코드로 복제하지 않는다.

## Config 원본과 빌드 식별

저장된 원본은 `Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset` 하나다. `GameRuntimeConfig`는 원본의 메모리 복사본을 만들며, 방에서 받은 값으로 원본 에셋을 수정하지 않는다. 적용 후 `LobbyHostConfig.Capture`의 JSON을 승인된 Host Config와 비교한다. 일치하지 않으면 게임 부착을 중단한다.

T10-B 식별자는 `T10GameSession.Build = "15"`다. 이 값은 로비의 호환성 확인과 iOS 빌드 번호에 연결된다. iOS Bundle ID는 `com.wolfuraark.c6prototype`, Mac 검증 앱은 `com.wolfuraark.c6prototype.t10b.desktop`이다. Unity·렌더러·패키지 버전은 변경하지 않는다.

## 로비에서 게임으로 진입

1. T10-A가 방·참가자·버전·Config와 각자의 Ready를 확인한다.
2. Host Start가 방의 시작 계약을 확정한다. Host는 P1, 다른 참가자는 P2다.
3. 두 기기는 같은 `sessionId`, `roundId = 1`, Config 지문과 Seed를 사용하여 기존 게임 서비스에 붙는다. 게임용 연결을 따로 만들거나 새 Seed를 뽑지 않는다.
4. Host는 구슬 0개, 스태미나 100, 몬스터 HP 100, 180초의 Ready 상태를 전체 Snapshot으로 보낸다.
5. Client는 전체 Snapshot을 검증·적용한 뒤 그 revision과 비교 지문으로 초기 ACK를 보낸다. Host가 보낸 기록과 정확히 일치해야 한다.
6. 초기 확인 전에는 생성과 전투 시간이 시작되지 않는다. 확인 후 준비된 라운드를 시작한다. Start 때문에 roundId를 다시 증가시키지 않는다.

첫 로비 Host Start에는 초기 확인을 마친 뒤 Playing으로 이어지는 절차가 포함된다. 결과의 Retry는 다음 라운드를 빈 Ready 상태로 만들고 다시 초기 ACK를 받는다. Retry 이후에는 Host가 Start를 눌러 준비된 라운드를 시작한다.

## 전체 논리 Snapshot

`GameSnapshot`은 방·세션·라운드·전체 revision, Config 지문·Seed, P1/P2와 Ready, 초기 확인 여부, `hostNow`·`serverTime`, 기존 Attack·Resource·Battle Snapshot을 포함한다. 기존 서비스의 revision은 각각 독립적인 값으로 보존한다.

Host는 `LateUpdate`에서 세 서비스의 처리 후 상태를 묶는다. 실제 명중의 HP·구슬 종료, 공격자 회복, 전투 결과가 연속 처리되는 도중의 값을 즉시 전체 상태로 발행하지 않는다. 같은 세션과 라운드의 일관된 상태만 발행하며 지속적으로 일관된 상태를 만들 수 없으면 오류로 종료한다.

Client는 Host sender와 수신자 nonce를 먼저 확인한다. 세 하위 Snapshot의 문맥, 승인된 Config·Seed, 정확한 두 플레이어, 모든 owner, 보관 수, 발사체 ID, HP, 초기 상태와 결과 불변식을 검증한다. 잘못된 상태는 부분 적용하지 않는다. Attack·Resource·Battle 상태를 모두 설치한 뒤 각 서비스에 변경 알림을 준다.

같은 라운드에서는 낮거나 같은 전체 revision을 새 상태로 채택하지 않는다. 동일 revision의 재전송은 내용까지 같은 경우에만 중복으로 취급한다. 하위 서비스의 revision이 같으면서 그 내용만 바뀐 경우도 거부한다. 이전 라운드의 상태는 현재 라운드를 바꾸지 못하며, 새 라운드는 먼저 초기 Ready 상태를 거쳐야 한다.

## 비교 기준과 공통 시계

동기화 비교는 **같은 sessionId·roundId·전체 revision**을 골라 수행한다. P1은 양쪽의 P1과, P2는 양쪽의 P2와 비교한다. 두 기기의 서로 다른 Local Player 스태미나가 다르다는 사실은 오류가 아니다. 화면에 P1/P2 자원과 라운드·revision을 함께 표시한다.

비교 지문은 수신자 nonce를 전체·하위 Snapshot에서 제외하고, 구슬·발사체·플레이어 배열을 ID 순으로 정렬한 뒤 전체 논리 내용을 SHA-256으로 계산한다. clock 표본과 하위 revision도 포함한다. 숫자는 사전 논리 정밀도 1e-6의 고정 소수점 표현을 사용하고, ID·정수·ulong revision은 원값 그대로 보존한다. JsonUtility의 소수점 끝자리 변동이 비교 결과를 바꾸지 않도록 해시 경로에서 JSON 재직렬화를 제거했다. 동일 상태 지문은 정확히 일치해야 한다. 논리 수치 검증의 허용 오차는 `1e-6`이다.

실시간 화면 시간의 허용 차이는 시험 전에 **1.0초**로 고정한다. Team HP의 표시 허용 차이는 `1.0초 × teamHpDecayPerSecond`다. 현재 설정에서는 1 HP다. 시험 후 기준을 바꾸지 않는다.

전투 종료 판정은 Host의 공통 deadline과 기존 `HostBattleClock`이 담당한다. 화면은 받은 Host 시간 표본과 NGO 서버 시간의 경과를 이용한다. Client의 독립적인 게임 시계로 피해나 결과를 결정하지 않는다. Victory·Defeat에서는 확인된 시간·HP·자원을 고정한다. 기존 일시 중단 동작에 따라 자원 회복이 멈춰도 공통 전투 deadline은 계속 유효하다.

## 구슬 뷰와 Pending

각 기기는 자기 owner의 사용 가능한 구슬만 2D로 표시한다. 뷰는 OrbId를 기준으로 추가·제거하며, 전체 Snapshot이 올 때마다 기존 구슬을 다시 생성하지 않는다. 로컬 드래그 위치는 매 프레임 전송하지 않는다. Client의 3D 발사체는 표시용이며 Host의 실제 Rigidbody/Collider가 피격을 확정한다.

생성·조합·발사는 기존 요청·영수증·Query 경로를 사용한다. 승인 응답만 먼저 도착한 경우 해당 inventory/resource revision의 전체 상태가 확인될 때까지 필요한 Pending을 유지한다. 조합 재료는 둘 다 잠그며, 발사는 같은 ID를 유지한다. 승인된 2D 제거와 최종 Consumed는 구분한다.

약 3초 동안 확정되지 않은 요청은 같은 요청의 Host 상태를 Query한다. Query는 새 생성·조합·발사를 재실행하지 않는다. 약 8초까지 확정되지 않으면 세션을 오류로 종료하며, 임의로 구슬을 다시 만들거나 비용을 되돌리지 않는다. 결과·Reset 이후 이전 라운드의 Pending과 요청은 새 라운드에서 재사용하지 않는다.

## 검증 상태의 경계

실행 수·실패 수·빌드·서명 결과는 root가 원시 XML·로그·산출물 증거를 확인한 뒤 별도 검증 기록에 채운다. 이 초안은 실행 결과를 주장하지 않는다.

현재 기기에 설치되어 있던 앱은 T09 빌드 13이다. 빌드 15의 설치와 실기기 실행은 이 문서 작성 시점에 `NOT_RUN`이며, 이전 빌드의 기기 PASS를 승계하지 않는다. 빌드 14도 별도의 설치 증거 없이 실행한 것으로 기록하지 않는다. T12의 실제 두 iPhone 검증은 별도이며, Mac 두 프로세스·직접 IP 또는 과거 iPhone+iPad 결과로 대체하지 않는다.
