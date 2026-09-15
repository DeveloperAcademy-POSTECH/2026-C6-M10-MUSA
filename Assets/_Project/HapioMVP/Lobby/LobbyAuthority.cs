using System;
using System.Collections.Generic;
using System.Linq;

namespace C6.Prototype.Lobby
{
    /// <summary>Host-only lobby decisions. No gameplay snapshot or combat mutation belongs to T10-A.</summary>
    public sealed class LobbyAuthority
    {
        private const int MaximumRequestsPerParticipant = 4096;
        private sealed class Participant
        {
            internal ulong Id;
            internal bool InitialStateReceived;
            internal bool Ready;
            internal string Nonce;
            internal ulong LastSequence;
            internal ulong AdmissionRevision;
            internal readonly HashSet<string> Requests = new HashSet<string>(StringComparer.Ordinal);
        }
        private readonly Participant host;
        private readonly List<Participant> peers = new List<Participant>();
        private readonly string hostConfigJson;
        private LobbyStartContract start;
        private ulong rosterRevision = 1;
        public string RoomId { get; }
        public string SessionId { get; }
        public string Build { get; }
        public uint Seed { get; }
        public string ConfigFingerprint { get; }
        public ulong Revision { get; private set; } = 1;
        public string Phase { get; private set; } = LobbyProtocol.Lobby;
        public string CloseReason { get; private set; } = string.Empty;
        public uint RoundId => start?.roundId ?? 0;
        public int MaximumParticipants { get; }
        public bool ContinuousTransfersEnabled { get; }
        public int ProtocolVersion => LobbyProtocol.For(MaximumParticipants, ContinuousTransfersEnabled);
        public int ParticipantCount => peers.Count + 1;
        public bool CanStart => Phase == LobbyProtocol.Lobby && peers.Count > 0 && host.Ready && host.InitialStateReceived
            && peers.All(p => p.Ready && p.InitialStateReceived);

        public LobbyAuthority(string roomId, string sessionId, string build, string hostConfigJson, uint seed, int maximumParticipants = LobbyProtocol.Capacity, bool continuousTransfers = false)
        {
            if (!LobbyWire.ValidId(roomId) || !LobbyWire.ValidId(sessionId) || !LobbyWire.ValidBuild(build)
                || !LobbyWire.ValidConfigJson(hostConfigJson)) throw new ArgumentException("Invalid bounded lobby identity or host configuration.");
            if (maximumParticipants != LobbyProtocol.Capacity && maximumParticipants != LobbyProtocol.MaximumCapacity)
                throw new ArgumentOutOfRangeException(nameof(maximumParticipants));
            if (continuousTransfers && maximumParticipants != LobbyProtocol.MaximumCapacity)
                throw new ArgumentException("Continuous transfers require the multiparty contract.");
            MaximumParticipants = maximumParticipants; ContinuousTransfersEnabled = continuousTransfers;
            RoomId = roomId; SessionId = sessionId; Build = build; this.hostConfigJson = hostConfigJson; Seed = seed;
            ConfigFingerprint = LobbyWire.Fingerprint(hostConfigJson);
            host = new Participant { Id = LobbyProtocol.HostClientId, InitialStateReceived = true, Nonce = string.Empty, AdmissionRevision = 1 };
        }

        public bool TryAdmit(ulong clientId, LobbyHello hello, out string reason)
        {
            if (clientId == LobbyProtocol.HostClientId) { reason = "HOST_ID_RESERVED"; return false; }
            reason = LobbyCompatibility.Check(hello, Build, RoomId, Phase, ParticipantCount, MaximumParticipants, ContinuousTransfersEnabled);
            if (reason.Length != 0) return false;
            if (peers.Any(p => p.Id == clientId || (MaximumParticipants > 2 && p.Nonce == hello.clientNonce)))
                { reason = "DUPLICATE_PARTICIPANT"; return false; }
            peers.Add(new Participant { Id = clientId, Nonce = hello.clientNonce, AdmissionRevision = Revision + 1 });
            if (MaximumParticipants > 2) { ResetReady(); rosterRevision = Revision + 1; }
            Revision++;
            return true;
        }

        public string NonceFor(ulong clientId) => clientId == LobbyProtocol.HostClientId ? host.Nonce
            : peers.FirstOrDefault(p => p.Id == clientId)?.Nonce;

        public bool Handle(ulong sender, LobbyRequest request, out string reason)
        {
            if (!LobbyWire.ValidRequest(request)) return Reject("INVALID_REQUEST", out reason);
            if (request.protocol != ProtocolVersion) return Reject("PROTOCOL_MISMATCH", out reason);
            if (request.roomId != RoomId || request.sessionId != SessionId) return Reject("STALE_SESSION", out reason);
            var participant = sender == host.Id ? host : peers.FirstOrDefault(p => p.Id == sender);
            if (participant == null) return Reject("NOT_A_PARTICIPANT", out reason);
            if (participant.Requests.Contains(request.requestId)) return Reject("DUPLICATE_REQUEST", out reason);
            if (request.sequence <= participant.LastSequence) return Reject("STALE_SEQUENCE", out reason);
            if (participant.Requests.Count >= MaximumRequestsPerParticipant) return Reject("REQUEST_LIMIT", out reason);
            // Record every structurally valid request from this bound participant, including rejected intents.
            participant.Requests.Add(request.requestId); participant.LastSequence = request.sequence;
            if (Phase == LobbyProtocol.Closed) return Reject("ROOM_CLOSED", out reason);
            // P3 Ready changes are independent while the seat roster is unchanged. Configuration ACK
            // is independent even of unrelated joins because the room's Config is immutable. Start and
            // Leave still require the exact latest snapshot; legacy scenes keep their original rule.
            bool revisionAccepted = request.revision == Revision;
            if (MaximumParticipants > 2 && request.revision <= Revision)
            {
                if (request.kind == LobbyProtocol.SetReady) revisionAccepted = request.revision >= rosterRevision;
                else if (request.kind == LobbyProtocol.AckInitial) revisionAccepted = request.revision >= participant.AdmissionRevision;
            }
            if (!revisionAccepted) return Reject("STALE_REVISION", out reason);
            if (request.kind == LobbyProtocol.Leave)
            { if (MaximumParticipants > 2 && sender != host.Id && Phase == LobbyProtocol.Lobby) return RemoveParticipant(sender, out reason);
                Close("PARTICIPANT_LEFT"); reason = string.Empty; return true; }
            if (Phase != LobbyProtocol.Lobby) return Reject("BATTLE_IN_PROGRESS", out reason);
            if (request.kind == LobbyProtocol.AckInitial)
            {
                if (request.configFingerprint != ConfigFingerprint) return Reject("CONFIG_MISMATCH", out reason);
                if (participant.InitialStateReceived) return Reject("INITIAL_ALREADY_ACKNOWLEDGED", out reason);
                participant.InitialStateReceived = true;
            }
            else if (request.kind == LobbyProtocol.SetReady)
            {
                if (!participant.InitialStateReceived) return Reject("INITIAL_STATE_REQUIRED", out reason);
                if (request.configFingerprint != ConfigFingerprint) return Reject("CONFIG_MISMATCH", out reason);
                if (participant.Ready == request.ready) return Reject("READY_UNCHANGED", out reason);
                participant.Ready = request.ready;
            }
            else if (request.kind == LobbyProtocol.Start)
            {
                if (sender != LobbyProtocol.HostClientId) return Reject("HOST_ONLY", out reason);
                if (!CanStart) return Reject(MaximumParticipants > 2 ? "ALL_READY_REQUIRED" : "BOTH_READY_REQUIRED", out reason);
                if (request.configFingerprint != ConfigFingerprint) return Reject("CONFIG_MISMATCH", out reason);
                start = new LobbyStartContract { roomId = RoomId, sessionId = SessionId, roundId = 1,
                    seed = Seed, configFingerprint = ConfigFingerprint, hostConfigJson = hostConfigJson, continuousTransfers = ContinuousTransfersEnabled,
                    participantIds = MaximumParticipants > 2 ? new[] { host.Id }.Concat(peers.Select(p => p.Id)).ToArray() : Array.Empty<ulong>() };
                Phase = LobbyProtocol.Playing;
            }
            else return Reject("UNKNOWN_REQUEST", out reason);
            Revision++;
            reason = string.Empty;
            return true;
        }

        public void Close(string reason)
        {
            if (Phase == LobbyProtocol.Closed) return;
            Phase = LobbyProtocol.Closed;
            CloseReason = string.IsNullOrWhiteSpace(reason) ? "ROOM_ENDED" : reason.Substring(0, Math.Min(reason.Length, 128));
            ResetReady();
            Revision++;
        }

        // Only the P3 lobby can shrink. Playing/legacy disconnections keep the original room-end policy.
        public bool RemoveParticipant(ulong clientId, out string reason)
        {
            if (clientId == host.Id) return Reject("HOST_CANNOT_LEAVE_SEAT", out reason);
            var peer = peers.FirstOrDefault(p => p.Id == clientId);
            if (peer == null) return Reject("NOT_A_PARTICIPANT", out reason);
            if (MaximumParticipants <= 2 || Phase != LobbyProtocol.Lobby)
            { Close("PARTICIPANT_LEFT"); reason = string.Empty; return true; }
            peers.Remove(peer); ResetReady(); Revision++; rosterRevision = Revision; reason = string.Empty; return true;
        }
        private void ResetReady() { host.Ready = false; foreach (var peer in peers) peer.Ready = false; }
        public LobbySnapshot Snapshot(string recipientNonce = "")
        {
            var roster = new[] { CopyPlayer(host, 1) }.Concat(peers.Select((p, i) => CopyPlayer(p, i + 2))).ToArray();
            return new LobbySnapshot
            {
                protocol = ProtocolVersion, build = Build, roomId = RoomId, sessionId = SessionId,
                revision = Revision, phase = Phase, seed = Seed, roundId = RoundId,
                hostConfigJson = hostConfigJson, configFingerprint = ConfigFingerprint, recipientNonce = recipientNonce,
                p1 = roster[0], p2 = roster.Length > 1 ? roster[1] : null,
                players = MaximumParticipants > 2 ? roster : Array.Empty<LobbyPlayer>(), canStart = CanStart,
                closeReason = CloseReason, start = start == null ? null : new LobbyStartContract
                { roomId = start.roomId, sessionId = start.sessionId, roundId = start.roundId, seed = start.seed,
                    configFingerprint = start.configFingerprint, hostConfigJson = start.hostConfigJson, continuousTransfers = start.continuousTransfers,
                    participantIds = (ulong[])start.participantIds.Clone() }
            };
        }
        private static LobbyPlayer CopyPlayer(Participant source, int number) => new LobbyPlayer
        { clientId = source.Id, playerNumber = number, connected = true, initialStateReceived = source.InitialStateReceived, ready = source.Ready };
        private static bool Reject(string value, out string reason) { reason = value; return false; }
    }
}
