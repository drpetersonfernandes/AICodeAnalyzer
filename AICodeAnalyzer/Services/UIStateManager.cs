using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace AICodeAnalyzer.Services;

public class UiStateManager
{
    private readonly LoggingService _loggingService;
    private readonly DispatcherTimer _statusUpdateTimer = new();
    private readonly string[] _loadingDots = { ".", "..", "...", "....", "....." };

    private bool _isProcessing;
    private int _dotsIndex;
    private string _baseStatusMessage = string.Empty;

    // UI Elements
    private readonly TextBlock _statusTextBlock;

    public UiStateManager(TextBlock statusTextBlock, LoggingService loggingService)
    {
        _statusTextBlock = statusTextBlock;
        _loggingService = loggingService;

        // Setup status update timer
        _statusUpdateTimer.Interval = TimeSpan.FromMilliseconds(300);
        _statusUpdateTimer.Tick += StatusUpdateTimer_Tick;
    }

    public void SetStatusMessage(string message)
    {
        if (_isProcessing) return;

        _statusTextBlock.Text = message;
        _loggingService.LogOperation($"Status: {message}");
    }

    public void SetProcessingState(bool isProcessing, string statusMessage = "")
    {
        if (isProcessing == _isProcessing) return;

        _isProcessing = isProcessing;

        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (isProcessing)
            {
                // Store the base message for the animation
                _baseStatusMessage = statusMessage;
                _dotsIndex = 0;

                // Start the timer for the animation
                _statusUpdateTimer.Start();

                // Only show wait cursor to indicate processing
                Mouse.OverrideCursor = Cursors.Wait;

                _loggingService.LogOperation($"Processing started: {statusMessage}");
            }
            else
            {
                // Stop the timer
                _statusUpdateTimer.Stop();

                // Clear the wait cursor
                Mouse.OverrideCursor = null;

                // Reset status message if we're no longer processing
                _statusTextBlock.Text = "Ready";

                _loggingService.LogOperation("Processing completed");
            }
        }));
    }

    private void StatusUpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isProcessing) return;

        // Update the status text with animated dots
        _dotsIndex = (_dotsIndex + 1) % _loadingDots.Length;
        _statusTextBlock.Text = $"{_baseStatusMessage}{_loadingDots[_dotsIndex]}";
    }

    public void EnableControl(Control control, bool enabled)
    {
        control.IsEnabled = enabled;

        if (enabled)
        {
            _loggingService.LogOperation($"Control enabled: {control.Name ?? "unnamed"}");
        }
        else
        {
            _loggingService.LogOperation($"Control disabled: {control.Name ?? "unnamed"}");
        }
    }

    public void SetControlVisibility(UIElement element, Visibility visibility)
    {
        element.Visibility = visibility;

        var controlName = element is FrameworkElement fe ? fe.Name : "unnamed";
        _loggingService.LogOperation($"Control visibility changed: {controlName} => {visibility}");
    }

    public void UpdateButtonContent(Button button, string content)
    {
        button.Content = content;
        _loggingService.LogOperation($"Button content updated: {button.Name ?? "unnamed"} => {content}");
    }
}