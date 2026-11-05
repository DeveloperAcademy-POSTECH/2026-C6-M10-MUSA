using System;

namespace C6Lab
{
    /// <summary>Presentation-only state, scoped to one authoritative config and round.</summary>
    public sealed class LabClayPresentationState
    {
        public string SessionId { get; private set; }
        public uint Round { get; private set; }
        public int ConfigRevision { get; private set; }
        public string ConfigFingerprint { get; private set; }
        public long Sequence { get; private set; }
        public float Thickness { get; private set; }
        public float Gloss { get; private set; }

        public bool Matches(string session, uint round, int revision, string fingerprint) => Sequence > 0
            && SessionId == session && Round == round && ConfigRevision == revision && ConfigFingerprint == fingerprint;

        public bool TryApply(string session, uint round, int revision, string fingerprint, long sequence, float thickness, float gloss)
        {
            if (string.IsNullOrEmpty(session) || round == 0 || revision < 1 || string.IsNullOrEmpty(fingerprint)
                || sequence <= 0 || !LabConfig.ValidClayAppearance(thickness, gloss)) return false;
            if (Matches(session, round, revision, fingerprint) && sequence <= Sequence) return false;
            SessionId = session; Round = round; ConfigRevision = revision; ConfigFingerprint = fingerprint;
            Sequence = sequence; Thickness = thickness; Gloss = gloss;
            return true;
        }
    }

    public sealed partial class LabNetwork
    {
        private readonly LabClayPresentationState clayAppearance = new LabClayPresentationState();
        private long clayAppearanceSequence;
        private bool HasCurrentClayAppearance => latest != null
            && clayAppearance.Matches(latest.sessionId, latest.round, configRevision, configFingerprint);
        public float ClayThickness => HasCurrentClayAppearance ? clayAppearance.Thickness : config != null ? config.ClayThickness : .006f;
        public float ClayGloss => HasCurrentClayAppearance ? clayAppearance.Gloss : config != null ? config.ClayGloss : .25f;
        public bool CanPreviewClayAppearance => DeveloperBuildAvailable && IsHost && IsBattlePaused && !GameStartHeld;
        public event Action<float, float> ClayAppearanceChanged;

        /// <summary>
        /// Cosmetic paused-room tuning only. Does not replace LabConfig or mutate its saved
        /// source, restart a battle, change a gameplay setting/fingerprint, or advance a clock.
        /// </summary>
        public bool TryPreviewClayAppearance(float thickness, float gloss)
        {
            if (!CanPreviewClayAppearance || latest == null || !LabConfig.ValidClayAppearance(thickness, gloss)
                || clayAppearanceSequence == long.MaxValue) return false;
            long sequence = ++clayAppearanceSequence;
            if (!clayAppearance.TryApply(latest.sessionId, latest.round, configRevision, configFingerprint,
                sequence, thickness, gloss)) return false;
            foreach (ulong id in order)
                if (id != LocalPlayerId)
                    Send(id, new LabNetworkPacket { type = "clay_appearance", sessionId = latest.sessionId,
                        round = latest.round, configRevision = configRevision, configFingerprint = configFingerprint,
                        requestId = sequence, x = thickness, y = gloss });
            ClayAppearanceChanged?.Invoke(thickness, gloss);
            return true;
        }

        // Called only in the existing server-only client packet switch.
        private void ReceiveClayAppearance(LabNetworkPacket packet)
        {
            if (!clientConfigReady || latest == null || packet.sessionId != latest.sessionId || packet.round != latest.round
                || packet.configRevision != configRevision || packet.configFingerprint != configFingerprint) return;
            if (!clayAppearance.TryApply(packet.sessionId, packet.round, packet.configRevision, packet.configFingerprint,
                packet.requestId, packet.x, packet.y)) return;
            ClayAppearanceChanged?.Invoke(packet.x, packet.y);
        }
    }
}
