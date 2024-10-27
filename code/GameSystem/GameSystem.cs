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

public sealed partial class GameSystem : Component, Component.INetworkListener,
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

	public IEnumerable<TeamComponent> BlueTeam => Scene?.GetAll<TeamComponent>().Where( x => x?.Team == Team.Blue );
	public IEnumerable<TeamComponent> RedTeam => Scene?.GetAll<TeamComponent>().Where( x => x?.Team == Team.Red );
	public IEnumerable<TeamComponent> YellowTeam => Scene?.GetAll<TeamComponent>().Where( x => x?.Team == Team.Yellow );
	public IEnumerable<TeamComponent> GreenTeam => Scene?.GetAll<TeamComponent>().Where( x => x?.Team == Team.Green );
	public IEnumerable<TeamComponent> NoneTeam => Scene?.GetAll<TeamComponent>().Where( x => x?.Team == Team.None );

	[Property, Sync, Feature( "Game Data" )] public float BlueTimeHeld { get; set; } = 5;
	[Property, Sync, Feature( "Game Data" )] public float RedTimeHeld { get; set; } = 5;
	[Property, Sync, Feature( "Game Data" )] public float YellowTimeHeld { get; set; } = 5;
	[Property, Sync, Feature( "Game Data" )] public float GreenTimeHeld { get; set; } = 5;
	public static GameSystem Instance { get; set; }

	public bool CountUp => State == GameState.Waiting;

	[Sync] public int Overtimes { get; set; } = 0;

	[Property, Feature( "Lobby Settings" ), InlineEditor] public LobbySettings LobbySettings { get; set; } = new();
	[Property, Sync, Feature( "Lobby Settings" )] public int MaxProps { get; set; } = 50;

	[Property, Feature( "Spawning" )] public Dictionary<string, int> ClassicIndents { get; set; } = new();

	[Sync] public float InitBlueTimeHeld { get; set; } = 5;
	[Sync] public float InitRedTimeHeld { get; set; } = 5;
	[Sync] public float InitYellowTimeHeld { get; set; } = 5;
	[Sync] public float InitGreenTimeHeld { get; set; } = 5;

	public IEnumerable<FortwarsProp> RedProps => Scene?.GetAll<FortwarsProp>().Where( x => x.Team == Team.Red );
	public IEnumerable<FortwarsProp> BlueProps => Scene?.GetAll<FortwarsProp>().Where( x => x.Team == Team.Blue );
	public IEnumerable<FortwarsProp> YellowProps => Scene?.GetAll<FortwarsProp>().Where( x => x.Team == Team.Yellow );
	public IEnumerable<FortwarsProp> GreenProps => Scene?.GetAll<FortwarsProp>().Where( x => x.Team == Team.Green );

	public bool IsPlaying => State == GameState.BuildMode || State == GameState.FightMode || State == GameState.OvertimeBuild || State == GameState.OvertimeFight;

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
		Log.Info( "Game System Started" );

		Instance = this;

		if ( Networking.IsHost )
		{
			//Get this shit loaded if we are a dedicated server
			if ( Application.IsHeadless )
				PlayerToStart = 1;

			InitBlueTimeHeld = BlueTimeHeld;
			InitRedTimeHeld = RedTimeHeld;
			InitYellowTimeHeld = YellowTimeHeld;
			InitGreenTimeHeld = GreenTimeHeld;

			var mapData = Scene.GetAll<MapData>()?.FirstOrDefault();

			//Load our map data from the scene
			if ( mapData.IsValid() )
			{
				FourTeams = mapData.FourTeams;
			}

			//Load our lobby settings from the file
			var lobbySettings = LobbySettings.Load();

			if ( LoadLobbySettings && lobbySettings is not null )
			{
				ClassicModels = lobbySettings?.ClassicModels ?? true;
				MaxProps = lobbySettings?.MaxProps ?? 50;
			}

			//Create our prop helpers
			foreach ( var prop in Scene.GetAll<Prop>() )
			{
				if ( prop.Components.TryGet<FortwarsProp>( out var p ) )
					return;

				prop.Network.SetOwnerTransfer( OwnerTransfer.Takeover );

				if ( prop.Components.TryGet<Rigidbody>( out var rb ) )
				{
					var propHelper = prop.Components.Create<FortwarsProp>();
					propHelper.Health = prop.Health;
					propHelper.Rigidbody = rb;

					if ( propHelper.Health == 0 )
						propHelper.Invincible = true;

					prop.Break();
				}
			}
		}
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		GameLoop();

		//If we are the dedicated server and all players left, end the game
		if ( Connection.All.Where( x => x != Connection.Local ).Count() == 0 && IsPlaying && Application.IsHeadless )
		{
			State = GameState.Ended;
			Scene.Dispatch( new OnGameEnd() );

			Log.Info( "All players left, ending game." );
		}
	}

	/// <summary> The main game loop </summary>
	public void GameLoop()
	{
		switch ( State )
		{
			case GameState.Waiting:
				//Start the game if we have enough players
				if ( Scene.GetAll<PlayerController>().Count() >= PlayerToStart && StateSwitch > 5 )
				{
					Scene.Dispatch( new OnBuildMode() );
					State = GameState.BuildMode;
				}
				break;

			case GameState.BuildMode:
				//After build time is over, switch to fight mode
				if ( StateSwitch > BuildTime )
				{
					Scene.Dispatch( new OnFightMode() );
					State = GameState.FightMode;
				}
				break;

			case GameState.FightMode:
				//Constantly check for the winning team
				CheckForWinningTeam();

				//If we don't have one by the end, start overtime
				if ( GetWinningTeam() == Team.None && StateSwitch > FightTime )
				{
					Scene.Dispatch( new OnGameOvertimeBuild() );
					State = GameState.OvertimeBuild;
				}
				break;
			//Same as above but for overtime
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

					Log.Info( $"Overtime: {Overtimes}" );

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

	/// <summary> Creates random teams for the players </summary>
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

	[Button( "Save Lobby Settings" ), Feature( "Lobby Settings" )]
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
