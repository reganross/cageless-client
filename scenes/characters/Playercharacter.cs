using Godot;
using System;

public partial class Playercharacter : CharacterBody3D
{
	[Export]
	public float Speed = 5.0f;

	public const float JumpVelocity = 4.5f;

	[Export]
	public float MouseSensitivity = 0.002f;

	/// <summary>How fast the body can turn horizontally (deg/s). Camera yaw follows the mouse immediately via a pivot offset.</summary>
	[Export]
	public float MaxBodyYawDegreesPerSecond = 90f;

	/// <summary>Weapon prefab instantiated as a child of the socket when the scene runs.</summary>
	[Export]
	public PackedScene WeaponScene { get; set; }

	[Export]
	public StringName ThrustAnimation { get; set; } = "Thrust";

	private AnimationPlayer _animationPlayer;
	private Node3D _cameraPivot;
	private PlayerController _controller = new();
	private NetworkTickClock.Advancer _tickClockAdvancer;
	private bool _usesLocalInput = true;
	private float _pitch;
	/// <summary>Extra yaw on <see cref="_cameraPivot"/> so look direction can lead the body.</summary>
	private float _yawOffset;

	public override void _Ready()
	{
		_cameraPivot = GetNode<Node3D>("CameraPivot");
		_animationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
		Input.MouseMode = Input.MouseModeEnum.Captured;

		if (WeaponScene == null)
			return;

		var socket = FindChild("WeaponPivot", recursive: true, owned: false) as Node3D;
		if (socket == null)
		{
			GD.PushWarning($"{nameof(Playercharacter)}: WeaponScene set but no child Node3D named \"WeaponSocket\" under this player.");
			return;
		}

		var weapon = WeaponScene.Instantiate<Node>();
		socket.AddChild(weapon);
	}

	public override void _Input(InputEvent e)
	{
		if (e.IsActionPressed("ui_cancel"))
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
			GetViewport().SetInputAsHandled();
			return;
		}

		if (Input.MouseMode == Input.MouseModeEnum.Visible
			&& e is InputEventMouseButton recaptureClick
			&& recaptureClick.Pressed
			&& recaptureClick.ButtonIndex == MouseButton.Left)
		{
			Input.MouseMode = Input.MouseModeEnum.Captured;
			GetViewport().SetInputAsHandled();
			return;
		}

		if (Input.MouseMode != Input.MouseModeEnum.Captured)
			return;

		if (e is InputEventMouseButton attackClick && attackClick.Pressed && attackClick.ButtonIndex == MouseButton.Left)
		{
			if (TryPlaySpearThrust())
				GetViewport().SetInputAsHandled();
			return;
		}

		if (e is InputEventMouseMotion motion)
		{
			_yawOffset -= motion.Relative.X * MouseSensitivity;

			_pitch -= motion.Relative.Y * MouseSensitivity;
			_pitch = Mathf.Clamp(_pitch, -1.2f, 1.2f);
			_controller.SetLookRotation(Rotation.Y + _yawOffset, _pitch);
			_cameraPivot.Rotation = new Vector3(_controller.LookPitch, _yawOffset, 0);
		}
	}

	public override void _Process(double delta)
	{
		if (_usesLocalInput)
			UpdateControllerFromInputMap();
	}

	public PlayerController Controller => _controller;
	public NetworkTickClock.Advancer TickClockAdvancer => _tickClockAdvancer;

	public void UseController(PlayerController controller)
	{
		_controller = controller ?? throw new ArgumentNullException(nameof(controller));
		_usesLocalInput = !controller.HasPlayerId;
		_pitch = controller.LookPitch;
		_yawOffset = Mathf.AngleDifference(Rotation.Y, controller.LookYaw);
	}

	public void UseTickClockAdvancer(NetworkTickClock.Advancer tickClockAdvancer)
	{
		_tickClockAdvancer = tickClockAdvancer ?? throw new ArgumentNullException(nameof(tickClockAdvancer));
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_usesLocalInput)
			_tickClockAdvancer?.Advance(delta);

		float dt = (float)delta;
		TurnTowardControllerLook(dt);

		Vector3 velocity = Velocity;

		if (!IsOnFloor())
			velocity += GetGravity() * (float)delta;

		if (_controller.GetActionStrength("ui_accept") > 0f && IsOnFloor())
			velocity.Y = JumpVelocity;

		Vector2 inputDir = _controller.GetMoveDirection();
		Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
		if (direction != Vector3.Zero)
		{
			velocity.X = direction.X * Speed;
			velocity.Z = direction.Z * Speed;
		}
		else
		{
			velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed);
			velocity.Z = Mathf.MoveToward(Velocity.Z, 0, Speed);
		}

		Velocity = velocity;
		MoveAndSlide();
	}

	private void UpdateControllerFromInputMap()
	{
		_controller.SetActionStrength("left", Input.GetActionStrength("left"));
		_controller.SetActionStrength("right", Input.GetActionStrength("right"));
		_controller.SetActionStrength("forward", Input.GetActionStrength("forward"));
		_controller.SetActionStrength("back", Input.GetActionStrength("back"));
		_controller.SetActionStrength("ui_accept", Input.GetActionStrength("ui_accept"));
		_controller.SetLookRotation(Rotation.Y + _yawOffset, _pitch);
	}

	private void TurnTowardControllerLook(float delta)
	{
		float yawDelta = Mathf.AngleDifference(Rotation.Y, _controller.LookYaw);
		float maxYawRad = Mathf.DegToRad(MaxBodyYawDegreesPerSecond) * delta;
		float step = Mathf.Clamp(yawDelta, -maxYawRad, maxYawRad);

		if (Mathf.Abs(step) > 0f)
		{
			RotateY(step);
		}

		_yawOffset = Mathf.AngleDifference(Rotation.Y, _controller.LookYaw);
		_cameraPivot.Rotation = new Vector3(_controller.LookPitch, _yawOffset, 0);
	}

	private bool TryPlaySpearThrust()
	{
		if (_animationPlayer == null || WeaponScene == null)
			return false;

		if (!WeaponScene.ResourcePath.Contains("spear", StringComparison.OrdinalIgnoreCase))
			return false;

		if (!_animationPlayer.HasAnimation(ThrustAnimation))
			return false;

		_animationPlayer.Play(ThrustAnimation);
		return true;
	}
}
