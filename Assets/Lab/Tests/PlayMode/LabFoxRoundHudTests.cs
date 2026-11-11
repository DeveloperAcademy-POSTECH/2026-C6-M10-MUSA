using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>
    /// 여우구슬 팀 판정 화면 (#664): 판정 중 화면 위 패널에 팀 전체 남은 여우구슬 수와 숫자 없는 시간 막대, 끝나면 야광귀 콤보와
    /// 같은 자리의 성공!/시간 끝!, 그 뒤 같은 BIG/SMALL 표시와 판 구슬 크기.
    /// </summary>
    public sealed class LabFoxRoundHudTests
    {
        [UnityTest]
        public IEnumerator TheTeamsFoxOrbsAndTheTimeShowAtTheTopWhileTheRoundRuns()
        {
            var canvasRoot = new GameObject("Fox round canvas", typeof(RectTransform), typeof(Canvas));
            canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var host = new GameObject("Fox round HUD");
            host.transform.SetParent(canvasRoot.transform, false);
            var hud = host.AddComponent<LabFoxRoundHud>();
            hud.Bind(null, canvasRoot.GetComponent<Canvas>());
            try
            {
                yield return null;
                var snapshot = new LabSnapshot
                {
                    sessionId = "fox-hud", round = 1, phase = LabPhase.Playing,
                    foxRoundStartedAt = 5d, foxRoundEndsAt = 20d, foxRoundRunning = true, foxRoundTotal = 6, foxRoundRemaining = 6,
                    foxRoundGoal = 4
                };
                hud.Present(snapshot, 4.9d, true, .1f);
                Assert.That(hud.Visible, Is.False, "Nothing before the drop.");

                hud.Present(snapshot, 5d, true, .1f);
                Assert.That(hud.Visible, Is.True);
                Assert.That(hud.CountText, Is.EqualTo("4"), "The team's goal (six fell, two may stay), not the orbs on the boards.");
                Assert.That(hud.TimeFill, Is.EqualTo(1f).Within(.01f));
                Assert.That(hud.Urgent, Is.False);
                Assert.That(hud.IconSprite, Is.SameAs(Resources.Load<Sprite>(LabOrbAppearance.FoxFireSpritePath)),
                    "The same fox orb art as on the boards.");
                Assert.That(hud.AnyImageTakesTouches, Is.False, "It never takes a touch.");

                snapshot.foxRoundRemaining = 4; snapshot.foxRoundCleared = 2;
                hud.Present(snapshot, 12.5d, true, .1f);
                Assert.That(hud.CountText, Is.EqualTo("2"), "It counts down as the team clears them.");
                Assert.That(hud.TimeFill, Is.EqualTo(.5f).Within(.01f), "Half the time left, as a bar with no seconds.");

                hud.Present(snapshot, 17.5d, true, .1f);
                Assert.That(hud.Urgent, Is.True, "Red in the last three seconds.");

                hud.Present(snapshot, 12.5d, false, .1f);
                Assert.That(hud.Visible, Is.False, "Hidden while paused.");

                snapshot.foxRoundRunning = false; snapshot.foxRoundRemaining = 0;
                snapshot.foxRoundResult = 1; snapshot.foxRoundResultAt = 18d; snapshot.foxRoundCount = 1;
                hud.Present(snapshot, 18d, true, .1f);
                Assert.That(hud.Visible, Is.False, "Gone the moment it is cleared; the result takes its place.");
            }
            finally
            {
                Object.Destroy(canvasRoot);
            }
        }

        [UnityTest]
        public IEnumerator TheRoundsResultShowsLikeTheCombosThenItsSmallOrBig()
        {
            var root = new GameObject("Fox round result fixture");
            var network = root.AddComponent<LabNetwork>();
            network.enabled = false;
            var canvasRoot = new GameObject("Fox round result canvas", typeof(RectTransform), typeof(Canvas));
            var hud = root.AddComponent<LabGourdComboHud>();
            hud.Bind(network, canvasRoot.GetComponent<Canvas>());
            FieldInfo latest = typeof(LabNetwork).GetField("latest", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(latest, Is.Not.Null);
            try
            {
                // 49.9초에 시간이 다 되어 모두 SMALL (64.9초까지). 야광귀 콤보는 한 번도 없었다.
                latest.SetValue(network, Lost(network, 50d));
                yield return null;
                yield return null;
                Assert.That(hud.ResultVisible, Is.True, "The combo's result words, in the same place.");
                Assert.That(hud.ResultLabel, Is.EqualTo(LabGourdComboHud.TimeUpText));
                Assert.That(hud.EffectVisible, Is.False, "SMALL waits for the words to go.");
                Assert.That(hud.BoardLook, Is.EqualTo(-1), "The board's orbs shrink at once.");

                yield return new WaitForSecondsRealtime(LabGourdComboHud.ResultSeconds + .1f);
                latest.SetValue(network, Lost(network, 57.4d));
                yield return null;
                yield return null;
                Assert.That(hud.EffectVisible, Is.True);
                Assert.That(hud.EffectLabel, Is.EqualTo(LabGourdComboHud.PenaltyLabel));
                Assert.That(hud.PenaltyBarFill, Is.EqualTo(.5f).Within(.05f), "Its bar runs from the round's own result.");

                LabSnapshot won = Lost(network, 57.6d);
                won.foxRoundCount = 2; won.foxRoundResult = 1; won.foxRoundResultAt = 57.5d;
                won.players[0].comboPenaltyUntil = 57.5d; won.players[0].comboRewardUntil = 72.5d;
                latest.SetValue(network, won);
                yield return null;
                yield return null;
                Assert.That(hud.ResultVisible, Is.True);
                Assert.That(hud.ResultLabel, Is.EqualTo(LabGourdComboHud.ClearText), "Cleared: 성공!.");
                Assert.That(hud.StarsEmitting, Is.True, "The talismans rain, like the combo's BIG.");
                Assert.That(hud.BoardLook, Is.EqualTo(1));

                var late = new GameObject("Fox round late joiner");
                try
                {
                    var lateHud = late.AddComponent<LabGourdComboHud>();
                    lateHud.Bind(network, canvasRoot.GetComponent<Canvas>());
                    LabSnapshot old = Lost(network, 60d);
                    old.foxRoundCount = 2; old.foxRoundResult = 1; old.foxRoundResultAt = 57.5d;
                    old.players[0].comboPenaltyUntil = 57.5d; old.players[0].comboRewardUntil = 72.5d;
                    latest.SetValue(network, old);
                    yield return null;
                    Assert.That(lateHud.ResultVisible, Is.False, "Someone joining later sees no old result words.");
                }
                finally { Object.Destroy(late); }
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(canvasRoot);
            }
        }

        private static LabSnapshot Lost(LabNetwork network, double now)
        {
            return new LabSnapshot
            {
                sessionId = "fox-result", round = 1, phase = LabPhase.Playing, serverTime = now,
                foxRoundStartedAt = 34.9d, foxRoundEndsAt = 49.9d, foxRoundTotal = 6,
                foxRoundResult = -1, foxRoundResultAt = 49.9d, foxRoundCount = 1,
                players = new[] { new LabPlayerState { id = network.LocalPlayerId, comboPenaltyUntil = 64.9d } }
            };
        }
    }
}
