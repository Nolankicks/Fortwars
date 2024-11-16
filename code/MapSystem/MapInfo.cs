
[GameResource( "Map Info", "mapinfo", "Info about the map", Icon = "explore" )]
public sealed class MapInfo : GameResource
{
	public string MapName { get; set; }
	public string MapDescription { get; set; }
	public bool FourTeams { get; set; }
	public SceneFile Scene { get; set; }
	public Texture Thumb { get; set; }
	public string Author { get; set; }
	public string PackageIndent { get; set; }
	public bool Hidden { get; set; } = false;
	public int Order { get; set; } = 0;

	public GameModeType AvailableGamemodes { get; set; }
}
