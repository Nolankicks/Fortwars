using Sandbox;

public sealed class DamageTrigger : Component, Component.ITriggerListener
{
	[Property] public int Damage { get; set; } = 10;

	/// <summary> If true, the trigger will always damage others when the game is in waiting or ended state </summary>
	[Property] public bool DamageAlways { get; set; } = false;

	public void OnTriggerEnter( Collider other )
	{
		var gs = GameSystem.Instance;

		if ( !gs.IsValid() )
			return;

		//TODO: @Nolankicks
		/*if ( (gs.State == GameSystem.GameState.Waiting || gs.State == GameSystem.GameState.Ended) && !DamageAlways )
			return;*/

		if ( other.GameObject.Root.Components.TryGet<HealthComponent>( out var player ) )
		{
			if ( player.IsDead )
				return;

			player.TakeDamage( GameObject, Damage, WorldPosition );
		}
	}
}
