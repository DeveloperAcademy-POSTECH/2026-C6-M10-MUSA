using UnityEngine;

namespace C6Lab
{
    /// <summary>A non-interactive arrow on the orb-camera layer, shown only for an armed manual pass.</summary>
    [DisallowMultipleComponent]
    public sealed class LabOrbTransferGuide : MonoBehaviour
    {
        private LabOrbBoard board;
        private SpriteRenderer arrow;
        private static Sprite sprite;
        public bool Visible => arrow != null && arrow.enabled;

        public static void Ensure(LabOrbBoard owner)
        {
            if (owner == null) return;
            var guide = owner.GetComponent<LabOrbTransferGuide>();
            if (guide == null) guide = owner.gameObject.AddComponent<LabOrbTransferGuide>();
            guide.board = owner;
        }

        private void LateUpdate()
        {
            if (board == null || !board.isActiveAndEnabled
                || !board.TryGetTransferGuide(out Vector2 position, out bool right))
            {
                if (arrow != null) arrow.enabled = false;
                return;
            }
            if (arrow == null)
            {
                var part = new GameObject("Transfer Direction Guide");
                part.layer = board.gameObject.layer;
                part.transform.SetParent(board.transform, false);
                arrow = part.AddComponent<SpriteRenderer>();
                arrow.sprite = GetSprite();
                arrow.sortingOrder = 160;
            }
            arrow.gameObject.layer = board.gameObject.layer;
            arrow.enabled = true;
            arrow.flipX = !right;
            float size = board.CenterBounds.width * .045f;
            // Draw in front of the sphere depth as well as above sprites in sorting order.
            arrow.transform.position = new Vector3(position.x, position.y, -size * 3f);
            arrow.transform.localScale = new Vector3(size, size, 1f);
            arrow.color = new Color(1f, .88f, .52f, .9f);
        }

        private void OnDisable() { if (arrow != null) arrow.enabled = false; }

        private static Sprite GetSprite()
        {
            if (sprite != null) return sprite;
            const int size = 48;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = (x + .5f) / size, py = (y + .5f) / size;
                    // A compact right-facing chevron; its transparent surround never intercepts input.
                    float tip = .78f - Mathf.Abs(py - .5f) * 1.3f;
                    float inner = .43f - Mathf.Abs(py - .5f) * 1.3f;
                    if (py > .12f && py < .88f && px <= tip && px >= inner)
                        pixels[y * size + x] = new Color32(255, 255, 255, 255);
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "Lab pass direction", hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
            sprite.name = "Lab pass direction";
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
