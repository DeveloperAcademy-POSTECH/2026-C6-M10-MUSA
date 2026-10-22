using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    public sealed class LabRoomFamilyDiscoveryTests
    {
        [UnityTest]
        public IEnumerator NativeBrowsersOnlyExposeTheirOwnFamilyAndKeepGamePresenceUpdates()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Native Bonjour supports macOS/iOS.");
            string suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            string labName = "C6Lab-family-" + suffix, gameName = "Hapio-family-" + suffix;
            using (var labHost = new LabBonjourDiscovery())
            using (var gameHost = new LabBonjourDiscovery(LabRoomFamily.Game))
            using (var labBrowser = new LabBonjourDiscovery())
            using (var gameBrowser = new LabBonjourDiscovery(LabRoomFamily.Game))
            {
                Assert.That(labHost.StartAdvertise(labName, 19481, true, true, 1, 2, false, true), Is.True, labHost.LastError);
                Assert.That(gameHost.StartAdvertise(gameName, 19482, true, true, 1, 2, false, true), Is.True, gameHost.LastError);
                Assert.That(labBrowser.StartBrowse(), Is.True, labBrowser.LastError);
                Assert.That(gameBrowser.StartBrowse(), Is.True, gameBrowser.LastError);
                double deadline = Time.realtimeSinceStartupAsDouble + 20d;
                while (Time.realtimeSinceStartupAsDouble < deadline)
                {
                    labHost.Tick(); gameHost.Tick(); labBrowser.Tick(); gameBrowser.Tick();
                    if (labBrowser.Targets.Any(t => t.ServiceName == labName)
                        && gameBrowser.Targets.Any(t => t.ServiceName == gameName)) break;
                    yield return null;
                }
                Assert.That(labBrowser.Targets.Any(t => t.ServiceName == labName && t.WaitingForHost), Is.True);
                Assert.That(gameBrowser.Targets.Any(t => t.ServiceName == gameName && t.WaitingForHost), Is.True);
                Assert.That(labBrowser.Targets.Any(t => t.ServiceName == gameName), Is.False);
                Assert.That(gameBrowser.Targets.Any(t => t.ServiceName == labName), Is.False);
                Assert.That(gameHost.UpdatePresence(2, 2, true), Is.True, gameHost.LastError);
                deadline = Time.realtimeSinceStartupAsDouble + 12d;
                while (Time.realtimeSinceStartupAsDouble < deadline)
                {
                    labHost.Tick(); gameHost.Tick(); labBrowser.Tick(); gameBrowser.Tick();
                    if (gameBrowser.Targets.Any(t => t.ServiceName == gameName && t.Playing && t.Participants == 2)) break;
                    yield return null;
                }
                Assert.That(gameBrowser.Targets.Any(t => t.ServiceName == gameName && t.Playing
                    && t.Participants == 2 && !t.WaitingForHost), Is.True);
                Assert.That(labBrowser.Targets.Any(t => t.ServiceName == gameName), Is.False);
            }
        }
    }
}
