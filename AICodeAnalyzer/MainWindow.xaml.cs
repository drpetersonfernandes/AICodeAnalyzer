using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace AICodeAnalyzer;

public partial class MainWindow
{
    private readonly HttpClient _httpClient;
    private readonly ApiKeyManager _keyManager;
    private string _selectedFolder = string.Empty;
    private readonly Dictionary<string, List<SourceFile>> _filesByExtension;
    private readonly List<ChatMessage> _conversationHistory = new();

    // Define source code file extensions to look for
    private readonly string[] _sourceExtensions = new[] 
    { 
        ".cs", ".xaml", ".java", ".js", ".ts", ".py", ".html", ".css", 
        ".cpp", ".h", ".c", ".go", ".rb", ".php", ".swift", ".kt", 
        ".rs", ".dart", ".scala", ".groovy", ".pl", ".sh", ".bat", 
        ".ps1", ".xml", ".json", ".yaml", ".yml", ".md", ".txt" 
    };

    public MainWindow()
    {
        InitializeComponent();
        _httpClient = new HttpClient();
        _filesByExtension = new Dictionary<string, List<SourceFile>>();
        _keyManager = new ApiKeyManager();
            
        // Populate API dropdown
        CboAiApi.Items.Add("Claude API");
        CboAiApi.Items.Add("ChatGPT API");
        CboAiApi.Items.Add("DeepSeek API");
        CboAiApi.Items.Add("GroK API");
        CboAiApi.Items.Add("Gemini API");
        CboAiApi.SelectedIndex = 0; // Default to Claude
    }

    private void CboAiApi_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CboAiApi.SelectedItem != null)
        {
            var apiSelection = CboAiApi.SelectedItem.ToString() ?? string.Empty;
            UpdatePreviousKeys(apiSelection);
        }
    }

    private void UpdatePreviousKeys(string apiProvider)
    {
        CboPreviousKeys.Items.Clear();
        CboPreviousKeys.Items.Add("Select a saved key");
        
        var savedKeys = _keyManager.GetKeysForProvider(apiProvider);
        foreach (var key in savedKeys)
        {
            CboPreviousKeys.Items.Add(MaskKey(key));
        }
        
        CboPreviousKeys.SelectedIndex = 0;
    }

    private string MaskKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length < 8)
            return "*****";
            
        return key.Substring(0, 3) + "*****" + key.Substring(key.Length - 3);
    }

    private void CboPreviousKeys_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CboPreviousKeys.SelectedIndex <= 0)
            return;
            
        var apiSelection = CboAiApi.SelectedItem?.ToString() ?? string.Empty;
        var savedKeys = _keyManager.GetKeysForProvider(apiSelection);
        
        if (CboPreviousKeys.SelectedIndex - 1 < savedKeys.Count)
        {
            TxtApiKey.Password = savedKeys[CboPreviousKeys.SelectedIndex - 1];
        }
    }

    private void BtnSaveKey_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TxtApiKey.Password))
        {
            MessageBox.Show("Please enter an API key before saving.", "No Key", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        var apiSelection = CboAiApi.SelectedItem?.ToString() ?? string.Empty;
        _keyManager.SaveKey(apiSelection, TxtApiKey.Password);
        UpdatePreviousKeys(apiSelection);
        
        MessageBox.Show("API key saved successfully.", "Key Saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow
        {
            Owner = this
        };
        aboutWindow.ShowDialog();
    }

    private async void BtnSelectFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "Select Project Folder"
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                _selectedFolder = dialog.FileName;
                TxtSelectedFolder.Text = _selectedFolder;
                await ScanFolder();
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.LogError(ex, "Opening folder selection dialog");
            TxtStatus.Text = "Error selecting folder.";
        }
    }

    private async Task ScanFolder()
    {
        if (string.IsNullOrEmpty(_selectedFolder) || !Directory.Exists(_selectedFolder))
            return;

        try
        {
            TxtStatus.Text = "Scanning folder for source files...";
            _filesByExtension.Clear();
            LvFiles.Items.Clear();
                
            // Run the scan in a background task
            await Task.Run(() => FindSourceFiles(_selectedFolder));

            // Display results
            var totalFiles = _filesByExtension.Values.Sum(list => list.Count);
            TxtStatus.Text = $"Found {totalFiles} source files.";

            // Display file stats by extension
            foreach (var ext in _filesByExtension.Keys.OrderBy(k => k))
            {
                var count = _filesByExtension[ext].Count;
                LvFiles.Items.Add($"{ext} - {count} files");
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.LogError(ex, "Scanning folder");
            TxtStatus.Text = "Error scanning folder.";
        }
    }

    private void FindSourceFiles(string folderPath)
    {
        var dirInfo = new DirectoryInfo(folderPath);
            
        // Get all files with source extensions in this directory
        foreach (var file in dirInfo.GetFiles())
        {
            var ext = file.Extension.ToLowerInvariant();
            if (_sourceExtensions.Contains(ext))
            {
                if (!_filesByExtension.ContainsKey(ext))
                    _filesByExtension[ext] = new List<SourceFile>();
                    
                _filesByExtension[ext].Add(new SourceFile
                {
                    Path = file.FullName,
                    RelativePath = file.FullName.Replace(_selectedFolder, "").TrimStart('\\', '/'),
                    Extension = ext,
                    Content = File.ReadAllText(file.FullName)
                });
            }
        }
            
        // Recursively process subdirectories (except hidden and system folders)
        foreach (var dir in dirInfo.GetDirectories())
        {
            // Skip hidden directories, bin, obj, node_modules, etc.
            if (dir.Attributes.HasFlag(FileAttributes.Hidden) || 
                dir.Attributes.HasFlag(FileAttributes.System) ||
                dir.Name.StartsWith(".") ||
                dir.Name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                dir.Name.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                dir.Name.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
                dir.Name.Equals("packages", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
                
            FindSourceFiles(dir.FullName);
        }
    }

    private async void BtnAnalyze_Click(object sender, RoutedEventArgs e)
    {
        if (_filesByExtension.Count == 0)
        {
            MessageBox.Show("Please select a folder and scan for files first.", "No Files", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrEmpty(TxtApiKey.Password))
        {
            MessageBox.Show("Please enter an API key.", "Missing API Key", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var apiSelection = CboAiApi.SelectedItem?.ToString() ?? "Claude API";
        TxtStatus.Text = $"Analyzing with {apiSelection}...";
            
        // Prepare consolidated code files
        var consolidatedFiles = PrepareConsolidatedFiles();
            
        // Begin a new conversation
        _conversationHistory.Clear();

        try
        {
            // Prepare the initial prompt to ask for code analysis
            var initialPrompt = GenerateInitialPrompt(consolidatedFiles);
                
            // Send it to selected API
            var response = await SendToAiApi(apiSelection, initialPrompt);
                
            // Display the response
            TxtResponse.Text = response;
                
            // Update conversation history
            _conversationHistory.Add(new ChatMessage { Role = "user", Content = initialPrompt });
            _conversationHistory.Add(new ChatMessage { Role = "assistant", Content = response });
                
            // Enable follow-up questions
            TxtFollowupQuestion.IsEnabled = true;
            BtnSendFollowup.IsEnabled = true;
            BtnSaveResponse.IsEnabled = true;

            TxtStatus.Text = "Analysis complete!";
        }
        catch (Exception ex)
        {
            ErrorLogger.LogError(ex, "Analyzing code");
            TxtStatus.Text = "Error during analysis.";
        }
    }

    private Dictionary<string, List<string>> PrepareConsolidatedFiles()
    {
        // This dictionary will hold file content grouped by extension
        var consolidatedFiles = new Dictionary<string, List<string>>();
            
        foreach (var ext in _filesByExtension.Keys)
        {
            var files = _filesByExtension[ext];
                
            if (!consolidatedFiles.ContainsKey(ext))
            {
                consolidatedFiles[ext] = new List<string>();
            }
                
            foreach (var file in files)
            {
                // Format each file with a header showing a relative path
                consolidatedFiles[ext].Add($"File: {file.RelativePath}\n```{GetLanguageForExtension(ext)}\n{file.Content}\n```\n");
            }
        }
            
        return consolidatedFiles;
    }

    private string GetLanguageForExtension(string ext)
    {
        return ext switch
        {
            ".cs" => "csharp",
            ".py" => "python",
            ".js" => "javascript",
            ".ts" => "typescript",
            ".java" => "java",
            ".html" => "html",
            ".css" => "css",
            ".cpp" or ".h" or ".c" => "cpp",
            ".go" => "go",
            ".rb" => "ruby",
            ".php" => "php",
            ".swift" => "swift",
            ".kt" => "kotlin",
            ".rs" => "rust",
            ".dart" => "dart",
            ".xaml" => "xml",
            ".xml" => "xml",
            ".json" => "json",
            ".yaml" or ".yml" => "yaml",
            ".md" => "markdown",
            _ => ""
        };
    }

    private string GenerateInitialPrompt(Dictionary<string, List<string>> consolidatedFiles)
    {
        var prompt = new StringBuilder();
            
        // Start with a clear instruction
        prompt.AppendLine("Please analyze the following source code files from my project. I would like you to:");
        prompt.AppendLine("1. Understand the overall structure and purpose of the codebase");
        prompt.AppendLine("2. Identify any bugs, errors, or inconsistencies");
        prompt.AppendLine("3. Highlight potential security vulnerabilities");
        prompt.AppendLine("4. Suggest improvements for code quality and maintainability");
        prompt.AppendLine("5. Provide specific recommendations for the most critical issues");
        prompt.AppendLine();
        prompt.AppendLine("Here are all the files from my project:");
        prompt.AppendLine();

        // Include files, grouped by extension
        foreach (var ext in consolidatedFiles.Keys)
        {
            prompt.AppendLine($"--- {ext.ToUpperInvariant()} FILES ---");
            prompt.AppendLine();
                
            foreach (var fileContent in consolidatedFiles[ext])
            {
                prompt.AppendLine(fileContent);
                prompt.AppendLine();
            }
        }
            
        return prompt.ToString();
    }

    private async Task<string> SendToAiApi(string apiSelection, string prompt)
    {
        // Set up the API request based on the selected API
        switch (apiSelection)
        {
            case "Claude API":
                return await SendToClaudeApi(prompt);
            case "ChatGPT API":
                return await SendToOpenAiApi(prompt);
            case "DeepSeek API":
                return await SendToDeepSeekApi(prompt);
            case "GroK API":
                return await SendToGroKApi(prompt);
            case "Gemini API":
                return await SendToGeminiApi(prompt);
            default:
                throw new NotImplementedException($"API {apiSelection} not implemented");
        }
    }

    private async Task<string> SendToClaudeApi(string prompt)
    {
        var apiKey = TxtApiKey.Password;
        var model = "claude-3-sonnet-20240229"; // Use a recent Claude model
        var apiUrl = "https://api.anthropic.com/v1/messages";
            
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        
        // Properly format the message history for Claude API
        var messages = new List<object>();
        
        // Claude API doesn't use system messages in the same way - they go in the messages' array
        // Add each message from history with the proper format
        foreach (var msg in _conversationHistory)
        {
            messages.Add(new { role = msg.Role, content = msg.Content });
        }
        
        // Add the current prompt
        messages.Add(new { role = "user", content = prompt });
        
        var requestData = new
        {
            model,
            messages,
            max_tokens = 4096
        };
            
        var content = new StringContent(
            JsonSerializer.Serialize(requestData),
            Encoding.UTF8,
            "application/json");
            
        var response = await _httpClient.PostAsync(apiUrl, content);
            
        if (!response.IsSuccessStatusCode)
        {
            var errorText = await response.Content.ReadAsStringAsync();
            throw new Exception($"API error ({response.StatusCode}): {errorText}");
        }
            
        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
            
        return doc.RootElement.GetProperty("content").EnumerateArray()
            .First(x => x.GetProperty("type").GetString() == "text")
            .GetProperty("text").GetString() ?? "No response";
    }

    private async Task<string> SendToOpenAiApi(string prompt)
    {
        var apiKey = TxtApiKey.Password;
        var model = "gpt-4-turbo-preview"; // Use a GPT-4 model
        var apiUrl = "https://api.openai.com/v1/chat/completions";
            
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        
        // Properly format the message history for OpenAI API
        var messages = new List<object>();
        
        // First add a system message if this is the first message
        if (_conversationHistory.Count == 0)
        {
            messages.Add(new { role = "system", content = "You are a helpful assistant specializing in code review and analysis." });
        }
        else
        {
            // Add each message from history with the proper format
            foreach (var msg in _conversationHistory)
            {
                messages.Add(new { role = msg.Role, content = msg.Content });
            }
        }
        
        // Add the current prompt
        messages.Add(new { role = "user", content = prompt });
        
        var requestData = new
        {
            model,
            messages,
            max_tokens = 4096
        };
            
        var content = new StringContent(
            JsonSerializer.Serialize(requestData),
            Encoding.UTF8,
            "application/json");
            
        var response = await _httpClient.PostAsync(apiUrl, content);
            
        if (!response.IsSuccessStatusCode)
        {
            var errorText = await response.Content.ReadAsStringAsync();
            throw new Exception($"API error ({response.StatusCode}): {errorText}");
        }
            
        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
            
        return doc.RootElement.GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content").GetString() ?? "No response";
    }

    private async Task<string> SendToDeepSeekApi(string prompt)
    {
        var apiKey = TxtApiKey.Password;
        var model = "deepseek-coder"; 
        var apiUrl = "https://api.deepseek.com/v1/chat/completions";
            
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        
        // Properly format the message history for DeepSeek API
        var messages = new List<object>();
        
        // First add a system message if this is the first message
        if (_conversationHistory.Count == 0)
        {
            messages.Add(new { role = "system", content = "You are a helpful assistant specializing in code review and analysis." });
        }
        else
        {
            // Add each message from history with the proper format
            foreach (var msg in _conversationHistory)
            {
                messages.Add(new { role = msg.Role, content = msg.Content });
            }
        }
        
        // Add the current prompt
        messages.Add(new { role = "user", content = prompt });
        
        var requestData = new
        {
            model,
            messages,
            max_tokens = 4096
        };
            
        var content = new StringContent(
            JsonSerializer.Serialize(requestData),
            Encoding.UTF8,
            "application/json");
            
        var response = await _httpClient.PostAsync(apiUrl, content);
            
        if (!response.IsSuccessStatusCode)
        {
            var errorText = await response.Content.ReadAsStringAsync();
            throw new Exception($"API error ({response.StatusCode}): {errorText}");
        }
            
        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
            
        return doc.RootElement.GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content").GetString() ?? "No response";
    }

    private async Task<string> SendToGroKApi(string prompt)
    {
        var apiKey = TxtApiKey.Password;
        var model = "grok-1"; 
        var apiUrl = "https://api.grok.x/v1/chat/completions";
            
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        
        // Properly format the message history for Grok API
        var messages = new List<object>();
        
        // First add a system message if this is the first message
        if (_conversationHistory.Count == 0)
        {
            messages.Add(new { role = "system", content = "You are a helpful assistant specializing in code review and analysis." });
        }
        else
        {
            // Add each message from history with the proper format
            foreach (var msg in _conversationHistory)
            {
                messages.Add(new { role = msg.Role, content = msg.Content });
            }
        }
        
        // Add the current prompt
        messages.Add(new { role = "user", content = prompt });
        
        var requestData = new
        {
            model,
            messages,
            max_tokens = 4096
        };
            
        var content = new StringContent(
            JsonSerializer.Serialize(requestData),
            Encoding.UTF8,
            "application/json");
            
        var response = await _httpClient.PostAsync(apiUrl, content);
            
        if (!response.IsSuccessStatusCode)
        {
            var errorText = await response.Content.ReadAsStringAsync();
            throw new Exception($"API error ({response.StatusCode}): {errorText}");
        }
            
        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
            
        return doc.RootElement.GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content").GetString() ?? "No response";
    }

    private async Task<string> SendToGeminiApi(string prompt)
    {
        var apiKey = TxtApiKey.Password;
        var model = "gemini-pro"; 
        var apiUrl = $"https://generativelanguage.googleapis.com/v1/models/{model}:generateContent?key={apiKey}";
            
        _httpClient.DefaultRequestHeaders.Clear();
        
        // Gemini has a different API structure, so we need to handle it differently
        // It doesn't support full conversation history in the same way
        // We'll simplify by just sending the current prompt
        var requestData = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                maxOutputTokens = 4096,
                temperature = 0.7
            }
        };
            
        var content = new StringContent(
            JsonSerializer.Serialize(requestData),
            Encoding.UTF8,
            "application/json");
            
        var response = await _httpClient.PostAsync(apiUrl, content);
            
        if (!response.IsSuccessStatusCode)
        {
            var errorText = await response.Content.ReadAsStringAsync();
            throw new Exception($"API error ({response.StatusCode}): {errorText}");
        }
            
        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
            
        return doc.RootElement.GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text").GetString() ?? "No response";
    }

    private async void BtnSendFollowup_Click(object sender, RoutedEventArgs e)
    {
        var followupQuestion = TxtFollowupQuestion.Text;
            
        if (string.IsNullOrWhiteSpace(followupQuestion))
        {
            MessageBox.Show("Please enter a follow-up question.", "Empty Question", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
            
        var apiSelection = CboAiApi.SelectedItem?.ToString() ?? "Claude API";
        TxtStatus.Text = $"Sending follow-up question to {apiSelection}...";
            
        try
        {
            // Add the follow-up question to the conversation history
            _conversationHistory.Add(new ChatMessage { Role = "user", Content = followupQuestion });
                
            // Send it to selected API
            var response = await SendToAiApi(apiSelection, followupQuestion);
                
            // Display the response
            TxtResponse.Text = response;
                
            // Update conversation history
            _conversationHistory.Add(new ChatMessage { Role = "assistant", Content = response });
                
            // Clear the follow-up question text box
            TxtFollowupQuestion.Text = "";
                
            TxtStatus.Text = "Follow-up response received!";
            BtnSaveResponse.IsEnabled = true;
        }
        catch (Exception ex)
        {
            ErrorLogger.LogError(ex, "Sending follow-up question");
            TxtStatus.Text = "Error sending follow-up question.";
        }
    }

    private void BtnSaveResponse_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(TxtResponse.Text))
            {
                MessageBox.Show("There is no response to save.", "No Response", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
        
            // Create a save file dialog
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = "txt",
                Title = "Save AI Analysis Response"
            };
        
            // If the user has selected a project folder, suggest that as the initial directory
            if (!string.IsNullOrEmpty(_selectedFolder) && Directory.Exists(_selectedFolder))
            {
                saveFileDialog.InitialDirectory = _selectedFolder;
            
                // Suggest a filename based on the project folder name and timestamp
                var folderName = new DirectoryInfo(_selectedFolder).Name;
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                saveFileDialog.FileName = $"{folderName}_analysis_{timestamp}.txt";
            }
            else
            {
                // Default filename with timestamp if no folder is selected
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                saveFileDialog.FileName = $"ai_analysis_{timestamp}.txt";
            }
        
            // Show the dialog and get the result
            var result = saveFileDialog.ShowDialog();
        
            // If the user clicked OK, save the file
            if (result == true)
            {
                // Save the response text to the selected file
                File.WriteAllText(saveFileDialog.FileName, TxtResponse.Text);
                MessageBox.Show($"Response saved to {saveFileDialog.FileName}", "Save Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.LogError(ex, "Saving response to file");
            MessageBox.Show("An error occurred while saving the response.", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}