using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// #498: draws private copies of what a battle first draws, under the opaque preparing cover, so Metal builds those
    /// render pipelines here and not on the first throw. Inactive pooled objects and shader collections do not do this; a
    /// real draw with the real camera and target does. Nothing here touches the board, the projectile root or the impact
    /// pool, and the copies are gone before the Host can begin the countdown because the initial-ready ACK waits for it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabBattleRenderWarmup : MonoBehaviour
    {
        /// <summary>Ribbons need two frames of movement before they have any geometry; two more cover the particles.</summary>
        public const int DrawFrames = 4, MinimumFrames = 2;
        /// <summary>Checked between frames: a shader build inside one frame cannot be interrupted.</summary>
        public const double SecondsLimit = 3d;
        /// <summary>The board's camera appears with the first snapshot; past this the world copies go without it.</summary>
        public const int CompositionWaitFrames = 30;

        private static readonly LabOrbElement[] Elements =
        {
            LabOrbElement.Fire, LabOrbElement.Water, LabOrbElement.Wood, LabOrbElement.Metal, LabOrbElement.Earth, LabOrbElement.None
        };
        /// <summary>Two steps must pass the widest motif spacing (.48 m), so even the last trail has its particles on screen for a frame.</summary>
        private const float StepMetres = .26f;
        private const string RootName = "Render warmup · private copies";

        private readonly List<LabProjectile> shots = new List<LabProjectile>();
        private readonly List<Transform> bursts = new List<Transform>();
        private readonly List<Transform> boardCopies = new List<Transform>();
        private readonly List<LabClayFlightSlot> clayShots = new List<LabClayFlightSlot>();
        private readonly List<LabClayImpactSlot> clayContacts = new List<LabClayImpactSlot>();
        private readonly List<Transform> claySurfaces = new List<Transform>();
        private LabClayShapes clayShapes;
        private LabNetwork network;
        private Camera battleCamera, boardCamera;
        private LabHud hud;
        private GameObject root;
        private Mesh dripMesh;
        private int waitedFrames, drawnFrames;
        private double startedAt;
        private bool requested;

        public bool Finished { get; private set; }
        public GameObject Root => root;
        public int DrawnFrames => drawnFrames;

        public static bool ShouldStop(int drawn, double elapsed) =>
            drawn >= DrawFrames || drawn >= MinimumFrames && elapsed >= SecondsLimit;

        public void Bind(LabNetwork owner, Camera worldCamera, LabHud display)
        {
            network = owner; battleCamera = worldCamera; hud = display;
        }

        /// <summary>
        /// Called each frame while the room waits for this device's initial-ready. Passing false, when the shared start budget
        /// is already short, ends it without drawing. Once finished it stays finished for this Scene.
        /// </summary>
        public void Request(bool mayDraw)
        {
            if (Finished || requested) return;
            if (!mayDraw || network == null || network.config == null || network.config.OrbAppearance == null
                || battleCamera == null) { Finished = true; return; }
            requested = true;
            startedAt = Time.realtimeSinceStartupAsDouble;
        }

        private void Update()
        {
            if (Finished || !requested) return;
            double elapsed = Time.realtimeSinceStartupAsDouble - startedAt;
            if (root == null)
            {
                var composition = hud != null ? hud.GetComponent<LabOrbHudComposition>() : null;
                bool boardReady = composition != null && composition.IsCompositing;
                bool boardFailed = composition != null && !string.IsNullOrEmpty(composition.InitializationError);
                if (!boardReady && !boardFailed && ++waitedFrames < CompositionWaitFrames && elapsed < SecondsLimit) return;
                boardCamera = boardReady ? composition.ForegroundCamera : null;
                using (LabFirstUse.Measure(LabFirstUseKind.RenderWarmup)) Spawn();
                startedAt = Time.realtimeSinceStartupAsDouble;
                return;
            }
            drawnFrames++;
            if (ShouldStop(drawnFrames, elapsed)) { Finish(); return; }
            MoveCopies();
        }

        private void Spawn()
        {
            root = new GameObject(RootName);
            LabConfig config = network.config;
            foreach (LabOrbElement element in Elements)
            {
                LabProjectile shot = LabProjectile.Create(WorldPoint(shots.Count, 0), config, root.transform, element);
                shot.BeginPuppet(config, false);
                shots.Add(shot);
            }
            LabImpactParticleVfx impacts = FindAnyObjectByType<LabImpactParticleVfx>();
            if (impacts != null && impacts.EffectPrefab != null)
                foreach (LabOrbElement element in Elements)
                {
                    LabImpactParticleBurst burst = Instantiate(impacts.EffectPrefab, WorldPoint(bursts.Count, 0), Quaternion.identity, root.transform);
                    burst.gameObject.SetActive(true);
                    burst.Begin(LabOrbVisualFactory.TemporaryElementColor(element), battleCamera.transform.forward, battleCamera);
                    bursts.Add(burst.transform);
                }
            SpawnShakeArtCopies();
            SpawnReleaseBubbleCopy(LabGourdAlert.YaguangReleasePath, -60f);
            SpawnReleaseBubbleCopy(LabGourdAlert.YaguangTauntPath, 60f);
            int artIndex = 0;
            foreach (string name in new[] { "CountOne", "CountTwo", "CountThree", "CountFour", "Panel", "Earth", "Wood", "Fire", "Water", "Metal", "Check", "BarFill", "SuccessStamp" })
                SpawnReleaseBubbleCopy(LabYaguangComboArt.Path + name, (artIndex++ % 5 - 2) * 45f);
            SpawnClayCopies(config);
            if (boardCamera != null) SpawnBoardCopies(config.OrbAppearance);
            MoveCopies();
        }

        private void SpawnShakeArtCopies()
        {
            var copy = new GameObject("Shake UI warmup", typeof(RectTransform), typeof(Canvas));
            copy.transform.SetParent(root.transform, false);
            var canvas = copy.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = battleCamera;
            canvas.planeDistance = battleCamera.nearClipPlane + .1f;
            var lightObject = new GameObject("HDR Flash warmup", typeof(RectTransform));
            lightObject.transform.SetParent(copy.transform, false);
            var lightRect = (RectTransform)lightObject.transform;
            lightRect.anchorMin = Vector2.zero;
            lightRect.anchorMax = Vector2.one;
            lightRect.offsetMin = lightRect.offsetMax = Vector2.zero;
            lightObject.AddComponent<LabYaguangFlashLight>().Present(.22f, 0f, 0f);
            int index = 0;
            foreach (string name in new[] { "Phone", "Title", "Guide", "GaugeTrack", "CellFull", "CellEmpty", "Countdown1", "Countdown2", "Countdown3", "EyesNormal", "EyesYellow" })
            {
                var image = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
                image.transform.SetParent(copy.transform, false);
                image.sprite = Resources.Load<Sprite>(LabYaguangShakeCue.ArtPath + name);
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.rectTransform.sizeDelta = new Vector2(100f, 100f);
                image.rectTransform.anchoredPosition = new Vector2((index % 6 - 3) * 70f, (index++ / 6) * 110f);
            }
        }

        private void SpawnReleaseBubbleCopy(string resourcePath, float x)
        {
            Sprite artwork = Resources.Load<Sprite>(resourcePath);
            if (artwork == null) return;
            var copy = new GameObject("Release bubble warmup", typeof(RectTransform), typeof(Canvas));
            copy.transform.SetParent(root.transform, false);
            var canvas = copy.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = battleCamera;
            canvas.planeDistance = battleCamera.nearClipPlane + .1f;
            var picture = new GameObject("Artwork", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            picture.transform.SetParent(copy.transform, false);
            var image = picture.GetComponent<UnityEngine.UI.Image>();
            image.sprite = artwork;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.rectTransform.sizeDelta = new Vector2(110f, 110f);
            image.rectTransform.anchoredPosition = new Vector2(x, 0f);
        }

        private void SpawnClayCopies(LabConfig config)
        {
            var profile = Resources.Load<LabFiveElementProfile>("FiveElements/Profile");
            var shader = Resources.Load<Shader>("FiveElements/LabWetPigment");
            if (profile == null || shader == null || !shader.isSupported) return;
            // Use the real slot geometry without registering a Scene service or consuming a gameplay pool seed.
            // PaperOrb's body and WetPigment's stretched trail/contact have distinct render pipelines.
            var copies = new GameObject("Clay copies").transform;
            copies.SetParent(root.transform, false);
            clayShapes = new LabClayShapes(shader);
            float diameter = Mathf.Clamp(config.ThrowRadius * 2f, .006f, 5f);
            for (int i = 0; i < Elements.Length; i++)
            {
                bool fiveColor = Elements[i] == LabOrbElement.None;
                uint seed = 37219u + (uint)i;
                Vector3 incoming = battleCamera.transform.forward;
                var flight = new LabClayFlightSlot(copies, i, clayShapes, profile);
                flight.Begin(Elements[i], ClayPoint(i, 0, false), incoming, diameter,
                    profile.normalStrength, false, seed, false, fiveColor: fiveColor);
                clayShots.Add(flight);
                var surface = new GameObject("Private contact anchor " + i).transform;
                surface.SetParent(copies, false);
                surface.position = ClayPoint(i, 0, true);
                claySurfaces.Add(surface);
                var contact = new LabClayImpactSlot(copies, i, clayShapes, profile);
                contact.Begin(Elements[i], surface.position, -incoming, incoming, diameter,
                    profile.normalStrength, surface, false, 0f, seed, false, fiveColor: fiveColor);
                // Already spread, with drops visible, even when the shared game clock is held during preparation.
                // The squash uses the same PaperOrb mesh/material as the flight body above.
                contact.Tick(.12f);
                clayContacts.Add(contact);
            }
        }

        private Vector3 ClayPoint(int index, int frame, bool contact) => battleCamera.ViewportToWorldPoint(new Vector3(
            index == Elements.Length - 1 ? .5f : .12f + index * .17f,
            index == Elements.Length - 1 ? (contact ? .84f : .48f) : (contact ? .67f : .31f),
            Mathf.Max(battleCamera.nearClipPlane + 2f, 3f)))
            + battleCamera.transform.right * (frame * StepMetres * .35f);

        private void SpawnBoardCopies(LabOrbAppearance appearance)
        {
            int layer = LayerMask.NameToLayer("LabOrbBoard");
            if (appearance.BombVisualPrefab != null)
            {
                LabBombVisual bomb = Instantiate(appearance.BombVisualPrefab);
                bomb.ShowCount("4");
                bomb.SetLit(true);
                bomb.Pose(appearance.BombCapAngle, 1f, appearance.BombCapAngle, 1f);
                AddBoardCopy(bomb.gameObject, layer);
            }
            Sprite sprite = appearance.SpriteFor(LabOrbKind.Yin);
            if (sprite != null)
            {
                var art = new GameObject("Sprite");
                art.AddComponent<SpriteRenderer>().sprite = sprite;
                AddBoardCopy(art, layer);
            }
            if (appearance.ShadowMaterial != null)
            {
                var shadow = new GameObject("Contact shadow");
                shadow.AddComponent<MeshFilter>().sharedMesh = LabOrbVisualFactory.GetShadowQuad();
                var surface = shadow.AddComponent<MeshRenderer>();
                surface.sharedMaterial = appearance.ShadowMaterial;
                surface.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                surface.receiveShadows = false;
                AddBoardCopy(shadow, layer);
            }
            if (appearance.VisualPrefab != null)
            {
                GameObject sphere = Instantiate(appearance.VisualPrefab);
                foreach (Collider collider in sphere.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                AddBoardCopy(sphere, layer);
            }
            var profile = Resources.Load<LabOrbDripProfile>(LabOrbDripProfile.ResourcesPath);
            if (profile != null && profile.Material != null)
            {
                // The same four vertex channels as a real drip, which is a different pipeline from a plain quad.
                dripMesh = new Mesh { name = "Render warmup drip" };
                dripMesh.SetVertices(new[] { new Vector3(-1f, -1f), new Vector3(1f, -1f), new Vector3(1f, 1f), new Vector3(-1f, 1f) });
                dripMesh.SetUVs(0, new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
                dripMesh.SetUVs(1, new[] { Vector4.zero, Vector4.zero, Vector4.zero, Vector4.zero });
                dripMesh.SetColors(new[] { Color.white, Color.white, Color.white, Color.white });
                dripMesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
                dripMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 4f);
                var drip = new GameObject("Drip");
                drip.AddComponent<MeshFilter>().sharedMesh = dripMesh;
                var surface = drip.AddComponent<MeshRenderer>();
                surface.sharedMaterial = profile.Material;
                surface.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                surface.receiveShadows = false;
                AddBoardCopy(drip, layer);
            }
        }

        private void AddBoardCopy(GameObject copy, int layer)
        {
            copy.transform.SetParent(root.transform, false);
            foreach (Transform part in copy.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = layer;
            boardCopies.Add(copy.transform);
        }

        private void MoveCopies()
        {
            for (int i = 0; i < shots.Count; i++) if (shots[i] != null) shots[i].transform.position = WorldPoint(i, drawnFrames);
            for (int i = 0; i < bursts.Count; i++) if (bursts[i] != null) bursts[i].position = WorldPoint(i, drawnFrames);
            for (int i = 0; i < clayShots.Count; i++)
            {
                clayShots[i].Move(ClayPoint(i, drawnFrames, false), battleCamera.transform.right);
                clayShots[i].Tick(1f / 60f);
                claySurfaces[i].position = ClayPoint(i, drawnFrames, true);
                clayContacts[i].Tick(0f);
            }
            if (boardCamera == null) return;
            float depth = (boardCamera.nearClipPlane + boardCamera.farClipPlane) * .5f;
            for (int i = 0; i < boardCopies.Count; i++)
            {
                float x = (i + 1f) / (boardCopies.Count + 1f);
                boardCopies[i].SetPositionAndRotation(boardCamera.ViewportToWorldPoint(new Vector3(x, .5f, depth)),
                    boardCamera.transform.rotation);
            }
        }

        // Relative to the live camera each frame, because the seat camera can still settle; off-screen copies draw nothing.
        private Vector3 WorldPoint(int index, int frame) => battleCamera.ViewportToWorldPoint(new Vector3(
            .05f + index * .1f, .5f, Mathf.Max(battleCamera.nearClipPlane + 2f, 3f)))
            + battleCamera.transform.right * (frame * StepMetres);

        private void Finish()
        {
            Finished = true;
            ReleaseCopies();
        }

        private void ReleaseCopies()
        {
            shots.Clear(); bursts.Clear(); boardCopies.Clear();
            foreach (var flight in clayShots) flight.Return();
            foreach (var contact in clayContacts) contact.Return();
            clayShots.Clear(); clayContacts.Clear(); claySurfaces.Clear();
            if (root != null) { root.SetActive(false); Destroy(root); root = null; }
            if (dripMesh != null) { Destroy(dripMesh); dripMesh = null; }
            clayShapes?.Dispose(); clayShapes = null;
        }

        private void OnDisable()
        {
            if (requested) Finished = true;
            ReleaseCopies();
        }
    }
}
