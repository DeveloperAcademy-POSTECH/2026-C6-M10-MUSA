using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using C6Lab;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>
    /// 튜토리얼 대사 키워드 강조 (#727). 대본(GameTutorialScript)의 대사에 아래 표시를 넣으면 말풍선과 대화 기록에서 그 부분만 다르게 보인다.
    /// - &lt;big&gt;…&lt;/big&gt;: 글자 색은 그대로, 크기만 그 말풍선 글자 크기의 GameTutorialArt.emphasisScale배 (기본 1.35). 말풍선이 넘쳐 글자가
    ///   작아지면 큰 글자도 같은 비율로 작아진다(FitBubble). 대화 기록에서는 크기를 키우지 않는다.
    /// - &lt;red&gt;…&lt;/red&gt;: 진홍(GameTutorialArt.emphasisRed, 기본 #8A1206, "경기 종료!"와 같은 색).
    /// - &lt;fire&gt; &lt;water&gt; &lt;wood&gt; &lt;metal&gt; &lt;earth&gt;: 전투 화면 오행 구슬 그림의 색(OrbTextColor, 시안과 같음). 테두리 없이 글자만
    ///   (시안). GameTutorialArt.elementOutlineWidth를 0보다 크게 하면 이 다섯 색 글자에만 먹색 테두리를 두를 수 있다.
    /// 강조한 부분(크게·빨강·오행 색)은 모두 굵게(&lt;b&gt;) 그린다. 대화 기록에서도 굵게 보인다(크기만 키우지 않는다).
    /// 대사의 Text(대화 기록 항목·검사가 보는 글)는 표시를 뺀 원래 문장이다. 화면에 그릴 때만 Unity 리치 텍스트로 바꾼다.
    /// </summary>
    public static class GameTutorialEmphasis
    {
        public const string Big = "big", Red = "red";
        private static readonly Regex Tag = new Regex("</?(big|red|fire|water|wood|metal|earth)>", RegexOptions.Compiled);
        private static readonly Dictionary<string, LabOrbElement> Elements = new Dictionary<string, LabOrbElement>
        {
            { "fire", LabOrbElement.Fire }, { "water", LabOrbElement.Water }, { "wood", LabOrbElement.Wood },
            { "metal", LabOrbElement.Metal }, { "earth", LabOrbElement.Earth }
        };

        /// <summary>표시를 뺀 원래 문장.</summary>
        public static string Strip(string marked) => string.IsNullOrEmpty(marked) ? marked ?? string.Empty : Tag.Replace(marked, string.Empty);

        /// <summary>표시가 하나라도 있는지.</summary>
        public static bool HasMarks(string marked) => !string.IsNullOrEmpty(marked) && Tag.IsMatch(marked);

        /// <summary>
        /// 화면에 그릴 리치 텍스트. bigSize가 0보다 크면 &lt;big&gt;을 그 크기로, 0이면 크기 없이(대화 기록) 바꾼다. bold면 강조한 부분을
        /// 모두 굵게 한다. 대본에 원래 있던 '&lt;' 같은 글자는 없다고 본다(대본은 이 표시만 쓴다).
        /// </summary>
        public static string ToRichText(string marked, int bigSize, Color red, bool bold = true)
        {
            if (string.IsNullOrEmpty(marked)) return marked ?? string.Empty;
            string redHex = "#" + ColorUtility.ToHtmlStringRGB(red);
            var built = new StringBuilder(marked.Length + 32);
            int at = 0;
            foreach (Match match in Tag.Matches(marked))
            {
                built.Append(marked, at, match.Index - at);
                at = match.Index + match.Length;
                bool closing = match.Value[1] == '/';
                string name = match.Groups[1].Value;
                if (closing && bold) built.Append("</b>");
                if (name == Big)
                {
                    if (bigSize > 0) built.Append(closing ? "</size>" : "<size=" + bigSize + ">");
                }
                else if (closing) built.Append("</color>");
                else built.Append("<color=").Append(name == Red ? redHex : ElementHex(Elements[name])).Append('>');
                if (!closing && bold) built.Append("<b>");
            }
            built.Append(marked, at, marked.Length - at);
            return built.ToString();
        }

        /// <summary>
        /// 오행 글자 색: 전투 화면 오행 구슬 그림(Hapio/Art/UI/Battle의 구슬, BattleOrbAppearance)의 가운데 색. 사용자 시안의 글자 색과 같다.
        /// 박에 칠해지는 색(LabPaintPalette)보다 조금 진하다.
        /// </summary>
        public static Color32 OrbTextColor(LabOrbElement element)
        {
            switch (element)
            {
                case LabOrbElement.Fire: return new Color32(0xD3, 0x2E, 0x16, 0xFF);
                case LabOrbElement.Water: return new Color32(0x0E, 0x61, 0x92, 0xFF);
                case LabOrbElement.Wood: return new Color32(0x2E, 0x69, 0x46, 0xFF);
                case LabOrbElement.Metal: return new Color32(0xF9, 0xB5, 0x20, 0xFF);
                case LabOrbElement.Earth: return new Color32(0x65, 0x40, 0x29, 0xFF);
                default: return new Color32(0, 0, 0, 0xFF);
            }
        }

        /// <summary>그 오행 글자 색 (#RRGGBB).</summary>
        public static string ElementHex(LabOrbElement element) => "#" + ColorUtility.ToHtmlStringRGB(OrbTextColor(element));

        /// <summary>정점 색이 다섯 오행 색 중 하나인지 (테두리를 두를 글자인지).</summary>
        public static bool IsElementColor(Color32 color)
        {
            foreach (LabOrbElement element in Elements.Values)
            {
                Color32 wanted = OrbTextColor(element);
                if (Mathf.Abs(color.r - wanted.r) <= 2 && Mathf.Abs(color.g - wanted.g) <= 2
                    && Mathf.Abs(color.b - wanted.b) <= 2) return true;
            }
            return false;
        }

        /// <summary>
        /// 말풍선 글자 크기를 정한다: maxSize부터 하나씩 줄이며, 큰 글자(그 크기의 bigScale배)까지 넣은 글이 글자 칸 높이에 들어가고 대본에 쓴
        /// 줄바꿈 말고는 줄이 더 나뉘지 않는 가장 큰 크기. 둘 다 맞는 크기가 없으면 높이만 맞는 가장 큰 크기, 그것도 없으면 minSize.
        /// Unity의 자동 맞춤(best fit)은 &lt;size&gt; 글자를 같이 줄이지 않아 넘치므로 끄고 직접 맞춘다. 정한 크기와 리치 텍스트를 글자에 넣고
        /// 그 크기를 돌려준다.
        /// </summary>
        public static int FitBubble(Text text, string marked, int maxSize, int minSize, float bigScale, Color red, bool bold = true)
        {
            if (text == null) return 0;
            maxSize = Mathf.Max(1, maxSize);
            minSize = Mathf.Clamp(minSize, 1, maxSize);
            text.supportRichText = true;
            text.resizeTextForBestFit = false;
            Rect box = text.rectTransform.rect;
            int written = string.IsNullOrEmpty(marked) ? 1 : marked.Split('\n').Length;
            int chosen = -1, fitsHeight = -1;
            for (int size = maxSize; size >= minSize; size--)
            {
                string rich = ToRichText(marked, BigSize(size, bigScale), red, bold);
                if (Measure(text, rich, size, box.width, out int lines) > box.height + .5f) continue;
                if (fitsHeight < 0) fitsHeight = size;
                if (lines <= written) { chosen = size; break; }
            }
            if (chosen < 0) chosen = fitsHeight >= 0 ? fitsHeight : minSize;
            text.fontSize = chosen;
            text.text = ToRichText(marked, BigSize(chosen, bigScale), red, bold);
            return chosen;
        }

        /// <summary>그 글자 크기일 때 큰 글자의 크기.</summary>
        public static int BigSize(int size, float bigScale) => Mathf.Max(size, Mathf.RoundToInt(size * Mathf.Max(1f, bigScale)));

        // The same measure as Text.preferredHeight, at a chosen size and without best fit, with the lines it wraps into.
        private static float Measure(Text text, string rich, int size, float width, out int lines)
        {
            TextGenerationSettings settings = text.GetGenerationSettings(new Vector2(width, 0f));
            settings.fontSize = size;
            settings.resizeTextForBestFit = false;
            settings.richText = true;
            settings.horizontalOverflow = HorizontalWrapMode.Wrap;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            float pixels = text.pixelsPerUnit > 0f ? text.pixelsPerUnit : 1f;
            TextGenerator generator = text.cachedTextGeneratorForLayout;
            float height = generator.GetPreferredHeight(rich, settings) / pixels;
            lines = generator.lineCount;
            return height;
        }
    }
}
