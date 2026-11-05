# 단일 수채 얼룩 — 평면 텍스처 비교

`C6 Lab > Experiments > Watercolor Flat Texture Comparison`에서 Assets 밖의 출력 폴더를 선택한다. 계획 1~2단계의 Editor 시제품이며 원본 브러시와 V2·V3·Transmission 비교, 게임 기본 경로를 유지한다.

## 비교와 해석

`round-flat-comparison.png`의 왼쪽부터 다음 순서다.

1. 기존 V3 변환 데이터를 사용한 Transmission 도장 수식의 평면 비교.
2. 원본의 RGB·Alpha에서 직접 만든 농담을 사용하는 비교.
3. 같은 원본 농담에서 중간 크기 얼룩 대비만 보강하고 가장 미세한 결은 조금 줄인 비교.

모두 조명·광택·요철·구형 투영·게임 셀 마스크를 제외한다. 첫 열의 최소 wash .02는 이 벤치에서 평면 전체에 적용되므로, 실제 게임의 도색 외곽을 재현한 이미지로 해석하지 않는다. 최종 박 표면의 추가 입자 효과도 포함하지 않는다.

중앙·오른쪽은 같은 색 #DE2D26, 같은 농도 보정 SourceGain=6을 쓴다. 이는 원본의 상대적인 농담을 색으로 표현하기 위한 실험값이며, 원본의 절대 밝기를 그대로 보존했다는 뜻은 아니다. 별도의 검증 출력은 검정·Gain1을 사용해 원본의 흰 바탕 합성과 직접 비교한다.

`round-flat-detail-1to1.png`는 각 1024² 출력의 같은 위치를 512²로 잘라 재확대 없이 붙인 이미지다. 원본 PNG는 1254²이며, 원본 픽셀 단위 충실도 검사는 1254²에서 수행한다.

`round-density-path-comparison.png`는 원본 premultiplied darkness / V2 데이터 R / V3 데이터 R을 흰 바탕에서 비교한다. V2·V3의 512² 데이터에서 384² 본문을 동일한 원본 위치에 맞춘다. 이 이미지는 데이터 변화를 확인하는 용도이며 최종 물감 불투명도·진행률이 아니다. 세 원본의 Alpha·농도와 원본/베이크 PNG도 별도로 출력한다.

## 데이터와 조절

- 원본은 수치 RGB·Alpha로 디코드한다. 원본 색을 임의로 선형화해 농도 데이터로 바꾸지 않는다.
- 직접 표현 데이터: RGB = A × (1 − 원본 RGB), A = 원본 Alpha. RGB와 Alpha를 따로 축소한 뒤 곱하지 않는다.
- 원본 기반 질감: Alpha로 가중한 반경18/4 원본 픽셀 box 평균에서 넓은 농담·중간 변화·미세 잔차를 분리한다. MediumGain=1.4, FineGain=.85, Enhancement=1을 비교 시작값으로 사용한다.
- 새 농담은 원래 안료량의 .65~1.35배 안으로 제한한다. 원본의 투명·순백 영역에 새 안료나 무작위 점을 만들지 않는다.
- Shader Properties와 `LabWatercolorFlatTextureStudy.Configure`에서 같은 값을 조정한다. Enhancement=0은 중앙 출력과 정확히 일치해야 한다.
- 계산 중에는 RGBAFloat 선형 데이터/렌더 타깃을 사용하고, 최종 display RGB를 선형 출력으로 바꾼 뒤 PNG 저장에서 한 번만 display로 변환한다. 수채 감산 혼색이나 유체 시뮬레이션은 아니다.

## 검증과 범위

`flat-texture-study.json`에 셰이더/원본 해시, 기존 Bake와 저장 데이터 일치 여부, 검정·Gain1 원본 충실도, Enhancement0 일치, 투명/순백 영역 보존, 반복 GPU 출력 일치, Scene·설정·Importer·파일 보존을 기록한다.

이 단계는 단일 얼룩의 평면 평가다. 실제 판정 셀 연결·재타격 성장·색층 겹침·박 6방향·관람/PNG 연결은 시각 평가 후 진행한다. 기존 게임 면적·건조·소유권·승패·물리·네트워크 설정은 변경하지 않는다. GPU float 데이터는 Editor 비교용이며 모바일 성능/메모리 검증을 대신하지 않는다.


## 2026-11-04 — 오른쪽 질감 선택과 실제 박 투영

사용자가 평면 비교의 오른쪽(원본 기반 중간 질감 보강)을 선택했다. `C6 Lab > Experiments > Selected Watercolor Gourd Comparison`은 같은 Round 원본과 같은 첫 명중/재타격3회 기록을 실제 WhiteGourd 복사본에 적용한다. 두 열 모두 세 임시 브러시 슬롯을 Round로 고정하므로 브러시 종류 선택에 따른 차이가 없다. 기존 게임의 seed·순번·위치·색/P 판정 지도는 유지한다.

선택안은 SourceGain6·MediumGain1.4·FineGain.85를 그대로 사용한다. 원본 darkness/bands의 mip 복사본은 base픽셀을 보존하며, 작은 자국에서의 샘플링을 위해 trilinear로 읽는다. 후보에는 최소 wash·추가 무작위 노이즈·별도테두리·광택을 더하지 않는다. 기존 overlay-only는 기존 overlap값을 사용한다.

평면의 흰 종이 합성 결과를 선형RGB target C로 바꾸고 alpha=max(1−C), premultipliedRGB=C−(1−alpha)로 표현해 같은 흰 바탕 결과를 유지한다. 통상 source-over의 표현 변환이며 안료 광학모델이 아니다. 기존 지급셀 gate·erase·최종 경계 재구성은 유지한다. 이 때문에 자국의 외곽은 여전히 게임 판정 셀에 맞춰 잘릴 수 있으며, 평면 자국 전체의 윤곽이 실제 박에 그대로 나타난다고 승인된 것은 아니다.

왼쪽은 Transmission Round 대조군, 오른쪽은 선택안이며 grid 윗줄은 최초1회·아랫줄은 최초+재타격3회다. actual/neutral 각각 정상배율1·상세배율1.6을 출력한다. 두 열의 표면은 같고 추가입자·섬유변조·종이요철은 끈다. 기존 확산음영은 남긴다. 게임 기본 V2·Scene·설정·통신은 변경하지 않았다. 완성 전체·다색 겹침·관람 연결과 시각 평가는 후속 범위다.


## 2026-11-04 — 수채 외곽 비교

사용자 피드백에 따라 선택한 질감의 외곽도 같은 Round 원본에서 계산하는 시제품을 추가했다. `C6 Lab > Experiments > Watercolor Brush Contour Comparison`에서 Assets 밖 폴더를 선택한다. 두 열 모두 같은 선택 셰이더·색·질감·Round 패키지·seed와 면적/성장 설정을 쓴다. 왼쪽은 기존 Splatter 판정 마스크, 오른쪽은 원본 Round의 Alpha로 만든 로컬 판정 마스크다. 위는 최초1회, 아래는 최초+재타격3회다.

로컬 판정 입력은 whiteRGB+원본Alpha를 쓰며 기존 threshold.15·면적맞춤·연결성·성장 알고리즘을 유지한다. 최초 칠할 셀과 재타격이 선호하는 외곽이 달라진다. 칠하는 셀의 위치/해시는 의도적으로 변경하고, 소모되는 셀 수·PaintCount·PendingCoverage는 같은 조건인지 검사한다. 동일 원본에서 내부 질감과 외곽을 만들며 원본 파일을 수정하지 않는다.

중간 고해상도 표현에서도 옛 Splatter 외곽이 다시 적용되는 경로를 확인했다. 새 비교의 양쪽은 `_StudyLegacySilhouette=0`을 최종 패스의 머터리얼에 바인딩해 실제 지급셀 보간만 사용한다. 기존 선택 시제품의 기본값1은 유지한다. Stamp 전용 property block은 최종 패스에 남지 않으므로 별도 Editor material callback으로 전달한다.

`LabPaintSurface.SetPatternForAppearanceStudy`와 마스크 factory는 UNITY_EDITOR 전용이다. 로컬 불변 복사본이며 기존 Resources 캐시를 교체하지 않는다. 첫 명중·저널 연결 전의 정상 비Legacy 표면만 허용한다. Clear는 현재 마스크를 유지하고 Reset은 설정 기본으로 돌아간다. 게임 Player의 생성자·리소스·설정·기본 V2는 그대로다. 시각 승인 후 실제 게임 도입은 별도 단계다.
