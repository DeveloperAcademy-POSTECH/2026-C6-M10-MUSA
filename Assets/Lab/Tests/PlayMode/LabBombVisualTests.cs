#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    public sealed class LabBombVisualTests
    {
        [UnityTest]
        public IEnumerator SavedBombArtUsesHandoffsIndependentlyOfTheExistingFuseAndKeepsCountUpright()
        {
            var root = new GameObject("Bomb art regression");
            var config = ScriptableObject.CreateInstance<LabConfig>();
            var look = AssetDatabase.LoadAssetAtPath<LabOrbAppearance>("Assets/Lab/Art/Orbs/LabOrbAppearance.asset");
            typeof(LabConfig).GetField("orbAppearance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(config, look);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform); camera.transform.position = new Vector3(0,0,-10); camera.orthographic=true;
            var board = root.AddComponent<LabOrbBoard>();
            board.Configure(config, camera, new Rect(-4,-2,8,4));
            try
            {
                var bomb = board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f,.5f), lockSwipes:4, fuseSeconds:15f);
                var marker = bomb.ItemMarker; var visual = marker.BombVisual;
                Assert.That(visual, Is.Not.Null, "The saved appearance must instantiate the real prefab.");
                Assert.That(bomb.GetComponentsInChildren<Collider>().Length, Is.EqualTo(1));
                float full = visual.FlameSize;
                foreach (int count in new[]{4,3,2,1})
                {
                    board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f,.5f), lockSwipes:count, fuseSeconds:15f);
                    Assert.That(marker.BombCountText, Is.EqualTo(count.ToString()));
                    Assert.That(visual.CountSprite.name, Is.EqualTo("BombCount"+count));
                    Assert.That(marker.RopeShare, Is.EqualTo(1f));
                }
                board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f,.5f), lockSwipes:1, fuseSeconds:9f);
                Assert.That(marker.BombCountText, Is.EqualTo("1"));
                Assert.That(marker.RopeShare, Is.EqualTo(.5f).Within(.0001f), "Existing (seconds-warning)/(total-warning) is preserved.");
                Assert.That(visual.FlameSize, Is.LessThan(full));
                Assert.That(marker.WarningHz, Is.Zero);
                marker.SetBombTurn(Quaternion.Euler(30,45,70));
                yield return null;
                Assert.That(Quaternion.Angle(visual.CountTransform.rotation, Quaternion.identity), Is.LessThan(.01f));
                board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f,.5f), lockSwipes:1, fuseSeconds:2.9f);
                Assert.That(marker.RopeShare, Is.Zero);
                Assert.That(marker.WarningHz, Is.GreaterThan(0));
                float until=Time.time+2; float green=1;
                while(Time.time<until && green>.6f) { yield return null; green=Mathf.Min(green,marker.BombArtColor.g); }
                Assert.That(green, Is.LessThan(.6f));
                board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f,.5f), lockSwipes:0, fuseSeconds:-1f, justUnlocked:true);
                Assert.That(marker.BombCountVisible, Is.False);
                Assert.That(marker.SparkVisible, Is.False);
                Assert.That(marker.SmokeVisible, Is.True);
                Assert.That(marker.RainbowVisible, Is.True);
                Assert.That(marker.WarningHz, Is.Zero);
                Assert.That(marker.BombArtColor, Is.EqualTo(Color.white));
            }
            finally { Object.Destroy(root); Object.Destroy(config); }
            yield return null;
        }

        [Test]
        public void SavedPrefabHasAllSuppliedPartsAndNoGameplayComponents()
        {
            var look=AssetDatabase.LoadAssetAtPath<LabOrbAppearance>("Assets/Lab/Art/Orbs/LabOrbAppearance.asset");
            Assert.That(look.BombVisualPrefab, Is.Not.Null);
            var prefab=look.BombVisualPrefab;
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true).Select(x=>x.GetType()), Is.EqualTo(new[]{typeof(LabBombVisual)}));
            var sprites=prefab.GetComponentsInChildren<SpriteRenderer>(true);
            Assert.That(sprites.Length, Is.EqualTo(5));
            Assert.That(sprites.All(x=>x.sprite!=null), Is.True);
            Assert.That(sprites.Single(x=>x.name=="Cap").transform.localScale.x, Is.EqualTo(67.2f/104f).Within(.0001f));
            Assert.That(sprites.Single(x=>x.name=="Fuse").sharedMaterial.shader.name, Is.EqualTo("C6Lab/Bomb Fuse"));
            foreach(int n in new[]{1,2,3,4}) Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Lab/Art/BombVisual/Parts/BombCount"+n+".png"), Is.Not.Null);
        }
    }
}
#endif
