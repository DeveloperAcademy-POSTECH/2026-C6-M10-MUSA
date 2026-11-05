using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabPaintImpactTests
    {
        [Test]
        public void NormalSpeedCalibrationIgnoresSkimmingAndClampsAtTheArtRange()
        {
            Assert.That(LabPaintImpact.Normalize(Vector3.back * 5f, Vector3.forward), Is.EqualTo(.25f));
            Assert.That(LabPaintImpact.Normalize(Vector3.back * 20f, Vector3.forward), Is.EqualTo(1f));
            Assert.That(LabPaintImpact.Normalize(Vector3.back * 200f, Vector3.forward), Is.EqualTo(1f));
            Assert.That(LabPaintImpact.Normalize(new Vector3(50, 0, -10), Vector3.forward), Is.EqualTo(.5f));
            Assert.That(LabPaintImpact.Normalize(Vector3.right * 50, Vector3.forward), Is.Zero);
            Assert.That(LabPaintImpact.Normalize(Vector3.forward * 50, Vector3.forward), Is.Zero);
            Assert.That(LabPaintImpact.Normalize(new Vector3(float.NaN,0,0), Vector3.forward), Is.EqualTo(.5f));
            Assert.That(LabPaintImpact.Normalize(Vector3.back * 20, Vector3.zero), Is.EqualTo(.5f));
        }

        [Test]
        public void DescriptorKeepsLegacySizeAndDefaultReservedBytesWhileCarryingImpact()
        {
            var stamp = Stamp();
            byte[] legacy = LabPaintVisualStamp.Encode(new[] { stamp });
            Assert.That(legacy.Length, Is.EqualTo(44));
            Assert.That(legacy[38], Is.Zero); Assert.That(legacy[39], Is.Zero);
            Assert.That(stamp.ImpactStrength, Is.EqualTo(.5f));
            stamp.impact01 = LabPaintVisualStamp.EncodeImpactStrength(1f);
            byte[] strong = LabPaintVisualStamp.Encode(new[] { stamp });
            Assert.That(strong.Where((value,index) => value != legacy[index]).Count(), Is.EqualTo(1));
            Assert.That(strong[38], Is.EqualTo(255));
            Assert.That(LabPaintVisualStamp.TryDecode(strong, out var decoded), Is.True);
            Assert.That(decoded[0].ImpactStrength, Is.EqualTo(1f));
            strong[39] = 1;
            Assert.That(LabPaintVisualStamp.TryDecode(strong, out _), Is.False, "The remaining reserved byte is still validated.");
            Assert.That(LabPaintVisualStamp.EncodeImpactStrength(.5f), Is.Zero);
            Assert.That(LabPaintVisualStamp.EncodeImpactStrength(float.NaN), Is.Zero);
        }

        [Test]
        public void ImpactChangesHintsAndGrowthHistoryWithoutChangingCoverageOwnershipOrBudgets()
        {
            var rules = new LabPaintGrowthSettings { Resolution = 0, InitialPercent = 2f, RepeatPercent = 1.5f, Pattern = LabPaintPattern.Splatter };
            var weak = new LabPaintSurface(rules,123u); var strong = new LabPaintSurface(rules,123u);
            for (int i=0; i<5; i++)
            {
                Vector3 direction = i < 3 ? Vector3.forward : Quaternion.AngleAxis(i*18,Vector3.up)*Vector3.forward;
                Assert.That(weak.RegisterHit(direction,Color.blue,7,2,1f,0f), Is.True);
                Assert.That(strong.RegisterHit(direction,Color.blue,7,2,1f,1f), Is.True);
                weak.Advance(.35d);strong.Advance(.35d);
            }
            weak.Advance(10d);strong.Advance(10d);
            var a = new Color32[weak.CellCount];var b = new Color32[strong.CellCount]; weak.CopyPixels(a);strong.CopyPixels(b);
            var pa = new byte[weak.CellCount];var pb = new byte[strong.CellCount]; weak.CopyPainterSlots(pa);strong.CopyPainterSlots(pb);
            Assert.That(a, Is.EqualTo(b));Assert.That(pa, Is.EqualTo(pb));
            Assert.That(weak.CoveredCount, Is.EqualTo(strong.CoveredCount));
            Assert.That(weak.PendingCoverage, Is.EqualTo(strong.PendingCoverage));
            Assert.That(weak.Revision, Is.EqualTo(strong.Revision));Assert.That(weak.PaintCount, Is.EqualTo(strong.PaintCount));
            Assert.That(weak.CopyVisualStamps().All(s=>s.ImpactStrength==0f), Is.True);
            Assert.That(strong.CopyVisualStamps().All(s=>s.ImpactStrength==1f), Is.True);
            uint old = weak.VisualVersion;
            var replacement = weak.CopyVisualStamps(); replacement[0].impact01=255;
            Assert.That(weak.ReplaceVisualStamps(replacement), Is.True);
            Assert.That(weak.VisualVersion, Is.GreaterThan(old), "Changing appearance-only strength still invalidates the render hint digest.");
        }

        [Test]
        public void ImpactSurvivesJournalFlushLookupAndDifferentReplayBatchSizes()
        {
            string directory=Path.Combine(Path.GetTempPath(),"c6-impact-"+Guid.NewGuid().ToString("N"));
            try
            {
                var surface=new LabPaintSurface(new LabPaintGrowthSettings {Resolution=0,InitialPercent=2f,RepeatPercent=1.5f},123u);
                using(var journal=new LabPaintArtJournal(Path.Combine(directory,"art"),"impact",surface.Columns,surface.Rows,"impact-test"))
                {
                    Assert.That(surface.AttachArtJournal(journal), Is.True);
                    surface.RegisterHit(Vector3.forward,Color.red,1,1,1f,.2f);
                    surface.RegisterHit(Vector3.forward,Color.blue,2,2,1f,.9f);
                    surface.Advance(2d);
                    Assert.That(journal.Flush(), Is.True,journal.Error);
                    var all=new List<LabPaintArtOperation>();
                    Assert.That(journal.ReadOperations(0,4096,all),Is.True,journal.Error);
                    var strokes=all.Where(o=>o.Kind==LabPaintArtOperationKind.StrokeBegin).ToArray();
                    Assert.That(strokes.Length,Is.GreaterThanOrEqualTo(2));
                    Assert.That(strokes[0].Stamp.ImpactStrength,Is.EqualTo(.2f).Within(1f/254f));
                    Assert.That(strokes.Any(o=>Mathf.Abs(o.Stamp.ImpactStrength-.9f)<1f/254f),Is.True);
                    foreach(var stroke in strokes)
                    {
                        Assert.That(journal.TryGetStroke(stroke.StrokeId,out var located),Is.True,journal.Error);
                        Assert.That(located.impact01,Is.EqualTo(stroke.Stamp.impact01));
                    }
                    var tiny=new List<LabPaintArtOperation>(); var batch=new List<LabPaintArtOperation>(); ulong through=0;
                    while(through<journal.LastSequence)
                    {Assert.That(journal.ReadOperations(through,1,batch),Is.True);Assert.That(batch,Is.Not.Empty);tiny.AddRange(batch);through=batch.Last().Sequence;}
                    Assert.That(tiny.Where(o=>o.Kind==LabPaintArtOperationKind.StrokeBegin).Select(o=>o.Stamp.impact01),Is.EqualTo(strokes.Select(o=>o.Stamp.impact01)));
                }
            }
            finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }

        private static LabPaintVisualStamp Stamp()=>new LabPaintVisualStamp
        {sequence=1,center=Vector3.forward,right=Vector3.right,radius=.2f,color=new Color32(20,40,60,255),painterSlot=2,pattern=LabPaintPattern.Splatter,seed=123};
    }
}
