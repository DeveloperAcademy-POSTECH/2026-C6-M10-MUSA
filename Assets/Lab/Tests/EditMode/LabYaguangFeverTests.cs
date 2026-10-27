using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>
    /// 야광귀 피버타임 (#343): 시간표의 방해가 끝나는 때(기본 100초)부터 전투가 끝날 때까지 스태미나 자동 회복이
    /// Inspector 배율(기본 2배)만큼 빨라진다. 다른 스테이지와 Lab 단판은 그대로다.
    /// </summary>
    public sealed class LabYaguangFeverTests
    {
        private static readonly ulong[] Players = { 0ul, 20ul };
        private LabConfig config;

        [SetUp]
        public void Create()
        {
            config = ScriptableObject.CreateInstance<LabConfig>();
            float[] values = config.CaptureDeveloperValues();
            // 큰 스태미나 통, 0에서 시작, 초당 1 회복. 섬광·콤보·공용 방해는 꺼서 스태미나만 본다.
            values[2] = 120f; values[3] = 1000f; values[4] = 0f;
            values[5] = 1f; values[6] = 1f; values[7] = 1f;
            values[155] = values[184] = 0f;
            values[207] = values[208] = 0f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(config.YaguangTimelineSabotageEndSeconds, Is.EqualTo(100f), "The timetable's sabotage ends at 100 s.");
            Assert.That(config.YaguangFeverStaminaMultiplier, Is.EqualTo(2f), "Fever doubles recovery by default.");
        }

        [TearDown] public void Dispose() => Object.DestroyImmediate(config);

        private LabModel Room(LabStageYokai stage)
        {
            var room = new LabModel();
            room.Begin("yaguang-fever", Players, 0d, config, stage);
            return room;
        }

        [Test]
        public void YaguangRecoveryDoublesFromTheEndOfTheSabotage()
        {
            LabModel room = Room(LabStageYokai.Yaguang);
            Assert.That(room.YaguangFeverStartsAt, Is.EqualTo(100d));
            Assert.That(room.Snapshot(99.5d).players[0].stamina, Is.EqualTo(99.5f).Within(.0001f));
            Assert.That(room.Snapshot(100d).players[0].stamina, Is.EqualTo(100f).Within(.0001f));
            Assert.That(room.Snapshot(100.5d).players[0].stamina, Is.EqualTo(101f).Within(.0001f), "Twice as fast in the fever.");
            Assert.That(room.Snapshot(101d).players[0].stamina, Is.EqualTo(102f).Within(.0001f));
            Assert.That(room.YaguangFeverRecoveryMultiplier(99.99d), Is.EqualTo(1f));
            Assert.That(room.YaguangFeverRecoveryMultiplier(100d), Is.EqualTo(2f));
        }

        [TestCase(LabStageYokai.None)]
        [TestCase(LabStageYokai.Gumiho)]
        public void OtherStagesKeepTheirRecovery(LabStageYokai stage)
        {
            LabModel room = Room(stage);
            Assert.That(room.YaguangFeverStartsAt, Is.EqualTo(-1d));
            Assert.That(room.Snapshot(101d).players[0].stamina, Is.EqualTo(101f).Within(.0001f));
            Assert.That(room.YaguangFeverRecoveryMultiplier(110d), Is.EqualTo(1f));
        }

        [Test]
        public void TheInspectorMultiplierIsClampedAndOneMeansNoFever()
        {
            JsonUtility.FromJsonOverwrite("{\"yaguangFeverStaminaMultiplier\":9}", config);
            Assert.That(config.YaguangFeverStaminaMultiplier, Is.EqualTo(4f));
            JsonUtility.FromJsonOverwrite("{\"yaguangFeverStaminaMultiplier\":1}", config);
            LabModel room = Room(LabStageYokai.Yaguang);
            Assert.That(room.Snapshot(101d).players[0].stamina, Is.EqualTo(101f).Within(.0001f));
        }

        [Test]
        public void TheFeverSongIsInResources()
        {
            Assert.That(Resources.LoadAll<AudioClip>(LabBattleBgm.FeverClipFolder), Has.Length.EqualTo(1),
                "Resources/Audio/Fever/Fever_Whimori.wav (#343)");
            Assert.That(Resources.LoadAll<AudioClip>(LabBattleBgm.ClipFolder), Has.Length.EqualTo(1),
                "The battle BGM folder still holds only the battle song; the fever song lives in its own folder.");
        }
    }
}
