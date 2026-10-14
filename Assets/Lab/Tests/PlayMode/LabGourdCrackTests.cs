using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace C6Lab.Tests
{
 public sealed class LabGourdCrackTests
 {
  [TestCase(1000,0)] [TestCase(980,1)] [TestCase(660,2)] [TestCase(330,3)] [TestCase(0,3)]
  public void SharedHpDeterminesStage(int hp,int expected)
   =>Assert.That(LabGourdCrack.StageForHp(hp,1000),Is.EqualTo(expected));
  #if UNITY_EDITOR
  [UnityTest]
  public IEnumerator SavedSceneMountsGourdAndResetsCracks()
  {
   yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Lab/Scenes/LabGourdCrack.unity", new LoadSceneParameters(LoadSceneMode.Single));
   yield return null;
   var c=Object.FindFirstObjectByType<LabGourdCrack>();Assert.That(c,Is.Not.Null);
   var renderers=c.target.VisualMount.GetComponentsInChildren<Renderer>();
   Assert.That(renderers.Any(r=>r.sharedMaterials.Contains(c.crackMaterial)),Is.True,"Imported shell must receive crack shader");
   Assert.That(c.target.Hitbox.enabled,Is.True);
   Assert.That(c.target.VisualMount.GetComponentsInChildren<Collider>().Length,Is.Zero);
   var camera=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(v=>v.name=="BattleCamera");
   Directory.CreateDirectory("Library/GourdCrackCaptures");
   for(int stage=0;stage<=3;stage++)
   {
    c.SetStage(stage);Assert.That(c.Stage,Is.EqualTo(stage));
    var rt=new RenderTexture(540,960,24);var image=new Texture2D(540,960,TextureFormat.RGB24,false);var old=RenderTexture.active;
    try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,540,960),0,0);image.Apply();File.WriteAllBytes("Library/GourdCrackCaptures/stage-"+stage+".png",image.EncodeToPNG());}
    finally{camera.targetTexture=null;RenderTexture.active=old;rt.Release();Object.Destroy(rt);Object.Destroy(image);}
   }
   var finish = c.GetComponent<LabGourdFinish>();
   Assert.That(finish, Is.Not.Null);
   var victory = new LabSnapshot { sessionId="finish-test", round=1, phase=LabPhase.Victory, hp=0 };
   Assert.That(LabGourdFinish.DelayVictory(c.network,victory),Is.True);
   finish.ObserveSnapshot(victory,.6f);
   Assert.That(finish.IsFinishing, Is.True);
   Assert.That(finish.Complete, Is.False);
   Assert.That(c.target.VisualMount.Find("Gourd Left Half").gameObject.activeSelf, Is.True);
   Assert.That(c.target.VisualMount.Find("Gourd Right Half").gameObject.activeSelf, Is.True);
   Assert.That(c.target.VisualMount.Find("Small Yokai Spirit").gameObject.activeSelf, Is.True);
   finish.Advance(1f); Assert.That(finish.Complete, Is.True);
   Assert.That(LabGourdFinish.DelayVictory(c.network,victory),Is.False);
   int childCount = c.target.VisualMount.childCount;
   finish.ObserveSnapshot(victory,0);Assert.That(c.target.VisualMount.childCount,Is.EqualTo(childCount),"Repeated snapshots cannot duplicate the finish");
   Assert.That(LabGourdFinish.DelayVictory(c.network,new LabSnapshot{phase=LabPhase.Defeat}),Is.False);
   var preview = new RenderTexture(540,960,24); var png = new Texture2D(540,960,TextureFormat.RGB24,false); var previous = RenderTexture.active;
   try { camera.targetTexture=preview; camera.Render();RenderTexture.active=preview;png.ReadPixels(new Rect(0,0,540,960),0,0);png.Apply();File.WriteAllBytes("Library/GourdCrackCaptures/finish.png",png.EncodeToPNG()); }
   finally { camera.targetTexture=null;RenderTexture.active=previous;preview.Release();Object.Destroy(preview);Object.Destroy(png); }
   finish.ObserveSnapshot(null,0);Assert.That(finish.IsFinishing, Is.False);
   Assert.That(c.target.VisualMount.GetChild(0).gameObject.activeSelf,Is.True);
   yield return null;Assert.That(c.Stage,Is.Zero,"No active session must reset damage");
  }
  #endif
 }
}
