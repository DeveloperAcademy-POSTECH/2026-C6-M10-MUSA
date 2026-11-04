#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// #479 지귀 실험실 이벤트 모니터 (에디터 전용). 지귀 시간표가 켜진 전투에서 화면 왼쪽 위에 작은 글자로
    /// 지귀 전투 시각 / 다음 이벤트 / 방금 일어난 이벤트 / 내 판 상태를 보여 준다.
    /// 스냅샷을 읽기만 하고 규칙·통신·다른 화면은 건드리지 않는다. Play하면 저절로 생기고 빌드에는 들어가지 않는다.
    /// </summary>
    [DefaultExecutionOrder(900)]
    public sealed class LabJigwiEventMonitor : MonoBehaviour
    {
        private const int MaximumLog = 6;
        private static bool visible = true;
        private readonly List<string> log = new List<string>();
        private readonly HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        private readonly StringBuilder text = new StringBuilder();
        private LabNetwork network;
        private string session;
        private uint round;
        private float nextFind;
        private GUIStyle style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var host = new GameObject("Jigwi Event Monitor (#479 Editor)") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<LabJigwiEventMonitor>();
        }

        private void Update()
        {
            if (network == null && Time.unscaledTime >= nextFind)
            {
                network = FindFirstObjectByType<LabNetwork>();
                nextFind = Time.unscaledTime + 1f;
            }
            text.Clear();
            LabSnapshot snapshot = network != null ? network.Snapshot : null;
            LabJigwiABSnapshot stage = snapshot?.jigwiAB;
            if (stage == null || !stage.enabled || snapshot.phase != LabPhase.Playing) return;
            if (session != snapshot.sessionId || round != snapshot.round)
            {
                session = snapshot.sessionId; round = snapshot.round;
                log.Clear(); seen.Clear();
            }
            double now = network.BattlePresentationTime;
            Observe(stage, now);
            Describe(stage, snapshot, network.LocalPlayerId, now);
        }

        private void Observe(LabJigwiABSnapshot stage, double now)
        {
            foreach (LabJigwiABRound value in stage.rounds ?? Array.Empty<LabJigwiABRound>())
            {
                if (value == null) continue;
                string name = Name(value);
                if (now >= value.warningAt) Add(value.id + "/warn", value.warningAt, stage, name + " 예고");
                bool started = value.resolution == LabJigwiABResolution.None || value.resolvedAt >= value.activeAt;
                if (started && now >= value.activeAt) Add(value.id + "/active", value.activeAt, stage, name + " 시작");
                if (value.resolution != LabJigwiABResolution.None && now >= value.resolvedAt)
                    Add(value.id + "/end", value.resolvedAt, stage, name + " " + Result(value));
                if (value.rewardUntil > 0d && now >= value.rewardUntil)
                    Add(value.id + "/reward", value.rewardUntil, stage, name + " 보상 끝");
            }
            if (stage.fever) Add("fever", stage.cutoffAt, stage, "피버 시작");
        }

        private void Add(string key, double at, LabJigwiABSnapshot stage, string label)
        {
            if (!seen.Add(key)) return;
            log.Insert(0, Seconds(at - stage.startedAt) + "  " + label);
            if (log.Count > MaximumLog) log.RemoveAt(log.Count - 1);
        }

        private void Describe(LabJigwiABSnapshot stage, LabSnapshot snapshot, ulong local, double now)
        {
            text.Append("지귀 실험실 (#479)  ").Append(Seconds(now - stage.startedAt));
            LabJigwiABRound active = stage.Active(now);
            if (active != null && active.Active(now))
                text.Append("  · ").Append(Name(active)).Append(" 진행 중 ").Append(Seconds(active.endsAt - now)).Append(" 남음");
            text.Append('\n').Append("다음: ").Append(Next(stage, now));
            int burning = 0, water = 0;
            foreach (LabOrbState orb in snapshot.orbs ?? Array.Empty<LabOrbState>())
            {
                if (orb == null || orb.owner != local || orb.inFlight) continue;
                if (orb.jigwiABBurnAt >= 0d) burning++;
                if (!string.IsNullOrEmpty(orb.jigwiABSupplyEventId)) water++;
            }
            text.Append('\n').Append("내 판: 불붙은 구슬 ").Append(burning).Append(" / 물 구슬 ").Append(water);
            foreach (string line in log) text.Append('\n').Append(line);
        }

        private static string Next(LabJigwiABSnapshot stage, double now)
        {
            double best = double.PositiveInfinity; string label = "없음";
            void Consider(double at, string name) { if (at > now && at < best) { best = at; label = name; } }
            for (int slot = 0; slot < LabJigwiABTimeline.SlotCount; slot++)
            {
                double active = stage.startedAt + LabJigwiABTimeline.ActiveOffset(slot);
                string name = Name(slot);
                Consider(active - LabJigwiABTimeline.WarningSeconds, name + " 예고");
                Consider(active, name + " 시작");
            }
            Consider(stage.cutoffAt, "피버 시작");
            return double.IsInfinity(best) ? label : label + " " + Seconds(best - stage.startedAt) + " (" + Seconds(best - now) + " 뒤)";
        }

        private static string Name(LabJigwiABRound value) => Name(value.slot);
        private static string Name(int slot) => (LabJigwiABTimeline.KindForSlot(slot) == LabJigwiABKind.PersonalBurn
            ? "A" : "B") + (slot / 2 + 1);
        private static string Result(LabJigwiABRound value)
        {
            switch (value.resolution)
            {
                case LabJigwiABResolution.Completed: return value.kind == LabJigwiABKind.GroupFire ? "성공" : "완료";
                case LabJigwiABResolution.Expired: return value.kind == LabJigwiABKind.GroupFire ? "실패" : "시간 끝";
                case LabJigwiABResolution.Cancelled: return "취소";
                default: return value.resolution.ToString();
            }
        }
        private static string Seconds(double value) =>
            Math.Max(0d, value).ToString("0.0", CultureInfo.InvariantCulture) + "초";

        private void OnGUI()
        {
            if (text.Length == 0) return;
            if (style == null)
                style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 12, wordWrap = false, richText = false };
            if (GUI.Button(new Rect(8f, 8f, 64f, 20f), visible ? "모니터 끄기" : "모니터")) visible = !visible;
            if (!visible) return;
            var content = new GUIContent(text.ToString());
            Vector2 size = style.CalcSize(content);
            GUI.Box(new Rect(8f, 30f, size.x + 8f, size.y + 4f), content, style);
        }
    }
}
#endif
