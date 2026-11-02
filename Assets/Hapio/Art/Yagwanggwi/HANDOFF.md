# 야광귀 박 — stage2 개발팀 전달

## Unity에서 확인

프로젝트 에셋: `Assets/Hapio/Art/Yagwanggwi/`

- `Yagwanggwi_Gourd_Preview.prefab`: URP Lit 재질과 Animator가 연결된 미리보기용 프리팹.
- `Models/Yagwanggwi_Gourd.fbx`: 박 전체 모델. 곡선 로프는 메시로 변환. 텍스처 내장.
- `Textures/`: 원본 내장 이미지 3장 및 재질별 BaseColor 베이크 3장, 모두 2048×2048.
- `Materials/`: 재질 14개. 베이크 BaseColor/원본 노멀맵 연결. 색동은 원본 색상 사용.
- `Animations/Yagwanggwi_Peek.anim`: 24fps, 원본 1–193프레임, 8초. 여러 부품의 동작을 하나의 클립으로 통합. 원본의 두 번 빼꼼하는 동작을 유지.
- `Animations/Yagwanggwi_Preview.controller`: 클립 1회 재생용. 게임의 이벤트/반복/상태 전환은 개발팀에서 연결.

`Yokai Visual`은 기존 박 안 요괴 장식용 슬롯이므로 이 박 전체 프리팹을 직접 연결하지 않는다. stage2 박 표시 교체와 기존 도색 셰이더/UV/충돌 연결은 개발팀 작업이다. 다른 스테이지의 WhiteGourd, Scene, StageDefinition은 수정하지 않았다.

## 원본과 재질 표현

전달 묶음의 `Source/Yagwanggwi_Gourd.blend`는 제공받은 원본 그대로다. 추출한 Image_0/1/2도 함께 보존했다. 원본에 있는 10개 오브젝트 Action은 동시에 재생하는 한 동작으로 내보냈으며, 현재 Scene 오브젝트에 연결되지 않은 예전 색동 shape-key Action 2개는 FBX 클립으로 내보내지 않았다.

노란 천의 Blender 노드 기반 색상은 BaseColor로 베이크했다. 색동의 Noise/Bump 및 Blender 전용 미세 표면 표현은 URP에서 동일하게 재현하지 않았고, 거칠기/금속성은 기본 수치로 연결했다. Image_2는 원본 보존용이며 URP의 Metallic/Smoothness로 변환 연결하지 않았다. 조명·톤매핑에 따라 원본 Blender와 차이가 날 수 있다.

## 수행한 검증

- Unity 6000.6.5f1에서 FBX 임포트 및 URP 프리팹 생성.
- 애니메이션 클립 1개, 8초, 커브 250개; 계층 경로 누락 0개.
- Renderer 20개, 누락/오류 재질 0개, BaseColor 텍스처 연결 슬롯 7개.
- 별도 미리보기 Scene에서 이전 4초 버전의 0/1/3초를 샘플링해 렌더링. 뚜껑 개폐 및 요괴 등장과 재질 색상 확인.
- 기존 게임 Scene 저장/스테이지 교체는 수행하지 않음. 실제 게임 도색·물리·네트워크 및 실기기 동작은 미검증.

이전 4초 버전의 미리보기 PNG는 전달 묶음의 `front_0.png`, `front_1.png`, `front_3.png` 참고.

## 8초 수정본 교체

전체 키프레임 시간을 1프레임 기준 2배로 늘려 24fps / 1–193프레임 / 8초로 교체했다. FBX와 추출 클립 모두 8초, 클립 1개, 계층 경로 누락 0개를 확인했다. 미리보기 Controller의 상태 Speed는 1이다. 기존 에셋 경로와 meta GUID는 유지하여 이전 4초 파일 내용을 교체했다. Blender의 대응 5개 시점에서 원본/수정본 포즈 행렬 차이는 0이었다. 기존 ZIP은 이전 4초 전달본이므로 현재 프로젝트 에셋을 사용한다.
