using Sandbox;

[GameResource( "Map Info", "mapinfo", "Info for a map", Icon = "public" )]
public sealed class MapInfo : GameResource
{
	public string MapName { get; set; }
	public Texture Thumb { get; set; }
	public bool FourTeams { get; set; } = false;
	public SceneFile MapScene { get; set; }
}
