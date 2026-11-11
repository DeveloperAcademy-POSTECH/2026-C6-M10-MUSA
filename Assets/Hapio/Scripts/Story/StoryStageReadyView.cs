using System;
using System.Linq;
using C6Lab;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>Local result waiting screen; only GameSession authorizes the shared next stage.</summary>
    public sealed class StoryStageReadyView : MonoBehaviour
    {
        public GameObject waitingPage, party, popup;
        public RectTransform paper, runnerSlot;
        public UnityEngine.UI.Image runner;
        public UnityEngine.UI.Text heading, count, memberCaption, startCaption;
        public UnityEngine.UI.Button quit, confirmQuit, cancelQuit, start;
        public UnityEngine.UI.Graphic startFill;
        public bool IsWaiting => waitingPage.activeSelf;
        public bool IsConfirmingQuit => popup.activeSelf;
        public string RunnerName => current == null ? "" : current.name;
        private GameLoadingArt art;
        private GameLoadingArt.Runner current;
        private float began, squashAt;
        private int frame = -1;
        private bool isHost;
        public const float CaptionSeconds = 3f;

        public static StoryStageReadyView Create(Transform parent)
        {
            var prefab = Resources.Load<StoryStageReadyView>("Hapio/Story/StageReadyView");
            return Instantiate(prefab, parent, false);
        }
        public void BeginWaiting(bool host, string runId, int stage, LabPlayerState[] players, ulong localId)
        {
            isHost = host; began = Time.unscaledTime; frame = -1;
            art = GameLoadingArt.Load();
            int seat = players == null ? 0 : Array.FindIndex(players.OrderBy(p => p.id).ToArray(), p => p.id == localId);
            current = art.runners[RunnerIndex(runId, stage, Math.Max(0, seat), art.runners.Length)];
            waitingPage.SetActive(true);
            GetComponent<StoryPageFit>().referenceSize = new Vector2(host ? 402 : 383.5677f, 874);
            Place(paper, host ? new Rect(-74,-167,704,1041) : new Rect(-224.2249f,-108.7729f,671.5301f,993.5503f));
            Place(runnerSlot, host ? new Rect(62,279,279.339f,260) : new Rect(53,261,279.339f,260));
            ((RectTransform)waitingPage.transform).sizeDelta = new Vector2(host ? 402 : 383.5677f,874);
            Place((RectTransform)popup.transform.GetChild(0),new Rect((host ? 402 : 383.5677f)/2-140.5f,340,281,200));
            UpdateRunner(0);
        }
        public static int RunnerIndex(string run, int stage, int seat, int count)
        {
            if (count < 1) return 0;
            uint hash=2166136261;
            foreach(char c in run ?? "") hash=unchecked((hash^c)*16777619);
            hash=unchecked((hash^(uint)stage)*16777619);
            int[] order=Enumerable.Range(0,count).ToArray();
            for(int i=count-1;i>0;i--) {hash=unchecked(hash*1664525+1013904223);int j=(int)(hash%(uint)(i+1));int tmp=order[i];order[i]=order[j];order[j]=tmp;}
            return order[Math.Max(0,seat)%count];
        }
        public void Present(bool allowQuit, bool canStart, int ready, int total, bool blocked, bool finalStage = false)
        {
            quit.gameObject.SetActive(allowQuit);
            quit.interactable = !blocked;
            if (!IsWaiting) return;
            // Keep the picture behind the modal stable while its busy flag holds the shared start.
            if (IsConfirmingQuit) { start.interactable = false; return; }
            bool all = total > 0 && ready == total;
            party.SetActive(isHost && all);
            runnerSlot.gameObject.SetActive(!isHost || !all);
            heading.gameObject.SetActive(isHost); count.gameObject.SetActive(isHost);
            memberCaption.gameObject.SetActive(!isHost); start.gameObject.SetActive(isHost);
            heading.text = all ? "그대의 시작을 기다리고 있소." : "동지들이 채비 중이오.";
            ((RectTransform)heading.transform.parent).anchoredPosition = new Vector2(201,-((all ? 177 : 184)+16));
            count.text = "전원 준비 완료 " + ready + "/" + total;
            startCaption.text = finalStage ? (canStart ? "마치기" : "마치기 대기")
                : (canStart ? "요괴 봉인 시작" : "요괴 봉인 대기");
            startFill.color = canStart ? GameLobbySeatColors.Ready : GameLobbySeatColors.Disabled;
            start.interactable = canStart && !blocked && !IsConfirmingQuit;
        }
        public void ShowQuit(bool show) => popup.SetActive(show);
        public void ResetView()
        {
            waitingPage.SetActive(false); popup.SetActive(false); quit.gameObject.SetActive(false);
            GetComponent<StoryPageFit>().referenceSize = new Vector2(402,874);
            Place((RectTransform)popup.transform.GetChild(0), new Rect(61,340,281,200));
        }
        private void Update()
        {
            if (!IsWaiting) return;
            float elapsed=Time.unscaledTime-began;
            memberCaption.text = ((int)(elapsed/CaptionSeconds)&1)==0
                ? "방장이 다음 봉인을\n준비하고 있소." : "모두가 준비되면\n함께 이동하오.";
            if(runnerSlot.gameObject.activeSelf) UpdateRunner(elapsed);
        }
        private void UpdateRunner(float elapsed)
        {
            if(current==null || !current.Usable) return;
            int next=GameLoadingView.FrameAt(elapsed,current.frameSeconds,current.frames.Length);
            if(next!=frame) {frame=next; runner.sprite=current.frames[frame];if(GameLoadingView.IsLanding(current,frame))squashAt=Time.unscaledTime;}
            float squash=GameLoadingView.LandingSquash(Time.unscaledTime-squashAt,art.landingSquashSeconds,art.landingSquash);
            runner.rectTransform.localScale=new Vector3(1+squash,1-squash,1);
            runner.preserveAspect=true;
        }
        public static void Place(RectTransform r,Rect b)
        {
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);
            r.anchoredPosition=new Vector2(b.x,-b.y);r.sizeDelta=b.size;
        }
    }
}
