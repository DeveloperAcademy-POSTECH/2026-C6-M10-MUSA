using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace Hapio.Tests
{
    public sealed class StoryResultPresentationTests
    {
        private GameObject root;
        [TearDown] public void Clean(){if(root!=null)Object.DestroyImmediate(root);Time.timeScale=1;}
        StoryCardFlip Card()
        {
            root=new GameObject("Card",typeof(RectTransform));root.SetActive(false);
            var card=new GameObject("Turn",typeof(RectTransform)).GetComponent<RectTransform>();card.SetParent(root.transform,false);card.localScale=new Vector3(2,3,1);
            var f=root.AddComponent<StoryCardFlip>();f.card=card;f.front=new GameObject("Front");f.back=new GameObject("Back");f.front.transform.SetParent(card);f.back.transform.SetParent(card);root.SetActive(true);return f;
        }
        [Test] public void FlipSwitchesOnlyAtTheMiddleAndPreservesAuthoredScale()
        {
            var f=Card();int flips=0;f.onFlipped.AddListener(()=>flips++);f.Flip();f.Advance(f.flipSeconds*.25f);
            Assert.That(f.front.activeSelf,Is.True);Assert.That(f.IsBack,Is.False);f.Advance(f.flipSeconds*.3f);
            Assert.That(f.IsBack,Is.True);Assert.That(f.back.activeSelf,Is.True);f.Advance(10);
            Assert.That(f.card.localScale,Is.EqualTo(new Vector3(2,3,1)));Assert.That(flips,Is.EqualTo(1));
            f.Flip();f.Advance(10);Assert.That(f.IsBack,Is.False);Assert.That(flips,Is.EqualTo(2));
        }
        [Test] public void RepeatedClicksDuringATurnDoNotStartAnotherTurnAndReenableResetsFront()
        {
            var f=Card();f.Flip();f.Flip();f.Advance(10);Assert.That(f.IsBack,Is.True);
            f.Flip();f.Advance(.1f);root.SetActive(false);root.SetActive(true);
            Assert.That(f.IsBack||f.IsFlipping,Is.False);Assert.That(f.card.localScale,Is.EqualTo(new Vector3(2,3,1)));
        }
        [Test] public void HorizontalSwipeFlipsOnceAndTheReleaseClickCannotFlipItAgain()
        {
            var f=Card();var es=new GameObject("Events",typeof(EventSystem));try{
            var e=new PointerEventData(es.GetComponent<EventSystem>()){pointerId=1,position=Vector2.zero};
            f.OnPointerDown(e);f.OnBeginDrag(e);e.position=new Vector2(100,0);f.OnEndDrag(e);f.OnPointerClick(e);f.Advance(10);
            Assert.That(f.IsBack,Is.True);Assert.That(f.IsFlipping,Is.False);
            f.OnPointerDown(e);f.OnPointerClick(e);f.Advance(10);Assert.That(f.IsBack,Is.False);
            }finally{Object.DestroyImmediate(es);}
        }
        [Test] public void VerticalAndShortDragsDoNotTurnTheCard()
        {
            var f=Card();var es=new GameObject("Events",typeof(EventSystem));try{
            var e=new PointerEventData(es.GetComponent<EventSystem>()){position=Vector2.zero};f.OnPointerDown(e);f.OnBeginDrag(e);e.position=new Vector2(0,200);f.OnEndDrag(e);f.Advance(10);Assert.That(f.IsBack,Is.False);
            }finally{Object.DestroyImmediate(es);}
        }
        StorySealGauge Gauge()
        {
            root=new GameObject("Gauge");root.SetActive(false);var f=root.AddComponent<StorySealGauge>();
            var fill=new GameObject("Fill",typeof(RectTransform),typeof(Image));fill.transform.SetParent(root.transform);f.fill=fill.GetComponent<Image>();f.fill.type=Image.Type.Filled;
            var label=new GameObject("Percent",typeof(RectTransform),typeof(Text));label.transform.SetParent(root.transform);f.percentage=label.GetComponent<Text>();root.SetActive(true);return f;
        }
        [Test] public void GaugeStartsAtZeroAndEasesToTheSuppliedValueWithoutOvershoot()
        {
            var g=Gauge();g.Show(.78f);Assert.That(g.Displayed,Is.Zero);Assert.That(g.percentage.text,Is.EqualTo("0%"));g.Advance(g.delay*.5f);Assert.That(g.Displayed,Is.Zero);
            float previous=0;for(int i=0;i<30;i++){g.Advance(.1f);Assert.That(g.Displayed,Is.InRange(previous,.78f));previous=g.Displayed;}
            Assert.That(g.fill.fillAmount,Is.EqualTo(.78f).Within(.0001f));Assert.That(g.percentage.text,Is.EqualTo("78%"));
        }
        [Test] public void NewResultsAndReopeningRestartTheGaugeAndInvalidInputsAreBounded()
        {
            var g=Gauge();g.Show(2);g.Advance(10);Assert.That(g.Displayed,Is.EqualTo(1));g.Show(-1);g.Advance(10);Assert.That(g.Displayed,Is.Zero);
            g.Show(float.NaN);g.Advance(10);Assert.That(g.Displayed,Is.Zero);g.Show(.4f);g.Advance(10);root.SetActive(false);root.SetActive(true);Assert.That(g.Displayed,Is.Zero);g.Advance(10);Assert.That(g.Displayed,Is.EqualTo(.4f));
        }
#if UNITY_EDITOR
        [UnityTest] public IEnumerator ActualCardRaycastTurnsThePaperAndBackButtonDoesNotTurnIt()
        {
            root=new GameObject("Card Canvas",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32000;
            var source=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hapio/Story/StageStories/Views/Gumiho_SealCard.prefab");
            var view=Object.Instantiate(source,root.transform,false);var flip=view.GetComponentInChildren<StoryCardFlip>(true);
            var events=EventSystem.current;GameObject ownedEvents=null;if(events==null){ownedEvents=new GameObject("Events",typeof(EventSystem));events=ownedEvents.GetComponent<EventSystem>();}
            try
            {
                yield return null;Canvas.ForceUpdateCanvases();
                var e=new PointerEventData(events){position=RectTransformUtility.WorldToScreenPoint(null,flip.card.TransformPoint(Vector2.zero))};
                var hits=new System.Collections.Generic.List<RaycastResult>();events.RaycastAll(e,hits);
                Assert.That(hits.Count,Is.GreaterThan(0));Assert.That(hits[0].gameObject,Is.SameAs(flip.gameObject));
                ExecuteEvents.Execute(hits[0].gameObject,e,ExecuteEvents.pointerClickHandler);flip.Advance(10);Assert.That(flip.IsBack,Is.True);
                yield return null;Canvas.ForceUpdateCanvases();
                int inspections=0;view.GetComponent<StoryResultActions>().onInspectGourd.AddListener(()=>inspections++);
                var button=flip.back.GetComponentInChildren<Button>();var rect=(RectTransform)button.transform;
                e.position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));Canvas.ForceUpdateCanvases();hits.Clear();events.RaycastAll(e,hits);
                Assert.That(hits[0].gameObject,Is.SameAs(button.gameObject));ExecuteEvents.Execute(hits[0].gameObject,e,ExecuteEvents.pointerClickHandler);
                Assert.That(inspections,Is.EqualTo(1));Assert.That(flip.IsBack&&!flip.IsFlipping,Is.True);
            }
            finally{if(ownedEvents!=null)Object.DestroyImmediate(ownedEvents);}
        }
        [UnityTest] public IEnumerator ActualSuccessAndFailureGroupsPlayWithoutRetainingPreviousPictures()
        {
            foreach(string name in new[]{"AllStageStories","Failure"})
            {
                root=new GameObject("Story Preview",typeof(RectTransform));root.SetActive(false);
                var player=root.AddComponent<StoryPlayer>();player.playOnStart=false;player.cutHost=(RectTransform)root.transform;root.SetActive(true);
                var sequence=UnityEditor.AssetDatabase.LoadAssetAtPath<StorySequence>("Assets/Hapio/Story/StageStories/"+name+".asset");
                int ended=0;player.Finished+=reason=>ended++;Assert.That(player.Play(sequence),Is.True);
                for(int i=0;i<sequence.cuts.Count;i++)
                {
                    Assert.That(player.CurrentCutIndex,Is.EqualTo(i));if(!player.IsReading)player.Next();
                    var view=player.CurrentView;yield return null;
                    Assert.That(root.GetComponentsInChildren<StoryCutView>(true).Length,Is.EqualTo(1));
                    var flip=view.GetComponentInChildren<StoryCardFlip>(true);
                    if(flip!=null){flip.Flip();flip.Advance(10);Assert.That(flip.IsBack,Is.True);}
                    for (int page = 1; page < sequence.cuts[i].PageCount; page++)
                    {
                        player.Next();
                        Assert.That(player.CurrentCutIndex, Is.EqualTo(i));
                        Assert.That(player.CurrentPageIndex, Is.EqualTo(page));
                        Assert.That(player.CurrentView, Is.SameAs(view));
                    }
                    player.Next();
                }
                Assert.That(player.IsPlaying,Is.False);Assert.That(ended,Is.EqualTo(1));Object.DestroyImmediate(root);root=null;
            }
        }
#endif
        [UnityTest] public IEnumerator CardAndGaugeContinueWhileTheGameClockIsPaused()
        {
            var f=Card();var g=root.AddComponent<StorySealGauge>();g.duration=.05f;g.delay=0;g.Show(.65f);Time.timeScale=0;f.flipSeconds=.05f;f.Flip();
            yield return new WaitForSecondsRealtime(.15f);Assert.That(f.IsBack,Is.True);Assert.That(g.Displayed,Is.EqualTo(.65f).Within(.001f));
        }
    }
}
