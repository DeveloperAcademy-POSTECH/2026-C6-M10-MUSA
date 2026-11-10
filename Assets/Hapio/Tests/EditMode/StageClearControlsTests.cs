using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Hapio.Tests
{
    public sealed class StageClearControlsTests
    {
        [TestCase("Gumiho",false)]
        [TestCase("Yagwanggui",false)]
        [TestCase("Jigwi",true)]
        public void SavedFinalCutShowsOnlyTheCorrectRoleControls(string name,bool final)
        {
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hapio/Story/StageStories/Views/"+name+"_After_02.prefab"));
            try
            {
                var c=root.GetComponent<StoryStageClearControls>();
                Assert.That(c.finalStage,Is.EqualTo(final));
                c.Present(false,true,true); Assert.That(c.nextSeal.gameObject.activeSelf,Is.False);
                c.Present(true,true,false); Assert.That(c.nextSeal.gameObject.activeSelf,Is.True); Assert.That(c.nextSeal.interactable,Is.False);
                c.Present(true,true,true); Assert.That(c.nextSeal.interactable,Is.True); Assert.That(c.waiting.gameObject.activeSelf,Is.False);
                c.Present(true,false,true); Assert.That(c.nextSeal.gameObject.activeSelf,Is.False);
                Assert.That(c.waiting.gameObject.activeSelf,Is.EqualTo(!final));
                Assert.That(c.heading.gameObject.activeSelf,Is.EqualTo(!final));
                Assert.That(c.waiting.text,Is.EqualTo("방장의 시작을 기다리시오."));
                Assert.That(root.GetComponent<StoryCutView>().requireContinueButton,Is.True);
                Assert.That(root.GetComponent<StoryCutView>().continueButton,Is.Null,"Background and inline story advance cannot bypass the Host gate.");
            }
            finally {Object.DestroyImmediate(root);}
        }
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        static void Set(object o,string name,object value)=>o.GetType().GetField(name,Flags).SetValue(o,value);
        static object Call(GameSession s,string name,params object[] args)=>typeof(GameSession).GetMethod(name,Flags).Invoke(s,args);
        [Test]
        public void SummaryStartIsStickyButAcceptsOnlyTheMatchingCompletedGeneration()
        {
            var root=new GameObject("Inactive summary control fixture");root.SetActive(false);
            try
            {
                var instance=Object.Instantiate(Resources.Load<GameObject>(GameSession.ResourcePath),root.transform);
                var session=instance.GetComponent<GameSession>();
                Set(session,"stageCatalog",GameStageCatalog.RequireDefault());
                var state=new GameFlowState {runId="run",revision=7,stageIndex=3,totalStages=3,phase=GameFlowPhase.Completed,mode=GameMode.Story,battleSessionId="battle",battleRound=4};
                Set(session,"state",state);Set(session,"expectedSettingsRevision",1);Set(session,"expectedSettingsFingerprint","fixture");
                Assert.That(session.StartSummary(),Is.False,"Disconnected/non-host code cannot authorize a summary.");
                var control=Call(session,"MakeControl","state",state.Copy());Set(control,"summaryStarted",true);
                foreach(string field in new[]{"runId","revision","stageIndex","settingsFingerprint"})
                {
                    object old=control.GetType().GetField(field,Flags).GetValue(control);
                    Set(control,field,old is string ? (object)"old" : old is uint ? (object)99u : 1);
                    Call(session,"ReadSummaryStart",control); Assert.That(session.SummaryStarted,Is.False,field);
                    Set(control,field,old);
                }
                var incoming=(GameFlowState)control.GetType().GetField("state",Flags).GetValue(control);
                incoming.battleRound=3;Call(session,"ReadSummaryStart",control);Assert.That(session.SummaryStarted,Is.False);
                incoming.battleRound=4;Call(session,"ReadSummaryStart",control);Assert.That(session.SummaryStarted,Is.True);
                Set(control,"summaryStarted",false);Call(session,"ReadSummaryStart",control);Assert.That(session.SummaryStarted,Is.True);
                Call(session,"ResetResultAdvance");Assert.That(session.SummaryStarted,Is.False);
                Set(control,"summaryStarted",true);state.phase=GameFlowPhase.StageResult;
                Call(session,"ReadSummaryStart",control);Assert.That(session.SummaryStarted,Is.False);
            }
            finally {Object.DestroyImmediate(root);}
        }
    }
}
