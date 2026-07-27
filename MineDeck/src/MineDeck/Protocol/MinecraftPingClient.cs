using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using MineDeck.Models;

namespace MineDeck.Protocol;

public sealed class MinecraftPingClient
{
    private const int MaximumPacketBytes = 1_048_576;

    public async Task<PingResult> PingAsync(string host, int port, int protocolVersion, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        try
        {
            using var client = new TcpClient { NoDelay = true };
            await client.ConnectAsync(host, port, timeoutCts.Token).ConfigureAwait(false);
            await using var stream = client.GetStream();

            var handshake = new MemoryStream();
            WriteVarInt(handshake, 0);
            WriteVarInt(handshake, protocolVersion);
            WriteString(handshake, host);
            Span<byte> portBytes = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(portBytes, checked((ushort)port));
            handshake.Write(portBytes);
            WriteVarInt(handshake, 1);
            await WritePacketAsync(stream, handshake.ToArray(), timeoutCts.Token).ConfigureAwait(false);
            await WritePacketAsync(stream, new byte[] { 0 }, timeoutCts.Token).ConfigureAwait(false);

            var response = await ReadPacketAsync(stream, timeoutCts.Token).ConfigureAwait(false);
            using var responseStream = new MemoryStream(response, writable: false);
            if (ReadVarInt(responseStream) != 0) throw new InvalidDataException("Unexpected Minecraft status packet.");
            var json = ReadString(responseStream);

            var stopwatch = Stopwatch.StartNew();
            var pingPayload = new byte[9];
            pingPayload[0] = 1;
            BinaryPrimitives.WriteInt64BigEndian(pingPayload.AsSpan(1), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            await WritePacketAsync(stream, pingPayload, timeoutCts.Token).ConfigureAwait(false);
            var pong = await ReadPacketAsync(stream, timeoutCts.Token).ConfigureAwait(false);
            stopwatch.Stop();
            if (pong.Length != 9 || pong[0] != 1) throw new InvalidDataException("Invalid Minecraft pong packet.");

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var version = root.TryGetProperty("version", out var v) && v.TryGetProperty("name", out var n) ? n.GetString() : null;
            var online = root.TryGetProperty("players", out var players) && players.TryGetProperty("online", out var o) ? o.GetInt32() : 0;
            var max = root.TryGetProperty("players", out players) && players.TryGetProperty("max", out var m) ? m.GetInt32() : 0;
            var motd = ExtractDescription(root);
            return new PingResult(true, version, online, max, stopwatch.ElapsedMilliseconds, motd, null, checkedAt);
        }
        catch (Exception exception) when (exception is SocketException or IOException or JsonException or OperationCanceledException or InvalidDataException)
        {
            var message = exception is OperationCanceledException ? "Ping timed out." : exception.Message;
            return new PingResult(false, null, 0, 0, 0, null, message, checkedAt);
        }
    }

    public static void WriteVarInt(Stream stream, int value)
    {
        var unsigned = unchecked((uint)value);
        do
        {
            var current = (byte)(unsigned & 0x7F);
            unsigned >>= 7;
            if (unsigned != 0) current |= 0x80;
            stream.WriteByte(current);
        } while (unsigned != 0);
    }

    public static int ReadVarInt(Stream stream)
    {
        var value = 0;
        for (var position = 0; position < 35; position += 7)
        {
            var read = stream.ReadByte();
            if (read < 0) throw new EndOfStreamException();
            value |= (read & 0x7F) << position;
            if ((read & 0x80) == 0) return value;
        }
        throw new InvalidDataException("VarInt exceeds five bytes.");
    }

    private static async Task WritePacketAsync(Stream stream, byte[] payload, CancellationToken cancellationToken)
    {
        using var framed = new MemoryStream();
        WriteVarInt(framed, payload.Length);
        framed.Write(payload);
        await stream.WriteAsync(framed.ToArray(), cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadPacketAsync(Stream stream, CancellationToken cancellationToken)
    {
        var length = await ReadVarIntAsync(stream, cancellationToken).ConfigureAwait(false);
        if (length is < 1 or > MaximumPacketBytes) throw new InvalidDataException("Invalid Minecraft packet length.");
        var result = new byte[length];
        await stream.ReadExactlyAsync(result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static async Task<int> ReadVarIntAsync(Stream stream, CancellationToken cancellationToken)
    {
        var value = 0;
        var one = new byte[1];
        for (var position = 0; position < 35; position += 7)
        {
            await stream.ReadExactlyAsync(one, cancellationToken).ConfigureAwait(false);
            value |= (one[0] & 0x7F) << position;
            if ((one[0] & 0x80) == 0) return value;
        }
        throw new InvalidDataException("VarInt exceeds five bytes.");
    }

    private static void WriteString(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteVarInt(stream, bytes.Length);
        stream.Write(bytes);
    }

    private static string ReadString(Stream stream)
    {
        var length = ReadVarInt(stream);
        if (length is < 0 or > MaximumPacketBytes) throw new InvalidDataException("Invalid Minecraft string length.");
        var bytes = new byte[length];
        stream.ReadExactly(bytes);
        return Encoding.UTF8.GetString(bytes);
    }

    private static string? ExtractDescription(JsonElement root)
    {
        if (!root.TryGetProperty("description", out var description)) return null;
        if (description.ValueKind == JsonValueKind.String) return description.GetString();
        if (description.ValueKind == JsonValueKind.Object && description.TryGetProperty("text", out var text)) return text.GetString();
        return description.ToString();
    }
}
