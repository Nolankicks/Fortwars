using Sandbox;
using Sandbox.Events;

public sealed class WallComponent : Component, IGameEventHandler<OnBuildMode>, IGameEventHandler<OnFightMode>,
IGameEventHandler<OnGameOvertimeBuild>, IGameEventHandler<OnGameEnd>, IGameEventHandler<OnGameOvertimeFight>,
IGameEventHandler<OnGameWaiting>, Component.ExecuteInEditor
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

	protected override void OnFixedUpdate()
	{
		if ( Scene.IsEditor && GameObject.NetworkMode != NetworkMode.Object )
		{
			GameObject.NetworkMode = NetworkMode.Object;
			Log.Info( "NetworkMode set to Object" );
		}
	}

	[Broadcast]
    public void ToggleEnable( bool enable )
    {
		if ( Scene.IsEditor )
			return;

        foreach ( var c in Components.GetAll() )
        {
            if ( c is WallComponent )
                continue;

            c.Enabled = enable;
        }
    }
}
