using Sandbox;
using Sandbox.Events;

public sealed class FortwarsProp : Component, Component.ICollisionListener, IGameEventHandler<OnGameEnd>
{
	[Property, Sync] public Prop Prop { get; set; }
	[Property, Sync] public Rigidbody Rigidbody { get; set; }
	[Property, Sync] public bool Invincible { get; set; } = false;
	[Property, Sync] public float CollisionThreshold { get; set; } = 1300;
	[Property, Sync] public int Divisor { get; set; } = 20;
	[Property, Sync] public Team Team { get; set; }

	public void OnCollisionStart( Collision other )
	{
		if ( IsProxy || !Rigidbody.IsValid() ) return;

		var speed = Rigidbody.Velocity.Length;
		var otherSpeed = other.Other.Body.Velocity.Length;
		if ( otherSpeed > speed ) speed = otherSpeed;

		if ( other.Other.GameObject.Root.Components.TryGet<Gib>( out var gib, FindMode.EnabledInSelfAndChildren ) )
			return;

		if ( speed >= CollisionThreshold )
		{
			var dmg = speed / Divisor;

			if ( !Invincible )
				Damage( dmg );

			if ( other.Other.GameObject.Root.Components.TryGet<HealthComponent>( out var player ) )
			{
				player.TakeDamage( GameObject, (int)dmg, WorldPosition );
			}
		}
	}

	protected override void OnStart()
	{
		if ( Networking.IsHost )
			Network.SetOwnerTransfer( OwnerTransfer.Takeover );
	}

	[Broadcast]
	public void Damage( float amount )
	{
		if ( Invincible )
			return;

		if ( (Prop?.Health ?? 0) <= 0 ) return;
		if ( IsProxy ) return;

		Prop.Health -= amount;
		if ( Prop.Health <= 0 )
		{
			Prop.Kill();
		}
	}

	void IGameEventHandler<OnGameEnd>.OnGameEvent( OnGameEnd eventArgs )
	{
		DestroyProp();
	}

	[Authority]
	public void DestroyProp()
	{
		if ( Invincible )
			return;

		GameObject.Destroy();
	}
}
