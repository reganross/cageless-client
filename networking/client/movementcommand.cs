using Godot;

public readonly struct MovementCommand
{
    public MovementCommand(Vector2 moveDirection, bool jumpPressed)
    {
        MoveDirection = moveDirection;
        JumpPressed = jumpPressed;
    }

    public Vector2 MoveDirection { get; }
    public bool JumpPressed { get; }
}
