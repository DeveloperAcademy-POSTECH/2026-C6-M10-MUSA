using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using C6.Prototype.Attack;
using C6.Prototype.Battle;
using C6.Prototype.Orbs;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
namespace C6.Prototype.GameSync
{
    // Explicit standalone diagnostic. Ordinary launches and all iOS builds have no probe behavior.
    public sealed class T11TransferProbe : MonoBehaviour
    {
#if DEVELOPMENT_BUILD && UNITY_STANDALONE && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Attach()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-c6T11ProbeDirectory") < 0) return;
            var game = FindAnyObjectByType<T10GameSession>();
            if (game != null) game.gameObject.AddComponent<T11TransferProbe>();
        }
        T10GameSession game;
        T09BattleController controller;
        string output, shared, role, port, runtimeError;
        int pointer = -23000, dropped, duplicates;
        bool ownOutput;
        readonly List<PassProof> passes = new List<PassProof>();
        readonly List<AttackRequestReply> replies = new List<AttackRequestReply>();
        Report report;
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();
            output=Arg(args,"-c6T11ProbeDirectory",null); role=Arg(args,"-c6T11Role","host"); port=Arg(args,"-c6T11Port","25126");
            report=new Report{role=role,startedUTC=DateTime.UtcNow.ToString("O"),buildGuid=Application.buildGUID,unity=Application.unityVersion,
                mode="ACTUAL_MAC_TWO_PROCESS_NORMAL_HOST_SEED_AUTOMATED_POINTER",physicalDevice=false};
            string error=null;
            try
            {
                Require(Path.IsPathRooted(output)&&(!Directory.Exists(output)||!Directory.EnumerateFileSystemEntries(output).Any()),"Output must be absolute and empty.");
                Require(role=="host"||role=="client","Invalid role.");
                Directory.CreateDirectory(output);ownOutput=true;shared=Directory.GetParent(output).FullName;
                game=GetComponent<T10GameSession>();controller=game.Controller;
                Require(game.TransfersEnabled&&controller.TransfersEnabled&&game.BuildIdentifier=="16","T11 scene is not wired.");
                controller.Attack.RequestResolved+=OnReply;Application.logMessageReceived+=OnLog;Application.runInBackground=true;
            }
            catch(Exception e){error=e.ToString();}
            var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0&&error==null)
            {
                bool more=false;object current=null;
                try{if(runtimeError!=null)throw new InvalidOperationException(runtimeError);more=stack.Peek().MoveNext();if(more)current=stack.Peek().Current;else stack.Pop();}
                catch(Exception e){error=e.ToString();}
                if(error!=null)break;
                if(more&&current is IEnumerator nested)stack.Push(nested);else if(more)yield return current;
            }
            report.status=error==null?"PASS":"FAIL";report.error=error;report.finishedUTC=DateTime.UtcNow.ToString("O");
            report.passes=passes.ToArray();report.replies=replies.ToArray();report.proofs=game?.Proofs.ToArray();report.droppedReplies=dropped;report.duplicatePackets=duplicates;
            report.gameError=game?.Error;report.sent=controller?.SentTransfers??0;report.received=controller?.ReceivedTransfers??0;
            if(ownOutput)File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(report,true));
            Debug.Log("C6_T11_PROBE_COMPLETE role="+role+" status="+report.status+" error="+error);
            Application.logMessageReceived-=OnLog;
            if(controller!=null){controller.Attack.RequestResolved-=OnReply;controller.EndDevelopmentTest();}
            yield return new WaitForSecondsRealtime(.5f);
            if(Array.IndexOf(args,"-c6T11Quit")>=0)Application.Quit(error==null?0:1);
        }
        IEnumerator Run()
        {
            for(int i=0;i<8;i++)yield return null;
            report.width=Screen.width;report.height=Screen.height;report.safeArea=Screen.safeArea;
            Require(controller.Views.Count==0&&!game.Attached,"Automatic inventory or game start.");
            bool okay=role=="host"?game.Lobby.CreateRoom("C6 T11",port):game.Lobby.JoinDirect("127.0.0.1",port);Require(okay,"Lobby refused.");
            if(role=="host")
            {
                yield return Wait(()=>game.Lobby.Connected,20,"Host lobby not ready");
                File.WriteAllText(Path.Combine(output,"host-lobby-ready.json"),JsonUtility.ToJson(game.Lobby.Snapshot,true));
            }
            yield return Wait(()=>game.Lobby.CanReady,30,"Config not acknowledged");Require(game.Lobby.ToggleReady(),"Ready failed");
            if(role=="host"){yield return Wait(()=>game.Lobby.CanStart,30,"Both Ready missing");Require(game.Lobby.StartMatch(),"Host Start refused");}
            yield return Wait(()=>game.InitialConfirmed&&controller.CanInteract,25,"Game not Playing");
            Require(controller.Views.Count==0&&controller.Resource.LocalPlayer.stamina==100,"Start is not empty/full");
            report.localPlayer=controller.Attack.LocalPlayerId;report.seed=game.Snapshot.seed;
            if(role=="host")
            {
                yield return Generate();
                WriteShared("raw-id.json",new Identity{id=OwnRaw()[0].id});
            }
            yield return Wait(()=>File.Exists(Path.Combine(shared,"raw-id.json")),15,"Raw ID missing");
            string raw=ReadShared<Identity>("raw-id.json").id;
            for(int step=0;step<8;step++)yield return Pass("raw",raw,step,step%2==0?"host":"client",step/2%2==0?OrbActionKind.TransferLeft:OrbActionKind.TransferRight);
            if(role=="host")
            {
                yield return MakeCombined(raw);
                var combined=controller.Attack.Snapshot.orbs.Single(o=>o.kind==(int)OrbKind.Combined&&o.state==(int)OrbAuthorityState.Idle);
                Require(combined.transferCount==0&&combined.lastTransferSequence==0,"Combination inherited transfer identity.");
                WriteShared("combined-id.json",new Identity{id=combined.id});
            }
            yield return Wait(()=>File.Exists(Path.Combine(shared,"combined-id.json")),70,"Combined ID missing");
            string id=ReadShared<Identity>("combined-id.json").id;
            for(int step=0;step<8;step++)yield return Pass("combined",id,step,step%2==0?"host":"client",step/2%2==0?OrbActionKind.TransferLeft:OrbActionKind.TransferRight);
            yield return Pass("combined",id,8,"host",OrbActionKind.TransferRight);
            if(role=="client")
            {
                yield return Wait(()=>controller.CanInteract&&controller.Views.ContainsKey(id),8,"Received Combined unavailable");
                var center=controller.OrbGridScreenRect.center;
                int move=--pointer;Require(controller.BeginPointer(move,controller.GetViewScreenPosition(id),false),"Received Combined reposition failed");
                controller.MovePointer(move,center);controller.EndPointer(move,center);yield return null;
                var start=controller.GetViewScreenPosition(id);int p=--pointer;
                Require(controller.BeginPointer(p,start,false),"Cannot grab received Combined");
                controller.MovePointer(p,new Vector2(start.x,controller.Layout.BottomPixelRect.yMax+15));
                controller.EndPointer(p,new Vector2(start.x,controller.Layout.BottomPixelRect.yMax+30));
            }
            yield return Wait(()=>controller.Attack.Snapshot.hp==80&&controller.Attack.Snapshot.orbs.All(o=>o.id!=id||o.state==(int)OrbAuthorityState.Consumed),10,"Received Combined did not produce a real hit");
            Require(controller.Attack.Snapshot.roundHits==1&&!controller.Views.ContainsKey(id),"Duplicate hit or lingering local view");
            yield return Wait(()=>game.Snapshot?.attack.hp==80&&game.Snapshot.attack.roundHits==1
                &&game.Snapshot.attack.orbs.All(o=>o.id!=id),5,"Aggregate did not settle after the actual hit");
            if(role=="client")Require(FindObjectsByType<Rigidbody>().Length==0,"Client has authoritative physics");
            yield return Capture("received-combined-real-hit");
            Require(game.Error.Length==0&&controller.TouchBegins==0,"Runtime error or debug pointer labeled touch");
            report.finalGame=Copy(game.Snapshot);report.receiverHit=true;
            File.WriteAllText(Path.Combine(shared,role+"-done.json"),"{}");
            yield return Wait(()=>File.Exists(Path.Combine(shared,(role=="host"?"client":"host")+"-done.json")),20,"Peer final proof missing");
            yield return new WaitForSecondsRealtime(role=="host"?2:1);
        }
        IEnumerator Pass(string group,string id,int step,string sender,OrbActionKind direction)
        {
            string key=group+"-"+step.ToString("00");ulong count=(ulong)step+1;
            bool sending=role==sender;
            yield return Wait(()=>controller.Attack.Snapshot?.orbs.Any(o=>o.id==id)==true,10,"Orb not in aggregate");
            var before=Copy(controller.Attack.Snapshot);var old=before.orbs.Single(o=>o.id==id);
            var proof=new PassProof{key=key,sender=sender,direction=direction.ToString(),sending=sending,orbId=id,kind=old.kind,polarity=old.polarity,
                beforeIds=LiveIds(before),beforeOwner=old.owner,beforeCount=old.transferCount};
            Require(old.transferCount==count-1,"Unexpected previous transfer count");
            WriteShared(key+"-"+role+"-ready.json",new Identity{id=id});
            yield return Wait(()=>File.Exists(Path.Combine(shared,key+"-"+(role=="host"?"client":"host")+"-ready.json")),15,"Peer before-state proof missing");
            if(sending)
            {
                yield return Wait(()=>controller.CanInteract&&controller.Views.ContainsKey(id),10,"Sender cannot interact");
                // Reposition from the arrival edge to a clear center with a separate gesture.
                Rect grid=controller.OrbGridScreenRect;Vector2 center=ClearPoint(id,.5f);
                int move=--pointer;Require(controller.BeginPointer(move,controller.GetViewScreenPosition(id),false),"Reposition grab failed");
                controller.MovePointer(move,center);controller.EndPointer(move,center);
                Require(!controller.Gestures.HasPending&&!controller.HasCombinationPending,"Interior move became an action");
                yield return null;
                Vector2 start=controller.GetViewScreenPosition(id);Rect lower=controller.Layout.BottomPixelRect;
                Vector2 edge=ClearPoint(id,direction==OrbActionKind.TransferLeft?.015f:.985f);
                edge.x=direction==OrbActionKind.TransferLeft?lower.xMin+lower.width*.015f:lower.xMax-lower.width*.015f;
                // Keep horizontal dominance; choose the same clear height before beginning.
                center=new Vector2(grid.center.x,edge.y);move=--pointer;
                Require(controller.BeginPointer(move,start,false),"Height reposition failed");controller.MovePointer(move,center);controller.EndPointer(move,center);yield return null;
                start=controller.GetViewScreenPosition(id);
                int p=--pointer;Require(controller.BeginPointer(p,start,false),"Transfer grab failed");
                for(int n=1;n<=8;n++){controller.MovePointer(p,Vector2.Lerp(start,edge,n/8f));Require(!controller.Gestures.HasPending,"Transfer happened before release");yield return null;}
                proof.heldBeforeRelease=controller.Gestures.ActivePointerId==p;
                proof.expectedHeight=OrbGestureEngine.NormalizeClamped(controller.GetViewScreenPosition(id),controller.TransferScreenRect).y;
                WriteShared(key+"-intent.json",proof);
                int receiptsBefore=replies.Count;
                if(group=="raw"&&step==1)DropRepliesForFirstTransfer();
                controller.EndPointer(p,edge);
                controller.EndPointer(p,edge);
                if(group=="raw"&&step==1)
                {
                    var submitted=(Dictionary<string,OrbActionRequest>)typeof(AttackSession).GetField("submitted",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controller.Attack);
                    var request=submitted.Values.Last(r=>r.OrbId==id&&r.SequenceNumber>old.sequence);
                    SendDuplicate(request);duplicates++;
                }
                yield return Wait(()=>replies.Count>receiptsBefore&&!controller.Gestures.HasPending,8,"Transfer receipt/query not resolved");
                var receipt=replies.Skip(receiptsBefore).Last(r=>r.orbId==id);
                Require(receipt.known&&receipt.accepted,"Transfer rejected: "+receipt.reason);
                proof.requestId=receipt.requestId;proof.receiptKnown=receipt.known;proof.receiptAccepted=receipt.accepted;
                yield return Wait(()=>controller.Attack.Snapshot.orbs.Single(o=>o.id==id).transferCount==count,6,"Sender aggregate missing");
                Require(!controller.Views.ContainsKey(id),"Sender kept a usable duplicate");
            }
            else
            {
                yield return Wait(()=>File.Exists(Path.Combine(shared,key+"-intent.json")),70,"Transfer intent not ready");
                proof.expectedHeight=ReadShared<PassProof>(key+"-intent.json").expectedHeight;
                yield return Wait(()=>controller.Attack.Snapshot.orbs.Any(o=>o.id==id&&o.owner==report.localPlayer&&o.transferCount==count)&&controller.Views.ContainsKey(id),10,"Receiver did not get same orb");
                var view=controller.Views[id];var pos=controller.GetViewScreenPosition(id);
                yield return new WaitForSecondsRealtime(.35f);
                Require(controller.Attack.Snapshot.orbs.Single(o=>o.id==id).transferCount==count&&!controller.Gestures.HasPending
                    &&ReferenceEquals(view,controller.Views[id])&&Vector2.Distance(pos,controller.GetViewScreenPosition(id))<.1f,"Arrival auto-returned or ordinary snapshots replaced/moved its view");
                proof.noAutoReturn=true;proof.displayPoint=pos;
                proof.receivedDisplayHeight=OrbGestureEngine.NormalizeClamped(pos,controller.TransferScreenRect).y;
                Require(Math.Abs(proof.receivedDisplayHeight-proof.expectedHeight)<.0001,"Different screen aspect changed normalized visible height");
            }
            var after=controller.Attack.Snapshot;var orb=after.orbs.Single(o=>o.id==id);
            proof.afterIds=LiveIds(after);proof.afterOwner=orb.owner;proof.afterCount=orb.transferCount;proof.entrySide=orb.entrySide;proof.entryPosition=orb.pos;proof.sequence=orb.sequence;
            Require(proof.beforeIds.SequenceEqual(proof.afterIds),"Transfer changed live ID set");
            Require(orb.kind==old.kind&&orb.polarity==old.polarity&&orb.transferCount==old.transferCount+1&&orb.owner!=old.owner&&orb.sequence>old.sequence,
                "Identity, ownership or sequence not atomic");
            int expectedSide=(int)(direction==OrbActionKind.TransferLeft?EntrySide.Right:EntrySide.Left);
            float expectedX=expectedSide==(int)EntrySide.Left?controller.Layout.Config.OrbRadiusScreenFraction:1-controller.Layout.Config.OrbRadiusScreenFraction;
            Require(orb.entrySide==expectedSide&&Math.Abs(orb.pos.x-expectedX)<.00001&&Math.Abs(orb.pos.y-proof.expectedHeight)<.00001,"Opposite edge/normalized height not preserved");
            passes.Add(proof);File.WriteAllText(Path.Combine(output,key+".json"),JsonUtility.ToJson(proof,true));
            File.WriteAllText(Path.Combine(shared,key+"-"+role+".json"),"{}");
            yield return Wait(()=>File.Exists(Path.Combine(shared,key+"-"+(role=="host"?"client":"host")+".json")),15,"Peer pass proof missing");
            if(step==1||step==7)yield return Capture(key);
        }
        Vector2 ClearPoint(string id,float x)
        {
            Rect grid=controller.OrbGridScreenRect;
            foreach(float y in new[]{.5f,.8f,.2f,.65f,.35f,.9f,.1f})
            {
                Vector2 point=grid.min+Vector2.Scale(new Vector2(x,y),grid.size);
                if(controller.Views.Keys.Where(k=>k!=id).All(k=>Vector2.Distance(point,controller.GetViewScreenPosition(k))>Screen.width*controller.Layout.Config.CombinationRadiusFraction*1.25f))return point;
            }
            throw new InvalidOperationException("No clear drop point for explicit probe");
        }
        IEnumerator Generate()
        {
            yield return Wait(()=>controller.CanInteract&&controller.Resource.CanGenerate,15,"Stamina unavailable");
            Require(controller.RequestGenerate(),"Generate rejected locally");
            yield return Wait(()=>!controller.Resource.HasPending,8,"Generation confirmation missing");
            Require(controller.Resource.LastResult?.accepted==true,"Host generation rejected");
        }
        OrbWire[] OwnRaw()=>controller.Attack.Snapshot.orbs.Where(o=>o.owner==report.localPlayer&&o.kind==(int)OrbKind.Raw&&o.state==(int)OrbAuthorityState.Idle).ToArray();
        IEnumerator MakeCombined(string returnedId)
        {
            double deadline=Time.realtimeSinceStartupAsDouble+60;
            while(!OwnRaw().Any(b=>b.id!=returnedId&&b.polarity!=OwnRaw().Single(a=>a.id==returnedId).polarity))
            {Require(Time.realtimeSinceStartupAsDouble<deadline,"Normal seed produced no opposite pair");yield return Generate();}
            var source=OwnRaw().Single(a=>a.id==returnedId);
            Require(source.transferCount==8,"Expected the Raw returned after eight passes");
            var pair=new[]{source,OwnRaw().First(b=>b.id!=returnedId&&b.polarity!=source.polarity)};
            yield return Wait(()=>controller.CanInteract&&controller.Views.ContainsKey(pair[0].id)&&controller.Views.ContainsKey(pair[1].id),5,"Combination views missing");
            Vector2 start=controller.GetViewScreenPosition(pair[0].id),end=controller.GetViewScreenPosition(pair[1].id);int p=--pointer;
            Require(controller.BeginPointer(p,start,false),"Combination grab failed");
            for(int n=1;n<=8;n++){controller.MovePointer(p,Vector2.Lerp(start,end,n/8f));Require(!controller.Gestures.HasPending,"Free combination drag became transfer");yield return null;}
            controller.EndPointer(p,end);
            yield return Wait(()=>!controller.HasCombinationPending&&!controller.Combination.HasPending,8,"Combination receipt missing");
            Require(controller.Combination.LastResult?.accepted==true&&controller.Combination.LastResult.sourceOrbId==returnedId
                &&controller.Combination.LastResult.currentSource?.state==(int)OrbAuthorityState.Consumed,"Returned Raw could not combine");
            report.returnedRawCombined=true;
        }
        void DropRepliesForFirstTransfer()
        {
            var original=(CustomMessagingManager.HandleNamedMessageDelegate)Delegate.CreateDelegate(typeof(CustomMessagingManager.HandleNamedMessageDelegate),controller.Attack,
                typeof(AttackSession).GetMethod("ReceiveReply",BindingFlags.Instance|BindingFlags.NonPublic));
            double until=Time.realtimeSinceStartupAsDouble+1.5;
            controller.Attack.Connection.OwnedManager.CustomMessagingManager.RegisterNamedMessageHandler("C6.T06.Reply.v1",(sender,reader)=>
            {if(Time.realtimeSinceStartupAsDouble<until){dropped++;return;}original(sender,reader);});
        }
        void SendDuplicate(OrbActionRequest request)
        {
            var packet=AttackRequestPacket.FromRequest(controller.Attack.LocalNonce,request);
            using(var writer=AttackWire.Write(packet))
            {controller.Attack.Connection.OwnedManager.CustomMessagingManager.SendNamedMessage("C6.T06.Launch.v1",NetworkManager.ServerClientId,writer,NetworkDelivery.ReliableFragmentedSequenced);}
        }
        IEnumerator Capture(string name)
        {
            yield return null;yield return new WaitForEndOfFrame();
            File.WriteAllText(Path.Combine(output,name+"-game.json"),JsonUtility.ToJson(game.Snapshot,true));
            var texture=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());Destroy(texture);
            File.WriteAllText(Path.Combine(output,"progress.json"),JsonUtility.ToJson(new Identity{id=name}));
        }
        static string[] LiveIds(AttackSnapshot state)=>state.orbs.Where(o=>o.state!=(int)OrbAuthorityState.Consumed).Select(o=>o.id).OrderBy(id=>id,StringComparer.Ordinal).ToArray();
        void WriteShared<T>(string name,T data)
        {
            string destination=Path.Combine(shared,name),temporary=destination+".tmp-"+Guid.NewGuid().ToString("N");
            File.WriteAllText(temporary,JsonUtility.ToJson(data,true));
            // Evidence synchronization only. Make presence mean complete JSON for the peer.
            File.Move(temporary,destination);
        }
        T ReadShared<T>(string name)=>JsonUtility.FromJson<T>(File.ReadAllText(Path.Combine(shared,name)));
        void OnReply(AttackRequestReply reply)=>replies.Add(Copy(reply));
        void OnLog(string text,string trace,LogType type){if(type==LogType.Error||type==LogType.Assert||type==LogType.Exception)runtimeError=text;}
        static T Copy<T>(T value)=>JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        static IEnumerator Wait(Func<bool> ready,float seconds,string error){double end=Time.realtimeSinceStartupAsDouble+seconds;while(!ready()&&Time.realtimeSinceStartupAsDouble<end)yield return null;Require(ready(),error);}
        static void Require(bool okay,string message){if(!okay)throw new InvalidOperationException(message);}
        static string Arg(string[] args,string key,string fallback){int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
        [Serializable]sealed class Identity{public string id;}
        [Serializable]sealed class PassProof
        {
            public string key,sender,direction,orbId,requestId;public bool sending,heldBeforeRelease,noAutoReturn,receiptKnown,receiptAccepted;
            public int kind,polarity,entrySide;public ulong beforeOwner,afterOwner,beforeCount,afterCount,sequence;public float expectedHeight,receivedDisplayHeight;
            public string[] beforeIds,afterIds;public Vector2 entryPosition,displayPoint;
        }
        [Serializable]sealed class Report
        {
            public string role,status,error,startedUTC,finishedUTC,buildGuid,unity,mode,gameError;public bool physicalDevice,returnedRawCombined,receiverHit;
            public int width,height,droppedReplies,duplicatePackets,sent,received;public uint seed;public ulong localPlayer;public Rect safeArea;
            public PassProof[] passes;public AttackRequestReply[] replies;public GameStateProof[] proofs;public GameSnapshot finalGame;
        }
#endif
    }
}
