using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using HarmonyLib;
using KPatcher.Properties;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable RedundantAssignment

namespace KPatcher.Patches
{
    //Can be used to prevent the app from crashing when clicking on UserName,
    //but this would open browser pages with a URL containing our precious data
    //System.String Krisp.AppHelper.JWTHelper::GenerateToken(System.String,System.String,System.String,System.Boolean)
    //[MethodRequired("Krisp.AppHelper.JWTHelper", "GenerateToken", "JWTHelper::GenerateToken method patch")]
    //[HarmonyPatch]
    //public class JWTHelper_GenerateTokenPatch
    //{
    //    [MethodImpl(MethodImplOptions.NoInlining)]
    //    public static MethodBase TargetMethod() => R.M[0];
    //    public static bool Prefix(ref string __result, string installationID, string sessionID, string secret, bool strong)
    //    {
    //        __result = string.Empty; //To fix crashes while the user is doing something weird
    //        return false;
    //    }
    //}

    /// <summary>
    /// Used to pass custom debug level to the target app loggers
    /// </summary>
    //System.Void Krisp.AppHelper.LogWrapper::Init(System.String,Shared.Interops.SafeNativeMethods/P7/Traces/Level,System.Int32)
    [MethodRequired("Krisp.AppHelper.LogWrapper", "Init", "LogWrapper:Init method patch")]
    [HarmonyPatch]
    public class LogWrapper_InitPatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => R.M[0];
        public static bool Prefix(string logFolder, ref LogLevel logLevel, int logMaxFileCount)
        {
            logLevel = (LogLevel)Settings.Default.DebugLevel;
            return true;
        }
    }

    /// <summary>
    /// Used to hook the logging of critical exception inside the target app
    /// </summary>
    //System.Void Krisp.App.KrispApp::LogToEventViewer(System.Exception,System.String)
    [MethodRequired("Krisp.App.KrispApp", "LogToEventViewer", "KrispApp:LogToEventViewer method patch")]
    [HarmonyPatch]
    public class KrispApp_LogToEventViewerPatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => R.M[0];
        public static bool Prefix(Exception ex, string description)
        {
            Console.WriteLine("Critical exception: \r\n{0}\r\nDescription:\r\n{1}", ex, description ?? "No description");
            //We'll output the exception to the console, so there's no need to write it to system's event log
            return false;
        }
    }

    /// <summary>
    /// Replaces path to loader with path to the target binary to prevent bugs from happening
    /// </summary>
    //System.String Shared.Helpers.EnvHelper::KrispExeFullPath()
    [PropertyRequired("Shared.Helpers.EnvHelper", "KrispExeFullPath", "EnvHelper:KrispExeFullPath property patch")]
    [HarmonyPatch]
    public class EnvHelper_KrispExeFullPathPatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => R.P[0].GetMethod;
        public static bool Prefix(ref string __result)
        {
            __result = Program.KPatcherFullPath;
            return false;
        }
    }

    /// <summary>
    /// Used to fix current directory path, to allow the patcher to run from any directory
    /// </summary>
    [HarmonyPatch]
    public class AppDomain_BaseDirectoryPatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => AccessTools.PropertyGetter(typeof(AppDomain), nameof(AppDomain.BaseDirectory));
        public static bool Prefix(object __instance, ref string __result)
        {
            if (__instance != AppDomain.CurrentDomain)
            {
                Console.WriteLine(nameof(AppDomain_BaseDirectoryPatch) + " WRONG DOMAIN");
                return true;
            }
            __result = Program.TargetDirectory;
            return false;
        }
    }

    /// <summary>
    /// Used to customize paths
    /// </summary>
    [HarmonyPatch(typeof(Path), nameof(Path.Combine), typeof(string), typeof(string))]
    public class Path_CombinePatch
    {
        public static bool Prefix(string __result, ref string path1, ref string path2)
        {
            //Console.WriteLine($"Path.Combine:{path1 ?? "NULL"} + {path2 ?? "NULL"}");
            if (Settings.Default.BlockNetwork || Settings.Default.OfflineMode)
            {
                if ((path2?.Contains("app.cache") ?? false) || path2 == "LiteDBData.db") path2 += ".offline";
            }
            if (path2 == "Logs")
                path2 = "LogsSilveIT";
            return true;
        }
    }

    /// <summary>
    /// Used to add custom resources / replace existing
    /// </summary>
    [HarmonyPatch(typeof(ResourceManager), nameof(ResourceManager.GetString), typeof(string), typeof(CultureInfo))]
    public class ResourceManager_GetStringPatch
    {
        public static bool Prefix(object __instance, ref string __result, string name, CultureInfo culture)
        {
            if (!string.IsNullOrEmpty(name) && name[0] == '\0')
            {
                __result = name.Remove(0, 1);
                return false;
            }

            if (name != "WhatsNew") return true;
            __result = "Patcher Settings";
            return false;

        }
    }
}