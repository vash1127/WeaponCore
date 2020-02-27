using System.IO;
using Sandbox.ModAPI;
using WeaponCore.Support;
namespace WeaponCore.Data.Scripts.WeaponCore.Support
{
    internal static class VersionControl
    {
        /*
        public static void PrepConfigFile(Session session)
        {
            const int Version = 1;
            const int Debug = 0;
            const bool DisableWeaponGridLimits = false;

            var dsCfgExists = MyAPIGateway.Utilities.FileExistsInGlobalStorage("WeaponCore.cfg");
            if (dsCfgExists)
            {
                var unPackCfg = MyAPIGateway.Utilities.ReadFileInGlobalStorage("WeaponCore.cfg");
                var unPackedData = MyAPIGateway.Utilities.SerializeFromXML<DefenseShieldsEnforcement>(unPackCfg.ReadToEnd());

                if (unPackedData.Version == Version && !invalidValue) return;

                session.Enforced.Debug = !unPackedData.Debug.Equals(-1) ? unPackedData.Debug : Debug;
                session.Enforced.DisableWeaponGridLimits = !unPackedData.DisableWeaponGridLimits.Equals(-1) ? unPackedData.DisableWeaponGridLimits : DisableWeaponGridLimits;
                if (unPackedData.Version < 1)
                {
                    session.Enforced.CapScaler = 0.5f;
                    session.Enforced.HpsEfficiency = 0.5f;
                    session.Enforced.HeatScaler = 0.0065f;
                    session.Enforced.BaseScaler = 10;
                }
                session.Enforced.Version = Version;
                UpdateConfigFile(session, unPackCfg);
            }
            else
            {
                session.Enforced.Version = Version;
                session.Enforced.Debug = Debug;

                WriteNewConfigFile(session);

                Log.Line($"wrote new config file - file exists: {MyAPIGateway.Utilities.FileExistsInGlobalStorage("DefenseShields.cfg")}");
            }
        }

        public static void ReadConfigFile(Session session)
        {
            var dsCfgExists = MyAPIGateway.Utilities.FileExistsInGlobalStorage("WeaponCore.cfg");

            if (session.Enforced.Debug == 3) Log.Line($"Reading config, file exists? {dsCfgExists}");

            if (!dsCfgExists) return;

            var cfg = MyAPIGateway.Utilities.ReadFileInGlobalStorage("WeaponCore.cfg");
            var data = MyAPIGateway.Utilities.SerializeFromXML<DefenseShieldsEnforcement>(cfg.ReadToEnd());
            session.Enforced = data;

            if (session.Enforced.Debug == 3) Log.Line($"Writing settings to mod:\n{data}");
        }

        private static void UpdateConfigFile(Session session, TextReader unPackCfg)
        {
            unPackCfg.Close();
            unPackCfg.Dispose();
            MyAPIGateway.Utilities.DeleteFileInGlobalStorage("WeaponCore.cfg");
            var newCfg = MyAPIGateway.Utilities.WriteFileInGlobalStorage("WeaponCore.cfg");
            var newData = MyAPIGateway.Utilities.SerializeToXML(Session.Enforced);
            newCfg.Write(newData);
            newCfg.Flush();
            newCfg.Close();
            Log.Line($"wrote modified config file - file exists: {MyAPIGateway.Utilities.FileExistsInGlobalStorage("WeaponCore.cfg")}");
        }

        private static void WriteNewConfigFile(Session session)
        {
            var cfg = MyAPIGateway.Utilities.WriteFileInGlobalStorage("WeaponCore.cfg");
            var data = MyAPIGateway.Utilities.SerializeToXML(session.Enforced);
            cfg.Write(data);
            cfg.Flush();
            cfg.Close();
        }
    */
    }
}
