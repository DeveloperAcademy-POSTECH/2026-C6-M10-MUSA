using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>
    /// 세 스테이지 피버 통일 (#441): 시작 시각은 스테이지마다 그대로(구미호 남은 20초, 야광귀/지귀 100초)이고,
    /// 그때부터 북에서 나오는 것은 모두 오색구슬이다. 모든 기기는 스냅샷만 보고 같은 시각에 피버를 알아본다.
    /// </summary>
    public sealed class LabStageFeverTests
    {
        private static readonly ulong[] Players = { 10ul, 20ul };
        private LabConfig config;
        private int serial;

        [SetUp]
        public void Create()
        {
            config = ScriptableObject.CreateInstance<LabConfig>();
            float[] values = config.CaptureDeveloperValues();
            values[2] = 120f;                  // 2분 전투 → 구미호 피버는 100초부터
            values[3] = values[4] = 1000f;     // 북을 여러 번 누를 만큼 큰 스태미나
            values[5] = 1f;
            values[74] = LabModel.MaximumParticipants; // 모두에게 북
            values[205] = values[206] = 0f;    // 공용/고유 방해 끄기
            values[155] = values[184] = values[207] = values[208] = 0f; // 섬광/콤보 끄기
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
        }

        [TearDown] public void Dispose() => Object.DestroyImmediate(config);

        private LabModel Room(LabStageYokai stage)
        {
            var model = new LabModel();
            model.Begin("stage-fever-" + stage, Players, 0d, config, stage);
            return model;
        }

        [TestCase(LabStageYokai.Gumiho)]
        [TestCase(LabStageYokai.Yaguang)]
        [TestCase(LabStageYokai.Jigwi)]
        public void EachStageKeepsItsStartAndItsDrumGivesFiveColorOrbsFromThen(LabStageYokai stage)
        {
            LabModel room = Room(stage);
            Assert.That(room.StageFeverStartsAt, Is.EqualTo(100d), "Gumiho: 120 s - 20 s. Yaguang/Jigwi: end of the sabotage.");
            Assert.That(room.StageFeverOn(99.9d), Is.False);
            Assert.That(room.StageFeverOn(100d), Is.True);

            Assert.That(room.TryGenerate(10ul, "before-" + ++serial, 60d, out LabOrbState before, out string reason), Is.True, reason);
            Assert.That(before.fiveColor, Is.False, "Before the fever the drum gives its usual orbs.");
            for (int i = 0; i < 3; i++)
            {
                Assert.That(room.TryGenerate(10ul, "fever-" + ++serial, 101d + i, out LabOrbState orb, out reason), Is.True, reason);
                Assert.That(orb.fiveColor, Is.True, stage + " fever gives a five-color orb.");
                Assert.That(orb.kind, Is.EqualTo(LabOrbKind.Combined), "It can be thrown at once.");
                Assert.That(orb.lockSwipes, Is.Zero);
            }
        }

        [Test]
        public void TheLabSingleBattleHasNoStageFever()
        {
            LabModel room = Room(LabStageYokai.None);
            Assert.That(room.StageFeverStartsAt, Is.EqualTo(-1d));
            Assert.That(room.TryGenerate(10ul, "late-" + ++serial, 110d, out LabOrbState orb, out string reason), Is.True, reason);
            Assert.That(orb.fiveColor, Is.False);
        }

        [Test]
        public void EveryScreenReadsTheSameStartFromTheSnapshot()
        {
            foreach (LabStageYokai stage in new[] { LabStageYokai.Gumiho, LabStageYokai.Jigwi })
            {
                LabModel room = Room(stage);
                room.Tick(99.9d);
                Assert.That(LabNetwork.StageFeverElapsed(room.Snapshot(99.9d), stage, -1d), Is.EqualTo(-1d), stage + " not yet.");
                room.Tick(103d);
                Assert.That(LabNetwork.StageFeverElapsed(room.Snapshot(103d), stage, -1d), Is.EqualTo(3d).Within(1e-6),
                    stage + " three seconds into the fever.");
            }
            var snapshot = new LabSnapshot { phase = LabPhase.Playing, serverTime = 50d };
            Assert.That(LabNetwork.StageFeverElapsed(snapshot, LabStageYokai.Yaguang, 4d), Is.EqualTo(4d),
                "The yaguang keeps its own count (playedSeconds).");
            snapshot.teamTerritory = true;
            Assert.That(LabNetwork.StageFeverElapsed(snapshot, LabStageYokai.Yaguang, 4d), Is.EqualTo(-1d), "No stage fever in 2:2.");
            Assert.That(LabNetwork.StageFeverElapsed(new LabSnapshot { phase = LabPhase.Playing, serverTime = 200d },
                LabStageYokai.None, -1d), Is.EqualTo(-1d));
        }
    }
}
