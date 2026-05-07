using System.Collections.Generic;
using System.Net;

/// <summary>
/// In-memory server transport paired with <see cref="LoopbackClientTransport"/>.
/// Uses the same ingress rules as UDP without binding a socket.
/// </summary>
public sealed class LoopbackServerTransport : IServerNetworkTransport
{
    private static readonly IPEndPoint VirtualPeer = new(IPAddress.Loopback, 65123);

    private readonly Queue<byte[]> serverIngress;
    private readonly Queue<byte[]> clientIngress;
    private readonly Dictionary<ClientId, IPEndPoint> clientEndpoints = new();

    public LoopbackServerTransport(Queue<byte[]> serverIngress, Queue<byte[]> clientIngress)
    {
        this.serverIngress = serverIngress;
        this.clientIngress = clientIngress;
    }

    public IReadOnlyCollection<ClientId> ConnectedClients => clientEndpoints.Keys;

    public void Start(int port)
    {
    }

    public int ProcessIncoming(NetworkServer server)
    {
        int processed = 0;
        while (serverIngress.TryDequeue(out var bytes))
        {
            if (ServerPacketIngress.ProcessClientPacket(
                    server,
                    this,
                    clientEndpoints.TryGetValue,
                    RegisterEndpoint,
                    UnregisterEndpoint,
                    bytes,
                    VirtualPeer))
            {
                processed++;
            }
        }

        return processed;
    }

    public void SendSnapshot(ClientId clientId, SnapshotPacket packet)
    {
        if (!clientEndpoints.ContainsKey(clientId))
        {
            return;
        }

        var bytes = SnapshotSerializer.Serialize(packet);
        clientIngress.Enqueue(bytes);
    }

    public void Dispose()
    {
        clientEndpoints.Clear();
        serverIngress.Clear();
        clientIngress.Clear();
    }

    private void RegisterEndpoint(ClientId clientId, IPEndPoint endpoint)
    {
        clientEndpoints[clientId] = endpoint;
    }

    private void UnregisterEndpoint(ClientId clientId)
    {
        clientEndpoints.Remove(clientId);
    }
}
