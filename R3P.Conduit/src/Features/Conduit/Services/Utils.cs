using System;
using Autodesk.AutoCAD.ApplicationServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Autodesk.AutoCAD.DatabaseServices;

namespace R3P.Hivemind.Features.Conduit.Services
{
    internal static class Utils
    {
        public static double CurveLength(Curve c) => c.GetDistanceAtParameter(c.EndParam) - c.GetDistanceAtParameter(c.StartParam);
        public static double ApplyAllowance(double len, double allowPct, double roundInc)
        {
            len *= (1.0 + allowPct / 100.0);
            if (roundInc > 0) len = Math.Round(len / roundInc, MidpointRounding.AwayFromZero) * roundInc;
            return len;
        }

        public static string FormatFtIn(double len)
        {
            int iu = Convert.ToInt32(AcadApp.GetSystemVariable("INSUNITS"));
            double inches = (iu == 1) ? len : (len * 12.0);
            int ft = (int)Math.Floor(inches / 12.0);
            double remIn = inches - ft * 12.0;
            return $"{ft}'-{remIn:0.#}\"";
        }

        public static double ToFeet(double len)
        {
            int iu = Convert.ToInt32(AcadApp.GetSystemVariable("INSUNITS"));
            return (iu == 1) ? (len / 12.0) : len; // inches->feet, else assume feet
        }
    }
}


