[GameResource( "FW Prop", "prop", "Prop definition for FW", Icon = "inventory_2" )]
public class PropResource : GameResource
{
	public string DisplayName { get; set; }

	public bool Hidden { get; set; } = false;

	public int Cost { get; set; }

	public int Max { get; set; }

	public GameObject PrefabOverride { get; set; }

	[Feature( "Wood" )] public Model BaseModel { get; set; }
	[Feature( "Wood" )] public int BaseHealth { get; set; } = 100;
	[Feature( "Wood" ), ImageAssetPath] public string BaseIcon { get; set; }

	[Feature( "Metal" )] public Model MetalModel { get; set; }
	[Feature( "Metal" )] public int MetalHealth { get; set; } = 200;
	[Feature( "Metal" ), ImageAssetPath] public string MetalIcon { get; set; }

	[Feature( "Steel" )] public Model SteelModel { get; set; }
	[Feature( "Steel" )] public int SteelHealth { get; set; } = 300;
	[Feature( "Steel" ), ImageAssetPath] public string SteelIcon { get; set; }
}
