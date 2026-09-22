# 정면 전투 화면 그래픽 적용

대상 브랜치: `feat/#37-ui-asset-update` · 대상 씬: `ContinuousTransferBattle.unity`

[Figma 원본](https://www.figma.com/design/wW7ny7lNQ4DXNJ4kD46AuR/-C6--%EC%9D%BC%EB%8B%A8%EB%AC%B4%EC%82%AC?node-id=1426-657)의 정면 화면(`1426:730`)에서 배경 원본과 에셋 꾸러미의 HUD 원본을 가져왔다. Figma의 샘플 요괴(`1426:733`)는 사용하지 않는다. 전투 대상은 기존 `Jangsanbeom.prefab`과 그 Rigidbody/Collider 및 피격 판정 그대로다. 좌·우 화면 샘플과 방어 영역 표시는 현재 씬에 추가하지 않았다.

## 에셋과 투명도

- `Assets/_Project/HapioMVP/Art/FigmaFront/FrontBackdrop.png`: 정면 배경 원본. 전체 화면을 채우는 불투명 이미지다.
- `Assets/_Project/HapioMVP/Art/FigmaFront/HudAtlasTransparent.png`: Figma의 원본 HUD 아틀라스. 가장자리 알파가 0인 투명 PNG다. 요괴 HP 장식, 시간 장식, 생성 버튼, 스태미나 장식·보석은 `RawImage.uvRect`로 원본의 각 구역을 표시한다. 개별 PNG 내보내기에 있던 흰 배경은 사용하지 않는다.
- HP·시간은 아틀라스의 장식 테두리 위에 어두운 트랙과 동적 색 채움을 겹친다. 아틀라스에 고정으로 그려진 예시 채움이 실제 수치로 오인되지 않게 가린다.
- 스태미나의 다섯 보석은 각각 20 자원 단위로 채워진다. `T09Hud.SetResources`가 각 칸의 클립 폭을 연속적으로 갱신하므로 45.5/100이면 2칸 완전 채움 + 3번째 칸 27.5%다.

## 씬에서 고치는 곳

Unity에서 `P4ContinuousTransferBattle/T09Overlay`를 열면 다음 항목을 직접 옮기고 크기를 조절할 수 있다.

| 항목 | 씬 오브젝트 | 실행 중 바뀌는 값 |
|---|---|---|
| 요괴 HP | `BattleStats/MonsterHpPanel`과 `FigmaMonsterHpTrack/MonsterHpFill` | HP 비율 |
| 생성 버튼 | `ResourceControls/ResourceButtons/GenerateButton` | 비용·대기 문구, 클릭 기능 |
| 스태미나 | `ResourceControls/PersonalStaminaPanel/ContinuousStaminaTrack/FigmaStaminaGem1~5` | 0~100의 연속 자원량 |
| 팀 시간 | `ResourceControls/TeamTimeBar/FigmaTeamTimeTrack/TeamTimeFill` | 남은 시간 비율 |

`T09Hud`의 기존 참조와 `T09BattleController`의 클릭 연결을 유지한다. `GenerateButton`의 `Button.targetGraphic`은 투명 아틀라스의 자식 `RawImage`를 가리킨다. 버튼의 Inspector On Click에 같은 동작을 추가하지 않는다. 투명 배경은 `FigmaAtlasArt` 자식의 `RawImage`가 표시한다.

배경은 두 카메라 자식 `Figma Front Upper Backdrop`·`Figma Front Lower Backdrop`에 있다. `FigmaViewportBackdrop`이 전체 화면 이미지의 각 카메라 영역을 UV로 나눠 보여 주며, 기기 화면 비율과 상하 분할이 바뀌어도 두 구역이 이어지게 조절한다. 카메라 뒤쪽에 배치되어 3D 요괴와 구슬을 가리지 않는다. 예전 무대 장식과 구슬판 격자의 Renderer만 꺼 두었고 콜라이더와 게임 오브젝트는 보존했다.

하단 컨트롤 구역은 Figma의 버튼·스태미나·시간 바가 겹치지 않도록 188 단위 높이로 잡았다. `ScreenLayoutConfig.MinimalBattleFooterHeight`와 같아서 구슬 물리 작업 영역이 버튼 밑으로 들어가지 않는다. 위치를 크게 바꾸면 이 작업 영역 경계와 함께 검토해야 한다.

## 다시 적용하거나 확인하기

1. Play를 멈추고 저장된 `ContinuousTransferBattle` 씬을 연다. 미저장 변경이 있으면 먼저 자신의 작업을 저장하거나 확인한다.
2. 메뉴 **C6 → UI → Apply Figma Front Artwork**는 원본 기본 배치를 다시 적용한다. 위치를 직접 조정한 후에는 이 메뉴를 다시 누르지 않는다.
3. 메뉴 **C6 → UI → Validate Battle UI Connections**로 저장 참조를 검사한다.
4. EditMode `EditableBattleUiSceneTests`는 참조·투명 아틀라스 연결·방어 표시 없음 등을, PlayMode `EditableBattleUiPlayTests`는 수치 갱신과 버튼 한 번당 구슬 한 개를 확인한다.
5. 최종 세로 화면 배치와 터치는 실제 iPhone/iPad에서 확인해야 한다. Editor 검사만으로 실기기 결과를 PASS로 기록하지 않는다.

## 이번 실행 기록

2026-09-22 · Unity 6000.5.7f1, 연결된 Editor에서 적용·저장했다. HUD 원본 아틀라스는 1205×1306 크기를 유지하도록 NPOT 자동 크기 변경을 껐다. 원본 PNG 모서리의 알파는 0이며, 흰 배경이 포함된 개별 내보내기 파일은 프로젝트에서 제거했다.

| 항목 | 상태 | 실행 결과 |
|---|---|---|
| 스크립트 컴파일·씬 연결 메뉴 | PASS | Unity Editor에서 적용 메뉴와 연결 검사 메뉴 실행 성공 |
| 저장 UI·카메라 EditMode | PASS | `EditableBattleUiSceneTests` 5/5, `BattleLayoutSceneTests` 2/2 |
| UI·카메라·구슬 물리 PlayMode | PASS | `EditableBattleUiPlayTests` 3/3, `BattleLayoutPlayTests` 4/4, `OrbPhysicsSandboxPlayTests` 8/8 |
| 세로 화면 시각 점검 | PASS · Editor 한정 | 402×874 Play-mode 합성 화면에서 투명 HUD, 기존 3D 장산범, 연속 배경, 다섯 스태미나 보석과 글자·격자 정리 확인 |
| iOS 빌드·설치·실기기 터치·다인 실행 | NOT_RUN | 이번 그래픽 작업에서는 실행하지 않음 |

시각 점검은 로비 표시만 잠시 멈춘 Play-mode 미리보기이며 게임 한 판이나 실기기 플레이 성공을 뜻하지 않는다. 미리보기 상태는 Play 종료 때 폐기했다.

## 후속 iPhone + Mac Unity 검증 · 2026-09-22

위 표의 `NOT_RUN`은 그래픽 적용 당시의 상태다. 후속 요청에서 같은 저장 씬의 미커밋 UI 변경을 포함해 새 iOS 앱을 출력했고, iPhone 17과 Mac Unity Editor Play를 한 방에 연결했다. iPad 빌드와 다인 기기 시험은 수행하지 않았다. Host는 Mac, Client는 iPhone이며 방 한정 개발자 설정으로 **2인 요괴 HP만 800→100**으로 낮춰 바 변화가 잘 보이게 했다. 저장 Config는 바꾸지 않았다.

| 확인 항목 | 상태 | 이번 실행 근거와 범위 |
|---|---|---|
| Unity iOS 출력 | PASS | Unity 6000.5.7f1, `ContinuousTransferBattle`·L2 Development 앱26 Xcode 프로젝트 생성. 빌드 영수증 `Succeeded`, 경고18·오류 집계1을 그대로 보존. 출력 검증 `PASS`이며 오류 집계가 0이라는 뜻은 아님 |
| Xcode 앱 빌드·서명 | PASS | Xcode 26.6 빌드 exit0, `codesign --verify --deep --strict` 성공. 앱 Bundle ID·빌드 번호26·기존 개인 개발 Team의 프로필 확인 |
| iPhone 설치·실행 | PASS | iPhone 17 설치 exit0, 새 앱의 로비 초기화 및 게임 준비 로그 확인. 설치와 실행을 구분해 확인 |
| iPhone ↔ Mac 연결·시작 | PASS | Mac Editor Play Host와 iPhone Client가 같은 방의 2/5 참가자로 입장·Ready, Host Start 후 양쪽 Playing. 앱 패키지 빌드26과 저장 씬의 게임 식별자25를 구분 |
| Mac 전투 화면 | PASS | Figma 배경과 투명 HUD, 기존 3D 요괴, 생성 버튼·5칸 보석·요괴 HP와 시간 바 표시. 시간 및 요괴 HP 변화가 화면에 반영되는 것을 관찰 |
| iPhone 실제 터치·Host 확정 | PASS | iPhone 구슬 생성·Yin/Yang 직접 조합·Combined 손떼기 투척 로그. Mac Host 실제 유효 피격 4회로 HP 100→80→60→40→20, Mac 화면 바도 감소. 180초 종료 시 HP20의 Defeat 확인 |
| iPhone 각 그래픽 요소의 개별 육안·스크린샷 | NOT_RUN | 사용자는 시험 전반에 대해 “이정도면 된듯”이라고 답하고 추가 직접 수정을 선택했다. iPhone의 에셋별 표시·스태미나 보석 애니메이션은 개별 회신/기기 화면 증거로 남기지 않았으므로 Mac 관찰을 기기 시각 PASS로 승계하지 않음 |

원시 Unity/Xcode/기기 로그와 서명·기기 식별 정보는 로컬 출력에만 두고 저장소에는 포함하지 않는다. Mac Play를 종료했으며 검증용 앱 빌드 번호와 자동 직렬화된 URP 자산은 작업 전 상태로 되돌렸다. 새 그래픽·씬·코드의 의도된 변경은 유지해 후속 배치 편집에 사용할 수 있다.

## 직접 편집한 배치 반영

사용자가 Unity 씬에서 HUD의 위치·크기를 다시 조절하고 `MonsterHpValue`, `MonsterHpCaption`, `TeamTimeValue`를 비활성화한 저장 상태를 이번 커밋에 포함한다. 요괴 HP·팀 시간의 채움 이미지, 스태미나 보석 5개, 생성 버튼과 컨트롤러 연결은 활성 상태로 유지된다. 위의 실기기 실행은 이 수동 배치 **이전**의 결과이므로 새 배치의 iPhone 화면 확인으로 승계하지 않는다.

수동 배치 저장 후 `Validate Battle UI Connections`가 통과했다. 관련 Unity 검사는 EditMode 7/7(`EditableBattleUiSceneTests` 5, `BattleLayoutSceneTests` 2), PlayMode 7/7(`EditableBattleUiPlayTests` 3, `BattleLayoutPlayTests` 4)이며 실패·건너뜀·미확정은 0이다. 새 배치의 Xcode 재빌드·iPhone 설치·터치 시험은 **NOT_RUN**이다.
