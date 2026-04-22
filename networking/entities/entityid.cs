public struct EntityId
{
    public int Value;

    public EntityId(int value)
    {
        Value = value;
    }

    public override int GetHashCode() => Value;
    public override bool Equals(object obj)
        => obj is EntityId other && other.Value == Value;
}