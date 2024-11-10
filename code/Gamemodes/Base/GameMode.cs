
using Sandbox.Events;

public partial class GameMode : Component, Component.INetworkListener
{
	/// <summary> The current state of the game, should never be null </summary>
	[Property, ReadOnly, Sync] public GameSystem GameSystem { get; set; }

	[Property] public RoundComponent InitialRound { get; set; }

	protected override void OnStart()
	{
		Log.Info( "Game Mode Started" );
	}

	public virtual void OnActive( Connection connection ) { }

	protected override void OnUpdate()
	{
		if ( Networking.IsHost )
		{
			//If we are the dedicated server and all players left, end the game
			if ( Connection.All.Where( x => x != Connection.Local ).Count() == 0 && GameSystem.IsPlaying && Application.IsHeadless )
			{
				GameSystem.State = GameSystem.GameState.Ended;
				Scene.Dispatch( new OnGameEnd() );

				Log.Info( "All players left, ending game." );
			}
		}
	}

	void INetworkListener.OnActive( Connection channel )
	{
		OnActive( channel );
	}

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

	/// <summary> Creates random teams for the players </summary>
	public void SetTeams()
	{
		var players = Scene.GetAllComponents<TeamComponent>().ToList();
		var teams = GameSystem.FourTeams ? new List<Team> { Team.Red, Team.Blue, Team.Yellow, Team.Green } : new List<Team> { Team.Red, Team.Blue };

		players = players.OrderBy( x => Game.Random.Next() ).ToList();

		for ( int i = 0; i < players.Count; i++ )
		{
			players[i].SetTeam( teams[i % teams.Count] );
		}

		Scene.GetAll<FWPlayerController>().ToList().ForEach( x => x.TeleportToTeamSpawnPoint() );
	}

	[Broadcast]
	public void BroadcastChangeState( GameSystem.GameState state )
	{
		Scene.Dispatch( new OnRoundSwitch( state ) );
	}

	[Broadcast]
	public void DeleteClassSelect()
	{
		var hud = Scene.GetAll<HUD>()?.FirstOrDefault();

		if ( hud.IsValid() )
		{
			foreach ( var select in hud.Panel.ChildrenOfType<ClassSelect>().ToList() )
			{
				select.Delete();
			}
		}
	}

	public List<string> FightModePopups = new()
	{
		"Fight for your right to party!",
		"Elect your mightiest ball grabber!",
		"Defend your teams base or die trying!",
		"First they ignore you, then they laugh at you, then they fight you, then you win!",
		"The harder you fight, the sweeter the victory!",
		"Is it a crime, to fight, for what is mine?",
		"Now I am become Death, the Destroyer of Forts",
	};

	public List<string> BuildModePopups = new()
	{
		"If they tear it down, build it again!",
		"Fat and heavy wins the race, FORTIFY your base!",
		"Elect your mightiest constructor of forts!",
		"Build your fort from the rocks thrown at you or that stood in your way, and it, like you, will have strength untold.",
		"Fortitude is the marshal of thought, the armor of the will, and the fort of reason.",
		"We shape our forts; thereafter they shape us.",
	};

	[ConCmd( "skip_wait" )]
	public static void SkipWait()
	{
		if ( !Networking.IsHost )
			return;

		var gs = GameSystem.Instance;

		if ( !gs.IsValid() )
			return;

		switch ( gs.State )
		{
			case GameSystem.GameState.Waiting:
				GameSystem.Instance?.Scene.Dispatch( new OnBuildMode() );
				GameSystem.Instance.State = GameSystem.GameState.BuildMode;
				break;
			case GameSystem.GameState.BuildMode:
				GameSystem.Instance?.Scene.Dispatch( new OnFightMode() );
				GameSystem.Instance.State = GameSystem.GameState.FightMode;
				break;
			case GameSystem.GameState.FightMode:
				GameSystem.Instance?.Scene.Dispatch( new OnGameEnd() );
				GameSystem.Instance.State = GameSystem.GameState.Ended;
				break;
			case GameSystem.GameState.OvertimeBuild:
				GameSystem.Instance?.Scene.Dispatch( new OnGameOvertimeFight() );
				GameSystem.Instance.State = GameSystem.GameState.OvertimeFight;
				break;
			case GameSystem.GameState.OvertimeFight:
				GameSystem.Instance?.Scene.Dispatch( new OnGameOvertimeBuild() );
				GameSystem.Instance.State = GameSystem.GameState.OvertimeBuild;
				break;
			case GameSystem.GameState.Ended:
				GameSystem.Instance?.Scene.Dispatch( new OnBuildMode() );
				GameSystem.Instance.State = GameSystem.GameState.BuildMode;
				break;
		}
	}
}



public sealed class GameModeObject : Component
{
	[Property] public GameModeType Type { get; set; }
}
