using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace AICodeAnalyzer;

/// <inheritdoc />
/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    private FileAssociationManager? _fileAssociationManager;

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);

            // Initialize the file association manager
            _fileAssociationManager = new FileAssociationManager(LogInformation, LogError);

            // Only register file association if the setting is enabled
            var settingsManager = new SettingsManager();
            if (settingsManager.Settings.RegisterAsDefaultMdHandler)
            {
                // Consider running this in the background since it involves registry operations
                await Task.Run(() => _fileAssociationManager.RegisterApplication());
            }

            // Check for command-line arguments (file path)
            if (e.Args.Length > 0)
            {
                var filePath = e.Args[0];
                // Store the file path for later use in MainWindow
                Properties["StartupFilePath"] = filePath;

                // The MainWindow will handle loading the file asynchronously when it initializes
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.LogError(ex, "Error in the OnStartup method.");
        }
    }

    public void UnregisterFileAssociation()
    {
        if (_fileAssociationManager == null)
        {
            _fileAssociationManager = new FileAssociationManager(LogInformation, LogError);
        }

        _fileAssociationManager.UnregisterApplication();
    }

    public void RegisterFileAssociation()
    {
        if (_fileAssociationManager == null)
        {
            _fileAssociationManager = new FileAssociationManager(LogInformation, LogError);
        }

        _fileAssociationManager.RegisterApplication();
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