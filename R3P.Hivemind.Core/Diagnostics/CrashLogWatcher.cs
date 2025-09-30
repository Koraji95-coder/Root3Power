using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace R3P.Hivemind.Core.Diagnostics
{
    public static class CrashLogWatcher
    {
        private static readonly object Sync = new();
        private static readonly List<FileSystemWatcher> Watchers = new();
        private static bool _initialized;
        private static readonly string[] InterestedExtensions = { ".json", ".xml", ".zip" };

        public static void Start()
        {
            lock (Sync)
            {
                if (_initialized) return;
                CrashLogStore.Initialize();

                foreach (var dir in DiscoverCrashDirectories())
                {
                    try
                    {
                        var watcher = new FileSystemWatcher(dir)
                        {
                            IncludeSubdirectories = true,
                            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                            Filter = "*.*"
                        };
                        watcher.Created += HandleFileEvent;
                        watcher.Changed += HandleFileEvent;
                        watcher.EnableRaisingEvents = true;
                        Watchers.Add(watcher);
                        CrashLogStore.Append(new CrashLogEntry
                        {
                            TimestampUtc = DateTime.UtcNow,
                            Summary = $"Watching crash folder: {dir}"
                        });
                    }
                    catch (System.Exception ex)
                    {
                        CrashLogStore.Append(new CrashLogEntry
                        {
                            TimestampUtc = DateTime.UtcNow,
                            Summary = $"Crash watcher failed to monitor {dir}",
                            Detail = ex.Message
                        });
                    }
                }

                _initialized = true;
            }
        }

        public static void Stop()
        {
            lock (Sync)
            {
                foreach (var watcher in Watchers)
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Created -= HandleFileEvent;
                    watcher.Changed -= HandleFileEvent;
                    watcher.Dispose();
                }
                Watchers.Clear();
                _initialized = false;
            }
        }

        private static void HandleFileEvent(object sender, FileSystemEventArgs e)
        {
            if (!IsCandidate(e.FullPath)) return;
            Task.Run(() => ProcessFileAsync(e.FullPath));
        }

        private static bool IsCandidate(string path)
        {
            var ext = Path.GetExtension(path);
            return InterestedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
        }

        private static async Task ProcessFileAsync(string path)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    if (!File.Exists(path)) return;
                    CrashLogEntry entry = null;
                    var ext = Path.GetExtension(path).ToLowerInvariant();
                    switch (ext)
                    {
                        case ".json":
                            entry = ParseJson(path);
                            break;
                        case ".xml":
                            entry = ParseXml(path);
                            break;
                        case ".zip":
                            entry = ParseZip(path);
                            break;
                    }

                    if (entry != null)
                    {
                        entry.SourcePath = path;
                        CrashLogStore.Append(entry);
                    }
                    return;
                }
                catch (IOException)
                {
                    await Task.Delay(200);
                }
                catch (UnauthorizedAccessException)
                {
                    await Task.Delay(200);
                }
                catch (System.Exception ex)
                {
                    CrashLogStore.Append(new CrashLogEntry
                    {
                        TimestampUtc = DateTime.UtcNow,
                        Summary = $"Failed to parse crash artifact {Path.GetFileName(path)}",
                        Detail = ex.ToString()
                    });
                    return;
                }
            }
        }

        private static CrashLogEntry ParseJson(string path)
        {
            using var stream = OpenReadShared(path);
            if (stream == null) return null;
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            var timestamp = root.TryGetProperty("CrashTime", out var crashTime) && crashTime.TryGetDateTime(out var dt)
                ? dt.ToUniversalTime()
                : DateTime.UtcNow;
            string summary = TryGetString(root, "AppName") ?? "AutoCAD crash";
            string module = TryGetString(root, "FaultModule");
            string exception = TryGetString(root, "ExceptionCode");
            string detail = TryGetString(root, "StackTrace") ?? root.ToString();

            return new CrashLogEntry
            {
                TimestampUtc = timestamp,
                Summary = summary,
                FaultModule = module,
                ExceptionCode = exception,
                Detail = detail
            };
        }

        private static CrashLogEntry ParseXml(string path)
        {
            using var stream = OpenReadShared(path);
            if (stream == null) return null;
            var doc = XDocument.Load(stream);
            var root = doc.Root;
            if (root == null) return null;
            var summary = root.Element("Description")?.Value ?? "AutoCAD crash";
            var module = root.Element("FaultModule")?.Value;
            var code = root.Element("ExceptionCode")?.Value;
            var timeStr = root.Element("CrashTime")?.Value;
            DateTime.TryParse(timeStr, out var time);
            var details = root.ToString();
            return new CrashLogEntry
            {
                TimestampUtc = time == default ? DateTime.UtcNow : DateTime.SpecifyKind(time, DateTimeKind.Local).ToUniversalTime(),
                Summary = summary,
                FaultModule = module,
                ExceptionCode = code,
                Detail = details
            };
        }

        private static CrashLogEntry ParseZip(string path)
        {
            using var archive = ZipFile.OpenRead(path);
            var entry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith("info.json", StringComparison.OrdinalIgnoreCase));
            if (entry != null)
            {
                using var entryStream = entry.Open();
                using var ms = new MemoryStream();
                entryStream.CopyTo(ms);
                ms.Position = 0;
                using var doc = JsonDocument.Parse(ms);
                var root = doc.RootElement;
                return new CrashLogEntry
                {
                    TimestampUtc = root.TryGetProperty("CrashTime", out var crashTime) && crashTime.TryGetDateTime(out var dt)
                        ? dt.ToUniversalTime()
                        : DateTime.UtcNow,
                    Summary = TryGetString(root, "AppName") ?? "AutoCAD crash (zip)",
                    FaultModule = TryGetString(root, "FaultModule"),
                    ExceptionCode = TryGetString(root, "ExceptionCode"),
                    Detail = root.ToString()
                };
            }
            return new CrashLogEntry
            {
                TimestampUtc = DateTime.UtcNow,
                Summary = $"Crash archive created: {Path.GetFileName(path)}"
            };
        }

        private static IEnumerable<string> DiscoverCrashDirectories()
        {
            var paths = new List<string>();
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var autocadRoot = Path.Combine(localAppData, "Autodesk");
            if (!Directory.Exists(autocadRoot)) return paths;

            foreach (var dir in Directory.EnumerateDirectories(autocadRoot, "AutoCAD*"))
            {
                try
                {
                    foreach (var sub in Directory.EnumerateDirectories(dir))
                    {
                        var crashDir = Path.Combine(sub, "CrashDumps");
                        if (Directory.Exists(crashDir))
                        {
                            paths.Add(crashDir);
                        }
                    }
                }
                catch
                {
                }
            }

            return paths;
        }

        private static Stream OpenReadShared(string path)
        {
            try
            {
                return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            }
            catch
            {
                return null;
            }
        }

        private static string TryGetString(JsonElement element, string name)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
            return null;
        }
    }
}
