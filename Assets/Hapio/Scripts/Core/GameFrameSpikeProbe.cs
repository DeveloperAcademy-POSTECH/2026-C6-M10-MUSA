using System;
using System.Globalization;
using System.Text;
using C6Lab;
using UnityEngine;

namespace Hapio
{
    /// <summary>Pure thresholds and formats of the battle frame probe (#481).</summary>
    public static class GameFrameSpikePolicy
    {
        public const float SpikeMs = 25f;
        public const float LongSpikeMs = 50f;
        /// <summary>The opening of Playing that the summary describes: the first tap, throw and hit happen here.</summary>
        public const float SummarySeconds = 10f;
        public const int SpikeCapacity = 256;
        public const int SummaryFrameCapacity = 2048;

        public static bool IsSpike(float milliseconds) => milliseconds > SpikeMs;

        /// <summary>Development players and the Editor only; a release player never attaches the probe.</summary>
        public static bool ProbeEnabled(bool debugBuild) => debugBuild;

        public static bool IsBattle(GameFlowPhase phase) => phase == GameFlowPhase.LoadingBattle
            || phase == GameFlowPhase.SynchronizingBattle || phase == GameFlowPhase.Countdown || phase == GameFlowPhase.Playing;

        /// <summary>The value at the given fraction of sorted samples, 0 when there are none.</summary>
        public static float Percentile(float[] sorted, int count, float fraction)
        {
            if (count <= 0) return 0f;
            int index = Mathf.Clamp(Mathf.CeilToInt(fraction * count) - 1, 0, count - 1);
            return sorted[index];
        }
    }

    /// <summary>
    /// Records long battle frames and what first ran in them (#481). A long frame is only written into preallocated
    /// arrays; everything is logged once, without stack traces, when the battle leaves Playing. Measurement only.
    /// </summary>
    [DefaultExecutionOrder(int.MaxValue)]
    public sealed class GameFrameSpikeProbe : MonoBehaviour
    {
        private struct Spike
        {
            public GameFlowPhase Phase;
            public bool Held;
            public float SincePlaying, Milliseconds;
            public int Uses, FirstUses;
        }

        private readonly Spike[] spikes = new Spike[GameFrameSpikePolicy.SpikeCapacity];
        private readonly float[] opening = new float[GameFrameSpikePolicy.SummaryFrameCapacity];
        private GameSession session;
        private int spikeCount, droppedSpikes, openingCount, openingOver25, openingOver50, seenUses, reschedules;
        private float openingMax;
        private double loadingSeconds, syncSeconds, countdownSeconds, phaseStartedAt, playingStartedAt = double.NaN;
        private bool recording, pendingValid, pendingHeld;
        private GameFlowPhase pendingPhase = GameFlowPhase.Home;
        private int pendingUses;
        private string runId = string.Empty, yokai = string.Empty;
        private int stageIndex;

        public int SpikeCount => spikeCount;
        public int FlushCount { get; private set; }

        public void Bind(GameSession owner) => session = owner;

        public void CountdownRescheduled() { if (recording) reschedules++; }

        private void LateUpdate()
        {
            if (session == null) return;
            GameFlowState state = session.State;
            double now = Time.realtimeSinceStartupAsDouble;
            // This frame's delta is the previous frame's cost: pair it with what that frame recorded.
            if (pendingValid && recording) Record(Time.unscaledDeltaTime * 1000f, now);
            bool battle = state != null && GameFrameSpikePolicy.IsBattle(state.phase);
            if (battle && !recording) Begin(state, now);
            else if (recording && (!battle || state.runId != runId || state.stageIndex != stageIndex)) Flush(now, battle ? state : null);
            if (recording && state.phase != pendingPhase) EnterPhase(state.phase, now);
            pendingValid = recording;
            pendingPhase = state != null ? state.phase : GameFlowPhase.Home;
            pendingHeld = session.Network != null && session.Network.GameStartHeld;
            pendingUses = LabFirstUse.FlagsFor(Time.frameCount);
        }

        private void Begin(GameFlowState state, double now)
        {
            recording = true;
            runId = state.runId ?? string.Empty;
            stageIndex = state.stageIndex;
            yokai = session.Network != null ? session.Network.GameStageYokai.ToString() : string.Empty;
            spikeCount = droppedSpikes = openingCount = openingOver25 = openingOver50 = seenUses = reschedules = 0;
            openingMax = 0f;
            loadingSeconds = syncSeconds = countdownSeconds = 0d;
            phaseStartedAt = now;
            playingStartedAt = state.phase == GameFlowPhase.Playing ? now : double.NaN;
        }

        private void EnterPhase(GameFlowPhase next, double now)
        {
            double spent = now - phaseStartedAt;
            if (pendingPhase == GameFlowPhase.LoadingBattle) loadingSeconds += spent;
            else if (pendingPhase == GameFlowPhase.SynchronizingBattle) syncSeconds += spent;
            else if (pendingPhase == GameFlowPhase.Countdown) countdownSeconds += spent;
            phaseStartedAt = now;
            if (next == GameFlowPhase.Playing && double.IsNaN(playingStartedAt)) playingStartedAt = now;
        }

        private void Record(float milliseconds, double now)
        {
            int firstUses = pendingUses & ~seenUses;
            seenUses |= pendingUses;
            double sincePlaying = double.IsNaN(playingStartedAt) ? -1d : now - playingStartedAt;
            if (pendingPhase == GameFlowPhase.Playing && sincePlaying >= 0d && sincePlaying <= GameFrameSpikePolicy.SummarySeconds)
            {
                if (openingCount < opening.Length) opening[openingCount++] = milliseconds;
                if (milliseconds > GameFrameSpikePolicy.SpikeMs) openingOver25++;
                if (milliseconds > GameFrameSpikePolicy.LongSpikeMs) openingOver50++;
                openingMax = Mathf.Max(openingMax, milliseconds);
            }
            if (!GameFrameSpikePolicy.IsSpike(milliseconds)) return;
            if (spikeCount >= spikes.Length) { droppedSpikes++; return; }
            spikes[spikeCount++] = new Spike { Phase = pendingPhase, Held = pendingHeld, SincePlaying = (float)sincePlaying,
                Milliseconds = milliseconds, Uses = pendingUses, FirstUses = firstUses };
        }

        private void Flush(double now, GameFlowState next)
        {
            EnterPhase(GameFlowPhase.Home, now);
            string stage = "run=" + runId + " stage=" + stageIndex.ToString(CultureInfo.InvariantCulture) + " yokai=" + yokai;
            for (int i = 0; i < spikeCount; i++)
            {
                Spike spike = spikes[i];
                Log("HAPIO_FRAME_SPIKE " + stage + " phase=" + spike.Phase + " held=" + (spike.Held ? "1" : "0")
                    + " t=" + spike.SincePlaying.ToString("0.00", CultureInfo.InvariantCulture)
                    + " ms=" + spike.Milliseconds.ToString("0.0", CultureInfo.InvariantCulture)
                    + " uses=" + LabFirstUse.Describe(spike.Uses) + " first=" + LabFirstUse.Describe(spike.FirstUses));
            }
            var sorted = new float[openingCount];
            Array.Copy(opening, sorted, openingCount);
            Array.Sort(sorted);
            Log(new StringBuilder("HAPIO_FRAME_SUMMARY ").Append(stage)
                .Append(" loading=").Append(loadingSeconds.ToString("0.00", CultureInfo.InvariantCulture))
                .Append(" sync=").Append(syncSeconds.ToString("0.00", CultureInfo.InvariantCulture))
                .Append(" countdown=").Append(countdownSeconds.ToString("0.00", CultureInfo.InvariantCulture))
                .Append(" reschedules=").Append(reschedules)
                .Append(" opening_frames=").Append(openingCount)
                .Append(" over25=").Append(openingOver25).Append(" over50=").Append(openingOver50)
                .Append(" max=").Append(openingMax.ToString("0.0", CultureInfo.InvariantCulture))
                .Append(" p99=").Append(GameFrameSpikePolicy.Percentile(sorted, openingCount, .99f).ToString("0.0", CultureInfo.InvariantCulture))
                .Append(" spikes=").Append(spikeCount).Append(" dropped=").Append(droppedSpikes).ToString());
            FlushCount++;
            recording = false;
            if (next != null) Begin(next, now);
        }

        private static void Log(string line) => Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", line);

        private void OnDestroy()
        {
            if (recording) Flush(Time.realtimeSinceStartupAsDouble, null);
        }
    }
}
