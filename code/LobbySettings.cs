using System.Text.Json;
using Sandbox;

public sealed class LobbySettings
{
	public int BuildTime { get; set; } = 300;
	public int FightTime { get; set; } = 300;
	public bool ClassicModels { get; set; } = false;

	public static void Save( LobbySettings settings )
	{
		var json = JsonSerializer.Serialize( settings );

		FileSystem.Data?.WriteAllText( "lobbysettings.json", json );
	}

	public static LobbySettings Load()
	{
		if ( !FileSystem.Data.FileExists( "lobbysettings.json" ) )
			return new LobbySettings();

		var json = FileSystem.Data.ReadAllText( "lobbysettings.json" );

		return JsonSerializer.Deserialize<LobbySettings>( json );
	}
}
