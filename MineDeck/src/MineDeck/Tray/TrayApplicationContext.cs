using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using MineDeck.Models;
using MineDeck.Services;

namespace MineDeck.Tray;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notify;
    private readonly Func<CancellationToken, Task<RuntimeStatus>> _status;
    private readonly Func<string, CancellationToken, Task<SupervisorReply>> _command;
    private readonly LauncherService _launcher;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly CancellationTokenSource _lifetime = new();
    private Icon? _icon;

    public TrayApplicationContext(
        Func<CancellationToken, Task<RuntimeStatus>> status,
        Func<string, CancellationToken, Task<SupervisorReply>> command,
        LauncherService launcher)
    {
        _status = status;
        _command = command;
        _launcher = launcher;
        _notify = new NotifyIcon
        {
            Visible = true,
            Text = "MineDeck · checking status",
            ContextMenuStrip = BuildMenu()
        };
        _notify.DoubleClick += (_, _) => _launcher.OpenDashboard();
        _timer = new System.Windows.Forms.Timer { Interval = 3000, Enabled = true };
        _timer.Tick += async (_, _) => await RefreshAsync().ConfigureAwait(true);
        _ = RefreshAsync();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open MineDeck", null, (_, _) => _launcher.OpenDashboard());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Start MineScape", null, async (_, _) => await RunAsync("start").ConfigureAwait(true));
        menu.Items.Add("Play MineScape", null, async (_, _) => await PlayAsync().ConfigureAwait(true));
        menu.Items.Add("Stop MineScape cleanly", null, async (_, _) => await RunAsync("stop").ConfigureAwait(true));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Start MineJammer", null, async (_, _) => await RunAsync("start-minejammer").ConfigureAwait(true));
        menu.Items.Add("Stop MineJammer cleanly", null, async (_, _) => await RunAsync("stop-minejammer").ConfigureAwait(true));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit tray (server stays running)", null, (_, _) => ExitThread());
        return menu;
    }

    private async Task RunAsync(string command)
    {
        try
        {
            var result = await _command(command, _lifetime.Token).ConfigureAwait(true);
            _notify.ShowBalloonTip(3500, result.Accepted ? "MineDeck" : "MineDeck could not complete that", result.Message,
                result.Accepted ? ToolTipIcon.Info : ToolTipIcon.Error);
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or TimeoutException or OperationCanceledException)
        {
            _notify.ShowBalloonTip(3500, "MineDeck supervisor unavailable", exception.Message, ToolTipIcon.Error);
        }
    }

    private async Task PlayAsync()
    {
        await RunAsync("start").ConfigureAwait(true);
        var deadline = DateTimeOffset.UtcNow.AddMinutes(3);
        while (DateTimeOffset.UtcNow < deadline && !_lifetime.IsCancellationRequested)
        {
            var current = await _status(_lifetime.Token).ConfigureAwait(true);
            if (current.Production.Online)
            {
                var result = _launcher.OpenOfficialLauncher();
                _notify.ShowBalloonTip(5000, "Play MineScape", result.Message, result.Accepted ? ToolTipIcon.Info : ToolTipIcon.Error);
                return;
            }
            await Task.Delay(2000, _lifetime.Token).ConfigureAwait(true);
        }
        _notify.ShowBalloonTip(5000, "MineScape did not come online", "The Launcher was not opened. Check MineDeck for the fault.", ToolTipIcon.Error);
    }

    private async Task RefreshAsync()
    {
        var current = await _status(_lifetime.Token).ConfigureAwait(true);
        var next = CreateIcon(current.State, current.MineJammer.Online);
        var previous = _icon;
        _notify.Icon = _icon = next;
        previous?.Dispose();
        var label = current.State switch
        {
            LifecycleState.Online => $"Online · {current.Production.OnlinePlayers}/{current.Production.MaxPlayers} · {current.Production.LatencyMilliseconds}ms",
            LifecycleState.Starting => "Starting",
            LifecycleState.Stopping => "Stopping",
            LifecycleState.Maintenance => "Maintenance",
            LifecycleState.Fault => "Fault",
            _ => "Offline"
        };
        if (current.MineJammer.Online) label += " · J";
        _notify.Text = ("MineDeck · " + label)[..Math.Min(63, ("MineDeck · " + label).Length)];
    }

    internal static Icon CreateIcon(LifecycleState state, bool mineJammer)
    {
        var color = state switch
        {
            LifecycleState.Online => Color.FromArgb(51, 199, 129),
            LifecycleState.Starting or LifecycleState.Stopping or LifecycleState.Maintenance => Color.FromArgb(240, 174, 74),
            LifecycleState.Fault => Color.FromArgb(234, 82, 92),
            _ => Color.FromArgb(126, 136, 148)
        };
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        using var brush = new SolidBrush(color);
        graphics.FillEllipse(brush, 2, 2, 27, 27);
        using var pen = new Pen(Color.FromArgb(235, 245, 239), 2.5f);
        graphics.DrawLine(pen, 9, 21, 9, 10);
        graphics.DrawLine(pen, 9, 10, 16, 18);
        graphics.DrawLine(pen, 16, 18, 23, 10);
        graphics.DrawLine(pen, 23, 10, 23, 21);
        if (mineJammer)
        {
            using var blue = new SolidBrush(Color.FromArgb(72, 145, 255));
            graphics.FillEllipse(blue, 18, 18, 14, 14);
            using var font = new Font("Segoe UI", 8, FontStyle.Bold, GraphicsUnit.Pixel);
            graphics.DrawString("J", font, Brushes.White, 22, 20);
        }
        var handle = bitmap.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
    }

    protected override void ExitThreadCore()
    {
        _lifetime.Cancel();
        _timer.Stop();
        _timer.Dispose();
        _notify.Visible = false;
        _notify.Dispose();
        _icon?.Dispose();
        base.ExitThreadCore();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}
