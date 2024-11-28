using Sandbox.Events;
using System;

public record DamageEvent( int Amount, GameObject Attacker, GameObject Player, Vector3 HitPos = default, Vector3 HitNormal = default ) : IGameEvent;
public record DeathEvent( GameObject Attacker, GameObject Player, Vector3 damagePos, Vector3 damageNormal ) : IGameEvent;

public record GlobalDamageEvent( int Amount, GameObject Attacker, GameObject Player, Vector3 HitPos = default ) : IGameEvent;

public partial class HealthComponent : Component
{
	[Property, Sync] public int Health { get; set; } = 100;
	[Property] public int MaxHealth { get; set; } = 100;
	[Property, Sync] public bool IsDead { get; set; } = false;
	[Property, Sync] public bool SpawnDamageIndicator { get; set; } = true;
	[Property] public Func<GameObject, int, bool> CanTakeDamage { get; set; }
	[Property] public Action<int, Vector3> OnDeathAction { get; set; }

	[Property, FeatureEnabled( "Autoheal" )] public bool AutohealEnabled { get; set; } = false;
	[Property, FeatureEnabled( "Autoheal" )] public int AutohealRate { get; set; } = 1;
	[Property, FeatureEnabled( "Autoheal" )] public float AutohealDelay { get; set; } = 5.0f;

	[Property, FeatureEnabled( "Blood" )] public bool BloodEnabled { get; set; } = false;
	[Property, Feature( "Blood" )] public GameObject BloodPrefab { get; set; }
	[Property, Feature( "Blood" )] public bool PlayOnLocal { get; set; }

	public TimeSince LastHit { get; set; }

	public virtual void OnDeath( GameObject Attacker, Vector3 damagePos, Vector3 damageNormal ) { }

	[Authority]
	public virtual void TakeDamage( GameObject Attacker, int damage = 10, Vector3 HitPos = default, Vector3 normal = default, bool spawnFlag = true )
	{
		if ( IsDead )
			return;

		if ( CanTakeDamage?.Invoke( Attacker, damage ) ?? false )
			return;

		LastHit = 0;

		var health = Health - damage;

		if ( health <= 0 )
			Health = 0;
		else
			Health = health;

		GameObject.Dispatch( new DamageEvent( damage, Attacker, GameObject, HitPos, normal ) );

		if ( BloodEnabled )
			BloodEffects( HitPos, normal );

		if ( Health <= 0 )
		{
			IsDead = true;
			GameObject.Dispatch( new DeathEvent( Attacker, GameObject, HitPos, normal ) );
			OnDeath( Attacker, HitPos, normal );
			OnDeathAction?.Invoke( damage, HitPos );

			var local = FWPlayerController.Local;
			var gs = Scene?.GetAll<GameSystem>()?.FirstOrDefault();

			if ( !local.IsValid() || (!local?.Inventory.IsValid() ?? true) )
				return;

			local.Inventory.CanPickUp = false;

			if ( gs.CurrentGameModeType == GameModeType.CaptureTheFlag && spawnFlag )
			{
				var flag = local.Inventory.Items.FirstOrDefault( x => x.Components.Get<Flag>().IsValid() )?.Components.Get<Flag>();

				Log.Info( flag.IsValid() );

				if ( flag.IsValid() )
				{
					flag.GameObject.Enabled = false;
				}

				//Since we are dropping the flag, we need to set our index to zero, since it was 2
				if ( local.Inventory.IsValid() )
					local.Inventory.Index = 0;
			}
		}
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		if ( IsAutoHealing() )
		{
			Heal( AutohealRate );
		}
	}

	public bool IsAutoHealing()
	{
		return AutohealEnabled && !IsDead && Health < MaxHealth && LastHit > AutohealDelay;
	}

	[Button]
	public void TestDamage()
	{
		TakeDamage( GameObject );
	}

	TimeSince lastHeal;

	public void Heal( int amount )
	{
		if ( lastHeal < 0.5f )
			return;

		lastHeal = 0;

		Health = Math.Min( Health + amount, MaxHealth );
	}

	[Broadcast]
	public void BroadcastGlobalDamageEvent( int Amount, GameObject Attacker, GameObject Player )
	{
		Scene.Dispatch( new GlobalDamageEvent( Amount, Attacker, Player ) );
	}

	[Broadcast]
	public void BloodEffects( Vector3 pos, Vector3 rot )
	{
		if ( !PlayOnLocal && !IsProxy )
			return;

		var go = BloodPrefab.Clone();
		go.WorldPosition = pos;
		go.WorldRotation = Rotation.LookAt( rot );
	}

	[Broadcast]
	public void ResetHealth()
	{
		Health = MaxHealth;
		IsDead = false;
	}
}
