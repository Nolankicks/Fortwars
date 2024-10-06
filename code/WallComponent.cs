using Sandbox;
using Sandbox.Events;

public sealed class WallComponent : Component, IGameEventHandler<OnBuildMode>, IGameEventHandler<OnFightMode>,
IGameEventHandler<OnGameOvertimeBuild>, IGameEventHandler<OnGameEnd>, IGameEventHandler<OnGameOvertimeFight>,
IGameEventHandler<OnGameWaiting>
{
    void IGameEventHandler<OnBuildMode>.OnGameEvent( OnBuildMode eventArgs )
    {
        ToggleEnable( true );
    }

    void IGameEventHandler<OnFightMode>.OnGameEvent( OnFightMode eventArgs )
    {
        ToggleEnable( false );
    }

    void IGameEventHandler<OnGameOvertimeBuild>.OnGameEvent( OnGameOvertimeBuild eventArgs )
    {
        ToggleEnable( true );
    }

    void IGameEventHandler<OnGameEnd>.OnGameEvent( OnGameEnd eventArgs )
    {
        ToggleEnable( false );
    }

    void IGameEventHandler<OnGameOvertimeFight>.OnGameEvent( OnGameOvertimeFight eventArgs )
    {
        ToggleEnable( false );
    }

    void IGameEventHandler<OnGameWaiting>.OnGameEvent( OnGameWaiting eventArgs )
    {
        ToggleEnable( false );
    }

	[Broadcast]
    public void ToggleEnable( bool enable )
    {
        foreach ( var c in Components.GetAll() )
        {
            if ( c is WallComponent )
                continue;

            c.Enabled = enable;
        }
    }
}
