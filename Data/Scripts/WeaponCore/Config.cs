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
            if (Session.Instance.Inited) return;
            Log.Init("debugdevelop.log");
            Log.Line($"Logging Started");
            foreach (var weaponDef in WeaponDefinitions)
            {
                foreach (var mount in weaponDef.MountPoints)
                {
                    var subTypeId = mount.Key;
                    var subPartId = mount.Value;
                    if (!TurretDefinitions.ContainsKey(subTypeId))
                        TurretDefinitions[subTypeId] = new Dictionary<string, string>
                        {
                            [subPartId] = weaponDef.DefinitionId
                        };
                    else TurretDefinitions[subTypeId][subPartId] = weaponDef.DefinitionId;
                }
            }

            foreach (var def in TurretDefinitions)
            {
                var subTypeIdHash = MyStringHash.GetOrCompute(def.Key);
                Session.Instance.SubTypeIdHashMap.Add(def.Key, subTypeIdHash);
                Session.Instance.WeaponPlatforms.Add(subTypeIdHash, new WeaponStructure(def, WeaponDefinitions));
            }
            Session.Instance.Inited = true;
        }
    }
}
