using System.Text.Json.Serialization;

public record OnRoundSwitch() : IGameEvent;

public sealed class RoundComponent : Component
{
	[Property, Feature( "Metadata" ), Sync] public string Name { get; set; }
	[Property, Sync, ReadOnly, Feature( "Metadata" )] public bool IsRoundActive { get; set; }
	[Property, Feature( "Metadata" )] public bool CheckForWinningTeam { get; set; } = false;
	[Property, Feature( "Metadata" )] public bool IsLastRound { get; set; } = false;

	[Property, ToggleGroup( "Condition" )] public bool Condition { get; set; }
	[Property, Group( "Condition" )] public Func<bool> EndCondition { get; set; }
	[Property, Group( "Condition" ), ShowIf( "IsLastRound", false )] public RoundComponent NextRoundCondition { get; set; }

	[Property, ToggleGroup( "Time" )] public bool Time { get; set; }

	[Property, Group( "Time" ), Sync] public float RoundTime { get; set; }
	[Property, Group( "Time" ), ShowIf( "IsLastRound", false )] public RoundComponent NextRoundTimer { get; set; }

	[Property, Feature( "Inventory" )] public bool AddClass { get; set; }
	[Property, Feature( "Inventory" )] public bool ClearAllWeapons { get; set; } = true;
	[Property, Feature( "Inventory" )] public bool ClearClass { get; set; }
	[Property, Feature( "Inventory" )] public List<WeaponData> PlayerWeapons { get; set; }

	[Property, Category( "Actions" )] public Action OnRoundStart { get; set; }
	[Property, Category( "Actions" )] public Action OnRoundEnd { get; set; }
	[Property, Category( "Actions" )] public Action RoundUpdate { get; set; }
	[Property, Category( "Actions" )] public Action<GameObject> OnPlayerJoin { get; set; }

	[InlineEditor, Property, Sync, JsonIgnore] public TimeUntil RoundTimer { get; set; }

	[Property] bool PlayersToSpawns { get; set; } = true;
	[Property] bool ResetPlayerResouces { get; set; } = false;

	[Property] public bool CanOpenClassSelect { get; set; } = false;
	[Property] public bool HasResources { get; set; } = false;

	[Property, ToggleGroup( "Warning" )] public bool Warning { get; set; }
	[Property, Group( "Warning" )] public string WarningText { get; set; }
	[Property, Group( "Warning" )] public int WarningTime { get; set; }
	[Property, Group( "Warning" )] public int WarningDuration { get; set; }

	public GameMode GameMode => Scene?.GetAll<GameMode>()?.FirstOrDefault();

	public void ActivateRound()
	{
		Log.Info( "Activating round: " + Name );

		OnRoundStart?.Invoke();

		RoundTimer = RoundTime;

		Scene.GetAll<Inventory>()?.ToList()?.ForEach( x =>
		{
			AddWeapons( x );
		} );

		IsRoundActive = true;

		var instance = GameSystem.Instance;

		if ( instance.IsValid() && instance.CurrentGameModeComponent.IsValid() )
		{
			instance.CurrentGameModeComponent.CurrentRound = this;

			instance.CurrentTime = RoundTime;
		}

		if ( ResetPlayerResouces )
			Scene?.GetAll<FWPlayerController>()?.ToList()?.ForEach( x => x.ResetResouces() );

		if ( PlayersToSpawns )
			TeamComponent.TeleportAllTeams();

		Scene.Dispatch( new OnRoundSwitch() );

		SetActiveRound();

		var gs = GameSystem.Instance;

		if ( gs.IsValid() )
			gs.StateSwitch = 0;
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !IsRoundActive )
			return;

		RoundUpdate?.Invoke();

		if ( Time )
		{
			if ( RoundTimer )
			{
				EndRound( true );
			}
		}

		if ( Condition )
		{
			if ( EndCondition?.Invoke() ?? false )
			{
				EndRound( false );
			}
		}

		if ( CheckForWinningTeam )
		{
			GameMode.CheckForWinningTeam();
		}

		if ( Warning )
		{
			if ( RoundTimer.Relative.CeilToInt() == WarningTime )
			{
				PopupHolder.BroadcastPopup( WarningText, WarningDuration );
			}
		}
	}

	public void AddWeapons( Inventory inventory )
	{
		if ( ClearAllWeapons )
			inventory.ClearAll();

		if ( ClearClass )
			inventory.ClearSelectedClass();

		inventory.AddItems( PlayerWeapons );

		if ( AddClass && inventory.SelectedClass is not null )
		{
			inventory.AddClass( inventory.SelectedClass );
		}
	}

	public void EndRound( bool timer )
	{
		OnRoundEnd?.Invoke();

		IsRoundActive = false;
		Log.Info( "Ending round: " + Name );

		if ( IsLastRound )
		{
			GameSystem.Instance?.CurrentGameModeComponent?.EndGame();
			Log.Info( "Last round ended" );
			return;
		}

		if ( !timer )
		{
			NextRoundCondition?.ActivateRound();
			Log.Info( "Next round condiction activated" );
		}
		else
		{
			NextRoundTimer?.ActivateRound();
			Log.Info( "Next round timer activated" );
		}
	}

	[Rpc.Broadcast]
	public void SetActiveRound()
	{
		//GameMode.ActiveRound = this;
	}

	/// <summary> Creates random teams for the players </summary>
	public void SetTeams()
	{
		var players = Scene.GetAllComponents<TeamComponent>().ToList();
		var teams = GameSystem.Instance.FourTeams ? new List<Team> { Team.Red, Team.Blue, Team.Yellow, Team.Green } : new List<Team> { Team.Red, Team.Blue };

		players = players.OrderBy( x => Game.Random.Next() ).ToList();

		for ( int i = 0; i < players.Count; i++ )
		{
			players[i].SetTeam( teams[i % teams.Count] );
		}

		Scene.GetAll<FWPlayerController>()?.ToList()?.ForEach( x => x.TeleportToTeamSpawnPoint() );
	}

	public void ResetAllHealth()
	{
		Scene.GetAll<HealthComponent>()?.ToList()?.ForEach( x => x.ResetHealth() );
	}

	[ActionGraphNode( "50 / 50" ), Pure]
	public static bool FiftyFifty()
	{
		return Game.Random.Float( 0, 1 ) > 0.5f;
	}

	[Rpc.Broadcast]
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

	public void OpenAllClassSelect()
	{
		Scene.GetAll<Inventory>()?.ToList()?.ForEach( x =>
		{
			x.OpenClassSelect();
		} );
	}

	public void ClearAll()
	{
		Scene.GetAll<Inventory>()?.ToList()?.ForEach( x =>
		{
			x.ClearAll();
		} );
	}

	public void ClearAllClasses()
	{
		Scene.GetAll<Inventory>()?.ToList()?.ForEach( x =>
		{
			x.ClearSelectedClass();
		} );
	}

	[Rpc.Broadcast]
	public static void DeleteAllMapVoting()
	{
		var hud = Game.ActiveScene?.GetAll<HUD>()?.FirstOrDefault();

		if ( hud.IsValid() && hud.Panel.IsValid() )
		{
			foreach ( var select in hud.Panel.ChildrenOfType<MapVoting>().ToList() )
			{
				select.Delete();
			}

			foreach ( var select in hud.Panel.ChildrenOfType<MiniMapVoting>().ToList() )
			{
				select.Delete();
			}
		}
	}

	public void DestroyAllProps()
	{
		Scene.GetAll<FortwarsProp>()?.Where( x => !x.Invincible )?.ToList()?.ForEach( x => x.DestroyProp() );
	}

	public void SpawnFightModePopups()
	{
		var gamemode = GameMode;

		if ( gamemode.IsValid() )
		{
			var title = Game.Random.FromList( gamemode.FightModePopups );

			PopupHolder.BroadcastPopup( title, 5 );
		}
	}

	public void SpawnBuildModePopups()
	{
		var gamemode = GameMode;

		if ( gamemode.IsValid() )
		{
			var title = Game.Random.FromList( gamemode.BuildModePopups );

			PopupHolder.BroadcastPopup( title, 5 );
		}
	}

	public void SpawnFlags()
	{
		SpawnNewFlag( Team.Red );
		SpawnNewFlag( Team.Blue );
	}

	public static void SpawnNewFlag( Team team )
	{
		var flags = Game.ActiveScene.GetAll<FlagSpawn>();

		if ( flags.Count() == 0 )
			return;

		SpawnFlag( team );
	}

	//Workaround until the builds get fixed
	[Rpc.Host]
	private static void SpawnFlag( Team team )
	{
		if ( Game.ActiveScene.GetAll<DroppedFlag>().Any( x => x.TeamFlag == team ) )
		{
			Log.Warning( "Flag already exists" );
			return;
		}

		var flagSpawn = Game.ActiveScene?.GetAll<FlagSpawn>()?.FirstOrDefault( x => x.Team == team );

		var flagPrefab = ResourceLibrary.Get<PrefabFile>( "prefabs/ctf/droppedflag.prefab" );

		var spawnedFlag = GameObject.Clone( flagPrefab );

		if ( spawnedFlag.IsValid() && flagSpawn.IsValid() )
		{
			spawnedFlag.WorldPosition = flagSpawn.WorldPosition;
			spawnedFlag.WorldRotation = flagSpawn.WorldRotation;

			if ( spawnedFlag.Components.TryGet<DroppedFlag>( out var droppedFlag ) )
			{
				droppedFlag.TeamFlag = team;
			}

			spawnedFlag.NetworkSpawn( null );
		}
	}

	public void RemoveAllFlags()
	{
		Scene.GetAll<DroppedFlag>()?.ToList()?.ForEach( x => x.GameObject.Destroy() );
	}
}
