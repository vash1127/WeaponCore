using System.Collections.Generic;
using VRage.Utils;
using WeaponCore.Support;

namespace WeaponCore.Data.Scripts.WeaponCore
{
    internal class Config
    {
        internal Dictionary<string, Dictionary<string, string>> TurretDefinitions = new Dictionary<string, Dictionary<string, string>>();
        internal List<WeaponDefinition> WeaponDefinitions = new List<WeaponDefinition>();

        internal void Init()
        {
            foreach (var weaponDef in WeaponDefinitions)
            {
                foreach (var mount in weaponDef.MountPoints)
                {
                    var subTypeId = mount.Key;
                    var subPartId = mount.Value;
                    TurretDefinitions[subTypeId] = new Dictionary<string, string>
                    {
                        [subPartId] = weaponDef.DefinitionId
                    };
                }
            }
            foreach (var def in TurretDefinitions)
                Session.Instance.WeaponStructures.Add(MyStringHash.GetOrCompute(def.Key), new WeaponStructure(def, WeaponDefinitions));
        }
    }
}
