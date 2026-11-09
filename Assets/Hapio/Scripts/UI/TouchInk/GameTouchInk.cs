using System;
using System.Collections.Generic;
using C6Lab;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>한 번의 터치에서 먹 방울 하나의 모양.</summary>
    public readonly struct GameTouchInkDroplet
    {
        public readonly int Picture;
        public readonly float Size, Rotation;
        public readonly Vector2 Offset;

        public GameTouchInkDroplet(int picture, float size, float rotation, Vector2 offset)
        {
            Picture = picture;
            Size = size;
            Rotation = rotation;
            Offset = offset;
        }
    }

    /// <summary>한 번의 터치에서 나올 먹물 번짐의 모양. 터치마다 새로 정한다.</summary>
    public sealed class GameTouchInkPlan
    {
        public int Stain;
        public float StainSize, StainRotation;
        public GameTouchInkDroplet[] Droplets = Array.Empty<GameTouchInkDroplet>();
    }

    /// <summary>
    /// 전투 화면 밖에서 터치할 때마다 그 자리에 희미한 먹물이 번졌다 사라진다 (#587).
    /// - 전투 Scene(튜토리얼·일시정지 포함)에서는 새로 찍지 않는다.
    /// - 터치를 가로채지 않는다: 이 캔버스에는 GraphicRaycaster가 없고 그림은 터치를 받지 않는다. 입력은 장치에서 직접 읽는다.
    /// - 이 기기 화면에만 보이고 네트워크로 보내지 않는다.
    /// - `Tap Sound`를 넣으면 터치할 때 소리가 한 번 난다(지금은 비어 있어 먹물만 나온다). Home 등에는 AudioListener가 일부러 없어서
    ///   (TitleRevealSound와 같은 이유), 소리가 나는 동안만 잠깐 붙인다.
    /// - 메뉴·오프닝 위, 장면 전환 페이드와 로딩 화면 아래에 그린다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameTouchInk : MonoBehaviour
    {
        /// <summary>Over every menu, the opening and the DEV button; under the scene fade (30100) and the loading screen (30150).</summary>
        public const int SortingOrder = 30050;

        private sealed class Mark
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public RawImage Stain;
            public readonly List<RawImage> Droplets = new List<RawImage>();
            public float StartedAt;
        }

        private readonly List<Mark> live = new List<Mark>();
        private readonly List<Mark> spare = new List<Mark>();
        private GameTouchInkArt art;
        private System.Random random;
        private RectTransform root;
        private AudioSource source;
        private AudioListener borrowed;
        private float listenUntil = -1f;

        /// <summary>How many ink marks are on the screen.</summary>
        public int ActiveMarks => live.Count;
        /// <summary>How many times the tap sound has played.</summary>
        public int SoundsPlayed { get; private set; }
        /// <summary>How strong the newest mark is right now (0 when there is none).</summary>
        public float NewestMarkAlpha => live.Count > 0 ? live[live.Count - 1].Group.alpha : 0f;

        public static GameTouchInk Create(Transform parent, GameTouchInkArt art = null, System.Random random = null)
        {
            art = art != null ? art : GameTouchInkArt.Load();
            if (art == null)
            {
                Debug.LogWarning("HAPIO_TOUCH_INK_ART_MISSING: Resources/" + GameTouchInkArt.ResourcePath);
                return null;
            }
            var go = new GameObject("GameTouchInk", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            if (parent != null) go.transform.SetParent(parent, false);
            var ink = go.AddComponent<GameTouchInk>();
            ink.Build(art, random ?? new System.Random());
            return ink;
        }

        private void Build(GameTouchInkArt inkArt, System.Random rng)
        {
            art = inkArt;
            random = rng;
            root = (RectTransform)transform;
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390f, 844f);
            scaler.matchWidthOrHeight = .5f;
        }

        private void OnDisable() => ReturnListener();

        private void Update()
        {
            float now = Time.unscaledTime;
            if (borrowed != null && (now >= listenUntil || AnotherListener())) ReturnListener();
            for (int i = live.Count - 1; i >= 0; i--)
            {
                float age = now - live[i].StartedAt;
                if (age >= art.fadeSeconds) Retire(i);
                else Present(live[i], age);
            }
            ReadTouches();
        }

        /// <summary>
        /// Every finger that came down this frame leaves a mark, with one sound however many fingers it was;
        /// the mouse counts only when no finger did.
        /// </summary>
        public void ReadTouches()
        {
            if (!ShowsOn(SceneManager.GetActiveScene().path)) return;
            bool touched = false;
            Touchscreen screen = Touchscreen.current;
            if (screen != null)
                foreach (TouchControl touch in screen.touches)
                {
                    if (!touch.press.wasPressedThisFrame) continue;
                    touched = true;
                    Stamp(touch.position.ReadValue());
                }
            Mouse mouse = Mouse.current;
            if (!touched && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                touched = true;
                Stamp(mouse.position.ReadValue());
            }
            if (touched) PlayTapSound();
        }

        private void PlayTapSound()
        {
            if (art.tapSound == null || art.soundVolume <= 0f) return;
            SoundsPlayed++;
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
            }
            if (borrowed == null && !AnotherListener()) borrowed = gameObject.AddComponent<AudioListener>();
            if (borrowed != null) listenUntil = Time.unscaledTime + art.tapSound.length + .1f;
            source.PlayOneShot(art.tapSound, art.soundVolume * LabAudioSettings.SfxVolume);
        }

        private bool AnotherListener()
        {
            foreach (AudioListener listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (listener != borrowed && listener.isActiveAndEnabled) return true;
            return false;
        }

        private void ReturnListener()
        {
            if (borrowed != null) Destroy(borrowed);
            borrowed = null;
            listenUntil = -1f;
        }

        /// <summary>Puts a freshly rolled mark at this screen point, at its strongest.</summary>
        public void Stamp(Vector2 screenPoint)
        {
            if (art.stains == null || art.stains.Length == 0) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPoint, null, out Vector2 local)) return;
            GameTouchInkPlan plan = RollMark(art, random);
            Mark mark = NextMark();
            mark.Root.anchoredPosition = local;
            mark.Root.SetAsLastSibling();
            Paint(mark.Stain, art.stains[plan.Stain], plan.StainSize / art.stainFill, plan.StainRotation, Vector2.zero);
            while (mark.Droplets.Count < plan.Droplets.Length) mark.Droplets.Add(NewPicture("Droplet", mark.Root));
            for (int i = 0; i < mark.Droplets.Count; i++)
            {
                bool used = i < plan.Droplets.Length;
                mark.Droplets[i].gameObject.SetActive(used);
                if (!used) continue;
                GameTouchInkDroplet droplet = plan.Droplets[i];
                Paint(mark.Droplets[i], art.droplets[droplet.Picture], droplet.Size / art.dropletFill, droplet.Rotation, droplet.Offset);
            }
            mark.StartedAt = Time.unscaledTime;
            mark.Root.gameObject.SetActive(true);
            Present(mark, 0f);
        }

        private Mark NextMark()
        {
            int limit = Mathf.Max(1, art.maxMarks);
            while (live.Count >= limit) Retire(0);
            Mark mark;
            if (spare.Count > 0)
            {
                mark = spare[spare.Count - 1];
                spare.RemoveAt(spare.Count - 1);
            }
            else mark = NewMark();
            live.Add(mark);
            return mark;
        }

        private void Retire(int index)
        {
            Mark mark = live[index];
            live.RemoveAt(index);
            mark.Root.gameObject.SetActive(false);
            spare.Add(mark);
        }

        private Mark NewMark()
        {
            var go = new GameObject("Ink Mark", typeof(RectTransform), typeof(CanvasGroup));
            var rect = (RectTransform)go.transform;
            rect.SetParent(root, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = Vector2.zero;
            var group = go.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            return new Mark { Root = rect, Group = group, Stain = NewPicture("Stain", rect) };
        }

        private static RawImage NewPicture(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            var picture = go.GetComponent<RawImage>();
            picture.raycastTarget = false;
            return picture;
        }

        private void Paint(RawImage picture, Texture2D texture, float size, float rotation, Vector2 offset)
        {
            picture.texture = texture;
            picture.color = new Color(art.inkColor.r, art.inkColor.g, art.inkColor.b, 1f);
            RectTransform rect = picture.rectTransform;
            float aspect = texture != null ? texture.width / (float)Mathf.Max(1, texture.height) : 1f;
            rect.sizeDelta = aspect >= 1f ? new Vector2(size, size / aspect) : new Vector2(size * aspect, size);
            rect.anchoredPosition = offset;
            rect.localEulerAngles = new Vector3(0f, 0f, rotation);
        }

        private void Present(Mark mark, float age)
        {
            mark.Group.alpha = MarkAlpha(age, art.fadeSeconds, art.inkStrength);
            mark.Root.localScale = Vector3.one * MarkScale(age, art.fadeSeconds, art.startScale, art.endScale);
        }

        // ---------- Pure rules (tested) ----------

        /// <summary>Everywhere but the battle screen, which also holds the tutorial and the pause menu.</summary>
        public static bool ShowsOn(string activeScenePath) => activeScenePath != GameScenePaths.Battle;

        /// <summary>At its strongest the moment it lands, then fading at once, with no time held.</summary>
        public static float MarkAlpha(float age, float seconds, float strength) =>
            strength * (1f - Mathf.Clamp01(age / Mathf.Max(.01f, seconds)));

        /// <summary>Spreads quickly at first and slows, like ink soaking into paper.</summary>
        public static float MarkScale(float age, float seconds, float start, float end)
        {
            float left = 1f - Mathf.Clamp01(age / Mathf.Max(.01f, seconds));
            return Mathf.Lerp(start, end, 1f - left * left);
        }

        /// <summary>A new shape for one touch: which stain, how turned and how big, and the droplets around it.</summary>
        public static GameTouchInkPlan RollMark(GameTouchInkArt art, System.Random random)
        {
            float vary = 1f + Between(random, -art.stainSizeVariation, art.stainSizeVariation);
            var plan = new GameTouchInkPlan
            {
                Stain = random.Next(art.stains.Length),
                StainSize = art.stainSize * vary,
                StainRotation = Between(random, 0f, 360f)
            };
            if (art.droplets == null || art.droplets.Length == 0) return plan;
            int fewest = Mathf.Max(0, art.dropletsMin);
            int count = random.Next(fewest, Mathf.Max(fewest, art.dropletsMax) + 1);
            plan.Droplets = new GameTouchInkDroplet[count];
            for (int i = 0; i < count; i++)
            {
                float angle = Between(random, 0f, Mathf.PI * 2f);
                float distance = Between(random, art.dropletDistanceMin, art.dropletDistanceMax);
                plan.Droplets[i] = new GameTouchInkDroplet(random.Next(art.droplets.Length),
                    Between(random, art.dropletSizeMin, art.dropletSizeMax), Between(random, 0f, 360f),
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance);
            }
            return plan;
        }

        private static float Between(System.Random random, float low, float high) =>
            Mathf.Lerp(low, Mathf.Max(low, high), (float)random.NextDouble());
    }
}
