using System;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.Components;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponDefinition;

namespace WeaponCore
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation, int.MinValue)]
    public partial class Session : MySessionComponentBase
    {
        public override void BeforeStart()
        {
            try
            {
                BeforeStartInit();
                MyConfig.Init();
            }
            catch (Exception ex) { Log.Line($"Exception in BeforeStart: {ex}"); }
        }

        public override void Draw()
        {
            try
            {
                if (!DedicatedServer && !DrawProjectiles.IsEmpty)
                {
                    DrawProjectile pInfo;
                    while (DrawProjectiles.TryDequeue(out pInfo))
                    {
                        if (pInfo.Logic == null) continue;
                        var structure = pInfo.Logic.Platform.Structure;
                        switch (structure.WeaponSystems[structure.PartNames[0]].WeaponType.Ammo)
                        {
                            case AmmoType.Beam:
                                DrawBeam(pInfo);
                                break;
                            case AmmoType.Bolt:
                                DrawBolt(pInfo);
                                break;
                            case AmmoType.Missile:
                                DrawMissile(pInfo);
                                break;
                        }
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SessionDraw: {ex}"); }
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                Timings();
                lock (_projectiles) if (!_projectiles.Hits.IsEmpty) ProcessHits();

                for (int i = 0; i < Logic.Count; i++)
                {
                    var logic = Logic[i];
                    if (logic.State.Value.Online)
                    {
                        for (int j = 0; j < logic.Platform.Weapons.Length; j++)
                        {
                            if (j != 0) continue;
                            var w = logic.Platform.Weapons[j];
                            if (w.TrackTarget && w.TargetSwap) w.SelectTarget();
                            if (w.TurretMode && w.Target != null) w.Rotate();
                            if (w.TrackTarget && w.ReadyToTrack) logic.Turret.TrackTarget(w.Target);
                            if (w.ReadyToShoot) w.Shoot();
                            switch (w.WeaponType.Ammo)
                            {
                                case AmmoType.Beam:
                                    GenerateBeams(logic);
                                    BeamOn = true;
                                    continue;
                                case AmmoType.Bolt:
                                    GenerateBolts(logic, j);
                                    continue;
                                case AmmoType.Missile:
                                    GenerateMissiles(logic);
                                    continue;
                            }
                        }
                    }
                }

                if (BeamOn)
                {
                    Dispatched = true;
                    lock (_projectiles) MyAPIGateway.Parallel.Start(_projectiles.RunBeams, WebDispatchDone);
                    BeamOn = false;
                }
                lock (_projectiles) MyAPIGateway.Parallel.Start(_projectiles.Update);
            }
            catch (Exception ex) { Log.Line($"Exception in SessionBeforeSim: {ex}"); }
        }

        public override void LoadData()
        {
            Instance = this;
        }

        protected override void UnloadData()
        {
            SApi.Unload();
            Instance = null;

            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PACKET_ID, ReceivedPacket);


            MyVisualScriptLogicProvider.PlayerDisconnected -= PlayerDisconnected;
            MyVisualScriptLogicProvider.PlayerRespawnRequest -= PlayerConnected;

            Log.Line("Logging stopped.");
            Log.Close();
        }

    }
}

