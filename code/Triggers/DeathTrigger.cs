using Sandbox;
using Sandbox.Events;

public sealed class DeathTrigger : Component, Component.ITriggerListener
{
	[Property] public bool TeleportBall { get; set; } = false;

	public void OnTriggerEnter( Collider other )
	{
		if ( other.GameObject.Components.TryGet<FWPlayerController>( out var player, FindMode.EverythingInSelfAndParent ) )
		{
			var inv = player?.Inventory;

			if ( player.IsValid() && inv.IsValid() && player.TeamComponent.IsValid() )
			{
				var team = player.TeamComponent.Team == Team.Red ? Team.Blue : Team.Red;

				var flagComponent = player.Inventory.Components.Get<Flag>( FindMode.EverythingInSelfAndDescendants );

				if ( flagComponent.IsValid() )
				{
					flagComponent.SetSpawnNewFlag( false );

					player.Inventory.RemoveItem( flagComponent.GameObject, true );

					RoundComponent.SpawnNewFlag( team );
				}
			}
		}

		if ( other.GameObject.Components.TryGet<HealthComponent>( out var health, FindMode.EverythingInSelfAndParent ) )
		{
			health.TakeDamage( null, health.Health, spawnFlag: false );
		}

		if ( other.GameObject.Components.TryGet<RollerMine>( out var mine ) && TeleportBall )
		{
			mine.ResetPosition();
		}

		if ( other.GameObject.Components.TryGet<DroppedFlag>( out var flag ) )
		{
			flag.ResetPos();
		}
	}
}
