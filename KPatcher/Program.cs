using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using HarmonyLib;
using KPatcher.Properties;

namespace KPatcher
{
    internal class Program
    {
        public static Thread TargetThread { get; set; }
        public static Assembly TargetAssembly { get; private set; }
        public static string KPatcherFullPath;
        public const string TargetBinaryName = "Krisp.exe";
        public static string TargetName = TargetBinaryName.Replace(".exe", "");

        public static string TargetFullPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), TargetName, TargetBinaryName);

        public static string TargetDirectory => Path.GetDirectoryName(TargetFullPath) ??
                                                   throw new InvalidOperationException("Pass valid path!");
        public static string TargetConfigFullPath => TargetFullPath + ".config";

        public static ReflectionHelper ReflectionHelper { get; private set; }
        public static AssemblyLoader AssemblyLoader { get; private set; }

        private static void Main(string[] args)
        {
            KPatcherFullPath = Assembly.GetExecutingAssembly().Location;
            try
            {
                //Fixing the main config path
                AppDomain.CurrentDomain.SetData("APP_CONFIG_FILE", TargetConfigFullPath);

                Utils.EnableConsoleCloseButton(false);
                if (!Settings.Default.ShowConsole)
                    Utils.ShowConsole(false);

                if (!Settings.Default.OnlyForEducationAccepted)
                {
                    var dres = MessageBox.Show("This application is intended to be used\r\n" +
                                               "for Educational Purposes ONLY!\r\n" +
                                               "Press YES only if you have read the license of this application and\r\n" +
                                               "agree to use the application in accordance with the above requirements.",
                        "Educational Purposes only!", MessageBoxButtons.YesNo);
                    if (dres == DialogResult.Yes)
                    {
                        Settings.Default.OnlyForEducationAccepted = true;
                        Settings.Default.Save();
                    }
                    else
                        Environment.Exit(0);
                }

                Console.WriteLine("KPatcher by SilveIT");

                if (!File.Exists(TargetFullPath))
                {
                    Console.WriteLine(TargetBinaryName + " doesn't exist, please make sure that your installation of the target app is good");
                    if (!Settings.Default.ShowConsole)
                        Utils.ShowConsole(true);
                    Utils.EnableConsoleCloseButton(true);
                    Console.ReadKey();
                    return;
                }

                //Fixing paths (this + hook for AppDomain.CurrentDomain.BaseDirectory + hook for EnvHelper:KrispExeFullPath)
                Directory.SetCurrentDirectory(TargetDirectory);

                //Loading target assembly and its dependencies
                AssemblyLoader = new AssemblyLoader(TargetFullPath, TargetDirectory);
                TargetAssembly = AssemblyLoader.Load();
                Console.WriteLine("Target assembly loaded! Loading types...");

                //The MAIN part of this project
                ReflectionHelper = new ReflectionHelper(true);
                //Finding all requirement attributes and resolving MemberInfos
                var failed = ReflectionHelper.LoadRequiredMemberInfoForAssembly(Assembly.GetExecutingAssembly());

                if (failed.Count != 0)
                {
                    Console.WriteLine("These requirements were not satisfied:");
                    foreach (var s in failed)
                        Console.WriteLine(s);
                    Console.WriteLine("Since we need a perfect environment to begin with, we'll consider our work complete!");
                    if (!Settings.Default.ShowConsole)
                        Utils.ShowConsole(true);
                    Utils.EnableConsoleCloseButton(true);
                    Console.ReadKey();
                    return;
                }
                Console.WriteLine("Types loaded!");

                //Using Harmony library to inject my code inside the target assembly
                var harmony = new Harmony("KPatcherMain");
                harmony.PatchAll();
                var patched = harmony.GetPatchedMethods();
                Console.WriteLine("Patch count: " + patched.Count());

                var ex = Utils.FixStartup();
                if (ex != null)
                {
                    Console.WriteLine("Failed to update startup values in the registry\r\n{0}", ex);
                    if (!Settings.Default.ShowConsole)
                        Utils.ShowConsole(true);
                    Utils.EnableConsoleCloseButton(true);
                    Console.ReadKey();
                }

                Console.WriteLine("Calling entrypoint...");
                TargetThread = AssemblyLoader.InvokeEntrypoint(args);
                Console.WriteLine("Entrypoint was called!");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                if (!Settings.Default.ShowConsole) 
                    Utils.ShowConsole(true);
                Utils.EnableConsoleCloseButton(true);
                Console.ReadKey();
            }

            TargetThread?.Join();
        }
    }
}
