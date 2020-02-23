using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;
using static WeaponCore.Support.TargetingDefinition;
using static WeaponCore.Platform.Weapon.TerminalActionState;
using static WeaponCore.Platform.MyWeaponPlatform.PlatformState;
using WeaponCore.Platform;

namespace WeaponCore.Support
{
    internal class ApiBackend
    {
        private readonly Session _session;
        internal readonly Dictionary<string, Delegate> ModApiMethods;
        private readonly Dictionary<string, Delegate> _terminalPbApiMethods;
        private delegate T5 OutFunc<T1,T2,T3,T4, T5>(T1 arg1, T2 arg2, T3 arg3, out T4 arg4);

        internal ApiBackend(Session session)
        {
            _session = session;

            ModApiMethods = new Dictionary<string, Delegate>()
            {
                ["GetAllCoreWeapons"] = new Func<List<MyDefinitionId>>(GetAllCoreWeapons),
                ["GetCoreStaticLaunchers"] = new Func<List<MyDefinitionId>>(GetCoreStaticLaunchers),
                ["GetCoreTurrets"] = new Func<IList<MyDefinitionId>>(GetCoreTurrets),
                ["SetTargetEntity"] = new Action<IMyEntity, IMyEntity, int>(SetTargetEntity),
                ["FireOnce"] = new Action<IMyTerminalBlock>(FireOnce),
                ["ToggleFire"] = new Action<IMyTerminalBlock, bool>(ToggleFire),
                ["WeaponReady"] = new Func<IMyTerminalBlock, bool>(WeaponReady),
                ["GetMaxRange"] = new Func<IMyTerminalBlock, float>(GetMaxRange),
                ["GetTurretTargetTypes"] = new Func<IMyTerminalBlock, IList<IList<Threat>>>(GetTurretTargetTypes),
                ["SetTurretTargetTypes"] = new Action<IMyTerminalBlock, IList<IList<Threat>>>(SetTurretTargetTypes),
                ["SetTurretRange"] = new Action<IMyTerminalBlock, float>(SetTurretRange),
                ["GetTargetedEntity"] = new Func<IMyTerminalBlock, IList<IMyEntity>>(GetTargetedEntity),
                ["IsTargetAligned"] = new OutFunc<IMyTerminalBlock, IMyEntity, int, Vector3D, bool>(IsTargetAligned),
                ["GetHeatLevel"] = new Func<IMyTerminalBlock, float>(GetHeatLevel),
                ["CurrentPower"] = new Func<IMyTerminalBlock, float>(CurrentPower),
                ["MaxPower"] = new Func<MyDefinitionId, float>(MaxPower),
                ["DisableRequiredPower"] = new Action<IMyTerminalBlock>(DisableRequiredPower),
                ["GetAllWeaponDefinitions"] = new Func<List<WeaponDefinition>>(GetAllWeaponDefinitions),

            };

            _terminalPbApiMethods = new Dictionary<string, Delegate>()
            {
                ["SetTargetEntity"] = new Action<IMyEntity, IMyEntity, int>(SetTargetEntity),
                ["FireOnce"] = new Action<IMyTerminalBlock>(FireOnce),
                ["ToggleFire"] = new Action<IMyTerminalBlock, bool>(ToggleFire),
                ["WeaponReady"] = new Func<IMyTerminalBlock, bool>(WeaponReady),
                ["GetMaxRange"] = new Func<IMyTerminalBlock, float>(GetMaxRange),
                ["GetTurretTargetTypes"] = new Func<IMyTerminalBlock, IList<IList<Threat>>>(GetTurretTargetTypes),
                ["SetTurretTargetTypes"] = new Action<IMyTerminalBlock, IList<IList<Threat>>>(SetTurretTargetTypes),
                ["SetTurretRange"] = new Action<IMyTerminalBlock, float>(SetTurretRange),
                ["GetTargetedEntity"] = new Func<IMyTerminalBlock, IList<IMyEntity>>(GetTargetedEntity),
                ["IsTargetAligned"] = new OutFunc<IMyTerminalBlock, IMyEntity, int, Vector3D, bool>(IsTargetAligned),
                ["GetHeatLevel"] = new Func<IMyTerminalBlock, float>(GetHeatLevel),
                ["CurrentPower"] = new Func<IMyTerminalBlock, float>(CurrentPower),
                ["MaxPower"] = new Func<MyDefinitionId, float>(MaxPower)
            };
        }

        internal void Init()
        {
            var pb = MyAPIGateway.TerminalControls.CreateProperty<Dictionary<string, Delegate>, IMyTerminalBlock>("WeaponCorePbAPI");
            pb.Getter = (b) => _terminalPbApiMethods;
            MyAPIGateway.TerminalControls.AddControl<Sandbox.ModAPI.Ingame.IMyProgrammableBlock>(pb);
        }

        private List<WeaponDefinition> GetAllWeaponDefinitions()
        {
            return new List<WeaponDefinition>(_session.WeaponDefinitions);
        }

        private List<MyDefinitionId> GetAllCoreWeapons()
        {
            return new List<MyDefinitionId>(_session.WeaponCoreBlockDefs.Values);
        }

        private List<MyDefinitionId> GetCoreStaticLaunchers()
        {
            return _session.WeaponCoreFixedBlockDefs;
        }

        private IList<MyDefinitionId> GetCoreTurrets()
        {
            return _session.WeaponCoreTurretBlockDefs.AsReadOnly();
        }

        private void SetTargetEntity(IMyEntity shooter, IMyEntity target, int priority = 0)
        {
            var shootingGrid = shooter as MyCubeGrid;

            if (shootingGrid != null)
            {
                GridAi ai;
                if (_session.GridTargetingAIs.TryGetValue(shootingGrid, out ai))
                    ai.Focus.ReassignTarget((MyEntity)target, priority, ai);
            }
            else
            {
                WeaponComponent comp;
                if(shooter.Components.TryGet(out comp))
                {
                    if (comp.Platform.State != Ready) return;

                    for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                        GridAi.AcquireTarget(comp.Platform.Weapons[i], false, (MyEntity)target);
                }
            }
        }

        private static void FireOnce(IMyTerminalBlock weaponBlock)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp))
            {
                if (comp.Platform.State != Ready) return;
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    var w = comp.Platform.Weapons[i];
                    
                    if (w.State.ManualShoot != ShootOff)
                        w.State.ManualShoot = ShootOnce;
                    else
                        w.State.ManualShoot = ShootOnce;
                }
            }
        }

        private static void ToggleFire(IMyTerminalBlock weaponBlock, bool on)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp))
            {
                if (comp.Platform.State != Ready) return;
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    var w = comp.Platform.Weapons[i];

                    if (!on && w.State.ManualShoot == ShootOn)
                    {
                        w.State.ManualShoot = ShootOff;
                        w.StopShooting();
                    }
                    else if (on && w.State.ManualShoot != ShootOff)
                        w.State.ManualShoot = ShootOn;
                    else if (on)
                        w.State.ManualShoot = ShootOn;
                }
            }
        }

        private static bool WeaponReady(IMyTerminalBlock weaponBlock)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp))
            {
                if (comp.Platform.State != Ready) return false;
                return comp.State.Value.Online;
            }

            return false;
        }

        private static float GetMaxRange(IMyTerminalBlock weaponBlock)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp))
            {
                if (comp.Platform.State != Ready) return 0f;

                var maxTrajectory = 0f;
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    var curMax = comp.Platform.Weapons[i].System.MaxTrajectory;
                    if (curMax > maxTrajectory)
                        maxTrajectory = (float)curMax;
                }
                return maxTrajectory;
            }
            return 0f;
        }

        private static IList<IList<Threat>> GetTurretTargetTypes(IMyTerminalBlock weaponBlock)
        {
            return new List<IList<Threat>>();
        }

        private static void SetTurretTargetTypes(IMyTerminalBlock weaponBlock, IList<IList<Threat>> threats)
        {

        }

        private static void SetTurretRange(IMyTerminalBlock weaponBlock, float range)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp))
            {
                if (comp.Platform.State != Ready) return;

                var maxTrajectory = GetMaxRange(weaponBlock);

                if (range > maxTrajectory)
                    comp.Set.Value.Range = maxTrajectory;
                else
                    comp.Set.Value.Range = range;
            }
        }

        private static IList<IMyEntity> GetTargetedEntity(IMyTerminalBlock weaponBlock)
        {
            IList<IMyEntity> targets = new List<IMyEntity>();
            ICollection<IMyEntity> targetsCheck = new HashSet<IMyEntity>();

            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp))
            {

                if (comp.Platform.State != Ready) return null;

                targets.Add(comp.Ai.Focus.Target[0]);
                targetsCheck.Add(comp.Ai.Focus.Target[0]);

                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    var entity = comp.Platform.Weapons[i].Target.Entity;
                    if (!comp.Platform.Weapons[i].Target.IsProjectile && !targetsCheck.Contains(entity))
                    {
                        targets.Add(entity);
                        targetsCheck.Add(entity);
                    }
                }

            }

            return targets;
        }

        private bool IsTargetAligned(IMyTerminalBlock weaponBlock, IMyEntity targetEnt, int weaponId, out Vector3D targetPos)
        {
            targetPos = targetEnt.WorldMatrix.Translation;
            Target target = new Target((MyCubeBlock)weaponBlock) { Entity = (MyEntity)targetEnt };
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp))
            {
                if (comp.Platform.State != Ready) return false;
                var w = comp.Platform.Weapons[weaponId];

                return Weapon.TargetAligned(w, target, out targetPos);
            }
            return false;
        }

        private static float GetHeatLevel(IMyTerminalBlock weaponBlock)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp))
            {

                if (comp.Platform.State != Ready || comp.MaxHeat <= 0) return 0f;

                return comp.State.Value.Heat / comp.MaxHeat;

            }

            return 0f;
        }

        private static float CurrentPower(IMyTerminalBlock weaponBlock)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp))
            {

                if (comp.Platform.State != Ready) return 0f;

                return comp.SinkPower;

            }

            return 0f;
        }

        private float MaxPower(MyDefinitionId weaponDef)
        {
            float power = 0f;
            WeaponStructure weapons;
            if (_session.WeaponPlatforms.TryGetValue(weaponDef.SubtypeId, out weapons))
            {
                foreach(var systems in weapons.WeaponSystems)
                {
                    var system = systems.Value;

                    if (!system.EnergyAmmo && !system.IsHybrid)
                        power += 0.001f;
                    else
                    {
                        var ewar = (int)system.Values.Ammo.AreaEffect.AreaEffect > 3;
                        var shotEnergyCost = ewar ? system.Values.HardPoint.EnergyCost * system.Values.Ammo.AreaEffect.AreaEffectDamage : system.Values.HardPoint.EnergyCost * system.Values.Ammo.BaseDamage;

                        power += ((shotEnergyCost * (system.RateOfFire * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS)) * system.Values.HardPoint.Loading.BarrelsPerShot) * system.Values.HardPoint.Loading.TrajectilesPerBarrel;
                    }

                }
            }
            return power;
        }

        private static void DisableRequiredPower(IMyTerminalBlock weaponBlock)
        {
            WeaponComponent comp;
            if (weaponBlock.Components.TryGet(out comp))
            {
                if (comp.Platform.State != Ready) return;

                comp.UnlimitedPower = true;
            }
        }
    }
}
