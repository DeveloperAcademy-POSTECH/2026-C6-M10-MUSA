using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>
    /// 야광귀 피버 스태미나 영롱 효과 (#343): 칸마다 겹친 그림이 제 칸까지만 채우고, 옆 칸과 색이 어긋나 물결이 흐르며,
    /// 시작 순간에만 한 번 번쩍인다.
    /// </summary>
    public sealed class LabYaguangFeverStaminaGlowTests
    {
        [Test]
        public void EachLayerFillsUpToItsOwnCellAndNoFurtherThanTheBar()
        {
            const int cells = 6;
            // 막대가 3.5칸 찼을 때: 0~2번 겹은 제 칸 끝까지, 3번부터는 막대가 찬 데까지만.
            float fraction = 3.5f / cells;
            Assert.That(LabYaguangFeverStaminaGlow.CellFill(fraction, 0, cells), Is.EqualTo(1f / 6f).Within(1e-5f));
            Assert.That(LabYaguangFeverStaminaGlow.CellFill(fraction, 2, cells), Is.EqualTo(3f / 6f).Within(1e-5f));
            for (int k = 3; k < cells; k++)
                Assert.That(LabYaguangFeverStaminaGlow.CellFill(fraction, k, cells), Is.EqualTo(fraction).Within(1e-5f));
            Assert.That(LabYaguangFeverStaminaGlow.CellFill(1f, cells - 1, cells), Is.EqualTo(1f));
            Assert.That(LabYaguangFeverStaminaGlow.CellFill(0f, 0, cells), Is.Zero);
            Assert.That(LabYaguangFeverStaminaGlow.CellFill(2f, 0, cells), Is.EqualTo(1f / 6f).Within(1e-5f));
        }

        [Test]
        public void NeighbouringCellsShowDifferentColoursThatKeepChanging()
        {
            Color first = LabYaguangFeverStaminaGlow.CellColor(1f, 0, .45f, .5f);
            Color next = LabYaguangFeverStaminaGlow.CellColor(1f, 1, .45f, .5f);
            Color later = LabYaguangFeverStaminaGlow.CellColor(1.3f, 0, .45f, .5f);
            Assert.That(first, Is.Not.EqualTo(next), "Each cell is a little behind the one on its left.");
            Assert.That(first, Is.Not.EqualTo(later), "The colours keep flowing.");
            Assert.That(LabYaguangFeverStaminaGlow.CellColor(1f, 0, .45f, 0f), Is.EqualTo(LabYaguangFeverStaminaGlow.CellColor(1f, 3, .45f, 0f)),
                "With no offset every cell is the same colour.");
        }

        [Test]
        public void TheStartBurstRisesQuicklyAndFadesOnlyOnce()
        {
            Assert.That(LabYaguangFeverStaminaGlow.BurstAmount(0f, .4f), Is.Zero);
            Assert.That(LabYaguangFeverStaminaGlow.BurstAmount(.1f, .4f), Is.EqualTo(1f).Within(1e-4f), "Full at a quarter of the burst.");
            Assert.That(LabYaguangFeverStaminaGlow.BurstAmount(.25f, .4f), Is.InRange(0f, 1f));
            Assert.That(LabYaguangFeverStaminaGlow.BurstAmount(.4f, .4f), Is.Zero, "Over after the burst.");
            Assert.That(LabYaguangFeverStaminaGlow.BurstAmount(5f, .4f), Is.Zero);
            Assert.That(LabYaguangFeverStaminaGlow.BurstAmount(.1f, 0f), Is.Zero, "No burst when it is turned off.");
        }
    }
}
