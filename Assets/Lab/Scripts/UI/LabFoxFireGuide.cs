using System;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 여우 구슬 안내 (#328): 이 플레이어(기기)의 판에 여우 구슬이 처음 떨어지면 박 위에 "빠르게 두 번 눌러서 여우 구슬을
    /// 사라지게 만들자!"를 띄우고, 내 판의 여우 구슬이 모두 사라지면 내린다. 앱을 켠 동안 다시는 띄우지 않는다.
    /// 화면 표시만 하며 터치를 막지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabFoxFireGuide : MonoBehaviour
    {
        public const string GuideText = "빠르게 두 번 눌러서\n여우 구슬을 사라지게 만들자!";
        /// <summary>The guide shows once per player (#328), under this name.</summary>
        public const string OnceKey = "fox-fire";
        // The cast reaches this screen just before the snapshot holding the orbs; it waits this long to see them.
        private const float ArrivalGraceSeconds = 2f;

        private LabNetwork network;
        private Canvas canvas;
        private RectTransform layer;
        private LabGuideText guide;
        private string guidedSession;
        private uint guidedRound;
        private bool guiding, seenFox;
        private float guidingSince;

        public bool Showing => guide != null && guide.Showing;
        public string Shown => guide != null ? guide.Shown : string.Empty;

        /// <summary>스냅샷과 기믹 알림을 받을 네트워크와, 글자를 그릴 Canvas를 연결한다.</summary>
        public void Bind(LabNetwork target, Canvas uiCanvas)
        {
            if (network != null) network.SabotageCast -= OnSabotageCast;
            network = target;
            canvas = uiCanvas != null ? uiCanvas.rootCanvas : null;
            if (network != null) network.SabotageCast += OnSabotageCast;
        }

        private void OnSabotageCast(LabSabotageKind kind, ulong targetPlayer)
        {
            LabSnapshot snapshot = network != null ? network.Snapshot : null;
            if (kind != LabSabotageKind.FoxFire || snapshot == null || targetPlayer != network.LocalPlayerId) return;
            // Only this player's first fox fire (#328); the round it falls in ends the showing at the latest.
            if (!LabGuideText.TryShowOnce(OnceKey)) return;
            guidedSession = snapshot.sessionId;
            guidedRound = snapshot.round;
            guiding = true;
            seenFox = false;
            guidingSince = Time.unscaledTime;
        }

        private void LateUpdate()
        {
            LabSnapshot snapshot = network != null ? network.Snapshot : null;
            bool playing = snapshot != null && snapshot.phase == LabPhase.Playing;
            if (!playing || snapshot.sessionId != guidedSession || snapshot.round != guidedRound) guiding = false;
            if (guiding)
            {
                bool foxHere = Array.Exists(snapshot.orbs ?? Array.Empty<LabOrbState>(), orb => orb != null
                    && orb.owner == network.LocalPlayerId && orb.kind == LabOrbKind.FoxFire && !orb.inFlight);
                seenFox |= foxHere;
                if (!foxHere && (seenFox || Time.unscaledTime - guidingSince > ArrivalGraceSeconds)) guiding = false;
            }
            bool shown = guiding && !snapshot.paused;
            if (!shown && guide == null) return;
            if (!EnsureLayer()) return;
            layer.SetAsLastSibling();
            guide.Draw(shown, GuideText);
        }

        private bool EnsureLayer()
        {
            if (layer != null) return true;
            if (canvas == null)
            {
                Canvas found = GetComponentInParent<Canvas>();
                canvas = found != null ? found.rootCanvas : null;
            }
            RectTransform canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
            if (canvasRect == null) return false;
            layer = new GameObject("Fox Fire Guide", typeof(RectTransform)).GetComponent<RectTransform>();
            layer.gameObject.layer = canvasRect.gameObject.layer;
            layer.SetParent(canvasRect, false);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            guide = LabGuideText.Create(layer, LabGuideText.FontOf(canvas), "Fox Fire Guide Text");
            return true;
        }

        private void OnDestroy()
        {
            if (network != null) network.SabotageCast -= OnSabotageCast;
            if (layer != null) Destroy(layer.gameObject);
        }
    }
}
