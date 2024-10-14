using Sandbox;
using Sandbox.Events;

public sealed class DeathTrigger : Component, Component.ITriggerListener
{
	[Property] public bool TeleportBall { get; set; } = false;

	public void OnTriggerEnter( Collider other )
	{
        Log.Info( $"{other.GameObject} entered the trigger" );

		if ( other.GameObject.Components.TryGet<HealthComponent>( out var health, FindMode.EverythingInSelfAndParent ) )
		{
			health.TakeDamage( null, health.Health );
		}

		if ( other.GameObject.Components.TryGet<RollerMine>( out var mine ) && TeleportBall )
		{
			mine.ResetPosition();
		}
	}
}
