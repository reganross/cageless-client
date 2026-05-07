using Godot;

public struct EntityState
{
    public int TypeId;
    public int OwnerId;

    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Velocity;

    public string AnimationName;
    public double AnimationTime;
    public bool IsAnimationPlaying;

    public int StateFlags;
}