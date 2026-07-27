using System.IO.Pipes;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace MineDeck.Services;

public sealed record SupervisorCommand(string Action);
public sealed record SupervisorReply(bool Accepted, string Message);

public sealed class SingleInstanceBroker : IDisposable
{
    private readonly string _pipeName;
    private readonly string _lockPath;
    private FileStream? _lockFile;

    public SingleInstanceBroker(string instanceName, string dataDirectory)
    {
        var identity = OperatingSystem.IsWindows() ? WindowsIdentity.GetCurrent().User?.Value : Environment.UserName;
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{identity}|{instanceName}")))[..20];
        _pipeName = $"MineDeck.Supervisor.{digest}";
        Directory.CreateDirectory(dataDirectory);
        _lockPath = Path.Combine(dataDirectory, $"supervisor-{digest}.lock");
    }

    public bool TryAcquire()
    {
        try
        {
            _lockFile = new FileStream(_lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            _lockFile.SetLength(0);
            using var writer = new StreamWriter(_lockFile, Encoding.UTF8, leaveOpen: true);
            writer.Write($"{Environment.ProcessId}|{DateTimeOffset.UtcNow:O}");
            writer.Flush();
            _lockFile.Flush(flushToDisk: true);
            return true;
        }
        catch (IOException)
        {
            _lockFile?.Dispose();
            _lockFile = null;
            return false;
        }
    }

    public async Task<SupervisorReply> SendAsync(SupervisorCommand command, CancellationToken cancellationToken)
    {
        using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await client.ConnectAsync(5000, cancellationToken).ConfigureAwait(false);
        using var writer = new StreamWriter(client, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(client, Encoding.UTF8, leaveOpen: true);
        await writer.WriteLineAsync(JsonSerializer.Serialize(command).AsMemory(), cancellationToken).ConfigureAwait(false);
        var replyLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(replyLine)
            ? new(false, "Supervisor returned an empty IPC response.")
            : JsonSerializer.Deserialize<SupervisorReply>(replyLine) ?? new(false, "Supervisor returned invalid IPC JSON.");
    }

    public async Task ListenAsync(Func<SupervisorCommand, CancellationToken, Task<SupervisorReply>> handler, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var server = new NamedPipeServerStream(
                _pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
            using var writer = new StreamWriter(server, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            var commandLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            var command = string.IsNullOrWhiteSpace(commandLine) ? null : JsonSerializer.Deserialize<SupervisorCommand>(commandLine);
            var reply = command is null ? new(false, "Invalid supervisor command.") : await handler(command, cancellationToken).ConfigureAwait(false);
            await writer.WriteLineAsync(JsonSerializer.Serialize(reply).AsMemory(), cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        _lockFile?.Dispose();
        _lockFile = null;
    }
}
