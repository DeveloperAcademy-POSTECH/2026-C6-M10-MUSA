using System;

namespace C6Lab
{
    /// <summary>The content variant is baked into a player by the dedicated build entry point.</summary>
    public static class LabGameBuildVariant
    {
        public const string YaguangDemoDefine = "HAPIO_YAGUANG_DEMO";
        public const int YaguangDemoRevision = 1;
        public const string StaffStageDemoDefine = "HAPIO_STAFF_STAGE_DEMO";
        // Revision 4 finishes each local afterstory directly at QR; older apps wait for shared readiness and an ending.
        public const int StaffStageDemoRevision = 4;
        /// <summary>Staff demo group is independent of player P slots and territory teams.</summary>
        public static bool IsValidDemoGroup(int group) => group == 1 || group == 2;
        public static bool IsValidStaffRoomIdentity(int stage, int group) => stage >= 1 && stage <= 3 && IsValidDemoGroup(group);
        public const string StaffStageTwoOnlyDefine = "HAPIO_STAFF_STAGE_TWO_ONLY";
        public const string TutorialOnlyDefine = "HAPIO_TUTORIAL_ONLY";

#if HAPIO_TUTORIAL_ONLY && (HAPIO_YAGUANG_DEMO || HAPIO_STAFF_STAGE_DEMO || HAPIO_STAFF_STAGE_TWO_ONLY)
#error The tutorial-only player cannot contain a multiplayer demo variant.
#endif

#if HAPIO_YAGUANG_DEMO && HAPIO_STAFF_STAGE_DEMO
#error A player cannot contain both the legacy Yaguang demo and staff stage-selection demo.
#endif

#if HAPIO_STAFF_STAGE_TWO_ONLY && !HAPIO_STAFF_STAGE_DEMO
#error The Stage2-only selection requires the staff stage-selection demo.
#endif

#if HAPIO_YAGUANG_DEMO
        private const bool BuiltAsYaguangDemo = true;
#else
        private const bool BuiltAsYaguangDemo = false;
#endif

#if HAPIO_STAFF_STAGE_TWO_ONLY
        private const bool BuiltAsStaffStageTwoOnly = true;
#else
        private const bool BuiltAsStaffStageTwoOnly = false;
#endif

#if HAPIO_STAFF_STAGE_DEMO
        private const bool BuiltAsStaffStageDemo = true;
#else
        private const bool BuiltAsStaffStageDemo = false;
#endif

#if HAPIO_TUTORIAL_ONLY
        private const bool BuiltAsTutorialOnly = true;
#else
        private const bool BuiltAsTutorialOnly = false;
#endif

        public static bool IsYaguangDemo
        {
            get
            {
#if UNITY_EDITOR
                if (testOverride.HasValue) return testOverride.Value;
#endif
                return BuiltAsYaguangDemo;
            }
        }

        public static bool IsStaffStageDemo
        {
            get
            {
#if UNITY_EDITOR
                if (staffStageTestOverride.HasValue) return staffStageTestOverride.Value;
#endif
                return BuiltAsStaffStageDemo;
            }
        }

        public static bool IsDemo => IsYaguangDemo || IsStaffStageDemo;

        /// <summary>Title-to-tutorial replay build; deliberately not a multiplayer demo.</summary>
        public static bool IsTutorialOnly
        {
            get
            {
#if UNITY_EDITOR
                if (tutorialOnlyTestOverride.HasValue) return tutorialOnlyTestOverride.Value;
#endif
                return BuiltAsTutorialOnly;
            }
        }

        /// <summary>A build-only selection restriction; it retains the staff Stage2 route and compatibility.</summary>
        public static bool IsStaffStageTwoOnly
        {
            get
            {
                if (!IsStaffStageDemo) return false;
#if UNITY_EDITOR
                if (staffStageTwoOnlyTestOverride.HasValue) return staffStageTwoOnlyTestOverride.Value;
#endif
                return BuiltAsStaffStageTwoOnly;
            }
        }

        public static bool AllowsStaffDemoStage(int stage) => stage >= 1 && stage <= 3
            && (!IsStaffStageTwoOnly || stage == 2);

#if UNITY_EDITOR
        private static bool? testOverride;
        private static bool? staffStageTestOverride;
        private static bool? staffStageTwoOnlyTestOverride;
        private static bool? tutorialOnlyTestOverride;

        /// <summary>Scoped QA for title/tutorial replay without saving a player define.</summary>
        public static IDisposable EditorOverrideTutorialOnlyForTests(bool enabled) => new TutorialOnlyTestOverride(enabled);

        private sealed class TutorialOnlyTestOverride : IDisposable
        {
            private readonly bool? previous;
            private bool disposed;

            public TutorialOnlyTestOverride(bool value)
            {
                previous = tutorialOnlyTestOverride;
                tutorialOnlyTestOverride = value;
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                tutorialOnlyTestOverride = previous;
            }
        }

        /// <summary>Scoped, in-memory QA only; never writes preferences or player settings.</summary>
        public static IDisposable OverrideForTests(bool isYaguangDemo) => new TestOverride(isYaguangDemo);

        /// <summary>In-memory staff content selection for QA; never persists build symbols.</summary>
        public static IDisposable OverrideStaffStageDemoForTests(bool enabled) => new StaffStageTestOverride(enabled);

        /// <summary>Scoped QA for the fixed selection, without changing the Editor's saved build symbols.</summary>
        public static IDisposable OverrideStaffStageTwoOnlyForTests(bool enabled) => new StaffStageTwoOnlyTestOverride(enabled);

        private sealed class StaffStageTwoOnlyTestOverride : IDisposable
        {
            private readonly bool? previous;
            private bool disposed;

            public StaffStageTwoOnlyTestOverride(bool value)
            {
                previous = staffStageTwoOnlyTestOverride;
                staffStageTwoOnlyTestOverride = value;
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                staffStageTwoOnlyTestOverride = previous;
            }
        }

        private sealed class StaffStageTestOverride : IDisposable
        {
            private readonly bool? previous;
            private bool disposed;

            public StaffStageTestOverride(bool value)
            {
                previous = staffStageTestOverride;
                staffStageTestOverride = value;
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                staffStageTestOverride = previous;
            }
        }

        private sealed class TestOverride : IDisposable
        {
            private readonly bool? previous;
            private bool disposed;

            public TestOverride(bool value)
            {
                previous = testOverride;
                testOverride = value;
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                testOverride = previous;
            }
        }
#endif
    }
}
