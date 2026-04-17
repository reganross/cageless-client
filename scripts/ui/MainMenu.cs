using Godot;

public partial class MainMenu : Control
{
	public void _on_connect_pressed()
	{
		GD.Print("Connect to server…");
	}

	public void _on_exit_pressed()
	{
		GetTree().Quit();
	}
}
