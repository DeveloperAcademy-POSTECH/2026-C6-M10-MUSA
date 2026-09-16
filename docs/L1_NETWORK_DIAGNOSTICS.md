# L1 로컬 네트워크 진단 · 구현과 확인 방법

작성일: 2026-09-15. 범위: [호환성 개선 계획](LOCAL_NETWORK_COMPATIBILITY_PLAN.md)의 **L1 진단·전송 경로 검증**.

상태: **L1 완료. L2 게임 연결 통합은 미시작.**

L1은 핫스팟에서 실패하는 지점을 작은 데이터 왕복으로 나누어 확인하는 단계다. **게임 방 입장이나 게임 규칙을 수정한 단계가 아니다.** 인터넷·클라우드 인증 없이 기존 Bonjour·NGO·Unity Transport를 사용한다. Photon·Relay·새 패키지와 N1의 custom UDP 인터페이스는 추가하지 않았다.

## 1. 현재 확인된 결과

검증 기준은 Unity6000.5.7f1 / NGO2.13.1 / Transport6.5.0이다. 아래 실행과 과거 빌드24의 게임 검증은 별개다.

| 검사 | 상태 | 확인 근거와 범위 |
|---|---|---|
| 최초 컴파일 시도 | FAIL · 이후 수정 | `editmode-01.log`: BuildReport의 int 값을 기록용 uint에 대입한 컴파일 오류. 기록 필드를 int로 수정한 뒤 다시 실행 |
| 수정 후 컴파일·L1 EditMode | PASS | `editmode-02.xml`: 실행36, 성공36, 실패0, 무시0. 2026-09-15 실행 |
| Mac stock UTP 데이터 왕복 | PASS | 위 XML의 `L1TransportCapabilityTests`18개에 실제 IPv4·IPv6 loopback과 자기 IPv6 link-local 인터페이스 왕복 포함 |
| Bonjour 주소 해석·수명 기본 검사 | PASS | 위 XML의 `L1BonjourProbeTests`18개. 네이티브 주소 구조를 만든 단위 검사와 비활성 객체 수명 검사이며, 실기기 방 검색 성공을 뜻하지 않음 |
| iOS Xcode 프로젝트 출력 | PASS | `l1-build-iOS.json`: Succeeded·오류0·경고13. 빌드25·Bonjour 선언·P4 전경 처리 후처리 기록 확인 |
| iOS 앱 빌드·서명 | PASS | `xcode-build-01-private.log`: BUILD SUCCEEDED. `signed-build.json`: 실제 앱 번호25·기존 Bundle ID·서명 검사 확인 |
| iPhone 17·iPad mini6 설치·실행 | PASS | 양쪽 설치·실행 명령 성공. 각각 기기에서 수집한 JSON에 build25·READY 기록. 화면 사용성 및 핫스팟 통신 성공과는 별개 |
| Mac 진단 앱 빌드·화면·Host 바인딩 | PASS | `l1-build-StandaloneOSX.json`: 오류0·경고15. 진단 화면·Host 버튼·RAW 및 UTP의 양 계열 수신 바인딩 확인 후 종료 |
| 실제 iPhone↔iPad IPv6 데이터 왕복 | PASS | 빌드25의 양쪽 기기 JSON. IPv6 stock 및 scoped UTP의 연결·임의 문자열 왕복 성공. iPhone Host의 echo 기록과 대조 |
| 핫스팟 환경에 대한 L1 Gate | PASS | 사용자가 iPhone 개인용 핫스팟 제공·iPad 연결 및 CHECK COMPLETE 화면을 확인. 양쪽 실제 데이터 왕복 기록과 결합해 L1 조건 충족 |
| 게임 입장·Ready·플레이·다인 회귀 | NOT_RUN | 진단 앱은 게임을 실행하지 않음 |

36개는 L1에 지정한 검사 수다. 기존 전체 회귀 검사나 다른 플랫폼 전체 검사가 통과했다는 뜻이 아니다. 실행 XML·로그·상세 주소가 담긴 원시 자료는 로컬에 보관하고, 공개 문서에는 파일명·검사 수·결과만 남긴다.

검사와 양 플랫폼 빌드에 사용한 L1 소스·`.meta`·공유 선언18개는 실행 후에도 바이트가 같았다. 파일별 SHA-256 목록을 정렬한 JSON의 SHA-256은 `de63fe4b1d161295fe69ce45dc4e780d6397e8ecefd9d318a33f0aaedfd6729a`다. manifest·lock·저장된 씬·ProjectSettings의 기준 파일도 동일했다. Unity Import가 임시 작업 폴더의 URP 설정3개를 직렬화한 차이는 보존하고 검토 대상 코드 이관에서 제외했다. 위 빌드 결과는 해당 독립 작업 폴더 실행 결과이며, 실제 작업 루트를 새로 Import한 검증을 대신하지 않는다.

Mac 검사에서 확인된 사실은 다음과 같다.

- stock IPv4 수신기는 IPv6 Client를 받지 않았고, 반대 조합도 연결 시간 초과로 끝났다.
- 별도 stock IPv4·IPv6 수신기를 같은 포트에 열고 각각 왕복할 수 있었다. 이것은 두 개의 `NetworkDriver` 시험이며, 기존 NGO의 단일 driver 통합이 완료됐다는 뜻은 아니다.
- 각 왕복에서257바이트의 내용을 비교하고 추가30회 update 동안 요청·응답이 각각1개인지 확인했다.
- 기본 주소 인코더는 IPv6 scope를 보존하지 않았다. scope를 보존한 endpoint를 stock UTP에 전달한 Mac 자기 link-local 왕복은 성공했다.

이후 실제 iOS IL2CPP 앱에서도 다음 결과를 수집했다. 2026-09-15 20:37 KST, iPhone 17 Host와 iPad mini6 Client의 같은 진단 빌드25다. 주소는 공개하지 않고 후보 번호로만 구분한다.

| 후보 | 경로 | 결과 | 관찰 |
|---|---|---|---|
| 1 · IPv4 | RAW / UTP_STOCK | FAIL / FAIL | 각각3초 내 일치하는 echo 또는 UTP 연결 없음 |
| 2 · non-link-local IPv6 | RAW / UTP_STOCK | FAIL / PASS | RAW의 출처 일치 검사를 통과한 응답 없음. UTP는 약0.17초에 연결·왕복 성공 |
| 3 · link-local IPv6 | RAW | PASS | 인터페이스 정보 포함, 약0.05초 왕복 |
| 3 · 같은 주소 | UTP_STOCK | FAIL | 인터페이스 정보0, 3초 내 연결 없음 |
| 3 · 같은 주소 | UTP_SCOPED | PASS | 검색에서 얻은 인터페이스 정보 보존, 약0.23초에 연결·왕복 성공 |

Host에는 IPv6 RAW echo7회·UTP echo2회가 기록됐다. 후보2 RAW는 Host 수신이 있었더라도 Client의 주소·포트·식별 문자열 일치 조건까지 충족됐다고 확인할 수 없다. 이를 IPv6 OS 통신 차단의 증거로 해석하지 않는다. 반대로 후보3의 같은 주소·같은 UTP 포트에서 scope만 바꾼 비교는 scope 보존의 필요성을 뒷받침한다. 3초는 진단용 제한으로, 모든 환경의 영구 접속 불가나 게임의8초 시험 결과를 뜻하지 않는다.

사용자는 이 시험의 네트워크가 iPhone 개인용 핫스팟이며 iPad가 그 핫스팟에 연결돼 있었다고 확인했다. 데이터는 두 앱의 직접 소켓으로 오갔으며 Mac을 중계 서버로 사용하지 않았다. 개발용 케이블은 연결된 상태였고, 케이블을 분리한 재확인은 NOT_RUN이다. L1 PASS는 성공하는 경로를 확보했다는 뜻이다. 위7개 경로 결과는 성공3·실패4 그대로 보존하며, 모든 후보가 성공했다고 바꾸지 않는다.

기본 UTP와 작은 endpoint 보완으로 실제 기기 데이터 경로를 확보했다. 기존 NGO Host에 IPv4·IPv6를 함께 수용하는 통합은 아직 하지 않았으며, 모든 기기의 동작이나 게임 입장 성공도 보장하지 않는다.

## 2. 코드에서 확인할 위치

아래 링크는 저장소 안의 실제 소스다. 먼저 함수 이름으로 검색하고, 이어 호출 위치를 따라가면 된다.

| 파일 | 역할과 먼저 읽을 함수 |
|---|---|
| [L1NetworkProbeBuild.cs](../Assets/_Project/HapioMVP/Editor/L1NetworkProbeBuild.cs) | `ExportIOS`, `BuildMac`, `Build`: 출력 경로·활성 플랫폼 확인, 진단용 define·앱 번호 적용, 권한 문구·Bonjour 서비스와 출력 기록 확인 |
| [L1NetworkProbe.cs](../Assets/_Project/HapioMVP/Lobby/L1NetworkProbe.cs) | `Bootstrap` → `Start` → `Host` 또는 `Browse`: 진단 화면 구성. `CheckRaw`, `CheckUtp`가 경로별 요청과 응답을 비교 |
| [L1BonjourDiscovery.cs](../Assets/_Project/HapioMVP/Lobby/Discovery/L1BonjourDiscovery.cs) | `StartAdvertise`, `StartBrowse`, `GetAddresses`, `Tick`: 진단 Host 검색, A·AAAA 수집, 인터페이스별 주소·TTL과 네이티브 작업 정리 |
| [L1BonjourAddressCodec.cs](../Assets/_Project/HapioMVP/Lobby/Discovery/L1BonjourAddressCodec.cs) | `TryDecode`: Apple의 IPv4·IPv6 주소 구조 해석. link-local의 scope가 없으면 검색 콜백의 인터페이스 번호 사용 |
| [L1TransportEndpoint.cs](../Assets/_Project/HapioMVP/Networking/L1TransportEndpoint.cs) | `TryParse`, `WithScope`, `GetScopeId`, `GetAddress`: 숫자 IPv6 scope를 UTP endpoint에 보존 |
| [L1TransportCapabilityTests.cs](../Assets/_Project/HapioMVP/Tests/Networking/EditMode/L1TransportCapabilityTests.cs) | 실제 Mac 소켓·stock UTP 왕복, 주소 계열의 수신 제한, scope 보존 확인 |
| [L1BonjourProbeTests.cs](../Assets/_Project/HapioMVP/Tests/Lobby/EditMode/L1BonjourProbeTests.cs) | 주소 계열·scope 복원, 잘못된 주소 구조 거부, 진단 서비스 분리 확인 |

기존 게임의 `DirectConnectionSession`이나 `T10GameSession`으로 진단 경로를 연결하지 않았다. 기존 Bonjour 네이티브 선언에는 IPv6 조회에 필요한 값이 추가되었지만, 게임의 주소 선택 로직을 L1 검색기로 교체하지 않았다.

## 3. 진단 화면이 실행되는 조건

앱 이름은 **C6 Network Check**, 진단 앱 번호는 **25**다. 저장된 게임 씬과 게임 프로토콜 기준은 빌드24다. 새 앱 번호를 게임 검증 완료 번호로 해석하지 않는다.

`C6_L1_PROBE`와 Development Build 조건이 함께 있을 때 `Bootstrap`이 실행된다. 저장된 P4 씬의 루트를 런타임에 비활성화하고 진단 화면을 만든다. 일반 게임 빌드에서는 이 define을 넣지 않는다. 빌드 도구는 `extraScriptingDefines`로 해당 출력에만 define을 전달하므로 프로젝트 공통 Scripting Define Symbols에 수동으로 추가할 필요가 없다.

Host가 여는 경로는 다음과 같다.

| 경로 | 수신 포트 | 비교 목적 |
|---|---:|---|
| RAW UDP IPv4·IPv6 | 28011 | Unity Transport 이전에 OS 소켓 데이터 왕복이 가능한지 확인 |
| stock UTP IPv4 | 28012 | 기존 패키지의 IPv4 연결·데이터 왕복 확인 |
| stock UTP IPv6 | 28013 | scope 유무에 따른 기존 패키지의 IPv6 동작 비교 |

진단 전용 Bonjour 서비스는 `_c6l1._udp`다. 게임 방 광고와 섞지 않는다. Client는 주소 계열이 한쪽으로만 몰리지 않게 최대8개 후보를 고르고, 후보마다 RAW → UTP_STOCK → 필요 시 UTP_SCOPED 순서로 시험한다. 각 경로의 제한은3초이며 기존 게임의 연결·응답 제한을 바꾼 값이 아니다.

응답은 요청에 들어 있던 임의의 식별 문자열과 같아야 한다. RAW는 응답 출처도, UTP는 해당 연결도 확인한다. **소켓 바인딩, UTP Connected 이벤트, 데이터 왕복 PASS를 각각 구분**한다.

## 4. 직접 출력하고 실행하기

기존 [빌드 안내](../BUILD_README.md)의 Unity·Xcode·서명 준비를 먼저 따른다. 같은 프로젝트가 Editor에서 열려 있으면 저장하고 정상 종료한 뒤 batchmode를 사용한다. 실행 중인 Editor 옆에 중복 batchmode를 띄우거나 Library·lock을 삭제하지 않는다.

진단 빌더는 기존 P4 `Prepare()`로 씬과 설정을 확인한다. 독립 검증 checkout에서 실행하고, Editor가 저장한 설정 변경은 최종 파일 비교로 확인한다. 아래 명령은 **저장소 루트에서 실행하는 iOS 출력 예시**다. 출력 이름은 매 시도 새 값으로 바꾼다.

```bash
C6_L1_PROJECT="$(pwd)"
C6_L1_UNITY='/Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app'
export C6_L1_OUTPUT_ROOT="$C6_L1_PROJECT/Builds/L1/manual-ios-01"
mkdir -p "$C6_L1_OUTPUT_ROOT"

env -u UNITY_RUN_TIMEOUT -u UNITY_TEST_TIMEOUT -u UNITY_BUILD_TIMEOUT \
  unity build "$C6_L1_PROJECT" --editor-path "$C6_L1_UNITY" \
  --format json --target iOS \
  --execute-method C6.Editor.L1NetworkProbeBuild.ExportIOS \
  --allow-dirty-build --no-tail \
  --log-file "$C6_L1_OUTPUT_ROOT/ios-export.log"
```

`C6_L1_OUTPUT_ROOT`는 **Unity 프로세스를 시작할 때** 전달해야 한다. 출력의 `iOS` 폴더가 이미 차 있으면 빌더가 중단한다. 이전 결과를 지우지 말고 새 출력 경로를 지정한다.

완료 판단은 프로세스 종료만으로 하지 않는다. 로그의 `C6_L1_EXPORT_COMPLETE`, `l1-build-iOS.json`의 성공 결과, `iOS/Unity-iPhone.xcodeproj`, 앱 번호와 권한 항목을 함께 확인한다. 이후 Xcode에서 자신의 Team·실기기를 선택해 빌드·서명·설치한다. 같은 새 출력으로 양쪽 기기를 설치한다.

위 명령은 설치된 Unity CLI 1.0.0-beta.8에서 실행한 형태다. CLI가 batchmode와 종료 인자를 관리하므로 직접 중복 추가하지 않는다. 자동 강제 종료 timeout도 사용하지 않는다.

Mac 진단 앱은 별도 출력 경로에서 위 명령의 플랫폼을 `--target StandaloneOSX`, 실행 함수를 `C6.Editor.L1NetworkProbeBuild.BuildMac`으로 바꾼다. 결과는 `macOS/C6NetworkCheck.app`이며 출력 기록은 `l1-build-StandaloneOSX.json`이다. Mac 빌더의 로컬 ad-hoc 서명은 App Store 배포 서명과 다르다.

실기기의 짧은 확인 순서는 다음과 같다. 위 결과는 이 절차를 한 번 수행한 증거이며, 다른 환경에서 다시 실행할 때는 별도 기록을 남긴다.

1. iPhone이 핫스팟을 제공하고, iPad는 그 핫스팟에 연결한다. 두 진단 앱의 화면을 켜 둔다.
2. iPhone에서 `HOST — keep open`을 누른다. IPv4·IPv6별 `HOST_BIND` 결과를 확인한다.
3. iPad에서 `FIND HOST`를 누른다. 발견된 진단 Host를 한 번 선택하면 주소별 시험이 자동 진행된다.
4. 실제 로컬 네트워크 권한 알림이 나오면 사용자가 응답한다. 권한 응답 이후 필요하면 검색을 다시 시작한다.
5. `CHECK COMPLETE`와 경로별 결과를 확인한다. `STOP / CANCEL`은 대기와 소켓을 정리한다. 앱이 실제로 중단되면 진단도 멈추므로 전경 복귀 후 역할을 다시 선택한다.

직접 주소 시험은 검색 문제와 데이터 경로 문제를 나눌 때 사용한다. 상대 진단 Host에서 확인한 주소를 입력하며, Client 자신의 IP나 설정의 라우터 주소를 Host 주소로 추측하지 않는다. link-local IPv6는 현재 기기의 인터페이스 번호까지 필요하다.

## 5. 결과를 읽고 유지보수하는 예

| 관찰 | 다음 확인 |
|---|---|
| Host가 목록에 없음 | Bonjour 광고·검색·해석 단계와 권한 기록 확인. 이것만으로 게임 UDP 차단을 확정하지 않음 |
| RAW는 PASS, UTP_STOCK은 FAIL | 해당 계열의 UTP Host 바인딩·endpoint·오류 코드 확인 |
| link-local에서 RAW·UTP_SCOPED는 PASS, UTP_STOCK은 FAIL | scope 보존 차이가 실제 경로 성공과 연결되는 증거. 같은 조건 재시작으로 확인 |
| UTP Connected지만 왕복 FAIL | 데이터 송수신·제한 시간·기기 중단 기록 확인. 연결 이벤트만으로 PASS 처리하지 않음 |
| 모두 FAIL | Host 수신·같은 네트워크·권한·인터페이스·네트워크 차단을 분리. 대기 시간만 늘려 해결됐다고 기록하지 않음 |

`L1TransportEndpoint`를 수정할 때는 Unity Test Runner의 EditMode에서 두 L1 테스트 클래스를 선택해 실행한다. 실제 실행 수·실패·무시를 확인하고 XML을 새 이름으로 남긴다. 테스트가 없는 환경이나 IPv6 인터페이스가 없어 Ignore된 결과는 해당 항목의 PASS가 아니다.

scope는 IPv6 주소가 사용할 로컬 인터페이스를 가리킨다. UTP의 기본 문자열 인코더는 이를 보존하지 않아, 현재 helper는 공개 `NetworkEndpoint.Transferrable`의 Baselib 주소 구조에서20바이트 위치의4바이트 값을 읽고 쓴다. 이는 설치 버전에 한정한 작은 보완이다.64바이트 크기 검사는 크기 변화만 잡으므로 Unity·UTP 변경 시 native 헤더·단위 검사·iOS IL2CPP 왕복을 다시 확인해야 한다. 문자열 왕복 검사만 통과했다고 실기기 전송을 승인하지 않는다.

진단은 `Application.persistentDataPath` 아래 `L1NetworkChecks/<실행ID>.json`에 경로·계열·scope·시간·결과를 저장한다. 상세 주소도 포함되므로 원본을 공개 저장소에 올리지 않는다. 공개 보고에는 예를 들어 “동일 빌드, IPv6 scoped UTP 왕복1회 성공, 다른 경로 시간 초과”처럼 필요한 결과만 옮긴다.

## 6. 다음 단계의 경계

**다음에 요청할 작업은 L2 「탐색·참가 통합」이다. 아직 시작하지 않았다.** 실제 핫스팟에서 확인한 IPv6·scope 처리와 IPv4 호환 경로를 기존 NGO Host 수신, 방 검색·후보 재시도·승인·최초 게임 상태에 연결한다. 기존 NGO의 단일 driver 구성에 두 주소 계열을 수용하는 방식을 별도로 구현·검사해야 한다. 일반 Wi-Fi와 인터넷 없는 LAN의 새 방 참가, 최대5인 게임, 구슬 전달·조합·피격·결과·Retry는 이후 같은 수정 빌드로 따로 검증한다.

Mac 자기 주소로 성공한 시험은 휴대기기 간 통신을 대신하지 않는다. 같은 네트워크 이름이어도 장치 간 통신을 실제로 차단하는 정책은 로컬 앱 수정으로 우회할 수 없다. 진단 결과 없이 특정 권한·통신사·차단 정책을 원인으로 단정하지 않는다.
