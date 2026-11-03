using System;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>Guest paint transactions applied in reused buffers, with the same results as before (#449).</summary>
    public sealed class LabPaintReceiveInPlaceTests
    {
        private const string Session = "in-place";

        [TestCase(0, 5)]
        [TestCase(1, 35)]
        [TestCase(2, 20480)]
        [TestCase(2, 400000)]
        public void TheStreamingHashEqualsTheOneShotHash(int resolution, int chunkBytes)
        {
            LabPaintSurface host = Host(resolution, 6);
            Color32[] pixels = Pixels(host); byte[] painters = Painters(host);
            using (SHA256 sha = SHA256.Create())
            {
                string expected = LabPaintPublisher.Hash(pixels, painters);
                Assert.That(LabPaintPublisher.Hash(pixels, painters, sha, new byte[chunkBytes]), Is.EqualTo(expected));
                Assert.That(LabPaintPublisher.Hash(pixels, painters, sha, new byte[chunkBytes]), Is.EqualTo(expected), "The instance is reusable.");
                Assert.Throws<ArgumentException>(() => LabPaintPublisher.Hash(pixels, painters, sha, new byte[4]));
            }
        }

        [Test]
        public void ADeltaThatKeepsTheStampsAllocatesFarLessThanOneMap()
        {
            var host = new ScriptedHost(2, 24);
            var publisher = new LabPaintPublisher();
            var receiver = new LabPaintReceiver(new LabPaintSurface());
            long epoch = 1;
            Assert.That(Apply(receiver, publisher.Publish(host.Surface, Session, 1, epoch++)), Is.EqualTo(LabPaintReceive.Applied));
            Assert.That(Apply(receiver, host.KeepStampsDelta(publisher, ref epoch, 40)), Is.EqualTo(LabPaintReceive.Applied), "Warm-up.");
            long allocated = long.MaxValue;
            for (int attempt = 0; attempt < 5 && allocated == long.MaxValue; attempt++)
            {
                LabPaintFrame[] delta = host.KeepStampsDelta(publisher, ref epoch, 40);
                int collections = GC.CollectionCount(0);
                long before = GC.GetTotalMemory(false);
                LabPaintReceive result = Apply(receiver, delta);
                long after = GC.GetTotalMemory(false);
                Assert.That(result, Is.EqualTo(LabPaintReceive.Applied));
                if (GC.CollectionCount(0) == collections) allocated = after - before;
            }
            Assert.That(allocated, Is.Not.EqualTo(long.MaxValue), "A collection ran during every attempt.");
            TestContext.WriteLine("KEEP_STAMPS_DELTA_ALLOCATED bytes=" + allocated);
            // Before #449 a delta like this allocated about 535 KB (two map copies, cell bytes for SHA, stamps).
            Assert.That(allocated, Is.LessThan(64 * 1024));
        }

        [Test]
        public void ACopyIntoASameSizeDisplayAllocatesAlmostNothing()
        {
            LabPaintSurface host = Host(2, 8);
            var display = new LabPaintSurface();
            Assert.That(display.TryCopyReplicaFrom(host, 8), Is.True);
            Hit(host, 77);
            Assert.That(display.TryCopyReplicaFrom(host, 9), Is.True, "New stamps: copied once.");
            long allocated = long.MaxValue;
            for (int attempt = 0; attempt < 5 && allocated == long.MaxValue; attempt++)
            {
                int collections = GC.CollectionCount(0);
                long before = GC.GetTotalMemory(false);
                Assert.That(display.TryCopyReplicaFrom(host, 9), Is.True);
                long after = GC.GetTotalMemory(false);
                if (GC.CollectionCount(0) == collections) allocated = after - before;
            }
            TestContext.WriteLine("DISPLAY_COPY_ALLOCATED bytes=" + allocated);
            Assert.That(allocated, Is.LessThan(8 * 1024), "Before #449 the display copy allocated about 340 KB.");
            Assert.That(Pixels(display), Is.EqualTo(Pixels(host)));
        }

        [TestCase("covered")]
        [TestCase("invalid-cell")]
        [TestCase("hash")]
        public void ARejectedDeltaLeavesTheReplicaAndItsBuffersUsable(string mutation)
        {
            var host = new ScriptedHost(1, 8);
            var publisher = new LabPaintPublisher();
            var replica = new LabPaintSurface();
            var receiver = new LabPaintReceiver(replica);
            long epoch = 1;
            Assert.That(Apply(receiver, publisher.Publish(host.Surface, Session, 1, epoch++)), Is.EqualTo(LabPaintReceive.Applied));
            Color32[] before = Pixels(replica); uint revision = replica.Revision; int covered = replica.CoveredCount;

            LabPaintFrame[] delta = host.KeepStampsDelta(publisher, ref epoch, 5);
            LabPaintFrame[] tampered;
            if (mutation == "covered") tampered = Rewrite(delta, frame => frame.coveredCount += 1);
            else if (mutation == "hash") tampered = Rewrite(delta, frame => frame.hash = new string('0', 64));
            else tampered = InvalidCellWithMatchingHash(delta, before, Painters(replica));
            Assert.That(Apply(receiver, tampered), Is.EqualTo(LabPaintReceive.NeedCheckpoint));
            Assert.That(replica.Revision, Is.EqualTo(revision));
            Assert.That(replica.CoveredCount, Is.EqualTo(covered));
            Assert.That(Pixels(replica), Is.EqualTo(before), "Nothing of the rejected delta reached the surface.");

            // The Host answers with a checkpoint; the reused buffers still produce the exact map.
            Assert.That(Apply(receiver, publisher.Checkpoint()), Is.EqualTo(LabPaintReceive.Applied));
            Assert.That(Pixels(replica), Is.EqualTo(Pixels(host.Surface)));
            Assert.That(Painters(replica), Is.EqualTo(Painters(host.Surface)));
            Assert.That(Apply(receiver, host.KeepStampsDelta(publisher, ref epoch, 7)), Is.EqualTo(LabPaintReceive.Applied));
            Assert.That(Pixels(replica), Is.EqualTo(Pixels(host.Surface)));
            Assert.That(replica.CoveredCount, Is.EqualTo(host.Surface.CoveredCount));
        }

        [Test]
        public void TheReceiverFollowsAResolutionChangeAndKeepsApplyingDeltas()
        {
            var replica = new LabPaintSurface();
            var receiver = new LabPaintReceiver(replica);
            // One per-peer publisher, as on the Host: a new source and size goes out as a fresh full map.
            var publisher = new LabPaintPublisher();
            var small = new ScriptedHost(0, 3);
            Assert.That(Apply(receiver, publisher.Publish(small.Surface, Session, 1, 1)), Is.EqualTo(LabPaintReceive.Applied));
            Assert.That(replica.Columns, Is.EqualTo(64));
            var large = new ScriptedHost(2, 3);
            Assert.That(Apply(receiver, publisher.Publish(large.Surface, Session, 2, 2)), Is.EqualTo(LabPaintReceive.Applied));
            Assert.That(replica.Columns, Is.EqualTo(256));
            long epoch = 3;
            for (int i = 0; i < 3; i++)
                Assert.That(Apply(receiver, large.KeepStampsDelta(publisher, ref epoch, 9, 2)), Is.EqualTo(LabPaintReceive.Applied));
            Assert.That(Pixels(replica), Is.EqualTo(Pixels(large.Surface)));
            Assert.That(Painters(replica), Is.EqualTo(Painters(large.Surface)));
            Assert.That(replica.CoveredCount, Is.EqualTo(large.Surface.CoveredCount));
        }

        [Test]
        public void TheCachedStampHashStillRejectsAWrongHashAndFollowsNewStamps()
        {
            var host = new ScriptedHost(1, 6);
            var publisher = new LabPaintPublisher();
            var replica = new LabPaintSurface();
            var receiver = new LabPaintReceiver(replica);
            long epoch = 1;
            Assert.That(Apply(receiver, publisher.Publish(host.Surface, Session, 1, epoch++)), Is.EqualTo(LabPaintReceive.Applied));
            LabPaintFrame[] keep = host.KeepStampsDelta(publisher, ref epoch, 3);
            Assert.That(Apply(receiver, Rewrite(keep, frame => frame.visualHash = new string('a', 64))), Is.EqualTo(LabPaintReceive.NeedCheckpoint),
                "A matching cache entry does not accept another hash.");
            Assert.That(Apply(receiver, keep.Select(Clone).ToArray()), Is.EqualTo(LabPaintReceive.Applied),
                "The untampered frames still apply: a rejected transaction does not consume its transfer number.");

            uint version = replica.VisualVersion;
            string oldHash = keep[0].visualHash;
            host.ReplaceStamps(Host(1, 9).CopyVisualStamps());
            Assert.That(Apply(receiver, publisher.Publish(host.Surface, Session, 1, epoch++)), Is.EqualTo(LabPaintReceive.Applied), "New stamps.");
            Assert.That(replica.VisualVersion, Is.Not.EqualTo(version));
            LabPaintFrame[] after = host.KeepStampsDelta(publisher, ref epoch, 3);
            Assert.That(after[0].visualHash, Is.Not.EqualTo(oldHash));
            Assert.That(Apply(receiver, Rewrite(after, frame => frame.visualHash = oldHash)), Is.EqualTo(LabPaintReceive.NeedCheckpoint),
                "The stale cached hash is not reused after the stamps changed.");
            Assert.That(Apply(receiver, after), Is.EqualTo(LabPaintReceive.Applied), "The cache followed the new stamps.");
            Assert.That(LabPaintVisualStamp.Encode(replica.CopyVisualStamps()), Is.EqualTo(LabPaintVisualStamp.Encode(host.Surface.CopyVisualStamps())));
        }

        [TestCase("    ")]
        [TestCase("\r\n\t ")]
        public void AMiddleFragmentThatOnlyDecodesOnItsOwnIsStillRejectedAsBefore(string whitespace)
        {
            var host = new ScriptedHost(0, 4);
            var publisher = new LabPaintPublisher();
            var replica = new LabPaintSurface();
            var receiver = new LabPaintReceiver(replica);
            long epoch = 1;
            Assert.That(Apply(receiver, publisher.Publish(host.Surface, Session, 1, epoch++)), Is.EqualTo(LabPaintReceive.Applied));
            uint revision = replica.Revision;
            LabPaintFrame[] delta = host.KeepStampsDelta(publisher, ref epoch, 2);
            Assume.That(delta, Has.Length.EqualTo(1));
            // The same body split so each fragment decodes on its own to exactly the right bytes: the first byte padded
            // and followed by whitespace, then the rest. Decoding each fragment separately would rebuild the valid delta.
            byte[] body = Convert.FromBase64String(delta[0].data);
            LabPaintFrame first = Clone(delta[0]), second = Clone(delta[0]);
            first.parts = second.parts = 2; first.part = 0; second.part = 1;
            first.data = Convert.ToBase64String(body, 0, 1) + whitespace;
            second.data = Convert.ToBase64String(body, 1, body.Length - 1);
            Assert.That(first.data.Length % 4, Is.Zero);
            Assert.That(Throws(() => Convert.FromBase64String(first.data + second.data)), Is.True, "The old concatenated decode rejects it.");
            Assert.That(Apply(receiver, new[] { first, second }), Is.EqualTo(LabPaintReceive.NeedCheckpoint));
            Assert.That(replica.Revision, Is.EqualTo(revision));
            Assert.That(Apply(receiver, delta), Is.EqualTo(LabPaintReceive.Applied), "The well-formed frame of the same transfer applies.");
        }

        private static bool Throws(Action action)
        {
            try { action(); return false; }
            catch (FormatException) { return true; }
        }

        /// <summary>
        /// A Host map whose cells change while its stamps stay the same, so every delta keeps the stamps (visualBytes -1).
        /// Splatter hits always add stamps and have no growth phase, so the map is driven through TryReplacePixels.
        /// </summary>
        private sealed class ScriptedHost
        {
            public readonly LabPaintSurface Surface = new LabPaintSurface();
            private readonly Color32[] pixels;
            private readonly byte[] painters;
            private LabPaintVisualStamp[] stamps;
            private uint revision = 100;
            private int paintCount;

            public ScriptedHost(int resolution, int hits)
            {
                LabPaintSurface basis = Host(resolution, hits);
                pixels = Pixels(basis); painters = Painters(basis); stamps = basis.CopyVisualStamps();
                paintCount = hits;
                Assert.That(stamps, Is.Not.Empty);
                Install();
            }

            public LabPaintFrame[] KeepStampsDelta(LabPaintPublisher publisher, ref long epoch, int cells, uint round = 1)
            {
                int painted = 0;
                for (int i = 0; i < pixels.Length && painted < cells; i++)
                {
                    if (pixels[i].a != 0) continue;
                    pixels[i] = new Color32((byte)(40 + painted), 120, 200, 255); painters[i] = (byte)(painted % 6 + 1);
                    painted++;
                }
                Assert.That(painted, Is.EqualTo(cells), "The map has room for the change.");
                paintCount++;
                Install();
                LabPaintFrame[] frames = publisher.Publish(Surface, Session, round, epoch++);
                Assert.That(frames, Is.Not.Empty);
                Assert.That(frames.All(frame => !frame.full && frame.visualBytes == -1), Is.True, "A delta that keeps the stamps.");
                return frames;
            }

            public void ReplaceStamps(LabPaintVisualStamp[] next) { stamps = next; Install(); }

            private void Install() => Assert.That(Surface.TryReplacePixels(pixels.Length == 2048 ? 64 : pixels.Length == 8192 ? 128 : 256,
                pixels.Length == 2048 ? 32 : pixels.Length == 8192 ? 64 : 128, revision++, pixels, painters, paintCount, stamps), Is.True);
        }

        private static LabPaintFrame[] InvalidCellWithMatchingHash(LabPaintFrame[] delta, Color32[] basePixels, byte[] basePainters)
        {
            byte[] body = Convert.FromBase64String(string.Concat(delta.Select(frame => frame.data)));
            body[2 + 3] = 128; // The first changed cell's alpha: neither empty nor opaque.
            Color32[] pixels = (Color32[])basePixels.Clone(); byte[] painters = (byte[])basePainters.Clone();
            for (int at = 0; at < body.Length; at += 7)
            {
                int index = body[at] | body[at + 1] << 8;
                pixels[index] = new Color32(body[at + 2], body[at + 3], body[at + 4], body[at + 5]);
                painters[index] = body[at + 6];
            }
            string hash = LabPaintPublisher.Hash(pixels, painters);
            string encoded = Convert.ToBase64String(body);
            Assume.That(encoded.Length, Is.LessThanOrEqualTo(delta.Length * LabPaintPublisher.FragmentCharacters));
            return Rewrite(delta, frame =>
            {
                int start = frame.part * LabPaintPublisher.FragmentCharacters;
                frame.data = encoded.Substring(start, Math.Min(LabPaintPublisher.FragmentCharacters, encoded.Length - start));
                frame.hash = hash;
            });
        }

        private static LabPaintFrame[] Rewrite(LabPaintFrame[] frames, Action<LabPaintFrame> change)
            => frames.Select(frame => { LabPaintFrame copy = Clone(frame); change(copy); return copy; }).ToArray();

        private static LabPaintFrame Clone(LabPaintFrame frame) => JsonUtility.FromJson<LabPaintFrame>(JsonUtility.ToJson(frame));

        private static LabPaintReceive Apply(LabPaintReceiver receiver, LabPaintFrame[] frames)
        {
            LabPaintReceive result = LabPaintReceive.Rejected;
            foreach (LabPaintFrame frame in frames) result = receiver.Receive(frame);
            return result;
        }

        private static LabPaintSurface Host(int resolution, int hits)
        {
            var host = new LabPaintSurface(new LabPaintGrowthSettings
            { Resolution = resolution, InitialPercent = 2f, RepeatPercent = 1.5f, Pattern = LabPaintPattern.Splatter }, 123u);
            for (int i = 0; i < hits; i++) Hit(host, i + 1);
            return host;
        }

        private static void Hit(LabPaintSurface surface, int seed)
        {
            var random = new System.Random(seed);
            uint revision = surface.Revision;
            for (int attempt = 0; attempt < 50 && surface.Revision == revision; attempt++)
                surface.RegisterHit(new Vector3((float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f,
                    (float)random.NextDouble() * 2f - 1f).normalized, Color.red, 1ul, seed % 6 + 1);
            Assert.That(surface.Revision, Is.Not.EqualTo(revision));
        }

        private static Color32[] Pixels(LabPaintSurface surface)
        {
            var pixels = new Color32[surface.CellCount];
            surface.CopyPixels(pixels);
            return pixels;
        }

        private static byte[] Painters(LabPaintSurface surface)
        {
            var painters = new byte[surface.CellCount];
            surface.CopyPainterSlots(painters);
            return painters;
        }
    }
}
