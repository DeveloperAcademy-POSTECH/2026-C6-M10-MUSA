using NUnit.Framework;
using UnityEngine;
namespace C6.Prototype.Orbs.Tests
{
    public sealed class TransferEdgeGestureTests
    {
        static readonly Rect Lower = new Rect(0, 0, 1000, 600);
        static readonly GestureTuning Tuning = new GestureTuning(.18f, 1.25f, .08f, .18f, true, true, true, .055f);
        OrbGestureEngine engine;
        static OrbRecord Orb(string id = "a", OrbKind kind = OrbKind.Raw, OrbPolarity polarity = OrbPolarity.Yin)
            => new OrbRecord(id, kind, kind == OrbKind.Combined ? OrbPolarity.None : polarity, 1, OrbAuthorityState.Idle, new Vector2(.5f,.5f), EntrySide.None, 0);
        [SetUp] public void Setup() => engine = new OrbGestureEngine();
        void Begin(float x = 500, float y = 300, OrbKind kind = OrbKind.Raw)
            => Assert.That(engine.Begin(1, Orb(kind:kind), new Vector2(x,y), false, Lower, 1000, Tuning), Is.True);
        [TestCase(40f, OrbActionKind.TransferLeft)] [TestCase(960f, OrbActionKind.TransferRight)]
        public void HoldingAtEdgeDoesNotTransferButReleaseSendsExactlyOnce(float x, OrbActionKind direction)
        {
            Begin(); var end = new Vector2(x,300);
            for(int i=0;i<4;i++) Assert.That(engine.Move(1,end,Lower,1000),Is.Null);
            var decision=engine.Up(1,end,Lower,1000,null);
            Assert.That(decision?.Kind,Is.EqualTo(direction));
            Assert.That(engine.Up(1,end,Lower,1000,null),Is.Null);
            Assert.That(engine.Begin(2,Orb(),end,false,Lower,1000,Tuning),Is.False);
            engine.ResolvePending("a");
            Assert.That(engine.Move(1,end,Lower,1000),Is.Null,"An acknowledged incoming/outgoing orb cannot auto-return without a new Begin.");
        }
        [TestCase(40f)] [TestCase(960f)]
        public void CombinedCanAlsoTransferBothDirections(float x)
        { Begin(kind:OrbKind.Combined); Assert.That(engine.Up(1,new Vector2(x,300),Lower,1000,null)?.Kind,Is.EqualTo(x<500?OrbActionKind.TransferLeft:OrbActionKind.TransferRight)); }
        [TestCase(100f,900f)] [TestCase(900f,100f)]
        public void LongHorizontalDragInsideWorkspaceRemainsLocal(float start,float end)
        { Begin(start); Assert.That(engine.Move(1,new Vector2(end,300),Lower,1000),Is.Null); Assert.That(engine.Up(1,new Vector2(end,300),Lower,1000,null),Is.Null); Assert.That(engine.HasPending,Is.False); }
        [TestCase(40f)] [TestCase(960f)]
        public void CombinationHasPriorityEvenAtTheTransferEdge(float x)
        { Begin(); var end=new Vector2(x,300); var target=new OrbDropTarget(Orb("b",polarity:OrbPolarity.Yang),end); Assert.That(engine.Up(1,end,Lower,1000,new[]{target})?.Kind,Is.EqualTo(OrbActionKind.Combine)); }
        [Test]
        public void InvalidSamePolarityDropAtEdgeIsNotTurnedIntoTransfer()
        { Begin(); var end=new Vector2(40,300); Assert.That(engine.Up(1,end,Lower,1000,new[]{new OrbDropTarget(Orb("b"),end)}),Is.Null); }
        [TestCase(100f,40f,300f,300f)] [TestCase(500f,40f,50f,590f)]
        public void EdgeDoesNotBypassDistanceOrDirectionDominance(float sx,float ex,float sy,float ey)
        { Begin(sx,sy); Assert.That(engine.Up(1,new Vector2(ex,ey),Lower,1000,null),Is.Null); }
        [TestCase(220f,40f)] [TestCase(780f,960f)]
        public void ExactDistanceThresholdIsInclusive(float start,float end)
        { Begin(start); Assert.That(engine.Up(1,new Vector2(end,300),Lower,1000,null),Is.Not.Null); }
        [Test]
        public void OutgoingDirectionMustMatchReleaseEdge()
        { Begin(1); Assert.That(engine.Up(1,new Vector2(40,300),Lower,1000,null),Is.Null); }
        [Test]
        public void CrossingBattleBoundaryWhileHeldLaunchesBeforeAnEdgeRelease()
        { Begin(500,550,OrbKind.Combined); Assert.That(engine.Move(1,new Vector2(960,610),Lower,1000)?.Kind,Is.EqualTo(OrbActionKind.Launch)); Assert.That(engine.Up(1,new Vector2(960,300),Lower,1000,null),Is.Null); }
        [TestCase(-1f)] [TestCase(600f)] [TestCase(700f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void RawReleaseOutsideLowerHeightNeverTransfers(float y)
        { Begin(); Assert.That(engine.Up(1,new Vector2(960,y),Lower,1000,null),Is.Null); }
        [Test]
        public void CancelledHeldEdgeProducesNoRequest()
        { Begin(); engine.Move(1,new Vector2(40,300),Lower,1000); engine.Cancel(1); Assert.That(engine.Up(1,new Vector2(40,300),Lower,1000,null),Is.Null); }
        [Test]
        public void GeometryOffsetAndDifferentScreenWidthKeepNormalizedDistanceAndHeight()
        {
            var lower=new Rect(100,200,800,400); Assert.That(engine.Begin(1,Orb(),new Vector2(600,400),false,lower,1200,Tuning),Is.True);
            var result=engine.Up(1,new Vector2(130,400),lower,1200,null);
            Assert.That(result?.Kind,Is.EqualTo(OrbActionKind.TransferLeft)); Assert.That(result?.NormalizedPosition.y,Is.EqualTo(.5f));
        }
    }
}
