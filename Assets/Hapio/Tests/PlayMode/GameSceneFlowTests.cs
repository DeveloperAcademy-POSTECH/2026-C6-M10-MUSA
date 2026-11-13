using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using C6Lab;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hapio.Tests
{
    /// <summary>
    /// Saved game Scenes and saved persistent-session prefab integration. Terminal victories
    /// use an explicit CPU-map fixture completion, not simulated projectile collision: throw
    /// and contact correctness remain the dedicated Lab projectile suites' responsibility.
    /// </summary>
    [Category("Slow")]
    public sealed class GameSceneFlowTests
    {
        private GameSession session;
        private LabNetwork network;
        private GameFigmaStrokeLibrary.Surface returnBackgroundEntry;
        private Sprite returnBackgroundSprite;
        // The receiver is test-owned; the real writer remains attached to the Host model.
        private LabPaintArtJournal delayedArtworkWriter, delayedArtworkReceiver;
        private LabGourdResultPreview comparisonPreview;
        private LabConfig sharedPresentationConfig;
        private LabOrbAppearance sharedPresentationOverride;

        [UnitySetUp]
        public IEnumerator LoadSavedHome()
        {
            sharedPresentationConfig = Resources.Load<LabConfig>("LabConfig");
            Assert.That(sharedPresentationConfig, Is.Not.Null);
            sharedPresentationOverride = (LabOrbAppearance)typeof(LabConfig)
                .GetField("orbPresentationOverride", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(sharedPresentationConfig);
            Assert.That(GameSession.Instance, Is.Null, "The previous fixture must release its persistent game session.");
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home, LoadSceneMode.Single);
            yield return Until(() => GameSession.Instance != null, "Saved Home must bootstrap its saved session prefab.");
            session = GameSession.Instance;
            network = session.Network;
            yield return Until(() => !session.IsLoadingScene, "Home bootstrap must finish loading.");
            // DEV/solo can start from the title; the first normal Start owns the local opening.
            yield return null;
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Home));
            Assert.That(network, Is.Not.Null);
            Assert.That(network.GameFlowEnabled, Is.True);
            Assert.That(network.IsConnected, Is.False);
            network.port = AvailableDualFamilyPort();
            float[] values = network.config.CaptureDeveloperValues();
            Set(values, "Battle Duration (seconds)", 600f);
            Set(values, "Player Max HP", 237f);
            Set(values, "Starting Stamina", 65f);
            Set(values, "Stamina Recovery Amount", 0f);
            Set(values, "Orb Generation Cost", 20f);
            Set(values, "Yin Chance (%)", 100f);
            Set(values, "Yang Chance (%)", 0f);
            Set(values, "Relay Orb Chance (%)", 0f);
            Set(values, "Secret Box Chance (%)", 0f);
            Set(values, "Potion Chance (%)", 0f);
            Set(values, "Bomb Chance (%)", 0f);
            Set(values, "Paint Surface Resolution (0 low 1 medium 2 high)", 0f);
            Set(values, "Paint Initial Coverage (%)", 100f);
            Set(values, "Paint Pattern", 0f);
            Set(values, "Paint Drying (0 off, 1 on)", 0f);
            Set(values, "Completed Gourd Viewer (0 off, 1 on)", 1f);
            Assert.That(session.ApplyDeveloperSettings(values, out string error), Is.True, error);
        }

        [UnityTearDown]
        public IEnumerator ReleaseOwnedSession()
        {
            try
            {
                RestoreReturnBackgroundSource();
                RestoreDelayedArtworkJournal();
                comparisonPreview?.Dispose(); comparisonPreview = null;
                Time.timeScale = 1f;
                // A test that ran the production countdown may have failed inside a nested yield, skipping its finally.
                GameTestStartTiming.UseShort();
                if (session != null)
                {
                    // Destroying the session cannot cancel a LoadSceneAsync already handed
                    // to Unity. Drain its current handoff before a destination can bootstrap a
                    // replacement singleton behind the next fixture's SetUp assertion.
                    session.enabled = true;
                    double deadline = Time.realtimeSinceStartupAsDouble + 20d;
                    while (session.IsLoadingScene && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                    network.Stop();
                    Object.Destroy(session.gameObject);
                    yield return null;
                    Assert.That(GameSession.Instance, Is.Null);
                }
                Scene current = SceneManager.GetActiveScene();
                Scene cleanup = SceneManager.CreateScene("Hapio game fixture cleanup " + Guid.NewGuid().ToString("N"));
                SceneManager.SetActiveScene(cleanup);
                if (current.IsValid() && current.isLoaded) yield return SceneManager.UnloadSceneAsync(current);
            }
            finally
            {
                // Battle UI applies a transient appearance to the shared Resource in memory.
                // Restore the exact incoming override after all Scene bindings have stopped,
                // including when teardown itself reports an assertion failure.
                if (sharedPresentationConfig != null)
                    sharedPresentationConfig.SetOrbPresentationOverride(sharedPresentationOverride);
                sharedPresentationConfig = null; sharedPresentationOverride = null;
            }
            session = null;
            network = null;
            yield return null;
        }

        [Category("GPU")]
        [UnityTest]
        public IEnumerator JigwiResultFinishesItsActualArtworkAfterBattleUnloadWhenFinalPagesArriveLate()
        {
            yield return StartJigwiWithDelayedArtwork();
            GameObject source = network.target.GetComponent<LabGourdPaint>().VisualInstance;
            LabSnapshot terminal = network.Snapshot;
            string battle = terminal.sessionId;
            uint round = terminal.round;
            Color32[] accepted = new Color32[network.PaintSurface.CellCount];
            network.PaintSurface.CopyPixels(accepted);
            byte[] painters = new byte[accepted.Length];
            network.PaintSurface.CopyPainterSlots(painters);
            ExpireOnlyTheFinishPresentationBudget();
            yield return WaitResultStory();
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Completed));
            Assert.That(session.State.stageIndex, Is.EqualTo(3));
            Assert.That(network.target, Is.Null, "The result must not keep the live Battle target or its scripts running.");
            Assert.That(source == null, Is.True, "The original display hierarchy must have unloaded with Battle.");
            Assert.That(session.GourdViewerState, Is.EqualTo(GameGourdViewerState.Pending));
            Assert.That(session.PreviewTexture, Is.Null, "An incomplete journal is not a completed painting.");
            Assert.That(session.IsViewerOpen, Is.False);
            session.OpenGourdViewer();
            Assert.That(session.IsViewerOpen, Is.False, "Pending input cannot expose an unfinished or previous-stage picture.");
            Assert.That(delayedArtworkReceiver.IsSealed, Is.False);

            DeliverDelayedArtworkPages();
            yield return Until(() => session.GourdViewerState == GameGourdViewerState.Ready,
                "The retained model and real journal must render after the Battle target has gone: " + session.GourdViewerError);
            Assert.That(session.PreviewTexture, Is.Not.Null);
            var preview = (LabGourdResultPreview)typeof(GameSession).GetField("preview",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
            Assert.That(preview.SessionId, Is.EqualTo(battle));
            Assert.That(preview.Round, Is.EqualTo(round));
            Assert.That(preview.PaintTexture.GetPixels32(), Is.EqualTo(accepted),
                "Background preparation must preserve the exact authoritative final cells.");
            Assert.That(preview.PainterTexture.GetPixels32().Select(pixel => pixel.r).ToArray(), Is.EqualTo(painters));
            Assert.That(preview.VisualHeightTexture, Is.Not.Null, "The real clay relief must be recovered with the color artwork.");
            Assert.That(preview.ModelRoot.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(preview.ModelRoot.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(preview.ModelRoot.GetComponentsInChildren<AudioSource>(true), Is.Empty);
            yield return Until(() => !ResultStory().player.UserInputBlocked && !session.SceneFade.BlocksInput,
                "The restored card must finish the normal result input shield.");
            var card = ResultStory().player.CurrentView;
            var flip = card.GetComponentInChildren<StoryCardFlip>(true);
            flip.Flip();
            var inspect = card.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                .Single(button => button.name == "Inspect Gourd Action");
            yield return Until(() => flip.IsBack && !flip.IsFlipping && inspect.IsInteractable(),
                "The existing Jigwi card button must become usable when its real picture is ready.");
            inspect.onClick.Invoke();
            Assert.That(session.IsViewerOpen, Is.True);
            session.CloseGourdViewer();
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Completed));
            Assert.That(session.State.stageIndex, Is.EqualTo(3));
            Assert.That(session.ResultSnapshot.sessionId, Is.EqualTo(battle));
        }

        [Category("GPU")]
        [UnityTest]
        public IEnumerator LeavingAResultDisposesPendingArtworkAndLatePagesCannotReopenTheViewer()
        {
            yield return StartJigwiWithDelayedArtwork();
            ExpireOnlyTheFinishPresentationBudget();
            yield return WaitResultStory();
            Assert.That(session.GourdViewerState, Is.EqualTo(GameGourdViewerState.Pending));
            var preparation = (LabGourdPreviewPreparation)typeof(GameSession).GetField("viewerPreparation",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
            Assert.That(preparation, Is.Not.Null);
            yield return Until(() => session.CanReturnHome, "A preserved final clear must still allow Home while optional art is pending.");
            session.GoHome();
            yield return WaitHome();
            Assert.That(session.GourdViewerState, Is.EqualTo(GameGourdViewerState.Disabled));
            Assert.That(session.PreviewTexture, Is.Null);
            Assert.That(preparation.TakePreview(), Is.Null, "Cancelled preparation cannot hand out a stale result.");
            Assert.That(delayedArtworkWriter.TryReadPage(0, out byte[] latePage), Is.True);
            Assert.That(delayedArtworkReceiver.TryImportPage(0, latePage), Is.False,
                "The previous Network round owns and disposes its receiver on Home.");
            yield return null;
            yield return null;
            Assert.That(session.GourdViewerState, Is.EqualTo(GameGourdViewerState.Disabled));
            Assert.That(session.PreviewTexture, Is.Null);
            Assert.That(session.IsViewerOpen, Is.False);
            session.StartSolo();
            yield return WaitPlaying(1);
            Assert.That(session.PreviewTexture, Is.Null, "A new room must never adopt the old final artwork.");
            Assert.That(session.IsViewerOpen, Is.False);
        }

        [Category("GPU")]
        [UnityTest]
        public IEnumerator ACorruptFinalArtworkReportsFailureButPreservesTheJigwiClearAndHomeAction()
        {
            yield return StartJigwiWithDelayedArtwork();
            delayedArtworkReceiver.Fail("Fixture final artwork storage failure");
            yield return Until(() => session.GourdViewerState == GameGourdViewerState.Failed,
                "A permanent journal error must stop preparation rather than remain Pending.");
            Assert.That(session.GourdViewerError, Does.Contain("Fixture final artwork storage failure"));
            ExpireOnlyTheFinishPresentationBudget();
            yield return WaitResultStory();
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Completed));
            Assert.That(session.ResultSnapshot.phase, Is.EqualTo(LabPhase.Victory));
            Assert.That(session.ResultSnapshot.paintCoveredCount, Is.EqualTo(session.ResultSnapshot.paintCellCount));
            Assert.That(session.GourdViewerState, Is.EqualTo(GameGourdViewerState.Failed));
            Assert.That(session.PreviewTexture, Is.Null);
            Assert.That(session.IsLoadingScene, Is.False);
            session.OpenGourdViewer();
            Assert.That(session.IsViewerOpen, Is.False);
            yield return Until(() => session.CanReturnHome, "An optional artwork failure must not remove the final Home action.");
            session.GoHome();
            yield return WaitHome();
        }

        [Category("GPU")]
        [UnityTest]
        public IEnumerator AStalledResultArtworkFailsAfterItsIdleBudgetWithoutRestartingSceneLoading()
        {
            yield return StartJigwiWithDelayedArtwork();
            ExpireOnlyTheFinishPresentationBudget();
            yield return WaitResultStory();
            var preparation = (LabGourdPreviewPreparation)typeof(GameSession).GetField("viewerPreparation",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
            yield return Until(() => preparation.AppliedThrough == delayedArtworkReceiver.LastSequence
                && preparation.RenderedThrough == delayedArtworkReceiver.LastSequence,
                "The received prefix must finish its real bounded GPU work before testing a genuinely idle wait.");
            Assert.That(preparation.Ready, Is.False, "A rendered prefix is still not the sealed final painting.");
            double now = Time.realtimeSinceStartupAsDouble;
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(GameSession).GetField("viewerLastSequence", fields).SetValue(session, delayedArtworkReceiver.LastSequence);
            typeof(GameSession).GetField("viewerApplied", fields).SetValue(session, preparation.AppliedThrough);
            typeof(GameSession).GetField("viewerRendered", fields).SetValue(session, preparation.RenderedThrough);
            typeof(GameSession).GetField("viewerProgressAt", fields).SetValue(session, now);
            typeof(GameSession).GetMethod("UpdateGourdViewerPreparation", fields).Invoke(session, new object[] { now + 31d });
            Assert.That(session.GourdViewerState, Is.EqualTo(GameGourdViewerState.Failed));
            Assert.That(session.GourdViewerError, Does.Contain("stopped making progress"));
            Assert.That(session.IsLoadingScene, Is.False, "The idle budget never introduces another full-screen wait.");
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Completed));
            Assert.That(session.ResultSnapshot.phase, Is.EqualTo(LabPhase.Victory));
            Assert.That(preparation.TakePreview(), Is.Null);
            Assert.That(session.PreviewTexture, Is.Null);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(GameScenePaths.StageResult));
        }

        [Category("GPU")]
        [UnityTest]
        public IEnumerator ADisposedBorrowedJournalFailsPendingViewingWithoutWaitingForTheIdleTimeout()
        {
            yield return StartJigwiWithDelayedArtwork();
            ExpireOnlyTheFinishPresentationBudget();
            yield return WaitResultStory();
            Assert.That(delayedArtworkReceiver.Error, Is.Null);
            delayedArtworkReceiver.Dispose();
            Assert.That(delayedArtworkReceiver.Error, Is.Null,
                "Journal disposal need not set Error, so preparation must test its borrowed lifetime too.");
            yield return Until(() => session.GourdViewerState == GameGourdViewerState.Failed,
                "A disposed borrowed record is a permanent error, not another 30-second receive wait.");
            Assert.That(session.GourdViewerError, Does.Contain("no longer available"));
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Completed));
            Assert.That(session.IsLoadingScene, Is.False);
            Assert.That(session.PreviewTexture, Is.Null);
            Assert.That(typeof(GameSession).GetField("viewerPreparation", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(session), Is.Null, "The failed background GPU/template owner must be released.");
        }

        [Category("GPU")]
        [UnityTest]
        public IEnumerator DeferredJigwiArtworkMatchesTheLiveFastCaptureForColorOwnerAndClayRelief()
        {
            yield return StartJigwiWithDelayedArtwork();
            var preparation = (LabGourdPreviewPreparation)typeof(GameSession).GetField("viewerPreparation",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
            LabGourdPaint source = network.target.GetComponent<LabGourdPaint>();
            // Hold only the session's presentation scheduling while its live paint renderer
            // consumes the real receiver pages. This does not pause or alter the accepted game result.
            session.enabled = false;
            try
            {
                DeliverDelayedArtworkPages();
                yield return Until(() => source != null && source.CompletionVisualReady,
                    "The source renderer must finish the very same sealed journal before taking its fast copy.");
                Assert.That(LabGourdResultPreview.TryCreate(network, out comparisonPreview, out string error), Is.True, error);
                yield return Until(() =>
                {
                    preparation.Tick();
                    return preparation.Ready || preparation.Failed;
                }, "The independent renderer must finish its delayed replay.");
                Assert.That(preparation.Failed, Is.False, preparation.Error);
                session.enabled = true;
                yield return Until(() => session.GourdViewerState == GameGourdViewerState.Ready,
                    "The session adopts only the completed independent output.");
                var deferred = (LabGourdResultPreview)typeof(GameSession).GetField("preview",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                Assert.That(deferred.PaintTexture.GetPixels32(), Is.EqualTo(comparisonPreview.PaintTexture.GetPixels32()));
                Assert.That(deferred.PainterTexture.GetPixels32(), Is.EqualTo(comparisonPreview.PainterTexture.GetPixels32()));
                Assert.That(ReadViewerMap(deferred.VisualPaintTexture), Is.EqualTo(ReadViewerMap(comparisonPreview.VisualPaintTexture)),
                    "The same actual journal must produce the same color texels through both capture paths.");
                Assert.That(ReadViewerMap(deferred.VisualPainterTexture), Is.EqualTo(ReadViewerMap(comparisonPreview.VisualPainterTexture)),
                    "The independent replay must not change the owner atlas.");
                Color[] expected = ReadViewerRelief(comparisonPreview.VisualHeightTexture);
                Color[] actual = ReadViewerRelief(deferred.VisualHeightTexture);
                Assert.That(actual.Length, Is.EqualTo(expected.Length));
                float maximum = 0f;
                for (int i = 0; i < actual.Length; i++)
                    for (int channel = 0; channel < 4; channel++)
                        maximum = Mathf.Max(maximum, Mathf.Abs(actual[i][channel] - expected[i][channel]));
                Assert.That(maximum, Is.LessThanOrEqualTo(1f / 1024f),
                    "Half-precision relief and encoded sticker normals must agree within one conservative Half quantization step.");
            }
            finally { if (session != null) session.enabled = true; }
            yield return WaitResultStory();
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Completed),
                "Finish the actual pending Scene handoff before releasing this fixture's session.");
        }

        private static Color32[] ReadViewerMap(Texture texture)
        {
            Assert.That(texture, Is.Not.Null);
            RenderTexture scratch = RenderTexture.GetTemporary(texture.width, texture.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true);
            try
            {
                Graphics.Blit(texture, scratch);
                RenderTexture.active = scratch;
                copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                copy.Apply(false, false);
                return copy.GetPixels32();
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(copy); RenderTexture.ReleaseTemporary(scratch); }
        }

        private static Color[] ReadViewerRelief(Texture texture)
        {
            Assert.That(texture, Is.Not.Null);
            RenderTexture scratch = RenderTexture.GetTemporary(texture.width, texture.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBAFloat, false, true);
            try
            {
                Graphics.Blit(texture, scratch);
                RenderTexture.active = scratch;
                copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                copy.Apply(false, false);
                return copy.GetPixels();
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(copy); RenderTexture.ReleaseTemporary(scratch); }
        }

        private IEnumerator StartJigwiWithDelayedArtwork()
        {
            session.StartSolo();
            for (int stage = 1; stage < 3; stage++)
            {
                yield return WaitPlaying(stage);
                session.Pause();
                Assert.That(session.DeveloperNextStage(), Is.True, "Use the real development skip to select Jigwi.");
            }
            yield return WaitPlaying(3);
            Assert.That(network.RequiresPaintArt, Is.True);
            Assert.That(network.PinnedWatercolorSettings.UsesClayStickers, Is.True);
            delayedArtworkWriter = network.PaintArtJournal;
            Assert.That(delayedArtworkWriter, Is.Not.Null);
            Assert.That(delayedArtworkWriter.Flush(), Is.True, delayedArtworkWriter.Error);
            delayedArtworkReceiver = new LabPaintArtJournal(System.IO.Path.Combine(Application.temporaryCachePath,
                "viewer-725-" + Guid.NewGuid().ToString("N") + ".art"), delayedArtworkWriter.Epoch,
                delayedArtworkWriter.Columns, delayedArtworkWriter.Rows, delayedArtworkWriter.StyleVersion, receiver: true);
            Assert.That(delayedArtworkReceiver.Error, Is.Null);
            DeliverDelayedArtworkPages();
            typeof(LabNetwork).GetField("paintArtJournal", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(network, delayedArtworkReceiver);
            CompleteMapInFixture(Model());
            yield return Until(() => network.Snapshot.phase == LabPhase.Victory,
                "The real model must publish a complete final result while its artwork is delayed.");
            string hash = (string)typeof(LabNetwork).GetMethod("HashArtSurface", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(network, new object[] { Model().PaintSurface });
            Assert.That(delayedArtworkWriter.Seal(Model().PaintSurface.Revision, hash), Is.True, delayedArtworkWriter.Error);
            Assert.That(delayedArtworkWriter.PageCount, Is.GreaterThan(delayedArtworkReceiver.PageCount));
            Assert.That(delayedArtworkReceiver.IsSealed, Is.False);
            yield return Until(() => typeof(GameSession).GetField("viewerPreparation", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(session) != null, "Capture a render-only template before the final presentation budget expires.");
            Assert.That(session.GourdViewerState, Is.EqualTo(GameGourdViewerState.Pending));
        }

        private void DeliverDelayedArtworkPages()
        {
            while (delayedArtworkReceiver.PageCount < delayedArtworkWriter.PageCount)
            {
                int page = delayedArtworkReceiver.PageCount;
                Assert.That(delayedArtworkWriter.TryReadPage(page, out byte[] raw), Is.True, delayedArtworkWriter.Error);
                Assert.That(delayedArtworkReceiver.TryImportPage(page, raw), Is.True, delayedArtworkReceiver.Error);
            }
        }

        private void ExpireOnlyTheFinishPresentationBudget()
        {
            var barrier = (GamePaintFinishBarrier)typeof(GameSession).GetField("finishBarrier",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
            Assert.That(barrier, Is.Not.Null);
            Assert.That(barrier.Expire(Time.realtimeSinceStartupAsDouble + GamePaintFinishBarrier.TimeoutSeconds + 1d), Is.True);
            typeof(GameSession).GetField("finishDeadlineAt", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(session, Time.realtimeSinceStartupAsDouble - 1d);
        }

        private void RestoreDelayedArtworkJournal()
        {
            if (delayedArtworkWriter == null && delayedArtworkReceiver == null) return;
            if (network != null && network.PaintArtJournal == delayedArtworkReceiver)
                typeof(LabNetwork).GetField("paintArtJournal", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(network, delayedArtworkWriter);
            delayedArtworkReceiver?.Dispose();
            // Stop will dispose the production writer when it is still the current round;
            // a Home/new-room reset already discarded that reference, so release it here.
            if (network == null || network.PaintArtJournal != delayedArtworkWriter) delayedArtworkWriter?.Dispose();
            delayedArtworkWriter = delayedArtworkReceiver = null;
        }

        [UnityTest]
        public IEnumerator SealReadinessAndQuitPopupKeepTheStageUntilConfirmedThenOpenRooms()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            CompleteMapInFixture(Model());
            yield return FinishResultStory();
            var story = ResultStory();
            Assert.That(session.CanAdvanceStage, Is.False, "Reading the story is not explicit readiness.");
            story.Advance();
            yield return Until(() => story.ReadyView.start.IsInteractable(), "Solo host is ready after tapping next seal.");
            Assert.That(story.ReadyView.IsWaiting, Is.True);
            Assert.That(session.State.stageIndex, Is.EqualTo(1));
            Assert.That(session.SealReadyCount, Is.EqualTo(1));
            story.ReadyView.quit.onClick.Invoke();
            Assert.That(story.ReadyView.IsConfirmingQuit, Is.True);
            Assert.That(session.CanAdvanceStage, Is.False, "A quit confirmation holds the shared start barrier.");
            story.StartPreparedStage();
            Assert.That(session.State.stageIndex, Is.EqualTo(1));
            story.ReadyView.cancelQuit.onClick.Invoke();
            Assert.That(story.ReadyView.IsConfirmingQuit, Is.False);
            Assert.That(story.ReadyView.IsWaiting && session.LocalSealReady, Is.True);
            yield return Until(() => story.ReadyView.start.IsInteractable(), "Cancel restores the prepared host.");
            story.ReadyView.quit.onClick.Invoke();
            story.ReadyView.confirmQuit.onClick.Invoke();
            yield return Until(() => !session.IsLoadingScene && session.State.phase == GameFlowPhase.Home,
                "Quit loads the room browser directly.");
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(GameScenePaths.Home));
            Assert.That(session.RoomBrowserVisible, Is.True);
            Assert.That(network.IsConnected, Is.False);
            Assert.That(session.LocalSealReady, Is.False);
        }

        [UnityTest]
        public IEnumerator SharedCountdownWaitsForInitialPresentationAndOnlyThenStartsGameplay()
        {
            // This test owns the visible 3/2/1 and "봉인 시작!": it runs the production start durations, not the
            // assembly's shortened gate (GameTestStartTiming), and pins those defaults.
            GameTestStartTiming.UseDefaults();
            try
            {
                Assert.That(GameStartTiming.IsDefault, Is.True);
                Assert.That(GameStartTiming.CountdownSeconds, Is.EqualTo(3d));
                Assert.That(GameStartTiming.InitialLeadSeconds, Is.EqualTo(1d));
                Assert.That(GameStartTiming.MaximumLeadSeconds, Is.EqualTo(5d));
                Assert.That(GameStartTiming.StartedLabelSeconds, Is.EqualTo(1d));
                session.StartSolo();
                Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.LoadingBattle));
                Assert.That(session.StartOverlay.Visible && session.StartOverlay.BlocksInput, Is.True,
                    "The loading caption must exist before the asynchronous Scene load starts.");
                yield return Until(() => session.State.phase == GameFlowPhase.Countdown
                    && session.StartOverlay.DisplayText == "3", "Prepared participants must enter the shared countdown.");
                Assert.That(network.IsGameInitialStateReady(session.State.battleSessionId, session.State.battleRound), Is.True);
                Assert.That(UnityEngine.Object.FindFirstObjectByType<GameBattleBindings>().IsPresentationReady, Is.True);
                var watercolorPaint = UnityEngine.Object.FindFirstObjectByType<LabGourdPaint>();
                Assert.That(network.PinnedWatercolorSettings.UsesClayStickers ? watercolorPaint.ClayStickersActive : watercolorPaint.WatercolorStampsActive, Is.True, watercolorPaint.WatercolorStampError);
                var watercolorBlock = new MaterialPropertyBlock();
                watercolorPaint.Shell.GetComponent<Renderer>().GetPropertyBlock(watercolorBlock);
                Assert.That(watercolorBlock.GetTexture("_PaintMap"), Is.SameAs(watercolorPaint.VisualPaintTexture),
                    "Hapio's actual Battle must bind the same watercolor renderer before the shared countdown.");
                LabSnapshot before = network.Snapshot;
                double remaining = before.remaining;
                float stamina = before.players.Single(player => player.id == network.LocalPlayerId).stamina;
                var digits = new System.Collections.Generic.HashSet<string>();
                LabOrbState drum = before.drums.Single(orb => orb.owner == network.LocalPlayerId);
                Assert.That(network.board.TryGetMotion(drum.id, out Vector2 initialPosition, out _), Is.True);
                LabHangingTarget hanging = network.target.GetComponent<LabHangingTarget>();
                float ropeElapsed = hanging.CycleElapsed;
                while (session.State.phase == GameFlowPhase.Countdown)
                {
                    digits.Add(session.StartOverlay.DisplayText);
                    Assert.That(network.GameStartHeld, Is.True);
                    Assert.That(network.IsBattlePaused, Is.False);
                    Assert.That(Time.timeScale, Is.EqualTo(1f));
                    Assert.That(network.CanPauseBattle || network.CanResumeBattle || session.CanDeveloperAdvanceStage, Is.False);
                    Assert.That(network.PauseBattle(), Is.False);
                    Assert.That(network.ResumeBattle(), Is.False);
                    network.Generate();
                    Assert.That(network.Snapshot.remaining, Is.EqualTo(remaining));
                    Assert.That(network.Snapshot.playedSeconds, Is.Zero);
                    Assert.That(network.Snapshot.orbs, Is.Empty);
                    Assert.That(network.Snapshot.players.Single(player => player.id == network.LocalPlayerId).stamina,
                        Is.EqualTo(stamina));
                    Assert.That(network.Snapshot.paintRevision, Is.EqualTo(before.paintRevision));
                    Assert.That(network.board.TryGetMotion(drum.id, out Vector2 heldPosition, out _), Is.True);
                    Assert.That(Vector2.Distance(heldPosition, initialPosition), Is.LessThan(.00001f));
                    Assert.That(hanging.CycleElapsed, Is.EqualTo(ropeElapsed));
                    yield return null;
                }
                Assert.That(digits, Is.SupersetOf(new[] { "3", "2", "1" }));
                Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Playing));
                Assert.That(network.TryGetGameServerTime(out double serverNow), Is.True);
                Assert.That(serverNow, Is.GreaterThanOrEqualTo(session.State.battleStartsAt));
                Assert.That(network.GameStartHeld, Is.False);
                Assert.That(session.StartOverlay.DisplayText, Is.EqualTo("봉인 시작!"), "A story stage opens with 봉인 시작! (#717).");
                Assert.That(session.StartOverlay.BlocksInput, Is.False);
                network.Generate();
                yield return Until(() => network.Snapshot.orbs.Length == 1, "Generation opens after the shared deadline.");
                yield return Until(() => network.Snapshot.remaining < remaining && !session.StartOverlay.Visible,
                    "The started caption fades while the normal battle clock advances.");
            }
            finally { GameTestStartTiming.UseShort(); }
        }

        [UnityTest]
        public IEnumerator PreparationTimeoutUsesTheOriginalLoadBudgetDuringReservationToo()
        {
            session.StartSolo();
            yield return Until(() => session.State.phase == GameFlowPhase.Countdown, "Reserve the shared start.");
            // Simulate a reservation without its final commit arriving after an already spent load budget.
            typeof(GameSession).GetField("countdownCommitted", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(session, false);
            typeof(GameSession).GetField("loadStartedAt", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(session, Time.realtimeSinceStartupAsDouble - 31d);
            yield return WaitGameOver();
            Assert.That(session.State.endReason, Does.Contain("preparing"));
            Assert.That(session.StartOverlay.Visible, Is.False);
            Assert.That(network.Snapshot.playedSeconds, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PreparedRoundEntersSynchronizationEvenWhenInitialArtworkIsNotReadyOnTheCreationFrame()
        {
            FieldInfo mismatch = typeof(LabNetwork).GetField("paintArtVersionMismatch", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(mismatch, Is.Not.Null);
            bool heldOneFrame = false;
            Action holdInitialReadiness = () =>
            {
                if (heldOneFrame || network.Snapshot == null || !network.GameStartHeld
                    || network.Snapshot.phase != LabPhase.Playing) return;
                heldOneFrame = true;
                mismatch.SetValue(network, true);
            };
            network.Changed += holdInitialReadiness;
            try
            {
                session.StartSolo();
                yield return Until(() => heldOneFrame, "The first held round must be created.");
                Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.SynchronizingBattle),
                    "Creating a held round must advance to synchronization even if presentation needs another frame.");
                Assert.That(network.IsGameInitialStateReady(session.State.battleSessionId, session.State.battleRound), Is.False);
                Assert.That(network.GameStartHeld, Is.True);
                network.Generate();
                Assert.That(network.Snapshot.orbs, Is.Empty);
                mismatch.SetValue(network, false);
                yield return Until(() => session.State.phase == GameFlowPhase.Countdown,
                    "The existing synchronization barrier must reserve a countdown after readiness recovers.");
                Assert.That(network.GameStartHeld, Is.True);
            }
            finally
            {
                network.Changed -= holdInitialReadiness;
                mismatch.SetValue(network, false);
            }
        }

        [UnityTest]
        public IEnumerator AGuestExitDuringTheRunMarksItAsLeavingSoItsLaterDropIsNotHeld()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic;
            // #501: a guest that quits the run sends "exit". In a 2:2 room the Host then returns to its lobby with that
            // guest still connected; if it drops there later, its place must not be held for 90 s. A fake member stands
            // in for the guest in this solo run.
            var order = (System.Collections.Generic.List<ulong>)typeof(LabNetwork).GetField("order", flags).GetValue(network);
            var leavers = (System.Collections.Generic.HashSet<ulong>)typeof(LabNetwork).GetField("lobbyLeavers", flags)
                .GetValue(network);
            order.Add(77); order.Add(78);
            System.Reflection.MethodInfo make = typeof(GameSession).GetMethod("MakeControl", flags);
            System.Reflection.MethodInfo apply = typeof(GameSession).GetMethod("ApplyControl", flags);
            // An exit naming another run (it arrived after a 2:2 room already returned to its lobby) still counts.
            object stale = make.Invoke(session, new object[] { "exit", null });
            stale.GetType().GetField("sender").SetValue(stale, 78UL);
            stale.GetType().GetField("runId").SetValue(stale, "an-earlier-run");
            apply.Invoke(session, new[] { stale });
            Assert.That(leavers, Does.Contain(78UL));
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Playing), "It names no current run, so nothing stops.");
            object control = make.Invoke(session, new object[] { "exit", null });
            control.GetType().GetField("sender").SetValue(control, 77UL);
            apply.Invoke(session, new[] { control });
            Assert.That(leavers, Does.Contain(77UL));
            order.Remove(77); order.Remove(78);
            leavers.Remove(77); leavers.Remove(78);
            yield return WaitGameOver();
            session.GoHome();
            yield return WaitHome();
        }

        [UnityTest]
        public IEnumerator LeavingDuringCountdownCancelsThePreparedRoundAndClearsTheOverlay()
        {
            session.StartSolo();
            yield return Until(() => session.State.phase == GameFlowPhase.Countdown,
                "A solo stage must also reserve a countdown.");
            session.ExitRun();
            yield return WaitGameOver();
            Assert.That(session.StartOverlay.Visible, Is.False);
            Assert.That(network.GameStartHeld, Is.False);
            network.Generate();
            Assert.That(network.Snapshot.orbs, Is.Empty);
            session.GoHome();
            yield return WaitHome();
            Assert.That(session.StartOverlay.Visible, Is.False);
            Assert.That(network.GameStartHeld, Is.False);
        }

        [UnityTest]
        public IEnumerator TheHostRetriesItsOwnStartCheckBrieflyAfterTheCommitThenStartsOrAborts()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo mismatch = typeof(LabNetwork).GetField("paintArtVersionMismatch", flags);
            FieldInfo committed = typeof(GameSession).GetField("countdownCommitted", flags);
            try
            {
                // A Host whose own initial state lags behind the commit keeps the committed Countdown for a moment.
                session.StartSolo();
                yield return Until(() => session.State.phase == GameFlowPhase.Countdown && (bool)committed.GetValue(session),
                    "The solo Host commits its countdown.");
                mismatch.SetValue(network, true);
                Assert.That(network.IsGameInitialStateReady(session.State.battleSessionId, session.State.battleRound), Is.False);
                double startsAt = session.State.battleStartsAt;
                double serverNow = 0d;
                yield return Until(() => network.TryGetGameServerTime(out serverNow) && serverNow >= startsAt + .1d,
                    "The shared start passes while the Host is not ready.");
                Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Countdown), "It does not abort at once.");
                Assert.That(network.GameStartHeld, Is.True);
                mismatch.SetValue(network, false);
                yield return WaitPlaying(1);
                Assert.That(network.GameStartHeld, Is.False);
                session.ExitRun();
                yield return WaitGameOver();
                session.GoHome();
                yield return WaitHome();

                // Still not ready half a second after the shared start: the original release failure aborts the run.
                session.StartSolo();
                yield return Until(() => session.State.phase == GameFlowPhase.Countdown && (bool)committed.GetValue(session),
                    "The second solo start commits its countdown.");
                mismatch.SetValue(network, true);
                startsAt = session.State.battleStartsAt;
                yield return WaitGameOver();
                Assert.That(session.State.endReason, Is.EqualTo("The prepared battle could not be started."));
                Assert.That(network.TryGetGameServerTime(out serverNow), Is.True);
                Assert.That(serverNow, Is.GreaterThanOrEqualTo(startsAt + GameStartWaitLimits.HostReleaseRetrySeconds));
            }
            finally { mismatch.SetValue(network, false); }
        }

        [UnityTest]
        public IEnumerator AGuestHeldTenForegroundSecondsAfterTheSharedStartEndsTheStoryRun()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo mismatch = typeof(LabNetwork).GetField("paintArtVersionMismatch", flags);
            FieldInfo held = typeof(GameSession).GetField("startHeldAccum", flags);
            MethodInfo guestWaits = typeof(GameSession).GetMethod("UpdateGuestStartWaits", flags);
            try
            {
                session.StartSolo();
                yield return WaitPlaying(1);
                Assert.That(session.GuestPreparationLimitSeconds, Is.EqualTo(30d + GameTestStartTiming.CountdownSeconds
                    + GameTestStartTiming.MaximumLeadSeconds + 7d).Within(1e-9), "45 s with the defaults, scaled here.");
                // This solo Host stands in for a Guest whose own presentation never becomes ready after the start.
                mismatch.SetValue(network, true);
                network.SetGameStartHeld(true);
                yield return null;
                Assert.That(network.GameStartHeld, Is.True, "Not ready, so the start hold stays.");
                held.SetValue(session, 9d);
                // A resume after 29 s in the background (#502) adds only one clamped frame.
                Assert.That((bool)guestWaits.Invoke(session, new object[] { 29d }), Is.False);
                Assert.That((double)held.GetValue(session), Is.EqualTo(9.25d));
                held.SetValue(session, 9.9d);
                Assert.That((bool)guestWaits.Invoke(session, new object[] { .2d }), Is.True);
                yield return WaitGameOver();
                Assert.That(session.State.endReason, Is.EqualTo("This device could not start the stage."));
                Assert.That(session.State.retryableDefeat, Is.False);
                Assert.That((double)held.GetValue(session), Is.Zero, "Leaving the start resets the counter.");
            }
            finally { mismatch.SetValue(network, false); }
            session.GoHome();
            yield return WaitHome();
        }

        [UnityTest]
        public IEnumerator AGuestPreparingPastItsLimitLeavesTheRun()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo preparing = typeof(GameSession).GetField("preparingAccum", flags);
            MethodInfo guestWaits = typeof(GameSession).GetMethod("UpdateGuestStartWaits", flags);
            session.StartSolo();
            yield return Until(() => session.IsPreparingBattle, "The solo stage starts preparing.");
            Assert.That((double)preparing.GetValue(session), Is.Zero);
            preparing.SetValue(session, session.GuestPreparationLimitSeconds - .1d);
            Assert.That((bool)guestWaits.Invoke(session, new object[] { .05d }), Is.False, "Still .05 s short.");
            // A background-length frame adds only .25 s, which is enough here.
            Assert.That((bool)guestWaits.Invoke(session, new object[] { 30d }), Is.True);
            yield return WaitGameOver();
            Assert.That(session.State.endReason, Is.EqualTo("The host did not start the stage."));
            Assert.That(network.GameStartHeld, Is.False);
            session.GoHome();
            yield return WaitHome();
        }

        [UnityTest]
        public IEnumerator AGuestStartHoldExitReturnsATerritoryRoomToItsLobby()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo solo = typeof(LabNetwork).GetField("developerSolo", flags);
            var order = (System.Collections.Generic.List<ulong>)typeof(LabNetwork).GetField("order", flags).GetValue(network);
            var leavers = (System.Collections.Generic.HashSet<ulong>)typeof(LabNetwork).GetField("lobbyLeavers", flags)
                .GetValue(network);
            session.StartSolo(GameMode.Territory);
            yield return WaitPlaying(1);
            Assert.That(session.IsTerritory, Is.True);
            // The exit a Guest sends from ExitWithLocalReason, as this room's Host receives it outside DEV solo.
            order.Add(77);
            solo.SetValue(network, false);
            try
            {
                object control = typeof(GameSession).GetMethod("MakeControl", flags).Invoke(session, new object[] { "exit", null });
                control.GetType().GetField("sender").SetValue(control, 77UL);
                typeof(GameSession).GetMethod("ApplyControl", flags).Invoke(session, new[] { control });
                Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Lobby), "A 2:2 room goes back to its lobby.");
                Assert.That(session.State.mode, Is.EqualTo(GameMode.Territory));
            }
            finally
            {
                solo.SetValue(network, true);
                order.Remove(77);
                leavers.Remove(77);
            }
            session.GoHome();
            yield return WaitHome();
        }

        [Category("Bonjour")]
        [UnityTest]
        public IEnumerator PendingGameCardsSurviveBattleGameOverAndHomeWhileLobbyLeaveReturnsToTheBrowser()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Game pending rooms require macOS/iOS Bonjour.");
            Assert.That(session.RoomBrowserVisible, Is.False, "A cold Home starts on its title page.");
            session.EnterRoomBrowser();
            Assert.That(session.RoomBrowserVisible, Is.True);
            Assert.That(session.CreatePendingRoom(2), Is.True, session.UiNotice);
            Assert.That(session.CreatePendingRoom(3), Is.True, session.UiNotice);
            yield return Until(() => session.PendingRooms.Count == 2 && session.PendingRooms.All(room => room.Ready),
                "The persistent Game session must finish both native room registrations.");
            var cards = session.PendingRooms.ToArray();
            Assert.That(cards.All(room => room.Name.StartsWith(LabRoomNames.GameServiceBase, StringComparison.Ordinal)), Is.True);
            string[] keys = cards.Select(room => room.Key).ToArray();
            GameSession persistent = session;
            LabNetwork persistentNetwork = network;

            session.StartSolo();
            yield return WaitPlaying(1);
            Assert.That(session.PendingRooms.Select(room => room.Key), Is.EqualTo(keys),
                "Unloading Home must not dispose another waiting room's advertisement.");
            CompleteMapInFixture(Model());
            yield return Until(() => session.State.phase == GameFlowPhase.StageResult && !session.IsLoadingScene
                && SceneManager.GetActiveScene().path == GameScenePaths.StageResult,
                "A real committed fixture victory must finish loading its result Scene.");
            Assert.That(session.PendingRooms.Select(room => room.Key), Is.EqualTo(keys));
            Assert.That(session.PendingRooms.All(room => room.Ready), Is.True,
                "Waiting-room advertisements remain owned by the session during results.");
            session.ExitRun();
            yield return WaitGameOver();
            Assert.That(session.PendingRooms.All(room => room.Ready), Is.True);
            session.GoHome();
            yield return WaitHome();
            yield return Until(() => network.CanChooseConnection, "The completed shutdown must release the active transport.");
            Assert.That(session.RoomBrowserVisible, Is.False, "GameOver returns to the title, not an automatic room entry.");
            Assert.That(session.PendingRooms.Select(room => room.Key), Is.EqualTo(keys));

            session.EnterRoomBrowser();
            Assert.That(session.EnterPendingRoom(keys[0]), Is.True, session.UiNotice);
            yield return Until(() => session.State.phase == GameFlowPhase.Lobby && !session.IsLoadingScene,
                "Entering the creator's card must use the existing Game transport and open its lobby.");
            Assert.That(GameSession.Instance, Is.SameAs(persistent));
            Assert.That(session.Network, Is.SameAs(persistentNetwork));
            Assert.That(network.GameFlowEnabled, Is.True);
            Assert.That(network.IsHost, Is.True);
            Assert.That(network.TargetParticipantCount, Is.EqualTo(2));
            Assert.That(network.LocalSlotNumber, Is.Zero, "Entering one's room does not reserve P1 for the Host.");
            Assert.That(network.ConnectedRoomName, Is.EqualTo(cards[0].Name));
            Assert.That(network.port, Is.EqualTo(cards[0].Port));
            Assert.That(session.PendingRooms.Select(room => room.Key), Is.EqualTo(keys.Skip(1)));
            session.GoHomeToRoomBrowser();
            yield return WaitHome();
            Assert.That(session.RoomBrowserVisible, Is.True, "Leaving a pre-battle lobby returns to its room browser.");
            Assert.That(session.PendingRooms.Single().Key, Is.EqualTo(keys[1]));
            Assert.That(session.PendingRooms.Single().Ready, Is.True);
        }

        [UnityTest]
        public IEnumerator SavedResultPreviewUsesOneNormalizedGestureHandlerAndClearsHeldDragsOnResetAndReopen()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            CompleteMapInFixture(Model());
            yield return Until(() => session.State.phase == GameFlowPhase.StageResult && !session.IsLoadingScene
                && SceneManager.GetActiveScene().path == GameScenePaths.StageResult, "The committed result must load its saved UI.");
            GameScreen screen = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<GameScreen>(true)).Single();
            screen.resultView.viewerButton.onClick.Invoke();
            yield return null;
            Assert.That(screen.viewerPanel.activeInHierarchy, Is.True);
            GameObject image = screen.viewerImage.gameObject;
            Assert.That(image.GetComponent<GamePreviewAspect>(), Is.Not.Null,
                "The saved StageResult's GameScreen must attach aspect fitting in its actual startup path.");
            GamePreviewInput adapter = image.GetComponent<GamePreviewInput>();
            Assert.That(adapter, Is.Not.Null, "Keep the saved Scene's adapter reference.");
            Assert.That(image.GetComponents<MonoBehaviour>().OfType<IDragHandler>().Count(), Is.EqualTo(1),
                "The adapter and the shared surface must not dispatch the same drag twice.");
            Assert.That(image.GetComponents<GourdViewerGestureSurface>(), Has.Length.EqualTo(1));
            var preview = (LabGourdResultPreview)typeof(GameSession).GetField("preview",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
            Quaternion initial = preview.ModelRotation;
            string paintHash = preview.PaintPixelHash;
            Color32[] paintPixels = preview.PaintTexture.GetPixels32();
            string result = JsonUtility.ToJson(session.ResultSnapshot);
            RectTransform rectangle = screen.viewerImage.rectTransform;
            Canvas.ForceUpdateCanvases();
            RectTransform viewport = rectangle.parent as RectTransform;
            Assert.That(viewport, Is.Not.Null);
            Assert.That(viewport.name, Is.EqualTo("Gourd preview aspect viewport"));
            // The texture shows a larger frame around the authored square (ExtendFrame); the gesture square keeps that
            // centre part's aspect, and the frame image around it shows the rest at the same scale.
            Vector2 frame = session.PreviewFrameScale;
            Assert.That(rectangle.rect.width / rectangle.rect.height,
                Is.EqualTo(screen.viewerImage.texture.width / frame.x / (screen.viewerImage.texture.height / frame.y)).Within(.001f),
                "The real captured gourd must keep its aspect on the actual portrait result Scene.");
            var imageCorners = new Vector3[4];
            rectangle.GetWorldCorners(imageCorners);
            foreach (Vector3 corner in imageCorners)
            {
                Vector3 local = viewport.InverseTransformPoint(corner);
                Assert.That(local.x, Is.InRange(viewport.rect.xMin - .01f, viewport.rect.xMax + .01f));
                Assert.That(local.y, Is.InRange(viewport.rect.yMin - .01f, viewport.rect.yMax + .01f));
            }
            Vector2 start = PreviewScreenPoint(rectangle, .4f, .5f);
            var pointer = new PointerEventData(EventSystem.current) { pointerId = 12345, position = start };
            ExecuteEvents.Execute(image, pointer, ExecuteEvents.pointerDownHandler);
            pointer.position = PreviewScreenPoint(rectangle, .41f, .5f);
            pointer.delta = pointer.position - start; // Pixel movement exceeds 1px on the saved layout.
            ExecuteEvents.Execute(image, pointer, ExecuteEvents.dragHandler);
            Quaternion expected = Quaternion.AngleAxis(-1.8f, Vector3.up) * initial;
            Assert.That(Quaternion.Angle(expected, preview.ModelRotation), Is.LessThan(.01f),
                "One percent of the preview width rotates 1.8 degrees, not a clamped 180 degrees.");
            screen.resetViewerButton.onClick.Invoke();
            Assert.That(Quaternion.Angle(initial, preview.ModelRotation), Is.LessThan(.001f));
            pointer.position = PreviewScreenPoint(rectangle, .6f, .5f);
            ExecuteEvents.Execute(image, pointer, ExecuteEvents.dragHandler);
            Assert.That(Quaternion.Angle(initial, preview.ModelRotation), Is.LessThan(.001f),
                "Reset must discard a held pointer's old baseline.");
            screen.closeViewerButton.onClick.Invoke();
            yield return null;
            Assert.That(screen.viewerPanel.activeSelf, Is.False);
            screen.resultView.viewerButton.onClick.Invoke();
            yield return null;
            ExecuteEvents.Execute(image, pointer, ExecuteEvents.dragHandler);
            Assert.That(Quaternion.Angle(initial, preview.ModelRotation), Is.LessThan(.001f),
                "Reopening must not adopt an old drag without a fresh pointer down.");
            Assert.That(image.GetComponents<GourdViewerGestureSurface>(), Has.Length.EqualTo(1));
            Assert.That(preview.PaintPixelHash, Is.EqualTo(paintHash));
            Assert.That(preview.PaintTexture.GetPixels32(), Is.EqualTo(paintPixels));
            Assert.That(JsonUtility.ToJson(session.ResultSnapshot), Is.EqualTo(result),
                "Local gesture input must not alter the committed gameplay result.");
        }

        [Category("GPU")]
        [UnityTest]
        public IEnumerator SealedGourdShadowFollowsPreviewRotationAndReset()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("Reading the projected shadow requires a graphics device.");

            session.StartSolo();
            yield return WaitPlaying(1);
            CompleteMapInFixture(Model());
            yield return Until(() => session.State.phase == GameFlowPhase.StageResult && !session.IsLoadingScene
                && SceneManager.GetActiveScene().path == GameScenePaths.StageResult && session.PreviewTexture != null,
                "Load the saved result screen with its completed preview.");
            GameScreen screen = Object.FindFirstObjectByType<GameScreen>();
            session.OpenGourdViewer();
            var shadow = screen.viewerPanel.transform.Find("Gourd Shadow").GetComponent<UnityEngine.UI.RawImage>();
            yield return Until(() => shadow.isActiveAndEnabled && shadow.texture is RenderTexture target && target.IsCreated(),
                "The visible viewer renders a shadow mask.");
            var projection = (RenderTexture)shadow.texture;
            var camera = Camera.allCameras.Single(candidate => candidate.targetTexture == session.PreviewTexture);
            Assert.That(camera.transform.IsChildOf(session.transform), Is.True,
                "The completed preview must live and tear down with its session.");
            Assert.That(camera.gameObject.scene, Is.EqualTo(session.gameObject.scene));
            Assert.That(Camera.allCameras.Count(candidate => candidate.name == camera.name), Is.EqualTo(1),
                "An old preview camera must not share the current staging area.");
            // The battle's hanging rope is drawn by LabHangingTarget's "Rope"/"Upper Rope" cylinders or the curved
            // "Continuous Rope" line; the viewer copies only the gourd model, so none of them may reach the preview rig.
            Transform rig = camera.transform.parent;
            Assert.That(rig.GetComponentsInChildren<Renderer>(true).Select(renderer => renderer.name)
                .Intersect(new[] { "Rope", "Upper Rope", "Continuous Rope" }), Is.Empty,
                "The viewer shows the gourd without its hanging rope.");
            Assert.That(rig.GetComponentsInChildren<LineRenderer>(true), Is.Empty, "No curved rope line in the viewer.");

            byte[] ReadAlpha()
            {
                var pixels = new Texture2D(projection.width, projection.height, TextureFormat.RGBA32, false, true);
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = projection;
                    pixels.ReadPixels(new Rect(0, 0, projection.width, projection.height), 0, 0, false);
                    return pixels.GetPixels32().Select(pixel => pixel.a).ToArray();
                }
                finally
                {
                    RenderTexture.active = previous;
                    Object.Destroy(pixels);
                }
            }

            byte[] initial = ReadAlpha();
            int covered = initial.Count(alpha => alpha > 127);
            Assert.That(covered, Is.InRange(500, initial.Length / 2), "The floor texture contains a visible, bounded silhouette.");
            session.RotatePreview(new Vector2(.18f, .40f));
            yield return null;
            Assert.That(shadow.texture, Is.SameAs(projection), "Turning the gourd reuses the projection texture.");
            Assert.That(ReadAlpha().Where((alpha, index) => Math.Abs(alpha - initial[index]) > 32).Count(),
                Is.GreaterThan(100), "Turning the real result mesh changes its projected silhouette.");
            // Zooming moves the preview camera; the floor shadow scales with the drawn gourd.
            float fitted = shadow.rectTransform.localScale.x;
            session.ZoomPreview(.4f);
            yield return null;
            Assert.That(shadow.rectTransform.localScale.x, Is.GreaterThan(fitted * 1.3f), "Zooming in grows the shadow with the gourd.");
            screen.resetViewerButton.onClick.Invoke();
            yield return null;
            Assert.That(ReadAlpha(), Is.EqualTo(initial), "Reset restores the original silhouette.");
            Assert.That(shadow.rectTransform.localScale.x, Is.EqualTo(fitted).Within(.001f), "Reset restores the shadow size.");
            // The stage art covers the screen without stretching: the visible part keeps the 402×874 proportions.
            var backdrop = screen.viewerPanel.transform.Find("Stage Background").GetComponent<UnityEngine.UI.RawImage>();
            Rect area = ((RectTransform)backdrop.transform).rect;
            float shown = area.width / area.height * backdrop.uvRect.height / backdrop.uvRect.width;
            Assert.That(shown, Is.EqualTo(402f / 874f).Within(.002f), "The stage background keeps its aspect.");
        }

        private static Vector2 PreviewScreenPoint(RectTransform rectangle, float x, float y)
        {
            Rect local = rectangle.rect;
            return RectTransformUtility.WorldToScreenPoint(null, rectangle.TransformPoint(new Vector3(
                local.xMin + local.width * x, local.yMin + local.height * y, 0f)));
        }

        [UnityTest]
        public IEnumerator AStoryStageSaysSealSucceededWithTheWhistleBeforeItsFinalHitEffect()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            int whistles = session.EndWhistleCount;
            Assert.That(session.BattleEndShown, Is.False);
            CompleteMapInFixture(Model());
            yield return Until(() => session.BattleEndShown, "Painting the whole gourd shows its end on this device (#717).");
            double endAt = Time.realtimeSinceStartupAsDouble;
            yield return null;
            Assert.That(session.StartOverlay.DisplayText, Is.EqualTo(GameStartOverlay.SealSucceededText));
            Assert.That(session.StartOverlay.ShowsFinished && session.StartOverlay.BlocksInput, Is.True,
                "Nothing can be pressed while 봉인 성공! is up.");
            Assert.That((Color32)session.StartOverlay.CaptionColor, Is.EqualTo(new Color32(0x8A, 0x12, 0x06, 0xFF)),
                "The same crimson as the territory's 경기 종료!.");
            Assert.That(network.board.UiInputBlocked, Is.True);
            Assert.That(session.EndWhistleCount, Is.EqualTo(whistles + 1), "One whistle.");
            Assert.That(session.PaintFinishProgress, Is.Zero, "The final-hit effect waits for 봉인 성공!.");

            yield return Until(() => session.PaintFinishProgress > 0f || session.State.phase != GameFlowPhase.Playing,
                "Then the final-hit effect plays.");
            Assert.That(Time.realtimeSinceStartupAsDouble - endAt, Is.GreaterThanOrEqualTo(session.BattleEndHoldSeconds - .1d),
                "봉인 성공! has its time before the final-hit effect.");
            yield return null;
            Assert.That(session.StartOverlay.ShowsFinished, Is.False, "봉인 성공! makes way for the final-hit effect.");
            Assert.That(session.EndWhistleCount, Is.EqualTo(whistles + 1), "Only once.");
            yield return FinishResultStory();
        }

        [UnityTest]
        public IEnumerator AStoryTimeUpSaysTimeUpInBlueWithTheWhistleThenGoesStraightToItsFailure()
        {
            float[] values = network.config.CaptureDeveloperValues();
            Set(values, "Battle Duration (seconds)", 1f);
            Assert.That(session.ApplyDeveloperSettings(values, out string error), Is.True, error);
            session.StartSolo();
            yield return WaitPlaying(1);
            int whistles = session.EndWhistleCount;
            // The real Network clock reaches the one-second limit with the gourd unpainted (#717).
            yield return Until(() => session.BattleEndShown, "Running out of time shows its end on this device.");
            double endAt = Time.realtimeSinceStartupAsDouble;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(GameScenePaths.Battle));
            yield return null;
            Assert.That(session.StartOverlay.DisplayText, Is.EqualTo(GameStartOverlay.TimeUpText));
            Assert.That(session.StartOverlay.ShowsFinished && session.StartOverlay.BlocksInput, Is.True,
                "Nothing can be pressed while 시간 초과! is up.");
            Assert.That((Color32)session.StartOverlay.CaptionColor, Is.EqualTo(new Color32(0x16, 0x4B, 0x8C, 0xFF)),
                "시간 초과! is blue, not the crimson of 봉인 성공!.");
            Assert.That(network.board.UiInputBlocked, Is.True);
            Assert.That(session.EndWhistleCount, Is.EqualTo(whistles + 1), "One whistle.");

            yield return WaitGameOver();
            Assert.That(Time.realtimeSinceStartupAsDouble - endAt, Is.GreaterThanOrEqualTo(session.BattleEndHoldSeconds - .1d),
                "시간 초과! has its time before the failure.");
            Assert.That(session.State.endReason, Is.EqualTo("Time ran out."));
            Assert.That(session.PaintFinishProgress, Is.Zero, "No final-hit effect on a time-up.");
            Assert.That(session.FinishUsesReplay, Is.False);
            Assert.That(session.EndWhistleCount, Is.EqualTo(whistles + 1), "Only once.");
            yield return null;
            Assert.That(session.StartOverlay.Visible, Is.False, "The failure replaces 시간 초과!.");
        }

        [UnityTest]
        public IEnumerator TerritoryTimeUpSaysGameOverWithTheWhistleThenOpensItsResultWithoutAPaintedGourd()
        {
            session.StartSolo(GameMode.Territory);
            yield return WaitPlaying(1);
            int whistles = session.EndWhistleCount;
            Assert.That(session.BattleEndShown, Is.False);
            // Bring the time limit to a second from now on the model's own clock (a fixture; the clock itself is not advanced).
            LabModel model = Model();
            typeof(LabModel).GetField("deadline", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(model, ModelTime(model) + 1d);
            yield return Until(() => session.BattleEndShown, "The time-up shows its end on this device (#717).");
            double endAt = Time.realtimeSinceStartupAsDouble;
            Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Victory));
            Assert.That(network.Snapshot.paintCoveredCount, Is.LessThan(network.Snapshot.paintCellCount),
                "A time-up gourd is not completely painted, so it has no completed picture to wait for.");
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(GameScenePaths.Battle));
            yield return null;
            Assert.That(session.StartOverlay.ShowsFinished, Is.True);
            Assert.That(session.StartOverlay.DisplayText, Is.EqualTo(GameStartOverlay.FinishedText));
            Assert.That(session.StartOverlay.BlocksInput, Is.True, "Nothing can be pressed until the result opens.");
            Assert.That(network.board.UiInputBlocked, Is.True, "No drag, throw or pass under a finger either.");
            Assert.That(session.EndWhistleCount, Is.EqualTo(whistles + 1), "One whistle, on every device.");

            yield return Until(() => SceneManager.GetActiveScene().path == GameScenePaths.StageResult && !session.IsLoadingScene,
                "The result opens right after the whistle.");
            double opened = Time.realtimeSinceStartupAsDouble - endAt;
            Assert.That(opened, Is.GreaterThanOrEqualTo(session.BattleEndHoldSeconds - .1d), "The whistle has its time first.");
            Assert.That(opened, Is.LessThan(session.BattleEndHoldSeconds + 3d),
                "It no longer waits out the result budget for a completed gourd that never comes.");
            Assert.That(session.EndWhistleCount, Is.EqualTo(whistles + 1), "Only once.");
            yield return null;
            Assert.That(session.StartOverlay.Visible, Is.False, "The result replaces \"경기 종료!\".");
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Completed));
            Assert.That(session.ResultSnapshot.teamTerritory, Is.True);
            Assert.That(session.PreviewTexture, Is.Null, "A gourd that is not completely painted has no 박 확인 picture.");
            Assert.That(session.GourdViewerState, Is.EqualTo(GameGourdViewerState.Disabled),
                "An incomplete territory map must not start a pending or failed Story artwork job.");
            Assert.That(session.GourdViewerError, Is.Null.Or.Empty);
            GameTerritoryResultView territory = Object.FindFirstObjectByType<GameScreen>().territoryView;
            yield return Until(() => territory.gameObject.activeInHierarchy, "The team result shows.");
            yield return Until(() => territory.table.homeButton.IsInteractable(), "Exit is available on the result.");
            territory.table.homeButton.onClick.Invoke();
            yield return WaitHome();
        }

        [UnityTest]
        public IEnumerator ADrumKeptFourSecondsGetsTheTutorialsPassGuideUntilItIsPassed()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            GameDrumPassHint hint = Object.FindFirstObjectByType<GameDrumPassHint>();
            Assert.That(hint, Is.Not.Null, "Every story and territory battle has the drum guide (#718).");
            yield return null;
            Assert.That(hint.Visible, Is.False, "Not in the first seconds.");

            // Drive it by hand on the real board and drum.
            hint.enabled = false;
            ulong me = network.LocalPlayerId;
            LabSnapshot solo = JsonUtility.FromJson<LabSnapshot>(JsonUtility.ToJson(network.Snapshot));
            hint.Present(solo, me, hint.HintAfterSeconds + .1f, true, solo: true);
            Assert.That(hint.Visible, Is.True, "DEV solo shows it too, to check it, though nothing is passed.");
            Assert.That(hint.PointingSide, Is.EqualTo(1));
            hint.Present(solo, me, .1f, true, solo: false);
            Assert.That(hint.Visible, Is.False, "A one-player room that is not DEV solo has nobody to pass to.");
            hint.Present(null, me, 0f, false);
            // A three-player copy of the real snapshot.
            LabSnapshot three = JsonUtility.FromJson<LabSnapshot>(JsonUtility.ToJson(network.Snapshot));
            LabPlayerState mine = three.players.Single(player => player.id == me);
            mine.seat = 0;
            three.players = new[] { mine, new LabPlayerState { id = me + 100, seat = 1 }, new LabPlayerState { id = me + 200, seat = 2 } };
            LabOrbState drum = three.drums.Single(orb => orb.owner == me);
            hint.Present(three, me, hint.HintAfterSeconds - .5f, true);
            Assert.That(hint.Visible, Is.False, "Not before the hint time (4 s).");
            hint.Present(three, me, 1f, true);
            Assert.That(hint.Visible, Is.True, "4 s without passing shows the hand and arrow.");
            Assert.That(hint.PointedDrumId, Is.EqualTo(drum.id));
            Assert.That(hint.PointingSide, Is.EqualTo(1), "Both sides open: right first.");
            hint.Present(three, me, Resources.Load<GameTutorialArt>(GameTutorialArt.ResourcePath).handDragSeconds + .05f, true);
            Assert.That(hint.PointingSide, Is.EqualTo(-1), "Then left, one push each.");

            hint.Present(three, me, .1f, false);
            Assert.That(hint.Visible, Is.False, "Hidden while paused or not playing.");
            hint.Present(three, me, .1f, true);
            Assert.That(hint.Visible, Is.True, "Back when the battle goes on and the drum is still here.");

            LabSnapshot passed = JsonUtility.FromJson<LabSnapshot>(JsonUtility.ToJson(three));
            passed.drums.Single(orb => orb.id == drum.id).owner = me + 100;
            hint.Present(passed, me, .1f, true);
            Assert.That(hint.Visible, Is.False, "Passed: gone at once.");
            hint.Present(three, me, .1f, true);
            Assert.That(hint.Visible, Is.False, "Back on my board, it counts from the start again.");
        }

        [UnityTest]
        public IEnumerator ShortStaminaSwellsTheGaugeShowsTheWarningAndFlashesTheEdgesRedOnlyOnTheBattleScreen()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            yield return null;
            var bindings = Object.FindFirstObjectByType<GameBattleBindings>();
            var feedback = bindings.sceneController.GetComponent<LabStaminaShortFeedback>();
            feedback.enabled = false; // only Advance moves its clock here
            var composition = bindings.hud.GetComponent<LabOrbHudComposition>();
            RectTransform display = composition != null && composition.Display != null ? composition.Display.rectTransform : null;
            Vector2 boardAt = display != null ? display.anchoredPosition : Vector2.zero;
            RectTransform gauge = (RectTransform)bindings.hud.staminaFill.transform.parent;
            Vector3 gaugeScale = gauge.localScale;

            feedback.Play();
            feedback.Advance(.08f);
            Assert.That(feedback.GaugeScale, Is.GreaterThan(1.02f), "The gauge swells a little while it shakes (#718).");
            Assert.That(gauge.localScale.x, Is.EqualTo(gaugeScale.x * feedback.GaugeScale).Within(.0001f));
            Assert.That(feedback.ShakeOffset.magnitude, Is.GreaterThan(0f), "And still shakes.");
            Assert.That(feedback.WarningVisible, Is.True);
            Assert.That(feedback.WarningLabel, Is.EqualTo("기운이 부족하다"));
            Assert.That(feedback.WarningIcon, Is.SameAs(Resources.Load<Sprite>(LabStaminaShortFeedback.WarningIconPath)),
                "One group with the red mark.");
            Assert.That(feedback.WarningShake.magnitude, Is.GreaterThan(0f), "The warning shakes with the gauge.");
            Assert.That(feedback.EdgeAlpha, Is.GreaterThan(0f), "The screen edges flash red.");
            if (display != null)
                Assert.That(Vector2.Distance(display.anchoredPosition, boardAt), Is.LessThan(.001f), "The board itself no longer shakes.");

            feedback.Advance(.6f);
            Assert.That(feedback.GaugeScale, Is.EqualTo(1f).Within(.0001f));
            Assert.That(feedback.EdgeAlpha, Is.Zero, "The red is gone after its moment...");
            Assert.That(feedback.WarningVisible, Is.True, "...while the warning stays a little longer.");
            feedback.Advance(1f);
            Assert.That(feedback.Playing, Is.False);
            Assert.That(feedback.WarningVisible, Is.False);
            Assert.That(gauge.localScale, Is.EqualTo(gaugeScale), "The gauge is its own size again.");
        }

        [UnityTest]
        public IEnumerator TerritoryDeveloperSoloUsesDesignedResultsAndReturnsHomeAfterViewingTheGourd()
        {
            session.StartSolo(GameMode.Territory);
            yield return WaitPlaying(1);
            Assert.That(session.IsTerritory, Is.True);
            Assert.That(session.State.totalStages, Is.EqualTo(1));
            Assert.That(network.Snapshot.teamTerritory, Is.True);
            Assert.That(network.config.TeamBattleSeconds, Is.EqualTo(90f));
            Assert.That(network.Snapshot.remaining, Is.InRange(80d, 90d));
            var livePaint = Object.FindFirstObjectByType<LabGourdPaint>();
            Assert.That(livePaint.HighResolutionActive, Is.True);
            Assert.That(livePaint.WatercolorActive, Is.False, "Only the team battle uses the opaque high-resolution presentation.");

            // Complete the real CPU map as a fixture; this is result-flow coverage, not a projectile hit test.
            LabPaintSurface surface = Model().PaintSurface;
            Color32 blue = LabPaintSurface.Opaque(Model().PaintColorFor(LabOrbElement.Water));
            var pixels = Enumerable.Repeat(blue, surface.CellCount).ToArray();
            var owners = Enumerable.Repeat((byte)network.LocalSlotNumber, surface.CellCount).ToArray();
            Assert.That(surface.TryReplacePixels(surface.Columns, surface.Rows, surface.Revision + 1,
                pixels, owners, 1), Is.True);
            yield return Until(() => session.State.phase == GameFlowPhase.Completed && !session.IsLoadingScene
                && SceneManager.GetActiveScene().path == GameScenePaths.StageResult,
                "The actual team sweep must reach the saved designed result Scene.");
            Assert.That(session.ResultSnapshot.teamWinner, Is.EqualTo(LabTeam.Blue));
            Assert.That(session.ResultSnapshot.blueCells, Is.EqualTo(surface.CellCount));
            Assert.That(session.ResultSnapshot.redCells, Is.Zero);
            GameScreen screen = Object.FindFirstObjectByType<GameScreen>();
            // #603: the saved StageResult Scene shows the designed team result; the old result card stays off.
            GameTerritoryResultView territory = screen.territoryView;
            Assert.That(territory, Is.Not.Null, "The saved StageResult Scene wires the #603 team result.");
            yield return Until(() => territory.gameObject.activeInHierarchy, "The team result shows.");
            Assert.That(screen.resultView.gameObject.activeSelf, Is.False);
            Assert.That(screen.resultView.background.gameObject.activeSelf, Is.False);
            Assert.That(screen.legacyResultContent.activeSelf, Is.False);
            GameStageResultView view = territory.table;
            Assert.That(view.background.transform.parent, Is.SameAs(territory.transform));
            Assert.That(territory.transform.parent, Is.SameAs(screen.transform));
            Assert.That(view.background.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(view.background.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(view.judgment.text, Does.Contain("청팀"));
            Assert.That(territory.bluePercent.text, Is.EqualTo("100%"));
            Assert.That(territory.redPercent.text, Is.EqualTo("0%"));
            Assert.That(territory.blueWinnerFlag.activeSelf && !territory.redWinnerFlag.activeSelf, Is.True);
            Assert.That(view.nextButton.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.viewerButton.gameObject.activeSelf && view.homeButton.gameObject.activeSelf, Is.True, "박 확인 and 나가기 stay.");
            yield return Until(() => view.viewerButton.interactable && view.homeButton.interactable,
                "Local result readiness and the pointer-release shield must open the two actions.");
            Texture preview = session.PreviewTexture;
            Assert.That(preview, Is.Not.Null);
            string result = JsonUtility.ToJson(session.ResultSnapshot);
            view.viewerButton.onClick.Invoke();
            yield return Until(() => session.IsViewerOpen && screen.viewerPanel.activeInHierarchy,
                "The team result must open the same owned gourd preview.");
            Assert.That(screen.viewerImage.texture, Is.SameAs(preview));
            Assert.That(JsonUtility.ToJson(session.ResultSnapshot), Is.EqualTo(result));
            screen.closeViewerButton.onClick.Invoke();
            yield return Until(() => !session.IsViewerOpen && !screen.viewerPanel.activeSelf, "Closing the preview restores the team result.");
            yield return Until(() => view.nextButton.interactable, "Retry unlocks when the gourd viewer closes.");
            string finishedBattle = session.State.battleSessionId;
            uint finishedRound = session.State.battleRound;
            view.nextButton.onClick.Invoke();
            yield return WaitPlaying(1);
            Assert.That(session.State.battleSessionId != finishedBattle || session.State.battleRound != finishedRound, Is.True);
            Assert.That(session.PreviewTexture, Is.Null);

            surface = Model().PaintSurface;
            Assert.That(surface.TryReplacePixels(surface.Columns, surface.Rows, surface.Revision + 1,
                pixels, owners, 1), Is.True);
            yield return Until(() => session.State.phase == GameFlowPhase.Completed && !session.IsLoadingScene,
                "The rematch has its own result.");
            view = Object.FindFirstObjectByType<GameScreen>().territoryView.table;
            yield return Until(() => view.homeButton.IsInteractable(), "Exit is available after the rematch.");
            view.homeButton.onClick.Invoke();
            yield return WaitHome();
            Assert.That(network.IsConnected, Is.False);
            Assert.That(session.RoomBrowserVisible, Is.False);
            Assert.That(session.PreviewTexture, Is.Null);
        }

        [Category("GPU")]
        [UnityTest]
        public IEnumerator SavedHomeRunsThreeFreshBattlesWithOneRoomAndKeepsCompletedMapAfterUnload()
        {
            bool UsesShader(Renderer renderer, string shaderName) => renderer != null
                && renderer.sharedMaterials.Any(material => material != null && material.shader != null
                    && material.shader.name == shaderName);

            int[] PartPath(Transform part, Transform visualRoot)
            {
                var path = new System.Collections.Generic.List<int>();
                for (Transform current = part; current != visualRoot; current = current.parent)
                {
                    Assert.That(current, Is.Not.Null, "Every paintable part belongs to the live visual instance.");
                    path.Add(current.GetSiblingIndex());
                }
                path.Reverse();
                return path.ToArray();
            }

            void AssertStampSurfaceBindings(Transform visualRoot, int[][] partPaths, Mesh[] partMeshes,
                LabWatercolorStampSettings expected, Texture[] maps, string context)
            {
                Assert.That(visualRoot, Is.Not.Null, context + ": a gourd model must exist.");
                Renderer[] shells = partPaths.Select(path =>
                {
                    Transform part = visualRoot;
                    foreach (int child in path) part = part.GetChild(child);
                    return part.GetComponent<Renderer>();
                }).ToArray();
                Assert.That(visualRoot.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => UsesShader(renderer, "C6Lab/GourdPaint")), Is.EquivalentTo(shells),
                    context + ": paint sources are the mounted shell and extras, not rope or unpainted art.");
                Assert.That(visualRoot.GetComponentsInChildren<Renderer>(true)
                    .Count(renderer => UsesShader(renderer, "C6Lab/ClayStickerSurface")),
                    Is.EqualTo(expected.UsesClayStickers ? shells.Length : 0),
                    context + ": every paintable part owns exactly one clay coat, without orphan layers.");
                var block = new MaterialPropertyBlock();
                for (int i = 0; i < shells.Length; i++)
                {
                    Renderer shell = shells[i];
                    string partContext = context + ": " + shell.name;
                    Assert.That(shell.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(partMeshes[i]), partContext);
                    if (expected.UsesClayStickers)
                    {
                        Renderer[] coats = shell.transform.Cast<Transform>().Select(child => child.GetComponent<Renderer>())
                            .Where(renderer => UsesShader(renderer, "C6Lab/ClayStickerSurface")).ToArray();
                        Assert.That(coats, Has.Length.EqualTo(1), partContext + ": preserve the part's direct coat.");
                        Renderer coat = coats[0];
                        Assert.That(coat.name, Is.EqualTo("Clay Sticker Overlay"), partContext);
                        Assert.That(coat.enabled, Is.True, partContext);
                        Assert.That(coat.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(partMeshes[i]), partContext);
                        Assert.That(coat.transform.localPosition, Is.EqualTo(Vector3.zero), partContext);
                        Assert.That(Quaternion.Angle(coat.transform.localRotation, Quaternion.identity), Is.LessThan(.0001f), partContext);
                        Assert.That(coat.transform.localScale, Is.EqualTo(Vector3.one), partContext);
                        coat.GetPropertyBlock(block);
                        Assert.That(block.GetTexture("_PaintMap"), Is.SameAs(maps[0]), partContext);
                        Assert.That(block.GetTexture("_PainterMap"), Is.SameAs(maps[1]), partContext);
                        Assert.That(block.GetTexture("_HeightMap"), Is.SameAs(maps[2]), partContext);
                        Assert.That(maps[2], Is.Not.Null, partContext + ": the relief remains owned and available.");
                        Assert.That(block.GetFloat("_PaintMapReady"), Is.EqualTo(1f), partContext);
                    }
                    shell.GetPropertyBlock(block);
                    Assert.That(block.GetTexture("_PaintMap"), Is.SameAs(maps[0]), partContext);
                    Assert.That(block.GetTexture("_PainterMap"), Is.SameAs(maps[1]), partContext);
                    if (expected.UsesClayStickers)
                        Assert.That(block.GetFloat("_PaintMapReady"), Is.Zero, partContext + ": the base must not duplicate its coat.");
                    Assert.That(block.GetFloat("_WatercolorV2Enabled"), Is.EqualTo(expected.UsesPaperStickers || expected.UsesClayStickers ? 0f : 1f),
                        partContext + ": use this round's captured rendering mode independently of the raw paint map.");
                    Assert.That(block.GetFloat("_PaperStickerEnabled"), Is.EqualTo(expected.UsesPaperStickers ? 1f : 0f), partContext);
                    Assert.That(block.GetFloat("_PaperStickerGrainStrength"), Is.EqualTo(expected.paperSticker.grainStrength), partContext);
                    Assert.That(block.GetFloat("_WatercolorGranulationStrength"), Is.EqualTo(expected.granulationStrength), partContext);
                    Assert.That(block.GetFloat("_WatercolorGranulationScale"), Is.EqualTo(expected.granulationScale), partContext);
                }
            }

            string MapHash(Texture map)
            {
                if (map == null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return null;
                var target = map as RenderTexture;
                Assert.That(target, Is.Not.Null, "The high-resolution completed maps must be GPU copies.");
                Assert.That(target.IsCreated(), Is.True);
                var pixels = new Texture2D(target.width, target.height,
                    target.format == RenderTextureFormat.ARGBHalf ? TextureFormat.RGBAHalf : TextureFormat.RGBA32, false, true);
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                    pixels.Apply(false, false);
                    using (var hash = System.Security.Cryptography.SHA256.Create())
                        return Convert.ToBase64String(hash.ComputeHash(pixels.GetRawTextureData<byte>().ToArray()));
                }
                finally { RenderTexture.active = previous; Object.DestroyImmediate(pixels); }
            }

            void AssertPreviewReleased(LabGourdResultPreview preview, Texture[] maps)
            {
                if (preview == null) return;
                Assert.That(preview.IsDisposed, Is.True, "The next stage/Home must release its previous completed copy.");
                foreach (Texture map in maps)
                    Assert.That(map == null || (map is RenderTexture rt && !rt.IsCreated()), Is.True,
                        "Previous color, P-number and height copies must not accumulate across stages.");
            }

            LabWatercolorStampSettings defaultWatercolor = LabWatercolorStampSettings.CaptureDefault();
            Assert.That(defaultWatercolor.enabled, Is.True, "Saved Home must start with the default watercolor presentation.");
            GameSession persistent = session;
            LabNetwork persistentNetwork = network;
            string previousBattleId = null;
            LabOrbBoard previousBoard = null;
            LabGourdResultPreview previousPreview = null;
            Texture[] previousPreviewMaps = null;
            session.StartSolo();

            for (int stage = 1; stage <= 3; stage++)
            {
                yield return WaitPlaying(stage);
                AssertPreviewReleased(previousPreview, previousPreviewMaps);
                Assert.That(GameSession.Instance, Is.SameAs(persistent));
                Assert.That(session.Network, Is.SameAs(persistentNetwork));
                Assert.That(network.IsConnected, Is.True);
                Assert.That(network.IsDeveloperSolo, Is.True);
                Assert.That(network.Snapshot.sessionId, Is.Not.EqualTo(previousBattleId));
                Assert.That(network.board.gameObject.scene.path, Is.EqualTo(GameScenePaths.Battle));
                Assert.That(network.target.gameObject.scene.path, Is.EqualTo(GameScenePaths.Battle));
                Assert.That(Object.FindObjectsByType<LabResultDashboard>(FindObjectsInactive.Include, FindObjectsSortMode.None),
                    Is.Empty, "Managed Battle must not attach the standalone Lab result dashboard.");
                if (stage > 1) Assert.That(previousBoard == null, Is.True, "The previous Battle board must be unloaded.");
                AssertFreshStage();
                Assert.That(Model().StageYokai, Is.EqualTo((LabStageYokai)stage),
                    "The shared Battle scene must create the selected stage's Host policy, not the legacy Lab mix.");
                Assert.That(session.CurrentStageDefinition.stageIndex, Is.EqualTo(stage));
                if (stage == 1)
                {
                    var staleRoot = new GameObject("Stale copied Lab lobby fixture");
                    staleRoot.SetActive(false);
                    var staleLobby = staleRoot.AddComponent<LabLobbyView>();
                    staleLobby.hud = network.board.gameObject.scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<LabHud>(true)).Single();
                    staleRoot.SetActive(true);
                    Assert.That(staleRoot.activeSelf, Is.False,
                        "Even a stale copied standalone view must refuse game-flow activation before touching widgets or advertising rooms.");
                    Object.Destroy(staleRoot);
                }


                var jigwiHud = SceneManager.GetActiveScene().GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<LabJigwiHud>(true)).Single();
                yield return null; // The saved HUD evaluates the real stage policy in LateUpdate.
                LabHud battleHud = SceneManager.GetActiveScene().GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<LabHud>(true)).Single();
                LabOrbHudComposition composition = battleHud.GetComponent<LabOrbHudComposition>();
                Assert.That(composition, Is.Not.Null, "Every shared Battle instance must attach the visual-only compositor.");
                Assert.That(composition.IsCompositing, Is.True, composition.InitializationError);
                Assert.That(battleHud.orbCamera.targetTexture, Is.Null,
                    "The saved camera remains the on-screen physics/input projection across all three stages.");
                Assert.That(composition.Display.transform.parent, Is.SameAs(battleHud.battleUi.transform));
                Assert.That(composition.Display.raycastTarget, Is.False);
                Assert.That(composition.ForegroundCamera.cullingMask & (1 << network.board.gameObject.layer), Is.Not.Zero);
                bool legacyJigwiManual = stage == 3 && !network.config.JigwiStageTimelineEnabled;
                Assert.That(network.JigwiManualControlsAllowed, Is.EqualTo(legacyJigwiManual));
                Assert.That(jigwiHud.testToggle.gameObject.activeInHierarchy, Is.EqualTo(legacyJigwiManual),
                    "Only an explicitly selected legacy Jigwi policy may expose the standalone manual panel; the stage timeline owns its own A/B patterns.");
                if (stage == 3)
                {
                    session.Pause();
                    yield return null;
                    Assert.That(jigwiHud.testToggle.gameObject.activeInHierarchy, Is.EqualTo(legacyJigwiManual),
                        "Pause preserves the selected policy: the timeline hides legacy controls, while the legacy fallback keeps its entry visible.");
                    Assert.That(jigwiHud.fireButton.interactable, Is.False);
                    Assert.That(jigwiHud.generationButton.interactable, Is.False);
                    session.Resume();
                    yield return null;
                }

                Assert.That(network.PinnedWatercolorSettings, Is.Not.Null);
                LabWatercolorStampSettings roundWatercolor = network.PinnedWatercolorSettings.Clone();
                Assert.That(roundWatercolor.Version, Is.EqualTo(defaultWatercolor.Version),
                    "Each fresh Battle must capture the saved default V2 appearance, not inherit a previous round's settings.");
                LabGourdPaint livePaint = network.target.GetComponent<LabGourdPaint>();
                Assert.That(livePaint, Is.Not.Null);
                Assert.That(roundWatercolor.UsesClayStickers ? livePaint.ClayStickersActive : livePaint.WatercolorStampsActive, Is.True, livePaint.WatercolorStampError);
                Transform liveVisualRoot = livePaint.VisualInstance.transform;
                Renderer[] paintParts = new[] { livePaint.Shell.GetComponent<Renderer>() }
                    .Concat(livePaint.PaintExtras).Distinct().ToArray();
                Assert.That(paintParts, Is.Not.Empty);
                Assert.That(paintParts.All(part => part != null), Is.True);
                int[][] paintPartPaths = paintParts.Select(part => PartPath(part.transform, liveVisualRoot)).ToArray();
                Mesh[] paintPartMeshes = paintParts.Select(part => part.GetComponent<MeshFilter>().sharedMesh).ToArray();
                Texture[] liveMaps = { livePaint.VisualPaintTexture, livePaint.VisualPainterTexture, livePaint.VisualHeightTexture };
                AssertStampSurfaceBindings(liveVisualRoot, paintPartPaths, paintPartMeshes, roundWatercolor, liveMaps,
                    "Live Battle stage " + stage);

                LabOrbBoard thisBoard = network.board;
                network.Generate();
                yield return Until(() => network.Snapshot.players.Single().generatedTotal == 1,
                    "The real network Generate path must commit one generated orb.");
                LabModel model = Model();
                Assert.That(model.TryApplyPlayerDamage(network.LocalPlayerId, 13, ModelTime(model)), Is.True);
                Assert.That(model.CopyPlayers().Single().personalHp, Is.EqualTo(224));
                Assert.That(model.CopyPlayers().Single().stamina, Is.EqualTo(45f).Within(.01f));
                Assert.That(model.CopyOwnedStates(network.LocalPlayerId).Any(orb => !orb.kind.IsDrum()), Is.True);

                string runId = session.State.runId;
                previousBattleId = network.Snapshot.sessionId;
                CompleteMapInFixture(model);
                double victoryAt = Time.realtimeSinceStartupAsDouble;
                yield return Until(() => session.IsFinishingBattle && session.PreviewTexture != null,
                    "Each stage must capture its completed preview while the Battle finish is still running.");
                var preparedPreview = (LabGourdResultPreview)typeof(GameSession).GetField("preview",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                Texture preparedTexture = session.PreviewTexture;
                Texture preparedVisualPaint = preparedPreview.VisualPaintTexture;
                Texture[] preparedMaps = { preparedVisualPaint, preparedPreview.VisualPainterTexture, preparedPreview.VisualHeightTexture };
                liveMaps = new[] { livePaint.VisualPaintTexture, livePaint.VisualPainterTexture, livePaint.VisualHeightTexture };
                string[] preparedMapHashes = preparedMaps.Select(MapHash).ToArray();
                for (int i = 0; i < preparedMaps.Length; i++)
                {
                    if (liveMaps[i] == null) continue;
                    Assert.That(preparedMaps[i], Is.Not.Null, "Every live visual map has an independent completed copy.");
                    Assert.That(preparedMaps[i], Is.Not.SameAs(liveMaps[i]), "Completed color, P-number and relief do not borrow live maps.");
                    Assert.That(MapHash(liveMaps[i]), Is.EqualTo(preparedMapHashes[i]), "Freezing must preserve each visual map's pixels.");
                }
                string completedCpuHash = preparedPreview.PaintPixelHash;
                Color32[] completedCpuPixels = preparedPreview.PaintTexture.GetPixels32();
                Transform copiedVisualRoot = preparedPreview.ModelRoot.Find("Centered model").GetChild(0);
                Assert.That(copiedVisualRoot.name, Is.EqualTo(liveVisualRoot.name));
                Assert.That(network.HasBattleReferences, Is.True, "Pre-capture uses the live Battle before unlink/unload.");
                Assert.That(thisBoard != null, Is.True);
                Assert.That(preparedPreview.SessionId, Is.EqualTo(previousBattleId));
                Assert.That(preparedPreview.Round, Is.EqualTo(session.State.battleRound));
                AssertStampSurfaceBindings(copiedVisualRoot, paintPartPaths, paintPartMeshes, roundWatercolor, preparedMaps,
                    "Prepared result stage " + stage);
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    Assert.That(preparedVisualPaint, Is.InstanceOf<RenderTexture>());
                    Assert.That(((RenderTexture)preparedVisualPaint).IsCreated(), Is.True,
                        "The GPU copy must already exist during the finish rather than being deferred until Scene change.");
                }
                GameFlowPhase resultPhase = stage == 3 ? GameFlowPhase.Completed : GameFlowPhase.StageResult;
                yield return Until(() => session.State.phase == resultPhase && !session.IsLoadingScene
                    && SceneManager.GetActiveScene().path == GameScenePaths.StageResult,
                    "The actual committed victory must load StageResult.");
                TestContext.WriteLine("Stage " + stage + " Victory to StageResult: "
                    + (Time.realtimeSinceStartupAsDouble - victoryAt).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "s");
                Assert.That(session.State.runId, Is.EqualTo(runId));
                Assert.That(session.State.stageIndex, Is.EqualTo(stage));
                Assert.That(network.HasBattleReferences, Is.False);
                Assert.That(thisBoard == null, Is.True);
                Assert.That(session.ResultSnapshot, Is.Not.Null);
                Assert.That(session.ResultSnapshot.sessionId, Is.EqualTo(previousBattleId));
                Assert.That(session.ResultSnapshot.paintCoveredCount, Is.EqualTo(session.ResultSnapshot.paintCellCount));
                Assert.That(session.ResultSnapshot.players.Single().generatedTotal, Is.EqualTo(1));
                Assert.That(session.PreviewTexture, Is.Not.Null, "The completed paint preview must survive Battle unload.");
                Assert.That(session.PreviewTexture, Is.SameAs(preparedTexture), "Scene navigation reuses the pre-captured render target.");
                Assert.That(typeof(GameSession).GetField("preview", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session),
                    Is.SameAs(preparedPreview), "Scene navigation reuses the same completed-result copy.");
                Assert.That(preparedPreview.VisualPaintTexture, Is.SameAs(preparedVisualPaint));
                AssertStampSurfaceBindings(copiedVisualRoot, paintPartPaths, paintPartMeshes, roundWatercolor, preparedMaps,
                    "Result after Battle unload stage " + stage);
                Assert.That(preparedPreview.VisualPainterTexture, Is.SameAs(preparedMaps[1]));
                Assert.That(preparedPreview.VisualHeightTexture, Is.SameAs(preparedMaps[2]));
                Assert.That(preparedMaps.Select(MapHash).ToArray(), Is.EqualTo(preparedMapHashes),
                    "All painted parts retain the exact frozen color, P-number and height after the live Battle is destroyed.");
                Assert.That(preparedPreview.PaintPixelHash, Is.EqualTo(completedCpuHash));
                Assert.That(preparedPreview.PaintTexture.GetPixels32(), Is.EqualTo(completedCpuPixels));
                Assert.That(livePaint == null, Is.True, "The result cannot still be using a live paint component.");
                foreach (Texture map in liveMaps)
                    Assert.That(map == null || (map is RenderTexture rt && !rt.IsCreated()), Is.True,
                        "Completed copies stay valid after their source GPU maps are released.");
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    var captured = (LabGourdResultPreview)typeof(GameSession).GetField("preview",
                        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                    Assert.That(captured.VisualPaintTexture, Is.InstanceOf<RenderTexture>());
                    Assert.That(captured.VisualPaintTexture.width, Is.EqualTo(1024));
                    Assert.That(captured.VisualPaintTexture.height, Is.EqualTo(512));
                    Assert.That(((RenderTexture)captured.VisualPaintTexture).IsCreated(), Is.True,
                        "All three completed-stage GPU maps survive unloading their live Battle.");
                }
                if (stage == 1)
                {
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    typeof(GameSession).GetProperty("ResultSnapshot").SetValue(session, null);
                    var received = (System.Collections.Generic.HashSet<ulong>)typeof(GameSession).GetField("resultReady", flags).GetValue(session);
                    received.Clear();
                    typeof(GameSession).GetField("nextResultReadyAt", flags).SetValue(session, 0d);
                    GameScreen screen = Object.FindFirstObjectByType<GameScreen>();
                    typeof(GameScreen).GetMethod("RefreshDesignedResult", flags).Invoke(screen, new object[] { network, true });
                    Assert.That(session.ResultSnapshot, Is.Null);
                    Assert.That(session.CanAdvanceStage, Is.False, "A missing required record cannot count as ready.");
                    Assert.That(screen.resultView.guidance.text, Does.Contain("결과 기록을 받고 있소"));
                    yield return Until(() => session.ResultSnapshot != null,
                        "The result screen must recover the matching latest Victory before acknowledging readiness again.");
                    Assert.That(session.ResultSnapshot.sessionId, Is.EqualTo(previousBattleId));
                    Assert.That(session.ResultSnapshot.round, Is.EqualTo(preparedPreview.Round));
                    yield return Until(() => received.Contains(network.LocalPlayerId),
                        "Record recovery must send the matching readiness acknowledgement again.");
                    Assert.That(session.PreviewTexture, Is.SameAs(preparedTexture), "Record recovery keeps the existing optional preview.");
                }
                yield return FinishResultStory();
                previousBoard = thisBoard;
                previousPreview = preparedPreview;
                previousPreviewMaps = preparedMaps;
                if (stage < 3)
                {
                    Assert.That(session.ConfirmSealReady(), Is.True, "The local participant explicitly confirms the next seal.");
                    yield return Until(() => session.CanAdvanceStage,
                        "Host must wait for every copied result and loaded result Scene before NEXT.");
                    yield return null;
                    session.NextStage();
                    session.NextStage(); // A repeated button must not skip the new loading generation.
                    Assert.That(session.State.stageIndex, Is.EqualTo(stage + 1));
                }
            }

            yield return Until(() => session.CanReturnHome,
                "Final Host exit must wait for every result acknowledgement.");
            yield return null;
            session.ExitRun(); // Normal final completion exits without creating GameOver.
            yield return WaitHome();
            AssertPreviewReleased(previousPreview, previousPreviewMaps);
            Assert.That(GameSession.Instance, Is.SameAs(persistent));
            Assert.That(session.State.endReason, Is.Empty);
            Assert.That(session.ResultSnapshot, Is.Null);
            Assert.That(session.PreviewTexture, Is.Null);
            Assert.That(network.IsConnected, Is.False);
            Assert.That(session.RoomBrowserVisible, Is.False, "Final clear returns to the title page.");
        }

        [UnityTest]
        public IEnumerator MidStageExitLatchesGameOverAndWaitsForExplicitHome()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            session.ExitRun();
            yield return WaitGameOver();
            string reason = session.State.endReason;
            uint revision = session.State.revision;
            Assert.That(reason, Does.Contain("left"));
            for (int frame = 0; frame < 5; frame++) yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(GameScenePaths.GameOver));
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.GameOver));
            Assert.That(session.State.endReason, Is.EqualTo(reason));
            Assert.That(session.State.revision, Is.EqualTo(revision));
            Assert.That(network.HasBattleReferences, Is.False);
            session.GoHome();
            yield return WaitHome();
        }

        [UnityTest]
        public IEnumerator FigmaResultsWaitForHostNextAndShowEachYokaiThenKeepFinalOverallAndHomeNavigation()
        {
            session.StartSolo();
            string[] stories = { "AfterGumiho", "AfterYagwanggui", "AfterJigwi" };
            for (int stage = 1; stage <= 3; stage++)
            {
                yield return WaitPlaying(stage);
                CompleteMapInFixture(Model());
                yield return WaitResultStory();
                GameStageStoryPresentation story = ResultStory();
                Assert.That(story.player.sequence, Is.SameAs(story.afterStages[stage - 1]),
                    "Each result plays only the current yokai's card and after-story.");
                Assert.That(story.player.sequence.cuts, Has.Count.EqualTo(3));
                Assert.That(story.afterStages[stage - 1].name, Is.EqualTo(stories[stage - 1]));
                AssertCurrentCard(story, session.ResultSnapshot);
                Assert.That(session.IsAnyResultPresentationBusy, Is.True);
                if (stage < 3) Assert.That(session.CanAdvanceStage, Is.False,
                    "Receiving the record cannot bypass the current story.");
                yield return FinishResultStory();
                StoryCutView clearView = story.player.CurrentView;
                StoryStageClearControls clear = clearView.GetComponent<StoryStageClearControls>();
                Assert.That(clear, Is.Not.Null, "SKIP lands on the stage's last authored clear picture.");
                Assert.That(clearView.GetComponentInChildren<StoryCardFlip>(true), Is.Null,
                    "SKIP does not instantiate the seal card again.");
                Assert.That(clear.finalStage, Is.EqualTo(stage == 3));
                Assert.That(story.BoundResult, Is.SameAs(session.ResultSnapshot));
                Assert.That(story.player.nextButton.gameObject.activeInHierarchy, Is.False,
                    "The authoring toolbar must not add a second progression button to the game.");
                Assert.That(story.advanceButton.gameObject.activeInHierarchy, Is.False,
                    "The clear picture's own 다음 봉인 replaces the footer advance button.");
                if (stage < 3)
                {
                    yield return Until(() => clear.nextSeal.gameObject.activeInHierarchy && clear.nextSeal.interactable,
                        "The Host starts the next stage only after story and result readiness.");
                    Assert.That(story.exitButton.gameObject.activeInHierarchy, Is.False,
                        "The approved clear picture has no footer 나가기.");
                    if (stage == 1)
                    {
                        // Keep the retired automatic-advance regression independent of viewer activity.
                        yield return new WaitForSecondsRealtime(10.5f);
                        Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.StageResult));
                        Assert.That(session.State.stageIndex, Is.EqualTo(1));
                        Assert.That(story.player.CurrentView.GetComponent<StoryResultActions>(), Is.Null);
                        Assert.That(story.viewerButton.gameObject.activeInHierarchy, Is.False,
                            "The approved clear picture has no footer 완성 박 보기.");
                        // Inspection itself (opened earlier from the seal card) still holds the Host's 다음 봉인.
                        session.OpenGourdViewer();
                        yield return Until(() => session.IsViewerOpen, "The captured gourd can still be inspected.");
                        yield return null;
                        Assert.That(story.presentationGroup.alpha, Is.Zero);
                        Assert.That(clear.nextSeal.interactable, Is.False);
                        story.Advance();
                        Assert.That(session.State.stageIndex, Is.EqualTo(1));
                        session.CloseGourdViewer();
                        yield return Until(() => clear.nextSeal.interactable && story.presentationGroup.alpha == 1,
                            "Closing inspection restores the same result and manual NEXT.");
                        Assert.That(story.player.CurrentView, Is.SameAs(clearView));
                        Assert.That(story.BoundResult, Is.SameAs(session.ResultSnapshot));

                        typeof(GameSession).GetMethod("SetResultViewer", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(session, new object[] { 72UL, true });
                        yield return null;
                        Assert.That(session.IsAnyResultViewerOpen, Is.True);
                        Assert.That(session.CanAdvanceStage, Is.False, "A peer inspection blocks shared start, not local readiness.");
                        story.StartPreparedStage();
                        Assert.That(session.State.stageIndex, Is.EqualTo(1));
                        typeof(GameSession).GetMethod("SetResultViewer", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(session, new object[] { 72UL, false });
                        yield return Until(() => clear.nextSeal.interactable, "A closed peer viewer restores NEXT.");
                        typeof(GameSession).GetMethod("SetResultPresentation", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(session, new object[] { 72UL, true });
                        yield return null;
                        Assert.That(session.CanAdvanceStage, Is.False, "A peer story still blocks shared start.");
                        story.StartPreparedStage();
                        Assert.That(session.State.stageIndex, Is.EqualTo(1));
                        typeof(GameSession).GetMethod("SetResultPresentation", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(session, new object[] { 72UL, false });
                        yield return Until(() => clear.nextSeal.interactable, "Finishing the peer story restores the one Host action.");

                        GameScreen screen = story.screen;
                        screen.gameObject.SetActive(false);
                        screen.legacyResultContent.SetActive(true);
                        screen.gameObject.SetActive(true);
                        Assert.That(story.BoundResult, Is.SameAs(session.ResultSnapshot),
                            "Re-enabling binds the actual story before another render frame.");
                        Assert.That(screen.resultView.gameObject.activeSelf, Is.False);
                        Assert.That(screen.legacyResultContent.activeSelf, Is.False);
                        Assert.That(story.player.UserInputBlocked, Is.True, "Re-entry shields inherited releases.");
                        yield return FinishResultStory();
                        clear = story.player.CurrentView.GetComponent<StoryStageClearControls>();
                        Assert.That(clear, Is.Not.Null, "Re-entry lands on the same authored clear picture.");
                        yield return Until(() => clear.nextSeal.interactable, "Released re-entry restores manual NEXT.");
                    }
                    clear.nextSeal.onClick.Invoke();
                    Assert.That(clear.nextSeal.interactable, Is.False, "Accepted NEXT disables the old button immediately.");
                    clear.nextSeal.onClick.Invoke();
                    Assert.That(session.State.stageIndex, Is.EqualTo(stage), "Next seal only opens the waiting page.");
                    yield return Until(() => story.ReadyView.start.IsInteractable(), "All participants enable the Host start.");
                    story.ReadyView.start.onClick.Invoke();
                    story.ReadyView.start.onClick.Invoke();
                    Assert.That(session.State.stageIndex, Is.EqualTo(stage + 1), "Repeated input cannot skip a loading generation.");
                    continue;
                }
                LabSnapshot snapshot = session.ResultSnapshot;
                Assert.That(snapshot.sessionId, Is.EqualTo(session.State.battleSessionId));
                Assert.That(snapshot.round, Is.EqualTo(session.State.battleRound));
                Assert.That(snapshot.phase, Is.EqualTo(LabPhase.Victory));
                Assert.That(story.exitButton.gameObject.activeInHierarchy, Is.False,
                    "The final result has one ending action, not an Exit button relabelled to the same action.");
                string sealCaption = clear.nextSeal.GetComponentInChildren<UnityEngine.UI.Text>(true)?.text;
                Assert.That(sealCaption, Is.EqualTo(StoryStageClearControls.FinishLabel), "The last yokai's button reads 마치기.");
                Assert.That(clear.heading.gameObject.activeInHierarchy, Is.False, "No 다음 요괴 잡으러 ... after the last yokai.");
                Assert.That(story.presentationRoot.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                    .Concat(story.player.CurrentView.GetComponentsInChildren<UnityEngine.UI.Button>(true)).Distinct()
                    .Count(button => button.gameObject.activeInHierarchy
                        && button.GetComponentInChildren<UnityEngine.UI.Text>(true)?.text == sealCaption), Is.EqualTo(1),
                    "다음 봉인 is the single Host action on the final clear picture.");
                Assert.That(story.overallButton.gameObject.activeInHierarchy, Is.False,
                    "The overall record moved into the ending's SealRunSummary cut.");
                Assert.That(story.IsShowingOverall, Is.False);
                yield return Until(() => clear.nextSeal.gameObject.activeInHierarchy && clear.nextSeal.interactable,
                    "The final result waits for result readiness.");
                // A member that leaves while still reading the final result must not block 다음 봉인 for good.
                typeof(GameSession).GetMethod("SetResultPresentation", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(session, new object[] { 72UL, true });
                yield return null;
                Assert.That(session.CanStartSummary, Is.False, "A reading peer holds the summary start.");
                typeof(GameSession).GetMethod("OnPeerDisconnected", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(session, new object[] { 72UL, false });
                yield return Until(() => clear.nextSeal.interactable, "A peer that left no longer holds the summary start.");
                Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Completed), "A member leaving the final result keeps the clear.");
                // #642: a completed run keeps its local result identity after the connection ends, and with no Host
                // left to press 마치기 the summary and ending open by themselves.
                ulong completedPlayerId = network.LocalPlayerId;
                Assert.That(session.ResultLocalPlayerId, Is.EqualTo(completedPlayerId));
                network.Stop();
                yield return null;
                Assert.That(network.LocalPlayerId, Is.EqualTo(ulong.MaxValue), "The transport identity disappears after disconnect.");
                Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Completed), "A completed run retains its local result.");
                Assert.That(session.ResultLocalPlayerId, Is.EqualTo(completedPlayerId),
                    "Opening the summary after the connection ends still identifies the actual result owner.");
                yield return Until(() => story.player.sequence == story.ending,
                    "Without a connection the summary and ending start without waiting for 마치기.");
                Assert.That(story.player.sequence.cuts, Has.Count.EqualTo(7),
                    "SealRunSummary plus the six ending cuts; do not show the authored fake Ending_07 title.");
                Assert.That(story.ending.cuts[0].viewPrefab.GetComponent<SealRunSummary>(), Is.Not.Null,
                    "The ending starts with the run summary.");
                foreach (StoryCutDefinition cut in story.ending.cuts) Assert.That(cut.cutId, Is.Not.EqualTo("Ending_07"));
                AssertSealSummaryFromActualStageRecords(story, true);
                Assert.That(story.skipButton.gameObject.activeSelf, Is.False, "The summary must be read before its Continue action.");
                Assert.That(story.screen.resultView.gameObject.activeInHierarchy, Is.False,
                    "The authored summary replaces the retired overall dashboard rather than layering over it.");
                yield return Until(() => story.player.CurrentView.continueButton.gameObject.activeInHierarchy
                    && story.player.CurrentView.continueButton.IsInteractable() && story.player.IsReading,
                    "The summary's own Continue becomes available after its entrance.");
                story.player.CurrentView.continueButton.onClick.Invoke();
                Assert.That(story.player.IsTransitioning, Is.True, "Continue starts the authored cloud wipe before replacing the summary.");
                yield return Until(() => story.player.CurrentCutIndex == 1 && !story.player.IsTransitioning && story.player.IsReading,
                    "The cloud wipe must finish on the actual first ending picture.");
                Assert.That(story.player.sequence.cuts[story.player.CurrentCutIndex].cutId, Is.EqualTo("Ending_01"));
                story.player.Skip();
                yield return WaitHome();
                Assert.That(network.IsConnected, Is.False);
                Assert.That(Object.FindFirstObjectByType<GameLobbyView>(), Is.Not.Null,
                    "Ending returns to the actual saved Home, not a duplicate story title.");
                yield return null;
                Assert.That(Object.FindFirstObjectByType<GameLobbyView>().titlePage.activeInHierarchy, Is.True,
                    "The ending returns to the title screen, not the room list.");
            }
        }

        [UnityTest]
        public IEnumerator NormalAfterStoryCompletionKeepsItsLastCutAndDoesNotReplayTheSealCard()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            CompleteMapInFixture(Model());
            yield return WaitResultStory();
            GameStageStoryPresentation story = ResultStory();
            yield return Until(() => !story.player.UserInputBlocked, "The actual result entry shield releases.");
            StoryCutView firstCard = story.player.CurrentView;
            AssertCurrentCard(story, session.ResultSnapshot);
            int viewChanges = 0;
            story.player.ViewChanged += _ => viewChanges++;
            Assert.That(story.skipButton.gameObject.activeSelf, Is.False, "The seal card has no SKIP.");
            yield return AdvancePast(story.player, () => story.player.CurrentView == firstCard);
            Assert.That(story.skipButton.gameObject.activeSelf, Is.True, "SKIP appears on the after-story pictures.");
            int steps = 0;
            while (story.player.IsPlaying && steps++ < 40)
            {
                // Direct cut sequencing is intentional here; genuine input is exercised by the neighbouring mouse test.
                story.player.Next();
                if (story.player.IsTransitioning)
                {
                    story.player.AdvanceTime(1f);
                    story.player.AdvanceTime(1f);
                }
                AssertNoInlineContinue(story.player.CurrentView);
                if (!story.player.IsPlaying)
                {
                    Assert.That(story.advanceButton.interactable, Is.False, "The story-ending frame cannot also activate NEXT.");
                    var endingClear = story.player.CurrentView.GetComponent<StoryStageClearControls>();
                    Assert.That(endingClear == null || !endingClear.nextSeal.interactable, Is.True,
                        "The story-ending frame cannot also activate 다음 봉인.");
                }
                yield return null;
            }
            Assert.That(story.player.IsPlaying, Is.False);
            StoryCutView retained = story.player.CurrentView;
            Assert.That(retained, Is.Not.Null);
            Assert.That(story.player.CurrentCutIndex, Is.EqualTo(2));
            Assert.That(retained.GetComponentInChildren<StoryCardRecords>(true), Is.Null,
                "Normal completion stays on the last after-story picture, not a recreated first card.");
            StoryStageClearControls clear = retained.GetComponent<StoryStageClearControls>();
            Assert.That(clear, Is.Not.Null, "The last after-story picture is the authored clear picture.");
            yield return Until(() => story.IsLocalStoryComplete && clear.nextSeal.gameObject.activeInHierarchy
                    && clear.nextSeal.interactable,
                "Released story completion enables the one Host stage action.");
            Assert.That(story.advanceButton.gameObject.activeInHierarchy, Is.False,
                "The clear picture's 다음 봉인 replaces the footer advance button.");
            Assert.That(story.player.CurrentView, Is.SameAs(retained));
            Assert.That(viewChanges, Is.EqualTo(2), "Only the two remaining authored cuts may be instantiated.");
            Assert.That(firstCard == null, Is.True);
            Assert.That(story.player.sequence, Is.SameAs(story.afterStages[0]));
            AssertNoInlineContinue(story.player.CurrentView);
            Assert.That(story.player.nextButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(story.viewerButton.gameObject.activeInHierarchy, Is.False,
                "The approved clear picture has no footer 완성 박 보기 or 나가기.");
            Assert.That(story.exitButton.gameObject.activeInHierarchy, Is.False);
            session.OpenGourdViewer();
            yield return Until(() => session.IsViewerOpen, "The captured gourd can still be inspected while the picture waits.");
            session.CloseGourdViewer();
            yield return Until(() => clear.nextSeal.interactable, "Inspection restores the same manual stage action.");
            Assert.That(story.player.CurrentView, Is.SameAs(retained));
            Assert.That(viewChanges, Is.EqualTo(2), "Closing inspection does not replay any story cut.");
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.StageResult));
            Assert.That(session.State.stageIndex, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SkipDuringAfterStoryReadingKeepsThePictureWithoutAnInlineNextAction()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            CompleteMapInFixture(Model());
            yield return WaitResultStory();
            GameStageStoryPresentation story = ResultStory();
            yield return Until(() => !story.player.UserInputBlocked, "The actual result entry shield releases.");
            int steps = 0;
            while ((story.player.CurrentCutIndex != 1 || !story.player.IsReading) && steps++ < 20)
            {
                story.player.Next();
                if (story.player.IsTransitioning)
                {
                    story.player.AdvanceTime(1f);
                    story.player.AdvanceTime(1f);
                }
                AssertNoInlineContinue(story.player.CurrentView);
                yield return null;
            }
            Assert.That(story.player.CurrentCutIndex, Is.EqualTo(1));
            Assert.That(story.player.IsReading, Is.True, "Exercise an actual after-story dialogue, not only the initial card.");
            StoryCutView skipped = story.player.CurrentView;
            Assert.That(skipped.GetComponent<StoryStageClearControls>(), Is.Null,
                "Skip from an after-story picture before the authored clear picture.");
            AssertNoInlineContinue(skipped);
            story.Skip();
            yield return Until(() => story.IsLocalStoryComplete && story.player.CurrentView != null
                    && story.player.CurrentView.GetComponent<StoryStageClearControls>() != null
                    && story.player.CurrentView.GetComponent<StoryStageClearControls>().nextSeal.interactable,
                "Skipping the reading cut lands on the clear picture and releases the one explicit Host stage action.");
            StoryCutView retained = story.player.CurrentView;
            StoryStageClearControls clear = retained.GetComponent<StoryStageClearControls>();
            Assert.That(story.player.sequence.cuts, Has.Count.EqualTo(1));
            Assert.That(story.player.sequence.cuts[0], Is.SameAs(story.afterStages[0].cuts[story.afterStages[0].cuts.Count - 1]),
                "SKIP shows the stage's last authored picture once.");
            Assert.That(retained.GetComponentInChildren<StoryCardFlip>(true), Is.Null, "SKIP does not replay the seal card.");
            Assert.That(retained.continueButton, Is.Null,
                "The clear picture's 다음 봉인 replaces the removed inline next-yokai control.");
            AssertNoInlineContinue(retained);
            Assert.That(story.player.nextButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(story.advanceButton.gameObject.activeInHierarchy, Is.False, "No footer duplicate of 다음 봉인.");
            string sealCaption = clear.nextSeal.GetComponentInChildren<UnityEngine.UI.Text>(true)?.text;
            Assert.That(story.presentationRoot.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                .Concat(retained.GetComponentsInChildren<UnityEngine.UI.Button>(true)).Distinct()
                .Count(button => button.gameObject.activeInHierarchy
                    && button.GetComponentInChildren<UnityEngine.UI.Text>(true)?.text == sealCaption), Is.EqualTo(1));
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.StageResult));
        }

        private void RestoreReturnBackgroundSource()
        {
            if (returnBackgroundEntry != null) returnBackgroundEntry.sprite = returnBackgroundSprite;
            returnBackgroundEntry = null;
            returnBackgroundSprite = null;
        }

        private static void AssertViewerReturnBackground(GameScreen screen, bool fallback)
        {
            var button = screen.closeViewerButton;
            var artwork = button.GetComponentInChildren<GameFigmaSurface>(true);
            var fill = button.GetComponentsInChildren<LabRoundedGraphic>(true).Single();
            Assert.That(artwork.enabled, Is.EqualTo(!fallback));
            Assert.That(fill.enabled, Is.EqualTo(fallback));
            Assert.That(button.GetComponentsInChildren<GameFigmaSurface>(true).Length, Is.EqualTo(1));
            Canvas.ForceUpdateCanvases();
            var mesh = (fallback ? (UnityEngine.UI.Graphic)fill : artwork).canvasRenderer.GetMesh();
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.vertexCount, Is.GreaterThanOrEqualTo(4), "Return background needs a drawn mesh after reopening.");
        }

        [UnityTest]
        public IEnumerator SavedStoryViewerSkipAndEndingButtonsReceiveRealMouseClicks()
        {
            InputSettings original = InputSystem.settings;
            HideFlags originalFlags = original.hideFlags;
            Mouse previousMouse = Mouse.current;
            original.hideFlags = HideFlags.DontUnloadUnusedAsset;
            InputSettings fixture = ScriptableObject.CreateInstance<InputSettings>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(original), fixture);
            fixture.hideFlags = HideFlags.HideAndDontSave;
            fixture.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            fixture.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            fixture.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            InputSystem.settings = fixture;
            Mouse mouse = InputSystem.AddDevice<Mouse>("Saved story button test mouse");
            mouse.MakeCurrent();
            try
            {
                session.StartSolo();
                GameStageStoryPresentation story = null;
                for (int stage = 1; stage <= 3; stage++)
                {
                    yield return WaitPlaying(stage);
                    CompleteMapInFixture(Model());
                    yield return WaitResultStory();
                    story = ResultStory();
                    yield return Until(() => !story.player.UserInputBlocked && story.player.IsReading
                        && !session.LoadingView.Visible && !session.SceneFade.BlocksInput,
                        "The saved result must finish its entry shield before genuine UI input.");
                    StoryCutView sealCard = story.player.CurrentView;
                    var flip = sealCard.GetComponentInChildren<StoryCardFlip>(true);
                    flip.Flip();
                    var inspect = sealCard.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                        .Single(button => button.name == "Inspect Gourd Action");
                    yield return Until(() => flip.IsBack && !flip.IsFlipping && inspect.IsInteractable(),
                        "The back of the seal card exposes its inspect button.");
                    Assert.That(session.IsViewerOpen, Is.False, "Inspection starts from the result card.");
                    Assert.That(story.screen.viewerPanel.activeInHierarchy, Is.False);
                    yield return ClickVisibleButtonWithMouse(mouse, inspect);
                    GameScreen screen = story.screen;
                    Assert.That(session.IsViewerOpen && screen.viewerPanel.activeInHierarchy, Is.True, "Stage " + stage);
                    Assert.That(session.State.stageIndex, Is.EqualTo(stage));
                    Assert.That(screen.viewerImage.isActiveAndEnabled, Is.True);
                    Assert.That(screen.viewerImage.texture, Is.SameAs(session.PreviewTexture).And.Not.Null);
                    var viewer = screen.viewerPanel.GetComponent<GameSealedGourdView>();
                    var art = Resources.Load<GameSealedGourdArt>("Hapio/UI/SealedGourdArt");
                    Assert.That(viewer.background.isActiveAndEnabled, Is.True);
                    Assert.That(viewer.background.texture, Is.SameAs(art.stageBackgrounds[stage - 1]),
                        "The card must open its own stage's viewer background.");
                    Assert.That(screen.resetViewerButton.gameObject.activeInHierarchy, Is.True);
                    Assert.That(screen.closeViewerButton.gameObject.activeInHierarchy, Is.True);
                    var close = screen.closeViewerButton;
                    Assert.That(((RectTransform)screen.resetViewerButton.transform).anchoredPosition.y, Is.EqualTo(-698f));
                    Assert.That(((RectTransform)close.transform).anchoredPosition.y, Is.EqualTo(-755f));
                    var surface = close.GetComponentInChildren<GameFigmaSurface>();
                    Assert.That(surface, Is.Not.Null, "The return button shows the Figma brush surface.");
                    Assert.That(surface.SurfaceName, Is.EqualTo("ResultAction"));
                    Assert.That(surface.raycastTarget, Is.False);
                    Assert.That(close.targetGraphic.isActiveAndEnabled && close.targetGraphic.raycastTarget, Is.True);
                    var caption = close.GetComponentInChildren<UnityEngine.UI.Text>();
                    Assert.That(caption.text, Is.EqualTo("결과로 돌아가기"));
                    Assert.That(caption.color, Is.EqualTo(GameLobbySeatColors.LightText));
                    var hint = screen.viewerPanel.GetComponentsInChildren<UnityEngine.UI.Text>()
                        .Single(text => text.name == "Rotate Hint");
                    Assert.That(hint.color, Is.EqualTo(GameBattleUiColors.SecondaryLightIvory800));
                    var preview = (LabGourdResultPreview)typeof(GameSession).GetField("preview",
                        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                    Quaternion initialRotation = preview.ModelRotation;
                    float initialZoom = preview.ZoomNormalized;
                    string initialPaint = preview.PaintPixelHash;
                    string initialResult = JsonUtility.ToJson(session.ResultSnapshot);
                    session.RotatePreview(new Vector2(.2f, .15f));
                    session.ZoomPreview(.3f);
                    yield return null;
                    Assert.That(Quaternion.Angle(initialRotation, preview.ModelRotation), Is.GreaterThan(1f));
                    Assert.That(preview.ZoomNormalized, Is.GreaterThan(initialZoom));
                    yield return ClickVisibleButtonWithMouse(mouse, screen.resetViewerButton);
                    Assert.That(session.IsViewerOpen && screen.viewerPanel.activeInHierarchy, Is.True,
                        "Reset keeps the current stage's gourd viewer open.");
                    Assert.That(Quaternion.Angle(initialRotation, preview.ModelRotation), Is.LessThan(.001f));
                    Assert.That(preview.ZoomNormalized, Is.EqualTo(initialZoom).Within(.001f));
                    Assert.That(preview.PaintPixelHash, Is.EqualTo(initialPaint));
                    Assert.That(JsonUtility.ToJson(session.ResultSnapshot), Is.EqualTo(initialResult));
                    yield return ClickVisibleButtonWithMouse(mouse, close);
                    Assert.That(session.IsViewerOpen || screen.viewerPanel.activeInHierarchy, Is.False, "Stage " + stage);
                    Assert.That(story.presentationGroup.alpha, Is.EqualTo(1f));
                    Assert.That(story.player.CurrentView, Is.SameAs(sealCard));
                    Assert.That(flip.IsBack, Is.True, "Returning retains the seal card's record face.");
                    Assert.That(story.BoundResult, Is.SameAs(session.ResultSnapshot));
                    if (stage == 1)
                    {
                        yield return ClickVisibleButtonWithMouse(mouse, inspect);
                        returnBackgroundEntry = GameFigmaStrokeLibrary.Find("ResultAction");
                        returnBackgroundSprite = returnBackgroundEntry.sprite;
                        returnBackgroundEntry.sprite = null;
                        yield return null;
                        AssertViewerReturnBackground(screen, true);
                        yield return ClickVisibleButtonWithMouse(mouse, close);
                        Assert.That(session.IsViewerOpen, Is.False, "The fallback keeps the real return click working.");
                        session.OpenGourdViewer();
                        yield return Until(() => session.IsViewerOpen && screen.viewerPanel.activeInHierarchy,
                            "The saved result can reopen inspection while the source is still missing.");
                        yield return null;
                        AssertViewerReturnBackground(screen, true);
                        yield return ClickVisibleButtonWithMouse(mouse, close);
                        RestoreReturnBackgroundSource();
                        yield return null;
                        yield return ClickVisibleButtonWithMouse(mouse, inspect);
                        AssertViewerReturnBackground(screen, false);
                        yield return ClickVisibleButtonWithMouse(mouse, close);
                        Assert.That(story.player.CurrentView, Is.SameAs(sealCard));
                        Assert.That(flip.IsBack, Is.True);
                        Assert.That(JsonUtility.ToJson(session.ResultSnapshot), Is.EqualTo(initialResult));
                    }
                    if (stage < 3)
                    {
                        yield return FinishResultStory();
                        var next = story.player.CurrentView.GetComponent<StoryStageClearControls>().nextSeal;
                        yield return Until(() => next.gameObject.activeInHierarchy && next.IsInteractable(),
                            "Closing inspection allows the Host to start the next stage.");
                        yield return ClickVisibleButtonWithMouse(mouse, next);
                        yield return Until(() => story.ReadyView.start.IsInteractable(), "The prepared host gets its separate start action.");
                        yield return ClickVisibleButtonWithMouse(mouse, story.ReadyView.start);
                    }
                }
                Assert.That(EventSystem.current.currentInputModule,
                    Is.InstanceOf<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(),
                    "Exercise the saved Input System UI module, not direct UnityEvent callbacks.");
                Assert.That(story.player.sequence, Is.SameAs(story.afterStages[2]));
                Assert.That(story.skipButton.gameObject.activeSelf, Is.False, "The seal card has no SKIP.");
                StoryCutView card = story.player.CurrentView;
                yield return AdvancePast(story.player, () => story.player.CurrentView == card);
                yield return Until(() => story.skipButton.gameObject.activeInHierarchy && story.skipButton.IsInteractable()
                    && story.player.IsReading && !story.player.IsTransitioning && !story.player.UserInputBlocked,
                    "SKIP appears on the first after-story picture.");
                StoryCutView skippedView = story.player.CurrentView;
                yield return ClickVisibleButtonWithMouse(mouse, story.skipButton);
                yield return Until(() => story.IsLocalStoryComplete,
                    "A real pointer click on the top SKIP must finish Jigwi's after-story.");
                StoryStageClearControls clear = story.player.CurrentView.GetComponent<StoryStageClearControls>();
                Assert.That(clear, Is.Not.Null, "SKIP lands on Jigwi's last authored clear picture.");
                Assert.That(story.player.CurrentView, Is.Not.SameAs(skippedView));
                Assert.That(story.player.CurrentView.GetComponentInChildren<StoryCardFlip>(true), Is.Null,
                    "SKIP does not replay a card.");
                Assert.That(story.player.nextButton.gameObject.activeInHierarchy, Is.False);
                Assert.That(story.advanceButton.gameObject.activeInHierarchy, Is.False);
                yield return Until(() => clear.nextSeal.gameObject.activeInHierarchy && clear.nextSeal.IsInteractable(),
                    "The clear picture exposes 다음 봉인 after the story is released.");
                yield return ClickVisibleButtonWithMouse(mouse, clear.nextSeal);
                Assert.That(story.ReadyView.IsWaiting, Is.True);
                yield return Until(() => story.ReadyView.start.IsInteractable(), "The host confirms readiness before starting the ending.");
                yield return ClickVisibleButtonWithMouse(mouse, story.ReadyView.start);
                yield return Until(() => story.player.sequence == story.ending && story.player.IsPlaying,
                    "A real click on 다음 봉인 must begin the summary and ending.");
                Assert.That(story.ending.cuts, Has.Count.EqualTo(7));
                Assert.That(session.StageResults, Has.Count.EqualTo(3));
                AssertSealSummaryFromActualStageRecords(story, true);
                Assert.That(story.skipButton.gameObject.activeSelf, Is.False, "A summary is not skipped as if it were Ending_01.");
                yield return Until(() => story.player.CurrentView.continueButton != null
                    && story.player.CurrentView.continueButton.gameObject.activeInHierarchy
                    && story.player.CurrentView.continueButton.IsInteractable() && story.player.IsReading,
                    "Wait for the actual summary Continue, not a hidden authoring control.");
                StoryCutView summaryView = story.player.CurrentView;
                yield return ClickVisibleButtonWithMouse(mouse, summaryView.continueButton);
                yield return Until(() => story.player.CurrentCutIndex == 1 && !story.player.IsTransitioning && story.player.IsReading,
                    "A real Continue click crosses the cloud wipe and reveals Ending_01.");
                Assert.That(story.player.sequence.cuts[1].cutId, Is.EqualTo("Ending_01"));
                Assert.That(story.player.CurrentView, Is.Not.SameAs(summaryView));
                yield return Until(() => story.skipButton.gameObject.activeInHierarchy && story.skipButton.IsInteractable(),
                    "The actual ending reuses the reachable top SKIP control.");
                yield return ClickVisibleButtonWithMouse(mouse, story.skipButton);
                yield return WaitHome();
                Assert.That(network.IsConnected, Is.False);
                Assert.That(Object.FindFirstObjectByType<GameLobbyView>(), Is.Not.Null);
            }
            finally
            {
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
                InputSystem.settings = original;
                original.hideFlags = originalFlags;
                if (fixture != null) Object.Destroy(fixture);
            }
        }

        private void AssertSealSummaryFromActualStageRecords(GameStageStoryPresentation story, bool allStagesCompleted)
        {
            var summary = story.player.CurrentView.GetComponent<SealRunSummary>();
            Assert.That(summary, Is.Not.Null, "The actual saved StageResult binds the authored summary.");
            Assert.That(Object.FindObjectsByType<SealRunSummaryPreview>(FindObjectsInactive.Include, FindObjectsSortMode.None),
                Is.Empty, "The real result path must never get its numbers from the authoring preview fixture.");
            Assert.That(story.player.CurrentCutIndex, Is.Zero);
            Assert.That(story.player.sequence.cuts[0].cutId, Is.EqualTo("SealRunSummary"));
            var records = session.SealStageResults;
            Assert.That(records.Count, Is.EqualTo(3), "The summary retains stage-number slots even when some records are missing.");
            Assert.That(summary.stageTimes, Has.Length.EqualTo(3));
            Assert.That(summary.contributions, Has.Length.EqualTo(3));
            double total = 0d;
            for (int stage = 0; stage < records.Count; stage++)
            {
                LabResultStage record = records[stage];
                if (record == null)
                {
                    Assert.That(summary.stageTimes[stage].text, Is.EqualTo("—"));
                    Assert.That(summary.contributions[stage].text, Is.EqualTo("—"));
                    continue;
                }
                Assert.That(record.PlayedSeconds, Is.GreaterThanOrEqualTo(0d));
                total += record.PlayedSeconds;
                Assert.That(summary.stageTimes[stage].text, Is.EqualTo(SummaryClock(record.PlayedSeconds)));
                LabPlayerState actualPlayer = record.Players.Single(player => player.id == session.ResultLocalPlayerId);
                Assert.That(actualPlayer.paintedCellsTotal, Is.GreaterThan(0L), "CPU fixture completion adds real authoritative surface cells.");
                Assert.That(actualPlayer.damageTotal, Is.Zero, "No projectile hit was manufactured to give this fixture attack points.");
                Assert.That(record.Players, Has.Length.EqualTo(1));
                Assert.That(summary.contributions[stage].text, Is.EqualTo("100%"),
                    "This solo painter owns all new cells even with zero attack score; contribution is painted area, not damage.");
            }
            Assert.That(records.All(record => record != null), Is.EqualTo(allStagesCompleted));
            Assert.That(summary.totalTime.text, Is.EqualTo(allStagesCompleted ? SummaryClock(total) : "—"),
                "Only all three actual successful battle times form a completed total.");
        }

        private static string SummaryClock(double seconds)
        {
            long whole = (long)Math.Floor(seconds);
            return (whole / 60).ToString("00", System.Globalization.CultureInfo.InvariantCulture) + ":"
                + (whole % 60).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static IEnumerator ClickVisibleButtonWithMouse(Mouse mouse, UnityEngine.UI.Button button)
        {
            Assert.That(button.gameObject.activeInHierarchy && button.IsInteractable(), Is.True);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Canvas canvas = button.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var rect = (RectTransform)button.transform;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(eventCamera, rect.TransformPoint(rect.rect.center));
            Assert.That(point.x, Is.InRange(0f, (float)Screen.width), button.name + " must be inside the actual Game view.");
            Assert.That(point.y, Is.InRange(0f, (float)Screen.height), button.name + " must be inside the actual Game view.");
            var pointer = new PointerEventData(EventSystem.current) { position = point };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, button.name + " must have a visible graphic raycast target.");
            string hitNames = string.Join(" > ", hits.Take(5).Select(hit => hit.gameObject.name));
            Assert.That(hits[0].module, Is.InstanceOf<UnityEngine.UI.GraphicRaycaster>(), hitNames);
            Assert.That(hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>(), Is.SameAs(button),
                "The actual first hit must belong to " + button.name + ", not an invisible cover: " + hitNames);

            // Feed device state through normal Dynamic Update and EventSystem frames. Do not invoke handlers,
            // UnityEvents, StoryPlayer.Skip, or manually process input before the UI module can consume its edges.
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator ResultEntryRequiresEveryHeldMouseAndTouchToReleaseBeforeFreshButtonInput()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            InputSettings original = InputSystem.settings;
            HideFlags originalFlags = original.hideFlags;
            // InputSystem destroys replaced HideAndDontSave settings; preserve this exact original instead.
            original.hideFlags = HideFlags.DontUnloadUnusedAsset;
            InputSettings fixture = ScriptableObject.CreateInstance<InputSettings>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(original), fixture);
            fixture.hideFlags = HideFlags.HideAndDontSave;
            fixture.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            fixture.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = fixture;
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Touchscreen touchscreen = InputSystem.AddDevice<Touchscreen>();
            try
            {
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                InputSystem.QueueStateEvent(touchscreen, new TouchState
                    { touchId = 91, phase = UnityEngine.InputSystem.TouchPhase.Began, position = Vector2.one * 20f });
                InputSystem.QueueStateEvent(touchscreen, new TouchState
                    { touchId = 92, phase = UnityEngine.InputSystem.TouchPhase.Began, position = Vector2.one * 40f });
                InputSystem.Update();
                Assert.That(mouse.leftButton.isPressed, Is.True);
                Assert.That(touchscreen.touches.Count(touch => touch.press.isPressed), Is.EqualTo(2));
                CompleteMapInFixture(Model());
                yield return Until(() => !session.IsLoadingScene && session.State.phase == GameFlowPhase.StageResult
                    && ResultStory() != null && ResultStory().BoundResult != null, "Victory must load while the old pointers remain held.");
                GameStageStoryPresentation view = ResultStory();
                Assert.That(view.advanceButton.interactable || view.exitButton.interactable || !view.player.UserInputBlocked, Is.False);
                // These bypass Button.interactable deliberately: the action handlers must also reject inherited input.
                view.Advance();
                view.RequestExit();
                view.InspectGourd();
                view.Skip();
                Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.StageResult));
                Assert.That(session.State.stageIndex, Is.EqualTo(1));
                Assert.That(session.IsViewerOpen, Is.False);

                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.Update();
                yield return null;
                yield return null;
                Assert.That(view.advanceButton.interactable || view.exitButton.interactable || !view.player.UserInputBlocked, Is.False,
                    "Releasing the mouse cannot unlock a result while any finger remains held.");
                InputSystem.QueueStateEvent(touchscreen, new TouchState
                    { touchId = 91, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = Vector2.one * 20f });
                InputSystem.Update();
                yield return null;
                Assert.That(view.advanceButton.interactable || view.exitButton.interactable || !view.player.UserInputBlocked, Is.False,
                    "The final held finger still shields all result actions.");
                InputSystem.QueueStateEvent(touchscreen, new TouchState
                    { touchId = 92, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = Vector2.one * 40f });
                InputSystem.Update();
                yield return Until(() => !view.player.UserInputBlocked,
                    "All released pointers restore story input without a timed countdown.");
                Assert.That(view.exitButton.gameObject.activeInHierarchy, Is.False,
                    "Story reading does not add the finished-result action group early.");
                Assert.That(session.CanAdvanceStage, Is.False, "Releasing input still requires story completion.");
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                InputSystem.Update();
                view.player.Skip(); // Finish sequencing while another physical pointer remains held.
                yield return null; yield return null;
                Assert.That(view.IsLocalStoryComplete, Is.False, "Story completion waits for the final held gesture release.");
                Assert.That(view.advanceButton.interactable, Is.False);
                var heldClear = view.player.CurrentView != null ? view.player.CurrentView.GetComponent<StoryStageClearControls>() : null;
                Assert.That(heldClear == null || !heldClear.nextSeal.interactable, Is.True);
                view.Advance();
                Assert.That(session.State.stageIndex, Is.EqualTo(1), "The inherited release cannot become a stage action.");
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.Update();
                yield return FinishResultStory();
                StoryStageClearControls clear = view.player.CurrentView.GetComponent<StoryStageClearControls>();
                Assert.That(clear, Is.Not.Null, "The completed story waits on its authored clear picture.");
                yield return Until(() => clear.nextSeal.gameObject.activeInHierarchy && clear.nextSeal.interactable,
                    "Only the completed story opens NEXT.");
                Assert.That(session.State.stageIndex, Is.EqualTo(1), "Releasing inherited input cannot itself advance.");
                CompleteButtonPointer(clear.nextSeal);
                Assert.That(session.State.stageIndex, Is.EqualTo(1), "Readiness does not start gameplay.");
                yield return Until(() => view.ReadyView.start.IsInteractable(), "Wait for the separate Host start gesture.");
                CompleteButtonPointer(view.ReadyView.start);
                Assert.That(session.State.stageIndex, Is.EqualTo(2), "A fresh completed pointer gesture starts the next stage.");
                yield return WaitPlaying(2);

                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                InputSystem.Update();
                Assert.That(Model().TryApplyPlayerDamage(network.LocalPlayerId, network.config.PlayerMaxHp, ModelTime(Model())), Is.True);
                yield return WaitGameOver();
                GameStageStoryPresentation defeat = ResultStory();
                Assert.That(defeat, Is.Not.Null);
                Assert.That(defeat.exitButton.interactable, Is.False, "Defeat shields the inherited battle pointer.");
                defeat.RequestExit();
                defeat.Skip();
                Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.GameOver));
                yield return null;
                Assert.That(defeat.player.sequence, Is.SameAs(defeat.failure));
                Assert.That(defeat.BoundResult.phase, Is.EqualTo(LabPhase.Defeat));
                Assert.That(defeat.BoundResult.sessionId, Is.EqualTo(session.State.battleSessionId),
                    "Stage-one victory must not leak into the stage-two defeat.");
                Assert.That(session.StageResults, Has.Count.EqualTo(1));
                Assert.That(session.CurrentStageDefinition.displayName, Is.EqualTo("야광귀"));
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.Update();
                yield return Until(() => !defeat.player.UserInputBlocked, "Released defeat input restores story input.");
                yield return FinishResultStory();
                var gauge = defeat.player.CurrentView.GetComponentInChildren<StorySealGauge>(true);
                Assert.That(gauge.target, Is.EqualTo(defeat.BoundResult.paintCoverage));
                AssertFailureUsesOnlyAuthoredActions(defeat);
                CompleteButtonPointer(FailureActionButton(defeat, "Exit"));
                yield return WaitHome();
            }
            finally
            {
                InputSystem.RemoveDevice(touchscreen);
                InputSystem.RemoveDevice(mouse);
                InputSystem.settings = original;
                original.hideFlags = originalFlags;
                if (fixture != null) Object.Destroy(fixture);
            }
        }

        private static void CompleteButtonPointer(UnityEngine.UI.Button button)
        {
            var canvas = button.GetComponentInParent<Canvas>();
            var rectangle = (RectTransform)button.transform;
            Vector2 position = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rectangle.TransformPoint(rectangle.rect.center));
            var pointer = new PointerEventData(EventSystem.current)
                { pointerId = -1, button = PointerEventData.InputButton.Left, position = position, pressPosition = position };
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        // StageTwoDefeatShowsYaguang… now lives in ResultEntryRequiresEveryHeldMouseAndTouch… and
        // StageThreeVictoryShowsFinalVictory… in FigmaResultsWaitForHostNext…: each repeated that test's exact path.
        [UnityTest]
        public IEnumerator StageThreeDefeatShowsJigwiAndOnlyTheCurrentBattleRecords()
        {
            session.StartSolo();
            for (int stage = 1; stage < 3; stage++)
            {
                yield return WaitPlaying(stage);
                CompleteMapInFixture(Model());
                yield return WaitResultStory();
                yield return FinishResultStory();
                Assert.That(session.ConfirmSealReady(), Is.True, "The local participant explicitly confirms the next seal.");
                yield return Until(() => session.CanAdvanceStage, "The preceding story and result must finish.");
                session.NextStage();
            }
            yield return WaitPlaying(3);
            LabModel model = Model();
            Assert.That(model.TryApplyPlayerDamage(network.LocalPlayerId, network.config.PlayerMaxHp,
                ModelTime(model)), Is.True);
            yield return WaitGameOver();
            yield return WaitResultStory();
            GameStageStoryPresentation story = ResultStory();
            Assert.That(session.CurrentStageDefinition.displayName, Is.EqualTo("지귀"));
            LabSnapshot snapshot = session.ResultSnapshot;
            Assert.That(snapshot.sessionId, Is.EqualTo(session.State.battleSessionId));
            Assert.That(snapshot.round, Is.EqualTo(session.State.battleRound));
            Assert.That(snapshot.phase, Is.EqualTo(LabPhase.Defeat));
            Assert.That(story.BoundResult, Is.SameAs(snapshot));
            Assert.That(session.StageResults, Has.Count.EqualTo(2), "A failed attempt does not replace earlier victories.");
            Assert.That(story.screen.resultView.gameObject.activeSelf, Is.False);
            yield return FinishResultStory();
            var gauge = story.player.CurrentView.GetComponentInChildren<StorySealGauge>(true);
            Assert.That(gauge.target, Is.EqualTo(snapshot.paintCoverage), "Failure uses Jigwi's actual coverage, never authored 78%.");
            AssertFailureUsesOnlyAuthoredActions(story);
            var exit = FailureActionButton(story, "Exit");
            yield return Until(() => exit.interactable, "The authored result permits explicit exit.");
            exit.onClick.Invoke();
            yield return WaitHome();
        }

        [UnityTest]
        public IEnumerator DefeatDisconnectRestoresTerminalGameOverInsteadOfLeavingAHiddenResult()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            LabModel model = Model();
            Assert.That(model.TryApplyPlayerDamage(network.LocalPlayerId, network.config.PlayerMaxHp, ModelTime(model)), Is.True);
            yield return WaitGameOver();
            yield return WaitResultStory();
            GameStageStoryPresentation story = ResultStory();
            Assert.That(story.IsPresenting, Is.True);
            Assert.That(story.screen.resultView.gameObject.activeSelf, Is.False);
            // Invoke the actual game-flow disconnect callback: this fixture has only one transport peer.
            typeof(GameSession).GetMethod("OnPeerDisconnected", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { 72UL, false });
            // #603: the saved GameOver Scene's terminal screen is the 불이/금옥이 leave screen, not the old result card.
            GameOverCharacterView leave = story.screen.gameOverView;
            Assert.That(leave, Is.Not.Null, "The saved GameOver Scene wires the #603 leave screen.");
            yield return Until(() => !story.IsPresenting && leave.gameObject.activeInHierarchy,
                "A disconnect during the failure story must restore the terminal screen immediately.");
            Assert.That(session.State.retryableDefeat, Is.False);
            Assert.That(session.CanRetryStage, Is.False);
            Assert.That(story.presentationRoot.activeSelf, Is.False);
            Assert.That(story.screen.resultView.gameObject.activeSelf, Is.False, "No hidden result card under it.");
            Assert.That(leave.bul.activeSelf != leave.geumok.activeSelf, Is.True, "Exactly one character.");
            Assert.That(leave.dialogue.text, Is.EqualTo(leave.geumok.activeSelf ? "지금, 누가 나갔지?" : "얼른 다시 돌아와!"));
            yield return Until(() => leave.homeButton.interactable, "The terminal screen must retain a visible exit.");
            leave.homeButton.onClick.Invoke();
            yield return WaitHome();
        }

        [UnityTest]
        public IEnumerator APreparationFailureGameOverAlsoUsesTheCharacterScreen()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            // A real non-leave abort path (the same one a failed battle start takes); nobody left the room.
            typeof(GameSession).GetMethod("AbortHost", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { "The prepared battle could not be started.", "판을 멈추고 로비로 돌아왔소." });
            yield return WaitGameOver();
            GameScreen screen = null;
            yield return Until(() => (screen = Object.FindFirstObjectByType<GameScreen>()) != null
                && screen.gameOverView != null && screen.gameOverView.gameObject.activeInHierarchy,
                "Preparation, loading and sync failures show the 불이/금옥이 screen like a leave.");
            Assert.That(session.State.retryableDefeat, Is.False);
            Assert.That(screen.resultView.gameObject.activeSelf, Is.False, "No result card under it.");
            GameOverCharacterView character = screen.gameOverView;
            Assert.That(character.bul.activeSelf != character.geumok.activeSelf, Is.True, "Exactly one character.");
            yield return Until(() => character.homeButton.interactable, "The character screen keeps an exit.");
            character.homeButton.onClick.Invoke();
            yield return WaitHome();
        }

        private static void AssertVisibleRows(GameStageResultView view, System.Collections.Generic.IReadOnlyList<LabResultRow> expected)
        {
            Transform[] visible = view.rows.Cast<Transform>().Where(row => row.gameObject.activeSelf).ToArray();
            Assert.That(visible.Length, Is.EqualTo(expected.Count));
            for (int index = 0; index < expected.Count; index++)
            {
                LabResultRow row = expected[index];
                string title = row.Title == "북 독점" ? "북 독점왕" : row.Title;
                Assert.That(visible[index].Find("Title").GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo(title));
                Assert.That(visible[index].Find("Record").GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo(row.Record));
                Assert.That(visible[index].Find("Highlight").gameObject.activeSelf, Is.EqualTo(row.Mine));
            }
        }

        [UnityTest]
        public IEnumerator AuthoritativeTimeoutLoadsGameOverInsteadOfNextStage()
        {
            float[] values = network.config.CaptureDeveloperValues();
            Set(values, "Battle Duration (seconds)", 1f);
            Assert.That(session.ApplyDeveloperSettings(values, out string error), Is.True, error);
            session.StartSolo();
            yield return WaitPlaying(1);
            // Let the actual Network clock reach the one-second configured deadline. Never
            // advance the model ahead of the Network clock or inject a terminal snapshot.
            yield return WaitGameOver();
            Assert.That(session.State.endReason, Is.EqualTo("Time ran out."));
            Assert.That(session.State.stageIndex, Is.EqualTo(1));
            yield return null;
            yield return WaitResultStory();
            GameStageStoryPresentation resultView = ResultStory();
            Assert.That(resultView.BoundResult, Is.SameAs(session.ResultSnapshot));
            Assert.That(resultView.BoundResult.phase, Is.EqualTo(LabPhase.Defeat));
            Assert.That(resultView.player.sequence, Is.SameAs(resultView.failure));
            Assert.That(resultView.player.CurrentCutIndex, Is.Zero);
            Assert.That(resultView.skipButton.gameObject.activeSelf, Is.False, "The first failure picture has no SKIP.");
            // The failure story is one picture and then the dashboard, whose own buttons replace SKIP.
            yield return AdvancePast(resultView.player, () => resultView.player.CurrentCutIndex == 0);
            Assert.That(resultView.skipButton.gameObject.activeSelf, Is.False);
            session.NextStage();
            yield return null;
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.GameOver));
            yield return FinishResultStory();
            var gauge = resultView.player.CurrentView.GetComponentInChildren<StorySealGauge>(true);
            Assert.That(gauge.target, Is.EqualTo(network.Snapshot.paintCoverage));
            AssertFailureUsesOnlyAuthoredActions(resultView);
            FailureActionButton(resultView, "Exit").onClick.Invoke();
            yield return WaitHome();
        }

        [UnityTest]
        public IEnumerator AuthoritativeAllPlayersDownUsesGameOverRatherThanStageResult()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            LabModel model = Model();
            Assert.That(model.TryApplyPlayerDamage(network.LocalPlayerId, network.config.PlayerMaxHp,
                ModelTime(model)), Is.True);
            Assert.That(model.Phase, Is.EqualTo(LabPhase.Defeat));
            yield return WaitGameOver();
            Assert.That(session.State.endReason, Is.EqualTo("All players are down."));
            yield return WaitResultStory();
            Assert.That(session.ResultSnapshot.phase, Is.EqualTo(LabPhase.Defeat));
            Assert.That(session.StageResults, Is.Empty);
            session.GoHome();
            yield return WaitHome();
        }

        [UnityTest]
        public IEnumerator PausedDeveloperApplyReloadsOnlyCurrentStageAndRestoresFreshResources()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            network.Generate();
            yield return Until(() => network.Snapshot.players.Single().generatedTotal == 1, "Generate must commit.");
            LabOrbBoard oldBoard = network.board;
            string runId = session.State.runId;
            string battleId = session.State.battleSessionId;
            session.Pause();
            Assert.That(network.IsBattlePaused, Is.True);
            double pausedRemaining = network.Snapshot.remaining;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(network.Snapshot.remaining, Is.EqualTo(pausedRemaining).Within(.001d),
                "A loaded game Scene must retain the shared pause clock.");
            session.Resume();
            Assert.That(network.IsBattlePaused, Is.False);
            yield return Until(() => network.Snapshot.remaining < pausedRemaining - .01d,
                "Resume must restart the existing stage clock without loading a new battle.");
            Assert.That(session.State.battleSessionId, Is.EqualTo(battleId));
            session.Pause();
            Assert.That(network.IsBattlePaused, Is.True);
            float[] values = network.config.CaptureDeveloperValues();
            Set(values, "Player Max HP", 219f);
            Set(values, "Starting Stamina", 73f);
            Set(values, "Gimmick Charm (0 off, 1 on)", 0f);
            int probeFlushes = session.FrameProbe != null ? session.FrameProbe.FlushCount : 0;
            Assert.That(session.ApplyDeveloperSettings(values, out string error), Is.True, error);
            yield return Until(() => session.State.phase == GameFlowPhase.Playing
                && session.State.battleSessionId != battleId && !session.IsLoadingScene,
                "Applying DEV settings must pass through a fresh Battle load barrier.");
            if (session.FrameProbe != null)
                Assert.That(session.FrameProbe.FlushCount, Is.EqualTo(probeFlushes + 1),
                    "#481: the reload of the same stage is reported as its own attempt.");
            Assert.That(session.State.stageIndex, Is.EqualTo(1));
            Assert.That(session.State.runId, Is.EqualTo(runId));
            Assert.That(oldBoard == null, Is.True);
            Assert.That(network.IsBattlePaused, Is.False);
            AssertFreshStage();
            Assert.That(network.Snapshot.players.Single().personalHp, Is.EqualTo(219));
            Assert.That(network.Snapshot.players.Single().stamina, Is.EqualTo(73f).Within(.01f));
            Assert.That(network.config.GimmickSwitches()[(int)LabSabotageKind.Charm], Is.False,
                "Restart must retain the user's OFF rather than reapply the stage's initial ON default.");
        }

        [UnityTest]
        public IEnumerator PausedBattleClayPreviewPersistsOnlyItsTwoCommonValuesIntoTheNextStage()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            LabConfig source = Resources.Load<LabConfig>("LabConfig");
            string saved = JsonUtility.ToJson(source);
            float[][] before = Enumerable.Range(0, 4).Select(session.CaptureDeveloperSettings).ToArray();
            LabConfig acceptedConfig = network.config;
            float[] accepted = acceptedConfig.CaptureDeveloperValues();
            session.Pause();
            yield return null;
            Assert.That(network.IsBattlePaused, Is.True);
            string oldBattle = session.State.battleSessionId;
            uint flowRevision = session.State.revision;
            int settingsRevision = network.SessionConfigRevision;
            string fingerprint = network.SessionConfigFingerprint;

            // Battle's LabDeveloperMode calls this API directly. Do not use the Home/
            // Lobby GameSession wrapper: the event bridge must persist these values.
            Assert.That(network.TryPreviewClayAppearance(.018f, .83f), Is.True);
            Assert.That(network.ClayThickness, Is.EqualTo(.018f));
            Assert.That(network.ClayGloss, Is.EqualTo(.83f));
            Assert.That(network.config, Is.SameAs(acceptedConfig));
            Assert.That(network.config.CaptureDeveloperValues(), Is.EqualTo(accepted),
                "Cosmetic preview must not replace or alter the accepted battle config.");
            Assert.That(network.SessionConfigRevision, Is.EqualTo(settingsRevision));
            Assert.That(network.SessionConfigFingerprint, Is.EqualTo(fingerprint));
            Assert.That(session.State.revision, Is.EqualTo(flowRevision));
            Assert.That(session.State.battleSessionId, Is.EqualTo(oldBattle));
            for (int scope = 0; scope < 4; scope++)
            {
                float[] remembered = session.CaptureDeveloperSettings(scope);
                Assert.That(remembered[LabConfig.ClayThicknessField], Is.EqualTo(.018f), "Scope " + scope);
                Assert.That(remembered[LabConfig.ClayGlossField], Is.EqualTo(.83f), "Scope " + scope);
                Assert.That(remembered.Take(LabConfig.ClayThicknessField), Is.EqualTo(before[scope].Take(LabConfig.ClayThicknessField)),
                    "The battle event must not apply an unrelated DEV draft in scope " + scope);
            }
            Assert.That(session.DeveloperNextStage(), Is.True);
            yield return WaitPlaying(2);
            Assert.That(session.State.battleSessionId, Is.Not.EqualTo(oldBattle));
            Assert.That(network.config, Is.Not.SameAs(acceptedConfig));
            Assert.That(network.config.ClayThickness, Is.EqualTo(.018f));
            Assert.That(network.config.ClayGloss, Is.EqualTo(.83f));
            Assert.That(network.ClayThickness, Is.EqualTo(.018f));
            Assert.That(network.ClayGloss, Is.EqualTo(.83f));
            Assert.That(network.PaintSurface.CoveredCount, Is.Zero);
            Assert.That(JsonUtility.ToJson(source), Is.EqualTo(saved));
        }

        [UnityTest]
        public IEnumerator PausedDeveloperNextStageUsesSavedButtonAndFreshBattlesWithoutApplyingUnsavedFields()
        {
            Assert.That(session.CanDeveloperAdvanceStage, Is.False);
            Assert.That(session.DeveloperNextStage(), Is.False, "Home has no stage to skip.");
            GameSession persistent = session;
            LabNetwork persistentNetwork = network;
            session.StartSolo();
            yield return WaitPlaying(1);
            string runId = session.State.runId;
            ulong playerId = network.LocalPlayerId;
            int slot = network.LocalSlotNumber;
            ulong[] participants = network.ConnectedPlayerIds.ToArray();
            var loadedPaths = new System.Collections.Generic.List<string>();
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> recordScene = (scene, _) => loadedPaths.Add(scene.path);
            SceneManager.sceneLoaded += recordScene;
            try
            {
                for (int stage = 1; stage <= 3; stage++)
                {
                    Assert.That(GameSession.Instance, Is.SameAs(persistent));
                    Assert.That(session.Network, Is.SameAs(persistentNetwork));
                    Assert.That(session.State.runId, Is.EqualTo(runId));
                    Assert.That(network.LocalPlayerId, Is.EqualTo(playerId));
                    Assert.That(network.LocalSlotNumber, Is.EqualTo(slot));
                    Assert.That(network.ConnectedPlayerIds, Is.EqualTo(participants));
                    Assert.That(Model().StageYokai, Is.EqualTo((LabStageYokai)stage));
                    AssertFreshStage();
                    Assert.That(network.IsBattlePaused, Is.False);
                    Assert.That(session.CanDeveloperAdvanceStage, Is.False, "DEV skip requires an explicitly paused battle.");
                    Assert.That(session.DeveloperNextStage(), Is.False);
                    Assert.That(session.State.stageIndex, Is.EqualTo(stage));
                    var scenery = Object.FindFirstObjectByType<GameStageBackgrounds>();
                    Assert.That(scenery, Is.Not.Null);
                    Assert.That(scenery.AppliedStage, Is.EqualTo(stage));
                    // #447: stages 2 and 3 each show their own scenery; stage 1 shows the default.
                    Assert.That(scenery.defaultBackground.studio.gameObject.activeInHierarchy, Is.EqualTo(stage == 1));
                    Assert.That(scenery.Resolve(2).studio.gameObject.activeInHierarchy, Is.EqualTo(stage == 2));
                    Assert.That(scenery.Resolve(3).studio.gameObject.activeInHierarchy, Is.EqualTo(stage == 3));

                    network.Generate();
                    yield return Until(() => network.Snapshot.players.Single().generatedTotal == 1,
                        "Leave real generated resources in the old Battle to verify next-stage cleanup.");
                    LabModel model = Model();
                    Assert.That(model.TryApplyPlayerDamage(playerId, 13, ModelTime(model)), Is.True);
                    LabOrbBoard oldBoard = network.board;
                    string oldBattle = session.State.battleSessionId;
                    session.Pause();
                    Assert.That(network.IsBattlePaused, Is.True);
                    yield return null;
                    GameBattleBindings bindings = SceneManager.GetActiveScene().GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<GameBattleBindings>(true)).Single();
                    LabDeveloperMode dev = bindings.developerMode;
                    Assert.That(bindings.developerNextStageButton, Is.Not.Null, "Use the saved editable DEV control.");
                    Assert.That(bindings.developerNextStageButton.transform.IsChildOf(dev.panel.transform), Is.True);
                    dev.Open();
                    yield return null;
                    Assert.That(dev.panel.activeInHierarchy, Is.True);
                    Assert.That(bindings.developerNextStageButton.gameObject.activeInHierarchy, Is.True);
                    Assert.That(bindings.developerNextStageButton.interactable, Is.EqualTo(stage < 3));
                    // Jigwi's 120-second timeline (228) is on in every stage's profile, so battle DEV never offers the old
                    // fire or its cycle, even expanded, and in Jigwi's own stage not the common gimmick interval either.
                    Assert.That(network.config.JigwiStageTimelineEnabled, Is.True);
                    Transform devContent = dev.valueInputs[2].transform.parent.parent;
                    var jigwiHeader = devContent.Find("Category Jigwi").GetComponent<UnityEngine.UI.Button>();
                    var gimmickHeader = devContent.Find("Category Gimmicks").GetComponent<UnityEngine.UI.Button>();
                    Assert.That(jigwiHeader.gameObject.activeSelf, Is.False, "Stage " + stage + ": no header for the old fire");
                    jigwiHeader.onClick.Invoke(); gimmickHeader.onClick.Invoke();
                    foreach (int index in new[] { 178, 179, 180, 181, 182, 183, 192, 193, 194, 195 })
                        Assert.That(dev.valueInputs[index].transform.parent.gameObject.activeSelf, Is.False, "Stage " + stage + ": " + index);
                    Assert.That(dev.valueInputs[205].transform.parent.gameObject.activeSelf, Is.EqualTo(stage != 3),
                        "Stage " + stage + ": the common gimmick interval");
                    Assert.That(dev.valueInputs[207].transform.parent.gameObject.activeSelf, Is.True, "Stage " + stage);
                    jigwiHeader.onClick.Invoke(); gimmickHeader.onClick.Invoke();
                    float[][] acceptedProfiles = Enumerable.Range(0, 4)
                        .Select(scope => session.CaptureDeveloperSettings(scope)).ToArray();
                    int hpField = Enumerable.Range(0, LabConfig.DeveloperFields.Count)
                        .Single(i => LabConfig.DeveloperFields[i].Label == "Player Max HP");
                    dev.valueInputs[hpField].text = "299"; // Deliberately do not APPLY or restart.
                    Assert.That(network.config.PlayerMaxHp, Is.EqualTo(237));
                    Assert.That(session.CanDeveloperAdvanceStage, Is.EqualTo(stage < 3));
                    uint revision = session.State.revision;
                    if (stage == 3)
                    {
                        Assert.That(session.DeveloperNextStage(), Is.False, "The last stage cannot create a stage 4 or fake completion.");
                        yield return null;
                        Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Playing));
                        Assert.That(session.State.stageIndex, Is.EqualTo(3));
                        Assert.That(session.State.revision, Is.EqualTo(revision));
                        Assert.That(session.State.battleSessionId, Is.EqualTo(oldBattle));
                        Assert.That(network.IsBattlePaused, Is.True);
                        Assert.That(session.ResultSnapshot, Is.Null);
                        Assert.That(session.PreviewTexture, Is.Null);
                        continue;
                    }

                    bindings.developerNextStageButton.onClick.Invoke();
                    Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.LoadingBattle));
                    Assert.That(session.State.stageIndex, Is.EqualTo(stage + 1));
                    Assert.That(session.IsLoadingScene, Is.True);
                    Assert.That(session.CanDeveloperAdvanceStage, Is.False);
                    uint loadingRevision = session.State.revision;
                    Assert.That(loadingRevision, Is.GreaterThan(revision));
                    Assert.That(session.DeveloperNextStage(), Is.False, "Duplicate input cannot skip the new loading generation.");
                    Assert.That(session.State.revision, Is.EqualTo(loadingRevision));
                    yield return WaitPlaying(stage + 1);
                    Assert.That(session.State.battleSessionId, Is.Not.EqualTo(oldBattle));
                    Assert.That(oldBoard == null, Is.True, "The previous Battle instance must be unloaded.");
                    AssertDeveloperSettings(acceptedProfiles[stage + 1]);
                    for (int scope = 0; scope <= 3; scope++)
                        Assert.That(session.CaptureDeveloperSettings(scope), Is.EqualTo(acceptedProfiles[scope]),
                            "Skipping must retain approved profiles and discard unapplied form text.");
                    Assert.That(loadedPaths, Does.Not.Contain(GameScenePaths.StageResult),
                        "Skipping must not produce a completed paint preview or a victory result.");
                }
            }
            finally { SceneManager.sceneLoaded -= recordScene; }
        }

        [UnityTest]
        public IEnumerator DeveloperNextStageCannotOvertakeAQueuedRestartOrLeaveGameOver()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            string battle = session.State.battleSessionId;
            session.Pause();
            float[] values = network.config.CaptureDeveloperValues();
            Set(values, "Player Max HP", 219f);
            Assert.That(session.ApplyDeveloperSettings(values, out string error), Is.True, error);
            Assert.That(session.CanDeveloperAdvanceStage, Is.False,
                "An accepted current-stage restart already owns the transition.");
            Assert.That(session.DeveloperNextStage(), Is.False);
            Assert.That(session.State.stageIndex, Is.EqualTo(1));
            yield return Until(() => session.State.phase == GameFlowPhase.Playing
                && !session.IsLoadingScene && session.State.battleSessionId != battle,
                "The already accepted DEV restart must finish on stage 1.");
            Assert.That(network.Snapshot.players.Single().personalHp, Is.EqualTo(219));
            session.ExitRun();
            yield return WaitGameOver();
            uint revision = session.State.revision;
            Assert.That(session.CanDeveloperAdvanceStage, Is.False);
            Assert.That(session.DeveloperNextStage(), Is.False);
            yield return null;
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.GameOver));
            Assert.That(session.State.stageIndex, Is.EqualTo(1));
            Assert.That(session.State.revision, Is.EqualTo(revision));
            Assert.That(session.ResultSnapshot, Is.Null);
        }

        [UnityTest]
        public IEnumerator ForeignStageDeveloperEditIsRejectedBeforeTheCurrentBattleEnds()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            session.Pause();
            string battle = session.State.battleSessionId;
            float[] beforeCommon = session.CaptureDeveloperSettings(0);
            float[] beforeStage = session.CaptureDeveloperSettings(1);
            LabConfig candidate = Object.Instantiate(network.config);
            try
            {
                float[] forged = candidate.CaptureDeveloperValues();
                forged[184] = forged[184] > .5f ? 0f : 1f; // Yaguang-only setting from the Gumiho Battle DEV form.
                Assert.That(candidate.TryApplyDeveloperValues(forged, out string error), Is.True, error);
                Assert.That(network.RestartGameSoloWithConfig(candidate), Is.False);
                Assert.That(network.Snapshot, Is.Not.Null, "Reject before EndBattleToLobby clears the active model.");
                Assert.That(network.IsBattlePaused, Is.True);
                Assert.That(session.State.battleSessionId, Is.EqualTo(battle));
                Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Playing));
                Assert.That(session.CaptureDeveloperSettings(0), Is.EqualTo(beforeCommon));
                Assert.That(session.CaptureDeveloperSettings(1), Is.EqualTo(beforeStage));
                yield return null;
                Assert.That(session.State.battleSessionId, Is.EqualTo(battle));
            }
            finally { Object.Destroy(candidate); }
        }

        [UnityTest]
        public IEnumerator StageControlNamesTheCatalogIdentityAndExactSettingsGeneration()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo make = typeof(GameSession).GetMethod("MakeControl", flags);
            MethodInfo valid = typeof(GameSession).GetMethod("ValidStateControl", flags);
            object control = make.Invoke(session, new object[] { "state", session.State });
            Assert.That(valid.Invoke(session, new[] { control }), Is.EqualTo(true));
            Type type = control.GetType();
            FieldInfo id = type.GetField("stageId");
            string expectedId = (string)id.GetValue(control);
            id.SetValue(control, "a-different-stage");
            Assert.That(valid.Invoke(session, new[] { control }), Is.EqualTo(false));
            id.SetValue(control, expectedId);
            type.GetField("yokai").SetValue(control, LabStageYokai.Jigwi);
            Assert.That(valid.Invoke(session, new[] { control }), Is.EqualTo(false));

            control = make.Invoke(session, new object[] { "loaded", null });
            MethodInfo matches = typeof(GameSession).GetMethod("MatchesControlIdentity", flags);
            Assert.That(matches.Invoke(session, new[] { control, (object)false }), Is.EqualTo(true));
            control.GetType().GetField("settingsFingerprint").SetValue(control, "previous-stage-profile");
            Assert.That(matches.Invoke(session, new[] { control, (object)false }), Is.EqualTo(false),
                "A Scene-ready message cannot acknowledge a different effective stage configuration.");
        }

        [UnityTest]
        public IEnumerator SabotageSettingsSurviveRestartAndNextStageWhileFoxAndCharmStateReset()
        {
            GameSession persistent = session;
            LabNetwork persistentNetwork = network;
            float[] foxSettings = network.config.CaptureDeveloperValues();
            Set(foxSettings, "Drop Combine (0 off, 1 on)", 1f);
            Set(foxSettings, "Common Gimmick Interval (seconds, 0 = none)", 0f);
            Set(foxSettings, "Unique Gimmick Interval (seconds, 0 = none)", 2f);
            Set(foxSettings, "Gimmick Chance Fox Fire (%)", 100f);
            Set(foxSettings, "Gimmick Chance Charm (%)", 0f);
            Set(foxSettings, "Fox Fire Count Min", 2f);
            Set(foxSettings, "Fox Fire Count Max", 2f);
            // Preserve a second fox for restart cleanup after the first contact (#664 team round).
            foxSettings[LabConfig.FoxRoundSpareMinField] = foxSettings[LabConfig.FoxRoundSpareMaxField] = 0f;
            Set(foxSettings, "Fox Fire Safe Seconds", .25f);
            Set(foxSettings, "Charm Seconds", 9f);
            Set(foxSettings, "Charm Rescue Taps per Neighbor (0 = none)", 5f);
            Assert.That(session.ApplyDeveloperSettings(foxSettings, out string error), Is.True, error);
            session.StartSolo();
            yield return WaitPlaying(1);
            AssertDeveloperSettings(foxSettings);

            LabOrbBoard foxBoard = network.board;
            yield return Until(() => network.Snapshot.orbs.Any(orb => orb.kind == LabOrbKind.FoxFire),
                "The persisted Home settings must cast real Fox Fire in the saved Battle.");
            LabOrbState[] foxes = network.Snapshot.orbs.Where(orb => orb.kind == LabOrbKind.FoxFire).ToArray();
            Assert.That(foxes.Length, Is.EqualTo(2), "The new shared Fox Fire count must reach the live model.");
            yield return Until(() => foxes.All(fox => foxBoard.IsFoxFire(fox.id)),
                "The saved board must receive the real Fox Fire drop.");
            LabOrbView foxView = foxBoard.GetComponentsInChildren<LabOrbView>()
                .Single(view => view.OrbId == foxes[0].id);
            Assert.That(foxView.FoxGlowVisible, Is.True);
            Assert.That(foxView.GetComponentInChildren<LabOrbSpriteArt>(), Is.Not.Null);
            yield return Until(() => !foxBoard.IsDropping(foxes[0].id), "The fox must land before it can consume Yin.");
            yield return new WaitForSeconds(network.config.FoxFireSafeSeconds + .1f);

            // The drum draws Yin or Yang by the session's seed (#324 caps a run of one kind at three), so generate
            // until a Yin comes; any Yang only rests on the board, and the first Yin is the one that meets the fox.
            for (int tries = 0; tries < 5 && !network.Snapshot.orbs.Any(orb => orb.kind == LabOrbKind.Yin); tries++)
            {
                int generated = network.Snapshot.players.Single().generatedTotal;
                network.Generate();
                yield return Until(() => network.Snapshot.players.Single().generatedTotal > generated,
                    "The actual Generate path must create an orb.");
            }
            Assert.That(network.Snapshot.orbs.Any(orb => orb.kind == LabOrbKind.Yin), Is.True,
                "The actual Generate path must create a Yin for Fox Fire contact.");
            string yinId = network.Snapshot.orbs.First(orb => orb.kind == LabOrbKind.Yin).id;
            yield return Until(() => foxBoard.GetComponentsInChildren<LabOrbView>().Any(view => view.OrbId == yinId),
                "The generated Yin must reach the saved board.");
            LabOrbView yinView = foxBoard.GetComponentsInChildren<LabOrbView>().Single(view => view.OrbId == yinId);
            // Controlled pointer positions exercise the actual board -> network -> Host request path.
            // This is a drop-overlap fixture, not a claim about uncontrolled collision accuracy.
            double gestureAt = Time.timeAsDouble;
            Vector2 foxPoint = new Vector2(foxView.Body.position.x, foxView.Body.position.y);
            Assert.That(foxBoard.TryBeginDragAtWorld(new Vector2(yinView.Body.position.x, yinView.Body.position.y),
                gestureAt), Is.True);
            Assert.That(foxBoard.DragToWorld(foxPoint, gestureAt + .1d), Is.True);
            Assert.That(foxBoard.EndDragAtWorld(foxPoint, gestureAt + .5d), Is.True);
            yield return Until(() => network.Snapshot.orbs.All(orb => orb.id != foxes[0].id && orb.id != yinId),
                "Fox contact must consume both real model orbs; hiding local previews is insufficient.");
            Assert.That(network.Snapshot.players.Single().passedTotal, Is.Zero);
            Assert.That(foxBoard.IsFoxFire(foxes[1].id), Is.True,
                "An unconsumed Fox remains to exercise cleanup during the same-stage reload.");

            string runId = session.State.runId;
            string firstBattleId = session.State.battleSessionId;
            session.Pause();
            Assert.That(network.IsBattlePaused, Is.True);
            float[] charmSettings = network.config.CaptureDeveloperValues();
            Set(charmSettings, "Unique Gimmick Interval (seconds, 0 = none)", 3f);
            Set(charmSettings, "Gimmick Chance Fox Fire (%)", 0f);
            Set(charmSettings, "Gimmick Chance Charm (%)", 100f);
            Set(charmSettings, "Fox Fire Count Min", 3f);
            Set(charmSettings, "Fox Fire Count Max", 4f);
            Set(charmSettings, "Fox Fire Safe Seconds", .75f);
            Set(charmSettings, "Charm Seconds", 11f);
            Set(charmSettings, "Charm Rescue Taps per Neighbor (0 = none)", 6f);
            Assert.That(session.ApplyDeveloperSettings(charmSettings, out error), Is.True, error);
            yield return Until(() => session.State.phase == GameFlowPhase.Playing
                && session.State.battleSessionId != firstBattleId && !session.IsLoadingScene,
                "The new gimmick settings must restart the current stage through a fresh saved Battle.");
            Assert.That(GameSession.Instance, Is.SameAs(persistent));
            Assert.That(session.Network, Is.SameAs(persistentNetwork));
            Assert.That(session.State.runId, Is.EqualTo(runId));
            Assert.That(session.State.stageIndex, Is.EqualTo(1));
            Assert.That(foxBoard == null, Is.True);
            Assert.That(foxView == null && yinView == null, Is.True);
            AssertDeveloperSettings(charmSettings);
            AssertFreshStage();
            AssertFreshSabotagePresentation();

            LabOrbBoard charmBoard = network.board;
            LabCharmHud charmHud = Object.FindAnyObjectByType<LabCharmHud>();
            LabGourdAlert charmAlert = Object.FindAnyObjectByType<LabGourdAlert>();
            yield return Until(() => charmBoard.Charmed && charmHud.GaugeVisible && charmAlert.ShowingHeart,
                "The retained settings must cast real Charm and reach all saved Battle presentations.");
            Assert.That(network.Snapshot.CharmOn(network.LocalPlayerId), Is.True);
            Assert.That(network.Snapshot.charmUntil - network.Snapshot.charmStartedAt, Is.EqualTo(11d).Within(.001d));
            Assert.That(charmBoard.CharmEdge.Visible, Is.True);
            Assert.That(charmHud.GaugeFill, Is.InRange(.01f, 1f));
            string charmBattleId = session.State.battleSessionId;
            CompleteMapInFixture(Model());
            yield return WaitResultStory();
            yield return FinishResultStory();
            Assert.That(session.ConfirmSealReady(), Is.True, "The local participant explicitly confirms the next seal.");
                yield return Until(() => session.CanAdvanceStage,
                "A real committed victory must reach the next-stage barrier while Charm is active.");
            Assert.That(charmBoard == null && charmHud == null && charmAlert == null, Is.True);
            session.NextStage();
            yield return WaitPlaying(2);
            Assert.That(GameSession.Instance, Is.SameAs(persistent));
            Assert.That(session.Network, Is.SameAs(persistentNetwork));
            Assert.That(session.State.runId, Is.EqualTo(runId));
            Assert.That(session.State.battleSessionId, Is.Not.EqualTo(charmBattleId));
            AssertDeveloperSettings(session.CaptureDeveloperSettings(2));
            Assert.That(Model().StageYokai, Is.EqualTo(LabStageYokai.Yaguang));
            Assert.That(session.CaptureDeveloperSettings(1), Is.EqualTo(charmSettings),
                "The next yokai uses its own profile without losing the completed stage's edits.");
            AssertFreshStage();
            AssertFreshSabotagePresentation();
        }

        private void AssertDeveloperSettings(float[] expected)
        {
            float[] actual = network.config.CaptureDeveloperValues();
            Assert.That(actual.Length, Is.EqualTo(265), "The persistent game session must use the current shared schema.");
            Assert.That(actual, Is.EqualTo(expected),
                "All approved values, including the gimmick group fields (#311), must survive Scene-owned DEV cleanup.");
        }

        private void AssertFreshSabotagePresentation()
        {
            LabSnapshot snapshot = network.Snapshot;
            Assert.That(snapshot.charmStartedAt, Is.LessThan(0d));
            Assert.That(snapshot.charmUntil, Is.LessThanOrEqualTo(snapshot.serverTime));
            Assert.That(snapshot.charmTaps, Is.Zero);
            Assert.That(snapshot.CharmOn(network.LocalPlayerId), Is.False);
            Assert.That(network.board.Charmed, Is.False);
            Assert.That(network.board.CharmEdge == null || !network.board.CharmEdge.Visible, Is.True);
            Assert.That(network.board.GetComponentsInChildren<LabOrbView>()
                .Any(view => view.Kind == LabOrbKind.FoxFire), Is.False);
            LabCharmHud hud = Object.FindAnyObjectByType<LabCharmHud>();
            LabCharmBubbles bubbles = Object.FindAnyObjectByType<LabCharmBubbles>();
            LabGourdAlert alert = Object.FindAnyObjectByType<LabGourdAlert>();
            Assert.That(hud, Is.Not.Null, "The saved Battle must bind its own Charm HUD to the persistent network.");
            Assert.That(bubbles, Is.Not.Null);
            Assert.That(alert, Is.Not.Null);
            Assert.That(hud.GaugeVisible || hud.AwakeShowing, Is.False);
            Assert.That(bubbles.LeftShowing || bubbles.RightShowing, Is.False);
            Assert.That(alert.Active, Is.False);
        }

        private void AssertFreshStage()
        {
            LabSnapshot snapshot = network.Snapshot;
            LabPlayerState player = snapshot.players.Single();
            Assert.That(snapshot.phase, Is.EqualTo(LabPhase.Playing));
            Assert.That(snapshot.orbs, Is.Empty);
            Assert.That(snapshot.drums.Length, Is.EqualTo(network.config.DrumsFor(1)));
            Assert.That(snapshot.drums.All(drum => drum.owner == network.LocalPlayerId), Is.True);
            Assert.That(player.personalHp, Is.EqualTo(network.config.PlayerMaxHp));
            Assert.That(player.stamina, Is.EqualTo(network.config.StaminaStart).Within(.01f));
            double duration = Model().StageYokai == LabStageYokai.Jigwi
                && network.config.JigwiStageTimelineEnabled ? 120d : network.config.BattleDurationSeconds;
            Assert.That(snapshot.remaining, Is.GreaterThan(duration - 10d));
            Assert.That(snapshot.remaining, Is.LessThanOrEqualTo(duration),
                "Fresh Jigwi timeline uses120 seconds; other and legacy policies preserve the common battle time (#328/#332).");
            Assert.That(snapshot.paintCoveredCount, Is.Zero);
            Assert.That(snapshot.paintCount, Is.Zero);
            Assert.That(network.PaintSurface.CoveredCount, Is.Zero);
            Assert.That(player.generatedTotal, Is.Zero);
            Assert.That(player.combinedTotal, Is.Zero);
            Assert.That(player.passedTotal, Is.Zero);
            Assert.That(player.drumPassedTotal, Is.Zero);
            Assert.That(player.hitTotal, Is.Zero);
            Assert.That(player.damageTotal, Is.Zero);
            Assert.That(player.ItemsUsed, Is.Zero);
            Assert.That(player.lastHit, Is.False);
            Assert.That(session.ResultSnapshot, Is.Null);
            Assert.That(session.PreviewTexture, Is.Null);
        }

        private static GameStageStoryPresentation ResultStory() => Object.FindFirstObjectByType<GameStageStoryPresentation>();

        private IEnumerator WaitResultStory() => Until(() => !session.IsLoadingScene && ResultStory() != null
            && ResultStory().IsPresenting && ResultStory().BoundResult != null && ResultStory().player.CurrentView != null,
            "The saved result story must bind the matching actual record before interaction.");

        // Picture changes wait for their cloud wipe, so keep asking for the next page until the picture really changes.
        private static IEnumerator AdvancePast(StoryPlayer player, Func<bool> stillOn)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 15d;
            while (player.IsPlaying && stillOn() && Time.realtimeSinceStartupAsDouble < deadline)
            { player.Next(); yield return null; }
            Assert.That(stillOn() && player.IsPlaying, Is.False, "The story moved on to the next picture.");
            yield return null;
        }

        private IEnumerator FinishResultStory()
        {
            yield return WaitResultStory();
            GameStageStoryPresentation story = ResultStory();
            yield return Until(() => story.IsLocalStoryComplete || !story.player.UserInputBlocked,
                "Inherited result inputs must release before story completion.");
            if (!story.IsLocalStoryComplete) story.player.Skip();
            yield return Until(() => story.IsLocalStoryComplete, "Completing the local story opens only its result controls.");
            yield return null;
        }

        private static void AssertNoInlineContinue(StoryCutView view)
        {
            Assert.That(view, Is.Not.Null);
            if (view.continueButton != null && !view.requireContinueButton)
                Assert.That(view.continueButton.gameObject.activeInHierarchy, Is.False,
                    "An authored ‘next yokai’ picture action must not compete with the actual stage transition.");
        }

        private static void AssertSkippedCardSettled(GameStageStoryPresentation story)
        {
            StoryCutView actual = story.player.CurrentView;
            StoryCutView authored = story.player.sequence.cuts[story.player.CurrentCutIndex].viewPrefab;
            int checkedBeats = 0;
            for (int index = 0; actual.beats != null && index < actual.beats.Length; index++)
            {
                StoryVisualBeat beat = actual.beats[index];
                if (beat == null || beat.motion != StoryVisualBeat.Motion.PaperDrop) continue;
                checkedBeats++;
                Assert.That(beat.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1), "Early Skip leaves a fully shown card.");
                Assert.That(beat.GetComponent<CanvasGroup>().blocksRaycasts, Is.True,
                    "The retained card must also be clickable when its interaction delay exceeds the entrance duration.");
                Assert.That(Vector2.Distance(((RectTransform)beat.transform).anchoredPosition,
                    ((RectTransform)authored.beats[index].transform).anchoredPosition), Is.LessThan(.001f));
            }
            Assert.That(checkedBeats, Is.GreaterThan(0), "The authored card fixture includes its paper-drop entrance.");
        }

        private static UnityEngine.UI.Button FailureActionButton(GameStageStoryPresentation story, string method)
        {
            var actions = story.player.CurrentView.GetComponent<StoryResultActions>();
            Assert.That(actions, Is.Not.Null, "The authored failure dashboard owns its actions.");
            return actions.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(button =>
                Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Any(i =>
                    button.onClick.GetPersistentTarget(i) == actions && button.onClick.GetPersistentMethodName(i) == method));
        }

        private static void AssertFailureUsesOnlyAuthoredActions(GameStageStoryPresentation story)
        {
            Assert.That(story.advanceButton.gameObject.activeInHierarchy, Is.False, "No duplicate footer retry.");
            Assert.That(story.exitButton.gameObject.activeInHierarchy, Is.False, "No duplicate footer exit.");
            Assert.That(story.viewerButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(story.overallButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(story.player.nextButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(FailureActionButton(story, "Retry").gameObject.activeInHierarchy, Is.True);
            Assert.That(FailureActionButton(story, "Exit").gameObject.activeInHierarchy, Is.True);
        }

        private static void AssertCurrentCard(GameStageStoryPresentation story, LabSnapshot snapshot)
        {
            Assert.That(story.BoundResult, Is.SameAs(snapshot));
            StoryCardRecords card = story.player.CurrentView.GetComponentInChildren<StoryCardRecords>(true);
            Assert.That(card, Is.Not.Null, "The current stage's actual seal card must be visible.");
            Assert.That(card.sealRate.text, Is.EqualTo(Mathf.RoundToInt(snapshot.paintCoverage * 100) + "%"));
            Assert.That(card.timeRemaining.text, Is.EqualTo(Mathf.CeilToInt((float)snapshot.remaining) + "초"));
            StoryCardRecords.Record[] records = GameStageStoryPresentation.CardRecords(snapshot, GameSession.Instance.Network.LocalPlayerId);
            var cardImages = story.player.CurrentView.GetComponentsInChildren<UnityEngine.UI.Image>(true);
            Assert.That(cardImages.Count(image => image.name == StoryRecordHighlight.HighlightName && image.gameObject.activeSelf),
                Is.EqualTo(records.Count(record => record.mine)), "Red marks exactly the awards this player earned.");
            Assert.That(cardImages.Any(image => image.name.StartsWith("Rectangle 12") && image.gameObject.activeSelf), Is.False,
                "The design's fixed sample strips are off on the real result path.");
            Assert.That(card.players, Has.Length.EqualTo(5));
            for (int row = 0; row < 5; row++)
            {
                Assert.That(card.players[row].text, Is.EqualTo(string.IsNullOrEmpty(records[row].player) ? "—" : records[row].player));
                Assert.That(card.values[row].text, Is.EqualTo(string.IsNullOrEmpty(records[row].value) ? "—" : records[row].value));
            }
        }

        private static void CompleteMapInFixture(LabModel model)
        {
            Assert.That(model.PaintSurface.RegisterHit(Vector3.forward, model.PaintColorFor(LabOrbElement.Fire),
                model.CopyPlayers().Single().id, 1), Is.True);
            Assert.That(model.PaintSurface.IsComplete, Is.True,
                "LEGACY 100% fixture coverage must actually paint every cell, not forge a victory snapshot.");
        }

        private LabModel Model() => (LabModel)typeof(LabNetwork)
            .GetField("model", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(network);
        private static double ModelTime(LabModel model) => (double)typeof(LabModel)
            .GetField("lastTime", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(model);

        private IEnumerator WaitPlaying(int stage) => Until(() => session.State.phase == GameFlowPhase.Playing
            && session.State.stageIndex == stage && !session.IsLoadingScene && network.HasBattleReferences
            && SceneManager.GetActiveScene().path == GameScenePaths.Battle, "Battle stage " + stage + " must start.");
        private IEnumerator WaitGameOver() => Until(() => session.State.phase == GameFlowPhase.GameOver
            && !session.IsLoadingScene && SceneManager.GetActiveScene().path == GameScenePaths.GameOver,
            "GameOver must load and remain available for explicit Home navigation.");
        private IEnumerator WaitHome() => Until(() => session.State.phase == GameFlowPhase.Home
            && !session.IsLoadingScene && SceneManager.GetActiveScene().path == GameScenePaths.Home,
            "Home navigation must finish loading.");

        private static IEnumerator Until(Func<bool> predicate, string message)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 20d;
            while (!predicate() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(predicate(), Is.True, message);
        }

        private static void Set(float[] values, string label, float value)
        {
            int index = Enumerable.Range(0, LabConfig.DeveloperFields.Count)
                .Single(i => LabConfig.DeveloperFields[i].Label == label);
            values[index] = value;
        }

        private static ushort AvailableDualFamilyPort()
        {
            for (int attempt = 0; attempt < 16; attempt++)
                using (var ipv4 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    ipv4.Bind(new IPEndPoint(IPAddress.Any, 0));
                    int port = ((IPEndPoint)ipv4.LocalEndPoint).Port;
                    try
                    {
                        if (Socket.OSSupportsIPv6)
                            using (var ipv6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp))
                            {
                                ipv6.DualMode = false;
                                ipv6.Bind(new IPEndPoint(IPAddress.IPv6Any, port));
                            }
                        return checked((ushort)port);
                    }
                    catch (SocketException error) when (error.SocketErrorCode == SocketError.AddressAlreadyInUse) { }
                }
            throw new InvalidOperationException("The test could not reserve a dual-family UDP Host port.");
        }
    }
}
