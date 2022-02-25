using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Windows;
using HarmonyLib;
using KPatcher.Properties;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable RedundantAssignment

namespace KPatcher.Patches
{
    /// <summary>
    /// <para>Should basically remove all web requests (at least it did)</para>
    /// <para>Might break some code which is using Uri's, but currently it works just fine</para>
    /// </summary>
    //System.Void System.Uri::CreateThis(System.String,System.Boolean,System.UriKind)
    [MethodRequired("System.Uri", "CreateThis", "Network blocking patch")]
    [HarmonyPatch]
    public class Uri_CreateThisPatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => R.M[0];
        public static bool Prefix(ref string uri, bool dontEscape, UriKind uriKind)
        {
            if (!Settings.Default.BlockNetwork)
                return true;
            if (!uri.StartsWith("http:") && !uri.StartsWith("https:") ||
                uri.StartsWith("http://defaultcontainer") || //Trying to not shoot in the leg...
                uri.StartsWith("http://foo/") ||
                uri.StartsWith("http://schemas.") ||
                uri.StartsWith("http://0.0.0.0")) //Remove to see if something is trying to create already patched URLs
                return true;
            Console.WriteLine("URL creation blocked: " + uri);
            uri = "http://0.0.0.0";
            return true;
        }
    }

    /// <summary>
    /// Used to filter requests which were not filtered by the hooks
    /// </summary>
    //RestSharp.IHttp RestSharp.RestClient::ConfigureHttp(RestSharp.IRestRequest)
    [PropertyRequired("RestSharp.IRestRequest", "Resource", "RestClient network filter")]
    [PropertyRequired("RestSharp.IRestRequest", "Parameters", "RestClient network filter")]
    [PropertyRequired("RestSharp.Parameter", "Name", "RestClient network filter")]
    [MethodRequired("RestSharp.RestClient", "ConfigureHttp", "RestClient network filter")]
    [HarmonyPatch]
    public class RestClient_ConfigureHttpPatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => R.M[0];
        public static bool Prefix(ref object request)
        {
            //To debug request details
            //var serializeOptions = new JsonSerializerOptions
            //{
            //    WriteIndented = true,
            //    ReferenceHandler = ReferenceHandler.Preserve,
            //    IgnoreReadOnlyProperties = true
            //};
            //Console.WriteLine(JsonSerializer.Serialize(request, serializeOptions));

            if (Settings.Default.BlockNetwork) return false;

            var url = (string)R.P[0].GetValue(request);
            var pass = false;
            Console.WriteLine("Requested: " + url);
            if (url.StartsWith("/version/"))
                pass = Settings.Default.EnableUpdates;
            else if (url.StartsWith("resource")) 
                pass = true;
            else if (url.StartsWith("/report/problem") || HasDebugInfo(request))
            {
                var res = Utils.ShowDialog("Are you sure?",
                    "You are about to upload your personal data to the external web servers.\r\n" +
                    "Please proceed only when you are 100% sure of what you are doing!",
                    "Cancel", "Proceed anyway");
                pass = res == MessageBoxResult.OK;
            }
            else if (url.StartsWith("/auth/logout") || url.StartsWith("/notification"))
                pass = !Settings.Default.OfflineMode;
            else if (url.StartsWith("/user/minutes") || url.StartsWith("/auth/token"))
            {
                if (Settings.Default.OfflineMode)
                {
                    //WTF? Should not be possible
                    throw new Exception("Something is wrong with patching, got an online request while in Offline Mode!");
                }
                pass = true;
            }
            else
                pass = !Settings.Default.OfflineMode && Settings.Default.PassUnknownRequests;
            Console.WriteLine((pass ? "Passing request: " : "Request was blocked: ") + url);
            return pass;

            bool HasDebugInfo(object req)
            {
                var prms = (IList)R.P[1].GetValue(req);
                return prms.Cast<object>().Any(prm => (string)R.P[2].GetValue(prm) == "application/zip");
            }
        }
    }

    #region RestSharp.IRestResponse`1<Krisp.BackEnd.KrispSDKResponse`1<T>> Krisp.BackEnd.KrispAwsSDK::DoRequest<T>(Krisp.BackEnd.RequestInfo)

    /// <summary>
    /// Replaces InstallationInfo synchronization logic when offline
    /// </summary>
    [TypeRequired("Krisp.BackEnd.InstallationInfo", "KrispAwsSDK:DoRequest<InstallationInfo> method patch")]
    [MethodRequired("Krisp.BackEnd.KrispAwsSDK", "DoRequest", "KrispAwsSDK:DoRequest<InstallationInfo> method patch")]
    [HarmonyPatch]
    public class DoRequestInstallationInfoPatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => R.M[0].MakeGenericMethod(R.T[0]);
        public static bool Prefix(ref object __result, object requestInfo)
        {
            if (!Settings.Default.BlockNetwork && !Settings.Default.OfflineMode) 
                return true;
            __result = null;
            try
            {
                Console.WriteLine("Requested InstallationInfo sync");
                (__result, _) = Utils.InstallationInfoGenerator.GenerateInstallationResponseREST(requestInfo);
                Console.WriteLine("Generated InstallationInfo sync");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return false;
        }
    }

    #endregion

    #region System.Void Krisp.BackEnd.KrispAwsSDK::DoAsyncRequest<T>(Krisp.BackEnd.RequestInfo,System.Action`2<Krisp.BackEnd.KrispSDKResponse`1<T>,System.Object>,System.Threading.CancellationToken)

    /// <summary>
    /// Replaces InstallationInfo synchronization logic when offline
    /// </summary>
    [TypeRequired("Krisp.BackEnd.InstallationInfo", "KrispAwsSDK:DoAsyncRequest<Krisp.BackEnd.InstallationInfo> method patch")]
    [MethodRequired("Krisp.BackEnd.KrispAwsSDK", "DoAsyncRequest", "KrispAwsSDK:DoAsyncRequest<Krisp.BackEnd.InstallationInfo> method patch")]
    [HarmonyPatch]
    public class DoAsyncRequestInstallationInfoPatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => R.M[0].MakeGenericMethod(R.T[0]);
        public static bool Prefix(object requestInfo, Action<object, object> callback, CancellationToken cancellationToken = default)
        {
            if (!Settings.Default.BlockNetwork && !Settings.Default.OfflineMode) 
                return true;
            try
            {
                Console.WriteLine("Requested InstallationInfo async");
                var (instResp, _) = Utils.InstallationInfoGenerator.GenerateInstallationResponseKrisp(requestInfo);
                Console.WriteLine("Generated InstallationInfo async, calling callback...");
                callback(instResp, null);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return false;
        }
    }

    /// <summary>
    /// VersionInfo request patches
    /// </summary>
    [TypeRequired("Krisp.BackEnd.VersionInfo", "KrispAwsSDK:DoAsyncRequest<VersionInfo> method patch")]
    [PropertyRequired("Krisp.BackEnd.VersionRequestInfo", "endpoint", "KrispAwsSDK:DoAsyncRequest<VersionInfo> method patch")]
    [MethodRequired("Krisp.BackEnd.KrispAwsSDK", "DoAsyncRequest", "KrispAwsSDK:DoAsyncRequest<VersionInfo> method patch")]
    [HarmonyPatch]
    public class DoAsyncRequestVersionInfoPatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => R.M[0].MakeGenericMethod(R.T[0]);
        public static bool Prefix(ref object requestInfo, Action<object, object> callback, CancellationToken cancellationToken = default)
        {
            //Won't allow any requests with excess info
            if (Settings.Default.BlockNetwork || !Settings.Default.EnableUpdates || JsonSerializer.Serialize(requestInfo).Length > 120)
                return false;

            var verReqInfoEndpointProp = R.P[0];
            var endpoint = (string)verReqInfoEndpointProp.GetValue(requestInfo);
            //It's kinda not official yet translation xD TODO: to be removed in future
            var endpointPatched = endpoint.Replace("ru-RU", "en-US");
            verReqInfoEndpointProp.SetValue(requestInfo, endpointPatched);
            return true;
        }
    }

    [MethodRequired("Krisp.BackEnd.KrispAwsSDK", "DoAsyncRequest", "KrispAwsSDK:DoAsyncRequest<UserProfileInfo> method reverse patch")]
    [HarmonyReversePatch]
    public class DoAsyncRequestUserProfileInfoReversePatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => R.M[0].MakeGenericMethod(R.T[0]);
        public static string DoRequestUserProfileInfo(object instance, object requestInfo, Action<object, object> callback, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("It's a stub");
        }
    }

    /// <summary>
    /// Used to patch user account details
    /// </summary>
    [TypeRequired("Krisp.BackEnd.UserProfileInfo", "KrispAwsSDK:DoAsyncRequest<UserProfileInfo> method patch")]
    [MethodRequired("Krisp.BackEnd.KrispAwsSDK", "DoAsyncRequest", "KrispAwsSDK:DoAsyncRequest<UserProfileInfo> method patch")]
    [HarmonyPatch]
    public class DoAsyncRequestUserProfileInfoPatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => R.M[0].MakeGenericMethod(R.T[0]);
        public static bool Prefix(object __instance, object requestInfo, Action<object, object> callback, CancellationToken cancellationToken = default)
        {
            if (!Settings.Default.BlockNetwork && !Settings.Default.OfflineMode)
            {
                if (!Settings.Default.PatchLicense) return true;
                DoAsyncRequestUserProfileInfoReversePatch.DoRequestUserProfileInfo(__instance, requestInfo, (response, error) =>
                {
                    if (error == null)
                        Utils.UserProfileInfoGenerator.PatchUserInfoKrisp(response);
                    else
                        Console.WriteLine("Something went wrong while requesting UserProfileInfo, passing without modification");
                    callback(response, error);
                }, cancellationToken);
                return false;
            }
            Console.WriteLine("Requested UserProfileInfo async");
            var (instResp, _) = Utils.UserProfileInfoGenerator.GenerateUserProfileInfoKrisp(Settings.Default.EnableUpdates);
            Console.WriteLine("Generated UserProfileInfo async, calling callback...");
            callback(instResp, null);
            return false;
        }
    }

    /// <summary>
    /// This patch is required to check if everything works fine
    /// </summary>
    [TypeRequired("Krisp.BackEnd.NotificationInfo", "KrispAwsSDK:DoAsyncRequest<NotificationInfo> method patch")]
    [TypeRequired("Krisp.BackEnd.NotificationResponse", "KrispAwsSDK:DoAsyncRequest<NotificationInfo> method patch")]
    [PropertyRequired("Krisp.BackEnd.NotificationInfo", "ref_string", "KrispAwsSDK:DoAsyncRequest<NotificationInfo> method patch")]
    [PropertyRequired("Krisp.BackEnd.NotificationInfo", "notifications", "KrispAwsSDK:DoAsyncRequest<NotificationInfo> method patch")]
    [MethodRequired("Krisp.BackEnd.KrispAwsSDK", "DoAsyncRequest", "KrispAwsSDK:DoAsyncRequest<NotificationInfo> method patch")]
    [HarmonyPatch]
    public class DoAsyncRequestNotificationInfoPatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => R.M[0].MakeGenericMethod(R.T[0]);
        public static bool Prefix(object requestInfo, Action<object, object> callback, CancellationToken cancellationToken = default)
        {
            if (Settings.Default.BlockNetwork || Settings.Default.OfflineMode)
            {
                Console.WriteLine("Requested NotificationInfo async");
                var notificationInfo = Activator.CreateInstance(R.T[0]);
                var notificationResponseList = Activator.CreateInstance(typeof(List<>).MakeGenericType(R.T[1]));
                R.P[0].SetValue(notificationInfo, "SilveIT");
                R.P[1].SetValue(notificationInfo, notificationResponseList);
                Console.WriteLine("Generated NotificationInfo async, calling callback...");
                callback(notificationInfo, null);
                return false;
            }
            return true;
        }
    }


    [TypeRequired("Krisp.BackEnd.AppTokenInfo", "KrispAwsSDK:DoAsyncRequest<AppTokenInfo> method patch")]
    [MethodRequired("Krisp.BackEnd.KrispAwsSDK", "DoAsyncRequest", "KrispAwsSDK:DoAsyncRequest<AppTokenInfo> method patch")]
    [HarmonyPatch]
    public class DoAsyncRequestAppTokenInfoPatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => R.M[0].MakeGenericMethod(R.T[0]);
        public static bool Prefix(object requestInfo, Action<object, object> callback, CancellationToken cancellationToken = default)
        {
            if (Settings.Default.BlockNetwork || Settings.Default.OfflineMode)
            {
                //WTF? Should not be possible
                throw new Exception("Something is wrong with patching, got an AppTokenInfo request!");
            }
            return true;
        }
    }

    #endregion
}