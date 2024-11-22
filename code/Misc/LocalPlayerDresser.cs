public sealed class LocalPlayerDresser : Component
{
	[RequireComponent] SkinnedModelRenderer model { get; set; }
	protected override void OnEnabled()
	{
		var clothes = ClothingContainer.CreateFromLocalUser();
		clothes.Apply( model );
	}
}
