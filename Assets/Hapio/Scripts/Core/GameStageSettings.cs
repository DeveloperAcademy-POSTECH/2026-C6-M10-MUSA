using System;
using C6Lab;
using UnityEngine;

namespace Hapio
{
    /// <summary>
    /// Host-owned runtime copies. Scope 0 edits shared values; scopes 1..3 edit unique mechanics; scope 4 is the
    /// 2:2 team territory battle (#281), which always keeps its switch on and edits only its time and fever.
    /// </summary>
    public sealed class GameStageSettings : IDisposable
    {
        public const int TerritoryScope = 4;
        private const int TerritorySwitch = 196;
        private readonly LabConfig template;
        private float[] common;
        private readonly float[][] effective = new float[3][];
        private float[] territory;
        public GameStageSettings(LabConfig source, GameStageCatalog catalog)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            catalog.Validate();
            template = UnityEngine.Object.Instantiate(source);
            template.name = "Hapio Runtime Stage Settings";
            common = template.CaptureDeveloperValues();
            try
            {
                for (int s = 1; s <= 3; s++)
                {
                    float[] values = (float[])common.Clone();
                    foreach (GameStageValue item in catalog.Get(s).defaultValues ?? Array.Empty<GameStageValue>())
                        values[item.index] = item.value;
                    if (!Validate(values, out string error)) throw new InvalidOperationException(error);
                    effective[s - 1] = values;
                }
                territory = (float[])common.Clone();
                territory[TerritorySwitch] = 1f;
                foreach (GameStageValue item in catalog.territoryDefaultValues ?? Array.Empty<GameStageValue>())
                    territory[item.index] = item.value;
                if (!Validate(territory, out string territoryError)) throw new InvalidOperationException(territoryError);
            }
            catch { Dispose(); throw; }
        }
        public float[] Capture(int scope)
        {
            CheckScope(scope);
            return (float[])(scope == 0 ? common : scope == TerritoryScope ? territory : effective[scope - 1]).Clone();
        }
        /// <summary>
        /// 2:2 (#305) keeps its own stamina, generation and orb lifetime: the story's values (Common) stop at the
        /// three stages and never reach the territory room, and the territory room's never reach Common.
        /// </summary>
        public static bool IsTerritoryOwned(int index) => index >= 3 && index <= 8 || index == 10;
        public static bool IsFieldVisible(int scope, int index)
        {
            if (scope < 0 || scope > TerritoryScope || index < 0 || index >= LabConfig.DeveloperFields.Count) return false;
            if (scope == TerritoryScope) return index >= 197 && index <= 199 || IsTerritoryOwned(index);
            bool yaguang = index >= 155 && index <= 164 || index >= 184 && index <= 191;
            // 매혹 예고 (#305) 202번도 구미호 칸이다.
            bool gumiho = index >= 166 && index <= 177 || index == 202;
            bool jigwi = index >= 178 && index <= 183 || index >= 192 && index <= 195;
            // 2:2 땅따먹기 (#281)의 켜기/끄기·시간·피버는 3요괴 스테이지 설정에 넣지 않는다.
            bool team = index >= 196 && index <= 199;
            if (scope == 0) return !yaguang && !gumiho && !jigwi && !team;
            // Legacy bomb/block weights are masked by the stage policy and never presented as useful controls.
            if (scope == 1) return gumiho && index != 169 && index != 170;
            return scope == 2 ? yaguang : jigwi;
        }
        public bool TryApply(int scope, float[] values, out string error)
        {
            error = "Select Common, one of the three stage scopes or 2:2.";
            if (template == null || scope < 0 || scope > TerritoryScope) return false;
            float[] before = Capture(scope);
            if (values == null || values.Length != before.Length) { error = "The stage settings form is incomplete."; return false; }
            for (int i = 0; i < values.Length; i++)
                if (!IsFieldVisible(scope, i) && !values[i].Equals(before[i]))
                { error = "A field from another settings scope was changed."; return false; }
            if (!Validate(values, out error)) return false;
            if (scope == TerritoryScope) { territory = (float[])values.Clone(); return true; }
            if (scope != 0) { effective[scope - 1] = (float[])values.Clone(); return true; }
            var candidates = new float[4][];
            for (int s = 0; s < 4; s++)
            {
                candidates[s] = (float[])(s < 3 ? effective[s] : territory).Clone();
                for (int i = 0; i < values.Length; i++)
                    if (IsFieldVisible(0, i) && !(s == 3 && IsTerritoryOwned(i))) candidates[s][i] = values[i];
                if (!Validate(candidates[s], out error)) return false;
            }
            common = (float[])values.Clone();
            for (int s = 0; s < 3; s++) effective[s] = candidates[s];
            territory = candidates[3];
            error = string.Empty;
            return true;
        }
        public LabConfig CreateStageConfig(int stage)
        {
            CheckScope(stage);
            if (stage == 0) throw new ArgumentOutOfRangeException(nameof(stage));
            var result = UnityEngine.Object.Instantiate(template);
            if (result.TryApplyDeveloperValues(stage == TerritoryScope ? territory : effective[stage - 1], out string error))
                return result;
            Release(result);
            throw new InvalidOperationException(error);
        }

        /// <summary>The 2:2 battle profile: Common values with the team territory switch on (#281).</summary>
        public LabConfig CreateTerritoryConfig() => CreateStageConfig(TerritoryScope);
        /// <summary>Legacy Battle DEV edits Common and the current yokai atomically, never another stage.</summary>
        public bool TryApplyBattle(int stage, float[] values, out string error, bool commit = true)
        {
            error = "A current stage settings form is required.";
            if (stage < 1 || stage > TerritoryScope || values == null || values.Length != LabConfig.DeveloperFields.Count)
                return false;
            float[] before = Capture(stage);
            for (int i = 0; i < values.Length; i++)
                if (!IsFieldVisible(0, i) && !IsFieldVisible(stage, i) && !values[i].Equals(before[i]))
                { error = "A field belonging to another stage was changed."; return false; }
            if (!Validate(values, out error)) return false;
            float[] nextCommon = (float[])common.Clone();
            var nextStages = new float[4][];
            for (int i = 0; i < values.Length; i++)
                if (SharesCommon(stage, i)) nextCommon[i] = values[i];
            for (int s = 1; s <= TerritoryScope; s++)
            {
                nextStages[s - 1] = Capture(s);
                for (int i = 0; i < values.Length; i++)
                    if (SharesCommon(stage, i) && SharesCommon(s, i) || s == stage && IsFieldVisible(stage, i))
                        nextStages[s - 1][i] = values[i];
                if (!Validate(nextStages[s - 1], out error)) return false;
            }
            if (commit)
            {
                common = nextCommon;
                for (int s = 0; s < 3; s++) effective[s] = nextStages[s];
                territory = nextStages[3];
            }
            error = string.Empty;
            return true;
        }
        /// <summary>A Common field this scope takes from (and gives to) Common; the territory room keeps its own.</summary>
        private static bool SharesCommon(int scope, int index) =>
            IsFieldVisible(0, index) && !(scope == TerritoryScope && IsTerritoryOwned(index));
        private bool Validate(float[] values, out string error)
        {
            var candidate = UnityEngine.Object.Instantiate(template);
            try { return candidate.TryApplyDeveloperValues(values, out error); }
            finally { Release(candidate); }
        }
        private void CheckScope(int scope)
        {
            if (template == null) throw new ObjectDisposedException(nameof(GameStageSettings));
            if (scope < 0 || scope > TerritoryScope) throw new ArgumentOutOfRangeException(nameof(scope));
        }
        public void Dispose() { Release(template); }
        private static void Release(UnityEngine.Object item)
        {
            if (item == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(item); else UnityEngine.Object.DestroyImmediate(item);
        }
    }
}
