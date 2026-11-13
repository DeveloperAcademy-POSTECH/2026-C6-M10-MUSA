using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace C6Lab
{
    /// <summary>Scene-local visual leases. The caller supplies the real flight path and confirmed contact.</summary>
    [DisallowMultipleComponent]
    public sealed class LabFiveElementVfx : MonoBehaviour
    {
        public readonly struct Flight
        {
            private readonly LabFiveElementVfx owner;
            private readonly int index, version;

            internal Flight(LabFiveElementVfx owner, int index, int version)
            {
                this.owner=owner; this.index=index; this.version=version;
            }

            public bool IsActive => owner != null && owner.IsCurrent(index, version);

            public void Move(Vector3 position, Vector3 direction)
            {
                if (IsActive && Finite(position) && Finite(direction)) owner.flights[index].Move(position, direction);
            }

            public bool TintFiveColor(float age, Color32[] palette)
            {
                return IsActive && Finite(age) && owner.flights[index].TintFiveColor(age,palette);
            }

            public bool Hit(Vector3 point, Vector3 normal, Vector3 incoming, Transform target = null, bool leaveMark = true)
            {
                if (!IsActive || !Finite(point) || !Finite(normal) || !Finite(incoming)) return false;
                var flight = owner.flights[index];
                bool fiveColor=flight.FiveColor;
                float paletteAge=flight.FiveColorAge;
                Color32[] palette=flight.FiveColorPalette;
                bool enhance=flight.EnhancedFiveColor;
                flight.Return();
                return owner.PlayImpactCore(flight.Element, point, normal, incoming, flight.Diameter, flight.Strength,
                    target, leaveMark, flight.Monochrome, fiveColor, paletteAge, palette, enhance);
            }

            public void Cancel()
            {
                if (IsActive) owner.flights[index].Return();
            }
        }

        private static readonly Dictionary<Scene, LabFiveElementVfx> Services = new Dictionary<Scene, LabFiveElementVfx>();
        private readonly List<LabClayFlightSlot> flights = new List<LabClayFlightSlot>();
        private readonly List<LabClayImpactSlot> hits = new List<LabClayImpactSlot>();
        private LabClayShapes shapes;
        private bool ownsProfile;
        private bool registerSceneService = true;
        private Scene sceneKey;
        private uint nextSeed = 37219u;
        public LabFiveElementProfile Profile { get; private set; }
        public bool ManualClock { get; set; }
        public bool LowDetail { get; set; }
        /// <summary>Local comparison override. Each lease captures this; no game configuration or network state changes.</summary>
        public bool FiveColorBoostEnabled { get; set; } = true;
        public bool UsesSimpleMaterial { get; private set; }
        public float MarkLifetimeOverride { get; set; } = -1f;
        public int ActiveFlightCount
        {
            get { int count=0; foreach (var f in flights) if (f.Active) ++count; return count; }
        }
        public int ActiveImpactCount
        {
            get { int count=0; foreach (var h in hits) if (h.Active) ++count; return count; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetServices() => Services.Clear();

        public static LabFiveElementVfx ForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            if (Services.TryGetValue(scene, out var service) && service != null) return service;
            var root = new GameObject("Five Element Clay VFX");
            SceneManager.MoveGameObjectToScene(root, scene);
            return root.AddComponent<LabFiveElementVfx>();
        }

        /// <summary>A presentation-owned pool with its own clock; never replaces or clears the battle's scene service.</summary>
        public static LabFiveElementVfx CreateManual(Transform parent)
        {
            if (parent == null) return null;
            var root = new GameObject("Final hit replay · clay VFX");
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            var service = root.AddComponent<LabFiveElementVfx>();
            service.registerSceneService = false;
            service.ManualClock = true;
            root.SetActive(true);
            return service;
        }

        private void Awake()
        {
            sceneKey = gameObject.scene;
            if (registerSceneService) Services[sceneKey] = this;
            Profile = Resources.Load<LabFiveElementProfile>("FiveElements/Profile");
            if (Profile == null)
            {
                ownsProfile = true;
                Profile = ScriptableObject.CreateInstance<LabFiveElementProfile>();
            }
            var shader = Resources.Load<Shader>("FiveElements/LabWetPigment");
            if (shader != null && shader.isSupported) shapes = new LabClayShapes(shader);
        }

        public Flight BeginFlight(LabOrbElement element, Vector3 position, Vector3 direction,
            float diameter, float strength = .3f, bool monochrome = false, bool fiveColor = false)
        {
            if (!Available(element, diameter, fiveColor) || !Finite(position) || !Finite(direction) || !Finite(strength)) return default;
            int i = 0;
            while (i < flights.Count && flights[i].Active) ++i;
            if (i == flights.Count)
            {
                if (i >= Profile.maxFlights) return default;
                flights.Add(new LabClayFlightSlot(transform, i, shapes, Profile));
            }
            flights[i].Begin(element, position, direction, diameter, Mathf.Clamp01(strength), monochrome, Seed(), LowDetail, fiveColor, FiveColorBoostEnabled);
            return new Flight(this, i, flights[i].Version);
        }

        public bool PlayImpact(LabOrbElement element, Vector3 point, Vector3 normal, Vector3 incoming,
            float diameter, float strength, Transform target = null, bool leaveMark = true, bool monochrome = false,
            bool fiveColor = false, float fiveColorAge = 0f, Color32[] fiveColorPalette = null)
        {
            float paletteAge=Finite(fiveColorAge) ? Mathf.Max(0f,fiveColorAge) : 0f;
            // #713: the Jigwi black orb passes its soot tones; null keeps the five colours.
            return PlayImpactCore(element,point,normal,incoming,diameter,strength,target,leaveMark,monochrome,fiveColor,paletteAge,fiveColorPalette,FiveColorBoostEnabled);
        }

        private bool PlayImpactCore(LabOrbElement element, Vector3 point, Vector3 normal, Vector3 incoming,
            float diameter, float strength, Transform target, bool leaveMark, bool monochrome,
            bool fiveColor, float paletteAge, Color32[] palette, bool enhanceFiveColor)
        {
            if (!Available(element, diameter, fiveColor) || !Finite(point) || !Finite(normal) || !Finite(incoming) || !Finite(strength)) return false;
            int limit = LowDetail ? Mathf.Min(Profile.lowDetailImpacts, Profile.maxImpacts) : Profile.maxImpacts;
            limit = Mathf.Max(1, limit);
            // Prefer a returned slot, then replace the oldest visible stain at the cap.
            int use = 0;
            while (use < hits.Count && hits[use].Active)
                ++use;
            if (ActiveImpactCount >= limit) use = OldestActiveImpact();
            else if (use == hits.Count) hits.Add(new LabClayImpactSlot(transform, use, shapes, Profile));
            float life = MarkLifetimeOverride >= 0 ? MarkLifetimeOverride : Profile.markSeconds;
            hits[use].Begin(element, point, normal, incoming, diameter, Mathf.Clamp01(strength),
                target, monochrome, leaveMark ? Mathf.Max(0f, life) : 0f, Seed(), LowDetail, fiveColor, enhanceFiveColor);
            if (fiveColor) hits[use].TintFiveColor(paletteAge,palette);
            return true;
        }

        private int OldestActiveImpact()
        {
            int index = -1;
            for (int i=0; i<hits.Count; ++i)
                if (hits[i].Active && (index < 0 || hits[i].Age > hits[index].Age)) index = i;
            return index;
        }

        private bool Available(LabOrbElement element, float diameter, bool fiveColor)
        {
            if (!isActiveAndEnabled || !Finite(diameter) || diameter <= 0f ||
                !((element >= LabOrbElement.Fire && element <= LabOrbElement.Earth) ||
                    (fiveColor && element == LabOrbElement.None))) return false;
            // A missing wet shader must not revive the old five-color dust/stars. Keep the same pooled
            // clay geometry and sampled clock with plain pigment if the pipeline's unlit shader is available.
            if (shapes == null && fiveColor)
            {
                var simple = Shader.Find("Universal Render Pipeline/Unlit");
                if (simple != null && simple.isSupported)
                {
                    shapes = new LabClayShapes(simple);
                    UsesSimpleMaterial = true;
                }
            }
            return shapes != null && (!UsesSimpleMaterial || fiveColor);
        }

        private bool IsCurrent(int index, int generation)
            => index >= 0 && index < flights.Count && flights[index].Active && flights[index].Version == generation;

        private uint Seed()
        {
            nextSeed = unchecked(1664525u*nextSeed + 1013904223u);
            return nextSeed;
        }

        private void LateUpdate()
        {
            if (!ManualClock) Advance(Time.deltaTime);
        }

        public void Advance(float dt)
        {
            if (dt < 0f || float.IsInfinity(dt) || float.IsNaN(dt)) return;
            foreach (var f in flights) if (f.Active) f.Tick(dt);
            foreach (var h in hits) if (h.Active) h.Tick(dt);
        }

        public void ClearAll()
        {
            foreach (var f in flights) f.Return();
            foreach (var h in hits) h.Return();
            nextSeed = 37219u;
        }

        private void OnDisable() => ClearAll();

        private void OnDestroy()
        {
            if (Services.TryGetValue(sceneKey, out var current) && current == this) Services.Remove(sceneKey);
            shapes?.Dispose();
            if (ownsProfile && Profile != null) Destroy(Profile);
        }

        private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        private static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);

        internal static Vector3 Direction(Vector3 value, Vector3 fallback) =>
            value.sqrMagnitude > .000001f ? value.normalized : fallback;
    }
}
