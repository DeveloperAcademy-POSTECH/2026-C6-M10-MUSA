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
        [Test] public void ScreenRewardsAppendWithoutShiftingEarlierFieldsAndRetiredValuesRemainReserved()
        {
            var v=config.CaptureDeveloperValues();Assert.That(v.Length,Is.EqualTo(253));
            Assert.That(v.Skip(240),Is.EqualTo(new[]{15f,4f,3f,5f,3f,12f,12f,.65f,.03f,4f,.3f,1.3f,1.5f}));
            Assert.That(v.Skip(228).Take(8),Is.EqualTo(new[]{1f,1f,7f,18f,30f,1f,3f,2f}));
            var old=new[]{229,230,231,232,233,234,246,247};
            foreach(int i in old)Assert.That(LabConfig.RetiredDeveloperFields,Does.Contain(i));
            var category=LabConfig.DeveloperCategories.Single(x=>x.Title=="지귀 120초 (Hapio 전용)");
            Assert.That(category.FieldIndices,Is.EqualTo(new[]{228,235}.Concat(Enumerable.Range(240,13)).Except(new[]{246,247})));
            Assert.That(v[55],Is.EqualTo(3));Assert.That(v[211],Is.EqualTo(120));
            Assert.That(LabConfig.DeveloperFields[240].Max, Is.EqualTo(15f));
        }
        [Test] public void JsonCopyRoundTripsNewValuesWithoutChangingTheRetainedPrefix()
        {
            var before=config.CaptureDeveloperValues();var requested=(float[])before.Clone();
            float[] tail={15,3,5,4,6,20,8,.8f,.05f,8,.5f,2.3f,2.6f};Array.Copy(tail,0,requested,240,13);
            Assert.That(config.TryApplyDeveloperValues(requested,out var error),Is.True,error);
            var json=JsonUtility.ToJson(new Values{values=config.CaptureDeveloperValues()});
            var peer=ScriptableObject.CreateInstance<LabConfig>();try
            {
                Assert.That(peer.TryApplyDeveloperValues(JsonUtility.FromJson<Values>(json).values,out error),Is.True,error);
                Assert.That(peer.CaptureDeveloperValues(),Is.EqualTo(requested));
                Assert.That(peer.CaptureDeveloperValues().Take(240),Is.EqualTo(before.Take(240)));
            }finally{Object.DestroyImmediate(peer);}
        }
        [TestCase(251)]
        [TestCase(252)]
        public void RewardScaleEndpointsRoundTripAndChangeOnlyTheirOwnSharedSlot(int index)
        {
            float[] baseline = config.CaptureDeveloperValues();
            LabDeveloperField field = LabConfig.DeveloperFields[index];
            Assert.That(new[] { field.Min, field.Max }, Is.EqualTo(new[] { 1f, 3f }));
            Assert.That(field.WholeNumber || field.IsToggle || field.IsChoice, Is.False);
            string fingerprint = LabSessionSettings.Fingerprint(baseline, 3, 5678);
            foreach (float value in new[] { 1f, 2.25f, 3f })
            {
                var requested = (float[])baseline.Clone(); requested[index] = value;
                Assert.That(config.TryApplyDeveloperValues(requested, out string error), Is.True, error);
                Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(requested));
                Assert.That(config.CaptureDeveloperValues().Take(251), Is.EqualTo(baseline.Take(251)));
                Assert.That(index == 251 ? config.JigwiABRewardSizeScale : config.JigwiABRewardPaintScale,
                    Is.EqualTo(value));
                Assert.That(LabSessionSettings.Fingerprint(requested, 3, 5678), Is.Not.EqualTo(fingerprint));
                float position = LabDeveloperSliderScale.Normalize(field, baseline[index], value);
                Assert.That(LabDeveloperSliderScale.Denormalize(field, baseline[index], position),
                    Is.EqualTo(value).Within(.0001f));
            }
        }

        [Test]
        public void RetiredWarningAndShrinkSlotsRemainStoredButNoCategoryCanDisplayThem()
        {
            float[] values = config.CaptureDeveloperValues();
            Assert.That(values[229], Is.EqualTo(1f));
            Assert.That(values[246], Is.EqualTo(12f));
            Assert.That(values[247], Is.EqualTo(.65f));
            foreach (int index in new[] { 229, 246, 247 })
            {
                Assert.That(LabConfig.DeveloperFields[index].Label, Does.StartWith("Reserved "));
                Assert.That(LabConfig.DeveloperCategories.SelectMany(category => category.FieldIndices), Has.No.Member(index));
            }
            values[229] = 2.5f; values[246] = 8f; values[247] = .8f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(config.JigwiStageWarningSeconds, Is.EqualTo(2.5f));
            Assert.That(config.JigwiABShrinkDelaySeconds, Is.EqualTo(8f));
            Assert.That(config.JigwiABShrinkScale, Is.EqualTo(.8f));
        }

        [Test]
        public void InspectorRewardScalesClampAndNonfiniteValuesUseTheirOwnDefaults()
        {
            JsonUtility.FromJsonOverwrite("{\"jigwiABRewardSizeScale\":99,\"jigwiABRewardPaintScale\":-1,\"jigwiABPersonalDurationSeconds\":99}", config);
            Assert.That(config.JigwiABRewardSizeScale, Is.EqualTo(3f));
            Assert.That(config.JigwiABRewardPaintScale, Is.EqualTo(1f));
            Assert.That(config.JigwiABPersonalDurationSeconds, Is.EqualTo(15f));
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(LabConfig).GetField("jigwiABRewardSizeScale", flags).SetValue(config, float.NaN);
            typeof(LabConfig).GetField("jigwiABRewardPaintScale", flags).SetValue(config, float.PositiveInfinity);
            typeof(LabConfig).GetField("jigwiABPersonalDurationSeconds", flags).SetValue(config, float.NaN);
            Assert.That(config.JigwiABRewardSizeScale, Is.EqualTo(1.3f));
            Assert.That(config.JigwiABRewardPaintScale, Is.EqualTo(1.5f));
            Assert.That(config.JigwiABPersonalDurationSeconds, Is.EqualTo(15f));
            Assert.That(config.CaptureDeveloperValues().Skip(251), Is.EqualTo(new[] { 1.3f, 1.5f }));
        }

        [TestCase(240,0f)] [TestCase(240,15.01f)] [TestCase(241,float.NaN)] [TestCase(242,1.5f)] [TestCase(243,.4f)]
        [TestCase(244,7f)] [TestCase(245,0f)] [TestCase(246,-1f)] [TestCase(247,.1f)]
        [TestCase(248,.2f)] [TestCase(249,0f)] [TestCase(250,float.PositiveInfinity)]
        [TestCase(251,.99f)] [TestCase(251,3.01f)] [TestCase(251,float.NaN)] [TestCase(251,float.PositiveInfinity)]
        [TestCase(252,.99f)] [TestCase(252,3.01f)] [TestCase(252,float.NaN)] [TestCase(252,float.PositiveInfinity)]
        public void InvalidScreenValueRejectsTheWholeFormAtomically(int index,float value)
        {
            var before=config.CaptureDeveloperValues();var candidate=(float[])before.Clone();candidate[0]=777;candidate[index]=value;
            Assert.That(config.TryApplyDeveloperValues(candidate,out _),Is.False);Assert.That(config.CaptureDeveloperValues(),Is.EqualTo(before));
        }
    }
}
