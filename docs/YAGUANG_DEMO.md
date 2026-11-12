# 야광귀 3인 시연 빌드 (#706)

일반 Story의 1→2→3 진행과 Lab을 보존하면서 별도 앱에서 야광귀 한 판만 진행한다. 시연은 원래 Stage2(`yaguang`, Yaguang) 설정·기믹·박·배경·도색·VFX를 사용한다. 시연의 순서는 `[2]`, 일반 Story는 `[1,2,3]`, Territory는 기존 `[1]`이다. StageCatalog는 원본 세 항목을 유지한다.

## 플레이 순서

타이틀 게임 시작 → 메인 스토리(기존 SKIP 가능) → 방 목록 → 방 생성/참가 → P1~P3 선택·전원 Ready → 방장 시작 → 기존 준비 동기화·3/2/1 → 야광귀 전투 → 야광귀 봉인 카드·후일담 → 전원 마치기/준비 → 방장 총평 진행 → 야광귀 한 판의 봉인 총평 → 엔딩 여섯 컷 → 실제 타이틀.

- 세 기기 모두 같은 시연 빌드를 설치한다. 방장 포함 세 명만 입장하며 두 명으로 시작하거나 네 번째 인원을 받지 않는다. 일반판·Lab과는 방 검색 및 Direct IP 접속 식별자가 다르다.
- 처음 시연 앱을 설치한 iOS 기기는 로컬 네트워크 권한을 허용한다. 기존 LAN/핫스팟 방 생성·검색·입장을 재사용한다.
- 시연에서는 튜토리얼과 솔로/팀전 입장을 제공하지 않는다. 타이틀에서 다시 게임 시작을 누르면 다음 관람객도 메인 스토리부터 본다. 일반판의 오프닝·튜토리얼 정책은 그대로다.
- 정상 패배는 기존 같은 방/같은 자리/승인 설정으로 야광귀를 재도전한다. 이탈·연결 끊김은 기존 게임오버 정책을 따른다. 준비·결과·관람의 방장 권한과 세대 검증을 우회하지 않는다.
- 야광귀의 실제 번호는2로 고정하되 시연 진행 표시는1/1이다. 결과/통계 키도 Stage2를 유지한다. 총평은 실제 성공한 야광귀의 시간과 내 신규 도색 면적 기여도만 표시하며 구미호·지귀의 완료 기록을 만들지 않는다.

## 준비와 빌드

Unity6000.6.5f1에서 프로젝트를 연다.

1. 시연 전용 스토리 자료를 명시적으로 준비할 때만 `Hapio > Demo > Update Yaguang Demo Stories`를 실행한다. `Resources/Hapio/Demo/Yaguang` 아래 사본을 만들며 일반 엔딩/Scene은 수정하지 않는다. 이미 준비된 자료로 빌드할 때는 다시 생성할 필요가 없다.
2. 배포용 Mac 앱은 `Hapio > Demo Build > Release > Build Mac App`을 실행한다.
3. 배포용 iOS는 `Hapio > Demo Build > Release > Export iOS Xcode Project`로 내보낸 뒤 생성된 Xcode 프로젝트에서 기존 개발 Team·연결 기기·서명을 설정해 빌드한다. Xcode 내보내기와 실제 기기 서명/설치 성공은 별도로 확인한다.
4. DEV 조정이 필요한 확인용 빌드는 같은 메뉴의 `Development QA` 하위 항목을 사용한다. QA에서도 시연의3인 규칙을 유지한다. Release에서는 기존 숨김3회 터치로도 DEV를 열 수 없다.
5. `Hapio > Demo Build > Open Last Output`으로 이번 시연의 결과 폴더를 연다. 일반 `Hapio > Development Build` 메뉴는 일반 게임이다.

결과는 `Builds/HapioDemo/<UTC시각-고유번호-release또는qa>/`다. Mac은 `macOS/Hapio-Yaguang-Demo.app`(QA는 별도 접미사), iOS는 `iOS/`에 생성한다. 앱 표시명은 `Hapio Yaguang Demo`, Bundle ID는 원래 해당 플랫폼 ID의 `.yaguangdemo` 접미사를 사용해 일반 앱과 구분한다. Release/QA는 같은 시연 앱을 갱신할 수 있으므로 배포할 산출물을 구분한다.

빌드마다 `build-manifest.json`에 생성 ID, 소스 커밋/미커밋 변경 여부, 대상/QA 여부, 앱 식별자, 통신 식별자, 원래 Stage2 설정 해시와 결과를 기록한다. 서로 다른 생성물의 실행 여부는 앱 이름뿐 아니라 해당 manifest로 확인한다. 빌드 실패/취소도 기록하며 기존 폴더를 덮어쓰지 않는다.

## 구현 연결과 보존 계약

- `LabGameBuildVariant`: `HAPIO_YAGUANG_DEMO`를 전용 빌드에만 전달한다. Development 여부로 콘텐츠를 선택하지 않으며 Editor 테스트 override는 메모리 범위에서만 복원한다.
- `GameRunProfile`: 실제 스테이지 정체성과 진행 위치/완료 여부를 구분한다. `GameRunFlow`, `GameFlowSnapshot`, `GameSession`이 동일한 경로로 시작·수신·재도전·총평을 판단한다.
- `GameLobbyView`/`GamePendingRoomRegistry`/`LabNetwork`: UI뿐 아니라 방 생성·설정 수신·준비에서도3인 Story를 확인한다.
- 일반판 접속 토큰 `C6HapioGame/54`, Lab 토큰/공유 프로토콜5702/설정265개는 유지한다. 시연 토큰은 `C6HapioDemo/1`, Game-family Bonjour TXT는 `C6-HAPIO-DEMO-1-5702`다. 검색과 실제 접속을 모두 분리한다.
- `GameStageStoryPresentation`: 원래 야광귀 후일담을 사용하고 마지막 봉인으로 표시한다. 시연용 `GameEnding` 사본은 단일 야광귀 총평과 기존 엔딩 그림/음향을 사용하며 첫 대사의 세 마리 표현만 야광귀 봉인으로 바꾼다. 제작용 가짜 타이틀 컷 대신 실제 Home 타이틀로 돌아간다.
- `GameDemoBuild`: 명시적인 기존 다섯 Game Scene으로 빌드하고, 임시 이름/ID/버전/빌드번호/플랫폼/Scene 목록·설정 파일을 복원한다. 저장된 전체 스테이지 카탈로그·Scene 배치·일반 빌드 기본값을 자동 변경하지 않는다.

## 검증 범위

구현·진행 조건은 일반/시연 경로, Host/Guest의 단계·설정·세대, 잘못된 참가 수, 일반판 혼합 접속 거절, 실패 재도전, 실제 Stage2 데이터, 총평/엔딩/타이틀을 확인한다. 실제로 수행한 검사·빌드 결과와 미실행 실기기 항목은 `VALIDATION.md`의 #706 기록을 따른다. 로컬 패킷 참가자를 사용한 자동 흐름 검사와 실제 세 기기의 화면·무선 환경 검증은 구분한다.
