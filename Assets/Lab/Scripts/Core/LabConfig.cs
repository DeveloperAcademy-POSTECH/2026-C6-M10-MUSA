using System;
using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    /// <summary>Arc preserves the existing release; Straight changes only launch pose and flight gravity.</summary>
    public enum LabThrowForm { Arc = 0, Straight = 1 }

    /// <summary>One numeric field in the development-build tuning panel.</summary>
    public readonly struct LabDeveloperField
    {
        public readonly string Label;
        public readonly float Min;
        public readonly float Max;
        public readonly bool WholeNumber;
        /// <summary>켜기/끄기(또는 둘 중 하나) 항목이면 0일 때·1일 때 버튼 글자. 숫자 항목은 null.</summary>
        public readonly string OffText, OnText;
        /// <summary>Named choices preserve their numeric wire values without a numeric UI.</summary>
        public readonly IReadOnlyList<string> Choices;
        public bool IsChoice => Choices != null;

        /// <summary>DEV 화면에서 숫자 대신 토글 버튼으로 보여주는 0/1 항목인지</summary>
        public bool IsToggle => OnText != null;

        /// <summary>화면에 보이는 이름. 토글은 "(0 off, 1 on)" 같은 설명을 뗀다. Label 자체는 테스트·검색용으로 그대로 둔다.</summary>
        public string DisplayLabel
        {
            get
            {
                if (!IsToggle || Label == null) return Label;
                int bracket = Label.IndexOf(" (", StringComparison.Ordinal);
                return bracket > 0 ? Label.Substring(0, bracket) : Label;
            }
        }

        /// <summary>0/1 값을 토글 버튼으로 보여주는 항목. 값과 저장·공유 방식은 숫자 항목과 같다.</summary>
        public static LabDeveloperField Toggle(string label, string offText = "OFF", string onText = "ON") =>
            new LabDeveloperField(label, 0f, 1f, true, offText, onText);

        public static LabDeveloperField Choice(string label, params string[] choices) =>
            new LabDeveloperField(label, 0f, choices.Length - 1, true, null, null,
                Array.AsReadOnly((string[])choices.Clone()));

        public LabDeveloperField(string label, float min, float max, bool wholeNumber = false)
            : this(label, min, max, wholeNumber, null, null) { }

        private LabDeveloperField(string label, float min, float max, bool wholeNumber, string offText, string onText,
            IReadOnlyList<string> choices = null)
        {
            Choices = choices;
            OffText = offText;
            OnText = onText;
            Label = label;
            Min = min;
            Max = max;
            WholeNumber = wholeNumber;
        }
    }

    ///<summary>
    /// DEV 패널에서 보이는 묶음. 항목 번호 (DeveloperFields 순서)는 바꾸지 않고, 
    /// 화면에 보이는 위치만 묶음별로 모은다. 
    /// </summary>
    public readonly struct LabDeveloperCategory
    {
        public readonly string Title;
        public readonly int[] FieldIndices;

        /// <summary>
        /// 묶음 안 구분선 (#268). DividerAt[k] 번째 줄(FieldIndices 안 위치) 바로 위에 DividerLabels[k]를 보여준다.
        /// 화면 표시만 바뀌고 값 번호는 그대로다. 없으면 null.
        /// </summary>
        public readonly int[] DividerAt;
        public readonly string[] DividerLabels;
        /// <summary>
        /// 묶음 제목 오른쪽의 한꺼번에 켜기/끄기 버튼이 함께 바꾸는 토글 항목 번호 (#268). 없으면 null.
        /// 새 설정값이 아니라 아래 토글들을 같은 값으로 맞추는 화면 단축키다.
        /// </summary>
        public readonly int[] SwitchFields;

        public LabDeveloperCategory(string title, params int[] fieldIndices)
            : this(title, fieldIndices, null, null, null) { }

        private LabDeveloperCategory(string title, int[] fieldIndices, int[] dividerAt, string[] dividerLabels,
            int[] switchFields)
        {
            Title = title;
            FieldIndices = fieldIndices;
            DividerAt = dividerAt;
            DividerLabels = dividerLabels;
            SwitchFields = switchFields;
        }

        /// <summary>줄 위치(FieldIndices 안 순서)와 글자를 짝지어 구분선을 붙인다.</summary>
        public LabDeveloperCategory WithDividers(params (int before, string label)[] dividers)
        {
            var at = new int[dividers.Length];
            var labels = new string[dividers.Length];
            for (int i = 0; i < dividers.Length; i++) { at[i] = dividers[i].before; labels[i] = dividers[i].label; }
            return new LabDeveloperCategory(Title, FieldIndices, at, labels, SwitchFields);
        }

        /// <summary>제목 오른쪽 버튼 하나로 이 토글들을 함께 켜고 끄게 한다.</summary>
        public LabDeveloperCategory WithSwitch(params int[] toggleFields) =>
            new LabDeveloperCategory(Title, FieldIndices, DividerAt, DividerLabels, toggleFields);
    }

    /// <summary>
    /// The lab's single Inspector-editable gameplay tuning asset. Its values are copied into a
    /// running host session; visual prefabs may be replaced without changing these rules.
    /// </summary>
    [CreateAssetMenu(fileName = "LabConfig", menuName = "C6 Physics Lab/Config")]
    public sealed class LabConfig : ScriptableObject
    {
        // Keep this order aligned with CaptureDeveloperValues and TryApplyDeveloperValues.
        // Bounds apply only to in-game developer input; existing Inspector assets are unchanged.
        public static IReadOnlyList<LabDeveloperField> DeveloperFields { get; } = Array.AsReadOnly(new[]
        {
            new LabDeveloperField("Monster Max HP", 1f, 1000000f, true),
            new LabDeveloperField("Damage per Hit", 1f, 1000000f, true),
            new LabDeveloperField("Battle Duration (seconds)", 1f, 86400f),
            new LabDeveloperField("Maximum Stamina", 1f, 1000000f),
            new LabDeveloperField("Starting Stamina", 0f, 1000000f),
            new LabDeveloperField("Orb Generation Cost", .001f, 1000000f),
            new LabDeveloperField("Stamina Recovery Amount", 0f, 1000000f),
            new LabDeveloperField("Stamina Recovery Interval (seconds)", .001f, 86400f),
            new LabDeveloperField("Stamina Reward per Hit", 0f, 1000000f),
            new LabDeveloperField("Stored Orb Limit", 1f, 100f, true),
            new LabDeveloperField("Orb Lifetime (seconds)", .1f, 86400f),
            new LabDeveloperField("Orb Bounce", 0f, 1f),
            new LabDeveloperField("Orb Floor Deceleration", 0f, 100000f),
            new LabDeveloperField("Orb Stop Speed", 0f, 100000f),
            new LabDeveloperField("Orb Maximum Release Speed", .001f, 100000f),
            new LabDeveloperField("Orb Radius", .001f, 100f),
            new LabDeveloperField("Orb Board Height", .1f, 1000f),
            new LabDeveloperField("Spawn Pop Start Size (0-1 of the orb)", 0f, 1f),
            new LabDeveloperField("Spawn Pop Duration (seconds)", 0f, 2f),
            new LabDeveloperField("Camera Radius", .001f, 1000f),
            new LabDeveloperField("Camera Height Offset", -1000f, 1000f),
            new LabDeveloperField("Throw Origin Radius", .001f, 1000f),
            new LabDeveloperField("Throw Origin Height Offset", -1000f, 1000f),
            new LabDeveloperField("Throw Origin Lateral Range", 0f, 1000f),
            new LabDeveloperField("Throw Minimum Swipe Speed", .001f, 100000f),
            new LabDeveloperField("Throw Minimum Swipe Distance", .001f, 1000f),
            new LabDeveloperField("Throw Upward Dominance", 0f, 1000f),
            new LabDeveloperField("Throw Maximum Swipe Speed", .001f, 100000f),
            new LabDeveloperField("Throw Minimum Upward Speed", .001f, 100000f),
            new LabDeveloperField("Throw Lateral Gain", .001f, 100000f),
            new LabDeveloperField("Throw Upward Gain", .001f, 100000f),
            new LabDeveloperField("Throw Forward Gain", .001f, 100000f),
            new LabDeveloperField("Throw Maximum World Speed", .001f, 100000f),
            new LabDeveloperField("Throw Gravity", .001f, 100000f),
            new LabDeveloperField("Throw Lifetime (seconds)", .001f, 86400f),
            new LabDeveloperField("Throw Radius", .001f, 100f),
            new LabDeveloperField("Throw Power Exponent", .25f, 3f),
            new LabDeveloperField("Throw Loft Offset (degrees)", -30f, 30f),
            new LabDeveloperField("Throw Air Damping", 0f, 5f),
            new LabDeveloperField("Yin Chance (%)", 0f, 100f, true),
            new LabDeveloperField("Yang Chance (%)", 0f, 100f, true),
            new LabDeveloperField("Relay Orb Chance (%)", 0f, 100f, true),
            new LabDeveloperField("Secret Box Chance (%)", 0f, 100f, true),
            new LabDeveloperField("Potion Chance (%)", 0f, 100f, true),
            new LabDeveloperField("Bomb Chance (%)", 0f, 100f, true),
            new LabDeveloperField("Item Lifetime (seconds)", .1f, 86400f),
            new LabDeveloperField("Item Unlock Swipes Min", 0f, 20f, true),
            new LabDeveloperField("Item Unlock Swipes Max", 0f, 20f, true),
            new LabDeveloperField("Player Max HP", 1f, 300f, true),
            new LabDeveloperField("Combine Speed", 0f, 10f),
            LabDeveloperField.Toggle("Drop Combine (0 off, 1 on)"),
            LabDeveloperField.Toggle("Contact Combine (0 off, 1 on)"),
            new LabDeveloperField("Potion Heal (HP)", 0f, 300f, true),
            new LabDeveloperField("Relay x2 Damage Multiplier", 1f, 20f),
            new LabDeveloperField("Relay MAX Damage Multiplier", 1f, 20f),
            new LabDeveloperField("Bomb Damage Multiplier", 1f, 20f),
            new LabDeveloperField("Camera Look Height Offset", -10f, 10f),
            new LabDeveloperField("Target Spin Strength", 0f, 200f),
            new LabDeveloperField("Target Spin Damping", 0f, 10f),
            new LabDeveloperField("Target Spin Maximum Speed (deg/s)", 30f, 1440f),
            new LabDeveloperField("Spawn Fan Angle (deg each side, 180 = all directions)", 0f, 180f),
            new LabDeveloperField("Spawn Minimum Speed (board widths/s)", 0f, 10f),
            new LabDeveloperField("Spawn Maximum Speed (board widths/s)", 0f, 10f),
            // 소리 크기 5개는 DEV에서 0~100 (50 = Inspector 값 그대로, 100 = 2배, 0 = 끔)
            new LabDeveloperField("BGM Volume (50 = Inspector)", 0f, 100f),
            new LabDeveloperField("Generate Sound Volume (50 = Inspector)", 0f, 100f),
            new LabDeveloperField("Hit Sound Volume (50 = Inspector)", 0f, 100f),
            new LabDeveloperField("Miss Sound Volume (50 = Inspector)", 0f, 100f),
            LabDeveloperField.Toggle("Haptics (0 off, 1 on)"),
            new LabDeveloperField("Generate Haptic Style (0 Light 1 Medium 2 Heavy 3 Soft 4 Rigid)", 0f, 4f, true),
            new LabDeveloperField("Generate Haptic Intensity", 0f, 1f),
            new LabDeveloperField("Hit Haptic Style (0-4)", 0f, 4f, true),
            new LabDeveloperField("Hit Haptic Intensity", 0f, 1f),
            new LabDeveloperField("Item Hit Haptic Style (0-4)", 0f, 4f, true),
            new LabDeveloperField("Item Hit Haptic Intensity", 0f, 1f),
            // AUTO (0) follows the room's player count (#236); 1 to 5 fix the number of drums.
            LabDeveloperField.Choice("Drum Count", "AUTO", "1", "2", "3", "4", "5"),
            new LabDeveloperField("Drum Radius", .1f, 1.5f),
            new LabDeveloperField("Drum Drift Speed (board widths/s)", 0f, 1f),
            new LabDeveloperField("Drum Drift Turning (per second)", 0f, 2f),
            new LabDeveloperField("Drum Spin Strength", 0f, 3f),
            LabDeveloperField.Toggle("Gourd Mode (0 paint, 1 crack)", "PAINT", "CRACK"),
            new LabDeveloperField("Bomb Blast Radius (board widths)", 0f, 2f),
            new LabDeveloperField("Bomb Blast Strength (board widths/s)", 0f, 20f),
            new LabDeveloperField("Bomb Haptic Style (0-4)", 0f, 4f, true),
            new LabDeveloperField("Bomb Haptic Intensity", 0f, 1f),
            new LabDeveloperField("Combine Sound Volume (50 = Inspector)", 0f, 100f),
            new LabDeveloperField("Combine Haptic Style (0-4)", 0f, 4f, true),
            new LabDeveloperField("Combine Haptic Intensity", 0f, 1f),
            new LabDeveloperField("Gourd Bomb Interval Min (seconds, 0 = off)", 0f, 600f),
            new LabDeveloperField("Gourd Bomb Interval Max (seconds, 0 = off)", 0f, 600f),
            new LabDeveloperField("Fuse Sound Volume (50 = Inspector)", 0f, 100f),
            new LabDeveloperField("Drum Stop Near From (% of board from entry)", 0f, 100f, true),
            new LabDeveloperField("Drum Stop Near To / Middle From (%)", 0f, 100f, true),
            new LabDeveloperField("Drum Stop Middle To / Far From (%)", 0f, 100f, true),
            new LabDeveloperField("Drum Stop Far To (%)", 0f, 100f, true),
            new LabDeveloperField("Drum Stop Near Chance (%)", 0f, 100f, true),
            new LabDeveloperField("Drum Stop Middle Chance (%)", 0f, 100f, true),
            new LabDeveloperField("Drum Stop Far Chance (%)", 0f, 100f, true),
            new LabDeveloperField("Drum Pass Through Chance (%)", 0f, 100f, true),
            LabDeveloperField.Toggle("Minimap (0 off, 1 on)"),
            new LabDeveloperField("Minimap Size (%, 50 = default)", 0f, 100f, true),
            new LabDeveloperField("Minimap Move X (- left, + right)", -300f, 100f, true),
            new LabDeveloperField("Minimap Move Y (- down, + up)", -300f, 500f, true),
            new LabDeveloperField("Bomb Sound Volume (50 = Inspector)", 0f, 100f),
            LabDeveloperField.Toggle("Bomb Sound (0 boom, 1 ppeong)", "BOOM", "PPEONG"),
            new LabDeveloperField("Paint Surface Resolution (0 low 1 medium 2 high)", 0f, 2f, true),
            new LabDeveloperField("Paint Initial Coverage (%)", .1f, 100f),
            new LabDeveloperField("Paint Repeat Growth (%)", .1f, 100f),
            new LabDeveloperField("Paint Growth Speed (%/s)", .1f, 100f),
            new LabDeveloperField("Paint Fill Chance (%)", 0f, 100f),
            new LabDeveloperField("Paint Vertical Weight", 1f, 4f),
            LabDeveloperField.Toggle("Completed Gourd Viewer (0 off, 1 on)"),
            LabDeveloperField.Toggle("Throw Form (0 arc, 1 straight)", "ARC", "STRAIGHT"),
            new LabDeveloperField("Straight Origin Offset X", -5f, 5f),
            new LabDeveloperField("Straight Origin Offset Y", -5f, 5f),
            new LabDeveloperField("Straight Origin Offset Z", -5f, 5f),
            new LabDeveloperField("Straight Pitch Offset (degrees)", -30f, 30f),
            new LabDeveloperField("Straight Loft Scale", 0f, 1f),
            new LabDeveloperField("Straight Gravity Scale", 0f, 2f),
            new LabDeveloperField("Gourd Rope Cycle Amplitude (world units)", 0f, 1f),
            new LabDeveloperField("Gourd Rope Cycle Period (seconds)", .5f, 10f),
            new LabDeveloperField("Gourd Rope Hit Bounce", 0f, 2f),
            new LabDeveloperField("Gourd Rope Spring", 0f, 100f),
            new LabDeveloperField("Gourd Rope Damping", 0f, 20f),
            new LabDeveloperField("Gourd Rope Maximum Hit Extension", 0f, 3f),
            new LabDeveloperField("Gourd Swing Impact Strength", 0f, 20f),
            new LabDeveloperField("Gourd Swing Maximum Angle (degrees)", 1f, 75f),
            new LabDeveloperField("Gourd Swing Return Strength", 0f, 100f),
            new LabDeveloperField("Gourd Swing Damping", 0f, 20f),
            LabDeveloperField.Toggle("Paint Grain (0 off, 1 on)"),
            new LabDeveloperField("Paint Grain Size", .25f, 4f),
            new LabDeveloperField("Paint Grain Strength", 0f, 1f),
            new LabDeveloperField("Paint Mottle Strength", 0f, 1f),
            LabDeveloperField.Toggle("Paint Growth Style (0 original, 1 spread + flow)", "ORIGINAL", "SPREAD + FLOW"),
            new LabDeveloperField("Paint Edge Raggedness", 0f, 1f),
            LabDeveloperField.Choice("Paint Pattern", "LEGACY", "BROAD", "SPLATTER", "FLOW"),
            new LabDeveloperField("Chuimsae Chance (%)", 0f, 100f, true),
            new LabDeveloperField("Chuimsae Min Gap (generates)", 0f, 20f, true),
            LabDeveloperField.Toggle("Player Paint Patterns (0 off, 1 on)"),
            new LabDeveloperField("Paint Cluster Compactness", 0f, 1f),
            new LabDeveloperField("Paint Cluster Irregularity", 0f, 1f),
            new LabDeveloperField("Gourd Visual Size (x)", .5f, 3f),
            new LabDeveloperField("Bomb Drag Lag (seconds, 0 = sticks to finger)", 0f, 1f),
            new LabDeveloperField("Bomb Release Speed (x orb)", .05f, 1f),
            new LabDeveloperField("Bomb Slide Deceleration (x orb)", 1f, 5f),
            new LabDeveloperField("Bomb Mass (x orb)", 1f, 20f),
            new LabDeveloperField("Blast Burn Seconds", 3f, 5f),
            new LabDeveloperField("Blast Shrink Scale (x radius)", .2f, 1f),
            new LabDeveloperField("Small Orb Paint Share (x paint)", 0f, 1f),
            LabDeveloperField.Toggle("Paint Drying (0 off, 1 on)"),
            new LabDeveloperField("Drying Delay (seconds)", 0f, 60f),
            new LabDeveloperField("Drying Rate (%/s)", 0f, 5f),
            new LabDeveloperField("Nearby Hit Radius (surface degrees)", 1f, 90f),
            new LabDeveloperField("Swipe Block Interval (seconds, 0 = off)", 0f, 600f),
            new LabDeveloperField("Swipe Block Seconds", 1f, 60f),
            new LabDeveloperField("Swipe Block Bomb Gap (seconds)", 0f, 15f),
            LabDeveloperField.Toggle("Gourd Color Combo (0 off, 1 on)"),
            new LabDeveloperField("Combo First Start (seconds)", 1f, 3600f),
            new LabDeveloperField("Combo Repeat Interval (seconds)", 1f, 3600f),
            new LabDeveloperField("Combo Time Limit (seconds)", 1f, 600f),
            new LabDeveloperField("Combo Penalty Duration (seconds)", 0f, 30f),
            new LabDeveloperField("Combo Penalty Size (x)", .01f, 1f),
            LabDeveloperField.Toggle("Combo Penalty Target (0 team, 1 thrower)", "TEAM", "THROWER"),
            new LabDeveloperField("Combo Reward Duration (seconds)", 0f, 30f),
            new LabDeveloperField("Combo Reward Size (x)", 1f, 3f),
            new LabDeveloperField("Combo Warning (seconds)", 0f, 10f),
            LabDeveloperField.Toggle("Board Orb Trail (0 off, 1 on)"),
            // The yokai's sabotage mix and the nine-tailed fox's sabotages (#251). An experiment, off by default.
            LabDeveloperField.Toggle("Sabotage Mix (0 off, 1 on)"),
            new LabDeveloperField("Sabotage Interval Min (seconds)", 1f, 600f),
            new LabDeveloperField("Sabotage Interval Max (seconds)", 1f, 600f),
            new LabDeveloperField("Sabotage Weight Gourd Bomb", 0f, 10f, true),
            new LabDeveloperField("Sabotage Weight Swipe Block", 0f, 10f, true),
            new LabDeveloperField("Sabotage Weight Fox Fire", 0f, 10f, true),
            new LabDeveloperField("Sabotage Weight Charm", 0f, 10f, true),
            new LabDeveloperField("Fox Fire Count Min", 1f, 10f, true),
            new LabDeveloperField("Fox Fire Count Max", 1f, 10f, true),
            new LabDeveloperField("Fox Fire Safe Seconds", 0f, 3f),
            new LabDeveloperField("Charm Seconds", 1f, 30f),
            new LabDeveloperField("Charm Rescue Taps (0 = none)", 0f, 10f, true),
            // The yaguang-gwi's flash (#268): its own schedule, separate from the sabotage mix.
            // Shown in Korean in the 야광귀 DEV group together with the combo.
            LabDeveloperField.Toggle("야광귀 섬광 (0 off, 1 on)", "끔", "켬"),
            LabDeveloperField.Toggle("섬광 대상 (0 모두, 1 한 명)", "모두", "한 명"),
            // Each range is centred on its default, so every slider starts in the middle.
            new LabDeveloperField("섬광 첫 시작 (초)", 1f, 61f),
            new LabDeveloperField("섬광 간격 (초)", 20f, 100f),
            new LabDeveloperField("섬광 예고 (초)", 0f, 4f),
            new LabDeveloperField("섬광 눈부심 (초)", 1f, 13f),
            new LabDeveloperField("섬광 회복 (초)", 0f, 4f)
        });

        /// <summary>Kinds a GENERATE press can create, in the same order as GenerationChances.</summary>
        public static IReadOnlyList<LabOrbKind> GeneratedKinds { get; } = Array.AsReadOnly(new[]
        {
            LabOrbKind.Yin, LabOrbKind.Yang, LabOrbKind.Relay,
            LabOrbKind.SecretBox, LabOrbKind.Potion, LabOrbKind.Bomb
        });

        // 화면 표시용 묶음. 새 DEV 항목을 추가하면 여기 한 곳에 번호를 넣는다.
        public static IReadOnlyList<LabDeveloperCategory> DeveloperCategories { get; } = Array.AsReadOnly(new[]
        {
            new LabDeveloperCategory("Battle", 0, 1, 2, 48),
            new LabDeveloperCategory("Stamina and generation", 3, 4, 5, 6, 7, 8, 9),
            new LabDeveloperCategory("Generation chances", 39, 40, 41, 42, 43, 44),
            new LabDeveloperCategory("Item orbs", 45, 46, 47, 52, 53, 54, 55, 80, 81, 141, 142, 143, 144, 145, 146, 147),
            new LabDeveloperCategory("Orb lifetime", 10),
            new LabDeveloperCategory("Flat orb board", 11, 12, 13, 14, 15, 16, 165),
            new LabDeveloperCategory("Orb combine", 49, 50, 51),
            new LabDeveloperCategory("Spawn presentation", 17, 18, 60, 61, 62),
            new LabDeveloperCategory("Camera and throw origin", 19, 20, 21, 22, 23, 56),
            new LabDeveloperCategory("Throw gesture", 24, 25, 26, 27),
            new LabDeveloperCategory("Throw flight", 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38),
            new LabDeveloperCategory("Throw form", 111, 112, 113, 114, 115, 116, 117),
            new LabDeveloperCategory("Target spin", 57, 58, 59),
            new LabDeveloperCategory("Sound and haptics", 63, 64, 135, 136, 65, 66, 67, 68, 69, 70, 71, 72, 73, 82, 83, 84, 85, 86, 89, 102, 103),
            new LabDeveloperCategory("Drum", 74, 75, 76, 77, 78, 90, 91, 92, 93, 94, 95, 96, 97),
            new LabDeveloperCategory("Gourd", 140, 79, 87, 88, 152, 153, 154, 104, 105, 106, 138, 139, 107, 108, 109, 110, 137,
                118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 131,
                134, 132, 133, 148, 149, 150, 151),
            new LabDeveloperCategory("Minimap", 98, 99, 100, 101),
            new LabDeveloperCategory("Sabotage", 166, 167, 168, 169, 170, 171, 172, 173, 174, 175, 176, 177),
            // 야광귀 묶음 (#268): 야광귀 스테이지에 쓰는 박 색상 콤보와 섬광. 제목 오른쪽 버튼 하나로 둘을 함께 켜고 끈다.
            new LabDeveloperCategory("야광귀", 155, 156, 164, 157, 158, 159, 160, 161, 162, 163,
                178, 179, 180, 181, 182, 183, 184)
                .WithSwitch(155, 178)
                .WithDividers((0, "── 콤보 ──"), (10, "── 섬광 ──"))
        });


        [Header("Battle")]
        [SerializeField, Min(1)] private int monsterMaxHp = 1000;
        [SerializeField, Min(1)] private int damage = 20;
        [SerializeField, Min(1f)] private float battleDurationSeconds = 180f;
        [SerializeField, Range(1, 300)] private int playerMaxHp = 300;

        [Header("Target spin")]
        [SerializeField, Range(0f, 200f)] private float targetSpinStrength = 80f;
        [SerializeField, Range(0f, 10f)] private float targetSpinDamping = 1.2f;
        [SerializeField, Range(30f, 1440f)] private float targetSpinMaximumSpeed = 720f;

        [Header("Stamina and generation")]
        [SerializeField, Min(1f)] private float staminaMax = 100f;
        [SerializeField, Min(0f)] private float staminaStart = 100f;
        [SerializeField, Min(.001f)] private float generateCost = 20f;
        [SerializeField, Min(0f)] private float staminaRecoveryAmount = 20f;
        [SerializeField, Min(.001f)] private float staminaRecoverySeconds = 1.7f;
        [SerializeField, Min(0f)] private float hitRecovery = 10f;
        [SerializeField, Range(1, 100)] private int orbStorageLimit = 20;

        [Header("Generation chances (percent, total 100)")]
        // 북은 음·양만 낸다 (#177). 아이템 확률은 0이지만 규칙 코드는 남겨 DEV에서 다시 켤 수 있다.
        [SerializeField, Range(0, 100)] private int yinChance = 50;
        [SerializeField, Range(0, 100)] private int yangChance = 50;
        [SerializeField, Range(0, 100)] private int relayChance = 0;
        [SerializeField, Range(0, 100)] private int secretBoxChance = 0;
        [SerializeField, Range(0, 100)] private int potionChance = 0;
        [SerializeField, Range(0, 100)] private int bombChance = 0;

        [Header("Item orbs")]
        [SerializeField, Min(.1f)] private float itemLifetimeSeconds = 15f;
        [SerializeField, Range(0, 20)] private int itemUnlockSwipesMin = 2;
        [SerializeField, Range(0, 20)] private int itemUnlockSwipesMax = 4;
        [SerializeField, Range(0, 300)] private int potionHeal = 10;
        [SerializeField, Range(1f, 20f)] private float relayDoubleMultiplier = 2f;
        [SerializeField, Range(1f, 20f)] private float relayMaxMultiplier = 4f;
        [SerializeField, Range(1f, 20f)] private float bombMultiplier = 3f;
        [Tooltip("잠긴 폭탄이 터질 때 구슬을 밀어내는 범위 (보드 너비 대비). 0이면 밀지 않는다.")]
        [SerializeField, Range(0f, 2f)] private float bombBlastRadius = .4f;
        [Tooltip("폭탄 바로 옆 구슬이 밀려나는 속도 (보드 너비/초). 범위 끝으로 갈수록 약해진다.")]
        [SerializeField, Range(0f, 20f)] private float bombBlastStrength = 9f;
        [Tooltip("무거운 폭탄 (#212): 끄는 폭탄이 손가락을 따라오는 데 걸리는 시간 (초). 0이면 구슬처럼 손가락에 붙는다.")]
        [SerializeField, Range(0f, 1f)] private float bombDragLag = .12f;
        [Tooltip("무거운 폭탄: 놓을 때 튕겨 나가는 속도 배율 (구슬 = 1). 작을수록 세게 그어야 옆으로 넘어간다. 던지기에는 쓰지 않는다.")]
        [SerializeField, Range(.05f, 1f)] private float bombReleaseSpeed = .75f;
        [Tooltip("무거운 폭탄: 미끄러질 때 감속 배율 (구슬 = 1). 클수록 빨리 멈춘다.")]
        [SerializeField, Range(1f, 5f)] private float bombSlideDeceleration = 1.3f;
        [Tooltip("무거운 폭탄: 질량 배율 (구슬 = 1). 클수록 구슬과 부딪혀도 덜 밀린다.")]
        [SerializeField, Range(1f, 20f)] private float bombMass = 4f;
        [Tooltip("폭발 패널티 (#213): 폭탄 충격파에 맞은 구슬에 노란 불이 붙어 있는 시간 (초).")]
        [SerializeField, Range(3f, 5f)] private float blastBurnSeconds = 4f;
        [Tooltip("폭발 패널티: 충격파에 맞은 구슬의 반지름 배율. 보이는 크기·충돌·잡기·던졌을 때 박 판정에 모두 쓰고, 사라질 때까지 유지한다.")]
        [SerializeField, Range(.2f, 1f)] private float blastShrinkScale = .5f;
        [Tooltip("폭발 패널티: 작아진 구슬이 박에 맞았을 때 칠하는 양의 배율 (보통 구슬 = 1).")]
        [SerializeField, Range(0f, 1f)] private float smallOrbPaintShare = .5f;

        [Header("Experiment: gourd color combo (#222)")]
        [Tooltip("켜면 전투 중 박이 4칸 색 콤보를 낸다. 끄면 기존 플레이와 같다. 기본 OFF (#266). DEV `Gourd Color Combo`로도 켤 수 있다.")]
        [SerializeField] private bool gourdColorComboEnabled;
        [Tooltip("전투 시작 후 첫 콤보가 나오는 시각 (초, 일시정지 시간 제외)")]
        [SerializeField, Range(1f, 3600f)] private float comboFirstStartSeconds = 50f;
        [Tooltip("콤보가 나오는 간격 (초). 앞 콤보가 나온 시각부터 잰다. 앞 콤보가 아직 진행 중이면 끝난 직후에 나온다.")]
        [SerializeField, Range(1f, 3600f)] private float comboRepeatIntervalSeconds = 60f;
        [Tooltip("콤보 4칸을 모두 맞혀야 하는 제한 시간 (초)")]
        [SerializeField, Range(1f, 600f)] private float comboTimeLimitSeconds = 15f;
        [Tooltip("콤보 실패 후 공 크기·칠 면적이 줄어드는 시간 (초). 0이면 페널티 없음.")]
        [SerializeField, Range(0f, 30f)] private float comboPenaltyDurationSeconds = 15f;
        [Tooltip("페널티 중 던지는 공 반지름과 박에 칠해지는 면적에 곱하는 배율")]
        [SerializeField, Range(.01f, 1f)] private float comboPenaltySizeScale = .1f;
        [Tooltip("페널티 대상. 팀 전체, 또는 틀린 색을 맞힌 사람만 (시간 초과는 언제나 팀 전체).")]
        [SerializeField] private bool comboPenaltyThrowerOnly;
        [Tooltip("콤보 성공 후 팀 전체의 공 크기/칠 면적이 커지는 시간 (초). 0이면 보상 없음.")]
        [SerializeField, Range(0f, 30f)] private float comboRewardDurationSeconds = 15f;
        [Tooltip("보상 중 던지는 공 반지름과 박에 칠해지는 면적에 곱하는 배율")]
        [SerializeField, Range(1f, 3f)] private float comboRewardSizeScale = 2.5f;
        [Tooltip("콤보 4칸이 나오기 전 COMBO! 3, 2, 1 예고를 보여주는 시간 (초, #241). 0이면 예고 없이 바로 나온다.")]
        [SerializeField, Range(0f, 10f)] private float comboWarningSeconds = 3f;

        [Header("Orb lifetime")]
        [SerializeField, Min(.1f)] private float orbLifetimeSeconds = 8f;

        [Header("Flat orb board")]
        [SerializeField, Range(0f, 1f)] private float orbRestitution = .8f;
        [SerializeField, Min(0f)] private float orbFloorDeceleration = 2f;
        [SerializeField, Min(0f)] private float orbStopSpeed = .065f;
        [SerializeField, Min(.001f)] private float orbMaxReleaseSpeed = 3f;
        [SerializeField, Min(.001f)] private float orbRadius = .32f;
        [SerializeField, Min(.1f)] private float boardHeight = 5f;
        [Tooltip("조합판의 결합(오행) 구슬이 움직일 때 던진 구슬처럼 오행 꼬리를 얕게 남긴다 (#254). 크기/진하기는 LabOrbBoardTrail Inspector.")]
        [SerializeField] private bool boardOrbTrailEnabled = true;

        [Header("Orb combine")]
        [Tooltip("두 구슬이 서로 다가간 속도 (보드 너비/초)가 이 값 이상이면 결합. 0 = 스쳐도 결합")]
        [SerializeField, Range(0f, 10f)] private float combineSpeed = 0f;
        [SerializeField] private bool dropCombineEnabled = true;
        [SerializeField] private bool contactCombineEnabled = true;

        [Header("Spawn presentation")]
        [Tooltip("북에서 나올 때 구슬의 처음 크기 (구슬 크기 대비 0~1)")]
        [SerializeField, Range(0f, 1f)] private float spawnPopStartScale = .3f;
        [Tooltip("구슬이 북에서 톡 튀어나와 제 크기가 되는 시간 (초). 이 동안은 다른 것과 부딪히지 않고 날아간다.")]
        [SerializeField, Range(0f, 2f)] private float spawnPopDuration = .15f;
        [Tooltip("북에서 구슬이 튀어나가는 각도. 위쪽 기준 좌우 각각 이 각도까지 (도). 180이면 모든 방향")]
        [SerializeField, Range(0f, 180f)] private float spawnFanAngle = 180f;
        [Tooltip("튀어나가는 최소 속도 (보드 너비/초)")]
        [SerializeField, Range(0f, 10f)] private float spawnMinSpeed = 1f;
        [Tooltip("튀어나가는 최대 속도 (보드 너비/초)")]
        [SerializeField, Range(0f, 10f)] private float spawnMaxSpeed = 1.8f;

        [Header("Drum")]
        [Tooltip("한 판에 놓이는 생성 북 개수. 0이면 AUTO(#236): 인원에 맞춰 2·3명은 1개, 4·5명은 2개(솔로 1개). 1~5면 인원과 상관없이 그 개수. 판 시작 때 받을 사람을 무작위로 정한다.")]
        [SerializeField, Range(0, 5)] private int drumCount = AutoDrumCount;
        [Tooltip("북의 충돌·표시 반지름 (월드 단위). 기본은 예전 화면 아래 생성 버튼 크기.")]
        [SerializeField, Range(.1f, 1.5f)] private float drumRadius = .6f;
        [Tooltip("가만히 둔 북이 저절로 떠도는 최고 속도 (보드 너비/초). 0이면 떠돌지 않는다.")]
        [SerializeField, Range(0f, 1f)] private float drumDriftSpeed = .1f;
        [Tooltip("떠도는 방향이 바뀌는 빠르기. 클수록 자주 방향을 튼다.")]
        [SerializeField, Range(0f, 2f)] private float drumDriftTurning = .25f;
        [Tooltip("떠돌며 돌 때와 부딪혀 돌 때의 회전 세기. 0이면 돌지 않는다.")]
        [SerializeField, Range(0f, 3f)] private float drumSpinStrength = 1f;
        [Tooltip("옆 사람에게 넘긴 북이 받는 판에서 멈추는 구간 경계 (들어온 가장자리에서 판 폭 %). " +
            "가까운 쪽 시작·가까운/가운데·가운데/먼 쪽·먼 쪽 끝 순서 (#178).")]
        [SerializeField] private int[] drumStopBounds = { 20, 40, 60, 80 };
        [Tooltip("넘긴 북이 가까운 쪽·가운데·먼 쪽에서 멈출 확률과 다음 사람에게 통과할 확률 (%, 합 100).")]
        [SerializeField] private int[] drumStopChances = { 40, 35, 15, 10 };

        [Header("Gourd (요괴 박)")]
        [Tooltip("Paint: 흰 박에 구슬 속성 색을 칠한다 (#153). Crack: 박 모델에 금이 가고 처치 연출이 나온다 (#163).")]
        [SerializeField] private LabGourdMode gourdMode = LabGourdMode.Paint;
        // DEMO_TUNING_VALUE: growth settings are captured when a round starts and may be tuned in DEV.
        [SerializeField, Range(0, 2)] private int paintSurfaceResolution = 2;
        [SerializeField, Range(.1f, 100f)] private float paintInitialPercent = 2f;
        [SerializeField, Range(.1f, 100f)] private float paintRepeatPercent = 1.5f;
        [Tooltip("반복 명중 성장을 맞은 영역 가까이에 모으는 정도. 새 판에 적용한다.")]
        [SerializeField, Range(0f, 1f)] private float paintClusterCompactness = .85f;
        [Tooltip("반복 명중 성장 덩어리 경계의 불규칙한 정도. 새 판에 적용한다.")]
        [SerializeField, Range(0f, 1f)] private float paintClusterIrregularity = .25f;
        [SerializeField, Range(.5f, 3f)] private float gourdVisualSize = 1.4f;
        public float GourdVisualSize => Mathf.Clamp(Finite(gourdVisualSize, 1.4f), .5f, 3f);
        [SerializeField, Range(.1f, 100f)] private float paintGrowthPercentPerSecond = 6f;
        [SerializeField, Range(0f, 100f)] private float paintFillChancePercent = 60f;
        [SerializeField, Range(1f, 4f)] private float paintVerticalWeight = 1.35f;
        [Tooltip("근처 유효 명중이 없는 도색을 빈 표면과 맞닿은 경계부터 점진적으로 지운다. 새 판에 적용한다.")]
        [SerializeField] private bool paintDryingEnabled = true;
        [Tooltip("박 표면의 근처에 마지막으로 명중한 뒤 도색을 유지하는 시간 (초).")]
        [SerializeField, Range(0f, 60f)] private float paintDryingDelaySeconds = 5f;
        [Tooltip("건조로 지우는 전체 박 표면 대비 초당 면적 (%p/초). 표면 분할 해상도와 독립적이다.")]
        [SerializeField, Range(0f, 5f)] private float paintDryingPercentPerSecond = .2f;
        [Tooltip("실제 명중점에서 박 표면을 따라 주변 도색의 유지 시간을 갱신하는 범위 (도).")]
        [SerializeField, Range(1f, 90f)] private float paintNearbyHitRadiusDegrees = 20f;
        [Tooltip("Paint 100% 승리 뒤 모든 참가자에게 완성된 박의 로컬 관람을 허용한다. 새 판에 적용한다.")]
        [SerializeField] private bool completedGourdViewerEnabled = true;
        [Tooltip("플레이어 P번호별 물감 무늬를 박과 HUD에 표시한다. 색칠·면적·승리 판정은 바꾸지 않으며 새 판에 적용한다.")]
        [SerializeField] private bool playerPaintPatternsEnabled = true;
        [Tooltip("줄이 스스로 늘고 줄어드는 폭(월드 단위). 0이면 주기 움직임을 끈다.")]
        [SerializeField, Range(0f, 1f)] private float gourdRopeCycleAmplitude = .45f;
        [Tooltip("줄이 한 번 늘었다 줄어드는 데 걸리는 시간(초).")]
        [SerializeField, Range(.5f, 10f)] private float gourdRopeCyclePeriod = 3f;
        [Tooltip("구슬 명중 시 줄을 아래로 당기는 힘. 클수록 더 크게 튕긴다.")]
        [SerializeField, Range(0f, 2f)] private float gourdRopeHitBounce = .32f;
        [Tooltip("명중 후 줄이 원래 길이로 돌아가는 탄성. 클수록 빠르게 왕복한다.")]
        [SerializeField, Range(0f, 100f)] private float gourdRopeSpring = 42f;
        [Tooltip("명중 후 줄의 상하 흔들림이 줄어드는 속도. 작을수록 오래 탱글거린다.")]
        [SerializeField, Range(0f, 20f)] private float gourdRopeDamping = 2f;
        [Tooltip("명중으로 추가로 늘어날 수 있는 최대 길이(월드 단위).")]
        [SerializeField, Range(0f, 3f)] private float gourdRopeMaximumHitExtension = .65f;
        [Tooltip("옆에서 빠르게 맞았을 때 박이 기울어지는 세기.")]
        [SerializeField, Range(0f, 20f)] private float gourdSwingImpactStrength = 12f;
        [Tooltip("박이 수직에서 기울어질 수 있는 최대 각도.")]
        [SerializeField, Range(1f, 75f)] private float gourdSwingMaximumAngle = 40f;
        [Tooltip("박이 중앙으로 돌아오는 힘.")]
        [SerializeField, Range(0f, 100f)] private float gourdSwingReturnStrength = 10f;
        [Tooltip("좌우 흔들림이 줄어드는 속도.")]
        [SerializeField, Range(0f, 20f)] private float gourdSwingDamping = 1.4f;
        [Tooltip("실험용 물감 입자·농담 표현. 도색 셀·면적·속성·승리 판정을 바꾸지 않는다.")]
        [SerializeField] private bool paintGrainEnabled = false;
        [Tooltip("박 표면에 고정된 물감 입자의 상대 크기. 클수록 입자가 크다.")]
        [SerializeField, Range(.25f, 4f)] private float paintGrainSize = 1f;
        [Tooltip("도색된 표면의 작은 입자 명암 세기. 0이면 작은 입자 변화를 끈다.")]
        [SerializeField, Range(0f, 1f)] private float paintGrainStrength = .3f;
        [Tooltip("도색된 표면의 큰 얼룩 농담 세기. 0이면 큰 얼룩 변화를 끈다.")]
        [SerializeField, Range(0f, 1f)] private float paintMottleStrength = .18f;
        [Tooltip("실험용 성장 방식. 0은 기존 성장, 1은 퍼짐과 중력 방향 흐름을 함께 사용한다. 새 판에 적용한다.")]
        [SerializeField, Range(0, 1)] private int paintGrowthStyle = 0;
        [Tooltip("Actual impact footprint. Splatter is the default. Applied only when a new round starts.")]
        [SerializeField] private LabPaintPattern paintPattern = LabPaintPattern.Splatter;
        [Tooltip("퍼짐 경계의 불규칙한 정도. 도색 면적 예산과는 별개다.")]
        [SerializeField, Range(0f, 1f)] private float paintEdgeRaggedness = .55f;
        [Tooltip("박이 무작위 한 명에게 폭탄을 떨어뜨리는 간격의 최소·최대 (초). 매번 이 사이에서 새로 뽑는다. 최대가 0이면 떨어뜨리지 않는다 (#177).")]
        [SerializeField, Min(0f)] private float gourdBombIntervalMin = 20f;
        [SerializeField, Min(0f)] private float gourdBombIntervalMax = 30f;
        [Tooltip("박의 넘기기 막기 (#215): 무작위 한 명의 한쪽 방향 넘기기를 막는 간격 (초). 0이면 막지 않는다.")]
        [SerializeField, Range(0f, 600f)] private float swipeBlockInterval = 40f;
        [Tooltip("넘기기 막기가 이어지는 시간 (초).")]
        [SerializeField, Range(1f, 60f)] private float swipeBlockSeconds = 10f;
        [Tooltip("넘기기 막기가 박 폭탄과 최소 이만큼 떨어져 시작한다 (초). 가까우면 그 폭탄 뒤로 미룬다.")]
        [SerializeField, Range(0f, 15f)] private float swipeBlockBombGap = 4f;

        [Header("Sabotage mix (#251)")]
        [Tooltip("요괴의 통합 방해공작 추첨. 켜면 박 폭탄·넘기기 막기의 각자 타이머를 멈추고, 아래 간격마다 켜진 방해공작 중 하나를 비율대로 골라 무작위 한 명에게 건다. 실험이라 기본은 끔.")]
        [SerializeField] private bool sabotageMixEnabled;
        [Tooltip("통합 추첨 간격의 최소·최대 (초). 매번 이 사이에서 새로 뽑는다.")]
        [SerializeField, Range(1f, 600f)] private float sabotageIntervalMin = 20f;
        [SerializeField, Range(1f, 600f)] private float sabotageIntervalMax = 30f;
        [Tooltip("방해공작별 비율. 0이면 후보에서 뺀다.")]
        [SerializeField, Range(0, 10)] private int sabotageWeightGourdBomb = 1;
        [SerializeField, Range(0, 10)] private int sabotageWeightSwipeBlock = 1;
        [SerializeField, Range(0, 10)] private int sabotageWeightFoxFire = 1;
        [SerializeField, Range(0, 10)] private int sabotageWeightCharm = 1;
        [Tooltip("여우불: 한 번에 떨어지는 여우구슬 개수의 최소·최대. 판이 거의 차 있으면 빈 칸 수만큼만 떨어진다.")]
        [SerializeField, Range(1, 10)] private int foxFireCountMin = 2;
        [SerializeField, Range(1, 10)] private int foxFireCountMax = 5;
        [Tooltip("여우불: 떨어진 뒤 이 시간 (초) 동안은 음·양과 닿아도 조합되지 않는다.")]
        [SerializeField, Range(0f, 3f)] private float foxFireSafeSeconds = 1f;
        [Tooltip("매혹: 공격(위로 던지기)을 못 하는 시간 (초).")]
        [SerializeField, Range(1f, 30f)] private float charmSeconds = 10f;
        [Tooltip("매혹: 양옆 사람이 합쳐 이만큼 하트 말풍선을 누르면 바로 풀린다. 0이면 시간으로만 풀린다.")]
        [SerializeField, Range(0, 10)] private int charmRescueTaps = 3;

        [Header("Yaguang flash (#268)")]
        [Tooltip("야광귀 섬광. 박에서 요괴가 빼꼼 나와 눈이 밝아진 뒤(예고) 화면이 하얗게 번쩍이고, 눈부신 동안 판 위 음양·결합 구슬과 아이템이 하얗게 보인다(북·박 색칠·HUD는 그대로). 화면 표시만 바뀌고 조작·판정은 그대로다. 방해공작 묶음(#251)과 따로 돈다. 야광귀 전용이라 기본은 끔(DEV `Yaguang Flash`로 켠다).")]
        [SerializeField] private bool yaguangFlashEnabled;
        [Tooltip("섬광 대상. 끄면(ALL) 모두에게 동시에, 켜면(ONE) 매번 무작위 한 명에게만.")]
        [SerializeField] private bool yaguangFlashTargetOne;
        [Tooltip("전투 시작 후 첫 섬광(번쩍이는 순간)까지의 시간 (초, 일시정지 제외). 예고는 그 직전에 온다. 범위의 가운데가 기본값.")]
        [SerializeField, Range(1f, 61f)] private float yaguangFlashFirstStartSeconds = 31f;
        [Tooltip("섬광과 다음 섬광 사이 간격 (초). 콤보 예고·진행 중이면 콤보가 끝날 때까지 미룬다. 기본 31·60초는 콤보(50초 시작·60초 간격)와 같은 박자로 콤보 19초 전마다 터져, 섬광(예고~회복 11초)이 콤보(예고 3 + 제한 15 + 결과 + 효과 15 ≈ 35초) 사이 약 25초 틈의 가운데에 놓이고 앞뒤로 약 7초씩 평소 플레이가 남는다.")]
        [SerializeField, Range(20f, 100f)] private float yaguangFlashIntervalSeconds = 60f;
        [Tooltip("번쩍이기 전 예고 시간 (초). 요괴가 빼꼼 나오고 박 옆 말풍선에 눈이 떴다가, 후반에 눈이 갑자기 빛난다. 0이면 예고 없이 번쩍인다.")]
        [SerializeField, Range(0f, 4f)] private float yaguangFlashWarningSeconds = 2f;
        [Tooltip("번쩍인 뒤 구슬·아이템이 완전히 하얗게 보이는 시간 (초)")]
        [SerializeField, Range(1f, 13f)] private float yaguangFlashGlareSeconds = 7f;
        [Tooltip("눈부심이 끝난 뒤 색이 천천히 돌아오는 시간 (초). 0이면 바로 돌아온다.")]
        [SerializeField, Range(0f, 4f)] private float yaguangFlashRecoverySeconds = 2f;

        [Header("Sound (기본 크기 × 기기별 일시정지 메뉴 BGM/SFX 슬라이더)")]
        [Tooltip("전투 BGM 기본 크기")]
        [SerializeField, Range(0f, 1f)] private float bgmVolume = .2f;
        [Tooltip("구슬 생성 장구 소리 기본 크기")]
        [SerializeField, Range(0f, 1f)] private float generateSoundVolume = 1f;
        [Tooltip("내가 던진 구슬이 요괴에 맞았을 때 소리 기본 크기 (던진 사람 기기만)")]
        [SerializeField, Range(0f, 1f)] private float hitSoundVolume = .9f;
        [Tooltip("박 모드 Paint에서 구슬이 박의 옆을 스쳐 맞았다고 보는 기준 (0 = 정중앙, 1 = 가장자리). 박 중심축에서 날아온 방향 기준으로 이만큼 이상 벗어나 맞으면 Audio/HitGlance 소리가 난다.")]
        [SerializeField, Range(0f, 1f)] private float glancingHitOffset = .7f;
        [Tooltip("내가 던진 구슬이 빗나갔을 때 소리 기본 크기 (던진 사람 기기만)")]
        [SerializeField, Range(0f, 1f)] private float missSoundVolume = .7f;
        // DEV에서 정하는 소리 크기 배율 (0~100, 50 = Inspector 값 그대로). Inspector에는 보이지 않고,
        // DEV 적용·방 공유 때만 바뀐다. 실제 크기 = Inspector 값 × (배율 / 50)
        [SerializeField, HideInInspector] private float bgmVolumeDev = DevVolumeDefault;
        [SerializeField, HideInInspector] private float generateSoundVolumeDev = DevVolumeDefault;
        [SerializeField, HideInInspector] private float hitSoundVolumeDev = DevVolumeDefault;
        [SerializeField, HideInInspector] private float missSoundVolumeDev = DevVolumeDefault;
        [SerializeField, HideInInspector] private float combineSoundVolumeDev = DevVolumeDefault;
        [SerializeField, HideInInspector] private float fuseSoundVolumeDev = DevVolumeDefault;
        [SerializeField, HideInInspector] private float bombSoundVolumeDev = DevVolumeDefault;
        [Tooltip("음·양 구슬이 결합될 때 소리 기본 크기 (결합한 사람 기기만)")]
        [SerializeField, Range(0f, 1f)] private float combineSoundVolume = .8f;
        [Tooltip("내 판에 잠긴 폭탄이 있는 동안 도화선이 타는 치지직 소리 기본 크기 (그 판 주인 기기만)")]
        [SerializeField, Range(0f, 1f)] private float fuseSoundVolume = .5f;
        [Tooltip("내 판에서 폭탄이 터질 때 \"뻥이요\"(또는 폭발음) 기본 크기 (그 판 주인 기기만)")]
        [SerializeField, Range(0f, 1f)] private float bombSoundVolume = .9f;
        [Tooltip("폭탄 소리 종류. Boom: 일반 폭발음, Ppeong: 뻥튀기 기계 \"뻥\" 소리. 팀 플레이 테스트로 하나를 고를 예정")]
        [SerializeField] private LabBombSoundStyle bombSoundStyle = LabBombSoundStyle.Ppeong;
        [Tooltip("구슬을 생성할 때마다 장구 대신 추임새(얼쑤·얼씨구)가 날 확률 (%). 0이면 추임새를 내지 않는다.")]
        [SerializeField, Range(0, 100)] private int chuimsaeChancePercent = 15;
        [Tooltip("추임새가 한 번 나온 뒤 최소 몇 번은 장구 소리만 낼지. 연달아 나오지 않게 한다.")]
        [SerializeField, Range(0, 20)] private int chuimsaeMinGap = 5;

        [Header("Haptics (아이폰 진동, 해당 플레이어 기기만)")]
        [Tooltip("끄면 생성·타격 진동이 모두 나지 않는다.")]
        [SerializeField] private bool hapticsEnabled = true;
        [Tooltip("내 구슬이 생성되어 떠오를 때 진동 종류")]
        [SerializeField] private LabHapticStyle generateHapticStyle = LabHapticStyle.Medium;
        [Tooltip("생성 진동 세기 (0 = 끔)")]
        [SerializeField, Range(0f, 1f)] private float generateHapticIntensity = 1f;
        [Tooltip("기본 피해(Damage per Hit) 이하로 맞았을 때 진동 종류")]
        [SerializeField] private LabHapticStyle hitHapticStyle = LabHapticStyle.Heavy;
        [Tooltip("기본 피해일 때 진동 세기 (0 = 끔)")]
        [SerializeField, Range(0f, 1f)] private float hitHapticIntensity = 1f;
        [Tooltip("기본보다 큰 피해(아이템)로 맞았을 때 진동 종류")]
        [SerializeField] private LabHapticStyle itemHitHapticStyle = LabHapticStyle.Heavy;
        [Tooltip("큰 피해일 때 진동 세기 (0 = 끔)")]
        [SerializeField, Range(0f, 1f)] private float itemHitHapticIntensity = 1f;
        [Tooltip("내 판에서 잠긴 폭탄이 터질 때 진동 종류")]
        [SerializeField] private LabHapticStyle bombHapticStyle = LabHapticStyle.Heavy;
        [Tooltip("폭탄 폭발 진동 세기 (0이면 진동 없음)")]
        [SerializeField, Range(0f, 1f)] private float bombHapticIntensity = 1f;
        [Tooltip("내가 구슬을 결합했을 때 진동 종류 (약하게)")]
        [SerializeField] private LabHapticStyle combineHapticStyle = LabHapticStyle.Light;
        [Tooltip("결합 진동 세기 (0 = 끔)")]
        [SerializeField, Range(0f, 1f)] private float combineHapticIntensity = 1f;

        [Header("Minimap (#181)")]
        [Tooltip("전투 화면 오른쪽 아래에 북이 누구한테 있는지 보여주는 미니맵을 켠다.")]
        [SerializeField] private bool minimapEnabled = true;
        [Tooltip("미니맵 크기 0~100 (%). 기본 50이 가운데, 100 = 원래 그린 크기(128×116), 0 = 보이지 않음. 오른쪽 아래 모서리를 기준으로 커지거나 작아진다.")]
        [SerializeField, Range(0, 100)] private int minimapSizePercent = 50;
        [Tooltip("미니맵을 기본 자리(오른쪽 이웃 동그라미 위)에서 옮기는 거리. -는 왼쪽, +는 오른쪽 (화면 폭 390 기준 단위)")]
        [SerializeField, Range(-300, 100)] private int minimapMoveX = 0;
        [Tooltip("미니맵을 기본 자리에서 옮기는 거리. -는 아래, +는 위 (화면 높이 844 기준 단위)")]
        [SerializeField, Range(-300, 500)] private int minimapMoveY = 0;
        public bool MinimapEnabled => minimapEnabled;
        public int MinimapSizePercent => Mathf.Clamp(minimapSizePercent, 0, 100);
        public Vector2 MinimapMove => new Vector2(Mathf.Clamp(minimapMoveX, -300, 100), Mathf.Clamp(minimapMoveY, -300, 500));

        [Header("Replaceable orb presentation")]
        [SerializeField] private LabOrbAppearance orbAppearance;

        [Header("Battle view and throw origin")]
        [SerializeField, Min(.001f)] private float cameraRadius = 8f;
        [SerializeField] private float cameraHeightOffset = .65f;
        [SerializeField, Range(-10f, 10f)] private float cameraLookHeightOffset = .8f;
        [SerializeField, Min(.001f)] private float throwOriginRadius = 4f;
        [SerializeField] private float throwOriginHeightOffset = -.5f;
        [SerializeField, Min(0f)] private float throwOriginLateralRange = 1f;

        [Header("Release throw")]
        [SerializeField, Min(.001f)] private float throwSwipeMinSpeed = .8f;
        [SerializeField, Min(.001f)] private float throwSwipeMinDistance = .12f;
        [SerializeField, Min(0f)] private float throwSwipeUpDominance = .65f;
        [SerializeField, Min(.001f)] private float throwMaxSwipeSpeed = 6f;
        [SerializeField, Min(.001f)] private float throwMinUpSpeed = 2f;
        [SerializeField, Min(.001f)] private float throwLateralGain = 2.5f;
        [SerializeField, Min(.001f)] private float throwUpGain = 2.1f;
        [SerializeField, Min(.001f)] private float throwForwardGain = 4.5f;
        [SerializeField, Min(.001f)] private float throwMaxWorldSpeed = 18f;
        [SerializeField, Min(.001f)] private float throwGravity = 9.81f;
        [SerializeField, Min(.001f)] private float throwLifetime = 4f;
        [SerializeField, Min(.001f)] private float throwRadius = .165f;
        [SerializeField, Range(.25f, 3f)] private float throwPowerExponent = 1f;
        [SerializeField, Range(-30f, 30f)] private float throwLoftOffsetDegrees;
        [SerializeField, Range(0f, 5f)] private float throwAirDamping;

        [Header("Projectile throw form")]
        [Tooltip("Arc: 기존 포물선. Straight: 카메라는 유지하고 플레이어 기준 출발점·상향각·중력만 바꾼다.")]
        [SerializeField] private LabThrowForm throwForm = LabThrowForm.Arc;
        // DEMO_TUNING_VALUE: tune in DEV and compare with Arc before choosing production values.
        [Tooltip("직구 출발점 (플레이어 오른쪽 X, 월드 위 Y, 타깃 쪽 Z). 기존 Throw Origin과 손을 놓은 가로 위치의 계산 결과에 더한다.")]
        [SerializeField] private Vector3 straightOriginOffset = new Vector3(.35f, .4f, 0f);
        [SerializeField, Range(-30f, 30f)] private float straightPitchOffsetDegrees = 3f;
        [SerializeField, Range(0f, 1f)] private float straightLoftScale = .1f;
        [SerializeField, Range(0f, 2f)] private float straightGravityScale = .1f;

        public LabThrowForm ThrowForm => throwForm == LabThrowForm.Straight ? LabThrowForm.Straight : LabThrowForm.Arc;
        public Vector3 StraightOriginOffset => new Vector3(
            Mathf.Clamp(Finite(straightOriginOffset.x, .35f), -5f, 5f),
            Mathf.Clamp(Finite(straightOriginOffset.y, .4f), -5f, 5f),
            Mathf.Clamp(Finite(straightOriginOffset.z, 0f), -5f, 5f));
        public float StraightPitchOffsetDegrees => Mathf.Clamp(Finite(straightPitchOffsetDegrees, 3f), -30f, 30f);
        public float StraightLoftScale => Mathf.Clamp(Finite(straightLoftScale, .1f), 0f, 1f);
        public float StraightGravityScale => Mathf.Clamp(Finite(straightGravityScale, .1f), 0f, 2f);
        public float EffectiveThrowGravity => ThrowForm == LabThrowForm.Straight ? ThrowGravity * StraightGravityScale : ThrowGravity;

        public int MonsterMaxHp => Mathf.Max(1, monsterMaxHp);
        public int PlayerMaxHp => Mathf.Clamp(playerMaxHp, 1, 300);
        public int Damage => Mathf.Max(1, damage);
        public float BattleDurationSeconds => Positive(battleDurationSeconds, 180f);
        public float TargetSpinStrength => Mathf.Clamp(Finite(targetSpinStrength, 80f), 0f, 200f);
        public float TargetSpinDamping => Mathf.Clamp(Finite(targetSpinDamping, 1.2f), 0f, 10f);
        public float TargetSpinMaximumSpeed => Mathf.Clamp(Finite(targetSpinMaximumSpeed, 720f), 30f, 1440f);
        public float StaminaMax => Positive(staminaMax, 100f);
        public float StaminaStart => Mathf.Clamp(Finite(staminaStart, 100f), 0f, StaminaMax);
        public float GenerateCost => Mathf.Clamp(Positive(generateCost, 20f), .001f, StaminaMax);
        public float StaminaRecoveryAmount => Mathf.Clamp(Finite(staminaRecoveryAmount, 20f), 0f, StaminaMax);
        public float StaminaRecoverySeconds => Positive(staminaRecoverySeconds, 1.7f);
        public float StaminaRecoveryPerSecond => StaminaRecoveryAmount / StaminaRecoverySeconds;
        public float HitRecovery => Mathf.Clamp(Finite(hitRecovery, 10f), 0f, StaminaMax);
        public int OrbStorageLimit => Mathf.Clamp(orbStorageLimit, 1, 100);

        /// <summary>
        /// Percent weights in GeneratedKinds order. An Inspector total of zero falls back to
        /// an even Yin/Yang split so GENERATE always creates something.
        /// </summary>
        public int[] GenerationChances()
        {
            int[] chances =
            {
                Mathf.Clamp(yinChance, 0, 100), Mathf.Clamp(yangChance, 0, 100),
                Mathf.Clamp(relayChance, 0, 100), Mathf.Clamp(secretBoxChance, 0, 100),
                Mathf.Clamp(potionChance, 0, 100), Mathf.Clamp(bombChance, 0, 100)
            };
            int total = 0;
            foreach (int chance in chances) total += chance;
            return total > 0 ? chances : new[] { 50, 50, 0, 0, 0, 0 };
        }

        public float OrbLifetimeSeconds => Positive(orbLifetimeSeconds, 8f);
        public float ItemLifetimeSeconds => Positive(itemLifetimeSeconds, 15f);
        public int ItemUnlockSwipesMin => Mathf.Clamp(Mathf.Min(itemUnlockSwipesMin, itemUnlockSwipesMax), 0, 20);
        public int ItemUnlockSwipesMax => Mathf.Clamp(Mathf.Max(itemUnlockSwipesMin, itemUnlockSwipesMax), 0, 20);
        public int PotionHeal => Mathf.Clamp(potionHeal, 0, 300);
        public float RelayDoubleMultiplier => Mathf.Clamp(Finite(relayDoubleMultiplier, 2f), 1f, 20f);
        public float RelayMaxMultiplier => Mathf.Clamp(Finite(relayMaxMultiplier, 4f), 1f, 20f);
        public float BombMultiplier => Mathf.Clamp(Finite(bombMultiplier, 3f), 1f, 20f);
        public float BombBlastRadius => Mathf.Clamp(Finite(bombBlastRadius, .4f), 0f, 2f);
        public float BombBlastStrength => Mathf.Clamp(Finite(bombBlastStrength, 9f), 0f, 20f);
        public float BombDragLag => Mathf.Clamp(Finite(bombDragLag, .12f), 0f, 1f);
        public float BombReleaseSpeed => Mathf.Clamp(Finite(bombReleaseSpeed, .75f), .05f, 1f);
        public float BombSlideDeceleration => Mathf.Clamp(Finite(bombSlideDeceleration, 1.3f), 1f, 5f);
        public float BombMass => Mathf.Clamp(Finite(bombMass, 4f), 1f, 20f);
        public float BlastBurnSeconds => Mathf.Clamp(Finite(blastBurnSeconds, 4f), 3f, 5f);
        public float BlastShrinkScale => Mathf.Clamp(Finite(blastShrinkScale, .5f), .2f, 1f);
        public float SmallOrbPaintShare => Mathf.Clamp(Finite(smallOrbPaintShare, .5f), 0f, 1f);
        public bool GourdColorComboEnabled => gourdColorComboEnabled;
        public float ComboFirstStartSeconds => Mathf.Clamp(Finite(comboFirstStartSeconds, 50f), 1f, 3600f);
        public float ComboRepeatIntervalSeconds => Mathf.Clamp(Finite(comboRepeatIntervalSeconds, 60f), 1f, 3600f);
        public float ComboTimeLimitSeconds => Mathf.Clamp(Finite(comboTimeLimitSeconds, 15f), 1f, 600f);
        public float ComboPenaltyDurationSeconds => Mathf.Clamp(Finite(comboPenaltyDurationSeconds, 15f), 0f, 30f);
        public float ComboPenaltySizeScale => Mathf.Clamp(Finite(comboPenaltySizeScale, .1f), .01f, 1f);
        public bool ComboPenaltyThrowerOnly => comboPenaltyThrowerOnly;
        public float ComboRewardDurationSeconds => Mathf.Clamp(Finite(comboRewardDurationSeconds, 15f), 0f, 30f);
        public float ComboRewardSizeScale => Mathf.Clamp(Finite(comboRewardSizeScale, 2.5f), 1f, 3f);
        public float ComboWarningSeconds => Mathf.Clamp(Finite(comboWarningSeconds, 3f), 0f, 10f);
        public bool BoardOrbTrailEnabled => boardOrbTrailEnabled;
        public float OrbRestitution => Mathf.Clamp01(Finite(orbRestitution, .8f));
        public float OrbFloorDeceleration => Mathf.Max(0f, Finite(orbFloorDeceleration, 2f));
        public float OrbStopSpeed => Mathf.Clamp(Finite(orbStopSpeed, .065f), 0f, OrbMaxReleaseSpeed);
        public float OrbMaxReleaseSpeed => Positive(orbMaxReleaseSpeed, 3f);
        public float OrbRadius => Positive(orbRadius, .32f);
        public float BoardHeight => Positive(boardHeight, 5f);

        public float CombineSpeed => Mathf.Clamp(Finite(combineSpeed, 0f), 0f, 10f);
        public bool DropCombineEnabled => dropCombineEnabled;
        public bool ContactCombineEnabled => contactCombineEnabled;

        public float SpawnPopStartScale => Mathf.Clamp01(Finite(spawnPopStartScale, .3f));
        public float SpawnPopDuration => Mathf.Clamp(Finite(spawnPopDuration, .15f), 0f, 2f);
        public float SpawnFanAngle => Mathf.Clamp(Finite(spawnFanAngle, 180f), 0f, 180f);
        public float SpawnMinSpeed => Mathf.Clamp(Finite(spawnMinSpeed, 1f), 0f, 10f);
        public float SpawnMaxSpeed => Mathf.Clamp(Finite(spawnMaxSpeed, 1.8f), SpawnMinSpeed, 10f);
        /// <summary>The Drum Count setting: <see cref="AutoDrumCount"/> (0) follows the players, 1 to 5 is fixed.</summary>
        public int DrumCount => Mathf.Clamp(drumCount, 0, 5);
        /// <summary>The Drum Count value that sets the number of drums by the room's player count (#236).</summary>
        public const int AutoDrumCount = 0;
        /// <summary>How many drums AUTO gives a room: one for up to three players, two for four or five.</summary>
        public static int DrumsForPlayers(int players) => players >= 4 ? 2 : 1;
        /// <summary>How many drums a round with this many players starts with.</summary>
        public int DrumsFor(int players) => DrumCount != AutoDrumCount ? DrumCount : DrumsForPlayers(players);
        public float DrumRadius => Mathf.Clamp(Finite(drumRadius, .6f), .1f, 1.5f);
        public float DrumDriftSpeed => Mathf.Clamp(Finite(drumDriftSpeed, .1f), 0f, 1f);
        public float DrumDriftTurning => Mathf.Clamp(Finite(drumDriftTurning, .25f), 0f, 2f);
        public float DrumSpinStrength => Mathf.Clamp(Finite(drumSpinStrength, 1f), 0f, 3f);
        private static readonly int[] DefaultDrumStopBounds = { 20, 40, 60, 80 };
        private static readonly int[] DefaultDrumStopChances = { 40, 35, 15, 10 };

        /// <summary>
        /// Stop zone edges as shares of the board from the entry edge, in order: near from, near to /
        /// middle from, middle to / far from, far to. An Inspector array that is not four increasing
        /// percentages falls back to 20/40/60/80.
        /// </summary>
        public float[] DrumStopBounds()
        {
            int[] bounds = ValidDrumStopBounds(drumStopBounds) ? drumStopBounds : DefaultDrumStopBounds;
            return new[] { bounds[0] / 100f, bounds[1] / 100f, bounds[2] / 100f, bounds[3] / 100f };
        }

        /// <summary>Near, middle, far and pass-through chances (%). An array not adding up to 100 falls back to 40/35/15/10.</summary>
        public int[] DrumStopChances()
        {
            int[] chances = ValidDrumStopChances(drumStopChances) ? drumStopChances : DefaultDrumStopChances;
            return (int[])chances.Clone();
        }

        private static bool ValidDrumStopBounds(int[] bounds)
        {
            if (bounds == null || bounds.Length != 4) return false;
            for (int i = 0; i < 4; i++)
                if (bounds[i] < 0 || bounds[i] > 100 || (i > 0 && bounds[i] < bounds[i - 1])) return false;
            return true;
        }

        private static bool ValidDrumStopChances(int[] chances)
        {
            if (chances == null || chances.Length != 4) return false;
            int total = 0;
            foreach (int chance in chances)
            {
                if (chance < 0 || chance > 100) return false;
                total += chance;
            }
            return total == 100;
        }
        public LabGourdMode GourdMode => gourdMode == LabGourdMode.Crack ? LabGourdMode.Crack : LabGourdMode.Paint;
        public const int DefaultPaintVictoryPercent = 100;
        // Paint victory requires the entire target surface; it is not a DEV tuning value.
        public int PaintVictoryPercent => DefaultPaintVictoryPercent;
        public int PaintSurfaceResolution => Mathf.Clamp(paintSurfaceResolution, 0, 2);
        public float PaintInitialPercent => Mathf.Clamp(Finite(paintInitialPercent, 2f), .1f, 100f);
        public float PaintRepeatPercent => Mathf.Clamp(Finite(paintRepeatPercent, 1.5f), .1f, 100f);
        public float PaintClusterCompactness => Mathf.Clamp01(Finite(paintClusterCompactness, .85f));
        public float PaintClusterIrregularity => Mathf.Clamp01(Finite(paintClusterIrregularity, .25f));
        public float PaintGrowthPercentPerSecond => Mathf.Clamp(Finite(paintGrowthPercentPerSecond, 6f), .1f, 100f);
        public float PaintFillChancePercent => Mathf.Clamp(Finite(paintFillChancePercent, 60f), 0f, 100f);
        public float PaintVerticalWeight => Mathf.Clamp(Finite(paintVerticalWeight, 1.35f), 1f, 4f);
        public bool PaintDryingEnabled => paintDryingEnabled;
        public float PaintDryingDelaySeconds => Mathf.Clamp(Finite(paintDryingDelaySeconds, 5f), 0f, 60f);
        public float PaintDryingPercentPerSecond => Mathf.Clamp(Finite(paintDryingPercentPerSecond, .2f), 0f, 5f);
        public float PaintNearbyHitRadiusDegrees => Mathf.Clamp(Finite(paintNearbyHitRadiusDegrees, 20f), 1f, 90f);
        public LabPaintGrowthSettings PaintGrowthSettings => new LabPaintGrowthSettings
        {
            Resolution = PaintSurfaceResolution,
            InitialPercent = PaintInitialPercent,
            RepeatPercent = PaintRepeatPercent,
            ClusterCompactness = PaintClusterCompactness,
            ClusterIrregularity = PaintClusterIrregularity,
            GrowthPercentPerSecond = PaintGrowthPercentPerSecond,
            FillChancePercent = PaintFillChancePercent,
            VerticalWeight = PaintVerticalWeight,
            Style = PaintGrowthStyle,
            Pattern = PaintPattern,
            Raggedness = PaintEdgeRaggedness,
            DryingEnabled = PaintDryingEnabled,
            DryingDelaySeconds = PaintDryingDelaySeconds,
            DryingPercentPerSecond = PaintDryingPercentPerSecond,
            NearbyHitRadiusDegrees = PaintNearbyHitRadiusDegrees
        };
        public bool CompletedGourdViewerEnabled => completedGourdViewerEnabled;
        public bool PlayerPaintPatternsEnabled => playerPaintPatternsEnabled;
        public float GourdRopeCycleAmplitude => Mathf.Clamp(Finite(gourdRopeCycleAmplitude, .45f), 0f, 1f);
        public float GourdRopeCyclePeriod => Mathf.Clamp(Finite(gourdRopeCyclePeriod, 3f), .5f, 10f);
        public float GourdRopeHitBounce => Mathf.Clamp(Finite(gourdRopeHitBounce, .32f), 0f, 2f);
        public float GourdRopeSpring => Mathf.Clamp(Finite(gourdRopeSpring, 42f), 0f, 100f);
        public float GourdRopeDamping => Mathf.Clamp(Finite(gourdRopeDamping, 2f), 0f, 20f);
        public float GourdRopeMaximumHitExtension => Mathf.Clamp(Finite(gourdRopeMaximumHitExtension, .65f), 0f, 3f);
        public float GourdSwingImpactStrength => Mathf.Clamp(Finite(gourdSwingImpactStrength, 12f), 0f, 20f);
        public float GourdSwingMaximumAngle => Mathf.Clamp(Finite(gourdSwingMaximumAngle, 40f), 1f, 75f);
        public float GourdSwingReturnStrength => Mathf.Clamp(Finite(gourdSwingReturnStrength, 10f), 0f, 100f);
        public float GourdSwingDamping => Mathf.Clamp(Finite(gourdSwingDamping, 1.4f), 0f, 20f);
        public bool PaintGrainEnabled => paintGrainEnabled;
        public float PaintGrainSize => Mathf.Clamp(Finite(paintGrainSize, 1f), .25f, 4f);
        public float PaintGrainStrength => Mathf.Clamp01(Finite(paintGrainStrength, .3f));
        public float PaintMottleStrength => Mathf.Clamp01(Finite(paintMottleStrength, .18f));
        public int PaintGrowthStyle => Mathf.Clamp(paintGrowthStyle, 0, 1);
        public LabPaintPattern PaintPattern => paintPattern >= LabPaintPattern.Legacy && paintPattern <= LabPaintPattern.Flow
            ? paintPattern : LabPaintPattern.Splatter;
        public float PaintEdgeRaggedness => Mathf.Clamp01(Finite(paintEdgeRaggedness, .55f));
        public float GourdBombIntervalMin => Mathf.Clamp(Finite(gourdBombIntervalMin, 20f), 0f, 600f);
        public float GourdBombIntervalMax => Mathf.Clamp(Finite(gourdBombIntervalMax, 30f), GourdBombIntervalMin, 600f);
        public float SwipeBlockInterval => Mathf.Clamp(Finite(swipeBlockInterval, 40f), 0f, 600f);
        public float SwipeBlockSeconds => Mathf.Clamp(Finite(swipeBlockSeconds, 10f), 1f, 60f);
        public float SwipeBlockBombGap => Mathf.Clamp(Finite(swipeBlockBombGap, 4f), 0f, 15f);
        public bool SabotageMixEnabled => sabotageMixEnabled;
        public float SabotageIntervalMin => Mathf.Clamp(Finite(sabotageIntervalMin, 20f), 1f, 600f);
        public float SabotageIntervalMax => Mathf.Clamp(Finite(sabotageIntervalMax, 30f), SabotageIntervalMin, 600f);
        /// <summary>How often the sabotage mix draws each sabotage, in <see cref="LabSabotageKind"/> order (#251).</summary>
        public int[] SabotageWeights() => new[]
        {
            Mathf.Clamp(sabotageWeightGourdBomb, 0, 10), Mathf.Clamp(sabotageWeightSwipeBlock, 0, 10),
            Mathf.Clamp(sabotageWeightFoxFire, 0, 10), Mathf.Clamp(sabotageWeightCharm, 0, 10)
        };
        public int FoxFireCountMin => Mathf.Clamp(foxFireCountMin, 1, 10);
        public int FoxFireCountMax => Mathf.Clamp(foxFireCountMax, FoxFireCountMin, 10);
        public float FoxFireSafeSeconds => Mathf.Clamp(Finite(foxFireSafeSeconds, 1f), 0f, 3f);
        public float CharmSeconds => Mathf.Clamp(Finite(charmSeconds, 10f), 1f, 30f);
        public int CharmRescueTaps => Mathf.Clamp(charmRescueTaps, 0, 10);
        public bool YaguangFlashEnabled => yaguangFlashEnabled;
        /// <summary>true면 섬광이 매번 무작위 한 명에게만, false면 모두에게 동시에 (#268)</summary>
        public bool YaguangFlashTargetOne => yaguangFlashTargetOne;
        public float YaguangFlashFirstStartSeconds => Mathf.Clamp(Finite(yaguangFlashFirstStartSeconds, 31f), 1f, 61f);
        public float YaguangFlashIntervalSeconds => Mathf.Clamp(Finite(yaguangFlashIntervalSeconds, 60f), 20f, 100f);
        public float YaguangFlashWarningSeconds => Mathf.Clamp(Finite(yaguangFlashWarningSeconds, 2f), 0f, 4f);
        public float YaguangFlashGlareSeconds => Mathf.Clamp(Finite(yaguangFlashGlareSeconds, 7f), 1f, 13f);
        public float YaguangFlashRecoverySeconds => Mathf.Clamp(Finite(yaguangFlashRecoverySeconds, 2f), 0f, 4f);
        public LabOrbAppearance OrbAppearance => orbAppearance;
        /// <summary>DEV 소리 크기의 기본값. 이 값이면 Inspector 값을 그대로 쓴다.</summary>
        public const float DevVolumeDefault = 50f;

        // 실제 소리 크기 = Inspector 값 × DEV 배율(50 = 1배). 최대 Inspector 값의 2배.
        public float BgmVolume => Scaled(Finite(bgmVolume, .2f), bgmVolumeDev);
        public float GenerateSoundVolume => Scaled(Finite(generateSoundVolume, 1f), generateSoundVolumeDev);
        public int ChuimsaeChancePercent => Mathf.Clamp(chuimsaeChancePercent, 0, 100);
        public int ChuimsaeMinGap => Mathf.Clamp(chuimsaeMinGap, 0, 20);
        public float HitSoundVolume => Scaled(Finite(hitSoundVolume, .9f), hitSoundVolumeDev);
        public float MissSoundVolume => Scaled(Finite(missSoundVolume, .7f), missSoundVolumeDev);
        public float GlancingHitOffset => Mathf.Clamp01(Finite(glancingHitOffset, .7f));
        public float CombineSoundVolume => Scaled(Finite(combineSoundVolume, .8f), combineSoundVolumeDev);
        public float FuseSoundVolume => Scaled(Finite(fuseSoundVolume, .5f), fuseSoundVolumeDev);
        public float BombSoundVolume => Scaled(Finite(bombSoundVolume, .9f), bombSoundVolumeDev);
        public LabBombSoundStyle BombSoundStyle =>
            bombSoundStyle == LabBombSoundStyle.Boom ? LabBombSoundStyle.Boom : LabBombSoundStyle.Ppeong;
        private static float DevVolume(float value) => Mathf.Clamp(Finite(value, DevVolumeDefault), 0f, 100f);
        private static float Scaled(float inspectorVolume, float devVolume) =>
            Mathf.Clamp01(inspectorVolume) * DevVolume(devVolume) / DevVolumeDefault;
        public bool HapticsEnabled => hapticsEnabled;
        public LabHapticStyle GenerateHapticStyle => ValidStyle(generateHapticStyle);
        public float GenerateHapticIntensity => Mathf.Clamp01(Finite(generateHapticIntensity, 1f));
        public LabHapticStyle HitHapticStyle => ValidStyle(hitHapticStyle);
        public float HitHapticIntensity => Mathf.Clamp01(Finite(hitHapticIntensity, 1f));
        public LabHapticStyle ItemHitHapticStyle => ValidStyle(itemHitHapticStyle);
        public float ItemHitHapticIntensity => Mathf.Clamp01(Finite(itemHitHapticIntensity, 1f));
        public LabHapticStyle BombHapticStyle => ValidStyle(bombHapticStyle);
        public float BombHapticIntensity => Mathf.Clamp01(Finite(bombHapticIntensity, 1f));
        public LabHapticStyle CombineHapticStyle => ValidStyle(combineHapticStyle);
        public float CombineHapticIntensity => Mathf.Clamp01(Finite(combineHapticIntensity, 1f));
        public float CameraRadius => Positive(cameraRadius, 8f);
        public float CameraHeightOffset => Finite(cameraHeightOffset, .65f);
        public float CameraLookHeightOffset => Mathf.Clamp(Finite(cameraLookHeightOffset, .8f), -10f, 10f);
        public float ThrowOriginRadius => Positive(throwOriginRadius, 4f);
        public float ThrowOriginHeightOffset => Finite(throwOriginHeightOffset, -.5f);
        public float ThrowOriginLateralRange => Mathf.Max(0f, Finite(throwOriginLateralRange, 1f));
        public float ThrowSwipeMinSpeed => Positive(throwSwipeMinSpeed, .8f);
        public float ThrowSwipeMinDistance => Positive(throwSwipeMinDistance, .12f);
        public float ThrowSwipeUpDominance => Mathf.Max(0f, Finite(throwSwipeUpDominance, .65f));
        public float ThrowMaxSwipeSpeed => Positive(throwMaxSwipeSpeed, 6f);
        public float ThrowMinUpSpeed => Positive(throwMinUpSpeed, 2f);
        public float ThrowLateralGain => Positive(throwLateralGain, 2.5f);
        public float ThrowUpGain => Positive(throwUpGain, 2.1f);
        public float ThrowForwardGain => Positive(throwForwardGain, 4.5f);
        public float ThrowMaxWorldSpeed => Positive(throwMaxWorldSpeed, 18f);
        public float ThrowGravity => Positive(throwGravity, 9.81f);
        public float ThrowLifetime => Positive(throwLifetime, 4f);
        public float ThrowRadius => Positive(throwRadius, .165f);
        public float ThrowPowerExponent => Mathf.Clamp(throwPowerExponent <= 0f
            ? 1f : Finite(throwPowerExponent, 1f), .25f, 3f);
        public float ThrowLoftOffsetDegrees => Mathf.Clamp(Finite(throwLoftOffsetDegrees, 0f), -30f, 30f);
        public float ThrowAirDamping => Mathf.Clamp(Finite(throwAirDamping, 0f), 0f, 5f);

        /// <summary>Returns effective, Inspector-clamped values in DeveloperFields order.</summary>
        public float[] CaptureDeveloperValues()
        {
            int[] chances = GenerationChances();
            float[] stopBounds = DrumStopBounds();
            int[] stopChances = DrumStopChances();
            int[] sabotageWeights = SabotageWeights();
            return new[]
            {
                (float)MonsterMaxHp, (float)Damage, BattleDurationSeconds,
                StaminaMax, StaminaStart, GenerateCost, StaminaRecoveryAmount,
                StaminaRecoverySeconds, HitRecovery, (float)OrbStorageLimit,
                OrbLifetimeSeconds, OrbRestitution, OrbFloorDeceleration, OrbStopSpeed,
                OrbMaxReleaseSpeed, OrbRadius, BoardHeight, SpawnPopStartScale,
                SpawnPopDuration, CameraRadius, CameraHeightOffset, ThrowOriginRadius,
                ThrowOriginHeightOffset, ThrowOriginLateralRange, ThrowSwipeMinSpeed,
                ThrowSwipeMinDistance, ThrowSwipeUpDominance, ThrowMaxSwipeSpeed,
                ThrowMinUpSpeed, ThrowLateralGain, ThrowUpGain, ThrowForwardGain,
                ThrowMaxWorldSpeed, ThrowGravity, ThrowLifetime, ThrowRadius,
                ThrowPowerExponent, ThrowLoftOffsetDegrees, ThrowAirDamping,
                chances[0], chances[1], chances[2], chances[3], chances[4], chances[5],
                ItemLifetimeSeconds, ItemUnlockSwipesMin, ItemUnlockSwipesMax,
                PlayerMaxHp,
                CombineSpeed, DropCombineEnabled ? 1f : 0f, ContactCombineEnabled ? 1f : 0f,
                PotionHeal, RelayDoubleMultiplier, RelayMaxMultiplier, BombMultiplier,
                CameraLookHeightOffset, TargetSpinStrength, TargetSpinDamping,
                TargetSpinMaximumSpeed,
                SpawnFanAngle, SpawnMinSpeed, SpawnMaxSpeed,
                DevVolume(bgmVolumeDev), DevVolume(generateSoundVolumeDev),
                DevVolume(hitSoundVolumeDev), DevVolume(missSoundVolumeDev),
                HapticsEnabled ? 1f : 0f,
                (float)GenerateHapticStyle, GenerateHapticIntensity,
                (float)HitHapticStyle, HitHapticIntensity,
                (float)ItemHitHapticStyle, ItemHitHapticIntensity,
                DrumCount, DrumRadius, DrumDriftSpeed, DrumDriftTurning, DrumSpinStrength,
                (float)GourdMode,
                BombBlastRadius, BombBlastStrength,
                (float)BombHapticStyle, BombHapticIntensity,
                DevVolume(combineSoundVolumeDev), (float)CombineHapticStyle, CombineHapticIntensity,
                GourdBombIntervalMin, GourdBombIntervalMax,
                DevVolume(fuseSoundVolumeDev),
                Mathf.Round(stopBounds[0] * 100f), Mathf.Round(stopBounds[1] * 100f),
                Mathf.Round(stopBounds[2] * 100f), Mathf.Round(stopBounds[3] * 100f),
                stopChances[0], stopChances[1], stopChances[2], stopChances[3],
                MinimapEnabled ? 1f : 0f, MinimapSizePercent, MinimapMove.x, MinimapMove.y,
                DevVolume(bombSoundVolumeDev),
                (float)BombSoundStyle,
                (float)PaintSurfaceResolution, PaintInitialPercent, PaintRepeatPercent,
                PaintGrowthPercentPerSecond, PaintFillChancePercent, PaintVerticalWeight,
                CompletedGourdViewerEnabled ? 1f : 0f,
                (float)ThrowForm, StraightOriginOffset.x, StraightOriginOffset.y, StraightOriginOffset.z,
                StraightPitchOffsetDegrees, StraightLoftScale, StraightGravityScale,
                GourdRopeCycleAmplitude, GourdRopeCyclePeriod, GourdRopeHitBounce,
                GourdRopeSpring, GourdRopeDamping, GourdRopeMaximumHitExtension,
                GourdSwingImpactStrength, GourdSwingMaximumAngle,
                GourdSwingReturnStrength, GourdSwingDamping,
                PaintGrainEnabled ? 1f : 0f, PaintGrainSize, PaintGrainStrength, PaintMottleStrength,
                (float)PaintGrowthStyle, PaintEdgeRaggedness, (float)PaintPattern,
                (float)ChuimsaeChancePercent, (float)ChuimsaeMinGap,
                PlayerPaintPatternsEnabled ? 1f : 0f,
                PaintClusterCompactness, PaintClusterIrregularity, GourdVisualSize,
                BombDragLag, BombReleaseSpeed, BombSlideDeceleration, BombMass,
                BlastBurnSeconds, BlastShrinkScale, SmallOrbPaintShare,
                PaintDryingEnabled ? 1f : 0f, PaintDryingDelaySeconds,
                PaintDryingPercentPerSecond, PaintNearbyHitRadiusDegrees,
                SwipeBlockInterval, SwipeBlockSeconds, SwipeBlockBombGap,
                GourdColorComboEnabled ? 1f : 0f, ComboFirstStartSeconds, ComboRepeatIntervalSeconds,
                ComboTimeLimitSeconds, ComboPenaltyDurationSeconds, ComboPenaltySizeScale,
                ComboPenaltyThrowerOnly ? 1f : 0f, ComboRewardDurationSeconds, ComboRewardSizeScale,
                ComboWarningSeconds,
                BoardOrbTrailEnabled ? 1f : 0f,
                SabotageMixEnabled ? 1f : 0f, SabotageIntervalMin, SabotageIntervalMax,
                sabotageWeights[0], sabotageWeights[1], sabotageWeights[2], sabotageWeights[3],
                FoxFireCountMin, FoxFireCountMax, FoxFireSafeSeconds, CharmSeconds, CharmRescueTaps,
                YaguangFlashEnabled ? 1f : 0f, YaguangFlashTargetOne ? 1f : 0f, YaguangFlashFirstStartSeconds,
                YaguangFlashIntervalSeconds, YaguangFlashWarningSeconds, YaguangFlashGlareSeconds,
                YaguangFlashRecoverySeconds
            };
        }

        /// <summary>Validates the complete form before modifying any gameplay value.</summary>
        public bool TryApplyDeveloperValues(float[] values, out string error)
        {
            if (values == null || values.Length != DeveloperFields.Count)
            {
                error = "The developer settings form is incomplete.";
                return false;
            }

            for (int i = 0; i < values.Length; i++)
            {
                LabDeveloperField field = DeveloperFields[i];
                float value = values[i];
                if (float.IsNaN(value) || float.IsInfinity(value) || value < field.Min || value > field.Max)
                {
                    error = field.Label + " must be a finite number from " + field.Min + " to " + field.Max + ".";
                    return false;
                }
                if (field.WholeNumber && value != Mathf.Round(value))
                {
                    error = field.Label + " must be a whole number.";
                    return false;
                }
            }

            if (values[4] > values[3]) { error = "Starting Stamina cannot exceed Maximum Stamina."; return false; }
            if (values[5] > values[3]) { error = "Orb Generation Cost cannot exceed Maximum Stamina."; return false; }
            if (values[6] > values[3]) { error = "Stamina Recovery Amount cannot exceed Maximum Stamina."; return false; }
            if (values[8] > values[3]) { error = "Stamina Reward per Hit cannot exceed Maximum Stamina."; return false; }
            if (values[13] > values[14]) { error = "Orb Stop Speed cannot exceed Orb Maximum Release Speed."; return false; }
            if (values[24] > values[27]) { error = "Throw Minimum Swipe Speed cannot exceed Throw Maximum Swipe Speed."; return false; }
            if (values[28] > values[32]) { error = "Throw Minimum Upward Speed cannot exceed Throw Maximum World Speed."; return false; }
            float chanceTotal = values[39] + values[40] + values[41] + values[42] + values[43] + values[44];
            if (chanceTotal != 100f) { error = "Generation chances must add up to 100 (now " + chanceTotal + ")."; return false; }
            if (values[46] > values[47]) { error = "Item Unlock Swipes Min cannot exceed Item Unlock Swipes Max."; return false; }
            if (values[61] > values[62]) { error = "Spawn Minimum Speed cannot exceed Spawn Maximum Speed."; return false; }
            if (values[87] > values[88]) { error = "Gourd Bomb Interval Min cannot exceed Gourd Bomb Interval Max."; return false; }
            if (values[90] > values[91] || values[91] > values[92] || values[92] > values[93])
            { error = "Drum stop zone edges must go from near to far (from <= to)."; return false; }
            float drumStopTotal = values[94] + values[95] + values[96] + values[97];
            if (drumStopTotal != 100f) { error = "Drum stop chances must add up to 100 (now " + drumStopTotal + ")."; return false; }
            if (values[167] > values[168]) { error = "Sabotage Interval Min cannot exceed Sabotage Interval Max."; return false; }
            if (values[173] > values[174]) { error = "Fox Fire Count Min cannot exceed Fox Fire Count Max."; return false; }

            monsterMaxHp = (int)values[0];
            damage = (int)values[1];
            battleDurationSeconds = values[2];
            staminaMax = values[3];
            staminaStart = values[4];
            generateCost = values[5];
            staminaRecoveryAmount = values[6];
            staminaRecoverySeconds = values[7];
            hitRecovery = values[8];
            orbStorageLimit = (int)values[9];
            orbLifetimeSeconds = values[10];
            orbRestitution = values[11];
            orbFloorDeceleration = values[12];
            orbStopSpeed = values[13];
            orbMaxReleaseSpeed = values[14];
            orbRadius = values[15];
            boardHeight = values[16];
            spawnPopStartScale = values[17];
            spawnPopDuration = values[18];
            cameraRadius = values[19];
            cameraHeightOffset = values[20];
            throwOriginRadius = values[21];
            throwOriginHeightOffset = values[22];
            throwOriginLateralRange = values[23];
            throwSwipeMinSpeed = values[24];
            throwSwipeMinDistance = values[25];
            throwSwipeUpDominance = values[26];
            throwMaxSwipeSpeed = values[27];
            throwMinUpSpeed = values[28];
            throwLateralGain = values[29];
            throwUpGain = values[30];
            throwForwardGain = values[31];
            throwMaxWorldSpeed = values[32];
            throwGravity = values[33];
            throwLifetime = values[34];
            throwRadius = values[35];
            throwPowerExponent = values[36];
            throwLoftOffsetDegrees = values[37];
            throwAirDamping = values[38];
            yinChance = (int)values[39];
            yangChance = (int)values[40];
            relayChance = (int)values[41];
            secretBoxChance = (int)values[42];
            potionChance = (int)values[43];
            bombChance = (int)values[44];
            itemLifetimeSeconds = values[45];
            itemUnlockSwipesMin = (int)values[46];
            itemUnlockSwipesMax = (int)values[47];
            playerMaxHp = (int)values[48];
            combineSpeed = values[49];
            dropCombineEnabled = values[50] >= .5f;
            contactCombineEnabled = values[51] >= .5f;
            potionHeal = (int)values[52];
            relayDoubleMultiplier = values[53];
            relayMaxMultiplier = values[54];
            bombMultiplier = values[55];
            cameraLookHeightOffset = values[56];
            targetSpinStrength = values[57];
            targetSpinDamping = values[58];
            targetSpinMaximumSpeed = values[59];
            spawnFanAngle = values[60];
            spawnMinSpeed = values[61];
            spawnMaxSpeed = values[62];
            bgmVolumeDev = values[63];
            generateSoundVolumeDev = values[64];
            hitSoundVolumeDev = values[65];
            missSoundVolumeDev = values[66];
            hapticsEnabled = values[67] >= .5f;
            generateHapticStyle = (LabHapticStyle)(int)values[68];
            generateHapticIntensity = values[69];
            hitHapticStyle = (LabHapticStyle)(int)values[70];
            hitHapticIntensity = values[71];
            itemHitHapticStyle = (LabHapticStyle)(int)values[72];
            itemHitHapticIntensity = values[73];
            drumCount = (int)values[74];
            drumRadius = values[75];
            drumDriftSpeed = values[76];
            drumDriftTurning = values[77];
            drumSpinStrength = values[78];
            gourdMode = values[79] >= .5f ? LabGourdMode.Crack : LabGourdMode.Paint;
            bombBlastRadius = values[80];
            bombBlastStrength = values[81];
            bombHapticStyle = (LabHapticStyle)(int)values[82];
            bombHapticIntensity = values[83];
            combineSoundVolumeDev = values[84];
            combineHapticStyle = (LabHapticStyle)(int)values[85];
            combineHapticIntensity = values[86];
            gourdBombIntervalMin = values[87];
            gourdBombIntervalMax = values[88];
            fuseSoundVolumeDev = values[89];
            drumStopBounds = new[] { (int)values[90], (int)values[91], (int)values[92], (int)values[93] };
            drumStopChances = new[] { (int)values[94], (int)values[95], (int)values[96], (int)values[97] };
            minimapEnabled = values[98] >= .5f;
            minimapSizePercent = (int)values[99];
            minimapMoveX = (int)values[100];
            minimapMoveY = (int)values[101];
            bombSoundVolumeDev = values[102];
            bombSoundStyle = values[103] >= .5f ? LabBombSoundStyle.Ppeong : LabBombSoundStyle.Boom;
            paintSurfaceResolution = (int)values[104];
            paintInitialPercent = values[105];
            paintRepeatPercent = values[106];
            paintGrowthPercentPerSecond = values[107];
            paintFillChancePercent = values[108];
            paintVerticalWeight = values[109];
            completedGourdViewerEnabled = values[110] >= .5f;
            throwForm = values[111] >= .5f ? LabThrowForm.Straight : LabThrowForm.Arc;
            straightOriginOffset = new Vector3(values[112], values[113], values[114]);
            straightPitchOffsetDegrees = values[115];
            straightLoftScale = values[116];
            straightGravityScale = values[117];
            gourdRopeCycleAmplitude = values[118];
            gourdRopeCyclePeriod = values[119];
            gourdRopeHitBounce = values[120];
            gourdRopeSpring = values[121];
            gourdRopeDamping = values[122];
            gourdRopeMaximumHitExtension = values[123];
            gourdSwingImpactStrength = values[124];
            gourdSwingMaximumAngle = values[125];
            gourdSwingReturnStrength = values[126];
            gourdSwingDamping = values[127];
            paintGrainEnabled = values[128] >= .5f;
            paintGrainSize = values[129];
            paintGrainStrength = values[130];
            paintMottleStrength = values[131];
            paintGrowthStyle = (int)values[132];
            paintEdgeRaggedness = values[133];
            paintPattern = (LabPaintPattern)(int)values[134];
            chuimsaeChancePercent = (int)values[135];
            chuimsaeMinGap = (int)values[136];
            playerPaintPatternsEnabled = values[137] == 1f;
            paintClusterCompactness = values[138];
            paintClusterIrregularity = values[139];
            gourdVisualSize = values[140];
            bombDragLag = values[141];
            bombReleaseSpeed = values[142];
            bombSlideDeceleration = values[143];
            bombMass = values[144];
            blastBurnSeconds = values[145];
            blastShrinkScale = values[146];
            smallOrbPaintShare = values[147];
            paintDryingEnabled = values[148] == 1f;
            paintDryingDelaySeconds = values[149];
            paintDryingPercentPerSecond = values[150];
            paintNearbyHitRadiusDegrees = values[151];
            swipeBlockInterval = values[152];
            swipeBlockSeconds = values[153];
            swipeBlockBombGap = values[154];
            gourdColorComboEnabled = values[155] >= .5f;
            comboFirstStartSeconds = values[156];
            comboRepeatIntervalSeconds = values[157];
            comboTimeLimitSeconds = values[158];
            comboPenaltyDurationSeconds = values[159];
            comboPenaltySizeScale = values[160];
            comboPenaltyThrowerOnly = values[161] >= .5f;
            comboRewardDurationSeconds = values[162];
            comboRewardSizeScale = values[163];
            comboWarningSeconds = values[164];
            boardOrbTrailEnabled = values[165] >= .5f;
            sabotageMixEnabled = values[166] >= .5f;
            sabotageIntervalMin = values[167];
            sabotageIntervalMax = values[168];
            sabotageWeightGourdBomb = (int)values[169];
            sabotageWeightSwipeBlock = (int)values[170];
            sabotageWeightFoxFire = (int)values[171];
            sabotageWeightCharm = (int)values[172];
            foxFireCountMin = (int)values[173];
            foxFireCountMax = (int)values[174];
            foxFireSafeSeconds = values[175];
            charmSeconds = values[176];
            charmRescueTaps = (int)values[177];
            yaguangFlashEnabled = values[178] >= .5f;
            yaguangFlashTargetOne = values[179] >= .5f;
            yaguangFlashFirstStartSeconds = values[180];
            yaguangFlashIntervalSeconds = values[181];
            yaguangFlashWarningSeconds = values[182];
            yaguangFlashGlareSeconds = values[183];
            yaguangFlashRecoverySeconds = values[184];
            error = string.Empty;
            return true;
        }

        private static float Finite(float value, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        private static LabHapticStyle ValidStyle(LabHapticStyle style) =>
            style >= LabHapticStyle.Light && style <= LabHapticStyle.Rigid ? style : LabHapticStyle.Light;
        private static float Positive(float value, float fallback) => Mathf.Max(.001f, Finite(value, fallback));
    }
}
