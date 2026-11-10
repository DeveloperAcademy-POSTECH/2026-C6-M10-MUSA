using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace Hapio.Tests
{
 public sealed class OpeningRevisionTests
 {
  [Serializable] class Manifest { public Frame[] frames; }
  [Serializable] class Frame { public int number;public string id,speaker,dialogue;public TextItem[] texts; }
  [Serializable] class TextItem { public string id,richText;public float x,y,width,height; }
  const string Root="Assets/Hapio/Story/Opening/";
  [Test] public void All28CutsUseApprovedDialogueTypographyAndUniqueSourceFrames(){
   var expected=JsonUtility.FromJson<Manifest>(File.ReadAllText(Root+"Revision28.json"));
   var sequence=AssetDatabase.LoadAssetAtPath<StorySequence>(Root+"Opening.asset");
   Assert.That(sequence.Validate(out var error),Is.True,error);Assert.That(sequence.cuts.Count,Is.EqualTo(28));
   foreach(var frame in expected.frames){
    var cut=sequence.cuts[frame.number-1];Assert.That(cut.cutId,Is.EqualTo($"OP_{frame.number:000}"));
    Assert.That(cut.viewPrefab.sourceFigmaNodeId,Is.EqualTo(frame.id));Assert.That(cut.PageCount,Is.EqualTo(1));
    Assert.That(cut.GetPage(0).speaker,Is.EqualTo(frame.speaker));Assert.That(cut.GetPage(0).dialogue,Is.EqualTo(frame.dialogue));
    var texts=cut.viewPrefab.GetComponentsInChildren<Text>(true);Assert.That(texts.Length,Is.EqualTo(frame.texts.Length));
    foreach(var t in frame.texts){var actual=texts.Single(x=>x.name=="Text ["+t.id+"]");Assert.That(actual.text,Is.EqualTo(t.richText));
     Assert.That(actual.rectTransform.sizeDelta.x*actual.rectTransform.localScale.x,Is.EqualTo(t.width).Within(.01));
     Assert.That(actual.rectTransform.anchoredPosition.x,Is.EqualTo(t.x+t.width/2).Within(.01));
     Assert.That(actual.rectTransform.anchoredPosition.y,Is.EqualTo(-t.y-t.height/2).Within(.01));
    }
   }
  }
  [TestCase(7,OpeningLayerMotion.Kind.Sigh,1)]
  [TestCase(12,OpeningLayerMotion.Kind.Sweep,2)]
  [TestCase(13,OpeningLayerMotion.Kind.Toss,4)]
  [TestCase(14,OpeningLayerMotion.Kind.Teeter,1)]
  [TestCase(15,OpeningLayerMotion.Kind.Drop,1)]
  [TestCase(16,OpeningLayerMotion.Kind.Escape,3)]
  public void RequestedMotionIsBoundToIndependentArtwork(int number,OpeningLayerMotion.Kind kind,int count){
   var view=AssetDatabase.LoadAssetAtPath<StoryCutView>(Root+$"Views/OP_{number:000}.prefab");
   Assert.That(view.openingMotions.Count(x=>x.kind==kind),Is.EqualTo(count));
  }
  [Test] public void DropCannotSkipItsFallAndRestoresDeterministicallyWhenReplayed(){
   var prefab=AssetDatabase.LoadAssetAtPath<StoryCutView>(Root+"Views/OP_015.prefab");var view=Object.Instantiate(prefab);
   try{Assert.That(view.waitForEntrance,Is.True);Assert.That(view.EntranceDuration,Is.GreaterThan(2.5));
    var drop=view.openingMotions.Single(x=>x.kind==OpeningLayerMotion.Kind.Drop);var rect=(RectTransform)drop.transform;
    drop.Sample(.1f);var start=rect.anchoredPosition;drop.Sample(1.35f);var middle=rect.anchoredPosition;
    drop.Sample(2.6f);Assert.That(rect.anchoredPosition.y,Is.LessThan(middle.y));Assert.That(middle.y,Is.LessThan(start.y));
    drop.Sample(.1f);drop.Sample(1.35f);Assert.That(rect.anchoredPosition,Is.EqualTo(middle));
   }finally{Object.DestroyImmediate(view.gameObject);}
  }
  [Test] public void LastBackgroundBleedsPastBothEdgesWithoutChangingAspect(){
   var view=AssetDatabase.LoadAssetAtPath<StoryCutView>(Root+"Views/OP_028.prefab");
   var image=view.GetComponentsInChildren<Image>(true).Single(x=>x.sprite!=null&&x.sprite.name=="72eb0");
   Assert.That(image.rectTransform.anchoredPosition.x,Is.EqualTo(-2).Within(.01));
   Assert.That(image.rectTransform.rect.width,Is.EqualTo(406).Within(.01));
   Assert.That(image.rectTransform.rect.width/image.rectTransform.rect.height,Is.EqualTo(402f/874).Within(.001));
  }
  [Test] public void InstructionScrollUsesNativeTextAndLastCutKeepsCloudTransitionLandscape(){
   var scroll=AssetDatabase.LoadAssetAtPath<StoryCutView>(Root+"Views/OP_023.prefab");Assert.That(scroll.GetComponentsInChildren<Text>(true),Has.Length.EqualTo(5));
   var last=AssetDatabase.LoadAssetAtPath<StoryCutView>(Root+"Views/OP_028.prefab");Assert.That(last.GetComponentsInChildren<Image>(true).Any(x=>x.sprite!=null&&x.sprite.name=="72eb0"),Is.True);
  }
 }
}
