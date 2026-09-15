using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using C6.Prototype.Attack;
using C6.Prototype.Battle;
using C6.Prototype.Lobby;
using C6.Prototype.Orbs;
using UnityEngine;

namespace C6.Prototype.GameSync
{
    /// <summary>Explicit new Mac process diagnostics. Never enabled by ordinary gameplay or on iOS.</summary>
    [DisallowMultipleComponent]
    public sealed class P3MultiplayerProbe : MonoBehaviour
    {
#if DEVELOPMENT_BUILD && UNITY_STANDALONE && !UNITY_EDITOR
        T10GameSession game; T09BattleController controller;
        string output, shared, port, runtimeError; int index, count, pointer = -33000;
        bool active, ownsOutput; Report report; ulong[] roster;
        readonly List<Proof> proofs = new List<Proof>(); readonly HashSet<string> proofKeys = new HashSet<string>();
        readonly List<Recovery> recoveries = new List<Recovery>();
        static double Now => Time.realtimeSinceStartupAsDouble;
        bool Host => index == 0;
        void Update()
        {
            if (!active || game?.Snapshot == null) return;
            var s=game.Snapshot; string key=s.sessionId+"/"+s.roundId+"/"+s.revision;
            if (proofKeys.Add(key)) proofs.Add(new Proof { session=s.sessionId, round=s.roundId, revision=s.revision, hash=GameWire.CanonicalHash(s), activeOrbs=s.attack.orbs.Length, flying=s.attack.projectiles.Length });
            report.maximumObservedOrbs=Math.Max(report.maximumObservedOrbs,s.attack.orbs.Length);
            report.maximumObservedFlying=Math.Max(report.maximumObservedFlying,s.attack.projectiles.Length);
            if(s.attack.orbs.Length==200&&s.attack.projectiles.Length==100)report.fullCapacitySimultaneouslyObserved=true;
            report.maximumObservedJsonBytes=Math.Max(report.maximumObservedJsonBytes,Encoding.UTF8.GetByteCount(JsonUtility.ToJson(s)));
            if (!Host && FindObjectsByType<HostProjectile3D>().Length>0) runtimeError="Client owns authoritative projectile.";
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs(); output=Arg(args,"-c6P3Output",null); if(output==null)yield break;
            index=int.Parse(Arg(args,"-c6P3Index","0")); count=int.Parse(Arg(args,"-c6P3Count","3")); port=Arg(args,"-c6P3Port","25153");
            report=new Report { index=index, participants=count, startedAtUtc=DateTime.UtcNow.ToString("O"),buildGuid=Application.buildGUID,unity=Application.unityVersion };
            string error=null;double deadline=Now+230;
            try {
                Require(Path.IsPathRooted(output)&&(!Directory.Exists(output)||!Directory.EnumerateFileSystemEntries(output).Any()),"Fresh absolute output required.");
                Require(count>=2&&count<=5&&index>=0&&index<=count,"Invalid probe participant index.");
                Directory.CreateDirectory(output);ownsOutput=true;shared=Directory.GetParent(output).FullName;
                game=GetComponent<T10GameSession>();controller=game.Controller;
                Require(game.BuildIdentifier=="23"&&game.MaximumParticipants==5&&game.Lobby.MaximumParticipants==5&&controller.MaximumParticipants==5
                    &&controller.OrbPhysicsEnabled&&controller.ReleaseThrowsEnabled,"Build23 opt-in wiring required.");
                Application.runInBackground=true;Application.targetFrameRate=60;Application.logMessageReceived+=Observe;active=true;
            } catch(Exception e){error=e.ToString();}
            var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0&&error==null){object next=null;bool more=false;
                try {Require(Now<deadline,"P3 overall deadline230sec.");if(runtimeError!=null)throw new InvalidOperationException(runtimeError);
                    more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;else stack.Pop();}
                catch(Exception e){error=e.ToString();}
                if(error!=null)break;if(more&&next is IEnumerator nested)stack.Push(nested);else if(more)yield return next;
            }
            active=false;Application.logMessageReceived-=Observe;report.status=error==null?"PASS":"FAIL";report.error=error;
            report.proofs=proofs.ToArray();report.recoveries=recoveries.ToArray();report.finishedAtUtc=DateTime.UtcNow.ToString("O");
            if(game!=null)game.Leave();if(ownsOutput)File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(report,true));
            Debug.Log("C6_P3_PROBE_COMPLETE index="+index+" status="+report.status+" error="+error);
            yield return new WaitForSecondsRealtime(.5f);Application.Quit(error==null?0:1);
        }
        IEnumerator Run()
        {
            for(int i=0;i<8;i++)yield return null;
            var display=Screen.mainWindowDisplayInfo;var move=Screen.MoveMainWindowTo(display,new Vector2Int(15+index*325,50));
            yield return Wait(()=>move==null||move.isDone,5,"Window move timeout");
            report.width=Screen.width;report.height=Screen.height;
            Require(Host?game.Lobby.CreateRoom("C6 P3 circle",port):game.Lobby.JoinDirect("127.0.0.1",port),"Room request refused");
            if(index==count){
                yield return Wait(()=>game.Lobby.Connection.State==C6.Prototype.Networking.DirectConnectionState.Failed,12,"Sixth was not rejected");
                report.rejection=game.Lobby.Connection.LastApprovalReason+" "+game.Lobby.Connection.Message;
                Require(report.rejection.Contains("FULL",StringComparison.OrdinalIgnoreCase)||report.rejection.Contains("full",StringComparison.OrdinalIgnoreCase),"Wrong sixth rejection: "+report.rejection);
                report.sixthRejected=true;Write("overflow-done",new Marker());yield break;
            }
            yield return Wait(()=>game.Lobby.CanReady,15,"Configuration acknowledgement missing");
            Write("joined-"+index,new Marker()); if(Host)Write("host-open",new Marker());
            yield return Wait(()=>game.Lobby.Snapshot?.ParticipantCount==count,25,"Full expected roster missing");
            if(count==5)yield return Wait(()=>Exists("overflow-done"),20,"Sixth admission check missing");
            // First Ready sweep tests all gates and then a real lobby departure/rejoin.
            yield return ReadySweep("before-reseat");
            if(index==count-1){game.Lobby.Leave();yield return Wait(()=>game.Lobby.Connection.CanStart,8,"Leaving peer transport not cleared");Write("peer-left",new Marker());}
            else {
                yield return Wait(()=>game.Lobby.Snapshot?.ParticipantCount==count-1,10,"Lobby did not preserve surviving peers");
                Require(game.Lobby.Snapshot.OrderedPlayers.All(p=>!p.ready)&&!game.Lobby.CanStart,"Roster change retained Ready");
                Write("reseat-seen-"+index,new Marker());
            }
            if(index==count-1){
                yield return Wait(()=>Enumerable.Range(0,count-1).All(i=>Exists("reseat-seen-"+i)),10,"Survivors missing reseat");
                Require(game.Lobby.JoinDirect("127.0.0.1",port),"Manual rejoin refused");yield return Wait(()=>game.Lobby.CanReady,15,"Rejoin config missing");
            }
            yield return Wait(()=>game.Lobby.Snapshot?.ParticipantCount==count,15,"Rejoined roster missing");
            report.lobbyReseat=true; yield return Barrier("rejoined");yield return ReadySweep("final-ready");
            roster=game.Lobby.Snapshot.OrderedPlayers.Select(p=>p.clientId).ToArray();report.roster=roster;report.localPlayer=game.Lobby.Connection.LocalClientId??ulong.MaxValue;
            Require(report.localPlayer==roster[index],"Actual local network identity does not match assigned seat");
            Require(game.Lobby.Snapshot.LocalPlayerNumber(report.localPlayer)==index+1,"Wrong local player number");
            report.left=game.Lobby.Snapshot.LeftPlayerNumber(report.localPlayer);report.right=game.Lobby.Snapshot.RightPlayerNumber(report.localPlayer);
            Require(report.left==(index+count-1)%count+1&&report.right==(index+1)%count+1,"Wrong circle neighbors");
            yield return Capture("01-lobby-ready");yield return Barrier("lobby-captured");
            // Hold the final, rejoined client's ACK through the real named-message path. Earlier
            // clients can respond normally, but no player may interact until the final ACK arrives.
            if(index==count-1)game.DevelopmentHoldInitialAck=true;
            yield return Barrier("initial-ack-held");
            if(Host)Require(game.Lobby.StartMatch(),"Host Start refused");
            yield return Wait(()=>game.Attached&&game.Snapshot?.battle.phase=="Ready",10,"Initial Ready snapshot missing while ACK held");
            if(Host){
                yield return Wait(()=>game.InitialAcks==count-2,8,"Earlier clients did not confirm initial state");
                yield return new WaitForSecondsRealtime(.4f);
                Require(!game.InitialConfirmed&&!game.CanStart&&!controller.CanInteract&&!game.StartPreparedRound(),"Missing final client ACK did not block Start");
                Write("initial-ack-blocked",new Marker());
            }
            yield return Wait(()=>Exists("initial-ack-blocked"),10,"Host did not verify final ACK gate");
            if(index==count-1)game.DevelopmentHoldInitialAck=false;
            report.initialAckGate=true;
            yield return Wait(()=>game.InitialConfirmed&&controller.CanInteract,18,"All initial game ACKs not confirmed");
            if(Host)Require(game.InitialAcks==count-1,"Initial ACK count does not match every remote participant");
            report.session=game.Snapshot.sessionId;report.room=game.Snapshot.roomId;
            Require(controller.Views.Count==0&&game.Snapshot.attack.orbs.Length==0&&game.Snapshot.attack.projectiles.Length==0&&game.Snapshot.attack.hp==100
                &&game.Snapshot.resources.players.All(p=>p.stamina==100&&p.generatedTotal==0)&&game.Snapshot.battle.duration==180,"Bad initial state");
            yield return Barrier("initial-empty-verified");
            yield return Generate();Write("raw-"+index,new Identity{id=controller.Resource.LastResult.confirmedOrb.id});
            yield return Barrier("generated");yield return Capture("02-playing");
            string raw=Read<Identity>("raw-0").id;
            for(int step=0;step<count;step++)yield return Transfer(raw,true,"raw-right-"+step);
            for(int step=0;step<count;step++)yield return Transfer(raw,false,"raw-left-"+step);
            // A direct normal-release combination, using clearly labelled polarity fixtures.
            if(Host){
                var a=controller.Attack.Registry.RegisterDevelopmentOrb(roster[0],OrbKind.Raw,OrbPolarity.Yin,new Vector2(.3f,.65f));
                var b=controller.Attack.Registry.RegisterDevelopmentOrb(roster[0],OrbKind.Raw,OrbPolarity.Yang,new Vector2(.7f,.65f));
                controller.Attack.PublishInventoryChange("p3-explicit-two-polarity-fixture");Write("combine-input",new Pair{a=a.OrbId,b=b.OrbId});report.explicitRawFixtures+=2;
            }
            yield return Wait(()=>Exists("combine-input"),8,"Combination fixture missing");var pair=Read<Pair>("combine-input");
            if(Host){
                yield return Wait(()=>controller.Views.ContainsKey(pair.a)&&controller.Views.ContainsKey(pair.b),6,"Raw views missing");
                int p=--pointer;Require(controller.BeginPointer(p,controller.GetViewScreenPosition(pair.a),false),"Combine grab failed");
                var target=controller.GetViewScreenPosition(pair.b);controller.MovePointer(p,target);controller.EndPointer(p,target);
                yield return Wait(()=>!controller.HasCombinationPending&&game.Snapshot.attack.orbs.Any(o=>o.kind==(int)OrbKind.Combined),6,"Direct combination failed");
                Write("combined",new Identity{id=game.Snapshot.attack.orbs.Single(o=>o.kind==(int)OrbKind.Combined).id});
            }
            yield return Wait(()=>Exists("combined"),8,"Combined identity missing");string combined=Read<Identity>("combined").id;
            yield return Wait(()=>Orb(combined)!=null,6,"Combined snapshot missing");
            for(int step=0;step<count;step++)yield return Transfer(combined,true,"comb-right-"+step);
            for(int step=0;step<count+1;step++)yield return Transfer(combined,false,"comb-left-"+step);
            Require(Orb(combined).owner==roster[count-1],"Last Client not final attacker");
            if(index==count-1){yield return Generate();yield return Swipe(combined);}
            yield return Wait(()=>game.Snapshot.attack.hp==80&&game.Snapshot.attack.roundHits==1,10,"Shared Client real hit absent");
            report.clientHit=true;yield return Capture("03-client-real-hit");yield return Barrier("client-hit");
            if(Host){
                Require(recoveries.Count==1&&recoveries[0].attacker==roster[count-1]&&Math.Abs(recoveries[0].added-5)<.0001,"Attacker-only+5 receipt missing");
                for(int i=0;i<4;i++){
                    string id=controller.Attack.Registry.RegisterDevelopmentOrb(roster[0],OrbKind.Combined,OrbPolarity.None,new Vector2(.5f,.55f)).OrbId;
                    report.explicitCombinedFixtures++;controller.Attack.PublishInventoryChange("p3-explicit-victory-fixture");
                    yield return Wait(()=>controller.Views.ContainsKey(id),6,"Victory fixture view missing");yield return Swipe(id);
                    int hp=60-i*20;yield return Wait(()=>game.Snapshot.attack.hp==hp,8,"Actual victory hit missing");
                }
            }
            yield return Wait(()=>game.Snapshot.battle.phase=="Victory"&&game.Snapshot.attack.hp==0&&game.Snapshot.attack.state=="Ended"&&!game.Snapshot.resources.playing,30,"Shared settled Victory missing");
            var frozen=FrozenResultHash(game.Snapshot);File.WriteAllText(Path.Combine(output,"terminal-before.json"),JsonUtility.ToJson(game.Snapshot,true));yield return new WaitForSecondsRealtime(.7f);
            File.WriteAllText(Path.Combine(output,"terminal-after.json"),JsonUtility.ToJson(game.Snapshot,true));
            Require(FrozenResultHash(game.Snapshot)==frozen,"Terminal gameplay result mutated");report.victory=true;
            yield return Capture("04-victory");uint old=game.Snapshot.roundId;yield return Barrier("victory");
            if(Host)game.Retry();
            yield return Wait(()=>game.Snapshot?.roundId==old+1&&game.InitialConfirmed&&game.Snapshot.battle.phase=="Ready",15,"Retry/all-ACK missing");
            Require(controller.Views.Count==0&&controller.OrbPhysics.Count==0&&!controller.Gestures.HasPending&&!controller.HasCombinationPending
                &&game.Snapshot.attack.projectiles.Length==0&&game.Snapshot.resources.players.All(p=>p.stamina==100&&p.generatedTotal==0),"Retry leaked prior state");
            report.retryClean=true;yield return Barrier("retry");if(Host)Require(game.StartPreparedRound(),"Retry start refused");
            yield return Wait(()=>controller.CanInteract,8,"Retry not playing");
            if(count==5){yield return CapacityFixture();}
            else {yield return Generate();yield return Barrier("next-round-generate");}
            report.finalGame=Copy(game.Snapshot);yield return Barrier("before-disconnect");
            if(index==count-1)game.Leave();
            yield return Wait(()=>controller.Views.Count==0&&controller.OrbPhysics.Count==0&&controller.Attack.ActiveProjectileCount==0
                &&!controller.Gestures.HasPending&&!controller.CanInteract&&(index==count-1?!game.Attached:game.Error.Length>0),15,"Battle departure failed to stop peers");
            report.disconnectReason=game.Error;game.Leave();
            yield return Wait(()=>!game.Attached&&game.Snapshot==null&&game.Lobby.Connection.CanStart,8,"END failed to clear interrupted state");
            report.disconnectClean=true;
        }
        IEnumerator ReadySweep(string key)
        {
            for(int i=0;i<count;i++){
                if(index==i){Require(game.Lobby.CanReady&&game.Lobby.ToggleReady(),"Ready intent refused");
                    yield return Wait(()=>game.Lobby.Snapshot?.Find(game.Lobby.Connection.LocalClientId??ulong.MaxValue)?.ready==true,8,"Ready not approved");Write(key+"-"+i,new Marker());}
                yield return Wait(()=>Exists(key+"-"+i),12,"Ready sequence missing");
                if(i<count-1&&Host){Require(!game.Lobby.CanStart&&!game.Lobby.StartMatch(),"Unready member did not block Start");report.unreadyStartRejected=true;}
                // Do not allow the next participant to become Ready before the Host checks this step.
                yield return Barrier(key+"-step-"+i);
            }
            yield return Wait(()=>game.Lobby.Snapshot?.canStart==true,8,"All Ready did not enable Start");
            yield return Barrier(key+"-complete");
        }
        IEnumerator Transfer(string id,bool right,string key)
        {
            yield return Wait(()=>Orb(id)!=null,6,"Transfer source missing");var before=Copy(Orb(id));int sender=Array.IndexOf(roster,before.owner);int recipient=(sender+(right?1:count-1))%count;
            yield return Barrier(key+"-source-observed");
            if(index==sender){
                Physics2D.SyncTransforms();int p=--pointer;Require(controller.BeginPointer(p,controller.GetViewScreenPosition(id),false),"Transfer grab failed "+key);
                Rect lower=controller.Layout.BottomPixelRect;var end=new Vector2(right?lower.xMax-lower.width*.015f:lower.xMin+lower.width*.015f,controller.GetViewScreenPosition(id).y);
                var start=controller.GetViewScreenPosition(id);for(int i=1;i<=7;i++){controller.MovePointer(p,Vector2.Lerp(start,end,i/7f));yield return null;}
                Require(Orb(id).transferCount==before.transferCount&&controller.Views.ContainsKey(id)&&!controller.Gestures.HasPending,"Transfer occurred before release");
                controller.EndPointer(p,end);
            }
            yield return Wait(()=>Orb(id)?.transferCount==before.transferCount+1,8,"Transfer not committed "+key);
            var after=Orb(id);Require(after.owner==roster[recipient]&&after.kind==before.kind&&after.polarity==before.polarity&&after.entrySide==(int)(right?EntrySide.Left:EntrySide.Right),"Wrong transfer identity/recipient/edge");
            if(index==sender)Require(!controller.Views.ContainsKey(id),"Sender retained orb");
            if(index==recipient){yield return Wait(()=>controller.Views.ContainsKey(id),3,"Receiver view absent");Require(controller.OrbPhysics.TryGetVelocity(id,out var velocity)&&velocity.sqrMagnitude<.0001f,"Receiving inherited motion");}
            report.transfers++;yield return Barrier(key);
        }
        IEnumerator CapacityFixture()
        {
            if(Host){
                // Explicit load fixture: each actual approved owner gets20 normal mapped launches and20 stored Raw.
                // Calls the real authority, actual SpawnProjectile and normal aggregate publish; no synthetic client state is applied.
                var spawn=typeof(AttackSession).GetMethod("SpawnProjectile",BindingFlags.Instance|BindingFlags.NonPublic);
                Require(spawn!=null,"Capacity spawn hook missing");
                foreach(ulong owner in roster)for(int i=0;i<20;i++){
                    var orb=controller.Attack.Registry.RegisterDevelopmentOrb(owner,OrbKind.Combined,OrbPolarity.None,new Vector2(.5f,.55f));
                    var request=new OrbActionRequest(controller.Attack.Registry.SessionId,controller.Attack.Registry.RoundId,"p3-load-"+owner+"-"+i,orb.OrbId,string.Empty,OrbActionKind.Launch,(ulong)(i+1),new Vector2(.5f,.7f),throwInput:new OrbThrowInput(new Vector2(.14f,.125f),.1f));
                    var result=controller.Attack.Authority.RequestLaunch(owner,request,true);Require(result.Accepted&&result.SpawnRequired,"Capacity authority refused valid launch");
                    spawn.Invoke(controller.Attack,new object[]{result.Orb,result.BallisticLaunch});
                }
                foreach(ulong owner in roster)for(int i=0;i<20;i++)controller.Attack.Registry.RegisterDevelopmentOrb(owner,OrbKind.Raw,i%2==0?OrbPolarity.Yin:OrbPolarity.Yang,new Vector2(.1f+(i%5)*.2f,.2f+(i/5)*.15f));
                report.explicitCombinedFixtures+=100;report.explicitRawFixtures+=100;
                controller.Attack.PublishInventoryChange("p3-explicit-100-stored-plus100-real-flying-load");
            }
            yield return Wait(()=>report.fullCapacitySimultaneouslyObserved,8,"Full200/100 wire capacity never received");
            report.capacityReceived=true;yield return Capture("05-capacity");
            var label=controller.Hud.RecoveryLabel;
            report.allStaminaVisible=label.preferredWidth<=label.rectTransform.rect.width+.01f&&Enumerable.Range(1,5).All(i=>label.text.Contains("P"+i+":100"));
            Require(report.allStaminaVisible,"Five stamina values are clipped in the narrow player viewport");
            yield return Wait(()=>game.Snapshot.attack.projectiles.Length==0&&game.Snapshot.attack.orbs.Length==100,9,"Capacity expiry lost stored orbs");
            Require(game.Snapshot.attack.hp==100&&game.Snapshot.resources.players.All(p=>p.storedOrbs==20),"Miss-only capacity fixture damaged target/lost storage");
            yield return Barrier("capacity-complete");
        }
        IEnumerator Generate(){int before=controller.Resource.LocalPlayer.generatedTotal;Require(controller.RequestGenerate(),"Paid Generate refused");
            yield return Wait(()=>!controller.Resource.HasPending&&controller.Resource.LocalPlayer.generatedTotal==before+1,8,"Generate receipt missing");
            var r=controller.Resource.LastResult;Require(r.accepted&&Math.Abs(r.staminaBefore-r.staminaAfter-20)<.0001,"Wrong paid cost");report.paidGenerations++;}
        IEnumerator Swipe(string id){Physics2D.SyncTransforms();int p=--pointer;Require(controller.BeginPointer(p,controller.GetViewScreenPosition(id),false),"Throw grab failed");
            var start=new Vector2(Screen.width*.5f,controller.Layout.BottomPixelRect.yMax-Screen.width*.025f);controller.MovePointer(p,start);yield return new WaitForSecondsRealtime(.16f);
            double began=Time.unscaledTimeAsDouble;var end=start;do{yield return null;float elapsed=(float)(Time.unscaledTimeAsDouble-began);end=start+Vector2.up*(Screen.width*elapsed*1.25f);controller.MovePointer(p,end);}while(Time.unscaledTimeAsDouble-began<.1);
            Require(controller.ThrowArmed&&!controller.Gestures.HasPending,"Throw not armed");controller.EndPointer(p,end);}
        OrbWire Orb(string id)=>game.Snapshot?.attack.orbs.FirstOrDefault(o=>o.id==id);
        IEnumerator Capture(string name){yield return null;yield return new WaitForEndOfFrame();File.WriteAllText(Path.Combine(output,name+".json"),(game.Snapshot!=null?JsonUtility.ToJson(game.Snapshot,true):JsonUtility.ToJson(game.Lobby.Snapshot,true)));
            var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());Destroy(image);}
        IEnumerator Barrier(string key){Write(key+"-"+index,new Marker());yield return Wait(()=>Enumerable.Range(0,count).All(i=>Exists(key+"-"+i)),15,"Barrier "+key);}
        bool Exists(string name)=>File.Exists(Path.Combine(shared,name+".json"));
        T Read<T>(string name)=>JsonUtility.FromJson<T>(File.ReadAllText(Path.Combine(shared,name+".json")));
        void Write<T>(string name,T value){string dst=Path.Combine(shared,name+".json"),tmp=dst+".tmp-"+Guid.NewGuid().ToString("N");File.WriteAllText(tmp,JsonUtility.ToJson(value,true));File.Move(tmp,dst);}
        // Transport/observation revisions and sample clocks continue for peer liveness after Victory.
        // Freeze gameplay (including deadline, remaining, HP, every resource and orb), not those metadata fields.
        static string FrozenResultHash(GameSnapshot value)
        {
            var copy=Copy(value);copy.revision=0;copy.hostNow=copy.serverTime=0;
            copy.attack.revision=copy.resources.revision=copy.battle.revision=0;
            return GameWire.CanonicalHash(copy);
        }
        static T Copy<T>(T value)=>JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        static IEnumerator Wait(Func<bool> test,float seconds,string reason){double until=Now+seconds;while(!test()&&Now<until)yield return null;Require(test(),reason);}
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        static string Arg(string[] args,string key,string fallback){int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
        void Observe(string text,string trace,LogType type){if(type==LogType.Exception||type==LogType.Assert||type==LogType.Error)runtimeError=text;
            if(!text.StartsWith("C6_T07_HIT_RECOVERY ",StringComparison.Ordinal))return;
            var v=text.Split(' ').Skip(1).Select(p=>p.Split(new[]{'='},2)).Where(p=>p.Length==2).ToDictionary(p=>p[0],p=>p[1]);
            recoveries.Add(new Recovery{orb=v["orb"],attacker=ulong.Parse(v["attacker"],CultureInfo.InvariantCulture),added=double.Parse(v["added"],CultureInfo.InvariantCulture),accepted=v["accepted"]=="True",duplicate=v["duplicate"]=="True"});}
        [Serializable]sealed class Marker{public string value="ready";}
        [Serializable]sealed class Identity{public string id;}
        [Serializable]sealed class Pair{public string a,b;}
        [Serializable]sealed class Proof{public string session,hash;public uint round;public ulong revision;public int activeOrbs,flying;}
        [Serializable]sealed class Recovery{public string orb;public ulong attacker;public double added;public bool accepted,duplicate;}
        [Serializable]sealed class Report{
            public string status,error,startedAtUtc,finishedAtUtc,buildGuid,unity,session,room,rejection,disconnectReason;
            public string scope="BUILD23_MAC_PROCESSES_DIRECT_IP_NORMAL_PAID_GENERATION_WITH_EXPLICIT_RAW_COMBINED_LOAD_FIXTURES";
            public string inputScope="DEBUG_POINTER; actual runtime frame samples and Host physics; not physical Touch";
            public bool physicalTouch,bonjourValidated,sixthRejected,lobbyReseat,unreadyStartRejected,initialAckGate,clientHit,victory,retryClean,disconnectClean,capacityReceived,fullCapacitySimultaneouslyObserved,allStaminaVisible;
            public int index,participants,width,height,left,right,paidGenerations,transfers,explicitRawFixtures,explicitCombinedFixtures,maximumObservedOrbs,maximumObservedFlying,maximumObservedJsonBytes;
            public ulong localPlayer;public ulong[]roster;public Proof[]proofs;public Recovery[]recoveries;public GameSnapshot finalGame;
        }
#endif
    }
}
