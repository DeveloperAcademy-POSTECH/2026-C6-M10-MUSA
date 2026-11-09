using System;
using System.Globalization;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// Bounded automatic recovery of room discovery (#476): a failed browse restarts with backoff, a Host whose
    /// advertisement died registers the same requested name again, and app resume rebuilds both. User actions reset
    /// the budget. Nothing here touches NGO, the transport or the Bonjour TXT format.
    /// </summary>
    public sealed partial class LabNetwork
    {
        private int roomSearchRetryAttempt;
        // Retries ran out; app resume (a permission granted in Settings, a network back) still tries once more.
        private bool roomSearchRetryExhausted;
        private double roomSearchRetryAt = double.NaN;
        private double browseAliveSince = double.NaN;
        // The search failure shown last and the message it was appended to, so a retry neither grows nor hides it.
        private string searchFailureMessage, searchFailureBase;
        private bool searchFailureBaseFailed;
        private bool hostAdvertiseWanted, hostAdvertiseAutoRename;
        private string hostAdvertiseName;
        private int hostAdvertiseAttempt;
        private double hostAdvertiseRetryAt = double.NaN;

        /// <summary>A failed room search will restart by itself; the screen may keep showing the last result meanwhile.</summary>
        public bool RoomSearchRetryPending => !double.IsNaN(roomSearchRetryAt);

        private void ScheduleRoomSearchRetry(double now)
        {
            if (!LabBonjourDiscovery.IsSupported
                || !LabDiscoveryBackoff.CanRetry(roomSearchRetryAttempt, LabDiscoveryBackoff.MaximumAttempts))
            {
                if (LabBonjourDiscovery.IsSupported)
                {
                    roomSearchRetryExhausted = true;
                    Debug.LogWarning("C6_LAB_DISCOVERY_RETRY kind=browse result=stopped");
                }
                return;
            }
            double delay = LabDiscoveryBackoff.Delay(roomSearchRetryAttempt++);
            roomSearchRetryAt = now + delay;
            Debug.Log("C6_LAB_DISCOVERY_RETRY kind=browse attempt=" + roomSearchRetryAttempt.ToString(CultureInfo.InvariantCulture)
                + " delay=" + delay.ToString("0", CultureInfo.InvariantCulture));
        }

        private void ResetRoomSearchRetry()
        {
            roomSearchRetryAttempt = 0;
            roomSearchRetryAt = double.NaN;
            browseAliveSince = double.NaN;
            roomSearchRetryExhausted = false;
        }

        /// <summary>
        /// Whether a room search keeps an iPhone awake. Hapio browses quietly behind its title after a game; only the
        /// visible room list counts there. Lab always shows its list.
        /// </summary>
        public bool RoomSearchKeepsAwake { get; set; } = true;

        /// <summary>Runs early in Update, where a pending one-shot resume runs too.</summary>
        private void UpdateRoomSearchRecovery(double now)
        {
            if (IsSearchingRooms)
            {
                if (double.IsNaN(browseAliveSince)) browseAliveSince = now;
                else if (now - browseAliveSince >= LabDiscoveryBackoff.StableSeconds) roomSearchRetryAttempt = 0;
            }
            else browseAliveSince = double.NaN;
            if (!RoomSearchRetryPending || now < roomSearchRetryAt) return;
            if (discovery != null || !CanBeginConnection || IsJoiningRoom)
            {
                // Hosting, joining or a fresh search took over: that path owns discovery now.
                roomSearchRetryAt = double.NaN;
                return;
            }
            roomSearchRetryAt = double.NaN;
            StartRoomSearchInternal(true);
        }

        /// <summary>A retried search that starts again restores whatever the failure was appended to.</summary>
        private void OnRoomSearchStarted()
        {
            if (searchFailureMessage == null || roomSearchMessage != searchFailureMessage) return;
            if (string.IsNullOrEmpty(searchFailureBase)) ClearRoomSearchMessage();
            else SetRoomSearchMessage(searchFailureBase, searchFailureBaseFailed);
            searchFailureMessage = searchFailureBase = null;
        }

        private void RememberHostAdvertisement(string requestedName, bool autoRename)
        {
            hostAdvertiseWanted = !string.IsNullOrEmpty(requestedName);
            hostAdvertiseName = requestedName;
            hostAdvertiseAutoRename = autoRename;
            hostAdvertiseAttempt = 0;
            hostAdvertiseRetryAt = double.NaN;
        }

        private void ForgetHostAdvertisement()
        {
            hostAdvertiseWanted = false;
            hostAdvertiseName = null;
            hostAdvertiseRetryAt = double.NaN;
        }

        private void ScheduleHostAdvertiseRetry(double now)
        {
            if (!LabDiscoveryBackoff.CanRetry(hostAdvertiseAttempt, LabDiscoveryBackoff.MaximumAttempts))
            {
                hostAdvertiseRetryAt = double.PositiveInfinity;
                Debug.LogWarning("C6_LAB_DISCOVERY_READVERTISE result=stopped name=" + hostAdvertiseName);
                return;
            }
            hostAdvertiseRetryAt = now + LabDiscoveryBackoff.Delay(hostAdvertiseAttempt++);
        }

        /// <summary>
        /// Runs after Tick, outside the native pump, on the same discovery so its family and 2:2 marker stay. A
        /// registration still inside its removed-reply window is alive and is left alone.
        /// </summary>
        private void RecoverHostAdvertisement(LabBonjourDiscovery active, double now)
        {
            if (!hostAdvertiseWanted || !IsHost || developerSolo || active.IsAdvertising) return;
            if (double.IsNaN(hostAdvertiseRetryAt)) { ScheduleHostAdvertiseRetry(now); return; }
            if (now < hostAdvertiseRetryAt) return;
            hostAdvertiseRetryAt = double.NaN;
            bool ipv4 = !(transport is LabDualStackTransport dual) || dual.HasIpv4Listener;
            bool ipv6 = transport is LabDualStackTransport second && second.HasIpv6Listener;
            bool started;
            try
            {
                started = active.StartAdvertise(hostAdvertiseName, port, ipv4, ipv6,
                    Math.Max(1, Math.Min(targetParticipantCount, LobbyPlaceCount)),
                    targetParticipantCount, RoomPresenceBusy, false, hostAdvertiseAutoRename);
            }
            catch (ArgumentException) { started = false; }
            Debug.Log("C6_LAB_DISCOVERY_READVERTISE attempt=" + hostAdvertiseAttempt.ToString(CultureInfo.InvariantCulture)
                + " name=" + hostAdvertiseName + " result=" + (started ? "started" : "failed"));
            if (started) return;
            // Another local room took the freed explicit name: let the daemon pick a free one instead.
            if (active.LocalRegistrationNameConflict) hostAdvertiseAutoRename = true;
            ScheduleHostAdvertiseRetry(now);
        }

        private void OnHostAdvertisementConfirmed()
        {
            hostAdvertiseAttempt = 0;
            hostAdvertiseRetryAt = double.NaN;
        }

        private void OnApplicationPause(bool paused)
        {
            // The Editor sends pause on focus changes; only a suspended phone needs its discovery rebuilt.
            if (!Application.isMobilePlatform) return;
            if (paused)
            {
                pausedAtWallClock = WallClockSeconds;
                return;
            }
            ResumeDiscoveryAfterPause();
            // The wall clock, not the transport's, measures a suspension (#501).
            double pausedFor = double.IsNaN(pausedAtWallClock) ? 0d : WallClockSeconds - pausedAtWallClock;
            pausedAtWallClock = double.NaN;
            ResumeLobbyAfterPause(pausedFor);
        }

        /// <summary>
        /// A suspended app may come back with a silent browse or a lost registration. Rebuild the browse (the 5 s row
        /// grace keeps the list) and retry a dead advertisement now; a live registration is never torn down.
        /// </summary>
        internal void ResumeDiscoveryAfterPause()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            bool search = false, host = false;
            if (roomSearchActive && CanBeginConnection && !IsJoiningRoom)
            {
                ResetRoomSearchRetry();
                search = StartRoomSearchInternal(true);
            }
            else if (RoomSearchRetryPending || roomSearchRetryExhausted)
            {
                roomSearchRetryAttempt = 0;
                roomSearchRetryExhausted = false;
                roomSearchRetryAt = now;
                search = true;
            }
            if (hostAdvertiseWanted && IsHost && discovery != null && !discovery.IsAdvertising)
            {
                hostAdvertiseAttempt = 0;
                hostAdvertiseRetryAt = now;
                host = true;
            }
            Debug.Log("C6_LAB_DISCOVERY_LIFECYCLE event=resume search=" + (search ? "1" : "0") + " host=" + (host ? "1" : "0"));
        }
    }
}
