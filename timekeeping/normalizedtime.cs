using Godot;

public readonly struct NormalizedTime
{
    public float Value { get; }

    public NormalizedTime(float value)
    {
        Value = Mathf.Clamp(value, 0f, 1f);
    }

    public override string ToString() => $"{Value:0.00}";
}