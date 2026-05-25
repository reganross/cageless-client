using System;

public readonly struct Tick : IEquatable<Tick>
{
    public int Value { get; }

    public Tick(int value)
    {
        Value = value;
    }

    public static Tick operator +(Tick a, Tick b) => new Tick(a.Value + b.Value);
    public static Tick operator -(Tick a, Tick b) => new Tick(a.Value - b.Value);
    public static Tick operator ++(Tick tick) => new Tick(tick.Value + 1);
    public static Tick operator --(Tick tick) => new Tick(tick.Value - 1);

    public static bool operator >(Tick a, Tick b) => a.Value > b.Value;
    public static bool operator <(Tick a, Tick b) => a.Value < b.Value;
    public static bool operator >=(Tick a, Tick b) => a.Value >= b.Value;
    public static bool operator <=(Tick a, Tick b) => a.Value <= b.Value;
    public static bool operator ==(Tick a, Tick b) => a.Equals(b);
    public static bool operator !=(Tick a, Tick b) => !a.Equals(b);

    public static implicit operator Tick(int value) => new Tick(value);
    public static implicit operator int(Tick tick) => tick.Value;

    public bool Equals(Tick other) => Value == other.Value;
    public override bool Equals(object obj) => obj is Tick other && Equals(other);
    public override int GetHashCode() => Value;
    public override string ToString() => $"{Value} ticks";
}