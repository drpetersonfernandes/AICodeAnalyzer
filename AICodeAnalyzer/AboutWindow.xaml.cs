using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace AICodeAnalyzer;

public partial class AboutWindow
{
    public AboutWindow()
    {
        InitializeComponent();
        DataContext = this;
        AppVersionTextBlock.Text = ApplicationVersion;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            const string contextMessage = "Error in the Hyperlink_RequestNavigate method.";
            ErrorLogger.LogError(ex, contextMessage);

            MessageBox.Show("Unable to open the link.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            // Mark the event as handled, regardless of success or failure
            e.Handled = true;
        }
    }

    private static string ApplicationVersion
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return "Version: " + (version?.ToString() ?? "Unknown");
        }
    }
}