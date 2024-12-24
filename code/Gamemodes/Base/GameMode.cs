public partial class GameMode : Component, Component.INetworkListener
{
	/// <summary> The current state of the game, should never be null </summary>
	[Property, ReadOnly, Sync] public GameSystem GameSystem { get; set; }
	[Property] public RoundComponent InitialRound { get; set; }
	[Property, Sync, ReadOnly] public RoundComponent CurrentRound { get; set; }
	[Property] public Action<Team> OnGameEnd { get; set; }
	[Property] public Action OnGameStart { get; set; }
	[Property, ReadOnly, Sync] public bool TeamsEnabled { get; set; } = false;
	[Property] public bool HasMapVoting { get; set; } = true;

	// Note this is awful and long and i hate it but it works
	// TODO: Big T says to fix this
	public static RoundComponent ActiveRound { get { return Game.ActiveScene.GetAllComponents<GameMode>().FirstOrDefault().Components.GetAll<RoundComponent>().Where( x => x.IsRoundActive ).FirstOrDefault(); } }

	[Property, Sync] public bool RespawnPlayers { get; set; } = true;
	[Property, ToggleGroup( "SetMaxPlayersToStart" )] public bool SetMaxPlayersToStart { get; set; } = false;
	[Property, ToggleGroup( "SetMaxPlayersToStart" )] public int MaxPlayersToStart { get; set; } = 2;

	protected override void OnStart()
	{
		Log.Info( "Game Mode Started" );

		if ( IsProxy )
			return;

		GameObject.Network.SetOrphanedMode( NetworkOrphaned.Host );
	}

	public bool GameHasStarted { get; set; } = false;

	public void StartGame()
	{
		GameHasStarted = true;
		CurrentRound = InitialRound;
		InitialRound.ActivateRound();
		Log.Info( "Activated inital round" );

		OnGameStart?.Invoke();
	}

	void INetworkListener.OnActive( Connection channel )
	{
		OnActive( channel );
	}

	[Rpc.Broadcast]
	public void EndGame( Team team = Team.None )
	{
		var gs = GameSystem.Instance;

		if ( !gs.IsValid() || !Networking.IsHost )
			return;

		if ( ActiveRound.IsValid() && ActiveRound.IsRoundActive )
		{
			ActiveRound.IsRoundActive = false;
			ActiveRound?.OnRoundEnd?.Invoke();
		}

		Log.Info( $"Game Ended: {team}" );

		// Credit Matek
		WinGame( team );

		OnGameEnd?.Invoke( team );

		GameSystem.Overtimes = 0;
		GameSystem.GameState = GameSystem.GameStates.S_END;
		GameSystem.StateSwitch = 0;

		if ( HasMapVoting && Connection.All.Any() && Networking.IsHost && !Application.IsHeadless )
		{
			GameSystem.GameState = GameSystem.GameStates.S_VOTING;

			HUD.FlashMapVoting();
		}
		else
		{
			GameSystem.GameState = GameSystem.GameStates.S_WAITING;
			Log.Info( "No map voting, waiting for next round" );
		}
	}

	private Dictionary<FWPlayerController, Team> playerTeamAssignments = new Dictionary<FWPlayerController, Team>();

	public void EnableTeams()
	{
		TeamsEnabled = true;

		TeamComponent.ResetTeams();

		var players = Scene.GetAllComponents<FWPlayerController>().ToList();
		var teamCounts = new Dictionary<Team, int>();

		foreach ( var team in Enum.GetValues( typeof( Team ) ).Cast<Team>().Where( x => x != Team.Green && x != Team.Yellow && x != Team.None ) )
		{
			teamCounts[team] = 0;
		}

		players = players.OrderBy( x => Game.Random.Next() ).ToList();

		// Assign teams to players
		foreach ( var player in players )
		{
			var team = GetTeamLowestCount( teamCounts );
			player.TeamComponent.SetTeam( team );
			playerTeamAssignments[player] = team;
			teamCounts[team]++;
		}
	}

	public Team GetTeamLowestCount( Dictionary<Team, int> teamCounts )
	{
		return teamCounts.OrderBy( kvp => kvp.Value ).First().Key;
	}

	public virtual Team WinningTeam() => Team.None;

	[Rpc.Broadcast]
	public void ResetPlayers()
	{
		Scene.Dispatch( new PlayerReset() );
	}

	[Rpc.Broadcast]
	public void SetGamemode( GameModeResource type )
	{
		if ( type is not null )
			GameSystem.SavedGameMode = type;
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
		Scene.GetAll<TeamComponent>()?.ToList()?.ForEach( x => x.SetTeam( Team.None, true ) );

		GameSystem.RedTimeHeld = GameSystem.InitRedTimeHeld;
		GameSystem.BlueTimeHeld = GameSystem.InitBlueTimeHeld;
		GameSystem.YellowTimeHeld = GameSystem.InitYellowTimeHeld;
		GameSystem.GreenTimeHeld = GameSystem.InitGreenTimeHeld;

		GameSystem.RedFlagsCaptured = 0;
		GameSystem.BlueFlagsCaptured = 0;
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
	public static void SkipWait( int i = 1 )
	{
		if ( !Networking.IsHost && !(DevCheck.IsDev( Connection.Local.SteamId ) && !Networking.IsHost) )
		{
			Log.Warning( "Only the host or a dev can call this method" );
			return;
		}

		var gs = GameSystem.Instance;

		if ( !gs.IsValid() )
			return;

		if ( gs.GameState == GameSystem.GameStates.S_WAITING || gs.GameState == GameSystem.GameStates.S_VOTING )
		{
			gs.GameState = GameSystem.GameStates.S_ACTIVE;

			RoundComponent.DeleteAllMapVoting();
		}

		if ( (!gs.CurrentGameModeComponent?.CurrentRound.IsValid() ?? false) && (gs.CurrentGameModeComponent?.InitialRound.IsValid() ?? false) )
		{
			gs.CurrentGameModeComponent.InitialRound.ActivateRound();
		}
		else if ( (gs.CurrentGameModeComponent?.CurrentRound?.NextRoundCondition.IsValid() ?? false) || (gs.CurrentGameModeComponent?.CurrentRound?.NextRoundTimer.IsValid() ?? false))
		{
			gs.CurrentGameModeComponent?.CurrentRound?.EndRound( i == 0 ? false : true );
		}
		else if ( gs.CurrentGameModeComponent?.CurrentRound.IsValid() ?? false )
		{
			Log.Warning( "Next round not found, use 'end_game' to force next round" );
		}
	}

	[ConCmd( "end_game" )]
	public static void EndGame()
	{
		if ( !Networking.IsHost && !(DevCheck.IsDev( Connection.Local.SteamId ) && !Networking.IsHost) )
		{
			Log.Warning( "Only the host or a dev can call this method" );
			return;
		}

		var gs = GameSystem.Instance;

		if ( !gs.IsValid() )
			return;

		gs.CurrentGameModeComponent?.EndGame();
	}
}

public sealed class GameModeObject : Component
{
	[Property] public GameModeType Type { get; set; }
}
