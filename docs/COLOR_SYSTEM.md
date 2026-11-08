# 합이오 컬러 시스템

2026-11-08 전달된 Figma 컬러 토큰을 보관하며, 앞으로 Hapio UI를 새로 만들거나 수정할 때 이 컬러 시스템을 기준으로 적용한다.

- 원본: [Hapio.tokens.json](../Assets/Hapio/Art/UI/Source/Hapio.tokens.json)
- 구성: `TextColor`, `5OrbsColor`, `NeutralColor`의 색상 토큰 99개, sRGB / `Mode 1`.
- 원본의 토큰 이름·RGB components·HEX·alpha·Figma 변수 ID를 그대로 보존한다. 기존 UI·구슬 팔레트에 자동 적용하는 런타임 설정은 아니다.
- 색상 선택 시 아래 HEX를 참고하고 실제 적용에는 원본의 `$value.alpha`를 함께 사용한다. 토큰 이름에 붙은 `***`와 `%`는 원본 이름의 일부다.
- 주의: `TextColor/DarkIvory/500***70%`의 실제 alpha는 **0.5**다. `SecondaryLightIvory/500***70%`의 alpha는 약 **0.7**이다. 이름만 보고 불투명도를 결정하지 않는다.
- `Stemina`와 `Stemina 2`는 동일한 Stage1~3 색을 가진 별도 원본 그룹으로 보존한다.

## UI 적용 규칙

- UI 요소의 역할과 Figma 변수 ID에 맞는 기존 토큰을 먼저 선택한다. 임의의 유사 색을 만들지 않는다.
- 적용한 코드·Prefab의 색상이 어떤 토큰에서 왔는지 코드 상수 이름·주석 또는 기능 문서에 토큰 경로를 기록한다.
- HEX는 확인용이며 실제 색상과 투명도는 원본의 sRGB components와 alpha를 함께 적용한다. Unity 선형 렌더에서의 합성 결과도 확인한다.
- 새로 만들거나 수정하는 UI부터 적용하며, 별도 색상이 명시된 사용자 요청은 우선한다. 이 파일을 저장하는 것만으로 기존 화면이나 게임 판정용 팔레트를 일괄 변경하지 않는다.

## 전체 팔레트

| 토큰 경로 | HEX | Alpha |
| --- | --- | --- |
| `TextColor/PrimaryBrown/100` | `#DCDAD8` | 1 |
| `TextColor/PrimaryBrown/200` | `#B9B5B1` | 1 |
| `TextColor/PrimaryBrown/300` | `#979189` | 1 |
| `TextColor/PrimaryBrown/400` | `#746C62` | 1 |
| `TextColor/PrimaryBrown/600` | `#41392F` | 1 |
| `TextColor/PrimaryBrown/700` | `#312B23` | 1 |
| `TextColor/PrimaryBrown/800` | `#201C18` | 1 |
| `TextColor/PrimaryBrown/900` | `#100E0C` | 1 |
| `TextColor/PrimaryBrown/500***` | `#51473B` | 1 |
| `TextColor/SecondaryLightIvory/100` | `#FAF6F1` | 1 |
| `TextColor/SecondaryLightIvory/200` | `#F6EDE3` | 1 |
| `TextColor/SecondaryLightIvory/300` | `#F1E4D5` | 1 |
| `TextColor/SecondaryLightIvory/400` | `#EDDBC7` | 1 |
| `TextColor/SecondaryLightIvory/600` | `#BAA894` | 1 |
| `TextColor/SecondaryLightIvory/700` | `#8B7E6F` | 1 |
| `TextColor/SecondaryLightIvory/800` | `#5D544A` | 1 |
| `TextColor/SecondaryLightIvory/900` | `#2E2A25` | 1 |
| `TextColor/SecondaryLightIvory/500***` | `#E8D2B9` | 1 |
| `TextColor/SecondaryLightIvory/500***70%` | `#E8D2B9` | 0.7 |
| `TextColor/DarkIvory/100` | `#F7F2ED` | 1 |
| `TextColor/DarkIvory/200` | `#EEE5DB` | 1 |
| `TextColor/DarkIvory/300` | `#E6D8C9` | 1 |
| `TextColor/DarkIvory/400` | `#DDCBB7` | 1 |
| `TextColor/DarkIvory/500` | `#D5BEA5` | 1 |
| `TextColor/DarkIvory/600` | `#AA9884` | 1 |
| `TextColor/DarkIvory/700` | `#807263` | 1 |
| `TextColor/DarkIvory/800` | `#554C42` | 1 |
| `TextColor/DarkIvory/900` | `#2B2621` | 1 |
| `TextColor/DarkIvory/500***70%` | `#D5BEA5` | 0.5 |
| `TextColor/GreenGray/100` | `#E2E2DF` | 1 |
| `TextColor/GreenGray/200` | `#C5C5C0` | 1 |
| `TextColor/GreenGray/300` | `#A8A9A0` | 1 |
| `TextColor/GreenGray/400` | `#8B8C81` | 1 |
| `TextColor/GreenGray/600` | `#58594E` | 1 |
| `TextColor/GreenGray/700` | `#42433A` | 1 |
| `TextColor/GreenGray/800` | `#2C2C27` | 1 |
| `TextColor/GreenGray/900` | `#161613` | 1 |
| `TextColor/GreenGray/500***` | `#6E6F61` | 1 |
| `TextColor/DarkRed/100` | `#EFDAD7` | 1 |
| `TextColor/DarkRed/200` | `#DFB5AF` | 1 |
| `TextColor/DarkRed/300` | `#CF9187` | 1 |
| `TextColor/DarkRed/400` | `#BF6C60` | 1 |
| `TextColor/DarkRed/600` | `#8C392D` | 1 |
| `TextColor/DarkRed/700` | `#692B21` | 1 |
| `TextColor/DarkRed/800` | `#461C16` | 1 |
| `TextColor/DarkRed/900` | `#230E0B` | 1 |
| `TextColor/DarkRed/500***` | `#AF4738` | 1 |
| `TextColor/Stemina/Stage1` | `#EFCDA6` | 1 |
| `TextColor/Stemina/Stage2` | `#D4BDA4` | 1 |
| `TextColor/Stemina/Stage3` | `#D0A573` | 1 |
| `TextColor/Stemina 2/Stage1` | `#EFCDA6` | 1 |
| `TextColor/Stemina 2/Stage2` | `#D4BDA4` | 1 |
| `TextColor/Stemina 2/Stage3` | `#D0A573` | 1 |
| `5OrbsColor/Firered/100` | `#F6D5D0` | 1 |
| `5OrbsColor/Firered/200` | `#EDABA1` | 1 |
| `5OrbsColor/Firered/400` | `#DC5844` | 1 |
| `5OrbsColor/Firered/600` | `#A92511` | 1 |
| `5OrbsColor/Firered/700` | `#7F1C0D` | 1 |
| `5OrbsColor/Firered/800` | `#541208` | 1 |
| `5OrbsColor/Firered/900` | `#2A0904` | 1 |
| `5OrbsColor/Firered/300***` | `#E58273` | 1 |
| `5OrbsColor/Firered/500***` | `#D32E15` | 1 |
| `5OrbsColor/WaterBlue/100` | `#CFDFE9` | 1 |
| `5OrbsColor/WaterBlue/200` | `#9FC0D3` | 1 |
| `5OrbsColor/WaterBlue/400` | `#3E81A8` | 1 |
| `5OrbsColor/WaterBlue/600` | `#0B4E75` | 1 |
| `5OrbsColor/WaterBlue/700` | `#083A58` | 1 |
| `5OrbsColor/WaterBlue/800` | `#06273A` | 1 |
| `5OrbsColor/WaterBlue/900` | `#03131D` | 1 |
| `5OrbsColor/WaterBlue/300***` | `#6EA0BE` | 1 |
| `5OrbsColor/WaterBlue/500***` | `#0E6192` | 1 |
| `5OrbsColor/TreeGreen/100` | `#D5E1DA` | 1 |
| `5OrbsColor/TreeGreen/200` | `#ACC3B5` | 1 |
| `5OrbsColor/TreeGreen/400` | `#59876B` | 1 |
| `5OrbsColor/TreeGreen/600` | `#265438` | 1 |
| `5OrbsColor/TreeGreen/700` | `#1C3F2A` | 1 |
| `5OrbsColor/TreeGreen/800` | `#132A1C` | 1 |
| `5OrbsColor/TreeGreen/900` | `#09150E` | 1 |
| `5OrbsColor/TreeGreen/300***` | `#82A590` | 1 |
| `5OrbsColor/TreeGreen/500***` | `#2F6946` | 1 |
| `5OrbsColor/MetalYellow/100` | `#FEF0D3` | 1 |
| `5OrbsColor/MetalYellow/200` | `#FDE1A6` | 1 |
| `5OrbsColor/MetalYellow/400` | `#FAC44D` | 1 |
| `5OrbsColor/MetalYellow/600` | `#C7911A` | 1 |
| `5OrbsColor/MetalYellow/700` | `#956D14` | 1 |
| `5OrbsColor/MetalYellow/800` | `#64480D` | 1 |
| `5OrbsColor/MetalYellow/900` | `#322407` | 1 |
| `5OrbsColor/MetalYellow/300***` | `#FBD37A` | 1 |
| `5OrbsColor/MetalYellow/500***` | `#F9B521` | 1 |
| `5OrbsColor/SoilBrown/100` | `#E0D9D4` | 1 |
| `5OrbsColor/SoilBrown/200` | `#C1B3A9` | 1 |
| `5OrbsColor/SoilBrown/400` | `#846654` | 1 |
| `5OrbsColor/SoilBrown/600` | `#513321` | 1 |
| `5OrbsColor/SoilBrown/700` | `#3D2619` | 1 |
| `5OrbsColor/SoilBrown/800` | `#281A10` | 1 |
| `5OrbsColor/SoilBrown/900` | `#140D08` | 1 |
| `5OrbsColor/SoilBrown/300***` | `#A38C7F` | 1 |
| `5OrbsColor/SoilBrown/500***` | `#654029` | 1 |
| `NeutralColor/Black` | `#000000` | 1 |
