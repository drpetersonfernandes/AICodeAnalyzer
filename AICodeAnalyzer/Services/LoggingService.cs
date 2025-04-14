using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace AICodeAnalyzer.Services;

public class LoggingService(TextBox logTextBox)
{
    private readonly TextBox _logTextBox = logTextBox;
    private readonly Dictionary<string, DateTime> _operationTimers = new();

    public void LogOperation(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var logEntry = $"[{timestamp}] {message}\r\n";

        // Add to log TextBox
        Application.Current.Dispatcher.Invoke(() =>
        {
            _logTextBox.AppendText(logEntry);
            _logTextBox.ScrollToEnd();

            // Limit log size (optional)
            if (_logTextBox.Text.Length > 50000)
            {
                _logTextBox.Text = _logTextBox.Text[^40000..];
            }
        });
    }

    public void StartOperationTimer(string operationName)
    {
        _operationTimers[operationName] = DateTime.Now;
        LogOperation($"Started: {operationName}");
    }

    public void EndOperationTimer(string operationName)
    {
        if (_operationTimers.TryGetValue(operationName, out var startTime))
        {
            var elapsed = DateTime.Now - startTime;
            LogOperation($"Completed: {operationName} (Elapsed: {elapsed.TotalSeconds:F2} seconds)");
            _operationTimers.Remove(operationName);
        }
        else
        {
            LogOperation($"Completed: {operationName} (Timer not found)");
        }
    }
}