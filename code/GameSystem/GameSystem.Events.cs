using Sandbox.Events;

public partial class GameSystem
{
	[After<OnGameWaiting>, After<OnPlayerJoin>]
	void IGameEventHandler<OnBuildMode>.OnGameEvent( OnBuildMode eventArgs )
	{
		SetTeams();

		Scene.GetAll<HealthComponent>()?.ToList()?.ForEach( x => x.ResetHealth() );

		Scene.GetAll<Inventory>()?.ToList()?.ForEach( x =>
		{
			x.ClearSelectedClass();
			x.ClearAll();
			x.AddItem( ResourceLibrary.GetAll<WeaponData>().FirstOrDefault( x => x.ResourceName == "propgun" ) );
			//x.AddItem( ResourceLibrary.GetAll<WeaponData>().FirstOrDefault( x => x.ResourceName == "physgun" ) );
			//x.OpenClassSelect();
		} );

		Log.Info( "Build Mode" );

		BroadcastChangeState( GameState.BuildMode );

		var text = Game.Random.FromList( BuildModePopups );

		PopupHolder.BroadcastPopup( text, 5 );

		StateSwitch = 0;
	}

	[After<OnBuildMode>]
	void IGameEventHandler<OnFightMode>.OnGameEvent( OnFightMode eventArgs )
	{
		StateSwitch = 0;
		Log.Info( "Fight Mode" );

		Scene.GetAll<HealthComponent>()?.ToList()?.ForEach( x => x.ResetHealth() );

		Scene.GetAll<Inventory>()?.ToList()?.ForEach( x =>
		{
			//Clear the inventory and give players the grav gun
			x.ClearAll();
			x.AddItem( ResourceLibrary.GetAll<WeaponData>().FirstOrDefault( x => x.ResourceName == "gravgun" ) );

			/*if ( x.SelectedClass is not null )
			{
				x.AddItem( x.SelectedClass.WeaponData );
			}*/

			x.OpenClassSelect();
		} );

		//Broadcast random text from a list
		var text = Game.Random.FromList( FightModePopups );
		PopupHolder.BroadcastPopup( text, 5 );

		//Event callback
		BroadcastChangeState( GameState.FightMode );
	}

	[After<OnFightMode>, After<OnGameOvertimeFight>]
	void IGameEventHandler<OnGameEnd>.OnGameEvent( OnGameEnd eventArgs )
	{
		StateSwitch = 0;
		Log.Info( "Game Ended" );

		var winningTeam = GetWinningTeam();

		OnTeamWon( GetWinningTeam() );

		Scene.GetAll<Inventory>()?.ToList()?.ForEach( x => x.ClearAll() );

		Scene.GetAll<FWPlayerController>()?.ToList()?.ForEach( x => x.ResetStats() );

		Log.Info( $"{GetWinningTeam()} won" );

		PopupHolder.BroadcastPopup( $"{GetWinningTeam()} won", 5 );

		Scene.GetAll<Inventory>()?.ToList()?.ForEach( x =>
		{
			x.ClearAll();
			x.ClearSelectedClass();
		} );

		Scene.GetAll<HealthComponent>()?.ToList()?.ForEach( x => x.ResetHealth() );

		DeleteClassSelect();

		BroadcastChangeState( GameState.Ended );

		RedTimeHeld = InitRedTimeHeld;
		BlueTimeHeld = InitBlueTimeHeld;
		YellowTimeHeld = InitYellowTimeHeld;
		GreenTimeHeld = InitGreenTimeHeld;
		Overtimes = 0;
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

	[Broadcast]
	public void OnTeamWon( Team team )
	{
		Scene.Dispatch( new OnTeamWin( team ) );
	}

	[After<OnGameEnd>]
	void IGameEventHandler<OnGameWaiting>.OnGameEvent( OnGameWaiting eventArgs )
	{
		StateSwitch = 0;
		ResetPlayers();
		Log.Info( "Game Waiting" );

		Scene.GetAll<HealthComponent>()?.ToList()?.ForEach( x => x.ResetHealth() );

		Scene.GetAll<TeamComponent>()?.ToList()?.ForEach( x => x.SetTeam( Team.None ) );

		BroadcastChangeState( GameState.Waiting );
	}

	[After<OnFightMode>, After<OnGameOvertimeFight>]
	void IGameEventHandler<OnGameOvertimeBuild>.OnGameEvent( OnGameOvertimeBuild eventArgs )
	{
		StateSwitch = 0;
		Log.Info( "Overtime" );

		Scene.GetAll<HealthComponent>()?.ToList()?.ForEach( x => x.ResetHealth() );

		Scene.GetAll<Inventory>()?.ToList()?.ForEach( x =>
		{
			x.ClearAll();
			x.AddItem( ResourceLibrary.GetAll<WeaponData>().FirstOrDefault( x => x.ResourceName == "propgun" ) );
			x.AddItem( ResourceLibrary.GetAll<WeaponData>().FirstOrDefault( x => x.ResourceName == "physgun" ) );
		} );

		BroadcastChangeState( GameState.OvertimeBuild );

		PopupHolder.BroadcastPopup( "Get ready for overtime, build now!", 5 );
	}

	[After<OnGameOvertimeBuild>]
	void IGameEventHandler<OnGameOvertimeFight>.OnGameEvent( OnGameOvertimeFight eventArgs )
	{
		StateSwitch = 0;
		Log.Info( "Overtime Fight" );

		Scene.GetAll<HealthComponent>()?.ToList()?.ForEach( x => x.ResetHealth() );

		Scene.GetAll<Inventory>()?.ToList()?.ForEach( x =>
		{
			x.ClearAll();
			x.AddItem( ResourceLibrary.GetAll<WeaponData>().FirstOrDefault( x => x.ResourceName == "gravgun" ) );

			if ( x.SelectedClass is not null )
				x.AddItem( x.SelectedClass.WeaponData, true );
		} );

		BroadcastChangeState( GameState.OvertimeFight );

		PopupHolder.BroadcastPopup( "Get ready for overtime, fight now!", 5 );
	}
}
