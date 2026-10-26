using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>Visual metadata is bounded and transactionally restored, never gameplay authority.</summary>
    public sealed class LabPaintVisualStateTests
    {
        [TestCase(0, 448, 449u, "b9539be08d5a54357870bc8426e954df7671990fc61b485a2469c3910b016f69")]
        [TestCase(1, 449, 450u, "ca46e9b7533eff499e962b190aafdfa09cc6c19afdf6c7ce2e96670e19e537e6")]
        [TestCase(2, 448, 449u, "e58372a9e1826f85d217aa645ecc0afe9e7a10a4b4b60f4a771213ada1b13b03")]
        [TestCase(3, 460, 461u, "c300b2dde57365642d6c34c7b5d2114d61d8af0b407f6cf8fbda40064ef06cd1")]
        public void CapturingHintsPreservesThePre299AuthoritativeBytesAndCounters(int pattern, int covered,
            uint revision, string expectedHash)
        {
            // Captured on main 41f15b7c before metadata hooks were installed. This compares
            // against the previous rules, rather than two copies of the modified algorithm.
            var surface = new LabPaintSurface(new LabPaintGrowthSettings
            { Resolution = 0, InitialPercent = 2f, RepeatPercent = 1.5f, Pattern = (LabPaintPattern)pattern }, 123u);
            var colors = new[] { LabPaintPalette.ColorFor(LabOrbElement.Fire),
                LabPaintPalette.ColorFor(LabOrbElement.Water), LabPaintPalette.ColorFor(LabOrbElement.Wood) };
            for (int i = 0; i < 12; i++)
            {
                Vector3 direction = i < 4 ? Vector3.forward
                    : new Vector3(Mathf.Cos(i * .73f), Mathf.Sin(i * .31f), Mathf.Sin(i * .73f)).normalized;
                Assert.That(surface.RegisterHit(direction, colors[i % 3], (ulong)(i % 3 + 1), i % 3 + 1), Is.True);
                surface.Advance(.21d + (i % 3) * .31d);
            }
            surface.Advance(9d);
            Assert.That(surface.PaintCount, Is.EqualTo(12));
            Assert.That(surface.CoveredCount, Is.EqualTo(covered));
            Assert.That(surface.Revision, Is.EqualTo(revision));
            Assert.That(PixelHash(surface), Is.EqualTo(expectedHash));
        }

        [Test]
        public void InitialAndFundedRepeatHintsHaveStableTangentsAndCopiesAreIndependent()
        {
            var source = Surface();
            source.RegisterHit(Vector3.forward, Color.red, 10, 1);
            var initial = source.CopyVisualStamps();
            Assert.That(initial, Has.Length.EqualTo(1));
            Assert.That(initial[0].center, Is.EqualTo(Vector3.forward));
            Assert.That(initial[0].radius, Is.GreaterThan(0f));
            Assert.That(Vector3.Dot(initial[0].right, initial[0].center), Is.Zero.Within(.001f));
            Assert.That(initial[0].Up.sqrMagnitude, Is.EqualTo(1f).Within(.001f));
            Assert.That(initial[0].painterSlot, Is.EqualTo(1));
            uint version = source.VisualVersion;
            source.RegisterHit(Vector3.forward, Color.blue, 20, 2);
            Assert.That(source.CopyVisualStamps(), Is.EqualTo(initial), "An unspent job is not an already painted splash.");
            source.Advance(1d);
            var grown = source.CopyVisualStamps();
            Assert.That(grown, Has.Length.EqualTo(2));
            Assert.That(grown[1].painterSlot, Is.EqualTo(2));
            Assert.That(grown[1].color, Is.EqualTo(LabPaintSurface.Opaque(Color.blue)));
            Assert.That(source.VisualVersion, Is.GreaterThan(version));
            source.Advance(.01d);
            Assert.That(source.CopyVisualStamps(), Has.Length.EqualTo(2), "A growth job records once, not for each emitted cell.");
            grown[0].center = Vector3.left;
            Assert.That(source.CopyVisualStamps()[0].center, Is.EqualTo(Vector3.forward));
            Assert.That(LabPaintVisualStamp.TryDecode(LabPaintVisualStamp.Encode(initial), out var decoded), Is.True);
            Assert.That(decoded, Is.EqualTo(initial));
        }

        [Test]
        public void ReplacingHintsIsDefensiveAndInvalidHintsCannotPartiallyReplaceAnyState()
        {
            var source = Surface(); source.RegisterHit(Vector3.forward, Color.red, 1, 1);
            var hints = source.CopyVisualStamps();
            var replica = Surface();
            Color32[] pixels = Pixels(source); byte[] painters = Painters(source);
            Assert.That(replica.TryReplacePixels(source.Columns, source.Rows, source.Revision, pixels, painters,
                source.PaintCount, hints), Is.True);
            uint version = replica.VisualVersion;
            string hash = PixelHash(replica);
            Assert.That(replica.ReplaceVisualStamps(hints), Is.True);
            Assert.That(replica.VisualVersion, Is.EqualTo(version), "An identical checkpoint does not invalidate the renderer cache.");
            hints[0].radius = float.NaN;
            Assert.That(replica.ReplaceVisualStamps(hints), Is.False);
            var empty = new Color32[pixels.Length];
            Assert.That(replica.TryReplacePixels(source.Columns, source.Rows, source.Revision + 1, empty,
                new byte[empty.Length], 0, hints), Is.False);
            Assert.That(PixelHash(replica), Is.EqualTo(hash));
            Assert.That(replica.VisualVersion, Is.EqualTo(version));
            Assert.That(replica.CopyVisualStamps()[0].radius, Is.Not.NaN);
        }

        [Test]
        public void MetadataIdentityPreservesEvenSignedZeroWireBitsAndDoesNotUseVectorEpsilon()
        {
            var source = Surface(); source.RegisterHit(Vector3.forward, Color.red, 1, 1);
            var hints = source.CopyVisualStamps(); uint before = source.VisualVersion;
            hints[0].center.x = BitConverter.Int32BitsToSingle(unchecked((int)0x80000000));
            byte[] requested = LabPaintVisualStamp.Encode(hints);
            Assert.That(source.ReplaceVisualStamps(hints), Is.True);
            Assert.That(source.VisualVersion, Is.GreaterThan(before));
            Assert.That(LabPaintVisualStamp.Encode(source.CopyVisualStamps()), Is.EqualTo(requested),
                "An unchanged-metadata delta validates the exact serialized hint digest.");
        }

        [Test]
        public void LongTerritoryPlayUsesABoundedWindowAndResetClearsTheOldRound()
        {
            var source = Surface(.1f);
            for (int i = 0; i < 150; i++)
                Assert.That(source.RegisterTerritoryHit(Vector3.forward, i % 2 == 0 ? Color.red : Color.blue,
                    (ulong)(i % 2 + 1), i % 2 + 1, 1f, out _, out _), Is.True);
            var stamps = source.CopyVisualStamps();
            Assert.That(stamps, Has.Length.EqualTo(LabPaintVisualStamp.MaximumCount));
            Assert.That(stamps[0].sequence, Is.EqualTo(23));
            Assert.That(stamps[stamps.Length - 1].sequence, Is.EqualTo(150));
            Assert.That(LabPaintVisualStamp.Encode(stamps), Has.Length.EqualTo(LabPaintVisualStamp.MaximumBytes));
            uint before = source.VisualVersion;
            source.Clear();
            Assert.That(source.CopyVisualStamps(), Is.Empty);
            Assert.That(source.VisualVersion, Is.GreaterThan(before));
            Assert.That(source.CoveredCount, Is.Zero);
        }

        [TestCase("nan")]
        [TestCase("zero-direction")]
        [TestCase("parallel-tangent")]
        [TestCase("radius")]
        [TestCase("painter")]
        [TestCase("pattern")]
        [TestCase("sequence")]
        [TestCase("count")]
        public void InvalidDescriptorListsAreRejectedBeforeInstallation(string corruption)
        {
            var source = Surface(); source.RegisterHit(Vector3.forward, Color.red, 1, 1);
            var hints = source.CopyVisualStamps(); uint version = source.VisualVersion;
            switch (corruption)
            {
                case "nan": hints[0].center.x = float.NaN; break;
                case "zero-direction": hints[0].center = Vector3.zero; break;
                case "parallel-tangent": hints[0].right = hints[0].center; break;
                case "radius": hints[0].radius = 0f; break;
                case "painter": hints[0].painterSlot = 6; break;
                case "pattern": hints[0].pattern = (LabPaintPattern)99; break;
                case "sequence": hints = new[] { hints[0], hints[0] }; break;
                case "count": hints = new LabPaintVisualStamp[LabPaintVisualStamp.MaximumCount + 1]; break;
            }
            Assert.That(source.ReplaceVisualStamps(hints), Is.False);
            Assert.That(source.VisualVersion, Is.EqualTo(version));
            Assert.That(source.CopyVisualStamps(), Has.Length.EqualTo(1));
        }

        [Test]
        public void ColorOwnershipAndHintsRemainInvisibleUntilTheWholeTransactionArrives()
        {
            var source = Surface(); source.RegisterHit(Vector3.forward, Color.red, 1, 1);
            var destination = Surface(); var receiver = new LabPaintReceiver(destination);
            var frames = new LabPaintPublisher().Publish(source, "visual-round", 1, 1);
            Assert.That(frames[0].visualBytes, Is.GreaterThan(0));
            foreach (var frame in frames.Skip(1).Reverse())
                Assert.That(receiver.Receive(frame), Is.EqualTo(LabPaintReceive.Pending));
            Assert.That(destination.CopyVisualStamps(), Is.Empty);
            Assert.That(destination.CoveredCount, Is.Zero);
            Assert.That(receiver.Receive(frames[0]), Is.EqualTo(LabPaintReceive.Applied));
            AssertEquivalent(source, destination);
            Assert.That(receiver.Receive(frames[0]), Is.EqualTo(LabPaintReceive.Rejected));
        }

        [Test]
        public void DryingDeltasReuseHintsAndAMissingMapDeltaRecoversFromTheFrozenCheckpoint()
        {
            var source = new LabPaintSurface(new LabPaintGrowthSettings
            { Resolution = 0, InitialPercent = 10f, DryingEnabled = true, DryingDelaySeconds = 1f,
                DryingPercentPerSecond = 2f }, 123u);
            source.RegisterHit(Vector3.forward, Color.red, 1, 1);
            var publisher = new LabPaintPublisher(); var replica = Surface(); var receiver = new LabPaintReceiver(replica);
            Apply(receiver, publisher.Publish(source, "dry", 1, 1));
            int initialCount = source.CoveredCount;
            source.Advance(2d);
            Assert.That(source.CoveredCount, Is.LessThan(initialCount));
            var lost = publisher.Publish(source, "dry", 1, 2);
            Assert.That(lost[0].visualBytes, Is.EqualTo(-1), "A changing erase mask does not resend the same brush history.");
            source.Advance(1d);
            var next = publisher.Publish(source, "dry", 1, 3);
            Assert.That(Apply(receiver, next), Is.EqualTo(LabPaintReceive.NeedCheckpoint));
            var checkpoint = publisher.Checkpoint();
            source.Advance(1d);
            Assert.That(Apply(receiver, checkpoint), Is.EqualTo(LabPaintReceive.Applied));
            Assert.That(replica.CopyVisualStamps(), Is.EqualTo(source.CopyVisualStamps()));
            Assert.That(replica.CoveredCount, Is.GreaterThan(source.CoveredCount), "Checkpoint pixels stay frozen at their published revision.");
        }

        [Test]
        public void MaximumVisualCheckpointFitsTheExistingBoundedPaintChannel()
        {
            var source = new LabPaintSurface(new LabPaintGrowthSettings { Resolution = 2 }, 1);
            var hints = Enumerable.Range(1, LabPaintVisualStamp.MaximumCount).Select(i => new LabPaintVisualStamp
            { sequence = (uint)i, center = Vector3.forward, right = Vector3.right, radius = .1f,
                color = new Color32(100, 110, 120, 255), painterSlot = 1, pattern = LabPaintPattern.Splatter }).ToArray();
            Assert.That(source.ReplaceVisualStamps(hints), Is.True);
            var frames = new LabPaintPublisher().Publish(source, "max", 1, 1);
            Assert.That(frames, Has.Length.EqualTo(LabPaintPublisher.MaximumFragmentCount));
            Assert.That(frames.All(f => f.data.Length <= LabPaintPublisher.FragmentCharacters), Is.True);
            var receiver = new LabPaintReceiver(Surface());
            Assert.That(Apply(receiver, frames), Is.EqualTo(LabPaintReceive.Applied));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CorruptHintDigestOrGeometryCannotCommitColorOwnershipOrMetadata(bool geometry)
        {
            var source = Surface(); source.RegisterHit(Vector3.forward, Color.red, 1, 1);
            var publisher = new LabPaintPublisher(); var frames = publisher.Publish(source, "bad", 1, 1);
            if (geometry)
            {
                byte[] payload = Convert.FromBase64String(string.Concat(frames.Select(f => f.data)));
                int start = payload.Length - frames[0].visualBytes;
                Array.Copy(BitConverter.GetBytes(float.NaN), 0, payload, start + 4, 4);
                var visual = new byte[frames[0].visualBytes]; Array.Copy(payload, start, visual, 0, visual.Length);
                string encoded = Convert.ToBase64String(payload);
                foreach (var f in frames)
                {
                    f.visualHash = LabPaintVisualStamp.Hash(visual);
                    f.data = encoded.Substring(f.part * LabPaintPublisher.FragmentCharacters,
                        Math.Min(LabPaintPublisher.FragmentCharacters, encoded.Length - f.part * LabPaintPublisher.FragmentCharacters));
                }
            }
            else foreach (var f in frames) f.visualHash = new string('f', 64);
            var replica = Surface(); replica.RegisterHit(Vector3.back, Color.blue, 2, 2);
            string before = PixelHash(replica); var oldHints = replica.CopyVisualStamps();
            var receiver = new LabPaintReceiver(replica);
            Assert.That(Apply(receiver, frames), Is.EqualTo(LabPaintReceive.NeedCheckpoint));
            Assert.That(PixelHash(replica), Is.EqualTo(before));
            Assert.That(replica.CopyVisualStamps(), Is.EqualTo(oldHints));
            Assert.That(Apply(receiver, publisher.Checkpoint()), Is.EqualTo(LabPaintReceive.Applied));
            AssertEquivalent(source, replica);
        }

        private static LabPaintSurface Surface(float initial = 2f) => new LabPaintSurface(new LabPaintGrowthSettings
        { Resolution = 0, InitialPercent = initial, RepeatPercent = 1.5f, Pattern = LabPaintPattern.Splatter }, 123u);
        private static Color32[] Pixels(LabPaintSurface surface)
        { var p = new Color32[surface.CellCount]; surface.CopyPixels(p); return p; }
        private static byte[] Painters(LabPaintSurface surface)
        { var p = new byte[surface.CellCount]; surface.CopyPainterSlots(p); return p; }
        private static string PixelHash(LabPaintSurface surface) => LabPaintPublisher.Hash(Pixels(surface), Painters(surface));
        private static LabPaintReceive Apply(LabPaintReceiver receiver, LabPaintFrame[] frames)
        {
            LabPaintReceive result = LabPaintReceive.Rejected;
            foreach (var f in frames) result = receiver.Receive(f);
            return result;
        }
        private static void AssertEquivalent(LabPaintSurface expected, LabPaintSurface actual)
        {
            Assert.That(PixelHash(actual), Is.EqualTo(PixelHash(expected)));
            Assert.That(actual.CoveredCount, Is.EqualTo(expected.CoveredCount));
            Assert.That(actual.Revision, Is.EqualTo(expected.Revision));
            Assert.That(actual.PaintCount, Is.EqualTo(expected.PaintCount));
            Assert.That(actual.CopyVisualStamps(), Is.EqualTo(expected.CopyVisualStamps()));
        }
    }
}
