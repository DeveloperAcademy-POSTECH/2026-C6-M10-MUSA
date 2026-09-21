using System;
using C6.Prototype.Attack;
using C6.Prototype.Lobby;
using C6.Prototype.Presentation;
using UnityEngine;

namespace C6.Prototype.GameSync
{
    // A runtime copy of the single saved Config. Host values never write back to an asset.
    [DefaultExecutionOrder(-300), DisallowMultipleComponent]
    public sealed class GameRuntimeConfig : MonoBehaviour
    {
        public ScreenLayoutConfig Value { get; private set; }
        private void Awake()
        {
            var layout = GetComponent<SplitScreenLayout>();
            Value = Instantiate(layout.Config); Value.name = layout.Config.name + " (approved room runtime)";
            Value.hideFlags = HideFlags.DontSave;
            layout.Configure(Value, layout.BattleCamera, layout.OrbCamera);
            var frame = FindAnyObjectByType<AttackLaunchFrame>();
            if (frame != null) frame.Configure(Value);
        }
        public void Apply(LobbyHostConfig c)
        {
            if (Value == null || !LobbyHostConfig.TryRead(JsonUtility.ToJson(c), out _))
                throw new ArgumentException("Invalid approved Config.");
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new Fields(c)), Value);
            if (JsonUtility.ToJson(LobbyHostConfig.Capture(Value)) != JsonUtility.ToJson(c))
                throw new InvalidOperationException("Runtime Config does not match the approved Host values.");
        }
        private void OnDestroy() { if (Value != null) Destroy(Value); }
        [Serializable] private sealed class Fields
        {
            public float upperFraction,horizontalSwipeFraction,horizontalDominance,combinationRadiusFraction;
            public int monsterMaxHp,baseDamage,orbStorageLimit;
            public int monsterMaxHp2Players, monsterMaxHp3Players, monsterMaxHp4Players, monsterMaxHp5Players;
            public float projectileSpeed,projectileLifetime,projectileRadius,attackSnapshotRateHz,launchWidth;
            public Vector3 launchOrigin,launchAim;
            public float staminaMax,staminaStart,generateCost,staminaRecoveryAmount,staminaRecoverySeconds,staminaHitRecovery;
            public float battleDurationSeconds,teamHpDecayPerSecond;
            public float throwMinUpSpeed;
            public float throwMaxInputSpeed;
            public float throwForwardGain;
            public float throwUpGain;
            public float throwLateralGain;
            public float throwMaxWorldSpeed;
            public float throwGravity;
            public float throwBounce;
            public float throwLifetime;
            public float throwSampleWindow;
            public float throwMinDuration;
            public float battleFramingPaddingFraction;
            public float throwFloorY;
            public float throwFloorFriction;
            public Vector3 throwFloorSize;
            public Vector3 monsterPosition;
            public Vector3 monsterHitboxCenter;
            public Vector3 monsterHitboxSize;
            public string monsterTargetId;
            public bool resourceDebugToolsEnabled;
            public Fields(LobbyHostConfig c)
            {
                upperFraction=c.upperFraction; horizontalSwipeFraction=c.horizontalSwipe; horizontalDominance=c.horizontalDominance;
                combinationRadiusFraction=c.combinationRadius; monsterMaxHp=c.monsterHp; monsterMaxHp2Players=c.monsterHp2; monsterMaxHp3Players=c.monsterHp3; monsterMaxHp4Players=c.monsterHp4; monsterMaxHp5Players=c.monsterHp5; baseDamage=c.damage; orbStorageLimit=c.storageLimit;
                projectileSpeed=c.projectileSpeed; projectileLifetime=c.projectileLifetime; projectileRadius=c.projectileRadius;
                attackSnapshotRateHz=c.snapshotRate; launchWidth=c.launchWidth; launchOrigin=c.launchOrigin; launchAim=c.launchAim;
                staminaMax=(float)c.staminaMax; staminaStart=(float)c.staminaStart; generateCost=(float)c.generateCost;
                staminaRecoveryAmount=(float)c.recoveryAmount; staminaRecoverySeconds=(float)c.recoverySeconds;
                staminaHitRecovery=(float)c.hitRecovery; battleDurationSeconds=(float)c.duration; teamHpDecayPerSecond=(float)c.teamHpDecay;
                throwMinUpSpeed=c.throwMinUpSpeed;
                throwMaxInputSpeed=c.throwMaxInputSpeed;
                throwForwardGain=c.throwForwardGain;
                throwUpGain=c.throwUpGain;
                throwLateralGain=c.throwLateralGain;
                throwMaxWorldSpeed=c.throwMaxWorldSpeed;
                throwGravity=c.throwGravity;
                throwBounce=c.throwBounce;
                throwLifetime=c.throwLifetime;
                throwSampleWindow=c.throwSampleWindow;
                throwMinDuration=c.throwMinDuration;
                battleFramingPaddingFraction=c.battleFramingPaddingFraction;
                throwFloorY=c.throwFloorY;
                throwFloorFriction=c.throwFloorFriction;
                throwFloorSize=c.throwFloorSize;
                monsterPosition=c.monsterPosition;
                monsterHitboxCenter=c.monsterHitboxCenter;
                monsterHitboxSize=c.monsterHitboxSize;
                monsterTargetId=c.monsterTargetId;
                resourceDebugToolsEnabled=false;
            }
        }
    }
}
