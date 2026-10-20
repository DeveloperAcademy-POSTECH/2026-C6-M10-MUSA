using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace C6Lab.Tests
{
    public sealed class LabOrbSpriteTests
    {
        static readonly LabOrbElement[] Elements = {LabOrbElement.Fire,LabOrbElement.Water,LabOrbElement.Wood,LabOrbElement.Metal,LabOrbElement.Earth};
        [UnityTest]
        public IEnumerator BoardArtChangesKindWithoutChangingCollisionAndCanReturnToMesh()
        {
            var config = Resources.Load<LabConfig>("LabConfig");
            var root = new GameObject("Sprite board fixture");
            root.AddComponent<Rigidbody>().useGravity = false;
            root.AddComponent<SphereCollider>();
            var view = root.AddComponent<LabOrbView>();
            view.ConfigureAppearance(config.OrbAppearance);
            try
            {
                for(int i=0;i<7;i++)
                {
                    var kind=i==0?LabOrbKind.Yin:i==1?LabOrbKind.Yang:LabOrbKind.Combined;
                    var element=i<2?LabOrbElement.None:Elements[i-2];
                    view.Configure("sprite",kind,.32f,element);
                    yield return null;
                    Assert.That(view.HitSphere.radius,Is.EqualTo(.32f));
                    Assert.That(root.GetComponentsInChildren<Collider>().Length,Is.EqualTo(1));
                    var sprite=root.GetComponentInChildren<LabOrbSpriteArt>();
                    Assert.That(sprite,Is.Not.Null);
                    Assert.That(sprite.GetComponent<SpriteRenderer>().sprite,Is.SameAs(config.OrbAppearance.SpriteFor(kind,element)));
                    Assert.That(Mathf.Max(sprite.GetComponent<SpriteRenderer>().bounds.size.x,sprite.GetComponent<SpriteRenderer>().bounds.size.y),Is.EqualTo(i<2 ? .64f : .768f).Within(.001f));
                    Assert.That(view.Visual.GetComponentsInChildren<MeshRenderer>().Length,Is.Zero);
                }
                view.ConfigureAppearance(null);
                yield return null;
                Assert.That(root.GetComponentInChildren<LabOrbSpriteArt>(),Is.Null);
                Assert.That(root.GetComponentsInChildren<MeshRenderer>().Length,Is.GreaterThan(0));
                Assert.That(view.HitSphere.radius,Is.EqualTo(.32f));
            }
            finally{Object.Destroy(root);}
        }
        [UnityTest]
        public IEnumerator EveryElementUsesItsArtInFlightAndKeepsTheShrunkColliderAndTrail()
        {
            var config=Resources.Load<LabConfig>("LabConfig");
            foreach(var element in Elements)
            {
                var shot=LabProjectile.Create(Vector3.zero,config,element:element,sizeScale:.5f);
                try
                {
                    Assert.That(shot.LaunchVisual(Vector3.forward,config),Is.True);
                    yield return null;
                    var art=shot.GetComponentInChildren<LabOrbSpriteArt>();
                    Assert.That(art.GetComponent<SpriteRenderer>().sprite,Is.SameAs(config.OrbAppearance.SpriteFor(LabOrbKind.Combined,element)));
                    Assert.That(shot.GetComponent<SphereCollider>().radius,Is.EqualTo(config.ThrowRadius*.5f).Within(.0001f));
                    Assert.That(Mathf.Max(art.GetComponent<SpriteRenderer>().bounds.size.x,art.GetComponent<SpriteRenderer>().bounds.size.y),Is.EqualTo(config.ThrowRadius*1.2f).Within(.001f));
                    Assert.That(shot.VisualMount.GetComponentsInChildren<MeshRenderer>().Length,Is.Zero);
                    Assert.That(shot.GetComponentsInChildren<Collider>().Length,Is.EqualTo(1));
                    Assert.That(shot.ElementFlightTrail!=null || shot.FlightTrail!=null,Is.True);
                }
                finally{Object.Destroy(shot.gameObject);}
            }
        }
        [UnityTest]
        public IEnumerator ProjectileArtFacesEachRenderingCamera()
        {
            var config=Resources.Load<LabConfig>("LabConfig");
            var shot=LabProjectile.Create(Vector3.zero,config,element:LabOrbElement.Fire);
            var go=new GameObject("Sprite camera");var camera=go.AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=1;camera.targetTexture=new RenderTexture(64,64,24);
            try
            {
                yield return null;
                var art=shot.GetComponentInChildren<LabOrbSpriteArt>();
                foreach(float angle in new[]{0f,65f,-65f})
                {
                    camera.transform.rotation=Quaternion.Euler(0,angle,0);
                    camera.transform.position=-camera.transform.forward*3;
                    camera.Render();
                    Assert.That(Quaternion.Angle(art.transform.rotation,camera.transform.rotation),Is.LessThan(.01f));
                }
            }
            finally{Object.Destroy(camera.targetTexture);Object.Destroy(go);Object.Destroy(shot.gameObject);}
        }
    }
}
