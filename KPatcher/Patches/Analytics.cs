using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable RedundantAssignment

namespace KPatcher.Patches
{
    //These patches are used to block all the telemetry inside target app
    //I guess the chance of leaking any information is quite low now :)

    //System.Void Shared.Interops.SafeNativeMethods+P7+Telemetry::.ctor(Shared.Interops.SafeNativeMethods/P7/Client,System.String)
    [TypeRequired("Shared.Interops.SafeNativeMethods+P7+Telemetry", "P7 telemetry patch")]
    [HarmonyPatch]
    public class P7Telemetry_P7_Telemetry_CreatePatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => R.C(R.T[0], o => o.GetParameters().Length == 2);
        public static bool Prefix(ref object __instance)
        {
            //Replacing public constructor with the private one which doesn't call P7's telemetry creation
            __instance = R.C(R.T[0], new[] { typeof(IntPtr) }).Invoke(new object[] { IntPtr.Zero });
            return false;
        }
    }

    //System.Void Shared.Analytics.AnalyticsClient::.ctor(System.String,System.String)
    [TypeRequired("Shared.Analytics.AnalyticsClient", "AnalyticsClient constructor patch")]
    [HarmonyPatch]
    public class AnalyticsClient_CtorPatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => R.C(R.T[0], new[] { typeof(string), typeof(string) });
        public static bool Prefix(ref string aUrl, string stoken)
        {
            aUrl = "http://0.0.0.0/NOMOREANALYTYCS";
            return true;
        }
    }

    [TypeRequired("Krisp.Analytics.AnalyticsManager", "AnalyticsManager constructor patch")]
    [HarmonyPatch]
    public class AnalyticsManager_CctorPatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => R.C(R.T[0], o => o.GetParameters().Length == 3);

        public static bool Prefix(ref object __instance)
        {
            __instance = new object();
            return false;
        }
    }

    [MethodRequired("Krisp.Analytics.AnalyticsManager", "Report", "AnalyticsManager:Report method patch")]
    [HarmonyPatch]
    public class AnalyticsManager_ReportPatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => R.M[0];
        public static bool Prefix(ref bool __result, dynamic aEvent)
        {
            Console.WriteLine("Reported event: " + aEvent.name);
            __result = true;
            return false;
        }
    }

    [MethodRequired("Krisp.Analytics.AnalyticsManager", "LogEvent", "AnalyticsManager:LogEvent method patch")]
    [HarmonyPatch]
    public class AnalyticsManager_LogEventPatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => R.M[0];
        public static bool Prefix(object aEvent)
        {
            Console.WriteLine(nameof(AnalyticsManager_LogEventPatch));
            return false;
        }
    }

    [MethodRequired("Krisp.Analytics.AnalyticsManager", "Pause", "AnalyticsManager:Pause method patch")]
    [HarmonyPatch]
    public class AnalyticsManager_PausePatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => R.M[0];
        public static bool Prefix()
        {
            Console.WriteLine(nameof(AnalyticsManager_PausePatch));
            return false;
        }
    }

    [MethodRequired("Krisp.Analytics.AnalyticsManager", "Resume", "AnalyticsManager:Resume method patch")]
    [HarmonyPatch]
    public class AnalyticsManager_ResumePatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => R.M[0];
        public static bool Prefix()
        {
            Console.WriteLine(nameof(AnalyticsManager_ResumePatch));
            return false;
        }
    }

    [MethodRequired("Krisp.Analytics.AnayticEventsSender", "Send", "Krisp.Analytics.AnayticEventsSender:Send method patch")]
    [HarmonyPatch]
    public class AnayticEventsSender_SendPatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => R.M[0];
        public static bool Prefix(ref bool __result, object entries)
        {
            Console.WriteLine(nameof(AnayticEventsSender_SendPatch));
            __result = true;
            return false;
        }
    }
}