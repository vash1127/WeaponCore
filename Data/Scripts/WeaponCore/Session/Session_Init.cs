using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Session
    {
        private void BeforeStartInit()
        {
            MpActive = MyAPIGateway.Multiplayer.MultiplayerActive;
            IsServer = MyAPIGateway.Multiplayer.IsServer;
            DedicatedServer = MyAPIGateway.Utilities.IsDedicated;

            Log.Init("debugdevelop.log");
            Log.Line($"Logging Started: Server:{IsServer} - Dedicated:{DedicatedServer} - MpActive:{MpActive}");

            MyAPIGateway.Multiplayer.RegisterMessageHandler(PACKET_ID, ReceivedPacket);

            //if (!DedicatedServer && IsServer) Players.TryAdd(MyAPIGateway.Session.Player.IdentityId, MyAPIGateway.Session.Player);
            if (!DedicatedServer && IsServer) PlayerConnected(MyAPIGateway.Session.Player.IdentityId);

            MyVisualScriptLogicProvider.PlayerDisconnected += PlayerDisconnected;
            MyVisualScriptLogicProvider.PlayerRespawnRequest += PlayerConnected;
            if (!DedicatedServer)
            {
                MyAPIGateway.TerminalControls.CustomControlGetter += CustomControls;
            }

            if (IsServer)
            {
                Log.Line("LoadConf - Session: This is a server");
                UtilsStatic.PrepConfigFile();
                UtilsStatic.ReadConfigFile();
            }

            if (MpActive)
            {
                SyncDist = MyAPIGateway.Session.SessionSettings.SyncDistance;
                SyncDistSqr = SyncDist * SyncDist;
                SyncBufferedDistSqr = SyncDistSqr + 250000;
                if (Enforced.Debug >= 2) Log.Line($"SyncDistSqr:{SyncDistSqr} - SyncBufferedDistSqr:{SyncBufferedDistSqr} - DistNorm:{SyncDist}");
            }
            else
            {
                SyncDist = MyAPIGateway.Session.SessionSettings.ViewDistance;
                SyncDistSqr = SyncDist * SyncDist;
                SyncBufferedDistSqr = SyncDistSqr + 250000;
                if (Enforced.Debug >= 2) Log.Line($"SyncDistSqr:{SyncDistSqr} - SyncBufferedDistSqr:{SyncBufferedDistSqr} - DistNorm:{SyncDist}");
            }
            //MyAPIGateway.Parallel.StartBackground(WebMonitor);

            if (!IsServer) RequestEnforcement(MyAPIGateway.Multiplayer.MyId);
            foreach (var mod in MyAPIGateway.Session.Mods)
                if (mod.PublishedFileId == 1365616918) ShieldMod = true;
            ShieldMod = true;
        }

        /*
        public void MasterLoadData()
        {
            RegisterMessageHandler(1, (msg)->LoadDefinitions(msg));
            SendMessage(2, "Request all definitions");
        }

        public void SlaveLoadData()
        {
            RegisterMessageHandler(2, (obj)->SendMessage(1, slaveDefinitions));
            SendMessage(1, slaveDefinitions);
        }
        */

        /*
         *  if (!_controlsCreated)
            {
                (MyAPIUtilities.Static as IMyUtilities).InvokeOnGameThread(CreateControls);
            }
        */

        private void MyEntities_OnEntityCreate(MyEntity obj)
        {
            List<TypeHolder> lstH = null;
            /*
            if (!typesAdapter.TryGetValue(obj.GetType(), out lstH))//if we don't have table for current type, then form it.
            {
                typesAdapter[obj.GetType()] = lstH = typesToRegister.Keys.Where(t => t.IsCompatible(obj)).ToList();
            }
            */
            List<MyTuple<Func<Type>, Func<MyEntityComponentBase>>> lst;
            foreach (var h in lstH)
            {
                if (typesToRegister.TryGetValue(h, out lst))
                {
                    for (int i = 0; i < lst.Count; i++)
                    {
                        var component = lst[i].Item2();
                        obj.Components.Add(lst[i].Item1?.Invoke() ?? component.GetType(), component);
                    }
                }
            }
            var block = obj as IMyCubeBlock;
            if (block != null && idsToRegister.TryGetValue(block.BlockDefinition, out lst))
            {
                for (int i = 0; i < lst.Count; i++)
                {
                    var component = lst[i].Item2();
                    obj.Components.Add(lst[i].Item1?.Invoke() ?? component.GetType(), component);
                }
            }
        }
        static Dictionary<MyDefinitionId, List<MyTuple<Func<Type>, Func<MyEntityComponentBase>>>> idsToRegister = new Dictionary<MyDefinitionId, List<MyTuple<Func<Type>, Func<MyEntityComponentBase>>>>();
        static Dictionary<TypeHolder, List<MyTuple<Func<Type>, Func<MyEntityComponentBase>>>> typesToRegister = new Dictionary<TypeHolder, List<MyTuple<Func<Type>, Func<MyEntityComponentBase>>>>();
        //static Dictionary<Type, List<TypeHolder>> typesAdapter = new Dictionary<Type, List<FrameworkAPI.TypeHolder>>();//cache to use more direct Type comparison

        abstract class TypeHolder : IEquatable<TypeHolder>
        {
            public abstract bool IsCompatible(MyEntity ent);

            public override bool Equals(object obj)
            {
                return Equals(obj as TypeHolder);
            }

            public bool Equals(TypeHolder other)
            {
                return other != null &&
                       EqualityComparer<Type>.Default.Equals(Type, other.Type);
            }

            public override int GetHashCode()
            {
                return 2049151605 + EqualityComparer<Type>.Default.GetHashCode(Type);
            }

            public abstract Type Type { get; }

            public static bool operator ==(TypeHolder holder1, TypeHolder holder2)
            {
                return EqualityComparer<TypeHolder>.Default.Equals(holder1, holder2);
            }

            public static bool operator !=(TypeHolder holder1, TypeHolder holder2)
            {
                return !(holder1 == holder2);
            }
        }
        class TypeHolder<T> : TypeHolder
        {
            public override Type Type => typeof(T);

            public override bool IsCompatible(MyEntity ent) => ent is T;
        }

        public static void RegisterComponent(MyDefinitionId idToUse, Func<Type> typeOverride, Func<MyEntityComponentBase> component)
        {
            List<MyTuple<Func<Type>, Func<MyEntityComponentBase>>> lst;
            if (!idsToRegister.TryGetValue(idToUse, out lst))
            {
                idsToRegister[idToUse] = lst = new List<MyTuple<Func<Type>, Func<MyEntityComponentBase>>>();
            }
            lst.Add(MyTuple.Create(typeOverride, component));
        }
        public static void RegisterComponent(MyDefinitionId idToUse, Func<MyEntityComponentBase> component)
        {
            RegisterComponent(idToUse, null, component);
        }
        public static void RegisterComponent<T>(MyDefinitionId idToUse) where T : MyEntityComponentBase, new()
        {
            RegisterComponent(idToUse, null, () => new T());
        }
        public static void RegisterComponent<T, Over>(MyDefinitionId idToUse) where T : MyEntityComponentBase, Over, new()
        {
            RegisterComponent(idToUse, () => typeof(Over), () => new T());
        }
        public static void RegisterComponent<E, T>() where T : MyEntityComponentBase, new() where E : IMyEntity
        {
            RegisterComponent<E>(null, () => new T());
        }
        public static void RegisterComponent<E, T, Over>() where T : MyEntityComponentBase, Over, new() where E : IMyEntity
        {
            RegisterComponent<E>(() => typeof(Over), () => new T());
        }
        public static void RegisterComponent<E>(Func<Type> typeOverride, Func<MyEntityComponentBase> component) where E : IMyEntity
        {
            List<MyTuple<Func<Type>, Func<MyEntityComponentBase>>> lst;
            var th = new TypeHolder<E>();
            if (!typesToRegister.TryGetValue(th, out lst))
            {
                typesToRegister[th] = lst = new List<MyTuple<Func<Type>, Func<MyEntityComponentBase>>>();
            }
            lst.Add(MyTuple.Create(typeOverride, component));
        }
    }
}
