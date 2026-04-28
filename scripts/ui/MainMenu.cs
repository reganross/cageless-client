using Godot;

public partial class MainMenu : Control
{
	private const string CombatScenePath = "res://scenes/combat.tscn";
	private const double JoinTimeoutSeconds = 2.0;
	private const double JoinPollSeconds = 0.05;

	private LineEdit _hostInput;
	private SpinBox _portInput;
	private Button _joinButton;
	private Label _statusLabel;

	public override void _Ready()
	{
		_hostInput = GetNode<LineEdit>("Center/VBox/JoinPanel/HostInput");
		_portInput = GetNode<SpinBox>("Center/VBox/JoinPanel/PortInput");
		_joinButton = GetNode<Button>("Center/VBox/JoinPanel/JoinButton");
		_statusLabel = GetNode<Label>("Center/VBox/StatusLabel");
	}

	public void _on_start_pressed()
	{
		NetworkSession.StartSinglePlayer();
		LoadCombatScene();
	}

	public void _on_start_multiplayer_pressed()
	{
		NetworkSession.StartHost(NetworkSession.DefaultPort);
		LoadCombatScene();
	}

	public async void _on_join_pressed()
	{
		string host = string.IsNullOrWhiteSpace(_hostInput.Text)
			? NetworkSession.DefaultHost
			: _hostInput.Text.Trim();
		int port = (int)_portInput.Value;

		_joinButton.Disabled = true;
		ShowStatus($"Connecting to {host}:{port}...");

		try
		{
			NetworkSession.StartClient(host, port);
			if (await WaitForServerResponse())
			{
				LoadCombatScene();
				return;
			}

			NetworkSession.Reset();
			ShowStatus("Failed to connect: no server response.");
		}
		catch (System.Exception ex)
		{
			NetworkSession.Reset();
			ShowStatus($"Failed to connect: {ex.Message}");
		}
		finally
		{
			_joinButton.Disabled = false;
		}
	}

	public void _on_exit_pressed()
	{
		GetTree().Quit();
	}

	private void LoadCombatScene()
	{
		GetTree().ChangeSceneToFile(CombatScenePath);
	}

	private async System.Threading.Tasks.Task<bool> WaitForServerResponse()
	{
		double elapsed = 0;
		while (elapsed < JoinTimeoutSeconds)
		{
			NetworkSession.Client?.ReceiveSnapshots();
			if (NetworkSession.Client != null
				&& NetworkSession.Client.TryGetLatestSnapshot(out _))
			{
				return true;
			}

			await ToSignal(GetTree().CreateTimer(JoinPollSeconds), SceneTreeTimer.SignalName.Timeout);
			elapsed += JoinPollSeconds;
		}

		return false;
	}

	private void ShowStatus(string message)
	{
		_statusLabel.Text = message;
	}
}
