using System.Collections.Generic;
using VRage;
using VRage.Game;
using VRage.Utils;
using VRageMath;
using static CoreSystems.Support.WeaponDefinition;
using static CoreSystems.Support.WeaponDefinition.HardPointDef;
namespace CoreSystems.Support
{

    internal class CoreStructure
    {
        internal Dictionary<MyStringHash, CoreSystem> PartSystems;
        internal Dictionary<int, int> HashToId;

        internal MyStringHash[] PartHashes;
        internal bool MultiParts;
        internal int ConstructPartCap;
        internal int PrimaryPart;
        internal string ModPath;
        internal Session Session;
        internal StructureTypes StructureType;
        internal EnittyTypes EntityType;
        internal float ApproximatePeakPowerCombined;
        internal int PowerPriority;
        internal enum EnittyTypes
        {
            Invalid,
            Rifle,
            Phantom,
            Block,
        }

        internal enum StructureTypes
        {
            Invalid,
            Weapon,
            Upgrade,
            Support,
            Phantom
        }
    }

    internal class WeaponStructure : CoreStructure
    {
        internal bool HasTurret;
        internal WeaponStructure(Session session, KeyValuePair<string, Dictionary<string, MyTuple<string, string, string>>> tDef, List<WeaponDefinition> wDefList, string modPath)
        {
            Session = session;
            var map = tDef.Value;
            var numOfParts = wDefList.Count;
            MultiParts = numOfParts > 1;
            ModPath = modPath;
            var partHashes = new MyStringHash[numOfParts];
            var muzzleHashes = new MyStringHash[numOfParts];
            var partId = 0;
            PartSystems = new Dictionary<MyStringHash, CoreSystem>(MyStringHash.Comparer);
            HashToId = new Dictionary<int, int>();
            PrimaryPart = -1;
            var partCap = 0;
            foreach (var w in map)
            {
                var typeName = w.Value.Item1;
                WeaponDefinition weaponDef = null;
                foreach (var def in wDefList)
                    if (def.HardPoint.PartName == typeName) weaponDef = def;

                if (weaponDef == null)
                {
                    Log.Line("CoreStructure failed to match PartName to typeName");
                    return;
                }

                var muzzletNameHash = MyStringHash.GetOrCompute(w.Key);
                muzzleHashes[partId] = muzzletNameHash;
                var azimuthNameHash = MyStringHash.GetOrCompute(w.Value.Item2);
                var elevationNameHash = MyStringHash.GetOrCompute(w.Value.Item3);
                var partNameIdHash = MyStringHash.GetOrCompute(weaponDef.HardPoint.PartName + $" {partId}");
                partHashes[partId] = partNameIdHash;

                var cap = weaponDef.HardPoint.Other.ConstructPartCap;
                if (partCap == 0 && cap > 0) partCap = cap;
                else if (cap > 0 && partCap > 0 && cap < partCap) partCap = cap;

                if (weaponDef.HardPoint.Ai.PrimaryTracking && PrimaryPart < 0)
                    PrimaryPart = partId;

                weaponDef.HardPoint.DeviateShotAngle = MathHelper.ToRadians(weaponDef.HardPoint.DeviateShotAngle);

                var shrapnelNames = new HashSet<string>();
                for (int i = 0; i < weaponDef.Ammos.Length; i++)
                {
                    var ammo = weaponDef.Ammos[i];
                    if (!shrapnelNames.Contains(ammo.Fragment.AmmoRound) && !string.IsNullOrEmpty(ammo.Fragment.AmmoRound))
                        shrapnelNames.Add(ammo.Fragment.AmmoRound);
                }

                var weaponAmmo = new WeaponSystem.AmmoType[weaponDef.Ammos.Length];
                for (int i = 0; i < weaponDef.Ammos.Length; i++)
                {
                    var ammo = weaponDef.Ammos[i];
                    var ammoDefId = new MyDefinitionId();
                    var ejectionDefId = new MyDefinitionId();

                    var ammoEnergy = ammo.AmmoMagazine == string.Empty || ammo.AmmoMagazine == "Energy";
                    foreach (var def in Session.AllDefinitions)
                    {
                        if (ammoEnergy && def.Id.SubtypeId.String == "Energy" || def.Id.SubtypeId.String == ammo.AmmoMagazine)
                            ammoDefId = def.Id;

                        if (ammo.Ejection.Type == AmmoDef.EjectionDef.SpawnType.Item && !string.IsNullOrEmpty(ammo.Ejection.CompDef.ItemName) && def.Id.SubtypeId.String == ammo.Ejection.CompDef.ItemName)
                            ejectionDefId = def.Id;
                    }


                    Session.AmmoDefIds.Add(ammoDefId);
                    Session.AmmoDamageMap[ammo] = null;
                    var ammoType = new WeaponSystem.AmmoType { AmmoDef = ammo, AmmoDefinitionId = ammoDefId, EjectionDefinitionId = ejectionDefId, AmmoName = ammo.AmmoRound, IsShrapnel = shrapnelNames.Contains(ammo.AmmoRound) }; 
                    weaponAmmo[i] = ammoType;
                }

                var partHash = (tDef.Key + partNameIdHash + elevationNameHash + muzzletNameHash + azimuthNameHash).GetHashCode();
                HashToId.Add(partHash, partId);
                var coreSystem = new WeaponSystem(Session, partNameIdHash, muzzletNameHash, azimuthNameHash, elevationNameHash, weaponDef, typeName, weaponAmmo, partHash, partId);

                if (coreSystem.Values.HardPoint.Ai.TurretAttached && !HasTurret)
                    HasTurret = true;
                
                ApproximatePeakPowerCombined += coreSystem.ApproximatePeakPower;

                PartSystems.Add(partNameIdHash, coreSystem);
                partId++;
            }

            if (PrimaryPart == -1)
                PrimaryPart = 0;

            ConstructPartCap = partCap;
            PartHashes = partHashes;

            var system = PartSystems[PartHashes[PrimaryPart]];
            StructureType = StructureTypes.Weapon;
            EntityType = system.PartType == HardwareDef.HardwareType.HandWeapon ? EnittyTypes.Block : EnittyTypes.Rifle;
        }
    }

    internal class UpgradeStructure : CoreStructure
    {
        internal UpgradeStructure(Session session, KeyValuePair<string, Dictionary<string, MyTuple<string, string, string>>> tDef, List<UpgradeDefinition> wDefList, string modPath)
        {
            Session = session;
            var map = tDef.Value;
            var numOfParts = wDefList.Count;
            MultiParts = numOfParts > 1;
            ModPath = modPath;
            var partHashes = new MyStringHash[numOfParts];
            var partId = 0;
            PartSystems = new Dictionary<MyStringHash, CoreSystem>(MyStringHash.Comparer);
            HashToId = new Dictionary<int, int>();
            PrimaryPart = -1;
            var partCap = 0;
            foreach (var w in map)
            {
                var typeName = w.Value.Item1;
                UpgradeDefinition upgradeDef = null;
                foreach (var def in wDefList)
                    if (def.HardPoint.PartName == typeName) upgradeDef = def;

                if (upgradeDef == null)
                {
                    Log.Line("CoreStructure failed to match PartName to typeName");
                    return;
                }

                var partNameIdHash = MyStringHash.GetOrCompute(upgradeDef.HardPoint.PartName + $" {partId}");
                partHashes[partId] = partNameIdHash;

                var cap = upgradeDef.HardPoint.Other.ConstructPartCap;
                if (partCap == 0 && cap > 0) partCap = cap;
                else if (cap > 0 && partCap > 0 && cap < partCap) partCap = cap;

                if (PrimaryPart < 0)
                    PrimaryPart = partId;

                var partHash = (tDef.Key + partNameIdHash).GetHashCode();
                HashToId.Add(partHash, partId);
                var coreSystem = new UpgradeSystem(Session, partNameIdHash, upgradeDef, typeName, partHash, partId);

                PartSystems.Add(partNameIdHash, coreSystem);
                partId++;
            }

            if (PrimaryPart == -1)
                PrimaryPart = 0;

            ConstructPartCap = partCap;
            PartHashes = partHashes;

            StructureType = StructureTypes.Upgrade;
            EntityType = EnittyTypes.Block;
        }
    }

    internal class SupportStructure : CoreStructure
    {
        internal bool CommonBlockRange;
        internal SupportStructure(Session session, KeyValuePair<string, Dictionary<string, MyTuple<string, string, string>>> tDef, List<SupportDefinition> wDefList, string modPath)
        {
            Session = session;
            var map = tDef.Value;
            var numOfParts = wDefList.Count;
            MultiParts = numOfParts > 1;
            ModPath = modPath;
            var partHashes = new MyStringHash[numOfParts];
            var partId = 0;
            PartSystems = new Dictionary<MyStringHash, CoreSystem>(MyStringHash.Comparer);
            HashToId = new Dictionary<int, int>();
            PrimaryPart = -1;
            var partCap = 0;
            var blockDistance = - 1;
            var commonBlockRange = true;
            foreach (var s in map)
            {
                var typeName = s.Value.Item1;
                SupportDefinition supportDef = null;
                foreach (var def in wDefList)
                    if (def.HardPoint.PartName == typeName) supportDef = def;

                if (supportDef == null)
                {
                    Log.Line("CoreStructure failed to match PartName to typeName");
                    return;
                }
                if (blockDistance < 0)
                    blockDistance = supportDef.Effect.BlockRange;

                if (blockDistance != supportDef.Effect.BlockRange)
                    commonBlockRange = false;

                var partNameIdHash = MyStringHash.GetOrCompute(supportDef.HardPoint.PartName + $" {partId}");
                partHashes[partId] = partNameIdHash;

                var cap = supportDef.HardPoint.Other.ConstructPartCap;
                if (partCap == 0 && cap > 0) partCap = cap;
                else if (cap > 0 && partCap > 0 && cap < partCap) partCap = cap;

                if (PrimaryPart < 0)
                    PrimaryPart = partId;

                var partHash = (tDef.Key + partNameIdHash).GetHashCode();
                HashToId.Add(partHash, partId);
                var coreSystem = new SupportSystem(Session, partNameIdHash, supportDef, typeName, partHash, partId);

                PartSystems.Add(partNameIdHash, coreSystem);
                partId++;
            }

            CommonBlockRange = commonBlockRange;

            if (PrimaryPart == -1)
                PrimaryPart = 0;

            ConstructPartCap = partCap;
            PartHashes = partHashes;

            StructureType = StructureTypes.Support;
            EntityType = EnittyTypes.Block;
        }
    }

    internal class PhantomStructure : CoreStructure
    {
        internal PhantomStructure(Session session, KeyValuePair<string, Dictionary<string, MyTuple<string, string, string>>> tDef, List<PhantomDefinition> wDefList, string modPath)
        {
            Session = session;
            var map = tDef.Value;
            var numOfParts = wDefList.Count;
            MultiParts = numOfParts > 1;
            ModPath = modPath;
            var partHashes = new MyStringHash[numOfParts];
            var partId = 0;
            PartSystems = new Dictionary<MyStringHash, CoreSystem>(MyStringHash.Comparer);
            HashToId = new Dictionary<int, int>();
            PrimaryPart = -1;
            var partCap = 0;
            foreach (var w in map)
            {
                var typeName = w.Value.Item1;
                PhantomDefinition phantomDef = null;
                foreach (var def in wDefList)
                    if (def.HardPoint.PartName == typeName) phantomDef = def;

                if (phantomDef == null)
                {
                    Log.Line("CoreStructure failed to match PartName to typeName");
                    return;
                }

                var partNameIdHash = MyStringHash.GetOrCompute(phantomDef.HardPoint.PartName + $" {partId}");
                partHashes[partId] = partNameIdHash;

                var cap = phantomDef.HardPoint.Other.ConstructPartCap;
                if (partCap == 0 && cap > 0) partCap = cap;
                else if (cap > 0 && partCap > 0 && cap < partCap) partCap = cap;

                if (PrimaryPart < 0)
                    PrimaryPart = partId;

                var partHash = (tDef.Key + partNameIdHash).GetHashCode();
                HashToId.Add(partHash, partId);
                var coreSystem = new PhantomSystem(Session, partNameIdHash, phantomDef, typeName, partHash, partId);

                PartSystems.Add(partNameIdHash, coreSystem);
                partId++;
            }

            if (PrimaryPart == -1)
                PrimaryPart = 0;

            ConstructPartCap = partCap;
            PartHashes = partHashes;

            StructureType = StructureTypes.Phantom;
            EntityType = EnittyTypes.Phantom;
        }
    }
}
