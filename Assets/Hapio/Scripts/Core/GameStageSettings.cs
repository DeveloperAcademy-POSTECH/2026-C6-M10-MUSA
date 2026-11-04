using System;
using System.Collections.Generic;
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
        /// <summary>섬광 대상 (0 모두, 1 한 명). 게임에서는 모두로 고정한다 (#301).</summary>
        public const int FlashTargetOneField = 185;
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
                    values[FlashTargetOneField] = 0f; // 섬광은 언제나 모두에게 (#301)
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
        private const int LabUniqueGimmicksField = 166;
        private static readonly int[] RetiredFields = System.Linq.Enumerable.ToArray(LabConfig.RetiredDeveloperFields);
        /// <summary>The common gimmick group's settings (#311), which each of the three stages keeps for itself.</summary>
        public static bool IsStageCommonGimmick(int index) => index == 169 || index == 170 || index == 205 || index == 207 || index == 208;
        public static bool IsFieldVisible(int scope, int index)
        {
            if (scope < 0 || scope > TerritoryScope || index < 0 || index >= LabConfig.DeveloperFields.Count) return false;
            // The gimmick groups (#311) replaced these timers; they stay in the form but are never shown.
            if (Array.IndexOf(RetiredFields, index) >= 0) return false;
            if (LabConfig.IsYaguangTimelineField(index)) return scope == 2;
            if (LabConfig.IsJigwiStageField(index)) return scope == 3;
            // The yaguang profile uses scheduled slots and alternation, so its generic group interval/chances do not apply.
            if (scope == 2 && (index == 205 || index == 169 || index == 170 || index == 156 || index == 157
                || index == 186 || index == 187)) return false;
            if (index == 79) return false; // Retired crack-mode selector; numeric slot stays reserved.
            if (scope == TerritoryScope) return index >= 197 && index <= 199 || IsTerritoryOwned(index);
            bool yaguang = index >= 155 && index <= 164 || index >= 184 && index <= 191 || index >= 202 && index <= 203 || index >= 236 && index <= 237;
            // The game always flashes everyone (#301); Lab alone can select a single target.
            if (index == FlashTargetOneField) return false;
            // Lab's own switch for borrowing the fox's gimmicks (#311); a game stage's yokai decides them instead.
            if (index == LabUniqueGimmicksField) return false;
            // Every stage sets its own common gimmicks (#311): interval, bomb and block switches and chances.
            if (IsStageCommonGimmick(index)) return scope >= 1 && scope <= 3;
            // The fox's own gimmicks (#311): interval, switches and chances, with fox fire and charm details (#251, #305).
            bool gumiho = index >= 171 && index <= 177 || index == 204 || index == 206 || index == 209 || index == 210
                // Fever time (#334) is the fox's alone for now; the other yokai keep their own endings.
                || index == LabConfig.FeverTimeSecondsField || index == LabConfig.FeverTimeRecoveryField;
            bool jigwi = index >= 178 && index <= 183 || index >= 192 && index <= 195;
            // 2:2 땅따먹기 (#281)의 켜기/끄기·시간·피버는 3요괴 스테이지 설정에 넣지 않는다.
            bool team = index >= 196 && index <= 199;
            if (scope == 0) return !yaguang && !gumiho && !jigwi && !team;
            if (scope == 1) return gumiho;
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

        /// <summary>
        /// 튜토리얼 (#477) 연습 판이 1스테이지(구미호) 값에서 바꾸는 DEV 항목 번호와 값. 시간은 끝나지 않고(화면에도 숨김),
        /// 구슬·폭탄은 대사를 읽는 동안 사라지거나 터지지 않으며, 무작위 방해공작·아이템·피버·콤보·칠 마름은 모두 끈다.
        /// 방해공작은 튜토리얼이 정한 단계에서만 직접 일으킨다.
        /// </summary>
        public static readonly IReadOnlyList<KeyValuePair<int, float>> TutorialOverrides = new[]
        {
            new KeyValuePair<int, float>(2, 86400f),   // Battle Duration: never runs out
            new KeyValuePair<int, float>(10, 86400f),  // Orb Lifetime: orbs wait while lines are read
            new KeyValuePair<int, float>(45, 86400f),  // Item Lifetime: a locked bomb never goes off
            new KeyValuePair<int, float>(39, 50f),     // Yin
            new KeyValuePair<int, float>(40, 50f),     // Yang
            new KeyValuePair<int, float>(41, 0f),      // Relay
            new KeyValuePair<int, float>(42, 0f),      // Secret Box
            new KeyValuePair<int, float>(43, 0f),      // Potion
            new KeyValuePair<int, float>(44, 0f),      // Bomb
            new KeyValuePair<int, float>(74, 1f),      // one drum for the three seats
            new KeyValuePair<int, float>(76, 0f),      // the drum does not wander
            new KeyValuePair<int, float>(77, 0f),
            new KeyValuePair<int, float>(87, 0f),      // no timed gourd bombs
            new KeyValuePair<int, float>(88, 0f),
            new KeyValuePair<int, float>(148, 0f),     // paint does not dry
            new KeyValuePair<int, float>(152, 0f),     // no timed swipe block
            new KeyValuePair<int, float>(155, 0f),     // no gourd color combo
            new KeyValuePair<int, float>(184, 0f),     // no yaguang flash
            new KeyValuePair<int, float>(205, 0f),     // no common gimmick draws
            new KeyValuePair<int, float>(206, 0f),     // no unique gimmick draws
            new KeyValuePair<int, float>(207, 0f),     // gimmick switches
            new KeyValuePair<int, float>(208, 0f),
            new KeyValuePair<int, float>(209, 0f),
            new KeyValuePair<int, float>(210, 0f),
            new KeyValuePair<int, float>(238, 0f),     // no fever time
        };

        /// <summary>The tutorial practice board (#477): stage 1 with <see cref="TutorialOverrides"/>.</summary>
        public LabConfig CreateTutorialConfig()
        {
            float[] values = Capture(1);
            foreach (KeyValuePair<int, float> item in TutorialOverrides) values[item.Key] = item.Value;
            var result = UnityEngine.Object.Instantiate(template);
            result.name = "Hapio Tutorial Settings";
            if (result.TryApplyDeveloperValues(values, out string error)) return result;
            Release(result);
            throw new InvalidOperationException(error);
        }
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
