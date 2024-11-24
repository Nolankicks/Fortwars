using System;
using Sandbox;
using Sandbox.Events;

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

	[Property] public bool Autoheal { get; set; } = false;

	public TimeSince LastHit { get; set; }

	public virtual void OnDeath( GameObject Attacker, Vector3 damagePos, Vector3 damageNormal ) { }

    [Authority]
    public virtual void TakeDamage( GameObject Attacker, int damage = 10, Vector3 HitPos = default, Vector3 normal = default )
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

		if ( Health <= 0 )
        {
            IsDead = true;
            GameObject.Dispatch( new DeathEvent( Attacker, GameObject, HitPos, normal ) );
			OnDeath( Attacker, HitPos, normal );
			OnDeathAction?.Invoke( damage, HitPos );
        }
    }

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		if ( IsAutoHealing() )
		{
			Heal( 1 );
		}
	}

	public bool IsAutoHealing()
	{
		return Autoheal && !IsDead && Health < MaxHealth && LastHit > 5;
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
    public void ResetHealth()
    {
        Health = MaxHealth;
        IsDead = false;
    }
}
