using Sandbox.Events;
using Sandbox.Utility;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public record OnBuildMode() : IGameEvent;
public record OnFightMode() : IGameEvent;
public record OnGameWaiting() : IGameEvent;
public record OnGameOvertimeBuild() : IGameEvent;
public record OnGameOvertimeFight() : IGameEvent;
public record OnGameEnd() : IGameEvent;
public record OnRoundSwitch( GameSystem.GameState state ) : IGameEvent;

public enum GameModeType
{
	Classic,
	RollermineWars
}

public sealed partial class GameSystem : Component
{
	[Property, Category( "Refrences" )] public GameObject PlayerPrefab { get; set; }
	[Property, Category( "Game Settings" )] public bool LoadLobbySettings { get; set; } = true;
	[Property, Category( "Game Settings" )] public bool StartServer { get; set; } = true;
	[Property, Category( "Game Settings" )] public bool SpawnPlayer { get; set; } = true;
	[Property, Category( "Game Settings" )] public float BuildTime { get; set; } = 300;
	[Property, Category( "Game Settings" )] public float FightTime { get; set; } = 300;
	[Property, Category( "Game Settings" ), Sync] public bool ClassicModels { get; set; } = false;
	[Property, Sync, Category( "Game Settings" )] public bool FourTeams { get; set; } = false;

	public float CurrentTime { get; set; }

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


	[Sync] public float InitBlueTimeHeld { get; set; } = 5;
	[Sync] public float InitRedTimeHeld { get; set; } = 5;
	[Sync] public float InitYellowTimeHeld { get; set; } = 5;
	[Sync] public float InitGreenTimeHeld { get; set; } = 5;

	public IEnumerable<FortwarsProp> RedProps => Scene?.GetAll<FortwarsProp>().Where( x => x.Team == Team.Red );
	public IEnumerable<FortwarsProp> BlueProps => Scene?.GetAll<FortwarsProp>().Where( x => x.Team == Team.Blue );
	public IEnumerable<FortwarsProp> YellowProps => Scene?.GetAll<FortwarsProp>().Where( x => x.Team == Team.Yellow );
	public IEnumerable<FortwarsProp> GreenProps => Scene?.GetAll<FortwarsProp>().Where( x => x.Team == Team.Green );

	public bool IsPlaying => State == GameState.BuildMode || State == GameState.FightMode || State == GameState.OvertimeBuild || State == GameState.OvertimeFight;

	[Property, Sync, Category( "Game Mode" )] public GameModeResource CurrentGameMode { get; set; }
	[Property, Sync, Category( "Game Mode" )] public GameModeType CurrentGameModeType { get; set; }
	[Property, Category( "Game Mode" )] public bool LoadGameData { get; set; } = true;
	public static GameModeResource SavedGameMode { get; set; }
	[Sync] public GameMode CurrentGameModeComponent { get; set; }

	protected override async Task OnLoad()
	{
		if ( Networking.IsHost && !Networking.IsActive && StartServer && !Scene.IsEditor )
		{
			LoadingScreen.Title = "Creating Lobby...";
			await Task.DelaySeconds( 0.1f );
			Networking.CreateLobby();
		}
	}

	protected override void OnAwake()
	{
		Instance = this;

		if ( !Networking.IsHost )
			return;

		if ( LoadGameData && SavedGameMode is not null )
		{
			CurrentGameMode = SavedGameMode;
		}
		else if ( LoadGameData && SavedGameMode is null )
		{
			Log.Warning( $"GameMode: {SavedGameMode} not found" );
		}

		var forcer = Scene.GetAll<GameModeForcer>()?.FirstOrDefault();

		if ( forcer.IsValid() )
		{
			var gameMode = GetGameModeType( forcer.GameMode );

			Log.Info( $"Forcing Game Mode to {gameMode}" );

			if ( gameMode is not null )
				CurrentGameMode = gameMode;
		}

		if ( CurrentGameMode is not null )
		{
			var mode = CurrentGameMode.Prefab.Clone();

			if ( mode.Components.TryGet<GameMode>( out var gm ) )
			{
				gm.GameSystem = this;

				CurrentGameModeComponent = gm;
			}

			mode.NetworkSpawn( null );

			CurrentGameModeType = CurrentGameMode.Type;
		}
	}

	protected override void OnStart()
	{
		if ( IsProxy )
			return;
		
		Scene.GetAll<GameModeObject>()?.Where( x => x.Type != CurrentGameModeType )?.ToList()?.ForEach( x => x?.GameObject?.Destroy() );

		if ( Networking.IsHost )
		{
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

	[Authority]
	public void AddKill()
	{
		TotalKills++;
	}

	/// <summary> Ugly I know </summary>
	public GameModeResource GetGameModeType( GameModeType type )
	{
		return ResourceLibrary.GetAll<GameModeResource>()?.FirstOrDefault( x => x.Type == type );
	}

	public static void SetGameMode( GameModeResource mode )
	{
		SavedGameMode = mode;

		Log.Info( $"Set GameMode as {mode}" );
	}

	
	[Button( "Save Lobby Settings" ), Feature( "Lobby Settings" )]
	public void SaveLobbySettings()
	{
		LobbySettings.Save( LobbySettings );

		Log.Info( $"Saved Lobby Settings as {FileSystem.Data?.ReadAllText( "lobbysettings.json" )}" );

		Log.Info( $"Loaded Lobby Settings as {JsonSerializer.Serialize( LobbySettings.Load() )}" );
	}
}
