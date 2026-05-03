using System;
using System.Collections.Generic;

public interface IServerSnapshotTransport : IDisposable
{
    IReadOnlyCollection<ClientId> ConnectedClients { get; }

    void Start(int port);

    void SendSnapshot(ClientId clientId, SnapshotPacket packet);
}
