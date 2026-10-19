# 오행 먹선 비행 트레일

`Profiles/`의 5개 `LabElementTrailProfile`은 `LabOrbAppearance.asset`에서 참조합니다. 게임 수치·도색 색상과 독립된 시각 에셋입니다. 길이·폭·시간·거리 간격·색상을 Inspector에서 바꾼 뒤 새 투척으로 비교합니다. [구현 구조·비교 도구·검증 방법](../../../../docs/ELEMENT_FLIGHT_TRAILS.md)을 참고하세요.

## 텍스처

`Textures/`의 6개 PNG는 실제 알파 채널이 있는 1024×1536 원본입니다. Unity에서는 최대 512로 가져오며 원본 파일은 보존합니다. 검은 먹선과 흰 담채를 가진 중립 마스크에 프로필 색을 곱합니다. 먹선은 검게 유지하고 한지·글씨·구슬 본체는 포함하지 않습니다. 리본은 같은 세로 그림의 UV를 전환하고, 입자는 원래 세로 방향으로 사용합니다. 수의 입자에는 별도 큰 물방울을 사용합니다.

| 파일 | 역할 |
| --- | --- |
| FireBrush.png | 갈라지는 뾰족한 불꽃 |
| WaterStream.png | 이어지는 매끈한 물줄기 |
| WaterDrop.png | 분리된 큰 물방울 |
| WoodLeaf.png | 반복되는 잎 |
| MetalStrand.png | 평행 세 줄 각각에 쓰는 가는 붓선 |
| EarthClump.png | 끊어지는 둥툭한 마른 붓 덩어리 |

`ElementInkTrail.shader`는 URP 전용 Unlit 알파 블렌드입니다. 가산 발광·그림자·물리 콜라이더를 사용하지 않습니다. 리본과 입자 모두 정점 색과 투명 텍스처를 사용합니다.

## 생성 기록

기본 제공 이미지 생성 도구(`image_gen`)로 아래 공통 프롬프트와 파일별 문구를 사용해 만들었으며 `transparent_background=true`를 지정했습니다. 사용자 첨부 시안의 그림체를 참고하되 원본의 글씨·종이·구슬은 추출하지 않았습니다. 에셋에는 개인 경로나 인증 정보가 포함되지 않습니다.

공통 지시:

> A single Unity VFX brush particle/ribbon texture, full RGBA transparent background. Hand-painted Korean ink and flat watercolour with drybrush texture, black ink contour and white opaque wash body only. A neutral luminance mask, no actual coloured pixels, no gray backdrop. White body is visible opaque paint, not a cutout. Subtle transparent bristle gaps; black ink stays black when multiplied by tint. Exactly one isolated vertical motif, centered, fills 80% height, ample transparent padding. Top +Y is forward, bottom trails backward. No paper, text, letters, labels, orb, shadow, glow, fake checkerboard or background.

[원문 프롬프트 6개](../../../../docs/asset-generation/element-flight-trail-prompts.json)를 함께 보존합니다. 아래는 파일별 지시의 요약입니다.

파일별 지시:

- FireBrush: broad central flame body, splitting into three or four long sharp ragged branches; large jagged outer silhouette, no tiny scattered specks.
- WaterStream: continuous smooth slightly S-shaped water stream, thick at top and tapering at bottom; flowing outline, white wash strokes, not flame-like, no detached drops.
- WoodLeaf: one slender pointed leaf, broad middle, tapered stem at bottom, pointed top; irregular ink contour, thin midrib and sparse drybrush gaps.
- MetalStrand: one thin almost straight vertical stripe, fine tapered bottom and blunt top; dry black edges, white wash interior; no extra parallel strands.
- EarthClump: one chunky blunt rounded clump, tall oval with scalloped edges and broken bristles; compact body, no leaf point.
- WaterDrop: one large pear-shaped droplet, rounded heavy bottom and pointed curved top; thin irregular ink contour, white wash and drybrush highlight.

생성 이미지는 구현용 텍스처이며 게임 안의 실제 렌더 검증과 별개입니다. 실행 결과는 `docs/VALIDATION.md`에 기록합니다.
