using C6Lab;
using UnityEngine;
namespace MUSA.GourdPaint
{
    /// <summary>Proposal-only surface painting from host-approved hit notifications.</summary>
    public sealed class GourdPaintTarget : MonoBehaviour
    {
        public LabTarget target;
        public LabNetwork network;
        public GameObject visualPrefab;
        public Material paintMaterial;
        [Range(.05f,.3f)] public float splatRadius = .15f;
        const int Capacity=128;
        readonly Vector4[] positions=new Vector4[Capacity], colors=new Vector4[Capacity];
        MaterialPropertyBlock block;
        Renderer shell;
        string roundKey;
        public int PaintCount { get; private set; }
        public Transform Shell => shell != null ? shell.transform : null;
        void Start()
        {
            target.SetVisualPrefab(visualPrefab);
            var root=target.VisualMount.GetChild(target.VisualMount.childCount-1);
            shell=root.Find("White Gourd").GetComponent<Renderer>();
            shell.sharedMaterial=paintMaterial;
            block=new MaterialPropertyBlock();
            target.ApprovedHit+=OnHit;
            ClearPaint();
        }
        void Update()
        {
            var s=network!=null?network.Snapshot:null;
            string key=s!=null?s.sessionId+":"+s.round:null;
            if(key!=roundKey){roundKey=key;ClearPaint();}
        }
        void OnHit(Vector3 localPoint,LabOrbElement element)
        {
            Vector3 world=target.Hitbox.transform.TransformPoint(localPoint);
            Vector3 direction=shell.transform.InverseTransformPoint(world).normalized;
            if (element < LabOrbElement.Fire || element > LabOrbElement.Earth) return;
            var appearance = network != null && network.config != null ? network.config.OrbAppearance : null;
            Paint(direction, appearance != null ? appearance.ElementColorFor(element) : LabOrbAppearance.DefaultElementColor(element));
        }
        public static Color Palette(int index)
        {
            return LabOrbAppearance.DefaultElementColor((LabOrbElement)(Mathf.Clamp(index,0,4)+1));
        }
        public bool Paint(Vector3 direction,Color color)
        {
            if(shell==null||PaintCount>=Capacity||!Finite(direction)||direction.sqrMagnitude<.00001f)return false;
            Vector3 p=direction.normalized*.5f;
            positions[PaintCount]=new Vector4(p.x,p.y,p.z,splatRadius);
            colors[PaintCount]=color;PaintCount++;Apply();return true;
        }
        public void ClearPaint(){PaintCount=0;Apply();}
        void Apply()
        {
            if(shell==null)return;
            block.SetInt("_PaintCount",PaintCount);
            block.SetVectorArray("_PaintPositions",positions);block.SetVectorArray("_PaintColors",colors);
            shell.SetPropertyBlock(block);
        }
        static bool Finite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.x)&&!float.IsInfinity(p.y)&&!float.IsInfinity(p.z);
        void OnDestroy(){if(target!=null)target.ApprovedHit-=OnHit;}
        void OnGUI()
        {
            var s=network!=null?network.Snapshot:null;
            if(s!=null)return;
            GUI.Box(new Rect(12,12,260,110),"색칠 봉인 실험 · 제안용");
            if(GUI.Button(new Rect(22,42,115,32),"색 자국 미리보기"))
            {
                var cam=Camera.main;
                if(cam==null)foreach(var c in Camera.allCameras)if(c.name=="BattleCamera")cam=c;
                Vector3 facing=cam!=null?shell.transform.InverseTransformPoint(cam.transform.position).normalized:Vector3.forward;
                Vector3 offset=new Vector3(Mathf.Sin(PaintCount*2.4f),Mathf.Cos(PaintCount*1.7f),0)*.3f;
                Paint(facing+offset,Palette(PaintCount%5));
            }
            if(GUI.Button(new Rect(145,42,115,32),"색 초기화"))ClearPaint();
            GUI.Label(new Rect(22,83,245,28),"실제 투척: DEV → START SOLO");
        }
    }
}
