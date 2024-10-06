using Sandbox.Citizen;
using Sandbox.Events;

public record WeaponAnimEvent( string anim, bool value ) : IGameEvent;

public class Item : Component, IGameEventHandler<OnItemEquipped>
{
	[Property, Sync, Category( "Base Item" )] public CitizenAnimationHelper.HoldTypes HoldType { get; set; }
	[Property, Category( "Base Item" )] public Model WorldModel { get; set; }
	[Property, Sync, Category( "Base Item" )] public Vector3 Offset { get; set; }
	public int Ammo { get; set; }
	[Property, ShowIf( "UsesAmmo", true ), Category( "Base Item" )] public int MaxAmmo { get; set; } = 30;
	[Property, Category( "Base Item" )] public bool UsesAmmo { get; set; } = true;

	[Authority]
	public void SubtractAmmo( int amount = 1 )
	{
		if ( !UsesAmmo || Ammo <= 0 )
			return;

		Ammo -= amount;
	}

	public bool CanUse()
	{
		return Ammo > 0;
	}

	public void Reload()
	{
		Ammo = MaxAmmo;
	}

	protected override void OnAwake()
	{
		if ( IsProxy )
			return;

		Ammo = MaxAmmo;
	}

	public virtual void OnEquip( OnItemEquipped onItemEquipped ) { }

	void IGameEventHandler<OnItemEquipped>.OnGameEvent( OnItemEquipped eventArgs )
	{
		OnEquip( eventArgs );

		var player = PlayerController.Local;

		if ( IsProxy || !player.IsValid() )
			return;

		player.HoldType = HoldType;

		BroadcastEquip( player );
	}

	[Broadcast]
	public void BroadcastEquip( PlayerController local )
	{
		if ( !local.IsValid() )
			return;

		local.HoldRenderer.Model = WorldModel;
		local.HoldRenderer.LocalPosition = Offset;
	}
}

public sealed class Weapon : Item
{
	[Property] public float FireRate { get; set; } = 0.1f;
	[Property] public float ReloadDelay { get; set; } = 1f;
	[Property] public int Damage { get; set; } = 10;
	TimeSince lastFired { get; set; }
	TimeSince equipTime { get; set; }
	TimeSince reloadTime { get; set; }
	[Property] public float Range { get; set; } = 5000;
	[Property] public float Spread { get; set; } = 0.03f;
	[Property] public int TraceTimes { get; set; } = 1;
	[Property] public SoundEvent FireSound { get; set; }
	[Property] public string AttackAnimName { get; set; } = "b_attack";
	[Property] public string ReloadAnimName { get; set; } = "b_reload";
	[Property] public bool IsShotgun { get; set; } = false;

	public override void OnEquip( OnItemEquipped onItemEquipped )
	{
		if ( IsProxy )
			return;

		equipTime = 0;
		reloadTime = ReloadDelay;
		lastFired = FireRate;
		GameObject.Dispatch( new WeaponAnimEvent( "b_reload", false ) );
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || equipTime < 0.2f )
			return;

		if ( (Input.Pressed( "attack1" ) || Input.Down( "attack1" ) && lastFired > FireRate) && CanUse() && reloadTime > ReloadDelay )
		{
			for ( var i = 0; i < TraceTimes; i++ )
				Shoot();

			var local = PlayerController.Local;

			if ( local.IsValid() )
			{
				local.BroadcastAttack();
			}

			GameObject.Dispatch( new WeaponAnimEvent( AttackAnimName, true ) );

			lastFired = 0;
		}

		if ( Input.Pressed( "reload" ) && reloadTime > ReloadDelay && Ammo != MaxAmmo )
		{
			reloadTime = 0;

			GameObject.Dispatch( new WeaponAnimEvent( "b_reload", true ) );

			GameObject.Dispatch( new WeaponAnimEvent( "b_empty", false ) );

			Invoke( ReloadDelay, () =>
			{
				Reload();
				GameObject.Dispatch( new WeaponAnimEvent( ReloadAnimName, true ) );
			} );
		}

		if ( Ammo <= 0 )
		{
			GameObject.Dispatch( new WeaponAnimEvent( "b_empty", true ) );
		}
	}

	public void Shoot()
	{
		var local = PlayerController.Local;

		var cam = Scene.Camera;

		if ( !local.IsValid() || !cam.IsValid() )
			return;

		var ray = cam.ScreenNormalToRay( 0.5f );

		ray.Forward += Vector3.Random * Spread;

		var tr = Scene.Trace.Ray( ray, Range )
			.IgnoreGameObjectHierarchy( local.GameObject )
			.Run();

		// local.EyeAngles += new Angles( Game.Random.Float( -1, 1 ), Game.Random.Float( -1, 1 ), 0 );

		if ( !tr.Hit )
		{
			BroadcastFireEffects( WorldPosition, Vector3.Zero, Vector3.Zero );
			return;
		}

		if ( tr.GameObject.Components.TryGet<TeamComponent>( out var team, FindMode.EverythingInSelfAndParent ) && local.TeamComponent.IsValid()
			&& local.TeamComponent.IsFriendly( team ) )
		{
			BroadcastFireEffects( WorldPosition, Vector3.Zero, Vector3.Zero );
			return;
		}

		if ( tr.GameObject.Components.TryGet<HealthComponent>( out var health, FindMode.EverythingInSelfAndParent ) )
		{
			health.TakeDamage( local.GameObject, Damage, tr.EndPosition, tr.Normal );

			SpawnParticleEffect( Cloud.ParticleSystem( "bolt.impactflesh" ), tr.EndPosition );
		}

		var damage = new DamageInfo( Damage, GameObject, GameObject, tr.Hitbox );
		damage.Position = tr.HitPosition;
		damage.Shape = tr.Shape;

		foreach ( var damageable in tr.GameObject.Components.GetAll<IDamageable>() )
		{
			damageable.OnDamage( damage );
		}

		SubtractAmmo();

		// Shitty hack
		if ( tr.GameObject.Components.TryGet<PlayerController>( out var player ) )
			BroadcastFireEffects( WorldPosition, 0, 0 );
		else
			BroadcastFireEffects( WorldPosition, tr.HitPosition, tr.Normal, true );
	}

	[Broadcast]
	public void BroadcastFireEffects( Vector3 pos, Vector3 hitPos, Vector3 normal, bool hit = false )
	{
		if ( FireSound is not null )
		{
			var sound = Sound.Play( FireSound, pos );
			if ( !sound.IsValid() )
				return;
		}

		if ( !hit )
			return;

		var decal = GameObject.Clone( "prefabs/bulletdecal.prefab", new CloneConfig { Parent = Scene.Root, StartEnabled = true } );
		decal.WorldPosition = hitPos + normal;
		decal.WorldRotation = Rotation.LookAt( -normal );
		decal.WorldScale = 1.0f;

	}

	public static void SpawnParticleEffect( ParticleSystem system, Vector3 pos )
	{
		var gb = new GameObject();

		gb.WorldPosition = pos;

		var particle = gb.Components.Create<LegacyParticleSystem>();

		particle.Particles = system;

		gb.Components.Create<Destoryer>();

		gb.NetworkSpawn( null );
	}
}

[GameResource( "Weapon Data", "weapons", "Data for a weapon", Icon = "track_changes" )]
public sealed class WeaponData : GameResource
{
	public string Name { get; set; }
	public Texture Icon { get; set; }
	public GameObject WeaponPrefab { get; set; }
}
