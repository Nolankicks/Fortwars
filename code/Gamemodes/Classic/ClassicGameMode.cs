
using System;
using System.Threading.Tasks;
using Sandbox.Events;

public partial class ClassicGameMode : GameMode, Component.INetworkListener,
IGameEventHandler<OnBuildMode>, IGameEventHandler<OnGameEnd>, IGameEventHandler<OnGameWaiting>, IGameEventHandler<OnFightMode>,
IGameEventHandler<OnGameOvertimeBuild>, IGameEventHandler<OnGameOvertimeFight>
{
	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		GameLoop();

		//If we are the dedicated server and all players left, end the game
		if ( Connection.All.Where( x => x != Connection.Local ).Count() == 0 && GameSystem.IsPlaying && Application.IsHeadless )
		{
			GameSystem.State = GameSystem.GameState.Ended;
			Scene.Dispatch( new OnGameEnd() );

			Log.Info( "All players left, ending game." );
		}
	}

	/// <summary> The main game loop </summary>
	public void GameLoop()
	{
		switch ( GameSystem.State )
		{
			case GameSystem.GameState.Waiting:
				//Start the game if we have enough players
				if ( Scene.GetAll<FWPlayerController>().Count() >= GameSystem.PlayerToStart && GameSystem.StateSwitch > 5 )
				{
					Scene.Dispatch( new OnBuildMode() );
					GameSystem.State = GameSystem.GameState.BuildMode;
				}
				break;

			case GameSystem.GameState.BuildMode:
				//After build time is over, switch to fight mode
				if ( GameSystem.StateSwitch > GameSystem.BuildTime )
				{
					Scene.Dispatch( new OnFightMode() );
					GameSystem.State = GameSystem.GameState.FightMode;
				}
				break;

			case GameSystem.GameState.FightMode:
				//Constantly check for the winning team
				CheckForWinningTeam();

				//If we don't have one by the end, start overtime
				if ( WinningTeam() == Team.None && GameSystem.StateSwitch > GameSystem.FightTime )
				{
					Scene.Dispatch( new OnGameOvertimeBuild() );
					GameSystem.State = GameSystem.GameState.OvertimeBuild;
				}
				break;
			//Same as above but for overtime
			case GameSystem.GameState.OvertimeBuild:
				if ( GameSystem.StateSwitch > GameSystem.BuildTime )
				{
					Scene.Dispatch( new OnGameOvertimeFight() );
					GameSystem.State = GameSystem.GameState.OvertimeFight;
				}
				break;
			case GameSystem.GameState.OvertimeFight:
				CheckForWinningTeam();

				if ( WinningTeam() == Team.None && GameSystem.StateSwitch > GameSystem.FightTime )
				{
					GameSystem.Overtimes++;

					Log.Info( $"Overtime: {GameSystem.Overtimes}" );

					Scene.Dispatch( new OnGameOvertimeBuild() );
					GameSystem.State = GameSystem.GameState.OvertimeBuild;
				}
				break;

			case GameSystem.GameState.Ended:
				Scene.Dispatch( new OnGameWaiting() );
				GameSystem.State = GameSystem.GameState.Waiting;
				break;
		}
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
	public void ResetPlayers()
	{
		Scene.Dispatch( new PlayerReset() );
	}

	public void CheckForWinningTeam()
	{
		var teams = new Dictionary<Team, float>
		{
			{ Team.Red, GameSystem.RedTimeHeld },
			{ Team.Blue, GameSystem.BlueTimeHeld },
			{ Team.Yellow, GameSystem.YellowTimeHeld },
			{ Team.Green, GameSystem.GreenTimeHeld }
		};

		var max = Math.Round( teams.Min( x => x.Value ), 1 );

		if ( teams.Any( x => x.Value <= 0 ) )
		{
			EndGame( teams.FirstOrDefault( x => x.Value == max ).Key );
		}
	}

	public override Team WinningTeam()
	{
		var teams = new Dictionary<Team, float>
		{
			{ Team.Red, GameSystem.RedTimeHeld },
			{ Team.Blue, GameSystem.BlueTimeHeld },
			{ Team.Yellow, GameSystem.YellowTimeHeld },
			{ Team.Green, GameSystem.GreenTimeHeld }
		};

		var min = teams.Min( x => x.Value );

		if ( Math.Round( min, 1 ) <= 0 )
		{
			return teams.FirstOrDefault( x => x.Value == min ).Key;
		}

		return Team.None;
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
