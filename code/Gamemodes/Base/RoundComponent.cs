using Sandbox.Events;
using System;

public record OnRoundSwitch() : IGameEvent;

public sealed class RoundComponent : Component
{
	[Property, Feature( "Metadata" )] public string Name { get; set; }
	[Property, ReadOnly, Feature( "Metadata" )] public bool IsRoundActive { get; set; }
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
	[Property, Feature( "Inventory" )] List<WeaponData> PlayerWeapons { get; set; }

	[Property, Category( "Actions" )] public Action OnRoundStart { get; set; }
	[Property, Category( "Actions" )] public Action OnRoundEnd { get; set; }
	[Property, Category( "Actions" )] public Action RoundUpdate { get; set; }

	[InlineEditor, Property, Sync] public TimeUntil RoundTimer { get; set; }

	[Property] bool PlayersToSpawns { get; set; } = true;

	public GameMode GameMode => Scene?.GetAll<GameMode>()?.FirstOrDefault();

	public void ActivateRound()
	{
		Log.Info( "Activating round: " + Name );

		OnRoundStart?.Invoke();

		RoundTimer = RoundTime;

		Scene.GetAll<Inventory>()?.ToList()?.ForEach( x =>
		{
			if ( ClearAllWeapons )
				x.ClearAll();

			if ( ClearClass )
				x.ClearSelectedClass();

			x.AddItems( PlayerWeapons );

			if ( AddClass && x.SelectedClass is not null )
			{
				x.AddClass( x.SelectedClass );
			}
		} );

		IsRoundActive = true;

		var instance = GameSystem.Instance;

		if ( instance.IsValid() && instance.CurrentGameModeComponent.IsValid() )
		{
			instance.CurrentGameModeComponent.CurrentRound = this;

			instance.CurrentTime = RoundTime;
		}

		if ( PlayersToSpawns )
			GameMode.ResetPlayers();

		Scene.Dispatch( new OnRoundSwitch() );
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
				EndRound();
			}
		}

		if ( Condition )
		{
			if ( EndCondition?.Invoke() ?? false )
			{
				EndRound();
			}
		}
	}

	public void EndRound()
	{
		OnRoundEnd?.Invoke();

		if ( IsLastRound )
		{
			GameSystem.Instance?.CurrentGameModeComponent?.EndGame();
			return;
		}

		if ( Condition )
			NextRoundCondition?.ActivateRound();
		else
			NextRoundTimer?.ActivateRound();

		IsRoundActive = false;
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
}
