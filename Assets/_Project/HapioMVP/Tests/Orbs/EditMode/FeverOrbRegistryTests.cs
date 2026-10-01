using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Orbs.Tests
{
    public sealed class FeverOrbRegistryTests
    {
        private HostOrbRegistry registry;

        [SetUp]
        public void SetUp()
        {
            registry = new HostOrbRegistry(true);
            registry.BeginSession("fever-session", 1);
        }

        [Test]
        public void ConversionPreservesIdentityOwnerPositionAndTransferMotion()
        {
            var raw = registry.RegisterDevelopmentOrb(1, OrbKind.Raw, OrbPolarity.Yin,
                new Vector2(.9f, .4f));
            var request = new OrbActionRequest("fever-session", 1, "transfer", raw.OrbId, null,
                OrbActionKind.TransferRight, 1, raw.NormalizedPosition,
                transferMotion: new OrbTransferMotion(new Vector2(2f, .5f), 7d));
            var reservation = registry.Reserve(1, request).Reservation;
            Assert.That(registry.TryCompleteReservedTransfer(reservation, 2, 20, .05f,
                out var transferred, request.TransferMotion), Is.True);

            var converted = registry.ConvertIdleRawToFeverAttack();

            Assert.That(converted.Count, Is.EqualTo(1));
            Assert.That(converted[0].OrbId, Is.EqualTo(raw.OrbId));
            Assert.That(converted[0].OwnerPlayerId, Is.EqualTo(2));
            Assert.That(converted[0].NormalizedPosition, Is.EqualTo(transferred.NormalizedPosition));
            Assert.That(converted[0].TransferMotion, Is.EqualTo(transferred.TransferMotion));
            Assert.That(converted[0].Kind, Is.EqualTo(OrbKind.FeverAttack));
            Assert.That(converted[0].Polarity, Is.EqualTo(OrbPolarity.None));
        }

        [Test]
        public void FeverEndConsumesIdleButNeverAStartedProjectile()
        {
            var idle = registry.RegisterGeneratedFeverAttack("fever-session", 1, 1, new Vector2(.2f, .3f));
            var launched = registry.RegisterGeneratedFeverAttack("fever-session", 1, 1, new Vector2(.7f, .6f));
            var request = new OrbActionRequest("fever-session", 1, "launch", launched.OrbId, null,
                OrbActionKind.Launch, 1, launched.NormalizedPosition);
            var reservation = registry.Reserve(1, request).Reservation;
            Assert.That(registry.TryBeginReservedLaunch(reservation, out _), Is.True);
            Assert.That(registry.TryAdvanceLaunch("fever-session", 1, launched.OrbId,
                OrbAuthorityState.Launching, OrbAuthorityState.Projectile, out _), Is.True);

            var removed = registry.ConsumeIdleFeverAttack();

            Assert.That(removed.Count, Is.EqualTo(1));
            Assert.That(removed[0].OrbId, Is.EqualTo(idle.OrbId));
            Assert.That(registry.TryGet(launched.OrbId, out var projectile), Is.True);
            Assert.That(projectile.AuthorityState, Is.EqualTo(OrbAuthorityState.Projectile));
            Assert.That(registry.TryAdvanceLaunch("fever-session", 1, launched.OrbId,
                OrbAuthorityState.Projectile, OrbAuthorityState.Consumed, out _), Is.True);
        }

        [Test]
        public void FeverAttackCanLaunchButCannotCombine()
        {
            var fever = registry.RegisterGeneratedFeverAttack("fever-session", 1, 1, Vector2.one * .5f);
            var launch = registry.Reserve(1, new OrbActionRequest("fever-session", 1, "launch",
                fever.OrbId, null, OrbActionKind.Launch, 1, fever.NormalizedPosition));
            Assert.That(launch.Accepted, Is.True);

            registry = new HostOrbRegistry(true);
            registry.BeginSession("fever-session", 1);
            fever = registry.RegisterGeneratedFeverAttack("fever-session", 1, 1, Vector2.one * .5f);
            var raw = registry.RegisterDevelopmentOrb(1, OrbKind.Raw, OrbPolarity.Yang,
                new Vector2(.3f, .3f));
            var combine = registry.Reserve(1, new OrbActionRequest("fever-session", 1, "combine",
                fever.OrbId, raw.OrbId, OrbActionKind.Combine, 1, fever.NormalizedPosition));
            Assert.That(combine.Accepted, Is.False);
        }

        [Test]
        public void RawViewConvertsInPlaceWithoutReplacingItsBodyOrVelocity()
        {
            var gameObject = new GameObject("fever-view");
            try
            {
                var body = gameObject.AddComponent<Rigidbody2D>();
                var view = gameObject.AddComponent<OrbView>();
                view.Configure("same-id", OrbKind.Raw, OrbPolarity.Yin, 0, .3f);
                body.linearVelocity = new Vector2(1.25f, -.5f);

                Assert.That(view.ConvertRawToFeverAttack(), Is.True);
                Assert.That(view.Kind, Is.EqualTo(OrbKind.FeverAttack));
                Assert.That(gameObject.GetComponent<Rigidbody2D>(), Is.SameAs(body));
                Assert.That(body.linearVelocity, Is.EqualTo(new Vector2(1.25f, -.5f)));
                Assert.That(view.transform.Find("SecondCore"), Is.Not.Null);
                Assert.That(view.GetComponentInChildren<TextMesh>(true).text, Is.EqualTo("FEVER"));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                OrbView.SetArtwork(null);
            }
        }
    }
}
