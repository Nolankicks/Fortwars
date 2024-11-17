
using Sandbox.Events;
using System;

public partial class GameMode : Component, Component.INetworkListener
{
	/// <summary> The current state of the game, should never be null </summary>
	[Property, ReadOnly, Sync] public GameSystem GameSystem { get; set; }

	[Property] public RoundComponent InitialRound { get; set; }

	[Property, Sync, ReadOnly] public RoundComponent CurrentRound { get; set; }

	[Property] public Action<Team> OnGameEnd { get; set; }

	[Property, ReadOnly] public bool TeamsEnabled { get; set; } = false;

	public static RoundComponent ActiveRound { get { return Game.ActiveScene.GetAllComponents<GameMode>().FirstOrDefault().Components.GetAll<RoundComponent>().Where( x => x.IsRoundActive ).FirstOrDefault(); } }

	protected override void OnStart()
	{
		Log.Info( "Game Mode Started" );
	}


	public bool GameHasStarted { get; set; } = false;

	public void StartGame()
	{
		GameHasStarted = true;
		CurrentRound = InitialRound;
		InitialRound.ActivateRound();
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

		Log.Info( $"Game Ended: {team}" );

		if ( team != Team.None )
			WinGame( team );
		else
			WinGame();

		OnGameEnd?.Invoke( team );

		GameSystem.Overtimes = 0;
		GameSystem.GameState = GameSystem.GameStates.S_END;
		GameSystem.StateSwitch = 0;
	}

	public void EnableTeams()
	{
		TeamComponent.ResetTeams();
		foreach ( var player in Scene.GetAllComponents<FWPlayerController>() )
		{
			player.TeamComponent.SetTeam( TeamComponent.GetTeamLowestCount() );
		}
	}

	public virtual Team WinningTeam() => Team.None;

	[Broadcast]
	public void ResetPlayers()
	{
		Scene.Dispatch( new PlayerReset() );
	}

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
		Scene.GetAll<RollerMine>()?.ToList()?.ForEach( x => x.ResetPosition() );

		GameSystem.RedTimeHeld = GameSystem.InitRedTimeHeld;
		GameSystem.BlueTimeHeld = GameSystem.InitBlueTimeHeld;
		GameSystem.YellowTimeHeld = GameSystem.InitYellowTimeHeld;
		GameSystem.GreenTimeHeld = GameSystem.InitGreenTimeHeld;
	}



	public virtual void CheckForWinningTeam() { }

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
	public static void SkipWait( int i )
	{
		if ( !Networking.IsHost )
			return;

		var gs = GameSystem.Instance;

		if ( !gs.IsValid() )
			return;

		if ( gs.GameState == GameSystem.GameStates.S_WAITING )
			gs.GameState = GameSystem.GameStates.S_ACTIVE;

		if ( (!gs.CurrentGameModeComponent?.CurrentRound.IsValid() ?? false) && (gs.CurrentGameModeComponent?.InitialRound.IsValid() ?? false) )
		{
			gs.CurrentGameModeComponent.InitialRound.ActivateRound();
		}
		else
		{
			gs.CurrentGameModeComponent?.CurrentRound?.EndRound( i == 0 ? false : true );
		}
	}
}



public sealed class GameModeObject : Component
{
	[Property] public GameModeType Type { get; set; }
}
