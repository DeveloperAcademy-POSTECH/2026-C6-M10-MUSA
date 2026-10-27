using UnityEngine;

namespace C6Lab
{
    /// <summary>Presentation only: the root sphere continues to own collision and gameplay size.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class LabOrbSpriteArt : MonoBehaviour
    {
        public static float SizeScaleFor(LabOrbKind kind) => kind == LabOrbKind.Combined ? 1.2f : 1f;

        public static LabOrbSpriteArt Set(Transform parent, LabOrbSpriteArt existing,
            Sprite sprite, float radius, float depth = 0f)
        {
            if (sprite == null)
            {
                if (existing != null) existing.gameObject.SetActive(false);
                return existing;
            }
            if (existing == null)
            {
                var art = new GameObject("Orb Sprite Art", typeof(SpriteRenderer));
                art.transform.SetParent(parent, false);
                existing = art.AddComponent<LabOrbSpriteArt>();
            }
            existing.gameObject.layer = parent.gameObject.layer;
            existing.gameObject.SetActive(true);
            existing.GetComponent<SpriteRenderer>().sprite = sprite;
            existing.transform.localPosition = new Vector3(0f, 0f, depth);
            existing.transform.localRotation = Quaternion.identity;
            existing.transform.localScale = Vector3.one * (2f * radius / Mathf.Max(.0001f,
                Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y)));
            return existing;
        }

        private LabOrbBoard board;
        private bool boardLooked;

        /// <summary>
        /// 판 구슬 그림은 판을 비추는 카메라(판의 입력 카메라) 쪽만 본다. 예전처럼 "지금 그리는 카메라" 쪽으로 돌리면,
        /// 같은 구슬을 다른 카메라(전투 3D 카메라, 에디터 Scene 창 등)도 그릴 때 그쪽으로 돌아간 채 남아
        /// 판에서 옆으로 선 종이처럼 길쭉하게 보였다. 판 카메라를 모를 때만 예전 방식을 쓴다.
        /// </summary>
        private Camera BoardCamera
        {
            get
            {
                if (!boardLooked) { board = GetComponentInParent<LabOrbBoard>(); boardLooked = true; }
                return board != null ? board.ViewCamera : null;
            }
        }

        private void LateUpdate()
        {
            Camera view = BoardCamera;
            if (view != null) transform.rotation = view.transform.rotation;
        }

        private void OnWillRenderObject()
        {
            if (BoardCamera != null) return;
            if (Camera.current != null) transform.rotation = Camera.current.transform.rotation;
        }
    }
}
