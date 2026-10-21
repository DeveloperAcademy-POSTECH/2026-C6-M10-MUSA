using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>
    /// 박 속 요괴 (#244, #250): 콤보 예고부터 콤보가 끝날 때까지 요괴가 나와 있고, 칸을 맞히면 움찔하며,
    /// 박이 폭탄을 떨어뜨리면 말풍선이 떠 있는 동안 쏙 나와 톡 튀고,
    /// 평소 반복의 어느 순간에 콤보가 시작돼도 모습이 튀지 않는다. 저장된 WhiteGourd 프리팹과 애니메이션을
    /// 그대로 쓰고, 시간은 Advance로 직접 넘긴다.
    /// </summary>
    public sealed class LabGourdYokaiTests
    {
        private const float Up = .0194f, Down = .0163f; // YokaiRoot 높이 (애니메이션 키)

        private GameObject gourd;
        private LabGourdYokai yokai;
        private Animation clipPlayer;
        private Transform root, lid, eye;

        [UnitySetUp]
        public IEnumerator MakeGourd()
        {
            var prefab = Resources.Load<GameObject>("GourdPaint/WhiteGourd");
            Assert.That(prefab, Is.Not.Null);
            gourd = Object.Instantiate(prefab);
            yokai = gourd.AddComponent<LabGourdYokai>();
            yield return null;
            Assert.That(yokai.Ready, Is.True, "The saved gourd has the demo animation and a YokaiRoot.");
            // The test drives time itself; the component's own LateUpdate would read a missing network.
            yokai.enabled = false;
            clipPlayer = gourd.GetComponentInChildren<Animation>(true);
            root = clipPlayer.transform.Find("YokaiRoot");
            lid = clipPlayer.transform.Find("LidPivot");
            eye = clipPlayer.transform.Find("YokaiRoot/EyePivot_L");
            Assert.That(new Object[] { root, lid, eye }, Has.None.Null);
        }

        [TearDown]
        public void DestroyGourd()
        {
            if (gourd != null) Object.Destroy(gourd);
        }

        [Test]
        public void TheYokaiComesOutForTheWarningStaysForTheComboAndGoesBackAfter()
        {
            SetClip(5.6f); // closed, in the middle of the ordinary loop
            Assert.That(root.localPosition.z, Is.EqualTo(Down).Within(.0002f));

            var warning = Waiting(17.1d);
            yokai.Advance(warning, 17.1d, 0f);
            Assert.That(yokai.Holding, Is.True, "COMBO! 3 brings the yokai out.");
            Step(warning, 17.1d, 2.5f);
            Assert.That(root.localPosition.z, Is.EqualTo(Up).Within(.0002f), "Fully out before the four slots appear.");
            Assert.That(Mathf.Abs(Lid()), Is.EqualTo(65f).Within(1f), "The lid is open.");

            var combo = Active(20d, 0);
            float lowestEye = 0f, highestEye = 0f;
            for (int i = 0; i < 400; i++) // 20 seconds of combo: it never goes back in
            {
                yokai.Advance(combo, 20d + i * .05d, .05f);
                Assert.That(yokai.Holding, Is.True);
                Assert.That(root.localPosition.z, Is.EqualTo(Up).Within(.0002f));
                lowestEye = Mathf.Min(lowestEye, Eye());
                highestEye = Mathf.Max(highestEye, Eye());
            }
            Assert.That(highestEye, Is.GreaterThan(20f), "It keeps looking around: left...");
            Assert.That(lowestEye, Is.LessThan(-20f), "...and right.");

            var ended = Waiting(30.1d);
            ended.comboCount = 1;
            ended.comboResult = LabComboResult.Timeout;
            ended.comboResultCount = 1;
            ended.comboWarningAt = 32d;
            ended.comboNextAt = 35d;
            yokai.Advance(ended, 30.1d, 0f);
            Assert.That(yokai.Holding, Is.False, "When the combo ends the ordinary animation takes over.");
            Assert.That(clipPlayer[clipPlayer.clip.name].speed, Is.EqualTo(1f));
        }

        [Test]
        public void HittingASlotMakesTheYokaiFlinchOnce()
        {
            SetClip(2.4f);
            yokai.Advance(Active(20d, 0), 20d, 0f);
            Vector3 normal = root.localScale;
            Assert.That(yokai.Flinching, Is.False);

            yokai.Advance(Active(21d, 1), 21d, .05f);
            Assert.That(yokai.Flinching, Is.True);
            Assert.That(root.localScale.x, Is.LessThan(normal.x * .95f), "It shrinks back first...");
            Step(Active(21d, 1), 21d, LabGourdYokai.FlinchSeconds);
            Assert.That(yokai.Flinching, Is.False);
            Assert.That(root.localScale, Is.EqualTo(normal), "...then is its usual size again.");
            Step(Active(22d, 1), 22d, 1f);
            Assert.That(yokai.Flinching, Is.False, "One hit, one flinch.");

            // The last slot ends the combo, but the yokai still flinches before going back in.
            var success = Waiting(23d);
            success.comboCount = 1;
            success.comboProgress = 4;
            success.comboResult = LabComboResult.Success;
            success.comboResultCount = 1;
            success.comboWarningAt = 32d;
            success.comboNextAt = 35d;
            yokai.Advance(Active(22.9d, 3), 22.9d, .05f);
            Step(Active(22.9d, 3), 22.9d, 1f);
            yokai.Advance(success, 23d, .05f);
            Assert.That(yokai.Flinching, Is.True);
            Assert.That(yokai.Holding, Is.False);
        }

        [Test]
        public void AComboStartingAnywhereInTheOrdinaryLoopDoesNotJump()
        {
            for (float t = 0f; t < LabGourdYokai.ClipLength; t += .1f)
            {
                SetClip(t);
                float height = root.localPosition.z, lidAngle = Lid(), eyeAngle = Eye();
                yokai.Advance(Waiting(17.1d), 17.1d, 0f);
                Assert.That(yokai.Holding, Is.True);
                Assert.That(root.localPosition.z, Is.EqualTo(height).Within(.0002f), "Height at " + t);
                Assert.That(Lid(), Is.EqualTo(lidAngle).Within(5f), "Lid at " + t);
                Assert.That(Eye(), Is.EqualTo(eyeAngle).Within(2f), "Eyes at " + t);
                yokai.Advance(null, 0d, 0f); // back to the ordinary loop for the next sample
                Assert.That(yokai.Holding, Is.False);
            }
        }

        [Test]
        public void NoComboOrAPausedWarningKeepsTheOrdinaryLoopOrHoldsStill()
        {
            SetClip(1f);
            var calm = new LabSnapshot { sessionId = "yokai", round = 1, phase = LabPhase.Playing, serverTime = 10d };
            yokai.Advance(calm, 10d, .1f);
            Assert.That(yokai.Holding, Is.False, "No combo (or combo OFF): the ordinary 6-second loop.");

            var warning = Waiting(17.5d);
            yokai.Advance(warning, 17.5d, 0f);
            float before = yokai.ClipTime;
            warning.paused = true;
            yokai.Advance(warning, 17.5d, 0f); // paused frames pass no time
            Assert.That(yokai.Holding, Is.True, "A pause during the combo keeps the yokai out.");
            Assert.That(yokai.ClipTime, Is.EqualTo(before));

            var over = new LabSnapshot { sessionId = "yokai", round = 1, phase = LabPhase.Victory, serverTime = 18d,
                comboWarningAt = 17d, comboNextAt = 20d };
            yokai.Advance(over, 18d, 0f);
            Assert.That(yokai.Holding, Is.False, "Only during the battle.");
        }

        [Test]
        public void ABombDropBringsTheYokaiOutQuicklyWithAPopForAsLongAsTheBubble()
        {
            SetClip(5.6f); // closed
            var calm = Calm(40d);
            yokai.BombDropped();
            yokai.Advance(calm, 40d, 0f);
            Assert.That(yokai.Holding, Is.True, "The gourd drops a bomb: out comes the yokai.");
            Assert.That(yokai.BombSecondsLeft, Is.EqualTo(yokai.BombShowSeconds));

            bool popped = false;
            float elapsed = 0f;
            for (; elapsed < .6f; elapsed += .05f)
            {
                yokai.Advance(calm, 40d + elapsed, .05f);
                popped |= yokai.Popping;
            }
            Assert.That(root.localPosition.z, Is.GreaterThan(Up - .0004f), "Out quickly, well before the bubble goes.");
            Assert.That(popped, Is.True, "It pops once it is out.");
            Assert.That(yokai.Flinching, Is.False, "A pop is not a combo flinch.");

            for (; elapsed < yokai.BombShowSeconds - .06f; elapsed += .05f)
            {
                yokai.Advance(calm, 40d + elapsed, .05f);
                Assert.That(yokai.Holding, Is.True, "Out while the \"!\" bubble is up.");
            }
            Step(calm, 40d + elapsed, .2f);
            Assert.That(yokai.Holding, Is.False, "When the bubble goes, so does the yokai.");
        }

        [Test]
        public void ABombDuringAComboOnlyPopsAndBothMustEndBeforeItGoesIn()
        {
            SetClip(2.4f);
            var combo = Active(25d, 1);
            Step(combo, 25d, 1f);
            Assert.That(yokai.Holding, Is.True);
            yokai.BombDropped();
            yokai.Advance(combo, 26d, .05f);
            Assert.That(yokai.Popping, Is.True, "Already out, so it pops straight away.");
            Assert.That(root.localPosition.z, Is.GreaterThan(Up - .0004f));

            var ended = Calm(26.1d);
            ended.comboCount = 1;
            ended.comboResult = LabComboResult.Timeout;
            ended.comboResultCount = 1;
            yokai.Advance(ended, 26.1d, .05f);
            Assert.That(yokai.Holding, Is.True, "The combo is over but the bomb bubble is still up.");
            Step(ended, 26.1d, yokai.BombShowSeconds);
            Assert.That(yokai.Holding, Is.False, "Both over: back in.");
        }

        [Test]
        public void TheNetworksGourdBombSignalReachesTheYokaiAndPausesHoldIt()
        {
            var host = new GameObject("Yokai bomb network fixture");
            try
            {
                var network = host.AddComponent<LabNetwork>();
                network.enabled = false;
                yokai.Bind(network);
                var field = typeof(LabNetwork).GetField("GourdBombDropped",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null);
                var signal = (System.Action<ulong, string, Vector2>)field.GetValue(network);
                Assert.That(signal, Is.Not.Null, "Bind listens for the gourd's bombs.");
                signal(2ul, "gourd-bomb-1", new Vector2(.5f, .5f));
                Assert.That(yokai.BombSecondsLeft, Is.EqualTo(yokai.BombShowSeconds));

                var paused = Calm(40d);
                paused.paused = true;
                yokai.Advance(paused, 40d, 0f);
                Assert.That(yokai.Holding, Is.True);
                yokai.Advance(paused, 40d, 0f);
                Assert.That(yokai.BombSecondsLeft, Is.EqualTo(yokai.BombShowSeconds), "No time passes in a pause.");

                yokai.Advance(new LabSnapshot { sessionId = "yokai", round = 1, phase = LabPhase.Defeat, serverTime = 41d },
                    41d, .05f);
                Assert.That(yokai.Holding, Is.False, "Only during the battle.");
                Assert.That(yokai.BombSecondsLeft, Is.Zero);

                yokai.Bind(null);
                Assert.That(field.GetValue(network), Is.Null, "Rebinding stops listening to the old network.");
            }
            finally
            {
                Object.Destroy(host);
            }
        }

        private static LabSnapshot Calm(double now) => new LabSnapshot
        {
            sessionId = "yokai", round = 1, phase = LabPhase.Playing, serverTime = now
        };

        private void Step(LabSnapshot snapshot, double from, float seconds)
        {
            for (float done = 0f; done < seconds - 1e-4f; done += .05f)
                yokai.Advance(snapshot, from + done, .05f);
        }

        private void SetClip(float time)
        {
            AnimationState state = clipPlayer[clipPlayer.clip.name];
            state.enabled = true;
            state.weight = 1f;
            state.speed = 0f;
            state.time = time;
            clipPlayer.Sample();
        }

        private float Lid() => Mathf.DeltaAngle(0f, lid.localEulerAngles.x);
        private float Eye() => Mathf.DeltaAngle(0f, eye.localEulerAngles.z);

        private static LabSnapshot Waiting(double now) => new LabSnapshot
        {
            sessionId = "yokai", round = 1, phase = LabPhase.Playing, serverTime = now,
            comboWarningAt = 17d, comboNextAt = 20d
        };

        private static LabSnapshot Active(double now, int progress) => new LabSnapshot
        {
            sessionId = "yokai", round = 1, phase = LabPhase.Playing, serverTime = now,
            comboCount = 1, comboActive = true, comboProgress = progress,
            comboColors = new[] { LabOrbElement.Fire, LabOrbElement.Water, LabOrbElement.Wood, LabOrbElement.Earth },
            comboStartedAt = 20d, comboEndsAt = 30d
        };
    }
}
