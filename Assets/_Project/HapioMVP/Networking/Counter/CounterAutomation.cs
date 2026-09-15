using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace C6.Prototype.Networking.Counter
{
    // Only explicit development command-line arguments activate automatic requests.
    [DisallowMultipleComponent, RequireComponent(typeof(DirectConnectionSession), typeof(CounterSync))]
    public sealed class CounterAutomation : MonoBehaviour
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        [Serializable]
        private sealed class RunReceipt
        {
            public string buildId = "C6-T03";
            public string role;
            public string scenario;
            public string result = "RUNNING";
            public string phase = "STARTING";
            public string scope = "Explicit automatic input in a macOS development player; not physical iPhone input or a two-iPhone gate.";
            public string currentState;
            public int participantCount;
            public string sessionId;
            public string localClientId;
            public bool snapshotReady;
            public long initialSnapshotValue = -1;
            public long value;
            public ulong revision;
            public ulong approved;
            public ulong rejected;
            public ulong sent;
            public ulong acknowledged;
            public ulong pending;
            public ulong duplicates;
            public string sendStartedUtc;
            public string sendEndedUtc;
            public List<string> observations = new List<string>();
        }

        private DirectConnectionSession session;
        private CounterSync counter;
        private RunReceipt receipt;
        private string output;
        private string releasePath;
        private string beginPath;
        private bool failed;
        private bool frozen;
        private bool subscribed;

        private IEnumerator Start()
        {
            string role = Arg("-c3Role", string.Empty);
            if (string.IsNullOrEmpty(role)) yield break;

            session = GetComponent<DirectConnectionSession>();
            counter = GetComponent<CounterSync>();
            output = Arg("-c3Results", string.Empty);
            releasePath = Arg("-c3Release", string.Empty);
            beginPath = Arg("-c3Begin", string.Empty);
            receipt = new RunReceipt { role = role, scenario = Arg("-c3Scenario", "concurrent") };
            Application.runInBackground = true;
            Subscribe();
            Record("START " + DateTime.UtcNow.ToString("O"));
            bool isHost = role == "host";
            bool isLan = receipt.scenario == "lan";
            Check(role == "host" || role == "client", "known-role");
            Check(receipt.scenario == "sequential" || receipt.scenario == "concurrent" || isLan, "known-scenario");
            Check(!isLan || isHost, "lan-observation-is-host-only");
            Check(isLan || !string.IsNullOrEmpty(releasePath), "automatic-scenarios-have-a-release-barrier");
            Check(receipt.scenario != "concurrent" || !string.IsNullOrEmpty(beginPath), "concurrent-scenario-has-a-start-barrier");
            if (failed) { Finish(); yield break; }

            string port = Arg("-c3Port", DirectConnectionSession.DefaultPort.ToString());
            string host = Arg("-c3Host", "127.0.0.1");
            Check(isHost ? session.StartHost(port) : session.Join(host, port), "start-connection");
            yield return WaitFor(() => session.State == DirectConnectionState.Connected && counter.IsReady,
                35f, "connected-with-full-initial-state");
            if (failed) { Finish(); yield break; }
            receipt.initialSnapshotValue = counter.Snapshot.Value;
            Record("INITIAL_SNAPSHOT value=" + receipt.initialSnapshotValue + " session=" + counter.Snapshot.SessionId);

            if (isHost && (receipt.scenario == "sequential" || isLan))
            {
                Check(counter.Snapshot.Value == 0, "new-host-starts-at-zero");
                yield return SendFifty();
                Check(counter.Snapshot.Value == 50 && counter.Snapshot.ApprovedCount == 50,
                    "host-prefills-fifty-before-peer-joins");
                if (failed) { Finish(); yield break; }
                SetPhase("PREFILLED");
                if (isLan)
                {
                    receipt.result = "OBSERVING";
                    Record("LAN_WAITING_FOR_MANUAL_PEER_INPUT hostAutomaticRequests=50");
                    // The app stays open. Changed records the eventual observation,
                    // without treating it as physical-input or two-iPhone proof.
                    yield break;
                }
            }
            else if (isHost) SetPhase("READY_FOR_PEER");

            yield return WaitFor(() => session.ParticipantIds.Count == 2 && counter.IsReady,
                35f, "two-participants-ready");
            if (failed) { Finish(); yield break; }

            if (receipt.scenario == "concurrent")
            {
                Check(counter.Snapshot.Value == 0, "concurrent-start-barrier-keeps-zero-initial-value");
                SetPhase("READY_FOR_REQUESTS");
                yield return WaitFor(() => File.Exists(beginPath), 35f, "concurrent-start-barrier-released");
                if (!failed) yield return SendFifty();
            }
            else if (!isHost)
            {
                Check(receipt.initialSnapshotValue == 50 && counter.Snapshot.Value == 50,
                    "joining-client-receives-prefilled-full-state");
                if (!failed) yield return SendFifty();
            }

            yield return WaitFor(() => counter.IsReady && counter.Snapshot.Value == 100
                && counter.AcknowledgedCount == 50 && counter.PendingCount == 0,
                35f, "all-fifty-local-requests-acknowledged-and-shared-value-one-hundred");
            if (failed) { Finish(); yield break; }
            CheckFinalState();
            ulong previousDuplicates = counter.DuplicateReceiptCount;
            Check(counter.ResendLastRequest(), "explicit-last-request-retransmission-sent");
            yield return WaitFor(() => counter.DuplicateReceiptCount > previousDuplicates,
                15f, "duplicate-receipt-returned");
            Check(counter.LastReceipt.HasValue && counter.LastReceipt.Value.Approved
                && counter.LastReceipt.Value.Duplicate && counter.LastReceipt.Value.RequestId == 50,
                "replay-is-original-approved-last-request");
            CheckFinalState();
            if (failed) { Finish(); yield break; }

            CaptureState();
            SetPhase("READY_TO_RELEASE");
            frozen = true;
            Unsubscribe();
            // The orchestrator compares both frozen records before releasing either
            // process. This prevents shutdown callbacks from erasing the peer's proof.
            yield return WaitFor(() => File.Exists(releasePath), 35f, "peer-results-compared-and-release-barrier-opened");
            Finish();
        }

        private IEnumerator SendFifty()
        {
            receipt.sendStartedUtc = DateTime.UtcNow.ToString("O");
            Record("SEND_BEGIN uniqueRequests=50 intervalSeconds=0.02");
            for (int index = 0; index < 50 && !failed; ++index)
            {
                if (!counter.RequestIncrement()) Check(false, "request-accepted-index-" + (index + 1));
                yield return new WaitForSecondsRealtime(0.02f);
            }
            receipt.sendEndedUtc = DateTime.UtcNow.ToString("O");
            Check(counter.SentCount == 50, "fifty-unique-local-requests-sent");
            Record("SEND_END");
        }

        private void CheckFinalState()
        {
            Check(counter.Snapshot.Value == 100 && counter.Snapshot.ApprovedCount == 100
                && counter.Snapshot.RejectedCount == 0 && counter.Snapshot.Revision == 100,
                "value-and-approved-and-revision-equal-one-hundred-with-zero-rejections");
            Check(counter.SentCount == 50 && counter.AcknowledgedCount == 50 && counter.PendingCount == 0,
                "local-unique-sent-and-acknowledged-fifty-with-zero-pending");
        }

        private IEnumerator WaitFor(Func<bool> predicate, float seconds, string label)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds;
            while (!failed && !predicate() && Time.realtimeSinceStartupAsDouble < until) yield return null;
            Check(!failed && predicate(), label);
        }

        private void Subscribe()
        {
            session.Changed += Changed;
            counter.Changed += Changed;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            session.Changed -= Changed;
            counter.Changed -= Changed;
            subscribed = false;
        }

        private void Changed()
        {
            if (receipt == null || frozen) return;
            CaptureState();
            if (receipt.scenario == "lan" && receipt.phase == "PREFILLED"
                && counter.IsReady && session.ParticipantIds.Count == 2 && counter.Snapshot.Value == 100
                && counter.Snapshot.ApprovedCount == 100 && counter.Snapshot.Revision == 100
                && counter.Snapshot.RejectedCount == 0 && counter.SentCount == 50
                && counter.AcknowledgedCount == 50 && counter.PendingCount == 0)
            {
                receipt.phase = "LAN_OBSERVED_100";
                receipt.result = "OBSERVED";
                Record("LAN_OBSERVED_100 automaticHostRequests=50 physicalPeerInput=NOT_VERIFIED twoIPhoneGate=NOT_RUN");
                frozen = true;
                Unsubscribe();
            }
            else WriteReceipt();
        }

        private void CaptureState()
        {
            if (frozen) return;
            receipt.currentState = session.State.ToString();
            receipt.participantCount = session.ParticipantIds.Count;
            receipt.sessionId = counter.Snapshot.SessionId;
            receipt.localClientId = session.LocalClientId?.ToString() ?? string.Empty;
            receipt.snapshotReady = counter.IsReady;
            receipt.value = counter.Snapshot.Value;
            receipt.revision = counter.Snapshot.Revision;
            receipt.approved = counter.Snapshot.ApprovedCount;
            receipt.rejected = counter.Snapshot.RejectedCount;
            receipt.sent = counter.SentCount;
            receipt.acknowledged = counter.AcknowledgedCount;
            receipt.pending = counter.PendingCount;
            receipt.duplicates = counter.DuplicateReceiptCount;
        }

        private void SetPhase(string phase)
        {
            receipt.phase = phase;
            Record("PHASE " + phase);
        }

        private void Check(bool success, string label)
        {
            if (!success) failed = true;
            Record((success ? "CHECK_PASS " : "CHECK_FAIL ") + label);
        }

        private void Record(string observation)
        {
            CaptureState();
            receipt.observations.Add(observation);
            Debug.Log("C6_T03_AUTOMATION " + observation);
            WriteReceipt();
        }

        private void WriteReceipt()
        {
            if (string.IsNullOrEmpty(output)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output, JsonUtility.ToJson(receipt, true));
        }

        private void Finish()
        {
            Unsubscribe();
            receipt.result = failed ? "FAIL" : "PASS";
            Record("COMPLETE " + receipt.result);
            frozen = true;
            session.Stop();
            Application.Quit(failed ? 1 : 0);
        }

        private void OnDestroy() => Unsubscribe();

        private static string Arg(string key, string fallback)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; ++index)
                if (arguments[index] == key) return arguments[index + 1];
            return fallback;
        }
#endif
    }
}
