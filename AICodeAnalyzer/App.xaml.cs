using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.IO.Pipes;
using System.Threading;
using AICodeAnalyzer.Services;

namespace AICodeAnalyzer;

public partial class App : IDisposable
{
    private FileAssociationManager? _fileAssociationManager;
    private const int ErrorBrokenPipe = unchecked((int)0x800700E7);

    // Added for single instance logic
    private const string AppUniqueId = "A1C0D3-A7A7-4A87-B1B1-F1L3A550C1A70R"; // A unique GUID-like string
    private static string PipeName => $"AICodeAnalyzerPipe-{Environment.UserName}-{AppUniqueId}"; // Unique pipe name per user
    private CancellationTokenSource? _pipeServerCts;

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            // --- Single Instance Logic ---
            bool isFirstInstance;
            string[] args = e.Args; // Capture args before passing them

            await using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.Asynchronous))
            {
                try
                {
                    // Try to connect to the existing instance's pipe server
                    await client.ConnectAsync(500); // Timeout after 500ms

                    // If connected, send the command-line arguments
                    await using (var writer = new StreamWriter(client))
                    {
                        writer.WriteLine(args.Length);
                        foreach (var arg in args)
                        {
                            await writer.WriteLineAsync(arg);
                        }

                        await writer.FlushAsync();
                    }

                    // The arguments have been sent, exit this instance
                    Shutdown();

                    return;
                }
                catch (TimeoutException)
                {
                    isFirstInstance = true;
                }
                catch (Exception ex)
                {
                    isFirstInstance = true;
                    LogError($"Pipe client connection failed: {ex.Message}");
                }
            }

            if (!isFirstInstance) return;

            var mainWindow = new MainWindow();
            Current.MainWindow = mainWindow;
            mainWindow.Show();

            _pipeServerCts = new CancellationTokenSource();
            _ = Task.Run(() => StartPipeServerAsync(_pipeServerCts.Token));

            base.OnStartup(e);

            _fileAssociationManager = new FileAssociationManager(LogInformation, LogError);

            var settingsManager = new SettingsManager();
            if (settingsManager.Settings.RegisterAsDefaultMdHandler)
            {
                await Task.Run(() => _fileAssociationManager.RegisterApplication());
            }

            // call a new public async method on MainWindow to load the file:
            if (args.Length <= 0 || !File.Exists(args[0])) return;

            var filePath = args[0];

            // Awaiting here - so the MainWindow loads the file immediately
            await mainWindow.LoadMarkdownFileAsync(filePath);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in the OnStartup method.");
            Shutdown(-1);
        }
    }

    private async Task StartPipeServerAsync(CancellationToken ct)
    {
        LogInformation($"Pipe server started on {PipeName}");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                // Wait for a client connection or cancellation
                await server.WaitForConnectionAsync(ct);

                // Client connected, read arguments
                using var reader = new StreamReader(server);
                var receivedArgs = new System.Collections.Generic.List<string>();

                try
                {
                    var argCountString = await reader.ReadLineAsync(ct);
                    if (int.TryParse(argCountString, out var argCount))
                    {
                        for (var i = 0; i < argCount; i++)
                        {
                            var arg = await reader.ReadLineAsync(ct);
                            if (arg != null)
                            {
                                receivedArgs.Add(arg);
                            }
                        }
                    }
                }
                // Catch broken pipe specifically using HResult
                catch (IOException ioEx) when (ioEx.HResult == ErrorBrokenPipe)
                {
                    // Client disconnected prematurely, just log and continue
                    LogInformation("Pipe client disconnected prematurely (broken pipe).");
                }
                catch (OperationCanceledException)
                {
                    // Cancellation requested while reading, exit loop
                    break;
                }
                catch (Exception ex)
                {
                    LogError($"Error reading from pipe: {ex.Message}");
                }

                // Process received arguments on the UI thread
                if (receivedArgs.Count > 0)
                {
                    // Find the main window and invoke the argument handling method
                    // Use InvokeAsync to avoid blocking the server thread
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (Current.MainWindow is MainWindow mainWindow)
                        {
                            mainWindow.HandleCommandLineArguments(receivedArgs.ToArray());
                        }
                        else
                        {
                            // If MainWindow isn't ready yet, store the args for when it is
                            Current.Properties["StartupFilePath"] = receivedArgs[0]; // Assuming the first arg is the file path
                            // We might need a more robust way to handle this if MainWindow takes long to load
                        }
                    });
                }

                // Close the connection (using block handles this)
            }
            catch (OperationCanceledException)
            {
                // Cancellation requested while waiting for connection, exit loop
                break;
            }
            catch (Exception ex)
            {
                // Log other server errors but keep listening
                LogError($"Pipe server error: {ex.Message}");
                await Task.Delay(1000, ct); // Wait a bit before trying to recreate the server
            }
        }

        LogInformation("Pipe server stopped.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Cancel the pipe server task
        _pipeServerCts?.Cancel();

        // Wait for the server task to finish (optional, but good practice)
        // Be cautious with waiting in OnExit, can cause deadlocks in some scenarios.
        // For simplicity, we'll just let it run until it naturally stops or the process is killed.
        // _pipeServerTask?.Wait(TimeSpan.FromSeconds(1)); // Wait a short time

        base.OnExit(e);
    }

    public void UnregisterFileAssociation()
    {
        if (_fileAssociationManager == null)
        {
            _fileAssociationManager = new FileAssociationManager(LogInformation, LogError);
        }

        _fileAssociationManager.UnregisterApplication();
    }

    public void RegisterFileAssociation()
    {
        if (_fileAssociationManager == null)
        {
            _fileAssociationManager = new FileAssociationManager(LogInformation, LogError);
        }

        _fileAssociationManager.RegisterApplication();
    }

    private static void LogError(string message)
    {
        Console.WriteLine($"ERROR: {message}");

        var ex = new Exception($"ERROR: {message}");
        Logger.LogError(ex, $"ERROR: {message}");
    }

    private static void LogInformation(string message)
    {
        Console.WriteLine($"INFORMATION: {message}");
    }

    public async Task UpdateFileAssociationAsync(bool register)
    {
        if (_fileAssociationManager == null)
        {
            _fileAssociationManager = new FileAssociationManager(LogInformation, LogError);
        }

        if (register)
        {
            await Task.Run(() => _fileAssociationManager.RegisterApplication());
        }
        else
        {
            await Task.Run(() => _fileAssociationManager.UnregisterApplication());
        }
    }

    public void Dispose()
    {
        // Cancel the pipe server task if it's running
        _pipeServerCts?.Cancel();
        _pipeServerCts?.Dispose();
        _pipeServerCts = null;

        _fileAssociationManager = null;

        // Suppress finalization since we've explicitly cleaned up resources
        GC.SuppressFinalize(this);
    }
}