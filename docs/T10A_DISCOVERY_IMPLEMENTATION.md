> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T10-A Bonjour 탐색 구현 기록

2026-09-13 작성. 방 발견 정보는 참가 권한이 없는 안내 값이다. 실제 protocol/build/Config 교환과 Host의 참가·Ready·Start 판정은 Lobby handshake에서 다시 검증한다. 이번 문서는 Discovery 코드와 독립 Mac 시험의 범위만 기록하며 Unity 앱 빌드·실기기 성공을 대신하지 않는다.

- 시스템 Bonjour의 고정 `_c6hapio._udp` / `local.` 서비스만 사용한다. 외부 서버, Relay, 패키지, 별도 multicast socket을 추가하지 않는다.
- `DNSServiceRegister` TXT에 room id/name/protocol/build/Config SHA-256/status/participants/heartbeat를 넣는다. 실제 UDP port는 SRV이며, 주소는 `DNSServiceResolve` 후 해당 interface의 `DNSServiceGetAddrInfo`로 IPv4를 얻는다. 임의 TXT address를 신뢰하지 않는다.
- 탐색은 `DNSServiceBrowse`, TXT 변화는 `DNSServiceQueryRecord`로 감시한다. TXT 교체 중 오래된 레코드의 제거 callback은 새 방을 지우지 않는다. Bonjour PTR goodbye는 해당 interface 항목을 즉시 지우며, 다른 interface의 같은 방이 남으면 그 주소로 계속 표시한다.
- Host TXT heartbeat 3초, 마지막 실제 TXT 수신 후 12초 만료, 최초 resolve deadline 8초, 최대 service 32개는 `DEMO_TUNING_VALUE`다. heartbeat 없는 이전 캐시 항목은 12초 뒤 제거된다. 새 heartbeat가 오면 다시 표시한다.
- main thread의 `Tick`이 DNSService socket을 `poll(0)`로 확인한 뒤 응답을 처리한다. 정적 AOT callback만 사용한다. callback 도중 reference를 해제하지 않으며, 처리 후 native ref와 GCHandle을 함께 해제한다. 중지·갱신·재시작·Dispose가 이전 목록/resolve/query를 정리한다. Dispose와 callback의 다른 thread 접근을 허용하지 않는다.
- codec는 제한 길이, 엄격한 UTF-8, 중복 key, 누락/잘못된 메타데이터를 검사한다. 만원·다른 버전·Playing 방도 목록에 남겨 Lobby에서 실패 이유를 설명할 수 있다. 직접 IP 경로는 별도다.

## 설치된 Apple SDK 적용

Apple의 [TN3179](https://developer.apple.com/documentation/technotes/tn3179-understanding-local-network-privacy)와 [dnssd 안내](https://developer.apple.com/documentation/dnssd)에 따라 iOS 및 Mac 앱 Info.plist에 `NSLocalNetworkUsageDescription`과 `NSBonjourServices = ["_c6hapio._udp"]`를 적용한다. 고정된 선언 서비스의 표준 Bonjour 등록·검색은 임의 Bonjour 서비스 탐색이나 직접 UDP multicast/broadcast와 다르므로 이 구현에는 `com.apple.developer.networking.multicast` entitlement가 필요하지 않다. 권한 거부 직후에는 실패할 수 있어 설정에서 허용 후 Refresh/재시작 경로를 둔다.

Xcode 26.6의 실제 `dns_sd.h` 주석은 TXT 감시에 `DNSServiceResolve`를 반복하지 말고 `DNSServiceQueryRecord`를 사용할 것을 명시한다. callback 타입·port의 network byte order·reference 해제 규칙도 이 설치 헤더를 확인했다.

이 SDK에는 `libdns_sd.tbd`가 없다. 공개 DNSService 심볼은 `libSystem.B`에서 재내보내므로 macOS P/Invoke는 `/usr/lib/libSystem.B.dylib`, iOS IL2CPP는 `__Internal`을 사용한다. 추가 native plugin 또는 존재하지 않는 tbd를 넣지 않는다. iPhoneOS SDK의 `usr/lib/libSystem.B.tbd`에 해당 DNSService 심볼이 있으며, 필요한 DNSService 9개와 poll을 모두 참조한 arm64 iOS 15 최소 타깃 임시 C 프로그램이 기본 libSystem 링크만으로 컴파일·링크됐다. 이는 서명 앱 빌드나 iOS 실행 근거가 아니다.

## 독립 실행 확인

스테이징 소스를 Unity 6000.5.7f1에 포함된 C# compiler로 컴파일했다. 같은 Unity Mono 런타임에서 임시 Mac 프로그램의 서로 다른 광고/탐색 객체가 실제 시스템 Bonjour를 사용했다.

| 범위 | 상태 | 근거 |
|---|---|---|
| Discovery C# 독립 컴파일 | PASS | `/private/tmp/C6.Discovery.dll` |
| 순수 codec/catalog·수명 assertion 49개 | PASS | 임시 reflection runner, Unity Test Runner와 구분 |
| 실제 Mac 자동 발견 및 IPv4/SRV port | PASS | `HOST_IPV4:27777`, 고유 임시 room id |
| participants 2 / Playing TXT 갱신 | PASS | native smoke r2 |
| heartbeat 중단 12초 만료 / 재개 후 재발견 | PASS | 조기 삭제가 아닌 11.5초 이상 경과를 별도 assert |
| goodbye / cancel / refresh / 재등록 / op 해제 | PASS | 마지막 광고·탐색 native op 각각 0 |
| arm64 iOS DNSService 기본 링크 | PASS | 임시 C link smoke; 앱/서명/기기 실행 제외 |
| Unity EditMode / PlayMode | NOT_RUN | root 통합 후 별도 실행 필요 |
| 실제 Mac 앱간 발견→Lobby Join | NOT_RUN | root 통합 후 별도 실행 필요 |
| iPhone 자동 발견/참가 | NOT_RUN | 실제 앱 설치·권한·기기 증거 필요 |

원시 로그는 `/private/tmp/c6-t10-discovery-native-smoke-r2.log`, `/private/tmp/c6-t10-discovery-standalone-assertions.log`, `/private/tmp/c6-dnssd-ios-link-smoke.log`에 있다. 초기 smoke는 TXT 교체 중 제거 callback 문제를 드러냈으며, 현재 r2는 수정 후 16.49초 실행으로 완료됐다. 첫 Mac 호출은 존재하지 않는 libdns_sd 경로로 실패했으며 설치 SDK/런타임의 libSystem 경로로 수정했다. 미실행을 PASS로 기록하지 않는다.

## 통합 API

`C6.Prototype.Lobby.Discovery.BonjourRoomDiscovery`를 main thread에서 생성하고 매 프레임 `Tick()` 또는 같은 단조 시계의 `Tick(double)`을 호출한다. `StartBrowse()` / `Refresh()` / `Advertise(RoomAdvertisement)` / `UpdateAdvertisement(RoomAdvertisement)`는 bool을 반환하고 `StopBrowse()` / `StopAdvertising()` / `Dispose()`는 void다. `Changed`와 `Failed(string)` 이벤트는 native callback 처리와 정리가 끝난 후 main thread에서 전달된다. Host는 메타데이터가 변할 때만 UpdateAdvertisement를 호출하며 정기 heartbeat는 Tick이 보낸다.

`RoomAdvertisement`의 `ConfigHash`는 64자리 SHA-256 hex이고 `Port`는 ushort다. `Rooms`는 interface별 결과를 room id로 중복 제거한 읽기 전용 snapshot이며, 각 `DiscoveredRoom`의 Advertisement는 복사본이다. UI는 `AdvertisingConfirmed`, `IsBrowsing`, `IsAdvertising`, `LastError`를 구분하고 discovery 정보만 보고 Ready나 Start를 승인하지 않는다. 세션 종료·배경 전환 정리는 Lobby controller가 Stop/Dispose를 호출한다.
