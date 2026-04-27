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

	public override void _Ready()
	{
		_enemyWaves = GetNode<Node3D>("EnemyWaves");
		SpawnPlayer();

		if (SpearmanScene == null)
		{
			GD.PushError($"{nameof(CombatWaveManager)}: SpearmanScene is not set.");
			return;
		}

		StartNextWave();
	}

	private void SpawnPlayer()
	{
		if (PlayerScene == null)
		{
			GD.PushError($"{nameof(CombatWaveManager)}: PlayerScene is not set.");
			return;
		}

		var player = PlayerScene.Instantiate<Playercharacter>();
		player.WeaponScene = PlayerWeaponScene;
		AddChild(player);
		player.GlobalPosition = PlayerSpawnPosition;
	}

	private void StartNextWave()
	{
		_waveIndex++;
		int count = StartingCount + (_waveIndex - 1) * IncreasePerWave;
		_remainingInWave = count;

		for (int i = 0; i < count; i++)
		{
			var spearman = SpearmanScene.Instantiate<Spearman>();
			_enemyWaves.AddChild(spearman);
			spearman.GlobalPosition = SpawnPositionAroundPlayer(i, count);
			spearman.Defeated += OnSpearmanDefeated;
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
}
