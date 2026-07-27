namespace MineDeck.Configuration;

public sealed class MineDeckOptions
{
    public string InstanceName { get; set; } = "family-production";
    public string DataDirectory { get; set; } = "%LOCALAPPDATA%\\MineScape\\MineDeck";
    public DashboardOptions Dashboard { get; set; } = new();
    public AdminOptions Admin { get; set; } = new();
    public MinecraftOptions Minecraft { get; set; } = new();
    public MinecraftOptions MineJammer { get; set; } = new() { Port = 25566 };
    public BridgeOptions Bridge { get; set; } = new();
    public LauncherOptions Launcher { get; set; } = new();
    public BackupOptions Backups { get; set; } = new();

    public void ExpandPaths()
    {
        DataDirectory = Environment.ExpandEnvironmentVariables(DataDirectory);
        Minecraft.ExpandPaths();
        MineJammer.ExpandPaths();
        Bridge.TokenFile = Environment.ExpandEnvironmentVariables(Bridge.TokenFile);
        Backups.DestinationDirectory = Environment.ExpandEnvironmentVariables(Backups.DestinationDirectory);
        Backups.SourceDirectories = Backups.SourceDirectories.Select(Environment.ExpandEnvironmentVariables).ToArray();
    }
}

public sealed class DashboardOptions
{
    public string ListenUrl { get; set; } = "http://127.0.0.1:43117";
    public string PublicBaseUrl { get; set; } = "http://127.0.0.1:43117";
}

public sealed class AdminOptions
{
    public string PasswordHash { get; set; } = string.Empty;
    public int CookieHours { get; set; } = 8;
}

public sealed class MinecraftOptions
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 25565;
    public int ProtocolVersion { get; set; } = -1;
    public string ExecutablePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public int StartupTimeoutSeconds { get; set; } = 180;

    public void ExpandPaths()
    {
        ExecutablePath = Environment.ExpandEnvironmentVariables(ExecutablePath);
        WorkingDirectory = Environment.ExpandEnvironmentVariables(WorkingDirectory);
    }
}

public sealed class BridgeOptions
{
    public string Mode { get; set; } = "Http";
    public string BaseUrl { get; set; } = "http://127.0.0.1:8765";
    public string TokenFile { get; set; } = string.Empty;
    public string TokenEnvironmentVariable { get; set; } = "MINESCAPE_BRIDGE_TOKEN";
    public int TimeoutSeconds { get; set; } = 12;
}

public sealed class LauncherOptions
{
    public string LaunchTarget { get; set; } = "minecraft-launcher:";
    public string QuickPlayLabel { get; set; } = "MineScape";
}

public sealed class BackupOptions
{
    public string DestinationDirectory { get; set; } = string.Empty;
    public string[] SourceDirectories { get; set; } = Array.Empty<string>();
}
