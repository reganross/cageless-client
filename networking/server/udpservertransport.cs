using System.Collections.Generic;
using System.IO;
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
        try
        {
            using var stream = new MemoryStream(bytes);
            using var reader = new BinaryReader(stream);

            var kind = (ClientPacketKind)reader.ReadInt32();
            switch (kind)
            {
                case ClientPacketKind.Connect:
                    return ProcessConnect(server, reader, remoteEndPoint);
                case ClientPacketKind.Controller:
                    return ProcessController(server, reader, remoteEndPoint);
                case ClientPacketKind.Disconnect:
                    return ProcessDisconnect(server, reader);
                default:
                    return false;
            }
        }
        catch (EndOfStreamException)
        {
            return false;
        }
        catch (InvalidDataException)
        {
            return false;
        }
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

    private bool ProcessConnect(
        NetworkServer server,
        BinaryReader reader,
        IPEndPoint remoteEndPoint)
    {
        var clientId = new ClientId(reader.ReadInt32());
        if (!server.ConnectClient(clientId))
        {
            return false;
        }

        RegisterClientEndpoint(clientId, remoteEndPoint);
        server.FlushSnapshots(this);
        return true;
    }

    private bool ProcessController(
        NetworkServer server,
        BinaryReader reader,
        IPEndPoint remoteEndPoint)
    {
        int payloadLength = reader.ReadInt32();
        if (payloadLength < 0)
        {
            return false;
        }

        var command = ClientCommandSerializer.Deserialize(reader.ReadBytes(payloadLength));
        if (!clientEndpoints.TryGetValue(command.ClientId, out var registeredEndpoint)
            || !registeredEndpoint.Equals(remoteEndPoint))
        {
            return false;
        }

        return server.ReceiveCommand(command);
    }

    private bool ProcessDisconnect(
        NetworkServer server,
        BinaryReader reader)
    {
        var clientId = new ClientId(reader.ReadInt32());
        UnregisterClientEndpoint(clientId);
        server.DisconnectClient(clientId);
        return true;
    }
}
