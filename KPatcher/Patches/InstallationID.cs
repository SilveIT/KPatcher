using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using KPatcher.Properties;
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace KPatcher.Patches
{
    /// <summary>
    /// Patches HWID property
    /// </summary>
    //System.String Shared.Helpers.InstallationID::get_HWID()
    [PropertyRequired("Shared.Helpers.InstallationID", "HWID", "InstallationID:HWID property patch")]
    [HarmonyPatch]
    public class InstallationID_HWIDPatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => R.P[0].GetMethod;
        public static bool Prefix(ref string __result)
        {
            if (!Settings.Default.BlockNetwork && !Settings.Default.OfflineMode)
                return true;

            __result = Utils.GetRandomHWID();

            return false;
        }
    }

    /// <summary>
    /// Patches HardwareIdentifier property
    /// </summary>
    //System.String Shared.Helpers.InstallationID::get_HardwareIdentifier()
    [PropertyRequired("Shared.Helpers.InstallationID", "HardwareIdentifier", "InstallationID:HardwareIdentifier property patch")]
    [HarmonyPatch]
    public class InstallationID_HardwareIdentifierPatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => R.P[0].GetMethod;
        public static bool Prefix(ref string __result)
        {
            if (!Settings.Default.BlockNetwork && !Settings.Default.OfflineMode)
                return true;

            __result = Utils.GetRandomHardwareIdentifier();

            return false;
        }
    }
}