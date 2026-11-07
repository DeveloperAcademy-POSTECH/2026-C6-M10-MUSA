# Mac 클레이 비교 앱 — 질감·배경 파일 사용 설명서

이 문서는 **#543 이미지 파일 비교 앱**의 사용법이다. 완성된 같은 5색 박을 보면서 스티커의 질감·두께·광택·색·빛·배경을 비교한다. 방에 입장하거나 직접 박을 칠할 필요가 없다. 일반 게임의 설정을 바꾸는 DEV 화면과는 별개이며, 여기서 바꾼 값은 게임이나 원본 파일에 자동 저장되지 않는다.

대상 Scene은 `Assets/Experiments/ClayPreview/ImageSamples/Scenes/ImageClayPreview.unity`다. 기존 세 프리셋 앱이나 파일 기능 없는 코팅 앱을 열면 이 문서의 `FILES / IMAGES`가 보이지 않을 수 있다. 구현 범위는 [둥근 클레이 실험 문서의 #543 절](ROUNDED_CLAY_STICKER_EXPERIMENT.md#543-스티커-질감배경-파일을-직접-불러오는-mac-실험), 실제 수행한 검사와 미실행 항목은 [검증 기록](VALIDATION.md)의 2026-11-07 · #543 절을 참고한다.

## 1. 이미 빌드된 앱으로 바로 보기

1. Finder에서 프로젝트 폴더를 연다.
2. `Builds` → `Experiments` → `ClayPreview`로 들어간다.
3. 원하는 빌드의 `생성시각-고유값` 폴더를 연다. 새 앱을 찾는다면 폴더 이름의 시각을 확인한다. 예전 앱도 같은 파일명을 사용한다.
4. `Hapio-Clay-Experiment.app`을 더블클릭한다.
5. 왼쪽에 완성 박, 오른쪽에 `CLAY STUDY` 조절 패널이 보이는지 확인한다.
6. 네 번째 `Sticker-local coating`이 선택돼 있고 그 아래에 `FILES / IMAGES`가 있으면 이미지 파일 비교 버전이다.

처음 실행할 때는 **외부 질감과 외부 배경이 없는 상태**다. 실제 도색 기록으로 만든 고정 도안을 사용하므로 다시 실행해도 얼룩 배치를 무작위로 바꾸지 않는다. 앱 파일은 빌드 산출물이어서 저장소에 포함하지 않는다. 다른 컴퓨터에 소스만 내려받았다면 [직접 빌드하기](#12-unity에서-직접-준비하고-mac-앱-빌드하기)를 따른다.

오른쪽 패널은 길다. **포인터를 오른쪽 패널 위에 둔 채 스크롤**하면 아래의 팔레트·재질·빛·시점·종료 버튼까지 볼 수 있다. 왼쪽 박 영역에서 스크롤하면 패널 이동 대신 박 확대·축소가 된다.

## 2. 3분 안에 샘플 비교하기

1. 시작 프리셋 `Sticker-local coating`을 유지한다.
2. `FILES / IMAGES`에서 `Fine clay`를 누른다.
3. 파일명과 작은 질감 미리보기가 생기고 `Apply surface texture`가 켜지는지 확인한다.
4. 같은 자리의 박을 보면서 `Hand-made`, `Fine fiber`를 차례로 누른다.
5. 차이가 약하면 박 위에서 휠로 확대하고 `GRAIN CONTRAST`를 올린다. 무늬 크기는 `PATTERN SCALE`로 조절한다.
6. `Apply surface texture`를 껐다 켜서 **같은 재질·색·도안에서 추가 질감만** 비교한다.
7. 처음 상태로 돌아가려면 `Clear texture`를 누른다.

켜진 체크 칸은 금색으로 채워지고 꺼진 칸은 비어 보인다. 질감 파일을 고르기 전에는 관련 조절이 비활성화된다.

| 앱 버튼 | 어떤 질감인가 | 원본 파일 |
| --- | --- | --- |
| `Fine clay` | 고운 무광 클레이의 미세입자 | [FineMatteClay.png](../Assets/Experiments/ClayPreview/ImageSamples/Sources/FineMatteClay.png) |
| `Hand-made` | 손으로 빚은 듯한 얕고 넓은 주름 | [HandKneadedClay.png](../Assets/Experiments/ClayPreview/ImageSamples/Sources/HandKneadedClay.png) |
| `Fine fiber` | 고운 한지 섬유결 | [FineHanjiFiber.png](../Assets/Experiments/ClayPreview/ImageSamples/Sources/FineHanjiFiber.png) |

세 원본은 각각 1254×1254 RGB 이미지다. 앱의 내장 샘플도 외부 파일과 같은 이미지 처리 경로를 사용한다. 제작 프롬프트는 [ImageSamplePrompts.json](../Assets/Experiments/ClayPreview/ImageSamples/Sources/ImageSamplePrompts.json)에 보관한다.

아래는 왼쪽부터 **추가 질감 없음 / Fine clay / Hand-made / Fine fiber**다. 네 칸의 도안·카메라·조명은 같다.

![앱 기본 질감 설정의 전체 비교](images/clay-image-samples-full.png)

아래 확대 비교만 질감 차이를 드러내기 위해 `PATTERN SCALE 0.25 / GRAIN CONTRAST 1 / SHALLOW RELIEF 1`로 강조했다. 앱 시작값이나 모든 사용자에게 권장하는 확정값은 아니다.

![강조 설정을 사용한 실제 박 표면 확대 비교](images/clay-image-samples-detail.png)

## 3. 내 이미지로 스티커 표면 질감 바꾸기

1. 사용할 이미지를 PNG 또는 JPG/JPEG로 준비한다. 조건은 [파일 조건](#5-파일-조건과-취소실패-처리)을 따른다.
2. `STICKER SURFACE` 아래의 `Choose texture file…`을 누른다.
3. Mac 파일 선택창에서 이미지를 선택하고 `Choose`로 확정한다. Editor에서 시험할 때는 Unity의 파일 선택창을 사용한다.
4. 선택된 파일명과 작은 미리보기를 확인한다.
5. 박을 확대하고 질감 조절 3개를 하나씩 바꾼다.
6. 효과가 마음에 들지 않으면 `Apply surface texture`를 끄거나 다른 파일·샘플을 고른다.

질감 입력은 **색 그림을 박 위에 그대로 붙이는 기능이 아니다.** 이미지의 밝고 어두운 결을 추출해 기존 스티커 색의 명암과 얕은 표면 방향에 사용한다. 빨간 이미지를 넣었다고 스티커가 빨간색으로 바뀌지 않는다. 색을 바꾸려면 `PALETTE · RGB / HEX`를 사용한다.

가져온 이미지는 처리 후 중립 회색에 가까운 미리보기로 보일 수 있다. 전체 평균 밝기를 빼고 밝기 차이를 사용하는 구조라서, **완전히 단색인 흰색·검정·회색 이미지나 완전히 투명한 이미지는 질감 변화가 없다.** 입자·섬유·주름처럼 실제 밝기 변화가 있는 이미지를 고른다. 일반 사진을 쓸 수도 있지만 피사체의 명암까지 반복 무늬로 보일 수 있다.

효과는 실제 스티커가 있는 부분에 적용한다. 완료 시 채워진 얇은 바탕·끈·뚜껑에 같은 질감을 덮지 않는다. 파일로 얼룩의 윤곽이나 도장 위치를 교체하지 않으며, 높이·면적·게임 판정을 다시 계산하지 않는다.

### 표면 질감 조절값

| 화면 항목 | 범위 | 시작값 | 조작 결과 |
| --- | --- | --- | --- |
| `Apply surface texture` | ON/OFF | 파일을 불러오면 ON | 가져온 파일을 유지한 채 추가 질감만 켜고 끈다. |
| `PATTERN SCALE` | 0.25~12 | 1 | **작게** 하면 무늬가 크게 보이고, **크게** 하면 더 촘촘하게 반복된다. |
| `GRAIN CONTRAST` | 0~1 | 0.5 | 기존 색 위에 보이는 이미지 결의 밝고 어두운 차이를 조절한다. 0이면 이 명암 변화가 없다. |
| `SHALLOW RELIEF` | 0~1 | 0.2 | 이미지 결이 조명에 반응하는 얕은 요철을 조절한다. 최대 약3도의 표면 방향 변화이며 실제 외곽 두께를 만드는 설정이 아니다. |

`GRAIN CONTRAST`와 `SHALLOW RELIEF`는 별도다. 명암만 비교하려면 요철을0으로, 조명에 의한 굴곡만 비교하려면 명암을0으로 둔다. 질감 OFF는 둘을 한꺼번에 끄지만 두 슬라이더의 숫자는 유지한다.

다른 질감 파일이나 샘플을 고르면 새 이미지가 적용되고 추가 질감은 ON으로 바뀐다. **크기·명암·요철 값은 유지**되므로 같은 조건의 파일 비교가 가능하다. 세 값까지 초기화하려면 `Clear texture`를 누른 뒤 다시 파일을 고른다.

## 4. 배경 이미지 넣기

1. 오른쪽 패널을 내려 `BACKGROUND`를 찾는다.
2. `Choose background file…`을 누른다.
3. PNG 또는 JPG/JPEG를 선택한다.
4. 선택한 파일명과 작은 미리보기, 박 뒤의 배경을 확인한다.
5. `Fill view (crop); OFF = fit whole image`로 맞춤 방식을 바꾼다.

| 상태 | 배경 표시 |
| --- | --- |
| `Fill view` ON | 가로세로 비율을 유지한 채 박 영역을 가득 채운다. 화면과 비율이 다르면 이미지 가장자리가 잘린다. |
| `Fill view` OFF | 가로세로 비율을 유지하면서 이미지 전체가 들어오게 맞춘다. 남는 곳은 기존 단색 배경으로 보인다. |
| `Clear background` | 가져온 배경만 제거하고 기존 스튜디오 단색으로 돌아간다. |

배경을 억지로 가로나 세로로 늘리지 않는다. 세로 그림을 가로로 넓은 창에서 `Fill view`로 보면 많이 잘릴 수 있으므로, 그림 전체가 필요한 비교에서는 OFF를 사용한다. PNG의 투명 부분은 알파를 유지한다.

배경은 **카메라에 고정된 2D 이미지**다. 박을 돌려도 함께 돌아가지 않으며 광원을 옮겨도 배경 자체 밝기는 바뀌지 않는다. 배경 사진의 빛이 박에 반사되거나 박의 환경광이 되는 HDRI 기능은 아니다. 조명까지 비교하려면 별도로 `LIGHT`를 조절한다.

질감과 배경에는 서로 다른 이미지를 사용할 수 있다. `Clear texture`는 배경을 지우지 않고, `Clear background`는 질감을 지우지 않는다. 배경을 제거하거나 새 파일로 교체해도 현재 `Fill view` 선택은 유지된다.

## 5. 파일 조건과 취소·실패 처리

| 조건 | 지원 범위 |
| --- | --- |
| 형식 | PNG, JPG/JPEG 이미지 데이터 |
| 해상도 | 가로·세로 **각각** 1~2048px. 정사각형일 필요는 없다. |
| 파일 크기 | 비어 있지 않은 파일, 최대16MiB(16×1024×1024바이트) |
| 표면 질감 | 원본의 색 대신 밝기 차이와 알파를 사용한다. 반복 표시한다. |
| 배경 | 원본 이미지의 색과 알파를 사용한다. |
| 그 외 형식 | PSD·EXR·HDR·TGA·WebP 등의 직접 불러오기는 지원하지 않는다. PNG/JPEG로 준비한다. |

큰 이미지는 선택 전에 이미지 편집기에서 가로·세로 각각2048px 이하로 줄여 저장한다. 앱이 자동으로 초대형 이미지를 축소하는 구조가 아니다. 헤더의 크기·파일 용량을 먼저 확인한 뒤 디코딩한다.

파일 선택창에서 취소하면 이전 파일과 설정이 유지된다. 읽기 실패·손상·지원하지 않는 데이터·크기 초과도 이전 결과를 지우지 않고 해당 영역에 오류를 표시한다. 확장자만 `.png`로 바꿔도 이미지 데이터가 바뀌지 않으므로 해결되지 않는다.

불러온 파일은 읽기만 한다. 앱이 외부 원본을 덮어쓰거나 Unity 프로젝트에 자동 임포트하지 않는다. 새 파일 적용이 성공한 뒤 이전 임시 이미지를 해제하며, 종료 시 불러온 이미지와 표시용 자원도 해제한다. 파일창을 여는 동안 박 조작은 잠시 멈춘다.

## 6. 네 가지 표현 프리셋 비교하기

`PRESET`은 같은 고정 도안의 표현 방식을 바꾼다. 선택된 버튼이 강조된다. 이미지 비교 앱은 **네 번째 `Sticker-local coating`으로 시작**한다.

| 버튼 | 비교할 내용 | 기본 두께 | 기본 Gloss |
| --- | --- | ---: | ---: |
| `Original v2` | 기존 #524 표현 기준 | 0.0100 | 0.35 |
| `Original outline + enhanced clay` | 기존 얼룩 윤곽에 개선된 재질 | 0.0140 | 0.08 |
| `Rounded outline + enhanced clay` | 새 둥근 윤곽과 개선 재질 | 0.0140 | 0.08 |
| `Sticker-local coating` | 둥근 윤곽에 스티커 자체 방향의 코팅 광택(V4) | 0.0140 | 0.08 |

프리셋을 바꾸면 **두께·Gloss·접촉 음영·기본 미세 질감은 해당 프리셋의 기본값으로 돌아간다.** 외부 질감·배경·이미지 조절값·편집한 팔레트·빛·현재 시점은 유지한다. 따라서 사용자가 바꾼 두께까지 똑같이 비교하려면 프리셋을 바꾼 뒤 같은 숫자로 다시 맞춘다.

`Original v2`에서는 개선 재질 전용 `CONTACT SHADE`와 `FINE DETAIL`이 비활성화된다. 외부 파일의 추가 질감과 개선 재질에 원래 있는 미세 질감은 서로 다른 설정이다.

새 윤곽은 같은 타격 기록을 다른 표현용 브러시로 그린 결과다. 얼룩 배치를 보기 좋게 다시 뽑거나 오래된 자국을 삭제하는 기능은 아니다.

## 7. 두께·광택·음영 조절하기

패널을 내려 `THICKNESS`부터 확인한다. 슬라이더 옆 숫자가 현재값이다.

| 항목 | 범위 | 개선 프리셋 시작값 | 무엇이 바뀌나 |
| --- | --- | ---: | --- |
| `THICKNESS` | 0~0.02 | 0.0140 | 스티커의 돌출 높이. 실제 박의 메시 형상을 교체하거나 도색 판정 면적을 바꾸지 않는다. |
| `GLOSS` | 0~1 | 0.08 | 코팅의 추가 반짝임. 0은 추가 광택이 없고, 높이면 강해진다. 모든 스티커에 같은 강도를 쓴다. |
| `CONTACT SHADE` | 0~0.5 | 0.15 | 겹친 스티커의 높이 차이에 따른 부드러운 접촉 음영. 개선 프리셋 전용이다. |
| `FINE DETAIL` | 0~1 | 1 | 개선 재질에 내장된 미세 요철·얕은 주름 강도. 외부 질감의 `SHALLOW RELIEF`와 별도다. |

V4는 도장 자체의 표면 방향을 이용해 반사를 나누며, 스티커가 없는 완료 바탕은 추가 코팅 반사에서 제외한다. 다만 강한 빛·Gloss·밀집된 얼룩의 조합은 큰 밝은 부분으로 보일 수 있다. 스티커별 반사 여부를 비교할 때는 같은 시점에서 `GLOSS 0 → 0.35 → 1`로 바꾸고 빛 방향도 조금 움직여 본다.

두께0에서도 색 그림은 남는다. 높이에 의존하는 접촉 음영·재질 세부 변화와 추가 표면 질감의 명암은 별개이므로, 완전히 평평한 색 기준을 보려면 외부 질감도 OFF하고 개선 재질의 `FINE DETAIL`도0으로 둬 비교한다.

## 8. 5색 팔레트와 HEX 입력

`PALETTE · RGB / HEX`에는 화·수·목·금·토 색이 각각 있다. 색 견본은 입력한 기본색이며, 실제 박 화면에는 농도·광택·조명이 더해지므로 견본과 화면 픽셀이 완전히 같은 RGB로 보이는 것은 아니다.

| 속성 | 기본 HEX | 기본 RGB(R, G, B) |
| --- | --- | --- |
| 화 / FIRE | `D85736` | 216, 87, 54 |
| 수 / WATER | `1471A7` | 20, 113, 167 |
| 목 / WOOD | `3D8258` | 61, 130, 88 |
| 금 / METAL | `F8B72A` | 248, 183, 42 |
| 토 / EARTH | `6F4A32` | 111, 74, 50 |

### HEX로 바꾸기

1. 바꿀 속성의 `HEX` 칸을 클릭한다.
2. 기존 내용을 지우고 예를 들어 `D85736`처럼 **6자리 RRGGBB**를 입력한다.
3. Enter나 Tab을 누르거나 다른 칸을 클릭해 확정한다. 입력 도중 매 글자를 바로 적용하지 않는다.
4. 색 견본·RGB 숫자·박의 해당 색이 함께 바뀌는지 확인한다.

대문자·소문자와 앞의 `#`, 앞뒤 공백을 허용한다. 예를 들어 `#d85736`도 된다. `FFF` 같은3자리, 알파가 포함된8자리, 0~9/A~F 이외 문자는 거절한다. 오류 테두리와 `HEX needs 6 digits: 0–9, A–F. Previous color is kept.`가 보이면 이전 유효 색이 유지된 상태다. 올바른6자리를 다시 입력한다.

### RGB로 바꾸기

각 `R`, `G`, `B`에 정수를 입력하고 확정한다. 채널 범위는0~255이며 범위 밖 값은 경계값으로 맞춘다. RGB 확정 시 HEX와 견본도 갱신된다.

색이 바뀌면 기존 고정 타격 기록을 새 팔레트로 다시 표현한다. `Updating colors…`가 보이는 동안에는 새 결과를 만드는 중이다. 준비 전에는 이전 완성 그림을 유지하며, 반복 입력해도 오래된 처리 결과를 최신 입력 위에 덮어쓰지 않게 한다. 원래 기본색으로 돌아오면 고정 지도를 직접 사용한다.

`Reset palette`는 다섯 색만 위 기본색으로 돌린다. 질감 파일·배경·빛·시점·두께·Gloss는 그대로다. 색을 바꿔도 스티커의 높이·위치·소유권·게임 규칙은 바뀌지 않는다.

## 9. 광원 움직이기

`LIGHT`는 박을 비추는 비교용 직접광이다. 박을 돌리는 것과 빛을 돌리는 것을 나눠 비교할 수 있다.

| 항목 | 범위 | 초기값(정확한 내부값 / 화면 반올림) |
| --- | --- | --- |
| `LEFT / RIGHT` | −180~180도 | −41.4914도 / 약−41도 |
| `UP / DOWN` | −80~80도 | 70.0484도 / 약70도 |
| `LIGHT INTENSITY` | 0~2 | 1.00 |

`LEFT / RIGHT`로 좌우 방향, `UP / DOWN`으로 높이 방향을 바꾼다. `LIGHT INTENSITY`를0으로 내려도 기존 확산·기본 음영까지 모두 검게 만드는 설정은 아니다. 비교용 직접광 강도를 바꾸는 값이다.

`Reset light`는 위 세 값만 초기화한다. 프리셋을 바꿔도 빛은 유지되지만 `Reset preset`은 빛도 초기화한다. 외부 배경의 밝기·색과 HDRI는 이 슬라이더로 바뀌지 않는다.

## 10. 회전·확대·여섯 방향 보기

- **박 영역에서 왼쪽 버튼을 누른 채 드래그**: 좌우 회전과 위아래 기울기. 버튼·슬라이더 영역에서는 박 회전 입력을 받지 않는다.
- **박 영역에서 휠/스크롤**: 확대·축소. 배율 범위는0.65~3이다. 확대해서 전체 박이 화면 밖으로 나가는 것은 의도한 확대 동작일 수 있다.
- **오른쪽 패널 위에서 스크롤**: 조절 패널 이동.
- 파일 선택창을 열거나 앱이 비활성화되면 진행 중인 드래그는 취소한다.

| `VIEW` 버튼 | 시점 |
| --- | --- |
| `Front` | 정면 |
| `Right` | 우측면 |
| `Back` | 후면 |
| `Left` | 좌측면 |
| `Diagonal` | 좌우45도 사선 |
| `Above` | 좌우45도·위쪽40도 사선 |

방향 버튼은 해당 방향과 **기본 배율1**로 맞춘다. 시점만 바꾸며 색·질감·빛·재질값은 유지한다. `Reset preset`도 정면·기본 배율로 되돌리지만 다른 값까지 함께 초기화하므로 아래 차이를 확인한다.

## 11. 초기화 버튼별 차이와 값 보관

| 동작 | 바뀌는 것 | 유지되는 것 |
| --- | --- | --- |
| 다른 `PRESET` 선택 | 표현 방식, 해당 기본 두께·Gloss, 접촉 음영0.15·기본 미세 질감1 | 파일·이미지 조절값·팔레트·빛·시점 |
| `Reset palette` | 다섯 기본색, 잘못 입력한 HEX 표시 | 파일·이미지 조절값·재질·빛·시점 |
| `Reset light` | 빛의 방향·강도 | 파일·이미지 조절값·색·재질·시점 |
| `VIEW` 방향 버튼 | 시점, 배율1 | 파일·이미지 조절값·색·재질·빛 |
| `Reset preset` | **현재 프리셋**의 기본 재질값, 기본색, 기본빛, 정면·배율1 | 선택한 프리셋, 질감·배경 파일, 이미지 조절값 |
| `Clear texture` | 질감 이미지 제거, ON·크기1·명암0.5·요철0.2로 초기화 | 배경·맞춤 방식·색·빛·재질·시점 |
| `Clear background` | 배경 이미지만 제거 | 질감·Fill 선택·색·빛·재질·시점 |
| 앱 종료 후 다시 실행 | 네 번째 프리셋·기본값·외부 파일 없는 초기 상태 | 고정된 도안 원본과 프로젝트 파일 |

**`Reset preset`으로 외부 파일까지 지워지지는 않는다.** 완전한 파일 미적용 상태가 필요하면 `Clear texture`와 `Clear background`를 각각 누른다.

앱은 값·외부 파일 경로·완성 화면을 저장하거나 내보내는 별도 버튼이 없다. 마음에 드는 조합은 사용한 파일 이름, HEX5개, 질감/재질/빛 숫자, 프리셋을 메모하거나 화면 캡처로 남긴다. 게임에 적용하려면 이 결과를 바탕으로 별도 구현·설정 반영이 필요하다. 비교 앱의 값을 게임 DEV에 자동 복사하지 않는다.

`Quit`는 패널 아래쪽에 있으며 Mac 비교 앱을 종료한다. Unity Editor Play에서의 종료는 Editor의 Play 버튼으로 한다. 앱은 자동 회전하지 않고30fps를 목표로 하며, 다른 앱으로 전환하면 렌더를 멈춘다.

## 12. Unity에서 직접 준비하고 Mac 앱 빌드하기

### 필요한 환경

- 현재 프로젝트 기준 Unity6000.6.5f1과 프로젝트 패키지를 정상적으로 여는 환경.
- macOS에서 Mac Standalone 빌드가 가능한 Unity 설치.
- 네이티브 파일 선택기를 컴파일할 `xcrun clang++`와 macOS SDK/Cocoa, `lipo`, `codesign` 도구. Xcode 또는 사용 가능한 Apple 개발 도구 경로가 준비돼 있어야 한다.
- 저장된 Scene과 컴파일 오류가 없는 프로젝트. Play 모드·컴파일 도중에는 준비/빌드를 실행하지 않는다.

추가 UPM 패키지는 설치하지 않는다. 이 경로는 Mac 실험 앱 빌드이며 iOS Export나 일반 게임의 `Hapio > Development Build`와 다르다.

### 최초 준비 또는 소스/샘플 변경 후

1. 기존 Scene의 미저장 편집을 먼저 확인한다. 필요한 변경을 저장하거나 별도 사본으로 보존한다. 도구가 임의로 대신 저장하지 않는다.
2. Play를 종료하고 컴파일을 기다린다.
3. 메뉴 **`C6 Lab > Experiments > Clay Images > Prepare Texture and Background Preview`**를 선택한다.
4. 준비 과정은 샘플3종·동일 데이터 복사·이미지 Scene 참조와 셰이더 기준을 검사한다. 기존 완성 색/높이 지도를 다시 칠하지 않는다.
5. Editor에서 직접 보려면 `Assets/Experiments/ClayPreview/ImageSamples/Scenes/ImageClayPreview.unity`를 열고 Play한다. Editor의 파일 선택은 네이티브 Mac 플러그인 대신 Unity 파일창을 사용한다.
6. 앱을 만들 때는 Play를 종료한다.

이미지 Scene을 일반 게임의 Build Settings 목록에 추가하지 않는다. 전용 메뉴가 실험 Scene 하나만 지정해 빌드한다. 다른 게임 Scene을 이미지 비교용으로 덮어쓰거나, 게임 Scene에 비교 컨트롤러를 추가하지 않는다.

### 앱 만들기와 찾기

1. 메뉴 **`C6 Lab > Experiments > Clay Images > Build Texture and Background Mac App`**을 선택한다.
2. 빌드가 끝날 때까지 기다린다. Console의 성공/오류를 확인한다.
3. 출력 경로 `Builds/Experiments/ClayPreview/<생성시각-고유값>/Hapio-Clay-Experiment.app`을 연다.
4. 최근 빌드 위치는 **`C6 Lab > Experiments > Clay Sticker > Open Last Mac Preview Build`**에서도 열 수 있다.
5. 앱에서 `FILES / IMAGES`와 샘플3종이 보이는지 확인한다.

`Build Coating Mac App`이나 `Build Mac Comparison App`은 다른 이전 비교 경로다. 외부 질감·배경 파일 기능을 보려면 위 `Clay Images`의 정확한 빌드 메뉴를 사용한다.

빌드는 준비 자료를 **읽기만 하며 자동으로 다시 준비하지 않는다.** 샘플 PNG나 표현 셰이더/include가 바뀌었다는 오류가 나면 변경 이유를 먼저 확인하고 `Prepare Texture and Background Preview`를 명시적으로 실행한 뒤 빌드한다. 임의로 해시 문자를 수정해 검사를 건너뛰지 않는다.

빌드 때만 파일 선택기 네이티브 번들을 arm64+x86_64로 만들고 `Assets/Experiments/ClayPreview/NativeBuildStage`에 임시 배치한다. 정상 종료 시 stage를 정리하고 기존 활성 플랫폼·Build Settings·PlayerSettings를 복원한다. 소스는 [tools/ClayPreviewNative](../tools/ClayPreviewNative/README.md)에 있고 빌드 바이너리는 커밋하지 않는다. 로컬 개발용 서명이 사용되며 배포용 공증을 수행하는 빌드가 아니다.

## 13. 같은 조건으로 평가하는 권장 순서

아래는 비교 작업 순서다. 새로운 게임 규칙이나 확정 미술 설정을 제안하는 표가 아니다.

1. **기준 맞추기**: 네 번째 프리셋 → `Reset preset` → `Clear texture` → `Clear background` → `Front`.
2. **한 가지 질감만 비교**: Fine clay 선택 → 기본1/0.5/0.2에서 ON/OFF. 같은 위치를 확대해 결을 확인한다.
3. **세 샘플 비교**: 같은 숫자에서 Hand-made와 Fine fiber를 바꾼다. 파일이 바뀌어도 도안이나 빛을 함께 바꾸지 않는다.
4. **질감 자체와 광택 분리**: 먼저 Gloss0에서 결을 고르고, 이어 Gloss만 올려 코팅 반사를 본다. 필요하면 명암0/요철0을 각각 시험한다.
5. **빛 영향 확인**: 빛 좌우 방향만 움직인 뒤 Reset light. 배경을 넣기 전후에도 같은 재질과 빛을 유지한다.
6. **색 비교**: 팔레트 한 색만 바꾸고 결과 갱신이 끝난 뒤 확인한다. 색 견본과 조명된 표면을 구분한다.
7. **여섯 방향 확인**: Front·Right·Back·Left·Diagonal·Above에서 반복 경계, 극점, 얼룩 사이 잘림을 확인한다.
8. **기록 남기기**: 사용한 질감/배경 파일과 모든 숫자, 좋았던 점·어색한 부분을 메모한다. 앱 종료 시 임시 설정은 사라진다.

프리셋 전체를 비교할 때는 각 프리셋이 재질 기본값을 다시 지정한다는 점에 유의한다. **같은 두께·Gloss 비교**인지 **각 프리셋 기본 모습 비교**인지 먼저 정해 기록하면 결과를 해석하기 쉽다.

## 14. 자주 생기는 상황

| 증상·실제 안내 | 확인과 해결 |
| --- | --- |
| `FILES / IMAGES`가 없다 | 파일 기능 없는 구형 앱/Scene인지 확인한다. `ImageClayPreview.unity`와 `Clay Images > Build Texture and Background Mac App`을 사용한다. |
| 질감 슬라이더가 흐리거나 눌리지 않는다 | 아직 질감 파일이 없다. 내장 샘플 또는 외부 파일을 고른다. 배경 맞춤도 배경을 고른 뒤 활성화된다. |
| 파일창에서 취소했는데 변화가 없다 | 정상 동작이다. 기존 파일과 값이 유지된다. |
| `Choose an image of 16 MiB or less.` 또는 `Choose a nonempty image of 16 MiB or less.` | 빈 파일/용량을 확인하고16MiB 이하 PNG/JPEG로 다시 저장한다. |
| `Image width and height must each be between 1 and 2048 pixels.` | 가로·세로 각각2048 이하로 준비한다. 한쪽만2048 이하인 것은 부족하다. |
| `Only PNG and JPEG image data is supported.` | 지원 형식으로 내보낸다. 확장자 이름만 바꾸지 않는다. |
| `This PNG/JPEG could not be decoded.` 또는 헤더 오류 | 이미지 편집기에서 정상적으로 열리는지 확인한 뒤 PNG/JPEG로 다시 내보낸다. 기존 적용 파일은 유지된다. |
| `The image could not be read (…)` | 선택한 파일이 읽을 수 있는 로컬 위치에 있는지 확인한다. 원본 접근 문제를 해결한 뒤 다시 고른다. |
| `The Mac picker plug-in is missing. Rebuild the comparison app with its native picker stage.` | 이미지용 전용 Mac 빌드 메뉴로 앱을 다시 만든다. 일반 게임/구형 빌드에 바이너리를 수동 복사하지 않는다. |
| `The Mac picker plug-in has an incompatible version. Rebuild the comparison app.` | 현재 소스로 전용 비교 앱을 다시 빌드한다. |
| `Use Prepare Texture and Background Preview first.` | 저장/Play 종료 후 이미지 준비 메뉴를 먼저 실행한다. |
| `A display shader or texture include changed. Explicitly prepare this preview; Build never refreshes it automatically.` | 소스 변경이 의도한 것인지 확인하고 이미지 준비를 다시 실행한 뒤 빌드한다. |
| 미저장 Scene 안내 때문에 Prepare/Build가 멈춘다 | 편집 내용을 먼저 저장하거나 사본으로 보존한다. 도구는 기존 편집을 자동 저장/폐기하지 않는다. |
| `A native plug-in stage remains from an interrupted build…` | 실제 빌드와 Play가 멈췄는지 확인한다. `C6 Lab > Experiments > Clay Sticker > Remove Stale Mac Picker Stage`로 이 도구 소유의 잔여 stage를 정리하고 다시 빌드한다. |
| `xcrun`/`clang++` 빌드 실패 | Apple 개발 도구 경로와 macOS SDK, Console의 해당 오류를 확인한다. 임의 UPM 패키지 추가로 해결되는 경로가 아니다. |
| HEX 오류 테두리/메시지 | 알파 없는6자리 RRGGBB를 입력하고 확정한다. 이전 유효 색은 유지된다. |
| 질감이 아무것도 안 바뀌는 것 같다 | ON 상태, 무늬가 있는 이미지인지, 명암·요철 강도가0인지 확인한다. 단색 이미지는 중립이다. 같은 박 위치를 확대해 비교한다. |
| 배경이 심하게 잘린다 | `Fill view`를 끈다. ON에서 비율 유지 크롭은 정상이다. |
| Reset을 눌러도 외부 파일이 남는다 | `Reset preset`은 파일 초기화가 아니다. Clear texture/Clear background를 각각 사용한다. |
| 다른 앱을 보고 돌아오니 잠시 멈춘 것 같다 | 비활성 상태에서 렌더를 쉬는 비교 앱이다. 해당 창을 활성화한다. |

## 15. 이 앱으로 바뀌지 않는 것과 현재 한계

- 일반 Lab/Hapio 게임의 기본 표현·DEV 설정·통신·명중·성장·건조·면적·소유권·승패·투척 물리를 바꾸지 않는다. 방 생성·LAN·핫스팟 검증용 앱이 아니다.
- 입력 질감은 색/높이 판정 지도를 덮어쓰는 기능이 아니다. 파일로 각 얼룩의 모양·크기·배치를 바꿀 수 없다.
- 원래 고정 기록의 작은 얼룩, 기존 지원 셀에 따른 일부 잘림, 완료 바탕에 보이는 셀 경계·유령 윤곽까지 해결한 버전은 아니다. 해당 현상과 새 표면 결을 나눠 평가한다.
- 샘플은 반복용 이미지로 제작했지만 수학적으로 완전한 이음새 연결을 보장하지 않는다. 과도한 반복이나 확대에서는 경계가 보일 수 있다.
- `SHALLOW RELIEF`는 조명용 얕은 요철이며 실제 두께 메시를 생성하지 않는다. `GLOSS`는 개별 스티커마다 별도 난수 강도를 주는 설정이 아니다.
- 저장된 완성 도안은1024×512이며 추가 파일 상한2048px과 구분한다. 큰 질감 파일을 넣어도 게임 판정 해상도나 원래 도장 지원 범위를 늘리지 않는다.
- 이번 파일 선택 앱은 Mac 비교용이다. iOS용 파일 선택·실기기 성능·장시간 사용 결과를 이 문서의 기능 존재만으로 통과했다고 해석하지 않는다.

## 16. 유지보수할 때 확인할 파일

| 바꾸려는 부분 | 시작 파일 |
| --- | --- |
| 파일 버튼·샘플·ON/OFF·값 유지·오류 표시 | [ClayPreviewController.Images.cs](../Assets/Experiments/ClayPreview/Runtime/ClayPreviewController.Images.cs) |
| 기존 프리셋·RGB/HEX·빛·회전·Reset | [ClayPreviewController.cs](../Assets/Experiments/ClayPreview/Runtime/ClayPreviewController.cs) |
| 이미지 형식·용량·해상도·회색 데이터 변환·이미지 해제 | [ClayPreviewImageImport.cs](../Assets/Experiments/ClayPreview/Runtime/ClayPreviewImageImport.cs) |
| 스티커 적용 재질·고정 배경·Fit/Fill | [ClayPreviewImagePresentation.cs](../Assets/Experiments/ClayPreview/Runtime/ClayPreviewImagePresentation.cs) |
| 패널·슬라이더·체크 표시 스타일 | [ClayPreview.uss](../Assets/Experiments/ClayPreview/Runtime/ClayPreview.uss) |
| Scene 준비·샘플/소스 검증·이미지 전용 빌드 메뉴 | [ClayPreviewImageExperiment.cs](../Assets/Editor/WatercolorPaint/ClaySticker/ClayPreviewImageExperiment.cs) |
| 네이티브 번들 컴파일·임시 stage·복구 메뉴 | [ClayPreviewMacImagePickerBuild.cs](../Assets/Editor/WatercolorPaint/ClaySticker/ClayPreviewMacImagePickerBuild.cs) · [네이티브 소스 설명](../tools/ClayPreviewNative/README.md) |
| 샘플 원본·같은 바이트의 런타임 자료·Manifest | [ImageSamples](../Assets/Experiments/ClayPreview/ImageSamples) |

셰이더나 샘플을 수정하면 준비 자료와 소스 기준 검사를 함께 확인한다. 게임용 자료와 비교용 자료를 구분하고, 실행하지 않은 환경을 검증 완료로 기록하지 않는다.
