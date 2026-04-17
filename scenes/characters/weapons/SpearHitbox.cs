using Godot;

public partial class SpearHitbox : Area3D
{
	private CharacterBody3D _wielder;

	public override void _Ready()
	{
		Monitoring = false;
		_wielder = FindWielder();
		BodyEntered += OnBodyEntered;
	}

	private CharacterBody3D FindWielder()
	{
		Node n = GetParent();
		while (n != null)
		{
			if (n is CharacterBody3D cb)
				return cb;
			n = n.GetParent();
		}
		return null;
	}

	private void OnBodyEntered(Node3D body)
	{
		if (body is not CharacterBody3D hit)
			return;

		if (_wielder != null && hit == _wielder)
			return;

		if (hit.IsInGroup("player"))
			GD.Print("Spear hit the player.");
		else if (hit.IsInGroup("enemy")
			&& _wielder != null
			&& _wielder.IsInGroup("player")
			&& hit.HasMethod("Die"))
			hit.Call("Die");
	}
}
