using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MineDeck.Configuration;

namespace MineDeck.Services;

public static partial class RuntimeLaunchValidator
{
    private const string ProductionPrefix = "var/MineScape/";
    private const string CanonicalSeed = "6246468738900744";
    private const string WorldBorderConfigurationPath = "config/worldborder.json5";
    private static readonly string[] RequiredModPrefixes =
    {
        "mods/bluemap-", "mods/cloth-config-", "mods/collective-", "mods/efallingtrees-",
        "mods/fabric-api-", "mods/ferritecore-", "mods/ledger-", "mods/lithium-",
        "mods/minescape-", "mods/worldborder-"
    };
    private static readonly DatapackContract[] DatapackContracts =
    {
        new(0, 0, "artifact", "matcha", "00-Matcha"),
        new(1, 10, "artifact", "terralith", "10-Terralith"),
        new(2, 20, "artifact", "amplified_nether", "20-Amplified_Nether"),
        new(3, 30, "artifact", "nullscape", "30-Nullscape"),
        new(4, 40, "generated", "MineJammer/build/MineScape-Villages.zip", "40-MineScape-Villages.zip"),
        new(5, 50, "generated", "MineJammer/build/MineScape-Fishing.zip", "50-MineScape-Fishing.zip"),
        new(6, 60, "generated", "MineJammer/build/MineScape-Compatibility.zip", "60-MineScape-Compatibility.zip")
    };

    public static string? Validate(MinecraftOptions server, bool production)
    {
        if (string.IsNullOrWhiteSpace(server.ExecutablePath) || !File.Exists(server.ExecutablePath))
            return $"Configured executable does not exist: {server.ExecutablePath}";
        if (string.IsNullOrWhiteSpace(server.WorkingDirectory) || !Directory.Exists(server.WorkingDirectory))
            return $"Configured working directory does not exist: {server.WorkingDirectory}";

        if (production)
        {
            var releaseError = ValidateProductionAssembly(server);
            if (releaseError is not null) return releaseError;
        }

        if (!File.Exists(Path.Combine(server.WorkingDirectory, "fabric-server-launch.jar")))
            return "The hash-verified Fabric server launcher is not installed for this instance.";
        var eulaPath = Path.Combine(server.WorkingDirectory, "eula.txt");
        if (!File.Exists(eulaPath) || !File.ReadLines(eulaPath).Any(line => line.Trim().Equals("eula=true", StringComparison.OrdinalIgnoreCase)))
            return "The Minecraft EULA has not been accepted by the owner for this instance.";
        return null;
    }

    private static string? ValidateProductionAssembly(MinecraftOptions server)
    {
        var workingRoot = Path.GetFullPath(server.WorkingDirectory);
        var statusPath = Path.Combine(workingRoot, "assembly-status.json");
        if (!File.Exists(statusPath))
            return "MineScape has no runtime assembly record; production start was refused.";
        try
        {
            using var status = JsonDocument.Parse(File.ReadAllText(statusPath));
            var root = status.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !StringEquals(root, "schema", "minescape.runtime-assembly.v1") ||
                !StringEquals(root, "instance", "MineScape"))
                return "MineScape runtime assembly record has the wrong schema or instance.";

            if (!root.TryGetProperty("launch_permitted", out var allowed) || allowed.ValueKind != JsonValueKind.True)
                return "MineScape is intentionally inert: V1 release gates are closed (launch_permitted=false).";
            if (!StringEquals(root, "mode", "release-assembly") ||
                !StringEquals(root, "launcher", "verified-or-installed") ||
                !StringEquals(root, "seed", CanonicalSeed) ||
                !BooleanIs(root, "online_mode", true) ||
                !BooleanIs(root, "whitelist", true) ||
                !BooleanIs(root, "eula_accepted_by_automation", false))
                return "MineScape runtime assembly record is not a production-safe release assembly.";
            if (!root.TryGetProperty("generated_datapacks_missing", out var missing) ||
                missing.ValueKind != JsonValueKind.Array || missing.GetArrayLength() != 0)
                return "MineScape runtime assembly still reports missing generated datapacks.";

            var bundlePath = Path.Combine(workingRoot, "server-bundle.lock.json");
            if (!File.Exists(bundlePath))
                return "MineScape deterministic server-bundle.lock.json is missing.";
            var actualBundleHash = HashFile(bundlePath);
            if (!root.TryGetProperty("server_bundle_sha512", out var recordedBundleHashElement) ||
                recordedBundleHashElement.ValueKind != JsonValueKind.String ||
                !HashEquals(actualBundleHash, recordedBundleHashElement.GetString()))
                return "MineScape assembly record is not bound to the current deterministic server bundle.";

            using var bundleDocument = JsonDocument.Parse(File.ReadAllText(bundlePath));
            var bundle = bundleDocument.RootElement;
            var bundleError = ValidateBundle(server, root, bundle, workingRoot);
            return bundleError;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return $"MineScape runtime assembly record could not be verified: {exception.Message}";
        }
    }

    private static string? ValidateBundle(MinecraftOptions server, JsonElement status, JsonElement bundle, string workingRoot)
    {
        if (bundle.ValueKind != JsonValueKind.Object ||
            !StringEquals(bundle, "schema", "minescape.server-bundle-lock.v1") ||
            !StringEquals(bundle, "instance", "MineScape") ||
            !StringEquals(bundle, "seed", CanonicalSeed))
            return "MineScape server bundle has the wrong schema, instance, or canonical seed.";
        foreach (var field in new[] { "minecraft", "fabric_loader", "fabric_launcher", "seed" })
        {
            if (!SameString(status, bundle, field))
                return $"MineScape assembly record and server bundle disagree on {field}.";
        }

        if (!bundle.TryGetProperty("fabric_launcher_sha512", out var launcherHashElement) ||
            launcherHashElement.ValueKind != JsonValueKind.String || !Sha512Pattern().IsMatch(launcherHashElement.GetString() ?? string.Empty))
            return "MineScape server bundle has no valid Fabric launcher hash.";
        var launcherPath = Path.Combine(workingRoot, "fabric-server-launch.jar");
        if (!File.Exists(launcherPath) || !HashEquals(HashFile(launcherPath), launcherHashElement.GetString()))
            return "MineScape Fabric launcher differs from the deterministic server bundle.";

        var configError = ValidateConfigurationFiles(server, bundle, workingRoot);
        if (configError is not null) return configError;

        if (!bundle.TryGetProperty("files", out var bundleFilesElement) || bundleFilesElement.ValueKind != JsonValueKind.Array)
            return "MineScape server bundle has no managed-file inventory.";
        var inventoryError = ReadAndVerifyBundleInventory(bundleFilesElement, workingRoot, out var bundleFiles);
        if (inventoryError is not null) return inventoryError;
        if (bundleFiles.Count != RequiredModPrefixes.Length + DatapackContracts.Length + 1)
            return "MineScape server bundle does not contain the exact canonical mod, datapack, and configuration inventory.";

        var modPaths = bundleFiles.Keys.Where(path => path.StartsWith("mods/", StringComparison.Ordinal)).ToArray();
        if (modPaths.Length != RequiredModPrefixes.Length ||
            RequiredModPrefixes.Any(prefix => modPaths.Count(path => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) != 1))
            return "MineScape server bundle does not contain exactly one of every canonical foundational server mod.";

        var configurationPaths = bundleFiles.Keys.Where(path => path.StartsWith("config/", StringComparison.Ordinal)).ToArray();
        if (configurationPaths.Length != 1 || !configurationPaths[0].Equals(WorldBorderConfigurationPath, StringComparison.Ordinal))
            return "MineScape server bundle does not contain exactly the canonical managed World Border configuration.";

        if (!bundle.TryGetProperty("datapacks_low_to_high", out var datapacks) || datapacks.ValueKind != JsonValueKind.Array ||
            datapacks.GetArrayLength() != DatapackContracts.Length)
            return "MineScape server bundle must lock exactly seven datapacks in canonical low-to-high order.";
        var datapackError = ValidateDatapacks(datapacks, bundleFiles);
        if (datapackError is not null) return datapackError;

        if (!status.TryGetProperty("datapack_order_contract", out var statusOrder) || statusOrder.ValueKind != JsonValueKind.Object ||
            !StringEquals(statusOrder, "direction", "lowest-to-highest") ||
            !BooleanIs(statusOrder, "activation_evidence_required", true) ||
            !statusOrder.TryGetProperty("entries", out var statusEntries) || !JsonElement.DeepEquals(statusEntries, datapacks))
            return "MineScape assembly record is not bound to the server bundle's exact datapack order.";

        if (!status.TryGetProperty("files", out var statusFilesElement) || statusFilesElement.ValueKind != JsonValueKind.Array)
            return "MineScape assembly record has no managed-file inventory.";
        var statusError = ReadStatusInventory(statusFilesElement, out var statusFiles);
        if (statusError is not null) return statusError;
        var expectedStatus = new Dictionary<string, string>(bundleFiles, StringComparer.OrdinalIgnoreCase)
        {
            ["fabric-server-launch.jar"] = launcherHashElement.GetString()!
        };
        if (statusFiles.Count != expectedStatus.Count || expectedStatus.Any(item =>
                !statusFiles.TryGetValue(item.Key, out var value) || !HashEquals(item.Value, value)))
            return "MineScape assembly inventory differs from the deterministic server bundle and launcher.";
        return null;
    }

    private static string? ValidateConfigurationFiles(MinecraftOptions server, JsonElement bundle, string workingRoot)
    {
        var serverProperties = Path.Combine(workingRoot, "server.properties");
        var bridgeProperties = Path.Combine(workingRoot, "config", "minescape", "bridge.properties");
        var worldBorderConfiguration = Path.Combine(workingRoot, "config", "worldborder.json5");
        if (!HashMatchesProperty(bundle, "server_properties_sha512", serverProperties))
            return "MineScape server.properties differs from the deterministic server bundle.";
        if (!HashMatchesProperty(bundle, "bridge_properties_sha512", bridgeProperties))
            return "MineScape Bridge properties differ from the deterministic server bundle.";
        if (!HashMatchesProperty(bundle, "world_border_config_sha512", worldBorderConfiguration))
            return "MineScape World Border configuration differs from the deterministic server bundle.";

        var serverValues = ReadProperties(serverProperties);
        var required = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["level-seed"] = CanonicalSeed,
            ["online-mode"] = "true",
            ["white-list"] = "true",
            ["enforce-whitelist"] = "true",
            ["enable-rcon"] = "false",
            ["gamemode"] = "survival",
            ["server-ip"] = string.Empty,
            ["server-port"] = server.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        if (required.Any(item => !serverValues.TryGetValue(item.Key, out var value) || value != item.Value))
            return "MineScape server.properties no longer has the canonical identity/security settings.";
        var bridgeValues = ReadProperties(bridgeProperties);
        if (!bridgeValues.TryGetValue("bridge.bind", out var bind) || bind != "127.0.0.1")
            return "MineScape Bridge is not locked to loopback.";
        if (!bridgeValues.TryGetValue("instance.mode", out var instanceMode) || instanceMode != "family")
            return "MineScape Bridge is not configured for the family runtime.";

        var worldBorderError = ValidateWorldBorderConfiguration(worldBorderConfiguration);
        if (worldBorderError is not null) return worldBorderError;
        return null;
    }

    private static string? ValidateWorldBorderConfiguration(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return "MineScape World Border configuration is not a flat JSON object.";

        var booleans = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["enableCustomOverworldBorder"] = true,
            ["enableCustomNetherBorder"] = true,
            ["enableCustomEndBorder"] = true,
            ["shouldLoopToOppositeBorder"] = false
        };
        var integers = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["distanceTeleportedBack"] = 32,
            ["overworldBorderPositiveX"] = 16384,
            ["overworldBorderNegativeX"] = -16384,
            ["overworldBorderPositiveZ"] = 16384,
            ["overworldBorderNegativeZ"] = -16384,
            ["netherBorderPositiveX"] = 2048,
            ["netherBorderNegativeX"] = -2048,
            ["netherBorderPositiveZ"] = 2048,
            ["netherBorderNegativeZ"] = -2048,
            ["endBorderPositiveX"] = 8192,
            ["endBorderNegativeX"] = -8192,
            ["endBorderPositiveZ"] = 8192,
            ["endBorderNegativeZ"] = -8192
        };
        var strings = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["nearBorderMessage"] = "The edge of this finite world is close.",
            ["hitBorderMessage"] = "You reached the world edge and were moved safely inward.",
            ["loopBorderMessage"] = "World-edge looping is disabled."
        };
        var expectedNames = booleans.Keys.Concat(integers.Keys).Concat(strings.Keys).ToHashSet(StringComparer.Ordinal);
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!expectedNames.Contains(property.Name) || !seenNames.Add(property.Name))
                return "MineScape World Border configuration has an unexpected or duplicate setting.";
        }
        if (!seenNames.SetEquals(expectedNames) ||
            booleans.Any(item => !BooleanIs(root, item.Key, item.Value)) ||
            integers.Any(item => !IntegerEquals(root, item.Key, item.Value)) ||
            strings.Any(item => !StringEquals(root, item.Key, item.Value)))
            return "MineScape World Border configuration no longer has the canonical finite-world semantics.";
        return null;
    }

    private static string? ReadAndVerifyBundleInventory(JsonElement entries, string workingRoot,
                                                        out Dictionary<string, string> result)
    {
        result = new(StringComparer.OrdinalIgnoreCase);
        var rootPrefix = workingRoot.EndsWith(Path.DirectorySeparatorChar)
            ? workingRoot
            : workingRoot + Path.DirectorySeparatorChar;
        foreach (var entry in entries.EnumerateArray())
        {
            if (!TryReadInventoryEntry(entry, out var path, out var expected) ||
                path.Contains('\\') || Path.IsPathRooted(path) || path.Split('/').Any(part => part is "." or "..") ||
                (!path.StartsWith("mods/", StringComparison.Ordinal) &&
                 !path.StartsWith("world/datapacks/", StringComparison.Ordinal) &&
                 !path.Equals(WorldBorderConfigurationPath, StringComparison.Ordinal)) ||
                !result.TryAdd(path, expected))
                return "MineScape server bundle contains a malformed, unsafe, or duplicate managed-file entry.";
            var candidate = Path.GetFullPath(Path.Combine(workingRoot, path.Replace('/', Path.DirectorySeparatorChar)));
            if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(candidate))
                return $"MineScape server bundle file is missing or escapes production: {path}";
            if (!HashEquals(HashFile(candidate), expected))
                return $"MineScape managed file hash changed after bundle locking: {path}";
        }
        return null;
    }

    private static string? ReadStatusInventory(JsonElement entries, out Dictionary<string, string> result)
    {
        result = new(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries.EnumerateArray())
        {
            if (!TryReadInventoryEntry(entry, out var recorded, out var hash))
                return "MineScape assembly inventory contains a malformed entry.";
            var normalized = recorded.Replace('\\', '/');
            if (!normalized.StartsWith(ProductionPrefix, StringComparison.Ordinal) ||
                !result.TryAdd(normalized[ProductionPrefix.Length..], hash))
                return "MineScape assembly inventory contains an unsafe or duplicate path.";
        }
        return null;
    }

    private static string? ValidateDatapacks(JsonElement datapacks, IReadOnlyDictionary<string, string> files)
    {
        var lockedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var entry in datapacks.EnumerateArray())
        {
            var contract = DatapackContracts[index];
            if (entry.ValueKind != JsonValueKind.Object ||
                !IntegerEquals(entry, "ordinal", contract.Ordinal) ||
                !IntegerEquals(entry, "priority", contract.Priority) ||
                !StringEquals(entry, "kind", contract.Kind) ||
                !StringEquals(entry, "source", contract.Source) ||
                !entry.TryGetProperty("destination", out var destinationElement) || destinationElement.ValueKind != JsonValueKind.String ||
                !entry.TryGetProperty("sha512", out var hashElement) || hashElement.ValueKind != JsonValueKind.String)
                return $"MineScape datapack order entry {index} does not match the canonical contract.";
            var destination = destinationElement.GetString() ?? string.Empty;
            var hash = hashElement.GetString() ?? string.Empty;
            var exactDestination = contract.DestinationPrefix.EndsWith(".zip", StringComparison.Ordinal);
            if (Path.GetFileName(destination) != destination || !destination.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                (exactDestination ? destination != contract.DestinationPrefix : !destination.StartsWith(contract.DestinationPrefix, StringComparison.Ordinal)) ||
                !Sha512Pattern().IsMatch(hash))
                return $"MineScape datapack destination/hash {index} is unsafe or not canonical.";
            var path = "world/datapacks/" + destination;
            if (!lockedPaths.Add(path) || !files.TryGetValue(path, out var fileHash) || !HashEquals(fileHash, hash))
                return $"MineScape datapack {destination} is not bound to the managed-file inventory.";
            index++;
        }
        var inventoryDatapacks = files.Keys.Where(path => path.StartsWith("world/datapacks/", StringComparison.Ordinal)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!inventoryDatapacks.SetEquals(lockedPaths))
            return "MineScape managed-file inventory contains an unordered or undeclared datapack.";
        return null;
    }

    private static Dictionary<string, string> ReadProperties(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var separator = line.IndexOf('=');
            if (separator < 1 || !result.TryAdd(line[..separator], line[(separator + 1)..]))
                throw new InvalidDataException($"Malformed or duplicate property in {Path.GetFileName(path)}.");
        }
        return result;
    }

    private static bool HashMatchesProperty(JsonElement element, string property, string path) =>
        File.Exists(path) && element.TryGetProperty(property, out var expected) &&
        expected.ValueKind == JsonValueKind.String && HashEquals(HashFile(path), expected.GetString());

    private static bool TryReadInventoryEntry(JsonElement entry, out string path, out string hash)
    {
        path = string.Empty;
        hash = string.Empty;
        if (entry.ValueKind != JsonValueKind.Object ||
            !entry.TryGetProperty("path", out var pathElement) || pathElement.ValueKind != JsonValueKind.String ||
            !entry.TryGetProperty("sha512", out var hashElement) || hashElement.ValueKind != JsonValueKind.String)
            return false;
        path = pathElement.GetString() ?? string.Empty;
        hash = hashElement.GetString() ?? string.Empty;
        return path.Length > 0 && Sha512Pattern().IsMatch(hash);
    }

    private static string HashFile(string path)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA512.HashData(input)).ToLowerInvariant();
    }

    private static bool HashEquals(string actual, string? expected) =>
        expected is not null && Sha512Pattern().IsMatch(actual) && Sha512Pattern().IsMatch(expected) &&
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(actual), Encoding.ASCII.GetBytes(expected));

    private static bool SameString(JsonElement left, JsonElement right, string name) =>
        left.TryGetProperty(name, out var leftValue) && right.TryGetProperty(name, out var rightValue) &&
        leftValue.ValueKind == JsonValueKind.String && rightValue.ValueKind == JsonValueKind.String &&
        string.Equals(leftValue.GetString(), rightValue.GetString(), StringComparison.Ordinal);

    private static bool StringEquals(JsonElement element, string name, string expected) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
        string.Equals(value.GetString(), expected, StringComparison.Ordinal);

    private static bool BooleanIs(JsonElement element, string name, bool expected) =>
        element.TryGetProperty(name, out var value) &&
        value.ValueKind == (expected ? JsonValueKind.True : JsonValueKind.False);

    private static bool IntegerEquals(JsonElement element, string name, int expected) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var actual) && actual == expected;

    [GeneratedRegex("^[0-9a-f]{128}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha512Pattern();

    private sealed record DatapackContract(int Ordinal, int Priority, string Kind, string Source, string DestinationPrefix);
}
