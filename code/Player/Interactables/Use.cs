using Sandbox;
using Sandbox.Events;

public sealed class Use : Component, IGameEventHandler<DeathEvent>, IGameEventHandler<OnRoundSwitch>
{
	public IPressable pressable { get; set; }

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		if ( Input.Pressed( "use" ) )
		{
			var local = FWPlayerController.Local;

			if ( !local.IsValid() )
				return;

			if ( !local.Eye.IsValid() )
				return;

			var eyePos = local.Eye.WorldPosition;

			var tr = Scene.Trace.Ray( eyePos, eyePos + local.EyeAngles.Forward * 1000 )
				.IgnoreGameObjectHierarchy( local.GameObject )
				.Run();

			if ( !tr.Hit )
				return;

			if ( tr.GameObject?.Root?.Components.TryGet<IPressable>( out var p, FindMode.EverythingInSelfAndChildren ) ?? false )
			{
				if ( !p.CanPress( new IPressable.Event( this ) ) )
					return;

				pressable = p;
				p.Press( new IPressable.Event( this ) );
			}
		}

		if ( Input.Released( "use" ) && pressable is not null )
		{
			pressable.Release( new IPressable.Event( this ) );
			pressable = null;
		}
	}

	void IGameEventHandler<DeathEvent>.OnGameEvent( DeathEvent eventArgs )
	{
		pressable?.Release( new IPressable.Event( this ) );
		pressable = null;
	}

	protected override void OnDisabled()
	{
		if ( pressable is not null )
		{
			pressable.Release( new IPressable.Event( this ) );
			pressable = null;
		}
	}

	void IGameEventHandler<OnRoundSwitch>.OnGameEvent( OnRoundSwitch eventArgs )
	{
		pressable?.Release( new IPressable.Event( this ) );
		pressable = null;
	}
}
