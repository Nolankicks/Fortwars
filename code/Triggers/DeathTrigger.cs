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

		if ( other.GameObject.Components.TryGet<DroppedFlag>( out var flag ) )
		{
			flag.ResetPos();
		}

		if ( other.GameObject.Components.TryGet<Flag>( out var flagComponent, FindMode.EverythingInSelfAndParent ) )
		{
			var player = other.GameObject.Components.Get<FWPlayerController>( FindMode.EverythingInSelfAndParent );

			if ( !player.IsValid() || (!player?.Inventory.IsValid() ?? true) || (!player?.TeamComponent.IsValid() ?? true) )
				return;

			var team = player.TeamComponent.Team;

			player.Inventory.RemoveItem( flagComponent.GameObject, true );

			RoundComponent.SpawnNewFlag( team );
		}
	}
}
