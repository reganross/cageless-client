public interface IServerNetworkTransport : IServerSnapshotTransport
{
    int ProcessIncoming(NetworkServer server);
}
