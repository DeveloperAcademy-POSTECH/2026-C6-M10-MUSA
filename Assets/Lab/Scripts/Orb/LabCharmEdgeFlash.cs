using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 매혹 (#251): 홀린 사람의 조합판에서 던지는 쪽 가장자리(위쪽 곡선)가 빨갛게 은은히 빛나, 지금은 던질 수 없다고
    /// 알려 준다. 곡선을 따라 아래(판 안쪽)로 퍼지는 띠를 그리며 천천히 밝아졌다 어두워진다. 판의 종이 바탕보다 앞, 구슬과
    /// 같은 평면에 깔린다. 매혹이 풀리면 부드럽게 사라진다. 화면 표시만 한다.
    /// </summary>
    public sealed class LabCharmEdgeFlash : MonoBehaviour
    {
        // The band reaches this share of the board width into the board, and a little above the line.
        private const float InwardShare = .07f, OutwardShare = .015f;
        private const float DimAlpha = .30f, BrightAlpha = .75f, BreatheHz = .6f;
        /// <summary>How long the glow takes to fade away when the charm ends.</summary>
        public const float ReleaseSeconds = .45f;
        private const int GlowOrder = -21;
        /// <summary>The charm's heart colour, shared with its gauge and the gourd's heart.</summary>
        public static readonly Color HeartColor = new Color(.93f, .24f, .36f);
        /// <summary>The red of the throwing edge that cannot throw.</summary>
        public static readonly Color GlowColor = new Color(.92f, .14f, .1f);
        private static Material glowMaterial;

        private MeshRenderer glow;
        private Mesh mesh;
        private Color[] colors = new Color[0];
        private IReadOnlyList<Vector2> shownEdge;
        private float age;
        private float releaseLeft = -1f;
        private float releaseFrom;
        private float alpha;
        private bool on;

        public bool Visible => glow != null && glow.gameObject.activeSelf;
        /// <summary>Fading away as the charm ends.</summary>
        public bool Releasing => Visible && releaseLeft >= 0f;
        /// <summary>The glow's opacity at the edge line now.</summary>
        public float Brightness => Visible ? alpha : 0f;
        public MeshRenderer Glow => glow;
        /// <summary>The glow's sorting order; the board's paper plate must stay below it.</summary>
        public int GlowSortingOrder => GlowOrder;

        public static LabCharmEdgeFlash Create(Transform board)
        {
            var flash = new GameObject("Charm Throw Edge");
            flash.layer = board.gameObject.layer;
            flash.transform.SetParent(board, false);
            return flash.AddComponent<LabCharmEdgeFlash>();
        }

        /// <summary>Lays the glow along the board's top edge (a left-to-right world polyline), reaching into the board.</summary>
        public void Show(IReadOnlyList<Vector2> topEdge, float boardWidth)
        {
            EnsureParts();
            if (!on || releaseLeft >= 0f) age = 0f;
            on = true;
            releaseLeft = -1f;
            if (!ReferenceEquals(topEdge, shownEdge)) Build(topEdge, boardWidth);
            glow.gameObject.SetActive(true);
            Pose();
        }

        /// <summary>The charm ended: the glow fades gently away.</summary>
        public void Release()
        {
            if (!on) return;
            on = false;
            releaseLeft = ReleaseSeconds;
            releaseFrom = alpha;
        }

        public void Hide()
        {
            on = false;
            releaseLeft = -1f;
            if (glow != null) glow.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!Visible) return;
            age += Time.deltaTime;
            if (releaseLeft >= 0f)
            {
                releaseLeft -= Time.deltaTime;
                if (releaseLeft < 0f)
                {
                    Hide();
                    return;
                }
            }
            Pose();
        }

        private void Pose()
        {
            // A soft red that slowly brightens and dims.
            alpha = Mathf.Lerp(DimAlpha, BrightAlpha, .5f + .5f * Mathf.Sin(age * BreatheHz * 2f * Mathf.PI));
            if (releaseLeft >= 0f) alpha = Mathf.Lerp(0f, releaseFrom, releaseLeft / ReleaseSeconds);
            // Three rows along the edge: clear just above it, the glow on the line, clear deep inside the board.
            for (int i = 0; i < colors.Length; i += 3)
            {
                colors[i] = new Color(GlowColor.r, GlowColor.g, GlowColor.b, 0f);
                colors[i + 1] = new Color(GlowColor.r, GlowColor.g, GlowColor.b, alpha);
                colors[i + 2] = new Color(GlowColor.r, GlowColor.g, GlowColor.b, 0f);
            }
            mesh.colors = colors;
        }

        private void Build(IReadOnlyList<Vector2> edge, float boardWidth)
        {
            shownEdge = edge;
            int count = edge != null ? edge.Count : 0;
            mesh.Clear();
            colors = new Color[count >= 2 ? count * 3 : 0];
            if (count < 2) return;
            var vertices = new Vector3[count * 3];
            var triangles = new int[(count - 1) * 12];
            for (int i = 0; i < count; i++)
            {
                Vector2 tangent = (edge[Mathf.Min(i + 1, count - 1)] - edge[Mathf.Max(i - 1, 0)]).normalized;
                // The edge runs left to right, so turning its direction clockwise points down into the board.
                var inward = new Vector2(tangent.y, -tangent.x);
                Vector2 point = edge[i];
                vertices[i * 3] = Local(point - inward * (OutwardShare * boardWidth));
                vertices[i * 3 + 1] = Local(point);
                vertices[i * 3 + 2] = Local(point + inward * (InwardShare * boardWidth));
            }
            for (int i = 0, t = 0; i < count - 1; i++)
                for (int row = 0; row < 2; row++)
                {
                    int a = i * 3 + row, b = a + 1, c = a + 3, d = a + 4;
                    triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                    triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
                }
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }

        private Vector3 Local(Vector2 world) => transform.InverseTransformPoint(new Vector3(world.x, world.y, 0f));

        private void EnsureParts()
        {
            if (glow != null) return;
            var part = new GameObject("Charm Throw Glow");
            part.layer = gameObject.layer;
            part.transform.SetParent(transform, false);
            mesh = new Mesh { name = "Charm throw edge glow" };
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            glow = part.AddComponent<MeshRenderer>();
            if (glowMaterial == null)
                glowMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Charm throw edge glow", hideFlags = HideFlags.DontSave };
            glow.sharedMaterial = glowMaterial;
            glow.sortingOrder = GlowOrder;
            glow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glow.receiveShadows = false;
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
