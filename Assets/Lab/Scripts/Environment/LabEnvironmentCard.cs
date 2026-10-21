using System.Collections.Generic;
using UnityEngine;
namespace C6Lab
{
 [ExecuteAlways,DisallowMultipleComponent,RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
 public sealed class LabEnvironmentCard:MonoBehaviour
 {
  [Tooltip("Disable to keep this individual card appearance when the Studio updates its group.")]
  public bool useGroupAppearance=true;
  public Texture2D png;
  public Sprite sprite;
  public Color tint=Color.white;
  [Range(0,1)] public float opacity=1;
  public bool preserveImageAspect=true;
  [Tooltip("World units. Stacked image planes approximate volume; this is not a solid extruded silhouette.")]
  [Range(0,2)] public float thickness=.15f;
  [Range(1,5)] public int depthLayers=3;
  public bool crossPlane;
  public bool billboard;
  public int sortingOrder;
  [Range(0,.2f)] public float windAmount;
  [Range(.1f,4)] public float windSpeed=1;
  Mesh ownedMesh;
  MaterialPropertyBlock properties;
  void OnEnable(){Refresh();}
  void OnValidate()
  {
#if UNITY_EDITOR
   UnityEditor.EditorApplication.delayCall+=DelayedRefresh;
#endif
  }
#if UNITY_EDITOR
  void DelayedRefresh(){if(this!=null && isActiveAndEnabled)Refresh();}
#endif
  public void Refresh()
  {
   var filter=GetComponent<MeshFilter>();var renderer=GetComponent<MeshRenderer>();
   if(filter==null||renderer==null)return;
   var texture=sprite!=null?sprite.texture:png;
   float imageAspect=texture!=null?(float)texture.width/texture.height:1;
   Vector2 uvMin=Vector2.zero,uvMax=Vector2.one;
   if(sprite!=null){var rect=sprite.textureRect;imageAspect=rect.width/rect.height;uvMin=new Vector2(rect.x/texture.width,rect.y/texture.height);uvMax=new Vector2(rect.xMax/texture.width,rect.yMax/texture.height);}
   var scale=transform.lossyScale;float boxAspect=Mathf.Abs(scale.x)/Mathf.Max(.0001f,Mathf.Abs(scale.y));
   float w=1,h=1;
   if(preserveImageAspect){if(imageAspect>boxAspect)h=boxAspect/imageAspect;else w=imageAspect/boxAspect;}
   var vertices=new List<Vector3>();var uvs=new List<Vector2>();var colors=new List<Color>();var triangles=new List<int>();
   int layers=thickness>.001f?Mathf.Clamp(depthLayers,1,5):1;
   for(int i=0;i<layers;i++)AddQuad(vertices,uvs,colors,triangles,w,h,layers==1?0:((float)i/(layers-1)-.5f)*thickness/Mathf.Max(.001f,Mathf.Abs(scale.z)),false,i==layers/2?1:.22f,uvMin,uvMax);
   if(crossPlane&&!billboard)AddQuad(vertices,uvs,colors,triangles,Mathf.Max(.15f,thickness)/Mathf.Max(.001f,Mathf.Abs(scale.z)),h,0,true,.65f,uvMin,uvMax);
   if(ownedMesh==null){ownedMesh=new Mesh{name="Environment Card Preview",hideFlags=HideFlags.HideAndDontSave};}
   ownedMesh.Clear();ownedMesh.SetVertices(vertices);ownedMesh.SetUVs(0,uvs);ownedMesh.SetColors(colors);ownedMesh.SetTriangles(triangles,0);ownedMesh.RecalculateBounds();
   var bounds=ownedMesh.bounds;bounds.Expand(new Vector3(windAmount*2,0,0));
   if(billboard){float radius=.5f*Mathf.Sqrt(scale.x*scale.x+scale.y*scale.y+thickness*thickness)+windAmount*Mathf.Abs(scale.x);bounds=new Bounds(Vector3.zero,new Vector3(2*radius/Mathf.Max(.001f,Mathf.Abs(scale.x)),2*radius/Mathf.Max(.001f,Mathf.Abs(scale.y)),2*radius/Mathf.Max(.001f,Mathf.Abs(scale.z))));}
   ownedMesh.bounds=bounds;
   filter.sharedMesh=ownedMesh;
   if(properties==null)properties=new MaterialPropertyBlock();properties.Clear();
   properties.SetTexture("_BaseMap",texture!=null?texture:Texture2D.whiteTexture);
   var color=tint;color.a*=opacity;properties.SetColor("_BaseColor",color);
   properties.SetFloat("_WindAmount",windAmount);properties.SetFloat("_WindSpeed",windSpeed);properties.SetFloat("_Billboard",billboard?1:0);
   renderer.SetPropertyBlock(properties);renderer.sortingOrder=sortingOrder;
  }
  static void AddQuad(List<Vector3> v,List<Vector2> uv,List<Color> c,List<int> t,float w,float h,float z,bool cross,float alpha,Vector2 lo,Vector2 hi)
  {
   int start=v.Count;var points=new[]{new Vector3(-w/2,-h/2,z),new Vector3(w/2,-h/2,z),new Vector3(w/2,h/2,z),new Vector3(-w/2,h/2,z)};
   foreach(var p in points){v.Add(cross?new Vector3(0,p.y,p.x):p);c.Add(new Color(1,1,1,alpha));}
   uv.Add(lo);uv.Add(new Vector2(hi.x,lo.y));uv.Add(hi);uv.Add(new Vector2(lo.x,hi.y));
   t.AddRange(new[]{start,start+2,start+1,start,start+3,start+2});
  }
  void OnDestroy()
  {
   if(ownedMesh!=null){if(Application.isPlaying)Destroy(ownedMesh);else DestroyImmediate(ownedMesh);}
  }
 }
}
