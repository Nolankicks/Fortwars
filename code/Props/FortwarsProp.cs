public enum PropLevel
{
	Base,
	Metal,
	Steel
}

public sealed class FortwarsProp : Component, Component.IDamageable
{
	[RequireComponent, Sync] public Rigidbody Rigidbody { get; set; }
	[Property, Sync] public bool Invincible { get; set; } = false;
	[Property, Sync] public float CollisionThreshold { get; set; } = 1300;
	[Property, Sync] public int Divisor { get; set; } = 20;
	[Property, Sync] public Team Team { get; set; } = Team.None;
	[Property, Sync] public FWPlayerController Grabber { get; set; }
	[Property, Sync] public bool CanKill { get; set; } = true;
	[Property, Sync] public float Health { get; set; } = 100;
	[Property, Sync] public float MaxHealth { get; set; } = 100;

	[Property, Sync] public PropResource Resource { get; set; }

	[RequireComponent] ModelRenderer Renderer { get; set; }
	[RequireComponent] ModelCollider Collider { get; set; }

	[Sync] public bool IsBuilding { get; set; } = false;
	[Sync] public string Builder { get; set; } = "";
	[Sync] public bool IsGrabbed { get; set; } = false;
	[Sync] public PropLevel Level { get; set; } = PropLevel.Base;
	[Sync] public bool IsHovered { get; set; } = false;

	/*public void OnCollisionStart( Collision other )
	{
		if ( IsProxy || !Rigidbody.IsValid() ) return;

		if ( other.Other.GameObject.Components.TryGet<Collider>( out var component ) )
		{
			Log.Info( component.IsTrigger );
		}

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
	}*/

	public void SetupObject( PropResource prop, Team team, PropLevel level = PropLevel.Base, string builder = "" )
	{
		Resource = prop;

		Builder = builder;

		Level = level;

		Physgun.AddTag( GameObject, "prop" );

		var newHealth = level switch
		{
			PropLevel.Base => prop.BaseHealth,
			PropLevel.Metal => prop.MetalHealth,
			PropLevel.Steel => prop.SteelHealth,
			_ => prop.BaseHealth
		};

		var newModel = level switch
		{
			PropLevel.Base => prop.BaseModel,
			PropLevel.Metal => prop.MetalModel,
			PropLevel.Steel => prop.SteelModel,
			_ => prop.BaseModel
		};

		if ( !prop.PrefabOverride.IsValid() )
		{
			Health = newHealth;
			Renderer.Model = newModel;
			Collider.Model = newModel;
			CanKill = false;
		}

		Team = team;

		MaxHealth = Health;

		Renderer.MaterialGroup = team switch
		{
			Team.Red => "red",
			Team.Blue => "default",
			_ => "default"
		};
	}

	protected override void OnStart()
	{
		if ( !IsProxy )
		{
			Network.SetOwnerTransfer( OwnerTransfer.Takeover );
			Network.SetOrphanedMode( NetworkOrphaned.ClearOwner );

			MaxHealth = Health;
		}
		else if ( Components.TryGet<HighlightOutline>( out var highlightOutline ) )
		{
			highlightOutline.Destroy();
			GameObject.Network.Refresh();
		}
	}

	[Rpc.Broadcast]
	public void Damage( float amount )
	{
		if ( Invincible || IsProxy )
			return;

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

			DestructionEffects();

			GameObject.Destroy();
		}
	}

	[Rpc.Broadcast]
	public void DestructionEffects()
	{
		if ( !GameObject.IsValid() )
			return;

		var particles = GameObject.Clone( "prefabs/effects/propdestroy.prefab" );
		particles.WorldPosition = WorldPosition;
		particles.WorldRotation = WorldRotation;

		var renderer = particles.Components.Get<SkinnedModelRenderer>( FindMode.InChildren );
		renderer.Model = Renderer.Model;
	}

	[Rpc.Owner]
	public void HealProp( float amount )
	{
		Health += amount;

		if ( Health > MaxHealth )
			Health = MaxHealth;
	}

	void IDamageable.OnDamage( in Sandbox.DamageInfo damage )
	{
		Damage( damage.Damage );
	}

	[Rpc.Owner]
	public void DestroyProp()
	{
		if ( Invincible )
			return;

		GameObject.Destroy();
	}

	[Rpc.Owner]
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
