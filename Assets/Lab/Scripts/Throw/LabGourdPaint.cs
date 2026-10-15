using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 박 페인트 (#153). 요괴 자리에 흰 박을 붙이고, Host가 승인한 타격 위치에 구슬 속성 색을 칠한다.
    /// Fire=빨강, Water=파랑, Wood=초록, Metal=노랑, Earth=갈색. 속성이 없는 타격은 칠하지 않는다.
    /// 본 게임에서는 LabSceneController가 시작할 때 붙이며, 같은 박에 박 모델을 다시 붙이는
    /// 크랙·마무리 연출(#163, LabGourdCrack·LabGourdFinish)은 꺼 둔다.
    /// 피해·승패·네트워크 패킷은 바꾸지 않는 화면 연출이다.
    /// </summary>
    public sealed class LabGourdPaint : MonoBehaviour
    {
        /// <summary>
        /// 본 게임 장면에서 페인트 박을 쓸지. 크랙 박을 시험하는 테스트만 false로 바꾼다.
        /// </summary>
        public static bool EnabledInMainScene = true;

        public const string ResourceFolder = "GourdPaint/";
        public const int Capacity = 128;

        public LabTarget target;
        public LabNetwork network;
        public GameObject visualPrefab;
        public Material paintMaterial;
        [Range(.05f, .3f)] public float splatRadius = .15f;

        private readonly Vector4[] positions = new Vector4[Capacity];
        private readonly Vector4[] colors = new Vector4[Capacity];
        private MaterialPropertyBlock block;
        private Renderer shell;
        private string roundKey;

        public int PaintCount { get; private set; }
        public Transform Shell => shell != null ? shell.transform : null;

        /// <summary>
        /// 게임 시작 시 코드로 붙일 때 부른다. 재질·흰 박은 Resources/GourdPaint에서 읽는다.
        /// 모든 Start보다 먼저 불려야 크랙 박이 붙지 않는다 (LabSceneController.Awake에서 호출).
        /// </summary>
        public static LabGourdPaint Attach(LabNetwork network)
        {
            if (network == null || network.target == null) return null;
            var existing = network.target.GetComponent<LabGourdPaint>();
            if (existing != null) return existing;
            var material = Resources.Load<Material>(ResourceFolder + "WhiteGourd");
            var prefab = Resources.Load<GameObject>(ResourceFolder + "WhiteGourd");
            if (material == null || prefab == null)
            {
                Debug.LogWarning("C6_LAB_GOURD_PAINT_MISSING: Resources/GourdPaint/WhiteGourd material or prefab.");
                return null;
            }
            var paint = network.target.gameObject.AddComponent<LabGourdPaint>();
            paint.target = network.target;
            paint.network = network;
            paint.paintMaterial = material;
            paint.visualPrefab = prefab;
            paint.DisableCrackPresentation();
            return paint;
        }

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            DisableCrackPresentation();
        }

        /// <summary>크랙 박은 늦게 실행되며 같은 표적에 자기 박 모델을 다시 붙이므로, 시작 전에 끈다.</summary>
        private void DisableCrackPresentation()
        {
            if (target == null) return;
            foreach (var crack in FindObjectsByType<LabGourdCrack>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (crack.target != target) continue;
                var finish = crack.GetComponent<LabGourdFinish>();
                if (finish != null) finish.enabled = false;
                crack.enabled = false;
            }
        }

        private void Start()
        {
            if (target == null || visualPrefab == null || paintMaterial == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            target.SetVisualPrefab(visualPrefab);
            Transform root = target.MountedVisual != null ? target.MountedVisual.transform : null;
            Transform white = root != null ? root.Find("White Gourd") : null;
            shell = white != null ? white.GetComponent<Renderer>() : null;
            if (shell == null)
            {
                Debug.LogWarning("C6_LAB_GOURD_PAINT_SHELL_MISSING: the white gourd prefab has no 'White Gourd' renderer.");
                return;
            }
            shell.sharedMaterial = paintMaterial;
            target.ApprovedHit += OnHit;
            ClearPaint();
        }

        private void Update()
        {
            // 새 라운드나 로비로 돌아오면 칠을 지운다.
            var snapshot = network != null ? network.Snapshot : null;
            string key = snapshot != null ? snapshot.sessionId + ":" + snapshot.round : null;
            if (key != roundKey)
            {
                roundKey = key;
                ClearPaint();
            }
        }

        private void OnHit(Vector3 localPoint, LabOrbElement element)
        {
            if (shell == null || element < LabOrbElement.Fire || element > LabOrbElement.Earth) return;
            Vector3 world = target.Hitbox.transform.TransformPoint(localPoint);
            Vector3 direction = shell.transform.InverseTransformPoint(world).normalized;
            var appearance = network != null && network.config != null ? network.config.OrbAppearance : null;
            Paint(direction, appearance != null ? appearance.ElementColorFor(element) : LabOrbAppearance.DefaultElementColor(element));
        }

        /// <summary>기본 속성 색 (0 Fire … 4 Earth)</summary>
        public static Color Palette(int index) =>
            LabOrbAppearance.DefaultElementColor((LabOrbElement)(Mathf.Clamp(index, 0, 4) + 1));

        /// <summary>흰 박 표면의 방향에 색 자국을 남긴다. 최대 128개, 넘으면 더 칠하지 않는다.</summary>
        public bool Paint(Vector3 direction, Color color)
        {
            if (shell == null || PaintCount >= Capacity || !Finite(direction) || direction.sqrMagnitude < .00001f) return false;
            Vector3 point = direction.normalized * .5f;
            positions[PaintCount] = new Vector4(point.x, point.y, point.z, splatRadius);
            colors[PaintCount] = color;
            PaintCount++;
            Apply();
            return true;
        }

        public void ClearPaint()
        {
            PaintCount = 0;
            Apply();
        }

        private void Apply()
        {
            if (shell == null) return;
            block.SetInt("_PaintCount", PaintCount);
            block.SetVectorArray("_PaintPositions", positions);
            block.SetVectorArray("_PaintColors", colors);
            shell.SetPropertyBlock(block);
        }

        private static bool Finite(Vector3 p) =>
            !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z)
            && !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z);

        private void OnDestroy()
        {
            if (target != null) target.ApprovedHit -= OnHit;
        }
    }
}
