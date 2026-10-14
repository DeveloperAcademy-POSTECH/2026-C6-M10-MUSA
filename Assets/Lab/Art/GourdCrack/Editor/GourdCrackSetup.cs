using System.IO;
using C6Lab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace C6Lab.EditorTools
{
 public static class GourdCrackSetup
 {
  public const string Path="Assets/Lab/Scenes/LabGourdCrack.unity";
  [MenuItem("C6 Lab/Open Gourd Crack Prototype")]
  public static void Open()
  {
   if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
   if(File.Exists(Path))EditorSceneManager.OpenScene(Path);else Create();
  }
  public static void Upgrade()
  {
   EditorSceneManager.OpenScene(Path);
   foreach(var c in Object.FindObjectsByType<LabGourdCrack>(FindObjectsSortMode.None))
    if(c.GetComponent<LabGourdFinish>()==null)c.gameObject.AddComponent<LabGourdFinish>();
   EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Path);
  }
  public static void Create()
  {
   EditorSceneManager.OpenScene("Assets/Lab/Scenes/Lab.unity");
   var network=Object.FindObjectsByType<LabNetwork>(FindObjectsSortMode.None);
   foreach(var n in network)
   {
    if(n.target==null)continue;
    var c=n.target.gameObject.AddComponent<LabGourdCrack>();c.network=n;c.target=n.target;
    c.gourdPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Lab/Art/Target2/Prefabs/Target2_Intact.prefab");
    const string matPath="Assets/Lab/Art/GourdCrack/GourdCrack.mat";
    var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
    if(mat==null){mat=new Material(Shader.Find("C6Lab/GourdCrack"));AssetDatabase.CreateAsset(mat,matPath);}
    c.crackMaterial=mat;
    n.target.gameObject.AddComponent<LabGourdFinish>();
   }
   AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Path);
   Debug.Log("GOURD_SCENE_CREATED");
  }
 }
}
