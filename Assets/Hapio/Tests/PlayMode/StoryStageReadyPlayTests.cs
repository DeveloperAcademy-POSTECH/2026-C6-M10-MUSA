using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using C6Lab;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hapio.Tests
{
    public sealed class StoryStageReadyPlayTests
    {
        [UnityTest]
        public IEnumerator MemberCaptionAlternatesAndExistingRunnerAnimatesWhileGameTimeIsStopped()
        {
            var root = new GameObject("Member waiting fixture", typeof(RectTransform));
            float oldScale = Time.timeScale;
            try
            {
                var view = StoryStageReadyView.Create(root.transform);
                view.BeginWaiting(false, "member", 1, new[] { new LabPlayerState { id = 7 } }, 7);
                view.Present(true, false, 0, 1, false);
                Time.timeScale = 0;
                yield return null;
                Assert.That(view.memberCaption.text, Is.EqualTo("방장이 다음 봉인을\n준비하고 있소."));
                var frames = new HashSet<Sprite>();
                double until = Time.realtimeSinceStartupAsDouble + .6;
                while (Time.realtimeSinceStartupAsDouble < until)
                {
                    frames.Add(view.runner.sprite);
                    yield return null;
                }
                Assert.That(frames.Count, Is.GreaterThan(1), "Use the teammate's frame animation rather than a still loading image.");
                typeof(StoryStageReadyView).GetField("began", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(view, Time.unscaledTime - StoryStageReadyView.CaptionSeconds - .1f);
                yield return null;
                Assert.That(view.memberCaption.text, Is.EqualTo("모두가 준비되면\n함께 이동하오."));
                Assert.That(view.start.gameObject.activeSelf, Is.False, "Only the host starts the next stage.");
            }
            finally { Time.timeScale = oldScale; Object.Destroy(root); }
            yield return null;
        }
    }
}
