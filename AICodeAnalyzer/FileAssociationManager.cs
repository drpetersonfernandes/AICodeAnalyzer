﻿using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace AICodeAnalyzer;

/// <summary>
/// Handles file associations using Windows API Code Pack
/// </summary>
public partial class FileAssociationManager(Action<string> logInfo, Action<string> logError)
{
    private const string FileExtension = ".md";
    private const string ProgId = "AICodeAnalyzer";
    private const string FileTypeDescription = "Markdown Document";
    private const string DefaultValueName = ""; // Empty string for default registry value

    private readonly Action<string> _logInfo = logInfo;
    private readonly Action<string> _logError = logError;

    public bool IsApplicationRegistered()
    {
        try
        {
            // Check if our ProgID is registered for the .md extension
            using var key = Registry.ClassesRoot.OpenSubKey(FileExtension);
            if (key == null) return false;

            var currentProgId = key.GetValue(DefaultValueName) as string;
            return currentProgId == ProgId;
        }
        catch (Exception ex)
        {
            _logError($"Error checking file association: {ex.Message}");
            return false;
        }
    }

    public bool RegisterApplication()
    {
        try
        {
            var exePath = GetExecutablePath();
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                _logError($"Cannot find executable path: {exePath}");
                return false;
            }

            _logInfo($"Registering with executable: {exePath}");

            // Create ProgID entry
            using (var progIdKey = Registry.ClassesRoot.CreateSubKey(ProgId))
            {
                if (progIdKey != null)
                {
                    progIdKey.SetValue(DefaultValueName, FileTypeDescription);

                    // Set the default icon
                    using var iconKey = progIdKey.CreateSubKey("DefaultIcon");
                    iconKey.SetValue(DefaultValueName, $"\"{exePath}\",0");

                    // Set up the open command
                    using var shellKey = progIdKey.CreateSubKey("shell");
                    using var openKey = shellKey.CreateSubKey("open");
                    using var commandKey = openKey.CreateSubKey("command");
                    commandKey.SetValue(DefaultValueName, $"\"{exePath}\" \"%1\"");
                }
            }

            // Associate file extension with ProgID
            using (var extensionKey = Registry.ClassesRoot.CreateSubKey(FileExtension))
            {
                if (extensionKey != null)
                {
                    extensionKey.SetValue(DefaultValueName, ProgId);
                    extensionKey.SetValue("PerceivedType", "text");

                    // Add ContentType for additional information
                    extensionKey.SetValue("Content Type", "text/markdown");
                }
            }

            // Notify Windows of the change
            SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);

            _logInfo("Successfully registered application as handler for .md files");
            return true;
        }
        catch (Exception ex)
        {
            _logError($"Error registering application: {ex.Message}");
            ErrorLogger.LogError(ex, "Error registering file association");

            return false;
        }
    }

    public void UnregisterApplication()
    {
        try
        {
            if (!IsApplicationRegistered())
            {
                _logInfo("Application is not registered as handler for .md files");

                return;
            }

            // Remove file association
            using (var extensionKey = Registry.ClassesRoot.OpenSubKey(FileExtension, true))
            {
                if (extensionKey != null)
                {
                    var currentProgId = extensionKey.GetValue(DefaultValueName) as string;
                    if (currentProgId == ProgId)
                    {
                        extensionKey.DeleteValue(DefaultValueName, false);
                        _logInfo("Removed file extension association");
                    }
                }
            }

            // Delete ProgID entry
            try
            {
                Registry.ClassesRoot.DeleteSubKeyTree(ProgId);
                _logInfo("Removed ProgID entry");
            }
            catch (ArgumentException)
            {
                // Key doesn't exist, which is fine
                _logInfo("ProgID entry was already removed");
            }

            // Notify Windows of the change
            SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);

            _logInfo("Successfully unregistered application");
        }
        catch (Exception ex)
        {
            _logError($"Error unregistering application: {ex.Message}");
            ErrorLogger.LogError(ex, "Error unregistering file association");
        }
    }

    private string GetExecutablePath()
    {
        try
        {
            // First try: Get the path from the current process
            var processPath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath) && processPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                return processPath;
            }

            // Second try: Use Assembly location
            var assemblyLocation = Assembly.GetEntryAssembly()?.Location;
            if (!string.IsNullOrEmpty(assemblyLocation))
            {
                var directory = Path.GetDirectoryName(assemblyLocation);
                var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
                if (!string.IsNullOrEmpty(directory) && !string.IsNullOrEmpty(assemblyName))
                {
                    var exePath = Path.Combine(directory, $"{assemblyName}.exe");
                    if (File.Exists(exePath))
                    {
                        return exePath;
                    }
                }
            }

            // Third try: Use AppDomain.BaseDirectory
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var appName = AppDomain.CurrentDomain.FriendlyName.Replace(".dll", "");
            var fallbackPath = Path.Combine(baseDir, $"{appName}.exe");
            if (File.Exists(fallbackPath))
            {
                return fallbackPath;
            }

            // Last resort: Find any EXE in the current directory
            var exeFiles = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.exe");
            if (exeFiles.Length > 0)
            {
                return exeFiles[0];
            }

            _logError("Could not determine executable path");
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logError($"Error getting executable path: {ex.Message}");

            return string.Empty;
        }
    }

    // Windows API for notifying the system of file association changes
    [System.Runtime.InteropServices.LibraryImport("shell32.dll")]
    private static partial void SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);
}