using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>#734 남은 30초 경고음 종류 (LabConfig에서 들어 보고 고른다).</summary>
    public enum LabThirtySecondsLeftSound { Gong = 0, Drum = 1 }

    /// <summary>
    /// 남은 30초 알림 (#734). 스토리 1/2/3라운드(구미호/야광귀/지귀)에서 남은 시간이 30초가 되는 순간 모든 기기 화면에
    /// - 위쪽 남은 시간 숫자가 크게 부풀었다 돌아오며 빨갛게 몇 번 깜빡이고,
    /// - 그 바로 밑에서 "30초 남았소!" 말풍선이 튀어나와 흔들리다가 잠시 머물고 흐려지며,
    /// - 화면 가장자리가 붉게 번쩍인 뒤 말풍선이 떠 있는 동안 붉은 테두리로 숨 쉬며 남고, 강한 경고음과 강한 진동이 한 번 난다.
    /// 튜토리얼 연습판과 2:2 대결에서는 띄우지 않는다. 남은 시간(스냅샷)만 보고 판단해서 라운드 기믹 코드와 따로 돈다.
    /// 30초가 지난 뒤(LateWindowSeconds 넘게) 들어온 기기에는 띄우지 않는다. 화면 표시만 하며 터치를 막지 않는다.
    /// 알림 시각, 말풍선 시간, 경고음(징/북)과 크기, 진동 세기는 LabConfig Inspector "남은 30초 알림 (#734)".
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabThirtySecondsLeftHud : MonoBehaviour
    {
        public const string WordsEnd = "초 남았소!";
        public const string GongFolder = "ThirtySecondsLeftGong", DrumFolder = "ThirtySecondsLeftDrum";
        /// <summary>알림 시각을 넘은 지 이 초 안에 본 기기만 띄운다.</summary>
        public const double LateWindowSeconds = 1.5d;
        public static readonly Color AlarmRed = new Color(.86f, .12f, .16f);
        private static readonly Color PaperColor = new Color(1f, .97f, .9f, .97f);
        private static readonly Color InkColor = new Color32(43, 36, 32, 255);
        // 시간 숫자: 이만큼 부풀고, 이 시간 동안 이 횟수만큼 빨갛게 깜빡인다.
        private const float TimerSwell = .55f, TimerSwellSeconds = .45f, TimerBlinkSeconds = 1.2f;
        private const int TimerBlinks = 3;
        // 가장자리: 처음에 이만큼 밝게 번쩍이고(EdgeSeconds에 걸쳐 빠짐), 말풍선이 떠 있는 동안 EdgeHold 언저리로 숨 쉬며 남는다.
        private const float EdgeSeconds = .6f, EdgeAlpha = .6f, EdgeHold = .38f, EdgeBreathe = .1f, EdgeHz = 2f;
        // 말풍선 크기 (Canvas 단위)
        private const float BubbleWidth = 176f, BubbleHeight = 46f, BubbleGap = 12f;
        private const int WordsSize = 26;

        private LabNetwork network;
        private Canvas canvas;
        private Text timer;
        private Color timerColor;
        private Vector3 timerScale = Vector3.one;
        private bool timerTouched;
        private RectTransform layer, bubble, tail;
        private Image back, tailImage, edge;
        private Text words;
        private double lastRemaining = double.NaN;
        private float age = float.PositiveInfinity;
        private AudioSource sfx;
        private static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>알림을 띄운 횟수 (테스트·확인용)</summary>
        public int ShowCount { get; private set; }
        /// <summary>지금 말풍선이 화면에 있는지</summary>
        public bool Showing => bubble != null && bubble.gameObject.activeInHierarchy;
        /// <summary>말풍선 글</summary>
        public string Shown => words != null ? words.text : string.Empty;

        /// <summary>스냅샷을 읽을 네트워크, 그릴 Canvas, 부풀리고 깜빡일 남은 시간 글자를 연결한다.</summary>
        public void Bind(LabNetwork target, Canvas uiCanvas, Text time)
        {
            RestoreTimer();
            network = target;
            canvas = uiCanvas != null ? uiCanvas.rootCanvas : null;
            timer = time;
        }

        /// <summary>"30초 남았소!" (알림 시각을 바꾸면 그 초로)</summary>
        public static string WordsFor(float at) => Mathf.RoundToInt(at) + WordsEnd;

        /// <summary>이 판에서 띄우는지: 스토리 라운드(스테이지 요괴가 있음)만, 튜토리얼과 2:2는 빼고.</summary>
        public static bool AppliesTo(bool gameFlow, bool tutorial, bool teamTerritory, LabStageYokai stage)
            => gameFlow && !tutorial && !teamTerritory && stage != LabStageYokai.None;

        /// <summary>
        /// 지난번에 본 남은 시간(previous, 처음이면 NaN)에서 지금(remaining)으로 오며 알림 시각(at)을 막 넘었는지.
        /// 넘은 지 LateWindowSeconds가 지났으면(늦게 들어온 기기) false.
        /// </summary>
        public static bool Crossed(double previous, double remaining, float at)
            => (double.IsNaN(previous) || previous > at) && remaining <= at && remaining > 0d && at - remaining < LateWindowSeconds;

        private void LateUpdate()
        {
            LabSnapshot snapshot = network != null ? network.Snapshot : null;
            bool playing = snapshot != null && snapshot.phase == LabPhase.Playing;
            if (!playing)
            {
                lastRemaining = double.NaN;
                if (age < float.PositiveInfinity) Hide();
                return;
            }
            bool on = !snapshot.paused && AppliesTo(network.GameFlowEnabled, network.IsTutorial, snapshot.teamTerritory, network.GameStageYokai);
            if (on)
            {
                float at = network.config != null ? network.config.ThirtySecondsLeftAt : 30f;
                if (Crossed(lastRemaining, snapshot.remaining, at)) Begin(at);
                lastRemaining = snapshot.remaining;
            }
            if (age < float.PositiveInfinity) Step(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// VFX 실험실(#734): 남은 시간과 상관없이 지금 한 번 띄운다 (화면/소리/진동만). 실제 30초 알림은 그대로 따로 뜬다.
        /// </summary>
        public bool PreviewNow()
        {
            Begin(network != null && network.config != null ? network.config.ThirtySecondsLeftAt : 30f);
            return layer != null;
        }

        /// <summary>VFX 실험실(#734): 고른 경고음만 지금 한 번 들려준다 (말풍선 없이, LabConfig 크기로).</summary>
        public void PreviewSound(LabThirtySecondsLeftSound sound)
            => PlaySound(FolderFor(sound), network != null && network.config != null ? network.config.ThirtySecondsLeftVolume : 1f);

        private void OnDisable() => Hide();

        private void OnDestroy()
        {
            RestoreTimer();
            if (layer != null) Destroy(layer.gameObject);
        }

        private void Begin(float at)
        {
            if (!EnsureLayer()) return;
            ShowCount++;
            age = 0f;
            words.text = WordsFor(at);
            layer.gameObject.SetActive(true);
            layer.SetAsLastSibling();
            if (timer != null && !timerTouched)
            {
                timerColor = timer.color;
                timerScale = timer.rectTransform.localScale;
                timerTouched = true;
            }
            LabConfig config = network != null ? network.config : null;
            PlaySound(FolderFor(config != null ? config.ThirtySecondsLeftSound : LabThirtySecondsLeftSound.Gong),
                config != null ? config.ThirtySecondsLeftVolume : 1f);
            float haptic = config != null ? config.ThirtySecondsLeftHaptic : 1f;
            if (haptic > 0f) LabHaptics.PlayDirect(config, LabHapticStyle.Heavy, haptic, .3f, .8f * haptic, .9f);
        }

        private void Hide()
        {
            age = float.PositiveInfinity;
            RestoreTimer();
            if (layer != null) layer.gameObject.SetActive(false);
        }

        private void RestoreTimer()
        {
            if (timer != null && timerTouched)
            {
                timer.color = timerColor;
                timer.rectTransform.localScale = timerScale;
            }
            timerTouched = false;
        }

        private void Step(float deltaTime)
        {
            age += deltaTime;
            float seconds = network != null && network.config != null ? network.config.ThirtySecondsLeftBubbleSeconds : 2.2f;
            // 시간 숫자: 확 부풀었다 돌아오고, 빨강/원래 색을 번갈아 깜빡인 뒤 원래대로.
            if (timer != null && timerTouched)
            {
                float swell = age < TimerSwellSeconds ? Mathf.Sin(Mathf.PI * age / TimerSwellSeconds) : 0f;
                timer.rectTransform.localScale = timerScale * (1f + TimerSwell * swell);
                bool red = age < TimerBlinkSeconds && Mathf.FloorToInt(age / TimerBlinkSeconds * TimerBlinks * 2f) % 2 == 0;
                timer.color = red ? new Color(AlarmRed.r, AlarmRed.g, AlarmRed.b, timerColor.a) : timerColor;
            }
            // 말풍선: 시간 숫자 밑에서 튀어나와 좌우로 흔들리다 자리를 잡고, 끝에 흐려진다.
            float alpha = BubbleAlpha(age, seconds);
            // 가장자리: 처음에 붉게 번쩍이고, 말풍선이 떠 있는 동안 붉은 테두리로 숨 쉬며 남다가 말풍선과 함께 흐려진다.
            float edgeAlpha = EdgeAlphaAt(age, alpha, Time.unscaledTime);
            edge.gameObject.SetActive(edgeAlpha > 0f);
            if (edgeAlpha > 0f) edge.color = new Color(AlarmRed.r, AlarmRed.g, AlarmRed.b, edgeAlpha);
            bubble.gameObject.SetActive(alpha > 0f);
            tail.gameObject.SetActive(alpha > 0f);
            if (alpha > 0f)
            {
                Place(BubbleShake(age));
                float scale = BubbleScale(age);
                bubble.localScale = tail.localScale = Vector3.one * scale;
                back.color = new Color(PaperColor.r, PaperColor.g, PaperColor.b, PaperColor.a * alpha);
                tailImage.color = back.color;
                words.color = new Color(AlarmRed.r, AlarmRed.g, AlarmRed.b, alpha);
            }
            if (alpha <= 0f && age >= TimerBlinkSeconds && edgeAlpha <= 0f) Hide();
        }

        /// <summary>
        /// 시작한 지 t초의 붉은 테두리 불투명도: 처음 번쩍임과, 말풍선이 떠 있는 동안(bubbleAlpha) 숨 쉬며 남는 테두리 중 큰 쪽.
        /// </summary>
        public static float EdgeAlphaAt(float t, float bubbleAlpha, float time)
        {
            float flash = t >= 0f && t < EdgeSeconds ? EdgeAlpha * Mathf.Pow(1f - t / EdgeSeconds, 2f) : 0f;
            float hold = bubbleAlpha * (EdgeHold + EdgeBreathe * Mathf.Sin(time * EdgeHz * 2f * Mathf.PI));
            return Mathf.Max(flash, hold);
        }

        /// <summary>시작한 지 t초의 말풍선 크기: 작게 시작해 1.3배까지 튀었다가 1배로 돌아온다.</summary>
        public static float BubbleScale(float t)
        {
            if (t < .16f) return Mathf.Lerp(.2f, 1.3f, Mathf.Max(0f, t) / .16f);
            if (t < .32f) return Mathf.Lerp(1.3f, 1f, (t - .16f) / .16f);
            return 1f + .03f * Mathf.Sin((t - .32f) * 10f);
        }

        /// <summary>시작한 지 t초의 말풍선 좌우 흔들림 (Canvas 단위): 처음 0.5초 동안 덜덜 떨다 멈춘다.</summary>
        public static float BubbleShake(float t) => t < .5f ? 7f * (1f - t / .5f) * Mathf.Sin(t * 70f) : 0f;

        /// <summary>시작한 지 t초의 말풍선 불투명도: seconds초 동안 보이고 마지막 0.4초에 흐려진다.</summary>
        public static float BubbleAlpha(float t, float seconds)
        {
            if (t < 0f || t >= seconds) return 0f;
            return Mathf.Clamp01((seconds - t) / .4f);
        }

        // 말풍선을 시간 숫자 바로 밑에 둔다 (왼쪽 끝을 숫자 틀에 맞추고 화면 안으로). 꼬리는 숫자 가운데를 가리킨다.
        private void Place(float shake)
        {
            Rect area = layer.rect;
            Vector2 anchor = new Vector2(area.xMin + 60f, area.yMax - 60f);
            float timerBottom = anchor.y, timerCenterX = anchor.x;
            if (timer != null)
            {
                timer.rectTransform.GetWorldCorners(Corners);
                Vector2 min = layer.InverseTransformPoint(Corners[0]), max = layer.InverseTransformPoint(Corners[2]);
                // 부풀기 전 크기로 잰다.
                float grow = timer.rectTransform.localScale.y / Mathf.Max(.01f, timerScale.y);
                float half = (max.y - min.y) * .5f / Mathf.Max(.01f, grow);
                float centerY = (min.y + max.y) * .5f;
                timerBottom = centerY - half;
                timerCenterX = (min.x + max.x) * .5f;
                anchor.x = timerCenterX - ((max.x - min.x) * .5f / Mathf.Max(.01f, grow));
            }
            float x = Mathf.Clamp(anchor.x + BubbleWidth * .5f - 8f, area.xMin + BubbleWidth * .5f + 6f, area.xMax - BubbleWidth * .5f - 6f);
            float y = timerBottom - BubbleGap - BubbleHeight * .5f;
            bubble.anchoredPosition = new Vector2(x + shake, y);
            tail.anchoredPosition = new Vector2(Mathf.Clamp(timerCenterX, x - BubbleWidth * .4f, x + BubbleWidth * .4f) + shake,
                y + BubbleHeight * .5f);
        }

        /// <summary>경고음 종류의 Resources/Audio 폴더 이름.</summary>
        public static string FolderFor(LabThirtySecondsLeftSound sound) => sound == LabThirtySecondsLeftSound.Drum ? DrumFolder : GongFolder;

        private void PlaySound(string folder, float volume)
        {
            if (volume <= 0f) return;
            AudioClip[] clips = Resources.LoadAll<AudioClip>("Audio/" + folder);
            if (clips == null || clips.Length == 0) return;
            if (sfx == null) { sfx = gameObject.AddComponent<AudioSource>(); sfx.playOnAwake = false; sfx.spatialBlend = 0f; }
            sfx.PlayOneShot(clips[Random.Range(0, clips.Length)], volume * LabAudioSettings.SfxVolume);
        }

        private bool EnsureLayer()
        {
            if (layer != null) return true;
            if (canvas == null)
            {
                Canvas found = GetComponentInParent<Canvas>();
                canvas = found != null ? found.rootCanvas : null;
            }
            RectTransform canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
            if (canvasRect == null) return false;
            layer = NewRect("Thirty Seconds Left", canvasRect);
            layer.anchorMin = Vector2.zero; layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            edge = NewRect("Thirty Seconds Left Edge", layer).gameObject.AddComponent<Image>();
            edge.rectTransform.anchorMin = Vector2.zero; edge.rectTransform.anchorMax = Vector2.one;
            edge.rectTransform.offsetMin = edge.rectTransform.offsetMax = Vector2.zero;
            edge.sprite = LabTeamFever.GlowSprite();
            edge.raycastTarget = false;
            edge.gameObject.SetActive(false);
            tail = NewRect("Thirty Seconds Left Tail", layer);
            tail.sizeDelta = new Vector2(16f, 16f);
            tail.localEulerAngles = new Vector3(0f, 0f, 45f);
            tailImage = tail.gameObject.AddComponent<Image>();
            tailImage.raycastTarget = false;
            tail.gameObject.SetActive(false);
            bubble = NewRect("Thirty Seconds Left Bubble", layer);
            bubble.sizeDelta = new Vector2(BubbleWidth, BubbleHeight);
            back = bubble.gameObject.AddComponent<Image>();
            back.sprite = LabGourdComboHud.RoundedSprite();
            back.type = Image.Type.Sliced;
            back.raycastTarget = false;
            Outline frame = bubble.gameObject.AddComponent<Outline>();
            frame.effectColor = new Color(AlarmRed.r, AlarmRed.g, AlarmRed.b, .9f);
            frame.effectDistance = new Vector2(3f, -3f);
            RectTransform text = NewRect("Words", bubble);
            text.anchorMin = Vector2.zero; text.anchorMax = Vector2.one;
            text.offsetMin = text.offsetMax = Vector2.zero;
            words = text.gameObject.AddComponent<Text>();
            words.font = timer != null && timer.font != null ? timer.font : LabGuideText.FontOf(canvas);
            words.fontSize = WordsSize;
            words.fontStyle = FontStyle.Bold;
            words.alignment = TextAnchor.MiddleCenter;
            words.horizontalOverflow = HorizontalWrapMode.Overflow;
            words.verticalOverflow = VerticalWrapMode.Overflow;
            words.raycastTarget = false;
            Outline ink = text.gameObject.AddComponent<Outline>();
            ink.effectColor = new Color(InkColor.r, InkColor.g, InkColor.b, .35f);
            ink.effectDistance = new Vector2(1f, -1f);
            bubble.gameObject.SetActive(false);
            return true;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            return rect;
        }
    }
}
