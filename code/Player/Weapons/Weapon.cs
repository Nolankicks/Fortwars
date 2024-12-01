using Sandbox.Citizen;
using Sandbox.Events;
using System.Threading;
using System.Threading.Tasks;

public record WeaponAnimEvent( string anim, bool value ) : IGameEvent;
public record OnReloadEvent() : IGameEvent;

public class Item : Component, IGameEventHandler<OnItemEquipped>
{
	[Property, Sync, Feature( "Base Item" )] public CitizenAnimationHelper.HoldTypes HoldType { get; set; }
	[Property, Feature( "Base Item" )] public Model WorldModel { get; set; }
	[Property, Sync, Feature( "Base Item" )] public Vector3 Offset { get; set; }
	public int Ammo { get; set; }

	[Property, FeatureEnabled( "Ammo" )] public bool UsesAmmo { get; set; } = true;

	[Property, Feature( "Ammo" )] public int MaxAmmo { get; set; } = 30;

	[Property, Feature( "Ammo" )] public int AmmoPerShot { get; set; } = 1;
	[Property, Feature( "Ammo" )] public bool UseShotgunReload { get; set; } = false;

	[Property] public Viewmodel VModel { get; set; }

	[Property] public GameObject TracerPoint { get; set; }

	[Property, FeatureEnabled( "ADS" )] public bool ADSEnabled { get; set; } = true;
	[Property, Feature( "ADS" )] public Vector3 ADSOffset { get; set; } = new( 4, 0, 0 );
	[Property, Feature( "ADS" )] public float ADSFOV { get; set; } = 0.8f;
	[Property, Feature( "ADS" )] public bool DisableCrosshair { get; set; } = false;

	[Property, FeatureEnabled( "Crosshair" )] public bool CrosshairEnabled { get; set; }
	[Property, Feature( "Crosshair" )] public WeaponCrosshair WeaponCrosshair { get; set; }

	public virtual bool IsReloading { get; set; } = false;


	[Rpc.Owner]
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

	protected override void OnDisabled()
	{
		if ( !IsProxy )
		{
			if ( WeaponCrosshair.IsValid() )
				WeaponCrosshair.Enabled = true;

			var local = FWPlayerController.Local;

			if ( local.IsValid() )
				local.IsADS = false;
		}
	}

	[Rpc.Broadcast]
	public void DestroyViewmodelRenderers()
	{
		if ( IsProxy )
		{
			foreach ( var renderer in Components.GetAll<ModelRenderer>().ToList() )
			{
				renderer.Destroy();
			}
		}
	}

	protected override void OnUpdate()
	{
		if ( VModel.IsValid() && VModel.Renderer.IsValid() && ADSEnabled )
		{
			var isAiming = Input.Down( "attack2" ) && !IsReloading;

			var local = FWPlayerController.Local;

			if ( local.IsValid() )
				local.IsADS = isAiming;

			var Renderer = VModel.Renderer;

			Renderer.Set( "ironsights", isAiming ? 1 : 0 );

			var targetPos = isAiming ? ADSOffset : Vector3.Zero;

			Renderer.LocalPosition = Renderer.LocalPosition.LerpTo( targetPos, Time.Delta * 10 );

			if ( WeaponCrosshair.IsValid() && DisableCrosshair )
				WeaponCrosshair.Enabled = !isAiming;

			if ( CameraController.Instance.IsValid() )
			{
				CameraController.Instance.FOVMultTarget = isAiming ? 0.8f : 1.0f;
			}
		}
		else if ( !ADSEnabled )
		{
			if ( CameraController.Instance.IsValid() )
			{
				CameraController.Instance.FOVMultTarget = 1.0f;
			}
		}
	}

	public virtual void OnEquip( OnItemEquipped onItemEquipped ) { }

	void IGameEventHandler<OnItemEquipped>.OnGameEvent( OnItemEquipped eventArgs )
	{
		OnEquip( eventArgs );

		var player = FWPlayerController.Local;

		if ( IsProxy || !player.IsValid() )
			return;

		player.HoldType = HoldType;

		BroadcastEquip( player );

		Sound.Play( "weapon.deploy", player.WorldPosition );
	}

	[Rpc.Broadcast]
	public void BroadcastEquip( FWPlayerController local )
	{
		if ( !local.IsValid() )
			return;

		local.HoldRenderer.GameObject.Enabled = true;

		local.HoldRenderer.Model = WorldModel;
		local.HoldRenderer.LocalPosition = Offset;
	}
}

public class Weapon : Item, IGameEventHandler<OnReloadEvent>, IGameEventHandler<PlayerDeath>
{
	[Property] public float FireRate { get; set; } = 0.1f;
	[Property] public float ReloadDelay { get; set; } = 1f;
	[Property] public int Damage { get; set; } = 10;
	TimeSince lastFired { get; set; }
	TimeSince equipTime { get; set; }
	TimeSince reloadTime { get; set; }
	[Property] public float Range { get; set; } = 5000;
	[Property] public float MinSpread { get; set; } = 0.03f;
	[Property] public float MaxSpread { get; set; } = 0.05f;
	[Property] public int TraceTimes { get; set; } = 1;
	[Property] public SoundEvent FireSound { get; set; }
	[Property] public string AttackAnimName { get; set; } = "b_attack";
	[Property] public string ReloadAnimName { get; set; } = "b_reload";

	[Property, FeatureEnabled( "Recoil" )] public bool UsesRecoil { get; set; }
	[Property, Feature( "Recoil" )] public Vector3 MinRecoilValues { get; set; }
	[Property, Feature( "Recoil" )] public Vector3 MaxRecoilValues { get; set; }

	[Property] public float PunchDecreaseRate { get; set; } = 0.05f;
	[Property] public float PunchFireIncrease { get; set; } = 0.1f;

	// Between 0-1, increases every fire and decreases over time
	float FirePunch { get; set; } = 0.0f;

	public virtual bool CanFire => true;

	private SceneTraceResult[] Traces;

	[Property, FeatureEnabled( "Casing" )] public bool HasBulletCasing { get; set; }
	[Property, Feature( "Casing" )] public Model CasingModel { get; set; }
	[Property, Feature( "Casing" )] public GameObject EjectionPoint { get; set; }

	public enum FireTypes
	{
		F_SEMIAUTO,
		F_AUTOMATIC
	}

	[Property] FireTypes FireType { get; set; } = FireTypes.F_SEMIAUTO;
	[Property] public bool HeadShotEnabled { get; set; } = true;

	public override void OnEquip( OnItemEquipped onItemEquipped )
	{
		if ( IsProxy )
			return;

		equipTime = 0;
		reloadTime = ReloadDelay;
		lastFired = FireRate;
		GameObject.Dispatch( new WeaponAnimEvent( "b_empty", false ) );
		GameObject.Dispatch( new WeaponAnimEvent( "b_reloading", false ) );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		FirePunch = FirePunch.LerpTo( 0.0f, PunchDecreaseRate * Time.Delta );
		FirePunch = FirePunch.Clamp( 0, 1 );

		if ( IsProxy || equipTime < 0.2f )
			return;

		Traces = new SceneTraceResult[TraceTimes];

		if ( (CheckFireInput() && lastFired > FireRate) && CanUse() && reloadTime > ReloadDelay && CanFire && !IsReloading )
		{
			Hit = false;
			for ( var i = 0; i < TraceTimes; i++ )
				Shoot( i );

			if ( Hit && CrosshairEnabled )
			{
				WeaponCrosshair?.DoHitmarker( Died );
			}

			SubtractAmmo();

			var local = FWPlayerController.Local;

			if ( local.IsValid() )
			{
				local.BroadcastAttack();
			}

			CameraController.Instance.RecoilFire( GetRecoilValue() );
			FirePunch += PunchFireIncrease;
			FirePunch = FirePunch.Clamp( 0, 1 );

			BroadcastShootEffects( Traces );
			CreateMuzzleFlash();
			EjectCasing();

			//Crosshair.Instance.GapAddition += 5;

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

	private bool Hit { get; set; } = false;
	private bool Died { get; set; } = false;

	public void Shoot( int index = 0 )
	{
		var local = FWPlayerController.Local;

		var cam = Scene.Camera;

		if ( !local.IsValid() || !cam.IsValid() )
			return;

		var ray = cam.ScreenNormalToRay( 0.5f );

		ray.Forward += Vector3.Random * (MinSpread.LerpTo( MaxSpread, FirePunch ));

		var tr = Scene.Trace.Ray( ray, Range )
			.IgnoreGameObjectHierarchy( local.GameObject )
			.WithoutTags( "playercollider" )
			.UseHitboxes()
			.Run();

		Traces[index] = tr;

		if ( !tr.GameObject.IsValid() || !tr.Hit )
			return;

		//Make sure we don't freeze the rollermine
		if ( tr.GameObject.Components.TryGet<RollerMine>( out var m, FindMode.EverythingInSelfAndParent ) )
			return;

		if ( tr.GameObject.Components.TryGet<HealthComponent>( out var health, FindMode.EverythingInSelfAndParent ) && !health.IsDead )
		{
			var team = health.GameObject.Components.Get<TeamComponent>();

			if ( team.IsValid() && local.TeamComponent.IsValid() && local.TeamComponent.IsFriendly( team ) && team.Team != Team.None )
				return;

			bool IsHeadShot = false;

			var dmg = Damage;

			if ( tr.Hitbox is not null && tr.Hitbox.Tags.Has( "head" ) && HeadShotEnabled )
			{
				dmg *= 2;
				IsHeadShot = true;
			}

			health.TakeDamage( local.GameObject, dmg, tr.EndPosition, tr.Normal, boneId: tr.Hitbox?.Bone?.Index ?? 0 );

			if ( !health.IsDead )
			{
				if ( health.SpawnDamageIndicator )
				{
					var text = GameObject.Clone( ResourceLibrary.Get<PrefabFile>( "prefabs/effects/textparticle.prefab" ) );
					text.WorldPosition = tr.HitPosition + tr.Normal * 10;
					text.WorldRotation = Rotation.LookAt( -cam.WorldRotation.Forward );

					if ( text.Components.TryGet<ParticleTextRenderer>( out var textRenderer ) )
					{
						var color = IsHeadShot ? Color.Red : Color.White;

						textRenderer.Text = new TextRendering.Scope( dmg.ToString(), color, 64, "Chakra Petch", 1000 );
					}
				}

				var hud = HUD.Instance;

				if ( hud.IsValid() )
					hud.FlashHitMarker();
			}

			Hit = true;
			Died = health.IsDead;
		}

		var damage = new DamageInfo( Damage, GameObject, GameObject, tr.Hitbox );
		damage.Position = tr.HitPosition;
		damage.Shape = tr.Shape;

		if ( !tr.GameObject.Root.Components.TryGet<FWPlayerController>( out var p, FindMode.EverythingInSelfAndParent ) && !tr.GameObject.Tags.Has( FW.Tags.Ragdoll ) )
		{
			tr.GameObject.Root.Network.TakeOwnership();

			foreach ( var damageable in tr.GameObject.Components.GetAll<IDamageable>() )
			{
				damageable.OnDamage( damage );
			}
		}
	}

	public void TriggerReload()
	{
		if ( IsReloading )
			return;

		if ( UseShotgunReload )
		{
			_ = ShotgunReload();
			return;
		}

		IsReloading = true;

		reloadTime = ReloadDelay;

		GameObject.Dispatch( new WeaponAnimEvent( ReloadAnimName, true ) );

		Invoke( ReloadDelay, () =>
		{
			Reload();
			GameObject.Dispatch( new WeaponAnimEvent( ReloadAnimName, false ) );
			GameObject.Dispatch( new WeaponAnimEvent( "b_empty", false ) );
			IsReloading = false;
		} );
	}

	[Rpc.Broadcast( NetFlags.Unreliable )]
	public void BroadcastShootEffects( SceneTraceResult[] traces )
	{
		if ( traces.Any() )
		{
			foreach ( var trace in traces )
			{
				if ( trace.Hit && !trace.GameObject.Components.TryGet<FWPlayerController>( out var player )
					&& !trace.GameObject.Components.TryGet<RollerMine>( out var mine ) && !trace.GameObject.Tags.Has( "no_decals" ) )
				{
					var decal = GameObject.Clone( "prefabs/effects/bulletdecal.prefab", new CloneConfig { Parent = Scene.Root, StartEnabled = true } );
					decal.WorldPosition = trace.HitPosition + trace.Normal;
					decal.WorldRotation = Rotation.LookAt( -trace.Normal );
					decal.WorldScale = 1.0f;
					decal.SetParent( trace.GameObject );

					Sound.Play( trace.Surface.Sounds.Bullet, trace.HitPosition );
				}
				CreateTracer( trace.StartPosition, trace.Direction );


			}
		}
		if ( FireSound is not null )
		{
			var snd = Sound.Play( FireSound, WorldPosition );
			snd.TargetMixer = Sandbox.Audio.Mixer.FindMixerByName( "game" );
			if ( TracerPoint.IsValid() )
			{
				snd.FollowParent = true;
				snd.SetParent( TracerPoint );
			}
		}
	}

	public static void SpawnParticleEffect( ParticleSystem system, Vector3 pos )
	{
		return;

		var gb = new GameObject();

		gb.WorldPosition = pos;

		var particle = gb.Components.Create<LegacyParticleSystem>();

		particle.Particles = system;

		gb.Components.Create<Destoryer>();

		gb.NetworkSpawn( null );
	}

	void IGameEventHandler<PlayerDeath>.OnGameEvent( PlayerDeath eventArgs )
	{
		if ( IsProxy )
			return;

		GameObject.Dispatch( new WeaponAnimEvent( "b_reloading", false ) );

		IsReloading = false;
	}

	void IGameEventHandler<OnReloadEvent>.OnGameEvent( OnReloadEvent eventArgs )
	{
		if ( IsProxy )
			return;

		GameObject.Dispatch( new WeaponAnimEvent( "b_reload", false ) );
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		if ( IsProxy )
			return;

		GameObject.Dispatch( new WeaponAnimEvent( "b_reloading", false ) );

		IsReloading = false;
	}

	bool CheckFireInput()
	{
		if ( FireType == FireTypes.F_SEMIAUTO )
			return Input.Pressed( "attack1" );
		if ( FireType == FireTypes.F_AUTOMATIC )
			return Input.Down( "attack1" );

		return false;
	}

	void CreateTracer( Vector3 StartPos, Vector3 Normal )
	{
		var tracer = GameObject.Clone( "prefabs/effects/tracer.prefab", new CloneConfig { StartEnabled = true } );

		if ( !tracer.IsValid() || !TracerPoint.IsValid() )
			return;

		if ( IsProxy ) { tracer.WorldPosition = StartPos; }
		else { tracer.WorldPosition = TracerPoint.WorldPosition; }
		tracer.WorldRotation = Rotation.LookAt( Normal );
		tracer.WorldScale = 1.0f;
	}

	void CreateMuzzleFlash()
	{
		if ( !TracerPoint.IsValid() )
			return;

		var flash = GameObject.Clone( "prefabs/effects/muzzleflash.prefab", new CloneConfig { Parent = TracerPoint, StartEnabled = true } );
		flash.WorldRotation = TracerPoint.WorldRotation;
	}

	void EjectCasing()
	{
		var local = FWPlayerController.Local;

		if ( !HasBulletCasing || !local.IsValid() || !EjectionPoint.IsValid() )
			return;

		var casing = GameObject.Clone( "prefabs/effects/bulletcasing.prefab", new CloneConfig { StartEnabled = true } );

		casing.WorldPosition = EjectionPoint.WorldPosition;
		casing.Components.Get<ModelRenderer>().Model = CasingModel;

		var rb = casing.Components.Get<Rigidbody>();
		rb.ApplyForce( EjectionPoint.WorldRotation.Up * 10.0f + EjectionPoint.WorldRotation.Forward * 500.0f + local.shrimpleCharacterController.Velocity );
	}

	Vector3 GetRecoilValue()
	{
		return Vector3.Lerp( MinRecoilValues, MaxRecoilValues, FirePunch );
	}

	public async Task ShotgunReload()
	{
		IsReloading = true;
		GameObject.Dispatch( new WeaponAnimEvent( "b_reloading", true ) );

		int ammoToFull = MaxAmmo - Ammo;
		for ( int i = 0; i < ammoToFull; i++ )
		{
			if ( Ammo == 0 )
			{
				GameObject.Dispatch( new WeaponAnimEvent( "b_reloading_first_shell", true ) );

				await Task.DelaySeconds( 2.5f );
				if ( !GameObject.IsValid() )
				{
					IsReloading = false;
					return;
				}
				Sound.Play( "shotgun.reload" );
			}
			else
			{
				GameObject.Dispatch( new WeaponAnimEvent( "b_reloading_shell", true ) );
				Sound.Play( "shotgun.reload" );

				await Task.DelaySeconds( 0.5f );
			}

			if ( !GameObject.IsValid() )
			{
				IsReloading = false;
				return;
			}

			Ammo++;
		}

		GameObject.Dispatch( new WeaponAnimEvent( "b_reloading", false ) );
		IsReloading = false;
	}
}

[GameResource( "Weapon Data", "weapons", "Data for a weapon", Icon = "track_changes" )]
public sealed class WeaponData : GameResource
{
	public string Name { get; set; }
	public GameObject WeaponPrefab { get; set; }
}
