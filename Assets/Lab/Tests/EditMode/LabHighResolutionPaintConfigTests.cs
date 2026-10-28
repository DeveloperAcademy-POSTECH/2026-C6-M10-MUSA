using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>Presentation quality travels with the room without changing existing game tuning.</summary>
    public sealed class LabHighResolutionPaintConfigTests
    {
        private LabConfig config;
        [SetUp] public void Create() => config = ScriptableObject.CreateInstance<LabConfig>();
        [TearDown] public void Dispose() => Object.DestroyImmediate(config);

        [Test]
        public void NewAndSavedConfigsDefaultToEnabled1024By512()
        {
            foreach (LabConfig source in new[] { config, Resources.Load<LabConfig>("LabConfig") })
            {
                Assert.That(source, Is.Not.Null);
                Assert.That(source.HighResolutionPaintEnabled, Is.True);
                Assert.That(source.HighResolutionPaintQuality, Is.Zero);
                Assert.That(source.HighResolutionPaintColumns, Is.EqualTo(1024));
                Assert.That(source.CaptureDeveloperValues().Skip(200).Take(2), Is.EqualTo(new[] { 1f, 0f }));
            }
            Assert.That(LabConfig.DeveloperFields[200].IsToggle, Is.True);
            Assert.That(LabConfig.DeveloperFields[201].Choices,
                Is.EqualTo(new[] { "1024 × 512", "2048 × 1024" }));
            var gourdFields = LabConfig.DeveloperCategories.Single(category => category.Title == "Gourd").FieldIndices;
            CollectionAssert.Contains(gourdFields, 200);
            CollectionAssert.Contains(gourdFields, 201);
        }

        [Test]
        public void RoomRoundTripChangesOnlyTheAppendedPresentationFields()
        {
            float[] before = config.CaptureDeveloperValues();
            float[] values = (float[])before.Clone();
            values[200] = 0f; values[201] = 1f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(config.HighResolutionPaintEnabled, Is.False);
            Assert.That(config.HighResolutionPaintColumns, Is.EqualTo(2048));
            CollectionAssert.AreEqual(before.Take(200), config.CaptureDeveloperValues().Take(200));
            var peer = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                Assert.That(peer.TryApplyDeveloperValues(config.CaptureDeveloperValues(), out error), Is.True, error);
                Assert.That(peer.HighResolutionPaintEnabled, Is.False);
                Assert.That(peer.HighResolutionPaintColumns, Is.EqualTo(2048));
                string applied = LabSessionSettings.Fingerprint(values, 4, 5677);
                Assert.That(applied, Is.EqualTo(LabSessionSettings.Fingerprint(peer.CaptureDeveloperValues(), 4, 5677)));
                Assert.That(applied, Is.Not.EqualTo(LabSessionSettings.Fingerprint(before, 4, 5677)));
            }
            finally { Object.DestroyImmediate(peer); }
        }

        [TestCase(200, -.1f)]
        [TestCase(200, .5f)]
        [TestCase(200, 1.1f)]
        [TestCase(201, -1f)]
        [TestCase(201, .5f)]
        [TestCase(201, 2f)]
        [TestCase(201, float.NaN)]
        [TestCase(201, float.PositiveInfinity)]
        public void InvalidPresentationSettingsCannotPartiallyApplyTheRoom(int index, float invalid)
        {
            float[] before = config.CaptureDeveloperValues();
            float[] values = (float[])before.Clone();
            values[0] = 777f; values[index] = invalid;
            Assert.That(config.TryApplyDeveloperValues(values, out _), Is.False);
            CollectionAssert.AreEqual(before, config.CaptureDeveloperValues());
        }

        [Test]
        public void OlderRoomPayloadIsRejectedAndInspectorQualityIsSafelyClamped()
        {
            float[] before = config.CaptureDeveloperValues();
            float[] old = before.Take(200).ToArray(); old[0] = 777f;
            Assert.That(config.TryApplyDeveloperValues(old, out _), Is.False);
            CollectionAssert.AreEqual(before, config.CaptureDeveloperValues());
            JsonUtility.FromJsonOverwrite("{\"highResolutionPaintQuality\":99}", config);
            Assert.That(config.HighResolutionPaintColumns, Is.EqualTo(2048));
            JsonUtility.FromJsonOverwrite("{\"highResolutionPaintQuality\":-1}", config);
            Assert.That(config.HighResolutionPaintColumns, Is.EqualTo(1024));
        }
    }
}
