> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T04 · 상단 3D·하단 2D·HUD 구성 결정

2026-09-13. 이 문서는 T04 구현 계약과 확인 방법을 설명한다. 파일·씬·빌드 도구가 있다는 사실을 실행 완료로 해석하지 않는다. 실제 컴파일, 자동 시험, 렌더, iOS 프로젝트 생성 결과는 [VALIDATION.md](VALIDATION.md)의 실행별 증거를 따른다.

## 범위와 선행 조건

사용자가 승인한 iPhone17 Host+iPad Client 기준의 T03/G2 결과를 선행 근거로 삼는다. [기기 조합 변경](G2_DEVICE_CHANGE.md)과 당시 증거는 그대로 보존한다. T04는 상단 전장, 하단 구슬 조작 예정 영역, 고정 HUD의 화면 골격이다. 기존 연결·공유 숫자 코드와 시험 씬을 보존하되 T04 화면이 자동으로 네트워크 세션을 시작하지 않는다.

구슬 모델·실제 구슬·Gesture·전달·조합·발사·공격·게임 자원·타이머·물리 피격은 이번 구현에 포함하지 않는다. T04 기본 도형의 Collider는 제거한다. 눈에 보이는 허수아비가 공격이나 유효 피격 판정을 제공한다는 뜻은 아니다.

## 단일 화면 비율과 실제 입력 경계

설정 원본은 `Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset` 하나다. `ScreenLayoutConfig`가 `upperFraction`만 직렬화하며 첫 값은 `0.55`다. `UpperFraction`은 상단, `LowerFraction = 1 - UpperFraction`은 하단 비율이다. 상단55%·하단45%는 통합본의 `DEMO_TUNING_VALUE`다. 하단 비율이나 별도 입력 픽셀 경계를 중복 저장하지 않는다.

`SplitScreenLayout.Configure(config, battleCamera, orbCamera)`로 저장된 참조를 연결한다. 서로 다른 카메라 두 개가 필요하며 이 컴포넌트는 카메라를 생성하지 않는다. `ApplyLayout()`은 다음 전체 렌더 표면의 정규화 영역을 두 카메라의 실제 `rect`에 적용한다.

| 영역 | Viewport Rect `(x, y, width, height)` | 기본값 |
|---|---|---|
| 상단 | `(0, LowerFraction, 1, UpperFraction)` | `(0, 0.45, 1, 0.55)` |
| 하단 | `(0, 0, 1, LowerFraction)` | `(0, 0, 1, 0.45)` |

`LateUpdate`에서 화면 크기, Config 참조·비율, 카메라 참조·영역 변경을 감지해 다시 적용하고 `Changed` 이벤트를 발행한다. `TopViewport`·`BottomViewport`·`TopPixelRect`·`BottomPixelRect`는 할당된 카메라의 실제 값을 노출한다. 향후 입력은 이 경계를 사용해야 한다.

`ContainsBottomScreenPoint(Vector2)`는 하단 카메라의 실제 `pixelRect.Contains`를 사용한다. 하단의 왼쪽·아래쪽 경계는 포함하고 오른쪽·위쪽 경계는 제외한다. `TryScreenToOrbPlane(Vector2, out Vector3)`은 하단 안의 점만 하단 카메라로 투영해 월드 XY 평면 `z = 0`에 대응시킨다. 이 함수는 좌표 변환이며 선택·드래그·공격 입력 처리가 아니다.

비율 편집값을 `0.01`~`0.99`로 제한하는 것은 두 카메라에 비어 있지 않은 영역을 남기는 `DEMO_ASSUMPTION`이다. NaN·Infinity는 기본값0.55로 복구한다. 이 한계값이 게임 규칙이나 극단적인 비율에서 HUD의 실사용성을 보장하지는 않는다. 이번 기본 비율을 바꾸면 실제 카메라·입력 경계·HUD 배치와 렌더를 함께 다시 확인한다.

## 카메라와 시각 도형

Unity6000.5.7f1 / 설치된 URP17.5.0의 기존 Universal Renderer를 사용한다. 렌더 파이프라인이나 Renderer Asset을 바꾸지 않고 `UniversalAdditionalCameraData.renderType = Base`인 카메라 두 개를 둔다. 둘 다 RenderTexture 없이 화면에 직접 렌더하며 Camera Stack은 비어 있다.

| 카메라 | 구성 | Culling Mask |
|---|---|---|
| Battle Camera | Perspective, FOV42, 위치 `(0, 3.1, -9)`, `(0, 2.2, 0)` 바라봄, depth0 | C6Battle + C6Background |
| Orb Camera | Orthographic, size3.2, 위치 `(0, 0, -10)`, 기본 회전, depth1 | C6Orbs |

카메라 구도·재질색·도형 크기·하단 그리드 간격은 T04의 시각 배치이며 게임 판정값이 아니다. 좌석별120° 시점은 추가하지 않는다. 같은 씬을 사용하는 두 기기는 같은 기본 허수아비 구도를 공유한다. 실제 기기별 종횡비의 최종 화면·Touch 확인은 T06에서 수행한다.

`C6Battle`, `C6Orbs`, `C6Background`는 기존 이름을 재사용하거나 TagManager의8~31번 빈 사용자 레이어에 추가한다. 기존 레이어를 교체하지 않으며 빈 칸이 없으면 준비를 중단한다. 배경과 허수아비는 상단에만, 빈 구슬 예정 영역의 흐린 그리드는 하단에만 그린다. 기본 도형·간단한 URP Lit/Unlit 재질은 `Presentation/Materials/`에서 생성·재사용한다.

AudioListener는 Battle Camera에 하나만 둔다. 씬은 입력이나 물리 게임 루프를 시작하지 않는다.

## HUD와 Safe Area

`T04Hud.Configure(SplitScreenLayout)`가 해당 화면 분할을 참조한다. HUD는 Screen Space - Overlay Canvas 하나를 사용한다. Canvas Scaler의 기준 해상도는390×844, Scale With Screen Size, Match Width Or Height0.5다. 이 값은 UI 표시 기준이며 입력 영역의 별도 픽셀 상수가 아니다.

`UISafeArea`를 사용하는 안전 영역 안에서 상단/하단 실제 카메라 영역과 겹치는 부분에 각각 HUD를 배치한다. Safe Area는 노치·홈 표시 주변의 UI를 보호하며 카메라 Viewport를 축소하거나 이동시키지 않는다. 화면 분할선도 실제 하단 영역의 위쪽 경계를 따른다.

| 항목 | T04 표시와 동작 |
|---|---|
| Monster HP | `-- / --`, 값 미연결 |
| Team Time | `--:--`, 카운트다운 없음 |
| Local Stamina | `-- / --`, 회복·소모 없음 |
| Connection | `OFFLINE`, 실제 세션 시작 없음 |
| Generate | `NOT WIRED`, 비활성 버튼, 생성 함수 연결 없음 |
| 안내 | `T04 / LAYOUT PREVIEW`, `HUD NOT CONNECTED` |

장식 Text/Image는 `raycastTarget = false`이며 생성 버튼도 이번에는 비활성이다. 기존 EventSystem이 없다면 HUD가 InputSystemUIInputModule을 가진 EventSystem 하나를 만든다. 씬을 다시 불러온 뒤 Canvas·EventSystem·AudioListener가 중복되지 않는지 실행 시험으로 확인한다.

## 저장 씬과 반복 생성

`C6.Editor.BattleLayoutBuild.Prepare()`가 `Assets/_Project/HapioMVP/Scenes/BattleLayout.unity`를 준비하고 Config·카메라·HUD·개발 캡처 참조를 연결한다. 이미 저장된 해당 씬이 있으면 재작성하지 않는다. Config와 재질도 이미 있으면 보존한다. 파일 경로가 다른 종류의 에셋으로 점유되어 있으면 덮어쓰지 않는다.

T04만 활성 Build Scene으로 설정하며 T01/T02/T03과 기존 SampleScene은 보존한다. 버전0.1.0/빌드6, 기존 Bundle ID, Portrait·iPhoneAndiPad·전체 화면·IL2CPP 대상이다. 이 설정을 적용한 사실과 iOS 프로젝트 생성·앱 빌드·서명·실기기 실행은 별도 항목이다.

`ExportIOS()`와 `BuildMac()`은 `C6_T04_OUTPUT_ROOT` 또는 `Builds/T04/` 아래에 각각 `iOS/`, `macOS/C6Layout.app`을 만든다. 기존 출력이 있으면 새 출력 루트를 요구한다. 기존 로컬 네트워크 목적 문구 후처리는 `DirectConnectionBuild.LocalNetworkPurpose`를 그대로 사용한다.

## 증거의 범위

`T04Capture.Configure(SplitScreenLayout)`는 Development Standalone 앱에서 명시적 `-c6CaptureDirectory <절대 경로>`가 있을 때만 실제 렌더를 캡처한다. 결과는 `render.png`와 `render.json`이며 둘 중 하나라도 이미 있으면 덮어쓰지 않고 실패한다. `-c6CaptureQuit`가 있을 때만 결과 기록 뒤 성공0/실패1로 앱을 종료한다. 보통 앱 실행에서 캡처나 자동 종료를 시작하지 않는다.

캡처 도우미는 해상도를 변경하지 않는다. 그래픽을 켠 플레이어에서8프레임과 EndOfFrame을 기다린 뒤 캡처하며 PNG 완료 여부를 최대10초 확인한다. 실제 렌더 이미지에는 관측된 해상도·앱 빌드·설정·소스 해시를 연결하며 설명용 화면 예시와 구분한다. Mac 화면은 iPhone/iPad의 실제 Touch·Safe Area·프레임 성능 증거가 아니다.

캡처 JSON의 결과는 PNG 저장 완료 여부다. 그 값만으로 허수아비·HUD의 시각 품질, 실제 기기 표시 또는 Task 전체를 판정하지 않는다. 생성된 이미지를 직접 확인한 결과를 별도로 기록한다.

자동 시험은 저장 씬 참조, 분할 비율 변경과 입력 경계, 하단 좌표 왕복, Safe Area와 카메라 영역 분리, HUD 미연결 표시, 장식 입력 통과, 씬 재진입의 중복 객체 여부를 다룬다. 테스트 개수·실패·누락은 실제 XML로 확인한다. iOS export는 실제 Xcode 프로젝트 생성과 plist를 확인해야 하며 소스의 Build 함수 존재만으로 인정하지 않는다.

T04에는 독립 실기기 Gate가 없고 화면·입력의 실기기 확인은 T06에 지정되어 있다. T04 완료로 G3 전체 또는 T06을 완료 처리하지 않는다. 다음 Task 후보 T05는 별도 사용자 요청 후 시작한다.
