using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using Microsoft.Win32;

namespace AICodeAnalyzer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    private const string FileExtension = ".md";
    private const string AppName = "AICodeAnalyzer";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Only register file association if the setting is enabled
        var settingsManager = new SettingsManager();
        if (settingsManager.Settings.RegisterAsDefaultMdHandler)
        {
            RegisterFileAssociation();
        }

        // Check for command-line arguments (file path)
        if (e.Args.Length > 0)
        {
            var filePath = e.Args[0];
            // Store the file path for later use in MainWindow
            Properties["StartupFilePath"] = filePath;

            // If the MainWindow is already created, load the file immediately
            if (MainWindow is MainWindow mainWindow)
            {
                mainWindow.LoadMarkdownFile(filePath);
            }
        }
    }

    public void UnregisterFileAssociation()
    {
        try
        {
            // Remove the file extension association
            using (var extensionKey = Registry.ClassesRoot.OpenSubKey(".md", true))
            {
                if (extensionKey != null)
                {
                    var currentHandler = extensionKey.GetValue(null) as string;
                    if (currentHandler == AppName)
                    {
                        extensionKey.DeleteValue(null!, false);
                        LogInformation("File extension association removed.");
                    }
                }
            }

            // Remove the application registration
            using (var appKey = Registry.ClassesRoot.OpenSubKey(AppName, true))
            {
                if (appKey != null)
                {
                    Registry.ClassesRoot.DeleteSubKeyTree(AppName);
                    LogInformation("Application registration removed.");
                }
            }

            LogInformation("File association unregistered successfully.");
        }
        catch (Exception ex)
        {
            LogError($"Error unregistering file association: {ex.Message}");
            ErrorLogger.LogError(ex, "Unregistering file association");
        }
    }

    public void RegisterFileAssociation()
    {
        try
        {
            var appPath = Assembly.GetEntryAssembly()?.Location;

            // Create registry key for the file extension
            using (var extensionKey = Registry.ClassesRoot.CreateSubKey(FileExtension))
            {
                if (extensionKey == null)
                {
                    LogError("Failed to create registry key for file extension.");
                    return;
                }

                extensionKey.SetValue(null, AppName);
                extensionKey.SetValue("PerceivedType", "text"); // Optional: Set perceived type
            }

            // Create registry key for the application
            using (var appKey = Registry.ClassesRoot.CreateSubKey(AppName))
            {
                if (appKey == null)
                {
                    LogError("Failed to create registry key for application.");
                    return;
                }

                appKey.SetValue(null, "Markdown File");

                // Create registry key for the application's shell command
                using (var shellKey = appKey.CreateSubKey("shell"))
                {
                    if (shellKey == null)
                    {
                        LogError("Failed to create registry key for shell command.");
                        return;
                    }

                    using (var openKey = shellKey.CreateSubKey("open"))
                    {
                        if (openKey == null)
                        {
                            LogError("Failed to create registry key for open command.");
                            return;
                        }

                        using (var commandKey = openKey.CreateSubKey("command"))
                        {
                            if (commandKey == null)
                            {
                                LogError("Failed to create registry key for command.");
                                return;
                            }

                            commandKey.SetValue(null, $"\"{appPath}\" \"%1\"");
                        }
                    }
                }

                using (var defaultIcon = appKey.CreateSubKey("DefaultIcon"))
                {
                    if (defaultIcon == null)
                    {
                        LogError("Failed to create registry key for DefaultIcon command.");
                        return;
                    }

                    defaultIcon.SetValue(null, appPath + ",0");
                }
            }

            LogInformation("File association registered successfully.");
        }
        catch (Exception ex)
        {
            LogError($"Error registering file association: {ex.Message}");
            ErrorLogger.LogError(ex, "Registering file association");
        }
    }

    private static void LogError(string message)
    {
        Console.WriteLine($"ERROR: {message}");
        Debug.WriteLine($"ERROR: {message}");
    }

    private static void LogInformation(string message)
    {
        Console.WriteLine($"INFORMATION: {message}");
        Debug.WriteLine($"INFORMATION: {message}");
    }
}