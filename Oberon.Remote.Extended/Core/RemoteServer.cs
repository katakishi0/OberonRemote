using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Fleck;

namespace Oberon.Extended;

public sealed class RemoteServer : IDisposable
{
    private WebSocketServer? server;
    private readonly ConcurrentDictionary<IWebSocketConnection, byte> clients = new();
    private byte[] packet = Protocol.NeutralPacket();
    private byte[] rumble = new byte[16];
    private static readonly byte[] SilentRumble = new byte[16];
    private long lastRequest;
    private long responses;
    private int paused;
    public Action<string>? Log { get; set; }
    public bool Running => server != null;
    public int Clients => clients.Count;
    public long Responses => Interlocked.Read(ref responses);
    public bool Paused { get => Volatile.Read(ref paused) == 1; set { Volatile.Write(ref paused, value ? 1 : 0); if (value) Volatile.Write(ref rumble, new byte[16]); } }
    public double RequestAgeSeconds => Interlocked.Read(ref lastRequest) == 0 ? double.PositiveInfinity : Stopwatch.GetElapsedTime(Interlocked.Read(ref lastRequest)).TotalSeconds;
    public void Publish(byte[] snapshot) => Volatile.Write(ref packet, snapshot);
    public byte[] Rumble => Paused || RequestAgeSeconds > 1 ? SilentRumble : Volatile.Read(ref rumble);

    public static bool IsLocalBindAddress(string address)
    {
        if (!IPAddress.TryParse(address, out var parsed) || parsed.AddressFamily != AddressFamily.InterNetwork) return false;
        if (IPAddress.IsLoopback(parsed) || parsed.Equals(IPAddress.Any)) return true;
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Any(a => a.Address.Equals(parsed));
    }
    public void Start(int port = 26401, string bindIp = "0.0.0.0")
    {
        if (server != null) return;
        if (port is < 1 or > 65535 || !IsLocalBindAddress(bindIp))
            throw new ArgumentException("The selected IP is not assigned to this computer.");
        FleckLog.LogAction = (level, message, exception) => { if (level >= LogLevel.Warn) Log?.Invoke(message); };
        var candidate = new WebSocketServer($"ws://{bindIp}:{port}");
        try
        {
            candidate.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    // One active Xbox, matching the upstream use case. Pairing probes can coexist.
                    clients.TryAdd(socket, 0);
                    Log?.Invoke("Conexão recebida: " + socket.ConnectionInfo.ClientIpAddress);
                    socket.Send(new byte[] { 0x0A }.Concat(Encoding.UTF8.GetBytes(Environment.MachineName)).ToArray());
                };
                socket.OnBinary = data =>
                {
                    if (data.Length is not (1 or 17) || data[0] != 0xFA) return;
                    Interlocked.Exchange(ref lastRequest, Stopwatch.GetTimestamp());
                    if (data.Length == 17 && !Paused) Volatile.Write(ref rumble, data.AsSpan(1, 16).ToArray());
                    socket.Send(Paused ? Protocol.NeutralPacket() : Volatile.Read(ref packet));
                    Interlocked.Increment(ref responses);
                };
                socket.OnClose = () => { clients.TryRemove(socket, out _); if (clients.IsEmpty) Volatile.Write(ref rumble, new byte[16]); Log?.Invoke("Conexão encerrada."); };
                socket.OnError = error => { Log?.Invoke("Rede: " + error.Message); Volatile.Write(ref rumble, new byte[16]); };
            });
            server = candidate;
            Log?.Invoke($"Server listening on {bindIp}:{port}.");
        }
        catch { candidate.Dispose(); throw; }
    }
    public void Stop()
    {
        foreach (var socket in clients.Keys) { try { socket.Close(); } catch { } }
        clients.Clear(); server?.Dispose(); server = null;
        Volatile.Write(ref rumble, new byte[16]); Interlocked.Exchange(ref lastRequest, 0);
        Log?.Invoke("Servidor parado.");
    }
    public void Dispose() => Stop();
}
