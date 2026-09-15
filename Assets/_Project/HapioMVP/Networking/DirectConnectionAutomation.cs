using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace C6.Prototype.Networking
{
    // Explicit development command-line harness. With no -c6Role the UI remains manual.
    public sealed class DirectConnectionAutomation : MonoBehaviour
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        [Serializable]
        private sealed class Receipt
        {
            public string buildId = "C6-T02";
            public string role;
            public string scenario;
            public string result = "RUNNING";
            public string currentState;
            public int participantCount;
            public List<string> observations = new List<string>();
        }
        private DirectConnectionSession session;
        private Receipt receipt;
        private string output;
        private bool failed;
        private string port;
        private string host;

        private IEnumerator Start()
        {
            var role = Arg("-c6Role", "");
            if (string.IsNullOrEmpty(role)) yield break;
            session = GetComponent<DirectConnectionSession>();
            output = Arg("-c6Results", "");
            port = Arg("-c6Port", DirectConnectionSession.DefaultPort.ToString());
            host = Arg("-c6Host", "127.0.0.1");
            receipt = new Receipt { role = role, scenario = Arg("-c6Scenario", "observe") };
            session.Changed += ObserveState;
            Application.runInBackground = true;
            Record("START " + DateTime.UtcNow.ToString("O"));
            if (receipt.scenario == "observe")
            {
                Check(role == "host" ? session.StartHost(port) : session.Join(host, port), "observe-start");
                yield break;
            }
            if (receipt.scenario == "timeout" || receipt.scenario == "reject")
            {
                Check(session.Join(host, port), "join-request-accepted");
                yield return WaitFor(() => session.State == DirectConnectionState.Failed && session.CanStart,
                    DirectConnectionSession.ConnectionTimeoutSeconds + 8f, "expected-connection-failure");
                Record("FAILURE_MESSAGE " + session.Message);
                if (receipt.scenario == "reject")
                    Check(session.Message.Contains("already has two participants"), "explicit-room-full-rejection");
                Check(session.ParticipantIds.Count == 0, "no-participants-after-failure");
                if (receipt.scenario == "timeout")
                {
                    Check(session.Join(host, port), "manual-retry-accepted");
                    session.Stop();
                    yield return WaitFor(() => session.CanStart, 8f, "cancel-stops-retry");
                }
                Finish();
                yield break;
            }
            if (receipt.scenario != "cycles")
            {
                Check(false, "unknown-scenario"); Finish(); yield break;
            }
            int cycles = int.Parse(Arg("-c6Cycles", "2"));
            float hold = float.Parse(Arg("-c6Hold", "3"), System.Globalization.CultureInfo.InvariantCulture);
            for (int cycle = 1; cycle <= cycles && !failed; cycle++)
            {
                bool isHost = role == "host";
                Check(isHost ? session.StartHost(port) : session.Join(host, port), "start-cycle-" + cycle);
                Check(!(isHost ? session.StartHost(port) : session.Join(host, port)), "duplicate-start-rejected");
                yield return WaitFor(() => session.State == DirectConnectionState.Connected && session.ParticipantIds.Count == 2,
                    35f, "two-participants-cycle-" + cycle);
                if (failed) break;
                var ids = session.ParticipantIds.ToArray();
                Check(ids.Distinct().Count() == 2 && ids.Contains(0UL) && session.LocalClientId.HasValue && ids.Contains(session.LocalClientId.Value), "identities-cycle-" + cycle);
                Check(UnityEngine.Object.FindObjectsByType<NetworkManager>().Length == 1, "one-manager-connected");
                Record("CONNECTED cycle=" + cycle + " local=" + session.LocalClientId + " ids=" + string.Join(",", ids));
                if (!isHost)
                {
                    yield return new WaitForSecondsRealtime(hold);
                    session.Stop();
                }
                yield return WaitFor(() => session.CanStart, hold + 15f, "session-ended-cycle-" + cycle);
                Check(session.ParticipantIds.Count == 0, "participants-cleared");
                Check(UnityEngine.Object.FindObjectsByType<NetworkManager>().Length <= 1, "no-duplicate-manager-after-stop");
                Record("ENDED cycle=" + cycle);
                yield return new WaitForSecondsRealtime(isHost ? 2f : 3f);
                Check(session.CanStart, "no-automatic-reconnect");
            }
            Finish();
        }

        private IEnumerator WaitFor(Func<bool> predicate, float seconds, string label)
        {
            var until = Time.realtimeSinceStartupAsDouble + seconds;
            while (!predicate() && Time.realtimeSinceStartupAsDouble < until && !failed) yield return null;
            Check(predicate(), label);
        }
        private void Check(bool success, string label)
        {
            if (!success) failed = true;
            Record((success ? "CHECK_PASS " : "CHECK_FAIL ") + label);
        }
        private void ObserveState()
        {
            receipt.currentState = session.State.ToString();
            receipt.participantCount = session.ParticipantIds.Count;
            Record("STATE " + receipt.currentState + " participants=" + receipt.participantCount);
        }
        private void OnDestroy()
        {
            if (session != null) session.Changed -= ObserveState;
        }
        private void Record(string text)
        {
            receipt.observations.Add(text);
            Debug.Log("C6_T02_AUTOMATION " + text);
            if (!string.IsNullOrEmpty(output))
            {
                var parent = Path.GetDirectoryName(Path.GetFullPath(output));
                Directory.CreateDirectory(parent);
                File.WriteAllText(output, JsonUtility.ToJson(receipt, true));
            }
        }
        private void Finish()
        {
            session.Stop();
            receipt.result = failed ? "FAIL" : "PASS";
            Record("COMPLETE " + receipt.result);
            Application.Quit(failed ? 1 : 0);
        }
        private static string Arg(string key, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == key) return args[i + 1];
            return fallback;
        }
#endif
    }
}
