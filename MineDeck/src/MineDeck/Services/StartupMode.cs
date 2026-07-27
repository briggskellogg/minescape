namespace MineDeck.Services;

public enum StartupKind
{
    Dashboard,
    Play,
    Tray,
    Background
}

public sealed record StartupMode(StartupKind Kind)
{
    public bool Background => Kind == StartupKind.Background;
    public bool Play => Kind == StartupKind.Play;
    public bool TrayOnly => Kind == StartupKind.Tray;
    public bool StartProduction => Kind is StartupKind.Dashboard or StartupKind.Play;
    public bool OpenDashboard => Kind is StartupKind.Dashboard or StartupKind.Play;
    public bool ShouldOpenOfficialLauncher(bool startAccepted, bool productionOnline) =>
        Play && startAccepted && productionOnline;

    public static StartupMode Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0) return new(StartupKind.Dashboard);
        if (args.Count != 1)
            throw new ArgumentException("Choose exactly one MineDeck mode: --dashboard, --play, --tray, or --background.");

        return args[0].ToLowerInvariant() switch
        {
            "--dashboard" => new(StartupKind.Dashboard),
            "--play" => new(StartupKind.Play),
            "--tray" => new(StartupKind.Tray),
            "--background" => new(StartupKind.Background),
            _ => throw new ArgumentException(
                $"Unknown MineDeck argument '{args[0]}'. Expected --dashboard, --play, --tray, or --background.")
        };
    }
}
