using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// Loads the battle's sound data while the stage is still behind the preparing overlay (#492). Every effect clip is
    /// Decompress On Load without preload, so its first PlayOneShot decoded it on that frame: the first tap, hit, bomb
    /// or pattern cue. The same clip objects the sound components loaded are used; streamed songs are left alone.
    /// </summary>
    public static class LabBattleAudioWarmup
    {
        /// <summary>Effects every stage can play. Kept in step with the sound components' own folder constants.</summary>
        public static readonly string[] CommonFolders =
        {
            LabGenerateSound.ClipFolder, LabGenerateSound.VoiceFolder, LabCombineSound.ClipFolder,
            LabThrowSound.HitFolder, LabThrowSound.CrackHitFolder, LabThrowSound.GlanceHitFolder, LabThrowSound.MissFolder,
            LabFlashRewardSound.FlashClearFolder, LabFlashRewardSound.FiveColorHitFolder,
            LabComboClearSound.ClipFolder, LabComboClearSound.FailClipFolder, LabComboHitSound.ClipFolder,
            LabBombSound.BoomFolder, LabBombSound.PpeongFolder, LabFuseSound.ClipFolder, LabStaminaShortSound.ClipFolder,
        };

        private static readonly string[] JigwiClips =
        {
            // #509: the 지귀 A/B shout clips were removed. #602: the old B fire loop (GroupFireLoop) was removed too,
            // because its licence could not be confirmed; the old fire-curtain B is silent and the smoke B is unchanged.
        };

        private static Object retainedRewardFire;
        // The scene load's unused-asset sweep frees a clip nothing references, with its decoded data: hold the stage's
        // set until the Battle closes (지귀 pattern clips are otherwise only loaded at their first cue).
        private static AudioClip[] retainedClips = System.Array.Empty<AudioClip>();
        private static object retainedOwner;

        /// <summary>The 지귀 reward fire art kept loaded for the current stage, or null on other stages.</summary>
        public static Object RetainedRewardFire => retainedRewardFire;

        /// <summary>The clips held loaded for the current Battle.</summary>
        public static IReadOnlyList<AudioClip> RetainedClips => retainedClips;

        /// <summary>
        /// Lets the next scene load free this stage's art and sound data. Only the owner that preloaded them releases
        /// them, so a closing Battle never drops what the next one already preloaded.
        /// </summary>
        public static void Release(object owner = null)
        {
            if (owner != null && !ReferenceEquals(owner, retainedOwner)) return;
            retainedClips = System.Array.Empty<AudioClip>();
            retainedRewardFire = null;
            retainedOwner = null;
        }

        /// <summary>Folders only this stage plays, including the fever song it starts mid-way.</summary>
        public static IReadOnlyList<string> StageFolders(LabStageYokai yokai)
        {
            switch (yokai)
            {
                // #664: the fox round ticks and goes BIG/SMALL with the 야광귀 combo's sounds.
                case LabStageYokai.Gumiho: return LabGumihoSound.ClipFolders.Concat(LabYaguangSound.FoxRoundClipFolders)
                    .Append(LabBattleBgm.GumihoFeverClipFolder).ToArray();
                case LabStageYokai.Yaguang: return LabYaguangSound.ClipFolders.Append(LabBattleBgm.FeverClipFolder).ToArray();
                case LabStageYokai.Jigwi: return LabJigwiSootSound.ClipFolders; // 지귀 has no fever song; #713 soot sounds.
                default: return System.Array.Empty<string>();
            }
        }

        /// <summary>Single clips only this stage plays.</summary>
        public static IReadOnlyList<string> StageClips(LabStageYokai yokai) =>
            yokai == LabStageYokai.Jigwi ? JigwiClips : System.Array.Empty<string>();

        /// <summary>
        /// Loads common and current-stage sound data and the 지귀 reward fire art, and releases the sound data of the other
        /// stages. Synchronous by design: call it only behind the opaque preparing overlay. Returns the clips loaded now.
        /// </summary>
        public static int Preload(LabStageYokai yokai, object owner = null)
        {
            retainedOwner = owner;
            var wanted = new HashSet<AudioClip>(CommonFolders.Concat(StageFolders(yokai)).SelectMany(Resources.LoadAll<AudioClip>)
                .Concat(StageClips(yokai).Select(Resources.Load<AudioClip>)).Where(clip => clip != null));
            foreach (LabStageYokai other in new[] { LabStageYokai.Gumiho, LabStageYokai.Yaguang, LabStageYokai.Jigwi })
            {
                if (other == yokai) continue;
                foreach (AudioClip clip in StageFolders(other).SelectMany(Resources.LoadAll<AudioClip>)
                    .Concat(StageClips(other).Select(Resources.Load<AudioClip>)))
                    if (clip != null && !wanted.Contains(clip) && clip.loadType != AudioClipLoadType.Streaming
                        && clip.loadState == AudioDataLoadState.Loaded) clip.UnloadAudioData();
            }
            retainedClips = wanted.ToArray();
            int loaded = 0;
            foreach (AudioClip clip in wanted)
                if (clip.loadType != AudioClipLoadType.Streaming && clip.loadState != AudioDataLoadState.Loaded && clip.LoadAudioData())
                    loaded++;
            // The first personal fire at 5 s loaded about 19 MiB of art on its frame; keep it for the stage instead.
            retainedRewardFire = yokai == LabStageYokai.Jigwi
                ? Resources.Load<LabJigwiRewardFireProfile>(LabJigwiRewardFireProfile.ResourcesPath) : null;
            return loaded;
        }
    }
}
