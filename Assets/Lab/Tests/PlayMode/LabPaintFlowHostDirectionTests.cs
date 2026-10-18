using System.Collections;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    public sealed class LabPaintFlowHostDirectionTests
    {
        private LabNetwork network;
        private LabConfig original, fixture;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            if (network != null)
            {
                if (network.IsConnected) network.Stop();
                network.config = original;
                Object.Destroy(network.gameObject);
            }
            if (fixture != null) Object.Destroy(fixture);
            yield return null;
            Scene scene = SceneManager.GetSceneByName("Lab");
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            SceneManager.SetActiveScene(SceneManager.CreateScene("Host flow cleanup"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator SavedHostSamplesWorldDownInTheSameLocalSpaceAsPaintAndStopsTheBattleClockOnPause()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            // NGO can preserve earlier fixture networks in DontDestroyOnLoad. Use the
            // saved Scene's explicit UI reference, never a global first-object lookup.
            network = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LabDeveloperMode>(true)).Single().network;
            Assert.That(network.target, Is.Not.Null);
            original = network.config;
            fixture = Object.Instantiate(original);
            string source = JsonUtility.ToJson(original);
            try
            {
                float[] values = fixture.CaptureDeveloperValues(); values[133] = 1; values[139] = 0;
                Assert.That(fixture.TryApplyDeveloperValues(values, out string error), Is.True, error);
                network.config = fixture; network.port = 19898;
                Assert.That(network.StartDeveloperSolo(), Is.True, network.Notice);
                yield return null;
                var room = (LabModel)typeof(LabNetwork).GetField("model", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(network);
                var clock = (LabBattleClock)typeof(LabNetwork).GetField("battleClock", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(network);
                var sample = typeof(LabNetwork).GetMethod("SampleHostPaintFlow", BindingFlags.Instance | BindingFlags.NonPublic);
                Transform mount = network.target.VisualMount;
                Quaternion rotation = mount.rotation;
                try
                {
                    mount.rotation = Quaternion.Euler(0f, 25f, 65f);
                    Vector3 expected = mount.InverseTransformDirection(Vector3.down).normalized;
                    Assert.That(Vector3.Distance(expected, Vector3.down), Is.GreaterThan(.5f));
                    sample.Invoke(network, new object[] { clock.Now(Time.realtimeSinceStartupAsDouble) });
                    Assert.That(Vector3.Distance(room.PaintSurface.FlowDirection, expected), Is.LessThan(.00001f));
                    Assert.That(network.PauseBattle(), Is.True);
                    double played = room.Snapshot(clock.Now(Time.realtimeSinceStartupAsDouble)).playedSeconds;
                    for (int i = 0; i < 3; i++)
                    {
                        yield return null;
                        Assert.That(room.Snapshot(clock.Now(Time.realtimeSinceStartupAsDouble)).playedSeconds, Is.EqualTo(played));
                    }
                    Assert.That(JsonUtility.ToJson(original), Is.EqualTo(source));
                }
                finally { mount.rotation = rotation; }
            }
            finally
            {
                if (network.IsConnected) network.Stop();
                network.config = original; Time.timeScale = 1f;
            }
        }
    }
}
