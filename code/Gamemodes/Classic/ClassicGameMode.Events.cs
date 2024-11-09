using Sandbox.Events;

public partial class ClassicGameMode
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

		BroadcastChangeState( GameSystem.GameState.BuildMode );

		var text = Game.Random.FromList( BuildModePopups );

		PopupHolder.BroadcastPopup( text, 5 );

		GameSystem.StateSwitch = 0;
	}

	[After<OnBuildMode>]
	void IGameEventHandler<OnFightMode>.OnGameEvent( OnFightMode eventArgs )
	{
		GameSystem.StateSwitch = 0;
		Log.Info( "Fight Mode" );

		Scene.GetAll<HealthComponent>()?.ToList()?.ForEach( x => x.ResetHealth() );

		Scene.GetAll<Inventory>()?.ToList()?.ForEach( x =>
		{
			//Clear the inventory and give players the grav gun
			x.ClearAll();
			x.AddItem( ResourceLibrary.GetAll<WeaponData>().FirstOrDefault( x => x.ResourceName == "gravgun" ) );

			x.OpenClassSelect();
		} );

		//Broadcast random text from a list
		var text = Game.Random.FromList( FightModePopups );
		PopupHolder.BroadcastPopup( text, 5 );

		//Event callback
		BroadcastChangeState( GameSystem.GameState.FightMode );
	}

	[After<OnFightMode>, After<OnGameOvertimeFight>]
	void IGameEventHandler<OnGameEnd>.OnGameEvent( OnGameEnd eventArgs )
	{
		GameSystem.StateSwitch = 0;
		Log.Info( "Game Ended" );

		Scene.GetAll<Inventory>()?.ToList()?.ForEach( x => x.ClearAll() );

		Scene.GetAll<FWPlayerController>()?.ToList()?.ForEach( x => x.ResetStats() );

		DeleteClassSelect();

		BroadcastChangeState( GameSystem.GameState.Ended );
	}

	[After<OnGameEnd>]
	void IGameEventHandler<OnGameWaiting>.OnGameEvent( OnGameWaiting eventArgs )
	{
		GameSystem.StateSwitch = 0;
		ResetPlayers();
		Log.Info( "Game Waiting" );

		Scene.GetAll<HealthComponent>()?.ToList()?.ForEach( x => x.ResetHealth() );

		Scene.GetAll<TeamComponent>()?.ToList()?.ForEach( x => x.SetTeam( Team.None ) );

		BroadcastChangeState( GameSystem.GameState.Waiting );
	}

	[After<OnFightMode>, After<OnGameOvertimeFight>]
	void IGameEventHandler<OnGameOvertimeBuild>.OnGameEvent( OnGameOvertimeBuild eventArgs )
	{
		GameSystem.StateSwitch = 0;
		Log.Info( "Overtime" );

		Scene.GetAll<HealthComponent>()?.ToList()?.ForEach( x => x.ResetHealth() );

		Scene.GetAll<Inventory>()?.ToList()?.ForEach( x =>
		{
			x.ClearAll();
			x.AddItem( ResourceLibrary.GetAll<WeaponData>().FirstOrDefault( x => x.ResourceName == "propgun" ) );
		} );

		BroadcastChangeState( GameSystem.GameState.OvertimeBuild );

		PopupHolder.BroadcastPopup( "Get ready for overtime, build now!", 5 );
	}

	[After<OnGameOvertimeBuild>]
	void IGameEventHandler<OnGameOvertimeFight>.OnGameEvent( OnGameOvertimeFight eventArgs )
	{
		GameSystem.StateSwitch = 0;
		Log.Info( "Overtime Fight" );

		Scene.GetAll<HealthComponent>()?.ToList()?.ForEach( x => x.ResetHealth() );

		Scene.GetAll<Inventory>()?.ToList()?.ForEach( x =>
		{
			x.ClearAll();
			x.AddItem( ResourceLibrary.GetAll<WeaponData>().FirstOrDefault( x => x.ResourceName == "gravgun" ) );

			if ( x.SelectedClass is not null )
				x.AddItem( x.SelectedClass.WeaponData, true );
		} );

		BroadcastChangeState( GameSystem.GameState.OvertimeFight );

		PopupHolder.BroadcastPopup( "Get ready for overtime, fight now!", 5 );
	}
}
