using Sandbox.Events;

public sealed class SpawnObject : Component, IGameEventHandler<DeathEvent>, IGameEventHandler<OnGameWaiting>,
IGameEventHandler<OnBuildMode>
{
	[RequireComponent] public HealthComponent Health { get; set; }
	[Property] public Team Team { get; set; }
	[Property, ReadOnly] public UpgradeLevel Level { get; set; }

	protected override void OnStart()
	{
		if ( !IsProxy )
			EnableAll( false );
	}

	void IGameEventHandler<DeathEvent>.OnGameEvent( DeathEvent eventArgs )
	{
		var gs = Scene.GetAll<GameMode>()?.FirstOrDefault();

		if ( !gs.IsValid() )
			return;

		gs.EndGame( Team );
	}

	void IGameEventHandler<OnGameWaiting>.OnGameEvent( OnGameWaiting eventArgs )
	{
		EnableAll( false );
	}

	void IGameEventHandler<OnBuildMode>.OnGameEvent( OnBuildMode eventArgs )
	{
		EnableAll();
	}

	[Broadcast]
	public void EnableAll( bool enabled = true )
	{
		foreach ( var component in GameObject.Components.GetAll().ToList() )
		{
			if ( component is not SpawnObject && component is not GameModeObject )
				component.Enabled = enabled;
		}
	}

	[Authority]
	public void Upgrade( UpgradeLevel level )
	{
		Level = level;

		switch ( level )
		{
			case UpgradeLevel.Level1:
				Health.MaxHealth = 700;
				Health.ResetHealth();
				break;
			case UpgradeLevel.Level2:
				Health.MaxHealth = 800;
				Health.ResetHealth();
				break;
			case UpgradeLevel.Level3:
				Health.MaxHealth = 900;
				Health.ResetHealth();
				break;
		}
	}

	[Button]
	public void UpgradeLevel1()
	{
		Upgrade( UpgradeLevel.Level1 );
	}

	[Button]
	public void UpgradeLevel2()
	{
		Upgrade( UpgradeLevel.Level2 );
	}

	[Button]
	public void UpgradeLevel3()
	{
		Upgrade( UpgradeLevel.Level3 );
	}

	public enum UpgradeLevel
	{
		Level1,
		Level2,
		Level3
	}
}
