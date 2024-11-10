
using Sandbox.Events;
using System;

public partial class ClassicGameMode : GameMode,
IGameEventHandler<OnBuildMode>, IGameEventHandler<OnGameEnd>, IGameEventHandler<OnGameWaiting>, IGameEventHandler<OnFightMode>,
IGameEventHandler<OnGameOvertimeBuild>, IGameEventHandler<OnGameOvertimeFight>
{
	protected override void OnUpdate()
	{
		if ( Networking.IsHost )
		{
			//GameLoop();
		}

		base.OnUpdate();
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
					//Scene.Dispatch( new OnBuildMode() );
					//GameSystem.State = GameSystem.GameState.BuildMode;
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
}
