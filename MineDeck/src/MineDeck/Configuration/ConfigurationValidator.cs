using System.Net;

namespace MineDeck.Configuration;

public static class ConfigurationValidator
{
    public static IReadOnlyList<string> Validate(MineDeckOptions options)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.InstanceName)) errors.Add("MineDeck:InstanceName is required.");
        if (string.IsNullOrWhiteSpace(options.DataDirectory)) errors.Add("MineDeck:DataDirectory is required.");
        ValidateLoopbackUrl(options.Dashboard.ListenUrl, "Dashboard:ListenUrl", errors);
        ValidateLoopbackUrl(options.Dashboard.PublicBaseUrl, "Dashboard:PublicBaseUrl", errors);
        if (Uri.TryCreate(options.Dashboard.ListenUrl, UriKind.Absolute, out var listen) &&
            Uri.TryCreate(options.Dashboard.PublicBaseUrl, UriKind.Absolute, out var publicBase) &&
            !string.Equals(listen.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped),
                publicBase.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped), StringComparison.OrdinalIgnoreCase))
            errors.Add("Dashboard:PublicBaseUrl must use the same origin as Dashboard:ListenUrl.");
        ValidateLoopbackUrl(options.Bridge.BaseUrl, "Bridge:BaseUrl", errors);
        ValidateLoopbackHost(options.Minecraft.Host, "Minecraft:Host", errors);
        ValidateLoopbackHost(options.MineJammer.Host, "MineJammer:Host", errors);
        ValidatePort(options.Minecraft.Port, "Minecraft:Port", errors);
        ValidatePort(options.MineJammer.Port, "MineJammer:Port", errors);
        if (options.Minecraft.Port == options.MineJammer.Port) errors.Add("Production and MineJammer ports must differ.");
        if (options.Admin.CookieHours is < 1 or > 24) errors.Add("Admin:CookieHours must be between 1 and 24.");
        if (!Security.PasswordHasher.IsValidEncoding(options.Admin.PasswordHash))
            errors.Add("Admin:PasswordHash is missing or invalid. Generate it with MineDeck.PasswordTool.");
        if (!string.Equals(options.Bridge.Mode, "Http", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(options.Bridge.Mode, "Mock", StringComparison.OrdinalIgnoreCase))
            errors.Add("Bridge:Mode must be Http or Mock.");
        if (options.Bridge.TimeoutSeconds is < 1 or > 60) errors.Add("Bridge:TimeoutSeconds must be between 1 and 60.");
        if (string.IsNullOrWhiteSpace(options.Bridge.TokenFile) && string.IsNullOrWhiteSpace(options.Bridge.TokenEnvironmentVariable))
            errors.Add("Bridge requires either TokenFile or TokenEnvironmentVariable.");
        if (!string.Equals(options.Launcher.LaunchTarget, "minecraft-launcher:", StringComparison.OrdinalIgnoreCase))
            errors.Add("Launcher:LaunchTarget must be the official minecraft-launcher: protocol target.");
        return errors;
    }

    private static void ValidateLoopbackHost(string value, string name, ICollection<string> errors)
    {
        if (string.Equals(value, "localhost", StringComparison.OrdinalIgnoreCase)) return;
        if (!IPAddress.TryParse(value, out var address) || !IPAddress.IsLoopback(address))
            errors.Add($"{name} must be a loopback host.");
    }

    private static void ValidateLoopbackUrl(string value, string name, ICollection<string> errors)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            errors.Add($"{name} must be an absolute HTTP(S) URL.");
            return;
        }

        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return;
        if (!IPAddress.TryParse(uri.Host, out var address) || !IPAddress.IsLoopback(address))
            errors.Add($"{name} must bind to loopback. Use Tailscale Serve for remote access.");
    }

    private static void ValidatePort(int value, string name, ICollection<string> errors)
    {
        if (value is < 1 or > 65535) errors.Add($"{name} must be between 1 and 65535.");
    }
}
