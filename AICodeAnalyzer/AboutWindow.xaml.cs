using System.Windows;

namespace AICodeAnalyzer;

public partial class AboutWindow
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}