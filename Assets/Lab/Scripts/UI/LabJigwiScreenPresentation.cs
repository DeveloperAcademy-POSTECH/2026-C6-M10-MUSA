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
            DrawTalismanProgress(active, now); // #595: icons, not words
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

        private void DrawTalismanProgress(LabJigwiABRound active, double now)
        {
            int done = 0, total = 0;
            bool show = active != null && active.Active(now) && active.kind == LabJigwiABKind.PersonalBurn && active.common;
            if (show)
                foreach (var seat in active.players ?? Array.Empty<LabJigwiABPlayerFire>())
                    if (seat != null) { total++; if (seat.talismanAt >= 0d) done++; }
            total = Mathf.Min(total, talismanIcons.Length);
            TalismanIconsShown = total; TalismanIconsLit = Mathf.Min(done, total);
            if (total == 0) { if (talismanRow != null && talismanRow.gameObject.activeSelf) talismanRow.gameObject.SetActive(false); return; }
            if (talismanRow == null)
            {
                talismanRow = new GameObject("TalismanProgress", typeof(RectTransform)).GetComponent<RectTransform>();
                talismanRow.gameObject.layer = gameObject.layer;
                talismanRow.SetParent(transform, false);
                // Just above the board, under the boss: where the red alarm flashes while writing.
                talismanRow.anchorMin = new Vector2(.2f, .475f); talismanRow.anchorMax = new Vector2(.8f, .545f);
                talismanRow.offsetMin = talismanRow.offsetMax = Vector2.zero;
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
            float t = Time.unscaledTime;
            for (int i = 0; i < talismanIcons.Length; i++)
            {
                var icon = talismanIcons[i];
                bool on = i < total;
                if (icon.gameObject.activeSelf != on) icon.gameObject.SetActive(on);
                if (!on) { talismanLitAt[i] = -1f; continue; }
                bool lit = i < done;
                if (!lit) talismanLitAt[i] = -1f;
                else if (talismanLitAt[i] < 0f) talismanLitAt[i] = t;
                float x = .5f + (i - (total - 1) * .5f) * .17f;
                var rect = icon.rectTransform;
                rect.anchorMin = new Vector2(x - .075f, 0f); rect.anchorMax = new Vector2(x + .075f, 1f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                float pop = lit ? 1f + .35f * Mathf.Clamp01(1f - (t - talismanLitAt[i]) / .35f) : 1f;
                rect.localScale = Vector3.one * pop;
                icon.color = lit ? Color.white : new Color(.38f, .34f, .32f, .55f);
            }
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
            texture.SetPixels(pixels); texture.Apply(false, true);
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
