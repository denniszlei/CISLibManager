using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace CadenceCisLibraryManager;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            LogAndShowFatalError("AppDomain Unhandled Exception", args.ExceptionObject as Exception);
        };

        DispatcherUnhandledException += (sender, args) =>
        {
            LogAndShowFatalError("Dispatcher Unhandled Exception", args.Exception);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            LogAndShowFatalError("Unobserved Task Exception", args.Exception);
            args.SetObserved();
        };
    }

    private static void LogAndShowFatalError(string source, Exception? ex)
    {
        var message = ex?.ToString() ?? "Unknown error";
        var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_crash.log");
        try
        {
            File.WriteAllText(logPath, $"[{DateTime.Now}] Source: {source}\r\nException:\r\n{message}");
        }
        catch
        {
        }

        MessageBox.Show($"程序启动或运行发生异常:\r\n\r\n{ex?.Message}\r\n\r\n详细堆栈已写入: {logPath}\r\n\r\n{message}", "致命错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
