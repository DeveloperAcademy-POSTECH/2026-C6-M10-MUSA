using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
namespace C6Lab.Tests
{
    public sealed class LabFigmaLobbyTests
    {
        private readonly LabSavedSceneNetworkFixture fixture=new LabSavedSceneNetworkFixture();
        private LabNetwork network; private LabLobbyView view;
        private Mouse pointer;
        private InputSettings originalInputSettings, fixtureInputSettings;
        private HideFlags originalInputFlags;
        private readonly List<InputDevice> disabledMice=new List<InputDevice>();
        [UnitySetUp]public IEnumerator Load()
        {
            yield return fixture.Load();view=Object.FindFirstObjectByType<LabLobbyView>();
            Assert.That(view,Is.Not.Null);network=view.hud.network;fixture.UseAvailablePort(network);
            view.hud.port.SetTextWithoutNotify(network.port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        [UnityTearDown]public IEnumerator Cleanup()
        {
            if(pointer!=null){InputSystem.RemoveDevice(pointer);pointer=null;}
            // Restore the asset reference before unloading the fixture Scene and its unused assets.
            if(fixtureInputSettings!=null)
            {InputSystem.settings=originalInputSettings;originalInputSettings.hideFlags=originalInputFlags;Object.Destroy(fixtureInputSettings);fixtureInputSettings=null;originalInputSettings=null;}
            if(network!=null)network.enabled=true;
            yield return fixture.Cleanup();
            foreach(var device in disabledMice)if(device.added)InputSystem.EnableDevice(device);
            disabledMice.Clear();
        }
        [UnityTest]public IEnumerator StartAndEmptyBrowserUseSavedFontsAndAssets()
        {
            Assert.That(view.titlePage.activeSelf,Is.True);Assert.That(view.browserPage.activeSelf,Is.False);
            Assert.That(view.mediumFont.name,Does.Contain("Shilla"));
            Assert.That(view.emptySlots.Concat(view.filledSlots).Concat(view.lines).All(s=>s!=null&&s.texture.width>0),Is.True);
            view.enterButton.onClick.Invoke();yield return null;
            Assert.That(view.browserPage.activeSelf,Is.True);Assert.That(view.titlePage.activeSelf,Is.False);
            Assert.That(view.countButtons.Length,Is.EqualTo(4));
            view.countButtons[0].onClick.Invoke();Assert.That(network.TargetParticipantCount,Is.EqualTo(2));
            view.countButtons[3].onClick.Invoke();Assert.That(network.TargetParticipantCount,Is.EqualTo(5));
            Assert.That(view.emptyRooms.activeSelf,Is.EqualTo(network.DiscoveredRooms.Count==0));
        }
        [UnityTest]public IEnumerator HostChoosesAndClearsAnyPositionAndEveryCapacityRequiresAllSlotsAndSettings()
        {
            for(int capacity=2;capacity<=5;capacity++)
            {
                while(!network.CanChooseConnection)yield return null;
                Assert.That(network.SetTargetParticipantCount(capacity),Is.True);fixture.UseAvailablePort(network);
                network.Host();yield return null;network.enabled=false;
                Assert.That(network.LocalSlotNumber,Is.Zero,"Authority does not reserve a P number.");
                Assert.That(view.positionPage.activeSelf,Is.True);Assert.That(view.slotButtons[0].interactable,Is.True);
                Assert.That(view.slotButtons.Count(b=>b.gameObject.activeSelf),Is.EqualTo(capacity));
                Assert.That(view.slotImages[0].sprite,Is.SameAs(view.emptySlots[0]));
                Assert.That(view.slotImages[0].sprite,Is.Not.SameAs(view.filledSlots[0]),"An unclaimed P1 needs dashed, unoccupied art.");
                view.slotButtons[capacity-1].onClick.Invoke();yield return null;
                Assert.That(network.LocalSlotNumber,Is.EqualTo(capacity));
                Assert.That(view.localLabels[capacity-1].text,Does.Contain("방장"));
                if(capacity!=1)Assert.That(view.localLabels[0].text,Does.Not.Contain("방장"));
                Assert.That(view.clearSlotButton.interactable,Is.True);
                view.clearSlotButton.onClick.Invoke();yield return null;
                Assert.That(network.LocalSlotNumber,Is.Zero);Assert.That(network.IsHost,Is.True);
                view.slotButtons[capacity-1].onClick.Invoke();yield return null;
                var order=Field<List<ulong>>("order");var slots=Field<LabPlayerSlots>("playerSlots");var ack=Field<HashSet<ulong>>("configAcknowledgements");
                ulong[] peers=Enumerable.Range(1,capacity-1).Select(i=>(ulong)(200+i)).ToArray();
                order.AddRange(peers);foreach(var id in peers)ack.Add(id);
                Assert.That(network.CanStartBattle,Is.False);
                for(int i=0;i<peers.Length;i++)Assert.That(slots.TrySelect(peers[i],i+1,out _),Is.True);
                yield return null;Assert.That(network.CanStartBattle,Is.True);Assert.That(view.sealButton.interactable,Is.True);
                Assert.That(view.heading.text,Is.EqualTo("요괴 봉인 준비 완료!"));
                ack.Remove(peers[0]);yield return null;Assert.That(view.sealButton.interactable,Is.False);
                ack.Add(peers[0]);slots.Release(peers[0]);yield return null;Assert.That(view.sealButton.interactable,Is.False);
                Assert.That(network.LocalSlotNumber,Is.EqualTo(capacity));
                foreach(var id in peers){order.Remove(id);slots.Release(id);ack.Remove(id);}
                network.enabled=true;network.Stop();yield return null;
            }
        }
        [UnityTest]public IEnumerator SoloGameplayHidesLobbyAndReturnRestoresRoomBrowser()
        {
            Assert.That(network.StartDeveloperSolo(),Is.True);yield return null;yield return null;
            Assert.That(view.design.gameObject.activeSelf,Is.False);Assert.That(view.background.gameObject.activeSelf,Is.False);
            Assert.That(view.hud.battleUi.activeSelf,Is.True);
            network.Stop();yield return null;yield return null;
            Assert.That(view.design.gameObject.activeSelf,Is.True);
        }
        [UnityTest]public IEnumerator BrowserRendersOneAndThreeRoomsFromDiscoveredPresence()
        {
            view.EnterLobby(); network.enabled=false;
            for(int count=1;count<=3;count+=2)
            {
                var targets=Enumerable.Range(0,count).Select(i=>new LabBonjourTarget("127.0.0.1",(ushort)(19000+i),1,"C6Lab-"+(24+i),i+1,5,false));
                Set("discoveredRooms",LabRoomCatalog.GroupTargets(targets));
                yield return null;yield return null;
                Assert.That(view.emptyRooms.activeSelf,Is.False);
                var rows=view.roomContent.GetComponentsInChildren<UnityEngine.UI.Button>().Where(b=>b!=view.roomTemplate).ToArray();
                Assert.That(rows.Length,Is.EqualTo(count));
                for(int i=0;i<count;i++)
                {
                    Assert.That(rows[i].transform.Find("Name").GetComponent<UnityEngine.UI.Text>().text,Is.EqualTo("수장고-"+(24+i)));
                    Assert.That(rows[i].transform.Find("Count").GetComponent<UnityEngine.UI.Text>().text,Is.EqualTo((i+1)+" / 5명 모였소"));
                }
            }
            Set("discoveredRooms",System.Array.Empty<LabDiscoveredRoom>());yield return null;yield return null;
            Assert.That(view.emptyRooms.activeSelf,Is.True);
        }
        [UnityTest]public IEnumerator CreatedRoomStaysInBrowserUntilItsCardIsPressedAndLeaveReturnsToBrowser()
        {
            view.EnterLobby();yield return null;yield return ClickPointer(view.createButton);
            Assert.That(network.IsConnected,Is.False);
            Assert.That(view.browserPage.activeSelf,Is.True);Assert.That(view.positionPage.activeSelf,Is.False);
            Assert.That(view.emptyRooms.activeSelf,Is.False,view.message.text);Assert.That(view.createButton.interactable,Is.False,view.message.text);
            Assert.That(view.countButtons.All(button=>button.interactable),Is.True);
            double deadline=Time.realtimeSinceStartupAsDouble+12;
            while(!VisibleRows().Any(row=>row.interactable)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            yield return new WaitForSeconds(2);
            var rows=VisibleRows();
            Assert.That(rows.Length,Is.EqualTo(1),view.message.text);Assert.That(rows[0].interactable,Is.True,view.message.text);
            string name=rows[0].transform.Find("Name").GetComponent<UnityEngine.UI.Text>().text;
            Assert.That(rows[0].transform.Find("Count").GetComponent<UnityEngine.UI.Text>().text,Is.EqualTo("1 / 3명 모였소"));
            Assert.That(view.positionPage.activeSelf,Is.False,"Name registration must not automatically enter the room.");
            yield return ClickPointer(rows[0]);
            Assert.That(network.IsHost,Is.True);Assert.That(LabRoomNames.Display(network.ConnectedRoomName),Is.EqualTo(name));
            Assert.That(view.positionPage.activeSelf,Is.True);Assert.That(view.browserPage.activeSelf,Is.False);
            Assert.That(network.LocalSlotNumber,Is.Zero);
            var leaveLabel=view.leaveButton.GetComponentInChildren<UnityEngine.UI.Text>();
            Assert.That(leaveLabel.alignment,Is.EqualTo(TextAnchor.MiddleLeft));
            Assert.That(((RectTransform)view.leaveButton.transform).anchoredPosition,Is.EqualTo(new Vector2(200,-93)));
            Assert.That(leaveLabel.rectTransform.anchoredPosition,Is.EqualTo(new Vector2(173,-22)));
            AssertVisibleHit(view.slotButtons[1]);
            yield return ClickPointer(view.leaveButton);yield return null;
            Assert.That(network.IsConnected,Is.False);Assert.That(view.browserPage.activeSelf,Is.True);
        }
        [UnityTest]public IEnumerator MultipleCreatedRoomsResetTheFormAndOnlyTheirCreatorActivatesTheChosenRoom()
        {
            view.EnterLobby();yield return null;
            foreach(int count in new[]{3,5,4})
            {
                yield return ClickPointer(view.countButtons[count-2]);
                Assert.That(view.createButton.interactable,Is.True);
                yield return ClickPointer(view.createButton);
                Assert.That(network.IsConnected,Is.False);Assert.That(network.CanChooseConnection,Is.True);
                Assert.That(view.countButtons.All(button=>button.interactable),Is.True);
                Assert.That(view.countButtons.All(button=>button.targetGraphic.color==LabLobbyView.Html("E8DFCB")),Is.True,view.message.text);
                Assert.That(view.createButton.interactable,Is.False,"Choose the next room's capacity after reset.");
                Assert.That(view.refreshButton.gameObject.activeSelf,Is.True);
                Assert.That(view.cancelButton.gameObject.activeSelf,Is.False);
                Assert.That(view.hud.developerMode.toggleButton.gameObject.activeSelf,Is.True);
            }
            double deadline=Time.realtimeSinceStartupAsDouble+25;
            while(Time.realtimeSinceStartupAsDouble<deadline&&(VisibleRows().Length!=3||VisibleRows().Any(row=>!row.interactable)))yield return null;
            var rows=VisibleRows();Assert.That(rows.Length,Is.EqualTo(3));
            var names=rows.Select(row=>row.transform.Find("Name").GetComponent<UnityEngine.UI.Text>().text).ToArray();
            Assert.That(names.Distinct().Count(),Is.EqualTo(3),string.Join(", ",names));
            Assert.That(rows.Select(row=>row.transform.Find("Count").GetComponent<UnityEngine.UI.Text>().text),Is.EqualTo(new[]{"1 / 3명 모였소","1 / 5명 모였소","1 / 4명 모였소"}));
            using(var browser=new LabBonjourDiscovery())
            {
                Assert.That(browser.StartBrowse(),Is.True);
                deadline=Time.realtimeSinceStartupAsDouble+15;
                while(Time.realtimeSinceStartupAsDouble<deadline)
                {
                    browser.Tick();
                    if(LabRoomCatalog.GroupTargets(browser.Targets).Count(room=>names.Contains(LabRoomNames.Display(room.Name))&&room.WaitingForHost)==3)break;
                    yield return null;
                }
                Assert.That(LabRoomCatalog.GroupTargets(browser.Targets).Count(room=>names.Contains(LabRoomNames.Display(room.Name))&&room.WaitingForHost),Is.EqualTo(3));
                var pending=LabRoomCatalog.GroupTargets(browser.Targets).Where(room=>names.Contains(LabRoomNames.Display(room.Name))&&room.WaitingForHost).ToArray();
                Assert.That(pending.Select(room=>room.Port).Distinct().Count(),Is.EqualTo(3));
                foreach(var room in pending)Assert.That(network.CanJoinDiscoveredRoom(room.Key),Is.False,"Others must wait for this room's creator.");
                yield return ClickPointer(rows[1]);
                Assert.That(network.IsHost,Is.True);Assert.That(network.LocalSlotNumber,Is.Zero);
                Assert.That(network.TargetParticipantCount,Is.EqualTo(5));Assert.That(LabRoomNames.Display(network.ConnectedRoomName),Is.EqualTo(names[1]));
                deadline=Time.realtimeSinceStartupAsDouble+10;
                while(Time.realtimeSinceStartupAsDouble<deadline)
                {
                    browser.Tick();
                    if(browser.Targets.Any(room=>LabRoomNames.Display(room.ServiceName)==names[1]&&!room.WaitingForHost))break;
                    yield return null;
                }
                Assert.That(browser.Targets.Any(room=>LabRoomNames.Display(room.ServiceName)==names[1]&&!room.WaitingForHost),Is.True);
                Assert.That(LabRoomCatalog.GroupTargets(browser.Targets).Count(room=>names.Contains(LabRoomNames.Display(room.Name))&&room.WaitingForHost),Is.EqualTo(2));
            }
            network.Stop();yield return null;yield return null;
            Assert.That(view.browserPage.activeSelf,Is.True);
            deadline=Time.realtimeSinceStartupAsDouble+5;
            while(VisibleRows().Length!=2&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(VisibleRows().Length,Is.EqualTo(2));
            yield return ClickPointer(view.countButtons[0]);Assert.That(view.createButton.interactable,Is.True);
        }
        [UnityTest]public IEnumerator NativeBonjourPublishesLiveCountAndBattleChanges()
        {
            if(!LabBonjourDiscovery.IsSupported)Assert.Ignore("Bonjour is supported only on macOS/iOS.");
            string name="C6Lab-test263-"+System.Guid.NewGuid().ToString("N").Substring(0,8);
            using(var host=new LabBonjourDiscovery())using(var browser=new LabBonjourDiscovery())
            {
                Assert.That(host.StartAdvertise(name,19163,true,true,1,3),Is.True,host.LastError);
                Assert.That(browser.StartBrowse(),Is.True,browser.LastError);
                double deadline=Time.realtimeSinceStartupAsDouble+12;
                while(Time.realtimeSinceStartupAsDouble<deadline&&!browser.Targets.Any(t=>t.ServiceName==name))
                {host.Tick();browser.Tick();yield return null;}
                Assert.That(browser.Targets.Any(t=>t.ServiceName==name&&t.Participants==1&&t.TargetParticipants==3),Is.True,browser.LastError);
                Assert.That(host.UpdatePresence(3,3,true),Is.True,host.LastError);
                deadline=Time.realtimeSinceStartupAsDouble+12;
                while(Time.realtimeSinceStartupAsDouble<deadline&&!browser.Targets.Any(t=>t.ServiceName==name&&t.Participants==3&&t.Playing))
                {host.Tick();browser.Tick();yield return null;}
                Assert.That(browser.Targets.Any(t=>t.ServiceName==name&&t.Participants==3&&t.Playing),Is.True,"Live TXT updates must reach existing room routes.");
            }
        }
        [UnityTest]public IEnumerator DeveloperUiRemainsReachableBeforeConnectionAndCanStartAndEndSolo()
        {
            var dev=view.hud.developerMode;
            yield return null;
            Assert.That(dev.toggleButton.gameObject.activeSelf,Is.True);
            dev.Open();Assert.That(dev.panel.activeSelf,Is.True);
            yield return null;
            Assert.That(view.design.gameObject.activeSelf,Is.False,"The painting must not cover the DEV form.");
            dev.applyStartButton.onClick.Invoke();yield return null;yield return null;
            Assert.That(network.IsDeveloperSolo,Is.True,network.Notice);
            Assert.That(view.hud.battleUi.activeSelf,Is.True);
            dev.EndSolo();yield return null;yield return null;
            Assert.That(dev.panel.activeSelf,Is.False);Assert.That(dev.toggleButton.gameObject.activeSelf,Is.True);
        }

        [UnityTest]public IEnumerator DirectIpControlsAreTouchableValidateInputAndKeepLegacyJoinCancelable()
        {
            view.EnterLobby();yield return null;
            yield return ClickPointer(view.directConnectionButton);
            Assert.That(view.directConnectionPanel.activeSelf,Is.True);
            AssertVisibleHit(view.directJoinButton);
            view.directAddress.text="";view.directPort.text="0";view.directJoinButton.onClick.Invoke();
            Assert.That(network.IsConnected||network.IsJoiningRoom,Is.False);
            Assert.That(view.directFeedback.text,Does.Contain("포트"));
            view.directAddress.text="127.0.0.1";view.directPort.text=network.port.ToString(System.Globalization.CultureInfo.InvariantCulture);
            view.directJoinButton.onClick.Invoke();
            Assert.That(network.IsJoiningRoom,Is.True,network.Notice);
            yield return null;
            Assert.That(view.directConnectionPanel.activeSelf,Is.False);
            Assert.That(view.cancelButton.gameObject.activeInHierarchy,Is.True);
            view.cancelButton.onClick.Invoke();
            double deadline=Time.realtimeSinceStartupAsDouble+5;
            while(!network.CanChooseConnection&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(network.CanChooseConnection,Is.True,network.Notice);
        }
        [UnityTest]public IEnumerator ThreePendingLabRoomsRetryConfirmedNameConflictsAndExposeSequentialNativeRoutes()
        {
            if(!LabBonjourDiscovery.IsSupported)Assert.Ignore("Bonjour is macOS/iOS only.");
            int firstNumber=100000+(int)(System.BitConverter.ToUInt32(System.Guid.NewGuid().ToByteArray(),0)%900001u);
            var registrations=new LabPendingRoom[3];
            var browser=new LabBonjourDiscovery();
            string[] expectedNames=Enumerable.Range(firstNumber,3).Select(number=>LabRoomNames.ServiceBase+" ("
                +number.ToString(System.Globalization.CultureInfo.InvariantCulture)+")").ToArray();
            string[] RegisteredKeys()=>registrations.Where(room=>room!=null).Select(room=>room.Key).OrderBy(key=>key).ToArray();
            LabDiscoveredRoom[] Found()=>LabRoomCatalog.GroupTargets(browser.Targets)
                .Where(room=>expectedNames.Contains(room.Name)).ToArray();
            string Details()=>"Requested first number: "+firstNumber+"; registered: "+string.Join(", ",registrations
                .Select(room=>room==null?"<not created>":(room.Name??"<none>")+"|"+room.Port+" ready="+room.Ready+" failed="+room.Failed))
                +"; discovered: "+string.Join(", ",Found().Select(room=>room.Name+"|"+room.Port));
            try
            {
                for(int index=0;index<registrations.Length;index++)
                {
                    Assert.That(LabPendingRoom.TryCreate(3,out registrations[index],out string error),Is.True,error);
                    Assert.That(registrations[index].Family,Is.EqualTo(LabRoomFamily.Lab));
                    double readyDeadline=Time.realtimeSinceStartupAsDouble+20;
                    // Every real pending room requests N. NoAutoRename must reject the occupied
                    // names and the product's Tick retry must reach N+1 / N+2, without fixture renaming.
                    while(!registrations[index].Ready&&Time.realtimeSinceStartupAsDouble<readyDeadline)
                    {
                        for(int existing=0;existing<=index;existing++)registrations[existing].Tick(true,firstNumber);
                        yield return null;
                    }
                    Assert.That(registrations[index].Ready,Is.True,Details());
                    Assert.That(registrations[index].Name,Is.EqualTo(expectedNames[index]),Details());
                }
                Assert.That(registrations.Select(room=>room.Port).Distinct().Count(),Is.EqualTo(3),Details());
                Assert.That(registrations.All(room=>room.Ready&&!room.Failed),Is.True,Details());
                Assert.That(browser.StartBrowse(),Is.True,browser.LastError);
                double deadline=Time.realtimeSinceStartupAsDouble+20;
                while(Time.realtimeSinceStartupAsDouble<deadline)
                {
                    foreach(var registration in registrations)registration.Tick(true,firstNumber);
                    browser.Tick();
                    var rooms=Found();
                    if(rooms.Length==3&&rooms.Select(room=>room.Key).OrderBy(key=>key).SequenceEqual(RegisteredKeys())
                        &&rooms.All(room=>LabRoomCatalog.RoutesForRoom(browser.Targets,room.Key).Length>0))break;
                    yield return null;
                }
                var found=Found();
                Assert.That(found.Length,Is.EqualTo(3),Details());
                Assert.That(found.Select(room=>room.Key).OrderBy(key=>key),Is.EqualTo(RegisteredKeys()),Details());
                Assert.That(found.Select(room=>{LabRoomNames.TryNumber(room.Name,out int number,LabRoomNames.ServiceBase);return number;})
                    .OrderBy(number=>number),Is.EqualTo(Enumerable.Range(firstNumber,3)),Details());
                Assert.That(found.All(room=>room.WaitingForHost&&!room.Playing&&room.Participants==1&&room.TargetParticipants==3),Is.True,Details());
                foreach(var room in found)
                {
                    var registration=registrations.Single(pending=>pending.Name==room.Name);
                    Assert.That(room.Port,Is.EqualTo(registration.Port),Details());
                    var routes=LabRoomCatalog.RoutesForRoom(browser.Targets,room.Key);
                    Assert.That(routes,Is.Not.Empty,Details());
                    Assert.That(routes.All(route=>route.ServiceName==registration.Name&&route.Port==registration.Port
                        &&route.WaitingForHost&&!route.Playing&&route.Participants==1&&route.TargetParticipants==3
                        &&System.Net.IPAddress.TryParse(route.Address,out _)),Is.True,Details());
                }
            }
            finally{foreach(var registration in registrations)registration?.Dispose();browser.Dispose();}
        }
        private void Set(string name,object value)=>typeof(LabNetwork).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(network,value);
        private T Field<T>(string name)=>(T)typeof(LabNetwork).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(network);
        private UnityEngine.UI.Button[] VisibleRows()=>view.roomContent.GetComponentsInChildren<UnityEngine.UI.Button>().Where(button=>button!=view.roomTemplate&&button.name=="CreatedRoom").ToArray();
        private static PointerEventData AssertVisibleHit(UnityEngine.UI.Button button)
        {
            Canvas.ForceUpdateCanvases();
            var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.position)};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
            Assert.That(hits.Count,Is.GreaterThan(0),button.name+" must receive screen input.");
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),Is.SameAs(button.gameObject),button.name+" must be the top input target.");
            return data;
        }
        private IEnumerator ClickPointer(UnityEngine.UI.Button button)
        {
            var data=AssertVisibleHit(button);
            if(pointer==null)
            {
                // Use an isolated runtime copy: editor focus and a human mouse must not eat queued test events.
                originalInputSettings=InputSystem.settings;originalInputFlags=originalInputSettings.hideFlags;
                // InputManager deletes HideAndDontSave settings on replacement; retain this original for restoration.
                originalInputSettings.hideFlags=HideFlags.DontUnloadUnusedAsset;
                fixtureInputSettings=Object.Instantiate(originalInputSettings);
                fixtureInputSettings.hideFlags=HideFlags.HideAndDontSave;
                fixtureInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                fixtureInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings=fixtureInputSettings;
                foreach(var device in InputSystem.devices.Where(device=>device is Mouse&&device.enabled).ToArray())
                {disabledMice.Add(device);InputSystem.DisableDevice(device);}
                pointer=InputSystem.AddDevice<Mouse>();
            }
            InputSystem.QueueStateEvent(pointer,new MouseState{position=data.position});yield return null;
            InputSystem.QueueStateEvent(pointer,new MouseState{position=data.position}.WithButton(MouseButton.Left));yield return null;
            InputSystem.QueueStateEvent(pointer,new MouseState{position=data.position});yield return null;
            yield return null; // The input module invokes onClick before the view's LateUpdate.
        }
    }
}
