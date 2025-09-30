using System;
using System.IO;

namespace R3P.Hivemind.Core
{
    public static class HivemindPaths
    {
        public static string AppDataRoot
        {
            get
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Root3Power", "Hivemind");
                Directory.CreateDirectory(path);
                return path;
            }
        }

        public static string LogsDirectory
        {
            get
            {
                var path = Path.Combine(AppDataRoot, "logs");
                Directory.CreateDirectory(path);
                return path;
            }
        }

        public static string CrashLogFile => Path.Combine(LogsDirectory, "crash-log.txt");
    }
}
