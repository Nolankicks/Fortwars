using Sandbox.Events;

public sealed class SpawnObject : Component, IGameEventHandler<DeathEvent>, IGameEventHandler<OnGameWaiting>,
IGameEventHandler<OnBuildMode>
{
	[RequireComponent] public HealthComponent Health { get; set; }
	[Property] public Team Team { get; set; }

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
}
