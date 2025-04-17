using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Markdig;
using Markdig.Wpf;

namespace AICodeAnalyzer.Services;

public class MarkdownService
{
    private readonly LoggingService _loggingService;
    private const double DefaultZoomLevel = 100.0;
    private const double MinZoomLevel = 20.0;
    private const double MaxZoomLevel = 500.0;

    private double _currentZoomLevel = 100.0;
    private readonly double _textBoxDefaultFontSize;

    private readonly TextBox _rawTextBox;
    private readonly MarkdownViewer _markdownViewer;
    private readonly ScrollViewer _markdownScrollViewer;
    private readonly TextBlock _zoomLevelDisplay;

    private bool _isMarkdownViewActive = true;

    public bool IsMarkdownViewActive => _isMarkdownViewActive;
    public double CurrentZoomLevel => _currentZoomLevel;

    public MarkdownService(
        LoggingService loggingService,
        TextBox rawTextBox,
        MarkdownViewer markdownViewer,
        ScrollViewer markdownScrollViewer,
        TextBlock zoomLevelDisplay)
    {
        _loggingService = loggingService;
        _rawTextBox = rawTextBox;
        _markdownViewer = markdownViewer;
        _markdownScrollViewer = markdownScrollViewer;
        _zoomLevelDisplay = zoomLevelDisplay;
        _textBoxDefaultFontSize = rawTextBox.FontSize;

        // Use default pipeline without modification
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        _markdownViewer.Pipeline = pipeline;

        UpdateZoomDisplay();
    }

    private string PreprocessMarkdown(string markdownContent)
    {
        if (string.IsNullOrEmpty(markdownContent))
            return markdownContent;

        // Refined fix for ```markdown wrapper
        if (!markdownContent.StartsWith("```markdown", StringComparison.OrdinalIgnoreCase))
            return markdownContent; // Return original if not wrapped

        var firstLineEnd = markdownContent.IndexOf('\n');
        if (firstLineEnd == -1) return markdownContent; // Return original if not wrapped
        // Get content after the first line
        var contentAfterFirstLine = markdownContent.Substring(firstLineEnd + 1);
        // Find the last ```
        var lastTripleBacktick = contentAfterFirstLine.LastIndexOf("```", StringComparison.Ordinal);
        if (lastTripleBacktick != -1)
        {
            // Get content before last ```
            markdownContent = contentAfterFirstLine.Substring(0, lastTripleBacktick).Trim();
            _loggingService.LogOperation("Preprocessed markdown response (removed ```markdown wrapper).");
            return markdownContent;
        }

        // If no closing ``` found, just remove opening line
        markdownContent = contentAfterFirstLine.Trim();
        _loggingService.LogOperation("Preprocessed markdown response (removed opening ```markdown line, no closing ``` found).");
        return markdownContent;
    }

    public void SetContent(string rawContent, bool resetZoom = false)
    {
        try
        {
            var processedMarkdown = PreprocessMarkdown(rawContent);

            _rawTextBox.Text = rawContent; // keep raw text

            // Assign markdown to viewer directly (no custom renderer)
            _markdownViewer.Markdown = processedMarkdown;

            _loggingService.LogOperation($"Rendering Markdown content ({processedMarkdown.Length} chars).");

            if (resetZoom)
            {
                ResetZoom();
            }
            else
            {
                UpdateZoomDisplay();
            }

            Application.Current.Dispatcher.InvokeAsync(UpdateMarkdownPageWidth, System.Windows.Threading.DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error setting Markdown content: {ex.Message}");
            ErrorLogger.LogError(ex, "Setting Markdown Content");
            _markdownViewer.Markdown = $"# Error Rendering Markdown\n\nAn error occurred while rendering the Markdown content.\n\n**Details:**\n```\n{ex.Message}\n```";
        }
    }

    public void ToggleView()
    {
        try
        {
            _isMarkdownViewActive = !_isMarkdownViewActive;

            if (_isMarkdownViewActive)
            {
                _rawTextBox.Visibility = Visibility.Collapsed;
                _markdownScrollViewer.Visibility = Visibility.Visible;

                // Re-render Markdown as normal, no custom renderer
                var processedMarkdown = PreprocessMarkdown(_rawTextBox.Text);
                _markdownViewer.Markdown = processedMarkdown;

                _loggingService.LogOperation("Switched to markdown view (re-rendered).");

                Application.Current.Dispatcher.InvokeAsync(UpdateMarkdownPageWidth, System.Windows.Threading.DispatcherPriority.Background);
            }
            else
            {
                _rawTextBox.Visibility = Visibility.Visible;
                _markdownScrollViewer.Visibility = Visibility.Collapsed;
                _loggingService.LogOperation("Switched to raw text view (edit mode)");
            }

            UpdateZoomDisplay();
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error toggling markdown view: {ex.Message}");
            ErrorLogger.LogError(ex, "Toggling markdown view");
            MessageBox.Show("An error occurred while toggling the markdown view.", "View Error", MessageBoxButton.OK, MessageBoxImage.Error);

            // Fallback to raw text view
            _rawTextBox.Visibility = Visibility.Visible;
            _markdownScrollViewer.Visibility = Visibility.Collapsed;
            _isMarkdownViewActive = false;
            UpdateZoomDisplay();
        }
    }

    public void ZoomIn()
    {
        _currentZoomLevel = Math.Min(MaxZoomLevel, _currentZoomLevel + 10.0);
        UpdateZoomDisplay();
    }

    public void ZoomOut()
    {
        _currentZoomLevel = Math.Max(MinZoomLevel, _currentZoomLevel - 10.0);
        UpdateZoomDisplay();
    }

    public void ResetZoom()
    {
        _currentZoomLevel = DefaultZoomLevel;

        // Actually use the _textBoxDefaultFontSize field
        if (!_isMarkdownViewActive)
        {
            _rawTextBox.FontSize = _textBoxDefaultFontSize;
        }

        UpdateZoomDisplay();
    }

    public void UpdateMarkdownPageWidth()
    {
        try
        {
            if (_markdownViewer.Document == null || !_isMarkdownViewActive) return;

            // Use ScrollViewer's ActualWidth if available and > 0, otherwise a sensible default
            var containerWidth = _markdownScrollViewer.ActualWidth > 20 ? _markdownScrollViewer.ActualWidth : 800;
            // Calculate width, ensuring it's reasonable (e.g., at least 600px)
            var contentWidth = Math.Max(600, containerWidth - 40); // Subtract some padding/margin space

            _markdownViewer.Document.PageWidth = contentWidth;
            _markdownViewer.Document.PagePadding = new Thickness(10); // Consistent padding
            _markdownViewer.Document.TextAlignment = TextAlignment.Left;
            // _loggingService.LogOperation($"Updated Markdown Page Width: {contentWidth}");
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error setting Markdown document properties: {ex.Message}");
            // Avoid logging errors repeatedly during layout passes
            // ErrorLogger.LogErrorSilently(ex, "Setting Markdown Page Width");
        }
    }

    private void UpdateZoomDisplay()
    {
        // Clamp the zoom level
        _currentZoomLevel = Math.Max(MinZoomLevel, Math.Min(MaxZoomLevel, _currentZoomLevel));
        var scaleFactor = _currentZoomLevel / 100.0;

        // Apply zoom using LayoutTransform to the appropriate container
        if (_isMarkdownViewActive)
        {
            // Apply to the MarkdownViewer itself or its direct container if needed
            _markdownViewer.LayoutTransform = new ScaleTransform(scaleFactor, scaleFactor);
            _rawTextBox.LayoutTransform = Transform.Identity; // Reset transform on hidden element
        }
        else
        {
            _rawTextBox.LayoutTransform = new ScaleTransform(scaleFactor, scaleFactor);
            _markdownViewer.LayoutTransform = Transform.Identity; // Reset transform on hidden element
        }

        // Update the TextBlock display
        _zoomLevelDisplay.Text = $"{_currentZoomLevel:F0}%";
        // _loggingService.LogOperation($"Zoom updated to {_currentZoomLevel:F0}%");

        // Re-calculate page width after zoom changes might affect layout
        if (_isMarkdownViewActive)
        {
            Application.Current.Dispatcher.InvokeAsync(UpdateMarkdownPageWidth, System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}