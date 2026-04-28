using Godot;

public sealed class ServerPlayerEntity : INetworkEntity
{
    private const float MoveSpeed = 5f;

    private EntityId id;
    private Vector3 position;
    private Vector3 velocity;
    private Quaternion rotation = Quaternion.Identity;

    public ServerPlayerEntity(ClientId ownerId)
    {
        OwnerId = ownerId;
    }

    public EntityId Id => id;
    public ClientId OwnerId { get; }

    public void AssignEntityId(EntityId entityId)
    {
        id = entityId;
    }

    public void Simulate(PlayerController controller)
    {
        if (controller == null)
        {
            velocity = Vector3.Zero;
            return;
        }

        rotation = Quaternion.FromEuler(new Vector3(0, controller.LookYaw, 0));

        var move = controller.GetMoveDirection();
        var localDirection = new Vector3(move.X, 0, move.Y);
        var worldDirection = new Basis(rotation) * localDirection;
        velocity = worldDirection.Normalized() * MoveSpeed;
        position += velocity * (float)NetworkTickClock.DefaultTickIntervalSeconds;
    }

    public EntityState CaptureState()
    {
        return new EntityState
        {
            TypeId = (int)NetworkEntityType.Player,
            OwnerId = OwnerId.Value,
            Position = position,
            Rotation = rotation,
            Velocity = velocity
        };
    }
}
