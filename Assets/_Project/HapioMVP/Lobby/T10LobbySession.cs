using System;
using System.Collections.Generic;
using System.Linq;
using C6.Prototype.Lobby.Discovery;
using C6.Prototype.Networking;
using C6.Prototype.Presentation;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace C6.Prototype.Lobby
{
    [Serializable] public sealed class LobbyReply
    { public string requestId, reason; public bool accepted; public ulong sequence; public LobbySnapshot snapshot; }

    [DisallowMultipleComponent, RequireComponent(typeof(DirectConnectionSession))]
    public sealed class T10LobbySession : MonoBehaviour
    {
        private string SyncMessage => continuousTransfers ? "C6.P4.Lobby.Sync.v1" : MaximumParticipants > 2 ? "C6.P3.Lobby.Sync.v1" : "C6.T10A.Sync.v1";
        private string StateMessage => continuousTransfers ? "C6.P4.Lobby.State.v1" : MaximumParticipants > 2 ? "C6.P3.Lobby.State.v1" : "C6.T10A.State.v1";
        private string RequestMessage => continuousTransfers ? "C6.P4.Lobby.Request.v1" : MaximumParticipants > 2 ? "C6.P3.Lobby.Request.v1" : "C6.T10A.Request.v1";
        private string ReplyMessage => continuousTransfers ? "C6.P4.Lobby.Reply.v1" : MaximumParticipants > 2 ? "C6.P3.Lobby.Reply.v1" : "C6.T10A.Reply.v1";
        [SerializeField] private int maximumParticipants = LobbyProtocol.Capacity;
        public int MaximumParticipants => maximumParticipants;
        [SerializeField] private bool continuousTransfers;
        public bool ContinuousTransfersEnabled => continuousTransfers;
        public int ProtocolVersion => LobbyProtocol.For(MaximumParticipants, continuousTransfers);
        public void ConfigureContinuousTransfers(bool enabled)
        {
            if (connection != null && !connection.CanStart) throw new InvalidOperationException("End room before configuring transfer mode.");
            if (enabled && MaximumParticipants != LobbyProtocol.MaximumCapacity) throw new InvalidOperationException("Continuous transfers require multiparty capacity.");
            continuousTransfers = enabled;
        }
        public void ConfigureCapacity(int maximum = LobbyProtocol.Capacity)
        {
            if (connection != null && !connection.CanStart) throw new InvalidOperationException("End room before configuring capacity.");
            if (maximum != LobbyProtocol.Capacity && maximum != LobbyProtocol.MaximumCapacity) throw new ArgumentOutOfRangeException(nameof(maximum));
            if (continuousTransfers && maximum != LobbyProtocol.MaximumCapacity) throw new InvalidOperationException("Disable continuous transfers before reducing capacity.");
            maximumParticipants = maximum;
        }
        [SerializeField] private ScreenLayoutConfig config;
        private LobbyHostConfig roomDefaults;
        [SerializeField] private string buildIdentifier = LobbyBuildInfo.Build;
        public string BuildIdentifier => buildIdentifier;
        public void ConfigureBuild(string value) { if (connection != null && !connection.CanStart) throw new InvalidOperationException("End room before configuring build."); if (string.IsNullOrWhiteSpace(value) || value.Length > 32) throw new ArgumentException("Invalid build."); buildIdentifier = value; }
        private DirectConnectionSession connection;
        private NetworkManager manager;
        private CustomMessagingManager messaging;
        private LobbyAuthority authority;
        private BonjourRoomDiscovery discovery;
        private string clientNonce, expectedRoom, roomName="C6 Room", pendingId, lastPublishedState;
        private ulong sequence, pendingRevision;
        private double joinedAt, nextSync, pendingAt, ackAt;
        private readonly Dictionary<ulong, double> admittedAt = new Dictionary<ulong, double>();
        private bool intentionalClose, wasConnected;
        private int bindingGeneration;
        private ushort roomPort=7777;
        private string advertisementKey;
        private double nextAdvertisementRetry;
        public LobbySnapshot Snapshot { get; private set; }
        public LobbyHostConfig HostConfig { get; private set; }
        public LobbyReply LastReply { get; private set; }
        public string Error { get; private set; }="";
        public string FailureStage { get; private set; }="";
        public string Status { get; private set; }="Create or find a room";
        public string JoinRoute { get; private set; }="NONE";
        public string RoomName=>roomName;
        public DirectConnectionSession Connection=>connection;
        public BonjourRoomDiscovery Discovery=>discovery;
        public IReadOnlyList<DiscoveredRoom> Rooms=>discovery.Rooms;
        public bool Connected=>connection!=null&&connection.State==DirectConnectionState.Connected&&manager!=null&&manager.IsListening&&!manager.ShutdownInProgress;
        // NGO connection approval precedes the authoritative lobby snapshot and its acknowledgement.
        // Keep Connected's transport meaning for existing game code, and gate the waiting-room UI separately.
        public bool InitialStateReady=>Connected&&Snapshot!=null&&HostConfig!=null&&LocalPlayer?.initialStateReceived==true;
        public bool IsHost=>Connected&&manager.IsHost;
        public bool HasPending=>!string.IsNullOrEmpty(pendingId);
        public bool LocalReady=>LocalPlayer?.ready??false;
        public LobbyPlayer LocalPlayer=>Snapshot?.Find(connection.LocalClientId??ulong.MaxValue);
        public bool CanReady=>Connected&&Snapshot?.phase==LobbyProtocol.Lobby&&LocalPlayer!=null&&LocalPlayer.initialStateReceived&&!HasPending;
        public bool CanStart=>IsHost&&Snapshot?.canStart==true&&!HasPending;
        public event Action Changed;
        public event Action<LobbyReply> RequestResolved;
        public event Action<LobbyStartContract> StartConfirmed;
        public event Action<bool> ApplicationPauseChanged;
        public void ReportInterruption(string reason)
        {
            Error=reason;FailureStage="SESSION";Status="Room ended / connect again";Changed?.Invoke();
        }
        private static double Now=>Time.realtimeSinceStartupAsDouble;

        public void Configure(ScreenLayoutConfig value)
        {
            if (connection != null && !connection.CanStart) throw new InvalidOperationException("End room before configuring.");
            config=value;
            roomDefaults=value==null?null:LobbyHostConfig.Capture(value);
        }
        public LobbyHostConfig CaptureRoomDefaults()
        {
            LobbyHostConfig source=roomDefaults??(config==null?null:LobbyHostConfig.Capture(config));
            if(source==null)return null;
            return LobbyHostConfig.TryRead(JsonUtility.ToJson(source),out LobbyHostConfig copy)?copy:null;
        }
        private void Awake()
        {
            connection=GetComponent<DirectConnectionSession>(); discovery=new BonjourRoomDiscovery();
            if(config!=null)roomDefaults=LobbyHostConfig.Capture(config);
            discovery.Changed+=OnDiscoveryChanged;
            connection.Changed+=OnConnectionChanged;
            connection.ParticipantDisconnected+=OnParticipantDisconnected;
        }
        private void Start()=>Debug.Log($"C6_T10A_READY build={buildIdentifier} protocol={ProtocolVersion} device={SystemInfo.deviceModel} os={SystemInfo.operatingSystem} screen={Screen.width}x{Screen.height}");
        public bool CreateRoom(string name,string port)=>CreateRoom(name,port,null);
        public bool CreateRoom(string name,string port,LobbyHostConfig requestedConfig)
        {
            if (!connection.CanStart||config==null) return false;
            if (!DirectConnectionValidation.TryParsePort(port,out roomPort)) return Fail("INVALID_PORT","INPUT");
            name=(name??"").Trim(); if (name.Length==0) name="C6 Room";
            if (name.Length>32||System.Text.Encoding.UTF8.GetByteCount(name)>80||name.Any(char.IsControl)) return Fail("INVALID_ROOM_NAME","INPUT");
            ResetLocal(); roomName=name; expectedRoom=Guid.NewGuid().ToString("N");
            LobbyHostConfig selected=requestedConfig??CaptureRoomDefaults();
            if(selected==null||!LobbyHostConfig.TryRead(JsonUtility.ToJson(selected),out LobbyHostConfig approved))
                return Fail("UNSUPPORTED_HOST_CONFIG","INPUT");
            HostConfig=approved;
            authority=new LobbyAuthority(expectedRoom,Guid.NewGuid().ToString("N"),buildIdentifier,JsonUtility.ToJson(HostConfig),BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(),0),MaximumParticipants,continuousTransfers);
            discovery.StopBrowse(); JoinRoute="HOST";
            if (!connection.ConfigureConnection((ushort)ProtocolVersion,Array.Empty<byte>(),Approve,MaximumParticipants,KeepLobbyOnPeerDisconnect)) return Fail("CONNECTION_BUSY");
            if (!connection.StartHost(port)) return Fail(string.IsNullOrEmpty(connection.FailureCode)?"HOST_FAILED":connection.FailureCode,connection.FailureStage);
            Status="Room open / waiting for participants"; Changed?.Invoke(); return true;
        }
        public bool Browse()
        {
            if (!connection.CanStart) return false;
            Error="";FailureStage=""; discovery.StartBrowse(); Status=discovery.IsBrowsing?"Finding nearby rooms":"Room search failed"; Changed?.Invoke(); return discovery.IsBrowsing;
        }
        public void RefreshRooms() { if(connection.CanStart) { Error="";FailureStage=""; discovery.Refresh(); Status="Refreshing nearby rooms"; Changed?.Invoke(); } }
        public void CancelBrowse() { discovery.StopBrowse(); if(FailureStage=="DISCOVERY"){Error="";FailureStage="";} Status="Search stopped"; Changed?.Invoke(); }
        public bool JoinRoom(string id)
        {
            if (!connection.CanStart) return false;
            var room=Rooms.FirstOrDefault(r=>r.RoomId==id);
            if(room==null||room.ExpiresAtSeconds<=Now) return Fail("STALE_ROOM","DISCOVERY");
            string problem=RoomProblem(room); if(problem.Length>0) return Fail(problem,"DISCOVERY");
            roomName=room.Name;
            var candidates=room.Candidates!=null&&room.Candidates.Count>0?room.Candidates:new[]{room.Address};
            return Join(candidates,room.Port.ToString(),room.RoomId,"BONJOUR");
        }
        public bool JoinDirect(string address,string port)=>Join(new[]{address},port,"","DIRECT_IP");
        public bool JoinForValidation(string address,string port,string build,string roomId="")=>Join(new[]{address},port,roomId,"EXPLICIT_VALIDATION",build);
        public bool JoinCandidatesForValidation(IEnumerable<string> addresses,string port,string build,string roomId="")=>Join(addresses,port,roomId,"EXPLICIT_VALIDATION",build);
        private bool Join(IEnumerable<string> addresses,string port,string roomId,string route,string build=null)
        {
            if (!connection.CanStart) return false;
            var candidates=(addresses??Array.Empty<string>()).ToArray();
            if (candidates.Length==0||!candidates.Any(address=>DirectConnectionValidation.TryParseAddress(address,out _))
                ||!DirectConnectionValidation.TryParsePort(port,out _))return Fail("INVALID_ADDRESS_OR_PORT","INPUT");
            build = build ?? buildIdentifier;
            ResetLocal(); expectedRoom=roomId; clientNonce=Guid.NewGuid().ToString("N");
            // One user JOIN creates one hello. Candidate retries must reuse room/build/nonce,
            // including after a failed driver has finished shutting down.
            var hello=new LobbyHello{protocol=ProtocolVersion,build=build,roomId=roomId,clientNonce=clientNonce};
            if(!connection.ConfigureConnection((ushort)ProtocolVersion,LobbyWire.Encode(hello),null,MaximumParticipants,KeepLobbyOnPeerDisconnect)) return Fail("CONNECTION_BUSY");
            discovery.StopBrowse(); JoinRoute=route; joinedAt=Now;
            bool ok=connection.JoinCandidates(candidates,port); Status=ok?connection.Message:"Connection failed";
            if(!ok){Error=string.IsNullOrEmpty(connection.FailureCode)?connection.Message:connection.FailureCode;FailureStage=connection.FailureStage;}
            Debug.Log($"C6_T10A_JOIN route={route} room={roomId} build={build}"); Changed?.Invoke();return ok;
        }
        private string Approve(ulong sender,byte[] payload)
        {
            if(sender==NetworkManager.ServerClientId) return string.Empty;
            if(authority==null||!LobbyWire.TryDecode(payload,out LobbyHello hello))return "INVALID_HELLO";
            bool ok=authority.TryAdmit(sender,hello,out string reason);
            if(ok) admittedAt[sender]=Now;
            Debug.Log($"C6_T10A_ADMISSION sender={sender} accepted={ok} reason={reason} room={authority.RoomId}");
            return reason;
        }
        public string RoomProblem(DiscoveredRoom r)
        {
            if(r.ExpiresAtSeconds<=Now)return "STALE_ROOM";
            if(r.ProtocolVersion!=ProtocolVersion)return "PROTOCOL_MISMATCH";
            if(r.Build!=buildIdentifier)return "BUILD_MISMATCH";
            if(r.Status!=RoomAdvertisementStatus.Lobby)return "BATTLE_IN_PROGRESS";
            if(r.Participants>=MaximumParticipants)return "ROOM_FULL";
            return string.Empty;
        }
        public bool ToggleReady()=>CanReady&&Submit(LobbyProtocol.SetReady,!LocalReady);
        public bool StartMatch()=>CanStart&&Submit(LobbyProtocol.Start,false);
        // Development tests use the same untrusted request path to verify Host rejection.
        public bool RequestStartForValidation()=>Connected&&Submit(LobbyProtocol.Start,false);
        private bool Submit(string kind,bool ready)
        {
            if(!Connected||Snapshot==null||HasPending)return false;
            var request=new LobbyRequest{protocol=ProtocolVersion,roomId=Snapshot.roomId,sessionId=Snapshot.sessionId,
                requestId=Guid.NewGuid().ToString("N"),sequence=++sequence,revision=Snapshot.revision,kind=kind,
                ready=ready,configFingerprint=Snapshot.configFingerprint};
            pendingId=request.requestId;pendingAt=Now;pendingRevision=request.revision;
            if(IsHost) ProcessRequest(NetworkManager.ServerClientId,request);
            else if(!Send(RequestMessage,NetworkManager.ServerClientId,request)){pendingId=null;return Fail("SEND_FAILED");}
            Changed?.Invoke(); return true;
        }
        public void Leave()
        {
            intentionalClose=true;
            if(authority!=null){authority.Close("HOST_CLOSED_ROOM");Publish();}
            discovery.StopAdvertising();connection.Stop();ResetLocal();Status="Room closed";Changed?.Invoke();
        }
        private bool KeepLobbyOnPeerDisconnect() => MaximumParticipants > 2 && Snapshot?.phase == LobbyProtocol.Lobby;
        private void OnParticipantDisconnected(ulong clientId)
        {
            if (MaximumParticipants <= 2 || !IsHost || authority == null) return;
            admittedAt.Remove(clientId);
            if (authority.RemoveParticipant(clientId, out _)) Publish();
        }
        private void OnDiscoveryChanged()
        {
            if(!string.IsNullOrEmpty(discovery.LastError)){Error=discovery.LastError;FailureStage="DISCOVERY";}
            else if(FailureStage=="DISCOVERY"){Error="";FailureStage="";}
            Changed?.Invoke();
        }
        private void OnConnectionChanged()
        {
            Bind();
            if(connection.State==DirectConnectionState.Failed)
            {
                Error=!string.IsNullOrEmpty(connection.LastApprovalReason)?connection.LastApprovalReason:
                    !string.IsNullOrEmpty(connection.FailureCode)?connection.FailureCode:connection.Message;
                FailureStage=connection.FailureStage;
                Status=FailureStage=="APPROVAL"?"Host did not approve entry":FailureStage=="SESSION"?"Room connection ended":"Connection failed";
            }
            else if(connection.JoinInProgress||connection.State==DirectConnectionState.StartingHost)
            { Error="";FailureStage="";Status=connection.Message; }
            else if(Connected&&!InitialStateReady)
            { Error="";FailureStage="";Status=Snapshot==null?"Host approved / receiving room settings":"Confirming room settings with the host"; }
            Changed?.Invoke();
        }
        private void Bind()
        {
            var candidate=connection.OwnedManager;
            bool active=connection.State==DirectConnectionState.Connected&&candidate!=null&&candidate.IsListening&&!candidate.ShutdownInProgress;
            if(!active)
            {
                if(wasConnected&&!connection.JoinInProgress&&connection.State!=DirectConnectionState.Connecting&&connection.State!=DirectConnectionState.StartingHost)
                { Unbind(); discovery.StopAdvertising(); Snapshot=null;HostConfig=null;pendingId=null; if(!intentionalClose)Status="Room ended / connect again"; }
                return;
            }
            if(manager==candidate&&messaging==candidate.CustomMessagingManager)return;
            Unbind(); manager=candidate;messaging=candidate.CustomMessagingManager;wasConnected=true;joinedAt=Now;nextSync=0;
            int token=bindingGeneration;
            messaging.RegisterNamedMessageHandler(SyncMessage,(sender,reader)=>{if(token==bindingGeneration&&manager==candidate)ReceiveSync(sender,reader);});
            messaging.RegisterNamedMessageHandler(StateMessage,(sender,reader)=>{if(token==bindingGeneration&&manager==candidate)ReceiveSnapshot(sender,reader);});
            messaging.RegisterNamedMessageHandler(RequestMessage,(sender,reader)=>{if(token==bindingGeneration&&manager==candidate)ReceiveRequest(sender,reader);});
            messaging.RegisterNamedMessageHandler(ReplyMessage,(sender,reader)=>{if(token==bindingGeneration&&manager==candidate)ReceiveReply(sender,reader);});
            if(manager.IsHost)Publish();
        }
        private void Update()
        {
            discovery.Tick();Bind();
            if(!Connected)return;
            if(IsHost)
            {
                if(authority==null){Abort("HOST_STATE_MISSING");return;}
                foreach (var entry in admittedAt.ToArray())
                {
                    var participant = authority.Snapshot().Find(entry.Key);
                    if (participant == null || participant.initialStateReceived) { admittedAt.Remove(entry.Key); continue; }
                    if (Now - entry.Value <= 12) continue;
                    if (MaximumParticipants <= 2) { Abort("INITIAL_STATE_TIMEOUT"); return; }
                    admittedAt.Remove(entry.Key);
                    authority.RemoveParticipant(entry.Key, out _);
                    if (manager.ConnectedClientsIds.Contains(entry.Key)) manager.DisconnectClient(entry.Key, "INITIAL_STATE_TIMEOUT");
                    Publish();
                    Debug.Log($"C6_P3_LOBBY_ACK_TIMEOUT sender={entry.Key}");
                }
                if(Snapshot==null||Snapshot.revision!=authority.Revision)Publish();
                UpdateAdvertisement();
            }
            else
            {
                if(Snapshot==null&&Now-joinedAt>12){Abort("INITIAL_STATE_TIMEOUT");return;}
                if(Now>=nextSync)
                { nextSync=Now+.75;Send(SyncMessage,NetworkManager.ServerClientId,new LobbyHello{protocol=ProtocolVersion,build=buildIdentifier,roomId=expectedRoom,clientNonce=clientNonce}); }
                if(Snapshot!=null&&LocalPlayer!=null&&!LocalPlayer.initialStateReceived&&!HasPending&&Now>=ackAt)
                { ackAt=Now+.2;Submit(LobbyProtocol.AckInitial,false); }
            }
            if(HasPending&&Now-pendingAt>8)Abort("REQUEST_CONFIRMATION_TIMEOUT");
        }
        private void ReceiveSync(ulong sender,FastBufferReader reader)
        {
            if(!IsHost||!Read(reader,out LobbyHello hello)||!LobbyWire.ValidHello(hello)
                ||hello.protocol!=ProtocolVersion||hello.build!=buildIdentifier
                ||(!string.IsNullOrEmpty(hello.roomId)&&hello.roomId!=authority.RoomId)
                ||authority.NonceFor(sender)!=hello.clientNonce)return;
            Send(StateMessage,sender,authority.Snapshot(hello.clientNonce));
        }
        private void ReceiveRequest(ulong sender,FastBufferReader reader)
        { if(IsHost&&Read(reader,out LobbyRequest request))ProcessRequest(sender,request); }
        private void ProcessRequest(ulong sender,LobbyRequest request)
        {
            if(authority==null||authority.NonceFor(sender)==null)return;
            bool accepted=authority.Handle(sender,request,out string reason);
            var reply=new LobbyReply{requestId=request.requestId,sequence=request.sequence,accepted=accepted,reason=reason,snapshot=authority.Snapshot(authority.NonceFor(sender))};
            Debug.Log($"C6_T10A_REQUEST sender={sender} kind={request.kind} accepted={accepted} reason={reason} revision={authority.Revision}");
            if(sender==NetworkManager.ServerClientId)ApplyReply(reply);else Send(ReplyMessage,sender,reply);
            Publish();
        }
        private void ReceiveSnapshot(ulong sender,FastBufferReader reader)
        { if(!IsHost&&sender==NetworkManager.ServerClientId&&Read(reader,out LobbySnapshot value))ApplySnapshot(value); }
        private void ReceiveReply(ulong sender,FastBufferReader reader)
        { if(!IsHost&&sender==NetworkManager.ServerClientId&&Read(reader,out LobbyReply value))ApplyReply(value); }
        private void ApplyReply(LobbyReply reply)
        {
            if(reply==null||reply.requestId!=pendingId||reply.sequence!=sequence)return;
            if(!LobbyWire.AcceptsReceipt(Snapshot,reply.snapshot,connection.LocalClientId??ulong.MaxValue,buildIdentifier,
                IsHost?"":clientNonce,expectedRoom,pendingRevision))return;
            if((Snapshot==null||reply.snapshot.revision>Snapshot.revision)&&!ApplySnapshot(reply.snapshot))return;
            pendingId=null;LastReply=reply;Error=reply.accepted?"":reply.reason;
            RequestResolved?.Invoke(reply);Changed?.Invoke();
        }
        private bool ApplySnapshot(LobbySnapshot value)
        {
            string nonce=IsHost?"":clientNonce;
            if(value == null || value.protocol != ProtocolVersion || !LobbyWire.AcceptsSnapshot(Snapshot,value,buildIdentifier,nonce,expectedRoom))return false;
            if(!LobbyHostConfig.TryRead(value.hostConfigJson,out var hostConfig)){Abort("UNSUPPORTED_HOST_CONFIG");return false;}
            if(connection.LocalClientId.HasValue&&value.Find(connection.LocalClientId.Value)==null){Abort("PLAYER_ID_MISSING");return false;}
            bool started=Snapshot?.phase!=LobbyProtocol.Playing&&value.phase==LobbyProtocol.Playing;
            bool rosterChanged = MaximumParticipants > 2 && Snapshot != null && !LobbyWire.SameRoster(Snapshot, value)
                && value.phase == LobbyProtocol.Lobby;
            if (rosterChanged && HasPending) { pendingId = null; Error = "ROSTER_CHANGED_READY_AGAIN"; }
            Snapshot=value;HostConfig=hostConfig;expectedRoom=value.roomId;
            Status=value.phase==LobbyProtocol.Playing?"Start confirmed":!InitialStateReady?"Confirming room settings with the host":
                rosterChanged?"Players changed / confirm Ready again":value.ParticipantCount>=2?value.ParticipantCount+" players connected":"Waiting for participants";
            string key=value.roomId+":"+value.revision;
            if(key!=lastPublishedState){lastPublishedState=key;Debug.Log($"C6_T10A_STATE local={connection.LocalClientId} room={value.roomId} revision={value.revision} phase={value.phase} participants={value.ParticipantCount} p1Ready={value.p1?.ready} p2Ready={value.p2?.ready} p2Initial={value.p2?.initialStateReceived} canStart={value.canStart} config={value.configFingerprint}");}
            if(started)StartConfirmed?.Invoke(value.start);
            Changed?.Invoke();return true;
        }
        private void Publish()
        {
            if(!IsHost||authority==null)return;
            ApplySnapshot(authority.Snapshot());
            foreach(var peer in connection.ParticipantIds)if(peer!=NetworkManager.ServerClientId&&authority.NonceFor(peer)!=null)
                Send(StateMessage,peer,authority.Snapshot(authority.NonceFor(peer)));
        }
        private void UpdateAdvertisement()
        {
            var ad=new RoomAdvertisement{RoomId=authority.RoomId,Name=roomName,ProtocolVersion=ProtocolVersion,
                Build=buildIdentifier,ConfigHash=authority.ConfigFingerprint,Participants=authority.ParticipantCount,
                Status=authority.Phase==LobbyProtocol.Lobby?RoomAdvertisementStatus.Lobby:RoomAdvertisementStatus.Playing,Port=roomPort};
            string key=ad.RoomId+":"+ad.Participants+":"+ad.Status;
            if(!discovery.IsAdvertising)
            {
                if(Now<nextAdvertisementRetry)return;
                nextAdvertisementRetry=Now+3;
                if(discovery.Advertise(ad))advertisementKey=key;
            }
            else if(key!=advertisementKey){discovery.UpdateAdvertisement(ad);advertisementKey=key;}
        }
        private bool Send<T>(string name,ulong recipient,T value) where T:class
        {
            if(messaging==null||manager==null||!manager.IsListening||manager.ShutdownInProgress)return false;
            try { var bytes=LobbyWire.Encode(value);using(var writer=new FastBufferWriter(bytes.Length+4,Allocator.Temp))
                { writer.WriteValueSafe(bytes.Length);writer.WriteBytesSafe(bytes);messaging.SendNamedMessage(name,recipient,writer,NetworkDelivery.ReliableFragmentedSequenced); }return true; }
            catch(Exception ex){Debug.LogWarning("C6_T10A_SEND_ERROR type="+ex.GetType().Name);return false;}
        }
        private static bool Read<T>(FastBufferReader reader,out T value) where T:class
        {
            value=null;
            try { if(reader.Length-reader.Position<4)return false;reader.ReadValueSafe(out int count);
                if(count<2||count>LobbyWire.MaximumBytes||reader.Length-reader.Position!=count)return false;
                var bytes=new byte[count];reader.ReadBytesSafe(ref bytes,count);return LobbyWire.TryDecode(bytes,out value); }
            catch(Exception){return false;}
        }
        private bool Fail(string reason,string stage=""){Error=reason;FailureStage=stage;Changed?.Invoke();return false;}
        private void Abort(string reason){Error=reason;FailureStage=!InitialStateReady?"INITIAL_STATE":"SESSION";Status="Room ended";if(authority!=null)authority.Close(reason);discovery.StopAdvertising();connection.Stop();Changed?.Invoke();}
        private void ResetLocal(){discovery.StopAdvertising();advertisementKey=null;nextAdvertisementRetry=0;Unbind();authority=null;Snapshot=null;HostConfig=null;LastReply=null;Error="";FailureStage="";sequence=0;pendingId=null;clientNonce="";expectedRoom="";lastPublishedState=null;intentionalClose=false;admittedAt.Clear();}
        private void Unbind()
        {
            bindingGeneration++;
            if(messaging!=null){messaging.UnregisterNamedMessageHandler(SyncMessage);messaging.UnregisterNamedMessageHandler(StateMessage);messaging.UnregisterNamedMessageHandler(RequestMessage);messaging.UnregisterNamedMessageHandler(ReplyMessage);}
            messaging=null;manager=null;wasConnected=false;
        }
        private void OnDisable(){discovery?.StopBrowse();if(connection!=null&&(connection.JoinInProgress||connection.State==DirectConnectionState.Connected||connection.State==DirectConnectionState.Connecting||connection.State==DirectConnectionState.StartingHost))Leave();}
        private void OnApplicationPause(bool paused){ApplicationPauseChanged?.Invoke(paused);if(paused){discovery?.StopBrowse();if(connection!=null&&(connection.JoinInProgress||connection.State==DirectConnectionState.Connected||connection.State==DirectConnectionState.Connecting||connection.State==DirectConnectionState.StartingHost))Leave();}}
        private void OnDestroy(){if(connection!=null){connection.Changed-=OnConnectionChanged;connection.ParticipantDisconnected-=OnParticipantDisconnected;}Unbind();if(discovery!=null){discovery.Changed-=OnDiscoveryChanged;discovery.Dispose();}Changed=null;RequestResolved=null;StartConfirmed=null;ApplicationPauseChanged=null;}
    }
}
