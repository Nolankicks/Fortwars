public sealed class WorldEmote : Component
{
	protected override void OnStart()
	{
		base.OnStart();
		Invoke( 5, () =>
		{
			DestroyGameObject();
		} );
	}
}
