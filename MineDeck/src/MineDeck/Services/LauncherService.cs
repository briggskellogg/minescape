using System.Diagnostics;
using MineDeck.Configuration;
using MineDeck.Models;

namespace MineDeck.Services;

public sealed class LauncherService
{
    private readonly MineDeckOptions _options;
    public LauncherService(MineDeckOptions options) => _options = options;

    public OperationResponse OpenDashboard()
    {
        try
        {
            Process.Start(new ProcessStartInfo(_options.Dashboard.PublicBaseUrl) { UseShellExecute = true });
            return new(true, "Dashboard opened.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new(false, $"Could not open the dashboard: {exception.Message}");
        }
    }

    public OperationResponse OpenOfficialLauncher()
    {
        try
        {
            Process.Start(new ProcessStartInfo(_options.Launcher.LaunchTarget) { UseShellExecute = true });
            return new(true, $"Official Launcher opened. Choose the pinned ‘{_options.Launcher.QuickPlayLabel}’ Quick Play tile.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new(false, $"MineScape is still online, but Windows could not open the configured Launcher target: {exception.Message}");
        }
    }
}
