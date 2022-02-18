using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using KPatcher.Properties;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable RedundantAssignment

namespace KPatcher.Patches
{
    /// <summary>
    /// Blocks NoInternetConnection state when not online
    /// </summary>
    //System.Void Krisp.BackEnd.AccountManager::SetState(Krisp.Models.AccountManagerState,Krisp.Models.AccountManagerErrorCode)
    [TypeRequired("Krisp.Models.AccountManagerState", "AccountManager:SetState method patch")]
    [TypeRequired("Krisp.Models.AccountManagerErrorCode", "AccountManager:SetState method patch")]
    [MethodRequired("Krisp.BackEnd.AccountManager", "SetState", "AccountManager:SetState method patch")]
    [HarmonyPatch]
    public class AccountManager_SetStatePatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => R.M[0];
        public static bool Prefix(ref int state, int errorCode)
        {
            var stateType = R.T[0];
            var errorCodeType = R.T[1];
            var stateName = Enum.GetName(stateType, state);
            var errorCodeName = Enum.GetName(errorCodeType, errorCode);
            Console.WriteLine("AccountManager state: " + stateName + "; errorCode: " + errorCodeName);
            if (Settings.Default.BlockNetwork || Settings.Default.OfflineMode && stateName == "NoInternetConnection")
                state = 0; //LoggedIn
            return true;
        }
    }

    /// <summary>
    /// Used to patch base account details to let the app start in offline environment without patching useless requests
    /// </summary>
    [MethodRequired("Krisp.BackEnd.AccountManager", "ObtainDataFromCache", "AccountManager:ObtainDataFromCache method patch")]
    [FieldRequired("Krisp.BackEnd.AccountManager", "_data", "AccountManager:ObtainDataFromCache method patch")]
    [PropertyRequired("Krisp.BackEnd.AccountManager+CachingData", "AppToken", "AccountManager:ObtainDataFromCache method patch")]
    [PropertyRequired("Krisp.BackEnd.AccountManager+CachingData", "SessionID", "AccountManager:ObtainDataFromCache method patch")]
    [MethodRequired("Krisp.BackEnd.AccountManager", "UpdateCache", "AccountManager:ObtainDataFromCache method patch")]
    [HarmonyPatch]
    public class AccountManager_ObtainDataFromCachePatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => R.M[0];
        public static bool Prefix(object __instance)
        {
            if (!Settings.Default.BlockNetwork && !Settings.Default.OfflineMode)
                return true;

            var dataFld = R.F[0];
            var cache = dataFld.GetValue(__instance);
            var cacheAppTokenProp = R.P[0];
            var cacheSessionIDProp = R.P[1];
            cacheAppTokenProp.SetValue(cache, "SilveIT");
            cacheSessionIDProp.SetValue(cache, "SilveIT");
            R.M[1].Invoke(__instance, null);
            Console.WriteLine("Patched AppToken and SessionID");

            return false;
        }
    }

    [MethodRequired("Krisp.BackEnd.AccountManager", "LogoutAsync", "AccountManager:LogoutAsync method patch")]
    [HarmonyPatch]
    public class AccountManager_LogoutAsyncPatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => R.M[0];
        public static bool Prefix()
        {
            if (!Settings.Default.BlockNetwork && !Settings.Default.OfflineMode)
                return true;

            Console.WriteLine("Preventing logging out");
            return false;
        }
    }
}