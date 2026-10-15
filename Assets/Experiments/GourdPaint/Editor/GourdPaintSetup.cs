using C6Lab;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
namespace MUSA.GourdPaint.EditorTools
{
 public static class GourdPaintSetup
 {
  const string Root="Assets/Experiments/GourdPaint";
  public const string ScenePath=Root+"/GourdPaintPrototype.unity";
  [MenuItem("C6 Lab/Open Gourd Paint Experiment")]
  public static void Open(){if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;if(System.IO.File.Exists(ScenePath))EditorSceneManager.OpenScene(ScenePath);else Create();}
  static GameObject Part(string name,PrimitiveType type,Transform parent,Vector3 p,Vector3 scale,Material mat)
  {var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=scale;Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=mat;return g;}
  public static void Create()
  {
   var mat=AssetDatabase.LoadAssetAtPath<Material>(Root+"/WhiteGourd.mat");if(mat==null){mat=new Material(Shader.Find("MUSA/GourdPaint"));AssetDatabase.CreateAsset(mat,Root+"/WhiteGourd.mat");}
   var wood=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Stem.mat");if(wood==null){wood=new Material(Shader.Find("Universal Render Pipeline/Unlit"));wood.SetColor("_BaseColor",new Color(.5f,.3f,.12f));AssetDatabase.CreateAsset(wood,Root+"/Stem.mat");}
   var root=new GameObject("Paint Gourd Visual");Part("White Gourd",PrimitiveType.Sphere,root.transform,Vector3.zero,Vector3.one*1.8f,mat);Part("Stem",PrimitiveType.Cylinder,root.transform,new Vector3(0,.96f,0),new Vector3(.14f,.12f,.14f),wood);
   var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/WhiteGourd.prefab");Object.DestroyImmediate(root);
   EditorSceneManager.OpenScene("Assets/Lab/Scenes/Lab.unity");
   foreach(var n in Object.FindObjectsByType<LabNetwork>(FindObjectsSortMode.None))if(n.target!=null){var p=n.target.gameObject.AddComponent<GourdPaintTarget>();p.target=n.target;p.network=n;p.paintMaterial=mat;p.visualPrefab=prefab;}
   AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),ScenePath);
  }
 }
}
