using System;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;
using System.Runtime.InteropServices;
using System.Windows;
using HarmonyLib;
using Microsoft.Win32;
using Application = System.Windows.Forms.Application;

namespace KPatcher
{
    [TypeRequired("Krisp.BackEnd.KrispSDKResponse`1", "Krisp network response generation")]
    [TypeRequired("RestSharp.RestResponse`1", "Krisp network response generation")]
    [TypeRequired("Krisp.UI.Views.Windows.MessageBox", "Krisp's messagebox usage")]
    [MethodRequired("Krisp.UI.DialogWindowFactory", "CreateDialogWindow", "Krisp's dialog usage")]
    [FieldRequired("Krisp.UI.Views.Windows.DialogWindow", "_result", "Krisp's dialog usage")]
    [PropertyRequired("Krisp.Properties.Resources", "ResourceManager", "Icon loader")]
    public static class Utils
    {
        [DllImport("kernel32.dll")]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);
        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        // ReSharper disable InconsistentNaming
        internal const uint SC_CLOSE = 0xF060;
        internal const uint MF_ENABLED = 0x00000000;
        internal const uint MF_GRAYED = 0x00000001;
        // ReSharper restore InconsistentNaming

        public static void EnableConsoleCloseButton(bool enable) => EnableCloseButton(GetConsoleWindow(), enable);

        public static void EnableCloseButton(IntPtr window, bool bEnabled)
        {
            var hSystemMenu = GetSystemMenu(window, false);
            EnableMenuItem(hSystemMenu, SC_CLOSE, MF_ENABLED | (bEnabled ? MF_ENABLED : MF_GRAYED));
        }

        public static void ShowConsole(bool show) => 
            ShowWindow(GetConsoleWindow(), show ? 5 : 0);

        /// <summary>
        /// Replaces path in the system startup of the target app
        /// </summary>
        /// <returns></returns>
        public static Exception FixStartup()
        {
            try
            {
                RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                if (registryKey == null)
                    throw new NullReferenceException("Registry Run key is null");
                var isInStartup = registryKey.GetValue(Program.TargetName) != null;
                if (isInStartup)
                {
                    Console.WriteLine("Patching original target registry record in system startup");
                    registryKey.DeleteValue(Program.TargetName);
                    registryKey.SetValue(Application.ProductName, Program.KPatcherFullPath);
                }
                //Just to update the path
                registryKey.SetValue(Application.ProductName, "\"" + Program.KPatcherFullPath + "\" -s");
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        /// <summary>
        /// Emulates successful KrispSDKResponse&lt;T&gt; instance, sets response data
        /// </summary>
        /// <param name="dataInstance"></param>
        /// <param name="krispDataType"></param>
        /// <returns></returns>
        public static (object, Type) MakeKrispResponse(object dataInstance, Type krispDataType)
        {
            var krispRespType = R.T[0].MakeGenericType(krispDataType);
            var krispResp = Activator.CreateInstance(krispRespType);
            var krispRespDataProp = AccessTools.Property(krispRespType, "data");
            krispRespDataProp.SetValue(krispResp, dataInstance);
            var krispRespHttpCodeProp = AccessTools.Property(krispRespType, "http_code");
            krispRespHttpCodeProp.SetValue(krispResp, 200);

            return (krispResp, krispRespType);
        }

        /// <summary>
        /// Emulates successful RestResponse&lt;T&gt; instance, sets response data
        /// </summary>
        public static (object, Type) MakeRestResponse(object dataInstance, Type restDataType)
        {
            var restRespType = R.T[1].MakeGenericType(restDataType);
            var restResp = Activator.CreateInstance(restRespType);

            var restRespDataProp = AccessTools.Property(restRespType, "Data");
            restRespDataProp.SetValue(restResp, dataInstance);
            var restRespStatusCodeProp = AccessTools.Property(restRespType, "StatusCode");
            restRespStatusCodeProp.SetValue(restResp, 200);
            var restRespResponseStatusProp = AccessTools.Property(restRespType, "ResponseStatus");
            restRespResponseStatusProp.SetValue(restResp, 1);

            //Won't fill the REST of the response since other fields and props are not used anywhere yet

            return (restResp, restRespType);
        }

        /// <summary>
        /// Wraps around Krisp's Dialog window implementation
        /// </summary>
        /// <returns>OK or Cancel</returns>
        public static MessageBoxResult ShowDialog(string title, string body, string negativeButtonContent, string positiveButtonContent, int ownerType = -1)
        {
            title = "\0" + title;
            body = "\0" + body;
            negativeButtonContent = "\0" + negativeButtonContent;
            positiveButtonContent = "\0" + positiveButtonContent;
            var window = (Window)R.M[0].Invoke(null,
                new object[] { title, body, negativeButtonContent, positiveButtonContent, ownerType });
            window.ShowDialog();
            return (MessageBoxResult)R.F[0].GetValue(window);
        }

        /// <summary>
        /// Wraps around Krisp's MessageBox window implementation
        /// </summary>
        public static void ShowMessageBox(string message) => 
            ((Window)Activator.CreateInstance(R.T[2], message)).ShowDialog();

        /// <summary>
        /// The proper way to find available cultures.
        /// </summary>
        /// <returns>Collection of CultureInfo from available resources</returns>
        public static IEnumerable<CultureInfo> GetAvailableCultures()
        {
            var result = new List<CultureInfo>();

            ResourceManager rm = new ResourceManager("Krisp.Properties.Resources", Program.AssemblyLoader.RequestedAssembly);

            var cultures = CultureInfo.GetCultures(CultureTypes.AllCultures);
            foreach (CultureInfo culture in cultures)
            {
                try
                {
                    if (culture.Equals(CultureInfo.InvariantCulture)) continue; //do not use "==", won't work

                    ResourceSet rs = rm.GetResourceSet(culture, true, false);
                    if (rs != null)
                        result.Add(culture);
                }
                catch (CultureNotFoundException)
                {
                    //Ignored
                }
            }
            return result;
        }

        //TODO might want to move it somewhere else
        [TypeRequired("Krisp.BackEnd.AppSettings", "InstallationInfo Generator")]                                   //0T
        [PropertyRequired("Krisp.BackEnd.InstallationRequestInfo", "body", "InstallationInfo Generator")]           //0P
        [PropertyRequired("Krisp.BackEnd.InstallationInfo", "installation_id", "InstallationInfo Generator")]       //1P, 2T
        [PropertyRequired("Krisp.BackEnd.InstallationInfo", "secret", "InstallationInfo Generator")]                //2P
        [PropertyRequired("Krisp.BackEnd.InstallationInfo", "settings", "InstallationInfo Generator")]              //3P
        [PropertyRequired("Krisp.BackEnd.AppSettings", "name", "InstallationInfo Generator")]                       //4P
        [PropertyRequired("Krisp.BackEnd.AppSettings", "value", "InstallationInfo Generator")]                      //5P
        public static class InstallationInfoGenerator
        {
            public static (object, Type) GenerateInstallationResponseKrisp(object installationRequestInfo)
            {
                var reqInfoBodyProp = R.P[0];
                var installationInfo = reqInfoBodyProp.GetValue(installationRequestInfo);

                var instInfInstallationIDProp = R.P[1];
                var instInfSecretProp = R.P[2];
                var instInfSettingsProp = R.P[3];

                instInfSecretProp.SetValue(installationInfo, instInfInstallationIDProp.GetValue(installationInfo));

                var appSettingsType = R.T[0];
                var appSettingsNameProp = R.P[4];
                var appSettingsValueProp = R.P[5];
                var listOfSettingsType = typeof(List<>).MakeGenericType(appSettingsType);
                var listOfSettingsAddMethod = AccessTools.Method(listOfSettingsType, "Add");
                var appSettings = Activator.CreateInstance(appSettingsType);
                var listOfSettings = Activator.CreateInstance(listOfSettingsType);

                appSettingsNameProp.SetValue(appSettings, "ignore_list");
                appSettingsValueProp.SetValue(appSettings, "[\"rundll32\",\"ptoneclk\",\"SystemSettings\",\"svchost\"]");
                listOfSettingsAddMethod.Invoke(listOfSettings, new[] { appSettings });

                instInfSettingsProp.SetValue(installationInfo, listOfSettings);

                return MakeKrispResponse(installationInfo, R.T[2]);
            }

            public static (object, Type) GenerateInstallationResponseREST(object installationRequestInfo)
            {
                var (krispResp, krispRespType) = GenerateInstallationResponseKrisp(installationRequestInfo);
                return MakeRestResponse(krispResp, krispRespType);
            }
        }

        [PropertyRequired("Krisp.BackEnd.Mode", "props", "UserProfileInfo Generator")]                              //0
        [PropertyRequired("Krisp.BackEnd.Mode", "name", "UserProfileInfo Generator")]                               //1
        [PropertyRequired("Krisp.BackEnd.Team", "id", "UserProfileInfo Generator")]                                 //2
        [PropertyRequired("Krisp.BackEnd.Team", "name", "UserProfileInfo Generator")]                               //3
        [PropertyRequired("Krisp.BackEnd.UpdateSetting", "prevent_update", "UserProfileInfo Generator")]            //4
        [PropertyRequired("Krisp.BackEnd.NCOutSetting", "pnc", "UserProfileInfo Generator")]                        //5
        [PropertyRequired("Krisp.BackEnd.BaseProfileSetting", "available", "UserProfileInfo Generator")]            //6
        [PropertyRequired("Krisp.BackEnd.ProfileSettings", "contact_support", "UserProfileInfo Generator")]         //7
        [PropertyRequired("Krisp.BackEnd.ProfileSettings", "report_problem", "UserProfileInfo Generator")]          //8
        [PropertyRequired("Krisp.BackEnd.ProfileSettings", "update", "UserProfileInfo Generator")]                  //9
        [PropertyRequired("Krisp.BackEnd.ProfileSettings", "nc_in", "UserProfileInfo Generator")]                   //10
        [PropertyRequired("Krisp.BackEnd.ProfileSettings", "nc_out", "UserProfileInfo Generator")]                  //11
        [PropertyRequired("Krisp.BackEnd.UserProfileInfo", "email", "UserProfileInfo Generator")]                   //12
        [PropertyRequired("Krisp.BackEnd.UserProfileInfo", "id", "UserProfileInfo Generator")]                      //13
        [PropertyRequired("Krisp.BackEnd.UserProfileInfo", "mode", "UserProfileInfo Generator")]                    //14
        [PropertyRequired("Krisp.BackEnd.UserProfileInfo", "team", "UserProfileInfo Generator")]                    //15
        [PropertyRequired("Krisp.BackEnd.UserProfileInfo", "settings", "UserProfileInfo Generator")]                //16
        [PropertyRequired("Krisp.BackEnd.UserProfileInfo", "ref_string", "UserProfileInfo Generator")]              //17
        public static class UserProfileInfoGenerator
        {
            // ReSharper disable InconsistentNaming
            /// <summary>
            /// Emulates UserProfileInfo instance
            /// </summary>
            /// <param name="updateEnabled"></param>
            /// <returns></returns>
            public static (object, Type) GenerateUserProfileInfoKrisp(bool updateEnabled)
            {
                var modePropsProp = R.P[0];
                var modeNameProp = R.P[1];
                var teamIDProp = R.P[2];
                var teamNameProp = R.P[3];
                var updateSettingPreventUpdateProp = R.P[4];
                var NCOutSettingPNCProp = R.P[5];
                var baseSettingAvailableProp = R.P[6];
                var profileSettingsContactSupportProp = R.P[7];
                var profileSettingsReportProblemProp = R.P[8];
                var profileSettingsUpdateProp = R.P[9];
                var profileSettingsNCInProp = R.P[10];
                var profileSettingsNCOutProp = R.P[11];
                var NCInStateProp = AccessTools.Property(profileSettingsNCInProp.PropertyType, "state");
                var NCOutStateProp = AccessTools.Property(profileSettingsNCOutProp.PropertyType, "state");
                var infoEmailProp = R.P[12];
                var infoIdProp = R.P[13];
                var infoModeProp = R.P[14];
                var infoTeamProp = R.P[15];
                var infoSettingsProp = R.P[16];
                var infoRefStringProp = R.P[17];

                var userProfileInfo = Activator.CreateInstance(R.T[12]);
                var mode = Activator.CreateInstance(R.T[0]);
                var team = Activator.CreateInstance(R.T[2]);
                var settings = Activator.CreateInstance(R.T[7]);
                var contact_support = Activator.CreateInstance(R.T[6]);
                var report_problem = Activator.CreateInstance(R.T[6]);
                var update = Activator.CreateInstance(R.T[4]);
                var nc_in = Activator.CreateInstance(profileSettingsNCInProp.PropertyType); //Was different type in the old version of the target app
                var nc_out = Activator.CreateInstance(R.T[5]);
                var pnc = Activator.CreateInstance(R.T[6]);

                baseSettingAvailableProp.SetValue(pnc, true);
                baseSettingAvailableProp.SetValue(nc_out, true);
                baseSettingAvailableProp.SetValue(update, updateEnabled);
                baseSettingAvailableProp.SetValue(report_problem, true);
                baseSettingAvailableProp.SetValue(contact_support, true);

                NCOutSettingPNCProp.SetValue(nc_out, pnc);
                NCOutStateProp?.SetValue(nc_out, "user_choice");
                NCInStateProp?.SetValue(nc_in, "user_choice");

                updateSettingPreventUpdateProp.SetValue(update, updateEnabled ? "off" : "on");

                profileSettingsContactSupportProp.SetValue(settings, contact_support);
                profileSettingsReportProblemProp.SetValue(settings, report_problem);
                profileSettingsUpdateProp.SetValue(settings, update);
                profileSettingsNCInProp.SetValue(settings, nc_in);
                profileSettingsNCOutProp.SetValue(settings, nc_out);

                teamNameProp.SetValue(team, "SilveIT");
                teamIDProp.SetValue(team, 1337U);

                modePropsProp.SetValue(mode, "");
                modeNameProp.SetValue(mode, "unlimited");

                infoEmailProp.SetValue(userProfileInfo, "ModdedBy@SilveIT");
                infoIdProp.SetValue(userProfileInfo, 1337U);
                infoModeProp.SetValue(userProfileInfo, mode);
                infoTeamProp.SetValue(userProfileInfo, team);
                infoSettingsProp.SetValue(userProfileInfo, settings);
                infoRefStringProp.SetValue(userProfileInfo, "SilveIT");

                return MakeKrispResponse(userProfileInfo, R.T[12]);
            }

            /// <summary>
            /// Replaces part of the existing UserProfileInfo instance
            /// </summary>
            /// <param name="krispResponse"></param>
            public static void PatchUserInfoKrisp(object krispResponse)
            {
                var dataProp = AccessTools.Property(krispResponse.GetType(), "data");
                var data = dataProp.GetValue(krispResponse);

                var modePropsProp = R.P[0];
                var modeNameProp = R.P[1];

                var infoModeProp = R.P[14];

                //Not sure why I create new instance...
                var mode = Activator.CreateInstance(R.T[0]);

                modePropsProp.SetValue(mode, "");
                modeNameProp.SetValue(mode, "unlimited");

                infoModeProp.SetValue(data, mode);
            }
            // ReSharper restore InconsistentNaming
        }
    }

    // ReSharper disable UnusedMember.Global
    // ReSharper disable InconsistentNaming
    public enum LogLevel
    {
        TRACE,
        DEBUG,
        INFO,
        WARNING,
        ERROR,
        CRITICAL,
        COUNT
    }
    // ReSharper restore InconsistentNaming
    // ReSharper restore UnusedMember.Global
}