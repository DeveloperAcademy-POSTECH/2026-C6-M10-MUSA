# 오디오 출처

게임에 들어간 소리 파일의 출처와 이용 조건을 기록한다. 새 소리를 추가할 때는 이 표에 함께 적는다.

## 배경음악

| 파일 | 용도 | 출처 | 이용 조건 | 가공 |
| --- | --- | --- | --- | --- |
| `Assets/Lab/Resources/Audio/BGM/Kkwaenggwari_13.wav` | 전투 BGM (반복 재생) | 국립국악원, [국악 디지털 음원](https://www.gugak.go.kr/digitaleum) | 출처 표시 조건으로 누구나 이용 가능 | 반복 재생을 위해 앞 무음 약 0.34초, 끝 무음 약 0.22초 제거. 소리 자체는 변경 없음 |

## 효과음

| 파일 | 용도 | 출처 | 이용 조건 | 가공 |
| --- | --- | --- | --- | --- |
| `Assets/Lab/Resources/Audio/Generate/Janggu_1_1.wav` ~ `Janggu_1_3.wav`, `Janggu_3_1.wav` ~ `Janggu_3_3.wav` (6개) | 구슬 생성 시 장구 소리 | 국립국악원, [국악 디지털 음원 – 단음 다운로드](https://www.gugak.go.kr/digitaleum/front/monotone/list.do) | 출처 표시 조건으로 누구나 이용 가능 | 앞 무음 약 0.19초 제거, 길이 0.8초로 자름(끝 0.2초 페이드아웃, 연속 생성 시 소리가 겹쳐 묻히지 않도록). 작게 녹음된 `Janggu_1_3`·`Janggu_3_2`·`Janggu_3_3`은 가장 큰 순간이 -0.5dB가 되도록 음량만 키움(+5.6 / +4.9 / +11.1dB). 음색 변경 없음 |
| `Assets/Lab/Resources/Audio/Hit/Punch.wav` | 내가 던진 구슬이 요괴에 맞았을 때 (던진 사람 기기만) | "Cartoon Punch Cassette E" by modusmogulus, [freesound.org](https://freesound.org/people/modusmogulus/sounds/785935/) | CC0 (출처 표시 의무 없음) | 이름만 변경. 소리 자체는 변경 없음 |
| `Assets/Lab/Resources/Audio/Miss/Miss.wav` | 내가 던진 구슬이 요괴를 빗나갔을 때 (던진 사람 기기만) | "Woosh_Whip 3" by CAT-FOX_ALEX, [freesound.org](https://freesound.org/people/CAT-FOX_ALEX/sounds/870096/) | CC0 (출처 표시 의무 없음) | 이름만 변경. 소리 자체는 변경 없음 |

**출처 표시 문구**

> 꽹과리·장구 소리: 국립국악원 국악 디지털 음원 (https://www.gugak.go.kr/digitaleum)

CC0 소리(Punch, Miss)는 표시 의무가 없지만, 크레딧 화면이 생기면 아래처럼 함께 적는 것을 권장한다.

> 타격음 "Cartoon Punch Cassette E" by modusmogulus, 빗나감 소리 "Woosh_Whip 3" by CAT-FOX_ALEX (freesound.org, CC0)

게임 안에 크레딧 화면이 생기면 위 문구를 함께 넣는다.
