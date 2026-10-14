using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
namespace Lab.TargetArt {
 public static class BakMaterialTools {
  [MenuItem("Lab/Target/Match Materials to Render Pipeline")]
  public static void MatchPipeline() {
   var pipeline=GraphicsSettings.currentRenderPipeline;
   string shaderName=pipeline==null?"Standard":pipeline.GetType().Name.Contains("HDRender")?"HDRP/Lit":"Universal Render Pipeline/Lit";
   var shader=Shader.Find(shaderName);if(shader==null){Debug.LogWarning("Target: shader not available: "+shaderName);return;}
   foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/Lab/Art/Target/Materials"})) {
    var mat=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
    var tex=mat.HasProperty("_BaseMap")?mat.GetTexture("_BaseMap"):mat.HasProperty("_BaseColorMap")?mat.GetTexture("_BaseColorMap"):mat.mainTexture;
    Color color=mat.HasProperty("_BaseColor")?mat.GetColor("_BaseColor"):mat.color;
    mat.shader=shader;
    if(mat.HasProperty("_BaseMap"))mat.SetTexture("_BaseMap",tex);
    if(mat.HasProperty("_BaseColorMap"))mat.SetTexture("_BaseColorMap",tex);
    if(mat.HasProperty("_MainTex"))mat.SetTexture("_MainTex",tex);
    if(mat.HasProperty("_BaseColor"))mat.SetColor("_BaseColor",color);
    if(mat.HasProperty("_Color"))mat.SetColor("_Color",color);
    if(mat.HasProperty("_Metallic"))mat.SetFloat("_Metallic",0);
    if(mat.HasProperty("_Smoothness"))mat.SetFloat("_Smoothness",.12f);
    if(mat.HasProperty("_Glossiness"))mat.SetFloat("_Glossiness",.12f);
    EditorUtility.SetDirty(mat);
   }
   AssetDatabase.SaveAssets();Debug.Log("Target materials ready: "+shaderName);
  }
 }
}
