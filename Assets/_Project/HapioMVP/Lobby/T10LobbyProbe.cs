using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using C6.Prototype.Networking;
using C6.Prototype.Lobby.Discovery;
using UnityEngine;

namespace C6.Prototype.Lobby
{
    // Opt-in development evidence. Ordinary launches and physical iOS launches never automate UI.
    public sealed class T10LobbyProbe : MonoBehaviour
    {
#if DEVELOPMENT_BUILD && UNITY_STANDALONE && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Attach()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-c6T10ProbeDirectory")<0)return;
            var session=FindAnyObjectByType<T10LobbySession>();if(session!=null)session.gameObject.AddComponent<T10LobbyProbe>();
        }
        T10LobbySession session;T10LobbyHud hud;
        string output,role,port,roomName,runtimeError;bool ownsOutput;
        string[] args;Report report;readonly List<Checkpoint> checks=new List<Checkpoint>();
        IEnumerator Start()
        {
            args=Environment.GetCommandLineArgs();output=Arg("-c6T10ProbeDirectory",null);role=Arg("-c6T10Role","host");
            port=Arg("-c6T10Port","25114");roomName=Arg("-c6T10RoomName","C6-T10-A");
            report=new Report{role=role,build=LobbyBuildInfo.Build,buildGuid=Application.buildGUID,unity=Application.unityVersion,startedAtUtc=DateTime.UtcNow.ToString("O")};
            string error=null;
            try
            {
                Require(Path.IsPathRooted(output),"Absolute output required.");
                Require(!Directory.Exists(output)||!Directory.EnumerateFileSystemEntries(output).Any(),"Preserve prior output; use an empty directory.");
                Directory.CreateDirectory(output);ownsOutput=true;
                session=GetComponent<T10LobbySession>();hud=GetComponent<T10LobbyHud>();Application.runInBackground=true;
                Application.logMessageReceived+=Observe;
            }
            catch(Exception ex){error=ex.ToString();}
            var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0&&error==null)
            {
                object next=null;bool more=false;
                try{if(runtimeError!=null)throw new InvalidOperationException(runtimeError);more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;else stack.Pop();}
                catch(Exception ex){error=ex.ToString();}
                if(error!=null)break;
                if(more&&next is IEnumerator nested)stack.Push(nested);else if(more)yield return next;
            }
            report.status=error==null?"PASS":"FAIL";report.error=error;report.finishedAtUtc=DateTime.UtcNow.ToString("O");report.checkpoints=checks.ToArray();
            if(ownsOutput)File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(report,true));
            Debug.Log("C6_T10A_PROBE_COMPLETE role="+role+" status="+report.status+" error="+error);
            Application.logMessageReceived-=Observe;
            if(session!=null){session.CancelBrowse();session.Leave();}
            yield return new WaitForSecondsRealtime(.5f);
            if(Array.IndexOf(args,"-c6T10ProbeQuit")>=0)Application.Quit(error==null?0:1);
        }
        IEnumerator Run()
        {
            for(int i=0;i<8;i++)yield return null;
            report.width=Screen.width;report.height=Screen.height;report.safeArea=Screen.safeArea;
            Require(session.Snapshot==null&&!session.Connected,"Ordinary scene must not auto-connect.");
            yield return Capture("offline");
            if(role=="render") {hud.SetDirectExpanded(true);yield return Capture("direct-fallback");yield break;}
            if(role=="discovery"){yield return DiscoveryExercise();yield break;}
            if(role=="reject")
            {
                Require(session.JoinForValidation(Arg("-c6T10Address","127.0.0.1"),port,Arg("-c6T10JoinBuild",LobbyBuildInfo.Build),Arg("-c6T10RoomId","")),"Rejection test connection could not start.");
                yield return Wait(()=>session.Connection.State==DirectConnectionState.Failed,15,"Connection rejection");
                report.rejection=session.Error;Require(session.Error.Contains(Arg("-c6T10ExpectedReason","BUILD_MISMATCH")),"Unexpected rejection: "+session.Error);
                Require(session.Snapshot==null&&!session.CanReady&&!session.CanStart,"Rejected participant acquired lobby state.");
                Record("rejected");yield return Capture("rejected");yield break;
            }
            bool host=role=="host"||role=="device-host";
            if(host)
            {
                Require(session.CreateRoom(roomName,port),"Create room failed.");
                yield return Wait(()=>session.Snapshot!=null&&session.Discovery.AdvertisingConfirmed,20,"Host state and actual Bonjour registration");
                Require(session.Snapshot.LocalPlayerNumber(0)==1&&session.Snapshot.ParticipantCount==1,"Host must be P1 alone.");
                Require(!session.StartMatch(),"Host started without two Ready players.");Record("host-advertised");yield return Capture("host-alone");
            }
            else
            {
                string route=Arg("-c6T10JoinRoute","BONJOUR");
                if(route=="DIRECT_IP")Require(session.JoinDirect(Arg("-c6T10Address","127.0.0.1"),port),"Direct fallback failed.");
                else
                {
                    Require(session.Browse(),"Bonjour browse failed.");
                    yield return Wait(()=>session.Rooms.Any(r=>r.Name==roomName&&session.RoomProblem(r)==""),60,"Real Bonjour resolved room");
                    var room=session.Rooms.First(r=>r.Name==roomName&&session.RoomProblem(r)=="");report.resolvedAddress=room.Address;report.resolvedRoomId=room.RoomId;
                    Record("bonjour-resolved");yield return Capture("discovered-room");
                    Require(session.JoinRoom(room.RoomId),"Discovered room join failed.");
                    Require(session.JoinRoute=="BONJOUR","Discovery route silently fell back to direct IP.");
                }
            }
            yield return Wait(()=>session.Snapshot?.ParticipantCount==2&&session.Snapshot.p2.initialStateReceived&&!session.HasPending,role.StartsWith("device")?900:90,"Both players and initial Config acknowledgement");
            var initial=session.Snapshot;ulong local=session.Connection.LocalClientId.Value;
            Require(initial.LocalPlayerNumber(local)==(host?1:2)&&initial.LeftPlayerNumber(local)==(host?2:1)&&initial.RightPlayerNumber(local)==(host?2:1),"Local/left/right assignment mismatch.");
            Require(session.HostConfig.initialOrbs==0&&session.HostConfig.staminaStart==100&&session.HostConfig.generateCost==20&&session.HostConfig.hitRecovery==5&&session.HostConfig.duration==180,"Host Config mismatch.");
            Record("initial-ack");yield return Capture("connected");
            if(!host)
            {
                Require(session.RequestStartForValidation(),"Client start request not sent.");
                yield return Wait(()=>!session.HasPending,10,"Client rejection acknowledgement");
                Require(session.LastReply!=null&&!session.LastReply.accepted&&session.LastReply.reason=="HOST_ONLY","Non-Host start was not explicitly rejected.");
                Require(session.Snapshot.phase==LobbyProtocol.Lobby,"Non-Host started room.");Record("non-host-start-rejected");
            }
            if(host)yield return Wait(()=>session.Snapshot?.p2?.ready==true,role.StartsWith("device")?900:90,"Client Ready before Host Ready");
            Require(session.ToggleReady(),"Local Ready rejected.");
            yield return Wait(()=>session.LocalReady&&!session.HasPending,10,"Ready acknowledgement");Record("local-ready");yield return Capture("ready");
            if(host)
            {
                yield return Wait(()=>session.CanStart,role.StartsWith("device")?900:90,"Two Ready participants");
                Record("both-ready");
                yield return new WaitForSecondsRealtime(float.Parse(Arg("-c6T10StartDelay",role=="host"?"15":"1"),System.Globalization.CultureInfo.InvariantCulture));
                Require(session.StartMatch(),"Host start refused after both Ready.");
            }
            yield return Wait(()=>session.Snapshot?.phase==LobbyProtocol.Playing&&!session.HasPending,90,"Shared Host Start contract");
            Require(!session.CanReady&&!session.CanStart&&session.Snapshot.start!=null,"Start must freeze lobby Ready/start and provide a contract.");
            report.finalSnapshot=JsonUtility.FromJson<LobbySnapshot>(JsonUtility.ToJson(session.Snapshot));
            report.joinRoute=session.JoinRoute;Record("playing");yield return Capture("start-confirmed");
            // Keep each peer alive long enough for the other peer to record the same contract.
            yield return new WaitForSecondsRealtime(role.StartsWith("device")?30:8);
        }
        IEnumerator DiscoveryExercise()
        {
            using(var advertiser=new BonjourRoomDiscovery())
            {
                var room=new RoomAdvertisement{RoomId=Guid.NewGuid().ToString("N"),Name=roomName,ProtocolVersion=LobbyProtocol.Version,Build=LobbyBuildInfo.Build,
                    ConfigHash=new string('a',64),Participants=1,Port=ushort.Parse(port),Status=RoomAdvertisementStatus.Lobby};
                Require(session.Browse()&&advertiser.Advertise(room),"Native browse/register setup failed.");
                yield return Pump(()=>session.Rooms.Any(r=>r.RoomId==room.RoomId),advertiser,20,"Initial DNS resolution");Record("native-room-resolved");
                room.Build="different-build";Require(advertiser.UpdateAdvertisement(room),"TXT version update failed.");
                yield return Pump(()=>session.Rooms.Any(r=>r.RoomId==room.RoomId&&session.RoomProblem(r)=="BUILD_MISMATCH"),advertiser,15,"Version metadata update");
                Require(!session.JoinRoom(room.RoomId)&&session.Error=="BUILD_MISMATCH","Wrong build room was joinable.");Record("wrong-build-row-rejected");
                room.Build=LobbyBuildInfo.Build;room.Participants=2;Require(advertiser.UpdateAdvertisement(room),"TXT full update failed.");
                yield return Pump(()=>session.Rooms.Any(r=>r.RoomId==room.RoomId&&session.RoomProblem(r)=="ROOM_FULL"),advertiser,15,"Full metadata update");
                Require(!session.JoinRoom(room.RoomId)&&session.Error=="ROOM_FULL","Full room was joinable.");Record("full-row-rejected");
                room.Participants=1;room.Status=RoomAdvertisementStatus.Playing;advertiser.UpdateAdvertisement(room);
                yield return Pump(()=>session.Rooms.Any(r=>r.RoomId==room.RoomId&&session.RoomProblem(r)=="BATTLE_IN_PROGRESS"),advertiser,15,"Playing metadata update");Record("playing-row-rejected");
                room.Status=RoomAdvertisementStatus.Lobby;advertiser.UpdateAdvertisement(room);
                yield return Pump(()=>session.Rooms.Any(r=>r.RoomId==room.RoomId&&session.RoomProblem(r)==""),advertiser,15,"Lobby metadata update");
                // Advertiser remains registered but its Tick/heartbeat is intentionally paused. Browser continues normally.
                yield return Wait(()=>session.Rooms.All(r=>r.RoomId!=room.RoomId),18,"Unrefreshed room expiry");
                Require(!session.JoinRoom(room.RoomId)&&session.Error=="STALE_ROOM","Expired room was joinable.");Record("heartbeat-expiry");
                advertiser.Tick();yield return Pump(()=>session.Rooms.Any(r=>r.RoomId==room.RoomId),advertiser,15,"Fresh heartbeat returns room");
                session.CancelBrowse();Require(!session.Discovery.IsBrowsing&&session.Rooms.Count==0,"Cancelled browse retained room rows.");Record("cancelled");
                session.RefreshRooms();yield return Pump(()=>session.Rooms.Any(r=>r.RoomId==room.RoomId),advertiser,20,"Refresh resolves live room");Record("refreshed");
                advertiser.StopAdvertising();yield return Wait(()=>session.Rooms.All(r=>r.RoomId!=room.RoomId),12,"Native goodbye removes ended room");Record("goodbye-removed");
            }
            session.CancelBrowse();Require(session.Discovery.ActiveNativeOperations==0,"Browse stop leaked native operations.");
        }
        IEnumerator Pump(Func<bool> done,BonjourRoomDiscovery advertiser,double seconds,string label)
        {double until=Time.realtimeSinceStartupAsDouble+seconds;while(!done()){Require(Time.realtimeSinceStartupAsDouble<until,label+" timed out; "+session.Discovery.LastError);advertiser.Tick();yield return null;}}
        IEnumerator Wait(Func<bool> done,double seconds,string label)
        {double until=Time.realtimeSinceStartupAsDouble+seconds;while(!done()){Require(Time.realtimeSinceStartupAsDouble<until,label+" timed out; "+session.Error);yield return null;}}
        IEnumerator Capture(string label)
        {Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(Path.Combine(output,label+".png"),image.EncodeToPNG());}finally{Destroy(image);}}
        void Record(string label)
        {
            var c=new Checkpoint{label=label,at=Time.realtimeSinceStartupAsDouble,snapshot=session.Snapshot==null?null:JsonUtility.FromJson<LobbySnapshot>(JsonUtility.ToJson(session.Snapshot)),error=session.Error,discovered=session.Rooms.Count};checks.Add(c);
            File.WriteAllText(Path.Combine(output,"progress.json"),JsonUtility.ToJson(c,true));Debug.Log("C6_T10A_PROBE_CHECK role="+role+" label="+label);
        }
        string Arg(string key,string fallback){int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
        static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
        void Observe(string condition,string trace,LogType type){if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)runtimeError=condition;}
        [Serializable]sealed class Checkpoint{public string label,error;public double at;public int discovered;public LobbySnapshot snapshot;}
        [Serializable]sealed class Report
        {public string status="NOT_RUN",role,build,buildGuid,unity,startedAtUtc,finishedAtUtc,error,joinRoute,resolvedAddress,resolvedRoomId,rejection;
            public string mode="EXPLICIT_DESKTOP_LOBBY_PROBE";public bool physicalDevice=false;public int width,height;public Rect safeArea;public Checkpoint[] checkpoints;public LobbySnapshot finalSnapshot;}
#endif
    }
}
