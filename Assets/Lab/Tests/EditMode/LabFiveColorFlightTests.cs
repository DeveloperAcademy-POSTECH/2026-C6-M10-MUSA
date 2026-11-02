using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>
    /// 3D로 날아가는 오색구슬 (#426): 납작한 그림을 얹지 않고 구슬 3D 공 자체의 색이 다섯 색 사이를 건너간다.
    /// 판 위 오색구슬은 지금처럼 그림을 얹는다.
    /// </summary>
    public sealed class LabFiveColorFlightTests
    {
        [Test]
        public void AFlyingFiveColorOrbIsOneColouredBallWithoutFlatArt()
        {
            var root = new GameObject("projectile");
            try
            {
                GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ball.name = "Default Sphere";
                ball.transform.SetParent(root.transform, false);
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "Roll Marker";
                marker.transform.SetParent(ball.transform, false);

                LabFiveColorSparkle look = LabFiveColorSparkle.AttachArt(root.transform, .3f, true);
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>(true), Is.Empty, "No flat picture beside the ball.");
                look.Tint();

                var block = new MaterialPropertyBlock();
                ball.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                // 프로젝트가 Linear 색 공간이라 MaterialPropertyBlock.SetColor는 sRGB 색을 linear로 바꿔 담는다.
                Color expected = QualitySettings.activeColorSpace == ColorSpace.Linear ? look.BodyColor.linear : look.BodyColor;
                Color actual = block.GetColor("_BaseColor");
                Assert.That(actual.r, Is.EqualTo(expected.r).Within(1e-3f), "The ball itself takes the five colours.");
                Assert.That(actual.g, Is.EqualTo(expected.g).Within(1e-3f));
                Assert.That(actual.b, Is.EqualTo(expected.b).Within(1e-3f));
                marker.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Assert.That(block.isEmpty, Is.True, "The roll marker keeps its own colour.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ABoardFiveColorOrbStillGetsItsArt()
        {
            var root = new GameObject("board orb");
            try
            {
                LabFiveColorSparkle.AttachArt(root.transform, .3f, false);
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>(true), Is.Not.Empty);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
