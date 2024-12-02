public sealed class MapLoadingSystem : GameObjectSystem<MapLoadingSystem>, ISceneStartup
{
	public MapLoadingSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.SceneLoaded, 0, OnSceneLoad, "OnSceneLoad" );
	}

	void OnSceneLoad()
	{
		//Don't load the engine scene if it's already loaded
		if ( Scene.GetAll<GameSystem>().Any() || Scene.IsEditor || Scene.GetAll<MainMenu>().Any() || Scene.GetAll<SceneLoaderBlocker>().Any() )
			return;

		var lobbyConfig = new Sandbox.Network.LobbyConfig();

		var lobbySettings = LobbySettings.Load();

		if ( lobbySettings is not null )
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

		var core = GameObject.Clone( ResourceLibrary.Get<PrefabFile>( "prefabs/core.prefab" ) );
		core.BreakFromPrefab();
	}

	void ISceneStartup.OnHostInitialize()
	{
		if ( !Application.IsHeadless )
			return;

		Log.Info( "Welcome to the Fortwars Dedicated Server!" );
		Log.Info( "" );
		Log.Info( "To load a map, use the 'loadmap' command followed by the map name listed below." );
		Log.Info( "" );
		Log.Info( "Available maps:" );

		foreach ( var map in ResourceLibrary.GetAll<MapInfo>().Where( x => !x.Hidden ) )
		{
			Log.Info( $"- {map.MapName}" );
		}

		Log.Info( "" );
		Log.Info( "To set the map use gamemode use the 'loadgamemode' command followed by the gamemode name." );
		Log.Info( "" );
		Log.Info( "Available gamemodes:" );

		foreach ( var gamemode in ResourceLibrary.GetAll<GameModeResource>().Where( x => !x.Hidden ) )
		{
			Log.Info( $"- {gamemode.ResourceName}" );
		}

		Log.Info( "" );
		Log.Info( "To start the game, use the 'startgame' command." );
	}

	public static MapInfo CurrentMap { get; private set; }
	public static GameModeResource CurrentGamemode { get; private set; }

	[ConCmd( "loadmap" )]
	public static void LoadMap( string mapName )
	{
		if ( !Application.IsHeadless )
		{
			Log.Warning( "This command can only be used on a dedicated server!" );
			return;
		}

		var map = ResourceLibrary.GetAll<MapInfo>().FirstOrDefault( x => x.MapName == mapName );

		if ( map is null )
		{
			Log.Warning( "Map not found!" );
			return;
		}

		Log.Info( $"Map set to {map.MapName}" );

		CurrentMap = map;
	}

	[ConCmd( "loadgamemode" )]
	public static void LoadGamemode( string gamemodeName )
	{
		if ( !Application.IsHeadless )
		{
			Log.Warning( "This command can only be used on a dedicated server!" );
			return;
		}

		var gamemode = ResourceLibrary.GetAll<GameModeResource>().FirstOrDefault( x => x.ResourceName == gamemodeName );

		if ( gamemode is null )
		{
			Log.Warning( "Gamemode not found!" );
			return;
		}

		Log.Info( $"Gamemode set to {gamemode.ResourceName}" );

		CurrentGamemode = gamemode;
	}

	[ConCmd( "startgame" )]
	public static void StartGame()
	{
		if ( !Application.IsHeadless )
		{
			Log.Warning( "This command can only be used on a dedicated server!" );
			return;
		}

		if ( CurrentMap is null )
		{
			Log.Warning( "No map selected! Falling back to default map: fw_easter" );
			CurrentMap = ResourceLibrary.Get<MapInfo>( "mapinfos/easter.mapinfo" );
		}

		if ( CurrentGamemode is null )
		{
			Log.Warning( "No gamemode selected! Falling back to default gamemode: Capture the Flag" );
			CurrentGamemode = ResourceLibrary.Get<GameModeResource>( "gamemodes/ctf.mode" );
		}

		GameSystem.SavedGameMode = CurrentGamemode;

		Game.ActiveScene.Load( CurrentMap.Scene );
	}

	public void LoadAdditiveScene( SceneFile mainScene, SceneFile additiveScene )
	{
		if ( Scene.IsEditor )
			return;

		Scene.Load( mainScene );

		var slo = new SceneLoadOptions();
		slo.IsAdditive = true;
		slo.SetScene( additiveScene );
		
		Scene.Load( slo );
	}
}

public sealed class SceneLoaderBlocker : Component { }
