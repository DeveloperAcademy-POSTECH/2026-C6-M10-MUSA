using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>
    /// 매혹 (#664) on the board: a charmed player's board stands still and takes no touch until the charm ends, then goes on
    /// as it was; the board beside a charmed player shuts its edge toward it with a pink wall that bounces everything back.
    /// </summary>
    [Category("Slow")]
    public sealed class LabCharmBoardTests
    {
        private static readonly Rect Bounds = new Rect(-4f, -2f, 8f, 4f);
        private GameObject root;
        private LabConfig config;
        private LabOrbBoard board;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Lab charm board test root");
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
        public IEnumerator ACharmedBoardStandsStillTakesNoTouchAndGoesOnAfterwards()
        {
            board.UpsertOrb("rolling", LabOrbKind.Yang, new Vector2(.3f, .5f), new Vector2(.25f, 0f));
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            Assert.That(board.TryGetMotion("rolling", out Vector2 before, out Vector2 moving), Is.True);
            Assert.That(moving.x, Is.GreaterThan(0f), "Rolling before the charm.");

            board.SetCharm(10f);
            Assert.That((board.Charmed, board.SimulationHeld), Is.EqualTo((true, true)));
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            Assert.That(board.TryGetMotion("rolling", out Vector2 held, out _), Is.True);
            Assert.That(Vector2.Distance(held, before), Is.LessThan(.05f), "It stands where the charm caught it.");
            Vector2 world = WorldOf(held);
            Assert.That(board.TryBeginDragAtWorld(world, 1d), Is.False, "No touch is taken while charmed.");

            board.SetCharm(0f);
            Assert.That((board.Charmed, board.SimulationHeld), Is.EqualTo((false, false)));
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            Assert.That(board.TryGetMotion("rolling", out Vector2 after, out _), Is.True);
            Assert.That(after.x, Is.GreaterThan(held.x), "Awake, it rolls on the way it was going.");
        }

        [Test]
        public void TheGameStartHoldAndTheCharmHoldNeitherLiftsTheOther()
        {
            board.SetSimulationHeld(true);
            board.SetCharm(10f);
            board.SetSimulationHeld(false);
            Assert.That(board.SimulationHeld, Is.True, "Still charmed after the start hold lifts.");
            board.SetSimulationHeld(true);
            board.SetCharm(0f);
            Assert.That(board.SimulationHeld, Is.True, "Still held for the start after the charm ends.");
            board.SetSimulationHeld(false);
            Assert.That(board.SimulationHeld, Is.False);
        }

        [UnityTest]
        public IEnumerator TheEdgeTowardACharmedNeighbourBouncesEverythingWhileTheOtherEdgeStillPasses()
        {
            var crossed = new List<LabOrbEdgeCrossing>();
            board.EdgeCrossed += crossed.Add;
            board.SetCharmWalls(1);
            Assert.That(board.CharmWallSide, Is.EqualTo(1));
            LabOrbTeamWall[] walls = CharmWalls();
            Assert.That(walls.Count(wall => wall.Visible), Is.EqualTo(1));
            Assert.That(walls.Single(wall => wall.Visible).Right, Is.True);
            Assert.That(walls.Single(wall => wall.Visible).Tint, Is.EqualTo(LabOrbBoard.CharmWallTint), "A pink wall.");

            board.UpsertOrb("swiped", LabOrbKind.Yang, new Vector2(.995f, .65f), new Vector2(.35f, .025f));
            for (int i = 0; i < 20 && board.CharmWallBounces == 0; i++) yield return new WaitForFixedUpdate();
            Assert.That(board.CharmWallBounces, Is.GreaterThanOrEqualTo(1));
            Assert.That(board.TryGetMotion("swiped", out _, out Vector2 back), Is.True);
            Assert.That(back.x, Is.LessThan(0f), "It comes back in.");

            board.UpsertOrb("inner", LabOrbKind.Yin, new Vector2(.005f, .4f), new Vector2(-.35f, 0f));
            for (int i = 0; i < 20 && !crossed.Exists(crossing => crossing.OrbId == "inner"); i++) yield return new WaitForFixedUpdate();
            Assert.That(crossed.Exists(crossing => crossing.OrbId == "inner" && !crossing.ToRight), Is.True, "The other edge passes.");
            Assert.That(crossed.Exists(crossing => crossing.ToRight), Is.False, "Nothing goes to the charmed side.");

            board.SetCharmWalls(LabTeams.BothSides);
            Assert.That(CharmWalls().Count(wall => wall.Visible), Is.EqualTo(2), "Both neighbours charmed: both edges shut.");
            board.SetCharmWalls(0);
            Assert.That(CharmWalls().Count(wall => wall.Visible), Is.Zero, "Open again once the charm ends.");
            board.SetCharmWalls(-1);
            board.Clear();
            Assert.That(board.CharmWallSide, Is.Zero, "A new round starts with no walls.");
            Assert.That(CharmWalls().Count(wall => wall.Visible), Is.Zero);
        }

        private LabOrbTeamWall[] CharmWalls() => board.GetComponentsInChildren<LabOrbTeamWall>(true)
            .Where(wall => wall.name == "Charm Wall Edge").ToArray();

        private static Vector2 WorldOf(Vector2 normalized) =>
            new Vector2(Bounds.xMin + normalized.x * Bounds.width, Bounds.yMin + normalized.y * Bounds.height);
    }
}
