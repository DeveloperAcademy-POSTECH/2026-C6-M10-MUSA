using System;
using NUnit.Framework;

namespace C6Lab.Tests
{
    public sealed class LabGameBuildVariantTests
    {
        [Test]
        public void TutorialOnlyOverrideIsIndependentOfMultiplayerDemosAndRestoresNestedScopes()
        {
            Assert.That(LabGameBuildVariant.IsTutorialOnly, Is.False);
            Assert.That(LabGameBuildVariant.TutorialOnlyDefine, Is.EqualTo("HAPIO_TUTORIAL_ONLY"));
            using (LabGameBuildVariant.EditorOverrideTutorialOnlyForTests(true))
            {
                Assert.That(LabGameBuildVariant.IsTutorialOnly, Is.True);
                Assert.That(LabGameBuildVariant.IsDemo || LabGameBuildVariant.IsYaguangDemo || LabGameBuildVariant.IsStaffStageDemo, Is.False);
                using (LabGameBuildVariant.EditorOverrideTutorialOnlyForTests(false))
                    Assert.That(LabGameBuildVariant.IsTutorialOnly, Is.False);
                Assert.That(LabGameBuildVariant.IsTutorialOnly, Is.True);
            }
            Assert.That(LabGameBuildVariant.IsTutorialOnly, Is.False);
        }

        [Test]
        public void TutorialOnlyOverrideCannotLeakAfterAnExceptionOrRepeatedDisposal()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                using (LabGameBuildVariant.EditorOverrideTutorialOnlyForTests(true))
                    throw new InvalidOperationException("Tutorial QA failed");
            });
            Assert.That(LabGameBuildVariant.IsTutorialOnly, Is.False);
            IDisposable scope = LabGameBuildVariant.EditorOverrideTutorialOnlyForTests(true);
            scope.Dispose();
            using (LabGameBuildVariant.EditorOverrideTutorialOnlyForTests(true))
            {
                scope.Dispose();
                Assert.That(LabGameBuildVariant.IsTutorialOnly, Is.True);
            }
            Assert.That(LabGameBuildVariant.IsTutorialOnly, Is.False);
        }

        [Test]
        public void EditorDefaultsToNormalAndScopedOverridesRestoreTheirPreviousValue()
        {
            Assert.That(LabGameBuildVariant.IsYaguangDemo, Is.False,
                "The demo symbol belongs to BuildPlayerOptions.extraScriptingDefines, never the saved Editor symbols.");
            using (LabGameBuildVariant.OverrideForTests(true))
            {
                Assert.That(LabGameBuildVariant.IsYaguangDemo, Is.True);
                using (LabGameBuildVariant.OverrideForTests(false))
                    Assert.That(LabGameBuildVariant.IsYaguangDemo, Is.False);
                Assert.That(LabGameBuildVariant.IsYaguangDemo, Is.True);
            }
            Assert.That(LabGameBuildVariant.IsYaguangDemo, Is.False);
        }

        [Test]
        public void ExceptionAndRepeatedDisposalCannotLeaveTheDemoOverrideEnabled()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                using (LabGameBuildVariant.OverrideForTests(true)) throw new InvalidOperationException("QA failed");
            });
            Assert.That(LabGameBuildVariant.IsYaguangDemo, Is.False);
            IDisposable scope = LabGameBuildVariant.OverrideForTests(true);
            scope.Dispose();
            using (LabGameBuildVariant.OverrideForTests(true))
            {
                scope.Dispose();
                Assert.That(LabGameBuildVariant.IsYaguangDemo, Is.True, "An already disposed scope cannot overwrite a newer one.");
            }
            Assert.That(LabGameBuildVariant.IsYaguangDemo, Is.False);
        }

        [Test]
        public void StaffOverrideDoesNotBecomeLegacyYaguangAndRestoresNestedScopes()
        {
            Assert.That(LabGameBuildVariant.IsStaffStageDemo, Is.False);
            Assert.That(LabGameBuildVariant.IsDemo, Is.False);
            using (LabGameBuildVariant.OverrideStaffStageDemoForTests(true))
            {
                Assert.That(LabGameBuildVariant.IsStaffStageDemo, Is.True);
                Assert.That(LabGameBuildVariant.IsYaguangDemo, Is.False);
                Assert.That(LabGameBuildVariant.IsDemo, Is.True);
                using (LabGameBuildVariant.OverrideStaffStageDemoForTests(false))
                    Assert.That(LabGameBuildVariant.IsDemo, Is.False);
                Assert.That(LabGameBuildVariant.IsStaffStageDemo, Is.True);
            }
            Assert.That(LabGameBuildVariant.IsDemo, Is.False);
        }

        [Test]
        public void StaffOverrideCannotLeakAfterAnExceptionOrRepeatedDisposal()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                using (LabGameBuildVariant.OverrideStaffStageDemoForTests(true))
                    throw new InvalidOperationException("Staff QA failed");
            });
            Assert.That(LabGameBuildVariant.IsDemo, Is.False);
            IDisposable scope = LabGameBuildVariant.OverrideStaffStageDemoForTests(true);
            scope.Dispose();
            using (LabGameBuildVariant.OverrideStaffStageDemoForTests(true))
            {
                scope.Dispose();
                Assert.That(LabGameBuildVariant.IsStaffStageDemo, Is.True);
            }
            Assert.That(LabGameBuildVariant.IsDemo, Is.False);
        }
    }
}
