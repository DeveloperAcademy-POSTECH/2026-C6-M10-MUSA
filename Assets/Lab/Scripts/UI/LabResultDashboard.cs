using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 승리/패배 결과 화면에 이번 라운드 칭호(딜왕·패스왕·막타·북 독점)를 칭호별로 한 줄씩 보여주는 대시보드.
    /// 줄마다 칭호 · 받은 사람(동점이면 모두) · 기록. 기록이 0인 칭호는 줄을 만들지 않는다.
    /// 장면 수정 없이 코드로 만들고, 결과 카드(VICTORY/DEFEAT·버튼 전체) 바로 밑에 놓는다.
    /// 결과 카드는 화면 위에서 1/3 지점으로 올려 목록이 아래 HUD에 가리지 않게 하고, 결과 화면이 끝나면 되돌린다.
    /// 값은 Host가 보낸 스냅샷의 플레이어 기록(LabPlayerState)을 그대로 쓰므로 모든 기기가 같다.
    /// 표 생김새는 LabResultTable(STAGE CLEAR 화면과 공용), 계산은 LabResultStats가 맡는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabResultDashboard : MonoBehaviour
    {
        /// <summary>결과 카드 아랫변과 목록 사이 간격</summary>
        public float gapBelowResult = 12f;
        /// <summary>결과 카드 윗변 위치: 화면 위에서부터의 비율 (1/3이면 위에서 1/3 지점). 목록은 그 밑에 붙는다.</summary>
        [Range(0f, 1f)] public float resultTopFromTop = 1f / 3f;
        /// <summary>표 생김새 (폭·줄 높이·글자 크기·색). STAGE CLEAR 화면의 표와 같은 기본값을 쓴다.</summary>
        public LabResultTableStyle style = new LabResultTableStyle();

        /// <summary>받은 칭호가 하나도 없을 때 한 줄</summary>
        public const string NoRecordLabel = LabResultTable.NoRecordLabel;

        private LabNetwork network;
        private GameObject resultControls;
        private RectTransform panel;
        private Font font;
        private string shownKey;
        // 카드를 옮기기 전 위치 (결과 화면이 끝나면 되돌린다)
        private bool moved;
        private Vector2 cardAnchorMin, cardAnchorMax, cardOffsetMin, cardOffsetMax;

        public bool IsShowing => panel != null && panel.gameObject.activeSelf;

        public void Bind(LabNetwork target, GameObject result, Font textFont)
        {
            network = target;
            resultControls = result;
            font = textFont != null ? textFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void Update()
        {
            LabSnapshot snapshot = network != null ? network.Snapshot : null;
            bool terminal = snapshot != null && (snapshot.phase == LabPhase.Victory || snapshot.phase == LabPhase.Defeat);
            terminal = terminal && !LabGourdFinish.DelayVictory(network, snapshot);
            if (!terminal)
            {
                if (panel != null && panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
                RestoreCard();
                shownKey = null;
                return;
            }
            string key = snapshot.sessionId + ":" + snapshot.round + ":" + snapshot.phase;
            if (key == shownKey && panel != null && panel.gameObject.activeSelf) return;
            shownKey = key;
            Show(snapshot.players, snapshot.phase == LabPhase.Victory, snapshot.playedSeconds);
        }

        /// <summary>칭호별 목록을 다시 그린다. (테스트에서도 직접 부를 수 있다)</summary>
        public void Show(LabPlayerState[] players, bool victory, double playedSeconds = 0d)
        {
            EnsurePanel();
            if (panel == null) return;
            var rows = (players ?? new LabPlayerState[0]).Where(p => p != null).OrderBy(p => p.seat).ToArray();
            ulong local = network != null ? network.LocalPlayerId : ulong.MaxValue;
            IReadOnlyList<LabResultAward> awards = LabResultStats.Awards(rows, victory);
            LabResultTable.Fill(panel, LabResultTable.RowsFor(awards, local, playedSeconds), style, font);
            panel.SetAsLastSibling();
            PlaceCard();
        }

        private void OnDisable() => RestoreCard();

        /// <summary>
        /// 결과 카드 윗변을 화면 위에서 resultTopFromTop 지점(기본 1/3)에 놓고, 목록을 카드 바로 밑에 다시 붙인다.
        /// 카드 크기·가로 위치는 그대로다.
        /// </summary>
        private void PlaceCard()
        {
            RestoreCard();
            var card = resultControls != null ? resultControls.transform as RectTransform : null;
            if (card == null || panel == null) return;
            cardAnchorMin = card.anchorMin;
            cardAnchorMax = card.anchorMax;
            cardOffsetMin = card.offsetMin;
            cardOffsetMax = card.offsetMax;
            float height = card.rect.height;
            float top = 1f - Mathf.Clamp01(resultTopFromTop);
            card.anchorMin = new Vector2(cardAnchorMin.x, top);
            card.anchorMax = new Vector2(cardAnchorMax.x, top);
            card.offsetMin = new Vector2(cardOffsetMin.x, -height);
            card.offsetMax = new Vector2(cardOffsetMax.x, 0f);
            moved = true;
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, top);
            panel.anchoredPosition = new Vector2(0f, -height - gapBelowResult);
        }

        private void RestoreCard()
        {
            if (!moved) return;
            moved = false;
            var card = resultControls != null ? resultControls.transform as RectTransform : null;
            if (card == null) return;
            card.anchorMin = cardAnchorMin;
            card.anchorMax = cardAnchorMax;
            card.offsetMin = cardOffsetMin;
            card.offsetMax = cardOffsetMax;
        }

        private void EnsurePanel()
        {
            if (panel != null) return;
            if (resultControls == null) return;
            Transform canvas = resultControls.transform.parent;
            if (!(resultControls.transform is RectTransform) || canvas == null) return;
            // 위치는 PlaceCard가 결과 카드 바로 밑으로 정한다.
            panel = LabResultTable.Create(canvas);
        }
    }
}
