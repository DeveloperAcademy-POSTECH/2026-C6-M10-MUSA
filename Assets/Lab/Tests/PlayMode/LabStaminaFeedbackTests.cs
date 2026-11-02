using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>Existing fill/layout is kept while passive overlays reflect approved costs and A's real clock.</summary>
    public sealed class LabStaminaFeedbackTests
    {
        private GameObject root;
        private LabShapeFillImage fill;
        private LabStaminaFeedback feedback;
        private Texture2D texture;
        private Sprite sprite;

        [SetUp]
        public void CreateExistingStaminaWindow()
        {
            root = new GameObject("Stamina feedback fixture", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var hud = root.AddComponent<LabHud>(); hud.enabled = false;
            var bar = new GameObject("Existing Stamina Window", typeof(RectTransform));
            bar.transform.SetParent(root.transform, false);
            var body = new GameObject("Existing Blue Fill", typeof(RectTransform));
            body.transform.SetParent(bar.transform, false);
            fill = body.AddComponent<LabShapeFillImage>();
            fill.rectTransform.sizeDelta = new Vector2(240f, 24f);
            fill.rectTransform.anchoredPosition = new Vector2(11f, 17f);
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.fillAmount = .5f;
            fill.filledColor = new Color(.03f, .36f, .75f);
            fill.emptyColor = new Color(.8f, .7f, .5f);
            texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16]; for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels(pixels); texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f);
            fill.sprite = sprite; hud.staminaFill = fill;
            feedback = root.AddComponent<LabStaminaFeedback>(); feedback.enabled = false;
            feedback.Bind(null, hud); // Only Present advances this fixture's visual clock.
        }
        [TearDown]
        public void DestroyWindow()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (sprite != null) Object.DestroyImmediate(sprite);
            if (texture != null) Object.DestroyImmediate(texture);
        }

        [Test]
        public void ApprovedSpendShowsOnlyCoveredBlueAndNeverChangesTheSavedSourceOrNeighbouringFeverLayer()
        {
            Color blue = fill.filledColor, empty = fill.emptyColor, tint = fill.color;
            Vector2 position = fill.rectTransform.anchoredPosition, size = fill.rectTransform.sizeDelta;
            var fever = new GameObject("Existing Yaguang Fever Colour", typeof(RectTransform)).AddComponent<UnityEngine.UI.Image>();
            fever.transform.SetParent(fill.transform.parent, false); fever.color = Color.green;
            feedback.NotifySpent(20f);
            feedback.Present(State(), 2, 2d, false, 0f, 4f);
            Assert.That(feedback.PulseVisible, Is.True);
            Assert.That(feedback.ConsumptionPulseCount, Is.EqualTo(1));
            Assert.That(feedback.ConsumptionImage.fillAmount, Is.EqualTo(.5f + 20f / 120f).Within(.00001f));
            Assert.That(feedback.ConsumptionImage.sprite, Is.SameAs(sprite), "Reuse the source alpha silhouette, not an opaque rectangle.");
            Assert.That(feedback.ConsumptionImage.type, Is.EqualTo(UnityEngine.UI.Image.Type.Filled));
            Assert.That(fill.fillAmount, Is.EqualTo(.5f));
            Assert.That(fill.filledColor, Is.EqualTo(blue)); Assert.That(fill.emptyColor, Is.EqualTo(empty));
            Assert.That(fill.color, Is.EqualTo(tint));
            Assert.That(fill.rectTransform.anchoredPosition, Is.EqualTo(position));
            Assert.That(fill.rectTransform.sizeDelta, Is.EqualTo(size));
            Assert.That(fever.color, Is.EqualTo(Color.green)); Assert.That(fever.gameObject.activeSelf, Is.True);
            Assert.That(feedback.EffectRoot.parent, Is.SameAs(fill.transform.parent), "Keep all new graphics inside the existing rear HUD subtree.");
            foreach (UnityEngine.UI.Graphic graphic in feedback.EffectRoot.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False, "Decorative feedback cannot consume a drag or tap.");
        }

        [Test]
        public void TheLastCostStillFlashesAndHeldClockFreezesBothThePulseAndAFlames()
        {
            fill.fillAmount = 0f;
            feedback.NotifySpent(20f);
            LabSnapshot snapshot = State(true);
            feedback.Present(snapshot, 2, 5.1d, false, .05f, 4f);
            Assert.That(feedback.PulseVisible, Is.True, "20→0 must remain visible as a short spent-coverage afterimage.");
            Assert.That(feedback.PulseFillFraction, Is.EqualTo(1f / 6f).Within(.00001f));
            Assert.That(fill.fillAmount, Is.Zero, "The afterimage does not restore actual stamina.");
            float opacity = feedback.PulseAlpha, animation = feedback.FireAnimationSeconds;
            feedback.Present(snapshot, 2, 5.1d, true, 1f, 4f);
            Assert.That(feedback.PulseAlpha, Is.EqualTo(opacity));
            Assert.That(feedback.FireAnimationSeconds, Is.EqualTo(animation));
            feedback.Present(snapshot, 2, 5.4d, false, .2f, 4f);
            Assert.That(feedback.PulseVisible, Is.False);
            Assert.That(feedback.FireVisible, Is.True);
            feedback.NotifySpent(0f); feedback.NotifySpent(float.NaN);
            Assert.That(feedback.ConsumptionPulseCount, Is.EqualTo(1));
        }

        [Test]
        public void OnlyTheActiveATargetGetsIgnitionAndItsFireCoverageFollowsTheRealFill()
        {
            var snapshot = State(true);
            feedback.Present(snapshot, 3, 5.1d, false, .2f, 4f);
            Assert.That(feedback.FireVisible, Is.False);
            feedback.Present(snapshot, 2, 4.5d, false, .2f, 4f);
            Assert.That(feedback.FireVisible, Is.False, "The one-second warning must not impersonate active 4x recovery.");
            feedback.Present(snapshot, 2, 5.1d, false, .2f, 4f);
            Assert.That(feedback.FireVisible, Is.True);
            Assert.That(feedback.FireFillFraction, Is.GreaterThan(0f).And.LessThan(fill.fillAmount));
            feedback.Present(snapshot, 2, 5.4d, false, .3f, 4f);
            Assert.That(feedback.FireFillFraction, Is.EqualTo(.5f).Within(.00001f));
            Assert.That(feedback.MultiplierLabel.text, Does.Contain("4"));
            fill.fillAmount = 1f;
            feedback.Present(snapshot, 2, 6d, false, .2f, 4f);
            Assert.That(feedback.FireFillFraction, Is.EqualTo(1f));
            Assert.That(feedback.FireVisible, Is.True, "Full stamina still communicates the active A buff.");
            snapshot.jigwiAB.rounds[0].resolution = LabJigwiABResolution.Completed;
            feedback.Present(snapshot, 2, 6d, false, .1f, 4f);
            Assert.That(feedback.FireVisible, Is.False);
            Assert.That(feedback.MultiplierLabel.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void FirstObservationOfAnAlreadyActiveHeldAStartsAtItsExistingIgnitionPhase()
        {
            LabSnapshot snapshot = State(true);
            feedback.Present(snapshot, 2, 11.2d, true, 0f, 4f);
            Assert.That(feedback.FireVisible, Is.True);
            Assert.That(feedback.FireAnimationSeconds, Is.EqualTo(6.2f).Within(.00001f));
            Assert.That(feedback.FireFillFraction, Is.EqualTo(fill.fillAmount).Within(.00001f),
                "A late paused snapshot must render its existing full fire coverage immediately.");
            float animation = feedback.FireAnimationSeconds;
            feedback.Present(snapshot, 2, 11.2d, true, 2f, 4f);
            Assert.That(feedback.FireAnimationSeconds, Is.EqualTo(animation));
            Assert.That(feedback.FireFillFraction, Is.EqualTo(fill.fillAmount).Within(.00001f));
            feedback.Present(snapshot, 2, 11.3d, false, .1f, 4f);
            Assert.That(feedback.FireAnimationSeconds, Is.EqualTo(animation + .1f).Within(.00001f));
            Assert.That(feedback.PulseVisible, Is.False, "Receiving an existing A state is not a new stamina spend.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FirstApprovedSpendBeforeTheNewRoundIsPresentedKeepsItsFlash(bool newSession)
        {
            var network = root.AddComponent<LabNetwork>(); network.enabled = false;
            feedback.Bind(network, root.GetComponent<LabHud>());
            var latest = typeof(LabNetwork).GetField("latest",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            LabSnapshot oldRound = State();
            latest.SetValue(network, oldRound);
            feedback.Present(oldRound, 2, 2d, false, 0f, 4f);

            LabSnapshot nextRound = State();
            if (newSession) nextRound.sessionId = "new-stamina-session";
            else nextRound.round++;
            latest.SetValue(network, nextRound);
            // Accepted generation arrives before the new round's first LateUpdate.
            feedback.NotifySpent(20f);
            feedback.Present(nextRound, 2, 0d, false, 0f, 4f);

            Assert.That(feedback.ConsumptionPulseCount, Is.EqualTo(1));
            Assert.That(feedback.PulseVisible, Is.True,
                "Identity reset must retire old effects without discarding a new round's approved spend.");
            Assert.That(feedback.PulseFillFraction, Is.EqualTo(.5f + 20f / 120f).Within(.00001f));
            Assert.That(fill.fillAmount, Is.EqualTo(.5f), "The flash never changes actual stamina.");
        }

        [Test]
        public void FreeSupplySnapshotsNeverCreateACostPulseAndRoundOrBattleEndClearsAllEffects()
        {
            var snapshot = State();
            snapshot.orbs = new[] { new LabOrbState { id = "free-water", owner = 2,
                kind = LabOrbKind.Combined, element = LabOrbElement.Water, jigwiSupply = true } };
            feedback.Present(snapshot, 2, 25d, false, .02f, 4f);
            Assert.That(feedback.PulseVisible, Is.False);
            Assert.That(feedback.ConsumptionPulseCount, Is.Zero);
            feedback.NotifySpent(20f);
            feedback.Present(snapshot, 2, 25d, false, 0f, 4f);
            Assert.That(feedback.PulseVisible, Is.True);
            snapshot.round++;
            feedback.Present(snapshot, 2, 0d, false, 0f, 4f);
            Assert.That(feedback.PulseVisible, Is.False, "A new round cannot inherit an old spend or fire.");
            snapshot = State(true);
            feedback.Present(snapshot, 2, 6d, false, .4f, 4f);
            Assert.That(feedback.FireVisible, Is.True);
            snapshot.phase = LabPhase.Victory;
            feedback.Present(snapshot, 2, 6d, false, .1f, 4f);
            Assert.That(feedback.FireVisible, Is.False); Assert.That(feedback.PulseVisible, Is.False);
            Assert.That(feedback.EffectRoot.gameObject.activeSelf, Is.False);
            Assert.That(fill.gameObject.activeSelf, Is.True);
        }

        private static LabSnapshot State(bool personal = false) => new LabSnapshot
        {
            sessionId = "stamina-ui", round = 1, phase = LabPhase.Playing,
            players = new[] { new LabPlayerState { id = 2, stamina = 60f } },
            jigwiAB = new LabJigwiABSnapshot { enabled = personal, startedAt = personal ? 0d : -1d,
                cutoffAt = personal ? 100d : -1d, rounds = personal ? new[] {
                    new LabJigwiABRound { id = "jigwi-ab/stamina-ui/1/0", kind = LabJigwiABKind.PersonalBurn,
                        targetPlayer = 2, warningAt = 4d, activeAt = 5d, endsAt = 20d }
                } : System.Array.Empty<LabJigwiABRound>() }
        };
    }
}
