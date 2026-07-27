using System.Net.Http.Json;
using MineDeck.Configuration;
using MineDeck.Models;

namespace MineDeck.Services;

public sealed class PublicStatusClient : IDisposable
{
    private readonly HttpClient _client;
    public PublicStatusClient(MineDeckOptions options) => _client = new HttpClient
    {
        BaseAddress = new Uri(options.Dashboard.PublicBaseUrl.TrimEnd('/') + "/"),
        Timeout = TimeSpan.FromSeconds(4)
    };

    public async Task<RuntimeStatus> GetAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _client.GetFromJsonAsync<RuntimeStatus>("api/public/status", cancellationToken).ConfigureAwait(false)
                ?? Offline("MineDeck returned an empty status.");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return Offline($"MineDeck supervisor unavailable: {exception.Message}") with { State = LifecycleState.Fault };
        }
    }

    public void Dispose() => _client.Dispose();

    private static RuntimeStatus Offline(string message) => new(
        LifecycleState.Offline, OperationKind.None, message,
        new(false, null, 0, 0, 0, null, message, DateTimeOffset.UtcNow),
        new(false, null, 0, 0, 0, null, "Not checked.", DateTimeOffset.UtcNow),
        false, message, null, null, 0, "Unavailable");
}
