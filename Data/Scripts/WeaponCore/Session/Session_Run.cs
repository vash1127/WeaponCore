using System;
using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Utils;
using VRageMath;
using VRageRender;
using WeaponCore.Projectiles;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponDefinition;
using static WeaponCore.Projectiles.Projectiles;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

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
                if (!DedicatedServer)
                {
                    DsUtil.Sw.Restart();
                    var even = Tick % 2 == 0;
                    if (even)
                    {
                        lock (DrawProjectiles1)
                        {
                            for (int i = 0; i < DrawProjectiles1.Count; i++)
                            {
                                var p = DrawProjectiles1[i];
                                var wDef = p.Weapon.WeaponType;
                                var line = p.Projectile;
                                MyTransparentGeometry.AddLocalLineBillboard(wDef.PhysicalMaterial, wDef.TrailColor, line.From, 0, line.Direction, (float)line.Length, wDef.ShotWidth);
                            }
                            DrawProjectiles1.Clear();
                        }
                    }
                    else
                    {
                        lock (DrawProjectiles0)
                        {
                            for (int i = 0; i < DrawProjectiles0.Count; i++)
                            {
                                var p = DrawProjectiles0[i];
                                var wDef = p.Weapon.WeaponType;
                                var line = p.Projectile;
                                MyTransparentGeometry.AddLocalLineBillboard(wDef.PhysicalMaterial, wDef.TrailColor, line.From, 0, line.Direction, (float)line.Length, wDef.ShotWidth);
                            }
                            DrawProjectiles0.Clear();
                        }
                    }
                    DsUtil.StopWatchReport("test", -1);
                    if (!DrawBeams.IsEmpty)
                        foreach (var b in DrawBeams)
                            DrawBeam(b);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SessionDraw: {ex}"); }
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                Timings();
                if (!_projectiles.Hits.IsEmpty) ProcessHits();
                DsUtil.Sw.Restart();
                lock (Fired)
                {
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
                                if (w.ShotCounter == 0)
                                {
                                    switch (w.WeaponType.Ammo)
                                    {
                                        case AmmoType.Beam:
                                            GenerateBeams(w);
                                            BeamOn = true;
                                            continue;
                                        default:
                                            var firedBolt = new FiredProjectile(w, _shotPool.Get());
                                            foreach (var m in w.Muzzles)
                                                if (Tick == m.LastShot)
                                                    firedBolt.Projectiles.Add(new Shot(m.Position, m.Direction));
                                            Fired.Add(firedBolt);
                                            break;
                                    }
                                }
                            }
                        }
                    }
                }
                DsUtil.StopWatchReport("test", -1);
                if (BeamOn)
                {
                    Dispatched = true;
                    MyAPIGateway.Parallel.Start(_projectiles.RunBeams, WebDispatchDone);
                    BeamOn = false;
                }

                MyAPIGateway.Parallel.Start(_projectiles.Update);
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

