using Sandbox.Citizen;
using Sandbox.Events;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public record OnBuildMode() : IGameEvent;
public record OnFightMode() : IGameEvent;
public record OnGameWaiting() : IGameEvent;
public record OnGameOvertimeBuild() : IGameEvent;
public record OnGameOvertimeFight() : IGameEvent;
public record OnOnGameEnd() : IGameEvent;
public record OnGameEnd() : IGameEvent;
public record OnTeamWin( Team team ) : IGameEvent;

public record OnRoundSwitch( GameSystem.GameState state ) : IGameEvent;

public sealed class GameSystem : Component, Component.INetworkListener,
IGameEventHandler<OnBuildMode>, IGameEventHandler<OnGameEnd>, IGameEventHandler<OnGameWaiting>, IGameEventHandler<OnFightMode>,
IGameEventHandler<OnGameOvertimeBuild>, IGameEventHandler<OnGameOvertimeFight>
{
	[Property, Category( "Refrences" )] public GameObject PlayerPrefab { get; set; }
	[Property, Category( "Game Settings" )] public bool LoadLobbySettings { get; set; } = true;
	[Property, Category( "Game Settings" )] public bool StartServer { get; set; } = true;
	[Property, Category( "Game Settings" )] public bool SpawnPlayer { get; set; } = true;
	[Property, Category( "Game Settings" )] public float BuildTime { get; set; } = 300;
	[Property, Category( "Game Settings" )] public float FightTime { get; set; } = 300;
	[Property, Category( "Game Settings" ), Sync] public bool ClassicModels { get; set; } = false;
	[Property, Sync, Category( "Game Settings" )] public bool FourTeams { get; set; } = false;

	public float CurrentTime => State == GameState.BuildMode || State == GameState.OvertimeBuild ? BuildTime : FightTime;

	[Property, Category( "Game Settings" )] public int PlayerToStart { get; set; } = 2;

	[Sync] public int TotalKills { get; set; } = 0;

	public enum GameState
	{
		Waiting,
		BuildMode,
		FightMode,
		OvertimeBuild,
		OvertimeFight,
		Ended
	}

	[Property, ReadOnly, Sync, Category( "Game State" )] public GameState State { get; set; } = GameState.Waiting;
	[Sync, InlineEditor, Property, JsonIgnore, Category( "Game State" )] public TimeSince StateSwitch { get; set; } = 0;

	public IEnumerable<TeamComponent> BlueTeam => Scene.GetAll<TeamComponent>().Where( x => x.Team == Team.Blue );
	public IEnumerable<TeamComponent> RedTeam => Scene.GetAll<TeamComponent>().Where( x => x.Team == Team.Red );
	public IEnumerable<TeamComponent> YellowTeam => Scene.GetAll<TeamComponent>().Where( x => x.Team == Team.Yellow );
	public IEnumerable<TeamComponent> GreenTeam => Scene.GetAll<TeamComponent>().Where( x => x.Team == Team.Green );
	public IEnumerable<TeamComponent> NoneTeam => Scene.GetAll<TeamComponent>().Where( x => x.Team == Team.None );

	[Property, Sync, Category( "Game Data" )] public float BlueTimeHeld { get; set; } = 5;
	[Property, Sync, Category( "Game Data" )] public float RedTimeHeld { get; set; } = 5;
	[Property, Sync, Category( "Game Data" )] public float YellowTimeHeld { get; set; } = 5;
	[Property, Sync, Category( "Game Data" )] public float GreenTimeHeld { get; set; } = 5;
	public static GameSystem Instance { get; set; }

	public bool CountUp => State == GameState.Waiting;

	[Sync] public int Overtimes { get; set; } = 0;
	//[Property, Sync, Category( "Game Data" )] public List<string> MountedIndents { get; set; } = new();
	[Property, Category( "Lobby Settings" ), InlineEditor] public LobbySettings LobbySettings { get; set; } = new();

	[Property, Category( "Game Config" )] public Dictionary<string, int> ClassicIndents { get; set; } = new();

	[Sync] public float InitBlueTimeHeld { get; set; } = 5;
	[Sync] public float InitRedTimeHeld { get; set; } = 5;
	[Sync] public float InitYellowTimeHeld { get; set; } = 5;
	[Sync] public float InitGreenTimeHeld { get; set; } = 5;

	public IEnumerable<FortwarsProp> RedProps => Scene?.GetAll<FortwarsProp>().Where( x => x.Team == Team.Red );
	public IEnumerable<FortwarsProp> BlueProps => Scene?.GetAll<FortwarsProp>().Where( x => x.Team == Team.Blue );
	public IEnumerable<FortwarsProp> YellowProps => Scene?.GetAll<FortwarsProp>().Where( x => x.Team == Team.Yellow );
	public IEnumerable<FortwarsProp> GreenProps => Scene?.GetAll<FortwarsProp>().Where( x => x.Team == Team.Green );

	[Property, Sync] public int MaxProps { get; set; } = 50;

	protected override async Task OnLoad()
	{
		if ( Networking.IsHost && !Networking.IsActive && StartServer && !Scene.IsEditor )
		{
			LoadingScreen.Title = "Creating Lobby...";
			await Task.DelaySeconds( 0.1f );
			Networking.CreateLobby();
		}
	}

	protected override void OnStart()
	{
		Instance = this;

		if ( Networking.IsHost )
		{
			InitBlueTimeHeld = BlueTimeHeld;
			InitRedTimeHeld = RedTimeHeld;
			InitYellowTimeHeld = YellowTimeHeld;
			InitGreenTimeHeld = GreenTimeHeld;

			var mapData = Scene.GetAll<MapData>()?.FirstOrDefault();

			if ( mapData.IsValid() )
			{
				FourTeams = mapData.FourTeams;
			}

			var lobbySettings = LobbySettings.Load();

			if ( LoadLobbySettings && lobbySettings is not null )
			{
				ClassicModels = lobbySettings.ClassicModels;
			}

			foreach ( var prop in Scene.GetAll<Prop>() )
			{
				if ( prop.Components.TryGet<FortwarsProp>( out var p ) )
					return;

				prop.Network.SetOwnerTransfer( OwnerTransfer.Takeover );

				if ( prop.Components.TryGet<Rigidbody>( out var rb ) )
				{
					var propHelper = prop.Components.Create<FortwarsProp>();
					propHelper.Prop = prop;
					propHelper.Rigidbody = rb;
				}
			}
		}
	}

	public void GameLoop()
	{
		switch ( State )
		{
			case GameState.Waiting:
				if ( Scene.GetAll<PlayerController>().Count() >= PlayerToStart && StateSwitch > 5 )
				{
					Scene.Dispatch( new OnBuildMode() );
					State = GameState.BuildMode;
				}
				break;

			case GameState.BuildMode:
				if ( StateSwitch > BuildTime )
				{
					Scene.Dispatch( new OnFightMode() );
					State = GameState.FightMode;
				}
				break;

			case GameState.FightMode:
				CheckForWinningTeam();

				if ( GetWinningTeam() == Team.None && StateSwitch > FightTime )
				{
					Scene.Dispatch( new OnGameOvertimeBuild() );
					State = GameState.OvertimeBuild;
				}
				break;
			case GameState.OvertimeBuild:
				if ( StateSwitch > BuildTime )
				{
					Scene.Dispatch( new OnGameOvertimeFight() );
					State = GameState.OvertimeFight;
				}
				break;
			case GameState.OvertimeFight:
				CheckForWinningTeam();

				if ( GetWinningTeam() == Team.None && StateSwitch > FightTime )
				{
					Overtimes++;

					Log.Info( "Overtime" + Overtimes );

					Scene.Dispatch( new OnGameOvertimeBuild() );
					State = GameState.OvertimeBuild;
				}
				break;

			case GameState.Ended:
				Scene.Dispatch( new OnGameWaiting() );
				State = GameState.Waiting;
				break;
		}
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		GameLoop();
	}

	public void OnActive( Connection connection )
	{
		connection.CanRefreshObjects = true;

		if ( !PlayerPrefab.IsValid() || !SpawnPlayer )
			return;

		var spawns = Scene.GetAllComponents<TeamSpawnPoint>().ToList();
		Transform SpawnTransform = spawns.Count > 0 ? Game.Random.FromList( spawns ).Transform.World : Transform.World;

		var player = PlayerPrefab.Clone( SpawnTransform );

		if ( player.Components.TryGet<PlayerController>( out var p ) )
			p.SetWorld( SpawnTransform );

		if ( player.Components.TryGet<CitizenAnimationHelper>( out var animHelper, FindMode.EnabledInSelfAndChildren ) && animHelper.Target.IsValid() )
		{
			var clothing = new ClothingContainer();
			clothing.Deserialize( connection.GetUserData( "avatar" ) );
			clothing.Apply( animHelper.Target );
		}

		player.NetworkSpawn( connection );

		if ( State != GameState.Waiting && State != GameState.Ended )
		{
			if ( player.Components.TryGet<PlayerController>( out var playerController ) )
			{
				var teams = FourTeams ? new List<Team> { Team.Red, Team.Blue, Team.Yellow, Team.Green } : new List<Team> { Team.Red, Team.Blue };

				var inv = playerController.Inventory;

				if ( !inv.IsValid() )
					return;

				playerController.TeamComponent?.SetTeam( Game.Random.FromList( teams ) );

				playerController.TeleportToTeamSpawnPoint();

				if ( State == GameState.BuildMode || State == GameState.OvertimeBuild )
				{
					//inv.OpenClassSelect();
					inv.AddItem( ResourceLibrary.GetAll<WeaponData>().FirstOrDefault( x => x.ResourceName == "propgun" ) );
					inv.AddItem( ResourceLibrary.GetAll<WeaponData>().FirstOrDefault( x => x.ResourceName == "physgun" ) );
				}
				else if ( State == GameState.FightMode || State == GameState.OvertimeFight )
				{
					inv.OpenClassSelect();

					playerController.Inventory.AddItem( ResourceLibrary.GetAll<WeaponData>().FirstOrDefault( x => x.ResourceName == "gravgun" ) );

					if ( inv.SelectedClass is not null )
						inv.AddItem( inv.SelectedClass.WeaponData );
				}
			}
		}

		Scene.Dispatch( new OnPlayerJoin() );
	}

	public void SetTeams()
	{
		var players = Scene.GetAllComponents<TeamComponent>().ToList();
		var teams = FourTeams ? new List<Team> { Team.Red, Team.Blue, Team.Yellow, Team.Green } : new List<Team> { Team.Red, Team.Blue };

		players = players.OrderBy( x => Game.Random.Next() ).ToList();

		for ( int i = 0; i < players.Count; i++ )
		{
			players[i].SetTeam( teams[i % teams.Count] );
		}

		Scene.GetAll<PlayerController>().ToList().ForEach( x => x.TeleportToTeamSpawnPoint() );
	}

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
			x.AddItem( ResourceLibrary.GetAll<WeaponData>().FirstOrDefault( x => x.ResourceName == "physgun" ) );
			x.OpenClassSelect();
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
			x.ClearAll();
			x.AddItem( ResourceLibrary.GetAll<WeaponData>().FirstOrDefault( x => x.ResourceName == "gravgun" ) );

			if ( x.SelectedClass is not null )
			{
				x.AddItem( x.SelectedClass.WeaponData );
			}
		} );

		var text = Game.Random.FromList( FightModePopups );

		BroadcastChangeState( GameState.FightMode );

		PopupHolder.BroadcastPopup( text, 5 );
	}

	[After<OnFightMode>, After<OnGameOvertimeFight>]
	void IGameEventHandler<OnGameEnd>.OnGameEvent( OnGameEnd eventArgs )
	{
		StateSwitch = 0;
		Log.Info( "Game Ended" );

		var winningTeam = GetWinningTeam();

		OnTeamWon( GetWinningTeam() );

		Scene.GetAll<Inventory>()?.ToList()?.ForEach( x => x.ClearAll() );

		Scene.GetAll<PlayerController>()?.ToList()?.ForEach( x => x.ResetStats() );

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
				x.AddItem( x.SelectedClass.WeaponData );
		} );

		BroadcastChangeState( GameState.OvertimeFight );

		PopupHolder.BroadcastPopup( "Get ready for overtime, fight now!", 5 );
	}

	[Broadcast]
	public void ResetPlayers()
	{
		Scene.Dispatch( new PlayerReset() );
	}

	[Authority]
	public void SubtractTimeHeld( Team team, float time )
	{
		switch ( team )
		{
			case Team.Red:
				RedTimeHeld -= time;
				break;
			case Team.Blue:
				BlueTimeHeld -= time;
				break;
			case Team.Yellow:
				YellowTimeHeld -= time;
				break;
			case Team.Green:
				GreenTimeHeld -= time;
				break;
		}
	}

	public void CheckForWinningTeam()
	{
		var teams = new Dictionary<Team, float>
		{
			{ Team.Red, RedTimeHeld },
			{ Team.Blue, BlueTimeHeld },
			{ Team.Yellow, YellowTimeHeld },
			{ Team.Green, GreenTimeHeld }
		};

		var max = Math.Round( teams.Min( x => x.Value ), 1 );

		if ( teams.Any( x => x.Value <= 0 ) )
		{
			Scene.Dispatch( new OnGameEnd() );
			State = GameState.Ended;
		}
	}

	public Team GetWinningTeam()
	{
		var teams = new Dictionary<Team, float>
		{
			{ Team.Red, RedTimeHeld },
			{ Team.Blue, BlueTimeHeld },
			{ Team.Yellow, YellowTimeHeld },
			{ Team.Green, GreenTimeHeld }
		};

		var min = teams.Min( x => x.Value );

		if ( Math.Round( min, 1 ) <= 0 )
		{
			return teams.FirstOrDefault( x => x.Value == min ).Key;
		}

		return Team.None;
	}

	[Authority]
	public void AddKill()
	{
		TotalKills++;
	}

	[Broadcast]
	public void BroadcastChangeState( GameState state )
	{
		Scene.Dispatch( new OnRoundSwitch( state ) );
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

	[Button( "Save Lobby Settings" ), Category( "Lobby Settings" )]
	public void SaveLobbySettings()
	{
		LobbySettings.Save( LobbySettings );

		Log.Info( $"Saved Lobby Settings as {FileSystem.Data?.ReadAllText( "lobbysettings.json" )}" );

		Log.Info( $"Loaded Lobby Settings as {JsonSerializer.Serialize( LobbySettings.Load() )}" );
	}

	[ConCmd( "skip_wait" )]
	public static void SkipWait()
	{
		Instance?.Scene.Dispatch( new OnBuildMode() );
		Instance.State = GameState.BuildMode;
	}
}

public sealed class MapLoadingSystem : GameObjectSystem<MapLoadingSystem>
{
	public MapLoadingSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.SceneLoaded, 1, OnSceneLoad, "OnSceneLoad" );
	}

	void OnSceneLoad()
	{
		if ( Scene.GetAll<GameSystem>().Count() > 0 || Scene.IsEditor || Scene.GetAll<MainMenu>().Count() > 0 )
			return;

		var slo = new SceneLoadOptions();
		slo.SetScene( "scenes/fortwarsmain.scene" );
		slo.IsAdditive = true;
		Scene.Load( slo );
	}
}

[Description( "Lets you set data for the map" )]
public sealed class MapData : Component
{
	[Property] public bool FourTeams { get; set; } = false;
}

[GameResource( "Map Info", "mapinfo", "Info about the map", Icon ="explore" )]
public sealed class MapInfo : GameResource
{
	public string MapName { get; set; }
	public string MapDescription { get; set; }
	public bool FourTeams { get; set; }
	public SceneFile Scene { get; set; }
	public Texture Thumb { get; set; }
	public string Author { get; set; }
	public bool Hidden { get; set; } = false;
}
