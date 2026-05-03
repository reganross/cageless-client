public class NetworkTickRatePolicy
{
    public NetworkTickRatePolicy(
        double inCombatIntervalSeconds = 0.05,
        double exploringIntervalSeconds = 0.1,
        double idleIntervalSeconds = 0.5)
    {
        InCombatIntervalSeconds = inCombatIntervalSeconds;
        ExploringIntervalSeconds = exploringIntervalSeconds;
        IdleIntervalSeconds = idleIntervalSeconds;
    }

    public double InCombatIntervalSeconds { get; }
    public double ExploringIntervalSeconds { get; }
    public double IdleIntervalSeconds { get; }

    public double GetInterval(NetworkActivityState state)
    {
        return state switch
        {
            NetworkActivityState.InCombat => InCombatIntervalSeconds,
            NetworkActivityState.Exploring => ExploringIntervalSeconds,
            NetworkActivityState.Idle => IdleIntervalSeconds,
            _ => ExploringIntervalSeconds
        };
    }
}
