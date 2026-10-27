using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace C6Lab.Tests
{
 public sealed class LabEnvironmentCardPlayTests
 {
  [UnityTest] public IEnumerator GrassWindChangesRenderedSilhouetteWhileTransformStaysFixed()
  {
   var root=new GameObject("Wind test");root.layer=31;root.transform.position=new Vector3(10000,10000,10000);root.transform.localScale=new Vector3(2,2,1);
   var cameraObject=new GameObject("Wind test camera");var cam=cameraObject.AddComponent<Camera>();cam.enabled=false;cameraObject.transform.position=root.transform.position+Vector3.back*5;cam.orthographic=true;cam.orthographicSize=1.4f;cam.cullingMask=1<<31;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;
   var rt=new RenderTexture(64,64,24);var image=new Texture2D(64,64,TextureFormat.RGB24,false);var mat=new Material(Shader.Find("C6Lab/Environment/InkCard"));var old=RenderTexture.active;
   try{
    var card=root.AddComponent<LabEnvironmentCard>();root.GetComponent<Renderer>().sharedMaterial=mat;card.thickness=0;card.windAmount=.18f;card.windSpeed=4;card.Refresh();cam.targetTexture=rt;
    yield return null;cam.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,64,64),0,0);image.Apply();var before=image.GetPixels32();var position=root.transform.position;
    // One pair can land on nearly equal sine values. Sample four distinct wind phases
    // against the same baseline while retaining the rendered-pixel threshold.
    int maxChanged=0;
    for(int sample=0;sample<4;sample++){
     yield return new WaitForSeconds(.23f);cam.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,64,64),0,0);image.Apply();var after=image.GetPixels32();int changed=0;for(int i=0;i<before.Length;i++)if(before[i].r!=after[i].r)changed++;
     maxChanged=Mathf.Max(maxChanged,changed);
     Assert.AreEqual(position,root.transform.position);Assert.IsNull(root.GetComponent<Collider>());
    }
    Assert.Greater(maxChanged,3,"Shader time must visibly animate the grass silhouette across the sampled wind phases.");Assert.AreEqual(position,root.transform.position);Assert.IsNull(root.GetComponent<Collider>());
   }finally{RenderTexture.active=old;cam.targetTexture=null;rt.Release();Object.Destroy(root);Object.Destroy(cameraObject);Object.Destroy(rt);Object.Destroy(image);Object.Destroy(mat);}
  }
 }
}
