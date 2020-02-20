using System;
using System.ComponentModel;
using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Platform.Weapon;
using static WeaponCore.Support.TargetingDefinition;

namespace WeaponCore
{


    [ProtoContract]
    public class CompStateValues
    {
        [ProtoMember(1)] public uint MId;
        [ProtoMember(2), DefaultValue(-1)] public float PowerLevel;
        [ProtoMember(3)] public bool Online;
        [ProtoMember(4)] public bool Overload;
        [ProtoMember(5)] public bool Message;
        [ProtoMember(6)] public int Heat;
        [ProtoMember(7)] public WeaponStateValues[] Weapons;
        [ProtoMember(8), DefaultValue(-1)] public long PlayerIdInTerminal = -1;
        [ProtoMember(9)] public bool ShootOn;
        [ProtoMember(10)] public bool ClickShoot;
        [ProtoMember(11)] public PlayerControl CurrentPlayerControl;
        [ProtoMember(12)] public float CurrentCharge;

    }

    [ProtoContract]
    public class CompSettingsValues
    {
        [ProtoMember(1)] public uint MId;
        [ProtoMember(2)] public bool Guidance = true;
        [ProtoMember(3)] public int Overload = 1;
        [ProtoMember(4)] public long Modes;
        [ProtoMember(5)] public float DpsModifier = 1;
        [ProtoMember(6)] public float RofModifier = 1;
        [ProtoMember(7)] public WeaponSettingsValues[] Weapons;
        [ProtoMember(8)] public float Range = 100;
        [ProtoMember(9)] internal MyObjectBuilder_Inventory Inventory = null;
        [ProtoMember(10)] public CompGroupOverrides Overrides;

        public CompSettingsValues()
        {
            Overrides = new CompGroupOverrides();
        }

    }

    [ProtoContract]
    public class WeaponStateValues
    {
        [ProtoMember(1)] public float Heat;
        [ProtoMember(2)] public int CurrentAmmo;
        [ProtoMember(3)] public MyFixedPoint CurrentMags;
        [ProtoMember(4)] public int ShotsFired;
        [ProtoMember(5)] public TerminalActionState ManualShoot = TerminalActionState.ShootOff;
        [ProtoMember(6)] public int SingleShotCounter;
        [ProtoMember(7)] public float CurrentCharge;
    }

    [ProtoContract]
    public class PlayerControl
    {
        [ProtoMember(1), DefaultValue(-1)] public long PlayerId = -1;
        [ProtoMember(2)] public ControlType CurrentControlType;
    }
    
    public enum ControlType
    {
        UI,
        Toolbar,
        None
    }

    [ProtoContract]
    public class WeaponSettingsValues
    {
        [ProtoMember(1)] public bool Enable = true;
    }
    
    
    [ProtoContract]
    public class MPTargetSync
    {
        [ProtoMember(1)] public TransferTarget[] Targets;


        public void Save(WeaponComponent comp, Guid id)
        {
            if (!comp.Session.MpActive) return;

            if (comp.MyCube == null || comp.MyCube.Storage == null) return;
            
            var binary = MyAPIGateway.Utilities.SerializeToBinary(this);
            comp.MyCube.Storage[id] = Convert.ToBase64String(binary);
        }

        public static void Load(WeaponComponent comp, Guid id)
        {
            if (!comp.Session.MpActive) return;

            string rawData;
            byte[] base64;
            if (comp.MyCube.Storage.TryGetValue(id, out rawData))
            {
                base64 = Convert.FromBase64String(rawData);
                comp.TargetsToUpdate = MyAPIGateway.Utilities.SerializeFromBinary<MPTargetSync>(base64);
            }
            else
            {
                comp.TargetsToUpdate = new MPTargetSync();
                comp.TargetsToUpdate.Targets = new TransferTarget[comp.Platform.Weapons.Length]; 
                for (int i = 0; i < comp.TargetsToUpdate.Targets.Length; i++)
                    comp.TargetsToUpdate.Targets[i] = new TransferTarget();
            }
        }
        public MPTargetSync() { }
    }

    [ProtoContract]
    public class CompGroupOverrides
    {
        [ProtoMember(1), DefaultValue(true)] public bool Activate = true;
        [ProtoMember(2)] public bool Neutrals;
        [ProtoMember(3)] public bool Unowned;
        [ProtoMember(4)] public bool Friendly;
        [ProtoMember(5)] public bool TargetPainter;
        [ProtoMember(6)] public bool ManualControl;
        [ProtoMember(7)] public bool FocusTargets;
        [ProtoMember(8)] public bool FocusSubSystem;
        [ProtoMember(9)] public BlockTypes SubSystem = BlockTypes.Any;
        [ProtoMember(10)] public bool Meteors;
        [ProtoMember(11)] public bool Biologicals;
        [ProtoMember(12)] public bool Projectiles;
    }
}
