using Sandbox;

public sealed class DamageTrigger : Component, Component.ITriggerListener
{
	[Property] public int Damage { get; set; } = 10;

	public void OnTriggerEnter( Collider other )
	{
		var gs = GameSystem.Instance;

		if ( !gs.IsValid() )
			return;

		if ( gs.State == GameSystem.GameState.Waiting || gs.State == GameSystem.GameState.Ended )
			return;

		if ( other.GameObject.Root.Components.TryGet<HealthComponent>( out var player ) )
		{
			if ( player.IsDead )
				return;

			player.TakeDamage( GameObject, Damage, WorldPosition );
		}
	}
}
