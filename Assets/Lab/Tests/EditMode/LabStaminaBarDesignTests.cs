using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace C6Lab.Tests
{
    public sealed class LabStaminaBarDesignTests
    {
        [TestCase(0f)]
        [TestCase(.5f)]
        [TestCase(.8f)]
        [TestCase(1f)]
        public void SavedBarRendersTheRequestedColoursAcrossEmptyPartialAndFullStamina(float amount)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Lab/Scenes/Lab.unity", OpenSceneMode.Additive);
            try
            {
                var hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LabHud>(true)).Single();
                var fill = hud.staminaFill as LabShapeFillImage;
                Assert.That(fill, Is.Not.Null);
                var bar = (RectTransform)fill.transform.parent;
                Assert.That(bar.sizeDelta, Is.EqualTo(new Vector2(223, 44)));
                Assert.That(bar.anchorMin.x, Is.EqualTo(229f / 402f).Within(.00001f));
                Assert.That(bar.anchorMin.y, Is.EqualTo(1f - 832.5f / 874f).Within(.00001f));
                Assert.That(fill.sprite.texture.width, Is.EqualTo(374));
                Assert.That(fill.sprite.texture.height, Is.EqualTo(50));
                var frame = bar.Find("StaminaFrame").GetComponent<Image>();
                Assert.That(frame.gameObject.activeSelf, Is.True);
                Assert.That(frame.sprite, Is.Not.Null);
                Assert.That(frame.raycastTarget || fill.raycastTarget, Is.False);
                fill.fillAmount = amount;
                Color32 blue = new Color32(0x2F, 0x62, 0xA2, 255);
                Color32 pale = new Color32(0xC1, 0xD2, 0xE0, 255);
                using (var mesh = new VertexHelper())
                {
                    typeof(LabShapeFillImage).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null)
                        .Invoke(fill, new object[] { mesh });
                    Assert.That(mesh.currentVertCount, Is.EqualTo(amount == 0 || amount == 1 ? 4 : 8));
                    var vertex = new UIVertex();
                    for (int i = 0; i < mesh.currentVertCount; i++)
                    {
                        mesh.PopulateUIVertex(ref vertex, i);
                        Assert.That(vertex.color, Is.EqualTo(amount > 0 && i < 4 ? blue : pale));
                    }
                    if (amount > 0 && amount < 1)
                    {
                        mesh.PopulateUIVertex(ref vertex, 2);
                        Assert.That(vertex.position.x, Is.EqualTo(Mathf.Lerp(fill.rectTransform.rect.xMin,
                            fill.rectTransform.rect.xMax, amount)).Within(.001f));
                    }
                }
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
