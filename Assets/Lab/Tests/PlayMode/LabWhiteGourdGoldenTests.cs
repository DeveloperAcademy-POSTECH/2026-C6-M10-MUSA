using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>
    /// #507 PR-1: the white gourd's mount, hitbox, paint binding and yokai poses are pinned to values recorded on main
    /// (6524427b) before the gourd model contract (`LabGourdVisualRig`) was added. Stages 1 and 3 must not change.
    /// #473 re-recorded only the mount golden, at the new default Gourd Visual Size 1.54; the yokai golden is unchanged.
    /// Set GOURD_GOLDEN_RECORD=1 to rewrite the golden files, only from a commit whose white gourd is known good.
    /// </summary>
    public sealed class LabWhiteGourdGoldenTests
    {
        private const double Tolerance = 1e-4;
        private static string GoldenFolder => Path.Combine(Application.dataPath, "Lab/Tests/PlayMode/Golden");
        private static bool Recording => Environment.GetEnvironmentVariable("GOURD_GOLDEN_RECORD") == "1";
        private readonly LabSavedSceneNetworkFixture fixture = new LabSavedSceneNetworkFixture();

        [UnityTearDown]
        public IEnumerator UnloadSavedScene() => fixture.Cleanup();

        [UnityTest]
        public IEnumerator TheMountedWhiteGourdMatchesTheRecordedMountHitboxAndPaintBinding()
        {
            yield return fixture.Load();
            var paint = Object.FindFirstObjectByType<LabGourdPaint>();
            Assert.That(paint, Is.Not.Null);
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            paint.RefreshCommittedMap();
            LabTarget target = paint.target;
            var lines = new List<string>();
            Transform visual = paint.VisualInstance.transform;
            lines.Add("visualPrefab=" + paint.visualPrefab.name);
            lines.Add("visualParentIsMount=" + (visual.parent == target.VisualMount));
            Pose(lines, "visual", visual);
            lines.Add("shellPath=" + PathFrom(visual, paint.Shell));
            Pose(lines, "shell", paint.Shell);
            // Relative to the visual mount: the hanging rope's automatic cycle moves the mount itself over time.
            Matrix(lines, "shellInMount", target.VisualMount.worldToLocalMatrix * paint.Shell.localToWorldMatrix);
            Mesh hit = target.Hitbox.sharedMesh;
            lines.Add("hitMeshIsShellMesh=" + (hit == paint.Shell.GetComponent<MeshFilter>().sharedMesh));
            lines.Add("hitMeshName=" + hit.name);
            lines.Add("hitMeshVertices=" + hit.vertexCount);
            Vector(lines, "hitMeshBoundsCenter", hit.bounds.center);
            Vector(lines, "hitMeshBoundsExtents", hit.bounds.extents);
            Matrix(lines, "hitboxInMount", target.VisualMount.worldToLocalMatrix * target.Hitbox.transform.localToWorldMatrix);
            Renderer shell = paint.Shell.GetComponent<Renderer>();
            lines.Add("shellMaterial=" + shell.sharedMaterial.name);
            lines.Add("shellMaterialIsPaint=" + (shell.sharedMaterial == paint.paintMaterial));
            var block = new MaterialPropertyBlock();
            shell.GetPropertyBlock(block);
            foreach (string name in new[] { "_PaintMapReady", "_PaintGrainEnabled", "_PaintGrainSize", "_PaintGrainStrength",
                         "_PaintMottleStrength", "_PlayerPaintPatternsEnabled" })
                lines.Add(name + "=" + Number(block.GetFloat(name)));
            lines.Add("paintMapIsVisual=" + (block.GetTexture("_PaintMap") == paint.VisualPaintTexture));
            lines.Add("painterMapIsVisual=" + (block.GetTexture("_PainterMap") == paint.VisualPainterTexture));
            var yokai = paint.VisualInstance.GetComponent<LabGourdYokai>();
            lines.Add("yokaiReady=" + (yokai != null && yokai.Ready));
            lines.Add("animatorsEnabled=" + Array.FindAll(paint.VisualInstance.GetComponentsInChildren<Animator>(true), a => a.enabled).Length);
            Compare("WhiteGourdMount.txt", lines);
        }

        [Test]
        public void TheWhiteGourdYokaiFollowsTheRecordedPoseSequenceForEveryEvent()
        {
            GameObject gourd = Object.Instantiate(Resources.Load<GameObject>("GourdPaint/WhiteGourd"));
            try
            {
                var yokai = gourd.AddComponent<LabGourdYokai>();
                yokai.enabled = false;
                Assert.That(yokai.Ready, Is.True);
                Animation clip = gourd.GetComponentInChildren<Animation>(true);
                Transform root = clip.transform.Find("YokaiRoot"), lid = clip.transform.Find("LidPivot");
                Transform eye = clip.transform.Find("YokaiRoot/EyePivot_L");
                var lines = new List<string>();
                int step = 0;
                void Record(string label)
                {
                    string prefix = (step++).ToString("D3") + "." + label;
                    lines.Add(prefix + ".holding=" + yokai.Holding + " flinch=" + yokai.Flinching + " pop=" + yokai.Popping);
                    lines.Add(prefix + ".clip=" + Number(yokai.ClipTime));
                    Pose(lines, prefix + ".root", root);
                    Pose(lines, prefix + ".lid", lid);
                    Pose(lines, prefix + ".eye", eye);
                }
                void Run(LabSnapshot snapshot, double from, float seconds, string label)
                {
                    float done = 0f;
                    for (int i = 0; done < seconds - 1e-4f; i++, done += .05f)
                    {
                        yokai.Advance(snapshot, from + done, .05f);
                        if (i % 3 == 0) Record(label);
                    }
                }

                AnimationState state = clip[clip.clip.name];
                state.enabled = true; state.weight = 1f; state.speed = 0f; state.time = 5.6f;
                clip.Sample();
                Record("closed");

                // Combo warning → combo with three hits → timeout.
                Run(Waiting(17.1d), 17.1d, 2.5f, "warning");
                Run(Active(20d, 0), 20d, 1f, "combo");
                Run(Active(21d, 1), 21d, .6f, "hit1");
                Run(Active(22d, 2), 22d, .6f, "hit2");
                Run(Active(22.9d, 3), 22.9d, .6f, "hit3");
                LabSnapshot ended = Waiting(30.1d);
                ended.comboCount = 1; ended.comboResult = LabComboResult.Timeout; ended.comboResultCount = 1;
                ended.comboWarningAt = 32d; ended.comboNextAt = 35d;
                yokai.Advance(ended, 30.1d, 0f);
                Record("released");

                // Bomb drop from a closed lid: quick rise, pop, back in when the bubble goes.
                state.enabled = true; state.speed = 0f; state.time = 5.6f; clip.Sample();
                yokai.BombDropped();
                Run(Calm(40d), 40d, yokai.BombShowSeconds + .3f, "bomb");

                // Flash warning → flash → stay → back in.
                state.enabled = true; state.speed = 0f; state.time = 5.6f; clip.Sample();
                var warning = Calm(50d);
                warning.flashWarningAt = 50d; warning.flashAt = 51.5d;
                Run(warning, 50d, 1.5f, "flashWarning");
                var flashed = Calm(51.5d);
                flashed.flashCount = 1; flashed.flashLastAt = 51.5d;
                Run(flashed, 51.5d, 1f, "flash");

                Compare("WhiteGourdYokai.txt", lines);
            }
            finally { Object.Destroy(gourd); }
        }

        private static LabSnapshot Calm(double now) => new LabSnapshot
        {
            sessionId = "yokai-golden", round = 1, phase = LabPhase.Playing, serverTime = now
        };

        private static LabSnapshot Waiting(double now)
        {
            LabSnapshot snapshot = Calm(now);
            snapshot.comboWarningAt = 17d; snapshot.comboNextAt = 20d;
            return snapshot;
        }

        private static LabSnapshot Active(double now, int progress)
        {
            LabSnapshot snapshot = Calm(now);
            snapshot.comboCount = 1; snapshot.comboActive = true; snapshot.comboProgress = progress;
            snapshot.comboColors = new[] { LabOrbElement.Fire, LabOrbElement.Water, LabOrbElement.Wood, LabOrbElement.Earth };
            snapshot.comboStartedAt = 20d; snapshot.comboEndsAt = 30d;
            return snapshot;
        }

        private static void Compare(string file, List<string> actual)
        {
            string path = Path.Combine(GoldenFolder, file);
            if (Recording)
            {
                Directory.CreateDirectory(GoldenFolder);
                File.WriteAllLines(path, actual);
                Assert.Pass("Recorded " + actual.Count + " golden lines to " + file);
            }
            Assert.That(File.Exists(path), Is.True, "Golden file missing: " + file);
            string[] expected = File.ReadAllLines(path);
            Assert.That(actual.Count, Is.EqualTo(expected.Length), file + " line count");
            for (int i = 0; i < expected.Length; i++)
                Assert.That(Same(expected[i], actual[i]), Is.True,
                    file + " line " + (i + 1) + "\n expected: " + expected[i] + "\n actual:   " + actual[i]);
        }

        /// <summary>Text must match exactly; numbers (space/comma separated after '=') within a small tolerance.</summary>
        private static bool Same(string expected, string actual)
        {
            if (expected == actual) return true;
            string[] e = expected.Split(' ', ',', '=', '(', ')'), a = actual.Split(' ', ',', '=', '(', ')');
            if (e.Length != a.Length) return false;
            for (int i = 0; i < e.Length; i++)
            {
                if (e[i] == a[i]) continue;
                if (!double.TryParse(e[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double x)
                    || !double.TryParse(a[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double y)
                    || Math.Abs(x - y) > Tolerance) return false;
            }
            return true;
        }

        private static string Number(float value) => value.ToString("0.000000", CultureInfo.InvariantCulture);
        private static string Number(double value) => value.ToString("0.000000", CultureInfo.InvariantCulture);

        private static void Vector(List<string> lines, string key, Vector3 v)
            => lines.Add(key + "=" + Number(v.x) + "," + Number(v.y) + "," + Number(v.z));

        private static void Pose(List<string> lines, string key, Transform t)
        {
            Vector(lines, key + ".position", t.localPosition);
            Quaternion r = t.localRotation;
            // q and -q are the same rotation; pin the sign so the text is stable.
            if (r.w < 0f || r.w == 0f && r.x < 0f) r = new Quaternion(-r.x, -r.y, -r.z, -r.w);
            lines.Add(key + ".rotation=" + Number(r.x) + "," + Number(r.y) + "," + Number(r.z) + "," + Number(r.w));
            Vector(lines, key + ".scale", t.localScale);
        }

        private static void Matrix(List<string> lines, string key, Matrix4x4 m)
        {
            var values = new string[12];
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 4; column++)
                    values[row * 4 + column] = Number(m[row, column]);
            lines.Add(key + "=" + string.Join(",", values));
        }

        private static string PathFrom(Transform root, Transform child)
        {
            string path = child.name;
            for (Transform t = child.parent; t != null && t != root; t = t.parent) path = t.name + "/" + path;
            return path;
        }
    }
}
