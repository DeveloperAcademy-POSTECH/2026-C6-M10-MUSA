using System;
using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    /// <summary>Owner-scoped invisible B collision planes; visible flames belong to the Canvas.</summary>
    public sealed partial class LabNetwork
    {
        private LabSnapshot jigwiABClockSnapshot;
        private double jigwiABReceivedAt;
        private readonly HashSet<string> jigwiABKeepSurfaces = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> jigwiABRemoveSurfaces = new List<string>();
        private readonly Dictionary<ulong, JigwiABReachableBand> jigwiABReachableBands = new Dictionary<ulong, JigwiABReachableBand>();
        private string jigwiABReachableRound;
        private sealed class JigwiABReachableBand
        {
            public LabConfig Config;
            public int Profile, Seat, Count;
            public Vector3 Eye, Normal, Position;
            public float PlaneDepth;
        }


        /// <summary>One held battle-clock sample for the Canvas and local/Host fire collision motion.</summary>
        public double JigwiABPresentationTime
        {
            get
            {
                if (latest == null) return 0d;
                if (IsHost) return battleClock.Now(Time.realtimeSinceStartupAsDouble);
                if (!ReferenceEquals(jigwiABClockSnapshot, latest))
                {
                    jigwiABClockSnapshot = latest;
                    jigwiABReceivedAt = Time.realtimeSinceStartupAsDouble;
                }
                return latest.serverTime + (latest.paused || GameStartHeld ? 0d
                    : Math.Max(0d, Time.realtimeSinceStartupAsDouble - jigwiABReceivedAt));
            }
        }

        private void SyncJigwiRewardBoardPresentation()
        {
            if (board == null) return;
            bool active = latest != null && latest.phase == LabPhase.Playing
                && latest.jigwiAB != null && latest.jigwiAB.enabled
                && latest.jigwiAB.Reward(JigwiABPresentationTime) != null;
            board.SetJigwiRewardFire(active, latest != null && (latest.paused || GameStartHeld));
        }

        /// <summary>Shared owner-facing point plane, in front of its full curtain and independent of local screen aspect.</summary>
        public bool TryGetJigwiABFireWorldPosition(LabJigwiABRound round, LabJigwiABFireTarget fire,
            ulong owner, int index, double now, out Vector3 position)
        {
            position = Vector3.zero;
            if (round == null || fire == null || double.IsNaN(now) || double.IsInfinity(now)
                || !TryJigwiABPlane(owner, out Vector3 eye, out Vector3 forward, out Vector3 right,
                    out Vector3 up, out float launchDepth, out float bossFront)) return false;
            float depth = Mathf.Lerp(launchDepth, bossFront, 1f / 3f);
            if (!TryJigwiABReachableBand(round, owner, eye, forward, depth, out JigwiABReachableBand band)) return false;
            Vector2 point = LabJigwiABFireMotion.Position(round, fire, index, now);
            int count = JigwiABFireCount(round, owner);
            float releaseX = Mathf.Clamp(JigwiABBaseReleaseX(index, count) + (point.x - fire.x) * .35f, .08f, .92f);
            float verticalMotion = Mathf.Clamp((point.y - fire.y) * .75f, -.05f, .05f);
            position = band.Position + right * ((releaseX - .5f) * 2f * config.ThrowOriginLateralRange)
                + up * verticalMotion;
            return Finite(position.x) && Finite(position.y) && Finite(position.z);
        }

        /// <summary>
        /// The Canvas projects the exact BoxCollider frame. Cover the current visible shell and
        /// every ignition's full motion/radius range, rather than its maximum future rope/swing path.
        /// Partial clears never shrink a hole because unlit ignition points stay in this union.
        /// </summary>
        public bool TryGetJigwiABCurtainFrame(LabJigwiABRound round, ulong owner, double now,
            out Vector3 center, out Quaternion rotation, out Vector2 halfSize)
        {
            center = Vector3.zero; rotation = Quaternion.identity; halfSize = Vector2.zero;
            if (round == null || double.IsNaN(now) || double.IsInfinity(now)
                || !TryJigwiABPlane(owner, out Vector3 eye, out Vector3 forward, out Vector3 right,
                    out Vector3 up, out float launchDepth, out float bossFront)) return false;
            float depth = Mathf.Lerp(launchDepth, bossFront, 2f / 3f);
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            Mesh mesh = target.Hitbox.sharedMesh;
            Bounds shell = mesh != null ? mesh.bounds : new Bounds(Vector3.zero, Vector3.one * 1.6f);
            for (int i = 0; i < 8; i++)
            {
                Vector3 local = shell.center + Vector3.Scale(shell.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                ExpandJigwiABCurtainProjection(target.Hitbox.transform.TransformPoint(local),
                    eye, forward, right, up, depth, ref minimum, ref maximum);
            }
            float pointDepth = Mathf.Lerp(launchDepth, bossFront, 1f / 3f);
            if (!TryJigwiABReachableBand(round, owner, eye, forward, pointDepth, out JigwiABReachableBand band)) return false;
            foreach (LabJigwiABPlayerFire player in round.players ?? Array.Empty<LabJigwiABPlayerFire>())
            {
                if (player?.targets == null || player.playerId != owner) continue;
                for (int index = 0; index < player.targets.Length; index++)
                {
                    if (player.targets[index] == null) continue;
                    float releaseX = JigwiABBaseReleaseX(index, player.targets.Length);
                    Vector3 pointCenter = band.Position + right * ((releaseX - .5f) * 2f * config.ThrowOriginLateralRange);
                    float horizontalReach = round.targetRadius + round.moveAmount * .35f * 2f * config.ThrowOriginLateralRange;
                    float verticalReach = round.targetRadius + Mathf.Min(.05f, round.moveAmount * .55f * .75f);
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 corner = pointCenter + right * (((i & 1) == 0 ? -1f : 1f) * horizontalReach)
                            + up * (((i & 2) == 0 ? -1f : 1f) * verticalReach)
                            + forward * (((i & 4) == 0 ? -1f : 1f) * round.targetRadius);
                        ExpandJigwiABCurtainProjection(corner, eye, forward, right, up, depth, ref minimum, ref maximum);
                    }
                }
                break;
            }
            float padding = Mathf.Max(.06f, config.ThrowRadius * .35f);
            Vector2 midpoint = (minimum + maximum) * .5f;
            halfSize = (maximum - minimum) * .5f + Vector2.one * padding;
            center = eye + forward * depth + right * midpoint.x + up * midpoint.y;
            rotation = Quaternion.LookRotation(forward, up);
            return Finite(halfSize.x) && Finite(halfSize.y) && halfSize.x > 0f && halfSize.y > 0f;
        }

        private static float JigwiABBaseReleaseX(int index, int count) => count <= 1 ? .5f
            : Mathf.Lerp(.15f, .85f, Mathf.Clamp01(index / (float)(count - 1)));

        private static int JigwiABFireCount(LabJigwiABRound round, ulong owner)
        {
            foreach (LabJigwiABPlayerFire player in round.players ?? Array.Empty<LabJigwiABPlayerFire>())
                if (player != null && player.playerId == owner) return player.targets?.Length ?? 1;
            return 1;
        }

        /// <summary>
        /// The target row samples an ordinary legal medium flick, not the camera's elevated aim line.
        /// Cache the first crossing of the point plane per event/seat/profile; animation only adds a
        /// small bounded offset afterwards. This never changes a real projectile's input or trajectory.
        /// </summary>
        private bool TryJigwiABReachableBand(LabJigwiABRound round, ulong owner, Vector3 eye, Vector3 normal,
            float depth, out JigwiABReachableBand band)
        {
            if (jigwiABReachableRound != round.id)
            { jigwiABReachableRound = round.id; jigwiABReachableBands.Clear(); }
            LabPlayerState player = null;
            foreach (LabPlayerState candidate in latest.players)
                if (candidate != null && candidate.id == owner) { player = candidate; break; }
            band = null;
            if (player == null) return false;
            int profile = JigwiABThrowProfile(config);
            if (jigwiABReachableBands.TryGetValue(owner, out band) && band.Config == config
                && band.Profile == profile && band.Seat == player.seat && band.Count == latest.players.Length
                && band.Eye == eye && band.Normal == normal && band.PlaneDepth == depth) return true;
            Vector3 center = target.transform.position;
            Vector3 origin = LabThrowMath.LaunchPosition(player.seat, latest.players.Length, center, .5f, config);
            Vector3 anchor = LabThrowMath.SeatPosition(player.seat, latest.players.Length, center,
                config.ThrowOriginRadius, config.ThrowOriginHeightOffset, true);
            float minimum = Mathf.Min(config.ThrowMaxSwipeSpeed,
                Mathf.Max(config.ThrowSwipeMinSpeed, config.ThrowSwipeMinDistance / .12f * 1.1f));
            float speed = Mathf.Lerp(minimum, config.ThrowMaxSwipeSpeed, .35f);
            Vector2 gesture = new Vector2(0f, speed);
            if (!LabThrowMath.IsThrowGesture(gesture, Mathf.Max(.24f, config.ThrowSwipeMinDistance), config)) return false;
            Vector3 velocity = LabThrowMath.LaunchVelocity(gesture, anchor, center, config);
            float needed = depth - Vector3.Dot(origin - eye, normal);
            float time = .05f, previous = 0f;
            bool found = false;
            float lifetime = Mathf.Max(.05f, config.ThrowLifetime);
            for (int i = 1; i <= 64; i++)
            {
                float sample = lifetime * i / 64f;
                if (Vector3.Dot(JigwiABFlightOffset(velocity, sample, config.EffectiveThrowGravity, config.ThrowAirDamping), normal) >= needed)
                {
                    float low = previous, high = sample;
                    for (int iteration = 0; iteration < 12; iteration++)
                    {
                        float middle = (low + high) * .5f;
                        if (Vector3.Dot(JigwiABFlightOffset(velocity, middle, config.EffectiveThrowGravity, config.ThrowAirDamping), normal) < needed)
                            low = middle;
                        else high = middle;
                    }
                    time = (low + high) * .5f; found = true; break;
                }
                previous = sample;
            }
            // Unplayable extreme DEV profiles still keep a nearby real legal-flight sample visible.
            if (!found) time = Mathf.Min(.05f, lifetime);
            if (band == null) band = new JigwiABReachableBand();
            band.Config = config; band.Profile = profile; band.Seat = player.seat; band.Count = latest.players.Length;
            band.Eye = eye; band.Normal = normal; band.PlaneDepth = depth;
            band.Position = origin + JigwiABFlightOffset(velocity, time, config.EffectiveThrowGravity, config.ThrowAirDamping);
            jigwiABReachableBands[owner] = band;
            return true;
        }

        private static Vector3 JigwiABFlightOffset(Vector3 velocity, float time, float gravity, float damping)
        {
            if (damping < .0001f) return velocity * time + Vector3.down * (.5f * gravity * time * time);
            float travel = (1f - Mathf.Exp(-damping * time)) / damping;
            float fall = (time - travel) / damping;
            return velocity * travel + Vector3.down * (gravity * fall);
        }

        private static int JigwiABThrowProfile(LabConfig value)
        {
            unchecked
            {
                int hash = (int)value.ThrowForm;
                hash = hash * 397 ^ new Vector4(value.ThrowOriginRadius, value.ThrowOriginHeightOffset,
                    value.ThrowOriginLateralRange, value.CameraLookHeightOffset).GetHashCode();
                hash = hash * 397 ^ new Vector4(value.ThrowSwipeMinSpeed, value.ThrowSwipeMinDistance,
                    value.ThrowMaxSwipeSpeed, value.ThrowSwipeUpDominance).GetHashCode();
                hash = hash * 397 ^ new Vector4(value.ThrowMinUpSpeed, value.ThrowLateralGain,
                    value.ThrowUpGain, value.ThrowForwardGain).GetHashCode();
                hash = hash * 397 ^ new Vector4(value.ThrowMaxWorldSpeed, value.ThrowGravity,
                    value.ThrowLifetime, value.ThrowPowerExponent).GetHashCode();
                hash = hash * 397 ^ new Vector4(value.ThrowLoftOffsetDegrees, value.ThrowAirDamping,
                    value.StraightLoftScale, value.StraightPitchOffsetDegrees).GetHashCode();
                Vector3 offset = value.StraightOriginOffset;
                return hash * 397 ^ new Vector4(value.StraightGravityScale, offset.x, offset.y, offset.z).GetHashCode();
            }
        }

        private static void ExpandJigwiABCurtainProjection(Vector3 worldPoint, Vector3 eye, Vector3 forward,
            Vector3 right, Vector3 up, float depth, ref Vector2 minimum, ref Vector2 maximum)
        {
            Vector3 relative = worldPoint - eye;
            // Extreme DEV geometry may enclose the eye; keep the API visible and let the
            // battle-viewport UI clip bound the art. The authoritative boss gate remains final.
            float z = Mathf.Max(.1f, Vector3.Dot(relative, forward));
            Vector2 projected = new Vector2(Vector3.Dot(relative, right), Vector3.Dot(relative, up)) * (depth / z);
            minimum = Vector2.Min(minimum, projected); maximum = Vector2.Max(maximum, projected);
        }

        private bool TryJigwiABPlane(ulong owner, out Vector3 eye, out Vector3 forward,
            out Vector3 right, out Vector3 up, out float launchDepth, out float bossFront)
        {
            eye = forward = right = up = Vector3.zero; launchDepth = bossFront = 0f;
            if (target == null || target.Hitbox == null || config == null || latest?.players == null
                || latest.players.Length < 1) return false;
            LabPlayerState player = null;
            for (int i = 0; i < latest.players.Length; i++)
                if (latest.players[i] != null && latest.players[i].id == owner) { player = latest.players[i]; break; }
            if (player == null || player.seat < 0 || player.seat >= latest.players.Length) return false;
            Vector3 origin = target.transform.position;
            eye = LabThrowMath.SeatPosition(player.seat, latest.players.Length, origin,
                config.CameraRadius, config.CameraHeightOffset, true);
            forward = (origin + Vector3.up * config.CameraLookHeightOffset - eye).normalized;
            right = Vector3.Cross(Vector3.up, forward).normalized;
            up = Vector3.Cross(forward, right).normalized;
            Vector3 launch = LabThrowMath.LaunchPosition(player.seat, latest.players.Length, origin, .5f, config);
            launchDepth = Mathf.Max(.1f, Vector3.Dot(launch - eye, forward));
            Vector3 scale = target.Hitbox.transform.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            Mesh mesh = target.Hitbox.sharedMesh;
            float radius = mesh != null ? Vector3.Scale(mesh.bounds.extents, scale).magnitude
                + Vector3.Scale(mesh.bounds.center, scale).magnitude : .8f;
            // Point depth stays stable under spin and rope movement. The wall rectangle follows
            // the current mesh projection, while Core still blocks real boss contacts that arc around it.
            float staticFront = Vector3.Dot(origin - eye, forward) - radius;
            bossFront = Mathf.Max(staticFront, launchDepth + Mathf.Max(.75f, config.ThrowRadius * 2f + .15f));
            return Finite(bossFront);
        }

        private void FixedUpdate()
        {
            if (IsHost && latest?.jigwiAB?.enabled == true) SyncJigwiFireSurfaces();
        }

        private void EnsureJigwiABFireRoot()
        {
            if (jigwiFireRoot != null) return;
            var child = new GameObject("Jigwi Fire Targets");
            child.transform.SetParent(transform, false); jigwiFireRoot = child.transform;
        }

        private void SyncJigwiABFireSurfaces()
        {
            double now = JigwiABPresentationTime;
            LabJigwiABRound round = latest.jigwiAB.Active(now);
            jigwiABKeepSurfaces.Clear();
            if (round != null && round.kind == LabJigwiABKind.GroupFire && round.Active(now))
                foreach (LabJigwiABPlayerFire player in round.players ?? Array.Empty<LabJigwiABPlayerFire>())
                {
                    if (player?.targets == null) continue;
                    if (TryGetJigwiABCurtainFrame(round, player.playerId, now, out Vector3 curtainCenter,
                        out Quaternion curtainRotation, out Vector2 curtainHalf))
                    {
                        string curtainId = LabJigwiABFireMotion.CurtainId(round.id, player.playerId);
                        jigwiABKeepSurfaces.Add(curtainId);
                        jigwiFireSurfaces.TryGetValue(curtainId, out LabJigwiFireSurface curtain);
                        if (curtain != null && (!curtain.IsCurtain || curtain.FireId != round.id || curtain.OwnerPlayerId != player.playerId))
                        { curtain.gameObject.SetActive(false); Destroy(curtain.gameObject); curtain = null; }
                        bool anyLit = false;
                        for (int i = 0; i < player.targets.Length; i++) anyLit |= player.targets[i] != null && player.targets[i].lit;
                        if (curtain == null)
                        {
                            EnsureJigwiABFireRoot();
                            curtain = LabJigwiFireSurface.CreateCurtain(jigwiFireRoot, player.playerId, round.id,
                                curtainId, curtainCenter, curtainRotation, curtainHalf, anyLit);
                            jigwiFireSurfaces[curtainId] = curtain;
                        }
                        else curtain.ConfigureCurtain(curtainCenter, curtainRotation, curtainHalf, anyLit);
                    }
                    for (int index = 0; index < player.targets.Length; index++)
                    {
                        LabJigwiABFireTarget fire = player.targets[index];
                        if (fire == null || !TryGetJigwiABFireWorldPosition(round, fire, player.playerId, index, now, out var point)) continue;
                        // The bounded DTO guarantees globally unique target IDs within this round.
                        string key = fire.id;
                        jigwiABKeepSurfaces.Add(key);
                        jigwiFireSurfaces.TryGetValue(key, out LabJigwiFireSurface surface);
                        if (surface != null && (surface.FireId != round.id || surface.OwnerPlayerId != player.playerId))
                        {
                            surface.gameObject.SetActive(false); Destroy(surface.gameObject); surface = null;
                        }
                        if (surface == null)
                        {
                            EnsureJigwiABFireRoot();
                            surface = LabJigwiFireSurface.Create(jigwiFireRoot, player.playerId, round.id,
                                fire.id, point, round.targetRadius, false);
                            jigwiFireSurfaces[key] = surface;
                        }
                        // Keep the same collider object across relights: already flying projectiles
                        // retain their owner-specific references, while unlit targets cannot collide.
                        surface.Configure(point, round.targetRadius, fire.lit, false);
                    }
                }
            jigwiABRemoveSurfaces.Clear();
            foreach (string key in jigwiFireSurfaces.Keys)
                if (!jigwiABKeepSurfaces.Contains(key)) jigwiABRemoveSurfaces.Add(key);
            foreach (string key in jigwiABRemoveSurfaces)
            {
                LabJigwiFireSurface surface = jigwiFireSurfaces[key];
                if (surface != null) { surface.gameObject.SetActive(false); Destroy(surface.gameObject); }
                jigwiFireSurfaces.Remove(key);
            }
        }
    }
}
