
using Sandbox.Events;

public class GameMode : Component
{
	/// <summary> The current state of the game, should never be null </summary>
	[Property, ReadOnly, Sync] public GameSystem GameSystem { get; set; }

	[Broadcast]
	public void EndGame( Team team = Team.None )
	{
		var gs = Scene.GetAll<GameSystem>()?.FirstOrDefault();

		if ( !gs.IsValid() || !Networking.IsHost )
			return;

		gs.State = GameSystem.GameState.Ended;

		Log.Info( $"Game Ended: {team}" );

		if ( team != Team.None )
			WinGame( team );
		else
			WinGame();
	}

	public virtual Team WinningTeam() => Team.None;

	public virtual void WinGame( Team team = Team.None )
	{
		if ( team == Team.None )
			team = WinningTeam();

		PopupHolder.BroadcastPopup( $"{team} won", 5 );

		Scene.GetAll<Inventory>()?.ToList()?.ForEach( x =>
		{
			x.ClearAll();
			x.ClearSelectedClass();
		} );

		Scene.GetAll<HealthComponent>()?.ToList()?.ForEach( x => x.ResetHealth() );

		GameSystem.RedTimeHeld = GameSystem.InitRedTimeHeld;
		GameSystem.BlueTimeHeld = GameSystem.InitBlueTimeHeld;
		GameSystem.YellowTimeHeld = GameSystem.InitYellowTimeHeld;
		GameSystem.GreenTimeHeld = GameSystem.InitGreenTimeHeld;
		GameSystem.Overtimes = 0;
	}

	[Broadcast]
	public void BroadcastChangeState( GameSystem.GameState state )
	{
		Scene.Dispatch( new OnRoundSwitch( state ) );
	}
}

[GameResource( "Game Mode", "mode", "A game mode that can be selected by the player", Icon = "gamepad" )]
public sealed class GameModeResource : GameResource
{
	public string Title { get; set; }
	[TextArea] public string Description { get; set; }
	public GameObject Prefab { get; set; }
	public bool Hidden { get; set; } = false;
	public GameModeType Type { get; set; }
}

public sealed class GameModeObject : Component
{
	[Property] public GameModeType Type { get; set; }
}
