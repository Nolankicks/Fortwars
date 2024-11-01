public sealed class Tracer : Component
{
	private float Speed { get; set; } = 2000.0f;

	protected override void OnStart()
	{
		base.OnStart();
		Invoke( 1.0f, DestroyGameObject );
	}

	protected override void OnUpdate()
	{
		WorldPosition += WorldRotation.Forward * Speed * Time.Delta;
	}
}
