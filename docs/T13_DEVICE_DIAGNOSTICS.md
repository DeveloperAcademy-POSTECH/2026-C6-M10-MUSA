> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T13 iOS development diagnostics (build 20)

On actual iOS, launch with explicit environment `C6_T13_DEVICE_RUN=<run-token>`. The CLI argument `-c6T13DeviceRun <run-token>` is also parsed when available; args-only activation produced no status in the first device attempt. Ordinary launches with neither setting remain inactive.
Run and command IDs accept 1–80 ASCII letters, digits, hyphens or underscores. Use fresh, unique IDs. Ordinary launches attach no component and perform no actions. Development iOS only; Editor/Mac builds do not attach or run it.

App data relative paths:
- `Documents/T13/<run>/command.json`: external explicit command
- `Documents/T13/<run>/status.json`: atomic live observation every 0.5 seconds
- `Documents/T13/<run>/result-<id>.json`: RUNNING reservation persisted before action/context validation; terminal PASS or FAIL, never replayed
- `Documents/T13/<run>/events.jsonl`: append-only boot/command and actual Unity lifecycle callbacks
- `Documents/T13/<run>/<id>.png`: optional actual device screenshot

Every command also requires `expectedBoot` equal to the current `status.json` boot value. This prevents an unreserved old command from running after a process relaunch. All examples below require replacing `CURRENT_BOOT_FROM_STATUS` with that actual value.

Every result echoes the complete command and SHA-256 of exact command JSON UTF-8 text as `commandHash`; `before` and `after` are observations. Boot IDs distinguish process relaunches in one run. Result PASS means its particular command completed, not the whole T13 acceptance gate. A RUNNING reservation found after restart becomes FAIL / PROCESS_ENDED_BEFORE_COMPLETION / NOT_REPLAYED. Reused ID/different payload never executes. Malformed unsafe IDs are rejected without producing an unsafe filename.

Example create:
```json
{"schema":1,"id":"create-001","run":"t13-ios-01","build":"20","expectedBoot":"CURRENT_BOOT_FROM_STATUS","action":"createRoom","expectedSession":"","expectedRoom":"","expectedRound":0,"name":"C6 T13","port":"7777","screenshot":true}
```
Create/join require fully empty current context and the connection ready to start. Join:
```json
{"schema":1,"id":"join-001","run":"t13-ios-01","build":"20","expectedBoot":"CURRENT_BOOT_FROM_STATUS","action":"joinDirect","expectedSession":"","expectedRoom":"","expectedRound":0,"address":"HOST_IPV4_FROM_STATUS","port":"7777"}
```

`ready`, `start`, `end`, `holdPeerResponses`, and `generateOnce` require `expectedSession`, `expectedRoom`, and `expectedRound` exactly matching the latest observation. Lobby round is 0 before Start, gameplay normally 1. A ready command rejects an already-ready state without toggling it off. `start` uses Lobby Start before attachment or prepared-round Start after attachment. No reset/seed/time/HP/stamina setters are provided.

Example response fault (only explicit fault injection):
```json
{"schema":1,"id":"hold-001","run":"t13-ios-01","build":"20","expectedBoot":"CURRENT_BOOT_FROM_STATUS","action":"holdPeerResponses","expectedSession":"ACTUAL_SESSION","expectedRoom":"ACTUAL_ROOM","expectedRound":1,"value":true}
```

Capture is read-only and may omit context. If `expectedSession` is supplied, all 3 context fields must match.
```json
{"schema":1,"id":"capture-001","run":"t13-ios-01","build":"20","expectedBoot":"CURRENT_BOOT_FROM_STATUS","action":"capture","screenshot":true}
```

Status includes live IPv4 addresses (captured at diagnostic boot), build/build GUID/config/hash, local ID/nonce, session/room/round, connection and pending states, Lobby and game snapshots, screen text/visibility, orb view IDs/count, projectiles, last/active command ID and hash, game/lobby error, LastInterruptionReason, response-hold flag and rejected-message counts. Lifecycle callbacks are observational; there is no synthetic pause/focus command. Forced process termination can omit lifecycle callbacks and must not be called an actual background/resume test.
