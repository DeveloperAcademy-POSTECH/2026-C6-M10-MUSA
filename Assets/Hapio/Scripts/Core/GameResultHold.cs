namespace Hapio
{
    /// <summary>
    /// Time limits that keep the shared result screens from waiting forever (all in real seconds, measured by the
    /// device that enforces them). The Host owns the result-ready limit and the hold notice/override; each device
    /// closes its own idle quit popup and gourd viewer. Tests may shorten them; the game always uses the defaults.
    /// </summary>
    internal static class GameResultHold
    {
        /// <summary>Every member must report its result within this time after the Host reported its own.</summary>
        internal const double DefaultResultReadySeconds = 30d;
        /// <summary>The Host's waiting screen names what still holds the room after this time.</summary>
        internal const double DefaultNoticeSeconds = 45d;
        /// <summary>The Host may press start without a member who is still reading or viewing after this time.</summary>
        internal const double DefaultOverrideSeconds = 120d;
        /// <summary>A quit popup or gourd viewer left open without any touch closes itself after this time.</summary>
        internal const double DefaultIdleCloseSeconds = 60d;

        internal static double ResultReadySeconds { get; private set; } = DefaultResultReadySeconds;
        internal static double NoticeSeconds { get; private set; } = DefaultNoticeSeconds;
        internal static double OverrideSeconds { get; private set; } = DefaultOverrideSeconds;
        internal static double IdleCloseSeconds { get; private set; } = DefaultIdleCloseSeconds;

        internal enum Stage { None, Notice, Override }

        internal static void Configure(double resultReady, double notice, double overrideAfter, double idleClose)
        {
            if (!(resultReady > 0d) || !(notice > 0d) || !(overrideAfter >= notice) || !(idleClose > 0d))
                throw new System.ArgumentOutOfRangeException(nameof(resultReady), "Result hold limits must be positive and ordered.");
            ResultReadySeconds = resultReady; NoticeSeconds = notice; OverrideSeconds = overrideAfter; IdleCloseSeconds = idleClose;
        }

        internal static void Reset() => Configure(DefaultResultReadySeconds, DefaultNoticeSeconds,
            DefaultOverrideSeconds, DefaultIdleCloseSeconds);

        /// <summary>How far a hold that began at <paramref name="since"/> (negative: no hold) has gone at <paramref name="now"/>.</summary>
        internal static Stage HoldStage(double since, double now)
        {
            if (since < 0d || double.IsNaN(now)) return Stage.None;
            double elapsed = now - since;
            if (elapsed >= OverrideSeconds) return Stage.Override;
            return elapsed >= NoticeSeconds ? Stage.Notice : Stage.None;
        }

        internal static bool ResultReadyExpired(double since, double now) => since >= 0d && now - since >= ResultReadySeconds;

        internal static bool IdleExpired(double lastInput, double now) => lastInput >= 0d && now - lastInput >= IdleCloseSeconds;
    }
}
