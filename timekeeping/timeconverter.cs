using Godot;

public static class TimeConverter
{
    public static Tick ToTicks(Milliseconds ms, float tickRate)
    {
        float ticks = ms.Value / 1000f * tickRate;
        return new Tick((int)Mathf.Round(ticks));
    }

    public static Milliseconds ToMilliseconds(Tick tick, float tickRate)
    {
        float ms = (tick.Value / tickRate) * 1000f;
        return new Milliseconds(ms);
    }
}