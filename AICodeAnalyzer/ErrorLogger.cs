using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;

namespace AICodeAnalyzer;

public static class ErrorLogger
{
    private const string LogFileName = "ErrorLog.txt";
    private static readonly string LogFilePath = Path.Combine(AppContext.BaseDirectory, LogFileName);

    public static void LogError(Exception ex, string context = "")
    {
        try
        {
            // Create a detailed error log
            var sb = new StringBuilder();
            sb.AppendLine(CultureInfo.InvariantCulture, $"===== Error Log: {DateTime.Now} =====");

            if (!string.IsNullOrEmpty(context))
                sb.AppendLine(CultureInfo.InvariantCulture, $"Context: {context}");

            sb.AppendLine(CultureInfo.InvariantCulture, $"Error Type: {ex.GetType().Name}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"Message: {ex.Message}");
            sb.AppendLine($"Stack Trace:");
            sb.AppendLine(ex.StackTrace);

            // Add any inner exception details
            if (ex.InnerException != null)
            {
                sb.AppendLine();
                sb.AppendLine("Inner Exception:");
                sb.AppendLine(CultureInfo.InvariantCulture, $"Type: {ex.InnerException.GetType().Name}");
                sb.AppendLine(CultureInfo.InvariantCulture, $"Message: {ex.InnerException.Message}");
                sb.AppendLine("Stack Trace:");
                sb.AppendLine(ex.InnerException.StackTrace);
            }

            sb.AppendLine();

            // Append to the log file
            File.AppendAllText(LogFilePath, sb.ToString());

            // Ask the user if they want to view the log
            var result = MessageBox.Show(
                $"An error occurred: {ex.Message}\n\nThe error has been logged to {LogFileName}. Would you like to open the log file?",
                "Error",
                MessageBoxButton.YesNo,
                MessageBoxImage.Error);

            if (result == MessageBoxResult.Yes)
            {
                // Open the log file in the default text editor
                Process.Start(new ProcessStartInfo
                {
                    FileName = LogFilePath,
                    UseShellExecute = true
                });
            }
        }
        catch
        {
            // If logging fails, show a basic error message
            MessageBox.Show(
                $"An error occurred: {ex.Message}\n\nFailed to write to the error log.",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Logs an error to the file without showing a dialog to the user
    /// </summary>
    /// <param name="ex">The exception to log</param>
    /// <param name="context">Optional context information</param>
    public static void LogErrorSilently(Exception ex, string context = "")
    {
        try
        {
            // Create a detailed error log
            var sb = new StringBuilder();
            sb.AppendLine(CultureInfo.InvariantCulture, $"===== Error Log: {DateTime.Now} =====");

            if (!string.IsNullOrEmpty(context))
                sb.AppendLine(CultureInfo.InvariantCulture, $"Context: {context}");

            sb.AppendLine(CultureInfo.InvariantCulture, $"Error Type: {ex.GetType().Name}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"Message: {ex.Message}");
            sb.AppendLine($"Stack Trace:");
            sb.AppendLine(ex.StackTrace);

            // Add any inner exception details
            if (ex.InnerException != null)
            {
                sb.AppendLine();
                sb.AppendLine("Inner Exception:");
                sb.AppendLine(CultureInfo.InvariantCulture, $"Type: {ex.InnerException.GetType().Name}");
                sb.AppendLine(CultureInfo.InvariantCulture, $"Message: {ex.InnerException.Message}");
                sb.AppendLine("Stack Trace:");
                sb.AppendLine(ex.InnerException.StackTrace);
            }

            sb.AppendLine();

            // Append to the log file without showing a dialog
            File.AppendAllText(LogFilePath, sb.ToString());
        }
        catch
        {
            // If silent logging fails, just continue without notify
        }
    }
}