using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hapio.Experiments
{
    public sealed partial class ClayPreviewController
    {
        public Shader shapeNoUndercoatCompositor;
        ClayPreviewShapeScenario requestedShapeScenario;
        bool omitShapeUndercoat;
        public ClayPreviewShapeScenario ShapeScenario => ActivePreset == 3 ? requestedShapeScenario : ClayPreviewShapeScenario.Full67;
        public bool OmitShapeUndercoat => ActivePreset == 3 && omitShapeUndercoat;
        bool ComparisonNeedsReplay => ActivePreset == 3 && (requestedShapeScenario != ClayPreviewShapeScenario.Full67 || omitShapeUndercoat);
        public ClayPreviewShapeDiagnostics ShapeDiagnostics => paletteReplay?.Diagnostics;
        Label shapeComparisonStatus, shapeSupportStatus, shapeFootprintStatus;
        Image shapeSupportImage, shapeFootprintImage;
        VisualElement shapeComparisonControls;
        DropdownField shapeScenarioField;
        Toggle shapeUndercoatToggle;
        ulong shownShapeGeneration = ulong.MaxValue;
        readonly List<string> shapeScenarioLabels = new List<string>
        { "기존 67회 완성 기록", "최초 명중 1회 · 정면 진단", "최초 명중 + 재타격 3회 · 같은 지점" };

        partial void BuildShapeDiagnosticsUi(VisualElement panel)
        {
            var foldout = new Foldout { text = "최대 표시 범위 / 적용 비교", value = true, name = "shapeSupportDiagnostics" };
            panel.Add(foldout);
            AddLabel(foldout, "마스크 512 × 512px · 그림 비율과 투명 여백 유지\n최대 표시 = 실제 도색 셀 + 상하좌우·대각선 2셀의 합집합\n고정 크기 사각형이 아닙니다. 한 셀만 칠했다면 최대 5 × 5셀입니다.", "clay-status");
            shapeComparisonControls = new VisualElement(); foldout.Add(shapeComparisonControls);
            shapeScenarioField = new DropdownField("도장 비교", shapeScenarioLabels, 0) { name = "shapeComparisonScenario" };
            shapeScenarioField.RegisterValueChangedCallback(evt => SelectShapeScenario((ClayPreviewShapeScenario)shapeScenarioLabels.IndexOf(evt.newValue)));
            shapeComparisonControls.Add(shapeScenarioField);
            shapeUndercoatToggle = new Toggle("완료 바탕 표시 · 색과 높이 함께") { value = true, name = "shapeUndercoatToggle" };
            shapeUndercoatToggle.RegisterValueChangedCallback(evt => SetShapeUndercoatVisible(evt.newValue));
            shapeComparisonControls.Add(shapeUndercoatToggle);
            shapeComparisonStatus = AddLabel(foldout, string.Empty, "clay-status");
            AddLabel(foldout, "초록: 실제 도색 / 파랑: 최대 허용 범위", "clay-status");
            shapeSupportImage = new Image { name = "shapeMaximumSupport", scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            shapeSupportImage.style.height = 160; foldout.Add(shapeSupportImage);
            shapeSupportStatus = AddLabel(foldout, string.Empty, "clay-status");
            AddLabel(foldout, "같은 곡면 좌표의 원형: 파랑은 허용 / 붉은색은 범위 밖", "clay-status");
            shapeFootprintImage = new Image { name = "shapeClippedFootprint", scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            shapeFootprintImage.style.height = 160; foldout.Add(shapeFootprintImage);
            shapeFootprintStatus = AddLabel(foldout, string.Empty, "clay-status");
            TickShapeDiagnostics();
        }

        public void SelectShapeScenario(ClayPreviewShapeScenario scenario)
        {
            if (!Enum.IsDefined(typeof(ClayPreviewShapeScenario), scenario)) throw new ArgumentOutOfRangeException(nameof(scenario));
            RequireShapeComparison();
            requestedShapeScenario = scenario;
            shapeScenarioField?.SetValueWithoutNotify(shapeScenarioLabels[(int)scenario]);
            RefreshPalettePreview();
        }
        public void SetShapeUndercoatVisible(bool visible)
        {
            RequireShapeComparison();
            if (!visible && (shapeNoUndercoatCompositor == null || !shapeNoUndercoatCompositor.isSupported))
                throw new InvalidOperationException("The comparison-only no-undercoat shader is not connected. Prepare the updated experiment first.");
            omitShapeUndercoat = !visible;
            shapeUndercoatToggle?.SetValueWithoutNotify(visible);
            RefreshPalettePreview();
        }
        void RestoreShapeComparisonSelection()
        {
            if (paletteReplay == null) return;
            requestedShapeScenario = paletteReplay.CompletedScenario;
            omitShapeUndercoat = paletteReplay.CompletedWithoutUndercoat;
            shapeScenarioField?.SetValueWithoutNotify(shapeScenarioLabels[(int)requestedShapeScenario]);
            shapeUndercoatToggle?.SetValueWithoutNotify(!omitShapeUndercoat);
        }
        void RequireShapeComparison()
        {
            if (!PaletteAvailable || PresetCount < 4) throw new InvalidOperationException("Open the image or coating comparison with its four frozen presets.");
            if (ActivePreset == 3) return;
            float thickness = Thickness, gloss = Gloss, contact = ContactStrength, detail = DetailStrength;
            SelectPreset(3); SetAppearance(thickness, gloss); SetSurfaceDetails(contact, detail);
        }

        partial void TickShapeDiagnostics()
        {
            if (shapeComparisonStatus == null) return;
            bool supported = PaletteAvailable && PresetCount >= 4;
            shapeComparisonControls?.SetEnabled(supported);
            if (shapeUndercoatToggle != null) shapeUndercoatToggle.SetEnabled(supported && shapeNoUndercoatCompositor != null && shapeNoUndercoatCompositor.isSupported);
            if (!supported) { shapeComparisonStatus.text = "이 장면에는 고정 기록이 없습니다. 이미지/코팅 비교 장면을 사용하세요."; return; }
            shapeComparisonStatus.text = PaletteBusy ? "새 요청을 처리 중입니다. 아래 범위는 마지막 적용 완료 결과입니다."
                : !string.IsNullOrEmpty(PaletteError) ? "적용 실패: 이전 완료 그림과 범위를 유지합니다. " + PaletteError
                : ActivePreset != 3 ? "코팅 프리셋을 선택하면 윤곽과 최대 범위를 비교할 수 있습니다."
                : ShapeScenario == ClayPreviewShapeScenario.Full67 ? "기존 seed 123 · 67회 기록. 100% 완성 시 지원 제한을 우회하며 틈은 바탕으로 채웁니다."
                : "독립 정면 진단 · seed 123 · 최초 2%, 재타격당 1.5%. 기존 67회 기록의 앞부분을 잘라낸 것이 아닙니다.";
            if (paletteReplay == null || !paletteReplay.HasPublished || paletteReplay.Diagnostics == null)
            {
                shapeSupportImage.image = shapeFootprintImage.image = null;
                shapeSupportStatus.text = "원본 고정 완성 도안입니다. 이미지/절차 모드나 1회·재타격 진단을 선택하면 실제 최대 범위를 표시합니다.";
                shapeFootprintStatus.text = string.Empty; shownShapeGeneration = ulong.MaxValue;
                return;
            }
            if (shownShapeGeneration == paletteReplay.PublishedGeneration) return;
            shownShapeGeneration = paletteReplay.PublishedGeneration;
            var diagnostics = paletteReplay.Diagnostics;
            shapeSupportImage.image = diagnostics.SupportTexture;
            shapeFootprintImage.image = diagnostics.FootprintTexture;
            shapeSupportStatus.text = $"적용 완료: {diagnostics.Columns} × {diagnostics.Rows} = {diagnostics.Columns * diagnostics.Rows:N0}셀\n실제 도색 {diagnostics.PaintedCells:N0}셀 / 최대 허용 {diagnostics.MaximumSupportCells:N0}셀";
            shapeFootprintStatus.text = diagnostics.Complete ? "100% 완료 결과: 지원 제한을 우회합니다. 바탕 끄기는 색과 높이에 함께 적용됩니다."
                : diagnostics.HasFootprint ? $"셀 중심 투영 진단: 원형 {diagnostics.FootprintSamples:N0}개 샘플 중 범위 밖 {diagnostics.OutsideSupportSamples:N0}개\n곡면 투영·현재 도장의 위치와 크기를 반영합니다. AA 픽셀 단위 잘림 비율은 아닙니다."
                : "기존 원본의 세 브러시는 다릅니다. 여기서는 실제 셀의 최대 범위만 표시합니다. 이미지/절차 모드에서 원형의 잘림도 비교할 수 있습니다.";
        }
        partial void ReleaseShapeDiagnostics()
        {
            if (shapeSupportImage != null) shapeSupportImage.image = null;
            if (shapeFootprintImage != null) shapeFootprintImage.image = null;
            shapeSupportImage = shapeFootprintImage = null; shapeComparisonStatus = shapeSupportStatus = shapeFootprintStatus = null;
        }
    }
}
