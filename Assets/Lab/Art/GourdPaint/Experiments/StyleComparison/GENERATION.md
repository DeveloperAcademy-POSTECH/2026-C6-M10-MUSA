# Experiment #200 — 물감 스타일 마스크 원본

내장 `image_gen` 도구로 2026-10-18 생성한 RGB 흑백 마스크3장이다. 원본1254×1254 PNG를 변형하지 않고 사용한다. 외부 참고 이미지를 복사하거나 원본 게임/그림을 텍스처로 붙인 것이 아니다. 밝기는 물감 발자국, 검정은 바깥 영역이다. `PaintStyleMaskImporter`가 linear/readable/무압축/no mipmap/NPOT 유지로 가져온다. 이 에셋은 비교 도구의 입력이며 기존 전투의 Resources·Config·Scene에는 연결하지 않는다.

| 파일 | 형태 |
| --- | --- |
| Broad.png | 서로 다른 크기의 둥근 덩어리를 연결한 넓은 비대칭 얼룩 |
| Splatter.png | 큰 본체·짧은 돌출부·가까운 작은 튄 점 |
| Flow.png | 위쪽 큰 본체와 길이가 다른 가는 아래 줄기 |

공통 생성 프롬프트:

> Create one production grayscale paint brush mask bitmap for a Unity game texture, square 1024x1024 or higher resolution. Flat pure black background. White opaque paint footprint, small medium-gray variations ONLY near ragged edges; high contrast matte white interior. Absolutely no colors, lettering, labels, photographs, brush tools, sphere, gourd, cast shadows, lighting, gradients across the canvas, watermark, frame or border. Keep the entire footprint inside a 12% black safety margin. The black background is intentionally solid, this is a luminance mask. Hand-authored organic paint silhouette with a strong large-scale shape, not a circular noise cloud.

Broad 추가 프롬프트:

> Broad blotch: a wide asymmetric splodge of spilled thick paint made of 4 to 6 unequal rounded lobes merged into one connected body, wider than tall. Gentle large concave bays, occasional short torn protrusions. Mostly one big dense body. No detached droplets and no long thin drips. Asymmetry and big lobes are more important than tiny grain.

Splatter 추가 프롬프트:

> Thrown paint splatter: one central dense irregular wide splash body with several unequal short arms and torn scalloped edges, off-center lobes. 6 to 12 small isolated satellite droplets very close to the main splash, varying size; no perfectly radial star, no symmetric spikes, no long downward drips. Preserve a substantial broad center, not a spray of tiny dots.

Flow 추가 프롬프트:

> Runny paint: a broad asymmetric paint blotch occupying the upper half of the footprint with 3 or 4 sparse slender downward drips of visibly unequal lengths. Main body has several unequal rounded lobes and ragged edges. Narrow drips, widely spaced, with subtly bulbous rounded ends; one reaches lower than others. Upper mass dominates, avoid one broad triangular wedge and avoid a curtain of evenly spaced noodles. A few short ragged protrusions are okay.

생성 지시의12% 여백·물방울 수는 시안 방향이다. 실제 결과의 정확한 여백/돌출부 수를 보장하는 사양이 아니며, 가져온 픽셀/실제 곡면 렌더를 검사한다. 전체 원시 렌더와 실행 로그는 별도 로컬에 보존한다. 개인 정보가 없는 선택용 비교1장만 `docs/images/gourd-paint-style-comparison.png`로 공개하며 추가 보정 없이 실제 메시 렌더를 그대로 사용한다.
