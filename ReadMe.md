Okay, here is a draft for a `README.md` file for your AI Code Analyzer application, based on the code and analysis provided.

```markdown
# AI Code Analyzer

**AI Code Analyzer** is a Windows desktop application built with WPF that allows developers to analyze their source code using various Large Language Models (LLMs) through their respective APIs. Select project folders or individual files, choose your preferred AI provider and model, configure analysis parameters, and get insights into code quality, potential bugs, security vulnerabilities, and suggestions for improvement.

---

## ✨ Features

*   **Flexible File Selection:**
    *   Scan entire project folders recursively.
    *   Add individual source code files.
    *   Clear the current file selection.
*   **Configurable File Filtering:**
    *   Define allowed source file extensions (e.g., `.cs`, `.py`, `.js`).
    *   Set a maximum file size limit to control costs and context window usage.
*   **Multi-Provider AI Support:**
    *   Integrates with multiple AI API providers:
        *   Anthropic Claude
        *   OpenAI ChatGPT
        *   DeepSeek
        *   Grok
        *   Google Gemini
    *   Select specific models offered by providers (where applicable).
*   **API Key Management:**
    *   Enter API keys for selected providers.
    *   Save keys for future use (associated with the provider).
    *   Select from previously saved (masked) keys.
    *   **(⚠️ Security Warning: See 'API Keys' section below)**
*   **Customizable Prompts:**
    *   Use a default prompt for general code analysis.
    *   Create, rename, edit, and delete custom prompt templates for specific analysis tasks.
*   **Code Analysis:**
    *   Sends selected file contents and the chosen prompt to the selected AI model.
    *   Handles conversation history for context-aware interactions.
*   **Response Viewing:**
    *   Displays AI responses formatted as Markdown using `Markdig.Wpf`.
    *   Option to view the raw text response.
    *   Zoom controls (Zoom In, Zoom Out, Reset) for the Markdown view (Ctrl+Scroll also works).
*   **Conversation Interaction:**
    *   Ask follow-up questions based on the initial analysis.
    *   Optionally include selected files from the list alongside follow-up questions for specific context.
    *   Navigate through previous and next responses within a conversation session.
*   **Persistence:**
    *   Saves application settings (file extensions, size limits, prompt templates) to `settings.xml`.
    *   Saves API keys to `keys.xml`.
*   **Logging & Output:**
    *   Detailed operation log panel within the application.
    *   Automatic saving of AI responses to an `AiOutput` folder in the application directory (e.g., `ProjectName_response_1_timestamp.md`).
    *   Error logging to `ErrorLog.txt`.

---

## 🤖 Supported AI Providers

*   Anthropic (Claude models)
*   OpenAI (ChatGPT models - currently defaults to `gpt-4-turbo-preview`)
*   DeepSeek (DeepSeek models)
*   Grok (Grok models)
*   Google (Gemini models)

---

## 📸 Screenshots

*(Placeholder: Add screenshots of the main window, configuration window, etc., here for better visualization)*

*   `[Screenshot of Main Window]`
*   `[Screenshot of Configuration Window - Prompt Settings]`
*   `[Screenshot of Configuration Window - File Settings]`

---

## 🚀 Getting Started

### Prerequisites

*   Windows Operating System
*   .NET Desktop Runtime (Specify version if known, e.g., .NET 6.0 or later)
*   API Keys for the AI providers you intend to use.

### Running the Application

1.  Download the latest release or build the application from the source code.
2.  Run the `AICodeAnalyzer.exe` executable.

### Basic Usage Workflow

1.  **Select Code:**
    *   Click **"Select Folder"** to choose a project directory. The application will scan it based on configured settings.
    *   Alternatively, click **"Add Files"** to select one or more individual source files.
    *   Use **"Clear"** to remove all selected files.
    *   The "Files List" panel shows a summary and the discovered/added files, organized by folder.
2.  **Select AI Provider & Model:**
    *   Choose an AI provider from the "AI API Selection" dropdown (e.g., "Claude API").
    *   If the provider supports multiple models (Claude, DeepSeek, Grok, Gemini), select the desired model from the "Model" dropdown. The tooltip provides a description.
3.  **Enter API Key:**
    *   Enter your API key for the selected provider in the "API Key" password box.
    *   Optionally, click **"Save Current Key"** to store it for future use with this provider.
    *   Previously saved keys (masked) can be selected from the dropdown next to the password box.
4.  **Select Prompt:**
    *   Choose an analysis prompt template from the "Initial Prompt" dropdown.
    *   Click **"Edit"** to manage your prompt templates (see Configuration).
5.  **Analyze:**
    *   Click **"Send Initial Prompt"**. The application will prepare the code and prompt, send it to the selected AI API, and display the response.
    *   The status bar and log panel provide feedback on the process.
6.  **View Response:**
    *   The AI's response appears in the "AI Analysis" panel, rendered as Markdown by default.
    *   Use the **Navigation Buttons** (`◀`, `▶`) to view different responses if you have a conversation history.
    *   Use the **Zoom Buttons** (`+`, `-`, `Reset`) or `Ctrl + Mouse Wheel` to adjust the Markdown view size.
    *   Click **"Show Raw Text"** to toggle between Markdown and plain text views.
    *   Click **"Save Response"** to manually save the current response to a file. (Responses are also auto-saved to the `AiOutput` folder).
7.  **Follow-up:**
    *   Enter a follow-up question in the "Follow-up Question" text box.
    *   Optionally, select specific files in the "Files List" panel and check the **"Include selected files with question"** checkbox to provide that file content as context for your question.
    *   Click **"Send"** to submit the follow-up. The new response will be displayed.

---

## ⚙️ Configuration

Access the configuration window via the **Settings -> Configure...** menu item.

*   **Prompt Settings Tab:**
    *   Select existing prompt templates from the dropdown.
    *   Edit the content of the selected template in the text box below.
    *   **New:** Create a new, named prompt template.
    *   **Rename:** Rename the currently selected (non-Default) template.
    *   **Delete:** Delete the currently selected (non-Default) template (only if more than one exists).
    *   **Restore Default Prompt:** Resets the content of the *currently selected* prompt to the application's built-in default text.
*   **File Settings Tab:**
    *   **Maximum File Size:** Adjust the slider to set the maximum size (in KB) for files to be included during scanning.
    *   **Source File Extensions:** Add or remove file extensions (e.g., `.ts`, `.rb`) that the application should recognize and include during scans. Use the "X" button to remove, and the text box/button below to add new ones (ensure they start with a dot `.`).

Click **"Save"** to apply changes or **"Cancel"** to discard. **"Reset to Defaults"** restores all settings (prompts, file size, extensions) to their original state.

---

## 🔑 API Keys

API keys are required to interact with the different AI provider services.

*   Keys are entered in the main window's "API Key" section.
*   Saved keys are stored **locally** on your machine in the `keys.xml` file within the application's directory.

**⚠️ IMPORTANT SECURITY WARNING:**

*   Currently, API keys are stored in **plain text** within the `keys.xml` file. This is a **significant security risk**. Anyone with access to this file can view your API keys.
*   **Recommendation:** It is strongly advised to modify the `ApiKeyManager.cs` to use secure storage like the Windows Data Protection API (DPAPI) via `ProtectedData.Protect`/`Unprotect` before using this application with valuable API keys in an untrusted environment. See the analysis document (`CSharp_AICodeAnalyzer_analysis_*.md`) for details.
*   Use this feature with caution and ensure the `keys.xml` file is adequately protected if you choose to save keys.

---

## 💻 Technology Stack

*   **Framework:** .NET Desktop (WPF - Windows Presentation Foundation)
*   **Language:** C#
*   **Markdown Rendering:** Markdig.Wpf
*   **Folder Browser Dialog:** WindowsAPICodePack-Shell
*   **Configuration/Key Storage:** XML Serialization (System.Xml.Serialization)

---

## 🐞 Known Issues & Limitations

*   **Potential UI Unresponsiveness:** Reading large numbers of files or very large files might still cause the UI to feel sluggish during the "Select Folder" scan, as file reading (`File.ReadAllText`) is synchronous within the background task. (See suggestion: Use `File.ReadAllTextAsync`).
*   **API Key Storage:** As mentioned above, API keys are stored insecurely in plain text in `keys.xml`. **This needs to be addressed.**
*   **Fragile File Matching:** Selecting files for follow-up questions relies on matching the displayed filename in the ListView, which could break if the display format changes.
*   **Inconsistent Model Handling:** The internal interface `IAiApiProvider` doesn't uniformly handle model selection, requiring specific checks in `MainWindow.xaml.cs`. OpenAI provider currently doesn't support model selection via the UI.
*   **Error Handling:** Some error handling is basic (`catch (Exception)`), and API-specific errors could be handled more gracefully. Save failures for settings/keys might happen silently.
*   **Duplicate Prompt Logic:** There might be redundant logic for handling duplicate prompt templates between `SettingsManager` and `ConfigurationWindow`.

---

## 📈 Future Improvements (Roadmap)

Based on the initial analysis, potential areas for improvement include:

*   **Security:** Implement secure API key storage using DPAPI.
*   **Architecture:** Refactor `MainWindow.xaml.cs` using the MVVM pattern for better maintainability and testability.
*   **Dependency Injection:** Introduce a DI container.
*   **Performance:** Use asynchronous file I/O (`File.ReadAllTextAsync`).
*   **Networking:** Use `HttpClientFactory` or a single `HttpClient` instance.
*   **Robustness:** Improve error handling and user feedback (especially for save failures). Make file selection for follow-ups less brittle (e.g., using `ListViewItem.Tag`).
*   **Configuration:** Consider using `appsettings.json` instead of XML.
*   **Consistency:** Refine the `IAiApiProvider` interface for consistent model handling. Add model selection support for OpenAI.
*   **Code Quality:** Address magic strings, consolidate duplicate logic.

---

## License

*(Specify License Here - e.g., MIT License, Apache 2.0, or Proprietary)*

```

**Key things added/emphasized:**

1.  **Clear Title and Overview:** Sets the stage immediately.
2.  **Features List (✨):** Breaks down capabilities into digestible points.
3.  **Supported Providers (🤖):** Explicitly lists them.
4.  **Screenshots Placeholder (📸):** Essential for a UI application.
5.  **Getting Started (🚀):** Clear prerequisites and step-by-step usage.
6.  **Configuration (⚙️):** Explains the settings options.
7.  **API Keys (🔑) with Security Warning (⚠️):** Highlights the critical security issue and references the analysis/solution.
8.  **Technology Stack (💻):** Lists the core components.
9.  **Known Issues (🐞):** Transparently lists current problems based on the analysis.
10. **Future Improvements (📈):** Outlines potential enhancements, giving direction.
11. **License Placeholder:** Standard practice.

Remember to replace the placeholder sections (like screenshots and license) with actual content.