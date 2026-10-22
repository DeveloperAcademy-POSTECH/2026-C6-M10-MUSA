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
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            Assert.That(crossed, Is.Empty, "The closed outer edge never hands an orb over.");
            Assert.That(board.TeamWallBounces, Is.GreaterThanOrEqualTo(1));
            Assert.That(board.TryGetMotion("swiped", out _, out Vector2 back), Is.True);
            Assert.That(back.x, Is.LessThan(0f), "It comes back in like off the top and bottom walls.");

            board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.75f, .3f), lockSwipes: 2);
            board.UpsertOrb("flung", LabOrbKind.Yin, new Vector2(.875f, .3f));
            Assert.That(board.Blast("bomb", new Vector2(.75f, .3f)), Is.GreaterThanOrEqualTo(1));
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            Assert.That(crossed.Exists(crossing => crossing.ToRight), Is.False, "A blast cannot push it through either.");

            board.UpsertOrb("inner", LabOrbKind.Yin, new Vector2(.005f, .4f), new Vector2(-.35f, 0f));
            for (int i = 0; i < 20 && crossed.Count == 0; i++) yield return new WaitForFixedUpdate();
            Assert.That(crossed.Exists(crossing => crossing.OrbId == "inner" && !crossing.ToRight), Is.True,
                "The teammate's side still passes.");

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
    }
}
