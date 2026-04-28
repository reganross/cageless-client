# Multiplayer Prototype Todo

Goal for tomorrow: get a working multiplayer prototype where a host starts an authoritative server, a client joins, the server creates a player in its local simulation, and snapshots cause clients to spawn and update remote scene nodes.

## Server Simulation
- When a client connects, create and register a player entity in the authoritative server simulation for that client.
- When a client disconnects, remove or mark that client's authoritative player entity for despawn and replication cleanup.
- Bind each connected client's `PlayerControllerManager` controller to its authoritative server-side player entity.
- Ensure server-created player entities appear in snapshots with enough identity/type data for clients to spawn remote player nodes.

## Snapshot Identity
- Define how snapshot entity state maps to Godot scene nodes.
- Add enough entity identity/type information to snapshots or registration so clients know what scene to instantiate for each entity.
- Decide how despawns are represented when an entity disappears from authoritative server snapshots.

## Client Scene Replication
- Create a client-side scene replication registry that tracks network entity ids to spawned Godot nodes.
- When a joined client receives a full snapshot, spawn missing remote nodes in the combat scene from configured `PackedScene`s.
- Apply incoming full and delta snapshot data to existing remote nodes so position, rotation, velocity, and state flags update from the server.
- Remove or despawn client-side nodes that the authoritative server says no longer exist.
- Ensure remote player nodes use server-updated controllers or snapshot state rather than local input.

## Combat Integration
- Wire the combat scene to process `NetworkSession` client snapshots each physics tick.
- Route received snapshots through the replication system.
- Keep the local joined player using local input while remote players are driven by replicated server state.

## Verification
- Add tests for client snapshot application: spawn missing nodes, update existing nodes, and ignore malformed or unknown entity data safely.
- Add tests for server-side player creation on connect and cleanup on disconnect.
- Run a local host-and-join smoke test to verify a joining client creates and updates scene nodes from server snapshots.
