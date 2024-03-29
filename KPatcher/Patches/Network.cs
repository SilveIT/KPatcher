﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
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
            if (uri.Contains("sentry.io"))
            {
                Console.WriteLine("Sentry URL blocked");
                uri = "https://SilveIT@0.0.0.0/1337";
                return true;
            }
            if (!Settings.Default.BlockNetwork)
                return true;
            if (!uri.StartsWith("http:") && !uri.StartsWith("https:") ||
                uri.StartsWith("http://defaultcontainer") || //Trying to not shoot in the leg...
                uri.StartsWith("http://foo/") ||
                uri.StartsWith("http://schemas.") ||
                uri.StartsWith("http://0.0.0.0") ||
                uri.StartsWith("https://0.0.0.0") ||
                uri.StartsWith("https://SilveIT@0.0.0.0")) //Remove to see if something is trying to create already patched URLs
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
            var serializeOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                ReferenceHandler = ReferenceHandler.Preserve,
                IgnoreReadOnlyProperties = true
            };
            var jsonRequest = JsonSerializer.Serialize(request, serializeOptions);
            //Console.WriteLine(jsonRequest);

            if (Settings.Default.BlockNetwork) return false;

            var url = (string)R.P[0].GetValue(request);
            bool pass;
            Console.WriteLine("Requested: " + url);
            if (jsonRequest.Contains("Bearer"))
            {
                pass = false;
                Console.WriteLine("Request contains authorization, blocking...");
            }
            else if (url.StartsWith("/version/")) //TODO fix update system
                pass = Settings.Default.EnableUpdates;
            else if (url.StartsWith("resource") || url.Contains("headset_lists")) 
                pass = true;
            else if (url.StartsWith("/report/problem") || jsonRequest.Contains("application/zip"))
                pass = false;
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
        }
    }

    #region RestSharp.IRestResponse`1<Krisp.BackEnd.KrispSDKResponse`1<T>> Krisp.BackEnd.KrispWebClient::DoRequest<T>(Krisp.BackEnd.RequestInfo)

    /// <summary>
    /// Replaces InstallationInfo synchronization logic when offline
    /// </summary>
    [TypeRequired("Krisp.BackEnd.InstallationInfo", "IKrispWebClient:DoRequest<InstallationInfo> method patch")]
    [MethodRequired("Krisp.BackEnd.KrispWebClient", "DoRequest", "IKrispWebClient:DoRequest<InstallationInfo> method patch")]
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

    #region System.Void Krisp.BackEnd.KrispWebClient::DoAsyncRequest<T>(Krisp.BackEnd.RequestInfo,System.Action`2<Krisp.BackEnd.KrispSDKResponse`1<T>,System.Object>,System.Threading.CancellationToken)

    /// <summary>
    /// Replaces InstallationInfo synchronization logic when offline
    /// </summary>
    [TypeRequired("Krisp.BackEnd.InstallationInfo", "IKrispWebClient:DoAsyncRequest<Krisp.BackEnd.InstallationInfo> method patch")]
    [MethodRequired("Krisp.BackEnd.KrispWebClient", "DoAsyncRequest", "IKrispWebClient:DoAsyncRequest<Krisp.BackEnd.InstallationInfo> method patch")]
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
    [TypeRequired("Krisp.BackEnd.VersionInfo", "IKrispWebClient:DoAsyncRequest<VersionInfo> method patch")]
    [PropertyRequired("Krisp.BackEnd.VersionRequestInfo", "endpoint", "IKrispWebClient:DoAsyncRequest<VersionInfo> method patch")]
    [MethodRequired("Krisp.BackEnd.KrispWebClient", "DoAsyncRequest", "IKrispWebClient:DoAsyncRequest<VersionInfo> method patch")]
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

    [MethodRequired("Krisp.BackEnd.KrispWebClient", "DoAsyncRequest", "IKrispWebClient:DoAsyncRequest<UserProfileInfo> method reverse patch")]
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
    [TypeRequired("Krisp.BackEnd.UserProfileInfo", "IKrispWebClient:DoAsyncRequest<UserProfileInfo> method patch")]
    [MethodRequired("Krisp.BackEnd.KrispWebClient", "DoAsyncRequest", "IKrispWebClient:DoAsyncRequest<UserProfileInfo> method patch")]
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
    [TypeRequired("Krisp.BackEnd.NotificationInfo", "IKrispWebClient:DoAsyncRequest<NotificationInfo> method patch")]
    [TypeRequired("Krisp.BackEnd.NotificationResponse", "IKrispWebClient:DoAsyncRequest<NotificationInfo> method patch")]
    [PropertyRequired("Krisp.BackEnd.NotificationInfo", "ref_string", "IKrispWebClient:DoAsyncRequest<NotificationInfo> method patch")]
    [PropertyRequired("Krisp.BackEnd.NotificationInfo", "notifications", "IKrispWebClient:DoAsyncRequest<NotificationInfo> method patch")]
    [MethodRequired("Krisp.BackEnd.KrispWebClient", "DoAsyncRequest", "IKrispWebClient:DoAsyncRequest<NotificationInfo> method patch")]
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


    [TypeRequired("Krisp.BackEnd.AppTokenInfo", "IKrispWebClient:DoAsyncRequest<AppTokenInfo> method patch")]
    [MethodRequired("Krisp.BackEnd.KrispWebClient", "DoAsyncRequest", "IKrispWebClient:DoAsyncRequest<AppTokenInfo> method patch")]
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