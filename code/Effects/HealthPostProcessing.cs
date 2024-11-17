public sealed class HealthPostProcessing : Component
{
	[Property] HealthComponent Health { get; set; }

	private Vignette vignette { get; set; }

	protected override void OnStart()
	{
		vignette = Scene.GetAllComponents<Vignette>().FirstOrDefault();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		vignette.Color = vignette.Color.WithAlpha( vignette.Color.a.LerpTo( GetIntensity(), Time.Delta * 10.0f ) );
	}

	float GetIntensity()
	{
		var percent = 1.0f - ((float)Health.Health / (float)Health.MaxHealth);

		if ( percent < 0.5f )
			return 0.0f;

		percent -= 0.5f;

		return percent * percent;
	}
}
