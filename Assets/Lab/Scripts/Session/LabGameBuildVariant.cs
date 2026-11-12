using System;

namespace C6Lab
{
    /// <summary>The content variant is baked into a player by the dedicated build entry point.</summary>
    public static class LabGameBuildVariant
    {
        public const string YaguangDemoDefine = "HAPIO_YAGUANG_DEMO";
        public const int YaguangDemoRevision = 1;

#if HAPIO_YAGUANG_DEMO
        private const bool BuiltAsYaguangDemo = true;
#else
        private const bool BuiltAsYaguangDemo = false;
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

#if UNITY_EDITOR
        private static bool? testOverride;

        /// <summary>Scoped, in-memory QA only; never writes preferences or player settings.</summary>
        public static IDisposable OverrideForTests(bool isYaguangDemo) => new TestOverride(isYaguangDemo);

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
