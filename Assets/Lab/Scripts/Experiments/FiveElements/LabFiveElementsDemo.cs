using UnityEngine;
using UnityEngine.UIElements;

namespace C6Lab
{
    public enum LabDemoSurface { Floor, Wall, MovingWall }
    public enum LabFiveElementsComparison { OrdinaryElements, CurrentFiveColor, EnhancedFiveColor }

    /// <summary>Isolated presentation clock. The sample path feeds the same VFX pool as a game throw.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class LabFiveElementsDemo : MonoBehaviour
    {
        public const float FlightSeconds = .68f;
        public const float CycleSeconds = 3.80f;
        private const float Spacing = 3.8f;
        private const float GalleryWidth = 21.15f;
        // Launch from the upper right so the true flight history reads side-on before contact.
        private static readonly Vector3 LaunchOffset = new Vector3(2.42f, 1.56f, -.32f);
        public Material stageMaterial;
        public StyleSheet hudStyle;
        public Font hudFont;
        public float diameter = .33f;

        private static readonly string[] Names = { "화", "수", "목", "금", "토" };
        private readonly Transform[] targets = new Transform[5];
        private readonly LabFiveElementVfx.Flight[] flights = new LabFiveElementVfx.Flight[5];
        private readonly Button[] laneButtons = new Button[5];
        private LabFiveElementVfx pool;
        private Material platformMaterial;
        private VisualElement shell, view, progress, scrub;
        private Label clockLabel, phaseLabel, comparisonTitle;
        private bool strong, mono, bright, distant, lowDetail;
        private LabDemoSurface surface;
        private LabFiveElementsComparison comparison;
        private int focus = -1, markSeconds = 2, frameRate = 60, lastHundredth = -1;
        private float speed = 1f;
        private bool initialized, previousBackground, framingPending, previousFiveColorBoost;
        private int previousFps, previousVSync;
        private float sampleRemainder;

        public Camera DemoCamera { get; private set; }
        public float PlaybackTime { get; private set; }
        public bool Paused { get; private set; }
        public int VolleyHits { get; private set; }
        public int ActiveFlights => pool.ActiveFlightCount;
        public int ActiveImpacts => pool.ActiveImpactCount;
        public int SelectedLane => focus;
        public LabFiveElementsComparison SelectedComparison => comparison;
        public string ComparisonCaption => comparison == LabFiveElementsComparison.OrdinaryElements ? "일반 오행" :
            comparison == LabFiveElementsComparison.CurrentFiveColor ? "현재 오색" : "강화 오색";

        private void Start()
        {
            if (stageMaterial == null || hudStyle == null || hudFont == null)
                throw new System.InvalidOperationException("Open the prepared FiveElementsComparison scene.");
            previousBackground = Application.runInBackground;
            previousFps = Application.targetFrameRate;
            previousVSync = QualitySettings.vSyncCount;
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = frameRate;
            initialized = true;
            pool = LabFiveElementVfx.ForScene(gameObject.scene);
            previousFiveColorBoost = pool.FiveColorBoostEnabled;
            pool.ManualClock = true;
            platformMaterial = new Material(stageMaterial) { name = "Comparison surface", hideFlags = HideFlags.DontSave };
            CreateStudio();
            CreateHud();
            FrameCamera();
            Replay();
        }

        private void OnDestroy()
        {
            if (!initialized) return;
            Application.runInBackground = previousBackground;
            Application.targetFrameRate = previousFps;
            QualitySettings.vSyncCount = previousVSync;
            if (platformMaterial != null) Destroy(platformMaterial);
            if (pool != null)
            {
                pool.ClearAll();
                pool.ManualClock = false;
                pool.MarkLifetimeOverride = -1;
                pool.LowDetail = false;
                pool.FiveColorBoostEnabled = previousFiveColorBoost;
            }
        }

        private void CreateStudio()
        {
            var cameraObject = new GameObject("Comparison Camera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.tag = "MainCamera";
            DemoCamera = cameraObject.AddComponent<Camera>();
            DemoCamera.clearFlags = CameraClearFlags.SolidColor;
            DemoCamera.nearClipPlane = .015f;
            DemoCamera.farClipPlane = 40f;
            DemoCamera.allowHDR = false;
            var lightObject = new GameObject("Softbox");
            lightObject.transform.SetParent(transform, false);
            var key = lightObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.45f;
            key.color = new Color(1f, .96f, .90f);
            key.shadows = LightShadows.Soft;
            key.shadowStrength = .62f;
            lightObject.transform.rotation = Quaternion.Euler(44f, -29f, 0f);

            for (int i = 0; i < targets.Length; i++)
            {
                targets[i] = new GameObject(Names[i] + " · Contact").transform;
                targets[i].SetParent(transform, false);
                // The anchor has unit scale. A separate shaded disc supplies the visible contact surface.
                GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = "Sample surface";
                disc.transform.SetParent(targets[i], false);
                disc.transform.localPosition = new Vector3(0, -.12f * diameter, 0);
                disc.transform.localScale = new Vector3(3.48f, .12f, 2.9f) * diameter;
                disc.GetComponent<Renderer>().sharedMaterial = platformMaterial;
                Destroy(disc.GetComponent<Collider>());
            }
            SetLens((float)Screen.width / Mathf.Max(1, Screen.height));
            SetLighting(false);
        }

        private void CreateHud()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.style.flexGrow = 1;
            root.styleSheets.Add(hudStyle);
            shell = root.Q("shell");
            shell.style.unityFont = hudFont;
            view = root.Q("view");
            view.RegisterCallback<GeometryChangedEvent>(_ => FrameCamera());
            shell.RegisterCallback<GeometryChangedEvent>(_ => shell.EnableInClassList("compact", shell.contentRect.width < 900f));
            var labels = root.Q("lanes");
            for (int i = 0; i < Names.Length; i++)
            {
                int lane = i;
                var button = new Button(() => SelectLane(focus == lane ? -1 : lane)) { text = Names[i] };
                button.AddToClassList("lane");
                button.style.borderBottomColor = pool.Profile.Pigment((LabOrbElement)(i + 1), false);
                labels.Add(button);
                laneButtons[i] = button;
            }
            clockLabel = root.Q<Label>("clock");
            phaseLabel = root.Q<Label>("phase");
            comparisonTitle = root.Q<Label>("comparisonTitle");
            Bind("ordinaryElements", () => SelectComparison(LabFiveElementsComparison.OrdinaryElements));
            Bind("currentFiveColor", () => SelectComparison(LabFiveElementsComparison.CurrentFiveColor));
            Bind("enhancedFiveColor", () => SelectComparison(LabFiveElementsComparison.EnhancedFiveColor));
            Bind("play", () => SetPaused(!Paused));
            Bind("restart", () => { Replay(); SetPaused(false); });
            Bind("speed", () => { speed = speed == 1f ? .25f : speed == .25f ? .5f : 1f; UpdateLabels(); });
            Bind("fps", () => SetFrameRate(frameRate == 60 ? 30 : 60));
            Bind("strength", () => { strong = !strong; Seek(PlaybackTime); });
            Bind("color", () => { mono = !mono; Seek(PlaybackTime); });
            Bind("backdrop", () => { SetLighting(!bright); UpdateLabels(); });
            Bind("surface", () => SetSurface((LabDemoSurface)(((int)surface + 1) % 3)));
            Bind("camera", () => { distant = !distant; FrameCamera(); UpdateLabels(); });
            Bind("element", () => SelectLane(focus == 4 ? -1 : focus + 1));
            Bind("mark", () => { markSeconds = markSeconds % 3 + 1; Seek(PlaybackTime); });
            Bind("detail", () => { lowDetail = !lowDetail; Seek(PlaybackTime); });

            progress = root.Q("progress");
            root.Q("impactTick").style.left = Length.Percent(FlightSeconds / CycleSeconds * 100);
            scrub = root.Q("scrub");
            scrub.RegisterCallback<PointerDownEvent>(e => { scrub.CapturePointer(e.pointerId); ScrubTo(e.localPosition.x); });
            scrub.RegisterCallback<PointerMoveEvent>(e => { if (scrub.HasPointerCapture(e.pointerId)) ScrubTo(e.localPosition.x); });
            scrub.RegisterCallback<PointerUpEvent>(e => scrub.ReleasePointer(e.pointerId));
        }

        private void Bind(string name, System.Action action) => shell.Q<Button>(name).clicked += action;
        private void Caption(string name, string caption) => shell.Q<Button>(name).text = caption;

        private void ScrubTo(float x)
        {
            SetPaused(true);
            Seek(Mathf.Clamp01(x / Mathf.Max(1f, scrub.contentRect.width)) * CycleSeconds);
        }

        public void SelectLane(int lane)
        {
            focus = Mathf.Clamp(lane, -1, 4);
            FrameCamera();
            Seek(PlaybackTime);
        }

        public void SelectComparison(LabFiveElementsComparison value)
        {
            if ((uint)value > (uint)LabFiveElementsComparison.EnhancedFiveColor)
                throw new System.ArgumentOutOfRangeException(nameof(value));
            comparison = value;
            if (pool != null) Seek(PlaybackTime);
        }

        public void SetSurface(LabDemoSurface value) { surface = value; Seek(PlaybackTime); }
        public void SetPaused(bool value) { Paused = value; UpdateLabels(); }

        public void SetFrameRate(int fps)
        {
            frameRate = fps <= 30 ? 30 : 60;
            Application.targetFrameRate = frameRate;
            sampleRemainder = 0;
            UpdateLabels();
        }

        public void SetComparison(bool strongHit, bool monochrome, bool brightBackdrop, bool gameCamera, bool reducedDetail)
        {
            strong = strongHit; mono = monochrome; distant = gameCamera; lowDetail = reducedDetail;
            SetLighting(brightBackdrop);
            FrameCamera();
            Seek(PlaybackTime);
        }

        private void SetLighting(bool light)
        {
            bright = light;
            DemoCamera.backgroundColor = light ? new Color(.79f, .77f, .72f) : new Color(.145f, .15f, .146f);
            platformMaterial.SetColor("_BaseColor", light ? new Color(.64f, .63f, .58f) : new Color(.27f, .275f, .26f));
        }

        private void FrameCamera()
        {
            framingPending = true;
            if (view == null || view.panel == null || shell == null) return;
            var box = view.worldBound;
            var full = shell.worldBound;
            // UI Toolkit geometry is initially NaN. Never feed it back into the camera or lane padding.
            if (!Finite(box.yMin) || !Finite(box.yMax) || !Finite(full.yMin) ||
                !Finite(box.height) || !Finite(full.height) || box.height <= 1f || full.height <= 1f) return;
            var rect = new Rect(0, 1f - (box.yMax - full.yMin) / full.height, 1, box.height / full.height);
            float aspect = (float)Screen.width / Mathf.Max(1f, Screen.height * rect.height);
            if (!Finite(rect.y) || !Finite(rect.height) || !Finite(aspect) || aspect <= 0f) return;
            DemoCamera.rect = rect;
            SetLens(aspect);
            framingPending = false;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public void SetLens(float aspect)
        {
            if (!Finite(aspect) || aspect <= 0f) return;
            float mid = focus < 0 ? 0f : (focus - 2) * Spacing * diameter;
            var center = new Vector3(mid, .55f * diameter, 0);
            DemoCamera.orthographic = !distant;
            Vector3 eye = distant ? new Vector3(0, .65f, -8f) : new Vector3(0, 2.1f, -5f);
            DemoCamera.transform.position = center + eye;
            DemoCamera.transform.LookAt(center);
            DemoCamera.fieldOfView = 52f;
            float width = (focus < 0 ? GalleryWidth : 5.90f) * diameter;
            // Reserve the same vertical margin for every style, including the larger reward splash on walls.
            DemoCamera.orthographicSize = Mathf.Max(3.6f * diameter, width / (2f * Mathf.Max(.1f, aspect)));
            if (shell != null)
            {
                var lanes = shell.Q("lanes");
                float visibleWidth = DemoCamera.orthographicSize * 2f * aspect;
                float inset = focus < 0 && !distant
                    ? Mathf.Max(0, (1f - 5f * Spacing * diameter / visibleWidth) * 50f) : 0;
                lanes.style.paddingLeft = lanes.style.paddingRight = Length.Percent(inset);
            }
        }

        private bool Visible(int i) => focus < 0 || focus == i;


        private void UpdateTargets(float time)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                var pos = new Vector3((i - 2) * Spacing * diameter, 0, 0);
                Quaternion orientation = surface == LabDemoSurface.Floor ? Quaternion.identity : Quaternion.Euler(-90, 0, 0);
                if (surface == LabDemoSurface.MovingWall)
                {
                    pos.x += Mathf.Sin(time * 3.5f) * diameter * .42f;
                    pos.y += Mathf.Sin(time * 4.1f) * diameter * .29f;
                    orientation = Quaternion.AngleAxis(12f * Mathf.Sin(time * 4f), Vector3.up) * orientation;
                }
                targets[i].SetPositionAndRotation(pos, orientation);
            }
        }

        private Vector3 FlightPosition(int i, float t)
        {
            Vector3 normal = targets[i].up;
            Vector3 end = targets[i].position + normal * (.5f * diameter);
            return end + (1f - t) * diameter * LaunchOffset + Vector3.up * (Mathf.Sin(t * Mathf.PI) * diameter * .37f);
        }

        private Vector3 FlightDirection(float t)
        {
            return (-LaunchOffset + Vector3.up * (.37f * Mathf.PI * Mathf.Cos(t * Mathf.PI))).normalized;
        }

        public void Replay()
        {
            pool.ClearAll();
            pool.MarkLifetimeOverride = markSeconds;
            pool.LowDetail = lowDetail;
            pool.FiveColorBoostEnabled = comparison == LabFiveElementsComparison.EnhancedFiveColor;
            bool fiveColor = comparison != LabFiveElementsComparison.OrdinaryElements;
            PlaybackTime = 0;
            VolleyHits = 0;
            sampleRemainder = 0;
            UpdateTargets(0);
            for (int i = 0; i < 5; i++)
            {
                bool visible = Visible(i);
                targets[i].gameObject.SetActive(visible);
                laneButtons[i].style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (visible)
                    flights[i] = pool.BeginFlight(fiveColor ? LabOrbElement.None : (LabOrbElement)(i + 1),
                        FlightPosition(i, 0), FlightDirection(0), diameter,
                        strong ? pool.Profile.strongStrength : pool.Profile.normalStrength, mono, fiveColor);
            }
            UpdateLabels();
        }

        private void Update()
        {
            if (framingPending && DemoCamera != null) FrameCamera();
            if (pool == null || Paused) return;
            float step = 1f / frameRate;
            sampleRemainder += Mathf.Min(Time.unscaledDeltaTime, .2f);
            while (sampleRemainder + .000001f >= step)
            {
                sampleRemainder = Mathf.Max(0, sampleRemainder - step);
                if (PlaybackTime >= CycleSeconds) Replay();
                Advance(step * speed);
            }
            UpdateTransport();
        }

        // Split exactly at the contact plane, even when the selected sampling interval spans the hit.
        private void Advance(float delta)
        {
            float next = Mathf.Min(CycleSeconds, PlaybackTime + delta);
            if (PlaybackTime < FlightSeconds)
            {
                float until = Mathf.Min(next, FlightSeconds);
                float t = until / FlightSeconds;
                UpdateTargets(until);
                for (int i = 0; i < 5; i++)
                    if (Visible(i)) flights[i].Move(FlightPosition(i, t), FlightDirection(t));
                pool.Advance(until - PlaybackTime);
                PlaybackTime = until;
                if (until >= FlightSeconds)
                {
                    for (int i = 0; i < 5; i++)
                        if (Visible(i) && flights[i].Hit(targets[i].position, targets[i].up, FlightDirection(1), targets[i], true))
                            VolleyHits++;
                }
            }
            UpdateTargets(next);
            if (next > PlaybackTime) pool.Advance(next - PlaybackTime);
            PlaybackTime = next;
        }

        public void Seek(float time)
        {
            float destination = Mathf.Clamp(time, 0, CycleSeconds);
            Replay();
            for (int j = 0; j < 240 && PlaybackTime < destination; j++)
                Advance(Mathf.Min(1f / 60f, destination - PlaybackTime));
            UpdateTransport();
        }

        private void UpdateLabels()
        {
            if (shell == null) return;
            bool fiveColor = comparison != LabFiveElementsComparison.OrdinaryElements;
            comparisonTitle.text = "오색흙  /  " + ComparisonCaption + " 비교";
            shell.Q<Button>("ordinaryElements").EnableInClassList("primary", !fiveColor);
            shell.Q<Button>("currentFiveColor").EnableInClassList("primary", comparison == LabFiveElementsComparison.CurrentFiveColor);
            shell.Q<Button>("enhancedFiveColor").EnableInClassList("primary", comparison == LabFiveElementsComparison.EnhancedFiveColor);
            for (int i = 0; i < laneButtons.Length; i++)
            {
                laneButtons[i].text = fiveColor ? "오색 " + (i + 1) : Names[i];
            }
            Caption("play", Paused ? "재생" : "일시정지");
            Caption("speed", speed == 1f ? "속도 1×" : speed == .5f ? "속도 0.5×" : "속도 0.25×");
            Caption("fps", frameRate + " fps");
            Caption("strength", strong ? "강타" : "일반 타격");
            Caption("color", mono ? "단색" : "컬러");
            Caption("backdrop", bright ? "밝은 배경" : "어두운 배경");
            Caption("surface", surface == LabDemoSurface.Floor ? "바닥" : surface == LabDemoSurface.Wall ? "벽" : "움직이는 벽");
            Caption("camera", distant ? "게임 거리" : "확대 비교");
            Caption("element", focus < 0 ? "다섯 동시" : "단일 · " + (fiveColor ? "오색 " + (focus + 1) : Names[focus]));
            Caption("mark", "흔적 " + markSeconds + "초");
            Caption("detail", lowDetail ? "낮은 품질" : "기본 품질");
            lastHundredth = -1;
            UpdateTransport();
        }

        private void UpdateTransport()
        {
            int cs = Mathf.RoundToInt(PlaybackTime * 100);
            if (cs == lastHundredth) return;
            lastHundredth = cs;
            progress.style.width = Length.Percent(PlaybackTime / CycleSeconds * 100);
            float t = PlaybackTime - FlightSeconds;
            clockLabel.text = t < 0 ? "비행  " + (-t).ToString("0.00") + " s" : "충돌  +" + t.ToString("0.00") + " s";
            phaseLabel.text = t < 0 ? "비행" : t < .05f ? "눌림" : t < .15f ? "반죽 퍼짐" : t < .70f ? "진흙 정착" :
                t < markSeconds ? "젖은 흔적" : "회수";
        }
    }
}
