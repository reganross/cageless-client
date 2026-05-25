using Xunit;

public class SceneNodeSpawnPolicyTests
{
    /*
     PURPOSE:
     Ensure non-entity scene nodes are not blocked by multiplayer authority rules.

     DESIGN RULE:
     - Static nodes can be created by local scene setup
     - Static nodes do not require server authority or snapshot data

     FAILURE MEANS:
     - Static scene props may disappear in multiplayer scenes
     - Scene setup may become coupled to network authority unnecessarily
    */
    [Theory]
    [InlineData(NetworkSessionMode.Disconnected)]
    [InlineData(NetworkSessionMode.Host)]
    [InlineData(NetworkSessionMode.Client)]
    public void CanSpawn_ShouldAllowStaticNodesInEveryMode(NetworkSessionMode mode)
    {
        Assert.True(SceneNodeSpawnPolicy.CanSpawn(
            SceneNodeSpawnKind.Static,
            SceneNodeSpawnSource.LocalScene,
            mode));
    }

    /*
     PURPOSE:
     Ensure host-mode sessions can create gameplay entities locally.

     DESIGN RULE:
     - Host owns authoritative simulation (including offline loopback host)
     - Local scene scripts may create gameplay entities only when hosting

     FAILURE MEANS:
     - Host combat may stop spawning players or enemies
     - Offline play may incorrectly run without server authority
    */
    [Fact]
    public void CanSpawn_ShouldAllowLocalEntityWhenHost()
    {
        Assert.True(SceneNodeSpawnPolicy.CanSpawn(
            SceneNodeSpawnKind.Entity,
            SceneNodeSpawnSource.LocalScene,
            NetworkSessionMode.Host));
    }

    [Theory]
    [InlineData(SceneNodeSpawnSource.LocalScene)]
    [InlineData(SceneNodeSpawnSource.ServerSimulation)]
    public void CanSpawn_ShouldRejectDisconnectedLocalEntitySpawns(SceneNodeSpawnSource source)
    {
        Assert.False(SceneNodeSpawnPolicy.CanSpawn(
            SceneNodeSpawnKind.Entity,
            source,
            NetworkSessionMode.Disconnected));
    }

    /*
     PURPOSE:
     Ensure host/server scenes can create authoritative gameplay entities.

     DESIGN RULE:
     - The server is allowed to create gameplay entities
     - Server-created entities should later replicate through snapshots

     FAILURE MEANS:
     - Host combat cannot spawn authoritative players or enemies
     - Server simulation may not be able to populate the scene
    */
    [Theory]
    [InlineData(SceneNodeSpawnSource.LocalScene)]
    [InlineData(SceneNodeSpawnSource.ServerSimulation)]
    public void CanSpawn_ShouldAllowEntityWhenHostIsServer(SceneNodeSpawnSource source)
    {
        Assert.True(SceneNodeSpawnPolicy.CanSpawn(
            SceneNodeSpawnKind.Entity,
            source,
            NetworkSessionMode.Host));
    }

    /*
     PURPOSE:
     Ensure clients cannot create their own gameplay entities from local scripts.

     DESIGN RULE:
     - Multiplayer clients receive gameplay entities from the server
     - Local client scene scripts must not create authoritative entities

     FAILURE MEANS:
     - Each client may spawn independent enemy waves
     - Client-owned scene entities may diverge from server authority
    */
    [Theory]
    [InlineData(SceneNodeSpawnSource.LocalScene)]
    [InlineData(SceneNodeSpawnSource.ServerSimulation)]
    public void CanSpawn_ShouldRejectClientLocalEntitySpawns(SceneNodeSpawnSource source)
    {
        Assert.False(SceneNodeSpawnPolicy.CanSpawn(
            SceneNodeSpawnKind.Entity,
            source,
            NetworkSessionMode.Client));
    }

    /*
     PURPOSE:
     Ensure replicated server entities can be instantiated on any client scene.

     DESIGN RULE:
     - Server snapshots are authoritative spawn instructions
     - Clients and hosts can instantiate entities that arrive from snapshots

     FAILURE MEANS:
     - Remote players or enemies may never appear
     - Snapshot replication may be unable to populate scenes
    */
    [Theory]
    [InlineData(NetworkSessionMode.Disconnected)]
    [InlineData(NetworkSessionMode.Host)]
    [InlineData(NetworkSessionMode.Client)]
    public void CanSpawn_ShouldAllowEntityFromServerSnapshot(NetworkSessionMode mode)
    {
        Assert.True(SceneNodeSpawnPolicy.CanSpawn(
            SceneNodeSpawnKind.Entity,
            SceneNodeSpawnSource.ServerSnapshot,
            mode));
    }
}
