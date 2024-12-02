public sealed class LocalPlayerDresser : Component
{
	[RequireComponent] SkinnedModelRenderer model { get; set; }
	[Property] public Model MungusModel { get; set; }

	protected override void OnEnabled()
	{
		if ( Connection.Local.SteamId == 76561198028633995 )
		{
			model.Model = MungusModel;
			return;
		}
		
		var clothes = ClothingContainer.CreateFromLocalUser();
		clothes.Apply( model );
	}
}
