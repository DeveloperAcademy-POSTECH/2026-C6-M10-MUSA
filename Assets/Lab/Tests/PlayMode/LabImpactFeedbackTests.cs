using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    public sealed class LabImpactFeedbackTests
    {
        [UnityTest]
        public IEnumerator ApprovedHitKeepsDamageNumberAndAnchorsWorldBurstToMovingHitbox()
        {
            var canvasObject = new GameObject("Impact Test Canvas", typeof(RectTransform), typeof(Canvas));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var cameraObject = new GameObject("Impact Test Camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -10f);
            var target = LabTarget.CreateCylinder(null, Vector3.zero, .6f, 2.7f);
            GameObject prefab = CreateBurstPrefab();
            var vfxObject = new GameObject("Impact VFX", typeof(LabImpactParticleVfx));
            var vfx = vfxObject.GetComponent<LabImpactParticleVfx>();
            SetPrivate(vfx, "effectPrefab", prefab.GetComponent<LabImpactParticleBurst>());
            SetPrivate(vfx, "battleCamera", camera);
            var feedback = canvasObject.AddComponent<LabImpactFeedback>();
            SetPrivate(feedback, "battleCamera", camera);
            SetPrivate(feedback, "particleVfx", vfx);
            float originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;

            try
            {
                Vector3 localPoint = new Vector3(.2f, .1f, -.1f);
                Vector3 velocity = new Vector3(0f, 0f, 8f);
                feedback.PlayHit(target, localPoint, LabOrbElement.Fire, 0, velocity);
                Assert.IsNull(canvasObject.transform.Find("Impact Feedback"),
                    "An unapproved zero-damage event must not create a hit marker.");
                Assert.AreEqual(0, vfx.ActiveBurstCount,
                    "Zero-damage events must not create the world-space burst either.");

                feedback.PlayHit(target, localPoint, LabOrbElement.Water, 17, velocity);
                Transform layer = canvasObject.transform.Find("Impact Feedback");
                Assert.IsNotNull(layer);
                RectTransform marker = layer.Find("Hit Feedback") as RectTransform;
                Assert.IsNotNull(marker);
                var number = marker.GetComponentInChildren<UnityEngine.UI.Text>();
                Assert.AreEqual("-17", number.text);
                Assert.That(number.color.b - number.color.r, Is.GreaterThan(.5f),
                    "Water damage should use the blue element palette.");
                Assert.IsNull(marker.Find("Expanding Hit Ring"),
                    "The obsolete UI ring must not be rebuilt beside the world-space impact VFX.");
                Assert.AreEqual(0, layer.GetComponentsInChildren<Collider>().Length,
                    "Visual feedback must not add a second hitbox.");
                foreach (var graphic in layer.GetComponentsInChildren<UnityEngine.UI.Graphic>())
                    Assert.IsFalse(graphic.raycastTarget,
                        "A hit marker must never intercept orb gestures or the pause button.");
                Assert.AreEqual(1, vfx.ActiveBurstCount,
                    "The same approved hit should make exactly one world-space burst.");
                LabImpactParticleBurst burst = ActiveBurst(vfx);
                Assert.IsNotNull(burst);
                Vector3 contactPoint = target.Hitbox.transform.TransformPoint(localPoint);
                Assert.That(Vector3.Distance(burst.transform.position, contactPoint), Is.LessThan(.1f),
                    "A small camera-facing surface offset is allowed, but VFX must stay at the hit.");
                Assert.That(Vector3.Dot(burst.transform.position - contactPoint,
                    (camera.transform.position - contactPoint).normalized), Is.GreaterThan(0f),
                    "The burst should sit just in front of the target surface, not behind it.");
                ParticleSystem core = burst.GetComponentInChildren<ParticleSystem>();
                Assert.IsNotNull(core);
                Color effectColor = core.main.startColor.color;
                Assert.That(effectColor.r, Is.EqualTo(number.color.r).Within(.001f));
                Assert.That(effectColor.g, Is.EqualTo(number.color.g).Within(.001f));
                Assert.That(effectColor.b, Is.EqualTo(number.color.b).Within(.001f));
                Assert.AreEqual(0, burst.GetComponentsInChildren<Collider>(true).Length,
                    "VFX cannot introduce a second target or projectile hitbox.");
                Assert.AreEqual(0, burst.GetComponentsInChildren<Rigidbody>(true).Length);
                Assert.AreEqual(0, burst.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Length,
                    "The world-space burst must not capture Canvas input.");

                float before = marker.anchoredPosition.x;
                Vector3 burstBefore = burst.transform.position;
                target.Hitbox.transform.position += Vector3.right * .5f;
                yield return null;
                Assert.That(marker.anchoredPosition.x - before, Is.GreaterThan(1f),
                    "The marker should track the moving MeshCollider, not the static target root.");
                Assert.That(Vector3.Distance(burst.transform.position - burstBefore,
                    Vector3.right * .5f), Is.LessThan(.01f),
                    "The burst must remain at the Host-confirmed local hit point while the target moves.");

                yield return new WaitForSecondsRealtime(.75f);
                yield return null;
                Assert.IsTrue(marker == null, "A short-lived marker should remove itself.");
                Assert.AreEqual(0, vfx.ActiveBurstCount,
                    "The burst must expire and return to its pool without accumulating objects.");
                Assert.IsFalse(burst.gameObject.activeSelf);

                feedback.PlayHit(target, localPoint, LabOrbElement.Fire, 5, velocity);
                Transform newLayer = canvasObject.transform.Find("Impact Feedback");
                Assert.IsNotNull(newLayer);
                Assert.AreEqual(1, vfx.ActiveBurstCount);
                feedback.Clear();
                Assert.IsFalse(newLayer.gameObject.activeSelf, "Clear must hide the effect immediately.");
                Assert.AreEqual(0, vfx.ActiveBurstCount,
                    "Leaving a round must clear pooled particles along with the damage number.");
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                Object.Destroy(canvasObject);
                Object.Destroy(cameraObject);
                Object.Destroy(target.gameObject);
                Object.Destroy(vfxObject);
                Object.Destroy(prefab);
            }
        }

        [UnityTest]
        public IEnumerator RapidHitsKeepThePoolBoundedAndPauseRejectsNewEffects()
        {
            var cameraObject = new GameObject("Impact Pool Camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -10f);
            var target = LabTarget.CreateCylinder(null, Vector3.zero, .6f, 2.7f);
            GameObject prefab = CreateBurstPrefab();
            var vfxObject = new GameObject("Impact Pool VFX", typeof(LabImpactParticleVfx));
            var vfx = vfxObject.GetComponent<LabImpactParticleVfx>();
            SetPrivate(vfx, "effectPrefab", prefab.GetComponent<LabImpactParticleBurst>());
            SetPrivate(vfx, "battleCamera", camera);
            float originalTimeScale = Time.timeScale;

            try
            {
                Time.timeScale = 1f;
                for (int i = 0; i < 12; i++)
                    Assert.IsTrue(vfx.Play(target, new Vector3(i * .01f, 0f, 0f),
                        LabOrbElement.Fire, Vector3.forward));
                Assert.AreEqual(8, vfx.ActiveBurstCount,
                    "An approved newest hit should replace the oldest burst at the pool cap.");
                Assert.AreEqual(8, vfx.GetComponentsInChildren<LabImpactParticleBurst>(true).Length,
                    "Rapid hits must reuse a bounded number of effect objects.");

                Time.timeScale = 0f;
                Assert.IsFalse(vfx.Play(target, Vector3.zero, LabOrbElement.Fire, Vector3.forward),
                    "A paused battle must not begin a new hit effect.");
                yield return new WaitForSecondsRealtime(.08f);
                Assert.AreEqual(8, vfx.ActiveBurstCount,
                    "Pausing must not age out an already approved effect on realtime clock.");
                vfx.Clear();
                Assert.AreEqual(0, vfx.ActiveBurstCount);
                foreach (LabImpactParticleBurst burst in
                    vfx.GetComponentsInChildren<LabImpactParticleBurst>(true))
                    Assert.IsFalse(burst.gameObject.activeSelf,
                        "Round cleanup must stop every overlapping burst immediately.");
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                Object.Destroy(vfxObject);
                Object.Destroy(prefab);
                Object.Destroy(target.gameObject);
                Object.Destroy(cameraObject);
            }
        }

        private static LabImpactParticleBurst ActiveBurst(LabImpactParticleVfx vfx)
        {
            foreach (LabImpactParticleBurst burst in
                vfx.GetComponentsInChildren<LabImpactParticleBurst>(true))
                if (burst.gameObject.activeSelf) return burst;
            return null;
        }

        private static GameObject CreateBurstPrefab()
        {
#if UNITY_EDITOR
            // Exercise the saved artwork too: a synthetic ParticleSystem with Unity's
            // default material can appear magenta in URP while these logic tests pass.
            const string path = "Assets/Lab/Art/Effects/ImpactParticleBurst.prefab";
            var authored = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(authored, "The Lab scene's impact prefab must exist at " + path);
            Assert.IsNotNull(authored.GetComponent<LabImpactParticleBurst>());
            var savedInstance = Object.Instantiate(authored);
            savedInstance.name = "Test Impact Burst";
            savedInstance.SetActive(false);
            foreach (var renderer in savedInstance.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                Assert.IsNotNull(renderer.sharedMaterial, "Each authored particle needs a material.");
                Assert.IsTrue(renderer.sharedMaterial.shader.isSupported,
                    "The authored particle shader must render in this project.");
            }
            return savedInstance;
#else
            var prefab = new GameObject("Test Impact Burst");
            prefab.SetActive(false);
            var burst = prefab.AddComponent<LabImpactParticleBurst>();
            var coreObject = new GameObject("core", typeof(ParticleSystem));
            coreObject.transform.SetParent(prefab.transform, false);
            var core = coreObject.GetComponent<ParticleSystem>();
            var main = core.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = .15f;
            main.startLifetime = .35f;
            var emission = core.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)4) });
            SetPrivate(burst, "core", core);
            return prefab;
#endif
        }

        private static void SetPrivate(object instance, string fieldName, object value)
        {
            FieldInfo field = instance.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "Expected serialized field missing: " + fieldName);
            field.SetValue(instance, value);
        }
    }
}
