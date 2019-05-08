using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using Sandbox.Game.Entities;
using VRage.ModAPI;
using VRage.Utils;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Logic
    {
        private void BeforeInit()
        {
            if (MyGrid == null) return;

            _isServer = Session.Instance.IsServer;
            _isDedicated = Session.Instance.DedicatedServer;
            _mpActive = Session.Instance.MpActive;

            Session.Instance.Logic.Add(this);
            _subTypeIdHash = MyStringHash.GetOrCompute(Turret.BlockDefinition.SubtypeId);
            Platform = new MyWeaponPlatform(_subTypeIdHash, Entity);
            InitPower();
            Targeting = MyGrid.Components.Get<MyGridTargeting>();
            MainInit = true;
        }

        private bool ResetEntity()
        {
            MyGrid = (MyCubeGrid)Turret.CubeGrid;
            MyCube = Turret as MyCubeBlock;
            if (MyGrid.Physics == null) return false;
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;

            if (_bInit) ResetEntityTick = Session.Instance.Tick + 1800;

            _bCount = 0;
            _bInit = false;
            _aInit = false;
            _allInited = false;
            WarmedUp = false;
            return true;
        }

        private bool PostInit()
        {
            if (!_isServer && _clientNotReady) return false;
            Session.Instance.CreateLogicElements(Turret);
            WepUi.CreateUi(Turret);
            if (!Session.Instance.WepAction)
            {
                Session.Instance.WepAction = true;
                Session.AppendConditionToAction<IMyLargeTurretBase>((a) => Session.Instance.WepActions.Contains(a.Id), (a, b) => b.GameLogic.GetAs<Logic>() != null && Session.Instance.WepActions.Contains(a.Id));
            }
            if (_isServer && !IsFunctional) return false;

            if (_mpActive && _isServer) State.NetworkUpdate();

            _allInited = true;
            return true;
        }

        private void StorageSetup()
        {
            var isServer = MyAPIGateway.Multiplayer.IsServer;

            if (Set == null) Set = new LogicSettings(Turret);
            if (State == null) State = new LogicState(Turret);

            if (Turret.Storage == null) State.StorageInit();

            if (!isServer)
            {
                var enforcement = Enforcements.LoadEnforcement(Turret);
                if (enforcement != null) Session.Enforced = enforcement;
            }
            Set.LoadSettings();
            if (!State.LoadState() && !isServer) _clientNotReady = true;
            UpdateSettings(Set.Value);
            if (isServer)
            {
                State.Value.Overload = false;
                State.Value.Heat = 0;
            }
            Set.LoadSettings();
            State.LoadState();
            StorageInit = true;
        }

        private void InitPower()
        {
            try
            {
                var enableState = Turret.Enabled;
                if (enableState)
                {
                    Turret.Enabled = false;
                    Turret.Enabled = true;
                }
                    
                Sink = Entity.Components.Get<MyResourceSinkComponent>();
                Sink.SetRequiredInputByType(_gId , .1f);
                Sink.Update();
            }
            catch (Exception ex) { Log.Line($"Exception in AddResourceSourceComponent: {ex}"); }
        }
    }
}
