using System;
using System.Diagnostics;
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
            LogAndShowFatalError("AppDomain Unhandled Exception", args.ExceptionObject as Exception, isTerminating: true);
        };

        DispatcherUnhandledException += (sender, args) =>
        {
            LogAndShowFatalError("Dispatcher Unhandled Exception", args.Exception, isTerminating: true);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            LogAndShowFatalError("Unobserved Task Exception", args.Exception, isTerminating: false);
            args.SetObserved();
        };
    }

    private static void LogAndShowFatalError(string source, Exception? ex, bool isTerminating)
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

        try
        {
            MessageBox.Show(
                $"程序启动或运行发生异常:\r\n\r\n{ex?.Message}\r\n\r\n详细错误已记录至同目录下的 startup_crash.log 文件。\r\n点击确定后程序将完全退出。",
                "程序异常终止",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
        }

        if (isTerminating)
        {
            try
            {
                Current?.Shutdown(-1);
            }
            catch
            {
            }

            // 彻底杀死当前进程及其线程，杜绝在后台僵尸驻留占用文件句柄
            Environment.Exit(-1);
        }
    }
}
