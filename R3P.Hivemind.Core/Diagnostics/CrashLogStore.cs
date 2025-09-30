using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace R3P.Hivemind.Core.Diagnostics
{
    public static class CrashLogStore
    {
        private static readonly object Sync = new();
        private static readonly List<string> Buffer = new();
        public static event EventHandler<string> LineAppended;

        public static IReadOnlyList<string> GetSnapshot()
        {
            lock (Sync)
            {
                return Buffer.ToArray();
            }
        }

        public static void Initialize()
        {
            lock (Sync)
            {
                Buffer.Clear();
                if (File.Exists(HivemindPaths.CrashLogFile))
                {
                    foreach (var line in File.ReadAllLines(HivemindPaths.CrashLogFile))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        Buffer.Add(line.TrimEnd());
                    }
                }
            }
        }

        public static void Append(CrashLogEntry entry)
        {
            if (entry == null) return;
            var message = entry.ToString();
            lock (Sync)
            {
                Buffer.Add(message);
                File.AppendAllText(HivemindPaths.CrashLogFile, message + Environment.NewLine);
            }
            var handler = LineAppended;
            handler?.Invoke(null, message);
        }
    }
}

