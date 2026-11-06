using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// #477 튜토리얼: 판 위 화면 자리에 보이지 않는 벽을 세워 구슬·북이 들어가지 못하게 한다. 위·아래 종이 테두리 벽과 같은
    /// 물리 벽이라 부딪히면 튕겨 나온다. 튜토리얼은 이것으로 캐릭터 얼굴과 기운 막대를 구슬이 가리지 못하게 한다.
    /// 보통 판은 세우지 않아 지금까지와 같다.
    /// </summary>
    public sealed partial class LabOrbBoard
    {
        private GameObject keepOutRoot;
        private readonly List<Rect> keepOutCircles = new List<Rect>();
        private readonly List<Rect> keepOutBoxes = new List<Rect>();

        /// <summary>How many keep-out walls stand right now.</summary>
        public int KeepOutCount => keepOutRoot != null ? keepOutRoot.transform.childCount : 0;

        /// <summary>
        /// Stand walls over these screen areas (pixels): a round wall inscribed in each of <paramref name="circles"/>
        /// and a box over each of <paramref name="boxes"/>. Empty lists take them all down. The same areas (within
        /// three pixels, so a shaking gauge does not rebuild them) are not rebuilt.
        /// </summary>
        public void SetKeepOut(IReadOnlyList<Rect> circles, IReadOnlyList<Rect> boxes)
        {
            if (Same(keepOutCircles, circles) && Same(keepOutBoxes, boxes)) return;
            keepOutCircles.Clear();
            keepOutBoxes.Clear();
            if (circles != null) keepOutCircles.AddRange(circles);
            if (boxes != null) keepOutBoxes.AddRange(boxes);
            if (keepOutRoot != null) Destroy(keepOutRoot);
            keepOutRoot = null;
            if (keepOutCircles.Count == 0 && keepOutBoxes.Count == 0 || inputCamera == null || config == null) return;
            keepOutRoot = new GameObject("Tutorial keep-out walls") { layer = gameObject.layer };
            keepOutRoot.transform.SetParent(transform, false);
            float depth = Mathf.Max(config.OrbRadius, config.DrumRadius) + WallThickness;
            foreach (Rect area in keepOutCircles)
            {
                if (!TryWorldArea(area, out Rect world)) continue;
                var wall = KeepOutWall("Keep-out circle", world.center);
                var sphere = wall.AddComponent<SphereCollider>();
                sphere.radius = Mathf.Min(world.width, world.height) * .5f;
                sphere.sharedMaterial = contactMaterial;
            }
            foreach (Rect area in keepOutBoxes)
            {
                if (!TryWorldArea(area, out Rect world)) continue;
                var wall = KeepOutWall("Keep-out box", world.center);
                var box = wall.AddComponent<BoxCollider>();
                box.size = new Vector3(world.width, world.height, depth * 2f);
                box.sharedMaterial = contactMaterial;
            }
        }

        private GameObject KeepOutWall(string label, Vector2 worldCenter)
        {
            var wall = new GameObject(label) { layer = gameObject.layer };
            wall.transform.SetParent(keepOutRoot.transform, false);
            // Positions are world coordinates on the orb plane, like the paper walls.
            wall.transform.SetPositionAndRotation(new Vector3(worldCenter.x, worldCenter.y, 0f), Quaternion.identity);
            return wall;
        }

        private bool TryWorldArea(Rect screen, out Rect world)
        {
            world = default;
            if (!TryScreenToWorld(screen.min, out Vector2 a) || !TryScreenToWorld(screen.max, out Vector2 b)) return false;
            world = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            return world.width > 0f && world.height > 0f;
        }

        private static bool Same(List<Rect> current, IReadOnlyList<Rect> next)
        {
            int count = next != null ? next.Count : 0;
            if (current.Count != count) return false;
            for (int i = 0; i < count; i++)
                if ((current[i].min - next[i].min).sqrMagnitude > 9f || (current[i].max - next[i].max).sqrMagnitude > 9f)
                    return false;
            return true;
        }
    }
}
