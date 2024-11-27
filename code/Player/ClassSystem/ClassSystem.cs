using Sandbox;


[GameResource( "Player Class", "class", "A weapon class for a player", Icon = "person")]
public sealed class PlayerClass : GameResource
{
	public WeaponData WeaponData { get; set; }
	public string ClassName { get; set; }
	[TextArea] public string ClassDescription { get; set; }
	public int WalkSpeed { get; set; } = 300;
	public int RunSpeed { get; set; } = 450;
	public int Health { get; set; } = 100;
	public bool Hidden { get; set; } = false;

	[Property, ToggleGroup( "SecondaryEnabled" )] public bool SecondaryEnabled { get; set; } = false;
	[Property, Group( "SecondaryEnabled" )] public WeaponData SecondaryWeaponData { get; set; }
}
