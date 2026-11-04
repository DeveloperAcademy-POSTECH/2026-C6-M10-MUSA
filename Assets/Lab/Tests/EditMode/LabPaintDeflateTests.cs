using System;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>Deflate-compressed paint frame bodies (#461): fewer frames, the same map, bounded inflation.</summary>
    public sealed class LabPaintDeflateTests
    {
        private const string Session = "deflate";

        [Test]
        public void AnEmptyStageStartMapIsOneFrameAndAFewBulkDatagrams()
        {
            var source = Surface(2);
            LabPaintFrame[] raw = new LabPaintPublisher().Publish(source, Session, 1, 1);
            LabPaintFrame[] auto = new LabPaintPublisher(null, LabPaintEncodingPolicy.Auto).Publish(source, Session, 1, 1);
            Assert.That(raw, Has.Length.EqualTo(107));
            Assert.That(raw.All(frame => frame.encoding == LabPaintPayloadCodec.Raw), Is.True);
            Assert.That(auto, Has.Length.EqualTo(1), "#461: the empty 256×128 map fits one frame.");
            Assert.That(auto[0].encoding, Is.EqualTo(LabPaintPayloadCodec.Deflate));
            Assert.That(auto[0].data.Length, Is.LessThanOrEqualTo(2048));
            int datagrams = BulkFragments(auto[0]);
            TestContext.WriteLine("EMPTY_MAP_BULK_FRAGMENTS auto=" + datagrams + " rawFrames=" + raw.Length
                + " rawFragments=" + raw.Sum(BulkFragments) + " chars=" + auto[0].data.Length);
            Assert.That(datagrams, Is.LessThanOrEqualTo(3));

            var replica = Surface(0);
            var receiver = new LabPaintReceiver(replica);
            Assert.That(Apply(receiver, auto), Is.EqualTo(LabPaintReceive.Applied));
            Assert.That(receiver.Matches(Snapshot(source)), Is.True);
            Assert.That(Pixels(replica), Is.EqualTo(Pixels(source)));
        }

        [Test]
        public void APaintedMapTakesFewerFramesAndArrivesIdentical()
        {
            var source = Surface(2);
            for (int i = 0; i < 12; i++) Hit(source, i + 1);
            LabPaintFrame[] raw = new LabPaintPublisher().Publish(source, Session, 1, 1);
            var publisher = new LabPaintPublisher(null, LabPaintEncodingPolicy.Auto);
            LabPaintFrame[] auto = publisher.Publish(source, Session, 1, 1);
            Assert.That(auto.Length, Is.LessThan(raw.Length));
            var replica = Surface(0);
            var receiver = new LabPaintReceiver(replica);
            Assert.That(Apply(receiver, auto), Is.EqualTo(LabPaintReceive.Applied));
            Assert.That(Pixels(replica), Is.EqualTo(Pixels(source)));
            Assert.That(Painters(replica), Is.EqualTo(Painters(source)));
            Assert.That(LabPaintVisualStamp.Encode(replica.CopyVisualStamps()), Is.EqualTo(LabPaintVisualStamp.Encode(source.CopyVisualStamps())));

            Hit(source, 99);
            LabPaintFrame[] delta = publisher.Publish(source, Session, 1, 2);
            Assert.That(delta.All(frame => !frame.full), Is.True);
            Assert.That(Apply(receiver, delta), Is.EqualTo(LabPaintReceive.Applied));
            Assert.That(Pixels(replica), Is.EqualTo(Pixels(source)));
            Assert.That(Apply(receiver, publisher.Checkpoint()), Is.EqualTo(LabPaintReceive.Applied), "A compressed checkpoint too.");
        }

        [Test]
        public void PeersSharingTheHostCacheWithAutoGetTheSameFramesAsPrivateAutoPublishers()
        {
            // The Host's runtime setup: one shared capture cache, every peer on Auto (#439 + #461).
            var source = Surface(2);
            var cache = new LabPaintPublishCache();
            var shared = new[] { new LabPaintPublisher(cache, LabPaintEncodingPolicy.Auto), new LabPaintPublisher(cache, LabPaintEncodingPolicy.Auto) };
            var alone = new[] { new LabPaintPublisher(null, LabPaintEncodingPolicy.Auto), new LabPaintPublisher(null, LabPaintEncodingPolicy.Auto) };
            var receivers = new[] { new LabPaintReceiver(Surface(0)), new LabPaintReceiver(Surface(0)) };
            for (long epoch = 1; epoch <= 8; epoch++)
            {
                if (epoch > 1) Hit(source, (int)epoch);
                for (int peer = 0; peer < 2; peer++)
                {
                    if (peer == 1 && (epoch == 3 || epoch == 4)) continue; // Peer 1's backlog was full.
                    LabPaintFrame[] frames = shared[peer].Publish(source, Session, 1, epoch);
                    Assert.That(Json(frames), Is.EqualTo(Json(alone[peer].Publish(source, Session, 1, epoch))), "Pass " + epoch + " peer " + peer);
                    if (frames.Length > 0) Assert.That(Apply(receivers[peer], frames), Is.EqualTo(LabPaintReceive.Applied));
                }
            }
            for (int peer = 0; peer < 2; peer++)
            {
                Assert.That(Json(shared[peer].Checkpoint()), Is.EqualTo(Json(alone[peer].Checkpoint())));
                LabPaintSurface replica = (LabPaintSurface)typeof(LabPaintReceiver).GetField("surface",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(receivers[peer]);
                Assert.That(Pixels(replica), Is.EqualTo(Pixels(source)), "Peer " + peer + " holds the Host map.");
            }
        }

        private static string Json(LabPaintFrame[] frames) => string.Join("\n", frames.Select(JsonUtility.ToJson));

        [Test]
        public void AOneCellDeltaStaysRawBecauseDeflateWouldBeLonger()
        {
            var painted = Surface(0);
            Hit(painted, 3);
            Color32[] pixels = Pixels(painted); byte[] painters = Painters(painted);
            int cell = Array.FindIndex(pixels, pixel => pixel.a == 255);
            var source = new LabPaintSurface();
            Assert.That(source.TryReplacePixels(64, 32, 50u, pixels, painters, 1), Is.True);
            var publisher = new LabPaintPublisher(null, LabPaintEncodingPolicy.Auto);
            publisher.Publish(source, Session, 1, 1);
            painters[cell] = (byte)(painters[cell] == 2 ? 3 : 2);
            Assert.That(source.TryReplacePixels(64, 32, 51u, pixels, painters, 1), Is.True);
            LabPaintFrame[] delta = publisher.Publish(source, Session, 1, 2);
            Assert.That(delta, Has.Length.EqualTo(1));
            Assert.That(delta[0].encoding, Is.EqualTo(LabPaintPayloadCodec.Raw));
        }

        [Test]
        public void ACompressedFullMapOfALowerRevisionIsStillRejected()
        {
            var newer = new LabPaintSurface(); var older = new LabPaintSurface();
            var painted = Surface(0); Hit(painted, 4);
            Assert.That(newer.TryReplacePixels(64, 32, 90u, Pixels(painted), Painters(painted), 1), Is.True);
            Assert.That(older.TryReplacePixels(64, 32, 80u, Pixels(painted), Painters(painted), 1), Is.True);
            var publisher = new LabPaintPublisher(null, LabPaintEncodingPolicy.Auto);
            var receiver = new LabPaintReceiver(Surface(0));
            Assert.That(Apply(receiver, publisher.Publish(newer, Session, 1, 1)), Is.EqualTo(LabPaintReceive.Applied));
            LabPaintFrame[] stale = publisher.Publish(older, Session, 1, 2);
            Assert.That(stale[0].encoding, Is.EqualTo(LabPaintPayloadCodec.Deflate));
            Assert.That(Apply(receiver, stale), Is.EqualTo(LabPaintReceive.Rejected));
        }

        [TestCase(-1, LabPaintReceive.Rejected)]
        [TestCase(2, LabPaintReceive.Rejected)]
        [TestCase(99, LabPaintReceive.Rejected)]
        public void AnUnknownEncodingIsRejected(int encoding, LabPaintReceive expected)
        {
            LabPaintFrame[] frames = new LabPaintPublisher(null, LabPaintEncodingPolicy.Auto).Publish(Surface(0), Session, 1, 1);
            Assert.That(Apply(new LabPaintReceiver(Surface(0)), Rewrite(frames, frame => frame.encoding = encoding)), Is.EqualTo(expected));
        }

        [Test]
        public void ACorruptOrOversizedDeflateBodyAsksForACheckpoint()
        {
            LabPaintFrame[] frames = new LabPaintPublisher(null, LabPaintEncodingPolicy.Auto).Publish(Surface(0), Session, 1, 1);
            Assert.That(frames[0].encoding, Is.EqualTo(LabPaintPayloadCodec.Deflate));
            var receiver = new LabPaintReceiver(Surface(0));
            // Raw bytes labelled as deflate.
            LabPaintFrame[] mislabelled = new LabPaintPublisher().Publish(Surface(0), Session, 1, 1).Take(1).Select(Clone).ToArray();
            mislabelled[0].parts = 1; mislabelled[0].encoding = LabPaintPayloadCodec.Deflate;
            Assert.That(Apply(receiver, mislabelled), Is.EqualTo(LabPaintReceive.NeedCheckpoint));
            // A bomb: a tiny body that would inflate far past the header's 64×32×5 bytes.
            string bomb = Convert.ToBase64String(LabPaintPayloadCodec.Compress(new byte[1 << 20]));
            Assume.That(bomb.Length, Is.LessThanOrEqualTo(LabPaintPublisher.FragmentCharacters));
            LabPaintFrame[] bombs = Rewrite(frames, frame => { frame.data = bomb; frame.sequence = 5; });
            Assert.That(Apply(receiver, bombs), Is.EqualTo(LabPaintReceive.NeedCheckpoint));
            // Parts that disagree on the encoding.
            var painted = Surface(1); Hit(painted, 6);
            LabPaintFrame[] many = new LabPaintPublisher().Publish(painted, Session, 1, 2);
            Assert.That(many.Length, Is.GreaterThan(1));
            LabPaintFrame[] mixed = many.Select(Clone).ToArray();
            mixed[1].encoding = 1 - mixed[1].encoding;
            Assert.That(Apply(receiver, new[] { mixed[0], mixed[1] }), Is.EqualTo(LabPaintReceive.NeedCheckpoint));
            Assert.That(Apply(receiver, many), Is.EqualTo(LabPaintReceive.Applied), "The genuine frames still apply.");
        }

        [Test]
        public void TheCodecRoundTripsAndStopsAtTheLimit()
        {
            var random = new System.Random(8);
            var data = new byte[5000];
            random.NextBytes(data);
            byte[] compressed = LabPaintPayloadCodec.Compress(data);
            byte[] buffer = null;
            Assert.That(LabPaintPayloadCodec.TryInflate(compressed, compressed.Length, data.Length, ref buffer, out int written), Is.True);
            Assert.That(written, Is.EqualTo(data.Length));
            Assert.That(buffer.Take(written), Is.EqualTo(data));
            Assert.That(LabPaintPayloadCodec.TryInflate(compressed, compressed.Length, data.Length - 1, ref buffer, out _), Is.False);
            Assert.That(LabPaintPayloadCodec.TryInflate(new byte[] { 1, 2, 3, 4, 5 }, 5, 100, ref buffer, out _), Is.False);
            Assert.That(LabPaintPayloadCodec.TryInflate(compressed, compressed.Length + 1, 100, ref buffer, out _), Is.False);
            byte[] empty = LabPaintPayloadCodec.Compress(Array.Empty<byte>());
            Assert.That(LabPaintPayloadCodec.TryInflate(empty, empty.Length, 0, ref buffer, out written), Is.True);
            Assert.That(written, Is.Zero);
        }

        private static int BulkFragments(LabPaintFrame frame)
        {
            Type bodyType = typeof(LabNetwork).Assembly.GetType("C6Lab.LabBulkPayload", true);
            object body = Activator.CreateInstance(bodyType, true);
            bodyType.GetField("type").SetValue(body, "paint_cells");
            bodyType.GetField("sessionId").SetValue(body, frame.session);
            bodyType.GetField("round").SetValue(body, frame.round);
            bodyType.GetField("configRevision").SetValue(body, 1);
            bodyType.GetField("configFingerprint").SetValue(body, new string('f', 64));
            bodyType.GetField("snapshotSequence").SetValue(body, frame.epoch);
            bodyType.GetField("payload").SetValue(body, LabPacketPayload.Encode(frame));
            return LabBulkMessageChunks.Split(Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)))).Length;
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

        private static LabSnapshot Snapshot(LabPaintSurface surface) => new LabSnapshot
        {
            sessionId = Session, round = 1u, paintColumns = surface.Columns, paintRows = surface.Rows,
            paintRevision = surface.Revision, paintCoveredCount = surface.CoveredCount,
            paintCellCount = surface.CellCount, paintCount = surface.PaintCount
        };

        private static LabPaintSurface Surface(int resolution) => new LabPaintSurface(new LabPaintGrowthSettings
        { Resolution = resolution, InitialPercent = 2f, RepeatPercent = 1.5f, Pattern = LabPaintPattern.Splatter }, 123u);

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
        { var pixels = new Color32[surface.CellCount]; surface.CopyPixels(pixels); return pixels; }

        private static byte[] Painters(LabPaintSurface surface)
        { var painters = new byte[surface.CellCount]; surface.CopyPainterSlots(painters); return painters; }
    }
}
