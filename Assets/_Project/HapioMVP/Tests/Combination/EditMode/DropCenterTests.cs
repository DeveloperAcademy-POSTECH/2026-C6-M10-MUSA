using C6.Prototype.Orbs;
using NUnit.Framework;
using UnityEngine;
namespace C6.Prototype.Combination.Tests
{
    public sealed class DropCenterTests
    {
        static OrbRecord Raw(string id,OrbPolarity polarity) => new OrbRecord(id,OrbKind.Raw,polarity,0,OrbAuthorityState.Idle,new Vector2(.5f,.5f),EntrySide.None,0);
        [Test]
        public void VisualCenterControlsDropWhileRawPointerStillControlsSwipe()
        {
            var lower = new Rect(0,0,1000,600); var tuning = new GestureTuning(.18f,1.25f,.08f,.18f);
            var target = new [] {new OrbDropTarget(Raw("b",OrbPolarity.Yang),new Vector2(620,250))};
            var engine = new OrbGestureEngine();
            Assert.That(engine.Begin(1,Raw("a",OrbPolarity.Yin),new Vector2(500,250),false,lower,1000,tuning),Is.True);
            Assert.That(engine.Up(1,new Vector2(520,250),lower,1000,target,new Vector2(550,250))?.Kind,Is.EqualTo(OrbActionKind.Combine));
            engine = new OrbGestureEngine();
            engine.Begin(1,Raw("a",OrbPolarity.Yin),new Vector2(500,250),false,lower,1000,tuning);
            Assert.That(engine.Up(1,new Vector2(700,250),lower,1000,target,new Vector2(620,250))?.Kind,Is.EqualTo(OrbActionKind.TransferRight));
        }
        [Test]
        public void InvalidDisplayCenterCannotSelectAnyTarget()
        {
            var lower = new Rect(0,0,1000,600);var engine=new OrbGestureEngine();
            engine.Begin(1,Raw("a",OrbPolarity.Yin),new Vector2(500,250),false,lower,1000,new GestureTuning(.18f,1.25f,.08f,.18f));
            Assert.That(engine.Up(1,new Vector2(510,250),lower,1000,new [] {new OrbDropTarget(Raw("b",OrbPolarity.Yang),new Vector2(510,250))},new Vector2(float.NaN,0)),Is.Null);
            Assert.That(engine.HasActivePointer,Is.False);
            Assert.That(engine.HasPending,Is.False);
        }
    }
}
