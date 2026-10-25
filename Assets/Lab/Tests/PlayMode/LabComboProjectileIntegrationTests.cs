using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace C6Lab.Tests
{
    /// <summary>Cross-feature presentation/physics checks, not a remote transport or hit-accuracy test.</summary>
    public sealed class LabComboProjectileIntegrationTests
    {
        [UnityTest]
        public IEnumerator ComboAndBlastScalesMatchOnHostAndReplicaWithTheAuthoredSphereArt()
        {
            LabConfig source = Resources.Load<LabConfig>("LabConfig");
            Assert.That(source, Is.Not.Null);
            string sourceBefore = JsonUtility.ToJson(source);
            var config = Object.Instantiate(source);
            var target = LabTarget.CreateCylinder(null, new Vector3(500f, 1000f, 500f), .8f, 2f);
            try
            {
                foreach (float comboScale in new[] { .3f, 1.5f })
                {
                    const float blastScale = .5f;
                    float radius = config.ThrowRadius * blastScale * comboScale;
                    float gravity = config.EffectiveThrowGravity;
                    LabProjectile host = LabProjectile.Create(new Vector3(-5f, 1000f, 0f), config,
                        element: LabOrbElement.Fire, sizeScale: blastScale);
                    LabProjectile replica = LabProjectile.Create(new Vector3(5f, 1000f, 0f), config,
                        element: LabOrbElement.Fire, sizeScale: blastScale);
                    try
                    {
                        host.SetRadiusScale(comboScale);
                        // Round-trip the actual projectile notice field used by the client path.
                        System.Type packetType = typeof(LabNetwork).Assembly.GetType("C6Lab.LabNetworkPacket");
                        Assert.That(packetType, Is.Not.Null);
                        object notice = System.Activator.CreateInstance(packetType);
                        packetType.GetField("type").SetValue(notice, "projectile");
                        packetType.GetField("sizeScale").SetValue(notice, comboScale);
                        object received = JsonUtility.FromJson(JsonUtility.ToJson(notice), packetType);
                        replica.SetRadiusScale((float)packetType.GetField("sizeScale").GetValue(received));
                        Assert.That(host.Launch("combo-scaled-host", 10, Vector3.forward,
                            config, target, _ => Assert.Fail("The far-away target must not be reached.")), Is.True);
                        Assert.That(replica.LaunchVisual(Vector3.forward, config, target), Is.True);
                        yield return null;
                        AssertScaledSphere(host, config, radius, false);
                        AssertScaledSphere(replica, config, radius, true);

                        // Expired/reconfigured combo settings apply to future throws, not these shots.
                        float[] changed = config.CaptureDeveloperValues();
                        changed[155] = 0f;
                        changed[160] = .19f;
                        changed[163] = 1f;
                        changed[33] = gravity * 2f;
                        Assert.That(config.TryApplyDeveloperValues(changed, out string error), Is.True, error);
                        yield return new WaitForFixedUpdate();
                        AssertScaledSphere(host, config, radius, false);
                        AssertScaledSphere(replica, config, radius, true);
                        Assert.That(host.GravityAcceleration, Is.EqualTo(gravity));
                        Assert.That(replica.GravityAcceleration, Is.EqualTo(gravity));
                    }
                    finally
                    {
                        if (host != null) Object.Destroy(host.gameObject);
                        if (replica != null) Object.Destroy(replica.gameObject);
                    }
                    yield return null;
                }
                Assert.That(JsonUtility.ToJson(source), Is.EqualTo(sourceBefore));
            }
            finally
            {
                Object.Destroy(target.gameObject);
                Object.Destroy(config);
            }
        }

        [Test]
        public void TheFallbackTrailUsesTheCombinedBlastAndComboRadius()
        {
            var config = ScriptableObject.CreateInstance<LabConfig>();
            var target = LabTarget.CreateCylinder(null, new Vector3(500f, 1000f, 500f), .8f, 2f);
            try
            {
                foreach (float comboScale in new[] { .3f, 1.5f })
                {
                    LabProjectile host = LabProjectile.Create(new Vector3(-5f, 1000f, 0f), config,
                        element: LabOrbElement.Fire, sizeScale: .5f);
                    LabProjectile replica = LabProjectile.Create(new Vector3(5f, 1000f, 0f), config,
                        element: LabOrbElement.Fire, sizeScale: .5f);
                    try
                    {
                        host.SetRadiusScale(comboScale);
                        replica.SetRadiusScale(comboScale);
                        host.Launch("fallback-combo", 10, Vector3.forward, config, target, _ => { });
                        replica.LaunchVisual(Vector3.forward, config, target);
                        float radius = config.ThrowRadius * .5f * comboScale;
                        foreach (LabProjectile shot in new[] { host, replica })
                        {
                            Assert.That(shot.FlightTrail, Is.Not.Null);
                            Assert.That(shot.FlightTrail.widthMultiplier, Is.EqualTo(radius * .75f).Within(.0001f));
                            Assert.That(shot.FlightTrail.emitting, Is.True);
                            Assert.That(shot.GetComponent<SphereCollider>().radius,
                                Is.EqualTo(radius).Within(.0001f));
                        }
                    }
                    finally
                    {
                        Object.DestroyImmediate(host.gameObject);
                        Object.DestroyImmediate(replica.gameObject);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(target.gameObject);
                Object.DestroyImmediate(config);
            }
        }

        [UnityTest]
        public IEnumerator TheHudReadsFourHostColorsAndHidesOnPauseAndANewOffRound()
        {
            var root = new GameObject("Combo HUD snapshot fixture");
            var network = root.AddComponent<LabNetwork>();
            network.enabled = false; // No sockets/real-time snapshots in this presentation fixture.
            var canvasRoot = new GameObject("Combo fixture canvas", typeof(RectTransform), typeof(Canvas));
            var canvas = canvasRoot.GetComponent<Canvas>();
            var hud = root.AddComponent<LabGourdComboHud>();
            hud.Bind(network, canvas);
            var colors = new[] { LabOrbElement.Fire, LabOrbElement.Water, LabOrbElement.Metal, LabOrbElement.Earth };
            var snapshot = new LabSnapshot
            {
                sessionId = "combo-hud", round = 1, phase = LabPhase.Playing,
                serverTime = 2d, comboStartedAt = 1d, comboEndsAt = 11d,
                comboCount = 1, comboActive = true, comboColors = colors, comboProgress = 2
            };
            FieldInfo latest = typeof(LabNetwork).GetField("latest", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(latest, Is.Not.Null);
            try
            {
                latest.SetValue(network, JsonUtility.FromJson<LabSnapshot>(JsonUtility.ToJson(snapshot)));
                yield return null;
                Assert.That(hud.PanelVisible, Is.True);
                for (int i = 0; i < colors.Length; i++)
                {
                    Assert.That(hud.SlotColor(i), Is.EqualTo((Color)LabPaintPalette.SrgbColorFor(colors[i])));
                    Assert.That(hud.SlotChecked(i), Is.EqualTo(i < 2));
                }
                foreach (Graphic graphic in canvasRoot.GetComponentsInChildren<Graphic>(true))
                    Assert.That(graphic.raycastTarget, Is.False, "Combo presentation cannot steal throw/board input.");
                snapshot.paused = true;
                latest.SetValue(network, snapshot);
                yield return null;
                Assert.That(canvasRoot.transform.Find("Gourd Combo").gameObject.activeSelf, Is.False);
                snapshot.paused = false;
                latest.SetValue(network, snapshot);
                yield return null;
                Assert.That(hud.PanelVisible, Is.True);
                latest.SetValue(network, new LabSnapshot
                {
                    sessionId = "combo-off", round = 1, phase = LabPhase.Playing, comboCount = 0
                });
                yield return null;
                Assert.That(canvasRoot.transform.Find("Gourd Combo").gameObject.activeSelf, Is.False,
                    "An OFF round has no combo count, so no stale previous-round display can remain.");
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(canvasRoot);
            }
        }

        [UnityTest]
        public IEnumerator TheWarningCountsDownWithoutColorsOrBadgeThenTheComboAppears()
        {
            var root = new GameObject("Combo warning fixture");
            var network = root.AddComponent<LabNetwork>();
            network.enabled = false;
            var canvasRoot = new GameObject("Combo warning canvas", typeof(RectTransform), typeof(Canvas));
            var hud = root.AddComponent<LabGourdComboHud>();
            hud.Bind(network, canvasRoot.GetComponent<Canvas>());
            var colors = new[] { LabOrbElement.Fire, LabOrbElement.Water, LabOrbElement.Metal, LabOrbElement.Earth };
            // The first combo already ended with a penalty for this player; the next one is 1.5 s away.
            var waiting = new LabSnapshot
            {
                sessionId = "combo-warning", round = 1, phase = LabPhase.Playing, serverTime = 31.5d,
                comboCount = 1, comboActive = false, comboColors = colors, comboProgress = 1,
                comboWarningAt = 30d, comboNextAt = 33d,
                players = new[] { new LabPlayerState { id = network.LocalPlayerId, comboPenaltyUntil = 40d } }
            };
            FieldInfo latest = typeof(LabNetwork).GetField("latest", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(latest, Is.Not.Null);
            try
            {
                latest.SetValue(network, JsonUtility.FromJson<LabSnapshot>(JsonUtility.ToJson(waiting)));
                yield return null;
                Assert.That(hud.WarningVisible, Is.True);
                Assert.That(hud.WarningCount, Is.EqualTo("2"), "1.5 s left shows 2, counting 3, 2, 1.");
                Assert.That(hud.PanelVisible, Is.False, "The colors stay hidden until the combo starts.");
                Assert.That(hud.EffectVisible, Is.False, "The SMALL badge steps aside during the warning.");
                foreach (Graphic graphic in canvasRoot.GetComponentsInChildren<Graphic>(true))
                    Assert.That(graphic.raycastTarget, Is.False, "The warning cannot steal throw/board input.");

                // The very first combo has a warning too, before any combo has been counted.
                latest.SetValue(network, new LabSnapshot
                {
                    sessionId = "combo-warning", round = 2, phase = LabPhase.Playing, serverTime = 17.2d,
                    comboWarningAt = 17d, comboNextAt = 20d
                });
                yield return null;
                Assert.That(hud.WarningVisible, Is.True);
                Assert.That(hud.WarningCount, Is.EqualTo("3"));

                latest.SetValue(network, new LabSnapshot
                {
                    sessionId = "combo-warning", round = 2, phase = LabPhase.Playing, serverTime = 20d,
                    comboCount = 1, comboActive = true, comboColors = colors, comboStartedAt = 20d, comboEndsAt = 30d
                });
                yield return null;
                Assert.That(hud.WarningVisible, Is.False);
                Assert.That(hud.PanelVisible, Is.True, "At zero the four slots appear.");
                Assert.That(hud.SlotColor(0), Is.EqualTo((Color)LabPaintPalette.SrgbColorFor(colors[0])));
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(canvasRoot);
            }
        }

        private static void AssertScaledSphere(LabProjectile shot, LabConfig config, float radius, bool replica)
        {
            SphereCollider collider = shot.GetComponent<SphereCollider>();
            Assert.That(collider.radius, Is.EqualTo(radius).Within(.0001f));
            Assert.That(collider.isTrigger, Is.EqualTo(replica));
            Assert.That(shot.GetComponentsInChildren<Collider>().Length, Is.EqualTo(1));
            Assert.That(shot.GetComponentsInChildren<Rigidbody>().Length, Is.EqualTo(1));
            Assert.That(shot.VisualMount.GetComponentsInChildren<LabOrbSpriteArt>(), Is.Empty,
                "Flight must keep its 3D presentation while the board retains its sprite art.");
            Transform surface = shot.VisualMount.Find("Replacement Visual/Sphere");
            Assert.That(surface, Is.Not.Null, "The saved appearance supplies the flight sphere prefab.");
            MeshRenderer renderer = surface.GetComponent<MeshRenderer>();
            Assert.That(renderer != null && renderer.gameObject.activeInHierarchy, Is.True);
            MeshFilter filter = surface.GetComponent<MeshFilter>();
            Assert.That(filter != null && filter.sharedMesh != null, Is.True);
            Assert.That(filter.sharedMesh.bounds.size.z, Is.GreaterThan(.1f), "The authored flight mesh has depth.");
            foreach (float axis in new[] { renderer.bounds.size.x, renderer.bounds.size.y, renderer.bounds.size.z })
                Assert.That(axis, Is.EqualTo(2f * radius).Within(.001f),
                    "Blast and combo scales must agree between the visual sphere and its collision radius.");
            Assert.That(renderer.sharedMaterial,
                Is.SameAs(config.OrbAppearance.MaterialFor(LabOrbKind.Combined, LabOrbElement.Fire)));
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            Color expected = config.OrbAppearance.ElementColorFor(LabOrbElement.Fire);
            foreach (string colorProperty in new[] { "_BaseColor", "_Color" })
            {
                Color actual = properties.GetColor(colorProperty);
                Assert.That(actual.r, Is.EqualTo(expected.r).Within(.0001f));
                Assert.That(actual.g, Is.EqualTo(expected.g).Within(.0001f));
                Assert.That(actual.b, Is.EqualTo(expected.b).Within(.0001f));
            }
            Assert.That(shot.ElementFlightTrail, Is.Not.Null, "The authored elemental trail survives combo sizing.");
        }
    }
}
