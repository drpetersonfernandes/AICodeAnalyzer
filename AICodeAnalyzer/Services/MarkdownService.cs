using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using AICodeAnalyzer.Markdown;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Wpf;
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

        var pipeline = CreateCustomPipeline();
        _markdownViewer.Pipeline = pipeline;

        UpdateZoomDisplay();
    }

    private MarkdownPipeline CreateCustomPipeline()
    {
        _loggingService.LogOperation("Creating custom Markdown pipeline with copy button renderer.");
        var pipelineBuilder = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions();

        // Build the pipeline first
        var pipeline = pipelineBuilder.Build();

        // Get the renderer and modify its renderers collection
        var renderer = new WpfRenderer(new FlowDocument());
        renderer.ObjectRenderers.RemoveAll(r => r is CodeBlockRenderer);
        renderer.ObjectRenderers.Add(new CodeBlockRendererWithCopyButton());

        return pipeline;
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
            // Get content before the last ```
            markdownContent = contentAfterFirstLine.Substring(0, lastTripleBacktick).Trim();
            _loggingService.LogOperation("Preprocessed markdown response (removed ```markdown wrapper).");
            return markdownContent; // Return the processed content
        }

        // If no closing ``` found, maybe just remove the first line? Or return as is?
        // Let's just remove the first line for now if closing ``` isn't found.
        markdownContent = contentAfterFirstLine.Trim();
        _loggingService.LogOperation("Preprocessed markdown response (removed opening ```markdown line, no closing ``` found).");
        return markdownContent;
    }


    public void SetContent(string rawContent, bool resetZoom = false)
    {
        try
        {
            var processedMarkdown = PreprocessMarkdown(rawContent);

            _rawTextBox.Text = rawContent; // Keep raw text in textbox

            // Assign Markdown to the viewer; it will use the custom pipeline
            _markdownViewer.Markdown = processedMarkdown;
            _loggingService.LogOperation($"Rendering Markdown content ({processedMarkdown.Length} chars).");

            if (resetZoom)
            {
                ResetZoom(); // Use the method to reset zoom properly
            }
            else
            {
                // Apply existing zoom settings even if not resetting
                UpdateZoomDisplay();
            }

            // Update page width after content is likely rendered
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
                // Switch to Markdown view
                _rawTextBox.Visibility = Visibility.Collapsed;
                _markdownScrollViewer.Visibility = Visibility.Visible;

                // Re-render the Markdown content from the raw text box
                var processedMarkdown = PreprocessMarkdown(_rawTextBox.Text);
                _markdownViewer.Markdown = processedMarkdown; // Assign again to trigger re-render
                _loggingService.LogOperation("Switched to markdown view (re-rendered).");

                // Update page width after rendering
                Application.Current.Dispatcher.InvokeAsync(UpdateMarkdownPageWidth, System.Windows.Threading.DispatcherPriority.Background);
            }
            else
            {
                // Switch to raw text view
                _rawTextBox.Visibility = Visibility.Visible;
                _markdownScrollViewer.Visibility = Visibility.Collapsed;
                _loggingService.LogOperation("Switched to raw text view (edit mode)");
            }

            UpdateZoomDisplay(); // Apply zoom to the currently visible view
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
            UpdateZoomDisplay(); // Ensure zoom applies correctly to fallback view
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
        _loggingService.LogOperation($"Zoom updated to {_currentZoomLevel:F0}%");

        // Re-calculate page width after zoom changes might affect layout
        if (_isMarkdownViewActive)
        {
            Application.Current.Dispatcher.InvokeAsync(UpdateMarkdownPageWidth, System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}