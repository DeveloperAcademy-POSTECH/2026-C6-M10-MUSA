using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// Canvas-only A/B feedback. The Host's actual fire collision plane supplies every B anchor;
    /// these passive graphics cannot consume a tap, award a hit or extinguish a fire themselves.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class LabJigwiScreenPresentation : MonoBehaviour
    {
        public const string PrefabResource = "Jigwi/ScreenOverlay";
        public LabNetwork source;
        public Canvas canvas;
        public Camera battleCamera;
        public Font font;
        public UnityEngine.UI.Image personalHeat;
        public UnityEngine.UI.RawImage[] personalFlames;
        public RectTransform fireLayer;
        public UnityEngine.UI.RectMask2D fireClip;
        public RectTransform groupFireCurtain;
        [Tooltip("Invisible stencil mask for the dense portion of the same fire artwork.")]
        public LabJigwiFlameGraphic curtainBody;
        public UnityEngine.UI.Mask curtainArtMask;
        public UnityEngine.UI.RawImage curtainArt, curtainCore;
        [Tooltip("B stage 3: the original strong curtain. Also used if a lower-stage artwork is missing.")]
        public Texture2D curtainTexture;
        [Tooltip("B stage 2: dedicated artwork shown while two ignition points remain.")]
        public Texture2D curtainStage2Texture;
        [Tooltip("B stage 1: dedicated artwork shown while the last ignition point remains.")]
        public Texture2D curtainStage1Texture;
        // Kept as a one-entry alias for saved diagnostics; B no longer draws repeated flame stamps.
        public UnityEngine.UI.RawImage[] curtainFlames;
        public LabJigwiFlameGraphic[] ignitionMarkers;
        public UnityEngine.UI.RawImage curtainWaterDouse;
        public LabJigwiWaterTrailGraphic waterFlightTrail;
        public LabJigwiGroupFireAudio audioView;
        public UnityEngine.UI.RawImage[] fireImages, fireWaterDouses;
        public UnityEngine.UI.RawImage personalWaterDouse;
        public UnityEngine.UI.Image statusPanel;
        public UnityEngine.UI.Text status, playerStatus;
        public UnityEngine.UI.Image failureOverlay;
        public UnityEngine.UI.RawImage[] failureFlames;
        public UnityEngine.UI.Text failureStatus;
        [SerializeField, Range(.1f, 2f)] private float waterDouseSeconds = .6f;
        [SerializeField, Range(0f, 8f)] private float flameSpeed = 2.4f;
        [SerializeField, Range(0f, .1f)] private float flameSway = .03f;
        [SerializeField] private Rect curtainDenseFrame = new Rect(.055f, .025f, .89f, .76f);
        [SerializeField] private Rect curtainDenseUv = new Rect(.12f, .035f, .76f, .67f);
        private const int MaxFires = 6;
        private readonly Dictionary<UnityEngine.UI.RawImage, Material> flameMaterials =
            new Dictionary<UnityEngine.UI.RawImage, Material>();
        private readonly HashSet<string> seenDouses = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> seenFailures = new HashSet<string>(StringComparer.Ordinal);
        private const double FailureFlashSeconds = 1d;
        private double failedAt = -1d;
        private readonly StringBuilder rows = new StringBuilder(180);
        private readonly Vector2[] lastFirePositions = new Vector2[MaxFires], lastFireSizes = new Vector2[MaxFires];
        private readonly bool[] hasFirePosition = new bool[MaxFires];
        private readonly double[] fireDousedAt = { -1d, -1d, -1d, -1d, -1d, -1d };
        private Texture2D flameTexture, waterTexture;
        private bool artReady, missingShaderReported, baselineReady;
        private string sessionId;
        private uint roundNumber;
        /// <summary>A clock step back smaller than this is jitter, not a new stage (#451).</summary>
        public const double RewindToleranceSeconds = .075d;
        private double stageStartedAt = -1d, previousNow = -1d, personalDousedAt = -1d, curtainDousedAt = -1d;
        private Vector2 curtainSplashCentre, curtainSplashSize;
        private RectTransform hpBoundaryRoot;
        private RectTransform[] hpBoundaries = Array.Empty<RectTransform>();
        private readonly Vector3[] clipCorners = new Vector3[4];
        private bool statusLayoutCached, groupStatusMoved;
        private PanelLayout originalStatusLayout;
        private struct PanelLayout
        {
            public Vector2 min, max, pivot, position, size;
            public PanelLayout(RectTransform value)
            { min = value.anchorMin; max = value.anchorMax; pivot = value.pivot; position = value.anchoredPosition; size = value.sizeDelta; }
            public void Restore(RectTransform value)
            { value.anchorMin = min; value.anchorMax = max; value.pivot = pivot; value.sizeDelta = size; value.anchoredPosition = position; }
        }
        public Rect BattleFireClipRect { get; private set; }
        public int VisibleScreenFires { get; private set; }
        /// <summary>#509: B shows no curtain or pillar any more; kept for callers that ask.</summary>
        public bool CurtainVisible => false;
        public int CurtainVisualLevel { get; private set; }
        public bool CurtainWaterVisible => curtainWaterDouse != null && curtainWaterDouse.gameObject.activeSelf;
        public bool GroupCurtainVisible => CurtainVisible;
        public int RemainingIgnitions { get; private set; }
        // Diagnostic severity only. Dedicated artwork replaces stage-dependent image alpha/height.
        public float CurtainStrength => !CurtainVisible ? 0f : CurtainVisualLevel == 3 ? 1f : CurtainVisualLevel == 2 ? .84f : .65f;
        public bool GroupWaterVisible => CurtainWaterVisible;
        public bool PersonalHeatVisible => personalHeat != null && personalHeat.gameObject.activeSelf;
        public bool PersonalWaterVisible => personalWaterDouse != null && personalWaterDouse.gameObject.activeSelf;
        public double PresentationTime { get; private set; }
        public bool FailureVisible => failureOverlay != null && failureOverlay.gameObject.activeSelf;
        public int FailureFlashCount { get; private set; }

        public static LabJigwiScreenPresentation EnsureFor(LabNetwork network, Canvas targetCanvas, Camera camera)
        {
            if (targetCanvas == null) return null;
            Canvas rootCanvas = targetCanvas.rootCanvas;
            var view = rootCanvas.GetComponentInChildren<LabJigwiScreenPresentation>(true);
            if (view == null)
            {
                GameObject prefab = Resources.Load<GameObject>(PrefabResource);
                GameObject go = prefab != null ? Instantiate(prefab, rootCanvas.transform, false)
                    : new GameObject("JigwiScreenOverlay", typeof(RectTransform), typeof(CanvasGroup));
                if (prefab == null) go.transform.SetParent(rootCanvas.transform, false);
                view = go.GetComponent<LabJigwiScreenPresentation>() ?? go.AddComponent<LabJigwiScreenPresentation>();
            }
            view.Bind(network, rootCanvas, camera);
            return view;
        }

        public void Bind(LabNetwork network, Canvas targetCanvas, Camera camera)
        {
            if (source != network) ResetHistory();
            source = network;
            canvas = targetCanvas != null ? targetCanvas.rootCanvas : null;
            battleCamera = camera;
            hpBoundaryRoot = null; hpBoundaries = Array.Empty<RectTransform>();
            EnsureWidgets();
            if (!statusLayoutCached && statusPanel != null)
            { originalStatusLayout = new PanelLayout(statusPanel.rectTransform); statusLayoutCached = true; }
            if (canvas != null)
            {
                foreach (var label in canvas.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                    if (label.font != null && label.font.HasCharacter('불')) { SetFont(label.font); break; }
                LabHud hud = canvas.GetComponentInChildren<LabHud>(true);
                CacheHpBoundary(hud);
                Transform pause = hud != null && hud.pauseMenu != null ? hud.pauseMenu.transform : null;
                while (pause != null && pause.parent != canvas.transform) pause = pause.parent;
                if (pause != null && transform.parent == canvas.transform)
                    transform.SetSiblingIndex(Math.Max(0, pause.GetSiblingIndex()
                        - (transform.GetSiblingIndex() < pause.GetSiblingIndex() ? 1 : 0)));
            }
            EnsureTalisman();
            EnsureSyncPillar();
        }

        /// <summary>#509: the shared 0/3 drops and the "now" cue (no pillar). Created at runtime.</summary>
        public LabJigwiSyncPillarView SyncPillar { get; private set; }
        private void EnsureSyncPillar()
        {
            if (fireLayer == null) return;
            if (SyncPillar == null)
            {
                SyncPillar = gameObject.AddComponent<LabJigwiSyncPillarView>();
                SyncPillar.Build(fireLayer, font);
            }
            Font shilla = canvas != null ? LabYaguangFeverHud.ShillaFont(canvas) : null;
            SyncPillar.SetFont(shilla != null ? shilla : font);
        }

        /// <summary>#479: the talisman that covers this player's board during A. Created at runtime, never saved in the prefab.</summary>
        public LabJigwiTalismanOverlay Talisman { get; private set; }
        private void EnsureTalisman()
        {
            if (!Application.isPlaying) return;
            if (Talisman == null)
            {
                var go = new GameObject("Jigwi Talisman (#479)", typeof(RectTransform));
                go.layer = gameObject.layer;
                var rect = (RectTransform)go.transform; rect.SetParent(transform, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                Talisman = go.AddComponent<LabJigwiTalismanOverlay>();
            }
            // #479: the talisman is written in the battle screen's Shilla Culture font (신라문화체).
            Font shilla = canvas != null ? LabYaguangFeverHud.ShillaFont(canvas) : null;
            Talisman.Bind(source, shilla != null ? shilla : font);
            // #551: the B 3-2-1 (seat circles, big numbers and "지금!") on every screen.
            if (Countdown == null)
            {
                var go = new GameObject("Jigwi B Countdown (#551)", typeof(RectTransform));
                go.layer = gameObject.layer;
                var rect = (RectTransform)go.transform; rect.SetParent(transform, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                Countdown = go.AddComponent<LabJigwiBCountdownView>();
            }
            Countdown.Bind(source, shilla != null ? shilla : font);
            // #551 나무 물통: my handle and rope on my board.
            if (BucketHandle == null)
            {
                var go = new GameObject("Jigwi B Bucket Handle (#551)", typeof(RectTransform));
                go.layer = gameObject.layer;
                var rect = (RectTransform)go.transform; rect.SetParent(transform, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                BucketHandle = go.AddComponent<LabJigwiBucketHandleView>();
            }
            BucketHandle.Bind(source, shilla != null ? shilla : font);
        }

        /// <summary>#551 나무 물통: my handle. Created at runtime with the talisman.</summary>
        public LabJigwiBucketHandleView BucketHandle { get; private set; }

        /// <summary>#551: the B 3-2-1. Created at runtime with the talisman.</summary>
        public LabJigwiBCountdownView Countdown { get; private set; }

        /// <summary>Used by the dedicated prefab builder; never rebuilds the existing battle Canvas.</summary>
        public void EnsureWidgets()
        {
            var rect = (RectTransform)transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            CanvasGroup group = GetComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            if (personalHeat == null)
            {
                personalHeat = Image("PersonalHeat", transform, new Color(1f, .21f, .025f, .055f));
                Stretch(personalHeat.rectTransform, Vector2.zero, Vector2.one);
                personalHeat.gameObject.SetActive(false);
            }
            if (personalFlames == null || personalFlames.Length != 2) Array.Resize(ref personalFlames, 2);
            for (int i = 0; i < personalFlames.Length; i++)
                if (personalFlames[i] == null)
                {
                    personalFlames[i] = Raw("PersonalEdgeFlame" + (i + 1), personalHeat.transform);
                    Stretch(personalFlames[i].rectTransform, new Vector2(i == 0 ? -.035f : .865f, .14f),
                        new Vector2(i == 0 ? .135f : 1.035f, .68f));
                    if (i == 1) personalFlames[i].uvRect = new Rect(1f, 0f, -1f, 1f);
                }
            if (fireLayer == null)
            {
                fireLayer = new GameObject("GroupFireLayer", typeof(RectTransform)).GetComponent<RectTransform>();
                fireLayer.SetParent(transform, false);
                Stretch(fireLayer, Vector2.zero, Vector2.one);
            }
            EnsureCurtainWidgets();
            if (fireImages == null || fireImages.Length != MaxFires) Array.Resize(ref fireImages, MaxFires);
            if (fireWaterDouses == null || fireWaterDouses.Length != MaxFires) Array.Resize(ref fireWaterDouses, MaxFires);
            for (int i = 0; i < MaxFires; i++)
            {
                if (fireImages[i] == null)
                {
                    fireImages[i] = Raw("ScreenFire" + (i + 1), fireLayer);
                    Centre(fireImages[i].rectTransform); fireImages[i].gameObject.SetActive(false);
                }
                if (fireWaterDouses[i] == null)
                {
                    fireWaterDouses[i] = Raw("FireWaterDouse" + (i + 1), fireLayer);
                    Centre(fireWaterDouses[i].rectTransform); fireWaterDouses[i].gameObject.SetActive(false);
                }
            }
            if (ignitionMarkers == null || ignitionMarkers.Length != MaxFires) Array.Resize(ref ignitionMarkers, MaxFires);
            for (int i = 0; i < MaxFires; i++)
                if (ignitionMarkers[i] == null)
                {
                    var marker = new GameObject("IgnitionRing" + (i + 1), typeof(RectTransform), typeof(LabJigwiFlameGraphic));
                    marker.transform.SetParent(fireImages[i].transform, false);
                    ignitionMarkers[i] = marker.GetComponent<LabJigwiFlameGraphic>();
                    ignitionMarkers[i].Configure(LabJigwiFlameGraphic.Shape.IgnitionMarker);
                    Stretch((RectTransform)marker.transform, Vector2.zero, Vector2.one);
                }
            if (curtainWaterDouse == null)
            {
                curtainWaterDouse = Raw("CurtainWaterDouse", fireLayer);
                Centre(curtainWaterDouse.rectTransform); curtainWaterDouse.gameObject.SetActive(false);
            }
            if (waterFlightTrail == null)
            {
                var trail = new GameObject("WaterFlightTrail", typeof(RectTransform), typeof(LabJigwiWaterTrailGraphic));
                trail.transform.SetParent(fireLayer, false);
                waterFlightTrail = trail.GetComponent<LabJigwiWaterTrailGraphic>();
                Stretch(waterFlightTrail.rectTransform, Vector2.zero, Vector2.one);
            }
            waterFlightTrail.raycastTarget = false;
            waterFlightTrail.transform.SetAsLastSibling();
            if (audioView == null)
            {
                var sound = new GameObject("GroupFireAudio", typeof(LabJigwiGroupFireAudio));
                sound.transform.SetParent(transform, false);
                audioView = sound.GetComponent<LabJigwiGroupFireAudio>();
            }
            audioView.EnsureSource();
            if (personalWaterDouse == null)
            {
                personalWaterDouse = Raw("PersonalWaterDouse", transform);
                Centre(personalWaterDouse.rectTransform);
                personalWaterDouse.rectTransform.anchorMin = personalWaterDouse.rectTransform.anchorMax = new Vector2(.5f, .55f);
                personalWaterDouse.gameObject.SetActive(false);
            }
            if (statusPanel == null)
            {
                statusPanel = Image("PatternStatus", transform, new Color(.08f, .025f, .008f, .68f));
                Stretch(statusPanel.rectTransform, new Vector2(.07f, .67f), new Vector2(.93f, .81f));
                statusPanel.gameObject.SetActive(false);
            }
            if (status == null)
            {
                status = Label("PatternInstruction", statusPanel.transform, 19);
                Stretch(status.rectTransform, new Vector2(.035f, .4f), new Vector2(.965f, .98f));
            }
            if (playerStatus == null)
            {
                playerStatus = Label("PlayerFireStatus", statusPanel.transform, 14);
                Stretch(playerStatus.rectTransform, new Vector2(.035f, .02f), new Vector2(.965f, .4f));
            }
            EnsureFailureWidgets();
            EnsureArt();
        }

        private void EnsureFailureWidgets()
        {
            if (failureOverlay == null)
            {
                failureOverlay = Image("GroupMissionFailure", transform, new Color(1f, .13f, .01f, .22f));
                Stretch(failureOverlay.rectTransform, Vector2.zero, Vector2.one);
                failureOverlay.gameObject.SetActive(false);
            }
            if (failureFlames == null || failureFlames.Length != 2) Array.Resize(ref failureFlames, 2);
            for (int i = 0; i < failureFlames.Length; i++)
                if (failureFlames[i] == null)
                {
                    failureFlames[i] = Raw("FailureFlame" + (i + 1), failureOverlay.transform);
                    Stretch(failureFlames[i].rectTransform, new Vector2(i == 0 ? -.08f : .4f, -.1f),
                        new Vector2(i == 0 ? .6f : 1.08f, 1.1f));
                    if (i == 1) failureFlames[i].uvRect = new Rect(1f, 0f, -1f, 1f);
                }
            if (failureStatus == null)
            {
                failureStatus = Label("FailureInstruction", failureOverlay.transform, 40);
                Stretch(failureStatus.rectTransform, new Vector2(.12f, .42f), new Vector2(.88f, .58f));
                failureStatus.text = string.Empty;
            }
        }

        private void EnsureCurtainWidgets()
        {
            if (fireClip == null)
            {
                fireClip = fireLayer.GetComponent<UnityEngine.UI.RectMask2D>();
                if (fireClip == null) fireClip = fireLayer.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            }
            if (groupFireCurtain == null)
            {
                groupFireCurtain = new GameObject("GroupFireCurtain", typeof(RectTransform)).GetComponent<RectTransform>();
                groupFireCurtain.SetParent(fireLayer, false); Centre(groupFireCurtain);
            }
            groupFireCurtain.SetAsFirstSibling();
            if (curtainBody == null)
            {
                var body = new GameObject("CurtainCoreMask", typeof(RectTransform), typeof(LabJigwiFlameGraphic));
                body.transform.SetParent(groupFireCurtain, false);
                curtainBody = body.GetComponent<LabJigwiFlameGraphic>();
            }
            curtainBody.gameObject.name = "CurtainCoreMask";
            curtainBody.Configure(LabJigwiFlameGraphic.Shape.CurtainArtMask);
            curtainBody.color = Color.white;
            if (curtainArtMask == null)
            {
                curtainArtMask = curtainBody.GetComponent<UnityEngine.UI.Mask>();
                if (curtainArtMask == null) curtainArtMask = curtainBody.gameObject.AddComponent<UnityEngine.UI.Mask>();
            }
            // The white ellipse only writes stencil. There is no coloured procedural plate.
            curtainArtMask.showMaskGraphic = false;
            if (curtainCore == null)
            {
                curtainCore = Raw("DenseCurtainArtwork", curtainBody.transform);
                Stretch(curtainCore.rectTransform, Vector2.zero, Vector2.one);
            }
            if (curtainArt == null)
            {
                curtainArt = Raw("ScreenFireCurtain", groupFireCurtain);
                Centre(curtainArt.rectTransform);
            }
            if (curtainFlames != null)
                foreach (var previous in curtainFlames)
                    if (previous != null && previous != curtainArt) previous.gameObject.SetActive(false);
            curtainFlames = new[] { curtainArt };
            groupFireCurtain.gameObject.SetActive(false);
        }

        private void CacheHpBoundary(LabHud hud)
        {
            if (hud == null || hud.yokaiHpFill == null) return;
            hpBoundaryRoot = hud.yokaiHpFill.rectTransform;
            // The saved frame has children (mask/title/track) beyond its own rectangle. Cache
            // their authored extents once, so future position edits remain respected without scans per frame.
            Transform candidate = hpBoundaryRoot;
            while (candidate != null && canvas != null && candidate != canvas.transform)
            {
                if (candidate.name == "FigmaYokaiHpBar") { hpBoundaryRoot = (RectTransform)candidate; break; }
                candidate = candidate.parent;
            }
            hpBoundaries = hpBoundaryRoot.GetComponentsInChildren<RectTransform>(true);
        }

        private void UpdateBattleFireClip()
        {
            if (fireLayer == null || fireClip == null || canvas == null || battleCamera == null) return;
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Rect screen = battleCamera.pixelRect;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(fireLayer, screen.min, uiCamera, out Vector2 low)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(fireLayer, screen.max, uiCamera, out Vector2 high)) return;
            Rect layer = fireLayer.rect;
            Vector2 minimum = Vector2.Min(low, high), maximum = Vector2.Max(low, high);
            low = Vector2.Max(layer.min, minimum); high = Vector2.Min(layer.max, maximum);
            if (hpBoundaryRoot != null && hpBoundaryRoot.gameObject.activeInHierarchy)
            {
                float bottom = float.PositiveInfinity;
                foreach (RectTransform boundary in hpBoundaries)
                {
                    if (boundary == null || !boundary.gameObject.activeInHierarchy) continue;
                    boundary.GetWorldCorners(clipCorners);
                    for (int i = 0; i < clipCorners.Length; i++)
                    {
                        Vector2 pixel = RectTransformUtility.WorldToScreenPoint(uiCamera, clipCorners[i]);
                        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(fireLayer, pixel, uiCamera, out Vector2 point))
                            bottom = Mathf.Min(bottom, point.y);
                    }
                }
                if (!float.IsInfinity(bottom)) high.y = Mathf.Min(high.y, bottom - 4f);
            }
            high.x = Mathf.Max(low.x + .01f, high.x); high.y = Mathf.Max(low.y + .01f, high.y);
            BattleFireClipRect = Rect.MinMaxRect(low.x, low.y, high.x, high.y);
            // Preserve the full-stretch fireLayer coordinate system used by shared world projection.
            // Only B is masked; A heat/water and all existing HUD objects remain outside this layer.
            fireClip.padding = new Vector4(Mathf.Max(0f, low.x - layer.xMin), Mathf.Max(0f, low.y - layer.yMin),
                Mathf.Max(0f, layer.xMax - high.x), Mathf.Max(0f, layer.yMax - high.y));
        }

        public void SetFont(Font value)
        {
            if (value == null) return;
            font = value;
            if (status != null) status.font = value;
            if (playerStatus != null) playerStatus.font = value;
            if (failureStatus != null) failureStatus.font = value;
        }

        private void EnsureArt()
        {
            if (artReady) return;
            flameTexture = Resources.Load<Texture2D>("Jigwi/SurfaceFlameWash");
            waterTexture = Resources.Load<Texture2D>("Jigwi/WaterDouseWash");
            if (personalFlames != null) foreach (var image in personalFlames) ConfigureFlame(image);
            if (fireImages != null) foreach (var image in fireImages) ConfigureFlame(image);
            if (curtainTexture == null) curtainTexture = Resources.Load<Texture2D>("Jigwi/ScreenFireCurtainWash");
            if (curtainStage2Texture == null) curtainStage2Texture = Resources.Load<Texture2D>("Jigwi/ScreenFireCurtainStage2");
            if (curtainStage1Texture == null) curtainStage1Texture = Resources.Load<Texture2D>("Jigwi/ScreenFireCurtainStage1");
            if (curtainArt != null) ConfigureFlame(curtainArt, curtainTexture);
            if (curtainCore != null) ConfigureFlame(curtainCore, curtainTexture);
            if (failureFlames != null) foreach (var image in failureFlames) ConfigureFlame(image, curtainTexture);
            if (curtainWaterDouse != null) curtainWaterDouse.texture = waterTexture;
            if (personalWaterDouse != null) personalWaterDouse.texture = waterTexture;
            if (fireWaterDouses != null) foreach (var image in fireWaterDouses) if (image != null) image.texture = waterTexture;
            artReady = Application.isPlaying;
        }

        private void ConfigureFlame(UnityEngine.UI.RawImage image, Texture2D texture = null)
        {
            if (image == null) return;
            image.texture = texture != null ? texture : flameTexture;
            if (!Application.isPlaying || flameMaterials.ContainsKey(image)) return;
            Shader shader = Resources.Load<Shader>("Jigwi/ScreenFlame");
            if (shader == null)
            {
                if (!missingShaderReported)
                { Debug.LogWarning("Jigwi screen flame shader is missing; the transparent art remains static.", this); missingShaderReported = true; }
                return;
            }
            var material = new Material(shader) { name = "Jigwi screen flame (runtime)", hideFlags = HideFlags.DontSave };
            image.material = material;
            flameMaterials.Add(image, material);
        }

        private void LateUpdate()
        {
            if (source == null) { Hide(); return; }
            if (battleCamera == null) battleCamera = source.BattleCamera;
            double now = source.JigwiABPresentationTime;
            ApplySnapshot(source.Snapshot, source.LocalPlayerId, now, source.GameStartHeld || source.BattlePresentationHeld);
        }

        public void ApplySnapshot(LabSnapshot snapshot, ulong localPlayer, double now, bool held = false)
        {
            if (personalHeat == null || fireLayer == null || status == null) EnsureWidgets();
            PresentationTime = now;
            if (snapshot == null || snapshot.phase != LabPhase.Playing || snapshot.jigwiAB?.enabled != true)
            { Hide(); ResetHistory(); return; }
            LabJigwiABSnapshot stage = snapshot.jigwiAB;
            // A step back within the jigwi voice's seek tolerance is clock jitter, not a new stage (#451).
            bool fresh = sessionId != snapshot.sessionId || roundNumber != snapshot.round || stageStartedAt != stage.startedAt
                || previousNow >= 0d && now < previousNow - RewindToleranceSeconds;
            if (fresh)
            {
                ResetHistory();
                sessionId = snapshot.sessionId; roundNumber = snapshot.round; stageStartedAt = stage.startedAt;
            }
            bool baseline = !baselineReady;
            ObserveDouses(stage, localPlayer, now, baseline);
            ObserveFailures(stage, now, baseline);
            baselineReady = true; previousNow = now;
            LabJigwiABRound active = stage.Active(now);
            // Active() can return the upcoming warning for instructions. Flames and the B loop
            // belong only to the real mission window, aligned with the voice's terminal shout.
            bool missionActive = active != null && active.Active(now);
            // #479: A no longer tints the screen or stands edge flames beside the board; the talisman and
            // the orbs charring one by one carry it. The saved objects stay (prefab), always hidden.
            personalHeat.gameObject.SetActive(false);
            float animationTime = missionActive ? (float)Math.Max(0d, now - active.activeAt) : 0f;
            VisibleScreenFires = 0;
            if (missionActive && active.kind == LabJigwiABKind.GroupFire) DrawGroup(active, localPlayer, now, animationTime, held);
            else SetFiresHidden(held);
            DrawWater(now, held);
            DrawStatus(stage, active, snapshot, localPlayer, now, held);
            DrawFailure(stage, localPlayer, now);
        }

        /// <summary>#479: true once this seat finished writing its talisman in this A.</summary>
        public static bool TalismanWritten(LabJigwiABRound round, ulong player)
        {
            if (round == null || round.kind != LabJigwiABKind.PersonalBurn) return false;
            foreach (var seat in round.players ?? Array.Empty<LabJigwiABPlayerFire>())
                if (seat != null && seat.playerId == player) return seat.talismanAt >= 0d;
            return false;
        }

        private void ObserveFailures(LabJigwiABSnapshot stage, double now, bool baseline)
        {
            foreach (var round in stage.rounds ?? Array.Empty<LabJigwiABRound>())
            {
                if (round == null || round.kind != LabJigwiABKind.GroupFire
                    || round.resolution != LabJigwiABResolution.Expired
                    || round.bucket) continue; // #551: the bucket B is personal, no shared failure flash
                string key = round.id + "/" + round.resolvedAt.ToString("R", CultureInfo.InvariantCulture);
                if (!seenFailures.Add(key) || baseline || round.resolvedAt < 0d
                    || now < round.resolvedAt || now - round.resolvedAt >= FailureFlashSeconds) continue;
                failedAt = round.resolvedAt;
                FailureFlashCount++;
            }
        }

        /// <summary>
        /// #540: 내 부적을 못 써 판이 탄 뒤(불덩이가 판에 닿은 때부터) 내 북에 붙은 불이 꺼지기까지 남은 초 (없으면 0).
        /// 길어야 seconds이고, 다른 기믹(A·B·공통 불)의 예고가 뜨거나 피버가 시작되면 그때 꺼져 다른 기믹과 겹치지 않는다.
        /// </summary>
        public static float DrumFireSecondsLeft(LabSnapshot snapshot, ulong player, double now, float seconds)
        {
            if (seconds <= 0f || snapshot == null || snapshot.phase != LabPhase.Playing || snapshot.jigwiAB?.rounds == null) return 0f;
            foreach (var round in snapshot.jigwiAB.rounds)
            {
                if (round == null || round.kind != LabJigwiABKind.PersonalBurn || round.resolution != LabJigwiABResolution.Expired
                    || round.resolvedAt < 0d || !SeatFailed(round, player)) continue;
                double from = round.resolvedAt + LabModel.JigwiTalismanFailBurnDelaySeconds;
                double until = from + seconds;
                if (snapshot.jigwiAB.cutoffAt > from) until = Math.Min(until, snapshot.jigwiAB.cutoffAt);
                foreach (var other in snapshot.jigwiAB.rounds)
                    if (other != null && other.warningAt > from) until = Math.Min(until, other.warningAt);
                foreach (var group in snapshot.jigwiStage?.groups ?? Array.Empty<LabJigwiStageGroup>())
                    if (group != null && group.warningAt > from) until = Math.Min(until, group.warningAt);
                if (now >= from && now < until) return (float)(until - now);
            }
            return 0f;
        }

        /// <summary>
        /// #540: 부적을 못 써 타는 구슬이 지금 타오르는 중이면 남은 초, 아니면 0. 지귀 대사가 끝나면 판의 구슬이 모두 함께
        /// `LabModel.JigwiTalismanFailBlazeSeconds`(1초) 동안 아래부터 재가 되어(`LabJigwiAshBurn`) 없어진다.
        /// 불이 붙기 전 구슬은 0(그대로 보임).
        /// </summary>
        public static float FailBlazeSecondsLeft(LabSnapshot snapshot, LabOrbState orb, double serverTime)
        {
            float left = FailBurnSecondsLeft(snapshot, orb, serverTime);
            return left <= (float)LabModel.JigwiTalismanFailBlazeSeconds + .01f ? left : 0f;
        }

        /// <summary>
        /// #540: 부적을 못 써 타기로 정해진 구슬이 없어지기까지 남은 초 (Host 소각 시각까지, 차례를 기다리는 시간 포함).
        /// 그 A가 시간이 다 돼 끝나고 불덩이가 판에 닿은 뒤의 소각만, 아니면 0.
        /// </summary>
        public static float FailBurnSecondsLeft(LabSnapshot snapshot, LabOrbState orb, double serverTime)
        {
            if (orb == null || orb.jigwiABBurnAt < 0d || string.IsNullOrEmpty(orb.jigwiABBurnEventId) || snapshot?.jigwiAB?.rounds == null) return 0f;
            foreach (var round in snapshot.jigwiAB.rounds)
                if (round != null && round.id == orb.jigwiABBurnEventId && round.kind == LabJigwiABKind.PersonalBurn
                    && round.resolution == LabJigwiABResolution.Expired
                    // They catch the moment the fireball lands on the board.
                    && serverTime >= round.resolvedAt + LabModel.JigwiTalismanFailBurnDelaySeconds)
                    return Mathf.Max(0f, (float)(orb.jigwiABBurnAt - serverTime));
            return 0f;
        }

        /// <summary>#540: 이 구슬이 부적을 못 써 끝난 A 때문에 타기로 정해졌는지 (불덩이가 닿기 전 포함). 이런 구슬엔 A 불을 붙이지 않는다.</summary>
        public static bool FailBurnPending(LabSnapshot snapshot, LabOrbState orb)
        {
            if (orb == null || orb.jigwiABBurnAt < 0d || string.IsNullOrEmpty(orb.jigwiABBurnEventId) || snapshot?.jigwiAB?.rounds == null) return false;
            foreach (var round in snapshot.jigwiAB.rounds)
                if (round != null && round.id == orb.jigwiABBurnEventId)
                    return round.kind == LabJigwiABKind.PersonalBurn && round.resolution == LabJigwiABResolution.Expired;
            return false;
        }

        // #595: a common talisman fails every seat when anyone did not finish it.
        private static bool SeatFailed(LabJigwiABRound round, ulong player) => round != null && round.TalismanFailed(player);

        /// <summary>#509: true while a failed B keeps the boards on fire (drums blocked).</summary>
        public bool BoardFireVisible { get; private set; }
        private void DrawFailure(LabJigwiABSnapshot stage, ulong localPlayer, double now)
        {
            if (failureOverlay == null) return;
            double fireUntil = -1d;
            foreach (var round in stage?.rounds ?? Array.Empty<LabJigwiABRound>())
            {
                if (round != null && round.kind == LabJigwiABKind.GroupFire && round.resolution == LabJigwiABResolution.Expired
                    && now >= round.resolvedAt && now < round.boardFireUntil) fireUntil = Math.Max(fireUntil, round.boardFireUntil);
                // #540: an unfinished talisman shows no screen fire; the orbs and drums blazing carry it.
            }
            BoardFireVisible = fireUntil > now;
            bool flash = failedAt >= 0d && now >= failedAt && now - failedAt < FailureFlashSeconds;
            bool visible = flash || BoardFireVisible;
            failureOverlay.gameObject.SetActive(visible);
            if (!visible) return;
            float elapsed = (float)(now - Math.Max(0d, failedAt));
            float opacity = flash ? 1f - elapsed / (float)FailureFlashSeconds : 0f;
            if (BoardFireVisible) opacity = Mathf.Max(opacity, .55f);
            if (failureStatus != null) failureStatus.text = string.Empty; // #509: no failure / board-fire words
            failureOverlay.color = new Color(1f, .13f, .01f, .22f * opacity);
            // #551: no fire on a failed B unless the board-fire punishment is on (it is off in the asset for now);
            // the short red tint alone marks the failure until a new failure look is decided.
            float flames = BoardFireVisible ? opacity : 0f;
            if (failureFlames != null) for (int i = 0; i < failureFlames.Length; i++)
                AnimateFlame(failureFlames[i], elapsed, i * 1.73f, .95f * flames);
            if (failureStatus != null) failureStatus.color = new Color(1f, .97f, .83f, BoardFireVisible ? 1f : opacity);
        }

        private void ObserveDouses(LabJigwiABSnapshot stage, ulong localPlayer, double now, bool baseline)
        {
            foreach (LabJigwiABRound round in stage.rounds ?? Array.Empty<LabJigwiABRound>())
            {
                if (round == null) continue;
                if (round.kind != LabJigwiABKind.GroupFire) continue;
                foreach (var player in round.players ?? Array.Empty<LabJigwiABPlayerFire>())
                {
                    if (player == null || player.playerId != localPlayer) continue;
                    if (player.lastWaterSplashAt >= 0d)
                    {
                        string splash = round.id + "/curtain-water/" + player.lastWaterSplashAt.ToString("R", CultureInfo.InvariantCulture);
                        if (seenDouses.Add(splash) && !baseline && Recent(player.lastWaterSplashAt, now)
                            && TryProjectCurtain(round, localPlayer, now, out Vector2 centre, out Vector2 size))
                        {
                            curtainDousedAt = player.lastWaterSplashAt;
                            curtainSplashCentre = centre; curtainSplashSize = size;
                        }
                    }
                    for (int i = 0; player.targets != null && i < player.targets.Length && i < MaxFires; i++)
                    {
                        var fire = player.targets[i];
                        if (fire == null || fire.extinguishedAt < 0d) continue;
                        string key = fire.id + "/" + fire.extinguishedAt.ToString("R", CultureInfo.InvariantCulture);
                        bool first = seenDouses.Add(key);
                        if (first && !baseline && Recent(fire.extinguishedAt, now))
                        {
                            if (TryProject(round, fire, localPlayer, i, fire.extinguishedAt, out Vector2 centre, out Vector2 size))
                            { lastFirePositions[i] = centre; lastFireSizes[i] = size; hasFirePosition[i] = true; }
                            if (hasFirePosition[i]) fireDousedAt[i] = fire.extinguishedAt;
                        }
                    }
                }
            }
        }
        private bool Recent(double at, double now) => at >= 0d && now >= at && now - at < waterDouseSeconds;

        private void DrawGroup(LabJigwiABRound round, ulong localPlayer, double now, float animationTime, bool held)
        {
            LabJigwiABPlayerFire mine = null;
            foreach (var player in round.players ?? Array.Empty<LabJigwiABPlayerFire>())
                if (player != null && player.playerId == localPlayer) { mine = player; break; }
            UpdateBattleFireClip();
            // #509: B is judged on the boss itself. No curtain, ignition or pillar art is shown and the fire loop stays off.
            if (groupFireCurtain != null) groupFireCurtain.gameObject.SetActive(false);
            if (fireImages != null) foreach (var image in fireImages) if (image != null) image.gameObject.SetActive(false);
            if (audioView != null) audioView.Stop();
            bool haptics = source != null && source.config != null && source.config.HapticsEnabled;
            bool picked = mine != null;
            var slot = picked && mine.targets != null && mine.targets.Length > 0 ? mine.targets[0] : null;
            CurtainVisualLevel = 0;
            // #509: picked seats that still have to hit inside the one chance (0 for a helper screen).
            int hitsSoFar = 0, pickedCount = 0;
            foreach (var player in round.players ?? Array.Empty<LabJigwiABPlayerFire>())
                if (player?.targets != null && player.targets.Length > 0)
                { pickedCount++; if (player.targets[0].extinguishedAt >= 0d) hitsSoFar++; }
            RemainingIgnitions = slot != null && slot.lit ? Math.Max(0, pickedCount - hitsSoFar) : 0;
            if (SyncPillar != null) SyncPillar.Present(round, mine, picked, BattleFireClipRect, now, held, haptics);
            // Reproject the real local water projectile path (picked seats throw the counted Water).
            if (waterFlightTrail != null)
            {
                if (picked) waterFlightTrail.Present(canvas, battleCamera, localPlayer, round.id, now, held);
                else waterFlightTrail.Hide();
            }
        }

        private static Rect SafeUv(Rect value)
        {
            float x = Mathf.Clamp(value.x, 0f, .99f), y = Mathf.Clamp(value.y, 0f, .99f);
            return new Rect(x, y, Mathf.Clamp(value.width, .01f, 1f - x), Mathf.Clamp(value.height, .01f, 1f - y));
        }
        private static Rect CoverUv(Texture texture, Rect window, float aspect)
        {
            window = SafeUv(window);
            if (texture == null || texture.height < 1 || !(aspect > 0f)) return window;
            float sourceAspect = texture.width * window.width / (texture.height * window.height);
            if (sourceAspect > aspect)
            {
                float width = window.width * aspect / sourceAspect;
                window.x += (window.width - width) * .5f; window.width = width;
            }
            else
            {
                float height = window.height * sourceAspect / aspect;
                window.y += (window.height - height) * .5f; window.height = height;
            }
            return window;
        }

        private bool TryProjectCurtain(LabJigwiABRound round, ulong owner, double now, out Vector2 centre, out Vector2 size)
        {
            centre = size = Vector2.zero;
            if (source == null || battleCamera == null || canvas == null || fireLayer == null
                || !source.TryGetJigwiABCurtainFrame(round, owner, now, out Vector3 world, out Quaternion rotation, out Vector2 half)) return false;
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = -min;
            for (int i = 0; i < 4; i++)
            {
                Vector3 corner = world + rotation * new Vector3((i & 1) == 0 ? -half.x : half.x,
                    (i & 2) == 0 ? -half.y : half.y, 0f);
                if (battleCamera.WorldToViewportPoint(corner).z <= 0f
                    || !RectTransformUtility.ScreenPointToLocalPointInRectangle(fireLayer, battleCamera.WorldToScreenPoint(corner),
                        uiCamera, out Vector2 projected)) return false;
                min = Vector2.Min(min, projected); max = Vector2.Max(max, projected);
            }
            centre = (min + max) * .5f; size = max - min;
            return size.x > .01f && size.y > .01f;
        }

        private bool TryProject(LabJigwiABRound round, LabJigwiABFireTarget fire, ulong owner, int index, double now,
            out Vector2 centre, out Vector2 size)
        {
            centre = size = Vector2.zero;
            if (source == null || battleCamera == null || canvas == null || fireLayer == null
                || !source.TryGetJigwiABFireWorldPosition(round, fire, owner, index, now, out Vector3 point)) return false;
            Vector3 projected = battleCamera.WorldToViewportPoint(point);
            if (projected.z <= 0f) return false;
            Vector2 screen = battleCamera.WorldToScreenPoint(point);
            Vector2 edge = battleCamera.WorldToScreenPoint(point + battleCamera.transform.right * round.targetRadius);
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(fireLayer, screen, uiCamera, out centre)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(fireLayer, edge, uiCamera, out Vector2 rim)) return false;
            float diameter = Vector2.Distance(centre, rim) * 2f;
            size = Vector2.one * diameter;
            return diameter > .01f;
        }

        private void DrawWater(double now, bool held)
        {
            float personalAge = (float)(now - personalDousedAt);
            bool showPersonal = personalDousedAt >= 0d && personalAge >= 0f && personalAge < waterDouseSeconds && waterTexture != null;
            personalWaterDouse.gameObject.SetActive(showPersonal);
            float waterAspect = waterTexture != null ? waterTexture.width / (float)waterTexture.height : 1.5f;
            if (showPersonal)
            {
                var root = (RectTransform)transform;
                float progress = Mathf.Clamp01(personalAge / waterDouseSeconds);
                float width = Mathf.Min(root.rect.width * .96f, root.rect.height * .6f * waterAspect)
                    * Mathf.Lerp(.86f, 1.06f, progress);
                personalWaterDouse.rectTransform.sizeDelta = new Vector2(width, width / waterAspect);
                // A broad pour descends over the board, using the same held battle clock as the fire.
                personalWaterDouse.rectTransform.anchoredPosition = Vector2.up * root.rect.height * Mathf.Lerp(.12f, -.06f, progress);
                personalWaterDouse.color = new Color(1f, 1f, 1f, 1f - progress);
            }
            float curtainAge = (float)(now - curtainDousedAt);
            bool showCurtainWater = curtainDousedAt >= 0d && curtainAge >= 0f && curtainAge < waterDouseSeconds && waterTexture != null;
            if (curtainWaterDouse != null)
            {
                curtainWaterDouse.gameObject.SetActive(showCurtainWater);
                if (showCurtainWater)
                {
                    float progress = curtainAge / waterDouseSeconds;
                    curtainWaterDouse.rectTransform.anchoredPosition = curtainSplashCentre;
                    float width = curtainSplashSize.x * Mathf.Lerp(.9f, 1.1f, progress);
                    curtainWaterDouse.rectTransform.sizeDelta = new Vector2(width, width / waterAspect);
                    curtainWaterDouse.color = new Color(1f, 1f, 1f, 1f - progress);
                }
            }
            for (int i = 0; fireWaterDouses != null && i < fireWaterDouses.Length; i++)
            {
                float age = (float)(now - fireDousedAt[i]);
                bool show = fireDousedAt[i] >= 0d && age >= 0f && age < waterDouseSeconds && waterTexture != null;
                var image = fireWaterDouses[i];
                if (image == null) continue;
                image.gameObject.SetActive(show);
                if (!show) continue;
                image.rectTransform.anchoredPosition = lastFirePositions[i];
                float width = lastFireSizes[i].x * (1.2f + .2f * age / waterDouseSeconds);
                image.rectTransform.sizeDelta = new Vector2(width, width / waterAspect);
                image.color = new Color(1f, 1f, 1f, 1f - age / waterDouseSeconds);
            }
        }

        private void DrawStatus(LabJigwiABSnapshot stage, LabJigwiABRound active, LabSnapshot snapshot, ulong local, double now, bool held)
        {
            // #509: no fire target to avoid any more; the instructions keep their saved place.
            RestoreStatusLayout();
            rows.Clear();
            // 피버는 세 스테이지 공통 연출(#441, LabYaguangFeverHud)이 알린다.
            // #509: the only words on screen are Jigwi's two-second taunts (no progress, guide, result or next-gimmick
            // lines). The drops, the "now" vibration and the failure overlay carry the rest without text.
            // #540: the taunts are spoken by Jigwi's voice now (LabJigwiTalismanOverlay); no warning words on screen.
            string words = string.Empty;
            LabJigwiABRound previewRound = AdvancePreview(now); // #595 editor preview (C6 Lab/Jigwi menu)
            if (previewRound != null)
            {
                DrawTalismanProgress(previewRound, now);
                MaybeBeginSeal(previewRound, now);
                AnimateSeal();
            }
            else
            {
                DrawTalismanProgress(LastCommonTalisman(stage, now) ?? active, now, snapshot, local); // #595: a circle of seats
                DrawTalismanSeal(stage, now); // #595: all written — the talismans fly to the boss, "물" bursts, a waterfall pours
            }
            status.text = words; playerStatus.text = rows.ToString();
            statusPanel.gameObject.SetActive(words.Length > 0);
            playerStatus.gameObject.SetActive(rows.Length > 0);
        }

        // ---------- #595 공용 부적 진행: 사람 수만큼 부적 아이콘이 놓이고, 쓴 사람 수만큼 환하게 켜진다(켜질 때 톡 튐) ----------
        private RectTransform talismanRow;
        private readonly UnityEngine.UI.Image[] talismanIcons = new UnityEngine.UI.Image[LabJigwiABNetworkValidation.MaximumPlayers];
        private readonly float[] talismanLitAt = { -1f, -1f, -1f, -1f, -1f };
        private static Sprite talismanIconSprite;
        /// <summary>테스트용: 지금 보이는 부적 아이콘 수 / 켜진 수.</summary>
        public int TalismanIconsShown { get; private set; }
        public int TalismanIconsLit { get; private set; }

        private const double TalismanLingerSeconds = 1.5d;
        /// <summary>#595: the common talisman running now, or the one that just ended (still lingering).</summary>
        private static LabJigwiABRound LastCommonTalisman(LabJigwiABSnapshot stage, double now)
        {
            LabJigwiABRound found = null;
            foreach (var round in stage?.rounds ?? Array.Empty<LabJigwiABRound>())
                if (round != null && round.kind == LabJigwiABKind.PersonalBurn && round.common && now >= round.activeAt
                    && (round.Active(now) || round.resolvedAt >= 0d && now < round.resolvedAt + TalismanLingerSeconds)) found = round;
            return found;
        }

        private UnityEngine.UI.Image talismanCircle;
        /// <summary>
        /// #595: 부적 아이콘을 작은 지도처럼 사람 자리대로 원 위에 놓는다(나는 아래, 다음 P부터 시계 방향). 그 사람이 다 쓰면
        /// 그 자리 부적이 톡 켜져, 누가 썼고 누가 아직인지 자리로 보인다.
        /// </summary>
        private void DrawTalismanProgress(LabJigwiABRound active, double now, LabSnapshot snapshot = null, ulong local = ulong.MaxValue)
        {
            int done = 0, total = 0;
            // #595: also for a moment after it ends, so the last talisman written visibly lights its icon
            // (alone, writing it finishes the round at once).
            bool show = active != null && active.kind == LabJigwiABKind.PersonalBurn && active.common
                && (active.Active(now) || active.resolvedAt >= 0d && now >= active.resolvedAt && now < active.resolvedAt + TalismanLingerSeconds
                    && (active.resolution == LabJigwiABResolution.Completed && active.id != sealedRound // #595: they flew to the boss
                        || active.resolution == LabJigwiABResolution.Expired));
            var seats = new List<LabJigwiABPlayerFire>();
            if (show)
                foreach (var seat in active.players ?? Array.Empty<LabJigwiABPlayerFire>())
                    if (seat != null && seats.Count < talismanIcons.Length) { seats.Add(seat); if (seat.talismanAt >= 0d) done++; }
            total = seats.Count;
            TalismanIconsShown = total; TalismanIconsLit = Mathf.Min(done, total);
            if (total == 0) { if (talismanRow != null && talismanRow.gameObject.activeSelf) talismanRow.gameObject.SetActive(false); return; }
            // Seat order, as on the minimap: by P number, starting from me.
            seats.Sort((a, b) => SeatNumber(snapshot, a.playerId).CompareTo(SeatNumber(snapshot, b.playerId)));
            int mine = Mathf.Max(0, seats.FindIndex(seat => seat.playerId == local));
            if (talismanRow == null)
            {
                talismanRow = new GameObject("TalismanProgress", typeof(RectTransform)).GetComponent<RectTransform>();
                talismanRow.gameObject.layer = gameObject.layer;
                talismanRow.SetParent(transform, false);
                // Just above the board, under the boss: where the red alarm flashes while writing.
                talismanRow.anchorMin = talismanRow.anchorMax = new Vector2(.5f, .515f);
                talismanCircle = new GameObject("Talisman Circle", typeof(RectTransform)).AddComponent<UnityEngine.UI.Image>();
                talismanCircle.gameObject.layer = gameObject.layer; talismanCircle.raycastTarget = false;
                talismanCircle.sprite = WaitRing(); talismanCircle.transform.SetParent(talismanRow, false);
                for (int i = 0; i < talismanIcons.Length; i++)
                {
                    var icon = new GameObject("Talisman " + i, typeof(RectTransform)).AddComponent<UnityEngine.UI.Image>();
                    icon.gameObject.layer = gameObject.layer;
                    icon.transform.SetParent(talismanRow, false);
                    icon.sprite = TalismanIcon(); icon.preserveAspect = true; icon.raycastTarget = false;
                    talismanIcons[i] = icon;
                }
            }
            if (!talismanRow.gameObject.activeSelf) talismanRow.gameObject.SetActive(true);
            talismanRow.SetAsLastSibling();
            var area = transform as RectTransform;
            float height = (area != null ? area.rect.height : 1600f) * .13f; // the circle's size
            talismanRow.sizeDelta = Vector2.one * height;
            float radius = total == 1 ? 0f : height * .36f;
            talismanCircle.rectTransform.sizeDelta = Vector2.one * radius * 2f;
            talismanCircle.color = new Color(.95f, .82f, .55f, total > 1 ? .45f : 0f);
            float t = Time.unscaledTime;
            for (int i = 0; i < talismanIcons.Length; i++)
            {
                var icon = talismanIcons[i];
                bool on = i < total;
                if (icon.gameObject.activeSelf != on) icon.gameObject.SetActive(on);
                if (!on) { talismanLitAt[i] = -1f; continue; }
                var seat = seats[i];
                bool lit = seat.talismanAt >= 0d, me = i == mine && local != ulong.MaxValue;
                if (!lit) talismanLitAt[i] = -1f;
                else if (talismanLitAt[i] < 0f) talismanLitAt[i] = t;
                var rect = icon.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = LabDrumMinimap.UnitPosition((i - mine + total) % total, total) * radius;
                rect.sizeDelta = new Vector2(height * .2f, height * .34f) * (me ? 1.15f : 1f);
                float pop = lit ? 1f + .45f * Mathf.Clamp01(1f - (t - talismanLitAt[i]) / .35f) : 1f;
                rect.localScale = Vector3.one * pop;
                icon.color = lit ? Color.white : new Color(.38f, .34f, .32f, .55f);
            }
        }

        private static int SeatNumber(LabSnapshot snapshot, ulong player)
        {
            foreach (var p in snapshot?.players ?? Array.Empty<LabPlayerState>()) if (p != null && p.id == player) return p.seat;
            return (int)(player % 1000ul); // preview seats (900, 901, ...) keep their order
        }

        // ---------- #595 누가 썼나: 사람 표시(양옆 P 동그라미, 작은 지도 자리)에 부적 도장이 쾅. 아직이면 그 표시가 깜빡 ----------
        private readonly Dictionary<RectTransform, UnityEngine.UI.Image> stamps = new Dictionary<RectTransform, UnityEngine.UI.Image>();
        private readonly Dictionary<RectTransform, float> stampedAt = new Dictionary<RectTransform, float>();
        private readonly HashSet<RectTransform> stampsUsed = new HashSet<RectTransform>();
        private LabDrumMinimap minimap;
        private LabHud hud;
        private static Sprite waitRingSprite;
        /// <summary>테스트용: 도장이 찍힌 사람 표시 수 / 기다리는 표시 수.</summary>
        public int StampsShown { get; private set; }
        public int StampsWaiting { get; private set; }

        private void DrawTalismanStamps(LabJigwiABRound round, LabSnapshot snapshot, ulong local, double now)
        {
            stampsUsed.Clear(); StampsShown = StampsWaiting = 0;
            if (round != null && snapshot?.players != null)
            {
                if (minimap == null) minimap = FindFirstObjectByType<LabDrumMinimap>();
                if (hud == null) hud = FindFirstObjectByType<LabHud>();
                LabPlayerState[] seats = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.OrderBy(
                    System.Linq.Enumerable.Where(snapshot.players, p => p != null), p => p.seat));
                int mine = Array.FindIndex(seats, p => p.id == local), n = seats.Length;
                foreach (var seat in round.players ?? Array.Empty<LabJigwiABPlayerFire>())
                {
                    if (seat == null) continue;
                    bool done = seat.talismanAt >= 0d;
                    if (minimap != null) Stamp(minimap.SeatMarker(seat.playerId), done);
                    // My left neighbour is the next seat, my right neighbour the one before (LabDrumMinimap.UnitPosition).
                    if (hud != null && mine >= 0 && n > 1)
                    {
                        int at = Array.FindIndex(seats, p => p.id == seat.playerId);
                        if (at == (mine + 1) % n && hud.leftNeighborBadge != null && hud.leftNeighborBadge.activeInHierarchy)
                            Stamp(hud.leftNeighborBadge.transform as RectTransform, done);
                        if (at == (mine + n - 1) % n && hud.rightNeighborBadge != null && hud.rightNeighborBadge.activeInHierarchy)
                            Stamp(hud.rightNeighborBadge.transform as RectTransform, done);
                    }
                }
            }
            foreach (var pair in stamps)
                if (!stampsUsed.Contains(pair.Key) && pair.Value != null && pair.Value.gameObject.activeSelf) pair.Value.gameObject.SetActive(false);
        }

        private void Stamp(RectTransform marker, bool done)
        {
            if (marker == null || !marker.gameObject.activeInHierarchy || stampsUsed.Contains(marker)) return;
            stampsUsed.Add(marker);
            if (!stamps.TryGetValue(marker, out var image) || image == null)
            {
                image = new GameObject("Talisman Stamp", typeof(RectTransform)).AddComponent<UnityEngine.UI.Image>();
                image.gameObject.layer = gameObject.layer; image.raycastTarget = false; image.preserveAspect = true;
                image.transform.SetParent(transform, false);
                stamps[marker] = image;
            }
            if (!image.gameObject.activeSelf) image.gameObject.SetActive(true);
            image.transform.SetAsLastSibling();
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.position = marker.TransformPoint(marker.rect.center);
            float side = Mathf.Max(marker.rect.width * marker.lossyScale.x, marker.rect.height * marker.lossyScale.y)
                / Mathf.Max(.0001f, rect.lossyScale.x);
            float t = Time.unscaledTime;
            if (done)
            {
                if (!stampedAt.TryGetValue(marker, out float when) || when < 0f) stampedAt[marker] = when = t;
                StampsShown++;
                image.sprite = TalismanIcon();
                // Slams down big and settles over the marker's lower right, tilted like a stamped seal.
                float slam = Mathf.Clamp01((t - when) / .18f);
                rect.sizeDelta = new Vector2(side * .55f, side * .95f);
                rect.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, slam * slam);
                rect.position += (Vector3)(new Vector2(side * .32f, -side * .1f) * rect.lossyScale.x);
                rect.localEulerAngles = new Vector3(0f, 0f, -12f);
                image.color = new Color(1f, 1f, 1f, Mathf.Lerp(.3f, 1f, slam));
            }
            else
            {
                stampedAt[marker] = -1f;
                StampsWaiting++;
                // Still writing: an ember ring blinks around the marker.
                image.sprite = WaitRing();
                rect.sizeDelta = Vector2.one * side * 1.35f;
                rect.localEulerAngles = Vector3.zero;
                float blink = .5f + .5f * Mathf.Sin(t * 6f);
                rect.localScale = Vector3.one * (1f + .08f * blink);
                image.color = new Color(1f, .45f, .12f, .35f + .55f * blink);
            }
        }

        private void HideStamps()
        {
            foreach (var pair in stamps) if (pair.Value != null) pair.Value.gameObject.SetActive(false);
            StampsShown = StampsWaiting = 0;
        }

        private static Sprite WaitRing()
        {
            if (waitRingSprite != null) return waitRingSprite;
            const int s = 64;
            var texture = new Texture2D(s, s, TextureFormat.RGBA32, false)
            { name = "Jigwi talisman wait ring (#595 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = new Vector2(x + .5f - s * .5f, y + .5f - s * .5f).magnitude / (s * .5f);
                    pixels[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 20f) * Mathf.Clamp01((d - .78f) * 20f));
                }
            texture.SetPixels(pixels); texture.Apply(false, true);
            waitRingSprite = Sprite.Create(texture, new Rect(0f, 0f, s, s), new Vector2(.5f, .5f), s);
            waitRingSprite.hideFlags = HideFlags.DontSave;
            return waitRingSprite;
        }

        // ---------- #595 모두 썼다: 켜진 부적들이 빛나다 박으로 휘릭 날아가고, 박에서 "물" 글자(신라문화체)가 확 커지며,
        // 위에서 부적 구슬 꼬리처럼 먹 테두리·물·하이라이트 세 겹 폭포가 쏟아지고 물방울이 튄다 ----------
        private RectTransform sealRoot;
        private UnityEngine.UI.Text waterGlyph;
        private UnityEngine.UI.RawImage waterfallBack, waterfall;
        private UnityEngine.UI.Image splash, whiteFlash;
        private static Texture2D waterfallTexture;
        private static Sprite dropSprite;
        private readonly UnityEngine.UI.Image[] flyers = new UnityEngine.UI.Image[LabJigwiABNetworkValidation.MaximumPlayers];
        private readonly UnityEngine.UI.Image[] flyerGlows = new UnityEngine.UI.Image[LabJigwiABNetworkValidation.MaximumPlayers];
        private readonly UnityEngine.UI.Image[] flyerShines = new UnityEngine.UI.Image[LabJigwiABNetworkValidation.MaximumPlayers];
        private const int SparklesEach = 4;
        private readonly UnityEngine.UI.Image[] sparkles = new UnityEngine.UI.Image[LabJigwiABNetworkValidation.MaximumPlayers * SparklesEach];
        private static Sprite sparkleSprite, shineSprite;
        private readonly Vector3[] flyFrom = new Vector3[LabJigwiABNetworkValidation.MaximumPlayers];
        private readonly UnityEngine.UI.Image[] drops = new UnityEngine.UI.Image[24];
        private readonly Vector2[] dropVelocity = new Vector2[24];
        private readonly Vector3[] dropAt = new Vector3[24];
        private readonly float[] dropBorn = new float[24];
        private string sealedRound;
        private float sealStartedAt = -1f;
        private int flyCount, nextDrop;
        private bool poured;
        private float waterfallPulseAt = -1f;
        private float lastDropAt;
        private AudioSource sealAudio;
        private const float GlowSeconds = 1f, FlySeconds = .4f, GlyphSeconds = .7f, PourSeconds = 2.2f; // #595 QA: a longer, grander pour
        /// <summary>테스트용: 완성 연출이 도는 중인지 / 폭포가 보이는지.</summary>
        public bool SealPlaying => sealStartedAt >= 0f;
        public bool WaterfallVisible => waterfall != null && waterfall.gameObject.activeInHierarchy;

        private void DrawTalismanSeal(LabJigwiABSnapshot stage, double now)
        {
            foreach (var round in stage?.rounds ?? Array.Empty<LabJigwiABRound>()) MaybeBeginSeal(round, now);
            AnimateSeal();
        }

        private void MaybeBeginSeal(LabJigwiABRound round, double now)
        {
            if (round == null || !round.common || round.kind != LabJigwiABKind.PersonalBurn
                || round.resolution != LabJigwiABResolution.Completed || round.resolvedAt < 0d) return;
            if (round.id == sealedRound || now < round.resolvedAt || now - round.resolvedAt > 1d) return;
            BeginSeal(round);
        }

        private void AnimateSeal()
        {
            if (sealStartedAt < 0f) return;
            float t = Time.unscaledTime, age = t - sealStartedAt;
            if (age >= GlowSeconds + FlySeconds + PourSeconds + .6f) { sealStartedAt = -1f; sealRoot.gameObject.SetActive(false); return; }
            Vector3 boss = BossPoint();
            float unit = transform.lossyScale.y;
            // ① All filled: the talismans themselves shine white for about a second (a soft halo breathing, sparkles
            // twinkling around them), then their power is released and they take off. The screen itself stays as it is.
            float glow = Mathf.Clamp01(age / GlowSeconds);
            if (whiteFlash.gameObject.activeSelf) whiteFlash.gameObject.SetActive(false);
            float shine = Mathf.Clamp01(age / .2f) * (age < GlowSeconds ? 1f : 1f - Mathf.Clamp01((age - GlowSeconds) / .25f));
            for (int i = 0; i < sparkles.Length; i++)
            {
                int owner = i / SparklesEach;
                bool on = owner < flyCount && shine > .01f;
                if (sparkles[i].gameObject.activeSelf != on) sparkles[i].gameObject.SetActive(on);
                if (!on) continue;
                // Each sparkle blinks on its own beat at its own spot around its talisman.
                float beat = Mathf.Repeat(t * 2.2f + i * .37f, 1f), twinkle = Mathf.Sin(Mathf.PI * beat);
                float angle = (i % SparklesEach) * (360f / SparklesEach) + i * 23f;
                Vector2 offset = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad))
                    * flyers[owner].rectTransform.sizeDelta.y * (.55f + .2f * beat);
                sparkles[i].rectTransform.position = flyFrom[owner] + (Vector3)(offset * transform.lossyScale.x);
                float size = flyers[owner].rectTransform.sizeDelta.x * (.35f + .45f * twinkle);
                sparkles[i].rectTransform.sizeDelta = Vector2.one * size;
                sparkles[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f * beat);
                sparkles[i].color = new Color(1f, 1f, .95f, shine * twinkle);
            }
            float fly = Mathf.Clamp01((age - GlowSeconds) / FlySeconds);
            for (int i = 0; i < flyers.Length; i++)
            {
                bool on = i < flyCount && fly < 1f;
                if (flyers[i].gameObject.activeSelf != on) flyers[i].gameObject.SetActive(on);
                if (flyerGlows[i].gameObject.activeSelf != on) flyerGlows[i].gameObject.SetActive(on);
                if (!on) { if (flyerShines[i].gameObject.activeSelf) flyerShines[i].gameObject.SetActive(false); continue; }
                float k = Mathf.Clamp01((fly - i * .05f) / .8f), ease = k * k;
                Vector3 arc = Vector3.up * Mathf.Sin(Mathf.PI * k) * 60f * unit;
                Vector3 at = Vector3.Lerp(flyFrom[i], boss, ease) + arc;
                float pulse = 1f + .12f * Mathf.Sin(Mathf.PI * glow) + .04f * Mathf.Sin(t * 9f) * (1f - fly);
                flyers[i].rectTransform.position = at;
                flyers[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, 360f * ease * (i % 2 == 0 ? 1f : -1f));
                flyers[i].rectTransform.localScale = Vector3.one * pulse * Mathf.Lerp(1f, .6f, ease);
                // The paper itself turns luminous white (a white silhouette laid over it).
                flyers[i].color = Color.white;
                bool shining = shine > .01f;
                if (flyerShines[i].gameObject.activeSelf != shining) flyerShines[i].gameObject.SetActive(shining);
                flyerShines[i].rectTransform.position = at;
                flyerShines[i].rectTransform.sizeDelta = flyers[i].rectTransform.sizeDelta;
                flyerShines[i].rectTransform.localEulerAngles = flyers[i].rectTransform.localEulerAngles;
                flyerShines[i].rectTransform.localScale = flyers[i].rectTransform.localScale;
                flyerShines[i].color = new Color(1f, 1f, 1f, .75f * shine * (.75f + .25f * Mathf.Sin(t * 7f + i)));
                float breathe = .85f + .15f * Mathf.Sin(t * 6f + i);
                flyerGlows[i].rectTransform.position = at;
                flyerGlows[i].rectTransform.sizeDelta = Vector2.one * flyers[i].rectTransform.sizeDelta.y * 2.1f * breathe;
                flyerGlows[i].color = new Color(1f, 1f, .96f, Mathf.Max(.95f * shine * breathe, .35f * (1f - ease)));
            }
            float s = age - GlowSeconds - FlySeconds;
            bool letter = false; // #595 QA: no "물" letter with the waterfall (kept off; the waterfall alone)
            if (waterGlyph.gameObject.activeSelf != letter) waterGlyph.gameObject.SetActive(letter);
            if (letter)
            {
                float g = s / GlyphSeconds;
                waterGlyph.rectTransform.position = boss;
                waterGlyph.rectTransform.localScale = Vector3.one * Mathf.Lerp(.6f, 3.4f, 1f - (1f - g) * (1f - g));
                waterGlyph.color = new Color(.2f, .62f, 1f, 1f - g * g);
            }
            // ③ The waterfall: an ink-edged water column (like the talisman orb's tail) in two layers rushing at different
            // speeds, wobbling a little, with a splash and drops flying off where it hits the boss.
            bool pour = s >= 0f && s < PourSeconds;
            foreach (var part in new UnityEngine.UI.Graphic[] { waterfallBack, waterfall, splash })
                if (part.gameObject.activeSelf != pour) part.gameObject.SetActive(pour);
            AnimateDrops(t, unit);
            if (!pour) return;
            if (!poured)
            {
                poured = true;
                PlayPour();
                // A heavy hit as it lands, then a deep rumble for as long as it pours (low sharpness = heavy water).
                if (source != null) LabHaptics.PlayDirect(source.config, LabHapticStyle.Heavy, 1f, PourSeconds - .4f, .85f, .15f);
                waterfallPulseAt = Time.unscaledTime + .45f;
                Collider hitbox = source != null && source.target != null ? source.target.Hitbox : null;
                if (hitbox != null) { LabJigwiWaterFx.Get(hitbox.gameObject.layer); LabJigwiWaterFx.Shake(hitbox.bounds.extents.magnitude * .1f, 1f); }
            }
            var rect = transform as RectTransform;
            float top = rect != null ? rect.TransformPoint(new Vector3(0f, rect.rect.yMax, 0f)).y : boss.y + 1000f;
            float width = (rect != null ? rect.rect.width : 600f) * .3f;
            float grow = Mathf.Clamp01(s / .18f), fade = 1f - Mathf.Clamp01((s - (PourSeconds - .4f)) / .4f);
            float wobble = 1f + .06f * Mathf.Sin(t * 17f) + .04f * Mathf.Sin(t * 29f);
            PlaceColumn(waterfallBack, boss, top, width * 1.25f * wobble * Mathf.Lerp(.5f, 1f, grow), grow, t * 1.6f, .55f * fade, -.5f);
            PlaceColumn(waterfall, boss, top, width * (2f - wobble) * Mathf.Lerp(.5f, 1f, grow), grow, t * 2.8f, .95f * fade, .3f);
            splash.rectTransform.position = boss;
            splash.rectTransform.sizeDelta = Vector2.one * width * 1.7f * (1f + .1f * Mathf.Sin(t * 30f));
            splash.color = new Color(.8f, .94f, 1f, .8f * fade * grow);
            // Rolling surges on top of the rumble, every 0.45 s, fading with the pour.
            if (source != null && waterfallPulseAt > 0f && t >= waterfallPulseAt && fade > .3f)
            { waterfallPulseAt = t + .45f; LabHaptics.PlayDirect(source.config, LabHapticStyle.Medium, .55f * fade); }
            // Drops fly off the splash all the time it pours.
            if (fade > .2f && t - lastDropAt > .03f)
            {
                lastDropAt = t;
                var d = drops[nextDrop]; int i2 = nextDrop; nextDrop = (nextDrop + 1) % drops.Length;
                dropAt[i2] = boss + new Vector3(UnityEngine.Random.Range(-.4f, .4f) * width * unit, 0f, 0f);
                dropVelocity[i2] = new Vector2(UnityEngine.Random.Range(-1f, 1f) * 520f, UnityEngine.Random.Range(380f, 720f));
                dropBorn[i2] = t;
                d.gameObject.SetActive(true);
            }
        }

        private void PlaceColumn(UnityEngine.UI.RawImage column, Vector3 boss, float top, float width, float grow, float scroll, float alpha, float sway)
        {
            var fall = column.rectTransform;
            fall.pivot = new Vector2(.5f, 1f);
            fall.position = new Vector3(boss.x, top, boss.z);
            float length = (top - boss.y) / Mathf.Max(.0001f, fall.lossyScale.y);
            fall.sizeDelta = new Vector2(width, length * grow);
            column.uvRect = new Rect(sway * .1f * Mathf.Sin(scroll), scroll, 1f, 1.4f);
            column.color = new Color(1f, 1f, 1f, alpha);
        }

        private void AnimateDrops(float t, float unit)
        {
            for (int i = 0; i < drops.Length; i++)
            {
                var d = drops[i];
                if (d == null || !d.gameObject.activeSelf) continue;
                float life = t - dropBorn[i];
                if (life > .6f) { d.gameObject.SetActive(false); continue; }
                Vector2 v = dropVelocity[i];
                d.rectTransform.position = dropAt[i] + new Vector3(v.x * life, v.y * life - 1400f * life * life, 0f) * unit;
                float size = Mathf.Lerp(26f, 10f, life / .6f);
                d.rectTransform.sizeDelta = new Vector2(size, size * 1.25f);
                d.color = new Color(.75f, .92f, 1f, 1f - life / .6f);
            }
        }

        private void BeginSeal(LabJigwiABRound round)
        {
            sealedRound = round.id; sealStartedAt = Time.unscaledTime; poured = false;
            if (sealRoot == null)
            {
                sealRoot = new GameObject("TalismanSeal", typeof(RectTransform)).GetComponent<RectTransform>();
                sealRoot.gameObject.layer = gameObject.layer; sealRoot.SetParent(transform, false);
                sealRoot.anchorMin = Vector2.zero; sealRoot.anchorMax = Vector2.one; sealRoot.offsetMin = sealRoot.offsetMax = Vector2.zero;
                waterfallBack = NewRaw("Talisman Waterfall Back"); waterfall = NewRaw("Talisman Waterfall");
                splash = NewSealImage("Talisman Waterfall Splash", LabProceduralFire.Glow());
                whiteFlash = NewSealImage("Talisman White Light", null);
                whiteFlash.rectTransform.anchorMin = Vector2.zero; whiteFlash.rectTransform.anchorMax = Vector2.one;
                whiteFlash.rectTransform.offsetMin = whiteFlash.rectTransform.offsetMax = Vector2.zero;
                whiteFlash.transform.SetAsFirstSibling(); // behind the glowing talismans
                for (int i = 0; i < drops.Length; i++) { drops[i] = NewSealImage("Waterfall Drop " + i, Drop()); drops[i].gameObject.SetActive(false); }
                for (int i = 0; i < flyers.Length; i++)
                {
                    flyerGlows[i] = NewSealImage("Talisman Glow " + i, LabProceduralFire.Glow());
                    flyers[i] = NewSealImage("Flying Talisman " + i, TalismanIcon()); flyers[i].preserveAspect = true;
                    // A white silhouette of the talisman laid over it: the paper itself shines.
                    flyerShines[i] = NewSealImage("Talisman Shine " + i, TalismanShine()); flyerShines[i].preserveAspect = true;
                }
                for (int i = 0; i < sparkles.Length; i++) sparkles[i] = NewSealImage("Talisman Sparkle " + i, Sparkle());
                waterGlyph = Label("Talisman Water Glyph", sealRoot, 150);
                waterGlyph.text = "물"; waterGlyph.alignment = TextAnchor.MiddleCenter;
                waterGlyph.horizontalOverflow = HorizontalWrapMode.Overflow; waterGlyph.verticalOverflow = VerticalWrapMode.Overflow;
                waterGlyph.rectTransform.sizeDelta = new Vector2(240f, 240f);
                var outline = waterGlyph.GetComponent<UnityEngine.UI.Outline>();
                if (outline == null) outline = waterGlyph.gameObject.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = new Color(.02f, .1f, .2f, .95f); outline.effectDistance = new Vector2(4f, -4f);
            }
            // The talisman's own letter: Shilla Culture, as written on the paper.
            Font glyphFont = Talisman != null ? Talisman.GlyphFont : null;
            if (glyphFont != null) waterGlyph.font = glyphFont;
            sealRoot.gameObject.SetActive(true); sealRoot.SetAsLastSibling();
            waterGlyph.gameObject.SetActive(false);
            foreach (var part in new UnityEngine.UI.Graphic[] { waterfallBack, waterfall, splash }) part.gameObject.SetActive(false);
            // The lit icons in the row take off from where they are.
            flyCount = 0;
            for (int i = 0; i < talismanIcons.Length && flyCount < flyers.Length; i++)
            {
                var icon = talismanIcons[i];
                if (icon == null || !icon.gameObject.activeInHierarchy) continue;
                flyFrom[flyCount] = icon.rectTransform.position;
                flyers[flyCount].rectTransform.sizeDelta = icon.rectTransform.rect.size;
                flyCount++;
            }
            if (talismanRow != null) talismanRow.gameObject.SetActive(false);
        }

        /// <summary>The talisman's outline filled white (laid over it so it shines).</summary>
        private static Sprite TalismanShine()
        {
            if (shineSprite != null) return shineSprite;
            Texture2D source = TalismanIcon().texture;
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
            { name = "Jigwi talisman shine (#595 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            Color[] pixels = source.GetPixels();
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(1f, 1f, 1f, pixels[i].a);
            texture.SetPixels(pixels); texture.Apply(false, true);
            shineSprite = Sprite.Create(texture, new Rect(0f, 0f, source.width, source.height), new Vector2(.5f, .5f), 64f);
            shineSprite.hideFlags = HideFlags.DontSave;
            return shineSprite;
        }

        /// <summary>A four-pointed twinkle star.</summary>
        private static Sprite Sparkle()
        {
            if (sparkleSprite != null) return sparkleSprite;
            const int s = 48;
            var texture = new Texture2D(s, s, TextureFormat.RGBA32, false)
            { name = "Jigwi talisman sparkle (#595 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float u = Mathf.Abs((x + .5f) / s * 2f - 1f), v = Mathf.Abs((y + .5f) / s * 2f - 1f);
                    float star = Mathf.Clamp01(1f - Mathf.Sqrt(u) - Mathf.Sqrt(v) + .35f) * 1.6f;
                    float core = Mathf.Clamp01(1f - new Vector2(u, v).magnitude * 3f);
                    pixels[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(star + core));
                }
            texture.SetPixels(pixels); texture.Apply(false, true);
            sparkleSprite = Sprite.Create(texture, new Rect(0f, 0f, s, s), new Vector2(.5f, .5f), s);
            sparkleSprite.hideFlags = HideFlags.DontSave;
            return sparkleSprite;
        }

        private UnityEngine.UI.RawImage NewRaw(string name)
        {
            var raw = new GameObject(name, typeof(RectTransform)).AddComponent<UnityEngine.UI.RawImage>();
            raw.gameObject.layer = gameObject.layer; raw.raycastTarget = false; raw.texture = WaterfallTexture();
            raw.transform.SetParent(sealRoot, false);
            return raw;
        }

        private UnityEngine.UI.Image NewSealImage(string name, Sprite sprite)
        {
            var image = new GameObject(name, typeof(RectTransform)).AddComponent<UnityEngine.UI.Image>();
            image.gameObject.layer = gameObject.layer; image.raycastTarget = false; image.sprite = sprite;
            image.transform.SetParent(sealRoot, false);
            return image;
        }

        /// <summary>
        /// Falling water like the talisman orb's tail: a dark ink edge, the blue body, and light highlight streaks
        /// (tiles vertically for the rush).
        /// </summary>
        private static Texture2D WaterfallTexture()
        {
            if (waterfallTexture != null) return waterfallTexture;
            const int w = 96, h = 192;
            waterfallTexture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { name = "Jigwi talisman waterfall (#595 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color[w * h];
            Color ink = new Color(.015f, .105f, .2f, .96f), body = new Color(.12f, .64f, .98f, .98f), light = new Color(.72f, .97f, 1f, .95f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + .5f) / w, v = (y + .5f) / h;
                    // The edge wobbles down the column so it reads as flowing water, not a bar.
                    float edge = .43f + .035f * Mathf.Sin(v * Mathf.PI * 6f) + .02f * Mathf.Sin(v * Mathf.PI * 14f + 1.3f);
                    float d = Mathf.Abs(u - .5f);
                    Color c;
                    if (d > edge + .05f) c = Color.clear;
                    else if (d > edge) c = ink;
                    else
                    {
                        float streak = Mathf.Pow(.5f + .5f * Mathf.Sin(u * 40f + Mathf.Sin(v * Mathf.PI * 4f) * 2f), 6f);
                        float shine = Mathf.Clamp01(1f - Mathf.Abs(u - .36f) / .07f) * (.6f + .4f * Mathf.Sin(v * Mathf.PI * 8f));
                        c = Color.Lerp(body, light, Mathf.Clamp01(streak * .7f + shine));
                    }
                    pixels[y * w + x] = c;
                }
            waterfallTexture.SetPixels(pixels); waterfallTexture.Apply(false, true);
            return waterfallTexture;
        }

        private static Sprite Drop()
        {
            if (dropSprite != null) return dropSprite;
            const int s = 32;
            var texture = new Texture2D(s, s, TextureFormat.RGBA32, false)
            { name = "Jigwi waterfall drop (#595 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = new Vector2(x + .5f - s * .5f, y + .5f - s * .5f).magnitude / (s * .5f);
                    pixels[y * s + x] = d > 1f ? Color.clear : d > .78f ? new Color(.015f, .105f, .2f, .9f)
                        : d < .35f && x < s / 2 && y > s / 2 ? Color.white : new Color(.45f, .8f, 1f, 1f);
                }
            texture.SetPixels(pixels); texture.Apply(false, true);
            dropSprite = Sprite.Create(texture, new Rect(0f, 0f, s, s), new Vector2(.5f, .5f), s);
            dropSprite.hideFlags = HideFlags.DontSave;
            return dropSprite;
        }

        private void PlayPour()
        {
            // #595: its own long, grand waterfall (built from the bucket pour); the bucket pour if it is missing.
            AudioClip[] clips = Resources.LoadAll<AudioClip>("Audio/JigwiTalismanWaterfall");
            if (clips == null || clips.Length == 0) clips = Resources.LoadAll<AudioClip>("Audio/JigwiBucketPour");
            if (clips == null || clips.Length == 0) return;
            if (sealAudio == null) { sealAudio = gameObject.AddComponent<AudioSource>(); sealAudio.playOnAwake = false; sealAudio.spatialBlend = 0f; }
            sealAudio.PlayOneShot(clips[0], LabAudioSettings.SfxVolume);
        }

        // ---------- #595 미리보기(에디터 메뉴 C6 Lab/Jigwi): 혼자서도 N명이 차례로 부적을 쓰고 모두 완성되는 장면을 본다(보기만, Host 칠 없음) ----------
        private LabJigwiABRound preview;
        private double previewStart = -1d;
        /// <summary>지귀 전투 중에 부르면 <paramref name="players"/>명이 0.6초 간격으로 부적을 쓰고 완성 연출까지 보여 준다.</summary>
        public void PreviewCommonTalisman(int players)
        {
            players = Mathf.Clamp(players, 1, LabJigwiABNetworkValidation.MaximumPlayers);
            previewStart = -1d;
            preview = new LabJigwiABRound
            {
                id = "preview-" + Time.frameCount, kind = LabJigwiABKind.PersonalBurn, common = true,
                players = new LabJigwiABPlayerFire[players], resolvedAt = -1d
            };
            for (int i = 0; i < players; i++) preview.players[i] = new LabJigwiABPlayerFire { playerId = 900ul + (ulong)i, talismanAt = -1d };
        }

        // 미리보기의 가짜 작은 지도(사람 수만큼 자리): 진짜 지도 자리에 덮어 그려 도장이 찍히는 모습을 본다.
        private RectTransform previewMap;
        private readonly RectTransform[] previewSeats = new RectTransform[LabJigwiABNetworkValidation.MaximumPlayers];
        private static readonly Vector2[][] PreviewSeatPoints =
        {
            new[] { new Vector2(0, 0) },
            new[] { new Vector2(0, -20), new Vector2(0, 20) },
            new[] { new Vector2(0, -23), new Vector2(-26, 24), new Vector2(26, 24) },
            new[] { new Vector2(0, -23), new Vector2(-27, 0), new Vector2(0, 24), new Vector2(27, 0) },
            new[] { new Vector2(1, -23), new Vector2(-24, -5), new Vector2(-17, 23), new Vector2(19, 23), new Vector2(24, -5) },
        };

        private void DrawPreviewMap(LabJigwiABRound round)
        {
            int n = round != null ? round.players.Length : 0;
            if (n == 0) { if (previewMap != null && previewMap.gameObject.activeSelf) previewMap.gameObject.SetActive(false); return; }
            if (minimap == null) minimap = FindFirstObjectByType<LabDrumMinimap>();
            if (previewMap == null)
            {
                previewMap = new GameObject("Talisman Preview Map", typeof(RectTransform)).GetComponent<RectTransform>();
                previewMap.gameObject.layer = gameObject.layer; previewMap.SetParent(transform, false);
                var back = previewMap.gameObject.AddComponent<UnityEngine.UI.Image>();
                back.raycastTarget = false; back.color = new Color(.973f, .914f, .804f, .96f);
                for (int i = 0; i < previewSeats.Length; i++)
                {
                    var seat = new GameObject("Preview Seat P" + (i + 1), typeof(RectTransform)).AddComponent<UnityEngine.UI.Image>();
                    seat.gameObject.layer = gameObject.layer; seat.raycastTarget = false; seat.transform.SetParent(previewMap, false);
                    seat.rectTransform.sizeDelta = Vector2.one * 24f;
                    if (minimap != null && minimap.playerSprites != null && i < minimap.playerSprites.Length) seat.sprite = minimap.playerSprites[i];
                    else seat.color = new Color(.851f, .824f, .773f, 1f);
                    previewSeats[i] = seat.rectTransform;
                }
            }
            previewMap.gameObject.SetActive(true); previewMap.SetAsLastSibling();
            RectTransform real = minimap != null ? minimap.Panel : null;
            previewMap.anchorMin = previewMap.anchorMax = previewMap.pivot = new Vector2(.5f, .5f);
            if (real != null && real.gameObject.activeInHierarchy)
            {
                previewMap.position = real.TransformPoint(real.rect.center);
                previewMap.sizeDelta = real.rect.size * real.lossyScale.x / Mathf.Max(.0001f, previewMap.lossyScale.x);
            }
            else
            {
                var rect = transform as RectTransform;
                previewMap.anchoredPosition = rect != null ? new Vector2(rect.rect.width * .36f, rect.rect.height * .32f) : Vector2.zero;
                previewMap.sizeDelta = new Vector2(128f, 116f);
            }
            float k = previewMap.sizeDelta.x / 128f;
            stampsUsed.Clear(); StampsShown = StampsWaiting = 0;
            for (int i = 0; i < previewSeats.Length; i++)
            {
                bool on = i < n;
                if (previewSeats[i].gameObject.activeSelf != on) previewSeats[i].gameObject.SetActive(on);
                if (!on) continue;
                previewSeats[i].anchoredPosition = PreviewSeatPoints[n - 1][i] * k;
                previewSeats[i].sizeDelta = Vector2.one * 24f * k;
                Stamp(previewSeats[i], round.players[i].talismanAt >= 0d);
            }
            HideUnusedStamps();
        }

        private void HideUnusedStamps()
        {
            foreach (var pair in stamps)
                if (!stampsUsed.Contains(pair.Key) && pair.Value != null && pair.Value.gameObject.activeSelf) pair.Value.gameObject.SetActive(false);
        }

        /// <summary>미리보기 회차를 지금 시각에 맞춰 진행시키고 돌려준다(없으면 null).</summary>
        private LabJigwiABRound AdvancePreview(double now)
        {
            if (preview == null) return null;
            if (previewStart < 0d) { previewStart = now; preview.warningAt = now - 2d; preview.activeAt = now; preview.endsAt = now + 10d; }
            int done = 0;
            for (int i = 0; i < preview.players.Length; i++)
            {
                double at = previewStart + .8d + .6d * i;
                if (now >= at && preview.players[i].talismanAt < 0d) preview.players[i].talismanAt = at;
                if (preview.players[i].talismanAt >= 0d) done++;
            }
            if (done == preview.players.Length && preview.resolution == LabJigwiABResolution.None)
            { preview.resolution = LabJigwiABResolution.Completed; preview.resolvedAt = now; }
            if (preview.resolvedAt >= 0d && now > preview.resolvedAt + 3d) { preview = null; return null; }
            return preview;
        }

        /// <summary>The boss's centre on this canvas (the middle of the upper screen when it cannot be projected).</summary>
        private Vector3 BossPoint()
        {
            var rect = transform as RectTransform;
            Vector3 fallback = rect != null ? rect.TransformPoint(new Vector3(0f, rect.rect.height * .2f, 0f)) : transform.position;
            if (source == null || source.target == null || battleCamera == null || rect == null) return fallback;
            Vector3 screen = battleCamera.WorldToScreenPoint(source.target.transform.position);
            if (screen.z <= 0f) return fallback;
            Canvas root = canvas != null ? canvas.rootCanvas : GetComponentInParent<Canvas>();
            Camera eye = root == null || root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            return RectTransformUtility.ScreenPointToWorldPointInRectangle(rect, screen, eye, out Vector3 world) ? world : fallback;
        }

        private void PlaySeal()
        {
            AudioClip[] clips = Resources.LoadAll<AudioClip>("Audio/JigwiTalismanSplash");
            if (clips == null || clips.Length == 0) return;
            if (sealAudio == null) { sealAudio = gameObject.AddComponent<AudioSource>(); sealAudio.playOnAwake = false; sealAudio.spatialBlend = 0f; }
            sealAudio.PlayOneShot(clips[0], LabAudioSettings.SfxVolume);
        }

        /// <summary>작은 부적: 노란 종이, 붉은 테두리, 가운데 붉은 획, 아래 붉은 도장.</summary>
        private static Sprite TalismanIcon()
        {
            if (talismanIconSprite != null) return talismanIconSprite;
            const int w = 36, h = 64;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { name = "Jigwi talisman icon (#595 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool inside = x >= 2 && x < w - 2 && y >= 2 && y < h - 2;
                    bool edge = inside && (x < 5 || x >= w - 5 || y < 5 || y >= h - 5);
                    bool stroke = inside && y > 20 && y < h - 10 && Mathf.Abs(x - (w * .5f + 5f * Mathf.Sin(y * .22f))) < 2.4f;
                    bool bar = inside && Mathf.Abs(y - (h - 16)) < 2f && x > 9 && x < w - 9;
                    bool seal = inside && new Vector2(x + .5f - w * .5f, y + .5f - 12f).magnitude < 5.5f;
                    pixels[y * w + x] = !inside ? Color.clear : edge ? new Color(.55f, .1f, .06f, 1f)
                        : stroke || bar || seal ? new Color(.82f, .13f, .08f, 1f) : new Color(1f, .9f, .55f, 1f);
                }
            texture.SetPixels(pixels); texture.Apply(false, false); // kept readable: TalismanShine copies its outline
            talismanIconSprite = Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(.5f, .5f), 64f);
            talismanIconSprite.hideFlags = HideFlags.DontSave;
            return talismanIconSprite;
        }

        private void PositionGroupStatus(LabJigwiABRound round, ulong owner, double now)
        {
            if (statusPanel == null || fireLayer == null || BattleFireClipRect.height <= 0f) return;
            if (!statusLayoutCached)
            { originalStatusLayout = new PanelLayout(statusPanel.rectTransform); statusLayoutCached = true; }
            float lower = float.PositiveInfinity, upper = float.NegativeInfinity;
            foreach (var player in round.players ?? Array.Empty<LabJigwiABPlayerFire>())
            {
                if (player == null || player.playerId != owner) continue;
                for (int i = 0; player.targets != null && i < player.targets.Length; i++)
                    if (player.targets[i] != null && TryProject(round, player.targets[i], owner, i, now, out Vector2 centre, out Vector2 size))
                    { lower = Mathf.Min(lower, centre.y - size.y * .5f); upper = Mathf.Max(upper, centre.y + size.y * .5f); }
            }
            if (float.IsInfinity(lower)) return;
            float gapAbove = BattleFireClipRect.yMax - upper, gapBelow = lower - BattleFireClipRect.yMin;
            bool above = gapAbove >= gapBelow;
            float gap = Mathf.Max(0f, above ? gapAbove : gapBelow);
            float height = Mathf.Min(Mathf.Max(1f, gap - 8f), Mathf.Min(70f, BattleFireClipRect.height));
            float y = above ? BattleFireClipRect.yMax - height * .5f : BattleFireClipRect.yMin + height * .5f;
            Vector3 world = fireLayer.TransformPoint(new Vector3(BattleFireClipRect.center.x, y, 0f));
            RectTransform panel = statusPanel.rectTransform;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            panel.sizeDelta = new Vector2(BattleFireClipRect.width * .94f, height);
            panel.anchoredPosition = (Vector2)panel.parent.InverseTransformPoint(world);
            groupStatusMoved = true;
        }
        private void RestoreStatusLayout()
        {
            if (!groupStatusMoved || !statusLayoutCached || statusPanel == null) return;
            originalStatusLayout.Restore(statusPanel.rectTransform); groupStatusMoved = false;
        }

        public static string PlayerLabel(LabSnapshot snapshot, ulong owner)
        {
            foreach (var player in snapshot.players ?? Array.Empty<LabPlayerState>())
                if (player != null && player.id == owner) return "P" + (player.seat + 1);
            return "동료";
        }

        private void SetFiresHidden(bool held = false)
        {
            if (audioView != null) audioView.Stop();
            // B's final water hit may finish the pattern before its real trail has faded.
            // Keep only that already-bound owner/event's residue; no new path is introduced.
            if (waterFlightTrail != null) waterFlightTrail.ContinueFading(held);
            CurtainVisualLevel = 0; RemainingIgnitions = 0;
            if (SyncPillar != null) SyncPillar.Hide();
            if (groupFireCurtain != null) groupFireCurtain.gameObject.SetActive(false);
            if (fireImages != null) foreach (var image in fireImages) if (image != null) image.gameObject.SetActive(false);
        }

        public void Hide()
        {
            if (audioView != null) audioView.Stop();
            if (waterFlightTrail != null) waterFlightTrail.Hide();
            VisibleScreenFires = 0;
            RestoreStatusLayout();
            CurtainVisualLevel = 0; RemainingIgnitions = 0;
            if (SyncPillar != null) SyncPillar.Hide();
            if (groupFireCurtain != null) groupFireCurtain.gameObject.SetActive(false);
            if (curtainWaterDouse != null) curtainWaterDouse.gameObject.SetActive(false);
            if (personalHeat != null) personalHeat.gameObject.SetActive(false);
            if (failureOverlay != null) failureOverlay.gameObject.SetActive(false);
            if (statusPanel != null) statusPanel.gameObject.SetActive(false);
            if (talismanRow != null) talismanRow.gameObject.SetActive(false); // #595
            HideStamps();
            if (sealRoot != null) sealRoot.gameObject.SetActive(false);
            TalismanIconsShown = TalismanIconsLit = 0;
            if (personalWaterDouse != null) personalWaterDouse.gameObject.SetActive(false);
            if (fireImages != null) foreach (var image in fireImages) if (image != null) image.gameObject.SetActive(false);
            if (fireWaterDouses != null) foreach (var image in fireWaterDouses) if (image != null) image.gameObject.SetActive(false);
        }
        private void ResetHistory()
        {
            if (audioView != null) audioView.Stop();
            if (waterFlightTrail != null) waterFlightTrail.Hide();
            seenDouses.Clear(); seenFailures.Clear(); baselineReady = false;
            failedAt = -1d; FailureFlashCount = 0;
            personalDousedAt = curtainDousedAt = -1d; previousNow = -1d; sessionId = null; stageStartedAt = -1d;
            for (int i = 0; i < MaxFires; i++) { fireDousedAt[i] = -1d; hasFirePosition[i] = false; }
        }
        private void OnDisable() { Hide(); ResetHistory(); }
        private void OnDestroy() { foreach (Material material in flameMaterials.Values) if (material != null) Destroy(material); }
        private void AnimateFlame(UnityEngine.UI.RawImage image, float time, float seed, float opacity)
        {
            if (image == null) return;
            image.color = new Color(1f, 1f, 1f, opacity);
            if (!flameMaterials.TryGetValue(image, out Material material)) return;
            material.SetFloat("_FlameTime", time); material.SetFloat("_FlameSpeed", flameSpeed);
            material.SetFloat("_Sway", flameSway); material.SetFloat("_Seed", seed);
        }
        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void Centre(RectTransform rect)
        { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = Vector2.one * 90f; }
        private static UnityEngine.UI.Image Image(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); go.transform.SetParent(parent, false);
            var image = go.GetComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false; return image;
        }
        private static UnityEngine.UI.RawImage Raw(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.RawImage)); go.transform.SetParent(parent, false);
            var image = go.GetComponent<UnityEngine.UI.RawImage>(); image.raycastTarget = false; return image;
        }
        private UnityEngine.UI.Text Label(string name, Transform parent, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Text)); go.transform.SetParent(parent, false);
            var text = go.GetComponent<UnityEngine.UI.Text>();
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size; text.resizeTextForBestFit = true; text.resizeTextMinSize = 12; text.resizeTextMaxSize = size;
            text.alignment = TextAnchor.MiddleCenter; text.color = new Color(1f, .97f, .83f); text.raycastTarget = false;
            var outline = go.AddComponent<UnityEngine.UI.Outline>(); outline.effectColor = new Color(.06f, .025f, .01f, .95f);
            outline.effectDistance = new Vector2(1f, -1f);
            return text;
        }
    }
}
