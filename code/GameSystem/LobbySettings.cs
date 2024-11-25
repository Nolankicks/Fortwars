using System.Text.Json;
using Sandbox;
using Sandbox.Network;

public sealed class LobbySettings
{
	public bool ClassicModels { get; set; } = true;
	public int WoodProps { get; set; } = 60;
	public int MetalProps { get; set; } = 30;
	public int SteelProps { get; set; } = 15;
	public LobbySettingsPanel.LobbyPrivacy Privacy { get; set; } = LobbySettingsPanel.LobbyPrivacy.Public;
	public int MaxPlayers { get; set; } = 64;
	public int PlayersToStart { get; set; } = 1;

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
