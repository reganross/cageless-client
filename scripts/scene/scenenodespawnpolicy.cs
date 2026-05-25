public static class SceneNodeSpawnPolicy
{
    public static bool CanSpawn(
        SceneNodeSpawnKind kind,
        SceneNodeSpawnSource source,
        NetworkSessionMode mode)
    {
        if (kind == SceneNodeSpawnKind.Static)
        {
            return true;
        }

        if (source == SceneNodeSpawnSource.ServerSnapshot)
        {
            return true;
        }

        return mode == NetworkSessionMode.Host;
    }
}
