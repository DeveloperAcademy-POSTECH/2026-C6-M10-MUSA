using System;
using C6Lab;
using UnityEngine;

namespace Hapio
{
    /// <summary>Host-owned runtime copies. Scope 0 edits shared values; scopes 1..3 edit unique mechanics.</summary>
    public sealed class GameStageSettings : IDisposable
    {
        private readonly LabConfig template;
        private float[] common;
        private readonly float[][] effective = new float[3][];
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
            }
            catch { Dispose(); throw; }
        }
        public float[] Capture(int scope)
        {
            CheckScope(scope);
            return (float[])(scope == 0 ? common : effective[scope - 1]).Clone();
        }
        public static bool IsFieldVisible(int scope, int index)
        {
            if (scope < 0 || scope > 3 || index < 0 || index >= LabConfig.DeveloperFields.Count) return false;
            bool yaguang = index >= 155 && index <= 164 || index >= 184 && index <= 191;
            bool gumiho = index >= 166 && index <= 177;
            bool jigwi = index >= 178 && index <= 183 || index >= 192 && index <= 195;
            if (scope == 0) return !yaguang && !gumiho && !jigwi;
            // Legacy bomb/block weights are masked by the stage policy and never presented as useful controls.
            if (scope == 1) return gumiho && index != 169 && index != 170;
            return scope == 2 ? yaguang : jigwi;
        }
        public bool TryApply(int scope, float[] values, out string error)
        {
            error = "Select Common or one of the three stage scopes.";
            if (template == null || scope < 0 || scope > 3) return false;
            float[] before = Capture(scope);
            if (values == null || values.Length != before.Length) { error = "The stage settings form is incomplete."; return false; }
            for (int i = 0; i < values.Length; i++)
                if (!IsFieldVisible(scope, i) && !values[i].Equals(before[i]))
                { error = "A field from another settings scope was changed."; return false; }
            if (!Validate(values, out error)) return false;
            if (scope != 0) { effective[scope - 1] = (float[])values.Clone(); return true; }
            var candidates = new float[3][];
            for (int s = 0; s < 3; s++)
            {
                candidates[s] = (float[])effective[s].Clone();
                for (int i = 0; i < values.Length; i++) if (IsFieldVisible(0, i)) candidates[s][i] = values[i];
                if (!Validate(candidates[s], out error)) return false;
            }
            common = (float[])values.Clone();
            for (int s = 0; s < 3; s++) effective[s] = candidates[s];
            error = string.Empty;
            return true;
        }
        public LabConfig CreateStageConfig(int stage)
        {
            CheckScope(stage);
            if (stage == 0) throw new ArgumentOutOfRangeException(nameof(stage));
            var result = UnityEngine.Object.Instantiate(template);
            if (result.TryApplyDeveloperValues(effective[stage - 1], out string error)) return result;
            Release(result);
            throw new InvalidOperationException(error);
        }
        /// <summary>Legacy Battle DEV edits Common and the current yokai atomically, never another stage.</summary>
        public bool TryApplyBattle(int stage, float[] values, out string error, bool commit = true)
        {
            error = "A current stage settings form is required.";
            if (stage < 1 || stage > 3 || values == null || values.Length != LabConfig.DeveloperFields.Count)
                return false;
            float[] before = Capture(stage);
            for (int i = 0; i < values.Length; i++)
                if (!IsFieldVisible(0, i) && !IsFieldVisible(stage, i) && !values[i].Equals(before[i]))
                { error = "A field belonging to another stage was changed."; return false; }
            if (!Validate(values, out error)) return false;
            float[] nextCommon = (float[])common.Clone();
            var nextStages = new float[3][];
            for (int i = 0; i < values.Length; i++)
                if (IsFieldVisible(0, i)) nextCommon[i] = values[i];
            for (int s = 1; s <= 3; s++)
            {
                nextStages[s - 1] = Capture(s);
                for (int i = 0; i < values.Length; i++)
                    if (IsFieldVisible(0, i) || s == stage && IsFieldVisible(stage, i))
                        nextStages[s - 1][i] = values[i];
                if (!Validate(nextStages[s - 1], out error)) return false;
            }
            if (commit)
            {
                common = nextCommon;
                for (int s = 0; s < 3; s++) effective[s] = nextStages[s];
            }
            error = string.Empty;
            return true;
        }
        private bool Validate(float[] values, out string error)
        {
            var candidate = UnityEngine.Object.Instantiate(template);
            try { return candidate.TryApplyDeveloperValues(values, out error); }
            finally { Release(candidate); }
        }
        private void CheckScope(int scope)
        {
            if (template == null) throw new ObjectDisposedException(nameof(GameStageSettings));
            if (scope < 0 || scope > 3) throw new ArgumentOutOfRangeException(nameof(scope));
        }
        public void Dispose() { Release(template); }
        private static void Release(UnityEngine.Object item)
        {
            if (item == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(item); else UnityEngine.Object.DestroyImmediate(item);
        }
    }
}
