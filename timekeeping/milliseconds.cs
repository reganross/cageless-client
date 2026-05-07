public readonly struct Milliseconds
{
    public float Value { get; }

    public Milliseconds(float value)
    {
        Value = value;
    }

    public override string ToString() => $"{Value} ms";
}