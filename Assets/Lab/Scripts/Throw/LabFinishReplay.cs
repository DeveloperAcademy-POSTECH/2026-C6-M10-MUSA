using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// Presentation-only replay of the final hit, shown three times from different angles before a story result. It
    /// draws the live, completed gourd at the Host's recorded poses (render-only, see
    /// <see cref="LabHangingTarget.SetPresentationPose"/>), a physics-free ghost orb on the recorded flight, and its own
    /// camera with letterbox bars over a hidden battle HUD. It never changes global time, hits, paint, stats, the
    /// battle camera's pose or any network state. A new tap skips it on this device only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabFinishReplay : MonoBehaviour
    {
        public const int OverlaySortingOrder = 29000;
        public const float LetterboxShare = .1f;
        public const float LetterboxSeconds = .25f;
        /// <summary>A tap this soon after the start is not a skip (a finger landing with the final throw).</summary>
        public const float SkipGraceSeconds = .15f;
        private const float MinimumFloorHeight = .25f;

        public LabNetwork Network { get; private set; }
        public LabFinishReplayShot Shot { get; private set; }
        public bool Running { get; private set; }
        public bool Complete { get; private set; }
        public bool Skipped { get; private set; }
        public float Elapsed { get; private set; }
        public int CurrentPass { get; private set; } = -1;
        public float ReplayTime { get; private set; }
        public int ImpactsPresented { get; private set; }
        public static float Duration => LabFinishReplayTimeline.Duration;
        public float Remaining => Mathf.Max(0f, Duration - Elapsed);
        public Camera ReplayCamera => replayCamera;
        public LabProjectile Ghost => ghost;
        public Canvas Overlay => overlay;
        public LabFiveElementVfx ClayVfx => replayVfx;

        private readonly List<Canvas> hiddenCanvases = new List<Canvas>();
        private readonly List<Camera> disabledCameras = new List<Camera>();
        private readonly List<LabImpactParticleBurst> bursts = new List<LabImpactParticleBurst>();
        private LabImpactParticleBurst burstPrefab;
        private LabHangingTarget hanging;
        private Camera replayCamera;
        private Canvas overlay;
        private RectTransform topBar, bottomBar;
        private Image flash;
        private CanvasGroup captionGroup;
        private Transform effectRoot;
        private LabFiveElementVfx replayVfx;
        private Transform impactAnchor;
        private LabProjectile ghost;
        private bool ghostDone;
        private LabThrowSound throwSound;
        private LabHangingTarget.RigState impactPose;
        private Vector3 gourdAtImpact, viewer, travel, side, normal;
        private float bodyRadius;
        private float previousReplayTime = float.NegativeInfinity;
        private float sampledFlightTime;
        private float impactElapsed = -1f;
        private string roundKey;

        public static LabFinishReplay Attach(LabNetwork network)
        {
            if (network == null || network.target == null) return null;
            var value = network.target.GetComponent<LabFinishReplay>();
            if (value == null) value = network.target.gameObject.AddComponent<LabFinishReplay>();
            value.Network = network;
            return value;
        }

        /// <summary>Starts the replay. False (nothing changed) when the record or the battle references are missing.</summary>
        public bool Begin(LabFinishReplayShot shot)
        {
            if (Running || Complete) return Running;
            if (shot == null || !shot.IsValid() || Network == null || Network.target == null || Network.config == null
                || Network.target.Hitbox == null) return false;
            hanging = Network.target.GetComponent<LabHangingTarget>();
            Camera battle = Network.BattleCamera;
            if (hanging == null || battle == null) return false;
            Shot = shot;
            roundKey = RoundKey();
            impactPose = shot.PoseAt(0f);
            gourdAtImpact = hanging.MovePosedPoint(Network.target.Hitbox.bounds.center, hanging.FixedPose, impactPose);
            Vector3 extents = Network.target.Hitbox.bounds.extents;
            bodyRadius = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z));
            viewer = battle.transform.position;
            Vector3 incoming = shot.ImpactVelocity.sqrMagnitude > 1e-6f ? shot.ImpactVelocity : shot.HitPoint - shot.LaunchPosition;
            travel = Horizontal(incoming, Horizontal(gourdAtImpact - shot.LaunchPosition, Vector3.forward));
            side = Vector3.Cross(Vector3.up, travel).normalized;
            if (Vector3.Dot(side, viewer - gourdAtImpact) < 0f) side = -side;
            normal = shot.OutwardNormal;
            throwSound = FindAnyObjectByType<LabThrowSound>();
            LabImpactParticleVfx impacts = FindAnyObjectByType<LabImpactParticleVfx>();
            burstPrefab = impacts != null ? impacts.EffectPrefab : null;

            BuildCamera(battle);
            HideBattleView(battle);
            BuildOverlay();
            effectRoot = new GameObject("Final hit replay · effects").transform;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(effectRoot.gameObject, gameObject.scene);
            replayVfx = LabFiveElementVfx.CreateManual(effectRoot);
            Running = true;
            Complete = Skipped = false;
            Elapsed = 0f;
            CurrentPass = -1;
            ImpactsPresented = 0;
            previousReplayTime = float.NegativeInfinity;
            Sample(0d);
            return true;
        }

        /// <summary>Shows the frame for the shared elapsed time since the common start.</summary>
        public void Sample(double sharedElapsed)
        {
            if (!Running || Shot == null) return;
            Elapsed = Mathf.Clamp((float)sharedElapsed, 0f, Duration);
            int pass = LabFinishReplayTimeline.Evaluate(Elapsed, Shot.FlightSeconds, out float time, out float passElapsed);
            if (pass != CurrentPass || time < previousReplayTime)
            {
                StartPass(pass);
                previousReplayTime = float.NegativeInfinity;
            }
            ReplayTime = time;
            LabHangingTarget.RigState pose = Shot.PoseAt(time);
            hanging.SetPresentationPose(pose);
            UpdateImpactAnchor(pose);
            UpdateGhost(time);
            if (previousReplayTime < 0f && time >= 0f)
            {
                // Seed contact orientation at the exact impact, even when this device samples after it.
                LabFinishReplayTimeline.Pass timing = LabFinishReplayTimeline.Passes[pass].Fitted(Shot.FlightSeconds);
                UpdateImpactAnchor(impactPose);
                PlaceCamera(pass, 0f, -timing.From / timing.Speed, impactPose);
                PresentImpact(pass, impactPose);
                UpdateImpactAnchor(pose);
            }
            // Clay follows the shared replay clock, including a late first sample after the contact.
            if (time >= 0f && replayVfx != null)
                replayVfx.Advance(Mathf.Max(0f, time - Mathf.Max(0f, previousReplayTime)));
            previousReplayTime = time;
            PlaceCamera(pass, time, passElapsed, pose);
            UpdateOverlay(pass, passElapsed);
            bursts.RemoveAll(burst => burst == null);
            foreach (LabImpactParticleBurst burst in bursts) burst.FaceCamera(replayCamera);
            if (Elapsed >= Duration) { Complete = true; Running = false; }
        }

        /// <summary>This device only: stop here and keep the last frame until the result takes over.</summary>
        public void Skip()
        {
            if (Complete) return;
            Skipped = Complete = true;
            Running = false;
        }

        /// <summary>Restores the battle cameras, HUD and gourd drawing and removes everything the replay made.</summary>
        public void ResetPresentation()
        {
            if (hanging != null) hanging.ClearPresentationPose();
            foreach (Camera camera in disabledCameras) if (camera != null) camera.enabled = true;
            foreach (Canvas canvas in hiddenCanvases) if (canvas != null) canvas.enabled = true;
            disabledCameras.Clear();
            hiddenCanvases.Clear();
            if (ghost != null) Destroy(ghost.gameObject);
            if (effectRoot != null) { effectRoot.gameObject.SetActive(false); Destroy(effectRoot.gameObject); }
            bursts.Clear();
            if (replayCamera != null) { replayCamera.enabled = false; Destroy(replayCamera.gameObject); }
            if (overlay != null) { overlay.gameObject.SetActive(false); Destroy(overlay.gameObject); }
            ghost = null; effectRoot = null; replayCamera = null; overlay = null;
            replayVfx = null; impactAnchor = null;
            topBar = bottomBar = null; flash = null; captionGroup = null;
            Shot = null; Running = Complete = Skipped = false; Elapsed = 0f; CurrentPass = -1; ImpactsPresented = 0;
            impactElapsed = -1f; ghostDone = false; roundKey = null;
        }

        private void Update()
        {
            if (Shot == null) return;
            if (RoundKey() != roundKey || Network.Snapshot == null || Network.Snapshot.phase != LabPhase.Victory)
            {
                ResetPresentation();
                return;
            }
            if (Running && Elapsed >= SkipGraceSeconds && NewPress()) Skip();
        }

        private static bool NewPress()
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
            if (Touchscreen.current == null) return false;
            foreach (var touch in Touchscreen.current.touches)
                if (touch.press.wasPressedThisFrame) return true;
            return false;
        }

        private string RoundKey() => Network != null && Network.Snapshot != null
            ? Network.Snapshot.sessionId + "/" + Network.Snapshot.round : null;

        private void StartPass(int pass)
        {
            CurrentPass = pass;
            if (ghost != null) Destroy(ghost.gameObject);
            ghost = null;
            ghostDone = false;
            impactElapsed = -1f;
            sampledFlightTime = -Shot.FlightSeconds;
            replayVfx?.ClearAll();
            // A cut never carries the last pass's fading trails or bursts into the new angle.
            if (effectRoot != null)
                for (int i = effectRoot.childCount - 1; i >= 0; i--)
                {
                    GameObject child = effectRoot.GetChild(i).gameObject;
                    if (replayVfx != null && child == replayVfx.gameObject) continue;
                    child.SetActive(false);
                    Destroy(child);
                }
            bursts.Clear();
            impactAnchor = new GameObject("Final hit replay · contact pose").transform;
            impactAnchor.SetParent(effectRoot, false);
        }

        private void UpdateGhost(float time)
        {
            if (ghostDone || !Shot.Launched(time)) return;
            if (ghost == null)
            {
                // Rebuild the short travelled tail from the recorded launch even when a cut opens halfway through a flight.
                ghost = LabProjectile.Create(Shot.LaunchPosition, Network.config, effectRoot, Shot.Element, Shot.BlastScale,
                    useClayFlight: true, fiveColor: Shot.FiveColor);
                ghost.name = "Final hit replay · ghost orb";
                ghost.SetRadiusScale(Shot.RadiusScale);
                ghost.SetJigwiABRadiusScale(Shot.JigwiRadiusScale);
                ghost.BeginPuppet(Network.config, Shot.JigwiReward, replayVfx, Shot.LaunchVelocity);
                // Realtime sprite tinting would overwrite the clay pool's sampled palette clock.
                if (Shot.FiveColor && !ghost.ClayFlightActive)
                    LabFiveColorSparkle.AttachArt(ghost.transform,
                        Network.config.OrbRadius * LabOrbSpriteArt.SizeScaleFor(LabOrbKind.Combined), true);
            }
            float until = Mathf.Min(time, 0f);
            while (sampledFlightTime < until)
            {
                float next = Mathf.Min(until, sampledFlightTime + LabFinishReplayShot.StepSeconds);
                Vector3 from = Shot.BallAt(sampledFlightTime), to = Shot.BallAt(next);
                ghost.SamplePuppet(to, to - from);
                replayVfx?.Advance(next - sampledFlightTime);
                sampledFlightTime = next;
            }
            ghost.TryTintClayFlight(Mathf.Max(0f, until + Shot.FlightSeconds), LabFiveColorSparkle.FiveColors);
            if (time >= 0f)
            {
                ghost.EndPuppet();
                ghost = null;
                ghostDone = true;
            }
        }

        private void UpdateImpactAnchor(LabHangingTarget.RigState pose)
        {
            if (impactAnchor == null) return;
            Matrix4x4 frame = hanging.PoseMatrix(pose);
            impactAnchor.SetPositionAndRotation(frame.MultiplyPoint3x4(Vector3.zero), frame.rotation);
            impactAnchor.localScale = frame.lossyScale;
        }

        private void PresentImpact(int pass, LabHangingTarget.RigState pose)
        {
            ImpactsPresented++;
            impactElapsed = Elapsed;
            Vector3 point = hanging.MovePosedPoint(Shot.HitPoint, impactPose, pose);
            float speed = LabFinishReplayTimeline.Passes[pass].Fitted(Shot.FlightSeconds).Speed;
            Matrix4x4 moved = hanging.PoseMatrix(pose) * hanging.PoseMatrix(impactPose).inverse;
            Vector3 posedNormal = moved.inverse.transpose.MultiplyVector(normal).normalized;
            Vector3 incoming = moved.MultiplyVector(Shot.ImpactVelocity);
            bool clay = replayVfx != null && replayVfx.PlayImpact(Shot.Element, point, posedNormal, incoming,
                2f * Shot.Radius, LabPaintImpact.Normalize(Shot.ImpactVelocity, normal), impactAnchor,
                leaveMark: false, fiveColor: Shot.FiveColor, fiveColorAge: Shot.FlightSeconds);
            // Five-color uses the pool's plain-pigment clay fallback if wet shading is unavailable.
            // If neither shader can render, keep sound/transition without resurrecting the legacy dust and stars.
            if (!clay && !Shot.FiveColor)
            {
                if (!PlayAuthoredBurst(point, speed)) Burst(point, speed);
            }
            throwSound?.PlayHit(0f, LabThrowSound.IsSmallThrow(Shot.LaunchScale),
                LabThrowSound.IsBigThrow(Shot.LaunchScale) && !Shot.FiveColor);
        }

        /// <summary>The game's own hit burst, as its own copy slowed to this pass's speed, facing the replay camera.</summary>
        private bool PlayAuthoredBurst(Vector3 point, float speed)
        {
            if (burstPrefab == null || replayCamera == null) return false;
            Vector3 toward = replayCamera.transform.position - point;
            LabImpactParticleBurst burst = Instantiate(burstPrefab, effectRoot);
            burst.name = "Final hit replay · impact burst";
            burst.transform.SetPositionAndRotation(point + (toward.sqrMagnitude > 1e-4f ? toward.normalized * .04f : Vector3.zero),
                Quaternion.identity);
            burst.gameObject.SetActive(true);
            foreach (ParticleSystem system in burst.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = system.main;
                main.useUnscaledTime = true;
                main.simulationSpeed = Mathf.Max(.05f, speed);
            }
            Color color = Shot.FiveColor ? new Color(1f, .88f, .45f, 1f) : LabOrbVisualFactory.TemporaryElementColor(Shot.Element);
            if (!burst.Begin(color, Shot.ImpactVelocity, replayCamera)) { Destroy(burst.gameObject); return false; }
            bursts.Add(burst);
            Destroy(burst.gameObject, burst.MaxLifetimeSeconds / Mathf.Max(.05f, speed) + .5f);
            return true;
        }

        private void Burst(Vector3 point, float speed)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return;
            Color color = Shot.FiveColor ? new Color(1f, .88f, .45f, 1f) : LabOrbVisualFactory.TemporaryElementColor(Shot.Element);
            color.a = 1f;
            var material = new Material(shader) { hideFlags = HideFlags.DontSave };
            material.SetColor("_BaseColor", color);
            var root = new GameObject("Final hit replay · impact burst");
            root.transform.SetParent(effectRoot, false);
            root.transform.SetPositionAndRotation(point, Quaternion.LookRotation(normal));
            var burst = root.AddComponent<ParticleSystem>();
            burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = burst.main;
            main.loop = false; main.playOnAwake = false; main.useUnscaledTime = true;
            main.simulationSpeed = speed; main.startLifetime = .45f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f); main.startSize = new ParticleSystem.MinMaxCurve(.03f, .08f);
            main.maxParticles = 32; main.startColor = color; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = .4f;
            var emission = burst.emission; emission.enabled = false;
            var shape = burst.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 55f; shape.radius = .04f;
            var size = burst.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var particles = root.GetComponent<ParticleSystemRenderer>();
            particles.sharedMaterial = material;
            root.AddComponent<LabFinishReplayOwnedMaterial>().Material = material;
            burst.Play();
            burst.Emit(32);
        }

        private void PlaceCamera(int pass, float time, float passElapsed, LabHangingTarget.RigState pose)
        {
            if (replayCamera == null) return;
            var timing = LabFinishReplayTimeline.Passes[pass];
            float progress = Mathf.Clamp01(passElapsed / timing.RealSeconds);
            float ease = progress * progress * (3f - 2f * progress);
            Vector3 hit = hanging.MovePosedPoint(Shot.HitPoint, impactPose, pose);
            Vector3 gourd = hanging.MovePosedPoint(gourdAtImpact, impactPose, pose);
            Vector3 ball = Shot.BallAt(Mathf.Min(time, 0f));
            Vector3 position, look;
            float across;
            if (pass == 0)
            {
                // Over the thrower's shoulder, easing in behind the orb.
                Vector3 forward = Horizontal(Shot.HitPoint - Shot.LaunchPosition, travel);
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                position = Shot.LaunchPosition - forward * 1.4f + Vector3.up * .55f + right * .25f + forward * (.6f * ease);
                look = Vector3.Lerp(ball, hit, Mathf.Max(.35f, Smooth(-.25f, .05f, time)));
                across = Mathf.Lerp(36f, 30f, progress);
            }
            else if (pass == 1)
            {
                // From the side, far enough that the orb and the gourd share the frame.
                LabFinishReplayTimeline.Pass fitted = timing.Fitted(Shot.FlightSeconds);
                Vector3 middle = Vector3.Lerp(Shot.BallAt(Mathf.Min(fitted.From, 0f)), hit, .5f);
                position = middle + side * 4.2f + Vector3.up * .35f + travel * (.5f * ease);
                look = Vector3.Lerp(ball, hit, .6f);
                across = 40f;
            }
            else
            {
                // Close on the impact, slowly circling in while the gourd swings away.
                Vector3 outward = Horizontal(normal + Vector3.up * .2f, -travel);
                outward = (outward + Vector3.up * .15f).normalized;
                float turn = Vector3.Dot(Vector3.Cross(Vector3.up, outward), side) >= 0f ? 1f : -1f;
                float settle = Mathf.Clamp01(passElapsed / (timing.RealSeconds + LabFinishReplayTimeline.TransitionSeconds));
                settle = settle * settle * (3f - 2f * settle);
                Vector3 direction = Quaternion.AngleAxis(turn * Mathf.Lerp(30f, 12f, settle), Vector3.up) * outward;
                position = hit + direction * Mathf.Lerp(2.7f, 2.0f, settle) + Vector3.up * .15f;
                look = Vector3.Lerp(hit, gourd, .3f);
                across = Mathf.Lerp(30f, 24f, settle);
            }
            float fov = VerticalFieldOfView(across, replayCamera.aspect);
            if (time >= 0f) look = Vector3.Lerp(look, Vector3.Lerp(hit, gourd, .35f), Smooth(0f, .3f, time));
            // Never inside the gourd, never under the floor.
            Vector3 away = position - gourd;
            float clearance = bodyRadius + .45f;
            if (away.magnitude < clearance) position = gourd + (away.sqrMagnitude > 1e-6f ? away.normalized : normal) * clearance;
            position.y = Mathf.Max(position.y, MinimumFloorHeight);
            Quaternion rotation = Quaternion.LookRotation((look - position).sqrMagnitude > 1e-6f ? look - position : travel, Vector3.up);
            if (pass == 2 && impactElapsed >= 0f)
            {
                // A short shake on the close-up's impact, on the replay camera only.
                float since = Elapsed - impactElapsed, amplitude = .035f * Mathf.Exp(-since * 8f);
                position += rotation * new Vector3(Mathf.Sin(since * 53f), Mathf.Sin(since * 71f + 1f), 0f) * amplitude;
            }
            replayCamera.transform.SetPositionAndRotation(position, rotation);
            replayCamera.fieldOfView = fov;
        }

        private void UpdateOverlay(int pass, float passElapsed)
        {
            if (overlay == null) return;
            float bars = Mathf.Clamp01(Elapsed / LetterboxSeconds);
            bars = bars * bars * (3f - 2f * bars);
            float height = ((RectTransform)overlay.transform).rect.height * LetterboxShare * bars;
            topBar.sizeDelta = new Vector2(0f, height);
            bottomBar.sizeDelta = new Vector2(0f, height);
            float cut = pass > 0 ? 1f - Mathf.Clamp01(passElapsed / LabFinishReplayTimeline.CutFlashSeconds) : 0f;
            flash.color = new Color(1f, 1f, 1f, .35f * cut);
            flash.enabled = cut > 0f;
            captionGroup.alpha = Mathf.Clamp01((Elapsed - .3f) / .3f);
        }

        private void BuildCamera(Camera battle)
        {
            var root = new GameObject("Final hit replay · camera");
            root.tag = "MainCamera";
            replayCamera = root.AddComponent<Camera>();
            replayCamera.CopyFrom(battle);
            replayCamera.rect = new Rect(0f, 0f, 1f, 1f);
            replayCamera.depth = Mathf.Max(battle.depth, 10f) + 40f;
            replayCamera.nearClipPlane = .05f;
            replayCamera.targetTexture = null;
            replayCamera.clearFlags = CameraClearFlags.SolidColor;
            replayCamera.backgroundColor = battle.backgroundColor;
            replayCamera.cullingMask = battle.cullingMask; // already without the orb board layer
        }

        private void HideBattleView(Camera battle)
        {
            foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (camera == replayCamera || !camera.enabled || camera.targetTexture != null
                    || camera.gameObject.scene != gameObject.scene) continue;
                camera.enabled = false;
                disabledCameras.Add(camera);
            }
            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || !canvas.enabled || canvas.gameObject.scene != gameObject.scene) continue;
                canvas.enabled = false;
                hiddenCanvases.Add(canvas);
            }
        }

        private void BuildOverlay()
        {
            Font font = null;
            foreach (Canvas canvas in hiddenCanvases) { font = LabGuideText.FontOf(canvas); if (font != null) break; }
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root = new GameObject("Final hit replay · letterbox", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            overlay = root.GetComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay.overrideSorting = true;
            overlay.sortingOrder = OverlaySortingOrder;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390f, 844f);
            scaler.matchWidthOrHeight = .5f;
            topBar = Bar("Top Bar", root.transform, 1f);
            bottomBar = Bar("Bottom Bar", root.transform, 0f);
            flash = Stretch("Cut Flash", root.transform).gameObject.AddComponent<Image>();
            flash.raycastTarget = false;
            flash.color = Color.clear;
            var caption = Stretch("Caption", bottomBar);
            captionGroup = caption.gameObject.AddComponent<CanvasGroup>();
            captionGroup.alpha = 0f;
            captionGroup.blocksRaycasts = false;
            string who = Shot.Slot > 0 ? "P" + Shot.Slot + " 마지막 일격" : "마지막 일격";
            Label(caption, "Who", font, who, 20, TextAnchor.MiddleLeft, new Color(1f, .93f, .78f, 1f));
            Label(caption, "Skip Hint", font, "탭하여 건너뛰기", 13, TextAnchor.MiddleRight, new Color(1f, 1f, 1f, .7f));
        }

        private static RectTransform Bar(string name, Transform parent, float edge)
        {
            var bar = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            bar.SetParent(parent, false);
            bar.anchorMin = new Vector2(0f, edge); bar.anchorMax = new Vector2(1f, edge);
            bar.pivot = new Vector2(.5f, edge);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = Vector2.zero;
            var image = bar.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
            return bar;
        }

        private static RectTransform Stretch(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void Label(RectTransform parent, string name, Font font, string words, int size, TextAnchor anchor, Color color)
        {
            var rect = Stretch(name, parent);
            rect.offsetMin = new Vector2(18f, 0f); rect.offsetMax = new Vector2(-18f, 0f);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = size; text.alignment = anchor; text.color = color;
            text.text = words; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
        }

        /// <summary>
        /// Shots are framed by their width, so a tall phone keeps the orb and the gourd side by side; the vertical
        /// angle grows for a narrow screen instead of cropping the action.
        /// </summary>
        public static float VerticalFieldOfView(float horizontalDegrees, float aspect)
        {
            if (!(aspect > .01f)) aspect = 1f;
            float half = Mathf.Tan(horizontalDegrees * .5f * Mathf.Deg2Rad);
            return Mathf.Clamp(2f * Mathf.Atan(half / aspect) * Mathf.Rad2Deg, 20f, 85f);
        }

        private static Vector3 Horizontal(Vector3 value, Vector3 fallback)
        {
            value.y = 0f;
            if (value.sqrMagnitude > 1e-6f) return value.normalized;
            fallback.y = 0f;
            return fallback.sqrMagnitude > 1e-6f ? fallback.normalized : Vector3.forward;
        }

        private static float Smooth(float from, float to, float value)
        {
            float t = Mathf.Clamp01((value - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        private void OnDisable() => ResetPresentation();
        private void OnDestroy() => ResetPresentation();
    }

    /// <summary>Destroys a runtime material with the effect that owns it.</summary>
    internal sealed class LabFinishReplayOwnedMaterial : MonoBehaviour
    {
        public Material Material;
        private void OnDestroy() { if (Material != null) Destroy(Material); }
    }
}
