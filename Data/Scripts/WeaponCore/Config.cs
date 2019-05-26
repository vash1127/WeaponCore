using System.Collections.Generic;
using VRage.Utils;
using WeaponCore.Support;

namespace WeaponCore.Data.Scripts.WeaponCore
{
    internal class Config
    {
        internal Dictionary<string, TurretDefinition> TurretDefinitions = new Dictionary<string, TurretDefinition>();
        internal Dictionary<string, BarrelGroup> BarrelDefinitions = new Dictionary<string, BarrelGroup>();
        internal Dictionary<string, WeaponDefinition> WeaponDefinitions = new Dictionary<string, WeaponDefinition>();

        internal void Init()
        {
            foreach (var def in TurretDefinitions)
                Session.Instance.WeaponStructures.Add(MyStringHash.GetOrCompute(def.Key), new WeaponStructure(def, WeaponDefinitions, BarrelDefinitions));
        }
    }
}
