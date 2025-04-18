using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AICodeAnalyzer.Models;
using System.Linq;

namespace AICodeAnalyzer;

public partial class ConfigurationWindow
{
    private readonly SettingsManager _settingsManager;
    private ApplicationSettings _workingSettings;
    private CodePrompt? _currentPrompt;
    private bool _isCurrentlyRegistered;

    private readonly ApiKeyManager _apiKeyManager = new();
    private readonly ApiProviderFactory _apiProviderFactory = new();
    private readonly Dictionary<string, List<ApiKeyItem>> _providerKeysMap = new();
    private string _currentProvider = string.Empty;

    public ConfigurationWindow(SettingsManager settingsManager)
    {
        InitializeComponent();

        _settingsManager = settingsManager;

        // Create a clone of the current settings to work with
        _workingSettings = new ApplicationSettings
        {
            MaxFileSizeKb = _settingsManager.Settings.MaxFileSizeKb,
            SourceFileExtensions = [.._settingsManager.Settings.SourceFileExtensions],
            SelectedPromptName = _settingsManager.Settings.SelectedPromptName
        };

        // Clone the prompt list without duplicates
        var uniquePromptNames = new HashSet<string>();

        // First pass to collect all prompt names and add them to a HashSet for uniqueness check
        foreach (var prompt in _settingsManager.Settings.CodePrompts)
        {
            uniquePromptNames.Add(prompt.Name);
        }

        // Second pass to add each unique prompt once
        foreach (var promptName in uniquePromptNames)
        {
            // Get the first prompt with this name
            var originalPrompt = _settingsManager.Settings.CodePrompts
                .FirstOrDefault(p => p.Name == promptName);

            if (originalPrompt != null)
            {
                _workingSettings.CodePrompts.Add(
                    new CodePrompt(originalPrompt.Name, originalPrompt.Content));
            }
        }

        // If there are no prompts, add the default one
        if (_workingSettings.CodePrompts.Count == 0)
        {
            _workingSettings.CodePrompts.Add(new CodePrompt("Analyze Source Code", _workingSettings.InitialPrompt));
            _workingSettings.SelectedPromptName = "Analyze Source Code";
        }

        LoadSettingsToUi();

        // Initialize the API Keys tab
        InitializeApiKeysTab();
    }

    private void InitializeFileAssociationTab()
    {
        // Check if the application is currently registered
        _isCurrentlyRegistered = IsApplicationRegisteredForMdFiles();

        // Set the checkbox state based on the ACTUAL current registration status
        ChkRegisterFileAssociation.IsChecked = _isCurrentlyRegistered;

        // Also update the working settings to match the actual state
        // This ensures the setting reflects reality if the user doesn't change anything
        _workingSettings.RegisterAsDefaultMdHandler = _isCurrentlyRegistered;

        // Update the status text
        UpdateFileAssociationStatus();
    }

    private void LoadSettingsToUi()
    {
        // Set file size
        SliderMaxFileSize.Value = _workingSettings.MaxFileSizeKb;
        UpdateFileSizeDisplay();

        // Load extensions
        LbExtensions.ItemsSource = null; // Clear first to force refresh
        LbExtensions.ItemsSource = _workingSettings.SourceFileExtensions;

        // Ensure no duplicates in prompt templates list - a more aggressive approach
        var uniquePrompts = new Dictionary<string, CodePrompt>();
        foreach (var prompt in _workingSettings.CodePrompts)
        {
            uniquePrompts.TryAdd(prompt.Name, prompt);
        }

        // Replace existing prompts with the unique set
        _workingSettings.CodePrompts.Clear();
        foreach (var prompt in uniquePrompts.Values)
        {
            _workingSettings.CodePrompts.Add(prompt);
        }

        // Manually create the items instead of binding directly
        CboPromptTemplates.Items.Clear();
        foreach (var prompt in _workingSettings.CodePrompts)
        {
            CboPromptTemplates.Items.Add(prompt);
        }

        // Select the current prompt
        if (!string.IsNullOrEmpty(_workingSettings.SelectedPromptName))
        {
            var selectedPrompt = _workingSettings.CodePrompts.FirstOrDefault(p =>
                p.Name == _workingSettings.SelectedPromptName);

            if (selectedPrompt != null)
            {
                // Find the actual item in the ComboBox that matches the prompt
                for (var i = 0; i < CboPromptTemplates.Items.Count; i++)
                {
                    if (CboPromptTemplates.Items[i] is not CodePrompt item || item.Name != selectedPrompt.Name)
                        continue;

                    CboPromptTemplates.SelectedIndex = i;
                    _currentPrompt = item;
                    TxtInitialPrompt.Text = item.Content;
                    break;
                }
            }
            else if (CboPromptTemplates.Items.Count > 0)
            {
                CboPromptTemplates.SelectedIndex = 0;
                _currentPrompt = CboPromptTemplates.Items[0] as CodePrompt;
                if (_currentPrompt != null)
                {
                    TxtInitialPrompt.Text = _currentPrompt.Content;
                }
            }
        }
        else if (CboPromptTemplates.Items.Count > 0)
        {
            CboPromptTemplates.SelectedIndex = 0;
            _currentPrompt = CboPromptTemplates.Items[0] as CodePrompt;
            if (_currentPrompt != null)
            {
                TxtInitialPrompt.Text = _currentPrompt.Content;
            }
        }

        UpdatePromptButtons();
        InitializeFileAssociationTab();
    }

    private static bool IsApplicationRegisteredForMdFiles()
    {
        try
        {
            var fileAssociationManager = new FileAssociationManager(static message => System.Diagnostics.Debug.WriteLine($"INFO: {message}"), static message => System.Diagnostics.Debug.WriteLine($"ERROR: {message}")
            );

            return fileAssociationManager.IsApplicationRegistered();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking file association: {ex.Message}");
            ErrorLogger.LogError(ex, $"Error checking file association: {ex.Message}");

            return false;
        }
    }

    private void UpdateFileAssociationStatus()
    {
        if (_isCurrentlyRegistered)
        {
            TxtAssociationStatus.Text = "Status: AI Code Analyzer is currently registered as the default application for .md files.";
            TxtAssociationStatus.Foreground = System.Windows.Media.Brushes.Green;
        }
        else
        {
            TxtAssociationStatus.Text = "Status: AI Code Analyzer is NOT currently registered as the default application for .md files.";
            TxtAssociationStatus.Foreground = System.Windows.Media.Brushes.Red;
        }
    }

    private void ChkRegisterFileAssociation_CheckedChanged(object sender, RoutedEventArgs e)
    {
        // Update the working settings
        _workingSettings.RegisterAsDefaultMdHandler = ChkRegisterFileAssociation.IsChecked == true;
    }

    private void UpdateFileSizeDisplay()
    {
        TxtMaxFileSize.Text = $"{SliderMaxFileSize.Value:F0} KB";
    }

    private void SliderMaxFileSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtMaxFileSize == null) return; // Check for null in case the event fires during initialization

        UpdateFileSizeDisplay();
        _workingSettings.MaxFileSizeKb = (int)SliderMaxFileSize.Value;
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
        if (string.IsNullOrWhiteSpace(extension)) return;
        // Ensure it starts with a dot
        if (!extension.StartsWith('.'))
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

    private void BtnRemoveExtension_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: string extension }) return;

        _workingSettings.SourceFileExtensions.Remove(extension);

        // Refresh the list
        LbExtensions.ItemsSource = null;
        LbExtensions.ItemsSource = _workingSettings.SourceFileExtensions;
    }

    private void UpdatePromptButtons()
    {
        var hasPrompts = _workingSettings.CodePrompts.Count > 0;
        var isDefaultSelected = _currentPrompt?.Name == "Analyze Source Code";

        // Always enable New button
        BtnNewPrompt.IsEnabled = true;

        // Enable Rename and Delete only if there's a selection and it's not the Default prompt
        BtnRenamePrompt.IsEnabled = hasPrompts && !isDefaultSelected;

        // Only allow deleting non-default prompts and if there's more than one prompt
        BtnDeletePrompt.IsEnabled = hasPrompts && !isDefaultSelected && _workingSettings.CodePrompts.Count > 1;
    }

    private void TxtInitialPrompt_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Update the current prompt content
        if (_currentPrompt != null)
        {
            _currentPrompt.Content = TxtInitialPrompt.Text;
        }
    }

    private void CboPromptTemplates_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CboPromptTemplates.SelectedItem is not CodePrompt selectedPrompt) return;

        _currentPrompt = selectedPrompt;
        _workingSettings.SelectedPromptName = selectedPrompt.Name;
        TxtInitialPrompt.Text = selectedPrompt.Content;

        // Update button states
        UpdatePromptButtons();
    }

    private void BtnNewPrompt_Click(object sender, RoutedEventArgs e)
    {
        // Show dialog to enter new prompt name
        var promptName = ShowPromptNameDialog("New Prompt Template", "Enter name for the new prompt template:");

        if (string.IsNullOrEmpty(promptName)) return;
        // Check if name already exists
        if (_workingSettings.CodePrompts.Any(p => p.Name == promptName))
        {
            MessageBox.Show($"A prompt template named '{promptName}' already exists.",
                "Duplicate Name", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Create new prompt with empty content
        var newPrompt = new CodePrompt(promptName, "");
        _workingSettings.CodePrompts.Add(newPrompt);

        // Refresh combobox
        RefreshPromptsComboBox();

        // Select the new prompt
        CboPromptTemplates.SelectedItem = newPrompt;
    }

    private void BtnRenamePrompt_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPrompt == null || _currentPrompt.Name == "Analyze Source Code")
            return;

        // Show dialog to enter new name
        var newName = ShowPromptNameDialog("Rename Prompt Template",
            "Enter new name for this prompt template:", _currentPrompt.Name);

        if (string.IsNullOrEmpty(newName) || newName == _currentPrompt.Name) return;
        // Check if name already exists
        if (_workingSettings.CodePrompts.Any(p => p.Name == newName))
        {
            MessageBox.Show($"A prompt template named '{newName}' already exists.",
                "Duplicate Name", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Rename the prompt
        _currentPrompt.Name = newName;

        // Update selected prompt name if needed
        if (_workingSettings.SelectedPromptName == _currentPrompt.Name)
        {
            _workingSettings.SelectedPromptName = newName;
        }

        // Refresh combobox
        RefreshPromptsComboBox();

        // Reselect the renamed prompt
        CboPromptTemplates.SelectedItem = _currentPrompt;
    }

    private void BtnDeletePrompt_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPrompt == null || _currentPrompt.Name == "Analyze Source Code" || _workingSettings.CodePrompts.Count <= 1)
            return;

        // Confirm deletion
        var result = MessageBox.Show($"Are you sure you want to delete the prompt template '{_currentPrompt.Name}'?",
            "Delete Prompt Template", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;
        // If we're deleting the currently selected prompt, select the Default or first available
        var needToUpdateSelected = _workingSettings.SelectedPromptName == _currentPrompt.Name;

        // Remove the prompt
        _workingSettings.CodePrompts.Remove(_currentPrompt);

        // Update selected prompt if needed
        if (needToUpdateSelected)
        {
            var defaultPrompt = _workingSettings.CodePrompts.FirstOrDefault(static p => p.Name == "Analyze Source Code");
            _workingSettings.SelectedPromptName = defaultPrompt?.Name ?? _workingSettings.CodePrompts.FirstOrDefault()?.Name;
        }

        // Refresh combobox and select appropriate item
        RefreshPromptsComboBox();

        if (!needToUpdateSelected) return;

        {
            var newSelected = _workingSettings.CodePrompts.FirstOrDefault(p => p.Name == _workingSettings.SelectedPromptName);
            CboPromptTemplates.SelectedItem = newSelected;
        }
    }

    private void RefreshPromptsComboBox()
    {
        var selectedName = _currentPrompt?.Name;

        // Ensure no duplicates in prompts collection
        var uniquePrompts = new Dictionary<string, CodePrompt>();
        foreach (var prompt in _workingSettings.CodePrompts)
        {
            uniquePrompts.TryAdd(prompt.Name, prompt);
        }

        // Replace the existing prompts with unique prompts
        _workingSettings.CodePrompts.Clear();
        foreach (var prompt in uniquePrompts.Values)
        {
            _workingSettings.CodePrompts.Add(prompt);
        }

        // Manually populate the ComboBox
        CboPromptTemplates.Items.Clear();
        foreach (var prompt in _workingSettings.CodePrompts)
        {
            CboPromptTemplates.Items.Add(prompt);
        }

        // Reselect the previously selected item if possible
        if (!string.IsNullOrEmpty(selectedName))
        {
            for (var i = 0; i < CboPromptTemplates.Items.Count; i++)
            {
                if (CboPromptTemplates.Items[i] is not CodePrompt item || item.Name != selectedName) continue;

                CboPromptTemplates.SelectedIndex = i;
                _currentPrompt = item;
                break;
            }
        }
        else if (CboPromptTemplates.Items.Count > 0)
        {
            CboPromptTemplates.SelectedIndex = 0;
            _currentPrompt = CboPromptTemplates.Items[0] as CodePrompt;
        }

        UpdatePromptButtons();
    }

    private string ShowPromptNameDialog(string title, string message, string defaultValue = "")
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize
        };

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = message, Margin = new Thickness(0, 0, 0, 10) });

        var textBox = new TextBox
        {
            Text = defaultValue,
            Margin = new Thickness(0, 0, 0, 20),
            Padding = new Thickness(5)
        };
        panel.Children.Add(textBox);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var okButton = new Button
        {
            Content = "OK",
            Padding = new Thickness(15, 5, 15, 5),
            Margin = new Thickness(0, 0, 10, 0),
            IsDefault = true
        };
        okButton.Click += (_, _) => { dialog.DialogResult = true; };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Padding = new Thickness(15, 5, 15, 5),
            IsCancel = true
        };

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        panel.Children.Add(buttonPanel);

        dialog.Content = panel;
        textBox.Focus();
        textBox.SelectAll();

        return dialog.ShowDialog() == true ? textBox.Text.Trim() : string.Empty;
    }

    private void BtnRestoreDefaultPrompt_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPrompt == null)
            return;

        // Find or create the default prompt template for reference
        var defaultSettings = new ApplicationSettings();
        var defaultPromptText = defaultSettings.InitialPrompt;

        // Update the current prompt text to default
        _currentPrompt.Content = defaultPromptText;
        TxtInitialPrompt.Text = defaultPromptText;
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Are you sure you want to reset all settings to default values?",
            "Reset Settings", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        _workingSettings = new ApplicationSettings(); // Create fresh defaults
        LoadSettingsToUi();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        // Apply changes to the actual settings
        _settingsManager.Settings.MaxFileSizeKb = _workingSettings.MaxFileSizeKb;
        _settingsManager.Settings.SourceFileExtensions = [.._workingSettings.SourceFileExtensions];

        // Update prompts
        _settingsManager.Settings.CodePrompts.Clear();
        foreach (var prompt in _workingSettings.CodePrompts)
        {
            _settingsManager.Settings.CodePrompts.Add(new CodePrompt(prompt.Name, prompt.Content));
        }

        // Update selected prompt
        _settingsManager.Settings.SelectedPromptName = _workingSettings.SelectedPromptName;

        // Save to file
        _settingsManager.SaveSettings();

        if (_workingSettings.RegisterAsDefaultMdHandler != _isCurrentlyRegistered)
        {
            // Registration state needs to be changed
            var app = Application.Current as App;
            if (_workingSettings.RegisterAsDefaultMdHandler)
            {
                app?.RegisterFileAssociation();
                _isCurrentlyRegistered = true;
            }
            else
            {
                app?.UnregisterFileAssociation();
                _isCurrentlyRegistered = false;
            }

            // Update UI
            UpdateFileAssociationStatus();
        }

        DialogResult = true;

        RestartApplication.Restart();

        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void InitializeApiKeysTab()
    {
        // Populate provider dropdown
        CboApiProviders.Items.Clear();
        foreach (var provider in _apiProviderFactory.AllProviders.OrderBy(static p => p.Name))
        {
            CboApiProviders.Items.Add(provider.Name);
        }

        // Load all keys for all providers
        LoadAllProviderKeys();

        // Default selects first provider if available
        if (CboApiProviders.Items.Count > 0)
        {
            CboApiProviders.SelectedIndex = 0;
        }
    }

    private void LoadAllProviderKeys()
    {
        _providerKeysMap.Clear();

        foreach (var provider in _apiProviderFactory.AllProviders)
        {
            // Get keys for this provider
            var keys = _apiKeyManager.GetKeysForProvider(provider.Name);

            // Create ApiKeyItem objects for display
            var keyItems = new List<ApiKeyItem>();
            foreach (var k in keys)
            {
                if (!string.IsNullOrEmpty(k))
                {
                    keyItems.Add(new ApiKeyItem
                    {
                        Key = MaskKey(k),
                        ActualKey = k
                    });
                }
            }

            // Add to map
            _providerKeysMap[provider.Name] = keyItems;
        }
    }

    private static string MaskKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        if (key.Length <= 8)
            return "****";

        return $"{key[..4]}...{key[^4..]}";
    }

    private void ApiProviders_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
    {
        if (CboApiProviders.SelectedItem is string selectedProvider)
        {
            _currentProvider = selectedProvider;

            // Update ListView with keys for this provider
            if (_providerKeysMap.TryGetValue(_currentProvider, out var keys))
            {
                LvApiKeys.ItemsSource = keys;
            }
            else
            {
                LvApiKeys.ItemsSource = new List<ApiKeyItem>(); // Empty list instead of null
            }
        }
        else
        {
            // Default to an empty list if no provider is selected
            LvApiKeys.ItemsSource = new List<ApiKeyItem>();
        }
    }

    private void BtnAddKey_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentProvider))
        {
            MessageBox.Show("Please select a provider first.", "No Provider Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var newKey = TxtNewApiKey.Password.Trim();

        if (string.IsNullOrWhiteSpace(newKey))
        {
            MessageBox.Show("Please enter an API key.", "Empty Key", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Check if the key already exists
        if (_providerKeysMap.TryGetValue(_currentProvider, out var existingKeys))
        {
            if (existingKeys.Any(k => k.ActualKey == newKey))
            {
                MessageBox.Show("This API key already exists for this provider.", "Duplicate Key", MessageBoxButton.OK, MessageBoxImage.Warning);

                return;
            }
        }

        // Add the key to the manager
        _apiKeyManager.SaveKey(_currentProvider, newKey);

        // Refresh UI
        LoadAllProviderKeys();
        ApiProviders_SelectionChanged(null, null);

        // Clear the input field
        TxtNewApiKey.Password = string.Empty;
    }

    private void BtnRemoveKey_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ApiKeyItem keyItem } ||
            string.IsNullOrEmpty(keyItem.ActualKey)) return;

        // Ask for confirmation
        var result = MessageBox.Show(
            "Are you sure you want to remove this API key?",
            "Confirm Removal",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        // Remove the key using our new method
        if (_apiKeyManager.RemoveKey(_currentProvider, keyItem.ActualKey))
        {
            // Refresh UI
            LoadAllProviderKeys();
            ApiProviders_SelectionChanged(null, null);
        }
        else
        {
            MessageBox.Show("Failed to remove the API key.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}