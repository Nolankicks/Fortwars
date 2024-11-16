using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

[Flags]
public enum GameModeType
{
	Classic = 1,
	RollermineWars = 2,
	Deathmatch = 4,
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

	[Property, Sync, Category( "Game Mode" )] public GameModeResource CurrentGameMode { get; set; }
	[Property, Sync, Category( "Game Mode" )] public GameModeType CurrentGameModeType { get; set; }
	[Property, Category( "Game Mode" )] public bool LoadGameData { get; set; } = true;
	public static GameModeResource SavedGameMode { get; set; }
	[ConVar( Name = "fw_server_gamemode", Help = "A gamemode override for dedicated servers. Gamemodes: classic, deathmatch" )] public static string ServerGameMode { get; set; } = "classic";
	[Sync] public GameMode CurrentGameModeComponent { get; set; }

	public enum GameStates
	{
		S_WAITING,
		S_ACTIVE,
		S_END
	}

	[Property, Sync, ReadOnly] public GameStates GameState { get; set; } = GameStates.S_WAITING;

	protected override async Task OnLoad()
	{
		if ( Networking.IsHost && !Networking.IsActive && StartServer && !Scene.IsEditor )
		{
			LoadingScreen.Title = "Creating Lobby...";
			await Task.DelaySeconds( 0.1f );

			var lobbyConfig = new Sandbox.Network.LobbyConfig();

			var lobbySettings = LobbySettings.Load();

			if ( LoadLobbySettings && lobbySettings is not null )
			{
				//Don't set the privacy when in the editor. Use the editors's privacy settings
				if ( !Game.IsEditor )
				{
					lobbyConfig.Privacy = lobbySettings.Privacy switch
					{
						LobbySettingsPanel.LobbyPrivacy.Public => Sandbox.Network.LobbyPrivacy.Public,
						LobbySettingsPanel.LobbyPrivacy.FriendsOnly => Sandbox.Network.LobbyPrivacy.FriendsOnly,
						LobbySettingsPanel.LobbyPrivacy.Private => Sandbox.Network.LobbyPrivacy.Private,
						_ => Sandbox.Network.LobbyPrivacy.Public
					};
				}

				lobbyConfig.MaxPlayers = lobbySettings.MaxPlayers;
			}

			Log.Info( $"Creating Lobby with {lobbyConfig.MaxPlayers} players and {lobbyConfig.Privacy} privacy" );

			Networking.CreateLobby( lobbyConfig );
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

		if ( Application.IsHeadless )
		{
			CurrentGameMode = ResourceLibrary.Get<GameModeResource>( $"gamemodes/{ServerGameMode}.mode" );
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

		Scene.GetAll<GameModeObject>()?.Where( x => !x.Type.HasFlag( CurrentGameModeType ) )?.ToList()?.ForEach( x => x.GameObject?.Destroy() );

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

	protected override void OnFixedUpdate()
	{
		if ( IsProxy )
			return;

		if ( GameState == GameStates.S_WAITING && CanStartGame() )
		{
			GameState = GameStates.S_ACTIVE;
			var gMode = Scene.GetAllComponents<GameMode>()?.FirstOrDefault();
			gMode.StartGame();
			StateSwitch = 0;
		}
		else if ( GameState == GameStates.S_END && StateSwitch > 5 )
		{
			GameState = GameStates.S_WAITING;

			StateSwitch = 0;
		}
	}

	[Button( "Save Lobby Settings" ), Feature( "Lobby Settings" )]
	public void SaveLobbySettings()
	{
		LobbySettings.Save( LobbySettings );

		Log.Info( $"Saved Lobby Settings as {FileSystem.Data?.ReadAllText( "lobbysettings.json" )}" );

		Log.Info( $"Loaded Lobby Settings as {JsonSerializer.Serialize( LobbySettings.Load() )}" );
	}

	public bool CanStartGame()
	{
		return Scene.GetAll<FWPlayerController>().Count() >= PlayerToStart && StateSwitch > 5;
	}
}
