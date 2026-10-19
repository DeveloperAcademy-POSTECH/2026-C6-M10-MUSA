using System;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>Transaction fixtures verify attribution and pigment commit together, not live transport.</summary>
    public sealed class LabPainterReplicationTests
    {
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void SamePigmentKeepsDistinctPainterIdentityAcrossFullAndRecoveryCheckpoints(int player)
        {
            var source = Surface();
            source.RegisterHit(Vector3.forward, Color.red, 123ul, player);
            var neutral = Surface();
            neutral.RegisterHit(Vector3.forward, Color.red, 123ul);
            Assert.That(Pixels(source), Is.EqualTo(Pixels(neutral)));
            Assert.That(LabPaintPublisher.Hash(Pixels(source), Painters(source)),
                Is.Not.EqualTo(LabPaintPublisher.Hash(Pixels(neutral), Painters(neutral))));
            Assert.That(Painters(source).Where(p => p != 0).Distinct(), Is.EqualTo(new[] { (byte)player }));

            var replica = Surface(); var receiver = new LabPaintReceiver(replica);
            var publisher = new LabPaintPublisher();
            Assert.That(Receive(receiver, publisher.Publish(source, "painters", 1, 1)), Is.EqualTo(LabPaintReceive.Applied));
            AssertSame(source, replica);
            source.Clear(); // The publisher's checkpoint is an isolated copy, not a live source reference.
            var recovered = Surface();
            Assert.That(Receive(new LabPaintReceiver(recovered), publisher.Checkpoint()), Is.EqualTo(LabPaintReceive.Applied));
            AssertSame(replica, recovered);
        }

        [Test]
        public void OutOfOrderFragmentsExposeNeitherNewColorsNorNewPaintersUntilTheLastPart()
        {
            var source = Surface();
            source.RegisterHit(Vector3.forward, Color.red, 90ul, 2);
            source.RegisterHit(Vector3.back, Color.red, 91ul, 4);
            var replica = Surface();
            replica.RegisterHit(Vector3.up, Color.blue, 92ul, 5);
            var oldColors = Pixels(replica); var oldPainters = Painters(replica);
            var receiver = new LabPaintReceiver(replica);
            var frames = new LabPaintPublisher().Publish(source, "painters", 1, 1).Reverse().ToArray();
            for (int i = 0; i < frames.Length - 1; i++)
            {
                Assert.That(receiver.Receive(frames[i]), Is.EqualTo(LabPaintReceive.Pending));
                Assert.That(Pixels(replica), Is.EqualTo(oldColors));
                Assert.That(Painters(replica), Is.EqualTo(oldPainters));
            }
            Assert.That(receiver.Receive(frames.Last()), Is.EqualTo(LabPaintReceive.Applied));
            AssertSame(source, replica);
        }

        [Test]
        public void AttributionOnlyChangeIsIncludedInDeltaAndHashEvenWithIdenticalPigmentAndCoverage()
        {
            var source = Surface(); var pixels = Pixels(source); var painters = Painters(source);
            pixels[91] = new Color32(20, 110, 55, 255); painters[91] = 1;
            Assert.That(source.TryReplacePixels(64, 32, 1, pixels, painters, 1), Is.True);
            var publisher = new LabPaintPublisher(); var replica = Surface();
            var receiver = new LabPaintReceiver(replica);
            Receive(receiver, publisher.Publish(source, "painters", 1, 1));
            string firstHash = LabPaintPublisher.Hash(Pixels(replica), Painters(replica));
            painters[91] = 5;
            Assert.That(source.TryReplacePixels(64, 32, 2, pixels, painters, 1), Is.True);
            var delta = publisher.Publish(source, "painters", 1, 2);
            Assert.That(delta.All(frame => !frame.full), Is.True);
            Assert.That(Convert.FromBase64String(string.Concat(delta.Select(frame => frame.data))).Length, Is.EqualTo(7));
            Assert.That(Receive(receiver, delta), Is.EqualTo(LabPaintReceive.Applied));
            Assert.That(replica.GetPainterSlot(91), Is.EqualTo(5));
            Assert.That(replica.CoveredCount, Is.EqualTo(1));
            Assert.That(Pixels(replica), Is.EqualTo(pixels));
            Assert.That(LabPaintPublisher.Hash(Pixels(replica), Painters(replica)), Is.Not.EqualTo(firstHash));
        }

        [TestCase("tampered-owner")] [TestCase("invalid-owner")] [TestCase("unpainted-owner")]
        public void CorruptAttributionLeavesThePreviousPairUntouchedAndCheckpointRecovers(string corruption)
        {
            var source = Surface(); source.RegisterHit(Vector3.forward, Color.red, 123ul, 3);
            var publisher = new LabPaintPublisher();
            var frames = publisher.Publish(source, "painters", 1, 1);
            var payload = Convert.FromBase64String(string.Concat(frames.Select(frame => frame.data)));
            int painted = Array.FindIndex(Pixels(source), p => p.a == 255);
            if (corruption == "unpainted-owner")
                payload[Array.FindIndex(Pixels(source), p => p.a == 0) * 5 + 4] = 1;
            else payload[painted * 5 + 4] = corruption == "invalid-owner" ? (byte)6 : (byte)2;
            string encoded = Convert.ToBase64String(payload), hash;
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(payload)).Replace("-", "").ToLowerInvariant();
            var changed = frames.Select(f => JsonUtility.FromJson<LabPaintFrame>(JsonUtility.ToJson(f))).ToArray();
            for (int i = 0; i < changed.Length; i++)
            {
                changed[i].data = encoded.Substring(i * LabPaintPublisher.FragmentCharacters,
                    Math.Min(LabPaintPublisher.FragmentCharacters, encoded.Length - i * LabPaintPublisher.FragmentCharacters));
                if (corruption != "tampered-owner") changed[i].hash = hash;
            }
            var replica = Surface(); replica.RegisterHit(Vector3.back, Color.blue, 456ul, 5);
            var beforeColors = Pixels(replica); var beforeOwners = Painters(replica); uint revision = replica.Revision;
            var receiver = new LabPaintReceiver(replica);
            Assert.That(Receive(receiver, changed), Is.EqualTo(LabPaintReceive.NeedCheckpoint));
            Assert.That(Pixels(replica), Is.EqualTo(beforeColors));
            Assert.That(Painters(replica), Is.EqualTo(beforeOwners));
            Assert.That(replica.Revision, Is.EqualTo(revision));
            Assert.That(Receive(receiver, publisher.Checkpoint()), Is.EqualTo(LabPaintReceive.Applied));
            AssertSame(source, replica);
        }

        private static LabPaintSurface Surface() => new LabPaintSurface(new LabPaintGrowthSettings
            { Resolution = 0, Pattern = LabPaintPattern.Legacy }, 42u);
        private static Color32[] Pixels(LabPaintSurface s) { var a = new Color32[s.CellCount]; s.CopyPixels(a); return a; }
        private static byte[] Painters(LabPaintSurface s) { var a = new byte[s.CellCount]; s.CopyPainterSlots(a); return a; }
        private static LabPaintReceive Receive(LabPaintReceiver receiver, LabPaintFrame[] frames)
        { LabPaintReceive result = LabPaintReceive.Rejected; foreach (var frame in frames) result = receiver.Receive(frame); return result; }
        private static void AssertSame(LabPaintSurface a, LabPaintSurface b)
        {
            Assert.That(Pixels(b), Is.EqualTo(Pixels(a)));
            Assert.That(Painters(b), Is.EqualTo(Painters(a)));
            Assert.That(b.Revision, Is.EqualTo(a.Revision));
            Assert.That(b.CoveredCount, Is.EqualTo(a.CoveredCount));
            Assert.That(b.PaintCount, Is.EqualTo(a.PaintCount));
        }
    }
}
