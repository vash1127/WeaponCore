using System;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRageMath;
using WeaponCore.Support;
using static Sandbox.Definitions.MyDefinitionManager;

namespace WeaponCore
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation | MyUpdateOrder.Simulation, int.MaxValue - 1)]
    public partial class Session : MySessionComponentBase
    {
        public override void BeforeStart()
        {
            try
            {
                BeforeStartInit();
            }
            catch (Exception ex) { Log.Line($"Exception in BeforeStart: {ex}"); }
        }

        public override void Draw()
        {
            try
            {
                if (!DedicatedServer)
                {
                    if (Ui.WheelActive) Ui.DrawWheel();

                    for (int i = 0; i < Projectiles.Wait.Length; i++)
                        lock (Projectiles.Wait[i])
                            DrawLists(Projectiles.DrawProjectiles[i]);
                    if (_shrinking.Count > 0)
                        Shrink();
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SessionDraw: {ex}"); }
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                /*
                if (Tick180)
                {
                    var a = 0;
                    var c = 0;
                    var m = 0;
                    var w = 0;
                    var p = 0;
                    var g = MyParticlesManager.ParticleEffectsForUpdate.Count;

                    for (int i = 0; i < Projectiles.Wait.Length; i++)
                        p += Projectiles.ProjectilePool[i].Active.Count;

                    foreach (var y in MyParticlesManager.ParticleEffectsForUpdate)
                    {
                        if (y.Name == "ShipWelderArc") a++;
                        else if (y.Name == "Smoke_Missile") c++;
                        else if (y.Name == "Explosion_Missile") m++;
                        else if (y.Name == "Explosion_Warhead_02") w++;
                        //else Log.Line($"{y.Name}");
                    }
                    //Log.Line($"projectiles:{p} - particles:{g} - eCount:{ExplosionCounter} - arkCount:{a} - Smoke_Missile:{c} - missileExp:{m} - what:{w}");
                }
                */
                Timings();
                Ui.UpdateInput();
                if (!Hits.IsEmpty) ProcessHits();
                if (!InventoryEvent.IsEmpty) UpdateBlockInventories();
            }
            catch (Exception ex) { Log.Line($"Exception in SessionBeforeSim: {ex}"); }
        }

        public override void Simulate()
        {
            try
            {
                AiLoop();
                UpdateWeaponPlatforms();
                Projectiles.Update();
            }
            catch (Exception ex) { Log.Line($"Exception in SessionBeforeSim: {ex}"); }
        }

        public override void UpdatingStopped()
        {
            try
            {
                Paused();
            }
            catch (Exception ex) { Log.Line($"Exception in UpdatingStopped: {ex}"); }
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
            }
            catch (Exception ex) { Log.Line($"Exception in SessionBeforeSim: {ex}"); }
        }

        public override void LoadData()
        {
            try
            {
                Instance = this;
                MyEntities.OnEntityCreate += OnEntityCreate;
                MyEntities.OnEntityDelete += OnEntityDelete;
                MyAPIGateway.Utilities.RegisterMessageHandler(7771, Handler);
                MyAPIGateway.Utilities.SendModMessage(7772, null);
                AllDefinitions = Static.GetAllDefinitions();
                SoundDefinitions = Static.GetSoundDefinitions();
            }
            catch (Exception ex) { Log.Line($"Exception in LoadData: {ex}"); }
        }

        protected override void UnloadData()
        {
            SApi.Unload();

            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PACKET_ID, ReceivedPacket);
            MyAPIGateway.Utilities.UnregisterMessageHandler(7771, Handler);

            MyEntities.OnEntityCreate -= OnEntityCreate;
            MyEntities.OnEntityDelete -= OnEntityDelete;

            MyVisualScriptLogicProvider.PlayerDisconnected -= PlayerDisconnected;
            MyVisualScriptLogicProvider.PlayerRespawnRequest -= PlayerConnected;

            Instance = null;
            Log.Line("Logging stopped.");
            Log.Close();
        }
    }
}

