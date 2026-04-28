using Godot;
using System;

public partial class CombatWaveManager : Node3D
{
	[Export]
	public PackedScene PlayerScene { get; set; }

	[Export]
	public PackedScene PlayerWeaponScene { get; set; }

	[Export]
	public Vector3 PlayerSpawnPosition { get; set; } = new(0, 0.25f, 0);

	[Export]
	public PackedScene SpearmanScene { get; set; }

	/// <summary>Spearmen in wave 1.</summary>
	[Export]
	public int StartingCount { get; set; } = 1;

	/// <summary>Added for each wave after the first: count = StartingCount + (wave - 1) * IncreasePerWave.</summary>
	[Export]
	public int IncreasePerWave { get; set; } = 1;

	[Export]
	public float SpawnRadius { get; set; } = 18f;

	[Export]
	public float DelayBetweenWavesSeconds { get; set; } = 0.5f;

	private int _waveIndex;
	private int _remainingInWave;
	private Node3D _enemyWaves;
	private Node3D _replicatedEntities;
	private ClientSceneReplicationRegistry _replicationRegistry;
	private ClientSnapshotTruthLayer _truthLayer;
	private SceneNodeSpawner _sceneNodeSpawner;
	private Playercharacter _localPlayer;
	private bool _localPlayerHasTruthLayer;
	private double _snapshotLogSeconds;

	public override void _Ready()
	{
		_enemyWaves = GetNode<Node3D>("EnemyWaves");
		_sceneNodeSpawner = new SceneNodeSpawner();
		_replicatedEntities = new Node3D
		{
			Name = "ReplicatedEntities"
		};
		AddChild(_replicatedEntities);

		if (NetworkSession.TickClock == null)
			NetworkSession.StartSinglePlayer();

		ConfigureReplication();
		SpawnPlayer();

		if (SpearmanScene == null)
		{
			GD.PushError($"{nameof(CombatWaveManager)}: SpearmanScene is not set.");
			return;
		}

		if (SceneNodeSpawnPolicy.CanSpawn(
			SceneNodeSpawnKind.Entity,
			SceneNodeSpawnSource.LocalScene,
			NetworkSession.Mode))
		{
			StartNextWave();
		}
	}

	private void SpawnPlayer()
	{
		if (PlayerScene == null)
		{
			GD.PushError($"{nameof(CombatWaveManager)}: PlayerScene is not set.");
			return;
		}

		_localPlayer = PlayerScene.Instantiate<Playercharacter>();
		_localPlayer.WeaponScene = PlayerWeaponScene;
		_localPlayer.UseTickClockAdvancer(NetworkSession.TickClockAdvancer);

		if (NetworkSession.Client != null)
			_localPlayer.UseController(NetworkSession.Client.Controller, usesLocalInput: true);

		AddChild(_localPlayer);
		_localPlayer.GlobalPosition = PlayerSpawnPosition;
	}

	public override void _PhysicsProcess(double delta)
	{
		NetworkSession.Tick(delta);
		ApplyReceivedSnapshots();
		PrintSnapshotEntitiesOncePerSecond(delta);
	}

	public override void _ExitTree()
	{
		if (NetworkSession.Mode == NetworkSessionMode.SinglePlayer)
			NetworkSession.Reset();
	}

	private void StartNextWave()
	{
		_waveIndex++;
		int count = StartingCount + (_waveIndex - 1) * IncreasePerWave;
		_remainingInWave = count;

		for (int i = 0; i < count; i++)
		{
			var transform = Transform3D.Identity;
			transform.Origin = SpawnPositionAroundPlayer(i, count);
			if (!_sceneNodeSpawner.TryAddNode(
				SpearmanScene,
				_enemyWaves,
				transform,
				SceneNodeSpawnKind.Entity,
				SceneNodeSpawnSource.LocalScene,
				out Spearman spearman))
			{
				continue;
			}

			spearman.Defeated += OnSpearmanDefeated;
			RegisterServerEntity(spearman);
		}

		GD.Print($"Wave {_waveIndex}: spawned {count} spearman.");
	}

	private Vector3 SpawnPositionAroundPlayer(int index, int total)
	{
		if (total <= 0)
			return Vector3.Zero;

		Vector3 center = Vector3.Zero;
		var player = GetTree().GetFirstNodeInGroup("player") as Node3D;
		if (player != null)
			center = player.GlobalPosition;

		float angle = Mathf.Tau * index / total;
		return new Vector3(
			center.X + Mathf.Cos(angle) * SpawnRadius,
			center.Y,
			center.Z + Mathf.Sin(angle) * SpawnRadius);
	}

	private void OnSpearmanDefeated()
	{
		_remainingInWave--;
		if (_remainingInWave > 0)
			return;

		if (DelayBetweenWavesSeconds > 0f)
		{
			SceneTreeTimer t = GetTree().CreateTimer(DelayBetweenWavesSeconds);
			t.Timeout += StartNextWave;
		}
		else
			StartNextWave();
	}

	private static void RegisterServerEntity(Spearman spearman)
	{
		if (NetworkSession.ServerHost == null)
		{
			GD.Print("Spearman spawned locally without server registration.");
			return;
		}

		var entityId = NetworkSession.ServerHost.Server.RegisterEntity(spearman);
		spearman.AssignEntityId(entityId);
		GD.Print($"Registered spearman entity {entityId.Value} with server snapshots.");
	}

	private void ConfigureReplication()
	{
		int localOwnerId = NetworkSession.Client?.ClientId.Value ?? 0;
		_replicationRegistry = new ClientSceneReplicationRegistry(localOwnerId);
		if (NetworkSession.Client != null)
			_truthLayer = new ClientSnapshotTruthLayer(NetworkSession.Client);

		_replicationRegistry.RegisterFactory(
			NetworkEntityType.Player,
			(entityId, state) => GodotSceneReplica.Spawn(
				PlayerScene,
				_replicatedEntities,
				entityId,
				state,
				_truthLayer,
				_sceneNodeSpawner,
				PlayerWeaponScene));
		_replicationRegistry.RegisterFactory(
			NetworkEntityType.Spearman,
			(entityId, state) => GodotSceneReplica.Spawn(
				SpearmanScene,
				_replicatedEntities,
				entityId,
				state,
				_truthLayer,
				_sceneNodeSpawner));
	}

	private void ApplyReceivedSnapshots()
	{
		while (NetworkSession.Client != null
			&& NetworkSession.Client.TryDequeueSnapshot(out var snapshot))
		{
			_replicationRegistry.ApplySnapshot(snapshot);
		}

		if (NetworkSession.Client != null)
		{
			_replicationRegistry.ReconcileEntities(NetworkSession.Client.LatestEntityStates);
		}

		TryAssignLocalPlayerTruthLayer();
	}

	private void TryAssignLocalPlayerTruthLayer()
	{
		if (_localPlayerHasTruthLayer
			|| _localPlayer == null
			|| _truthLayer == null
			|| NetworkSession.Client == null)
		{
			return;
		}

		if (!NetworkSession.Client.TryGetLatestEntityIdForOwner(
			NetworkSession.Client.ClientId,
			NetworkEntityType.Player,
			out int entityId))
		{
			return;
		}

		_localPlayer.UseTruthLayer(entityId, _truthLayer);
		_localPlayerHasTruthLayer = true;
	}

	private void PrintSnapshotEntitiesOncePerSecond(double delta)
	{
		_snapshotLogSeconds += delta;
		if (_snapshotLogSeconds < 1.0)
			return;

		_snapshotLogSeconds = 0;

		if (NetworkSession.ServerHost != null)
		{
			PrintSnapshotEntities("server", NetworkSession.ServerHost.Server.GetLatestSnapshot());
		}

		if (NetworkSession.Client != null)
		{
			PrintEntityStates(
				"client cache",
				NetworkSession.Client.LatestEntityStates);
		}
	}

	private static void PrintSnapshotEntities(string source, SnapshotFrame snapshot)
	{
		if (snapshot.States == null)
		{
			GD.Print($"Snapshot {source}: no snapshot yet.");
			return;
		}

		GD.Print($"Snapshot {source}: tick={snapshot.Tick}, entities={snapshot.States.Count}");
		foreach (var kv in snapshot.States)
		{
			var state = kv.Value;
			GD.Print(
				$"  entity={kv.Key} type={(NetworkEntityType)state.TypeId} owner={state.OwnerId} position={state.Position}");
		}
	}

	private static void PrintEntityStates(
		string source,
		System.Collections.Generic.IReadOnlyDictionary<int, EntityState> states)
	{
		GD.Print($"Snapshot {source}: entities={states.Count}");
		foreach (var kv in states)
		{
			var state = kv.Value;
			GD.Print(
				$"  entity={kv.Key} type={(NetworkEntityType)state.TypeId} owner={state.OwnerId} position={state.Position}");
		}
	}
}
