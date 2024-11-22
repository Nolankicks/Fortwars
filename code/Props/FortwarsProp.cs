using Sandbox.Events;
using System;

public sealed class FortwarsProp : Component, Component.ICollisionListener, Component.IDamageable
{
	[RequireComponent, Sync] public Rigidbody Rigidbody { get; set; }
	[Property, Sync] public bool Invincible { get; set; } = false;
	[Property, Sync] public float CollisionThreshold { get; set; } = 1300;
	[Property, Sync] public int Divisor { get; set; } = 20;
	[Property, Sync] public Team Team { get; set; } = Team.None;
	[Property, Sync] public FWPlayerController Grabber { get; set; }
	[Property, Sync] public bool CanKill { get; set; } = true;
	[Property, Sync] public float Health { get; set; } = 100;

	[Property, Sync] public PropResource Resource { get; set; }

	[RequireComponent] ModelRenderer Renderer { get; set; }
	[RequireComponent] ModelCollider Collider { get; set; }



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

				var cam = Scene.Camera;
				
				var text = GameObject.Clone( ResourceLibrary.Get<PrefabFile>( "prefabs/effects/textparticle.prefab" ) );
				text.WorldPosition = other.Contact.Point + other.Contact.Normal * 10;

				if ( cam.IsValid() )
					text.WorldRotation = Rotation.LookAt( -cam.WorldRotation.Forward );

				if ( text.Components.TryGet<ParticleTextRenderer>( out var textRenderer ) )
				{
					textRenderer.Text = new TextRendering.Scope( ((int)dmg).ToString(), Color.White, 24 );
				}

				Sound.Play( "hitmarker" );
			}
		}
	}

	public void SetupObject( PropResource prop, Team team )
	{
		Resource = prop;

		if ( !prop.PrefabOverride.IsValid() )
		{
			Health = 100;
			Renderer.Model = prop.Model;
			Collider.Model = prop.Model;
			CanKill = false;
			SetStaticBodyType( Rigidbody );
		}
		Team = team;

	}

	protected override void OnStart()
	{
		if ( Networking.IsHost )
			Network.SetOwnerTransfer( OwnerTransfer.Takeover );

		//if ( IsProxy && !Invincible && Rigidbody.IsValid() && Rigidbody.PhysicsBody.IsValid() )
		//{
		if ( Rigidbody.IsValid() && !Tags.Has( FW.Tags.Rollermine ) && Rigidbody.PhysicsBody.IsValid() )
			Rigidbody.PhysicsBody.BodyType = PhysicsBodyType.Static;
		//}
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


	[Broadcast]
	public void SetStaticBodyType( Rigidbody rb )
	{
		if ( rb.IsValid() && rb.PhysicsBody.IsValid() )
			rb.PhysicsBody.BodyType = PhysicsBodyType.Static;
	}

	void IDamageable.OnDamage( in Sandbox.DamageInfo damage )
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

	[ActionGraphNode( "Destroy All Props" )]
	public static void DestroyAllProps()
	{
		foreach ( var prop in Game.ActiveScene.GetAll<FortwarsProp>().Where( x => !x.Invincible ) )
		{
			prop.DestroyProp();
		}
	}
}
