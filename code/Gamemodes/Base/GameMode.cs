
public class GameMode : Component
{
	/// <summary> The current state of the game, should never be null </summary>
	[Property, ReadOnly, Sync] public GameSystem GameSystem { get; set; }
}

[GameResource( "Game Mode", "mode", "A game mode that can be selected by the player", Icon = "gamepad" )]
public sealed class GameModeResource : GameResource
{
	public string Title { get; set; }
	[TextArea] public string Description { get; set; }
	public GameObject Prefab { get; set; }
	public bool Hidden { get; set; } = false;
}
