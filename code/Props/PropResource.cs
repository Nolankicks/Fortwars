[GameResource( "FW Prop", "prop", "Prop definition for FW", Icon = "inventory_2" )]
public class PropResource : GameResource
{
	public string DisplayName { get; set; }

	public bool Hidden { get; set; } = false;

	public Model Model { get; set; }

	public int Cost { get; set; }

	public int Max { get; set; }

	[ImageAssetPath] public string Icon { get; set; }

	public GameObject PrefabOverride { get; set; }
}
