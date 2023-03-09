using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Windows;
using HarmonyLib;
using KPatcher.Properties;
using Microsoft.Win32;
using Application = System.Windows.Forms.Application;

namespace KPatcher
{
    [TypeRequired("Krisp.BackEnd.KrispWebClientResponse`1", "Krisp network response generation")]
    [TypeRequired("RestSharp.RestResponse`1", "Krisp network response generation")]
    [TypeRequired("Krisp.UI.Views.Windows.MessageBox", "Krisp's messagebox usage")]
    [MethodRequired("Krisp.UI.DialogWindowFactory", "CreateDialogWindow", "Krisp's dialog usage")]
    [FieldRequired("Krisp.UI.Views.Windows.DialogWindow", "_result", "Krisp's dialog usage")]
    [PropertyRequired("Krisp.Properties.Resources", "ResourceManager", "Icon loader")]
    public static class Utils
    {
        private static Random _random = new Random();

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

        public static string RandomHexDigits(int length)
        {
            const string hexChars = "ABCDEF0123456789";
            return new string(Enumerable.Repeat(hexChars, length).Select(s => s[_random.Next(s.Length)]).ToArray());
        }

        public static string RandomDigits(int length)
        {
            const string hexChars = "0123456789";
            return new string(Enumerable.Repeat(hexChars, length).Select(s => s[_random.Next(s.Length)]).ToArray());
        }

        public static string GetRandomHWID() => RandomHexDigits(4);

        public static string GetRandomHardwareIdentifier()
        {
            //For example:
            //Samsung SSD 980 PRO 1TBD31C_0C80_4238_7B3C.ASUSTeK COMPUTER INC.895938754001256
            var res = "Samsung SSD 980 PRO 1TB";
            res += RandomHexDigits(4) + '_' + RandomHexDigits(4) + '_' + RandomHexDigits(4) + '_' + RandomHexDigits(4);
            res += ".ASUSTeK COMPUTER INC.";
            res += RandomDigits(15);

            return res;
        }

        /// <summary>
        /// Replaces path in the system startup of the target app
        /// </summary>
        /// <returns></returns>
        public static Exception FixStartup()
        {
            try
            {
                var registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
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
        /// <returns></returns>
        public static (object, Type) MakeKrispResponse(object dataInstance)
        {
            var krispRespType = R.T[0].MakeGenericType(dataInstance.GetType());
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

            var rm = new ResourceManager("Krisp.Properties.Resources", Program.AssemblyLoader.RequestedAssembly);

            var cultures = CultureInfo.GetCultures(CultureTypes.AllCultures);
            foreach (var culture in cultures)
            {
                try
                {
                    if (culture.Equals(CultureInfo.InvariantCulture)) continue; //do not use "==", won't work

                    var rs = rm.GetResourceSet(culture, true, false);
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

                return MakeKrispResponse(installationInfo);
            }

            public static (object, Type) GenerateInstallationResponseREST(object installationRequestInfo)
            {
                var (krispResp, krispRespType) = GenerateInstallationResponseKrisp(installationRequestInfo);
                return MakeRestResponse(krispResp, krispRespType);
            }
        }

        [TypeRequired("Krisp.BackEnd.UserProfileInfo", "UserProfileInfo Generator")]
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
                var userProfileInfo = JsonSerializer.Deserialize(Resources.UserProfileInfo, R.T[0]);
                return MakeKrispResponse(userProfileInfo);
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