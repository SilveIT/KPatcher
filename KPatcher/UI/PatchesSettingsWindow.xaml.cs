using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;

namespace KPatcher.UI
{
    public partial class PatcherSettingsWindow
    {
        private SettingsViewModel SettingsModel { get; }
        public PatcherSettingsWindow()
        {
            InitializeComponent();

            //To make a borderless window with custom border and titlebar
            var resizableBorderLessChrome = new WindowChrome
            {
                CornerRadius = new CornerRadius(0),
                CaptionHeight = 0
            };
            WindowChrome.SetWindowChrome(this, resizableBorderLessChrome);

            SettingsModel = new SettingsViewModel();
            DataContext = SettingsModel;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void Rectangle_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            DragMove();
        }

        private void BtnSaveSettings_OnClick(object sender, RoutedEventArgs e) => SettingsModel.Save();

        private void PatcherSettingsWindow_OnClosing(object sender, CancelEventArgs e) => e.Cancel = !SettingsModel.ProcessClosing();

        private void BtnResetSettings_OnClick(object sender, RoutedEventArgs e) => SettingsModel.ResetSettings();

        //private void BtnTest_OnClick(object sender, RoutedEventArgs e)
        //{
        //    try
        //    {
        //        var type = HarmonyLib.AccessTools.TypeByName("Krisp.BackEnd.AccountManager");
        //        var method = HarmonyLib.AccessTools.Method(type, "<StartFetchUserProfileTimer>b__88_0");
        //        method.Invoke(AccountManager_SetStatePatch.AccountManagerInstance, new object[] { null, null });
        //    }
        //    catch (Exception exception)
        //    {
        //        Console.WriteLine(exception);
        //    }
        //}
    }
}
