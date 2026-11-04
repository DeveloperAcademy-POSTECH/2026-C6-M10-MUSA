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
            Assert.That(art.minFontSize, Is.LessThanOrEqualTo(art.maxFontSize));
        }
    }
}
