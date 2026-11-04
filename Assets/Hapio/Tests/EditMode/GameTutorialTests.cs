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
            foreach (GameTutorialLine line in goal.Lines)
                Assert.That(line.Text.Split('\n').Length, Is.LessThanOrEqualTo(2), "One bubble holds two lines at most.");
            Assert.That(GameTutorialScript.StageLabel, Is.EqualTo("튜토리얼"));
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
                (44, "Bomb Chance"), (74, "Drum Count"), (76, "Drum Drift Speed"), (77, "Drum Drift Turning"),
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
                    float[] values = tutorial.CaptureDeveloperValues();
                    CollectionAssert.AreEqual(new[] { 50f, 50f, 0f, 0f, 0f, 0f }, values.Skip(39).Take(6).ToArray());
                    // Everything the tutorial does not name stays the stage-1 value (stamina, throw, paint...).
                    float[] stage = stageOne.CaptureDeveloperValues();
                    var named = GameStageSettings.TutorialOverrides.Select(item => item.Key).ToHashSet();
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
            // One mesh, no pieces: its 72-sided outer ring must still cover the farthest corner of the screen.
            var area = new Rect(-195f, -422f, 390f, 844f);
            foreach (Vector2 center in new[] { new Vector2(0f, 160f), new Vector2(-180f, 400f), new Vector2(300f, -500f) })
            {
                float covering = GameTutorialSpotlight.CoveringRadius(area, center, 100f);
                float inscribed = covering * Mathf.Cos(Mathf.PI / 72f);
                foreach (Vector2 corner in new[] { area.min, area.max, new Vector2(area.xMin, area.yMax), new Vector2(area.xMax, area.yMin) })
                    Assert.That(inscribed, Is.GreaterThan(Vector2.Distance(center, corner)), center + " → " + corner);
            }
            Assert.That(typeof(GameTutorialDirector).GetField("dimBars",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic), Is.Null,
                "Overlapping dim pieces drew a square outline around the gourd.");
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
            Assert.That(art.minFontSize, Is.LessThanOrEqualTo(art.maxFontSize));
        }
    }
}
