using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>Long stage songs stream; the short fever songs that start mid-song stay decompressed (#489).</summary>
    public sealed class LabBattleBgmImportTests
    {
        [Test]
        public void EveryLongStageSongStreamsAndFeverSongsStayDecompressed()
        {
            foreach (string folder in new[] { LabBattleBgm.ClipFolder, LabBattleBgm.YaguangClipFolder,
                LabBattleBgm.GumihoClipFolder, LabBattleBgm.TerritoryClipFolder, LabBattleBgm.JigwiClipFolder })
            {
                AudioClip[] clips = Resources.LoadAll<AudioClip>(folder);
                Assert.That(clips, Is.Not.Empty, folder);
                foreach (AudioClip clip in clips)
                    Assert.That(clip.loadType, Is.EqualTo(AudioClipLoadType.Streaming),
                        folder + "/" + clip.name + " is not decoded whole on the frame it starts.");
            }
            Assert.That(Resources.LoadAll<AudioClip>(LabBattleBgm.YaguangClipFolder).Select(clip => clip.name),
                Is.EqualTo(new[] { "Yaguang_Stage" }));
            Assert.That(Resources.LoadAll<AudioClip>(LabBattleBgm.ClipFolder).Select(clip => clip.name),
                Is.EqualTo(new[] { "Kkwaenggwari_13" }));
            Assert.That(Resources.LoadAll<AudioClip>(LabBattleBgm.JigwiClipFolder).Select(clip => clip.name),
                Is.EqualTo(new[] { "Jigwi_Stage" }));
            foreach (string folder in new[] { LabBattleBgm.FeverClipFolder, LabBattleBgm.GumihoFeverClipFolder, LabBattleBgm.JigwiFeverClipFolder })
                foreach (AudioClip clip in Resources.LoadAll<AudioClip>(folder))
                    Assert.That(clip.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad),
                        folder + "/" + clip.name + " starts at an elapsed offset; it is preloaded instead of streamed.");
        }
    }
}
