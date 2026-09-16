using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using C6.Prototype.Lobby.Discovery;
using C6.Prototype.Networking;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace C6.Prototype.Lobby
{
    /// <summary>Explicit L1 build only. Tests data paths, never runs game admission or gameplay.</summary>
    public sealed class L1NetworkProbe : MonoBehaviour
    {
        public const string AppBuild = "25";
        public const ushort BasePort = 28011;
        const float AttemptSeconds = 3f;
        readonly List<Socket> echoSockets = new List<Socket>();
        readonly List<NetworkDriver> echoDrivers = new List<NetworkDriver>();
        readonly List<string> lines = new List<string>();
        readonly byte[] receiveBuffer = new byte[2048];
        Socket clientSocket;
        NetworkDriver clientDriver;
        int clientResourceToken;
        L1BonjourDiscovery discovery;
        RectTransform list;
        Text status, output;
        InputField direct;
        bool hosting, testing, saveFailed;
        int generation;
        string receiptPath, runId;
        Receipt receipt;
        static double Now => Time.realtimeSinceStartupAsDouble;

#if C6_L1_PROBE && (DEVELOPMENT_BUILD || UNITY_EDITOR)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            // The saved P4 scene stays intact; this diagnostic build deactivates it at runtime.
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
            new GameObject("L1 Network Diagnostic Only").AddComponent<L1NetworkProbe>();
        }
#endif

        void Start()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 30;
            runId = Guid.NewGuid().ToString("N");
            receipt = new Receipt { runId=runId, appBuild=AppBuild, unity=Application.unityVersion,
                startedUtc=DateTime.UtcNow.ToString("O"), gameplay="NOT_RUN" };
            var directory = Path.Combine(Application.persistentDataPath, "L1NetworkChecks");
            try { Directory.CreateDirectory(directory); receiptPath = Path.Combine(directory, runId + ".json"); }
            catch (Exception error) { Debug.LogWarning("C6_L1 RECEIPT_DIRECTORY_FAILED " + ErrorCode(error)); }
            discovery = new L1BonjourDiscovery();
            discovery.Changed += RefreshTargets;
            discovery.Diagnostic += value => Log("DISCOVERY " + value);
            BuildUi();
            Log("READY app=25 task=L1 game=NOT_RUN");
        }

        void Update()
        {
            discovery?.Tick();
            if (!hosting) return;
            foreach (var socket in echoSockets)
            {
                for (int i=0;i<16;i++)
                {
                    EndPoint sender = new IPEndPoint(socket.AddressFamily==AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any,0);
                    try
                    {
                        int count=socket.ReceiveFrom(receiveBuffer,ref sender);
                        if(count<7 || count>256 || !Encoding.ASCII.GetString(receiveBuffer,0,count).StartsWith("C6-L1/",StringComparison.Ordinal)) continue;
                        socket.SendTo(receiveBuffer,0,count,SocketFlags.None,sender);
                        Log("HOST_ECHO route=RAW family="+socket.AddressFamily);
                    }
                    catch(SocketException error)
                    {
                        if(error.SocketErrorCode!=SocketError.WouldBlock) Log("RAW_RECEIVE error="+error.SocketErrorCode);
                        break;
                    }
                }
            }
            for(int i=0;i<echoDrivers.Count;i++)
            {
                var driver=echoDrivers[i];
                try
                {
                    driver.ScheduleUpdate().Complete();
                    while(driver.Accept().IsCreated) { }
                    Unity.Networking.Transport.NetworkEvent.Type type;
                    while((type=driver.PopEvent(out var peer,out var reader))!=Unity.Networking.Transport.NetworkEvent.Type.Empty)
                    {
                        if(type!=Unity.Networking.Transport.NetworkEvent.Type.Data || reader.Length<7 || reader.Length>256) continue;
                        var data=new byte[reader.Length];for(int b=0;b<data.Length;b++)data[b]=reader.ReadByte();
                        if(!Encoding.ASCII.GetString(data).StartsWith("C6-L1/",StringComparison.Ordinal))continue;
                        if(driver.BeginSend(peer,out var writer)!=0)continue;
                        foreach(byte value in data) writer.WriteByte(value);
                        int sent=driver.EndSend(writer);
                        if(sent>=0)Log("HOST_ECHO route=UTP family="+driver.GetLocalEndpoint().Family);
                        else Log("HOST_SEND route=UTP error="+sent);
                    }
                    echoDrivers[i]=driver;
                }
                catch(Exception error)
                {
                    echoDrivers.RemoveAt(i--);
                    if(driver.IsCreated)driver.Dispose();
                    Log("HOST_UPDATE route=UTP FAIL "+ErrorCode(error));
                }
            }
        }

        void Host()
        {
            StopAll();
            receipt.role="HOST";
            foreach(var family in new[]{AddressFamily.InterNetwork,AddressFamily.InterNetworkV6})
            {
                Socket socket=null;
                try
                {
                    socket=new Socket(family,SocketType.Dgram,ProtocolType.Udp);
                    if(family==AddressFamily.InterNetworkV6)socket.DualMode=false;
                    socket.Blocking=false;
                    socket.Bind(new IPEndPoint(family==AddressFamily.InterNetwork?IPAddress.Any:IPAddress.IPv6Any,BasePort));
                    echoSockets.Add(socket);Log("HOST_BIND RAW "+family+" PASS");
                }
                catch(Exception e){socket?.Dispose();Log("HOST_BIND RAW "+family+" FAIL "+ErrorCode(e));}
                NetworkDriver driver=default;
                try
                {
                    driver=NetworkDriver.Create();
                    var bind=family==AddressFamily.InterNetwork?NetworkEndpoint.AnyIpv4.WithPort(BasePort+1):NetworkEndpoint.AnyIpv6.WithPort(BasePort+2);
                    int error=driver.Bind(bind);
                    if(error==0)error=driver.Listen();
                    if(error==0){echoDrivers.Add(driver);driver=default;Log("HOST_BIND UTP "+family+" PASS");}
                    else Log("HOST_BIND UTP "+family+" FAIL code="+error);
                }
                catch(Exception error){Log("HOST_BIND UTP "+family+" FAIL "+ErrorCode(error));}
                finally{if(driver.IsCreated)driver.Dispose();}
            }
            hosting=echoSockets.Count>0 || echoDrivers.Count>0;
            if(hosting)
            {
                string name="C6-L1-"+runId.Substring(0,6);
                discovery.StartAdvertise(name,BasePort);
                status.text="HOST: "+name+"\nKeep this screen open. On the other device tap FIND HOST.";
            }
            Save();
        }

        void Browse()
        {
            StopAll();receipt.role="CLIENT";discovery.StartBrowse();
            status.text="Finding L1 hosts… Tap the discovered host once to test all its addresses.";
            Save();
        }

        void RefreshTargets()
        {
            if(list==null || testing)return;
            foreach(Transform child in list) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            foreach(var group in discovery.Targets.GroupBy(t=>t.ServiceName))
            {
                string name=group.Key;
                var targets=SelectCandidates(group);
                AddButton(list,name+" / "+targets.Length+" addresses",()=>BeginChecks(targets));
            }
            if(!string.IsNullOrEmpty(discovery.LastError))status.text=discovery.LastError;
        }

        // Keep at least one representative of every available route before filling the cap.
        // Scoped link-local addresses on distinct interfaces remain distinct candidates.
        static L1BonjourTarget[] SelectCandidates(IEnumerable<L1BonjourTarget> source)
        {
            var candidates=source.Where(t=>t!=null && t.Port>0 && t.Port<=ushort.MaxValue-2 && IPAddress.TryParse(t.Address,out _))
                .GroupBy(t=>IPAddress.Parse(t.Address).ToString()+"/"+t.Port).Select(g=>g.First()).ToArray();
            var buckets=new[]
            {
                new Queue<L1BonjourTarget>(candidates.Where(t=>!t.Address.Contains(":"))),
                new Queue<L1BonjourTarget>(candidates.Where(t=>t.Address.Contains(":")&&!t.Address.Contains("%"))),
                new Queue<L1BonjourTarget>(candidates.Where(t=>t.Address.Contains(":")&&t.Address.Contains("%")))
            };
            var selected=new List<L1BonjourTarget>(8);
            while(selected.Count<8 && buckets.Any(bucket=>bucket.Count>0))
                foreach(var bucket in buckets)if(bucket.Count>0 && selected.Count<8)selected.Add(bucket.Dequeue());
            return selected.ToArray();
        }

        void BeginChecks(IReadOnlyList<L1BonjourTarget> targets)
        {
            if(testing || hosting)return;
            var copy=SelectCandidates(targets).Select(t=>new Target {address=t.Address,port=t.Port,service=t.ServiceName}).ToArray();
            if(copy.Length==0){status.text="No usable numeric address was resolved. Refresh the hosts.";return;}
            StartCoroutine(CheckAll(copy,++generation));
        }

        void DirectCheck()
        {
            if(testing || hosting)return;
            if(!IPAddress.TryParse(direct.text.Trim(),out _)){status.text="Enter a numeric IPv4 or IPv6 address.";return;}
            StartCoroutine(CheckAll(new[]{new Target{address=direct.text.Trim(),port=BasePort,service="DIRECT"}},++generation));
        }

        IEnumerator CheckAll(Target[] targets,int token)
        {
            testing=true;receipt.role="CLIENT";
            try
            {
                foreach(var target in targets)
                {
                    if(token!=generation)yield break;
                    status.text="Testing "+target.address+"\nRAW UDP → Unity Transport. Keep both apps open.";
                    yield return CheckRaw(target,token);
                    bool ipv6=target.address.Contains(":");
                    bool scoped=target.address.Contains("%");
                    yield return CheckUtp(target,token,false);
                    if(ipv6 && scoped)yield return CheckUtp(target,token,true);
                }
                status.text="CHECK COMPLETE\nResults below. Game connection has not been tested.";
            }
            finally {testing=false;Save();RefreshTargets();}
        }

        IEnumerator CheckRaw(Target target,int token)
        {
            var result=NewResult(target,"RAW");
            Socket socket=null;IPEndPoint endpoint=null;byte[] challenge=Challenge();double started=Now;
            try
            {
                endpoint=new IPEndPoint(IPAddress.Parse(target.address),target.port);
                if(endpoint.AddressFamily==AddressFamily.InterNetworkV6)result.scope=(uint)endpoint.Address.ScopeId;
                socket=new Socket(endpoint.AddressFamily,SocketType.Dgram,ProtocolType.Udp);
                clientSocket=socket;clientResourceToken=token;
                if(endpoint.AddressFamily==AddressFamily.InterNetworkV6)socket.DualMode=false;
                socket.Blocking=false;
                socket.Bind(new IPEndPoint(endpoint.AddressFamily==AddressFamily.InterNetwork?IPAddress.Any:IPAddress.IPv6Any,0));
            }
            catch(Exception e){result.detail=ErrorCode(e);ReleaseClientResources(token);Record(result);yield break;}
            try
            {
                double nextSend=0;
                while(Now-started<AttemptSeconds && token==generation)
                {
                    if(Now>=nextSend)
                    {
                        nextSend=Now+.5;
                        try{socket.SendTo(challenge,endpoint);result.sent++;}catch(SocketException e){result.detail=e.SocketErrorCode.ToString();}
                    }
                    EndPoint from=new IPEndPoint(endpoint.AddressFamily==AddressFamily.InterNetwork?IPAddress.Any:IPAddress.IPv6Any,0);
                    int count=0;
                    try{count=socket.ReceiveFrom(receiveBuffer,ref from);}catch(SocketException e){if(e.SocketErrorCode!=SocketError.WouldBlock)result.detail=e.SocketErrorCode.ToString();}
                    if(count==challenge.Length && from is IPEndPoint source && source.Equals(endpoint) && receiveBuffer.Take(count).SequenceEqual(challenge))
                    {result.status="PASS";result.detail="Matched source and random challenge echo";break;}
                    yield return null;
                }
            }
            finally{ReleaseClientResources(token);result.seconds=Now-started;if(token!=generation)result.status="CANCELLED";Record(result);}
        }

        IEnumerator CheckUtp(Target target,int token,bool scoped)
        {
            var result=NewResult(target,scoped?"UTP_SCOPED":"UTP_STOCK");
            double started=Now,nextSend=0;
            try
            {
                if(!TryStartUtp(target,scoped,token,result,out var connection))yield break;
                var challenge=Challenge();
                while(Now-started<AttemptSeconds && token==generation)
                {
                    if(!AdvanceUtp(connection,challenge,result,ref nextSend))break;
                    if(result.status=="PASS")break;
                    yield return null;
                }
            }
            finally{ReleaseClientResources(token);result.seconds=Now-started;if(token!=generation)result.status="CANCELLED";Record(result);}
        }

        bool TryStartUtp(Target target,bool scoped,int token,Result result,out NetworkConnection connection)
        {
            connection=default;
            try
            {
                bool ipv6=target.address.Contains(":");
                if(target.port==0 || target.port>ushort.MaxValue-2){result.detail="INVALID_PROBE_PORT";return false;}
                result.port=(ushort)(target.port+(ipv6?2:1));
                var bare=target.address.Split('%')[0];NetworkEndpoint endpoint;
                bool parsed=scoped?L1TransportEndpoint.TryParse(target.address,result.port,out endpoint,out _):
                    NetworkEndpoint.TryParse(bare,result.port,out endpoint,ipv6?NetworkFamily.Ipv6:NetworkFamily.Ipv4);
                if(!parsed){result.detail="ENDPOINT_PARSE_FAILED";return false;}
                result.scope=L1TransportEndpoint.GetScopeId(endpoint);
                clientResourceToken=token;clientDriver=NetworkDriver.Create();
                int bind=clientDriver.Bind(ipv6?NetworkEndpoint.AnyIpv6:NetworkEndpoint.AnyIpv4);
                if(bind!=0){result.detail="BIND_FAILED_"+bind;return false;}
                connection=clientDriver.Connect(endpoint);
                if(!connection.IsCreated){result.detail="CONNECT_NOT_CREATED";return false;}
                return true;
            }
            catch(Exception error){result.detail="UTP_SETUP_"+ErrorCode(error);return false;}
        }

        bool AdvanceUtp(NetworkConnection connection,byte[] challenge,Result result,ref double nextSend)
        {
            try
            {
                clientDriver.ScheduleUpdate().Complete();
                Unity.Networking.Transport.NetworkEvent.Type type;
                while((type=clientDriver.PopEvent(out var peer,out var reader))!=Unity.Networking.Transport.NetworkEvent.Type.Empty)
                {
                    if(peer!=connection)continue;
                    if(type==Unity.Networking.Transport.NetworkEvent.Type.Connect)result.connected=true;
                    if(type==Unity.Networking.Transport.NetworkEvent.Type.Disconnect){result.detail="UTP_DISCONNECTED";return false;}
                    if(type==Unity.Networking.Transport.NetworkEvent.Type.Data && reader.Length==challenge.Length)
                    {
                        var data=new byte[reader.Length];for(int i=0;i<data.Length;i++)data[i]=reader.ReadByte();
                        if(data.SequenceEqual(challenge)){result.status="PASS";result.detail="Matched UTP connection and random challenge echo";return true;}
                    }
                }
                if(result.connected && Now>=nextSend)
                {
                    nextSend=Now+.5;
                    int begin=clientDriver.BeginSend(connection,out var writer);
                    if(begin!=0){result.detail="UTP_BEGIN_SEND_"+begin;return true;}
                    foreach(byte value in challenge)writer.WriteByte(value);
                    int sent=clientDriver.EndSend(writer);
                    if(sent>=0)result.sent++;
                    else result.detail="UTP_SEND_"+sent;
                }
                return true;
            }
            catch(Exception error){result.detail="UTP_UPDATE_"+ErrorCode(error);return false;}
        }

        void ReleaseClientResources(int token)
        {
            if(token!=clientResourceToken)return;
            clientResourceToken=0;
            var socket=clientSocket;clientSocket=null;
            var driver=clientDriver;clientDriver=default;
            socket?.Dispose();
            if(driver.IsCreated)driver.Dispose();
        }

        Result NewResult(Target target,string route)=>new Result{address=target.address,port=target.port,service=target.service,
            route=route,family=target.address.Contains(":")?"IPv6":"IPv4",status="FAIL",detail="NO_MATCHING_ECHO_WITHIN_3_SECONDS"};
        static byte[] Challenge()=>Encoding.ASCII.GetBytes("C6-L1/"+Guid.NewGuid().ToString("N"));
        static string ErrorCode(Exception e)=>e is SocketException socket?socket.SocketErrorCode.ToString():e.GetType().Name;
        void Record(Result result)
        {
            receipt.results.Add(result);
            Log(result.route+" "+result.family+" scope="+result.scope+" "+result.status+" "+result.seconds.ToString("F2")+"s "+result.detail);
        }
        void Log(string value)
        {
            lines.Add(value);if(lines.Count>60)lines.RemoveAt(0);
            if(output!=null)output.text=string.Join("\n",lines);
            Debug.Log("C6_L1 "+value);Save();
        }
        void Save()
        {
            if(receipt==null || receiptPath==null)return;
            receipt.updatedUtc=DateTime.UtcNow.ToString("O");receipt.logs=lines.ToArray();
            try { File.WriteAllText(receiptPath,JsonUtility.ToJson(receipt,true)); }
            catch(Exception error)
            {
                if(!saveFailed)Debug.LogWarning("C6_L1 RECEIPT_WRITE_FAILED "+ErrorCode(error));
                saveFailed=true;
            }
        }
        void StopAll()
        {
            generation++;StopAllCoroutines();ReleaseClientResources(clientResourceToken);testing=false;hosting=false;
            discovery?.Stop();
            foreach(var socket in echoSockets)socket.Dispose();echoSockets.Clear();
            foreach(var driver in echoDrivers)if(driver.IsCreated)driver.Dispose();echoDrivers.Clear();
            if(status!=null)status.text="Stopped. Choose HOST or FIND HOST.";
        }
        void OnApplicationPause(bool paused){if(paused){StopAll();Log("PAUSED diagnostic stopped; choose role again");}}
        void OnDestroy(){StopAll();discovery?.Dispose();}

        void BuildUi()
        {
            var canvas=new GameObject("L1 Canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.transform.SetParent(transform);
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(390,844);scaler.matchWidthOrHeight=0;
            new GameObject("L1 Input",typeof(EventSystem),typeof(InputSystemUIInputModule)).transform.SetParent(transform);
            var panel=new GameObject("Panel",typeof(RectTransform),typeof(Image));panel.transform.SetParent(canvas.transform,false);panel.GetComponent<Image>().color=new Color(.04f,.06f,.1f);
            var rect=(RectTransform)panel.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;
            var scrollObject=new GameObject("Scroll",typeof(RectTransform),typeof(ScrollRect));scrollObject.transform.SetParent(panel.transform,false);
            var sr=(RectTransform)scrollObject.transform;sr.anchorMin=Vector2.zero;sr.anchorMax=Vector2.one;sr.offsetMin=new Vector2(16,36);sr.offsetMax=new Vector2(-16,-60);
            var viewport=new GameObject("Viewport",typeof(RectTransform),typeof(Image),typeof(RectMask2D));viewport.transform.SetParent(sr,false);viewport.GetComponent<Image>().color=Color.clear;var vr=(RectTransform)viewport.transform;vr.anchorMin=Vector2.zero;vr.anchorMax=Vector2.one;vr.offsetMin=vr.offsetMax=Vector2.zero;
            list=null;var content=new GameObject("Content",typeof(RectTransform),typeof(VerticalLayoutGroup),typeof(ContentSizeFitter));content.transform.SetParent(vr,false);
            var cr=(RectTransform)content.transform;cr.anchorMin=new Vector2(0,1);cr.anchorMax=Vector2.one;cr.pivot=new Vector2(.5f,1);cr.sizeDelta=Vector2.zero;
            var layout=content.GetComponent<VerticalLayoutGroup>();layout.spacing=12;layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.childControlWidth=true;layout.childForceExpandWidth=true;
            content.GetComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=scrollObject.GetComponent<ScrollRect>();scroll.viewport=vr;scroll.content=cr;scroll.horizontal=false;
            AddText(cr,"C6 NETWORK CHECK / L1\nApp 25 · diagnostic only",24);
            status=AddText(cr,"iPhone hotspot: HOST\niPad: FIND HOST → tap the discovered host.",18);
            AddButton(cr,"HOST — keep open",Host);AddButton(cr,"FIND HOST",Browse);AddButton(cr,"STOP / CANCEL",()=>{StopAll();Log("USER_STOP");});
            var group=new GameObject("Hosts",typeof(RectTransform),typeof(VerticalLayoutGroup),typeof(ContentSizeFitter));group.transform.SetParent(cr,false);list=(RectTransform)group.transform;
            var gl=group.GetComponent<VerticalLayoutGroup>();gl.spacing=8;gl.childControlHeight=true;gl.childForceExpandHeight=false;
            group.GetComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            AddText(cr,"Optional direct IP (diagnostic ports are fixed)",15);
            var inputObject=new GameObject("Direct address",typeof(RectTransform),typeof(Image),typeof(InputField),typeof(LayoutElement));inputObject.transform.SetParent(cr,false);
            inputObject.GetComponent<LayoutElement>().preferredHeight=52;inputObject.GetComponent<Image>().color=new Color(.12f,.17f,.22f);
            direct=inputObject.GetComponent<InputField>();direct.targetGraphic=inputObject.GetComponent<Image>();direct.characterLimit=64;direct.keyboardType=TouchScreenKeyboardType.ASCIICapable;
            var inputText=AddText((RectTransform)inputObject.transform,"",17);var tr=inputText.rectTransform;tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=new Vector2(8,4);tr.offsetMax=new Vector2(-8,-4);direct.textComponent=inputText;
            AddButton(cr,"TEST DIRECT",DirectCheck);
            output=AddText(cr,"",15);
        }
        static Text AddText(RectTransform parent,string value,int size)
        {
            var go=new GameObject("Text",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
            var text=go.GetComponent<Text>();text.font=UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=size;text.color=Color.white;text.text=value;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.raycastTarget=false;
            return text;
        }
        static void AddButton(RectTransform parent,string title,UnityEngine.Events.UnityAction action)
        {
            var go=new GameObject(title,typeof(RectTransform),typeof(Image),typeof(Button),typeof(LayoutElement));go.transform.SetParent(parent,false);go.GetComponent<Image>().color=new Color(.1f,.3f,.4f);go.GetComponent<LayoutElement>().preferredHeight=58;
            go.GetComponent<Button>().targetGraphic=go.GetComponent<Image>();go.GetComponent<Button>().onClick.AddListener(action);var text=AddText((RectTransform)go.transform,title,18);text.alignment=TextAnchor.MiddleCenter;
            var rect=text.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(8,3);rect.offsetMax=new Vector2(-8,-3);
        }
        sealed class Target{public string address,service;public ushort port;}
        [Serializable]sealed class Receipt{public string runId,appBuild,unity,startedUtc,updatedUtc,role,gameplay;public string[] logs;public List<Result> results=new List<Result>();}
        [Serializable]sealed class Result{public string address,service,route,family,status,detail;public ushort port;public uint scope;public int sent;public bool connected;public double seconds;}
    }
}
