using Markdig.Syntax;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using AICodeAnalyzer.Utilities; // Assuming MarkdownHelper is here
using Markdig.Renderers;
using Markdig.Renderers.Wpf;

// Add this alias if needed, though not strictly required by this specific change
// using WpfBlock = System.Windows.Documents.Block;

namespace AICodeAnalyzer.Markdown;

public class CodeBlockRendererWithCopyButton : WpfObjectRenderer<CodeBlock>
{
    protected override void Write(WpfRenderer renderer, CodeBlock obj)
    {
        var codeToCopy = MarkdownHelper.GetCode(obj);

        var copyButton = new Button
        {
            Content = "Copy",
            Tag = codeToCopy,
            Cursor = System.Windows.Input.Cursors.Hand,
            Margin = new Thickness(0, 2, 2, 0),
            Padding = new Thickness(5.0, 2.0, 5.0, 2.0),
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Top, // Still useful for alignment within the docked area
            HorizontalAlignment = HorizontalAlignment.Right, // Will align right within the top docked area
            // Style = Application.Current.TryFindResource("CopyButtonStyle") as Style // Keep style commented out for now
            Background = Brushes.LightBlue // Add a visible background for testing
        };

        // Connect the event handler or use lambda
        copyButton.Click += CopyCodeButton_Click;

        // --- Default rendering logic to get the code block visual ---
        var defaultRenderer = new CodeBlockRenderer();
        var tempFlowDocument = new FlowDocument();
        var tempRenderer = new WpfRenderer(tempFlowDocument);
        defaultRenderer.Write(tempRenderer, obj);

        var defaultCodeBlockVisual = tempFlowDocument.Blocks.FirstOrDefault();

        if (defaultCodeBlockVisual == null)
        {
            // Fallback if default rendering fails
            defaultCodeBlockVisual = new Paragraph(new Run(codeToCopy))
            {
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0),
                Padding = new Thickness(5)
            };
        }
        else
        {
            // Remove from temporary document to avoid parenting issues
            tempFlowDocument.Blocks.Remove(defaultCodeBlockVisual);
        }
        // --- End default rendering logic ---


        // --- Use DockPanel for layout ---
        var dockPanel = new DockPanel();
        var backgroundBrush = Application.Current.TryFindResource("CodeBlockBackgroundBrush")
            as Brush ?? new SolidColorBrush(Color.FromRgb(245, 245, 245));
        dockPanel.Background = backgroundBrush;

        // Dock the button to the top, align right within that docked area
        DockPanel.SetDock(copyButton, Dock.Top);
        // copyButton.HorizontalAlignment is already set to Right above
        dockPanel.Children.Add(copyButton); // Add button first

        // Create the FlowDocument and viewer for the code
        var codeFlowDoc = new FlowDocument(defaultCodeBlockVisual)
        {
            PagePadding = new Thickness(4),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            TextAlignment = TextAlignment.Left
        };
        var codeViewer = new FlowDocumentScrollViewer
        {
            Document = codeFlowDoc,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, // Or Auto if needed
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, // Or Auto if needed
            Padding = new Thickness(0),
            Margin = new Thickness(0),
            Background = Brushes.Transparent // So DockPanel background shows
        };

        // The code viewer will fill the remaining space below the button
        dockPanel.Children.Add(codeViewer);
        // --- End DockPanel ---


        // Create the container that holds our DockPanel
        var container = new BlockUIContainer(dockPanel) // Use DockPanel here
        {
            Margin = new Thickness(0, 2, 0, 8) // Standard margin for blocks
        };

        // Add the container to the main document being rendered
        renderer.WriteBlock(container);
    }

    private static void CopyCodeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string codeToCopy } button) return;

        try
        {
            Clipboard.SetText(codeToCopy);

            // Optional: Provide visual feedback
            var originalContent = button.Content;
            var originalBackground = button.Background; // Store original background
            button.Content = "Copied!";
            button.Background = Brushes.LightGreen; // Change background on success

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1.5)
            };
            timer.Tick += (s, args) =>
            {
                if (button.Content?.ToString() == "Copied!")
                {
                    button.Content = originalContent;
                    button.Background = originalBackground; // Restore original background
                }

                timer.Stop();
            };
            timer.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to copy code to clipboard: {ex.Message}", "Copy Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            ErrorLogger.LogErrorSilently(ex, "Copying code block to clipboard");
        }
    }
}