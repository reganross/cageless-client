using Godot;
using System;

public partial class Spearman : CharacterBody3D, INetworkEntity
{
	[Signal]
	public delegate void DefeatedEventHandler();

	[Export]
	public float AggroRange = 12f;

	[Export]
	public float MeleeRange = 1f;

	[Export]
	public float MoveSpeed = 3.5f;

	[Export]
	public StringName ThrustAnimation { get; set; } = "Thrust";

	[Export]
	public StringName DeathAnimation { get; set; } = "Death";

	[Export]
	public float RemoveAfterDeathSeconds { get; set; } = 2f;

	[Export]
	public float SnapshotPositionLerpSpeed { get; set; } = 12f;

	[Export]
	public float SnapshotRotationLerpSpeed { get; set; } = 12f;

	[Export]
	public float SnapshotFloorY { get; set; } = 0.25f;

	private AnimationPlayer _animationPlayer;
	private Node3D _player;
	private ClientSnapshotTruthLayer _truthLayer;
	private int _networkEntityId;
	private EntityId _entityId;
	private bool _aware;
	private bool _dead;

	public EntityId Id => _entityId;

	public override void _Ready()
	{
		_animationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
	}

	public override void _ExitTree()
	{
		if (_entityId.Value != 0 && NetworkSession.ServerHost != null)
		{
			NetworkSession.ServerHost.Server.DeregisterEntity(_entityId);
			_entityId = default;
		}
	}

	public void Die()
	{
		if (_dead)
			return;

		_dead = true;
		SetPhysicsProcess(false);

		if (_animationPlayer != null)
		{
			_animationPlayer.Stop();
			if (_animationPlayer.HasAnimation(DeathAnimation))
				_animationPlayer.Play(DeathAnimation);
		}

		var collision = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		if (collision != null)
			collision.Disabled = true;

		CollisionLayer = 0;
		CollisionMask = 0;

		EmitSignal(SignalName.Defeated);

		SceneTreeTimer timer = GetTree().CreateTimer(RemoveAfterDeathSeconds);
		timer.Timeout += OnDeathRemoveTimer;
	}

	private void OnDeathRemoveTimer()
	{
		QueueFree();
	}

	public void UseTruthLayer(int entityId, ClientSnapshotTruthLayer truthLayer)
	{
		_networkEntityId = entityId;
		_truthLayer = truthLayer ?? throw new ArgumentNullException(nameof(truthLayer));
	}

	public void AssignEntityId(EntityId entityId)
	{
		_entityId = entityId;
	}

	public EntityState CaptureState()
	{
		var snapshotPosition = GlobalPosition;
		if (snapshotPosition.Y < SnapshotFloorY)
		{
			snapshotPosition.Y = SnapshotFloorY;
		}

		return new EntityState
		{
			TypeId = (int)NetworkEntityType.Spearman,
			Position = snapshotPosition,
			Rotation = Quaternion.FromEuler(Rotation),
			Velocity = Velocity,
			StateFlags = _dead ? 1 : 0
		};
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_dead)
			return;

		_truthLayer?.TryApplyTruth(
			_networkEntityId,
			this,
			delta,
			SnapshotPositionLerpSpeed,
			SnapshotRotationLerpSpeed);
		if (_truthLayer != null)
			return;

		float dt = (float)delta;
		Vector3 velocity = Velocity;

		if (!IsOnFloor())
			velocity += GetGravity() * dt;

		if (_player == null || !IsInstanceValid(_player))
			_player = GetTree().GetFirstNodeInGroup("player") as Node3D;

		if (_player == null)
		{
			velocity.X = Mathf.MoveToward(velocity.X, 0, MoveSpeed);
			velocity.Z = Mathf.MoveToward(velocity.Z, 0, MoveSpeed);
			Velocity = velocity;
			MoveAndSlide();
			return;
		}

		float dist = HorizontalDistance(GlobalPosition, _player.GlobalPosition);

		if (!_aware && dist <= AggroRange)
			_aware = true;

		if (!_aware)
		{
			velocity.X = Mathf.MoveToward(velocity.X, 0, MoveSpeed);
			velocity.Z = Mathf.MoveToward(velocity.Z, 0, MoveSpeed);
			Velocity = velocity;
			MoveAndSlide();
			return;
		}

		if (dist > 0.05f)
		{
			var lookTarget = _player.GlobalPosition;
			lookTarget.Y = GlobalPosition.Y;
			LookAt(lookTarget, Vector3.Up);
		}

		if (dist <= MeleeRange)
		{
			velocity.X = Mathf.MoveToward(velocity.X, 0, MoveSpeed);
			velocity.Z = Mathf.MoveToward(velocity.Z, 0, MoveSpeed);

			if (_animationPlayer != null
				&& _animationPlayer.HasAnimation(ThrustAnimation)
				&& !_animationPlayer.IsPlaying())
				_animationPlayer.Play(ThrustAnimation);
		}
		else
		{
			Vector3 toPlayer = _player.GlobalPosition - GlobalPosition;
			toPlayer.Y = 0;
			if (toPlayer.LengthSquared() > 0.0001f)
			{
				toPlayer = toPlayer.Normalized();
				velocity.X = toPlayer.X * MoveSpeed;
				velocity.Z = toPlayer.Z * MoveSpeed;
			}
		}

		Velocity = velocity;
		MoveAndSlide();
	}

	private static float HorizontalDistance(Vector3 a, Vector3 b)
	{
		a.Y = 0;
		b.Y = 0;
		return a.DistanceTo(b);
	}
}
