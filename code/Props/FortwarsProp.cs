using System;
using Sandbox;
using Sandbox.Events;

public sealed class FortwarsProp : Component, Component.ICollisionListener, IGameEventHandler<OnGameEnd>, Component.IDamageable
{
	[Property, Sync] public Rigidbody Rigidbody { get; set; }
	[Property, Sync] public bool Invincible { get; set; } = false;
	[Property, Sync] public float CollisionThreshold { get; set; } = 1300;
	[Property, Sync] public int Divisor { get; set; } = 20;
	[Property, Sync] public Team Team { get; set; }
	[Property, Sync] public FWPlayerController Grabber { get; set; }
	[Property, Sync] public bool CanKill { get; set; } = true;
	[Property, Sync] public float Health { get; set; } = 100;

	public void OnCollisionStart( Collision other )
	{
		if ( IsProxy || !Rigidbody.IsValid() ) return;

		var speed = Rigidbody.Velocity.Length;
		var otherSpeed = other.Other.Body.Velocity.Length;
		if ( otherSpeed > speed ) speed = otherSpeed;

		if ( other.Other.GameObject.Root.Components.TryGet<Gib>( out var gib, FindMode.EnabledInSelfAndChildren ) || !CanKill )
			return;

		if ( speed >= CollisionThreshold )
		{
			var dmg = speed / Divisor;

			if ( !Invincible )
				Damage( dmg );

			if ( other.Other.GameObject?.Root?.Components?.TryGet<HealthComponent>( out var player ) == true )
			{
				if ( player?.Network.OwnerId == Grabber?.Network.OwnerId && player.Network.OwnerId != Guid.Empty && Grabber.Network.OwnerId != Guid.Empty && Grabber.IsValid() )
					return;

				player?.TakeDamage( null, (int)dmg, WorldPosition );
			}
		}
	}

	protected override void OnStart()
	{
		if ( Networking.IsHost )
			Network.SetOwnerTransfer( OwnerTransfer.Takeover );

		if ( IsProxy && !Invincible && Rigidbody.IsValid() && Rigidbody.PhysicsBody.IsValid() )
		{
			Rigidbody.PhysicsBody.BodyType = PhysicsBodyType.Static;
		}
	}

	[Broadcast]
	public void Damage( float amount )
	{
		if ( Invincible )
			return;
		
		if ( IsProxy ) return;

		Health -= amount;
		if ( Health <= 0 )
		{
			//Create a new prop
			var propGo = new GameObject();
			propGo.WorldTransform = WorldTransform;

			//Create a prop component
			var prop = propGo.Components.Create<Prop>();
			prop.Model = Components.Get<ModelRenderer>()?.Model;

			//Spawn our prop
			propGo.NetworkSpawn();

			//Kill it :(
			var gibs = prop.CreateGibs();
			prop.GameObject.Destroy();

			gibs?.ForEach( x => x.GameObject.NetworkSpawn() );

			GameObject.Destroy();
		}
	}

	void IGameEventHandler<OnGameEnd>.OnGameEvent( OnGameEnd eventArgs )
	{
		DestroyProp();
	}

	void IDamageable.OnDamage(in Sandbox.DamageInfo damage)
	{
		Damage( damage.Damage );
	}

	[Authority]
	public void DestroyProp()
	{
		if ( Invincible )
			return;

		GameObject.Destroy();
	}

	[Authority]
	public void SetGrabber( FWPlayerController player )
	{
		Grabber = player;
	}
}
