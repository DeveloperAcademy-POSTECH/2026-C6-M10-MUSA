using System.Linq;
using System.Reflection;
using C6Lab;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio.Tests
{
    public sealed class GameStageResultViewTests
    {
        [TestCase(GameScenePaths.StageResult, true)]
        [TestCase(GameScenePaths.GameOver, false)]
        public void SavedResultsHaveEditableWidgetsAndRealBackgrounds(string path, bool victory)
        {
            WithView(path, view =>
            {
                Assert.That(view.background.texture, Is.Not.Null);
                Assert.That(view.background.texture.width, Is.GreaterThan(1000));
                Assert.That(view.background.raycastTarget, Is.False);
                Assert.That(view.viewerButton, Is.Not.SameAs(view.homeButton));
                Assert.That(view.nextButton.gameObject.activeSelf, Is.False);
                if (victory) Assert.That(view.viewerButton, Is.Not.Null);
                else Assert.That(view.viewerButton, Is.Null);
                if (victory)
                {
                    // 2:2 (#293): blue watercolour corners for 청, red for 홍.
                    Assert.That(view.blueTeamBackground, Is.Not.Null.And.Not.SameAs(view.redTeamBackground));
                    Assert.That(view.redTeamBackground, Is.SameAs(view.background.texture));
                }
                Assert.That(view.yokai.font.name, Is.EqualTo("ShillaCultureBold"));
                Assert.That(view.GetComponentInParent<GameScreen>().legacyResultContent.activeSelf, Is.False);
                Assert.That(view.scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            });
        }

        [Test]
        public void FirstResultRefreshInitializesNonHostActionsWithoutLegacyContent()
        {
            WithView(GameScenePaths.StageResult, view =>
            {
                GameObject root = Object.Instantiate(Resources.Load<GameObject>(GameSession.ResourcePath));
                try
                {
                    GameSession session = root.GetComponent<GameSession>();
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    typeof(GameSession).GetField("stageCatalog", flags).SetValue(session, GameStageCatalog.RequireDefault());
                    session.State.phase = GameFlowPhase.StageResult;
                    session.State.stageIndex = 2;
                    session.State.runId = "non-host-result-fixture";
                    GameScreen screen = view.GetComponentInParent<GameScreen>();
                    typeof(GameScreen).GetField("session", flags).SetValue(screen, session);
                    screen.legacyResultContent.SetActive(true);
                    view.judgment.text = "stale saved result";

                    typeof(GameScreen).GetMethod("OnEnable", flags).Invoke(screen, null);

                    Assert.That(session.Network.IsHost, Is.False);
                    Assert.That(view.stage.text, Is.EqualTo("STAGE 2"));
                    Assert.That(view.yokai.text, Is.EqualTo("야광귀"));
                    Assert.That(view.judgment.text, Is.EqualTo("봉인 성공 !"));
                    Assert.That(view.nextButton.gameObject.activeSelf, Is.False, "Only the Host sees manual NEXT.");
                    Assert.That(view.guidance.text, Does.Contain("결과 기록을 받고 있소"));
                    Assert.That(screen.legacyResultContent.activeSelf, Is.False);
                    typeof(GameSession).GetProperty("ResultSnapshot").SetValue(session,
                        new LabSnapshot { phase = LabPhase.Victory, players = new LabPlayerState[0] });
                    typeof(GameScreen).GetMethod("OnEnable", flags).Invoke(screen, null);
                    Assert.That(view.guidance.text, Does.Contain("방장이 다음 스테이지를"));
                    typeof(GameScreen).GetMethod("OnNextButton", flags).Invoke(screen, null);
                    Assert.That(session.State.stageIndex, Is.EqualTo(2), "Hidden/programmatic non-Host input cannot advance.");

                    typeof(GameScreen).GetField("resultAwaitingPointerRelease", flags).SetValue(screen, true);
                    typeof(GameScreen).GetMethod("ApplyResultInputGate", flags).Invoke(screen, null);
                    Assert.That(view.homeButton.interactable, Is.False);
                    Assert.That(view.viewerButton.interactable, Is.False);
                    typeof(GameScreen).GetMethod("OnHomeButton", flags).Invoke(screen, null);
                    typeof(GameScreen).GetMethod("OnViewerButton", flags).Invoke(screen, null);
                    Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.StageResult),
                        "Handler guards must reject input even when a disabled button is invoked programmatically.");
                }
                finally { Object.DestroyImmediate(root); }
            });
        }

        [TestCase(LabTeam.Blue)]
        [TestCase(LabTeam.Red)]
        public void TerritoryResultShowsTheWinnerOnItsTeamColourAndComparesTheTeams(LabTeam winner)
        {
            WithView(GameScenePaths.StageResult, view =>
            {
                GameObject root = Object.Instantiate(Resources.Load<GameObject>(GameSession.ResourcePath));
                try
                {
                    GameSession session = root.GetComponent<GameSession>();
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    typeof(GameSession).GetField("stageCatalog", flags).SetValue(session, GameStageCatalog.RequireDefault());
                    session.State.mode = GameMode.Territory;
                    session.State.phase = GameFlowPhase.Completed;
                    session.State.stageIndex = 1;
                    session.State.runId = "territory-result-fixture";
                    GameScreen screen = view.GetComponentInParent<GameScreen>();
                    typeof(GameScreen).GetField("session", flags).SetValue(screen, session);
                    screen.legacyResultContent.SetActive(true);
                    bool blue = winner == LabTeam.Blue;
                    // P1·P2 청, P3·P4 홍: throws 10..13, hits 5..8, stolen 0/100/200/300 cells.
                    LabPlayerState[] players = Enumerable.Range(0, 4).Select(seat => new LabPlayerState
                        { id = (ulong)(seat + 1), seat = seat, throwTotal = 10 + seat, hitTotal = 5 + seat, stolenCells = 100 * seat }).ToArray();
                    typeof(GameSession).GetProperty("ResultSnapshot").SetValue(session, new LabSnapshot
                    {
                        phase = LabPhase.Victory, teamTerritory = true, teamWinner = winner, paintCellCount = 1000,
                        blueCells = blue ? 550 : 300, redCells = blue ? 300 : 550, paintCoverage = .85f, remaining = 15.2f,
                        players = players
                    });

                    typeof(GameScreen).GetMethod("OnEnable", flags).Invoke(screen, null);

                    Assert.That(screen.legacyResultContent.activeSelf, Is.False, "2:2 uses the designed result page (#293).");
                    Assert.That(view.gameObject.activeInHierarchy, Is.True);
                    Assert.That(view.stage.text, Is.EqualTo("2 : 2"));
                    Assert.That(view.yokai.text, Is.EqualTo("땅따먹기 결과"));
                    Assert.That(view.judgment.text, Does.Contain(blue ? "청팀" : "홍팀").And.Contain("승리~!"));
                    Assert.That(view.background.texture, Is.SameAs(blue ? view.blueTeamBackground : view.redTeamBackground));
                    Assert.That(view.paint.text, Is.EqualTo("PAINT 85.0%"));
                    Assert.That(view.timeLeft.text, Is.EqualTo("TIME LEFT 16s"));
                    Assert.That(view.design.Find("AwardTable/TitleHeader").GetComponent<Text>().text, Is.EqualTo("항목"));
                    Assert.That(view.design.Find("AwardTable/PlayerHeader").GetComponent<Text>().text, Is.EqualTo("청팀"));
                    Assert.That(view.design.Find("AwardTable/RecordHeader").GetComponent<Text>().text, Is.EqualTo("홍팀"));
                    string Cell(int row, string column) => view.rows.Find("Award_" + row + "/" + column).GetComponent<Text>().text;
                    Assert.That(new[] { Cell(0, "Title"), Cell(0, "Player"), Cell(0, "Record") },
                        Is.EqualTo(new[] { "차지한 땅", blue ? "55.0%" : "30.0%", blue ? "30.0%" : "55.0%" }));
                    Assert.That(new[] { Cell(1, "Player"), Cell(1, "Record") }, Is.EqualTo(new[] { "10.0%", "50.0%" }), "빼앗은 땅");
                    Assert.That(new[] { Cell(2, "Player"), Cell(2, "Record") }, Is.EqualTo(new[] { "21회", "25회" }), "던진 횟수");
                    Assert.That(new[] { Cell(3, "Player"), Cell(3, "Record") }, Is.EqualTo(new[] { "11회", "15회" }), "명중 횟수");
                    Assert.That(new[] { Cell(4, "Title"), Cell(4, "Player"), Cell(4, "Record") }, Is.EqualTo(new[] { "피버 횟수", "0번", "0번" }));
                    Assert.That(view.rows.Find("Award_0/Highlight").gameObject.activeSelf, Is.False, "Teams, not this player's row.");
                    Assert.That(view.rows.Find("Award_2/Record").GetComponent<Text>().color, Is.EqualTo(GameStageResultView.RedTeamInk),
                        "The leading team's value is in its colour.");
                    Assert.That(view.nextButton.gameObject.activeSelf, Is.False, "One battle: there is no next stage.");
                    Assert.That(view.guidance.text, Does.Contain("땅따먹기를 다시 하고싶다면"));
                    Assert.That(view.homeButton.GetComponentInChildren<Text>(true).text, Is.EqualTo("나가기"));
                    Assert.That(view.viewerButton.GetComponentInChildren<Text>(true).text, Is.EqualTo("박 확인"));
                }
                finally { Object.DestroyImmediate(root); }
            });
        }

        [Test]
        public void TeamTableCountsEachTeamsPlayersAndLeavesTiesUncoloured()
        {
            var result = new LabSnapshot
            {
                paintCellCount = 200, blueCells = 50, redCells = 50,
                players = new[] { new LabPlayerState { seat = 0, throwTotal = 3 }, new LabPlayerState { seat = 3, throwTotal = 3 } }
            };
            var rows = GameTerritoryResult.Rows(result, 2, 1);
            Assert.That(rows.Select(row => row.Title), Is.EqualTo(new[] { "차지한 땅", "빼앗은 땅", "던진 횟수", "명중 횟수", "피버 횟수" }));
            Assert.That(rows[0].Leader, Is.EqualTo(LabTeam.None), "25% each: a tie.");
            Assert.That(rows[2].Blue + " / " + rows[2].Red, Is.EqualTo("3회 / 3회"));
            Assert.That(rows[4].Leader, Is.EqualTo(LabTeam.Blue));
            Assert.That(GameTerritoryResult.Rows(null, 0, 0), Is.Empty);
        }

        [Test]
        public void TerritoryDrawRestoresTheSavedBackgroundAndLeavesEveryTiedRowNeutral()
        {
            WithView(GameScenePaths.StageResult, view =>
            {
                Texture savedBackground = view.background.texture;
                var blueWin = new LabSnapshot
                {
                    phase = LabPhase.Victory, teamTerritory = true, teamWinner = LabTeam.Blue,
                    paintCellCount = 200, blueCells = 120, redCells = 40, paintCoverage = .8f
                };
                view.PresentTerritory(LabTeam.Blue, blueWin, GameTerritoryResult.Rows(blueWin, 2, 1), "청팀 결과");
                Assert.That(view.background.texture, Is.SameAs(view.blueTeamBackground));
                Assert.That(view.rows.Find("Award_0/Player").GetComponent<Text>().color,
                    Is.EqualTo(GameStageResultView.BlueTeamInk));

                var draw = new LabSnapshot
                {
                    phase = LabPhase.Victory, teamTerritory = true, teamWinner = LabTeam.None,
                    paintCellCount = 200, blueCells = 50, redCells = 50, paintCoverage = .5f, remaining = 0f,
                    players = new[]
                    {
                        new LabPlayerState { id = 1, seat = 0, throwTotal = 3, hitTotal = 1, stolenCells = 10 },
                        new LabPlayerState { id = 3, seat = 2, throwTotal = 3, hitTotal = 1, stolenCells = 10 }
                    }
                };
                string before = JsonUtility.ToJson(draw);
                var records = GameTerritoryResult.Rows(draw, 1, 1);
                Assert.That(records.Select(row => row.Leader), Is.All.EqualTo(LabTeam.None));
                view.PresentTerritory(LabTeam.None, draw, records, "무승부 결과");

                Assert.That(view.background.texture, Is.SameAs(savedBackground),
                    "A draw restores the saved background even after a blue victory used the same view.");
                Assert.That(view.stage.text, Is.EqualTo("2 : 2"));
                Assert.That(view.yokai.text, Is.EqualTo("땅따먹기 결과"));
                Assert.That(view.judgment.text, Is.EqualTo("무승부~!"));
                Assert.That(view.paint.text, Is.EqualTo("PAINT 50.0%"));
                Assert.That(view.timeLeft.text, Is.EqualTo("TIME LEFT 0s"));
                Assert.That(view.rows.Cast<Transform>().Count(child => child.gameObject.activeSelf), Is.EqualTo(5));
                for (int i = 0; i < records.Count; i++)
                {
                    Transform row = view.rows.Find("Award_" + i);
                    Assert.That(row.Find("Title").GetComponent<Text>().text, Is.EqualTo(records[i].Title));
                    Assert.That(row.Find("Player").GetComponent<Text>().text, Is.EqualTo(records[i].Blue));
                    Assert.That(row.Find("Record").GetComponent<Text>().text, Is.EqualTo(records[i].Red));
                    Assert.That(row.Find("Player").GetComponent<Text>().color, Is.EqualTo(view.secondaryInk));
                    Assert.That(row.Find("Record").GetComponent<Text>().color, Is.EqualTo(view.secondaryInk));
                    Assert.That(row.Find("Highlight").gameObject.activeSelf, Is.False);
                }
                Assert.That(JsonUtility.ToJson(draw), Is.EqualTo(before), "Presentation must not change the tied result.");
            });
        }

        [Test]
        public void StoryPresentationRestoresItsBackgroundHeadersAndRowInkAfterATerritoryResult()
        {
            WithView(GameScenePaths.StageResult, view =>
            {
                Texture savedBackground = view.background.texture;
                string[] headerNames = { "TitleHeader", "PlayerHeader", "RecordHeader" };
                Text[] headers = headerNames.Select(name => view.design.Find("AwardTable/" + name).GetComponent<Text>()).ToArray();
                string[] savedLabels = headers.Select(header => header.text).ToArray();
                Color[] savedColors = headers.Select(header => header.color).ToArray();
                var territory = new LabSnapshot
                {
                    phase = LabPhase.Victory, teamTerritory = true, teamWinner = LabTeam.Blue,
                    paintCellCount = 200, blueCells = 120, redCells = 40, paintCoverage = .8f
                };
                view.PresentTerritory(LabTeam.Blue, territory, GameTerritoryResult.Rows(territory, 2, 1), "청팀 결과");
                Assert.That(view.background.texture, Is.SameAs(view.blueTeamBackground));
                Assert.That(headers.Select(header => header.text), Is.EqualTo(new[] { "항목", "청팀", "홍팀" }));
                Assert.That(view.rows.Find("Award_0/Player").GetComponent<Text>().color,
                    Is.EqualTo(GameStageResultView.BlueTeamInk));

                var story = new LabSnapshot { phase = LabPhase.Victory, paintCoverage = .823f, remaining = 15.2f };
                string before = JsonUtility.ToJson(story);
                var awards = new[]
                {
                    new LabResultRow("꾸준한 명중", "P2", "3회", false),
                    new LabResultRow("조합의 달인", "P1(나)", "5회", true)
                };
                view.Present(true, 2, "야광귀", story, awards, "다음 스테이지 안내");

                Assert.That(view.background.texture, Is.SameAs(savedBackground));
                Assert.That(headers.Select(header => header.text), Is.EqualTo(savedLabels));
                Assert.That(headers.Select(header => header.color), Is.EqualTo(savedColors));
                Assert.That(view.stage.text, Is.EqualTo("STAGE 2"));
                Assert.That(view.yokai.text, Is.EqualTo("야광귀"));
                Assert.That(view.judgment.text, Is.EqualTo("봉인 성공 !"));
                Assert.That(view.paint.text, Is.EqualTo("PAINT 82.3%"));
                Assert.That(view.timeLeft.text, Is.EqualTo("TIME LEFT 16s"));
                Assert.That(view.guidance.text, Is.EqualTo("다음 스테이지 안내"));
                Assert.That(view.viewerButton.GetComponentInChildren<Text>(true).text, Is.EqualTo("야광귀 봉인 박 확인"));
                Assert.That(view.rows.Cast<Transform>().Count(child => child.gameObject.activeSelf), Is.EqualTo(2));
                for (int i = 0; i < awards.Length; i++)
                {
                    Transform row = view.rows.Find("Award_" + i);
                    Assert.That(row.Find("Title").GetComponent<Text>().text, Is.EqualTo(awards[i].Title));
                    Assert.That(row.Find("Player").GetComponent<Text>().text, Is.EqualTo(awards[i].Who));
                    Assert.That(row.Find("Record").GetComponent<Text>().text, Is.EqualTo(awards[i].Record));
                    Assert.That(row.Find("Highlight").gameObject.activeSelf, Is.EqualTo(awards[i].Mine));
                    Color expected = awards[i].Mine ? Color.white : view.secondaryInk;
                    foreach (string column in new[] { "Title", "Player", "Record" })
                        Assert.That(row.Find(column).GetComponent<Text>().color, Is.EqualTo(expected));
                }
                Assert.That(JsonUtility.ToJson(story), Is.EqualTo(before), "Presentation must not change the Story result.");
            });
        }

        [Test]
        public void LongTiedAwardsCanScrollAndReturnToAnEmptyResultWithoutStaleRows()
        {
            WithView(GameScenePaths.StageResult, view =>
            {
                var awards = Enumerable.Range(0, 7).Select(i => new LabResultRow("칭호 " + i,
                    "P1(나), P2, P3, P4, P5", "12345회", i == 0)).ToArray();
                view.SetRows(awards);
                Assert.That(view.scroll.vertical, Is.True);
                Assert.That(view.rows.rect.height, Is.EqualTo(280));
                Assert.That(view.rows.Cast<Transform>().Count(child => child.gameObject.activeSelf), Is.EqualTo(7));
                Assert.That(view.rows.Find("Award_0/Highlight").gameObject.activeSelf, Is.True);
                Assert.That(view.rows.Find("Award_1/Highlight").gameObject.activeSelf, Is.False);
                view.SetRows(new LabResultRow[0]);
                Assert.That(view.emptyLabel.gameObject.activeSelf, Is.True);
                Assert.That(view.scroll.vertical, Is.False);
                Assert.That(view.rows.Cast<Transform>().Any(child => child.gameObject.activeSelf), Is.False);
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PresentationUsesSnapshotValuesWithoutChangingTheAuthoritativeResult(bool victory)
        {
            WithView(victory ? GameScenePaths.StageResult : GameScenePaths.GameOver, view =>
            {
                var snapshot = new LabSnapshot { paintCoverage = .823f, remaining = 15.2f };
                string before = JsonUtility.ToJson(snapshot);
                view.Present(victory, 2, "야광귀", snapshot, new LabResultRow[0], "안내");
                Assert.That(view.stage.text, Is.EqualTo("STAGE 2"));
                Assert.That(view.yokai.text, Is.EqualTo("야광귀"));
                Assert.That(view.judgment.text, Is.EqualTo(victory ? "봉인 성공 !" : "봉인 실패..."));
                Assert.That(view.paint.text, Is.EqualTo("PAINT 82.3%"));
                Assert.That(view.timeLeft.text, Is.EqualTo("TIME LEFT 16s"));
                Assert.That(JsonUtility.ToJson(snapshot), Is.EqualTo(before));
            });
        }

        private static void WithView(string path, System.Action<GameStageResultView> action)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var view = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameStageResultView>(true)).Single();
                action(view);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
