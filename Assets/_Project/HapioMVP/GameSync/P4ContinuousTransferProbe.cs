using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using C6.Prototype.Attack;
using C6.Prototype.Battle;
using C6.Prototype.Orbs;
using UnityEngine;

namespace C6.Prototype.GameSync
{
    /// <summary>Explicit development Mac fixture. Inert on iOS and without its launch arguments.</summary>
    [DisallowMultipleComponent]
    public sealed class P4ContinuousTransferProbe : MonoBehaviour
    {
#if DEVELOPMENT_BUILD && UNITY_STANDALONE && !UNITY_EDITOR
        T10GameSession game; T09BattleController controller;
        string output, shared, port, runtimeError; int index, count, pointer = -44000;
        bool active, ownsOutput; Report report; ulong[] roster;
        readonly List<Proof> proofs = new List<Proof>(); readonly HashSet<string> proofKeys = new HashSet<string>();
        readonly List<string> motionLogs = new List<string>(); readonly List<Pass> passes = new List<Pass>();
        static double Now => Time.realtimeSinceStartupAsDouble;
        bool Host => index == 0;
        void Update()
        {
            if (!active || game?.Snapshot == null) return;
            var s = game.Snapshot; string key = s.sessionId + "/" + s.roundId + "/" + s.revision;
            if (proofKeys.Add(key)) proofs.Add(new Proof { session=s.sessionId, round=s.roundId, revision=s.revision, hash=GameWire.CanonicalHash(s) });
            if (!Host && FindObjectsByType<HostProjectile3D>().Length > 0) runtimeError="Client owns authoritative projectile";
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs(); output=Arg(args,"-c6P4Output",null); if(output==null)yield break;
            index=int.Parse(Arg(args,"-c6P4Index","0")); count=int.Parse(Arg(args,"-c6P4Count","3")); port=Arg(args,"-c6P4Port","25243");
            report=new Report { index=index, participants=count, startedAtUtc=DateTime.UtcNow.ToString("O"), buildGuid=Application.buildGUID, unity=Application.unityVersion };
            string error=null; double deadline=Now+170;
            try {
                Require(Path.IsPathRooted(output)&&(!Directory.Exists(output)||!Directory.EnumerateFileSystemEntries(output).Any()),"Fresh absolute output required");
                Require(count>=2&&count<=5&&index>=0&&index<count,"Bad participant index");
                Directory.CreateDirectory(output); ownsOutput=true; shared=Directory.GetParent(output).FullName;
                game=GetComponent<T10GameSession>(); controller=game.Controller;
                Require(game.BuildIdentifier=="24"&&game.ContinuousTransfersEnabled&&controller.ContinuousTransfersEnabled&&controller.OrbPhysicsEnabled,"Build24 passage wiring required");
                Application.runInBackground=true; Application.targetFrameRate=60; Application.logMessageReceived+=Observe; active=true;
            } catch(Exception e){error=e.ToString();}
            var stack=new Stack<IEnumerator>(); stack.Push(Run());
            while(stack.Count>0&&error==null){object next=null;bool more=false;
                try{Require(Now<deadline,"Overall deadline170sec");if(runtimeError!=null)throw new InvalidOperationException(runtimeError);
                    more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;else stack.Pop();}
                catch(Exception e){error=e.ToString();}
                if(error!=null)break;if(more&&next is IEnumerator nested)stack.Push(nested);else if(more)yield return next;
            }
            active=false;Application.logMessageReceived-=Observe;report.status=error==null?"PASS":"FAIL";report.error=error;
            report.proofs=proofs.ToArray();report.passes=passes.ToArray();report.motionLogs=motionLogs.ToArray();report.finishedAtUtc=DateTime.UtcNow.ToString("O");
            if(game!=null)game.Leave();if(ownsOutput)File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(report,true));
            Debug.Log("C6_P4_PROBE_COMPLETE index="+index+" status="+report.status+" error="+error);
            yield return new WaitForSecondsRealtime(.5f);Application.Quit(error==null?0:1);
        }
        IEnumerator Run()
        {
            for(int i=0;i<8;i++)yield return null;
            Require(Host?game.Lobby.CreateRoom("C6 P4 passage",port):game.Lobby.JoinDirect("127.0.0.1",port),"Room request refused");
            yield return Wait(()=>game.Lobby.CanReady,15,"Configuration acknowledgement missing");Write("joined-"+index,new Marker());
            yield return Wait(()=>game.Lobby.Snapshot?.ParticipantCount==count,35,"Expected roster missing");
            roster=game.Lobby.Snapshot.OrderedPlayers.Select(p=>p.clientId).ToArray();report.roster=roster;report.localPlayer=game.Lobby.Connection.LocalClientId??ulong.MaxValue;
            Require(report.localPlayer==roster[index],"Wrong network seat");
            report.left=game.Lobby.Snapshot.LeftPlayerNumber(report.localPlayer);report.right=game.Lobby.Snapshot.RightPlayerNumber(report.localPlayer);
            Require(report.left==(index+count-1)%count+1&&report.right==(index+1)%count+1,"Wrong ring neighbors");
            Require(game.Lobby.ToggleReady(),"Ready refused");
            yield return Wait(()=>game.Lobby.Snapshot?.canStart==true,12,"All Ready missing");yield return Barrier("ready");
            if(Host)Require(game.Lobby.StartMatch(),"Host Start refused");
            yield return Wait(()=>game.InitialConfirmed&&controller.CanInteract,18,"Initial confirmation/Playing missing");
            report.session=game.Snapshot.sessionId;report.width=Screen.width;report.height=Screen.height;
            Require(game.Snapshot.attack.orbs.Length==0&&game.Snapshot.attack.hp==100&&game.Snapshot.resources.players.All(p=>p.stamina==100),"Initial state changed");
            yield return Barrier("empty");
            yield return ContinuousPass("raw-right",OrbKind.Raw,true);
            yield return ContinuousPass("combined-left",OrbKind.Combined,false);
            yield return Capture("01-continuous-stopped");
            // Retry is a result-screen operation. Complete the real battle with explicit
            // Combined fixtures and ordinary release throws; never bypass its Playing gate.
            if (Host)
                for (int i=0;i<5;i++)
                {
                    string id=controller.Attack.Registry.RegisterDevelopmentOrb(roster[0],OrbKind.Combined,
                        OrbPolarity.None,new Vector2(.5f,.55f)).OrbId;
                    controller.Attack.PublishInventoryChange("p4-explicit-real-hit-before-retry");
                    yield return Wait(()=>controller.Views.ContainsKey(id),6,"Victory fixture missing");
                    yield return Swipe(id);
                    int hp=80-i*20;
                    yield return Wait(()=>game.Snapshot.attack.hp==hp,8,"Actual result hit missing");
                    report.actualHits++;
                }
            yield return Wait(()=>game.Snapshot.battle.phase=="Victory"&&game.Snapshot.attack.hp==0,40,"Shared Victory missing");
            report.victory=true;
            uint old=game.Snapshot.roundId;yield return Barrier("before-retry");if(Host)game.Retry();
            yield return Wait(()=>game.Snapshot?.roundId==old+1&&game.InitialConfirmed&&game.Snapshot.battle.phase=="Ready",15,"Retry missing");
            Require(controller.Views.Count==0&&controller.OrbPhysics.Count==0&&!controller.Gestures.HasPending,"Retry leaked rolling state");report.retryClean=true;
            yield return Barrier("retry");if(Host)Require(game.StartPreparedRound(),"Retry start refused");
            yield return Wait(()=>controller.CanInteract,8,"Retry playing missing");
            // A full recipient must not cause a retry loop, reflection, loss, or duplicated ownership.
            if(Host){
                for(int i=0;i<20;i++)controller.Attack.Registry.RegisterDevelopmentOrb(roster[1],OrbKind.Raw,i%2==0?OrbPolarity.Yin:OrbPolarity.Yang,new Vector2(.1f+(i%5)*.2f,.2f+(i/5)*.15f));
                var a=controller.Attack.Registry.RegisterDevelopmentOrb(roster[0],OrbKind.Raw,OrbPolarity.Yin,new Vector2(.5f,.55f));
                controller.Attack.PublishInventoryChange("p4-explicit-capacity-fixture");Write("full-source",new Identity{id=a.OrbId});
            }
            yield return Wait(()=>Exists("full-source"),8,"Full fixture missing");string full=Read<Identity>("full-source").id;
            yield return Wait(()=>Orb(full)!=null&&game.Snapshot.resources.players.Any(p=>p.playerId==roster[1]&&p.storedOrbs==20),8,"Full snapshot missing");yield return Barrier("full-ready");
            if(Host){yield return Push(full,true);yield return Wait(()=>motionLogs.Any(s=>s.Contains("orb="+full)&&s.StartsWith("C6_P4_EDGE_CAPTURE")),5,"Full edge attempt missing");}
            yield return new WaitForSecondsRealtime(2f);
            Require(Orb(full).owner==roster[0]&&Orb(full).transferCount==0,"Full receiver changed ownership");
            if(Host){Require(controller.Views.ContainsKey(full)&&controller.OrbPhysics.TryGetVelocity(full,out var v)&&v==Vector2.zero,"Rejected source not stopped");
                Require(motionLogs.Count(s=>s.StartsWith("C6_P4_EDGE_CAPTURE")&&s.Contains("orb="+full))==1,"Rejected edge retry flood");}
            report.fullRecipientStopped=true;yield return Barrier("full-done");
            report.finalGame=JsonUtility.FromJson<GameSnapshot>(JsonUtility.ToJson(game.Snapshot));
            yield return Capture("02-capacity-stop");yield return Barrier("complete");
        }
        IEnumerator ContinuousPass(string key,OrbKind kind,bool right)
        {
            if(Host){var orb=controller.Attack.Registry.RegisterDevelopmentOrb(roster[0],kind,kind==OrbKind.Raw?OrbPolarity.Yin:OrbPolarity.None,new Vector2(.5f,kind==OrbKind.Raw?.75f:.3f));
                controller.Attack.PublishInventoryChange("p4-explicit-single-push-fixture");Write(key,new Identity{id=orb.OrbId});}
            yield return Wait(()=>Exists(key),8,"Fixture identity missing");string id=Read<Identity>(key).id;
            yield return Wait(()=>Orb(id)!=null,8,"Fixture snapshot missing");yield return Barrier(key+"-ready");
            if(Host){
                yield return Wait(()=>controller.Views.ContainsKey(id),5,"View missing");
                int held=--pointer;Require(controller.BeginPointer(held,controller.GetViewScreenPosition(id),false),"Held grab refused");
                var edge=controller.GetViewScreenPosition(id);edge.x=right?Screen.width-1:1;controller.MovePointer(held,edge);
                yield return new WaitForSecondsRealtime(.3f);Require(Orb(id).transferCount==0&&controller.Views.ContainsKey(id),"Held orb crossed");
                controller.EndPointer(held,edge);yield return new WaitForSecondsRealtime(.1f);
                Require(Orb(id).transferCount==0,"Stationary release crossed");report.heldRetained=true;
                yield return Push(id,right);
            }
            yield return Wait(()=>Orb(id).transferCount>=2,10,"A single push did not cross multiple screens "+key);
            yield return new WaitForSecondsRealtime(5f);
            var final=Orb(id);ulong crossings=final.transferCount;
            Require(crossings>=2&&final.kind==(int)kind&&final.state==(int)OrbAuthorityState.Idle,"Identity changed during rolling");
            int ownerSeat=right?(int)(crossings%(ulong)count):(count-(int)(crossings%(ulong)count))%count;
            Require(final.owner==roster[ownerSeat]&&final.entrySide==(int)(right?EntrySide.Left:EntrySide.Right),"Wrong final ring owner/entry");
            Require(final.rightTransferCount==(right?crossings:0)&&final.hasTransferMotion,"Wrong directional receipt");
            if(index==ownerSeat){Require(controller.Views.ContainsKey(id)&&controller.OrbPhysics.TryGetVelocity(id,out var v)&&v==Vector2.zero,"Friction did not settle receiver");}
            else Require(!controller.Views.ContainsKey(id),"Non-owner retains a physical orb");
            yield return new WaitForSecondsRealtime(.5f);Require(Orb(id).transferCount==crossings,"Stopped orb sent another request");
            passes.Add(new Pass{id=id,kind=kind.ToString(),right=right,crossings=crossings,finalOwner=final.owner,stopped=true,singlePush=true});
            Require(game.Snapshot.attack.hp==100&&game.Snapshot.attack.projectiles.Length==0,"Passive rolling fired attack");
            yield return Barrier(key+"-done");
        }
        IEnumerator Push(string id,bool right)
        {
            Physics2D.SyncTransforms();int p=--pointer;Require(controller.BeginPointer(p,controller.GetViewScreenPosition(id),false),"Push grab refused");
            var area=controller.Hud.OrbWorkspaceScreenRect;var start=new Vector2(area.center.x,controller.GetViewScreenPosition(id).y);
            controller.MovePointer(p,start);yield return new WaitForSecondsRealtime(.2f);
            double begin=Time.unscaledTimeAsDouble;var end=start;
            do{yield return null;float elapsed=(float)(Time.unscaledTimeAsDouble-begin);end=start+Vector2.right*(right?1:-1)*area.width*Mathf.Min(.40f,elapsed*4f);controller.MovePointer(p,end);}while(Time.unscaledTimeAsDouble-begin<.1);
            controller.EndPointer(p,end);report.pushes++;
        }
        IEnumerator Swipe(string id)
        {
            Physics2D.SyncTransforms();int p=--pointer;
            Require(controller.BeginPointer(p,controller.GetViewScreenPosition(id),false),"Throw grab failed");
            var start=new Vector2(Screen.width*.5f,controller.Layout.BottomPixelRect.yMax-Screen.width*.025f);
            controller.MovePointer(p,start);yield return new WaitForSecondsRealtime(.16f);
            double began=Time.unscaledTimeAsDouble;var end=start;
            do{yield return null;float elapsed=(float)(Time.unscaledTimeAsDouble-began);end=start+Vector2.up*(Screen.width*elapsed*1.25f);controller.MovePointer(p,end);}while(Time.unscaledTimeAsDouble-began<.1);
            Require(controller.ThrowArmed&&!controller.Gestures.HasPending,"Throw not armed");controller.EndPointer(p,end);
        }
        OrbWire Orb(string id)=>game.Snapshot?.attack.orbs.FirstOrDefault(o=>o.id==id);
        IEnumerator Capture(string name){yield return null;yield return new WaitForEndOfFrame();var texture=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());Destroy(texture);}
        IEnumerator Barrier(string key){Write(key+"-"+index,new Marker());yield return Wait(()=>Enumerable.Range(0,count).All(i=>Exists(key+"-"+i)),15,"Barrier "+key);}
        bool Exists(string name)=>File.Exists(Path.Combine(shared,name+".json"));
        T Read<T>(string name)=>JsonUtility.FromJson<T>(File.ReadAllText(Path.Combine(shared,name+".json")));
        void Write<T>(string name,T value){string dst=Path.Combine(shared,name+".json"),tmp=dst+".tmp-"+Guid.NewGuid().ToString("N");File.WriteAllText(tmp,JsonUtility.ToJson(value,true));File.Move(tmp,dst);}
        static IEnumerator Wait(Func<bool> test,float seconds,string reason){double until=Now+seconds;while(!test()&&Now<until)yield return null;Require(test(),reason);}
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        static string Arg(string[] args,string key,string fallback){int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
        void Observe(string text,string trace,LogType type){if(type==LogType.Exception||type==LogType.Assert||type==LogType.Error)runtimeError=text;
            if(text.StartsWith("C6_P4_EDGE_CAPTURE",StringComparison.Ordinal)||text.StartsWith("C6_P4_RESUME",StringComparison.Ordinal))motionLogs.Add(text);}
        [Serializable]sealed class Marker{public string value="ready";}
        [Serializable]sealed class Identity{public string id;}
        [Serializable]sealed class Proof{public string session,hash;public uint round;public ulong revision;}
        [Serializable]sealed class Pass{public string id,kind;public bool right,stopped,singlePush;public ulong crossings,finalOwner;}
        [Serializable]sealed class Report{
            public string status,error,startedAtUtc,finishedAtUtc,buildGuid,unity,session;
            public string scope="BUILD24_MAC_DIRECT_IP_EXPLICIT_RAW_COMBINED_CAPACITY_FIXTURES_DEBUG_POINTER";
            public bool physicalTouch,bonjourValidated,heldRetained,retryClean,fullRecipientStopped,victory;
            public int index,participants,width,height,left,right,pushes,actualHits;public ulong localPlayer;public ulong[]roster;
            public Proof[]proofs;public Pass[]passes;public string[]motionLogs;public GameSnapshot finalGame;
        }
#endif
    }
}
