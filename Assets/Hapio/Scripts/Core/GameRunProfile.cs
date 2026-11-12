using System;
using C6Lab;

namespace Hapio
{
    /// <summary>
    /// Immutable order of canonical stage identities. A route position never replaces the
    /// catalog index used by settings, artwork, yokai rules or transport stage identity.
    /// </summary>
    public sealed class GameRunProfile
    {
        public static readonly GameRunProfile NormalStory = new GameRunProfile("story", 1, 2, 3);
        public static readonly GameRunProfile YaguangDemo = new GameRunProfile("yaguang-demo", 2);
        public static readonly GameRunProfile Territory = new GameRunProfile("territory", 1);
        private readonly int[] stages;

        private GameRunProfile(string id, params int[] stages)
        {
            Id = id;
            this.stages = (int[])stages.Clone();
        }

        public string Id { get; }
        public int FirstStage => stages[0];
        public int FinalStage => stages[stages.Length - 1];
        public int Count => stages.Length;
        public bool IsYaguangDemo => ReferenceEquals(this, YaguangDemo);

        public static GameRunProfile ForMode(GameMode mode)
        {
            if (!Enum.IsDefined(typeof(GameMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            return mode == GameMode.Territory ? Territory
                : LabGameBuildVariant.IsYaguangDemo ? YaguangDemo : NormalStory;
        }

        public bool Contains(int stage) => Position(stage) > 0;

        /// <summary>One-based route position, or zero when the canonical stage is absent.</summary>
        public int Position(int stage)
        {
            for (int i = 0; i < stages.Length; i++) if (stages[i] == stage) return i + 1;
            return 0;
        }

        /// <summary>The next canonical stage, or zero when no next stage exists.</summary>
        public int Next(int stage)
        {
            int position = Position(stage);
            return position > 0 && position < stages.Length ? stages[position] : 0;
        }
    }
}
