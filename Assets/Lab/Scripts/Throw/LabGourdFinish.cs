using UnityEngine;
namespace C6Lab
{
    /// <summary>One local replay per authoritative victory; gameplay ends immediately as before.</summary>
    [RequireComponent(typeof(LabGourdCrack))]
    public sealed class LabGourdFinish : MonoBehaviour
    {
        public const float Duration = 1.2f;
        LabGourdCrack crack;
        GameObject intact, left, right, spirit;
        Material spiritMaterial;
        Vector3 restPosition;
        string key;
        float elapsed;
        bool running;
        public bool Complete => running && elapsed >= Duration;
        public bool IsFinishing => running;
        void Awake() => crack = GetComponent<LabGourdCrack>();
        bool CanPresent => isActiveAndEnabled && crack != null && crack.IsReady;
        public static bool DelayVictory(LabNetwork network, LabSnapshot snapshot)
        {
            if (snapshot == null || snapshot.phase != LabPhase.Victory || network == null || network.target == null) return false;
            var finish = network.target.GetComponent<LabGourdFinish>();
            // An absent, disabled, or invalid presentation must never hold the result UI open.
            // A failed/cancelled attempt retains its round key and is not started again.
            return finish != null && finish.CanPresent && finish.crack.network == network
                && (finish.key != RoundKey(snapshot) || (finish.running && !finish.Complete));
        }
        static string RoundKey(LabSnapshot s) => s.sessionId + ":" + s.round;
        void Update()
        {
            var s = crack != null && crack.network != null ? crack.network.Snapshot : null;
            ObserveSnapshot(s, Time.unscaledDeltaTime);
        }
        public void ObserveSnapshot(LabSnapshot s, float delta)
        {
            if (s == null || s.phase != LabPhase.Victory)
            { if (running) ResetPresentation(); key = null; return; }
            string next = RoundKey(s);
            if (next != key) { ResetPresentation(); key = next; BeginPresentation(); }
            if (!CanPresent) { ResetPresentation(); return; }
            if (running) Advance(delta);
        }
        public void BeginPresentation()
        {
            if (running || !CanPresent) return;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return;
            intact = crack.VisualInstance;
            restPosition = intact.transform.localPosition;
            left = Half(intact, -1); right = Half(intact, 1);
            left.SetActive(false); right.SetActive(false);
            spirit = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spirit.name = "Small Yokai Spirit";
            spirit.transform.SetParent(crack.target.VisualMount, false);
            spirit.transform.position = crack.target.Hitbox.bounds.center;
            spirit.transform.localScale = Vector3.one * .34f;
            LabVisualSafety.RemovePhysics(spirit);
            spiritMaterial = new Material(shader);
            spiritMaterial.SetColor("_BaseColor", new Color(1f,.64f,.2f));
            spirit.GetComponent<Renderer>().sharedMaterial = spiritMaterial;
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Spirit core"; core.transform.SetParent(spirit.transform,false);
            core.transform.localScale = Vector3.one * .72f;
            core.transform.localPosition = Vector3.up * .3f;
            LabVisualSafety.RemovePhysics(core);
            core.GetComponent<Renderer>().sharedMaterial = crack.crackMaterial;
            spirit.SetActive(false); elapsed = 0; running = true;
        }
        GameObject Half(GameObject source, int side)
        {
            var copy = Instantiate(source, source.transform.parent);
            copy.name = side < 0 ? "Gourd Left Half" : "Gourd Right Half";
            Bounds bounds = crack.target.Hitbox.bounds;
            Vector3 axis = source.transform.parent.right;
            foreach (var r in copy.GetComponentsInChildren<Renderer>())
            {
                if (!System.Array.Exists(r.sharedMaterials, m => m == crack.crackMaterial)) { r.enabled = false; continue; }
                // Plane in mesh coordinates moves with the half, avoiding camera-facing slices.
                Vector3 n = r.localToWorldMatrix.transpose.MultiplyVector(axis).normalized;
                Vector3 p = r.transform.InverseTransformPoint(bounds.center);
                var block = new MaterialPropertyBlock(); r.GetPropertyBlock(block);
                block.SetFloat("_SplitSide", side);
                block.SetVector("_SplitPlane", new Vector4(n.x,n.y,n.z,-Vector3.Dot(n,p)));
                block.SetFloat("_CrackStage", 3); r.SetPropertyBlock(block);
            }
            return copy;
        }
        public void Advance(float delta)
        {
            if (!running) return;
            if (!CanPresent || intact == null || intact != crack.VisualInstance
                || left == null || right == null || spirit == null)
            {
                ResetPresentation();
                return;
            }
            if (float.IsNaN(delta)) delta = 0f;
            elapsed = Mathf.Min(Duration, elapsed + Mathf.Max(0, delta));
            if (elapsed < .2f)
            {
                foreach(var r in intact.GetComponentsInChildren<Renderer>())
                {
                    var block = new MaterialPropertyBlock();r.GetPropertyBlock(block);
                    block.SetFloat("_FinishGlow",elapsed/.2f);r.SetPropertyBlock(block);
                }
                intact.transform.localPosition = restPosition + Vector3.right * (Mathf.Sin(elapsed*90)*.025f);
                return;
            }
            intact.SetActive(false); left.SetActive(true); right.SetActive(true); spirit.SetActive(true);
            float t = Mathf.Clamp01((elapsed-.2f)/.8f); float ease = 1-Mathf.Pow(1-t,3);
            left.transform.localPosition = restPosition + new Vector3(-.7f*ease,-.55f*t*t,0);
            right.transform.localPosition = restPosition + new Vector3(.7f*ease,-.55f*t*t,0);
            left.transform.localRotation = intact.transform.localRotation * Quaternion.Euler(0,0,35*ease);
            right.transform.localRotation = intact.transform.localRotation * Quaternion.Euler(0,0,-35*ease);
            spirit.transform.localScale = Vector3.one * (.34f + .025f*Mathf.Sin(elapsed*9));
        }
        public void ResetPresentation()
        {
            if (intact != null)
            {
                intact.SetActive(true); intact.transform.localPosition = restPosition;
                foreach(var r in intact.GetComponentsInChildren<Renderer>())
                { var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);block.SetFloat("_FinishGlow",0);r.SetPropertyBlock(block); }
            }
            foreach(var item in new[]{left,right,spirit}) if(item != null){item.SetActive(false);Destroy(item);}
            if(spiritMaterial != null) Destroy(spiritMaterial);
            intact=left=right=spirit=null;spiritMaterial=null;running=false;elapsed=0;
        }
        void OnDisable() => ResetPresentation();
        void OnDestroy() => ResetPresentation();
    }
}
