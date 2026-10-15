using System.Collections;using System.IO;using C6Lab;using NUnit.Framework;using UnityEngine;using UnityEngine.SceneManagement;using UnityEngine.TestTools;
namespace MUSA.GourdPaint.Tests
{
 public sealed class GourdPaintTests
 {
  #if UNITY_EDITOR
  [UnityTest]public IEnumerator ApprovedHitPaintFollowsSurfaceAndResets()
  {
   yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Experiments/GourdPaint/GourdPaintPrototype.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;
   var paint=Object.FindFirstObjectByType<GourdPaintTarget>();Assert.That(paint,Is.Not.Null);Assert.That(paint.PaintCount,Is.Zero);
   paint.target.NotifyApprovedHit(new Vector3(0,0,.5f),LabOrbElement.Fire);Assert.That(paint.PaintCount,Is.EqualTo(1));
   var check = new MaterialPropertyBlock(); paint.Shell.GetComponent<Renderer>().GetPropertyBlock(check);
   Assert.That(check.GetVectorArray("_PaintColors")[0],Is.EqualTo((Vector4)new Color(.88f,.16f,.14f)));
   paint.target.NotifyApprovedHit(new Vector3(0,0,.5f),LabOrbElement.Metal);
   paint.Shell.GetComponent<Renderer>().GetPropertyBlock(check);
   Assert.That(check.GetVectorArray("_PaintColors")[1],Is.EqualTo((Vector4)new Color(.96f,.8f,.15f)));
   paint.target.NotifyApprovedHit(new Vector3(0,0,.5f),LabOrbElement.Earth);
   paint.Shell.GetComponent<Renderer>().GetPropertyBlock(check);
   Assert.That(check.GetVectorArray("_PaintColors")[2],Is.EqualTo((Vector4)new Color(.52f,.33f,.16f)));
   paint.ClearPaint();paint.target.NotifyApprovedHit(new Vector3(0,0,.5f),LabOrbElement.Fire);
   Vector3 position=paint.Shell.position;
   for(int i=1;i<5;i++)paint.Paint(new Vector3(Mathf.Sin(i*1.2f)*.35f,Mathf.Cos(i*1.5f)*.4f,1),GourdPaintTarget.Palette(i));
   var block=new MaterialPropertyBlock();paint.Shell.GetComponent<Renderer>().GetPropertyBlock(block);var points=block.GetVectorArray("_PaintPositions");
   paint.target.VisualMount.Rotate(0,25,0);paint.Shell.GetComponent<Renderer>().GetPropertyBlock(block);Assert.That(block.GetVectorArray("_PaintPositions")[0],Is.EqualTo(points[0]));
   Assert.That(paint.Paint(new Vector3(float.NaN,0,0),Color.red),Is.False);
   Camera camera=null;foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))if(c.name=="BattleCamera")camera=c;
   camera.rect=new Rect(0,0,1,1);camera.transform.position=paint.Shell.position+new Vector3(0,.3f,3.3f);camera.transform.LookAt(paint.Shell.position);
   var rt=new RenderTexture(540,960,24);var image=new Texture2D(540,960,TextureFormat.RGB24,false);var old=RenderTexture.active;
   try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,540,960),0,0);image.Apply();Directory.CreateDirectory("Library/GourdPaintCaptures");File.WriteAllBytes("Library/GourdPaintCaptures/paint.png",image.EncodeToPNG());}
   finally{camera.targetTexture=null;RenderTexture.active=old;rt.Release();Object.Destroy(rt);Object.Destroy(image);}
   paint.ClearPaint();Assert.That(paint.PaintCount,Is.Zero);
   for(int i=0;i<128;i++)Assert.That(paint.Paint(Vector3.forward,Color.red),Is.True);
   Assert.That(paint.Paint(Vector3.forward,Color.blue),Is.False);Assert.That(paint.PaintCount,Is.EqualTo(128));
   paint.ClearPaint();Assert.That(paint.PaintCount,Is.Zero);
  }
  #endif
 }
}
