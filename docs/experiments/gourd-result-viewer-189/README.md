> 역사 기록: #189 실험 당시의 사용법·구조입니다. #191 통합 후 현재 사용법은 저장소 README의 완성된 박 관람 항목을 따릅니다. 아래 과거 기록은 새 구현의 검증 결과가 아닙니다.

# Experiment #189 — 완성된 도색 박 관람

정식 기능 반영 전의 **로컬 결과 관람 실험**입니다. 브랜치는 `experiment/#189-gourd-result-viewer`이며 main 병합은 별도 판단합니다. 기본 Lab Scene·전투용 박·111개 공유 설정·프로토콜 5628은 유지합니다.

## 켜고 확인하기

### Unity Editor

1. 기존 `Assets/Lab/Scenes/Lab.unity`를 엽니다.
2. `C6 Lab → Experiments → Gourd Result Viewer → Enable in Play Mode`를 선택합니다.
3. Play에서 DEV 솔로 또는 기존 방 생성·입장으로 Paint 전투를 시작합니다.
4. 실제 도색이 100%가 되어 Victory가 되면 `EXPERIMENT · VIEW GOURD`를 누릅니다.
5. `C6 Lab → Experiments → Gourd Result Viewer → Disable`로 끕니다.

관람 조작만 빠르게 확인하려면 DEV `Gourd → Paint Initial Coverage (%)`를 100으로 적용한 새 솔로 판에서 실제 Combined를 한 번 명중시킬 수 있습니다. 이는 관람용 튜닝 fixture이며 기본 성장 난이도 검사가 아닙니다.

메뉴 설정은 이 Editor 세션의 opt-in이며 Scene·Config에 저장하지 않습니다. 기본값은 OFF입니다. UI 프리팹이 이미 포함되어 있으므로 평소에는 `Build UI Prefab`을 누를 필요가 없습니다. 해당 메뉴는 프리팹이 없을 때 최초 생성하며 기존 프리팹은 덮어쓰지 않습니다.

### Mac Development 앱

기존 `C6 Lab → Build Mac App`으로 빌드하고 앱 실행 파일에 **`--lab-gourd-result-viewer`**를 전달합니다. README의 기존 Host/Client 실행 인자와 함께 사용할 수 있습니다. 앱을 일반 실행하면 관람 실험을 켜지 않으며, Release 앱은 이 실험 인자를 활성화하지 않습니다.

```sh
"Builds/Lab/<출력 폴더>/macOS/C6_Physics_Lab.app/Contents/MacOS/C6_Physics_Lab" --lab-gourd-result-viewer
```

## 조작

- 한 손가락 또는 마우스 드래그: 좌우·상하 회전. 손을 놓으면 정지합니다.
- 관람 이미지에서 두 손가락 벌리기/모으기: 확대·축소.
- 마우스 휠: 확대·축소.
- `RESET VIEW`: 최초 회전·확대값으로 복귀.
- `BACK TO RESULTS`: 기존 결과 화면으로 복귀.

관람 화면은 결과 버튼 위에 불투명 모달로 표시되어 뒤쪽 버튼을 누르지 않게 합니다. 각자 다른 방향으로 볼 수 있고, 자신의 관람 회전은 다른 참가자에게 전송하지 않습니다. Host가 다음 판을 시작하거나 전원 로비로 돌아가면 참가자의 관람도 자동 종료됩니다.

## 구조와 보존 범위

- `Runtime/GourdResultViewerExperiment.cs`: 명시적 opt-in, Paint Victory·100%·Snapshot/맵 일치, 진입·이탈 수명 관리.
- `Runtime/LabGourdResultPreview.cs`: 최종 RGBA를 복사한 소유 텍스처·재질, MeshFilter/MeshRenderer만의 모델, 독립 카메라와 RenderTexture, 회전/확대/정리.
- `Runtime/GourdResultViewerUI.cs`: 저장된 UI 참조와 버튼 연결.
- `Runtime/GourdViewerGestureSurface.cs`: 관람 이미지에서만 드래그·핀치·휠 입력 처리.
- `Editor/GourdResultViewerExperimentEditor.cs`: 로컬 Editor opt-in과 프리팹 최초 생성.
- `Resources/GourdResultViewer/ViewerUI.prefab`: Canvas, 결과 진입 버튼, 모달, 이미지, 상태·조작 안내, 초기화·닫기 버튼. Prefab Mode/Inspector에서 직접 편집합니다.
- `Tests/EditMode`, `Tests/PlayMode`: 실험 전용 검사. 기본 게임 테스트와 별도 어셈블리입니다.

원본 `LabGourdPaint`를 비활성화하거나 네트워크 박·카메라를 회전시키지 않습니다. 관람용 복사본은 게임 스크립트·Collider·Rigidbody를 포함하지 않으며 원본 메시/재질/도색을 변경하지 않습니다. Snapshot의 세션·라운드·Revision/해상도/칠한 셀 수·명중 수가 최종 공개 맵과 같을 때만 복사합니다. 관람 종료, 새 판, 모드 전환, 연결 종료와 Scene 파괴에서 카메라·RenderTexture·도색 복사본·재질을 정리합니다.

프리팹의 `previewTextureSize`는 관람 렌더 해상도(기본 768 정방형)이며 실제 도색 셀 해상도와 별개입니다. 카메라와 모델은 실험 전용 멀리 떨어진 공간·레이어에서 렌더링하며 기존 카메라·레이어 설정 파일을 변경하지 않습니다.

## 검증 범위

실제 실행 결과와 미실행 범위는 이 폴더의 `VALIDATION.md`에 기록합니다. 짧은 100% 초기 면적 DEV fixture와 기본 성장 밸런스, 같은 Mac의 두 앱과 실기기·핫스팟 검증을 구분합니다. 원시 로그·앱·임시 실행 도구는 커밋하지 않습니다.
