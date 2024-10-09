using System.Text.Json;
using Sandbox;

public sealed class LobbySettings
{
	public bool ClassicModels { get; set; } = false;

	public static void Save( LobbySettings settings )
	{
		var json = JsonSerializer.Serialize( settings );

		FileSystem.Data?.WriteAllText( "lobbysettings.json", json );
	}

	public static LobbySettings Load()
	{
		if ( !FileSystem.Data?.FileExists( "lobbysettings.json" ) ?? false )
			return new LobbySettings();

		var json = FileSystem.Data.ReadAllText( "lobbysettings.json" );

		return JsonSerializer.Deserialize<LobbySettings>( json );
	}
}
