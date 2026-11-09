using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace C6Lab.Editor
{
    // Captures only the effect simulation on the main thread. The verifier separately renders the camera.
    internal static class LabFiveElementCostSample
    {
        private const int Volleys = 24;
        private const int Steps = 42;

        [Serializable]
        private sealed class Evidence
        {
            public string scope = "Warm Unity Editor main thread, five concurrent impacts, 42 updates at 1/60s per volley; excludes rendering and HUD. Renderer and triangle counts are submitted geometry estimates, not measured GPU draw calls.";
            public Sample normal, reduced;
        }

        [Serializable]
        private sealed class Sample
        {
            public int volleys = Volleys, updates = Volleys * Steps;
            public double medianUpdateMs, p95UpdateMs;
            public long managedBytesForUpdates;
            public int activeMeshRenderersAtRelease, trianglesAtRelease, transparentMeshRenderersAtRelease;
        }

        internal static void Measure(LabFiveElementsDemo demo, string output)
        {
            var pool = LabFiveElementVfx.ForScene(demo.gameObject.scene);
            var report = new Evidence();
            try
            {
                report.normal = MeasureQuality(pool, demo.diameter, false);
                report.reduced = MeasureQuality(pool, demo.diameter, true);
                File.WriteAllText(Path.Combine(output, "effect-cost.json"), JsonUtility.ToJson(report, true));
            }
            finally
            {
                pool.ClearAll();
                pool.LowDetail = false;
            }
        }

        private static Sample MeasureQuality(LabFiveElementVfx pool, float diameter, bool reduced)
        {
            pool.LowDetail = reduced;
            var sample = new Sample();
            var durations = new long[Volleys * Steps];
            int cursor = 0;
            // Two complete volleys populate the pools and exercise native mesh uploads before timing.
            for (int run = -2; run < Volleys; run++)
            {
                pool.ClearAll();
                for (int lane = 0; lane < 5; lane++)
                {
                    if (!pool.PlayImpact((LabOrbElement)(lane + 1), Vector3.right * (lane * 4f * diameter),
                        Vector3.up, new Vector3(.7f, -1f, .1f), diameter, .6f))
                        throw new InvalidOperationException("The cost sample could not acquire five impacts.");
                }
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int frame = 0; frame < Steps; frame++)
                {
                    long start = Stopwatch.GetTimestamp();
                    pool.Advance(1f / 60f);
                    long elapsed = Stopwatch.GetTimestamp() - start;
                    if (run >= 0) durations[cursor++] = elapsed;
                    if (run == -1 && frame == 20) CountVisibleGeometry(pool, sample);
                }
                if (run >= 0) sample.managedBytesForUpdates += GC.GetAllocatedBytesForCurrentThread() - before;
            }
            Array.Sort(durations);
            double ms = 1000d / Stopwatch.Frequency;
            sample.medianUpdateMs = durations[durations.Length / 2] * ms;
            sample.p95UpdateMs = durations[(int)(durations.Length * .95)] * ms;
            return sample;
        }

        private static void CountVisibleGeometry(LabFiveElementVfx pool, Sample sample)
        {
            foreach (var renderer in pool.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (!mesh) continue;
                sample.activeMeshRenderersAtRelease++;
                if (renderer.sharedMaterial.renderQueue >= 2501) sample.transparentMeshRenderersAtRelease++;
                for (int i = 0; i < mesh.subMeshCount; i++)
                    if (mesh.GetTopology(i) == MeshTopology.Triangles)
                        sample.trianglesAtRelease += (int)(mesh.GetIndexCount(i) / 3);
            }
        }
    }
}
