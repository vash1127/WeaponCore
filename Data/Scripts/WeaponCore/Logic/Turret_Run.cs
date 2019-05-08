using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using System;
using Sandbox.ModAPI;
using Sandbox.Common.ObjectBuilders;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using WeaponCore.Support;
using IMyLargeTurretBase = Sandbox.ModAPI.IMyLargeTurretBase;

namespace WeaponCore
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_LargeMissileTurret), false, "PDCTurretLB", "PDCTurretSB")]

    public partial class Logic : MyGameLogicComponent
    {
        public override void OnAddedToContainer()
        {
            if (!_containerInited)
            {
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
                Turret = (IMyLargeTurretBase)Entity;
                Gun = (IMyGunObject<MyGunBase>)Entity;
                _containerInited = true;
            }
            if (Entity.InScene) OnAddedToScene();
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            StorageSetup();
        }

        public override void OnAddedToScene()
        {
            try
            {
                MyGrid = (MyCubeGrid)Turret.CubeGrid;
                MyCube = Turret as MyCubeBlock;
                RegisterEvents();

                if (!MainInit) return;
                ResetEntity();
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToScene: {ex}"); }
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            try
            {
                if (!_bInit) BeforeInit();
                //else if (!_aInit) AfterInit();
                else if (_bCount < SyncCount * _bTime)
                {
                    NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                    if (!_firstLoop)
                    {
                        NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
                        if (MainInit) _bCount++;
                    }
                }
                else _readyToSync = true;

            }
            catch (Exception ex) { Log.Line($"Exception in Controller UpdateOnceBeforeFrame: {ex}"); }
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                if (!EntityAlive()) return;

                var state = WeaponState();
                if (state != Status.Online)
                {
                    if (NotFailed) FailWeapon(state);
                    else if (State.Value.Message) UpdateNetworkState();
                    return;
                }

                if (!_isServer || !State.Value.Online) return;
                if (Starting) ComingOnline();
                if (_mpActive && (Sync || _count == 29))
                {
                    if (Sync)
                    {
                        UpdateNetworkState();
                        Sync = false;
                    }
                    else if (Session.Instance.Tick1800) UpdateNetworkState();
                }
                _firstRun = false;
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
        }

        public override bool IsSerialized()
        {
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                if (Turret.Storage != null)
                {
                    State.SaveState();
                    Set.SaveSettings();
                }
            }
            return false;
        }

        public override void OnBeforeRemovedFromContainer()
        {
            if (Entity.InScene) OnRemovedFromScene();
        }

        public override void OnRemovedFromScene()
        {
            try
            {
                if (Session.Instance.Logic.Contains(this)) Session.Instance.Logic.Remove(this);
                Platform.SubParts.Entity = null;
                RegisterEvents(false);
                IsWorking = false;
                IsFunctional = false;
            }
            catch (Exception ex) { Log.Line($"Exception in OnRemovedFromScene: {ex}"); }
        }

        public override void MarkForClose()
        {
            try
            {
                base.MarkForClose();
            }
            catch (Exception ex) { Log.Line($"Exception in MarkForClose: {ex}"); }
        }

        public override void Close()
        {
            try
            {
                base.Close();
                if (Session.Instance.Logic.Contains(this)) Session.Instance.Logic.Remove(this);
            }
            catch (Exception ex) { Log.Line($"Exception in Close: {ex}"); }
        }
    }
}