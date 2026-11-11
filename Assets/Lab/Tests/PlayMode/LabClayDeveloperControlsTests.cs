using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>Actual saved DEV slider + local Host flow, and a direct client receive-contract probe; not a remote transport test.</summary>
    public sealed class LabClayDeveloperControlsTests
    {
        readonly LabSavedSceneNetworkFixture fixture = new LabSavedSceneNetworkFixture();
        LabDeveloperMode ui;
        [UnitySetUp] public IEnumerator Load()
        {
            yield return fixture.Load();
            ui = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LabDeveloperMode>(true)).Single();
        }
        [UnityTearDown] public IEnumerator Cleanup() { Time.timeScale = 1; yield return fixture.Cleanup(); }

        [UnityTest]
        public IEnumerator PausedHostSlidersPreviewImmediatelyAndExplicitRestartKeepsBothValues()
        {
            string saved = JsonUtility.ToJson(ui.sourceConfig);
            Assert.That(ui.valueInputs.Length, Is.EqualTo(265));
            fixture.UseAvailablePort(ui.network);
            ui.Open(); ui.applyStartButton.onClick.Invoke();
            for (int i = 0; i < 120 && !ui.network.CanPauseBattle; i++) yield return null;
            Assert.That(ui.network.PauseBattle(), Is.True);
            yield return null;
            ui.Open(); Assert.That(ui.panel.activeSelf, Is.True);
            uint round = ui.network.Snapshot.round;
            string session = ui.network.Snapshot.sessionId;
            LabPaintSurface surface = ui.network.PaintSurface;
            uint revision = surface.Revision; int cells = surface.CoveredCount;
            float acceptedThickness = ui.network.config.ClayThickness;
            float acceptedGloss = ui.network.config.ClayGloss;

            ui.valueSliders[LabConfig.ClayThicknessField].value = 1;
            Assert.That(ui.network.ClayThickness, Is.EqualTo(.02f));
            Assert.That(ui.network.ClayGloss, Is.EqualTo(acceptedGloss));
            ui.valueSliders[LabConfig.ClayGlossField].value = 1;
            Assert.That(ui.network.ClayGloss, Is.EqualTo(1));
            Assert.That(ui.network.config.ClayThickness, Is.EqualTo(acceptedThickness), "A live preview is not a replacement accepted config.");
            Assert.That(ui.network.config.ClayGloss, Is.EqualTo(acceptedGloss));
            Assert.That(ui.network.Snapshot.round, Is.EqualTo(round));
            Assert.That(surface.Revision, Is.EqualTo(revision)); Assert.That(surface.CoveredCount, Is.EqualTo(cells));
            Assert.That(JsonUtility.ToJson(ui.sourceConfig), Is.EqualTo(saved));

            yield return null;
            var paint = ui.network.target.GetComponent<LabGourdPaint>();
            var coat = paint.VisualInstance.GetComponentsInChildren<Renderer>(true).Single(r=>r.sharedMaterial!=null && r.sharedMaterial.shader.name=="C6Lab/ClayStickerSurface");
            var appearance = new MaterialPropertyBlock();coat.GetPropertyBlock(appearance);
            Assert.That(appearance.GetFloat("_ClayThickness"),Is.EqualTo(.02f));
            Assert.That(appearance.GetFloat("_ClayGloss"),Is.EqualTo(1f));
            ui.valueSliders[LabConfig.ClayThicknessField].value=0;yield return null;coat.GetPropertyBlock(appearance);
            Assert.That(appearance.GetFloat("_ClayThickness"),Is.Zero);
            Assert.That(appearance.GetFloat("_ClayGloss"),Is.EqualTo(1f),"Thickness and gloss are independent.");
            ui.valueSliders[LabConfig.ClayGlossField].value=0;yield return null;coat.GetPropertyBlock(appearance);
            Assert.That(appearance.GetFloat("_ClayGloss"),Is.Zero);
            ui.valueSliders[LabConfig.ClayThicknessField].value=1;ui.valueSliders[LabConfig.ClayGlossField].value=1;yield return null;
            ui.Close(); ui.Open();
            Assert.That(float.Parse(ui.valueInputs[257].text, CultureInfo.InvariantCulture), Is.EqualTo(.02f));
            Assert.That(float.Parse(ui.valueInputs[258].text, CultureInfo.InvariantCulture), Is.EqualTo(1));
            ui.applyStartButton.onClick.Invoke();
            yield return null; yield return null;
            Assert.That(ui.network.Snapshot.sessionId, Is.Not.EqualTo(session));
            Assert.That(ui.network.config.ClayThickness, Is.EqualTo(.02f));
            Assert.That(ui.network.config.ClayGloss, Is.EqualTo(1));
            Assert.That(JsonUtility.ToJson(ui.sourceConfig), Is.EqualTo(saved));
        }

        [Test]
        public void ClientReceiveRequiresCurrentRoundConfigAndMonotonicSequence()
        {
            var go = new GameObject("Clay receive probe"); go.SetActive(false);
            var peer = go.AddComponent<LabNetwork>();
            try
            {
                peer.config = ui.sourceConfig;
                Put(peer, "latest", new LabSnapshot { sessionId = "clay-room", round = 9, phase = LabPhase.Playing, paused = true });
                Put(peer, "clientConfigReady", true); Put(peer, "configRevision", 3); Put(peer, "configFingerprint", "clay-config");
                var receive = typeof(LabNetwork).GetMethod("ReceiveClayAppearance", BindingFlags.Instance | BindingFlags.NonPublic);
                System.Type packetType = receive.GetParameters()[0].ParameterType;
                object Packet(string session, uint round, int configRevision, string fingerprint, long sequence, float thickness, float gloss)
                {
                    object p = System.Activator.CreateInstance(packetType);
                    foreach (var value in new (string, object)[] { ("sessionId",session),("round",round),("configRevision",configRevision),
                        ("configFingerprint",fingerprint),("requestId",sequence),("x",thickness),("y",gloss) })
                        packetType.GetField(value.Item1).SetValue(p,value.Item2);
                    return p;
                }
                receive.Invoke(peer,new[] { Packet("clay-room",9,3,"clay-config",10,.019f,.9f) });
                Assert.That(peer.ClayThickness, Is.EqualTo(.019f)); Assert.That(peer.ClayGloss, Is.EqualTo(.9f));
                foreach (object invalid in new[] {
                    Packet("old-room",9,3,"clay-config",11,.001f,0), Packet("clay-room",8,3,"clay-config",11,.001f,0),
                    Packet("clay-room",9,2,"clay-config",11,.001f,0), Packet("clay-room",9,3,"old-config",11,.001f,0),
                    Packet("clay-room",9,3,"clay-config",9,.001f,0), Packet("clay-room",9,3,"clay-config",10,.001f,0),
                    Packet("clay-room",9,3,"clay-config",11,float.NaN,0) }) receive.Invoke(peer,new[] {invalid});
                Assert.That(peer.ClayThickness, Is.EqualTo(.019f)); Assert.That(peer.ClayGloss, Is.EqualTo(.9f));
                Assert.That(peer.config, Is.SameAs(ui.sourceConfig));
            }
            finally { Object.DestroyImmediate(go); }
        }
        static void Put(object target,string field,object value) => typeof(LabNetwork).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    }
}
