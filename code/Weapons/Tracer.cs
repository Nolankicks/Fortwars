public sealed class Tracer : Component
{
	[Property] public float Speed { get; set; } = 5000.0f;

	protected override void OnStart()
	{
		base.OnStart();
		Invoke( 0.05f, DestroyGameObject );
	}

	protected override void OnUpdate()
	{
		WorldPosition += WorldRotation.Forward * Speed * Time.Delta;
	}
}
