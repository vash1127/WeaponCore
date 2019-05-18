using System;
using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using WeaponCore.Support;

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
                    lock (_projectiles.WaitA) DrawLists(DrawProjectilesA);
                    lock (_projectiles.WaitB) DrawLists(DrawProjectilesB);
                    lock (_projectiles.WaitC) DrawLists(DrawProjectilesC);
                    lock (_projectiles.WaitD) DrawLists(DrawProjectilesD);
                    lock (_projectiles.WaitE) DrawLists(DrawProjectilesE);
                    lock (_projectiles.WaitF) DrawLists(DrawProjectilesF);
                    if (!DrawBeams.IsEmpty)
                        foreach (var b in DrawBeams)
                            DrawBeam(b);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SessionDraw: {ex}"); }
        }

        private static void DrawLists(List<DrawProjectile> drawList)
        {
            for (int i = 0; i < drawList.Count; i++)
            {
                var p = drawList[i];
                var wDef = p.Weapon.WeaponType;
                var line = p.Projectile;
                MyTransparentGeometry.AddLocalLineBillboard(wDef.PhysicalMaterial, wDef.TrailColor, line.From, 0, line.Direction, (float)line.Length, wDef.ShotWidth);
            }
            drawList.Clear();
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                Timings();
                if (!_projectiles.Hits.IsEmpty) ProcessHits();
                UpdateWeaponPlatforms();
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
            //MyEntities.OnEntityCreate += MyEntities_OnEntityCreate;
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

