using System;
using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    [DisallowMultipleComponent]
    public sealed class LabBackgroundStudio : MonoBehaviour
    {
        [Serializable]
        public sealed class Decoration
        {
            public string title = "New decoration";
            public bool visible = true;
            public string category = "Decorations";
            [Range(0,1)] public float opacity = 1;
            [Range(0,2)] public float thickness = .15f;
            [Range(1,5)] public int depthLayers = 3;
            public bool crossPlane;
            public bool billboard;
            public int sortingOrder;
            [Range(0,.2f)] public float windAmount;
            [Range(.1f,4)] public float windSpeed = 1;
            public float boxHeight;
            [HideInInspector] public Transform group;
            [HideInInspector] public Material material;
            [HideInInspector] public string stableId;
            [HideInInspector] public List<Transform> cards = new List<Transform>();
            public Texture2D png;
            public Color tint = Color.white;
            [Range(1,32)] public int count = 1;
            [Min(9)] public float radius = 20;
            public float height = 2;
            [Min(.1f)] public float width = 8;
            [Range(0,360)] public float angle;
            [Range(0,.5f)] public float sizeVariation = .08f;
            public bool alternateFlip;
            [Range(.01f,.99f)] public float alphaCutoff = .25f;
        }

        [Header("기준 / References")]
        public Transform target;
        public Transform collisionFloor;
        public Vector3 center = new Vector3(0,1.35f,10);
        [Header("바닥 / Ground")]
        public bool showGround = true;
        public Texture2D groundPng;
        public Color groundColor = Color.white;
        public Vector2 groundSize = new Vector2(40,40);
        public Vector2 groundTiling = new Vector2(3,3);
        public Vector2 groundOffset;
        public float groundHeightOffset = .01f;
        public Texture2D groundNormal;
        [Range(0,2)] public float normalStrength = 1;
        [Range(0,1)] public float smoothness = .05f;
        [Header("한지 배경 / Surround")]
        public bool showSurround = true;
        public Texture2D paperPng;
        public Color paperColor = Color.white;
        [Range(25,38)] public float surroundRadius = 30;
        public Vector2 paperTiling = new Vector2(8,4);
        [Header("보드 위 빈틈 채우기 / Board Backing")]
        public bool fillBoardBackground = true;
        public Camera boardCamera;
        public Texture2D boardBackgroundPng;
        public Color boardBackgroundColor = Color.white;
        [Header("원형 장식 / PNG Decorations")]
        public List<Decoration> decorations = new List<Decoration>();
        [SerializeField, HideInInspector] Transform generated;
        [SerializeField, HideInInspector] Transform generatedGround;
        [SerializeField, HideInInspector] Transform generatedBoardBacking;
        public Vector3 Center { get { return target != null ? target.position : center; } }

#if UNITY_EDITOR
        public void ApplyBackground() { UpdateBackground(false); }
        public void RelayoutBackground() { UpdateBackground(true); }
        void UpdateBackground(bool layout)
        {
            if(Application.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            const string folder="Assets/Lab/Art/Environment/StudioMaterials";
            EnsureFolder(folder);
            if(generated==null)
            {
                var root=new GameObject("Generated Background");UnityEditor.Undo.RegisterCreatedObjectUndo(root,"Create background root");
                root.transform.SetParent(transform,false);root.transform.position=Center;generated=root.transform;
            }
            if(generatedGround!=null)
            {
                UnityEditor.Undo.RecordObject(generatedGround.gameObject,"Ground visibility");generatedGround.gameObject.SetActive(showGround);
                var mat=generatedGround.GetComponent<Renderer>().sharedMaterial;UnityEditor.Undo.RecordObject(mat,"Update ground");
                SetTexture(mat,groundPng,groundColor,groundTiling,groundOffset);mat.SetFloat("_Smoothness",smoothness);mat.SetTexture("_BumpMap",groundNormal);mat.SetFloat("_BumpScale",normalStrength);
                if(groundNormal!=null)mat.EnableKeyword("_NORMALMAP");else mat.DisableKeyword("_NORMALMAP");UnityEditor.EditorUtility.SetDirty(mat);
            }
            var surround=DirectChild(generated,"Hanji Surround");
            if(surround!=null)
            {
                UnityEditor.Undo.RecordObject(surround.gameObject,"Surround visibility");surround.gameObject.SetActive(showSurround);
                var mat=surround.GetComponent<Renderer>().sharedMaterial;UnityEditor.Undo.RecordObject(mat,"Update paper");SetTexture(mat,paperPng,paperColor,paperTiling,Vector2.zero);UnityEditor.EditorUtility.SetDirty(mat);
            }
            if(generatedBoardBacking!=null)
            {
                UnityEditor.Undo.RecordObject(generatedBoardBacking.gameObject,"Backing visibility");generatedBoardBacking.gameObject.SetActive(fillBoardBackground);
                var mat=generatedBoardBacking.GetComponent<Renderer>().sharedMaterial;UnityEditor.Undo.RecordObject(mat,"Update backing");SetTexture(mat,boardBackgroundPng!=null?boardBackgroundPng:groundPng,boardBackgroundColor,new Vector2(20,20),Vector2.zero);UnityEditor.EditorUtility.SetDirty(mat);
            }
            if(layout)
            {
                if(generatedGround!=null){UnityEditor.Undo.RecordObject(generatedGround,"Ground layout");var ps=generatedGround.parent.lossyScale;generatedGround.localScale=new Vector3(Mathf.Max(1,groundSize.x)/Mathf.Abs(ps.x),Mathf.Max(1,groundSize.y)/Mathf.Abs(ps.z),1);generatedGround.position=new Vector3(Center.x,(collisionFloor!=null?collisionFloor.position.y:0)+groundHeightOffset,Center.z);}
                if(surround!=null){UnityEditor.Undo.RecordObject(surround,"Surround layout");surround.localScale=Vector3.one*Mathf.Clamp(surroundRadius,25,38)*2;}
            }
            foreach(var item in decorations)
            {
                if(item==null)continue;
                if(item.group==null)item.group=DirectChild(generated,item.title);
                if(item.group==null && !item.visible)continue;
                if(item.group==null){var go=new GameObject(item.title);UnityEditor.Undo.RegisterCreatedObjectUndo(go,"Add decoration group");go.transform.SetParent(generated,false);item.group=go.transform;}
                UnityEditor.Undo.RecordObject(item.group.gameObject,"Group visibility");item.group.gameObject.SetActive(item.visible&&item.png!=null);
                if(item.cards==null)item.cards=new List<Transform>();
                if(item.cards.Count==0)foreach(Transform child in item.group)if(child.GetComponent<MeshRenderer>()!=null)item.cards.Add(child);
                if(item.stableId==null||item.stableId.Length==0)item.stableId=Guid.NewGuid().ToString("N");
                if(item.material==null && item.cards.Count>0 && item.cards[0]!=null)item.material=item.cards[0].GetComponent<Renderer>().sharedMaterial;
                if(item.material==null)item.material=MaterialAt(folder+"/Art_"+item.stableId+".mat","C6Lab/Environment/InkCard");
                UnityEditor.Undo.RecordObject(item.material,"Card material");item.material.shader=Shader.Find("C6Lab/Environment/InkCard");item.material.SetTexture("_BaseMap",item.png);item.material.SetColor("_BaseColor",Color.white);item.material.renderQueue=3000;UnityEditor.EditorUtility.SetDirty(item.material);
                int count=Mathf.Clamp(item.count,1,32);
                for(int i=0;i<Mathf.Max(count,item.cards.Count);i++)
                {
                    bool created=i>=item.cards.Count||item.cards[i]==null;
                    Transform card;
                    if(created){var go=Primitive("PNG "+(i+1).ToString("00"),PrimitiveType.Quad,item.group,item.material);UnityEditor.Undo.RegisterCreatedObjectUndo(go,"Add background card");card=go.transform;if(i<item.cards.Count)item.cards[i]=card;else item.cards.Add(card);}else card=item.cards[i];
                    UnityEditor.Undo.RecordObject(card.gameObject,"Card visibility");card.gameObject.SetActive(i<count&&item.visible&&item.png!=null);
                    if(i>=count)continue;
                    if(created||layout)
                    {
                        UnityEditor.Undo.RecordObject(card,"Arrange background card");float a=(item.angle+i*360f/count)*Mathf.Deg2Rad;var radial=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));float width=Mathf.Max(.1f,item.width)*(1+Mathf.Sin(i*2.4f)*item.sizeVariation);
                        card.position=Center+radial*Mathf.Clamp(item.radius,9,surroundRadius-2)+Vector3.up*(item.height-Center.y);card.rotation=Quaternion.LookRotation(radial);
                        float height=item.boxHeight>0?item.boxHeight:(item.png!=null?width*item.png.height/item.png.width:width);
                        card.localScale=new Vector3(width*(item.alternateFlip&&i%2==1?-1:1),height,1);
                    }
                    var component=card.GetComponent<LabEnvironmentCard>();if(component==null)component=UnityEditor.Undo.AddComponent<LabEnvironmentCard>(card.gameObject);
                    UnityEditor.Undo.RecordObject(component,"Update artwork");if(component.useGroupAppearance){component.png=item.png;component.sprite=null;component.tint=item.tint;component.opacity=item.opacity;component.thickness=item.thickness;component.depthLayers=item.depthLayers;component.crossPlane=item.crossPlane;component.billboard=item.billboard;component.sortingOrder=item.sortingOrder;component.windAmount=item.windAmount;component.windSpeed=item.windSpeed;}
                    UnityEditor.Undo.RecordObject(card.GetComponent<Renderer>(),"Assign card material");card.GetComponent<Renderer>().sharedMaterial=item.material;component.Refresh();UnityEditor.EditorUtility.SetDirty(component);
                }
            }
            UnityEditor.EditorUtility.SetDirty(this);UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);UnityEditor.AssetDatabase.SaveAssets();
        }
        static Transform DirectChild(Transform parent,string name)
        {
            if(parent==null)return null;foreach(Transform child in parent)if(child.name==name)return child;return null;
        }
        static void EnsureFolder(string path)
        {
            if(UnityEditor.AssetDatabase.IsValidFolder(path))return;
            string parent=System.IO.Path.GetDirectoryName(path).Replace('\\','/');
            EnsureFolder(parent);
            UnityEditor.AssetDatabase.CreateFolder(parent,System.IO.Path.GetFileName(path));
        }
        static Material MaterialAt(string path,string shader)
        {
            var m=UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find(shader));UnityEditor.AssetDatabase.CreateAsset(m,path);}
            UnityEditor.Undo.RecordObject(m,"Update background material");
            UnityEditor.EditorUtility.SetDirty(m);
            return m;
        }
        static void SetTexture(Material m,Texture2D png,Color color,Vector2 tiling,Vector2 offset)
        {
            m.SetTexture("_BaseMap",png); m.SetColor("_BaseColor",color);
            m.SetTextureScale("_BaseMap",tiling);m.SetTextureOffset("_BaseMap",offset);
        }
        static void Cutout(Material m,float cutoff)
        {
            m.SetFloat("_Surface",0);m.SetFloat("_AlphaClip",1);m.SetFloat("_Cutoff",cutoff);
            m.EnableKeyword("_ALPHATEST_ON");m.SetOverrideTag("RenderType","TransparentCutout");m.renderQueue=2450;
        }
        static GameObject Primitive(string name,PrimitiveType type,Transform parent,Material mat)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.layer=0;go.transform.SetParent(parent,false);
            UnityEditor.Undo.DestroyObjectImmediate(go.GetComponent<Collider>());
            var r=go.GetComponent<Renderer>();r.sharedMaterial=mat;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows=false;r.lightProbeUsage=UnityEngine.Rendering.LightProbeUsage.Off;
            return go;
        }
#endif
    }
}
