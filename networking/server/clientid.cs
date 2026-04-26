public readonly struct ClientId
{
    public readonly int Value;

    public ClientId(int value)
    {
        Value = value;
    }

    public override int GetHashCode() => Value;

    public override bool Equals(object obj)
        => obj is ClientId other && other.Value == Value;
}
