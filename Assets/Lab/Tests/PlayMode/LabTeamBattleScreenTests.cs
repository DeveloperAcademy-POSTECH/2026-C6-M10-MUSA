using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace C6Lab.Tests
{
    /// <summary>2:2 청홍 땅따먹기 (#281) 전투 화면: 판의 바깥쪽 벽과 위쪽 청·홍 막대, 내 팀 고리.</summary>
    public sealed class LabTeamBattleScreenTests
    {
        private static readonly Rect Bounds = new Rect(-4f, -2f, 8f, 4f);
        private GameObject root;
        private LabConfig config;
        private LabOrbBoard board;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Lab team screen test root");
            config = ScriptableObject.CreateInstance<LabConfig>();
            var cameraObject = new GameObject("Board camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera inputCamera = cameraObject.AddComponent<Camera>();
            inputCamera.orthographic = true;
            var boardObject = new GameObject("Board");
            boardObject.transform.SetParent(root.transform, false);
            board = boardObject.AddComponent<LabOrbBoard>();
            board.Configure(config, inputCamera, Bounds);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) Object.Destroy(root);
            if (config != null) Object.Destroy(config);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheOuterWallBouncesEverythingWhileTheInnerEdgeStillPasses()
        {
            var crossed = new System.Collections.Generic.List<LabOrbEdgeCrossing>();
            board.EdgeCrossed += crossed.Add;
            board.SetTeamWall(1);
            Assert.That(board.TeamWallSide, Is.EqualTo(1));
            Assert.That(board.TeamWall.Visible && board.TeamWall.Right, Is.True);

            board.UpsertOrb("swiped", LabOrbKind.Yang, new Vector2(.995f, .65f), new Vector2(.35f, .025f));
            // Read the motion on the bounce itself: the floor slows the orb to a stop within about ten steps.
            for (int i = 0; i < 20 && board.TeamWallBounces == 0; i++) yield return new WaitForFixedUpdate();
            Assert.That(board.TeamWallBounces, Is.GreaterThanOrEqualTo(1));
            Assert.That(board.TryGetMotion("swiped", out _, out Vector2 back), Is.True);
            Assert.That(back.x, Is.LessThan(0f), "It comes back in like off the top and bottom walls.");
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            Assert.That(crossed, Is.Empty, "The closed outer edge never hands an orb over.");

            board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.75f, .3f), lockSwipes: 2);
            board.UpsertOrb("flung", LabOrbKind.Yin, new Vector2(.875f, .3f));
            Assert.That(board.Blast("bomb", new Vector2(.75f, .3f)), Is.GreaterThanOrEqualTo(1));
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            Assert.That(crossed.Exists(crossing => crossing.ToRight), Is.False, "A blast cannot push it through either.");

            board.UpsertOrb("inner", LabOrbKind.Yin, new Vector2(.005f, .4f), new Vector2(-.35f, 0f));
            // The earlier blast may already have sent another body through the open left edge.
            // Wait for this orb, rather than skipping physics as soon as any crossing exists.
            for (int i = 0; i < 20 && !crossed.Exists(crossing => crossing.OrbId == "inner" && !crossing.ToRight); i++)
                yield return new WaitForFixedUpdate();
            Assert.That(crossed.Exists(crossing => crossing.OrbId == "inner" && !crossing.ToRight), Is.True,
                "The teammate's side still passes. Observed: " + string.Join(", ",
                    crossed.ConvertAll(crossing => crossing.OrbId + (crossing.ToRight ? ":right" : ":left"))));
            Assert.That(crossed.Exists(crossing => crossing.ToRight), Is.False,
                "The outer wall stays closed while the teammate-side pass completes.");

            board.Clear();
            Assert.That(board.TeamWallSide, Is.Zero, "A new round starts without the wall until the Host says so.");
            Assert.That(board.TeamWall.Visible, Is.False);
        }

        [UnityTest]
        public IEnumerator TheShareBarFillsBlueFromTheLeftAndRedFromTheRightAndRingsMyBadge()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            canvasObject.transform.SetParent(root.transform, false);
            var battle = new GameObject("FigmaBattleUi", typeof(RectTransform));
            battle.transform.SetParent(canvasObject.transform, false);
            var badgeObject = new GameObject("Local Badge", typeof(RectTransform), typeof(Image));
            badgeObject.transform.SetParent(battle.transform, false);
            Image badge = badgeObject.GetComponent<Image>();
            ((RectTransform)badge.transform).sizeDelta = new Vector2(40f, 40f);

            LabTeamBar bar = LabTeamBar.Create((RectTransform)battle.transform, null);
            var snapshot = new LabSnapshot
            {
                teamTerritory = true, phase = LabPhase.Playing, paintCellCount = 2048, blueCells = 512, redCells = 1024
            };
            for (float waited = 0f; waited < 1.6f; waited += Time.unscaledDeltaTime)
            {
                bar.Present(snapshot, LabTeam.Red, badge);
                yield return null;
            }
            Assert.That(bar.BlueShare, Is.EqualTo(.25f).Within(.001f));
            Assert.That(bar.RedShare, Is.EqualTo(.5f).Within(.001f));
            Image blue = bar.transform.Find("Fill Mask/Blue Fill").GetComponent<Image>();
            Image red = bar.transform.Find("Fill Mask/Red Fill").GetComponent<Image>();
            Assert.That(blue.sprite != null && red.sprite != null, Is.True,
                "Without a sprite an Image ignores Filled and covers the whole bar (#281 first screen).");
            Assert.That(blue.type == Image.Type.Filled && red.type == Image.Type.Filled, Is.True);
            Assert.That(blue.fillOrigin, Is.EqualTo((int)Image.OriginHorizontal.Left));
            Assert.That(red.fillOrigin, Is.EqualTo((int)Image.OriginHorizontal.Right));
            Assert.That(blue.fillAmount, Is.EqualTo(.25f).Within(.001f));
            Assert.That(red.fillAmount, Is.EqualTo(.5f).Within(.001f));
            // The flags stand on each team's edge: Blue a quarter in from the left, Red half in from the right.
            Assert.That(bar.BlueFlagAt, Is.EqualTo((3f + .25f * 330f) / 336f).Within(.001f));
            Assert.That(bar.RedFlagAt, Is.EqualTo(1f - (3f + .5f * 330f) / 336f).Within(.001f));
            Assert.That(bar.BlueFlagAt, Is.LessThan(bar.RedFlagAt));
            Assert.That(bar.transform.Find("Blue Flag/Cloth/Label").GetComponent<Text>().text, Is.EqualTo("청"));
            Assert.That(bar.transform.Find("Red Flag/Cloth/Label").GetComponent<Text>().text, Is.EqualTo("홍"));
            Assert.That(bar.LocalRing, Is.Not.Null);
            Assert.That(bar.LocalRing.gameObject.activeSelf, Is.True);
            Assert.That(bar.LocalRing.color, Is.EqualTo(LabTeamBar.Red));
            Assert.That(bar.LocalRing.transform.GetSiblingIndex(), Is.LessThan(badge.transform.GetSiblingIndex()),
                "The ring sits behind the badge.");

            bar.Hide();
            Assert.That(bar.Visible, Is.False);
            Assert.That(bar.LocalRing.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void FeverGaugesCountHitsThenTheFeverTimeAndTheResultShowsFinalNumbers()
        {
            RectTransform battle = Battle();
            LabTeamBar bar = LabTeamBar.Create(battle, null);
            var snapshot = new LabSnapshot
            {
                teamTerritory = true, phase = LabPhase.Playing, serverTime = 20d, paintCellCount = 1000,
                blueCells = 300, redCells = 200, blueFeverHits = 6, redFeverStartedAt = 16d, redFeverUntil = 24d
            };
            bar.Present(snapshot, LabTeam.Blue, null, 15);
            Assert.That(bar.GaugesShown, Is.True);
            Assert.That(bar.BlueGauge, Is.EqualTo(6f / 15f).Within(.001f));
            Assert.That(bar.BlueGaugeText, Is.EqualTo("피버 6/15"));
            Assert.That(bar.RedGauge, Is.EqualTo(.5f).Within(.001f), "During a fever the gauge runs down its time.");
            Assert.That(bar.RedGaugeText, Is.EqualTo("피버! 4초"));
            Assert.That(bar.NumbersText, Is.Empty, "The battle bar shows no numbers.");
            bar.Present(snapshot, LabTeam.Blue, null, 0);
            Assert.That(bar.GaugesShown, Is.False, "No fever setting, no gauges.");

            snapshot.phase = LabPhase.Victory;
            bar.ShowFinal(snapshot, Color.white);
            Assert.That(bar.BlueShare, Is.EqualTo(.3f).Within(.001f), "The result fills at once, without easing.");
            Assert.That(bar.RedShare, Is.EqualTo(.2f).Within(.001f));
            Assert.That(bar.NumbersText, Is.EqualTo("청 30.0% / 빈 칸 50.0% / 홍 20.0%"));
            Assert.That(bar.BlueFlagAt, Is.EqualTo((3f + .3f * 330f) / 336f).Within(.001f), "The result flags stand at the final edges.");
            Assert.That(bar.GaugesShown, Is.False);
        }

        [UnityTest]
        public IEnumerator OnlyMyTeamsFeverIsAnnouncedOnceWithTheSameWordsAndGlowsAtTheEdges()
        {
            foreach (LabTeam mine in new[] { LabTeam.Blue, LabTeam.Red })
            {
                LabTeamFever fever = LabTeamFever.Create(Battle(), null);
                var snapshot = new LabSnapshot { teamTerritory = true, phase = LabPhase.Playing, serverTime = 10.2d };
                LabTeam other = mine == LabTeam.Blue ? LabTeam.Red : LabTeam.Blue;
                SetFever(snapshot, other, 10d, 18d);
                fever.Present(snapshot, mine);
                Assert.That(fever.BannerShown, Is.False, "The other team's fever shows only on its gauge.");
                Assert.That(fever.GlowAlpha, Is.Zero);

                SetFever(snapshot, mine, 10.1d, 18.1d);
                fever.Present(snapshot, mine);
                Assert.That(fever.BannerShown, Is.True);
                Assert.That(fever.BannerText, Is.EqualTo("피버타임!"), "The same words for Blue and Red.");
                Assert.That(fever.SubtitleText, Is.EqualTo("마구 던져버리자!!"));
                Assert.That(fever.GlowAlpha, Is.GreaterThanOrEqualTo(.45f), "A clear edge colour, not a faint one.");
                Color team = mine == LabTeam.Blue ? LabTeamBar.Blue : LabTeamBar.Red;
                Assert.That(fever.GlowColor.r, Is.EqualTo(team.r).Within(.001f));
                Assert.That(fever.GlowColor.b, Is.EqualTo(team.b).Within(.001f));

                for (float waited = 0f; waited < 1.8f; waited += Time.unscaledDeltaTime)
                {
                    fever.Present(snapshot, mine);
                    yield return null;
                }
                Assert.That(fever.BannerShown, Is.False, "The banner fades away after its moment.");
                fever.Present(snapshot, mine);
                Assert.That(fever.BannerShown, Is.False, "The same fever is never announced twice.");

                snapshot.serverTime = 30d;
                SetFever(snapshot, mine, 12d, 20d);
                for (float waited = 0f; waited < .7f; waited += Time.unscaledDeltaTime)
                {
                    fever.Present(snapshot, mine);
                    yield return null;
                }
                Assert.That(fever.BannerShown, Is.False, "A fever seen long after it began is not announced.");
                Assert.That(fever.GlowAlpha, Is.Zero, "The edges calm down when the fever ends.");
                Object.Destroy(fever.gameObject);
            }
        }

        private static void SetFever(LabSnapshot snapshot, LabTeam team, double startedAt, double until)
        {
            if (team == LabTeam.Blue) { snapshot.blueFeverStartedAt = startedAt; snapshot.blueFeverUntil = until; }
            else { snapshot.redFeverStartedAt = startedAt; snapshot.redFeverUntil = until; }
        }

        [UnityTest]
        public IEnumerator AFeverOrbWearsARainbowAndATurnedOrbSwellsAsItChanges()
        {
            LabOrbView yin = board.UpsertOrb("turned", LabOrbKind.Yin, new Vector2(.5f, .5f));
            Assert.That(yin.FeverShown, Is.False);
            yield return null;
            LabOrbView turned = board.UpsertOrb("turned", LabOrbKind.Combined, new Vector2(.5f, .5f),
                element: LabOrbElement.Water, fever: true);
            Assert.That(turned.Kind, Is.EqualTo(LabOrbKind.Combined));
            Assert.That(turned.FeverShown, Is.True);
            if (turned.Visual != null) Assert.That(turned.Visual.localScale.x, Is.GreaterThan(1f), "It swells, then settles.");
            yield return new WaitForSeconds(.6f);
            if (turned.Visual != null) Assert.That(turned.Visual.localScale.x, Is.EqualTo(1f).Within(.001f));

            LabOrbView plain = board.UpsertOrb("plain", LabOrbKind.Combined, new Vector2(.3f, .3f), element: LabOrbElement.Fire);
            Assert.That(plain.FeverShown, Is.False, "An ordinary combined orb has no rainbow.");
            board.UpsertOrb("turned", LabOrbKind.Combined, new Vector2(.5f, .5f), element: LabOrbElement.Water, fever: false);
            Assert.That(turned.FeverShown, Is.False);
        }

        private RectTransform Battle()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            canvasObject.transform.SetParent(root.transform, false);
            var battle = new GameObject("FigmaBattleUi", typeof(RectTransform));
            battle.transform.SetParent(canvasObject.transform, false);
            return (RectTransform)battle.transform;
        }
    }
}
