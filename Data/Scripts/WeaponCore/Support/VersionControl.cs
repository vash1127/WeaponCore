using System.IO;
using Sandbox.ModAPI;
using WeaponCore.Support;
namespace WeaponCore.Support
{
    internal class VersionControl
    {
        public static void PrepConfigFile(Session session)
        {
            return;
            const int version = 1;
            const int debug = 0;
            const int disableWeaponGridLimits = 0;

            var dsCfgExists = MyAPIGateway.Utilities.FileExistsInGlobalStorage("WeaponCore.cfg");
            if (dsCfgExists)
            {
                var unPackCfg = MyAPIGateway.Utilities.ReadFileInGlobalStorage("WeaponCore.cfg");
                var unPackedData = MyAPIGateway.Utilities.SerializeFromXML<Enforcements>(unPackCfg.ReadToEnd());

                if (unPackedData.Enforcement.Version == version) return;

                session.Enforced.Enforcement.Debug = !unPackedData.Enforcement.Debug.Equals(-1) ? unPackedData.Enforcement.Debug : debug;
                session.Enforced.Enforcement.DisableWeaponGridLimits = !unPackedData.Enforcement.DisableWeaponGridLimits.Equals(-1) ? unPackedData.Enforcement.DisableWeaponGridLimits : disableWeaponGridLimits;
                if (unPackedData.Enforcement.Version < 1)
                {
                }
                session.Enforced.Enforcement.Version = version;
                UpdateConfigFile(session, unPackCfg);
            }
            else
            {
                session.Enforced.Enforcement.Version = version;
                session.Enforced.Enforcement.Debug = debug;

                WriteNewConfigFile(session);

                Log.Line($"wrote new config file - file exists: {MyAPIGateway.Utilities.FileExistsInGlobalStorage("DefenseShields.cfg")}");
            }
        }

        private static void UpdateConfigFile(Session session, TextReader unPackCfg)
        {
            unPackCfg.Close();
            unPackCfg.Dispose();
            MyAPIGateway.Utilities.DeleteFileInGlobalStorage("WeaponCore.cfg");
            var newCfg = MyAPIGateway.Utilities.WriteFileInGlobalStorage("WeaponCore.cfg");
            var newData = MyAPIGateway.Utilities.SerializeToXML(session.Enforced);
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
    }
}
