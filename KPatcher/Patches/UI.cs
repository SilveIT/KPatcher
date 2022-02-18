using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using KPatcher.Models;
using KPatcher.UI;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable RedundantAssignment

namespace KPatcher.Patches
{
    /// <summary>
    /// Replaces WhatsNew with a new command which opens the patcher's settings window
    /// </summary>
    [PropertyRequired("Krisp.UI.ViewModels.KrispWindowViewModel", "WhatsNewCommand", "KrispWindowViewModel.WhatsNewCommand property getter patch")]
    [HarmonyPatch]
    public class KrispWindowViewModel_WhatsNewCommandPatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => R.P[0].GetMethod;
        public static bool Prefix(ref object __result)
        {
            var command = new UICommand(delegate { new PatcherSettingsWindow().ShowDialog(); });
            __result = command;
            return false;
        }
    }

    /// <summary>
    /// Fixes Krisp's available language cultures
    /// </summary>
    //System.Collections.ObjectModel.ObservableCollection`1<System.Globalization.CultureInfo> Krisp.UI.ViewModels.TranslationSourceViewModel::AvailableLanguages()
    [PropertyRequired("Krisp.UI.ViewModels.TranslationSourceViewModel", "AvailableLanguages",
        "KrispApp:LogToEventViewer property patch")]
    [HarmonyPatch]
    public class TranslationSourceViewModel_AvailableLanguagesPatch
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod() => R.P[0].GetMethod;

        public static bool Prefix(ref ObservableCollection<CultureInfo> __result)
        {
            var res = new ObservableCollection<CultureInfo> { new CultureInfo("en-US") };
            Utils.GetAvailableCultures().Do(o => res.Add(o));
            __result = res;
            return false;
        }
    }
}