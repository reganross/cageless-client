using Godot;

public sealed class ServerPlayerEntity : INetworkEntity
{
    private const float MoveSpeed = 5f;
    private const string PlayerScenePath = "res://scenes/characters/playercharacter.tscn";
    private static GodotCollisionRigSampler collisionRigSampler;

    private EntityId id;
    private Vector3 position;
    private Vector3 velocity;
    private Quaternion rotation = Quaternion.Identity;

    public ServerPlayerEntity(ClientId ownerId, Vector3 initialPosition = default)
    {
        OwnerId = ownerId;
        position = initialPosition;
    }

    /// <summary>
    /// Snap authoritative physics pose (e.g. scene spawn); clears residual velocity.
    /// </summary>
    public void ResetToSpawn(Vector3 worldPosition)
    {
        position = worldPosition;
        velocity = Vector3.Zero;
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

    public CollisionRigSnapshot GetCollisionRig(EntityState state)
    {
        collisionRigSampler ??= GodotCollisionRigSampler.FromScenePath(PlayerScenePath);
        return collisionRigSampler.Sample(state);
    }
}
