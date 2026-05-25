using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

public class UdpServerTransport : IServerNetworkTransport
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

    public int ProcessIncoming(NetworkServer server)
    {
        if (udpClient == null)
        {
            return 0;
        }

        int processed = 0;
        while (udpClient.Available > 0)
        {
            IPEndPoint remoteEndPoint = null;
            var bytes = udpClient.Receive(ref remoteEndPoint);
            if (ProcessClientPacket(server, bytes, remoteEndPoint))
            {
                processed++;
            }
        }

        return processed;
    }

    public bool ProcessClientPacket(
        NetworkServer server,
        byte[] bytes,
        IPEndPoint remoteEndPoint)
    {
        return ServerPacketIngress.ProcessClientPacket(
            server,
            this,
            clientEndpoints.TryGetValue,
            RegisterClientEndpoint,
            UnregisterClientEndpoint,
            bytes,
            remoteEndPoint);
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
