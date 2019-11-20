using System;
using Sandbox.Game;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.Utils;
using WeaponCore.Platform;
using IMyConveyorSorter = Sandbox.ModAPI.IMyConveyorSorter;

namespace WeaponCore.Support
{
    public partial class WeaponComponent : MyEntityComponentBase
    {
        public override void OnAddedToContainer()
        {
            try
            {
                base.OnAddedToContainer();
                if (Container.Entity.InScene)
                {
                    lock (this)
                        if (Platform == null)
                            InitPlatform();
                }
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToContainer: {ex}"); }
        }

        public override void OnAddedToScene()
        {
            try
            {
                base.OnAddedToScene();
                lock (this)
                {
                    if (MainInit) ReInitPlatform();
                    else MyAPIGateway.Utilities.InvokeOnGameThread(InitPlatform);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToScene: {ex}"); }
        }
        
        public override void OnBeforeRemovedFromContainer()
        {
            base.OnBeforeRemovedFromContainer();
            if (Container.Entity.InScene)
            {
            }
            else
            {
                SinkInfo.RequiredInputFunc = null;
                Sink.Init(MyStringHash.GetOrCompute("Charging"), SinkInfo);
                Sink = null;
            }
        }

        public void InitPlatform()
        {
            _isServer = Ai.Session.IsServer;
            _isDedicated = Ai.Session.DedicatedServer;
            _mpActive = Ai.Session.MpActive;
            Entity.NeedsUpdate = ~MyEntityUpdateEnum.EACH_10TH_FRAME;
            Ai.FirstRun = true;
            Platform = new MyWeaponPlatform(this);
            if (!Platform.Inited)
            {
                WeaponComponent removed;
                Ai.WeaponBase.TryRemove(MyCube, out removed);
                Log.Line("init platform returned");
                return;
            }

            if (MyCube is IMyLargeMissileTurret)
            {
                MissileBase = (IMyLargeMissileTurret)MyCube;
                IsSorterTurret = false;
                MissileBase.EnableIdleRotation = false;
            }
            else if (MyCube is IMyConveyorSorter)
            {
                SorterBase = (IMyConveyorSorter)MyCube;
                IsSorterTurret = true;
            }

            //TODO add to config

            StorageSetup();

            MaxRequiredPower = 0;
            HeatPerSecond = 0;
            OptimalDps = 0;

            for (int i  = 0; i< Platform.Weapons.Length; i++)
            {
                var weapon = Platform.Weapons[i];
                weapon.InitTracking();
                DpsAndHeatInit(weapon);
                weapon.UpdateBarrelRotation();

                if (State.Value.Weapons[weapon.WeaponId].CurrentMags == 0)
                    weapon.EventTriggerStateChanged(Weapon.EventTriggers.EmptyOnGameLoad, true);

            }

            Ai.OptimalDps += OptimalDps;

            InventoryInit();
            PowerInit();
            RegisterEvents();
            OnAddedToSceneTasks();

            if (IsSorterTurret)
            {
                if (!SorterBase.Enabled)
                {
                    foreach (var w in Platform.Weapons)
                        w.EventTriggerStateChanged(Weapon.EventTriggers.TurnOff, true);
                }
            }
            else {
                if (!MissileBase.Enabled)
                {
                    foreach (var w in Platform.Weapons)
                        w.EventTriggerStateChanged(Weapon.EventTriggers.TurnOff, true);
                }
            }

            MainInit = true;
        }

        public void ReInitPlatform()
        {
            GridAi gridAi;
            if (!Ai.Session.GridTargetingAIs.TryGetValue(MyCube.CubeGrid, out gridAi))
            {
                gridAi = new GridAi(MyCube.CubeGrid, Ai.Session, Ai.Session.Tick);
                Ai.Session.GridTargetingAIs.TryAdd(MyCube.CubeGrid, gridAi);
            }
            Ai = gridAi;
            RegisterEvents();
            if (gridAi != null && gridAi.WeaponBase.TryAdd(MyCube, this))
                MyAPIGateway.Utilities.InvokeOnGameThread(OnAddedToSceneTasks);
        }

        private void OnAddedToSceneTasks()
        {
            if (!Ai.Session.GridToFatMap.ContainsKey(MyCube.CubeGrid))
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(OnAddedToSceneTasks);
                return;
            }

            if (MainInit)
                Platform.ResetParts(this);

            Entity.NeedsWorldMatrix = true;

            Ai.TotalSinkPower += MaxRequiredPower;
            Ai.MinSinkPower += IdlePower;
            Ai.RecalcPowerPercent = true;
            Ai.UpdatePowerSources = true;
            if (!Ai.GridInit)
            {
                Ai.GridInit = true;
                foreach (var cubeBlock in Ai.Session.GridToFatMap[MyCube.CubeGrid].MyCubeBocks)
                {
                    Ai.FatBlockAdded(cubeBlock);
                }
            }

            Status = !IsWorking ? Start.Starting : Start.ReInit;
        }

        public override void OnRemovedFromScene()
        {
            try
            {
                base.OnRemovedFromScene();
                RemoveComp();
            }
            catch (Exception ex) { Log.Line($"Exception in OnRemovedFromScene: {ex}"); }
        }

        public override bool IsSerialized()
        {
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                if (IsSorterTurret)
                {
                    if (SorterBase.Storage != null)
                    {
                        State.SaveState();
                        Set.SaveSettings();
                    }
                }
                else
                {
                    if (MissileBase.Storage != null)
                    {
                        State.SaveState();
                        Set.SaveSettings();
                    }
                }
            }
            return false;
        }

        public override string ComponentTypeDebugString
        {
            get { return "Shield"; }
        }
    }
}
