using NUnit.Framework;

namespace Hapio.Tests
{
    /// <summary>
    /// Shortens the #306 shared battle start for every fixture of this assembly (namespace Hapio.Tests) and restores
    /// the production defaults after the run. Only the lengths shrink: each start still waits for the initial snapshot
    /// and paint, reserves a shared time, collects the per-generation receipts, commits, shows 3/2/1 and only then
    /// releases Playing, with the same reschedule rule and the same preparation budget.
    /// A test about the visible countdown itself switches back with <see cref="UseDefaults"/>; its fixture's teardown
    /// calls <see cref="UseShort"/> again, because a failed nested yield skips the test's own finally.
    /// </summary>
    [SetUpFixture]
    public sealed class GameTestStartTiming
    {
        // 0.3 s still spans many frames (about 18 at 60 fps, 9 in a slow 30 fps Editor), so every start passes
        // through real Countdown frames with the overlay blocking input and the game held; it is not a skipped phase.
        internal const double CountdownSeconds = .3d;
        // Every Hapio PlayMode start is solo: the Host reserves, marks its own receipt and commits in the same Update,
        // at the same NGO server time, so 0.1 s of lead never forces a reschedule (the frame probe asserts 0).
        internal const double InitialLeadSeconds = .1d;
        // "시작!" never blocks input; 0.1 s is still several frames before the overlay hides.
        internal const double StartedLabelSeconds = .1d;
        // The 5 s cap stays: it is reached only after repeated missing receipts, and keeping it means a slow receipt
        // in a test still converges exactly as on a device instead of being rescheduled forever under a smaller cap.
        internal const double MaximumLeadSeconds = GameStartTiming.DefaultMaximumLeadSeconds;

        [OneTimeSetUp]
        public void ShortenStartGate() => UseShort();

        [OneTimeTearDown]
        public void RestoreStartGate() => UseDefaults();

        internal static void UseShort() =>
            GameStartTiming.Configure(CountdownSeconds, InitialLeadSeconds, MaximumLeadSeconds, StartedLabelSeconds);

        internal static void UseDefaults() => GameStartTiming.Reset();
    }
}
