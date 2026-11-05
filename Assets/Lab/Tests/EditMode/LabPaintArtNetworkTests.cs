using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>Network integration fences; no sockets or GPU are faked as a successful playtest.</summary>
    public sealed class LabPaintArtNetworkTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void HostObservesActualHitCommitsOnceAndSealsOnlyTheMatchingSurface()
        {
            using (var fixture = new Fixture())
            {
                fixture.Begin();
                LabPaintArtJournal journal = fixture.Network.PaintArtJournal;
                Assert.That(journal, Is.SameAs(fixture.Model.PaintSurface.ArtJournal));
                Assert.That(journal.Error, Is.Null);
                Assert.That(fixture.Network.PaintArtInitialReady, Is.False, "The initial page is not published yet.");
                fixture.Commit();
                Assert.That(fixture.Network.PaintArtInitialReady, Is.True);
                ulong committed = journal.LastSequence; int pages = journal.PageCount;
                fixture.Commit();
                Assert.That(journal.LastSequence, Is.EqualTo(committed), "An unchanged publish must not create a commit loop.");
                Assert.That(journal.PageCount, Is.EqualTo(pages));
                Assert.That(fixture.Model.PaintSurface.RegisterHit(Vector3.forward, Color.blue, 1, 2), Is.True);
                fixture.Commit();
                Assert.That(journal.LastSequence, Is.GreaterThan(committed));
                Assert.That(journal.CommitSurfaceHash, Is.EqualTo(Hash(fixture.Model.PaintSurface)));
                Assert.That(journal.CommitRevision, Is.EqualTo(fixture.Model.PaintSurface.Revision));
                fixture.Latest.phase = LabPhase.Defeat;
                fixture.Invoke("CommitPaintArt", fixture.Latest);
                Assert.That(journal.IsSealed, Is.True);
                Assert.That(journal.SealSurfaceHash, Is.EqualTo(Hash(fixture.Model.PaintSurface)));
                Assert.That(journal.SealRevision, Is.EqualTo(fixture.Model.PaintSurface.Revision));
            }
        }

        [Test]
        public void ReceivingApplyingAndRenderingAreDistinctAndOldRoundCannotBeReady()
        {
            using (var fixture = new Fixture())
            {
                fixture.Begin();
                fixture.Model.PaintSurface.RegisterHit(Vector3.forward, Color.red, 0, 1);
                fixture.Commit(); fixture.Latest.phase = LabPhase.Victory;
                fixture.Invoke("CommitPaintArt", fixture.Latest);
                var journal = fixture.Network.PaintArtJournal;
                Assert.That(fixture.Network.PaintSurface.TryCopyReplicaFrom(fixture.Model.PaintSurface,
                    fixture.Model.PaintSurface.PaintCount), Is.True);
                Assert.That(fixture.Network.PaintArtCompletionReady, Is.False, "Stored pages alone are not a finished GPU image.");
                fixture.Network.ReportPaintArtRendered(journal.LastSequence, journal.LastSequence - 1);
                Assert.That(fixture.Network.PaintArtCompletionReady, Is.False, "Applied operations are not a completed GPU fence.");
                fixture.Network.ReportPaintArtRendered(journal.LastSequence, journal.LastSequence);
                Assert.That(fixture.Network.PaintArtCompletionReady, Is.True);
                // Recreating GPU targets legitimately rewinds presentation readiness.
                fixture.Network.ReportPaintArtRendered(0, 0);
                Assert.That(fixture.Network.PaintArtCompletionReady, Is.False);
                fixture.Network.ReportPaintArtRendered(journal.LastSequence, journal.LastSequence);
                fixture.Latest.round++;
                Assert.That(fixture.Network.PaintArtInitialReady, Is.False, "Prior-round pages cannot release the next round.");
                Assert.That(fixture.Network.PaintArtCompletionReady, Is.False);
            }
        }

        [Test]
        public void RenderingRecoveryRequiresFreshFencesAndCannotClearARecordingFailure()
        {
            using (var fixture = new Fixture())
            {
                fixture.Begin();
                fixture.Model.PaintSurface.RegisterHit(Vector3.forward, Color.red, 0, 1);
                fixture.Commit(); fixture.Latest.phase = LabPhase.Victory;
                fixture.Invoke("CommitPaintArt", fixture.Latest);
                var journal = fixture.Network.PaintArtJournal;
                fixture.Network.PaintSurface.TryCopyReplicaFrom(fixture.Model.PaintSurface,
                    fixture.Model.PaintSurface.PaintCount);
                fixture.Network.ReportPaintArtRendered(journal.LastSequence, journal.LastSequence);
                Assert.That(fixture.Network.PaintArtCompletionReady, Is.True);
                fixture.Network.ReportPaintArtRenderingError("GPU resource allocation fixture");
                Assert.That(fixture.Network.PaintArtError, Does.Contain("GPU resource"));
                Assert.That(fixture.Network.PaintArtCompletionReady, Is.False);
                fixture.Network.ReportPaintArtRendered(0, 0);
                fixture.Network.ReportPaintArtRenderingError(null);
                Assert.That(fixture.Network.PaintArtError, Is.Null);
                Assert.That(fixture.Network.PaintArtCompletionReady, Is.False,
                    "Clearing the allocation error does not stand in for replaying and finishing the new GPU image.");
                fixture.Network.ReportPaintArtRendered(journal.LastSequence, journal.LastSequence);
                Assert.That(fixture.Network.PaintArtCompletionReady, Is.True);
                journal.Fail("permanent disk fixture");
                fixture.Network.ReportPaintArtRenderingError(null);
                Assert.That(fixture.Network.PaintArtError, Does.Contain("permanent disk"));
                Assert.That(fixture.Network.PaintArtCompletionReady, Is.False,
                    "A renderer retry cannot recover operations that the recorder did not preserve.");
            }
        }

        [Test]
        public void SurfaceHashMismatchOrStorageFailureCannotProduceAnExactCompletedArtwork()
        {
            using (var fixture = new Fixture())
            {
                fixture.Begin();
                fixture.Model.PaintSurface.RegisterHit(Vector3.forward, Color.red, 0, 1);
                fixture.Commit(); fixture.Latest.phase = LabPhase.Victory;
                fixture.Invoke("CommitPaintArt", fixture.Latest);
                var journal = fixture.Network.PaintArtJournal;
                fixture.Network.ReportPaintArtRendered(journal.LastSequence, journal.LastSequence);
                var source = fixture.Model.PaintSurface;
                // A valid shape/revision with different authoritative bytes must fail the final digest gate.
                Assert.That(fixture.Network.PaintSurface.TryReplacePixels(source.Columns, source.Rows, source.Revision,
                    new Color32[source.CellCount], new byte[source.CellCount], source.PaintCount,
                    source.CopyVisualStamps()), Is.True);
                Assert.That(fixture.Network.PaintArtCompletionReady, Is.False);
                journal.Fail("disk full fixture");
                Assert.That(fixture.Network.PaintArtError, Does.Contain("disk full"));
                Assert.That(fixture.Network.PaintArtCompletionReady, Is.False);
                Assert.That(fixture.Network.PaintArtInitialReady, Is.True,
                    "Storage failure remains visible but must not change gameplay admission like a version mismatch.");
            }
        }

        [Test]
        public void BadStyleIsRejectedBeforeImportAndCannotReleaseTheInitialGate()
        {
            using (var fixture = new Fixture())
            {
                fixture.PrepareGuest();
                string path = Path.Combine(Path.GetTempPath(), "art-wrong-style-" + Guid.NewGuid().ToString("N"));
                using (var host = new LabPaintArtJournal(path, fixture.Latest.sessionId + "/" + fixture.Latest.round,
                    256, 128, "wrong-style"))
                {
                    host.Flush(); host.TryReadPage(0, out byte[] raw);
                    var page = LabPaintArtTransfer.Encode(host.Epoch, host.StyleVersion, host.Columns, host.Rows, 0, raw);
                    fixture.Deliver(page);
                }
                Assert.That(fixture.Network.PaintArtError, Does.Contain("version"));
                Assert.That(fixture.Network.PaintArtJournal, Is.Null);
                Assert.That(fixture.Network.PaintArtInitialReady, Is.False);
                Assert.That(fixture.Network.PaintArtCompletionReady, Is.False);
            }
        }

        [Test]
        public void ANewRoundRecoversAfterThePreviousRoundFailedBeforeCreatingAJournal()
        {
            using (var fixture = new Fixture())
            {
                fixture.PrepareGuest();
                string badPath = Path.Combine(Path.GetTempPath(), "art-prior-bad-" + Guid.NewGuid().ToString("N"));
                using (var badHost = new LabPaintArtJournal(badPath,
                    fixture.Latest.sessionId + "/" + fixture.Latest.round, 256, 128, "wrong-style"))
                {
                    badHost.Flush(); badHost.TryReadPage(0, out var raw);
                    fixture.Deliver(LabPaintArtTransfer.Encode(badHost.Epoch, badHost.StyleVersion, 256, 128, 0, raw));
                }
                Assert.That(fixture.Network.PaintArtJournal, Is.Null);
                Assert.That(fixture.Network.PaintArtError, Does.Contain("version"));
                Assert.That(fixture.Network.PaintArtInitialReady, Is.False);
                fixture.ReplaceModel("recovered-round");
                string path = Path.Combine(Path.GetTempPath(), "art-recovered-" + Guid.NewGuid().ToString("N"));
                string style = LabWatercolorStampSettings.CaptureDefault().Version;
                using (var host = new LabPaintArtJournal(path,
                    fixture.Latest.sessionId + "/" + fixture.Latest.round, 256, 128, style))
                {
                    host.Flush(); host.TryReadPage(0, out var raw);
                    fixture.Deliver(LabPaintArtTransfer.Encode(host.Epoch, host.StyleVersion, 256, 128, 0, raw));
                }
                Assert.That(fixture.Network.PaintArtError, Is.Null, "A previous round's error must not poison the new round.");
                Assert.That(fixture.Network.PaintArtJournal.Epoch,
                    Is.EqualTo(fixture.Latest.sessionId + "/" + fixture.Latest.round));
                Assert.That(fixture.Network.PaintArtInitialReady, Is.True);
                Assert.That(fixture.Network.PaintArtCompletionReady, Is.False, "An initial page is still not a completed artwork.");
            }
        }

        [Test]
        public void AnArtworkFailurePreservesACompletedCpuResultButDoesNotEnableItsPreview()
        {
            using (var fixture = new Fixture())
            {
                // Complete an actual Host surface through the ordinary funded paint path.
                fixture.Model.PaintSurface.Reset(new LabPaintGrowthSettings
                    { Resolution = 0, Pattern = LabPaintPattern.Legacy, InitialPercent = 100f }, 17);
                fixture.Begin();
                Assert.That(fixture.Model.PaintSurface.RegisterHit(Vector3.forward, Color.blue, 0, 1), Is.True);
                Assert.That(fixture.Model.PaintSurface.IsComplete, Is.True);
                fixture.Commit(); fixture.Latest.phase = LabPhase.Victory;
                fixture.Invoke("CommitPaintArt", fixture.Latest);
                Assert.That(fixture.Network.PaintSurface.TryCopyReplicaFrom(fixture.Model.PaintSurface,
                    fixture.Model.PaintSurface.PaintCount), Is.True);
                fixture.MakeConnectedHostForReadinessOnly();
                fixture.Network.PaintArtJournal.Fail("disk failure fixture");
                Assert.That(LabGourdResultPreview.SurfaceMatches(fixture.Network, out var cpuError,
                    requireExactArtwork: false), Is.True, cpuError);
                Assert.That(LabGourdResultPreview.SurfaceMatches(fixture.Network, out var artError), Is.False);
                Assert.That(artError, Does.Contain("disk failure"));
                Assert.That(LabGourdResultPreview.TryCreate(fixture.Network, out var preview, out _), Is.False);
                Assert.That(preview, Is.Null);
                Assert.That(fixture.Latest.phase, Is.EqualTo(LabPhase.Victory));
                Assert.That(fixture.Network.PaintSurface.IsComplete, Is.True);
            }
        }

        [Test]
        public void NewRoundReleasesOldFilesAndResetReleasesTheCurrentRoundWithoutChangingConfig()
        {
            using (var fixture = new Fixture())
            {
                fixture.Begin(); fixture.Commit();
                var first = fixture.Network.PaintArtJournal; string oldPath = fixture.Get<string>("paintArtPath");
                Assert.That(File.Exists(oldPath), Is.True);
                string configBefore = JsonUtility.ToJson(fixture.Config);
                fixture.ReplaceModel("second-round"); fixture.Begin(); fixture.Commit();
                Assert.That(fixture.Network.PaintArtJournal, Is.Not.SameAs(first));
                Assert.That(File.Exists(oldPath), Is.False);
                Assert.That(File.Exists(oldPath + ".strokes"), Is.False);
                Assert.That(first.TryReadPage(0, out _), Is.False);
                string secondPath = fixture.Get<string>("paintArtPath");
                fixture.Invoke("ResetPaintArt");
                Assert.That(fixture.Network.PaintArtJournal, Is.Null);
                Assert.That(File.Exists(secondPath), Is.False);
                Assert.That(File.Exists(secondPath + ".strokes"), Is.False);
                Assert.That(fixture.Model.PaintSurface.ArtJournal, Is.Null);
                Assert.That(JsonUtility.ToJson(fixture.Config), Is.EqualTo(configBefore));
            }
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void DisabledArtworkDoesNotSelectTheNewGameplayContour(bool enabled, bool highResolution)
        {
            var profile = Resources.Load<LabWatercolorStampProfile>(LabWatercolorStampProfile.ResourcePath);
            bool original = profile.settings.enabled;
            try
            {
                profile.settings.enabled = enabled;
                using (var fixture = new Fixture())
                {
                    typeof(LabConfig).GetField("highResolutionPaintEnabled", Private).SetValue(fixture.Config, highResolution);
                    typeof(LabNetwork).GetMethod("BeginPaintArt", Private).Invoke(fixture.Network, null);
                    Assert.That(fixture.Network.RequiresPaintArt, Is.False);
                    Assert.That(fixture.Model.PaintSurface.UsesPaperStickerContour, Is.False,
                        "The old rendering path must retain its existing logical contour while stamps are disabled.");
                }
            }
            finally { profile.settings.enabled = original; }
        }

        [Test]
        public void ProfileSettingsArePinnedAsACopyForTheRound()
        {
            using (var fixture = new Fixture())
            {
                fixture.Begin();
                string pinned = fixture.Network.PinnedWatercolorSettings.Version;
                var authored = Resources.Load<LabWatercolorStampProfile>(LabWatercolorStampProfile.ResourcePath);
                Assert.That(authored, Is.Not.Null);
                float oldDensity = authored.settings.density;
                try
                {
                    authored.settings.density = oldDensity > .5f ? .2f : .9f;
                    Assert.That(LabWatercolorStampSettings.CaptureDefault().Version, Is.Not.EqualTo(pinned));
                    Assert.That(fixture.Network.PinnedWatercolorSettings.Version, Is.EqualTo(pinned));
                    Assert.That(fixture.Network.PaintArtJournal.StyleVersion, Is.EqualTo(pinned));
                }
                finally { authored.settings.density = oldDensity; }
            }
        }

        private static string Hash(LabPaintSurface surface)
        {
            var pixels = new Color32[surface.CellCount]; var painters = new byte[surface.CellCount];
            surface.CopyPixels(pixels); surface.CopyPainterSlots(painters);
            return LabPaintPublisher.Hash(pixels, painters);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root;
            private Component fakeManager;
            private Type managerType;
            public readonly LabNetwork Network;
            public readonly LabConfig Config;
            public LabModel Model { get; private set; }
            public LabSnapshot Latest { get; private set; }
            public Fixture()
            {
                root = new GameObject("Artwork journal integration fixture"); root.SetActive(false);
                Network = root.AddComponent<LabNetwork>(); Config = ScriptableObject.CreateInstance<LabConfig>();
                Network.config = Config; ReplaceModel("art-round");
            }
            public void ReplaceModel(string session)
            {
                Model = new LabModel(); Model.Begin(session, new ulong[] { 0, 1 }, 0d, Config);
                Latest = Model.Snapshot(0d);
                Set("model", Model); Set("sessionId", session); Set("latest", Latest);
            }
            public void Begin()
            {
                Invoke("BeginPaintArt");
                Assert.That(Network.RequiresPaintArt, Is.True, "This fixture must exercise the enabled cooperative profile.");
            }
            public void Commit()
            {
                Latest = Model.Snapshot(0d); Set("latest", Latest); Invoke("CommitPaintArt", Latest);
            }
            public void PrepareGuest()
            {
                Set("clientConfigReady", true); Set("configRevision", 7); Set("configFingerprint", "settings");
                Set("pinnedWatercolorSettings", LabWatercolorStampSettings.CaptureDefault());
            }
            public void Deliver(LabPaintArtPage page)
            {
                Type packetType = typeof(LabNetwork).Assembly.GetType("C6Lab.LabNetworkPacket", true);
                object packet = Activator.CreateInstance(packetType);
                void Put(string name, object value) => packetType.GetField(name).SetValue(packet, value);
                Put("sessionId", Latest.sessionId); Put("round", Latest.round); Put("configRevision", 7);
                Put("configFingerprint", "settings"); Put("payload", JsonUtility.ToJson(page));
                Invoke("ReceivePaintArtPage", packet);
            }
            // Only the existing readiness predicate needs a connected transport flag. No
            // socket is opened and this fixture never claims a successful multiplayer session.
            public void MakeConnectedHostForReadinessOnly()
            {
                managerType = typeof(LabNetwork).GetField("manager").FieldType;
                fakeManager = root.AddComponent(managerType);
                typeof(LabNetwork).GetField("manager").SetValue(Network, fakeManager);
                object client = managerType.GetProperty("LocalClient").GetValue(fakeManager);
                client.GetType().GetProperty("IsServer", Private).SetValue(client, true);
                client.GetType().GetProperty("IsClient", Private).SetValue(client, true);
                managerType.GetProperty("IsListening").SetValue(fakeManager, true);
                Assert.That(Network.IsConnected, Is.True);
            }
            public void Set(string name, object value) => typeof(LabNetwork).GetField(name, Private).SetValue(Network, value);
            public T Get<T>(string name) => (T)typeof(LabNetwork).GetField(name, Private).GetValue(Network);
            public void Invoke(string name, params object[] arguments)
                => typeof(LabNetwork).GetMethod(name, Private).Invoke(Network, arguments);
            public void Dispose()
            {
                Invoke("ResetPaintArt");
                if (fakeManager != null) managerType.GetProperty("IsListening").SetValue(fakeManager, false);
                UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(Config);
            }
        }
    }
}
