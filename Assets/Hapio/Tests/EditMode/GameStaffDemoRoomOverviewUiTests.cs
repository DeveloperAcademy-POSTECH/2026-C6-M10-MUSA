using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Hapio.Tests
{
    public sealed class GameStaffDemoRoomOverviewUiTests
    {
        [Test]
        public void SavedSelectorKeepsItsGroupCardsAndAnIndependentFullScreenCreationConfirmation()
        {
            var prefab = Resources.Load<GameObject>(GameStaffDemoView.ResourcePath);
            Assert.That(prefab, Is.Not.Null);
            var view = prefab.GetComponent<GameStaffDemoView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(view.groupButtons, Has.Length.EqualTo(2));
            Assert.That(view.groupButtons.Distinct().Count(), Is.EqualTo(2));
            Assert.That(view.creationConfirmation, Is.Not.Null);
            Assert.That(view.creationConfirmation.activeSelf, Is.False,
                "Merely opening Home must not pre-confirm or present a stale creation request.");
            Assert.That(view.creationConfirmation.transform.parent, Is.EqualTo(view.transform),
                "The modal mask must cover Home outside its safe-area card too.");
            var mask = view.creationConfirmation.GetComponent<UnityEngine.UI.Image>();
            Assert.That(mask, Is.Not.Null);
            Assert.That(mask.raycastTarget, Is.True);
            var rect = view.creationConfirmation.GetComponent<RectTransform>();
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(rect.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.offsetMax, Is.EqualTo(Vector2.zero));
            Assert.That(view.creationConfirmation.GetComponentInChildren<GameSafeArea>(true), Is.Not.Null);
            Assert.That(view.creationConfirmationMessage, Is.Not.Null);
            Assert.That(view.creationConfirmationMessage.transform.IsChildOf(view.creationConfirmation.transform), Is.True);
            Assert.That(view.creationConfirmationMessage.raycastTarget, Is.False);
            Assert.That(view.confirmCreationButton, Is.Not.Null);
            Assert.That(view.cancelCreationButton, Is.Not.Null.And.Not.SameAs(view.confirmCreationButton));
            foreach (var button in new[] { view.confirmCreationButton, view.cancelCreationButton })
            {
                Assert.That(button.transform.IsChildOf(view.creationConfirmation.transform), Is.True);
                Assert.That(button.targetGraphic, Is.Not.Null);
                Assert.That(button.targetGraphic.raycastTarget, Is.True);
                Assert.That(view.groupButtons.Contains(button), Is.False);
                Assert.That(view.stageButtons.Contains(button), Is.False);
            }
            Assert.That(prefab.GetComponentInChildren<Canvas>(true), Is.Null,
                "The saved panel reuses the existing Home Canvas/Scaler/Raycaster.");
        }
    }
}
