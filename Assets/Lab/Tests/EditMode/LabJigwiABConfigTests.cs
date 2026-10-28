using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
namespace C6Lab.Tests
{
    public sealed class LabJigwiABConfigTests
    {
        LabConfig config;
        [Serializable] sealed class Values { public float[] values; }
        [SetUp] public void Create()=>config=ScriptableObject.CreateInstance<LabConfig>();
        [TearDown] public void Dispose()=>Object.DestroyImmediate(config);
        [Test] public void ElevenScreenSettingsAppendAndOldSurfaceValuesRemainReserved()
        {
            var v=config.CaptureDeveloperValues();Assert.That(v.Length,Is.EqualTo(251));
            Assert.That(v.Skip(240),Is.EqualTo(new[]{10f,4f,3f,5f,3f,12f,12f,.65f,.03f,4f,.3f}));
            Assert.That(v.Skip(228).Take(8),Is.EqualTo(new[]{1f,1f,7f,18f,30f,1f,3f,2f}));
            var old=new[]{230,231,232,233,234};
            foreach(int i in old)Assert.That(LabConfig.RetiredDeveloperFields,Does.Contain(i));
            var category=LabConfig.DeveloperCategories.Single(x=>x.Title=="지귀 120초 (Hapio 전용)");
            Assert.That(category.FieldIndices,Is.EqualTo(new[]{228,229,235}.Concat(Enumerable.Range(240,11))));
            Assert.That(v[55],Is.EqualTo(3));Assert.That(v[211],Is.EqualTo(120));
        }
        [Test] public void JsonCopyRoundTripsNewValuesWithoutChangingTheRetainedPrefix()
        {
            var before=config.CaptureDeveloperValues();var requested=(float[])before.Clone();
            float[] tail={15,3,5,4,6,20,8,.8f,.05f,8,.5f};Array.Copy(tail,0,requested,240,11);
            Assert.That(config.TryApplyDeveloperValues(requested,out var error),Is.True,error);
            var json=JsonUtility.ToJson(new Values{values=config.CaptureDeveloperValues()});
            var peer=ScriptableObject.CreateInstance<LabConfig>();try
            {
                Assert.That(peer.TryApplyDeveloperValues(JsonUtility.FromJson<Values>(json).values,out error),Is.True,error);
                Assert.That(peer.CaptureDeveloperValues(),Is.EqualTo(requested));
                Assert.That(peer.CaptureDeveloperValues().Take(240),Is.EqualTo(before.Take(240)));
            }finally{Object.DestroyImmediate(peer);}
        }
        [TestCase(240,0f)] [TestCase(241,float.NaN)] [TestCase(242,1.5f)] [TestCase(243,.4f)]
        [TestCase(244,7f)] [TestCase(245,0f)] [TestCase(246,-1f)] [TestCase(247,.1f)]
        [TestCase(248,.2f)] [TestCase(249,0f)] [TestCase(250,float.PositiveInfinity)]
        public void InvalidScreenValueRejectsTheWholeFormAtomically(int index,float value)
        {
            var before=config.CaptureDeveloperValues();var candidate=(float[])before.Clone();candidate[0]=777;candidate[index]=value;
            Assert.That(config.TryApplyDeveloperValues(candidate,out _),Is.False);Assert.That(config.CaptureDeveloperValues(),Is.EqualTo(before));
        }
        [Test] public void Older240SettingPayloadCannotSilentlyUseLocalScreenDefaults()
        {
            var before=config.CaptureDeveloperValues();var old=before.Take(240).ToArray();old[0]=777;
            Assert.That(config.TryApplyDeveloperValues(old,out _),Is.False);Assert.That(config.CaptureDeveloperValues(),Is.EqualTo(before));
            Assert.Throws<ArgumentException>(()=>LabSessionSettings.Fingerprint(old,2,5677));
        }
    }
}
