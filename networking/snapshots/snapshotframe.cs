using System.Collections.Generic;

public struct SnapshotFrame
{
    public Tick Tick;
    public Dictionary<Tick, EntityState> States;
}