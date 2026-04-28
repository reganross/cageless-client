using System;

public class NetworkTickClock
{
    public const double DefaultTickIntervalSeconds = 0.05;

    private readonly double tickIntervalSeconds;
    private double accumulatedSeconds;
    private bool hasAdvancer;

    public NetworkTickClock(double tickIntervalSeconds = DefaultTickIntervalSeconds)
    {
        if (tickIntervalSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tickIntervalSeconds));
        }

        this.tickIntervalSeconds = tickIntervalSeconds;
    }

    public int CurrentTick { get; private set; }
    public int PendingTicks { get; private set; }

    public Advancer CreateAdvancer()
    {
        if (hasAdvancer)
        {
            throw new InvalidOperationException("Network tick clock already has an advancer.");
        }

        hasAdvancer = true;
        return new Advancer(this);
    }

    public bool TryRequestTick(out int tick)
    {
        if (PendingTicks == 0)
        {
            tick = 0;
            return false;
        }

        tick = CurrentTick - PendingTicks + 1;
        PendingTicks--;
        return true;
    }

    public void Reset()
    {
        accumulatedSeconds = 0;
        CurrentTick = 0;
        PendingTicks = 0;
    }

    private int Advance(double deltaSeconds)
    {
        if (deltaSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        }

        int completed = 0;
        accumulatedSeconds += deltaSeconds;

        while (accumulatedSeconds + 0.000000001 >= tickIntervalSeconds)
        {
            accumulatedSeconds -= tickIntervalSeconds;
            CurrentTick++;
            PendingTicks++;
            completed++;
        }

        return completed;
    }

    public sealed class Advancer : IDisposable
    {
        private readonly NetworkTickClock clock;
        private bool disposed;

        internal Advancer(NetworkTickClock clock)
        {
            this.clock = clock;
        }

        public int CurrentTick
        {
            get
            {
                ThrowIfDisposed();
                return clock.CurrentTick;
            }
        }

        public int Advance(double deltaSeconds)
        {
            ThrowIfDisposed();
            return clock.Advance(deltaSeconds);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            clock.hasAdvancer = false;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(Advancer));
            }
        }
    }
}
