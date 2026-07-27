using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MineDeck.Configuration;
using MineDeck.Models;

namespace MineDeck.Bridge;

public sealed class HttpBridgeGateway : IBridgeGateway, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] MissingCapabilities =
    {
        "authenticated pending-player capture/denial", "native whitelist synchronization", "role assignment",
        "characters/hearts", "fog/map", "settlements/wards", "progress", "loot provenance", "rewards",
        "consistent backups", "Steward leases", "MineJammer promotion", "clean server stop"
    };

    private readonly HttpClient _client;
    private readonly BridgeOptions _options;

    public HttpBridgeGateway(MineDeckOptions options)
    {
        _options = options.Bridge;
        _client = new HttpClient
        {
            BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds)
        };
    }

    public async Task<BridgeHealth> HealthAsync(CancellationToken cancellationToken)
    {
        var result = await GetAsync<HealthDto>("v1/health", cancellationToken).ConfigureAwait(false);
        if (!result.Ok || result.Data is null) return new(false, result.Error, null, null, false);
        var health = result.Data;
        return new(true, $"{health.Status}: {health.Detail}", health.BridgeVersion, health.WorldEpoch, false);
    }

    public async Task<ApiEnvelope<BridgeSnapshot>> SnapshotAsync(CancellationToken cancellationToken)
    {
        var playersTask = GetAsync<PlayersEnvelope>("v1/players", cancellationToken);
        var whitelistTask = GetAsync<WhitelistEnvelope>("v1/whitelist", cancellationToken);
        var assistTask = GetAsync<AssistEnvelope>("v1/family-assist", cancellationToken);
        await Task.WhenAll(playersTask, whitelistTask, assistTask).ConfigureAwait(false);
        var players = playersTask.Result;
        var whitelist = whitelistTask.Result;
        var assists = assistTask.Result;
        if (!players.Ok) return ApiEnvelope<BridgeSnapshot>.Failure(players.Error, false);
        if (!whitelist.Ok) return ApiEnvelope<BridgeSnapshot>.Failure(whitelist.Error, false);
        if (!assists.Ok) return ApiEnvelope<BridgeSnapshot>.Failure(assists.Error, false);

        var playerRows = players.Data?.Players ?? Array.Empty<PlayerDto>();
        var whitelistRows = whitelist.Data?.Entries ?? Array.Empty<WhitelistDto>();
        var byPlayer = playerRows.ToDictionary(x => x.Uuid);
        var byWhitelist = whitelistRows.ToDictionary(x => x.Uuid);

        var pending = playerRows
            .Where(player => !byWhitelist.ContainsKey(player.Uuid))
            .Select(player => new PendingPlayer(
                player.Uuid, player.CurrentName, null,
                "Bridge /v1/players; authenticated-pending timestamps are not exposed yet", null))
            .ToArray();

        var enrolled = whitelistRows.Select(entry =>
        {
            byPlayer.TryGetValue(entry.Uuid, out var player);
            return new EnrolledPlayer(
                entry.Uuid,
                player?.CurrentName ?? (string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.Uuid.ToString() : entry.DisplayName),
                player?.AccessRole ?? "Not exposed",
                entry.Status,
                player?.Online ?? false,
                player?.SessionMode ?? "Not exposed",
                player?.CountsTowardFamilyExploration ?? false);
        }).ToArray();

        var activity = (assists.Data?.Tickets ?? Array.Empty<AssistDto>())
            .Select(ticket => new ActivityEvent(
                ticket.TicketId.ToString("N"), ticket.CreatedAt, "family-assist",
                $"{ticket.Action} for {ticket.PlayerId}: {ticket.Reason}", ticket.Status))
            .OrderByDescending(x => x.At)
            .ToArray();

        return ApiEnvelope<BridgeSnapshot>.Success(new BridgeSnapshot(
            pending, enrolled, Array.Empty<CharacterSummary>(), Array.Empty<FogSummary>(),
            Array.Empty<SettlementSummary>(), Array.Empty<PlayerProgress>(), Array.Empty<LootProvenance>(),
            Array.Empty<StewardshipEvent>(), Array.Empty<RewardEvent>(), activity,
            "Natural Matcha economy: 1x (locked)", true, MissingCapabilities));
    }

    public async Task<BridgeResponse> ApprovePlayerAsync(PlayerDecision request, CancellationToken cancellationToken)
    {
        var result = await SendAsync<WhitelistDto>(HttpMethod.Post, "v1/whitelist",
            new { uuid = request.Uuid, displayName = (string?)null }, cancellationToken).ConfigureAwait(false);
        if (!result.Ok || result.Data is null) return new(false, result.Error);
        var roleNote = string.IsNullOrWhiteSpace(request.Role) || request.Role.Equals("Not assigned", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : $" Requested role ‘{request.Role}’ was not applied because the current Bridge has no role endpoint.";
        return new(true,
            $"Whitelist intention recorded for UUID {request.Uuid} with state {result.Data.Status}. Native Minecraft whitelist synchronization is still a release gate.{roleNote}");
    }

    public Task<BridgeResponse> DenyPlayerAsync(PlayerDenial request, CancellationToken cancellationToken) =>
        Task.FromResult(Unsupported("The current Bridge has no durable pending-player denial endpoint; the UUID remains unapproved."));

    public async Task<BridgeResponse> RevokePlayerAsync(PlayerDenial request, CancellationToken cancellationToken)
    {
        var result = await SendAsync<RemoveWhitelistDto>(HttpMethod.Delete, $"v1/whitelist/{request.Uuid:D}", null, cancellationToken).ConfigureAwait(false);
        return result.Ok && result.Data is not null
            ? new(result.Data.Removed, result.Data.Removed ? $"Whitelist intention removed for UUID {request.Uuid}." : "UUID was not in the Bridge whitelist.")
            : new(false, result.Error);
    }

    public async Task<BridgeResponse> RequestFamilyAssistAsync(FamilyAssistRequest request, CancellationToken cancellationToken)
    {
        var valid = new[] { "SET_EASY", "SET_NORMAL", "START_AT_DAWN", "RESCUE_TO_PUBLIC_SPAWN", "MATCHA_HINT" };
        if (!valid.Contains(request.Action, StringComparer.Ordinal)) return new(false, "Unknown Family Assist action.");
        var result = await SendAsync<AssistDto>(HttpMethod.Post, "v1/family-assist", new
        {
            playerId = request.PlayerId,
            action = request.Action,
            reason = request.Reason
        }, cancellationToken).ConfigureAwait(false);
        if (!result.Ok || result.Data is null) return new(false, result.Error);
        return new(true, $"Family Assist ticket {result.Data.TicketId} is {result.Data.Status}. The current game-action adapter may still be release-gated.", result.Data.TicketId.ToString());
    }

    public Task<BridgeResponse> PardonCharacterAsync(CharacterDecision request, CancellationToken ct) => UnsupportedAsync("Character pardon");
    public Task<BridgeResponse> BeginNewCharacterAsync(NewCharacterRequest request, CancellationToken ct) => UnsupportedAsync("New-character transaction");
    public Task<BridgeResponse> RegisterSettlementAsync(SettlementRegistration request, CancellationToken ct) => UnsupportedAsync("Settlement registration");
    public Task<BridgeResponse> CorrectRewardAsync(RewardCorrection request, CancellationToken ct) => UnsupportedAsync("Reward correction");
    public Task<BridgeResponse> RequestStewardLeaseAsync(StewardLeaseRequest request, CancellationToken ct) => UnsupportedAsync("Steward lease");
    public Task<BridgeResponse> EndStewardLeaseAsync(CancellationToken ct) => UnsupportedAsync("Steward lease end");
    public Task<BridgeResponse> StopProductionAsync(CancellationToken ct) => UnsupportedAsync("Clean production stop");
    public Task<BridgeResponse> StopMineJammerAsync(CancellationToken ct) => UnsupportedAsync("Clean MineJammer stop");
    public Task<BridgeResponse> ComparePromotionAsync(PromotionRequest request, CancellationToken ct) => UnsupportedAsync("MineJammer comparison");
    public Task<BridgeResponse> CommitPromotionAsync(PromotionRequest request, CancellationToken ct) => UnsupportedAsync("MineJammer promotion");
    public Task<ApiEnvelope<BackupPreparation>> PrepareBackupAsync(BackupPreparationRequest request, CancellationToken ct) =>
        Task.FromResult(ApiEnvelope<BackupPreparation>.Failure("The current Bridge has no quiesced-backup endpoint; MineDeck refused to copy the live world.", false));
    public Task<BridgeResponse> CompleteBackupAsync(BackupCompleteRequest request, CancellationToken ct) => UnsupportedAsync("Backup completion");

    public void Dispose() => _client.Dispose();

    private async Task<CallResult<T>> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        var token = ReadToken(out var tokenError);
        if (token is null) return new(false, default, tokenError);
        try
        {
            using var request = Create(HttpMethod.Get, path, token);
            using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return new(false, default, await ErrorAsync(response, cancellationToken).ConfigureAwait(false));
            var data = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false);
            return data is null ? new(false, default, "Bridge returned an empty response.") : new(true, data, string.Empty);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new(false, default, $"Bridge unavailable: {exception.Message}");
        }
    }

    private async Task<CallResult<T>> SendAsync<T>(HttpMethod method, string path, object? payload, CancellationToken cancellationToken)
    {
        var token = ReadToken(out var tokenError);
        if (token is null) return new(false, default, tokenError);
        try
        {
            using var request = Create(method, path, token, payload);
            using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return new(false, default, await ErrorAsync(response, cancellationToken).ConfigureAwait(false));
            var data = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false);
            return data is null ? new(false, default, "Bridge returned an empty response.") : new(true, data, string.Empty);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new(false, default, $"Bridge unavailable; no mutation was retried: {exception.Message}");
        }
    }

    private HttpRequestMessage Create(HttpMethod method, string path, string token, object? payload = null)
    {
        var request = new HttpRequestMessage(method, path) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) } };
        if (payload is not null) request.Content = JsonContent.Create(payload, options: JsonOptions);
        return request;
    }

    private string? ReadToken(out string error)
    {
        try
        {
            var token = string.IsNullOrWhiteSpace(_options.TokenEnvironmentVariable)
                ? null
                : Environment.GetEnvironmentVariable(_options.TokenEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(_options.TokenFile) && File.Exists(_options.TokenFile))
                token = File.ReadAllText(_options.TokenFile).Trim();
            if (string.IsNullOrWhiteSpace(token) || token.Length < 32)
            {
                error = "Bridge token is not available yet. Start MineScape once or configure the local token path.";
                return null;
            }
            error = string.Empty;
            return token;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = $"Bridge token could not be read: {exception.Message}";
            return null;
        }
    }

    private static async Task<string> ErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            if (document.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("message", out var message))
                return $"Bridge HTTP {(int)response.StatusCode}: {message.GetString()}";
        }
        catch (JsonException) { }
        return $"Bridge returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).";
    }

    private static BridgeResponse Unsupported(string capability) => new(false, capability);
    private static Task<BridgeResponse> UnsupportedAsync(string capability) =>
        Task.FromResult(Unsupported($"{capability} is not exposed by the current Bridge; no mutation was attempted."));

    private sealed record CallResult<T>(bool Ok, T? Data, string Error);
    private sealed record HealthDto(string Status, string BridgeVersion, string MinecraftVersion, string WorldEpoch, int OnlinePlayers, DateTimeOffset ObservedAt, string Detail);
    private sealed record PlayersEnvelope(IReadOnlyList<PlayerDto> Players);
    private sealed record PlayerDto(Guid Uuid, string CurrentName, bool Online, string AccessRole, string SessionMode, bool CountsTowardFamilyExploration);
    private sealed record WhitelistEnvelope(IReadOnlyList<WhitelistDto> Entries);
    private sealed record WhitelistDto(Guid Uuid, string DisplayName, string Status, DateTimeOffset ChangedAt);
    private sealed record RemoveWhitelistDto(bool Removed, Guid Uuid);
    private sealed record AssistEnvelope(IReadOnlyList<AssistDto> Tickets);
    private sealed record AssistDto(Guid TicketId, Guid PlayerId, string Action, string Reason, string Status, DateTimeOffset CreatedAt);
}
