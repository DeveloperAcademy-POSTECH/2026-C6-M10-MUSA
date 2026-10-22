using System.Linq;
using NUnit.Framework;
namespace C6Lab.Tests
{
    public sealed class LabRoomNamesTests
    {
        [TestCase("C6Lab-Room",1)] [TestCase("C6Lab-Room (2)",2)] [TestCase("C6Lab-Room (10)",10)]
        [TestCase("Hapio-Room",1)] [TestCase("Hapio-Room (2)",2)] [TestCase("Hapio-Room (10)",10)]
        public void ConfirmedBonjourNamesHaveTheSameNumberOnEveryScreen(string name,int number)
        {
            Assert.That(LabRoomNames.TryNumber(name,out int actual),Is.True);
            Assert.That(actual,Is.EqualTo(number));Assert.That(LabLobbyView.DisplayRoomName(name),Is.EqualTo("수장고-"+number));
        }
        [TestCase("C6Lab-Room (0)")] [TestCase("C6Lab-Room (-2)")] [TestCase("C6Lab-Room (1)")]
        [TestCase("C6Lab-Room (2) trailing")] [TestCase("C6Lab-Room (999999999999999999999)")]
        [TestCase("Hapio-Room (0)")] [TestCase("Hapio-Room (-2)")] [TestCase("Hapio-Room (1)")]
        [TestCase("Hapio-Room (2) trailing")]
        public void InvalidSuffixCannotAliasAnotherRoomsNumber(string name)
        {Assert.That(LabRoomNames.TryNumber(name,out _),Is.False);}
        [Test]
        public void CatalogUsesNumericRoomOrderAndKeepsSeparateJoinRoutes()
        {
            var rooms=LabRoomCatalog.GroupTargets(new[]{"C6Lab-Room (10)","C6Lab-Room (2)","C6Lab-Room"}
                .Select((n,i)=>new LabBonjourTarget("127.0.0.1",(ushort)(19001+i),1,n,1,3)));
            Assert.That(rooms.Select(r=>LabRoomNames.SortNumber(r.Name)),Is.EqualTo(new[]{1,2,10}));
            Assert.That(rooms.Select(r=>r.Key).Distinct().Count(),Is.EqualTo(3));
        }
    }
}
