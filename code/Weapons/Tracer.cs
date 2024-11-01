public sealed class Tracer : Component
{
	protected override void OnStart()
	{
		base.OnStart();
		Invoke( 1.0f, DestroyGameObject );
	}

	protected override void OnUpdate()
	{
		WorldPosition += WorldRotation.Forward * 1000.0f * Time.Delta;
	}
}
