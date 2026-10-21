using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>
    /// 박 속 요괴 (#244): 콤보 예고부터 콤보가 끝날 때까지 요괴가 나와 있고, 칸을 맞히면 움찔하며,
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
        public void ANewComboSnapshotThatAlreadyContainsAHitFlinchesWithoutReplayingIt()
        {
            SetClip(2.4f);
            // A client can receive progress 1 before it ever receives progress 0.
            var firstHit = Active(20.1d, 1);
            yokai.Advance(firstHit, 20.1d, .05f);
            Assert.That(yokai.Holding, Is.True);
            Assert.That(yokai.Flinching, Is.True, "The first received active snapshot includes an accepted hit.");
            Step(firstHit, 20.1d, LabGourdYokai.FlinchSeconds);
            Assert.That(yokai.Flinching, Is.False);
            yokai.Advance(firstHit, 20.5d, .05f);
            Assert.That(yokai.Flinching, Is.False, "Receiving the same progress never replays a hit.");

            // A later combo can skip progress 0 in the same way, after its warning.
            var warning = Waiting(32d);
            warning.comboCount = 1;
            warning.comboProgress = 1;
            warning.comboWarningAt = 32d;
            warning.comboNextAt = 35d;
            yokai.Advance(warning, 32d, 0f);
            var nextHit = Active(35.1d, 1);
            nextHit.comboCount = 2;
            yokai.Advance(nextHit, 35.1d, .05f);
            Assert.That(yokai.Flinching, Is.True, "A new combo has a fresh zero-progress baseline.");
        }

        [Test]
        public void AnOldCompletedComboDoesNotInventAHitWhenFirstObservedAfterItEnded()
        {
            SetClip(2.4f);
            var completed = Success(23d);
            yokai.Advance(completed, 23d, .05f);
            Assert.That(yokai.Holding, Is.False);
            Assert.That(yokai.Flinching, Is.False, "There was no live combo window or held warning to react to.");
            yokai.Advance(completed, 23.1d, .05f);
            Assert.That(yokai.Flinching, Is.False);
        }

        [Test]
        public void PausingTheFinalHitFlinchKeepsItsPoseAndResumingRestoresTheAnimationPose()
        {
            SetClip(2.4f);
            yokai.Advance(Active(22.9d, 3), 22.9d, 0f);
            Step(Active(22.9d, 3), 22.9d, LabGourdYokai.FlinchSeconds);
            Vector3 normalScale = root.localScale;
            var completed = Success(23d);
            yokai.Advance(completed, 23d, .05f);
            Assert.That(yokai.Holding, Is.False, "The last hit releases the ordinary animation.");
            Assert.That(yokai.Flinching, Is.True);
            Vector3 pausedPosition = root.localPosition, pausedScale = root.localScale;
            float pausedTime = yokai.ClipTime;
            completed.paused = true;
            for (int frame = 0; frame < 120; frame++) yokai.Advance(completed, 23d, 0f);
            Assert.That(root.localPosition, Is.EqualTo(pausedPosition), "A frozen frame must not add another hop.");
            Assert.That(root.localScale, Is.EqualTo(pausedScale));
            Assert.That(yokai.ClipTime, Is.EqualTo(pausedTime));
            Assert.That(yokai.Flinching, Is.True, "Pause does not consume flinch time.");

            completed.paused = false;
            Step(completed, 23d, LabGourdYokai.FlinchSeconds);
            Assert.That(yokai.Flinching, Is.False);
            Assert.That(root.localScale, Is.EqualTo(normalScale));
            Vector3 restoredPosition = root.localPosition;
            clipPlayer.Sample();
            Assert.That(root.localPosition, Is.EqualTo(restoredPosition), "The completed flinch leaves no hop offset.");
            Assert.That(clipPlayer[clipPlayer.clip.name].speed, Is.EqualTo(1f));
        }

        [Test]
        public void DisablingDuringAFinalHitFlinchClearsItsOffsetAndScale()
        {
            SetClip(2.4f);
            yokai.Advance(Active(22.9d, 3), 22.9d, 0f);
            Step(Active(22.9d, 3), 22.9d, LabGourdYokai.FlinchSeconds);
            Vector3 normalScale = root.localScale;
            // The setup disables LateUpdate to drive time; enable it only within this synchronous test.
            yokai.enabled = true;
            yokai.Advance(Success(23d), 23d, .05f);
            Assert.That(yokai.Flinching, Is.True);
            yokai.enabled = false;
            Assert.That(yokai.Flinching, Is.False);
            Assert.That(root.localScale, Is.EqualTo(normalScale));
            Vector3 restoredPosition = root.localPosition;
            clipPlayer.Sample();
            Assert.That(root.localPosition, Is.EqualTo(restoredPosition));
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

        private static LabSnapshot Success(double now) => new LabSnapshot
        {
            sessionId = "yokai", round = 1, phase = LabPhase.Playing, serverTime = now,
            comboCount = 1, comboProgress = LabGourdCombo.Length,
            comboResult = LabComboResult.Success, comboResultCount = 1,
            comboWarningAt = 32d, comboNextAt = 35d
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
