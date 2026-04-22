using System.Collections.Generic;

public struct SnapshotFrame
{
    public long Tick;
    public Dictionary<int, EntityState> States;
}