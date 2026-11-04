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
        }

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
                failureStatus.text = "실패";
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
            DrawFailure(now);
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
                    || round.resolution != LabJigwiABResolution.Expired) continue;
                string key = round.id + "/" + round.resolvedAt.ToString("R", CultureInfo.InvariantCulture);
                if (!seenFailures.Add(key) || baseline || round.resolvedAt < 0d
                    || now < round.resolvedAt || now - round.resolvedAt >= FailureFlashSeconds) continue;
                failedAt = round.resolvedAt;
                FailureFlashCount++;
            }
        }

        private void DrawFailure(double now)
        {
            if (failureOverlay == null) return;
            bool visible = failedAt >= 0d && now >= failedAt && now - failedAt < FailureFlashSeconds;
            failureOverlay.gameObject.SetActive(visible);
            if (!visible) return;
            float elapsed = (float)(now - failedAt), opacity = 1f - elapsed / (float)FailureFlashSeconds;
            failureOverlay.color = new Color(1f, .13f, .01f, .22f * opacity);
            if (failureFlames != null) for (int i = 0; i < failureFlames.Length; i++)
                AnimateFlame(failureFlames[i], elapsed, i * 1.73f, .95f * opacity);
            if (failureStatus != null) failureStatus.color = new Color(1f, .97f, .83f, opacity);
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
            RemainingIgnitions = slot != null && slot.lit ? Math.Max(1, LabJigwiABTimeline.GroupSyncRequired - round.syncCount) : 0;
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
            string words = string.Empty;
            if (active != null)
            {
                string shout = active.Warning(now) ? (active.kind == LabJigwiABKind.PersonalBurn
                        ? "으아아 마음이 불타오른다!!!\n" : "다 함께 물을 끼얹어라!\n") : string.Empty;
                // #479: A has no hit count or target text; only the warning shout remains.
                if (active.kind == LabJigwiABKind.PersonalBurn) words = shout.TrimEnd('\n');
                else
                {
                    // #509: who is picked, and what this screen should do.
                    var picked = new StringBuilder();
                    bool mine = false;
                    foreach (var player in active.players ?? Array.Empty<LabJigwiABPlayerFire>())
                    {
                        if (player == null) continue;
                        if (picked.Length > 0) picked.Append('·');
                        picked.Append(PlayerLabel(snapshot, player.playerId));
                        mine |= player.playerId == local;
                    }
                    words = shout + "지목 " + picked + " · 1초 안에 함께 박 맞히기 "
                        + active.syncCount + "/" + LabJigwiABTimeline.GroupSyncRequired + " · "
                        + Math.Max(0d, active.endsAt - Math.Max(now, active.activeAt)).ToString("0.0", CultureInfo.InvariantCulture) + "초";
                    rows.Append(mine ? "물 구슬로 박을 맞혀요 · 지목된 사람과 동시에"
                        : "물 구슬을 " + picked + "에게 굴려 주세요");
                }
            }
            if (active == null && !stage.fever)
                foreach (var finished in stage.rounds ?? Array.Empty<LabJigwiABRound>())
                    if (finished != null && finished.kind == LabJigwiABKind.GroupFire
                        && finished.resolution == LabJigwiABResolution.Completed && Recent(finished.resolvedAt, now))
                        words = "모든 불을 함께 껐어요!";
            LabJigwiABRound reward = stage.Reward(now);
            if (reward != null)
            {
                string remaining = Math.Max(0d, reward.rewardUntil - now).ToString("0.0", CultureInfo.InvariantCulture);
                words = words.Length > 0 ? words + "\n성공 보상 · " + remaining + "초"
                    : "모든 불을 함께 껐어요! · 보상 " + remaining + "초";
                if (rows.Length > 0) rows.Append('\n');
                rows.Append("투척 크기 ").Append(reward.rewardSizeScale.ToString("0.#", CultureInfo.InvariantCulture))
                    .Append("배 · 도색 ").Append(reward.rewardPaintScale.ToString("0.#", CultureInfo.InvariantCulture))
                    .Append("배 · 불꽃 궤적");
            }
            foreach (var upcoming in stage.rounds ?? Array.Empty<LabJigwiABRound>())
            {
                if (upcoming == null || upcoming == active || !upcoming.Warning(now)) continue;
                words += "\n다음 기믹 예고 · " + (upcoming.kind == LabJigwiABKind.PersonalBurn ? "부적 쓰기" : "동시 소화");
                break;
            }
            status.text = words; playerStatus.text = rows.ToString();
            statusPanel.gameObject.SetActive(words.Length > 0);
            playerStatus.gameObject.SetActive(rows.Length > 0);
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
