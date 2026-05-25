public interface INetworkEntity
{
    EntityId Id { get; }

    EntityState CaptureState();

    CollisionRigSnapshot GetCollisionRig(EntityState state) => default;
}