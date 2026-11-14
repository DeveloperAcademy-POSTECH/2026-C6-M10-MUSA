using System;

namespace C6Lab
{
    /// <summary>The content variant is baked into a player by the dedicated build entry point.</summary>
    public static class LabGameBuildVariant
    {
        public const string YaguangDemoDefine = "HAPIO_YAGUANG_DEMO";
        public const int YaguangDemoRevision = 1;
        public const string StaffStageDemoDefine = "HAPIO_STAFF_STAGE_DEMO";
        public const int StaffStageDemoRevision = 1;

#if HAPIO_YAGUANG_DEMO && HAPIO_STAFF_STAGE_DEMO
#error A player cannot contain both the legacy Yaguang demo and staff stage-selection demo.
#endif

#if HAPIO_YAGUANG_DEMO
        private const bool BuiltAsYaguangDemo = true;
#else
        private const bool BuiltAsYaguangDemo = false;
#endif

#if HAPIO_STAFF_STAGE_DEMO
        private const bool BuiltAsStaffStageDemo = true;
#else
        private const bool BuiltAsStaffStageDemo = false;
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

#if UNITY_EDITOR
        private static bool? testOverride;
        private static bool? staffStageTestOverride;

        /// <summary>Scoped, in-memory QA only; never writes preferences or player settings.</summary>
        public static IDisposable OverrideForTests(bool isYaguangDemo) => new TestOverride(isYaguangDemo);

        /// <summary>In-memory staff content selection for QA; never persists build symbols.</summary>
        public static IDisposable OverrideStaffStageDemoForTests(bool enabled) => new StaffStageTestOverride(enabled);

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
