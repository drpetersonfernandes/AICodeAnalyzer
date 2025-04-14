using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Markdig.Wpf;

namespace AICodeAnalyzer.Services;

public class MarkdownService(
    LoggingService loggingService,
    TextBox rawTextBox,
    MarkdownViewer markdownViewer,
    ScrollViewer markdownScrollViewer,
    TextBlock zoomLevelDisplay)
{
    private readonly LoggingService _loggingService = loggingService;
    private const double DefaultZoomLevel = 100.0;
    private const double MinZoomLevel = 20.0;
    private const double MaxZoomLevel = 500.0;

    private double _currentZoomLevel = 100.0;
    private readonly double _textBoxDefaultFontSize = rawTextBox.FontSize;

    private readonly TextBox _rawTextBox = rawTextBox;
    private readonly MarkdownViewer _markdownViewer = markdownViewer;
    private readonly ScrollViewer _markdownScrollViewer = markdownScrollViewer;
    private readonly TextBlock _zoomLevelDisplay = zoomLevelDisplay;

    private bool _isMarkdownViewActive = true;

    public bool IsMarkdownViewActive => _isMarkdownViewActive;
    public double CurrentZoomLevel => _currentZoomLevel;

    private string PreprocessMarkdown(string markdownContent)
    {
        if (string.IsNullOrEmpty(markdownContent))
            return markdownContent;

        // Fix for responses that start with ```Markdown by removing that line
        // This prevents the entire content from being treated as a code block
        if (markdownContent.StartsWith("```markdown", StringComparison.Ordinal) ||
            markdownContent.StartsWith("```Markdown", StringComparison.Ordinal))
        {
            // Find the first line break after the ```Markdown
            var lineBreakIndex = markdownContent.IndexOf('\n');
            if (lineBreakIndex > 0)
            {
                // Remove the ```markdown line
                markdownContent = markdownContent.Substring(lineBreakIndex + 1);

                // Look for the closing ``` and remove it as well
                var closingBackticksIndex = markdownContent.LastIndexOf("```", StringComparison.Ordinal);
                if (closingBackticksIndex >= 0)
                {
                    markdownContent = markdownContent.Substring(0, closingBackticksIndex).TrimEnd();
                }

                _loggingService.LogOperation("Preprocessed markdown response to fix formatting issues");
            }
        }

        return markdownContent;
    }

    public void SetContent(string rawContent, bool resetZoom = false)
    {
        var processedMarkdown = PreprocessMarkdown(rawContent);

        // Set content in both views
        _rawTextBox.Text = rawContent;
        _markdownViewer.Markdown = processedMarkdown;

        // Reset zoom if requested
        if (resetZoom)
        {
            _currentZoomLevel = DefaultZoomLevel;
        }

        // Apply zoom settings
        UpdateZoomDisplay();
        UpdateMarkdownPageWidth();
    }

    public void ToggleView()
    {
        try
        {
            _isMarkdownViewActive = !_isMarkdownViewActive;

            if (_isMarkdownViewActive)
            {
                // Switch to Markdown view
                _rawTextBox.Visibility = Visibility.Collapsed;
                _markdownScrollViewer.Visibility = Visibility.Visible;

                // Set the Markdown content with preprocessing
                var processedMarkdown = PreprocessMarkdown(_rawTextBox.Text);
                _markdownViewer.Markdown = processedMarkdown;

                // Update the page width
                UpdateMarkdownPageWidth();

                // Log the switch to Markdown view
                _loggingService.LogOperation("Switched to markdown view with preprocessing");
            }
            else
            {
                // Switch to raw text view
                _rawTextBox.Visibility = Visibility.Visible;
                _markdownScrollViewer.Visibility = Visibility.Collapsed;

                // Log the switch to raw text view
                _loggingService.LogOperation("Switched to raw text view (edit mode)");
            }

            // Update zoom display
            UpdateZoomDisplay();
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error toggling markdown view: {ex.Message}");
            ErrorLogger.LogError(ex, "Toggling markdown view");
            MessageBox.Show("An error occurred while toggling the markdown view.", "View Error", MessageBoxButton.OK,
                MessageBoxImage.Error);

            // Revert to a raw text as a fallback
            _rawTextBox.Visibility = Visibility.Visible;
            _markdownScrollViewer.Visibility = Visibility.Collapsed;
            _isMarkdownViewActive = false;
        }
    }

    public void ZoomIn()
    {
        _currentZoomLevel += 10.0; // Increase by 10%
        UpdateZoomDisplay();
    }

    public void ZoomOut()
    {
        _currentZoomLevel -= 10.0; // Decrease by 10%
        UpdateZoomDisplay();
    }

    public void ResetZoom()
    {
        _currentZoomLevel = DefaultZoomLevel;
        UpdateZoomDisplay();
    }

    public void UpdateMarkdownPageWidth()
    {
        try
        {
            // If we can access the internal FlowDocument, set its properties
            if (_markdownViewer.Document != null)
            {
                // Calculate 90% of the available width
                var containerWidth = _markdownScrollViewer.ActualWidth;
                var contentWidth = Math.Max(800, containerWidth * 0.9); // Use at least 800 px or 90% of container

                _markdownViewer.Document.PageWidth = contentWidth;
                _markdownViewer.Document.PagePadding = new Thickness(0);
                _markdownViewer.Document.TextAlignment = TextAlignment.Left;
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error setting Markdown document properties: {ex.Message}");
            ErrorLogger.LogError(ex, $"Error setting Markdown document properties: {ex.Message}");
        }
    }

    private void UpdateZoomDisplay()
    {
        // Clamp the zoom level within min/max bounds
        _currentZoomLevel = Math.Max(MinZoomLevel, Math.Min(MaxZoomLevel, _currentZoomLevel));

        // Apply zoom to Markdown view using ScaleTransform
        _markdownViewer.LayoutTransform = new ScaleTransform(
            _currentZoomLevel / 100.0, // X scale factor
            _currentZoomLevel / 100.0 // Y scale factor
        );

        // For raw text view, adjust font size based on zoom level
        _rawTextBox.FontSize = _textBoxDefaultFontSize * (_currentZoomLevel / 100.0);

        // Update the TextBlock display
        _zoomLevelDisplay.Text = $"{_currentZoomLevel:F0}%";

        // When zooming, we might want to update the page width as well
        UpdateMarkdownPageWidth();
    }
}