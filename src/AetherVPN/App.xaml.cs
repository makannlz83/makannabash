using System;
using System.Windows;

namespace AetherVPN;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Ensure only one instance runs
        var mutex = new System.Threading.Mutex(true, "AetherVPN_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("AetherVPN is already running.", "AetherVPN", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }
        GC.KeepAlive(mutex);
    }
}
