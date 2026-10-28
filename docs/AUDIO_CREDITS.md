# 오디오 출처

게임에 들어간 소리 파일의 출처와 이용 조건을 기록한다. 새 소리를 추가할 때는 이 표에 함께 적는다.

## 배경음악

| 파일 | 용도 | 출처 | 이용 조건 | 가공 |
| --- | --- | --- | --- | --- |
| `Assets/Lab/Resources/Audio/BGM/Kkwaenggwari_13.wav` | 전투 BGM (반복 재생) | 국립국악원, [국악 디지털 음원](https://www.gugak.go.kr/digitaleum) | 출처 표시 조건으로 누구나 이용 가능 | 반복 재생을 위해 앞 무음 약 0.34초, 끝 무음 약 0.22초 제거. 소리 자체는 변경 없음 |
| `Assets/Lab/Resources/Audio/Fever/Fever_Whimori.wav` | 야광귀 피버타임 (#343, 방해가 끝난 뒤 20초, 모든 기기). 전투 BGM 대신 처음부터 한 번 튼다 | 국립국악원, [국악 디지털 음원](https://www.gugak.go.kr/digitaleum/front/monotone/list.do) — 전투 BGM의 "동살풀이"(꽹과리)와 태평소 "연주 - 능게" | 공공누리 제1유형 (출처표시, 상업적 이용·변경 가능) | 꽹과리: 20초 동안 빠르기 1.4배 → 1.9배로 점점 빨라지게(음 높이 유지). 태평소: +3반음 올리고 빠르기 1.25배 → 1.7배로 점점 빨라지게. 두 소리를 섞어(태평소 0.6) 압축·리미터로 크기를 맞추고 앞 0.2초/끝 0.8초를 줄임. 44.1kHz 스테레오 16bit wav, 20초. 만든 방법: ffmpeg rubberband(`asendcmd`로 빠르기 변화) |

## 효과음

| 파일 | 용도 | 출처 | 이용 조건 | 가공 |
| --- | --- | --- | --- | --- |
| `Assets/Lab/Resources/Audio/Generate/Janggu_1_1.wav` ~ `Janggu_1_3.wav`, `Janggu_3_1.wav` ~ `Janggu_3_3.wav` (6개) | 구슬 생성 시 장구 소리 | 국립국악원, [국악 디지털 음원 – 단음 다운로드](https://www.gugak.go.kr/digitaleum/front/monotone/list.do) | 출처 표시 조건으로 누구나 이용 가능 | 앞 무음 약 0.19초 제거, 길이 0.8초로 자름(끝 0.2초 페이드아웃, 연속 생성 시 소리가 겹쳐 묻히지 않도록). 작게 녹음된 `Janggu_1_3`·`Janggu_3_2`·`Janggu_3_3`은 가장 큰 순간이 -0.5dB가 되도록 음량만 키움(+5.6 / +4.9 / +11.1dB). 음색 변경 없음 |
| `Assets/Lab/Resources/Audio/GenerateVoice/Eolssu_1.wav` ~ `Eolssu_3.wav` (3개) | 구슬 생성 시 세 번째마다 나는 "얼쑤" 추임새 (장구 → 장구 → 얼쑤 순서) | 국립국악원, [국악 디지털 음원 – 단음 다운로드](https://www.gugak.go.kr/digitaleum/front/monotone/list.do) – 추임새: 정회석(`Chu-imsae by JungHoeseok_1`) → Eolssu_1, 강효주(`_5`) → Eolssu_2, 이춘희(`_3`) → Eolssu_3 | 출처 표시 조건으로 누구나 이용 가능 | 앞뒤 무음을 잘라 0.52~0.95초로 맞춤(끝 0.04초 페이드아웃). 작게 녹음되어 가장 큰 순간이 -1dB가 되도록 음량만 키움(+9.5~+12.2dB). 96kHz 스테레오를 44.1kHz 모노 16bit로 저장. 음색 변경 없음 |
| `Assets/Lab/Resources/Audio/ComboClear/Jota.wav` | 박 색상 콤보 성공(`CLEAR!`) 때 나는 "좋다" 추임새 (#271, 팀 전체 성공이라 모든 기기) | 국립국악원, [국악 디지털 음원 – 단음 다운로드](https://www.gugak.go.kr/digitaleum/front/monotone/list.do) – 추임새: 염경애 "좋타"(`Chu-imsaebyYeomKyeongae_2.wav`) | 출처 표시 조건으로 누구나 이용 가능 (공공누리 제1유형, 상업적 이용·변경 가능) | 앞 숨소리 0.3초를 잘라내고 끝 무음을 정리해 1.97초로 맞춤(앞 0.01초 페이드인, 끝 0.05초 페이드아웃). 96kHz 32bit float 스테레오를 44.1kHz 모노 16bit로 저장하고 가장 큰 순간이 -1dB가 되도록 음량만 맞춤(+1dB). 음색 변경 없음 |
| `Assets/Lab/Resources/Audio/Hit/Splat_1.wav` | 박 모드 Paint에서 내가 던진 구슬이 요괴의 가운데 쪽에 맞았을 때 나는 물감 소리 (던진 사람 기기만, 3개 중 무작위) – 긴 찰싹 | "Squishes and Splats" 팩 by mrickey13, freesound.org (pack 29012), [검색 링크](https://freesound.org/search/?q=Squish+sound+effect&f=license%3A%22Creative+Commons+0%22+pack_grouping%3A%2229012_Squishes+and+Splats%22) | CC0 (출처 표시 의무 없음) | 이름만 변경. 소리 자체는 변경 없음 |
| `Assets/Lab/Resources/Audio/Hit/Splat_3.wav` | 박 모드 Paint에서 내가 던진 구슬이 요괴의 가운데 쪽에 맞았을 때 나는 물감 소리 (던진 사람 기기만, 3개 중 무작위) – 짧은 찰싹 | "Squishes and Splats" 팩 by mrickey13, freesound.org (pack 29012), [검색 링크](https://freesound.org/search/?q=Squish+sound+effect&f=license%3A%22Creative+Commons+0%22+pack_grouping%3A%2229012_Squishes+and+Splats%22) | CC0 (출처 표시 의무 없음) | 이름만 변경. 소리 자체는 변경 없음 |
| `Assets/Lab/Resources/Audio/Hit/Splat_4.wav` | 박 모드 Paint에서 내가 던진 구슬이 요괴의 가운데 쪽에 맞았을 때 나는 물감 소리 (던진 사람 기기만, 3개 중 무작위) – 중간 길이 찰싹 | "Squishes and Splats" 팩 by mrickey13, freesound.org (pack 29012), [검색 링크](https://freesound.org/search/?q=Squish+sound+effect&f=license%3A%22Creative+Commons+0%22+pack_grouping%3A%2229012_Squishes+and+Splats%22) | CC0 (출처 표시 의무 없음) | 이름만 변경. 소리 자체는 변경 없음 |
| `Assets/Lab/Resources/Audio/HitGlance/Squelch.wav` | 박 모드 Paint에서 내가 던진 구슬이 박의 옆을 스쳐 맞았을 때 나는 낮고 짧은 철퍽 소리 (던진 사람 기기만, 기준은 LabConfig `Glancing Hit Offset`) | "SFX_SQUELCH_slayer_215.wav" by MrFossy, [freesound.org](https://freesound.org/people/MrFossy/sounds/589836/) | CC0 (출처 표시 의무 없음) | 앞뒤 무음을 잘라 0.114초로 맞춤(끝 0.02초 페이드아웃). 24bit를 44.1kHz 16bit 스테레오로 저장. 음량·음색 변경 없음 |
| `Assets/Lab/Resources/Audio/HitCrack/Punch.wav` | 박 모드 Crack에서 내가 던진 구슬이 요괴에 맞았을 때 (던진 사람 기기만) | "Cartoon Punch Cassette E" by modusmogulus, [freesound.org](https://freesound.org/people/modusmogulus/sounds/785935/) | CC0 (출처 표시 의무 없음) | 이름만 변경. 소리 자체는 변경 없음 |
| `Assets/Lab/Resources/Audio/Combine/Combine_1.wav` ~ `Combine_5.wav` (5개) | 내가 음·양 구슬을 결합했을 때 (Host 승인 후 결합한 사람 기기만, 무작위) | "chimes" 시리즈 by _bliind, freesound.org: [chimes-1](https://freesound.org/people/_bliind/sounds/491655/) → Combine_1, [chimes-3](https://freesound.org/people/_bliind/sounds/491660/) → Combine_2, [chimes-7](https://freesound.org/people/_bliind/sounds/491668/) → Combine_3, [chimes-9](https://freesound.org/people/_bliind/sounds/491667/) → Combine_4, [chimes-16](https://freesound.org/people/_bliind/sounds/491656/) → Combine_5 | CC0 (출처 표시 의무 없음) | 소리가 끝난 뒤 남는 무음·잔향을 잘라 길이 0.27~0.57초로 맞춤(끝 0.05초 페이드아웃). 원본이 작게 녹음되어 가장 큰 순간이 -1dB가 되도록 음량만 키움(+11~+20dB). 16bit로 저장. 음색 변경 없음 |
| `Assets/Lab/Resources/Audio/Bomb/Boom/Boom.wav` | 내 판에서 폭탄이 터질 때 (Bomb Sound = BOOM, 그 판 주인 기기만) | "rubble_explode" by canberries4, [freesound.org](https://freesound.org/people/canberries4/) | CC0 (출처 표시 의무 없음) | 앞 무음 약 0.035초 제거, 끝 0.06초 페이드아웃, 가장 큰 순간이 -1dB가 되도록 음량만 키움(+7dB). mp3를 44.1kHz 16bit wav로 저장. 음색 변경 없음 |
| `Assets/Lab/Resources/Audio/Bomb/Ppeong/Ppeong.wav` | 내 판에서 폭탄이 터질 때 (Bomb Sound = PPEONG, 그 판 주인 기기만) | "뻥튀기 뻥2" by 김용배, [공유마당 (한국저작권위원회)](https://gongu.copyright.or.kr/gongu/wrt/wrt/view.do?wrtSn=13253710&menuNo=200020) | **CC BY (저작자 표시)** – 출처 표시 의무 있음 | 끝 0.06초 페이드아웃, 스테레오를 모노로 합치고 가장 큰 순간이 -1dB가 되도록 맞춤. 44.1kHz 16bit로 저장. 음색 변경 없음 |
| `Assets/Lab/Resources/Audio/Miss/Miss.wav` | 내가 던진 구슬이 요괴를 빗나갔을 때 (던진 사람 기기만) | "Woosh_Whip 3" by CAT-FOX_ALEX, [freesound.org](https://freesound.org/people/CAT-FOX_ALEX/sounds/870096/) | CC0 (출처 표시 의무 없음) | 이름만 변경. 소리 자체는 변경 없음 |
| `Assets/Lab/Resources/Audio/FlashClear/FlashClear.wav` | 야광귀 섬광을 흔들어 다 풀었을 때 (#338, 푼 사람 기기만) | 팀 제공 CC0 소리 (원본 이름 미확인, 파일 정보 "Melody Assistant 7.4.7c, 2010") | CC0 | 원본 .ogg(4초, 스테레오 48kHz)에서 소리 있는 앞 0.9초만 남겨 1.1초로 자르고 끝 0.25초를 줄임, 44.1kHz 모노 16bit wav |
| `Assets/Lab/Resources/Audio/FiveColorHit/FiveColorHit_1.wav`, `FiveColorHit_2.wav` (2개) | 내가 던진 오색구슬(#329)이 박에 맞았을 때 (#338, 던진 사람 기기만, 맞을 때마다 1 → 2 → 1 번갈아) | 팀 제공 CC0 소리 (원본 이름 미확인) | CC0 | 원본 .mp3(1.06초, 스테레오 48kHz)의 뒤 무음을 잘라 0.96초, 44.1kHz 모노 16bit wav |
| `Assets/Lab/Resources/Audio/ComboHit/ComboHit.wav` | 박 색상 콤보에서 맞는 칸을 맞힐 때 (#338, 모든 기기). 코드가 1~4칸마다 도·레·미·솔(0·2·4·7반음, 재생 속도 1·1.12·1.26·1.50배)로 높여 낸다 | 팀 제공 CC0 소리 (Freesound, 원본 이름 미확인) | CC0 | 원본 wav(0.46초, 스테레오 44.1kHz, 평균 -29.6dB)를 압축·증폭해 평균 약 -12.5dB(최대 -1.4dB)로 크게 하고 끝 0.1초를 줄임, 44.1kHz 모노 16bit wav |
| `Assets/Lab/Resources/Audio/ComboFail/ComboFail.wav` | 박 색상 콤보 실패(틀린 색·시간 초과) 때 (#338, 팀 전체 실패라 모든 기기) | 팀 제공 CC0 소리 (원본 이름 미확인) | CC0 | 원본 mp3(0.29초, 모노 48kHz)의 앞 무음 0.03초를 잘라 0.26초로 만들고 +3dB(최대 -0.3dB) 키움, 44.1kHz 모노 16bit wav |

**출처 표시 문구 (공공누리 제1유형)**

국립국악원 국악 디지털 음원은 공공누리 제1유형(출처표시)으로 개방되어 있다. 상업적 이용과 변경(자르기·음량 조절)이 가능하며, 아래처럼 출처와 링크를 표시한다. 국립국악원이 이 게임을 후원하거나 특별한 관계가 있는 것처럼 표시하지 않는다. 다운로드 페이지에 작성 연도와 연주자가 나와 있지 않아 연도는 적지 않고, 연주자가 표시된 추임새만 작성자를 적었다.

> 본 저작물은 '국립국악원'에서 작성하여 공공누리 제1유형으로 개방한 '동살풀이(작성자: 국립국악원)'를 이용하였으며, 해당 저작물은 '국립국악원, https://www.gugak.go.kr/digitaleum/front/monotone/list.do'에서 무료로 다운받으실 수 있습니다.

> 본 저작물은 '국립국악원'에서 작성하여 공공누리 제1유형으로 개방한 '장구-덩-강, 장구-덩-중, 장구-덩-약, 장구-쿵-강, 장구-쿵-중, 장구-쿵-약(작성자: 국립국악원)'을 이용하였으며, 해당 저작물은 '국립국악원, https://www.gugak.go.kr/digitaleum/front/monotone/list.do'에서 무료로 다운받으실 수 있습니다.

> 본 저작물은 '국립국악원'에서 작성하여 공공누리 제1유형으로 개방한 '추임새(작성자: 정회석, 강효주, 이춘희, 염경애)'를 이용하였으며, 해당 저작물은 '국립국악원, https://www.gugak.go.kr/digitaleum/front/monotone/list.do'에서 무료로 다운받으실 수 있습니다.

> 본 저작물은 '국립국악원'에서 작성하여 공공누리 제1유형으로 개방한 '연주 - 능게(태평소, 작성자: 국립국악원)'를 이용하였으며, 해당 저작물은 '국립국악원, https://www.gugak.go.kr/digitaleum/front/monotone/list.do'에서 무료로 다운받으실 수 있습니다.

| 우리 파일 | 국립국악원 저작물명 |
| --- | --- |
| `BGM/Kkwaenggwari_13.wav` | 동살풀이 |
| `Generate/Janggu_1_1.wav`, `Janggu_1_2.wav`, `Janggu_1_3.wav` | 장구-덩-강, 장구-덩-중, 장구-덩-약 |
| `Generate/Janggu_3_1.wav`, `Janggu_3_2.wav`, `Janggu_3_3.wav` | 장구-쿵-강, 장구-쿵-중, 장구-쿵-약 |
| `GenerateVoice/Eolssu_1.wav`, `Eolssu_2.wav`, `Eolssu_3.wav` | 추임새 (정회석, 강효주, 이춘희) |
| `ComboClear/Jota.wav` | 추임새 – 좋타 (염경애) |
| `Fever/Fever_Whimori.wav` | 동살풀이 (꽹과리), 연주 - 능게 (태평소) |

**CC BY 소리 (출처 표시 의무 있음)**

> 김용배의 "뻥튀기 뻥2"(공유마당, https://gongu.copyright.or.kr/gongu/wrt/wrt/view.do?wrtSn=13253710&menuNo=200020)은 CC BY 라이선스로 제공됩니다.

CC0 소리(Punch, Splat, Squelch, Combine, Miss, Boom, FlashClear, FiveColorHit, ComboHit, ComboFail)는 표시 의무가 없지만, 크레딧 화면이 생기면 아래처럼 함께 적는 것을 권장한다.

> 타격음 "Cartoon Punch Cassette E" by modusmogulus, 페인트 타격음 "Squishes and Splats" by mrickey13, 스친 타격음 "SFX_SQUELCH_slayer_215" by MrFossy, 결합 소리 "chimes" by _bliind, 빗나감 소리 "Woosh_Whip 3" by CAT-FOX_ALEX, 폭발음 "rubble_explode" by canberries4 (freesound.org, CC0)

게임 안에 크레딧 화면이 생기면 위 문구를 함께 넣는다.
