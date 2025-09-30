using System;

namespace R3P.Hivemind.Core.Diagnostics
{
    public sealed class CrashLogEntry
    {
        public DateTime TimestampUtc { get; set; }
        public string SourcePath { get; set; } = string.Empty;
        public string Summary { get; set; } = "AutoCAD crash detected";
        public string? FaultModule { get; set; }
        public string? ExceptionCode { get; set; }
        public string? Detail { get; set; }

        public override string ToString()
        {
            var time = TimestampUtc == default ? DateTime.UtcNow : TimestampUtc;
            var module = string.IsNullOrWhiteSpace(FaultModule) ? string.Empty : $" | Module: {FaultModule}";
            var code = string.IsNullOrWhiteSpace(ExceptionCode) ? string.Empty : $" | Code: {ExceptionCode}";
            var summary = string.IsNullOrWhiteSpace(Summary) ? "AutoCAD crash detected" : Summary;
            return $"[{time:u}] {summary}{module}{code}";
        }
    }
}
