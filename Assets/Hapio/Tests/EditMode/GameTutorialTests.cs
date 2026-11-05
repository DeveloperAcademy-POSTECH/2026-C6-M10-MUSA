using System.Linq;
using C6Lab;
using NUnit.Framework;
using UnityEngine;

namespace Hapio.Tests
{
    /// <summary>튜토리얼 (#477) 0단계: 3자리 연습 판, 설정, 대본, 그림.</summary>
    public sealed class GameTutorialTests
    {
        [Test]
        public void GoalStepPlaysTheApprovedLinesWithGeumokAndBul()
        {
            GameTutorialStep goal = GameTutorialScript.Steps[0];
            Assert.That(goal, Is.SameAs(GameTutorialScript.Goal));
            string[] words = goal.Lines.Select(line => line.Text).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                "잠깐! 무작정 가면 안 돼.",
                "사용법부터 익히고 가자.",
                "에이~ 그냥 던지면 되는 거 아냐?",
                "그러다 또 사고 칠래?",
                "마침 박 속에 작은 요괴가 남아 있네.\n얘로 연습하자!",
                "봉인박을 오행의 기운으로\n빈틈없이 칠하면 요괴를 가둘 수 있어."
            }, words);
            CollectionAssert.AreEqual(new[]
            {
                GameTutorialSpeaker.Geumok, GameTutorialSpeaker.Geumok, GameTutorialSpeaker.Bul,
                GameTutorialSpeaker.Geumok, GameTutorialSpeaker.Geumok, GameTutorialSpeaker.Geumok
            }, goal.Lines.Select(line => line.Speaker).ToArray());
            // Only the goal line darkens the screen around the gourd.
            CollectionAssert.AreEqual(new[] { false, false, false, false, false, true },
                goal.Lines.Select(line => line.Focus == GameTutorialFocus.Gourd).ToArray());
            Assert.That(goal.Beats.All(beat => beat.IsLine), Is.True, "The goal step only talks.");
            foreach (GameTutorialStep step in GameTutorialScript.Steps)
                foreach (GameTutorialBeat line in step.Lines)
                    Assert.That(line.Text.Split('\n').Length, Is.LessThanOrEqualTo(2), "One bubble holds two lines at most.");
            Assert.That(GameTutorialScript.StageLabel, Is.EqualTo("튜토리얼"));
        }

        [Test]
        public void DrumStepTapsThreeTimesThenShowsTheGaugeRunOutAndRefill()
        {
            GameTutorialStep drum = GameTutorialScript.Steps[1];
            Assert.That(drum, Is.SameAs(GameTutorialScript.Drum));
            GameTutorialBeat[] beats = drum.Beats.ToArray();
            CollectionAssert.AreEqual(new[]
            {
                GameTutorialAction.None, GameTutorialAction.None, GameTutorialAction.TapDrum, GameTutorialAction.None,
                GameTutorialAction.None, GameTutorialAction.None, GameTutorialAction.RefillStamina
            }, beats.Select(beat => beat.Action).ToArray());
            CollectionAssert.AreEqual(new[]
            {
                "먼저 북을 쳐 봐.", "음구슬이나 양구슬이 튀어나올 거야.", "오, 나온다 나와!",
                "북을 칠 때마다 기운이 줄어.", "바닥나면 잠깐 숨을 골라야 해."
            }, drum.Lines.Select(line => line.Text).ToArray());
            CollectionAssert.AreEqual(new[]
            {
                GameTutorialSpeaker.Geumok, GameTutorialSpeaker.Geumok, GameTutorialSpeaker.Bul,
                GameTutorialSpeaker.Geumok, GameTutorialSpeaker.Geumok
            }, drum.Lines.Select(line => line.Speaker).ToArray());
            // Lit: the drum, the drum, (the tap), nothing, the gauge, the gauge (with the short cue), the refill.
            CollectionAssert.AreEqual(new[]
            {
                GameTutorialFocus.Drum, GameTutorialFocus.Drum, GameTutorialFocus.Drum, GameTutorialFocus.None,
                GameTutorialFocus.Stamina, GameTutorialFocus.Stamina, GameTutorialFocus.Stamina
            }, beats.Select(beat => beat.Focus).ToArray());
            Assert.That(beats[5].Cue, Is.EqualTo(GameTutorialCue.StaminaShort));
            Assert.That(beats.Count(beat => beat.Cue != GameTutorialCue.None), Is.EqualTo(1));
            Assert.That(beats[2].Count, Is.EqualTo(GameStageSettings.TutorialDrumTaps).And.EqualTo(3));
            Assert.That(GameTutorialDirector.BannerText(beats[2], 0), Is.EqualTo("북을 눌러 구슬을 만드시오 (0/3)"));
            Assert.That(GameTutorialDirector.BannerText(beats[2], 2), Is.EqualTo("북을 눌러 구슬을 만드시오 (2/3)"));
            Assert.That(beats[6].Text, Is.Empty, "The refill has no banner.");
            // Three taps: yin, yang, yin, so step 2 combines one pair and keeps one yin.
            CollectionAssert.AreEqual(new[] { LabOrbKind.Yin, LabOrbKind.Yang, LabOrbKind.Yin }, GameTutorialScript.DrumResults);
        }

        [Test]
        public void CombineStepDragsOneYinOntoTheYangAndThenShowsTheRedOrbPlainly()
        {
            GameTutorialStep combine = GameTutorialScript.Steps[2];
            Assert.That(combine, Is.SameAs(GameTutorialScript.Combine));
            GameTutorialBeat[] beats = combine.Beats.ToArray();
            CollectionAssert.AreEqual(new[]
            {
                "검은 음구슬과 하얀 양구슬을\n맞붙여 봐.", "검은 거 두 개는 안 돼?", "같은 색끼리는 안 붙어!",
                "우와, 색이 변했어!", "그게 오행구슬이야.", "화·수·목·금·토 중 하나가 나와."
            }, combine.Lines.Select(line => line.Text).ToArray());
            CollectionAssert.AreEqual(new[]
            {
                GameTutorialSpeaker.Geumok, GameTutorialSpeaker.Bul, GameTutorialSpeaker.Geumok,
                GameTutorialSpeaker.Bul, GameTutorialSpeaker.Geumok, GameTutorialSpeaker.Geumok
            }, combine.Lines.Select(line => line.Speaker).ToArray());
            Assert.That(beats[3].Action, Is.EqualTo(GameTutorialAction.CombineOrbs));
            Assert.That(beats.Count(beat => !beat.IsLine), Is.EqualTo(1));
            CollectionAssert.AreEqual(new[]
            {
                GameTutorialFocus.Pair, GameTutorialFocus.Yins, GameTutorialFocus.Yins, GameTutorialFocus.Pair,
                GameTutorialFocus.None, GameTutorialFocus.None, GameTutorialFocus.None
            }, beats.Select(beat => beat.Focus).ToArray());
            Assert.That(GameTutorialDirector.BannerText(beats[3], 0), Is.EqualTo("음구슬을 끌어 양구슬 위에 놓으시오"),
                "One combination needs no counter.");
            Assert.That(GameTutorialScript.CombineResult, Is.EqualTo(LabOrbElement.Fire));
        }

        [Test]
        public void ScriptedCombinationIsAlwaysFireAndKeepsTheOtherYin()
        {
            using (var settings = new GameStageSettings(Resources.Load<LabConfig>("LabConfig"), GameStageCatalog.RequireDefault()))
            {
                LabConfig tutorial = settings.CreateTutorialConfig();
                try
                {
                    const ulong me = 0UL;
                    ulong[] seats = { LabNetwork.TutorialRightSeatId, me, LabNetwork.TutorialLeftSeatId };
                    for (int round = 0; round < 8; round++)
                    {
                        var model = new LabModel();
                        double now = 10d;
                        model.Begin("tutorial-combine-" + round, seats, now, tutorial, LabStageYokai.Gumiho);
                        model.TutorialHandDrumsTo(me);
                        model.TutorialScriptGenerations(me, LabOrbKind.Yin, LabOrbKind.Yang, LabOrbKind.Yin);
                        var made = new System.Collections.Generic.List<LabOrbState>();
                        for (int tap = 0; tap < 3; tap++)
                        {
                            now += .5d;
                            Assert.That(model.TryGenerate(me, "tap-" + tap, now, out LabOrbState orb, out string why), Is.True, why);
                            made.Add(orb);
                        }
                        Assert.That(model.TutorialScriptCombinations(me, GameTutorialScript.CombineResult), Is.True);
                        now += .5d;
                        Assert.That(model.TryCombineAt(me, made[0].id, made[1].id, "combine", now, new Vector2(.5f, .5f),
                            Vector2.zero, out LabOrbState fire, out string reason), Is.True, reason);
                        Assert.That(fire.kind, Is.EqualTo(LabOrbKind.Combined));
                        Assert.That(fire.element, Is.EqualTo(LabOrbElement.Fire), "Round " + round);
                        Assert.That(model.TutorialScriptedCombinationsLeft, Is.EqualTo(0));
                        LabSnapshot snapshot = model.Snapshot(now);
                        Assert.That(snapshot.orbs.Count(orb => orb.owner == me && orb.kind == LabOrbKind.Yin), Is.EqualTo(1));
                        Assert.That(snapshot.players.Single(player => player.id == me).combinedTotal, Is.EqualTo(1));
                    }
                    var other = new LabModel();
                    other.Begin("tutorial-combine-other", seats, 1d, tutorial, LabStageYokai.Gumiho);
                    Assert.That(other.TutorialScriptCombinations(me, LabOrbElement.None), Is.False, "Only the five elements.");
                    Assert.That(other.TutorialScriptCombinations(99UL, LabOrbElement.Fire), Is.False, "Only a seated player.");
                }
                finally { Object.DestroyImmediate(tutorial); }
            }
        }

        [Test]
        public void DrumOrbsLeaveOnFixedLaunchesToFixedRestingPlaces()
        {
            // speed² = 2 × slowing × distance: the floor stops each orb exactly that far away.
            Vector2 launch = GameTutorialScript.LaunchVelocity(0f, .5f, 2f);
            Assert.That(launch.x, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(launch.y, Is.EqualTo(Mathf.Sqrt(2f)).Within(1e-5f));
            Assert.That(GameTutorialScript.LaunchVelocity(-90f, .25f, 2f).x, Is.EqualTo(-1f).Within(1e-5f));
            Assert.That(GameTutorialScript.DrumLaunches.Count, Is.EqualTo(GameTutorialScript.DrumResults.Count));
            // Where each rests from the drum (board widths), as the user's layout: first yin left of the drum, the yang
            // right of it at the same height, the other yin just up-left of the yang.
            Vector2[] rest = GameTutorialScript.DrumLaunches.Select(item => new Vector2(
                Mathf.Sin(item.angleFromUp * Mathf.Deg2Rad), Mathf.Cos(item.angleFromUp * Mathf.Deg2Rad)) * item.distance).ToArray();
            Assert.That(rest[0].x, Is.EqualTo(-.354f).Within(.005f)); Assert.That(rest[0].y, Is.EqualTo(.068f).Within(.005f));
            Assert.That(rest[1].x, Is.EqualTo(.25f).Within(.005f)); Assert.That(rest[1].y, Is.EqualTo(.068f).Within(.005f));
            Assert.That(rest[2].x, Is.EqualTo(.155f).Within(.005f)); Assert.That(rest[2].y, Is.EqualTo(.195f).Within(.005f));
            foreach (Vector2 place in rest) Assert.That(Mathf.Abs(place.x), Is.LessThan(.4f), "Stays well inside the sides.");
            // 2단계 pairs the yang with its nearest yin: the one beside it; the left yin waits for 4단계.
            Assert.That((rest[2] - rest[1]).magnitude, Is.LessThan((rest[0] - rest[1]).magnitude * .5f));

            using (var settings = new GameStageSettings(Resources.Load<LabConfig>("LabConfig"), GameStageCatalog.RequireDefault()))
            {
                LabConfig tutorial = settings.CreateTutorialConfig();
                try
                {
                    const ulong me = 0UL;
                    var model = new LabModel();
                    double now = 10d;
                    model.Begin("tutorial-launch", new[] { LabNetwork.TutorialRightSeatId, me, LabNetwork.TutorialLeftSeatId },
                        now, tutorial, LabStageYokai.Gumiho);
                    model.TutorialHandDrumsTo(me);
                    Vector2[] launches = GameTutorialScript.DrumLaunches.Select(item =>
                        GameTutorialScript.LaunchVelocity(item.angleFromUp, item.distance, tutorial.OrbFloorDeceleration)).ToArray();
                    Assert.That(model.TutorialScriptGenerations(me, GameTutorialScript.DrumResults, launches), Is.True);
                    for (int tap = 0; tap < launches.Length; tap++)
                    {
                        now += .5d;
                        Assert.That(model.TryGenerate(me, "tap-" + tap, now, out LabOrbState orb, out string why), Is.True, why);
                        Assert.That(orb.kind, Is.EqualTo(GameTutorialScript.DrumResults[tap]));
                        Assert.That(new Vector2(orb.vx, orb.vy), Is.EqualTo(launches[tap]), "Launch " + tap);
                        Assert.That(new Vector2(orb.x, orb.y), Is.EqualTo(new Vector2(.5f, .5f)), "From the drum's center.");
                    }
                    Assert.That(model.TutorialScriptGenerations(me, GameTutorialScript.DrumResults, new[] { Vector2.zero }), Is.False,
                        "One launch per orb.");
                }
                finally { Object.DestroyImmediate(tutorial); }
            }
        }

        [Test]
        public void BlackOrbsLightIsACapsuleTiltedAlongThem()
        {
            GameTutorialDirector.CapsuleBetween(new Vector2(0f, 0f), new Vector2(10f, 10f), 2f, out Rect level, out float degrees);
            Assert.That(level.center.x, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(level.center.y, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(level.width, Is.EqualTo(10f * Mathf.Sqrt(2f) + 4f).Within(1e-4f));
            Assert.That(level.height, Is.EqualTo(4f).Within(1e-4f));
            Assert.That(degrees, Is.EqualTo(45f).Within(1e-3f));
            GameTutorialDirector.CapsuleBetween(new Vector2(10f, 0f), new Vector2(0f, 10f), 2f, out _, out degrees);
            Assert.That(degrees, Is.EqualTo(-45f).Within(1e-3f), "Never more than a quarter turn.");
            GameTutorialDirector.CapsuleBetween(new Vector2(3f, 3f), new Vector2(3f, 3f), 2f, out level, out degrees);
            Assert.That((level.width, level.height, degrees), Is.EqualTo((4f, 4f, 0f)), "One orb: a circle.");
            // Turning the outline keeps it the same shape about its center.
            var points = new System.Collections.Generic.List<Vector2>();
            var normals = new System.Collections.Generic.List<Vector2>();
            var hole = new Rect(-12f, -2f, 24f, 4f);
            GameTutorialSpotlight.Outline(hole, 2f, points, normals);
            var before = points.Select(point => (point - hole.center).magnitude).ToArray();
            GameTutorialSpotlight.Rotate(points, normals, hole.center, 30f);
            for (int i = 0; i < points.Count; i++)
                Assert.That((points[i] - hole.center).magnitude, Is.EqualTo(before[i]).Within(1e-4f));
            Assert.That(normals.All(normal => Mathf.Abs(normal.magnitude - 1f) < 1e-4f), Is.True);
        }

        [Test]
        public void DrumOrbsSlideOnlyWhatIsLeftAfterLeavingTheDrumsEdge()
        {
            // A new orb starts just outside the drum (drum radius + orb radius), so its slide is shorter by that much.
            float clearance = GameTutorialScript.DrumClearance(.6f, .32f, 4.6f);
            Assert.That(clearance, Is.EqualTo(.2f).Within(1e-5f));
            Assert.That(GameTutorialScript.SlideAfterDrum(.36f, clearance), Is.EqualTo(.16f).Within(1e-5f));
            Assert.That(GameTutorialScript.SlideAfterDrum(.1f, clearance), Is.GreaterThan(0f), "Always a small push out.");
            Assert.That(GameTutorialScript.DrumClearance(.6f, .32f, 0f), Is.EqualTo(0f), "No board yet: no clearance.");
            // With a clearance like the saved layout's (about 0.16 of the board) every orb slides a little further for a
            // livelier burst, yet the left yin stops at least 0.1 of the board inside the left edge.
            const float layoutClearance = .164f, extra = .06f, margin = .1f;
            foreach ((float angle, float distance) in GameTutorialScript.DrumLaunches)
            {
                float plain = GameTutorialScript.SlideAfterDrum(distance, layoutClearance);
                float slide = GameTutorialScript.SlideAfterDrum(distance, layoutClearance, extra, angle, margin);
                Assert.That(slide, Is.GreaterThan(plain), "Further than the plain slide at " + angle);
                Assert.That(slide, Is.LessThanOrEqualTo(plain + extra + 1e-5f));
                float restX = .5f + (layoutClearance + slide) * Mathf.Sin(angle * Mathf.Deg2Rad);
                Assert.That(restX, Is.InRange(margin - 1e-4f, 1f - margin + 1e-4f), "Inside the sides at " + angle);
            }
            GameTutorialArt art = GameTutorialArt.Load();
            Assert.That(art.drumLaunchExtra, Is.EqualTo(.06f));
            Assert.That(art.drumLaunchEdgeMargin, Is.EqualTo(.1f));
        }

        [Test]
        public void PairLightIsTheSmallestCircleAroundBothOrbs()
        {
            GameTutorialDirector.PairCircle(new Vector2(0f, 0f), 1f, new Vector2(4f, 0f), 1f, out Vector2 center, out float radius);
            Assert.That(center, Is.EqualTo(new Vector2(2f, 0f)));
            Assert.That(radius, Is.EqualTo(3f));
            GameTutorialDirector.PairCircle(new Vector2(0f, 0f), 1f, new Vector2(0f, 3f), 2f, out center, out radius);
            Assert.That(radius, Is.EqualTo(3f));
            // From the small one's far side (y -1) to the big one's (y 5): the middle is y 2, past the halfway point 1.5.
            Assert.That(center.y, Is.EqualTo(2f).Within(1e-5f), "Shifted toward the bigger one.");
            GameTutorialDirector.PairCircle(new Vector2(0f, 0f), 5f, new Vector2(1f, 0f), 1f, out center, out radius);
            Assert.That((center, radius), Is.EqualTo((new Vector2(0f, 0f), 5f)), "One inside the other.");
        }

        [Test]
        public void DragGuidePressesTravelsLetsGoAndFades()
        {
            GameTutorialDirector.HandDrag(0f, out float travel, out float press, out float alpha);
            Assert.That((travel, press, alpha), Is.EqualTo((0f, 0f, 0f)));
            GameTutorialDirector.HandDrag(.12f, out travel, out press, out alpha);
            Assert.That(travel, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(press, Is.EqualTo(1f).Within(1e-4f));
            GameTutorialDirector.HandDrag(.42f, out travel, out press, out _);
            Assert.That(travel, Is.EqualTo(.5f).Within(1e-4f), "Halfway along the arrow.");
            Assert.That(press, Is.EqualTo(1f));
            GameTutorialDirector.HandDrag(.84f, out travel, out press, out alpha);
            Assert.That(travel, Is.EqualTo(1f));
            Assert.That(press, Is.EqualTo(0f).Within(1e-4f), "Let go on the yang.");
            Assert.That(alpha, Is.EqualTo(1f).Within(1e-4f));
            GameTutorialDirector.HandDrag(.99f, out _, out _, out alpha);
            Assert.That(alpha, Is.LessThan(.1f), "Fades before the next pass.");
        }

        [Test]
        public void ThrowStepFlicksTheRedOrbAtTheGourdThenLightsThePaintBar()
        {
            GameTutorialStep throwing = GameTutorialScript.Steps[3];
            Assert.That(throwing, Is.SameAs(GameTutorialScript.Throw));
            GameTutorialBeat[] beats = throwing.Beats.ToArray();
            CollectionAssert.AreEqual(new[]
            {
                GameTutorialAction.None, GameTutorialAction.ThrowOrb, GameTutorialAction.None, GameTutorialAction.None
            }, beats.Select(beat => beat.Action).ToArray());
            CollectionAssert.AreEqual(new[]
            {
                "이제 오행구슬을 위로 휙!\n박을 향해 던져 봐.", "맞았다! 박에 색이 묻었어!", "박이 100% 칠해질 때까지\n계속 칠하면 돼!"
            }, throwing.Lines.Select(line => line.Text).ToArray());
            CollectionAssert.AreEqual(new[] { GameTutorialSpeaker.Geumok, GameTutorialSpeaker.Bul, GameTutorialSpeaker.Geumok },
                throwing.Lines.Select(line => line.Speaker).ToArray());
            CollectionAssert.AreEqual(new[]
            {
                GameTutorialFocus.None, GameTutorialFocus.None, GameTutorialFocus.None, GameTutorialFocus.Progress
            }, beats.Select(beat => beat.Focus).ToArray(), "The gourd stays in sight while throwing.");
            Assert.That(GameTutorialDirector.BannerText(beats[1], 0), Is.EqualTo("빨간 구슬을 위로 밀어 박에 던지시오"));
            Assert.That(GameTutorialScript.RetryLine, Is.EqualTo("다시 해 봐!"));
            Assert.That(GameTutorialScript.RetrySpeaker, Is.EqualTo(GameTutorialSpeaker.Geumok));
            Vector2 landing = GameTutorialScript.RetryLanding;
            Assert.That(landing.x, Is.InRange(.6f, .9f), "Right, clear of the drum and the left yin.");
            Assert.That(landing.y, Is.InRange(.5f, .9f));
        }

        [Test]
        public void MissedRedOrbIsGivenBackAtItsLanding()
        {
            using (var settings = new GameStageSettings(Resources.Load<LabConfig>("LabConfig"), GameStageCatalog.RequireDefault()))
            {
                LabConfig tutorial = settings.CreateTutorialConfig();
                try
                {
                    const ulong me = 0UL;
                    var model = new LabModel();
                    double now = 10d;
                    model.Begin("tutorial-drop", new[] { LabNetwork.TutorialRightSeatId, me, LabNetwork.TutorialLeftSeatId },
                        now, tutorial, LabStageYokai.Gumiho);
                    float stamina = model.Snapshot(now).players.Single(player => player.id == me).stamina;
                    now += 1d;
                    Assert.That(model.TutorialDropOrb(me, LabOrbKind.Combined, LabOrbElement.Fire, GameTutorialScript.RetryLanding,
                        now, out LabOrbState fire), Is.True);
                    Assert.That((fire.kind, fire.element, fire.owner), Is.EqualTo((LabOrbKind.Combined, LabOrbElement.Fire, me)));
                    Assert.That(new Vector2(fire.x, fire.y), Is.EqualTo(GameTutorialScript.RetryLanding));
                    Assert.That(fire.kind.CanThrow(fire.lockSwipes), Is.True, "It can be thrown again.");
                    LabSnapshot snapshot = model.Snapshot(now);
                    Assert.That(snapshot.orbs.Any(orb => orb.id == fire.id), Is.True);
                    Assert.That(snapshot.players.Single(player => player.id == me).stamina, Is.EqualTo(stamina).Within(1e-4f),
                        "No stamina is spent.");
                    Assert.That(model.TutorialDropOrb(me, LabOrbKind.Combined, LabOrbElement.None, new Vector2(.5f, .5f), now, out _), Is.False);
                    Assert.That(model.TutorialDropOrb(me, LabOrbKind.Drum, LabOrbElement.None, new Vector2(.5f, .5f), now, out _), Is.False);
                    Assert.That(model.TutorialDropOrb(me, LabOrbKind.Combined, LabOrbElement.Fire, new Vector2(1.5f, .5f), now, out _), Is.False);
                    Assert.That(model.TutorialDropOrb(99UL, LabOrbKind.Combined, LabOrbElement.Fire, new Vector2(.5f, .5f), now, out _), Is.False);
                }
                finally { Object.DestroyImmediate(tutorial); }
            }
        }

        [Test]
        public void FifthMissTeasesOnceThenItIsTryAgainAsEver()
        {
            // Easter egg: 금옥이 says "다시 해 봐!" after every miss, except the fifth, which opens with the tease.
            string[] firstWords = Enumerable.Range(1, 9).Select(GameTutorialScript.MissLine).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                GameTutorialScript.RetryLine, GameTutorialScript.RetryLine, GameTutorialScript.RetryLine,
                GameTutorialScript.RetryLine, GameTutorialScript.TeaseLine, GameTutorialScript.RetryLine,
                GameTutorialScript.RetryLine, GameTutorialScript.RetryLine, GameTutorialScript.RetryLine
            }, firstWords);
            Assert.That(GameTutorialScript.TeaseMisses, Is.EqualTo(5));
            Assert.That(GameTutorialScript.TeaseLine, Is.EqualTo("......\n너 일부러 그러는 거지?"));
            Assert.That(GameTutorialScript.TeaseLine.Split('\n').Length, Is.LessThanOrEqualTo(2), "One bubble, two lines at most.");
            var art = ScriptableObject.CreateInstance<GameTutorialArt>();
            try
            {
                Assert.That(art.teaseSeconds, Is.GreaterThan(art.remarkSeconds), "The pause before the punch line needs its time.");
            }
            finally { Object.DestroyImmediate(art); }
        }

        [Test]
        public void PaintBarLightEndsAtTheDrawnPercentNotItsWideBox()
        {
            var box = new Rect(100f, 0f, 200f, 30f);
            Rect left = GameTutorialDirector.DrawnText(box, 100f, 25f, TextAnchor.MiddleLeft);
            Assert.That((left.xMin, left.xMax), Is.EqualTo((100f, 150f)), "A quarter of the box from its left.");
            Rect right = GameTutorialDirector.DrawnText(box, 100f, 25f, TextAnchor.MiddleRight);
            Assert.That((right.xMin, right.xMax), Is.EqualTo((250f, 300f)));
            Rect middle = GameTutorialDirector.DrawnText(box, 100f, 25f, TextAnchor.MiddleCenter);
            Assert.That((middle.xMin, middle.xMax), Is.EqualTo((175f, 225f)));
            Assert.That(GameTutorialDirector.DrawnText(box, 100f, 400f, TextAnchor.MiddleLeft).width, Is.EqualTo(200f),
                "Never wider than the box.");
        }

        [Test]
        public void ThrowGuideFlicksUpQuicklyAndRests()
        {
            GameTutorialDirector.HandSwipe(0f, out float travel, out float press, out float alpha);
            Assert.That((travel, press, alpha), Is.EqualTo((0f, 0f, 0f)));
            GameTutorialDirector.HandSwipe(.12f, out travel, out press, out _);
            Assert.That(travel, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(press, Is.EqualTo(1f).Within(1e-4f));
            GameTutorialDirector.HandSwipe(.26f, out float half, out _, out _);
            Assert.That(half, Is.LessThan(.5f), "Speeds up like a flick.");
            GameTutorialDirector.HandSwipe(.4f, out travel, out press, out alpha);
            Assert.That(travel, Is.EqualTo(1f));
            Assert.That(press, Is.EqualTo(0f));
            GameTutorialDirector.HandSwipe(.7f, out _, out _, out alpha);
            Assert.That(alpha, Is.EqualTo(0f), "Rests unseen before the next flick.");
        }

        [Test]
        public void PassStepSendsTheDrumRightToGeumokWhoGivesItBackWithAYang()
        {
            GameTutorialStep pass = GameTutorialScript.Steps[4];
            Assert.That(pass, Is.SameAs(GameTutorialScript.Pass));
            GameTutorialBeat[] beats = pass.Beats.ToArray();
            CollectionAssert.AreEqual(new[]
            {
                GameTutorialAction.None, GameTutorialAction.None, GameTutorialAction.PassDrum, GameTutorialAction.None,
                GameTutorialAction.None
            }, beats.Select(beat => beat.Action).ToArray());
            CollectionAssert.AreEqual(new[]
            {
                "나만 너무 열심히 하고 있는 거 아니야?",
                "맞아! 너가 북을 줘야지... \n북은 몇 개 없어서 같이 써야 해! 옆으로 밀면 넘어가. ",
                "고마워!\n대신 내가 만든 구슬과 북 줄게~", "짝이 안 맞는 구슬은\n이렇게 친구한테 넘기면 돼."
            }, pass.Lines.Select(line => line.Text).ToArray());
            CollectionAssert.AreEqual(new[]
            {
                GameTutorialSpeaker.Bul, GameTutorialSpeaker.Geumok, GameTutorialSpeaker.Geumok, GameTutorialSpeaker.Geumok
            }, pass.Lines.Select(line => line.Speaker).ToArray());
            Assert.That(beats[3].Cue, Is.EqualTo(GameTutorialCue.GiveBack), "\"고마워!\" gives the yang and the drum back.");
            Assert.That(GameTutorialDirector.BannerText(beats[2], 0), Is.EqualTo("북을 오른쪽 끝으로 밀어 금옥이에게 넘기시오"));
            Assert.That(GameTutorialScript.WrongSideLine, Is.EqualTo("이건 금옥이한테 줘."));
            Assert.That(GameTutorialScript.WrongSideSpeaker, Is.EqualTo(GameTutorialSpeaker.Muk));
        }

        [Test]
        public void StoryNeighborsHandTheDrumAndAYangBackOverTheSharedEdge()
        {
            using (var settings = new GameStageSettings(Resources.Load<LabConfig>("LabConfig"), GameStageCatalog.RequireDefault()))
            {
                LabConfig tutorial = settings.CreateTutorialConfig();
                try
                {
                    const ulong me = 0UL;
                    ulong geumok = LabNetwork.TutorialRightSeatId, muk = LabNetwork.TutorialLeftSeatId;
                    var model = new LabModel();
                    double now = 10d;
                    model.Begin("tutorial-pass", new[] { geumok, me, muk }, now, tutorial, LabStageYokai.Gumiho);
                    model.TutorialHandDrumsTo(me);
                    string drum = model.Snapshot(now).drums[0].id;
                    // I flick the drum off my right edge: it goes to 금옥이.
                    now += .5d;
                    Assert.That(model.TryTransferAt(me, drum, true, new Vector2(1f, .5f), new Vector2(1f, 0f), "pass-right", now,
                        LabEdgeCrossingKind.Swipe, out _, out string why), Is.True, why);
                    Assert.That(model.Snapshot(now).drums[0].owner, Is.EqualTo(geumok));
                    // 금옥이 makes a yang and passes it back, then the drum: both enter at my right edge, sliding left.
                    now += .5d;
                    Assert.That(model.TutorialDropOrb(geumok, LabOrbKind.Yang, LabOrbElement.None, new Vector2(.5f, .62f), now,
                        out LabOrbState yang), Is.True);
                    Assert.That(model.TutorialHandOver(geumok, yang.id, me, .62f, 1f, now, out why), Is.True, why);
                    Assert.That(model.TutorialHandOver(geumok, drum, me, .5f, 1f, now, out why), Is.True, why);
                    LabSnapshot back = model.Snapshot(now);
                    LabOrbState gotYang = back.orbs.Single(orb => orb.id == yang.id);
                    Assert.That((gotYang.owner, gotYang.kind, gotYang.x), Is.EqualTo((me, LabOrbKind.Yang, 1f)));
                    Assert.That(gotYang.vx, Is.LessThan(0f), "Slides in from the right.");
                    Assert.That(back.drums[0].owner, Is.EqualTo(me));
                    Assert.That(back.drums[0].x, Is.EqualTo(1f));
                    // The wrong way: 묵이 hands the drum back from the left.
                    now += .5d;
                    Assert.That(model.TryTransferAt(me, drum, false, new Vector2(0f, .5f), new Vector2(-1f, 0f), "pass-left", now,
                        LabEdgeCrossingKind.Swipe, out _, out why), Is.True, why);
                    Assert.That(model.Snapshot(now).drums[0].owner, Is.EqualTo(muk));
                    Assert.That(model.TutorialHandOver(muk, drum, me, .5f, 1f, now, out why), Is.True, why);
                    LabOrbState fromMuk = model.Snapshot(now).drums[0];
                    Assert.That((fromMuk.owner, fromMuk.x), Is.EqualTo((me, 0f)));
                    Assert.That(fromMuk.vx, Is.GreaterThan(0f), "Slides in from the left.");
                    Assert.That(model.TutorialHandOver(me, drum, me, .5f, 1f, now, out _), Is.False, "Not to oneself.");
                }
                finally { Object.DestroyImmediate(tutorial); }
            }
        }

        [Test]
        public void BombStepDropsABombThePlayerPassesRightAndGeumokSendsBack()
        {
            GameTutorialStep bomb = GameTutorialScript.Steps[5];
            Assert.That(bomb, Is.SameAs(GameTutorialScript.Bomb));
            GameTutorialBeat[] beats = bomb.Beats.ToArray();
            CollectionAssert.AreEqual(new[]
            {
                GameTutorialAction.None, GameTutorialAction.None, GameTutorialAction.PassBomb, GameTutorialAction.None,
                GameTutorialAction.WaitBombBack, GameTutorialAction.None, GameTutorialAction.None, GameTutorialAction.None
            }, beats.Select(beat => beat.Action).ToArray());
            CollectionAssert.AreEqual(new[]
            {
                "으악! 박에서 뭐가 떨어졌어!", "요괴가 던진 폭탄이야!\n적힌 숫자만큼 옆으로 넘기면 풀려.",
                "앗 뜨거! 도로 받아!", "풀렸다!", "풀린 폭탄은 오행 구슬 두 개가 돼.",
                "가만히 냅두면 터지니까\n얼른얼른 넘겨야겠네!"
            }, bomb.Lines.Select(line => line.Text).ToArray());
            CollectionAssert.AreEqual(new[]
            {
                GameTutorialSpeaker.Bul, GameTutorialSpeaker.Geumok, GameTutorialSpeaker.Geumok, GameTutorialSpeaker.Geumok,
                GameTutorialSpeaker.Geumok, GameTutorialSpeaker.Bul
            }, bomb.Lines.Select(line => line.Speaker).ToArray());
            Assert.That(beats[0].Cue, Is.EqualTo(GameTutorialCue.DropBomb), "The bomb falls as 불이 cries out.");
            Assert.That(beats[1].Focus, Is.EqualTo(GameTutorialFocus.Bomb), "The bomb is lit while 금옥이 explains it.");
            Assert.That(beats[3].Cue, Is.EqualTo(GameTutorialCue.ReturnBomb), "\"앗 뜨거!\" passes it straight back.");
            Assert.That(GameTutorialDirector.BannerText(beats[2], 0), Is.EqualTo("폭탄을 오른쪽 금옥이에게 넘기시오"));
            Assert.That(string.IsNullOrEmpty(beats[4].Text), Is.True, "Waiting for it to come back shows no banner.");
            Assert.That(beats[5].Cue, Is.EqualTo(GameTutorialCue.OpenBomb), "It opens with \"풀렸다!\", not before.");
            // Two locks: one comes off going to 금옥이, the last coming back, so it opens on my board.
            Assert.That(GameTutorialScript.BombLocks, Is.EqualTo(2));
            var art = ScriptableObject.CreateInstance<GameTutorialArt>();
            try
            {
                Assert.That(art.bombHandDragSeconds, Is.LessThan(art.handDragSeconds), "The bomb's hand pushes faster.");
            }
            finally { Object.DestroyImmediate(art); }
        }

        [Test]
        public void GourdBombLosesOneLockEachWayAndOpensIntoTwoOrbsOnMyBoard()
        {
            using (var settings = new GameStageSettings(Resources.Load<LabConfig>("LabConfig"), GameStageCatalog.RequireDefault()))
            {
                LabConfig tutorial = settings.CreateTutorialConfig();
                try
                {
                    const ulong me = 0UL;
                    ulong geumok = LabNetwork.TutorialRightSeatId, muk = LabNetwork.TutorialLeftSeatId;
                    var model = new LabModel();
                    double now = 10d;
                    model.Begin("tutorial-bomb", new[] { geumok, me, muk }, now, tutorial, LabStageYokai.Gumiho);
                    model.TutorialHandDrumsTo(me);
                    Assert.That(model.TutorialDropGourdBomb(me, GameTutorialScript.BombLanding, GameTutorialScript.BombLocks, now,
                        out LabOrbState dropped), Is.True);
                    Assert.That((dropped.owner, dropped.kind, dropped.lockSwipes, dropped.origin),
                        Is.EqualTo((me, LabOrbKind.Bomb, 2, LabOrbOrigin.Gourd)));
                    LabGourdBomb[] announced = model.DrainGourdBombs();
                    Assert.That(announced.Length, Is.EqualTo(1), "Announced once so the board drops it in from the gourd.");
                    Assert.That((announced[0].BombId, announced[0].Position), Is.EqualTo((dropped.id, GameTutorialScript.BombLanding)));
                    Assert.That(model.DrainGourdBombs(), Is.Empty);

                    // Locked, it cannot be opened in place.
                    Assert.That(model.TryUseItemAt(me, dropped.id, "open-early", now, GameTutorialScript.BombLanding, out _,
                        out string why), Is.False);
                    Assert.That(why, Is.EqualTo("ITEM_LOCKED"));

                    // I push it off my right edge to 금옥이: one lock off.
                    now += .5d;
                    Assert.That(model.TryTransferAt(me, dropped.id, true, new Vector2(1f, .7f), new Vector2(1.5f, 0f), "bomb-right",
                        now, LabEdgeCrossingKind.Swipe, out _, out why), Is.True, why);
                    LabOrbState atGeumok = model.Snapshot(now).orbs.Single(orb => orb.id == dropped.id);
                    Assert.That((atGeumok.owner, atGeumok.lockSwipes), Is.EqualTo((geumok, 1)));

                    // "앗 뜨거! 도로 받아!": the last lock comes off on the way back, and it stops inside my board.
                    now += .5d;
                    Assert.That(model.TutorialHandOver(geumok, dropped.id, me, .5f, 1f, now, out why), Is.True, why);
                    LabOrbState back = model.Snapshot(now).orbs.Single(orb => orb.id == dropped.id);
                    Assert.That((back.owner, back.lockSwipes, back.arrival), Is.EqualTo((me, 0, LabDrumArrival.Stop)));
                    Assert.That(back.x, Is.EqualTo(1f), "Comes in over the edge shared with 금옥이.");
                    Assert.That(back.stopAt, Is.InRange(.2f, .48f), "Rests in the near stretch of my board.");

                    // Resting, my board opens it: two elemental attack orbs.
                    int before = model.Snapshot(now).orbs.Count(orb => orb.owner == me && orb.kind == LabOrbKind.Combined);
                    now += .5d;
                    Assert.That(model.TryUseItemAt(me, dropped.id, "open", now, new Vector2(.7f, .5f), out _, out why), Is.True, why);
                    Assert.That(why, Is.EqualTo("BOMB_OPENED"));
                    LabSnapshot opened = model.Snapshot(now);
                    Assert.That(opened.orbs.Any(orb => orb.kind == LabOrbKind.Bomb), Is.False);
                    LabOrbState[] made = opened.orbs.Where(orb => orb.owner == me && orb.kind == LabOrbKind.Combined).ToArray();
                    Assert.That(made.Length, Is.EqualTo(before + 2));
                    Assert.That(made.All(orb => orb.element != LabOrbElement.None), Is.True, "Each is an elemental attack orb.");
                }
                finally { Object.DestroyImmediate(tutorial); }
            }
        }

        [Test]
        public void BlockStepShutsTheRightEdgeThenTheDrumGoesLeftToMukAndAllFourSayGo()
        {
            Assert.That(GameTutorialScript.Steps.Count, Is.EqualTo(7));
            GameTutorialStep block = GameTutorialScript.Steps[6];
            Assert.That(block, Is.SameAs(GameTutorialScript.Block), "The last step.");
            GameTutorialBeat[] beats = block.Beats.ToArray();
            CollectionAssert.AreEqual(new[]
            {
                GameTutorialAction.WatchBlock, GameTutorialAction.None, GameTutorialAction.None, GameTutorialAction.None,
                GameTutorialAction.None, GameTutorialAction.PassDrumLeft, GameTutorialAction.None, GameTutorialAction.None,
                GameTutorialAction.None, GameTutorialAction.None, GameTutorialAction.None
            }, beats.Select(beat => beat.Action).ToArray());
            CollectionAssert.AreEqual(new[]
            {
                "어? 오른쪽이 빨개졌어!", "요괴가 오른쪽을 막은 것 같아!", "막힌 쪽은 튕겨 나와.\n반대쪽 묵이한테 북을 넘겨!",
                "드디어 내 차례가 왔네\n기다리느라 힘들었어...", "받았어. 고마워.", "좋아, 이 정도면\n잡을 수 있겠어!",
                "앞으로 나올 요괴들이 어떤 방해를 할지 모르니,\n조심하자!", "이번엔 진짜다!\n다 같이 가자!", "가보자!"
            }, block.Lines.Select(line => line.Text).ToArray());
            CollectionAssert.AreEqual(new[]
            {
                GameTutorialSpeaker.Bul, GameTutorialSpeaker.Geumok, GameTutorialSpeaker.Geumok, GameTutorialSpeaker.Muk,
                GameTutorialSpeaker.Muk, GameTutorialSpeaker.Geumok, GameTutorialSpeaker.Geumok, GameTutorialSpeaker.Bul,
                GameTutorialSpeaker.Muk
            }, block.Lines.Select(line => line.Speaker).ToArray());
            Assert.That(string.IsNullOrEmpty(beats[0].Text), Is.True, "The red flashes come first, with no banner.");
            Assert.That(beats.All(beat => beat.Focus == GameTutorialFocus.None), Is.True, "Nothing darkens the red edge.");
            Assert.That(GameTutorialDirector.BannerText(beats[5], 0), Is.EqualTo("북을 왼쪽 끝으로 밀어 묵이에게 넘기시오"));
            Assert.That(GameTutorialScript.BlockedSide, Is.EqualTo(1), "The yokai shuts the right edge, 금옥이's side.");
            var art = ScriptableObject.CreateInstance<GameTutorialArt>();
            try
            {
                Assert.That(art.blockWarningSeconds, Is.EqualTo(LabOrbEdgeBlock.OpeningSeconds), "The first line waits out the flashes.");
            }
            finally { Object.DestroyImmediate(art); }
        }

        [Test]
        public void TutorialSwipeBlockHoldsUntilLiftedAndStopsOnlyMyPassesThatWay()
        {
            using (var settings = new GameStageSettings(Resources.Load<LabConfig>("LabConfig"), GameStageCatalog.RequireDefault()))
            {
                LabConfig tutorial = settings.CreateTutorialConfig();
                try
                {
                    const ulong me = 0UL;
                    ulong geumok = LabNetwork.TutorialRightSeatId, muk = LabNetwork.TutorialLeftSeatId;
                    var model = new LabModel();
                    double now = 10d;
                    model.Begin("tutorial-block", new[] { geumok, me, muk }, now, tutorial, LabStageYokai.Gumiho);
                    model.TutorialHandDrumsTo(me);
                    string drum = model.Snapshot(now).drums[0].id;
                    Assert.That(model.TutorialBlockSwipe(me, GameTutorialScript.BlockedSide, now), Is.True);
                    Assert.That(model.TutorialBlockSwipe(0xDEADUL, 1, now), Is.False, "Only a player at the table.");

                    // An hour later it still holds: it waits for the player, not the clock.
                    now += 3600d;
                    LabSnapshot held = model.Snapshot(now);
                    Assert.That((held.SwipeBlockOn(me), held.swipeBlockedRight), Is.EqualTo((true, true)));
                    Assert.That(held.swipeBlockedUntil - held.serverTime, Is.GreaterThan(3600d));
                    Assert.That(model.SwipeBlocked(me, false, now), Is.False, "The left edge stays open.");

                    Assert.That(model.TryTransferAt(me, drum, true, new Vector2(1f, .5f), new Vector2(1f, 0f), "blocked", now,
                        LabEdgeCrossingKind.Swipe, out _, out string why), Is.False);
                    Assert.That(why, Is.EqualTo("SWIPE_BLOCKED"));
                    now += .5d;
                    Assert.That(model.TryTransferAt(me, drum, false, new Vector2(0f, .5f), new Vector2(-1f, 0f), "to-muk", now,
                        LabEdgeCrossingKind.Swipe, out _, out why), Is.True, why);
                    Assert.That(model.Snapshot(now).drums[0].owner, Is.EqualTo(muk));

                    Assert.That(model.TutorialBlockSwipe(me, 0, now), Is.True);
                    LabSnapshot lifted = model.Snapshot(now);
                    Assert.That((lifted.SwipeBlockOn(me), lifted.swipeBlockedUntil), Is.EqualTo((false, -1d)));
                    Assert.That(model.SwipeBlocked(me, true, now), Is.False);
                }
                finally { Object.DestroyImmediate(tutorial); }
            }
        }

        [Test]
        public void HeldSwipeBlockEdgeGlowsWithoutACountdown()
        {
            var board = new GameObject("Tutorial Block Board");
            try
            {
                LabOrbEdgeBlock edge = LabOrbEdgeBlock.Create(board.transform);
                edge.SecondsShown = false;
                edge.Show(true, 1f, -1f, 1f, 2f, .1f);
                edge.SetSeconds((float)LabModel.TutorialSwipeBlockHoldSeconds);
                Assert.That((edge.Visible, edge.Right), Is.EqualTo((true, true)));
                Assert.That(edge.SecondsText, Is.Empty, "No 86400 on the edge.");
                edge.SecondsShown = true;
                Assert.That(edge.SecondsText, Is.EqualTo("86400"), "The game's own blocks still count down.");
                edge.Hide();
                Assert.That(edge.Visible, Is.False);
            }
            finally { Object.DestroyImmediate(board); }
        }

        [Test]
        public void MirrorPopsLikeABubbleThenTheDrumBrakesEvenlyToRestThenFades()
        {
            const float pop = .28f, glide = 1f, hold = .5f, fade = .15f, overshoot = 1.1f;
            GameTutorialDirector.MirrorWindow(0f, pop, glide, hold, fade, overshoot, out float scale, out float alpha, out float drum);
            Assert.That((scale, alpha, drum), Is.EqualTo((0f, 1f, 0f)), "Pops up from nothing; the drum waits at the edge.");
            GameTutorialDirector.MirrorWindow(pop, pop, glide, hold, fade, overshoot, out scale, out _, out drum);
            Assert.That((scale, drum), Is.EqualTo((1f, 0f)), "Popped to size, then the drum comes in.");
            GameTutorialDirector.MirrorWindow(pop + glide * .5f, pop, glide, hold, fade, overshoot, out _, out _, out drum);
            Assert.That(drum, Is.EqualTo(.75f).Within(1e-4f), "Even braking: three quarters of the way at half the time.");
            GameTutorialDirector.MirrorWindow(pop + glide, pop, glide, hold, fade, overshoot, out _, out alpha, out drum);
            Assert.That((drum, alpha), Is.EqualTo((1f, 1f)), "At rest on its spot, still shown.");
            GameTutorialDirector.MirrorWindow(pop + glide + hold + fade * .5f, pop, glide, hold, fade, overshoot, out _, out alpha, out _);
            Assert.That(alpha, Is.EqualTo(.5f).Within(1e-4f), "Fades after holding.");
            Assert.That(GameTutorialDirector.MirrorTotal(pop, glide, hold, fade), Is.EqualTo(pop + glide + hold + fade).Within(1e-5f));

            // The same arrival as a real board: a gentle pass is sped up to rest within 1.2 s, a hard one capped.
            Assert.That(GameTutorialDirector.MirrorGlideSeconds(.5f, LabOrbBoard.StopArrivalSpeed(.01f, .5f)),
                Is.EqualTo(1.2f).Within(1e-4f));
            float hard = LabOrbBoard.StopArrivalSpeed(50f, .5f);
            Assert.That(hard, Is.EqualTo(Mathf.Sqrt(2f * 8f * .5f)).Within(1e-4f), "Brakes no harder than a real board.");
            Assert.That(LabOrbBoard.StopArrivalSpeed(-3f, .5f), Is.EqualTo(3f).Within(1e-4f), "Either way it was swiped.");
            var art = ScriptableObject.CreateInstance<GameTutorialArt>();
            try { Assert.That(art.mirrorViewMinWidth, Is.GreaterThan(.5f), "The window reaches past the middle, where it rests."); }
            finally { Object.DestroyImmediate(art); }
        }

        [Test]
        public void GourdYokaiTurnsAsFarAsMySeatGoesRoundSoItFacesMe()
        {
            // Seat cameras go round the gourd: the first seat at 0°, then 120° and 240° for three.
            Assert.That(GameTutorialDirector.YokaiTurnFor(0, 3, true), Is.EqualTo(0f), "The first seat already faces it.");
            Assert.That(GameTutorialDirector.YokaiTurnFor(1, 3, true), Is.EqualTo(120f), "Me in the middle (P2).");
            Assert.That(GameTutorialDirector.YokaiTurnFor(2, 3, true), Is.EqualTo(240f));
            Assert.That(GameTutorialDirector.YokaiTurnFor(1, 4, false), Is.EqualTo(-90f), "The other way round for team seats.");
            Assert.That(GameTutorialDirector.YokaiTurnFor(-1, 3, true), Is.EqualTo(0f));
            // The camera at that seat sits where the first seat's would after the same turn.
            Vector3 first = LabThrowMath.SeatPosition(0, 3, Vector3.zero, 6f, 2f);
            Vector3 mine = LabThrowMath.SeatPosition(1, 3, Vector3.zero, 6f, 2f);
            Vector3 turned = Quaternion.AngleAxis(GameTutorialDirector.YokaiTurnFor(1, 3, true), Vector3.up) * first;
            Assert.That(Vector3.Distance(turned, mine), Is.LessThan(1e-3f));
            Assert.That(GameTutorialScript.SeatOf(GameTutorialSpeaker.Bul), Is.EqualTo(1), "Which is why the tutorial turns it.");
        }

        [Test]
        public void ChoiceButtonsLiftALittleUnderAFingerAndSettleBack()
        {
            Assert.That(GameTutorialPressScale.Approach(1f, 1.08f, 1.08f, .08f, .04f), Is.EqualTo(1.04f).Within(1e-4f),
                "Half way in half the time.");
            Assert.That(GameTutorialPressScale.Approach(1.04f, 1.08f, 1.08f, .08f, .1f), Is.EqualTo(1.08f).Within(1e-4f),
                "Then holds.");
            Assert.That(GameTutorialPressScale.Approach(1.08f, 1f, 1.08f, .08f, .1f), Is.EqualTo(1f).Within(1e-4f), "And back.");
            var art = ScriptableObject.CreateInstance<GameTutorialArt>();
            try { Assert.That(art.skipChoicePressScale, Is.InRange(1.02f, 1.15f), "A little, not a jump."); }
            finally { Object.DestroyImmediate(art); }
        }

        [Test]
        public void SpeakersSitMukLeftBulMiddleGeumokRightByTheGamesNeighborRule()
        {
            // A player's right neighbor is the previous seat and the left one the next seat.
            int me = GameTutorialScript.SeatOf(GameTutorialSpeaker.Bul);
            Assert.That(me, Is.EqualTo(1));
            Assert.That(GameTutorialScript.SeatOf(GameTutorialSpeaker.Geumok), Is.EqualTo(0));
            Assert.That(GameTutorialScript.SeatOf(GameTutorialSpeaker.Muk), Is.EqualTo(2));
            Assert.That(LabPlayerSlots.NeighborSeat(me, 3, true), Is.EqualTo(GameTutorialScript.SeatOf(GameTutorialSpeaker.Geumok)));
            Assert.That(LabPlayerSlots.NeighborSeat(me, 3, false), Is.EqualTo(GameTutorialScript.SeatOf(GameTutorialSpeaker.Muk)));
        }

        [Test]
        public void TutorialOverridesStillNameTheIntendedDeveloperFields()
        {
            var expected = new (int index, string label)[]
            {
                (2, "Battle Duration"), (10, "Orb Lifetime"), (45, "Item Lifetime"), (39, "Yin Chance"),
                (40, "Yang Chance"), (41, "Relay Orb Chance"), (42, "Secret Box Chance"), (43, "Potion Chance"),
                (44, "Bomb Chance"), (51, "Contact Combine"), (74, "Drum Count"), (76, "Drum Drift Speed"), (77, "Drum Drift Turning"),
                (90, "Drum Stop Near From"), (91, "Drum Stop Near To"), (92, "Drum Stop Middle To"), (93, "Drum Stop Far To"),
                (94, "Drum Stop Near Chance"), (95, "Drum Stop Middle Chance"), (96, "Drum Stop Far Chance"), (97, "Drum Pass Through Chance"),
                (87, "Gourd Bomb Interval Min"), (88, "Gourd Bomb Interval Max"), (148, "Paint Drying"),
                (152, "Swipe Block Interval"), (155, "Gourd Color Combo"), (184, "야광귀 섬광"),
                (205, "Common Gimmick Interval"), (206, "Unique Gimmick Interval"), (207, "Gimmick Gourd Bomb"),
                (208, "Gimmick Swipe Block"), (209, "Gimmick Fox Fire"), (210, "Gimmick Charm"), (238, "피버 타임 시작")
            };
            CollectionAssert.AreEquivalent(expected.Select(item => item.index),
                GameStageSettings.TutorialOverrides.Select(item => item.Key));
            foreach ((int index, string label) in expected)
                StringAssert.StartsWith(label, LabConfig.DeveloperFields[index].Label, "Field " + index);
        }

        [Test]
        public void TutorialConfigNeverEndsAndDrawsNoGimmickItemOrFever()
        {
            using (var settings = new GameStageSettings(Resources.Load<LabConfig>("LabConfig"), GameStageCatalog.RequireDefault()))
            {
                LabConfig tutorial = settings.CreateTutorialConfig();
                LabConfig stageOne = settings.CreateStageConfig(1);
                try
                {
                    Assert.That(tutorial.BattleDurationSeconds, Is.EqualTo(86400f));
                    Assert.That(tutorial.OrbLifetimeSeconds, Is.EqualTo(86400f));
                    Assert.That(tutorial.ItemLifetimeSeconds, Is.EqualTo(86400f));
                    Assert.That(tutorial.DrumCount, Is.EqualTo(1));
                    Assert.That(tutorial.DrumDriftSpeed, Is.EqualTo(0f));
                    Assert.That(tutorial.GimmickSwitches(), Is.All.False);
                    Assert.That(tutorial.CommonGimmickInterval, Is.EqualTo(0f));
                    Assert.That(tutorial.UniqueGimmickInterval, Is.EqualTo(0f));
                    Assert.That(tutorial.FeverTimeSeconds, Is.EqualTo(0f));
                    Assert.That(tutorial.GourdColorComboEnabled, Is.False);
                    Assert.That(tutorial.PaintDryingEnabled, Is.False);
                    Assert.That(tutorial.ContactCombineEnabled, Is.False, "Orbs from the drum never merge by touching.");
                    Assert.That(tutorial.DropCombineEnabled, Is.True, "Dropping one on the other still combines (2단계).");
                    // A full gauge holds exactly three taps.
                    Assert.That(tutorial.StaminaMax, Is.EqualTo(tutorial.GenerateCost * GameStageSettings.TutorialDrumTaps));
                    Assert.That(tutorial.StaminaStart, Is.EqualTo(tutorial.StaminaMax));
                    Assert.That(tutorial.GenerateCost, Is.EqualTo(stageOne.GenerateCost));
                    Assert.That(tutorial.StaminaRecoveryAmount, Is.LessThanOrEqualTo(tutorial.StaminaMax));
                    Assert.That(tutorial.HitRecovery, Is.LessThanOrEqualTo(tutorial.StaminaMax));
                    float[] values = tutorial.CaptureDeveloperValues();
                    CollectionAssert.AreEqual(new[] { 50f, 50f, 0f, 0f, 0f, 0f }, values.Skip(39).Take(6).ToArray());
                    // Everything the tutorial does not name stays the stage-1 value (throw, paint...). The gauge
                    // (maximum, start, and the refill and hit reward it caps) is sized above.
                    float[] stage = stageOne.CaptureDeveloperValues();
                    var named = GameStageSettings.TutorialOverrides.Select(item => item.Key).ToHashSet();
                    named.UnionWith(new[] { 3, 4, 6, 8 });
                    for (int i = 0; i < values.Length; i++)
                        if (!named.Contains(i)) Assert.That(values[i], Is.EqualTo(stage[i]), LabConfig.DeveloperFields[i].Label);
                }
                finally
                {
                    Object.DestroyImmediate(tutorial);
                    Object.DestroyImmediate(stageOne);
                }
            }
        }

        [Test]
        public void PracticeBoardSeatsTwoVirtualNeighborsAndHandsTheDrumToTheMiddle()
        {
            using (var settings = new GameStageSettings(Resources.Load<LabConfig>("LabConfig"), GameStageCatalog.RequireDefault()))
            {
                LabConfig tutorial = settings.CreateTutorialConfig();
                try
                {
                    const ulong me = 0UL;
                    ulong[] seats = { LabNetwork.TutorialRightSeatId, me, LabNetwork.TutorialLeftSeatId };
                    Assert.That(seats.Distinct().Count(), Is.EqualTo(3));
                    for (int round = 0; round < 8; round++)
                    {
                        var model = new LabModel();
                        model.Begin("tutorial-" + round, seats, 5d, tutorial, LabStageYokai.Gumiho);
                        Assert.That(model.TutorialHandDrumsTo(me), Is.True);
                        LabSnapshot snapshot = model.Snapshot(5d);
                        Assert.That(snapshot.players.OrderBy(player => player.seat).Select(player => player.id), Is.EqualTo(seats));
                        Assert.That(snapshot.players.Single(player => player.id == me).seat, Is.EqualTo(1));
                        Assert.That(snapshot.drums.Length, Is.EqualTo(1));
                        Assert.That(snapshot.drums[0].owner, Is.EqualTo(me));
                        Assert.That(snapshot.drums[0].x, Is.EqualTo(.5f));
                        Assert.That(snapshot.drums[0].y, Is.EqualTo(.5f));
                        // The HUD shows P3 (묵이) on the left and P1 (금옥이) on the right.
                        LabHud.GetNeighborLabels(snapshot, me, out string left, out string right);
                        Assert.That(left, Is.EqualTo("P" + (GameTutorialScript.SeatOf(GameTutorialSpeaker.Muk) + 1)));
                        Assert.That(right, Is.EqualTo("P" + (GameTutorialScript.SeatOf(GameTutorialSpeaker.Geumok) + 1)));
                    }
                    var stranger = new LabModel();
                    stranger.Begin("tutorial-stranger", seats, 5d, tutorial, LabStageYokai.Gumiho);
                    Assert.That(stranger.TutorialHandDrumsTo(99UL), Is.False);
                    Assert.That(new LabModel().TutorialHandDrumsTo(me), Is.False, "No round has begun.");
                }
                finally { Object.DestroyImmediate(tutorial); }
            }
        }

        [Test]
        public void FacesOverTheScreenEdgeMoveJustEnoughInward()
        {
            // 금옥이's 1.8x circle at 335..416 on a 390-wide screen, 6 from each edge: moves 32 left to 303..384.
            Assert.That(GameTutorialDirector.Inward(335f, 416f, 6f, 384f), Is.EqualTo(-32f));
            // 묵이's circle at -24..57 moves 30 right to 6..87.
            Assert.That(GameTutorialDirector.Inward(-24f, 57f, 6f, 384f), Is.EqualTo(30f));
            Assert.That(GameTutorialDirector.Inward(58f, 112f, 6f, 384f), Is.EqualTo(0f), "Already inside: stays.");
            Assert.That(GameTutorialDirector.Inward(-10f, 400f, 6f, 384f), Is.EqualTo(0f), "Too large to fit: not chased.");
        }

        [Test]
        public void SpotlightIsOneSheetWhoseOuterRingReachesEveryScreenCorner()
        {
            // One mesh, no pieces: for a round hole (the gourd, the drum) and a capsule (the stamina bar) alike,
            // the dark ring's polygon must still cover every corner of the screen.
            var area = new Rect(-195f, -422f, 390f, 844f);
            var holes = new (Rect hole, float corner)[]
            {
                (new Rect(-100f, 60f, 200f, 200f), 100f), (new Rect(-280f, 300f, 200f, 200f), 100f),
                (new Rect(200f, -600f, 200f, 200f), 100f), (new Rect(-92f, -412f, 239f, 60f), 30f)
            };
            var points = new System.Collections.Generic.List<Vector2>();
            var normals = new System.Collections.Generic.List<Vector2>();
            foreach ((Rect hole, float corner) in holes)
            {
                GameTutorialSpotlight.Outline(hole, corner, points, normals);
                Assert.That(points.Count, Is.EqualTo(normals.Count));
                // Every outline point lies on the rounded rectangle's edge, with a unit outward normal.
                for (int i = 0; i < points.Count; i++)
                {
                    Assert.That(normals[i].magnitude, Is.EqualTo(1f).Within(1e-4f));
                    Vector2 inner = new Vector2(Mathf.Clamp(points[i].x, hole.xMin + corner, hole.xMax - corner),
                        Mathf.Clamp(points[i].y, hole.yMin + corner, hole.yMax - corner));
                    Assert.That(Vector2.Distance(points[i], inner), Is.EqualTo(corner).Within(1e-3f));
                    // No point twice: a near-zero edge points anywhere and leaves a sliver in the mesh.
                    Assert.That(Vector2.Distance(points[i], points[(i + 1) % points.Count]), Is.GreaterThan(.1f),
                        hole + " points " + i + " and " + (i + 1) % points.Count);
                }
                float far = GameTutorialSpotlight.CoveringRadius(area, hole.center, points);
                var ring = points.Select(point => hole.center + (point - hole.center).normalized * far).ToArray();
                foreach (Vector2 corner2 in new[] { area.min, area.max, new Vector2(area.xMin, area.yMax), new Vector2(area.xMax, area.yMin) })
                    Assert.That(InsideConvex(ring, corner2), Is.True, hole + " → " + corner2);
            }
            Assert.That(typeof(GameTutorialDirector).GetField("dimBars",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic), Is.Null,
                "Overlapping dim pieces drew a square outline around the gourd.");
        }

        private static bool InsideConvex(Vector2[] polygon, Vector2 point)
        {
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Length];
                if ((b - a).sqrMagnitude < 1e-8f) continue;
                if ((b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x) < 0f) return false;
            }
            return true;
        }

        [Test]
        public void ScriptedTapsMakeYinYangYinThenTheHeldGaugeStaysEmptyUntilReleased()
        {
            using (var settings = new GameStageSettings(Resources.Load<LabConfig>("LabConfig"), GameStageCatalog.RequireDefault()))
            {
                LabConfig tutorial = settings.CreateTutorialConfig();
                try
                {
                    const ulong me = 0UL;
                    ulong[] seats = { LabNetwork.TutorialRightSeatId, me, LabNetwork.TutorialLeftSeatId };
                    for (int round = 0; round < 6; round++)
                    {
                        var model = new LabModel();
                        double now = 10d;
                        model.Begin("tutorial-drum-" + round, seats, now, tutorial, LabStageYokai.Gumiho);
                        model.TutorialHandDrumsTo(me);
                        Assert.That(model.TutorialHoldStaminaRecovery(true, now), Is.True);
                        Assert.That(model.TutorialScriptGenerations(me, LabOrbKind.Yin, LabOrbKind.Yang, LabOrbKind.Yin), Is.True);
                        var made = new System.Collections.Generic.List<LabOrbKind>();
                        for (int tap = 0; tap < 3; tap++)
                        {
                            now += .5d;
                            Assert.That(model.TryGenerate(me, "tap-" + tap, now, out LabOrbState orb, out string reason), Is.True, reason);
                            made.Add(orb.kind);
                        }
                        CollectionAssert.AreEqual(new[] { LabOrbKind.Yin, LabOrbKind.Yang, LabOrbKind.Yin }, made);
                        Assert.That(model.TutorialScriptedGenerationsLeft, Is.EqualTo(0));
                        LabPlayerState player = model.Snapshot(now).players.Single(p => p.id == me);
                        Assert.That(player.stamina, Is.EqualTo(0f).Within(1e-4f), "The third tap empties the gauge.");
                        Assert.That(player.generatedTotal, Is.EqualTo(3));
                        now += .5d;
                        Assert.That(model.TryGenerate(me, "tap-4", now, out _, out string refused), Is.False);
                        Assert.That(refused, Is.EqualTo(LabModel.InsufficientStaminaReason));
                        // Held: five seconds pass and nothing comes back, nor later all at once.
                        now += 5d;
                        model.Tick(now);
                        Assert.That(model.Snapshot(now).players.Single(p => p.id == me).stamina, Is.EqualTo(0f).Within(1e-4f));
                        // Capped: it breathes back only a sliver, still short of a tap, and stops there.
                        float sliver = GameTutorialDirector.ShortStaminaFor(tutorial.GenerateCost, .5f);
                        Assert.That(model.TutorialCapStaminaRecovery(sliver, now), Is.True);
                        Assert.That(model.TutorialStaminaHeld, Is.True);
                        now += 3d;
                        model.Tick(now);
                        Assert.That(model.Snapshot(now).players.Single(p => p.id == me).stamina, Is.EqualTo(sliver).Within(1e-3f));
                        Assert.That(sliver, Is.GreaterThan(0f).And.LessThan(tutorial.GenerateCost));
                        now += .5d;
                        Assert.That(model.TryGenerate(me, "tap-5", now, out _, out string stillShort), Is.False);
                        Assert.That(stillShort, Is.EqualTo(LabModel.InsufficientStaminaReason));
                        // Released: the normal rate again from the sliver, nothing owed for the held time.
                        Assert.That(model.TutorialHoldStaminaRecovery(false, now), Is.True);
                        Assert.That(model.TutorialStaminaHeld, Is.False);
                        now += 1d;
                        model.Tick(now);
                        float refilled = model.Snapshot(now).players.Single(p => p.id == me).stamina;
                        Assert.That(refilled, Is.EqualTo(Mathf.Min(tutorial.StaminaMax, sliver + tutorial.StaminaRecoveryPerSecond)).Within(1e-3f));
                        Assert.That(model.TutorialCapStaminaRecovery(-1f, now), Is.False, "No negative ceiling.");
                    }
                    var other = new LabModel();
                    other.Begin("tutorial-drum-other", seats, 1d, tutorial, LabStageYokai.Gumiho);
                    Assert.That(other.TutorialScriptGenerations(99UL, LabOrbKind.Yin), Is.False, "Only a seated player.");
                    Assert.That(other.TutorialScriptGenerations(me, LabOrbKind.Drum), Is.False, "A drum is never generated.");
                }
                finally { Object.DestroyImmediate(tutorial); }
            }
        }

        [Test]
        public void ShortCueLeavesASliverShortOfOneTap()
        {
            Assert.That(GameTutorialDirector.ShortStaminaFor(20f, .5f), Is.EqualTo(10f));
            Assert.That(GameTutorialDirector.ShortStaminaFor(20f, 1.5f), Is.LessThan(20f), "Never a whole tap.");
            Assert.That(GameTutorialDirector.ShortStaminaFor(20f, 0f), Is.GreaterThan(0f), "Always some red to see.");
            GameTutorialArt art = GameTutorialArt.Load();
            Assert.That(art.shortStaminaShareOfTap, Is.GreaterThan(0f).And.LessThan(1f));
        }

        [Test]
        public void PointingHandPressesOnceAPeriodAndRests()
        {
            Assert.That(GameTutorialDirector.HandPress(0f), Is.EqualTo(0f));
            Assert.That(GameTutorialDirector.HandPress(.15f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(GameTutorialDirector.HandPress(.35f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(GameTutorialDirector.HandPress(.7f), Is.EqualTo(0f));
            Assert.That(GameTutorialDirector.HandPress(1.15f), Is.EqualTo(1f).Within(1e-5f), "Repeats every period.");
        }

        [Test]
        public void BubblePopsPastFullSizeAndSettles()
        {
            Assert.That(GameTutorialDirector.PopScale(0f, 1.1f), Is.EqualTo(0f));
            Assert.That(GameTutorialDirector.PopScale(.6f, 1.1f), Is.EqualTo(1.1f).Within(1e-5f));
            Assert.That(GameTutorialDirector.PopScale(1f, 1.1f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(GameTutorialDirector.PopScale(2f, 1.1f), Is.EqualTo(1f).Within(1e-5f));
            float previous = 0f;
            for (float t = .05f; t <= .6f; t += .05f)
            {
                float scale = GameTutorialDirector.PopScale(t, 1.1f);
                Assert.That(scale, Is.GreaterThan(previous), "Rising at " + t);
                previous = scale;
            }
        }

        [Test]
        public void TutorialArtHoldsTheThreeFacesTheBubbleAndTheShillaFonts()
        {
            GameTutorialArt art = GameTutorialArt.Load();
            Assert.That(art, Is.Not.Null, GameTutorialArt.ResourcePath);
            Assert.That(art.muk, Is.Not.Null);
            Assert.That(art.bul, Is.Not.Null);
            Assert.That(art.geumok, Is.Not.Null);
            Assert.That(art.bubble, Is.Not.Null);
            Assert.That(art.dialogueFont != null ? art.dialogueFont.name : null, Is.EqualTo("Shilla_Culture(M)"));
            Assert.That(art.bannerFont != null ? art.bannerFont.name : null, Is.EqualTo("ShillaCultureBold"));
            Assert.That(art.bannerTextColor, Is.EqualTo(Color.black), "Black Shilla guide text.");
            Assert.That(art.hand, Is.Not.Null, "The pointing hand.");
            Assert.That(art.tapHint, Is.Not.Null, "The ▽ that says tap to go on.");
            // 성공! replaces the banner: black #000000 with Figma's 0.2 stroke.
            Assert.That(art.successColor, Is.EqualTo(Color.black));
            Assert.That((art.successStroke, art.successStrokeColor), Is.EqualTo((.2f, Color.black)));
            Assert.That(art.minFontSize, Is.LessThanOrEqualTo(art.maxFontSize));
        }

        [Test]
        public void SkipDialogAsksInTheMockupsWordsOnTheGivenFrames()
        {
            Assert.That(GameTutorialScript.SkipButtonLabel, Is.EqualTo("건너뛰기"));
            Assert.That(GameTutorialScript.SkipTitle, Is.EqualTo("튜토리얼을 건너뛰겠소?"));
            Assert.That(GameTutorialScript.SkipBody.Split('\n'), Is.EqualTo(new[]
            {
                "튜토리얼을 건너뛰면 구슬 전달과 조합 등", "기본 조작을 익히지 못해", "협동 플레이에 어려움이 있을 수 있소."
            }));
            Assert.That((GameTutorialScript.SkipStay, GameTutorialScript.SkipLeave), Is.EqualTo(("계속 배우기", "건너뛰기")));
            GameTutorialArt art = GameTutorialArt.Load();
            Assert.That(art.skipPanel, Is.Not.Null, "The dialog frame.");
            Assert.That(art.skipButton, Is.Not.Null, "The button frame.");
            // Nine-sliced, so the frame keeps its width however wide the window or button is drawn.
            Assert.That(art.skipPanel.border.x, Is.GreaterThan(0f));
            Assert.That(art.skipButton.border.x, Is.GreaterThan(0f));
            float scale = art.skipArtPixelsPerPoint;
            Assert.That(art.skipPanel.border.y / scale * 2f, Is.LessThan(art.skipPanelSize.y), "The window fits its own corners.");
            Assert.That(art.skipButton.border.y / scale * 2f, Is.LessThan(art.skipButtonSize.y), "The small button too.");
            Assert.That(art.skipButton.border.y / scale * 2f, Is.LessThan(art.skipChoiceSize.y));
            Assert.That(art.skipCloseSeconds, Is.LessThan(art.popSeconds), "It closes faster than it opens.");
        }

        [Test]
        public void SkipButtonFadesInOverHalfASecondAfterTheFirstTap()
        {
            Assert.That(GameTutorialDirector.SkipFade(-1f, .5f), Is.EqualTo(0f), "Not before a tap.");
            Assert.That(GameTutorialDirector.SkipFade(0f, .5f), Is.EqualTo(0f));
            Assert.That(GameTutorialDirector.SkipFade(.25f, .5f), Is.EqualTo(.5f).Within(1e-5f));
            Assert.That(GameTutorialDirector.SkipFade(.5f, .5f), Is.EqualTo(1f));
            Assert.That(GameTutorialDirector.SkipFade(9f, .5f), Is.EqualTo(1f), "Then stays.");
            var art = ScriptableObject.CreateInstance<GameTutorialArt>();
            try { Assert.That(art.skipFadeSeconds, Is.EqualTo(.5f)); }
            finally { Object.DestroyImmediate(art); }
        }

        [Test]
        public void TapHintStartsAtRestEasesDownAndBackUpEachPeriod()
        {
            const float period = .8f, depth = 4f;
            Assert.That(GameTutorialDirector.TapHintDrop(0f, period, depth), Is.EqualTo(0f), "Appears at rest.");
            Assert.That(GameTutorialDirector.TapHintDrop(period * .25f, period, depth), Is.EqualTo(-depth * .5f).Within(1e-4f));
            Assert.That(GameTutorialDirector.TapHintDrop(period * .5f, period, depth), Is.EqualTo(-depth).Within(1e-4f),
                "Lowest halfway.");
            Assert.That(GameTutorialDirector.TapHintDrop(period * .75f, period, depth), Is.EqualTo(-depth * .5f).Within(1e-4f));
            Assert.That(GameTutorialDirector.TapHintDrop(period, period, depth), Is.EqualTo(0f).Within(1e-4f), "Back up.");
            Assert.That(GameTutorialDirector.TapHintDrop(period * 2.5f, period, depth), Is.EqualTo(-depth).Within(1e-4f),
                "Over and over.");
            for (float t = 0f; t < 3f; t += .01f)
                Assert.That(GameTutorialDirector.TapHintDrop(t, period, depth), Is.InRange(-depth, 0f), "Never above its rest.");
            Assert.That(GameTutorialDirector.TapHintDrop(.4f, period, 0f), Is.EqualTo(0f), "No depth: still.");
            Assert.That(GameTutorialDirector.TapHintDrop(.4f, 0f, depth), Is.EqualTo(0f), "No period: still.");
            var art = ScriptableObject.CreateInstance<GameTutorialArt>();
            try
            {
                Assert.That(art.tapHintSize.x / art.tapHintSize.y, Is.EqualTo(256f / 223f).Within(.05f), "The art's own shape.");
            }
            finally { Object.DestroyImmediate(art); }
        }
    }
}
