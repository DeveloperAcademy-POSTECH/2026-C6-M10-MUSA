using System;
using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// Keeps the saved Overlay layout and input camera intact, while compositing the board's
    /// foreground between its rear HUD decorations and the rest of the interface.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(500)]
    public sealed class LabOrbHudComposition : MonoBehaviour
    {
        public const string PaperLayerName = "LabBoardPaper";
        public const string CompositeShaderResource = "OrbHud/OrbHudComposite";
        private const string ForegroundName = "OrbHudForeground";

        private LabHud hud;
        private LabOrbBoard board;
        private Camera source;
        private Camera foreground;
        private UnityEngine.UI.RawImage display;
        private RenderTexture texture;
        private Material material;
        private readonly List<Transform> originalSiblings = new List<Transform>();
        private readonly Dictionary<GameObject, int> rearLayers = new Dictionary<GameObject, int>();
        private int sourceMask, battleMask, foregroundMask;
        private Camera capturedBattleCamera;
        private bool captured, failed;
        private string initializationError;

        public bool IsCompositing => captured && texture != null && foreground != null;
        public Camera ForegroundCamera => foreground;
        public UnityEngine.UI.RawImage Display => display;
        public RenderTexture Texture => texture;
        public string InitializationError => initializationError;

        public static void Attach(LabHud target, LabOrbBoard orbBoard, Camera inputCamera)
        {
            if (target == null || orbBoard == null || inputCamera == null) return;
            var composition = target.GetComponent<LabOrbHudComposition>();
            if (composition == null) composition = target.gameObject.AddComponent<LabOrbHudComposition>();
            composition.Bind(target, orbBoard, inputCamera);
        }

        public void Bind(LabHud target, LabOrbBoard orbBoard, Camera inputCamera)
        {
            if (hud == target && board == orbBoard && source == inputCamera) return;
            Release();
            hud = target;
            board = orbBoard;
            source = inputCamera;
            failed = false;
            initializationError = null;
        }

        private void LateUpdate()
        {
            if (failed) return;
            if (hud == null || board == null || source == null || !source.isActiveAndEnabled
                || hud.battleUi == null || !hud.battleUi.activeInHierarchy)
            {
                Release();
                return;
            }
            try
            {
                if (!captured) Initialize();
                ResizeTarget();
                SyncCamera();
                PlaceDisplay();
            }
            catch (Exception error)
            {
                Release();
                failed = true;
                initializationError = error.Message;
                Debug.LogWarning("C6_LAB_ORB_HUD_FALLBACK " + error.Message, this);
            }
        }

        private void Initialize()
        {
            Canvas canvas = hud.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                || source.targetTexture != null || !source.orthographic)
                throw new InvalidOperationException("Orb HUD composition requires the saved Overlay Canvas and on-screen orthographic input camera.");
            Transform parent = hud.battleUi.transform;
            Transform stamina = hud.staminaFill != null ? hud.staminaFill.transform.parent : null;
            Transform left = hud.leftNeighborBadge != null ? hud.leftNeighborBadge.transform : null;
            Transform right = hud.rightNeighborBadge != null ? hud.rightNeighborBadge.transform : null;
            if (stamina == null || left == null || right == null || stamina.parent != parent
                || left.parent != parent || right.parent != parent)
                throw new InvalidOperationException("The three rear HUD decorations must retain their saved BattleUi parent.");
            Transform paper = board.transform.Find("OrbBoardPlate");
            if (paper == null || paper.GetComponent<Renderer>() == null)
                throw new InvalidOperationException("The saved board paper renderer is missing.");
            int paperLayer = LayerMask.NameToLayer(PaperLayerName);
            if (paperLayer < 0 || paperLayer == board.gameObject.layer)
                throw new InvalidOperationException("The dedicated board-paper layer is missing.");
            Shader shader = Resources.Load<Shader>(CompositeShaderResource);
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException("The orb foreground composite shader is unavailable.");

            // Capture before any mutation, so allocation and rendering failures leave the normal board usable.
            sourceMask = source.cullingMask;
            capturedBattleCamera = hud.battleCamera;
            if (capturedBattleCamera != null) battleMask = capturedBattleCamera.cullingMask;
            foregroundMask = sourceMask & ~(1 << paperLayer);
            if ((foregroundMask & (1 << board.gameObject.layer)) == 0)
                throw new InvalidOperationException("The input camera must see the orb-board foreground layer.");
            for (int i = 0; i < parent.childCount; i++) originalSiblings.Add(parent.GetChild(i));
            RememberRear(paper);
            Transform backdrop = source.transform.Find("Temporary Board Background");
            if (backdrop != null) RememberRear(backdrop);
            captured = true;

            material = new Material(shader) { name = "Orb HUD foreground composite", hideFlags = HideFlags.HideAndDontSave };
            var cameraObject = new GameObject("Orb HUD display camera") { hideFlags = HideFlags.HideAndDontSave };
            cameraObject.transform.SetParent(transform, false);
            foreground = cameraObject.AddComponent<Camera>();
            foreground.enabled = false;
            var imageObject = new GameObject(ForegroundName, typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            imageObject.hideFlags = HideFlags.HideAndDontSave;
            imageObject.layer = parent.gameObject.layer;
            imageObject.transform.SetParent(parent, false);
            display = imageObject.GetComponent<UnityEngine.UI.RawImage>();
            display.raycastTarget = false;
            display.material = material;
            display.color = Color.white;

            var rear = new List<Transform> { stamina, left, right };
            rear.Sort((a, b) => a.GetSiblingIndex().CompareTo(b.GetSiblingIndex()));
            for (int i = 0; i < rear.Count; i++) rear[i].SetSiblingIndex(i);
            display.transform.SetSiblingIndex(rear.Count);
            ResizeTarget();
            SyncCamera();
            PlaceDisplay();
            foreach (GameObject item in rearLayers.Keys) item.layer = paperLayer;
            source.cullingMask = 1 << paperLayer;
            if (capturedBattleCamera != null) capturedBattleCamera.cullingMask &= ~(1 << paperLayer);
            foreground.enabled = true;
        }

        private void RememberRear(Transform root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                if (!rearLayers.ContainsKey(renderer.gameObject))
                    rearLayers.Add(renderer.gameObject, renderer.gameObject.layer);
        }

        private void ResizeTarget()
        {
            Rect rect = source.pixelRect;
            int width = Mathf.Max(1, Mathf.RoundToInt(rect.width));
            int height = Mathf.Max(1, Mathf.RoundToInt(rect.height));
            if (texture != null && texture.width == width && texture.height == height)
            {
                if (!texture.IsCreated()) throw new InvalidOperationException("The orb foreground render target was lost.");
                return;
            }
            var replacement = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "Orb HUD foreground", hideFlags = HideFlags.HideAndDontSave,
                antiAliasing = 1, useMipMap = false, autoGenerateMips = false,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            if (!replacement.Create())
            {
                DestroyOwned(replacement);
                throw new InvalidOperationException("The orb foreground render target could not be allocated.");
            }
            if (foreground != null) foreground.targetTexture = replacement;
            if (display != null) display.texture = replacement;
            if (texture != null) { texture.Release(); DestroyOwned(texture); }
            texture = replacement;
        }

        private void SyncCamera()
        {
            // CopyFrom deliberately targets only Camera; this new camera has no post-processing or
            // additional renderer features. It never becomes the board's input camera.
            foreground.CopyFrom(source);
            foreground.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            foreground.targetTexture = texture;
            foreground.rect = new Rect(0f, 0f, 1f, 1f);
            foreground.aspect = source.aspect;
            foreground.worldToCameraMatrix = source.worldToCameraMatrix;
            foreground.projectionMatrix = source.projectionMatrix;
            foreground.cullingMask = foregroundMask;
            foreground.clearFlags = CameraClearFlags.SolidColor;
            foreground.backgroundColor = Color.clear;
            foreground.allowHDR = false;
            foreground.allowMSAA = false;
            foreground.depth = source.depth - 1f;
            foreground.enabled = true;
        }

        private void PlaceDisplay()
        {
            var parent = (RectTransform)hud.battleUi.transform;
            Rect screen = source.pixelRect;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen.min, null, out Vector2 min)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen.max, null, out Vector2 max))
                throw new InvalidOperationException("The orb viewport could not be mapped to the saved Overlay layout.");
            RectTransform rect = display.rectTransform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = min - parent.rect.min;
            rect.sizeDelta = max - min;
        }

        private void OnDisable() => Release();
        private void OnDestroy() => Release();

        private void Release()
        {
            if (foreground != null)
            {
                foreground.enabled = false;
                foreground.targetTexture = null;
                foreground.gameObject.SetActive(false);
                DestroyOwned(foreground.gameObject);
            }
            foreground = null;
            if (display != null)
            {
                display.gameObject.SetActive(false);
                display.texture = null;
                DestroyOwned(display.gameObject);
            }
            display = null;
            if (texture != null) { texture.Release(); DestroyOwned(texture); }
            texture = null;
            if (material != null) DestroyOwned(material);
            material = null;
            if (captured)
            {
                if (source != null) source.cullingMask = sourceMask;
                if (capturedBattleCamera != null) capturedBattleCamera.cullingMask = battleMask;
                foreach (var entry in rearLayers)
                    if (entry.Key != null) entry.Key.layer = entry.Value;
                for (int i = 0; i < originalSiblings.Count; i++)
                    if (originalSiblings[i] != null && hud != null && hud.battleUi != null
                        && originalSiblings[i].parent == hud.battleUi.transform)
                        originalSiblings[i].SetSiblingIndex(i);
            }
            captured = false;
            capturedBattleCamera = null;
            rearLayers.Clear();
            originalSiblings.Clear();
        }

        private static void DestroyOwned(UnityEngine.Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
