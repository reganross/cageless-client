public interface INetworkEntity
{
    EntityId Id { get; }

    EntityState CaptureState();
}