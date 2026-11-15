using NUnit.Framework;
using UnityEngine;

namespace Hapio.Tests
{
    /// <summary>#745 스토리 결과 징글과 진동.</summary>
    public sealed class GameResultJingleTests
    {
        [Test]
        public void BothJinglesAreInResourcesAndShort()
        {
            foreach (string path in new[] { GameResultJingle.VictoryPath, GameResultJingle.DefeatPath })
            {
                var clip = Resources.Load<AudioClip>(path);
                Assert.That(clip, Is.Not.Null, path);
                Assert.That(clip.length, Is.InRange(2f, 6f), path);
            }
        }

        [Test]
        public void TheBeatsRunInOrderAndEndWithinTheJingle()
        {
            foreach (var beats in new[] { GameResultJingle.VictoryBeats, GameResultJingle.DefeatBeats })
            {
                Assert.That(beats, Is.Not.Empty);
                for (int i = 0; i < beats.Length; i++)
                {
                    Assert.That(beats[i].intensity, Is.InRange(0f, 1f));
                    Assert.That(beats[i].at + beats[i].continuousSeconds, Is.LessThanOrEqualTo(5f));
                    if (i > 0) Assert.That(beats[i].at, Is.GreaterThanOrEqualTo(beats[i - 1].at));
                }
            }
        }
    }
}
