using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>One Host copy of the surface per publish pass is shared by every peer and changes no frame byte (#439).</summary>
    public sealed class LabPaintPublishCacheTests
    {
        private const string Session = "shared-capture";

        [Test]
        public void PeersSharingTheCacheTakeOneCapturePerPassAndGetTheSameFramesAsPrivatePublishers()
        {
            var source = Surface(2);
            var cache = new LabPaintPublishCache();
            var shared = new[] { new LabPaintPublisher(cache), new LabPaintPublisher(cache), new LabPaintPublisher(cache) };
            var alone = new[] { new LabPaintPublisher(), new LabPaintPublisher(), new LabPaintPublisher() };
            var random = new System.Random(5);
            for (long epoch = 1; epoch <= 12; epoch++)
            {
                if (epoch > 1) Hit(source, random, epoch % 2 == 0 ? Color.red : Color.blue, (int)(epoch % 3) + 1);
                int before = cache.CapturesTaken;
                for (int peer = 0; peer < shared.Length; peer++)
                    Assert.That(Json(shared[peer].Publish(source, Session, 1, epoch)),
                        Is.EqualTo(Json(alone[peer].Publish(source, Session, 1, epoch))), "Pass " + epoch + " peer " + peer);
                Assert.That(cache.CapturesTaken - before, Is.EqualTo(1), "One copy of the surface per pass.");
            }
            for (int peer = 0; peer < shared.Length; peer++)
                Assert.That(Json(shared[peer].Checkpoint()), Is.EqualTo(Json(alone[peer].Checkpoint())));
        }

        [Test]
        public void FramesMatchMainsPublisherByteForByteAcrossVisualStampsSkippedPassesRoundsAndCheckpoints()
        {
            var settings = new LabPaintGrowthSettings
            { Resolution = 1, InitialPercent = 2f, RepeatPercent = 1.5f, Pattern = LabPaintPattern.Splatter };
            var source = new LabPaintSurface(settings, 123u);
            var cache = new LabPaintPublishCache();
            var shared = new[] { new LabPaintPublisher(cache), new LabPaintPublisher(cache), new LabPaintPublisher(cache) };
            var reference = new[] { new MainPublisher(), new MainPublisher(), new MainPublisher() };
            var random = new System.Random(3);
            string session = Session; uint round = 1;
            int compared = 0, visualFrames = 0;
            for (long epoch = 1; epoch <= 16; epoch++)
            {
                if (epoch == 8) { source.Reset(settings, 77u); round = 2; }
                else if (epoch == 12) session = "next-session";
                else if (epoch > 1) Hit(source, random, epoch % 2 == 0 ? Color.red : Color.blue, (int)(epoch % 3) + 1);
                for (int peer = 0; peer < shared.Length; peer++)
                {
                    // Peer 2's backlog is full on some passes, so it catches up from an older base.
                    if (peer == 2 && (epoch >= 3 && epoch <= 5 || epoch == 13)) continue;
                    LabPaintFrame[] actual = shared[peer].Publish(source, session, round, epoch);
                    Assert.That(Json(actual), Is.EqualTo(Json(reference[peer].Publish(source, session, round, epoch))),
                        "Pass " + epoch + " peer " + peer);
                    compared += actual.Length;
                    visualFrames += actual.Count(frame => frame.visualBytes > 0);
                }
                if (epoch % 5 == 0)
                    for (int peer = 0; peer < shared.Length; peer++)
                        Assert.That(Json(shared[peer].Checkpoint()), Is.EqualTo(Json(reference[peer].Checkpoint())), "Checkpoint " + epoch);
            }
            Assert.That(compared, Is.GreaterThan(0));
            Assert.That(visualFrames, Is.GreaterThan(0), "Visual stamps were part of the compared frames.");
        }

        [Test]
        public void APeerThatSkippedPassesGetsADeltaFromItsOwnLastPublishedState()
        {
            var source = Surface(1);
            var cache = new LabPaintPublishCache();
            var everyPass = new LabPaintPublisher(cache);
            var skipping = new LabPaintPublisher(cache);
            var replica = Surface(1);
            var receiver = new LabPaintReceiver(replica);
            var random = new System.Random(9);
            Assert.That(Apply(receiver, skipping.Publish(source, Session, 1, 1)), Is.EqualTo(LabPaintReceive.Applied));
            everyPass.Publish(source, Session, 1, 1);
            uint lastSeenBySkipping = source.Revision;
            for (long epoch = 2; epoch <= 6; epoch++)
            {
                Hit(source, random, Color.green, 2);
                everyPass.Publish(source, Session, 1, epoch);
                // The skipping peer's backlog was full for epochs 2..5 and catches up at 6.
                if (epoch < 6) continue;
                LabPaintFrame[] catchUp = skipping.Publish(source, Session, 1, epoch);
                Assert.That(catchUp.All(frame => !frame.full), Is.True, "A delta, not a full map.");
                Assert.That(catchUp[0].fromRevision, Is.EqualTo(lastSeenBySkipping), "From its own base, not the other peer's.");
                Assert.That(Apply(receiver, catchUp), Is.EqualTo(LabPaintReceive.Applied));
            }
            Assert.That(Pixels(replica), Is.EqualTo(Pixels(source)));
            Assert.That(Painters(replica), Is.EqualTo(Painters(source)));
            Assert.That(replica.Revision, Is.EqualTo(source.Revision));
        }

        [Test]
        public void ACellWhoseOnlyChangeIsItsPainterIsSentAsAOneCellDelta()
        {
            var painted = Surface(0);
            painted.RegisterHit(Vector3.forward, Color.red, 1, 1);
            Color32[] pixels = Pixels(painted);
            byte[] painters = Painters(painted);
            int cell = Array.FindIndex(pixels, pixel => pixel.a == 255);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));

            var source = Surface(0);
            Assert.That(source.TryReplacePixels(64, 32, 100u, pixels, painters, 1), Is.True);
            var cache = new LabPaintPublishCache();
            var publisher = new LabPaintPublisher(cache);
            var replica = Surface(0);
            var receiver = new LabPaintReceiver(replica);
            Assert.That(Apply(receiver, publisher.Publish(source, Session, 1, 1)), Is.EqualTo(LabPaintReceive.Applied));

            painters[cell] = (byte)(painters[cell] == 2 ? 3 : 2);
            Assert.That(source.TryReplacePixels(64, 32, 101u, pixels, painters, 1), Is.True);
            LabPaintFrame[] delta = publisher.Publish(source, Session, 1, 2);
            Assert.That(delta, Has.Length.EqualTo(1));
            Assert.That(delta[0].full, Is.False);
            byte[] body = Convert.FromBase64String(delta[0].data);
            Assert.That(body, Has.Length.EqualTo(7), "Index (2) + RGBA (4) + painter (1) for exactly one cell.");
            Assert.That(body[0] | body[1] << 8, Is.EqualTo(cell));
            Assert.That(body[6], Is.EqualTo(painters[cell]));
            Assert.That(Apply(receiver, delta), Is.EqualTo(LabPaintReceive.Applied));
            Assert.That(Painters(replica)[cell], Is.EqualTo(painters[cell]));
        }

        [Test]
        public void AnUnchangedSurfaceSendsNothingAndAClearedCacheTakesANewCapture()
        {
            var source = Surface(0);
            var cache = new LabPaintPublishCache();
            var publisher = new LabPaintPublisher(cache);
            Assert.That(publisher.Publish(source, Session, 1, 1), Is.Not.Empty);
            Assert.That(publisher.Publish(source, Session, 1, 2), Is.Empty, "No change, no frame and no new capture.");
            Assert.That(cache.CapturesTaken, Is.EqualTo(1));
            source.RegisterHit(Vector3.forward, Color.red, 1, 1);
            cache.Clear();
            Assert.That(new LabPaintPublisher(cache).Publish(source, Session, 1, 3).All(frame => frame.full), Is.True);
            Assert.That(publisher.Publish(source, Session, 1, 3).All(frame => !frame.full), Is.True);
            Assert.That(cache.CapturesTaken, Is.EqualTo(2), "Both peers of pass 3 shared the capture taken after Clear.");
        }

        /// <summary>LabPaintPublisher as it was on main before #439, kept verbatim through public APIs as the reference.</summary>
        private sealed class MainPublisher
        {
            private Color32[] published;
            private byte[] publishedPainters;
            private byte[] publishedVisual = Array.Empty<byte>();
            private LabPaintSurface publishedSource;
            private uint publishedVisualVersion;
            private string publishedVisualHash;
            private string session;
            private uint round, revision;
            private int columns, rows, paintCount, covered;
            private string hash;
            private long sequence, epoch;

            public LabPaintFrame[] Publish(LabPaintSurface source, string nextSession, uint nextRound, long nextEpoch)
            {
                bool sameSource = ReferenceEquals(publishedSource, source);
                if (sameSource && published != null && session == nextSession && round == nextRound
                    && columns == source.Columns && rows == source.Rows && revision == source.Revision
                    && paintCount == source.PaintCount && publishedVisualVersion == source.VisualVersion)
                    return Array.Empty<LabPaintFrame>();
                var current = new Color32[source.CellCount];
                var painters = new byte[source.CellCount];
                source.CopyPixels(current);
                source.CopyPainterSlots(painters);
                byte[] visual = sameSource && publishedVisualVersion == source.VisualVersion
                    ? publishedVisual : LabPaintVisualStamp.Encode(source.CopyVisualStamps());
                bool visualChanged = !visual.SequenceEqual(publishedVisual);
                string visualHash = visual.Length == 0 ? null : sameSource && publishedVisualVersion == source.VisualVersion
                    ? publishedVisualHash : LabPaintVisualStamp.Hash(visual);
                bool fresh = published == null || !sameSource || session != nextSession || round != nextRound
                    || columns != source.Columns || rows != source.Rows;
                if (!fresh && revision == source.Revision && paintCount == source.PaintCount && !visualChanged)
                    return Array.Empty<LabPaintFrame>();
                var delta = new System.Collections.Generic.List<byte>();
                if (!fresh)
                    for (int i = 0; i < current.Length; i++)
                        if (!current[i].Equals(published[i]) || painters[i] != publishedPainters[i])
                        {
                            delta.Add((byte)i); delta.Add((byte)(i >> 8));
                            delta.Add(current[i].r); delta.Add(current[i].g); delta.Add(current[i].b); delta.Add(current[i].a);
                            delta.Add(painters[i]);
                        }
                bool full = fresh || delta.Count >= current.Length * 5;
                byte[] payload = full ? CellBytes(current, painters) : delta.ToArray();
                var frames = Frames(nextSession, nextRound, nextEpoch, ++sequence, full,
                    fresh ? 0u : revision, source.Revision, source.Columns, source.Rows,
                    source.PaintCount, source.CoveredCount, LabPaintPublisher.Hash(current, painters), payload,
                    visual, visualHash, full || visualChanged);
                published = current; publishedPainters = painters; publishedVisual = visual;
                publishedSource = source; publishedVisualVersion = source.VisualVersion; publishedVisualHash = visualHash;
                session = nextSession; round = nextRound; epoch = nextEpoch;
                columns = source.Columns; rows = source.Rows; revision = source.Revision;
                paintCount = source.PaintCount; covered = source.CoveredCount; hash = frames[0].hash;
                return frames;
            }

            public LabPaintFrame[] Checkpoint()
                => published == null ? Array.Empty<LabPaintFrame>()
                    : Frames(session, round, epoch, ++sequence, true, 0, revision, columns, rows,
                        paintCount, covered, hash, CellBytes(published, publishedPainters), publishedVisual, publishedVisualHash, true);

            private static byte[] CellBytes(Color32[] pixels, byte[] painters)
            {
                var bytes = new byte[pixels.Length * 5];
                for (int i = 0; i < pixels.Length; i++)
                {
                    int at = i * 5;
                    bytes[at] = pixels[i].r; bytes[at + 1] = pixels[i].g;
                    bytes[at + 2] = pixels[i].b; bytes[at + 3] = pixels[i].a;
                    bytes[at + 4] = painters[i];
                }
                return bytes;
            }

            private static LabPaintFrame[] Frames(string session, uint round, long epoch, long sequence,
                bool full, uint from, uint revision, int columns, int rows, int paintCount,
                int coveredCount, string hash, byte[] cellPayload, byte[] visual, string visualHash, bool includeVisual)
            {
                int visualSize = includeVisual ? visual.Length : visual.Length == 0 ? 0 : -1;
                byte[] payload = cellPayload;
                if (visualSize > 0)
                {
                    payload = new byte[cellPayload.Length + visual.Length];
                    Array.Copy(cellPayload, payload, cellPayload.Length);
                    Array.Copy(visual, 0, payload, cellPayload.Length, visual.Length);
                }
                string encoded = Convert.ToBase64String(payload);
                int size = LabPaintPublisher.FragmentCharacters;
                int count = Math.Max(1, (encoded.Length + size - 1) / size);
                var result = new LabPaintFrame[count];
                for (int i = 0; i < count; i++)
                    result[i] = new LabPaintFrame
                    {
                        session = session, round = round, epoch = epoch, sequence = sequence,
                        full = full, fromRevision = from, revision = revision, columns = columns, rows = rows,
                        paintCount = paintCount, coveredCount = coveredCount, hash = hash, part = i, parts = count,
                        visualBytes = visualSize, visualHash = visualHash,
                        data = encoded.Substring(i * size, Math.Min(size, encoded.Length - i * size))
                    };
                return result;
            }
        }

        private static LabPaintReceive Apply(LabPaintReceiver receiver, LabPaintFrame[] frames)
        {
            LabPaintReceive result = LabPaintReceive.Rejected;
            foreach (LabPaintFrame frame in frames) result = receiver.Receive(frame);
            return result;
        }

        private static string Json(LabPaintFrame[] frames) => string.Join("\n", frames.Select(JsonUtility.ToJson));

        private static void Hit(LabPaintSurface surface, System.Random random, Color color, int painter)
        {
            uint revision = surface.Revision;
            for (int attempt = 0; attempt < 50 && surface.Revision == revision; attempt++)
                surface.RegisterHit(Direction(random), color, 1, painter);
            Assert.That(surface.Revision, Is.Not.EqualTo(revision), "Every pass must change the surface.");
        }

        private static Vector3 Direction(System.Random random) => new Vector3((float)random.NextDouble() * 2f - 1f,
            (float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f).normalized;

        private static LabPaintSurface Surface(int resolution)
            => new LabPaintSurface(new LabPaintGrowthSettings { Pattern = LabPaintPattern.Legacy, Resolution = resolution }, 42u);

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
