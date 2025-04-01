using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AICodeAnalyzer.Models;

namespace AICodeAnalyzer;

public partial class ConfigurationWindow
{
    private readonly SettingsManager _settingsManager;
    private ApplicationSettings _workingSettings;
    
    public ConfigurationWindow(SettingsManager settingsManager)
    {
        InitializeComponent();
        
        _settingsManager = settingsManager;
        
        // Create a clone of the current settings to work with
        _workingSettings = new ApplicationSettings
        {
            MaxFileSizeKb = _settingsManager.Settings.MaxFileSizeKb,
            SourceFileExtensions = new List<string>(_settingsManager.Settings.SourceFileExtensions),
            InitialPrompt = _settingsManager.Settings.InitialPrompt
        };
        
        LoadSettingsToUi();
    }
    
    private void LoadSettingsToUi()
    {
        // Set file size
        SliderMaxFileSize.Value = _workingSettings.MaxFileSizeKb;
        UpdateFileSizeDisplay();
        
        // Load extensions
        LbExtensions.ItemsSource = null; // Clear first to force refresh
        LbExtensions.ItemsSource = _workingSettings.SourceFileExtensions;
        
        // Load initial prompt
        TxtInitialPrompt.Text = _workingSettings.InitialPrompt;
    }
    
    private void UpdateFileSizeDisplay()
    {
        TxtMaxFileSize.Text = $"{SliderMaxFileSize.Value:F0} KB";
    }
    
    private void SliderMaxFileSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtMaxFileSize != null) // Check for null in case the event fires during initialization
        {
            UpdateFileSizeDisplay();
            _workingSettings.MaxFileSizeKb = (int)SliderMaxFileSize.Value;
        }
    }
    
    private void BtnAddExtension_Click(object sender, RoutedEventArgs e)
    {
        AddNewExtension();
    }
    
    private void TxtNewExtension_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AddNewExtension();
        }
    }
    
    private void AddNewExtension()
    {
        var extension = TxtNewExtension.Text.Trim();
        
        // Validate and format the extension
        if (!string.IsNullOrWhiteSpace(extension))
        {
            // Ensure it starts with a dot
            if (!extension.StartsWith("."))
            {
                extension = "." + extension;
            }
            
            // Convert to lowercase for consistency
            extension = extension.ToLowerInvariant();
            
            // Check if it already exists
            if (!_workingSettings.SourceFileExtensions.Contains(extension))
            {
                _workingSettings.SourceFileExtensions.Add(extension);
                _workingSettings.SourceFileExtensions.Sort(); // Keep the list sorted
                
                // Refresh the list
                LbExtensions.ItemsSource = null;
                LbExtensions.ItemsSource = _workingSettings.SourceFileExtensions;
                
                // Clear the input
                TxtNewExtension.Text = string.Empty;
            }
            else
            {
                MessageBox.Show($"The extension '{extension}' already exists in the list.", 
                    "Duplicate Extension", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
    
    private void BtnRemoveExtension_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is string extension)
        {
            _workingSettings.SourceFileExtensions.Remove(extension);
            
            // Refresh the list
            LbExtensions.ItemsSource = null;
            LbExtensions.ItemsSource = _workingSettings.SourceFileExtensions;
        }
    }
    
    private void TxtInitialPrompt_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Update the working settings with the current prompt text
        _workingSettings.InitialPrompt = TxtInitialPrompt.Text;
    }
    
    private void BtnRestoreDefaultPrompt_Click(object sender, RoutedEventArgs e)
    {
        // Create a new settings object to get the default prompt
        var defaultSettings = new ApplicationSettings();
        
        // Set the prompt text to the default
        TxtInitialPrompt.Text = defaultSettings.InitialPrompt;
        _workingSettings.InitialPrompt = defaultSettings.InitialPrompt;
    }
    
    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Are you sure you want to reset all settings to default values?", 
            "Reset Settings", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
        if (result == MessageBoxResult.Yes)
        {
            _workingSettings = new ApplicationSettings(); // Create fresh defaults
            LoadSettingsToUi();
        }
    }
    
    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        // Apply changes to the actual settings
        _settingsManager.Settings.MaxFileSizeKb = _workingSettings.MaxFileSizeKb;
        _settingsManager.Settings.SourceFileExtensions = new List<string>(_workingSettings.SourceFileExtensions);
        _settingsManager.Settings.InitialPrompt = _workingSettings.InitialPrompt;
        
        // Save to file
        _settingsManager.SaveSettings();
        
        DialogResult = true;
        Close();
    }
    
    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}