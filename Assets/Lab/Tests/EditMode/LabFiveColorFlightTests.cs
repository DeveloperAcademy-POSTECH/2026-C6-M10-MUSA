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
                // Linear 색 공간에서는 MaterialPropertyBlock에 넣었다 꺼낸 색이 아주 조금(약 3e-8) 달라질 수 있다.
                // 정확히 같은지 대신 오차 안에서 비교한다 (구슬마다 시작 색이 달라 실행 순서에 따라 흔들리지 않게).
                Color expected = look.BodyColor;
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
