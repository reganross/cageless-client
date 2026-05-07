using System;
using System.IO;
using System.Net;

/// <summary>
/// Shared client→server packet decode path for transports (UDP, loopback, tests).
/// </summary>
public static class ServerPacketIngress
{
    public delegate bool TryLookupClientEndpoint(ClientId clientId, out IPEndPoint endpoint);

    public static bool ProcessClientPacket(
        NetworkServer server,
        IServerSnapshotTransport snapshotFlushTransport,
        TryLookupClientEndpoint tryLookupRegisteredEndpoint,
        Action<ClientId, IPEndPoint> registerEndpoint,
        Action<ClientId> unregisterEndpoint,
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
                    return ProcessConnect(
                        server,
                        snapshotFlushTransport,
                        registerEndpoint,
                        reader,
                        remoteEndPoint);
                case ClientPacketKind.Controller:
                    return ProcessController(
                        server,
                        tryLookupRegisteredEndpoint,
                        reader,
                        remoteEndPoint);
                case ClientPacketKind.Disconnect:
                    return ProcessDisconnect(server, unregisterEndpoint, reader);
                case ClientPacketKind.Attack:
                    return ProcessAttack(
                        server,
                        tryLookupRegisteredEndpoint,
                        reader,
                        remoteEndPoint);
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

    private static bool ProcessConnect(
        NetworkServer server,
        IServerSnapshotTransport snapshotFlushTransport,
        Action<ClientId, IPEndPoint> registerEndpoint,
        BinaryReader reader,
        IPEndPoint remoteEndPoint)
    {
        var clientId = new ClientId(reader.ReadInt32());
        if (!server.ConnectClient(clientId))
        {
            return false;
        }

        registerEndpoint(clientId, remoteEndPoint);
        server.FlushSnapshots(snapshotFlushTransport);
        return true;
    }

    private static bool ProcessController(
        NetworkServer server,
        TryLookupClientEndpoint tryLookupRegisteredEndpoint,
        BinaryReader reader,
        IPEndPoint remoteEndPoint)
    {
        int payloadLength = reader.ReadInt32();
        if (payloadLength < 0)
        {
            return false;
        }

        var command = ClientCommandSerializer.Deserialize(reader.ReadBytes(payloadLength));
        if (!tryLookupRegisteredEndpoint(command.ClientId, out var registeredEndpoint)
            || !registeredEndpoint.Equals(remoteEndPoint))
        {
            return false;
        }

        return server.ReceiveCommand(command);
    }

    private static bool ProcessAttack(
        NetworkServer server,
        TryLookupClientEndpoint tryLookupRegisteredEndpoint,
        BinaryReader reader,
        IPEndPoint remoteEndPoint)
    {
        int payloadLength = reader.ReadInt32();
        if (payloadLength < 0)
        {
            return false;
        }

        var command = AttackCommandSerializer.Deserialize(reader.ReadBytes(payloadLength));
        if (!tryLookupRegisteredEndpoint(command.ClientId, out var registeredEndpoint)
            || !registeredEndpoint.Equals(remoteEndPoint))
        {
            return false;
        }

        return server.ReceiveAttackCommand(command);
    }

    private static bool ProcessDisconnect(
        NetworkServer server,
        Action<ClientId> unregisterEndpoint,
        BinaryReader reader)
    {
        var clientId = new ClientId(reader.ReadInt32());
        unregisterEndpoint(clientId);
        server.DisconnectClient(clientId);
        return true;
    }
}
