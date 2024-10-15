using Sandbox;
using Sandbox.Events;

public sealed class RollerMine : Component, IGameEventHandler<OnGameEnd>, IGameEventHandler<OnBuildMode>,
IGameEventHandler<OnFightMode>, IGameEventHandler<OnGameOvertimeBuild>, IGameEventHandler<OnGameOvertimeFight>,
IGameEventHandler<OnGameWaiting>
{
	[Sync] public Transform StartingTransform { get; set; }
	[Sync] public bool IsGrabbed { get; set; } = false;
	[Property, Sync] public SkinnedModelRenderer Renderer { get; set; }

	protected override void OnStart()
	{
		if ( Networking.IsHost )
			StartingTransform = Transform.World;
	}

	[Authority]
	public void ResetPosition()
	{
		if ( Components.TryGet<Rigidbody>( out var rb ) )
		{
			rb.Velocity = Vector3.Zero;
			rb.AngularVelocity = Vector3.Zero;
		}

		Transform.ClearInterpolation();

		Transform.World = StartingTransform;
	}

	protected override void OnUpdate()
	{
		if ( Renderer.IsValid() )
			Renderer.Set( "b_open", IsGrabbed );
	}

	void IGameEventHandler<OnGameEnd>.OnGameEvent( OnGameEnd eventArgs )
	{
		ResetPosition();
	}

	void IGameEventHandler<OnBuildMode>.OnGameEvent( OnBuildMode eventArgs )
	{
		ResetPosition();
	}

	void IGameEventHandler<OnFightMode>.OnGameEvent( OnFightMode eventArgs )
	{
		ResetPosition();
	}

	void IGameEventHandler<OnGameOvertimeBuild>.OnGameEvent( OnGameOvertimeBuild eventArgs )
	{
		ResetPosition();
	}

	void IGameEventHandler<OnGameOvertimeFight>.OnGameEvent( OnGameOvertimeFight eventArgs )
	{
		ResetPosition();
	}

	void IGameEventHandler<OnGameWaiting>.OnGameEvent( OnGameWaiting eventArgs )
	{
		ResetPosition();
	}

	[Broadcast]
	public void SetGrabbed( bool isGrabbed )
	{
		IsGrabbed = isGrabbed;
	}
}
