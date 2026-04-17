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
			_cameraPivot.Rotation = new Vector3(_pitch, _yawOffset, 0);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		float maxYawRad = Mathf.DegToRad(MaxBodyYawDegreesPerSecond) * dt;
		float absOffset = Mathf.Abs(_yawOffset);
		if (absOffset > 0f)
		{
			float step = Mathf.Sign(_yawOffset) * Mathf.Min(absOffset, maxYawRad);
			RotateY(step);
			_yawOffset -= step;
			_cameraPivot.Rotation = new Vector3(_pitch, _yawOffset, 0);
		}

		Vector3 velocity = Velocity;

		if (!IsOnFloor())
			velocity += GetGravity() * (float)delta;

		if (Input.IsActionJustPressed("ui_accept") && IsOnFloor())
			velocity.Y = JumpVelocity;

		Vector2 inputDir = Input.GetVector("left", "right", "forward", "back");
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
