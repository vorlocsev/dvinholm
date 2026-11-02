using System;
using System.Linq;
using System.Windows;

namespace Dvinholm.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--shell-smoke", StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var shell = new MainWindow(skipAdapterScan: true, skipPreferenceWrite: true);
                shell.AssertEightOfflineContracts();
                shell.Close();
                Shutdown(0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("WPF shell smoke FAILED: " + ex);
                Shutdown(1);
            }
            return;
        }
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
}
