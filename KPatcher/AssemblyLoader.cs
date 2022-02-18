using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;

namespace KPatcher
{
    /// <summary>
    /// Assembly dependency resolver
    /// </summary>
    public class AssemblyLoader : IDisposable
    {
        public Assembly RequestedAssembly { get; private set; }
        private readonly string _path;
        private readonly Dictionary<string, string> _possibleDependencies;

        public AssemblyLoader(string path, string depSearchPath = default)
        {
            if (string.IsNullOrEmpty(depSearchPath))
                depSearchPath = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Pass valid path!");
            _path = path;
            var dlls = new DirectoryInfo(depSearchPath).GetFiles("*.dll", SearchOption.AllDirectories);
            _possibleDependencies = new Dictionary<string, string>();
            foreach (var dll in dlls)
            {
                try
                {
                    AssemblyName assName = AssemblyName.GetAssemblyName(dll.FullName);
                    _possibleDependencies.Add(assName.FullName, dll.FullName);
                }
                catch (Exception)
                {
                    //Ignored
                }
            }
        }

        public Assembly Load()
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            AssemblyName assemblyName = AssemblyName.GetAssemblyName(_path);
            var ass = AppDomain.CurrentDomain.Load(assemblyName);
            RequestedAssembly = ass;
            return ass;
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            {
                try
                {
                    var r = _possibleDependencies.TryGetValue(args.Name, out var dll);
                    return !r ? null : Assembly.LoadFrom(dll);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }

        public Thread InvokeEntrypoint() => InvokeEntrypoint(Array.Empty<string>());

        public Thread InvokeEntrypoint(string[] args, bool exitOnSuccess = true)
        {
            if (RequestedAssembly == null)
                throw new InvalidOperationException("Load assembly first!");
            AppDomain.CurrentDomain.UnhandledException += UnhandledExcHandler;
            var newWindowThread = new Thread(() =>
            {
                SynchronizationContext.SetSynchronizationContext(SynchronizationContext.Current);
                var result = (int)RequestedAssembly.EntryPoint.Invoke(null, new object[] { args });
                if (exitOnSuccess && result == 0)
                    Environment.Exit(0);
                Console.WriteLine($"Assembly entrypoint exited with code {result}!");
            });
            newWindowThread.SetApartmentState(ApartmentState.STA);
            newWindowThread.IsBackground = true;
            newWindowThread.Start();
            return newWindowThread;
        }

        private static void UnhandledExcHandler(object sender, UnhandledExceptionEventArgs args) =>
            Console.WriteLine($"{((Exception)args.ExceptionObject).Message}\r\n" +
                              $"Runtime is terminating: {args.IsTerminating}");

        public void Dispose()
        {
            AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
            AppDomain.CurrentDomain.UnhandledException -= UnhandledExcHandler;
        }
    }
}