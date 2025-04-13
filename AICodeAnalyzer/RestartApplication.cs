using System;
using System.Diagnostics;
using System.Windows;

namespace AICodeAnalyzer;

public class RestartApplication
{
    public static void Restart()
    {
        var processModule = Process.GetCurrentProcess()?.MainModule;
        if (processModule == null) return;
        var startInfo = new ProcessStartInfo
        {
            FileName = processModule.FileName,
            UseShellExecute = true
        };

        Process.Start(startInfo);

        Application.Current.Shutdown();
        Environment.Exit(0);
    }
}