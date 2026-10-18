using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>Packet fixtures exercise paint transactions; they are not live-network evidence.</summary>
    public sealed class LabPaintReplicationTests
    {
        private const string Session = "paint-fixture-session";
        private const uint Round = 1u;

        [TestCase(0, 64, 32, 3)]
        [TestCase(1, 128, 64, 11)]
        [TestCase(2, 256, 128, 43)]
        public void FullCheckpointsAreBoundedAndReassembleAtEverySupportedResolution(
            int resolution, int columns, int rows, int parts)
        {
            var source = NewSurface(resolution);
            Assert.That(source.RegisterHit(Vector3.forward, Color.red, 7), Is.True);
            var publisher = new LabPaintPublisher();
            LabPaintFrame[] frames = publisher.Publish(source, Session, Round, 10);
            Assert.That(frames.Length, Is.EqualTo(parts));
            Assert.That(frames.All(frame => frame.full), Is.True);
            Assert.That(frames.All(frame => frame.columns == columns && frame.rows == rows), Is.True);
            Assert.That(frames.Select(frame => frame.part), Is.EqualTo(Enumerable.Range(0, parts)));
            Assert.That(frames.All(frame => frame.parts == parts), Is.True);
            Assert.That(frames.All(frame => frame.data.Length <= LabPaintPublisher.FragmentCharacters), Is.True);
            Assert.That(frames.All(frame => Encoding.UTF8.GetByteCount(JsonUtility.ToJson(frame)) < 32 * 1024), Is.True,
                "Even the maximum-resolution paint-only fragment leaves room for its packet envelope.");
            Assert.That(string.Concat(frames.Select(frame => frame.data)).Length,
                Is.EqualTo(((columns * rows * 4 + 2) / 3) * 4));
            var replica = NewSurface();
            var receiver = new LabPaintReceiver(replica);
            Assert.That(ReceiveAll(receiver, frames.Reverse().ToArray()), Is.EqualTo(LabPaintReceive.Applied));
            AssertReplica(source, replica);
            Assert.That(receiver.Matches(Snapshot(source)), Is.True);
        }

        [Test]
        public void OutOfOrderAndDuplicatePartsKeepTheVisibleMapAtomicUntilCompletion()
        {
            var source = NewSurface();
            source.RegisterHit(Vector3.forward, Color.red, 7);
            var replica = NewSurface();
            replica.RegisterHit(Vector3.back, Color.blue, 8);
            Color32[] previousPixels = Pixels(replica);
            uint previousRevision = replica.Revision;
            int previousCovered = replica.CoveredCount, previousCount = replica.PaintCount;
            var receiver = new LabPaintReceiver(replica);
            LabPaintFrame[] frames = new LabPaintPublisher().Publish(source, Session, Round, 10);
            Assert.That(frames.Length, Is.GreaterThan(1));
            Assert.That(receiver.Receive(frames[frames.Length - 1]), Is.EqualTo(LabPaintReceive.Pending));
            Assert.That(receiver.Receive(Clone(frames[frames.Length - 1])), Is.EqualTo(LabPaintReceive.Pending));
            Assert.That(receiver.HasPending, Is.True);
            AssertUnchanged(replica, previousPixels, previousRevision, previousCovered, previousCount);
            Assert.That(receiver.Matches(Snapshot(source)), Is.False,
                "A complete game snapshot cannot expose a partially received paint map to the HUD.");
            for (int i = frames.Length - 2; i >= 0; i--)
            {
                LabPaintReceive result = receiver.Receive(frames[i]);
                if (i > 0)
                {
                    Assert.That(result, Is.EqualTo(LabPaintReceive.Pending));
                    AssertUnchanged(replica, previousPixels, previousRevision, previousCovered, previousCount);
                }
                else Assert.That(result, Is.EqualTo(LabPaintReceive.Applied));
            }
            Assert.That(receiver.HasPending, Is.False);
            AssertReplica(source, replica);
            Assert.That(receiver.Matches(Snapshot(source)), Is.True);
            foreach (LabPaintFrame duplicate in frames)
                Assert.That(receiver.Receive(duplicate), Is.EqualTo(LabPaintReceive.Rejected),
                    "A completed transaction cannot apply a second time.");
        }

        [Test]
        public void SmallChangesUseADeltaAndAnUnchangedPublishedMapSendsNothing()
        {
            var source = NewSurface();
            source.RegisterHit(Vector3.forward, Color.red, 7);
            var publisher = new LabPaintPublisher();
            var replica = NewSurface();
            var receiver = new LabPaintReceiver(replica);
            Assert.That(ReceiveAll(receiver, publisher.Publish(source, Session, Round, 10)), Is.EqualTo(LabPaintReceive.Applied));
            uint baseRevision = replica.Revision;
            source.RegisterHit(Vector3.back, Color.blue, 8);
            LabPaintFrame[] delta = publisher.Publish(source, Session, Round, 11);
            Assert.That(delta, Is.Not.Empty);
            Assert.That(delta.All(frame => !frame.full && frame.fromRevision == baseRevision), Is.True);
            Assert.That(ReceiveAll(receiver, delta), Is.EqualTo(LabPaintReceive.Applied));
            AssertReplica(source, replica);
            Assert.That(publisher.Publish(source, Session, Round, 12), Is.Empty,
                "Epoch is the associated snapshot sequence; advancing it alone does not resend the same map.");
        }

        [Test]
        public void SameRevisionPaintCountUpdateIsAnEmptyDeltaAndStillCommits()
        {
            var source = NewSurface();
            source.RegisterHit(Vector3.forward, Color.red, 7);
            var publisher = new LabPaintPublisher();
            var replica = NewSurface();
            var receiver = new LabPaintReceiver(replica);
            ReceiveAll(receiver, publisher.Publish(source, Session, Round, 10));
            uint revision = source.Revision;
            Color32[] pixels = Pixels(source);
            source.RegisterHit(Vector3.forward, Color.blue, 8);
            Assert.That(source.Revision, Is.EqualTo(revision), "A repeat hit queues growth without painting immediately.");
            Assert.That(source.PaintCount, Is.EqualTo(2));
            LabPaintFrame[] update = publisher.Publish(source, Session, Round, 11);
            Assert.That(update.Length, Is.EqualTo(1));
            Assert.That(update[0].full, Is.False);
            Assert.That(update[0].data, Is.Empty);
            Assert.That(update[0].fromRevision, Is.EqualTo(update[0].revision));
            Assert.That(receiver.Matches(Snapshot(source)), Is.False, "Hit counts are part of the shared map identity.");
            Assert.That(receiver.Receive(update[0]), Is.EqualTo(LabPaintReceive.Applied));
            Assert.That(replica.Revision, Is.EqualTo(revision));
            Assert.That(replica.PaintCount, Is.EqualTo(2));
            Assert.That(Pixels(replica), Is.EqualTo(pixels));
            Assert.That(receiver.Matches(Snapshot(source)), Is.True);
        }

        [Test]
        public void LostBaseRequiresACheckpointAndTheFullPublishedMapRecoversExactly()
        {
            var source = NewSurface();
            source.RegisterHit(Vector3.forward, Color.red, 7);
            var publisher = new LabPaintPublisher();
            var replica = NewSurface();
            var receiver = new LabPaintReceiver(replica);
            ReceiveAll(receiver, publisher.Publish(source, Session, Round, 10));
            Color32[] before = Pixels(replica);
            uint beforeRevision = replica.Revision;
            int beforeCovered = replica.CoveredCount, beforeCount = replica.PaintCount;
            source.RegisterHit(Vector3.back, Color.blue, 8);
            LabPaintFrame[] lost = publisher.Publish(source, Session, Round, 11);
            Assert.That(lost.All(frame => !frame.full), Is.True);
            source.RegisterHit(Vector3.left, Color.green, 9);
            LabPaintFrame[] missingBase = publisher.Publish(source, Session, Round, 12);
            Assert.That(missingBase.All(frame => !frame.full), Is.True);
            Assert.That(ReceiveAll(receiver, missingBase), Is.EqualTo(LabPaintReceive.NeedCheckpoint));
            AssertUnchanged(replica, before, beforeRevision, beforeCovered, beforeCount);
            Assert.That(receiver.HasPending, Is.False);
            Assert.That(receiver.Matches(Snapshot(source)), Is.False);
            LabPaintFrame[] checkpoint = publisher.Checkpoint();
            Assert.That(checkpoint.All(frame => frame.full), Is.True);
            Assert.That(ReceiveAll(receiver, checkpoint), Is.EqualTo(LabPaintReceive.Applied));
            AssertReplica(source, replica);
            foreach (LabPaintFrame old in lost)
                Assert.That(receiver.Receive(old), Is.EqualTo(LabPaintReceive.Rejected));
        }

        [Test]
        public void CheckpointUsesTheLastPublishedCopyEvenIfTheHostSurfaceChangesLater()
        {
            var source = NewSurface();
            source.RegisterHit(Vector3.forward, Color.red, 7);
            var publisher = new LabPaintPublisher();
            LabPaintFrame[] published = publisher.Publish(source, Session, Round, 10);
            Color32[] frozen = Pixels(source);
            LabSnapshot frozenSnapshot = Snapshot(source);
            source.RegisterHit(Vector3.back, Color.blue, 8);
            LabPaintFrame[] checkpoint = publisher.Checkpoint();
            Assert.That(checkpoint[0].sequence, Is.GreaterThan(published[0].sequence));
            Assert.That(checkpoint[0].epoch, Is.EqualTo(10), "A checkpoint refers to the snapshot of its published map.");
            var replica = NewSurface();
            var receiver = new LabPaintReceiver(replica);
            Assert.That(ReceiveAll(receiver, checkpoint), Is.EqualTo(LabPaintReceive.Applied));
            Assert.That(Pixels(replica), Is.EqualTo(frozen));
            Assert.That(receiver.Matches(frozenSnapshot), Is.True);
            Assert.That(receiver.Matches(Snapshot(source)), Is.False);
        }

        [Test]
        public void ADeltaCannotInitializeAnUnrelatedReplicaAndResetRestoresTheSequenceBoundary()
        {
            var source = NewSurface();
            source.RegisterHit(Vector3.forward, Color.red, 7);
            var publisher = new LabPaintPublisher();
            publisher.Publish(source, Session, Round, 10);
            source.RegisterHit(Vector3.back, Color.blue, 8);
            LabPaintFrame[] delta = publisher.Publish(source, Session, Round, 11);
            var replica = NewSurface();
            var receiver = new LabPaintReceiver(replica);
            Assert.That(receiver.Receive(delta[0]), Is.EqualTo(LabPaintReceive.NeedCheckpoint));
            Assert.That(receiver.HasPending, Is.False);
            Assert.That(replica.CoveredCount, Is.Zero);
            Assert.That(ReceiveAll(receiver, publisher.Checkpoint()), Is.EqualTo(LabPaintReceive.Applied));
            AssertReplica(source, replica);
            publisher.Reset();
            Assert.That(publisher.Checkpoint(), Is.Empty);
            LabPaintFrame[] resetFull = publisher.Publish(source, "next-session", 1, 20);
            Assert.That(resetFull[0].sequence, Is.EqualTo(1));
            Assert.That(receiver.Receive(resetFull[0]), Is.EqualTo(LabPaintReceive.Rejected),
                "Publisher sequence numbers are global for a connected receiver, even when epoch advances.");
            receiver.Reset();
            Assert.That(receiver.Session, Is.Null);
            Assert.That(receiver.Round, Is.Zero);
            Assert.That(receiver.Epoch, Is.Zero);
            Assert.That(receiver.HasPending, Is.False);
            Assert.That(replica.CoveredCount, Is.Zero);
            Assert.That(ReceiveAll(receiver, resetFull), Is.EqualTo(LabPaintReceive.Applied));
            Assert.That(receiver.Session, Is.EqualTo("next-session"));
            AssertReplica(source, replica);
        }

        [Test]
        public void StaleEpochSessionRoundAndSequenceCannotReplaceCommittedState()
        {
            var source = NewSurface();
            source.RegisterHit(Vector3.forward, Color.red, 7);
            var publisher = new LabPaintPublisher();
            var replica = NewSurface();
            var receiver = new LabPaintReceiver(replica);
            ReceiveAll(receiver, publisher.Publish(source, Session, Round, 10));
            LabPaintFrame template = publisher.Checkpoint()[0];
            Color32[] before = Pixels(replica);
            uint revision = replica.Revision;
            int covered = replica.CoveredCount, count = replica.PaintCount;
            LabPaintFrame staleEpoch = Clone(template); staleEpoch.epoch = 9;
            LabPaintFrame wrongSession = Clone(template); wrongSession.session = "another-session";
            LabPaintFrame wrongRound = Clone(template); wrongRound.round = 2;
            LabPaintFrame staleSequence = Clone(template); staleSequence.sequence = 1; staleSequence.epoch = 100;
            foreach (LabPaintFrame invalid in new[] { staleEpoch, wrongSession, wrongRound, staleSequence })
                Assert.That(receiver.Receive(invalid), Is.EqualTo(LabPaintReceive.Rejected));
            AssertUnchanged(replica, before, revision, covered, count);
            LabPaintFrame[] nextRound = publisher.Publish(source, Session, 2, 11);
            Assert.That(nextRound.All(frame => frame.full), Is.True);
            Assert.That(ReceiveAll(receiver, nextRound), Is.EqualTo(LabPaintReceive.Applied));
            LabPaintFrame oldRound = Clone(publisher.Checkpoint()[0]); oldRound.round = 1; oldRound.epoch = 12;
            Assert.That(receiver.Receive(oldRound), Is.EqualTo(LabPaintReceive.Rejected));
        }

        [Test]
        public void ANewerTransactionSupersedesPendingPartsAndOlderPartsCannotFinishIt()
        {
            var source = NewSurface();
            source.RegisterHit(Vector3.forward, Color.red, 7);
            var publisher = new LabPaintPublisher();
            LabPaintFrame[] older = publisher.Publish(source, Session, Round, 10);
            LabPaintFrame[] newer = publisher.Checkpoint();
            var replica = NewSurface();
            var receiver = new LabPaintReceiver(replica);
            Assert.That(receiver.Receive(older[0]), Is.EqualTo(LabPaintReceive.Pending));
            Assert.That(receiver.Receive(newer[0]), Is.EqualTo(LabPaintReceive.Pending));
            Assert.That(receiver.Receive(older[1]), Is.EqualTo(LabPaintReceive.Rejected));
            Assert.That(replica.CoveredCount, Is.Zero);
            Assert.That(ReceiveAll(receiver, newer.Skip(1).ToArray()), Is.EqualTo(LabPaintReceive.Applied));
            AssertReplica(source, replica);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConflictingPartDataOrHeadersDiscardTheTransactionWithoutCommitting(bool changeHeader)
        {
            var source = NewSurface();
            source.RegisterHit(Vector3.forward, Color.red, 7);
            var publisher = new LabPaintPublisher();
            LabPaintFrame[] frames = publisher.Publish(source, Session, Round, 10);
            var replica = NewSurface();
            var receiver = new LabPaintReceiver(replica);
            Color32[] before = Pixels(replica);
            uint revision = replica.Revision;
            Assert.That(receiver.Receive(frames[0]), Is.EqualTo(LabPaintReceive.Pending));
            LabPaintFrame bad = Clone(changeHeader ? frames[1] : frames[0]);
            if (changeHeader) bad.paintCount++;
            else bad.data = (bad.data[0] == 'A' ? "B" : "A") + bad.data.Substring(1);
            Assert.That(receiver.Receive(bad), Is.EqualTo(LabPaintReceive.NeedCheckpoint));
            Assert.That(receiver.HasPending, Is.False);
            AssertUnchanged(replica, before, revision, 0, 0);
            Assert.That(ReceiveAll(receiver, publisher.Checkpoint()), Is.EqualTo(LabPaintReceive.Applied));
        }

        [TestCase("session-null")]
        [TestCase("session-long")]
        [TestCase("round")]
        [TestCase("epoch")]
        [TestCase("sequence")]
        [TestCase("dimensions")]
        [TestCase("paint-count")]
        [TestCase("covered-negative")]
        [TestCase("covered-large")]
        [TestCase("parts-zero")]
        [TestCase("parts-large")]
        [TestCase("part-negative")]
        [TestCase("part-outside")]
        [TestCase("data-null")]
        [TestCase("data-large")]
        [TestCase("hash-null")]
        [TestCase("hash-short")]
        public void InvalidFrameMetadataIsRejectedBeforeAllocatingOrChangingTheReplica(string field)
        {
            var source = NewSurface();
            var replica = NewSurface();
            var receiver = new LabPaintReceiver(replica);
            LabPaintFrame frame = Clone(new LabPaintPublisher().Publish(source, Session, Round, 10)[0]);
            switch (field)
            {
                case "session-null": frame.session = null; break;
                case "session-long": frame.session = new string('s', 129); break;
                case "round": frame.round = 0; break;
                case "epoch": frame.epoch = 0; break;
                case "sequence": frame.sequence = 0; break;
                case "dimensions": frame.columns = 128; break;
                case "paint-count": frame.paintCount = -1; break;
                case "covered-negative": frame.coveredCount = -1; break;
                case "covered-large": frame.coveredCount = source.CellCount + 1; break;
                case "parts-zero": frame.parts = 0; break;
                case "parts-large": frame.parts = 65; break;
                case "part-negative": frame.part = -1; break;
                case "part-outside": frame.part = frame.parts; break;
                case "data-null": frame.data = null; break;
                case "data-large": frame.data = new string('A', LabPaintPublisher.FragmentCharacters + 1); break;
                case "hash-null": frame.hash = null; break;
                case "hash-short": frame.hash = new string('0', 63); break;
            }
            uint revision = replica.Revision;
            Assert.That(receiver.Receive(frame), Is.EqualTo(LabPaintReceive.Rejected));
            Assert.That(receiver.HasPending, Is.False);
            Assert.That(replica.Revision, Is.EqualTo(revision));
            Assert.That(replica.CoveredCount, Is.Zero);
            Assert.That(receiver.Session, Is.Null);
            Assert.That(receiver.Receive(null), Is.EqualTo(LabPaintReceive.Rejected));
        }

        [TestCase("hash")]
        [TestCase("base64")]
        [TestCase("full-short")]
        [TestCase("full-long")]
        [TestCase("alpha")]
        [TestCase("transparent-rgb")]
        [TestCase("covered-mismatch")]
        public void InvalidCompletedCheckpointRequestsRecoveryAndLeavesEveryPixelUnchanged(string corruption)
        {
            var source = NewSurface();
            var publisher = new LabPaintPublisher();
            LabPaintFrame[] frames = publisher.Publish(source, Session, Round, 10);
            byte[] payload = Convert.FromBase64String(string.Concat(frames.Select(frame => frame.data)));
            switch (corruption)
            {
                case "hash": foreach (LabPaintFrame frame in frames) frame.hash = new string('f', 64); break;
                case "base64": frames[0].data = "!" + frames[0].data.Substring(1); break;
                case "full-short": frames = Reframe(frames[0], payload.Take(payload.Length - 4).ToArray()); break;
                case "full-long": frames = Reframe(frames[0], payload.Concat(new byte[4]).ToArray()); break;
                case "alpha": payload[3] = 127; frames = Reframe(frames[0], payload); break;
                case "transparent-rgb": payload[0] = 1; frames = Reframe(frames[0], payload); break;
                case "covered-mismatch": foreach (LabPaintFrame frame in frames) frame.coveredCount = 1; break;
            }
            var replica = NewSurface();
            replica.RegisterHit(Vector3.back, Color.blue, 8);
            Color32[] before = Pixels(replica);
            uint revision = replica.Revision;
            int covered = replica.CoveredCount, count = replica.PaintCount;
            var receiver = new LabPaintReceiver(replica);
            Assert.That(ReceiveAll(receiver, frames), Is.EqualTo(LabPaintReceive.NeedCheckpoint));
            AssertUnchanged(replica, before, revision, covered, count);
            Assert.That(receiver.Session, Is.Null);
            Assert.That(receiver.HasPending, Is.False);
            Assert.That(ReceiveAll(receiver, publisher.Checkpoint()), Is.EqualTo(LabPaintReceive.Applied));
            AssertReplica(source, replica);
        }

        [TestCase("base")]
        [TestCase("revision")]
        [TestCase("length")]
        [TestCase("too-long")]
        [TestCase("outside")]
        [TestCase("duplicate")]
        [TestCase("unsorted")]
        [TestCase("dimensions")]
        public void InvalidDeltaCannotPartiallyApplyAndAFullCheckpointRecovers(string corruption)
        {
            var source = NewSurface();
            source.RegisterHit(Vector3.forward, Color.red, 7);
            var publisher = new LabPaintPublisher();
            var replica = NewSurface();
            var receiver = new LabPaintReceiver(replica);
            ReceiveAll(receiver, publisher.Publish(source, Session, Round, 10));
            Color32[] before = Pixels(replica);
            uint revision = replica.Revision;
            int covered = replica.CoveredCount, count = replica.PaintCount;
            source.RegisterHit(Vector3.back, Color.blue, 8);
            LabPaintFrame[] frames = publisher.Publish(source, Session, Round, 11);
            switch (corruption)
            {
                case "base": foreach (LabPaintFrame frame in frames) frame.fromRevision++; break;
                case "revision": foreach (LabPaintFrame frame in frames) frame.revision = frame.fromRevision - 1; break;
                case "length": frames = Reframe(frames[0], new byte[5]); break;
                case "too-long": frames = Reframe(frames[0], new byte[(source.CellCount + 1) * 6]); break;
                case "outside": frames = Reframe(frames[0], new byte[] { 0, 8, 1, 2, 3, 255 }); break;
                case "duplicate": frames = Reframe(frames[0], new byte[] { 1, 0, 1, 2, 3, 255, 1, 0, 3, 2, 1, 255 }); break;
                case "unsorted": frames = Reframe(frames[0], new byte[] { 2, 0, 1, 2, 3, 255, 1, 0, 3, 2, 1, 255 }); break;
                case "dimensions": foreach (LabPaintFrame frame in frames) { frame.columns = 128; frame.rows = 64; } break;
            }
            Assert.That(ReceiveAll(receiver, frames), Is.EqualTo(LabPaintReceive.NeedCheckpoint));
            AssertUnchanged(replica, before, revision, covered, count);
            Assert.That(receiver.HasPending, Is.False);
            Assert.That(ReceiveAll(receiver, publisher.Checkpoint()), Is.EqualTo(LabPaintReceive.Applied));
            AssertReplica(source, replica);
        }

        [Test]
        public void SnapshotMatchingIncludesSessionRoundDimensionsRevisionAndBothCounts()
        {
            var source = NewSurface();
            source.RegisterHit(Vector3.forward, Color.red, 7);
            var receiver = new LabPaintReceiver(NewSurface());
            ReceiveAll(receiver, new LabPaintPublisher().Publish(source, Session, Round, 10));
            LabSnapshot snapshot = Snapshot(source);
            Assert.That(receiver.Matches(snapshot), Is.True);
            Assert.That(receiver.Matches(null), Is.False);
            Action<LabSnapshot>[] edits =
            {
                value => value.sessionId = "other", value => value.round++,
                value => value.paintColumns++, value => value.paintRows++, value => value.paintRevision++,
                value => value.paintCoveredCount++, value => value.paintCellCount++, value => value.paintCount++
            };
            foreach (Action<LabSnapshot> edit in edits)
            {
                LabSnapshot changed = JsonUtility.FromJson<LabSnapshot>(JsonUtility.ToJson(snapshot));
                edit(changed);
                Assert.That(receiver.Matches(changed), Is.False);
            }
        }

        private static LabPaintSurface NewSurface(int resolution = 0)
            => new LabPaintSurface(new LabPaintGrowthSettings { Pattern = LabPaintPattern.Legacy, Resolution = resolution }, 42u);

        private static Color32[] Pixels(LabPaintSurface surface)
        {
            var pixels = new Color32[surface.CellCount];
            surface.CopyPixels(pixels);
            return pixels;
        }

        private static LabSnapshot Snapshot(LabPaintSurface surface) => new LabSnapshot
        {
            sessionId = Session, round = Round, paintColumns = surface.Columns, paintRows = surface.Rows,
            paintRevision = surface.Revision, paintCoveredCount = surface.CoveredCount,
            paintCellCount = surface.CellCount, paintCount = surface.PaintCount
        };

        private static LabPaintFrame Clone(LabPaintFrame frame)
            => JsonUtility.FromJson<LabPaintFrame>(JsonUtility.ToJson(frame));

        private static LabPaintReceive ReceiveAll(LabPaintReceiver receiver, LabPaintFrame[] frames)
        {
            Assert.That(frames, Is.Not.Empty);
            LabPaintReceive result = LabPaintReceive.Rejected;
            for (int i = 0; i < frames.Length; i++)
            {
                result = receiver.Receive(frames[i]);
                if (i < frames.Length - 1) Assert.That(result, Is.EqualTo(LabPaintReceive.Pending), "Part " + i);
            }
            return result;
        }

        private static void AssertReplica(LabPaintSurface source, LabPaintSurface replica)
        {
            Assert.That(replica.Columns, Is.EqualTo(source.Columns));
            Assert.That(replica.Rows, Is.EqualTo(source.Rows));
            Assert.That(replica.Revision, Is.EqualTo(source.Revision));
            Assert.That(replica.CoveredCount, Is.EqualTo(source.CoveredCount));
            Assert.That(replica.PaintCount, Is.EqualTo(source.PaintCount));
            Assert.That(Pixels(replica), Is.EqualTo(Pixels(source)));
        }

        private static void AssertUnchanged(LabPaintSurface surface, Color32[] pixels, uint revision, int covered, int count)
        {
            Assert.That(Pixels(surface), Is.EqualTo(pixels));
            Assert.That(surface.Revision, Is.EqualTo(revision));
            Assert.That(surface.CoveredCount, Is.EqualTo(covered));
            Assert.That(surface.PaintCount, Is.EqualTo(count));
        }

        private static LabPaintFrame[] Reframe(LabPaintFrame template, byte[] payload)
        {
            string encoded = Convert.ToBase64String(payload);
            int parts = Math.Max(1, (encoded.Length + LabPaintPublisher.FragmentCharacters - 1) / LabPaintPublisher.FragmentCharacters);
            var frames = new LabPaintFrame[parts];
            string hash;
            using (var sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(payload)).Replace("-", string.Empty).ToLowerInvariant();
            for (int i = 0; i < parts; i++)
            {
                LabPaintFrame frame = Clone(template);
                frame.parts = parts; frame.part = i;
                frame.data = encoded.Substring(i * LabPaintPublisher.FragmentCharacters,
                    Math.Min(LabPaintPublisher.FragmentCharacters, encoded.Length - i * LabPaintPublisher.FragmentCharacters));
                if (frame.full) frame.hash = hash;
                frames[i] = frame;
            }
            return frames;
        }
    }
}
