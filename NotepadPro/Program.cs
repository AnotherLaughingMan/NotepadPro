using Avalonia;
using Avalonia.ReactiveUI;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NotepadPro;

sealed class Program
{
    private static readonly object LogLock = new();

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex, "Fatal startup error");
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseReactiveUI()
            .WithInterFont()
            .LogToTrace();

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            WriteCrashLog(ex, "Unhandled exception");
            return;
        }

        WriteCrashLog(null, $"Unhandled exception: {e.ExceptionObject}");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }

    private static void WriteCrashLog(Exception? exception, string context)
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var logDir = Path.Combine(basePath, "NotepadPro", "logs");
        var logPath = Path.Combine(logDir, "crash.log");

        lock (LogLock)
        {
            Directory.CreateDirectory(logDir);
            using var writer = new StreamWriter(logPath, append: true);
            writer.WriteLine("==== Crash Report ====");
            writer.WriteLine($"Time: {DateTimeOffset.Now:O}");
            writer.WriteLine($"Context: {context}");
            if (exception != null)
            {
                writer.WriteLine(exception);
            }
            writer.WriteLine();
        }
    }
}
