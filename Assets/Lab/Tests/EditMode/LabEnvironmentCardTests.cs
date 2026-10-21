using NUnit.Framework;
using UnityEngine;
namespace C6Lab.Tests
{
 public sealed class LabEnvironmentCardTests
 {
  [Test] public void ReplacingImagePreservesPlacementAndDisplayBox()
  {
   var go=new GameObject("Card test");var wide=new Texture2D(64,16);var tall=new Texture2D(16,64);
   try{
    go.transform.position=new Vector3(4,2,9);go.transform.rotation=Quaternion.Euler(0,37,0);go.transform.localScale=new Vector3(6,3,1);
    var card=go.AddComponent<LabEnvironmentCard>();card.thickness=0;card.png=wide;card.Refresh();
    var pos=go.transform.position;var rot=go.transform.rotation;var scale=go.transform.localScale;
    card.png=tall;card.Refresh();Assert.AreEqual(pos,go.transform.position);Assert.AreEqual(rot,go.transform.rotation);Assert.AreEqual(scale,go.transform.localScale);
    foreach(var v in go.GetComponent<MeshFilter>().sharedMesh.vertices){Assert.LessOrEqual(Mathf.Abs(v.x),.5001f);Assert.LessOrEqual(Mathf.Abs(v.y),.5001f);}
   }finally{Object.DestroyImmediate(go);Object.DestroyImmediate(wide);Object.DestroyImmediate(tall);}
  }
  [Test] public void ThicknessAddsSideCoverageWithoutPhysics()
  {
   var go=new GameObject("Card test");try{go.transform.localScale=new Vector3(4,2,1);var c=go.AddComponent<LabEnvironmentCard>();c.thickness=.4f;c.depthLayers=3;c.crossPlane=true;c.Refresh();var bounds=go.GetComponent<MeshFilter>().sharedMesh.bounds;Assert.GreaterOrEqual(bounds.size.z,.399f);Assert.IsNull(go.GetComponent<Collider>());Assert.IsNull(go.GetComponent<Rigidbody>());}finally{Object.DestroyImmediate(go);}
  }
  [Test] public void OpacityAndWindArePerCardNotSharedMaterialChanges()
  {
   var go=new GameObject("Card test");var mat=new Material(Shader.Find("C6Lab/Environment/InkCard"));try{var card=go.AddComponent<LabEnvironmentCard>();go.GetComponent<Renderer>().sharedMaterial=mat;card.opacity=.3f;card.windAmount=.08f;card.Refresh();var block=new MaterialPropertyBlock();go.GetComponent<Renderer>().GetPropertyBlock(block);Assert.That(block.GetColor("_BaseColor").a,Is.EqualTo(.3f).Within(.001f));Assert.That(block.GetFloat("_WindAmount"),Is.EqualTo(.08f).Within(.001f));Assert.That(mat.GetColor("_BaseColor").a,Is.EqualTo(1));}finally{Object.DestroyImmediate(go);Object.DestroyImmediate(mat);}
  }
 }
}
