using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;

namespace R3P.Hivemind.Core.Features.Conduit.Model
{
    public class ConduitItem
    {
        public ObjectId Id { get; set; }
        public string Handle { get; set; }
        public string Tag { get; set; }
        public double Raw { get; set; }
        public double Adjusted { get; set; }
        public string FtIn { get; set; }
        public string Hint { get; set; }
        public override string ToString()
            => $"{Tag} | {FtIn}  [{Adjusted.ToString("0.###", CultureInfo.InvariantCulture)}]  <{Handle}>" + (string.IsNullOrEmpty(Hint) ? "" : $"  ({Hint})");
    }
}



