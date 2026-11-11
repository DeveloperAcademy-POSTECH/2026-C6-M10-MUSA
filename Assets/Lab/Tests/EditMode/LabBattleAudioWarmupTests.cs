using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>Battle sound data loads behind the preparing overlay, not on its first play (#492).</summary>
    public sealed class LabBattleAudioWarmupTests
    {
        [Test]
        public void EveryCommonEffectFolderOfTheSoundComponentsIsPreloaded()
        {
            var owners = new[] { typeof(LabGenerateSound), typeof(LabCombineSound), typeof(LabThrowSound), typeof(LabFlashRewardSound),
                typeof(LabComboClearSound), typeof(LabComboHitSound), typeof(LabBombSound), typeof(LabFuseSound), typeof(LabStaminaShortSound) };
            foreach (System.Type owner in owners)
                foreach (FieldInfo field in owner.GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(field => field.IsLiteral && field.Name.EndsWith("Folder")))
                {
                    string folder = (string)field.GetRawConstantValue();
                    if (owner == typeof(LabBombSound) && field.Name == nameof(LabBombSound.ClipFolder)) continue; // parent of Boom/Ppeong
                    Assert.That(LabBattleAudioWarmup.CommonFolders, Does.Contain(folder), owner.Name + "." + field.Name);
                }
            // Some folders wait for their sounds (FlashClear is empty today); an empty folder preloads nothing.
            Assert.That(LabBattleAudioWarmup.CommonFolders.Count(folder => Resources.LoadAll<AudioClip>(folder).Length > 0),
                Is.GreaterThan(LabBattleAudioWarmup.CommonFolders.Length / 2));
        }

        [Test]
        public void EachStageLoadsItsOwnEffectsAndFeverAndReleasesTheOthers()
        {
            Assert.That(LabBattleAudioWarmup.StageFolders(LabStageYokai.Gumiho), Does.Contain(LabBattleBgm.GumihoFeverClipFolder));
            Assert.That(LabBattleAudioWarmup.StageFolders(LabStageYokai.Yaguang), Does.Contain(LabBattleBgm.FeverClipFolder));
            Assert.That(LabBattleAudioWarmup.StageFolders(LabStageYokai.None), Is.Empty);
            Assert.That(LabBattleAudioWarmup.StageFolders(LabStageYokai.Jigwi), Does.Not.Contain(LabBattleBgm.FeverClipFolder),
                "지귀 never plays a fever song.");

            LabBattleAudioWarmup.Preload(LabStageYokai.Yaguang);
            AssertLoaded(LabStageYokai.Yaguang, true);
            Assert.That(LabBattleAudioWarmup.RetainedRewardFire, Is.Null);

            LabBattleAudioWarmup.Preload(LabStageYokai.Gumiho);
            AssertLoaded(LabStageYokai.Gumiho, true);
            Assert.That(LabBattleAudioWarmup.StageFolders(LabStageYokai.Gumiho), Is.SupersetOf(LabYaguangSound.FoxRoundClipFolders),
                "#664: the fox round ticks and goes BIG/SMALL with the 야광귀 combo's sounds.");
            Assert.That(LabYaguangSound.ClipFolders.Except(LabYaguangSound.FoxRoundClipFolders).SelectMany(Resources.LoadAll<AudioClip>)
                .All(clip => clip.loadState != AudioDataLoadState.Loaded), Is.True, "Another stage's effects are released.");

            LabBattleAudioWarmup.Preload(LabStageYokai.Jigwi);
            AssertLoaded(LabStageYokai.Jigwi, true);
            foreach (string resource in LabBattleAudioWarmup.StageClips(LabStageYokai.Jigwi))
                Assert.That(Resources.Load<AudioClip>(resource).loadState, Is.EqualTo(AudioDataLoadState.Loaded), resource);
            Assert.That(LabBattleAudioWarmup.RetainedRewardFire, Is.InstanceOf<LabJigwiRewardFireProfile>(),
                "지귀 keeps its reward fire art loaded before the first personal fire.");
            Assert.That(LabBattleAudioWarmup.Preload(LabStageYokai.Jigwi), Is.Zero, "A second preload of the same stage loads nothing new.");
            foreach (string resource in LabBattleAudioWarmup.StageClips(LabStageYokai.Jigwi))
                Assert.That(LabBattleAudioWarmup.RetainedClips, Does.Contain(Resources.Load<AudioClip>(resource)),
                    "The pattern clips stay referenced through the scene load's unused-asset sweep.");
            var owner = new object();
            LabBattleAudioWarmup.Preload(LabStageYokai.Jigwi, owner);
            LabBattleAudioWarmup.Release(new object());
            Assert.That(LabBattleAudioWarmup.RetainedClips, Is.Not.Empty, "A closing Battle never drops another Battle's preload.");
            LabBattleAudioWarmup.Release(owner);
            Assert.That(LabBattleAudioWarmup.RetainedRewardFire, Is.Null);
            Assert.That(LabBattleAudioWarmup.RetainedClips, Is.Empty);
        }

        private static void AssertLoaded(LabStageYokai yokai, bool loaded)
        {
            foreach (AudioClip clip in LabBattleAudioWarmup.CommonFolders.Concat(LabBattleAudioWarmup.StageFolders(yokai))
                .SelectMany(Resources.LoadAll<AudioClip>).Where(clip => clip.loadType != AudioClipLoadType.Streaming))
                Assert.That(clip.loadState == AudioDataLoadState.Loaded, Is.EqualTo(loaded), yokai + ": " + clip.name);
        }
    }
}
