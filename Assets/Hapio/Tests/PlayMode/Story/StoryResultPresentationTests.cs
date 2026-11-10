using System.Collections;
using System.Linq;
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
                var cut = view.GetComponent<StoryCutView>();
                var drop = cut.beats.Single(beat => beat != null && beat.motion == StoryVisualBeat.Motion.PaperDrop);
                Assert.That(drop.interactionDelay, Is.GreaterThan(drop.Duration),
                    "Use the real card's delayed interaction, not a fixture where the entrance already unlocks clicks.");
                cut.Sample(0);
                Assert.That(drop.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                int sourcesBeforeSettle = view.GetComponentsInChildren<AudioSource>(true).Length;
                cut.ShowCompletedPose();
                Assert.That(drop.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
                Assert.That(drop.GetComponent<CanvasGroup>().blocksRaycasts, Is.True,
                    "Settling an early-Skipped card must release its delayed interaction gate.");
                Assert.That(view.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(sourcesBeforeSettle),
                    "The quiet completed pose must not create the paper-drop or stamp sound sources.");
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
        [UnityTest] public IEnumerator ContinueKeepsTheCardUntilCoveredAndRevealsNewArtBeforeItsLine()
        {
            root = new GameObject("Story", typeof(RectTransform));
            root.SetActive(false);
            var player = root.AddComponent<StoryPlayer>();
            player.playOnStart = false;
            player.cutHost = (RectTransform)root.transform;
            player.hideInlineContinueButtons = true;
            root.SetActive(true);
            var sequence = UnityEditor.AssetDatabase.LoadAssetAtPath<StorySequence>(
                "Assets/Hapio/Story/StageStories/AfterYagwanggui.asset");
            Assert.That(player.Play(sequence), Is.True);
            var card = player.CurrentView;
            player.AdvanceTime(card.EntranceDuration + .01f);
            Assert.That(card.continueButton.gameObject.activeInHierarchy, Is.True);
            player.RequestSurfaceAdvance();
            Assert.That(player.IsTransitioning, Is.False, "Tapping outside the card must not leave it.");
            card.continueButton.onClick.Invoke();
            Assert.That(player.IsTransitioning, Is.True);
            player.AdvanceTime(.2f);
            player.Next();
            Assert.That(player.CurrentView, Is.SameAs(card));
            player.AdvanceTime(card.exitCloudWipe.SwapTime - .2f);
            Assert.That(player.CurrentCutIndex, Is.EqualTo(1));
            Assert.That(player.IsTransitioning, Is.True);
            Assert.That(player.cutHost.GetChild(player.cutHost.childCount - 1).GetComponent<StoryCloudWipe>(), Is.Not.Null);
            var line = System.Array.Find(player.CurrentView.beats, b => b.name.Contains("4407:2129"));
            var manbok = System.Array.Find(player.CurrentView.layers, l => l.name.Contains("4210:3252"));
            player.AdvanceTime(.3f);
            Assert.That(player.IsTransitioning, Is.True);
            Assert.That(manbok.GetComponent<CanvasGroup>().alpha, Is.GreaterThan(0));
            Assert.That(line.GetComponent<CanvasGroup>().alpha, Is.Zero);
            player.AdvanceTime(1f);
            Assert.That(player.IsTransitioning, Is.False);
            Assert.That(line.GetComponent<CanvasGroup>().alpha, Is.Zero);
            player.RequestNext();
            Assert.That(player.IsReading, Is.False);
            player.AdvanceTime(line.delay + line.duration);
            Assert.That(line.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(player.IsReading, Is.True);
            yield return null;
        }

        StoryCloudWipe SharedWipe()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<StoryCloudWipe>(
                "Assets/Hapio/Story/StageStories/Views/StoryCloudWipe.prefab");
            Assert.That(prefab, Is.Not.Null);
            root = Object.Instantiate(prefab.gameObject);
            ((RectTransform)root.transform).sizeDelta = new Vector2(402, 874);
            return root.GetComponent<StoryCloudWipe>();
        }

        [Test] public void SharedWipeKeepsCoveredFrameAndCallsEachCallbackOnce()
        {
            var wipe = SharedWipe();
            int swaps = 0, completed = 0;
            Assert.That(wipe.TryPlay(() =>
            {
                swaps++;
                Assert.That(root.GetComponentInChildren<CanvasGroup>().alpha, Is.EqualTo(1));
            }, () => completed++), Is.True);
            Assert.That(wipe.TryPlay(() => Assert.Fail("Duplicate request")), Is.False);
            Assert.That(root.GetComponent<Image>().raycastTarget, Is.True);
            wipe.Advance(10);
            Assert.That(swaps, Is.EqualTo(1));
            Assert.That(completed, Is.Zero);
            Assert.That(wipe.IsPlaying, Is.True, "Slow frames must not skip full cover.");
            wipe.Advance(10);
            wipe.Advance(10);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(root.GetComponentInChildren<CanvasGroup>().alpha, Is.Zero);
            Assert.That(root.GetComponent<Image>().raycastTarget, Is.False);
        }

        [Test] public void SharedWipeStaggersCloudsAndSwapsOnlyAfterFullCover()
        {
            var wipe = SharedWipe();
            var serialized = new UnityEditor.SerializedObject(wipe);
            var clouds = serialized.FindProperty("clouds");
            var positions = new Vector2[clouds.arraySize];
            wipe.Sample(0);
            for (int i = 0; i < clouds.arraySize; i++)
                positions[i] = ((RectTransform)clouds.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("rect").objectReferenceValue).anchoredPosition;
            wipe.Sample(wipe.SwapTime * .05f);
            int moved = 0;
            for (int i = 0; i < clouds.arraySize; i++)
                if (((RectTransform)clouds.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("rect").objectReferenceValue).anchoredPosition != positions[i]) moved++;
            Assert.That(moved, Is.InRange(1, clouds.arraySize - 1), "Clouds must not start together.");
            int swaps = 0;
            wipe.TryPlay(() =>
            {
                swaps++;
                Assert.That(root.GetComponentInChildren<CanvasGroup>().alpha, Is.EqualTo(1));
                for (int i = 0; i < clouds.arraySize; i++)
                {
                    var cloud = clouds.GetArrayElementAtIndex(i);
                    Assert.That(((RectTransform)cloud.FindPropertyRelative("rect").objectReferenceValue).anchoredPosition,
                        Is.EqualTo(cloud.FindPropertyRelative("covered").vector2Value));
                }
            });
            wipe.Advance(wipe.SwapTime - .01f);
            Assert.That(swaps, Is.Zero);
            wipe.Advance(.02f);
            Assert.That(swaps, Is.EqualTo(1));
            wipe.Advance(.05f);
            Assert.That(root.GetComponentInChildren<CanvasGroup>().alpha, Is.EqualTo(1));
            wipe.Advance(wipe.Duration);
            for (int i = 0; i < clouds.arraySize; i++)
            {
                var rect = (RectTransform)clouds.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("rect").objectReferenceValue;
                Assert.That(rect.anchoredPosition.x <= -rect.rect.width || rect.anchoredPosition.x >= 402f, Is.True);
            }
        }

        [Test] public void SharedWipeDepartureFinishesInWavesInsteadOfCatchingUpAtOneDeadline()
        {
            var wipe = SharedWipe();
            var serialized = new UnityEditor.SerializedObject(wipe);
            var clouds = serialized.FindProperty("clouds");
            var outside = new Vector2[clouds.arraySize];
            wipe.Sample(wipe.Duration);
            for (int i = 0; i < clouds.arraySize; i++)
                outside[i] = ((RectTransform)clouds.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("rect").objectReferenceValue).anchoredPosition;
            float openSeconds = serialized.FindProperty("openSeconds").floatValue;
            float openStart = wipe.Duration - openSeconds;
            wipe.Sample(openStart + openSeconds * .85f);
            int finished = 0;
            for (int i = 0; i < clouds.arraySize; i++)
                if (((RectTransform)clouds.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("rect").objectReferenceValue).anchoredPosition == outside[i]) finished++;
            Assert.That(finished, Is.InRange(1, clouds.arraySize - 1),
                "Some clouds must have departed while the trailing clouds are still moving.");
            wipe.Sample(wipe.Duration);
            for (int i = 0; i < clouds.arraySize; i++)
                Assert.That(((RectTransform)clouds.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("rect").objectReferenceValue).anchoredPosition, Is.EqualTo(outside[i]));
        }

        [Test] public void SmallCloudsTrailTheLargeCloudsAndLeaveNoStaticOverlay()
        {
            var wipe = SharedWipe();
            var serialized = new UnityEditor.SerializedObject(wipe);
            var clouds = serialized.FindProperty("clouds");
            Assert.That(clouds.arraySize, Is.EqualTo(9));
            var outside = new Vector2[clouds.arraySize];
            wipe.Sample(0f);
            for (int i = 0; i < clouds.arraySize; i++)
                outside[i] = ((RectTransform)clouds.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("rect").objectReferenceValue).anchoredPosition;
            float opening = serialized.FindProperty("openSeconds").floatValue;
            wipe.Sample(wipe.Duration - opening * .08f);
            for (int i = 0; i < 6; i++)
                Assert.That(((RectTransform)clouds.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("rect").objectReferenceValue).anchoredPosition, Is.EqualTo(outside[i]));
            var last = (RectTransform)clouds.GetArrayElementAtIndex(8)
                .FindPropertyRelative("rect").objectReferenceValue;
            Assert.That(last.anchoredPosition, Is.Not.EqualTo(outside[8]));
            wipe.Cancel();
            for (int i = 0; i < clouds.arraySize; i++)
                Assert.That(((RectTransform)clouds.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("rect").objectReferenceValue).anchoredPosition, Is.EqualTo(outside[i]));
        }

        [Test] public void SmallCloudsExitLeftRightLeftWithoutReversingDuringDeparture()
        {
            var wipe = SharedWipe();
            var serialized = new UnityEditor.SerializedObject(wipe);
            var clouds = serialized.FindProperty("clouds");
            float opening = serialized.FindProperty("openSeconds").floatValue;
            float start = wipe.Duration - opening;
            var previous = new float[3];
            for (int i = 0; i < 3; i++)
                previous[i] = clouds.GetArrayElementAtIndex(6 + i).FindPropertyRelative("covered").vector2Value.x;
            for (int frame = 0; frame <= 60; frame++)
            {
                wipe.Sample(start + opening * frame / 60f);
                for (int i = 0; i < 3; i++)
                {
                    var rect = (RectTransform)clouds.GetArrayElementAtIndex(6 + i).FindPropertyRelative("rect").objectReferenceValue;
                    float x = rect.anchoredPosition.x;
                    Assert.That(i == 1 ? x >= previous[i] - .001f : x <= previous[i] + .001f, Is.True);
                    previous[i] = x;
                    if (frame == 60) Assert.That(i == 1 ? x >= 402f : x <= -rect.rect.width, Is.True);
                }
            }
        }

        [Test] public void SharedWipeDisableDropsCallbacksAndCanReplay()
        {
            var wipe = SharedWipe();
            int swaps = 0, completed = 0;
            wipe.TryPlay(() => swaps++, () => completed++);
            wipe.Advance(.1f);
            root.SetActive(false);
            root.SetActive(true);
            wipe.Advance(10);
            Assert.That(swaps + completed, Is.Zero);
            Assert.That(root.GetComponent<Image>().raycastTarget, Is.False);
            Assert.That(wipe.TryPlay(() => swaps++, () => completed++), Is.True);
            wipe.Advance(10);
            wipe.Cancel();
            wipe.Advance(10);
            Assert.That(swaps, Is.EqualTo(1));
            Assert.That(completed, Is.Zero);
            Assert.That(root.GetComponentInChildren<CanvasGroup>().alpha, Is.Zero);
        }

        [UnityTest] public IEnumerator SharedWipeRunsWhileGameTimeIsPaused()
        {
            var wipe = SharedWipe();
            int swaps = 0, completed = 0;
            Time.timeScale = 0;
            Assert.That(wipe.TryPlay(() => swaps++, () => completed++), Is.True);
            float deadline = Time.realtimeSinceStartup + 5;
            while (wipe.IsPlaying && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(swaps, Is.EqualTo(1));
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(wipe.IsPlaying, Is.False);
        }

        [Test] public void EndingNoticeWaitsForDarkenedPicture()
        {
            var sequence = UnityEditor.AssetDatabase.LoadAssetAtPath<StorySequence>(
                "Assets/Hapio/Story/StageStories/Ending.asset");
            root = Object.Instantiate(sequence.cuts[5].viewPrefab.gameObject);
            var cut = root.GetComponent<StoryCutView>();
            var shade = System.Array.Find(cut.layers, l => l.name.Contains("4210:3382"));
            var notice = System.Array.Find(cut.beats, b => b.name.Contains("4407:2127"));
            cut.Sample(shade.EntranceDuration);
            Assert.That(shade.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(notice.GetComponent<CanvasGroup>().alpha, Is.Zero);
            cut.Sample(cut.EntranceDuration + .01f);
            Assert.That(notice.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
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
                    if (player.IsTransitioning)
                    {
                        player.AdvanceTime(1f);
                        player.AdvanceTime(1f);
                    }
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
