using System;
using NUnit.Framework;
using UnityEngine;
namespace C6Lab.Tests
{
    public sealed class LabLobbyDesignTests
    {
        [TestCase(2,1,false)] [TestCase(3,2,false)] [TestCase(4,4,false)] [TestCase(5,5,true)]
        public void PresenceRoundTripsCapacityCountAndBattle(int capacity,int count,bool playing)
        {
            byte[] record=LabRoomPresence.Encode("test",true,true,count,capacity,playing);
            Assert.That(LabRoomPresence.TryDecode(record,"test",out bool v4,out bool v6,out int n,out int c,out bool b),Is.True);
            Assert.That(v4&&v6,Is.True);Assert.That(n,Is.EqualTo(count));Assert.That(c,Is.EqualTo(capacity));Assert.That(b,Is.EqualTo(playing));
            Assert.That(LabRoomPresence.TryDecode(record,"other",out _,out _,out _,out _,out _),Is.False);
        }
        [Test]
        public void InvalidPresenceCannotPublishImpossibleOrTruncatedRoomCounts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>LabRoomPresence.Encode("test",true,false,4,3,false));
            var record=LabRoomPresence.Encode("test",true,false,1,3,false);
            record[0]--;
            Assert.That(LabRoomPresence.TryDecode(record,"test",out _,out _,out _,out _,out _),Is.False);
            Assert.That(LabRoomPresence.TryDecode(null,"test",out _,out _,out _,out _,out _),Is.False);
        }
        [Test]
        public void PendingRoomMetadataReservesOnlyItsCreatorAndCannotPretendToBePlaying()
        {
            byte[] record=LabRoomPresence.Encode("test",true,true,1,5,false,true);
            Assert.That(LabRoomPresence.TryDecode(record,"test",out _,out _,out int count,out int capacity,out bool playing,out bool waiting),Is.True);
            Assert.That(waiting,Is.True);Assert.That(playing,Is.False);Assert.That(count,Is.EqualTo(1));Assert.That(capacity,Is.EqualTo(5));
            Assert.Throws<ArgumentOutOfRangeException>(()=>LabRoomPresence.Encode("test",true,true,2,5,false,true));
            Assert.Throws<ArgumentOutOfRangeException>(()=>LabRoomPresence.Encode("test",true,true,1,5,true,true));
        }
        [Test]
        public void DiscoveryPreservesTheCreatorWaitingState()
        {
            var rooms=LabRoomCatalog.GroupTargets(new[]{new LabBonjourTarget("127.0.0.1",7777,1,"C6Lab-Room",1,3,false,true)});
            Assert.That(rooms.Count,Is.EqualTo(1));Assert.That(rooms[0].WaitingForHost,Is.True);Assert.That(rooms[0].IsFull,Is.False);
        }
        [Test]
        public void RoomRoutesShareLivePresenceAndFullRoomsAreIdentified()
        {
            var rooms=LabRoomCatalog.GroupTargets(new[]{new LabBonjourTarget("127.0.0.1",7777,1,"C6Lab-24",3,3,false),new LabBonjourTarget("::1",7777,1,"C6Lab-24",3,3,false)});
            Assert.That(rooms.Count,Is.EqualTo(1));Assert.That(rooms[0].IsFull,Is.True);
            Assert.That(rooms[0].Participants,Is.EqualTo(3));Assert.That(rooms[0].TargetParticipants,Is.EqualTo(3));
        }
        [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void EveryRoomHasDistinctTouchableSlotsAndHostAtTheBottom(int count)
        {
            for(int slot=1;slot<=count;slot++)
            {
                var p=LabLobbyView.SlotCenter(count,slot);
                Assert.That(p.x,Is.InRange(62,340));Assert.That(p.y,Is.InRange(365,610));
                for(int other=slot+1;other<=count;other++)Assert.That(Vector2.Distance(p,LabLobbyView.SlotCenter(count,other)),Is.GreaterThan(80));
            }
            Assert.That(LabLobbyView.SlotCenter(count,1),Is.EqualTo(new Vector2(201,574)));
        }
        [Test]
        public void BackgroundUsesOriginalPaintingCropAndRoomDisplayPreservesIdentity()
        {
            Rect uv=LabLobbyView.BackgroundUv(true,new Vector2(402,874));
            Assert.That(uv.x,Is.EqualTo(717f/1347f).Within(.0001));Assert.That(uv.width,Is.EqualTo(402f/1347f).Within(.0001));
            Assert.That(LabLobbyView.DisplayRoomName("C6Lab-24 (2)"),Is.EqualTo("수장고-24 (2)"));
        }
    }
}
