using Autodesk.AutoCAD.DatabaseServices;

namespace R3P.Hivemind.Features.Conduit.Services
{
    internal static class TagService
    {
        public static string NextTag(Database db, Transaction tr, ConfigService.Config cfg)
        {
            string tag = $"{cfg.Prefix}{cfg.Next}";
            cfg.Next += 1;
            ConfigService.Set(db, cfg);
            return tag;
        }
    }
}



