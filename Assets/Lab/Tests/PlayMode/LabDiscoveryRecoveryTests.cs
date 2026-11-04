using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>
    /// Native macOS Bonjour recovery (#476). Failures are forced through the discovery's own Fail path; a real phone
    /// suspension, lock or Local Network denial is not reproduced here.
    /// </summary>
    public sealed class LabDiscoveryRecoveryTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator AnExpiredAddressIsAskedForAgainAndTheLiveRoomStaysListed()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Native Bonjour supports macOS/iOS.");
            string name = "C6Lab-renew-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            using (var host = new LabBonjourDiscovery())
            using (var browser = new LabBonjourDiscovery())
            {
                var log = new List<string>();
                browser.Diagnostic += log.Add;
                Assert.That(host.StartAdvertise(name, 19491, true, true, 1, 2), Is.True, host.LastError);
                Assert.That(browser.StartBrowse(), Is.True, browser.LastError);
                yield return TickUntil(() => browser.Targets.Any(t => t.ServiceName == name), 20d, host, browser);
                Assert.That(browser.Targets.Any(t => t.ServiceName == name), Is.True, "The room is found.");

                // Pretend the TTL ran out while mDNSResponder kept the record fresh in its cache without a callback.
                object[] entries = Services(browser).Where(s => (string)Field(s, "Name").GetValue(s) == name).ToArray();
                foreach (object record in Records(browser, name)) Field(record, "ExpiresAt").SetValue(record, 0d);
                bool dropped = false;
                double until = Time.realtimeSinceStartupAsDouble + LabBonjourDiscovery.ResolveTimeoutSeconds + 1d;
                while (Time.realtimeSinceStartupAsDouble < until)
                {
                    host.Tick(); browser.Tick();
                    dropped |= !browser.Targets.Any(t => t.ServiceName == name);
                    yield return null;
                }
                Assert.That(log, Has.Some.Contains("stage=address result=renewing"));
                Assert.That(dropped, Is.False, "The room stays listed through the renewal.");
                // Not rescued by the separate re-resolve path: the same entries answered their new query.
                Assert.That(log, Has.None.Contains("stage=address result=failed"));
                Assert.That(log, Has.None.Contains("stage=resolve result=retry"));
                Assert.That(Services(browser).Where(s => (string)Field(s, "Name").GetValue(s) == name), Is.EquivalentTo(entries));
                Assert.That(Records(browser, name).All(record => !(bool)Field(record, "Renewing").GetValue(record)), Is.True);
            }
        }

        [UnityTest]
        public IEnumerator ARoomWhoseLookupFailedIsResolvedAgainWhileTheBrowseLives()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Native Bonjour supports macOS/iOS.");
            string name = "C6Lab-retry-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            using (var host = new LabBonjourDiscovery())
            using (var browser = new LabBonjourDiscovery())
            {
                var log = new List<string>();
                browser.Diagnostic += log.Add;
                Assert.That(host.StartAdvertise(name, 19492, true, true, 1, 2), Is.True, host.LastError);
                Assert.That(browser.StartBrowse(), Is.True, browser.LastError);
                yield return TickUntil(() => browser.Targets.Any(t => t.ServiceName == name), 20d, host, browser);
                Assert.That(browser.Targets.Any(t => t.ServiceName == name), Is.True);

                // The live browse never reports this room again, so before #476 it stayed hidden until a refresh.
                // One entry per interface that saw the room (loopback and Wi-Fi alike).
                foreach (object service in Services(browser).Where(s => (string)Field(s, "Name").GetValue(s) == name).ToArray())
                    Fail(browser, Field(service, "Address").GetValue(service), "forced address failure");
                Assert.That(browser.Targets.Any(t => t.ServiceName == name), Is.False, "The failed lookup drops the room.");
                Assert.That(browser.IsBrowsing, Is.True);
                yield return TickUntil(() => browser.Targets.Any(t => t.ServiceName == name), 10d, host, browser);
                Assert.That(browser.Targets.Any(t => t.ServiceName == name), Is.True, "The room is resolved again.");
                Assert.That(log, Has.Some.Contains("stage=resolve result=retry attempt=1"));
            }
        }

        [UnityTest]
        public IEnumerator AHostWhoseRegistrationDiedRegistersTheSameNameAgainWithoutShowingCreating()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Native Bonjour supports macOS/iOS.");
            LabNetwork network = null;
            yield return LoadLab(value => network = value);
            network.port = 18291;
            try
            {
                network.Host();
                Assert.That(network.IsHost, Is.True, network.Notice);
                yield return Until(() => !network.HostRoomNamePending && !string.IsNullOrEmpty(network.HostRoomName), 15d);
                Assert.That(network.HostRoomName, Is.Not.Empty, "First registration confirmed.");
                string named = network.HostRoomName;
                LabBonjourDiscovery discovery = Discovery(network);
                Fail(discovery, typeof(LabBonjourDiscovery).GetField("advertisement", Private).GetValue(discovery), "forced registration loss");
                Assert.That(discovery.IsAdvertising, Is.False);
                bool pendingSeen = false;
                double deadline = Time.realtimeSinceStartupAsDouble + 10d;
                while (Time.realtimeSinceStartupAsDouble < deadline && !(discovery.IsAdvertising && discovery.AdvertisingConfirmed))
                {
                    pendingSeen |= network.HostRoomNamePending;
                    yield return null;
                }
                Assert.That(Discovery(network), Is.SameAs(discovery), "The same discovery keeps its family and 2:2 marker.");
                Assert.That(discovery.IsAdvertising && discovery.AdvertisingConfirmed, Is.True, "The room is advertised again.");
                Assert.That(pendingSeen, Is.False, "A named room never shows '방을 만들고 있소...' again.");
                Assert.That(network.HostRoomName, Is.EqualTo(named));
            }
            finally { network.Stop(); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AFailedRoomSearchRestartsByItselfAndResumeRebuildsALiveOne()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Native Bonjour supports macOS/iOS.");
            LabNetwork network = null;
            yield return LoadLab(value => network = value);
            network.port = 18292;
            // Let the Lab HUD's one automatic first search run, so only this test restarts the search below.
            yield return Until(() => network.IsSearchingRooms, 5d);
            try
            {
                Assert.That(network.StartRoomSearch(), Is.True, network.RoomSearchMessage);
                LabBonjourDiscovery first = Discovery(network);
                Fail(first, typeof(LabBonjourDiscovery).GetField("browse", Private).GetValue(first), "forced browse failure");
                yield return null;
                Assert.That(network.IsSearchingRooms, Is.False);
                Assert.That(network.RoomSearchFailed, Is.True, "The failure is still shown.");
                Assert.That(network.RoomSearchRetryPending, Is.True);
                string shown = network.RoomSearchMessage;
                yield return Until(() => network.IsSearchingRooms, 5d);
                Assert.That(network.IsSearchingRooms, Is.True, "The search restarted after its first delay.");
                Assert.That(network.RoomSearchRetryPending, Is.False);
                Assert.That(network.RoomSearchFailed, Is.False, "A working search no longer shows the failure.");

                Assert.That(shown, Is.EqualTo("forced browse failure"));

                // A failure appended to an earlier reason, reported again before any restart, is not appended twice.
                MethodInfo report = typeof(LabNetwork).GetMethod("ReportRoomSearchFailure", Private);
                typeof(LabNetwork).GetMethod("SetRoomSearchMessage", Private).Invoke(network, new object[] { "Join failed.", true });
                report.Invoke(network, new object[] { "e1", true });
                string appended = network.RoomSearchMessage;
                Assert.That(appended, Is.EqualTo("Join failed. Search unavailable: e1"));
                report.Invoke(network, new object[] { "e1", true });
                Assert.That(network.RoomSearchMessage, Is.EqualTo(appended));
                typeof(LabNetwork).GetMethod("OnRoomSearchStarted", Private).Invoke(network, null);
                Assert.That(network.RoomSearchMessage, Is.EqualTo("Join failed."), "A restarted search keeps the earlier reason.");

                // Retries used up: only app resume tries again. A user cancel between retries stays canceled.
                typeof(LabNetwork).GetField("roomSearchRetryAttempt", Private).SetValue(network, LabDiscoveryBackoff.MaximumAttempts);
                LabBonjourDiscovery second = Discovery(network);
                Fail(second, typeof(LabBonjourDiscovery).GetField("browse", Private).GetValue(second), "forced browse failure");
                yield return null;
                Assert.That(network.IsSearchingRooms || network.RoomSearchRetryPending, Is.False, "No more retries.");
                typeof(LabNetwork).GetMethod("ResumeDiscoveryAfterPause", Private).Invoke(network, null);
                Assert.That(network.RoomSearchRetryPending, Is.True, "Resume tries again.");
                network.CancelRoomSearch();
                Assert.That(network.RoomSearchRetryPending, Is.False);
                yield return null;
                Assert.That(network.IsSearchingRooms, Is.False, "The canceled search does not come back.");
                Assert.That(network.StartRoomSearch(), Is.True);

                LabBonjourDiscovery live = Discovery(network);
                typeof(LabNetwork).GetMethod("ResumeDiscoveryAfterPause", Private).Invoke(network, null);
                Assert.That(Discovery(network), Is.Not.SameAs(live), "Resume rebuilds a browse that may have gone silent.");
                Assert.That(network.IsSearchingRooms, Is.True);
                network.StartRoomSearch();
                Assert.That(network.RoomSearchRetryPending, Is.False);
            }
            finally { network.Stop(); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ResumeLeavesALiveHostRegistrationAlone()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Native Bonjour supports macOS/iOS.");
            LabNetwork network = null;
            yield return LoadLab(value => network = value);
            network.port = 18293;
            try
            {
                network.Host();
                yield return Until(() => !network.HostRoomNamePending && !string.IsNullOrEmpty(network.HostRoomName), 15d);
                LabBonjourDiscovery discovery = Discovery(network);
                object registration = typeof(LabBonjourDiscovery).GetField("advertisement", Private).GetValue(discovery);
                typeof(LabNetwork).GetMethod("ResumeDiscoveryAfterPause", Private).Invoke(network, null);
                yield return null; yield return null;
                Assert.That(Discovery(network), Is.SameAs(discovery));
                Assert.That(typeof(LabBonjourDiscovery).GetField("advertisement", Private).GetValue(discovery), Is.SameAs(registration));
                Assert.That(discovery.IsAdvertising, Is.True);
            }
            finally { network.Stop(); }
            yield return null;
        }

        private static IEnumerator LoadLab(Action<LabNetwork> found)
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            found(UnityEngine.Object.FindAnyObjectByType<LabNetwork>());
        }

        private static IEnumerator Until(Func<bool> condition, double seconds)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        }

        private static IEnumerator TickUntil(Func<bool> condition, double seconds, params LabBonjourDiscovery[] discoveries)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                foreach (LabBonjourDiscovery discovery in discoveries) discovery.Tick();
                if (condition()) yield break;
                yield return null;
            }
        }

        private static IEnumerator TickFor(double seconds, params LabBonjourDiscovery[] discoveries) =>
            TickUntil(() => false, seconds, discoveries);

        private static LabBonjourDiscovery Discovery(LabNetwork network) =>
            (LabBonjourDiscovery)typeof(LabNetwork).GetField("discovery", Private).GetValue(network);

        private static IEnumerable<object> Services(LabBonjourDiscovery discovery) =>
            ((IDictionary)typeof(LabBonjourDiscovery).GetField("services", Private).GetValue(discovery)).Values.Cast<object>();

        private static IEnumerable<object> Records(LabBonjourDiscovery discovery, string name) => Services(discovery)
            .Where(service => (string)Field(service, "Name").GetValue(service) == name)
            .SelectMany(service => ((IDictionary)Field(service, "Addresses").GetValue(service)).Values.Cast<object>()).ToArray();

        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, Private);

        private static void Fail(LabBonjourDiscovery discovery, object operation, string reason) =>
            typeof(LabBonjourDiscovery).GetMethod("Fail", Private).Invoke(discovery, new[] { operation, reason });
    }
}
