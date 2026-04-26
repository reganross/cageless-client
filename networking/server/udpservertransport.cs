using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

public class UdpServerTransport : IServerSnapshotTransport
{
    private readonly Dictionary<ClientId, IPEndPoint> clientEndpoints = new();
    private UdpClient udpClient;

    public IReadOnlyCollection<ClientId> ConnectedClients => clientEndpoints.Keys;

    public void Start(int port)
    {
        Dispose();
        udpClient = new UdpClient(port);
    }

    public void RegisterClientEndpoint(ClientId clientId, IPEndPoint endpoint)
    {
        clientEndpoints[clientId] = endpoint;
    }

    public void UnregisterClientEndpoint(ClientId clientId)
    {
        clientEndpoints.Remove(clientId);
    }

    public void SendSnapshot(ClientId clientId, SnapshotPacket packet)
    {
        if (udpClient == null || !clientEndpoints.TryGetValue(clientId, out var endpoint))
        {
            return;
        }

        var bytes = SnapshotSerializer.Serialize(packet);
        udpClient.Send(bytes, bytes.Length, endpoint);
    }

    public void Dispose()
    {
        udpClient?.Dispose();
        udpClient = null;
    }
}
