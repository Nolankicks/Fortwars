using Sandbox.Citizen;
using Sandbox.Events;

public record WeaponAnimEvent( string anim, bool value ) : IGameEvent;
public record OnReloadEvent() : IGameEvent;

public class Item : Component, IGameEventHandler<OnItemEquipped>
{
	[Property, Sync, Feature( "Base Item" )] public CitizenAnimationHelper.HoldTypes HoldType { get; set; }
	[Property, Feature( "Base Item" )] public Model WorldModel { get; set; }
	[Property, Sync, Feature( "Base Item" )] public Vector3 Offset { get; set; }
	public int Ammo { get; set; }

	[Property, Feature( "Base Item" )] public bool UsesAmmo { get; set; } = true;

	[Property, ShowIf( "UsesAmmo", true ), Feature( "Base Item" )] public int MaxAmmo { get; set; } = 30;

	[Property, ShowIf( "UsesAmmo", true ), Feature( "Base Item" )] public int AmmoPerShot { get; set; } = 1;

	[Property] public Viewmodel VModel { get; set; }

	[Authority]
	public void SubtractAmmo()
	{
		if ( !UsesAmmo || Ammo <= 0 )
			return;

		Ammo -= AmmoPerShot;
	}

	public bool CanUse()
	{
		return Ammo > 0;
	}

	public void Reload()
	{
		Ammo = MaxAmmo;
		GameObject.Dispatch( new OnReloadEvent() );
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

		Sound.Play( "weapon.deploy", player.WorldPosition );
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

public class Weapon : Item, IGameEventHandler<OnReloadEvent>
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
	public virtual bool CanFire => true;

	public enum FireTypes
	{
		F_SEMIAUTO,
		F_AUTOMATIC
	}

	[Property] FireTypes FireType { get; set; } = FireTypes.F_SEMIAUTO;

	public override void OnEquip( OnItemEquipped onItemEquipped )
	{
		if ( IsProxy )
			return;

		equipTime = 0;
		reloadTime = ReloadDelay;
		lastFired = FireRate;
		GameObject.Dispatch( new WeaponAnimEvent( "b_empty", false ) );
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || equipTime < 0.2f )
			return;

		if ( (CheckFireInput() && lastFired > FireRate) && CanUse() && reloadTime > ReloadDelay && CanFire )
		{
			for ( var i = 0; i < TraceTimes; i++ )
				Shoot();

			SubtractAmmo();

			var local = PlayerController.Local;

			if ( local.IsValid() )
			{
				local.BroadcastAttack();
			}

			GameObject.Dispatch( new WeaponAnimEvent( AttackAnimName, true ) );

			lastFired = 0;
		}
		else if ( (Input.Pressed( "attack1" ) || Input.Down( "attack1" ) && lastFired > FireRate) && Ammo <= 0 && reloadTime > ReloadDelay )
		{
			TriggerReload();
		}

		if ( Input.Pressed( "reload" ) && reloadTime > ReloadDelay && Ammo != MaxAmmo )
		{
			TriggerReload();
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

		// Shitty hack
		if ( tr.GameObject.Components.TryGet<PlayerController>( out var player, FindMode.EverythingInSelfAndParent ) )
			BroadcastFireEffects( WorldPosition, 0, 0 );
		else
			BroadcastFireEffects( WorldPosition, tr.HitPosition, tr.Normal, true );

		if ( tr.GameObject.Root.Components.TryGet<RollerMine>( out var r ) )
			return;

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

			Sound.Play( "hitmarker" );
		}

		var damage = new DamageInfo( Damage, GameObject, GameObject, tr.Hitbox );
		damage.Position = tr.HitPosition;
		damage.Shape = tr.Shape;

		if ( !tr.GameObject.Root.Components.TryGet<PlayerController>( out var p, FindMode.EverythingInSelfAndParent ) && !tr.GameObject.Tags.Has( FW.Tags.Ragdoll ) )
		{
			tr.GameObject.Root.Network.TakeOwnership();

			if ( tr.Body.IsValid() && !tr.GameObject.Root.Components.TryGet<Gib>( out var g, FindMode.EverythingInSelfAndAncestors ) )
				tr.Body.BodyType = PhysicsBodyType.Static;

			foreach ( var damageable in tr.GameObject.Components.GetAll<IDamageable>() )
			{
				damageable.OnDamage( damage );
			}
		}
	}

	public void TriggerReload()
	{
		reloadTime = 0;

		GameObject.Dispatch( new WeaponAnimEvent( "b_reload", true ) );

		Invoke( ReloadDelay, () =>
		{
			Reload();
			GameObject.Dispatch( new WeaponAnimEvent( ReloadAnimName, true ) );
			GameObject.Dispatch( new WeaponAnimEvent( "b_empty", false ) );
		} );
	}

	[Broadcast]
	public void BroadcastFireEffects( Vector3 pos, Vector3 hitPos, Vector3 normal, bool hit = false )
	{
		if ( FireSound is not null )
		{
			Sound.Play( FireSound, pos );
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

	void IGameEventHandler<OnReloadEvent>.OnGameEvent( OnReloadEvent eventArgs )
	{
		if ( IsProxy )
			return;

		GameObject.Dispatch( new WeaponAnimEvent( "b_reload", false ) );
	}

	bool CheckFireInput()
	{
		if ( FireType == FireTypes.F_SEMIAUTO )
			return Input.Pressed( "attack1" );
		if ( FireType == FireTypes.F_AUTOMATIC )
			return Input.Down( "attack1" );

		return false;
	}
}

[GameResource( "Weapon Data", "weapons", "Data for a weapon", Icon = "track_changes" )]
public sealed class WeaponData : GameResource
{
	public string Name { get; set; }
	public GameObject WeaponPrefab { get; set; }
}
